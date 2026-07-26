using POS.Application.Common;

namespace POS.Application.Abstractions.Payments;

/// <summary>
/// Đọc payload VietQR từ dữ liệu ảnh.
///
/// Interface nằm ở Application để các tầng bên ngoài
/// không phụ thuộc trực tiếp vào ZXing.Net hoặc WPF imaging.
///
/// Decoder chỉ đọc và kiểm tra ảnh.
/// Decoder không:
/// - thay đổi cấu hình;
/// - tạo Order;
/// - xác nhận thanh toán;
/// - gọi ngân hàng;
/// - ghi tài khoản vào database.
/// </summary>
public interface IVietQrImageDecoder
{
    /// <summary>
    /// Giải mã một ảnh PNG, JPEG, BMP hoặc định dạng ảnh
    /// được WPF BitmapDecoder hỗ trợ.
    ///
    /// Kết quả thành công là payload nguyên bản trong QR.
    /// </summary>
    Result<string> DecodePayload(
        byte[] imageBytes);
}