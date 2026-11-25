using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RhshfBoaProfilingRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DirectorId",
                table: "RhshfSupportingDocuments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ManagementStaffId",
                table: "RhshfSupportingDocuments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "RhshfSupportingDocuments",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "RhshfSupportingDocuments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                // Existing documents become Pending: read back by name, "" is not a member and would throw.
                defaultValue: "Pending")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "RhshfSupportingDocuments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedByUserId",
                table: "RhshfSupportingDocuments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<bool>(
                name: "IsChiefPromoter",
                table: "RhshfDirectors",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MeansOfIdExpiry",
                table: "RhshfDirectors",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeansOfIdNumber",
                table: "RhshfDirectors",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "MeansOfIdType",
                table: "RhshfDirectors",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Nin",
                table: "RhshfDirectors",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RhshfManagementStaff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RhshfCreditProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FullName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Position = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsChiefPromoter = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfManagementStaff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RhshfManagementStaff_RhshfCreditProfiles_RhshfCreditProfileId",
                        column: x => x.RhshfCreditProfileId,
                        principalTable: "RhshfCreditProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RhshfProfilingFieldRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Key = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsMandatory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfProfilingFieldRequirements", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfSupportingDocuments_DirectorId",
                table: "RhshfSupportingDocuments",
                column: "DirectorId");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfSupportingDocuments_ManagementStaffId",
                table: "RhshfSupportingDocuments",
                column: "ManagementStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfManagementStaff_RhshfCreditProfileId",
                table: "RhshfManagementStaff",
                column: "RhshfCreditProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfProfilingFieldRequirements_Key",
                table: "RhshfProfilingFieldRequirements",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RhshfManagementStaff");

            migrationBuilder.DropTable(
                name: "RhshfProfilingFieldRequirements");

            migrationBuilder.DropIndex(
                name: "IX_RhshfSupportingDocuments_DirectorId",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropIndex(
                name: "IX_RhshfSupportingDocuments_ManagementStaffId",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "DirectorId",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "ManagementStaffId",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "IsChiefPromoter",
                table: "RhshfDirectors");

            migrationBuilder.DropColumn(
                name: "MeansOfIdExpiry",
                table: "RhshfDirectors");

            migrationBuilder.DropColumn(
                name: "MeansOfIdNumber",
                table: "RhshfDirectors");

            migrationBuilder.DropColumn(
                name: "MeansOfIdType",
                table: "RhshfDirectors");

            migrationBuilder.DropColumn(
                name: "Nin",
                table: "RhshfDirectors");
        }
    }
}
