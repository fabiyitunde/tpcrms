using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalClearanceToCollateral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegalClearanceNotes",
                table: "Collaterals",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LegalClearedAt",
                table: "Collaterals",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LegalClearedByUserId",
                table: "Collaterals",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LegalClearanceNotes",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "LegalClearedAt",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "LegalClearedByUserId",
                table: "Collaterals");
        }
    }
}
