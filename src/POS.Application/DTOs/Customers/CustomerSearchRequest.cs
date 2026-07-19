using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Customers;

/// <summary>
/// Điều kiện tìm kiếm và phân trang khách hàng.
/// </summary>
public sealed class CustomerSearchRequest
{
    public const int DefaultPageSize = 20;

    public const int MaximumPageSize = 200;

    public CustomerSearchRequest(
        string? searchTerm = null,
        CustomerTier? tier = null,
        bool? isActive = null,
        int pageNumber = 1,
        int pageSize = DefaultPageSize)
    {
        if (tier.HasValue &&
            !Enum.IsDefined(tier.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tier),
                "Hạng khách hàng không hợp lệ.");
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
        Tier = tier;
        IsActive = isActive;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>
    /// Tìm theo mã, họ tên hoặc số điện thoại.
    /// </summary>
    public string? SearchTerm { get; }

    public CustomerTier? Tier { get; }

    public bool? IsActive { get; }

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
            BusinessRules.Customers.FullNameMaxLength)
        {
            throw new ArgumentException(
                "Từ khóa tìm kiếm khách hàng quá dài.",
                nameof(searchTerm));
        }

        return trimmed;
    }
}