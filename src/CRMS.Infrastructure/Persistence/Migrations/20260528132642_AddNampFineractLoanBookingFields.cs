using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampFineractLoanBookingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedInterestRate",
                table: "NampApplications",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FineractClientId",
                table: "NampApplications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FineractLoanAccountNumber",
                table: "NampApplications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "FineractLoanId",
                table: "NampApplications",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedInterestRate",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "FineractClientId",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "FineractLoanAccountNumber",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "FineractLoanId",
                table: "NampApplications");
        }
    }
}
