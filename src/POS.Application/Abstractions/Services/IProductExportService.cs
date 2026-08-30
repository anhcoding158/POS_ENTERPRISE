using POS.Application.Common;
using POS.Application.DTOs.Exports;

namespace POS.Application.Abstractions.Services;

public interface IProductExportService
{
    Task<Result<ProductExportData>> ExportAsync(
        ProductExportRequest request,
        CancellationToken cancellationToken = default);
}
