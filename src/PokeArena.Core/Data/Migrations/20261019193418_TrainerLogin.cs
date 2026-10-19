using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokeArena.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrainerLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Login",
                table: "Trainers",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Best guess until the trainer's next chat message brings the real login: for most viewers the login
            // is the lower-cased display name.
            migrationBuilder.Sql("UPDATE \"Trainers\" SET \"Login\" = lower(\"DisplayName\");");

            migrationBuilder.CreateIndex(
                name: "IX_Trainers_Login",
                table: "Trainers",
                column: "Login");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trainers_Login",
                table: "Trainers");

            migrationBuilder.DropColumn(
                name: "Login",
                table: "Trainers");
        }
    }
}
