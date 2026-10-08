using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Backup;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;

namespace Wissen.Tests;

public class SeitenXmlImportTests
{
    private static ApplicationDbContext CreateContext(string? name = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options);

    private const string Xml = """
        <seiten erstelltAm="2026-10-08T15:51:37Z">
          <seite id="7">
            <path>doc/x</path>
            <kategorie>Technik</kategorie>
            <markdown># Neu</markdown>
            <versionsgeschichte>
              <version nummer="1" erstelltAm="2026-10-08T15:01:00Z">
                <kategorie>Allgemein</kategorie>
                <markdown>alt</markdown>
              </version>
              <version nummer="2" erstelltAm="2026-10-08T15:11:00Z">
                <kategorie>Technik</kategorie>
                <markdown># Neu</markdown>
              </version>
            </versionsgeschichte>
          </seite>
        </seiten>
        """;

    [Fact]
    public async Task Import_AddsPageWithHistoryAndRenderedHtml()
    {
        using var db = CreateContext();
        var import = new SeitenXmlImport(db);

        var result = await import.ImportAsync(import.Read(XDocument.Parse(Xml)));

        Assert.Equal(new SeitenXmlImportResult(1, 0), result);
        var seite = await db.Seiten.Include(s => s.Versionen).SingleAsync();
        Assert.Equal("doc/x", seite.Path);
        Assert.Equal("Technik", seite.Kategorie);
        Assert.Equal("# Neu", seite.MarkdownInhalt);
        Assert.Contains("<h1", seite.Inhalt);
        Assert.Equal([1, 2], seite.Versionen.Select(v => v.Nummer).Order());
        var erste = seite.Versionen.Single(v => v.Nummer == 1);
        Assert.Equal("alt", erste.MarkdownInhalt);
        Assert.Equal("Allgemein", erste.Kategorie);
        Assert.Equal(new DateTime(2026, 10, 8, 15, 1, 0, DateTimeKind.Utc), erste.ErstelltAm);
        Assert.Equal(DateTimeKind.Utc, erste.ErstelltAm.Kind);
    }

    [Fact]
    public async Task Import_ReplacesExistingPageAndKeepsOthers()
    {
        var name = Guid.NewGuid().ToString();
        int id;
        using (var db = CreateContext(name))
        {
            db.Seiten.Add(new Seite
            {
                Path = "doc/x",
                Kategorie = "Vorher",
                MarkdownInhalt = "vorher",
                Versionen = [new SeitenVersion { Nummer = 1, MarkdownInhalt = "vorher" }, new SeitenVersion { Nummer = 5, MarkdownInhalt = "weg" }],
            });
            db.Seiten.Add(new Seite { Path = "doc/bleibt", Kategorie = "Allgemein", MarkdownInhalt = "bleibt" });
            await db.SaveChangesAsync();
            id = (await db.Seiten.SingleAsync(s => s.Path == "doc/x")).Id;
        }

        using (var db = CreateContext(name))
        {
            var import = new SeitenXmlImport(db);
            var seiten = import.Read(XDocument.Parse(Xml));

            Assert.Equal(["doc/x"], await import.FindExistingPathsAsync(seiten));
            Assert.Equal(new SeitenXmlImportResult(0, 1), await import.ImportAsync(seiten));
        }

        using (var db = CreateContext(name))
        {
            var seite = await db.Seiten.Include(s => s.Versionen).SingleAsync(s => s.Path == "doc/x");
            Assert.Equal(id, seite.Id);
            Assert.Equal("# Neu", seite.MarkdownInhalt);
            Assert.Equal([1, 2], seite.Versionen.Select(v => v.Nummer).Order());
            Assert.Equal("alt", seite.Versionen.Single(v => v.Nummer == 1).MarkdownInhalt);
            Assert.Equal("bleibt", (await db.Seiten.SingleAsync(s => s.Path == "doc/bleibt")).MarkdownInhalt);
            Assert.Equal(2, await db.SeitenVersionen.CountAsync());
        }
    }

    [Fact]
    public async Task ExportThenImport_RoundTrips()
    {
        using var quelle = CreateContext();
        var import = new SeitenXmlImport(quelle);
        await import.ImportAsync(import.Read(XDocument.Parse(Xml)));
        var exportiert = await new SeitenXmlExport(quelle).CreateAsync();

        using var ziel = CreateContext();
        var zielImport = new SeitenXmlImport(ziel);
        await zielImport.ImportAsync(zielImport.Read(XDocument.Parse(exportiert.ToString())));
        var erneut = await new SeitenXmlExport(ziel).CreateAsync();

        Assert.Equal(
            exportiert.Root!.Elements("seite").Select(s => s.ToString()),
            erneut.Root!.Elements("seite").Select(s => s.ToString()));
    }

    [Fact]
    public void Read_RemovesScriptFromImportedMarkdown()
    {
        using var db = CreateContext();
        var xml = Xml.Replace("<markdown># Neu</markdown>", "<markdown>[x](javascript:alert(1)) # T {onclick=alert(1)}</markdown>");

        var seite = Assert.Single(new SeitenXmlImport(db).Read(XDocument.Parse(xml)));

        Assert.DoesNotContain("javascript:", seite.Inhalt);
        Assert.DoesNotContain("onclick", seite.Inhalt);
        Assert.All(seite.Versionen, v => Assert.DoesNotContain("javascript:", v.Inhalt));
    }

    [Theory]
    [InlineData("<seiten ", "<artikel ")]
    [InlineData("<path>doc/x</path>", "")]
    [InlineData("<path>doc/x</path>", "<path>doc/a b</path>")]
    [InlineData("<path>doc/x</path>", "<path>../etc</path>")]
    [InlineData("<kategorie>Technik</kategorie>\n    <markdown># Neu</markdown>\n    <versionsgeschichte>", "<kategorie>Technik</kategorie>\n    <versionsgeschichte>")]
    [InlineData("nummer=\"2\"", "nummer=\"1\"")]
    [InlineData("nummer=\"2\"", "nummer=\"zwei\"")]
    [InlineData("erstelltAm=\"2026-10-08T15:11:00Z\"", "erstelltAm=\"gestern\"")]
    [InlineData("erstelltAm=\"2026-10-08T15:11:00Z\"", "")]
    public void Read_RejectsInvalidFile(string old, string replacement)
    {
        using var db = CreateContext();
        var xml = Xml.Replace(old, replacement).Replace("</seiten>", replacement == "<artikel " ? "</artikel>" : "</seiten>");
        Assert.NotEqual(Xml, xml);

        Assert.Throws<FormatException>(() => new SeitenXmlImport(db).Read(XDocument.Parse(xml)));
    }

    [Fact]
    public void Read_RejectsDuplicatePath()
    {
        using var db = CreateContext();
        var seite = XDocument.Parse(Xml).Root!.Element("seite")!;
        var doppelt = new XDocument(new XElement("seiten", seite, seite));

        Assert.Throws<FormatException>(() => new SeitenXmlImport(db).Read(doppelt));
    }
}
