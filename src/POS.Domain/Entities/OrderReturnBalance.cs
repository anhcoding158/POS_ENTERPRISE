using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class OrderReturnBalance
{
    private OrderReturnBalance()
    {
    }

    public OrderReturnBalance(int orderItemId)
    {
        if (orderItemId <= 0)
            throw new DomainException("ORDER_RETURN_BALANCE.INVALID_ITEM", "Dòng bán không hợp lệ.");
        OrderItemId = orderItemId;
        ConcurrencyToken = Guid.NewGuid();
    }

    public int OrderItemId { get; private set; }
    public int ReturnedQuantity { get; private set; }
    public long RefundedAmount { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public OrderItem? OrderItem { get; private set; }

    public void Register(int quantity, long amount, int soldQuantity, long refundableAmount)
    {
        if (quantity <= 0 || amount <= 0)
            throw new DomainException("ORDER_RETURN_BALANCE.INVALID_DELTA", "Số lượng và tiền hoàn phải lớn hơn 0.");

        var returned = checked(ReturnedQuantity + quantity);
        var refunded = checked(RefundedAmount + amount);
        if (returned > soldQuantity || refunded > refundableAmount)
            throw new DomainException("ORDER_RETURN_BALANCE.EXCEEDED", "Trả hàng vượt số lượng hoặc số tiền đã bán.");

        ReturnedQuantity = returned;
        RefundedAmount = refunded;
        ConcurrencyToken = Guid.NewGuid();
    }
}
