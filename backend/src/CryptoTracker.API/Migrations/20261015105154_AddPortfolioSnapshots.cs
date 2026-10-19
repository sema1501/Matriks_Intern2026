using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoTracker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CashBalance = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    HoldingsValue = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioSnapshots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_UserId_TakenAt",
                table: "PortfolioSnapshots",
                columns: new[] { "UserId", "TakenAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioSnapshots");
        }
    }
}
