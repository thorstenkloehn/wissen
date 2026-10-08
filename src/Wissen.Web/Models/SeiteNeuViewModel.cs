using System.ComponentModel.DataAnnotations;
using Wissen.Infrastructure.Models;

namespace Wissen.Web.Models;

public class SeiteNeuViewModel
{
    [Required(ErrorMessage = "Bitte geben Sie einen Pfad an.")]
    [StringLength(500, ErrorMessage = "Der Pfad darf höchstens {1} Zeichen lang sein.")]
    [RegularExpression(@"^\s*/?" + Seite.PathSegment + "(/" + Seite.PathSegment + @")*/?\s*$",
        ErrorMessage = "Der Pfad darf nur Buchstaben, Ziffern, Bindestriche und Unterstriche enthalten, getrennt durch Schrägstriche.")]
    [Display(Name = "Pfad")]
    public string? Path { get; set; }

    [Required(ErrorMessage = "Bitte geben Sie eine Kategorie an.")]
    [StringLength(200, ErrorMessage = "Die Kategorie darf höchstens {1} Zeichen lang sein.")]
    [Display(Name = "Kategorie")]
    public string? Kategorie { get; set; }

    [Required(ErrorMessage = "Bitte geben Sie einen Inhalt ein.")]
    [StringLength(Seite.MaxMarkdownLength, ErrorMessage = "Der Inhalt darf höchstens {1} Zeichen lang sein.")]
    [Display(Name = "Inhalt (Markdown)")]
    public string? MarkdownInhalt { get; set; }
}
