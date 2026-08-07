using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class OrderReturnRepository(PosDbContext dbContext) :
    IOrderReturnRepository
{
    public async Task AddAsync(OrderReturn orderReturn, CancellationToken cancellationToken = default) =>
        await dbContext.OrderReturns.AddAsync(orderReturn, cancellationToken);

    public Task<OrderReturn?> GetByIdReadOnlyAsync(int id, CancellationToken cancellationToken = default) =>
        ReadQuery().SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public Task<OrderReturn?> GetByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken = default) =>
        ReadQuery().SingleOrDefaultAsync(entity => entity.ClientRequestId == clientRequestId, cancellationToken);

    public async Task<IReadOnlyList<OrderReturn>> GetByOrderIdReadOnlyAsync(
        int orderId,
        CancellationToken cancellationToken = default) =>
        await ReadQuery()
            .Where(entity => entity.OrderId == orderId)
            .OrderBy(entity => entity.CreatedAtUtc)
            .ThenBy(entity => entity.Id)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, OrderReturnBalance>> GetBalancesForOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default) =>
        await dbContext.OrderReturnBalances
            .AsNoTracking()
            .Where(balance => balance.OrderItem!.OrderId == orderId)
            .ToDictionaryAsync(balance => balance.OrderItemId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, OrderReturnBalance>> GetBalancesForOrdersAsync(
        IReadOnlyCollection<int> orderIds,
        CancellationToken cancellationToken = default) =>
        await dbContext.OrderReturnBalances.AsNoTracking()
            .Where(balance => orderIds.Contains(balance.OrderItem!.OrderId))
            .ToDictionaryAsync(balance => balance.OrderItemId, cancellationToken);

    public async Task<OrderReturnBalance> GetOrCreateTrackedBalanceAsync(
        int orderItemId,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.OrderReturnBalances
            .SingleOrDefaultAsync(entity => entity.OrderItemId == orderItemId, cancellationToken);
        if (existing is not null)
            return existing;

        var balance = new OrderReturnBalance(orderItemId);
        await dbContext.OrderReturnBalances.AddAsync(balance, cancellationToken);
        return balance;
    }

    private IQueryable<OrderReturn> ReadQuery() =>
        dbContext.OrderReturns.AsNoTracking()
            .AsSplitQuery()
            .Include(entity => entity.ProcessedByUser)
            .Include(entity => entity.Items);
}
