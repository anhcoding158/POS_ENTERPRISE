using System.Globalization;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.Authorization;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Authorization;
using POS.Application.Common;
using POS.Application.DTOs.Products;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Atomic, permission-gated product operations. Preview never mutates entities;
/// commit rechecks the expected update timestamp inside the database transaction.
/// </summary>
public sealed class BulkProductOperationService : IBulkProductOperationService
{
    private const int MaximumSelection = 500;
    private const string BusinessArea = "Sản phẩm và thao tác hàng loạt";

    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly ISecurityAuditRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissions;

    public BulkProductOperationService(
        IProductRepository products,
        ICategoryRepository categories,
        ISecurityAuditRepository audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUserService currentUser,
        IPermissionService permissions)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _categories = categories ?? throw new ArgumentNullException(nameof(categories));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    public async Task<Result<BulkProductPreview>> PreviewAsync(
        BulkProductOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var authorization = _permissions.Authorize(SystemCapability.ManageProducts);
        if (authorization.IsFailure)
            return Result.Failure<BulkProductPreview>(authorization.AppError);

        var requestErrors = ValidateRequest(request);
        if (requestErrors.Count > 0)
            return Result.Success(new BulkProductPreview(Guid.NewGuid(), request, [], 0, 0, false, requestErrors));

        Category? category = null;
        if (request.Operation == BulkProductOperationType.SetCategory)
        {
            category = await _categories.GetByIdAsync(request.CategoryId!.Value, cancellationToken);
            if (category is null || !category.IsActive)
            {
                return Result.Success(new BulkProductPreview(
                    Guid.NewGuid(), request, [], 0, 0, false,
                    [new AppError(ErrorCodes.General.Validation, "Danh mục không tồn tại hoặc đang ngừng hoạt động.")]));
            }
        }

        var rows = new List<BulkProductPreviewRow>(request.Selection.Count);
        foreach (var selection in request.Selection)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var product = await _products.GetByIdReadOnlyAsync(selection.ProductId, cancellationToken);
            if (product is null)
            {
                rows.Add(new(selection.ProductId, "—", "—", "—", "—", false, "Không tìm thấy sản phẩm."));
                continue;
            }

            if (product.UpdatedAtUtc != selection.ExpectedUpdatedAtUtc.ToUniversalTime())
            {
                rows.Add(new(product.Id, product.Code, product.Name, "—", "—", false, "Sản phẩm đã thay đổi; hãy tạo preview mới."));
                continue;
            }

            var (before, after, error) = Describe(product, request, category);
            rows.Add(new(product.Id, product.Code, product.Name, before, after, error is null && before != after, error));
        }

