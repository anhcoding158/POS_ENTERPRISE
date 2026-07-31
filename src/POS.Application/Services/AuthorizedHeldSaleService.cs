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
        var authorization = permissionService.Authorize(SystemCapability.UseCheckout);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<HeldSaleDto>(authorization.AppError))
            : innerService.CreateHeldSaleAsync(request, cancellationToken);
    }

    public Task<Result<IReadOnlyList<HeldSaleDto>>> GetActiveHeldSalesAsync(
        int limit = 100, CancellationToken cancellationToken = default)
    {
        var authorization = permissionService.Authorize(SystemCapability.UseCheckout);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<IReadOnlyList<HeldSaleDto>>(authorization.AppError))
            : innerService.GetActiveHeldSalesAsync(limit, cancellationToken);
    }

    public Task<Result<HeldSaleResumeDto>> GetHeldSaleForResumeAsync(
        int heldSaleId, CancellationToken cancellationToken = default)
    {
        var authorization = permissionService.Authorize(SystemCapability.UseCheckout);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<HeldSaleResumeDto>(authorization.AppError))
            : innerService.GetHeldSaleForResumeAsync(heldSaleId, cancellationToken);
    }

    public Task<Result> CancelHeldSaleAsync(
        int heldSaleId, CancellationToken cancellationToken = default)
    {
        var authorization = permissionService.Authorize(SystemCapability.UseCheckout);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure(authorization.AppError))
            : innerService.CancelHeldSaleAsync(heldSaleId, cancellationToken);
    }
}
