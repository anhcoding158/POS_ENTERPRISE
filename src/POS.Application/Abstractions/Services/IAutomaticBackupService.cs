namespace POS.Application.Abstractions.Services;

public interface IAutomaticBackupService
{
    Task<AutomaticBackupResult> RunAsync(CancellationToken cancellationToken = default);
}
