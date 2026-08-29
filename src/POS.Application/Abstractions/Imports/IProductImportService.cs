using POS.Application.DTOs.ProductImports;

namespace POS.Application.Abstractions.ProductImports;

public enum ProductImportDuplicatePolicy
{
    Skip = 1,
    Update = 2,
    Error = 3
}

public enum ProductImportBatchStatus
{
    Committed = 1,
    RolledBack = 2
}

public enum ProductImportRowOutcome
{
    Created = 1,
    Updated = 2,
    Skipped = 3,
    Failed = 4
}

public sealed record ProductImportRequest(
    string FilePath,
    ProductImportPreviewResult Preview,
    ProductImportDuplicatePolicy DuplicatePolicy);

public sealed record ProductImportRowResult(
    int SourceRowNumber,
    ProductImportRowOutcome Outcome,
    string? ErrorCode = null,
    string? Message = null);

public sealed record ProductImportResult(
    Guid BatchId,
    ProductImportDuplicatePolicy DuplicatePolicy,
    ProductImportBatchStatus Status,
    int TotalValidRowsRequested,
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<ProductImportRowResult> Rows,
    IReadOnlyList<ProductImportIssue> Issues)
{
    public bool IsCommitted => Status == ProductImportBatchStatus.Committed;

    public static ProductImportResult Failure(
        Guid batchId,
        ProductImportDuplicatePolicy policy,
        int totalRows,
        IReadOnlyList<ProductImportIssue> issues) =>
        new(
            batchId,
            policy,
            ProductImportBatchStatus.RolledBack,
            totalRows,
            0,
            0,
            0,
            totalRows,
            [],
            issues);
}

public interface IProductImportService
{
    Task<ProductImportResult> ImportAsync(
        ProductImportRequest request,
        CancellationToken cancellationToken = default);
}
