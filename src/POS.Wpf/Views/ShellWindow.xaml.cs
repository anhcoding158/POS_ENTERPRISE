using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ chính của ứng dụng.
/// </summary>
public partial class ShellWindow :
    global::System.Windows.Window
{
    private readonly ShellViewModel _viewModel;

    public ShellWindow(
        ShellViewModel viewModel)
    {
        _viewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        InitializeComponent();

        DataContext = _viewModel;

        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnWindowLoaded;

        await _viewModel.InitializeAsync();
    }
}