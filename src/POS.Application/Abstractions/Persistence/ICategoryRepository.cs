using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Truy cập dữ liệu danh mục sản phẩm.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// Lấy danh mục theo khóa chính.
    /// </summary>
    Task<Category?> GetByIdAsync(
        int categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh mục theo tên.
    ///
    /// Việc so sánh tên không phân biệt hoa thường
    /// được triển khai tại Infrastructure.
    /// </summary>
    Task<Category?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy toàn bộ danh mục đang hoạt động,
    /// sắp xếp theo DisplayOrder rồi đến Name.
    ///
    /// Dùng để hiển thị combobox hoặc bộ lọc sản phẩm.
    /// </summary>
    Task<IReadOnlyList<Category>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm danh mục có phân trang.
    /// </summary>
    Task<PagedResult<Category>> SearchAsync(
        string? searchTerm,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra tên danh mục đã tồn tại hay chưa.
    /// </summary>
    Task<bool> NameExistsAsync(
        string name,
        int? excludeCategoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm danh mục mới nhưng chưa lưu database.
    /// </summary>
    Task AddAsync(
        Category category,
        CancellationToken cancellationToken = default);
}