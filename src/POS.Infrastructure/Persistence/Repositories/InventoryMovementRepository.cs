using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Inventory;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation của kho lịch sử tồn kho.
/// </summary>
public sealed class InventoryMovementRepository :
    IInventoryMovementRepository
{
    private const string LikeEscapeCharacter = "\\";

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
            string? productSearchTerm = null,
            CancellationToken cancellationToken = default)
    {
        var skip =
            CalculateSkip(
                pageNumber,
                pageSize);

        var query = BuildSearchQuery(
            productId,
            movementType,
            fromUtc,
            toUtc,
            referenceType,
            productSearchTerm);

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

    public async Task<InventoryMovementSummaryDto> GetSummaryAsync(
        int? productId,
        InventoryMovementType? movementType,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? referenceType,
        string? productSearchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildSearchQuery(
            productId,
            movementType,
            fromUtc,
            toUtc,
            referenceType,
            productSearchTerm);

        var aggregate =
            await query
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    TotalCount = group.Count(),
                    IncreaseCount = group.Count(
                        movement => movement.QuantityDelta > 0),
                    DecreaseCount = group.Count(
                        movement => movement.QuantityDelta < 0),
                    NeutralCount = group.Count(
                        movement => movement.QuantityDelta == 0)
                })
                .SingleOrDefaultAsync(cancellationToken);

        return aggregate is null
            ? new InventoryMovementSummaryDto(0, 0, 0, 0)
            : new InventoryMovementSummaryDto(
                aggregate.TotalCount,
                aggregate.IncreaseCount,
                aggregate.DecreaseCount,
                aggregate.NeutralCount);
    }

    public async Task<IReadOnlyList<InventoryMovement>> ExportAsync(
        int? productId,
        InventoryMovementType? movementType,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? referenceType,
        string? productSearchTerm,
        int maximumRows,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRows);

        var query = BuildSearchQuery(
            productId,
            movementType,
            fromUtc,
            toUtc,
            referenceType,
            productSearchTerm);

        return await query
            .Include(movement => movement.Product)
            .OrderByDescending(movement => movement.OccurredAtUtc)
            .ThenByDescending(movement => movement.Id)
            .Take(maximumRows)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<InventoryMovement> BuildSearchQuery(
        int? productId,
        InventoryMovementType? movementType,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? referenceType,
        string? productSearchTerm)
    {
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
                movement => movement.ProductId == productId.Value);
        }

        var normalizedProductSearchTerm =
            NormalizeOptionalText(productSearchTerm);

        if (normalizedProductSearchTerm is not null)
        {
            var pattern =
                BuildContainsPattern(normalizedProductSearchTerm);

            query = query.Where(
                movement =>
                    movement.Product != null &&
                    (EF.Functions.Like(
                         movement.Product.Code,
                         pattern,
                         LikeEscapeCharacter) ||
                     EF.Functions.Like(
                         movement.Product.Name,
                         pattern,
                         LikeEscapeCharacter) ||
                     (movement.Product.Barcode != null &&
                      EF.Functions.Like(
                          movement.Product.Barcode,
                          pattern,
                          LikeEscapeCharacter))));
        }

        if (movementType.HasValue)
        {
            if (movementType.Value == InventoryMovementType.Unknown ||
                !Enum.IsDefined(movementType.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(movementType),
                    movementType,
                    "Loại biến động kho không hợp lệ.");
            }

            query = query.Where(
                movement => movement.MovementType == movementType.Value);
        }

        if (fromUtc.HasValue)
        {
            var normalizedFrom = NormalizeUtc(fromUtc.Value, nameof(fromUtc));
            query = query.Where(
                movement => movement.OccurredAtUtc >= normalizedFrom);
        }

        if (toUtc.HasValue)
        {
            var normalizedTo = NormalizeUtc(toUtc.Value, nameof(toUtc));
            query = query.Where(
                movement => movement.OccurredAtUtc <= normalizedTo);
        }

        var normalizedReferenceType = NormalizeOptionalText(referenceType);
        if (normalizedReferenceType is not null)
        {
            normalizedReferenceType = normalizedReferenceType.ToUpperInvariant();
            query = query.Where(
                movement => movement.ReferenceType == normalizedReferenceType);
        }

        return query;
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

    private static string BuildContainsPattern(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
