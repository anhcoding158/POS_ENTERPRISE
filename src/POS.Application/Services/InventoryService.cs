using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Inventory;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Services;

/// <summary>
/// Triển khai use case quản lý tồn kho.
///
/// Transaction boundary:
///
/// 1. đọc Product có tracking;
/// 2. mở transaction;
/// 3. thay đổi Product bằng phương thức Domain;
/// 4. tạo InventoryMovement;
/// 5. SaveChanges một lần;
/// 6. commit.
///
/// Product và InventoryMovement vì vậy luôn được lưu
/// hoặc rollback cùng nhau.
/// </summary>
public sealed class InventoryService : IInventoryService
{
    private readonly IProductRepository
        _productRepository;

    private readonly IInventoryMovementRepository
        _movementRepository;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly IClock
        _clock;

    public InventoryService(
        IProductRepository productRepository,
        IInventoryMovementRepository movementRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _productRepository =
            productRepository ??
            throw new ArgumentNullException(
                nameof(productRepository));

        _movementRepository =
            movementRepository ??
            throw new ArgumentNullException(
                nameof(movementRepository));

        _unitOfWork =
            unitOfWork ??
            throw new ArgumentNullException(
                nameof(unitOfWork));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));
    }

    public async Task<Result<InventoryAdjustmentResultDto>>
        AdjustAsync(
            InventoryAdjustmentRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var product =
            await _productRepository.GetByIdAsync(
                request.ProductId,
                cancellationToken);

        if (product is null)
        {
            return Result.Failure<
                InventoryAdjustmentResultDto>(
                new Error(
                    ErrorCodes.Inventory.ProductNotFound,
                    "Không tìm thấy sản phẩm cần điều chỉnh kho."));
        }

        if (!product.TrackInventory)
        {
            return Result.Failure<
                InventoryAdjustmentResultDto>(
                new Error(
                    ErrorCodes.Inventory.InventoryNotTracked,
                    "Sản phẩm này không theo dõi tồn kho."));
        }

        /*
         * Product được đọc trước khi mở transaction.
         *
         * Nếu một thao tác khác cập nhật Product trong khoảng
         * thời gian này, GUID concurrency token tại database
         * sẽ khiến SaveChanges thất bại thay vì ghi đè dữ liệu.
         */
        await using var transaction =
            await _unitOfWork.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var occurredAtUtc =
                _clock.UtcNow;

            var quantityBefore =
                product.StockQuantity;

            ApplyMovement(
                product,
                request,
                occurredAtUtc);

            var quantityAfter =
                product.StockQuantity;

            var quantityDelta =
                checked(
                    quantityAfter -
                    quantityBefore);

            var movement =
                new InventoryMovement(
                    product.Id,
                    request.MovementType,
                    quantityDelta,
                    quantityBefore,
                    quantityAfter,
                    request.Reason,
                    occurredAtUtc,
                    request.ReferenceType,
                    request.ReferenceId,
                    performedByUserId: null);

            await _movementRepository.AddAsync(
                movement,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            return Result.Success(
                MapToAdjustmentResult(
                    movement,
                    product));
        }
        catch (DomainException exception)
        {
            /*
             * Khi return khỏi await using mà transaction
             * chưa commit, EfApplicationTransaction tự rollback.
             */
            return Result.Failure<
                InventoryAdjustmentResultDto>(
                MapDomainError(
                    exception));
        }
        catch (
            PersistenceConflictException exception)
        {
            return Result.Failure<
                InventoryAdjustmentResultDto>(
                MapPersistenceConflict(
                    exception));
        }
    }

    public async Task<
        Result<PagedResult<InventoryMovementDto>>>
        SearchAsync(
            InventorySearchRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        var page =
            await _movementRepository.SearchAsync(
                request.ProductId,
                request.MovementType,
                request.FromUtc,
                request.ToUtc,
                request.ReferenceType,
                request.PageNumber,
                request.PageSize,
                cancellationToken);

        var items =
            page.Items
                .Select(MapToMovementDto)
                .ToArray();

        var resultPage =
            new PagedResult<InventoryMovementDto>(
                items,
                page.PageNumber,
                page.PageSize,
                page.TotalCount);

        return Result.Success(resultPage);
    }

    private static void ApplyMovement(
        Product product,
        InventoryAdjustmentRequest request,
        DateTimeOffset occurredAtUtc)
    {
        switch (request.MovementType)
        {
            case InventoryMovementType.StockIn:
                product.IncreaseStock(
                    request.Quantity,
                    occurredAtUtc);

                break;

            case InventoryMovementType.StockOut:
                product.DecreaseStock(
                    request.Quantity,
                    occurredAtUtc);

                break;

            case InventoryMovementType.Adjustment:
                if (request.Quantity > 0)
                {
                    product.IncreaseStock(
                        request.Quantity,
                        occurredAtUtc);
                }
                else
                {
                    /*
                     * InventoryAdjustmentRequest đã chặn
                     * int.MinValue và mọi delta vượt giới hạn,
                     * nên phép đổi dấu này an toàn.
                     */
                    product.DecreaseStock(
                        -request.Quantity,
                        occurredAtUtc);
                }

                break;

            case InventoryMovementType.Stocktake:
                product.ReconcileStock(
                    request.Quantity,
                    occurredAtUtc);

                break;

            default:
                throw new DomainException(
                    ErrorCodes.Inventory
                        .UnsupportedManualMovement,
                    "Loại biến động này không được thực hiện thủ công.");
        }
    }

    private static InventoryAdjustmentResultDto
        MapToAdjustmentResult(
            InventoryMovement movement,
            Product product)
    {
        return new InventoryAdjustmentResultDto(
            movement.Id,
            product.Id,
            product.Code,
            product.Name,
            product.UnitName,
            movement.MovementType,
            movement.QuantityBefore,
            movement.QuantityDelta,
            movement.QuantityAfter,
            movement.Reason,
            movement.ReferenceType,
            movement.ReferenceId,
            movement.PerformedByUserId,
            movement.OccurredAtUtc);
    }

    private static InventoryMovementDto
        MapToMovementDto(
            InventoryMovement movement)
    {
        var product =
            movement.Product;

        return new InventoryMovementDto(
            movement.Id,
            movement.ProductId,
            product?.Code ??
                string.Empty,
            product?.Name ??
                "Không xác định",
            product?.UnitName ??
                string.Empty,
            movement.MovementType,
            movement.QuantityBefore,
            movement.QuantityDelta,
            movement.QuantityAfter,
            movement.Reason,
            movement.ReferenceType,
            movement.ReferenceId,
            movement.PerformedByUserId,
            movement.OccurredAtUtc);
    }

    private static Error MapDomainError(
        DomainException exception)
    {
        if (string.Equals(
                exception.Code,
                "PRODUCT.INVENTORY_NOT_TRACKED",
                StringComparison.Ordinal))
        {
            return new Error(
                ErrorCodes.Inventory.InventoryNotTracked,
                exception.Message);
        }

        if (string.Equals(
                exception.Code,
                "PRODUCT.INSUFFICIENT_STOCK",
                StringComparison.Ordinal))
        {
            return new Error(
                ErrorCodes.Inventory.InsufficientStock,
                exception.Message);
        }

        if (exception.Code is
            "PRODUCT.STOCK_OVERFLOW" or
            "PRODUCT.STOCK_UNDERFLOW" or
            "PRODUCT.STOCK_TOO_LARGE" or
            "PRODUCT.STOCK_TOO_LOW" or
            "PRODUCT.NEGATIVE_STOCK_NOT_ALLOWED")
        {
            return new Error(
                ErrorCodes.Inventory.InvalidQuantity,
                exception.Message);
        }

        return new Error(
            exception.Code,
            exception.Message);
    }

    private static Error MapPersistenceConflict(
        PersistenceConflictException exception)
    {
        if (exception.Kind ==
            PersistenceConflictKind.Concurrency)
        {
            return new Error(
                ErrorCodes.Inventory.ConcurrencyConflict,
                "Tồn kho đã được một thao tác khác thay đổi. " +
                "Hãy tải lại sản phẩm rồi thực hiện lại.");
        }

        return new Error(
            ErrorCodes.Inventory.PersistenceConflict,
            "Không thể lưu biến động tồn kho do dữ liệu " +
            "đang xung đột. Hãy tải lại và thử lại.");
    }
}