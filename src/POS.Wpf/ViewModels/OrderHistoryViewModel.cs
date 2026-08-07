using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.DTOs.Orders;
using POS.Domain.Enums;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public sealed record OrderStatusFilterOption(
    string DisplayName,
    OrderHistoryStatus? Value);

public sealed record PaymentMethodFilterOption(
    string DisplayName,
    PaymentMethod? Value);

public sealed class OrderHistoryViewModel : ViewModelBase, IDisposable
{
    private readonly IOrderHistoryService _service;
    private readonly IReceiptPreviewService _preview;
    private readonly ILogger<OrderHistoryViewModel> _logger;
    private readonly IOrderReturnWindowService? _returnWindow;
    private readonly IPermissionService? _permissions;
    private CancellationTokenSource? _loadSource;
    private CancellationTokenSource? _detailsSource;
    private CancellationTokenSource? _receiptSource;
    private OrderHistoryRowViewModel? _selectedOrder;
    private OrderHistoryDetailsDto? _selectedDetails;
    private string _searchText = string.Empty;
    private DateTime? _fromDate = DateTime.Today;
    private DateTime? _toDate = DateTime.Today;
    private OrderStatusFilterOption _selectedStatusFilter;
    private PaymentMethodFilterOption _selectedPaymentMethodFilter;
    private bool _isLoading;
    private bool _isLoadingDetails;
    private bool _isOpeningReceipt;
    private string _statusMessage = "Sẵn sàng tra cứu.";
    private string? _errorMessage;
    private int _currentPage = 1;
    private int _totalCount;
    private int _totalPages;
    private bool _disposed;

