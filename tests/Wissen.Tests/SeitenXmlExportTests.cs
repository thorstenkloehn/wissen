using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Backup;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;

namespace Wissen.Tests;

public class SeitenXmlExportTests
{
    [Fact]
    public async Task Create_ContainsPagesWithVersionHistory()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Seiten.Add(new Seite
        {
            Path = "doc/x",
            Kategorie = "Technik",
            MarkdownInhalt = "# Neu\n\n<b> & Zeile\u0001",
            Inhalt = "<h1>Neu</h1>",
            Versionen =
            [
                new SeitenVersion { Nummer = 2, Kategorie = "Technik", MarkdownInhalt = "# Neu", Autor = "anna@example.org", ErstelltAm = new DateTime(2026, 10, 8, 15, 11, 0, DateTimeKind.Utc) },
                new SeitenVersion { Nummer = 1, Kategorie = "Allgemein", MarkdownInhalt = "alt", ErstelltAm = new DateTime(2026, 10, 8, 15, 1, 0, DateTimeKind.Utc) },
            ],
        });
        db.Seiten.Add(new Seite { Path = "doc/a", Kategorie = "Allgemein", MarkdownInhalt = "a" });
        await db.SaveChangesAsync();

        var document = await new SeitenXmlExport(db).CreateAsync();

        var seiten = document.Root!.Elements("seite").ToList();
        Assert.Equal(["doc/a", "doc/x"], seiten.Select(s => s.Element("path")!.Value));
        var seite = seiten[1];
        Assert.NotNull(seite.Attribute("id"));
        Assert.Equal("Technik", seite.Element("kategorie")!.Value);
        Assert.Equal("# Neu\n\n<b> & Zeile", seite.Element("markdown")!.Value);
        Assert.Null(seite.Element("inhalt"));
        var versionen = seite.Element("versionsgeschichte")!.Elements("version").ToList();
        Assert.Equal(["1", "2"], versionen.Select(v => v.Attribute("nummer")!.Value));
        Assert.Equal("alt", versionen[0].Element("markdown")!.Value);
        Assert.Equal("Allgemein", versionen[0].Element("kategorie")!.Value);
        Assert.Equal("2026-10-08T15:01:00Z", versionen[0].Attribute("erstelltAm")!.Value);
        Assert.Null(versionen[0].Attribute("autor"));
        Assert.Equal("anna@example.org", versionen[1].Attribute("autor")!.Value);

        // Das Dokument lässt sich als XML schreiben und wieder lesen.
        var gelesen = System.Xml.Linq.XDocument.Parse(document.ToString());
        Assert.Equal(2, gelesen.Root!.Elements("seite").Count());
    }
}
