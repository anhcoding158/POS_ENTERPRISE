using POS.Application.DTOs.Printing;
using POS.Domain.Enums;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace POS.Infrastructure.Printing;

/// <summary>
/// Dựng FlowDocument cho hóa đơn giấy K80.
///
/// Builder chỉ sử dụng ReceiptRequest bất biến.
/// Không đọc lại Product, Order, Store hoặc dữ liệu sống.
///
/// Builder không mở PrintDialog và không gửi print job.
/// Việc preview/in thuộc WpfReceiptService và Presentation.
/// </summary>
public sealed class ReceiptDocumentBuilder
{
    /*
     * WPF sử dụng device-independent pixel:
     * 1 inch = 96 DIP.
     *
     * 80 mm tương đương khoảng 302.36 DIP.
     */
    public const double K80PageWidth = 302.36;

    /*
     * Chừa khoảng 3 mm ở mỗi bên để phù hợp với
     * vùng không in được của nhiều máy K80.
     */
    public const double K80HorizontalMargin = 11.34;

    public const double K80VerticalMargin = 9.45;

    public const double K80ContentWidth =
        K80PageWidth -
        (K80HorizontalMargin * 2);

    private const double BaseFontSize = 10.5;
    private const double SmallFontSize = 9;
    private const double StoreNameFontSize = 15;
    private const double ReceiptTitleFontSize = 13;
    private const double TotalFontSize = 13;

    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private static readonly FontFamily
        ReceiptFontFamily =
            new(
                "Segoe UI");

    private static readonly FontFamily
        SeparatorFontFamily =
            new(
                "Consolas");

    /// <summary>
    /// Dựng tài liệu hóa đơn K80.
    ///
    /// Store chưa được cấu hình không được phép đi vào
    /// preview hoặc print pipeline production.
    /// </summary>
    public FlowDocument Build(
        ReceiptRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (!request.Store.IsConfigured)
        {
            throw new InvalidOperationException(
                "Không thể dựng hóa đơn vì thông tin " +
                "cửa hàng chưa được cấu hình.");
        }

        var document =
            CreateDocument();

        AddStoreHeader(
            document,
            request);

        AddReceiptIdentity(
            document,
            request);

        AddOptionalOrderInformation(
            document,
            request);

        AddSeparator(
            document);

        AddLineHeader(
            document);

        foreach (var line in request.Lines)
        {
            AddReceiptLine(
                document,
                line);
        }

        AddSeparator(
            document);

        AddTotals(
            document,
            request);

        AddOptionalReceiptNotes(
            document,
            request);

        AddFooter(
            document,
            request);

        return document;
    }

    private static FlowDocument CreateDocument()
    {
        return new FlowDocument
        {
            PageWidth =
                K80PageWidth,

            PagePadding =
                new Thickness(
                    K80HorizontalMargin,
                    K80VerticalMargin,
                    K80HorizontalMargin,
                    K80VerticalMargin),

            ColumnWidth =
                double.PositiveInfinity,

            IsColumnWidthFlexible =
                true,

            FontFamily =
                ReceiptFontFamily,

            FontSize =
                BaseFontSize,

            Foreground =
                Brushes.Black,

            Background =
                Brushes.White,

            LineStackingStrategy =
                LineStackingStrategy.BlockLineHeight,

            LineHeight =
                15,

            TextAlignment =
                TextAlignment.Left
        };
    }

    private static void AddStoreHeader(
        FlowDocument document,
        ReceiptRequest request)
    {
        document.Blocks.Add(
            CreateCenteredParagraph(
                request.Store.Name,
                StoreNameFontSize,
                FontWeights.Bold,
                tag:
                    "Receipt.StoreName"));

        AddOptionalCenteredParagraph(
            document,
            request.Store.Address,
            SmallFontSize,
            "Receipt.StoreAddress");

        if (!string.IsNullOrWhiteSpace(
                request.Store.Phone))
        {
            document.Blocks.Add(
                CreateCenteredParagraph(
                    $"Điện thoại: {request.Store.Phone}",
                    SmallFontSize,
                    FontWeights.Normal,
                    tag:
                        "Receipt.StorePhone"));
        }

        if (!string.IsNullOrWhiteSpace(
                request.Store.TaxCode))
        {
            document.Blocks.Add(
                CreateCenteredParagraph(
                    $"Mã số thuế: {request.Store.TaxCode}",
                    SmallFontSize,
                    FontWeights.Normal,
                    tag:
                        "Receipt.StoreTaxCode"));
        }

        document.Blocks.Add(
            CreateCenteredParagraph(
                "HÓA ĐƠN BÁN HÀNG",
                ReceiptTitleFontSize,
                FontWeights.Bold,
                topMargin:
                    7,
                tag:
                    "Receipt.Title"));

        if (request.IsReprint)
        {
            document.Blocks.Add(
                CreateCenteredParagraph(
                    $"*** BẢN IN LẠI LẦN " +
                    $"{request.CopyNumber:N0} ***",
                    BaseFontSize,
                    FontWeights.Bold,
                    topMargin:
                        2,
                    tag:
                        "Receipt.Reprint"));
        }
    }

