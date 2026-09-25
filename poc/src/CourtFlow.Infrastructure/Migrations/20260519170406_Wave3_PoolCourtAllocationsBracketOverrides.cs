using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wave3_PoolCourtAllocationsBracketOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sequence",
                table: "pools",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "target_size",
                table: "pools",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.CreateTable(
                name: "bracket_seed_slot_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    bracket_seed_slot_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    previous_manual_tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    new_manual_tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    actor_user_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bracket_seed_slot_overrides", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pool_court_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    pool_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    court_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    allocation_type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    priority_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pool_court_allocations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pools_division_id_sequence",
                table: "pools",
                columns: new[] { "division_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_bracket_seed_slot_overrides_at_utc",
                table: "bracket_seed_slot_overrides",
                column: "at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_bracket_seed_slot_overrides_bracket_seed_slot_id",
                table: "bracket_seed_slot_overrides",
                column: "bracket_seed_slot_id");

            migrationBuilder.CreateIndex(
                name: "ix_pool_court_allocations_pool_id_court_id",
                table: "pool_court_allocations",
                columns: new[] { "pool_id", "court_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pool_court_allocations_pool_id_priority_order",
                table: "pool_court_allocations",
                columns: new[] { "pool_id", "priority_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bracket_seed_slot_overrides");

            migrationBuilder.DropTable(
                name: "pool_court_allocations");

            migrationBuilder.DropIndex(
                name: "ix_pools_division_id_sequence",
                table: "pools");

            migrationBuilder.DropColumn(
                name: "sequence",
                table: "pools");

            migrationBuilder.DropColumn(
                name: "target_size",
                table: "pools");
        }
    }
}
