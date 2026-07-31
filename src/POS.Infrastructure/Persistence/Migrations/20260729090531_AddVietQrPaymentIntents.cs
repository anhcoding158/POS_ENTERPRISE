using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVietQrPaymentIntents : Migration
    {
        private static readonly string[] CreatedByStatusUpdatedColumns =
            ["CreatedByUserId", "Status", "UpdatedAtUtc"];

        private static readonly string[] StatusUpdatedColumns =
            ["Status", "UpdatedAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentIntents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayCode = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 3, nullable: false),
                    TransferContent = table.Column<string>(type: "TEXT", maxLength: 99, nullable: false),
                    PayloadText = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    BankCodeSnapshot = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AccountNumberSnapshot = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AccountNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    QuoteFingerprint = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    HeldSaleId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfirmedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletedOrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    PresentedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ConfirmedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    CancelledAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ExpiresAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentIntents", x => x.Id);
                    table.CheckConstraint("CK_PaymentIntents_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_PaymentIntents_Currency", "\"Currency\" = 'VND'");
                    table.CheckConstraint("CK_PaymentIntents_PayloadHash", "length(\"PayloadHash\") = 64 AND \"PayloadHash\" NOT GLOB '*[^0-9A-F]*'");
                    table.CheckConstraint("CK_PaymentIntents_Provider", "\"Provider\" = 1");
                    table.CheckConstraint("CK_PaymentIntents_QuoteFingerprint", "length(\"QuoteFingerprint\") = 64 AND \"QuoteFingerprint\" NOT GLOB '*[^0-9A-F]*'");
                    table.CheckConstraint("CK_PaymentIntents_StateShape", "(\"Status\" = 1 AND \"PresentedAtUtc\" IS NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 2 AND \"PresentedAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 3 AND \"PresentedAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 4 AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"CompletedOrderId\" IS NOT NULL AND \"CancelledAtUtc\" IS NULL) OR (\"Status\" = 5 AND \"CancelledAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 6 AND \"CompletedAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL AND \"CancelledAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_PaymentIntents_Status", "\"Status\" IN (1,2,3,4,5,6)");
                    table.ForeignKey(
                        name: "FK_PaymentIntents_HeldSales_HeldSaleId",
                        column: x => x.HeldSaleId,
                        principalTable: "HeldSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentIntents_Orders_CompletedOrderId",
                        column: x => x.CompletedOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentIntents_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentIntents_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_ClientRequestId",
                table: "PaymentIntents",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_CompletedOrderId",
                table: "PaymentIntents",
                column: "CompletedOrderId",
                unique: true,
                filter: "\"CompletedOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_ConfirmedByUserId",
                table: "PaymentIntents",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_CreatedByUserId_Status_UpdatedAtUtc",
                table: "PaymentIntents",
                columns: CreatedByStatusUpdatedColumns);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_DisplayCode",
                table: "PaymentIntents",
                column: "DisplayCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_HeldSaleId",
                table: "PaymentIntents",
                column: "HeldSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_PayloadHash",
                table: "PaymentIntents",
                column: "PayloadHash");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_QuoteFingerprint",
                table: "PaymentIntents",
                column: "QuoteFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_Status_UpdatedAtUtc",
                table: "PaymentIntents",
                columns: StatusUpdatedColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentIntents");
        }
    }
}
