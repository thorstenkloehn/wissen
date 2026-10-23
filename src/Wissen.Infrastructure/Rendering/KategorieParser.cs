using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax.Inlines;

namespace Wissen.Infrastructure.Rendering;

// Eine Kategorie im Markdown, z. B. [[Kategorie:Hauptseite]]. Sie erscheint nicht im HTML.
public class KategorieInline : LeafInline
{
    public string Name { get; set; } = string.Empty;
}

// Erkennt [[Kategorie:Name]] im Fließtext. Als Inline-Parser greift er nicht in Code (`...` und
// Codeblöcken) und nicht nach einem Backslash (\[[Kategorie:...]]): Dort bleibt die Angabe Text.
public class KategorieParser : InlineParser
{
    public const string Anfang = "[[Kategorie:";

    private const string Ende = "]]";

    public const int MaxNameLength = 200;

    public KategorieParser()
    {
        OpeningCharacters = ['['];
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var text = slice.Text.AsSpan(slice.Start, slice.Length);
        if (!text.StartsWith(Anfang, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = text[Anfang.Length..];
        var ende = rest.IndexOf(Ende, StringComparison.Ordinal);
        // Ohne "[", "]", "|" und Zeilenumbruch im Namen, sonst bleibt alles Text.
        if (ende < 0 || rest[..ende].IndexOfAny("[]|\r\n") >= 0)
        {
            return false;
        }

        var name = rest[..ende].Trim().ToString();
        if (name.Length is 0 or > MaxNameLength)
        {
            return false;
        }

        var start = processor.GetSourcePosition(slice.Start, out var line, out var column);
        var laenge = Anfang.Length + ende + Ende.Length;
        processor.Inline = new KategorieInline
        {
            Name = name,
            Span = new(start, start + laenge - 1),
            Line = line,
            Column = column,
        };
        slice.Start += laenge;
        return true;
    }
}
