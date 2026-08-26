using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class PaymentIntentMigrationTests
{
    private const string InitialPaymentIntentMigration =
        "20260729090531_AddVietQrPaymentIntents";

    private const string CompletedPaymentIntentSchemaMigration =
        "20260730025400_CompleteVietQrPaymentIntentSchema";

    [Fact]
    public Task Previously_applied_payment_intent_migration_is_upgraded_forward() =>
        AssertOldAppliedSchemaUpgradeAsync();

    [Fact]
    public Task Missing_CheckoutRequestJson_is_added_by_a_new_forward_migration() =>
        AssertOldAppliedSchemaUpgradeAsync();

    [Fact]
    public async Task Applied_migration_is_not_assumed_to_rerun_after_its_file_changes()
    {
        await using var fixture = await MigrationFixture.CreateAtAsync(
            InitialPaymentIntentMigration);

        Assert.DoesNotContain(
            "CheckoutRequestJson",
            await GetColumnsAsync(fixture.Connection));

        Assert.Contains(
            InitialPaymentIntentMigration,
            await fixture.Context.Database.GetAppliedMigrationsAsync());
        Assert.Contains(
            CompletedPaymentIntentSchemaMigration,
            await fixture.Context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Existing_payment_intent_rows_survive_schema_completion()
    {
        await using var fixture = await MigrationFixture.CreateAtAsync(
            InitialPaymentIntentMigration);
        await SeedLegacyPaymentIntentAsync(fixture);

        await fixture.Context.Database.MigrateAsync();

        Assert.Equal(1, await fixture.Context.PaymentIntents.CountAsync());
        Assert.Equal(
            "{\"schema\":\"legacy-payment-intent-v1\"}",
            await fixture.Context.PaymentIntents
                .Select(value => value.CheckoutRequestJson)
                .SingleAsync());
    }

    [Fact]
    public async Task Fresh_database_reaches_the_same_final_schema()
    {
        await using var fresh = await MigrationFixture.CreateLatestAsync();
        await using var upgraded = await MigrationFixture.CreateAtAsync(
            InitialPaymentIntentMigration);
        await upgraded.Context.Database.MigrateAsync();

        Assert.Equal(
            await GetColumnsAsync(fresh.Connection),
            await GetColumnsAsync(upgraded.Connection));
    }

    [Fact]
    public async Task Already_latest_database_is_not_modified_twice()
    {
        await using var fixture = await MigrationFixture.CreateLatestAsync();
        var before = await GetCreateSqlAsync(fixture.Connection);

        await fixture.Context.Database.MigrateAsync();

        Assert.Equal(before, await GetCreateSqlAsync(fixture.Connection));
        Assert.Empty(await fixture.Context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Payment_intent_query_succeeds_after_upgrade()
    {
        await using var fixture = await MigrationFixture.CreateAtAsync(
            InitialPaymentIntentMigration);
        await SeedLegacyPaymentIntentAsync(fixture);
        await fixture.Context.Database.MigrateAsync();

        var intent = await fixture.Context.PaymentIntents.AsNoTracking().SingleAsync();

        Assert.Equal(PaymentIntentStatus.Created, intent.Status);
        Assert.NotEmpty(intent.CheckoutRequestJson);
    }

    [Fact]
    public async Task Database_integrity_and_foreign_keys_pass_after_upgrade()
    {
        await using var fixture = await MigrationFixture.CreateAtAsync(
            InitialPaymentIntentMigration);
        await SeedLegacyPaymentIntentAsync(fixture);
        await fixture.Context.Database.MigrateAsync();

        await AssertIntegrity(fixture.Connection);
    }

    [Fact]
    public async Task Existing_sales_data_survives_payment_intent_up_and_down()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditableEntityInterceptor())
            .Options;
        var now = new DateTimeOffset(2026, 7, 29, 2, 0, 0, TimeSpan.Zero);
        string before;
        string previousMigration;

        await using (var old = new PosDbContext(options))
        {
            var migrations = old.Database.GetMigrations().ToArray();
            var index = Array.FindIndex(migrations,
                value => value.EndsWith("AddVietQrPaymentIntents", StringComparison.Ordinal));
            Assert.True(index > 0);
            previousMigration = migrations[index - 1];
            await old.GetService<IMigrator>().MigrateAsync(previousMigration);
            var category = new Category("Payment migration", 1, now);
            const int userId = 1;
            await InsertLegacyUserAsync(old.Database.GetDbConnection(), userId, $"paymig.{Guid.NewGuid():N}", "Cashier", Role.Cashier, now);
            old.Add(category);
            await old.SaveChangesAsync();
            var product = new Product(category.Id, "PAY-MIG", "Product", "Unit",
                10_000, 30_000, 9, 1, true, false, now);
            old.Products.Add(product);
            await old.SaveChangesAsync();
            var order = new Order("HD-PAY-MIG", userId, now);
            order.AddItem(product.Id, product.Code, product.Name, product.UnitName,
                1, product.CostPrice, product.SalePrice, now);
            order.PrepareForPayment(now);
            order.MarkPaid(PaymentMethod.Cash, 50_000, now);
            order.Complete(now);
            old.Orders.Add(order);
            product.DecreaseStock(1, now);
            await old.SaveChangesAsync();
            old.OrderReceiptSnapshots.Add(new OrderReceiptSnapshot(
                order.Id, 1, "{\"immutable\":true}", now));
            await old.SaveChangesAsync();
            before = await Snapshot(old);
        }

        await using (var latest = new PosDbContext(options))
        {
            await latest.Database.MigrateAsync();
            Assert.Equal(before, await Snapshot(latest));
            Assert.Empty(await latest.PaymentIntents.ToArrayAsync());
            await AssertIntegrity(connection);
        }

        await using (var down = new PosDbContext(options))
        {
            await down.GetService<IMigrator>().MigrateAsync(previousMigration);
            Assert.Equal(before, await Snapshot(down));
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PaymentIntents';";
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
            await AssertIntegrity(connection);
        }
    }

    private static async Task<string> Snapshot(PosDbContext context) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            products = await context.Products.AsNoTracking()
                .Select(x => new { x.Id, x.StockQuantity, x.SalePrice }).ToArrayAsync(),
            orders = await context.Orders.AsNoTracking()
                .Select(x => new { x.Id, x.OrderCode, x.TotalAmount }).ToArrayAsync(),
            items = await context.OrderItems.CountAsync(),
            receipts = await context.OrderReceiptSnapshots.AsNoTracking()
                .Select(x => x.PayloadJson).ToArrayAsync()
        });

    private static async Task AssertIntegrity(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", await command.ExecuteScalarAsync());
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync());
    }

    private static async Task AssertOldAppliedSchemaUpgradeAsync()
    {
        await using var fixture = await MigrationFixture.CreateAtAsync(
            InitialPaymentIntentMigration);
        Assert.DoesNotContain(
            "CheckoutRequestJson",
            await GetColumnsAsync(fixture.Connection));

        await fixture.Context.Database.MigrateAsync();

        var columns = await GetColumnsAsync(fixture.Connection);
        Assert.Contains("CheckoutRequestJson", columns);
        Assert.Contains("ExpiredAtUtc", columns);
        Assert.Contains("ExpirationReason", columns);
        Assert.Contains(
            CompletedPaymentIntentSchemaMigration,
            await fixture.Context.Database.GetAppliedMigrationsAsync());
    }

    private static async Task SeedLegacyPaymentIntentAsync(
        MigrationFixture fixture)
    {
        var now = new DateTimeOffset(2026, 7, 29, 3, 0, 0, TimeSpan.Zero);
        const int userId = 1;
        await InsertLegacyUserAsync(fixture.Connection, userId, $"legacy.pay.{Guid.NewGuid():N}", "Legacy payment", Role.Cashier, now);

        await using var command = fixture.Connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "PaymentIntents" (
                "ClientRequestId", "DisplayCode", "Provider", "Status",
                "Amount", "Currency", "TransferContent", "PayloadText",
                "PayloadHash", "BankCodeSnapshot", "AccountNumberSnapshot",
                "AccountNameSnapshot", "QuoteFingerprint", "HeldSaleId",
                "CreatedByUserId", "ConfirmedByUserId", "CompletedOrderId",
                "PresentedAtUtc", "ConfirmedAtUtc", "CompletedAtUtc",
                "CancelledAtUtc", "ExpiresAtUtc", "ConcurrencyToken",
                "CreatedAtUtc", "UpdatedAtUtc")
            VALUES (
                $requestId, 'VQLEGACY000001', 1, 1,
                125000, 'VND', 'VQLEGACY000001', 'legacy-payload',
                $hash, '970436', '0123456789',
                'LEGACY SHOP', $hash, NULL,
                $userId, NULL, NULL,
                NULL, NULL, NULL,
                NULL, $expires, $token,
                $created, $created);
            """;
        command.Parameters.AddWithValue("$requestId", Guid.NewGuid());
        command.Parameters.AddWithValue("$hash", new string('A', 64));
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$expires", now.AddMinutes(15).ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$token", Guid.NewGuid());
        command.Parameters.AddWithValue("$created", now.ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertLegacyUserAsync(
        System.Data.Common.DbConnection connection,
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
        AddParameter(command, "$id", userId);
        AddParameter(command, "$username", username);
        AddParameter(command, "$normalized", username.ToUpperInvariant());
        AddParameter(command, "$fullName", fullName);
        AddParameter(command, "$role", (int)role);
        AddParameter(command, "$token", Guid.NewGuid().ToString());
        AddParameter(command, "$created", now.ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task<string[]> GetColumnsAsync(
        SqliteConnection connection)
    {
        var columns = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('PaymentIntents');";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));
        return columns.ToArray();
    }

    private static async Task<string> GetCreateSqlAsync(
        SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='PaymentIntents';";
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private sealed class MigrationFixture : IAsyncDisposable
    {
        private MigrationFixture(
            SqliteConnection connection,
            PosDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public PosDbContext Context { get; }

        public static async Task<MigrationFixture> CreateAtAsync(
            string migration)
        {
            var connection = new SqliteConnection(
                "Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PosDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(new AuditableEntityInterceptor())
                .Options;
            var context = new PosDbContext(options);
            await context.GetService<IMigrator>().MigrateAsync(migration);
            return new MigrationFixture(connection, context);
        }

        public static async Task<MigrationFixture> CreateLatestAsync()
        {
            var fixture = await CreateAtAsync(
                CompletedPaymentIntentSchemaMigration);
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
