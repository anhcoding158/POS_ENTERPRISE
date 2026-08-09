namespace POS.Application.Abstractions.Services;

public interface IManualBackupService
{
    Task<ManualBackupResult> BackupAsync(
        ManualBackupRequest request,
        CancellationToken cancellationToken = default);
}
