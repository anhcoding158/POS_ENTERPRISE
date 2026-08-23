using System.IO;

namespace POS.Application.Abstractions.Services;

public enum ManualBackupStatus
{
    Success,
    Busy,
    Cancelled,
    InvalidDestination,
    DestinationUnavailable,
    SourceUnavailable,
    ArchiveAlreadyExists,
    VerificationFailed,
    UnexpectedFailure
}

public sealed record ManualBackupResult
{
    private ManualBackupResult(
        ManualBackupStatus status,
        string? backupFilePath,
        long? backupFileSizeBytes,
        string? sha256Hex,
        DateTimeOffset? completedAtUtc)
    {
        Status = status;
        BackupFilePath = backupFilePath;
        BackupFileSizeBytes = backupFileSizeBytes;
        Sha256Hex = sha256Hex;
        CompletedAtUtc = completedAtUtc;
    }

    public ManualBackupStatus Status { get; }
    public string? BackupFilePath { get; }
    public long? BackupFileSizeBytes { get; }
    public string? Sha256Hex { get; }
    public DateTimeOffset? CompletedAtUtc { get; }
    public bool IsSuccess => Status == ManualBackupStatus.Success;

    public static ManualBackupResult Success(
        string backupFilePath,
        long backupFileSizeBytes,
        string sha256Hex,
        DateTimeOffset completedAtUtc) =>
        new(
            ManualBackupStatus.Success,
            Path.GetFullPath(
                backupFilePath ??
                throw new ArgumentNullException(
                    nameof(backupFilePath))),
            backupFileSizeBytes,
            sha256Hex ?? throw new ArgumentNullException(nameof(sha256Hex)),
            completedAtUtc);

    public static ManualBackupResult Failure(
        ManualBackupStatus status)
    {
        if (status == ManualBackupStatus.Success)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        return new ManualBackupResult(
            status,
            null,
            null,
            null,
            null);
    }
}
