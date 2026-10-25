namespace Wissen.Web.Models;

// Ein Eintrag der Blog-Übersicht. Ohne Datum nur bei Seiten ohne Versionsgeschichte.
public record BlogArtikel(string Pfad, string? Titel, DateTime? VeroeffentlichtAm, List<string> Kategorien);

// Eine Seite der Blog-Übersicht: ihre Artikel, ihre Nummer (ab 1) und die Zahl aller Seiten (mindestens 1).
public record BlogUebersicht(List<BlogArtikel> Artikel, int Seite, int Seiten);
