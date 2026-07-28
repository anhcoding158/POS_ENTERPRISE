using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Printing;
using POS.Application.DTOs.Authentication;
using POS.Application.DTOs.Checkout;
using POS.Application.DTOs.HeldSales;
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

internal sealed class HeldSaleTestDatabase : IAsyncDisposable
{
    internal static readonly DateTimeOffset Now =
        new(2026, 7, 28, 8, 30, 0, TimeSpan.Zero);

    private readonly string _path;
    private readonly DbContextOptions<PosDbContext> _options;

    private HeldSaleTestDatabase(string path, DbContextOptions<PosDbContext> options)
    {
        _path = path;
        _options = options;
    }

    public int UserId { get; private set; }
    public int OtherUserId { get; private set; }
    public int ProductId { get; private set; }

    public static async Task<HeldSaleTestDatabase> CreateAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pos-held-sale-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite($"Data Source={path};Foreign Keys=True;Default Timeout=30;Pooling=False")
            .AddInterceptors(new AuditableEntityInterceptor())
            .Options;
        var database = new HeldSaleTestDatabase(path, options);
        await using var context = database.Context();
        await context.Database.EnsureCreatedAsync();
        var category = new Category($"Held sale {Guid.NewGuid():N}", 1, Now);
        var user = new User($"cashier.{Guid.NewGuid():N}", "hash", "Thu ngân A", Role.Cashier, Now);
        var other = new User($"cashier.{Guid.NewGuid():N}", "hash", "Thu ngân B", Role.Cashier, Now);
        context.AddRange(category, user, other);
        await context.SaveChangesAsync();
        var product = new Product(category.Id, "CF-01", "Cà phê sữa", "Ly",
            12_000, 35_000, 20, 2, true, false, Now, "893000000001");
        context.Products.Add(product);
        await context.SaveChangesAsync();
        database.UserId = user.Id;
        database.OtherUserId = other.Id;
        database.ProductId = product.Id;
        return database;
    }

    public PosDbContext Context() => new(_options);

    public HeldSaleService HeldSaleService(PosDbContext context, int? userId = null) =>
        new(
            new HeldSaleRepository(context),
            new ProductRepository(context),
            new EfUnitOfWork(context),
            CurrentUser(userId ?? UserId),
            new FixedClock(),
            new HeldSaleRequestCanonicalizer());

    public CheckoutService CheckoutService(
        PosDbContext context,
        IReceiptSnapshotSerializer? serializer = null,
        IOrderReceiptSnapshotRepository? snapshots = null,
        int? userId = null) =>
        new(
            new ProductRepository(context),
            new OrderRepository(context),
            snapshots ?? new OrderReceiptSnapshotRepository(context),
            new InventoryMovementRepository(context),
            new EfUnitOfWork(context),
            new OrderCodeGenerator(),
            CurrentUser(userId ?? UserId),
            new FixedClock(),
            NullLogger<CheckoutService>.Instance,
            serializer ?? new ReceiptSnapshotJsonSerializer(),
            new StoreProvider(),
            new CheckoutRequestJournalRepository(context),
            new CheckoutRequestCanonicalizer(),
            new HeldSaleRepository(context));

    public CreateHeldSaleRequest HoldRequest(
        Guid requestId,
        int quantity = 2,
        string? label = "Khách áo xanh",
        string? notes = "Giao sau") =>
        new(requestId, label, notes,
            [new CreateHeldSaleLineRequest(ProductId, quantity, "Ít đá")]);

    public CheckoutRequest CheckoutRequest(
        Guid requestId,
        int? heldSaleId,
        int quantity = 2,
        long cash = 100_000) =>
        new([new CheckoutLineRequest(ProductId, quantity, notes: "Ít đá")],
            PaymentMethod.Cash, cash, notes: "Giao sau",
            clientRequestId: requestId, heldSaleId: heldSaleId);

    public async Task<int> CreateHeldSaleAsync(Guid? requestId = null)
    {
        await using var context = Context();
        var result = await HeldSaleService(context)
            .CreateHeldSaleAsync(HoldRequest(requestId ?? Guid.NewGuid()));
        if (result.IsFailure)
            throw new InvalidOperationException(result.Error.Message);
        return result.Value.Id;
    }

    public async Task AssertBusinessStateAsync(
        int heldSales,
        HeldSaleStatus heldStatus,
        int orders,
        int stock,
        int movements,
        int receipts,
        CheckoutRequestStatus? journalStatus = null)
    {
        await using var context = Context();
        Assert.Equal(heldSales, await context.HeldSales.CountAsync());
        Assert.Equal(heldStatus, await context.HeldSales.Select(x => x.Status).SingleAsync());
        Assert.Equal(orders, await context.Orders.CountAsync());
        Assert.Equal(orders, await context.OrderItems.CountAsync());
        Assert.Equal(stock, await context.Products.Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(movements, await context.InventoryMovements.CountAsync());
        Assert.Equal(receipts, await context.OrderReceiptSnapshots.CountAsync());
        if (journalStatus.HasValue)
            Assert.Equal(journalStatus, await context.CheckoutRequestJournals
                .Select(x => x.Status).SingleAsync());
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private static CurrentUserService CurrentUser(int userId)
    {
        var service = new CurrentUserService();
        service.SetCurrentUser(new AuthenticatedUserDto(
            userId, "cashier", $"Thu ngân #{userId}", Role.Cashier, Now));
        return service;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class StoreProvider : IReceiptStoreSnapshotProvider
    {
        public ReceiptStoreSnapshotDto GetCurrentSnapshot() =>
            new("POS Test", null, null, null, "Cảm ơn");
    }
}

internal sealed class HeldSaleThrowingSerializer : IReceiptSnapshotSerializer
{
    public string Serialize(ReceiptRequest snapshot) =>
        throw new InvalidOperationException("Synthetic receipt serialization failure.");

    public ReceiptRequest Deserialize(string json) =>
        throw new InvalidOperationException();
}

internal sealed class HeldSaleThrowingSnapshotRepository :
    IOrderReceiptSnapshotRepository
{
    public Task AddAsync(
        OrderReceiptSnapshot snapshot,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Synthetic receipt repository failure.");

    public Task<OrderReceiptSnapshot?> GetByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<OrderReceiptSnapshot?>(null);
}
