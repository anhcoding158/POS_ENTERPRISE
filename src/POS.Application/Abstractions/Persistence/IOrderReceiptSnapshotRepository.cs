using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

/// <summary>
/// Persists the immutable original receipt snapshot for an order.
/// </summary>
public interface IOrderReceiptSnapshotRepository
{
    Task AddAsync(
        OrderReceiptSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<OrderReceiptSnapshot?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default);
}
