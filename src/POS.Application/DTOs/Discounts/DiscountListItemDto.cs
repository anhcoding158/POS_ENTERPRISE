using POS.Domain.Enums;

namespace POS.Application.DTOs.Discounts;

/// <summary>
/// Dữ liệu khuyến mãi dùng cho màn hình danh sách.
/// </summary>
public sealed record DiscountListItemDto(
    int Id,
    string Code,
    string Name,
    DiscountType Type,
    decimal Value,
    long MinimumOrderAmount,
    long? MaximumDiscountAmount,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    int? UsageLimit,
    int UsedCount,
    int? RemainingUsageCount,
    bool IsActive,
    bool IsCurrentlyAvailable);