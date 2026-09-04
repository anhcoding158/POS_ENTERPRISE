using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PurchaseOrderMigrationTests
{
    [Fact]
    public async Task Fresh_schema_contains_purchase_order_tables_and_no_goods_receipt_tables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = new PosDbContext(
            new DbContextOptionsBuilder<PosDbContext>().UseSqlite(connection).Options);

        await context.Database.MigrateAsync();
        var tables = await context.Database.SqlQueryRaw<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToArrayAsync();

        Assert.Contains("PurchaseOrders", tables);
        Assert.Contains("PurchaseOrderLines", tables);
        Assert.DoesNotContain(tables, name => name.Contains("GoodsReceipt", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(21, (int)SecurityAuditAction.PurchaseOrderCreated);
        Assert.Equal(24, (int)SecurityAuditAction.PurchaseOrderCancelled);
    }

    [Fact]
    public async Task Upgrade_from_supplier_master_migration_applies_purchase_order_foundation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = new PosDbContext(
            new DbContextOptionsBuilder<PosDbContext>().UseSqlite(connection).Options);

        await context.Database.MigrateAsync("20260903161131_AddSupplierMaster");
        Assert.Contains(
            "20260903161131_AddSupplierMaster",
            await context.Database.GetAppliedMigrationsAsync());

        await context.Database.MigrateAsync();

        Assert.Contains(
            "20260904150256_AddPurchaseOrdersFoundation",
            await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Contains(
            "PurchaseOrders",
            await context.Database.SqlQueryRaw<string>(
                    "SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
                .ToArrayAsync());
    }
}
