using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampFineractDisbursedOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FineractDisbursedOn",
                table: "NampApplications",
                type: "datetime(6)",
                nullable: true);

            // Backfill. Before this column existed, a loan id could only ever be stored after a fully
            // successful booking (create + approve + disburse), so every existing linked application is
            // disbursed. Without this, those rows would read as "created but not disbursed" on the Loan
            // Account tab and their booking retry would reopen. Deployment date is the closest true
            // disbursement moment we hold; ModifiedAt/CreatedAt are fallbacks for older rows.
            // Fills the new column only — never touches existing data.
            migrationBuilder.Sql("""
                UPDATE NampApplications
                SET FineractDisbursedOn = COALESCE(DeployedAt, ModifiedAt, CreatedAt)
                WHERE FineractLoanId IS NOT NULL AND FineractDisbursedOn IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FineractDisbursedOn",
                table: "NampApplications");
        }
    }
}
