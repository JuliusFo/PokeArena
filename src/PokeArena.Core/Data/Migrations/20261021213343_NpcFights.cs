using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeArena.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class NpcFights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Tokens",
                table: "Trainers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NpcId",
                table: "Battles",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tokens",
                table: "Battles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NpcBadges",
                columns: table => new
                {
                    TrainerId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NpcId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    WonAt = table.Column<long>(type: "INTEGER", nullable: false),
                    BattleId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcBadges", x => new { x.TrainerId, x.NpcId });
                    table.ForeignKey(
                        name: "FK_NpcBadges_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Trainers",
                        principalColumn: "TwitchUserId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NpcBadges");

            migrationBuilder.DropColumn(
                name: "Tokens",
                table: "Trainers");

            migrationBuilder.DropColumn(
                name: "NpcId",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "Tokens",
                table: "Battles");
        }
    }
}
