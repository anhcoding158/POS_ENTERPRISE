using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Services;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Support;

public sealed class ManualBackupService : IManualBackupService
{
    private readonly InfrastructureOptions _infrastructureOptions;
    private readonly PosDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IBackupCoordinator _coordinator;

    public ManualBackupService(
        IOptions<InfrastructureOptions> infrastructureOptions,
        PosDbContext dbContext,
        IClock clock,
        IBackupCoordinator coordinator)
    {
        _infrastructureOptions = infrastructureOptions?.Value ??
            throw new ArgumentNullException(nameof(infrastructureOptions));
        _infrastructureOptions.Validate();
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public async Task<ManualBackupResult> BackupAsync(
        ManualBackupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ManualBackupResult.Failure(ManualBackupStatus.InvalidDestination);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ManualBackupResult.Failure(ManualBackupStatus.Cancelled);
        }

        if (!_coordinator.TryAcquire(out var lease) || lease is null)
        {
            return ManualBackupResult.Failure(ManualBackupStatus.Busy);
        }

        await using (lease)
        {
            return await BackupCoreAsync(request, cancellationToken);
        }
    }

    private async Task<ManualBackupResult> BackupCoreAsync(
        ManualBackupRequest request,
        CancellationToken cancellationToken)
    {

        string destinationDirectory;
        try
        {
            if (string.IsNullOrWhiteSpace(request.DestinationDirectory) ||
                !Path.IsPathFullyQualified(request.DestinationDirectory))
            {
                return ManualBackupResult.Failure(ManualBackupStatus.InvalidDestination);
            }

            destinationDirectory = Path.GetFullPath(request.DestinationDirectory);
            if (!Directory.Exists(destinationDirectory))
            {
                return ManualBackupResult.Failure(ManualBackupStatus.InvalidDestination);
            }

            var attributes = File.GetAttributes(destinationDirectory);
            if ((attributes & FileAttributes.Directory) == 0 ||
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                return ManualBackupResult.Failure(ManualBackupStatus.InvalidDestination);
            }
        }
        catch (Exception exception) when (IsDestinationException(exception))
        {
            return ManualBackupResult.Failure(ManualBackupStatus.InvalidDestination);
        }

        var sourceDatabasePath =
            DatabasePathResolver.ResolveDatabasePathWithoutCreatingDirectory(
                _infrastructureOptions.DatabasePath);

        if (!File.Exists(sourceDatabasePath))
        {
            return ManualBackupResult.Failure(ManualBackupStatus.SourceUnavailable);
        }

        if (!SqliteDatabaseSafetyService.CheckIntegrity(sourceDatabasePath).IsSuccess)
        {
            return ManualBackupResult.Failure(ManualBackupStatus.VerificationFailed);
        }

        var sourceSchemaVerified = await VerifySchemaCompatibilityAsync(
            sourceDatabasePath,
            cancellationToken);

        if (!sourceSchemaVerified)
        {
            return ManualBackupResult.Failure(ManualBackupStatus.VerificationFailed);
        }

        var backupResult = SqliteDatabaseSafetyService.CreateVerifiedBackup(
            sourceDatabasePath,
            destinationDirectory,
            _clock.UtcNow);

        if (!backupResult.IsSuccess ||
            string.IsNullOrWhiteSpace(backupResult.BackupFilePath))
        {
            return MapFailure(backupResult.Message);
        }

        var backupPath = backupResult.BackupFilePath;
        try
        {
            if (!SqliteDatabaseSafetyService.CheckIntegrity(backupPath).IsSuccess)
                return VerificationFailureWithCleanup(backupPath, destinationDirectory);

            var backupSchemaVerified = await VerifySchemaCompatibilityAsync(
                backupPath,
                cancellationToken);

            if (!backupSchemaVerified)
                return VerificationFailureWithCleanup(backupPath, destinationDirectory);

            var fileInfo = new FileInfo(backupPath);
            if (!fileInfo.Exists || fileInfo.Length <= 0)
                return VerificationFailureWithCleanup(backupPath, destinationDirectory);

            var sha256Hex = await ComputeSha256Async(backupPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(sha256Hex))
                return VerificationFailureWithCleanup(backupPath, destinationDirectory);

            return ManualBackupResult.Success(
                backupPath,
                fileInfo.Length,
                sha256Hex,
                _clock.UtcNow);
        }
        catch
        {
            DeleteOperationOutput(backupPath, destinationDirectory);
            throw;
        }
    }

    private static async Task<bool> VerifySchemaCompatibilityAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(
            new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Private,
                Pooling = false
            }.ToString());

        await using var context =
            new PosDbContext(optionsBuilder.Options);

        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;
        return !context.Database.HasPendingModelChanges();
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static ManualBackupResult MapFailure(string message)
    {
        if (message.Contains("trùng", StringComparison.OrdinalIgnoreCase))
        {
            return ManualBackupResult.Failure(ManualBackupStatus.ArchiveAlreadyExists);
        }

        if (message.Contains("không tìm thấy", StringComparison.OrdinalIgnoreCase))
        {
            return ManualBackupResult.Failure(ManualBackupStatus.SourceUnavailable);
        }

        if (message.Contains("xác minh", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("toàn vẹn", StringComparison.OrdinalIgnoreCase))
        {
            return ManualBackupResult.Failure(ManualBackupStatus.VerificationFailed);
        }

        if (message.Contains("quyền", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("ghi file", StringComparison.OrdinalIgnoreCase))
        {
            return ManualBackupResult.Failure(ManualBackupStatus.DestinationUnavailable);
        }

        return ManualBackupResult.Failure(ManualBackupStatus.UnexpectedFailure);
    }

    private static ManualBackupResult VerificationFailureWithCleanup(string backupPath, string destinationDirectory)
    {
        DeleteOperationOutput(backupPath, destinationDirectory);
        return ManualBackupResult.Failure(ManualBackupStatus.VerificationFailed);
    }

    private static void DeleteOperationOutput(string backupPath, string destinationDirectory)
    {
        try
        {
            var fullPath = Path.GetFullPath(backupPath);
            if (!string.Equals(Path.GetDirectoryName(fullPath), destinationDirectory, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullPath).StartsWith("pos-enterprise-pre-migration-", StringComparison.Ordinal) ||
                !Path.GetExtension(fullPath).Equals(".db", StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(fullPath) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) == 0)
                File.Delete(fullPath);
        }
        catch { }
    }

    private static bool IsDestinationException(Exception exception) => exception is
        IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
