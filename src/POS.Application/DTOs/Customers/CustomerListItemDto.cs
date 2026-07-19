using POS.Domain.Enums;

namespace POS.Application.DTOs.Customers;

/// <summary>
/// Dữ liệu khách hàng dùng cho màn hình danh sách
/// và chọn khách trong quá trình bán hàng.
/// </summary>
public sealed record CustomerListItemDto(
    int Id,
    string Code,
    string FullName,
    string PhoneNumber,
    CustomerTier Tier,
    long LoyaltyPoints,
    long TotalSpent,
    int OrderCount,
    DateTimeOffset? LastPurchaseAtUtc,
    bool IsActive);