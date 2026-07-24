using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Application.Validation;

/// <summary>
/// Kiểm tra cấu trúc yêu cầu Checkout trước khi mở transaction.
///
/// Các kiểm tra cần database như:
/// - sản phẩm có tồn tại;
/// - sản phẩm còn hoạt động;
/// - giá hiện tại;
/// - tồn kho hiện tại;
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
            BusinessRules.Orders.MaximumLinesPerOrder)
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
                ErrorCodes.Checkout.InvalidPaymentMethod,
                "Phương thức thanh toán không hợp lệ.");
        }

        /*
         * Checkout hiện hỗ trợ hai phương thức:
         *
         * 1. Cash:
         *    - tiền khách đưa phải không âm;
         *    - không vượt giới hạn giá trị đơn hàng;
         *    - CheckoutService và Domain sẽ kiểm tra
         *      số tiền có đủ thanh toán hay không.
         *
         * 2. VietQr:
         *    - chỉ được gửi sau quy trình xác nhận thủ công
         *      tại Presentation;
         *    - CashReceived bắt buộc bằng 0;
         *    - Domain sẽ lưu ChangeAmount bằng 0.
         *
         * Việc hiển thị QR không phải xác nhận tự động
         * từ ngân hàng. Validator chỉ kiểm tra đúng cấu trúc
         * request sau khi thu ngân đã thực hiện bước xác nhận.
         */
        switch (request.PaymentMethod)
        {
            case PaymentMethod.Cash:

                if (request.CashReceived < 0 ||
                    request.CashReceived >
                    BusinessRules.Orders
                        .MaximumOrderAmount)
                {
                    return Failure(
                        ErrorCodes.General.Validation,
                        "Tiền khách đưa không hợp lệ.");
                }

                break;

            case PaymentMethod.VietQr:

                if (request.CashReceived != 0)
                {
                    return Failure(
                        ErrorCodes.General.Validation,
                        "Thanh toán VietQR không được nhập " +
                        "tiền khách đưa.");
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
                ErrorCodes.Checkout.CustomerNotSupported,
                "Chức năng gắn khách hàng sẽ được kích hoạt " +
                "sau khi module khách hàng hoàn thiện.");
        }

        if (request.RestaurantTableId.HasValue)
        {
            return Failure(
                ErrorCodes.Checkout.RestaurantTableNotSupported,
                "Chức năng chọn bàn sẽ được kích hoạt " +
                "sau khi module sơ đồ bàn hoàn thiện.");
        }

        if (request.DiscountCode is not null)
        {
            return Failure(
                ErrorCodes.Checkout.DiscountNotSupported,
                "Mã giảm giá chưa được hỗ trợ trong phiên bản Checkout này.");
        }

        if (request.Notes?.Length >
            BusinessRules.Orders.NotesMaxLength)
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

        foreach (var line in request.Lines)
        {
            if (line.ProductId <= 0)
            {
                return Failure(
                    ErrorCodes.Checkout.ProductNotFound,
                    "Mã sản phẩm trong giỏ hàng không hợp lệ.");
            }

            if (!productIds.Add(
                    line.ProductId))
            {
                return Failure(
                    ErrorCodes.Checkout.DuplicateProduct,
                    "Một sản phẩm không được xuất hiện nhiều lần " +
                    "trong cùng giỏ hàng.");
            }

            if (line.Quantity <= 0 ||
                line.Quantity >
                BusinessRules.Orders.MaximumLineQuantity)
            {
                return Failure(
                    ErrorCodes.Checkout.InvalidQuantity,
                    "Số lượng sản phẩm trong giỏ hàng không hợp lệ.");
            }

            /*
             * Không nhận modifier từ giao diện khi catalog
             * modifier chưa được triển khai hoàn chỉnh.
             */
            if (line.Modifiers.Count > 0)
            {
                return Failure(
                    ErrorCodes.Checkout.ModifiersNotSupported,
                    "Modifier và topping chưa được hỗ trợ " +
                    "trong phiên bản Checkout này.");
            }

            /*
             * Giao diện không được tự gửi số tiền giảm.
             *
             * Sau này giảm giá phải được tính bằng policy
             * và dữ liệu đọc từ database.
             */
            if (line.LineDiscountAmount != 0)
            {
                return Failure(
                    ErrorCodes.Checkout.LineDiscountNotSupported,
                    "Giảm giá trực tiếp trên dòng hàng chưa được hỗ trợ.");
            }

            if (line.Notes?.Length >
                BusinessRules.Orders.NotesMaxLength)
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
            new Error(
                code,
                message));
    }
}