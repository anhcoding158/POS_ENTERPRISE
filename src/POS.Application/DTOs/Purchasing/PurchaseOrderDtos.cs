using POS.Application.Common;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Purchasing;

public sealed record PurchaseOrderLineRequest(
    int ProductId,
    int OrderedQuantity,
    long AgreedUnitCost,
    int SortOrder);

public sealed record PurchaseOrderListItemDto(
    int Id,
    string OrderNumber,
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    PurchaseOrderStatus Status,
    int LineCount,
    long TotalOrderedQuantity,
    long GrandTotal,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PurchaseOrderLineDto(
    int Id,
    int ProductId,
    string ProductCode,
    string ProductName,
    string UnitName,
    int OrderedQuantity,
    int ReceivedQuantity,
    long AgreedUnitCost,
    long LineTotal,
    int SortOrder);

public sealed record PurchaseOrderDetailsDto(
    int Id,
    string OrderNumber,
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    string? SupplierTaxCode,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string? Notes,
    PurchaseOrderStatus Status,
    DateTimeOffset? OrderedAtUtc,
    int? OrderedByUserId,
    DateTimeOffset? CancelledAtUtc,
    int? CancelledByUserId,
    string? CancellationReason,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    long TotalOrderedQuantity,
    long TotalReceivedQuantity,
    long GrandTotal,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PurchaseOrderSearchRequest(
    string? SearchTerm = null,
    PurchaseOrderStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20);

public sealed record CreatePurchaseOrderRequest(
    int SupplierId,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string? Notes,
    IReadOnlyCollection<PurchaseOrderLineRequest> Lines);

public sealed record UpdateDraftPurchaseOrderRequest(
    int PurchaseOrderId,
    int SupplierId,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string? Notes,
    IReadOnlyCollection<PurchaseOrderLineRequest> Lines,
    DateTimeOffset ExpectedUpdatedAtUtc);

public sealed record MarkPurchaseOrderOrderedRequest(
    int PurchaseOrderId,
    DateTimeOffset ExpectedUpdatedAtUtc);

public sealed record AmendOrderedPurchaseOrderRequest(
    int PurchaseOrderId,
    DateOnly? ExpectedDeliveryDate,
    string? Notes,
    IReadOnlyCollection<PurchaseOrderLineRequest> Lines,
    DateTimeOffset ExpectedUpdatedAtUtc);

public sealed record CancelPurchaseOrderRequest(
    int PurchaseOrderId,
    string Reason,
    DateTimeOffset ExpectedUpdatedAtUtc);
