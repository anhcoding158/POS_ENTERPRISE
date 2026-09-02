using System.Globalization;

namespace POS.Wpf.Services;

public enum NumericEditOperation
{
    Insert,
    Backspace,
    Delete,
    Clear
}

public sealed record NumericEditResult(
    string Digits,
    bool IsNegative,
    string DisplayText,
    int CaretIndex,
    bool IsEmpty,
    bool IsValid);

/// <summary>
/// Applies a logical edit to the digit model behind a grouped numeric display.
/// It deliberately does not validate transient display text such as "1.5000".
/// </summary>
public static class NumericInputEditEngine
{
    public static NumericEditResult Insert(
        string displayText,
        int caretIndex,
        int selectionStart,
        int selectionLength,
        string insertedText,
        NumericInputMode mode,
        bool allowNegative = false,
        bool? negativeOverride = null) =>
        Apply(
            displayText,
            caretIndex,
            selectionStart,
            selectionLength,
            NumericEditOperation.Insert,
            insertedText,
            mode,
            allowNegative,
            negativeOverride);

    public static NumericEditResult Backspace(
        string displayText,
        int caretIndex,
        int selectionStart,
        int selectionLength,
        NumericInputMode mode,
        bool allowNegative = false) =>
        Apply(
            displayText,
            caretIndex,
            selectionStart,
            selectionLength,
            NumericEditOperation.Backspace,
            string.Empty,
            mode,
            allowNegative,
            null);

    public static NumericEditResult Delete(
        string displayText,
        int caretIndex,
        int selectionStart,
        int selectionLength,
        NumericInputMode mode,
        bool allowNegative = false) =>
        Apply(
            displayText,
            caretIndex,
            selectionStart,
            selectionLength,
            NumericEditOperation.Delete,
            string.Empty,
            mode,
            allowNegative,
            null);

    public static NumericEditResult Clear(
        string displayText,
        int caretIndex,
        int selectionStart,
        int selectionLength,
        NumericInputMode mode,
        bool allowNegative = false) =>
        Apply(
            displayText,
            caretIndex,
            selectionStart,
            selectionLength,
            NumericEditOperation.Clear,
            string.Empty,
            mode,
            allowNegative,
            null);

    private static NumericEditResult Apply(
        string displayText,
        int caretIndex,
        int selectionStart,
        int selectionLength,
        NumericEditOperation operation,
        string insertedText,
        NumericInputMode mode,
        bool allowNegative,
        bool? negativeOverride)
    {
        var safeText = displayText ?? string.Empty;
        var safeCaret = Math.Clamp(caretIndex, 0, safeText.Length);
        var safeSelectionStart = Math.Clamp(selectionStart, 0, safeText.Length);
        var safeSelectionLength = Math.Clamp(
            selectionLength,
            0,
            safeText.Length - safeSelectionStart);
        var isNegative = mode == NumericInputMode.SignedInteger &&
            safeText.TrimStart().StartsWith('-');

        var digits = ExtractDigits(safeText);
        var startDigit = CountDigitsBefore(safeText, safeSelectionStart);
        var endDigit = CountDigitsBefore(
            safeText,
            safeSelectionStart + safeSelectionLength);
        var caretDigit = CountDigitsBefore(safeText, safeCaret);

        if (operation == NumericEditOperation.Clear)
        {
            digits = string.Empty;
            caretDigit = 0;
            startDigit = 0;
            endDigit = ExtractDigits(safeText).Length;
        }
        else if (safeSelectionLength > 0)
        {
            digits = digits.Remove(startDigit, endDigit - startDigit);
            caretDigit = startDigit;
        }
        else if (operation == NumericEditOperation.Backspace && caretDigit > 0)
        {
            digits = digits.Remove(caretDigit - 1, 1);
            caretDigit--;
        }
        else if (operation == NumericEditOperation.Delete && caretDigit < digits.Length)
        {
            digits = digits.Remove(caretDigit, 1);
        }

        if (operation == NumericEditOperation.Insert)
        {
            var digitsToInsert = new string(
                (insertedText ?? string.Empty).Where(IsAsciiDigit).ToArray());
            digits = digits.Insert(caretDigit, digitsToInsert);
            caretDigit += digitsToInsert.Length;
        }

        if (negativeOverride.HasValue)
        {
            isNegative = negativeOverride.Value;
        }

        var leadingZeroCount = CountRemovableLeadingZeros(digits);
        if (leadingZeroCount > 0)
        {
            digits = digits[leadingZeroCount..];
            caretDigit = Math.Max(0, caretDigit - leadingZeroCount);
        }

        if (digits.Length == 0)
        {
            return new NumericEditResult(
                string.Empty,
                false,
                string.Empty,
                0,
                true,
                true);
        }

        if (!long.TryParse(
                digits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return new NumericEditResult(
                digits,
                false,
                safeText,
                safeCaret,
                false,
                false);
        }

        if (mode == NumericInputMode.NonNegativeInteger ||
            mode == NumericInputMode.MoneyVnd)
        {
            value = Math.Max(0, value);
        }

        var signedValue = isNegative ? -value : value;
        var formatted = NumericInputFormatter.Format(signedValue, mode);
        return new NumericEditResult(
            digits,
            isNegative,
            formatted,
            FindCaretAfterDigits(formatted, caretDigit),
            false,
            true);
    }

    private static string ExtractDigits(string text) =>
        new(text.Where(IsAsciiDigit).ToArray());

    private static int CountDigitsBefore(string text, int index)
    {
        var count = 0;
        for (var i = 0; i < Math.Clamp(index, 0, text.Length); i++)
        {
            if (IsAsciiDigit(text[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountRemovableLeadingZeros(string digits)
    {
        var count = 0;
        while (count < digits.Length - 1 && digits[count] == '0')
        {
            count++;
        }

        return count;
    }

    private static int FindCaretAfterDigits(string text, int digitCount)
    {
        if (digitCount <= 0)
        {
            return 0;
        }

        var found = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (!IsAsciiDigit(text[index]))
            {
                continue;
            }

            found++;
            if (found == digitCount)
            {
                return index + 1;
            }
        }

        return text.Length;
    }

    private static bool IsAsciiDigit(char character) =>
        character is >= '0' and <= '9';
}
