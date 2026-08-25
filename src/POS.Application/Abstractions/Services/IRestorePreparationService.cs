namespace POS.Application.Abstractions.Services;

public interface IRestorePreparationService
{
    Task<RestorePreparationResult> PrepareAsync(
        RestorePreparationRequest request,
        CancellationToken cancellationToken = default);
}
