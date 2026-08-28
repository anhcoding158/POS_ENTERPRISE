using POS.Application.Common;
using POS.Application.DTOs.Audit;

namespace POS.Application.Abstractions.Services;

public interface IAuditLogService
{
    Task<Result<PagedResult<AuditListItemDto>>> SearchAsync(
        AuditSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AuditDetailsDto>> GetDetailsAsync(
        int auditId,
        CancellationToken cancellationToken = default);
}
