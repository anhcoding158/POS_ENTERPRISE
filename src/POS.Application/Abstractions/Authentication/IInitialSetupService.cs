using POS.Application.Common;
using POS.Application.DTOs.Authentication;

namespace POS.Application.Abstractions.Authentication;

/// <summary>
/// Thiết lập bảo mật trong lần chạy đầu tiên.
///
/// Chỉ được phép tạo Administrator khi database
/// chưa tồn tại bất kỳ tài khoản nào.
/// </summary>
public interface IInitialSetupService
{
    /// <summary>
    /// Trả về true khi database chưa có tài khoản
    /// và cần mở màn hình thiết lập ban đầu.
    /// </summary>
    Task<Result<bool>> IsSetupRequiredAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo Administrator đầu tiên và thiết lập
    /// phiên đăng nhập hiện tại.
    /// </summary>
    Task<Result<AuthenticatedUserDto>>
        CreateInitialAdministratorAsync(
            InitialAdministratorRequest request,
            CancellationToken cancellationToken = default);
}