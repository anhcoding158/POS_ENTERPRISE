using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeldSalePaymentOwnershipGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentIntents_HeldSaleId",
                table: "PaymentIntents");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentIntents_Active_HeldSaleOwner",
                table: "PaymentIntents",
                column: "HeldSaleId",
                unique: true,
                filter: "\"HeldSaleId\" IS NOT NULL AND \"Status\" IN (1,2,3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PaymentIntents_Active_HeldSaleOwner",
                table: "PaymentIntents");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_HeldSaleId",
                table: "PaymentIntents",
                column: "HeldSaleId");
        }
    }
}
