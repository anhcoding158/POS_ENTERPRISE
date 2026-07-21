using POS.Wpf.ViewModels;

namespace POS.Wpf.Views;

/// <summary>
/// Cửa sổ thiết lập Administrator đầu tiên.
/// </summary>
public partial class FirstRunSetupWindow :
    global::System.Windows.Window
{
    private readonly FirstRunSetupViewModel
        _viewModel;

    private bool
        _isSynchronizingPasswords;

    public FirstRunSetupWindow(
        FirstRunSetupViewModel viewModel)
    {
        _viewModel =
            viewModel ??
            throw new ArgumentNullException(
                nameof(viewModel));

        InitializeComponent();

        DataContext =
            _viewModel;

        _viewModel.RequestClose +=
            OnRequestClose;

        Loaded +=
            OnWindowLoaded;

        Closed +=
            OnWindowClosed;
    }

    private void OnWindowLoaded(
        object sender,
        global::System.Windows
            .RoutedEventArgs e)
    {
        UsernameInput.Focus();
    }

    private void OnPasswordChanged(
        object sender,
        global::System.Windows
            .RoutedEventArgs e)
    {
        if (_isSynchronizingPasswords)
        {
            return;
        }

        _isSynchronizingPasswords = true;

        try
        {
            var password =
                PasswordInput.Password;

            if (!string.Equals(
                    VisiblePasswordInput.Text,
                    password,
                    StringComparison.Ordinal))
            {
                VisiblePasswordInput.Text =
                    password;
            }

            _viewModel.UpdatePassword(
                password);
        }
        finally
        {
            _isSynchronizingPasswords = false;
        }
    }

    private void OnVisiblePasswordChanged(
        object sender,
        global::System.Windows.Controls
            .TextChangedEventArgs e)
    {
        if (_isSynchronizingPasswords)
        {
            return;
        }

        _isSynchronizingPasswords = true;

        try
        {
            var password =
                VisiblePasswordInput.Text;

            if (!string.Equals(
                    PasswordInput.Password,
                    password,
                    StringComparison.Ordinal))
            {
                PasswordInput.Password =
                    password;
            }

            _viewModel.UpdatePassword(
                password);
        }
        finally
        {
            _isSynchronizingPasswords = false;
        }
    }

    private void OnConfirmPasswordChanged(
        object sender,
        global::System.Windows
            .RoutedEventArgs e)
    {
        if (_isSynchronizingPasswords)
        {
            return;
        }

        _isSynchronizingPasswords = true;

        try
        {
            var password =
                ConfirmPasswordInput.Password;

            if (!string.Equals(
                    VisibleConfirmPasswordInput.Text,
                    password,
                    StringComparison.Ordinal))
            {
                VisibleConfirmPasswordInput.Text =
                    password;
            }

            _viewModel.UpdateConfirmPassword(
                password);
        }
        finally
        {
            _isSynchronizingPasswords = false;
        }
    }

    private void OnVisibleConfirmPasswordChanged(
        object sender,
        global::System.Windows.Controls
            .TextChangedEventArgs e)
    {
        if (_isSynchronizingPasswords)
        {
            return;
        }

        _isSynchronizingPasswords = true;

        try
        {
            var password =
                VisibleConfirmPasswordInput.Text;

            if (!string.Equals(
                    ConfirmPasswordInput.Password,
                    password,
                    StringComparison.Ordinal))
            {
                ConfirmPasswordInput.Password =
                    password;
            }

            _viewModel.UpdateConfirmPassword(
                password);
        }
        finally
        {
            _isSynchronizingPasswords = false;
        }
    }

    private void OnShowPasswordsChecked(
        object sender,
        global::System.Windows
            .RoutedEventArgs e)
    {
        VisiblePasswordInput.Visibility =
            global::System.Windows.Visibility
                .Visible;

        PasswordInput.Visibility =
            global::System.Windows.Visibility
                .Collapsed;

        VisibleConfirmPasswordInput.Visibility =
            global::System.Windows.Visibility
                .Visible;

        ConfirmPasswordInput.Visibility =
            global::System.Windows.Visibility
                .Collapsed;
    }

    private void OnShowPasswordsUnchecked(
        object sender,
        global::System.Windows
            .RoutedEventArgs e)
    {
        PasswordInput.Visibility =
            global::System.Windows.Visibility
                .Visible;

        VisiblePasswordInput.Visibility =
            global::System.Windows.Visibility
                .Collapsed;

        ConfirmPasswordInput.Visibility =
            global::System.Windows.Visibility
                .Visible;

        VisibleConfirmPasswordInput.Visibility =
            global::System.Windows.Visibility
                .Collapsed;
    }

    private void OnRequestClose(
        bool? dialogResult)
    {
        DialogResult =
            dialogResult;
    }

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
    {
        _viewModel.RequestClose -=
            OnRequestClose;

        Loaded -=
            OnWindowLoaded;

        Closed -=
            OnWindowClosed;
    }
}