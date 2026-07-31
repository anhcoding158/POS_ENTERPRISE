using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeldSales : Migration
    {
        private static readonly string[] IndexColumns1 = ["HeldSaleId", "SortOrder"];
        private static readonly string[] IndexColumns2 = ["CreatedByUserId", "Status", "UpdatedAtUtc"];
        private static readonly string[] IndexColumns3 = ["Status", "UpdatedAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeldSales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    DisplayCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    Label = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CancelledAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedOrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeldSales", x => x.Id);
                    table.CheckConstraint("CK_HeldSales_CreatedBy", "\"CreatedByUserId\" > 0");
                    table.CheckConstraint("CK_HeldSales_Fingerprint", "length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" NOT GLOB '*[^0-9A-F]*'");
                    table.CheckConstraint("CK_HeldSales_State", "(\"Status\" = 1 AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 2 AND \"CompletedAtUtc\" IS NOT NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NOT NULL) OR (\"Status\" = 3 AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NOT NULL AND \"CompletedOrderId\" IS NULL)");
                    table.CheckConstraint("CK_HeldSales_Status", "\"Status\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_HeldSales_Orders_CompletedOrderId",
                        column: x => x.CompletedOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HeldSales_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HeldSaleLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HeldSaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductCodeSnapshot = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    BarcodeSnapshot = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ProductNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPriceSnapshot = table.Column<long>(type: "INTEGER", nullable: false),
                    LineTotalSnapshot = table.Column<long>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    LineNotesSnapshot = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeldSaleLines", x => x.Id);
                    table.CheckConstraint("CK_HeldSaleLines_Code", "length(trim(\"ProductCodeSnapshot\")) > 0");
                    table.CheckConstraint("CK_HeldSaleLines_LineTotal", "\"LineTotalSnapshot\" >= 0 AND \"LineTotalSnapshot\" = \"UnitPriceSnapshot\" * \"Quantity\"");
                    table.CheckConstraint("CK_HeldSaleLines_Name", "length(trim(\"ProductNameSnapshot\")) > 0");
                    table.CheckConstraint("CK_HeldSaleLines_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_HeldSaleLines_SortOrder", "\"SortOrder\" >= 0");
                    table.CheckConstraint("CK_HeldSaleLines_UnitPrice", "\"UnitPriceSnapshot\" >= 0");
                    table.ForeignKey(
                        name: "FK_HeldSaleLines_HeldSales_HeldSaleId",
                        column: x => x.HeldSaleId,
                        principalTable: "HeldSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HeldSaleLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeldSaleLines_ProductId",
                table: "HeldSaleLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_HeldSaleLines_HeldSale_SortOrder",
                table: "HeldSaleLines",
                columns: IndexColumns1,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeldSales_CreatedBy_Status_UpdatedAtUtc",
                table: "HeldSales",
                columns: IndexColumns2);

            migrationBuilder.CreateIndex(
                name: "IX_HeldSales_Status_UpdatedAtUtc",
                table: "HeldSales",
                columns: IndexColumns3);

            migrationBuilder.CreateIndex(
                name: "UX_HeldSales_ClientRequestId",
                table: "HeldSales",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_HeldSales_CompletedOrderId",
                table: "HeldSales",
                column: "CompletedOrderId",
                unique: true,
                filter: "\"CompletedOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_HeldSales_DisplayCode",
                table: "HeldSales",
                column: "DisplayCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeldSaleLines");

            migrationBuilder.DropTable(
                name: "HeldSales");
        }
    }
}
