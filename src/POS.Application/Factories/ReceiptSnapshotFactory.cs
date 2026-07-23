using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Printing;
using POS.Domain.Enums;

namespace POS.Application.Factories;

/// <summary>
/// Chuyển kết quả checkout đã hoàn tất thành snapshot hóa đơn
/// bất biến.
///
/// Factory chỉ sử dụng dữ liệu đã được CheckoutService chốt.
/// Không đọc lại Product, Modifier, Order hoặc giá bán đang sống.
/// </summary>
public static class ReceiptSnapshotFactory
{
    /// <summary>
    /// Overload tương thích với call site cũ.
    ///
    /// Snapshot được đánh dấu chưa cấu hình cửa hàng.
    /// Call site production phải dùng overload có store.
    /// </summary>
    public static ReceiptRequest Create(
        CheckoutResultDto checkoutResult,
        string? receiptNotes = null)
    {
        return Create(
            checkoutResult:
                checkoutResult,

            store:
                ReceiptStoreSnapshotDto.Unconfigured,

            receiptNotes:
                receiptNotes,

            copyKind:
                ReceiptCopyKind.Original,

            copyNumber:
                0);
    }

    /// <summary>
    /// Tạo snapshot production có đầy đủ thông tin cửa hàng
    /// và trạng thái bản gốc/bản in lại.
    /// </summary>
    public static ReceiptRequest Create(
        CheckoutResultDto checkoutResult,
        ReceiptStoreSnapshotDto store,
        string? receiptNotes = null,
        ReceiptCopyKind copyKind =
            ReceiptCopyKind.Original,
        int copyNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(
            checkoutResult);

        ArgumentNullException.ThrowIfNull(
            store);

        if (checkoutResult.Status !=
            OrderStatus.Completed)
        {
            throw new ArgumentException(
                "Chỉ kết quả checkout đã hoàn tất mới được " +
                "chuyển thành snapshot hóa đơn.",
                nameof(checkoutResult));
        }

        if (checkoutResult.Lines is null)
        {
            throw new ArgumentException(
                "Kết quả checkout không có danh sách dòng hàng.",
                nameof(checkoutResult));
        }

        var receiptLines =
            checkoutResult.Lines
                .Select(
                    MapLine)
                .ToArray();

        return new ReceiptRequest(
            store:
                store,

            copyKind:
                copyKind,

            copyNumber:
                copyNumber,

            orderId:
                checkoutResult.OrderId,

            orderCode:
                checkoutResult.OrderCode,

            cashierName:
                checkoutResult.CashierName,

            createdAtUtc:
                checkoutResult.CreatedAtUtc,

            paymentMethod:
                checkoutResult.PaymentMethod,

            subtotal:
                checkoutResult.Subtotal,

            discountAmount:
                checkoutResult.DiscountAmount,

            totalAmount:
                checkoutResult.TotalAmount,

            cashReceived:
                checkoutResult.CashReceived,

            changeAmount:
                checkoutResult.ChangeAmount,

            lines:
                receiptLines,

            customerName:
                checkoutResult.CustomerName,

            restaurantTableName:
                checkoutResult.RestaurantTableName,

            discountCode:
                checkoutResult.DiscountCode,

            notes:
                receiptNotes,

            paidAtUtc:
                checkoutResult.PaidAtUtc);
    }

    private static ReceiptLineDto MapLine(
        CheckoutLineResultDto checkoutLine)
    {
        ArgumentNullException.ThrowIfNull(
            checkoutLine);

        if (checkoutLine.Modifiers is null)
        {
            throw new ArgumentException(
                "Dòng checkout không có danh sách modifier.",
                nameof(checkoutLine));
        }

        var receiptModifiers =
            checkoutLine.Modifiers
                .Select(
                    MapModifier)
                .ToArray();

        return new ReceiptLineDto(
            orderItemId:
                checkoutLine.OrderItemId,

            productId:
                checkoutLine.ProductId,

            productCode:
                checkoutLine.ProductCode,

            productName:
                checkoutLine.ProductName,

            unitName:
                checkoutLine.UnitName,

            quantity:
                checkoutLine.Quantity,

            unitSalePrice:
                checkoutLine.UnitSalePrice,

            modifierAmountPerUnit:
                checkoutLine.ModifierAmountPerUnit,

            finalUnitPrice:
                checkoutLine.FinalUnitPrice,

            grossAmount:
                checkoutLine.GrossAmount,

            lineDiscountAmount:
                checkoutLine.LineDiscountAmount,

            netAmount:
                checkoutLine.NetAmount,

            notes:
                checkoutLine.Notes,

            modifiers:
                receiptModifiers);
    }

    private static ReceiptModifierDto MapModifier(
        CheckoutLineModifierResultDto checkoutModifier)
    {
        ArgumentNullException.ThrowIfNull(
            checkoutModifier);

        return new ReceiptModifierDto(
            modifierId:
                checkoutModifier.ModifierId,

            modifierGroupId:
                checkoutModifier.ModifierGroupId,

            modifierGroupName:
                checkoutModifier.ModifierGroupName,

            name:
                checkoutModifier.ModifierName,

            quantity:
                checkoutModifier.Quantity,

            unitAdditionalPrice:
                checkoutModifier.UnitAdditionalPrice,

            amountPerProductUnit:
                checkoutModifier.AmountPerProductUnit);
    }
}