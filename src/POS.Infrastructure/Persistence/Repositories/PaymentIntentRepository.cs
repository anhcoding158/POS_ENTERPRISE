using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class PaymentIntentRepository(PosDbContext dbContext) : IPaymentIntentRepository
{
    public async Task AddAsync(PaymentIntent intent, CancellationToken cancellationToken = default) =>
        await dbContext.PaymentIntents.AddAsync(intent, cancellationToken);

    public Task<PaymentIntent?> GetByIdAsync(int id, bool tracked, CancellationToken cancellationToken = default) =>
        Query(tracked).SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public Task<PaymentIntent?> GetByClientRequestIdAsync(Guid clientRequestId, bool tracked, CancellationToken cancellationToken = default) =>
        Query(tracked).SingleOrDefaultAsync(value => value.ClientRequestId == clientRequestId, cancellationToken);

    public Task<PaymentIntent?> GetByCompletedOrderIdAsync(
        int orderId, CancellationToken cancellationToken = default) =>
        dbContext.PaymentIntents.AsNoTracking()
            .SingleOrDefaultAsync(value => value.CompletedOrderId == orderId, cancellationToken);

    public Task<PaymentIntent?> GetActiveByHeldSaleIdAsync(
        int heldSaleId, bool tracked, CancellationToken cancellationToken = default) =>
        Query(tracked).SingleOrDefaultAsync(value =>
            value.HeldSaleId == heldSaleId &&
            !dbContext.PaymentIntentManualResolutions.Any(
                resolution => resolution.PaymentIntentId == value.Id) &&
            (value.Status == PaymentIntentStatus.Created ||
             value.Status == PaymentIntentStatus.Presented ||
             value.Status == PaymentIntentStatus.Confirmed),
            cancellationToken);

    public Task ReloadTrackedAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return dbContext.Entry(intent).ReloadAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentIntent>> GetPendingAsync(
        int createdByUserId, int limit, CancellationToken cancellationToken = default) =>
        await dbContext.PaymentIntents.AsNoTracking()
            .Where(value => value.CreatedByUserId == createdByUserId &&
                !dbContext.PaymentIntentManualResolutions.Any(
                    resolution => resolution.PaymentIntentId == value.Id) &&
                (value.Status == PaymentIntentStatus.Created ||
                 value.Status == PaymentIntentStatus.Presented ||
                 value.Status == PaymentIntentStatus.Confirmed))
            .OrderByDescending(value => value.UpdatedAtUtc)
            .ThenByDescending(value => value.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

    public Task<PaymentIntentManualResolution?> GetResolutionAsync(
        int paymentIntentId, CancellationToken cancellationToken = default) =>
        dbContext.PaymentIntentManualResolutions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PaymentIntentId == paymentIntentId, cancellationToken);

    public async Task AddResolutionAsync(
        PaymentIntentManualResolution resolution, CancellationToken cancellationToken = default) =>
        await dbContext.PaymentIntentManualResolutions.AddAsync(resolution, cancellationToken);

    public async Task<IReadOnlyList<PaymentIntentManualResolution>> GetResolutionHistoryAsync(
        int limit, CancellationToken cancellationToken = default) =>
        await dbContext.PaymentIntentManualResolutions.AsNoTracking()
            .OrderByDescending(x => x.ResolvedAtUtc).ThenByDescending(x => x.Id)
            .Take(Math.Clamp(limit, 1, 100)).ToArrayAsync(cancellationToken);

    private IQueryable<PaymentIntent> Query(bool tracked) =>
        tracked ? dbContext.PaymentIntents : dbContext.PaymentIntents.AsNoTracking();
}
