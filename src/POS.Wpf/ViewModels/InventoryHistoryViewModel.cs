using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;
using POS.Domain.Enums;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Một lựa chọn sản phẩm trong bộ lọc lịch sử kho.
/// </summary>
public sealed record InventoryProductFilterOption(
    int? ProductId,
    string DisplayName);

/// <summary>
/// Một lựa chọn loại biến động trong bộ lọc.
/// </summary>
public sealed record InventoryMovementFilterOption(
    InventoryMovementType? Value,
    string DisplayName);

/// <summary>
/// ViewModel của cửa sổ lịch sử tồn kho.
///
/// Không giữ IInventoryService, IProductService hoặc DbContext.
/// Mỗi lần tìm kiếm đều tạo scope riêng.
/// </summary>
public sealed class InventoryHistoryViewModel :
    ViewModelBase
{
    private const int HistoryPageSize = 30;
    private const int ProductLookupPageSize = 40;

    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private static readonly
        InventoryProductFilterOption
        AllProductsOption =
            new(
                ProductId: null,
                DisplayName: "Tất cả sản phẩm");

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ILogger<
        InventoryHistoryViewModel>
        _logger;

    private readonly IReadOnlyList<
        InventoryMovementFilterOption>
        _movementFilters;

    private int? _initialProductId;

    private bool _isInitialized;
    private bool _isLoading;

    private string _productSearchTerm =
        string.Empty;

    private string _referenceType =
        string.Empty;

    private InventoryProductFilterOption
        _selectedProductFilter =
            AllProductsOption;

    private InventoryMovementFilterOption
        _selectedMovementFilter;

    private DateTime? _fromDate =
        DateTime.Today.AddDays(-30);

    private DateTime? _toDate =
        DateTime.Today;

    private InventoryMovementRowViewModel?
        _selectedMovement;

    private int _pageNumber = 1;
    private int _totalPages = 1;

    private int _totalMovements;
    private int _increasesOnPage;
    private int _decreasesOnPage;
    private int _neutralOnPage;

    private long _netChangeOnPage;

    private string _statusMessage =
        "Đang chuẩn bị lịch sử tồn kho...";

    private string _errorMessage =
        string.Empty;

    private string _lastUpdatedText =
        "Chưa tải dữ liệu";

    public InventoryHistoryViewModel(
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryHistoryViewModel> logger)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        _movementFilters =
        [
            new InventoryMovementFilterOption(
                Value: null,
                DisplayName: "Tất cả nghiệp vụ"),

            new InventoryMovementFilterOption(
                InventoryMovementType.StockIn,
                "Nhập kho"),

            new InventoryMovementFilterOption(
                InventoryMovementType.StockOut,
                "Xuất kho"),

            new InventoryMovementFilterOption(
                InventoryMovementType.Adjustment,
                "Điều chỉnh"),

            new InventoryMovementFilterOption(
                InventoryMovementType.Stocktake,
                "Kiểm kê"),

            new InventoryMovementFilterOption(
                InventoryMovementType.Sale,
                "Bán hàng"),

            new InventoryMovementFilterOption(
                InventoryMovementType.Refund,
                "Hoàn hàng"),

            new InventoryMovementFilterOption(
                InventoryMovementType.OpeningBalance,
                "Tồn đầu kỳ")
        ];

        _selectedMovementFilter =
            _movementFilters[0];

        ProductOptions.Add(
            AllProductsOption);

        SearchCommand =
            new AsyncRelayCommand(
                SearchAsync,
                CanLoad,
                HandleCommandException);

        RefreshCommand =
            new AsyncRelayCommand(
                RefreshAsync,
                CanLoad,
                HandleCommandException);

        SearchProductsCommand =
            new AsyncRelayCommand(
                SearchProductsAsync,
                CanLoad,
                HandleCommandException);

        ResetFiltersCommand =
            new AsyncRelayCommand(
                ResetFiltersAsync,
                CanLoad,
                HandleCommandException);

        PreviousPageCommand =
            new AsyncRelayCommand(
                PreviousPageAsync,
                CanGoToPreviousPage,
                HandleCommandException);

        NextPageCommand =
            new AsyncRelayCommand(
                NextPageAsync,
                CanGoToNextPage,
                HandleCommandException);
    }

    public ObservableCollection<
        InventoryProductFilterOption>
        ProductOptions
    { get; } = [];

    public IReadOnlyList<
        InventoryMovementFilterOption>
        MovementFilters =>
            _movementFilters;

    public ObservableCollection<
        InventoryMovementRowViewModel>
        Movements
    { get; } = [];

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand SearchProductsCommand
    {
        get;
    }

    public AsyncRelayCommand ResetFiltersCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    public AsyncRelayCommand NextPageCommand { get; }

    public string ProductSearchTerm
    {
        get => _productSearchTerm;

        set => SetProperty(
            ref _productSearchTerm,
            value);
    }

    public string ReferenceType
    {
        get => _referenceType;

        set => SetProperty(
            ref _referenceType,
            value);
    }

    public InventoryProductFilterOption
        SelectedProductFilter
    {
        get => _selectedProductFilter;

        set
        {
            ArgumentNullException.ThrowIfNull(
                value);

            SetProperty(
                ref _selectedProductFilter,
                value);
        }
    }

    public InventoryMovementFilterOption
        SelectedMovementFilter
    {
        get => _selectedMovementFilter;

        set
        {
            ArgumentNullException.ThrowIfNull(
                value);

            SetProperty(
                ref _selectedMovementFilter,
                value);
        }
    }

    public DateTime? FromDate
    {
        get => _fromDate;

        set => SetProperty(
            ref _fromDate,
            value);
    }

    public DateTime? ToDate
    {
        get => _toDate;

        set => SetProperty(
            ref _toDate,
            value);
    }

    public InventoryMovementRowViewModel?
        SelectedMovement
    {
        get => _selectedMovement;

        set
        {
            if (!SetProperty(
                    ref _selectedMovement,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasSelectedMovement));

            OnPropertyChanged(
                nameof(SelectedMovementTitle));

            OnPropertyChanged(
                nameof(SelectedMovementReason));

            OnPropertyChanged(
                nameof(SelectedMovementReference));

            OnPropertyChanged(
                nameof(SelectedMovementAuditText));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;

        private set
        {
            if (!SetProperty(
                    ref _isLoading,
                    value))
            {
                return;
            }

            NotifyCommandStates();
        }
    }

    public int PageNumber
    {
        get => _pageNumber;

        private set
        {
            if (!SetProperty(
                    ref _pageNumber,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(PageText));

            NotifyCommandStates();
        }
    }

    public int TotalPages
    {
        get => _totalPages;

        private set
        {
            if (!SetProperty(
                    ref _totalPages,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(PageText));

            NotifyCommandStates();
        }
    }

    public int TotalMovements
    {
        get => _totalMovements;

        private set
        {
            if (!SetProperty(
                    ref _totalMovements,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(TotalMovementsText));
        }
    }

    public int IncreasesOnPage
    {
        get => _increasesOnPage;

        private set
        {
            if (!SetProperty(
                    ref _increasesOnPage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(IncreasesOnPageText));
        }
    }

    public int DecreasesOnPage
    {
        get => _decreasesOnPage;

        private set
        {
            if (!SetProperty(
                    ref _decreasesOnPage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(DecreasesOnPageText));
        }
    }

    public int NeutralOnPage
    {
        get => _neutralOnPage;

        private set => SetProperty(
            ref _neutralOnPage,
            value);
    }

    public long NetChangeOnPage
    {
        get => _netChangeOnPage;

        private set
        {
            if (!SetProperty(
                    ref _netChangeOnPage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(NetChangeOnPageText));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;

        private set => SetProperty(
            ref _statusMessage,
            value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;

        private set
        {
            if (!SetProperty(
                    ref _errorMessage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasError));
        }
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;

        private set => SetProperty(
            ref _lastUpdatedText,
            value);
    }

    public bool HasError =>
        !string.IsNullOrWhiteSpace(
            ErrorMessage);

    public bool HasMovements =>
        Movements.Count > 0;

    public bool HasSelectedMovement =>
        SelectedMovement is not null;

    public string PageText =>
        $"Trang {PageNumber:N0} / {TotalPages:N0}";

    public string TotalMovementsText =>
        TotalMovements.ToString(
            "N0",
            VietnameseCulture);

    public string IncreasesOnPageText =>
        IncreasesOnPage.ToString(
            "N0",
            VietnameseCulture);

    public string DecreasesOnPageText =>
        DecreasesOnPage.ToString(
            "N0",
            VietnameseCulture);

    public string NetChangeOnPageText =>
        NetChangeOnPage switch
        {
            > 0 =>
                $"+{NetChangeOnPage.ToString(
                    "N0",
                    VietnameseCulture)}",

            _ =>
                NetChangeOnPage.ToString(
                    "N0",
                    VietnameseCulture)
        };

    public string SelectedMovementTitle =>
        SelectedMovement is null
            ? "Chọn một biến động để xem chi tiết"
            : $"{SelectedMovement.MovementTypeText} • " +
              $"{SelectedMovement.ProductIdentityText}";

    public string SelectedMovementReason =>
        SelectedMovement?.Reason ??
        "Lý do và thông tin kiểm toán sẽ hiển thị tại đây.";

    public string SelectedMovementReference =>
        SelectedMovement?.ReferenceText ??
        "Không có chứng từ được chọn.";

    public string SelectedMovementAuditText =>
        SelectedMovement is null
            ? "Chưa chọn bản ghi"
            : $"{SelectedMovement.OccurredAtText} • " +
              $"{SelectedMovement.PerformedByText}";

    public async Task<bool> InitializeAsync(
        int? productId)
    {
        if (_isInitialized)
        {
            return true;
        }

        if (productId.HasValue &&
            productId.Value <= 0)
        {
            ErrorMessage =
                "Mã sản phẩm không hợp lệ.";

            return false;
        }

        _initialProductId =
            productId;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var productsLoaded =
                await LoadProductOptionsCoreAsync(
                    productId);

            if (!productsLoaded)
            {
                return false;
            }

            var movementsLoaded =
                await LoadMovementsCoreAsync();

            if (!movementsLoaded)
            {
                return false;
            }

            _isInitialized = true;

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể khởi tạo cửa sổ lịch sử kho.");

            ErrorMessage =
                "Không thể tải lịch sử kho. " +
                exception.GetBaseException().Message;

            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchAsync()
    {
        PageNumber = 1;

        await LoadMovementsAsync();
    }

    private Task RefreshAsync()
    {
        return LoadMovementsAsync();
    }

    private async Task SearchProductsAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var selectedProductId =
                SelectedProductFilter.ProductId;

            var loaded =
                await LoadProductOptionsCoreAsync(
                    selectedProductId);

            if (loaded)
            {
                StatusMessage =
                    "Danh sách sản phẩm trong bộ lọc đã được cập nhật.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ResetFiltersAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            ProductSearchTerm =
                string.Empty;

            ReferenceType =
                string.Empty;

            SelectedMovementFilter =
                MovementFilters[0];

            FromDate =
                DateTime.Today.AddDays(-30);

            ToDate =
                DateTime.Today;

            await LoadProductOptionsCoreAsync(
                _initialProductId);

            PageNumber = 1;

            await LoadMovementsCoreAsync();

            StatusMessage =
                "Bộ lọc đã được đặt lại.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PreviousPageAsync()
    {
        if (!CanGoToPreviousPage())
        {
            return;
        }

        var previousPage =
            PageNumber;

        PageNumber--;

        var loaded =
            await LoadMovementsAsync();

        if (!loaded)
        {
            PageNumber =
                previousPage;
        }
    }

    private async Task NextPageAsync()
    {
        if (!CanGoToNextPage())
        {
            return;
        }

        var previousPage =
            PageNumber;

        PageNumber++;

        var loaded =
            await LoadMovementsAsync();

        if (!loaded)
        {
            PageNumber =
                previousPage;
        }
    }

    private async Task<bool> LoadMovementsAsync()
    {
        if (IsLoading)
        {
            return false;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            return await LoadMovementsCoreAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<bool> LoadMovementsCoreAsync()
    {
        if (!TryCreateSearchRequest(
                out var request,
                out var validationMessage))
        {
            ErrorMessage =
                validationMessage;

            return false;
        }

        StatusMessage =
            "Đang tải lịch sử tồn kho...";

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var inventoryService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IInventoryService>();

            var result =
                await inventoryService.SearchAsync(
                    request!);

            if (result.IsFailure)
            {
                ErrorMessage =
                    result.Error.Message;

                _logger.LogWarning(
                    "Tải lịch sử kho thất bại: " +
                    "{ErrorCode} - {ErrorMessage}",
                    result.Error.Code,
                    result.Error.Message);

                return false;
            }

            var page =
                result.Value;

            var rows =
                page.Items
                    .Select(
                        movement =>
                            new InventoryMovementRowViewModel(
                                movement))
                    .ToArray();

            Movements.Clear();

            foreach (var row in rows)
            {
                Movements.Add(row);
            }

            SelectedMovement = null;

            PageNumber =
                page.PageNumber;

            TotalPages =
                Math.Max(
                    1,
                    page.TotalPages);

            TotalMovements =
                page.TotalCount;

            IncreasesOnPage =
                rows.Count(
                    movement =>
                        movement.IsIncrease);

            DecreasesOnPage =
                rows.Count(
                    movement =>
                        movement.IsDecrease);

            NeutralOnPage =
                rows.Count(
                    movement =>
                        movement.IsNeutral);

            NetChangeOnPage =
                rows.Sum(
                    movement =>
                        (long)
                        movement.QuantityDelta);

            OnPropertyChanged(
                nameof(HasMovements));

            StatusMessage =
                rows.Length == 0
                    ? "Không tìm thấy biến động kho phù hợp."
                    : $"Đã tải {rows.Length:N0} biến động kho.";

            LastUpdatedText =
                $"Cập nhật lúc " +
                $"{DateTimeOffset.Now:HH:mm:ss}";

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể tải lịch sử tồn kho.");

            ErrorMessage =
                "Không thể tải lịch sử tồn kho. " +
                exception.GetBaseException().Message;

            return false;
        }
    }

    private async Task<bool>
        LoadProductOptionsCoreAsync(
            int? productIdToPreserve)
    {
        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var productService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IProductService>();

            var request =
                new ProductSearchRequest(
                    searchTerm:
                        ProductSearchTerm,

                    categoryId:
                        null,

                    isActive:
                        null,

                    isLowStock:
                        null,

                    pageNumber:
                        1,

                    pageSize:
                        ProductLookupPageSize);

            var searchResult =
                await productService.SearchAsync(
                    request);

            if (searchResult.IsFailure)
            {
                ErrorMessage =
                    searchResult.Error.Message;

                return false;
            }

            var options =
                searchResult.Value.Items
                    .Select(MapProductOption)
                    .ToList();

            if (productIdToPreserve.HasValue &&
                options.All(
                    option =>
                        option.ProductId !=
                        productIdToPreserve.Value))
            {
                var detailsResult =
                    await productService.GetByIdAsync(
                        productIdToPreserve.Value);

                if (detailsResult.IsSuccess)
                {
                    options.Insert(
                        0,
                        new InventoryProductFilterOption(
                            detailsResult.Value.Id,
                            $"{detailsResult.Value.Code} — " +
                            $"{detailsResult.Value.Name}"));
                }
            }

            ProductOptions.Clear();

            ProductOptions.Add(
                AllProductsOption);

            foreach (var option in options
                         .GroupBy(
                             option =>
                                 option.ProductId)
                         .Select(
                             group =>
                                 group.First()))
            {
                ProductOptions.Add(
                    option);
            }

            SelectedProductFilter =
                productIdToPreserve.HasValue
                    ? ProductOptions
                          .FirstOrDefault(
                              option =>
                                  option.ProductId ==
                                  productIdToPreserve.Value)
                      ??
                      AllProductsOption
                    : AllProductsOption;

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể tải danh sách sản phẩm cho bộ lọc kho.");

            ErrorMessage =
                "Không thể tải danh sách sản phẩm. " +
                exception.GetBaseException().Message;

            return false;
        }
    }

    private bool TryCreateSearchRequest(
        out InventorySearchRequest? request,
        out string validationMessage)
    {
        request = null;
        validationMessage = string.Empty;

        if (FromDate.HasValue &&
            ToDate.HasValue &&
            FromDate.Value.Date >
            ToDate.Value.Date)
        {
            validationMessage =
                "Ngày bắt đầu không được lớn hơn ngày kết thúc.";

            return false;
        }

        try
        {
            request =
                new InventorySearchRequest(
                    productId:
                        SelectedProductFilter.ProductId,

                    movementType:
                        SelectedMovementFilter.Value,

                    fromUtc:
                        FromDate.HasValue
                            ? ConvertStartOfLocalDayToUtc(
                                FromDate.Value)
                            : null,

                    toUtc:
                        ToDate.HasValue
                            ? ConvertEndOfLocalDayToUtc(
                                ToDate.Value)
                            : null,

                    referenceType:
                        ReferenceType,

                    pageNumber:
                        PageNumber,

                    pageSize:
                        HistoryPageSize);

            return true;
        }
        catch (ArgumentException exception)
        {
            validationMessage =
                exception.Message;

            return false;
        }
    }

    private static InventoryProductFilterOption
        MapProductOption(
            ProductListItemDto product)
    {
        return new InventoryProductFilterOption(
            product.Id,
            $"{product.Code} — {product.Name}");
    }

    private static DateTimeOffset
        ConvertStartOfLocalDayToUtc(
            DateTime date)
    {
        var localDate =
            DateTime.SpecifyKind(
                date.Date,
                DateTimeKind.Local);

        return new DateTimeOffset(
                localDate)
            .ToUniversalTime();
    }

    private static DateTimeOffset
        ConvertEndOfLocalDayToUtc(
            DateTime date)
    {
        var localEnd =
            DateTime.SpecifyKind(
                date.Date
                    .AddDays(1)
                    .AddMilliseconds(-1),
                DateTimeKind.Local);

        return new DateTimeOffset(
                localEnd)
            .ToUniversalTime();
    }

    private bool CanLoad()
    {
        return !IsLoading;
    }

    private bool CanGoToPreviousPage()
    {
        return !IsLoading &&
               PageNumber > 1;
    }

    private bool CanGoToNextPage()
    {
        return !IsLoading &&
               PageNumber < TotalPages;
    }

    private void HandleCommandException(
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Một lệnh lịch sử kho không thể hoàn thành.");

        ErrorMessage =
            "Thao tác không thể hoàn thành. " +
            exception.GetBaseException().Message;
    }

    private void NotifyCommandStates()
    {
        SearchCommand
            .NotifyCanExecuteChanged();

        RefreshCommand
            .NotifyCanExecuteChanged();

        SearchProductsCommand
            .NotifyCanExecuteChanged();

        ResetFiltersCommand
            .NotifyCanExecuteChanged();

        PreviousPageCommand
            .NotifyCanExecuteChanged();

        NextPageCommand
            .NotifyCanExecuteChanged();
    }
}