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
    /// productId null:
    ///     mở lịch sử của toàn bộ sản phẩm.
    ///
    /// productId có giá trị:
    ///     ưu tiên lọc theo sản phẩm đó.
    /// </summary>
    Task ShowHistoryAsync(
        int? productId = null);
}