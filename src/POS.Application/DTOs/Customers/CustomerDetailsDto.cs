using POS.Domain.Enums;

namespace POS.Application.DTOs.Customers;

/// <summary>
/// Thông tin đầy đủ của khách hàng.
/// </summary>
public sealed record CustomerDetailsDto(
    int Id,
    string Code,
    string FullName,
    string PhoneNumber,
    string NormalizedPhoneNumber,
    string? Address,
    string? Notes,
    CustomerTier Tier,
    long LoyaltyPoints,
    long TotalSpent,
    int OrderCount,
    DateTimeOffset? LastPurchaseAtUtc,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);