using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ quản lý danh mục.
/// </summary>
public partial class CategoryManagementWindow :
    global::System.Windows.Window
{
    public CategoryManagementWindow(
        CategoryManagementViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(
            viewModel);

        InitializeComponent();

        DataContext =
            viewModel;
    }
}