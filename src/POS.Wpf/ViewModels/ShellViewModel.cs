using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Products;
using POS.Wpf.Commands;
namespace POS.Wpf.ViewModels;

/// <summary>
/// Điều khiển màn hình danh mục sản phẩm.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private const int DefaultPageSize = 20;

    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo("vi-VN");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShellViewModel> _logger;

    private string? _searchTerm;
    private bool _isLoading;
    private bool _isInitialized;

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
    ILogger<ShellViewModel> logger)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

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
    }

    public ObservableCollection<ProductRowViewModel>
        Products
    { get; } = [];

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    public AsyncRelayCommand NextPageCommand { get; }

    public string? SearchTerm
    {
        get => _searchTerm;

        set => SetProperty(
            ref _searchTerm,
            value);
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

            OnPropertyChanged(nameof(PageText));
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

            OnPropertyChanged(nameof(PageText));
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

            OnPropertyChanged(nameof(TotalProductsText));
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
                nameof(ActiveProductsOnPageText));
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
                nameof(LowStockProductsOnPageText));
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
                nameof(InventoryValueOnPageText));
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

    private async Task PreviousPageAsync()
    {
        if (!CanGoToPreviousPage())
        {
            return;
        }

        var previousPage = PageNumber;

        PageNumber--;

        var succeeded =
            await LoadProductsAsync();

        if (!succeeded)
        {
            PageNumber = previousPage;
        }
    }

    private async Task NextPageAsync()
    {
        if (!CanGoToNextPage())
        {
            return;
        }

        var previousPage = PageNumber;

        PageNumber++;

        var succeeded =
            await LoadProductsAsync();

        if (!succeeded)
        {
            PageNumber = previousPage;
        }
    }

    private async Task<bool> LoadProductsAsync()
    {
        if (IsLoading)
        {
            return false;
        }

        IsLoading = true;

        StatusMessage =
            "Đang tải dữ liệu sản phẩm...";

        try
        {
            var request =
                new ProductSearchRequest(
                    searchTerm: SearchTerm,
                    categoryId: null,
                    isActive: null,
                    isLowStock: null,
                    pageNumber: PageNumber,
                    pageSize: DefaultPageSize);

            /*
 * Mỗi thao tác tạo một DI scope riêng.
 *
 * ProductService, repository và PosDbContext sẽ được
 * giải phóng ngay khi thao tác kết thúc.
 */
            await using var operationScope =
                _scopeFactory.CreateAsyncScope();

            var productService =
                operationScope.ServiceProvider
                    .GetRequiredService<IProductService>();

            var result =
                await productService.SearchAsync(
                    request);

            if (result.IsFailure)
            {
                StatusMessage =
                    result.Error.Message;

                _logger.LogWarning(
                    "Tải sản phẩm thất bại: {ErrorCode} - {ErrorMessage}",
                    result.Error.Code,
                    result.Error.Message);

                return false;
            }

            var page = result.Value;

            var rows = page.Items
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

            PageNumber = page.PageNumber;

            TotalPages =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        page.TotalCount /
                        (double)page.PageSize));

            TotalProducts = page.TotalCount;

            ActiveProductsOnPage =
                rows.Count(
                    product =>
                        product.IsActive);

            LowStockProductsOnPage =
                rows.Count(
                    product =>
                        product.TrackInventory &&
                        product.IsLowStock);

            InventoryValueOnPage =
                rows
                    .Where(
                        product =>
                            product.TrackInventory &&
                            product.StockQuantity > 0)
                    .Sum(
                        product =>
                            (decimal)product.CostPrice *
                            product.StockQuantity);

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
                exception.GetBaseException().Message;

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
            exception.GetBaseException().Message;
    }

    private void NotifyCommandStates()
    {
        SearchCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }
}