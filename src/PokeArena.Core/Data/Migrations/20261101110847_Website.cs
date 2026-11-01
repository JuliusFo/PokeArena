using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeArena.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Website : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AcquiredAt",
                table: "Specimens",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            // Existing specimens: the owner got them when caught, unless PvP moved them — then at the latest such fight.
            migrationBuilder.Sql("""
                UPDATE "Specimens" SET "AcquiredAt" = COALESCE(
                    (SELECT MAX("b"."FoughtAt") FROM "Battles" AS "b"
                     WHERE "b"."Kind" = 'Pvp' AND "b"."LootSpecimenId" = "Specimens"."Id"),
                    "CaughtAt");
                """);

            migrationBuilder.CreateTable(
                name: "PublishQueue",
                columns: table => new
                {
                    Path = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    QueuedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    NextAttemptAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublishQueue", x => x.Path);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublishQueue_NextAttemptAt",
                table: "PublishQueue",
                column: "NextAttemptAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublishQueue");

            migrationBuilder.DropColumn(
                name: "AcquiredAt",
                table: "Specimens");
        }
    }
}
