using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchSlotSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "match_slot_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    slot = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    source_kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    fixed_tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    source_match_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    source_pool_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    source_position = table.Column<int>(type: "INTEGER", nullable: true),
                    resolved_tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_slot_sources", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_match_slot_sources_match_id_slot",
                table: "match_slot_sources",
                columns: new[] { "match_id", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_slot_sources_source_match_id",
                table: "match_slot_sources",
                column: "source_match_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_slot_sources");
        }
    }
}
