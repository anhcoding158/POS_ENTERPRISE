using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutIdempotencyJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckoutRequestJournals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    CanonicalRequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    PreparedQuoteFingerprint = table.Column<string>(type: "TEXT", fixedLength: true, maxLength: 64, nullable: false),
                    PreparedQuoteJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PreparedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    AcknowledgedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    AbandonedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    AbandonedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutRequestJournals", x => x.Id);
                    table.CheckConstraint("CK_CheckoutRequestJournals_Json", "length(trim(\"CanonicalRequestJson\")) > 0 AND length(trim(\"PreparedQuoteJson\")) > 0");
                    table.CheckConstraint("CK_CheckoutRequestJournals_QuoteFingerprint", "length(\"PreparedQuoteFingerprint\") = 64 AND \"PreparedQuoteFingerprint\" NOT GLOB '*[^0-9A-F]*'");
                    table.CheckConstraint("CK_CheckoutRequestJournals_RequestFingerprint", "length(\"RequestFingerprint\") = 64 AND \"RequestFingerprint\" NOT GLOB '*[^0-9A-F]*'");
                    table.CheckConstraint("CK_CheckoutRequestJournals_StateShape", "(\"Status\" = 1 AND \"OrderId\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"AcknowledgedAtUtc\" IS NULL AND \"AbandonedAtUtc\" IS NULL AND \"AbandonedByUserId\" IS NULL) OR (\"Status\" = 2 AND \"OrderId\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"AbandonedAtUtc\" IS NULL AND \"AbandonedByUserId\" IS NULL) OR (\"Status\" = 3 AND \"OrderId\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"AcknowledgedAtUtc\" IS NULL AND \"AbandonedAtUtc\" IS NOT NULL AND \"AbandonedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CK_CheckoutRequestJournals_Status", "\"Status\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_CheckoutRequestJournals_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckoutRequestJournals_Users_AbandonedByUserId",
                        column: x => x.AbandonedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CheckoutRequestJournals_Users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutRequestJournals_AbandonedByUserId",
                table: "CheckoutRequestJournals",
                column: "AbandonedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutRequestJournals_CreatedAtUtc",
                table: "CheckoutRequestJournals",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutRequestJournals_Status_AcknowledgedAtUtc",
                table: "CheckoutRequestJournals",
                columns: new[] { "Status", "AcknowledgedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutRequestJournals_User_Status_CreatedAtUtc",
                table: "CheckoutRequestJournals",
                columns: new[] { "PreparedByUserId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_CheckoutRequestJournals_ClientRequestId",
                table: "CheckoutRequestJournals",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CheckoutRequestJournals_OrderId",
                table: "CheckoutRequestJournals",
                column: "OrderId",
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutRequestJournals");
        }
    }
}
