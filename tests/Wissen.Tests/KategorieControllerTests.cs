using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;
using Wissen.Web.Controllers;
using Wissen.Web.Models;

namespace Wissen.Tests;

public class KategorieControllerTests
{
    private static async Task<ApplicationDbContext> CreateContextAsync()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Seiten.AddRange(
            new Seite { Path = "doc/zwei", Kategorien = ["Technik", "Hauptseite"] },
            new Seite { Path = "doc/eins", Kategorien = ["Technik"] },
            new Seite { Path = "doc/ohne" },
            // Nur die Kategorien der Seite zählen, nicht die früherer Versionen.
            new Seite { Path = "doc/alt", Versionen = [new SeitenVersion { Nummer = 1, Kategorien = ["Alt"] }] });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Seiten_ListsPagesOfCategorySorted()
    {
        using var db = await CreateContextAsync();

        var result = Assert.IsType<ViewResult>(await new KategorieController(db).Seiten("Technik"));

        var model = Assert.IsType<KategorieViewModel>(result.Model);
        Assert.Equal("Technik", model.Name);
        Assert.Equal(["doc/eins", "doc/zwei"], model.Pfade);
        Assert.Null(result.StatusCode);
    }

    [Theory]
    [InlineData("Alt")]
    [InlineData("technik")]
    [InlineData("Unbekannt")]
    public async Task Seiten_IsNotFoundWithoutPages(string name)
    {
        using var db = await CreateContextAsync();

        var result = Assert.IsType<ViewResult>(await new KategorieController(db).Seiten(name));

        Assert.Empty(Assert.IsType<KategorieViewModel>(result.Model).Pfade);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact]
    public async Task Index_ListsCategoriesWithCount()
    {
        using var db = await CreateContextAsync();

        var result = Assert.IsType<ViewResult>(await new KategorieController(db).Index());

        Assert.Equal([new KategorieAnzahl("Hauptseite", 1), new KategorieAnzahl("Technik", 2)], Assert.IsType<List<KategorieAnzahl>>(result.Model));
    }

    [Theory]
    [InlineData("Hauptseite", "/kategorie/Hauptseite")]
    [InlineData("Haupt Seite", "/kategorie/Haupt%20Seite")]
    [InlineData("A/B?#", "/kategorie/A/B%3F%23")]
    [InlineData("Übersicht", "/kategorie/%C3%9Cbersicht")]
    public void Adresse_EncodesName(string name, string adresse)
    {
        Assert.Equal(adresse, KategorieController.Adresse(name));
    }

    [Theory]
    [InlineData("kategorie", true)]
    [InlineData("Kategorie/x", true)]
    [InlineData("kategorien", false)]
    [InlineData("doc/kategorie", false)]
    public void IstReserviert(string path, bool reserviert)
    {
        Assert.Equal(reserviert, KategorieController.IstReserviert(path));
    }

    [Fact]
    public async Task SeiteNeu_RejectsPathOfCategoryPages()
    {
        using var db = await CreateContextAsync();
        var controller = new SeiteController(db);

        var result = await controller.Neu(new SeiteNeuViewModel { Path = "Kategorie/Technik", MarkdownInhalt = "x" });

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(nameof(SeiteNeuViewModel.Path)));
        Assert.Equal(4, await db.Seiten.CountAsync());
    }
}
