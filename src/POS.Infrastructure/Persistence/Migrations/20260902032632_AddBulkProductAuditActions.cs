using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkProductAuditActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents",
                sql: "\"Action\" >= 1 AND \"Action\" <= 15");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents",
                sql: "\"Action\" >= 1 AND \"Action\" <= 10");
        }
    }
}
