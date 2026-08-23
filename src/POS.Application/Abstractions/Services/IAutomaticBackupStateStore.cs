namespace POS.Application.Abstractions.Services;

public interface IAutomaticBackupStateStore
{
    Task<AutomaticBackupStateReadResult> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(AutomaticBackupState state, CancellationToken cancellationToken = default);
}
