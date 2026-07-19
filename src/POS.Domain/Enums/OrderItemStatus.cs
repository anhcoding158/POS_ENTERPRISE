namespace POS.Domain.Enums;

/// <summary>
/// Trạng thái của một dòng hàng trong đơn.
/// </summary>
public enum OrderItemStatus
{
    Active = 1,

    Cancelled = 2,

    PartiallyRefunded = 3,

    Refunded = 4
}