using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReceiptSnapshotPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderReceiptSnapshots",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReceiptSnapshots", x => x.OrderId);
                    table.CheckConstraint("CK_OrderReceiptSnapshots_PayloadJson_NotEmpty", "length(trim(\"PayloadJson\")) > 0");
                    table.CheckConstraint("CK_OrderReceiptSnapshots_SnapshotVersion", "\"SnapshotVersion\" > 0");
                    table.ForeignKey(
                        name: "FK_OrderReceiptSnapshots_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderReceiptSnapshots");
        }
    }
}
