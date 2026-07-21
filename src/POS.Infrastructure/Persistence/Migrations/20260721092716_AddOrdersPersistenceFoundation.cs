using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdersPersistenceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    CashierUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    RestaurantTableId = table.Column<int>(type: "INTEGER", nullable: true),
                    DiscountId = table.Column<int>(type: "INTEGER", nullable: true),
                    DiscountCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true, collation: "NOCASE"),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Subtotal = table.Column<long>(type: "INTEGER", nullable: false),
                    DiscountAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: true),
                    CashReceived = table.Column<long>(type: "INTEGER", nullable: false),
                    ChangeAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    RefundedAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    PaidAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CancelledAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CancelReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_CancelledAtUtc_State", "(\"Status\" = 5 AND \"CancelledAtUtc\" IS NOT NULL) OR (\"Status\" <> 5 AND \"CancelledAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_Orders_CashierUserId_Positive", "\"CashierUserId\" > 0");
                    table.CheckConstraint("CK_Orders_CashPayment_Rule", "\"PaymentMethod\" IS NULL OR \"PaymentMethod\" <> 1 OR \"CashReceived\" >= \"TotalAmount\"");
                    table.CheckConstraint("CK_Orders_CashReceived_NonNegative", "\"CashReceived\" >= 0");
                    table.CheckConstraint("CK_Orders_ChangeAmount_NonNegative", "\"ChangeAmount\" >= 0");
                    table.CheckConstraint("CK_Orders_ChangeNotAboveCash", "\"ChangeAmount\" <= \"CashReceived\"");
                    table.CheckConstraint("CK_Orders_CustomerId_Positive", "\"CustomerId\" IS NULL OR \"CustomerId\" > 0");
                    table.CheckConstraint("CK_Orders_DiscountAmount_Range", "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"Subtotal\"");
                    table.CheckConstraint("CK_Orders_DiscountId_Positive", "\"DiscountId\" IS NULL OR \"DiscountId\" > 0");
                    table.CheckConstraint("CK_Orders_NonCashPayment_Rule", "\"PaymentMethod\" IS NULL OR \"PaymentMethod\" = 1 OR (\"CashReceived\" = 0 AND \"ChangeAmount\" = 0)");
                    table.CheckConstraint("CK_Orders_PaidAtUtc_State", "(\"Status\" IN (1, 2, 5) AND \"PaidAtUtc\" IS NULL) OR (\"Status\" IN (3, 4, 6, 7) AND \"PaidAtUtc\" IS NOT NULL)");
                    table.CheckConstraint("CK_Orders_PaidState_Payment", "(\"Status\" IN (1, 2, 5) AND \"PaymentMethod\" IS NULL) OR (\"Status\" IN (3, 4, 6, 7) AND \"PaymentMethod\" IS NOT NULL)");
                    table.CheckConstraint("CK_Orders_PaymentMethod_Valid", "\"PaymentMethod\" IS NULL OR \"PaymentMethod\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Orders_RefundedAmount_Range", "\"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"TotalAmount\"");
                    table.CheckConstraint("CK_Orders_RestaurantTableId_Positive", "\"RestaurantTableId\" IS NULL OR \"RestaurantTableId\" > 0");
                    table.CheckConstraint("CK_Orders_Status_Valid", "\"Status\" IN (1, 2, 3, 4, 5, 6, 7)");
                    table.CheckConstraint("CK_Orders_Subtotal_Range", "\"Subtotal\" >= 0 AND \"Subtotal\" <= 999999999999");
                    table.CheckConstraint("CK_Orders_TotalAmount_Equation", "\"TotalAmount\" = \"Subtotal\" - \"DiscountAmount\"");
                    table.CheckConstraint("CK_Orders_TotalAmount_Range", "\"TotalAmount\" >= 0 AND \"TotalAmount\" <= 999999999999");
                    table.ForeignKey(
                        name: "FK_Orders_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UnitName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitCostPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitSalePrice = table.Column<long>(type: "INTEGER", nullable: false),
                    LineDiscountAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RefundedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_LineDiscount_NonNegative", "\"LineDiscountAmount\" >= 0");
                    table.CheckConstraint("CK_OrderItems_OrderId_Positive", "\"OrderId\" > 0");
                    table.CheckConstraint("CK_OrderItems_ProductId_Positive", "\"ProductId\" > 0");
                    table.CheckConstraint("CK_OrderItems_Quantity_Range", "\"Quantity\" > 0 AND \"Quantity\" <= 999999");
                    table.CheckConstraint("CK_OrderItems_RefundedQuantity_Range", "\"RefundedQuantity\" >= 0 AND \"RefundedQuantity\" <= \"Quantity\"");
                    table.CheckConstraint("CK_OrderItems_Status_Valid", "\"Status\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_OrderItems_UnitCostPrice_Range", "\"UnitCostPrice\" >= 0 AND \"UnitCostPrice\" <= 999999999999");
                    table.CheckConstraint("CK_OrderItems_UnitSalePrice_Range", "\"UnitSalePrice\" >= 0 AND \"UnitSalePrice\" <= 999999999999");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemModifiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModifierId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModifierGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModifierGroupName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifierName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitAdditionalPrice = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemModifiers", x => x.Id);
                    table.CheckConstraint("CK_OrderItemModifiers_GroupId_Positive", "\"ModifierGroupId\" > 0");
                    table.CheckConstraint("CK_OrderItemModifiers_ModifierId_Positive", "\"ModifierId\" > 0");
                    table.CheckConstraint("CK_OrderItemModifiers_OrderItemId_Positive", "\"OrderItemId\" > 0");
                    table.CheckConstraint("CK_OrderItemModifiers_Price_Range", "\"UnitAdditionalPrice\" >= 0 AND \"UnitAdditionalPrice\" <= 999999999");
                    table.CheckConstraint("CK_OrderItemModifiers_Quantity_Range", "\"Quantity\" > 0 AND \"Quantity\" <= 999999");
                    table.ForeignKey(
                        name: "FK_OrderItemModifiers_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemModifiers_Item_Group",
                table: "OrderItemModifiers",
                columns: new[] { "OrderItemId", "ModifierGroupId" });

            migrationBuilder.CreateIndex(
                name: "UX_OrderItemModifiers_OrderItem_Modifier",
                table: "OrderItemModifiers",
                columns: new[] { "OrderItemId", "ModifierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_Product_CreatedAtUtc",
                table: "OrderItems",
                columns: new[] { "ProductId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Cashier_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "CashierUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Customer_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Table_Status",
                table: "Orders",
                columns: new[] { "RestaurantTableId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Orders_OrderCode",
                table: "Orders",
                column: "OrderCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItemModifiers");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
