using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurablePaymentIntentManualResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentIntentManualResolutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PaymentIntentId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolutionType = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolvedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ResolvedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ExternalReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LinkedOrderId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentIntentManualResolutions", x => x.Id);
                    table.CheckConstraint("CK_PaymentIntentManualResolutions_Shape", "(\"ResolutionType\" = 1 AND \"LinkedOrderId\" IS NOT NULL) OR (\"ResolutionType\" = 2 AND \"LinkedOrderId\" IS NULL) OR (\"ResolutionType\" = 3 AND \"LinkedOrderId\" IS NULL AND length(trim(\"ExternalReference\")) > 0)");
                    table.CheckConstraint("CK_PaymentIntentManualResolutions_Type", "\"ResolutionType\" IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_PaymentIntentManualResolutions_Orders_LinkedOrderId",
                        column: x => x.LinkedOrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentIntentManualResolutions_PaymentIntents_PaymentIntentId",
                        column: x => x.PaymentIntentId,
                        principalTable: "PaymentIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentIntentManualResolutions_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntentManualResolutions_LinkedOrderId",
                table: "PaymentIntentManualResolutions",
                column: "LinkedOrderId",
                unique: true,
                filter: "\"LinkedOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntentManualResolutions_PaymentIntentId",
                table: "PaymentIntentManualResolutions",
                column: "PaymentIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntentManualResolutions_ResolvedByUserId",
                table: "PaymentIntentManualResolutions",
                column: "ResolvedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentIntentManualResolutions");
        }
    }
}
