using POS.Application.DTOs.Authentication;
using POS.Domain.Enums;

namespace POS.Application.Abstractions.Authentication;

/// <summary>
/// Quản lý phiên người dùng hiện đang đăng nhập.
///
/// Interface không phụ thuộc WPF nên có thể dùng trong
/// Application service và kiểm thử.
/// </summary>
public interface ICurrentUserService
{
    AuthenticatedUserDto? CurrentUser { get; }

    bool IsAuthenticated { get; }

    int? UserId { get; }

    string? Username { get; }

    string? FullName { get; }

    Role? Role { get; }

    bool IsInRole(Role role);

    void SetCurrentUser(
        AuthenticatedUserDto user);

    void Clear();
}