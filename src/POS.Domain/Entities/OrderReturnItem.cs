using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class OrderReturnItem : Entity
{
    private OrderReturnItem()
    {
    }

    public OrderReturnItem(
        int orderItemId,
        int productId,
        string productCode,
        string productName,
        string unitName,
        int returnQuantity,
        int restockQuantity,
        long refundAmount)
    {
        if (orderItemId <= 0 || productId <= 0)
            throw new DomainException("ORDER_RETURN_ITEM.INVALID_REFERENCE", "Dòng bán hoặc sản phẩm không hợp lệ.");
        if (string.IsNullOrWhiteSpace(productCode) ||
            string.IsNullOrWhiteSpace(productName) ||
            string.IsNullOrWhiteSpace(unitName))
            throw new DomainException("ORDER_RETURN_ITEM.SNAPSHOT_REQUIRED", "Snapshot sản phẩm không đầy đủ.");
        if (returnQuantity <= 0 || restockQuantity < 0 || restockQuantity > returnQuantity)
            throw new DomainException("ORDER_RETURN_ITEM.INVALID_QUANTITY", "Số lượng trả/nhập kho không hợp lệ.");
        if (refundAmount <= 0)
            throw new DomainException("ORDER_RETURN_ITEM.INVALID_AMOUNT", "Tiền hoàn phải lớn hơn 0.");

        OrderItemId = orderItemId;
        ProductId = productId;
        ProductCode = productCode.Trim().ToUpperInvariant();
        ProductName = productName.Trim();
        UnitName = unitName.Trim();
        ReturnQuantity = returnQuantity;
        RestockQuantity = restockQuantity;
        RefundAmount = refundAmount;
    }

    public int OrderReturnId { get; private set; }
    public int OrderItemId { get; private set; }
    public int ProductId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string UnitName { get; private set; } = string.Empty;
    public int ReturnQuantity { get; private set; }
    public int RestockQuantity { get; private set; }
    public long RefundAmount { get; private set; }
    public OrderReturn? OrderReturn { get; private set; }
    public OrderItem? OrderItem { get; private set; }
    public Product? Product { get; private set; }
}
