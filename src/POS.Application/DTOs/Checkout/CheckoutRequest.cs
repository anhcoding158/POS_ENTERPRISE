using POS.Domain.Enums;

namespace POS.Application.DTOs.Checkout;

/// <summary>
/// Yêu cầu hoàn tất một giao dịch bán hàng.
///
/// CashierUserId không nằm trong request.
/// CheckoutService phải lấy thu ngân từ ICurrentUserService.
/// </summary>
public sealed class CheckoutRequest
{
    public CheckoutRequest(
        IEnumerable<CheckoutLineRequest>? lines,
        PaymentMethod paymentMethod,
        long cashReceived,
        int? customerId = null,
        int? restaurantTableId = null,
        string? discountCode = null,
        string? notes = null)
    {
        Lines = lines?
            .ToArray()
            ?? Array.Empty<CheckoutLineRequest>();

        PaymentMethod = paymentMethod;
        CashReceived = cashReceived;

        CustomerId = customerId;
        RestaurantTableId = restaurantTableId;

        DiscountCode = NormalizeOptionalCode(discountCode);

        Notes = string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();
    }

    public IReadOnlyList<CheckoutLineRequest> Lines { get; }

    public PaymentMethod PaymentMethod { get; }

    /// <summary>
    /// Chỉ có ý nghĩa khi thanh toán bằng tiền mặt.
    /// Với phương thức khác giá trị phải bằng 0.
    /// </summary>
    public long CashReceived { get; }

    public int? CustomerId { get; }

    public int? RestaurantTableId { get; }

    public string? DiscountCode { get; }

    public string? Notes { get; }

    private static string? NormalizeOptionalCode(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value
                .Trim()
                .ToUpperInvariant();
    }
}