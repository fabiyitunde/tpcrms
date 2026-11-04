using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfEopIdIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RhshfFinancingLines_EopId",
                table: "RhshfFinancingLines",
                column: "EopId");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfFinancingEops_EopId",
                table: "RhshfFinancingEops",
                column: "EopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RhshfFinancingLines_EopId",
                table: "RhshfFinancingLines");

            migrationBuilder.DropIndex(
                name: "IX_RhshfFinancingEops_EopId",
                table: "RhshfFinancingEops");
        }
    }
}
