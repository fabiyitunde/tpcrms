using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfEopHashCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotHashCheck",
                table: "RhshfFinancingEops",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotSent")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotHashScheme",
                table: "RhshfFinancingEops",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotHashV2",
                table: "RhshfFinancingEops",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SnapshotHashV2Scheme",
                table: "RhshfFinancingEops",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotHashCheck",
                table: "RhshfFinancingEops");

            migrationBuilder.DropColumn(
                name: "SnapshotHashScheme",
                table: "RhshfFinancingEops");

            migrationBuilder.DropColumn(
                name: "SnapshotHashV2",
                table: "RhshfFinancingEops");

            migrationBuilder.DropColumn(
                name: "SnapshotHashV2Scheme",
                table: "RhshfFinancingEops");
        }
    }
}
