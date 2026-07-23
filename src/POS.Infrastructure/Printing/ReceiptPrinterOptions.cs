namespace POS.Infrastructure.Printing;

/// <summary>
/// Cấu hình máy in hóa đơn.
///
/// Cấu hình được bind từ section Hardware
/// trong appsettings.json.
///
/// Checkpoint hiện tại chỉ hỗ trợ giấy K80.
/// </summary>
public sealed class ReceiptPrinterOptions
{
    public const string SectionName =
        "Hardware";

    public const string SupportedPaperSize =
        "K80";

    private const int MaximumPrinterNameLength =
        260;

    /// <summary>
    /// Tên máy in đúng như Windows hiển thị
    /// trong Printers &amp; scanners.
    /// </summary>
    public string PrinterName
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Khổ giấy receipt được ứng dụng hỗ trợ.
    /// </summary>
    public string PaperSize
    {
        get;
        set;
    } = SupportedPaperSize;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(
                PrinterName))
        {
            throw new InvalidOperationException(
                "Hardware:PrinterName không được để trống.");
        }

        var normalizedPrinterName =
            PrinterName.Trim();

        if (normalizedPrinterName.Length >
            MaximumPrinterNameLength)
        {
            throw new InvalidOperationException(
                $"Hardware:PrinterName không được vượt quá " +
                $"{MaximumPrinterNameLength} ký tự.");
        }

        if (string.IsNullOrWhiteSpace(
                PaperSize))
        {
            throw new InvalidOperationException(
                "Hardware:PaperSize không được để trống.");
        }

        if (!string.Equals(
                PaperSize.Trim(),
                SupportedPaperSize,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Khổ giấy '{PaperSize}' chưa được hỗ trợ. " +
                $"Phiên bản hiện tại chỉ hỗ trợ " +
                $"{SupportedPaperSize}.");
        }
    }

    public string GetNormalizedPrinterName()
    {
        Validate();

        return PrinterName.Trim();
    }

    public string GetNormalizedPaperSize()
    {
        Validate();

        return PaperSize
            .Trim()
            .ToUpperInvariant();
    }
}