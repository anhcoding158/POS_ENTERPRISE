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
        string? notes = null,
        long confirmedPaymentAmount = 0)
    {
        Lines =
            lines?
                .ToArray()
            ??
            Array.Empty<
                CheckoutLineRequest>();

        PaymentMethod =
            paymentMethod;

        CashReceived =
            cashReceived;

        ConfirmedPaymentAmount =
            confirmedPaymentAmount;

        CustomerId =
            customerId;

        RestaurantTableId =
            restaurantTableId;

        DiscountCode =
            NormalizeOptionalCode(
                discountCode);

        Notes =
            string.IsNullOrWhiteSpace(
                notes)
                ? null
                : notes.Trim();
    }

    public IReadOnlyList<
        CheckoutLineRequest> Lines
    {
        get;
    }

    public PaymentMethod PaymentMethod
    {
        get;
    }

    /// <summary>
    /// Tiền khách giao trực tiếp cho thu ngân.
    ///
    /// Chỉ có ý nghĩa khi thanh toán bằng tiền mặt.
    /// Với phương thức khác, giá trị phải bằng 0.
    /// </summary>
    public long CashReceived
    {
        get;
    }

    /// <summary>
    /// Số tiền không dùng tiền mặt mà thu ngân đã xác nhận
    /// cửa hàng thực sự nhận được.
    ///
    /// Quy tắc hiện tại:
    ///
    /// - Cash:
    ///   phải bằng 0 vì tiền mặt sử dụng CashReceived.
    ///
    /// - VietQr:
    ///   phải lớn hơn 0 và phải bằng chính xác tổng đơn
    ///   được CheckoutService tính lại từ database.
    ///
    /// Giá trị này không chứng minh tiền đã về ngân hàng.
    /// Presentation chỉ được gửi nó sau khi thu ngân thực hiện
    /// bước xác nhận thủ công theo quy trình cửa hàng.
    /// </summary>
    public long ConfirmedPaymentAmount
    {
        get;
    }

    public int? CustomerId
    {
        get;
    }

    public int? RestaurantTableId
    {
        get;
    }

    public string? DiscountCode
    {
        get;
    }

    public string? Notes
    {
        get;
    }

    private static string?
        NormalizeOptionalCode(
            string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value
                .Trim()
                .ToUpperInvariant();
    }
}