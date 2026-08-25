using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Support;

public sealed class RestoreWorkerService
{
    private readonly InfrastructureOptions _options;
    private readonly RestoreOperationStore _store;
    private readonly RestoreArtifactInspector _inspector;
    private readonly IRestoreWorkerRuntime _runtime;
    private readonly TimeSpan _parentExitTimeout;

    public RestoreWorkerService(
        IOptions<InfrastructureOptions> options,
        RestoreOperationStore store,
        RestoreArtifactInspector inspector)
        : this(options, store, inspector, new SystemRestoreWorkerRuntime(), TimeSpan.FromSeconds(30)) { }

    internal RestoreWorkerService(
        IOptions<InfrastructureOptions> options,
        RestoreOperationStore store,
        RestoreArtifactInspector inspector,
        IRestoreWorkerRuntime runtime,
        TimeSpan parentExitTimeout)
    {
        _options = options.Value;
        _options.Validate();
        _store = store;
        _inspector = inspector;
        _runtime = runtime;
        _parentExitTimeout = parentExitTimeout;
    }

    public async Task<RestoreExecutionResult> ExecuteAsync(
        string planPath,
        Guid operationId,
        string oneTimeOperationToken,
        CancellationToken cancellationToken = default)
    {
        RestoreOperationPlan plan;
        try
        {
            plan = await _store.ReadAndValidateAsync(
                planPath, operationId, oneTimeOperationToken, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            return Result(RestoreExecutionStatus.Cancelled, "Restore.Worker.Cancelled", operationId, null);
        }
        catch (RestoreOperationStoreException exception)
        {
            return Result(exception.Status,
                exception.Status == RestoreExecutionStatus.InvalidOperationToken
                    ? "Restore.Worker.InvalidToken" : "Restore.Worker.InvalidPlan",
                operationId, null);
        }

        try
        {
            if (plan.State == RestoreOperationState.Verified)
                return Result(RestoreExecutionStatus.Success, "Restore.Worker.Verified",
                    operationId, plan, restartRequired: true);
            if (plan.State == RestoreOperationState.RolledBack)
                return Result(RestoreExecutionStatus.RollbackSucceeded,
                    "Restore.Worker.RollbackSucceeded", operationId, plan,
                    rollbackAttempted: true, rollbackCompleted: true,
                    restartRequired: true);
            if (plan.State == RestoreOperationState.RollbackFailed)
                return Result(RestoreExecutionStatus.RollbackFailed,
                    "Restore.Worker.RecoveryRequired", operationId, plan,
                    rollbackAttempted: true);

            if (plan.State is RestoreOperationState.Prepared or
                RestoreOperationState.WaitingForParentExit)
            {
                if (plan.State == RestoreOperationState.Prepared)
                    plan = await _store.TransitionAsync(plan,
                        RestoreOperationState.WaitingForParentExit, null, CancellationToken.None);
                var parent = await WaitForRecordedParentAsync(plan, CancellationToken.None);
                if (parent is not null)
                    return Result(parent.Value.Status, parent.Value.MessageKey, operationId, plan);
            }

            ValidateActiveDatabase(plan);
            ValidateAndCleanupSidecars(plan.ActiveDatabasePath);
            ProbeExclusiveAccess(plan.ActiveDatabasePath);

            if (plan.State == RestoreOperationState.WaitingForParentExit)
                plan = await _store.TransitionAsync(plan,
                    RestoreOperationState.ReplacementStarted, null, CancellationToken.None);

            if (plan.State == RestoreOperationState.ReplacementStarted)
            {
                var active = await SnapshotAsync(plan.ActiveDatabasePath, CancellationToken.None);
                var activeIsOriginal = Matches(active, plan.OriginalDatabaseByteLength,
                    plan.OriginalDatabaseSha256Hex);
                var activeIsCandidate = Matches(active, plan.CandidateByteLength,
                    plan.CandidateSha256Hex);
                if (activeIsCandidate && File.Exists(plan.RollbackPath))
                {
                    plan = await _store.TransitionAsync(plan,
                        RestoreOperationState.CandidateInstalled, null, CancellationToken.None);
                }
                else if (activeIsOriginal && File.Exists(plan.StagedCandidatePath))
                {
                    var staged = await VerifyCandidateAsync(plan.StagedCandidatePath, plan);
                    if (!staged)
                        return Result(RestoreExecutionStatus.SourceChanged,
                            "Restore.Worker.CandidateChanged", operationId, plan);
                    try
                    {
                        _runtime.Replace(plan.StagedCandidatePath,
                            plan.ActiveDatabasePath, plan.RollbackPath);
                    }
                    catch
                    {
                        return Result(RestoreExecutionStatus.ReplacementFailed,
                            "Restore.Worker.ReplacementFailed", operationId, plan);
                    }
                    plan = await _store.TransitionAsync(plan,
                        RestoreOperationState.CandidateInstalled, null, CancellationToken.None);
                }
                else
                {
                    return Result(RestoreExecutionStatus.RecoveryRequired,
                        "Restore.Worker.AmbiguousReplacementState", operationId, plan);
                }
            }

            if (plan.State == RestoreOperationState.CandidateInstalled)
            {
                if (await VerifyCandidateAsync(plan.ActiveDatabasePath, plan) &&
                    !_runtime.ForcePostInstallVerificationFailure)
                {
                    ValidateAndCleanupSidecars(plan.ActiveDatabasePath);
                    plan = await _store.TransitionAsync(plan,
                        RestoreOperationState.Verified, null, CancellationToken.None);
                    var success = Result(RestoreExecutionStatus.Success,
                        "Restore.Worker.Success", operationId, plan, restartRequired: true);
                    await RestoreOperationStore.WriteResultAsync(plan, success, CancellationToken.None);
                    return success;
                }
                return await RollbackAsync(plan,
                    "Restore.Worker.PostRestoreVerificationFailed");
            }

            if (plan.State == RestoreOperationState.RollbackStarted)
                return await RollbackAsync(plan, "Restore.Worker.ResumeRollback");

            return Result(RestoreExecutionStatus.InvalidPlan,
                "Restore.Worker.InvalidState", operationId, plan);
        }
        catch (OperationCanceledException)
        {
            return Result(RestoreExecutionStatus.Cancelled,
                "Restore.Worker.CancelledBeforeReplacement", operationId, plan);
        }
        catch (RestoreOperationStoreException exception)
        {
            return Result(exception.Status, "Restore.Worker.InvalidPlan", operationId, plan);
        }
        catch (UnsafeSidecarException)
        {
            return Result(RestoreExecutionStatus.UnsafeSidecar,
                "Restore.Worker.UnsafeSidecar", operationId, plan);
        }
        catch (IOException)
        {
            if (plan.State is (RestoreOperationState.ReplacementStarted or
                RestoreOperationState.CandidateInstalled or RestoreOperationState.RollbackStarted) &&
                File.Exists(plan.RollbackPath))
                return await RollbackAsync(plan, "Restore.Worker.DurableUpdateFailed");
            return Result(RestoreExecutionStatus.DatabaseBusy,
                "Restore.Worker.DatabaseBusy", operationId, plan);
        }
        catch (UnauthorizedAccessException)
        {
            if (plan.State is (RestoreOperationState.ReplacementStarted or
                RestoreOperationState.CandidateInstalled or RestoreOperationState.RollbackStarted) &&
                File.Exists(plan.RollbackPath))
                return await RollbackAsync(plan, "Restore.Worker.DurableUpdateFailed");
            return Result(RestoreExecutionStatus.DatabaseBusy,
                "Restore.Worker.DatabaseBusy", operationId, plan);
        }
        catch
        {
            return Result(RestoreExecutionStatus.UnexpectedFailure,
                "Restore.Worker.UnexpectedFailure", operationId, plan);
        }
    }

    private async Task<RestoreExecutionResult> RollbackAsync(
        RestoreOperationPlan plan, string failureCode)
    {
        try
        {
            if (plan.State != RestoreOperationState.RollbackStarted)
                plan = await _store.TransitionAsync(plan,
                    RestoreOperationState.RollbackStarted, failureCode, CancellationToken.None);

            var active = await SnapshotAsync(plan.ActiveDatabasePath, CancellationToken.None);
            if (Matches(active, plan.OriginalDatabaseByteLength, plan.OriginalDatabaseSha256Hex))
            {
                plan = await _store.TransitionAsync(plan,
                    RestoreOperationState.RolledBack, failureCode, CancellationToken.None);
                return Result(RestoreExecutionStatus.RollbackSucceeded,
                    "Restore.Worker.RollbackSucceeded", plan.OperationId, plan,
                    rollbackAttempted: true, rollbackCompleted: true,
                    restartRequired: true);
            }

            if (!File.Exists(plan.RollbackPath)) throw new IOException("Rollback artifact missing.");
            if (File.Exists(plan.FailedCandidatePath)) throw new IOException("Failed candidate path occupied.");
            _runtime.Replace(plan.RollbackPath, plan.ActiveDatabasePath, plan.FailedCandidatePath);
            var restored = await SnapshotAsync(plan.ActiveDatabasePath, CancellationToken.None);
            var inspection = await _inspector.InspectInternalAsync(
                plan.ActiveDatabasePath,
                RestoreArtifactInspectionMode.WorkerActiveDatabaseVerification,
                CancellationToken.None);
            if (!Matches(restored, plan.OriginalDatabaseByteLength, plan.OriginalDatabaseSha256Hex) ||
                !inspection.IsRestorable)
                throw new IOException("Rollback verification failed.");

            plan = await _store.TransitionAsync(plan,
                RestoreOperationState.RolledBack, failureCode, CancellationToken.None);
            var result = Result(RestoreExecutionStatus.RollbackSucceeded,
                "Restore.Worker.RollbackSucceeded", plan.OperationId, plan,
                rollbackAttempted: true, rollbackCompleted: true,
                restartRequired: true);
            await RestoreOperationStore.WriteResultAsync(plan, result, CancellationToken.None);
            return result;
        }
        catch
        {
            try
            {
                if (plan.State != RestoreOperationState.RollbackFailed)
                    plan = await _store.TransitionAsync(plan,
                        RestoreOperationState.RollbackFailed, failureCode, CancellationToken.None);
            }
            catch { }
            return Result(RestoreExecutionStatus.RollbackFailed,
                "Restore.Worker.RollbackFailed", plan.OperationId, plan,
                rollbackAttempted: true);
        }
    }

    private async Task<(RestoreExecutionStatus Status, string MessageKey)?> WaitForRecordedParentAsync(
        RestoreOperationPlan plan, CancellationToken cancellationToken)
    {
        var identity = _runtime.GetProcessIdentity(plan.ParentProcessId);
        if (identity is null) return null;
        if (identity.Value.StartTimeUtcTicks != plan.ParentProcessStartTimeUtcTicks ||
            (plan.ExpectedExecutablePath is not null && identity.Value.ExecutablePath is not null &&
             !string.Equals(Path.GetFullPath(identity.Value.ExecutablePath),
                 Path.GetFullPath(plan.ExpectedExecutablePath), StringComparison.OrdinalIgnoreCase)))
            return (RestoreExecutionStatus.ParentProcessMismatch,
                "Restore.Worker.ParentProcessMismatch");
        var exited = await _runtime.WaitForExitAsync(
            plan.ParentProcessId, _parentExitTimeout, cancellationToken);
        return exited ? null : (RestoreExecutionStatus.ParentExitTimeout,
            "Restore.Worker.ParentExitTimeout");
    }

    private void ValidateActiveDatabase(RestoreOperationPlan plan)
    {
        var resolved = DatabasePathResolver.ResolveDatabasePathWithoutCreatingDirectory(_options.DatabasePath);
        if (!string.Equals(Path.GetFullPath(resolved), Path.GetFullPath(plan.ActiveDatabasePath),
                StringComparison.OrdinalIgnoreCase) || !File.Exists(resolved) ||
            resolved.StartsWith("\\\\", StringComparison.Ordinal))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
        var attributes = File.GetAttributes(resolved);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
    }

    private static void ValidateAndCleanupSidecars(string activeDatabasePath)
    {
        foreach (var sidecar in new[] { activeDatabasePath + "-wal", activeDatabasePath + "-shm" })
        {
            if (!File.Exists(sidecar) && !Directory.Exists(sidecar)) continue;
            var attributes = File.GetAttributes(sidecar);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new UnsafeSidecarException();
            File.Delete(sidecar);
        }
    }

    private static void ProbeExclusiveAccess(string activeDatabasePath)
    {
        using var stream = new FileStream(activeDatabasePath, FileMode.Open,
            FileAccess.ReadWrite, FileShare.None);
    }

    private async Task<bool> VerifyCandidateAsync(string path, RestoreOperationPlan plan)
    {
        if (!File.Exists(path)) return false;
        var snapshot = await SnapshotAsync(path, CancellationToken.None);
        if (!Matches(snapshot, plan.CandidateByteLength, plan.CandidateSha256Hex)) return false;
        var inspection = await _inspector.InspectInternalAsync(path,
            RestoreArtifactInspectionMode.WorkerActiveDatabaseVerification,
            CancellationToken.None);
        return inspection.IsRestorable && inspection.ByteLength == plan.CandidateByteLength &&
            string.Equals(inspection.Sha256Hex, plan.CandidateSha256Hex,
                StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(long Length, string Hash)> SnapshotAsync(
        string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return (info.Length, Convert.ToHexString(hash));
    }

    private static bool Matches((long Length, string Hash) snapshot, long length, string hash) =>
        snapshot.Length == length && string.Equals(snapshot.Hash, hash,
            StringComparison.OrdinalIgnoreCase);

    private static RestoreExecutionResult Result(
        RestoreExecutionStatus status, string messageKey, Guid operationId,
        RestoreOperationPlan? plan, bool rollbackAttempted = false,
        bool rollbackCompleted = false, bool restartRequired = false) =>
        new(status, messageKey, operationId, plan?.State,
            plan?.CandidateByteLength, plan?.CandidateSha256Hex,
            plan?.SafetyBackupByteLength, plan?.SafetyBackupSha256Hex,
            rollbackAttempted, rollbackCompleted, restartRequired);

    private sealed class UnsafeSidecarException : Exception;
}

internal readonly record struct RestoreProcessIdentity(
    long StartTimeUtcTicks,
    string? ExecutablePath);

internal interface IRestoreWorkerRuntime
{
    RestoreProcessIdentity? GetProcessIdentity(int processId);
    Task<bool> WaitForExitAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken);
    void Replace(string source, string destination, string backup);
    bool ForcePostInstallVerificationFailure { get; }
}

internal sealed class SystemRestoreWorkerRuntime : IRestoreWorkerRuntime
{
    public bool ForcePostInstallVerificationFailure => false;

    public RestoreProcessIdentity? GetProcessIdentity(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            string? executable = null;
            try { executable = process.MainModule?.FileName; }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or
                NotSupportedException) { }
            return new(process.StartTime.ToUniversalTime().Ticks, executable);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
            Win32Exception) { return null; }
    }

    public async Task<bool> WaitForExitAsync(
        int processId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            try { await process.WaitForExitAsync(timeoutCts.Token); return true; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
        }
        catch (ArgumentException) { return true; }
    }

    public void Replace(string source, string destination, string backup) =>
        File.Replace(source, destination, backup, ignoreMetadataErrors: true);
}
