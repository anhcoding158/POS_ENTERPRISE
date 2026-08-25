using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.Services;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Support;

public sealed class RestorePreparationService : IRestorePreparationService
{
    private readonly InfrastructureOptions _options;
    private readonly IRestoreArtifactInspector _publicInspector;
    private readonly RestoreArtifactInspector _workerInspector;
    private readonly IBackupCoordinator _coordinator;
    private readonly RestoreOperationStore _store;

    public RestorePreparationService(
        IOptions<InfrastructureOptions> options,
        IRestoreArtifactInspector publicInspector,
        RestoreArtifactInspector workerInspector,
        IBackupCoordinator coordinator,
        RestoreOperationStore store)
    {
        _options = options.Value;
        _options.Validate();
        _publicInspector = publicInspector;
        _workerInspector = workerInspector;
        _coordinator = coordinator;
        _store = store;
    }

    public async Task<RestorePreparationResult> PrepareAsync(
        RestorePreparationRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid operationId = Guid.Empty;
        string? operationDirectory = null;
        string? safetyBackupPath = null;
        var preparedCommitted = false;
        var safetyBackupPhase = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request is null || !ValidateParent(request, out var executablePath))
                return Failure(RestoreExecutionStatus.ParentProcessMismatch,
                    "Restore.Preparation.ParentProcessMismatch");

            var selected = await _publicInspector.InspectAsync(
                request.SelectedArtifactPath, cancellationToken);
            if (!selected.IsRestorable || selected.ByteLength is null ||
                string.IsNullOrWhiteSpace(selected.Sha256Hex))
                return Failure(RestoreExecutionStatus.ArtifactValidationFailed,
                    "Restore.Preparation.ArtifactValidationFailed");

            if (!_coordinator.TryAcquire(out var lease) || lease is null)
                return Failure(RestoreExecutionStatus.DatabaseBusy,
                    "Restore.Preparation.DatabaseBusy");

