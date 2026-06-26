using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentSessionMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_start_time",
                table: "tournaments",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "session_mode",
                table: "tournaments",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "SingleSession");

            migrationBuilder.Sql("""
                UPDATE tournaments
                SET session_mode = 'MultiWave'
                WHERE id IN (SELECT DISTINCT tournament_id FROM waves);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_start_time",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "session_mode",
                table: "tournaments");
        }
    }
}
