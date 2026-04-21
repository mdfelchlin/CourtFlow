using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBracketDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "brackets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "display_name",
                table: "brackets");
        }
    }
}
