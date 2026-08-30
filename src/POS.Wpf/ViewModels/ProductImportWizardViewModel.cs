using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.ProductImports;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Categories;
using POS.Application.DTOs.ProductImports;
using POS.Application.ProductImports;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public sealed record ProductImportFieldOption(
    string DisplayName,
    string? CanonicalKey);

public sealed class ProductImportHeaderRowViewModel : ViewModelBase
{
    private readonly Action _onChanged;
    private ProductImportFieldOption _selectedField;

    public ProductImportHeaderRowViewModel(
        ProductImportHeader header,
        ProductImportFieldOption selectedField,
        Action onChanged)
    {
        Header = header;
        _selectedField = selectedField;
        _onChanged = onChanged;
    }

    public ProductImportHeader Header { get; }

    public string OriginalName =>
        string.IsNullOrWhiteSpace(Header.OriginalName)
            ? "(cột không có tiêu đề)"
            : Header.OriginalName;

    public string SampleValue =>
        string.IsNullOrWhiteSpace(Header.SampleValue)
            ? "—"
            : Header.SampleValue!;

    public IReadOnlyList<ProductImportFieldOption> FieldOptions { get; } =
        [new("Không ánh xạ", null), .. ProductImportSchemaCatalog.Fields.Select(field =>
            new ProductImportFieldOption(
                field.VietnameseLabel,
                field.CanonicalKey))];

    public ProductImportFieldOption SelectedField
    {
        get => _selectedField;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!SetProperty(ref _selectedField, value))
            {
                return;
            }

            _onChanged();
        }
    }
}

public sealed class ProductImportPreviewRowViewModel
{
    public ProductImportPreviewRowViewModel(ProductImportRow row)
    {
        SourceRowNumber = row.SourceRowNumber;
        ProductCode = row.ProductCode ?? "";
        Barcode = row.Barcode ?? "";
        Name = row.Name ?? "";
        CategoryName = row.CategoryName ?? "";
        UnitName = row.UnitName ?? "";
        SalePrice = row.SalePrice?.ToString("N0", CultureInfo.InvariantCulture) ?? "";
        CostPrice = row.CostPrice?.ToString("N0", CultureInfo.InvariantCulture) ?? "";
        InitialStock = row.InitialStockQuantity?.ToString("N0", CultureInfo.InvariantCulture) ?? "";
        MinimumStock = row.MinimumStock?.ToString("N0", CultureInfo.InvariantCulture) ?? "";
        IsActive = row.IsActive switch { true => "Đang bán", false => "Ngừng bán", _ => "" };
        Notes = row.Notes ?? "";
        Issues = row.Issues;
    }

    public int SourceRowNumber { get; }
    public string ProductCode { get; }
    public string Barcode { get; }
    public string Name { get; }
    public string CategoryName { get; }
    public string UnitName { get; }
    public string SalePrice { get; }
    public string CostPrice { get; }
    public string InitialStock { get; }
    public string MinimumStock { get; }
    public string IsActive { get; }
    public string Notes { get; }
    public IReadOnlyList<ProductImportIssue> Issues { get; }
    public string IssueText => Issues.Count == 0 ? "" : string.Join("; ", Issues.Select(issue => issue.Message));
}

public sealed class ProductImportIssueViewModel
{
    public ProductImportIssueViewModel(ProductImportIssue issue)
    {
        SourceRow = issue.SourceRowNumber?.ToString(CultureInfo.InvariantCulture) ?? "Tệp";
        Field = ProductImportSchemaCatalog.FindByCanonicalKey(issue.FieldKey)?.VietnameseLabel ?? "Tệp";
        Severity = issue.Severity == ProductImportIssueSeverity.Error ? "Lỗi" : "Cảnh báo";
        Message = issue.Message;
    }

    public string SourceRow { get; }
    public string Field { get; }
    public string Severity { get; }
    public string Message { get; }
}

public sealed record ProductImportIssueGroupViewModel(
    string Title,
    int Count,
    string Summary);

