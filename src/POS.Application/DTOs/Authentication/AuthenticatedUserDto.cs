using POS.Domain.Enums;

namespace POS.Application.DTOs.Authentication;

/// <summary>
/// Thông tin phiên đăng nhập hiện tại.
///
/// DTO này tuyệt đối không chứa password hash.
/// </summary>
public sealed record AuthenticatedUserDto
{
    public AuthenticatedUserDto(
        int id,
        string username,
        string fullName,
        Role role,
        DateTimeOffset authenticatedAtUtc)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                "Mã người dùng phải lớn hơn 0.");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException(
                "Tên đăng nhập không được để trống.",
                nameof(username));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException(
                "Họ tên người dùng không được để trống.",
                nameof(fullName));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role),
                "Vai trò người dùng không hợp lệ.");
        }

        if (authenticatedAtUtc == default)
        {
            throw new ArgumentException(
                "Thời điểm đăng nhập không hợp lệ.",
                nameof(authenticatedAtUtc));
        }

        Id = id;
        Username = username.Trim();
        FullName = fullName.Trim();
        Role = role;
        AuthenticatedAtUtc =
            authenticatedAtUtc.ToUniversalTime();
    }

    public int Id { get; }

    public string Username { get; }

    public string FullName { get; }

    public Role Role { get; }

    public DateTimeOffset AuthenticatedAtUtc { get; }
}