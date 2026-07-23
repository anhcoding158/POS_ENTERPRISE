using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Orders;
using POS.Application.Abstractions.Printing;
using POS.Application.Common;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Printing;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Kiểm thử việc nối receipt snapshot vào checkout.
///
/// Các test khóa những nguyên tắc quan trọng:
/// - receipt được tạo từ dữ liệu giao dịch đã chốt;
/// - receipt được tạo trước transaction commit;
/// - lỗi tạo receipt phải rollback toàn bộ giao dịch;
/// - CheckoutService không được gọi printer;
/// - DI production phải đăng ký đầy đủ receipt foundation.
/// </summary>
public sealed class CheckoutReceiptIntegrationTests
{
    private static readonly DateTimeOffset
        UtcNow =
            new(
                2026,
                7,
                23,
                15,
                30,
                0,
                TimeSpan.Zero);

    [Fact]
    public async Task
        Successful_checkout_must_return_configured_receipt_snapshot()
    {
        await using var database =
            await CheckoutReceiptTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedAsync(
                stockQuantity:
                    10);

        await using var context =
            database.CreateContext();

        var storeProvider =
            new FixedReceiptStoreSnapshotProvider(
                new ReceiptStoreSnapshotDto(
                    name:
                        "Cửa hàng Ánh Dương",

                    address:
                        "123 Đường Trần Phú, Hà Nội",

                    phone:
                        "0999 888 777",

                    taxCode:
                        "0101234567",

                    footerMessage:
                        "Cảm ơn quý khách!"));

        var service =
            CreateService(
                context,
                seed,
                orderCode:
                    "HD-RECEIPT-0001",

                storeProvider);

        var result =
            await service.CheckoutAsync(
                CreateRequest(
                    seed.ProductId),
                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsSuccess,
            result.Error.ToString());

        var checkoutResult =
            result.Value;

        var receipt =
            Assert.IsType<ReceiptRequest>(
                checkoutResult.ReceiptSnapshot);

        Assert.True(
            receipt.Store.IsConfigured);

        Assert.Equal(
            "Cửa hàng Ánh Dương",
            receipt.Store.Name);

        Assert.Equal(
            "123 Đường Trần Phú, Hà Nội",
            receipt.Store.Address);

        Assert.Equal(
            "0999 888 777",
            receipt.Store.Phone);

        Assert.Equal(
            "0101234567",
            receipt.Store.TaxCode);

        Assert.Equal(
            ReceiptCopyKind.Original,
            receipt.CopyKind);

        Assert.Equal(
            0,
            receipt.CopyNumber);

        Assert.False(
            receipt.IsReprint);

        Assert.Equal(
            checkoutResult.OrderId,
            receipt.OrderId);

        Assert.Equal(
            checkoutResult.OrderCode,
            receipt.OrderCode);

        Assert.Equal(
            checkoutResult.CashierName,
            receipt.CashierName);

        Assert.Equal(
            checkoutResult.Subtotal,
            receipt.Subtotal);

        Assert.Equal(
            checkoutResult.DiscountAmount,
            receipt.DiscountAmount);

        Assert.Equal(
            checkoutResult.TotalAmount,
            receipt.TotalAmount);

        Assert.Equal(
            checkoutResult.CashReceived,
            receipt.CashReceived);

        Assert.Equal(
            checkoutResult.ChangeAmount,
            receipt.ChangeAmount);

        Assert.Equal(
            checkoutResult.PaymentMethod,
            receipt.PaymentMethod);

        Assert.Equal(
            checkoutResult.CreatedAtUtc,
            receipt.CreatedAtUtc);

        Assert.Equal(
            checkoutResult.PaidAtUtc,
            receipt.PaidAtUtc);

        Assert.Equal(
            "Giao hàng tại quầy",
            receipt.Notes);

        var receiptLine =
            Assert.Single(
                receipt.Lines);

        Assert.Equal(
            seed.ProductId,
            receiptLine.ProductId);

        Assert.Equal(
            "SP-RECEIPT",
            receiptLine.ProductCode);

        Assert.Equal(
            "Cà phê sữa đá",
            receiptLine.ProductName);

        Assert.Equal(
            "Ly",
            receiptLine.UnitName);

        Assert.Equal(
            2,
            receiptLine.Quantity);

        Assert.Equal(
            30_000,
            receiptLine.UnitSalePrice);

        Assert.Equal(
            60_000,
            receiptLine.NetAmount);

        Assert.Equal(
            "Ít đá",
            receiptLine.Notes);

        /*
         * Receipt DTO không được làm lộ giá vốn,
         * dù CheckoutLineResultDto nội bộ có UnitCostPrice.
         */
        Assert.Null(
            typeof(ReceiptLineDto)
                .GetProperty(
                    "UnitCostPrice"));

        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            1,
            await verifyContext
                .Orders
                .CountAsync(
                    TestContext
                        .Current
                        .CancellationToken));

        Assert.Equal(
            1,
            await verifyContext
                .OrderItems
                .CountAsync(
                    TestContext
                        .Current
                        .CancellationToken));

