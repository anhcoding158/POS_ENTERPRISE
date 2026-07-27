using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core persistence cho bản chụp hóa đơn gốc bất biến.
/// </summary>
public sealed class OrderReceiptSnapshotRepository :
    IOrderReceiptSnapshotRepository
{
    private readonly PosDbContext
        _dbContext;

    public OrderReceiptSnapshotRepository(
        PosDbContext dbContext)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    public async Task AddAsync(
        OrderReceiptSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        await _dbContext.OrderReceiptSnapshots
            .AddAsync(
                snapshot,
                cancellationToken);
    }

    public Task<OrderReceiptSnapshot?>
        GetByOrderIdAsync(
            int orderId,
            CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return Task.FromResult<
                OrderReceiptSnapshot?>(
                    null);
        }

        return _dbContext.OrderReceiptSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(
                snapshot =>
                    snapshot.OrderId == orderId,
                cancellationToken);
    }
}
