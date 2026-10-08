namespace Wissen.Infrastructure.Models;

public class Seite
{
    public int Id { get; set; }

    // Adresse der Seite ohne führenden Schrägstrich, z. B. "doc/einleitung"; eindeutig.
    public string Path { get; set; } = string.Empty;

    public string Inhalt { get; set; } = string.Empty;

    public string MarkdownInhalt { get; set; } = string.Empty;

    public string Kategorie { get; set; } = string.Empty;

    public List<SeitenVersion> Versionen { get; set; } = [];
}
