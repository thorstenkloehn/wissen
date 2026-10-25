using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;
using Wissen.Infrastructure.Models;

namespace Wissen.Infrastructure.Backup;

// Schreibt alle Seiten samt Versionsgeschichte als XML: Id, Pfad, Markdown und (außer bei gewöhnlichen Seiten) den Typ. Das erzeugte HTML
// (Seite.Inhalt) und die Kategorien fehlen bewusst, beides entsteht neu aus dem Markdown.
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
                        seite.Typ == Inhaltstyp.Seite ? null : new XAttribute("typ", InhaltstypXml.Name(seite.Typ)),
                        new XElement("path", Clean(seite.Path)),
                        new XElement("markdown", Clean(seite.MarkdownInhalt)),
                        new XElement("versionsgeschichte",
                            seite.Versionen.OrderBy(v => v.Nummer).Select(version =>
                                new XElement("version",
                                    new XAttribute("nummer", version.Nummer),
                                    new XAttribute("erstelltAm", version.ErstelltAm),
                                    version.Autor is null ? null : new XAttribute("autor", Clean(version.Autor)),
                                    new XElement("markdown", Clean(version.MarkdownInhalt)))))))));
    }

    // Steuerzeichen, die in XML nicht vorkommen dürfen, würden das Speichern abbrechen.
    private static string Clean(string text) =>
        text.All(XmlConvert.IsXmlChar) ? text : string.Concat(text.Where(XmlConvert.IsXmlChar));
}
