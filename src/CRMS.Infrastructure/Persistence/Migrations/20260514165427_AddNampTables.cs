using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NampApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApplicationNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicationReference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LoanProductId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApplicantName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BoaAccountNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicantPhone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicantEmail = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicantCategory = table.Column<int>(type: "int", nullable: false),
                    EquipmentDescription = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EquipmentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BranchId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OfficeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LocationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CommitteeTier = table.Column<int>(type: "int", nullable: false),
                    RecalledByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RecalledAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TechnicalAppraisalByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TechnicalAppraisalAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TechnicalAppraisalNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FinancialAppraisalByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FinancialAppraisalAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinancialAppraisalNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrentCommitteeReviewId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CommitteeDecisionAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CommitteeDecisionByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CommitteeDecisionNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RatifiedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RatifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RatificationDeclineNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OfferLetterStoragePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OfferGeneratedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OfferAcceptedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OfferAcceptedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    OfferLapsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PreDeploymentVerifiedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PreDeploymentVerifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PreDeploymentNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TrainingCompletedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TrainingCompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeployedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DeployedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GpsActivated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeploymentNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
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
                    table.PrimaryKey("PK_NampApplications", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampRoutingConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApplicantCategory = table.Column<int>(type: "int", nullable: false),
                    CommitteeTier = table.Column<int>(type: "int", nullable: false),
                    MinEquipmentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxEquipmentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_NampRoutingConfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampStagingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApplicationReference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawPayload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicantName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BoaAccountNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicantPhone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicantEmail = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicantCategory = table.Column<int>(type: "int", nullable: false),
                    EquipmentDescription = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EquipmentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ResolvedBranchId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ResolvedOfficeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ResolvedLocationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    BranchResolutionNote = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsRecalled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RecalledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RecalledByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ReceivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampStagingRecords", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampWorkflowConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignedRole = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SlaHours = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsTerminal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampWorkflowConfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampWorkflowInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false),
                    CurrentStageName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignedRole = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignedToUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EnteredCurrentStageAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SLADueAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsSLABreached = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinalStatus = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampWorkflowInstances", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UploadedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NampDocuments_NampApplications_NampApplicationId",
                        column: x => x.NampApplicationId,
                        principalTable: "NampApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
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
                    table.PrimaryKey("PK_NampStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NampStatusHistory_NampApplications_NampApplicationId",
                        column: x => x.NampApplicationId,
                        principalTable: "NampApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NampApplications_ApplicationNumber",
                table: "NampApplications",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NampApplications_ApplicationReference",
                table: "NampApplications",
                column: "ApplicationReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NampApplications_BranchId",
                table: "NampApplications",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_NampApplications_Status",
                table: "NampApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NampApplications_Status_BranchId",
                table: "NampApplications",
                columns: new[] { "Status", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_NampDocuments_NampApplicationId",
                table: "NampDocuments",
                column: "NampApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_NampDocuments_NampApplicationId_Stage",
                table: "NampDocuments",
                columns: new[] { "NampApplicationId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_NampRoutingConfigs_ApplicantCategory",
                table: "NampRoutingConfigs",
                column: "ApplicantCategory");

            migrationBuilder.CreateIndex(
                name: "IX_NampRoutingConfigs_ApplicantCategory_IsActive_Priority",
                table: "NampRoutingConfigs",
                columns: new[] { "ApplicantCategory", "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_NampRoutingConfigs_IsActive",
                table: "NampRoutingConfigs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_NampStagingRecords_ApplicationReference",
                table: "NampStagingRecords",
                column: "ApplicationReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NampStagingRecords_IsRecalled",
                table: "NampStagingRecords",
                column: "IsRecalled");

            migrationBuilder.CreateIndex(
                name: "IX_NampStagingRecords_ReceivedAt",
                table: "NampStagingRecords",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NampStagingRecords_ResolvedBranchId",
                table: "NampStagingRecords",
                column: "ResolvedBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_NampStatusHistory_NampApplicationId",
                table: "NampStatusHistory",
                column: "NampApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_NampStatusHistory_NampApplicationId_ChangedAt",
                table: "NampStatusHistory",
                columns: new[] { "NampApplicationId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NampWorkflowConfigs_SortOrder",
                table: "NampWorkflowConfigs",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_NampWorkflowConfigs_Status",
                table: "NampWorkflowConfigs",
                column: "Status",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NampWorkflowInstances_AssignedRole",
                table: "NampWorkflowInstances",
                column: "AssignedRole");

            migrationBuilder.CreateIndex(
                name: "IX_NampWorkflowInstances_IsCompleted_AssignedRole",
                table: "NampWorkflowInstances",
                columns: new[] { "IsCompleted", "AssignedRole" });

            migrationBuilder.CreateIndex(
                name: "IX_NampWorkflowInstances_IsSLABreached",
                table: "NampWorkflowInstances",
                column: "IsSLABreached");

            migrationBuilder.CreateIndex(
                name: "IX_NampWorkflowInstances_NampApplicationId",
                table: "NampWorkflowInstances",
                column: "NampApplicationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NampDocuments");

            migrationBuilder.DropTable(
                name: "NampRoutingConfigs");

            migrationBuilder.DropTable(
                name: "NampStagingRecords");

            migrationBuilder.DropTable(
                name: "NampStatusHistory");

            migrationBuilder.DropTable(
                name: "NampWorkflowConfigs");

            migrationBuilder.DropTable(
                name: "NampWorkflowInstances");

            migrationBuilder.DropTable(
                name: "NampApplications");
        }
    }
}
