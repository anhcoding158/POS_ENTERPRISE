using System.Media;
using POS.Domain.Common;
using POS.Domain.Enums;
using POS.Domain.Services;
using POS.Wpf.Services;

namespace POS.Wpf.Views;

public partial class SalesDiscountWindow : global::System.Windows.Window
{
    private readonly long _subtotal;
    private bool _isFormattingValue;
    private string _lastValidFixedText = string.Empty;

    public SalesDiscountWindow(
        long subtotal,
        SalesDiscountType initialType = SalesDiscountType.None,
        long initialValue = 0,
        string? initialReason = null)
    {
        _subtotal = subtotal;
        InitializeComponent();
        SubtotalText.Text = $"{SalesDiscountInputFormatter.FormatVnd(subtotal)} đ";
        if (initialType != SalesDiscountType.None)
        {
            TypeBox.SelectedIndex =
                initialType == SalesDiscountType.Percentage ? 1 : 0;
            ReasonBox.Text = initialReason ?? string.Empty;
            var valueText = initialType == SalesDiscountType.FixedAmount
                ? SalesDiscountInputFormatter.FormatVnd(initialValue)
                : FormatPercentage(initialValue);
            SetValueText(valueText, valueText.Length);
        }
        UpdateSummary();
    }

    public SalesDiscountType DiscountType { get; private set; }
    public long DiscountValue { get; private set; }
    public string DiscountReason { get; private set; } = string.Empty;

    private bool IsPercentage => TypeBox?.SelectedIndex == 1;

    private static string FormatPercentage(long basisPoints)
    {
        var whole = basisPoints / 100;
        var fraction = basisPoints % 100;
        return fraction switch
        {
            0 => whole.ToString(global::System.Globalization.CultureInfo.InvariantCulture),
            _ when fraction % 10 == 0 =>
                $"{whole.ToString(global::System.Globalization.CultureInfo.InvariantCulture)},{fraction / 10}",
            _ => $"{whole.ToString(global::System.Globalization.CultureInfo.InvariantCulture)},{fraction:00}"
        };
    }

