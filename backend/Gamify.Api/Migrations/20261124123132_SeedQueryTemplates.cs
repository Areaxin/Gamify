using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Gamify.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedQueryTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "QueryTemplates",
                columns: new[] { "Id", "IsActive", "SortOrder", "Text", "Title" },
                values: new object[,]
                {
                    { 1, true, 1, "Ieškau ramaus žaidimo vienam, iki 15 eurų, pradedantiesiems.", "Ramus žaidimas vienam" },
                    { 2, true, 2, "Ieškau smagaus žaidimo dviem žmonėms, kurį galėčiau žaisti su draugu.", "Žaidimas dviem draugams" },
                    { 3, true, 3, "Ieškau siaubo žaidimo su gera istorija ir įtempta atmosfera.", "Siaubo žaidimas" },
                    { 4, true, 4, "Ieškau smagaus žaidimo keturiems draugams, iki 20 eurų.", "Žaidimas keturiems draugams" },
                    { 5, true, 5, "Ieškau lengvai išmokstamo žaidimo pradedantiesiems.", "Žaidimas pradedantiesiems" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "QueryTemplates",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "QueryTemplates",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "QueryTemplates",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "QueryTemplates",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "QueryTemplates",
                keyColumn: "Id",
                keyValue: 5);
        }
    }
}
