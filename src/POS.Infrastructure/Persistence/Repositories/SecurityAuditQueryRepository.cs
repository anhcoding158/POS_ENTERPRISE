using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Audit;
using POS.Application.DTOs.Products;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;

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
        if (request.Action.HasValue)
        {
            var action = request.Action.Value;
            if (action == SecurityAuditAction.EmployeeUpdated)
                query = query.Where(audit => audit.Action == action && !(audit.TargetEmployeeId == null && audit.TargetUserId == null && audit.BusinessArea == AuditPresentationResolver.LegacyBulkBusinessArea && audit.TargetType == AuditPresentationResolver.LegacyBulkTargetType && audit.TargetDisplayNameSnapshot.StartsWith("Batch ") && audit.BeforeValuesJson.Contains("\"FieldKey\":\"operation\"")));
            else if (action is >= SecurityAuditAction.BulkProductPricesUpdated and <= SecurityAuditAction.BulkProductOperation)
            {
                var operation = action switch
                {
                    SecurityAuditAction.BulkProductPricesUpdated => nameof(BulkProductOperationType.SetPrices),
                    SecurityAuditAction.BulkProductCategoryChanged => nameof(BulkProductOperationType.SetCategory),
                    SecurityAuditAction.BulkProductActiveStateChanged => nameof(BulkProductOperationType.SetActiveState),
                    SecurityAuditAction.BulkProductMinimumStockChanged => nameof(BulkProductOperationType.SetMinimumStock),
                    _ => string.Empty
                };
                query = action == SecurityAuditAction.BulkProductOperation
                    ? query.Where(audit => audit.Action == action || (audit.Action == SecurityAuditAction.EmployeeUpdated && audit.TargetEmployeeId == null && audit.TargetUserId == null && audit.BusinessArea == AuditPresentationResolver.LegacyBulkBusinessArea && audit.TargetType == AuditPresentationResolver.LegacyBulkTargetType && audit.TargetDisplayNameSnapshot.StartsWith("Batch ") && audit.BeforeValuesJson.Contains("\"FieldKey\":\"operation\"") && !audit.BeforeValuesJson.Contains("\"AfterValue\":\"SetPrices\"") && !audit.BeforeValuesJson.Contains("\"AfterValue\":\"SetCategory\"") && !audit.BeforeValuesJson.Contains("\"AfterValue\":\"SetActiveState\"") && !audit.BeforeValuesJson.Contains("\"AfterValue\":\"SetMinimumStock\"")))
                    : query.Where(audit => audit.Action == action || (audit.Action == SecurityAuditAction.EmployeeUpdated && audit.TargetEmployeeId == null && audit.TargetUserId == null && audit.BusinessArea == AuditPresentationResolver.LegacyBulkBusinessArea && audit.TargetType == AuditPresentationResolver.LegacyBulkTargetType && audit.TargetDisplayNameSnapshot.StartsWith("Batch ") && audit.BeforeValuesJson.Contains("\"FieldKey\":\"operation\"") && audit.BeforeValuesJson.Contains($"\"AfterValue\":\"{operation}\"")));
            }
            else
                query = query.Where(audit => audit.Action == action);
        }
        if (!string.IsNullOrWhiteSpace(request.Result)) query = query.Where(audit => audit.Result == request.Result.Trim());
        if (!string.IsNullOrWhiteSpace(request.BusinessArea))
        {
            var businessArea = request.BusinessArea.Trim();
            query = businessArea == AuditPresentationResolver.BulkBusinessArea
                ? query.Where(audit => audit.BusinessArea == businessArea || audit.BusinessArea == AuditPresentationResolver.LegacyBulkBusinessArea)
                : query.Where(audit => audit.BusinessArea == businessArea);
        }
        if (!string.IsNullOrWhiteSpace(request.Actor)) query = query.Where(audit => audit.ActorDisplayNameSnapshot.Contains(request.Actor.Trim()));
        if (!string.IsNullOrWhiteSpace(request.Target)) query = query.Where(audit => audit.TargetDisplayNameSnapshot.Contains(request.Target.Trim()));
        return query;
    }

    private static AuditListItemDto MapList(SecurityAuditEvent audit) =>
        new(audit.Id, audit.CreatedAtUtc, Actor(audit), AuditPresentationResolver.ResolveAction(audit),
            AuditPresentationResolver.ResolveBusinessArea(audit), AuditPresentationResolver.ResolveTarget(audit), audit.Result,
            string.IsNullOrWhiteSpace(audit.TerminalId) ? "Không xác định" : audit.TerminalId,
            audit.OperationId) { TechnicalTarget = AuditPresentationResolver.TechnicalTarget(audit) };

    private static AuditDetailsDto MapDetails(SecurityAuditEvent audit) =>
        new(audit.Id, audit.CreatedAtUtc, Actor(audit), AuditPresentationResolver.ResolveAction(audit),
            AuditPresentationResolver.ResolveBusinessArea(audit), AuditPresentationResolver.ResolveTarget(audit), audit.Result,
            string.IsNullOrWhiteSpace(audit.TerminalId) ? "Không xác định" : audit.TerminalId,
            audit.OperationId,
            SecurityAuditChangeSet.Deserialize(audit.BeforeValuesJson).Select(change => new AuditChangeDto(change.FieldKey, change.BeforeValue, change.AfterValue)).ToArray())
        {
            TargetType = AuditPresentationResolver.ResolveTargetType(audit),
            TechnicalTarget = AuditPresentationResolver.TechnicalTarget(audit)
        };

    private static string Actor(SecurityAuditEvent audit) =>
        string.IsNullOrWhiteSpace(audit.ActorDisplayNameSnapshot) ? (audit.ActorUserId.HasValue ? $"Người dùng #{audit.ActorUserId}" : "Không xác định") : audit.ActorDisplayNameSnapshot;

    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0 || pageSize <= 0 || pageSize > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Phân trang nhật ký không hợp lệ.");
    }
}
