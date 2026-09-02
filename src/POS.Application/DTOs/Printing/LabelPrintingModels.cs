using System.Collections.ObjectModel;
using System.Globalization;

namespace POS.Application.DTOs.Printing;

public enum LabelTemplateKind
{
    Standard50x30 = 1,
    Standard60x40 = 2,
    Custom = 3
}

/// <summary>
/// Một media page chứa đúng một tem. Các kích thước đều là millimet.
/// </summary>
public sealed record LabelTemplate(
    LabelTemplateKind Kind,
    string DisplayName,
    double WidthMm,
    double HeightMm,
    double OffsetXmm = 0,
    double OffsetYmm = 0,
    double InnerMarginMm = 2)
{
    public const double MinimumSizeMm = 10;
    public const double MaximumSizeMm = 300;
    public const double MaximumOffsetMm = 100;
    public const double MaximumInnerMarginMm = 20;

    public static LabelTemplate Standard50x30 { get; } =
        new(LabelTemplateKind.Standard50x30, "50 × 30 mm", 50, 30);

    public static LabelTemplate Standard60x40 { get; } =
        new(LabelTemplateKind.Standard60x40, "60 × 40 mm", 60, 40);

    public static IReadOnlyList<LabelTemplate> Presets { get; } =
        new ReadOnlyCollection<LabelTemplate>([Standard50x30, Standard60x40]);

    public bool IsValid(out string error)
    {
        if (!double.IsFinite(WidthMm) || WidthMm is < MinimumSizeMm or > MaximumSizeMm)
        {
            error = $"Chiều rộng tem phải từ {MinimumSizeMm:N0} đến {MaximumSizeMm:N0} mm.";
            return false;
        }
        if (!double.IsFinite(HeightMm) || HeightMm is < MinimumSizeMm or > MaximumSizeMm)
        {
            error = $"Chiều cao tem phải từ {MinimumSizeMm:N0} đến {MaximumSizeMm:N0} mm.";
            return false;
        }
        if (!double.IsFinite(OffsetXmm) || Math.Abs(OffsetXmm) > MaximumOffsetMm ||
            !double.IsFinite(OffsetYmm) || Math.Abs(OffsetYmm) > MaximumOffsetMm)
        {
            error = $"Căn chỉnh X/Y chỉ được trong khoảng ±{MaximumOffsetMm:N0} mm.";
            return false;
        }
        if (!double.IsFinite(InnerMarginMm) || InnerMarginMm < 0 || InnerMarginMm > MaximumInnerMarginMm)
        {
            error = $"Lề trong phải từ 0 đến {MaximumInnerMarginMm:N0} mm.";
            return false;
        }
        if (WidthMm <= InnerMarginMm * 2 || HeightMm <= InnerMarginMm * 2)
        {
            error = "Lề trong phải nhỏ hơn kích thước tem.";
            return false;
        }
        if (Math.Abs(OffsetXmm) > InnerMarginMm || Math.Abs(OffsetYmm) > InnerMarginMm)
        {
            error = "Căn chỉnh X/Y không được đẩy nội dung ra ngoài vùng an toàn của tem.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}

public sealed record LabelProductSnapshot(
    int ProductId,
    string ProductCode,
    string ProductName,
    long SalePrice,
    string? Barcode,
    bool IsActive,
    int DefaultQuantity = 1)
{
    public bool HasValidBarcode => LabelBarcodeValidator.IsValid(Barcode);

    public string BarcodeError =>
        LabelBarcodeValidator.GetError(Barcode);
}

public sealed record LabelJobSnapshot(
    DateTimeOffset CreatedAtUtc,
    string PrintDateText,
    LabelTemplate Template,
    IReadOnlyList<LabelProductSnapshot> Products)
{
    public int TotalLabels => Products.Sum(x => x.DefaultQuantity);

    public static LabelJobSnapshot Create(
        DateTimeOffset createdAtUtc,
        IEnumerable<LabelProductSnapshot> products,
        LabelTemplate template)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(template);
        var items = products.DistinctBy(x => x.ProductId).ToArray();
        if (items.Length == 0)
        {
            throw new ArgumentException("Phải có ít nhất một sản phẩm.", nameof(products));
        }
        if (!template.IsValid(out var templateError))
        {
            throw new ArgumentException(templateError, nameof(template));
        }
        foreach (var item in items)
        {
            if (item.ProductId <= 0 || string.IsNullOrWhiteSpace(item.ProductCode) ||
                string.IsNullOrWhiteSpace(item.ProductName) || item.SalePrice < 0 ||
                item.DefaultQuantity <= 0 || !item.HasValidBarcode)
            {
                throw new ArgumentException("Snapshot sản phẩm không hợp lệ.", nameof(products));
            }
        }
        if (createdAtUtc == default)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(createdAtUtc, default);
        }
        return new LabelJobSnapshot(
            createdAtUtc.ToUniversalTime(),
            createdAtUtc.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN")),
            template,
            new ReadOnlyCollection<LabelProductSnapshot>(items));
    }
}

public sealed record LabelPrinterInfo(
    string Name,
    bool IsAvailable,
    bool SupportsCustomMedia = true,
    double? MaximumWidthMm = null,
    double? MaximumHeightMm = null);

public sealed record LabelPrintSettings
{
    public LabelTemplateKind TemplateKind { get; init; } = LabelTemplateKind.Standard50x30;
    public double WidthMm { get; init; } = 50;
    public double HeightMm { get; init; } = 30;
    public double OffsetXmm { get; init; }
    public double OffsetYmm { get; init; }
    public double InnerMarginMm { get; init; } = 2;
    public string? PrinterName { get; init; }
}

public sealed record LabelPrintRequest(
    LabelJobSnapshot Job,
    string PrinterName,
    bool IsTestPrint,
    int RequestedLabelCount)
{
    public int EffectiveLabelCount =>
        IsTestPrint ? 1 : Job.TotalLabels;
}

public static class LabelBarcodeValidator
{
    public const int MaximumLength = 80;

    public static bool IsValid(string? value) =>
        string.IsNullOrWhiteSpace(GetErrorOrNull(value));

    public static string GetError(string? value) =>
        GetErrorOrNull(value) ?? string.Empty;

    private static string? GetErrorOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Sản phẩm chưa có Barcode; ProductCode không được tự dùng thay thế.";
        }
        var normalized = value.Trim();
        if (normalized.Length > MaximumLength)
        {
            return $"Barcode không được vượt quá {MaximumLength} ký tự.";
        }
        if (normalized.Any(c => c < 32 || c > 126))
        {
            return "Barcode phải là chuỗi ASCII in được để mã hóa Code 128.";
        }
        return null;
    }
}
