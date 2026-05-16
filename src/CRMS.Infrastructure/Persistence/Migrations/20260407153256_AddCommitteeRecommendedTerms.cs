using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitteeRecommendedTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RecommendedAmount",
                table: "CommitteeReviews",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecommendedConditions",
                table: "CommitteeReviews",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "RecommendedInterestRate",
                table: "CommitteeReviews",
                type: "decimal(8,4)",
                precision: 8,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecommendedTenorMonths",
                table: "CommitteeReviews",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecommendedAmount",
                table: "CommitteeReviews");

            migrationBuilder.DropColumn(
                name: "RecommendedConditions",
                table: "CommitteeReviews");

            migrationBuilder.DropColumn(
                name: "RecommendedInterestRate",
                table: "CommitteeReviews");

            migrationBuilder.DropColumn(
                name: "RecommendedTenorMonths",
                table: "CommitteeReviews");
        }
    }
}
