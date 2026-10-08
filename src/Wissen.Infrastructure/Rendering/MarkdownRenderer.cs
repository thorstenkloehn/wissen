using AngleSharp.Dom;
using Ganss.Xss;
using Markdig;

namespace Wissen.Infrastructure.Rendering;

// Das Markdown lässt sich nicht in HTML umsetzen; die Meldung ist für den Benutzer bestimmt.
public class MarkdownException(string message, Exception? innerException = null) : Exception(message, innerException);

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

        // Bilder und Medien von fremden Servern würden beim bloßen Lesen einer Seite geladen und
        // verrieten dem fremden Server die IP-Adresse jedes Besuchers. Aus einem fremden Bild wird
        // deshalb ein Link, den der Leser selbst anklicken muss; andere fremde Quellen entfallen.
        sanitizer.AllowedAttributes.Remove("srcset");
        sanitizer.FilterUrl += (_, e) =>
        {
            if (e.Tag.LocalName is not ("a" or "img") && IsExternal(e.OriginalUrl))
            {
                e.SanitizedUrl = null;
            }
        };
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { LocalName: "img" } img && img.GetAttribute("src") is { } src && IsExternal(src))
            {
                e.ReplacementNodes.Add(CreateImageLink(e.Document, img, src));
            }
        };
        return sanitizer;
    }

    // Fremd ist jede Adresse, die der Browser nicht auf dem eigenen Server auflöst.
    public static bool IsExternal(string url)
    {
        // Browser lesen "\" wie "/" und überspringen Tabs und Zeilenumbrüche: "/\fremd.example" und
        // "/<Tab>/fremd.example" führen auf einen fremden Server.
        if (url.Any(c => c == '\\' || char.IsControl(c)))
        {
            return true;
        }
        return !Uri.TryCreate(LocalBase, url.Trim(), out var resolved)
            || resolved.Scheme != LocalBase.Scheme
            || resolved.Host != LocalBase.Host;
    }

    private static readonly Uri LocalBase = new("https://eigener-server.invalid/a/b");

    private static INode CreateImageLink(IDocument document, IElement img, string src)
    {
        var alt = img.GetAttribute("alt");
        var text = string.IsNullOrWhiteSpace(alt) ? src : alt;
        // Steht das Bild schon in einem Link, bleibt nur der Text: Links lassen sich nicht schachteln.
        if (img.Ancestors<IElement>().Any(a => a.LocalName == "a"))
        {
            return document.CreateTextNode(text);
        }

        var link = document.CreateElement("a");
        link.SetAttribute("href", src);
        link.TextContent = $"Bild: {text}";
        return link;
    }

    // Seite.Inhalt wird roh ausgegeben. DisableHtml allein genügt dafür nicht: Markdown erlaubt
    // weiterhin javascript:-Links und Attribute wie {onclick=...}; die entfernt erst der Sanitizer.
    // Mit maxHtmlLength bricht die Umsetzung ab, bevor der Sanitizer ein übergroßes HTML bearbeitet:
    // Seine Laufzeit wächst mit der Größe stark an (2,5 Millionen Zeichen brauchten über 20 Sekunden).
    public static string ToHtml(string markdown, int maxHtmlLength = int.MaxValue)
    {
        string html;
        try
        {
            html = Markdown.ToHtml(markdown, Pipeline);
        }
        // Markdig bricht bei zu tiefer Verschachtelung und bei übergroßen Tabellen mit diesen Ausnahmen ab.
        catch (Exception e) when (e is ArgumentException or OverflowException)
        {
            throw new MarkdownException("Der Inhalt ist zu tief verschachtelt oder enthält eine zu große Tabelle.", e);
        }

        if (html.Length > maxHtmlLength)
        {
            throw new MarkdownException($"Aus dem Inhalt entsteht eine zu große Seite ({html.Length:N0} Zeichen HTML, erlaubt sind {maxHtmlLength:N0}). Bitte teilen Sie den Inhalt auf mehrere Seiten auf.");
        }
        return Sanitizer.Sanitize(html);
    }
}
