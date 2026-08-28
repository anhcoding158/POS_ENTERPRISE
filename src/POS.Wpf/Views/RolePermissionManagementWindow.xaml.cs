using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class RolePermissionManagementWindow : global::System.Windows.Window
{
    public RolePermissionManagementWindow(RolePermissionManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }
}
