using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TournamentPublicationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at_utc",
                table: "tournaments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "publication_status",
                table: "tournaments",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<DateTime>(
                name: "published_at_utc",
                table: "tournaments",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "archived_at_utc",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "publication_status",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "published_at_utc",
                table: "tournaments");
        }
    }
}
