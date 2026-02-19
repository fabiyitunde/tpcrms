using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Guarantors",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "Guarantors",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<bool>(
                name: "CalculatedRatios_IsDSCREstimated",
                table: "FinancialStatements",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Collaterals",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "Collaterals",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "CalculatedRatios_IsDSCREstimated",
                table: "FinancialStatements");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Collaterals");
        }
    }
}
