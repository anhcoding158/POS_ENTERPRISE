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
    public async Task Existing_sales_receipts_returns_and_checkout_journals_survive_held_sales_migration()
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
            var heldSaleMigrationIndex = Array.FindIndex(
                migrations,
                migration => migration.EndsWith(
                    "AddHeldSales",
                    StringComparison.Ordinal));
            Assert.True(heldSaleMigrationIndex > 0);
            await oldSchema.GetService<IMigrator>().MigrateAsync(
                migrations[heldSaleMigrationIndex - 1]);

            var category = new Category("Migration preservation", 1, Now);
            const int userId = 1;
            await InsertLegacyUserAsync(connection, userId, $"migration.{Guid.NewGuid():N}", "Thu ngân", Role.Cashier, Now);
            oldSchema.Add(category);
            await oldSchema.SaveChangesAsync();
            var product = new Product(category.Id, "MIG-01", "Sản phẩm cũ", "Ly",
                10_000, 30_000, 8, 1, true, false, Now);
            oldSchema.Products.Add(product);
            await oldSchema.SaveChangesAsync();
            var order = new Order("HD-MIGRATION", userId, Now);
            var item = order.AddItem(product.Id, product.Code, product.Name, product.UnitName,
                1, product.CostPrice, product.SalePrice, Now);
            order.PrepareForPayment(Now);
            order.MarkPaid(PaymentMethod.Cash, 50_000, Now);
            order.Complete(Now);
            oldSchema.Orders.Add(order);
            oldSchema.InventoryMovements.Add(new InventoryMovement(
                product.Id, InventoryMovementType.Sale, -1, 8, 7,
                "Bán hàng migration", Now, "ORDER", order.OrderCode, userId));
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
                order.Id, userId, Now.AddMinutes(1), "Khách trả",
                PaymentMethod.Cash, null, [returnItem]));
            var balance = new OrderReturnBalance(item.Id);
            balance.Register(1, item.NetAmount, item.Quantity, item.NetAmount);
            oldSchema.OrderReturnBalances.Add(balance);
            var preparedJournal = new CheckoutRequestJournal(
                Guid.NewGuid(), new string('A', 64), "{\"version\":1}",
                new string('B', 64), "{\"total\":30000,\"lines\":[],\"paymentMethod\":1}",
                userId, Now.AddMinutes(2));
            var completedJournal = new CheckoutRequestJournal(
                Guid.NewGuid(), new string('C', 64), "{\"version\":1}",
                new string('D', 64), "{\"total\":30000,\"lines\":[],\"paymentMethod\":1}",
                userId, Now.AddMinutes(3));
            completedJournal.Complete(order.Id, Now.AddMinutes(4));
            oldSchema.CheckoutRequestJournals.AddRange(preparedJournal, completedJournal);
            await oldSchema.SaveChangesAsync();
            productId = product.Id;
            orderId = order.Id;
            before = await SnapshotAsync(oldSchema, productId, orderId);
        }

        await using (var latest = new PosDbContext(options))
        {
            await latest.Database.MigrateAsync();
            Assert.Equal(before, await SnapshotAsync(latest, productId, orderId));
            Assert.Equal(2, await latest.CheckoutRequestJournals.CountAsync());
            Assert.Empty(await latest.HeldSales.ToArrayAsync());
            Assert.Empty(await latest.HeldSaleLines.ToArrayAsync());
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", (string?)await command.ExecuteScalarAsync());
        }

        await using (var down = new PosDbContext(options))
        {
            var migrations = down.Database.GetMigrations().ToArray();
            var heldSaleMigrationIndex = Array.FindIndex(
                migrations,
                migration => migration.EndsWith(
                    "AddHeldSales",
                    StringComparison.Ordinal));
            await down.GetService<IMigrator>().MigrateAsync(
                migrations[heldSaleMigrationIndex - 1]);
            Assert.Equal(before, await SnapshotAsync(down, productId, orderId));
            Assert.Equal(2, await down.CheckoutRequestJournals.CountAsync());
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='HeldSales';";
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
        var journals = await context.CheckoutRequestJournals.AsNoTracking()
            .OrderBy(x => x.Status)
            .Select(x => new
            {
                x.ClientRequestId,
                x.RequestFingerprint,
                x.CanonicalRequestJson,
                x.PreparedQuoteFingerprint,
                x.PreparedQuoteJson,
                x.Status,
                x.PreparedByUserId,
                x.OrderId,
                x.CompletedAtUtc
            })
            .ToArrayAsync();
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            product,
            order,
            item,
            receipt,
            movements = await context.InventoryMovements.CountAsync(),
            returns = await context.OrderReturns.CountAsync(),
            returnItems = await context.OrderReturnItems.CountAsync(),
            balances = await context.OrderReturnBalances.CountAsync(),
            journals
        });
    }

    private static async Task InsertLegacyUserAsync(
        SqliteConnection connection,
        int userId,
        string username,
        string fullName,
        Role role,
        DateTimeOffset now)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "Users" (
                "Id", "Username", "NormalizedUsername", "PasswordHash", "FullName",
                "Role", "IsActive", "FailedLoginAttempts", "LockedUntilUtc", "LastLoginAtUtc",
                "ConcurrencyToken", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ($id, $username, $normalized, 'hash', $fullName,
                $role, 1, 0, NULL, NULL, $token, $created, $created);
            """;
        command.Parameters.AddWithValue("$id", userId);
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$normalized", username.ToUpperInvariant());
        command.Parameters.AddWithValue("$fullName", fullName);
        command.Parameters.AddWithValue("$role", (int)role);
        command.Parameters.AddWithValue("$token", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$created", now.ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync();
    }
}
