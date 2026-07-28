using POS.Domain.Common;
using POS.Domain.Constants;

namespace POS.Domain.Entities;

public sealed class HeldSaleLine : Entity
{
    private HeldSaleLine()
    {
    }

    internal HeldSaleLine(
        int productId,
        string productCodeSnapshot,
        string? barcodeSnapshot,
        string productNameSnapshot,
        int quantity,
        long unitPriceSnapshot,
        int sortOrder,
        string? lineNotesSnapshot)
    {
        if (productId <= 0)
            throw new DomainException("HELD_SALE.INVALID_PRODUCT", "Sản phẩm không hợp lệ.");
        if (string.IsNullOrWhiteSpace(productCodeSnapshot))
            throw new DomainException("HELD_SALE.PRODUCT_CODE_REQUIRED", "Mã sản phẩm snapshot không được để trống.");
        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new DomainException("HELD_SALE.PRODUCT_NAME_REQUIRED", "Tên sản phẩm snapshot không được để trống.");
        if (productCodeSnapshot.Trim().Length > BusinessRules.Products.CodeMaxLength ||
            productNameSnapshot.Trim().Length > BusinessRules.Products.NameMaxLength ||
            (!string.IsNullOrWhiteSpace(barcodeSnapshot) &&
             barcodeSnapshot.Trim().Length > BusinessRules.Products.BarcodeMaxLength))
            throw new DomainException("HELD_SALE.SNAPSHOT_TOO_LONG", "Thông tin sản phẩm snapshot vượt quá giới hạn.");
        if (quantity <= 0 || quantity > BusinessRules.Orders.MaximumLineQuantity)
            throw new DomainException("HELD_SALE.INVALID_QUANTITY", "Số lượng đơn giữ không hợp lệ.");
        if (unitPriceSnapshot < 0 || unitPriceSnapshot > BusinessRules.Products.MaximumPrice)
            throw new DomainException("HELD_SALE.INVALID_PRICE", "Giá snapshot không hợp lệ.");
        if (sortOrder < 0)
            throw new DomainException("HELD_SALE.INVALID_SORT_ORDER", "Thứ tự dòng không hợp lệ.");

        ProductId = productId;
        ProductCodeSnapshot = productCodeSnapshot.Trim();
        BarcodeSnapshot = string.IsNullOrWhiteSpace(barcodeSnapshot) ? null : barcodeSnapshot.Trim();
        ProductNameSnapshot = productNameSnapshot.Trim();
        Quantity = quantity;
        UnitPriceSnapshot = unitPriceSnapshot;
        LineTotalSnapshot = checked(unitPriceSnapshot * quantity);
        SortOrder = sortOrder;
        LineNotesSnapshot = Normalize(lineNotesSnapshot, BusinessRules.Orders.NotesMaxLength);
    }

    public int HeldSaleId { get; private set; }
    public int ProductId { get; private set; }
    public string ProductCodeSnapshot { get; private set; } = string.Empty;
    public string? BarcodeSnapshot { get; private set; }
    public string ProductNameSnapshot { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public long UnitPriceSnapshot { get; private set; }
    public long LineTotalSnapshot { get; private set; }
    public int SortOrder { get; private set; }
    public string? LineNotesSnapshot { get; private set; }
    public HeldSale? HeldSale { get; private set; }
    public Product? Product { get; private set; }

    private static string? Normalize(string? value, int maximumLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
            throw new DomainException("HELD_SALE.TEXT_TOO_LONG", "Nội dung đơn giữ vượt quá giới hạn.");
        return normalized;
    }
}
