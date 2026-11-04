using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfEopAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmendmentRestartStage",
                table: "LoanProducts",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RhshfEopAmendments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RhshfCreditProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SubmissionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CycleNumber = table.Column<int>(type: "int", nullable: false),
                    ReplacedCycleNumber = table.Column<int>(type: "int", nullable: false),
                    AmendedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PreviousTotalEopValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewTotalEopValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ChangesJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreviousBreakdownJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawPayload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfEopAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RhshfEopAmendments_RhshfCreditProfiles_RhshfCreditProfileId",
                        column: x => x.RhshfCreditProfileId,
                        principalTable: "RhshfCreditProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfEopAmendments_RhshfCreditProfileId",
                table: "RhshfEopAmendments",
                column: "RhshfCreditProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfEopAmendments_SubmissionId",
                table: "RhshfEopAmendments",
                column: "SubmissionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RhshfEopAmendments");

            migrationBuilder.DropColumn(
                name: "AmendmentRestartStage",
                table: "LoanProducts");
        }
    }
}
