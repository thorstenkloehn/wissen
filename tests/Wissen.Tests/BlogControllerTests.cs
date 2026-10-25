using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Backup;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;
using Wissen.Web.Controllers;
using Wissen.Web.Models;

namespace Wissen.Tests;

public class BlogControllerTests
{
    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SeitenVersion Version(int nummer, int tag) =>
        new() { Nummer = nummer, ErstelltAm = new DateTime(2026, 10, tag, 12, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public async Task Index_ListsOnlyArticlesNewestFirstByFirstVersion()
    {
        using var db = CreateContext();
        db.Seiten.AddRange(
            new Seite { Path = "blog/alt", Typ = Inhaltstyp.Blogartikel, Kategorien = ["Technik"], Versionen = [Version(1, 1), Version(2, 20)] },
            new Seite { Path = "blog/neu", Typ = Inhaltstyp.Blogartikel, Versionen = [Version(1, 10)] },
            new Seite { Path = "doc/seite", Versionen = [Version(1, 15)] });
        await db.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await new BlogController(db).Index());

        var uebersicht = Assert.IsType<BlogUebersicht>(result.Model);
        Assert.Equal((1, 1), (uebersicht.Seite, uebersicht.Seiten));
        var artikel = uebersicht.Artikel;
        Assert.Equal(["blog/neu", "blog/alt"], artikel.Select(a => a.Pfad));
        Assert.Equal(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc), artikel[1].VeroeffentlichtAm);
        Assert.Equal(["Technik"], artikel[1].Kategorien);
    }

    [Theory]
    [InlineData("blog", true)]
    [InlineData("Blog", true)]
    [InlineData("blog/artikel", false)]
    [InlineData("doc/blog", false)]
    public void IstReserviert(string path, bool reserviert)
    {
        Assert.Equal(reserviert, BlogController.IstReserviert(path));
    }

    [Theory]
    [InlineData("blog/x", "Mein Artikel", "Mein Artikel")]
    [InlineData("blog/mein-erster_artikel", null, "mein erster artikel")]
    [InlineData("notiz", null, "notiz")]
    public void Titel_UsesHeadingOrLastPathSegment(string path, string? ueberschrift, string titel)
    {
        Assert.Equal(titel, BlogController.Titel(new BlogArtikel(path, ueberschrift, null, [])));
    }

