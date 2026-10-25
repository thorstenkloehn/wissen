using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wissen.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeiteTitel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Titel",
                table: "Seiten",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Titel",
                table: "Seiten");
        }
    }
}
