using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditAppraisalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditAppraisalDscr",
                table: "LoanApplications",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditAppraisalLeverage",
                table: "LoanApplications",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditAppraisalCurrentRatio",
                table: "LoanApplications",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditAppraisalLtv",
                table: "LoanApplications",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreditAppraisalCapacityRating",
                table: "LoanApplications",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CreditAppraisalRecommendation",
                table: "LoanApplications",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CreditAppraisalNotes",
                table: "LoanApplications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CreditAppraisalMemoPath",
                table: "LoanApplications",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CreditAppraisalMemoFileName",
                table: "LoanApplications",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreditAppraisalSavedAt",
                table: "LoanApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreditAppraisalSavedByUserId",
                table: "LoanApplications",
                type: "char(36)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreditAppraisalDscr", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalLeverage", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalCurrentRatio", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalLtv", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalCapacityRating", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalRecommendation", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalNotes", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalMemoPath", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalMemoFileName", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalSavedAt", table: "LoanApplications");
            migrationBuilder.DropColumn(name: "CreditAppraisalSavedByUserId", table: "LoanApplications");
        }
    }
}
