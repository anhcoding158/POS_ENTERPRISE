using POS.Domain.Enums;

namespace POS.Application.DTOs.Discounts;

/// <summary>
/// Thông tin đầy đủ của một chương trình giảm giá.
/// </summary>
public sealed record DiscountDetailsDto(
    int Id,
    string Code,
    string Name,
    string? Description,
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
    bool IsCurrentlyAvailable,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);