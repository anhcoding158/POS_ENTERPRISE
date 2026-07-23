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

    private const double BaseFontSize = 9.7;
    private const double SmallFontSize = 8.4;
    private const double StoreNameFontSize = 15;
    private const double ReceiptTitleFontSize = 13.5;
    private const double TotalFontSize = 14.5;

    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private static readonly FontFamily
        ReceiptFontFamily =
            new(
                "Segoe UI");

    private static readonly FontFamily
        BrandFontFamily =
            new(
                "Georgia");

    private static readonly Brush
        ReceiptTextBrush =
            CreateFrozenBrush(
                40,
                36,
                32);

    private static readonly Brush
        ReceiptMutedBrush =
            CreateFrozenBrush(
                101,
                94,
                86);

    private static readonly Brush
        ReceiptGoldBrush =
            CreateFrozenBrush(
                177,
                128,
                35);

    private static readonly Brush
        ReceiptGoldSoftBrush =
            CreateFrozenBrush(
                248,
                240,
                219);

    private static readonly Brush
        ReceiptRuleBrush =
            CreateFrozenBrush(
                168,
                158,
                147);

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

        AddBrandHeader(
            document,
            request);

        AddHorizontalRule(
            document,
            topMargin:
                4,
            bottomMargin:
                6);

        AddReceiptTitle(
            document,
            request);

        AddReceiptIdentity(
            document,
            request);

        AddOptionalOrderInformation(
            document,
            request);

        AddHorizontalRule(
            document,
            topMargin:
                5,
            bottomMargin:
                5);

        AddLineItems(
            document,
            request);

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
                ReceiptTextBrush,

            Background =
                Brushes.White,

            LineStackingStrategy =
                LineStackingStrategy.BlockLineHeight,

            LineHeight =
                14,

            TextAlignment =
                TextAlignment.Left
        };
    }

    private static void AddBrandHeader(
        FlowDocument document,
        ReceiptRequest request)
    {
        var table =
            new Table
            {
                CellSpacing =
                    0,

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        1),

                Tag =
                    "Receipt.BrandHeader"
            };

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        0.28,
                        GridUnitType.Star)
            });

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        0.72,
                        GridUnitType.Star)
            });

        var rowGroup =
            new TableRowGroup();

        table.RowGroups.Add(
            rowGroup);

        var row =
            new TableRow();

        var monogramParagraph =
            new Paragraph
            {
                Margin =
                    new Thickness(
                        0),

                TextAlignment =
                    TextAlignment.Center,

                FontFamily =
                    BrandFontFamily,

                FontSize =
                    20,

                FontWeight =
                    FontWeights.Bold,

                Foreground =
                    ReceiptGoldBrush,

                LineHeight =
                    20
            };

        monogramParagraph.Inlines.Add(
            new Run(
                "P\nE"));

        var monogramCell =
            new TableCell(
                monogramParagraph)
            {
                Padding =
                    new Thickness(
                        5,
                        4,
                        5,
                        4),

                BorderBrush =
                    ReceiptGoldBrush,

                BorderThickness =
                    new Thickness(
                        1),

                Tag =
                    "Receipt.BrandMonogram"
            };

        var informationCell =
            new TableCell
            {
                Padding =
                    new Thickness(
                        10,
                        0,
                        0,
                        0),

                Tag =
                    "Receipt.BrandInformation"
            };

        informationCell.Blocks.Add(
            CreateParagraph(
                request.Store.Name,
                StoreNameFontSize,
                FontWeights.Bold,
                TextAlignment.Left,
                margin:
                    new Thickness(
                        0),
                fontFamily:
                    BrandFontFamily,
                foreground:
                    ReceiptTextBrush,
                tag:
                    "Receipt.StoreName"));

        informationCell.Blocks.Add(
            CreateParagraph(
                "BÁN LẺ • NHÀ HÀNG • CÀ PHÊ",
                7.5,
                FontWeights.SemiBold,
                TextAlignment.Left,
                margin:
                    new Thickness(
                        0,
                        1,
                        0,
                        2),
                foreground:
                    ReceiptGoldBrush,
                tag:
                    "Receipt.StoreType"));

        if (!string.IsNullOrWhiteSpace(
                request.Store.Address))
        {
            informationCell.Blocks.Add(
                CreateParagraph(
                    request.Store.Address,
                    SmallFontSize,
                    FontWeights.Normal,
                    TextAlignment.Left,
                    margin:
                        new Thickness(
                            0,
                            1,
                            0,
                            0),
                    foreground:
                        ReceiptTextBrush,
                    tag:
                        "Receipt.StoreAddress"));
        }

        if (!string.IsNullOrWhiteSpace(
                request.Store.Phone))
        {
            informationCell.Blocks.Add(
                CreateParagraph(
                    $"Điện thoại: {request.Store.Phone}",
                    SmallFontSize,
                    FontWeights.Normal,
                    TextAlignment.Left,
                    margin:
                        new Thickness(
                            0,
                            1,
                            0,
                            0),
                    foreground:
                        ReceiptTextBrush,
                    tag:
                        "Receipt.StorePhone"));
        }

        if (!string.IsNullOrWhiteSpace(
                request.Store.TaxCode))
        {
            informationCell.Blocks.Add(
                CreateParagraph(
                    $"Mã số thuế: {request.Store.TaxCode}",
                    SmallFontSize,
                    FontWeights.Normal,
                    TextAlignment.Left,
                    margin:
                        new Thickness(
                            0,
                            1,
                            0,
                            0),
                    foreground:
                        ReceiptTextBrush,
                    tag:
                        "Receipt.StoreTaxCode"));
        }

        row.Cells.Add(
            monogramCell);

        row.Cells.Add(
            informationCell);

        rowGroup.Rows.Add(
            row);

        document.Blocks.Add(
            table);
    }

    private static void AddReceiptTitle(
        FlowDocument document,
        ReceiptRequest request)
    {
        document.Blocks.Add(
            CreateParagraph(
                "HÓA ĐƠN BÁN HÀNG",
                ReceiptTitleFontSize,
                FontWeights.Bold,
                TextAlignment.Center,
                margin:
                    new Thickness(
                        0,
                        0,
                        0,
                        5),
                foreground:
                    ReceiptTextBrush,
                tag:
                    "Receipt.Title"));

        if (!request.IsReprint)
        {
            return;
        }

        document.Blocks.Add(
            CreateParagraph(
                $"BẢN IN LẠI LẦN " +
                $"{request.CopyNumber:N0}",
                BaseFontSize,
                FontWeights.Bold,
                TextAlignment.Center,
                margin:
                    new Thickness(
                        0,
                        0,
                        0,
                        5),
                foreground:
                    ReceiptGoldBrush,
                tag:
                    "Receipt.Reprint"));
    }

    private static void AddReceiptIdentity(
        FlowDocument document,
        ReceiptRequest request)
    {
        var table =
            CreateInformationTable(
                tag:
                    "Receipt.Identity");

        AddInformationRow(
            table,
            "Mã đơn:",
            request.OrderCode,
            "Receipt.OrderCode");

        AddInformationRow(
            table,
            "Thời gian:",
            FormatDateTime(
                request.PaidAtUtc),
            "Receipt.PaidAt");

        AddInformationRow(
            table,
            "Thu ngân:",
            request.CashierName,
            "Receipt.Cashier");

        AddInformationRow(
            table,
            "Thanh toán:",
            FormatPaymentMethod(
                request.PaymentMethod),
            "Receipt.PaymentMethod");

        document.Blocks.Add(
            table);
    }

    private static void AddOptionalOrderInformation(
        FlowDocument document,
        ReceiptRequest request)
    {
        var rows =
            new List<
                (string Label,
                 string Value,
                 string Tag)>();

        if (!string.IsNullOrWhiteSpace(
                request.CustomerName))
        {
            rows.Add(
                (
                    "Khách hàng:",
                    request.CustomerName,
                    "Receipt.Customer"
                ));
        }

        if (!string.IsNullOrWhiteSpace(
                request.RestaurantTableName))
        {
            rows.Add(
                (
                    "Bàn:",
                    request.RestaurantTableName,
                    "Receipt.Table"
                ));
        }

        if (!string.IsNullOrWhiteSpace(
                request.DiscountCode))
        {
            rows.Add(
                (
                    "Mã giảm giá:",
                    request.DiscountCode,
                    "Receipt.DiscountCode"
                ));
        }

        if (rows.Count == 0)
        {
            return;
        }

        var table =
            CreateInformationTable(
                tag:
                    "Receipt.OptionalInformation");

        foreach (var row in rows)
        {
            AddInformationRow(
                table,
                row.Label,
                row.Value,
                row.Tag);
        }

        document.Blocks.Add(
            table);
    }

    private static Table CreateInformationTable(
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
                        0.29,
                        GridUnitType.Star)
            });

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        0.71,
                        GridUnitType.Star)
            });

        table.RowGroups.Add(
            new TableRowGroup());

        return table;
    }

    private static void AddInformationRow(
        Table table,
        string label,
        string value,
        string tag)
    {
        var row =
            new TableRow
            {
                Tag =
                    tag
            };

        row.Cells.Add(
            CreateTextCell(
                label,
                TextAlignment.Left,
                FontWeights.Bold,
                SmallFontSize,
                padding:
                    new Thickness(
                        0,
                        1,
                        4,
                        1),
                foreground:
                    ReceiptTextBrush));

        row.Cells.Add(
            CreateTextCell(
                value,
                TextAlignment.Left,
                FontWeights.Normal,
                SmallFontSize,
                padding:
                    new Thickness(
                        0,
                        1,
                        0,
                        1),
                foreground:
                    ReceiptTextBrush));

        table.RowGroups[0]
            .Rows
            .Add(
                row);
    }

    private static void AddLineItems(
        FlowDocument document,
        ReceiptRequest request)
    {
        var table =
            new Table
            {
                CellSpacing =
                    0,

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        5),

                Tag =
                    "Receipt.LineItems"
            };

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        0.45,
                        GridUnitType.Star)
            });

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        0.13,
                        GridUnitType.Star)
            });

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        0.2,
                        GridUnitType.Star)
            });

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        0.22,
                        GridUnitType.Star)
            });

        var rowGroup =
            new TableRowGroup();

        table.RowGroups.Add(
            rowGroup);

        AddLineHeaderRow(
            rowGroup);

        foreach (var line in request.Lines)
        {
            AddReceiptLineRows(
                rowGroup,
                line);
        }

        document.Blocks.Add(
            table);
    }

    private static void AddLineHeaderRow(
        TableRowGroup rowGroup)
    {
        var row =
            new TableRow
            {
                Background =
                    ReceiptGoldSoftBrush,

                Tag =
                    "Receipt.LineHeader"
            };

        row.Cells.Add(
            CreateTextCell(
                "SẢN PHẨM",
                TextAlignment.Left,
                FontWeights.Bold,
                7.6,
                padding:
                    new Thickness(
                        3,
                        4,
                        2,
                        4),
                foreground:
                    ReceiptTextBrush,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    new Thickness(
                        0,
                        0,
                        0,
                        0.8)));

        row.Cells.Add(
            CreateTextCell(
                "SL",
                TextAlignment.Center,
                FontWeights.Bold,
                7.6,
                padding:
                    new Thickness(
                        1,
                        4,
                        1,
                        4),
                foreground:
                    ReceiptTextBrush,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    new Thickness(
                        0,
                        0,
                        0,
                        0.8)));

        row.Cells.Add(
            CreateTextCell(
                "ĐƠN GIÁ",
                TextAlignment.Right,
                FontWeights.Bold,
                7.6,
                padding:
                    new Thickness(
                        1,
                        4,
                        1,
                        4),
                foreground:
                    ReceiptTextBrush,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    new Thickness(
                        0,
                        0,
                        0,
                        0.8)));

        row.Cells.Add(
            CreateTextCell(
                "THÀNH TIỀN",
                TextAlignment.Right,
                FontWeights.Bold,
                7.6,
                padding:
                    new Thickness(
                        1,
                        4,
                        2,
                        4),
                foreground:
                    ReceiptTextBrush,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    new Thickness(
                        0,
                        0,
                        0,
                        0.8)));

        rowGroup.Rows.Add(
            row);
    }

    private static void AddReceiptLineRows(
        TableRowGroup rowGroup,
        ReceiptLineDto line)
    {
        var itemRow =
            new TableRow
            {
                Tag =
                    $"Receipt.Line.{line.OrderItemId}"
            };

        var bottomBorder =
            new Thickness(
                0,
                0,
                0,
                0.45);

        itemRow.Cells.Add(
            CreateTextCell(
                line.ProductName,
                TextAlignment.Left,
                FontWeights.SemiBold,
                BaseFontSize,
                padding:
                    new Thickness(
                        3,
                        5,
                        3,
                        5),
                foreground:
                    ReceiptTextBrush,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    bottomBorder));

        itemRow.Cells.Add(
            CreateTextCell(
                $"{line.Quantity.ToString(
                    "N0",
                    VietnameseCulture)} " +
                $"{line.UnitName}",
                TextAlignment.Center,
                FontWeights.Normal,
                SmallFontSize,
                padding:
                    new Thickness(
                        1,
                        5,
                        1,
                        5),
                foreground:
                    ReceiptTextBrush,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    bottomBorder));

        itemRow.Cells.Add(
            CreateTextCell(
                FormatMoney(
                    line.FinalUnitPrice),
                TextAlignment.Right,
                FontWeights.Normal,
                SmallFontSize,
                padding:
                    new Thickness(
                        1,
                        5,
                        1,
                        5),
                foreground:
                    ReceiptTextBrush,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    bottomBorder));

        itemRow.Cells.Add(
            CreateTextCell(
                FormatMoney(
                    line.NetAmount),
                TextAlignment.Right,
                FontWeights.SemiBold,
                SmallFontSize,
                padding:
                    new Thickness(
                        1,
                        5,
                        2,
                        5),
                foreground:
                    ReceiptTextBrush,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    bottomBorder));

        rowGroup.Rows.Add(
            itemRow);

        var details =
            BuildLineDetails(
                line);

        if (details.Count == 0)
        {
            return;
        }

        var detailCell =
            new TableCell
            {
                ColumnSpan =
                    4,

                Padding =
                    new Thickness(
                        10,
                        0,
                        3,
                        5),

                BorderBrush =
                    ReceiptRuleBrush,

                BorderThickness =
                    new Thickness(
                        0,
                        0,
                        0,
                        0.45),

                Tag =
                    $"Receipt.Line.{line.OrderItemId}.Details"
            };

        foreach (var detail in details)
        {
            detailCell.Blocks.Add(
                CreateParagraph(
                    detail.Text,
                    7.8,
                    detail.IsImportant
                        ? FontWeights.SemiBold
                        : FontWeights.Normal,
                    TextAlignment.Left,
                    margin:
                        new Thickness(
                            0,
                            1,
                            0,
                            0),
                    foreground:
                        detail.IsImportant
                            ? ReceiptTextBrush
                            : ReceiptMutedBrush,
                    fontStyle:
                        detail.IsItalic
                            ? FontStyles.Italic
                            : FontStyles.Normal));
        }

        var detailRow =
            new TableRow();

        detailRow.Cells.Add(
            detailCell);

        rowGroup.Rows.Add(
            detailRow);
    }

    private static IReadOnlyList<
        ReceiptLineDetail>
        BuildLineDetails(
            ReceiptLineDto line)
    {
        var details =
            new List<
                ReceiptLineDetail>();

        foreach (var modifier in line.Modifiers)
        {
            var text =
                $"+ {modifier.Name} " +
                $"× {modifier.Quantity.ToString(
                    "N0",
                    VietnameseCulture)}";

            if (modifier.AmountPerProductUnit > 0)
            {
                text +=
                    $"  (+{FormatMoney(
                        modifier.AmountPerProductUnit)}/sp)";
            }

            details.Add(
                new ReceiptLineDetail(
                    text,
                    IsImportant:
                        false,
                    IsItalic:
                        false));
        }

        if (line.LineDiscountAmount > 0)
        {
            details.Add(
                new ReceiptLineDetail(
                    $"Giảm dòng: -" +
                    $"{FormatMoney(
                        line.LineDiscountAmount)}",
                    IsImportant:
                        true,
                    IsItalic:
                        false));
        }

        if (!string.IsNullOrWhiteSpace(
                line.Notes))
        {
            details.Add(
                new ReceiptLineDetail(
                    $"Ghi chú: {line.Notes}",
                    IsImportant:
                        false,
                    IsItalic:
                        true));
        }

        return details;
    }

    private static void AddTotals(
        FlowDocument document,
        ReceiptRequest request)
    {
        var table =
            new Table
            {
                CellSpacing =
                    0,

                Margin =
                    new Thickness(
                        0,
                        2,
                        0,
                        0),

                Tag =
                    "Receipt.Totals"
            };

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        0.56,
                        GridUnitType.Star)
            });

        table.Columns.Add(
            new TableColumn
            {
                Width =
                    new GridLength(
                        0.44,
                        GridUnitType.Star)
            });

        table.RowGroups.Add(
            new TableRowGroup());

        AddTotalRow(
            table,
            "Tiền hàng",
            FormatMoney(
                request.Subtotal),
            FontWeights.Normal,
            BaseFontSize,
            ReceiptTextBrush,
            topBorder:
                false);

        AddTotalRow(
            table,
            "Giảm giá",
            request.DiscountAmount > 0
                ? $"-{FormatMoney(
                    request.DiscountAmount)}"
                : FormatMoney(
                    0),
            FontWeights.Normal,
            BaseFontSize,
            ReceiptTextBrush,
            topBorder:
                false);

        AddTotalRow(
            table,
            "TỔNG THANH TOÁN",
            FormatMoney(
                request.TotalAmount),
            FontWeights.Bold,
            TotalFontSize,
            ReceiptGoldBrush,
            topBorder:
                true);

        if (request.PaymentMethod ==
            PaymentMethod.Cash)
        {
            AddTotalRow(
                table,
                "Tiền khách đưa",
                FormatMoney(
                    request.CashReceived),
                FontWeights.Normal,
                BaseFontSize,
                ReceiptTextBrush,
                topBorder:
                    false);

            AddTotalRow(
                table,
                "Tiền thừa",
                FormatMoney(
                    request.ChangeAmount),
                FontWeights.SemiBold,
                BaseFontSize,
                ReceiptTextBrush,
                topBorder:
                    false);
        }

        document.Blocks.Add(
            table);
    }

    private static void AddTotalRow(
        Table table,
        string label,
        string amount,
        FontWeight fontWeight,
        double fontSize,
        Brush foreground,
        bool topBorder)
    {
        var borderThickness =
            topBorder
                ? new Thickness(
                    0,
                    0.9,
                    0,
                    0)
                : new Thickness(
                    0);

        var row =
            new TableRow();

        row.Cells.Add(
            CreateTextCell(
                label,
                TextAlignment.Left,
                fontWeight,
                fontSize,
                padding:
                    new Thickness(
                        3,
                        topBorder
                            ? 6
                            : 2,
                        3,
                        2),
                foreground:
                    foreground,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    borderThickness));

        row.Cells.Add(
            CreateTextCell(
                amount,
                TextAlignment.Right,
                fontWeight,
                fontSize,
                padding:
                    new Thickness(
                        3,
                        topBorder
                            ? 6
                            : 2,
                        3,
                        2),
                foreground:
                    foreground,
                borderBrush:
                    ReceiptRuleBrush,
                borderThickness:
                    borderThickness));

        table.RowGroups[0]
            .Rows
            .Add(
                row);
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

        AddHorizontalRule(
            document,
            topMargin:
                5,
            bottomMargin:
                3);

        var paragraph =
            new Paragraph
            {
                Margin =
                    new Thickness(
                        2,
                        0,
                        2,
                        0),

                FontSize =
                    SmallFontSize,

                Foreground =
                    ReceiptMutedBrush,

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
        AddHorizontalRule(
            document,
            topMargin:
                6,
            bottomMargin:
                5);

        var footerMessage =
            string.IsNullOrWhiteSpace(
                request.Store.FooterMessage)
                ? "Cảm ơn quý khách và hẹn gặp lại!"
                : request.Store.FooterMessage;

        document.Blocks.Add(
            CreateParagraph(
                footerMessage,
                9,
                FontWeights.SemiBold,
                TextAlignment.Center,
                margin:
                    new Thickness(
                        0,
                        0,
                        0,
                        3),
                foreground:
                    ReceiptTextBrush,
                tag:
                    "Receipt.Footer"));

        document.Blocks.Add(
            CreateParagraph(
                "Hóa đơn được tạo từ dữ liệu giao dịch đã chốt.",
                7.4,
                FontWeights.Normal,
                TextAlignment.Center,
                margin:
                    new Thickness(
                        0,
                        0,
                        0,
                        5),
                foreground:
                    ReceiptMutedBrush,
                tag:
                    "Receipt.SnapshotNotice"));

        document.Blocks.Add(
            CreateParagraph(
                "────────  ◇  ────────",
                8.5,
                FontWeights.Normal,
                TextAlignment.Center,
                margin:
                    new Thickness(
                        0),
                foreground:
                    ReceiptGoldBrush,
                tag:
                    "Receipt.FooterDecoration"));
    }

    private static void AddHorizontalRule(
        FlowDocument document,
        double topMargin,
        double bottomMargin)
    {
        var paragraph =
            new Paragraph
            {
                Margin =
                    new Thickness(
                        0,
                        topMargin,
                        0,
                        bottomMargin),

                BorderBrush =
                    ReceiptRuleBrush,

                BorderThickness =
                    new Thickness(
                        0,
                        0,
                        0,
                        0.8),

                FontSize =
                    1,

                LineHeight =
                    1,

                Tag =
                    "Receipt.Separator"
            };

        paragraph.Inlines.Add(
            new Run(
                " "));

        document.Blocks.Add(
            paragraph);
    }

    private static TableCell CreateTextCell(
        string text,
        TextAlignment textAlignment,
        FontWeight fontWeight,
        double fontSize,
        Thickness padding,
        Brush foreground,
        Brush? borderBrush = null,
        Thickness? borderThickness = null)
    {
        var paragraph =
            CreateParagraph(
                text,
                fontSize,
                fontWeight,
                textAlignment,
                margin:
                    new Thickness(
                        0),
                foreground:
                    foreground);

        return new TableCell(
            paragraph)
        {
            Padding =
                padding,

            BorderBrush =
                borderBrush,

            BorderThickness =
                borderThickness ??
                new Thickness(
                    0)
        };
    }

    private static Paragraph CreateParagraph(
        string text,
        double fontSize,
        FontWeight fontWeight,
        TextAlignment textAlignment,
        Thickness margin,
        FontFamily? fontFamily = null,
        Brush? foreground = null,
        FontStyle? fontStyle = null,
        string? tag = null)
    {
        return new Paragraph(
            new Run(
                text))
        {
            Margin =
                margin,

            TextAlignment =
                textAlignment,

            FontSize =
                fontSize,

            FontWeight =
                fontWeight,

            FontFamily =
                fontFamily ??
                ReceiptFontFamily,

            Foreground =
                foreground ??
                ReceiptTextBrush,

            FontStyle =
                fontStyle ??
                FontStyles.Normal,

            Tag =
                tag
        };
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

    private static Brush CreateFrozenBrush(
        byte red,
        byte green,
        byte blue)
    {
        var brush =
            new SolidColorBrush(
                Color.FromRgb(
                    red,
                    green,
                    blue));

        brush.Freeze();

        return brush;
    }

    private sealed record ReceiptLineDetail(
        string Text,
        bool IsImportant,
        bool IsItalic);
}