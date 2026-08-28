using POS.Application.Abstractions.StoreSetup;
using POS.Application.Validation;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.StoreSetup;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ManualUxHotfixTests
{
    [Fact]
    public async Task Missing_backup_directory_is_a_warning_and_does_not_block_sales_readiness()
    {
        var root = Path.Combine(Path.GetTempPath(), "POS-Enterprise-Readiness-" + Guid.NewGuid().ToString("N"));
        var databaseDirectory = Path.Combine(root, "database");
        var backupDirectory = Path.Combine(root, "backup-not-created");
        Directory.CreateDirectory(databaseDirectory);
        try
        {
            var paths = new StoreSettingsPathProvider(
                DatabaseRuntimeGuard.IsolatedTestMode,
                Path.Combine(databaseDirectory, "pos-enterprise-isolated.db"),
                AppContext.BaseDirectory);
            var evaluator = new StoreSettingsReadinessEvaluator(new StoreSettingsValidator(), paths);
            var readiness = await evaluator.EvaluateAsync(new StoreSettingsSnapshot
            {
                StoreName = "Cua hang test",
                TaxCode = "invalid-tax-code",
                DatabaseDirectory = databaseDirectory,
                BackupDirectory = backupDirectory,
                TimeZoneId = "SE Asia Standard Time"
            });

            Assert.True(readiness.IsReady);
            Assert.Contains(readiness.Warnings, issue => issue.Code == "BackupDirectory.Missing");
            Assert.Contains(readiness.Warnings, issue => issue.Code == "TaxCode.Invalid");
            Assert.Empty(readiness.Errors);
        }
        finally
        {
            DeleteOwnedTemporaryDirectory(root);
        }
    }

    [Fact]
    public void Readiness_dialog_constructs_real_wpf_resources_and_exposes_actionable_controls()
    {
        RunOnSta(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var app = new POS.Wpf.App();
                app.InitializeComponent();
            }

            var dialog = new StoreReadinessDialogWindow([
                new StoreSettingsIssue("StoreName.Required", "StoreName", "Tên cửa hàng là bắt buộc.")
            ]);

            Assert.Same(dialog, dialog.DataContext);
            Assert.NotNull(dialog.FindName("StoreReadinessOpenSettingsButton"));
            Assert.NotNull(dialog.FindName("StoreReadinessCloseButton"));
            Assert.Single(dialog.Issues);
            dialog.StoreReadinessOpenSettingsButton.RaiseEvent(
                new global::System.Windows.RoutedEventArgs(
                    global::System.Windows.Controls.Button.ClickEvent));
            Assert.True(dialog.OpenSettingsRequested);
            dialog.Close();
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static void DeleteOwnedTemporaryDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Temporary readiness test escaped its boundary.");
        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, recursive: true);
    }
}
