using POS.Application.Common;
using POS.Application.DTOs.Checkout;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Application.Validation;

/// <summary>
/// Kiểm tra cấu trúc yêu cầu Checkout trước khi mở transaction.
///
/// Các kiểm tra cần database như sản phẩm tồn tại, trạng thái bán,
/// giá và tồn kho được CheckoutService thực hiện sau.
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
         * 8B chỉ được phép xác nhận tiền mặt.
         *
         * VietQR/Card/BankTransfer cần quy trình xác nhận
         * thanh toán thật ở chặng 8D.
         */
        if (request.PaymentMethod !=
            PaymentMethod.Cash)
        {
            return Failure(
                ErrorCodes.Checkout.PaymentMethodNotSupported,
                "Phiên bản hiện tại chỉ hỗ trợ thanh toán tiền mặt.");
        }

        if (request.CashReceived < 0 ||
            request.CashReceived >
            BusinessRules.Orders.MaximumOrderAmount)
        {
            return Failure(
                ErrorCodes.General.Validation,
                "Tiền khách đưa không hợp lệ.");
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

        foreach (var line in request.Lines)
        {
            if (line.ProductId <= 0)
            {
                return Failure(
                    ErrorCodes.Checkout.ProductNotFound,
                    "Mã sản phẩm trong giỏ hàng không hợp lệ.");
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
             * Không nhận giá modifier từ giao diện khi catalog
             * modifier chưa được đưa vào database.
             */
            if (line.Modifiers.Count > 0)
            {
                return Failure(
                    ErrorCodes.Checkout.ModifiersNotSupported,
                    "Modifier và topping chưa được hỗ trợ " +
                    "trong phiên bản Checkout này.");
            }

            /*
             * Không cho WPF tự gửi số tiền giảm.
             * Sau này discount phải được tính từ policy/database.
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