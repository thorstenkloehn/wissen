using System.ComponentModel.DataAnnotations;
using Wissen.Infrastructure.Models;

namespace Wissen.Web.Models;

public class SeiteBearbeitenViewModel
{
    // Nur zur Anzeige; der Pfad einer Seite wird beim Bearbeiten nicht geändert.
    public string? Path { get; set; }

    [Required(ErrorMessage = "Bitte geben Sie einen Inhalt ein.")]
    [StringLength(Seite.MaxMarkdownLength, ErrorMessage = "Der Inhalt darf höchstens {1} Zeichen lang sein.")]
    [Display(Name = "Inhalt (Markdown)")]
    public string? MarkdownInhalt { get; set; }

    [Display(Name = "Typ")]
    public Inhaltstyp Typ { get; set; } = Inhaltstyp.Seite;
}
