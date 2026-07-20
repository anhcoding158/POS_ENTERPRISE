using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ điều chỉnh kho.
/// </summary>
public partial class InventoryAdjustmentWindow :
    global::System.Windows.Window
{
    private readonly InventoryAdjustmentViewModel
        _viewModel;

    public InventoryAdjustmentWindow(
        InventoryAdjustmentViewModel viewModel)
    {
        _viewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        InitializeComponent();

        DataContext =
            _viewModel;

        _viewModel.CloseRequested +=
            OnCloseRequested;

        Closed +=
            OnWindowClosed;
    }

    private void OnCloseRequested(
        bool dialogResult)
    {
        /*
         * InventoryDialogService luôn mở cửa sổ bằng ShowDialog,
         * nên gán DialogResult sẽ tự đóng cửa sổ.
         */
        DialogResult =
            dialogResult;
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
    {
        _viewModel.CloseRequested -=
            OnCloseRequested;

        Closed -=
            OnWindowClosed;
    }
}