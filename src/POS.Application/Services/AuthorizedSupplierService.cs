using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Suppliers;

namespace POS.Application.Services;

public sealed class AuthorizedSupplierService : ISupplierService
{
    private readonly ISupplierService _innerService;
    private readonly IPermissionService _permissionService;

    public AuthorizedSupplierService(ISupplierService innerService, IPermissionService permissionService)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public Task<Result<PagedResult<SupplierListItemDto>>> SearchAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default) =>
        Read(() => _innerService.SearchAsync(request, cancellationToken));

    public Task<Result<SupplierDetailsDto>> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default) =>
        Read(() => _innerService.GetByIdAsync(supplierId, cancellationToken));

    public Task<Result<SupplierDetailsDto>> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default) =>
        Write(() => _innerService.CreateAsync(request, cancellationToken));

    public Task<Result<SupplierDetailsDto>> UpdateAsync(UpdateSupplierRequest request, CancellationToken cancellationToken = default) =>
        Write(() => _innerService.UpdateAsync(request, cancellationToken));

    public Task<Result> SetActiveStateAsync(SetSupplierActiveStateRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = _permissionService.Authorize(SystemCapability.ManageSuppliers);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure(authorization.AppError))
            : _innerService.SetActiveStateAsync(request, cancellationToken);
    }

    private Task<Result<T>> Read<T>(Func<Task<Result<T>>> action)
    {
        var authorization = _permissionService.Authorize(SystemCapability.ViewSuppliers);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<T>(authorization.AppError))
            : action();
    }

    private Task<Result<T>> Write<T>(Func<Task<Result<T>>> action)
    {
        var authorization = _permissionService.Authorize(SystemCapability.ManageSuppliers);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<T>(authorization.AppError))
            : action();
    }
}
