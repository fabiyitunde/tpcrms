using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferLettersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the table first if it exists from a prior incomplete migration attempt
            migrationBuilder.Sql("DROP TABLE IF EXISTS `OfferLetters`;");

            migrationBuilder.CreateTable(
                name: "OfferLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    LoanApplicationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ApplicationNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    GeneratedByUserName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    StoragePath = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CustomerName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ProductName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedTenorMonths = table.Column<int>(type: "int", nullable: false),
                    ApprovedInterestRate = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    TotalInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRepayment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlyInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentCount = table.Column<int>(type: "int", nullable: false),
                    ExpectedDisbursementDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ScheduleSource = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfferLetters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfferLetters_LoanApplicationId",
                table: "OfferLetters",
                column: "LoanApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OfferLetters_LoanApplicationId_Version",
                table: "OfferLetters",
                columns: new[] { "LoanApplicationId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OfferLetters");
        }
    }
}
