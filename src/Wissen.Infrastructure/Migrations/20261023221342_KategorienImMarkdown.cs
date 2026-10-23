using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wissen.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KategorienImMarkdown : Migration
    {
        // Die bisherige Kategorie jeder Seite und Version wird als [[Kategorie:...]] ans Markdown
        // angehängt, sofern sie dort nicht schon steht, und als erste Kategorie übernommen. Namen mit
        // "[", "]" oder "|" lassen sich so nicht schreiben und entfallen. Das HTML bleibt gültig, weil
        // die Angabe darin nicht erscheint; "seiten-rendern" gleicht danach Kategorien ab, die schon
        // vorher im Markdown standen.
        private const string Uebernehmen = """
            UPDATE "{0}" SET
                "Kategorien" = ARRAY[btrim("Kategorie")],
                "MarkdownInhalt" = CASE
                    WHEN position('[[Kategorie:' || btrim("Kategorie") || ']]' IN "MarkdownInhalt") > 0 THEN "MarkdownInhalt"
                    ELSE "MarkdownInhalt" || E'\n\n[[Kategorie:' || btrim("Kategorie") || ']]'
                END
            WHERE btrim("Kategorie") <> '' AND "Kategorie" !~ '[][|]';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Seiten_Kategorie",
                table: "Seiten");

            foreach (var table in new[] { "Seiten", "SeitenVersionen" })
            {
                migrationBuilder.AddColumn<List<string>>(
                    name: "Kategorien",
                    table: table,
                    type: "text[]",
                    nullable: false,
                    defaultValueSql: "'{}'");

                migrationBuilder.Sql(string.Format(Uebernehmen, table));

                migrationBuilder.DropColumn(
                    name: "Kategorie",
                    table: table);
            }

            migrationBuilder.CreateIndex(
                name: "IX_Seiten_Kategorien",
                table: "Seiten",
                column: "Kategorien")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        // Zurück bleibt nur die erste Kategorie; das Markdown behält die angehängten Angaben.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Seiten_Kategorien",
                table: "Seiten");

            foreach (var table in new[] { "Seiten", "SeitenVersionen" })
            {
                migrationBuilder.AddColumn<string>(
                    name: "Kategorie",
                    table: table,
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false,
                    defaultValue: "");

                migrationBuilder.Sql($"""UPDATE "{table}" SET "Kategorie" = left(coalesce("Kategorien"[1], ''), 200);""");

                migrationBuilder.DropColumn(
                    name: "Kategorien",
                    table: table);
            }

            migrationBuilder.CreateIndex(
                name: "IX_Seiten_Kategorie",
                table: "Seiten",
                column: "Kategorie");
        }
    }
}
