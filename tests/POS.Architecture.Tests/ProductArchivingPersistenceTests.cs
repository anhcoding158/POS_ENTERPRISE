using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

public sealed class ProductArchivingPersistenceTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            2026,
            7,
            27,
            8,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task Migration_must_backfill_existing_products_as_not_archived()
    {
        await using var database =
            ProductArchivingTestDatabase.Create();
        await using var context =
            database.CreateContext();

        var migrator =
            context.GetService<IMigrator>();
        var migrations =
            context.Database
                .GetMigrations()
                .ToArray();
        var archivingMigration =
            FindArchivingMigration(migrations);
        var archivingIndex =
            Array.IndexOf(
                migrations,
                archivingMigration);
        var previousMigration =
            migrations[archivingIndex - 1];

        await migrator.MigrateAsync(previousMigration);
        await SeedLegacyProductAsync(context);
        await migrator.MigrateAsync(archivingMigration);

        await using var command =
            context.Database
                .GetDbConnection()
                .CreateCommand();

        command.CommandText =
            """
            SELECT
                "IsArchived",
                "ArchivedAtUtc",
                "ArchivedByUserId"
            FROM "Products"
            WHERE "Code" = 'LEGACY-ARCHIVE';
            """;

        await context.Database
            .OpenConnectionAsync();

        await using var reader =
            await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(0L, reader.GetInt64(0));
        Assert.True(reader.IsDBNull(1));
        Assert.True(reader.IsDBNull(2));
    }

    [Fact]
    public async Task Archived_product_must_round_trip_actor_and_utc_timestamp()
    {
        await using var database =
            ProductArchivingTestDatabase.Create();
        await using var context =
            database.CreateContext();

        await context.Database.MigrateAsync();

        var (user, product) =
            await SeedCurrentModelAsync(context);
        var localArchiveTime =
            new DateTimeOffset(
                2026,
                7,
                27,
                16,
                15,
                30,
                456,
                TimeSpan.FromHours(7));

        product.Archive(
            user.Id,
            localArchiveTime);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded =
            await context.Products
                .AsNoTracking()
                .SingleAsync(
                    item =>
                        item.Id == product.Id);

        Assert.True(reloaded.IsArchived);
        Assert.False(reloaded.IsActive);
        Assert.Equal(user.Id, reloaded.ArchivedByUserId);
        Assert.Equal(
            localArchiveTime.ToUniversalTime(),
            reloaded.ArchivedAtUtc);
        Assert.Equal(
            TimeSpan.Zero,
            reloaded.ArchivedAtUtc?.Offset);
    }

    [Fact]
    public async Task Archive_actor_foreign_key_must_restrict_user_delete()
    {
        await using var database =
            ProductArchivingTestDatabase.Create();
        await using var context =
            database.CreateContext();

        await context.Database.MigrateAsync();
        var (user, product) =
            await SeedCurrentModelAsync(context);

        product.Archive(
            user.Id,
            CreatedAtUtc.AddHours(1));
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        var userToDelete =
            await context.Users
                .SingleAsync(
                    item =>
                        item.Id == user.Id);

        context.Users.Remove(userToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        context.ChangeTracker.Clear();

        Assert.True(
            await context.Products
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.Id == product.Id));
    }

    [Fact]
    public async Task Archive_state_constraint_must_reject_inconsistent_rows()
    {
        await using var database =
            ProductArchivingTestDatabase.Create();
        await using var context =
            database.CreateContext();

        await context.Database.MigrateAsync();
        var (_, product) =
            await SeedCurrentModelAsync(context);

        await Assert.ThrowsAsync<SqliteException>(
            () =>
                context.Database.ExecuteSqlRawAsync(
                    """
                    UPDATE "Products"
                    SET
                        "IsArchived" = 1,
                        "ArchivedAtUtc" = NULL
                    WHERE "Id" = {0};
                    """,
                    product.Id));
    }

    [Fact]
    public async Task Archive_composite_index_must_exist()
    {
        await using var database =
            ProductArchivingTestDatabase.Create();
        await using var context =
            database.CreateContext();

        await context.Database.MigrateAsync();
        await context.Database.OpenConnectionAsync();

        var indexNames =
            new List<string>();

        await using (
            var listCommand =
                context.Database
                    .GetDbConnection()
                    .CreateCommand())
        {
            listCommand.CommandText =
                "PRAGMA index_list('Products');";

            await using var reader =
                await listCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                indexNames.Add(reader.GetString(1));
            }
        }

        var hasExpectedColumns = false;

        foreach (var indexName in indexNames)
        {
            await using var infoCommand =
                context.Database
                    .GetDbConnection()
                    .CreateCommand();

            infoCommand.CommandText =
                $"PRAGMA index_info('{indexName.Replace(
                    "'",
                    "''",
                    StringComparison.Ordinal)}');";

            var columns =
                new List<string>();

            await using var reader =
                await infoCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(2));
            }

            if (columns.SequenceEqual(
                    [
                        "IsArchived",
                        "IsActive",
                        "Name"
                    ]))
            {
                hasExpectedColumns = true;
                break;
            }
        }

        Assert.True(
            hasExpectedColumns,
            "Không tìm thấy composite index IsArchived, IsActive, Name.");
    }

    private static string FindArchivingMigration(
        string[] migrations)
    {
        var migration =
            migrations.Single(
                item =>
                    item.EndsWith(
                        "_AddProductArchiving",
                        StringComparison.Ordinal));
        var migrationIndex =
            Array.IndexOf(
                migrations,
                migration);

        Assert.True(
            migrationIndex > 0,
            "Migration archiving phải có migration đứng trước.");

        return migration;
    }

    private static async Task SeedLegacyProductAsync(
        PosDbContext context)
    {
        var timestamp =
            CreatedAtUtc.ToUnixTimeMilliseconds();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Categories"
            (
                "Id",
                "ConcurrencyToken",
                "CreatedAtUtc",
                "UpdatedAtUtc",
                "Name",
                "Description",
                "DisplayOrder",
                "IsActive"
            )
            VALUES
            (
                {1},
                {Guid.NewGuid().ToString("D")},
                {timestamp},
                {timestamp},
                {"Danh mục legacy"},
                NULL,
                {1},
                {true}
            );
            """);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "Products"
            (
                "AllowNegativeStock",
                "Barcode",
                "CategoryId",
                "Code",
                "ConcurrencyToken",
                "CostPrice",
                "CreatedAtUtc",
                "Description",
                "ImagePath",
                "IsActive",
                "MinimumStock",
                "Name",
                "SalePrice",
                "StockQuantity",
                "TrackInventory",
                "UnitName",
                "UpdatedAtUtc"
            )
            VALUES
            (
                {false},
                NULL,
                {1},
                {"LEGACY-ARCHIVE"},
                {Guid.NewGuid().ToString("D")},
                {10_000L},
                {timestamp},
                NULL,
                NULL,
                {true},
                {1},
                {"Sản phẩm legacy"},
                {15_000L},
                {5},
                {true},
                {"Cái"},
                {timestamp}
            );
            """);
    }

    private static async Task<(User User, Product Product)>
        SeedCurrentModelAsync(
            PosDbContext context)
    {
        var category =
            new Category(
                name: $"Danh mục {Guid.NewGuid():N}",
                displayOrder: 1,
                CreatedAtUtc);
        var user =
            new User(
                username: $"user-{Guid.NewGuid():N}",
                passwordHash: "test-password-hash",
                fullName: "Người lưu trữ",
                Role.Manager,
                CreatedAtUtc);

        context.AddRange(category, user);
        await context.SaveChangesAsync();

        var product =
            new Product(
                category.Id,
                code: $"ARCH-{Guid.NewGuid():N}",
                name: "Sản phẩm persistence",
                unitName: "Cái",
                costPrice: 10_000,
                salePrice: 15_000,
                stockQuantity: 5,
                minimumStock: 1,
                trackInventory: true,
                allowNegativeStock: false,
                CreatedAtUtc);

        context.Products.Add(product);
        await context.SaveChangesAsync();

        return (user, product);
    }

    private sealed class ProductArchivingTestDatabase :
        IAsyncDisposable
    {
        private readonly string _databasePath;
        private readonly DbContextOptions<PosDbContext> _options;

        private ProductArchivingTestDatabase(
            string databasePath)
        {
            _databasePath = databasePath;

            _options =
                new DbContextOptionsBuilder<PosDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};" +
                        "Foreign Keys=True;" +
                        "Pooling=False")
                    .AddInterceptors(
                        new AuditableEntityInterceptor())
                    .EnableDetailedErrors()
                    .Options;
        }

        public static ProductArchivingTestDatabase Create()
        {
            var databasePath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"pos-product-archiving-{Guid.NewGuid():N}.db");

            return new ProductArchivingTestDatabase(
                databasePath);
        }

        public PosDbContext CreateContext()
        {
            return new PosDbContext(_options);
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }

            return ValueTask.CompletedTask;
        }
    }
}
