using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Printing;
using POS.Application.Factories;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Khóa contract bất biến của snapshot hóa đơn.
///
/// Các test bảo đảm renderer và reprint trong tương lai
/// không phải đọc lại Product, Modifier hoặc Order đang sống.
/// </summary>
public sealed class ReceiptSnapshotTests
{
    private static readonly DateTimeOffset
        CreatedAt =
            new(
                2026,
                7,
                22,
                8,
                15,
                0,
                TimeSpan.FromHours(7));

    private static readonly DateTimeOffset
        PaidAt =
            new(
                2026,
                7,
                22,
                8,
                17,
                30,
                TimeSpan.FromHours(7));

    [Fact]
    public void Valid_cash_receipt_must_preserve_unicode_and_vnd_values()
    {
        var receipt =
            CreateReceipt();

        Assert.Equal(
            ReceiptRequest.CurrentSnapshotVersion,
            receipt.SnapshotVersion);

        Assert.Equal(
            42,
            receipt.OrderId);

        Assert.Equal(
            "HD-20260722-000042",
            receipt.OrderCode);

        Assert.Equal(
            "Nguyễn Văn An",
            receipt.CashierName);

        Assert.Equal(
            "Trần Thị Bình",
            receipt.CustomerName);

        Assert.Equal(
            "Bàn VIP 01",
            receipt.RestaurantTableName);

        Assert.Equal(
            "KM-HÈ-2026",
            receipt.DiscountCode);

        Assert.Equal(
            "Khách cần hóa đơn VAT",
            receipt.Notes);

        Assert.Equal(
            CreatedAt.ToUniversalTime(),
            receipt.CreatedAtUtc);

        Assert.Equal(
            PaidAt.ToUniversalTime(),
            receipt.PaidAtUtc);

        Assert.Equal(
            PaymentMethod.Cash,
            receipt.PaymentMethod);

        Assert.Equal(
            80_000,
            receipt.Subtotal);

        Assert.Equal(
            5_000,
            receipt.DiscountAmount);

        Assert.Equal(
            75_000,
            receipt.TotalAmount);

        Assert.Equal(
            100_000,
            receipt.CashReceived);

        Assert.Equal(
            25_000,
            receipt.ChangeAmount);

        var line =
            Assert.Single(
                receipt.Lines);

        Assert.Equal(
            1001,
            line.OrderItemId);

        Assert.Equal(
            101,
            line.ProductId);

        Assert.Equal(
            "CF-SUA-01",
            line.ProductCode);

        Assert.Equal(
            "Cà phê sữa đá",
            line.ProductName);

        Assert.Equal(
            "Ly",
            line.UnitName);

        Assert.Equal(
            2,
            line.Quantity);

        Assert.Equal(
            35_000,
            line.UnitSalePrice);

        Assert.Equal(
            10_000,
            line.ModifierAmountPerUnit);

        Assert.Equal(
            45_000,
            line.FinalUnitPrice);

        Assert.Equal(
            90_000,
            line.GrossAmount);

        Assert.Equal(
            10_000,
            line.LineDiscountAmount);

        Assert.Equal(
            80_000,
            line.NetAmount);

        Assert.Equal(
            "Ít đá, ít ngọt",
            line.Notes);

        var modifier =
            Assert.Single(
                line.Modifiers);

        Assert.Equal(
            501,
            modifier.ModifierId);

        Assert.Equal(
            51,
            modifier.ModifierGroupId);

        Assert.Equal(
            "Topping",
            modifier.ModifierGroupName);

        Assert.Equal(
            "Trân châu đường đen",
            modifier.Name);

        Assert.Equal(
            2,
            modifier.Quantity);

        Assert.Equal(
            5_000,
            modifier.UnitAdditionalPrice);

        Assert.Equal(
            10_000,
            modifier.AmountPerProductUnit);
    }

