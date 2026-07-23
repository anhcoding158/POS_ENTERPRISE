using POS.Application.DTOs.Printing;
using POS.Domain.Enums;
using POS.Infrastructure.Printing;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ReceiptSnapshotSerializationTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                22,
                1,
                15,
                0,
                TimeSpan.Zero);

    private static readonly DateTimeOffset
        PaidAtUtc =
            CreatedAtUtc.AddMinutes(
                2);

    private readonly ReceiptSnapshotJsonSerializer
        _serializer =
            new();

    [Fact]
    public void Configured_snapshot_must_round_trip_unicode_and_vnd()
    {
        var source =
            CreateSnapshot();

        var json =
            _serializer.Serialize(
                source);

        var restored =
            _serializer.Deserialize(
                json);

        Assert.Equal(
            ReceiptRequest.CurrentSnapshotVersion,
            restored.SnapshotVersion);

        Assert.True(
            restored.Store.IsConfigured);

        Assert.Equal(
            "Cửa hàng Ánh Dương",
            restored.Store.Name);

        Assert.Equal(
            "123 Đường Trần Phú, Hà Nội",
            restored.Store.Address);

        Assert.Equal(
            "Cảm ơn quý khách và hẹn gặp lại!",
            restored.Store.FooterMessage);

        Assert.Equal(
            "HD-20260722-000042",
            restored.OrderCode);

        Assert.Equal(
            "Nguyễn Văn An",
            restored.CashierName);

        Assert.Equal(
            "Trần Thị Bình",
            restored.CustomerName);

        Assert.Equal(
            80_000,
            restored.Subtotal);

        Assert.Equal(
            5_000,
            restored.DiscountAmount);

        Assert.Equal(
            75_000,
            restored.TotalAmount);

        Assert.Equal(
            100_000,
            restored.CashReceived);

        Assert.Equal(
            25_000,
            restored.ChangeAmount);

        var line =
            Assert.Single(
                restored.Lines);

        Assert.Equal(
            "Cà phê sữa đá",
            line.ProductName);

        Assert.Equal(
            "Ít đá, ít ngọt",
            line.Notes);

        var modifier =
            Assert.Single(
                line.Modifiers);

        Assert.Equal(
            "Trân châu đường đen",
            modifier.Name);
    }

    [Fact]
    public void Reprint_metadata_must_round_trip()
    {
        var source =
            CreateSnapshot(
                copyKind:
                    ReceiptCopyKind.Reprint,

                copyNumber:
                    3);

        var restored =
            _serializer.Deserialize(
                _serializer.Serialize(
                    source));

        Assert.True(
            restored.IsReprint);

        Assert.Equal(
            ReceiptCopyKind.Reprint,
            restored.CopyKind);

        Assert.Equal(
            3,
            restored.CopyNumber);
    }

    [Fact]
    public void Unconfigured_store_must_round_trip_canonically()
    {
        var source =
            CreateSnapshot(
                store:
                    ReceiptStoreSnapshotDto
                        .Unconfigured);

        var restored =
            _serializer.Deserialize(
                _serializer.Serialize(
                    source));

        Assert.False(
            restored.Store.IsConfigured);

        Assert.Equal(
            "CHƯA CẤU HÌNH CỬA HÀNG",
            restored.Store.Name);
    }

    [Fact]
    public void Serialization_must_be_deterministic_and_hide_internal_data()
    {
        var source =
            CreateSnapshot();

        var first =
            _serializer.Serialize(
                source);

        var second =
            _serializer.Serialize(
                source);

        Assert.Equal(
            first,
            second);

        Assert.False(
            first.Contains(
                "unitCostPrice",
                StringComparison.OrdinalIgnoreCase));

        Assert.False(
            first.Contains(
                "wifiPassword",
                StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            "Cửa hàng Ánh Dương",
            first);
    }

    [Fact]
    public void Unsupported_snapshot_version_must_be_rejected()
    {
        var json =
            _serializer.Serialize(
                CreateSnapshot());

        Assert.Contains(
            "\"snapshotVersion\":1",
            json);

        var changed =
            json.Replace(
                "\"snapshotVersion\":1",
                "\"snapshotVersion\":99",
                StringComparison.Ordinal);

        var exception =
            Assert.Throws<InvalidDataException>(
                () =>
                    _serializer.Deserialize(
                        changed));

        Assert.Contains(
            "99",
            exception.Message);
    }

    [Fact]
    public void Unknown_json_member_must_be_rejected()
    {
        var json =
            _serializer.Serialize(
                CreateSnapshot());

        var changed =
            json[..^1] +
            ",\"unexpected\":true}";

        Assert.Throws<InvalidDataException>(
            () =>
                _serializer.Deserialize(
                    changed));
    }

    [Fact]
    public void Tampered_total_amount_must_be_rejected()
    {
        var json =
            _serializer.Serialize(
                CreateSnapshot());

        Assert.Contains(
            "\"totalAmount\":75000",
            json);

        var changed =
            json.Replace(
                "\"totalAmount\":75000",
                "\"totalAmount\":74000",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () =>
                _serializer.Deserialize(
                    changed));
    }

    [Fact]
    public void Malformed_json_must_be_rejected()
    {
        Assert.Throws<InvalidDataException>(
            () =>
                _serializer.Deserialize(
                    "{not-valid-json"));
    }

    private static ReceiptRequest CreateSnapshot(
        ReceiptCopyKind copyKind =
            ReceiptCopyKind.Original,
        int copyNumber = 0,
        ReceiptStoreSnapshotDto? store = null)
    {
        return new ReceiptRequest(
            store:
                store ??
                new ReceiptStoreSnapshotDto(
                    name:
                        "Cửa hàng Ánh Dương",

                    address:
                        "123 Đường Trần Phú, Hà Nội",

                    phone:
                        "0999 888 777",

                    taxCode:
                        "0101234567",

                    footerMessage:
                        "Cảm ơn quý khách và hẹn gặp lại!"),

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
                80_000,

            discountAmount:
                5_000,

            totalAmount:
                75_000,

            cashReceived:
                100_000,

            changeAmount:
                25_000,

            lines:
                [
                    CreateLine()
                ],

            customerName:
                "Trần Thị Bình",

            restaurantTableName:
                "Bàn VIP 01",

            discountCode:
                "KM-HÈ-2026",

            notes:
                "Khách cần hóa đơn VAT",

            paidAtUtc:
                PaidAtUtc);
    }

    private static ReceiptLineDto CreateLine()
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
                2,

            unitSalePrice:
                35_000,

            modifierAmountPerUnit:
                10_000,

            finalUnitPrice:
                45_000,

            grossAmount:
                90_000,

            lineDiscountAmount:
                10_000,

            netAmount:
                80_000,

            notes:
                "Ít đá, ít ngọt",

            modifiers:
                [
                    new ReceiptModifierDto(
                        modifierId:
                            501,

                        modifierGroupId:
                            51,

                        modifierGroupName:
                            "Topping",

                        name:
                            "Trân châu đường đen",

                        quantity:
                            2,

                        unitAdditionalPrice:
                            5_000,

                        amountPerProductUnit:
                            10_000)
                ]);
    }
}