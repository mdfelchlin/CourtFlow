using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wave0_BackfillTournamentTeamsDivisionRankings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """

                INSERT INTO tournament_teams (
                    id,
                    organization_id,
                    tournament_id,
                    division_id,
                    team_id,
                    entry_status,
                    checked_in_status,
                    notes,
                    withdrawn_at,
                    withdrawal_reason,
                    effective_after_match_id,
                    created_at_utc,
                    updated_at_utc)
                SELECT 
                    t.id,
                    t.organization_id,
                    d.tournament_id,
                    t.division_id,
                    t.id,
                    'Active',
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    datetime('now'),
                    datetime('now')
                FROM teams t
                INNER JOIN divisions d ON d.id = t.division_id
                WHERE NOT EXISTS (
                    SELECT 1 FROM tournament_teams tt
                    WHERE tt.team_id = t.id AND tt.tournament_id = d.tournament_id);

                INSERT INTO division_rankings (
                    id,
                    organization_id,
                    division_id,
                    tournament_team_id,
                    rank,
                    source,
                    is_manual_override,
                    created_at_utc,
                    updated_at_utc)
                SELECT 
                    lower(hex(randomblob(16))),
                    tt.organization_id,
                    tt.division_id,
                    tt.id,
                    CAST(ordered.rn AS INTEGER),
                    'seed_legacy',
                    0,
                    datetime('now'),
                    datetime('now')
                FROM (
                    SELECT 
                        tt.id AS tt_pk,
                        row_number() OVER (
                            PARTITION BY t.division_id
                            ORDER BY 
                                CASE WHEN t.seed_hint IS NULL THEN 1 ELSE 0 END ASC,
                                t.seed_hint ASC,
                                trim(lower(t.name)) ASC,
                                t.id
                        ) AS rn
                    FROM tournament_teams tt
                    INNER JOIN teams t ON t.id = tt.team_id
                    INNER JOIN divisions d ON d.id = tt.division_id AND d.id = t.division_id
                    WHERE d.tournament_id = tt.tournament_id
                ) AS ordered
                INNER JOIN tournament_teams tt ON tt.id = ordered.tt_pk
                WHERE NOT EXISTS (
                    SELECT 1 FROM division_rankings dr WHERE dr.tournament_team_id = tt.id);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
