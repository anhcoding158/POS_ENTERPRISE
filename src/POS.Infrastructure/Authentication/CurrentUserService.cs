using POS.Application.Abstractions.Authentication;
using POS.Application.DTOs.Authentication;
using POS.Domain.Enums;

namespace POS.Infrastructure.Authentication;

/// <summary>
/// Lưu phiên đăng nhập hiện tại trong bộ nhớ.
///
/// Service được đăng ký Singleton để một phiên đăng nhập
/// được dùng chung trong toàn bộ ứng dụng WPF.
/// </summary>
public sealed class CurrentUserService :
    ICurrentUserService
{
    private readonly object _syncRoot = new();

    private AuthenticatedUserDto?
        _currentUser;

    public AuthenticatedUserDto? CurrentUser
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentUser;
            }
        }
    }

    public bool IsAuthenticated =>
        CurrentUser is not null;

    public int? UserId =>
        CurrentUser?.Id;

    public string? Username =>
        CurrentUser?.Username;

    public string? FullName =>
        CurrentUser?.FullName;

    public Role? Role =>
        CurrentUser?.Role;

    public bool IsInRole(
        Role role)
    {
        if (!Enum.IsDefined(role))
        {
            return false;
        }

        return CurrentUser?.Role ==
               role;
    }

    public void SetCurrentUser(
        AuthenticatedUserDto user)
    {
        ArgumentNullException.ThrowIfNull(
            user);

        lock (_syncRoot)
        {
            _currentUser = user;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _currentUser = null;
        }
    }
}