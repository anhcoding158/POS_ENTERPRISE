using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using POS.Wpf;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class EmployeeAccountSuccessDialogUiTests
{
    [Fact]
    public async Task Success_dialog_materializes_without_password_and_exposes_safe_modal_contract()
    {
        await RunOnStaAsync(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var application = new App();
                application.InitializeComponent();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            var dialog = new EmployeeAccountSuccessWindow(new EmployeeAccountSuccessEventArgs(
                "Tạo tài khoản thành công", "admin5", "Nguyễn Văn C"));
            try
            {
                dialog.Show();
                dialog.Measure(new Size(600, 500));
                dialog.Arrange(new Rect(0, 0, 600, 500));
                dialog.UpdateLayout();
                Assert.Equal("EmployeeAccountSuccessWindow", AutomationProperties.GetAutomationId(dialog));
                Assert.Equal(WindowStartupLocation.CenterOwner, dialog.WindowStartupLocation);
                Assert.Equal("Tạo tài khoản thành công", dialog.Title);
                var text = string.Join("|", FindVisualDescendants<TextBlock>(dialog).Select(block => block.Text));
                Assert.Contains("admin5", text, StringComparison.Ordinal);
                Assert.Contains("Nguyễn Văn C", text, StringComparison.Ordinal);
                Assert.Contains("Chờ nhân viên đổi mật khẩu lần đầu", text, StringComparison.Ordinal);
                Assert.DoesNotContain("Temp123!", text, StringComparison.Ordinal);
                var acknowledge = FindVisualDescendants<Button>(dialog).Single(button => AutomationProperties.GetAutomationId(button) == "EmployeeAccountSuccessAcknowledgeButton");
                Assert.True(acknowledge.IsDefault);
                Assert.True(acknowledge.IsCancel);
                Assert.Equal("Đã hiểu", acknowledge.Content?.ToString());
            }
            finally
            {
                dialog.Close();
            }
        });
    }

    [Fact]
    public async Task Employee_create_success_dialog_uses_persisted_identity_without_account_secret_copy()
    {
        await RunOnStaAsync(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var application = new App();
                application.InitializeComponent();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            var dialog = new EmployeeAccountSuccessWindow(new EmployeeAccountSuccessEventArgs(
                "Tạo nhân viên thành công", string.Empty, "Nguyễn Văn D", "EMP-0006", "Đang làm việc"));
            try
            {
                dialog.Show();
                dialog.Measure(new Size(600, 500));
                dialog.Arrange(new Rect(0, 0, 600, 500));
                dialog.UpdateLayout();
                var text = string.Join("|", FindVisualDescendants<TextBlock>(dialog).Where(block => block.IsVisible).Select(block => block.Text));
                Assert.Contains("Hồ sơ Nguyễn Văn D đã được tạo.", text, StringComparison.Ordinal);
                Assert.Contains("EMP-0006", text, StringComparison.Ordinal);
                Assert.Contains("Đang làm việc", text, StringComparison.Ordinal);
                Assert.DoesNotContain("mật khẩu", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Temp123!", text, StringComparison.Ordinal);
            }
            finally
            {
                dialog.Close();
            }
        });
    }

    private static Task<object?> RunOnStaAsync(Action action)
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(null); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualDescendants<T>(child)) yield return descendant;
        }
    }
}
