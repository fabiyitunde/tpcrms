using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalOverrideRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalOverrideRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LoanApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Stage = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActorId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ActorName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NoteText = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsResolved = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolvedByName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
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
                    table.PrimaryKey("PK_ApprovalOverrideRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalOverrideRecords_LoanApplications_LoanApplicationId",
                        column: x => x.LoanApplicationId,
                        principalTable: "LoanApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalOverrideRecords_LoanApplicationId",
                table: "ApprovalOverrideRecords",
                column: "LoanApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalOverrideRecords");
        }
    }
}
