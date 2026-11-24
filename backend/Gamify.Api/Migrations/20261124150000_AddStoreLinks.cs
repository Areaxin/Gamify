using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gamify.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StoreUrl",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoreClicks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Store = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ClickedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreClicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreClicks_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StoreClicks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 1,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/413150/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 2,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/673950/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 3,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/477160/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 4,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/837470/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 5,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/914710/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 6,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/2707930/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 8,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/1135690/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 11,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/1672970/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 12,
                column: "StoreUrl",
                value: "https://store.epicgames.com/p/rocket-league");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 13,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/620/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 14,
                column: "StoreUrl",
                value: "https://store.epicgames.com/p/world-of-goo-2");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 15,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/550/");

            migrationBuilder.UpdateData(
                table: "Games",
                keyColumn: "Id",
                keyValue: 16,
                column: "StoreUrl",
                value: "https://store.steampowered.com/app/379720/");

            migrationBuilder.CreateIndex(
                name: "IX_StoreClicks_GameId_ClickedAt",
                table: "StoreClicks",
                columns: new[] { "GameId", "ClickedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreClicks_UserId",
                table: "StoreClicks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreClicks");

            migrationBuilder.DropColumn(
                name: "StoreUrl",
                table: "Games");
        }
    }
}
