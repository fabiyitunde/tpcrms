using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfFinancingSliceBDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseUnit",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "PackCount",
                table: "RhshfFinancingLines",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackSize",
                table: "RhshfFinancingLines",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PackUnit",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "PackagingExcessQuantity",
                table: "RhshfFinancingLines",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerPack",
                table: "RhshfFinancingLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceVersionId",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ProcurementQuantity",
                table: "RhshfFinancingLines",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderAssignmentId",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderBillableQuantity",
                table: "RhshfFinancingLines",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderId",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderRate",
                table: "RhshfFinancingLines",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRateBasis",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProviderRateVersion",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProviderServiceType",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SupplierProductId",
                table: "RhshfFinancingLines",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "EopApprovedAt",
                table: "RhshfFinancingEops",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EopReference",
                table: "RhshfFinancingEops",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "EopRevision",
                table: "RhshfFinancingEops",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotHash",
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
                name: "BaseUnit",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "PackCount",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "PackSize",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "PackUnit",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "PackagingExcessQuantity",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "PricePerPack",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "PriceVersionId",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ProcurementQuantity",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ProviderAssignmentId",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ProviderBillableQuantity",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ProviderRate",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ProviderRateBasis",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ProviderRateVersion",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "ProviderServiceType",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "SupplierProductId",
                table: "RhshfFinancingLines");

            migrationBuilder.DropColumn(
                name: "EopApprovedAt",
                table: "RhshfFinancingEops");

            migrationBuilder.DropColumn(
                name: "EopReference",
                table: "RhshfFinancingEops");

            migrationBuilder.DropColumn(
                name: "EopRevision",
                table: "RhshfFinancingEops");

            migrationBuilder.DropColumn(
                name: "SnapshotHash",
                table: "RhshfFinancingEops");
        }
    }
}
