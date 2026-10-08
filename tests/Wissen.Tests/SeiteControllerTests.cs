using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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

    [Theory]
    [InlineData("[x](javascript:alert(1))", "javascript:")]
    [InlineData("<javascript:alert(1)>", "href=\"javascript:")]
    [InlineData("![b](javascript:alert(1))", "javascript:")]
    [InlineData("# Titel {onclick=alert(1)}", "onclick")]
    [InlineData("[x](http://a.example){onmouseover=alert(1)}", "onmouseover")]
    public async Task Neu_RemovesScriptFromMarkdown(string markdown, string forbidden)
    {
        using var db = CreateContext();

        await new SeiteController(db).Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = markdown });

        Assert.DoesNotContain(forbidden, (await db.Seiten.SingleAsync()).Inhalt);
    }

    [Fact]
    public async Task Neu_KeepsOrdinaryMarkdown()
    {
        using var db = CreateContext();

        await new SeiteController(db).Neu(new SeiteNeuViewModel
        {
            Path = "doc/x",
            Kategorie = "Allgemein",
            MarkdownInhalt = "# Titel\n\n[Link](https://example.org) und [Mail](mailto:a@example.org)\n\n| a | b |\n|---|---|\n| 1 | 2 |",
        });

        var inhalt = (await db.Seiten.SingleAsync()).Inhalt;
        Assert.Contains("<h1 id=\"titel\">Titel</h1>", inhalt);
        Assert.Contains("href=\"https://example.org\"", inhalt);
        Assert.Contains("href=\"mailto:a@example.org\"", inhalt);
        Assert.Contains("<table>", inhalt);
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
    public async Task Zuruecksetzen_RestoresOldStateAsNewVersion()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "alt" });
        var id = (await db.Seiten.SingleAsync()).Id;
        await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { Kategorie = "Technik", MarkdownInhalt = "neu" });

        var result = await controller.Zuruecksetzen(id, 1);

        Assert.Equal("/doc/x", Assert.IsType<LocalRedirectResult>(result).Url);
        var seite = await db.Seiten.Include(s => s.Versionen).SingleAsync();
        Assert.Equal("alt", seite.MarkdownInhalt);
        Assert.Equal("Allgemein", seite.Kategorie);
        Assert.Contains("alt", seite.Inhalt);
        Assert.Equal([1, 2, 3], seite.Versionen.Select(v => v.Nummer).Order());
        Assert.Equal("neu", seite.Versionen.Single(v => v.Nummer == 2).MarkdownInhalt);
        Assert.Equal("alt", seite.Versionen.Single(v => v.Nummer == 3).MarkdownInhalt);
    }

    [Fact]
    public async Task Zuruecksetzen_ToCurrentState_AddsNoVersion()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "Text" });
        var id = (await db.Seiten.SingleAsync()).Id;

        await controller.Zuruecksetzen(id, 1);

        Assert.Equal(1, await db.SeitenVersionen.CountAsync());
        Assert.IsType<NotFoundResult>(await controller.Zuruecksetzen(id, 7));
    }

    [Fact]
    public async Task Loeschen_RemovesPageAndVersions()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "alt" });
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/y", Kategorie = "Allgemein", MarkdownInhalt = "bleibt" });
        var id = (await db.Seiten.SingleAsync(s => s.Path == "doc/x")).Id;
        await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { Kategorie = "Allgemein", MarkdownInhalt = "neu" });

        var result = await controller.LoeschenBestaetigt(id);

        Assert.Equal("/doc/x", Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Equal("doc/y", (await db.Seiten.SingleAsync()).Path);
        Assert.Equal("bleibt", (await db.SeitenVersionen.SingleAsync()).MarkdownInhalt);
        Assert.IsType<NotFoundResult>(await controller.LoeschenBestaetigt(id));
    }

    [Fact]
    public async Task Doc_ListsVersionsNewestFirst()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "alt" });
        var id = (await db.Seiten.SingleAsync()).Id;
        await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { Kategorie = "Allgemein", MarkdownInhalt = "neu" });

        var seite = Assert.IsType<Seite>(Assert.IsType<ViewResult>(await CreateDocController(db, angemeldet: true).HandleAll("doc/x")).Model);

        Assert.Equal([2, 1], seite.Versionen.Select(v => v.Nummer));
    }

    [Fact]
    public async Task Doc_Anonymous_GetsContentWithoutVersions()
    {
        using var db = CreateContext();
        await new SeiteController(db).Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "Text" });

        var seite = Assert.IsType<Seite>(Assert.IsType<ViewResult>(await CreateDocController(db, angemeldet: false).HandleAll("doc/x")).Model);

        Assert.Contains("Text", seite.Inhalt);
        Assert.Empty(seite.Versionen);
    }

    private static docController CreateDocController(ApplicationDbContext db, bool angemeldet) =>
        new(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(angemeldet ? new ClaimsIdentity("Test") : new ClaimsIdentity()),
                },
            },
        };

    // 200 000 Zeichen, aus denen über 3 Millionen Zeichen HTML würden.
    private static readonly string ZuGrossesMarkdown = string.Concat(Enumerable.Repeat("[^1]", 49_990)) + "\n\n[^1]: x";

    [Fact]
    public async Task Neu_RejectsMarkdownThatCannotBeRendered()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);

        var result = await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = new string('>', 10_000) });

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(nameof(SeiteNeuViewModel.MarkdownInhalt)));
        Assert.Equal(0, await db.Seiten.CountAsync());
    }

    [Fact]
    public async Task Neu_RejectsMarkdownWithTooMuchHtml()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);

        var result = await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = ZuGrossesMarkdown });

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(nameof(SeiteNeuViewModel.MarkdownInhalt)));
        Assert.Equal(0, await db.Seiten.CountAsync());
    }

    [Fact]
    public async Task Bearbeiten_LeavesPageUntouchedWhenMarkdownCannotBeRendered()
    {
        using var db = CreateContext();
        var controller = new SeiteController(db);
        await controller.Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "alt" });
        var id = (await db.Seiten.SingleAsync()).Id;

        var result = await controller.Bearbeiten(id, new SeiteBearbeitenViewModel { Kategorie = "Technik", MarkdownInhalt = ZuGrossesMarkdown });

        var model = Assert.IsType<SeiteBearbeitenViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("doc/x", model.Path);
        Assert.True(controller.ModelState.ContainsKey(nameof(SeiteBearbeitenViewModel.MarkdownInhalt)));
        db.ChangeTracker.Clear();
        var seite = await db.Seiten.Include(s => s.Versionen).SingleAsync();
        Assert.Equal("Allgemein", seite.Kategorie);
        Assert.Equal("alt", seite.MarkdownInhalt);
        Assert.Single(seite.Versionen);
    }

    private static SeiteController AngemeldetAls(ApplicationDbContext db, string name) =>
        new(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Test")),
                },
            },
        };

    [Fact]
    public async Task EveryVersionRecordsTheAccountThatSavedIt()
    {
        using var db = CreateContext();
        await AngemeldetAls(db, "anna@example.org").Neu(new SeiteNeuViewModel { Path = "doc/x", Kategorie = "Allgemein", MarkdownInhalt = "eins" });
        var id = (await db.Seiten.SingleAsync()).Id;
        await AngemeldetAls(db, "bert@example.org").Bearbeiten(id, new SeiteBearbeitenViewModel { Kategorie = "Allgemein", MarkdownInhalt = "zwei" });
        await AngemeldetAls(db, "carla@example.org").Zuruecksetzen(id, 1);

        var autoren = await db.SeitenVersionen.OrderBy(v => v.Nummer).Select(v => v.Autor).ToListAsync();
        Assert.Equal(["anna@example.org", "bert@example.org", "carla@example.org"], autoren);
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
