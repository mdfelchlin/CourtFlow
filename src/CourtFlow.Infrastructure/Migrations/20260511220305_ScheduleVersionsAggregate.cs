using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleVersionsAggregate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "schedule_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    mutation_sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_versions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_schedule_versions_tournament_id",
                table: "schedule_versions",
                column: "tournament_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schedule_versions");
        }
    }
}
