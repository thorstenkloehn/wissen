using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;
using Wissen.Infrastructure.Rendering;

namespace Wissen.Tests;

public class SeitenRendernTests
{
    private static ApplicationDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options);

    // Eine Seite mit veraltetem HTML (noch mit onclick und ohne Vorsatz an der id) und eine aktuelle.
    private static async Task<string> SeedAsync()
    {
        var name = Guid.NewGuid().ToString();
        using var db = CreateContext(name);
        db.Seiten.Add(new Seite
        {
            Path = "doc/alt",
            MarkdownInhalt = "# Titel",
            Inhalt = "<h1 id=\"titel\" onclick=\"alert(1)\">Titel</h1>",
            Versionen =
            [
                new SeitenVersion { Nummer = 1, MarkdownInhalt = "eins", Inhalt = MarkdownRenderer.ToHtml("eins"), Autor = "anna@example.org", ErstelltAm = new DateTime(2026, 10, 8, 15, 1, 0, DateTimeKind.Utc) },
                new SeitenVersion { Nummer = 2, MarkdownInhalt = "# Titel", Inhalt = "<h1 id=\"titel\" onclick=\"alert(1)\">Titel</h1>" },
            ],
        });
        db.Seiten.Add(new Seite { Path = "doc/neu", MarkdownInhalt = "neu", Inhalt = MarkdownRenderer.ToHtml("neu") });
        await db.SaveChangesAsync();
        return name;
    }

    [Fact]
    public async Task Run_TakesCategoriesFromMarkdown()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = CreateContext(name))
        {
            // Vor den Kategorien im Markdown stand die Angabe als Text im HTML.
            db.Seiten.Add(new Seite
            {
                Path = "doc/x",
                MarkdownInhalt = "Text\n\n[[Kategorie:A]] [[Kategorie:B]]",
                Inhalt = "<p>Text</p>\n<p>[[Kategorie:A]] [[Kategorie:B]]</p>\n",
                Kategorien = ["A"],
                Versionen = [new SeitenVersion { Nummer = 1, MarkdownInhalt = "[[Kategorie:C]]", Inhalt = "" }],
            });
            await db.SaveChangesAsync();
        }

        using (var db = CreateContext(name))
        {
            Assert.Equal(new SeitenRendernResult(1, 1, 1, 1, []), (await new SeitenRendern(db).RunAsync(speichern: true)) with { Fehler = [] });
        }

        using (var db = CreateContext(name))
        {
            var seite = await db.Seiten.Include(s => s.Versionen).SingleAsync();
            Assert.Equal(["A", "B"], seite.Kategorien);
            Assert.Equal("<p>Text</p>\n", seite.Inhalt);
            Assert.Equal(["C"], seite.Versionen.Single().Kategorien);
        }
    }

    [Fact]
    public async Task Run_SetsTitleOfPagesSavedBeforeTitles()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = CreateContext(name))
        {
            // HTML und Kategorien sind aktuell, nur der Titel fehlt noch.
            db.Seiten.Add(new Seite
            {
                Path = "blog/x",
                MarkdownInhalt = "# Artikel",
                Inhalt = MarkdownRenderer.ToHtml("# Artikel"),
                Versionen = [new SeitenVersion { Nummer = 1, MarkdownInhalt = "# Entwurf", Inhalt = MarkdownRenderer.ToHtml("# Entwurf") }],
            });
            await db.SaveChangesAsync();
        }

        using (var db = CreateContext(name))
        {
            var result = await new SeitenRendern(db).RunAsync(speichern: true);
            Assert.Equal(1, result.SeitenGeaendert);
            Assert.Equal(1, result.VersionenGeaendert);
        }

        using (var db = CreateContext(name))
        {
            var seite = await db.Seiten.Include(s => s.Versionen).SingleAsync();
            Assert.Equal("Artikel", seite.Titel);
            Assert.Equal("Entwurf", seite.Versionen.Single().Titel);
        }
    }

    [Fact]
    public async Task Run_RendersStoredHtmlAgainAndKeepsEverythingElse()
    {
        var name = await SeedAsync();

        SeitenRendernResult result;
        using (var db = CreateContext(name))
        {
            result = await new SeitenRendern(db).RunAsync(speichern: true);
        }

        Assert.Equal(new SeitenRendernResult(2, 1, 2, 1, []), result with { Fehler = [] });
        Assert.Empty(result.Fehler);
        using (var db = CreateContext(name))
        {
            var seite = await db.Seiten.Include(s => s.Versionen).SingleAsync(s => s.Path == "doc/alt");
            Assert.Equal(MarkdownRenderer.ToHtml("# Titel"), seite.Inhalt);
            Assert.Equal("# Titel", seite.MarkdownInhalt);
            Assert.Equal(2, seite.Versionen.Count);
            Assert.DoesNotContain("onclick", seite.Versionen.Single(v => v.Nummer == 2).Inhalt);
            var erste = seite.Versionen.Single(v => v.Nummer == 1);
            Assert.Equal("anna@example.org", erste.Autor);
            Assert.Equal(new DateTime(2026, 10, 8, 15, 1, 0, DateTimeKind.Utc), erste.ErstelltAm);
        }
    }

    [Fact]
    public async Task Run_OnlyCountsWithoutSaving()
    {
        var name = await SeedAsync();

        using (var db = CreateContext(name))
        {
            var result = await new SeitenRendern(db).RunAsync(speichern: false);
            Assert.Equal((1, 1), (result.SeitenGeaendert, result.VersionenGeaendert));
        }

        using (var db = CreateContext(name))
        {
            Assert.Contains("onclick", (await db.Seiten.SingleAsync(s => s.Path == "doc/alt")).Inhalt);
        }
    }

    [Fact]
    public async Task Run_ReportsWhatCannotBeRenderedAndKeepsItsHtml()
    {
        var name = Guid.NewGuid().ToString();
        using (var db = CreateContext(name))
        {
            db.Seiten.Add(new Seite { Path = "doc/tief", MarkdownInhalt = new string('>', 10_000), Inhalt = "<p>alt</p>" });
            db.Seiten.Add(new Seite { Path = "doc/x", MarkdownInhalt = "x", Inhalt = "" });
            await db.SaveChangesAsync();
        }

        using (var db = CreateContext(name))
        {
            var result = await new SeitenRendern(db).RunAsync(speichern: true);
            Assert.Contains("doc/tief", Assert.Single(result.Fehler));
            Assert.Equal(1, result.SeitenGeaendert);
        }

        using (var db = CreateContext(name))
        {
            Assert.Equal("<p>alt</p>", (await db.Seiten.SingleAsync(s => s.Path == "doc/tief")).Inhalt);
            Assert.Equal(MarkdownRenderer.ToHtml("x"), (await db.Seiten.SingleAsync(s => s.Path == "doc/x")).Inhalt);
        }
    }
}
