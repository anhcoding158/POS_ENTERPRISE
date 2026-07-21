namespace POS.Application.DTOs.Authentication;

/// <summary>
/// Dữ liệu người dùng nhập khi đăng nhập.
///
/// Không dùng record vì record tự tạo ToString chứa
/// toàn bộ thuộc tính, có thể làm lộ mật khẩu trong log.
/// </summary>
public sealed class LoginRequest
{
    public LoginRequest(
        string? username,
        string? password,
        bool rememberLogin = false)
    {
        Username =
            username?.Trim() ??
            string.Empty;

        Password =
            password ??
            string.Empty;

        RememberLogin =
            rememberLogin;
    }

    public string Username { get; }

    public string Password { get; }

    /// <summary>
    /// Ghi nhớ phiên trên tài khoản Windows hiện tại
    /// trong tối đa 30 ngày.
    /// </summary>
    public bool RememberLogin { get; }

    /// <summary>
    /// Không bao giờ đưa Password vào chuỗi log.
    /// </summary>
    public override string ToString()
    {
        return
            $"LoginRequest " +
            $"{{ Username = {Username}, " +
            $"RememberLogin = {RememberLogin} }}";
    }
}