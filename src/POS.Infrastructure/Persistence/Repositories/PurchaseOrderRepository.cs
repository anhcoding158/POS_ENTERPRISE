using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private const string LikeEscapeCharacter = "\\";
    private readonly PosDbContext _dbContext;

    public PurchaseOrderRepository(PosDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<PurchaseOrder?> GetByIdAsync(
        int purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        purchaseOrderId <= 0
            ? Task.FromResult<PurchaseOrder?>(null)
            : _dbContext.PurchaseOrders
                .Include(order => order.Lines)
                .SingleOrDefaultAsync(order => order.Id == purchaseOrderId, cancellationToken);

    public Task<PurchaseOrder?> GetByIdReadOnlyAsync(
        int purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        purchaseOrderId <= 0
            ? Task.FromResult<PurchaseOrder?>(null)
            : _dbContext.PurchaseOrders
                .AsNoTracking()
                .Include(order => order.Lines)
                .SingleOrDefaultAsync(order => order.Id == purchaseOrderId, cancellationToken);

    public async Task<PagedResult<PurchaseOrder>> SearchAsync(
        string? searchTerm,
        PurchaseOrderStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip = CalculateSkip(pageNumber, pageSize);
        IQueryable<PurchaseOrder> query = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(order => order.Lines);
        var normalizedSearchTerm = NormalizeOptional(searchTerm);
        if (normalizedSearchTerm is not null)
        {
            var pattern = BuildContainsPattern(normalizedSearchTerm.ToUpperInvariant());
            query = query.Where(order =>
                EF.Functions.Like(order.NormalizedOrderNumber, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(order.SupplierCode, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(order.SupplierName, pattern, LikeEscapeCharacter));
        }

        if (status.HasValue)
            query = query.Where(order => order.Status == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.CreatedAtUtc)
            .ThenBy(order => order.NormalizedOrderNumber)
            .ThenBy(order => order.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<PurchaseOrder>(items, pageNumber, pageSize, totalCount);
    }

    public Task<bool> NormalizedOrderNumberExistsAsync(
        string normalizedOrderNumber,
        CancellationToken cancellationToken = default)
    {
        var normalized = normalizedOrderNumber.Trim().ToUpperInvariant();
        return _dbContext.PurchaseOrders
            .AsNoTracking()
            .AnyAsync(order => order.NormalizedOrderNumber == normalized, cancellationToken);
    }

    public async Task AddAsync(
        PurchaseOrder purchaseOrder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(purchaseOrder);
        await _dbContext.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken);
    }

    private static int CalculateSkip(int pageNumber, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, BusinessRules.PurchaseOrders.MaximumSearchPageSize);
        try { return checked((pageNumber - 1) * pageSize); }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(pageNumber)); }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildContainsPattern(string value) =>
        $"%{value.Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
            .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal)}%";
}
