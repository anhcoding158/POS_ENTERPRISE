using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using POS.Application.Abstractions.StoreSetup;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public sealed class StoreSettingsViewModel : ViewModelBase
{
    private readonly IStoreSettingsStore _store;
    private readonly IStoreSettingsValidator _validator;
    private readonly IStoreSettingsReadinessEvaluator _readiness;
    private readonly IStoreSettingsLogoService _logos;
    private readonly IPrinterTestService _printers;
    private readonly IStoreSettingsQrPreviewService _qr;
    private readonly IStoreSettingsFilePicker _picker;
    private StoreSettingsSnapshot _original;
    private bool _busy;
    private string _storeName = "", _address = "", _hotline = "", _taxCode = "", _databaseDirectory = "", _backupDirectory = "", _defaultPrinter = "", _bankBin = "", _accountNumber = "", _accountName = "", _vietQrContent = "POS";
    private string _paperSize = "K80", _scannerMode = "KeyboardWedge", _cashDrawerMode = "Disabled", _currency = "VietnameseDong", _timeZoneId = "SE Asia Standard Time";
    private int _copies = 1, _latest = 7, _weekly = 4, _monthly = 3;
    private bool _autoPrint, _vietQrEnabled;
    private string? _logoAssetName;
    private string? _logoToRemove;
    private string _status = "";
    private IReadOnlyList<StoreSettingsIssue> _issues = Array.Empty<StoreSettingsIssue>();

    public StoreSettingsViewModel(IStoreSettingsStore store, IStoreSettingsValidator validator, IStoreSettingsReadinessEvaluator readiness, IStoreSettingsLogoService logos, IPrinterTestService printers, IStoreSettingsQrPreviewService qr, IStoreSettingsFilePicker picker)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store)); _validator = validator ?? throw new ArgumentNullException(nameof(validator)); _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness)); _logos = logos ?? throw new ArgumentNullException(nameof(logos)); _printers = printers ?? throw new ArgumentNullException(nameof(printers)); _qr = qr ?? throw new ArgumentNullException(nameof(qr)); _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _original = store.Current; Apply(_original);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave, HandleException);
        ResetCommand = new AsyncRelayCommand(ResetAsync, () => !_busy, HandleException);
        TestQrCommand = new AsyncRelayCommand(TestQrAsync, () => !_busy, HandleException);
        TestPrinterCommand = new AsyncRelayCommand(TestPrinterAsync, () => !_busy, HandleException);
        ReplaceLogoCommand = new AsyncRelayCommand(ReplaceLogoAsync, () => !_busy, HandleException);
        RemoveLogoCommand = new AsyncRelayCommand(RemoveLogoAsync, () => !_busy && !string.IsNullOrWhiteSpace(_logoAssetName), HandleException);
        RefreshPrintersCommand = new AsyncRelayCommand(RefreshPrintersAsync, () => !_busy, HandleException);
        RefreshValidation();
    }

    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand ResetCommand { get; }
    public AsyncRelayCommand TestQrCommand { get; }
    public AsyncRelayCommand TestPrinterCommand { get; }
    public AsyncRelayCommand ReplaceLogoCommand { get; }
    public AsyncRelayCommand RemoveLogoCommand { get; }
    public AsyncRelayCommand RefreshPrintersCommand { get; }
    public ObservableCollection<PrinterInfo> Printers { get; } = new();
    public BitmapImage? QrPreview { get; private set; }
    public string StoreName { get => _storeName; set => Change(ref _storeName, value); }
    public string Address { get => _address; set => Change(ref _address, value); }
    public string Hotline { get => _hotline; set => Change(ref _hotline, value); }
    public string TaxCode { get => _taxCode; set => Change(ref _taxCode, value); }
    public string DatabaseDirectory { get => _databaseDirectory; set => Change(ref _databaseDirectory, value); }
    public string BackupDirectory { get => _backupDirectory; set => Change(ref _backupDirectory, value); }
    public string DefaultPrinter { get => _defaultPrinter; set => Change(ref _defaultPrinter, value); }
    public string BankBin { get => _bankBin; set => Change(ref _bankBin, value); }
    public string BankAccountNumber { get => _accountNumber; set => Change(ref _accountNumber, value); }
    public string BankAccountName { get => _accountName; set => Change(ref _accountName, value); }
    public string VietQrContent { get => _vietQrContent; set => Change(ref _vietQrContent, value); }
    public string PaperSize { get => _paperSize; set => Change(ref _paperSize, value); }
    public string ScannerMode { get => _scannerMode; set => Change(ref _scannerMode, value); }
    public string CashDrawerMode { get => _cashDrawerMode; set => Change(ref _cashDrawerMode, value); }
    public string Currency { get => _currency; set => Change(ref _currency, value); }
    public string TimeZoneId { get => _timeZoneId; set => Change(ref _timeZoneId, value); }
    public int PrintCopyCount { get => _copies; set => Change(ref _copies, value); }
    public int LatestRetention { get => _latest; set => Change(ref _latest, value); }
    public int WeeklyRetention { get => _weekly; set => Change(ref _weekly, value); }
    public int MonthlyRetention { get => _monthly; set => Change(ref _monthly, value); }
    public bool AutoPrint { get => _autoPrint; set => Change(ref _autoPrint, value); }
    public bool VietQrEnabled { get => _vietQrEnabled; set => Change(ref _vietQrEnabled, value); }
    public string? LogoAssetName { get => _logoAssetName; private set { if (SetProperty(ref _logoAssetName, value)) { OnPropertyChanged(nameof(LogoPath)); RemoveLogoCommand?.NotifyCanExecuteChanged(); } } }
    public string? LogoPath => _logos.GetManagedPath(_logoAssetName);
    public bool IsDirty => BuildDraft() with { Version = _original.Version } != _original;
    public bool CanSave => !_busy && _issues.All(x => x.Severity != StoreSettingsIssueSeverity.Error);
    public string ValidationSummary => _issues.Count == 0 ? "Cấu hình hiện tại hợp lệ." : string.Join(Environment.NewLine, _issues.Select(x => (x.Severity == StoreSettingsIssueSeverity.Error ? "Lỗi: " : "Cảnh báo: ") + x.Message));
    public string StatusMessage { get => _status; private set => SetProperty(ref _status, value); }
    public bool RestartRequired => BuildDraft().RequiresRestartComparedTo(_original);

    private void Change<T>(ref T field, T value, string? name = null) { if (!SetProperty(ref field, value, name)) return; RefreshValidation(); }
    private StoreSettingsSnapshot BuildDraft() => new() { Version = _original.Version, StoreName = StoreName, Address = Address, Hotline = Hotline, TaxCode = TaxCode, LogoAssetName = LogoAssetName, Currency = ParseCurrency(Currency), TimeZoneId = TimeZoneId, PaperSize = ParseEnum<ReceiptPaperSize>(PaperSize, ReceiptPaperSize.K80), PrintCopyCount = PrintCopyCount, AutoPrint = AutoPrint, DefaultPrinter = DefaultPrinter, Scanner = ParseEnum<POS.Application.Abstractions.StoreSetup.ScannerMode>(ScannerMode, POS.Application.Abstractions.StoreSetup.ScannerMode.KeyboardWedge), CashDrawer = ParseEnum<CashDrawerMode>(CashDrawerMode, POS.Application.Abstractions.StoreSetup.CashDrawerMode.Disabled), VietQrEnabled = VietQrEnabled, BankBin = BankBin, BankAccountNumber = BankAccountNumber, BankAccountName = BankAccountName, VietQrContent = VietQrContent, DatabaseDirectory = DatabaseDirectory, BackupDirectory = BackupDirectory, Retention = new StoreRetentionPolicy { LatestCount = LatestRetention, WeeklyCount = WeeklyRetention, MonthlyCount = MonthlyRetention } };
    private void RefreshValidation() { _issues = _validator.Validate(BuildDraft()).Issues; OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(CanSave)); OnPropertyChanged(nameof(ValidationSummary)); OnPropertyChanged(nameof(RestartRequired)); SaveCommand?.NotifyCanExecuteChanged(); }
    private async Task SaveAsync() { _busy = true; SaveCommand.NotifyCanExecuteChanged(); try { var draft = BuildDraft(); var restartRequired = draft.RequiresRestartComparedTo(_original); var readiness = await _readiness.EvaluateAsync(draft); _issues = readiness.Issues; OnPropertyChanged(nameof(ValidationSummary)); if (!readiness.IsReady) return; var result = await _store.SaveAsync(draft, _original.Version); if (!result.IsSuccess || result.Settings is null) { StatusMessage = result.Status == StoreSettingsSaveStatus.Conflict ? "Cấu hình đã thay đổi ở phiên khác; hãy tải lại trước khi ghi." : "Không thể lưu cấu hình cửa hàng."; return; } var oldLogo = _logoToRemove; _original = result.Settings; _logoToRemove = null; Apply(_original); if (oldLogo is not null && !string.Equals(oldLogo, _original.LogoAssetName, StringComparison.Ordinal)) await _logos.RemoveAsync(oldLogo); StatusMessage = restartRequired ? "Đã lưu. Một số thay đổi cần khởi động lại ứng dụng." : "Đã lưu cấu hình cửa hàng."; } finally { _busy = false; SaveCommand.NotifyCanExecuteChanged(); ResetCommand.NotifyCanExecuteChanged(); TestQrCommand.NotifyCanExecuteChanged(); TestPrinterCommand.NotifyCanExecuteChanged(); } }
    private Task ResetAsync() { _logoToRemove = null; Apply(_original); StatusMessage = "Đã hoàn tác thay đổi chưa lưu."; return Task.CompletedTask; }
    private async Task TestQrAsync() { var draft = BuildDraft(); var validation = _validator.Validate(draft); if (!validation.IsValid) { _issues = validation.Issues; OnPropertyChanged(nameof(ValidationSummary)); return; } var bytes = await _qr.GenerateAsync(draft); using var stream = new MemoryStream(bytes); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); QrPreview = image; OnPropertyChanged(nameof(QrPreview)); StatusMessage = "Đã tạo QR thử từ dữ liệu nháp; chưa lưu cấu hình."; }
    private async Task TestPrinterAsync() { var result = await _printers.TestAsync(DefaultPrinter); StatusMessage = result.Message; }
    private Task RefreshPrintersAsync() { Printers.Clear(); foreach (var printer in _printers.Discover()) Printers.Add(printer); return Task.CompletedTask; }
    private async Task ReplaceLogoAsync() { var source = _picker.PickLogo(); if (source is null) return; var old = LogoAssetName; var imported = await _logos.ImportAsync(source); if (old is not null && !string.Equals(old, imported, StringComparison.Ordinal)) _logoToRemove = old; LogoAssetName = imported; StatusMessage = "Đã nhập logo vào vùng lưu trữ được quản lý."; }
    private Task RemoveLogoAsync() { _logoToRemove = LogoAssetName; LogoAssetName = null; StatusMessage = "Logo sẽ được gỡ khi lưu cấu hình."; return Task.CompletedTask; }
    private void Apply(StoreSettingsSnapshot s) { _logoToRemove = null; StoreName = s.StoreName; Address = s.Address ?? ""; Hotline = s.Hotline ?? ""; TaxCode = s.TaxCode ?? ""; LogoAssetName = s.LogoAssetName; Currency = s.Currency.ToString(); TimeZoneId = s.TimeZoneId; PaperSize = s.PaperSize.ToString(); PrintCopyCount = s.PrintCopyCount; AutoPrint = s.AutoPrint; DefaultPrinter = s.DefaultPrinter ?? ""; ScannerMode = s.Scanner.ToString(); CashDrawerMode = s.CashDrawer.ToString(); VietQrEnabled = s.VietQrEnabled; BankBin = s.BankBin ?? ""; BankAccountNumber = s.BankAccountNumber ?? ""; BankAccountName = s.BankAccountName ?? ""; VietQrContent = s.VietQrContent ?? "POS"; DatabaseDirectory = s.DatabaseDirectory; BackupDirectory = s.BackupDirectory; LatestRetention = s.Retention.LatestCount; WeeklyRetention = s.Retention.WeeklyCount; MonthlyRetention = s.Retention.MonthlyCount; }
    private void HandleException(Exception exception) { StatusMessage = "Không thể hoàn tất thao tác cấu hình."; }
    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum => Enum.TryParse<T>(value, true, out var result) ? result : fallback;
    private static StoreCurrency ParseCurrency(string value) => ParseEnum(value, StoreCurrency.VietnameseDong);
}
