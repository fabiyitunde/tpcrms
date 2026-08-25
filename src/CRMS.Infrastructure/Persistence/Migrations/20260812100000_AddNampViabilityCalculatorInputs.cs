using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampViabilityCalculatorInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HectaresPerMonth",
                table: "NampFinancialAppraisalReports",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RatePerHectare",
                table: "NampFinancialAppraisalReports",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyFuelCost",
                table: "NampFinancialAppraisalReports",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyMaintenanceCost",
                table: "NampFinancialAppraisalReports",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyOperatorWage",
                table: "NampFinancialAppraisalReports",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HectaresPerMonth",       table: "NampFinancialAppraisalReports");
            migrationBuilder.DropColumn(name: "RatePerHectare",         table: "NampFinancialAppraisalReports");
            migrationBuilder.DropColumn(name: "MonthlyFuelCost",        table: "NampFinancialAppraisalReports");
            migrationBuilder.DropColumn(name: "MonthlyMaintenanceCost", table: "NampFinancialAppraisalReports");
            migrationBuilder.DropColumn(name: "MonthlyOperatorWage",    table: "NampFinancialAppraisalReports");
        }
    }
}
