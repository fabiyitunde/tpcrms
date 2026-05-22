using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampPreDeploymentChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquityDepositConfirmed",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "EquityDepositNote",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "GpsConsentNote",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "GpsConsentObtained",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "LeaseDocumentsNote",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "LeaseDocumentsSigned",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "NaicInsuranceInPlace",
                table: "NampApplications");

            migrationBuilder.DropColumn(
                name: "NaicInsuranceNote",
                table: "NampApplications");

            migrationBuilder.CreateTable(
                name: "NampPreDeploymentChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TemplateItemId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequiresDocumentUpload = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DocumentCategory = table.Column<int>(type: "int", nullable: true),
                    IsMandatory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
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
                    table.PrimaryKey("PK_NampPreDeploymentChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NampPreDeploymentChecklistItems_NampApplications_NampApplica~",
                        column: x => x.NampApplicationId,
                        principalTable: "NampApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NampPreDeploymentChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequiresDocumentUpload = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DocumentCategory = table.Column<int>(type: "int", nullable: true),
                    IsMandatory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_NampPreDeploymentChecklistTemplates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NampPreDeploymentChecklistItems_NampApplicationId",
                table: "NampPreDeploymentChecklistItems",
                column: "NampApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_NampPreDeploymentChecklistItems_NampApplicationId_TemplateIt~",
                table: "NampPreDeploymentChecklistItems",
                columns: new[] { "NampApplicationId", "TemplateItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NampPreDeploymentChecklistTemplates_IsActive",
                table: "NampPreDeploymentChecklistTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_NampPreDeploymentChecklistTemplates_SortOrder",
                table: "NampPreDeploymentChecklistTemplates",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NampPreDeploymentChecklistItems");

            migrationBuilder.DropTable(
                name: "NampPreDeploymentChecklistTemplates");

            migrationBuilder.AddColumn<bool>(
                name: "EquityDepositConfirmed",
                table: "NampApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquityDepositNote",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "GpsConsentNote",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "GpsConsentObtained",
                table: "NampApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseDocumentsNote",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "LeaseDocumentsSigned",
                table: "NampApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NaicInsuranceInPlace",
                table: "NampApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NaicInsuranceNote",
                table: "NampApplications",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
