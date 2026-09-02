using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Categories;
using POS.Application.DTOs.Products;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public sealed record BulkProductOperationOption(
    BulkProductOperationType Operation,
    string DisplayName,
    string Description);

public sealed record BulkProductStatusOption(bool Value, string DisplayName);

public sealed class BulkProductPreviewRowViewModel
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public BulkProductPreviewRowViewModel(ProductRowViewModel row, BulkProductOperationType operation)
    {
        ProductCode = row.Code;
        ProductName = row.Name;
        BeforeValue = operation switch
        {
            BulkProductOperationType.SetPrices => $"Bán {row.SalePrice:N0} ₫ · Vốn {row.CostPrice:N0} ₫",
            BulkProductOperationType.SetCategory => row.CategoryName,
            BulkProductOperationType.SetActiveState => row.StatusText,
            BulkProductOperationType.SetMinimumStock => $"{row.MinimumStock.ToString("N0", VietnameseCulture)} · tồn thực tế {row.StockDisplay}",
            _ => "—"
        };
        AfterValue = "—";
        ResultText = "Chưa có bản xem trước";
    }

    public BulkProductPreviewRowViewModel(BulkProductPreviewRow row)
    {
        ProductCode = row.ProductCode;
        ProductName = row.ProductName;
        BeforeValue = row.BeforeValue;
        AfterValue = row.AfterValue;
        ResultText = row.ErrorMessage ?? (row.WillChange ? "Sẽ thay đổi" : "Không đổi");
        HasError = row.ErrorMessage is not null;
    }

    public string ProductCode { get; }
    public string ProductName { get; }
    public string BeforeValue { get; }
    public string AfterValue { get; }
    public string ResultText { get; }
    public bool HasError { get; }
}

public sealed class BulkProductViewModel : ViewModelBase, IDisposable
{
    private readonly IReadOnlyList<ProductRowViewModel> _selectedProducts;
    private readonly IBulkProductOperationService _service;
    private CancellationTokenSource? _cancellation;
    private BulkProductPreview? _preview;
    private BulkProductOperationOption _selectedOperation;
    private CategoryOptionDto? _selectedCategory;
    private string _salePriceText = string.Empty;
    private string _costPriceText = string.Empty;
    private string _minimumStockText = string.Empty;
    private BulkProductStatusOption? _selectedStatus;
    private bool _isBusy;
    private string _statusMessage = "Kiểm tra trước để xem thay đổi dự kiến.";
    private string _errorMessage = string.Empty;

