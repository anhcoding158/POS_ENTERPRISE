using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.ProductImports;
using POS.Application.Authorization;
using POS.Application.DTOs.ProductImports;

namespace POS.Application.Services;

public sealed class AuthorizedProductImportService : IProductImportService
{
    private readonly IProductImportService _innerService;
    private readonly IPermissionService _permissionService;

    public AuthorizedProductImportService(
        IProductImportService innerService,
        IPermissionService permissionService)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
    }

    public Task<ProductImportResult> ImportAsync(
        ProductImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authorization = _permissionService.Authorize(SystemCapability.ManageProducts);
        if (authorization.IsFailure)
        {
            var batchId = Guid.NewGuid();
            var issue = new ProductImportIssue(
                ProductImportIssueSeverity.Error,
                authorization.AppError.Code,
                authorization.AppError.Message);
            return Task.FromResult(ProductImportResult.Failure(
                batchId,
                request.DuplicatePolicy,
                request.Preview.ValidatedRows.Count,
                [issue]));
        }

        return _innerService.ImportAsync(request, cancellationToken);
    }
}
