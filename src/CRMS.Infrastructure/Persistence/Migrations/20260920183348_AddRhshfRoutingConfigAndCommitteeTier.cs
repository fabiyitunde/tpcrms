using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfRoutingConfigAndCommitteeTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "RhshfCommitteeReviews",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "RhshfCommitteeReviews",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Any pre-existing rows got the column default (""), which doesn't round-trip through
            // the enum string conversion — backfill to a valid tier so old rows stay readable.
            migrationBuilder.Sql("UPDATE `RhshfCommitteeReviews` SET `Tier` = 'BranchCredit' WHERE `Tier` = '';");

            migrationBuilder.CreateTable(
                name: "RhshfRoutingConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Tier = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MinEopValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxEopValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfRoutingConfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfRoutingConfigs_IsActive",
                table: "RhshfRoutingConfigs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfRoutingConfigs_IsActive_Priority",
                table: "RhshfRoutingConfigs",
                columns: new[] { "IsActive", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RhshfRoutingConfigs");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "RhshfCommitteeReviews");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "RhshfCommitteeReviews");
        }
    }
}