/// <summary>
/// Điều phối toàn bộ import UI. Không truy cập DbContext; mọi DB operation
/// đi qua Application services trong scope ngắn.
/// </summary>
public sealed class ProductImportWizardViewModel : ViewModelBase, IDisposable
{
    private const int MaximumDisplayedIssues = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProductImportPreviewService _previewService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ProductImportWizardViewModel> _logger;
    private readonly IProductExportDialogService? _exportDialogService;
    private readonly ProductImportLimits _limits = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private CancellationTokenSource? _operationCancellation;

    private string? _filePath;
    private string _fileName = "Chưa chọn tệp";
    private string? _selectedWorksheetName;
    private ProductImportDuplicatePolicyOption _selectedPolicy;
    private ProductImportPreviewResult? _preview;
    private ProductImportResult? _result;
    private int _currentStep;
    private bool _isBusy;
    private bool _mappingConfigured;
    private bool _previewStale;
    private bool _showMapping;
    private bool _showFullPreview;
    private bool _showIssueDetails;
    private string _statusMessage = "Chọn tệp CSV hoặc XLSX để bắt đầu.";
    private string _errorMessage = string.Empty;
    private bool _closeAfterBusy;

    public ProductImportWizardViewModel(
        IServiceScopeFactory scopeFactory,
        IProductImportPreviewService previewService,
        IPermissionService permissionService,
        ILogger<ProductImportWizardViewModel> logger,
        IProductExportDialogService? exportDialogService = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exportDialogService = exportDialogService;

        DuplicatePolicies =
        [
            new(ProductImportDuplicatePolicy.Skip, "Chỉ thêm sản phẩm mới", "Bỏ qua sản phẩm đã có và giữ nguyên thông tin hiện tại."),
            new(ProductImportDuplicatePolicy.Update, "Cập nhật", "Cập nhật thông tin được phép; giữ lịch sử và không thay tồn hiện tại."),
            new(ProductImportDuplicatePolicy.Error, "Dừng nếu có sản phẩm trùng", "Không lưu cả lượt nhập nếu phát hiện sản phẩm đã có.")
        ];
        _selectedPolicy = DuplicatePolicies[0];

        ChooseFileCommand = new AsyncRelayCommand(ChooseFileAsync, () => !IsBusy, HandleException);
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, CanPreview, HandleException);
        ConfirmImportCommand = new AsyncRelayCommand(ImportAsync, CanConfirm, HandleException);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsBusy, HandleException);
        ImportAnotherFileCommand = new AsyncRelayCommand(ImportAnotherFileAsync, () => !IsBusy, HandleException);
        CloseCommand = new AsyncRelayCommand(CloseAsync, () => !IsBusy, HandleException);
        ToggleMappingCommand = new AsyncRelayCommand(ToggleMappingAsync, () => !IsBusy && HasPreview, HandleException);
        ToggleFullPreviewCommand = new AsyncRelayCommand(ToggleFullPreviewAsync, () => !IsBusy && HasPreview, HandleException);
        ToggleIssueDetailsCommand = new AsyncRelayCommand(ToggleIssueDetailsAsync, () => !IsBusy && HasIssues, HandleException);
        ViewProductsCommand = new AsyncRelayCommand(ViewProductsAsync, () => !IsBusy && HasImportResult && _result?.IsCommitted == true, HandleException);
        DownloadTemplateCommand = new AsyncRelayCommand(DownloadTemplateAsync, () => !IsBusy && _exportDialogService is not null, HandleException);
    }

    public event Action<bool?>? RequestClose;

    public ObservableCollection<ProductImportHeaderRowViewModel> Headers { get; } = [];
    public ObservableCollection<ProductImportPreviewRowViewModel> PreviewRows { get; } = [];
    public ObservableCollection<ProductImportIssueViewModel> Issues { get; } = [];
    public ObservableCollection<ProductImportIssueGroupViewModel> IssueGroups { get; } = [];
    public IReadOnlyList<ProductImportDuplicatePolicyOption> DuplicatePolicies { get; }
    public AsyncRelayCommand ChooseFileCommand { get; }
    public AsyncRelayCommand PreviewCommand { get; }
    public AsyncRelayCommand ConfirmImportCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public AsyncRelayCommand ImportAnotherFileCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }
    public AsyncRelayCommand ToggleMappingCommand { get; }
    public AsyncRelayCommand ToggleFullPreviewCommand { get; }
    public AsyncRelayCommand ToggleIssueDetailsCommand { get; }
    public AsyncRelayCommand ViewProductsCommand { get; }
    public AsyncRelayCommand DownloadTemplateCommand { get; }

    public string? FilePath { get => _filePath; private set => SetProperty(ref _filePath, value); }
    public string FileName { get => _fileName; private set => SetProperty(ref _fileName, value); }
    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);
    public IReadOnlyList<string> WorksheetNames { get; private set; } = [];
    public string? SelectedWorksheetName
    {
        get => _selectedWorksheetName;
        set
        {
            if (!SetProperty(ref _selectedWorksheetName, value)) return;
            if (_preview is not null) InvalidatePreview("Trang tính đã đổi; hãy kiểm tra lại.");
            OnPropertyChanged(nameof(HasSelectedWorksheet));
            OnPropertyChanged(nameof(CanRevalidate));
            NotifyCommands();
        }
    }

    public ProductImportDuplicatePolicyOption SelectedPolicy
    {
        get => _selectedPolicy;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!SetProperty(ref _selectedPolicy, value)) return;
            if (_preview is not null) InvalidatePreview("Cách xử lý sản phẩm đã có đã đổi; hãy kiểm tra lại.");
            OnPropertyChanged(nameof(PolicyExplanation));
            OnPropertyChanged(nameof(ConfirmationText));
            NotifyCommands();
        }
    }

    public ProductImportPreviewResult? PreviewResult => _preview;
    public ProductImportResult? ImportResult => _result;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) { OnPropertyChanged(nameof(CanRevalidate)); OnPropertyChanged(nameof(ShowFullPreviewAction)); NotifyCommands(); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public int CurrentStep { get => _currentStep; private set { if (SetProperty(ref _currentStep, value)) NotifyCommands(); } }
    public bool IsPreviewStale => _previewStale;
    public bool HasPreview => _preview is not null;
    public bool HasSelectedWorksheet => !string.IsNullOrWhiteSpace(SelectedWorksheetName);
    public bool HasMultipleWorksheets => WorksheetNames.Count > 1;
    public bool HasIssues => Issues.Count > 0;
    public bool HasImportResult => _result is not null;
    public bool ShowMapping { get => _showMapping; private set => SetProperty(ref _showMapping, value); }
    public bool ShowFullPreview { get => _showFullPreview; private set => SetProperty(ref _showFullPreview, value); }
    public bool ShowIssueDetails { get => _showIssueDetails; private set => SetProperty(ref _showIssueDetails, value); }
    public bool ShowMappingSection => ShowMapping || HasMappingAttention;
    public bool ShowCompactPreview => !ShowFullPreview;
    public bool ShowChooseFileAction => !IsBusy && CurrentStep == 0 && !HasFile;
    public bool ShowCheckAction => !IsBusy && HasFile &&
        ((CurrentStep == 0 && HasMultipleWorksheets && HasSelectedWorksheet) || (CurrentStep == 1 && CanRevalidate));
    public bool ShowImportAction => !IsBusy && CurrentStep == 1 && CanImport;
    public bool ShowChangeFileAction => !IsBusy && HasFile && CurrentStep != 3;
    public bool ShowViewProductsAction => !IsBusy && HasImportResult && _result?.IsCommitted == true;
    public bool ShowResultRetryAction => !IsBusy && HasImportResult && _result?.IsCommitted != true;
    public bool HasPreviewRows => PreviewRows.Count > 0;
    public bool NoPreviewRows => _preview is not null && !HasPreviewRows;
    public bool ShowFullPreviewAction => !IsBusy && HasPreviewRows;
    public bool ShowBlockingHint => !IsBusy && _preview is not null && !CanImport && !_previewStale;
    public bool HasMappingAttention => Headers.Any(header => header.SelectedField.CanonicalKey is null) ||
        (_preview?.FileIssues.Any(issue => issue.Code is "HEADER_REQUIRED_MISSING" or "HEADER_UNKNOWN" or "HEADER_DUPLICATE" or "MAPPING_COLUMN_INVALID" or "MAPPING_DUPLICATE_TARGET") == true);
    public bool HasDuplicateRows => _preview?.Summary.DuplicateProductCodeCount > 0 || _preview?.Summary.DuplicateBarcodeCount > 0;
    public bool CanImport => _preview?.CanImport == true && !_previewStale && _permissionService.HasPermission(SystemCapability.ManageProducts);
    public bool CanRevalidate => !IsBusy && HasFile && _previewStale && (!HasMultipleWorksheets || HasSelectedWorksheet);
    public string PolicyExplanation => SelectedPolicy.Description;
    public string MappingStatusText => HasMappingAttention
        ? "Một vài cột chưa được xác định. Hãy mở phần ghép cột để chọn đúng thông tin."
        : "Đã nhận diện các cột trong file.";
    public string InspectionConclusion
    {
        get
        {
            if (_preview is null) return "Chưa có kết quả kiểm tra.";
            if (_preview.Summary.TotalDataRows == 0)
                return _preview.Summary.EmptyRows > 0
                    ? "File chỉ có dòng trống."
                    : "File chỉ có tiêu đề, chưa có sản phẩm để nhập.";
            if (_preview.Summary.ErrorCount > 0)
                return ErrorRowCount > 0
                    ? $"Có {ErrorRowCount:N0} dòng có lỗi cần sửa trước khi nhập."
                    : $"Có {_preview.Summary.ErrorCount:N0} vấn đề cần sửa trước khi nhập.";
            if (_preview.Summary.WarningCount > 0)
                return $"Danh sách đã đọc đủ; có {_preview.Summary.WarningCount:N0} cảnh báo cần xem lại.";
            return "Danh sách đã sẵn sàng để nhập.";
        }
    }
    public string InspectionDetail => _preview is null
        ? ""
        : $"Đã kiểm tra {_preview.Summary.TotalDataRows:N0} dòng · {_preview.Summary.ValidRows:N0} dòng hợp lệ · {_preview.Summary.ErrorCount:N0} vấn đề · {_preview.Summary.WarningCount:N0} cảnh báo.";
    public string SummaryText => InspectionDetail;
    public string PreviewLimitText => _preview is null ? "" : $"Đang xem {_preview.PreviewRows.Count:N0}/{_preview.Summary.TotalDataRows:N0} dòng. Tất cả dòng trong file vẫn được kiểm tra.";
    public string IssueLimitText => _preview is null ? "" : $"Có {_preview.Summary.ErrorCount:N0} vấn đề trên {ErrorRowCount:N0} dòng lỗi và {_preview.Summary.WarningCount:N0} cảnh báo. Hiển thị tối đa {MaximumDisplayedIssues:N0} chi tiết.";
    public string GuidanceText => _preview is null ? "" : BuildGuidanceText();
    public string BlockingHintText => _preview is null
        ? ""
        : _preview.Summary.TotalDataRows == 0
            ? "Hãy chọn một file có ít nhất một dòng sản phẩm rồi kiểm tra lại."
            : GuidanceText;
    public string ConfirmationText => _preview is null ? "" : $"{FileName} · {SelectedWorksheetName ?? "CSV"} · {_preview.Summary.ValidRows:N0} dòng hợp lệ · {PolicyDisplayName} nếu gặp sản phẩm đã có.";
    public string PolicyDisplayName => SelectedPolicy.DisplayName;
    public string ResultHeadline => _result is null ? "" : _result.IsCommitted ? "Đã nhập sản phẩm" : "Lần nhập này chưa được lưu";
    public string ResultText => _result is null ? "" : _result.IsCommitted
        ? $"Thêm mới {_result.CreatedCount:N0} · cập nhật {_result.UpdatedCount:N0} · giữ nguyên {_result.SkippedCount:N0}. Danh sách sản phẩm đã được cập nhật."
        : $"Toàn bộ dữ liệu của lần nhập này chưa được lưu. Đã hoàn tác an toàn; không có dòng nào được tính là nhập thành công.";
    public string ResultNextStep => _result is null ? "" : _result.IsCommitted
        ? "Bạn có thể xem danh sách sản phẩm hoặc nhập một file khác."
        : "Kiểm tra lại các vấn đề được báo, sửa file rồi thực hiện lại một lần nhập mới.";
    public bool ResultIsSuccess => _result?.IsCommitted == true;
    public bool ResultIsFailure => _result is not null && !_result.IsCommitted;
    public string ResultIconText => ResultIsSuccess ? "✓" : "!";
    public int ErrorRowCount => _preview is null ? 0 : _preview.ValidatedRows
        .Count(row => row.Issues.Any(issue => issue.Severity == ProductImportIssueSeverity.Error));
    public string ImportLimitsHint => $"Hỗ trợ Excel .xlsx và CSV · tối đa {_limits.MaximumFileSizeBytes / (1024 * 1024):N0} MB mỗi file.";

    public async Task SelectFileAsync(string filePath)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(filePath)) return;
        ResetForNewFile(filePath);
        await PreviewAsync();
    }

    public bool RequestWindowClose()
    {
        if (!IsBusy) return true;
        _closeAfterBusy = true;
        CancelOperation();
        return false;
    }

    private async Task ChooseFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn tệp sản phẩm để nhập",
            Filter = "Tệp sản phẩm (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false,
            AddExtension = false
        };
        if (dialog.ShowDialog(global::System.Windows.Application.Current?.MainWindow) == true)
            await SelectFileAsync(dialog.FileName);
    }

    private async Task PreviewAsync()
    {
        if (!CanPreview()) return;
        await _operationGate.WaitAsync();
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Đang đọc và kiểm tra tệp...";
            _operationCancellation = new CancellationTokenSource();
            var token = _operationCancellation.Token;
            var references = await LoadReferencesAsync(token);
            if (references is null) return;

            var options = new ProductImportPreviewOptions(
                Limits: _limits,
                References: references,
                WorksheetName: SelectedWorksheetName,
                ColumnMappings: _mappingConfigured ? Headers.Select(header => new ProductImportColumnMapping(header.Header.ColumnIndex, header.SelectedField.CanonicalKey)).ToArray() : null);
            var result = await _previewService.PreviewAsync(FilePath!, options, token);
            ApplyPreview(result);
            StatusMessage = result.FileIssues.Any(issue => issue.Code == "WORKSHEET_SELECTION_REQUIRED")
                ? "Hãy chọn trang tính chứa sản phẩm rồi bấm Kiểm tra lại."
                : result.Summary.TotalDataRows == 0 && result.Summary.EmptyRows > 0
                    ? "Tệp không có dòng dữ liệu đủ điều kiện để nhập."
                    : result.Summary.TotalDataRows == 0 && result.FileIssues.Count == 0
                        ? "Tệp chỉ có header; chưa có sản phẩm để nhập."
                        : result.CanImport ? "Danh sách đã sẵn sàng để nhập." : "Danh sách có vấn đề; xem phần cần xử lý.";
            if (CurrentStep == 0 && result.Headers.Count > 0) CurrentStep = 1;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy thao tác đọc/kiểm tra.";
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            IsBusy = false;
            _operationGate.Release();
            CompleteDeferredClose();
        }
    }

    private async Task<ProductImportReferenceData?> LoadReferencesAsync(CancellationToken token)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var result = await service.ListActiveAsync(token);
        if (result.IsFailure)
        {
            ErrorMessage = result.AppError.Message;
            StatusMessage = "Không thể tải danh mục tham chiếu; chưa đọc dữ liệu import.";
            return null;
        }
        return new ProductImportReferenceData(
            result.Value.ToDictionary(
                category => ProductImportSchemaCatalog.NormalizeHeader(category.Name),
                category => category.Id,
                StringComparer.Ordinal),
            null);
    }

    private async Task ImportAsync()
    {
        if (!CanConfirm()) return;
        await _operationGate.WaitAsync();
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Đang lưu sản phẩm; vui lòng chờ kết quả...";
            _operationCancellation = new CancellationTokenSource();
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IProductImportService>();
            var result = await service.ImportAsync(
                new ProductImportRequest(FilePath!, _preview!, SelectedPolicy.Policy),
                _operationCancellation.Token);
            _result = result;
            OnPropertyChanged(nameof(ImportResult));
            OnPropertyChanged(nameof(HasImportResult));
            OnPropertyChanged(nameof(ResultText));
            OnPropertyChanged(nameof(ResultHeadline));
            OnPropertyChanged(nameof(ResultNextStep));
            OnPropertyChanged(nameof(ResultIsSuccess));
            OnPropertyChanged(nameof(ResultIsFailure));
            OnPropertyChanged(nameof(ResultIconText));
            ViewProductsCommand.NotifyCanExecuteChanged();
            StatusMessage = result.IsCommitted ? "Đã lưu sản phẩm thành công." : "Lần nhập chưa được lưu; dữ liệu đã được hoàn tác.";
            CurrentStep = 3;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy yêu cầu nhập; dữ liệu sẽ không được lưu dở dang.";
        }
        finally
        {
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            IsBusy = false;
            _operationGate.Release();
            CompleteDeferredClose();
        }
    }

    private async Task CancelAsync()
    {
        CancelOperation();
        StatusMessage = "Đang hủy thao tác...";
        await Task.CompletedTask;
    }

    private async Task ImportAnotherFileAsync()
    {
        ResetForNewFile(null);
        CurrentStep = 0;
        await Task.CompletedTask;
    }

    private async Task CloseAsync()
    {
        RequestClose?.Invoke(_result?.IsCommitted == true ? true : false);
        await Task.CompletedTask;
    }

    private async Task ToggleMappingAsync()
    {
        ShowMapping = !ShowMapping;
        OnPropertyChanged(nameof(ShowMappingSection));
        await Task.CompletedTask;
    }

    private async Task ToggleFullPreviewAsync()
    {
        ShowFullPreview = !ShowFullPreview;
        OnPropertyChanged(nameof(ShowCompactPreview));
        await Task.CompletedTask;
    }

    private async Task ToggleIssueDetailsAsync()
    {
        ShowIssueDetails = !ShowIssueDetails;
        await Task.CompletedTask;
    }

    private async Task ViewProductsAsync()
    {
        RequestClose?.Invoke(true);
        await Task.CompletedTask;
    }

    private async Task DownloadTemplateAsync()
    {
        if (_exportDialogService is not null)
            await _exportDialogService.ShowTemplateAsync();
    }

    private void ApplyPreview(ProductImportPreviewResult result)
    {
        _preview = result;
        _previewStale = false;
        WorksheetNames = result.WorksheetNames;
        OnPropertyChanged(nameof(PreviewResult));
        OnPropertyChanged(nameof(WorksheetNames));
        OnPropertyChanged(nameof(HasMultipleWorksheets));
        OnPropertyChanged(nameof(HasSelectedWorksheet));
        if (!string.IsNullOrWhiteSpace(result.SelectedWorksheetName))
        {
            _selectedWorksheetName = result.SelectedWorksheetName;
            OnPropertyChanged(nameof(SelectedWorksheetName));
        }

        Headers.Clear();
        var options = ProductImportSchemaCatalog.Fields.ToDictionary(
            field => field.CanonicalKey,
            field => new ProductImportFieldOption(field.VietnameseLabel, field.CanonicalKey),
            StringComparer.Ordinal);
        foreach (var header in result.Headers)
        {
            var selected = header.CanonicalFieldKey is not null && options.TryGetValue(header.CanonicalFieldKey, out var option)
                ? option
                : new ProductImportFieldOption("Không ánh xạ", null);
            Headers.Add(new ProductImportHeaderRowViewModel(header, selected, MappingChanged));
        }
        ShowMapping = HasMappingAttention;

        PreviewRows.Clear();
        foreach (var row in result.PreviewRows) PreviewRows.Add(new ProductImportPreviewRowViewModel(row));
        Issues.Clear();
        var allIssues = result.FileIssues.Concat(result.ValidatedRows.SelectMany(row => row.Issues)).ToArray();
        foreach (var issue in allIssues.Take(MaximumDisplayedIssues)) Issues.Add(new ProductImportIssueViewModel(issue));
        IssueGroups.Clear();
        foreach (var group in allIssues
            .GroupBy(issue => issue.FieldKey ?? issue.Code, StringComparer.Ordinal)
            .Select(group => new ProductImportIssueGroupViewModel(
                FriendlyIssueGroupTitle(group.First()),
                group.Count(),
                group.First().Message)))
        {
            IssueGroups.Add(group);
        }
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasPreviewRows));
        OnPropertyChanged(nameof(NoPreviewRows));
        OnPropertyChanged(nameof(ShowFullPreviewAction));
        OnPropertyChanged(nameof(ShowBlockingHint));
        OnPropertyChanged(nameof(HasMappingAttention));
        OnPropertyChanged(nameof(MappingStatusText));
        OnPropertyChanged(nameof(ShowMappingSection));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(InspectionConclusion));
        OnPropertyChanged(nameof(InspectionDetail));
        OnPropertyChanged(nameof(PreviewLimitText));
        OnPropertyChanged(nameof(IssueLimitText));
        OnPropertyChanged(nameof(GuidanceText));
        OnPropertyChanged(nameof(BlockingHintText));
        OnPropertyChanged(nameof(ConfirmationText));
        OnPropertyChanged(nameof(HasDuplicateRows));
        OnPropertyChanged(nameof(CanImport));
        OnPropertyChanged(nameof(ShowBlockingHint));
        OnPropertyChanged(nameof(ShowCompactPreview));
        OnPropertyChanged(nameof(ShowMappingSection));
        NotifyCommands();
    }

    private void MappingChanged()
    {
        _mappingConfigured = true;
        InvalidatePreview("Cách ghép cột đã đổi; hãy kiểm tra lại trước khi nhập.");
    }

    private void InvalidatePreview(string message)
    {
        _previewStale = true;
        StatusMessage = message;
        OnPropertyChanged(nameof(IsPreviewStale));
        OnPropertyChanged(nameof(CanImport));
        OnPropertyChanged(nameof(CanRevalidate));
        OnPropertyChanged(nameof(ShowBlockingHint));
        OnPropertyChanged(nameof(ConfirmationText));
        NotifyCommands();
    }

    private void ResetForNewFile(string? path)
    {
        FilePath = path;
        FileName = string.IsNullOrWhiteSpace(path) ? "Chưa chọn tệp" : Path.GetFileName(path);
        _selectedWorksheetName = null;
        _mappingConfigured = false;
        _previewStale = false;
        _preview = null;
        _result = null;
        WorksheetNames = [];
        Headers.Clear();
        PreviewRows.Clear();
        Issues.Clear();
        IssueGroups.Clear();
        CurrentStep = 0;
        ShowMapping = false;
        ShowFullPreview = false;
        ShowIssueDetails = false;
        ErrorMessage = string.Empty;
        StatusMessage = "Đang chuẩn bị đọc thử tệp...";
        OnPropertyChanged(string.Empty);
    }

    private bool CanPreview() => !IsBusy && !string.IsNullOrWhiteSpace(FilePath) && (!HasMultipleWorksheets || HasSelectedWorksheet);
    private bool CanConfirm() => !IsBusy && CurrentStep == 1 && CanImport && HasValidMapping();
    private bool HasValidMapping() => Headers.Count > 0 && ProductImportSchemaCatalog.Fields.Where(field => field.Required).All(required => Headers.Any(header => header.SelectedField.CanonicalKey == required.CanonicalKey));

    private void CancelOperation() => _operationCancellation?.Cancel();

    private void CompleteDeferredClose()
    {
        if (!_closeAfterBusy || IsBusy) return;
        _closeAfterBusy = false;
        RequestClose?.Invoke(_result?.IsCommitted == true ? true : false);
    }

    private void NotifyCommands()
    {
        ChooseFileCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
        ConfirmImportCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ImportAnotherFileCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
        ToggleMappingCommand.NotifyCanExecuteChanged();
        ToggleFullPreviewCommand.NotifyCanExecuteChanged();
        ToggleIssueDetailsCommand.NotifyCanExecuteChanged();
        ViewProductsCommand.NotifyCanExecuteChanged();
        DownloadTemplateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowChooseFileAction));
        OnPropertyChanged(nameof(ShowCheckAction));
        OnPropertyChanged(nameof(ShowImportAction));
        OnPropertyChanged(nameof(ShowChangeFileAction));
        OnPropertyChanged(nameof(ShowViewProductsAction));
        OnPropertyChanged(nameof(ShowResultRetryAction));
    }

    private string BuildGuidanceText()
    {
        var issues = _preview!.FileIssues.Concat(_preview.ValidatedRows.SelectMany(row => row.Issues)).ToArray();
        if (issues.Any(issue => issue.Code is "CATEGORY_NOT_FOUND" or "CATEGORY_INACTIVE" or "CATEGORY_REFERENCE_CHANGED"))
            return "Một hoặc nhiều danh mục chưa có hoặc đang tạm ngừng. Hãy sửa tên danh mục trong file hoặc mở màn Danh mục sản phẩm để xử lý, sau đó đọc lại file.";
        if (issues.Any(issue => issue.Code is "HEADER_REQUIRED_MISSING" or "HEADER_UNKNOWN" or "HEADER_DUPLICATE" or "MAPPING_COLUMN_INVALID" or "MAPPING_DUPLICATE_TARGET"))
            return "Hãy mở phần ghép cột, chọn thông tin đúng cho từng cột nguồn rồi bấm Kiểm tra lại.";
        if (issues.Any(issue => issue.Code is "DUPLICATE_PRODUCT_CODE" or "DUPLICATE_BARCODE" or "IDENTITY_CONFLICT"))
            return "Kiểm tra lại mã sản phẩm và mã vạch; một mã vạch không thể thuộc hai sản phẩm khác nhau.";
        return "Mở chi tiết vấn đề để xem số dòng, trường cần sửa và hướng xử lý.";
    }

    private static string FriendlyIssueGroupTitle(ProductImportIssue issue) =>
        ProductImportSchemaCatalog.FindByCanonicalKey(issue.FieldKey)?.VietnameseLabel ?? issue.Code switch
        {
            "HEADER_REQUIRED_MISSING" => "Thiếu cột bắt buộc",
            "HEADER_UNKNOWN" => "Cột chưa nhận diện",
            "HEADER_DUPLICATE" => "Cột bị lặp",
            "MAPPING_COLUMN_INVALID" or "MAPPING_DUPLICATE_TARGET" => "Ghép cột cần kiểm tra",
            "CATEGORY_NOT_FOUND" => "Danh mục chưa có",
            "CATEGORY_INACTIVE" => "Danh mục đang tạm ngừng",
            "CATEGORY_REFERENCE_CHANGED" => "Danh mục đã thay đổi",
            "DUPLICATE_PRODUCT_CODE" or "DUPLICATE_BARCODE" => "Sản phẩm đã có",
            "IDENTITY_CONFLICT" => "Mã sản phẩm và mã vạch xung đột",
            _ => "Vấn đề trong file"
        };

    private void HandleException(Exception exception)
    {
        PosLog.Error(_logger, exception, "Product import wizard failed.");
        ErrorMessage = "Không thể hoàn tất thao tác import. Vui lòng kiểm tra lại tệp và thử lại.";
        StatusMessage = "Thao tác thất bại.";
    }

    public void Dispose()
    {
        _operationCancellation?.Dispose();
        _operationGate.Dispose();
    }
}

public sealed record ProductImportDuplicatePolicyOption(
    ProductImportDuplicatePolicy Policy,
    string DisplayName,
    string Description);
