using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Discounts;

/// <summary>
/// Điều kiện tìm kiếm và phân trang khuyến mãi.
/// </summary>
public sealed class DiscountSearchRequest
{
    public const int DefaultPageSize = 20;

    public const int MaximumPageSize = 200;

    public DiscountSearchRequest(
        string? searchTerm = null,
        DiscountType? type = null,
        bool? isActive = null,
        DateTimeOffset? activeAtUtc = null,
        int pageNumber = 1,
        int pageSize = DefaultPageSize)
    {
        if (type.HasValue &&
            !Enum.IsDefined(type.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                "Loại giảm giá không hợp lệ.");
        }

        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                "Số trang phải lớn hơn 0.");
        }

        if (pageSize <= 0 ||
            pageSize > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                $"Kích thước trang phải từ 1 đến " +
                $"{MaximumPageSize}.");
        }

        SearchTerm = NormalizeSearchTerm(searchTerm);
        Type = type;
        IsActive = isActive;
        ActiveAtUtc = activeAtUtc?.ToUniversalTime();
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>
    /// Tìm theo mã hoặc tên chương trình giảm giá.
    /// </summary>
    public string? SearchTerm { get; }

    public DiscountType? Type { get; }

    public bool? IsActive { get; }

    /// <summary>
    /// Khi có giá trị, chỉ lấy khuyến mãi có hiệu lực
    /// tại thời điểm này.
    /// </summary>
    public DateTimeOffset? ActiveAtUtc { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    private static string? NormalizeSearchTerm(
        string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        var trimmed = searchTerm.Trim();

        if (trimmed.Length >
            BusinessRules.Discounts.NameMaxLength)
        {
            throw new ArgumentException(
                "Từ khóa tìm kiếm khuyến mãi quá dài.",
                nameof(searchTerm));
        }

        return trimmed;
    }
}