    private static void AddReceiptIdentity(
        FlowDocument document,
        ReceiptRequest request)
    {
        document.Blocks.Add(
            CreateLabelValueParagraph(
                "Mã đơn:",
                request.OrderCode,
                tag:
                    "Receipt.OrderCode"));

        document.Blocks.Add(
            CreateLabelValueParagraph(
                "Thời gian:",
                FormatDateTime(
                    request.PaidAtUtc),
                tag:
                    "Receipt.PaidAt"));

        document.Blocks.Add(
            CreateLabelValueParagraph(
                "Thu ngân:",
                request.CashierName,
                tag:
                    "Receipt.Cashier"));

        document.Blocks.Add(
            CreateLabelValueParagraph(
                "Thanh toán:",
                FormatPaymentMethod(
                    request.PaymentMethod),
                tag:
                    "Receipt.PaymentMethod"));
    }

    private static void AddOptionalOrderInformation(
        FlowDocument document,
        ReceiptRequest request)
    {
        if (!string.IsNullOrWhiteSpace(
                request.CustomerName))
        {
            document.Blocks.Add(
                CreateLabelValueParagraph(
                    "Khách hàng:",
                    request.CustomerName,
                    tag:
                        "Receipt.Customer"));
        }

        if (!string.IsNullOrWhiteSpace(
                request.RestaurantTableName))
        {
            document.Blocks.Add(
                CreateLabelValueParagraph(
                    "Bàn:",
                    request.RestaurantTableName,
                    tag:
                        "Receipt.Table"));
        }

        if (!string.IsNullOrWhiteSpace(
                request.DiscountCode))
        {
            document.Blocks.Add(
                CreateLabelValueParagraph(
                    "Mã giảm giá:",
                    request.DiscountCode,
                    tag:
                        "Receipt.DiscountCode"));
        }
    }

    private static void AddLineHeader(
        FlowDocument document)
    {
        var table =
            CreateTwoColumnTable(
                leftStarWidth:
                    0.67,

                rightStarWidth:
                    0.33,

                tag:
                    "Receipt.LineHeader");

        AddTwoColumnRow(
            table,
            leftText:
                "SẢN PHẨM / SL × ĐƠN GIÁ",

            rightText:
                "THÀNH TIỀN",

            fontWeight:
                FontWeights.Bold,

            fontSize:
                SmallFontSize);

        document.Blocks.Add(
            table);
    }

