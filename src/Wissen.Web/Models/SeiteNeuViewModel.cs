using System.ComponentModel.DataAnnotations;

namespace Wissen.Web.Models;

public class SeiteNeuViewModel
{
    [Required(ErrorMessage = "Bitte geben Sie einen Pfad an.")]
    [StringLength(500, ErrorMessage = "Der Pfad darf höchstens {1} Zeichen lang sein.")]
    // Das Muster wird auch im Browser geprüft; JavaScript kennt dort kein \p{L}, deshalb die Zeichenbereiche.
    [RegularExpression(@"^\s*/?[A-Za-z0-9À-ÖØ-öø-ɏ_-]+(/[A-Za-z0-9À-ÖØ-öø-ɏ_-]+)*/?\s*$",
        ErrorMessage = "Der Pfad darf nur Buchstaben, Ziffern, Bindestriche und Unterstriche enthalten, getrennt durch Schrägstriche.")]
    [Display(Name = "Pfad")]
    public string? Path { get; set; }

    [Required(ErrorMessage = "Bitte geben Sie eine Kategorie an.")]
    [StringLength(200, ErrorMessage = "Die Kategorie darf höchstens {1} Zeichen lang sein.")]
    [Display(Name = "Kategorie")]
    public string? Kategorie { get; set; }

    [Required(ErrorMessage = "Bitte geben Sie einen Inhalt ein.")]
    [Display(Name = "Inhalt (Markdown)")]
    public string? MarkdownInhalt { get; set; }
}
