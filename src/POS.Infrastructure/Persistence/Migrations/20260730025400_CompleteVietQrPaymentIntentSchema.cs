using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteVietQrPaymentIntentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutRequestJson",
                table: "PaymentIntents",
                type: "TEXT",
                maxLength: 16384,
                nullable: false,
                defaultValue: "{\"schema\":\"legacy-payment-intent-v1\"}");

            migrationBuilder.AddColumn<long>(
                name: "ExpiredAtUtc",
                table: "PaymentIntents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpirationReason",
                table: "PaymentIntents",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "PaymentIntents"
                SET "ExpiredAtUtc" = COALESCE("ExpiresAtUtc", "UpdatedAtUtc"),
                    "ExpirationReason" = 'Legacy expired PaymentIntent upgraded from schema v1.'
                WHERE "Status" = 6;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentIntents_StateShape",
                table: "PaymentIntents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentIntents_StateShape",
                table: "PaymentIntents",
                sql: "(\"Status\" = 1 AND \"PresentedAtUtc\" IS NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 2 AND \"PresentedAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 3 AND \"PresentedAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 4 AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"CompletedOrderId\" IS NOT NULL AND \"CancelledAtUtc\" IS NULL) OR (\"Status\" = 5 AND \"CancelledAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 6 AND \"ExpiredAtUtc\" IS NOT NULL AND \"ExpirationReason\" IS NOT NULL AND \"CompletedAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL AND \"CancelledAtUtc\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentIntents_StateShape",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "CheckoutRequestJson",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "ExpiredAtUtc",
                table: "PaymentIntents");

            migrationBuilder.DropColumn(
                name: "ExpirationReason",
                table: "PaymentIntents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentIntents_StateShape",
                table: "PaymentIntents",
                sql: "(\"Status\" = 1 AND \"PresentedAtUtc\" IS NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 2 AND \"PresentedAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 3 AND \"PresentedAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 4 AND \"ConfirmedAtUtc\" IS NOT NULL AND \"ConfirmedByUserId\" IS NOT NULL AND \"CompletedAtUtc\" IS NOT NULL AND \"CompletedOrderId\" IS NOT NULL AND \"CancelledAtUtc\" IS NULL) OR (\"Status\" = 5 AND \"CancelledAtUtc\" IS NOT NULL AND \"ConfirmedAtUtc\" IS NULL AND \"CompletedAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL) OR (\"Status\" = 6 AND \"CompletedAtUtc\" IS NULL AND \"CompletedOrderId\" IS NULL AND \"CancelledAtUtc\" IS NULL)");
        }
    }
}
