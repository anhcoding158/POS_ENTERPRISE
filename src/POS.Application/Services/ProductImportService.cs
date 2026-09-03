using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.ProductImports;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.ProductImports;
using POS.Application.ProductImports;
using POS.Domain.Entities;
using POS.Domain.Common;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Atomic product import use case. It consumes a revalidated R5.1A snapshot,
/// never parses or stores raw import content, and never creates categories.
/// </summary>
public sealed class ProductImportService : IProductImportService
{
    private const string ImportBusinessArea = "Sản phẩm và nhập dữ liệu";
    private const string ImportTargetType = "Product import batch";
    private const string ImportReferenceType = "PRODUCT_IMPORT";
    private const string OpeningBalanceReason = "Tồn đầu kỳ khi nhập sản phẩm.";

    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IInventoryMovementRepository _movementRepository;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductImportPreviewService _previewService;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissions;

    public ProductImportService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IInventoryMovementRepository movementRepository,
        ISecurityAuditRepository auditRepository,
        IUnitOfWork unitOfWork,
        IProductImportPreviewService previewService,
        IClock clock,
        ICurrentUserService currentUser,
        IPermissionService permissions)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _movementRepository = movementRepository ?? throw new ArgumentNullException(nameof(movementRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public async Task<ProductImportResult> ImportAsync(
        ProductImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batchId = Guid.NewGuid();
        cancellationToken.ThrowIfCancellationRequested();

        var manageAuthorization = _permissions.Authorize(SystemCapability.ManageProducts);
        if (manageAuthorization.IsFailure)
        {
            return Failure(
                batchId,
                request,
                manageAuthorization.AppError.Code,
                manageAuthorization.AppError.Message);
        }

        if (!Enum.IsDefined(request.DuplicatePolicy))
        {
            return Failure(batchId, request, "IMPORT_POLICY_INVALID", "Chính sách xử lý trùng không hợp lệ.");
        }

        var preview = request.Preview;
        if (string.IsNullOrWhiteSpace(preview.File.ContentSha256) ||
            preview.ReferenceSnapshot?.CategoryIdsByNormalizedName is null ||
            preview.ValidatedRows.Count != preview.Summary.TotalDataRows)
        {
            return Failure(batchId, request, "PREVIEW_SNAPSHOT_REQUIRED", "Preview đã hết hiệu lực; vui lòng preview và xác nhận lại tệp.");
        }

        ProductImportPreviewResult currentPreview;
        try
        {
            currentPreview = await _previewService.PreviewAsync(
                request.FilePath,
                new ProductImportPreviewOptions(
                    References: new ProductImportReferenceData(
                        preview.ReferenceSnapshot.CategoryIdsByNormalizedName,
                        preview.ReferenceSnapshot.KnownUnitNames),
                    WorksheetName: preview.SelectedWorksheetName,
                    ColumnMappings: preview.ColumnMappings),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Failure(batchId, request, "IMPORT_CANCELLED", "Nhập dữ liệu đã được hủy trước khi ghi.");
        }

        var snapshotIssues = ValidateCurrentPreview(preview, currentPreview, request.DuplicatePolicy);
        if (snapshotIssues.Count > 0)
        {
            return Failure(batchId, request, snapshotIssues);
        }

        var rows = currentPreview.ValidatedRows;
        var categoryMap = await ResolveCategoriesAsync(
            rows,
            preview.ReferenceSnapshot.CategoryIdsByNormalizedName,
            cancellationToken);
        if (categoryMap.Issues.Count > 0)
        {
            return Failure(batchId, request, categoryMap.Issues);
        }

        if (rows.Any(row => row.InitialStockQuantity.GetValueOrDefault() > 0) &&
            !_permissions.HasPermission(SystemCapability.AdjustInventory))
        {
            return Failure(batchId, request, "GENERAL.FORBIDDEN", "Tài khoản hiện tại không có quyền điều chỉnh tồn kho để nhập tồn đầu kỳ.");
        }

        var planning = await BuildPlanAsync(
            rows,
            categoryMap.Categories,
            request.DuplicatePolicy,
            cancellationToken);
        if (planning.Issues.Count > 0)
        {
            return Failure(batchId, request, planning.Issues);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = _clock.UtcNow;
            foreach (var plan in planning.Plans)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (plan.Outcome == ProductImportRowOutcome.Skipped)
                    continue;

                if (plan.IsCreate)
                {
                    await _productRepository.AddAsync(plan.Product, cancellationToken);
                }
                else
                {
                    ApplyUpdate(plan.Product, plan.Row, plan.Category.Id, now);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var plan in planning.Plans.Where(plan => plan.IsCreate && plan.Row.InitialStockQuantity.GetValueOrDefault() > 0))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var quantity = plan.Row.InitialStockQuantity!.Value;
                plan.Product.ReconcileStock(quantity, now);
                await _movementRepository.AddAsync(
                    new InventoryMovement(
                        plan.Product.Id,
                        InventoryMovementType.OpeningBalance,
                        quantity,
                        0,
                        quantity,
                        OpeningBalanceReason,
                        now,
                        ImportReferenceType,
                        batchId.ToString("N"),
                        _currentUser.UserId),
                    cancellationToken);
            }

            var createdCount = planning.Plans.Count(plan => plan.Outcome == ProductImportRowOutcome.Created);
            var updatedCount = planning.Plans.Count(plan => plan.Outcome == ProductImportRowOutcome.Updated);
            var skippedCount = planning.Plans.Count(plan => plan.Outcome == ProductImportRowOutcome.Skipped);
            var audit = new SecurityAuditEvent(
                _currentUser.UserId,
                null,
                null,
                SecurityAuditAction.BulkProductOperation,
                "Success",
                batchId,
                now,
                _currentUser.FullName,
                $"Batch {batchId:N}",
                ImportBusinessArea,
                ImportTargetType,
                "Không xác định",
                [
                    new SecurityAuditChange("batch_id", null, batchId.ToString("N")),
                    new SecurityAuditChange("duplicate_policy", null, request.DuplicatePolicy.ToString()),
                    new SecurityAuditChange("total_valid_rows", null, rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new SecurityAuditChange("created_count", null, createdCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new SecurityAuditChange("updated_count", null, updatedCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new SecurityAuditChange("skipped_count", null, skippedCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new SecurityAuditChange("failed_count", null, "0"),
                    new SecurityAuditChange("status", null, "committed")
                ]);
            await _auditRepository.AddAsync(audit, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ProductImportResult(
                batchId,
                request.DuplicatePolicy,
                ProductImportBatchStatus.Committed,
                rows.Count,
                createdCount,
                updatedCount,
                skippedCount,
                0,
                planning.Plans.Select(plan => new ProductImportRowResult(plan.Row.SourceRowNumber, plan.Outcome)).ToArray(),
                []);
        }
        catch (OperationCanceledException)
        {
            return Failure(batchId, request, "IMPORT_CANCELLED", "Nhập dữ liệu đã được hủy; toàn bộ thay đổi đã được hoàn tác.", rows.Count);
        }
        catch (PersistenceConflictException exception)
        {
            var code = exception.Kind == PersistenceConflictKind.Concurrency
                ? "PERSISTENCE_CONCURRENCY"
                : "PERSISTENCE_CONFLICT";
            return Failure(batchId, request, code, "Dữ liệu đã thay đổi hoặc bị trùng; toàn bộ lô nhập đã được hoàn tác.", rows.Count);
        }
        catch (DatabaseOperationException exception)
        {
            var message = exception.Kind is DatabaseFailureKind.Busy or DatabaseFailureKind.Locked
                ? "Dữ liệu đang bận; toàn bộ lô nhập đã được hoàn tác. Vui lòng thử lại."
                : "Không thể lưu lô nhập an toàn; toàn bộ thay đổi đã được hoàn tác.";
            return Failure(batchId, request, "DATABASE_OPERATION_FAILED", message, rows.Count);
        }
        catch (DomainException)
        {
            return Failure(batchId, request, "IMPORT_DOMAIN_VALIDATION", "Dữ liệu không đáp ứng quy tắc sản phẩm/kho; toàn bộ lô nhập đã được hoàn tác.", rows.Count);
        }
        catch (Exception)
        {
            return Failure(batchId, request, "IMPORT_FAILED", "Không thể hoàn tất lô nhập an toàn; toàn bộ thay đổi đã được hoàn tác.", rows.Count);
        }
    }

    private async Task<(Dictionary<string, Category> Categories, IReadOnlyList<ProductImportIssue> Issues)> ResolveCategoriesAsync(
        IReadOnlyList<ProductImportRow> rows,
        IReadOnlyDictionary<string, int> referenceIds,
        CancellationToken cancellationToken)
    {
        var categories = new Dictionary<string, Category>(StringComparer.Ordinal);
        var issues = new List<ProductImportIssue>();
        var normalizedReferenceIds = referenceIds
            .GroupBy(pair => ProductImportSchemaCatalog.NormalizeHeader(pair.Key), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single().Value, StringComparer.Ordinal);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.CategoryName is null)
                continue;
            var key = ProductImportSchemaCatalog.NormalizeHeader(row.CategoryName);
            if (categories.ContainsKey(key))
                continue;
            if (!normalizedReferenceIds.TryGetValue(key, out var categoryId))
            {
                issues.Add(Issue("CATEGORY_NOT_FOUND", "Danh mục không tồn tại trong snapshot; vui lòng preview lại.", row));
                continue;
            }
            var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
            if (category is null)
            {
                issues.Add(Issue("CATEGORY_NOT_FOUND", "Danh mục không còn tồn tại; vui lòng preview lại.", row));
            }
            else if (!category.IsActive)
            {
                issues.Add(Issue("CATEGORY_INACTIVE", "Danh mục đang ngừng hoạt động; không thể nhập sản phẩm.", row));
            }
            else if (!string.Equals(
                         ProductImportSchemaCatalog.NormalizeHeader(category.Name),
                         key,
                         StringComparison.Ordinal))
            {
                issues.Add(Issue("CATEGORY_REFERENCE_CHANGED", "Danh mục tham chiếu đã thay đổi; vui lòng preview và validate lại.", row));
            }
            else
            {
                categories[key] = category;
            }
        }
        return (categories, issues);
    }

    private async Task<(List<RowPlan> Plans, IReadOnlyList<ProductImportIssue> Issues)> BuildPlanAsync(
        IReadOnlyList<ProductImportRow> rows,
        Dictionary<string, Category> categories,
        ProductImportDuplicatePolicy policy,
        CancellationToken cancellationToken)
    {
        var plans = new List<RowPlan>();
        var issues = new List<ProductImportIssue>();
        var byCode = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        var byBarcode = new Dictionary<string, Product>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.ProductCode is null || row.Name is null || row.CategoryName is null || row.UnitName is null || row.SalePrice is null || row.CostPrice is null)
                continue;

            var category = categories[ProductImportSchemaCatalog.NormalizeHeader(row.CategoryName)];
            var codeTarget = await FindByCodeAsync(row.ProductCode, byCode, cancellationToken);
            var barcodeTarget = row.Barcode is null ? null : await FindByBarcodeAsync(row.Barcode, byBarcode, cancellationToken);
            if (codeTarget is not null && barcodeTarget is not null && !SameProduct(codeTarget, barcodeTarget))
            {
                issues.Add(Issue("IDENTITY_CONFLICT", "ProductCode và Barcode đang trỏ tới hai sản phẩm khác nhau.", row));
                continue;
            }

            var target = codeTarget ?? barcodeTarget;
            var duplicate = target is not null || HasDuplicateIssue(row);
            if (duplicate && policy == ProductImportDuplicatePolicy.Error)
            {
                issues.Add(Issue("DUPLICATE_REJECTED", "Lô nhập có dữ liệu trùng và chính sách hiện tại là Error.", row));
                continue;
            }

            if (target is not null && policy == ProductImportDuplicatePolicy.Skip)
            {
                plans.Add(new RowPlan(row, category, target, false, ProductImportRowOutcome.Skipped));
                continue;
            }

            if (target is not null && target.IsArchived)
            {
                issues.Add(Issue("PRODUCT_ARCHIVED", "Sản phẩm đã lưu trữ không thể được cập nhật.", row));
                continue;
            }

            if (target is not null)
            {
                if (row.InitialStockQuantity.GetValueOrDefault() != 0)
                {
                    issues.Add(Issue("UPDATE_OPENING_STOCK_NOT_ALLOWED", "Tồn đầu kỳ chỉ được áp dụng khi tạo sản phẩm mới; không ghi đè tồn hiện tại.", row));
                    continue;
                }
                plans.Add(new RowPlan(row, category, target, false, ProductImportRowOutcome.Updated));
            }
            else
            {
                try
                {
                    var product = new Product(
                        category.Id,
                        row.ProductCode,
                        row.Name,
                        row.UnitName,
                        row.CostPrice.Value,
                        row.SalePrice.Value,
                        0,
                        row.MinimumStock.GetValueOrDefault(),
                        true,
                        false,
                        _clock.UtcNow,
                        row.Barcode,
                        row.Notes);
                    if (row.IsActive == false)
                        product.Deactivate(_clock.UtcNow);
                    plans.Add(new RowPlan(row, category, product, true, ProductImportRowOutcome.Created));
                    byCode[product.Code] = product;
                    if (product.Barcode is not null)
                        byBarcode[product.Barcode] = product;
                }
                catch (DomainException)
                {
                    issues.Add(Issue("PRODUCT_DOMAIN_INVALID", "Dữ liệu sản phẩm không đáp ứng quy tắc nghiệp vụ hiện hành.", row));
                }
            }
        }
        return (plans, issues);
    }

    private async Task<Product?> FindByCodeAsync(string code, Dictionary<string, Product> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(code, out var cached))
            return cached;
        var found = await _productRepository.GetByCodeAsync(code, cancellationToken);
        if (found is null)
            return null;
        var tracked = await _productRepository.GetByIdAsync(found.Id, cancellationToken) ?? found;
        cache[tracked.Code] = tracked;
        return tracked;
    }

    private async Task<Product?> FindByBarcodeAsync(string barcode, Dictionary<string, Product> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(barcode, out var cached))
            return cached;
        var found = await _productRepository.GetByBarcodeAsync(barcode, cancellationToken);
        if (found is null)
            return null;
        var tracked = await _productRepository.GetByIdAsync(found.Id, cancellationToken) ?? found;
        cache[tracked.Barcode!] = tracked;
        return tracked;
    }

    private static void ApplyUpdate(Product product, ProductImportRow row, int categoryId, DateTimeOffset now)
    {
        product.UpdateDetails(categoryId, row.ProductCode!, row.Barcode, row.Name!, row.Notes, row.UnitName!, product.ImagePath, now);
        product.ChangePrices(row.CostPrice!.Value, row.SalePrice!.Value, now);
        product.ConfigureInventory(row.MinimumStock.GetValueOrDefault(), product.TrackInventory, product.AllowNegativeStock, now);
        if (row.IsActive.GetValueOrDefault(true))
            product.Activate(now);
        else
            product.Deactivate(now);
    }

    private static bool SameProduct(Product left, Product right) =>
        ReferenceEquals(left, right) || (left.Id > 0 && left.Id == right.Id);

    private static bool HasDuplicateIssue(ProductImportRow row) =>
        row.Issues.Any(issue => issue.Code is "DUPLICATE_PRODUCT_CODE" or "DUPLICATE_BARCODE");

    private static List<ProductImportIssue> ValidateCurrentPreview(
        ProductImportPreviewResult expected,
        ProductImportPreviewResult current,
        ProductImportDuplicatePolicy policy)
    {
        var issues = new List<ProductImportIssue>();
        if (!string.Equals(expected.File.ContentSha256, current.File.ContentSha256, StringComparison.OrdinalIgnoreCase) ||
            expected.Summary.TotalDataRows != current.Summary.TotalDataRows ||
            expected.Format != current.Format)
        {
            issues.Add(new(ProductImportIssueSeverity.Error, "PREVIEW_STALE", "Tệp hoặc kết quả preview đã thay đổi; vui lòng preview và validate lại."));
            return issues;
        }

        issues.AddRange(current.FileIssues.Where(issue => issue.Severity == ProductImportIssueSeverity.Error));
        foreach (var row in current.ValidatedRows)
        {
            foreach (var issue in row.Issues.Where(issue => issue.Severity == ProductImportIssueSeverity.Error))
            {
                if ((policy is ProductImportDuplicatePolicy.Skip or ProductImportDuplicatePolicy.Update) &&
                    (issue.Code is "DUPLICATE_PRODUCT_CODE" or "DUPLICATE_BARCODE"))
                    continue;
                issues.Add(issue);
            }
        }
        return issues;
    }

    private static ProductImportIssue Issue(string code, string message, ProductImportRow row) =>
        new(ProductImportIssueSeverity.Error, code, message, row.SourceRowNumber);

    private static ProductImportResult Failure(Guid batchId, ProductImportRequest request, string code, string message, int? totalRows = null) =>
        ProductImportResult.Failure(batchId, request.DuplicatePolicy, totalRows ?? request.Preview.ValidatedRows.Count, [new(ProductImportIssueSeverity.Error, code, message)]);

    private static ProductImportResult Failure(Guid batchId, ProductImportRequest request, IReadOnlyList<ProductImportIssue> issues) =>
        ProductImportResult.Failure(batchId, request.DuplicatePolicy, request.Preview.ValidatedRows.Count, issues);

    private sealed record RowPlan(ProductImportRow Row, Category Category, Product Product, bool IsCreate, ProductImportRowOutcome Outcome);
}
