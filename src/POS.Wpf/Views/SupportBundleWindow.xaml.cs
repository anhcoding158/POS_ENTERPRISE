using System.ComponentModel;
using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class SupportBundleWindow : global::System.Windows.Window
{
    private readonly SupportBundleViewModel _viewModel;
    private bool _allowClose;

    public SupportBundleWindow(SupportBundleViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
        _viewModel.CloseReady += OnCloseReady;
    }

    private void OnCloseClick(
        object sender, global::System.Windows.RoutedEventArgs e) => Close();

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        if (!_viewModel.RequestClose()) e.Cancel = true;
    }

    private void OnCloseReady(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            _allowClose = true;
            Close();
        });
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Closing -= OnWindowClosing;
        Closed -= OnWindowClosed;
        _viewModel.CloseReady -= OnCloseReady;
        _viewModel.Dispose();
    }
}
