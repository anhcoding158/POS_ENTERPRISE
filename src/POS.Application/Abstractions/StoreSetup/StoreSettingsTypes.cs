using System.Collections.ObjectModel;

namespace POS.Application.Abstractions.StoreSetup;

public enum StoreCurrency
{
    VietnameseDong = 1
}

public enum ReceiptPaperSize
{
    K80 = 1
}

public enum ScannerMode
{
    Disabled = 1,
    KeyboardWedge = 2
}

public enum CashDrawerMode
{
    Disabled = 1,
    PrinterPulse = 2
}

public enum StoreSettingsApplyTiming
{
    Immediately = 1,
    NextOperation = 2,
    RestartRequired = 3,
    TestOnly = 4,
    ReadinessOnly = 5
}

public enum StoreSettingsIssueSeverity
{
    Error = 1,
    Warning = 2
}

public sealed record StoreSettingsIssue(
    string Code,
    string Field,
    string Message,
    StoreSettingsIssueSeverity Severity = StoreSettingsIssueSeverity.Error);

public sealed record StoreSettingsValidationResult(
    IReadOnlyList<StoreSettingsIssue> Issues)
{
    public bool IsValid => Issues.All(x => x.Severity != StoreSettingsIssueSeverity.Error);
    public IReadOnlyList<StoreSettingsIssue> Errors =>
        Issues.Where(x => x.Severity == StoreSettingsIssueSeverity.Error).ToArray();
}

public sealed record StoreSettingsReadiness(
    IReadOnlyList<StoreSettingsIssue> Issues,
    bool IsReady)
{
    public bool HasBlockingErrors => Issues.Any(x => x.Severity == StoreSettingsIssueSeverity.Error);
    public IReadOnlyList<StoreSettingsIssue> Errors =>
        Issues.Where(x => x.Severity == StoreSettingsIssueSeverity.Error).ToArray();
    public IReadOnlyList<StoreSettingsIssue> Warnings =>
        Issues.Where(x => x.Severity == StoreSettingsIssueSeverity.Warning).ToArray();
}

public sealed record StoreRetentionPolicy
{
    public int LatestCount { get; init; } = 7;
    public int WeeklyCount { get; init; } = 4;
    public int MonthlyCount { get; init; } = 3;
    public long MaximumTotalBytes { get; init; } = 2_147_483_648L;
}

/// <summary>
/// Immutable, application-facing Store Setup snapshot. It deliberately has
/// no EF, WPF, JSON or Infrastructure types.
/// </summary>
public sealed record StoreSettingsSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public long Version { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Hotline { get; init; }
    public string? TaxCode { get; init; }
    public string? LogoAssetName { get; init; }
    public StoreCurrency Currency { get; init; } = StoreCurrency.VietnameseDong;
    public string TimeZoneId { get; init; } = "SE Asia Standard Time";
    public ReceiptPaperSize PaperSize { get; init; } = ReceiptPaperSize.K80;
    public int PrintCopyCount { get; init; } = 1;
    public bool AutoPrint { get; init; }
    public string? DefaultPrinter { get; init; }
    public ScannerMode Scanner { get; init; } = ScannerMode.KeyboardWedge;
    public CashDrawerMode CashDrawer { get; init; } = CashDrawerMode.Disabled;
    public bool VietQrEnabled { get; init; }
    public string? BankBin { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? BankAccountName { get; init; }
    public string? VietQrContent { get; init; }
    public string DatabaseDirectory { get; init; } = string.Empty;
    public string BackupDirectory { get; init; } = string.Empty;
    public StoreRetentionPolicy Retention { get; init; } = new();

    public bool RequiresRestartComparedTo(StoreSettingsSnapshot other) =>
        !string.Equals(DatabaseDirectory, other.DatabaseDirectory, StringComparison.OrdinalIgnoreCase);
}

public static class StoreSettingsDefaults
{
    public static StoreSettingsSnapshot Create(
        string databaseDirectory = "",
        string backupDirectory = "") =>
        new()
        {
            SchemaVersion = StoreSettingsSnapshot.CurrentSchemaVersion,
            Version = 0,
            StoreName = string.Empty,
            Currency = StoreCurrency.VietnameseDong,
            TimeZoneId = "SE Asia Standard Time",
            PaperSize = ReceiptPaperSize.K80,
            PrintCopyCount = 1,
            AutoPrint = false,
            Scanner = ScannerMode.KeyboardWedge,
            CashDrawer = CashDrawerMode.Disabled,
            VietQrEnabled = false,
            DatabaseDirectory = databaseDirectory,
            BackupDirectory = backupDirectory,
            Retention = new StoreRetentionPolicy()
        };
}

public interface IStoreSettingsValidator
{
    StoreSettingsValidationResult Validate(StoreSettingsSnapshot settings);
}

public interface IStoreSettingsReadinessEvaluator
{
    Task<StoreSettingsReadiness> EvaluateAsync(
        StoreSettingsSnapshot settings,
        CancellationToken cancellationToken = default);
}

public sealed record StoreSettingsLoadResult(
    StoreSettingsSnapshot Settings,
    IReadOnlyList<StoreSettingsIssue> Issues,
    bool WasRecovered);

public enum StoreSettingsSaveStatus
{
    Success = 1,
    ValidationFailed = 2,
    Conflict = 3,
    UnsupportedVersion = 4,
    Failed = 5,
    Cancelled = 6
}

public sealed record StoreSettingsSaveResult(
    StoreSettingsSaveStatus Status,
    StoreSettingsSnapshot? Settings = null,
    IReadOnlyList<StoreSettingsIssue>? Issues = null)
{
    public bool IsSuccess => Status == StoreSettingsSaveStatus.Success;
}

public interface IStoreSettingsStore
{
    StoreSettingsSnapshot Current { get; }
    Task<StoreSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);
    Task<StoreSettingsSaveResult> SaveAsync(
        StoreSettingsSnapshot settings,
        long expectedVersion,
        CancellationToken cancellationToken = default);
}

