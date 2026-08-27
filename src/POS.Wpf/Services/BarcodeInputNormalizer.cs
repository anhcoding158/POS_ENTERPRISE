namespace POS.Wpf.Services;

/// <summary>
/// Normalizes input at the same boundary used by Sales barcode lookup.
/// </summary>
public static class BarcodeInputNormalizer
{
    public static string? Normalize(string? input)
    {
        var normalized = input?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    public static string ForDisplay(string? input)
    {
        var normalized = Normalize(input);
        if (normalized is null)
            return string.Empty;

        return normalized.Length <= 48
            ? normalized
            : normalized[..48] + "…";
    }
}
