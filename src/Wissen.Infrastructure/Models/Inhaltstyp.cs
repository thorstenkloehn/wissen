namespace Wissen.Infrastructure.Models;

// Art einer Seite. Blogartikel zeigt die App zusätzlich unter /blog, nach Datum geordnet; sonst
// verhalten sich beide gleich (Versionen, Kategorien, Rechte).
public enum Inhaltstyp
{
    Seite = 0,
    Blogartikel = 1,
}
