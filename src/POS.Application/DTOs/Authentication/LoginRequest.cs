namespace POS.Application.DTOs.Authentication;

/// <summary>
/// Dữ liệu người dùng nhập khi đăng nhập.
///
/// Không dùng record vì record tự tạo ToString chứa toàn bộ
/// thuộc tính, có thể vô tình đưa mật khẩu vào log.
/// </summary>
public sealed class LoginRequest
{
    public LoginRequest(
        string? username,
        string? password)
    {
        Username = username?.Trim() ?? string.Empty;
        Password = password ?? string.Empty;
    }

    public string Username { get; }

    public string Password { get; }

    /// <summary>
    /// Không bao giờ đưa Password vào chuỗi log.
    /// </summary>
    public override string ToString()
    {
        return $"LoginRequest {{ Username = {Username} }}";
    }
}