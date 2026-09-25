using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPoolSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pool_settings",
                columns: table => new
                {
                    stage_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    organization_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    teams_per_pool_target = table.Column<int>(type: "INTEGER", nullable: false),
                    pool_size_strategy = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    seeding_method = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    club_separation_mode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    play_style = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    court_allocation_mode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ref_assignment_mode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pool_settings", x => x.stage_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pool_settings_organization_id",
                table: "pool_settings",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pool_settings");
        }
    }
}
