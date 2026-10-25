using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;
using Wissen.Web.Models;

namespace Wissen.Web.Controllers;

// Alle Seiten vom Typ Blogartikel, die neuesten zuerst. Wie die Seiten selbst ohne Anmeldung lesbar.
// Die Artikel liegen unter ihrem eigenen Pfad; nur genau /blog ist belegt, SeiteController.Neu lässt dort keine Seite anlegen.
[Route(Prefix)]
public class BlogController(ApplicationDbContext db) : Controller
{
    public const string Prefix = "blog";

    // Ob unter diesem Pfad die Übersicht statt einer Seite liegt; das Routing achtet nicht auf Groß- und Kleinschreibung.
    public static bool IstReserviert(string path) =>
        path.Equals(Prefix, StringComparison.OrdinalIgnoreCase);

    // Überschrift eines Artikels in der Übersicht: seine erste Überschrift, ohne eine (oder vor seiten-rendern)
    // der letzte Abschnitt des Pfads, "-" und "_" als Leerzeichen.
    public static string Titel(BlogArtikel artikel) =>
        artikel.Titel ?? artikel.Pfad.Split('/')[^1].Replace('-', ' ').Replace('_', ' ');

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Das Datum eines Artikels ist das seiner ersten Version; für die Liste genügen Pfad, Titel, Datum und Kategorien.
        var artikel = await db.Seiten
            .AsNoTracking()
            .Where(s => s.Typ == Inhaltstyp.Blogartikel)
            .Select(s => new BlogArtikel(s.Path, s.Titel, s.Versionen.Min(v => (DateTime?)v.ErstelltAm), s.Kategorien))
            .ToListAsync();

        return View(artikel
            .OrderByDescending(a => a.VeroeffentlichtAm)
            .ThenBy(a => a.Pfad, StringComparer.Ordinal)
            .ToList());
    }
}
