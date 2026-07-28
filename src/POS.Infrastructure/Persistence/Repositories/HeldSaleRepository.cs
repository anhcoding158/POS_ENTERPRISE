using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class HeldSaleRepository(PosDbContext dbContext) : IHeldSaleRepository
{
    public async Task AddAsync(HeldSale heldSale, CancellationToken cancellationToken = default) =>
        await dbContext.HeldSales.AddAsync(heldSale, cancellationToken);

    public Task<HeldSale?> GetByClientRequestIdAsync(
        Guid clientRequestId, bool tracked, CancellationToken cancellationToken = default) =>
        Query(tracked).Include(value => value.Lines).Include(value => value.CreatedByUser)
            .SingleOrDefaultAsync(value => value.ClientRequestId == clientRequestId, cancellationToken);

    public Task<HeldSale?> GetByIdAsync(
        int heldSaleId, bool tracked, CancellationToken cancellationToken = default) =>
        Query(tracked).Include(value => value.Lines).Include(value => value.CreatedByUser)
            .SingleOrDefaultAsync(value => value.Id == heldSaleId, cancellationToken);

    public Task ReloadTrackedAsync(HeldSale heldSale, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(heldSale);
        return dbContext.Entry(heldSale).ReloadAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HeldSale>> GetActiveAsync(
        int createdByUserId, int limit, CancellationToken cancellationToken = default) =>
        await dbContext.HeldSales.AsNoTracking()
            .Include(value => value.Lines)
            .Include(value => value.CreatedByUser)
            .Where(value => value.CreatedByUserId == createdByUserId &&
                value.Status == HeldSaleStatus.Active)
            .OrderByDescending(value => value.UpdatedAtUtc)
            .ThenByDescending(value => value.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

    private IQueryable<HeldSale> Query(bool tracked) =>
        tracked ? dbContext.HeldSales : dbContext.HeldSales.AsNoTracking();
}
