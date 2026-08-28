using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Persistence;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Audit;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class SecureAuditLogTests
{
    [Fact]
    public void Audit_change_set_is_allowlisted_and_rejects_credential_like_fields()
    {
        var json = SecurityAuditChangeSet.Serialize([
            new SecurityAuditChange("Vai trò", "Thu ngân", "Quản lý")]);
        var roundTrip = SecurityAuditChangeSet.Deserialize(json);
        Assert.Single(roundTrip);
        Assert.Equal("Vai trò", roundTrip[0].FieldKey);
        Assert.Equal("Thu ngân", roundTrip[0].BeforeValue);
        Assert.Equal("Quản lý", roundTrip[0].AfterValue);
        Assert.Throws<DomainException>(() => SecurityAuditChangeSet.Serialize([
            new SecurityAuditChange("PasswordHash", null, "not persisted")]));
        Assert.Throws<DomainException>(() => SecurityAuditChangeSet.Serialize([
            new SecurityAuditChange("Ghi chú", null, "temporary password") ]));
    }

    [Fact]
    public void Audit_event_has_safe_metadata_and_no_credential_surface()
    {
        var audit = new SecurityAuditEvent(1, 2, 3, SecurityAuditAction.RoleChanged, "Success", Guid.NewGuid(), DateTimeOffset.UtcNow,
            "Quản trị viên", "Nhân viên kho", "Nhân viên và tài khoản", "Tài khoản", "TERM-ISOLATED",
            [new SecurityAuditChange("Vai trò", "Thu ngân", "Quản lý")]);
        Assert.Equal("TERM-ISOLATED", audit.TerminalId);
        Assert.Equal("Quản trị viên", audit.ActorDisplayNameSnapshot);
        Assert.DoesNotContain("password", audit.BeforeValuesJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", audit.BeforeValuesJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", audit.BeforeValuesJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Audit_query_service_requires_typed_view_permission()
    {
        var repository = new FakeAuditQueryRepository();
        var allowed = new AuditLogService(repository, new FakePermissionService(true));
        var result = await allowed.SearchAsync(new AuditSearchRequest());
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal("Quản trị viên", result.Value.Items[0].Actor);

        var denied = await new AuditLogService(repository, new FakePermissionService(false)).SearchAsync(new AuditSearchRequest());
        Assert.True(denied.IsFailure);
    }

    private sealed class FakePermissionService(bool allowed) : IPermissionService
    {
        public bool HasPermission(SystemCapability permission) => allowed;
        public Result Authorize(SystemCapability permission) => allowed
            ? Result.Success()
            : Result.Failure(new AppError(ErrorCodes.General.Forbidden, "Không có quyền."));
    }

    private sealed class FakeAuditQueryRepository : ISecurityAuditQueryRepository
    {
        public Task<PagedResult<AuditListItemDto>> SearchAsync(AuditSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<AuditListItemDto>([
                new AuditListItemDto(1, DateTimeOffset.UtcNow, "Quản trị viên", SecurityAuditAction.RoleChanged, "Nhân viên và tài khoản", "Nhân viên kho", "Success", "TERM-ISOLATED", Guid.NewGuid())
            ], 1, 25, 1));

        public Task<AuditDetailsDto?> GetDetailsAsync(int auditId, CancellationToken cancellationToken = default) => Task.FromResult<AuditDetailsDto?>(null);
    }
}
