using POS.Domain.Enums;
using POS.Domain.Services;

namespace POS.Application.DTOs.Checkout;

public sealed record SalesDiscountRequest
{
    public SalesDiscountRequest(SalesDiscountType type, long value, string? reason)
    {
        Type = type;
        Value = value;
        Reason = SalesDiscountCalculator.NormalizeReason(type, reason);
    }

    public SalesDiscountType Type { get; }
    public long Value { get; }
    public string? Reason { get; }

    public static SalesDiscountRequest None { get; } =
        new(SalesDiscountType.None, 0, null);
}
