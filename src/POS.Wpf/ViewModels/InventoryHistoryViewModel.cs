using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Inventory;
using POS.Domain.Enums;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

public sealed record InventoryMovementFilterOption(
    InventoryMovementType? Value,
    string DisplayName);

public sealed record InventoryReferenceFilterOption(
    string? Value,
    string DisplayName);

/// <summary>
/// Điều phối một pipeline truy vấn duy nhất cho lịch sử tồn kho.
/// WPF không truy cập Product/Inventory repository trực tiếp.
/// </summary>
public sealed class InventoryHistoryViewModel : ViewModelBase, IDisposable
{
    private const int HistoryPageSize = 30;
    private const int SearchDebounceMilliseconds = 300;

    private static readonly CultureInfo VietnameseCulture =
        CultureInfo.GetCultureInfo("vi-VN");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InventoryHistoryViewModel> _logger;
    private readonly IReadOnlyList<InventoryMovementFilterOption> _movementFilters;
    private readonly IReadOnlyList<InventoryReferenceFilterOption> _referenceFilters;
    private readonly DateTime _defaultFromDate = DateTime.Today.AddDays(-30);
    private readonly DateTime _defaultToDate = DateTime.Today;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private CancellationTokenSource? _queryCancellation;
    private CancellationTokenSource? _debounceCancellation;
    private long _queryVersion;
    private bool _isInitialized;
    private bool _isLoading;
    private bool _disposed;
    private bool _suppressAutoApply;
    private int? _productScopeId;
    private string _productSearchTerm = string.Empty;
    private InventoryMovementFilterOption _selectedMovementFilter;
    private InventoryReferenceFilterOption _selectedReferenceFilter;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private InventoryMovementRowViewModel? _selectedMovement;
    private int _pageNumber = 1;
    private int _totalPages = 1;
    private int _totalMovements;
    private int _increasesOnPage;
    private int _decreasesOnPage;
    private long _netChangeOnPage;
    private string _statusMessage = "Đang chuẩn bị lịch sử tồn kho...";
    private string _errorMessage = string.Empty;
    private string _lastUpdatedText = "Chưa tải dữ liệu";

    public InventoryHistoryViewModel(
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryHistoryViewModel> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _movementFilters =
        [
            new(null, "Tất cả loại thay đổi"),
            new(InventoryMovementType.StockIn, "Nhập kho"),
            new(InventoryMovementType.StockOut, "Xuất kho"),
            new(InventoryMovementType.Adjustment, "Điều chỉnh"),
            new(InventoryMovementType.Stocktake, "Kiểm kê"),
            new(InventoryMovementType.Sale, "Bán hàng"),
            new(InventoryMovementType.Refund, "Hoàn hàng"),
            new(InventoryMovementType.OpeningBalance, "Tồn đầu kỳ")
        ];

        _referenceFilters =
        [
            new(null, "Tất cả nguồn"),
            new("ORDER", "Bán hàng"),
            new("RECEIPT", "Nhập hàng"),
            new("REFUND", "Hoàn hàng"),
            new("ORDER_RETURN", "Hoàn hàng theo đơn"),
            new("PRODUCT_IMPORT", "Nhập sản phẩm")
        ];

        _selectedMovementFilter = _movementFilters[0];
        _selectedReferenceFilter = _referenceFilters[0];
        _fromDate = _defaultFromDate;
        _toDate = _defaultToDate;

        ApplyFiltersCommand = new AsyncRelayCommand(
            ApplyFiltersImmediatelyAsync,
            CanLoad,
            HandleCommandException);
        SearchCommand = ApplyFiltersCommand;
        RefreshCommand = new AsyncRelayCommand(
            RefreshAsync,
            CanLoad,
            HandleCommandException);
        ClearFiltersCommand = new AsyncRelayCommand(
            ClearFiltersAsync,
            CanClearFilters,
            HandleCommandException);
        ClearProductScopeCommand = new AsyncRelayCommand(
            ClearProductScopeAsync,
            () => CanLoad() && HasProductScope,
            HandleCommandException);
        ClearSearchCommand = new AsyncRelayCommand(
            ClearSearchAsync,
            () => CanLoad() && HasSearchTerm,
            HandleCommandException);
        PreviousPageCommand = new AsyncRelayCommand(
            PreviousPageAsync,
            CanGoToPreviousPage,
            HandleCommandException);
        NextPageCommand = new AsyncRelayCommand(
            NextPageAsync,
            CanGoToNextPage,
            HandleCommandException);
    }

