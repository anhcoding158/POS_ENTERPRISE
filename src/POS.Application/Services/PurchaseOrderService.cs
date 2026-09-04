using System.Globalization;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Purchasing;
using POS.Application.Abstractions.Security;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Purchasing;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private const string BusinessArea = "Mua hàng";
    private const string TargetType = "Purchase Order";
    private const int NumberGenerationAttempts = 3;

    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IProductRepository _productRepository;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IPurchaseOrderNumberGenerator _numberGenerator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITerminalIdentityProvider? _terminalIdentityProvider;

    public PurchaseOrderService(
        IPurchaseOrderRepository purchaseOrderRepository,
        ISupplierRepository supplierRepository,
        IProductRepository productRepository,
        ISecurityAuditRepository auditRepository,
        IPurchaseOrderNumberGenerator numberGenerator,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUserService currentUserService,
        ITerminalIdentityProvider? terminalIdentityProvider = null)
    {
        _purchaseOrderRepository = purchaseOrderRepository ?? throw new ArgumentNullException(nameof(purchaseOrderRepository));
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _numberGenerator = numberGenerator ?? throw new ArgumentNullException(nameof(numberGenerator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _terminalIdentityProvider = terminalIdentityProvider;
    }

    public async Task<Result<PagedResult<PurchaseOrderListItemDto>>> SearchAsync(
        PurchaseOrderSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var page = await _purchaseOrderRepository.SearchAsync(
                request.SearchTerm,
                request.Status,
                request.PageNumber,
                request.PageSize,
                cancellationToken);
            return Result.Success(page.Map(MapList));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Failure<PagedResult<PurchaseOrderListItemDto>>(
                ErrorCodes.General.Validation,
                exception.Message);
        }
    }

    public async Task<Result<PurchaseOrderDetailsDto>> GetByIdAsync(
        int purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        if (purchaseOrderId <= 0)
        {
            return Failure<PurchaseOrderDetailsDto>(
                ErrorCodes.General.Validation,
                "Mã Purchase Order phải lớn hơn 0.");
        }

        var purchaseOrder = await _purchaseOrderRepository.GetByIdReadOnlyAsync(
            purchaseOrderId,
            cancellationToken);
        return purchaseOrder is null
            ? Failure<PurchaseOrderDetailsDto>(
                ErrorCodes.PurchaseOrders.NotFound,
                "Không tìm thấy Purchase Order.")
            : Result.Success(MapDetails(purchaseOrder));
    }

    public async Task<Result<PurchaseOrderDetailsDto>> CreateDraftAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lineValidation = ValidateLineRequests(request.Lines);
        if (lineValidation is not null)
        {
            return Failure<PurchaseOrderDetailsDto>(
                ErrorCodes.General.Validation,
                lineValidation);
        }

        var supplier = await _supplierRepository.GetByIdReadOnlyAsync(
            request.SupplierId,
            cancellationToken);
        var supplierValidation = ValidateSupplierForNewOrder(supplier);
        if (supplierValidation is not null)
        {
            return Failure<PurchaseOrderDetailsDto>(
                supplierValidation.Value.Code,
                supplierValidation.Value.Message);
        }

        var products = await LoadProductsAsync(request.Lines, cancellationToken);
        var productValidation = ValidateProductsForNewOrder(products, request.Lines.Select(line => line.ProductId));
        if (productValidation is not null)
        {
            return Failure<PurchaseOrderDetailsDto>(
                productValidation.Value.Code,
                productValidation.Value.Message);
        }

        for (var attempt = 0; attempt < NumberGenerationAttempts; attempt++)
        {
            PurchaseOrder purchaseOrder;
            try
            {
                purchaseOrder = CreateAggregate(
                    _numberGenerator.Generate(_clock.UtcNow),
                    request,
                    supplier!,
                    products!);
            }
            catch (DomainException exception)
            {
                return Failure<PurchaseOrderDetailsDto>(exception.Code, exception.Message);
            }

            if (await _purchaseOrderRepository.NormalizedOrderNumberExistsAsync(
                    purchaseOrder.NormalizedOrderNumber,
                    cancellationToken))
            {
                continue;
            }

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await _purchaseOrderRepository.AddAsync(purchaseOrder, cancellationToken);
                await _auditRepository.AddAsync(
                    CreateAudit(
                        purchaseOrder,
                        SecurityAuditAction.PurchaseOrderCreated,
                        [
                            new SecurityAuditChange("Số dòng", null, purchaseOrder.Lines.Count.ToString(CultureInfo.InvariantCulture)),
                            new SecurityAuditChange("Tổng số lượng", null, TotalQuantity(purchaseOrder).ToString(CultureInfo.InvariantCulture))
                        ]),
                    cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(MapDetails(purchaseOrder));
            }
            catch (PersistenceConflictException exception) when (
                exception.Target == PersistenceConflictTargets.PurchaseOrderNumber &&
                attempt + 1 < NumberGenerationAttempts)
            {
                // Một tiến trình khác vừa chiếm candidate; thử candidate mới.
            }
            catch (PersistenceConflictException exception)
            {
                return Failure<PurchaseOrderDetailsDto>(
                    MapConflictCode(exception),
                    MapConflictMessage(exception));
            }
        }

        return Failure<PurchaseOrderDetailsDto>(
            ErrorCodes.PurchaseOrders.NumberAlreadyExists,
            "Không thể tạo số Purchase Order duy nhất sau số lần thử giới hạn.");
    }

    public async Task<Result<PurchaseOrderDetailsDto>> UpdateDraftAsync(
        UpdateDraftPurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lineValidation = ValidateLineRequests(request.Lines);
        if (lineValidation is not null)
        {
            return Failure<PurchaseOrderDetailsDto>(ErrorCodes.General.Validation, lineValidation);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var purchaseOrder = await _purchaseOrderRepository.GetByIdAsync(
                request.PurchaseOrderId,
                cancellationToken);
            if (purchaseOrder is null)
            {
                return Failure<PurchaseOrderDetailsDto>(ErrorCodes.PurchaseOrders.NotFound, "Không tìm thấy Purchase Order.");
            }

            if (!IsExpectedVersion(purchaseOrder.UpdatedAtUtc, request.ExpectedUpdatedAtUtc))
            {
                return Conflict<PurchaseOrderDetailsDto>();
            }

            var supplier = await _supplierRepository.GetByIdReadOnlyAsync(request.SupplierId, cancellationToken);
            var supplierValidation = ValidateSupplierForNewOrder(supplier);
            if (supplierValidation is not null)
            {
                return Failure<PurchaseOrderDetailsDto>(supplierValidation.Value.Code, supplierValidation.Value.Message);
            }

            var products = await LoadProductsAsync(request.Lines, cancellationToken);
            var productValidation = ValidateProductsForNewOrder(products, request.Lines.Select(line => line.ProductId));
            if (productValidation is not null)
            {
                return Failure<PurchaseOrderDetailsDto>(productValidation.Value.Code, productValidation.Value.Message);
            }

            purchaseOrder.UpdateDraftHeader(
                request.SupplierId,
                supplier!.Code,
                supplier.Name,
                supplier.TaxCode,
                request.OrderDate,
                request.ExpectedDeliveryDate,
                request.Notes,
                _clock.UtcNow);
            ApplyDraftLines(purchaseOrder, request.Lines, products!, _clock.UtcNow);

            await _auditRepository.AddAsync(
                CreateAudit(
                    purchaseOrder,
                    SecurityAuditAction.PurchaseOrderUpdated,
                    [new SecurityAuditChange("Nội dung", null, "Đã cập nhật Purchase Order nháp")]),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(MapDetails(purchaseOrder));
        }
        catch (DomainException exception)
        {
            return Failure<PurchaseOrderDetailsDto>(exception.Code, exception.Message);
        }
        catch (PersistenceConflictException exception)
        {
            return Failure<PurchaseOrderDetailsDto>(MapConflictCode(exception), MapConflictMessage(exception));
        }
    }

    public async Task<Result<PurchaseOrderDetailsDto>> MarkOrderedAsync(
        MarkPurchaseOrderOrderedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorId = RequireActorId();
        if (actorId.IsFailure)
            return Result.Failure<PurchaseOrderDetailsDto>(actorId.AppError);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var purchaseOrder = await _purchaseOrderRepository.GetByIdAsync(request.PurchaseOrderId, cancellationToken);
            if (purchaseOrder is null)
                return Failure<PurchaseOrderDetailsDto>(ErrorCodes.PurchaseOrders.NotFound, "Không tìm thấy Purchase Order.");
            if (!IsExpectedVersion(purchaseOrder.UpdatedAtUtc, request.ExpectedUpdatedAtUtc))
                return Conflict<PurchaseOrderDetailsDto>();

            var supplier = await _supplierRepository.GetByIdReadOnlyAsync(purchaseOrder.SupplierId, cancellationToken);
            var supplierValidation = ValidateSupplierForNewOrder(supplier);
            if (supplierValidation is not null)
                return Failure<PurchaseOrderDetailsDto>(supplierValidation.Value.Code, supplierValidation.Value.Message);

            var products = await LoadProductsForAggregateAsync(purchaseOrder, cancellationToken);
            var productValidation = ValidateProductsForNewOrder(products, purchaseOrder.Lines.Select(line => line.ProductId));
            if (productValidation is not null)
                return Failure<PurchaseOrderDetailsDto>(productValidation.Value.Code, productValidation.Value.Message);

            var snapshots = products!.ToDictionary(
                pair => pair.Key,
                pair => new PurchaseOrder.ProductSnapshot(
                    pair.Value.Code,
                    pair.Value.Name,
                    pair.Value.UnitName));
            purchaseOrder.FinalizeSnapshotsAndMarkOrdered(
                new PurchaseOrder.SupplierSnapshot(supplier!.Code, supplier.Name, supplier.TaxCode),
                snapshots,
                actorId.Value,
                _clock.UtcNow);

            await _auditRepository.AddAsync(
                CreateAudit(
                    purchaseOrder,
                    SecurityAuditAction.PurchaseOrderOrdered,
                    [new SecurityAuditChange("Trạng thái", "Nháp", "Đã đặt")]),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(MapDetails(purchaseOrder));
        }
        catch (DomainException exception)
        {
            return Failure<PurchaseOrderDetailsDto>(exception.Code, exception.Message);
        }
        catch (PersistenceConflictException exception)
        {
            return Failure<PurchaseOrderDetailsDto>(MapConflictCode(exception), MapConflictMessage(exception));
        }
    }

    public async Task<Result<PurchaseOrderDetailsDto>> AmendOrderedAsync(
        AmendOrderedPurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lineValidation = ValidateLineRequests(request.Lines);
        if (lineValidation is not null)
            return Failure<PurchaseOrderDetailsDto>(ErrorCodes.General.Validation, lineValidation);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var purchaseOrder = await _purchaseOrderRepository.GetByIdAsync(request.PurchaseOrderId, cancellationToken);
            if (purchaseOrder is null)
                return Failure<PurchaseOrderDetailsDto>(ErrorCodes.PurchaseOrders.NotFound, "Không tìm thấy Purchase Order.");
            if (!IsExpectedVersion(purchaseOrder.UpdatedAtUtc, request.ExpectedUpdatedAtUtc))
                return Conflict<PurchaseOrderDetailsDto>();

            purchaseOrder.ChangeOrderedHeader(request.ExpectedDeliveryDate, request.Notes, _clock.UtcNow);
            var requestedProducts = request.Lines.Select(line => line.ProductId).ToHashSet();
            foreach (var line in purchaseOrder.Lines.ToArray())
            {
                if (!requestedProducts.Contains(line.ProductId))
                    purchaseOrder.RemoveLine(line, _clock.UtcNow);
            }

            foreach (var requestLine in request.Lines)
            {
                var line = purchaseOrder.Lines.SingleOrDefault(item => item.ProductId == requestLine.ProductId);
                if (line is null)
                {
                    return Failure<PurchaseOrderDetailsDto>(
                        ErrorCodes.PurchaseOrders.OrderedIdentityImmutable,
                        "Không được thêm sản phẩm mới vào Purchase Order đã đặt.");
                }

                purchaseOrder.AmendOrderedLine(
                    line,
                    requestLine.OrderedQuantity,
                    requestLine.AgreedUnitCost,
                    requestLine.SortOrder,
                    _clock.UtcNow);
            }

            await _auditRepository.AddAsync(
                CreateAudit(
                    purchaseOrder,
                    SecurityAuditAction.PurchaseOrderUpdated,
                    [new SecurityAuditChange("Nội dung", null, "Đã amendment Purchase Order")]),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(MapDetails(purchaseOrder));
        }
        catch (DomainException exception)
        {
            return Failure<PurchaseOrderDetailsDto>(exception.Code, exception.Message);
        }
        catch (PersistenceConflictException exception)
        {
            return Failure<PurchaseOrderDetailsDto>(MapConflictCode(exception), MapConflictMessage(exception));
        }
    }

    public async Task<Result<PurchaseOrderDetailsDto>> CancelAsync(
        CancelPurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorId = RequireActorId();
        if (actorId.IsFailure)
            return Result.Failure<PurchaseOrderDetailsDto>(actorId.AppError);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var purchaseOrder = await _purchaseOrderRepository.GetByIdAsync(request.PurchaseOrderId, cancellationToken);
            if (purchaseOrder is null)
                return Failure<PurchaseOrderDetailsDto>(ErrorCodes.PurchaseOrders.NotFound, "Không tìm thấy Purchase Order.");
            if (!IsExpectedVersion(purchaseOrder.UpdatedAtUtc, request.ExpectedUpdatedAtUtc))
                return Conflict<PurchaseOrderDetailsDto>();

            purchaseOrder.Cancel(request.Reason, actorId.Value, _clock.UtcNow);
            await _auditRepository.AddAsync(
                CreateAudit(
                    purchaseOrder,
                    SecurityAuditAction.PurchaseOrderCancelled,
                    [new SecurityAuditChange("Trạng thái", null, "Đã hủy")]),
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(MapDetails(purchaseOrder));
        }
        catch (DomainException exception)
        {
            return Failure<PurchaseOrderDetailsDto>(exception.Code, exception.Message);
        }
        catch (PersistenceConflictException exception)
        {
            return Failure<PurchaseOrderDetailsDto>(MapConflictCode(exception), MapConflictMessage(exception));
        }
    }

    private PurchaseOrder CreateAggregate(
        string orderNumber,
        CreatePurchaseOrderRequest request,
        Supplier supplier,
        IReadOnlyDictionary<int, Product> products)
    {
        var purchaseOrder = new PurchaseOrder(
            orderNumber,
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.TaxCode,
            request.OrderDate,
            request.ExpectedDeliveryDate,
            request.Notes,
            _clock.UtcNow);
        ApplyDraftLines(purchaseOrder, request.Lines, products, _clock.UtcNow);
        return purchaseOrder;
    }

    private static void ApplyDraftLines(
        PurchaseOrder purchaseOrder,
        IReadOnlyCollection<PurchaseOrderLineRequest> requests,
        IReadOnlyDictionary<int, Product> products,
        DateTimeOffset utcNow)
    {
        var requestedProducts = requests.Select(line => line.ProductId).ToHashSet();
        foreach (var line in purchaseOrder.Lines.ToArray())
        {
            if (!requestedProducts.Contains(line.ProductId))
                purchaseOrder.RemoveLine(line, utcNow);
        }

        foreach (var request in requests.OrderBy(line => line.SortOrder))
        {
            if (!products.TryGetValue(request.ProductId, out var product))
                throw new DomainException("PURCHASE_ORDER.PRODUCT_NOT_FOUND", "Không tìm thấy sản phẩm.");

            var existing = purchaseOrder.Lines.SingleOrDefault(line => line.ProductId == request.ProductId);
            if (existing is null)
            {
                purchaseOrder.AddLine(
                    product.Id,
                    product.Code,
                    product.Name,
                    product.UnitName,
                    request.OrderedQuantity,
                    request.AgreedUnitCost,
                    request.SortOrder,
                    utcNow);
            }
            else
            {
                purchaseOrder.UpdateDraftLine(
                    existing,
                    product.Code,
                    product.Name,
                    product.UnitName,
                    request.OrderedQuantity,
                    request.AgreedUnitCost,
                    request.SortOrder,
                    utcNow);
            }
        }
    }

    private async Task<IReadOnlyDictionary<int, Product>> LoadProductsAsync(
        IReadOnlyCollection<PurchaseOrderLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var products = new Dictionary<int, Product>();
        foreach (var line in lines)
        {
            var product = await _productRepository.GetByIdReadOnlyAsync(line.ProductId, cancellationToken);
            if (product is not null) products[product.Id] = product;
        }

        return products;
    }

    private async Task<IReadOnlyDictionary<int, Product>> LoadProductsForAggregateAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken)
    {
        var products = new Dictionary<int, Product>();
        foreach (var line in purchaseOrder.Lines)
        {
            var product = await _productRepository.GetByIdReadOnlyAsync(line.ProductId, cancellationToken);
            if (product is not null) products[product.Id] = product;
        }

        return products;
    }

    private static (string Code, string Message)? ValidateSupplierForNewOrder(Supplier? supplier)
    {
        if (supplier is null)
            return (ErrorCodes.Suppliers.NotFound, "Không tìm thấy nhà cung cấp.");
        if (!supplier.IsActive)
            return (ErrorCodes.PurchaseOrders.SupplierInactive, "Nhà cung cấp đã ngừng hoạt động.");
        return null;
    }

    private static (string Code, string Message)? ValidateProductsForNewOrder(
        IReadOnlyDictionary<int, Product> products,
        IEnumerable<int> productIds)
    {
        foreach (var productId in productIds)
        {
            if (!products.TryGetValue(productId, out var product))
                return (ErrorCodes.PurchaseOrders.ProductInactive, "Không tìm thấy sản phẩm.");
            if (!product.IsActive)
                return (ErrorCodes.PurchaseOrders.ProductInactive, "Sản phẩm đã ngừng bán.");
            if (product.IsArchived)
                return (ErrorCodes.PurchaseOrders.ProductArchived, "Sản phẩm đã lưu trữ.");
            if (!product.TrackInventory)
                return (ErrorCodes.PurchaseOrders.ProductNotTracked, "Sản phẩm không theo dõi tồn kho.");
        }

        return null;
    }

    private static string? ValidateLineRequests(IReadOnlyCollection<PurchaseOrderLineRequest>? lines)
    {
        if (lines is null || lines.Count == 0)
            return "Purchase Order phải có ít nhất một dòng.";
        if (lines.Count > POS.Domain.Constants.BusinessRules.PurchaseOrders.MaximumLines)
            return "Purchase Order vượt quá số dòng cho phép.";
        if (lines.GroupBy(line => line.ProductId).Any(group => group.Count() > 1))
            return "Mỗi sản phẩm chỉ được xuất hiện một lần trong Purchase Order.";
        return null;
    }

    private SecurityAuditEvent CreateAudit(
        PurchaseOrder purchaseOrder,
        SecurityAuditAction action,
        IReadOnlyList<SecurityAuditChange> changes) =>
        new(
            _currentUserService.UserId,
            null,
            null,
            action,
            "Success",
            Guid.NewGuid(),
            _clock.UtcNow,
            _currentUserService.FullName,
            $"{purchaseOrder.OrderNumber} — {purchaseOrder.SupplierName}",
            BusinessArea,
            TargetType,
            _terminalIdentityProvider?.TerminalId ?? "TERM-UNKNOWN",
            changes);

    private static PurchaseOrderListItemDto MapList(PurchaseOrder purchaseOrder) =>
        new(
            purchaseOrder.Id,
            purchaseOrder.OrderNumber,
            purchaseOrder.SupplierId,
            purchaseOrder.SupplierCode,
            purchaseOrder.SupplierName,
            purchaseOrder.OrderDate,
            purchaseOrder.ExpectedDeliveryDate,
            purchaseOrder.Status,
            purchaseOrder.Lines.Count,
            TotalQuantity(purchaseOrder),
            purchaseOrder.GrandTotal,
            purchaseOrder.CreatedAtUtc,
            purchaseOrder.UpdatedAtUtc);

    private static PurchaseOrderDetailsDto MapDetails(PurchaseOrder purchaseOrder)
    {
        var lines = purchaseOrder.Lines
            .OrderBy(line => line.SortOrder)
            .ThenBy(line => line.Id)
            .Select(line => new PurchaseOrderLineDto(
                line.Id,
                line.ProductId,
                line.ProductCode,
                line.ProductName,
                line.UnitName,
                line.OrderedQuantity,
                line.ReceivedQuantity,
                line.AgreedUnitCost,
                line.LineTotal,
                line.SortOrder))
            .ToArray();
        return new PurchaseOrderDetailsDto(
            purchaseOrder.Id,
            purchaseOrder.OrderNumber,
            purchaseOrder.SupplierId,
            purchaseOrder.SupplierCode,
            purchaseOrder.SupplierName,
            purchaseOrder.SupplierTaxCode,
            purchaseOrder.OrderDate,
            purchaseOrder.ExpectedDeliveryDate,
            purchaseOrder.Notes,
            purchaseOrder.Status,
            purchaseOrder.OrderedAtUtc,
            purchaseOrder.OrderedByUserId,
            purchaseOrder.CancelledAtUtc,
            purchaseOrder.CancelledByUserId,
            purchaseOrder.CancellationReason,
            lines,
            SafeSumQuantities(lines.Select(line => line.OrderedQuantity)),
            SafeSumQuantities(lines.Select(line => line.ReceivedQuantity)),
            purchaseOrder.GrandTotal,
            purchaseOrder.CreatedAtUtc,
            purchaseOrder.UpdatedAtUtc);
    }

    private static long TotalQuantity(PurchaseOrder purchaseOrder) =>
        SafeSumQuantities(purchaseOrder.Lines.Select(line => line.OrderedQuantity));

    private static long SafeSumQuantities(IEnumerable<int> values)
    {
        try
        {
            var total = 0L;
            foreach (var value in values)
                total = checked(total + value);
            return total;
        }
        catch (OverflowException exception)
        {
            throw new DomainException(
                "PURCHASE_ORDER.TOTAL_QUANTITY_OVERFLOW",
                "Tổng số lượng Purchase Order vượt quá giới hạn.",
                exception);
        }
    }

    private Result<int> RequireActorId()
    {
        return _currentUserService.UserId is > 0
            ? Result.Success(_currentUserService.UserId.Value)
            : Result.Failure<int>(new AppError(ErrorCodes.General.Unauthorized, "Phiên đăng nhập hiện tại không hợp lệ."));
    }

    private static bool IsExpectedVersion(DateTimeOffset actual, DateTimeOffset expected) =>
        expected != default && actual.ToUniversalTime() == expected.ToUniversalTime();

    private static Result<T> Conflict<T>() =>
        Failure<T>(ErrorCodes.PurchaseOrders.ConcurrencyConflict, "Purchase Order đã được thay đổi. Vui lòng tải lại rồi thử lại.");

    private static string MapConflictCode(PersistenceConflictException exception) =>
        exception.Kind == PersistenceConflictKind.Concurrency
            ? ErrorCodes.PurchaseOrders.ConcurrencyConflict
            : exception.Target == PersistenceConflictTargets.PurchaseOrderNumber
                ? ErrorCodes.PurchaseOrders.NumberAlreadyExists
                : ErrorCodes.PurchaseOrders.PersistenceConflict;

    private static string MapConflictMessage(PersistenceConflictException exception) =>
        exception.Kind == PersistenceConflictKind.Concurrency
            ? "Purchase Order đã được thay đổi. Vui lòng tải lại rồi thử lại."
            : exception.Target == PersistenceConflictTargets.PurchaseOrderNumber
                ? "Số Purchase Order đã tồn tại."
                : "Không thể lưu Purchase Order do dữ liệu đang xung đột.";

    private static Result<T> Failure<T>(string code, string message) =>
        Result.Failure<T>(new AppError(code, message));
}
