namespace POS.Application.DTOs.Authentication;

/// <summary>
/// Dữ liệu dùng để khôi phục phiên đăng nhập trên
/// chính tài khoản Windows đã tạo credential.
///
/// Không chứa mật khẩu hoặc password hash gốc.
/// </summary>
public sealed record RememberedLoginCredential(
    int Version,
    int UserId,
    string PasswordHashFingerprint,
    DateTimeOffset ExpiresAtUtc)
{
    public const int CurrentVersion = 1;
}