using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameNonPerformingToDelinquentFacilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safe rename: only rename if old column exists
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'BureauReports' AND COLUMN_NAME = 'NonPerformingAccounts');
                SET @sql = IF(@col_exists > 0, 
                    'ALTER TABLE `BureauReports` RENAME COLUMN `NonPerformingAccounts` TO `DelinquentFacilities`',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DelinquentFacilities",
                table: "BureauReports",
                newName: "NonPerformingAccounts");
        }
    }
}
