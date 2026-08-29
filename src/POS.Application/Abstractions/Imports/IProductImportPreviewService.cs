using POS.Application.DTOs.ProductImports;

namespace POS.Application.Abstractions.ProductImports;

/// <summary>
/// Đọc và kiểm tra trước dữ liệu sản phẩm.
/// Contract này không có thao tác ghi database.
/// </summary>
public interface IProductImportPreviewService
{
    Task<ProductImportPreviewResult> PreviewAsync(
        string filePath,
        ProductImportPreviewOptions? options = null,
        CancellationToken cancellationToken = default);
}
