using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm tra invariant thuần Domain của tồn kho.
/// </summary>
public sealed class InventoryDomainTests
{
    private static readonly DateTimeOffset
        OccurredAtUtc =
            new(
                2026,
                7,
                20,
                10,
                30,
                0,
                TimeSpan.Zero);

    [Fact]
    public void Stock_in_must_create_positive_delta()
    {
        var movement =
            new InventoryMovement(
                productId: 1,
                InventoryMovementType.StockIn,
                quantityDelta: 5,
                quantityBefore: 10,
                quantityAfter: 15,
                reason: "Nhập hàng",
                OccurredAtUtc);

        Assert.Equal(
            5,
            movement.QuantityDelta);

        Assert.True(
            movement.IsIncrease);

        Assert.False(
            movement.IsDecrease);
    }

    [Fact]
    public void Stock_out_must_create_negative_delta()
    {
        var movement =
            new InventoryMovement(
                productId: 1,
                InventoryMovementType.StockOut,
                quantityDelta: -3,
                quantityBefore: 10,
                quantityAfter: 7,
                reason: "Hàng hỏng",
                OccurredAtUtc);

        Assert.Equal(
            -3,
            movement.QuantityDelta);

        Assert.True(
            movement.IsDecrease);
    }

    [Fact]
    public void Stocktake_may_record_zero_difference()
    {
        var movement =
            new InventoryMovement(
                productId: 1,
                InventoryMovementType.Stocktake,
                quantityDelta: 0,
                quantityBefore: 10,
                quantityAfter: 10,
                reason: "Kiểm kê cuối ngày",
                OccurredAtUtc);

        Assert.Equal(
            10,
            movement.QuantityAfter);

        Assert.False(
            movement.IsIncrease);

        Assert.False(
            movement.IsDecrease);
    }

    [Fact]
    public void Stock_in_must_reject_negative_delta()
    {
        var exception =
            Assert.Throws<DomainException>(
                () =>
                    new InventoryMovement(
                        productId: 1,
                        InventoryMovementType.StockIn,
                        quantityDelta: -5,
                        quantityBefore: 10,
                        quantityAfter: 5,
                        reason: "Sai hướng",
                        OccurredAtUtc));

        Assert.Equal(
            "INVENTORY.INCREASE_REQUIRED",
            exception.Code);
    }

    [Fact]
    public void Movement_must_reject_inconsistent_quantities()
    {
        var exception =
            Assert.Throws<DomainException>(
                () =>
                    new InventoryMovement(
                        productId: 1,
                        InventoryMovementType.Adjustment,
                        quantityDelta: 5,
                        quantityBefore: 10,
                        quantityAfter: 20,
                        reason: "Sai phương trình",
                        OccurredAtUtc));

        Assert.Equal(
            "INVENTORY.INCONSISTENT_QUANTITIES",
            exception.Code);
    }

    [Fact]
    public void Reference_type_and_id_must_be_supplied_together()
    {
        var exception =
            Assert.Throws<DomainException>(
                () =>
                    new InventoryMovement(
                        productId: 1,
                        InventoryMovementType.StockIn,
                        quantityDelta: 5,
                        quantityBefore: 10,
                        quantityAfter: 15,
                        reason: "Nhập hàng",
                        OccurredAtUtc,
                        referenceType: "RECEIPT",
                        referenceId: null));

        Assert.Equal(
            "INVENTORY.INCOMPLETE_REFERENCE",
            exception.Code);
    }

    [Fact]
    public void Opening_balance_must_start_from_zero()
    {
        var exception =
            Assert.Throws<DomainException>(
                () =>
                    new InventoryMovement(
                        productId: 1,
                        InventoryMovementType.OpeningBalance,
                        quantityDelta: 5,
                        quantityBefore: 2,
                        quantityAfter: 7,
                        reason: "Tồn đầu kỳ",
                        OccurredAtUtc));

        Assert.Equal(
            "INVENTORY.INVALID_OPENING_BALANCE",
            exception.Code);
    }

    [Fact]
    public void Product_must_reject_negative_stock_when_disabled()
    {
        var product =
            CreateProduct(
                stockQuantity: 2,
                allowNegativeStock: false);

        var exception =
            Assert.Throws<DomainException>(
                () =>
                    product.DecreaseStock(
                        quantity: 3,
                        OccurredAtUtc));

        Assert.Equal(
            "PRODUCT.INSUFFICIENT_STOCK",
            exception.Code);

        Assert.Equal(
            2,
            product.StockQuantity);
    }

    [Fact]
    public void Product_may_have_negative_stock_when_enabled()
    {
        var product =
            CreateProduct(
                stockQuantity: 2,
                allowNegativeStock: true);

        product.DecreaseStock(
            quantity: 5,
            OccurredAtUtc);

        Assert.Equal(
            -3,
            product.StockQuantity);
    }

    [Fact]
    public void Product_must_reject_stock_overflow()
    {
        var product =
            CreateProduct(
                BusinessRules.Products
                    .MaximumStockQuantity,
                allowNegativeStock: false);

        var exception =
            Assert.Throws<DomainException>(
                () =>
                    product.IncreaseStock(
                        quantity: 1,
                        OccurredAtUtc));

        Assert.Equal(
            "PRODUCT.STOCK_OVERFLOW",
            exception.Code);
    }

    private static Product CreateProduct(
        int stockQuantity,
        bool allowNegativeStock)
    {
        var createdAtUtc =
            OccurredAtUtc
                .AddHours(-1);

        return new Product(
            categoryId: 1,
            code: "TEST-001",
            name: "Sản phẩm kiểm thử",
            unitName: "Cái",
            costPrice: 10_000,
            salePrice: 15_000,
            stockQuantity,
            minimumStock: 1,
            trackInventory: true,
            allowNegativeStock,
            createdAtUtc);
    }
}