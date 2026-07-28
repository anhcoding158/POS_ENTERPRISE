using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ quản lý danh mục.
/// </summary>
public partial class CategoryManagementWindow :
    global::System.Windows.Window
{
    private readonly global::System.Windows.Threading.DispatcherTimer
        _searchDebounceTimer;

    public CategoryManagementWindow(
        CategoryManagementViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(
            viewModel);

        InitializeComponent();

        DataContext =
            viewModel;

        _searchDebounceTimer =
            new global::System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };

        _searchDebounceTimer.Tick +=
            OnSearchDebounceTick;

        Closed +=
            OnWindowClosed;
    }

    private void OnSearchTextChanged(
        object sender,
        global::System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void OnSearchKeyDown(
        object sender,
        global::System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key !=
            global::System.Windows.Input.Key.Enter)
        {
            return;
        }

        _searchDebounceTimer.Stop();
        ExecuteSearch();
        e.Handled = true;
    }

    private void OnStatusFilterChanged(
        object sender,
        global::System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _searchDebounceTimer.Stop();
        ExecuteSearch();
    }

    private void OnClearFiltersClick(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        /*
         * ResetFiltersCommand đã tự tải đúng một lần.
         * Hủy tick do việc command đổi SearchTerm/status phát sinh.
         */
        _searchDebounceTimer.Stop();
    }

    private void OnSearchDebounceTick(
        object? sender,
        EventArgs e)
    {
        _searchDebounceTimer.Stop();
        ExecuteSearch();
    }

    private void ExecuteSearch()
    {
        if (DataContext is
                CategoryManagementViewModel viewModel &&
            viewModel.SearchCommand.CanExecute(null))
        {
            viewModel.SearchCommand.Execute(null);
        }
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Tick -=
            OnSearchDebounceTick;
        Closed -=
            OnWindowClosed;
    }
}
