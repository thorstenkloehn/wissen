using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;

namespace Wissen.Infrastructure.Backup;

// Schreibt alle Seiten samt Versionsgeschichte als XML: Id, Pfad, Kategorie und Markdown.
// Das erzeugte HTML (Seite.Inhalt) fehlt bewusst, es lässt sich aus dem Markdown neu erzeugen.
public class SeitenXmlExport(ApplicationDbContext db)
{
    public async Task<XDocument> CreateAsync(CancellationToken cancellationToken = default)
    {
        var seiten = await db.Seiten
            .AsNoTracking()
            .Include(s => s.Versionen)
            .OrderBy(s => s.Path)
            .ToListAsync(cancellationToken);

        return new XDocument(
            new XElement("seiten",
                new XAttribute("erstelltAm", DateTime.UtcNow),
                seiten.Select(seite =>
                    new XElement("seite",
                        new XAttribute("id", seite.Id),
                        new XElement("path", Clean(seite.Path)),
                        new XElement("kategorie", Clean(seite.Kategorie)),
                        new XElement("markdown", Clean(seite.MarkdownInhalt)),
                        new XElement("versionsgeschichte",
                            seite.Versionen.OrderBy(v => v.Nummer).Select(version =>
                                new XElement("version",
                                    new XAttribute("nummer", version.Nummer),
                                    new XAttribute("erstelltAm", version.ErstelltAm),
                                    new XElement("kategorie", Clean(version.Kategorie)),
                                    new XElement("markdown", Clean(version.MarkdownInhalt)))))))));
    }

    // Steuerzeichen, die in XML nicht vorkommen dürfen, würden das Speichern abbrechen.
    private static string Clean(string text) =>
        text.All(XmlConvert.IsXmlChar) ? text : string.Concat(text.Where(XmlConvert.IsXmlChar));
}
