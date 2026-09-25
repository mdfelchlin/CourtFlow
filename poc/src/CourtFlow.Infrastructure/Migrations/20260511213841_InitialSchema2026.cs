using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brackets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    division_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<int>(type: "INTEGER", nullable: false),
                    if_necessary_final = table.Column<bool>(type: "INTEGER", nullable: false),
                    team_capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brackets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    division_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    pool_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    bracket_node_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    court_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    home_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    away_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    ref_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    wave = table.Column<int>(type: "INTEGER", nullable: false),
                    scheduled_start_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    duration_minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    actual_start_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_score_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    finalized_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    state = table.Column<int>(type: "INTEGER", nullable: false),
                    winner_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scheduling_time_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    court_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    wave = table.Column<int>(type: "INTEGER", nullable: false),
                    start_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    duration_minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scheduling_time_slots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bracket_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    bracket_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    branch = table.Column<int>(type: "INTEGER", nullable: false),
                    round = table.Column<int>(type: "INTEGER", nullable: false),
                    slot = table.Column<int>(type: "INTEGER", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    wins_to_node_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    wins_to_slot = table.Column<int>(type: "INTEGER", nullable: true),
                    loses_to_node_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    loses_to_slot = table.Column<int>(type: "INTEGER", nullable: true),
                    home_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    away_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    home_seed = table.Column<int>(type: "INTEGER", nullable: true),
                    away_seed = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bracket_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_bracket_nodes_brackets_bracket_id",
                        column: x => x.bracket_id,
                        principalTable: "brackets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    set_number = table.Column<int>(type: "INTEGER", nullable: false),
                    home_score = table.Column<int>(type: "INTEGER", nullable: false),
                    away_score = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_sets", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_sets_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    external_user_id = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organization_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_organization_memberships_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tournaments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    start_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    end_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    time_zone_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    default_match_duration_minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    tournament_partner_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tournaments", x => x.id);
                    table.ForeignKey(
                        name: "fk_tournaments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "divisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    format = table.Column<int>(type: "INTEGER", nullable: false),
                    max_teams = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_divisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_divisions_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venues", x => x.id);
                    table.ForeignKey(
                        name: "fk_venues_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pools",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    division_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pools", x => x.id);
                    table.ForeignKey(
                        name: "fk_pools_divisions_division_id",
                        column: x => x.division_id,
                        principalTable: "divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    division_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    pool_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    club = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    wave = table.Column<int>(type: "INTEGER", nullable: false),
                    seed_hint = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teams", x => x.id);
                    table.ForeignKey(
                        name: "fk_teams_divisions_division_id",
                        column: x => x.division_id,
                        principalTable: "divisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "courts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    venue_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    tags = table.Column<string>(type: "text[]", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courts", x => x.id);
                    table.ForeignKey(
                        name: "fk_courts_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bracket_nodes_bracket_id_branch_round_slot",
                table: "bracket_nodes",
                columns: new[] { "bracket_id", "branch", "round", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_brackets_division_id",
                table: "brackets",
                column: "division_id");

            migrationBuilder.CreateIndex(
                name: "ix_brackets_tournament_id",
                table: "brackets",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "ix_courts_venue_id",
                table: "courts",
                column: "venue_id");

            migrationBuilder.CreateIndex(
                name: "ix_divisions_tournament_id",
                table: "divisions",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_sets_match_id_set_number",
                table: "match_sets",
                columns: new[] { "match_id", "set_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matches_bracket_node_id",
                table: "matches",
                column: "bracket_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_court_id",
                table: "matches",
                column: "court_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_division_id",
                table: "matches",
                column: "division_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_pool_id",
                table: "matches",
                column: "pool_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_tournament_id",
                table: "matches",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_tournament_id_scheduled_start_utc",
                table: "matches",
                columns: new[] { "tournament_id", "scheduled_start_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_organization_memberships_organization_id_external_user_id",
                table: "organization_memberships",
                columns: new[] { "organization_id", "external_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_slug",
                table: "organizations",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pools_division_id",
                table: "pools",
                column: "division_id");

            migrationBuilder.CreateIndex(
                name: "ix_scheduling_time_slots_tournament_id_court_id_start_utc",
                table: "scheduling_time_slots",
                columns: new[] { "tournament_id", "court_id", "start_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_scheduling_time_slots_tournament_id_wave_start_utc",
                table: "scheduling_time_slots",
                columns: new[] { "tournament_id", "wave", "start_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_teams_division_id",
                table: "teams",
                column: "division_id");

            migrationBuilder.CreateIndex(
                name: "ix_teams_pool_id",
                table: "teams",
                column: "pool_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournaments_organization_id_name",
                table: "tournaments",
                columns: new[] { "organization_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_venues_tournament_id",
                table: "venues",
                column: "tournament_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bracket_nodes");

            migrationBuilder.DropTable(
                name: "courts");

            migrationBuilder.DropTable(
                name: "match_sets");

            migrationBuilder.DropTable(
                name: "organization_memberships");

            migrationBuilder.DropTable(
                name: "pools");

            migrationBuilder.DropTable(
                name: "scheduling_time_slots");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "brackets");

            migrationBuilder.DropTable(
                name: "venues");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "divisions");

            migrationBuilder.DropTable(
                name: "tournaments");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
