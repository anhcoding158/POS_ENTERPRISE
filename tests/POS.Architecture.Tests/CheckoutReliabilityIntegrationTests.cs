using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Orders;
using POS.Application.Abstractions.Persistence;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Checkout;
using POS.Application.Services;
using POS.Application.Validation;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Hàng rào độ tin cậy của Checkout trước khi xây Sales UI.
///
/// Các test dùng SQLite thật trong bộ nhớ để xác nhận:
/// - rollback thực sự;
/// - optimistic concurrency;
/// - unique constraint;
/// - tồn kho và lịch sử kho luôn đồng bộ với Order.
/// </summary>
public sealed class
    CheckoutReliabilityIntegrationTests
{
    private static readonly DateTimeOffset
        UtcNow =
            new(
                2026,
                7,
                21,
                17,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public void
        Duplicate_product_lines_must_be_rejected()
    {
        var request =
            new CheckoutRequest(
                lines:
                [
                    new CheckoutLineRequest(
                        productId: 10,
                        quantity: 1),

                    new CheckoutLineRequest(
                        productId: 10,
                        quantity: 2)
                ],
                paymentMethod:
                    PaymentMethod.Cash,
                cashReceived:
                    200_000);

        var result =
            CheckoutValidator.Validate(
                request);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Checkout.DuplicateProduct,
            result.Error.Code);
    }

    [Fact]
    public async Task
        Stale_checkout_must_not_oversell_product()
    {
        await using var database =
            await CheckoutTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedBaseAsync();

        var product =
            await database.AddProductAsync(
                seed,
                stockQuantity:
                    5,

                trackInventory:
                    true,

                allowNegativeStock:
                    false);

        /*
         * staleContext tải Product trước khi giao dịch đầu tiên
         * cập nhật concurrency token.
         */
        await using var staleContext =
            database.CreateContext();

        var staleProduct =
            await staleContext.Products
                .Include(
                    item =>
                        item.Category)
                .SingleAsync(
                    item =>
                        item.Id ==
                        product.ProductId);

        await using (
            var firstContext =
                database.CreateContext())
        {
            var firstService =
                CreateService(
                    firstContext,
                    seed,
                    "HD-CONCURRENT-0001");

            var firstResult =
                await firstService.CheckoutAsync(
                    CreateCashRequest(
                        (
                            product.ProductId,
                            3
                        )));

            Assert.True(
                firstResult.IsSuccess,
                firstResult.Error.ToString());
        }

        /*
         * Repository này cố ý trả Product đã cũ đang được
         * staleContext tracking.
         */
        var staleRepository =
            new PreloadedProductRepository(
                staleProduct);

        var staleService =
            CreateService(
                staleContext,
                seed,
                "HD-CONCURRENT-0002",
                productRepository:
                    staleRepository);

        var staleResult =
            await staleService.CheckoutAsync(
                CreateCashRequest(
                    (
                        product.ProductId,
                        3
                    )));

        Assert.True(
            staleResult.IsFailure);

        Assert.Equal(
            ErrorCodes.Checkout.ConcurrencyConflict,
            staleResult.Error.Code);

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            2,
            await verifyContext.Products
                .Where(
                    item =>
                        item.Id ==
                        product.ProductId)
                .Select(
                    item =>
                        item.StockQuantity)
                .SingleAsync());

        Assert.Equal(
            1,
            await verifyContext.Orders
                .CountAsync());

        Assert.Equal(
            1,
            await verifyContext.InventoryMovements
                .CountAsync());
    }

    [Fact]
    public async Task
        Failure_after_save_must_rollback_everything()
    {
        await using var database =
            await CheckoutTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedBaseAsync();

        var product =
            await database.AddProductAsync(
                seed,
                stockQuantity:
                    10,

                trackInventory:
                    true,

                allowNegativeStock:
                    false);

        await using (
            var context =
                database.CreateContext())
        {
            var realUnitOfWork =
                new EfUnitOfWork(
                    context);

            var failingUnitOfWork =
                new FailingAfterSaveUnitOfWork(
                    realUnitOfWork);

            var service =
                CreateService(
                    context,
                    seed,
                    "HD-ROLLBACK-SAVE-0001",
                    unitOfWork:
                        failingUnitOfWork);

            var result =
                await service.CheckoutAsync(
                    CreateCashRequest(
                        (
                            product.ProductId,
                            2
                        )));

            Assert.True(
                result.IsFailure);

            Assert.Equal(
                ErrorCodes.Checkout.SaveFailed,
                result.Error.Code);
        }

        await AssertDatabaseStateAsync(
            database,
            product.ProductId,
            expectedStock:
                10,

            expectedOrders:
                0,

            expectedOrderItems:
                0,

            expectedMovements:
                0);
    }

    [Fact]
    public async Task
        Cancellation_before_commit_must_rollback_everything()
    {
        await using var database =
            await CheckoutTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedBaseAsync();

        var product =
            await database.AddProductAsync(
                seed,
                stockQuantity:
                    10,

                trackInventory:
                    true,

                allowNegativeStock:
                    false);

        using var cancellationSource =
            new CancellationTokenSource();

        await using (
            var context =
                database.CreateContext())
        {
            var cancellingUnitOfWork =
                new CancelBeforeCommitUnitOfWork(
                    new EfUnitOfWork(
                        context),

                    cancellationSource);

            var service =
                CreateService(
                    context,
                    seed,
                    "HD-CANCEL-0001",
                    unitOfWork:
                        cancellingUnitOfWork);

            await Assert.ThrowsAnyAsync<
                OperationCanceledException>(
                    () =>
                        service.CheckoutAsync(
                            CreateCashRequest(
                                (
                                    product.ProductId,
                                    2
                                )),
                            cancellationSource.Token));
        }

        await AssertDatabaseStateAsync(
            database,
            product.ProductId,
            expectedStock:
                10,

            expectedOrders:
                0,

            expectedOrderItems:
                0,

            expectedMovements:
                0);
    }

    [Fact]
    public async Task
        Order_code_race_must_rollback_stock_and_movement()
    {
        await using var database =
            await CheckoutTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedBaseAsync();

        var product =
            await database.AddProductAsync(
                seed,
                stockQuantity:
                    10,

                trackInventory:
                    true,

                allowNegativeStock:
                    false);

        const string duplicateCode =
            "HD-RACE-UNIQUE-0001";

        await database.SeedDraftOrderAsync(
            seed.UserId,
            duplicateCode);

        await using (
            var context =
                database.CreateContext())
        {
            var regularOrderRepository =
                new OrderRepository(
                    context);

            /*
             * Mô phỏng race:
             *
             * CodeExists trả false nhưng trước SaveChanges,
             * một giao dịch khác đã sử dụng mã đó.
             *
             * Unique index trong database phải là hàng rào cuối.
             */
            var blindRepository =
                new BlindCodeCheckOrderRepository(
                    regularOrderRepository);

            var service =
                CreateService(
                    context,
                    seed,
                    duplicateCode,
                    orderRepository:
                        blindRepository);

            var result =
                await service.CheckoutAsync(
                    CreateCashRequest(
                        (
                            product.ProductId,
                            2
                        )));

            Assert.True(
                result.IsFailure);

            Assert.Equal(
                ErrorCodes.Checkout.OrderCodeConflict,
                result.Error.Code);
        }

        await AssertDatabaseStateAsync(
            database,
            product.ProductId,
            expectedStock:
                10,

            expectedOrders:
                1,

            expectedOrderItems:
                0,

            expectedMovements:
                0);
    }

    [Fact]
    public async Task
        Mixed_cart_failure_must_not_partially_sell_valid_product()
    {
        await using var database =
            await CheckoutTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedBaseAsync();

        var availableProduct =
            await database.AddProductAsync(
                seed,
                stockQuantity:
                    20,

                trackInventory:
                    true,

                allowNegativeStock:
                    false);

        var insufficientProduct =
            await database.AddProductAsync(
                seed,
                stockQuantity:
                    1,

                trackInventory:
                    true,

                allowNegativeStock:
                    false);

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    seed,
                    "HD-MIXED-CART-0001");

            var result =
                await service.CheckoutAsync(
                    CreateCashRequest(
                        (
                            availableProduct.ProductId,
                            5
                        ),
                        (
                            insufficientProduct.ProductId,
                            2
                        )));

            Assert.True(
                result.IsFailure);

            Assert.Equal(
                ErrorCodes.Checkout.InsufficientStock,
                result.Error.Code);
        }

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            20,
            await verifyContext.Products
                .Where(
                    item =>
                        item.Id ==
                        availableProduct.ProductId)
                .Select(
                    item =>
                        item.StockQuantity)
                .SingleAsync());

        Assert.Equal(
            1,
            await verifyContext.Products
                .Where(
                    item =>
                        item.Id ==
                        insufficientProduct.ProductId)
                .Select(
                    item =>
                        item.StockQuantity)
                .SingleAsync());

        Assert.Equal(
            0,
            await verifyContext.Orders
                .CountAsync());

        Assert.Equal(
            0,
            await verifyContext.InventoryMovements
                .CountAsync());
    }

    [Fact]
    public async Task
        Negative_stock_policy_must_be_honoured()
    {
        await using var database =
            await CheckoutTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedBaseAsync();

        var product =
            await database.AddProductAsync(
                seed,
                stockQuantity:
                    1,

                trackInventory:
                    true,

                allowNegativeStock:
                    true);

        await using (
            var context =
                database.CreateContext())
        {
            var service =
                CreateService(
                    context,
                    seed,
                    "HD-NEGATIVE-STOCK-0001");

            var result =
                await service.CheckoutAsync(
                    CreateCashRequest(
                        (
                            product.ProductId,
                            3
                        )));

            Assert.True(
                result.IsSuccess,
                result.Error.ToString());
        }

        await AssertDatabaseStateAsync(
            database,
            product.ProductId,
            expectedStock:
                -2,

            expectedOrders:
                1,

            expectedOrderItems:
                1,

            expectedMovements:
                1);
    }

    [Fact]
    public async Task
        Sequential_checkout_stress_must_keep_stock_consistent()
    {
        await using var database =
            await CheckoutTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedBaseAsync();

        var product =
            await database.AddProductAsync(
                seed,
                stockQuantity:
                    50,

                trackInventory:
                    true,

                allowNegativeStock:
                    false);

        const int checkoutCount =
            20;

        for (var index = 1;
             index <= checkoutCount;
             index++)
        {
            await using var context =
                database.CreateContext();

            var service =
                CreateService(
                    context,
                    seed,
                    $"HD-STRESS-{index:0000}");

            var result =
                await service.CheckoutAsync(
                    CreateCashRequest(
                        (
                            product.ProductId,
                            1
                        )));

            Assert.True(
                result.IsSuccess,
                $"Giao dịch {index} lỗi: " +
                $"{result.Error}");
        }

        await AssertDatabaseStateAsync(
            database,
            product.ProductId,
            expectedStock:
                30,

            expectedOrders:
                checkoutCount,

            expectedOrderItems:
                checkoutCount,

            expectedMovements:
                checkoutCount);

        await using var verifyContext =
            database.CreateContext();

        var distinctOrderCodes =
            await verifyContext.Orders
                .Select(
                    order =>
                        order.OrderCode)
                .Distinct()
                .CountAsync();

        Assert.Equal(
            checkoutCount,
            distinctOrderCodes);

        var movementTotal =
            await verifyContext.InventoryMovements
                .SumAsync(
                    movement =>
                        movement.QuantityDelta);

        Assert.Equal(
            -checkoutCount,
            movementTotal);
    }

    private static CheckoutRequest
        CreateCashRequest(
            params (int ProductId, int Quantity)[]
                lines)
    {
        return new CheckoutRequest(
            lines:
                lines.Select(
                    line =>
                        new CheckoutLineRequest(
                            line.ProductId,
                            line.Quantity)),

            paymentMethod:
                PaymentMethod.Cash,

            cashReceived:
                10_000_000);
    }

    private static CheckoutService CreateService(
        PosDbContext context,
        SeedData seed,
        string orderCode,
        IProductRepository? productRepository = null,
        IOrderRepository? orderRepository = null,
        IUnitOfWork? unitOfWork = null)
    {
        var currentUser =
            new CurrentUserService();

        currentUser.SetCurrentUser(
            new AuthenticatedUserDto(
                id:
                    seed.UserId,

                username:
                    "cashier.reliability",

                fullName:
                    "Thu ngân Reliability",

                role:
                    Role.Cashier,

                authenticatedAtUtc:
                    UtcNow));

        return new CheckoutService(
            productRepository ??
                new ProductRepository(
                    context),

            orderRepository ??
                new OrderRepository(
                    context),

            new InventoryMovementRepository(
                context),

            unitOfWork ??
                new EfUnitOfWork(
                    context),

            new FixedOrderCodeGenerator(
                orderCode),

            currentUser,

            new FixedClock(
                UtcNow),

            NullLogger<CheckoutService>
                .Instance);
    }

    private static async Task
        AssertDatabaseStateAsync(
            CheckoutTestDatabase database,
            int productId,
            int expectedStock,
            int expectedOrders,
            int expectedOrderItems,
            int expectedMovements)
    {
        await using var context =
            database.CreateContext();

        var stock =
            await context.Products
                .Where(
                    product =>
                        product.Id ==
                        productId)
                .Select(
                    product =>
                        product.StockQuantity)
                .SingleAsync();

        Assert.Equal(
            expectedStock,
            stock);

        Assert.Equal(
            expectedOrders,
            await context.Orders
                .CountAsync());

        Assert.Equal(
            expectedOrderItems,
            await context.OrderItems
                .CountAsync());

        Assert.Equal(
            expectedMovements,
            await context.InventoryMovements
                .CountAsync());
    }

    private sealed record SeedData(
        int UserId,
        int CategoryId);

    private sealed record ProductSeed(
        int ProductId,
        int InitialStock);

    private sealed class FixedClock :
        IClock
    {
        public FixedClock(
            DateTimeOffset utcNow)
        {
            UtcNow =
                utcNow.ToUniversalTime();
        }

        public DateTimeOffset UtcNow
        {
            get;
        }
    }

    private sealed class
        FixedOrderCodeGenerator :
            IOrderCodeGenerator
    {
        private readonly string
            _orderCode;

        public FixedOrderCodeGenerator(
            string orderCode)
        {
            _orderCode =
                orderCode;
        }

        public string Generate(
            DateTimeOffset utcNow)
        {
            return _orderCode;
        }
    }

    /// <summary>
    /// Trả về Product đã được context tracking từ trước.
    /// Dùng để tạo optimistic concurrency conflict có kiểm soát.
    /// </summary>
    private sealed class
        PreloadedProductRepository :
            IProductRepository
    {
        private readonly Product
            _product;

        public PreloadedProductRepository(
            Product product)
        {
            _product =
                product;
        }

        public Task<Product?> GetByIdAsync(
            int productId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult<Product?>(
                productId == _product.Id
                    ? _product
                    : null);
        }

        public Task<Product?> GetByCodeAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Product?> GetByBarcodeAsync(
            string barcode,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }

        public Task<bool> CodeExistsAsync(
            string code,
            int? excludeProductId = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> BarcodeExistsAsync(
            string barcode,
            int? excludeProductId = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(
            Product product,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Cố ý trả false ở bước pre-check để mô phỏng
    /// một giao dịch khác chèn mã Order ngay trước SaveChanges.
    /// </summary>
    private sealed class
        BlindCodeCheckOrderRepository :
            IOrderRepository
    {
        private readonly IOrderRepository
            _inner;

        public BlindCodeCheckOrderRepository(
            IOrderRepository inner)
        {
            _inner =
                inner;
        }

        public Task<Order?> GetByIdAsync(
            int orderId,
            CancellationToken cancellationToken = default)
        {
            return _inner.GetByIdAsync(
                orderId,
                cancellationToken);
        }

        public Task<Order?> GetByCodeAsync(
            string orderCode,
            CancellationToken cancellationToken = default)
        {
            return _inner.GetByCodeAsync(
                orderCode,
                cancellationToken);
        }

        public Task<PagedResult<Order>> SearchAsync(
            string? searchTerm,
            OrderStatus? status,
            int? customerId,
            int? cashierUserId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return _inner.SearchAsync(
                searchTerm,
                status,
                customerId,
                cashierUserId,
                fromUtc,
                toUtc,
                pageNumber,
                pageSize,
                cancellationToken);
        }

        public Task<bool> CodeExistsAsync(
            string orderCode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                false);
        }

        public Task AddAsync(
            Order order,
            CancellationToken cancellationToken = default)
        {
            return _inner.AddAsync(
                order,
                cancellationToken);
        }
    }

    /// <summary>
    /// SaveChanges chạy thật trong transaction rồi mới ném lỗi.
    /// Khi CheckoutService kết thúc, transaction chưa commit
    /// phải tự rollback toàn bộ.
    /// </summary>
    private sealed class
        FailingAfterSaveUnitOfWork :
            IUnitOfWork
    {
        private readonly IUnitOfWork
            _inner;

        public FailingAfterSaveUnitOfWork(
            IUnitOfWork inner)
        {
            _inner =
                inner;
        }

        public async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            await _inner.SaveChangesAsync(
                cancellationToken);

            throw new InvalidOperationException(
                "Mô phỏng lỗi sau SaveChanges.");
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
    /// Hủy request sau SaveChanges nhưng ngay trước Commit.
    /// </summary>
    private sealed class
        CancelBeforeCommitUnitOfWork :
            IUnitOfWork
    {
        private readonly IUnitOfWork
            _inner;

        private readonly CancellationTokenSource
            _cancellationSource;

        public CancelBeforeCommitUnitOfWork(
            IUnitOfWork inner,
            CancellationTokenSource cancellationSource)
        {
            _inner =
                inner;

            _cancellationSource =
                cancellationSource;
        }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return _inner.SaveChangesAsync(
                cancellationToken);
        }

        public async Task<IApplicationTransaction>
            BeginTransactionAsync(
                CancellationToken cancellationToken = default)
        {
            var transaction =
                await _inner.BeginTransactionAsync(
                    cancellationToken);

            return new CancelBeforeCommitTransaction(
                transaction,
                _cancellationSource);
        }
    }

    private sealed class
        CancelBeforeCommitTransaction :
            IApplicationTransaction
    {
        private readonly IApplicationTransaction
            _inner;

        private readonly CancellationTokenSource
            _cancellationSource;

        public CancelBeforeCommitTransaction(
            IApplicationTransaction inner,
            CancellationTokenSource cancellationSource)
        {
            _inner =
                inner;

            _cancellationSource =
                cancellationSource;
        }

        public bool IsCompleted =>
            _inner.IsCompleted;

        public Task CommitAsync(
            CancellationToken cancellationToken = default)
        {
            _cancellationSource.Cancel();

            cancellationToken
                .ThrowIfCancellationRequested();

            throw new OperationCanceledException(
                cancellationToken);
        }

        public Task RollbackAsync(
            CancellationToken cancellationToken = default)
        {
            return _inner.RollbackAsync(
                cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _inner.DisposeAsync();
        }
    }

    private sealed class CheckoutTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly
            DbContextOptions<PosDbContext>
            _options;

        private CheckoutTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection =
                connection;

            _options =
                options;
        }

        public static async Task<
            CheckoutTestDatabase>
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
                        new AuditableEntityInterceptor())
                    .EnableDetailedErrors()
                    .Options;

            var database =
                new CheckoutTestDatabase(
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

        public async Task<SeedData>
            SeedBaseAsync()
        {
            await using var context =
                CreateContext();

            var category =
                new Category(
                    name:
                        $"Checkout Reliability " +
                        $"{Guid.NewGuid():N}",

                    displayOrder:
                        1,

                    utcNow:
                        UtcNow);

            var user =
                new User(
                    username:
                        $"reliability.{Guid.NewGuid():N}",

                    passwordHash:
                        "reliability-password-hash",

                    fullName:
                        "Thu ngân Reliability",

                    role:
                        Role.Cashier,

                    utcNow:
                        UtcNow);

            context.Categories.Add(
                category);

            context.Users.Add(
                user);

            await context.SaveChangesAsync();

            return new SeedData(
                user.Id,
                category.Id);
        }

        public async Task<ProductSeed>
            AddProductAsync(
                SeedData seed,
                int stockQuantity,
                bool trackInventory,
                bool allowNegativeStock)
        {
            await using var context =
                CreateContext();

            var product =
                new Product(
                    categoryId:
                        seed.CategoryId,

                    code:
                        $"SP-{Guid.NewGuid():N}",

                    name:
                        $"Sản phẩm Reliability " +
                        $"{Guid.NewGuid():N}",

                    unitName:
                        "Phần",

                    costPrice:
                        10_000,

                    salePrice:
                        30_000,

                    stockQuantity:
                        stockQuantity,

                    minimumStock:
                        trackInventory
                            ? 2
                            : 0,

                    trackInventory:
                        trackInventory,

                    allowNegativeStock:
                        allowNegativeStock,

                    utcNow:
                        UtcNow);

            context.Products.Add(
                product);

            await context.SaveChangesAsync();

            return new ProductSeed(
                product.Id,
                stockQuantity);
        }

        public async Task SeedDraftOrderAsync(
            int cashierUserId,
            string orderCode)
        {
            await using var context =
                CreateContext();

            context.Orders.Add(
                new Order(
                    orderCode:
                        orderCode,

                    cashierUserId:
                        cashierUserId,

                    utcNow:
                        UtcNow));

            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }
}