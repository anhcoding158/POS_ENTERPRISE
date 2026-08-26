using POS.Application.Abstractions.StoreSetup;
using POS.Application.Validation;
using POS.Infrastructure.StoreSetup;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class StoreSettingsTests
{
    [Fact]
    public void Complete_settings_are_valid_and_equality_is_deterministic()
    {
        var root = CreateRoot();
        try
        {
            var settings = Complete(root);
            var validator = new StoreSettingsValidator();
            var first = validator.Validate(settings);
            var second = validator.Validate(settings with { });
            Assert.True(first.IsValid);
            Assert.Equal(first.IsValid, second.IsValid);
            Assert.Equal(first.Issues, second.Issues);
            Assert.Equal(settings, settings with { });
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("", "StoreName.Required")]
    [InlineData("   ", "StoreName.Required")]
    [InlineData("Cửa hàng\u0001", "StoreName.Invalid")]
    public void Store_name_validation_is_centralized(string name, string code)
    {
        var root = CreateRoot();
        try
        {
            var result = new StoreSettingsValidator().Validate(Complete(root) with { StoreName = name });
            Assert.Contains(result.Errors, issue => issue.Code == code);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Viet_qr_and_device_dependencies_are_validated()
    {
        var root = CreateRoot();
        try
        {
            var validator = new StoreSettingsValidator();
            var result = validator.Validate(Complete(root) with
            {
                VietQrEnabled = true,
                BankBin = "12",
                BankAccountNumber = "abc",
                AutoPrint = true,
                DefaultPrinter = "",
                CashDrawer = CashDrawerMode.PrinterPulse
            });
            Assert.Contains(result.Errors, x => x.Code == "VietQr.BankBin");
            Assert.Contains(result.Errors, x => x.Code == "VietQr.AccountNumber");
            Assert.Contains(result.Errors, x => x.Code == "Printer.Required");
            Assert.Contains(result.Errors, x => x.Code == "CashDrawer.PrinterRequired");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Json_store_round_trips_and_rejects_concurrent_version()
    {
        var root = CreateRoot();
        try
        {
            var db = Path.Combine(root, "db");
            var backup = Path.Combine(root, "backup");
            Directory.CreateDirectory(db); Directory.CreateDirectory(backup);
            var paths = new StoreSettingsPathProvider(DatabaseRuntimeMode(), Path.Combine(db, "pos.db"), AppContext.BaseDirectory);
            var store = new JsonStoreSettingsStore(paths, new StoreSettingsValidator());
            var settings = Complete(root) with { DatabaseDirectory = db, BackupDirectory = backup };
            var saved = await store.SaveAsync(settings, 0);
            Assert.True(saved.IsSuccess);
            Assert.Equal(1, store.Current.Version);
            var conflict = await store.SaveAsync(settings with { StoreName = "Cửa hàng khác" }, 0);
            Assert.Equal(StoreSettingsSaveStatus.Conflict, conflict.Status);
            var reloaded = new JsonStoreSettingsStore(paths, new StoreSettingsValidator());
            Assert.Equal("Cửa hàng mẫu", reloaded.Current.StoreName);
            Assert.Equal(1, reloaded.Current.Version);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Isolated_settings_path_is_inside_database_boundary_and_load_does_not_create_files()
    {
        var root = CreateRoot();
        try
        {
            var db = Path.Combine(root, "scenario", "database"); Directory.CreateDirectory(db);
            var paths = new StoreSettingsPathProvider(DatabaseRuntimeMode(), Path.Combine(db, "pos.db"), AppContext.BaseDirectory);
            var store = new JsonStoreSettingsStore(paths, new StoreSettingsValidator());
            var result = await store.LoadAsync();
            Assert.Equal(paths.Root, db, StringComparer.OrdinalIgnoreCase);
            Assert.False(File.Exists(paths.SettingsPath));
            Assert.False(result.WasRecovered);
        }
        finally { Directory.Delete(root, true); }
    }

    private static StoreSettingsSnapshot Complete(string root) => new()
    {
        StoreName = "Cửa hàng mẫu", Address = "Số 1\nĐường A", Hotline = "+84999888777", TaxCode = "0123456789", Currency = StoreCurrency.VietnameseDong,
        TimeZoneId = TimeZoneInfo.Local.Id, PaperSize = ReceiptPaperSize.K80, PrintCopyCount = 2, DefaultPrinter = "Printer", Scanner = ScannerMode.KeyboardWedge,
        CashDrawer = CashDrawerMode.Disabled, DatabaseDirectory = Path.Combine(root, "database"), BackupDirectory = Path.Combine(root, "backup"), Retention = new StoreRetentionPolicy()
    };
    private static string CreateRoot() { var root = Path.Combine(Path.GetTempPath(), "POS-R41-StoreSettings-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Directory.CreateDirectory(Path.Combine(root, "database")); Directory.CreateDirectory(Path.Combine(root, "backup")); return root; }
    private static string DatabaseRuntimeMode() => DatabaseRuntimeGuard.IsolatedTestMode;
}
