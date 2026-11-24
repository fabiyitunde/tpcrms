using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyCreditChecksForPartners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                table: "CustomerCheckLogs",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "INDIVIDUAL")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                table: "CustomerCheckJobs",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "INDIVIDUAL")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "AllowCompanyChecks",
                table: "ApiConsumers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubjectType",
                table: "CustomerCheckLogs");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                table: "CustomerCheckJobs");

            migrationBuilder.DropColumn(
                name: "AllowCompanyChecks",
                table: "ApiConsumers");
        }
    }
}
