using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfItemisedFinancing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedEopTotal",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExcludedFromLoan",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancedLoanAmount",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancingChargesTotal",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancingInputsTotal",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancingPackagingExcessTotal",
                table: "RhshfCreditProfiles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RhshfFinancingCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RhshfCreditProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Label = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RateBasis = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InLoan = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfFinancingCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RhshfFinancingCharges_RhshfCreditProfiles_RhshfCreditProfile~",
                        column: x => x.RhshfCreditProfileId,
                        principalTable: "RhshfCreditProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RhshfFinancingLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RhshfCreditProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EopId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Crop = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Item = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpecificInput = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Supplier = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QuantityPerFarmer = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    Unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Basis = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Farmers = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PackagingExcess = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InLoan = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfFinancingLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RhshfFinancingLines_RhshfCreditProfiles_RhshfCreditProfileId",
                        column: x => x.RhshfCreditProfileId,
                        principalTable: "RhshfCreditProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfFinancingCharges_RhshfCreditProfileId",
                table: "RhshfFinancingCharges",
                column: "RhshfCreditProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfFinancingLines_RhshfCreditProfileId",
                table: "RhshfFinancingLines",
                column: "RhshfCreditProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RhshfFinancingCharges");

            migrationBuilder.DropTable(
                name: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ApprovedEopTotal",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "ExcludedFromLoan",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "FinancedLoanAmount",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "FinancingChargesTotal",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "FinancingInputsTotal",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropColumn(
                name: "FinancingPackagingExcessTotal",
                table: "RhshfCreditProfiles");
        }
    }
}
