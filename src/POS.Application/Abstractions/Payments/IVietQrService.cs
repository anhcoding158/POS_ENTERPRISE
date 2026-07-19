using POS.Application.Common;
using POS.Application.DTOs.Payments;

namespace POS.Application.Abstractions.Payments;

/// <summary>
/// Tạo payload và ảnh VietQR.
///
/// Implementation thực tế nằm trong Infrastructure.
/// Application không phụ thuộc QRCoder.
/// </summary>
public interface IVietQrService
{
    /// <summary>
    /// Tạo chuỗi payload theo chuẩn VietQR/EMVCo.
    /// </summary>
    Result<string> BuildPayload(
        VietQrRequest request);

    /// <summary>
    /// Tạo ảnh QR dạng PNG.
    ///
    /// Kết quả trả về là mảng byte để WPF có thể hiển thị
    /// hoặc đưa vào tài liệu in.
    /// </summary>
    Result<byte[]> GeneratePng(
        VietQrRequest request);
}