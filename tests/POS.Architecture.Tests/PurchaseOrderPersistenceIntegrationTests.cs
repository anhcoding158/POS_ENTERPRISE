using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions.Authentication;
using POS.Application.Abstractions.DateTime;
using POS.Application.Abstractions.Persistence;
using POS.Application.Abstractions.Purchasing;
using POS.Application.Services;
using POS.Application.DTOs.Purchasing;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Authentication;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Purchasing;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PurchaseOrderPersistenceIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Purchase_order_operations_do_not_change_stock_cost_or_inventory_movements()
    {
        await using var database = await TestDatabase.CreateLatestAsync();
        var service = database.CreateService();
        var productBefore = await database.Context.Products.AsNoTracking().SingleAsync();
        var movementCountBefore = await database.Context.InventoryMovements.CountAsync();

        var created = await service.CreateDraftAsync(
            new CreatePurchaseOrderRequest(
                database.SupplierId,
                new DateOnly(2026, 9, 4),
                new DateOnly(2026, 9, 8),
                "draft note",
                [new PurchaseOrderLineRequest(database.ProductId, 3, 12500, 1)]));

        Assert.True(created.IsSuccess, created.AppError.Message);
        Assert.Equal(PurchaseOrderStatus.Draft, created.Value.Status);
        Assert.Equal(0, created.Value.Lines.Single().ReceivedQuantity);

        var ordered = await service.MarkOrderedAsync(
            new MarkPurchaseOrderOrderedRequest(
                created.Value.Id,
                created.Value.UpdatedAtUtc));

        Assert.True(ordered.IsSuccess, ordered.AppError.Message);
        Assert.Equal(PurchaseOrderStatus.Ordered, ordered.Value.Status);

        var amended = await service.AmendOrderedAsync(
            new AmendOrderedPurchaseOrderRequest(
                created.Value.Id,
                new DateOnly(2026, 9, 10),
                "amended note",
                [new PurchaseOrderLineRequest(database.ProductId, 4, 13000, 1)],
                ordered.Value.UpdatedAtUtc));

        Assert.True(amended.IsSuccess, amended.AppError.Message);
        Assert.Equal(4, amended.Value.Lines.Single().OrderedQuantity);

        var cancelled = await service.CancelAsync(
            new CancelPurchaseOrderRequest(
                created.Value.Id,
                "supplier unavailable",
                amended.Value.UpdatedAtUtc));

        Assert.True(cancelled.IsSuccess, cancelled.AppError.Message);
        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelled.Value.Status);

        var productAfter = await database.Context.Products.AsNoTracking().SingleAsync();
        Assert.Equal(productBefore.StockQuantity, productAfter.StockQuantity);
        Assert.Equal(productBefore.CostPrice, productAfter.CostPrice);
        Assert.Equal(movementCountBefore, await database.Context.InventoryMovements.CountAsync());
        Assert.Contains(
            await database.Context.SecurityAuditEvents.Select(audit => audit.Action).ToArrayAsync(),
            action => action == SecurityAuditAction.PurchaseOrderCreated);
        Assert.Contains(
            await database.Context.SecurityAuditEvents.Select(audit => audit.Action).ToArrayAsync(),
            action => action == SecurityAuditAction.PurchaseOrderOrdered);
        Assert.Contains(
            await database.Context.SecurityAuditEvents.Select(audit => audit.Action).ToArrayAsync(),
            action => action == SecurityAuditAction.PurchaseOrderUpdated);
        Assert.Contains(
            await database.Context.SecurityAuditEvents.Select(audit => audit.Action).ToArrayAsync(),
            action => action == SecurityAuditAction.PurchaseOrderCancelled);
    }

    [Fact]
    public async Task Mark_ordered_refreshes_live_master_data_once_and_ordered_snapshot_then_stays_stable()
    {
        await using var database = await TestDatabase.CreateLatestAsync();
        var service = database.CreateService();
        var created = await service.CreateDraftAsync(
            new CreatePurchaseOrderRequest(
                database.SupplierId,
                new DateOnly(2026, 9, 4),
                null,
                null,
                [new PurchaseOrderLineRequest(database.ProductId, 1, 1000, 1)]));
        Assert.True(created.IsSuccess, created.AppError.Message);

        var supplier = await database.Context.Suppliers.SingleAsync();
        supplier.UpdateProfile("SUP-01", "Tên Supplier mới", "TAX-NEW", null, null, null, null, null, Now.AddMinutes(1));
        var product = await database.Context.Products.SingleAsync();
        product.UpdateDetails(product.CategoryId, "P-01", null, "Tên Product mới", null, "Hộp", null, Now.AddMinutes(1));
        await database.Context.SaveChangesAsync();

        var ordered = await service.MarkOrderedAsync(
            new MarkPurchaseOrderOrderedRequest(created.Value.Id, created.Value.UpdatedAtUtc));

        Assert.True(ordered.IsSuccess, ordered.AppError.Message);
        Assert.Equal("Tên Supplier mới", ordered.Value.SupplierName);
        Assert.Equal("TAX-NEW", ordered.Value.SupplierTaxCode);
        Assert.Equal("Tên Product mới", ordered.Value.Lines.Single().ProductName);

        supplier.UpdateProfile("SUP-01", "Tên Supplier sau đó", "TAX-LATER", null, null, null, null, null, Now.AddMinutes(2));
        product.UpdateDetails(product.CategoryId, "P-01", null, "Tên Product sau đó", null, "Cái", null, Now.AddMinutes(2));
        await database.Context.SaveChangesAsync();

        var persisted = await database.Context.PurchaseOrders
            .Include(order => order.Lines)
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal("Tên Supplier mới", persisted.SupplierName);
        Assert.Equal("Tên Product mới", persisted.Lines.Single().ProductName);
        Assert.Equal("Hộp", persisted.Lines.Single().UnitName);
    }

    [Fact]
    public async Task Inactive_supplier_is_rejected_for_new_draft()
    {
        await using var database = await TestDatabase.CreateLatestAsync();
        var supplier = await database.Context.Suppliers.SingleAsync();
        supplier.Deactivate(Now.AddMinutes(1));
        await database.Context.SaveChangesAsync();

        var result = await database.CreateService().CreateDraftAsync(
            new CreatePurchaseOrderRequest(
                database.SupplierId,
                new DateOnly(2026, 9, 4),
                null,
                null,
                [new PurchaseOrderLineRequest(database.ProductId, 1, 1000, 1)]));

        Assert.True(result.IsFailure);
        Assert.Equal("PURCHASE_ORDER.SUPPLIER_INACTIVE", result.AppError.Code);
        Assert.Empty(await database.Context.PurchaseOrders.ToArrayAsync());
    }

    [Fact]
    public async Task Audit_failure_rolls_back_draft_and_leaves_inventory_unchanged()
    {
        await using var database = await TestDatabase.CreateLatestAsync();
        var productBefore = await database.Context.Products.AsNoTracking().SingleAsync();
        var movementCountBefore = await database.Context.InventoryMovements.CountAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            database.CreateService(new TestDatabase.ThrowingAuditRepository()).CreateDraftAsync(
                new CreatePurchaseOrderRequest(
                    database.SupplierId,
                    new DateOnly(2026, 9, 4),
                    null,
                    null,
                    [new PurchaseOrderLineRequest(database.ProductId, 2, 1000, 1)])));

        Assert.Equal(0, await database.Context.PurchaseOrders.AsNoTracking().CountAsync());
        Assert.Equal(0, await database.Context.PurchaseOrderLines.AsNoTracking().CountAsync());
        var productAfter = await database.Context.Products.AsNoTracking().SingleAsync();
        Assert.Equal(productBefore.StockQuantity, productAfter.StockQuantity);
        Assert.Equal(productBefore.CostPrice, productAfter.CostPrice);
        Assert.Equal(movementCountBefore, await database.Context.InventoryMovements.CountAsync());
    }

    [Fact]
    public async Task Exhausted_duplicate_number_retries_leave_no_half_order()
    {
        await using var database = await TestDatabase.CreateLatestAsync();
        var first = await database.CreateService().CreateDraftAsync(
            new CreatePurchaseOrderRequest(
                database.SupplierId,
                new DateOnly(2026, 9, 4),
                null,
                null,
                [new PurchaseOrderLineRequest(database.ProductId, 1, 1000, 1)]));
        Assert.True(first.IsSuccess, first.AppError.Message);

        var before = await database.Context.PurchaseOrders.AsNoTracking().CountAsync();
        var result = await database.CreateService(
            numberGenerator: new TestDatabase.FixedNumberGenerator(first.Value.OrderNumber)).CreateDraftAsync(
                new CreatePurchaseOrderRequest(
                    database.SupplierId,
                    new DateOnly(2026, 9, 4),
                    null,
                    null,
                    [new PurchaseOrderLineRequest(database.ProductId, 2, 1000, 1)]));

        Assert.True(result.IsFailure);
        Assert.Equal("PURCHASE_ORDER.NUMBER_ALREADY_EXISTS", result.AppError.Code);
        Assert.Equal(before, await database.Context.PurchaseOrders.AsNoTracking().CountAsync());
        Assert.Equal(before, await database.Context.PurchaseOrderLines.AsNoTracking().CountAsync());
    }

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, PosDbContext context, int supplierId, int productId)
        {
            Connection = connection;
            Context = context;
            SupplierId = supplierId;
            ProductId = productId;
        }

        public SqliteConnection Connection { get; }
        public PosDbContext Context { get; }
        public int SupplierId { get; }
        public int ProductId { get; }

        public static async Task<TestDatabase> CreateLatestAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var context = new PosDbContext(
                new DbContextOptionsBuilder<PosDbContext>()
                    .UseSqlite(connection)
                    .AddInterceptors(new POS.Infrastructure.Persistence.AuditableEntityInterceptor())
                    .Options);
            await context.Database.MigrateAsync();

            var category = new Category("Danh mục", 1, Now);
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            var supplier = new Supplier("SUP-01", "Tên Supplier", "TAX-01", null, null, null, null, null, Now);
            var product = new Product(category.Id, "P-01", "Tên Product", "Hộp", 3000, 5000, 10, 0, true, false, Now);
            var user = new User("admin.r62", "hash", "Quản trị", Role.Administrator, Now);
            context.Suppliers.Add(supplier);
            context.Products.Add(product);
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return new(connection, context, supplier.Id, product.Id);
        }

        public PurchaseOrderService CreateService(
            ISecurityAuditRepository? auditRepository = null,
            IPurchaseOrderNumberGenerator? numberGenerator = null)
        {
            var currentUser = new CurrentUserService();
            var user = new POS.Application.DTOs.Authentication.AuthenticatedUserDto(
                1, "admin.r62", "Quản trị", Role.Administrator, Now);
            currentUser.SetCurrentUser(user);
            return new PurchaseOrderService(
                new PurchaseOrderRepository(Context),
                new SupplierRepository(Context),
                new ProductRepository(Context),
                auditRepository ?? new SecurityAuditRepository(Context),
                numberGenerator ?? new PurchaseOrderNumberGenerator(),
                new EfUnitOfWork(Context),
                new FixedClock(Now),
                currentUser);
        }

        internal sealed class ThrowingAuditRepository : ISecurityAuditRepository
        {
            public Task AddAsync(
                SecurityAuditEvent auditEvent,
                CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("controlled audit failure");
        }

        internal sealed class FixedNumberGenerator(string value) : IPurchaseOrderNumberGenerator
        {
            public string Generate(DateTimeOffset utcNow) => value;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
