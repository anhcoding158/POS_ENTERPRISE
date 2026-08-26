using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAccountManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ForcePasswordChange",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyLocked",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmployeeCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    NormalizedEmployeeCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false, collation: "NOCASE"),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true, collation: "NOCASE"),
                    EmailAddress = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true, collation: "NOCASE"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.CheckConstraint("CK_Employees_Code_Length", "length(\"EmployeeCode\") >= 2 AND length(\"EmployeeCode\") <= 30");
                    table.CheckConstraint("CK_Employees_FullName_Length", "length(\"FullName\") >= 1 AND length(\"FullName\") <= 150");
                    table.CheckConstraint("CK_Employees_NormalizedCode_Length", "length(\"NormalizedEmployeeCode\") >= 2 AND length(\"NormalizedEmployeeCode\") <= 30");
                    table.ForeignKey(
                        name: "FK_Employees_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve existing accounts as employees during the additive upgrade. The generated
            // code is deterministic, contains no credential material, and leaves all existing
            // User IDs and historical references unchanged.
            migrationBuilder.Sql("""
                INSERT INTO "Employees"
                    ("EmployeeCode", "NormalizedEmployeeCode", "FullName", "PhoneNumber", "EmailAddress", "IsActive", "UserId", "ConcurrencyToken", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    'EMP-' || printf('%024X', "Id"),
                    'EMP-' || printf('%024X', "Id"),
                    "FullName",
                    NULL,
                    NULL,
                    "IsActive",
                    "Id",
                    lower(substr(printf('%032x', "Id"), 1, 8) || '-' || substr(printf('%032x', "Id"), 9, 4) || '-' || substr(printf('%032x', "Id"), 13, 4) || '-' || substr(printf('%032x', "Id"), 17, 4) || '-' || substr(printf('%032x', "Id"), 21, 12)),
                    "CreatedAtUtc",
                    "UpdatedAtUtc"
                FROM "Users"
                WHERE NOT EXISTS (SELECT 1 FROM "Employees" AS e WHERE e."UserId" = "Users"."Id");
                """);

            migrationBuilder.CreateTable(
                name: "SecurityAuditEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetEmployeeId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Result = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OperationId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditEvents", x => x.Id);
                    table.CheckConstraint("CK_SecurityAuditEvents_Action_Valid", "\"Action\" >= 1 AND \"Action\" <= 10");
                    table.CheckConstraint("CK_SecurityAuditEvents_Result_Length", "length(\"Result\") >= 1 AND length(\"Result\") <= 100");
                    table.ForeignKey(
                        name: "FK_SecurityAuditEvents_Employees_TargetEmployeeId",
                        column: x => x.TargetEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SecurityAuditEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SecurityAuditEvents_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Active_FullName",
                table: "Employees",
                columns: new[] { "IsActive", "FullName" });

            migrationBuilder.CreateIndex(
                name: "UX_Employees_NormalizedEmployeeCode",
                table: "Employees",
                column: "NormalizedEmployeeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Employees_UserId",
                table: "Employees",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_ActorUserId",
                table: "SecurityAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_TargetEmployee_Created",
                table: "SecurityAuditEvents",
                columns: new[] { "TargetEmployeeId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_TargetUser_Created",
                table: "SecurityAuditEvents",
                columns: new[] { "TargetUserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityAuditEvents");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropColumn(
                name: "ForcePasswordChange",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsManuallyLocked",
                table: "Users");
        }
    }
}
