using POS.Application.Common;
using POS.Application.DTOs.Authentication;

namespace POS.Application.Abstractions.Authentication;

/// <summary>
/// Các ca sử dụng đăng nhập, khôi phục phiên và đăng xuất.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthenticatedUserDto>>
        LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Thử khôi phục phiên đăng nhập đã được Windows bảo vệ.
    ///
    /// Value:
    /// - true: đã khôi phục và thiết lập CurrentUser;
    /// - false: không có credential hợp lệ, giao diện mở Login.
    /// </summary>
    Task<Result<bool>>
        TryRestoreRememberedLoginAsync(
            CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa session trong RAM và credential ghi nhớ
    /// của máy hiện tại.
    /// </summary>
    Result Logout();
}