    public ObservableCollection<InventoryMovementRowViewModel> Movements { get; } = [];
    public IReadOnlyList<InventoryMovementFilterOption> MovementFilters => _movementFilters;
    public IReadOnlyList<InventoryReferenceFilterOption> ReferenceFilters => _referenceFilters;

    public AsyncRelayCommand ApplyFiltersCommand { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ClearFiltersCommand { get; }
    public AsyncRelayCommand ClearProductScopeCommand { get; }
    public AsyncRelayCommand ClearSearchCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }

    public string ProductSearchTerm
    {
        get => _productSearchTerm;
        set
        {
            if (SetProperty(ref _productSearchTerm, value ?? string.Empty))
                OnFilterChanged();
        }
    }

    public InventoryMovementFilterOption SelectedMovementFilter
    {
        get => _selectedMovementFilter;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedMovementFilter, value))
                OnFilterChanged();
        }
    }

    public InventoryReferenceFilterOption SelectedReferenceFilter
    {
        get => _selectedReferenceFilter;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedReferenceFilter, value))
                OnFilterChanged();
        }
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                OnPropertyChanged(nameof(HasDateRangeError));
                OnPropertyChanged(nameof(DateRangeError));
                OnFilterChanged();
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                OnPropertyChanged(nameof(HasDateRangeError));
                OnPropertyChanged(nameof(DateRangeError));
                OnFilterChanged();
            }
        }
    }

    public InventoryMovementRowViewModel? SelectedMovement
    {
        get => _selectedMovement;
        set
        {
            if (!SetProperty(ref _selectedMovement, value)) return;
            OnPropertyChanged(nameof(HasSelectedMovement));
            OnPropertyChanged(nameof(SelectedMovementTitle));
            OnPropertyChanged(nameof(SelectedMovementReason));
            OnPropertyChanged(nameof(SelectedMovementReference));
            OnPropertyChanged(nameof(SelectedMovementAuditText));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value)) return;
            NotifyCommandStates();
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public int PageNumber
    {
        get => _pageNumber;
        private set
        {
            if (!SetProperty(ref _pageNumber, value)) return;
            OnPropertyChanged(nameof(PageText));
            NotifyCommandStates();
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (!SetProperty(ref _totalPages, value)) return;
            OnPropertyChanged(nameof(PageText));
            NotifyCommandStates();
        }
    }

    public int TotalMovements
    {
        get => _totalMovements;
        private set
        {
            if (!SetProperty(ref _totalMovements, value)) return;
            OnPropertyChanged(nameof(TotalMovementsText));
        }
    }

    public int IncreasesOnPage
    {
        get => _increasesOnPage;
        private set
        {
            if (!SetProperty(ref _increasesOnPage, value)) return;
            OnPropertyChanged(nameof(IncreasesOnPageText));
            OnPropertyChanged(nameof(PageDirectionSummary));
        }
    }

    public int DecreasesOnPage
    {
        get => _decreasesOnPage;
        private set
        {
            if (!SetProperty(ref _decreasesOnPage, value)) return;
            OnPropertyChanged(nameof(DecreasesOnPageText));
            OnPropertyChanged(nameof(PageDirectionSummary));
        }
    }

    public long NetChangeOnPage
    {
        get => _netChangeOnPage;
        private set
        {
            if (!SetProperty(ref _netChangeOnPage, value)) return;
            OnPropertyChanged(nameof(NetChangeOnPageText));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value)) return;
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetProperty(ref _lastUpdatedText, value);
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasMovements => Movements.Count > 0;
    public bool HasSelectedMovement => SelectedMovement is not null;
    public bool ShowEmptyState => !IsLoading && !HasError && !HasMovements;
    public bool HasProductScope => _productScopeId.HasValue;
    public bool HasSearchTerm => !string.IsNullOrWhiteSpace(ProductSearchTerm);
    public bool HasDateRangeError => !IsValidDateRange;
    public string DateRangeError => HasDateRangeError
        ? "Ngày bắt đầu không được lớn hơn ngày kết thúc."
        : string.Empty;
    public bool HasAdditionalFilters =>
        SelectedMovementFilter.Value.HasValue ||
        SelectedReferenceFilter.Value is not null ||
        FromDate?.Date != _defaultFromDate.Date ||
        ToDate?.Date != _defaultToDate.Date;
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(ProductSearchTerm) ||
        HasProductScope ||
        HasAdditionalFilters;
    public string FilterSummaryText => HasProductScope
        ? "Đang giới hạn theo sản phẩm đã chọn từ màn trước."
        : HasActiveFilters
            ? "Đang áp dụng bộ lọc bổ sung."
            : "Đang xem toàn bộ lịch sử trong khoảng ngày mặc định.";
    public string PageText => $"Trang {PageNumber:N0} / {TotalPages:N0}";
    public string TotalMovementsText => TotalMovements.ToString("N0", VietnameseCulture);
    public string IncreasesOnPageText => IncreasesOnPage.ToString("N0", VietnameseCulture);
    public string DecreasesOnPageText => DecreasesOnPage.ToString("N0", VietnameseCulture);
    public string PageDirectionSummary => $"{IncreasesOnPageText} / {DecreasesOnPageText}";
    public string NetChangeOnPageText => NetChangeOnPage > 0
        ? $"+{NetChangeOnPage.ToString("N0", VietnameseCulture)}"
        : NetChangeOnPage.ToString("N0", VietnameseCulture);
    public string EmptyStateText => HasActiveFilters
        ? "Không có lịch sử phù hợp với từ khóa và bộ lọc này."
        : "Chưa có lịch sử tồn kho trong khoảng thời gian này. Không có lịch sử phù hợp với bộ lọc hiện tại.";
    public string SelectedMovementTitle => SelectedMovement is null
        ? "Chọn một thay đổi để xem chi tiết"
        : $"{SelectedMovement.MovementTypeText} • {SelectedMovement.ProductIdentityText}";
    public string SelectedMovementReason => SelectedMovement?.Reason ?? string.Empty;
    public string SelectedMovementReference => SelectedMovement?.ReferenceText ?? string.Empty;
    public string SelectedMovementAuditText => SelectedMovement is null
        ? string.Empty
        : $"{SelectedMovement.OccurredAtText} • {SelectedMovement.PerformedByText}";

    public async Task<bool> InitializeAsync(int? productId)
    {
        if (_isInitialized) return true;
        if (productId is <= 0)
        {
            ErrorMessage = "Mã sản phẩm không hợp lệ.";
            return false;
        }

        _productScopeId = productId;
        OnPropertyChanged(nameof(HasProductScope));
        OnPropertyChanged(nameof(FilterSummaryText));
        _isInitialized = true;
        return await LoadMovementsAsync(true, "Đang tải lịch sử tồn kho...");
    }

    private async Task ApplyFiltersImmediatelyAsync()
    {
        CancelDebounce();
        await LoadMovementsAsync(true, "Đang cập nhật lịch sử tồn kho...");
    }

    private async Task RefreshAsync()
    {
        CancelDebounce();
        await LoadMovementsAsync(false, "Đang làm mới lịch sử tồn kho...");
    }

    private async Task ClearFiltersAsync()
    {
        if (!CanClearFilters()) return;

        CancelDebounce();
        _suppressAutoApply = true;
        try
        {
            ProductSearchTerm = string.Empty;
            SelectedMovementFilter = _movementFilters[0];
            SelectedReferenceFilter = _referenceFilters[0];
            FromDate = _defaultFromDate;
            ToDate = _defaultToDate;
            _productScopeId = null;
            OnPropertyChanged(nameof(HasProductScope));
            OnPropertyChanged(nameof(FilterSummaryText));
        }
        finally
        {
            _suppressAutoApply = false;
        }

        await LoadMovementsAsync(true, "Đang tải lại lịch sử theo bộ lọc mặc định...");
    }

    private async Task ClearProductScopeAsync()
    {
        if (!HasProductScope) return;
        _productScopeId = null;
        OnPropertyChanged(nameof(HasProductScope));
        OnPropertyChanged(nameof(FilterSummaryText));
        await LoadMovementsAsync(true, "Đang cập nhật phạm vi lịch sử...");
    }

    private async Task ClearSearchAsync()
    {
        if (!HasSearchTerm) return;
        CancelDebounce();
        _suppressAutoApply = true;
        try
        {
            ProductSearchTerm = string.Empty;
        }
        finally
        {
            _suppressAutoApply = false;
        }

        await LoadMovementsAsync(true, "Đang cập nhật lịch sử tồn kho...");
    }

    private async Task PreviousPageAsync()
    {
        if (!CanGoToPreviousPage()) return;
        var oldPage = PageNumber;
        PageNumber--;
        if (!await LoadMovementsAsync(false, "Đang tải trang trước...")) PageNumber = oldPage;
    }

    private async Task NextPageAsync()
    {
        if (!CanGoToNextPage()) return;
        var oldPage = PageNumber;
        PageNumber++;
        if (!await LoadMovementsAsync(false, "Đang tải trang tiếp theo...")) PageNumber = oldPage;
    }

    private async Task DebouncedApplyAsync(CancellationTokenSource source)
    {
        try
        {
            await Task.Delay(SearchDebounceMilliseconds, source.Token);
            await LoadMovementsAsync(true, "Đang cập nhật lịch sử tồn kho...");
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            HandleCommandException(exception);
        }
        finally
        {
            if (ReferenceEquals(_debounceCancellation, source))
            {
                _debounceCancellation = null;
                source.Dispose();
            }
        }
    }

    private async Task<bool> LoadMovementsAsync(bool resetPage, string loadingMessage)
    {
        if (_disposed) return false;
        if (resetPage) PageNumber = 1;

        var version = Interlocked.Increment(ref _queryVersion);
        var previousCancellation = _queryCancellation;
        previousCancellation?.Cancel();
        previousCancellation?.Dispose();
        var queryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _queryCancellation = queryCancellation;
        var token = queryCancellation.Token;

        if (!TryCreateSearchRequest(out var request, out var validationMessage))
        {
            if (version == Volatile.Read(ref _queryVersion))
            {
                ClearDisplayedResults();
                ErrorMessage = validationMessage;
                StatusMessage = "Hãy sửa điều kiện ngày rồi thử lại.";
                IsLoading = false;
            }
            if (ReferenceEquals(_queryCancellation, queryCancellation))
                _queryCancellation = null;
            queryCancellation.Dispose();
            return false;
        }

        ClearDisplayedResults();
        ErrorMessage = string.Empty;
        StatusMessage = loadingMessage;
        IsLoading = true;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();
            var result = await inventoryService.SearchAsync(request!, token);
            token.ThrowIfCancellationRequested();
            if (version != Volatile.Read(ref _queryVersion)) return false;

            if (result.IsFailure)
            {
                ErrorMessage = "Không thể tải lịch sử tồn kho. Vui lòng thử lại.";
                StatusMessage = "Không thể tải dữ liệu lịch sử.";
                return false;
            }

            var page = result.Value;
            if (!resetPage && page.TotalPages > 0 && PageNumber > page.TotalPages)
            {
                PageNumber = page.TotalPages;
                if (!TryCreateSearchRequest(out request, out validationMessage))
                {
                    ErrorMessage = validationMessage;
                    return false;
                }

                page = (await inventoryService.SearchAsync(request!, token)).Value;
                token.ThrowIfCancellationRequested();
                if (version != Volatile.Read(ref _queryVersion)) return false;
            }

            ApplyPage(page);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            if (version != Volatile.Read(ref _queryVersion)) return false;
            global::POS.Application.Common.PosLog.Error(_logger, exception, "Không thể tải lịch sử tồn kho.");
            ErrorMessage = "Không thể tải lịch sử tồn kho. Vui lòng thử lại.";
            StatusMessage = "Không thể tải dữ liệu lịch sử.";
            return false;
        }
        finally
        {
            if (version == Volatile.Read(ref _queryVersion))
            {
                IsLoading = false;
                if (ReferenceEquals(_queryCancellation, queryCancellation))
                    _queryCancellation = null;
                queryCancellation.Dispose();
            }
        }
    }

    private void ApplyPage(POS.Application.Common.PagedResult<InventoryMovementDto> page)
    {
        foreach (var movement in page.Items)
            Movements.Add(new InventoryMovementRowViewModel(movement));

        PageNumber = page.TotalPages == 0 ? 1 : page.PageNumber;
        TotalPages = Math.Max(1, page.TotalPages);
        TotalMovements = page.TotalCount;
        IncreasesOnPage = Movements.Count(row => row.IsIncrease);
        DecreasesOnPage = Movements.Count(row => row.IsDecrease);
        NetChangeOnPage = Movements.Sum(row => (long)row.QuantityDelta);
        OnPropertyChanged(nameof(HasMovements));
        OnPropertyChanged(nameof(ShowEmptyState));
        StatusMessage = Movements.Count == 0
            ? EmptyStateText
            : $"Đã tải {Movements.Count:N0} lượt thay đổi.";
        LastUpdatedText = $"Cập nhật lúc {DateTimeOffset.Now:HH:mm:ss}";
    }

    private void OnFilterChanged()
    {
        OnPropertyChanged(nameof(HasAdditionalFilters));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(HasSearchTerm));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(FilterSummaryText));
        NotifyCommandStates();
        if (!_isInitialized || _suppressAutoApply || _disposed) return;

        ClearDisplayedResults();
        CancelDebounce();
        var source = new CancellationTokenSource();
        _debounceCancellation = source;
        _ = DebouncedApplyAsync(source);
    }

    private void ClearDisplayedResults()
    {
        Movements.Clear();
        SelectedMovement = null;
        TotalMovements = 0;
        IncreasesOnPage = 0;
        DecreasesOnPage = 0;
        NetChangeOnPage = 0;
        OnPropertyChanged(nameof(HasMovements));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private bool TryCreateSearchRequest(out InventorySearchRequest? request, out string validationMessage)
    {
        request = null;
        validationMessage = string.Empty;
        if (!IsValidDateRange)
        {
            validationMessage = DateRangeError;
            return false;
        }

        try
        {
            request = new InventorySearchRequest(
                productId: _productScopeId,
                movementType: SelectedMovementFilter.Value,
                fromUtc: FromDate.HasValue ? ConvertStartOfLocalDayToUtc(FromDate.Value) : null,
                toUtc: ToDate.HasValue ? ConvertEndOfLocalDayToUtc(ToDate.Value) : null,
                referenceType: SelectedReferenceFilter.Value,
                pageNumber: PageNumber,
                pageSize: HistoryPageSize,
                productSearchTerm: ProductSearchTerm);
            return true;
        }
        catch (ArgumentException exception)
        {
            validationMessage = exception.Message;
            return false;
        }
    }

    private bool IsValidDateRange =>
        !FromDate.HasValue || !ToDate.HasValue || FromDate.Value.Date <= ToDate.Value.Date;

    private static DateTimeOffset ConvertStartOfLocalDayToUtc(DateTime date)
    {
        var localDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Local);
        return new DateTimeOffset(localDate).ToUniversalTime();
    }

    private static DateTimeOffset ConvertEndOfLocalDayToUtc(DateTime date)
    {
        var localEnd = DateTime.SpecifyKind(date.Date.AddDays(1).AddMilliseconds(-1), DateTimeKind.Local);
        return new DateTimeOffset(localEnd).ToUniversalTime();
    }

    private bool CanLoad() => !_isLoading && !_disposed;
    private bool CanClearFilters() => CanLoad() && HasActiveFilters;
    private bool CanGoToPreviousPage() => CanLoad() && PageNumber > 1;
    private bool CanGoToNextPage() => CanLoad() && PageNumber < TotalPages;

    private void CancelDebounce()
    {
        var source = _debounceCancellation;
        _debounceCancellation = null;
        source?.Cancel();
        source?.Dispose();
    }

    private void NotifyCommandStates()
    {
        ApplyFiltersCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        ClearFiltersCommand.NotifyCanExecuteChanged();
        ClearProductScopeCommand.NotifyCanExecuteChanged();
        ClearSearchCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void HandleCommandException(Exception exception)
    {
        global::POS.Application.Common.PosLog.Error(_logger, exception, "Một thao tác lịch sử kho không thể hoàn thành.");
        ErrorMessage = "Thao tác không thể hoàn thành. Vui lòng thử lại.";
        StatusMessage = "Không thể hoàn thành thao tác.";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetimeCancellation.Cancel();
        _debounceCancellation?.Cancel();
        _queryCancellation?.Cancel();
        _debounceCancellation?.Dispose();
        _queryCancellation?.Dispose();
        _lifetimeCancellation.Dispose();
        GC.SuppressFinalize(this);
    }
}
