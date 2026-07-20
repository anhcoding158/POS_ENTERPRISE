using POS.Application.Common;
using POS.Application.DTOs.Categories;

namespace POS.Application.Abstractions.Services;

/// <summary>
/// Các use case quản lý danh mục sản phẩm.
///
/// WPF chỉ làm việc với interface và DTO.
/// Không truy cập Entity hoặc Repository trực tiếp.
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// Lấy danh mục đang hoạt động cho ComboBox.
    /// </summary>
    Task<Result<IReadOnlyList<CategoryOptionDto>>>
        ListActiveAsync(
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm danh mục có phân trang.
    /// </summary>
    Task<Result<PagedResult<CategoryListItemDto>>>
        SearchAsync(
            CategorySearchRequest request,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy chi tiết một danh mục.
    /// </summary>
    Task<Result<CategoryDetailsDto>>
        GetByIdAsync(
            int categoryId,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo danh mục mới.
    /// </summary>
    Task<Result<CategoryDetailsDto>>
        CreateAsync(
            CreateCategoryRequest request,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật danh mục.
    /// </summary>
    Task<Result<CategoryDetailsDto>>
        UpdateAsync(
            UpdateCategoryRequest request,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Bật hoặc ngừng hoạt động danh mục.
    ///
    /// Không xóa cứng để tránh làm hỏng các Product
    /// đang tham chiếu tới danh mục.
    /// </summary>
    Task<Result>
        SetActiveStateAsync(
            int categoryId,
            bool isActive,
            CancellationToken cancellationToken = default);
}