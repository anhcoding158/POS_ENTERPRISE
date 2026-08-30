using POS.Application.Common;
using POS.Application.DTOs.Products;

namespace POS.Application.Abstractions.Services;

public interface IBulkProductOperationService
{
    Task<Result<BulkProductPreview>> PreviewAsync(
        BulkProductOperationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<BulkProductOperationResult>> CommitAsync(
        BulkProductPreview preview,
        CancellationToken cancellationToken = default);
}
