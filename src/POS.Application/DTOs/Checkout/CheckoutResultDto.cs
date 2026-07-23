using POS.Application.DTOs.Printing;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Checkout;

/// <summary>
/// Kết quả hoàn chỉnh sau khi giao dịch được lưu
/// và transaction đã commit thành công.
///
/// ReceiptSnapshot được tạo trước transaction commit,
/// nhưng chỉ được trả cho Presentation sau khi commit thành công.
///
/// Việc preview hoặc in hóa đơn không thuộc trách nhiệm
/// của CheckoutService.
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
    IReadOnlyList<CheckoutLineResultDto> Lines)
{
    /// <summary>
    /// Snapshot hóa đơn bất biến của giao dịch.
    ///
    /// CheckoutService production luôn thiết lập thuộc tính này
    /// trước khi commit transaction.
    ///
    /// Nullable tạm thời để giữ tương thích với các fake service
    /// và test cũ đang tự tạo CheckoutResultDto.
    /// </summary>
    public ReceiptRequest? ReceiptSnapshot
    {
        get;
        init;
    }
}