using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

public interface ISecurityAuditRepository
{
    Task AddAsync(
        SecurityAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
