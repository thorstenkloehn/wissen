using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Web.Models;

namespace Wissen.Web.Controllers;

// Die Kategorien aus dem Markdown ([[Kategorie:Name]]). Wie die Seiten selbst ohne Anmeldung lesbar.
// Unter /kategorie/... liegt deshalb keine Seite; SeiteController.Neu lässt dort keine anlegen.
[Route(Prefix)]
public class KategorieController(ApplicationDbContext db) : Controller
{
    public const string Prefix = "kategorie";

    private static readonly StringComparer Sortierung = StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), ignoreCase: true);

    // Ob unter diesem Pfad die Kategorien statt einer Seite liegen; das Routing achtet nicht auf Groß- und Kleinschreibung.
    public static bool IstReserviert(string path) =>
        path.Split('/')[0].Equals(Prefix, StringComparison.OrdinalIgnoreCase);

    // Adresse der Seite einer Kategorie; "/" im Namen bleibt ein Schrägstrich, die Route nimmt den Rest der Adresse.
    public static string Adresse(string kategorie) =>
        $"/{Prefix}/" + string.Join('/', kategorie.Split('/').Select(Uri.EscapeDataString));

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Nur die Kategorienlisten, nicht die Inhalte; gezählt wird hier statt in der Datenbank.
        var listen = await db.Seiten.AsNoTracking().Select(s => s.Kategorien).ToListAsync();

        return View(listen
            .SelectMany(k => k)
            .GroupBy(k => k)
            .Select(g => new KategorieAnzahl(g.Key, g.Count()))
            .OrderBy(k => k.Name, Sortierung)
            .ToList());
    }

    [HttpGet("{**name}")]
    public async Task<IActionResult> Seiten(string name)
    {
        var pfade = await db.Seiten
            .AsNoTracking()
            .Where(s => s.Kategorien.Contains(name))
            .Select(s => s.Path)
            .ToListAsync();

        var ergebnis = View(new KategorieViewModel(name, pfade.OrderBy(p => p, Sortierung).ToList()));
        // Eine Kategorie gibt es nur, solange eine Seite sie nennt.
        if (pfade.Count == 0)
        {
            ergebnis.StatusCode = StatusCodes.Status404NotFound;
        }
        return ergebnis;
    }
}
