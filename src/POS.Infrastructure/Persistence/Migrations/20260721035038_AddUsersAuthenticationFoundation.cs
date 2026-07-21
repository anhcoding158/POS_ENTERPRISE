using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAuthenticationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    NormalizedUsername = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false, collation: "NOCASE"),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedLoginAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LockedUntilUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    LastLoginAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_FailedLoginAttempts_NonNegative", "\"FailedLoginAttempts\" >= 0");
                    table.CheckConstraint("CK_Users_FullName_Length", "length(\"FullName\") >= 1 AND length(\"FullName\") <= 150");
                    table.CheckConstraint("CK_Users_NormalizedUsername_Length", "length(\"NormalizedUsername\") >= 3 AND length(\"NormalizedUsername\") <= 50");
                    table.CheckConstraint("CK_Users_PasswordHash_Length", "length(\"PasswordHash\") >= 1 AND length(\"PasswordHash\") <= 200");
                    table.CheckConstraint("CK_Users_Role_Valid", "\"Role\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Users_Username_Length", "length(\"Username\") >= 3 AND length(\"Username\") <= 50");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Active_Role_FullName",
                table: "Users",
                columns: new[] { "IsActive", "Role", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_LockedUntilUtc",
                table: "Users",
                column: "LockedUntilUtc");

            migrationBuilder.CreateIndex(
                name: "UX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
