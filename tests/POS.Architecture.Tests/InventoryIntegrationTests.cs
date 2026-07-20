using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Inventory;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Integration test dùng SQLite thật trong bộ nhớ.
///
/// Kiểm tra đồng thời:
/// - EF mapping;
/// - Product update;
/// - InventoryMovement insert;
/// - transaction rollback;
/// - optimistic concurrency;
/// - truy vấn lịch sử và phân trang.
/// </summary>
public sealed class InventoryIntegrationTests
{
    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                20,
                8,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task Stock_in_must_commit_product_and_movement_together()
    {
        await using var database =
            await InventoryTestDatabase
                .CreateAsync();

        var productId =
            await database.SeedProductAsync(
                initialStock: 10,
                allowNegativeStock: false);

        var clock =
            new FixedClock(
                CreatedAtUtc.AddHours(1));

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    clock);

            var result =
                await service.AdjustAsync(
                    new InventoryAdjustmentRequest(
                        productId,
                        InventoryMovementType.StockIn,
                        quantity: 5,
                        reason: "Nhập hàng kiểm thử"));

            Assert.True(
                result.IsSuccess,
                result.Error.ToString());

            Assert.Equal(
                10,
                result.Value.QuantityBefore);

            Assert.Equal(
                5,
                result.Value.QuantityDelta);

            Assert.Equal(
                15,
                result.Value.QuantityAfter);

