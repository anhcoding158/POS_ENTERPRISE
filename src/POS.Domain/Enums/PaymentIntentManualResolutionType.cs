namespace POS.Domain.Enums;

public enum PaymentIntentManualResolutionType
{
    LinkExistingOrder = 1,
    NoRealMoneyTestTransaction = 2,
    RefundedExternally = 3
}
