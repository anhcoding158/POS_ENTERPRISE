using POS.Domain.Common;
using POS.Domain.Constants;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Tài khoản đăng nhập và phân quyền người dùng.
/// Chỉ lưu password hash, tuyệt đối không lưu mật khẩu gốc.
/// </summary>
public sealed class User : AuditableEntity
{
    private User()
    {
    }

    public User(
        string username,
        string passwordHash,
        string fullName,
        Role role,
        DateTimeOffset utcNow)
    {
        SetUsername(username);
        SetPasswordHash(passwordHash);
        SetFullName(fullName);

        Role = ValidateRole(role);
        IsActive = true;

        MarkCreated(utcNow);
    }

    public string Username { get; private set; } = string.Empty;

    /// <summary>
    /// Username chuẩn hóa để tìm kiếm và tạo unique index.
    /// </summary>
    public string NormalizedUsername { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public Role Role { get; private set; }

    public bool IsActive { get; private set; }

    public int FailedLoginAttempts { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public bool IsLocked(DateTimeOffset utcNow)
    {
        return LockedUntilUtc.HasValue &&
               LockedUntilUtc.Value >
               utcNow.ToUniversalTime();
    }

    public void ChangeUsername(
        string username,
        DateTimeOffset utcNow)
    {
        SetUsername(username);
        MarkUpdated(utcNow);
    }

    public void ChangePasswordHash(
        string passwordHash,
        DateTimeOffset utcNow)
    {
        SetPasswordHash(passwordHash);

        FailedLoginAttempts = 0;
        LockedUntilUtc = null;

        MarkUpdated(utcNow);
    }

    public void UpdateProfile(
        string fullName,
        Role role,
        DateTimeOffset utcNow)
    {
        SetFullName(fullName);
        Role = ValidateRole(role);

        MarkUpdated(utcNow);
    }

    public void RegisterSuccessfulLogin(
        DateTimeOffset utcNow)
    {
        var normalizedUtc = utcNow.ToUniversalTime();

        LastLoginAtUtc = normalizedUtc;
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;

        MarkUpdated(normalizedUtc);
    }

    public void RegisterFailedLogin(
        DateTimeOffset utcNow,
        TimeSpan lockDuration)
    {
        if (lockDuration <= TimeSpan.Zero)
        {
            throw new DomainException(
                "USER.INVALID_LOCK_DURATION",
                "Thời gian khóa tài khoản phải lớn hơn 0.");
        }

        if (FailedLoginAttempts < int.MaxValue)
        {
            FailedLoginAttempts++;
        }

        var normalizedUtc = utcNow.ToUniversalTime();

        if (FailedLoginAttempts >=
            BusinessRules.Users.FailedLoginLimit)
        {
            LockedUntilUtc =
                normalizedUtc.Add(lockDuration);
        }

        MarkUpdated(normalizedUtc);
    }

    public void Unlock(DateTimeOffset utcNow)
    {
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;

        MarkUpdated(utcNow);
    }

    public void Activate(DateTimeOffset utcNow)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        FailedLoginAttempts = 0;
        LockedUntilUtc = null;

        MarkUpdated(utcNow);
    }

    public void Deactivate(DateTimeOffset utcNow)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;

        MarkUpdated(utcNow);
    }

    private void SetUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException(
                "USER.USERNAME_REQUIRED",
                "Tên đăng nhập không được để trống.");
        }

        var trimmed = username.Trim();

        if (trimmed.Length <
                BusinessRules.Users.UsernameMinLength ||
            trimmed.Length >
                BusinessRules.Users.UsernameMaxLength)
        {
            throw new DomainException(
                "USER.INVALID_USERNAME_LENGTH",
                $"Tên đăng nhập phải có từ " +
                $"{BusinessRules.Users.UsernameMinLength} đến " +
                $"{BusinessRules.Users.UsernameMaxLength} ký tự.");
        }

        if (!trimmed.All(IsAllowedUsernameCharacter))
        {
            throw new DomainException(
                "USER.INVALID_USERNAME",
                "Tên đăng nhập chỉ được chứa chữ, số, dấu chấm, " +
                "gạch dưới hoặc gạch ngang.");
        }

        Username = trimmed;
        NormalizedUsername =
            trimmed.ToUpperInvariant();
    }

    private void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException(
                "USER.PASSWORD_HASH_REQUIRED",
                "Password hash không được để trống.");
        }

        var trimmed = passwordHash.Trim();

        if (trimmed.Length >
            BusinessRules.Users.PasswordHashMaxLength)
        {
            throw new DomainException(
                "USER.PASSWORD_HASH_TOO_LONG",
                "Password hash vượt quá giới hạn cho phép.");
        }

        PasswordHash = trimmed;
    }

    private void SetFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException(
                "USER.FULL_NAME_REQUIRED",
                "Họ tên người dùng không được để trống.");
        }

        var trimmed = fullName.Trim();

        if (trimmed.Length >
            BusinessRules.Users.FullNameMaxLength)
        {
            throw new DomainException(
                "USER.FULL_NAME_TOO_LONG",
                "Họ tên người dùng vượt quá giới hạn cho phép.");
        }

        FullName = trimmed;
    }

    private static Role ValidateRole(Role role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException(
                "USER.INVALID_ROLE",
                "Vai trò người dùng không hợp lệ.");
        }

        return role;
    }

    private static bool IsAllowedUsernameCharacter(char character)
    {
        return char.IsLetterOrDigit(character) ||
               character is '.' or '_' or '-';
    }
}