using System.Text;

namespace POS.Application.Authentication;

public sealed record PasswordPolicyValidation(
    bool IsValid,
    bool HasMinimumLength,
    bool HasUppercase,
    bool HasLowercase,
    bool HasDigit,
    bool HasSpecialCharacter,
    bool HasNoWhitespace,
    bool DoesNotContainUsername,
    string ErrorMessage);

public static class PasswordPolicy
{
    public const int MinimumLength = 10;
    public const int MaximumUtf8Bytes = 72;

    public static PasswordPolicyValidation Validate(
        string? password,
        string? username = null)
    {
        var value = password ?? string.Empty;
        var hasMinimumLength = value.Length >= MinimumLength;
        var hasUppercase = value.Any(char.IsUpper);
        var hasLowercase = value.Any(char.IsLower);
        var hasDigit = value.Any(char.IsDigit);
        var hasSpecialCharacter = value.Any(character => !char.IsLetterOrDigit(character));
        var hasNoWhitespace = !value.Any(char.IsWhiteSpace);
        var doesNotContainUsername = string.IsNullOrWhiteSpace(username) ||
            !value.Contains(username.Trim(), StringComparison.OrdinalIgnoreCase);
        var bytesWithinLimit = Encoding.UTF8.GetByteCount(value) <= MaximumUtf8Bytes;

        var message = !hasMinimumLength
            ? $"Mật khẩu phải có ít nhất {MinimumLength} ký tự."
            : !bytesWithinLimit
                ? $"Mật khẩu không được vượt quá {MaximumUtf8Bytes} byte UTF-8."
                : !hasNoWhitespace
                    ? "Mật khẩu không được chứa khoảng trắng."
                    : !hasUppercase
                        ? "Mật khẩu phải có ít nhất một chữ hoa."
                        : !hasLowercase
                            ? "Mật khẩu phải có ít nhất một chữ thường."
                            : !hasDigit
                                ? "Mật khẩu phải có ít nhất một chữ số."
                                : !hasSpecialCharacter
                                    ? "Mật khẩu phải có ít nhất một ký tự đặc biệt."
                                    : !doesNotContainUsername
                                        ? "Mật khẩu không được chứa tên đăng nhập."
                                        : string.Empty;

        return new PasswordPolicyValidation(
            message.Length == 0,
            hasMinimumLength,
            hasUppercase,
            hasLowercase,
            hasDigit,
            hasSpecialCharacter,
            hasNoWhitespace,
            doesNotContainUsername,
            message);
    }
}
