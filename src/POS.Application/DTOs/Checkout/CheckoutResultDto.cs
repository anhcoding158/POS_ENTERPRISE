using POS.Domain.Enums;

namespace POS.Application.DTOs.Checkout;

/// <summary>
/// Kết quả hoàn chỉnh sau khi giao dịch được lưu thành công.
/// </summary>
public sealed record CheckoutResultDto(
    int OrderId,
    string OrderCode,
    int CashierUserId,
    string CashierName,
    int? CustomerId,
    string? CustomerName,
    int? RestaurantTableId,
    string? RestaurantTableName,
    string? DiscountCode,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    long Subtotal,
    long DiscountAmount,
    long TotalAmount,
    long CashReceived,
    long ChangeAmount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset PaidAtUtc,
    IReadOnlyList<CheckoutLineResultDto> Lines);