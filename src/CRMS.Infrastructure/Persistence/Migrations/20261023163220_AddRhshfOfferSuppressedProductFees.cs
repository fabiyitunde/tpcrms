using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfOfferSuppressedProductFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuppressedProductFees",
                table: "RhshfOffers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuppressedProductFees",
                table: "RhshfOffers");
        }
    }
}
