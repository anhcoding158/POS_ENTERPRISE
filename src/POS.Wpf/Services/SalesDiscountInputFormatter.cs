using System.Globalization;
using System.Text;

namespace POS.Wpf.Services;

public static class SalesDiscountInputFormatter
{
    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    public static string FormatVnd(long amount) =>
        amount.ToString("N0", VietnameseCulture);

    public static bool TryParseVndInput(string? text, out long amount)
    {
        amount = 0;
        if (!TrySanitizePastedVndText(text, out var digits) ||
            digits.Length == 0)
        {
            return false;
        }

        return long.TryParse(
            digits,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out amount);
    }

    public static bool TrySanitizePastedVndText(
        string? text,
        out string digits)
    {
        var builder = new StringBuilder(text?.Length ?? 0);
        foreach (var character in text ?? string.Empty)
        {
            if (IsAsciiDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (character is '.' or ',' or ' ' or '\t' or '\r' or '\n' or
                'đ' or 'Đ' or '₫')
            {
                continue;
            }

            digits = string.Empty;
            return false;
        }

        digits = builder.ToString();
        return true;
    }

    public static bool TryParsePercentage(
        string? text,
        out long basisPoints)
    {
        basisPoints = 0;
        var normalized = (text ?? string.Empty).Trim();
        if (normalized.EndsWith('%'))
            normalized = normalized[..^1].TrimEnd();

        if (normalized.Length == 0 ||
            normalized[0] is '+' or '-')
        {
            return false;
        }

        var separatorIndex = -1;
        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];
            if (IsAsciiDigit(character))
                continue;
            if (character is not (',' or '.') || separatorIndex >= 0)
                return false;
            separatorIndex = index;
        }

        var wholeText = separatorIndex < 0
            ? normalized
            : normalized[..separatorIndex];
        var fractionText = separatorIndex < 0
            ? string.Empty
            : normalized[(separatorIndex + 1)..];

        if (wholeText.Length == 0 ||
            fractionText.Length > 2 ||
            (separatorIndex >= 0 && fractionText.Length == 0) ||
            !int.TryParse(
                wholeText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var whole) ||
            whole > 100)
        {
            return false;
        }

        var fraction = fractionText.Length switch
        {
            0 => 0,
            1 => fractionText[0] - '0',
            _ => ((fractionText[0] - '0') * 10) + (fractionText[1] - '0')
        };
        if (fractionText.Length == 1)
            fraction *= 10;
        if (whole == 100 && fraction != 0)
            return false;

        basisPoints = checked((whole * 100L) + fraction);
        return basisPoints > 0;
    }

    public static int CountDigitsToRight(string text, int caretIndex)
    {
        var count = 0;
        for (var index = Math.Clamp(caretIndex, 0, text.Length);
             index < text.Length;
             index++)
        {
            if (IsAsciiDigit(text[index]))
                count++;
        }

        return count;
    }

    public static int FindCaretIndex(string formattedText, int digitsToRight)
    {
        if (digitsToRight <= 0)
            return formattedText.Length;

        var found = 0;
        for (var index = formattedText.Length - 1; index >= 0; index--)
        {
            if (!IsAsciiDigit(formattedText[index]))
                continue;
            if (++found == digitsToRight)
                return index;
        }

        return 0;
    }

    public static bool IsAsciiDigit(char character) =>
        character is >= '0' and <= '9';
}
