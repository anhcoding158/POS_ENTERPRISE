using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

public enum HeldSalePaymentOwnership
{
    Unlocked,
    LockedByCreatedPaymentIntent,
    LockedByPresentedPaymentIntent,
    LockedByConfirmedPaymentIntent,
    Completed,
    Conflict
}

public static class HeldSalePaymentOwnershipPolicy
{
    public const string LockedMessage =
        "Đơn giữ này đang thuộc một giao dịch VietQR chưa hoàn tất. Vui lòng xử lý giao dịch VietQR trước.";

    public static HeldSalePaymentOwnership Evaluate(
        HeldSale heldSale,
        PaymentIntent? activeIntent)
    {
        if (heldSale.Status == HeldSaleStatus.Completed)
            return activeIntent?.Status == PaymentIntentStatus.Confirmed &&
                   heldSale.CompletedOrderId != activeIntent.CompletedOrderId
                ? HeldSalePaymentOwnership.Conflict
                : HeldSalePaymentOwnership.Completed;
        if (heldSale.Status != HeldSaleStatus.Active || activeIntent is null)
            return HeldSalePaymentOwnership.Unlocked;
        return activeIntent.Status switch
        {
            PaymentIntentStatus.Created => HeldSalePaymentOwnership.LockedByCreatedPaymentIntent,
            PaymentIntentStatus.Presented => HeldSalePaymentOwnership.LockedByPresentedPaymentIntent,
            PaymentIntentStatus.Confirmed => HeldSalePaymentOwnership.LockedByConfirmedPaymentIntent,
            _ => HeldSalePaymentOwnership.Unlocked
        };
    }
}
