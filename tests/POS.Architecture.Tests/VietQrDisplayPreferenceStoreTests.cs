using POS.Wpf.Services;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm tra cấu hình cách hiển thị VietQR
/// không phụ thuộc LocalApplicationData thật của máy chạy test.
/// </summary>
public sealed class VietQrDisplayPreferenceStoreTests :
    IDisposable
{
    private readonly string _temporaryDirectory;

    public VietQrDisplayPreferenceStoreTests()
    {
        _temporaryDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "POS-Enterprise-Tests",
                Guid.NewGuid()
                    .ToString("N"));
    }

    [Fact]
    public void Missing_file_returns_customer_display_default()
    {
        var store =
            CreateStore();

        var mode =
            store.Load();

        Assert.Equal(
            VietQrDisplayMode.CustomerDisplay,
            mode);
    }

    [Theory]
    [InlineData(
        VietQrDisplayMode.CustomerDisplay)]
    [InlineData(
        VietQrDisplayMode.CashierDisplay)]
    [InlineData(
        VietQrDisplayMode.PrintSlip)]
    [InlineData(
        VietQrDisplayMode.AskEveryTime)]
    public void Saved_mode_can_be_loaded_again(
        VietQrDisplayMode expectedMode)
    {
        var store =
            CreateStore();

        store.Save(
            expectedMode);

        var reloadedStore =
            CreateStore();

        Assert.Equal(
            expectedMode,
            reloadedStore.Load());
    }

    [Fact]
    public void Invalid_json_falls_back_to_default_mode()
    {
        var store =
            CreateStore();

        Directory.CreateDirectory(
            _temporaryDirectory);

        File.WriteAllText(
            store.FilePath,
            "{not-valid-json");

        Assert.Equal(
            VietQrDisplayMode.CustomerDisplay,
            store.Load());
    }

    [Fact]
    public void Unknown_mode_falls_back_to_default_mode()
    {
        var store =
            CreateStore();

        Directory.CreateDirectory(
            _temporaryDirectory);

        File.WriteAllText(
            store.FilePath,
            """
            {
              "version": 1,
              "mode": "UnknownMode"
            }
            """);

        Assert.Equal(
            VietQrDisplayMode.CustomerDisplay,
            store.Load());
    }

    [Fact]
    public void Invalid_mode_is_rejected_when_saving()
    {
        var store =
            CreateStore();

        var invalidMode =
            (VietQrDisplayMode)999;

        Assert.Throws<
            ArgumentOutOfRangeException>(
                () =>
                    store.Save(
                        invalidMode));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(
                    _temporaryDirectory))
            {
                Directory.Delete(
                    _temporaryDirectory,
                    recursive:
                        true);
            }
        }
        catch
        {
            /*
             * Dọn thư mục test là best-effort.
             */
        }
    }

    private VietQrDisplayPreferenceStore
        CreateStore()
    {
        return new VietQrDisplayPreferenceStore(
            Path.Combine(
                _temporaryDirectory,
                "vietqr-display-settings.json"));
    }
}