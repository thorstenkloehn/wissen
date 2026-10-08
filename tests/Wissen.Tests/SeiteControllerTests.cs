using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;
using Wissen.Web.Controllers;
using Wissen.Web.Models;

namespace Wissen.Tests;

public class SeiteControllerTests
{
    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Neu_SavesPageWithFirstVersionAndRedirects()
    {
        using var db = CreateContext();

        var result = await new SeiteController(db).Neu(new SeiteNeuViewModel
        {
            Path = " /doc/einleitung/ ",
            Kategorie = " Allgemein ",
            MarkdownInhalt = "# Titel",
        });

        Assert.Equal("/doc/einleitung", Assert.IsType<LocalRedirectResult>(result).Url);
        var seite = await db.Seiten.Include(s => s.Versionen).SingleAsync();
        Assert.Equal("doc/einleitung", seite.Path);
        Assert.Equal("Allgemein", seite.Kategorie);
        Assert.Equal("# Titel", seite.MarkdownInhalt);
        Assert.Contains("<h1", seite.Inhalt);
        var version = Assert.Single(seite.Versionen);
        Assert.Equal(1, version.Nummer);
        Assert.Equal(seite.Inhalt, version.Inhalt);
    }

    [Fact]
    public async Task Neu_EscapesHtmlInMarkdown()
    {
        using var db = CreateContext();

        await new SeiteController(db).Neu(new SeiteNeuViewModel
        {
            Path = "doc/x",
            Kategorie = "Allgemein",
            MarkdownInhalt = "<script>alert(1)</script>",
        });

        Assert.DoesNotContain("<script", (await db.Seiten.SingleAsync()).Inhalt);
    }

    [Fact]
    public async Task Neu_RejectsExistingPath()
    {
        using var db = CreateContext();
        db.Seiten.Add(new Seite { Path = "doc/x" });
        await db.SaveChangesAsync();
        var controller = new SeiteController(db);

        var result = await controller.Neu(new SeiteNeuViewModel
        {
            Path = "doc/x",
            Kategorie = "Allgemein",
            MarkdownInhalt = "Text",
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(1, await db.Seiten.CountAsync());
    }

    [Fact]
    public async Task Bearbeiten_UpdatesPageAndAddsVersion()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "alt" });
        var id = (await db.Seiten.SingleAsync()).Id;

        var result = await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { Kategorie = "Technik", MarkdownInhalt = "**neu**" });

        Assert.Equal("/doc/x", Assert.IsType<LocalRedirectResult>(result).Url);
        var seite = await db.Seiten.Include(s => s.Versionen).SingleAsync();
        Assert.Equal("Technik", seite.Kategorie);
        Assert.Equal("**neu**", seite.MarkdownInhalt);
        Assert.Contains("<strong>neu</strong>", seite.Inhalt);
        Assert.Equal([1, 2], seite.Versionen.Select(v => v.Nummer).Order());
        Assert.Equal("alt", seite.Versionen.Single(v => v.Nummer == 1).MarkdownInhalt);
        Assert.Equal("**neu**", seite.Versionen.Single(v => v.Nummer == 2).MarkdownInhalt);
    }

    [Fact]
    public async Task Bearbeiten_WithoutChange_AddsNoVersion()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "Text" });
        var id = (await db.Seiten.SingleAsync()).Id;

        await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { Kategorie = "Allgemein", MarkdownInhalt = "Text" });

        Assert.Equal(1, await db.SeitenVersionen.CountAsync());
    }

    [Fact]
    public async Task Bearbeiten_UnknownPage_ReturnsNotFound()
    {
        using var db = CreateContext();

        Assert.IsType<NotFoundResult>(await new SeiteController(db).Bearbeiten(42));
    }

    [Fact]
    public async Task Version_ReturnsRequestedVersion()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "alt" });
        var id = (await db.Seiten.SingleAsync()).Id;
        await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { Kategorie = "Allgemein", MarkdownInhalt = "neu" });

        var version = Assert.IsType<SeitenVersion>(Assert.IsType<ViewResult>(await controller.Version(id, 1)).Model);

        Assert.Equal("alt", version.MarkdownInhalt);
        Assert.Equal("doc/x", version.Seite.Path);
        Assert.IsType<NotFoundResult>(await controller.Version(id, 3));
    }

    [Fact]
    public async Task Doc_ListsVersionsNewestFirst()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "alt" });
        var id = (await db.Seiten.SingleAsync()).Id;
        await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { Kategorie = "Allgemein", MarkdownInhalt = "neu" });

        var seite = Assert.IsType<Seite>(Assert.IsType<ViewResult>(await new docController(db).HandleAll("doc/x")).Model);

        Assert.Equal([2, 1], seite.Versionen.Select(v => v.Nummer));
    }
}

public class SeiteNeuViewModelTests
{
    [Theory]
    [InlineData("doc", true)]
    [InlineData("doc/einleitung", true)]
    [InlineData(" /doc/Über-uns_2/ ", true)]
    [InlineData("doc//x", false)]
    [InlineData("doc/a b", false)]
    [InlineData("doc/<script>", false)]
    public void Path_IsValidated(string path, bool expected)
    {
        var model = new SeiteNeuViewModel { Path = path, Kategorie = "Allgemein", MarkdownInhalt = "Text" };

        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model, new System.ComponentModel.DataAnnotations.ValidationContext(model), null, validateAllProperties: true);

        Assert.Equal(expected, valid);
    }
}
