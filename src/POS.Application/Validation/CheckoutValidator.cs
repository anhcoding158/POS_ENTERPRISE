using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Domain.Constants;
using POS.Domain.Enums;
using POS.Domain.Common;
using POS.Domain.Services;

namespace POS.Application.Validation;

/// <summary>
/// Kiểm tra cấu trúc yêu cầu Checkout trước khi mở transaction.
///
/// Các kiểm tra cần database như:
/// - sản phẩm có tồn tại;
/// - sản phẩm còn hoạt động;
/// - giá hiện tại;
/// - tồn kho hiện tại;
/// - số tiền VietQR có khớp tổng đơn thực tế;
///
/// sẽ được CheckoutService thực hiện sau.
/// </summary>
public static class CheckoutValidator
{
    public static Result Validate(
        CheckoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.Lines.Count == 0)
        {
            return Failure(
                ErrorCodes.Checkout.EmptyCart,
                "Giỏ hàng phải có ít nhất một sản phẩm.");
        }

        if (request.Lines.Count >
            BusinessRules.Orders
                .MaximumLinesPerOrder)
        {
            return Failure(
                ErrorCodes.General.Validation,
                $"Đơn hàng không được vượt quá " +
                $"{BusinessRules.Orders.MaximumLinesPerOrder:N0} dòng.");
        }

        if (!Enum.IsDefined(
                request.PaymentMethod))
        {
            return Failure(
                ErrorCodes.Checkout
                    .InvalidPaymentMethod,
                "Phương thức thanh toán không hợp lệ.");
        }

        /*
         * =====================================================
         * PAYMENT CONTRACT
         * =====================================================
         *
         * Cash:
         * - CashReceived chứa tiền khách giao;
         * - ConfirmedPaymentAmount phải bằng 0.
         *
         * VietQR:
         * - CashReceived phải bằng 0;
         * - ConfirmedPaymentAmount phải lớn hơn 0;
         * - CheckoutService sẽ so sánh số này với tổng đơn
         *   được tính lại từ database.
         *
         * Việc QR xuất hiện không đồng nghĩa ngân hàng
         * đã xác nhận giao dịch.
         */
        switch (request.PaymentMethod)
        {
            case PaymentMethod.Cash:

                if (request.PaymentIntentId.HasValue)
                {
                    return Failure(
                        ErrorCodes.General.Validation,
                        "Thanh toán tiền mặt không được có PaymentIntentId.");
                }

                if (request.CashReceived < 0 ||
                    request.CashReceived >
                    BusinessRules.Orders
                        .MaximumOrderAmount)
                {
                    return Failure(
                        ErrorCodes.General.Validation,
                        "Tiền khách đưa không hợp lệ.");
                }

                if (request.ConfirmedPaymentAmount !=
                    0)
                {
                    return Failure(
                        ErrorCodes.General.Validation,
                        "Thanh toán tiền mặt không được gửi " +
                        "số tiền xác nhận không dùng tiền mặt.");
                }

                break;

            case PaymentMethod.VietQr:

                if (request.CashReceived !=
                    0)
                {
                    return Failure(
                        ErrorCodes.General.Validation,
                        "Thanh toán VietQR không được nhập " +
                        "tiền khách đưa.");
                }

                if (request.ConfirmedPaymentAmount <=
                        0 ||
                    request.ConfirmedPaymentAmount >
                    BusinessRules.Orders
                        .MaximumOrderAmount)
                {
                    return Failure(
                        ErrorCodes.Payments.InvalidAmount,
                        "Số tiền VietQR đã xác nhận không hợp lệ.");
                }

                break;

            case PaymentMethod.BankTransfer:
            case PaymentMethod.Card:

                return Failure(
                    ErrorCodes.Checkout
                        .PaymentMethodNotSupported,
                    "Phiên bản hiện tại chỉ hỗ trợ " +
                    "tiền mặt và VietQR.");

            default:

                return Failure(
                    ErrorCodes.Checkout
                        .InvalidPaymentMethod,
                    "Phương thức thanh toán không hợp lệ.");
        }

        if (request.CustomerId.HasValue)
        {
            return Failure(
                ErrorCodes.Checkout
                    .CustomerNotSupported,
                "Chức năng gắn khách hàng sẽ được kích hoạt " +
                "sau khi module khách hàng hoàn thiện.");
        }

