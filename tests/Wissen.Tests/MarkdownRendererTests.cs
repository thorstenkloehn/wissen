using Wissen.Infrastructure.Rendering;

namespace Wissen.Tests;

public class MarkdownRendererTests
{
    [Theory]
    [InlineData("Absatz {.position-fixed .top-0 .start-0 .w-100 .h-100}")]
    [InlineData("[x](https://a.example){.stretched-link}")]
    [InlineData("[x](https://a.example){style=\"position:fixed;top:0;left:0;width:100%;height:100%\"}")]
    [InlineData("Absatz {style=\"background:url(https://a.example/x.png)\"}")]
    [InlineData(":::position-fixed\nText\n:::")]
    [InlineData("```position-fixed\nCode\n```\n\nAbsatz {.language-x .fixed-top}")]
    public void ToHtml_RemovesFreelyChosenClassesAndStyles(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.DoesNotContain("style=", html);
        Assert.DoesNotContain("class=\"position-fixed", html);
        Assert.DoesNotContain("stretched-link", html);
        Assert.DoesNotContain("fixed-top", html);
        Assert.DoesNotContain("w-100", html);
    }

    [Fact]
    public void ToHtml_KeepsWhatMarkdigGenerates()
    {
        var html = MarkdownRenderer.ToHtml("""
            # Titel

            | a | b |
            |:-:|--:|
            | 1 | 2 |

            - [x] erledigt

            Text[^1]

            [^1]: Fußnote

            ```csharp
            var x = 1;
            ```
            """);

        Assert.Contains("<h1 id=\"inhalt-titel\">", html);
        Assert.Contains("text-align: center", html);
        Assert.Contains("text-align: right", html);
        Assert.Contains("class=\"task-list-item\"", html);
        Assert.Contains("class=\"footnote-ref\"", html);
        Assert.Contains("class=\"language-csharp\"", html);
    }

    [Theory]
    [InlineData("![Logo](https://fremd.example/p.png)", "<a href=\"https://fremd.example/p.png\">Bild: Logo</a>")]
    [InlineData("![](http://fremd.example/p.png)", "<a href=\"http://fremd.example/p.png\">Bild: http://fremd.example/p.png</a>")]
    [InlineData("![Logo](//fremd.example/p.png)", "<a href=\"//fremd.example/p.png\">Bild: Logo</a>")]
    [InlineData("![Logo](https:/fremd.example/p.png)", "<a href=\"https:/fremd.example/p.png\">Bild: Logo</a>")]
    [InlineData("![Logo](https:fremd.example/p.png)", "<a href=\"https:fremd.example/p.png\">Bild: Logo</a>")]
    [InlineData("[![Logo](https://fremd.example/p.png)](https://ziel.example/)", "<a href=\"https://ziel.example/\">Logo</a>")]
    public void ToHtml_TurnsExternalImagesIntoLinks(string markdown, string erwartet)
    {
        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.Contains(erwartet, html);
        Assert.DoesNotContain("<img", html);
    }

    [Theory]
    [InlineData("![x](/\\fremd.example/p.png)")]
    [InlineData("![x](https://fremd.example/p.png){srcset=\"https://fremd.example/q.png 2x\"}")]
    [InlineData("![x](/bilder/a.png){srcset=\"https://fremd.example/q.png 2x\"}")]
    [InlineData("![Film](https://fremd.example/film.mp4)")]
    [InlineData("![Ton](https://fremd.example/ton.mp3)")]
    [InlineData("![Video](https://www.youtube.com/watch?v=abc)")]
    public void ToHtml_LoadsNothingFromOtherServers(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.DoesNotContain("src=\"http", html);
        Assert.DoesNotContain("src=\"//", html);
        Assert.DoesNotContain("src=\"/\\", html);
        Assert.DoesNotContain("srcset", html);
        Assert.DoesNotContain("<iframe", html);
    }

