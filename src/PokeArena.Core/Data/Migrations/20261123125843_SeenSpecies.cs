using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeArena.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeenSpecies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeenSpecies",
                columns: table => new
                {
                    TrainerId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SpeciesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeenSpecies", x => new { x.TrainerId, x.SpeciesId });
                    table.ForeignKey(
                        name: "FK_SeenSpecies_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Trainers",
                        principalColumn: "TwitchUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Fill it once from the fights fought so far: every fighter in the log of a fight the trainer took part
            // in. The log is the BattleLog JSON with web defaults (camelCase).
            migrationBuilder.Sql(
                """
                INSERT OR IGNORE INTO "SeenSpecies" ("TrainerId", "SpeciesId")
                SELECT DISTINCT p."TrainerId", CAST(json_extract(f.value, '$.speciesId') AS INTEGER)
                FROM "BattleParticipants" AS p
                JOIN "Battles" AS b ON b."Id" = p."BattleId"
                JOIN json_each(b."LogJson", '$.fighters') AS f
                WHERE json_extract(f.value, '$.speciesId') IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeenSpecies");
        }
    }
}
