namespace POS.Application.DTOs.Inventory;

/// <summary>
/// Thống kê số lượt biến động trên toàn bộ tập kết quả sau khi áp dụng
/// cùng điều kiện với truy vấn lịch sử.
/// </summary>
public sealed record InventoryMovementSummaryDto(
    int TotalCount,
    int IncreaseCount,
    int DecreaseCount,
    int NeutralCount);
