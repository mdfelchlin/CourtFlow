using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wave1_MatchStageIdAndStagesBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "stage_id",
                table: "matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """

                INSERT INTO stages (
                    id, organization_id, division_id, stage_number, stage_type, name, status, seeding_source, created_at_utc, updated_at_utc)
                SELECT lower(hex(randomblob(16))),
                    d.organization_id,
                    d.id,
                    1,
                    'Pool',
                    'Pool play',
                    'Draft',
                    'division_rankings',
                    datetime('now'),
                    datetime('now')
                FROM divisions d
                WHERE NOT EXISTS (SELECT 1 FROM stages s WHERE s.division_id = d.id AND s.stage_number = 1);

                INSERT INTO stages (
                    id, organization_id, division_id, stage_number, stage_type, name, status, seeding_source, created_at_utc, updated_at_utc)
                SELECT lower(hex(randomblob(16))),
                    d.organization_id,
                    d.id,
                    2,
                    'Bracket',
                    'Bracket',
                    'Draft',
                    'prior_stage_finish_positions',
                    datetime('now'),
                    datetime('now')
                FROM divisions d
                WHERE NOT EXISTS (SELECT 1 FROM stages s WHERE s.division_id = d.id AND s.stage_number = 2);

                UPDATE matches
                SET stage_id = (
                    SELECT st.id FROM stages st
                    WHERE st.division_id = matches.division_id
                      AND st.stage_type = 'Pool'
                      AND st.stage_number = 1 LIMIT 1)
                WHERE matches.pool_id IS NOT NULL AND matches.bracket_node_id IS NULL AND matches.stage_id IS NULL;

                """);

            migrationBuilder.CreateIndex(
                name: "ix_matches_stage_id",
                table: "matches",
                column: "stage_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_matches_stage_id",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "stage_id",
                table: "matches");
        }
    }
}
