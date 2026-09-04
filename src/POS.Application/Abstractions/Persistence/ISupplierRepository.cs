using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Abstractions.Persistence;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default);

    Task<Supplier?> GetByIdReadOnlyAsync(int supplierId, CancellationToken cancellationToken = default);

    Task<PagedResult<Supplier>> SearchAsync(
        string? searchTerm,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> NormalizedCodeExistsAsync(
        string normalizedCode,
        int? excludeSupplierId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
}
