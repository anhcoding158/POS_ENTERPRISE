using POS.Wpf.ViewModels;
using System.ComponentModel;
using System.Windows.Threading;

namespace POS.Wpf.Views;

public partial class EmployeeManagementWindow : global::System.Windows.Window
{
    private EmployeeManagementViewModel ViewModel => (EmployeeManagementViewModel)DataContext;
    private DispatcherTimer? _toastTimer;

    public EmployeeManagementWindow(EmployeeManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.AccountSuccessRequested += OnAccountSuccessRequested;
        viewModel.ToastRequested += OnToastRequested;
        viewModel.ErrorNotificationRequested += OnErrorNotificationRequested;
        RefreshRoleFilterBinding();
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, global::System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ViewModel.InitializeAsync();
        RefreshRoleFilterBinding();
    }

    private void RefreshRoleFilterBinding()
    {
        EmployeeRoleFilterComboBox.GetBindingExpression(
            global::System.Windows.Controls.ComboBox.SelectedItemProperty)?.UpdateTarget();
        EmployeeRoleFilterComboBox.GetBindingExpression(
            global::System.Windows.Controls.ComboBox.TextProperty)?.UpdateTarget();
    }

    private async void OnSelectionChanged(object sender, global::System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is not EmployeeManagementViewModel viewModel || e.AddedItems.Count == 0 || e.AddedItems[0] is not EmployeeRowViewModel row)
            return;

        if (viewModel.IsDirty)
        {
            var result = global::System.Windows.MessageBox.Show(
                this,
                "Bạn còn thay đổi chưa lưu. Chuyển sang nhân viên khác và bỏ các thay đổi này?",
                "Thay đổi chưa lưu",
                global::System.Windows.MessageBoxButton.YesNo,
                global::System.Windows.MessageBoxImage.Warning);
            if (result != global::System.Windows.MessageBoxResult.Yes)
            {
                viewModel.RestoreSelection(e.RemovedItems.Count > 0 ? e.RemovedItems[0] as EmployeeRowViewModel : null);
                return;
            }

            viewModel.DiscardUnsavedChanges();
        }

        await viewModel.SelectEmployeeAsync(row.Id);
    }

    private async void OnFilterChanged(object sender, global::System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (IsLoaded && DataContext is EmployeeManagementViewModel viewModel && !viewModel.IsBusy)
            await viewModel.ApplyFiltersAsync();
    }

    private void OnAccountPasswordChanged(object sender, global::System.Windows.RoutedEventArgs e) => ViewModel.SetAccountPassword(AccountPasswordInput.Password);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EmployeeManagementViewModel.IsCreatingAccount) && !ViewModel.IsCreatingAccount)
            AccountPasswordInput.Clear();
    }

    private void OnAccountSuccessRequested(object? sender, EmployeeAccountSuccessEventArgs e)
    {
        var dialog = new EmployeeAccountSuccessWindow(e)
        {
            Owner = this,
            WindowStartupLocation = global::System.Windows.WindowStartupLocation.CenterOwner
        };
        dialog.ShowDialog();
    }

    private void OnToastRequested(object? sender, EmployeeOperationToastEventArgs e)
    {
        EmployeeToastText.Text = e.Message;
        EmployeeToastHost.Visibility = global::System.Windows.Visibility.Visible;
        _toastTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
        _toastTimer.Stop();
        _toastTimer.Tick -= OnToastTimerTick;
        _toastTimer.Tick += OnToastTimerTick;
        _toastTimer.Start();
    }

    private void OnErrorNotificationRequested(object? sender, EmployeeOperationErrorEventArgs e)
    {
        HideToast();
        global::System.Windows.MessageBox.Show(
            this,
            e.Message,
            e.Title,
            global::System.Windows.MessageBoxButton.OK,
            global::System.Windows.MessageBoxImage.Warning);
    }

    private void OnToastTimerTick(object? sender, EventArgs e) => HideToast();

    private void OnToastCloseClick(object sender, global::System.Windows.RoutedEventArgs e) => HideToast();

    private void HideToast()
    {
        _toastTimer?.Stop();
        EmployeeToastHost.Visibility = global::System.Windows.Visibility.Collapsed;
    }
    private async void OnResetPasswordClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (!ViewModel.ResetPasswordCommand.CanExecute(null))
            return;

        var dialog = new EmployeePasswordResetWindow { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        var temporaryPassword = dialog.TemporaryPassword;
        try
        {
            await ViewModel.ResetPasswordWithValueAsync(temporaryPassword);
        }
        finally
        {
            temporaryPassword = string.Empty;
        }
    }

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

    private void OnToggleAccountActiveClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (!ViewModel.ToggleAccountActiveCommand.CanExecute(null))
            return;

        var message = ViewModel.AccountStatusText == "Đã vô hiệu hóa"
            ? "Xác nhận kích hoạt lại tài khoản? Tài khoản sẽ được phép đăng nhập trở lại."
            : "Xác nhận vô hiệu hóa tài khoản? Tài khoản sẽ không thể đăng nhập; lịch sử vẫn được giữ nguyên.";
        if (global::System.Windows.MessageBox.Show(this, message, "Xác nhận", global::System.Windows.MessageBoxButton.YesNo, global::System.Windows.MessageBoxImage.Warning) == global::System.Windows.MessageBoxResult.Yes)
            ViewModel.ToggleAccountActiveCommand.Execute(null);
    }

    private void OnChangeRoleClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (global::System.Windows.MessageBox.Show(this, "Xác nhận cập nhật vai trò và quyền hiệu lực của tài khoản?", "Xác nhận", global::System.Windows.MessageBoxButton.YesNo, global::System.Windows.MessageBoxImage.Warning) != global::System.Windows.MessageBoxResult.Yes)
            return;

        ViewModel.ChangeRoleCommand.Execute(null);
    }

    private void OnCreateCloseClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (ViewModel.IsDirty && global::System.Windows.MessageBox.Show(
                this,
                "Bạn có thay đổi chưa lưu. Bạn có muốn hủy việc thêm nhân viên?",
                "Thay đổi chưa lưu",
                global::System.Windows.MessageBoxButton.YesNo,
                global::System.Windows.MessageBoxImage.Warning) != global::System.Windows.MessageBoxResult.Yes)
            return;

        ViewModel.CancelEditCommand.Execute(null);
    }

    private void OnCancelEditClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (ViewModel.IsCreateMode)
        {
            OnCreateCloseClick(sender, e);
            return;
        }

        ViewModel.CancelEditCommand.Execute(null);
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
        AccountPasswordInput.Clear();
    }

    private void OnClosed(object? sender, global::System.EventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.AccountSuccessRequested -= OnAccountSuccessRequested;
        ViewModel.ToastRequested -= OnToastRequested;
        ViewModel.ErrorNotificationRequested -= OnErrorNotificationRequested;
        HideToast();
        _toastTimer = null;
        AccountPasswordInput?.Clear();
        ViewModel.Dispose();
    }
}