        var errors = rows.Where(row => row.ErrorMessage is not null)
            .Select(row => new AppError(ErrorCodes.General.Validation, $"{row.ProductCode}: {row.ErrorMessage}"))
            .ToArray();
        return Result.Success(new BulkProductPreview(
            Guid.NewGuid(), request, rows, rows.Count(row => row.WillChange), rows.Count(row => !row.WillChange && row.ErrorMessage is null), errors.Length == 0 && rows.Count > 0, errors));
    }

    public async Task<Result<BulkProductOperationResult>> CommitAsync(
        BulkProductPreview preview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var operationId = preview.PreviewId;
        cancellationToken.ThrowIfCancellationRequested();
        var authorization = _permissions.Authorize(SystemCapability.ManageProducts);
        if (authorization.IsFailure)
            return Result.Failure<BulkProductOperationResult>(authorization.AppError);
        if (!preview.CanConfirm || preview.Errors.Count > 0)
            return Result.Success(Failed(operationId, preview.Request.Selection.Count, "Preview chưa hợp lệ hoặc đã hết hiệu lực; hãy kiểm tra lại."));

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            Category? category = null;
            if (preview.Request.Operation == BulkProductOperationType.SetCategory)
            {
                category = await _categories.GetByIdAsync(preview.Request.CategoryId!.Value, cancellationToken);
                if (category is null || !category.IsActive)
                    return Result.Success(Failed(operationId, preview.Request.Selection.Count, "Danh mục đã thay đổi hoặc không còn hoạt động."));
            }

            var changed = 0;
            var noOp = 0;
            foreach (var selection in preview.Request.Selection)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var product = await _products.GetByIdAsync(selection.ProductId, cancellationToken);
                if (product is null || product.UpdatedAtUtc != selection.ExpectedUpdatedAtUtc.ToUniversalTime() || product.IsArchived)
                    return Result.Success(Failed(operationId, preview.Request.Selection.Count, "Sản phẩm đã thay đổi, lưu trữ hoặc không còn tồn tại; toàn bộ thao tác đã được hoàn tác."));

                if (!Apply(product, preview.Request, _clock.UtcNow, out var didChange))
                    return Result.Success(Failed(operationId, preview.Request.Selection.Count, "Dữ liệu không đáp ứng quy tắc sản phẩm hiện hành; toàn bộ thao tác đã được hoàn tác."));
                if (didChange) changed++; else noOp++;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (changed > 0)
            {
                var audit = new SecurityAuditEvent(
                    _currentUser.UserId, null, null, SecurityAuditAction.EmployeeUpdated, "Success", operationId, _clock.UtcNow,
                    _currentUser.FullName, $"Batch {operationId:N}", BusinessArea, "Product bulk operation", "Không xác định",
                    [
                        new SecurityAuditChange("operation", null, preview.Request.Operation.ToString()),
                        new SecurityAuditChange("requested_count", null, preview.Request.Selection.Count.ToString(CultureInfo.InvariantCulture)),
                        new SecurityAuditChange("changed_count", null, changed.ToString(CultureInfo.InvariantCulture)),
                        new SecurityAuditChange("no_op_count", null, noOp.ToString(CultureInfo.InvariantCulture))
                    ]);
                await _audit.AddAsync(audit, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success(new BulkProductOperationResult(operationId, true, preview.Request.Selection.Count, changed, noOp, []));
        }
        catch (OperationCanceledException)
        {
            return Result.Success(Failed(operationId, preview.Request.Selection.Count, "Đã hủy; toàn bộ thay đổi đã được hoàn tác."));
        }
        catch (PersistenceConflictException)
        {
            return Result.Success(Failed(operationId, preview.Request.Selection.Count, "Dữ liệu đã thay đổi hoặc bị trùng; toàn bộ thao tác đã được hoàn tác."));
        }
        catch (DomainException)
        {
            return Result.Success(Failed(operationId, preview.Request.Selection.Count, "Dữ liệu không đáp ứng quy tắc nghiệp vụ; toàn bộ thao tác đã được hoàn tác."));
        }
        catch (Exception)
        {
            return Result.Success(Failed(operationId, preview.Request.Selection.Count, "Không thể lưu thao tác hàng loạt; toàn bộ thay đổi đã được hoàn tác."));
        }
    }

    private static List<AppError> ValidateRequest(BulkProductOperationRequest request)
    {
        var errors = new List<AppError>();
        if (!Enum.IsDefined(request.Operation))
            errors.Add(new(ErrorCodes.General.Validation, "Thao tác hàng loạt không hợp lệ."));
        if (request.Selection.Count == 0 || request.Selection.Count > MaximumSelection)
            errors.Add(new(ErrorCodes.General.Validation, $"Chọn từ 1 đến {MaximumSelection} sản phẩm."));
        if (request.Selection.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
            errors.Add(new(ErrorCodes.General.Validation, "Danh sách sản phẩm bị lặp."));
        if (request.Selection.Any(item => item.ProductId <= 0 || item.ExpectedUpdatedAtUtc == default))
            errors.Add(new(ErrorCodes.General.Validation, "Thông tin phiên bản sản phẩm không hợp lệ."));
        if (request.Operation == BulkProductOperationType.SetPrices &&
            (request.SalePrice is null || request.CostPrice is null || request.SalePrice < 0 || request.CostPrice < 0 || request.SalePrice > 999_999_999_999 || request.CostPrice > 999_999_999_999))
            errors.Add(new(ErrorCodes.General.Validation, "Giá bán và giá vốn phải là số nguyên không âm trong giới hạn hệ thống."));
        if (request.Operation == BulkProductOperationType.SetCategory && request.CategoryId is null or <= 0)
            errors.Add(new(ErrorCodes.General.Validation, "Danh mục cần chọn không hợp lệ."));
        if (request.Operation == BulkProductOperationType.SetActiveState && request.IsActive is null)
            errors.Add(new(ErrorCodes.General.Validation, "Trạng thái cần chọn không hợp lệ."));
        if (request.Operation == BulkProductOperationType.SetMinimumStock && (request.MinimumStock is null || request.MinimumStock < 0 || request.MinimumStock > 999_999_999))
            errors.Add(new(ErrorCodes.General.Validation, "Tồn tối thiểu phải là số nguyên không âm trong giới hạn hệ thống."));
        return errors;
    }

    private static (string Before, string After, string? Error) Describe(Product product, BulkProductOperationRequest request, Category? category) => request.Operation switch
    {
        BulkProductOperationType.SetPrices => ($"Giá vốn {product.CostPrice:N0} · Giá bán {product.SalePrice:N0}", $"Giá vốn {request.CostPrice!.Value:N0} · Giá bán {request.SalePrice!.Value:N0}", null),
        BulkProductOperationType.SetCategory => (product.Category?.Name ?? product.CategoryId.ToString(CultureInfo.InvariantCulture), category!.Name, null),
        BulkProductOperationType.SetActiveState => (product.IsActive ? "Đang bán" : "Ngừng bán", request.IsActive!.Value ? "Đang bán" : "Ngừng bán", null),
        BulkProductOperationType.SetMinimumStock => (product.MinimumStock.ToString(CultureInfo.InvariantCulture), request.MinimumStock!.Value.ToString(CultureInfo.InvariantCulture), null),
        _ => ("—", "—", "Thao tác không được hỗ trợ.")
    };

    private static bool Apply(Product product, BulkProductOperationRequest request, DateTimeOffset now, out bool changed)
    {
        changed = true;
        try
        {
            switch (request.Operation)
            {
                case BulkProductOperationType.SetPrices:
                    changed = product.CostPrice != request.CostPrice!.Value || product.SalePrice != request.SalePrice!.Value;
                    if (changed) product.ChangePrices(request.CostPrice!.Value, request.SalePrice!.Value, now);
                    break;
                case BulkProductOperationType.SetCategory:
                    changed = product.CategoryId != request.CategoryId!.Value;
                    if (changed) product.UpdateDetails(request.CategoryId.Value, product.Code, product.Barcode, product.Name, product.Description, product.UnitName, product.ImagePath, now);
                    break;
                case BulkProductOperationType.SetActiveState:
                    changed = product.IsActive != request.IsActive!.Value;
                    if (changed) { if (request.IsActive.Value) product.Activate(now); else product.Deactivate(now); }
                    break;
                case BulkProductOperationType.SetMinimumStock:
                    changed = product.MinimumStock != request.MinimumStock!.Value;
                    if (changed) product.ConfigureInventory(request.MinimumStock.Value, product.TrackInventory, product.AllowNegativeStock, now);
                    break;
                default: return false;
            }
            return true;
        }
        catch (DomainException) { return false; }
    }

    private static BulkProductOperationResult Failed(Guid operationId, int count, string message) =>
        new(operationId, false, count, 0, 0, [new AppError(ErrorCodes.General.Validation, message)]);
}
