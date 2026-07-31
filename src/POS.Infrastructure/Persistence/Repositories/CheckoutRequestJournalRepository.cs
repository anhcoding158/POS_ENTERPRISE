using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class CheckoutRequestJournalRepository(PosDbContext dbContext) :
    ICheckoutRequestJournalRepository
{
    public async Task AddAsync(CheckoutRequestJournal journal, CancellationToken cancellationToken = default) =>
        await dbContext.CheckoutRequestJournals.AddAsync(journal, cancellationToken);

    public Task<CheckoutRequestJournal?> GetTrackedAsync(Guid clientRequestId, CancellationToken cancellationToken = default) =>
        dbContext.CheckoutRequestJournals.SingleOrDefaultAsync(
            journal => journal.ClientRequestId == clientRequestId, cancellationToken);

    public Task ReloadTrackedAsync(
        CheckoutRequestJournal journal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(journal);
        return dbContext.Entry(journal).ReloadAsync(cancellationToken);
    }

    public Task<CheckoutRequestJournal?> GetReadOnlyAsync(Guid clientRequestId, CancellationToken cancellationToken = default) =>
        dbContext.CheckoutRequestJournals.AsNoTracking().SingleOrDefaultAsync(
            journal => journal.ClientRequestId == clientRequestId, cancellationToken);

    public Task<CheckoutRequestJournal?> GetByOrderIdReadOnlyAsync(int orderId, CancellationToken cancellationToken = default) =>
        dbContext.CheckoutRequestJournals.AsNoTracking().SingleOrDefaultAsync(
            journal => journal.OrderId == orderId, cancellationToken);

    public async Task<IReadOnlyList<CheckoutRequestJournal>> GetActiveRecoveryAsync(
        int preparedByUserId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(preparedByUserId);
        if (limit is <= 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit));

        return await dbContext.CheckoutRequestJournals
            .AsNoTracking()
            .Include(journal => journal.Order)
            .Where(journal => journal.PreparedByUserId == preparedByUserId &&
                (journal.Status == CheckoutRequestStatus.Prepared ||
                 journal.Status == CheckoutRequestStatus.Completed && journal.AcknowledgedAtUtc == null))
            .OrderByDescending(journal => journal.CreatedAtUtc)
            .ThenByDescending(journal => journal.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }
}
