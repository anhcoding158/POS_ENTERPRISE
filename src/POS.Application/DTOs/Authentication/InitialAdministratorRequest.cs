namespace POS.Application.DTOs.Authentication;

/// <summary>
/// Dữ liệu tạo quản trị viên đầu tiên.
///
/// Không dùng record để tránh Password xuất hiện
/// trong ToString hoặc log ngoài ý muốn.
/// </summary>
public sealed class InitialAdministratorRequest
{
    public InitialAdministratorRequest(
        string? username,
        string? fullName,
        string? password,
        string? confirmPassword)
    {
        Username =
            username?.Trim() ??
            string.Empty;

        FullName =
            fullName?.Trim() ??
            string.Empty;

        Password =
            password ??
            string.Empty;

        ConfirmPassword =
            confirmPassword ??
            string.Empty;
    }

    public string Username { get; }

    public string FullName { get; }

    public string Password { get; }

    public string ConfirmPassword { get; }

    public override string ToString()
    {
        return
            $"InitialAdministratorRequest " +
            $"{{ Username = {Username}, " +
            $"FullName = {FullName} }}";
    }
}