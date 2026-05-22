using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyTechAppraisalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "WaterSourceAvailability",
                table: "NampTechnicalAppraisalReports",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "SoilConditionRating",
                table: "NampTechnicalAppraisalReports",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "FarmLocationDescription",
                table: "NampTechnicalAppraisalReports",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CurrentServiceContracts",
                table: "NampTechnicalAppraisalReports",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ExistingFleetSize",
                table: "NampTechnicalAppraisalReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasMaintenanceFacility",
                table: "NampTechnicalAppraisalReports",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationalCoverageArea",
                table: "NampTechnicalAppraisalReports",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StorageFacilityDescription",
                table: "NampTechnicalAppraisalReports",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "TechnicalStaffCount",
                table: "NampTechnicalAppraisalReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsInOperation",
                table: "NampTechnicalAppraisalReports",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentServiceContracts",
                table: "NampTechnicalAppraisalReports");

            migrationBuilder.DropColumn(
                name: "ExistingFleetSize",
                table: "NampTechnicalAppraisalReports");

            migrationBuilder.DropColumn(
                name: "HasMaintenanceFacility",
                table: "NampTechnicalAppraisalReports");

            migrationBuilder.DropColumn(
                name: "OperationalCoverageArea",
                table: "NampTechnicalAppraisalReports");

            migrationBuilder.DropColumn(
                name: "StorageFacilityDescription",
                table: "NampTechnicalAppraisalReports");

            migrationBuilder.DropColumn(
                name: "TechnicalStaffCount",
                table: "NampTechnicalAppraisalReports");

            migrationBuilder.DropColumn(
                name: "YearsInOperation",
                table: "NampTechnicalAppraisalReports");

            migrationBuilder.UpdateData(
                table: "NampTechnicalAppraisalReports",
                keyColumn: "WaterSourceAvailability",
                keyValue: null,
                column: "WaterSourceAvailability",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "WaterSourceAvailability",
                table: "NampTechnicalAppraisalReports",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "SoilConditionRating",
                table: "NampTechnicalAppraisalReports",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "NampTechnicalAppraisalReports",
                keyColumn: "FarmLocationDescription",
                keyValue: null,
                column: "FarmLocationDescription",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "FarmLocationDescription",
                table: "NampTechnicalAppraisalReports",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
