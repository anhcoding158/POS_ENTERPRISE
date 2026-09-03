using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeSecurityAuditHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents");

            migrationBuilder.AddColumn<long>(
                name: "LastFailedLoginAtUtc",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents",
                sql: "\"Action\" >= 1 AND \"Action\" <= 16");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "LastFailedLoginAtUtc",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents",
                sql: "\"Action\" >= 1 AND \"Action\" <= 15");
        }
    }
}
