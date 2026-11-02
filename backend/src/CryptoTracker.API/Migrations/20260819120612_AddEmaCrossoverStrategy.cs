using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEmaCrossoverStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LongEmaPeriod",
                table: "TradingBots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShortEmaPeriod",
                table: "TradingBots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Strategy",
                table: "TradingBots",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LongEmaPeriod",
                table: "TradingBots");

            migrationBuilder.DropColumn(
                name: "ShortEmaPeriod",
                table: "TradingBots");

            migrationBuilder.DropColumn(
                name: "Strategy",
                table: "TradingBots");
        }
    }
}
