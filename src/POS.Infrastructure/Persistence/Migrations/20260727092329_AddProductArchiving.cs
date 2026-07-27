using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductArchiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ArchivedAtUtc",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchivedByUserId",
                table: "Products",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Archived_Active_Name",
                table: "Products",
                columns: new[] { "IsArchived", "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ArchivedByUserId",
                table: "Products",
                column: "ArchivedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ArchiveState",
                table: "Products",
                sql: "(\"IsArchived\" = 0 AND \"ArchivedAtUtc\" IS NULL AND \"ArchivedByUserId\" IS NULL) OR (\"IsArchived\" = 1 AND \"ArchivedAtUtc\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Users_ArchivedByUserId",
                table: "Products",
                column: "ArchivedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Users_ArchivedByUserId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Archived_Active_Name",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_ArchivedByUserId",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ArchiveState",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Products");
        }
    }
}
