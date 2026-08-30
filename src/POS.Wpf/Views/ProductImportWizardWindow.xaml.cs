using System.ComponentModel;
using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class ProductImportWizardWindow : global::System.Windows.Window
{
    private readonly ProductImportWizardViewModel _viewModel;

    public ProductImportWizardWindow(ProductImportWizardViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.RequestClose += OnRequestClose;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnRequestClose(bool? dialogResult) => DialogResult = dialogResult;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.RequestWindowClose()) e.Cancel = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        Closing -= OnClosing;
        Closed -= OnClosed;
        _viewModel.Dispose();
    }
}
