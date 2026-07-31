using System.Collections.ObjectModel;
using POS.Application.Abstractions.Services;
using POS.Application.DTOs.Orders;
using POS.Domain.Enums;
using POS.Wpf.Commands;
using POS.Wpf.Services;

namespace POS.Wpf.ViewModels;

public sealed class OrderReturnViewModel : ViewModelBase, IDisposable
{
    private readonly IOrderReturnService _service;
    private readonly IOrderReturnConfirmationService _confirmation;
    private readonly int _orderId;
    private readonly CancellationTokenSource _lifetimeSource = new();
    private string _reason = string.Empty;
    private string? _refundReference;
    private PaymentMethod _refundMethod = PaymentMethod.Cash;
    private bool _isLoading;
    private bool _isSubmitting;
    private bool _isSuccessful;
    private bool _disposed;
    private long _loadVersion;
    private string? _message;

    public OrderReturnViewModel(
        IOrderReturnService service,
        IOrderReturnConfirmationService confirmation,
        int orderId)
    {
        _service = service;
        _confirmation = confirmation;
        _orderId = orderId;
        ClientRequestId = Guid.NewGuid();
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => CanSubmit);
    }

    public Guid ClientRequestId { get; }
    public ObservableCollection<OrderReturnLineViewModel> Lines { get; } = [];
    public AsyncRelayCommand SubmitCommand { get; }
    public string OrderCode { get; private set; } = string.Empty;
    public IReadOnlyList<OrderReturnSummaryDto> PriorReturns { get; private set; } = [];
    public IReadOnlyList<PaymentMethod> RefundMethods { get; } = [PaymentMethod.Cash, PaymentMethod.VietQr];
    public string Reason
    {
        get => _reason;
        set { if (SetProperty(ref _reason, value ?? string.Empty)) Notify(); }
    }
    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);
    public PaymentMethod RefundMethod
    {
        get => _refundMethod;
        set
        {
            if (SetProperty(ref _refundMethod, value))
            {
                OnPropertyChanged(nameof(VietQrWarning));
                OnPropertyChanged(nameof(RefundReferenceHint));
            }
        }
    }
    public string? RefundReference
    {
        get => _refundReference;
        set => SetProperty(ref _refundReference, value);
    }
    public string VietQrWarning => RefundMethod == PaymentMethod.VietQr
        ? "Hệ thống không tự chuyển tiền về tài khoản khách. Hãy thực hiện hoàn tiền bên ngoài và nhập mã tham chiếu nếu có."
        : string.Empty;
    public string RefundReferenceHint => RefundMethod == PaymentMethod.VietQr
        ? "Mã giao dịch hoặc ghi chú đối soát"
        : "Không bắt buộc khi hoàn tiền mặt";
    public long TotalRefundAmount => Lines.Sum(line => line.PreviewRefundAmount);
    public int TotalReturnQuantity => Lines.Sum(line => line.ReturnQuantity);
    public int TotalRestockQuantity => Lines.Sum(line => line.RestockQuantity);
    public bool HasNonRestockedQuantity =>
        TotalReturnQuantity > TotalRestockQuantity;
    public bool CanSubmit => !IsLoading && !IsSubmitting && !IsSuccessful &&
        !string.IsNullOrWhiteSpace(Reason) &&
        Lines.Any(line => line.ReturnQuantity > 0) &&
        Lines.All(line => line.IsValid) && TotalRefundAmount > 0;
    public bool IsLoading
    {
        get => _isLoading;
        private set { if (SetProperty(ref _isLoading, value)) Notify(); }
    }
    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set { if (SetProperty(ref _isSubmitting, value)) Notify(); }
    }
    public bool IsSuccessful
    {
        get => _isSuccessful;
        private set { if (SetProperty(ref _isSuccessful, value)) Notify(); }
    }
    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var version = Interlocked.Increment(ref _loadVersion);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _lifetimeSource.Token);
        IsLoading = true;
        try
        {
            var result = await _service.GetReturnableOrderAsync(
                _orderId, linkedSource.Token);
            if (_disposed || version != Volatile.Read(ref _loadVersion))
                return;
            if (result.IsFailure)
            {
                Message = result.AppError.Message;
                return;
            }
            OrderCode = result.Value.OrderCode;
            PriorReturns = result.Value.PriorReturns;
            foreach (var existingLine in Lines)
                existingLine.PropertyChanged -= OnLinePropertyChanged;
            Lines.Clear();
            foreach (var line in result.Value.Lines.Where(line => line.RemainingQuantity > 0))
            {
                var lineViewModel = new OrderReturnLineViewModel(line);
                lineViewModel.PropertyChanged += OnLinePropertyChanged;
                Lines.Add(lineViewModel);
            }
            OnPropertyChanged(nameof(OrderCode));
            OnPropertyChanged(nameof(PriorReturns));
            Notify();
        }
        catch (OperationCanceledException) when (linkedSource.IsCancellationRequested)
        {
        }
        finally
        {
            if (!_disposed && version == Volatile.Read(ref _loadVersion))
                IsLoading = false;
        }
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit)
            return;
        var confirmationMessage =
            $"Mã đơn: {OrderCode}\n" +
            $"Tổng số lượng sản phẩm trả: {TotalReturnQuantity:N0}\n" +
            $"Tổng số lượng nhập lại kho: {TotalRestockQuantity:N0}\n" +
            $"Tổng tiền hoàn: {TotalRefundAmount:N0} ₫\n" +
            $"Phương thức hoàn: {RefundMethod}\n\n" +
            "Chứng từ trả hàng không thể sửa hoặc xóa sau khi hoàn tất.";
        if (!_confirmation.Confirm(confirmationMessage))
            return;

        IsSubmitting = true;
        try
        {
            var result = await _service.ProcessAsync(
                new OrderReturnRequest(
                    ClientRequestId, _orderId, Reason, RefundMethod, RefundReference,
                    Lines.Where(line => line.ReturnQuantity > 0)
                        .Select(line => new OrderReturnLineRequest(
                            line.OrderItemId, line.ReturnQuantity, line.RestockQuantity)).ToArray()),
                _lifetimeSource.Token);
            if (_disposed)
                return;
            Message = result.IsSuccess
                ? $"Đã tạo chứng từ trả hàng #{result.Value.ReturnId}."
                : result.AppError.Message;
            if (result.IsSuccess)
                IsSuccessful = true;
        }
        catch (OperationCanceledException) when (_lifetimeSource.IsCancellationRequested)
        {
        }
        finally
        {
            if (!_disposed)
                IsSubmitting = false;
        }
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(HasReason));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(TotalRefundAmount));
        OnPropertyChanged(nameof(TotalReturnQuantity));
        OnPropertyChanged(nameof(TotalRestockQuantity));
        OnPropertyChanged(nameof(HasNonRestockedQuantity));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private void OnLinePropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is
            nameof(OrderReturnLineViewModel.ReturnQuantity) or
            nameof(OrderReturnLineViewModel.RestockQuantity) or
            nameof(OrderReturnLineViewModel.PreviewRefundAmount))
            Notify();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Interlocked.Increment(ref _loadVersion);
        foreach (var line in Lines)
            line.PropertyChanged -= OnLinePropertyChanged;
        _lifetimeSource.Cancel();
        _lifetimeSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