    public BulkProductViewModel(
        IReadOnlyList<ProductRowViewModel> selectedProducts,
        IBulkProductOperationService service,
        IReadOnlyList<CategoryOptionDto> categories)
    {
        _selectedProducts = selectedProducts ?? throw new ArgumentNullException(nameof(selectedProducts));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        Categories = categories ?? throw new ArgumentNullException(nameof(categories));
        Operations =
        [
            new(BulkProductOperationType.SetPrices, "Cập nhật giá", "Đặt lại giá bán và giá vốn cho các sản phẩm đã chọn."),
            new(BulkProductOperationType.SetCategory, "Chuyển danh mục", "Chọn một danh mục đang hoạt động; không tự tạo danh mục."),
            new(BulkProductOperationType.SetActiveState, "Đổi trạng thái bán", "Chuyển giữa Đang bán và Ngừng bán; không phải lưu trữ."),
            new(BulkProductOperationType.SetMinimumStock, "Đặt tồn tối thiểu", "Chỉ đổi ngưỡng cảnh báo, không đổi tồn thực tế.")
        ];
        _selectedOperation = Operations[0];
        StatusOptions = [new(true, "Đang bán"), new(false, "Ngừng bán")];
        PreviewCommand = new AsyncRelayCommand(PreviewAsync, () => !IsBusy, HandleException);
        ConfirmCommand = new AsyncRelayCommand(ConfirmAsync, () => !IsBusy && _preview?.CanConfirm == true && _preview.ChangeCount > 0, HandleException);
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsBusy, HandleException);
        CloseCommand = new AsyncRelayCommand(CloseAsync, () => !IsBusy, HandleException);
        LoadReferenceRows();
    }

    public event Action<bool?>? RequestClose;
    public IReadOnlyList<BulkProductOperationOption> Operations { get; }
    public IReadOnlyList<CategoryOptionDto> Categories { get; }
    public IReadOnlyList<BulkProductStatusOption> StatusOptions { get; }
    public ObservableCollection<BulkProductPreviewRowViewModel> PreviewRows { get; } = [];
    public AsyncRelayCommand PreviewCommand { get; }
    public AsyncRelayCommand ConfirmCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }
    public AsyncRelayCommand CloseCommand { get; }
    public int SelectedCount => _selectedProducts.Count;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCommands(); } }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool HasPreview => _preview is not null;
    public bool HasErrors => _preview?.Errors.Count > 0;
    public string SummaryText => _preview is null
        ? "Chưa có bản xem trước."
        : $"Sẽ thay đổi {_preview.ChangeCount:N0} sản phẩm; {_preview.NoOpCount:N0} sản phẩm không cần đổi.";
    public string SelectionText => $"Đã chọn {_selectedProducts.Count:N0} sản phẩm trên trang hiện tại.";
    public string OperationDescription => _selectedOperation.Description;
    public string PreviewHeading => _preview is null ? "Sản phẩm đã chọn và thay đổi dự kiến" : "Xem trước thay đổi";
    public bool IsPriceOperation => SelectedOperation.Operation == BulkProductOperationType.SetPrices;
    public bool IsCategoryOperation => SelectedOperation.Operation == BulkProductOperationType.SetCategory;
    public bool IsStatusOperation => SelectedOperation.Operation == BulkProductOperationType.SetActiveState;
    public bool IsMinimumStockOperation => SelectedOperation.Operation == BulkProductOperationType.SetMinimumStock;
    public BulkProductOperationOption SelectedOperation
    {
        get => _selectedOperation;
        set { ArgumentNullException.ThrowIfNull(value); if (!SetProperty(ref _selectedOperation, value)) return; InvalidatePreview(); OnPropertyChanged(nameof(OperationDescription)); }
    }
    public CategoryOptionDto? SelectedCategory
    {
        get => _selectedCategory;
        set { if (!SetProperty(ref _selectedCategory, value)) return; InvalidatePreview(); }
    }
    public string SalePriceText { get => _salePriceText; set { if (SetProperty(ref _salePriceText, value)) InvalidatePreview(); } }
    public string CostPriceText { get => _costPriceText; set { if (SetProperty(ref _costPriceText, value)) InvalidatePreview(); } }
    public string MinimumStockText { get => _minimumStockText; set { if (SetProperty(ref _minimumStockText, value)) InvalidatePreview(); } }
    public BulkProductStatusOption? SelectedStatus { get => _selectedStatus; set { if (SetProperty(ref _selectedStatus, value)) InvalidatePreview(); } }
    public bool IsActive
    {
        get => SelectedStatus?.Value ?? true;
        set => SelectedStatus = StatusOptions.First(option => option.Value == value);
    }

    private async Task PreviewAsync()
    {
        if (!TryBuildRequest(out var request, out var error)) { ErrorMessage = error; return; }
        await ExecuteAsync(async token =>
        {
            var result = await _service.PreviewAsync(request, token);
            if (result.IsFailure) { ErrorMessage = result.AppError.Message; _preview = null; }
            else ApplyPreview(result.Value);
        }, "Đang kiểm tra thay đổi dự kiến...");
    }

    private async Task ConfirmAsync()
    {
        if (_preview is null || !_preview.CanConfirm) return;
        await ExecuteAsync(async token =>
        {
            var result = await _service.CommitAsync(_preview, token);
            if (result.IsFailure) { ErrorMessage = result.AppError.Message; return; }
            if (result.Value.IsCommitted)
            {
                StatusMessage = $"Đã cập nhật {result.Value.ChangedCount:N0} sản phẩm; {result.Value.NoOpCount:N0} sản phẩm không đổi.";
                RequestClose?.Invoke(true);
            }
            else
            {
                ErrorMessage = result.Value.Errors.Count == 0
                    ? "Thao tác chưa được lưu."
                    : result.Value.Errors[0].Message;
                StatusMessage = "Chưa có thay đổi nào được lưu; toàn bộ thao tác đã được hoàn tác.";
            }
        }, "Đang lưu thay đổi; vui lòng chờ kết quả...");
    }

    private async Task CancelAsync() { _cancellation?.Cancel(); await Task.CompletedTask; }
    private async Task CloseAsync() { RequestClose?.Invoke(false); await Task.CompletedTask; }

    private async Task ExecuteAsync(Func<CancellationToken, Task> action, string status)
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = string.Empty; StatusMessage = status;
        _cancellation = new CancellationTokenSource();
        try { await action(_cancellation.Token); }
        catch (OperationCanceledException) { StatusMessage = "Đã hủy; chưa có thay đổi nào được lưu."; }
        finally { _cancellation.Dispose(); _cancellation = null; IsBusy = false; NotifyCommands(); }
    }

    private bool TryBuildRequest(out BulkProductOperationRequest request, out string error)
    {
        var selection = new BulkProductSelection[_selectedProducts.Count];
        for (var index = 0; index < _selectedProducts.Count; index++)
        {
            var row = _selectedProducts[index];
            selection[index] = new BulkProductSelection(row.Id, row.UpdatedAtUtc);
        }

        request = new(SelectedOperation.Operation, selection);
        error = string.Empty;
        if (SelectedOperation.Operation == BulkProductOperationType.SetPrices)
        {
            if (!TryParseLong(SalePriceText, out var sale) || !TryParseLong(CostPriceText, out var cost)) { error = "Nhập giá bán và giá vốn là số nguyên không âm."; return false; }
            request = request with { SalePrice = sale, CostPrice = cost };
        }
        else if (SelectedOperation.Operation == BulkProductOperationType.SetCategory)
        {
            if (SelectedCategory is null) { error = "Chọn danh mục đang hoạt động."; return false; }
            request = request with { CategoryId = SelectedCategory.Id };
        }
        else if (SelectedOperation.Operation == BulkProductOperationType.SetActiveState)
        {
            if (SelectedStatus is null) { error = "Chọn trạng thái bán mới."; return false; }
            request = request with { IsActive = SelectedStatus.Value };
        }
        else if (!TryParseInt(MinimumStockText, out var minimumStock))
        { error = "Nhập tồn tối thiểu là số nguyên không âm."; return false; }
        else request = request with { MinimumStock = minimumStock };
        return true;
    }

    private void ApplyPreview(BulkProductPreview preview)
    {
        _preview = preview; PreviewRows.Clear();
        foreach (var row in preview.Rows) PreviewRows.Add(new BulkProductPreviewRowViewModel(row));
        StatusMessage = !preview.CanConfirm ? "Có vấn đề cần xử lý trước khi lưu." : preview.ChangeCount == 0 ? "Đã kiểm tra: không có sản phẩm nào cần đổi; chưa có gì được lưu." : "Đã kiểm tra. Hãy xem lại rồi xác nhận lưu.";
        OnPropertyChanged(string.Empty); NotifyCommands();
    }

    private void InvalidatePreview() { _preview = null; LoadReferenceRows(); OnPropertyChanged(nameof(HasPreview)); OnPropertyChanged(nameof(SummaryText)); OnPropertyChanged(nameof(PreviewHeading)); OnPropertyChanged(nameof(IsPriceOperation)); OnPropertyChanged(nameof(IsCategoryOperation)); OnPropertyChanged(nameof(IsStatusOperation)); OnPropertyChanged(nameof(IsMinimumStockOperation)); ConfirmCommand.NotifyCanExecuteChanged(); }
    private void LoadReferenceRows()
    {
        PreviewRows.Clear();
        foreach (var row in _selectedProducts)
            PreviewRows.Add(new BulkProductPreviewRowViewModel(row, SelectedOperation.Operation));
    }
    private static bool TryParseLong(string value, out long result) => long.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0;
    private static bool TryParseInt(string value, out int result) => int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0;
    private void NotifyCommands() { PreviewCommand.NotifyCanExecuteChanged(); ConfirmCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); CloseCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(HasPreview)); OnPropertyChanged(nameof(HasErrors)); OnPropertyChanged(nameof(SummaryText)); }
    private void HandleException(Exception exception)
    {
        Trace.TraceError(
            "Bulk product operation failed. ExceptionType={0}",
            exception.GetType().FullName);
        ErrorMessage =
            "Không thể hoàn tất thao tác hàng loạt. " +
            "Hãy kiểm tra lại dữ liệu và thử lại.";
        StatusMessage = "Thao tác hàng loạt thất bại.";
    }
    public void Dispose() { _cancellation?.Cancel(); _cancellation?.Dispose(); }
}
