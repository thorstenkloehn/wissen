using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Wissen.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeiteUndVersionen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Seiten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Inhalt = table.Column<string>(type: "text", nullable: false),
                    MarkdownInhalt = table.Column<string>(type: "text", nullable: false),
                    Kategorie = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seiten", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeitenVersionen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeiteId = table.Column<int>(type: "integer", nullable: false),
                    Nummer = table.Column<int>(type: "integer", nullable: false),
                    Inhalt = table.Column<string>(type: "text", nullable: false),
                    MarkdownInhalt = table.Column<string>(type: "text", nullable: false),
                    Kategorie = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ErstelltAm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeitenVersionen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeitenVersionen_Seiten_SeiteId",
                        column: x => x.SeiteId,
                        principalTable: "Seiten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Seiten_Kategorie",
                table: "Seiten",
                column: "Kategorie");

            migrationBuilder.CreateIndex(
                name: "IX_Seiten_Path",
                table: "Seiten",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeitenVersionen_SeiteId_Nummer",
                table: "SeitenVersionen",
                columns: new[] { "SeiteId", "Nummer" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeitenVersionen");

            migrationBuilder.DropTable(
                name: "Seiten");
        }
    }
}
