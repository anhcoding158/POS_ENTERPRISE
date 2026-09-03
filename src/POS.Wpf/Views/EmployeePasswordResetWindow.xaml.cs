namespace POS.Wpf.Views;

public partial class EmployeePasswordResetWindow : global::System.Windows.Window
{
    public EmployeePasswordResetWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            TemporaryPasswordInput.Clear();
            ConfirmPasswordInput.Clear();
        };
    }

    public string TemporaryPassword { get; private set; } = string.Empty;

    private void OnPasswordChanged(object sender, global::System.Windows.RoutedEventArgs e)
    {
        ValidationText.Visibility = global::System.Windows.Visibility.Collapsed;
    }

    private void OnConfirmClick(object sender, global::System.Windows.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TemporaryPasswordInput.Password))
        {
            ShowValidation("Vui lòng nhập mật khẩu tạm thời.");
            return;
        }

        if (!string.Equals(TemporaryPasswordInput.Password, ConfirmPasswordInput.Password, StringComparison.Ordinal))
        {
            ShowValidation("Mật khẩu xác nhận không khớp.");
            return;
        }

        TemporaryPassword = TemporaryPasswordInput.Password;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, global::System.Windows.RoutedEventArgs e) => DialogResult = false;

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.Visibility = global::System.Windows.Visibility.Visible;
    }
}
