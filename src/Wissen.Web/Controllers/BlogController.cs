using System.Globalization;
using System.Xml.Linq;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;
using Wissen.Web.Models;

namespace Wissen.Web.Controllers;

// Alle Seiten vom Typ Blogartikel, die neuesten zuerst. Wie die Seiten selbst ohne Anmeldung lesbar.
// Die Artikel liegen unter ihrem eigenen Pfad; nur genau /blog ist belegt, SeiteController.Neu lässt dort keine Seite anlegen.
// Der Feed unter /blog/feed.xml braucht keine Sperre: Ein Pfad mit Punkt ist für Seiten ungültig.
[Route(Prefix)]
public class BlogController(ApplicationDbContext db) : Controller
{
    public const string Prefix = "blog";

    public const string FeedAdresse = "/" + Prefix + "/feed.xml";

    // So viele der neuesten Artikel stehen im Feed, jeder mit seinem ganzen Inhalt.
    public const int FeedArtikel = 20;

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

    // RSS 2.0 mit den neuesten Artikeln. Der Autor fehlt bewusst: Es ist die E-Mail-Adresse des Kontos,
    // die auch die Versionsgeschichte nur Angemeldeten zeigt.
    [HttpGet("feed.xml")]
    public async Task<IActionResult> Feed()
    {
        var artikel = await db.Seiten
            .AsNoTracking()
            .Where(s => s.Typ == Inhaltstyp.Blogartikel)
            .Select(s => new { s.Path, s.Titel, s.Inhalt, s.Kategorien, Datum = s.Versionen.Min(v => (DateTime?)v.ErstelltAm) })
            .OrderByDescending(a => a.Datum)
            .ThenBy(a => a.Path)
            .Take(FeedArtikel)
            .ToListAsync();

        // Feedreader brauchen absolute Adressen; der Host ist durch AllowedHosts beschränkt.
        var basis = $"{Request.Scheme}://{Request.Host}";
        var rss = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XElement("channel",
                    new XElement("title", "wissen – Blog"),
                    new XElement("link", basis + "/" + Prefix),
                    new XElement("description", "Die neuesten Artikel aus dem Blog von wissen."),
                    new XElement("language", "de-DE"),
                    artikel.Select(a =>
                    {
                        var adresse = basis + Adresse(a.Path);
                        return new XElement("item",
                            new XElement("title", Titel(new BlogArtikel(a.Path, a.Titel, a.Datum, a.Kategorien))),
                            new XElement("link", adresse),
                            new XElement("guid", new XAttribute("isPermaLink", "true"), adresse),
                            a.Datum is { } datum ? new XElement("pubDate", datum.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)) : null,
                            a.Kategorien.Select(k => new XElement("category", k)),
                            new XElement("description", AbsoluteAdressen(a.Inhalt, new Uri(adresse))));
                    }))));

        return Content(rss.Declaration + rss.ToString(SaveOptions.DisableFormatting), "application/rss+xml; charset=utf-8");
    }

    private static readonly HtmlParser Parser = new();

    // Im Feed fehlt die Seite, gegen die der Browser relative Adressen (/doc/x, bild.png, #inhalt-...)
    // auflöst; nicht jeder Feedreader nimmt dafür den Link des Artikels. Deshalb werden href und src
    // gegen die Adresse des Artikels aufgelöst. Adressen mit Schema (https:, mailto:) bleiben, wie sie sind.
    public static string AbsoluteAdressen(string html, Uri artikel)
    {
        var dokument = Parser.ParseDocument(html);
        foreach (var element in dokument.Body!.QuerySelectorAll("[href], [src]"))
        {
            foreach (var name in (string[])["href", "src"])
            {
                if (element.GetAttribute(name) is { } wert && Uri.TryCreate(artikel, wert.Trim(), out var absolut))
                {
                    element.SetAttribute(name, absolut.AbsoluteUri);
                }
            }
        }
        return dokument.Body.InnerHtml;
    }

    // Abschnittsweise kodiert wie die Links der Übersicht.
    public static string Adresse(string path) =>
        "/" + string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
