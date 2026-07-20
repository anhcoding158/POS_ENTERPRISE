using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ xem lịch sử tồn kho.
///
/// Code-behind chỉ thực hiện gắn DataContext.
/// Không chứa nghiệp vụ hoặc truy cập database.
/// </summary>
public partial class InventoryHistoryWindow :
    global::System.Windows.Window
{
    private readonly InventoryHistoryViewModel
        _viewModel;

    public InventoryHistoryWindow(
        InventoryHistoryViewModel viewModel)
    {
        _viewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        InitializeComponent();

        DataContext =
            _viewModel;
    }
}