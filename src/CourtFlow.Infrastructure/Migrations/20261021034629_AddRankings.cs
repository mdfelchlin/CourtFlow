using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRankings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ranking_places",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    IsNew = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ranking_places", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ranking_places_divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "divisions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ranking_places_tournament_teams_EntryId",
                        column: x => x.EntryId,
                        principalTable: "tournament_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ranking_places_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Reordering writes the final ranks in one save. Postgres would reject the
            // momentary duplicate if this unique check ran row by row, so it waits until commit.
            migrationBuilder.Sql(
                """
                ALTER TABLE ranking_places
                ADD CONSTRAINT "IX_ranking_places_DivisionId_Rank"
                UNIQUE ("DivisionId", "Rank")
                DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ranking_places_EntryId",
                table: "ranking_places",
                column: "EntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ranking_places_TournamentId",
                table: "ranking_places",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ranking_places");
        }
    }
}
