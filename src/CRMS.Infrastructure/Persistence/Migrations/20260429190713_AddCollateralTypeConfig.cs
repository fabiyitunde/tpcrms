using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollateralTypeConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CollateralTypeConfigId",
                table: "Collaterals",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<decimal>(
                name: "IndicativeValue",
                table: "Collaterals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndicativeValueCurrency",
                table: "Collaterals",
                type: "varchar(3)",
                maxLength: 3,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ValuationBasis",
                table: "Collaterals",
                type: "varchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "MarketValue")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ValuationReportPath",
                table: "Collaterals",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ValuerAcceptableValue",
                table: "Collaterals",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuerAcceptableValueCurrency",
                table: "Collaterals",
                type: "varchar(3)",
                maxLength: 3,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ValuerCompany",
                table: "Collaterals",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ValuerName",
                table: "Collaterals",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CollateralTypeConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HaircutRate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    ValuationBasis = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: false, defaultValue: "MarketValue")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ModifiedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralTypeConfigs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CollateralTypeConfigs_Code",
                table: "CollateralTypeConfigs",
                column: "Code",
                unique: true);

            // Seed default collateral types matching the legacy enum values
            var systemUserId = new Guid("00000000-0000-0000-0000-000000000001");
            var now = new DateTime(2026, 4, 29, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "CollateralTypeConfigs",
                columns: ["Id", "Name", "Code", "Description", "HaircutRate", "ValuationBasis", "IsActive", "SortOrder", "CreatedByUserId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy"],
                values: new object[,]
                {
                    { Guid.NewGuid(), "Cash Deposit",      "CashDeposit",  "Cash held as security",                       0.00m, "MarketValue", true, 1,  systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Fixed Deposit",     "FixedDeposit", "Fixed term deposit as collateral",             0.05m, "MarketValue", true, 2,  systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Treasury Bills",    "TreasuryBills","Government treasury bills",                    0.05m, "MarketValue", true, 3,  systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Government Bonds",  "Bonds",        "Government or corporate bonds",                0.10m, "MarketValue", true, 4,  systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Stocks/Shares",     "Stocks",       "Listed equities and shares",                  0.30m, "MarketValue", true, 5,  systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Real Estate",       "RealEstate",   "Land and buildings",                          0.20m, "FSV",         true, 6,  systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Vehicle",           "Vehicle",      "Motor vehicles and rolling stock",            0.30m, "FSV",         true, 7,  systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Equipment",         "Equipment",    "Industrial and commercial equipment",         0.40m, "FSV",         true, 8,  systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Inventory",         "Inventory",    "Stock in trade and raw materials",            0.50m, "MarketValue", true, 9,  systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Insurance Policy",  "Insurance",    "Life or endowment insurance policies",        0.20m, "MarketValue", true, 10, systemUserId, now, "SYSTEM", null, null },
                    { Guid.NewGuid(), "Other",             "Other",        "Other collateral types",                      0.40m, "MarketValue", true, 99, systemUserId, now, "SYSTEM", null, null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollateralTypeConfigs");

            migrationBuilder.DropColumn(
                name: "CollateralTypeConfigId",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "IndicativeValue",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "IndicativeValueCurrency",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "ValuationBasis",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "ValuationReportPath",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "ValuerAcceptableValue",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "ValuerAcceptableValueCurrency",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "ValuerCompany",
                table: "Collaterals");

            migrationBuilder.DropColumn(
                name: "ValuerName",
                table: "Collaterals");
        }
    }
}
