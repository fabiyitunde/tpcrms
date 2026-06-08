using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampFinancialAppraisalRentalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RepaymentSource",
                table: "NampFinancialAppraisalReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ProjectedMonthlyRentalRevenue",
                table: "NampFinancialAppraisalReports",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UtilisationRateAssumption",
                table: "NampFinancialAppraisalReports",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DemandEvidenceNote",
                table: "NampFinancialAppraisalReports",
                type: "longtext",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepaymentSource",
                table: "NampFinancialAppraisalReports");

            migrationBuilder.DropColumn(
                name: "ProjectedMonthlyRentalRevenue",
                table: "NampFinancialAppraisalReports");

            migrationBuilder.DropColumn(
                name: "UtilisationRateAssumption",
                table: "NampFinancialAppraisalReports");

            migrationBuilder.DropColumn(
                name: "DemandEvidenceNote",
                table: "NampFinancialAppraisalReports");
        }
    }
}
