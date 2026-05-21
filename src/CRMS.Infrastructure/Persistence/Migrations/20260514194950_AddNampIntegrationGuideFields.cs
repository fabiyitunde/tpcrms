using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampIntegrationGuideFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clean up any columns partially added by a previous failed migration attempt.
            // Uses a stored procedure + information_schema check since MySQL lacks DROP COLUMN IF EXISTS.
            migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS `crms_drop_col_if_exists`;
CREATE PROCEDURE `crms_drop_col_if_exists`(IN tbl VARCHAR(128), IN col VARCHAR(128))
BEGIN
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = tbl AND COLUMN_NAME = col
    ) THEN
        SET @_ddl = CONCAT('ALTER TABLE `', tbl, '` DROP COLUMN `', col, '`');
        PREPARE _s FROM @_ddl;
        EXECUTE _s;
        DEALLOCATE PREPARE _s;
    END IF;
END;", suppressTransaction: true);

            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampStagingRecords', 'CrmsApplicationNumber')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'BoaAccountName')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'Bvn')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'CartTotal')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'CompanyName')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'DateOfBirth')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'EmployerName')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'EmploymentStatus')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'EquipmentCartJson')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'EquityAmount')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'EquityPercent')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'EstimatedMonthlyIncome')", suppressTransaction: true);
            migrationBuilder.Sql("CALL `crms_drop_col_if_exists`('NampApplications', 'EstimatedNetWorth')", suppressTransaction: true);
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `crms_drop_col_if_exists`", suppressTransaction: true);

            migrationBuilder.AddColumn<string>(
                name: "CrmsApplicationNumber",
                table: "NampStagingRecords",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalAppraisalNote",
                table: "NampApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "RatificationDeclineNote",
                table: "NampApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PreDeploymentNote",
                table: "NampApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "FinancialAppraisalNote",
                table: "NampApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DeploymentNote",
                table: "NampApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CommitteeDecisionNote",
                table: "NampApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BoaAccountName",
                table: "NampApplications",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Bvn",
                table: "NampApplications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "CartTotal",
                table: "NampApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "NampApplications",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DateOfBirth",
                table: "NampApplications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EmployerName",
                table: "NampApplications",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EmploymentStatus",
                table: "NampApplications",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EquipmentCartJson",
                table: "NampApplications",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "EquityAmount",
                table: "NampApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EquityPercent",
                table: "NampApplications",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedMonthlyIncome",
                table: "NampApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedNetWorth",
                table: "NampApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExistingLoanObligations",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IndustrySector",
                table: "NampApplications",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "LoanAmount",
                table: "NampApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoanPurpose",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LocalGovernmentArea",
                table: "NampApplications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyLivingExpenses",
                table: "NampApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nin",
                table: "NampApplications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "NumberOfDependants",
                table: "NampApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "NampApplications",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OtherIncomeSource",
                table: "NampApplications",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OtherMonthlyIncome",
                table: "NampApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnedAssetsDescription",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RcNumber",
                table: "NampApplications",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "RequestedTenorMonths",
                table: "NampApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateOfResidence",
                table: "NampApplications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "NampApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NampCollaterals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CollateralType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssetIdentifier = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Location = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OwnerName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OwnershipType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MarketValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ForcedSaleValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LienType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LienReference = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LienRegistrationAuthority = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsInsured = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    InsurancePolicyNumber = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InsuranceCompany = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InsuredValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InsuranceExpiryDate = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
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
                    table.PrimaryKey("PK_NampCollaterals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NampCollaterals_NampApplications_NampApplicationId",
                        column: x => x.NampApplicationId,
                        principalTable: "NampApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampFinancialStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FinancialYear = table.Column<int>(type: "int", nullable: false),
                    YearEndDate = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FinancialYearType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InputMethod = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Currency = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Revenue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherOperatingIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CostOfSales = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GrossProfit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SellingExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AdministrativeExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DepreciationAmortization = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherOperatingExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OperatingExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Ebitda = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InterestIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InterestExpense = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherFinanceCosts = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IncomeTaxExpense = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DividendsDeclared = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetProfit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CashAndCashEquivalents = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TradeReceivables = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Inventory = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PrepaidExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherCurrentAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCurrentAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PropertyPlantEquipment = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IntangibleAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LongTermInvestments = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DeferredTaxAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherNonCurrentAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalNonCurrentAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalAssets = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TradePayables = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ShortTermBorrowings = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrentPortionLongTermDebt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AccruedExpenses = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxPayable = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherCurrentLiabilities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCurrentLiabilities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LongTermDebt = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DeferredTaxLiabilities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Provisions = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherNonCurrentLiabilities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalNonCurrentLiabilities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalLiabilities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ShareCapital = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SharePremium = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RetainedEarnings = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherReserves = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalEquity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfProfitBeforeTax = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfDepreciationAmortization = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfInterestExpenseAddBack = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfChangesInWorkingCapital = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfTaxPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfOtherOperatingAdjustments = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetCashFromOperating = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfPurchaseOfPpe = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfSaleOfPpe = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfPurchaseOfInvestments = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfSaleOfInvestments = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfInterestReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfDividendsReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfOtherInvestingActivities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetCashFromInvesting = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfProceedsFromBorrowings = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfRepaymentOfBorrowings = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfInterestPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfDividendsPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfProceedsFromShareIssue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfOtherFinancingActivities = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetCashFromFinancing = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NetChangeInCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CfOpeningCashBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ClosingCashBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AuditorName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuditorFirm = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuditDate = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuditOpinion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
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
                    table.PrimaryKey("PK_NampFinancialStatements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NampFinancialStatements_NampApplications_NampApplicationId",
                        column: x => x.NampApplicationId,
                        principalTable: "NampApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampGuarantors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FullName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bvn = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nin = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DateOfBirth = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompanyName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompanyRegistrationNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GuarantorType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GuaranteeType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelationshipToApplicant = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDirector = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsShareholder = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ShareholdingPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DeclaredNetWorth = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Occupation = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployerName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MonthlyIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    GuaranteeLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsUnlimited = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampGuarantors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NampGuarantors_NampApplications_NampApplicationId",
                        column: x => x.NampApplicationId,
                        principalTable: "NampApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NampCollaterals_NampApplicationId",
                table: "NampCollaterals",
                column: "NampApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_NampFinancialStatements_NampApplicationId",
                table: "NampFinancialStatements",
                column: "NampApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_NampFinancialStatements_NampApplicationId_FinancialYear",
                table: "NampFinancialStatements",
                columns: new[] { "NampApplicationId", "FinancialYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NampGuarantors_NampApplicationId",
                table: "NampGuarantors",
                column: "NampApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NampCollaterals");

            migrationBuilder.DropTable(
                name: "NampFinancialStatements");

            migrationBuilder.DropTable(
                name: "NampGuarantors");

            migrationBuilder.DropColumn(
                name: "CrmsApplicationNumber",
                table: "NampStagingRecords");

            migrationBuilder.DropColumn(
                name: "BoaAccountName",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "Bvn",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "CartTotal",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "EmployerName",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "EmploymentStatus",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "EquipmentCartJson",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "EquityAmount",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "EquityPercent",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "EstimatedMonthlyIncome",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "EstimatedNetWorth",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "ExistingLoanObligations",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "IndustrySector",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "LoanAmount",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "LoanPurpose",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "LocalGovernmentArea",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "MonthlyLivingExpenses",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "Nin",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "NumberOfDependants",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "OtherIncomeSource",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "OtherMonthlyIncome",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "OwnedAssetsDescription",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "RcNumber",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "RequestedTenorMonths",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "StateOfResidence",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "NampApplications");

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalAppraisalNote",
                table: "NampApplications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "RatificationDeclineNote",
                table: "NampApplications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "PreDeploymentNote",
                table: "NampApplications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "FinancialAppraisalNote",
                table: "NampApplications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "DeploymentNote",
                table: "NampApplications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "CommitteeDecisionNote",
                table: "NampApplications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
