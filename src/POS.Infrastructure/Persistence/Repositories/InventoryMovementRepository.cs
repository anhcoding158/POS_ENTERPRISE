using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation của kho lịch sử tồn kho.
/// </summary>
public sealed class InventoryMovementRepository :
    IInventoryMovementRepository
{
    private readonly PosDbContext _dbContext;

    public InventoryMovementRepository(
        PosDbContext dbContext)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    public Task<InventoryMovement?> GetByIdAsync(
        int movementId,
        CancellationToken cancellationToken = default)
    {
        if (movementId <= 0)
        {
            return Task.FromResult<
                InventoryMovement?>(null);
        }

        return _dbContext
            .InventoryMovements
            .AsNoTracking()
            .Include(
                movement =>
                    movement.Product)
            .SingleOrDefaultAsync(
                movement =>
                    movement.Id == movementId,
                cancellationToken);
    }

    public async Task<PagedResult<InventoryMovement>>
        SearchAsync(
            int? productId,
            InventoryMovementType? movementType,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            string? referenceType,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var skip =
            CalculateSkip(
                pageNumber,
                pageSize);

        IQueryable<InventoryMovement> query =
            _dbContext
                .InventoryMovements
                .AsNoTracking();

        if (productId.HasValue)
        {
            if (productId.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(productId),
                    productId,
                    "Mã sản phẩm phải lớn hơn 0.");
            }

            query = query.Where(
                movement =>
                    movement.ProductId ==
                    productId.Value);
        }

        if (movementType.HasValue)
        {
            if (movementType.Value ==
                    InventoryMovementType.Unknown ||
                !Enum.IsDefined(
                    movementType.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(movementType),
                    movementType,
                    "Loại biến động kho không hợp lệ.");
            }

            query = query.Where(
                movement =>
                    movement.MovementType ==
                    movementType.Value);
        }

        if (fromUtc.HasValue)
        {
            var normalizedFrom =
                NormalizeUtc(
                    fromUtc.Value,
                    nameof(fromUtc));

            query = query.Where(
                movement =>
                    movement.OccurredAtUtc >=
                    normalizedFrom);
        }

        if (toUtc.HasValue)
        {
            var normalizedTo =
                NormalizeUtc(
                    toUtc.Value,
                    nameof(toUtc));

            query = query.Where(
                movement =>
                    movement.OccurredAtUtc <=
                    normalizedTo);
        }

        var normalizedReferenceType =
            NormalizeOptionalText(
                referenceType);

        if (normalizedReferenceType is not null)
        {
            normalizedReferenceType =
                normalizedReferenceType
                    .ToUpperInvariant();

            query = query.Where(
                movement =>
                    movement.ReferenceType ==
                    normalizedReferenceType);
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var items =
            await query
                .Include(
                    movement =>
                        movement.Product)
                .OrderByDescending(
                    movement =>
                        movement.OccurredAtUtc)
                .ThenByDescending(
                    movement =>
                        movement.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(
                    cancellationToken);

        return new PagedResult<InventoryMovement>(
            items,
            pageNumber,
            pageSize,
            totalCount);
    }

    public async Task AddAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            movement);

        await _dbContext
            .InventoryMovements
            .AddAsync(
                movement,
                cancellationToken);
    }

    private static int CalculateSkip(
        int pageNumber,
        int pageSize)
    {
        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                pageNumber,
                "Số trang phải lớn hơn 0.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                "Kích thước trang phải lớn hơn 0.");
        }

        try
        {
            return checked(
                (pageNumber - 1) *
                pageSize);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                pageNumber,
                "Vị trí phân trang vượt quá giới hạn.");
        }
    }

    private static DateTimeOffset NormalizeUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default)
        {
            throw new ArgumentException(
                "Thời điểm tìm kiếm không hợp lệ.",
                parameterName);
        }

        return value.ToUniversalTime();
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}