    public OrderHistoryViewModel(
        IOrderHistoryService service,
        IReceiptPreviewService preview,
        ILogger<OrderHistoryViewModel> logger,
        IOrderReturnWindowService? returnWindow = null,
        IPermissionService? permissions = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _returnWindow = returnWindow;
        _permissions = permissions;
        StatusFilters =
        [
            new("Tất cả trạng thái", null),
            new("Hoàn thành", OrderHistoryStatus.Completed),
            new("Hoàn một phần", OrderHistoryStatus.PartiallyReturned),
            new("Đã hoàn", OrderHistoryStatus.FullyReturned)
        ];
        PaymentMethodFilters =
        [
            new("Tất cả phương thức", null),
            new("Tiền mặt", PaymentMethod.Cash),
            new("VietQR", PaymentMethod.VietQr),
            new("Chuyển khoản", PaymentMethod.BankTransfer),
            new("Thẻ", PaymentMethod.Card)
        ];
        _selectedStatusFilter = StatusFilters.Single(
            option => option.Value == OrderHistoryStatus.Completed);
        _selectedPaymentMethodFilter = PaymentMethodFilters[0];
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading, OnCommandError);
        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsLoading, OnCommandError);
        ResetFiltersCommand = new AsyncRelayCommand(ResetFiltersAsync, () => !IsLoading, OnCommandError);
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, () => CanGoPrevious, OnCommandError);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, () => CanGoNext, OnCommandError);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading, OnCommandError);
        OpenReceiptCommand = new AsyncRelayCommand(OpenReceiptAsync, () => CanOpenReceipt, OnCommandError);
        OpenReturnCommand = new AsyncRelayCommand(OpenReturnAsync, () => CanOpenReturn, OnCommandError);
    }

    public ObservableCollection<OrderHistoryRowViewModel> Orders { get; } = [];
    public ObservableCollection<OrderHistoryLineViewModel> SelectedOrderLines { get; } = [];
    public IReadOnlyList<OrderStatusFilterOption> StatusFilters { get; }
    public IReadOnlyList<PaymentMethodFilterOption> PaymentMethodFilters { get; }
    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand ResetFiltersCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand OpenReceiptCommand { get; }
    public AsyncRelayCommand OpenReturnCommand { get; }
    private readonly int _pageSize = 25;
    public int PageSize => _pageSize;

    public OrderHistoryRowViewModel? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (!SetProperty(ref _selectedOrder, value))
            {
                return;
            }
            _ = LoadDetailsAsync(value);
            NotifySelectionState();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value ?? string.Empty)) CurrentPage = 1; }
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set { if (SetProperty(ref _fromDate, value)) CurrentPage = 1; }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set { if (SetProperty(ref _toDate, value)) CurrentPage = 1; }
    }

    public OrderStatusFilterOption SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set { if (SetProperty(ref _selectedStatusFilter, value ?? throw new ArgumentNullException(nameof(value)))) CurrentPage = 1; }
    }

    public PaymentMethodFilterOption SelectedPaymentMethodFilter
    {
        get => _selectedPaymentMethodFilter;
        set { if (SetProperty(ref _selectedPaymentMethodFilter, value ?? throw new ArgumentNullException(nameof(value)))) CurrentPage = 1; }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                NotifyCommands();
            }
        }
    }

    public bool IsLoadingDetails
    {
        get => _isLoadingDetails;
        private set
        {
            if (SetProperty(ref _isLoadingDetails, value))
            {
                NotifySelectionState();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (SetProperty(ref _totalPages, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public bool CanGoPrevious => !IsLoading && CurrentPage > 1;
    public bool CanGoNext => !IsLoading && CurrentPage < TotalPages;
    public bool HasSelectedOrder => SelectedOrder is not null;
    public string? SelectedOrderNotes =>
        _selectedDetails is not null &&
        _selectedDetails.OrderId == SelectedOrder?.OrderId
            ? _selectedDetails.Notes
            : null;
    public bool HasSelectedOrderNotes =>
        !string.IsNullOrWhiteSpace(SelectedOrderNotes);
    public string SelectedSubtotalText => FormatSelectedMoney(details => details.Subtotal);
    public string SelectedDiscountAmountText => FormatSelectedMoney(details => details.DiscountAmount);
    public string SelectedOriginalTotalText => FormatSelectedMoney(details => details.TotalAmount);
    public string SelectedRefundedAmountText => FormatSelectedMoney(details => details.Lines.Sum(line => line.RefundedAmount));
    public string SelectedRemainingValueText => FormatSelectedMoney(details =>
        Math.Max(0, details.TotalAmount - details.Lines.Sum(line => line.RefundedAmount)));
    public string SelectedCashReceivedText => FormatSelectedMoney(details => details.CashReceived);
    public string SelectedChangeAmountText => FormatSelectedMoney(details => details.ChangeAmount);
    public string SelectedOrderTotalsText
    {
        get
        {
            var details = _selectedDetails;
            if (details is null || details.OrderId != SelectedOrder?.OrderId) return string.Empty;
            var refunded = details.Lines.Sum(line => line.RefundedAmount);
            var remaining = Math.Max(0, details.TotalAmount - refunded);
            return $"Tạm tính: {details.Subtotal:N0} ₫\n" +
                   $"Giảm giá: {details.DiscountAmount:N0} ₫\n" +
                   $"Tổng tiền gốc: {details.TotalAmount:N0} ₫\n" +
                   $"Đã hoàn: {refunded:N0} ₫\n" +
                   $"Giá trị còn lại: {remaining:N0} ₫\n" +
                   $"Khách đưa: {details.CashReceived:N0} ₫ · Tiền thừa: {details.ChangeAmount:N0} ₫";
        }
    }
    public bool HasSelectedReturns => _selectedDetails?.Returns?.Count > 0;
    public string SelectedReturnsText => _selectedDetails?.Returns is not { Count: > 0 } values
        ? "Không có dữ liệu trả hàng."
        : string.Join("\n", values.Select(value =>
            $"{value.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} · {value.ProcessedBy} · " +
            $"SL {value.ReturnedQuantity:N0} · {value.RefundedAmount:N0} ₫ · {value.Reason}"));

    private string FormatSelectedMoney(Func<OrderHistoryDetailsDto, long> selector)
    {
        var details = _selectedDetails;
        return details is null || details.OrderId != SelectedOrder?.OrderId
            ? "—"
            : $"{selector(details):N0} ₫";
    }
    public bool HasSelectedVietQrReference
    {
        get
        {
            var details = _selectedDetails;
            return details is not null &&
                   details.OrderId == SelectedOrder?.OrderId &&
                   !string.IsNullOrWhiteSpace(details.PaymentIntentDisplayCode);
        }
    }
    public string SelectedVietQrReferenceText =>
        HasSelectedVietQrReference
            ? $"{_selectedDetails!.PaymentIntentDisplayCode} · " +
              $"{_selectedDetails.PaymentConfirmedAtUtc?.ToLocalTime():dd/MM/yyyy HH:mm}"
            : string.Empty;

    public bool HasSelectedOrderDiscount
    {
        get
        {
            var details = _selectedDetails;
            return details is not null &&
                   details.OrderId == SelectedOrder?.OrderId &&
                   details.SalesDiscountType != POS.Domain.Enums.SalesDiscountType.None;
        }
    }

    public string SelectedOrderDiscountText
    {
        get
        {
            var details = _selectedDetails;
            if (!HasSelectedOrderDiscount || details is null)
                return string.Empty;

            return $"{(details.SalesDiscountType == POS.Domain.Enums.SalesDiscountType.FixedAmount ? "Giảm theo số tiền" : "Giảm theo phần trăm")}: " +
                   $"{SalesDiscountPresentationFormatter.FormatRequestedValue(details.SalesDiscountType, details.RequestedDiscountValue)}\n" +
                   $"Số tiền giảm: {SalesDiscountPresentationFormatter.FormatMoney(details.DiscountAmount)}\n" +
                   $"Lý do: {details.DiscountReason}\n" +
                   $"Người thực hiện: {details.DiscountAppliedBy} · " +
                   $"{SalesDiscountPresentationFormatter.FormatLocalTime(details.DiscountAppliedAtUtc)}";
        }
    }
    public bool HasReceiptSnapshot =>
        _selectedDetails?.OrderId == SelectedOrder?.OrderId &&
        _selectedDetails?.HasReceiptSnapshot == true;
    public bool CanOpenReceipt =>
        HasSelectedOrder &&
        HasReceiptSnapshot &&
        !IsLoadingDetails &&
        !_isOpeningReceipt;
    public bool CanOpenReturn =>
        _returnWindow is not null &&
        _permissions?.HasPermission(SystemCapability.ProcessReturns) == true &&
        _selectedDetails?.OrderId == SelectedOrder?.OrderId &&
        _selectedDetails?.Status != OrderStatus.Refunded &&
        _selectedDetails?.Lines.Any(line => line.ReturnedQuantity < line.Quantity) == true &&
        !IsLoading && !IsLoadingDetails;
    public string ReturnAvailabilityMessage => !HasSelectedOrder ? "Chọn đơn để trả hàng."
        : IsLoadingDetails ? "Đang kiểm tra khả năng trả hàng."
        : _selectedDetails?.Lines.Any(line => line.ReturnedQuantity < line.Quantity) != true
            ? "Đơn đã hoàn toàn bộ, không còn số lượng có thể trả." : string.Empty;
    public string ReceiptAvailabilityMessage =>
        HasSelectedOrder && !IsLoadingDetails && !HasReceiptSnapshot
            ? "Đơn cũ chưa có snapshot hóa đơn để in lại."
            : string.Empty;

    public static (DateTimeOffset? FromUtc, DateTimeOffset? ToUtc)
        ConvertLocalDateRangeToUtc(DateTime? fromDate, DateTime? toDate)
    {
        DateTimeOffset? fromUtc = fromDate.HasValue
            ? ConvertLocalToUtc(DateTime.SpecifyKind(
                fromDate.Value.Date,
                DateTimeKind.Unspecified))
            : null;
        DateTimeOffset? toUtc = toDate.HasValue
            ? ConvertLocalToUtc(DateTime.SpecifyKind(
                toDate.Value.Date.AddDays(1).AddMilliseconds(-1),
                DateTimeKind.Unspecified))
            : null;
        return (fromUtc, toUtc);
    }

    private static DateTimeOffset ConvertLocalToUtc(DateTime local)
    {
        if (TimeZoneInfo.Local.IsInvalidTime(local))
        {
            throw new ArgumentException(
                "Thời gian đã chọn không tồn tại trong múi giờ Windows của máy.");
        }
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.Local);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadAsync();
    }

    private async Task ResetFiltersAsync()
    {
        SearchText = string.Empty;
        FromDate = DateTime.Today;
        ToDate = DateTime.Today;
        SelectedStatusFilter = StatusFilters.Single(
            option => option.Value == OrderHistoryStatus.Completed);
        SelectedPaymentMethodFilter = PaymentMethodFilters[0];
        CurrentPage = 1;
        await LoadAsync();
    }

    private async Task PreviousPageAsync()
    {
        CurrentPage--;
        await LoadAsync();
    }

    private async Task NextPageAsync()
    {
        CurrentPage++;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (FromDate.HasValue && ToDate.HasValue &&
            FromDate.Value.Date > ToDate.Value.Date)
        {
            ErrorMessage = "Từ ngày không được sau Đến ngày.";
            return;
        }

        var source = ReplaceSource(ref _loadSource);
        IsLoading = true;
        ErrorMessage = null;
        ClearSelection();
        try
        {
            var range = ConvertLocalDateRangeToUtc(FromDate, ToDate);
            var result = await _service.SearchAsync(
                new OrderHistorySearchRequest(
                    SearchText,
                    SelectedStatusFilter.Value,
                    SelectedPaymentMethodFilter.Value,
                    null,
                    range.FromUtc,
                    range.ToUtc,
                    CurrentPage,
                    PageSize),
                source.Token);
            if (source.IsCancellationRequested)
            {
                return;
            }
            if (result.IsFailure)
            {
                ErrorMessage = result.AppError.Message;
                return;
            }
            Orders.Clear();
            foreach (var item in result.Value.Items)
            {
                Orders.Add(new OrderHistoryRowViewModel(item));
            }
            TotalCount = result.Value.TotalCount;
            TotalPages = result.Value.TotalPages;
            StatusMessage = $"Tìm thấy {TotalCount:N0} đơn hàng.";
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_loadSource, source))
            {
                IsLoading = false;
            }
        }
    }

    private async Task LoadDetailsAsync(OrderHistoryRowViewModel? row)
    {
        _detailsSource?.Cancel();
        SelectedOrderLines.Clear();
        _selectedDetails = null;
        OnPropertyChanged(nameof(SelectedOrderNotes));
        OnPropertyChanged(nameof(HasSelectedOrderNotes));
        OnPropertyChanged(nameof(SelectedOrderTotalsText));
        NotifyFinancialSummary();
        OnPropertyChanged(nameof(HasSelectedReturns));
        OnPropertyChanged(nameof(SelectedReturnsText));
        OnPropertyChanged(nameof(HasSelectedOrderDiscount));
        OnPropertyChanged(nameof(SelectedOrderDiscountText));
        OnPropertyChanged(nameof(HasSelectedVietQrReference));
        OnPropertyChanged(nameof(SelectedVietQrReferenceText));
        NotifySelectionState();
        if (row is null || _disposed)
        {
            return;
        }
        var source = ReplaceSource(ref _detailsSource);
        IsLoadingDetails = true;
        try
        {
            var result = await _service.GetDetailsAsync(row.OrderId, source.Token);
            if (source.IsCancellationRequested ||
                SelectedOrder?.OrderId != row.OrderId)
            {
                return;
            }
            if (result.IsFailure)
            {
                ErrorMessage = result.AppError.Message;
                return;
            }
            _selectedDetails = result.Value;
            OnPropertyChanged(nameof(SelectedOrderNotes));
            OnPropertyChanged(nameof(HasSelectedOrderNotes));
            OnPropertyChanged(nameof(SelectedOrderTotalsText));
            NotifyFinancialSummary();
            OnPropertyChanged(nameof(HasSelectedReturns));
            OnPropertyChanged(nameof(SelectedReturnsText));
            OnPropertyChanged(nameof(HasSelectedOrderDiscount));
            OnPropertyChanged(nameof(SelectedOrderDiscountText));
            OnPropertyChanged(nameof(HasSelectedVietQrReference));
            OnPropertyChanged(nameof(SelectedVietQrReferenceText));
            foreach (var line in result.Value.Lines)
            {
                SelectedOrderLines.Add(new OrderHistoryLineViewModel(line));
            }
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger, exception, "Không thể tải chi tiết đơn hàng.");
            ErrorMessage = "Không thể tải chi tiết đơn hàng.";
        }
        finally
        {
            if (ReferenceEquals(_detailsSource, source))
            {
                IsLoadingDetails = false;
            }
            NotifySelectionState();
        }
    }

    private async Task OpenReceiptAsync()
    {
        var orderId = SelectedOrder?.OrderId;
        if (!orderId.HasValue || !CanOpenReceipt)
        {
            return;
        }
        var source = ReplaceSource(ref _receiptSource);
        _isOpeningReceipt = true;
        NotifySelectionState();
        try
        {
            var result = await _service.GetReprintReceiptAsync(
                orderId.Value,
                source.Token);
            if (source.IsCancellationRequested ||
                SelectedOrder?.OrderId != orderId.Value)
            {
                return;
            }
            if (result.IsFailure)
            {
                ErrorMessage = result.AppError.Message;
                return;
            }
            await _preview.ShowAsync(result.Value, source.Token);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            global::POS.Application.Common.PosLog.Error(_logger, exception, "Không thể mở bản sao hóa đơn.");
            ErrorMessage = "Không thể mở bản sao hóa đơn.";
        }
        finally
        {
            if (ReferenceEquals(_receiptSource, source))
            {
                _isOpeningReceipt = false;
            }
            NotifySelectionState();
        }
    }

    private async Task OpenReturnAsync()
    {
        var selected = SelectedOrder;
        if (selected is null || !CanOpenReturn || _returnWindow is null)
            return;

        if (await _returnWindow.ShowAsync(selected.OrderId))
            await LoadDetailsAsync(selected);
    }

    private static CancellationTokenSource ReplaceSource(
        ref CancellationTokenSource? target)
    {
        target?.Cancel();
        target?.Dispose();
        target = new CancellationTokenSource();
        return target;
    }

    private void ClearSelection()
    {
        SelectedOrder = null;
        SelectedOrderLines.Clear();
        _selectedDetails = null;
        OnPropertyChanged(nameof(SelectedOrderNotes));
        OnPropertyChanged(nameof(HasSelectedOrderNotes));
        OnPropertyChanged(nameof(SelectedOrderTotalsText));
        NotifyFinancialSummary();
        OnPropertyChanged(nameof(HasSelectedReturns));
        OnPropertyChanged(nameof(SelectedReturnsText));
        NotifySelectionState();
    }

    private void NotifySelectionState()
    {
        OnPropertyChanged(nameof(HasSelectedOrder));
        OnPropertyChanged(nameof(HasReceiptSnapshot));
        OnPropertyChanged(nameof(CanOpenReceipt));
        OnPropertyChanged(nameof(CanOpenReturn));
        OnPropertyChanged(nameof(ReturnAvailabilityMessage));
        OnPropertyChanged(nameof(ReceiptAvailabilityMessage));
        OpenReceiptCommand.NotifyCanExecuteChanged();
        OpenReturnCommand.NotifyCanExecuteChanged();
    }

    private void NotifyFinancialSummary()
    {
        OnPropertyChanged(nameof(SelectedSubtotalText));
        OnPropertyChanged(nameof(SelectedDiscountAmountText));
        OnPropertyChanged(nameof(SelectedOriginalTotalText));
        OnPropertyChanged(nameof(SelectedRefundedAmountText));
        OnPropertyChanged(nameof(SelectedRemainingValueText));
        OnPropertyChanged(nameof(SelectedCashReceivedText));
        OnPropertyChanged(nameof(SelectedChangeAmountText));
    }

    private void NotifyCommands()
    {
        LoadCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
        ResetFiltersCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
    }

    private void OnCommandError(Exception exception)
    {
        global::POS.Application.Common.PosLog.Error(_logger, exception, "Lệnh lịch sử đơn hàng thất bại.");
        ErrorMessage = "Không thể hoàn thành thao tác lịch sử đơn hàng.";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _loadSource?.Cancel();
        _detailsSource?.Cancel();
        _receiptSource?.Cancel();
        _loadSource?.Dispose();
        _detailsSource?.Dispose();
        _receiptSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
