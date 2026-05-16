using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditAdvisoryJsonColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionsJson",
                table: "CreditAdvisories",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CovenantsJson",
                table: "CreditAdvisories",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RedFlagsJson",
                table: "CreditAdvisories",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RiskScoresJson",
                table: "CreditAdvisories",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionsJson",
                table: "CreditAdvisories");

            migrationBuilder.DropColumn(
                name: "CovenantsJson",
                table: "CreditAdvisories");

            migrationBuilder.DropColumn(
                name: "RedFlagsJson",
                table: "CreditAdvisories");

            migrationBuilder.DropColumn(
                name: "RiskScoresJson",
                table: "CreditAdvisories");
        }
    }
}
