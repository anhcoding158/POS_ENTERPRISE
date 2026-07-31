using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReturnsFoundation : Migration
    {
        private static readonly string[] IndexColumns1 = ["OrderId", "CreatedAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_IncreaseDirection",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_MovementType_Range",
                table: "InventoryMovements");

            migrationBuilder.CreateTable(
                name: "OrderReturnBalances",
                columns: table => new
                {
                    OrderItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReturnedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RefundedAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReturnBalances", x => x.OrderItemId);
                    table.CheckConstraint("CK_OrderReturnBalances_RefundedAmount", "\"RefundedAmount\" >= 0");
                    table.CheckConstraint("CK_OrderReturnBalances_ReturnedQuantity", "\"ReturnedQuantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_OrderReturnBalances_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderReturns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RefundMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    RefundReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TotalRefundAmount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReturns", x => x.Id);
                    table.CheckConstraint("CK_OrderReturns_OrderId", "\"OrderId\" > 0");
                    table.CheckConstraint("CK_OrderReturns_ProcessedByUserId", "\"ProcessedByUserId\" > 0");
                    table.CheckConstraint("CK_OrderReturns_RequestFingerprint", "length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" NOT GLOB '*[^0-9A-F]*'");
                    table.CheckConstraint("CK_OrderReturns_TotalRefundAmount", "\"TotalRefundAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_OrderReturns_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReturns_Users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderReturnItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderReturnId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UnitName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ReturnQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RestockQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RefundAmount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReturnItems", x => x.Id);
                    table.CheckConstraint("CK_OrderReturnItems_RefundAmount", "\"RefundAmount\" > 0");
                    table.CheckConstraint("CK_OrderReturnItems_RestockQuantity", "\"RestockQuantity\" >= 0 AND \"RestockQuantity\" <= \"ReturnQuantity\"");
                    table.CheckConstraint("CK_OrderReturnItems_ReturnQuantity", "\"ReturnQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_OrderReturnItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReturnItems_OrderReturns_OrderReturnId",
                        column: x => x.OrderReturnId,
                        principalTable: "OrderReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderReturnItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_IncreaseDirection",
                table: "InventoryMovements",
                sql: "\"MovementType\" NOT IN (1, 6, 8) OR \"QuantityDelta\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_MovementType_Range",
                table: "InventoryMovements",
                sql: "\"MovementType\" >= 1 AND \"MovementType\" <= 8");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturnItems_OrderItemId",
                table: "OrderReturnItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturnItems_OrderReturnId",
                table: "OrderReturnItems",
                column: "OrderReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturnItems_ProductId",
                table: "OrderReturnItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturns_Order_CreatedAtUtc",
                table: "OrderReturns",
                columns: IndexColumns1);

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturns_ProcessedByUserId",
                table: "OrderReturns",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_OrderReturns_ClientRequestId",
                table: "OrderReturns",
                column: "ClientRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderReturnBalances");

            migrationBuilder.DropTable(
                name: "OrderReturnItems");

            migrationBuilder.DropTable(
                name: "OrderReturns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_IncreaseDirection",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_MovementType_Range",
                table: "InventoryMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_IncreaseDirection",
                table: "InventoryMovements",
                sql: "\"MovementType\" NOT IN (1, 6) OR \"QuantityDelta\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_MovementType_Range",
                table: "InventoryMovements",
                sql: "\"MovementType\" >= 1 AND \"MovementType\" <= 7");
        }
    }
}