    private void OnTypeChanged(
        object sender,
        global::System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ValueBox is null)
            return;
        ValueSuffix.Text = IsPercentage ? "%" : "đ";
        SetValueText(string.Empty, 0);
        UpdateSummary();
    }

    private void OnInputChanged(
        object sender,
        global::System.Windows.RoutedEventArgs e) =>
        UpdateSummary();

    private void OnValuePreviewTextInput(
        object sender,
        global::System.Windows.Input.TextCompositionEventArgs e)
    {
        foreach (var character in e.Text)
        {
            var accepted = SalesDiscountInputFormatter.IsAsciiDigit(character) ||
                (IsPercentage && character is ',' or '.');
            if (accepted)
                continue;
            e.Handled = true;
            SystemSounds.Beep.Play();
            return;
        }
    }

    private void OnValuePasting(
        object sender,
        global::System.Windows.DataObjectPastingEventArgs e)
    {
        if (sender is not global::System.Windows.Controls.TextBox textBox ||
            !e.SourceDataObject.GetDataPresent(
                global::System.Windows.DataFormats.UnicodeText,
                true) ||
            e.SourceDataObject.GetData(
                global::System.Windows.DataFormats.UnicodeText,
                true) is not string pastedText)
        {
            RejectPaste(e);
            return;
        }

        var start = Math.Clamp(textBox.SelectionStart, 0, textBox.Text.Length);
        var length = Math.Clamp(
            textBox.SelectionLength,
            0,
            textBox.Text.Length - start);

        if (IsPercentage)
        {
            var candidate = textBox.Text.Remove(start, length)
                .Insert(start, pastedText.Trim().TrimEnd('%').TrimEnd());
            if (!SalesDiscountInputFormatter.TryParsePercentage(
                    candidate,
                    out _))
            {
                RejectPaste(e);
                return;
            }
            e.CancelCommand();
            SetValueText(candidate, start + pastedText.Trim().TrimEnd('%').TrimEnd().Length);
            return;
        }

        if (!SalesDiscountInputFormatter.TrySanitizePastedVndText(
                pastedText,
                out var pastedDigits) ||
            pastedDigits.Length == 0)
        {
            RejectPaste(e);
            return;
        }

        var candidateText = textBox.Text.Remove(start, length)
            .Insert(start, pastedDigits);
        if (!SalesDiscountInputFormatter.TryParseVndInput(
                candidateText,
                out var amount))
        {
            RejectPaste(e);
            return;
        }

        var digitsToRight = SalesDiscountInputFormatter.CountDigitsToRight(
            candidateText,
            start + pastedDigits.Length);
        var formatted = SalesDiscountInputFormatter.FormatVnd(amount);
        e.CancelCommand();
        SetValueText(
            formatted,
            SalesDiscountInputFormatter.FindCaretIndex(
                formatted,
                digitsToRight));
    }

    private static void RejectPaste(
        global::System.Windows.DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        SystemSounds.Beep.Play();
    }

    private void OnValueTextChanged(
        object sender,
        global::System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isFormattingValue ||
            sender is not global::System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        if (IsPercentage)
        {
            UpdateSummary();
            return;
        }

        var original = textBox.Text ?? string.Empty;
        var digitsToRight = SalesDiscountInputFormatter.CountDigitsToRight(
            original,
            textBox.CaretIndex);
        if (original.Length == 0)
        {
            _lastValidFixedText = string.Empty;
            UpdateSummary();
            return;
        }

        if (!SalesDiscountInputFormatter.TryParseVndInput(
                original,
                out var amount))
        {
            SystemSounds.Beep.Play();
            SetValueText(_lastValidFixedText, _lastValidFixedText.Length);
            return;
        }

        var formatted = SalesDiscountInputFormatter.FormatVnd(amount);
        _lastValidFixedText = formatted;
        if (!string.Equals(original, formatted, StringComparison.Ordinal))
        {
            SetValueText(
                formatted,
                SalesDiscountInputFormatter.FindCaretIndex(
                    formatted,
                    digitsToRight));
        }
        UpdateSummary();
    }

    private void SetValueText(string text, int caretIndex)
    {
        try
        {
            _isFormattingValue = true;
            ValueBox.Text = text;
            ValueBox.CaretIndex = Math.Clamp(caretIndex, 0, text.Length);
            if (!IsPercentage)
                _lastValidFixedText = text;
        }
        finally
        {
            _isFormattingValue = false;
        }
    }

    private void OnApply(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        if (!TryRead(
                out var type,
                out var value,
                out var reason,
                out _,
                out var error))
        {
            ShowError(error);
            return;
        }

        DiscountType = type;
        DiscountValue = value;
        DiscountReason = reason;
        DialogResult = true;
    }

    private void UpdateSummary()
    {
        if (DiscountAmountText is null || ApplyButton is null)
            return;

        if (TryRead(
                out _,
                out _,
                out _,
                out var amount,
                out var error))
        {
            DiscountAmountText.Text =
                $"-{SalesDiscountInputFormatter.FormatVnd(amount)} đ";
            FinalTotalText.Text =
                $"{SalesDiscountInputFormatter.FormatVnd(_subtotal - amount)} đ";
            ApplyButton.IsEnabled = true;
            ShowError(string.Empty);
        }
        else
        {
            DiscountAmountText.Text = "0 đ";
            FinalTotalText.Text =
                $"{SalesDiscountInputFormatter.FormatVnd(_subtotal)} đ";
            ApplyButton.IsEnabled = false;
            ShowError(error);
        }
    }

    private void ShowError(string error)
    {
        ErrorText.Text = error;
        ErrorText.Visibility = string.IsNullOrEmpty(error)
            ? global::System.Windows.Visibility.Collapsed
            : global::System.Windows.Visibility.Visible;
    }

    private bool TryRead(
        out SalesDiscountType type,
        out long value,
        out string reason,
        out long amount,
        out string error)
    {
        type = IsPercentage
            ? SalesDiscountType.Percentage
            : SalesDiscountType.FixedAmount;
        amount = 0;
        reason = ReasonBox?.Text ?? string.Empty;
        error = string.Empty;

        var parsed = type == SalesDiscountType.FixedAmount
            ? SalesDiscountInputFormatter.TryParseVndInput(
                ValueBox?.Text,
                out value)
            : SalesDiscountInputFormatter.TryParsePercentage(
                ValueBox?.Text,
                out value);
        if (!parsed)
        {
            error = type == SalesDiscountType.FixedAmount
                ? "Nhập số tiền giảm hợp lệ, lớn hơn 0."
                : "Phần trăm phải lớn hơn 0, không quá 100 và tối đa 2 chữ số thập phân.";
            return false;
        }

        try
        {
            amount = SalesDiscountCalculator.Resolve(
                _subtotal,
                type,
                value,
                reason);
            reason = SalesDiscountCalculator.NormalizeReason(type, reason)!;
            return true;
        }
        catch (DomainException exception)
        {
            error = exception.Message;
            return false;
        }
    }
}
