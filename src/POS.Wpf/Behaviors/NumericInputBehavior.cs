using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using POS.Wpf.Services;

namespace POS.Wpf.Behaviors;

public static class NumericInputBehavior
{
    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.RegisterAttached(
            "Mode", typeof(NumericInputMode?), typeof(NumericInputBehavior),
            new PropertyMetadata(null, OnModeChanged));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderText", typeof(string), typeof(NumericInputBehavior),
            new PropertyMetadata(string.Empty, OnPlaceholderChanged));

    public static readonly DependencyProperty AllowNegativeProperty =
        DependencyProperty.RegisterAttached(
            "AllowNegative", typeof(bool), typeof(NumericInputBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsAttachedProperty =
        DependencyProperty.RegisterAttached("IsAttached", typeof(bool), typeof(NumericInputBehavior));
    private static readonly DependencyProperty IsFormattingProperty =
        DependencyProperty.RegisterAttached("IsFormatting", typeof(bool), typeof(NumericInputBehavior));
    private static readonly DependencyProperty LastValidTextProperty =
        DependencyProperty.RegisterAttached(
            "LastValidText", typeof(string), typeof(NumericInputBehavior),
            new PropertyMetadata(string.Empty));
    private static readonly DependencyProperty PlaceholderAdornerProperty =
        DependencyProperty.RegisterAttached(
            "PlaceholderAdorner", typeof(NumericPlaceholderAdorner), typeof(NumericInputBehavior));

    public static void SetMode(DependencyObject element, NumericInputMode? value) => element.SetValue(ModeProperty, value);
    public static NumericInputMode? GetMode(DependencyObject element) => (NumericInputMode?)element.GetValue(ModeProperty);
    public static void SetPlaceholderText(DependencyObject element, string value) => element.SetValue(PlaceholderTextProperty, value);
    public static string GetPlaceholderText(DependencyObject element) => (string)element.GetValue(PlaceholderTextProperty);
    public static void SetAllowNegative(DependencyObject element, bool value) => element.SetValue(AllowNegativeProperty, value);
    public static bool GetAllowNegative(DependencyObject element) => (bool)element.GetValue(AllowNegativeProperty);

    private static void OnModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not TextBox textBox) return;
        if (args.OldValue is NumericInputMode) Detach(textBox);
        if (args.NewValue is NumericInputMode mode) Attach(textBox, mode);
    }

    private static void OnPlaceholderChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is TextBox textBox) RefreshPlaceholder(textBox);
    }

    private static void Attach(TextBox textBox, NumericInputMode mode)
    {
        if (GetIsAttached(textBox)) return;

        SetIsAttached(textBox, true);
        SetLastValidText(
            textBox,
            NumericInputFormatter.TryParse(textBox.Text, mode, out var value)
                ? NumericInputFormatter.Format(value, mode)
                : string.Empty);
        textBox.PreviewTextInput += OnPreviewTextInput;
        textBox.PreviewKeyDown += OnPreviewKeyDown;
        textBox.TextChanged += OnTextChanged;
        DataObject.AddPastingHandler(textBox, OnPasting);
        textBox.Loaded += OnLoaded;
        textBox.Unloaded += OnUnloaded;
        RefreshPlaceholder(textBox);
    }

    private static void Detach(TextBox textBox)
    {
        if (!GetIsAttached(textBox)) return;
        textBox.PreviewTextInput -= OnPreviewTextInput;
        textBox.PreviewKeyDown -= OnPreviewKeyDown;
        textBox.TextChanged -= OnTextChanged;
        DataObject.RemovePastingHandler(textBox, OnPasting);
        textBox.Loaded -= OnLoaded;
        textBox.Unloaded -= OnUnloaded;
        RemovePlaceholder(textBox);
        SetIsAttached(textBox, false);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || GetMode(textBox) is not { } mode) return;
        NormalizeExternalText(textBox, mode);
        EnsurePlaceholder(textBox);
        RefreshPlaceholder(textBox);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox) RemovePlaceholder(textBox);
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox || GetMode(textBox) is not { } mode) return;

        e.Handled = true;
        if (mode == NumericInputMode.SignedInteger &&
            e.Text == "-" &&
            GetAllowNegative(textBox) &&
            CountDigitsBefore(textBox.Text, textBox.CaretIndex) == 0)
        {
            ApplyNegativeSign(textBox);
            return;
        }

        if (string.IsNullOrEmpty(e.Text) || e.Text.All(IsSeparator)) return;
        if (!e.Text.All(IsAsciiDigit)) return;

        ApplyResult(textBox, NumericInputEditEngine.Insert(
            textBox.Text, textBox.CaretIndex, textBox.SelectionStart,
            textBox.SelectionLength, e.Text, mode, GetAllowNegative(textBox)));
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || GetMode(textBox) is not { } mode ||
            Keyboard.Modifiers != ModifierKeys.None) return;

        NumericEditResult? result = e.Key switch
        {
            Key.Back => NumericInputEditEngine.Backspace(
                textBox.Text, textBox.CaretIndex, textBox.SelectionStart,
                textBox.SelectionLength, mode, GetAllowNegative(textBox)),
            Key.Delete => NumericInputEditEngine.Delete(
                textBox.Text, textBox.CaretIndex, textBox.SelectionStart,
                textBox.SelectionLength, mode, GetAllowNegative(textBox)),
            _ => null
        };
        if (result is null) return;
        e.Handled = true;
        ApplyResult(textBox, result);
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || GetIsFormatting(textBox) ||
            GetMode(textBox) is not { } mode) return;

        RefreshPlaceholder(textBox);
        if (textBox.IsReadOnly || !textBox.IsEnabled)
        {
            SetLastValidText(textBox, textBox.Text);
            return;
        }
        NormalizeExternalText(textBox, mode);
    }

    private static void NormalizeExternalText(TextBox textBox, NumericInputMode mode)
    {
        if (string.IsNullOrEmpty(textBox.Text))
        {
            SetLastValidText(textBox, string.Empty);
            RefreshPlaceholder(textBox);
            return;
        }

        if (!NumericInputFormatter.TryParse(textBox.Text, mode, out var value))
        {
            RestoreLastValidText(textBox);
            return;
        }

        var formatted = NumericInputFormatter.Format(value, mode);
        SetLastValidText(textBox, formatted);
        if (string.Equals(textBox.Text, formatted, StringComparison.Ordinal)) return;

        SetIsFormatting(textBox, true);
        try
        {
            textBox.Text = formatted;
            textBox.CaretIndex = formatted.Length;
        }
        finally { SetIsFormatting(textBox, false); }
        RefreshPlaceholder(textBox);
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || GetMode(textBox) is not { } mode ||
            !e.DataObject.GetDataPresent(DataFormats.UnicodeText) ||
            e.DataObject.GetData(DataFormats.UnicodeText) is not string pasted ||
            !NumericInputFormatter.TryParse(pasted, mode, out var pastedValue) ||
            pastedValue < 0 && !GetAllowNegative(textBox))
        {
            e.CancelCommand();
            return;
        }

        var magnitude = Math.Abs(pastedValue).ToString(CultureInfo.InvariantCulture);
        var replacesAll = textBox.SelectionStart == 0 &&
            textBox.SelectionLength >= textBox.Text.Length;
        e.CancelCommand();
        ApplyResult(textBox, NumericInputEditEngine.Insert(
            textBox.Text, textBox.CaretIndex, textBox.SelectionStart,
            textBox.SelectionLength, magnitude, mode, GetAllowNegative(textBox),
            replacesAll ? pastedValue < 0 : null));
    }

    private static void ApplyNegativeSign(TextBox textBox)
    {
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;
        SetIsFormatting(textBox, true);
        try
        {
            textBox.Text = textBox.Text.Remove(selectionStart, selectionLength)
                .Insert(selectionStart, "-");
            textBox.CaretIndex = selectionStart + 1;
        }
        finally { SetIsFormatting(textBox, false); }
    }

    private static void ApplyResult(TextBox textBox, NumericEditResult result)
    {
        if (!result.IsValid) return;
        SetLastValidText(textBox, result.DisplayText);
        SetIsFormatting(textBox, true);
        try
        {
            textBox.Text = result.DisplayText;
            textBox.CaretIndex = Math.Clamp(result.CaretIndex, 0, result.DisplayText.Length);
        }
        finally { SetIsFormatting(textBox, false); }
        RefreshPlaceholder(textBox);
    }

    private static void RestoreLastValidText(TextBox textBox)
    {
        var lastValid = GetLastValidText(textBox);
        SetIsFormatting(textBox, true);
        try
        {
            textBox.Text = lastValid;
            textBox.CaretIndex = lastValid.Length;
        }
        finally { SetIsFormatting(textBox, false); }
        RefreshPlaceholder(textBox);
    }

    private static int CountDigitsBefore(string text, int index) =>
        text[..Math.Clamp(index, 0, text.Length)].Count(IsAsciiDigit);
    private static bool IsAsciiDigit(char character) => character is >= '0' and <= '9';
    private static bool IsSeparator(char character) => character is '.' or ',' or ' ' or '\u00A0' or '\u202F';

    private static void EnsurePlaceholder(TextBox textBox)
    {
        var layer = AdornerLayer.GetAdornerLayer(textBox);
        if (layer is null || GetPlaceholderAdorner(textBox) is not null) return;
        var adorner = new NumericPlaceholderAdorner(textBox);
        SetPlaceholderAdorner(textBox, adorner);
        layer.Add(adorner);
    }

    private static void RefreshPlaceholder(TextBox textBox) =>
        GetPlaceholderAdorner(textBox)?.Update(GetPlaceholderText(textBox), string.IsNullOrEmpty(textBox.Text));

    private static void RemovePlaceholder(TextBox textBox)
    {
        var adorner = GetPlaceholderAdorner(textBox);
        if (adorner is null) return;
        AdornerLayer.GetAdornerLayer(textBox)?.Remove(adorner);
        SetPlaceholderAdorner(textBox, null);
    }

    private static bool GetIsAttached(DependencyObject element) => (bool)element.GetValue(IsAttachedProperty);
    private static void SetIsAttached(DependencyObject element, bool value) => element.SetValue(IsAttachedProperty, value);
    private static bool GetIsFormatting(DependencyObject element) => (bool)element.GetValue(IsFormattingProperty);
    private static void SetIsFormatting(DependencyObject element, bool value) => element.SetValue(IsFormattingProperty, value);
    private static string GetLastValidText(DependencyObject element) => (string)element.GetValue(LastValidTextProperty);
    private static void SetLastValidText(DependencyObject element, string value) => element.SetValue(LastValidTextProperty, value);
    private static NumericPlaceholderAdorner? GetPlaceholderAdorner(DependencyObject element) => (NumericPlaceholderAdorner?)element.GetValue(PlaceholderAdornerProperty);
    private static void SetPlaceholderAdorner(DependencyObject element, NumericPlaceholderAdorner? value) => element.SetValue(PlaceholderAdornerProperty, value);

    private sealed class NumericPlaceholderAdorner : Adorner
    {
        private readonly TextBlock _textBlock = new();
        public NumericPlaceholderAdorner(TextBox owner) : base(owner)
        {
            IsHitTestVisible = false;
            _textBlock.IsHitTestVisible = false;
            AddVisualChild(_textBlock);
            AddLogicalChild(_textBlock);
        }
        public void Update(string text, bool isVisible)
        {
            var owner = (TextBox)AdornedElement;
            _textBlock.Text = text;
            _textBlock.Visibility = isVisible && text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            _textBlock.Foreground = owner.Foreground;
            _textBlock.FontFamily = owner.FontFamily;
            _textBlock.FontSize = owner.FontSize;
            _textBlock.FontWeight = owner.FontWeight;
            _textBlock.Opacity = 0.45;
            InvalidateMeasure();
            InvalidateArrange();
        }
        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => index == 0 ? _textBlock : throw new ArgumentOutOfRangeException(nameof(index));
        protected override Size MeasureOverride(Size constraint) { _textBlock.Measure(constraint); return constraint; }
        protected override Size ArrangeOverride(Size finalSize)
        {
            var padding = ((TextBox)AdornedElement).Padding;
            _textBlock.Arrange(new Rect(padding.Left, padding.Top,
                Math.Max(0, finalSize.Width - padding.Left - padding.Right),
                Math.Max(0, finalSize.Height - padding.Top - padding.Bottom)));
            return finalSize;
        }
    }
}
