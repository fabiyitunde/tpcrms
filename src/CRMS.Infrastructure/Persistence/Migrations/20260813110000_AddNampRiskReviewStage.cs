using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampRiskReviewStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RiskReviewedByUserId",
                table: "NampApplications",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RiskReviewedAt",
                table: "NampApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskReviewNote",
                table: "NampApplications",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskReturnNote",
                table: "NampApplications",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskDeclineNote",
                table: "NampApplications",
                type: "longtext",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RiskReviewedByUserId", table: "NampApplications");
            migrationBuilder.DropColumn(name: "RiskReviewedAt",       table: "NampApplications");
            migrationBuilder.DropColumn(name: "RiskReviewNote",       table: "NampApplications");
            migrationBuilder.DropColumn(name: "RiskReturnNote",       table: "NampApplications");
            migrationBuilder.DropColumn(name: "RiskDeclineNote",      table: "NampApplications");
        }
    }
}
