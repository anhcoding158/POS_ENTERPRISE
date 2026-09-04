using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents");

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    NormalizedCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, collation: "NOCASE"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    TaxCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ContactName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    EmailAddress = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.CheckConstraint("CK_Suppliers_Code_Length", "length(\"Code\") >= 2 AND length(\"Code\") <= 30");
                    table.CheckConstraint("CK_Suppliers_Name_Length", "length(\"Name\") >= 1 AND length(\"Name\") <= 150");
                    table.CheckConstraint("CK_Suppliers_NormalizedCode_Length", "length(\"NormalizedCode\") >= 2 AND length(\"NormalizedCode\") <= 30");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents",
                sql: "\"Action\" >= 1 AND \"Action\" <= 20");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Active_Name_Code",
                table: "Suppliers",
                columns: new[] { "IsActive", "Name", "Code" });

            migrationBuilder.CreateIndex(
                name: "UX_Suppliers_NormalizedCode",
                table: "Suppliers",
                column: "NormalizedCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SecurityAuditEvents_Action_Valid",
                table: "SecurityAuditEvents",
                sql: "\"Action\" >= 1 AND \"Action\" <= 16");
        }
    }
}
