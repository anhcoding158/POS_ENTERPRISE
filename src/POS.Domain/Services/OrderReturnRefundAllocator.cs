using System.Numerics;
using POS.Domain.Common;

namespace POS.Domain.Services;

public sealed record OrderReturnAllocationLine(
    int OrderItemId,
    int SoldQuantity,
    long NetAmount);

public static class OrderReturnRefundAllocator
{
    public static IReadOnlyDictionary<int, long> AllocateOrderTotal(
        long orderTotal,
        IEnumerable<OrderReturnAllocationLine> lines)
    {
        var ordered = lines.OrderBy(line => line.OrderItemId).ToArray();
        if (orderTotal <= 0 || ordered.Length == 0 ||
            ordered.Any(line => line.SoldQuantity <= 0 || line.NetAmount < 0))
            throw new DomainException("ORDER_RETURN.INVALID_ALLOCATION", "Dữ liệu phân bổ tiền hoàn không hợp lệ.");

        var weight = ordered.Aggregate(0L, (sum, line) => checked(sum + line.NetAmount));
        if (weight <= 0)
            throw new DomainException("ORDER_RETURN.ZERO_ALLOCATION_WEIGHT", "Không thể phân bổ đơn hàng không có trọng số.");

        var result = new Dictionary<int, long>();
        var allocated = 0L;
        for (var index = 0; index < ordered.Length; index++)
        {
            var amount = index == ordered.Length - 1
                ? orderTotal - allocated
                : DivideFloor(orderTotal, ordered[index].NetAmount, weight);
            result.Add(ordered[index].OrderItemId, amount);
            allocated = checked(allocated + amount);
        }
        return result;
    }

    public static long CalculateCurrentRefund(
        long refundableLineTotal,
        int soldQuantity,
        int alreadyReturnedQuantity,
        long alreadyRefundedAmount,
        int returnQuantity)
    {
        if (refundableLineTotal < 0 || soldQuantity <= 0 ||
            alreadyReturnedQuantity < 0 || returnQuantity <= 0 ||
            alreadyReturnedQuantity + returnQuantity > soldQuantity ||
            alreadyRefundedAmount < 0)
            throw new DomainException("ORDER_RETURN.INVALID_REFUND_INPUT", "Dữ liệu tính tiền hoàn không hợp lệ.");

        var cumulative = alreadyReturnedQuantity + returnQuantity;
        var target = DivideFloor(refundableLineTotal, cumulative, soldQuantity);
        var current = checked(target - alreadyRefundedAmount);
        if (current <= 0)
            throw new DomainException("ORDER_RETURN.NON_POSITIVE_REFUND", "Tiền hoàn hiện tại phải lớn hơn 0.");
        return current;
    }

    private static long DivideFloor(long total, long multiplier, long divisor) =>
        checked((long)((BigInteger)total * multiplier / divisor));
}
