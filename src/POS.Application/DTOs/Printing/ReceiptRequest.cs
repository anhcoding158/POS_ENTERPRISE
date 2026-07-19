using POS.Domain.Enums;

namespace POS.Application.DTOs.Printing;

/// <summary>
/// Toàn bộ dữ liệu nghiệp vụ cần thiết để in hóa đơn.
///
/// Tên cửa hàng, địa chỉ, hotline và cấu hình máy in
/// sẽ được Infrastructure lấy từ cấu hình ứng dụng.
/// </summary>
public sealed class ReceiptRequest
{
    public ReceiptRequest(
        int orderId,
        string? orderCode,
        string? cashierName,
        DateTimeOffset createdAtUtc,
        PaymentMethod paymentMethod,
        long subtotal,
        long discountAmount,
        long totalAmount,
        long cashReceived,
        long changeAmount,
        IEnumerable<ReceiptLineDto>? lines,
        string? customerName = null,
        string? restaurantTableName = null,
        string? discountCode = null,
        string? notes = null)
    {
        OrderId = orderId;
        OrderCode = NormalizeRequiredText(orderCode);
        CashierName = NormalizeRequiredText(cashierName);

        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        PaymentMethod = paymentMethod;

        Subtotal = subtotal;
        DiscountAmount = discountAmount;
        TotalAmount = totalAmount;
        CashReceived = cashReceived;
        ChangeAmount = changeAmount;

        Lines = lines?
            .ToArray()
            ?? Array.Empty<ReceiptLineDto>();

        CustomerName = NormalizeOptionalText(customerName);

        RestaurantTableName =
            NormalizeOptionalText(restaurantTableName);

        DiscountCode =
            NormalizeOptionalText(discountCode);

        Notes = NormalizeOptionalText(notes);
    }

    public int OrderId { get; }

    public string OrderCode { get; }

    public string CashierName { get; }

    public string? CustomerName { get; }

    public string? RestaurantTableName { get; }

    public string? DiscountCode { get; }

    public string? Notes { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public PaymentMethod PaymentMethod { get; }

    public long Subtotal { get; }

    public long DiscountAmount { get; }

    public long TotalAmount { get; }

    public long CashReceived { get; }

    public long ChangeAmount { get; }

    public IReadOnlyList<ReceiptLineDto> Lines { get; }

    private static string NormalizeRequiredText(
        string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}