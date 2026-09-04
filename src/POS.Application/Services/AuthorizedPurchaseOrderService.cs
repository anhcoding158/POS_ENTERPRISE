using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Purchasing;

namespace POS.Application.Services;

public sealed class AuthorizedPurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderService _innerService;
    private readonly IPermissionService _permissionService;

    public AuthorizedPurchaseOrderService(
        IPurchaseOrderService innerService,
        IPermissionService permissionService)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public Task<Result<PagedResult<PurchaseOrderListItemDto>>> SearchAsync(
        PurchaseOrderSearchRequest request,
        CancellationToken cancellationToken = default) =>
        Read(() => _innerService.SearchAsync(request, cancellationToken));

    public Task<Result<PurchaseOrderDetailsDto>> GetByIdAsync(
        int purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        Read(() => _innerService.GetByIdAsync(purchaseOrderId, cancellationToken));

    public Task<Result<PurchaseOrderDetailsDto>> CreateDraftAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default) =>
        Write(() => _innerService.CreateDraftAsync(request, cancellationToken));

    public Task<Result<PurchaseOrderDetailsDto>> UpdateDraftAsync(
        UpdateDraftPurchaseOrderRequest request,
        CancellationToken cancellationToken = default) =>
        Write(() => _innerService.UpdateDraftAsync(request, cancellationToken));

    public Task<Result<PurchaseOrderDetailsDto>> MarkOrderedAsync(
        MarkPurchaseOrderOrderedRequest request,
        CancellationToken cancellationToken = default) =>
        Write(() => _innerService.MarkOrderedAsync(request, cancellationToken));

    public Task<Result<PurchaseOrderDetailsDto>> AmendOrderedAsync(
        AmendOrderedPurchaseOrderRequest request,
        CancellationToken cancellationToken = default) =>
        Write(() => _innerService.AmendOrderedAsync(request, cancellationToken));

    public Task<Result<PurchaseOrderDetailsDto>> CancelAsync(
        CancelPurchaseOrderRequest request,
        CancellationToken cancellationToken = default) =>
        Write(() => _innerService.CancelAsync(request, cancellationToken));

    private Task<Result<T>> Read<T>(Func<Task<Result<T>>> action)
    {
        var authorization = _permissionService.Authorize(SystemCapability.ViewPurchaseOrders);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<T>(authorization.AppError))
            : action();
    }

    private Task<Result<T>> Write<T>(Func<Task<Result<T>>> action)
    {
        var authorization = _permissionService.Authorize(SystemCapability.ManagePurchaseOrders);
        return authorization.IsFailure
            ? Task.FromResult(Result.Failure<T>(authorization.AppError))
            : action();
    }
}
