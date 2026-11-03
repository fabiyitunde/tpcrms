using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRhshfOfferValidity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ValidityDays",
                table: "RhshfOffers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfferValidityDays",
                table: "LoanProducts",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidityDays",
                table: "RhshfOffers");

            migrationBuilder.DropColumn(
                name: "OfferValidityDays",
                table: "LoanProducts");
        }
    }
}
