using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStandingCommitteeLocationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StandingCommittees_CommitteeType",
                table: "StandingCommittees");

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "StandingCommittees",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_StandingCommittees_CommitteeType_LocationId",
                table: "StandingCommittees",
                columns: new[] { "CommitteeType", "LocationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StandingCommittees_CommitteeType_LocationId",
                table: "StandingCommittees");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "StandingCommittees");

            migrationBuilder.CreateIndex(
                name: "IX_StandingCommittees_CommitteeType",
                table: "StandingCommittees",
                column: "CommitteeType",
                unique: true);
        }
    }
}
