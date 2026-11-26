using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeArena.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeenSpeciesDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FirstBattleId",
                table: "SeenSpecies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FirstSeenAt",
                table: "SeenSpecies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastBattleId",
                table: "SeenSpecies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastSeenAt",
                table: "SeenSpecies",
                type: "INTEGER",
                nullable: true);

            // Fill them once from the fights fought so far, like the SeenSpecies migration filled the rows: per trainer
            // and species the first and the latest fight whose log has it (by time, then id). FoughtAt is UTC ticks.
            migrationBuilder.Sql(
                """
                WITH "Sighting" AS (
                    SELECT DISTINCT p."TrainerId" AS "TrainerId",
                           CAST(json_extract(f.value, '$.speciesId') AS INTEGER) AS "SpeciesId",
                           b."FoughtAt" AS "FoughtAt",
                           b."Id" AS "BattleId"
                    FROM "BattleParticipants" AS p
                    JOIN "Battles" AS b ON b."Id" = p."BattleId"
                    JOIN json_each(b."LogJson", '$.fighters') AS f
                    WHERE json_extract(f.value, '$.speciesId') IS NOT NULL),
                "Ranked" AS (
                    SELECT *,
                           ROW_NUMBER() OVER (PARTITION BY "TrainerId", "SpeciesId" ORDER BY "FoughtAt", "BattleId") AS "FromFirst",
                           ROW_NUMBER() OVER (PARTITION BY "TrainerId", "SpeciesId" ORDER BY "FoughtAt" DESC, "BattleId" DESC) AS "FromLast"
                    FROM "Sighting")
                UPDATE "SeenSpecies"
                SET "FirstSeenAt" = first."FoughtAt",
                    "FirstBattleId" = first."BattleId",
                    "LastSeenAt" = last."FoughtAt",
                    "LastBattleId" = last."BattleId"
                FROM "Ranked" AS first
                JOIN "Ranked" AS last
                  ON last."TrainerId" = first."TrainerId" AND last."SpeciesId" = first."SpeciesId" AND last."FromLast" = 1
                WHERE first."FromFirst" = 1
                  AND first."TrainerId" = "SeenSpecies"."TrainerId"
                  AND first."SpeciesId" = "SeenSpecies"."SpeciesId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstBattleId",
                table: "SeenSpecies");

            migrationBuilder.DropColumn(
                name: "FirstSeenAt",
                table: "SeenSpecies");

            migrationBuilder.DropColumn(
                name: "LastBattleId",
                table: "SeenSpecies");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "SeenSpecies");
        }
    }
}
