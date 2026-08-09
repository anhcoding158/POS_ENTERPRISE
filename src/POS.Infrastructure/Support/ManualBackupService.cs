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

    public ManualBackupService(
        IOptions<InfrastructureOptions> infrastructureOptions,
        PosDbContext dbContext,
        IClock clock)
    {
        _infrastructureOptions = infrastructureOptions?.Value ??
            throw new ArgumentNullException(nameof(infrastructureOptions));
        _infrastructureOptions.Validate();
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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

        if (!SqliteDatabaseSafetyService.CheckIntegrity(backupPath).IsSuccess)
        {
            return ManualBackupResult.Failure(ManualBackupStatus.VerificationFailed);
        }

        var backupSchemaVerified = await VerifySchemaCompatibilityAsync(
            backupPath,
            cancellationToken);

        if (!backupSchemaVerified)
        {
            return ManualBackupResult.Failure(ManualBackupStatus.VerificationFailed);
        }

        var fileInfo = new FileInfo(backupPath);
        if (!fileInfo.Exists || fileInfo.Length <= 0)
        {
            return ManualBackupResult.Failure(ManualBackupStatus.VerificationFailed);
        }

        var sha256Hex = await ComputeSha256Async(backupPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(sha256Hex))
        {
            return ManualBackupResult.Failure(ManualBackupStatus.VerificationFailed);
        }

        return ManualBackupResult.Success(
            backupPath,
            fileInfo.Length,
            sha256Hex,
            _clock.UtcNow);
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

    private static bool IsDestinationException(Exception exception) => exception is
        IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
