using POS.Application.Common;

namespace POS.Application.Abstractions.Payments;

/// <summary>
/// Kho lưu payload VietQR nền của cửa hàng.
///
/// Payload được lấy từ ảnh QR ngân hàng do quản lý chọn.
/// Application không phụ thuộc Windows DPAPI hoặc file system.
/// </summary>
public interface IVietQrPayloadStore
{
    /// <summary>
    /// True khi máy hiện tại có payload đã lưu,
    /// giải mã được và còn hợp lệ.
    /// </summary>
    bool IsConfigured
    {
        get;
    }

    /// <summary>
    /// Đọc payload VietQR nền.
    /// </summary>
    Result<string> Load();

    /// <summary>
    /// Lưu hoặc thay thế payload VietQR nền.
    /// </summary>
    Result Save(
        string payload);

    /// <summary>
    /// Xóa cấu hình VietQR trên máy hiện tại.
    /// </summary>
    Result Delete();
}