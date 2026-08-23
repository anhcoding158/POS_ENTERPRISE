using System.IO;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Services;

namespace POS.Infrastructure.Support;

public sealed class AutomaticBackupService(
    IServiceScopeFactory scopeFactory,
    AutomaticBackupPathProvider paths,
    IAutomaticBackupStateStore stateStore,
    AutomaticBackupRetentionService retention,
    IAutomaticBackupStatusSource statusSource,
    IClock clock,
    AutomaticBackupPolicy policy) : IAutomaticBackupService
{
    public async Task<AutomaticBackupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var attemptedAt = clock.UtcNow.ToUniversalTime();
        string? pendingManualArtifact = null;
        statusSource.Publish(new(AutomaticBackupStatus.Running));
        try
        {
            Directory.CreateDirectory(paths.Root);
            if (!paths.IsManagedRootSafe())
                return await FailAsync(attemptedAt, AutomaticBackupStatus.Failed, cancellationToken);
            var rootAttributes = File.GetAttributes(paths.Root);
            if ((rootAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
                return await FailAsync(attemptedAt, AutomaticBackupStatus.Failed, cancellationToken);

            await using var scope = scopeFactory.CreateAsyncScope();
            var manual = scope.ServiceProvider.GetRequiredService<IManualBackupService>();
            var manualResult = await manual.BackupAsync(new ManualBackupRequest(paths.Root), cancellationToken);
            if (manualResult.Status == ManualBackupStatus.Busy)
                return await FailAsync(attemptedAt, AutomaticBackupStatus.DeferredBusy, cancellationToken);
            if (manualResult.Status == ManualBackupStatus.Cancelled)
                return await FailAsync(attemptedAt, AutomaticBackupStatus.Cancelled, cancellationToken);
            if (!manualResult.IsSuccess || manualResult.BackupFilePath is null ||
                manualResult.CompletedAtUtc is null || manualResult.BackupFileSizeBytes is null ||
                string.IsNullOrWhiteSpace(manualResult.Sha256Hex))
                return await FailAsync(attemptedAt, AutomaticBackupStatus.Failed, cancellationToken);

            var manualPath = Path.GetFullPath(manualResult.BackupFilePath);
            if (!string.Equals(Path.GetDirectoryName(manualPath), paths.Root, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(manualPath) ||
                (File.GetAttributes(manualPath) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                return await FailAsync(attemptedAt, AutomaticBackupStatus.Failed, cancellationToken);
            pendingManualArtifact = manualPath;
            cancellationToken.ThrowIfCancellationRequested();
            var identifier = paths.CreateArtifactIdentifier(manualResult.CompletedAtUtc.Value);
            var finalPath = Path.Combine(paths.Root, identifier);
            File.Move(manualPath, finalPath, overwrite: false);
            pendingManualArtifact = null;
            var completedAt = clock.UtcNow.ToUniversalTime();
            var successState = new AutomaticBackupState
            {
                LastVerifiedSuccessUtc = completedAt,
                LastVerifiedArtifact = identifier,
                LastVerifiedByteLength = manualResult.BackupFileSizeBytes,
                LastVerifiedSha256 = manualResult.Sha256Hex,
                LastAttemptUtc = attemptedAt,
                LastResult = AutomaticBackupStatus.Success,
                NextAttemptUtc = completedAt + policy.DueInterval
            };
            try
            {
                await stateStore.WriteAsync(successState, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                var persistenceFailure = new AutomaticBackupResult(AutomaticBackupStatus.Failed, attemptedAt,
                    completedAt, identifier, manualResult.BackupFileSizeBytes, manualResult.Sha256Hex,
                    "Artifact đã verify nhưng không thể ghi trạng thái automatic backup.");
                statusSource.Publish(new(AutomaticBackupStatus.Failed, null, identifier, persistenceFailure.Warning));
                return persistenceFailure;
            }

            var retentionResult = await retention.PruneAsync(identifier, cancellationToken);
            var finalStatus = retentionResult.HasWarning
                ? AutomaticBackupStatus.SuccessWithRetentionWarning : AutomaticBackupStatus.Success;
            if (retentionResult.HasWarning)
            {
                successState = successState with
                {
                    LastResult = finalStatus,
                    LastRetentionWarning = retentionResult.Warning
                };
                try { await stateStore.WriteAsync(successState, cancellationToken); } catch { }
            }
            var result = new AutomaticBackupResult(finalStatus, attemptedAt, completedAt, identifier,
                manualResult.BackupFileSizeBytes, manualResult.Sha256Hex, retentionResult.Warning);
            statusSource.Publish(new(finalStatus, completedAt, identifier, retentionResult.Warning));
            return result;
        }
        catch (OperationCanceledException)
        {
            DeletePendingManualArtifact(pendingManualArtifact);
            statusSource.Publish(new(AutomaticBackupStatus.Cancelled));
            return new(AutomaticBackupStatus.Cancelled, attemptedAt);
        }
        catch
        {
            DeletePendingManualArtifact(pendingManualArtifact);
            return await FailAsync(attemptedAt, AutomaticBackupStatus.Failed, CancellationToken.None);
        }
    }

    private void DeletePendingManualArtifact(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!string.Equals(Path.GetDirectoryName(fullPath), paths.Root, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullPath).StartsWith("pos-enterprise-pre-migration-", StringComparison.Ordinal) ||
                (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0) return;
            File.Delete(fullPath);
        }
        catch { }
    }

    private async Task<AutomaticBackupResult> FailAsync(
        DateTimeOffset attemptedAt, AutomaticBackupStatus resultStatus, CancellationToken cancellationToken)
    {
        AutomaticBackupState? previous = null;
        try
        {
            var read = await stateStore.ReadAsync(cancellationToken);
            if (read.Status == AutomaticBackupStateReadStatus.Valid) previous = read.State;
        }
        catch { }
        var state = new AutomaticBackupState
        {
            LastVerifiedSuccessUtc = previous?.LastVerifiedSuccessUtc,
            LastVerifiedArtifact = previous?.LastVerifiedArtifact,
            LastVerifiedByteLength = previous?.LastVerifiedByteLength,
            LastVerifiedSha256 = previous?.LastVerifiedSha256,
            LastAttemptUtc = attemptedAt,
            LastResult = resultStatus,
            NextAttemptUtc = attemptedAt + policy.RetryInterval,
            LastRetentionWarning = previous?.LastRetentionWarning
        };
        try { await stateStore.WriteAsync(state, cancellationToken); } catch { }
        statusSource.Publish(new(resultStatus));
        return new(resultStatus, attemptedAt);
    }
}
