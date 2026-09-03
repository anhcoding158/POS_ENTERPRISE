using System.Windows;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.StoreSetup;
using POS.Application.Common;
using POS.Application.DTOs.Printing;
using POS.Domain.Enums;
using POS.Wpf;
using POS.Wpf.Services;
using POS.Wpf.ViewModels;
using POS.Wpf.Views;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class StoreSetupShellUxTests
{
    [Fact]
    public async Task Store_setup_uses_business_language_and_safe_region_fallback()
    {
        var viewModel = CreateViewModel(new StoreSettingsSnapshot
        {
            StoreName = "Cua hang mau",
            TimeZoneId = "legacy-invalid-time-zone"
        });

        Assert.Equal("Việt Nam đồng (VND)", viewModel.CurrencyDisplay);
        Assert.Equal("Việt Nam (UTC+7) — dùng mặc định an toàn", viewModel.TimeZoneDisplay);
        Assert.Equal("Thiết lập khu vực: Việt Nam • VND • UTC+7", viewModel.RegionalSettingsText);
        Assert.DoesNotContain("VietnameseDong", viewModel.CurrencyDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("SE Asia Standard Time", viewModel.TimeZoneDisplay, StringComparison.Ordinal);
        await viewModel.InitializeAsync();
        Assert.Contains("Đã tìm thấy 1 máy in", viewModel.PrinterDiscoveryMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Printer_refresh_preserves_selection_and_warns_when_missing()
    {
        var store = new FakeStore(new StoreSettingsSnapshot
        {
            StoreName = "Cua hang mau",
            DefaultPrinter = "Receipt printer"
        });
        var printers = new FakePrinters(new PrinterInfo("Receipt printer", true));
        var viewModel = CreateViewModel(store.Current, printers);

        await viewModel.InitializeAsync();
        Assert.Equal("Receipt printer", viewModel.DefaultPrinter);
        Assert.Empty(viewModel.PrinterSelectionWarning);

        printers.Items.Clear();
        printers.Items.Add(new PrinterInfo("Other printer", false));
        await viewModel.RefreshPrintersAsyncForTest();

        Assert.Empty(viewModel.DefaultPrinter);
        Assert.Contains("không còn", viewModel.PrinterSelectionWarning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Save_preserves_hidden_compatibility_settings_and_print_test_uses_real_receipt_service()
    {
        var store = new FakeStore(new StoreSettingsSnapshot
        {
            StoreName = "Cua hang mau",
            DefaultPrinter = "Receipt printer",
            VietQrEnabled = true,
            BankBin = "970415",
            BankAccountNumber = "123456789",
            BankAccountName = "Cua hang mau",
            VietQrContent = "Thanh toan",
            CashDrawer = CashDrawerMode.PrinterPulse
        });
        var receipt = new FakeReceiptService();
        var viewModel = CreateViewModel(store, new FakePrinters(new PrinterInfo("Receipt printer", true)), receipt);

        viewModel.StoreName = "Cua hang da cap nhat";
        viewModel.SaveCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SaveCommand);

        Assert.False(viewModel.IsDirty);
        Assert.Empty(viewModel.StatusMessage);
        Assert.True(store.Current.VietQrEnabled);
        Assert.Equal("970415", store.Current.BankBin);
        Assert.Equal(CashDrawerMode.PrinterPulse, store.Current.CashDrawer);

        viewModel.PrintTestReceiptCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PrintTestReceiptCommand);
        Assert.NotNull(receipt.Request);
        Assert.Contains(receipt.Request!.Lines, line => line.ProductName == "Phiếu thử máy in");
        Assert.Contains("Đã gửi phiếu thử K80", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Print_test_receipt_uses_the_current_snapshot_logo_provider()
    {
        var receipt = new FakeReceiptService();
        var viewModel = CreateViewModel(
            new FakeStore(new StoreSettingsSnapshot
            {
                StoreName = "MiniMart",
                DefaultPrinter = "Receipt printer"
            }),
            receipt: receipt,
            receiptStoreSnapshotProvider: new FakeReceiptStoreSnapshotProvider(
                new ReceiptStoreSnapshotDto(
                    "MiniMart",
                    logoBytes: Convert.FromBase64String(
                        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="),
                    logoMimeType: "image/png")));


        viewModel.PrintTestReceiptCommand.Execute(null);
        await WaitForCommandAsync(viewModel.PrintTestReceiptCommand);

        Assert.NotNull(receipt.Request);
        Assert.True(receipt.Request!.Store.HasLogo);
    }

    [Fact]
    public async Task Successful_save_raises_one_success_request_and_removes_inline_success_feedback()
    {
        var store = new FakeStore(new StoreSettingsSnapshot { StoreName = "Cua hang mau" });
        var viewModel = CreateViewModel(store);
        var successCount = 0;
        viewModel.SaveSucceeded += (_, _) => successCount++;
        viewModel.StoreName = "Cua hang da cap nhat";

        viewModel.SaveCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SaveCommand);

        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(1, successCount);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanSave);
        Assert.Empty(viewModel.StatusMessage);
        Assert.Empty(viewModel.SaveStateText);
        Assert.Equal("Đã lưu", viewModel.DirtyStateText);
    }

    [Fact]
    public async Task Save_without_changes_is_not_executed_and_does_not_raise_success()
    {
        var store = new FakeStore(new StoreSettingsSnapshot { StoreName = "Cua hang mau" });
        var viewModel = CreateViewModel(store);
        var successCount = 0;
        viewModel.SaveSucceeded += (_, _) => successCount++;

        Assert.False(viewModel.SaveCommand.CanExecute(null));
        viewModel.SaveCommand.Execute(null);
        await Task.Delay(20);

        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(0, successCount);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public async Task Validation_failure_does_not_raise_success_and_keeps_draft_dirty()
    {
        var store = new FakeStore(new StoreSettingsSnapshot { StoreName = "Cua hang mau" });
        var viewModel = CreateViewModel(store, readiness: new FakeReadiness(false));
        var successCount = 0;
        viewModel.SaveSucceeded += (_, _) => successCount++;
        viewModel.StoreName = "Cua hang dang sua";

        viewModel.SaveCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SaveCommand);

        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(0, successCount);
        Assert.True(viewModel.IsDirty);
        Assert.Contains("Kiểm tra lại", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Selecting_new_logo_makes_draft_dirty_and_enables_save()
    {
        var store = new FakeStore(new StoreSettingsSnapshot
        {
            StoreName = "Cua hang mau"
        });
        var logos = new FakeLogos { ImportedAssetName = "logo-mini.png" };
        var picker = new FakePicker { NextLogoPath = "C:\\fixture\\mini.png" };
        var viewModel = CreateViewModel(store, logos: logos, picker: picker);

        viewModel.ReplaceLogoCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ReplaceLogoCommand);

        Assert.Equal("logo-mini.png", viewModel.LogoAssetName);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.Contains("Bấm", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_pending_logo_persists_it_and_resets_dirty_state()
    {
        var store = new FakeStore(new StoreSettingsSnapshot
        {
            StoreName = "Cua hang mau"
        });
        var logos = new FakeLogos { ImportedAssetName = "logo-mini.png" };
        var picker = new FakePicker { NextLogoPath = "C:\\fixture\\mini.png" };
        var viewModel = CreateViewModel(store, logos: logos, picker: picker);

        viewModel.ReplaceLogoCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ReplaceLogoCommand);
        viewModel.SaveCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SaveCommand);

        Assert.Equal("logo-mini.png", store.Current.LogoAssetName);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public async Task Selecting_same_logo_content_is_a_truthful_no_op()
    {
        var store = new FakeStore(new StoreSettingsSnapshot
        {
            StoreName = "Cua hang mau",
            LogoAssetName = "logo-a.png"
        });
        var logos = new FakeLogos { SameContent = true };
        var picker = new FakePicker { NextLogoPath = "C:\\fixture\\logo-a.png" };
        var viewModel = CreateViewModel(store, logos: logos, picker: picker);

        viewModel.ReplaceLogoCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ReplaceLogoCommand);

        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.Equal(
            "Logo này đang được sử dụng. Không có thay đổi cần lưu.",
            viewModel.StatusMessage);
        Assert.Equal(0, logos.ImportCallCount);
    }

    [Fact]
    public async Task Different_logo_content_is_dirty_even_when_source_name_is_similar()
    {
        var store = new FakeStore(new StoreSettingsSnapshot
        {
            StoreName = "Cua hang mau",
            LogoAssetName = "logo-a.png"
        });
        var logos = new FakeLogos { ImportedAssetName = "logo-b.png" };
        var picker = new FakePicker { NextLogoPath = "C:\\fixture\\logo-a.png" };
        var viewModel = CreateViewModel(store, logos: logos, picker: picker);

        viewModel.ReplaceLogoCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ReplaceLogoCommand);

        Assert.Equal("logo-b.png", viewModel.LogoAssetName);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task Removing_logo_enables_save_and_reset_restores_baseline_without_removing_it()
    {
        var store = new FakeStore(new StoreSettingsSnapshot
        {
            StoreName = "Cua hang mau",
            LogoAssetName = "logo-a.png"
        });
        var logos = new FakeLogos();
        var viewModel = CreateViewModel(store, logos: logos);

        viewModel.RemoveLogoCommand.Execute(null);
        await WaitForCommandAsync(viewModel.RemoveLogoCommand);

        Assert.Null(viewModel.LogoAssetName);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.SaveCommand.CanExecute(null));

        viewModel.ResetCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ResetCommand);

        Assert.Equal("logo-a.png", viewModel.LogoAssetName);
        Assert.False(viewModel.IsDirty);
        Assert.DoesNotContain("logo-a.png", logos.RemovedAssets);
    }

    [Fact]
    public async Task Reset_removes_pending_replacement_but_keeps_saved_logo()
    {
        var store = new FakeStore(new StoreSettingsSnapshot
        {
            StoreName = "Cua hang mau",
            LogoAssetName = "logo-a.png"
        });
        var logos = new FakeLogos { ImportedAssetName = "logo-b.png" };
        var picker = new FakePicker { NextLogoPath = "C:\\fixture\\logo-b.png" };
        var viewModel = CreateViewModel(store, logos: logos, picker: picker);

        viewModel.ReplaceLogoCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ReplaceLogoCommand);
        viewModel.ResetCommand.Execute(null);
        await WaitForCommandAsync(viewModel.ResetCommand);

        Assert.Equal("logo-a.png", viewModel.LogoAssetName);
        Assert.Contains("logo-b.png", logos.RemovedAssets);
        Assert.DoesNotContain("logo-a.png", logos.RemovedAssets);
    }

    [Fact]
    public async Task Save_failure_does_not_raise_success_and_keeps_error_feedback()
    {
        var store = new FakeStore(new StoreSettingsSnapshot { StoreName = "Cua hang mau" })
        {
            SaveStatus = StoreSettingsSaveStatus.Failed
        };
        var viewModel = CreateViewModel(store);
        var successCount = 0;
        viewModel.SaveSucceeded += (_, _) => successCount++;
        viewModel.StoreName = "Cua hang dang sua";

        viewModel.SaveCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SaveCommand);

        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(0, successCount);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("Không thể lưu cài đặt cửa hàng.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Save_exception_does_not_raise_success_and_keeps_draft_dirty()
    {
        var store = new FakeStore(new StoreSettingsSnapshot { StoreName = "Cua hang mau" })
        {
            ThrowOnSave = true
        };
        var viewModel = CreateViewModel(store);
        var successCount = 0;
        viewModel.SaveSucceeded += (_, _) => successCount++;
        viewModel.StoreName = "Cua hang dang sua";

        viewModel.SaveCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SaveCommand);

        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(0, successCount);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("Không thể hoàn tất thao tác. Hãy thử lại.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Scanner_test_accepts_sales_normalized_input_and_supports_cancel_and_timeout()
    {
        var scanner = new ScannerTestViewModel();

        scanner.StartCommand.Execute(null);
        await Task.Delay(25);
        Assert.True(scanner.IsListening);
        Assert.True(scanner.ReceiveScan("  890123\r\n  "));
        Assert.False(scanner.IsListening);
        Assert.Equal("890123", scanner.LastBarcode);
        Assert.Contains("Máy quét hoạt động bình thường", scanner.StatusMessage, StringComparison.Ordinal);

        scanner.StartCommand.Execute(null);
        await Task.Delay(25);
        scanner.Timeout();
        Assert.False(scanner.IsListening);
        Assert.Contains("Không nhận được mã", scanner.StatusMessage, StringComparison.Ordinal);

        scanner.StartCommand.Execute(null);
        await Task.Delay(25);
        scanner.CancelCommand.Execute(null);
        await Task.Delay(25);
        Assert.False(scanner.IsListening);
        Assert.Contains("Đã hủy", scanner.StatusMessage, StringComparison.Ordinal);
        scanner.Dispose();
    }

    [Fact]
    public void Real_store_setup_view_loads_resources_and_binds_the_production_view_model()
    {
        RunOnSta(() =>
        {
            if (global::System.Windows.Application.Current is null)
            {
                var app = new App();
                app.InitializeComponent();
            }

            var viewModel = CreateViewModel(new StoreSettingsSnapshot
            {
                StoreName = "Cua hang mau",
                TimeZoneId = "SE Asia Standard Time"
            });
            var window = new StoreSettingsWindow(viewModel);

            Assert.Same(viewModel, window.DataContext);
            Assert.Equal("Đã lưu", viewModel.DirtyStateText);
            Assert.NotNull(window.FindName("StoreSetupScannerCapture"));
            window.Close();
        });
    }

    [Fact]
    public void Shell_source_contains_one_level_task_groups_without_dead_customer_entry()
    {
        var root = FindRepositoryRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "POS.Wpf", "Views", "ShellWindow.xaml"));

        foreach (var group in new[]
        {
            "ShellInventoryGroup", "ShellOrdersGroup", "ShellQrGroup",
            "ShellManagementGroup", "ShellDataSupportGroup"
        })
            Assert.Contains(group, shell, StringComparison.Ordinal);

        Assert.Contains("ShellSalesNavigationButton", shell, StringComparison.Ordinal);
        Assert.Contains("CanViewVietQr", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Khách hàng\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("VietnameseDong", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SE Asia Standard Time", shell, StringComparison.Ordinal);
    }

    private static StoreSettingsViewModel CreateViewModel(
        StoreSettingsSnapshot settings,
        FakePrinters? printers = null,
        IReceiptService? receipt = null,
        IReceiptStoreSnapshotProvider? receiptStoreSnapshotProvider = null)
    {
        return CreateViewModel(
            new FakeStore(settings),
            printers,
            receipt,
            receiptStoreSnapshotProvider: receiptStoreSnapshotProvider);
    }

    private static StoreSettingsViewModel CreateViewModel(
        FakeStore store,
        FakePrinters? printers = null,
        IReceiptService? receipt = null,
        FakeReadiness? readiness = null,
        FakeLogos? logos = null,
        FakePicker? picker = null,
        IReceiptStoreSnapshotProvider? receiptStoreSnapshotProvider = null)
    {
        return new StoreSettingsViewModel(
            store,
            new FakeValidator(),
            readiness ?? new FakeReadiness(),
            logos ?? new FakeLogos(),
            printers ?? new FakePrinters(new PrinterInfo("Receipt printer", true)),
            new FakeQr(),
            picker ?? new FakePicker(),
            receipt,
            receiptStoreSnapshotProvider);
    }

    private static async Task WaitForCommandAsync(POS.Wpf.Commands.AsyncRelayCommand command)
    {
        for (var attempt = 0; command.IsExecuting && attempt < 100; attempt++)
            await Task.Delay(10);
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

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "POS.Enterprise.slnx")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class FakeStore(StoreSettingsSnapshot initial) : IStoreSettingsStore
    {
        public StoreSettingsSnapshot Current { get; private set; } = initial;
        public int SaveCallCount { get; private set; }
        public StoreSettingsSaveStatus SaveStatus { get; set; } = StoreSettingsSaveStatus.Success;
        public bool ThrowOnSave { get; set; }
        public Task<StoreSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoreSettingsLoadResult(Current, [], false));
        public Task<StoreSettingsSaveResult> SaveAsync(StoreSettingsSnapshot settings, long expectedVersion, CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            if (ThrowOnSave)
                throw new InvalidOperationException("test save failure");

            if (SaveStatus != StoreSettingsSaveStatus.Success)
                return Task.FromResult(new StoreSettingsSaveResult(SaveStatus));

            Current = settings with { Version = expectedVersion + 1 };
            return Task.FromResult(new StoreSettingsSaveResult(StoreSettingsSaveStatus.Success, Current));
        }
    }

    private sealed class FakeValidator : IStoreSettingsValidator
    {
        public StoreSettingsValidationResult Validate(StoreSettingsSnapshot settings) =>
            new([]);
    }

    private sealed class FakeReadiness(bool isReady = true) : IStoreSettingsReadinessEvaluator
    {
        public bool IsReady { get; set; } = isReady;
        public Task<StoreSettingsReadiness> EvaluateAsync(StoreSettingsSnapshot settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoreSettingsReadiness([], IsReady));
    }

    private sealed class FakeLogos : IStoreSettingsLogoService
    {
        public string ImportedAssetName { get; set; } = "logo.png";
        public bool SameContent { get; set; }
        public int ImportCallCount { get; private set; }
        public List<string> RemovedAssets { get; } = [];
        public Task<string> ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            ImportCallCount++;
            return Task.FromResult(ImportedAssetName);
        }
        public Task<bool> IsSameContentAsync(string sourcePath, string? assetName, CancellationToken cancellationToken = default) => Task.FromResult(SameContent);
        public Task RemoveAsync(string? assetName, CancellationToken cancellationToken = default)
        {
            if (assetName is not null)
                RemovedAssets.Add(assetName);
            return Task.CompletedTask;
        }
        public string? GetManagedPath(string? assetName) => assetName;
    }

    private sealed class FakePrinters(params PrinterInfo[] initial) : IPrinterTestService
    {
        public List<PrinterInfo> Items { get; } = [.. initial];
        public IReadOnlyList<PrinterInfo> Discover() => Items;
        public Task<PrinterTestResult> TestAsync(string? printerName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PrinterTestResult(PrinterTestStatus.Available, "Đã kết nối máy in."));
    }

    private sealed class FakeQr : IStoreSettingsQrPreviewService
    {
        public Task<byte[]> GenerateAsync(StoreSettingsSnapshot settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());
    }

    private sealed class FakePicker : IStoreSettingsFilePicker
    {
        public string? NextLogoPath { get; set; }
        public string? PickLogo() => NextLogoPath;
    }

    private sealed class FakeReceiptService : IReceiptService
    {
        public ReceiptRequest? Request { get; private set; }

        public Task<Result> PrintAsync(ReceiptRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeReceiptStoreSnapshotProvider(
        ReceiptStoreSnapshotDto snapshot) : IReceiptStoreSnapshotProvider
    {
        public ReceiptStoreSnapshotDto GetCurrentSnapshot() => snapshot;
    }
}

internal static class StoreSetupTestExtensions
{
    public static async Task RefreshPrintersAsyncForTest(this StoreSettingsViewModel viewModel)
    {
        viewModel.RefreshPrintersCommand.Execute(null);
        await Task.Delay(50);
    }
}
