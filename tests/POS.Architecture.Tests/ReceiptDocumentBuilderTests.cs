using POS.Application.DTOs.Printing;
using POS.Domain.Enums;
using POS.Infrastructure.Printing;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Documents;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử renderer hóa đơn K80.
///
/// Test tập trung vào:
/// - kích thước K80;
/// - Unicode tiếng Việt;
/// - dữ liệu nghiệp vụ bắt buộc;
/// - wrap tên sản phẩm dài;
/// - nhãn bản in lại;
/// - chặn Store chưa cấu hình;
/// - không làm lộ dữ liệu kỹ thuật.
/// </summary>
public sealed class ReceiptDocumentBuilderTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                23,
                14,
                30,
                0,
                TimeSpan.Zero);

    [Fact]
    public void
        Build_must_create_k80_document_with_complete_receipt_content()
    {
        RunOnSta(
            () =>
            {
                var request =
                    CreateReceipt();

                var builder =
                    new ReceiptDocumentBuilder();

                var document =
                    builder.Build(
                        request);

                Assert.Equal(
                    ReceiptDocumentBuilder.K80PageWidth,
                    document.PageWidth,
                    precision:
                        2);

                Assert.Equal(
                    ReceiptDocumentBuilder.K80HorizontalMargin,
                    document.PagePadding.Left,
                    precision:
                        2);

                Assert.Equal(
                    ReceiptDocumentBuilder.K80HorizontalMargin,
                    document.PagePadding.Right,
                    precision:
                        2);

                Assert.True(
                    double.IsPositiveInfinity(
                        document.ColumnWidth));

                var text =
                    ReadDocumentText(
                        document);

                Assert.Contains(
                    "CỬA HÀNG ÁNH DƯƠNG",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "123 Đường Trần Phú, Hà Nội",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "Điện thoại: 0999 888 777",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "Mã số thuế: 0101234567",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "HÓA ĐƠN BÁN HÀNG",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "HD-K80-0001",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "Thu ngân Nguyễn Văn Á",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "Tiền mặt",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "Cà phê sữa đá",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "Trân châu trắng",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "Ít đá, ít ngọt",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "90.000 ₫",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "80.000 ₫",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "100.000 ₫",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "20.000 ₫",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "Giao hàng tại quầy",
                    text,
                    StringComparison.Ordinal);

                Assert.Contains(
                    "Cảm ơn quý khách!",
                    text,
                    StringComparison.Ordinal);
            });
    }

    [Fact]
    public void
        Build_must_preserve_long_vietnamese_product_name_for_wrapping()
    {
        RunOnSta(
            () =>
            {
                const string longProductName =
                    "Combo cà phê sữa đá đặc biệt kèm bánh " +
                    "croissant bơ Pháp và topping trân châu trắng";

                var request =
                    CreateReceipt(
                        secondProductName:
                            longProductName);

                var builder =
                    new ReceiptDocumentBuilder();

                var document =
                    builder.Build(
                        request);

                var text =
                    ReadDocumentText(
                        document);

                Assert.Contains(
                    longProductName,
                    text,
                    StringComparison.Ordinal);

                Assert.Equal(
                    ReceiptDocumentBuilder.K80ContentWidth,
                    document.PageWidth -
                    document.PagePadding.Left -
                    document.PagePadding.Right,
                    precision:
                        2);
            });
    }

    [Fact]
    public void
        Build_reprint_must_show_copy_label_and_number()
    {
        RunOnSta(
            () =>
            {
                var request =
                    CreateReceipt(
                        copyKind:
                            ReceiptCopyKind.Reprint,

                        copyNumber:
                            2);

                var builder =
                    new ReceiptDocumentBuilder();

                var document =
                    builder.Build(
                        request);

                var text =
                    ReadDocumentText(
                        document);

                Assert.Contains(
                    "BẢN IN LẠI LẦN 2",
                    text,
                    StringComparison.Ordinal);
            });
    }

    [Fact]
    public void
        Build_original_must_not_show_reprint_label()
    {
        RunOnSta(
            () =>
            {
                var builder =
                    new ReceiptDocumentBuilder();

                var document =
                    builder.Build(
                        CreateReceipt());

                var text =
                    ReadDocumentText(
                        document);

                Assert.DoesNotContain(
                    "BẢN IN LẠI",
                    text,
                    StringComparison.Ordinal);
            });
    }

    [Fact]
    public void
        Build_must_reject_unconfigured_store()
    {
        RunOnSta(
            () =>
            {
                var request =
                    CreateReceipt(
                        useUnconfiguredStore:
                            true);

                var builder =
                    new ReceiptDocumentBuilder();

                var exception =
                    Assert.Throws<
                        InvalidOperationException>(
                            () =>
                                builder.Build(
                                    request));

                Assert.Contains(
                    "chưa được cấu hình",
                    exception.Message,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    [Theory]
    [InlineData(
        PaymentMethod.Cash,
        "Tiền mặt")]
    [InlineData(
        PaymentMethod.VietQr,
        "VietQR")]
    [InlineData(
        PaymentMethod.BankTransfer,
        "Chuyển khoản")]
    [InlineData(
        PaymentMethod.Card,
        "Thẻ")]
    public void
        Build_must_render_supported_payment_method(
            PaymentMethod paymentMethod,
            string expectedText)
    {
        RunOnSta(
            () =>
            {
                var builder =
                    new ReceiptDocumentBuilder();

                var document =
                    builder.Build(
                        CreateReceipt(
                            paymentMethod:
                                paymentMethod));

                var text =
                    ReadDocumentText(
                        document);

                Assert.Contains(
                    expectedText,
                    text,
                    StringComparison.Ordinal);

                if (paymentMethod !=
                    PaymentMethod.Cash)
                {
                    Assert.DoesNotContain(
                        "Tiền khách đưa",
                        text,
                        StringComparison.Ordinal);

                    Assert.DoesNotContain(
                        "TIỀN THỪA",
                        text,
                        StringComparison.Ordinal);
                }
            });
    }

    [Fact]
    public void
        Rendered_document_must_not_contain_cost_or_secret_fields()
    {
        RunOnSta(
            () =>
            {
                var builder =
                    new ReceiptDocumentBuilder();

                var document =
                    builder.Build(
                        CreateReceipt());

                var text =
                    ReadDocumentText(
                        document);

                Assert.DoesNotContain(
                    "UnitCostPrice",
                    text,
                    StringComparison.OrdinalIgnoreCase);

                Assert.DoesNotContain(
                    "CostPrice",
                    text,
                    StringComparison.OrdinalIgnoreCase);

                Assert.DoesNotContain(
                    "WifiPassword",
                    text,
                    StringComparison.OrdinalIgnoreCase);

                Assert.DoesNotContain(
                    "ConnectionString",
                    text,
                    StringComparison.OrdinalIgnoreCase);

                Assert.DoesNotContain(
                    "PasswordHash",
                    text,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    private static ReceiptRequest CreateReceipt(
        string? secondProductName = null,
        ReceiptCopyKind copyKind =
            ReceiptCopyKind.Original,
        int copyNumber = 0,
        PaymentMethod paymentMethod =
            PaymentMethod.Cash,
        bool useUnconfiguredStore = false)
    {
        var modifier =
            new ReceiptModifierDto(
                modifierId:
                    1,

                modifierGroupId:
                    1,

                modifierGroupName:
                    "Topping",

                name:
                    "Trân châu trắng",

                quantity:
                    1,

                unitAdditionalPrice:
                    5_000,

                amountPerProductUnit:
                    5_000);

        var firstLine =
            new ReceiptLineDto(
                orderItemId:
                    1,

                productId:
                    10,

                productCode:
                    "CF-SUA-DA",

                productName:
                    "Cà phê sữa đá",

                unitName:
                    "Ly",

                quantity:
                    2,

                unitSalePrice:
                    30_000,

                modifierAmountPerUnit:
                    5_000,

                finalUnitPrice:
                    35_000,

                grossAmount:
                    70_000,

                lineDiscountAmount:
                    5_000,

                netAmount:
                    65_000,

                notes:
                    "Ít đá, ít ngọt",

                modifiers:
                [
                    modifier
                ]);

        var secondLine =
            new ReceiptLineDto(
                orderItemId:
                    2,

                productId:
                    20,

                productCode:
                    "BANH-BO",

                productName:
                    secondProductName ??
                    "Bánh croissant bơ",

                unitName:
                    "Cái",

                quantity:
                    1,

                unitSalePrice:
                    25_000,

                modifierAmountPerUnit:
                    0,

                finalUnitPrice:
                    25_000,

                grossAmount:
                    25_000,

                lineDiscountAmount:
                    0,

                netAmount:
                    25_000,

                notes:
                    null,

                modifiers:
                    []);

        var store =
            useUnconfiguredStore
                ? ReceiptStoreSnapshotDto
                    .Unconfigured
                : new ReceiptStoreSnapshotDto(
                    name:
                        "CỬA HÀNG ÁNH DƯƠNG",

                    address:
                        "123 Đường Trần Phú, Hà Nội",

                    phone:
                        "0999 888 777",

                    taxCode:
                        "0101234567",

                    footerMessage:
                        "Cảm ơn quý khách!");

        var cashReceived =
            paymentMethod ==
            PaymentMethod.Cash
                ? 100_000
                : 0;

        var changeAmount =
            paymentMethod ==
            PaymentMethod.Cash
                ? 20_000
                : 0;

        return new ReceiptRequest(
            store:
                store,

            copyKind:
                copyKind,

            copyNumber:
                copyNumber,

            orderId:
                1001,

            orderCode:
                "HD-K80-0001",

            cashierName:
                "Thu ngân Nguyễn Văn Á",

            createdAtUtc:
                CreatedAtUtc,

            paymentMethod:
                paymentMethod,

            subtotal:
                90_000,

            discountAmount:
                10_000,

            totalAmount:
                80_000,

            cashReceived:
                cashReceived,

            changeAmount:
                changeAmount,

            lines:
            [
                firstLine,
                secondLine
            ],

            customerName:
                "Khách hàng Trần Thị B",

            restaurantTableName:
                "Bàn A01",

            discountCode:
                "GIAM10K",

            notes:
                "Giao hàng tại quầy",

            paidAtUtc:
                CreatedAtUtc.AddMinutes(
                    2));
    }

    private static string ReadDocumentText(
        FlowDocument document)
    {
        return new TextRange(
                document.ContentStart,
                document.ContentEnd)
            .Text
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Trim();
    }

    private static void RunOnSta(
        Action action)
    {
        ArgumentNullException.ThrowIfNull(
            action);

        Exception? capturedException =
            null;

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception exception)
                    {
                        capturedException =
                            exception;
                    }
                });

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();
        thread.Join();

        if (capturedException is not null)
        {
            ExceptionDispatchInfo
                .Capture(
                    capturedException)
                .Throw();
        }
    }
}