using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNampLegalClearanceStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LegalClearedByUserId",
                table: "NampApplications",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LegalClearedAt",
                table: "NampApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalClearanceNote",
                table: "NampApplications",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LegalDeclineNote",
                table: "NampApplications",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LegalReturnNote",
                table: "NampApplications",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LegalClearedByUserId", table: "NampApplications");
            migrationBuilder.DropColumn(name: "LegalClearedAt", table: "NampApplications");
            migrationBuilder.DropColumn(name: "LegalClearanceNote", table: "NampApplications");
            migrationBuilder.DropColumn(name: "LegalDeclineNote", table: "NampApplications");
            migrationBuilder.DropColumn(name: "LegalReturnNote", table: "NampApplications");
        }
    }
}
