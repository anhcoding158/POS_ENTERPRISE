namespace POS.Wpf.Services;

/// <summary>
/// Mở các cửa sổ nghiệp vụ tồn kho.
///
/// ViewModel màn hình chính không trực tiếp tạo Window.
/// </summary>
public interface IInventoryDialogService
{
    /// <summary>
    /// Mở cửa sổ điều chỉnh kho cho một sản phẩm.
    ///
    /// Trả true khi biến động đã được commit thành công.
    /// </summary>
    Task<bool> ShowAdjustmentAsync(
        int productId);

    /// <summary>
    /// Mở lịch sử kho.
    ///
    /// productSearchTerm null:
    ///     mở lịch sử của toàn bộ sản phẩm.
    ///
    /// productSearchTerm có giá trị:
    ///     mở với một tiêu chí sản phẩm hiển thị ngay trong ô tìm kiếm.
    /// </summary>
    Task ShowHistoryAsync(
        string? productSearchTerm = null,
        string? productDisplayText = null);
}
