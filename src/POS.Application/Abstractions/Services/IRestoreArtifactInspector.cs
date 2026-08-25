namespace POS.Application.Abstractions.Services;

public interface IRestoreArtifactInspector
{
    Task<RestoreArtifactInspection> InspectAsync(
        string? selectedSourcePath,
        CancellationToken cancellationToken = default);
}
