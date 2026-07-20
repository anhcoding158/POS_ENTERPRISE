namespace POS.Domain.Enums;

/// <summary>
/// Loại biến động tồn kho.
///
/// Giá trị số được giữ ổn định vì được lưu trực tiếp
/// trong database.
/// </summary>
public enum InventoryMovementType
{
    Unknown = 0,

    /// <summary>
    /// Nhập kho thủ công từ nhà cung cấp hoặc nguồn khác.
    /// QuantityDelta phải lớn hơn 0.
    /// </summary>
    StockIn = 1,

    /// <summary>
    /// Xuất kho thủ công do hỏng, hao hụt hoặc sử dụng nội bộ.
    /// QuantityDelta phải nhỏ hơn 0.
    /// </summary>
    StockOut = 2,

    /// <summary>
    /// Điều chỉnh tăng hoặc giảm tồn kho.
    /// QuantityDelta được phép dương hoặc âm nhưng không bằng 0.
    /// </summary>
    Adjustment = 3,

    /// <summary>
    /// Kiểm kê và chốt tồn kho thực tế.
    /// Có thể tạo biến động bằng 0 để lưu dấu lần kiểm kê.
    /// </summary>
    Stocktake = 4,

    /// <summary>
    /// Xuất kho do bán hàng.
    /// </summary>
    Sale = 5,

    /// <summary>
    /// Nhập lại kho do hoàn hàng.
    /// </summary>
    Refund = 6,

    /// <summary>
    /// Tồn đầu kỳ khi tạo hoặc chuyển đổi dữ liệu sản phẩm.
    /// </summary>
    OpeningBalance = 7
}