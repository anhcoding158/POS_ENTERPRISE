using POS.Application.Common;
using POS.Application.DTOs.Audit;

namespace POS.Application.Abstractions.Persistence;

public interface ISecurityAuditQueryRepository
{
    Task<PagedResult<AuditListItemDto>> SearchAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<AuditDetailsDto?> GetDetailsAsync(
        int auditId,
        CancellationToken cancellationToken = default);
}
