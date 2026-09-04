using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private const string LikeEscapeCharacter = "\\";
    private readonly PosDbContext _dbContext;

    public SupplierRepository(PosDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public Task<Supplier?> GetByIdAsync(int supplierId, CancellationToken cancellationToken = default) =>
        supplierId <= 0
            ? Task.FromResult<Supplier?>(null)
            : _dbContext.Suppliers.SingleOrDefaultAsync(supplier => supplier.Id == supplierId, cancellationToken);

    public Task<Supplier?> GetByIdReadOnlyAsync(int supplierId, CancellationToken cancellationToken = default) =>
        supplierId <= 0
            ? Task.FromResult<Supplier?>(null)
            : _dbContext.Suppliers.AsNoTracking().SingleOrDefaultAsync(supplier => supplier.Id == supplierId, cancellationToken);

    public async Task<PagedResult<Supplier>> SearchAsync(
        string? searchTerm,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip = CalculateSkip(pageNumber, pageSize);
        IQueryable<Supplier> query = _dbContext.Suppliers.AsNoTracking();
        var normalizedSearchTerm = NormalizeOptional(searchTerm);
        if (normalizedSearchTerm is not null)
        {
            var pattern = BuildContainsPattern(normalizedSearchTerm);
            query = query.Where(supplier =>
                EF.Functions.Like(supplier.Code, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(supplier.Name, pattern, LikeEscapeCharacter) ||
                (supplier.TaxCode != null && EF.Functions.Like(supplier.TaxCode, pattern, LikeEscapeCharacter)) ||
                (supplier.ContactName != null && EF.Functions.Like(supplier.ContactName, pattern, LikeEscapeCharacter)) ||
                (supplier.PhoneNumber != null && EF.Functions.Like(supplier.PhoneNumber, pattern, LikeEscapeCharacter)));
        }

        if (isActive.HasValue)
            query = query.Where(supplier => supplier.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(supplier => supplier.IsActive)
            .ThenBy(supplier => supplier.Name)
            .ThenBy(supplier => supplier.Code)
            .ThenBy(supplier => supplier.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<Supplier>(items, pageNumber, pageSize, totalCount);
    }

    public Task<bool> NormalizedCodeExistsAsync(
        string normalizedCode,
        int? excludeSupplierId = null,
        CancellationToken cancellationToken = default)
    {
        var value = (normalizedCode ?? string.Empty).Trim().ToUpperInvariant();
        IQueryable<Supplier> query = _dbContext.Suppliers.AsNoTracking()
            .Where(supplier => supplier.NormalizedCode == value);
        if (excludeSupplierId.HasValue)
            query = query.Where(supplier => supplier.Id != excludeSupplierId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        await _dbContext.Suppliers.AddAsync(supplier, cancellationToken);
    }

    private static int CalculateSkip(int pageNumber, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, POS.Domain.Constants.BusinessRules.Suppliers.MaximumSearchPageSize);
        try { return checked((pageNumber - 1) * pageSize); }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(pageNumber)); }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildContainsPattern(string value) =>
        $"%{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
}
