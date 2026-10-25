namespace Wissen.Web.Models;

// Ein Eintrag der Blog-Übersicht. Ohne Datum nur bei Seiten ohne Versionsgeschichte.
public record BlogArtikel(string Pfad, string? Titel, DateTime? VeroeffentlichtAm, List<string> Kategorien);
