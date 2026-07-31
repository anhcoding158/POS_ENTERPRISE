using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Payments;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.HeldSales;
using POS.Application.DTOs.Payments;
using POS.Application.DTOs.Printing;
using POS.Application.DTOs.Products;
using POS.Domain.Constants;
using POS.Domain.Common;
using POS.Domain.Enums;
using POS.Domain.Services;
using POS.Application.Authorization;
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

    private readonly ICheckoutRecoveryConfirmationService
        _recoveryConfirmation;

    private readonly ILogger<SalesViewModel>
        _logger;

    private string? _searchTerm;
    private string? _scanCode;
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
    private Guid? _checkoutClientRequestId;
    private Guid? _paymentIntentClientRequestId;
    private int? _pendingPaymentIntentId;
    private CheckoutRecoveryItemViewModel? _selectedRecovery;
    private PaymentIntentRecoveryItemViewModel? _selectedPaymentIntentRecovery;
    private bool _isLoadingRecovery;
    private bool _isProcessingRecovery;
    private string? _paymentIntentRecoveryError;
    private bool _isPaymentIntentRecoveryOpen;
    private CancellationTokenSource? _recoveryLoadSource;
    private CancellationTokenSource? _paymentIntentRecoveryLoadSource;
    private readonly Dictionary<int, CheckoutRecoveryDto>
        _confirmedCheckoutRecoveries = [];
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private SalesCartLineViewModel? _selectedCartLine;
    private readonly IHeldSaleDialogService _heldSaleDialogs;
    private Guid? _holdClientRequestId;
    private int? _activeHeldSaleId;
    private SalesDiscountRequest _salesDiscount = SalesDiscountRequest.None;
    private int _activeHeldSaleCount;
    private bool _isHeldSaleBusy;

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
        ILogger<SalesViewModel> logger,
        ICheckoutRecoveryConfirmationService recoveryConfirmation,
        IHeldSaleDialogService? heldSaleDialogs = null)
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

        _recoveryConfirmation =
            recoveryConfirmation ??
            throw new ArgumentNullException(
                nameof(recoveryConfirmation));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));

        _heldSaleDialogs = heldSaleDialogs ?? new NullHeldSaleDialogService();

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

        HoldSaleCommand = new AsyncRelayCommand(
            HoldSaleAsync,
            CanHoldSale,
            HandleCommandException);

        OpenHeldSalesCommand = new AsyncRelayCommand(
            OpenHeldSalesAsync,
            () => !IsBusy && !_isHeldSaleBusy,
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

        RetryRecoveryCommand =
            new AsyncRelayCommand(
                RetryRecoveryAsync,
                () => SelectedRecovery?.CanRetry == true && !IsRecoveryBusy,
                HandleCommandException);

        AbandonRecoveryCommand =
            new AsyncRelayCommand(
                AbandonRecoveryAsync,
                () => SelectedRecovery?.CanAbandon == true && !IsRecoveryBusy,
                HandleCommandException);

        AcknowledgeRecoveryCommand =
            new AsyncRelayCommand(
                AcknowledgeRecoveryAsync,
                () => SelectedRecovery?.IsCompleted == true && !IsRecoveryBusy,
                HandleCommandException);

        OpenRecoveryReceiptCommand =
            new AsyncRelayCommand(
                OpenRecoveryReceiptAsync,
                () => SelectedRecovery?.CanOpenReceipt == true && !IsRecoveryBusy,
                HandleCommandException);

        RetryPaymentIntentRecoveryCommand =
            new AsyncRelayCommand(
                RetryPaymentIntentRecoveryAsync,
                CanRetryPaymentIntentRecovery,
                HandleCommandException);

        ShowPaymentIntentQrCommand =
            new AsyncRelayCommand(
                ShowPaymentIntentQrAsync,
                () => SelectedPaymentIntentRecovery?.CanShowQr == true &&
                      !IsProcessingRecovery,
                HandleCommandException);

        ConfirmPaymentIntentRecoveryCommand =
            new AsyncRelayCommand(
                ConfirmPaymentIntentRecoveryAsync,
                () => SelectedPaymentIntentRecovery?.CanConfirm == true &&
                      !IsProcessingRecovery,
                HandleCommandException);

        CancelPaymentIntentRecoveryCommand =
            new AsyncRelayCommand(
                CancelPaymentIntentRecoveryAsync,
                () => SelectedPaymentIntentRecovery?.CanCancel == true &&
                      !IsProcessingRecovery,
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

    public ObservableCollection<CheckoutRecoveryItemViewModel>
        CheckoutRecoveries
    {
        get;
    } = [];

    public ObservableCollection<PaymentIntentRecoveryItemViewModel>
        PendingPaymentIntents
    {
        get;
    } = [];

    public bool HasPendingPaymentIntentRecovery =>
        PendingPaymentIntents.Count > 0;

    public string PendingPaymentIntentButtonText =>
        $"VietQR cần xử lý ({PendingPaymentIntents.Count:N0})";

    public int ManualReviewPaymentIntentCount =>
        PendingPaymentIntents.Count(value => value.IsManualReview);

    public bool HasManualReviewPaymentIntentWarning =>
        ManualReviewPaymentIntentCount > 0;

    public string ManualReviewPaymentIntentWarningText =>
        ManualReviewPaymentIntentCount == 1
            ? "Có 1 giao dịch VietQR đã xác nhận nhận tiền cần xử lý."
            : $"Có {ManualReviewPaymentIntentCount} giao dịch VietQR đã xác nhận nhận tiền cần xử lý.";

    public bool IsPaymentIntentRecoveryOpen
    {
        get => _isPaymentIntentRecoveryOpen;
        private set
        {
            if (!SetProperty(ref _isPaymentIntentRecoveryOpen, value))
                return;
            OnPropertyChanged(nameof(IsOrderLocked));
            OnPropertyChanged(nameof(CanEditOrder));
            OnPropertyChanged(nameof(IsPaymentSelectionEnabled));
            OnPropertyChanged(nameof(IsCashInputEnabled));
            NotifyCommandStates();
        }
    }

    public void OpenPaymentIntentRecovery()
    {
        if (HasPendingPaymentIntentRecovery)
            IsPaymentIntentRecoveryOpen = true;
    }

    public void ClosePaymentIntentRecoveryForLater() =>
        IsPaymentIntentRecoveryOpen = false;

    public bool ContinueSalesAfterManualReviewWarning()
    {
        if (SelectedPaymentIntentRecovery?.CanContinueSales != true ||
            !_recoveryConfirmation.ConfirmContinueSales())
            return false;

        IsPaymentIntentRecoveryOpen = false;
        return true;
    }

    public async Task<bool> ResolveSelectedPaymentIntentManuallyAsync(
        ResolvePaymentIntentManuallyRequest request)
    {
        if (SelectedPaymentIntentRecovery?.Id != request.PaymentIntentId)
            return false;
        IsProcessingRecovery = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IPaymentIntentService>();
            var result = await service.ResolveManuallyAsync(request);
            if (result.IsFailure)
            {
                ShowError(result.AppError.Message);
                return false;
            }
            await ReloadRecoveryStateAsync(openHighestPriority: false);
            IsPaymentIntentRecoveryOpen = false;
            ShowSuccess("Đã lưu kết quả xử lý thủ công VietQR.");
            return true;
        }
        finally
        {
            IsProcessingRecovery = false;
        }
    }

    public async Task<IReadOnlyList<PaymentIntentManualResolutionDto>>
        LoadPaymentIntentManualResolutionHistoryAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPaymentIntentService>();
            var result = await service.GetManualResolutionHistoryAsync(100);
            if (result.IsSuccess)
                return result.Value;
            ShowError(result.AppError.Message);
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(
                _logger, exception, "Không thể tải lịch sử xử lý thủ công PaymentIntent.");
            ShowError("Không thể tải lịch sử xử lý thủ công VietQR.");
        }

        return Array.Empty<PaymentIntentManualResolutionDto>();
    }

    public PaymentIntentRecoveryItemViewModel? SelectedPaymentIntentRecovery
    {
        get => _selectedPaymentIntentRecovery;
        set
        {
            if (SetProperty(ref _selectedPaymentIntentRecovery, value))
            {
                RetryPaymentIntentRecoveryCommand.NotifyCanExecuteChanged();
                ShowPaymentIntentQrCommand.NotifyCanExecuteChanged();
                ConfirmPaymentIntentRecoveryCommand.NotifyCanExecuteChanged();
                CancelPaymentIntentRecoveryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public SalesCartLineViewModel? SelectedCartLine
    {
        get => _selectedCartLine;
        set => SetProperty(ref _selectedCartLine, value);
    }

    public CheckoutRecoveryItemViewModel? SelectedRecovery
    {
        get => _selectedRecovery;
        set
        {
            if (!SetProperty(ref _selectedRecovery, value))
            {
                return;
            }

            NotifyRecoveryPresentation();
        }
    }

    public bool HasCheckoutRecovery => CheckoutRecoveries.Count > 0;

    public bool IsRecoveryBusy => IsLoadingRecovery || IsProcessingRecovery;
    public bool IsRecoveryOperationIdle => !IsProcessingRecovery;
    public string? PaymentIntentRecoveryError
    {
        get => _paymentIntentRecoveryError;
        private set
        {
            if (SetProperty(ref _paymentIntentRecoveryError, value))
                OnPropertyChanged(nameof(HasPaymentIntentRecoveryError));
        }
    }
    public bool HasPaymentIntentRecoveryError =>
        !string.IsNullOrWhiteSpace(PaymentIntentRecoveryError);

    public bool IsLoadingRecovery
    {
        get => _isLoadingRecovery;
        private set
        {
            if (SetProperty(ref _isLoadingRecovery, value))
            {
                OnPropertyChanged(nameof(IsRecoveryBusy));
                OnPropertyChanged(nameof(IsRecoveryOperationIdle));
                NotifyRecoveryCommands();
            }
        }
    }

    public bool IsProcessingRecovery
    {
        get => _isProcessingRecovery;
        private set
        {
            if (SetProperty(ref _isProcessingRecovery, value))
            {
                OnPropertyChanged(nameof(IsRecoveryBusy));
                OnPropertyChanged(nameof(IsRecoveryOperationIdle));
                NotifyRecoveryCommands();
            }
        }
    }

    public AsyncRelayCommand RetryRecoveryCommand { get; }

    public AsyncRelayCommand AbandonRecoveryCommand { get; }

    public AsyncRelayCommand AcknowledgeRecoveryCommand { get; }

    public AsyncRelayCommand OpenRecoveryReceiptCommand { get; }

    public AsyncRelayCommand RetryPaymentIntentRecoveryCommand { get; }
    public AsyncRelayCommand ShowPaymentIntentQrCommand { get; }
    public AsyncRelayCommand ConfirmPaymentIntentRecoveryCommand { get; }
    public AsyncRelayCommand CancelPaymentIntentRecoveryCommand { get; }

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand ClearCartCommand { get; }

    public AsyncRelayCommand HoldSaleCommand { get; }

    public AsyncRelayCommand OpenHeldSalesCommand { get; }

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

    public string? ScanCode
    {
        get => _scanCode;

        set => SetProperty(
            ref _scanCode,
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
        HasPendingVietQrAuthorization ||
        HasCheckoutRecovery ||
        IsPaymentIntentRecoveryOpen;

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
            : "F8 · Thanh toán";

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

    public decimal EstimatedSubtotal =>
        CartLines.Sum(
            line =>
                line.LineTotal);

    public long ResolvedDiscountAmount
    {
        get
        {
            if (_salesDiscount.Type == SalesDiscountType.None || EstimatedSubtotal <= 0)
                return 0;
            try
            {
                return SalesDiscountCalculator.Resolve(
                    checked((long)EstimatedSubtotal), _salesDiscount.Type,
                    _salesDiscount.Value, _salesDiscount.Reason);
            }
            catch (DomainException)
            {
                return 0;
            }
        }
    }

    public decimal EstimatedTotal => EstimatedSubtotal - ResolvedDiscountAmount;
    public string EstimatedSubtotalText =>
        $"{EstimatedSubtotal.ToString("N0", VietnameseCulture)} ₫";
    public string ResolvedDiscountAmountText =>
        ResolvedDiscountAmount == 0
            ? "0 ₫"
            : $"-{ResolvedDiscountAmount.ToString("N0", VietnameseCulture)} ₫";
    public string SalesDiscountValidationText =>
        IsSalesDiscountValid
            ? string.Empty
            : "Giảm giá hiện tại không còn hợp lệ. Hãy sửa hoặc xóa giảm giá trước khi thanh toán.";
    public bool HasSalesDiscount => _salesDiscount.Type != SalesDiscountType.None;
    public bool IsSalesDiscountValid =>
        !HasSalesDiscount || ResolvedDiscountAmount > 0;
    public bool CanApplySalesDiscount =>
        !IsCheckingOut && !HasPendingVietQrAuthorization &&
        (_currentUserService.Role is Role.Administrator or Role.Manager);
    public string SalesDiscountSummary => !HasSalesDiscount
        ? "Chưa áp dụng giảm giá"
        : $"{(_salesDiscount.Type == SalesDiscountType.FixedAmount ? "Theo số tiền" : "Theo phần trăm")} · " +
          $"-{ResolvedDiscountAmount.ToString("N0", VietnameseCulture)} ₫ · {_salesDiscount.Reason}";
    public SalesDiscountRequest CurrentSalesDiscount => _salesDiscount;

    public bool TryApplySalesDiscount(
        SalesDiscountType type, long value, string reason, out string? error)
    {
        error = null;
        if (!CanApplySalesDiscount)
        {
            error = "Bạn không có quyền áp dụng giảm giá hoặc đơn đang bị khóa.";
            return false;
        }
        try
        {
            _ = SalesDiscountCalculator.Resolve(
                checked((long)EstimatedSubtotal), type, value, reason);
            _salesDiscount = new SalesDiscountRequest(type, value, reason);
            NotifyCartPresentation();
            return true;
        }
        catch (DomainException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public void ClearSalesDiscount()
    {
        if (IsCheckingOut || HasPendingVietQrAuthorization)
            return;
        _salesDiscount = SalesDiscountRequest.None;
        NotifyCartPresentation();
    }

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

    public int? ActiveHeldSaleId
    {
        get => _activeHeldSaleId;
        private set
        {
            if (SetProperty(ref _activeHeldSaleId, value))
                NotifyCommandStates();
        }
    }

    public int ActiveHeldSaleCount
    {
        get => _activeHeldSaleCount;
        private set
        {
            if (SetProperty(ref _activeHeldSaleCount, value))
            {
                OnPropertyChanged(nameof(ActiveHeldSaleCountText));
                OnPropertyChanged(nameof(ActiveHeldSaleButtonText));
                OnPropertyChanged(nameof(ActiveHeldSaleAutomationName));
                OnPropertyChanged(nameof(HasActiveHeldSales));
            }
        }
    }

    public string ActiveHeldSaleCountText =>
        ActiveHeldSaleCount.ToString("N0", VietnameseCulture);

    public string ActiveHeldSaleButtonText =>
        $"Đơn đang giữ ({ActiveHeldSaleCountText})";

    public bool HasActiveHeldSales => ActiveHeldSaleCount > 0;

    public string ActiveHeldSaleAutomationName =>
        $"Đơn đang giữ, {ActiveHeldSaleCountText} đơn";

    public event EventHandler? ScanFocusRequested;

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
            cancellationToken: default);

        await LoadCheckoutRecoveryAsync();
        await LoadPaymentIntentRecoveryAsync();
        await RefreshHeldSaleCountAsync();
    }

    private async Task LoadPaymentIntentRecoveryAsync()
    {
        _paymentIntentRecoveryLoadSource?.Cancel();
        _paymentIntentRecoveryLoadSource?.Dispose();
        var source = new CancellationTokenSource();
        _paymentIntentRecoveryLoadSource = source;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service =
                scope.ServiceProvider.GetRequiredService<IPaymentIntentService>();
            var result = await service.RecoverPendingAsync(
                limit: 25,
                source.Token);

            if (_isDisposed ||
                source.IsCancellationRequested ||
                !ReferenceEquals(
                    _paymentIntentRecoveryLoadSource,
                    source))
            {
                return;
            }

            if (result.IsFailure)
            {
                global::POS.Application.Common.PosLog.Warning(_logger,
                    "Không thể tải PaymentIntent recovery: {Code} - {Message}",
                    result.AppError.Code,
                    result.AppError.Message);
                return;
            }

            PendingPaymentIntents.Clear();
            foreach (var intent in result.Value)
            {
                _confirmedCheckoutRecoveries.TryGetValue(
                    intent.Id,
                    out var checkoutRecovery);
                PendingPaymentIntents.Add(
                    new PaymentIntentRecoveryItemViewModel(
                        intent,
                        checkoutRecovery));
            }
            SelectedPaymentIntentRecovery = PendingPaymentIntents.FirstOrDefault();
            OnPropertyChanged(nameof(HasPendingPaymentIntentRecovery));
            OnPropertyChanged(nameof(PendingPaymentIntentButtonText));
            OnPropertyChanged(nameof(ManualReviewPaymentIntentCount));
            OnPropertyChanged(nameof(HasManualReviewPaymentIntentWarning));
            OnPropertyChanged(nameof(ManualReviewPaymentIntentWarningText));
            IsPaymentIntentRecoveryOpen = HasPendingPaymentIntentRecovery;
        }
        catch (OperationCanceledException)
            when (source.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger,
                exception,
                "Không thể tải PaymentIntent recovery.");
        }
        finally
        {
            if (ReferenceEquals(
                    _paymentIntentRecoveryLoadSource,
                    source))
            {
                _paymentIntentRecoveryLoadSource = null;
                source.Dispose();
            }
        }
    }

    private async Task ReloadRecoveryStateAsync(bool openHighestPriority)
    {
        var selectedPaymentIntentId = SelectedPaymentIntentRecovery?.Id;
        var selectedCheckoutId = SelectedRecovery?.ClientRequestId;
        var wasPaymentOpen = IsPaymentIntentRecoveryOpen;

        await LoadCheckoutRecoveryAsync();
        await LoadPaymentIntentRecoveryAsync();

        SelectedPaymentIntentRecovery = selectedPaymentIntentId.HasValue
            ? PendingPaymentIntents.FirstOrDefault(x => x.Id == selectedPaymentIntentId.Value)
                ?? PendingPaymentIntents.FirstOrDefault()
            : PendingPaymentIntents.FirstOrDefault();
        SelectedRecovery = selectedCheckoutId.HasValue
            ? CheckoutRecoveries.FirstOrDefault(x => x.ClientRequestId == selectedCheckoutId.Value)
                ?? CheckoutRecoveries.FirstOrDefault()
            : CheckoutRecoveries.FirstOrDefault();

        IsPaymentIntentRecoveryOpen = openHighestPriority
            ? HasPendingPaymentIntentRecovery
            : wasPaymentOpen && HasPendingPaymentIntentRecovery;
    }

    private bool CanRetryPaymentIntentRecovery(object? parameter) =>
        parameter is int paymentIntentId &&
        paymentIntentId > 0 &&
        SelectedPaymentIntentRecovery is { CanRetryCheckout: true } pending &&
        pending.Id == paymentIntentId &&
        !IsProcessingRecovery;

    private async Task RetryPaymentIntentRecoveryAsync(object? parameter)
    {
        if (parameter is not int paymentIntentId ||
            SelectedPaymentIntentRecovery is not { CanRetryCheckout: true } pending ||
            pending.Id != paymentIntentId)
        {
            return;
        }

        IsProcessingRecovery = true;
        PaymentIntentRecoveryError = null;
        ShowNeutral("Đang hoàn tất đơn...");
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var checkout = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
            var result = await checkout.RetryConfirmedPaymentIntentAsync(paymentIntentId);
            if (result.IsFailure)
            {
                global::POS.Application.Common.PosLog.Warning(
                    _logger,
                    "Không thể retry PaymentIntent {PaymentIntentId}: {Code} - {Message}",
                    paymentIntentId,
                    result.AppError.Code,
                    result.AppError.Message);
                PaymentIntentRecoveryError = result.AppError.Message;
                ShowError(result.AppError.Message);
                return;
            }
            await ReloadRecoveryStateAsync(openHighestPriority: false);
            PaymentIntentRecoveryError = null;
            ShowSuccess($"Đã lưu đơn {result.Value.OrderCode} từ giao dịch VietQR đã xác nhận.");
            if (result.Value.ReceiptSnapshot is not null)
                await ShowReceiptPreviewAsync(result.Value.ReceiptSnapshot);
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(
                _logger,
                exception,
                "Retry PaymentIntent {PaymentIntentId} thất bại.",
                paymentIntentId);
            PaymentIntentRecoveryError =
                "Không thể hoàn tất đơn VietQR lúc này. Giao dịch đã nhận tiền vẫn được giữ nguyên; " +
                "vui lòng thử lại hoặc đóng để xử lý sau.";
            ShowError(PaymentIntentRecoveryError);
        }
        finally
        {
            IsProcessingRecovery = false;
        }
    }

    private async Task ShowPaymentIntentQrAsync()
    {
        var pending = SelectedPaymentIntentRecovery;
        if (pending?.CanShowQr != true)
            return;

        IsProcessingRecovery = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentService>();
            var gateway = scope.ServiceProvider.GetRequiredService<IVietQrPaymentGateway>();
            var dialog = scope.ServiceProvider.GetRequiredService<IVietQrPaymentDialogService>();
            var latest = await intents.GetByIdAsync(pending.Id);
            if (latest.IsFailure)
            {
                ShowError(latest.AppError.Message);
                return;
            }

            if (latest.Value.Status is not (PaymentIntentStatus.Created or PaymentIntentStatus.Presented))
            {
                ShowError("Trạng thái giao dịch VietQR đã thay đổi. Vui lòng tải lại danh sách.");
                await LoadPaymentIntentRecoveryAsync();
                return;
            }

            var png = gateway.RenderPng(latest.Value.PayloadText);
            if (png.IsFailure)
            {
                ShowError("Không thể hiển thị mã VietQR đã lưu. Vui lòng thử lại.");
                return;
            }

            var shown = await dialog.ShowPresentationAsync(
                new VietQrPaymentPresentation(
                    latest.Value.Amount,
                    latest.Value.DisplayCode,
                    latest.Value.TransferContent,
                    png.Value)
                {
                    BankName = latest.Value.BankCode,
                    AccountName = latest.Value.AccountName,
                    RecipientInformationMessage =
                        $"Tài khoản {latest.Value.AccountNumber}"
                });
            if (shown.IsFailure)
            {
                ShowError("Không thể mở màn hình VietQR. Vui lòng thử lại.");
                return;
            }

            if (latest.Value.Status == PaymentIntentStatus.Created)
            {
                var presented = await intents.MarkPresentedAsync(pending.Id);
                if (presented.IsFailure)
                {
                    ShowError(presented.AppError.Message);
                    return;
                }
            }

            await LoadPaymentIntentRecoveryAsync();
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger, exception, "Không thể hiển thị PaymentIntent {PaymentIntentId}.", pending.Id);
            ShowError("Không thể mở màn hình VietQR. Vui lòng thử lại.");
        }
        finally
        {
            IsProcessingRecovery = false;
        }
    }

    private async Task ConfirmPaymentIntentRecoveryAsync()
    {
        var pending = SelectedPaymentIntentRecovery;
        if (pending?.CanConfirm != true ||
            !_recoveryConfirmation.ConfirmPaymentReceived(pending.Recovery.Amount))
            return;

        IsProcessingRecovery = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentService>();
            var latest = await intents.GetByIdAsync(pending.Id);
            if (latest.IsFailure || latest.Value.Status != PaymentIntentStatus.Presented)
            {
                ShowError(latest.IsFailure
                    ? latest.AppError.Message
                    : "Trạng thái giao dịch VietQR đã thay đổi. Vui lòng tải lại danh sách.");
                return;
            }

            var confirmed = await intents.ConfirmReceivedAsync(pending.Id);
            if (confirmed.IsFailure)
            {
                ShowError(confirmed.AppError.Message);
                return;
            }

            var checkout = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
            var result = await checkout.RetryConfirmedPaymentIntentAsync(pending.Id);
            if (result.IsFailure)
            {
                ShowError(result.AppError.Message);
                await LoadPaymentIntentRecoveryAsync();
                return;
            }

            await ReloadRecoveryStateAsync(openHighestPriority: false);
            ShowSuccess($"Đã lưu đơn {result.Value.OrderCode} từ giao dịch VietQR đã xác nhận.");
            if (result.Value.ReceiptSnapshot is not null)
                await ShowReceiptPreviewAsync(result.Value.ReceiptSnapshot);
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger, exception, "Không thể xác nhận PaymentIntent {PaymentIntentId}.", pending.Id);
            ShowError("Không thể hoàn tất giao dịch VietQR. Trạng thái đã lưu được giữ nguyên.");
        }
        finally
        {
            IsProcessingRecovery = false;
        }
    }

    private async Task CancelPaymentIntentRecoveryAsync()
    {
        var pending = SelectedPaymentIntentRecovery;
        if (pending?.CanCancel != true ||
            !_recoveryConfirmation.ConfirmCancelPaymentIntent(pending.DisplayCode))
            return;

        IsProcessingRecovery = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var intents = scope.ServiceProvider.GetRequiredService<IPaymentIntentService>();
            var latest = await intents.GetByIdAsync(pending.Id);
            if (latest.IsFailure ||
                latest.Value.Status is not (PaymentIntentStatus.Created or PaymentIntentStatus.Presented))
            {
                ShowError(latest.IsFailure
                    ? latest.AppError.Message
                    : "Giao dịch đã được xác nhận và không thể hủy.");
                await LoadPaymentIntentRecoveryAsync();
                return;
            }

            var cancelled = await intents.CancelAsync(pending.Id);
            if (cancelled.IsFailure)
            {
                ShowError(cancelled.AppError.Message);
                return;
            }

            await ReloadRecoveryStateAsync(openHighestPriority: false);
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger, exception, "Không thể hủy PaymentIntent {PaymentIntentId}.", pending.Id);
            ShowError("Không thể hủy mã VietQR. Vui lòng thử lại.");
        }
        finally
        {
            IsProcessingRecovery = false;
        }
    }

    private async Task LoadCheckoutRecoveryAsync()
    {
        _recoveryLoadSource?.Cancel();
        _recoveryLoadSource?.Dispose();
        var source = new CancellationTokenSource();
        _recoveryLoadSource = source;
        IsLoadingRecovery = true;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
            var result = await service.GetCheckoutRecoveryAsync(
                limit: 25,
                source.Token);

            if (_isDisposed ||
                source.IsCancellationRequested ||
                !ReferenceEquals(_recoveryLoadSource, source))
            {
                return;
            }

            if (result.IsFailure)
            {
                global::POS.Application.Common.PosLog.Warning(_logger,
                    "Không thể tải checkout recovery: {Code} - {Message}",
                    result.AppError.Code,
                    result.AppError.Message);
                ShowError(
                    "Không thể kiểm tra giao dịch dang dở. Bạn vẫn có thể tiếp tục bán hàng.");
                return;
            }

            CheckoutRecoveries.Clear();
            _confirmedCheckoutRecoveries.Clear();
            foreach (var recovery in result.Value)
            {
                if (recovery.HasConfirmedPayment &&
                    recovery.PaymentIntentId is int paymentIntentId)
                {
                    _confirmedCheckoutRecoveries[paymentIntentId] = recovery;
                }

                if (_checkoutClientRequestId ==
                        recovery.ClientRequestId ||
                    recovery.HasConfirmedPayment)
                    continue;

                CheckoutRecoveries.Add(new CheckoutRecoveryItemViewModel(recovery));
            }

            SelectedRecovery = CheckoutRecoveries.FirstOrDefault();
            OnPropertyChanged(nameof(HasCheckoutRecovery));
            NotifyPaymentPresentation();
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger, exception, "Không thể tải checkout recovery.");
            if (!_isDisposed)
            {
                ShowError(
                    "Không thể kiểm tra giao dịch dang dở. Bạn vẫn có thể tiếp tục bán hàng.");
            }
        }
        finally
        {
            if (ReferenceEquals(_recoveryLoadSource, source))
            {
                _recoveryLoadSource = null;
                IsLoadingRecovery = false;
                source.Dispose();
            }
        }
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
                global::POS.Application.Common.PosLog.Warning(_logger,
                    "Không thể tải danh mục bán hàng: " +
                    "{Code} - {Message}",
                    result.AppError.Code,
                    result.AppError.Message);

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
            global::POS.Application.Common.PosLog.Error(_logger,
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
            cancellationToken: default);
    }

    private Task<bool> SearchAsync()
    {
        return LoadProductsAsync();
    }

    public async Task<bool> ProcessScanOrSearchAsync(
        string? input,
        CancellationToken cancellationToken = default)
    {
        var normalized = input?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        await _scanGate.WaitAsync(cancellationToken);
        try
        {
            if (!CanMutateCart(showStatus: true))
            {
                return false;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var productService =
                scope.ServiceProvider.GetRequiredService<IProductService>();
            var exact =
                await productService.FindSalesExactAsync(
                    normalized,
                    cancellationToken);

            if (exact.IsSuccess)
            {
                var product =
                    new SalesProductCardViewModel(
                        exact.Value,
                        AddProductAsync);

                if (product.IsArchived)
                {
                    ShowError("Sản phẩm đã ngừng bán.");
                    return false;
                }

                if (!product.IsActive)
                {
                    ShowError("Sản phẩm đã ngừng bán.");
                    return false;
                }

                var before = CartItemCount;
                await AddProductAsync(product);
                if (CartItemCount > before)
                {
                    ScanCode = string.Empty;
                    return true;
                }

                return false;
            }

            ShowError($"Không tìm thấy mã sản phẩm “{normalized}”.");
            return false;
        }
        finally
        {
            _scanGate.Release();
        }
    }

    private Task<bool> RefreshAsync()
    {
        return LoadProductsAsync();
    }

    private async Task<bool> LoadProductsAsync(
        CancellationToken cancellationToken = default)
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

                    isArchived:
                        false,

                    isLowStock:
                        null,

                    pageNumber:
                        1,

                    pageSize:
                        CatalogPageSize);

            var result =
                await productService
                    .SearchAsync(
                        request,
                        cancellationToken);

            if (result.IsFailure)
            {
                ShowError(
                    result.AppError.Message);

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

            return true;
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger,
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


    private Task AddProductAsync(
        SalesProductCardViewModel product)
    {
        ArgumentNullException.ThrowIfNull(
            product);

        if (!CanMutateCart(showStatus: true))
        {
            return Task.CompletedTask;
        }

        if (product.IsArchived || !product.IsActive)
        {
            ShowError("Sản phẩm đã ngừng bán.");
            return Task.CompletedTask;
        }

        if (!product.CanSell)
        {
            ShowError(
                product.TrackInventory &&
                !product.AllowNegativeStock
                    ? "Số lượng trong giỏ đã đạt tồn kho hiện có."
                    : $"'{product.Name}' hiện không thể bán.");

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
                    existingLine.TrackInventory &&
                    !existingLine.AllowNegativeStock
                        ? "Số lượng trong giỏ đã đạt tồn kho hiện có."
                        : $"'{product.Name}' đã đạt số lượng tối đa có thể bán.");
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
                RemoveCartLine,
                () => CanMutateCart(showStatus: false),
                () => CanMutateCart(showStatus: true));

        CartLines.Add(
            line);
        SelectedCartLine = line;

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

        if (!CanMutateCart(showStatus: true))
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

        if (!CanMutateCart(showStatus: true))
        {
            ShowPendingVietQrLockError();

            return;
        }

        var removedIndex = CartLines.IndexOf(line);
        if (!CartLines.Remove(
                line))
        {
            return;
        }

        SelectedCartLine = CartLines.Count == 0
            ? null
            : CartLines[Math.Min(removedIndex, CartLines.Count - 1)];

        NotifyCartPresentation();

        ShowNeutral(
            $"Đã xóa '{line.ProductName}' khỏi đơn.");
    }

    private async Task HoldSaleAsync()
    {
        if (!CanHoldSale())
            return;

        _holdClientRequestId ??= Guid.NewGuid();
        var dialog = _heldSaleDialogs.ShowHold(
            CartLineCount,
            CartItemCount,
            checked((long)EstimatedSubtotal),
            ResolvedDiscountAmount,
            checked((long)EstimatedTotal),
            _salesDiscount.Type,
            _salesDiscount.Value,
            _holdClientRequestId.Value);
        if (dialog is null)
            return;

        _isHeldSaleBusy = true;
        NotifyCommandStates();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IHeldSaleService>();
            var request = new CreateHeldSaleRequest(
                dialog.ClientRequestId,
                dialog.Label,
                dialog.Notes,
                CartLines.Select(line =>
                    new CreateHeldSaleLineRequest(line.ProductId, line.Quantity)).ToArray(),
                _salesDiscount);
            var result = await service.CreateHeldSaleAsync(request);
            if (result.IsFailure)
            {
                ShowError(result.AppError.Message);
                return;
            }

            CartLines.Clear();
            SelectedCartLine = null;
            CashReceivedText = string.Empty;
            OrderNotes = string.Empty;
            ActiveHeldSaleId = null;
            _salesDiscount = SalesDiscountRequest.None;
            _checkoutClientRequestId = null;
            _holdClientRequestId = null;
            _orderSessionVersion++;
            ResetPaymentState(resetSelectedMethod: true);
            NotifyCartPresentation();
            await RefreshHeldSaleCountAsync();
            ShowSuccess($"Đã giữ đơn {result.Value.DisplayCode}.");
            ScanFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isHeldSaleBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task OpenHeldSalesAsync()
    {
        if (_isHeldSaleBusy || IsBusy)
            return;
        _isHeldSaleBusy = true;
        NotifyCommandStates();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IHeldSaleService>();
            var list = await service.GetActiveHeldSalesAsync();
            if (list.IsFailure)
            {
                ShowError(list.AppError.Message);
                return;
            }
            ActiveHeldSaleCount = list.Value.Count;
            OnPropertyChanged(nameof(ActiveHeldSaleCountText));
            var action = _heldSaleDialogs.ShowActiveList(list.Value);
            if (action is null)
                return;
            if (action.Action == HeldSaleListAction.Cancel)
            {
                if (!_heldSaleDialogs.ConfirmCancel())
                    return;
                var cancelled = await service.CancelHeldSaleAsync(action.HeldSaleId);
                if (cancelled.IsFailure)
                {
                    ShowError(cancelled.AppError.Message);
                    return;
                }
                await RefreshHeldSaleCountAsync();
                ShowSuccess("Đã hủy đơn giữ.");
                return;
            }

            if (HasCartItems)
            {
                ShowError("Đơn hiện tại đang có sản phẩm. Hãy giữ hoặc làm trống đơn hiện tại trước khi mở đơn khác.");
                return;
            }

            var resume = await service.GetHeldSaleForResumeAsync(action.HeldSaleId);
            if (resume.IsFailure)
            {
                ShowError(resume.AppError.Message);
                return;
            }
            var selection = _heldSaleDialogs.ShowResumeReview(resume.Value);
            if (selection is null)
                return;
            ApplyResumedSale(resume.Value, selection);
        }
        finally
        {
            _isHeldSaleBusy = false;
            NotifyCommandStates();
        }
    }

    private void ApplyResumedSale(
        HeldSaleResumeDto heldSale,
        HeldSaleResumeDialogResult selection)
    {
        var selected = selection.Lines.Where(value => value.Include).ToDictionary(value => value.ProductId);
        foreach (var live in heldSale.Lines.Where(value => selected.ContainsKey(value.ProductId)))
        {
            var choice = selected[live.ProductId];
            if (live.CurrentUnitPrice is null ||
                string.IsNullOrWhiteSpace(live.CurrentProductCode) ||
                string.IsNullOrWhiteSpace(live.CurrentProductName) ||
                string.IsNullOrWhiteSpace(live.CurrentUnitName) ||
                choice.Quantity <= 0 ||
                live.CurrentUnitPrice != live.UnitPriceSnapshot && !choice.CurrentPriceAccepted)
                throw new InvalidOperationException("Kết quả review đơn giữ không hợp lệ.");

            CartLines.Add(new SalesCartLineViewModel(
                live.ProductId,
                live.CurrentProductCode,
                live.CurrentProductName,
                live.CurrentUnitName,
                live.CurrentUnitPrice.Value,
                live.CurrentStock ?? 0,
                live.TrackInventory,
                live.AllowNegativeStock,
                choice.Quantity,
                OnCartLineChanged,
                RemoveCartLine,
                () => CanMutateCart(showStatus: false),
                () => CanMutateCart(showStatus: true)));
        }
        if (CartLines.Count == 0)
            return;
        BeginNewOrderSession();
        ActiveHeldSaleId = heldSale.Id;
        _salesDiscount = new SalesDiscountRequest(
            heldSale.DiscountType, heldSale.RequestedDiscountValue, heldSale.DiscountReason);
        OrderNotes = heldSale.Notes ?? string.Empty;
        SelectedCartLine = CartLines[0];
        NotifyCartPresentation();
        ShowSuccess($"Đã mở lại {heldSale.DisplayCode} bằng giá và tồn kho hiện tại.");
        ScanFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task RefreshHeldSaleCountAsync()
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IHeldSaleService>();
            var result = await service.GetActiveHeldSalesAsync();
            if (result.IsSuccess)
            {
                ActiveHeldSaleCount = result.Value.Count;
                OnPropertyChanged(nameof(ActiveHeldSaleCountText));
            }
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Warning(_logger, exception, "Không thể cập nhật số đơn đang giữ.");
        }
    }

    private bool CanHoldSale() =>
        !_isHeldSaleBusy &&
        !IsBusy &&
        HasCartItems &&
        ActiveHeldSaleId is null &&
        !HasCheckoutRecovery &&
        !IsRecoveryBusy &&
        !HasPendingVietQrAuthorization &&
        _checkoutClientRequestId is null;

    private Task ClearCartAsync()
    {
        if (!CanMutateCart(showStatus: true))
        {
            ShowPendingVietQrLockError();

            return Task.CompletedTask;
        }

        if (!_recoveryConfirmation.ConfirmClearCart())
        {
            return Task.CompletedTask;
        }

        _orderSessionVersion++;

        CancelLastOrderAutoDismiss();
        ClearLastOrderPresentation();

        CartLines.Clear();
        _salesDiscount = SalesDiscountRequest.None;
        SelectedCartLine = null;
        ActiveHeldSaleId = null;
        _holdClientRequestId = null;

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

    public void RemoveSelectedCartLine()
    {
        if (SelectedCartLine is not null)
        {
            RemoveCartLine(SelectedCartLine);
        }
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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);

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

        ArgumentOutOfRangeException.ThrowIfNegative(amount);

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
                IsVietQrPaymentSelected
                    ? await AuthorizePaymentIntentAsync(
                        requestLines,
                        totalAmount)
                    : await _paymentFlowService
                        .AuthorizeAsync(
                            new SalesPaymentAuthorizationRequest(
                                paymentMethod:
                                    SelectedPaymentMethod,

                                totalAmount:
                                    totalAmount,

                                cashReceived:
                                    cashReceived,

                                existingAuthorization:
                                    null));

            if (authorizationResult.IsFailure)
            {
                ShowError(
                    authorizationResult
                        .AppError
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
                            .ConfirmedPaymentAmount,

                    clientRequestId:
                        _checkoutClientRequestId ??=
                            Guid.NewGuid(),

                    heldSaleId:
                        ActiveHeldSaleId,

                    salesDiscount:
                        _salesDiscount,

                    paymentIntentId:
                        authorization.IsVietQr
                            ? _pendingPaymentIntentId
                            : null);

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
                authorization.IsVietQr && _pendingPaymentIntentId is int paymentIntentId
                    ? await checkoutService.RetryConfirmedPaymentIntentAsync(paymentIntentId)
                    : await checkoutService.CheckoutAsync(request);

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
                    cancellationToken: default);

                await LoadCheckoutRecoveryAsync();

                ShowCheckoutFailure(
                    result.AppError,
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
                global::POS.Application.Common.PosLog.Error(_logger,
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
            _salesDiscount = SalesDiscountRequest.None;
            SelectedCartLine = null;
            ActiveHeldSaleId = null;

            CashReceivedText =
                string.Empty;

            OrderNotes =
                string.Empty;

            NotifyCartPresentation();

            await LoadProductsAsync(
                cancellationToken: default);

            successMessage =
                $"Thanh toán {FormatPaymentMethod(
                    completedOrder.PaymentMethod)} thành công • " +
                $"{completedOrder.OrderCode} • " +
                $"{completedOrder.TotalAmount.ToString(
                    "N0",
                    VietnameseCulture)} ₫";

            ShowSuccess(
                successMessage);

            var acknowledgment =
                await checkoutService
                    .AcknowledgeCheckoutAsync(
                        completedOrder.CheckoutClientRequestId ??
                        request.ClientRequestId);

            if (acknowledgment.IsFailure)
            {
                global::POS.Application.Common.PosLog.Warning(_logger,
                    "Checkout {OrderCode} đã hoàn tất nhưng acknowledgment thất bại: {ErrorCode}",
                    completedOrder.OrderCode,
                    acknowledgment.AppError.Code);
            }

            _checkoutClientRequestId =
                null;

            await RefreshHeldSaleCountAsync();
            await ReloadRecoveryStateAsync(openHighestPriority: false);
        }
        catch (OperationCanceledException)
        {
            ShowNeutral(
                "Đã dừng thao tác thanh toán. Đơn hàng chưa được lưu.");
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger,
                exception,
                "Thanh toán từ giao diện bán hàng thất bại.");

            if (IsPaymentIntentSchemaCompatibilityFailure(
                    exception))
            {
                ShowError(
                    "Cơ sở dữ liệu chưa được nâng cấp đầy đủ cho VietQR. " +
                    "Vui lòng đóng ứng dụng và mở lại để hoàn tất nâng cấp.");
            }
            else if (authorization?.IsVietQr ==
                true)
            {
                ShowCheckoutFailure(
                    "Không thể hoàn tất thanh toán VietQR. " +
                    "Yêu cầu đã xác nhận vẫn được giữ để thử lại.",
                    authorization);
            }
            else
            {
                ShowError(
                    "Không thể hoàn tất thanh toán. " +
                    "Vui lòng kiểm tra và thử lại.");
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

    private async Task RetryRecoveryAsync()
    {
        var recovery = SelectedRecovery?.Recovery;
        if (recovery?.PreparedRequest is null || !recovery.CanRetry)
        {
            return;
        }

        IsProcessingRecovery = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
            var result = await service.CheckoutAsync(recovery.PreparedRequest);
            if (result.IsFailure)
            {
                ShowError(
                    result.AppError.Code == "CHECKOUT.PREPARATION_STALE"
                        ? "Giá hoặc dữ liệu bán hàng đã thay đổi. Hãy bỏ giao dịch dang dở, kiểm tra lại và tạo đơn mới."
                        : result.AppError.Message);
                return;
            }

            var completed = result.Value;
            LastOrderCode = completed.OrderCode;
            LastOrderSummary =
                $"{FormatPaymentMethod(completed.PaymentMethod)} • " +
                $"{completed.TotalAmount.ToString("N0", VietnameseCulture)} ₫";
            ShowSuccess(
                $"Giao dịch {completed.OrderCode} đã hoàn tất và được phục hồi an toàn.");

            if (completed.ReceiptSnapshot is not null)
            {
                await ShowReceiptPreviewAsync(completed.ReceiptSnapshot);
            }

            var acknowledgment =
                await service.AcknowledgeCheckoutAsync(recovery.ClientRequestId);
            if (acknowledgment.IsFailure)
            {
                global::POS.Application.Common.PosLog.Warning(_logger,
                    "Checkout recovery {ClientRequestId} đã hoàn tất nhưng acknowledgment thất bại: {Code}",
                    recovery.ClientRequestId,
                    acknowledgment.AppError.Code);
            }

            RemoveSelectedRecovery();
            await LoadProductsAsync();
        }
        finally
        {
            IsProcessingRecovery = false;
        }
    }

    private async Task AbandonRecoveryAsync()
    {
        var recovery = SelectedRecovery?.Recovery;
        if (recovery is null ||
            !recovery.CanAbandon ||
            !_recoveryConfirmation.ConfirmAbandon())
        {
            return;
        }

        IsProcessingRecovery = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
            var result = await service.AbandonCheckoutAsync(recovery.ClientRequestId);
            if (result.IsFailure)
            {
                ShowError(result.AppError.Message);
                return;
            }

            if (_checkoutClientRequestId == recovery.ClientRequestId)
            {
                _checkoutClientRequestId = null;
            }

            RemoveSelectedRecovery();
            ShowNeutral("Đã bỏ giao dịch dang dở. Có thể bắt đầu một đơn mới.");
        }
        finally
        {
            IsProcessingRecovery = false;
        }
    }

    private async Task AcknowledgeRecoveryAsync()
    {
        var recovery = SelectedRecovery?.Recovery;
        if (recovery is null || recovery.Status != CheckoutRequestStatus.Completed)
        {
            return;
        }

        IsProcessingRecovery = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
            var result = await service.AcknowledgeCheckoutAsync(recovery.ClientRequestId);
            if (result.IsFailure)
            {
                ShowError(result.AppError.Message);
                return;
            }

            RemoveSelectedRecovery();
            ShowNeutral("Đã xác nhận giao dịch hoàn tất.");
        }
        finally
        {
            IsProcessingRecovery = false;
        }
    }

    private async Task OpenRecoveryReceiptAsync()
    {
        if (SelectedRecovery?.Recovery.OrderId is not int orderId)
        {
            return;
        }

        IsProcessingRecovery = true;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var history = scope.ServiceProvider.GetRequiredService<IOrderHistoryService>();
            var receipt = await history.GetReprintReceiptAsync(orderId);
            if (receipt.IsFailure)
            {
                ShowError(receipt.AppError.Message);
                return;
            }

            await ShowReceiptPreviewAsync(receipt.Value);
        }
        finally
        {
            IsProcessingRecovery = false;
        }
    }

    private void RemoveSelectedRecovery()
    {
        var selected = SelectedRecovery;
        if (selected is not null)
        {
            CheckoutRecoveries.Remove(selected);
        }

        SelectedRecovery = CheckoutRecoveries.FirstOrDefault();
        OnPropertyChanged(nameof(HasCheckoutRecovery));
        NotifyPaymentPresentation();
        NotifyRecoveryPresentation();
    }

    private void NotifyRecoveryPresentation()
    {
        OnPropertyChanged(nameof(SelectedRecovery));
        OnPropertyChanged(nameof(HasCheckoutRecovery));
        NotifyRecoveryCommands();
    }

    private void NotifyRecoveryCommands()
    {
        RetryRecoveryCommand.NotifyCanExecuteChanged();
        AbandonRecoveryCommand.NotifyCanExecuteChanged();
        AcknowledgeRecoveryCommand.NotifyCanExecuteChanged();
        OpenRecoveryReceiptCommand.NotifyCanExecuteChanged();
        RetryPaymentIntentRecoveryCommand.NotifyCanExecuteChanged();
        ShowPaymentIntentQrCommand.NotifyCanExecuteChanged();
        ConfirmPaymentIntentRecoveryCommand.NotifyCanExecuteChanged();
        CancelPaymentIntentRecoveryCommand.NotifyCanExecuteChanged();
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
        AppError error,
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
            global::POS.Application.Common.PosLog.Error(_logger,
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
        _pendingPaymentIntentId = null;
        _paymentIntentClientRequestId = null;

        if (resetSelectedMethod)
        {
            _selectedPaymentMethod =
                PaymentMethod.Cash;
        }

        NotifyPaymentPresentation();
        NotifyCashPresentation();
        NotifyCommandStates();
    }

    private async Task<Result<SalesPaymentAuthorizationOutcome>>
        AuthorizePaymentIntentAsync(
            IReadOnlyList<CheckoutLineRequest> requestLines,
            long totalAmount)
    {
        if (_pendingVietQrAuthorization is not null &&
            _pendingPaymentIntentId.HasValue)
            return Result.Success(
                SalesPaymentAuthorizationOutcome.Authorized(
                    _pendingVietQrAuthorization));

        _paymentIntentClientRequestId ??= Guid.NewGuid();

        var intentCheckout = new CheckoutRequest(
            lines: requestLines,
            paymentMethod: PaymentMethod.VietQr,
            cashReceived: 0,
            notes: OrderNotes,
            confirmedPaymentAmount: 1,
            clientRequestId: _checkoutClientRequestId ??= Guid.NewGuid(),
            heldSaleId: ActiveHeldSaleId,
            salesDiscount: _salesDiscount);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var intentService =
            scope.ServiceProvider.GetRequiredService<IPaymentIntentService>();
        var gateway =
            scope.ServiceProvider.GetRequiredService<IVietQrPaymentGateway>();
        var dialog =
            scope.ServiceProvider.GetRequiredService<IVietQrPaymentDialogService>();

        var created = await intentService.CreateAsync(
            new CreatePaymentIntentRequest(
                _paymentIntentClientRequestId.Value,
                intentCheckout));
        if (created.IsFailure)
            return Result.Failure<SalesPaymentAuthorizationOutcome>(
                created.AppError);

        _pendingPaymentIntentId = created.Value.Id;

        var png = gateway.RenderPng(created.Value.PayloadText);
        if (png.IsFailure)
            return Result.Failure<SalesPaymentAuthorizationOutcome>(
                png.AppError);

        var dialogResult = await dialog.ShowPresentationAsync(
            new VietQrPaymentPresentation(
                created.Value.Amount,
                created.Value.DisplayCode,
                created.Value.TransferContent,
                png.Value)
            {
                BankName = created.Value.BankCode,
                AccountName = created.Value.AccountName,
                RecipientInformationMessage =
                    $"Tài khoản {created.Value.AccountNumber}"
            });

        if (dialogResult.IsFailure)
            return Result.Failure<SalesPaymentAuthorizationOutcome>(
                dialogResult.AppError);

        var presented = await intentService.MarkPresentedAsync(
            created.Value.Id);
        if (presented.IsFailure)
            return Result.Failure<SalesPaymentAuthorizationOutcome>(
                presented.AppError);

        if (!dialogResult.Value.Confirmed)
        {
            var cancelled = await intentService.CancelAsync(
                created.Value.Id);
            if (cancelled.IsFailure)
                return Result.Failure<SalesPaymentAuthorizationOutcome>(
                    cancelled.AppError);

            _pendingPaymentIntentId = null;
            _paymentIntentClientRequestId = null;
            return Result.Success(
                SalesPaymentAuthorizationOutcome.Cancelled());
        }

        var confirmed = await intentService.ConfirmReceivedAsync(
            created.Value.Id);
        if (confirmed.IsFailure)
            return Result.Failure<SalesPaymentAuthorizationOutcome>(
                confirmed.AppError);

        return Result.Success(
            SalesPaymentAuthorizationOutcome.Authorized(
                new SalesPaymentAuthorization(
                    PaymentMethod.VietQr,
                    cashReceived: 0,
                    confirmedPaymentAmount: totalAmount,
                    paymentReference: confirmed.Value.DisplayCode,
                    transferContent: confirmed.Value.TransferContent)));
    }

    private static bool IsPaymentIntentSchemaCompatibilityFailure(
        Exception exception)
    {
        var message =
            exception
                .GetBaseException()
                .Message;

        return message.Contains(
                   "no such column",
                   StringComparison.OrdinalIgnoreCase) &&
               message.Contains(
                   "PaymentIntent",
                   StringComparison.OrdinalIgnoreCase);
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

    private bool CanMutateCart(bool showStatus)
    {
        if (IsCheckingOut)
        {
            if (showStatus)
            {
                ShowError("Đang xử lý thanh toán, không thể sửa giỏ.");
            }

            return false;
        }

        if (HasCheckoutRecovery || IsRecoveryBusy)
        {
            if (showStatus)
            {
                ShowError(
                    "Giao dịch đang chờ khôi phục, hãy xử lý trước khi sửa giỏ.");
            }

            return false;
        }

        if (HasPendingVietQrAuthorization)
        {
            if (showStatus)
            {
                ShowPendingVietQrLockError();
            }

            return false;
        }

        return !IsLoadingProducts;
    }

    private bool CanClearCart()
    {
        return
            !IsBusy &&
            CanMutateCart(showStatus: false) &&
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
            !IsSalesDiscountValid ||
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
                _pendingVietQrAuthorization is not null;
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
        OnPropertyChanged(nameof(EstimatedSubtotal));
        OnPropertyChanged(nameof(EstimatedSubtotalText));
        OnPropertyChanged(nameof(ResolvedDiscountAmount));
        OnPropertyChanged(nameof(ResolvedDiscountAmountText));
        OnPropertyChanged(nameof(HasSalesDiscount));
        OnPropertyChanged(nameof(IsSalesDiscountValid));
        OnPropertyChanged(nameof(SalesDiscountValidationText));
        OnPropertyChanged(nameof(CanApplySalesDiscount));
        OnPropertyChanged(nameof(SalesDiscountSummary));

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

        HoldSaleCommand.NotifyCanExecuteChanged();
        OpenHeldSalesCommand.NotifyCanExecuteChanged();

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
        global::POS.Application.Common.PosLog.Error(_logger,
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

        var recoverySource = _recoveryLoadSource;
        _recoveryLoadSource = null;
        if (recoverySource is not null)
        {
            recoverySource.Cancel();
            recoverySource.Dispose();
        }

        var paymentIntentRecoverySource =
            _paymentIntentRecoveryLoadSource;
        _paymentIntentRecoveryLoadSource = null;
        if (paymentIntentRecoverySource is not null)
        {
            paymentIntentRecoverySource.Cancel();
            paymentIntentRecoverySource.Dispose();
        }

        _pendingVietQrAuthorization =
            null;

        GC.SuppressFinalize(
            this);
    }
}
