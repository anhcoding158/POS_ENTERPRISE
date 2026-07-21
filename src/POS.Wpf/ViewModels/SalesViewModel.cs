using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Products;
using POS.Domain.Constants;
using POS.Domain.Enums;
using POS.Wpf.Commands;

namespace POS.Wpf.ViewModels;

/// <summary>
/// ViewModel chính của quầy bán hàng.
///
/// Nguyên tắc an toàn:
/// - không giữ DbContext lâu dài;
/// - mỗi thao tác dữ liệu tạo DI scope riêng;
/// - UI không gửi giá bán tới CheckoutService;
/// - CheckoutService xác nhận lại sản phẩm, giá và tồn kho;
/// - một ProductId chỉ xuất hiện một lần trong giỏ.
/// </summary>
public sealed class SalesViewModel :
    ViewModelBase,
    IDisposable
{
    private const int
        CatalogPageSize = 200;

    private static readonly TimeSpan
        LastOrderBannerLifetime =
            TimeSpan.FromSeconds(7);

    private static readonly CultureInfo
        VietnameseCulture =
            CultureInfo.GetCultureInfo(
                "vi-VN");

    private readonly IServiceScopeFactory
        _scopeFactory;

    private readonly ICurrentUserService
        _currentUserService;

    private readonly ILogger<SalesViewModel>
        _logger;

    private string? _searchTerm;
    private string _cashReceivedText =
        string.Empty;

    private string _orderNotes =
        string.Empty;

    private string _statusMessage =
        string.Empty;

    private bool _isStatusError;
    private bool _isStatusSuccess;

    private bool _isLoadingProducts;
    private bool _isCheckingOut;
    private bool _isInitialized;

    private int? _selectedCategoryId;

    private string? _lastOrderCode;
    private string? _lastOrderSummary;

    private CancellationTokenSource?
        _lastOrderDismissalSource;

    private long _orderSessionVersion;
    private bool _isDisposed;

    /*
     * Bốn mệnh giá gợi ý được tính lại theo:
     * - tổng tiền hiện tại khi ô nhập đang trống;
     * - số người dùng đang nhập khi ô có dữ liệu.
     */
    private readonly long[]
        _quickCashAmounts =
    [
        100_000,
        200_000,
        500_000,
        1_000_000
    ];

    public SalesViewModel(
        IServiceScopeFactory scopeFactory,
        ICurrentUserService currentUserService,
        ILogger<SalesViewModel> logger)
    {
        _scopeFactory =
            scopeFactory ??
            throw new ArgumentNullException(
                nameof(scopeFactory));

        _currentUserService =
            currentUserService ??
            throw new ArgumentNullException(
                nameof(currentUserService));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        if (!_currentUserService
            .IsAuthenticated)
        {
            throw new InvalidOperationException(
                "Không thể mở quầy bán hàng khi chưa đăng nhập.");
        }

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

        ClearCartCommand =
            new AsyncRelayCommand(
                ClearCartAsync,
                CanClearCart,
                HandleCommandException);

        ExactCashCommand =
            new AsyncRelayCommand(
                SetExactCashAsync,
                CanSetCash,
                HandleCommandException);

        QuickCash1Command =
            CreateQuickCashCommand(
                suggestionIndex: 0);

        QuickCash2Command =
            CreateQuickCashCommand(
                suggestionIndex: 1);

        QuickCash3Command =
            CreateQuickCashCommand(
                suggestionIndex: 2);

        QuickCash4Command =
            CreateQuickCashCommand(
                suggestionIndex: 3);

        CheckoutCommand =
            new AsyncRelayCommand(
                CheckoutAsync,
                CanCheckout,
                HandleCommandException);
    }

    public ObservableCollection<
        SalesCategoryFilterViewModel>
        CategoryFilters
    {
        get;
    } = [];

    public ObservableCollection<
        SalesProductCardViewModel>
        ProductCards
    {
        get;
    } = [];

    public ObservableCollection<
        SalesCartLineViewModel>
        CartLines
    {
        get;
    } = [];

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand ClearCartCommand { get; }

    public AsyncRelayCommand ExactCashCommand { get; }

    public AsyncRelayCommand QuickCash1Command { get; }

    public AsyncRelayCommand QuickCash2Command { get; }

    public AsyncRelayCommand QuickCash3Command { get; }

    public AsyncRelayCommand QuickCash4Command { get; }

    public string QuickCash1Text =>
        FormatQuickCashSuggestion(
            _quickCashAmounts[0]);

    public string QuickCash2Text =>
        FormatQuickCashSuggestion(
            _quickCashAmounts[1]);

    public string QuickCash3Text =>
        FormatQuickCashSuggestion(
            _quickCashAmounts[2]);

    public string QuickCash4Text =>
        FormatQuickCashSuggestion(
            _quickCashAmounts[3]);

    public AsyncRelayCommand CheckoutCommand { get; }

    public string? SearchTerm
    {
        get => _searchTerm;

        set => SetProperty(
            ref _searchTerm,
            value);
    }

    public string CashReceivedText
    {
        get => _cashReceivedText;

        set
        {
            if (!SetProperty(
                    ref _cashReceivedText,
                    value ?? string.Empty))
            {
                return;
            }

            /*
             * Cập nhật bốn nút ngay sau mỗi ký tự nhập.
             *
             * Ví dụ:
             * 5   → 5K, 50K, 500K, 5 TRIỆU
             * 20  → 20K, 200K, 2 TRIỆU, 20 TRIỆU
             */
            UpdateQuickCashSuggestions();

            NotifyCashPresentation();

            CheckoutCommand
                .NotifyCanExecuteChanged();
        }
    }

    public string OrderNotes
    {
        get => _orderNotes;

        set => SetProperty(
            ref _orderNotes,
            value ?? string.Empty);
    }

    public bool IsLoadingProducts
    {
        get => _isLoadingProducts;

        private set
        {
            if (!SetProperty(
                    ref _isLoadingProducts,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(IsBusy));

            NotifyCommandStates();
        }
    }

    public bool IsCheckingOut
    {
        get => _isCheckingOut;

        private set
        {
            if (!SetProperty(
                    ref _isCheckingOut,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(IsBusy));

            NotifyCommandStates();
        }
    }

    public bool IsBusy =>
        IsLoadingProducts ||
        IsCheckingOut;

    public string StatusMessage
    {
        get => _statusMessage;

        private set
        {
            if (!SetProperty(
                    ref _statusMessage,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasStatusMessage));
        }
    }

    public bool IsStatusError
    {
        get => _isStatusError;

        private set => SetProperty(
            ref _isStatusError,
            value);
    }

    public bool IsStatusSuccess
    {
        get => _isStatusSuccess;

        private set => SetProperty(
            ref _isStatusSuccess,
            value);
    }

    public bool HasStatusMessage =>
        !string.IsNullOrWhiteSpace(
            StatusMessage);

    public string CurrentCashierName =>
        _currentUserService.FullName ??
        _currentUserService.Username ??
        "Thu ngân";

    public string CurrentRoleText =>
        _currentUserService.Role switch
        {
            Role.Administrator =>
                "Quản trị viên",

            Role.Manager =>
                "Quản lý",

            Role.Cashier =>
                "Thu ngân",

            Role.InventoryStaff =>
                "Nhân viên kho",

            _ =>
                "Nhân viên"
        };

    public int CartItemCount =>
        CartLines.Sum(
            line =>
                line.Quantity);

    public int CartLineCount =>
        CartLines.Count;

    public decimal EstimatedTotal =>
        CartLines.Sum(
            line =>
                line.LineTotal);

    public string CartItemCountText =>
        $"{CartItemCount:N0} món";

    public string CartLineCountText =>
        $"{CartLineCount:N0} dòng hàng";

    public string EstimatedTotalText =>
        $"{EstimatedTotal.ToString(
            "N0",
            VietnameseCulture)} ₫";

    public string ProductResultText =>
        ProductCards.Count == 0
            ? "Không có sản phẩm"
            : $"{ProductCards.Count:N0} sản phẩm đang hiển thị";

    public bool HasCartItems =>
        CartLines.Count > 0;

    public bool HasLastOrder =>
        !string.IsNullOrWhiteSpace(
            LastOrderCode);

    public string? LastOrderCode
    {
        get => _lastOrderCode;

        private set
        {
            if (!SetProperty(
                    ref _lastOrderCode,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(HasLastOrder));
        }
    }

    public string? LastOrderSummary
    {
        get => _lastOrderSummary;

        private set => SetProperty(
            ref _lastOrderSummary,
            value);
    }

    public string CashPreviewText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(
                    CashReceivedText))
            {
                return "Chưa nhập tiền khách đưa";
            }

            if (!TryGetCashReceived(
                    out var amount))
            {
                return "Số tiền không hợp lệ";
            }

            return
                $"{amount.ToString(
                    "N0",
                    VietnameseCulture)} ₫";
        }
    }

    public string ChangePreviewText
    {
        get
        {
            if (!TryGetCashReceived(
                    out var cash))
            {
                return "—";
            }

            var change =
                (decimal)cash -
                EstimatedTotal;

            if (change < 0)
            {
                return
                    $"Thiếu {Math.Abs(change).ToString(
                        "N0",
                        VietnameseCulture)} ₫";
            }

            return
                $"{change.ToString(
                    "N0",
                    VietnameseCulture)} ₫";
        }
    }

    public bool HasEnoughCash
    {
        get
        {
            return
                TryGetCashReceived(
                    out var cash) &&
                (decimal)cash >=
                EstimatedTotal;
        }
    }

    public string CashHintText
    {
        get
        {
            if (!HasCartItems)
            {
                return
                    "Thêm sản phẩm trước khi nhập tiền.";
            }

            if (string.IsNullOrWhiteSpace(
                    CashReceivedText))
            {
                return
                    "Nhập tiền khách đưa hoặc chọn một mệnh giá nhanh.";
            }

            if (!TryGetCashReceived(
                    out _))
            {
                return
                    "Tiền khách đưa phải là số nguyên không âm.";
            }

            return HasEnoughCash
                ? "Số tiền đã đủ để thanh toán."
                : "Tiền khách đưa chưa đủ.";
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        await LoadCategoriesAsync();
        await LoadProductsAsync(
            autoAddExactMatch:
                false);
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var categoryService =
                scope.ServiceProvider
                    .GetRequiredService<
                        ICategoryService>();

            var result =
                await categoryService
                    .ListActiveAsync();

            CategoryFilters.Clear();

            CategoryFilters.Add(
                new SalesCategoryFilterViewModel(
                    categoryId:
                        null,

                    name:
                        "Tất cả",

                    isSelected:
                        true,

                    selectAsync:
                        SelectCategoryAsync));

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Không thể tải danh mục bán hàng: " +
                    "{Code} - {Message}",
                    result.Error.Code,
                    result.Error.Message);

                return;
            }

            foreach (var category in
                     result.Value)
            {
                CategoryFilters.Add(
                    new SalesCategoryFilterViewModel(
                        category.Id,
                        category.Name,
                        isSelected:
                            false,
                        SelectCategoryAsync));
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể tải danh mục cho màn hình bán hàng.");

            if (CategoryFilters.Count == 0)
            {
                CategoryFilters.Add(
                    new SalesCategoryFilterViewModel(
                        null,
                        "Tất cả",
                        true,
                        SelectCategoryAsync));
            }
        }
    }

    private async Task SelectCategoryAsync(
        SalesCategoryFilterViewModel
            selectedCategory)
    {
        ArgumentNullException.ThrowIfNull(
            selectedCategory);

        foreach (var category in
                 CategoryFilters)
        {
            category.IsSelected =
                ReferenceEquals(
                    category,
                    selectedCategory);
        }

        _selectedCategoryId =
            selectedCategory.CategoryId;

        await LoadProductsAsync(
            autoAddExactMatch:
                false);
    }

    private Task SearchAsync()
    {
        return LoadProductsAsync(
            autoAddExactMatch:
                true);
    }

    private Task RefreshAsync()
    {
        return LoadProductsAsync(
            autoAddExactMatch:
                false);
    }

    private async Task<bool> LoadProductsAsync(
        bool autoAddExactMatch)
    {
        if (IsLoadingProducts)
        {
            return false;
        }

        IsLoadingProducts = true;

        ShowNeutral(
            "Đang tải thực đơn bán hàng...");

        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var productService =
                scope.ServiceProvider
                    .GetRequiredService<
                        IProductService>();

            var request =
                new ProductSearchRequest(
                    searchTerm:
                        SearchTerm,

                    categoryId:
                        _selectedCategoryId,

                    isActive:
                        true,

                    isLowStock:
                        null,

                    pageNumber:
                        1,

                    pageSize:
                        CatalogPageSize);

            var result =
                await productService
                    .SearchAsync(
                        request);

            if (result.IsFailure)
            {
                ShowError(
                    result.Error.Message);

                return false;
            }

            var products =
                result.Value.Items
                    .Select(
                        product =>
                            new SalesProductCardViewModel(
                                product,
                                AddProductAsync))
                    .ToArray();

            ProductCards.Clear();

            foreach (var product in
                     products)
            {
                ProductCards.Add(
                    product);
            }

            OnPropertyChanged(
                nameof(ProductResultText));

            ShowNeutral(
                products.Length == 0
                    ? "Không tìm thấy sản phẩm phù hợp."
                    : $"Đã tải {products.Length:N0} sản phẩm.");

            if (autoAddExactMatch)
            {
                await TryAutoAddExactMatchAsync(
                    products);
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Không thể tải catalog bán hàng.");

            ShowError(
                "Không thể tải sản phẩm. " +
                exception
                    .GetBaseException()
                    .Message);

            return false;
        }
        finally
        {
            IsLoadingProducts = false;
        }
    }

    /// <summary>
    /// Hỗ trợ máy quét barcode:
    /// khi từ khóa trùng chính xác mã hoặc barcode
    /// và chỉ tìm thấy một sản phẩm, tự thêm vào giỏ.
    /// </summary>
    private async Task
        TryAutoAddExactMatchAsync(
            IReadOnlyList<
                SalesProductCardViewModel>
                products)
    {
        var search =
            SearchTerm?.Trim();

        if (string.IsNullOrWhiteSpace(
                search) ||
            products.Count != 1)
        {
            return;
        }

        var product =
            products[0];

        var exactMatch =
            string.Equals(
                product.Code,
                search,
                StringComparison
                    .OrdinalIgnoreCase)

            ||

            string.Equals(
                product.Barcode,
                search,
                StringComparison
                    .OrdinalIgnoreCase);

        if (!exactMatch)
        {
            return;
        }

        await AddProductAsync(
            product);

        SearchTerm =
            string.Empty;
    }

    private Task AddProductAsync(
        SalesProductCardViewModel product)
    {
        ArgumentNullException.ThrowIfNull(
            product);

        if (!product.CanSell)
        {
            ShowError(
                $"'{product.Name}' hiện không thể bán.");

            return Task.CompletedTask;
        }

        /*
         * Món đầu tiên của một giỏ mới đánh dấu việc bắt đầu
         * phiên bán hàng mới. Thông báo hóa đơn trước đó phải
         * biến mất ngay để thu ngân không nhầm với đơn hiện tại.
         */
        if (!HasCartItems)
        {
            BeginNewOrderSession();
        }

        var existingLine =
            CartLines.FirstOrDefault(
                line =>
                    line.ProductId ==
                    product.ProductId);

        if (existingLine is not null)
        {
            if (!existingLine.TryIncrease())
            {
                ShowError(
                    $"'{product.Name}' đã đạt số lượng " +
                    "tối đa có thể bán.");
            }
            else
            {
                ShowNeutral(
                    $"Đã tăng số lượng '{product.Name}'.");
            }

            return Task.CompletedTask;
        }

        var line =
            new SalesCartLineViewModel(
                product,
                OnCartLineChanged,
                RemoveCartLine);

        CartLines.Add(
            line);

        NotifyCartPresentation();

        ShowNeutral(
            $"Đã thêm '{product.Name}' vào đơn.");

        return Task.CompletedTask;
    }

    private void OnCartLineChanged(
        SalesCartLineViewModel line)
    {
        ArgumentNullException.ThrowIfNull(
            line);

        NotifyCartPresentation();

        ShowNeutral(
            $"Đã cập nhật số lượng " +
            $"'{line.ProductName}'.");
    }

    private void RemoveCartLine(
        SalesCartLineViewModel line)
    {
        ArgumentNullException.ThrowIfNull(
            line);

        if (!CartLines.Remove(
                line))
        {
            return;
        }

        NotifyCartPresentation();

        ShowNeutral(
            $"Đã xóa '{line.ProductName}' khỏi đơn.");
    }

    private Task ClearCartAsync()
    {
        _orderSessionVersion++;

        CancelLastOrderAutoDismiss();
        ClearLastOrderPresentation();

        CartLines.Clear();

        CashReceivedText =
            string.Empty;

        OrderNotes =
            string.Empty;

        NotifyCartPresentation();

        ShowNeutral(
            "Đã làm trống đơn hàng.");

        return Task.CompletedTask;
    }

    private Task SetExactCashAsync()
    {
        if (!TryConvertEstimatedTotal(
                out var total))
        {
            ShowError(
                "Tổng tiền vượt quá giới hạn thanh toán.");

            return Task.CompletedTask;
        }

        SetCashAmount(
            total);

        return Task.CompletedTask;
    }

    private AsyncRelayCommand
    CreateQuickCashCommand(
        int suggestionIndex)
    {
        if (suggestionIndex < 0 ||
            suggestionIndex >=
            _quickCashAmounts.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(suggestionIndex));
        }

        return new AsyncRelayCommand(
            () =>
            {
                /*
                 * Đọc giá trị mới nhất tại thời điểm bấm.
                 *
                 * Không capture một số tiền cố định từ constructor.
                 */
                var amount =
                    _quickCashAmounts[
                        suggestionIndex];

                SetCashAmount(
                    amount);

                return Task.CompletedTask;
            },
            () =>
                CanUseQuickCashSuggestion(
                    suggestionIndex),
            HandleCommandException);
    }

    private bool CanUseQuickCashSuggestion(
        int suggestionIndex)
    {
        return
            !IsBusy &&
            HasCartItems &&
            suggestionIndex >= 0 &&
            suggestionIndex <
            _quickCashAmounts.Length &&
            _quickCashAmounts[
                suggestionIndex] > 0 &&
            (decimal)_quickCashAmounts[
                suggestionIndex] >=
            EstimatedTotal;
    }

    /// <summary>
    /// Tính lại bốn mệnh giá gợi ý.
    ///
    /// Chế độ nhập nhanh:
    /// - 5   → 5K, 50K, 500K, 5 triệu;
    /// - 20  → 20K, 200K, 2 triệu, 20 triệu.
    ///
    /// Khi người dùng đã nhập một số tiền đầy đủ hoặc để trống,
    /// hệ thống gợi ý các mốc làm tròn phù hợp với tổng đơn.
    /// </summary>
    private void UpdateQuickCashSuggestions()
    {
        var suggestions =
            BuildQuickCashSuggestions();

        for (var index = 0;
             index <
             _quickCashAmounts.Length;
             index++)
        {
            _quickCashAmounts[index] =
                suggestions[index];
        }

        OnPropertyChanged(
            nameof(QuickCash1Text));

        OnPropertyChanged(
            nameof(QuickCash2Text));

        OnPropertyChanged(
            nameof(QuickCash3Text));

        OnPropertyChanged(
            nameof(QuickCash4Text));

        QuickCash1Command
            .NotifyCanExecuteChanged();

        QuickCash2Command
            .NotifyCanExecuteChanged();

        QuickCash3Command
            .NotifyCanExecuteChanged();

        QuickCash4Command
            .NotifyCanExecuteChanged();
    }

    private long[] BuildQuickCashSuggestions()
    {
        var suggestions =
            new List<long>(
                capacity: 12);

        var hasOrderTotal =
            TryConvertEstimatedTotal(
                out var orderTotal) &&
            orderTotal > 0;

        var hasEnteredAmount =
            TryGetCashReceived(
                out var enteredAmount) &&
            enteredAmount > 0;

        if (hasEnteredAmount)
        {
            /*
             * Quy tắc dễ đoán cho thu ngân:
             *
             * - nhập 5      → 5K, 50K, 500K, 5 triệu;
             * - nhập 50     → 50K, 500K, 5 triệu, 50 triệu;
             * - nhập 5000   → 5K, 50K, 500K, 5 triệu;
             * - nhập 25000  → 25K, 250K, 2,5 triệu, 25 triệu.
             *
             * Với số lẻ như 23.334, hệ thống giữ chính số đó
             * ở nút đầu rồi gợi ý các mốc tiền thực dụng tiếp theo.
             */
            if (ShouldUseScalableCashEntry(
                    enteredAmount))
            {
                var baseAmount =
                    enteredAmount < 1_000
                        ? checked(
                            enteredAmount *
                            1_000)
                        : enteredAmount;

                AddGeometricCashSuggestions(
                    suggestions,
                    baseAmount);
            }
            else
            {
                AddQuickCashCandidate(
                    suggestions,
                    enteredAmount);

                var practicalAnchor =
                    hasOrderTotal
                        ? Math.Max(
                            enteredAmount,
                            orderTotal)
                        : enteredAmount;

                AddPracticalCashSuggestions(
                    suggestions,
                    practicalAnchor);
            }
        }
        else if (hasOrderTotal)
        {
            AddPracticalCashSuggestions(
                suggestions,
                orderTotal);
        }

        /*
         * Danh sách dự phòng chỉ dùng để bổ sung đủ bốn nút.
         * Nó không còn thay thế toàn bộ gợi ý sau chữ số thứ tư.
         */
        var fallbackAmounts =
            new long[]
            {
                50_000,
                100_000,
                200_000,
                500_000,
                1_000_000,
                2_000_000,
                5_000_000,
                10_000_000
            };

        foreach (var fallbackAmount in
                 fallbackAmounts)
        {
            AddQuickCashCandidate(
                suggestions,
                fallbackAmount);
        }

        return suggestions
            .Take(
                _quickCashAmounts.Length)
            .ToArray();
    }

    private static bool ShouldUseScalableCashEntry(
        long enteredAmount)
    {
        if (enteredAmount is > 0 and < 1_000)
        {
            return true;
        }

        /*
         * Những số tròn dưới 100.000 thường là cách thu ngân
         * gõ tắt mệnh giá, ví dụ 5000 hoặc 20000.
         */
        return enteredAmount is >= 1_000 and < 100_000 &&
               enteredAmount % 1_000 == 0;
    }

    private static void
        AddGeometricCashSuggestions(
            ICollection<long> suggestions,
            long baseAmount)
    {
        var currentAmount =
            baseAmount;

        for (var index = 0;
             index < 4;
             index++)
        {
            AddQuickCashCandidate(
                suggestions,
                currentAmount);

            try
            {
                currentAmount =
                    checked(
                        currentAmount *
                        10);
            }
            catch (OverflowException)
            {
                break;
            }
        }
    }

    private static void
        AddPracticalCashSuggestions(
            ICollection<long> suggestions,
            long anchorAmount)
    {
        var roundingSteps =
            new long[]
            {
                10_000,
                20_000,
                50_000,
                100_000,
                200_000,
                500_000,
                1_000_000,
                2_000_000,
                5_000_000,
                10_000_000
            };

        foreach (var step in
                 roundingSteps)
        {
            AddQuickCashCandidate(
                suggestions,
                RoundUpCash(
                    anchorAmount,
                    step));
        }
    }

    private static void AddQuickCashCandidate(
        ICollection<long> suggestions,
        long amount)
    {
        if (amount <= 0 ||
            amount >
            BusinessRules.Orders
                .MaximumOrderAmount ||
            suggestions.Contains(
                amount))
        {
            return;
        }

        suggestions.Add(
            amount);
    }

    private static long RoundUpCash(
        long amount,
        long step)
    {
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(step));
        }

        if (amount <= 0)
        {
            return step;
        }

        var remainder =
            amount % step;

        if (remainder == 0)
        {
            return amount;
        }

        try
        {
            return checked(
                amount +
                step -
                remainder);
        }
        catch (OverflowException)
        {
            return BusinessRules.Orders
                .MaximumOrderAmount;
        }
    }

    private static string
        FormatQuickCashSuggestion(
            long amount)
    {
        if (amount >=
            1_000_000)
        {
            var millions =
                amount /
                1_000_000m;

            return
                $"{millions.ToString(
                    "0.#",
                    VietnameseCulture)} TRIỆU";
        }

        if (amount >=
            1_000)
        {
            var thousands =
                amount /
                1_000m;

            return
                $"{thousands.ToString(
                    "0.#",
                    VietnameseCulture)}K";
        }

        return amount.ToString(
            "N0",
            VietnameseCulture);
    }

    private void SetCashAmount(
        long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount));
        }

        CashReceivedText =
            amount.ToString(
                "N0",
                VietnameseCulture);

        ShowNeutral(
            $"Tiền khách đưa: " +
            $"{amount.ToString(
                "N0",
                VietnameseCulture)} ₫.");
    }

    private async Task CheckoutAsync()
    {
        if (!HasCartItems)
        {
            ShowError(
                "Đơn hàng chưa có sản phẩm.");

            return;
        }

        if (!TryGetCashReceived(
                out var cashReceived))
        {
            ShowError(
                "Tiền khách đưa không hợp lệ.");

            return;
        }

        if ((decimal)cashReceived <
            EstimatedTotal)
        {
            ShowError(
                "Tiền khách đưa chưa đủ thanh toán.");

            return;
        }

        var requestLines =
            CartLines
                .Select(
                    line =>
                        new CheckoutLineRequest(
                            productId:
                                line.ProductId,

                            quantity:
                                line.Quantity))
                .ToArray();

        var request =
            new CheckoutRequest(
                lines:
                    requestLines,

                paymentMethod:
                    PaymentMethod.Cash,

                cashReceived:
                    cashReceived,

                notes:
                    OrderNotes);

        IsCheckingOut = true;

        ShowNeutral(
            "Đang xác nhận giá, tồn kho và lưu giao dịch...");

        try
        {
            await using var scope =
                _scopeFactory
                    .CreateAsyncScope();

            var checkoutService =
                scope.ServiceProvider
                    .GetRequiredService<
                        ICheckoutService>();

            var result =
                await checkoutService
                    .CheckoutAsync(
                        request);

            if (result.IsFailure)
            {
                ShowError(
                    result.Error.Message);

                /*
                 * Tồn kho hoặc giá có thể đã thay đổi
                 * trên một cửa sổ/máy khác.
                 */
                await LoadProductsAsync(
                    autoAddExactMatch:
                        false);

                ShowError(
                    result.Error.Message);

                return;
            }

            var completedOrder =
                result.Value;

            var completedOrderSessionVersion =
                ++_orderSessionVersion;

            LastOrderCode =
                completedOrder.OrderCode;

            LastOrderSummary =
                $"Đã thu " +
                $"{completedOrder.CashReceived.ToString(
                    "N0",
                    VietnameseCulture)} ₫ • " +
                $"Trả lại " +
                $"{completedOrder.ChangeAmount.ToString(
                    "N0",
                    VietnameseCulture)} ₫";

            CartLines.Clear();

            CashReceivedText =
                string.Empty;

            OrderNotes =
                string.Empty;

            NotifyCartPresentation();

            await LoadProductsAsync(
                autoAddExactMatch:
                    false);

            var successMessage =
                $"Thanh toán thành công • " +
                $"{completedOrder.OrderCode} • " +
                $"{completedOrder.TotalAmount.ToString(
                    "N0",
                    VietnameseCulture)} ₫";

            ShowSuccess(
                successMessage);

            ScheduleLastOrderAutoDismiss(
                completedOrder.OrderCode,
                successMessage,
                completedOrderSessionVersion);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Thanh toán từ giao diện bán hàng thất bại.");

            ShowError(
                "Không thể hoàn tất thanh toán. " +
                exception
                    .GetBaseException()
                    .Message);
        }
        finally
        {
            IsCheckingOut = false;
        }
    }

    private void BeginNewOrderSession()
    {
        _orderSessionVersion++;

        CancelLastOrderAutoDismiss();
        ClearLastOrderPresentation();
    }

    private void ScheduleLastOrderAutoDismiss(
        string orderCode,
        string successMessage,
        long completedOrderSessionVersion)
    {
        CancelLastOrderAutoDismiss();

        var cancellationSource =
            new CancellationTokenSource();

        _lastOrderDismissalSource =
            cancellationSource;

        _ =
            DismissLastOrderAfterDelayAsync(
                orderCode,
                successMessage,
                completedOrderSessionVersion,
                cancellationSource);
    }

    private async Task DismissLastOrderAfterDelayAsync(
        string orderCode,
        string successMessage,
        long completedOrderSessionVersion,
        CancellationTokenSource cancellationSource)
    {
        try
        {
            await Task.Delay(
                LastOrderBannerLifetime,
                cancellationSource.Token);

            if (_isDisposed ||
                cancellationSource.IsCancellationRequested ||
                completedOrderSessionVersion !=
                _orderSessionVersion ||
                IsCheckingOut ||
                HasCartItems ||
                !string.Equals(
                    LastOrderCode,
                    orderCode,
                    StringComparison.Ordinal))
            {
                return;
            }

            ClearLastOrderPresentation();

            /*
             * Chỉ đưa màn hình về trạng thái nghỉ khi thông báo
             * hiện tại vẫn đúng là thông báo của hóa đơn vừa xong.
             * Không ghi đè lỗi hoặc thao tác mới của người dùng.
             */
            if (IsStatusSuccess &&
                string.Equals(
                    StatusMessage,
                    successMessage,
                    StringComparison.Ordinal))
            {
                ShowNeutral(
                    "Sẵn sàng nhận đơn hàng mới.");
            }
        }
        catch (OperationCanceledException)
        {
            /*
             * Đơn mới đã bắt đầu hoặc cửa sổ đã đóng.
             * Đây là luồng kết thúc có chủ ý.
             */
        }
        finally
        {
            if (ReferenceEquals(
                    _lastOrderDismissalSource,
                    cancellationSource))
            {
                _lastOrderDismissalSource =
                    null;

                cancellationSource.Dispose();
            }
        }
    }

    private void CancelLastOrderAutoDismiss()
    {
        var cancellationSource =
            _lastOrderDismissalSource;

        _lastOrderDismissalSource =
            null;

        if (cancellationSource is null)
        {
            return;
        }

        try
        {
            cancellationSource.Cancel();
        }
        finally
        {
            cancellationSource.Dispose();
        }
    }

    private void ClearLastOrderPresentation()
    {
        LastOrderCode =
            null;

        LastOrderSummary =
            null;
    }

    private bool TryGetCashReceived(
        out long amount)
    {
        var normalized =
            CashReceivedText
                .Trim()
                .Replace(
                    ".",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    ",",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    " ",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    "₫",
                    string.Empty,
                    StringComparison.Ordinal);

        if (normalized.Length == 0)
        {
            amount = 0;

            return false;
        }

        return long.TryParse(
                   normalized,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out amount)

               &&

               amount >= 0;
    }

    private bool TryConvertEstimatedTotal(
        out long total)
    {
        if (EstimatedTotal < 0 ||
            EstimatedTotal > long.MaxValue ||
            EstimatedTotal !=
            decimal.Truncate(
                EstimatedTotal))
        {
            total = 0;

            return false;
        }

        total =
            (long)EstimatedTotal;

        return true;
    }

    private bool CanLoadProducts()
    {
        return !IsBusy;
    }

    private bool CanClearCart()
    {
        return !IsBusy &&
               HasCartItems;
    }

    private bool CanSetCash()
    {
        return !IsBusy &&
               HasCartItems;
    }

    private bool CanCheckout()
    {
        return
            !IsBusy &&
            HasCartItems &&
            HasEnoughCash &&
            EstimatedTotal > 0 &&
            EstimatedTotal <=
            long.MaxValue;
    }

    private void NotifyCartPresentation()
    {
        OnPropertyChanged(
            nameof(CartItemCount));

        OnPropertyChanged(
            nameof(CartLineCount));

        OnPropertyChanged(
            nameof(EstimatedTotal));

        OnPropertyChanged(
            nameof(CartItemCountText));

        OnPropertyChanged(
            nameof(CartLineCountText));

        OnPropertyChanged(
            nameof(EstimatedTotalText));

        OnPropertyChanged(
            nameof(HasCartItems));

        /*
         * Tổng tiền thay đổi thì các mệnh giá gợi ý
         * cũng phải được tính lại.
         */
        UpdateQuickCashSuggestions();

        NotifyCashPresentation();
        NotifyCommandStates();
    }

    private void NotifyCashPresentation()
    {
        OnPropertyChanged(
            nameof(CashPreviewText));

        OnPropertyChanged(
            nameof(ChangePreviewText));

        OnPropertyChanged(
            nameof(HasEnoughCash));

        OnPropertyChanged(
            nameof(CashHintText));
    }

    private void NotifyCommandStates()
    {
        SearchCommand
            .NotifyCanExecuteChanged();

        RefreshCommand
            .NotifyCanExecuteChanged();

        ClearCartCommand
            .NotifyCanExecuteChanged();

        ExactCashCommand
            .NotifyCanExecuteChanged();

        QuickCash1Command
            .NotifyCanExecuteChanged();

        QuickCash2Command
            .NotifyCanExecuteChanged();

        QuickCash3Command
            .NotifyCanExecuteChanged();

        QuickCash4Command
            .NotifyCanExecuteChanged();

        CheckoutCommand
            .NotifyCanExecuteChanged();
    }

    private void HandleCommandException(
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Một lệnh trên màn hình bán hàng thất bại.");

        ShowError(
            "Thao tác không thể hoàn thành. " +
            exception
                .GetBaseException()
                .Message);
    }

    private void ShowNeutral(
        string message)
    {
        IsStatusError = false;
        IsStatusSuccess = false;
        StatusMessage = message;
    }

    private void ShowError(
        string message)
    {
        IsStatusError = true;
        IsStatusSuccess = false;
        StatusMessage = message;
    }

    private void ShowSuccess(
        string message)
    {
        IsStatusError = false;
        IsStatusSuccess = true;
        StatusMessage = message;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed =
            true;

        CancelLastOrderAutoDismiss();

        GC.SuppressFinalize(
            this);
    }
}