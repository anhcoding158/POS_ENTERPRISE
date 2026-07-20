using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.DTOs.Products;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Integration test cho việc tạo sản phẩm và tồn đầu kỳ.
///
/// Test sử dụng SQLite thật trong bộ nhớ để kiểm tra:
/// - Product và OpeningBalance được commit cùng nhau;
/// - tồn âm hợp lệ khi được cho phép;
/// - tồn bằng 0 không tạo movement rỗng;
/// - lỗi ở lần lưu thứ hai rollback toàn bộ transaction;
/// - Product Update không được thay đổi tồn kho.
/// </summary>
public sealed class ProductOpeningBalanceIntegrationTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                20,
                15,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task Create_with_positive_stock_must_create_one_opening_balance()
    {
        await using var database =
            await OpeningBalanceTestDatabase
                .CreateAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    new EfUnitOfWork(context));

            var result =
                await service.CreateAsync(
                    CreateRequest(
                        code: "OPEN-001",
                        initialStock: 25,
                        allowNegativeStock: false));

            Assert.True(
                result.IsSuccess,
                result.Error.ToString());

            Assert.True(
                result.Value.Id > 0);

            Assert.Equal(
                25,
                result.Value.StockQuantity);
        }

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext
                .Products
                .AsNoTracking()
                .SingleAsync();

        var movement =
            await verifyContext
                .InventoryMovements
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            product.Id,
            movement.ProductId);

        Assert.Equal(
            InventoryMovementType.OpeningBalance,
            movement.MovementType);

        Assert.Equal(
            0,
            movement.QuantityBefore);

        Assert.Equal(
            25,
            movement.QuantityDelta);

        Assert.Equal(
            25,
            movement.QuantityAfter);

        Assert.Equal(
            25,
            product.StockQuantity);

        Assert.Equal(
            "Tồn đầu kỳ khi tạo sản phẩm.",
            movement.Reason);

        Assert.Equal(
            CreatedAtUtc,
            movement.OccurredAtUtc);
    }

    [Fact]
    public async Task Create_with_negative_stock_must_work_when_allowed()
    {
        await using var database =
            await OpeningBalanceTestDatabase
                .CreateAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    new EfUnitOfWork(context));

            var result =
                await service.CreateAsync(
                    CreateRequest(
                        code: "OPEN-NEGATIVE",
                        initialStock: -4,
                        allowNegativeStock: true));

            Assert.True(
                result.IsSuccess,
                result.Error.ToString());

            Assert.Equal(
                -4,
                result.Value.StockQuantity);

            Assert.True(
                result.Value.AllowNegativeStock);
        }

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext
                .Products
                .AsNoTracking()
                .SingleAsync();

        var movement =
            await verifyContext
                .InventoryMovements
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            -4,
            product.StockQuantity);

        Assert.Equal(
            InventoryMovementType.OpeningBalance,
            movement.MovementType);

        Assert.Equal(
            0,
            movement.QuantityBefore);

        Assert.Equal(
            -4,
            movement.QuantityDelta);

        Assert.Equal(
            -4,
            movement.QuantityAfter);
    }

    [Fact]
    public async Task Create_with_zero_stock_must_not_create_empty_movement()
    {
        await using var database =
            await OpeningBalanceTestDatabase
                .CreateAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    new EfUnitOfWork(context));

            var result =
                await service.CreateAsync(
                    CreateRequest(
                        code: "OPEN-ZERO",
                        initialStock: 0,
                        allowNegativeStock: false));

            Assert.True(
                result.IsSuccess,
                result.Error.ToString());

            Assert.Equal(
                0,
                result.Value.StockQuantity);
        }

        await using var verifyContext =
            database.CreateContext();

        var productCount =
            await verifyContext
                .Products
                .CountAsync();

        var movementCount =
            await verifyContext
                .InventoryMovements
                .CountAsync();

        Assert.Equal(
            1,
            productCount);

        Assert.Equal(
            0,
            movementCount);
    }

    [Fact]
    public async Task Non_tracked_product_must_reject_non_zero_initial_stock()
    {
        await using var database =
            await OpeningBalanceTestDatabase
                .CreateAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    new EfUnitOfWork(context));

            var request =
                new CreateProductRequest(
                    categoryId: 1,
                    code: "NO-TRACK-STOCK",
                    name: "Sản phẩm không theo dõi kho",
                    unitName: "Cái",
                    costPrice: 10_000,
                    salePrice: 15_000,
                    initialStockQuantity: 5,
                    minimumStock: 0,
                    trackInventory: false,
                    allowNegativeStock: false);

            var result =
                await service.CreateAsync(
                    request);

            Assert.True(
                result.IsFailure);

            Assert.Equal(
                "GENERAL.VALIDATION",
                result.Error.Code);
        }

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            0,
            await verifyContext.Products.CountAsync());

        Assert.Equal(
            0,
            await verifyContext
                .InventoryMovements
                .CountAsync());
    }

    [Fact]
    public async Task Failure_after_second_save_must_rollback_product_and_movement()
    {
        await using var database =
            await OpeningBalanceTestDatabase
                .CreateAsync();

        await using (
            var context =
                database.CreateContext())
        {
            var regularUnitOfWork =
                new EfUnitOfWork(
                    context);

            var failingUnitOfWork =
                new FailAfterSecondSaveUnitOfWork(
                    regularUnitOfWork);

            var service =
                CreateService(
                    context,
                    failingUnitOfWork);

            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    () =>
                        service.CreateAsync(
                            CreateRequest(
                                code: "OPEN-ROLLBACK",
                                initialStock: 12,
                                allowNegativeStock: false)));
        }

        await using var verifyContext =
            database.CreateContext();

        var productCount =
            await verifyContext
                .Products
                .CountAsync();

        var movementCount =
            await verifyContext
                .InventoryMovements
                .CountAsync();

        Assert.Equal(
            0,
            productCount);

        Assert.Equal(
            0,
            movementCount);
    }

    [Fact]
    public async Task Update_product_must_preserve_stock_and_opening_history()
    {
        await using var database =
            await OpeningBalanceTestDatabase
                .CreateAsync();

        int productId;

        await using (
            var createContext =
                database.CreateContext())
        {
            var createService =
                CreateService(
                    createContext,
                    new EfUnitOfWork(
                        createContext));

            var createResult =
                await createService.CreateAsync(
                    CreateRequest(
                        code: "OPEN-EDIT",
                        initialStock: 18,
                        allowNegativeStock: false));

            Assert.True(
                createResult.IsSuccess,
                createResult.Error.ToString());

            productId =
                createResult.Value.Id;
        }

        await using (
            var updateContext =
                database.CreateContext())
        {
            var updateService =
                CreateService(
                    updateContext,
                    new EfUnitOfWork(
                        updateContext));

            var updateRequest =
                new UpdateProductRequest(
                    productId,
                    categoryId: 1,
                    code: "OPEN-EDIT",
                    name: "Sản phẩm đã đổi tên",
                    unitName: "Hộp",
                    costPrice: 12_000,
                    salePrice: 19_000,
                    minimumStock: 3,
                    trackInventory: true,
                    allowNegativeStock: false,
                    isActive: true,
                    barcode: "8930000000001",
                    description:
                        "Cập nhật thông tin nhưng không sửa tồn kho.");

            var updateResult =
                await updateService.UpdateAsync(
                    updateRequest);

            Assert.True(
                updateResult.IsSuccess,
                updateResult.Error.ToString());

            Assert.Equal(
                18,
                updateResult.Value.StockQuantity);

            Assert.Equal(
                "Sản phẩm đã đổi tên",
                updateResult.Value.Name);

            Assert.Equal(
                "Hộp",
                updateResult.Value.UnitName);
        }

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext
                .Products
                .AsNoTracking()
                .SingleAsync();

        var movements =
            await verifyContext
                .InventoryMovements
                .AsNoTracking()
                .ToListAsync();

        Assert.Equal(
            18,
            product.StockQuantity);

        Assert.Equal(
            "Sản phẩm đã đổi tên",
            product.Name);

        Assert.Single(
            movements);

        Assert.Equal(
            InventoryMovementType.OpeningBalance,
            movements[0].MovementType);

        Assert.Equal(
            18,
            movements[0].QuantityAfter);
    }

    private static ProductService CreateService(
        PosDbContext context,
        IUnitOfWork unitOfWork)
    {
        return new ProductService(
            new ProductRepository(
                context),

            new CategoryRepository(
                context),

            new InventoryMovementRepository(
                context),

            unitOfWork,

            new FixedClock(
                CreatedAtUtc));
    }

    private static CreateProductRequest
        CreateRequest(
            string code,
            int initialStock,
            bool allowNegativeStock)
    {
        return new CreateProductRequest(
            categoryId: 1,
            code,
            name: $"Sản phẩm {code}",
            unitName: "Cái",
            costPrice: 10_000,
            salePrice: 15_000,
            initialStockQuantity:
                initialStock,
            minimumStock: 2,
            trackInventory: true,
            allowNegativeStock);
    }

    private sealed class FixedClock :
        IClock
    {
        public FixedClock(
            DateTimeOffset utcNow)
        {
            UtcNow =
                utcNow.ToUniversalTime();
        }

        public DateTimeOffset UtcNow { get; }
    }

    /// <summary>
    /// Cho phép lần SaveChanges đầu tiên thành công để Product
    /// nhận Id, sau đó lưu Product + OpeningBalance rồi phát sinh
    /// lỗi giả lập.
    ///
    /// Transaction phải rollback cả hai lần SaveChanges.
    /// </summary>
    private sealed class
        FailAfterSecondSaveUnitOfWork :
            IUnitOfWork
    {
        private readonly IUnitOfWork
            _inner;

        private int _saveCount;

        public FailAfterSecondSaveUnitOfWork(
            IUnitOfWork inner)
        {
            _inner =
                inner ??
                throw new ArgumentNullException(
                    nameof(inner));
        }

        public async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            _saveCount++;

            var affectedRows =
                await _inner.SaveChangesAsync(
                    cancellationToken);

            if (_saveCount == 2)
            {
                throw new InvalidOperationException(
                    "Synthetic failure after the second SaveChanges.");
            }

            return affectedRows;
        }

        public Task<IApplicationTransaction>
            BeginTransactionAsync(
                CancellationToken cancellationToken = default)
        {
            return _inner.BeginTransactionAsync(
                cancellationToken);
        }
    }

    /// <summary>
    /// SQLite database thật trong bộ nhớ.
    ///
    /// Một connection được giữ mở trong toàn bộ test để dữ liệu
    /// không bị mất giữa các DbContext.
    /// </summary>
    private sealed class
        OpeningBalanceTestDatabase :
            IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly
            DbContextOptions<PosDbContext>
            _options;

        private OpeningBalanceTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection =
                connection;

            _options =
                options;
        }

        public static async Task<
            OpeningBalanceTestDatabase>
            CreateAsync()
        {
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:;" +
                    "Foreign Keys=True");

            await connection.OpenAsync();

            var options =
                new DbContextOptionsBuilder<
                        PosDbContext>()
                    .UseSqlite(
                        connection)
                    .AddInterceptors(
                        new TestConcurrencyTokenInterceptor())
                    .EnableDetailedErrors()
                    .Options;

            var database =
                new OpeningBalanceTestDatabase(
                    connection,
                    options);

            await using (
                var context =
                    database.CreateContext())
            {
                await context.Database
                    .EnsureCreatedAsync();
            }

            await database
                .SeedActiveCategoryAsync();

            return database;
        }

        public PosDbContext CreateContext()
        {
            return new PosDbContext(
                _options);
        }

        private async Task SeedActiveCategoryAsync()
        {
            await using var command =
                _connection.CreateCommand();

            command.CommandText =
                """
                INSERT INTO "Categories"
                (
                    "Id",
                    "ConcurrencyToken",
                    "CreatedAtUtc",
                    "UpdatedAtUtc",
                    "Name",
                    "Description",
                    "DisplayOrder",
                    "IsActive"
                )
                VALUES
                (
                    1,
                    $token,
                    $createdAtUtc,
                    $updatedAtUtc,
                    $name,
                    NULL,
                    1,
                    1
                );
                """;

            var unixMilliseconds =
                CreatedAtUtc
                    .ToUnixTimeMilliseconds();

            command.Parameters.AddWithValue(
                "$token",
                Guid.NewGuid()
                    .ToString("D"));

            command.Parameters.AddWithValue(
                "$createdAtUtc",
                unixMilliseconds);

            command.Parameters.AddWithValue(
                "$updatedAtUtc",
                unixMilliseconds);

            command.Parameters.AddWithValue(
                "$name",
                "Đồ uống");

            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }

    /// <summary>
    /// Mô phỏng cơ chế concurrency token của production.
    /// </summary>
    private sealed class
        TestConcurrencyTokenInterceptor :
            SaveChangesInterceptor
    {
        public override
            InterceptionResult<int>
            SavingChanges(
                DbContextEventData eventData,
                InterceptionResult<int> result)
        {
            ApplyConcurrencyTokens(
                eventData.Context);

            return result;
        }

        public override
            ValueTask<InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            ApplyConcurrencyTokens(
                eventData.Context);

            return ValueTask.FromResult(
                result);
        }

        private static void ApplyConcurrencyTokens(
            DbContext? dbContext)
        {
            if (dbContext is null)
            {
                return;
            }

            var entries =
                dbContext
                    .ChangeTracker
                    .Entries<AuditableEntity>()
                    .Where(
                        entry =>
                            entry.State is
                                EntityState.Added or
                                EntityState.Modified);

            foreach (var entry in entries)
            {
                SetConcurrencyToken(
                    entry);
            }
        }

        private static void SetConcurrencyToken(
            EntityEntry<AuditableEntity> entry)
        {
            var tokenProperty =
                entry.Properties
                    .Single(
                        property =>
                            property.Metadata
                                .IsConcurrencyToken &&
                            property.Metadata.ClrType ==
                                typeof(Guid));

            tokenProperty.CurrentValue =
                Guid.NewGuid();
        }
    }
}