using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Orders;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Checkout;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class CheckoutServiceIntegrationTests
{
    private static readonly DateTimeOffset
        UtcNow =
            new(
                2026,
                7,
                21,
                12,
                0,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task Successful_checkout_must_commit_order_stock_and_movement()
    {
        await using var database =
            await CheckoutTestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync(
                stockQuantity: 10,
                trackInventory: true,
                isActive: true);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed,
                "HD-CHECKOUT-0001");

        var result =
            await service.CheckoutAsync(
                new CheckoutRequest(
                    lines:
                    [
                        new CheckoutLineRequest(
                            seed.ProductId,
                            quantity: 2)
                    ],
                    paymentMethod:
                        PaymentMethod.Cash,
                    cashReceived:
                        100_000));

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        Assert.Equal(
            60_000,
            result.Value.TotalAmount);

        Assert.Equal(
            40_000,
            result.Value.ChangeAmount);

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            1,
            await verifyContext.Orders.CountAsync());

        Assert.Equal(
            1,
            await verifyContext.OrderItems.CountAsync());

        Assert.Equal(
            1,
            await verifyContext
                .InventoryMovements
                .CountAsync());

        var product =
            await verifyContext.Products
                .SingleAsync(
                    item =>
                        item.Id ==
                        seed.ProductId);

        Assert.Equal(
            8,
            product.StockQuantity);

        var movement =
            await verifyContext
                .InventoryMovements
                .SingleAsync();

        Assert.Equal(
            InventoryMovementType.Sale,
            movement.MovementType);

        Assert.Equal(
            -2,
            movement.QuantityDelta);

        Assert.Equal(
            "ORDER",
            movement.ReferenceType);

        Assert.Equal(
            "HD-CHECKOUT-0001",
            movement.ReferenceId);
    }

    [Fact]
    public async Task Insufficient_stock_must_leave_database_unchanged()
    {
        await using var database =
            await CheckoutTestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync(
                stockQuantity: 3,
                trackInventory: true,
                isActive: true);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed,
                "HD-CHECKOUT-0002");

        var result =
            await service.CheckoutAsync(
                new CheckoutRequest(
                    lines:
                    [
                        new CheckoutLineRequest(
                            seed.ProductId,
                            quantity: 4)
                    ],
                    paymentMethod:
                        PaymentMethod.Cash,
                    cashReceived:
                        200_000));

        Assert.Equal(
            POS.Application.Common
                .ErrorCodes.Checkout.InsufficientStock,
            result.Error.Code);

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            0,
            await verifyContext.Orders.CountAsync());

        Assert.Equal(
            0,
            await verifyContext
                .InventoryMovements
                .CountAsync());

        Assert.Equal(
            3,
            await verifyContext.Products
                .Where(
                    item =>
                        item.Id ==
                        seed.ProductId)
                .Select(
                    item =>
                        item.StockQuantity)
                .SingleAsync());
    }

    [Fact]
    public async Task Inactive_product_must_not_be_sold()
    {
        await using var database =
            await CheckoutTestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync(
                stockQuantity: 10,
                trackInventory: true,
                isActive: false);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed,
                "HD-CHECKOUT-0003");

        var result =
            await service.CheckoutAsync(
                new CheckoutRequest(
                    lines:
                    [
                        new CheckoutLineRequest(
                            seed.ProductId,
                            quantity: 1)
                    ],
                    paymentMethod:
                        PaymentMethod.Cash,
                    cashReceived:
                        100_000));

        Assert.Equal(
            POS.Application.Common
                .ErrorCodes.Checkout.ProductInactive,
            result.Error.Code);

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            0,
            await verifyContext.Orders.CountAsync());
    }

    [Fact]
    public async Task Product_without_inventory_tracking_must_not_create_movement()
    {
        await using var database =
            await CheckoutTestDatabase.CreateAsync();

        var seed =
            await database.SeedAsync(
                stockQuantity: 0,
                trackInventory: false,
                isActive: true);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed,
                "HD-CHECKOUT-0004");

        var result =
            await service.CheckoutAsync(
                new CheckoutRequest(
                    lines:
                    [
                        new CheckoutLineRequest(
                            seed.ProductId,
                            quantity: 3)
                    ],
                    paymentMethod:
                        PaymentMethod.Cash,
                    cashReceived:
                        100_000));

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            1,
            await verifyContext.Orders.CountAsync());

        Assert.Equal(
            0,
            await verifyContext
                .InventoryMovements
                .CountAsync());

        Assert.Equal(
            0,
            await verifyContext.Products
                .Where(
                    item =>
                        item.Id ==
                        seed.ProductId)
                .Select(
                    item =>
                        item.StockQuantity)
                .SingleAsync());
    }

    private static CheckoutService CreateService(
        PosDbContext context,
        SeedData seed,
        string orderCode)
    {
        var currentUser =
            new CurrentUserService();

        currentUser.SetCurrentUser(
            new AuthenticatedUserDto(
                id:
                    seed.UserId,

                username:
                    "cashier.checkout",

                fullName:
                    "Thu ngân Checkout",

                role:
                    Role.Cashier,

                authenticatedAtUtc:
                    UtcNow));

        return new CheckoutService(
            new ProductRepository(
                context),

            new OrderRepository(
                context),

            new InventoryMovementRepository(
                context),

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

    private sealed record SeedData(
        int UserId,
        int ProductId);

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

    private sealed class FixedOrderCodeGenerator :
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

    private sealed class CheckoutTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly DbContextOptions<
            PosDbContext>
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
            CheckoutTestDatabase> CreateAsync()
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

        public async Task<SeedData> SeedAsync(
            int stockQuantity,
            bool trackInventory,
            bool isActive)
        {
            await using var context =
                CreateContext();

            var category =
                new Category(
                    name:
                        $"Danh mục Checkout {Guid.NewGuid():N}",

                    displayOrder:
                        1,

                    utcNow:
                        UtcNow);

            var user =
                new User(
                    username:
                        $"cashier.{Guid.NewGuid():N}",

                    passwordHash:
                        "checkout-test-password-hash",

                    fullName:
                        "Thu ngân Checkout",

                    role:
                        Role.Cashier,

                    utcNow:
                        UtcNow);

            context.Categories.Add(
                category);

            context.Users.Add(
                user);

            await context.SaveChangesAsync();

            var product =
                new Product(
                    categoryId:
                        category.Id,

                    code:
                        $"SP-{Guid.NewGuid():N}",

                    name:
                        "Sản phẩm Checkout",

                    unitName:
                        "Phần",

                    costPrice:
                        10_000,

                    salePrice:
                        30_000,

                    stockQuantity:
                        stockQuantity,

                    minimumStock:
                        trackInventory ? 2 : 0,

                    trackInventory:
                        trackInventory,

                    allowNegativeStock:
                        false,

                    utcNow:
                        UtcNow);

            if (!isActive)
            {
                product.Deactivate(
                    UtcNow.AddMinutes(1));
            }

            context.Products.Add(
                product);

            await context.SaveChangesAsync();

            return new SeedData(
                user.Id,
                product.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }
}