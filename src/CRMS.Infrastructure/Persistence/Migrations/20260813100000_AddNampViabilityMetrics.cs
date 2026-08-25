using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampViabilityMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NetPresentValue",
                table: "NampFinancialAppraisalReports",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BenefitCostRatio",
                table: "NampFinancialAppraisalReports",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InternalRateOfReturn",
                table: "NampFinancialAppraisalReports",
                type: "decimal(10,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitabilityIndex",
                table: "NampFinancialAppraisalReports",
                type: "decimal(10,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NetPresentValue",       table: "NampFinancialAppraisalReports");
            migrationBuilder.DropColumn(name: "BenefitCostRatio",      table: "NampFinancialAppraisalReports");
            migrationBuilder.DropColumn(name: "InternalRateOfReturn",  table: "NampFinancialAppraisalReports");
            migrationBuilder.DropColumn(name: "ProfitabilityIndex",    table: "NampFinancialAppraisalReports");
        }
    }
}
