using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampAppraisalReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "NampDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "NampApplicationId",
                table: "CreditCheckOutbox",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "NampApplicationId",
                table: "BureauReports",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "NampFinancialAppraisalReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PreparedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SavedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MonthlyDisposableIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DebtServiceCoverageRatio = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    LoanToValueRatio = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    RepaymentCapacityRating = table.Column<int>(type: "int", nullable: false),
                    EquityAssessmentNote = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreditBureauSummary = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreditOfficerRecommendation = table.Column<int>(type: "int", nullable: false),
                    SummaryNotes = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampFinancialAppraisalReports", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampTechnicalAppraisalReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PreparedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SavedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FarmLocationDescription = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GpsCoordinates = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LandAreaAssessedHectares = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    SoilConditionRating = table.Column<int>(type: "int", nullable: false),
                    WaterSourceAvailability = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProposedEquipmentSuitability = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InfrastructureNotes = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RisksIdentified = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecommendedMitigations = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OverallViabilityRating = table.Column<int>(type: "int", nullable: false),
                    EngineerRecommendation = table.Column<int>(type: "int", nullable: false),
                    SummaryNotes = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampTechnicalAppraisalReports", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCheckOutbox_NampApplicationId",
                table: "CreditCheckOutbox",
                column: "NampApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_BureauReports_NampApplicationId",
                table: "BureauReports",
                column: "NampApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_NampFinancialAppraisalReports_NampApplicationId",
                table: "NampFinancialAppraisalReports",
                column: "NampApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NampTechnicalAppraisalReports_NampApplicationId",
                table: "NampTechnicalAppraisalReports",
                column: "NampApplicationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NampFinancialAppraisalReports");

            migrationBuilder.DropTable(
                name: "NampTechnicalAppraisalReports");

            migrationBuilder.DropIndex(
                name: "IX_CreditCheckOutbox_NampApplicationId",
                table: "CreditCheckOutbox");

            migrationBuilder.DropIndex(
                name: "IX_BureauReports_NampApplicationId",
                table: "BureauReports");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "NampDocuments");

            migrationBuilder.DropColumn(
                name: "NampApplicationId",
                table: "CreditCheckOutbox");

            migrationBuilder.DropColumn(
                name: "NampApplicationId",
                table: "BureauReports");
        }
    }
}
