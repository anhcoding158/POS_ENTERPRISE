using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class StoreSettingsWindow : global::System.Windows.Window
{
    private readonly StoreSettingsViewModel _viewModel;
    private bool _isShowingSaveSuccess;
    public StoreSettingsWindow(StoreSettingsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = viewModel;
        Closing += OnClosing;
        _viewModel.ScannerTest.FocusRequested += OnScannerFocusRequested;
        _viewModel.SaveSucceeded += OnSaveSucceeded;
    }

    private async void OnWindowLoaded(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnWindowLoaded;
        await _viewModel.InitializeAsync();
    }

    private void OnScannerFocusRequested(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(
            () =>
            {
                StoreSetupScannerCapture.Focus();
                StoreSetupScannerCapture.SelectAll();
            },
            global::System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnScannerCaptureKeyDown(
        object sender,
        global::System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != global::System.Windows.Input.Key.Enter)
            return;

        _viewModel.ScannerTest.ReceiveScan(
            StoreSetupScannerCapture.Text);
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.ScannerTest.FocusRequested -= OnScannerFocusRequested;
        _viewModel.SaveSucceeded -= OnSaveSucceeded;
        _viewModel.ScannerTest.Dispose();
        base.OnClosed(e);
    }

    private void OnSaveSucceeded(object? sender, StoreSettingsSaveSucceededEventArgs e)
    {
        if (_isShowingSaveSuccess)
            return;

        _isShowingSaveSuccess = true;
        try
        {
            var dialog = new SuccessDialogWindow(SuccessDialogRequest.StoreSettingsSaved())
            {
                Owner = this,
                WindowStartupLocation = global::System.Windows.WindowStartupLocation.CenterOwner
            };
            dialog.ShowDialog();
            StoreSetupSave.Focus();
        }
        finally
        {
            _isShowingSaveSuccess = false;
        }
    }
    private void OnCancelClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (_viewModel.IsDirty &&
            global::System.Windows.MessageBox.Show(
                "Bạn có thay đổi chưa lưu. Bỏ các thay đổi này?",
                "Xác nhận",
                global::System.Windows.MessageBoxButton.YesNo,
                global::System.Windows.MessageBoxImage.Question) !=
                global::System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        _viewModel.ResetCommand.Execute(null);
    }
    private void OnClosing(object? sender, global::System.ComponentModel.CancelEventArgs e) { if (_viewModel.IsDirty && global::System.Windows.MessageBox.Show("Bạn có thay đổi chưa lưu. Đóng cửa sổ?", "Xác nhận", global::System.Windows.MessageBoxButton.YesNo, global::System.Windows.MessageBoxImage.Question) != global::System.Windows.MessageBoxResult.Yes) e.Cancel = true; }
}