            await using (lease)
            {
                safetyBackupPhase = true;
                var active = ValidateActiveDatabasePath();
                var databaseDirectory = Path.GetDirectoryName(active)!;
                var safetyDirectory = Path.Combine(databaseDirectory, "backups", "pre-restore");
                EnsureSafeCreationBoundary(databaseDirectory, safetyDirectory);

                var backup = SqliteDatabaseSafetyService.CreateVerifiedBackup(
                    active, safetyDirectory, DateTimeOffset.UtcNow,
                    SqliteBackupArtifactKind.PreRestore);
                if (!backup.IsSuccess || string.IsNullOrWhiteSpace(backup.BackupFilePath))
                    return Failure(RestoreExecutionStatus.PreRestoreBackupFailed,
                        "Restore.Preparation.PreRestoreBackupFailed");
                safetyBackupPath = backup.BackupFilePath;
                var safety = await _workerInspector.InspectInternalAsync(
                    safetyBackupPath,
                    RestoreArtifactInspectionMode.WorkerActiveDatabaseVerification,
                    cancellationToken);
                if (!safety.IsRestorable || safety.ByteLength is null ||
                    string.IsNullOrWhiteSpace(safety.Sha256Hex))
                    return Failure(RestoreExecutionStatus.PreRestoreBackupFailed,
                        "Restore.Preparation.PreRestoreVerificationFailed");
                if (!await CheckpointWalAsync(active, cancellationToken))
                    return Failure(RestoreExecutionStatus.DatabaseBusy,
                        "Restore.Preparation.DatabaseBusy");
                var original = await SnapshotAsync(active, cancellationToken);
                safetyBackupPhase = false;

                operationId = Guid.NewGuid();
                _store.CreateOperationDirectory(operationId);
                operationDirectory = _store.GetOperationDirectory(operationId);
                var stagedPath = Path.Combine(operationDirectory, "candidate.db");
                await CopyCreateNewAsync(request.SelectedArtifactPath, stagedPath, cancellationToken);

                var sourceAfter = await _publicInspector.InspectAsync(
                    request.SelectedArtifactPath, cancellationToken);
                if (!sourceAfter.IsRestorable || sourceAfter.ByteLength != selected.ByteLength ||
                    !string.Equals(sourceAfter.Sha256Hex, selected.Sha256Hex,
                        StringComparison.OrdinalIgnoreCase))
                    return Failure(RestoreExecutionStatus.SourceChanged,
                        "Restore.Preparation.SourceChanged", operationId);

                var staged = await _workerInspector.InspectInternalAsync(
                    stagedPath,
                    RestoreArtifactInspectionMode.WorkerActiveDatabaseVerification,
                    cancellationToken);
                if (!staged.IsRestorable || staged.ByteLength != selected.ByteLength ||
                    !string.Equals(staged.Sha256Hex, selected.Sha256Hex,
                        StringComparison.OrdinalIgnoreCase))
                    return Failure(RestoreExecutionStatus.StagingFailed,
                        "Restore.Preparation.StagingVerificationFailed", operationId);

                cancellationToken.ThrowIfCancellationRequested();
                var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var now = DateTimeOffset.UtcNow;
                var markerPath = _store.GetPlanPath(operationId);
                var plan = new RestoreOperationPlan
                {
                    FormatVersion = RestoreOperationStore.CurrentFormatVersion,
                    OperationId = operationId,
                    OperationTokenSha256Hex = RestoreOperationStore.HashToken(rawToken),
                    CreatedUtc = now,
                    LastUpdatedUtc = now,
                    State = RestoreOperationState.Prepared,
                    ParentProcessId = request.ParentProcessId,
                    ParentProcessStartTimeUtcTicks = request.ParentProcessStartTimeUtc.UtcTicks,
                    ExpectedExecutablePath = executablePath,
                    ActiveDatabasePath = active,
                    OriginalDatabaseByteLength = original.Length,
                    OriginalDatabaseSha256Hex = original.Hash,
                    StagedCandidatePath = stagedPath,
                    CandidateByteLength = staged.ByteLength.Value,
                    CandidateSha256Hex = staged.Sha256Hex!,
                    CandidateSchemaCompatibility = staged.SchemaCompatibility,
                    CandidateAppliedMigrationCount = staged.AppliedMigrationCount,
                    CandidateLatestMigrationId = staged.LatestAppliedMigrationId,
                    SafetyBackupPath = safetyBackupPath,
                    SafetyBackupByteLength = safety.ByteLength.Value,
                    SafetyBackupSha256Hex = safety.Sha256Hex!,
                    RollbackPath = Path.Combine(operationDirectory, "original.rollback.db"),
                    FailedCandidatePath = Path.Combine(operationDirectory, "failed-candidate.db"),
                    OperationMarkerPath = markerPath,
                    ResultMarkerPath = Path.Combine(operationDirectory, RestoreOperationStore.ResultFileName)
                };
                await _store.WriteNewAsync(plan, cancellationToken);
                preparedCommitted = true;
                return new RestorePreparationResult(
                    RestoreExecutionStatus.Success,
                    "Restore.Preparation.Prepared",
                    operationId,
                    RestoreOperationState.Prepared,
                    markerPath,
                    rawToken,
                    Path.GetFileName(safetyBackupPath),
                    safety.ByteLength,
                    safety.Sha256Hex);
            }
        }
        catch (OperationCanceledException)
        {
            return Failure(RestoreExecutionStatus.Cancelled,
                "Restore.Preparation.Cancelled", operationId);
        }
        catch (RestoreOperationStoreException exception)
        {
            return Failure(exception.Status, "Restore.Preparation.UnsafePath", operationId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return safetyBackupPhase
                ? Failure(RestoreExecutionStatus.PreRestoreBackupFailed,
                    "Restore.Preparation.PreRestoreBackupFailed", operationId)
                : Failure(RestoreExecutionStatus.StagingFailed,
                    "Restore.Preparation.StagingFailed", operationId);
        }
        catch
        {
            return Failure(RestoreExecutionStatus.UnexpectedFailure,
                "Restore.Preparation.UnexpectedFailure", operationId);
        }
        finally
        {
            if (!preparedCommitted && operationDirectory is not null)
                DeleteUncommittedOperationBestEffort(operationDirectory);
        }
    }

    private string ValidateActiveDatabasePath()
    {
        var path = DatabasePathResolver.ResolveDatabasePathWithoutCreatingDirectory(_options.DatabasePath);
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal) ||
            !File.Exists(path))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
        EnsureNoReparseAncestors(path);
        return path;
    }

    private static bool ValidateParent(RestorePreparationRequest request, out string? executablePath)
    {
        executablePath = null;
        if (request.ParentProcessId <= 0 || request.ParentProcessStartTimeUtc <= DateTimeOffset.UnixEpoch)
            return false;
        try
        {
            using var process = Process.GetProcessById(request.ParentProcessId);
            if (process.StartTime.ToUniversalTime().Ticks != request.ParentProcessStartTimeUtc.UtcTicks)
                return false;
            try { executablePath = process.MainModule?.FileName is { } path ? Path.GetFullPath(path) : null; }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or
                InvalidOperationException or NotSupportedException) { }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
            System.ComponentModel.Win32Exception) { return false; }
    }

    private static async Task CopyCreateNewAsync(
        string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await input.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
        output.Flush(flushToDisk: true);
    }

    private static async Task<(long Length, string Hash)> SnapshotAsync(
        string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return (info.Length, Convert.ToHexString(hash));
    }

    private static async Task<bool> CheckpointWalAsync(
        string path, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) && reader.GetInt32(0) == 0;
    }

    private static void EnsureSafeCreationBoundary(string databaseDirectory, string target)
    {
        EnsureNoReparseAncestors(databaseDirectory);
        var relative = Path.GetRelativePath(databaseDirectory, target);
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
            throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
        for (var current = new DirectoryInfo(target); current is not null; current = current.Parent)
        {
            if (!current.Exists) continue;
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
            if (string.Equals(current.FullName, databaseDirectory, StringComparison.OrdinalIgnoreCase)) break;
        }
    }

    private static void EnsureNoReparseAncestors(string path)
    {
        for (var current = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!);
             current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new RestoreOperationStoreException(RestoreExecutionStatus.UnsafeDatabasePath);
    }

    private static void DeleteUncommittedOperationBestEffort(string operationDirectory)
    {
        try
        {
            if (!Directory.Exists(operationDirectory)) return;
            var attributes = File.GetAttributes(operationDirectory);
            if ((attributes & FileAttributes.ReparsePoint) == 0)
                Directory.Delete(operationDirectory, recursive: true);
        }
        catch { }
    }

    private static RestorePreparationResult Failure(
        RestoreExecutionStatus status, string messageKey, Guid operationId = default) =>
        new(status, messageKey, operationId, null, null, null, null, null, null);
}