    private static void AddReceiptLine(
        FlowDocument document,
        ReceiptLineDto line)
    {
        var productNameParagraph =
            new Paragraph
            {
                Margin =
                    new Thickness(
                        0,
                        4,
                        0,
                        0),

                FontWeight =
                    FontWeights.SemiBold,

                FontSize =
                    BaseFontSize,

                Tag =
                    $"Receipt.Line.{line.OrderItemId}.Name"
            };

        productNameParagraph.Inlines.Add(
            new Run(
                line.ProductName));

        document.Blocks.Add(
            productNameParagraph);

        var quantityAndPrice =
            $"{line.Quantity.ToString(
                "N0",
                VietnameseCulture)} " +
            $"{line.UnitName} × " +
            $"{FormatMoney(line.FinalUnitPrice)}";

        var amountTable =
            CreateTwoColumnTable(
                leftStarWidth:
                    0.67,

                rightStarWidth:
                    0.33,

                tag:
                    $"Receipt.Line.{line.OrderItemId}.Amount");

        AddTwoColumnRow(
            amountTable,
            quantityAndPrice,
            FormatMoney(
                line.NetAmount),
            FontWeights.Normal,
            SmallFontSize);

        document.Blocks.Add(
            amountTable);

        foreach (var modifier in line.Modifiers)
        {
            var modifierText =
                $"+ {modifier.Name} " +
                $"× {modifier.Quantity.ToString(
                    "N0",
                    VietnameseCulture)}";

            if (modifier.AmountPerProductUnit > 0)
            {
                modifierText +=
                    $"  (+{FormatMoney(
                        modifier.AmountPerProductUnit)}/sp)";
            }

            document.Blocks.Add(
                CreateIndentedParagraph(
                    modifierText,
                    tag:
                        $"Receipt.Line.{line.OrderItemId}.Modifier"));
        }

        if (line.LineDiscountAmount > 0)
        {
            document.Blocks.Add(
                CreateIndentedParagraph(
                    $"Giảm dòng: -" +
                    $"{FormatMoney(
                        line.LineDiscountAmount)}",
                    tag:
                        $"Receipt.Line.{line.OrderItemId}.Discount"));
        }

        if (!string.IsNullOrWhiteSpace(
                line.Notes))
        {
            document.Blocks.Add(
                CreateIndentedParagraph(
                    $"Ghi chú: {line.Notes}",
                    italic:
                        true,
                    tag:
                        $"Receipt.Line.{line.OrderItemId}.Notes"));
        }
    }

    private static void AddTotals(
        FlowDocument document,
        ReceiptRequest request)
    {
        var totalsTable =
            CreateTwoColumnTable(
                leftStarWidth:
                    0.56,

                rightStarWidth:
                    0.44,

                tag:
                    "Receipt.Totals");

        AddTwoColumnRow(
            totalsTable,
            "Tiền hàng",
            FormatMoney(
                request.Subtotal),
            FontWeights.Normal,
            BaseFontSize);

        if (request.DiscountAmount > 0)
        {
            AddTwoColumnRow(
                totalsTable,
                "Giảm giá",
                $"-{FormatMoney(
                    request.DiscountAmount)}",
                FontWeights.Normal,
                BaseFontSize);
        }

        AddTwoColumnRow(
            totalsTable,
            "TỔNG THANH TOÁN",
            FormatMoney(
                request.TotalAmount),
            FontWeights.Bold,
            TotalFontSize,
            topPadding:
                4);

        if (request.PaymentMethod ==
            PaymentMethod.Cash)
        {
            AddTwoColumnRow(
                totalsTable,
                "Tiền khách đưa",
                FormatMoney(
                    request.CashReceived),
                FontWeights.Normal,
                BaseFontSize);

            AddTwoColumnRow(
                totalsTable,
                "TIỀN THỪA",
                FormatMoney(
                    request.ChangeAmount),
                FontWeights.Bold,
                BaseFontSize);
        }

        document.Blocks.Add(
            totalsTable);
    }

    private static void AddOptionalReceiptNotes(
        FlowDocument document,
        ReceiptRequest request)
    {
        if (string.IsNullOrWhiteSpace(
                request.Notes))
        {
            return;
        }

        AddSeparator(
            document);

        var paragraph =
            new Paragraph
            {
                Margin =
                    new Thickness(
                        0,
                        2,
                        0,
                        0),

                FontSize =
                    SmallFontSize,

                Tag =
                    "Receipt.Notes"
            };

        paragraph.Inlines.Add(
            new Bold(
                new Run(
                    "Ghi chú đơn: ")));

        paragraph.Inlines.Add(
            new Run(
                request.Notes));

        document.Blocks.Add(
            paragraph);
    }

    private static void AddFooter(
        FlowDocument document,
        ReceiptRequest request)
    {
        AddSeparator(
            document);

        var footerMessage =
            string.IsNullOrWhiteSpace(
                request.Store.FooterMessage)
                ? "Cảm ơn quý khách và hẹn gặp lại!"
                : request.Store.FooterMessage;

        document.Blocks.Add(
            CreateCenteredParagraph(
                footerMessage,
                SmallFontSize,
                FontWeights.SemiBold,
                topMargin:
                    3,
                tag:
                    "Receipt.Footer"));

        document.Blocks.Add(
            CreateCenteredParagraph(
                "Hóa đơn được tạo từ dữ liệu giao dịch đã chốt.",
                8,
                FontWeights.Normal,
                topMargin:
                    2,
                tag:
                    "Receipt.SnapshotNotice"));
    }

    private static void AddSeparator(
        FlowDocument document)
    {
        document.Blocks.Add(
            CreateCenteredParagraph(
                new string(
                    '-',
                    38),
                8,
                FontWeights.Normal,
                fontFamily:
                    SeparatorFontFamily,
                tag:
                    "Receipt.Separator"));
    }

    private static Table CreateTwoColumnTable(
        double leftStarWidth,
        double rightStarWidth,
        string tag)
    {
        var table =
            new Table
            {
                CellSpacing =
                    0,

                Margin =
                    new Thickness(
                        0),

                Tag =
                    tag
            };

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        leftStarWidth,
                        GridUnitType.Star)
            });

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        rightStarWidth,
                        GridUnitType.Star)
            });

        table.RowGroups.Add(
            new TableRowGroup());

        return table;
    }

    private static void AddTwoColumnRow(
        Table table,
        string leftText,
        string rightText,
        FontWeight fontWeight,
        double fontSize,
        double topPadding = 0)
    {
        var row =
            new TableRow();

        row.Cells.Add(
            CreateTableCell(
                leftText,
                TextAlignment.Left,
                fontWeight,
                fontSize,
                topPadding));

        row.Cells.Add(
            CreateTableCell(
                rightText,
                TextAlignment.Right,
                fontWeight,
                fontSize,
                topPadding));

        table.RowGroups[0]
            .Rows
            .Add(
                row);
    }

    private static TableCell CreateTableCell(
        string text,
        TextAlignment textAlignment,
        FontWeight fontWeight,
        double fontSize,
        double topPadding)
    {
        var paragraph =
            new Paragraph(
                new Run(
                    text))
            {
                Margin =
                    new Thickness(
                        0),

                TextAlignment =
                    textAlignment,

                FontWeight =
                    fontWeight,

                FontSize =
                    fontSize
            };

        return new TableCell(
            paragraph)
        {
            Padding =
                new Thickness(
                    0,
                    topPadding,
                    0,
                    0)
        };
    }

    private static Paragraph CreateCenteredParagraph(
        string text,
        double fontSize,
        FontWeight fontWeight,
        double topMargin = 0,
        FontFamily? fontFamily = null,
        string? tag = null)
    {
        return new Paragraph(
            new Run(
                text))
        {
            Margin =
                new Thickness(
                    0,
                    topMargin,
                    0,
                    0),

            TextAlignment =
                TextAlignment.Center,

            FontSize =
                fontSize,

            FontWeight =
                fontWeight,

            FontFamily =
                fontFamily ??
                ReceiptFontFamily,

            Tag =
                tag
        };
    }

    private static Paragraph CreateLabelValueParagraph(
        string label,
        string value,
        string tag)
    {
        var paragraph =
            new Paragraph
            {
                Margin =
                    new Thickness(
                        0),

                FontSize =
                    SmallFontSize,

                Tag =
                    tag
            };

        paragraph.Inlines.Add(
            new Bold(
                new Run(
                    $"{label} ")));

        paragraph.Inlines.Add(
            new Run(
                value));

        return paragraph;
    }

    private static Paragraph CreateIndentedParagraph(
        string text,
        bool italic = false,
        string? tag = null)
    {
        var paragraph =
            new Paragraph(
                new Run(
                    text))
            {
                Margin =
                    new Thickness(
                        12,
                        0,
                        0,
                        0),

                FontSize =
                    SmallFontSize,

                Foreground =
                    Brushes.DimGray,

                Tag =
                    tag
            };

        if (italic)
        {
            paragraph.FontStyle =
                FontStyles.Italic;
        }

        return paragraph;
    }

    private static void AddOptionalCenteredParagraph(
        FlowDocument document,
        string? text,
        double fontSize,
        string tag)
    {
        if (string.IsNullOrWhiteSpace(
                text))
        {
            return;
        }

        document.Blocks.Add(
            CreateCenteredParagraph(
                text,
                fontSize,
                FontWeights.Normal,
                tag:
                    tag));
    }

    private static string FormatDateTime(
        DateTimeOffset utcDateTime)
    {
        return utcDateTime
            .ToLocalTime()
            .ToString(
                "dd/MM/yyyy HH:mm:ss",
                VietnameseCulture);
    }

    private static string FormatMoney(
        long amount)
    {
        return
            $"{amount.ToString(
                "N0",
                VietnameseCulture)} ₫";
    }

    private static string FormatPaymentMethod(
        PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Cash =>
                "Tiền mặt",

            PaymentMethod.VietQr =>
                "VietQR",

            PaymentMethod.BankTransfer =>
                "Chuyển khoản",

            PaymentMethod.Card =>
                "Thẻ",

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(paymentMethod),
                    paymentMethod,
                    "Phương thức thanh toán không hợp lệ.")
        };
    }
}