using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDivisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "divisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_divisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_divisions_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_divisions_TournamentId",
                table: "divisions",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "divisions");
        }
    }
}
