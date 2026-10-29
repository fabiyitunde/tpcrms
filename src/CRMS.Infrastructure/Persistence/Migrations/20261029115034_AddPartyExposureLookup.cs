using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyExposureLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartyExposureBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequestId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedByUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedByRole = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CallbackUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubjectCount = table.Column<int>(type: "int", nullable: false),
                    ProcessedCount = table.Column<int>(type: "int", nullable: false),
                    ResultsJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Failure = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CallbackStatusCode = table.Column<int>(type: "int", nullable: true),
                    CallbackSentAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyExposureBatches", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PartyExposureLookupLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequestId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedByUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedByRole = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubjectRef = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BatchId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    BvnHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NinHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NubanHashes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MatchStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RelationshipCount = table.Column<int>(type: "int", nullable: false),
                    DirectFacilities = table.Column<int>(type: "int", nullable: false),
                    IndirectFacilities = table.Column<int>(type: "int", nullable: false),
                    Truncated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IdentityConflict = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
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
                    table.PrimaryKey("PK_PartyExposureLookupLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfGuarantors_Bvn",
                table: "RhshfGuarantors",
                column: "Bvn");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfCreditProfiles_BoaAccountNumber",
                table: "RhshfCreditProfiles",
                column: "BoaAccountNumber");

            migrationBuilder.CreateIndex(
                name: "IX_NampGuarantors_Bvn",
                table: "NampGuarantors",
                column: "Bvn");

            migrationBuilder.CreateIndex(
                name: "IX_NampGuarantors_Nin",
                table: "NampGuarantors",
                column: "Nin");

            migrationBuilder.CreateIndex(
                name: "IX_NampDirectors_Bvn",
                table: "NampDirectors",
                column: "Bvn");

            migrationBuilder.CreateIndex(
                name: "IX_NampApplications_BoaAccountNumber",
                table: "NampApplications",
                column: "BoaAccountNumber");

            migrationBuilder.CreateIndex(
                name: "IX_NampApplications_Bvn",
                table: "NampApplications",
                column: "Bvn");

            migrationBuilder.CreateIndex(
                name: "IX_NampApplications_Nin",
                table: "NampApplications",
                column: "Nin");

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplicationParties_BVN",
                table: "LoanApplicationParties",
                column: "BVN");

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_NIN",
                table: "Guarantors",
                column: "NIN");

            migrationBuilder.CreateIndex(
                name: "IX_PartyExposureBatches_RequestId",
                table: "PartyExposureBatches",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyExposureBatches_Status",
                table: "PartyExposureBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PartyExposureLookupLogs_BatchId",
                table: "PartyExposureLookupLogs",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PartyExposureLookupLogs_BvnHash",
                table: "PartyExposureLookupLogs",
                column: "BvnHash");

            migrationBuilder.CreateIndex(
                name: "IX_PartyExposureLookupLogs_CreatedAt",
                table: "PartyExposureLookupLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PartyExposureLookupLogs_RequestId",
                table: "PartyExposureLookupLogs",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartyExposureBatches");

            migrationBuilder.DropTable(
                name: "PartyExposureLookupLogs");

            migrationBuilder.DropIndex(
                name: "IX_RhshfGuarantors_Bvn",
                table: "RhshfGuarantors");

            migrationBuilder.DropIndex(
                name: "IX_RhshfCreditProfiles_BoaAccountNumber",
                table: "RhshfCreditProfiles");

            migrationBuilder.DropIndex(
                name: "IX_NampGuarantors_Bvn",
                table: "NampGuarantors");

            migrationBuilder.DropIndex(
                name: "IX_NampGuarantors_Nin",
                table: "NampGuarantors");

            migrationBuilder.DropIndex(
                name: "IX_NampDirectors_Bvn",
                table: "NampDirectors");

            migrationBuilder.DropIndex(
                name: "IX_NampApplications_BoaAccountNumber",
                table: "NampApplications");

            migrationBuilder.DropIndex(
                name: "IX_NampApplications_Bvn",
                table: "NampApplications");

            migrationBuilder.DropIndex(
                name: "IX_NampApplications_Nin",
                table: "NampApplications");

            migrationBuilder.DropIndex(
                name: "IX_LoanApplicationParties_BVN",
                table: "LoanApplicationParties");

            migrationBuilder.DropIndex(
                name: "IX_Guarantors_NIN",
                table: "Guarantors");
        }
    }
}
