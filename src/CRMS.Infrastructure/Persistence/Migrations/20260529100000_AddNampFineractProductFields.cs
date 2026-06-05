using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampFineractProductFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FineractProductId",
                table: "NampApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FineractProductName",
                table: "NampApplications",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "FineractNominalInterestRate",
                table: "NampApplications",
                type: "decimal(10,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FineractProductId",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "FineractProductName",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "FineractNominalInterestRate",
                table: "NampApplications");
        }
    }
}
