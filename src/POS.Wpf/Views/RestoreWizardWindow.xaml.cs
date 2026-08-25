using System.ComponentModel;
using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class RestoreWizardWindow : global::System.Windows.Window
{
    private readonly RestoreWizardViewModel _viewModel;
    private bool _closed;

    public RestoreWizardWindow(RestoreWizardViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
    }

    private void OnCancelClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (!_viewModel.CanCancel) return;
        if (_viewModel.IsBusy) _viewModel.CancelCommand.Execute(null);
        else Close();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_closed) return;
        if (!_viewModel.RequestClose()) e.Cancel = true;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _closed = true;
        Closing -= OnWindowClosing;
        Closed -= OnWindowClosed;
        _viewModel.Dispose();
        DataContext = null;
        Owner?.Activate();
    }
}
