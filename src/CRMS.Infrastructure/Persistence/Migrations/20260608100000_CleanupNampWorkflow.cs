using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CleanupNampWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Wipe test data (pre-production) ──────────────────────────────
            // Delete all NAMP applications (cascades to Documents, StatusHistory,
            // Guarantors, Collaterals, FinancialStatements, PreDeploymentChecklist,
            // WorkflowInstances, Advisories, FinancialAppraisalReports).
            migrationBuilder.Sql("DELETE FROM NampApplications;");

            // Delete workflow configs so they will be re-seeded with updated stages.
            migrationBuilder.Sql("DELETE FROM NampWorkflowConfigs;");

            // Delete routing configs so they will be re-seeded.
            migrationBuilder.Sql("DELETE FROM NampRoutingConfigs;");

            // Delete staging queue (all pre-production test records).
            migrationBuilder.Sql("DELETE FROM NampStagingRecords;");

            // ── Drop removed tables ───────────────────────────────────────────
            migrationBuilder.Sql("DROP TABLE IF EXISTS NampTechnicalAppraisalReports;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS NampViabilityScoreConfigs;");

            // ── Drop removed columns from NampApplications ───────────────────
            migrationBuilder.DropColumn(
                name: "TechnicalAppraisalByUserId",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "TechnicalAppraisalAt",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "TechnicalAppraisalNote",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "TrainingCompletedByUserId",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "TrainingCompletedAt",
                table: "NampApplications");

            // ── Convert Status columns from INT to VARCHAR(50) ────────────────
            // NampApplications.Status
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NampApplications",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            // NampStatusHistory.Status
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NampStatusHistory",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            // NampWorkflowConfigs.Status
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "NampWorkflowConfigs",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down migration is intentionally minimal — this is a destructive cleanup
            // for a pre-production test environment. Re-adding the dropped tables and
            // columns would require restoring data that has been wiped.

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "NampWorkflowConfigs",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "NampStatusHistory",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "NampApplications",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);
        }
    }
}
