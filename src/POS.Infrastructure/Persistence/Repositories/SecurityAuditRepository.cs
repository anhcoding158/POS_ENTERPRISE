using POS.Application.Abstractions.Persistence;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class SecurityAuditRepository : ISecurityAuditRepository
{
    private readonly PosDbContext _dbContext;

    public SecurityAuditRepository(PosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task AddAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        return _dbContext.SecurityAuditEvents.AddAsync(auditEvent, cancellationToken).AsTask();
    }
}