    [Fact]
    public void Receipt_line_must_copy_modifier_collection()
    {
        var sourceModifiers =
            new List<ReceiptModifierDto>
            {
                CreateModifier()
            };

        var line =
            CreateLine(
                sourceModifiers);

        sourceModifiers.Clear();

        Assert.Single(
            line.Modifiers);

        var readOnlyModifiers =
            Assert.IsAssignableFrom<
                IList<ReceiptModifierDto>>(
                line.Modifiers);

        Assert.Throws<NotSupportedException>(
            () =>
                readOnlyModifiers.Add(
                    CreateModifier()));
    }

    [Fact]
    public void Receipt_request_must_copy_line_collection()
    {
        var sourceLines =
            new List<ReceiptLineDto>
            {
                CreateLine()
            };

        var receipt =
            CreateReceipt(
                lines:
                    sourceLines);

        sourceLines.Clear();

        Assert.Single(
            receipt.Lines);

        var readOnlyLines =
            Assert.IsAssignableFrom<
                IList<ReceiptLineDto>>(
                receipt.Lines);

        Assert.Throws<NotSupportedException>(
            () =>
                readOnlyLines.Add(
                    CreateLine()));
    }

    [Fact]
    public void Receipt_modifier_must_reject_inconsistent_amount()
    {
        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    new ReceiptModifierDto(
                        modifierId: 501,
                        modifierGroupId: 51,
                        modifierGroupName: "Topping",
                        name: "Trân châu đường đen",
                        quantity: 2,
                        unitAdditionalPrice: 5_000,
                        amountPerProductUnit: 9_000));

