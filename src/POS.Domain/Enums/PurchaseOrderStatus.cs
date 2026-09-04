namespace POS.Domain.Enums;

/// <summary>
/// Trạng thái vòng đời của Purchase Order.
/// Các giá trị đã lưu là append-only.
/// </summary>
public enum PurchaseOrderStatus
{
    Draft = 1,
    Ordered = 2,
    Cancelled = 3
}
