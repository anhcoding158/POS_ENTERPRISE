namespace POS.Domain.Enums;

/// <summary>
/// Trạng thái vòng đời của đơn hàng.
/// </summary>
public enum OrderStatus
{
    Draft = 1,

    PendingPayment = 2,

    Paid = 3,

    Completed = 4,

    Cancelled = 5,

    PartiallyRefunded = 6,

    Refunded = 7
}