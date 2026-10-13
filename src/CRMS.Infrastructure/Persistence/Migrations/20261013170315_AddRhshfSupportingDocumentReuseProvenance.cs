using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfSupportingDocumentReuseProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalUploadedAt",
                table: "RhshfSupportingDocuments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReusedFromDocumentId",
                table: "RhshfSupportingDocuments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "ReusedFromReference",
                table: "RhshfSupportingDocuments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalUploadedAt",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "ReusedFromDocumentId",
                table: "RhshfSupportingDocuments");

            migrationBuilder.DropColumn(
                name: "ReusedFromReference",
                table: "RhshfSupportingDocuments");
        }
    }
}
