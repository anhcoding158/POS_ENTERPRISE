using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.StoreSetup;
using POS.Application.DTOs.Printing;
using POS.Domain.Enums;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public sealed class StoreSettingsViewModel : ViewModelBase
{
    private const string VietnameseTimeZoneId = "SE Asia Standard Time";

    private readonly IStoreSettingsStore _store;
    private readonly IStoreSettingsValidator _validator;
    private readonly IStoreSettingsReadinessEvaluator _readiness;
    private readonly IStoreSettingsLogoService _logos;
    private readonly IPrinterTestService _printers;
    private readonly IStoreSettingsQrPreviewService _qr;
    private readonly IStoreSettingsFilePicker _picker;
    private readonly IReceiptService? _receiptService;

    private StoreSettingsSnapshot _original;
    private bool _busy;
    private bool _saving;
    private bool _printerBusy;
    private bool _initialized;
    private string _storeName = string.Empty;
    private string _address = string.Empty;
    private string _hotline = string.Empty;
    private string _taxCode = string.Empty;
    private string _databaseDirectory = string.Empty;
    private string _backupDirectory = string.Empty;
    private string _defaultPrinter = string.Empty;
    private string _bankBin = string.Empty;
    private string _accountNumber = string.Empty;
    private string _accountName = string.Empty;
    private string _vietQrContent = string.Empty;
    private string _paperSize = "K80";
    private string _scannerMode = "KeyboardWedge";
    private string _cashDrawerMode = "Disabled";
    private string _currency = "VietnameseDong";
    private string _timeZoneId = VietnameseTimeZoneId;
    private int _copies = 1;
    private int _latest = 7;
    private int _weekly = 4;
    private int _monthly = 3;
    private bool _autoPrint;
    private bool _vietQrEnabled;
    private string? _logoAssetName;
    private string? _logoToRemove;
    private string _status = "Sẵn sàng chỉnh sửa cài đặt cửa hàng.";
    private string _printerDiscoveryMessage = "Bấm “Quét lại máy in” để xem máy in đang có.";
    private string _printerSelectionWarning = string.Empty;
    private IReadOnlyList<StoreSettingsIssue> _issues = Array.Empty<StoreSettingsIssue>();

    public StoreSettingsViewModel(
        IStoreSettingsStore store,
        IStoreSettingsValidator validator,
        IStoreSettingsReadinessEvaluator readiness,
        IStoreSettingsLogoService logos,
        IPrinterTestService printers,
        IStoreSettingsQrPreviewService qr,
        IStoreSettingsFilePicker picker,
        IReceiptService? receiptService = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
        _logos = logos ?? throw new ArgumentNullException(nameof(logos));
        _printers = printers ?? throw new ArgumentNullException(nameof(printers));
        _qr = qr ?? throw new ArgumentNullException(nameof(qr));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _receiptService = receiptService;
        ScannerTest = new ScannerTestViewModel();

        _original = store.Current;
        Apply(_original);

        SaveCommand = new AsyncRelayCommand(
            SaveAsync,
            () => CanSave,
            HandleException);
        ResetCommand = new AsyncRelayCommand(
            ResetAsync,
            () => !_busy,
            HandleException);
        TestQrCommand = new AsyncRelayCommand(
            TestQrAsync,
            () => !_busy,
            HandleException);
        TestPrinterCommand = new AsyncRelayCommand(
            TestPrinterAsync,
            () => !_busy,
            HandleException);
        PrintTestReceiptCommand = new AsyncRelayCommand(
            PrintTestReceiptAsync,
            () => CanPrintTest,
            HandleException);
        ReplaceLogoCommand = new AsyncRelayCommand(
            ReplaceLogoAsync,
            () => !_busy,
            HandleException);
        RemoveLogoCommand = new AsyncRelayCommand(
            RemoveLogoAsync,
            () => !_busy && !string.IsNullOrWhiteSpace(_logoAssetName),
            HandleException);
        RefreshPrintersCommand = new AsyncRelayCommand(
            RefreshPrintersAsync,
            () => !_busy,
            HandleException);

        RefreshValidation();
    }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ResetCommand { get; }
    public AsyncRelayCommand TestQrCommand { get; }
    public AsyncRelayCommand TestPrinterCommand { get; }
    public AsyncRelayCommand PrintTestReceiptCommand { get; }
    public AsyncRelayCommand ReplaceLogoCommand { get; }
    public AsyncRelayCommand RemoveLogoCommand { get; }
    public AsyncRelayCommand RefreshPrintersCommand { get; }

    public ObservableCollection<PrinterInfo> Printers { get; } = [];
    public IReadOnlyList<int> PrintCopyOptions { get; } = [1, 2, 3, 4, 5];
    public IReadOnlyList<int> BackupRetentionOptions { get; } =
        Enumerable.Range(1, 100).ToArray();
    public IReadOnlyList<int> WeeklyRetentionOptions { get; } =
        Enumerable.Range(0, 53).ToArray();
    public IReadOnlyList<int> MonthlyRetentionOptions { get; } =
        Enumerable.Range(0, 25).ToArray();
    public ScannerTestViewModel ScannerTest { get; }
    public BitmapImage? QrPreview { get; private set; }

    public string StoreName
    {
        get => _storeName;
        set => Change(ref _storeName, value);
    }

    public string Address
    {
        get => _address;
        set => Change(ref _address, value);
    }

    public string Hotline
    {
        get => _hotline;
        set => Change(ref _hotline, value);
    }

    public string TaxCode
    {
        get => _taxCode;
        set => Change(ref _taxCode, value);
    }

    public string StoreNameValidationMessage =>
        FieldValidationMessage("StoreName");

    public string HotlineValidationMessage =>
        FieldValidationMessage("Hotline");

    public string TaxCodeValidationMessage =>
        FieldValidationMessage("TaxCode");

    public string DatabaseDirectory
    {
        get => _databaseDirectory;
        set => Change(ref _databaseDirectory, value);
    }

    public string BackupDirectory
    {
        get => _backupDirectory;
        set => Change(ref _backupDirectory, value);
    }

    public string DefaultPrinter
    {
        get => _defaultPrinter;
        set => Change(ref _defaultPrinter, value);
    }

    public string BankBin
    {
        get => _bankBin;
        set => Change(ref _bankBin, value);
    }

    public string BankAccountNumber
    {
        get => _accountNumber;
        set => Change(ref _accountNumber, value);
    }

    public string BankAccountName
    {
        get => _accountName;
        set => Change(ref _accountName, value);
    }

    public string VietQrContent
    {
        get => _vietQrContent;
        set => Change(ref _vietQrContent, value);
    }

    public string PaperSize
    {
        get => _paperSize;
        set => Change(ref _paperSize, value);
    }

    public string ScannerMode
    {
        get => _scannerMode;
        set => Change(ref _scannerMode, value);
    }

    public string CashDrawerMode
    {
        get => _cashDrawerMode;
        set => Change(ref _cashDrawerMode, value);
    }

    public string Currency
    {
        get => _currency;
        set => Change(ref _currency, value);
    }

    public string TimeZoneId
    {
        get => _timeZoneId;
        set => Change(ref _timeZoneId, value);
    }

    public int PrintCopyCount
    {
        get => _copies;
        set => Change(ref _copies, value);
    }

    public int LatestRetention
    {
        get => _latest;
        set => Change(ref _latest, value);
    }

    public int WeeklyRetention
    {
        get => _weekly;
        set => Change(ref _weekly, value);
    }

    public int MonthlyRetention
    {
        get => _monthly;
        set => Change(ref _monthly, value);
    }

    public bool AutoPrint
    {
        get => _autoPrint;
        set => Change(ref _autoPrint, value);
    }

    public bool VietQrEnabled
    {
        get => _vietQrEnabled;
        set => Change(ref _vietQrEnabled, value);
    }

    public string? LogoAssetName
    {
        get => _logoAssetName;
        private set
        {
            if (!SetProperty(ref _logoAssetName, value))
                return;

            OnPropertyChanged(nameof(LogoPath));
            RemoveLogoCommand?.NotifyCanExecuteChanged();
        }
    }

    public string? LogoPath => _logos.GetManagedPath(_logoAssetName);

    public string CurrencyDisplay =>
        _original.Currency == StoreCurrency.VietnameseDong
            ? "Việt Nam đồng (VND)"
            : "Loại tiền tệ cửa hàng";

    public string TimeZoneDisplay =>
        HasInvalidLegacyTimeZone
            ? "Việt Nam (UTC+7) — dùng mặc định an toàn"
            : "Việt Nam (UTC+7) — tự động";

    public string RegionalSettingsText =>
        _original.Currency == StoreCurrency.VietnameseDong
            ? "Thiết lập khu vực: Việt Nam • VND • UTC+7"
            : "Thiết lập khu vực cửa hàng";

    public string ScannerStatusText =>
        _original.Scanner ==
            POS.Application.Abstractions.StoreSetup.ScannerMode.KeyboardWedge
            ? "Máy quét mã vạch: dùng được ngay khi cắm USB"
            : "Máy quét mã vạch: kiểm tra kết nối USB";

    public string ScannerInstructionText =>
        _original.Scanner ==
            POS.Application.Abstractions.StoreSetup.ScannerMode.KeyboardWedge
            ? "Bấm “Quét thử mã”, sau đó quét một sản phẩm để kiểm tra."
            : "Bấm “Quét thử mã” để kiểm tra thiết bị.";

    public bool HasInvalidLegacyTimeZone =>
        !IsValidTimeZone(_timeZoneId);

    public bool IsDirty =>
        BuildDraft() with { Version = _original.Version } != _original;

    public bool CanSave =>
        !_busy &&
        _issues.All(x => x.Severity != StoreSettingsIssueSeverity.Error);

    public bool CanPrintTest =>
        !_busy &&
        _receiptService is not null &&
        !IsDirty &&
        !string.IsNullOrWhiteSpace(DefaultPrinter);

    public string ValidationSummary =>
        _issues.Count == 0
            ? "Cấu hình hiện tại hợp lệ."
            : string.Join(
                Environment.NewLine,
                _issues.Select(x =>
                    (x.Severity == StoreSettingsIssueSeverity.Error
                        ? "Lỗi: "
                        : "Cảnh báo: ") + x.Message));

    public string StatusMessage
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string DirtyStateText =>
        IsDirty ? "Có thay đổi chưa lưu" : "Đã lưu";

    public string SaveStateText =>
        _saving ? "Đang lưu…" : DirtyStateText;

    public bool IsBusy => _busy;
    public bool IsSaving => _saving;
    public bool IsPrinterBusy => _printerBusy;

    public string PrinterDiscoveryMessage => _printerDiscoveryMessage;
    public string PrinterSelectionWarning => _printerSelectionWarning;

    public bool RestartRequired =>
        BuildDraft().RequiresRestartComparedTo(_original);

    public string RestartMessage =>
        RestartRequired
            ? "Thay đổi vị trí dữ liệu có hiệu lực sau khi mở lại ứng dụng."
            : string.Empty;

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        await RefreshPrintersAsync();
    }

    private void Change<T>(ref T field, T value)
    {
        if (!SetProperty(ref field, value))
            return;

        RefreshValidation();
    }

    private StoreSettingsSnapshot BuildDraft() =>
        new()
        {
            Version = _original.Version,
            StoreName = StoreName,
            Address = NormalizeOptionalText(Address),
            Hotline = NormalizeOptionalText(Hotline),
            TaxCode = NormalizeOptionalText(TaxCode),
            LogoAssetName = LogoAssetName,
            Currency = ParseCurrency(Currency),
            TimeZoneId = NormalizeTimeZoneId(TimeZoneId),
            PaperSize = ParseEnum(PaperSize, ReceiptPaperSize.K80),
            PrintCopyCount = PrintCopyCount,
            AutoPrint = AutoPrint,
            DefaultPrinter = NormalizeOptionalText(DefaultPrinter),
            Scanner = ParseEnum(
                ScannerMode,
                POS.Application.Abstractions.StoreSetup.ScannerMode.KeyboardWedge),
            CashDrawer = ParseEnum(
                CashDrawerMode,
                POS.Application.Abstractions.StoreSetup.CashDrawerMode.Disabled),
            VietQrEnabled = VietQrEnabled,
            BankBin = NormalizeOptionalText(BankBin),
            BankAccountNumber = NormalizeOptionalText(BankAccountNumber),
            BankAccountName = NormalizeOptionalText(BankAccountName),
            VietQrContent = NormalizeOptionalText(VietQrContent),
            DatabaseDirectory = DatabaseDirectory,
            BackupDirectory = BackupDirectory,
            Retention = new StoreRetentionPolicy
            {
                LatestCount = LatestRetention,
                WeeklyCount = WeeklyRetention,
                MonthlyCount = MonthlyRetention
            }
        };

    private void RefreshValidation()
    {
        _issues = _validator.Validate(BuildDraft()).Issues;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanPrintTest));
        OnPropertyChanged(nameof(DirtyStateText));
        OnPropertyChanged(nameof(SaveStateText));
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(StoreNameValidationMessage));
        OnPropertyChanged(nameof(HotlineValidationMessage));
        OnPropertyChanged(nameof(TaxCodeValidationMessage));
        OnPropertyChanged(nameof(RestartRequired));
        OnPropertyChanged(nameof(HasInvalidLegacyTimeZone));
        OnPropertyChanged(nameof(TimeZoneDisplay));
        SaveCommand?.NotifyCanExecuteChanged();
        PrintTestReceiptCommand?.NotifyCanExecuteChanged();
    }

    private async Task SaveAsync()
    {
        _busy = true;
        _saving = true;
        NotifyBusyState();
        StatusMessage = "Đang lưu…";

        try
        {
            var draft = BuildDraft();
            var restartRequired = draft.RequiresRestartComparedTo(_original);
            var readiness = await _readiness.EvaluateAsync(draft);
            _issues = readiness.Issues;
            OnPropertyChanged(nameof(ValidationSummary));

            if (!readiness.IsReady)
            {
                StatusMessage = "Kiểm tra lại thông tin trước khi lưu.";
                return;
            }

            var result = await _store.SaveAsync(draft, _original.Version);
            if (!result.IsSuccess || result.Settings is null)
            {
                StatusMessage = result.Status == StoreSettingsSaveStatus.Conflict
                    ? "Cài đặt đã thay đổi ở nơi khác; hãy tải lại trước khi ghi."
                    : "Không thể lưu cài đặt cửa hàng.";
                return;
            }

            var oldLogo = _logoToRemove;
            _original = result.Settings;
            _logoToRemove = null;
            Apply(_original);

            if (oldLogo is not null &&
                !string.Equals(oldLogo, _original.LogoAssetName, StringComparison.Ordinal))
            {
                await _logos.RemoveAsync(oldLogo);
            }

            StatusMessage = restartRequired
                ? "Đã lưu cài đặt cửa hàng. Một số thay đổi có hiệu lực sau khi mở lại ứng dụng."
                : "Đã lưu cài đặt cửa hàng.";
        }
        finally
        {
            _saving = false;
            SetIdle();
        }
    }

    private Task ResetAsync()
    {
        _logoToRemove = null;
        Apply(_original);
        StatusMessage = "Đã bỏ các thay đổi chưa lưu.";
        return Task.CompletedTask;
    }

    private async Task TestPrinterAsync()
    {
        SetBusy(printerBusy: true);
        StatusMessage = "Đang kiểm tra máy in…";
        try
        {
            var result = await _printers.TestAsync(DefaultPrinter);
            StatusMessage = result.Message;
        }
        finally
        {
            SetIdle();
        }
    }

    private async Task PrintTestReceiptAsync()
    {
        if (_receiptService is null)
        {
            StatusMessage = "Chưa sẵn sàng dịch vụ in thử.";
            return;
        }

        if (IsDirty)
        {
            StatusMessage = "Hãy lưu cài đặt máy in trước khi in thử.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DefaultPrinter))
        {
            StatusMessage = "Hãy chọn máy in hóa đơn trước khi in thử.";
            return;
        }

        SetBusy();
        StatusMessage = "Đang gửi phiếu thử đến máy in…";
        try
        {
            var settings = _store.Current;
            var store = new ReceiptStoreSnapshotDto(
                settings.StoreName,
                settings.Address,
                settings.Hotline,
                settings.TaxCode);
            var line = new ReceiptLineDto(
                1,
                1,
                "TEST",
                "Phiếu thử máy in",
                "phần",
                1,
                1_000,
                0,
                1_000,
                1_000,
                0,
                1_000,
                null,
                []);
            var receipt = new ReceiptRequest(
                store,
                ReceiptCopyKind.Original,
                0,
                1,
                "TEST-PRINT",
                "POS Enterprise",
                DateTimeOffset.UtcNow,
                PaymentMethod.Cash,
                1_000,
                0,
                1_000,
                1_000,
                0,
                [line]);
            var result = await _receiptService.PrintAsync(receipt);
            StatusMessage = result.IsSuccess
                ? "Đã gửi phiếu thử K80 đến máy in."
                : result.AppError.Message;
        }
        finally
        {
            SetIdle();
        }
    }

    private async Task RefreshPrintersAsync()
    {
        SetBusy(printerBusy: true);
        StatusMessage = "Đang tìm máy in…";
        try
        {
            await Task.Yield();
            var selected = DefaultPrinter;
            var discovered = _printers.Discover();
            Printers.Clear();
            foreach (var printer in discovered)
                Printers.Add(printer);

            if (!string.IsNullOrWhiteSpace(selected) &&
                !Printers.Any(x => string.Equals(
                    x.Name,
                    selected,
                    StringComparison.OrdinalIgnoreCase)))
            {
                DefaultPrinter = string.Empty;
                _printerSelectionWarning =
                    "Máy in đã chọn không còn khả dụng. Vui lòng chọn lại.";
            }
            else
            {
                _printerSelectionWarning = string.Empty;
            }

            _printerDiscoveryMessage = Printers.Count == 0
                ? "Không tìm thấy máy in nào."
                : $"Đã tìm thấy {Printers.Count} máy in.";
            OnPropertyChanged(nameof(PrinterDiscoveryMessage));
            OnPropertyChanged(nameof(PrinterSelectionWarning));
            StatusMessage = _printerDiscoveryMessage;
        }
        finally
        {
            SetIdle();
        }
    }

    private async Task TestQrAsync()
    {
        var draft = BuildDraft();
        var validation = _validator.Validate(draft);
        if (!validation.IsValid)
        {
            _issues = validation.Issues;
            OnPropertyChanged(nameof(ValidationSummary));
            StatusMessage = "Kiểm tra lại thông tin trước khi tạo QR.";
            return;
        }

        var bytes = await _qr.GenerateAsync(draft);
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        QrPreview = image;
        OnPropertyChanged(nameof(QrPreview));
        StatusMessage = "Đã tạo QR thử từ dữ liệu nháp; chưa lưu cấu hình.";
    }

    private async Task ReplaceLogoAsync()
    {
        var source = _picker.PickLogo();
        if (source is null)
            return;

        var old = LogoAssetName;
        var imported = await _logos.ImportAsync(source);
        if (old is not null &&
            !string.Equals(old, imported, StringComparison.Ordinal))
        {
            _logoToRemove = old;
        }

        LogoAssetName = imported;
        StatusMessage = "Đã chọn logo. Bấm “Lưu thay đổi” để áp dụng.";
    }

    private Task RemoveLogoAsync()
    {
        _logoToRemove = LogoAssetName;
        LogoAssetName = null;
        StatusMessage = "Logo sẽ được xóa khi bạn lưu thay đổi.";
        return Task.CompletedTask;
    }

    private void Apply(StoreSettingsSnapshot settings)
    {
        _logoToRemove = null;
        StoreName = settings.StoreName;
        Address = settings.Address ?? string.Empty;
        Hotline = settings.Hotline ?? string.Empty;
        TaxCode = settings.TaxCode ?? string.Empty;
        LogoAssetName = settings.LogoAssetName;
        Currency = settings.Currency.ToString();
        TimeZoneId = settings.TimeZoneId;
        PaperSize = settings.PaperSize.ToString();
        PrintCopyCount = settings.PrintCopyCount;
        AutoPrint = settings.AutoPrint;
        DefaultPrinter = settings.DefaultPrinter ?? string.Empty;
        ScannerMode = settings.Scanner.ToString();
        CashDrawerMode = settings.CashDrawer.ToString();
        VietQrEnabled = settings.VietQrEnabled;
        BankBin = settings.BankBin ?? string.Empty;
        BankAccountNumber = settings.BankAccountNumber ?? string.Empty;
        BankAccountName = settings.BankAccountName ?? string.Empty;
        VietQrContent = settings.VietQrContent ?? string.Empty;
        DatabaseDirectory = settings.DatabaseDirectory;
        BackupDirectory = settings.BackupDirectory;
        LatestRetention = settings.Retention.LatestCount;
        WeeklyRetention = settings.Retention.WeeklyCount;
        MonthlyRetention = settings.Retention.MonthlyCount;
    }

    private void SetBusy(bool? printerBusy = null)
    {
        _busy = true;
        if (printerBusy.HasValue)
            _printerBusy = printerBusy.Value;
        NotifyBusyState();
    }

    private void SetIdle()
    {
        _busy = false;
        _printerBusy = false;
        NotifyBusyState();
    }

    private void NotifyBusyState()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(IsPrinterBusy));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanPrintTest));
        OnPropertyChanged(nameof(SaveStateText));
        SaveCommand?.NotifyCanExecuteChanged();
        ResetCommand?.NotifyCanExecuteChanged();
        TestQrCommand?.NotifyCanExecuteChanged();
        TestPrinterCommand?.NotifyCanExecuteChanged();
        PrintTestReceiptCommand?.NotifyCanExecuteChanged();
        ReplaceLogoCommand?.NotifyCanExecuteChanged();
        RemoveLogoCommand?.NotifyCanExecuteChanged();
        RefreshPrintersCommand?.NotifyCanExecuteChanged();
    }

    private string FieldValidationMessage(string field) =>
        _issues.FirstOrDefault(x =>
            string.Equals(x.Field, field, StringComparison.Ordinal) &&
            x.Severity == StoreSettingsIssueSeverity.Error)?.Message ?? string.Empty;

    private void HandleException(Exception exception)
    {
        StatusMessage = "Không thể hoàn tất thao tác. Hãy thử lại.";
    }

    private static T ParseEnum<T>(string value, T fallback)
        where T : struct, Enum =>
        Enum.TryParse(value, true, out T result) ? result : fallback;

    private static StoreCurrency ParseCurrency(string value) =>
        ParseEnum(value, StoreCurrency.VietnameseDong);

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string NormalizeTimeZoneId(string? value) =>
        IsValidTimeZone(value) ? value!.Trim() : VietnameseTimeZoneId;

    private static bool IsValidTimeZone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
