using System.Text.RegularExpressions;

namespace Wissen.Infrastructure.Models;

public partial class Seite
{
    // Erlaubte Zeichen eines Pfadabschnitts als regulärer Ausdruck. Ausgeschriebene Zeichenbereiche,
    // weil das Muster auch im Browser geprüft wird und JavaScript dort kein \p{L} kennt.
    public const string PathSegment = @"[A-Za-z0-9\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u024F_-]+";

    public const int MaxPathLength = 500;

    [GeneratedRegex("^" + PathSegment + "(/" + PathSegment + ")*$")]
    private static partial Regex PathPattern();

    // Ob unter diesem Pfad eine Seite liegen kann: Abschnitte aus erlaubten Zeichen, durch "/" getrennt,
    // ohne Schrägstrich am Anfang oder Ende.
    public static bool IsValidPath(string? path) =>
        path is not null && path.Length <= MaxPathLength && PathPattern().IsMatch(path);

    // Obergrenze für das Markdown einer Seite in Zeichen. Jede Änderung speichert Markdown und HTML
    // als neue Version; ohne Grenze ließe sich die Datenbank mit wenigen Anfragen füllen.
    public const int MaxMarkdownLength = 200_000;

    // Obergrenze für das daraus erzeugte HTML in Zeichen. Gewöhnlicher Text wird etwa doppelt so lang;
    // Fußnoten und Abkürzungen können ihn vervielfachen (200 000 Zeichen ergaben über 3 Millionen).
    public const int MaxHtmlLength = 3 * MaxMarkdownLength;

    public int Id { get; set; }

    // Adresse der Seite ohne führenden Schrägstrich, z. B. "doc/einleitung"; eindeutig.
    public string Path { get; set; } = string.Empty;

    public string Inhalt { get; set; } = string.Empty;

    public string MarkdownInhalt { get; set; } = string.Empty;

    public string Kategorie { get; set; } = string.Empty;

    public List<SeitenVersion> Versionen { get; set; } = [];
}
