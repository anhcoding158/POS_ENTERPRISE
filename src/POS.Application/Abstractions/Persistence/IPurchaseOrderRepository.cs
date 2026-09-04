using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Abstractions.Persistence;

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(
        int purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrder?> GetByIdReadOnlyAsync(
        int purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PurchaseOrder>> SearchAsync(
        string? searchTerm,
        PurchaseOrderStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> NormalizedOrderNumberExistsAsync(
        string normalizedOrderNumber,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default);
}
