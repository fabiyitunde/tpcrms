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
            // ── DATA WIPES REMOVED (incident 2026-06-15) ──────────────────────
            // This migration originally ran row-deletion statements against
            // NampApplications, NampWorkflowConfigs, NampRoutingConfigs and
            // NampStagingRecords as a "pre-production test data" cleanup. That
            // assumption was wrong: it wiped live production data on deploy. Those
            // statements have been removed so this migration can never wipe data
            // again (e.g. on a fresh/restored environment not yet at this migration).
            //
            // POLICY: migrations are additive. They must not delete or truncate rows,
            // nor drop data-bearing tables. Genuine data cleanup is done via a
            // separate, reviewed, manually-run script against a backed-up environment.

            // ── Drop removed tables (no longer in the domain model) ───────────
            // These tables were never part of the live NAMP model; dropping them keeps
            // the schema in sync. They hold no production data.
            migrationBuilder.Sql("DROP TABLE IF EXISTS NampTechnicalAppraisalReports;"); // DESTRUCTIVE-APPROVED: removed table, no live data (incident 2026-06-15)
            migrationBuilder.Sql("DROP TABLE IF EXISTS NampViabilityScoreConfigs;");      // DESTRUCTIVE-APPROVED: removed table, no live data (incident 2026-06-15)

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
