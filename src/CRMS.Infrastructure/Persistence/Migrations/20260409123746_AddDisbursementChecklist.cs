using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisbursementChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OfferAcceptedAt",
                table: "LoanApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfferAcceptedByUserId",
                table: "LoanApplications",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferIssuedAt",
                table: "LoanApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OfferIssuedByUserId",
                table: "LoanApplications",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "DisbursementChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LoanApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TemplateItemId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ItemName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsMandatory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ConditionType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubsequentDueDays = table.Column<int>(type: "int", nullable: true),
                    RequiresDocumentUpload = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiresLegalRatification = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CanBeWaived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SatisfiedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SatisfiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EvidenceDocumentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LegalRatifiedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LegalRatifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LegalReturnReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WaiverProposedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    WaiverProposedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WaiverReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WaiverRatifiedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    WaiverRatifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WaiverRejectionReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DueDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExtensionRequestedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ExtensionRequestedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExtensionReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalDueDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExtensionRatifiedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ExtensionRatifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisbursementChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisbursementChecklistItems_LoanApplications_LoanApplicationId",
                        column: x => x.LoanApplicationId,
                        principalTable: "LoanApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DisbursementChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LoanProductId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ItemName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsMandatory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ConditionType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubsequentDueDays = table.Column<int>(type: "int", nullable: true),
                    RequiresDocumentUpload = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiresLegalRatification = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CanBeWaived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisbursementChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisbursementChecklistTemplates_LoanProducts_LoanProductId",
                        column: x => x.LoanProductId,
                        principalTable: "LoanProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementChecklistItems_LoanApplicationId",
                table: "DisbursementChecklistItems",
                column: "LoanApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementChecklistItems_LoanApplicationId_Status",
                table: "DisbursementChecklistItems",
                columns: new[] { "LoanApplicationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementChecklistTemplates_LoanProductId",
                table: "DisbursementChecklistTemplates",
                column: "LoanProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisbursementChecklistItems");

            migrationBuilder.DropTable(
                name: "DisbursementChecklistTemplates");

            migrationBuilder.DropColumn(
                name: "OfferAcceptedAt",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "OfferAcceptedByUserId",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "OfferIssuedAt",
                table: "LoanApplications");

            migrationBuilder.DropColumn(
                name: "OfferIssuedByUserId",
                table: "LoanApplications");
        }
    }
}
