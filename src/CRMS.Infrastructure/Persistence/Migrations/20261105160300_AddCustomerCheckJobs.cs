using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCheckJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdvisoryCalls",
                table: "CustomerCheckLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BureauCalls",
                table: "CustomerCheckLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FraudCalls",
                table: "CustomerCheckLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdentityCalls",
                table: "CustomerCheckLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "JobId",
                table: "CustomerCheckLogs",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "ScoreCalls",
                table: "CustomerCheckLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AllowedCallbackHosts",
                table: "ApiConsumers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CallbackSecretCiphertext",
                table: "ApiConsumers",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CallbackSecretPrefix",
                table: "ApiConsumers",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CallbackSecretRotatedAt",
                table: "ApiConsumers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallbackUrl",
                table: "ApiConsumers",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "MaxQueuedJobs",
                table: "ApiConsumers",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.CreateTable(
                name: "CustomerCheckJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConsumerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConsumerName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BvnHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BvnLast4 = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SectionsRequested = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestCiphertext = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CallbackUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CallbackStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EventId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RunAttempts = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    ResultCiphertext = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SectionOutcomes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BureauUsed = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BureauReused = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BilledCalls = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CallbackAttempts = table.Column<int>(type: "int", nullable: false),
                    NextCallbackAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CallbackDeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastCallbackStatusCode = table.Column<int>(type: "int", nullable: true),
                    LastCallbackError = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdvisoryScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AdvisoryRating = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdvisoryRecommendation = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
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
                    table.PrimaryKey("PK_CustomerCheckJobs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCheckLogs_JobId",
                table: "CustomerCheckLogs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCheckJobs_BvnHash",
                table: "CustomerCheckJobs",
                column: "BvnHash");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCheckJobs_CallbackStatus_NextCallbackAt",
                table: "CustomerCheckJobs",
                columns: new[] { "CallbackStatus", "NextCallbackAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCheckJobs_ConsumerId_CreatedAt",
                table: "CustomerCheckJobs",
                columns: new[] { "ConsumerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCheckJobs_ConsumerId_RequestId",
                table: "CustomerCheckJobs",
                columns: new[] { "ConsumerId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCheckJobs_Status_CreatedAt",
                table: "CustomerCheckJobs",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerCheckJobs");

            migrationBuilder.DropIndex(
                name: "IX_CustomerCheckLogs_JobId",
                table: "CustomerCheckLogs");

            migrationBuilder.DropColumn(
                name: "AdvisoryCalls",
                table: "CustomerCheckLogs");

            migrationBuilder.DropColumn(
                name: "BureauCalls",
                table: "CustomerCheckLogs");

            migrationBuilder.DropColumn(
                name: "FraudCalls",
                table: "CustomerCheckLogs");

            migrationBuilder.DropColumn(
                name: "IdentityCalls",
                table: "CustomerCheckLogs");

            migrationBuilder.DropColumn(
                name: "JobId",
                table: "CustomerCheckLogs");

            migrationBuilder.DropColumn(
                name: "ScoreCalls",
                table: "CustomerCheckLogs");

            migrationBuilder.DropColumn(
                name: "AllowedCallbackHosts",
                table: "ApiConsumers");

            migrationBuilder.DropColumn(
                name: "CallbackSecretCiphertext",
                table: "ApiConsumers");

            migrationBuilder.DropColumn(
                name: "CallbackSecretPrefix",
                table: "ApiConsumers");

            migrationBuilder.DropColumn(
                name: "CallbackSecretRotatedAt",
                table: "ApiConsumers");

            migrationBuilder.DropColumn(
                name: "CallbackUrl",
                table: "ApiConsumers");

            migrationBuilder.DropColumn(
                name: "MaxQueuedJobs",
                table: "ApiConsumers");
        }
    }
}
