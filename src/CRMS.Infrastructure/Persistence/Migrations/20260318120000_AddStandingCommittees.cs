using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    public partial class AddStandingCommittees : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StandingCommittees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    CommitteeType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    RequiredVotes = table.Column<int>(type: "int", nullable: false),
                    MinimumApprovalVotes = table.Column<int>(type: "int", nullable: false),
                    DefaultDeadlineHours = table.Column<int>(type: "int", nullable: false),
                    MinAmountThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxAmountThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingCommittees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StandingCommitteeMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    StandingCommitteeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    IsChairperson = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingCommitteeMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandingCommitteeMembers_StandingCommittees_StandingCommitte~",
                        column: x => x.StandingCommitteeId,
                        principalTable: "StandingCommittees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StandingCommittees_CommitteeType",
                table: "StandingCommittees",
                column: "CommitteeType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandingCommittees_IsActive",
                table: "StandingCommittees",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StandingCommitteeMembers_StandingCommitteeId_UserId",
                table: "StandingCommitteeMembers",
                columns: new[] { "StandingCommitteeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandingCommitteeMembers_UserId",
                table: "StandingCommitteeMembers",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "StandingCommitteeMembers");
            migrationBuilder.DropTable(name: "StandingCommittees");
        }
    }
}
