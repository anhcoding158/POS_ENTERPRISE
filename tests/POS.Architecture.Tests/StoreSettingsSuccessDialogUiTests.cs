using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using POS.Wpf;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class StoreSettingsSuccessDialogUiTests
{
    [Fact]
    public async Task Store_settings_success_dialog_has_safe_modal_contract_and_no_duplicate_status_copy()
    {
        await RunOnStaAsync(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var application = new App();
                application.InitializeComponent();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            var dialog = new SuccessDialogWindow(SuccessDialogRequest.StoreSettingsSaved());
            try
            {
                dialog.Show();
                dialog.Measure(new Size(600, 500));
                dialog.Arrange(new Rect(0, 0, 600, 500));
                dialog.UpdateLayout();

                Assert.Equal("SuccessDialogWindow", AutomationProperties.GetAutomationId(dialog));
                Assert.Equal(WindowStartupLocation.CenterOwner, dialog.WindowStartupLocation);
                Assert.Equal("Lưu cài đặt thành công", dialog.Title);
                var text = string.Join("|", FindVisualDescendants<TextBlock>(dialog).Select(block => block.Text));
                Assert.Contains("Cài đặt cửa hàng đã được lưu thành công.", text, StringComparison.Ordinal);
                Assert.Contains("Các thay đổi đã sẵn sàng sử dụng.", text, StringComparison.Ordinal);

                var acknowledge = FindVisualDescendants<Button>(dialog).Single(button =>
                    AutomationProperties.GetAutomationId(button) == "StoreSettingsSuccessAcknowledgeButton");
                Assert.True(acknowledge.IsDefault);
                Assert.True(acknowledge.IsCancel);
                Assert.Equal("Đã hiểu", acknowledge.Content?.ToString());
                Assert.Equal("Đã hiểu", AutomationProperties.GetName(acknowledge));
            }
            finally
            {
                dialog.Close();
            }
        });
    }

    private static Task RunOnStaAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
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
