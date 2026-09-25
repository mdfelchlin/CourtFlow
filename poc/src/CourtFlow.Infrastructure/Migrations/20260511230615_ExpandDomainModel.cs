using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_schedule_versions_tournament_id",
                table: "schedule_versions");

            migrationBuilder.AddColumn<string>(
                name: "created_by_user_id",
                table: "schedule_versions",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "published_at",
                table: "schedule_versions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "superseded_at",
                table: "schedule_versions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "superseded_by_schedule_version_id",
                table: "schedule_versions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "advancement_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_stage_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_pool_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    source_finish_position = table.Column<int>(type: "INTEGER", nullable: false),
                    target_bracket_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    target_seed_slot = table.Column<int>(type: "INTEGER", nullable: false),
                    order_by = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_advancement_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bracket_seed_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    bracket_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    seed_position = table.Column<int>(type: "INTEGER", nullable: false),
                    label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    expected_source_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    advancement_rule_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    manual_tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    resolved_tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bracket_seed_slots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "court_outages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    court_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    starts_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ends_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_court_outages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "division_rankings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    division_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    rank = table.Column<int>(type: "INTEGER", nullable: false),
                    source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    is_manual_override = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_division_rankings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    file_name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    total_rows = table.Column<int>(type: "INTEGER", nullable: false),
                    success_rows = table.Column<int>(type: "INTEGER", nullable: false),
                    error_rows = table.Column<int>(type: "INTEGER", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "match_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    schedule_version_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    court_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    scheduled_start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    scheduled_end = table.Column<DateTime>(type: "TEXT", nullable: false),
                    assignment_status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "match_officials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    work_team_tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    r1tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    r2tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    assignment_source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    manual_assigned_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    manual_reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_officials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pool_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    pool_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    seed_in_pool = table.Column<int>(type: "INTEGER", nullable: false),
                    finish_position = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pool_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "repair_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    division_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    trigger_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    preview_diagnostics_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_by_user_id = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repair_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    division_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    stage_number = table.Column<int>(type: "INTEGER", nullable: false),
                    stage_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    seeding_source = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tournament_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    division_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    entry_status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    checked_in_status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    withdrawn_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    withdrawal_reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    effective_after_match_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tournament_teams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tournament_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    source_tournament_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    usage_count = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tournament_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "waves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    display_order = table.Column<int>(type: "INTEGER", nullable: false),
                    default_start_time = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    default_end_time = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waves", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "import_rows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    import_job_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    row_number = table.Column<int>(type: "INTEGER", nullable: false),
                    raw_values = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    validation_errors = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    matched_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    matched_club_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_rows", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_rows_import_jobs_import_job_id",
                        column: x => x.import_job_id,
                        principalTable: "import_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repair_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    repair_job_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    action_type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    target_id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    before_json = table.Column<string>(type: "TEXT", nullable: true),
                    after_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repair_actions", x => x.id);
                    table.ForeignKey(
                        name: "fk_repair_actions_repair_jobs_repair_job_id",
                        column: x => x.repair_job_id,
                        principalTable: "repair_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_schedule_versions_tournament_id",
                table: "schedule_versions",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "ix_advancement_rules_source_stage_id",
                table: "advancement_rules",
                column: "source_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_advancement_rules_target_bracket_id",
                table: "advancement_rules",
                column: "target_bracket_id");

            migrationBuilder.CreateIndex(
                name: "ix_bracket_seed_slots_bracket_id_seed_position",
                table: "bracket_seed_slots",
                columns: new[] { "bracket_id", "seed_position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_court_outages_court_id",
                table: "court_outages",
                column: "court_id");

            migrationBuilder.CreateIndex(
                name: "ix_court_outages_court_id_starts_at_ends_at",
                table: "court_outages",
                columns: new[] { "court_id", "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_division_rankings_division_id_rank",
                table: "division_rankings",
                columns: new[] { "division_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_division_rankings_division_id_tournament_team_id",
                table: "division_rankings",
                columns: new[] { "division_id", "tournament_team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_jobs_tournament_id",
                table: "import_jobs",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_rows_import_job_id_row_number",
                table: "import_rows",
                columns: new[] { "import_job_id", "row_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_assignments_court_id_scheduled_start",
                table: "match_assignments",
                columns: new[] { "court_id", "scheduled_start" });

            migrationBuilder.CreateIndex(
                name: "ix_match_assignments_match_id",
                table: "match_assignments",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_assignments_schedule_version_id",
                table: "match_assignments",
                column: "schedule_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_officials_match_id",
                table: "match_officials",
                column: "match_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pool_entries_pool_id_seed_in_pool",
                table: "pool_entries",
                columns: new[] { "pool_id", "seed_in_pool" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pool_entries_pool_id_tournament_team_id",
                table: "pool_entries",
                columns: new[] { "pool_id", "tournament_team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_repair_actions_repair_job_id",
                table: "repair_actions",
                column: "repair_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_repair_jobs_tournament_id",
                table: "repair_jobs",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "ix_stages_division_id_stage_number",
                table: "stages",
                columns: new[] { "division_id", "stage_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tournament_teams_tournament_id_division_id",
                table: "tournament_teams",
                columns: new[] { "tournament_id", "division_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tournament_teams_tournament_id_team_id",
                table: "tournament_teams",
                columns: new[] { "tournament_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tournament_templates_organization_id",
                table: "tournament_templates",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_waves_tournament_id_code",
                table: "waves",
                columns: new[] { "tournament_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "advancement_rules");

            migrationBuilder.DropTable(
                name: "bracket_seed_slots");

            migrationBuilder.DropTable(
                name: "court_outages");

            migrationBuilder.DropTable(
                name: "division_rankings");

            migrationBuilder.DropTable(
                name: "import_rows");

            migrationBuilder.DropTable(
                name: "match_assignments");

            migrationBuilder.DropTable(
                name: "match_officials");

            migrationBuilder.DropTable(
                name: "pool_entries");

            migrationBuilder.DropTable(
                name: "repair_actions");

            migrationBuilder.DropTable(
                name: "stages");

            migrationBuilder.DropTable(
                name: "tournament_teams");

            migrationBuilder.DropTable(
                name: "tournament_templates");

            migrationBuilder.DropTable(
                name: "waves");

            migrationBuilder.DropTable(
                name: "import_jobs");

            migrationBuilder.DropTable(
                name: "repair_jobs");

            migrationBuilder.DropIndex(
                name: "ix_schedule_versions_tournament_id",
                table: "schedule_versions");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "schedule_versions");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "schedule_versions");

            migrationBuilder.DropColumn(
                name: "superseded_at",
                table: "schedule_versions");

            migrationBuilder.DropColumn(
                name: "superseded_by_schedule_version_id",
                table: "schedule_versions");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_versions_tournament_id",
                table: "schedule_versions",
                column: "tournament_id",
                unique: true);
        }
    }
}
