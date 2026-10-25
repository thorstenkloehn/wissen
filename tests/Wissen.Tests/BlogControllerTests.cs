using System.Xml.Linq;
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

        var artikel = Assert.IsType<List<BlogArtikel>>(result.Model);
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
}
