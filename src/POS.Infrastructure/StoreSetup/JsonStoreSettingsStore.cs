using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.StoreSetup;
using POS.Infrastructure.Payments;
using POS.Infrastructure.Printing;

namespace POS.Infrastructure.StoreSetup;

public sealed class JsonStoreSettingsStore : IStoreSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly StoreSettingsPathProvider _paths;
    private readonly IStoreSettingsValidator _validator;
    private readonly StoreSettingsSnapshot _fallbackDefaults;
    private readonly object _gate = new();
    private StoreSettingsSnapshot _current;

    public JsonStoreSettingsStore(
        StoreSettingsPathProvider paths,
        IStoreSettingsValidator validator,
        IOptions<ReceiptStoreOptions>? storeOptions = null,
        IOptions<VietQrOptions>? vietQrOptions = null,
        IOptions<ReceiptPrinterOptions>? printerOptions = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _fallbackDefaults = CreateFallbackDefaults(
            paths,
            ReadOptions(storeOptions),
            ReadOptions(vietQrOptions),
            ReadOptions(printerOptions));
        _current = _fallbackDefaults;
        LoadExisting();
    }

    public StoreSettingsSnapshot Current { get { lock (_gate) return _current; } }

    public async Task<StoreSettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_paths.SettingsPath))
            return new(Current, Array.Empty<StoreSettingsIssue>(), false);
        try
        {
            await using var stream = new FileStream(_paths.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var value = await JsonSerializer.DeserializeAsync<StoreSettingsSnapshot>(stream, JsonOptions, cancellationToken);
            if (value is null) return Recovery("StoreSettings.Corrupt", "Cấu hình cửa hàng bị hỏng; hệ thống dùng cấu hình an toàn mặc định.");
            if (value.SchemaVersion != StoreSettingsSnapshot.CurrentSchemaVersion)
                return Recovery("StoreSettings.UnsupportedVersion", "Phiên bản cấu hình cửa hàng không được hỗ trợ.");
            var validation = _validator.Validate(value);
            lock (_gate) _current = value;
            return new(value, validation.Issues, false);
        }
        catch (OperationCanceledException) { throw; }
        catch (JsonException) { return Recovery("StoreSettings.Corrupt", "Cấu hình cửa hàng bị hỏng; hệ thống dùng cấu hình an toàn mặc định."); }
        catch (IOException) { return Recovery("StoreSettings.Unavailable", "Không thể đọc cấu hình cửa hàng."); }
        catch (UnauthorizedAccessException) { return Recovery("StoreSettings.Unavailable", "Không có quyền đọc cấu hình cửa hàng."); }
    }

    public async Task<StoreSettingsSaveResult> SaveAsync(StoreSettingsSnapshot settings, long expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        var validation = _validator.Validate(settings);
        if (!validation.IsValid) return new(StoreSettingsSaveStatus.ValidationFailed, settings, validation.Issues);
        lock (_gate)
        {
            if (_current.Version != expectedVersion) return new(StoreSettingsSaveStatus.Conflict, _current);
        }
        var disk = await ReadVersionAsync(cancellationToken);
        if (disk != expectedVersion) return new(StoreSettingsSaveStatus.Conflict, Current);
        var committed = settings with { SchemaVersion = StoreSettingsSnapshot.CurrentSchemaVersion, Version = checked(expectedVersion + 1) };
        Directory.CreateDirectory(_paths.Root);
        EnsureManagedRoot();
        var tempPath = Path.Combine(_paths.Root, $".{StoreSettingsPathProvider.SettingsFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, committed, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(_paths.SettingsPath))
            {
                var attrs = File.GetAttributes(_paths.SettingsPath);
                if ((attrs & FileAttributes.ReparsePoint) != 0) throw new IOException("Tệp cấu hình không an toàn.");
                File.Replace(tempPath, _paths.SettingsPath, null, true);
            }
            else File.Move(tempPath, _paths.SettingsPath, false);
            lock (_gate) _current = committed;
            return new(StoreSettingsSaveStatus.Success, committed);
        }
        catch (OperationCanceledException) { return new(StoreSettingsSaveStatus.Cancelled, Current); }
        catch (IOException) { return new(StoreSettingsSaveStatus.Failed, Current); }
        catch (UnauthorizedAccessException) { return new(StoreSettingsSaveStatus.Failed, Current); }
        finally { try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { } }
    }

    private void LoadExisting()
    {
        if (!File.Exists(_paths.SettingsPath)) return;
        try
        {
            var value = JsonSerializer.Deserialize<StoreSettingsSnapshot>(File.ReadAllText(_paths.SettingsPath), JsonOptions);
            if (value is not null && value.SchemaVersion == StoreSettingsSnapshot.CurrentSchemaVersion) lock (_gate) _current = value;
        }
        catch (IOException) { }
        catch (JsonException) { }
        catch (UnauthorizedAccessException) { }
    }

    private async Task<long> ReadVersionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.SettingsPath)) return 0;
        try
        {
            await using var stream = new FileStream(_paths.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var value = await JsonSerializer.DeserializeAsync<StoreSettingsSnapshot>(stream, JsonOptions, cancellationToken);
            return value?.SchemaVersion == StoreSettingsSnapshot.CurrentSchemaVersion ? value.Version : -1;
        }
        catch (JsonException) { return -1; }
        catch (IOException) { return -1; }
    }

    private StoreSettingsLoadResult Recovery(string code, string message)
    {
        var issue = new StoreSettingsIssue(code, "StoreSettings", message);
        lock (_gate) _current = _fallbackDefaults;
        return new(_fallbackDefaults, new[] { issue }, true);
    }

    private static StoreSettingsSnapshot CreateFallbackDefaults(
        StoreSettingsPathProvider paths,
        ReceiptStoreOptions? store,
        VietQrOptions? qr,
        ReceiptPrinterOptions? printer)
    {
        var defaults = StoreSettingsDefaults.Create(paths.EffectiveDatabaseDirectory, paths.DefaultBackupDirectory);
        if (store is not null && !string.IsNullOrWhiteSpace(store.Name))
        {
            defaults = defaults with
            {
                StoreName = store.Name.Trim(),
                Address = NormalizeOptional(store.Address),
                Hotline = NormalizeOptional(store.Phone),
                TaxCode = NormalizeOptional(store.TaxCode)
            };
        }

        if (qr is not null)
        {
            defaults = defaults with
            {
                VietQrEnabled = qr.EnableVietQr,
                BankBin = NormalizeOptional(qr.BankBin),
                BankAccountNumber = NormalizeOptional(qr.AccountNumber),
                BankAccountName = NormalizeOptional(qr.AccountName),
                VietQrContent = NormalizeOptional(qr.TransferContentPrefix)
            };
        }

        if (printer is not null && !string.IsNullOrWhiteSpace(printer.PrinterName))
        {
            defaults = defaults with { DefaultPrinter = printer.PrinterName.Trim() };
        }

        return defaults;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static T? ReadOptions<T>(IOptions<T>? options) where T : class
    {
        try { return options?.Value; }
        catch (OptionsValidationException) { return null; }
    }

    private void EnsureManagedRoot()
    {
        var info = new DirectoryInfo(_paths.Root);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Thư mục cấu hình không an toàn.");
    }
}
