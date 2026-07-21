using POS.Application.DTOs.Authentication;

namespace POS.Application.Abstractions.Authentication;

/// <summary>
/// Kho lưu credential đăng nhập được bảo vệ bởi hệ điều hành.
///
/// Application không phụ thuộc trực tiếp Windows DPAPI,
/// JSON hoặc đường dẫn file.
/// </summary>
public interface IRememberedLoginStore
{
    /// <summary>
    /// Đọc credential hiện tại.
    ///
    /// Trả về null khi:
    /// - chưa từng ghi nhớ đăng nhập;
    /// - file bị hỏng;
    /// - file không thể giải mã;
    /// - dữ liệu không đúng phiên bản.
    /// </summary>
    RememberedLoginCredential? Load();

    /// <summary>
    /// Lưu hoặc thay thế credential hiện tại.
    /// </summary>
    bool TrySave(
        RememberedLoginCredential credential);

    /// <summary>
    /// Xóa credential của máy hiện tại.
    ///
    /// Trả về true khi file không tồn tại hoặc đã xóa thành công.
    /// </summary>
    bool TryDelete();
}