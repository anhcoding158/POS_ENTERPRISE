using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class StoreSettingsWindow : global::System.Windows.Window
{
    private readonly StoreSettingsViewModel _viewModel;
    public StoreSettingsWindow(StoreSettingsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent(); DataContext = viewModel; Closing += OnClosing;
    }
    private void OnCancelClick(object sender, global::System.Windows.RoutedEventArgs e) { if (_viewModel.IsDirty && global::System.Windows.MessageBox.Show("Bạn có thay đổi chưa lưu. Đóng cửa sổ?", "Xác nhận", global::System.Windows.MessageBoxButton.YesNo, global::System.Windows.MessageBoxImage.Question) != global::System.Windows.MessageBoxResult.Yes) return; Close(); }
    private void OnClosing(object? sender, global::System.ComponentModel.CancelEventArgs e) { if (_viewModel.IsDirty && global::System.Windows.MessageBox.Show("Bạn có thay đổi chưa lưu. Đóng cửa sổ?", "Xác nhận", global::System.Windows.MessageBoxButton.YesNo, global::System.Windows.MessageBoxImage.Question) != global::System.Windows.MessageBoxResult.Yes) e.Cancel = true; }
}
