using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfLegalClearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RhshfOffers_RhshfCreditProfileId_CycleNumber",
                table: "RhshfOffers");

            migrationBuilder.CreateTable(
                name: "RhshfLegalClearances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RhshfCreditProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CycleNumber = table.Column<int>(type: "int", nullable: false),
                    LegalOfficerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClearedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Outcome = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Comments = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RhshfLegalClearances", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfOffers_RhshfCreditProfileId_CycleNumber",
                table: "RhshfOffers",
                columns: new[] { "RhshfCreditProfileId", "CycleNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_RhshfLegalClearances_RhshfCreditProfileId_CycleNumber",
                table: "RhshfLegalClearances",
                columns: new[] { "RhshfCreditProfileId", "CycleNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RhshfLegalClearances");

            migrationBuilder.DropIndex(
                name: "IX_RhshfOffers_RhshfCreditProfileId_CycleNumber",
                table: "RhshfOffers");

            migrationBuilder.CreateIndex(
                name: "IX_RhshfOffers_RhshfCreditProfileId_CycleNumber",
                table: "RhshfOffers",
                columns: new[] { "RhshfCreditProfileId", "CycleNumber" },
                unique: true);
        }
    }
}
