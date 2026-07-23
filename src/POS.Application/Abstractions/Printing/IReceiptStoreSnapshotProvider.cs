using POS.Application.DTOs.Printing;

namespace POS.Application.Abstractions.Printing;

/// <summary>
/// Cung cấp snapshot bất biến của thông tin cửa hàng
/// tại thời điểm tạo hóa đơn.
///
/// Application chỉ biết contract này và không phụ thuộc
/// IConfiguration, IOptions hoặc appsettings.json.
/// </summary>
public interface IReceiptStoreSnapshotProvider
{
    /// <summary>
    /// Trả về thông tin cửa hàng đã được chuẩn hóa
    /// và kiểm tra hợp lệ.
    ///
    /// Snapshot không được chứa secret thanh toán,
    /// mật khẩu Wi-Fi hoặc dữ liệu nhạy cảm khác.
    /// </summary>
    ReceiptStoreSnapshotDto GetCurrentSnapshot();
}