namespace Wissen.Infrastructure.Models;

public class Seite
{
    // Erlaubte Zeichen eines Pfadabschnitts als regulärer Ausdruck. Ausgeschriebene Zeichenbereiche,
    // weil das Muster auch im Browser geprüft wird und JavaScript dort kein \p{L} kennt.
    public const string PathSegment = @"[A-Za-z0-9\u00C0-\u00D6\u00D8-\u00F6\u00F8-\u024F_-]+";

    // Obergrenze für das Markdown einer Seite in Zeichen. Jede Änderung speichert Markdown und HTML
    // als neue Version; ohne Grenze ließe sich die Datenbank mit wenigen Anfragen füllen.
    public const int MaxMarkdownLength = 200_000;

    public int Id { get; set; }

    // Adresse der Seite ohne führenden Schrägstrich, z. B. "doc/einleitung"; eindeutig.
    public string Path { get; set; } = string.Empty;

    public string Inhalt { get; set; } = string.Empty;

    public string MarkdownInhalt { get; set; } = string.Empty;

    public string Kategorie { get; set; } = string.Empty;

    public List<SeitenVersion> Versionen { get; set; } = [];
}