    [Fact]
    public async Task SeiteNeuUndBearbeiten_StoreFirstHeadingAsTitle()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "blog/eins", MarkdownInhalt = "# Erster Titel\n\nText", Typ = Inhaltstyp.Blogartikel });
        var id = (await db.Seiten.SingleAsync()).Id;
        Assert.Equal("Erster Titel", (await db.Seiten.SingleAsync()).Titel);

        await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { MarkdownInhalt = "Ohne Überschrift", Typ = Inhaltstyp.Blogartikel });

        Assert.Null((await db.Seiten.AsNoTracking().SingleAsync()).Titel);
        // Jede Version behält die Überschrift ihres Stands.
        var versionen = await db.SeitenVersionen.OrderBy(v => v.Nummer).Select(v => v.Titel).ToListAsync();
        Assert.Equal(["Erster Titel", null], versionen);
    }

    [Fact]
    public async Task SeiteNeu_RejectsPathOfOverview()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);

        var result = await controller.Neu(new SeiteNeuViewModel { Path = "Blog", MarkdownInhalt = "x", Typ = Inhaltstyp.Blogartikel });

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(nameof(SeiteNeuViewModel.Path)));
        Assert.Empty(db.Seiten);
    }

    [Fact]
    public async Task SeiteNeu_SavesType()
    {
        using var db = CreateContext();

        await new SeiteController(db).Neu(new SeiteNeuViewModel { Path = "blog/eins", MarkdownInhalt = "x", Typ = Inhaltstyp.Blogartikel });

        Assert.Equal(Inhaltstyp.Blogartikel, (await db.Seiten.SingleAsync()).Typ);
    }

    [Fact]
    public async Task Bearbeiten_ChangesTypeWithoutNewVersion()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", MarkdownInhalt = "Text" });
        var id = (await db.Seiten.SingleAsync()).Id;

        await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { MarkdownInhalt = "Text", Typ = Inhaltstyp.Blogartikel });

        Assert.Equal(Inhaltstyp.Blogartikel, (await db.Seiten.AsNoTracking().SingleAsync()).Typ);
        Assert.Equal(1, await db.SeitenVersionen.CountAsync());
    }

    [Fact]
    public async Task Bearbeiten_ShowsTypeOfPage()
    {
        using var db = CreateContext();
        db.Seiten.Add(new Seite { Path = "blog/x", Typ = Inhaltstyp.Blogartikel });
        await db.SaveChangesAsync();

        var result = Assert.IsType<ViewResult>(await new SeiteController(db).Bearbeiten((await db.Seiten.SingleAsync()).Id));

        Assert.Equal(Inhaltstyp.Blogartikel, Assert.IsType<SeiteBearbeitenViewModel>(result.Model).Typ);
    }

    [Fact]
    public async Task XmlBackup_KeepsType()
    {
        using var db = CreateContext();
        db.Seiten.AddRange(
            new Seite { Path = "blog/x", MarkdownInhalt = "x", Typ = Inhaltstyp.Blogartikel },
            new Seite { Path = "doc/y", MarkdownInhalt = "y" });
        await db.SaveChangesAsync();

        var document = await new SeitenXmlExport(db).CreateAsync();
        var elemente = document.Root!.Elements("seite").ToList();
        Assert.Equal("blogartikel", elemente[0].Attribute("typ")!.Value);
        Assert.Null(elemente[1].Attribute("typ"));

        var seiten = new SeitenXmlImport(CreateContext()).Read(XDocument.Parse(document.ToString()));
        Assert.Equal([Inhaltstyp.Blogartikel, Inhaltstyp.Seite], seiten.Select(s => s.Typ));
    }

    [Fact]
    public void XmlImport_RejectsUnknownType()
    {
        var document = XDocument.Parse("<seiten><seite typ=\"roman\"><path>doc/x</path><markdown>x</markdown></seite></seiten>");

        Assert.Throws<FormatException>(() => new SeitenXmlImport(CreateContext()).Read(document));
    }

    [Fact]
    public async Task Feed_ContainsNewestArticlesWithAbsoluteLinks()
    {
        using var db = CreateContext();
        db.Seiten.AddRange(
            new Seite { Path = "blog/alt", Titel = "Alter Artikel", Inhalt = "<p>alt</p>", Typ = Inhaltstyp.Blogartikel, Kategorien = ["Technik"], Versionen = [Version(1, 1)] },
            new Seite { Path = "blog/über", Inhalt = "<p>neu</p>", Typ = Inhaltstyp.Blogartikel, Versionen = [Version(1, 10)] },
            new Seite { Path = "doc/seite", Versionen = [Version(1, 15)] });
        for (var i = 0; i < BlogController.FeedArtikel; i++)
        {
            db.Seiten.Add(new Seite { Path = $"blog/frueh-{i}", Typ = Inhaltstyp.Blogartikel, Versionen = [new SeitenVersion { Nummer = 1, ErstelltAm = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) }] });
        }
        await db.SaveChangesAsync();
        var controller = new BlogController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        controller.Request.Scheme = "https";
        controller.Request.Host = new HostString("wissen.example");

        var result = Assert.IsType<ContentResult>(await controller.Feed());

        Assert.StartsWith("application/rss+xml", result.ContentType);
        Assert.StartsWith("<?xml", result.Content);
        var items = XDocument.Parse(result.Content!).Root!.Element("channel")!.Elements("item").ToList();
        Assert.Equal(BlogController.FeedArtikel, items.Count);
        Assert.Equal(["über", "Alter Artikel"], items.Take(2).Select(i => i.Element("title")!.Value));
        Assert.Equal("https://wissen.example/blog/%C3%BCber", items[0].Element("link")!.Value);
        Assert.Equal("<p>neu</p>", items[0].Element("description")!.Value);
        Assert.Equal("<p>alt</p>", items[1].Element("description")!.Value);
        Assert.Equal("Thu, 01 Oct 2026 12:00:00 GMT", items[1].Element("pubDate")!.Value);
        Assert.Equal(["Technik"], items[1].Elements("category").Select(c => c.Value));
    }

    [Theory]
    [InlineData("<a href=\"/doc/x\">x</a>", "<a href=\"https://wissen.example/doc/x\">x</a>")]
    [InlineData("<img src=\"bild.png\" alt=\"b\">", "<img src=\"https://wissen.example/blog/bild.png\" alt=\"b\">")]
    [InlineData("<a href=\"#inhalt-fn1\">1</a>", "<a href=\"https://wissen.example/blog/artikel#inhalt-fn1\">1</a>")]
    [InlineData("<a href=\"https://fremd.example/a?b=1\">f</a>", "<a href=\"https://fremd.example/a?b=1\">f</a>")]
    [InlineData("<a href=\"mailto:a@example.org\">m</a>", "<a href=\"mailto:a@example.org\">m</a>")]
    [InlineData("<p>ohne &amp; Link</p>", "<p>ohne &amp; Link</p>")]
    public void AbsoluteAdressen_ResolvesAgainstArticle(string html, string erwartet)
    {
        Assert.Equal(erwartet, BlogController.AbsoluteAdressen(html, new Uri("https://wissen.example/blog/artikel")));
    }

    [Fact]
    public async Task Index_SplitsArticlesIntoPages()
    {
        using var db = CreateContext();
        // Artikel 0 ist der älteste, der letzte der neueste.
        for (var i = 0; i < BlogController.ArtikelProSeite + 5; i++)
        {
            db.Seiten.Add(new Seite { Path = $"blog/a{i:D2}", Typ = Inhaltstyp.Blogartikel, Versionen = [new SeitenVersion { Nummer = 1, ErstelltAm = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i) }] });
        }
        await db.SaveChangesAsync();
        var controller = new BlogController(db);

        var erste = Assert.IsType<BlogUebersicht>(Assert.IsType<ViewResult>(await controller.Index()).Model);
        var zweite = Assert.IsType<BlogUebersicht>(Assert.IsType<ViewResult>(await controller.Index(2)).Model);

        Assert.Equal(BlogController.ArtikelProSeite, erste.Artikel.Count);
        Assert.Equal($"blog/a{BlogController.ArtikelProSeite + 4:D2}", erste.Artikel[0].Pfad);
        Assert.Equal((2, 2), (zweite.Seite, zweite.Seiten));
        Assert.Equal(["blog/a04", "blog/a03", "blog/a02", "blog/a01", "blog/a00"], zweite.Artikel.Select(a => a.Pfad));
        Assert.IsType<NotFoundResult>(await controller.Index(3));
        Assert.IsType<NotFoundResult>(await controller.Index(0));
    }

    [Fact]
    public async Task Index_ShowsFirstPageWithoutArticles()
    {
        using var db = CreateContext();

        var uebersicht = Assert.IsType<BlogUebersicht>(Assert.IsType<ViewResult>(await new BlogController(db).Index()).Model);

        Assert.Empty(uebersicht.Artikel);
        Assert.Equal(1, uebersicht.Seiten);
        Assert.IsType<NotFoundResult>(await new BlogController(db).Index(2));
    }

    [Theory]
    [InlineData(1, 1, new int[] { 1 })]
    [InlineData(1, 3, new int[] { 1, 2, 3 })]
    [InlineData(1, 10, new int[] { 1, 2, 3, 0, 10 })]
    [InlineData(6, 10, new int[] { 1, 0, 4, 5, 6, 7, 8, 0, 10 })]
    [InlineData(4, 10, new int[] { 1, 2, 3, 4, 5, 6, 0, 10 })]
    public void Seitennummern_ShowsEdgesAndNeighbours(int seite, int seiten, int[] erwartet)
    {
        // 0 steht hier für eine Lücke.
        Assert.Equal(erwartet, BlogController.Seitennummern(seite, seiten).Select(n => n ?? 0));
    }

    [Theory]
    [InlineData(1, "/blog")]
    [InlineData(3, "/blog?seite=3")]
    public void UebersichtAdresse(int seite, string adresse)
    {
        Assert.Equal(adresse, BlogController.UebersichtAdresse(seite));
    }
}
