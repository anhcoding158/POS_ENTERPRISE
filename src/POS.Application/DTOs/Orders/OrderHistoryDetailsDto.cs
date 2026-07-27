using POS.Domain.Enums;

namespace POS.Application.DTOs.Orders;

public sealed record OrderHistoryDetailsDto(
    int OrderId,
    string OrderCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PaidAtUtc,
    int CashierUserId,
    string CashierName,
    OrderStatus Status,
    PaymentMethod? PaymentMethod,
    long Subtotal,
    long DiscountAmount,
    long TotalAmount,
    long CashReceived,
    long ChangeAmount,
    string? Notes,
    string? DiscountCode,
    int? CustomerId,
    int? RestaurantTableId,
    bool HasReceiptSnapshot,
    IReadOnlyList<OrderHistoryLineDto> Lines);
