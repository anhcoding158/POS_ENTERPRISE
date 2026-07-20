using POS.Application.Common;
using POS.Application.DTOs.Categories;

namespace POS.Application.Abstractions.Services;

/// <summary>
/// Các use case đọc và quản lý danh mục sản phẩm.
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Lấy danh mục đang hoạt động theo thứ tự hiển thị.
    /// </summary>
    Task<Result<IReadOnlyList<CategoryOptionDto>>>
        ListActiveAsync(
            CancellationToken cancellationToken = default);
}