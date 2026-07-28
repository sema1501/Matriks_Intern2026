using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTradingBotsAndSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradingBots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    BuyRsiThreshold = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SellRsiThreshold = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TradeQuantity = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingBots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradingBots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BotSignals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BotId = table.Column<int>(type: "int", nullable: false),
                    SignalType = table.Column<int>(type: "int", nullable: false),
                    RsiValueAtSignal = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PriceAtSignal = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BotSignals_TradingBots_BotId",
                        column: x => x.BotId,
                        principalTable: "TradingBots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotSignals_BotId",
                table: "BotSignals",
                column: "BotId");

            migrationBuilder.CreateIndex(
                name: "IX_BotSignals_CreatedAt",
                table: "BotSignals",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TradingBots_UserId_Symbol",
                table: "TradingBots",
                columns: new[] { "UserId", "Symbol" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotSignals");

            migrationBuilder.DropTable(
                name: "TradingBots");
        }
    }
}
