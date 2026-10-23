namespace Wissen.Web.Models;

public record KategorieAnzahl(string Name, int Seiten);

// Eine Kategorie und die Pfade der Seiten, die sie nennen.
public record KategorieViewModel(string Name, List<string> Pfade);
