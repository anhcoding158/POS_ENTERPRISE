using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Orders;
using POS.Application.Abstractions.Persistence;
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
using POS.Infrastructure.Printing;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ReceiptSnapshotPersistenceTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 27, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Successful_checkout_must_persist_exactly_one_receipt_snapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await database.SeedAsync();
        await using var context = database.CreateContext();
        var serializer = new ReceiptSnapshotJsonSerializer();

        var result = await CreateService(context, seed, serializer: serializer)
            .CheckoutAsync(CreateRequest(seed.ProductId));

        Assert.True(result.IsSuccess, result.AppError.ToString());
        await using var verify = database.CreateContext();
        var persisted = await verify.OrderReceiptSnapshots.SingleAsync();
        Assert.Equal(result.Value.OrderId, persisted.OrderId);
        Assert.Equal(ReceiptRequest.CurrentSnapshotVersion, persisted.SnapshotVersion);
        Assert.False(string.IsNullOrWhiteSpace(persisted.PayloadJson));
        Assert.Equal(1, await verify.Orders.CountAsync());
    }

    [Fact]
    public async Task Persisted_snapshot_must_deserialize_to_original_checkout_snapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await database.SeedAsync();
        await using var context = database.CreateContext();
        var serializer = new ReceiptSnapshotJsonSerializer();
        var result = await CreateService(context, seed, serializer: serializer)
            .CheckoutAsync(CreateRequest(seed.ProductId));

        Assert.True(result.IsSuccess, result.AppError.ToString());
        await using var verify = database.CreateContext();
        var payload = await verify.OrderReceiptSnapshots
            .Select(snapshot => snapshot.PayloadJson)
            .SingleAsync();
        var restored = serializer.Deserialize(payload);
        var original = Assert.IsType<ReceiptRequest>(result.Value.ReceiptSnapshot);

        Assert.Equal(original.OrderId, restored.OrderId);
        Assert.Equal(original.OrderCode, restored.OrderCode);
        Assert.Equal(original.PaymentMethod, restored.PaymentMethod);
        Assert.Equal(original.CashReceived, restored.CashReceived);
        Assert.Equal(original.ChangeAmount, restored.ChangeAmount);
        Assert.Equal(original.TotalAmount, restored.TotalAmount);
        Assert.Equal(original.Notes, restored.Notes);
        Assert.Equal("Cửa hàng Việt", restored.Store.Name);
        var line = Assert.Single(restored.Lines);
        Assert.Equal("Cà phê sữa đá", line.ProductName);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(30_000, line.UnitSalePrice);
    }

    [Fact]
    public async Task Persisted_snapshot_must_not_change_when_product_changes_after_checkout()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await database.SeedAsync();
        var serializer = new ReceiptSnapshotJsonSerializer();
        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context, seed, serializer: serializer)
                .CheckoutAsync(CreateRequest(seed.ProductId));
            Assert.True(result.IsSuccess, result.AppError.ToString());
        }

        await using (var update = database.CreateContext())
        {
            var product = await update.Products.SingleAsync(p => p.Id == seed.ProductId);
            product.UpdateDetails(
                product.CategoryId, product.Code, product.Barcode,
                "Tên sản phẩm đã đổi", product.Description, product.UnitName,
                "images/new.png", UtcNow.AddMinutes(1));
            product.ChangePrices(99_000, 199_000, UtcNow.AddMinutes(1));
            await update.SaveChangesAsync();
        }

        await using var verify = database.CreateContext();
        var payload = await verify.OrderReceiptSnapshots
            .Select(snapshot => snapshot.PayloadJson)
            .SingleAsync();
        var restored = serializer.Deserialize(payload);
        var line = Assert.Single(restored.Lines);
        Assert.Equal("Cà phê sữa đá", line.ProductName);
        Assert.Equal(30_000, line.UnitSalePrice);
        Assert.DoesNotContain("Tên sản phẩm đã đổi", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Serializer_failure_must_rollback_entire_checkout()
    {
        await AssertCheckoutFailureRollsBackAsync(
            (context, seed) => CreateService(
                context, seed, serializer: new ThrowingSerializer()));
    }

    [Fact]
    public async Task Snapshot_repository_failure_must_rollback_entire_checkout()
    {
        await AssertCheckoutFailureRollsBackAsync(
            (context, seed) => CreateService(
                context, seed, snapshotRepository: new ThrowingSnapshotRepository()));
    }

    [Fact]
    public async Task Second_save_failure_must_rollback_entire_checkout()
    {
        await AssertCheckoutFailureRollsBackAsync(
            (context, seed) => CreateService(
                context, seed, unitOfWork: new ThrowOnSecondSaveUnitOfWork(context)));
    }

    [Fact]
    public async Task Duplicate_snapshot_for_same_order_must_be_rejected()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await database.SeedAsync();
        int orderId;
        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context, seed)
                .CheckoutAsync(CreateRequest(seed.ProductId));
            Assert.True(result.IsSuccess, result.AppError.ToString());
            orderId = result.Value.OrderId;
        }

        await using (var duplicateContext = database.CreateContext())
        {
            duplicateContext.OrderReceiptSnapshots.Add(
                new OrderReceiptSnapshot(orderId, 1, "{\"duplicate\":true}", UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(
                () => duplicateContext.SaveChangesAsync());
        }

        await using var verify = database.CreateContext();
        var persisted = await verify.OrderReceiptSnapshots.SingleAsync();
        Assert.DoesNotContain("duplicate", persisted.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_order_without_snapshot_must_survive_migration()
    {
        await using var connection = new SqliteConnection(
            "Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        int orderId;

        await using (var before = new PosDbContext(options))
        {
            var migrations = before.Database.GetMigrations().ToArray();
            var previous = migrations[^2];
            await before.GetService<IMigrator>().MigrateAsync(previous);
            var seed = await SeedAsync(before);
            var order = CreateCompletedOrder(seed.UserId, seed.ProductId);
            before.Orders.Add(order);
            await before.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var latest = new PosDbContext(options))
        {
            await latest.Database.MigrateAsync();
            Assert.True(await latest.Orders.AnyAsync(order => order.Id == orderId));
            Assert.Equal(0, await latest.OrderReceiptSnapshots.CountAsync());
        }
    }

    [Fact]
    public async Task Deleting_order_with_snapshot_must_be_restricted()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await database.SeedAsync();
        int orderId;
        await using (var context = database.CreateContext())
        {
            var result = await CreateService(context, seed)
                .CheckoutAsync(CreateRequest(seed.ProductId));
            Assert.True(result.IsSuccess, result.AppError.ToString());
            orderId = result.Value.OrderId;
        }

        await using (var delete = database.CreateContext())
        {
            delete.Orders.Remove(await delete.Orders.SingleAsync(o => o.Id == orderId));
            await Assert.ThrowsAsync<DbUpdateException>(() => delete.SaveChangesAsync());
        }

        await using var verify = database.CreateContext();
        Assert.True(await verify.Orders.AnyAsync(o => o.Id == orderId));
        Assert.True(await verify.OrderReceiptSnapshots.AnyAsync(s => s.OrderId == orderId));
    }

    [Fact]
    public async Task Receipt_snapshot_repository_get_by_order_id_must_be_no_tracking()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await database.SeedAsync();
        int orderId;
        await using (var checkout = database.CreateContext())
        {
            var result = await CreateService(checkout, seed)
                .CheckoutAsync(CreateRequest(seed.ProductId));
            Assert.True(result.IsSuccess, result.AppError.ToString());
            orderId = result.Value.OrderId;
        }

        await using var read = database.CreateContext();
        var repository = new OrderReceiptSnapshotRepository(read);
        Assert.NotNull(await repository.GetByOrderIdAsync(orderId));
        Assert.Empty(read.ChangeTracker.Entries<OrderReceiptSnapshot>());
    }

    [Fact]
    public async Task Persisted_payload_must_not_contain_cost_price_or_known_secrets()
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await database.SeedAsync();
        await using var context = database.CreateContext();
        var result = await CreateService(context, seed)
            .CheckoutAsync(CreateRequest(seed.ProductId));
        Assert.True(result.IsSuccess, result.AppError.ToString());

        await using var verify = database.CreateContext();
        var payload = await verify.OrderReceiptSnapshots
            .Select(snapshot => snapshot.PayloadJson)
            .SingleAsync();
        Assert.DoesNotContain("UnitCostPrice", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CostPrice", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Checkout_service_must_not_depend_on_print_service()
    {
        var constructor = Assert.Single(typeof(CheckoutService).GetConstructors());
        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IReceiptService));
    }

    [Fact]
    public void Receipt_snapshot_dependencies_must_have_required_lifetimes_and_scopes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(CreateConfiguration());
        services.AddScoped<CheckoutService>();
        var repositoryDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOrderReceiptSnapshotRepository));
        Assert.Equal(ServiceLifetime.Scoped, repositoryDescriptor.Lifetime);
        Assert.Equal(typeof(OrderReceiptSnapshotRepository), repositoryDescriptor.ImplementationType);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var serializer1 = provider.GetRequiredService<IReceiptSnapshotSerializer>();
        var serializer2 = provider.GetRequiredService<IReceiptSnapshotSerializer>();
        Assert.Same(serializer1, serializer2);
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();
        var repository1 = scope1.ServiceProvider
            .GetRequiredService<IOrderReceiptSnapshotRepository>();
        Assert.Same(
            repository1,
            scope1.ServiceProvider.GetRequiredService<IOrderReceiptSnapshotRepository>());
        Assert.NotSame(
            repository1,
            scope2.ServiceProvider.GetRequiredService<IOrderReceiptSnapshotRepository>());
        Assert.Same(
            scope1.ServiceProvider.GetRequiredService<PosDbContext>(),
            GetRepositoryContext(repository1));
        Assert.NotNull(
            scope1.ServiceProvider.GetRequiredService<CheckoutService>());
    }

    private static PosDbContext GetRepositoryContext(
        IOrderReceiptSnapshotRepository repository)
    {
        var field = typeof(OrderReceiptSnapshotRepository).GetField(
            "_dbContext",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        return Assert.IsType<PosDbContext>(field?.GetValue(repository));
    }

    private static async Task AssertCheckoutFailureRollsBackAsync(
        Func<PosDbContext, SeedData, CheckoutService> serviceFactory)
    {
        await using var database = await TestDatabase.CreateAsync();
        var seed = await database.SeedAsync();
        await using (var context = database.CreateContext())
        {
            var result = await serviceFactory(context, seed)
                .CheckoutAsync(CreateRequest(seed.ProductId));
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCodes.Checkout.SaveFailed, result.AppError.Code);
        }

        await using var verify = database.CreateContext();
        Assert.Equal(0, await verify.Orders.CountAsync());
        Assert.Equal(0, await verify.OrderItems.CountAsync());
        Assert.Equal(0, await verify.OrderReceiptSnapshots.CountAsync());
        Assert.Equal(0, await verify.InventoryMovements.CountAsync());
        Assert.Equal(
            10,
            await verify.Products
                .Where(product => product.Id == seed.ProductId)
                .Select(product => product.StockQuantity)
                .SingleAsync());
    }

    private static CheckoutService CreateService(
        PosDbContext context,
        SeedData seed,
        IReceiptSnapshotSerializer? serializer = null,
        IOrderReceiptSnapshotRepository? snapshotRepository = null,
        IUnitOfWork? unitOfWork = null)
    {
        var currentUser = new CurrentUserService();
        currentUser.SetCurrentUser(
            new AuthenticatedUserDto(
                seed.UserId, "receipt.cashier", "Thu ngân Việt",
                Role.Cashier, UtcNow));
        return new CheckoutService(
            new ProductRepository(context),
            new OrderRepository(context),
            snapshotRepository ?? new OrderReceiptSnapshotRepository(context),
            new InventoryMovementRepository(context),
            unitOfWork ?? new EfUnitOfWork(context),
            new FixedOrderCodeGenerator($"HD-{Guid.NewGuid():N}"),
            currentUser,
            new FixedClock(),
            NullLogger<CheckoutService>.Instance,
            serializer ?? new ReceiptSnapshotJsonSerializer(),
            new FixedStoreProvider());
    }

    private static CheckoutRequest CreateRequest(int productId) =>
        new(
            [new CheckoutLineRequest(productId, 2, notes: "Ít đá")],
            PaymentMethod.Cash,
            cashReceived: 100_000,
            notes: "Giao tại quầy – tiếng Việt");

    private static Order CreateCompletedOrder(int userId, int productId)
    {
        var order = new Order($"OLD-{Guid.NewGuid():N}", userId, UtcNow);
        order.AddItem(
            productId, "SP-RECEIPT", "Cà phê sữa đá", "Ly",
            1, 10_000, 30_000, UtcNow);
        order.PrepareForPayment(UtcNow);
        order.MarkPaid(PaymentMethod.Cash, 30_000, UtcNow);
        order.Complete(UtcNow);
        return order;
    }

    private static DbContextOptions<PosDbContext> CreateOptions(
        SqliteConnection connection) =>
        new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditableEntityInterceptor())
            .Options;

    private static async Task<SeedData> SeedAsync(PosDbContext context)
    {
        var category = new Category(
            $"Danh mục {Guid.NewGuid():N}", 1, UtcNow);
        int userId;
        if (await HasColumnAsync(context, "Users", "ForcePasswordChange") &&
            await HasColumnAsync(context, "Users", "LastFailedLoginAtUtc"))
        {
            var user = new User(
                $"receipt.{Guid.NewGuid():N}",
                "fixture",
                "Thu ngân Việt",
                Role.Cashier,
                UtcNow);
            context.AddRange(category, user);
            await context.SaveChangesAsync();
            userId = user.Id;
        }
        else
        {
            userId = 1;
            await InsertLegacyUserAsync((SqliteConnection)context.Database.GetDbConnection(), userId, UtcNow);
            context.Add(category);
            await context.SaveChangesAsync();
        }
        var product = new Product(
            category.Id, "SP-RECEIPT", "Cà phê sữa đá", "Ly",
            10_000, 30_000, 10, 2, true, false, UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return new SeedData(userId, product.Id);
    }

    private static async Task<bool> HasColumnAsync(PosDbContext context, string table, string column)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info('{table}');";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static async Task InsertLegacyUserAsync(SqliteConnection connection, int userId, DateTimeOffset now)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "Users" (
                "Id", "Username", "NormalizedUsername", "PasswordHash", "FullName",
                "Role", "IsActive", "FailedLoginAttempts", "LockedUntilUtc", "LastLoginAtUtc",
                "ConcurrencyToken", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ($id, 'receipt.legacy', 'RECEIPT.LEGACY', 'fixture', 'Thu ngân Việt',
                3, 1, 0, NULL, NULL, $token, $created, $created);
            """;
        command.Parameters.AddWithValue("$id", userId);
        command.Parameters.AddWithValue("$token", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$created", now.ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync();
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Infrastructure:DatabasePath"] = "data/receipt-persistence-di.db",
                    ["Infrastructure:DatabaseTimeoutSeconds"] = "30",
                    ["Infrastructure:ApplyMigrationsOnStartup"] = "false",
                    ["Infrastructure:SeedDefaultAdministrator"] = "false",
                    ["Store:Name"] = "Cửa hàng DI"
                })
            .Build();

    private sealed record SeedData(int UserId, int ProductId);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => ReceiptSnapshotPersistenceTests.UtcNow;
    }

    private sealed class FixedOrderCodeGenerator(string orderCode) : IOrderCodeGenerator
    {
        public string Generate(DateTimeOffset utcNow) => orderCode;
    }

    private sealed class FixedStoreProvider : IReceiptStoreSnapshotProvider
    {
        public ReceiptStoreSnapshotDto GetCurrentSnapshot() =>
            new(
                "Cửa hàng Việt",
                "123 Đường Trần Phú",
                "0901 234 567",
                "0101234567",
                "Cảm ơn quý khách!");
    }

    private sealed class ThrowingSerializer : IReceiptSnapshotSerializer
    {
        public string Serialize(ReceiptRequest snapshot) =>
            throw new InvalidOperationException("Intentional serializer failure.");

        public ReceiptRequest Deserialize(string json) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingSnapshotRepository :
        IOrderReceiptSnapshotRepository
    {
        public Task AddAsync(
            OrderReceiptSnapshot snapshot,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Intentional repository failure.");

        public Task<OrderReceiptSnapshot?> GetByOrderIdAsync(
            int orderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<OrderReceiptSnapshot?>(null);
    }

    private sealed class ThrowOnSecondSaveUnitOfWork : IUnitOfWork
    {
        private readonly EfUnitOfWork _inner;
        private int _saveCount;

        public ThrowOnSecondSaveUnitOfWork(PosDbContext context)
        {
            _inner = new EfUnitOfWork(context);
        }

        public Task<IApplicationTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default) =>
            _inner.BeginTransactionAsync(cancellationToken);

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            _saveCount++;
            return _saveCount == 2
                ? throw new InvalidOperationException("Intentional second save failure.")
                : _inner.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<PosDbContext> _options;

        private TestDatabase(SqliteConnection connection)
        {
            _connection = connection;
            _options = CreateOptions(connection);
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection(
                "Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var database = new TestDatabase(connection);
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync();
            return database;
        }

        public PosDbContext CreateContext() => new(_options);

        public async Task<SeedData> SeedAsync()
        {
            await using var context = CreateContext();
            return await ReceiptSnapshotPersistenceTests.SeedAsync(context);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
