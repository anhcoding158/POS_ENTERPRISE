using System.Globalization;
using POS.Domain.Enums;

namespace POS.Wpf.Services;

public static class SalesDiscountPresentationFormatter
{
    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    public static string FormatMoney(long amount) =>
        $"{amount.ToString("N0", VietnameseCulture)} đ";

    public static string FormatPercentage(long basisPoints)
    {
        var whole = basisPoints / 100;
        var fraction = basisPoints % 100;
        return fraction switch
        {
            0 => $"{whole.ToString(CultureInfo.InvariantCulture)}%",
            _ when fraction % 10 == 0 =>
                $"{whole.ToString(CultureInfo.InvariantCulture)},{fraction / 10}%",
            _ => $"{whole.ToString(CultureInfo.InvariantCulture)},{fraction:00}%"
        };
    }

    public static string FormatRequestedValue(
        SalesDiscountType type,
        long requestedValue) =>
        type switch
        {
            SalesDiscountType.FixedAmount => FormatMoney(requestedValue),
            SalesDiscountType.Percentage => FormatPercentage(requestedValue),
            _ => string.Empty
        };

    public static string FormatLocalTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", VietnameseCulture);

    public static string FormatLocalTime(DateTimeOffset? value) =>
        value.HasValue ? FormatLocalTime(value.Value) : "—";
}
