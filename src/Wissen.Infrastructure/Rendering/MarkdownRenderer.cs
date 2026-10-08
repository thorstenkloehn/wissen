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

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("id");
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedSchemes.Add("mailto");
        return sanitizer;
    }

    // Seite.Inhalt wird roh ausgegeben. DisableHtml allein genügt dafür nicht: Markdown erlaubt
    // weiterhin javascript:-Links und Attribute wie {onclick=...}; die entfernt erst der Sanitizer.
    public static string ToHtml(string markdown) =>
        Sanitizer.Sanitize(Markdown.ToHtml(markdown, Pipeline));
}
