using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Gamify.Api.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationsAndAgeRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    PriceCents = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: false),
                    Moods = table.Column<int>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumPcLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    PegiAge = table.Column<int>(type: "INTEGER", nullable: true),
                    EsrbAge = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupMember",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMember", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMember_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GamePlatforms",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlatformsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamePlatforms", x => new { x.GameId, x.PlatformsId });
                    table.ForeignKey(
                        name: "FK_GamePlatforms_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GamePlatforms_Platforms_PlatformsId",
                        column: x => x.PlatformsId,
                        principalTable: "Platforms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Games",
                columns: new[] { "Id", "Difficulty", "EsrbAge", "MaxPlayers", "MinimumPcLevel", "Moods", "PegiAge", "PriceCents", "Title" },
                values: new object[,]
                {
                    { 1, 1, 10, 4, 1, 1, 12, 1399, "Stardew Valley" },
                    { 2, 1, null, 4, 1, 1, 3, 1999, "Farm Together" },
                    { 3, 1, 0, 4, 1, 17, 3, 1999, "Human Fall Flat" },
                    { 4, 1, 0, 2, 1, 17, 3, 1999, "Untitled Goose Game" },
                    { 5, 1, 10, 2, 1, 3, 7, 1499, "Cat Quest II" },
                    { 6, 1, 10, 4, 2, 3, 12, 0, "Palia" },
                    { 7, 1, 10, 4, 2, 3, 7, 2999, "Fae Farm" },
                    { 8, 1, 0, 1, 1, 17, 3, 1999, "Unpacking" },
                    { 9, 1, 0, 4, null, 17, 3, 1999, "Snipperclips" },
                    { 10, 1, 10, 4, 2, 3, 7, 2999, "Minecraft" },
                    { 11, 1, 10, 4, 2, 6, 7, 1999, "Minecraft Dungeons" },
                    { 12, 2, 0, 4, 2, 72, 3, 0, "Rocket League" },
                    { 13, 2, 10, 2, 1, 16, 12, 975, "Portal 2" },
                    { 14, 2, 13, 2, 1, 48, null, 2999, "World of Goo 2" },
                    { 15, 2, 17, 4, 1, 4, 18, 975, "Left 4 Dead 2" },
                    { 16, 3, 17, 1, 3, 4, 18, 1999, "DOOM (2016)" }
                });

            migrationBuilder.InsertData(
                table: "GamePlatforms",
                columns: new[] { "GameId", "PlatformsId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 1, 2 },
                    { 1, 3 },
                    { 1, 4 },
                    { 2, 1 },
                    { 2, 2 },
                    { 2, 3 },
                    { 2, 4 },
                    { 3, 1 },
                    { 3, 2 },
                    { 3, 3 },
                    { 3, 4 },
                    { 4, 1 },
                    { 4, 2 },
                    { 4, 3 },
                    { 4, 4 },
                    { 5, 1 },
                    { 5, 2 },
                    { 5, 3 },
                    { 5, 4 },
                    { 6, 1 },
                    { 6, 4 },
                    { 7, 1 },
                    { 7, 4 },
                    { 8, 1 },
                    { 8, 2 },
                    { 8, 3 },
                    { 8, 4 },
                    { 9, 4 },
                    { 10, 1 },
                    { 10, 2 },
                    { 10, 3 },
                    { 10, 4 },
                    { 11, 1 },
                    { 11, 2 },
                    { 11, 3 },
                    { 11, 4 },
                    { 12, 1 },
                    { 12, 2 },
                    { 12, 3 },
                    { 12, 4 },
                    { 13, 1 },
                    { 13, 4 },
                    { 14, 1 },
                    { 14, 4 },
                    { 15, 1 },
                    { 16, 1 },
                    { 16, 2 },
                    { 16, 3 },
                    { 16, 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GamePlatforms_PlatformsId",
                table: "GamePlatforms",
                column: "PlatformsId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMember_UserId",
                table: "GroupMember",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GamePlatforms");

            migrationBuilder.DropTable(
                name: "GroupMember");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Users");
        }
    }
}
