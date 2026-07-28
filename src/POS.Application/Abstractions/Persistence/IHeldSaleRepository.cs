using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

public interface IHeldSaleRepository
{
    Task AddAsync(HeldSale heldSale, CancellationToken cancellationToken = default);
    Task<HeldSale?> GetByClientRequestIdAsync(Guid clientRequestId, bool tracked,
        CancellationToken cancellationToken = default);
    Task<HeldSale?> GetByIdAsync(int heldSaleId, bool tracked,
        CancellationToken cancellationToken = default);
    Task ReloadTrackedAsync(HeldSale heldSale, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HeldSale>> GetActiveAsync(int createdByUserId, int limit,
        CancellationToken cancellationToken = default);
}
