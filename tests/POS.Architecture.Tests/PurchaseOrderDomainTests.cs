using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PurchaseOrderDomainTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Purchase_order_starts_as_draft_without_received_quantity()
    {
        var purchaseOrder = CreateOrder();
        var line = purchaseOrder.AddLine(1, "P01", "Sản phẩm", "Hộp", 3, 12500, 1, Now);

        Assert.Equal(PurchaseOrderStatus.Draft, purchaseOrder.Status);
        Assert.Equal(0, line.ReceivedQuantity);
        Assert.Equal(37500, purchaseOrder.GrandTotal);
    }

    [Fact]
    public void Duplicate_product_is_rejected()
    {
        var purchaseOrder = CreateOrder();
        purchaseOrder.AddLine(1, "P01", "Sản phẩm", "Hộp", 1, 100, 1, Now);

        var exception = Assert.Throws<DomainException>(() =>
            purchaseOrder.AddLine(1, "P01", "Sản phẩm", "Hộp", 1, 100, 2, Now));

        Assert.Equal("PURCHASE_ORDER.DUPLICATE_PRODUCT", exception.Code);
    }

    [Fact]
    public void Invalid_quantity_cost_and_expected_date_are_rejected()
    {
        Assert.Throws<DomainException>(() =>
            CreateOrder(expectedDeliveryDate: new DateOnly(2026, 9, 3)));

        var purchaseOrder = CreateOrder();
        Assert.Throws<DomainException>(() =>
            purchaseOrder.AddLine(1, "P01", "Sản phẩm", "Hộp", 0, 100, 1, Now));
        Assert.Throws<DomainException>(() =>
            purchaseOrder.AddLine(1, "P01", "Sản phẩm", "Hộp", 1, -1, 1, Now));
    }

    [Fact]
    public void Mark_ordered_refreshes_final_snapshots_and_is_not_repeatable()
    {
        var purchaseOrder = CreateOrder();
        var line = purchaseOrder.AddLine(1, "OLD", "Tên cũ", "Cái", 2, 500, 1, Now);

        purchaseOrder.FinalizeSnapshotsAndMarkOrdered(
            new PurchaseOrder.SupplierSnapshot("SUP-02", "Nhà cung cấp mới", "TAX02"),
            new Dictionary<int, PurchaseOrder.ProductSnapshot>
            {
                [line.ProductId] = new("P01", "Tên hiện tại", "Hộp")
            },
            7,
            Now.AddMinutes(1));

        Assert.Equal(PurchaseOrderStatus.Ordered, purchaseOrder.Status);
        Assert.Equal("SUP-02", purchaseOrder.SupplierCode);
        Assert.Equal("Tên hiện tại", line.ProductName);
        Assert.Equal(7, purchaseOrder.OrderedByUserId);

        var exception = Assert.Throws<DomainException>(() =>
            purchaseOrder.FinalizeSnapshotsAndMarkOrdered(
                new PurchaseOrder.SupplierSnapshot("SUP-02", "Nhà cung cấp mới", null),
                new Dictionary<int, PurchaseOrder.ProductSnapshot>
                {
                    [line.ProductId] = new("P01", "Tên hiện tại", "Hộp")
                },
                7,
                Now.AddMinutes(2)));
        Assert.Equal("PURCHASE_ORDER.NOT_DRAFT", exception.Code);
    }

    [Fact]
    public void Ordered_amendment_keeps_identity_snapshot_and_cancelled_is_read_only()
    {
        var purchaseOrder = CreateOrderedOrder(out var line);
        purchaseOrder.AmendOrderedLine(line, 4, 600, 1, Now.AddMinutes(2));

        Assert.Equal(4, line.OrderedQuantity);
        Assert.Equal("P01", line.ProductCode);
        Assert.Equal("SUP-01", purchaseOrder.SupplierCode);

        purchaseOrder.Cancel("Không còn nhu cầu", 7, Now.AddMinutes(3));
        Assert.Equal(PurchaseOrderStatus.Cancelled, purchaseOrder.Status);

        var exception = Assert.Throws<DomainException>(() =>
            purchaseOrder.ChangeOrderedHeader(null, "Sửa", Now.AddMinutes(4)));
        Assert.Equal("PURCHASE_ORDER.NOT_ORDERED", exception.Code);
    }

    [Fact]
    public void Ordered_order_requires_lines_and_cancel_requires_reason()
    {
        var empty = CreateOrder();
        var missingLines = Assert.Throws<DomainException>(() =>
            empty.FinalizeSnapshotsAndMarkOrdered(
                new PurchaseOrder.SupplierSnapshot("SUP-01", "Nhà cung cấp", null),
                new Dictionary<int, PurchaseOrder.ProductSnapshot>(),
                7,
                Now));
        Assert.Equal("PURCHASE_ORDER.EMPTY", missingLines.Code);

        var purchaseOrder = CreateOrder();
        purchaseOrder.AddLine(1, "P01", "Sản phẩm", "Hộp", 1, 100, 1, Now);
        var exception = Assert.Throws<DomainException>(() => purchaseOrder.Cancel(" ", 7, Now));
        Assert.Equal("PURCHASE_ORDER.INVALID_CANCELLATION_REASON", exception.Code);
    }

    [Fact]
    public void Domain_has_no_stock_or_inventory_movement_mutation_surface()
    {
        Assert.DoesNotContain(
            typeof(PurchaseOrder).GetMethods(),
            method => method.Name.Contains("Stock", StringComparison.OrdinalIgnoreCase) ||
                      method.Name.Contains("Movement", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(PurchaseOrderLine).GetProperties(),
            property => property.Name == "StockQuantity" || property.Name == "CostPrice");
    }

    private static PurchaseOrder CreateOrder(DateOnly? expectedDeliveryDate = null) =>
        new(
            "po-20260904-000000000-ABCDEF12",
            10,
            "SUP-01",
            "Nhà cung cấp",
            null,
            new DateOnly(2026, 9, 4),
            expectedDeliveryDate,
            null,
            Now);

    private static PurchaseOrder CreateOrderedOrder(out PurchaseOrderLine line)
    {
        var purchaseOrder = CreateOrder();
        line = purchaseOrder.AddLine(1, "P01", "Sản phẩm", "Hộp", 3, 500, 1, Now);
        purchaseOrder.FinalizeSnapshotsAndMarkOrdered(
            new PurchaseOrder.SupplierSnapshot("SUP-01", "Nhà cung cấp", null),
            new Dictionary<int, PurchaseOrder.ProductSnapshot>
            {
                [line.ProductId] = new("P01", "Sản phẩm", "Hộp")
            },
            7,
            Now.AddMinutes(1));
        return purchaseOrder;
    }
}
