using System.ComponentModel;
using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class ProductExportWindow : global::System.Windows.Window
{
    private readonly ProductExportViewModel _viewModel;

    public ProductExportWindow(ProductExportViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.DialogOwner = this;
        _viewModel.RequestClose += OnRequestClose;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnRequestClose(bool? result) => DialogResult = result;
    private void OnClosing(object? sender, CancelEventArgs e) { if (_viewModel.IsBusy) e.Cancel = true; }
    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        Closing -= OnClosing;
        Closed -= OnClosed;
        _viewModel.Dispose();
    }
}
