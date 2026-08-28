using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Audit;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class SecurityAuditQueryRepository : ISecurityAuditQueryRepository
{
    private readonly PosDbContext _dbContext;

    public SecurityAuditQueryRepository(PosDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<PagedResult<AuditListItemDto>> SearchAsync(AuditSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.PageNumber, request.PageSize);
        var query = ApplyFilters(_dbContext.SecurityAuditEvents.AsNoTracking(), request);
        var total = await query.CountAsync(cancellationToken);
        var entities = await query.OrderByDescending(audit => audit.CreatedAtUtc).ThenByDescending(audit => audit.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToArrayAsync(cancellationToken);
        return new PagedResult<AuditListItemDto>(entities.Select(MapList).ToArray(), request.PageNumber, request.PageSize, total);
    }

    public async Task<AuditDetailsDto?> GetDetailsAsync(int auditId, CancellationToken cancellationToken = default)
    {
        if (auditId <= 0) return null;
        var audit = await _dbContext.SecurityAuditEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == auditId, cancellationToken);
        return audit is null ? null : MapDetails(audit);
    }

    private static IQueryable<SecurityAuditEvent> ApplyFilters(IQueryable<SecurityAuditEvent> query, AuditSearchRequest request)
    {
        if (request.FromUtc.HasValue) query = query.Where(audit => audit.CreatedAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) query = query.Where(audit => audit.CreatedAtUtc <= request.ToUtc.Value);
        if (request.Action.HasValue) query = query.Where(audit => audit.Action == request.Action.Value);
        if (!string.IsNullOrWhiteSpace(request.Result)) query = query.Where(audit => audit.Result == request.Result.Trim());
        if (!string.IsNullOrWhiteSpace(request.BusinessArea)) query = query.Where(audit => audit.BusinessArea == request.BusinessArea.Trim());
        if (!string.IsNullOrWhiteSpace(request.Actor)) query = query.Where(audit => audit.ActorDisplayNameSnapshot.Contains(request.Actor.Trim()));
        if (!string.IsNullOrWhiteSpace(request.Target)) query = query.Where(audit => audit.TargetDisplayNameSnapshot.Contains(request.Target.Trim()));
        return query;
    }

    private static AuditListItemDto MapList(SecurityAuditEvent audit) =>
        new(audit.Id, audit.CreatedAtUtc, Actor(audit), audit.Action,
            string.IsNullOrWhiteSpace(audit.BusinessArea) ? "Nhân viên và tài khoản" : audit.BusinessArea,
            Target(audit), audit.Result,
            string.IsNullOrWhiteSpace(audit.TerminalId) ? "Không xác định" : audit.TerminalId,
            audit.OperationId);

    private static AuditDetailsDto MapDetails(SecurityAuditEvent audit) =>
        new(audit.Id, audit.CreatedAtUtc, Actor(audit), audit.Action,
            string.IsNullOrWhiteSpace(audit.BusinessArea) ? "Nhân viên và tài khoản" : audit.BusinessArea,
            Target(audit), audit.Result,
            string.IsNullOrWhiteSpace(audit.TerminalId) ? "Không xác định" : audit.TerminalId,
            audit.OperationId,
            SecurityAuditChangeSet.Deserialize(audit.BeforeValuesJson).Select(change => new AuditChangeDto(change.FieldKey, change.BeforeValue, change.AfterValue)).ToArray())
        {
            TargetType = string.IsNullOrWhiteSpace(audit.TargetType) ? "Không xác định" : audit.TargetType
        };

    private static string Actor(SecurityAuditEvent audit) =>
        string.IsNullOrWhiteSpace(audit.ActorDisplayNameSnapshot) ? (audit.ActorUserId.HasValue ? $"Người dùng #{audit.ActorUserId}" : "Không xác định") : audit.ActorDisplayNameSnapshot;

    private static string Target(SecurityAuditEvent audit) =>
        string.IsNullOrWhiteSpace(audit.TargetDisplayNameSnapshot) ? (audit.TargetEmployeeId.HasValue ? $"Nhân viên #{audit.TargetEmployeeId}" : audit.TargetUserId.HasValue ? $"Tài khoản #{audit.TargetUserId}" : "Không xác định") : audit.TargetDisplayNameSnapshot;

    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0 || pageSize <= 0 || pageSize > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Phân trang nhật ký không hợp lệ.");
    }
}
