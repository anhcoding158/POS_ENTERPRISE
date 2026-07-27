using System.Security.Cryptography;
using System.Text;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Orders;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Services;

namespace POS.Application.Services;

public sealed class OrderReturnService(
    IOrderReturnRepository returns,
    IOrderRepository orders,
    IProductRepository products,
    IInventoryMovementRepository movements,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IClock clock) : IOrderReturnService
{
    public async Task<Result<OrderReturnResultDto>> ProcessAsync(
        OrderReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateAndNormalize(request);
        if (validation.Error is not null)
            return Result.Failure<OrderReturnResultDto>(validation.Error);

        if (currentUser.UserId is not int actorId || actorId <= 0)
            return Failure("ORDER_RETURN.UNAUTHORIZED", "Phiên người dùng không hợp lệ.");

        var fingerprint = ComputeFingerprint(validation.Request!);
        var existing = await returns.GetByClientRequestIdAsync(
            validation.Request!.ClientRequestId, cancellationToken);
        if (existing is not null)
            return string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                ? Result.Success(MapResult(existing, true))
                : Failure("ORDER_RETURN.IDEMPOTENCY_CONFLICT", "ClientRequestId đã được dùng cho yêu cầu khác.");

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await orders.GetByIdAsync(validation.Request.OrderId, cancellationToken);
            if (order is null)
                return await RollbackFailure("ORDER_RETURN.NOT_FOUND", "Không tìm thấy đơn hàng.", transaction);
            if (order.Status != OrderStatus.Completed)
                return await RollbackFailure("ORDER_RETURN.NOT_COMPLETED", "Chỉ đơn đã hoàn tất mới được trả.", transaction);

            var allocations = OrderReturnRefundAllocator.AllocateOrderTotal(
                order.TotalAmount,
                order.Items.Select(item =>
                    new OrderReturnAllocationLine(item.Id, item.Quantity, item.NetAmount)));
            var requestItems = new List<OrderReturnItem>();
            var now = clock.UtcNow;

            foreach (var line in validation.Request.Lines)
            {
                var orderItem = order.Items.SingleOrDefault(item => item.Id == line.OrderItemId);
                if (orderItem is null)
                    return await RollbackFailure("ORDER_RETURN.LINE_NOT_FOUND", "Dòng hàng không thuộc đơn.", transaction);

                var balance = await returns.GetOrCreateTrackedBalanceAsync(orderItem.Id, cancellationToken);
                if (line.ReturnQuantity > orderItem.Quantity - balance.ReturnedQuantity)
                    return await RollbackFailure("ORDER_RETURN.QUANTITY_EXCEEDED", "Số lượng trả vượt quá số còn lại.", transaction);

                var refund = OrderReturnRefundAllocator.CalculateCurrentRefund(
                    allocations[orderItem.Id],
                    orderItem.Quantity,
                    balance.ReturnedQuantity,
                    balance.RefundedAmount,
                    line.ReturnQuantity);
                balance.Register(line.ReturnQuantity, refund, orderItem.Quantity, allocations[orderItem.Id]);

                if (line.RestockQuantity > 0)
                {
                    var product = await products.GetByIdAsync(orderItem.ProductId, cancellationToken);
                    if (product is null || !product.TrackInventory)
                        return await RollbackFailure("ORDER_RETURN.CANNOT_RESTOCK", "Sản phẩm không theo dõi kho.", transaction);

                    var before = product.StockQuantity;
                    product.RestockFromCustomerReturn(line.RestockQuantity, now);
                    await movements.AddAsync(
                        new InventoryMovement(
                            product.Id,
                            InventoryMovementType.CustomerReturn,
                            line.RestockQuantity,
                            before,
                            product.StockQuantity,
                            validation.Request.Reason,
                            now,
                            "ORDER_RETURN",
                            validation.Request.ClientRequestId.ToString("D"),
                            actorId),
                        cancellationToken);
                }

                requestItems.Add(new OrderReturnItem(
                    orderItem.Id,
                    orderItem.ProductId,
                    orderItem.ProductCode,
                    orderItem.ProductName,
                    orderItem.UnitName,
                    line.ReturnQuantity,
                    line.RestockQuantity,
                    refund));
            }

            var document = new OrderReturn(
                validation.Request.ClientRequestId,
                fingerprint,
                order.Id,
                actorId,
                now,
                validation.Request.Reason,
                validation.Request.RefundMethod,
                validation.Request.RefundReference,
                requestItems);
            await returns.AddAsync(document, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(MapResult(document, false));
        }
        catch (OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception exception) when (
            exception is DomainException or
            PersistenceConflictException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Failure(
                exception is PersistenceConflictException
                    ? "ORDER_RETURN.CONCURRENCY_CONFLICT"
                    : "ORDER_RETURN.DOMAIN_FAILURE",
                exception.Message);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<IReadOnlyList<OrderReturnSummaryDto>>> GetReturnsByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
            return Result.Failure<IReadOnlyList<OrderReturnSummaryDto>>(
                new Error(ErrorCodes.General.Validation, "OrderId không hợp lệ."));
        var documents = await returns.GetByOrderIdReadOnlyAsync(orderId, cancellationToken);
        return Result.Success<IReadOnlyList<OrderReturnSummaryDto>>(
            documents.Select(MapSummary).ToArray());
    }

    public async Task<Result<ReturnableOrderDto>> GetReturnableOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await orders.GetByIdReadOnlyAsync(orderId, cancellationToken);
        if (order is null)
            return Result.Failure<ReturnableOrderDto>(
                new Error("ORDER_RETURN.NOT_FOUND", "Không tìm thấy đơn hàng."));
        if (order.Status != OrderStatus.Completed)
            return Result.Failure<ReturnableOrderDto>(
                new Error("ORDER_RETURN.NOT_COMPLETED", "Đơn hàng không đủ điều kiện trả."));

        var balances = await returns.GetBalancesForOrderAsync(orderId, cancellationToken);
        var allocations = OrderReturnRefundAllocator.AllocateOrderTotal(
            order.TotalAmount,
            order.Items.Select(item => new OrderReturnAllocationLine(item.Id, item.Quantity, item.NetAmount)));
        var lines = new List<ReturnableOrderLineDto>();
        foreach (var item in order.Items)
        {
            balances.TryGetValue(item.Id, out var balance);
            var returned = balance?.ReturnedQuantity ?? 0;
            var refunded = balance?.RefundedAmount ?? 0;
            var product = await products.GetByIdAsync(item.ProductId, cancellationToken);
            lines.Add(new(
                item.Id, item.ProductId, item.ProductCode, item.ProductName, item.UnitName,
                item.Quantity, returned, item.Quantity - returned,
                allocations[item.Id] - refunded,
                product?.TrackInventory == true,
                product?.IsArchived == true));
        }
        var prior = await returns.GetByOrderIdReadOnlyAsync(orderId, cancellationToken);
        return Result.Success(new ReturnableOrderDto(
            order.Id, order.OrderCode, order.PaidAtUtc ?? order.CreatedAtUtc,
            order.CashierUser?.FullName ?? $"#{order.CashierUserId}",
            order.PaymentMethod!.Value, lines, prior.Select(MapSummary).ToArray()));
    }

    public static string ComputeFingerprint(OrderReturnRequest request)
    {
        var canonical = new StringBuilder()
            .Append(request.OrderId).Append('\n')
            .Append(NormalizeText(request.Reason)).Append('\n')
            .Append((int)request.RefundMethod).Append('\n')
            .Append(NormalizeOptional(request.RefundReference) ?? string.Empty).Append('\n');
        foreach (var line in request.Lines.OrderBy(line => line.OrderItemId))
            canonical.Append(line.OrderItemId).Append(':')
                .Append(line.ReturnQuantity).Append(':')
                .Append(line.RestockQuantity).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static (OrderReturnRequest? Request, Error? Error) ValidateAndNormalize(OrderReturnRequest request)
    {
        if (request is null || request.ClientRequestId == Guid.Empty ||
            request.OrderId <= 0 || string.IsNullOrWhiteSpace(request.Reason) ||
            !Enum.IsDefined(request.RefundMethod) || request.Lines is null ||
            request.Lines.Count == 0 || request.Lines.GroupBy(line => line.OrderItemId).Any(group => group.Count() > 1) ||
            request.Lines.Any(line => line.OrderItemId <= 0 || line.ReturnQuantity <= 0 ||
                line.RestockQuantity < 0 || line.RestockQuantity > line.ReturnQuantity))
            return (null, new Error(ErrorCodes.General.Validation, "Yêu cầu trả hàng không hợp lệ."));

        return (request with
        {
            Reason = NormalizeText(request.Reason),
            RefundReference = NormalizeOptional(request.RefundReference),
            Lines = request.Lines.OrderBy(line => line.OrderItemId).ToArray()
        }, null);
    }

    private static string NormalizeText(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeText(value);
    private static OrderReturnSummaryDto MapSummary(OrderReturn entity) =>
        new(entity.Id, entity.CreatedAtUtc, entity.TotalRefundAmount, entity.RefundMethod, entity.Reason);
    private static OrderReturnResultDto MapResult(OrderReturn entity, bool replay) =>
        new(entity.Id, entity.ClientRequestId, entity.OrderId, entity.CreatedAtUtc,
            entity.TotalRefundAmount, replay,
            entity.Items.Select(item => new OrderReturnLineResultDto(
                item.OrderItemId, item.ProductId, item.ProductCode, item.ProductName,
                item.ReturnQuantity, item.RestockQuantity, item.RefundAmount)).ToArray());
    private static Result<OrderReturnResultDto> Failure(string code, string message) =>
        Result.Failure<OrderReturnResultDto>(new Error(code, message));
    private static async Task<Result<OrderReturnResultDto>> RollbackFailure(
        string code, string message, IApplicationTransaction transaction)
    {
        await transaction.RollbackAsync(CancellationToken.None);
        return Failure(code, message);
    }
}
