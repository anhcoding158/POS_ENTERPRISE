using POS.Application.Common;
using POS.Application.DTOs.Authentication;

namespace POS.Application.Abstractions.Authentication;

/// <summary>
/// Các ca sử dụng đăng nhập và đăng xuất.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthenticatedUserDto>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Result Logout();
}