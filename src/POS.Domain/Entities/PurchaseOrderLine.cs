using POS.Domain.Common;
using POS.Domain.Constants;

namespace POS.Domain.Entities;

/// <summary>
/// Một dòng hàng thuộc PurchaseOrder.
/// Product identity và snapshot được giữ tại dòng chứng từ.
/// </summary>
public sealed class PurchaseOrderLine : Entity
{
    private PurchaseOrderLine()
    {
    }

    internal PurchaseOrderLine(
        int productId,
        string productCode,
        string productName,
        string unitName,
        int orderedQuantity,
        long agreedUnitCost,
        int sortOrder)
    {
        SetProductId(productId);
        SetProductSnapshot(productCode, productName, unitName);
        SetQuantity(orderedQuantity);
        SetAgreedUnitCost(agreedUnitCost);
        SetSortOrder(sortOrder);
    }

    public int PurchaseOrderId { get; private set; }

    public int ProductId { get; private set; }

    public string ProductCode { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public string UnitName { get; private set; } = string.Empty;

    public int OrderedQuantity { get; private set; }

    /// <summary>
    /// Projection chuẩn bị cho R6.2C. R6.2A không có mutation path.
    /// </summary>
    public int ReceivedQuantity { get; private set; }

    public long AgreedUnitCost { get; private set; }

    public int SortOrder { get; private set; }

    public PurchaseOrder? PurchaseOrder { get; private set; }

    public long LineTotal => CalculateLineTotal();

    internal void UpdateDraft(
        string productCode,
        string productName,
        string unitName,
        int orderedQuantity,
        long agreedUnitCost,
        int sortOrder)
    {
        if (ReceivedQuantity != 0)
        {
            throw new DomainException(
                "PURCHASE_ORDER.RECEIVED_LINE_NOT_DRAFT_EDITABLE",
                "Dòng đã nhận hàng không còn là dòng nháp có thể thay thế.");
        }

        SetProductSnapshot(productCode, productName, unitName);
        SetQuantity(orderedQuantity);
        SetAgreedUnitCost(agreedUnitCost);
        SetSortOrder(sortOrder);
    }

    internal void AmendOrdered(
        int orderedQuantity,
        long agreedUnitCost,
        int sortOrder)
    {
        SetQuantity(orderedQuantity);
        SetAgreedUnitCost(agreedUnitCost);
        SetSortOrder(sortOrder);
    }

    internal void RefreshProductSnapshot(
        string productCode,
        string productName,
        string unitName)
    {
        SetProductSnapshot(productCode, productName, unitName);
    }

    private long CalculateLineTotal()
    {
        try
        {
            var total = checked((long)OrderedQuantity * AgreedUnitCost);
            if (total > BusinessRules.PurchaseOrders.MaximumOrderAmount)
            {
                throw new DomainException(
                    "PURCHASE_ORDER.LINE_TOTAL_TOO_LARGE",
                    "Thành tiền dòng mua hàng vượt quá giới hạn.");
            }

            return total;
        }
        catch (OverflowException exception)
        {
            throw new DomainException(
                "PURCHASE_ORDER.LINE_TOTAL_OVERFLOW",
                "Thành tiền dòng mua hàng vượt quá giới hạn.",
                exception);
        }
    }

    private void SetProductId(int value)
    {
        if (value <= 0)
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_PRODUCT_ID",
                "Sản phẩm của dòng mua hàng không hợp lệ.");
        }

        ProductId = value;
    }

    private void SetProductSnapshot(
        string productCode,
        string productName,
        string unitName)
    {
        ProductCode = NormalizeRequired(
            productCode,
            BusinessRules.Products.CodeMaxLength,
            "PURCHASE_ORDER.INVALID_PRODUCT_CODE",
            "Mã sản phẩm trong chứng từ không hợp lệ.");
        ProductName = NormalizeRequired(
            productName,
            BusinessRules.Products.NameMaxLength,
            "PURCHASE_ORDER.INVALID_PRODUCT_NAME",
            "Tên sản phẩm trong chứng từ không hợp lệ.");
        UnitName = NormalizeRequired(
            unitName,
            BusinessRules.Products.UnitNameMaxLength,
            "PURCHASE_ORDER.INVALID_UNIT_NAME",
            "Đơn vị sản phẩm trong chứng từ không hợp lệ.");
    }

    private void SetQuantity(int value)
    {
        if (value <= 0 || value > BusinessRules.PurchaseOrders.MaximumLineQuantity)
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_ORDERED_QUANTITY",
                "Số lượng đặt hàng không hợp lệ.");
        }

        if (value < ReceivedQuantity)
        {
            throw new DomainException(
                "PURCHASE_ORDER.ORDERED_BELOW_RECEIVED",
                "Số lượng đặt không được nhỏ hơn số lượng đã nhận.");
        }

        OrderedQuantity = value;
    }

    private void SetAgreedUnitCost(long value)
    {
        if (value < 0 || value > BusinessRules.Products.MaximumPrice)
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_UNIT_COST",
                "Đơn giá mua không hợp lệ.");
        }

        AgreedUnitCost = value;
    }

    private void SetSortOrder(int value)
    {
        if (value <= 0)
        {
            throw new DomainException(
                "PURCHASE_ORDER.INVALID_SORT_ORDER",
                "Thứ tự dòng mua hàng không hợp lệ.");
        }

        SortOrder = value;
    }

    private static string NormalizeRequired(
        string value,
        int maxLength,
        string code,
        string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 ||
            normalized.Length > maxLength ||
            normalized.Any(char.IsControl))
        {
            throw new DomainException(code, message);
        }

        return normalized;
    }
}
