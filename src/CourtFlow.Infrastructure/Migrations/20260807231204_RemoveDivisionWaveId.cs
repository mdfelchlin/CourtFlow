using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDivisionWaveId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_divisions_wave_id",
                table: "divisions");

            migrationBuilder.DropColumn(
                name: "wave_id",
                table: "divisions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "wave_id",
                table: "divisions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_divisions_wave_id",
                table: "divisions",
                column: "wave_id");
        }
    }
}
