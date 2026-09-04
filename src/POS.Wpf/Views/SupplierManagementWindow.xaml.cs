using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class SupplierManagementWindow : global::System.Windows.Window
{
    private readonly SupplierManagementViewModel _viewModel;
    private readonly global::System.Windows.Threading.DispatcherTimer _searchTimer;

    public SupplierManagementWindow(SupplierManagementViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _searchTimer = new global::System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += OnSearchTimer;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, global::System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
    }

    private void OnSearchKeyDown(object sender, global::System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != global::System.Windows.Input.Key.Enter) { _searchTimer.Stop(); _searchTimer.Start(); return; }
        _searchTimer.Stop(); if (_viewModel.SearchCommand.CanExecute(null)) _viewModel.SearchCommand.Execute(null); e.Handled = true;
    }

    private void OnFilterChanged(object sender, global::System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _searchTimer.Stop(); if (_viewModel.SearchCommand.CanExecute(null)) _viewModel.SearchCommand.Execute(null);
    }

    private void OnSearchTimer(object? sender, EventArgs e)
    {
        _searchTimer.Stop(); if (_viewModel.SearchCommand.CanExecute(null)) _viewModel.SearchCommand.Execute(null);
    }

    private void OnToggleActiveClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (_viewModel.SelectedDetails is null) return;
        var action = _viewModel.SelectedDetails.IsActive ? "ngừng hoạt động" : "kích hoạt lại";
        var answer = global::System.Windows.MessageBox.Show(this,
            $"Bạn có chắc muốn {action} nhà cung cấp “{_viewModel.SelectedDetails.Name}” không?",
            "Xác nhận thay đổi trạng thái", global::System.Windows.MessageBoxButton.YesNo,
            global::System.Windows.MessageBoxImage.Warning, global::System.Windows.MessageBoxResult.No);
        if (answer == global::System.Windows.MessageBoxResult.Yes &&
            _viewModel.ToggleActiveCommand.CanExecute(null))
        {
            _viewModel.ToggleActiveCommand.Execute(null);
        }
        e.Handled = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _searchTimer.Stop(); _searchTimer.Tick -= OnSearchTimer; Closed -= OnClosed;
    }
}