public interface IStoreSettingsLogoService
{
    Task<string> ImportAsync(string sourcePath, CancellationToken cancellationToken = default);
    Task<bool> IsSameContentAsync(
        string sourcePath,
        string? assetName,
        CancellationToken cancellationToken = default);
    Task RemoveAsync(string? assetName, CancellationToken cancellationToken = default);
    string? GetManagedPath(string? assetName);
}

/// <summary>
/// Nội dung logo đã được đọc từ kho quản lý tại boundary tạo snapshot.
/// Không chứa đường dẫn file sống để receipt có thể tự chứa và bất biến.
/// </summary>
public sealed class StoreLogoContent
{
    public const int MaximumBytes = 512 * 1024;

    public StoreLogoContent(
        IEnumerable<byte> bytes,
        string mimeType)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var copy = bytes.ToArray();
        if (copy.Length is <= 0 or > MaximumBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytes),
                "Nội dung logo vượt giới hạn snapshot.");
        }

        if (string.IsNullOrWhiteSpace(mimeType))
        {
            throw new ArgumentException(
                "MIME type của logo không được để trống.",
                nameof(mimeType));
        }

        Bytes = Array.AsReadOnly(copy);
        MimeType = mimeType.Trim().ToLowerInvariant();
    }

    public IReadOnlyList<byte> Bytes { get; }

    public string MimeType { get; }
}

/// <summary>
/// Đọc logo đã quản lý thành nội dung bounded để nhúng vào receipt snapshot.
/// Việc đọc chỉ xảy ra khi tạo receipt mới, không phải lúc preview/reprint.
/// </summary>
public interface IStoreSettingsLogoContentProvider
{
    StoreLogoContent? TryRead(string? assetName);
}

public enum PrinterTestStatus
{
    Available = 1,
    NotConfigured = 2,
    Unavailable = 3,
    AccessDenied = 4,
    Cancelled = 5
}

public sealed record PrinterInfo(string Name, bool IsDefault);
public sealed record PrinterTestResult(PrinterTestStatus Status, string Message);

public interface IPrinterTestService
{
    IReadOnlyList<PrinterInfo> Discover();
    Task<PrinterTestResult> TestAsync(string? printerName, CancellationToken cancellationToken = default);
}

public interface IStoreSettingsQrPreviewService
{
    Task<byte[]> GenerateAsync(StoreSettingsSnapshot settings, CancellationToken cancellationToken = default);
}
