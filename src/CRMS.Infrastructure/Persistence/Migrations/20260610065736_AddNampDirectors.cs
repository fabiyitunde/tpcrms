using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampDirectors : Migration
    {
        // NOTE: This migration is deliberately scoped to ONLY the additive changes for the
        // NAMP directors/shareholders feature (NampDirectors table + CAC company-profile columns).
        // EF scaffolded extra Drop/Rename/Alter operations because the model snapshot had drifted
        // behind several hand-written migrations (tech-appraisal removal, rental fields, status->string).
        // Those changes are already present in the database, so re-applying them here would fail or
        // corrupt data. The regenerated model snapshot (Designer.cs + CRMSDbContextModelSnapshot.cs)
        // is kept as-is, which brings the snapshot back in sync going forward.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── CAC company profile columns on NampApplications ────────────────
            migrationBuilder.AddColumn<string>(
                name: "CacStatus",
                table: "NampApplications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CacEntityType",
                table: "NampApplications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CacRegistrationDate",
                table: "NampApplications",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CacNatureOfBusiness",
                table: "NampApplications",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "CacShareCapital",
                table: "NampApplications",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CacCompanyId",
                table: "NampApplications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CacAddress",
                table: "NampApplications",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CacCity",
                table: "NampApplications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CacState",
                table: "NampApplications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "CacFetchedAt",
                table: "NampApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CacRawJson",
                table: "NampApplications",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // ── NampDirectors table ────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "NampDirectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NampApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CacDirectorId = table.Column<long>(type: "bigint", nullable: true),
                    SourcedFromCac = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FullName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Surname = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OtherName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DateOfBirth = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nationality = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Occupation = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhoneNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    State = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsChairman = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DateOfAppointment = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AffiliateType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RoleStatus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TypeOfShares = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NumSharesAllotted = table.Column<long>(type: "bigint", nullable: true),
                    ShareholdingPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Bvn = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdentityNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BvnVerified = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NampDirectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NampDirectors_NampApplications_NampApplicationId",
                        column: x => x.NampApplicationId,
                        principalTable: "NampApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NampDirectors_NampApplicationId",
                table: "NampDirectors",
                column: "NampApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NampDirectors");

            migrationBuilder.DropColumn(name: "CacStatus", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacEntityType", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacRegistrationDate", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacNatureOfBusiness", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacShareCapital", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacCompanyId", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacAddress", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacCity", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacState", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacFetchedAt", table: "NampApplications");
            migrationBuilder.DropColumn(name: "CacRawJson", table: "NampApplications");
        }
    }
}
