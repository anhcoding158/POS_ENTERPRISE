using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Architecture.Tests;

/// <summary>
/// Integration test cho migration backfill tồn đầu kỳ.
///
/// Test sử dụng SQLite thật trong bộ nhớ và migration thật
/// từ POS.Infrastructure.
///
/// Các mục tiêu:
/// - chỉ backfill Product hợp lệ;
/// - không tạo movement rỗng;
/// - không tạo lịch sử giả cho Product đã có movement;
/// - Down chỉ xóa dữ liệu do migration tạo;
/// - Up lại không làm sai tồn kho.
/// </summary>
public sealed class InventoryOpeningBalanceMigrationTests
{
    private const int CategoryId = 901;

    private const int TrackedNonZeroProductId = 1001;
    private const int TrackedZeroProductId = 1002;
    private const int UntrackedProductId = 1003;
    private const int ProductWithHistoryId = 1004;

    private const string BackfillReferenceType =
        "SYSTEM_MIGRATION";

    private const string BackfillReferenceId =
        "6C3C_OPENING_BALANCE_BACKFILL_V1";

    private static readonly DateTimeOffset
        CreatedAtUtc =
            new(
                2026,
                7,
                19,
                13,
                24,
                38,
                TimeSpan.Zero);

    [Fact]
    public async Task
        Backfill_migration_must_preserve_inventory_audit_rules()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:;" +
                "Foreign Keys=True");

        await connection.OpenAsync();

        var options =
            new DbContextOptionsBuilder<
                    PosDbContext>()
                .UseSqlite(connection)
                .EnableDetailedErrors()
                .Options;

        await using var context =
            new PosDbContext(options);

        var migrations =
            context.Database
                .GetMigrations()
                .ToArray();

        var backfillMigration =
            migrations.Single(
                migration =>
                    migration.EndsWith(
                        "_BackfillInventoryOpeningBalances",
                        StringComparison.Ordinal));

        var backfillIndex =
            Array.IndexOf(
                migrations,
                backfillMigration);

        Assert.True(
            backfillIndex > 0,
            "Migration backfill phải có một migration đứng trước.");

        var previousMigration =
            migrations[
                backfillIndex - 1];

        var migrator =
            context.GetService<
                IMigrator>();

        /*
         * Chỉ migrate tới trạng thái ngay trước Backfill.
         *
         * Bảng Products và InventoryMovements đã tồn tại,
         * nhưng dữ liệu backfill chưa được tạo.
         */
        await migrator.MigrateAsync(
            previousMigration);

        await SeedPreBackfillDataAsync(
            context);

        await AssertPreBackfillStateAsync(
            context);

        /*
         * Chạy migration Up thật.
         */
        await migrator.MigrateAsync(
            backfillMigration);

        await AssertBackfillStateAsync(
            context);

        /*
         * Chạy migration Down thật.
         *
         * Chỉ OpeningBalance mang reference của migration
         * được phép bị xóa.
         */
        await migrator.MigrateAsync(
            previousMigration);

        await AssertDownStateAsync(
            context);

        /*
         * Chạy Up lần nữa để kiểm tra migration có thể
         * áp dụng lại sau rollback.
         */
        await migrator.MigrateAsync(
            backfillMigration);

        await AssertBackfillStateAsync(
            context);
    }

    private static async Task
        SeedPreBackfillDataAsync(
            PosDbContext context)
    {
        var createdAtUnixMilliseconds =
            CreatedAtUtc
                .ToUnixTimeMilliseconds();

        /*
         * Category được chèn bằng SQL vì test đang tập trung
         * vào migration, không phụ thuộc constructor Domain.
         */
        await context.Database
            .ExecuteSqlInterpolatedAsync(
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
                    {CategoryId},
                    {Guid.NewGuid().ToString("D")},
                    {createdAtUnixMilliseconds},
                    {createdAtUnixMilliseconds},
                    {"Danh mục kiểm thử migration"},
                    NULL,
                    {1},
                    {true}
                );
                """);

        /*
         * 1. Product theo dõi kho, tồn khác 0, chưa có history.
         *
         * Đây là Product duy nhất phải được backfill.
         */
        await InsertProductAsync(
            context,
            productId:
                TrackedNonZeroProductId,
            code:
                "MIGRATION-TRACKED-10",
            stockQuantity:
                10,
            trackInventory:
                true);

        /*
         * 2. Product theo dõi kho nhưng tồn bằng 0.
         *
         * Không được tạo movement delta 0.
         */
        await InsertProductAsync(
            context,
            productId:
                TrackedZeroProductId,
            code:
                "MIGRATION-TRACKED-0",
            stockQuantity:
                0,
            trackInventory:
                true);

        /*
         * 3. Product không theo dõi kho.
         *
         * Dữ liệu cũ có thể vẫn còn StockQuantity,
         * nhưng migration không được hợp thức hóa số tồn này.
         */
        await InsertProductAsync(
            context,
            productId:
                UntrackedProductId,
            code:
                "MIGRATION-NOT-TRACKED",
            stockQuantity:
                7,
            trackInventory:
                false);

        /*
         * 4. Product đã có một movement thật.
         *
         * Migration không được tạo thêm OpeningBalance từ
         * số tồn hiện tại vì sẽ làm sai lịch sử.
         */
        await InsertProductAsync(
            context,
            productId:
                ProductWithHistoryId,
            code:
                "MIGRATION-HAS-HISTORY",
            stockQuantity:
                5,
            trackInventory:
                true);

        await context.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "InventoryMovements"
                (
                    "ProductId",
                    "MovementType",
                    "QuantityDelta",
                    "QuantityBefore",
                    "QuantityAfter",
                    "Reason",
                    "ReferenceType",
                    "ReferenceId",
                    "PerformedByUserId",
                    "OccurredAtUtc"
                )
                VALUES
                (
                    {ProductWithHistoryId},
                    {(int)InventoryMovementType.StockIn},
                    {5},
                    {0},
                    {5},
                    {"Movement thật đã tồn tại trước migration."},
                    NULL,
                    NULL,
                    NULL,
                    {createdAtUnixMilliseconds}
                );
                """);
    }

    private static async Task
        InsertProductAsync(
            PosDbContext context,
            int productId,
            string code,
            int stockQuantity,
            bool trackInventory)
    {
        var createdAtUnixMilliseconds =
            CreatedAtUtc
                .ToUnixTimeMilliseconds();

        var minimumStock =
            trackInventory
                ? 1
                : 0;

        await context.Database
            .ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "Products"
                (
                    "Id",
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
                    {productId},
                    {false},
                    NULL,
                    {CategoryId},
                    {code},
                    {Guid.NewGuid().ToString("D")},
                    {10_000L},
                    {createdAtUnixMilliseconds},
                    NULL,
                    NULL,
                    {true},
                    {minimumStock},
                    {$"Sản phẩm {code}"},
                    {15_000L},
                    {stockQuantity},
                    {trackInventory},
                    {"Cái"},
                    {createdAtUnixMilliseconds}
                );
                """);
    }

    private static async Task
        AssertPreBackfillStateAsync(
            PosDbContext context)
    {
        context.ChangeTracker.Clear();

        var movementCount =
            await context
                .InventoryMovements
                .AsNoTracking()
                .CountAsync();

        /*
         * Chỉ có movement thật của ProductWithHistory.
         */
        Assert.Equal(
            1,
            movementCount);

        var openingBalanceCount =
            await context
                .InventoryMovements
                .AsNoTracking()
                .CountAsync(
                    movement =>
                        movement.MovementType ==
                        InventoryMovementType
                            .OpeningBalance);

        Assert.Equal(
            0,
            openingBalanceCount);
    }

    private static async Task
        AssertBackfillStateAsync(
            PosDbContext context)
    {
        context.ChangeTracker.Clear();

        var products =
            await context
                .Products
                .AsNoTracking()
                .OrderBy(
                    product =>
                        product.Id)
                .ToArrayAsync();

        /*
         * Migration chỉ thêm audit history.
         * Tuyệt đối không được sửa StockQuantity của Product.
         */
        Assert.Equal(
            4,
            products.Length);

        Assert.Equal(
            10,
            products.Single(
                    product =>
                        product.Id ==
                        TrackedNonZeroProductId)
                .StockQuantity);

        Assert.Equal(
            0,
            products.Single(
                    product =>
                        product.Id ==
                        TrackedZeroProductId)
                .StockQuantity);

        Assert.Equal(
            7,
            products.Single(
                    product =>
                        product.Id ==
                        UntrackedProductId)
                .StockQuantity);

        Assert.Equal(
            5,
            products.Single(
                    product =>
                        product.Id ==
                        ProductWithHistoryId)
                .StockQuantity);

        var movements =
            await context
                .InventoryMovements
                .AsNoTracking()
                .OrderBy(
                    movement =>
                        movement.ProductId)
                .ToArrayAsync();

        /*
         * Một movement thật + một movement backfill.
         */
        Assert.Equal(
            2,
            movements.Length);

        var backfilledMovements =
            movements
                .Where(
                    movement =>
                        movement.ProductId ==
                        TrackedNonZeroProductId)
                .ToArray();

        var backfillMovement =
            Assert.Single(
                backfilledMovements);

        Assert.Equal(
            InventoryMovementType
                .OpeningBalance,
            backfillMovement.MovementType);

        Assert.Equal(
            0,
            backfillMovement.QuantityBefore);

        Assert.Equal(
            10,
            backfillMovement.QuantityDelta);

        Assert.Equal(
            10,
            backfillMovement.QuantityAfter);

        Assert.Equal(
            BackfillReferenceType,
            backfillMovement.ReferenceType);

        Assert.Equal(
            BackfillReferenceId,
            backfillMovement.ReferenceId);

        Assert.Equal(
            CreatedAtUtc,
            backfillMovement.OccurredAtUtc);

        /*
         * Tồn bằng 0 không được tạo history rỗng.
         */
        Assert.DoesNotContain(
            movements,
            movement =>
                movement.ProductId ==
                TrackedZeroProductId);

        /*
         * Product không theo dõi kho không được backfill.
         */
        Assert.DoesNotContain(
            movements,
            movement =>
                movement.ProductId ==
                UntrackedProductId);

        /*
         * Product đã có movement thật vẫn chỉ có một movement.
         */
        var existingHistory =
            movements
                .Where(
                    movement =>
                        movement.ProductId ==
                        ProductWithHistoryId)
                .ToArray();

        var existingMovement =
            Assert.Single(
                existingHistory);

        Assert.Equal(
            InventoryMovementType.StockIn,
            existingMovement.MovementType);

        Assert.Null(
            existingMovement.ReferenceType);

        Assert.Null(
            existingMovement.ReferenceId);
    }

    private static async Task
        AssertDownStateAsync(
            PosDbContext context)
    {
        context.ChangeTracker.Clear();

        var movements =
            await context
                .InventoryMovements
                .AsNoTracking()
                .ToArrayAsync();

        /*
         * Movement backfill phải bị xóa.
         */
        Assert.DoesNotContain(
            movements,
            movement =>
                movement.ReferenceType ==
                    BackfillReferenceType &&
                movement.ReferenceId ==
                    BackfillReferenceId);

        /*
         * Movement thật phải còn nguyên.
         */
        var remainingMovement =
            Assert.Single(
                movements);

        Assert.Equal(
            ProductWithHistoryId,
            remainingMovement.ProductId);

        Assert.Equal(
            InventoryMovementType.StockIn,
            remainingMovement.MovementType);

        /*
         * Down migration không được sửa tồn Product.
         */
        var productStocks =
            await context
                .Products
                .AsNoTracking()
                .ToDictionaryAsync(
                    product =>
                        product.Id,
                    product =>
                        product.StockQuantity);

        Assert.Equal(
            10,
            productStocks[
                TrackedNonZeroProductId]);

        Assert.Equal(
            0,
            productStocks[
                TrackedZeroProductId]);

        Assert.Equal(
            7,
            productStocks[
                UntrackedProductId]);

        Assert.Equal(
            5,
            productStocks[
                ProductWithHistoryId]);
    }
}