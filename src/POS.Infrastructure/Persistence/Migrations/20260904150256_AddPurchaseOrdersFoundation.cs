using System;
using Microsoft.EntityFrameworkCore.Migrations;

#pragma warning disable CA1861

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrdersFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents");

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    NormalizedOrderNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SupplierName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    SupplierTaxCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    OrderDate = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedDeliveryDate = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    OrderedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CancelledAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.CheckConstraint("CK_PurchaseOrders_CancelledAtUtc_State", "(\"Status\" = 3 AND \"CancelledAtUtc\" IS NOT NULL AND \"CancellationReason\" IS NOT NULL) OR (\"Status\" <> 3 AND \"CancelledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");
                    table.CheckConstraint("CK_PurchaseOrders_ExpectedDate_Range", "\"ExpectedDeliveryDate\" IS NULL OR \"ExpectedDeliveryDate\" >= \"OrderDate\"");
                    table.CheckConstraint("CK_PurchaseOrders_OrderedAtUtc_State", "(\"Status\" = 1 AND \"OrderedAtUtc\" IS NULL AND \"OrderedByUserId\" IS NULL) OR (\"Status\" = 2 AND \"OrderedAtUtc\" IS NOT NULL AND \"OrderedByUserId\" IS NOT NULL) OR \"Status\" = 3");
                    table.CheckConstraint("CK_PurchaseOrders_OrderNumber_Length", "length(\"OrderNumber\") >= 1 AND length(\"OrderNumber\") <= 50");
                    table.CheckConstraint("CK_PurchaseOrders_Status_Valid", "\"Status\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Users_OrderedByUserId",
                        column: x => x.OrderedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PurchaseOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UnitName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OrderedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AgreedUnitCost = table.Column<long>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.Id);
                    table.CheckConstraint("CK_PurchaseOrderLines_AgreedUnitCost_Range", "\"AgreedUnitCost\" >= 0 AND \"AgreedUnitCost\" <= 999999999999");
                    table.CheckConstraint("CK_PurchaseOrderLines_OrderedQuantity_Range", "\"OrderedQuantity\" > 0 AND \"OrderedQuantity\" <= 999999");
                    table.CheckConstraint("CK_PurchaseOrderLines_ReceivedQuantity_Range", "\"ReceivedQuantity\" >= 0 AND \"ReceivedQuantity\" <= \"OrderedQuantity\"");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents",
                sql: "\"Action\" >= 1 AND \"Action\" <= 24");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_Order_SortOrder",
                table: "PurchaseOrderLines",
                columns: new[] { "PurchaseOrderId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_Product",
                table: "PurchaseOrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_PurchaseOrderLines_Order_Product",
                table: "PurchaseOrderLines",
                columns: new[] { "PurchaseOrderId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CancelledByUserId",
                table: "PurchaseOrders",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OrderedByUserId",
                table: "PurchaseOrders",
                column: "OrderedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Status_ExpectedDeliveryDate",
                table: "PurchaseOrders",
                columns: new[] { "Status", "ExpectedDeliveryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Supplier_Status_OrderDate",
                table: "PurchaseOrders",
                columns: new[] { "SupplierId", "Status", "OrderDate" });

            migrationBuilder.CreateIndex(
                name: "UX_PurchaseOrders_NormalizedOrderNumber",
                table: "PurchaseOrders",
                column: "NormalizedOrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents",
                sql: "\"Action\" >= 1 AND \"Action\" <= 20");
        }
    }
}
