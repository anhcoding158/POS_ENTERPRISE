using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Abstractions.DateTime;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence;

/// <summary>
/// Khởi tạo database, áp dụng migration và tạo dữ liệu demo.
///
/// Quá trình seed được thiết kế idempotent:
/// chạy nhiều lần cũng không tạo dữ liệu trùng.
/// </summary>
public sealed class DatabaseInitializer
{
    private readonly PosDbContext _dbContext;
    private readonly InfrastructureOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        PosDbContext dbContext,
        IOptions<InfrastructureOptions> options,
        IClock clock,
        ILogger<DatabaseInitializer> logger)
    {
        _dbContext =
            dbContext ??
            throw new ArgumentNullException(
                nameof(dbContext));

        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));

        _logger =
            logger ??
            throw new ArgumentNullException(
                nameof(logger));
    }

    /// <summary>
    /// Chuẩn bị database để ứng dụng có thể hoạt động.
    /// </summary>
    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        _options.Validate();

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Bắt đầu khởi tạo database POS Enterprise.");

        if (_options.ApplyMigrationsOnStartup)
        {
            await ApplyMigrationsAsync(
                cancellationToken);
        }
        else
        {
            await EnsureDatabaseExistsAsync(
                cancellationToken);
        }

        if (_options.SeedDemoProductCatalog)
        {
            _logger.LogInformation(
                "Môi trường hiện tại cho phép tạo dữ liệu demo.");

            await SeedProductCatalogAsync(
                cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Đã bỏ qua dữ liệu demo vì " +
                "SeedDemoProductCatalog đang tắt.");
        }
        _logger.LogInformation(
            "Khởi tạo database POS Enterprise hoàn tất.");
    }

    private async Task ApplyMigrationsAsync(
        CancellationToken cancellationToken)
    {
        var pendingMigrations =
            await _dbContext.Database
                .GetPendingMigrationsAsync(
                    cancellationToken);

        var migrations =
            pendingMigrations.ToArray();

        if (migrations.Length == 0)
        {
            _logger.LogInformation(
                "Database không có migration đang chờ.");
        }
        else
        {
            _logger.LogInformation(
                "Đang áp dụng {MigrationCount} migration: {Migrations}",
                migrations.Length,
                string.Join(", ", migrations));
        }

        await _dbContext.Database.MigrateAsync(
            cancellationToken);
    }

    private async Task EnsureDatabaseExistsAsync(
        CancellationToken cancellationToken)
    {
        var canConnect =
            await _dbContext.Database.CanConnectAsync(
                cancellationToken);

        if (!canConnect)
        {
            throw new InvalidOperationException(
                "Database chưa tồn tại hoặc không thể kết nối. " +
                "Hãy bật ApplyMigrationsOnStartup hoặc chạy " +
                "dotnet ef database update.");
        }
    }

    private async Task SeedProductCatalogAsync(
        CancellationToken cancellationToken)
    {
        /*
         * Transaction bảo đảm danh mục và sản phẩm demo
         * được tạo trọn vẹn hoặc không tạo gì cả.
         */
        await using var transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            var categories =
                await EnsureDemoCategoriesAsync(
                    cancellationToken);

            await EnsureDemoProductsAsync(
                categories,
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            throw;
        }
    }

    private async Task<IReadOnlyDictionary<string, Category>>
        EnsureDemoCategoriesAsync(
            CancellationToken cancellationToken)
    {
        var existingCategories =
            await _dbContext.Categories
                .ToListAsync(
                    cancellationToken);

        var categoriesByName =
            existingCategories.ToDictionary(
                category => category.Name,
                StringComparer.OrdinalIgnoreCase);

        var utcNow = _clock.UtcNow;

        AddCategoryIfMissing(
            categoriesByName,
            "Đồ uống",
            "Cà phê, trà và các loại nước giải khát.",
            displayOrder: 10,
            utcNow);

        AddCategoryIfMissing(
            categoriesByName,
            "Đồ ăn nhanh",
            "Các món ăn phục vụ nhanh tại quầy.",
            displayOrder: 20,
            utcNow);

        AddCategoryIfMissing(
            categoriesByName,
            "Topping",
            "Các lựa chọn bổ sung cho đồ uống và món ăn.",
            displayOrder: 30,
            utcNow);

        var newCategories =
            categoriesByName.Values
                .Where(category => category.IsTransient)
                .ToArray();

        if (newCategories.Length > 0)
        {
            await _dbContext.Categories.AddRangeAsync(
                newCategories,
                cancellationToken);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "Đã tạo {CategoryCount} danh mục demo.",
                newCategories.Length);
        }

        return categoriesByName;
    }

    private async Task EnsureDemoProductsAsync(
        IReadOnlyDictionary<string, Category> categories,
        CancellationToken cancellationToken)
    {
        var existingProductCodes =
            await _dbContext.Products
                .AsNoTracking()
                .Select(product => product.Code)
                .ToListAsync(
                    cancellationToken);

        var productCodes =
            existingProductCodes.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        var products = new List<Product>();

        var utcNow = _clock.UtcNow;

        var beverageCategory =
            GetRequiredCategory(
                categories,
                "Đồ uống");

        var foodCategory =
            GetRequiredCategory(
                categories,
                "Đồ ăn nhanh");

        var toppingCategory =
            GetRequiredCategory(
                categories,
                "Topping");

        AddProductIfMissing(
            products,
            productCodes,
            beverageCategory.Id,
            code: "CF-DEN",
            barcode: "8938505970011",
            name: "Cà phê đen",
            description: "Cà phê đen truyền thống.",
            unitName: "Ly",
            costPrice: 10_000,
            salePrice: 25_000,
            stockQuantity: 100,
            minimumStock: 10,
            utcNow);

        AddProductIfMissing(
            products,
            productCodes,
            beverageCategory.Id,
            code: "CF-SUA",
            barcode: "8938505970028",
            name: "Cà phê sữa",
            description: "Cà phê sữa đá truyền thống.",
            unitName: "Ly",
            costPrice: 12_000,
            salePrice: 30_000,
            stockQuantity: 100,
            minimumStock: 10,
            utcNow);

        AddProductIfMissing(
            products,
            productCodes,
            beverageCategory.Id,
            code: "TRA-DAO",
            barcode: "8938505970035",
            name: "Trà đào",
            description: "Trà đào cam sả.",
            unitName: "Ly",
            costPrice: 15_000,
            salePrice: 35_000,
            stockQuantity: 80,
            minimumStock: 10,
            utcNow);

        AddProductIfMissing(
            products,
            productCodes,
            beverageCategory.Id,
            code: "TRA-TAC",
            barcode: "8938505970042",
            name: "Trà tắc",
            description: "Trà tắc thanh mát.",
            unitName: "Ly",
            costPrice: 8_000,
            salePrice: 20_000,
            stockQuantity: 80,
            minimumStock: 10,
            utcNow);

        AddProductIfMissing(
            products,
            productCodes,
            beverageCategory.Id,
            code: "NUOC-SUOI",
            barcode: "8938505970059",
            name: "Nước suối",
            description: "Nước uống đóng chai.",
            unitName: "Chai",
            costPrice: 5_000,
            salePrice: 10_000,
            stockQuantity: 120,
            minimumStock: 20,
            utcNow);

        AddProductIfMissing(
            products,
            productCodes,
            foodCategory.Id,
            code: "BANH-MI",
            barcode: "8938505970066",
            name: "Bánh mì thịt",
            description: "Bánh mì kẹp thịt và rau.",
            unitName: "Cái",
            costPrice: 13_000,
            salePrice: 25_000,
            stockQuantity: 40,
            minimumStock: 5,
            utcNow);

        AddProductIfMissing(
            products,
            productCodes,
            foodCategory.Id,
            code: "MI-XAO",
            barcode: "8938505970073",
            name: "Mì xào bò",
            description: "Mì xào bò và rau cải.",
            unitName: "Phần",
            costPrice: 25_000,
            salePrice: 45_000,
            stockQuantity: 30,
            minimumStock: 5,
            utcNow);

        AddProductIfMissing(
            products,
            productCodes,
            foodCategory.Id,
            code: "COM-GA",
            barcode: "8938505970080",
            name: "Cơm gà",
            description: "Cơm gà phục vụ tại quầy.",
            unitName: "Phần",
            costPrice: 28_000,
            salePrice: 50_000,
            stockQuantity: 30,
            minimumStock: 5,
            utcNow);

        AddProductIfMissing(
            products,
            productCodes,
            toppingCategory.Id,
            code: "TOP-TRAN-CHAU",
            barcode: null,
            name: "Trân châu",
            description: "Topping trân châu đen.",
            unitName: "Phần",
            costPrice: 3_000,
            salePrice: 8_000,
            stockQuantity: 100,
            minimumStock: 10,
            utcNow);

        AddProductIfMissing(
            products,
            productCodes,
            toppingCategory.Id,
            code: "TOP-THACH",
            barcode: null,
            name: "Thạch trái cây",
            description: "Topping thạch trái cây.",
            unitName: "Phần",
            costPrice: 3_000,
            salePrice: 8_000,
            stockQuantity: 100,
            minimumStock: 10,
            utcNow);

        if (products.Count == 0)
        {
            _logger.LogInformation(
                "Dữ liệu sản phẩm demo đã tồn tại.");

            return;
        }

        await _dbContext.Products.AddRangeAsync(
            products,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            "Đã tạo {ProductCount} sản phẩm demo.",
            products.Count);
    }

    private static void AddCategoryIfMissing(
        IDictionary<string, Category> categories,
        string name,
        string description,
        int displayOrder,
        DateTimeOffset utcNow)
    {
        if (categories.ContainsKey(name))
        {
            return;
        }

        var category =
            new Category(
                name,
                displayOrder,
                utcNow,
                description);

        categories.Add(
            name,
            category);
    }

    private static void AddProductIfMissing(
        ICollection<Product> products,
        ISet<string> productCodes,
        int categoryId,
        string code,
        string? barcode,
        string name,
        string description,
        string unitName,
        long costPrice,
        long salePrice,
        int stockQuantity,
        int minimumStock,
        DateTimeOffset utcNow)
    {
        if (!productCodes.Add(code))
        {
            return;
        }

        var product =
            new Product(
                categoryId,
                code,
                name,
                unitName,
                costPrice,
                salePrice,
                stockQuantity,
                minimumStock,
                trackInventory: true,
                allowNegativeStock: false,
                utcNow,
                barcode,
                description);

        products.Add(product);
    }

    private static Category GetRequiredCategory(
        IReadOnlyDictionary<string, Category> categories,
        string categoryName)
    {
        if (categories.TryGetValue(
                categoryName,
                out var category))
        {
            return category;
        }

        throw new InvalidOperationException(
            $"Không tìm thấy danh mục seed '{categoryName}'.");
    }
}