            Assert.True(
                result.Value.MovementId > 0);
        }

        await using var verifyContext =
            database.CreateContext();

        var product =
            await verifyContext.Products
                .AsNoTracking()
                .SingleAsync(
                    item =>
                        item.Id == productId);

        var movement =
            await verifyContext
                .InventoryMovements
                .AsNoTracking()
                .SingleAsync();

        Assert.Equal(
            15,
            product.StockQuantity);

        Assert.Equal(
            productId,
            movement.ProductId);

        Assert.Equal(
            10,
            movement.QuantityBefore);

        Assert.Equal(
            15,
            movement.QuantityAfter);
    }

    [Fact]
    public async Task Stock_out_must_not_allow_negative_stock_when_disabled()
    {
        await using var database =
            await InventoryTestDatabase
                .CreateAsync();

        var productId =
            await database.SeedProductAsync(
                initialStock: 2,
                allowNegativeStock: false);

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    new FixedClock(
                        CreatedAtUtc.AddHours(1)));

            var result =
                await service.AdjustAsync(
                    new InventoryAdjustmentRequest(
                        productId,
                        InventoryMovementType.StockOut,
                        quantity: 5,
                        reason: "Xuất vượt tồn"));

            Assert.True(
                result.IsFailure);

            Assert.Equal(
                ErrorCodes.Inventory
                    .InsufficientStock,
                result.Error.Code);
        }

        await using var verifyContext =
            database.CreateContext();

        var stock =
            await verifyContext.Products
                .Where(
                    product =>
                        product.Id == productId)
                .Select(
                    product =>
                        product.StockQuantity)
                .SingleAsync();

        var movementCount =
            await verifyContext
                .InventoryMovements
                .CountAsync();

        Assert.Equal(
            2,
            stock);

        Assert.Equal(
            0,
            movementCount);
    }

    [Fact]
    public async Task Stock_out_may_create_negative_stock_when_enabled()
    {
        await using var database =
            await InventoryTestDatabase
                .CreateAsync();

        var productId =
            await database.SeedProductAsync(
                initialStock: 2,
                allowNegativeStock: true);

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    new FixedClock(
                        CreatedAtUtc.AddHours(1)));

            var result =
                await service.AdjustAsync(
                    new InventoryAdjustmentRequest(
                        productId,
                        InventoryMovementType.StockOut,
                        quantity: 5,
                        reason: "Cho phép bán âm"));

            Assert.True(
                result.IsSuccess,
                result.Error.ToString());

            Assert.Equal(
                -3,
                result.Value.QuantityAfter);
        }

        await using var verifyContext =
            database.CreateContext();

        var stock =
            await verifyContext.Products
                .Where(
                    product =>
                        product.Id == productId)
                .Select(
                    product =>
                        product.StockQuantity)
                .SingleAsync();

        Assert.Equal(
            -3,
            stock);
    }

    [Fact]
    public async Task Failure_after_save_must_rollback_product_and_movement()
    {
        await using var database =
            await InventoryTestDatabase
                .CreateAsync();

        var productId =
            await database.SeedProductAsync(
                initialStock: 10,
                allowNegativeStock: false);

        await using (
            var context =
                database.CreateContext())
        {
            var regularUnitOfWork =
                new EfUnitOfWork(
                    context);

            var failingUnitOfWork =
                new FailingAfterSaveUnitOfWork(
                    regularUnitOfWork);

            var service =
                new InventoryService(
                    new TrackingProductRepository(
                        context),
                    new InventoryMovementRepository(
                        context),
                    failingUnitOfWork,
                    new FixedClock(
                        CreatedAtUtc.AddHours(1)));

            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    () =>
                        service.AdjustAsync(
                            new InventoryAdjustmentRequest(
                                productId,
                                InventoryMovementType.StockIn,
                                quantity: 5,
                                reason: "Mô phỏng lỗi sau SaveChanges")));
        }

        await using var verifyContext =
            database.CreateContext();

        var stock =
            await verifyContext.Products
                .Where(
                    product =>
                        product.Id == productId)
                .Select(
                    product =>
                        product.StockQuantity)
                .SingleAsync();

        var movementCount =
            await verifyContext
                .InventoryMovements
                .CountAsync();

        Assert.Equal(
            10,
            stock);

        Assert.Equal(
            0,
            movementCount);
    }

    [Fact]
    public async Task Stale_product_update_must_return_concurrency_conflict()
    {
        await using var database =
            await InventoryTestDatabase
                .CreateAsync();

        var productId =
            await database.SeedProductAsync(
                initialStock: 10,
                allowNegativeStock: false);

        await using var firstContext =
            database.CreateContext();

        await using var staleContext =
            database.CreateContext();

        var staleProductRepository =
            new TrackingProductRepository(
                staleContext);

        /*
         * Tải Product trước khi thao tác thứ nhất commit,
         * nhờ vậy staleContext giữ concurrency token cũ.
         */
        var staleProduct =
            await staleProductRepository
                .GetByIdAsync(
                    productId);

        Assert.NotNull(
            staleProduct);

        var firstService =
            CreateService(
                firstContext,
                new FixedClock(
                    CreatedAtUtc.AddHours(1)));

        var firstResult =
            await firstService.AdjustAsync(
                new InventoryAdjustmentRequest(
                    productId,
                    InventoryMovementType.StockIn,
                    quantity: 2,
                    reason: "Thao tác thứ nhất"));

        Assert.True(
            firstResult.IsSuccess,
            firstResult.Error.ToString());

        var staleService =
            new InventoryService(
                staleProductRepository,
                new InventoryMovementRepository(
                    staleContext),
                new EfUnitOfWork(
                    staleContext),
                new FixedClock(
                    CreatedAtUtc.AddHours(2)));

        var staleResult =
            await staleService.AdjustAsync(
                new InventoryAdjustmentRequest(
                    productId,
                    InventoryMovementType.StockIn,
                    quantity: 3,
                    reason: "Thao tác dùng dữ liệu cũ"));

        Assert.True(
            staleResult.IsFailure);

        Assert.Equal(
            ErrorCodes.Inventory
                .ConcurrencyConflict,
            staleResult.Error.Code);

        await using var verifyContext =
            database.CreateContext();

        var stock =
            await verifyContext.Products
                .Where(
                    product =>
                        product.Id == productId)
                .Select(
                    product =>
                        product.StockQuantity)
                .SingleAsync();

        var movementCount =
            await verifyContext
                .InventoryMovements
                .CountAsync();

        Assert.Equal(
            12,
            stock);

        Assert.Equal(
            1,
            movementCount);
    }

    [Fact]
    public async Task History_search_must_return_newest_first_and_paginate()
    {
        await using var database =
            await InventoryTestDatabase
                .CreateAsync();

        var productId =
            await database.SeedProductAsync(
                initialStock: 10,
                allowNegativeStock: false);

        var clock =
            new SequenceClock(
                [
                    CreatedAtUtc.AddMinutes(10),
                    CreatedAtUtc.AddMinutes(20),
                    CreatedAtUtc.AddMinutes(30)
                ]);

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    clock);

            for (var index = 1;
                 index <= 3;
                 index++)
            {
                var result =
                    await service.AdjustAsync(
                        new InventoryAdjustmentRequest(
                            productId,
                            InventoryMovementType.StockIn,
                            quantity: 1,
                            reason: $"Lần {index}"));

                Assert.True(
                    result.IsSuccess,
                    result.Error.ToString());
            }
        }

        await using var searchContext =
            database.CreateContext();

        var repository =
            new InventoryMovementRepository(
                searchContext);

        var firstPage =
            await repository.SearchAsync(
                productId,
                movementType: null,
                fromUtc: null,
                toUtc: null,
                referenceType: null,
                pageNumber: 1,
                pageSize: 2);

        Assert.Equal(
            3,
            firstPage.TotalCount);

        Assert.Equal(
            2,
            firstPage.Items.Count);

        Assert.Equal(
            "Lần 3",
            firstPage.Items[0].Reason);

        Assert.Equal(
            "Lần 2",
            firstPage.Items[1].Reason);

        Assert.True(
            firstPage.HasNextPage);

        var secondPage =
            await repository.SearchAsync(
                productId,
                movementType: null,
                fromUtc: null,
                toUtc: null,
                referenceType: null,
                pageNumber: 2,
                pageSize: 2);

        Assert.Single(
            secondPage.Items);

        Assert.Equal(
            "Lần 1",
            secondPage.Items[0].Reason);
    }

    private static InventoryService CreateService(
        PosDbContext context,
        IClock clock)
    {
        return new InventoryService(
            new TrackingProductRepository(
                context),
            new InventoryMovementRepository(
                context),
            new EfUnitOfWork(
                context),
            clock);
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

    private sealed class SequenceClock :
        IClock
    {
        private readonly Queue<DateTimeOffset>
            _values;

        public SequenceClock(
            IEnumerable<DateTimeOffset> values)
        {
            ArgumentNullException.ThrowIfNull(
                values);

            _values =
                new Queue<DateTimeOffset>(
                    values.Select(
                        value =>
                            value.ToUniversalTime()));

            if (_values.Count == 0)
            {
                throw new ArgumentException(
                    "Phải có ít nhất một giá trị thời gian.",
                    nameof(values));
            }
        }

        public DateTimeOffset UtcNow
        {
            get
            {
                if (_values.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Không còn giá trị thời gian kiểm thử.");
                }

                return _values.Dequeue();
            }
        }
    }

    private sealed class FailingAfterSaveUnitOfWork :
        IUnitOfWork
    {
        private readonly IUnitOfWork _inner;

        public FailingAfterSaveUnitOfWork(
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
            await _inner.SaveChangesAsync(
                cancellationToken);

            throw new InvalidOperationException(
                "Synthetic failure after SaveChanges.");
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
    /// Repository Product tối thiểu dùng trong integration test.
    ///
    /// Không Include Category để test tồn kho không phụ thuộc
    /// dữ liệu danh mục.
    /// </summary>
    private sealed class TrackingProductRepository :
        IProductRepository
    {
        private readonly PosDbContext _dbContext;

        public TrackingProductRepository(
            PosDbContext dbContext)
        {
            _dbContext =
                dbContext ??
                throw new ArgumentNullException(
                    nameof(dbContext));
        }

        public Task<Product?> GetByIdAsync(
            int productId,
            CancellationToken cancellationToken = default)
        {
            var localProduct =
                _dbContext.Products.Local
                    .FirstOrDefault(
                        product =>
                            product.Id == productId);

            if (localProduct is not null)
            {
                return Task.FromResult<
                    Product?>(localProduct);
            }

            return _dbContext.Products
                .SingleOrDefaultAsync(
                    product =>
                        product.Id == productId,
                    cancellationToken);
        }

        public Task<Product?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            return _dbContext.Products
                .SingleOrDefaultAsync(
                    product =>
                        product.Code == code,
                    cancellationToken);
        }

        public Task<Product?> GetByBarcodeAsync(
            string barcode,
            CancellationToken cancellationToken = default)
        {
            return _dbContext.Products
                .SingleOrDefaultAsync(
                    product =>
                        product.Barcode == barcode,
                    cancellationToken);
        }

        public Task<PagedResult<Product>> SearchAsync(
            string? searchTerm,
            int? categoryId,
            bool? isActive,
            bool? isLowStock,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Integration test này không dùng tìm kiếm Product.");
        }

        public Task<bool> CodeExistsAsync(
            string code,
            int? excludeProductId = null,
            CancellationToken cancellationToken = default)
        {
            return _dbContext.Products
                .AnyAsync(
                    product =>
                        product.Code == code &&
                        (
                            !excludeProductId.HasValue ||
                            product.Id !=
                            excludeProductId.Value
                        ),
                    cancellationToken);
        }

        public Task<bool> BarcodeExistsAsync(
            string barcode,
            int? excludeProductId = null,
            CancellationToken cancellationToken = default)
        {
            return _dbContext.Products
                .AnyAsync(
                    product =>
                        product.Barcode == barcode &&
                        (
                            !excludeProductId.HasValue ||
                            product.Id !=
                            excludeProductId.Value
                        ),
                    cancellationToken);
        }

        public async Task AddAsync(
            Product product,
            CancellationToken cancellationToken = default)
        {
            await _dbContext.Products.AddAsync(
                product,
                cancellationToken);
        }
    }

    /// <summary>
    /// SQLite database thật trong bộ nhớ.
    ///
    /// Một connection được giữ mở trong suốt test để database
    /// không bị xóa giữa các DbContext.
    /// </summary>
    private sealed class InventoryTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly
            DbContextOptions<PosDbContext>
            _options;

        private InventoryTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<
            InventoryTestDatabase>
            CreateAsync()
        {
            /*
             * Tắt foreign key chỉ trong test này để tạo Product
             * mà không cần phụ thuộc constructor Category.
             *
             * Các check constraint, transaction và concurrency
             * vẫn chạy bằng SQLite thật.
             */
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:;" +
                    "Foreign Keys=False");

            await connection.OpenAsync();

            var options =
                new DbContextOptionsBuilder<
                        PosDbContext>()
                    .UseSqlite(connection)
                    .AddInterceptors(
                        new TestConcurrencyTokenInterceptor())
                    .EnableDetailedErrors()
                    .Options;

            var database =
                new InventoryTestDatabase(
                    connection,
                    options);

            await using var context =
                database.CreateContext();

            await context.Database
                .EnsureCreatedAsync();

            return database;
        }

        public PosDbContext CreateContext()
        {
            return new PosDbContext(
                _options);
        }

        public async Task<int> SeedProductAsync(
            int initialStock,
            bool allowNegativeStock)
        {
            await using var context =
                CreateContext();

            var product =
                new Product(
                    categoryId: 1,
                    code: "INV-TEST-001",
                    name: "Sản phẩm kiểm thử kho",
                    unitName: "Cái",
                    costPrice: 10_000,
                    salePrice: 15_000,
                    stockQuantity: initialStock,
                    minimumStock: 1,
                    trackInventory: true,
                    allowNegativeStock,
                    CreatedAtUtc);

            await context.Products.AddAsync(
                product);

            await context.SaveChangesAsync();

            return product.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Mô phỏng đúng nguyên tắc interceptor production:
    /// mỗi lần thêm/sửa AuditableEntity sẽ nhận GUID token mới.
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