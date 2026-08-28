using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Persistence;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class RolePermissionManagementTests
{
    [Fact]
    public void Built_in_roles_have_stable_vietnamese_labels_and_unique_permissions()
    {
        Assert.Equal(4, Enum.GetValues<Role>().Length);
        Assert.Equal("Quản trị viên", RolePermissionPolicy.GetRoleDisplayName(Role.Administrator));
        Assert.Equal("Quản lý", RolePermissionPolicy.GetRoleDisplayName(Role.Manager));
        Assert.Equal("Thu ngân", RolePermissionPolicy.GetRoleDisplayName(Role.Cashier));
        Assert.Equal("Nhân viên kho", RolePermissionPolicy.GetRoleDisplayName(Role.InventoryStaff));
        Assert.Equal(PermissionCatalog.All.Count, PermissionCatalog.All.Select(item => item.Capability).Distinct().Count());
        Assert.Contains(PermissionCatalog.All, item => item.Capability == SystemCapability.ViewAuditLog);
    }

    [Fact]
    public void Only_administrator_has_audit_and_role_administration_permissions()
    {
        Assert.True(RolePermissionPolicy.HasPermission(Role.Administrator, SystemCapability.AssignRolesPermissions));
        Assert.True(RolePermissionPolicy.HasPermission(Role.Administrator, SystemCapability.ViewAuditLog));
        Assert.All(new[] { Role.Manager, Role.Cashier, Role.InventoryStaff }, role =>
        {
            Assert.False(RolePermissionPolicy.HasPermission(role, SystemCapability.AssignRolesPermissions));
            Assert.False(RolePermissionPolicy.HasPermission(role, SystemCapability.ViewAuditLog));
        });
    }

    [Fact]
    public async Task Role_snapshot_uses_database_role_usage_and_denies_unknown_access()
    {
        var repository = new FakeUserRepository();
        var service = new RolePermissionManagementService(repository, new FakePermissionService(true));
        var result = await service.GetSnapshotAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Count);
        Assert.Equal(3, result.Value.Single(item => item.Role == Role.Administrator).AccountUsageCount);
        Assert.Contains(result.Value.Single(item => item.Role == Role.Cashier).DeniedPermissions,
            permission => permission.Capability == SystemCapability.ViewAuditLog);

        var denied = await new RolePermissionManagementService(repository, new FakePermissionService(false)).GetSnapshotAsync();
        Assert.True(denied.IsFailure);
    }

    private sealed class FakePermissionService(bool allowed) : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) => allowed;
        public Result Authorize(SystemCapability permission) => allowed
            ? Result.Success()
            : Result.Failure(new AppError(ErrorCodes.General.Forbidden, "Không có quyền."));
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<PagedResult<User>> SearchAsync(string? searchTerm, Role? role, bool? isActive, int pageNumber, int pageSize, CancellationToken cancellationToken = default) => Task.FromResult(PagedResult.Empty<User>(pageNumber, pageSize));
        public Task<bool> NormalizedUsernameExistsAsync(string normalizedUsername, int? excludeUserId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<int> CountByRoleAsync(Role role, CancellationToken cancellationToken = default) => Task.FromResult(role == Role.Administrator ? 3 : role == Role.Manager ? 2 : 1);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
