using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Printing;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Printing;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public sealed class LabelPrintViewModel : ViewModelBase, IDisposable
{
    public const int MaximumTotalLabels = 5000;

    private readonly IClock _clock;
    private readonly ILabelPrinterCatalog _printerCatalog;
    private readonly ILabelPrintSettingsStore? _settingsStore;
    private readonly ILabelPrintingService _printingService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<LabelPrintViewModel> _logger;
    private readonly ILabelPreviewDebounceScheduler _previewScheduler;
    private LabelTemplate _selectedTemplate;
    private LabelPrinterInfo? _selectedPrinter;
    private string? _preferredPrinterName;
    private bool _printerSelectionLost;
    private bool _suppressSettingsPersistence;
    private string _widthText = "50";
    private string _heightText = "30";
    private string _offsetXText = "0";
    private string _offsetYText = "0";
    private string _innerMarginText = "2";
    private string _statusMessage = string.Empty;
    private string _printerWarning = string.Empty;
    private string _validationMessage = string.Empty;
    private string _previewMessage = string.Empty;
    private bool _isBusy;
    private bool _isPreviewUpdating;
    private LabelJobSnapshot? _previewJob;
    private int _previewPageNumber = 1;
    private IDisposable? _previewDebounce;
    private long _previewRevision;

    public LabelPrintViewModel(
        IReadOnlyList<ProductRowViewModel> selectedProducts,
        IClock clock,
        ILabelPrinterCatalog printerCatalog,
        ILabelPrintingService printingService,
        IPermissionService permissionService,
        ILogger<LabelPrintViewModel> logger,
        ILabelPrintSettingsStore? settingsStore = null,
        ILabelPreviewDebounceScheduler? previewScheduler = null)
    {
        ArgumentNullException.ThrowIfNull(selectedProducts);
        if (selectedProducts.Count == 0) throw new ArgumentException("Phải chọn ít nhất một sản phẩm.", nameof(selectedProducts));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _printerCatalog = printerCatalog ?? throw new ArgumentNullException(nameof(printerCatalog));
        _settingsStore = settingsStore;
        _printingService = printingService ?? throw new ArgumentNullException(nameof(printingService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _previewScheduler = previewScheduler ?? new DispatcherLabelPreviewDebounceScheduler(System.Windows.Threading.Dispatcher.CurrentDispatcher);

        var savedSettings = settingsStore?.Current ?? new LabelPrintSettings();
        _selectedTemplate = CreateTemplate(savedSettings);
        _preferredPrinterName = savedSettings.PrinterName;
        Products = new ObservableCollection<LabelProductRowViewModel>(
            selectedProducts.DistinctBy(x => x.Id).Select(x => new LabelProductRowViewModel(x)));
        foreach (var row in Products) row.PropertyChanged += OnProductPropertyChanged;
        TemplateOptions = new ReadOnlyCollection<LabelTemplate>(
            [.. LabelTemplate.Presets, _selectedTemplate.Kind == LabelTemplateKind.Custom
                ? _selectedTemplate
                : new LabelTemplate(LabelTemplateKind.Custom, "Kích thước tùy chỉnh", 50, 30)]);
        Printers = new ObservableCollection<LabelPrinterInfo>();

        UpdateEditorTextFromTemplate();
        RefreshPrinters();
        RebuildPreview();

        RefreshPrintersCommand = new AsyncRelayCommand(RefreshPrintersAsync, () => !_isBusy);
        TestPrintCommand = new AsyncRelayCommand(TestPrintAsync, CanTestPrint, HandleException);
        PrintCommand = new AsyncRelayCommand(PrintAsync, CanPrint, HandleException);
        CloseCommand = new AsyncRelayCommand(() => { RequestClose?.Invoke(false); return Task.CompletedTask; }, () => !_isBusy);
        PreviousPreviewCommand = new AsyncRelayCommand(() => { PreviewPageNumber--; NotifyPreview(); return Task.CompletedTask; }, () => PreviewPageNumber > 1 && !_isBusy);
        NextPreviewCommand = new AsyncRelayCommand(() => { PreviewPageNumber++; NotifyPreview(); return Task.CompletedTask; }, () => PreviewPageNumber < PreviewPageCount && !_isBusy);
    }

    public ObservableCollection<LabelProductRowViewModel> Products { get; }
    public ReadOnlyCollection<LabelTemplate> TemplateOptions { get; }
    public ObservableCollection<LabelPrinterInfo> Printers { get; }
    public AsyncRelayCommand RefreshPrintersCommand { get; }
    public AsyncRelayCommand TestPrintCommand { get; }
    public AsyncRelayCommand PrintCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }
    public AsyncRelayCommand PreviousPreviewCommand { get; }
    public AsyncRelayCommand NextPreviewCommand { get; }
    public event Action<bool>? RequestClose;
    public event Action? PreviewChanged;

    public LabelTemplate SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (!SetProperty(ref _selectedTemplate, value)) return;
            OnPropertyChanged(nameof(SelectedTemplateOption));
            UpdateEditorTextFromTemplate();
            QueuePreviewRebuild();
            PersistSettings();
        }
    }

    public LabelTemplate? SelectedTemplateOption
    {
        get => TemplateOptions.FirstOrDefault(option => option.Kind == SelectedTemplate.Kind);
        set
        {
            if (value is not null) SelectedTemplate = value;
        }
    }

    public LabelPrinterInfo? SelectedPrinter
    {
        get => _selectedPrinter;
        set
        {
            if (!SetProperty(ref _selectedPrinter, value)) return;
            _preferredPrinterName = value?.Name;
            if (value is not null) _printerSelectionLost = false;
            PersistSettings();
            PrinterWarning = value is null ? "Chưa chọn máy in tem." : string.Empty;
            UpdatePrinterWarning();
            NotifyCommandStates();
        }
    }

    public string WidthText { get => _widthText; set { if (SetProperty(ref _widthText, value ?? string.Empty)) { QueuePreviewRebuild(); PersistSettings(); } } }
    public string HeightText { get => _heightText; set { if (SetProperty(ref _heightText, value ?? string.Empty)) { QueuePreviewRebuild(); PersistSettings(); } } }
    public string OffsetXText { get => _offsetXText; set { if (SetProperty(ref _offsetXText, value ?? string.Empty)) { QueuePreviewRebuild(); PersistSettings(); } } }
    public string OffsetYText { get => _offsetYText; set { if (SetProperty(ref _offsetYText, value ?? string.Empty)) { QueuePreviewRebuild(); PersistSettings(); } } }
    public string InnerMarginText { get => _innerMarginText; set { if (SetProperty(ref _innerMarginText, value ?? string.Empty)) { QueuePreviewRebuild(); PersistSettings(); } } }

    public string PrintDateText => _clock.UtcNow.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string PrinterWarning { get => _printerWarning; private set => SetProperty(ref _printerWarning, value); }
    public string ValidationMessage { get => _validationMessage; private set => SetProperty(ref _validationMessage, value); }
    public string PreviewMessage { get => _previewMessage; private set => SetProperty(ref _previewMessage, value); }
    public bool IsBusy { get => _isBusy; private set { if (!SetProperty(ref _isBusy, value)) return; NotifyCommandStates(); } }
    public bool IsPreviewUpdating { get => _isPreviewUpdating; private set { if (SetProperty(ref _isPreviewUpdating, value)) NotifyCommandStates(); } }
    public bool IsCustomTemplate => SelectedTemplate.Kind == LabelTemplateKind.Custom;
    public int ProductCount => Products.Count;
    public int TotalLabels => _previewJob?.TotalLabels ?? 0;
    public string TotalLabelsText => $"{TotalLabels:N0} tem • {ProductCount:N0} sản phẩm";
    public string PrintButtonText => IsPreviewValid && !IsPreviewUpdating
        ? $"In {TotalLabels:N0} tem"
        : "In tem";
    public bool IsPreviewValid => _previewJob is not null && string.IsNullOrEmpty(ValidationMessage);
    public int PreviewPageNumber { get => _previewPageNumber; private set => SetProperty(ref _previewPageNumber, value); }
    public int PreviewPageCount => _previewJob?.TotalLabels ?? 0;
    public LabelProductSnapshot? PreviewProduct =>
        _previewJob is null || PreviewPageNumber < 1 || PreviewPageNumber > PreviewPageCount
            ? null
            : ExpandPreviewProducts(_previewJob)[PreviewPageNumber - 1];
    public LabelTemplate PreviewTemplate => _previewJob?.Template ?? SelectedTemplate;
    public string PreviewDateText => _previewJob?.PrintDateText ?? PrintDateText;

    public string CanPrintReason
    {
        get
        {
            if (!_permissionService.HasPermission(SystemCapability.ManageProducts)) return "Bạn không có quyền quản lý sản phẩm để in tem.";
            if (SelectedPrinter is null) return "Hãy chọn máy in tem.";
            if (!SelectedPrinter.IsAvailable) return "Máy in đã chọn hiện không khả dụng.";
            if (IsPreviewUpdating) return "Đang cập nhật xem trước…";
            if (!IsPreviewValid) return string.IsNullOrWhiteSpace(ValidationMessage) ? "Nhập số lượng hợp lệ trước khi in." : ValidationMessage;
            return string.Empty;
        }
    }

    public string ValidationSummary => ValidationMessage;

    private void QueuePreviewRebuild()
    {
        _previewDebounce?.Dispose();
        _previewDebounce = null;
        var revision = ++_previewRevision;
        _previewJob = null;
        foreach (var row in Products) row.ErrorText = string.Empty;
        ValidationMessage = string.Empty;
        PreviewMessage = "Đang cập nhật xem trước…";
        IsPreviewUpdating = true;
        UpdatePrinterWarning();
        PreviewPageNumber = 0;
        OnPropertyChanged(nameof(TotalLabels));
        OnPropertyChanged(nameof(TotalLabelsText));
        OnPropertyChanged(nameof(PrintButtonText));
        OnPropertyChanged(nameof(IsPreviewValid));
        OnPropertyChanged(nameof(CanPrintReason));
        NotifyPreview();
        _previewDebounce = _previewScheduler.Schedule(TimeSpan.FromMilliseconds(300), () =>
        {
            if (revision != _previewRevision) return;
            _previewDebounce = null;
            IsPreviewUpdating = false;
            RebuildPreview();
        });
    }

    private void RebuildPreview()
    {
        foreach (var row in Products) row.ErrorText = string.Empty;
        _previewJob = null;
        ValidationMessage = string.Empty;
        PreviewMessage = string.Empty;
        var template = ParseTemplate(out var templateError);
        if (template is null)
        {
            ValidationMessage = templateError;
            FinishPreviewRebuild();
            return;
        }
        var snapshots = new List<LabelProductSnapshot>();
        foreach (var row in Products)
        {
            if (!row.TryGetQuantity(out var quantity))
            {
                row.ErrorText = "Nhập số lượng từ 1 đến 1.000.";
                continue;
            }
            var snapshot = row.ToSnapshot(quantity);
            if (!snapshot.HasValidBarcode)
            {
                row.ErrorText = snapshot.BarcodeError;
                continue;
            }
            snapshots.Add(snapshot);
        }
        var invalidRows = Products.Where(x => !string.IsNullOrEmpty(x.ErrorText)).ToArray();
        if (invalidRows.Length > 0)
        {
            ValidationMessage = invalidRows.Length > 1
                ? $"Còn {invalidRows.Length:N0} sản phẩm cần nhập dữ liệu hợp lệ."
                : string.Empty;
            FinishPreviewRebuild();
            return;
        }
        try
        {
            var job = LabelJobSnapshot.Create(_clock.UtcNow, snapshots, template);
            if (job.TotalLabels > MaximumTotalLabels)
            {
                ValidationMessage = $"Tổng số tem không được vượt quá {MaximumTotalLabels:N0}.";
            }
            else
            {
                _previewJob = job;
                PreviewMessage = $"{job.TotalLabels:N0} tem • ngày in {job.PrintDateText}";
            }
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            ValidationMessage = exception.Message;
        }
        FinishPreviewRebuild();
    }

    private void FinishPreviewRebuild()
    {
        PreviewPageNumber = _previewJob is null
            ? 0
            : Math.Clamp(PreviewPageNumber < 1 ? 1 : PreviewPageNumber, 1, PreviewPageCount);
        OnPropertyChanged(nameof(IsCustomTemplate));
        OnPropertyChanged(nameof(PrintDateText));
        OnPropertyChanged(nameof(TotalLabels));
        OnPropertyChanged(nameof(TotalLabelsText));
        OnPropertyChanged(nameof(PrintButtonText));
        OnPropertyChanged(nameof(IsPreviewValid));
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(CanPrintReason));
        NotifyPreview();
        NotifyCommandStates();
    }

    private static LabelTemplate CreateTemplate(LabelPrintSettings settings)
    {
        var name = settings.TemplateKind switch
        {
            LabelTemplateKind.Standard60x40 => "Tiêu chuẩn 60 × 40 mm",
            LabelTemplateKind.Custom => "Kích thước tùy chỉnh",
            _ => "Tiêu chuẩn 50 × 30 mm"
        };
        var template = new LabelTemplate(
            settings.TemplateKind,
            name,
            settings.WidthMm,
            settings.HeightMm,
            settings.OffsetXmm,
            settings.OffsetYmm,
            settings.InnerMarginMm);
        return template.IsValid(out _) ? template : LabelTemplate.Standard50x30;
    }

    private void PersistSettings()
    {
        if (_settingsStore is null || _suppressSettingsPersistence) return;
        var template = ParseTemplate(out _);
        if (template is null) return;
        _settingsStore.Save(new LabelPrintSettings
        {
            TemplateKind = template.Kind,
            WidthMm = template.WidthMm,
            HeightMm = template.HeightMm,
            OffsetXmm = template.OffsetXmm,
            OffsetYmm = template.OffsetYmm,
            InnerMarginMm = template.InnerMarginMm,
            PrinterName = SelectedPrinter?.Name ?? _preferredPrinterName
        });
    }

    private LabelTemplate? ParseTemplate(out string error)
    {
        if (SelectedTemplate.Kind != LabelTemplateKind.Custom)
        {
            var preset = SelectedTemplate with { OffsetXmm = ReadNumber(OffsetXText), OffsetYmm = ReadNumber(OffsetYText), InnerMarginMm = ReadNumber(InnerMarginText) };
            if (!preset.IsValid(out error)) return null;
            return preset;
        }
        if (!TryReadNumber(WidthText, out var width) || !TryReadNumber(HeightText, out var height) ||
            !TryReadNumber(OffsetXText, out var offsetX) || !TryReadNumber(OffsetYText, out var offsetY) ||
            !TryReadNumber(InnerMarginText, out var margin))
        {
            error = "Kích thước, căn chỉnh và lề phải là số hợp lệ.";
            return null;
        }
        var template = new LabelTemplate(LabelTemplateKind.Custom, "Kích thước tùy chỉnh", width, height, offsetX, offsetY, margin);
        if (!template.IsValid(out error)) return null;
        return template;
    }

    private static double ReadNumber(string text) => TryReadNumber(text, out var value) ? value : double.NaN;

    private static bool TryReadNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
        double.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("vi-VN"), out value);

    private void UpdateEditorTextFromTemplate()
    {
        _widthText = SelectedTemplate.WidthMm.ToString("0.##", CultureInfo.InvariantCulture);
        _heightText = SelectedTemplate.HeightMm.ToString("0.##", CultureInfo.InvariantCulture);
        _offsetXText = SelectedTemplate.OffsetXmm.ToString("0.##", CultureInfo.InvariantCulture);
        _offsetYText = SelectedTemplate.OffsetYmm.ToString("0.##", CultureInfo.InvariantCulture);
        _innerMarginText = SelectedTemplate.InnerMarginMm.ToString("0.##", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(WidthText)); OnPropertyChanged(nameof(HeightText));
        OnPropertyChanged(nameof(OffsetXText)); OnPropertyChanged(nameof(OffsetYText)); OnPropertyChanged(nameof(InnerMarginText));
    }

    private void RefreshPrinters()
    {
        try
        {
            var current = SelectedPrinter?.Name ?? _preferredPrinterName;
            var discovered = (_printerCatalog.Discover() ?? Array.Empty<LabelPrinterInfo>())
                .Where(printer => !string.IsNullOrWhiteSpace(printer.Name))
                .DistinctBy(printer => printer.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Printers.Clear();
            foreach (var printer in discovered) Printers.Add(printer);
            var selectedStillExists = current is not null && discovered.Any(printer =>
                string.Equals(printer.Name, current, StringComparison.OrdinalIgnoreCase));
            if (current is not null && !selectedStillExists) _printerSelectionLost = true;
            _suppressSettingsPersistence = true;
            SelectedPrinter = current is null && !_printerSelectionLost ? Printers.FirstOrDefault(x => x.IsAvailable) :
                Printers.FirstOrDefault(x => string.Equals(x.Name, current, StringComparison.OrdinalIgnoreCase));
            _suppressSettingsPersistence = false;
            PrinterWarning = current is not null && SelectedPrinter is null
                ? "Máy in đã chọn không còn khả dụng. Vui lòng chọn lại."
                    : Printers.Count == 0
                        ? "Không tìm thấy máy in. Bạn vẫn có thể xem trước; lệnh in sẽ bị khóa."
                        : string.Empty;
            UpdatePrinterWarning();
            OnPropertyChanged(nameof(CanPrintReason));
        }
        catch (Exception exception)
        {
            _suppressSettingsPersistence = false;
            Printers.Clear();
            SelectedPrinter = null;
            PrinterWarning = "Không thể tải danh sách máy in. Hãy thử lại.";
            StatusMessage = $"Không thể tải máy in: {exception.GetBaseException().Message}";
            OnPropertyChanged(nameof(CanPrintReason));
        }
    }

    private Task RefreshPrintersAsync()
    {
        var before = Printers.Select(printer => printer.Name).ToArray();
        RefreshPrinters();
        if (string.Equals(PrinterWarning, "Không thể tải danh sách máy in. Hãy thử lại.", StringComparison.Ordinal))
            return Task.CompletedTask;
        var after = Printers.Select(printer => printer.Name).ToArray();
        StatusMessage = before.SequenceEqual(after, StringComparer.OrdinalIgnoreCase)
            ? $"Đã làm mới • danh sách không thay đổi • {Printers.Count:N0} máy in."
            : $"Đã làm mới • tìm thấy {Printers.Count:N0} máy in.";
        return Task.CompletedTask;
    }

    private void UpdatePrinterWarning()
    {
        if (SelectedPrinter is null || !SelectedPrinter.IsAvailable || SelectedPrinter.SupportsCustomMedia) return;
        PrinterWarning = $"Máy in này không xác nhận khổ {WidthText} × {HeightText} mm. Hãy in một tem kiểm tra trước khi in số lượng lớn.";
    }

    private bool CanTestPrint() => CanPrintCore() && _previewJob is not null && !IsPreviewUpdating;
    private bool CanPrint() => CanPrintCore() && _previewJob is not null && !IsPreviewUpdating;
    private bool CanPrintCore() => !IsBusy && _permissionService.HasPermission(SystemCapability.ManageProducts) && SelectedPrinter is { IsAvailable: true } && IsPreviewValid;

    private async Task TestPrintAsync()
    {
        var job = _previewJob;
        if (job is null || SelectedPrinter is null) return;
        IsBusy = true;
        StatusMessage = $"Đang gửi 1 tem kiểm tra đến {SelectedPrinter.Name}…";
        try
        {
            var result = await _printingService.PrintAsync(new LabelPrintRequest(job, SelectedPrinter.Name, true, 1));
            StatusMessage = result.IsSuccess
                ? "Đã gửi 1 tem kiểm tra. Hãy kiểm tra vị trí và kích thước trên giấy."
                : result.AppError.Code == ErrorCodes.Printing.Cancelled
                    ? "Đã hủy in thử."
                    : result.AppError.Message;
        }
        catch (OperationCanceledException) { StatusMessage = "Đã hủy in thử."; }
        catch (Exception exception)
        {
            PosLog.Error(_logger, exception, "In thử tem giá thất bại.");
            StatusMessage = $"In thử thất bại: {exception.GetBaseException().Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task PrintAsync()
    {
        var job = _previewJob;
        if (job is null || SelectedPrinter is null) return;
        IsBusy = true;
        StatusMessage = $"Đang gửi {job.TotalLabels:N0} tem đến {SelectedPrinter.Name}…";
        try
        {
            var result = await _printingService.PrintAsync(new LabelPrintRequest(job, SelectedPrinter.Name, false, job.TotalLabels));
            if (result.IsSuccess)
            {
                StatusMessage = $"Đã gửi {job.TotalLabels:N0} tem tới máy in.";
                IsBusy = false;
                RequestClose?.Invoke(true);
                return;
            }
            else StatusMessage = result.AppError.Code == ErrorCodes.Printing.Cancelled
                ? "Đã hủy in tem."
                : result.AppError.Message;
        }
        catch (OperationCanceledException) { StatusMessage = "Đã hủy in tem."; }
        catch (Exception exception)
        {
            PosLog.Error(_logger, exception, "In tem giá thất bại.");
            StatusMessage = $"In tem thất bại: {exception.GetBaseException().Message}";
        }
        finally { IsBusy = false; }
    }

    private void NotifyPreview()
    {
        OnPropertyChanged(nameof(PreviewProduct));
        OnPropertyChanged(nameof(PreviewTemplate));
        OnPropertyChanged(nameof(PreviewDateText));
        OnPropertyChanged(nameof(PreviewPageCount));
        PreviousPreviewCommand?.NotifyCanExecuteChanged();
        NextPreviewCommand?.NotifyCanExecuteChanged();
        PreviewChanged?.Invoke();
    }

    private static List<LabelProductSnapshot> ExpandPreviewProducts(LabelJobSnapshot job)
    {
        var result = new List<LabelProductSnapshot>();
        foreach (var item in job.Products) for (var i = 0; i < item.DefaultQuantity; i++) result.Add(item);
        return result;
    }

    private void NotifyCommandStates()
    {
        RefreshPrintersCommand?.NotifyCanExecuteChanged();
        TestPrintCommand?.NotifyCanExecuteChanged();
        PrintCommand?.NotifyCanExecuteChanged();
        CloseCommand?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanPrintReason));
    }

    private void OnProductPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LabelProductRowViewModel.QuantityText)) QueuePreviewRebuild();
    }

    private void HandleException(Exception exception)
    {
        global::POS.Application.Common.PosLog.Error(_logger, exception, "In tem giá thất bại.");
        StatusMessage = "Không thể gửi lệnh in tem. Hãy kiểm tra máy in và thử lại.";
    }

    public void Dispose()
    {
        _previewDebounce?.Dispose();
        _previewDebounce = null;
        foreach (var row in Products) row.PropertyChanged -= OnProductPropertyChanged;
        GC.SuppressFinalize(this);
    }
}
