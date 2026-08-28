using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Domain.Enums;

namespace POS.Application.Services;

public sealed class RolePermissionManagementService : IRolePermissionManagementService
{
    private readonly IUserRepository _users;
    private readonly IPermissionService _permissions;

    public RolePermissionManagementService(IUserRepository users, IPermissionService permissions)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public async Task<Result<IReadOnlyList<RolePermissionSnapshot>>> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var authorization = _permissions.Authorize(SystemCapability.AssignRolesPermissions);
        if (authorization.IsFailure)
            return Result.Failure<IReadOnlyList<RolePermissionSnapshot>>(authorization.AppError);

        var snapshots = new List<RolePermissionSnapshot>();
        foreach (var role in Enum.GetValues<Role>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var allowed = RolePermissionPolicy.GetEffectivePermissions(role)
                .Select(PermissionCatalog.Get)
                .ToArray();
            var denied = PermissionCatalog.All
                .Where(permission => !RolePermissionPolicy.HasPermission(role, permission.Capability))
                .ToArray();
            snapshots.Add(new RolePermissionSnapshot(
                role,
                RolePermissionPolicy.GetRoleDisplayName(role),
                IsBuiltIn: true,
                IsProtected: role == Role.Administrator,
                await _users.CountByRoleAsync(role, cancellationToken),
                allowed,
                denied));
        }

        return Result.Success<IReadOnlyList<RolePermissionSnapshot>>(snapshots);
    }
}
