using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Products;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

/// <summary>
/// Điều khiển màn hình sản phẩm và tồn kho.
///
/// ViewModel không giữ ProductService, InventoryService
/// hoặc DbContext lâu dài.
///
/// Mỗi thao tác dữ liệu tạo một DI scope ngắn riêng.
/// </summary>
public sealed class ShellViewModel :
    ViewModelBase
{
    private const int DefaultPageSize = 20;

    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly IProductDialogService
        _productDialogService;

    private readonly IInventoryDialogService
        _inventoryDialogService;

    private readonly ILogger<ShellViewModel>
        _logger;

    private string? _searchTerm;

    private bool _isLoading;
    private bool _isInitialized;

    private ProductRowViewModel?
        _selectedProduct;

    private int _pageNumber = 1;
    private int _totalPages = 1;

    private int _totalProducts;
    private int _activeProductsOnPage;
    private int _lowStockProductsOnPage;

    private decimal _inventoryValueOnPage;

    private string _statusMessage =
        "Đang chuẩn bị dữ liệu sản phẩm...";

    private string _lastUpdatedText =
        "Chưa tải dữ liệu";

    public ShellViewModel(
        IServiceScopeFactory scopeFactory,
        IProductDialogService productDialogService,
        IInventoryDialogService inventoryDialogService,
        ILogger<ShellViewModel> logger)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        _productDialogService =
            productDialogService ??
            throw new ArgumentNullException(
                nameof(productDialogService));

        _inventoryDialogService =
            inventoryDialogService ??
            throw new ArgumentNullException(
                nameof(inventoryDialogService));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        SearchCommand =
            new AsyncRelayCommand(
                SearchAsync,
                CanLoadProducts,
                HandleCommandException);

        RefreshCommand =
            new AsyncRelayCommand(
                RefreshAsync,
                CanLoadProducts,
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

        AddProductCommand =
            new AsyncRelayCommand(
                AddProductAsync,
                CanLoadProducts,
                HandleCommandException);

        EditProductCommand =
            new AsyncRelayCommand(
                EditProductAsync,
                CanEditSelectedProduct,
                HandleCommandException);

        AdjustInventoryCommand =
            new AsyncRelayCommand(
                AdjustInventoryAsync,
                CanAdjustSelectedProduct,
                HandleCommandException);

        ViewInventoryHistoryCommand =
            new AsyncRelayCommand(
                ViewInventoryHistoryAsync,
                CanLoadProducts,
                HandleCommandException);

        ToggleProductActiveCommand =
            new AsyncRelayCommand(
                ToggleProductActiveAsync,
                CanEditSelectedProduct,
                HandleCommandException);
    }

    public ObservableCollection<
        ProductRowViewModel>
        Products
    { get; } = [];

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    public AsyncRelayCommand NextPageCommand { get; }

    public AsyncRelayCommand AddProductCommand { get; }

    public AsyncRelayCommand EditProductCommand { get; }

    public AsyncRelayCommand AdjustInventoryCommand
    {
        get;
    }

    public AsyncRelayCommand ViewInventoryHistoryCommand
    {
        get;
    }

    public AsyncRelayCommand ToggleProductActiveCommand
    {
        get;
    }

    public string? SearchTerm
    {
        get => _searchTerm;

        set => SetProperty(
            ref _searchTerm,
            value);
    }

    public ProductRowViewModel?
        SelectedProduct
    {
        get => _selectedProduct;

        set
        {
            if (!SetProperty(
                    ref _selectedProduct,
                    value))
            {
                return;
            }

            NotifySelectedProductPresentation();
            NotifyCommandStates();
        }
    }

    public bool HasSelectedProduct =>
        SelectedProduct is not null;

    public bool SelectedProductTracksInventory =>
        SelectedProduct?.TrackInventory == true;

    public string ToggleProductButtonText =>
        SelectedProduct?.IsActive == true
            ? "Ngừng bán"
            : "Bật bán";

    public string SelectedProductHint
    {
        get
        {
            var selectedProduct =
                SelectedProduct;

            if (selectedProduct is null)
            {
                return
                    "Chọn một sản phẩm để chỉnh sửa, quản lý kho " +
                    "hoặc thay đổi trạng thái bán.";
            }

            if (!selectedProduct.TrackInventory)
            {
                return
                    $"Đã chọn: {selectedProduct.Name} • " +
                    "Sản phẩm không theo dõi tồn kho.";
            }

            return
                $"Đã chọn: {selectedProduct.Name} • " +
                $"Tồn hiện tại {selectedProduct.StockDisplay}.";
        }
    }

    public string InventoryActionHint
    {
        get
        {
            var selectedProduct =
                SelectedProduct;

            if (selectedProduct is null)
            {
                return
                    "Chọn sản phẩm trước khi điều chỉnh tồn kho.";
            }

            if (!selectedProduct.TrackInventory)
            {
                return
                    "Sản phẩm này đang tắt theo dõi kho. " +
                    "Hãy bật theo dõi kho trong màn hình sửa sản phẩm.";
            }

            return
                $"Mở nghiệp vụ nhập, xuất, điều chỉnh hoặc kiểm kê " +
                $"cho {selectedProduct.Name}.";
        }
    }

    public string InventoryHistoryActionHint
    {
        get
        {
            var selectedProduct =
                SelectedProduct;

            return selectedProduct is null
                ? "Mở lịch sử tồn kho của toàn bộ sản phẩm."
                : $"Mở lịch sử tồn kho của {selectedProduct.Name}.";
        }
    }

    public string SelectedProductCodeText =>
        SelectedProduct?.Code ??
        "—";

    public string SelectedProductStockText =>
        SelectedProduct?.StockDisplay ??
        "—";

    public string SelectedProductStockStateText =>
        SelectedProduct?.StockStateText ??
        "Chưa chọn sản phẩm";

    public string SelectedProductPolicyText
    {
        get
        {
            var selectedProduct =
                SelectedProduct;

            if (selectedProduct is null)
            {
                return "Chưa có dữ liệu";
            }

            if (!selectedProduct.TrackInventory)
            {
                return "Không theo dõi kho";
            }

            return selectedProduct.AllowNegativeStock
                ? "Cho phép tồn âm"
                : "Chặn tồn âm";
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

    public int TotalProducts
    {
        get => _totalProducts;

        private set
        {
            if (!SetProperty(
                    ref _totalProducts,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(TotalProductsText));
        }
    }

    public int ActiveProductsOnPage
    {
        get => _activeProductsOnPage;

        private set
        {
            if (!SetProperty(
                    ref _activeProductsOnPage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(
                    ActiveProductsOnPageText));
        }
    }

    public int LowStockProductsOnPage
    {
        get => _lowStockProductsOnPage;

        private set
        {
            if (!SetProperty(
                    ref _lowStockProductsOnPage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(
                    LowStockProductsOnPageText));
        }
    }

    public decimal InventoryValueOnPage
    {
        get => _inventoryValueOnPage;

        private set
        {
            if (!SetProperty(
                    ref _inventoryValueOnPage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(
                    InventoryValueOnPageText));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;

        private set => SetProperty(
            ref _statusMessage,
            value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;

        private set => SetProperty(
            ref _lastUpdatedText,
            value);
    }

    public string PageText =>
        $"Trang {PageNumber:N0} / {TotalPages:N0}";

    public string TotalProductsText =>
        TotalProducts.ToString(
            "N0",
            VietnameseCulture);

    public string ActiveProductsOnPageText =>
        ActiveProductsOnPage.ToString(
            "N0",
            VietnameseCulture);

    public string LowStockProductsOnPageText =>
        LowStockProductsOnPage.ToString(
            "N0",
            VietnameseCulture);

    public string InventoryValueOnPageText =>
        $"{InventoryValueOnPage.ToString(
            "N0",
            VietnameseCulture)} ₫";

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        await LoadProductsAsync();
    }

    private async Task SearchAsync()
    {
        PageNumber = 1;

        await LoadProductsAsync();
    }

    private Task RefreshAsync()
    {
        return LoadProductsAsync();
    }

    private async Task AddProductAsync()
    {
        var saved =
            await _productDialogService
                .ShowCreateAsync();

        if (!saved)
        {
            return;
        }

        PageNumber = 1;

        var reloaded =
            await LoadProductsAsync();

        if (reloaded)
        {
            StatusMessage =
                "Sản phẩm mới đã được tạo thành công.";
        }
    }

    private async Task EditProductAsync()
    {
        var selectedProduct =
            SelectedProduct;

        if (selectedProduct is null)
        {
            return;
        }

        var productId =
            selectedProduct.Id;

        var saved =
            await _productDialogService
                .ShowEditAsync(
                    productId);

        if (!saved)
        {
            return;
        }

        var reloaded =
            await LoadProductsAsync(
                productId);

        if (reloaded)
        {
            StatusMessage =
                "Thông tin sản phẩm đã được cập nhật.";
        }
    }

    private async Task AdjustInventoryAsync()
    {
        var selectedProduct =
            SelectedProduct;

        if (selectedProduct is null ||
            !selectedProduct.TrackInventory)
        {
            return;
        }

        var productId =
            selectedProduct.Id;

        var productName =
            selectedProduct.Name;

        var saved =
            await _inventoryDialogService
                .ShowAdjustmentAsync(
                    productId);

        if (!saved)
        {
            return;
        }

        /*
         * Dialog chỉ trả true sau khi transaction
         * Product + InventoryMovement được commit.
         */
        var reloaded =
            await LoadProductsAsync(
                productId);

        if (reloaded)
        {
            StatusMessage =
                $"Biến động kho của '{productName}' " +
                "đã được lưu thành công.";
        }
    }

    private async Task ViewInventoryHistoryAsync()
    {
        var selectedProduct =
            SelectedProduct;

        var productId =
            selectedProduct?.Id;

        StatusMessage =
            selectedProduct is null
                ? "Đang mở lịch sử tồn kho toàn hệ thống..."
                : $"Đang mở lịch sử kho của " +
                  $"'{selectedProduct.Name}'...";

        await _inventoryDialogService
            .ShowHistoryAsync(
                productId);

        StatusMessage =
            selectedProduct is null
                ? "Đã đóng màn hình lịch sử tồn kho."
                : $"Đã đóng lịch sử kho của " +
                  $"'{selectedProduct.Name}'.";
    }

    private async Task ToggleProductActiveAsync()
    {
        var selectedProduct =
            SelectedProduct;

        if (selectedProduct is null)
        {
            return;
        }

        var productId =
            selectedProduct.Id;

        var targetState =
            !selectedProduct.IsActive;

        IsLoading = true;

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var productService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IProductService>();

            var result =
                await productService
                    .SetActiveStateAsync(
                        productId,
                        targetState);

            if (result.IsFailure)
            {
                StatusMessage =
                    result.Error.Message;

                return;
            }
        }
        finally
        {
            IsLoading = false;
        }

        var reloaded =
            await LoadProductsAsync(
                productId);

        if (reloaded)
        {
            StatusMessage =
                targetState
                    ? "Sản phẩm đã được bật bán."
                    : "Sản phẩm đã được ngừng bán.";
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

        var succeeded =
            await LoadProductsAsync();

        if (!succeeded)
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

        var succeeded =
            await LoadProductsAsync();

        if (!succeeded)
        {
            PageNumber =
                previousPage;
        }
    }

    private async Task<bool> LoadProductsAsync(
        int? productIdToSelect = null)
    {
        if (IsLoading)
        {
            return false;
        }

        var selectedProductId =
            productIdToSelect ??
            SelectedProduct?.Id;

        IsLoading = true;

        StatusMessage =
            "Đang tải dữ liệu sản phẩm...";

        try
        {
            var request =
                new ProductSearchRequest(
                    searchTerm:
                        SearchTerm,

                    categoryId:
                        null,

                    isActive:
                        null,

                    isLowStock:
                        null,

                    pageNumber:
                        PageNumber,

                    pageSize:
                        DefaultPageSize);

            await using var operationScope =
                _scopeFactory.CreateAsyncScope();

            var productService =
                operationScope.ServiceProvider
                    .GetRequiredService<
                        IProductService>();

            var result =
                await productService.SearchAsync(
                    request);

            if (result.IsFailure)
            {
                StatusMessage =
                    result.Error.Message;

                _logger.LogWarning(
                    "Tải sản phẩm thất bại: " +
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
                        product =>
                            new ProductRowViewModel(
                                product))
                    .ToArray();

            Products.Clear();

            foreach (var row in rows)
            {
                Products.Add(row);
            }

            PageNumber =
                page.PageNumber;

            TotalPages =
                Math.Max(
                    1,
                    page.TotalPages);

            TotalProducts =
                page.TotalCount;

            ActiveProductsOnPage =
                rows.Count(
                    product =>
                        product.IsActive);

            LowStockProductsOnPage =
                rows.Count(
                    product =>
                        product.TrackInventory &&
                        product.IsLowStock);

            /*
             * Chuyển CostPrice sang decimal trước khi nhân
             * để tránh overflow long trong KPI giao diện.
             */
            InventoryValueOnPage =
                rows
                    .Where(
                        product =>
                            product.TrackInventory &&
                            product.StockQuantity > 0)
                    .Sum(
                        product =>
                            (decimal)
                            product.CostPrice *
                            product.StockQuantity);

            SelectedProduct =
                selectedProductId.HasValue
                    ? Products.FirstOrDefault(
                        product =>
                            product.Id ==
                            selectedProductId.Value)
                    : null;

            StatusMessage =
                rows.Length == 0
                    ? "Không tìm thấy sản phẩm phù hợp."
                    : $"Đã tải {rows.Length:N0} sản phẩm.";

            LastUpdatedText =
                $"Cập nhật lúc " +
                $"{DateTimeOffset.Now:HH:mm:ss}";

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Có lỗi khi tải danh sách sản phẩm.");

            StatusMessage =
                "Không thể tải sản phẩm. " +
                exception
                    .GetBaseException()
                    .Message;

            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanLoadProducts()
    {
        return !IsLoading;
    }

    private bool CanEditSelectedProduct()
    {
        return !IsLoading &&
               SelectedProduct is not null;
    }

    private bool CanAdjustSelectedProduct()
    {
        return !IsLoading &&
               SelectedProduct?.TrackInventory == true;
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
            "Một lệnh giao diện không thể hoàn thành.");

        StatusMessage =
            "Thao tác không thể hoàn thành. " +
            exception
                .GetBaseException()
                .Message;
    }

    private void NotifySelectedProductPresentation()
    {
        OnPropertyChanged(
            nameof(HasSelectedProduct));

        OnPropertyChanged(
            nameof(
                SelectedProductTracksInventory));

        OnPropertyChanged(
            nameof(
                ToggleProductButtonText));

        OnPropertyChanged(
            nameof(
                SelectedProductHint));

        OnPropertyChanged(
            nameof(
                InventoryActionHint));

        OnPropertyChanged(
            nameof(
                InventoryHistoryActionHint));

        OnPropertyChanged(
            nameof(
                SelectedProductCodeText));

        OnPropertyChanged(
            nameof(
                SelectedProductStockText));

        OnPropertyChanged(
            nameof(
                SelectedProductStockStateText));

        OnPropertyChanged(
            nameof(
                SelectedProductPolicyText));
    }

    private void NotifyCommandStates()
    {
        SearchCommand
            .NotifyCanExecuteChanged();

        RefreshCommand
            .NotifyCanExecuteChanged();

        PreviousPageCommand
            .NotifyCanExecuteChanged();

        NextPageCommand
            .NotifyCanExecuteChanged();

        AddProductCommand
            .NotifyCanExecuteChanged();

        EditProductCommand
            .NotifyCanExecuteChanged();

        AdjustInventoryCommand
            .NotifyCanExecuteChanged();

        ViewInventoryHistoryCommand
            .NotifyCanExecuteChanged();

        ToggleProductActiveCommand
            .NotifyCanExecuteChanged();
    }
}