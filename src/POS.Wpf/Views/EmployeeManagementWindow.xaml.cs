using POS.Wpf.ViewModels;
using System.ComponentModel;

namespace POS.Wpf.Views;

public partial class EmployeeManagementWindow : global::System.Windows.Window
{
    private EmployeeManagementViewModel ViewModel => (EmployeeManagementViewModel)DataContext;

    public EmployeeManagementWindow(EmployeeManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, global::System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.InitializeAsync();
    }

    private async void OnSelectionChanged(object sender, global::System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is EmployeeManagementViewModel viewModel && e.AddedItems.Count > 0 && e.AddedItems[0] is EmployeeRowViewModel row)
            await viewModel.SelectEmployeeAsync(row.Id);
    }

    private void OnAccountPasswordChanged(object sender, global::System.Windows.RoutedEventArgs e) => ViewModel.SetAccountPassword(AccountPasswordInput.Password);
    private void OnResetPasswordChanged(object sender, global::System.Windows.RoutedEventArgs e) => ViewModel.SetResetPassword(ResetPasswordInput.Password);

    private void OnToggleLockClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (global::System.Windows.MessageBox.Show(this, "Xác nhận thay đổi trạng thái khóa tài khoản?", "Xác nhận", global::System.Windows.MessageBoxButton.YesNo, global::System.Windows.MessageBoxImage.Warning) == global::System.Windows.MessageBoxResult.Yes)
            ViewModel.ToggleLockCommand.Execute(null);
    }

    private void OnToggleActiveClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (global::System.Windows.MessageBox.Show(this, "Xác nhận thay đổi trạng thái nhân viên? Lịch sử giao dịch sẽ được giữ nguyên.", "Xác nhận", global::System.Windows.MessageBoxButton.YesNo, global::System.Windows.MessageBoxImage.Warning) == global::System.Windows.MessageBoxResult.Yes)
            ViewModel.ToggleActiveCommand.Execute(null);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.IsDirty || ViewModel.IsBusy)
            return;

        var result = global::System.Windows.MessageBox.Show(
            this,
            "Bạn còn thay đổi chưa lưu. Bạn có muốn đóng và bỏ các thay đổi này không?",
            "Thay đổi chưa lưu",
            global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Warning);
        if (result != global::System.Windows.MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        ViewModel.DiscardUnsavedChanges();
    }
}
