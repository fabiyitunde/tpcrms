using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPreDeploymentGateChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EquityDepositConfirmed",
                table: "NampApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquityDepositNote",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "GpsConsentNote",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "GpsConsentObtained",
                table: "NampApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseDocumentsNote",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "LeaseDocumentsSigned",
                table: "NampApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NaicInsuranceInPlace",
                table: "NampApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NaicInsuranceNote",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquityDepositConfirmed",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "EquityDepositNote",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "GpsConsentNote",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "GpsConsentObtained",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "LeaseDocumentsNote",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "LeaseDocumentsSigned",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "NaicInsuranceInPlace",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "NaicInsuranceNote",
                table: "NampApplications");
        }
    }
}
