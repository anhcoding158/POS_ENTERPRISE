using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Truy cập dữ liệu chương trình giảm giá.
/// </summary>
public interface IDiscountRepository
{
    /// <summary>
    /// Lấy khuyến mãi theo khóa chính.
    /// </summary>
    Task<Discount?> GetByIdAsync(
        int discountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy khuyến mãi theo mã.
    /// </summary>
    Task<Discount?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tìm kiếm khuyến mãi có phân trang.
    /// </summary>
    Task<PagedResult<Discount>> SearchAsync(
        string? searchTerm,
        DiscountType? type,
        bool? isActive,
        DateTimeOffset? activeAtUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra mã khuyến mãi đã tồn tại hay chưa.
    /// </summary>
    Task<bool> CodeExistsAsync(
        string code,
        int? excludeDiscountId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thêm khuyến mãi mới nhưng chưa lưu database.
    /// </summary>
    Task AddAsync(
        Discount discount,
        CancellationToken cancellationToken = default);
}