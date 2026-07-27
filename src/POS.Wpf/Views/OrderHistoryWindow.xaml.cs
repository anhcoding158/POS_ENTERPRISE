using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class OrderHistoryWindow :
    global::System.Windows.Window
{
    private readonly OrderHistoryViewModel _viewModel;

    public OrderHistoryWindow(OrderHistoryViewModel viewModel)
    {
        _viewModel = viewModel ??
            throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(
        object sender,
        global::System.Windows.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (_viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Dispose();
    }
}
