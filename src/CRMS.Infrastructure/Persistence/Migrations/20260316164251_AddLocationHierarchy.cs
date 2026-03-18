using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All operations are idempotent to handle partial migration runs
            
            // 1. Handle Users.BranchId -> LocationId rename/add
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'BranchId');
                SET @sql = IF(@col_exists > 0, 
                    'ALTER TABLE `Users` RENAME COLUMN `BranchId` TO `LocationId`',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
            
            migrationBuilder.Sql(@"
                SET @col_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'LocationId');
                SET @sql = IF(@col_exists = 0, 
                    'ALTER TABLE `Users` ADD COLUMN `LocationId` char(36) NULL',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // 2. Create Locations table if not exists
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `Locations` (
                    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                    `Code` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
                    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `Type` int NOT NULL,
                    `ParentLocationId` char(36) COLLATE ascii_general_ci NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `Address` varchar(500) CHARACTER SET utf8mb4 NULL,
                    `ManagerName` varchar(100) CHARACTER SET utf8mb4 NULL,
                    `ContactPhone` varchar(20) CHARACTER SET utf8mb4 NULL,
                    `ContactEmail` varchar(100) CHARACTER SET utf8mb4 NULL,
                    `SortOrder` int NOT NULL DEFAULT 0,
                    `CreatedAt` datetime(6) NOT NULL,
                    `CreatedBy` longtext CHARACTER SET utf8mb4 NOT NULL,
                    `ModifiedAt` datetime(6) NULL,
                    `ModifiedBy` longtext CHARACTER SET utf8mb4 NULL,
                    CONSTRAINT `PK_Locations` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
            ");

            // 3. Add FK on Locations.ParentLocationId if not exists
            migrationBuilder.Sql(@"
                SET @fk_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Locations' AND CONSTRAINT_NAME = 'FK_Locations_Locations_ParentLocationId');
                SET @sql = IF(@fk_exists = 0, 
                    'ALTER TABLE `Locations` ADD CONSTRAINT `FK_Locations_Locations_ParentLocationId` FOREIGN KEY (`ParentLocationId`) REFERENCES `Locations` (`Id`) ON DELETE RESTRICT',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // 4. Create indexes if not exist (MySQL ignores CREATE INDEX IF NOT EXISTS, use procedure)
            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND INDEX_NAME = 'IX_Users_LocationId');
                SET @sql = IF(@idx_exists = 0, 
                    'CREATE INDEX `IX_Users_LocationId` ON `Users` (`LocationId`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ConsentRecords' AND INDEX_NAME = 'IX_ConsentRecords_NIN');
                SET @sql = IF(@idx_exists = 0, 
                    'CREATE INDEX `IX_ConsentRecords_NIN` ON `ConsentRecords` (`NIN`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'BureauReports' AND INDEX_NAME = 'IX_BureauReports_ConsentRecordId');
                SET @sql = IF(@idx_exists = 0, 
                    'CREATE INDEX `IX_BureauReports_ConsentRecordId` ON `BureauReports` (`ConsentRecordId`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Locations' AND INDEX_NAME = 'IX_Locations_Code');
                SET @sql = IF(@idx_exists = 0, 
                    'CREATE UNIQUE INDEX `IX_Locations_Code` ON `Locations` (`Code`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Locations' AND INDEX_NAME = 'IX_Locations_IsActive');
                SET @sql = IF(@idx_exists = 0, 
                    'CREATE INDEX `IX_Locations_IsActive` ON `Locations` (`IsActive`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Locations' AND INDEX_NAME = 'IX_Locations_ParentLocationId');
                SET @sql = IF(@idx_exists = 0, 
                    'CREATE INDEX `IX_Locations_ParentLocationId` ON `Locations` (`ParentLocationId`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Locations' AND INDEX_NAME = 'IX_Locations_Type');
                SET @sql = IF(@idx_exists = 0, 
                    'CREATE INDEX `IX_Locations_Type` ON `Locations` (`Type`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            migrationBuilder.Sql(@"
                SET @idx_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Locations' AND INDEX_NAME = 'IX_Locations_Type_IsActive');
                SET @sql = IF(@idx_exists = 0, 
                    'CREATE INDEX `IX_Locations_Type_IsActive` ON `Locations` (`Type`, `IsActive`)',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");

            // 5. Clear orphan LocationId values in Users that don't exist in Locations
            migrationBuilder.Sql(@"
                UPDATE `Users` SET `LocationId` = NULL 
                WHERE `LocationId` IS NOT NULL 
                AND `LocationId` NOT IN (SELECT `Id` FROM `Locations`);
            ");

            // 6. Add FK from Users to Locations if not exists
            migrationBuilder.Sql(@"
                SET @fk_exists = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND CONSTRAINT_NAME = 'FK_Users_Locations_LocationId');
                SET @sql = IF(@fk_exists = 0, 
                    'ALTER TABLE `Users` ADD CONSTRAINT `FK_Users_Locations_LocationId` FOREIGN KEY (`LocationId`) REFERENCES `Locations` (`Id`) ON DELETE SET NULL',
                    'SELECT 1');
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Locations_LocationId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Users_LocationId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ConsentRecords_NIN",
                table: "ConsentRecords");

            migrationBuilder.DropIndex(
                name: "IX_BureauReports_ConsentRecordId",
                table: "BureauReports");

            migrationBuilder.RenameColumn(
                name: "LocationId",
                table: "Users",
                newName: "BranchId");

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalOverdue",
                table: "BureauReports",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "FraudRecommendation",
                table: "BureauReports",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "FraudCheckRawJson",
                table: "BureauReports",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "LONGTEXT",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
