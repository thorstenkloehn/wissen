using Ganss.Xss;
using Markdig;

namespace Wissen.Infrastructure.Rendering;

// Erzeugt aus dem Markdown einer Seite das HTML für Seite.Inhalt.
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    // Klassen, die Markdig selbst erzeugt (Fußnoten, Aufgabenlisten, Formeln, Diagramme).
    private static readonly string[] MarkdigClasses =
    [
        "footnotes", "footnote-ref", "footnote-back-ref", "task-list-item", "contains-task-list",
        "math", "mermaid", "nomnoml",
    ];

    // Die Sprache eines Codeblocks, z. B. language-csharp.
    private const string LanguageClassPrefix = "language-";

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("id");
        sanitizer.AllowedSchemes.Add("mailto");

        // Frei wählbare Klassen und Stile ({.position-fixed}, {style=...}, :::klasse) könnten die ganze
        // Seite überdecken, etwa mit einem unsichtbaren Link. Erlaubt bleibt nur, was Markdig selbst
        // erzeugt: seine eigenen Klassen und die Ausrichtung von Tabellenspalten.
        sanitizer.AllowedAttributes.Add("class");
        foreach (var name in MarkdigClasses)
        {
            sanitizer.AllowedClasses.Add(name);
        }
        sanitizer.RemovingCssClass += (_, e) =>
            e.Cancel = e.CssClass.StartsWith(LanguageClassPrefix, StringComparison.Ordinal);
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.Add("text-align");
        return sanitizer;
    }

    // Seite.Inhalt wird roh ausgegeben. DisableHtml allein genügt dafür nicht: Markdown erlaubt
    // weiterhin javascript:-Links und Attribute wie {onclick=...}; die entfernt erst der Sanitizer.
    public static string ToHtml(string markdown) =>
        Sanitizer.Sanitize(Markdown.ToHtml(markdown, Pipeline));
}
