using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class CheckoutMigrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Existing_sales_receipts_and_returns_survive_checkout_journal_migration()
    {
        await using var connection = new SqliteConnection(
            "Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditableEntityInterceptor())
            .Options;
        int productId;
        int orderId;
        string before;

        await using (var oldSchema = new PosDbContext(options))
        {
            var migrations = oldSchema.Database.GetMigrations().ToArray();
            Assert.EndsWith("AddCheckoutIdempotencyJournal", migrations[^1], StringComparison.Ordinal);
            await oldSchema.GetService<IMigrator>().MigrateAsync(migrations[^2]);

            var category = new Category("Migration preservation", 1, Now);
            var user = new User($"migration.{Guid.NewGuid():N}", "hash", "Thu ngân", Role.Cashier, Now);
            oldSchema.AddRange(category, user);
            await oldSchema.SaveChangesAsync();
            var product = new Product(category.Id, "MIG-01", "Sản phẩm cũ", "Ly",
                10_000, 30_000, 8, 1, true, false, Now);
            oldSchema.Products.Add(product);
            await oldSchema.SaveChangesAsync();
            var order = new Order("HD-MIGRATION", user.Id, Now);
            var item = order.AddItem(product.Id, product.Code, product.Name, product.UnitName,
                1, product.CostPrice, product.SalePrice, Now);
            order.PrepareForPayment(Now);
            order.MarkPaid(PaymentMethod.Cash, 50_000, Now);
            order.Complete(Now);
            oldSchema.Orders.Add(order);
            oldSchema.InventoryMovements.Add(new InventoryMovement(
                product.Id, InventoryMovementType.Sale, -1, 8, 7,
                "Bán hàng migration", Now, "ORDER", order.OrderCode, user.Id));
            product.DecreaseStock(1, Now);
            await oldSchema.SaveChangesAsync();
            oldSchema.OrderReceiptSnapshots.Add(new OrderReceiptSnapshot(
                order.Id, 1, "{\"snapshot\":\"immutable-old\"}", Now));
            var returnItem = new OrderReturnItem(
                item.Id, product.Id, item.ProductCode, item.ProductName, item.UnitName,
                1, 1, item.NetAmount);
            oldSchema.OrderReturns.Add(new OrderReturn(
                Guid.NewGuid(),
                "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
                order.Id, user.Id, Now.AddMinutes(1), "Khách trả",
                PaymentMethod.Cash, null, [returnItem]));
            var balance = new OrderReturnBalance(item.Id);
            balance.Register(1, item.NetAmount, item.Quantity, item.NetAmount);
            oldSchema.OrderReturnBalances.Add(balance);
            await oldSchema.SaveChangesAsync();
            productId = product.Id;
            orderId = order.Id;
            before = await SnapshotAsync(oldSchema, productId, orderId);
        }

        await using (var latest = new PosDbContext(options))
        {
            await latest.Database.MigrateAsync();
            Assert.Equal(before, await SnapshotAsync(latest, productId, orderId));
            Assert.Empty(await latest.CheckoutRequestJournals.ToArrayAsync());
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", (string?)await command.ExecuteScalarAsync());
        }

        await using (var down = new PosDbContext(options))
        {
            var migrations = down.Database.GetMigrations().ToArray();
            await down.GetService<IMigrator>().MigrateAsync(migrations[^2]);
            Assert.Equal(before, await SnapshotAsync(down, productId, orderId));
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='CheckoutRequestJournals';";
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        }
    }

    private static async Task<string> SnapshotAsync(
        PosDbContext context,
        int productId,
        int orderId)
    {
        var product = await context.Products.AsNoTracking()
            .Where(x => x.Id == productId)
            .Select(x => new { x.StockQuantity, x.IsArchived, x.SalePrice })
            .SingleAsync();
        var order = await context.Orders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => new { x.OrderCode, x.TotalAmount, x.CashReceived, x.ChangeAmount })
            .SingleAsync();
        var item = await context.OrderItems.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .Select(x => new { x.ProductCode, x.ProductName, x.UnitSalePrice, x.NetAmount })
            .SingleAsync();
        var receipt = await context.OrderReceiptSnapshots.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .Select(x => x.PayloadJson)
            .SingleAsync();
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            product,
            order,
            item,
            receipt,
            movements = await context.InventoryMovements.CountAsync(),
            returns = await context.OrderReturns.CountAsync(),
            returnItems = await context.OrderReturnItems.CountAsync(),
            balances = await context.OrderReturnBalances.CountAsync()
        });
    }
}
