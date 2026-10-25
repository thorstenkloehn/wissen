using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wissen.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inhaltstyp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Typ",
                table: "Seiten",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Seiten_Typ",
                table: "Seiten",
                column: "Typ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Seiten_Typ",
                table: "Seiten");

            migrationBuilder.DropColumn(
                name: "Typ",
                table: "Seiten");
        }
    }
}
