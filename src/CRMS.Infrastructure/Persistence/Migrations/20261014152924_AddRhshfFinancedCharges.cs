using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfFinancedCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceCharge",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnologyServicesFee",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkingCapitalCharge",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InsuranceCharge",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "TechnologyServicesFee",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "WorkingCapitalCharge",
                table: "RhshfCreditProfiles");
        }
    }
}