        if (request.RestaurantTableId.HasValue)
        {
            return Failure(
                ErrorCodes.Checkout
                    .RestaurantTableNotSupported,
                "Chức năng chọn bàn sẽ được kích hoạt " +
                "sau khi module sơ đồ bàn hoàn thiện.");
        }

        if (request.DiscountCode is not null)
        {
            return Failure(
                ErrorCodes.Checkout
                    .DiscountNotSupported,
                "Mã giảm giá chưa được hỗ trợ " +
                "trong phiên bản Checkout này.");
        }

        try
        {
            if (request.SalesDiscount.Type == SalesDiscountType.None)
            {
                if (request.SalesDiscount.Value != 0 ||
                    request.SalesDiscount.Reason is not null)
                    return Failure("SALES_DISCOUNT.INVALID_NONE", "Không giảm giá có payload không hợp lệ.");
            }
            else
            {
                _ = SalesDiscountCalculator.NormalizeReason(
                    request.SalesDiscount.Type, request.SalesDiscount.Reason);
                if (request.SalesDiscount.Value <= 0 ||
                    request.SalesDiscount.Type == SalesDiscountType.Percentage &&
                    request.SalesDiscount.Value > 10_000)
                    return Failure("SALES_DISCOUNT.VALUE_INVALID", "Giá trị giảm giá không hợp lệ.");
            }
        }
        catch (DomainException exception)
        {
            return Failure(exception.Code, exception.Message);
        }

        if (request.Notes?.Length >
            BusinessRules.Orders
                .NotesMaxLength)
        {
            return Failure(
                ErrorCodes.General.Validation,
                "Ghi chú đơn hàng vượt quá giới hạn.");
        }

        /*
         * HashSet phải nằm ngoài foreach.
         *
         * Nó giữ ProductId của tất cả dòng đã duyệt,
         * nhờ đó dòng thứ hai có cùng ProductId sẽ bị từ chối.
         */
        var productIds =
            new HashSet<int>();

        foreach (var line in
                 request.Lines)
        {
            if (line.ProductId <= 0)
            {
                return Failure(
                    ErrorCodes.Checkout
                        .ProductNotFound,
                    "Mã sản phẩm trong giỏ hàng không hợp lệ.");
            }

            if (!productIds.Add(
                    line.ProductId))
            {
                return Failure(
                    ErrorCodes.Checkout
                        .DuplicateProduct,
                    "Một sản phẩm không được xuất hiện nhiều lần " +
                    "trong cùng giỏ hàng.");
            }

            if (line.Quantity <= 0 ||
                line.Quantity >
                BusinessRules.Orders
                    .MaximumLineQuantity)
            {
                return Failure(
                    ErrorCodes.Checkout
                        .InvalidQuantity,
                    "Số lượng sản phẩm trong giỏ hàng " +
                    "không hợp lệ.");
            }

            /*
             * Không nhận modifier từ giao diện khi catalog
             * modifier chưa được triển khai hoàn chỉnh.
             */
            if (line.Modifiers.Count >
                0)
            {
                return Failure(
                    ErrorCodes.Checkout
                        .ModifiersNotSupported,
                    "Modifier và topping chưa được hỗ trợ " +
                    "trong phiên bản Checkout này.");
            }

            /*
             * Giao diện không được tự gửi số tiền giảm.
             *
             * Sau này giảm giá phải được tính bằng policy
             * và dữ liệu đọc từ database.
             */
            if (line.LineDiscountAmount !=
                0)
            {
                return Failure(
                    ErrorCodes.Checkout
                        .LineDiscountNotSupported,
                    "Giảm giá trực tiếp trên dòng hàng " +
                    "chưa được hỗ trợ.");
            }

            if (line.Notes?.Length >
                BusinessRules.Orders
                    .NotesMaxLength)
            {
                return Failure(
                    ErrorCodes.General.Validation,
                    "Ghi chú dòng hàng vượt quá giới hạn.");
            }
        }

        return Result.Success();
    }

    private static Result Failure(
        string code,
        string message)
    {
        return Result.Failure(
            new AppError(
                code,
                message));
    }
}
