using POS.Application.Common;
using POS.Application.DTOs.Purchasing;

namespace POS.Application.Abstractions.Services;

public interface IPurchaseOrderService
{
    Task<Result<PagedResult<PurchaseOrderListItemDto>>> SearchAsync(
        PurchaseOrderSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> GetByIdAsync(
        int purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> CreateDraftAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> UpdateDraftAsync(
        UpdateDraftPurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> MarkOrderedAsync(
        MarkPurchaseOrderOrderedRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> AmendOrderedAsync(
        AmendOrderedPurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PurchaseOrderDetailsDto>> CancelAsync(
        CancelPurchaseOrderRequest request,
        CancellationToken cancellationToken = default);
}
