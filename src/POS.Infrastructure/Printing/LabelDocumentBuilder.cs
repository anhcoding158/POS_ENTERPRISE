using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using POS.Application.DTOs.Printing;
using POS.Application.Printing;

namespace POS.Infrastructure.Printing;

/// <summary>
/// Nguồn sự thật duy nhất cho bố cục tem. Preview dùng DrawingVisual này;
/// paginator in cũng gọi đúng renderer này, nên không có layout raster thứ hai.
/// </summary>
public static class LabelDocumentBuilder
{
    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    private static readonly Brush Ink = Freeze(38, 31, 28);
    private static readonly Brush Muted = Freeze(108, 94, 83);
    private static readonly Brush Burgundy = Freeze(145, 18, 29);
    private static readonly Brush Gold = Freeze(184, 137, 47);

    public static LabelDocumentPaginator Build(
        LabelJobSnapshot job,
        bool isTestPrint = false)
    {
        ArgumentNullException.ThrowIfNull(job);
        return new LabelDocumentPaginator(job, isTestPrint);
    }

    public static DrawingVisual Render(
        LabelProductSnapshot product,
        LabelTemplate template,
        string printDateText)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(template);
        if (!template.IsValid(out var error))
        {
            throw new ArgumentException(error, nameof(template));
        }
        if (!product.HasValidBarcode)
        {
            throw new ArgumentException(product.BarcodeError, nameof(product));
        }

        var width = MillimetreConverter.ToDip(template.WidthMm);
        var height = MillimetreConverter.ToDip(template.HeightMm);
        var offsetX = MillimetreConverter.ToDip(template.OffsetXmm);
        var offsetY = MillimetreConverter.ToDip(template.OffsetYmm);
        var margin = MillimetreConverter.ToDip(template.InnerMarginMm);
        var visual = new DrawingVisual();

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, new Pen(Gold, 1), new Rect(0, 0, width, height));

            var content = new Rect(
                margin + offsetX,
                margin + offsetY,
                Math.Max(1, width - margin * 2),
                Math.Max(1, height - margin * 2));

            DrawText(dc, product.ProductName, content.X, content.Y, content.Width, 9.5,
                FontWeights.SemiBold, Ink, 2);
            DrawText(dc, FormatMoney(product.SalePrice), content.X, content.Y + 15, content.Width, 14,
                FontWeights.Bold, Burgundy, 1);

            var barcodeY = content.Y + 34;
            var barcodeHeight = Math.Max(20, Math.Min(42, content.Height * .38));
            DrawBarcode(dc, product.Barcode!.Trim(), content.X, barcodeY, content.Width, barcodeHeight);
            DrawText(dc, product.Barcode.Trim(), content.X, barcodeY + barcodeHeight + 2,
                content.Width, 7.5, FontWeights.Normal, Muted, 1, TextAlignment.Center);

            var footerY = height - margin - 13;
            DrawText(dc, $"Mã: {product.ProductCode}", content.X, footerY, content.Width * .54, 7.5,
                FontWeights.SemiBold, Ink, 1);
            DrawText(dc, printDateText, content.X + content.Width * .54, footerY,
                content.Width * .46, 7.5, FontWeights.Normal, Muted, 1, TextAlignment.Right);
        }

        return visual;
    }

    private static void DrawBarcode(
        DrawingContext dc,
        string value,
        double x,
        double y,
        double width,
        double height)
    {
        var matrix = LabelBarcodeEncoder.Encode(
            value,
            Math.Max(1, (int)Math.Round(width)),
            Math.Max(1, (int)Math.Round(height)));

        // Code 128 requires a quiet zone. The renderer owns it and never paints there.
        var quietModules = Math.Max(10, matrix.Width / 20);
        var moduleWidth = width / (matrix.Width + quietModules * 2d);
        var barWidth = moduleWidth * matrix.Width;
        var left = x + (width - barWidth) / 2d;

        for (var column = 0; column < matrix.Width; column++)
        {
            if (!matrix[column, 0])
            {
                continue;
            }

            dc.DrawRectangle(Brushes.Black, null,
                new Rect(left + column * moduleWidth, y, moduleWidth + .05, height));
        }
    }

    private static void DrawText(
        DrawingContext dc,
        string text,
        double x,
        double y,
        double width,
        double fontSize,
        FontWeight weight,
        Brush brush,
        int maxLines,
        TextAlignment alignment = TextAlignment.Left)
    {
        var formatted = new FormattedText(
            text,
            VietnameseCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            fontSize,
            brush,
            1d)
        {
            MaxTextWidth = Math.Max(1, width),
            MaxTextHeight = Math.Max(1, fontSize * (maxLines + .25)),
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = alignment
        };
        dc.DrawText(formatted, new Point(x, y));
    }

    private static string FormatMoney(long amount) =>
        $"{amount.ToString("N0", VietnameseCulture)} ₫";

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed class LabelDocumentPaginator : DocumentPaginator
{
    private readonly LabelJobSnapshot _job;
    private readonly List<LabelProductSnapshot> _pages;
    private Size _pageSize;

    public LabelDocumentPaginator(LabelJobSnapshot job, bool isTestPrint)
    {
        _job = job ?? throw new ArgumentNullException(nameof(job));
        _pages = ExpandPages(job, isTestPrint);
        _pageSize = new Size(
            MillimetreConverter.ToDip(_job.Template.WidthMm),
            MillimetreConverter.ToDip(_job.Template.HeightMm));
    }

    public override bool IsPageCountValid => true;
    public override int PageCount => _pages.Count;
    public override Size PageSize
    {
        get => _pageSize;
        set => _pageSize = value;
    }
    public override IDocumentPaginatorSource Source => null!;

    public override DocumentPage GetPage(int pageNumber)
    {
        if (pageNumber < 0 || pageNumber >= PageCount)
        {
            return DocumentPage.Missing;
        }
        var visual = LabelDocumentBuilder.Render(
            _pages[pageNumber], _job.Template, _job.PrintDateText);
        return new DocumentPage(visual, PageSize, Rect.Empty, new Rect(PageSize));
    }

    public LabelProductSnapshot GetProduct(int pageNumber) => _pages[pageNumber];

    private static List<LabelProductSnapshot> ExpandPages(
        LabelJobSnapshot job,
        bool isTestPrint)
    {
        var result = new List<LabelProductSnapshot>();
        foreach (var product in job.Products)
        {
            var count = isTestPrint && result.Count == 0 ? 1 : product.DefaultQuantity;
            for (var i = 0; i < count; i++)
            {
                result.Add(product);
                if (isTestPrint)
                {
                    return result;
                }
            }
        }
        return result;
    }
}
