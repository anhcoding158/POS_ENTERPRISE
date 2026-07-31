using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Printing;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.Printing;
using POS.Application.Services;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Orders;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Printing;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class CheckoutIdempotencyApplicationTests
{
    [Fact]
    public async Task Prepare_creates_recoverable_journal_without_business_mutation()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        await using var context = database.Context();
        var result = await database.Service(context).PrepareCheckoutAsync(database.Request(Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        await database.AssertStateAsync(journals: 1, orders: 0, stock: 10, movements: 0, receipts: 0);
        await using var restart = database.Context();
        var recovery = await database.Service(restart).GetCheckoutRecoveryAsync();
        var item = Assert.Single(recovery.Value);
        Assert.True(item.CanRetry);
        Assert.True(item.CanAbandon);
        Assert.NotNull(item.PreparedRequest);
        Assert.Equal(30_000, item.TotalAmount);
    }

    [Fact]
    public async Task Duplicate_prepare_same_payload_returns_existing_and_different_payload_conflicts()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var id = Guid.NewGuid();
        await using (var first = database.Context())
            Assert.True((await database.Service(first).PrepareCheckoutAsync(database.Request(id))).IsSuccess);
        await using (var same = database.Context())
            Assert.True((await database.Service(same).PrepareCheckoutAsync(database.Request(id))).IsSuccess);
        await using (var different = database.Context())
        {
            var conflict = await database.Service(different).PrepareCheckoutAsync(
                database.Request(id, quantity: 2, cash: 100_000));
            Assert.True(conflict.IsFailure);
            Assert.Equal("CHECKOUT.IDEMPOTENCY_CONFLICT", conflict.AppError.Code);
        }
        await database.AssertStateAsync(1, 0, 10, 0, 0);
    }

    [Fact]
    public async Task Concurrent_prepare_same_payload_creates_exactly_one_journal()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        await using var context1 = database.Context();
        await using var context2 = database.Context();
        var results = await Task.WhenAll(
            database.Service(context1).PrepareCheckoutAsync(request),
            database.Service(context2).PrepareCheckoutAsync(request));
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(results[0].Value.ClientRequestId, results[1].Value.ClientRequestId);
        await database.AssertStateAsync(1, 0, 10, 0, 0);
    }

    [Fact]
    public async Task Concurrent_prepare_different_payload_has_one_winner_and_one_conflict()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var id = Guid.NewGuid();
        await using var context1 = database.Context();
        await using var context2 = database.Context();
        var results = await Task.WhenAll(
            database.Service(context1).PrepareCheckoutAsync(database.Request(id)),
            database.Service(context2).PrepareCheckoutAsync(database.Request(id, quantity: 2, cash: 100_000)));
        Assert.Single(results, result => result.IsSuccess);
        var conflict = Assert.Single(results, result => result.IsFailure);
        Assert.Equal("CHECKOUT.IDEMPOTENCY_CONFLICT", conflict.AppError.Code);
        await database.AssertStateAsync(1, 0, 10, 0, 0);
    }

    [Fact]
    public async Task Changed_product_price_makes_prepared_checkout_stale_without_business_mutation()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        await using var prepareContext = database.Context();
        var service = database.Service(prepareContext);
        Assert.True((await service.PrepareCheckoutAsync(request)).IsSuccess);
        await using (var update = database.Context())
        {
            var product = await update.Products.SingleAsync();
            product.ChangePrices(10_000, 35_000, CheckoutDatabase.Now.AddMinutes(1));
            await update.SaveChangesAsync();
        }

        var result = await service.CheckoutAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("CHECKOUT.PREPARATION_STALE", result.AppError.Code);
        await database.AssertStateAsync(1, 0, 10, 0, 0);
        await using var verify = database.Context();
        Assert.Equal(CheckoutRequestStatus.Prepared,
            await verify.CheckoutRequestJournals.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Unsupported_modifier_or_discount_cannot_create_prepared_journal()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        await using var context = database.Context();
        var service = database.Service(context);
        var modifier = new CheckoutRequest(
            [new CheckoutLineRequest(database.ProductId, 1, [new(10, 1)])],
            PaymentMethod.Cash, 50_000, clientRequestId: Guid.NewGuid());
        var discount = new CheckoutRequest(
            [new CheckoutLineRequest(database.ProductId, 1)],
            PaymentMethod.Cash, 50_000, discountCode: "SALE",
            clientRequestId: Guid.NewGuid());
        Assert.True((await service.PrepareCheckoutAsync(modifier)).IsFailure);
        Assert.True((await service.PrepareCheckoutAsync(discount)).IsFailure);
        await database.AssertStateAsync(0, 0, 10, 0, 0);
    }

    [Fact]
    public async Task Prepared_quote_is_deterministic_and_contains_no_cost_or_secrets()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        await using var firstContext = database.Context();
        await using var secondContext = database.Context();
        var first = await database.Service(firstContext)
            .PrepareCheckoutAsync(database.Request(Guid.NewGuid()));
        var second = await database.Service(secondContext)
            .PrepareCheckoutAsync(database.Request(Guid.NewGuid()));
        Assert.Equal(first.Value.PreparedQuoteFingerprint, second.Value.PreparedQuoteFingerprint);
        Assert.Equal(first.Value.PreparedQuoteJson, second.Value.PreparedQuoteJson);
        Assert.Matches("^[0-9A-F]{64}$", first.Value.PreparedQuoteFingerprint);
        Assert.DoesNotContain("Cost", first.Value.PreparedQuoteJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", first.Value.PreparedQuoteJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", first.Value.PreparedQuoteJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Receipt", first.Value.PreparedQuoteJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"version\":1", first.Value.PreparedQuoteJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Archived_product_after_prepare_is_ineligible_without_business_mutation()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        await using var prepareContext = database.Context();
        var service = database.Service(prepareContext);
        Assert.True((await service.PrepareCheckoutAsync(request)).IsSuccess);
        await using (var update = database.Context())
        {
            var product = await update.Products.SingleAsync();
            product.Archive(database.UserId, CheckoutDatabase.Now.AddMinutes(1));
            await update.SaveChangesAsync();
        }
        Assert.True((await service.CheckoutAsync(request)).IsFailure);
        await database.AssertStateAsync(1, 0, 10, 0, 0);
    }

    [Fact]
    public async Task Concurrent_same_request_commits_exactly_one_business_result()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        await using (var prepare = database.Context())
            Assert.True((await database.Service(prepare).PrepareCheckoutAsync(request)).IsSuccess);

        await using var context1 = database.Context();
        await using var context2 = database.Context();
        var results = await Task.WhenAll(
            database.Service(context1).CheckoutAsync(request),
            database.Service(context2).CheckoutAsync(request));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(results[0].Value.OrderId, results[1].Value.OrderId);
        Assert.Single(results, result => result.Value.IsIdempotentReplay);
        await database.AssertStateAsync(1, 1, 9, 1, 1);
        await using var verify = database.Context();
        var journal = await verify.CheckoutRequestJournals.SingleAsync();
        Assert.Equal(CheckoutRequestStatus.Completed, journal.Status);
        Assert.NotNull(journal.OrderId);
        await verify.Database.OpenConnectionAsync();
        await using var command = verify.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var integrity = (string?)await command.ExecuteScalarAsync();
        Assert.Equal("ok", integrity);
    }

    [Fact]
    public async Task Concurrent_process_and_abandon_has_only_one_valid_winner()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        await using (var prepare = database.Context())
            Assert.True((await database.Service(prepare).PrepareCheckoutAsync(request)).IsSuccess);
        await using var processContext = database.Context();
        await using var abandonContext = database.Context();
        var process = database.Service(processContext).CheckoutAsync(request);
        var abandon = database.Service(abandonContext)
            .AbandonCheckoutAsync(request.ClientRequestId);
        await Task.WhenAll(process, abandon);
        var processResult = await process;
        var abandonResult = await abandon;
        Assert.NotEqual(processResult.IsSuccess, abandonResult.IsSuccess);
        await using var verify = database.Context();
        var journal = await verify.CheckoutRequestJournals.SingleAsync();
        Assert.Contains(journal.Status, new[]
        {
            CheckoutRequestStatus.Completed,
            CheckoutRequestStatus.Abandoned
        });
        if (journal.Status == CheckoutRequestStatus.Completed)
            await database.AssertStateAsync(1, 1, 9, 1, 1);
        else
            await database.AssertStateAsync(1, 0, 10, 0, 0);
    }

    [Fact]
    public async Task Concurrent_acknowledge_is_idempotent()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        await using (var process = database.Context())
            Assert.True((await database.Service(process).CheckoutAsync(request)).IsSuccess);
        await using var context1 = database.Context();
        await using var context2 = database.Context();
        var results = await Task.WhenAll(
            database.Service(context1).AcknowledgeCheckoutAsync(request.ClientRequestId),
            database.Service(context2).AcknowledgeCheckoutAsync(request.ClientRequestId));
        Assert.All(results, result => Assert.True(result.IsSuccess));
        await using var verify = database.Context();
        Assert.NotNull(await verify.CheckoutRequestJournals
            .Select(x => x.AcknowledgedAtUtc).SingleAsync());
        await database.AssertStateAsync(1, 1, 9, 1, 1);
    }

    [Fact]
    public async Task Completed_replay_ignores_later_product_changes_and_has_no_side_effect()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        int orderId;
        await using (var process = database.Context())
        {
            var first = await database.Service(process).CheckoutAsync(request);
            Assert.True(first.IsSuccess);
            orderId = first.Value.OrderId;
        }
        await using (var update = database.Context())
        {
            var product = await update.Products.SingleAsync();
            product.UpdateDetails(product.CategoryId, product.Code, product.Barcode,
                "Tên live đã đổi", product.Description, product.UnitName, product.ImagePath,
                CheckoutDatabase.Now.AddMinutes(2));
            product.ChangePrices(product.CostPrice, 99_000, CheckoutDatabase.Now.AddMinutes(2));
            await update.SaveChangesAsync();
        }
        await using (var replayContext = database.Context())
        {
            var replay = await database.Service(replayContext).CheckoutAsync(request);
            Assert.True(replay.IsSuccess);
            Assert.True(replay.Value.IsIdempotentReplay);
            Assert.Equal(orderId, replay.Value.OrderId);
            Assert.Equal("Cà phê", Assert.Single(replay.Value.Lines).ProductName);
            Assert.Equal(30_000, replay.Value.TotalAmount);
            Assert.NotNull(replay.Value.ReceiptSnapshot);
        }
        await database.AssertStateAsync(1, 1, 9, 1, 1);
    }

    [Fact]
    public async Task Receipt_serialization_failure_rolls_back_everything_and_keeps_prepared()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        await using (var prepare = database.Context())
            Assert.True((await database.Service(prepare).PrepareCheckoutAsync(request)).IsSuccess);
        await using (var process = database.Context())
        {
            var result = await database.Service(process, serializer: new ThrowingSerializer())
                .CheckoutAsync(request);
            Assert.True(result.IsFailure);
        }
        await database.AssertStateAsync(1, 0, 10, 0, 0);
        await using var verify = database.Context();
        Assert.Equal(CheckoutRequestStatus.Prepared,
            await verify.CheckoutRequestJournals.Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Receipt_repository_failure_rolls_back_everything_and_retry_completes_once()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        await using (var prepare = database.Context())
            Assert.True((await database.Service(prepare).PrepareCheckoutAsync(request)).IsSuccess);
        await using (var fail = database.Context())
            Assert.True((await database.Service(fail, snapshots: new ThrowingSnapshotRepository())
                .CheckoutAsync(request)).IsFailure);
        await database.AssertStateAsync(1, 0, 10, 0, 0);
        await using (var retry = database.Context())
            Assert.True((await database.Service(retry).CheckoutAsync(request)).IsSuccess);
        await database.AssertStateAsync(1, 1, 9, 1, 1);
    }

    [Fact]
    public async Task Restart_recovery_distinguishes_prepared_completed_acknowledged_and_abandoned()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var prepared = database.Request(Guid.NewGuid());
        var completed = database.Request(Guid.NewGuid());
        var abandoned = database.Request(Guid.NewGuid());
        await using (var scope = database.Context())
        {
            var service = database.Service(scope);
            Assert.True((await service.PrepareCheckoutAsync(prepared)).IsSuccess);
            Assert.True((await service.CheckoutAsync(completed)).IsSuccess);
            Assert.True((await service.PrepareCheckoutAsync(abandoned)).IsSuccess);
            Assert.True((await service.AbandonCheckoutAsync(abandoned.ClientRequestId)).IsSuccess);
        }
        await using (var restart = database.Context())
        {
            var recovery = await database.Service(restart).GetCheckoutRecoveryAsync();
            Assert.Equal(2, recovery.Value.Count);
            Assert.Contains(recovery.Value, x => x.ClientRequestId == prepared.ClientRequestId &&
                x.Status == CheckoutRequestStatus.Prepared);
            Assert.Contains(recovery.Value, x => x.ClientRequestId == completed.ClientRequestId &&
                x.Status == CheckoutRequestStatus.Completed && x.OrderCode is not null);
        }
        await using (var acknowledge = database.Context())
        {
            var service = database.Service(acknowledge);
            Assert.True((await service.AcknowledgeCheckoutAsync(completed.ClientRequestId)).IsSuccess);
            Assert.True((await service.AcknowledgeCheckoutAsync(completed.ClientRequestId)).IsSuccess);
        }
        await using var final = database.Context();
        Assert.Single((await database.Service(final).GetCheckoutRecoveryAsync()).Value);
    }

    [Fact]
    public async Task Foreign_user_cannot_recover_process_acknowledge_or_abandon_journal()
    {
        await using var database = await CheckoutDatabase.CreateAsync();
        var request = database.Request(Guid.NewGuid());
        await using (var prepare = database.Context())
            Assert.True((await database.Service(prepare).PrepareCheckoutAsync(request)).IsSuccess);
        await using var foreignContext = database.Context();
        var foreign = database.Service(foreignContext, userId: database.OtherUserId);
        Assert.Empty((await foreign.GetCheckoutRecoveryAsync()).Value);
        Assert.True((await foreign.CheckoutAsync(request)).IsFailure);
        Assert.True((await foreign.AcknowledgeCheckoutAsync(request.ClientRequestId)).IsFailure);
        Assert.True((await foreign.AbandonCheckoutAsync(request.ClientRequestId)).IsFailure);
        await database.AssertStateAsync(1, 0, 10, 0, 0);
    }

    private sealed class CheckoutDatabase : IAsyncDisposable
    {
        public static readonly DateTimeOffset Now =
            new(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);
        private readonly string _path;
        private readonly DbContextOptions<PosDbContext> _options;
        private CheckoutDatabase(string path, DbContextOptions<PosDbContext> options) =>
            (_path, _options) = (path, options);
        public int UserId { get; private set; }
        public int OtherUserId { get; private set; }
        public int ProductId { get; private set; }
        public PosDbContext Context() => new(_options);

        public static async Task<CheckoutDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"pos-checkout-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite($"Data Source={path};Foreign Keys=True;Default Timeout=30;Pooling=False")
                .AddInterceptors(new AuditableEntityInterceptor())
                .Options;
            var database = new CheckoutDatabase(path, options);
            await using var context = database.Context();
            await context.Database.EnsureCreatedAsync();
            var category = new Category($"Checkout {Guid.NewGuid():N}", 1, Now);
            var user = new User($"cashier.{Guid.NewGuid():N}", "hash", "Thu ngân", Role.Cashier, Now);
            var other = new User($"other.{Guid.NewGuid():N}", "hash", "Thu ngân khác", Role.Cashier, Now);
            context.AddRange(category, user, other);
            await context.SaveChangesAsync();
            var product = new Product(category.Id, "CF", "Cà phê", "Ly",
                10_000, 30_000, 10, 2, true, false, Now);
            context.Products.Add(product);
            await context.SaveChangesAsync();
            database.UserId = user.Id;
            database.OtherUserId = other.Id;
            database.ProductId = product.Id;
            return database;
        }

        public CheckoutRequest Request(Guid id, int quantity = 1, long cash = 50_000) =>
            new([new CheckoutLineRequest(ProductId, quantity)], PaymentMethod.Cash,
                cash, notes: "Tại quầy", clientRequestId: id);

        public CheckoutService Service(
            PosDbContext context,
            int? userId = null,
            IReceiptSnapshotSerializer? serializer = null,
            IOrderReceiptSnapshotRepository? snapshots = null)
        {
            var currentUser = new CurrentUserService();
            currentUser.SetCurrentUser(new AuthenticatedUserDto(
                userId ?? UserId, "cashier", "Thu ngân", Role.Cashier, Now));
            return new CheckoutService(
                new ProductRepository(context),
                new OrderRepository(context),
                snapshots ?? new OrderReceiptSnapshotRepository(context),
                new InventoryMovementRepository(context),
                new EfUnitOfWork(context),
                new OrderCodeGenerator(),
                currentUser,
                new FixedClock(),
                NullLogger<CheckoutService>.Instance,
                serializer ?? new ReceiptSnapshotJsonSerializer(),
                new StoreProvider(),
                new CheckoutRequestJournalRepository(context),
                new CheckoutRequestCanonicalizer());
        }

        public async Task AssertStateAsync(
            int journals, int orders, int stock, int movements, int receipts)
        {
            await using var context = Context();
            Assert.Equal(journals, await context.CheckoutRequestJournals.CountAsync());
            Assert.Equal(orders, await context.Orders.CountAsync());
            Assert.Equal(orders, await context.OrderItems.CountAsync());
            Assert.Equal(stock, await context.Products.Select(x => x.StockQuantity).SingleAsync());
            Assert.Equal(movements, await context.InventoryMovements.CountAsync());
            Assert.Equal(receipts, await context.OrderReceiptSnapshots.CountAsync());
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(_path))
                File.Delete(_path);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => CheckoutDatabase.Now;
    }

    private sealed class StoreProvider : IReceiptStoreSnapshotProvider
    {
        public ReceiptStoreSnapshotDto GetCurrentSnapshot() =>
            new("POS Test", null, null, null, "Cảm ơn");
    }

    private sealed class ThrowingSerializer : IReceiptSnapshotSerializer
    {
        public string Serialize(ReceiptRequest snapshot) =>
            throw new InvalidOperationException("Synthetic receipt serialization failure.");
        public ReceiptRequest Deserialize(string json) =>
            throw new InvalidOperationException();
    }

    private sealed class ThrowingSnapshotRepository : IOrderReceiptSnapshotRepository
    {
        public Task AddAsync(OrderReceiptSnapshot snapshot, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Synthetic receipt repository failure.");
        public Task<OrderReceiptSnapshot?> GetByOrderIdAsync(
            int orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<OrderReceiptSnapshot?>(null);
    }
}
