using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;
using Wissen.Web.Models;

namespace Wissen.Web.Controllers;

[Authorize]
[Route("seite")]
public class SeiteController(ApplicationDbContext db) : Controller
{
    // HTML im Markdown wird maskiert, damit Seite.Inhalt gefahrlos roh ausgegeben werden kann.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    [HttpGet("neu")]
    public IActionResult Neu(string? path)
    {
        return View(new SeiteNeuViewModel { Path = path });
    }

    [HttpPost("neu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Neu(SeiteNeuViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var path = model.Path!.Trim().Trim('/');
        if (await db.Seiten.AnyAsync(s => s.Path == path))
        {
            ModelState.AddModelError(nameof(model.Path), "Unter diesem Pfad gibt es bereits eine Seite.");
            return View(model);
        }

        var kategorie = model.Kategorie!.Trim();
        var inhalt = Markdown.ToHtml(model.MarkdownInhalt!, Pipeline);

        var seite = new Seite
        {
            Path = path,
            Kategorie = kategorie,
            MarkdownInhalt = model.MarkdownInhalt!,
            Inhalt = inhalt,
        };
        seite.Versionen.Add(new SeitenVersion
        {
            Nummer = 1,
            Kategorie = kategorie,
            MarkdownInhalt = model.MarkdownInhalt!,
            Inhalt = inhalt,
        });

        db.Seiten.Add(seite);
        await db.SaveChangesAsync();

        return RedirectToSeite(path);
    }

    [HttpGet("bearbeiten/{id:int}")]
    public async Task<IActionResult> Bearbeiten(int id)
    {
        var seite = await db.Seiten.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (seite is null)
        {
            return NotFound();
        }

        return View(new SeiteBearbeitenViewModel
        {
            Path = seite.Path,
            Kategorie = seite.Kategorie,
            MarkdownInhalt = seite.MarkdownInhalt,
        });
    }

    [HttpPost("bearbeiten/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bearbeiten(int id, SeiteBearbeitenViewModel model)
    {
        var seite = await db.Seiten.FirstOrDefaultAsync(s => s.Id == id);
        if (seite is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.Path = seite.Path;
            return View(model);
        }

        var kategorie = model.Kategorie!.Trim();
        // Ohne Änderung entsteht keine neue Version.
        if (kategorie != seite.Kategorie || model.MarkdownInhalt != seite.MarkdownInhalt)
        {
            var letzteNummer = await db.SeitenVersionen
                .Where(v => v.SeiteId == id)
                .MaxAsync(v => (int?)v.Nummer) ?? 0;

            seite.Kategorie = kategorie;
            seite.MarkdownInhalt = model.MarkdownInhalt!;
            seite.Inhalt = Markdown.ToHtml(model.MarkdownInhalt!, Pipeline);
            db.SeitenVersionen.Add(new SeitenVersion
            {
                SeiteId = id,
                Nummer = letzteNummer + 1,
                Kategorie = seite.Kategorie,
                MarkdownInhalt = seite.MarkdownInhalt,
                Inhalt = seite.Inhalt,
            });
            await db.SaveChangesAsync();
        }

        return RedirectToSeite(seite.Path);
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/version/{nummer:int}")]
    public async Task<IActionResult> Version(int id, int nummer)
    {
        var version = await db.SeitenVersionen
            .AsNoTracking()
            .Include(v => v.Seite)
            .FirstOrDefaultAsync(v => v.SeiteId == id && v.Nummer == nummer);
        if (version is null)
        {
            return NotFound();
        }

        return View(version);
    }

    private LocalRedirectResult RedirectToSeite(string path) =>
        LocalRedirect("/" + string.Join('/', path.Split('/').Select(Uri.EscapeDataString)));
}
