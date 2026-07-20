using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Bổ sung OpeningBalance cho các Product cũ được tạo
/// trước khi hệ thống InventoryMovement được triển khai.
///
/// Chỉ backfill Product thỏa mãn toàn bộ điều kiện:
///
/// - đang theo dõi tồn kho;
/// - tồn hiện tại khác 0;
/// - chưa có bất kỳ lịch sử kho nào.
///
/// Product đã có movement sẽ không bị can thiệp, vì lấy tồn hiện
/// tại làm tồn đầu kỳ sau khi đã phát sinh giao dịch sẽ tạo lịch
/// sử sai lệch.
/// </summary>
public partial class BackfillInventoryOpeningBalances :
    Migration
{
    private const int OpeningBalanceMovementType = 7;

    private const string BackfillReferenceType =
        "SYSTEM_MIGRATION";

    private const string BackfillReferenceId =
        "6C3C_OPENING_BALANCE_BACKFILL_V1";

    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(
            migrationBuilder);

        /*
         * Id được SQLite tự sinh.
         *
         * OccurredAtUtc lấy theo CreatedAtUtc của Product,
         * giúp OpeningBalance xuất hiện tại thời điểm sản phẩm
         * bắt đầu tồn tại thay vì thời điểm chạy nâng cấp.
         *
         * Câu lệnh có tính idempotent:
         * sau khi movement được thêm, NOT EXISTS không còn đúng.
         */
        migrationBuilder.Sql(
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
            SELECT
                product."Id",
                {OpeningBalanceMovementType},
                product."StockQuantity",
                0,
                product."StockQuantity",
                'Tồn đầu kỳ được bổ sung tự động khi nâng cấp lịch sử kho.',
                '{BackfillReferenceType}',
                '{BackfillReferenceId}',
                NULL,
                product."CreatedAtUtc"
            FROM "Products" AS product
            WHERE
                product."TrackInventory" = 1
                AND product."StockQuantity" <> 0
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM "InventoryMovements" AS movement
                    WHERE
                        movement."ProductId" =
                        product."Id"
                );
            """);
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(
            migrationBuilder);

        /*
         * Chỉ xóa movement được đánh dấu chính xác bởi migration.
         *
         * Movement tạo từ ProductService hoặc thao tác người dùng
         * không có cặp reference này nên không bị ảnh hưởng.
         */
        migrationBuilder.Sql(
            $"""
            DELETE FROM "InventoryMovements"
            WHERE
                "MovementType" =
                {OpeningBalanceMovementType}
                AND "ReferenceType" =
                '{BackfillReferenceType}'
                AND "ReferenceId" =
                '{BackfillReferenceId}'
                AND "QuantityBefore" = 0
                AND "QuantityAfter" =
                    "QuantityDelta";
            """);
    }
}