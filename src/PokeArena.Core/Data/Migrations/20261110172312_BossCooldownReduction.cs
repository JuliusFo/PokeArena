using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeArena.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class BossCooldownReduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BossCooldownReductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Cooldown = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Until = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BossCooldownReductions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BossCooldownReductions");
        }
    }
}
