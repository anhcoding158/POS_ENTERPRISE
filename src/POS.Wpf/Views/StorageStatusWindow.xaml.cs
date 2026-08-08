using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class StorageStatusWindow : global::System.Windows.Window
{
    private readonly StorageStatusViewModel _viewModel;

    public StorageStatusWindow(StorageStatusViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (_viewModel.State == StorageStatusUiState.NotChecked)
            await _viewModel.RefreshAsync();
    }

    private void OnCloseClick(
        object sender, global::System.Windows.RoutedEventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        Closed -= OnClosed;
        _viewModel.CancelRefresh();
    }
}
