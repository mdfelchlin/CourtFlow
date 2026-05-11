using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type_name = table.Column<string>(type: "TEXT", maxLength: 768, nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    attempt_count = table.Column<int>(type: "INTEGER", nullable: false),
                    last_error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at_utc_created_at_utc",
                table: "outbox_messages",
                columns: new[] { "processed_at_utc", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");
        }
    }
}
