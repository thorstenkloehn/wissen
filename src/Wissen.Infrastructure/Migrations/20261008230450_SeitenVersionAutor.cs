using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wissen.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeitenVersionAutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Autor",
                table: "SeitenVersionen",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Autor",
                table: "SeitenVersionen");
        }
    }
}
