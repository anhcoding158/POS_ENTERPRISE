using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Printing;
using POS.Application.DTOs.Products;
using POS.Domain.Constants;
using POS.Domain.Enums;
using POS.Wpf.Commands;
using POS.Wpf.Services;

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

    /*
     * Metadata đối soát VietQR sẽ được nối vào Notes.
     * Giữ phần ghi chú người dùng tối đa 350 ký tự để
     * tổng Notes không vượt BusinessRules.Orders.NotesMaxLength.
     */
    private const int
        VietQrUserNotesMaxLength = 350;

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

    private readonly IReceiptPreviewService
        _receiptPreviewService;

    private readonly ISalesPaymentFlowService
        _paymentFlowService;

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

    private PaymentMethod
        _selectedPaymentMethod =
            PaymentMethod.Cash;

    private SalesPaymentAuthorization?
        _pendingVietQrAuthorization;

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
        IReceiptPreviewService receiptPreviewService,
        ISalesPaymentFlowService paymentFlowService,
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

        _receiptPreviewService =
            receiptPreviewService ??
            throw new ArgumentNullException(
                nameof(receiptPreviewService));

        _paymentFlowService =
            paymentFlowService ??
            throw new ArgumentNullException(
                nameof(paymentFlowService));

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

        SelectCashPaymentCommand =
            new AsyncRelayCommand(
                SelectCashPaymentAsync,
                CanSelectCashPayment,
                HandleCommandException);

        SelectVietQrPaymentCommand =
            new AsyncRelayCommand(
                SelectVietQrPaymentAsync,
                CanSelectVietQrPayment,
                HandleCommandException);

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

    public AsyncRelayCommand
        SelectCashPaymentCommand
    {
        get;
    }

    public AsyncRelayCommand
        SelectVietQrPaymentCommand
    {
        get;
    }

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
            var normalized =
                value ??
                string.Empty;

            if (HasPendingVietQrAuthorization &&
                !string.Equals(
                    _cashReceivedText,
                    normalized,
                    StringComparison.Ordinal))
            {
                OnPropertyChanged(
                    nameof(CashReceivedText));

                ShowPendingVietQrLockError();

                return;
            }

            if (!SetProperty(
                    ref _cashReceivedText,
                    normalized))
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

        set
        {
            var normalized =
                value ??
                string.Empty;

            if (HasPendingVietQrAuthorization &&
                !string.Equals(
                    _orderNotes,
                    normalized,
                    StringComparison.Ordinal))
            {
                /*
                 * Sau khi thu ngân đã xác nhận nhận tiền,
                 * nội dung đơn phải giữ nguyên cho lần thử lưu lại.
                 */
                OnPropertyChanged(
                    nameof(OrderNotes));

                ShowError(
                    "Đơn VietQR đã được xác nhận nhận tiền. " +
                    "Không được sửa ghi chú trước khi lưu xong.");

                return;
            }

            if (!SetProperty(
                    ref _orderNotes,
                    normalized))
            {
                return;
            }

            OnPropertyChanged(
                nameof(OrderNotesLengthText));

            CheckoutCommand
                .NotifyCanExecuteChanged();
        }
    }

    public PaymentMethod SelectedPaymentMethod =>
        _selectedPaymentMethod;

    public bool IsCashPaymentSelected =>
        SelectedPaymentMethod ==
        PaymentMethod.Cash;

    public bool IsVietQrPaymentSelected =>
        SelectedPaymentMethod ==
        PaymentMethod.VietQr;

    public bool IsVietQrEnabled =>
        _paymentFlowService
            .IsVietQrEnabled;

    public bool HasPendingVietQrAuthorization =>
        _pendingVietQrAuthorization is not null;

    public bool IsOrderLocked =>
        HasPendingVietQrAuthorization;

    public bool CanEditOrder =>
        !IsBusy &&
        !IsOrderLocked;

    public bool IsPaymentSelectionEnabled =>
        !IsBusy &&
        !IsOrderLocked;

    public bool IsCashInputEnabled =>
        IsCashPaymentSelected &&
        CanEditOrder;

    public int OrderNotesMaxLength =>
        IsVietQrPaymentSelected
            ? VietQrUserNotesMaxLength
            : BusinessRules.Orders
                .NotesMaxLength;

    public string OrderNotesLengthText =>
        $"{OrderNotes.Length:N0}/" +
        $"{OrderNotesMaxLength:N0}";

    public string SelectedPaymentMethodText =>
        SelectedPaymentMethod switch
        {
            PaymentMethod.Cash =>
                "Tiền mặt",

            PaymentMethod.VietQr =>
                "VietQR",

            _ =>
                "Không hỗ trợ"
        };

    public string PaymentMethodHintText
    {
        get
        {
            if (HasPendingVietQrAuthorization)
            {
                return
                    "Đã xác nhận nhận tiền VietQR. " +
                    "Giữ nguyên đơn và thử lưu lại; " +
                    "không yêu cầu khách chuyển thêm.";
            }

            if (IsCashPaymentSelected)
            {
                return
                    "Nhập tiền khách đưa, hệ thống sẽ tính tiền trả lại.";
            }

            return IsVietQrEnabled
                ? "Mở mã VietQR và chỉ lưu đơn sau khi thu ngân " +
                  "xác nhận cửa hàng đã nhận đủ tiền."
                : "VietQR chưa được bật trong cấu hình cửa hàng.";
        }
    }

    public string CheckoutButtonTitle =>
        HasPendingVietQrAuthorization
            ? "THỬ LƯU LẠI ĐƠN VIETQR"
            : IsVietQrPaymentSelected
                ? "MỞ MÃ THANH TOÁN VIETQR"
                : "THANH TOÁN TIỀN MẶT";

    public string CheckoutButtonSubtitle =>
        HasPendingVietQrAuthorization
            ? "Không mở mã mới • Giữ nguyên xác nhận cũ"
            : IsVietQrPaymentSelected
                ? "F8 • Quét mã và xác nhận thủ công"
                : "F8 • Xác nhận giá và tồn kho";

    public string PendingVietQrReferenceText =>
        _pendingVietQrAuthorization?
            .PaymentReference ??
        string.Empty;

    public string PendingVietQrAmountText =>
        _pendingVietQrAuthorization is null
            ? string.Empty
            : $"{_pendingVietQrAuthorization
                .ConfirmedPaymentAmount
                .ToString(
                    "N0",
                    VietnameseCulture)} ₫";

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

            NotifyPaymentPresentation();
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

            NotifyPaymentPresentation();
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
            if (IsVietQrPaymentSelected)
            {
                return "Không áp dụng";
            }

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
            if (IsVietQrPaymentSelected)
            {
                return "0 ₫";
            }

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
                IsCashPaymentSelected &&
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
            if (IsVietQrPaymentSelected)
            {
                return HasPendingVietQrAuthorization
                    ? "Đã giữ xác nhận VietQR cũ để thử lưu lại đơn."
                    : IsVietQrEnabled
                        ? "VietQR không sử dụng tiền khách đưa " +
                          "hoặc tiền trả lại."
                        : "VietQR chưa được cấu hình.";
            }

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

        if (HasPendingVietQrAuthorization)
        {
            ShowPendingVietQrLockError();

            return Task.CompletedTask;
        }

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

        if (HasPendingVietQrAuthorization)
        {
            /*
             * XAML ở checkpoint kế tiếp sẽ phủ lớp khóa lên giỏ.
             * Guard này là lớp bảo vệ thứ hai cho lời gọi ngoài UI.
             */
            ShowPendingVietQrLockError();

            NotifyCartPresentation();

            return;
        }

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

        if (HasPendingVietQrAuthorization)
        {
            ShowPendingVietQrLockError();

            return;
        }

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
        if (HasPendingVietQrAuthorization)
        {
            ShowPendingVietQrLockError();

            return Task.CompletedTask;
        }

        _orderSessionVersion++;

        CancelLastOrderAutoDismiss();
        ClearLastOrderPresentation();

        CartLines.Clear();

        CashReceivedText =
            string.Empty;

        OrderNotes =
            string.Empty;

        ResetPaymentState(
            resetSelectedMethod:
                true);

        NotifyCartPresentation();

        ShowNeutral(
            "Đã làm trống đơn hàng.");

        return Task.CompletedTask;
    }

    private Task SetExactCashAsync()
    {
        if (!IsCashPaymentSelected ||
            HasPendingVietQrAuthorization)
        {
            ShowError(
                "Tiền đủ chỉ áp dụng cho thanh toán tiền mặt.");

            return Task.CompletedTask;
        }

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
            IsCashPaymentSelected &&
            !HasPendingVietQrAuthorization &&
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
        if (!IsCashPaymentSelected ||
            HasPendingVietQrAuthorization)
        {
            ShowError(
                "Không thể nhập tiền mặt trong trạng thái hiện tại.");

            return;
        }

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

    private Task SelectCashPaymentAsync()
    {
        return SelectPaymentMethodAsync(
            PaymentMethod.Cash);
    }

    private Task SelectVietQrPaymentAsync()
    {
        return SelectPaymentMethodAsync(
            PaymentMethod.VietQr);
    }

    private Task SelectPaymentMethodAsync(
        PaymentMethod paymentMethod)
    {
        if (IsBusy)
        {
            return Task.CompletedTask;
        }

        if (HasPendingVietQrAuthorization)
        {
            ShowPendingVietQrLockError();

            return Task.CompletedTask;
        }

        if (paymentMethod ==
                PaymentMethod.VietQr &&
            !IsVietQrEnabled)
        {
            ShowError(
                "VietQR chưa được bật hoặc chưa được " +
                "cấu hình cho cửa hàng.");

            return Task.CompletedTask;
        }

        if (paymentMethod is not
            (PaymentMethod.Cash or
             PaymentMethod.VietQr))
        {
            ShowError(
                "Quầy bán hàng hiện chỉ hỗ trợ " +
                "tiền mặt và VietQR.");

            return Task.CompletedTask;
        }

        if (_selectedPaymentMethod ==
            paymentMethod)
        {
            return Task.CompletedTask;
        }

        _selectedPaymentMethod =
            paymentMethod;

        NotifyPaymentPresentation();
        NotifyCashPresentation();
        NotifyCommandStates();

        ShowNeutral(
            paymentMethod ==
            PaymentMethod.Cash
                ? "Đã chọn thanh toán tiền mặt."
                : "Đã chọn VietQR. Hệ thống chỉ lưu đơn " +
                  "sau khi thu ngân xác nhận đã nhận đủ tiền.");

        return Task.CompletedTask;
    }

    private async Task CheckoutAsync()
    {
        if (!HasCartItems)
        {
            ShowError(
                "Đơn hàng chưa có sản phẩm.");

            return;
        }

        if (!TryConvertEstimatedTotal(
                out var totalAmount) ||
            totalAmount <= 0 ||
            totalAmount >
            BusinessRules.Orders
                .MaximumOrderAmount)
        {
            ShowError(
                "Tổng tiền không hợp lệ hoặc vượt giới hạn thanh toán.");

            return;
        }

        if (OrderNotes.Length >
            OrderNotesMaxLength)
        {
            ShowError(
                IsVietQrPaymentSelected
                    ? $"Ghi chú VietQR chỉ được tối đa " +
                      $"{VietQrUserNotesMaxLength:N0} ký tự " +
                      "để chừa chỗ lưu thông tin đối soát."
                    : $"Ghi chú đơn hàng không được vượt quá " +
                      $"{BusinessRules.Orders.NotesMaxLength:N0} ký tự.");

            return;
        }

        long cashReceived;

        if (IsCashPaymentSelected)
        {
            if (!TryGetCashReceived(
                    out cashReceived))
            {
                ShowError(
                    "Tiền khách đưa không hợp lệ.");

                return;
            }

            if (cashReceived <
                totalAmount)
            {
                ShowError(
                    "Tiền khách đưa chưa đủ thanh toán.");

                return;
            }
        }
        else
        {
            cashReceived =
                0;
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

        ReceiptRequest? receiptToPreview =
            null;

        string? completedOrderCode =
            null;

        string? successMessage =
            null;

        var completedOrderSessionVersion =
            0L;

        SalesPaymentAuthorization?
            authorization =
                null;

        IsCheckingOut =
            true;

        ShowNeutral(
            HasPendingVietQrAuthorization
                ? "Đang thử lưu lại đơn bằng xác nhận " +
                  "VietQR đã có..."
                : IsVietQrPaymentSelected
                    ? "Đang chuẩn bị mã VietQR..."
                    : "Đang xác thực tiền mặt...");

        try
        {
            var authorizationResult =
                await _paymentFlowService
                    .AuthorizeAsync(
                        new SalesPaymentAuthorizationRequest(
                            paymentMethod:
                                SelectedPaymentMethod,

                            totalAmount:
                                totalAmount,

                            cashReceived:
                                cashReceived,

                            existingAuthorization:
                                _pendingVietQrAuthorization));

            if (authorizationResult.IsFailure)
            {
                ShowError(
                    authorizationResult
                        .Error
                        .Message);

                return;
            }

            if (authorizationResult
                .Value
                .IsCancelled)
            {
                ShowNeutral(
                    "Đã hủy thanh toán VietQR. " +
                    "Đơn hàng chưa được lưu.");

                return;
            }

            authorization =
                authorizationResult
                    .Value
                    .Authorization;

            if (authorization is null)
            {
                ShowError(
                    "Luồng thanh toán không trả về xác nhận hợp lệ.");

                return;
            }

            if (authorization.IsVietQr)
            {
                SetPendingVietQrAuthorization(
                    authorization);
            }

            if (!TryBuildCheckoutNotes(
                    authorization,
                    out var checkoutNotes,
                    out var notesError))
            {
                ShowError(
                    notesError);

                return;
            }

            var request =
                new CheckoutRequest(
                    lines:
                        requestLines,

                    paymentMethod:
                        authorization
                            .PaymentMethod,

                    cashReceived:
                        authorization
                            .CashReceived,

                    notes:
                        checkoutNotes,

                    confirmedPaymentAmount:
                        authorization
                            .ConfirmedPaymentAmount);

            ShowNeutral(
                authorization.IsVietQr
                    ? "Đã xác nhận VietQR. Đang kiểm tra giá, " +
                      "tồn kho và lưu giao dịch..."
                    : "Đang xác nhận giá, tồn kho và lưu giao dịch...");

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
                /*
                 * Tồn kho hoặc giá có thể đã thay đổi
                 * trên một cửa sổ/máy khác.
                 *
                 * Với VietQR, authorization được giữ lại.
                 * Lần thử sau không mở QR mới.
                 */
                await LoadProductsAsync(
                    autoAddExactMatch:
                        false);

                ShowCheckoutFailure(
                    result.Error,
                    authorization);

                return;
            }

            var completedOrder =
                result.Value;

            completedOrderCode =
                completedOrder.OrderCode;

            completedOrderSessionVersion =
                ++_orderSessionVersion;

            receiptToPreview =
                completedOrder.ReceiptSnapshot;

            if (receiptToPreview is null)
            {
                _logger.LogError(
                    "Checkout {OrderCode} đã commit nhưng " +
                    "không trả về receipt snapshot.",
                    completedOrder.OrderCode);
            }

            LastOrderCode =
                completedOrder.OrderCode;

            LastOrderSummary =
                BuildCompletedOrderSummary(
                    completedOrder,
                    authorization);

            /*
             * Chỉ xóa authorization sau khi CheckoutService
             * trả về success và transaction đã commit.
             */
            ResetPaymentState(
                resetSelectedMethod:
                    true);

            CartLines.Clear();

            CashReceivedText =
                string.Empty;

            OrderNotes =
                string.Empty;

            NotifyCartPresentation();

            await LoadProductsAsync(
                autoAddExactMatch:
                    false);

            successMessage =
                $"Thanh toán {FormatPaymentMethod(
                    completedOrder.PaymentMethod)} thành công • " +
                $"{completedOrder.OrderCode} • " +
                $"{completedOrder.TotalAmount.ToString(
                    "N0",
                    VietnameseCulture)} ₫";

            ShowSuccess(
                successMessage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Thanh toán từ giao diện bán hàng thất bại.");

            if (authorization?.IsVietQr ==
                true)
            {
                ShowCheckoutFailure(
                    "Không thể hoàn tất thanh toán. " +
                    exception
                        .GetBaseException()
                        .Message,
                    authorization);
            }
            else
            {
                ShowError(
                    "Không thể hoàn tất thanh toán. " +
                    exception
                        .GetBaseException()
                        .Message);
            }
        }
        finally
        {
            IsCheckingOut =
                false;
        }

        /*
         * Receipt preview chỉ được mở sau khi:
         * - CheckoutService đã trả success;
         * - transaction đã commit;
         * - overlay khóa checkout đã được gỡ.
         *
         * Lỗi preview hoặc lỗi máy in không được biến
         * giao dịch đã lưu thành checkout thất bại.
         */
        if (receiptToPreview is not null)
        {
            await ShowReceiptPreviewAsync(
                receiptToPreview);
        }
        else if (!string.IsNullOrWhiteSpace(
                     completedOrderCode))
        {
            ShowError(
                $"Giao dịch {completedOrderCode} đã lưu thành công " +
                "nhưng chưa thể tạo bản xem trước hóa đơn.");
        }

        if (!string.IsNullOrWhiteSpace(
                completedOrderCode) &&
            !string.IsNullOrWhiteSpace(
                successMessage))
        {
            ScheduleLastOrderAutoDismiss(
                completedOrderCode,
                successMessage,
                completedOrderSessionVersion);
        }
    }

    private bool TryBuildCheckoutNotes(
        SalesPaymentAuthorization authorization,
        out string? checkoutNotes,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(
            authorization);

        var userNotes =
            string.IsNullOrWhiteSpace(
                OrderNotes)
                ? null
                : OrderNotes.Trim();

        if (!authorization.IsVietQr)
        {
            if (userNotes?.Length >
                BusinessRules.Orders
                    .NotesMaxLength)
            {
                checkoutNotes =
                    null;

                errorMessage =
                    "Ghi chú đơn hàng vượt quá giới hạn.";

                return false;
            }

            checkoutNotes =
                userNotes;

            errorMessage =
                string.Empty;

            return true;
        }

        if (userNotes?.Length >
            VietQrUserNotesMaxLength)
        {
            checkoutNotes =
                null;

            errorMessage =
                $"Ghi chú VietQR chỉ được tối đa " +
                $"{VietQrUserNotesMaxLength:N0} ký tự.";

            return false;
        }

        var paymentReference =
            authorization
                .PaymentReference;

        var transferContent =
            authorization
                .TransferContent;

        if (string.IsNullOrWhiteSpace(
                paymentReference) ||
            string.IsNullOrWhiteSpace(
                transferContent))
        {
            checkoutNotes =
                null;

            errorMessage =
                "Xác nhận VietQR thiếu thông tin đối soát.";

            return false;
        }

        var reconciliationNote =
            $"[VIETQR] Ref={paymentReference}; " +
            $"Content={transferContent}";

        checkoutNotes =
            userNotes is null
                ? reconciliationNote
                : $"{userNotes}" +
                  $"{Environment.NewLine}" +
                  $"{reconciliationNote}";

        if (checkoutNotes.Length >
            BusinessRules.Orders
                .NotesMaxLength)
        {
            checkoutNotes =
                null;

            errorMessage =
                "Ghi chú và thông tin đối soát VietQR " +
                "vượt quá giới hạn lưu trữ.";

            return false;
        }

        errorMessage =
            string.Empty;

        return true;
    }

    private static string BuildCompletedOrderSummary(
        CheckoutResultDto completedOrder,
        SalesPaymentAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(
            completedOrder);

        ArgumentNullException.ThrowIfNull(
            authorization);

        if (authorization.IsVietQr)
        {
            return
                $"VietQR • " +
                $"{authorization.PaymentReference} • " +
                $"Đã xác nhận " +
                $"{completedOrder.TotalAmount.ToString(
                    "N0",
                    VietnameseCulture)} ₫";
        }

        return
            $"Đã thu " +
            $"{completedOrder.CashReceived.ToString(
                "N0",
                VietnameseCulture)} ₫ • " +
            $"Trả lại " +
            $"{completedOrder.ChangeAmount.ToString(
                "N0",
                VietnameseCulture)} ₫";
    }

    private void ShowCheckoutFailure(
        Error error,
        SalesPaymentAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(
            authorization);

        if (!authorization.IsVietQr)
        {
            ShowError(
                error.Message);

            return;
        }

        /*
         * Khách có thể đã chuyển tiền thật.
         *
         * Không được hiển thị thông báo chung khiến thu ngân
         * tạo QR khác hoặc yêu cầu khách chuyển lần hai.
         */
        if (string.Equals(
                error.Code,
                ErrorCodes.Payments
                    .VietQrAmountMismatch,
                StringComparison.Ordinal))
        {
            ShowError(
                "ĐÃ XÁC NHẬN NHẬN TIỀN VIETQR NHƯNG " +
                "TỔNG ĐƠN TRONG HỆ THỐNG ĐÃ THAY ĐỔI. " +
                "Không yêu cầu khách chuyển thêm và không tạo QR mới. " +
                $"Mã tham chiếu: " +
                $"{authorization.PaymentReference}. " +
                $"Số tiền đã nhận: " +
                $"{authorization.ConfirmedPaymentAmount.ToString(
                    "N0",
                    VietnameseCulture)} ₫. " +
                $"Chi tiết: {error.Message} " +
                "Giữ nguyên đơn và báo quản lý kiểm tra giá, tồn kho " +
                "hoặc dữ liệu sản phẩm trước khi thử lưu lại.");

            return;
        }

        ShowError(
            "ĐÃ XÁC NHẬN NHẬN TIỀN VIETQR NHƯNG ĐƠN CHƯA LƯU. " +
            "Không yêu cầu khách chuyển thêm. " +
            $"Mã tham chiếu: " +
            $"{authorization.PaymentReference}. " +
            $"Số tiền đã nhận: " +
            $"{authorization.ConfirmedPaymentAmount.ToString(
                "N0",
                VietnameseCulture)} ₫. " +
            $"Lỗi: {error.Message} " +
            "Giữ nguyên giỏ và bấm “Thử lưu lại đơn VietQR” " +
            "sau khi đã xử lý nguyên nhân, hoặc báo quản lý.");
    }

    private void ShowCheckoutFailure(
        string failureMessage,
        SalesPaymentAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(
            authorization);

        if (!authorization.IsVietQr)
        {
            ShowError(
                failureMessage);

            return;
        }

        ShowError(
            "ĐÃ XÁC NHẬN NHẬN TIỀN VIETQR NHƯNG ĐƠN CHƯA LƯU. " +
            "Không yêu cầu khách chuyển thêm. " +
            $"Mã tham chiếu: " +
            $"{authorization.PaymentReference}. " +
            $"Số tiền đã nhận: " +
            $"{authorization.ConfirmedPaymentAmount.ToString(
                "N0",
                VietnameseCulture)} ₫. " +
            $"Lỗi: {failureMessage} " +
            "Giữ nguyên giỏ và thử lưu lại sau khi đã xử lý nguyên nhân, " +
            "hoặc báo quản lý.");
    }

    private static string FormatPaymentMethod(
        PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Cash =>
                "tiền mặt",

            PaymentMethod.VietQr =>
                "VietQR",

            PaymentMethod.BankTransfer =>
                "chuyển khoản",

            PaymentMethod.Card =>
                "thẻ",

            _ =>
                "không xác định"
        };
    }

    private async Task ShowReceiptPreviewAsync(
        ReceiptRequest receipt)
    {
        ArgumentNullException.ThrowIfNull(
            receipt);

        try
        {
            await _receiptPreviewService
                .ShowAsync(
                    receipt);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Giao dịch {OrderCode} đã lưu nhưng " +
                "không thể mở receipt preview.",
                receipt.OrderCode);

            ShowError(
                $"Giao dịch {receipt.OrderCode} đã lưu thành công, " +
                "nhưng không thể mở màn xem trước hóa đơn. " +
                exception
                    .GetBaseException()
                    .Message);
        }
    }

    private void BeginNewOrderSession()
    {
        _orderSessionVersion++;

        CancelLastOrderAutoDismiss();
        ClearLastOrderPresentation();

        ResetPaymentState(
            resetSelectedMethod:
                false);
    }

    private void SetPendingVietQrAuthorization(
        SalesPaymentAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(
            authorization);

        if (!authorization.IsVietQr)
        {
            throw new ArgumentException(
                "Chỉ authorization VietQR mới được giữ để thử lại.",
                nameof(authorization));
        }

        _pendingVietQrAuthorization =
            authorization;

        _selectedPaymentMethod =
            PaymentMethod.VietQr;

        NotifyPaymentPresentation();
        NotifyCashPresentation();
        NotifyCommandStates();
    }

    private void ResetPaymentState(
        bool resetSelectedMethod)
    {
        _pendingVietQrAuthorization =
            null;

        if (resetSelectedMethod)
        {
            _selectedPaymentMethod =
                PaymentMethod.Cash;
        }

        NotifyPaymentPresentation();
        NotifyCashPresentation();
        NotifyCommandStates();
    }

    private void ShowPendingVietQrLockError()
    {
        ShowError(
            "Đơn này đã được thu ngân xác nhận nhận tiền VietQR. " +
            "Không được sửa món, số lượng, ghi chú hoặc phương thức " +
            "thanh toán trước khi lưu xong. " +
            "Không yêu cầu khách chuyển thêm.");
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
        return
            !IsBusy &&
            !HasPendingVietQrAuthorization &&
            HasCartItems;
    }

    private bool CanSetCash()
    {
        return
            !IsBusy &&
            IsCashPaymentSelected &&
            !HasPendingVietQrAuthorization &&
            HasCartItems;
    }

    private bool CanSelectCashPayment()
    {
        return
            !IsBusy &&
            !HasPendingVietQrAuthorization;
    }

    private bool CanSelectVietQrPayment()
    {
        return
            !IsBusy &&
            !HasPendingVietQrAuthorization &&
            IsVietQrEnabled;
    }

    private bool CanCheckout()
    {
        if (IsBusy ||
            !HasCartItems ||
            EstimatedTotal <= 0 ||
            EstimatedTotal >
            BusinessRules.Orders
                .MaximumOrderAmount ||
            EstimatedTotal >
            long.MaxValue)
        {
            return false;
        }

        if (HasPendingVietQrAuthorization)
        {
            return
                IsVietQrPaymentSelected &&
                _pendingVietQrAuthorization is not null &&
                TryConvertEstimatedTotal(
                    out var pendingTotal) &&
                pendingTotal ==
                _pendingVietQrAuthorization
                    .ConfirmedPaymentAmount;
        }

        if (IsCashPaymentSelected)
        {
            return
                HasEnoughCash;
        }

        return
            IsVietQrPaymentSelected &&
            IsVietQrEnabled &&
            OrderNotes.Length <=
            VietQrUserNotesMaxLength;
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

    private void NotifyPaymentPresentation()
    {
        OnPropertyChanged(
            nameof(SelectedPaymentMethod));

        OnPropertyChanged(
            nameof(IsCashPaymentSelected));

        OnPropertyChanged(
            nameof(IsVietQrPaymentSelected));

        OnPropertyChanged(
            nameof(IsVietQrEnabled));

        OnPropertyChanged(
            nameof(HasPendingVietQrAuthorization));

        OnPropertyChanged(
            nameof(IsOrderLocked));

        OnPropertyChanged(
            nameof(CanEditOrder));

        OnPropertyChanged(
            nameof(IsPaymentSelectionEnabled));

        OnPropertyChanged(
            nameof(IsCashInputEnabled));

        OnPropertyChanged(
            nameof(OrderNotesMaxLength));

        OnPropertyChanged(
            nameof(OrderNotesLengthText));

        OnPropertyChanged(
            nameof(SelectedPaymentMethodText));

        OnPropertyChanged(
            nameof(PaymentMethodHintText));

        OnPropertyChanged(
            nameof(CheckoutButtonTitle));

        OnPropertyChanged(
            nameof(CheckoutButtonSubtitle));

        OnPropertyChanged(
            nameof(PendingVietQrReferenceText));

        OnPropertyChanged(
            nameof(PendingVietQrAmountText));
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

        SelectCashPaymentCommand
            .NotifyCanExecuteChanged();

        SelectVietQrPaymentCommand
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

        _pendingVietQrAuthorization =
            null;

        GC.SuppressFinalize(
            this);
    }
}