using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Printing;
using POS.Application.Abstractions.Services;
using POS.Application.Common;
using POS.Application.DTOs.Orders;
using POS.Application.DTOs.Printing;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Printing;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class OrderHistoryServiceTests
{
    [Fact]
    public async Task Search_must_filter_by_order_code()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddOrderAsync("HD-TARGET-01", OrderStatus.Draft);
        await database.AddOrderAsync("HD-OTHER-02", OrderStatus.Draft);

        var result = await database.CreateService().SearchAsync(
            new(SearchTerm: "TARGET"));

        Assert.True(result.IsSuccess);
        Assert.Equal("HD-TARGET-01", Assert.Single(result.Value.Items).OrderCode);
    }

    [Fact]
    public async Task Search_must_filter_by_status()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddOrderAsync("HD-DRAFT", OrderStatus.Draft);
        await database.AddOrderAsync("HD-COMPLETE", OrderStatus.Completed);

        var result = await database.CreateService().SearchAsync(
            new(Status: OrderStatus.Completed));

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Items,
            item => Assert.Equal(OrderStatus.Completed, item.Status));
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task Search_must_filter_by_payment_method()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddOrderAsync(
            "HD-CASH",
            OrderStatus.Completed,
            PaymentMethod.Cash);
        await database.AddOrderAsync(
            "HD-QR",
            OrderStatus.Completed,
            PaymentMethod.VietQr);

        var result = await database.CreateService().SearchAsync(
            new(PaymentMethod: PaymentMethod.VietQr));

        Assert.True(result.IsSuccess);
        Assert.Equal("HD-QR", Assert.Single(result.Value.Items).OrderCode);
    }

    [Fact]
    public async Task Search_must_filter_by_cashier_user_id()
    {
        await using var database = await TestDatabase.CreateAsync();
        var secondCashier = await database.AddCashierAsync("Thu ngân Hai");
        await database.AddOrderAsync("HD-ONE", OrderStatus.Draft);
        await database.AddOrderAsync(
            "HD-TWO",
            OrderStatus.Draft,
            cashierId: secondCashier);

        var result = await database.CreateService().SearchAsync(
            new(CashierUserId: secondCashier));

        Assert.Equal("HD-TWO", Assert.Single(result.Value.Items).OrderCode);
    }

    [Fact]
    public async Task Search_must_filter_by_utc_date_range()
    {
        await using var database = await TestDatabase.CreateAsync();
        var inside = TestDatabase.UtcNow;
        await database.AddOrderAsync(
            "HD-INSIDE",
            OrderStatus.Draft,
            createdAtUtc: inside);
        await database.AddOrderAsync(
            "HD-OUTSIDE",
            OrderStatus.Draft,
            createdAtUtc: inside.AddDays(-2));

        var result = await database.CreateService().SearchAsync(
            new(
                FromUtc: inside,
                ToUtc: inside));

        Assert.Equal("HD-INSIDE", Assert.Single(result.Value.Items).OrderCode);
    }

    [Fact]
    public async Task Search_filters_must_apply_before_count_and_paging()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddOrderAsync(
            "HD-CASH-1",
            OrderStatus.Completed,
            PaymentMethod.Cash);
        await database.AddOrderAsync(
            "HD-CASH-2",
            OrderStatus.Completed,
            PaymentMethod.Cash);
        await database.AddOrderAsync(
            "HD-QR",
            OrderStatus.Completed,
            PaymentMethod.VietQr);

        var result = await database.CreateService().SearchAsync(
            new(
                Status: OrderStatus.Completed,
                PaymentMethod: PaymentMethod.Cash,
                PageSize: 1));

        Assert.Equal(2, result.Value.TotalCount);
        Assert.Single(result.Value.Items);
        Assert.Equal(PaymentMethod.Cash, result.Value.Items[0].PaymentMethod);
    }

    [Fact]
    public async Task Search_must_order_newest_first()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddOrderAsync(
            "HD-OLD",
            OrderStatus.Draft,
            createdAtUtc: TestDatabase.UtcNow.AddHours(-1));
        await database.AddOrderAsync(
            "HD-NEW",
            OrderStatus.Draft,
            createdAtUtc: TestDatabase.UtcNow);

        var result = await database.CreateService().SearchAsync(new());

        Assert.Equal(
            ["HD-NEW", "HD-OLD"],
            result.Value.Items.Select(item => item.OrderCode));
    }

    [Fact]
    public async Task Search_must_not_deserialize_receipt_payload_for_list()
    {
        await using var database = await TestDatabase.CreateAsync();
        await database.AddOrderAsync("HD-LIST", OrderStatus.Draft);
        var serializer = new RecordingSerializer();

        var result = await database.CreateService(serializer).SearchAsync(new());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, serializer.DeserializeCalls);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task Search_must_reject_invalid_paging(
        int pageNumber,
        int pageSize)
    {
        var orders = new RecordingOrderRepository();
        var service = CreateService(orders);

        var result = await service.SearchAsync(
            new(PageNumber: pageNumber, PageSize: pageSize));

        Assert.True(result.IsFailure);
        Assert.Equal(0, orders.SearchCalls);
    }

    [Fact]
    public async Task Search_must_reject_invalid_date_range()
    {
        var orders = new RecordingOrderRepository();
        var service = CreateService(orders);

        var result = await service.SearchAsync(
            new(
                FromUtc: TestDatabase.UtcNow,
                ToUtc: TestDatabase.UtcNow.AddDays(-1)));

        Assert.True(result.IsFailure);
        Assert.Equal(0, orders.SearchCalls);
    }

    [Fact]
    public async Task Details_must_use_order_item_snapshot_values()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-SNAPSHOT",
            OrderStatus.Completed);
        await database.ChangeLiveProductAsync();

        var result = await database.CreateService().GetDetailsAsync(orderId);

        var line = Assert.Single(result.Value.Lines);
        Assert.Equal("Cà phê sữa", line.ProductName);
        Assert.Equal(25_000, line.UnitSalePrice);
    }

    [Fact]
    public void Details_must_not_expose_unit_cost_price()
    {
        var names = typeof(OrderHistoryLineDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("UnitCostPrice", names);
        Assert.DoesNotContain("CostPrice", names);
        Assert.DoesNotContain("Profit", names);
    }

    [Fact]
    public async Task Details_must_include_modifiers_and_notes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-MODIFIER",
            OrderStatus.Completed,
            includeModifier: true);

        var result = await database.CreateService().GetDetailsAsync(orderId);

        var line = Assert.Single(result.Value.Lines);
        Assert.Equal("Ít đá", line.Notes);
        Assert.Equal("Trân châu", Assert.Single(line.Modifiers).ModifierName);
    }

    [Fact]
    public async Task Details_must_report_receipt_snapshot_available()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-HAS-RECEIPT",
            OrderStatus.Completed,
            addSnapshot: true);

        var result = await database.CreateService().GetDetailsAsync(orderId);

        Assert.True(result.Value.HasReceiptSnapshot);
    }

    [Fact]
    public async Task Old_order_without_snapshot_must_report_snapshot_unavailable()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-OLD-NO-RECEIPT",
            OrderStatus.Completed);

        var result = await database.CreateService().GetDetailsAsync(orderId);

        Assert.False(result.Value.HasReceiptSnapshot);
    }

    [Fact]
    public async Task Details_must_return_not_found_for_missing_order()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.CreateService().GetDetailsAsync(999_999);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.Orders.NotFound, result.AppError.Code);
    }

    [Fact]
    public async Task Details_repository_read_must_be_no_tracking()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-NOTRACK",
            OrderStatus.Completed);
        await using var context = database.CreateContext();
        var repository = new OrderRepository(context);

        var order = await repository.GetByIdReadOnlyAsync(orderId);

        Assert.NotNull(order);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Reprint_must_deserialize_persisted_snapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-REPRINT",
            OrderStatus.Completed,
            addSnapshot: true);
        var serializer = new RecordingSerializer();

        var result = await database.CreateService(serializer)
            .GetReprintReceiptAsync(orderId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, serializer.DeserializeCalls);
    }

    [Fact]
    public async Task Reprint_must_preserve_store_and_transaction_snapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-UNICODE",
            OrderStatus.Completed,
            addSnapshot: true);

        var result = await database.CreateService()
            .GetReprintReceiptAsync(orderId);
        var receipt = result.Value;

        Assert.Equal("Cửa hàng Việt", receipt.Store.Name);
        Assert.Equal("1 Đường Sữa", receipt.Store.Address);
        Assert.Equal(orderId, receipt.OrderId);
        Assert.Equal("HD-UNICODE", receipt.OrderCode);
        Assert.Equal("Thu ngân Một", receipt.CashierName);
        Assert.Equal(PaymentMethod.Cash, receipt.PaymentMethod);
        Assert.Equal(25_000, receipt.Subtotal);
        Assert.Equal(25_000, receipt.TotalAmount);
        Assert.Equal(30_000, receipt.CashReceived);
        Assert.Equal(5_000, receipt.ChangeAmount);
        Assert.Equal("Cà phê sữa", Assert.Single(receipt.Lines).ProductName);
        Assert.Equal("Mang đi", receipt.Notes);
    }

    [Fact]
    public async Task Reprint_must_set_copy_kind_reprint_and_copy_number_one()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-COPY",
            OrderStatus.Completed,
            addSnapshot: true);

        var receipt = (await database.CreateService()
            .GetReprintReceiptAsync(orderId)).Value;

        Assert.Equal(ReceiptCopyKind.Reprint, receipt.CopyKind);
        Assert.Equal(1, receipt.CopyNumber);
    }

    [Fact]
    public async Task Reprint_must_not_mutate_original_persisted_payload()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-IMMUTABLE",
            OrderStatus.Completed,
            addSnapshot: true);
        var before = await database.GetPayloadAsync(orderId);

        await database.CreateService().GetReprintReceiptAsync(orderId);

        var after = await database.GetPayloadAsync(orderId);
        Assert.Equal(before, after);
        var original = new ReceiptSnapshotJsonSerializer().Deserialize(after);
        Assert.Equal(ReceiptCopyKind.Original, original.CopyKind);
        Assert.Equal(0, original.CopyNumber);
    }

    [Fact]
    public async Task Reprint_must_fail_for_order_without_snapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-LEGACY",
            OrderStatus.Completed);

        var result = await database.CreateService()
            .GetReprintReceiptAsync(orderId);

        Assert.True(result.IsFailure);
        Assert.Contains("trước khi", result.AppError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reprint_must_fail_when_snapshot_order_id_does_not_match()
    {
        var receipt = CreateReceipt(2, "HD-MISMATCH");
        var serializer = new ReceiptSnapshotJsonSerializer();
        var service = CreateService(
            new RecordingOrderRepository(),
            new FixedSnapshotRepository(
                new OrderReceiptSnapshot(
                    1,
                    receipt.SnapshotVersion,
                    serializer.Serialize(receipt),
                    TestDatabase.UtcNow)),
            serializer);

        var result = await service.GetReprintReceiptAsync(1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.Orders.ReceiptSnapshotInvalid, result.AppError.Code);
    }

    [Fact]
    public async Task Reprint_must_fail_when_snapshot_version_does_not_match_payload()
    {
        var receipt = CreateReceipt(1, "HD-VERSION");
        var serializer = new ReceiptSnapshotJsonSerializer();
        var service = CreateService(
            new RecordingOrderRepository(),
            new FixedSnapshotRepository(
                new OrderReceiptSnapshot(
                    1,
                    receipt.SnapshotVersion + 1,
                    serializer.Serialize(receipt),
                    TestDatabase.UtcNow)),
            serializer);

        var result = await service.GetReprintReceiptAsync(1);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Repeated_reprint_reads_must_not_change_database()
    {
        await using var database = await TestDatabase.CreateAsync();
        var orderId = await database.AddOrderAsync(
            "HD-REPEAT",
            OrderStatus.Completed,
            addSnapshot: true);
        var before = await database.CountBusinessRowsAsync();

        var first = await database.CreateService().GetReprintReceiptAsync(orderId);
        var second = await database.CreateService().GetReprintReceiptAsync(orderId);

        Assert.Equal(1, first.Value.CopyNumber);
        Assert.Equal(1, second.Value.CopyNumber);
        Assert.Equal(before, await database.CountBusinessRowsAsync());
    }

    [Fact]
    public void IOrderHistoryService_must_be_scoped_and_serializer_singleton()
    {
        var services = CreateInfrastructureServices();
        var history = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IOrderHistoryService));
        var serializer = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IReceiptSnapshotSerializer));

        Assert.Equal(ServiceLifetime.Scoped, history.Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, serializer.Lifetime);
    }

    [Fact]
    public void ValidateOnBuild_and_ValidateScopes_must_pass()
    {
        var services = CreateInfrastructureServices();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IOrderHistoryService>();
        var same = firstScope.ServiceProvider.GetRequiredService<IOrderHistoryService>();
        var second = secondScope.ServiceProvider.GetRequiredService<IOrderHistoryService>();
        Assert.Same(first, same);
        Assert.NotSame(first, second);
    }

    private static ServiceCollection CreateInfrastructureServices()
    {
        var values = new Dictionary<string, string?>
        {
            ["Infrastructure:DatabasePath"] = "data/order-history-registration.db",
            ["Infrastructure:DatabaseTimeoutSeconds"] = "30",
            ["Infrastructure:ApplyMigrationsOnStartup"] = "false",
            ["Infrastructure:SeedDefaultAdministrator"] = "false"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services;
    }

    private static OrderHistoryService CreateService(
        IOrderRepository orders,
        IOrderReceiptSnapshotRepository? snapshots = null,
        IReceiptSnapshotSerializer? serializer = null) =>
        new(
            orders,
            snapshots ?? new FixedSnapshotRepository(null),
            serializer ?? new ReceiptSnapshotJsonSerializer());

    private static ReceiptRequest CreateReceipt(int orderId, string code)
    {
        var line = new ReceiptLineDto(
            1,
            1,
            "SP-CAFE",
            "Cà phê sữa",
            "Ly",
            1,
            25_000,
            0,
            25_000,
            25_000,
            0,
            25_000,
            "Ít đá",
            []);
        return new ReceiptRequest(
            new ReceiptStoreSnapshotDto(
                "Cửa hàng Việt",
                "1 Đường Sữa"),
            ReceiptCopyKind.Original,
            0,
            orderId,
            code,
            "Thu ngân Một",
            TestDatabase.UtcNow,
            PaymentMethod.Cash,
            25_000,
            0,
            25_000,
            30_000,
            5_000,
            [line],
            notes: "Mang đi",
            paidAtUtc: TestDatabase.UtcNow.AddMinutes(1));
    }

    private sealed class RecordingSerializer : IReceiptSnapshotSerializer
    {
        private readonly ReceiptSnapshotJsonSerializer _inner = new();
        public int DeserializeCalls { get; private set; }
        public string Serialize(ReceiptRequest snapshot) => _inner.Serialize(snapshot);
        public ReceiptRequest Deserialize(string json)
        {
            DeserializeCalls++;
            return _inner.Deserialize(json);
        }
    }

    private sealed class FixedSnapshotRepository(
        OrderReceiptSnapshot? snapshot) : IOrderReceiptSnapshotRepository
    {
        public Task AddAsync(
            OrderReceiptSnapshot value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<OrderReceiptSnapshot?> GetByOrderIdAsync(
            int orderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class RecordingOrderRepository : IOrderRepository
    {
        public int SearchCalls { get; private set; }
        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(null);
        public Task<Order?> GetByIdReadOnlyAsync(int orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(null);
        public Task<Order?> GetByCodeAsync(string orderCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Order?>(null);
        public Task<PagedResult<Order>> SearchAsync(
            string? searchTerm,
            OrderStatus? status,
            int? customerId,
            int? cashierUserId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            SearchAsync(
                searchTerm,
                status,
                customerId,
                cashierUserId,
                fromUtc,
                toUtc,
                null,
                pageNumber,
                pageSize,
                cancellationToken);
        public Task<PagedResult<Order>> SearchAsync(
            string? searchTerm,
            OrderStatus? status,
            int? customerId,
            int? cashierUserId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            PaymentMethod? paymentMethod,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(PagedResult.Empty<Order>(pageNumber, pageSize));
        }
        public Task<bool> CodeExistsAsync(string orderCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        public static readonly DateTimeOffset UtcNow =
            new(2026, 7, 27, 8, 0, 0, TimeSpan.Zero);
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<PosDbContext> _options;
        private readonly int _categoryId;
        private readonly int _productId;
        private readonly int _cashierId;

        private TestDatabase(
            SqliteConnection connection,
            DbContextOptions<PosDbContext> options,
            int categoryId,
            int productId,
            int cashierId)
        {
            _connection = connection;
            _options = options;
            _categoryId = categoryId;
            _productId = productId;
            _cashierId = cashierId;
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection(
                "Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var context = new PosDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var category = new Category("Đồ uống", 1, UtcNow);
            context.Categories.Add(category);
            var cashier = new User(
                "cashier.one",
                "password-hash",
                "Thu ngân Một",
                Role.Cashier,
                UtcNow);
            context.Users.Add(cashier);
            await context.SaveChangesAsync();
            var product = new Product(
                category.Id,
                "SP-CAFE",
                "Cà phê hiện tại",
                "Ly",
                10_000,
                25_000,
                100,
                5,
                true,
                false,
                UtcNow);
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return new TestDatabase(
                connection,
                options,
                category.Id,
                product.Id,
                cashier.Id);
        }

        public PosDbContext CreateContext() => new(_options);

        public OrderHistoryService CreateService(
            IReceiptSnapshotSerializer? serializer = null)
        {
            var context = CreateContext();
            return new OrderHistoryService(
                new OrderRepository(context),
                new OrderReceiptSnapshotRepository(context),
                serializer ?? new ReceiptSnapshotJsonSerializer());
        }

        public async Task<int> AddCashierAsync(string name)
        {
            await using var context = CreateContext();
            var user = new User(
                $"cashier.{Guid.NewGuid():N}",
                "password-hash",
                name,
                Role.Cashier,
                UtcNow);
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user.Id;
        }

        public async Task<int> AddOrderAsync(
            string code,
            OrderStatus status,
            PaymentMethod paymentMethod = PaymentMethod.Cash,
            int? cashierId = null,
            DateTimeOffset? createdAtUtc = null,
            bool includeModifier = false,
            bool addSnapshot = false)
        {
            await using var context = CreateContext();
            var created = createdAtUtc ?? UtcNow;
            var order = new Order(
                code,
                cashierId ?? _cashierId,
                created,
                notes: "Ghi chú đơn");
            if (status != OrderStatus.Draft)
            {
                var item = order.AddItem(
                    _productId,
                    "SP-CAFE",
                    "Cà phê sữa",
                    "Ly",
                    1,
                    10_000,
                    25_000,
                    created,
                    "Ít đá");
                if (includeModifier)
                {
                    order.AddItemModifier(
                        item,
                        1,
                        1,
                        "Topping",
                        "Trân châu",
                        1,
                        0,
                        created);
                }
                order.PrepareForPayment(created.AddMinutes(1));
                if (status != OrderStatus.PendingPayment)
                {
                    order.MarkPaid(
                        paymentMethod,
                        paymentMethod == PaymentMethod.Cash ? 30_000 : 0,
                        created.AddMinutes(2));
                    if (status == OrderStatus.Completed)
                    {
                        order.Complete(created.AddMinutes(3));
                    }
                }
            }
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            if (addSnapshot)
            {
                var receipt = CreateReceipt(order.Id, order.OrderCode);
                context.OrderReceiptSnapshots.Add(
                    new OrderReceiptSnapshot(
                        order.Id,
                        receipt.SnapshotVersion,
                        new ReceiptSnapshotJsonSerializer().Serialize(receipt),
                        created.AddMinutes(3)));
                await context.SaveChangesAsync();
            }
            return order.Id;
        }

        public async Task ChangeLiveProductAsync()
        {
            await using var context = CreateContext();
            var product = await context.Products.SingleAsync(
                value => value.Id == _productId);
            product.UpdateDetails(
                _categoryId,
                product.Code,
                product.Barcode,
                "Tên sản phẩm đã đổi",
                product.Description,
                product.UnitName,
                "changed.png",
                UtcNow.AddDays(1));
            product.ChangePrices(12_000, 99_000, UtcNow.AddDays(1));
            await context.SaveChangesAsync();
        }

        public async Task<string> GetPayloadAsync(int orderId)
        {
            await using var context = CreateContext();
            return await context.OrderReceiptSnapshots
                .Where(snapshot => snapshot.OrderId == orderId)
                .Select(snapshot => snapshot.PayloadJson)
                .SingleAsync();
        }

        public async Task<(int Orders, int Items, int Movements, int Snapshots)>
            CountBusinessRowsAsync()
        {
            await using var context = CreateContext();
            return (
                await context.Orders.CountAsync(),
                await context.OrderItems.CountAsync(),
                await context.InventoryMovements.CountAsync(),
                await context.OrderReceiptSnapshots.CountAsync());
        }

        public async ValueTask DisposeAsync() =>
            await _connection.DisposeAsync();
    }
}
