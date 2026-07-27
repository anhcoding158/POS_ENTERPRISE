using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Orders;
using POS.Domain.Enums;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public sealed record OrderStatusFilterOption(
    string DisplayName,
    OrderStatus? Value);

public sealed record PaymentMethodFilterOption(
    string DisplayName,
    PaymentMethod? Value);

public sealed class OrderHistoryViewModel : ViewModelBase, IDisposable
{
    private readonly IOrderHistoryService _service;
    private readonly IReceiptPreviewService _preview;
    private readonly ILogger<OrderHistoryViewModel> _logger;
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
        ILogger<OrderHistoryViewModel> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        StatusFilters =
        [
            new("Tất cả trạng thái", null),
            new("Hoàn thành", OrderStatus.Completed),
            new("Đã thanh toán", OrderStatus.Paid),
            new("Chờ thanh toán", OrderStatus.PendingPayment),
            new("Nháp", OrderStatus.Draft),
            new("Đã hủy", OrderStatus.Cancelled),
            new("Hoàn một phần", OrderStatus.PartiallyRefunded),
            new("Đã hoàn", OrderStatus.Refunded)
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
            option => option.Value == OrderStatus.Completed);
        _selectedPaymentMethodFilter = PaymentMethodFilters[0];
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading, OnCommandError);
        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsLoading, OnCommandError);
        ResetFiltersCommand = new AsyncRelayCommand(ResetFiltersAsync, () => !IsLoading, OnCommandError);
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, () => CanGoPrevious, OnCommandError);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, () => CanGoNext, OnCommandError);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsLoading, OnCommandError);
        OpenReceiptCommand = new AsyncRelayCommand(OpenReceiptAsync, () => CanOpenReceipt, OnCommandError);
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
    public int PageSize => 25;

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
        set => SetProperty(ref _searchText, value ?? string.Empty);
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public OrderStatusFilterOption SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set => SetProperty(
            ref _selectedStatusFilter,
            value ?? throw new ArgumentNullException(nameof(value)));
    }

    public PaymentMethodFilterOption SelectedPaymentMethodFilter
    {
        get => _selectedPaymentMethodFilter;
        set => SetProperty(
            ref _selectedPaymentMethodFilter,
            value ?? throw new ArgumentNullException(nameof(value)));
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
    public bool HasReceiptSnapshot =>
        _selectedDetails?.OrderId == SelectedOrder?.OrderId &&
        _selectedDetails?.HasReceiptSnapshot == true;
    public bool CanOpenReceipt =>
        HasSelectedOrder &&
        HasReceiptSnapshot &&
        !IsLoadingDetails &&
        !_isOpeningReceipt;
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
            option => option.Value == OrderStatus.Completed);
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
                ErrorMessage = result.Error.Message;
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
                ErrorMessage = result.Error.Message;
                return;
            }
            _selectedDetails = result.Value;
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
            _logger.LogError(exception, "Không thể tải chi tiết đơn hàng.");
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
                ErrorMessage = result.Error.Message;
                return;
            }
            await _preview.ShowAsync(result.Value, source.Token);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Không thể mở bản sao hóa đơn.");
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
        NotifySelectionState();
    }

    private void NotifySelectionState()
    {
        OnPropertyChanged(nameof(HasSelectedOrder));
        OnPropertyChanged(nameof(HasReceiptSnapshot));
        OnPropertyChanged(nameof(CanOpenReceipt));
        OnPropertyChanged(nameof(ReceiptAvailabilityMessage));
        OpenReceiptCommand.NotifyCanExecuteChanged();
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
        _logger.LogError(exception, "Lệnh lịch sử đơn hàng thất bại.");
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
