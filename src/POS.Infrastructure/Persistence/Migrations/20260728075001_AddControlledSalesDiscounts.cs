using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddControlledSalesDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                table: "HeldSales",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountType",
                table: "HeldSales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "RequestedDiscountValue",
                table: "HeldSales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ResolvedDiscountAmountSnapshot",
                table: "HeldSales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SubtotalSnapshot",
                table: "HeldSales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TotalSnapshot",
                table: "HeldSales",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "OrderDiscountSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedValue = table.Column<long>(type: "INTEGER", nullable: false),
                    ResolvedAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AppliedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppliedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderDiscountSnapshots", x => x.Id);
                    table.CheckConstraint("CK_OrderDiscountSnapshots_AppliedByUserId", "\"AppliedByUserId\" > 0");
                    table.CheckConstraint("CK_OrderDiscountSnapshots_Reason", "length(trim(\"Reason\")) > 0");
                    table.CheckConstraint("CK_OrderDiscountSnapshots_RequestedValue", "\"RequestedValue\" > 0");
                    table.CheckConstraint("CK_OrderDiscountSnapshots_ResolvedAmount", "\"ResolvedAmount\" > 0");
                    table.CheckConstraint("CK_OrderDiscountSnapshots_Type", "\"Type\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_OrderDiscountSnapshots_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderDiscountSnapshots_Users_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                UPDATE "HeldSales"
                SET "SubtotalSnapshot" = COALESCE(
                        (SELECT SUM("LineTotalSnapshot")
                         FROM "HeldSaleLines"
                         WHERE "HeldSaleId" = "HeldSales"."Id"), 0),
                    "TotalSnapshot" = COALESCE(
                        (SELECT SUM("LineTotalSnapshot")
                         FROM "HeldSaleLines"
                         WHERE "HeldSaleId" = "HeldSales"."Id"), 0)
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_HeldSales_DiscountAmount",
                table: "HeldSales",
                sql: "\"ResolvedDiscountAmountSnapshot\" >= 0 AND \"ResolvedDiscountAmountSnapshot\" <= \"SubtotalSnapshot\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HeldSales_TotalEquation",
                table: "HeldSales",
                sql: "\"TotalSnapshot\" = \"SubtotalSnapshot\" - \"ResolvedDiscountAmountSnapshot\"");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDiscountSnapshots_AppliedByUserId",
                table: "OrderDiscountSnapshots",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_OrderDiscountSnapshots_OrderId",
                table: "OrderDiscountSnapshots",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderDiscountSnapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HeldSales_DiscountAmount",
                table: "HeldSales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HeldSales_TotalEquation",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "RequestedDiscountValue",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "ResolvedDiscountAmountSnapshot",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "SubtotalSnapshot",
                table: "HeldSales");

            migrationBuilder.DropColumn(
                name: "TotalSnapshot",
                table: "HeldSales");
        }
    }
}
