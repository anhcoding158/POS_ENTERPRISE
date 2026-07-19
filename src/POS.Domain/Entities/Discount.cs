using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Chương trình giảm giá theo phần trăm hoặc số tiền cố định.
/// </summary>
public sealed class Discount : AuditableEntity
{
    private Discount()
    {
    }

    private Discount(
        string code,
        string name,
        string? description,
        DiscountType type,
        decimal value,
        long minimumOrderAmount,
        long? maximumDiscountAmount,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        int? usageLimit,
        DateTimeOffset utcNow)
    {
        SetCode(code);
        SetName(name);
        SetDescription(description);

        SetRules(
            type,
            value,
            minimumOrderAmount,
            maximumDiscountAmount);

        SetValidityPeriod(
            startsAtUtc,
            endsAtUtc);

        SetUsageLimit(usageLimit);

        IsActive = true;

        MarkCreated(utcNow);
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DiscountType Type { get; private set; }

    public decimal Value { get; private set; }

    public long MinimumOrderAmount { get; private set; }

    public long? MaximumDiscountAmount { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset? EndsAtUtc { get; private set; }

    public int? UsageLimit { get; private set; }

    public int UsedCount { get; private set; }

    public bool IsActive { get; private set; }

    public static Discount CreatePercent(
        string code,
        string name,
        decimal percent,
        long minimumOrderAmount,
        long? maximumDiscountAmount,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        int? usageLimit,
        DateTimeOffset utcNow,
        string? description = null)
    {
        return new Discount(
            code,
            name,
            description,
            DiscountType.Percent,
            percent,
            minimumOrderAmount,
            maximumDiscountAmount,
            startsAtUtc,
            endsAtUtc,
            usageLimit,
            utcNow);
    }

    public static Discount CreateFixedAmount(
        string code,
        string name,
        long fixedAmount,
        long minimumOrderAmount,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        int? usageLimit,
        DateTimeOffset utcNow,
        string? description = null)
    {
        return new Discount(
            code,
            name,
            description,
            DiscountType.FixedAmount,
            fixedAmount,
            minimumOrderAmount,
            null,
            startsAtUtc,
            endsAtUtc,
            usageLimit,
            utcNow);
    }

    public bool CanApply(
        long orderSubtotal,
        DateTimeOffset utcNow)
    {
        if (!IsActive ||
            orderSubtotal < MinimumOrderAmount)
        {
            return false;
        }

        var normalizedUtc = utcNow.ToUniversalTime();

        if (normalizedUtc < StartsAtUtc)
        {
            return false;
        }

        if (EndsAtUtc.HasValue &&
            normalizedUtc > EndsAtUtc.Value)
        {
            return false;
        }

        if (UsageLimit.HasValue &&
            UsedCount >= UsageLimit.Value)
        {
            return false;
        }

        return true;
    }

    public long CalculateDiscountAmount(
        long orderSubtotal,
        DateTimeOffset utcNow)
    {
        if (orderSubtotal < 0 ||
            orderSubtotal >
            BusinessRules.Orders.MaximumOrderAmount)
        {
            throw new DomainException(
                "DISCOUNT.INVALID_ORDER_SUBTOTAL",
                "Tiền hàng không hợp lệ.");
        }

        if (!CanApply(orderSubtotal, utcNow))
        {
            throw new DomainException(
                "DISCOUNT.NOT_APPLICABLE",
                "Khuyến mãi không thể áp dụng cho đơn hàng này.");
        }

        long discountAmount;

        if (Type == DiscountType.Percent)
        {
            var rawAmount =
                orderSubtotal * Value / 100m;

            var roundedAmount = decimal.Round(
                rawAmount,
                0,
                MidpointRounding.AwayFromZero);

            discountAmount =
                decimal.ToInt64(roundedAmount);
        }
        else
        {
            discountAmount =
                decimal.ToInt64(Value);
        }

        if (MaximumDiscountAmount.HasValue)
        {
            discountAmount = Math.Min(
                discountAmount,
                MaximumDiscountAmount.Value);
        }

        return Math.Min(
            discountAmount,
            orderSubtotal);
    }

    public void Update(
        string name,
        string? description,
        DiscountType type,
        decimal value,
        long minimumOrderAmount,
        long? maximumDiscountAmount,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        int? usageLimit,
        DateTimeOffset utcNow)
    {
        SetName(name);
        SetDescription(description);

        SetRules(
            type,
            value,
            minimumOrderAmount,
            maximumDiscountAmount);

        SetValidityPeriod(
            startsAtUtc,
            endsAtUtc);

        SetUsageLimit(usageLimit);

        MarkUpdated(utcNow);
    }

    public void RegisterUsage(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            throw new DomainException(
                "DISCOUNT.IS_INACTIVE",
                "Khuyến mãi đang ngừng hoạt động.");
        }

        if (UsageLimit.HasValue &&
            UsedCount >= UsageLimit.Value)
        {
            throw new DomainException(
                "DISCOUNT.USAGE_LIMIT_REACHED",
                "Khuyến mãi đã hết lượt sử dụng.");
        }

        if (UsedCount == int.MaxValue)
        {
            throw new DomainException(
                "DISCOUNT.USAGE_COUNT_OVERFLOW",
                "Số lượt sử dụng vượt quá giới hạn.");
        }

        UsedCount++;

        MarkUpdated(utcNow);
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        MarkUpdated(utcNow);
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        MarkUpdated(utcNow);
    }

    private void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException(
                "DISCOUNT.CODE_REQUIRED",
                "Mã khuyến mãi không được để trống.");
        }