        Assert.Equal(
            1,
            await verifyContext
                .InventoryMovements
                .CountAsync(
                    TestContext
                        .Current
                        .CancellationToken));

        var remainingStock =
            await verifyContext.Products
                .Where(
                    product =>
                        product.Id ==
                        seed.ProductId)
                .Select(
                    product =>
                        product.StockQuantity)
                .SingleAsync(
                    TestContext
                        .Current
                        .CancellationToken);

        Assert.Equal(
            8,
            remainingStock);
    }

    [Fact]
    public async Task
        Receipt_provider_failure_must_rollback_entire_checkout()
    {
        await using var database =
            await CheckoutReceiptTestDatabase
                .CreateAsync();

        var seed =
            await database.SeedAsync(
                stockQuantity:
                    10);

        await using var context =
            database.CreateContext();

        var service =
            CreateService(
                context,
                seed,
                orderCode:
                    "HD-RECEIPT-ROLLBACK",

                new ThrowingReceiptStoreSnapshotProvider());

        var result =
            await service.CheckoutAsync(
                CreateRequest(
                    seed.ProductId),
                TestContext
                    .Current
                    .CancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            ErrorCodes.Checkout.SaveFailed,
            result.Error.Code);

        Assert.Null(
            result.ValueOrDefault);

        /*
         * Dùng context mới để tránh đọc state đang được
         * tracking trong context đã thực hiện checkout.
         *
         * Nếu provider được gọi sau Commit thì các assertion
         * dưới đây sẽ thất bại.
         */
        await using var verifyContext =
            database.CreateContext();

        Assert.Equal(
            0,
            await verifyContext
                .Orders
                .CountAsync(
                    TestContext
                        .Current
                        .CancellationToken));

        Assert.Equal(
            0,
            await verifyContext
                .OrderItems
                .CountAsync(
                    TestContext
                        .Current
                        .CancellationToken));

        Assert.Equal(
            0,
            await verifyContext
                .InventoryMovements
                .CountAsync(
                    TestContext
                        .Current
                        .CancellationToken));

        var remainingStock =
            await verifyContext.Products
                .Where(
                    product =>
                        product.Id ==
                        seed.ProductId)
                .Select(
                    product =>
                        product.StockQuantity)
                .SingleAsync(
                    TestContext
                        .Current
                        .CancellationToken);

        Assert.Equal(
            10,
            remainingStock);
    }

    [Fact]
    public void
        Checkout_service_must_not_depend_on_print_service()
    {
        var constructors =
            typeof(CheckoutService)
                .GetConstructors();

        var constructor =
            Assert.Single(
                constructors);

        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter =>
                parameter.ParameterType ==
                typeof(IReceiptService));

        Assert.DoesNotContain(
            typeof(CheckoutService)
                .GetFields(
                    System.Reflection
                        .BindingFlags.Instance |
                    System.Reflection
                        .BindingFlags.NonPublic),
            field =>
                field.FieldType ==
                typeof(IReceiptService));
    }

    [Fact]
    public void
        Infrastructure_must_register_receipt_foundation_as_singletons()
    {
        var configurationValues =
            new Dictionary<string, string?>
            {
                ["Infrastructure:DatabasePath"] =
                    "data/receipt-di-test.db",

                ["Infrastructure:DatabaseTimeoutSeconds"] =
                    "30",

                ["Infrastructure:ApplyMigrationsOnStartup"] =
                    "false",

                ["Infrastructure:SeedDemoProductCatalog"] =
                    "false",

                ["Infrastructure:SeedDefaultAdministrator"] =
                    "false",

                ["Store:Name"] =
                    "Cửa hàng DI",

                ["Store:Address"] =
                    "Địa chỉ DI",

                ["Store:Phone"] =
                    "0901 234 567",

                ["Store:TaxCode"] =
                    "0109999999",

                ["Store:FooterMessage"] =
                    "Hẹn gặp lại!"
            };

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    configurationValues)
                .Build();

        var services =
            new ServiceCollection();

        services.AddLogging();

        services.AddInfrastructure(
            configuration);

        using var serviceProvider =
            services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild =
                        true,

                    ValidateScopes =
                        true
                });

        var firstSerializer =
            serviceProvider
                .GetRequiredService<
                    IReceiptSnapshotSerializer>();

        var secondSerializer =
            serviceProvider
                .GetRequiredService<
                    IReceiptSnapshotSerializer>();

        var firstStoreProvider =
            serviceProvider
                .GetRequiredService<
                    IReceiptStoreSnapshotProvider>();

        var secondStoreProvider =
            serviceProvider
                .GetRequiredService<
                    IReceiptStoreSnapshotProvider>();

        Assert.Same(
            firstSerializer,
            secondSerializer);

        Assert.Same(
            firstStoreProvider,
            secondStoreProvider);

        var storeSnapshot =
            firstStoreProvider
                .GetCurrentSnapshot();

        Assert.True(
            storeSnapshot.IsConfigured);

        Assert.Equal(
            "Cửa hàng DI",
            storeSnapshot.Name);

        Assert.Equal(
            "Địa chỉ DI",
            storeSnapshot.Address);

        Assert.Equal(
            "0901 234 567",
            storeSnapshot.Phone);

        Assert.Equal(
            "0109999999",
            storeSnapshot.TaxCode);
    }

    private static CheckoutRequest CreateRequest(
        int productId)
    {
        return new CheckoutRequest(
            lines:
            [
                new CheckoutLineRequest(
                    productId:
                        productId,

                    quantity:
                        2,

                    notes:
                        "Ít đá")
            ],

            paymentMethod:
                PaymentMethod.Cash,

            cashReceived:
                100_000,

            notes:
                "Giao hàng tại quầy");
    }

    private static CheckoutService CreateService(
        PosDbContext context,
        SeedData seed,
        string orderCode,
        IReceiptStoreSnapshotProvider storeProvider)
    {
        var currentUser =
            new CurrentUserService();

        currentUser.SetCurrentUser(
            new AuthenticatedUserDto(
                id:
                    seed.UserId,

                username:
                    "cashier.receipt",

                fullName:
                    "Thu ngân Receipt",

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
                .Instance,

            storeProvider);
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

        public DateTimeOffset UtcNow
        {
            get;
        }
    }

    private sealed class FixedOrderCodeGenerator :
        IOrderCodeGenerator
    {
        private readonly string
            _orderCode;

        public FixedOrderCodeGenerator(
            string orderCode)
        {
            if (string.IsNullOrWhiteSpace(
                    orderCode))
            {
                throw new ArgumentException(
                    "Mã đơn test không được để trống.",
                    nameof(orderCode));
            }

            _orderCode =
                orderCode;
        }

        public string Generate(
            DateTimeOffset utcNow)
        {
            return _orderCode;
        }
    }

    private sealed class
        FixedReceiptStoreSnapshotProvider :
        IReceiptStoreSnapshotProvider
    {
        private readonly ReceiptStoreSnapshotDto
            _snapshot;

        public FixedReceiptStoreSnapshotProvider(
            ReceiptStoreSnapshotDto snapshot)
        {
            _snapshot =
                snapshot ??
                throw new ArgumentNullException(
                    nameof(snapshot));
        }

        public ReceiptStoreSnapshotDto
            GetCurrentSnapshot()
        {
            return _snapshot;
        }
    }

    private sealed class
        ThrowingReceiptStoreSnapshotProvider :
        IReceiptStoreSnapshotProvider
    {
        public ReceiptStoreSnapshotDto
            GetCurrentSnapshot()
        {
            throw new InvalidOperationException(
                "Lỗi receipt provider có chủ ý trong integration test.");
        }
    }

    private sealed class
        CheckoutReceiptTestDatabase :
        IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly DbContextOptions<
            PosDbContext>
            _options;

        private CheckoutReceiptTestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options)
        {
            _connection =
                connection;

            _options =
                options;
        }

        public static async Task<
            CheckoutReceiptTestDatabase>
            CreateAsync()
        {
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:;" +
                    "Foreign Keys=True");

            await connection.OpenAsync(
                TestContext
                    .Current
                    .CancellationToken);

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
                new CheckoutReceiptTestDatabase(
                    connection,
                    options);

            await using var context =
                database.CreateContext();

            await context.Database
                .EnsureCreatedAsync(
                    TestContext
                        .Current
                        .CancellationToken);

            return database;
        }

        public PosDbContext CreateContext()
        {
            return new PosDbContext(
                _options);
        }

        public async Task<SeedData> SeedAsync(
            int stockQuantity)
        {
            await using var context =
                CreateContext();

            var category =
                new Category(
                    name:
                        $"Danh mục Receipt " +
                        $"{Guid.NewGuid():N}",

                    displayOrder:
                        1,

                    utcNow:
                        UtcNow);

            var user =
                new User(
                    username:
                        $"cashier.receipt." +
                        $"{Guid.NewGuid():N}",

                    passwordHash:
                        "receipt-checkout-test-password-hash",

                    fullName:
                        "Thu ngân Receipt",

                    role:
                        Role.Cashier,

                    utcNow:
                        UtcNow);

            context.Categories.Add(
                category);

            context.Users.Add(
                user);

            await context.SaveChangesAsync(
                TestContext
                    .Current
                    .CancellationToken);

            var product =
                new Product(
                    categoryId:
                        category.Id,

                    code:
                        "SP-RECEIPT",

                    name:
                        "Cà phê sữa đá",

                    unitName:
                        "Ly",

                    costPrice:
                        10_000,

                    salePrice:
                        30_000,

                    stockQuantity:
                        stockQuantity,

                    minimumStock:
                        2,

                    trackInventory:
                        true,

                    allowNegativeStock:
                        false,

                    utcNow:
                        UtcNow);

            context.Products.Add(
                product);

            await context.SaveChangesAsync(
                TestContext
                    .Current
                    .CancellationToken);

            return new SeedData(
                user.Id,
                product.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection
                .DisposeAsync();

            GC.SuppressFinalize(
                this);
        }
    }
}