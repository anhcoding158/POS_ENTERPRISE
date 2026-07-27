using POS.Domain.Enums;

namespace POS.Application.DTOs.Orders;

public sealed record OrderReturnLineRequest(
    int OrderItemId,
    int ReturnQuantity,
    int RestockQuantity);

public sealed record OrderReturnRequest(
    Guid ClientRequestId,
    int OrderId,
    string Reason,
    PaymentMethod RefundMethod,
    string? RefundReference,
    IReadOnlyList<OrderReturnLineRequest> Lines);

public sealed record OrderReturnLineResultDto(
    int OrderItemId,
    int ProductId,
    string ProductCode,
    string ProductName,
    int ReturnQuantity,
    int RestockQuantity,
    long RefundAmount);

public sealed record OrderReturnResultDto(
    int ReturnId,
    Guid ClientRequestId,
    int OrderId,
    DateTimeOffset CreatedAtUtc,
    long TotalRefundAmount,
    bool IsIdempotentReplay,
    IReadOnlyList<OrderReturnLineResultDto> Lines);

public sealed record OrderReturnSummaryDto(
    int ReturnId,
    DateTimeOffset CreatedAtUtc,
    long TotalRefundAmount,
    PaymentMethod RefundMethod,
    string Reason);

public sealed record ReturnableOrderLineDto(
    int OrderItemId,
    int ProductId,
    string ProductCode,
    string ProductName,
    string UnitName,
    int SoldQuantity,
    int ReturnedQuantity,
    int RemainingQuantity,
    long RemainingRefundableAmount,
    bool TrackInventory,
    bool IsArchived);

public sealed record ReturnableOrderDto(
    int OrderId,
    string OrderCode,
    DateTimeOffset SoldAtUtc,
    string CashierName,
    PaymentMethod OriginalPaymentMethod,
    IReadOnlyList<ReturnableOrderLineDto> Lines,
    IReadOnlyList<OrderReturnSummaryDto> PriorReturns);
