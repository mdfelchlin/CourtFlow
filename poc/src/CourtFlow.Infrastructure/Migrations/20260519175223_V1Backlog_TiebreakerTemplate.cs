using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class V1Backlog_TiebreakerTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "structure_json",
                table: "tournament_templates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tiebreaker_preset",
                table: "divisions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "structure_json",
                table: "tournament_templates");

            migrationBuilder.DropColumn(
                name: "tiebreaker_preset",
                table: "divisions");
        }
    }
}
