using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using POS.Application.Common;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.StoreSetup;
using POS.Application.DTOs.Printing;

namespace POS.Infrastructure.Printing;

/// <summary>
/// Typed configuration ánh xạ từ section:
///
/// "Store"
///
/// Không khai báo WifiPassword hoặc bất kỳ secret nào vì
/// các giá trị đó không thuộc dữ liệu hóa đơn.
/// </summary>
public sealed class ReceiptStoreOptions
{
    public const string SectionName =
        "Store";

    public string Name { get; set; } =
        string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? TaxCode { get; set; }

    public string? FooterMessage { get; set; }

    /// <summary>
    /// Kiểm tra cấu hình và tạo snapshot bất biến.
    ///
    /// ReceiptStoreSnapshotDto là nguồn sự thật cho:
    /// - chuẩn hóa khoảng trắng;
    /// - giới hạn độ dài;
    /// - tên cửa hàng bắt buộc;
    /// - loại bỏ giá trị tùy chọn trống.
    /// </summary>
    public ReceiptStoreSnapshotDto CreateSnapshot()
    {
        return new ReceiptStoreSnapshotDto(
            name:
                Name,

            address:
                Address,

            phone:
                Phone,

            taxCode:
                TaxCode,

            footerMessage:
                FooterMessage);
    }

    public void Validate()
    {
        _ =
            CreateSnapshot();
    }
}

/// <summary>
/// Infrastructure implementation cung cấp snapshot cửa hàng
/// đã được kiểm tra từ typed configuration.
///
/// Snapshot được tạo một lần khi provider được khởi tạo.
/// Mọi receipt trong cùng phiên ứng dụng dùng cùng thông tin
/// cửa hàng ổn định, không bị thay đổi giữa lúc checkout.
/// </summary>
public sealed class ReceiptStoreSnapshotProvider :
    IReceiptStoreSnapshotProvider
{
    private readonly ReceiptStoreSnapshotDto? _legacySnapshot;
    private readonly IStoreSettingsStore? _settingsStore;
    private readonly IStoreSettingsLogoContentProvider? _logoContentProvider;
    private readonly ILogger<ReceiptStoreSnapshotProvider>? _logger;

    public ReceiptStoreSnapshotProvider(IStoreSettingsStore settingsStore)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _logger = null;
    }

    public ReceiptStoreSnapshotProvider(
        IStoreSettingsStore settingsStore,
        IStoreSettingsLogoContentProvider logoContentProvider)
        : this(settingsStore)
    {
        _logoContentProvider = logoContentProvider ?? throw new ArgumentNullException(nameof(logoContentProvider));
    }

    public ReceiptStoreSnapshotProvider(
        IStoreSettingsStore settingsStore,
        IStoreSettingsLogoContentProvider logoContentProvider,
        IOptions<ReceiptStoreOptions> legacyOptions,
        ILogger<ReceiptStoreSnapshotProvider>? logger = null)
        : this(settingsStore, logoContentProvider)
    {
        ArgumentNullException.ThrowIfNull(legacyOptions);
        var value = legacyOptions.Value ?? throw new InvalidOperationException("Không tìm thấy cấu hình Store.");
        value.Validate();
        _legacySnapshot = value.CreateSnapshot();
        _logger = logger;
    }

    public ReceiptStoreSnapshotProvider(
        IStoreSettingsStore settingsStore,
        IOptions<ReceiptStoreOptions> legacyOptions)
        : this(settingsStore)
    {
        ArgumentNullException.ThrowIfNull(legacyOptions);
        var value = legacyOptions.Value ?? throw new InvalidOperationException("Không tìm thấy cấu hình Store.");
        value.Validate();
        _legacySnapshot = value.CreateSnapshot();
    }

    public ReceiptStoreSnapshotProvider(
        IOptions<ReceiptStoreOptions> options)
    {
        _logger = null;
        ArgumentNullException.ThrowIfNull(
            options);

        var value =
            options.Value ??
            throw new InvalidOperationException(
                "Không tìm thấy cấu hình Store.");

        try
        {
            value.Validate();

            _legacySnapshot = value.CreateSnapshot();
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "Cấu hình Store không hợp lệ.",
                exception);
        }
    }

    public ReceiptStoreSnapshotDto
        GetCurrentSnapshot()
    {
        /*
         * ReceiptStoreSnapshotDto là immutable,
         * nên có thể chia sẻ an toàn trong toàn bộ ứng dụng.
         */
        if (_settingsStore is not null)
        {
            var settings = _settingsStore.Current;
            var configuredLogo =
                !string.IsNullOrWhiteSpace(settings.LogoAssetName);

            if (string.IsNullOrWhiteSpace(settings.StoreName))
            {
                LogSnapshot(
                    configuredLogo,
                    managedLogoResolved: false,
                    embeddedLogoByteCount: 0,
                    fallbackReason: "StoreNotConfigured");
                return _legacySnapshot ?? ReceiptStoreSnapshotDto.Unconfigured;
            }

            var logo =
                _logoContentProvider?.TryRead(
                    settings.LogoAssetName);

            LogSnapshot(
                configuredLogo,
                managedLogoResolved: logo is not null,
                embeddedLogoByteCount: logo?.Bytes.Count ?? 0,
                fallbackReason:
                    !configuredLogo
                        ? "NoConfiguredLogo"
                        : _logoContentProvider is null
                            ? "LogoProviderUnavailable"
                            : logo is null
                                ? "ManagedLogoUnavailableOrInvalid"
                                : "None");

            return new ReceiptStoreSnapshotDto(
                settings.StoreName,
                settings.Address,
                settings.Hotline,
                settings.TaxCode,
                logoBytes:
                    logo?.Bytes,
                logoMimeType:
                    logo?.MimeType);
        }
        return _legacySnapshot ?? ReceiptStoreSnapshotDto.Unconfigured;
    }

    private void LogSnapshot(
        bool configuredLogo,
        bool managedLogoResolved,
        int embeddedLogoByteCount,
        string fallbackReason)
    {
        if (_logger is null)
        {
            return;
        }

        PosLog.Information(
            _logger,
            "ReceiptSnapshot.Store: " +
            "ConfiguredLogoAssetNamePresent={ConfiguredLogoAssetNamePresent}; " +
            "ManagedLogoResolved={ManagedLogoResolved}; " +
            "EmbeddedLogoByteCount={EmbeddedLogoByteCount}; " +
            "FallbackReason={FallbackReason}",
            configuredLogo,
            managedLogoResolved,
            embeddedLogoByteCount,
            fallbackReason);
    }
}
