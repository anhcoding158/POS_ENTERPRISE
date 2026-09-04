using POS.Application.Common;
using POS.Application.DTOs.Suppliers;

namespace POS.Application.Abstractions.Services;

public interface ISupplierService
{
    Task<Result<PagedResult<SupplierListItemDto>>> SearchAsync(
        SupplierSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SupplierDetailsDto>> GetByIdAsync(
        int supplierId,
        CancellationToken cancellationToken = default);

    Task<Result<SupplierDetailsDto>> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SupplierDetailsDto>> UpdateAsync(
        UpdateSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> SetActiveStateAsync(
        SetSupplierActiveStateRequest request,
        CancellationToken cancellationToken = default);
}
