using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfDocumentFormatRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedFormats",
                table: "RhshfDocumentRequirements",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "MustMatchFinancialTemplate",
                table: "RhshfDocumentRequirements",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedFormats",
                table: "RhshfDocumentRequirements");

            migrationBuilder.DropColumn(
                name: "MustMatchFinancialTemplate",
                table: "RhshfDocumentRequirements");
        }
    }
}
