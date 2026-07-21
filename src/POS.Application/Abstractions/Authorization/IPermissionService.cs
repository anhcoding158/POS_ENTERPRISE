using POS.Application.Authorization;
using POS.Application.Common;

namespace POS.Application.Abstractions.Authorization;

/// <summary>
/// Kiểm tra quyền của người dùng đang đăng nhập.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Trả về true khi phiên hiện tại có quyền.
    ///
    /// Phiên chưa đăng nhập luôn trả về false.
    /// </summary>
    bool HasPermission(
        SystemPermission permission);

    /// <summary>
    /// Trả về:
    /// - Unauthorized khi chưa đăng nhập;
    /// - Forbidden khi đã đăng nhập nhưng không có quyền;
    /// - Success khi được phép.
    /// </summary>
    Result Authorize(
        SystemPermission permission);
}