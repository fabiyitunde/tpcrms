using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfFinancialAppraisal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RhshfAppraisalThresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MinDscr = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    MinGrossMarginPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    HurdleRatePercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    MinYieldHeadroomPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    MinPriceHeadroomPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfAppraisalThresholds", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RhshfFarmPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RhshfCreditProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CycleNumber = table.Column<int>(type: "int", nullable: false),
                    Crop = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Hectares = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ExpectedYieldKgPerHectare = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ExpectedPricePerKg = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RecordedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfFarmPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RhshfFarmPlans_RhshfCreditProfiles_RhshfCreditProfileId",
                        column: x => x.RhshfCreditProfileId,
                        principalTable: "RhshfCreditProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RhshfFinancialAppraisalReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RhshfCreditProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CycleNumber = table.Column<int>(type: "int", nullable: false),
                    PreparedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SavedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OwnProductionCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HarvestAndLogisticsCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CycleMonths = table.Column<int>(type: "int", nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    AssumptionBasisNote = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GrossRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinancedInputCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OwnCashCosts = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InterestCharge = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AmountDueAtHarvest = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashAvailableForDebtService = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossMargin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossMarginPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    Dscr = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    NetReturnToFarmer = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Irr = table.Column<decimal>(type: "decimal(12,6)", nullable: true),
                    NetPresentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalHectares = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BlendedYieldKgPerHectare = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    BlendedPricePerKg = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BreakEvenYieldKgPerHectare = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    BreakEvenPricePerKg = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    YieldHeadroomPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    PriceHeadroomPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    ThresholdMinDscr = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ThresholdMinGrossMarginPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ThresholdHurdleRatePercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ThresholdMinYieldHeadroomPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ThresholdMinPriceHeadroomPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    DscrPass = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GrossMarginPass = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IrrPass = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    YieldHeadroomPass = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PriceHeadroomPass = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RepaymentCapacityRating = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreditOfficerRecommendation = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SummaryNotes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OverrideJustification = table.Column<string>(type: "longtext", nullable: true)
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
                    table.PrimaryKey("PK_RhshfFinancialAppraisalReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RhshfFinancialAppraisalReports_RhshfCreditProfiles_RhshfCred~",
                        column: x => x.RhshfCreditProfileId,
                        principalTable: "RhshfCreditProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfAppraisalThresholds_IsActive",
                table: "RhshfAppraisalThresholds",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfFarmPlans_RhshfCreditProfileId_CycleNumber",
                table: "RhshfFarmPlans",
                columns: new[] { "RhshfCreditProfileId", "CycleNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_RhshfFinancialAppraisalReports_RhshfCreditProfileId_CycleNum~",
                table: "RhshfFinancialAppraisalReports",
                columns: new[] { "RhshfCreditProfileId", "CycleNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RhshfAppraisalThresholds");

            migrationBuilder.DropTable(
                name: "RhshfFarmPlans");

            migrationBuilder.DropTable(
                name: "RhshfFinancialAppraisalReports");
        }
    }
}