        var normalized = code
            .Trim()
            .ToUpperInvariant();

        if (normalized.Length >
            BusinessRules.Discounts.CodeMaxLength)
        {
            throw new DomainException(
                "DISCOUNT.CODE_TOO_LONG",
                "Mã khuyến mãi vượt quá giới hạn.");
        }

        Code = normalized;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(
                "DISCOUNT.NAME_REQUIRED",
                "Tên khuyến mãi không được để trống.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length >
            BusinessRules.Discounts.NameMaxLength)
        {
            throw new DomainException(
                "DISCOUNT.NAME_TOO_LONG",
                "Tên khuyến mãi vượt quá giới hạn.");
        }

        Name = trimmed;
    }

    private void SetDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalized?.Length >
            BusinessRules.Discounts.DescriptionMaxLength)
        {
            throw new DomainException(
                "DISCOUNT.DESCRIPTION_TOO_LONG",
                "Mô tả khuyến mãi vượt quá giới hạn.");
        }

        Description = normalized;
    }

    private void SetRules(
        DiscountType type,
        decimal value,
        long minimumOrderAmount,
        long? maximumDiscountAmount)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException(
                "DISCOUNT.INVALID_TYPE",
                "Loại giảm giá không hợp lệ.");
        }

        if (minimumOrderAmount < 0 ||
            minimumOrderAmount >
            BusinessRules.Orders.MaximumOrderAmount)
        {
            throw new DomainException(
                "DISCOUNT.INVALID_MINIMUM_ORDER_AMOUNT",
                "Giá trị đơn tối thiểu không hợp lệ.");
        }

        if (maximumDiscountAmount.HasValue &&
            (maximumDiscountAmount.Value <= 0 ||
             maximumDiscountAmount.Value >
             BusinessRules.Discounts.MaximumFixedAmount))
        {
            throw new DomainException(
                "DISCOUNT.INVALID_MAXIMUM_AMOUNT",
                "Mức giảm tối đa không hợp lệ.");
        }

        if (type == DiscountType.Percent)
        {
            if (value <= 0 ||
                value >
                BusinessRules.Discounts.MaximumPercent)
            {
                throw new DomainException(
                    "DISCOUNT.INVALID_PERCENT",
                    "Phần trăm giảm giá không hợp lệ.");
            }
        }
        else
        {
            if (value <= 0 ||
                value >
                BusinessRules.Discounts.MaximumFixedAmount ||
                value != decimal.Truncate(value))
            {
                throw new DomainException(
                    "DISCOUNT.INVALID_FIXED_AMOUNT",
                    "Số tiền giảm cố định không hợp lệ.");
            }

            maximumDiscountAmount = null;
        }

        Type = type;
        Value = value;
        MinimumOrderAmount = minimumOrderAmount;
        MaximumDiscountAmount = maximumDiscountAmount;
    }

    private void SetValidityPeriod(
    DateTimeOffset startsAtUtc,
    DateTimeOffset? endsAtUtc)
    {
        if (startsAtUtc == default)
        {
            throw new DomainException(
                "DISCOUNT.START_TIME_REQUIRED",
                "Thời điểm bắt đầu khuyến mãi không được để trống.");
        }

        if (endsAtUtc.HasValue &&
            endsAtUtc.Value == default)
        {
            throw new DomainException(
                "DISCOUNT.INVALID_END_TIME",
                "Thời điểm kết thúc khuyến mãi không hợp lệ.");
        }

        var normalizedStart =
            startsAtUtc.ToUniversalTime();

        var normalizedEnd =
            endsAtUtc?.ToUniversalTime();

        if (normalizedEnd.HasValue &&
            normalizedEnd.Value <= normalizedStart)
        {
            throw new DomainException(
                "DISCOUNT.INVALID_VALIDITY_PERIOD",
                "Thời điểm kết thúc phải lớn hơn thời điểm bắt đầu.");
        }

        StartsAtUtc = normalizedStart;
        EndsAtUtc = normalizedEnd;
    }

    private void SetUsageLimit(int? usageLimit)
    {
        if (usageLimit.HasValue &&
            usageLimit.Value <= 0)
        {
            throw new DomainException(
                "DISCOUNT.INVALID_USAGE_LIMIT",
                "Giới hạn lượt sử dụng phải lớn hơn 0.");
        }

        if (usageLimit.HasValue &&
            usageLimit.Value < UsedCount)
        {
            throw new DomainException(
                "DISCOUNT.USAGE_LIMIT_BELOW_USED_COUNT",
                "Giới hạn mới nhỏ hơn số lượt đã sử dụng.");
        }

        UsageLimit = usageLimit;
    }
}