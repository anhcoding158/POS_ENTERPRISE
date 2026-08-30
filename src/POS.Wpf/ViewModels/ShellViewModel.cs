using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.DTOs.Products;
using POS.Domain.Enums;
using POS.Wpf.Commands;
using POS.Wpf.Services;


namespace POS.Wpf.ViewModels;

public sealed record ProductStockFilterOption(
    string DisplayName,
    bool? IsLowStock);

public sealed record ProductStatusFilterOption(
    string DisplayName,
    bool? IsActive,
    bool? IsArchived);

public enum ShellRoute
{
    Overview,
    Products,
    Categories
}

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

    private readonly IProductImportDialogService?
        _productImportDialogService;

    private readonly ICategoryManagementDialogService
        _categoryManagementDialogService;

    private readonly IInventoryDialogService
        _inventoryDialogService;

    private readonly IOrderHistoryWindowService
        _orderHistoryWindowService;

    private readonly IPermissionService
        _permissionService;

    private readonly ICurrentUserService?
        _currentUserService;

    private readonly ILogger<ShellViewModel>
        _logger;

    private string? _searchTerm;

    private ProductStockFilterOption?
        _selectedStockFilter;

    private ProductStatusFilterOption?
        _selectedProductStatusFilter;

    private bool
        _productFilterReloadPending;

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

    private bool _isInventoryExpanded = true;
    private bool _isOrdersExpanded;
    private bool _isQrExpanded;
    private bool _isManagementExpanded;
    private bool _isDataExpanded;
    private bool _isSidebarCompact;
    private ShellRoute _activeRoute =
        ShellRoute.Products;

    public ShellViewModel(
        IServiceScopeFactory scopeFactory,
        IProductDialogService productDialogService,
        ICategoryManagementDialogService
            categoryManagementDialogService,
        IInventoryDialogService inventoryDialogService,
        IOrderHistoryWindowService orderHistoryWindowService,
        IPermissionService permissionService,
        ILogger<ShellViewModel> logger,
        ICurrentUserService? currentUserService = null,
        IProductImportDialogService? productImportDialogService = null)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        _productDialogService =
            productDialogService ??
            throw new ArgumentNullException(
                nameof(productDialogService));

        _productImportDialogService =
            productImportDialogService;

        _categoryManagementDialogService =
            categoryManagementDialogService ??
            throw new ArgumentNullException(
                nameof(categoryManagementDialogService));

        _inventoryDialogService =
            inventoryDialogService ??
            throw new ArgumentNullException(
                nameof(inventoryDialogService));

        _orderHistoryWindowService =
            orderHistoryWindowService ??
            throw new ArgumentNullException(
                nameof(orderHistoryWindowService));

        _permissionService =
            permissionService ??
            throw new ArgumentNullException(
                nameof(permissionService));

        _currentUserService =
            currentUserService;

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        StockFilterOptions =
            Array.AsReadOnly<
                ProductStockFilterOption>(
            [
                new(
                    "Tất cả tồn kho",
                    null),

                new(
                    "Sắp hết hoặc hết hàng",
                    true),

                new(
                    "Còn đủ tồn kho",
                    false)
            ]);

        _selectedStockFilter =
            StockFilterOptions[0];

        ProductStatusFilters =
            Array.AsReadOnly<
                ProductStatusFilterOption>(
            [
                new(
                    "Tất cả trạng thái",
                    null,
                    false),

                new(
                    "Đang bán",
                    true,
                    false),

                new(
                    "Ngừng bán",
                    false,
                    false),

                new(
                    "Đã lưu trữ",
                    null,
                    true)
            ]);

        _selectedProductStatusFilter =
            ProductStatusFilters[0];

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

        ImportProductsCommand =
            new AsyncRelayCommand(
                ImportProductsAsync,
                CanImportProducts,
                HandleCommandException);

        OpenCategoryManagementCommand =
            new AsyncRelayCommand(
                OpenCategoryManagementAsync,
                CanLoadProducts,
                HandleCommandException);

        NavigateToOverviewCommand =
            new AsyncRelayCommand(
                NavigateToOverviewAsync);

        NavigateToProductsCommand =
            new AsyncRelayCommand(
                NavigateToProductsAsync,
                () => CanViewProducts,
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

        ClearSelectedProductCommand =
            new AsyncRelayCommand(
                ClearSelectedProductAsync,
                CanClearSelectedProduct,
                HandleCommandException);

        ViewInventoryHistoryCommand =
            new AsyncRelayCommand(
                ViewInventoryHistoryAsync,
                CanLoadProducts,
                HandleCommandException);

        OpenOrderHistoryCommand =
            new AsyncRelayCommand(
                OpenOrderHistoryAsync,
                CanOpenOrderHistory,
                HandleCommandException);

        ToggleProductActiveCommand =
            new AsyncRelayCommand(
                ToggleProductActiveAsync,
                CanEditSelectedProduct,
                HandleCommandException);

        ToggleProductArchiveCommand =
            new AsyncRelayCommand(
                ToggleProductArchiveAsync,
                CanArchiveSelectedProduct,
                HandleCommandException);

        ResetProductStatusFilterCommand =
            new AsyncRelayCommand(
                ResetProductStatusFilterAsync,
                CanLoadProducts,
                HandleCommandException);

        ResetStockFilterCommand =
            new AsyncRelayCommand(
                ResetStockFilterAsync,
                CanLoadProducts,
                HandleCommandException);

        ResetProductFiltersCommand =
            new AsyncRelayCommand(
                ResetProductFiltersAsync,
                CanLoadProducts,
                HandleCommandException);
    }

    public ObservableCollection<
        ProductRowViewModel>
        Products
    { get; } = [];

    public IReadOnlyList<
        ProductStockFilterOption>
        StockFilterOptions
    { get; }

    public IReadOnlyList<
        ProductStatusFilterOption>
        ProductStatusFilters
    { get; }

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    public AsyncRelayCommand NextPageCommand { get; }

    public AsyncRelayCommand AddProductCommand { get; }

    public AsyncRelayCommand ImportProductsCommand { get; }

    public AsyncRelayCommand
        OpenCategoryManagementCommand
    { get; }

    public AsyncRelayCommand NavigateToOverviewCommand
    { get; }

    public AsyncRelayCommand NavigateToProductsCommand
    { get; }

    public AsyncRelayCommand EditProductCommand { get; }

    public AsyncRelayCommand AdjustInventoryCommand
    {
        get;
    }

    public AsyncRelayCommand ClearSelectedProductCommand
    {
        get;
    }

    public AsyncRelayCommand ViewInventoryHistoryCommand
    {
        get;
    }

    public AsyncRelayCommand OpenOrderHistoryCommand
    {
        get;
    }

    public AsyncRelayCommand ToggleProductActiveCommand
    {
        get;
    }

    public AsyncRelayCommand ToggleProductArchiveCommand
    {
        get;
    }

    public AsyncRelayCommand
        ResetProductStatusFilterCommand
    { get; }

    public AsyncRelayCommand ResetStockFilterCommand
    { get; }

    public AsyncRelayCommand ResetProductFiltersCommand
    { get; }

    public string? SearchTerm
    {
        get => _searchTerm;

        set => SetProperty(
            ref _searchTerm,
            value);
    }

    public ProductStockFilterOption?
        SelectedStockFilter
    {
        get => _selectedStockFilter;

        set
        {
            if (!SetProperty(
                    ref _selectedStockFilter,
                    value))
            {
                return;
            }

            NotifyFilterPresentation();
            QueueProductFilterReload();
        }
    }

    public ProductStatusFilterOption?
        SelectedProductStatusFilter
    {
        get => _selectedProductStatusFilter;

        set
        {
            if (!SetProperty(
                    ref _selectedProductStatusFilter,
                    value))
            {
                return;
            }

            NotifyFilterPresentation();
            QueueProductFilterReload();
        }
    }

    public bool HasProductStatusFilter =>
        !ReferenceEquals(
            SelectedProductStatusFilter,
            ProductStatusFilters[0]);

    public bool HasStockFilter =>
        SelectedStockFilter?.IsLowStock is not null;

    public bool HasProductFilters =>
        HasProductStatusFilter ||
        HasStockFilter;

    public string ProductStatusFilterText =>
        SelectedProductStatusFilter?.DisplayName ??
        ProductStatusFilters[0].DisplayName;

    public string StockFilterText =>
        SelectedStockFilter?.DisplayName ??
        StockFilterOptions[0].DisplayName;

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

    public bool CanModifySelectedProduct =>
        SelectedProduct is
        {
            IsArchived: false
        };

    public bool SelectedProductTracksInventory =>
        SelectedProduct?.TrackInventory == true;

    public string ToggleProductButtonText =>
        SelectedProduct?.IsActive == true
            ? "Ngừng bán"
            : "Bán lại";

    public string ToggleProductArchiveButtonText =>
        SelectedProduct?.IsArchived == true
            ? "Khôi phục"
            : "Lưu trữ";

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
            // Giữ property instance-bound để WPF tự cập nhật cùng selection.
            _ = SelectedProduct;
            return "Mở lịch sử tồn kho của toàn bộ sản phẩm.";
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

    public bool CanViewProducts =>
        _permissionService.HasPermission(
            SystemCapability.ViewProductCatalog);

    public bool CanManageCategories =>
        _permissionService.HasPermission(
            SystemCapability.ManageCategories);

    public bool CanUseCheckout =>
        _permissionService.HasPermission(
            SystemCapability.UseCheckout);

    public bool CanViewOrderHistory =>
        _permissionService.HasPermission(
            SystemCapability.ViewReports);

    public bool CanManageStoreSetup =>
        _permissionService.HasPermission(
            SystemCapability.ManageStoreSetup);

    public bool CanRestoreData => CanManageStoreSetup;

    public bool CanViewEmployees =>
        _permissionService.HasPermission(
            SystemCapability.ViewEmployees);

    public bool CanViewVietQr =>
        _currentUserService?.Role is
            Role.Administrator or Role.Manager;

    public ShellRoute ActiveRoute
    {
        get => _activeRoute;
        private set
        {
            if (!SetProperty(
                    ref _activeRoute,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(IsOverviewRouteActive));
            OnPropertyChanged(
                nameof(IsProductsRouteActive));
            OnPropertyChanged(
                nameof(IsCategoriesRouteActive));
        }
    }

    public bool IsOverviewRouteActive =>
        ActiveRoute == ShellRoute.Overview;

    public bool IsProductsRouteActive =>
        ActiveRoute == ShellRoute.Products;

    public bool IsCategoriesRouteActive =>
        ActiveRoute == ShellRoute.Categories;

    public bool IsSidebarCompact =>
        _isSidebarCompact;

    public bool IsSidebarExpanded =>
        !_isSidebarCompact;

    public double SidebarWidth =>
        _isSidebarCompact
            ? 76d
            : 276d;

    public void UpdateViewportWidth(
        double viewportWidth)
    {
        var compact =
            viewportWidth < 1180d;

        if (!SetProperty(
                ref _isSidebarCompact,
                compact,
                nameof(IsSidebarCompact)))
        {
            return;
        }

        OnPropertyChanged(
            nameof(IsSidebarExpanded));
        OnPropertyChanged(
            nameof(SidebarWidth));
    }

    public bool IsInventoryExpanded
    {
        get => _isInventoryExpanded;
        set => SetExpandedGroup(
            ref _isInventoryExpanded,
            value,
            nameof(IsInventoryExpanded));
    }

    public bool IsOrdersExpanded
    {
        get => _isOrdersExpanded;
        set => SetExpandedGroup(
            ref _isOrdersExpanded,
            value,
            nameof(IsOrdersExpanded));
    }

    public bool IsQrExpanded
    {
        get => _isQrExpanded;
        set => SetExpandedGroup(
            ref _isQrExpanded,
            value,
            nameof(IsQrExpanded));
    }

    public bool IsManagementExpanded
    {
        get => _isManagementExpanded;
        set => SetExpandedGroup(
            ref _isManagementExpanded,
            value,
            nameof(IsManagementExpanded));
    }

    public bool IsDataExpanded
    {
        get => _isDataExpanded;
        set => SetExpandedGroup(
            ref _isDataExpanded,
            value,
            nameof(IsDataExpanded));
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

    /// <summary>
    /// Đồng bộ lại catalog sau khi một module khác
    /// có thể đã thay đổi giá hoặc tồn kho.
    ///
    /// Ví dụ:
    /// - Checkout;
    /// - nhập kho;
    /// - đồng bộ từ máy khác.
    /// </summary>
    public Task<bool>
        RefreshAfterExternalChangeAsync()
    {
        return LoadProductsAsync(
            SelectedProduct?.Id);
    }

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

    private Task<bool> RefreshAsync()
    {
        return LoadProductsAsync();
    }

    private Task ResetProductStatusFilterAsync()
    {
        SelectedProductStatusFilter =
            ProductStatusFilters[0];

        return Task.CompletedTask;
    }

    private Task ResetStockFilterAsync()
    {
        SelectedStockFilter =
            StockFilterOptions[0];

        return Task.CompletedTask;
    }

    private Task ResetProductFiltersAsync()
    {
        var statusChanged =
            !ReferenceEquals(
                SelectedProductStatusFilter,
                ProductStatusFilters[0]);

        var stockChanged =
            !ReferenceEquals(
                SelectedStockFilter,
                StockFilterOptions[0]);

        if (!statusChanged &&
            !stockChanged)
        {
            return Task.CompletedTask;
        }

        _selectedProductStatusFilter =
            ProductStatusFilters[0];

        _selectedStockFilter =
            StockFilterOptions[0];

        OnPropertyChanged(
            nameof(SelectedProductStatusFilter));

        OnPropertyChanged(
            nameof(SelectedStockFilter));

        NotifyFilterPresentation();
        QueueProductFilterReload();

        return Task.CompletedTask;
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

    private async Task ImportProductsAsync()
    {
        if (_productImportDialogService is null)
        {
            StatusMessage = "Chức năng nhập sản phẩm chưa được đăng ký.";
            return;
        }

        var imported =
            await _productImportDialogService
                .ShowAsync();

        if (imported)
        {
            await LoadProductsAsync(
                SelectedProduct?.Id);
        }
    }

    private bool CanImportProducts()
    {
        return !IsLoading &&
               _permissionService.HasPermission(
                   SystemCapability.ManageProducts);
    }

    private Task NavigateToOverviewAsync()
    {
        ActiveRoute =
            ShellRoute.Overview;

        return Task.CompletedTask;
    }

    private Task NavigateToProductsAsync()
    {
        IsInventoryExpanded =
            true;

        ActiveRoute =
            ShellRoute.Products;

        /*
         * Danh sách sản phẩm đã được Shell sở hữu và tải một lần.
         * Chuyển về route đang hoạt động chỉ đổi trạng thái điều hướng;
         * không tạo ViewModel mới và không chạy thêm truy vấn.
         */
        return Task.CompletedTask;
    }

    private async Task OpenCategoryManagementAsync()
    {
        var selectedProductId =
            SelectedProduct?.Id;

        IsInventoryExpanded =
            true;

        ActiveRoute =
            ShellRoute.Categories;

        StatusMessage =
            "Đang mở màn hình quản lý danh mục...";

        await _categoryManagementDialogService
            .ShowAsync();

        /*
         * Danh mục có thể đã được thêm, đổi tên,
         * đổi thứ tự hoặc thay đổi trạng thái.
         *
         * Tải lại Product grid để tên danh mục hiển thị
         * luôn đồng bộ với dữ liệu mới nhất.
         */
        var reloaded =
            await LoadProductsAsync(
                selectedProductId);

        if (reloaded)
        {
            StatusMessage =
                "Danh mục đã được đồng bộ với danh sách sản phẩm.";
        }
    }

    private void SetExpandedGroup(
        ref bool field,
        bool value,
        string propertyName)
    {
        if (!SetProperty(
                ref field,
                value,
                propertyName) ||
            !value)
        {
            return;
        }

        CollapseGroup(
            ref _isInventoryExpanded,
            nameof(IsInventoryExpanded),
            propertyName);
        CollapseGroup(
            ref _isOrdersExpanded,
            nameof(IsOrdersExpanded),
            propertyName);
        CollapseGroup(
            ref _isQrExpanded,
            nameof(IsQrExpanded),
            propertyName);
        CollapseGroup(
            ref _isManagementExpanded,
            nameof(IsManagementExpanded),
            propertyName);
        CollapseGroup(
            ref _isDataExpanded,
            nameof(IsDataExpanded),
            propertyName);
    }

    private void CollapseGroup(
        ref bool field,
        string propertyName,
        string expandedPropertyName)
    {
        if (string.Equals(
                propertyName,
                expandedPropertyName,
                StringComparison.Ordinal))
        {
            return;
        }

        SetProperty(
            ref field,
            false,
            propertyName);
    }

    private async Task EditProductAsync()
    {
        var selectedProduct =
            SelectedProduct;

        if (selectedProduct is null ||
            selectedProduct.IsArchived)
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
            selectedProduct.IsArchived ||
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
        StatusMessage =
            "Đang mở lịch sử tồn kho toàn bộ sản phẩm...";

        await _inventoryDialogService
            .ShowHistoryAsync();

        StatusMessage =
            "Đã đóng màn hình lịch sử tồn kho.";
    }

    private Task ClearSelectedProductAsync()
    {
        if (!CanClearSelectedProduct())
        {
            return Task.CompletedTask;
        }

        SelectedProduct = null;
        StatusMessage =
            "Đã bỏ chọn sản phẩm. Chọn một sản phẩm để thao tác.";

        return Task.CompletedTask;
    }

    private async Task ToggleProductActiveAsync()
    {
        var selectedProduct =
            SelectedProduct;

        if (selectedProduct is null ||
            selectedProduct.IsArchived)
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
                    result.AppError.Message;

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

    private async Task ToggleProductArchiveAsync()
    {
        var selectedProduct =
            SelectedProduct;

        if (selectedProduct is null)
        {
            return;
        }

        var productId =
            selectedProduct.Id;

        var isRestore =
            selectedProduct.IsArchived;

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
                isRestore
                    ? await productService
                        .RestoreAsync(
                            productId,
                            CancellationToken.None)
                    : await productService
                        .ArchiveAsync(
                            productId,
                            CancellationToken.None);

            if (result.IsFailure)
            {
                StatusMessage =
                    result.AppError.Message;

                return;
            }
        }
        finally
        {
            IsLoading = false;
        }

        var reloaded =
            await LoadProductsAsync();

        if (reloaded)
        {
            StatusMessage =
                isRestore
                    ? "Đã khôi phục sản phẩm. " +
                      "Sản phẩm vẫn đang ở trạng thái ngừng bán."
                    : "Đã lưu trữ sản phẩm.";
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
                        SelectedProductStatusFilter?
                            .IsActive,

                    isLowStock:
                        SelectedStockFilter?
                            .IsLowStock,

                    isArchived:
                        SelectedProductStatusFilter?
                            .IsArchived,

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
                    result.AppError.Message;

                global::POS.Application.Common.PosLog.Warning(_logger,
                    "Tải sản phẩm thất bại: " +
                    "{ErrorCode} - {ErrorMessage}",
                    result.AppError.Code,
                    result.AppError.Message);

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
            global::POS.Application.Common.PosLog.Error(_logger,
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

            if (_productFilterReloadPending)
            {
                _productFilterReloadPending =
                    false;

                PageNumber = 1;

                _ = LoadProductsAsync();
            }
        }
    }

    private bool CanLoadProducts()
    {
        return !IsLoading;
    }

    private void QueueProductFilterReload()
    {
        PageNumber = 1;

        if (IsLoading)
        {
            _productFilterReloadPending =
                true;

            return;
        }

        _ = LoadProductsAsync();
    }

    private void NotifyFilterPresentation()
    {
        OnPropertyChanged(
            nameof(HasProductStatusFilter));

        OnPropertyChanged(
            nameof(HasStockFilter));

        OnPropertyChanged(
            nameof(HasProductFilters));

        OnPropertyChanged(
            nameof(ProductStatusFilterText));

        OnPropertyChanged(
            nameof(StockFilterText));
    }

    private bool CanEditSelectedProduct()
    {
        return !IsLoading &&
               SelectedProduct is
               {
                   IsArchived: false
               };
    }

    private bool CanAdjustSelectedProduct()
    {
        return !IsLoading &&
               SelectedProduct is
               {
                   TrackInventory: true,
                   IsArchived: false
               };
    }

    private bool CanClearSelectedProduct()
    {
        return !IsLoading &&
               SelectedProduct is not null;
    }

    private bool CanArchiveSelectedProduct()
    {
        return !IsLoading &&
               SelectedProduct is not null;
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

    private Task OpenOrderHistoryAsync()
    {
        return _orderHistoryWindowService.ShowAsync();
    }

    private bool CanOpenOrderHistory()
    {
        return !IsLoading &&
               _permissionService.HasPermission(
                   SystemCapability.ViewReports);
    }

    private void HandleCommandException(
        Exception exception)
    {
        global::POS.Application.Common.PosLog.Error(_logger,
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
                CanModifySelectedProduct));

        OnPropertyChanged(
            nameof(
                SelectedProductTracksInventory));

        OnPropertyChanged(
            nameof(
                ToggleProductButtonText));

        OnPropertyChanged(
            nameof(
                ToggleProductArchiveButtonText));

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

        ImportProductsCommand
            .NotifyCanExecuteChanged();

        OpenCategoryManagementCommand
            .NotifyCanExecuteChanged();

        NavigateToProductsCommand
            .NotifyCanExecuteChanged();

        EditProductCommand
            .NotifyCanExecuteChanged();

        AdjustInventoryCommand
            .NotifyCanExecuteChanged();

        ClearSelectedProductCommand
            .NotifyCanExecuteChanged();

        ViewInventoryHistoryCommand
            .NotifyCanExecuteChanged();

        OpenOrderHistoryCommand
            .NotifyCanExecuteChanged();

        ToggleProductActiveCommand
            .NotifyCanExecuteChanged();

        ToggleProductArchiveCommand
            .NotifyCanExecuteChanged();

        ResetProductStatusFilterCommand
            .NotifyCanExecuteChanged();

        ResetStockFilterCommand
            .NotifyCanExecuteChanged();

        ResetProductFiltersCommand
            .NotifyCanExecuteChanged();
    }
}
