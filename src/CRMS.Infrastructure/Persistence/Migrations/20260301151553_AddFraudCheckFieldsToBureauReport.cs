using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFraudCheckFieldsToBureauReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FraudCheckRawJson",
                table: "BureauReports",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FraudRecommendation",
                table: "BureauReports",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "FraudRiskScore",
                table: "BureauReports",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FraudCheckRawJson",
                table: "BureauReports");

            migrationBuilder.DropColumn(
                name: "FraudRecommendation",
                table: "BureauReports");

            migrationBuilder.DropColumn(
                name: "FraudRiskScore",
                table: "BureauReports");
        }
    }
}
