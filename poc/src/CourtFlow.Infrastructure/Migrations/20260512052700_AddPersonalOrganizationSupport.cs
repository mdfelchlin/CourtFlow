using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourtFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalOrganizationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "clerk_org_id",
                table: "organizations",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_external_user_id",
                table: "organizations",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_clerk_org_id",
                table: "organizations",
                column: "clerk_org_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_owner_external_user_id",
                table: "organizations",
                column: "owner_external_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_organizations_clerk_org_id",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "ix_organizations_owner_external_user_id",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "clerk_org_id",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "owner_external_user_id",
                table: "organizations");
        }
    }
}