    [Theory]
    [InlineData("![Plan](/bilder/plan.png)", "<img src=\"/bilder/plan.png\" alt=\"Plan\">")]
    [InlineData("![Plan](plan.png)", "<img src=\"plan.png\" alt=\"Plan\">")]
    [InlineData("[Link](https://fremd.example/)", "<a href=\"https://fremd.example/\">Link</a>")]
    public void ToHtml_KeepsOwnImagesAndOrdinaryLinks(string markdown, string erwartet)
    {
        Assert.Contains(erwartet, MarkdownRenderer.ToHtml(markdown));
    }

    [Theory]
    [InlineData("/bilder/a.png", false)]
    [InlineData("a.png", false)]
    [InlineData("../a.png?x=1#y", false)]
    [InlineData("https://fremd.example/a.png", true)]
    [InlineData("//fremd.example/a.png", true)]
    [InlineData("/\\fremd.example/a.png", true)]
    [InlineData("\\\\fremd.example/a.png", true)]
    [InlineData("/\t/fremd.example/a.png", true)]
    [InlineData("data:image/png;base64,AAAA", true)]
    [InlineData("https:/fremd.example/a.png", true)]
    [InlineData("https:fremd.example/a.png", true)]
    [InlineData(" HTTPS:/fremd.example/a.png", true)]
    [InlineData("http:/fremd.example/a.png", true)]
    [InlineData("bilder/a:b.png", false)]
    [InlineData("./a:b.png", false)]
    [InlineData("?x=a:b", false)]
    public void IsExternal_RecognisesAddressesOutsideTheOwnServer(string url, bool fremd)
    {
        Assert.Equal(fremd, MarkdownRenderer.IsExternal(url));
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("Text {onclick=alert(1)}")]
    [InlineData("<script>alert(1)</script>")]
    public void ToHtml_RemovesScripts(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.DoesNotContain("javascript:", html);
        Assert.DoesNotContain("onclick", html);
        Assert.DoesNotContain("<script", html);
    }

    [Theory]
    [InlineData('>')]
    [InlineData('[')]
    public void ToHtml_RejectsDeeplyNestedMarkdown(char zeichen)
    {
        Assert.Throws<MarkdownException>(() => MarkdownRenderer.ToHtml(new string(zeichen, 200_000)));
    }

    [Fact]
    public void ToHtml_RejectsHtmlAboveTheLimit()
    {
        // Jeder Verweis auf die Fußnote wird im HTML um ein Vielfaches länger.
        var markdown = string.Concat(Enumerable.Repeat("[^1]", 1_000)) + "\n\n[^1]: x";

        Assert.Throws<MarkdownException>(() => MarkdownRenderer.ToHtml(markdown, maxHtmlLength: 10_000));
        Assert.Contains("footnote", MarkdownRenderer.ToHtml(markdown));
    }

    [Theory]
    [InlineData("Text {#getElementById}")]
    [InlineData("Text {id=cookie name=cookie}")]
    [InlineData("![a](/a.png){#forms name=forms}")]
    [InlineData("# getElementById")]
    public void ToHtml_KeepsBrowserNamesFromBeingOverridden(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.DoesNotContain("name=", html);
        Assert.DoesNotContain("id=\"getElementById\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id=\"cookie\"", html);
        Assert.DoesNotContain("id=\"forms\"", html);
        Assert.Contains("id=\"inhalt-", html);
    }

    [Fact]
    public void ToHtml_KeepsJumpsWithinThePageWorking()
    {
        var html = MarkdownRenderer.ToHtml("""
            # Erster Teil

            Siehe [unten](#zweiter-teil) und die Fußnote[^1], nicht aber [fremd](/doc#zweiter-teil) oder [leer](#).

            ## Zweiter Teil

            [^1]: Anmerkung
            """);

        Assert.Contains("<h2 id=\"inhalt-zweiter-teil\">", html);
        Assert.Contains("href=\"#inhalt-zweiter-teil\"", html);
        Assert.Contains("href=\"/doc#zweiter-teil\"", html);
        Assert.Contains("href=\"#\"", html);
        // Jedes Sprungziel innerhalb der Seite hat ein Element mit genau dieser id.
        var ziele = System.Text.RegularExpressions.Regex.Matches(html, "href=\"#([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.True(ziele.Count >= 3);
        Assert.All(ziele, ziel => Assert.Contains($"id=\"{ziel}\"", html));
    }
}
