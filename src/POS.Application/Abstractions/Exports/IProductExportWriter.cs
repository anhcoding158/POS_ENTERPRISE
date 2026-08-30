using POS.Application.DTOs.Exports;

namespace POS.Application.Abstractions.Exports;

public interface IProductExportWriter
{
    Task WriteAsync(
        ProductExportData data,
        ProductExportFormat format,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
