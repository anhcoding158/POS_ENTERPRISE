using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

public partial class ForcedPasswordChangeWindow : global::System.Windows.Window
{
    private readonly ForcedPasswordChangeViewModel _viewModel;

    public ForcedPasswordChangeWindow(ForcedPasswordChangeViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.RequestClose += OnRequestClose;
        Closed += OnClosed;
    }

    private void OnNewPasswordChanged(object sender, global::System.Windows.RoutedEventArgs e) =>
        _viewModel.UpdateNewPassword(NewPasswordInput.Password);

    private void OnConfirmPasswordChanged(object sender, global::System.Windows.RoutedEventArgs e) =>
        _viewModel.UpdateConfirmPassword(ConfirmPasswordInput.Password);

    private void OnRequestClose(bool? dialogResult) => DialogResult = dialogResult;

    private void OnClosed(object? sender, EventArgs e) => _viewModel.RequestClose -= OnRequestClose;
}
