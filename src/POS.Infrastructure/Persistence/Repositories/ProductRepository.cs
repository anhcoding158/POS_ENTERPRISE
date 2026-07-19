using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation của IProductRepository.
/// </summary>
public sealed class ProductRepository :
    IProductRepository
{
    private const string LikeEscapeCharacter = "\\";

    private readonly PosDbContext _dbContext;

    public ProductRepository(PosDbContext dbContext)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));
    }

    /// <summary>
    /// Truy vấn có tracking vì ProductService và CheckoutService
    /// có thể thay đổi entity rồi gọi IUnitOfWork.
    /// </summary>
    public Task<Product?> GetByIdAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return Task.FromResult<Product?>(null);
        }

        return _dbContext.Products
            .Include(
                product =>
                    product.Category)
            .SingleOrDefaultAsync(
                product =>
                    product.Id == productId,
                cancellationToken);
    }

    public Task<Product?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode =
            NormalizeRequiredText(
                    code,
                    nameof(code))
                .ToUpperInvariant();

        return _dbContext.Products
            .AsNoTracking()
            .Include(
                product =>
                    product.Category)
            .SingleOrDefaultAsync(
                product =>
                    product.Code ==
                    normalizedCode,
                cancellationToken);
    }

    public Task<Product?> GetByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        var normalizedBarcode =
            NormalizeRequiredText(
                barcode,
                nameof(barcode));

        return _dbContext.Products
            .AsNoTracking()
            .Include(
                product =>
                    product.Category)
            .SingleOrDefaultAsync(
                product =>
                    product.Barcode ==
                    normalizedBarcode,
                cancellationToken);
    }

    public async Task<PagedResult<Product>> SearchAsync(
        string? searchTerm,
        int? categoryId,
        bool? isActive,
        bool? isLowStock,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip =
            CalculateSkip(
                pageNumber,
                pageSize);

        IQueryable<Product> query =
            _dbContext.Products
                .AsNoTracking();

        var normalizedSearchTerm =
            NormalizeOptionalText(searchTerm);

        if (normalizedSearchTerm is not null)
        {
            var pattern =
                BuildContainsPattern(
                    normalizedSearchTerm);

            query = query.Where(
                product =>
                    EF.Functions.Like(
                        product.Code,
                        pattern,
                        LikeEscapeCharacter) ||

                    EF.Functions.Like(
                        product.Name,
                        pattern,
                        LikeEscapeCharacter) ||

                    (
                        product.Barcode != null &&
                        EF.Functions.Like(
                            product.Barcode,
                            pattern,
                            LikeEscapeCharacter)
                    ));
        }

        if (categoryId.HasValue)
        {
            if (categoryId.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(categoryId),
                    "Mã danh mục phải lớn hơn 0.");
            }

            query = query.Where(
                product =>
                    product.CategoryId ==
                    categoryId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(
                product =>
                    product.IsActive ==
                    isActive.Value);
        }

        if (isLowStock.HasValue)
        {
            /*
             * Product.IsLowStock là thuộc tính tính toán
             * và đã bị Ignore trong EF mapping.
             *
             * Vì vậy truy vấn phải biểu diễn đúng công thức Domain:
             *
             * TrackInventory &&
             * StockQuantity <= MinimumStock
             */
            query = isLowStock.Value
                ? query.Where(
                    product =>
                        product.TrackInventory &&
                        product.StockQuantity <=
                        product.MinimumStock)

                : query.Where(
                    product =>
                        !product.TrackInventory ||
                        product.StockQuantity >
                        product.MinimumStock);
        }

        var totalCount =
            await query.CountAsync(
                cancellationToken);

        var items =
            await query
                .Include(
                    product =>
                        product.Category)
                .OrderByDescending(
                    product =>
                        product.IsActive)
                .ThenBy(
                    product =>
                        product.Name)
                .ThenBy(
                    product =>
                        product.Code)
                .ThenBy(
                    product =>
                        product.Id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(
                    cancellationToken);

        return new PagedResult<Product>(
            items,
            pageNumber,
            pageSize,
            totalCount);
    }

    public Task<bool> CodeExistsAsync(
        string code,
        int? excludeProductId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode =
            NormalizeRequiredText(
                    code,
                    nameof(code))
                .ToUpperInvariant();

        IQueryable<Product> query =
            _dbContext.Products
                .AsNoTracking()
                .Where(
                    product =>
                        product.Code ==
                        normalizedCode);

        if (excludeProductId.HasValue)
        {
            query = query.Where(
                product =>
                    product.Id !=
                    excludeProductId.Value);
        }

        return query.AnyAsync(
            cancellationToken);
    }

    public Task<bool> BarcodeExistsAsync(
        string barcode,
        int? excludeProductId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedBarcode =
            NormalizeRequiredText(
                barcode,
                nameof(barcode));

        IQueryable<Product> query =
            _dbContext.Products
                .AsNoTracking()
                .Where(
                    product =>
                        product.Barcode ==
                        normalizedBarcode);

        if (excludeProductId.HasValue)
        {
            query = query.Where(
                product =>
                    product.Id !=
                    excludeProductId.Value);
        }

        return query.AnyAsync(
            cancellationToken);
    }

    public async Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);

        await _dbContext.Products.AddAsync(
            product,
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
        /*
         * % và _ là wildcard của SQL LIKE.
         * Ta escape chúng để từ khóa của người dùng
         * được hiểu là văn bản bình thường.
         */
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