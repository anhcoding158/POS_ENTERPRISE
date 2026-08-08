namespace POS.Application.Abstractions.Services;

public interface ISupportBundleService
{
    Task<SupportBundleResult> ExportAsync(
        SupportBundleRequest request,
        CancellationToken cancellationToken = default);
}
