using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Printing;
using POS.Application.Factories;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ReceiptStoreSnapshotTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                22,
                8,
                15,
                0,
                TimeSpan.Zero);

    [Fact]
    public void Store_snapshot_must_normalize_unicode_fields()
    {
        var store =
            CreateStore();

        Assert.True(
            store.IsConfigured);

        Assert.Equal(
            "Cửa hàng Ánh Dương",
            store.Name);

        Assert.Equal(
            "123 Đường Trần Phú, Hà Nội",
            store.Address);

        Assert.Equal(
            "0999 888 777",
            store.Phone);

        Assert.Equal(
            "0101234567",
            store.TaxCode);

        Assert.Equal(
            "Cảm ơn quý khách và hẹn gặp lại!",
            store.FooterMessage);
    }

    [Fact]
    public void Store_snapshot_must_reject_blank_name()
    {
        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    new ReceiptStoreSnapshotDto(
                        name:
                            "   "));

        Assert.Equal(
            "name",
            exception.ParamName);
    }

    [Fact]
    public void Unconfigured_store_must_be_explicit()
    {
        var store =
            ReceiptStoreSnapshotDto.Unconfigured;

        Assert.False(
            store.IsConfigured);

        Assert.Equal(
            "CHƯA CẤU HÌNH CỬA HÀNG",
            store.Name);
    }

    [Fact]
    public void Store_snapshot_must_not_expose_wifi_password()
    {
        var property =
            typeof(ReceiptStoreSnapshotDto)
                .GetProperty(
                    "WifiPassword");

        Assert.Null(
            property);
    }

    [Fact]
    public void Original_receipt_must_use_copy_number_zero()
    {
        var receipt =
            CreateReceipt(
                copyKind:
                    ReceiptCopyKind.Original,

                copyNumber:
                    0);

        Assert.Equal(
            ReceiptCopyKind.Original,
            receipt.CopyKind);

        Assert.Equal(
            0,
            receipt.CopyNumber);

        Assert.False(
            receipt.IsReprint);

        Assert.True(
            receipt.Store.IsConfigured);
    }

    [Fact]
    public void Original_receipt_must_reject_positive_copy_number()
    {
        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    CreateReceipt(
                        copyKind:
                            ReceiptCopyKind.Original,

                        copyNumber:
                            1));

        Assert.Equal(
            "copyNumber",
            exception.ParamName);
    }

    [Fact]
    public void Reprint_receipt_must_require_positive_copy_number()
    {
        var exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    CreateReceipt(
                        copyKind:
                            ReceiptCopyKind.Reprint,

                        copyNumber:
                            0));

        Assert.Equal(
            "copyNumber",
            exception.ParamName);
    }

    [Fact]
    public void Reprint_receipt_must_preserve_store_snapshot()
    {
        var receipt =
            CreateReceipt(
                copyKind:
                    ReceiptCopyKind.Reprint,

                copyNumber:
                    2);

        Assert.True(
            receipt.IsReprint);

        Assert.Equal(
            2,
            receipt.CopyNumber);

        Assert.Equal(
            "Cửa hàng Ánh Dương",
            receipt.Store.Name);
    }

    [Fact]
    public void Factory_must_create_configured_original_snapshot()
    {
        var checkoutResult =
            CreateCheckoutResult();

        var store =
            CreateStore();

        var receipt =
            ReceiptSnapshotFactory.Create(
                checkoutResult:
                    checkoutResult,

                store:
                    store,

                receiptNotes:
                    "  Giao hàng tại quầy  ");

        Assert.Same(
            store,
            receipt.Store);

        Assert.True(
            receipt.Store.IsConfigured);

        Assert.Equal(
            ReceiptCopyKind.Original,
            receipt.CopyKind);

        Assert.Equal(
            0,
            receipt.CopyNumber);

        Assert.Equal(
            "Giao hàng tại quầy",
            receipt.Notes);

        Assert.Equal(
            checkoutResult.OrderCode,
            receipt.OrderCode);
    }

    private static ReceiptStoreSnapshotDto CreateStore()
    {
        return new ReceiptStoreSnapshotDto(
            name:
                "  Cửa hàng Ánh Dương  ",

            address:
                "  123 Đường Trần Phú, Hà Nội  ",

            phone:
                "  0999 888 777  ",

            taxCode:
                "  0101234567  ",

            footerMessage:
                "  Cảm ơn quý khách và hẹn gặp lại!  ");
    }

    private static ReceiptRequest CreateReceipt(
        ReceiptCopyKind copyKind,
        int copyNumber)
    {
        return new ReceiptRequest(
            store:
                CreateStore(),

            copyKind:
                copyKind,

            copyNumber:
                copyNumber,

            orderId:
                42,

            orderCode:
                "HD-20260722-000042",

            cashierName:
                "Nguyễn Văn An",

            createdAtUtc:
                CreatedAtUtc,

            paymentMethod:
                PaymentMethod.Cash,

            subtotal:
                75_000,

            discountAmount:
                0,

            totalAmount:
                75_000,

            cashReceived:
                100_000,

            changeAmount:
                25_000,

            lines:
                [
                    CreateReceiptLine()
                ],

            paidAtUtc:
                CreatedAtUtc);
    }

    private static ReceiptLineDto CreateReceiptLine()
    {
        return new ReceiptLineDto(
            orderItemId:
                1001,

            productId:
                101,

            productCode:
                "CF-SUA-01",

            productName:
                "Cà phê sữa đá",

            unitName:
                "Ly",

            quantity:
                1,

            unitSalePrice:
                75_000,

            modifierAmountPerUnit:
                0,

            finalUnitPrice:
                75_000,

            grossAmount:
                75_000,

            lineDiscountAmount:
                0,

            netAmount:
                75_000,

            notes:
                null,

            modifiers:
                Array.Empty<ReceiptModifierDto>());
    }

    private static CheckoutResultDto
        CreateCheckoutResult()
    {
        return new CheckoutResultDto(
            OrderId:
                42,

            OrderCode:
                "HD-20260722-000042",

            CashierUserId:
                7,

            CashierName:
                "Nguyễn Văn An",

            CustomerId:
                null,

            CustomerName:
                null,

            RestaurantTableId:
                null,

            RestaurantTableName:
                null,

            DiscountCode:
                null,

            Status:
                OrderStatus.Completed,

            PaymentMethod:
                PaymentMethod.Cash,

            Subtotal:
                75_000,

            DiscountAmount:
                0,

            TotalAmount:
                75_000,

            CashReceived:
                100_000,

            ChangeAmount:
                25_000,

            CreatedAtUtc:
                CreatedAtUtc,

            PaidAtUtc:
                CreatedAtUtc,

            Lines:
                [
                    new CheckoutLineResultDto(
                        OrderItemId:
                            1001,

                        ProductId:
                            101,

                        ProductCode:
                            "CF-SUA-01",

                        ProductName:
                            "Cà phê sữa đá",

                        UnitName:
                            "Ly",

                        Quantity:
                            1,

                        UnitCostPrice:
                            30_000,

                        UnitSalePrice:
                            75_000,

                        ModifierAmountPerUnit:
                            0,

                        FinalUnitPrice:
                            75_000,

                        GrossAmount:
                            75_000,

                        LineDiscountAmount:
                            0,

                        NetAmount:
                            75_000,

                        Notes:
                            null,

                        Modifiers:
                            Array.Empty<
                                CheckoutLineModifierResultDto>())
                ]);
    }
}