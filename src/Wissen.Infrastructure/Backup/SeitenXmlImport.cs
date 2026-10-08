using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;
using Wissen.Infrastructure.Rendering;

namespace Wissen.Infrastructure.Backup;

public record SeitenXmlImportResult(int Neu, int Ersetzt);

// Liest eine mit SeitenXmlExport geschriebene Datei wieder ein. Schlüssel ist der Pfad:
// Eine vorhandene Seite mit demselben Pfad wird samt Versionsgeschichte ersetzt, andere Seiten
// bleiben unberührt. Die Ids aus der Datei werden nicht übernommen.
public partial class SeitenXmlImport(ApplicationDbContext db)
{
    [GeneratedRegex("^" + Seite.PathSegment + "(/" + Seite.PathSegment + ")*$")]
    private static partial Regex PathPattern();

    // Prüft die Datei und erzeugt die Seiten; das HTML entsteht neu aus dem Markdown.
    public IReadOnlyList<Seite> Read(XDocument document)
    {
        if (document.Root?.Name != "seiten")
        {
            throw new FormatException("Das Wurzelelement muss <seiten> heißen.");
        }

        var seiten = new List<Seite>();
        foreach (var element in document.Root.Elements("seite"))
        {
            var path = Required(element, "path", "einer Seite").Trim();
            if (path.Length > 500 || !PathPattern().IsMatch(path))
            {
                throw new FormatException($"Der Pfad »{path}« ist ungültig.");
            }
            if (seiten.Any(s => s.Path == path))
            {
                throw new FormatException($"Der Pfad »{path}« kommt in der Datei mehrfach vor.");
            }

            var markdown = Required(element, "markdown", $"der Seite »{path}«");
            var seite = new Seite
            {
                Path = path,
                Kategorie = Kategorie(element, $"der Seite »{path}«"),
                MarkdownInhalt = markdown,
                Inhalt = ToHtml(markdown, $"der Seite »{path}«"),
            };

            foreach (var versionElement in element.Element("versionsgeschichte")?.Elements("version") ?? [])
            {
                var ort = $"einer Version der Seite »{path}«";
                if (!int.TryParse(versionElement.Attribute("nummer")?.Value, out var nummer) || nummer < 1)
                {
                    throw new FormatException($"Das Attribut nummer {ort} fehlt oder ist keine positive Zahl.");
                }
                if (seite.Versionen.Any(v => v.Nummer == nummer))
                {
                    throw new FormatException($"Die Version {nummer} der Seite »{path}« kommt mehrfach vor.");
                }

                DateTime erstelltAm;
                try
                {
                    erstelltAm = ((DateTime)versionElement.Attribute("erstelltAm")!).ToUniversalTime();
                }
                catch (Exception e) when (e is FormatException or ArgumentNullException)
                {
                    throw new FormatException($"Das Attribut erstelltAm der Version {nummer} der Seite »{path}« fehlt oder ist kein Zeitpunkt.");
                }

                var versionMarkdown = Required(versionElement, "markdown", ort);
                seite.Versionen.Add(new SeitenVersion
                {
                    Nummer = nummer,
                    Kategorie = Kategorie(versionElement, ort),
                    MarkdownInhalt = versionMarkdown,
                    Inhalt = ToHtml(versionMarkdown, $"der Version {nummer} der Seite »{path}«"),
                    ErstelltAm = erstelltAm,
                    Autor = Autor(versionElement, nummer, path),
                });
            }

            seiten.Add(seite);
        }

        return seiten;
    }

    // Pfade aus der Datei, zu denen es schon eine Seite gibt.
    public async Task<IReadOnlyList<string>> FindExistingPathsAsync(IReadOnlyList<Seite> seiten, CancellationToken cancellationToken = default)
    {
        var paths = seiten.Select(s => s.Path).ToList();
        return await db.Seiten
            .Where(s => paths.Contains(s.Path))
            .OrderBy(s => s.Path)
            .Select(s => s.Path)
            .ToListAsync(cancellationToken);
    }

    // Speichert alle Seiten in einem Schritt: Entweder wird alles übernommen oder nichts.
    public async Task<SeitenXmlImportResult> ImportAsync(IReadOnlyList<Seite> seiten, CancellationToken cancellationToken = default)
    {
        var paths = seiten.Select(s => s.Path).ToList();
        var vorhanden = await db.Seiten
            .Include(s => s.Versionen)
            .Where(s => paths.Contains(s.Path))
            .ToDictionaryAsync(s => s.Path, cancellationToken);

        foreach (var seite in seiten)
        {
            if (vorhanden.TryGetValue(seite.Path, out var alt))
            {
                // Die vorhandene Seite behält ihre Id; Inhalt und Versionsgeschichte kommen aus der Datei.
                alt.Kategorie = seite.Kategorie;
                alt.MarkdownInhalt = seite.MarkdownInhalt;
                alt.Inhalt = seite.Inhalt;
                db.SeitenVersionen.RemoveRange(alt.Versionen);
                foreach (var version in seite.Versionen)
                {
                    version.SeiteId = alt.Id;
                    db.SeitenVersionen.Add(version);
                }
            }
            else
            {
                db.Seiten.Add(seite);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SeitenXmlImportResult(seiten.Count - vorhanden.Count, vorhanden.Count);
    }

    private static string ToHtml(string markdown, string ort)
    {
        try
        {
            return MarkdownRenderer.ToHtml(markdown);
        }
        catch (MarkdownException e)
        {
            throw new FormatException($"Das Markdown {ort} lässt sich nicht umsetzen: {e.Message}");
        }
    }

    private static string Required(XElement element, string name, string ort) =>
        element.Element(name)?.Value ?? throw new FormatException($"Das Element <{name}> {ort} fehlt.");

    // Das Attribut autor fehlt bei Versionen aus der Zeit vor dieser Angabe.
    private static string? Autor(XElement versionElement, int nummer, string path)
    {
        var autor = versionElement.Attribute("autor")?.Value.Trim();
        if (string.IsNullOrEmpty(autor))
        {
            return null;
        }
        return autor.Length <= 256
            ? autor
            : throw new FormatException($"Das Attribut autor der Version {nummer} der Seite »{path}« ist länger als 256 Zeichen.");
    }

    private static string Kategorie(XElement element, string ort)
    {
        var kategorie = Required(element, "kategorie", ort).Trim();
        return kategorie.Length <= 200
            ? kategorie
            : throw new FormatException($"Die Kategorie {ort} ist länger als 200 Zeichen.");
    }
}
