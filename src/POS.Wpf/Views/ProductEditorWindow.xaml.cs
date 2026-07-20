using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ thêm và sửa sản phẩm.
/// </summary>
public partial class ProductEditorWindow :
    global::System.Windows.Window
{
    private readonly ProductEditorViewModel
        _viewModel;

    public ProductEditorWindow(
        ProductEditorViewModel viewModel)
    {
        _viewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        InitializeComponent();

        DataContext = _viewModel;

        _viewModel.RequestClose +=
            OnRequestClose;

        Closed += OnWindowClosed;
    }

    private void OnRequestClose(
        bool? dialogResult)
    {
        DialogResult = dialogResult;
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
    {
        _viewModel.RequestClose -=
            OnRequestClose;

        Closed -= OnWindowClosed;
    }
}