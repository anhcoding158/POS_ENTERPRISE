using POS.Wpf.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.Wpf.Views;

public partial class OrderReturnWindow : global::System.Windows.Window
{
    private static readonly Regex NonDigitPattern = new(
        "[^0-9]+",
        RegexOptions.CultureInvariant);

    public OrderReturnWindow(OrderReturnViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(OrderReturnViewModel.IsSuccessful) &&
                viewModel.IsSuccessful)
            {
                DialogResult = true;
            }
        };
    }

    private void OnContentRendered(object? sender, EventArgs eventArgs)
    {
        ReturnLinesGrid.UpdateLayout();
        FindVisualChild<TextBox>(ReturnLinesGrid, "ReturnQuantityEditor")?.Focus();
    }

    private static T? FindVisualChild<T>(
        DependencyObject parent,
        string name)
        where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match && match.Name == name)
                return match;
            var descendant = FindVisualChild<T>(child, name);
            if (descendant is not null)
                return descendant;
        }
        return null;
    }

    private void OnQuantityPreviewTextInput(
        object sender,
        TextCompositionEventArgs eventArgs) =>
        eventArgs.Handled = NonDigitPattern.IsMatch(eventArgs.Text);

    private void OnQuantityPasting(
        object sender,
        DataObjectPastingEventArgs eventArgs)
    {
        if (!eventArgs.DataObject.GetDataPresent(DataFormats.UnicodeText) ||
            eventArgs.DataObject.GetData(DataFormats.UnicodeText) is not string text ||
            string.IsNullOrEmpty(text) ||
            NonDigitPattern.IsMatch(text))
            eventArgs.CancelCommand();
    }

    private void OnReturnLinesPreviewKeyDown(
        object sender,
        KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Enter)
            return;
        eventArgs.Handled = true;
        if (Keyboard.FocusedElement is UIElement element)
            element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }
}
