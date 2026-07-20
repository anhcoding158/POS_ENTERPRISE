using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovementType = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReferenceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true, collation: "NOCASE"),
                    ReferenceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PerformedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    OccurredAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.CheckConstraint("CK_InventoryMovements_AdjustmentDirection", "\"MovementType\" <> 3 OR \"QuantityDelta\" <> 0");
                    table.CheckConstraint("CK_InventoryMovements_DecreaseDirection", "\"MovementType\" NOT IN (2, 5) OR \"QuantityDelta\" < 0");
                    table.CheckConstraint("CK_InventoryMovements_IncreaseDirection", "\"MovementType\" NOT IN (1, 6) OR \"QuantityDelta\" > 0");
                    table.CheckConstraint("CK_InventoryMovements_MovementType_Range", "\"MovementType\" >= 1 AND \"MovementType\" <= 7");
                    table.CheckConstraint("CK_InventoryMovements_OpeningBalance", "\"MovementType\" <> 7 OR (\"QuantityBefore\" = 0 AND \"QuantityDelta\" <> 0)");
                    table.CheckConstraint("CK_InventoryMovements_PerformedByUserId", "\"PerformedByUserId\" IS NULL OR \"PerformedByUserId\" > 0");
                    table.CheckConstraint("CK_InventoryMovements_ProductId_Positive", "\"ProductId\" > 0");
                    table.CheckConstraint("CK_InventoryMovements_QuantityAfter_Range", "\"QuantityAfter\" >= -999999999 AND \"QuantityAfter\" <= 999999999");
                    table.CheckConstraint("CK_InventoryMovements_QuantityBefore_Range", "\"QuantityBefore\" >= -999999999 AND \"QuantityBefore\" <= 999999999");
                    table.CheckConstraint("CK_InventoryMovements_QuantityDelta_Range", "\"QuantityDelta\" >= -1999999998 AND \"QuantityDelta\" <= 1999999998");
                    table.CheckConstraint("CK_InventoryMovements_QuantityEquation", "\"QuantityAfter\" = \"QuantityBefore\" + \"QuantityDelta\"");
                    table.CheckConstraint("CK_InventoryMovements_ReferencePair", "(\"ReferenceType\" IS NULL AND \"ReferenceId\" IS NULL) OR (\"ReferenceType\" IS NOT NULL AND \"ReferenceId\" IS NOT NULL)");
                    table.CheckConstraint("CK_InventoryMovements_ZeroDelta_StocktakeOnly", "\"QuantityDelta\" <> 0 OR \"MovementType\" = 4");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_OccurredAt",
                table: "InventoryMovements",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Product_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "ProductId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Reference",
                table: "InventoryMovements",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Type_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "MovementType", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryMovements");
        }
    }
}
