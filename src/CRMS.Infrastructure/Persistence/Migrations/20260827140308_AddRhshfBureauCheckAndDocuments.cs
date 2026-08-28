using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfBureauCheckAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BureauActiveLoans",
                table: "RhshfCreditProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BureauCheckOutcome",
                table: "RhshfCreditProfiles",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotRun")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "BureauCheckedAt",
                table: "RhshfCreditProfiles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BureauDelinquentFacilities",
                table: "RhshfCreditProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BureauRawJson",
                table: "RhshfCreditProfiles",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "BureauTotalLoans",
                table: "RhshfCreditProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BureauTotalOutstanding",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BureauTotalOverdue",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RhshfSupportingDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RhshfCreditProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoragePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfSupportingDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RhshfSupportingDocuments_RhshfCreditProfiles_RhshfCreditProf~",
                        column: x => x.RhshfCreditProfileId,
                        principalTable: "RhshfCreditProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfSupportingDocuments_RhshfCreditProfileId",
                table: "RhshfSupportingDocuments",
                column: "RhshfCreditProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "BureauActiveLoans",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "BureauCheckOutcome",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "BureauCheckedAt",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "BureauDelinquentFacilities",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "BureauRawJson",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "BureauTotalLoans",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "BureauTotalOutstanding",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "BureauTotalOverdue",
                table: "RhshfCreditProfiles");
        }
    }
}
