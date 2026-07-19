using POS.Domain.Enums;

namespace POS.Application.DTOs.Discounts;

/// <summary>
/// Dữ liệu tạo chương trình giảm giá.
///
/// Value có ý nghĩa tùy theo Type:
/// - Percent: phần trăm giảm;
/// - FixedAmount: số tiền VND được giảm.
/// </summary>
public sealed class CreateDiscountRequest
{
    public CreateDiscountRequest(
        string? code,
        string? name,
        string? description,
        DiscountType type,
        decimal value,
        long minimumOrderAmount,
        long? maximumDiscountAmount,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        int? usageLimit)
    {
        Code = NormalizeRequiredText(code);
        Name = NormalizeRequiredText(name);
        Description = NormalizeOptionalText(description);

        Type = type;
        Value = value;
        MinimumOrderAmount = minimumOrderAmount;
        MaximumDiscountAmount = maximumDiscountAmount;

        StartsAtUtc = startsAtUtc.ToUniversalTime();
        EndsAtUtc = endsAtUtc?.ToUniversalTime();

        UsageLimit = usageLimit;
    }

    public string Code { get; }

    public string Name { get; }

    public string? Description { get; }

    public DiscountType Type { get; }

    public decimal Value { get; }

    public long MinimumOrderAmount { get; }

    public long? MaximumDiscountAmount { get; }

    public DateTimeOffset StartsAtUtc { get; }

    public DateTimeOffset? EndsAtUtc { get; }

    public int? UsageLimit { get; }

    private static string NormalizeRequiredText(
        string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}