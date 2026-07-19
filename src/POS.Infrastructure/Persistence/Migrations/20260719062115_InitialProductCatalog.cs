using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialProductCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_DisplayOrder_Range", "\"DisplayOrder\" >= 0 AND \"DisplayOrder\" <= 100000");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    UnitName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CostPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    SalePrice = table.Column<long>(type: "INTEGER", nullable: false),
                    StockQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumStock = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackInventory = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowNegativeStock = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_AllowNegativeStock_RequiresTracking", "\"AllowNegativeStock\" = 0 OR \"TrackInventory\" = 1");
                    table.CheckConstraint("CK_Products_CostPrice_Range", "\"CostPrice\" >= 0 AND \"CostPrice\" <= 999999999999");
                    table.CheckConstraint("CK_Products_MinimumStock_Range", "\"MinimumStock\" >= 0 AND \"MinimumStock\" <= 999999999");
                    table.CheckConstraint("CK_Products_MinimumStock_RequiresTracking", "\"TrackInventory\" = 1 OR \"MinimumStock\" = 0");
                    table.CheckConstraint("CK_Products_NegativeStock_Rule", "\"AllowNegativeStock\" = 1 OR \"StockQuantity\" >= 0");
                    table.CheckConstraint("CK_Products_SalePrice_Range", "\"SalePrice\" >= 0 AND \"SalePrice\" <= 999999999999");
                    table.CheckConstraint("CK_Products_StockQuantity_Range", "\"StockQuantity\" >= -999999999 AND \"StockQuantity\" <= 999999999");
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Active_DisplayOrder_Name",
                table: "Categories",
                columns: new[] { "IsActive", "DisplayOrder", "Name" });

            migrationBuilder.CreateIndex(
                name: "UX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category_Active_Name",
                table: "Products",
                columns: new[] { "CategoryId", "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Inventory_Active_Stock",
                table: "Products",
                columns: new[] { "TrackInventory", "IsActive", "StockQuantity" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "UX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Products_Code",
                table: "Products",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
