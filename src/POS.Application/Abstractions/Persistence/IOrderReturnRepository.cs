using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

public interface IOrderReturnRepository
{
    Task AddAsync(OrderReturn orderReturn, CancellationToken cancellationToken = default);
    Task<OrderReturn?> GetByIdReadOnlyAsync(int id, CancellationToken cancellationToken = default);
    Task<OrderReturn?> GetByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderReturn>> GetByOrderIdReadOnlyAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, OrderReturnBalance>> GetBalancesForOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, OrderReturnBalance>> GetBalancesForOrdersAsync(IReadOnlyCollection<int> orderIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<int, OrderReturnBalance>>(new Dictionary<int, OrderReturnBalance>());
    Task<OrderReturnBalance> GetOrCreateTrackedBalanceAsync(int orderItemId, CancellationToken cancellationToken = default);
}
