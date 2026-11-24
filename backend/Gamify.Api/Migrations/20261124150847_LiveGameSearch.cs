using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Gamify.Api.Migrations
{
    /// <inheritdoc />
    public partial class LiveGameSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE "StoreClicksWithUrls" (
                    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "UserId" INTEGER NOT NULL REFERENCES "Users" ("Id") ON DELETE CASCADE,
                    "GameId" INTEGER NULL,
                    "StoreUrl" TEXT NULL,
                    "Store" TEXT NOT NULL,
                    "ClickedAt" TEXT NOT NULL
                );
                INSERT INTO "StoreClicksWithUrls" ("Id", "UserId", "GameId", "StoreUrl", "Store", "ClickedAt")
                SELECT c."Id", c."UserId", c."GameId", g."StoreUrl", c."Store", c."ClickedAt"
                FROM "StoreClicks" c LEFT JOIN "Games" g ON g."Id" = c."GameId";
                DROP TABLE "StoreClicks";
                ALTER TABLE "StoreClicksWithUrls" RENAME TO "StoreClicks";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StoreClicks_UserId",
                table: "StoreClicks",
                column: "UserId");

            migrationBuilder.DropTable(name: "GamePlatforms");
            migrationBuilder.DropTable(name: "Games");

            migrationBuilder.Sql("""
                UPDATE "SavedPlaylists"
                SET "GamesJson" = (
                    SELECT json_group_array(json_remove(value, '$.PriceCents', '$.priceCents'))
                    FROM json_each("SavedPlaylists"."GamesJson")
                )
                WHERE json_valid("GamesJson") AND json_type("GamesJson") = 'array';
                """);

            migrationBuilder.AddColumn<bool>(
                name: "ForGroup",
                table: "QueryHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_StoreClicks_StoreUrl_ClickedAt",
                table: "StoreClicks",
                columns: new[] { "StoreUrl", "ClickedAt" });

            migrationBuilder.Sql("""
                DELETE FROM "QueryHistory"
                WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", ROW_NUMBER() OVER (
                            PARTITION BY "UserId", "Text", "ForGroup" ORDER BY "CreatedAt" DESC, "Id" DESC
                        ) AS entry_rank
                        FROM "QueryHistory"
                    ) WHERE entry_rank > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_QueryHistory_UserId_Text_ForGroup",
                table: "QueryHistory",
                columns: new[] { "UserId", "Text", "ForGroup" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "StoreClicks" WHERE "GameId" IS NULL;""");
            migrationBuilder.DropIndex(
                name: "IX_StoreClicks_StoreUrl_ClickedAt",
                table: "StoreClicks");

            migrationBuilder.DropIndex(
                name: "IX_QueryHistory_UserId_Text_ForGroup",
                table: "QueryHistory");

            migrationBuilder.DropColumn(
                name: "StoreUrl",
                table: "StoreClicks");

            migrationBuilder.DropColumn(
                name: "ForGroup",
                table: "QueryHistory");

            migrationBuilder.AlterColumn<int>(
                name: "GameId",
                table: "StoreClicks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    EsrbAge = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumPcLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    Moods = table.Column<int>(type: "INTEGER", nullable: false),
                    PegiAge = table.Column<int>(type: "INTEGER", nullable: true),
                    PriceCents = table.Column<int>(type: "INTEGER", nullable: false),
                    StoreUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
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
                columns: new[] { "Id", "Difficulty", "EsrbAge", "MaxPlayers", "MinimumPcLevel", "Moods", "PegiAge", "PriceCents", "StoreUrl", "Title" },
                values: new object[,]
                {
                    { 1, 1, 10, 4, 1, 1, 12, 1399, "https://store.steampowered.com/app/413150/", "Stardew Valley" },
                    { 2, 1, null, 4, 1, 1, 3, 1999, "https://store.steampowered.com/app/673950/", "Farm Together" },
                    { 3, 1, 0, 4, 1, 17, 3, 1999, "https://store.steampowered.com/app/477160/", "Human Fall Flat" },
                    { 4, 1, 0, 2, 1, 17, 3, 1999, "https://store.steampowered.com/app/837470/", "Untitled Goose Game" },
                    { 5, 1, 10, 2, 1, 3, 7, 1499, "https://store.steampowered.com/app/914710/", "Cat Quest II" },
                    { 6, 1, 10, 4, 2, 3, 12, 0, "https://store.steampowered.com/app/2707930/", "Palia" },
                    { 7, 1, 10, 4, 2, 3, 7, 2999, null, "Fae Farm" },
                    { 8, 1, 0, 1, 1, 17, 3, 1999, "https://store.steampowered.com/app/1135690/", "Unpacking" },
                    { 9, 1, 0, 4, null, 17, 3, 1999, null, "Snipperclips" },
                    { 10, 1, 10, 4, 2, 3, 7, 2999, null, "Minecraft" },
                    { 11, 1, 10, 4, 2, 6, 7, 1999, "https://store.steampowered.com/app/1672970/", "Minecraft Dungeons" },
                    { 12, 2, 0, 4, 2, 72, 3, 0, "https://store.epicgames.com/p/rocket-league", "Rocket League" },
                    { 13, 2, 10, 2, 1, 16, 12, 975, "https://store.steampowered.com/app/620/", "Portal 2" },
                    { 14, 2, 13, 2, 1, 48, null, 2999, "https://store.epicgames.com/p/world-of-goo-2", "World of Goo 2" },
                    { 15, 2, 17, 4, 1, 4, 18, 975, "https://store.steampowered.com/app/550/", "Left 4 Dead 2" },
                    { 16, 3, 17, 1, 3, 4, 18, 1999, "https://store.steampowered.com/app/379720/", "DOOM (2016)" }
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
                name: "IX_StoreClicks_GameId_ClickedAt",
                table: "StoreClicks",
                columns: new[] { "GameId", "ClickedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GamePlatforms_PlatformsId",
                table: "GamePlatforms",
                column: "PlatformsId");

            migrationBuilder.AddForeignKey(
                name: "FK_StoreClicks_Games_GameId",
                table: "StoreClicks",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
