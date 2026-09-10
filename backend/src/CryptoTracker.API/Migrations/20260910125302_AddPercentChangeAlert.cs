using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPercentChangeAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PercentChangeThreshold",
                table: "PriceAlerts",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReferencePrice",
                table: "PriceAlerts",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "PriceAlerts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PercentChangeThreshold",
                table: "PriceAlerts");

            migrationBuilder.DropColumn(
                name: "ReferencePrice",
                table: "PriceAlerts");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "PriceAlerts");
        }
    }
}
