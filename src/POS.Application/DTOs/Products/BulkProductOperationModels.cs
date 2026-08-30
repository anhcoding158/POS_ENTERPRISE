using POS.Application.Common;

namespace POS.Application.DTOs.Products;

public enum BulkProductOperationType
{
    SetPrices,
    SetCategory,
    SetActiveState,
    SetMinimumStock
}

public sealed record BulkProductSelection(
    int ProductId,
    DateTimeOffset ExpectedUpdatedAtUtc);

public sealed record BulkProductOperationRequest(
    BulkProductOperationType Operation,
    IReadOnlyList<BulkProductSelection> Selection,
    long? CostPrice = null,
    long? SalePrice = null,
    int? CategoryId = null,
    bool? IsActive = null,
    int? MinimumStock = null);

public sealed record BulkProductPreviewRow(
    int ProductId,
    string ProductCode,
    string ProductName,
    string BeforeValue,
    string AfterValue,
    bool WillChange,
    string? ErrorMessage);

public sealed record BulkProductPreview(
    Guid PreviewId,
    BulkProductOperationRequest Request,
    IReadOnlyList<BulkProductPreviewRow> Rows,
    int ChangeCount,
    int NoOpCount,
    bool CanConfirm,
    IReadOnlyList<AppError> Errors);

public sealed record BulkProductOperationResult(
    Guid OperationId,
    bool IsCommitted,
    int RequestedCount,
    int ChangedCount,
    int NoOpCount,
    IReadOnlyList<AppError> Errors);
