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
        var viewModel = CreateViewModel(store.Current, new FakePrinters(new PrinterInfo("Receipt printer", true)), receipt);

        viewModel.StoreName = "Cua hang da cap nhat";
        viewModel.SaveCommand.Execute(null);
        await WaitForCommandAsync(viewModel.SaveCommand);

        Assert.False(viewModel.IsDirty);
        Assert.Contains("Đã lưu cài đặt cửa hàng", viewModel.StatusMessage, StringComparison.Ordinal);
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
        IReceiptService? receipt = null)
    {
        var fakeStore = new FakeStore(settings);
        return new StoreSettingsViewModel(
            fakeStore,
            new FakeValidator(),
            new FakeReadiness(),
            new FakeLogos(),
            printers ?? new FakePrinters(new PrinterInfo("Receipt printer", true)),
            new FakeQr(),
            new FakePicker(),
            receipt);
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
        public Task<StoreSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoreSettingsLoadResult(Current, [], false));
        public Task<StoreSettingsSaveResult> SaveAsync(StoreSettingsSnapshot settings, long expectedVersion, CancellationToken cancellationToken = default)
        {
            Current = settings with { Version = expectedVersion + 1 };
            return Task.FromResult(new StoreSettingsSaveResult(StoreSettingsSaveStatus.Success, Current));
        }
    }

    private sealed class FakeValidator : IStoreSettingsValidator
    {
        public StoreSettingsValidationResult Validate(StoreSettingsSnapshot settings) =>
            new([]);
    }

    private sealed class FakeReadiness : IStoreSettingsReadinessEvaluator
    {
        public Task<StoreSettingsReadiness> EvaluateAsync(StoreSettingsSnapshot settings, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StoreSettingsReadiness([], true));
    }

    private sealed class FakeLogos : IStoreSettingsLogoService
    {
        public Task<string> ImportAsync(string sourcePath, CancellationToken cancellationToken = default) => Task.FromResult("logo.png");
        public Task RemoveAsync(string? assetName, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
        public string? PickLogo() => null;
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
}

internal static class StoreSetupTestExtensions
{
    public static async Task RefreshPrintersAsyncForTest(this StoreSettingsViewModel viewModel)
    {
        viewModel.RefreshPrintersCommand.Execute(null);
        await Task.Delay(50);
    }
}
