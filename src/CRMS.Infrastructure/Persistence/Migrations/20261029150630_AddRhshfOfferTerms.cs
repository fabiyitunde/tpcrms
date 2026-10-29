using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfOfferTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedInterest",
                table: "RhshfOffers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestRatePercent",
                table: "RhshfOffers",
                type: "decimal(9,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TenorMonths",
                table: "RhshfOffers",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedInterest",
                table: "RhshfOffers");

            migrationBuilder.DropColumn(
                name: "InterestRatePercent",
                table: "RhshfOffers");

            migrationBuilder.DropColumn(
                name: "TenorMonths",
                table: "RhshfOffers");
        }
    }
}
