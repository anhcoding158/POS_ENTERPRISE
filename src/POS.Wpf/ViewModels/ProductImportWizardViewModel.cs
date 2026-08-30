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
                $"{field.VietnameseLabel} · {field.CanonicalKey}",
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
        Field = ProductImportSchemaCatalog.FindByCanonicalKey(issue.FieldKey)?.VietnameseLabel ?? issue.FieldKey ?? "Tệp";
        Severity = issue.Severity == ProductImportIssueSeverity.Error ? "Lỗi" : "Cảnh báo";
        Message = issue.Message;
    }

    public string SourceRow { get; }
    public string Field { get; }
    public string Severity { get; }
    public string Message { get; }
}

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
    private string _statusMessage = "Chọn tệp CSV hoặc XLSX để bắt đầu.";
    private string _errorMessage = string.Empty;
    private bool _closeAfterBusy;

    public ProductImportWizardViewModel(
        IServiceScopeFactory scopeFactory,
        IProductImportPreviewService previewService,
        IPermissionService permissionService,
        ILogger<ProductImportWizardViewModel> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DuplicatePolicies =
        [
            new(ProductImportDuplicatePolicy.Skip, "Bỏ qua", "Giữ nguyên sản phẩm trùng."),
            new(ProductImportDuplicatePolicy.Update, "Cập nhật", "Giữ Product ID/lịch sử; không thay tồn hiện tại."),
            new(ProductImportDuplicatePolicy.Error, "Báo lỗi", "Từ chối toàn bộ lô nếu có trùng.")
        ];
        _selectedPolicy = DuplicatePolicies[0];

        ChooseFileCommand = new AsyncRelayCommand(ChooseFileAsync, () => !IsBusy, HandleException);
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, CanPreview, HandleException);
        ContinueCommand = new AsyncRelayCommand(ContinueAsync, CanContinue, HandleException);
        BackCommand = new AsyncRelayCommand(BackAsync, () => !IsBusy && CurrentStep > 0, HandleException);
        ConfirmImportCommand = new AsyncRelayCommand(ImportAsync, CanConfirm, HandleException);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsBusy, HandleException);
        ImportAnotherFileCommand = new AsyncRelayCommand(ImportAnotherFileAsync, () => !IsBusy, HandleException);
        CloseCommand = new AsyncRelayCommand(CloseAsync, () => !IsBusy, HandleException);
    }

    public event Action<bool?>? RequestClose;

    public ObservableCollection<ProductImportHeaderRowViewModel> Headers { get; } = [];
    public ObservableCollection<ProductImportPreviewRowViewModel> PreviewRows { get; } = [];
    public ObservableCollection<ProductImportIssueViewModel> Issues { get; } = [];
    public IReadOnlyList<ProductImportDuplicatePolicyOption> DuplicatePolicies { get; }
    public AsyncRelayCommand ChooseFileCommand { get; }
    public AsyncRelayCommand PreviewCommand { get; }
    public AsyncRelayCommand ContinueCommand { get; }
    public AsyncRelayCommand BackCommand { get; }
    public AsyncRelayCommand ConfirmImportCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public AsyncRelayCommand ImportAnotherFileCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }

    public string? FilePath { get => _filePath; private set => SetProperty(ref _filePath, value); }
    public string FileName { get => _fileName; private set => SetProperty(ref _fileName, value); }
    public IReadOnlyList<string> WorksheetNames { get; private set; } = [];
    public string? SelectedWorksheetName
    {
        get => _selectedWorksheetName;
        set
        {
            if (!SetProperty(ref _selectedWorksheetName, value)) return;
            if (_preview is not null) InvalidatePreview("Worksheet đã đổi; hãy đọc thử lại.");
            OnPropertyChanged(nameof(HasSelectedWorksheet));
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
            if (_preview is not null) InvalidatePreview("Chính sách xử lý trùng đã đổi; hãy đọc thử lại.");
            OnPropertyChanged(nameof(PolicyExplanation));
            NotifyCommands();
        }
    }

    public ProductImportPreviewResult? PreviewResult => _preview;
    public ProductImportResult? ImportResult => _result;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCommands(); } }
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
    public bool CanImport => _preview?.CanImport == true && !_previewStale && _permissionService.HasPermission(SystemCapability.ManageProducts);
    public string PolicyExplanation => SelectedPolicy.Description;
    public string SummaryText => _preview is null ? "Chưa có dữ liệu preview." :
        $"{_preview.Summary.TotalDataRows:N0} dòng · hợp lệ {_preview.Summary.ValidRows:N0} · lỗi {_preview.Summary.InvalidRows:N0} · cảnh báo {_preview.Summary.WarningCount:N0}";
    public string PreviewLimitText => _preview is null ? "" : $"Đang hiển thị tối đa {_preview.PreviewRows.Count:N0}/{_preview.Summary.TotalDataRows:N0} dòng; validation đã chạy trên toàn bộ tệp.";
    public string IssueLimitText => _preview is null ? "" : $"Đang hiển thị tối đa {Issues.Count:N0} vấn đề; tổng thực tế {_preview.Summary.ErrorCount + _preview.Summary.WarningCount:N0}.";
    public string ResultText => _result is null ? "" : _result.IsCommitted
        ? $"Đã hoàn tất: tạo {_result.CreatedCount:N0}, cập nhật {_result.UpdatedCount:N0}, bỏ qua {_result.SkippedCount:N0}, lỗi {_result.FailedCount:N0}."
        : $"Lô nhập đã rollback an toàn. Đã xử lý yêu cầu {_result.TotalValidRowsRequested:N0} dòng; không có trạng thái thành công giả.";

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
                References: references,
                WorksheetName: SelectedWorksheetName,
                ColumnMappings: _mappingConfigured ? Headers.Select(header => new ProductImportColumnMapping(header.Header.ColumnIndex, header.SelectedField.CanonicalKey)).ToArray() : null);
            var result = await _previewService.PreviewAsync(FilePath!, options, token);
            ApplyPreview(result);
            StatusMessage = result.FileIssues.Any(issue => issue.Code == "WORKSHEET_SELECTION_REQUIRED")
                ? "Hãy chọn worksheet rồi bấm Đọc thử & kiểm tra."
                : result.Summary.TotalDataRows == 0 && result.Summary.EmptyRows > 0
                    ? "Tệp không có dòng dữ liệu đủ điều kiện để nhập."
                    : result.Summary.TotalDataRows == 0 && result.FileIssues.Count == 0
                        ? "Tệp chỉ có header; chưa có sản phẩm để nhập."
                        : result.CanImport ? "Preview hợp lệ; có thể tiếp tục xác nhận." : "Preview có vấn đề; xem lỗi theo dòng trước khi xác nhận.";
            if (CurrentStep == 0 && result.CanImport) CurrentStep = 1;
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

    private async Task ContinueAsync()
    {
        if (!CanContinue()) return;
        CurrentStep = 2;
        StatusMessage = "Kiểm tra lại file, worksheet, chính sách và tác động rồi mới xác nhận nhập.";
        await Task.CompletedTask;
    }

    private async Task BackAsync()
    {
        if (CurrentStep > 0) CurrentStep--;
        await Task.CompletedTask;
    }

    private async Task ImportAsync()
    {
        if (!CanConfirm()) return;
        await _operationGate.WaitAsync();
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Đang nhập theo transaction; vui lòng chờ kết quả commit hoặc rollback...";
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
            StatusMessage = result.IsCommitted ? "Nhập sản phẩm đã commit thành công." : "Nhập sản phẩm không commit; transaction đã rollback.";
            if (result.IsCommitted) CurrentStep = 3;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy yêu cầu nhập; transaction sẽ rollback an toàn.";
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
            field => new ProductImportFieldOption($"{field.VietnameseLabel} · {field.CanonicalKey}", field.CanonicalKey),
            StringComparer.Ordinal);
        foreach (var header in result.Headers)
        {
            var selected = header.CanonicalFieldKey is not null && options.TryGetValue(header.CanonicalFieldKey, out var option)
                ? option
                : new ProductImportFieldOption("Không ánh xạ", null);
            Headers.Add(new ProductImportHeaderRowViewModel(header, selected, MappingChanged));
        }

        PreviewRows.Clear();
        foreach (var row in result.PreviewRows) PreviewRows.Add(new ProductImportPreviewRowViewModel(row));
        Issues.Clear();
        var allIssues = result.FileIssues.Concat(result.ValidatedRows.SelectMany(row => row.Issues)).ToArray();
        foreach (var issue in allIssues.Take(MaximumDisplayedIssues)) Issues.Add(new ProductImportIssueViewModel(issue));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(PreviewLimitText));
        OnPropertyChanged(nameof(IssueLimitText));
        OnPropertyChanged(nameof(CanImport));
        NotifyCommands();
    }

    private void MappingChanged()
    {
        _mappingConfigured = true;
        InvalidatePreview("Mapping đã đổi; hãy đọc thử & kiểm tra lại trước khi tiếp tục.");
    }

    private void InvalidatePreview(string message)
    {
        _previewStale = true;
        StatusMessage = message;
        OnPropertyChanged(nameof(IsPreviewStale));
        OnPropertyChanged(nameof(CanImport));
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
        CurrentStep = 0;
        ErrorMessage = string.Empty;
        StatusMessage = "Đang chuẩn bị đọc thử tệp...";
        OnPropertyChanged(string.Empty);
    }

    private bool CanPreview() => !IsBusy && !string.IsNullOrWhiteSpace(FilePath) && (!HasMultipleWorksheets || HasSelectedWorksheet);
    private bool CanContinue() => !IsBusy && CurrentStep == 1 && _preview?.CanImport == true && !_previewStale && HasValidMapping();
    private bool CanConfirm() => !IsBusy && CurrentStep == 2 && CanImport && HasValidMapping();
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
        ContinueCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        ConfirmImportCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ImportAnotherFileCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

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
