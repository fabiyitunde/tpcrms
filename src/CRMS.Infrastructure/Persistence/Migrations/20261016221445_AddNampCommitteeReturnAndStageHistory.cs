using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampCommitteeReturnAndStageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommitteeReturnNote",
                table: "NampApplications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CommitteeReturnedAt",
                table: "NampApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommitteeReturnedByUserId",
                table: "NampApplications",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "ReviewPassNumber",
                table: "NampApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "NampFinancialAppraisalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PassNumber = table.Column<int>(type: "int", nullable: false),
                    AppraisedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AppraisedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsApproved = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampFinancialAppraisalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NampFinancialAppraisalRecords_NampApplications_NampApplicati~",
                        column: x => x.NampApplicationId,
                        principalTable: "NampApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampRiskReviewRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PassNumber = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Outcome = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampRiskReviewRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NampRiskReviewRecords_NampApplications_NampApplicationId",
                        column: x => x.NampApplicationId,
                        principalTable: "NampApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NampFinancialAppraisalRecords_NampApplicationId",
                table: "NampFinancialAppraisalRecords",
                column: "NampApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_NampFinancialAppraisalRecords_NampApplicationId_PassNumber",
                table: "NampFinancialAppraisalRecords",
                columns: new[] { "NampApplicationId", "PassNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_NampRiskReviewRecords_NampApplicationId",
                table: "NampRiskReviewRecords",
                column: "NampApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_NampRiskReviewRecords_NampApplicationId_PassNumber",
                table: "NampRiskReviewRecords",
                columns: new[] { "NampApplicationId", "PassNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NampFinancialAppraisalRecords");

            migrationBuilder.DropTable(
                name: "NampRiskReviewRecords");

            migrationBuilder.DropColumn(
                name: "CommitteeReturnNote",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "CommitteeReturnedAt",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "CommitteeReturnedByUserId",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "ReviewPassNumber",
                table: "NampApplications");
        }
    }
}
