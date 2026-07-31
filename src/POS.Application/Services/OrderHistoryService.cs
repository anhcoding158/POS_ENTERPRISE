using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Orders;
using POS.Application.DTOs.Printing;
using POS.Application.Factories;
using POS.Domain.Entities;

namespace POS.Application.Services;

public sealed class OrderHistoryService : IOrderHistoryService
{
    private const int MaximumPageSize = 200;
    private readonly IOrderRepository _orders;
    private readonly IOrderReceiptSnapshotRepository _snapshots;
    private readonly IReceiptSnapshotSerializer _serializer;

    public OrderHistoryService(
        IOrderRepository orders,
        IOrderReceiptSnapshotRepository snapshots,
        IReceiptSnapshotSerializer serializer)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public async Task<Result<PagedResult<OrderHistoryListItemDto>>> SearchAsync(
        OrderHistorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var error = Validate(request);
        if (error is not null)
        {
            return Result.Failure<PagedResult<OrderHistoryListItemDto>>(error);
        }

        var page = await _orders.SearchAsync(
            request.SearchTerm,
            request.Status,
            customerId: null,
            request.CashierUserId,
            request.FromUtc,
            request.ToUtc,
            request.PaymentMethod,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Result.Success(page.Map(MapListItem));
    }

    public async Task<Result<OrderHistoryDetailsDto>> GetDetailsAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return ValidationFailure<OrderHistoryDetailsDto>(
                "Mã đơn hàng phải lớn hơn 0.");
        }

        var order = await _orders.GetByIdReadOnlyAsync(
            orderId,
            cancellationToken);
        if (order is null)
        {
            return Result.Failure<OrderHistoryDetailsDto>(
                new AppError(ErrorCodes.Orders.NotFound, "Không tìm thấy đơn hàng."));
        }

        var snapshot = await _snapshots.GetByOrderIdAsync(
            orderId,
            cancellationToken);

        ReceiptRequest? receipt = null;
        if (snapshot is not null)
        {
            try
            {
                receipt = _serializer.Deserialize(snapshot.PayloadJson);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or ArgumentException)
            {
                // Details remain available even when an old snapshot is unreadable.
            }
        }

        return Result.Success(MapDetails(order, snapshot is not null, receipt));
    }

    public async Task<Result<ReceiptRequest>> GetReprintReceiptAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return ValidationFailure<ReceiptRequest>(
                "Mã đơn hàng phải lớn hơn 0.");
        }

        var snapshot = await _snapshots.GetByOrderIdAsync(
            orderId,
            cancellationToken);
        if (snapshot is null)
        {
            return Result.Failure<ReceiptRequest>(
                new AppError(
                    ErrorCodes.Orders.ReceiptSnapshotUnavailable,
                    "Đơn hàng này được tạo trước khi hệ thống lưu snapshot hóa đơn, nên chưa thể in lại."));
        }

        ReceiptRequest original;
        try
        {
            original = _serializer.Deserialize(snapshot.PayloadJson);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or ArgumentException)
        {
            return Result.Failure<ReceiptRequest>(
                new AppError(
                    ErrorCodes.Orders.ReceiptSnapshotInvalid,
                    "Snapshot hóa đơn đã lưu không hợp lệ."));
        }

        if (original.OrderId != orderId ||
            original.SnapshotVersion != snapshot.SnapshotVersion)
        {
            return Result.Failure<ReceiptRequest>(
                new AppError(
                    ErrorCodes.Orders.ReceiptSnapshotInvalid,
                    "Snapshot hóa đơn không khớp với đơn hàng."));
        }

        return Result.Success(
            ReceiptSnapshotFactory.CreateReprint(original, 1));
    }

    private static AppError? Validate(OrderHistorySearchRequest request)
    {
        if (request.PageNumber <= 0 ||
            request.PageSize is <= 0 or > MaximumPageSize ||
            request.CashierUserId <= 0 ||
            request.FromUtc.HasValue &&
            request.FromUtc.Value == default ||
            request.ToUtc.HasValue &&
            request.ToUtc.Value == default ||
            request.FromUtc > request.ToUtc ||
            request.Status.HasValue && !Enum.IsDefined(request.Status.Value) ||
            request.PaymentMethod.HasValue && !Enum.IsDefined(request.PaymentMethod.Value))
        {
            return new AppError(
                ErrorCodes.General.Validation,
                "Bộ lọc lịch sử đơn hàng không hợp lệ.");
        }

        return null;
    }

    private static OrderHistoryListItemDto MapListItem(Order order) =>
        new(
            order.Id,
            order.OrderCode,
            order.CreatedAtUtc,
            order.PaidAtUtc,
            order.CashierUserId,
            order.CashierUser?.FullName ?? $"#{order.CashierUserId}",
            order.Status,
            order.PaymentMethod,
            order.Subtotal,
            order.DiscountAmount,
            order.TotalAmount,
            order.CashReceived,
            order.ChangeAmount);

    private static OrderHistoryDetailsDto MapDetails(
        Order order,
        bool hasReceiptSnapshot,
        ReceiptRequest? receipt) =>
        new(
            order.Id,
            order.OrderCode,
            order.CreatedAtUtc,
            order.PaidAtUtc,
            order.CashierUserId,
            order.CashierUser?.FullName ?? $"#{order.CashierUserId}",
            order.Status,
            order.PaymentMethod,
            order.Subtotal,
            order.DiscountAmount,
            order.TotalAmount,
            order.CashReceived,
            order.ChangeAmount,
            order.Notes,
            order.DiscountCode,
            order.CustomerId,
            order.RestaurantTableId,
            hasReceiptSnapshot,
            order.Items.Select(item =>
                new OrderHistoryLineDto(
                    item.Id,
                    item.ProductId,
                    item.ProductCode,
                    item.ProductName,
                    item.UnitName,
                    item.Quantity,
                    item.UnitSalePrice,
                    item.ModifierAmountPerUnit,
                    item.FinalUnitPrice,
                    item.GrossAmount,
                    item.LineDiscountAmount,
                    item.NetAmount,
                    item.Notes,
                    item.Modifiers.Select(modifier =>
                        new OrderHistoryModifierDto(
                            modifier.ModifierId,
                            modifier.ModifierGroupId,
                            modifier.ModifierGroupName,
                            modifier.ModifierName,
                            modifier.Quantity,
                            modifier.UnitAdditionalPrice,
                            modifier.AmountPerProductUnit)).ToArray())).ToArray(),
            order.DiscountSnapshot?.Type ?? POS.Domain.Enums.SalesDiscountType.None,
            order.DiscountSnapshot?.RequestedValue ?? 0,
            order.DiscountSnapshot?.Reason,
            order.DiscountSnapshot?.AppliedByUserId,
            order.DiscountSnapshot?.AppliedByUser.FullName,
            order.DiscountSnapshot?.AppliedAtUtc,
            receipt?.PaymentIntentId,
            receipt?.PaymentIntentDisplayCode,
            receipt?.PaymentConfirmedAtUtc);

    private static Result<T> ValidationFailure<T>(string message) =>
        Result.Failure<T>(
            new AppError(ErrorCodes.General.Validation, message));
}
