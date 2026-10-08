using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Identity;
using Wissen.Infrastructure.Models;
using Wissen.Infrastructure.Rendering;
using Wissen.Web.Models;

namespace Wissen.Web.Controllers;

[Authorize]
[Route("seite")]
public class SeiteController(ApplicationDbContext db) : Controller
{
    [HttpGet("neu")]
    public IActionResult Neu(string? path)
    {
        // Das Formular zeigt den Pfad nur an; ohne Pfad gibt es nichts anzulegen.
        if (string.IsNullOrWhiteSpace(path))
        {
            return LocalRedirect("/");
        }

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
        string inhalt;
        try
        {
            inhalt = MarkdownRenderer.ToHtml(model.MarkdownInhalt!, Seite.MaxHtmlLength);
        }
        catch (MarkdownException e)
        {
            ModelState.AddModelError(nameof(model.MarkdownInhalt), e.Message);
            return View(model);
        }

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

        try
        {
            await AendereSeite(seite, model.Kategorie!.Trim(), model.MarkdownInhalt!);
        }
        catch (MarkdownException e)
        {
            ModelState.AddModelError(nameof(model.MarkdownInhalt), e.Message);
            model.Path = seite.Path;
            return View(model);
        }

        return RedirectToSeite(seite.Path);
    }

    // Löschen entfernt auch die Versionsgeschichte und lässt sich nicht zurücknehmen.
    [Authorize(Roles = Rollen.Administrator)]
    [HttpGet("loeschen/{id:int}")]
    public async Task<IActionResult> Loeschen(int id)
    {
        var seite = await db.Seiten.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (seite is null)
        {
            return NotFound();
        }

        ViewData["Versionen"] = await db.SeitenVersionen.CountAsync(v => v.SeiteId == id);
        return View(seite);
    }

    [Authorize(Roles = Rollen.Administrator)]
    [HttpPost("loeschen/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoeschenBestaetigt(int id)
    {
        var seite = await db.Seiten.Include(s => s.Versionen).FirstOrDefaultAsync(s => s.Id == id);
        if (seite is null)
        {
            return NotFound();
        }

        // Die Versionen werden mit der Seite gelöscht.
        db.Seiten.Remove(seite);
        await db.SaveChangesAsync();

        return RedirectToSeite(seite.Path);
    }

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

    // Setzt die Seite auf den Stand einer früheren Version zurück. Die Geschichte bleibt erhalten:
    // Der alte Stand wird als neue Version angehängt.
    [HttpPost("{id:int}/version/{nummer:int}/zuruecksetzen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Zuruecksetzen(int id, int nummer)
    {
        var seite = await db.Seiten.FirstOrDefaultAsync(s => s.Id == id);
        var version = await db.SeitenVersionen
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.SeiteId == id && v.Nummer == nummer);
        if (seite is null || version is null)
        {
            return NotFound();
        }

        try
        {
            await AendereSeite(seite, version.Kategorie, version.MarkdownInhalt);
        }
        // Nur bei Versionen möglich, die nicht über das Formular entstanden sind (restore-xml).
        catch (MarkdownException e)
        {
            return UnprocessableEntity($"Die Seite lässt sich nicht auf diese Version zurücksetzen: {e.Message}");
        }

        return RedirectToSeite(seite.Path);
    }

    // Übernimmt Kategorie und Markdown in die Seite und hängt eine neue Version an.
    // Ohne Änderung entsteht keine neue Version.
    private async Task AendereSeite(Seite seite, string kategorie, string markdownInhalt)
    {
        if (kategorie == seite.Kategorie && markdownInhalt == seite.MarkdownInhalt)
        {
            return;
        }

        // Vor jeder Änderung an der Seite: Lässt sich das Markdown nicht umsetzen, bleibt sie unberührt.
        var inhalt = MarkdownRenderer.ToHtml(markdownInhalt, Seite.MaxHtmlLength);

        var letzteNummer = await db.SeitenVersionen
            .Where(v => v.SeiteId == seite.Id)
            .MaxAsync(v => (int?)v.Nummer) ?? 0;

        seite.Kategorie = kategorie;
        seite.MarkdownInhalt = markdownInhalt;
        seite.Inhalt = inhalt;
        db.SeitenVersionen.Add(new SeitenVersion
        {
            SeiteId = seite.Id,
            Nummer = letzteNummer + 1,
            Kategorie = seite.Kategorie,
            MarkdownInhalt = seite.MarkdownInhalt,
            Inhalt = seite.Inhalt,
        });
        await db.SaveChangesAsync();
    }

    private LocalRedirectResult RedirectToSeite(string path) =>
        LocalRedirect("/" + string.Join('/', path.Split('/').Select(Uri.EscapeDataString)));
}
