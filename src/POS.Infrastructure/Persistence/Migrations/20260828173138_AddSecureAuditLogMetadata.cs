using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecureAuditLogMetadata : Migration
    {
        private static readonly string[] AreaActionCreatedColumns = ["BusinessArea", "Action", "CreatedAtUtc"];
        private static readonly string[] CreatedIdColumns = ["CreatedAtUtc", "Id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorDisplayNameSnapshot",
                table: "SecurityAuditEvents",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AfterValuesJson",
                table: "SecurityAuditEvents",
                type: "TEXT",
                maxLength: 64000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BeforeValuesJson",
                table: "SecurityAuditEvents",
                type: "TEXT",
                maxLength: 64000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BusinessArea",
                table: "SecurityAuditEvents",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetDisplayNameSnapshot",
                table: "SecurityAuditEvents",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "SecurityAuditEvents",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TerminalId",
                table: "SecurityAuditEvents",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_Area_Action_Created",
                table: "SecurityAuditEvents",
                columns: AreaActionCreatedColumns);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_Created_Id",
                table: "SecurityAuditEvents",
                columns: CreatedIdColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SecurityAuditEvents_Area_Action_Created",
                table: "SecurityAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_SecurityAuditEvents_Created_Id",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "ActorDisplayNameSnapshot",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "AfterValuesJson",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "BeforeValuesJson",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "BusinessArea",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "TargetDisplayNameSnapshot",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "SecurityAuditEvents");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "SecurityAuditEvents");
        }
    }
}