        Assert.Equal(
            "amountPerProductUnit",
            exception.ParamName);
    }

    [Fact]
    public void Receipt_line_must_reject_inconsistent_modifier_total()
    {
        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    CreateLine(
                        modifierAmountPerUnit:
                            9_000,

                        finalUnitPrice:
                            44_000,

                        grossAmount:
                            88_000,

                        lineDiscountAmount:
                            8_000,

                        netAmount:
                            80_000));

        Assert.Equal(
            "modifierAmountPerUnit",
            exception.ParamName);
    }

    [Fact]
    public void Receipt_line_must_reject_inconsistent_net_amount()
    {
        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    CreateLine(
                        netAmount:
                            79_000));

        Assert.Equal(
            "netAmount",
            exception.ParamName);
    }

    [Fact]
    public void Receipt_request_must_reject_inconsistent_subtotal()
    {
        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    CreateReceipt(
                        subtotal:
                            79_000,

                        discountAmount:
                            4_000,

                        totalAmount:
                            75_000));

        Assert.Equal(
            "subtotal",
            exception.ParamName);
    }

    [Fact]
    public void Cash_receipt_must_reject_incorrect_change()
    {
        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    CreateReceipt(
                        changeAmount:
                            20_000));

        Assert.Equal(
            "changeAmount",
            exception.ParamName);
    }

    [Fact]
    public void Non_cash_receipt_must_reject_cash_values()
    {
        var exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    CreateReceipt(
                        paymentMethod:
                            PaymentMethod.BankTransfer,

                        cashReceived:
                            75_000,

                        changeAmount:
                            0));

        Assert.Equal(
            "cashReceived",
            exception.ParamName);
    }

    [Fact]
    public void Factory_must_map_completed_checkout_to_receipt_snapshot()
    {
        var checkoutResult =
            CreateCheckoutResult();

        var receipt =
            ReceiptSnapshotFactory.Create(
                checkoutResult,
                receiptNotes:
                    "  Bản in lần đầu  ");

        Assert.Equal(
            checkoutResult.OrderId,
            receipt.OrderId);

        Assert.Equal(
            checkoutResult.OrderCode,
            receipt.OrderCode);

        Assert.Equal(
            checkoutResult.CashierName,
            receipt.CashierName);

        Assert.Equal(
            checkoutResult.CustomerName,
            receipt.CustomerName);

        Assert.Equal(
            checkoutResult.RestaurantTableName,
            receipt.RestaurantTableName);

        Assert.Equal(
            checkoutResult.DiscountCode,
            receipt.DiscountCode);

        Assert.Equal(
            "Bản in lần đầu",
            receipt.Notes);

        Assert.Equal(
            checkoutResult.CreatedAtUtc.ToUniversalTime(),
            receipt.CreatedAtUtc);

        Assert.Equal(
            checkoutResult.PaidAtUtc.ToUniversalTime(),
            receipt.PaidAtUtc);

        Assert.Equal(
            checkoutResult.PaymentMethod,
            receipt.PaymentMethod);

        Assert.Equal(
            checkoutResult.Subtotal,
            receipt.Subtotal);

        Assert.Equal(
            checkoutResult.DiscountAmount,
            receipt.DiscountAmount);

        Assert.Equal(
            checkoutResult.TotalAmount,
            receipt.TotalAmount);

        Assert.Equal(
            checkoutResult.CashReceived,
            receipt.CashReceived);

        Assert.Equal(
            checkoutResult.ChangeAmount,
            receipt.ChangeAmount);

        var sourceLine =
            Assert.Single(
                checkoutResult.Lines);

        var receiptLine =
            Assert.Single(
                receipt.Lines);

        Assert.Equal(
            sourceLine.OrderItemId,
            receiptLine.OrderItemId);

        Assert.Equal(
            sourceLine.ProductId,
            receiptLine.ProductId);

        Assert.Equal(
            sourceLine.ProductCode,
            receiptLine.ProductCode);

        Assert.Equal(
            sourceLine.ProductName,
            receiptLine.ProductName);

        Assert.Equal(
            sourceLine.UnitName,
            receiptLine.UnitName);

        Assert.Equal(
            sourceLine.Quantity,
            receiptLine.Quantity);

        Assert.Equal(
            sourceLine.UnitSalePrice,
            receiptLine.UnitSalePrice);

        Assert.Equal(
            sourceLine.NetAmount,
            receiptLine.NetAmount);

        var sourceModifier =
            Assert.Single(
                sourceLine.Modifiers);

        var receiptModifier =
            Assert.Single(
                receiptLine.Modifiers);

        Assert.Equal(
            sourceModifier.ModifierId,
            receiptModifier.ModifierId);

        Assert.Equal(
            sourceModifier.ModifierGroupId,
            receiptModifier.ModifierGroupId);

        Assert.Equal(
            sourceModifier.ModifierGroupName,
            receiptModifier.ModifierGroupName);

        Assert.Equal(
            sourceModifier.ModifierName,
            receiptModifier.Name);
    }

    [Fact]
    public void Factory_snapshot_must_not_change_when_source_lists_change()
    {
        var sourceModifiers =
            new List<CheckoutLineModifierResultDto>
            {
                CreateCheckoutModifier()
            };

        var sourceLines =
            new List<CheckoutLineResultDto>
            {
                CreateCheckoutLine(
                    sourceModifiers)
            };

        var checkoutResult =
            CreateCheckoutResult(
                sourceLines);

        var receipt =
            ReceiptSnapshotFactory.Create(
                checkoutResult);

        sourceModifiers.Clear();
        sourceLines.Clear();

        var receiptLine =
            Assert.Single(
                receipt.Lines);

        Assert.Single(
            receiptLine.Modifiers);
    }

    [Fact]
    public void Factory_must_reject_non_completed_checkout()
    {
        var checkoutResult =
            CreateCheckoutResult()
                with
            {
                Status =
                        OrderStatus.Paid
            };

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    ReceiptSnapshotFactory.Create(
                        checkoutResult));

        Assert.Equal(
            "checkoutResult",
            exception.ParamName);
    }

    [Fact]
    public void Receipt_line_contract_must_not_expose_unit_cost_price()
    {
        var property =
            typeof(ReceiptLineDto)
                .GetProperty(
                    nameof(
                        CheckoutLineResultDto
                            .UnitCostPrice));

        Assert.Null(
            property);
    }

    private static ReceiptRequest CreateReceipt(
        IEnumerable<ReceiptLineDto>? lines = null,
        PaymentMethod paymentMethod =
            PaymentMethod.Cash,
        long subtotal = 80_000,
        long discountAmount = 5_000,
        long totalAmount = 75_000,
        long cashReceived = 100_000,
        long changeAmount = 25_000)
    {
        return new ReceiptRequest(
            orderId:
                42,

            orderCode:
                "  HD-20260722-000042  ",

            cashierName:
                "  Nguyễn Văn An  ",

            createdAtUtc:
                CreatedAt,

            paymentMethod:
                paymentMethod,

            subtotal:
                subtotal,

            discountAmount:
                discountAmount,

            totalAmount:
                totalAmount,

            cashReceived:
                cashReceived,

            changeAmount:
                changeAmount,

            lines:
                lines ??
                [
                    CreateLine()
                ],

            customerName:
                "  Trần Thị Bình  ",

            restaurantTableName:
                "  Bàn VIP 01  ",

            discountCode:
                "  KM-HÈ-2026  ",

            notes:
                "  Khách cần hóa đơn VAT  ",

            paidAtUtc:
                PaidAt);
    }

    private static ReceiptLineDto CreateLine(
        IEnumerable<ReceiptModifierDto>? modifiers = null,
        long modifierAmountPerUnit = 10_000,
        long finalUnitPrice = 45_000,
        long grossAmount = 90_000,
        long lineDiscountAmount = 10_000,
        long netAmount = 80_000)
    {
        return new ReceiptLineDto(
            orderItemId:
                1001,

            productId:
                101,

            productCode:
                "  CF-SUA-01  ",

            productName:
                "  Cà phê sữa đá  ",

            unitName:
                "  Ly  ",

            quantity:
                2,

            unitSalePrice:
                35_000,

            modifierAmountPerUnit:
                modifierAmountPerUnit,

            finalUnitPrice:
                finalUnitPrice,

            grossAmount:
                grossAmount,

            lineDiscountAmount:
                lineDiscountAmount,

            netAmount:
                netAmount,

            notes:
                "  Ít đá, ít ngọt  ",

            modifiers:
                modifiers ??
                [
                    CreateModifier()
                ]);
    }

    private static ReceiptModifierDto CreateModifier()
    {
        return new ReceiptModifierDto(
            modifierId:
                501,

            modifierGroupId:
                51,

            modifierGroupName:
                "  Topping  ",

            name:
                "  Trân châu đường đen  ",

            quantity:
                2,

            unitAdditionalPrice:
                5_000,

            amountPerProductUnit:
                10_000);
    }

    private static CheckoutResultDto
        CreateCheckoutResult(
            IReadOnlyList<CheckoutLineResultDto>? lines =
                null)
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
                81,

            CustomerName:
                "Trần Thị Bình",

            RestaurantTableId:
                12,

            RestaurantTableName:
                "Bàn VIP 01",

            DiscountCode:
                "KM-HÈ-2026",

            Status:
                OrderStatus.Completed,

            PaymentMethod:
                PaymentMethod.Cash,

            Subtotal:
                80_000,

            DiscountAmount:
                5_000,

            TotalAmount:
                75_000,

            CashReceived:
                100_000,

            ChangeAmount:
                25_000,

            CreatedAtUtc:
                CreatedAt,

            PaidAtUtc:
                PaidAt,

            Lines:
                lines ??
                [
                    CreateCheckoutLine()
                ]);
    }

    private static CheckoutLineResultDto
        CreateCheckoutLine(
            IReadOnlyList<
                CheckoutLineModifierResultDto>? modifiers =
                    null)
    {
        return new CheckoutLineResultDto(
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
                2,

            UnitCostPrice:
                18_000,

            UnitSalePrice:
                35_000,

            ModifierAmountPerUnit:
                10_000,

            FinalUnitPrice:
                45_000,

            GrossAmount:
                90_000,

            LineDiscountAmount:
                10_000,

            NetAmount:
                80_000,

            Notes:
                "Ít đá, ít ngọt",

            Modifiers:
                modifiers ??
                [
                    CreateCheckoutModifier()
                ]);
    }

    private static CheckoutLineModifierResultDto
        CreateCheckoutModifier()
    {
        return new CheckoutLineModifierResultDto(
            ModifierId:
                501,

            ModifierGroupId:
                51,

            ModifierGroupName:
                "Topping",

            ModifierName:
                "Trân châu đường đen",

            Quantity:
                2,

            UnitAdditionalPrice:
                5_000,

            AmountPerProductUnit:
                10_000);
    }
}