using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeArena.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class PvpBattles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LootSpeciesId",
                table: "Battles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LootSpecimenId",
                table: "Battles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LootSpeciesId",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "LootSpecimenId",
                table: "Battles");
        }
    }
}
