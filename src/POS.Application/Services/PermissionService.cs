using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Authorization;
using POS.Application.Common;

namespace POS.Application.Services;

/// <summary>
/// Kiểm tra quyền dựa trên phiên người dùng hiện tại.
///
/// Service không phụ thuộc WPF hoặc EF Core.
/// </summary>
public sealed class PermissionService :
    IPermissionService
{
    private readonly ICurrentUserService
        _currentUserService;

    public PermissionService(
        ICurrentUserService currentUserService)
    {
        _currentUserService =
            currentUserService ??
            throw new ArgumentNullException(
                nameof(currentUserService));
    }

    public bool HasPermission(
        SystemPermission permission)
    {
        ValidatePermission(
            permission);

        if (!_currentUserService
            .IsAuthenticated)
        {
            return false;
        }

        var role =
            _currentUserService.Role;

        if (!role.HasValue)
        {
            return false;
        }

        return RolePermissionPolicy
            .HasPermission(
                role.Value,
                permission);
    }

    public Result Authorize(
        SystemPermission permission)
    {
        ValidatePermission(
            permission);

        if (!_currentUserService
            .IsAuthenticated)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.General.Unauthorized,
                    "Phiên đăng nhập không còn hợp lệ. " +
                    "Vui lòng đăng nhập lại."));
        }

        var role =
            _currentUserService.Role;

        if (!role.HasValue)
        {
            return Result.Failure(
                new Error(
                    ErrorCodes.General.Unauthorized,
                    "Phiên đăng nhập thiếu thông tin vai trò. " +
                    "Vui lòng đăng nhập lại."));
        }

        if (RolePermissionPolicy.HasPermission(
                role.Value,
                permission))
        {
            return Result.Success();
        }

        var permissionDisplayName =
            RolePermissionPolicy
                .GetDisplayName(
                    permission);

        return Result.Failure(
            new Error(
                ErrorCodes.General.Forbidden,
                $"Tài khoản hiện tại không có quyền " +
                $"{permissionDisplayName}."));
    }

    private static void ValidatePermission(
        SystemPermission permission)
    {
        if (!Enum.IsDefined(
                permission))
        {
            throw new ArgumentOutOfRangeException(
                nameof(permission),
                permission,
                "Quyền hệ thống không hợp lệ.");
        }
    }
}