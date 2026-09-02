using System.Globalization;
using System.Text;

namespace POS.Wpf.Services;

public enum NumericInputMode
{
    MoneyVnd,
    NonNegativeInteger,
    SignedInteger
}

/// <summary>
/// Strict parser and canonical formatter for integer VND and non-negative counts.
/// The UI behavior owns draft/caret handling; this type owns only the value contract.
/// </summary>
public static class NumericInputFormatter
{
    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    public static string Format(long value, NumericInputMode mode)
    {
        if (value < 0 && mode != NumericInputMode.SignedInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return value.ToString("N0", VietnameseCulture);
    }

    public static bool TryParse(
        string? text,
        NumericInputMode mode,
        out long value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var candidate = text.Trim();
        var isNegative = candidate.StartsWith('-');

        if (isNegative)
        {
            if (mode != NumericInputMode.SignedInteger || candidate.Length == 1)
            {
                return false;
            }

            candidate = candidate[1..];
        }

        if (!TryGetDigits(candidate, out var digits))
        {
            return false;
        }

        if (!long.TryParse(
                digits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value))
        {
            value = 0;
            return false;
        }

        if (isNegative)
        {
            value = -value;
        }

        return mode switch
        {
            NumericInputMode.MoneyVnd => value >= 0,
            NumericInputMode.NonNegativeInteger => value >= 0,
            NumericInputMode.SignedInteger => true,
            _ => false
        };
    }

    private static bool TryGetDigits(
        string candidate,
        out string digits)
    {
        digits = string.Empty;

        if (candidate.Length == 0)
        {
            return false;
        }

        var separator = '\0';
        var separatorCount = 0;

        foreach (var character in candidate)
        {
            if (character is '.' or ',')
            {
                if (separator == '\0')
                {
                    separator = character;
                }
                else if (separator != character)
                {
                    return false;
                }

                separatorCount++;
                continue;
            }

            if (IsSpaceSeparator(character))
            {
                if (separator == '\0')
                {
                    separator = ' ';
                }
                else if (separator != ' ')
                {
                    return false;
                }

                separatorCount++;
                continue;
            }

            if (!IsAsciiDigit(character))
            {
                return false;
            }
        }

        if (separatorCount == 0)
        {
            if (!AllDigits(candidate))
            {
                return false;
            }

            digits = candidate;
            return true;
        }

        var groups = SplitGroups(candidate, separator);

        if (groups.Length < 2 ||
            groups[0].Length is < 1 or > 3 ||
            groups.Skip(1).Any(group => group.Length != 3) ||
            groups.Any(group => !AllDigits(group)))
        {
            return false;
        }

        var builder = new StringBuilder(candidate.Length - separatorCount);
        foreach (var group in groups)
        {
            builder.Append(group);
        }

        digits = builder.ToString();
        return digits.Length > 0;
    }

    private static string[] SplitGroups(
        string value,
        char separator)
    {
        if (separator == ' ')
        {
            return value
                .Split(
                    [' ', '\u00A0', '\u202F'],
                    StringSplitOptions.None);
        }

        return value.Split(separator, StringSplitOptions.None);
    }

    private static bool AllDigits(string value)
    {
        return value.Length > 0 && value.All(IsAsciiDigit);
    }

    private static bool IsAsciiDigit(char value) =>
        value is >= '0' and <= '9';

    private static bool IsSpaceSeparator(char value) =>
        value is ' ' or '\u00A0' or '\u202F';
}
