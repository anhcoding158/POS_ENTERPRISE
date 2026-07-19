namespace POS.Application.Abstractions.Authentication;

/// <summary>
/// Mã hóa và kiểm tra mật khẩu.
///
/// Application chỉ biết interface này và không phụ thuộc BCrypt.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Tạo password hash từ mật khẩu dạng rõ.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Kiểm tra mật khẩu dạng rõ với password hash đã lưu.
    /// </summary>
    bool VerifyPassword(
        string password,
        string passwordHash);
}