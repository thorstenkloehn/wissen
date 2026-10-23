using Microsoft.EntityFrameworkCore;
using Wissen.Infrastructure.Data;

namespace Wissen.Infrastructure.Rendering;

// Fehler nennt die Stände, deren Markdown sich nicht umsetzen ließ; sie behalten ihr bisheriges HTML.
public record SeitenRendernResult(int Seiten, int SeitenGeaendert, int Versionen, int VersionenGeaendert, IReadOnlyList<string> Fehler);

// Erzeugt das gespeicherte HTML und die Kategorien aller Seiten und Versionen neu aus dem Markdown. Seite.Inhalt wird
// roh ausgegeben und entsteht nur beim Speichern: Ohne diesen Schritt gälte eine strengere Regel
// des MarkdownRenderer nicht für Seiten, die vorher gespeichert wurden.
public class SeitenRendern(ApplicationDbContext db)
{
    // Mit speichern: false wird nur gezählt, was sich ändern würde.
    public async Task<SeitenRendernResult> RunAsync(bool speichern, CancellationToken cancellationToken = default)
    {
        var ids = await db.Seiten.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync(cancellationToken);
        int seitenGeaendert = 0, versionen = 0, versionenGeaendert = 0;
        var fehler = new List<string>();

        // Seite für Seite, damit nie alle Inhalte zugleich im Arbeitsspeicher liegen.
        foreach (var id in ids)
        {
            var seite = await db.Seiten.Include(s => s.Versionen).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (seite is null)
            {
                continue;
            }

            if (Render(seite.MarkdownInhalt, $"Seite »{seite.Path}«", fehler) is { } inhalt
                && (inhalt.Html != seite.Inhalt || !inhalt.Kategorien.SequenceEqual(seite.Kategorien)))
            {
                seite.Inhalt = inhalt.Html;
                seite.Kategorien = inhalt.Kategorien;
                seitenGeaendert++;
            }
            foreach (var version in seite.Versionen.OrderBy(v => v.Nummer))
            {
                versionen++;
                if (Render(version.MarkdownInhalt, $"Version {version.Nummer} der Seite »{seite.Path}«", fehler) is { } versionInhalt
                    && (versionInhalt.Html != version.Inhalt || !versionInhalt.Kategorien.SequenceEqual(version.Kategorien)))
                {
                    version.Inhalt = versionInhalt.Html;
                    version.Kategorien = versionInhalt.Kategorien;
                    versionenGeaendert++;
                }
            }

            if (speichern)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            db.ChangeTracker.Clear();
        }

        return new SeitenRendernResult(ids.Count, seitenGeaendert, versionen, versionenGeaendert, fehler);
    }

    // Wie restore-xml ohne Obergrenze für das HTML: Was gespeichert ist, soll sich auch erneuern lassen.
    private static GerendertesMarkdown? Render(string markdown, string ort, List<string> fehler)
    {
        try
        {
            return MarkdownRenderer.Render(markdown);
        }
        catch (MarkdownException e)
        {
            fehler.Add($"{ort}: {e.Message}");
            return null;
        }
    }
}
