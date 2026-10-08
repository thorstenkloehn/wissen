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

        Assert.Contains("<h1 id=\"titel\">", html);
        Assert.Contains("text-align: center", html);
        Assert.Contains("text-align: right", html);
        Assert.Contains("class=\"task-list-item\"", html);
        Assert.Contains("class=\"footnote-ref\"", html);
        Assert.Contains("class=\"language-csharp\"", html);
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
}
