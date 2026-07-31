using POS.Domain.Common;
using POS.Domain.Enums;
using POS.Domain.Services;

namespace POS.Domain.Entities;

public sealed class OrderDiscountSnapshot : Entity
{
    private OrderDiscountSnapshot() { }

    public OrderDiscountSnapshot(
        Order order,
        SalesDiscountType type,
        long requestedValue,
        long resolvedAmount,
        string reason,
        int appliedByUserId,
        DateTimeOffset appliedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (type == SalesDiscountType.None)
            throw new DomainException("SALES_DISCOUNT.TYPE_INVALID", "Snapshot phải chứa giảm giá.");
        var expected = SalesDiscountCalculator.Resolve(order.Subtotal, type, requestedValue, reason);
        if (resolvedAmount != expected)
            throw new DomainException("SALES_DISCOUNT.RESOLUTION_MISMATCH", "Số tiền giảm đã chốt không hợp lệ.");
        if (appliedByUserId <= 0)
            throw new DomainException("SALES_DISCOUNT.ACTOR_INVALID", "Người áp dụng giảm giá không hợp lệ.");
        if (appliedAtUtc == default)
            throw new DomainException("SALES_DISCOUNT.TIME_INVALID", "Thời điểm áp dụng giảm giá không hợp lệ.");

        Order = order;
        Type = type;
        RequestedValue = requestedValue;
        ResolvedAmount = resolvedAmount;
        Reason = SalesDiscountCalculator.NormalizeReason(type, reason)!;
        AppliedByUserId = appliedByUserId;
        AppliedAtUtc = appliedAtUtc.ToUniversalTime();
    }

    public int OrderId { get; private set; }
    public SalesDiscountType Type { get; private set; }
    public long RequestedValue { get; private set; }
    public long ResolvedAmount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public int AppliedByUserId { get; private set; }
    public DateTimeOffset AppliedAtUtc { get; private set; }
    public Order Order { get; private set; } = null!;
    public User AppliedByUser { get; private set; } = null!;
}
