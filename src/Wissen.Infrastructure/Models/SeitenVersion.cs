namespace Wissen.Infrastructure.Models;

// Ein früherer Stand einer Seite; die Versionen einer Seite bilden ihre Versionsgeschichte.
public class SeitenVersion
{
    public int Id { get; set; }

    public int SeiteId { get; set; }

    public Seite Seite { get; set; } = null!;

    // Fortlaufende Nummer innerhalb einer Seite, beginnend bei 1.
    public int Nummer { get; set; }

    public string Inhalt { get; set; } = string.Empty;

    public string MarkdownInhalt { get; set; } = string.Empty;

    public string Kategorie { get; set; } = string.Empty;

    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
}
