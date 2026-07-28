using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.HeldSales;

namespace POS.Application.Services;

public sealed class AuthorizedHeldSaleService(
    IHeldSaleService innerService,
    IPermissionService permissionService) : IHeldSaleService
{
    public Task<Result<HeldSaleDto>> CreateHeldSaleAsync(
        CreateHeldSaleRequest request, CancellationToken cancellationToken = default)
    {
        var authorization = permissionService.Authorize(SystemPermission.UseCheckout);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<HeldSaleDto>(authorization.Error))
            : innerService.CreateHeldSaleAsync(request, cancellationToken);
    }

    public Task<Result<IReadOnlyList<HeldSaleDto>>> GetActiveHeldSalesAsync(
        int limit = 100, CancellationToken cancellationToken = default)
    {
        var authorization = permissionService.Authorize(SystemPermission.UseCheckout);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<IReadOnlyList<HeldSaleDto>>(authorization.Error))
            : innerService.GetActiveHeldSalesAsync(limit, cancellationToken);
    }

    public Task<Result<HeldSaleResumeDto>> GetHeldSaleForResumeAsync(
        int heldSaleId, CancellationToken cancellationToken = default)
    {
        var authorization = permissionService.Authorize(SystemPermission.UseCheckout);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<HeldSaleResumeDto>(authorization.Error))
            : innerService.GetHeldSaleForResumeAsync(heldSaleId, cancellationToken);
    }

    public Task<Result> CancelHeldSaleAsync(
        int heldSaleId, CancellationToken cancellationToken = default)
    {
        var authorization = permissionService.Authorize(SystemPermission.UseCheckout);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure(authorization.Error))
            : innerService.CancelHeldSaleAsync(heldSaleId, cancellationToken);
    }
}
