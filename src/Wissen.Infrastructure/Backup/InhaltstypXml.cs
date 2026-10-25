using Wissen.Infrastructure.Models;

namespace Wissen.Infrastructure.Backup;

// Schreibweise des Inhaltstyps im Attribut typ der XML-Sicherung. Feste Namen statt der Zahl aus der
// Datenbank, damit die Datei lesbar bleibt und nicht von der Reihenfolge im enum abhängt.
internal static class InhaltstypXml
{
    public static string Name(Inhaltstyp typ) => typ switch
    {
        Inhaltstyp.Seite => "seite",
        Inhaltstyp.Blogartikel => "blogartikel",
        _ => throw new ArgumentOutOfRangeException(nameof(typ), typ, null),
    };

    public static Inhaltstyp? Parse(string name) => name switch
    {
        "seite" => Inhaltstyp.Seite,
        "blogartikel" => Inhaltstyp.Blogartikel,
        _ => null,
    };
}
