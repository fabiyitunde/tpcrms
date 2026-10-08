using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfDirectorDeclaredByFac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeclaredByFac",
                table: "RhshfDirectors",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // Backfill: existing non-CAC directors are treated as FAC-declared (protected from staff
            // deletion) — the safe side, since the stale CAC rows are the ones meant to stay removable.
            // Additive data update only (no drop/delete), so the destructive-migration scanner is fine.
            migrationBuilder.Sql("UPDATE `RhshfDirectors` SET `DeclaredByFac` = 1 WHERE `SourcedFromCac` = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeclaredByFac",
                table: "RhshfDirectors");
        }
    }
}
