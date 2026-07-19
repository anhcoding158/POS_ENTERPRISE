using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation của ICategoryRepository.
/// </summary>
public sealed class CategoryRepository :
    ICategoryRepository
{
    private const string LikeEscapeCharacter = "\\";

    private readonly PosDbContext _dbContext;

    public CategoryRepository(PosDbContext dbContext)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    /// <summary>
    /// Truy vấn có tracking vì entity có thể được
    /// chỉnh sửa và lưu bằng IUnitOfWork.
    /// </summary>
    public Task<Category?> GetByIdAsync(
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        if (categoryId <= 0)
        {
            return Task.FromResult<Category?>(null);
        }

        return _dbContext.Categories
            .SingleOrDefaultAsync(
                category =>
                    category.Id == categoryId,
                cancellationToken);
    }

    /// <summary>
    /// Truy vấn chỉ đọc nên không bật change tracking.
    /// </summary>
    public Task<Category?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var normalizedName =
            NormalizeRequiredText(
                name,
                nameof(name));

        return _dbContext.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                category =>
                    category.Name == normalizedName,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Category>>
        ListActiveAsync(
            CancellationToken cancellationToken = default)
    {
        var categories =
            await _dbContext.Categories
                .AsNoTracking()
                .Where(
                    category =>
                        category.IsActive)
                .OrderBy(
                    category =>
                        category.DisplayOrder)
                .ThenBy(
                    category =>
                        category.Name)
                .ThenBy(
                    category =>
                        category.Id)
                .ToListAsync(
                    cancellationToken);

        return categories;
    }

    public async Task<PagedResult<Category>> SearchAsync(
        string? searchTerm,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip =
            CalculateSkip(
                pageNumber,
                pageSize);

        IQueryable<Category> query =
            _dbContext.Categories
                .AsNoTracking();

        var normalizedSearchTerm =
            NormalizeOptionalText(searchTerm);

        if (normalizedSearchTerm is not null)
        {
            var pattern =
                BuildContainsPattern(
                    normalizedSearchTerm);

            query = query.Where(
                category =>
                    EF.Functions.Like(
                        category.Name,
                        pattern,
                        LikeEscapeCharacter) ||

                    (
                        category.Description != null &&
                        EF.Functions.Like(
                            category.Description,
                            pattern,
                            LikeEscapeCharacter)
                    ));
        }

        if (isActive.HasValue)
        {
            query = query.Where(
                category =>
                    category.IsActive ==
                    isActive.Value);
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var items =
            await query
                .OrderBy(
                    category =>
                        category.DisplayOrder)
                .ThenBy(
                    category =>
                        category.Name)
                .ThenBy(
                    category =>
                        category.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(
                    cancellationToken);

        return new PagedResult<Category>(
            items,
            pageNumber,
            pageSize,
            totalCount);
    }

    public Task<bool> NameExistsAsync(
        string name,
        int? excludeCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName =
            NormalizeRequiredText(
                name,
                nameof(name));

        IQueryable<Category> query =
            _dbContext.Categories
                .AsNoTracking()
                .Where(
                    category =>
                        category.Name ==
                        normalizedName);

        if (excludeCategoryId.HasValue)
        {
            query = query.Where(
                category =>
                    category.Id !=
                    excludeCategoryId.Value);
        }

        return query.AnyAsync(
            cancellationToken);
    }

    public async Task AddAsync(
        Category category,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        await _dbContext.Categories.AddAsync(
            category,
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
                "Số trang phải lớn hơn 0.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
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

    private static string BuildContainsPattern(
        string searchTerm)
    {
        var escaped = searchTerm
            .Replace(
                "\\",
                "\\\\",
                StringComparison.Ordinal)
            .Replace(
                "%",
                "\\%",
                StringComparison.Ordinal)
            .Replace(
                "_",
                "\\_",
                StringComparison.Ordinal);

        return $"%{escaped}%";
    }

    private static string NormalizeRequiredText(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Giá trị không được để trống.",
                parameterName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}