using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class SupplierEditorWindow : global::System.Windows.Window
{
    private readonly SupplierEditorViewModel _viewModel;

    public SupplierEditorWindow(SupplierEditorViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.RequestClose += OnRequestClose;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnRequestClose(bool? result) => DialogResult = result;

    private void OnClosing(object? sender, global::System.ComponentModel.CancelEventArgs e)
    {
        if (DialogResult == true || !_viewModel.IsDirty || _viewModel.IsBusy) return;
        var answer = global::System.Windows.MessageBox.Show(this,
            "Bạn còn thay đổi chưa lưu. Đóng cửa sổ sẽ bỏ các thay đổi này?",
            "Thay đổi chưa lưu", global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Warning, global::System.Windows.MessageBoxResult.No);
        e.Cancel = answer != global::System.Windows.MessageBoxResult.Yes;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        Closing -= OnClosing;
        Closed -= OnClosed;
    }
}
