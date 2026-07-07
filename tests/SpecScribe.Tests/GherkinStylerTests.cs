using SpecScribe;

namespace SpecScribe.Tests;

public class GherkinStylerTests
{
    [Fact]
    public void StyleCriterion_SplitsEachKeywordOntoItsOwnLine()
    {
        var html = GherkinStyler.StyleCriterion(
            "<strong>Given</strong> a plan <strong>When</strong> it renders <strong>Then</strong> it reads <strong>And</strong> it links");

        // No literal space after the chip — the chip's CSS margin supplies the gap so the hanging
        // indent lands wrapped text on the same column as the clause's first word.
        Assert.Equal(4, Count(html, "<span class=\"gherkin-line\">"));
        Assert.Contains("<span class=\"gherkin-kw kw-given\">Given</span>a plan", html);
        Assert.Contains("<span class=\"gherkin-kw kw-when\">When</span>it renders", html);
        Assert.Contains("<span class=\"gherkin-kw kw-then\">Then</span>it reads", html);
        Assert.Contains("<span class=\"gherkin-kw kw-and\">And</span>it links", html);
        Assert.DoesNotContain("<strong>Given</strong>", html);
    }

    [Fact]
    public void StyleCriterion_LeavesNonKeywordBoldRunsAlone()
    {
        var html = GherkinStyler.StyleCriterion(
            "<strong>Given</strong> x <strong>Then</strong> y. <strong>Origin &amp; scope:</strong> first story of the epic.");

        Assert.Contains("<strong>Origin &amp; scope:</strong> first story of the epic.", html);
        // The trailing note rides inside the last gherkin line, not a line of its own.
        Assert.Equal(2, Count(html, "<span class=\"gherkin-line\">"));
    }

    [Fact]
    public void StyleCriterion_ReturnsHtmlWithoutKeywordsUnchanged()
    {
        const string input = "<strong>Note:</strong> no gherkin here, and \"given\" in prose stays plain.";
        Assert.Equal(input, GherkinStyler.StyleCriterion(input));
    }

    [Fact]
    public void StyleCriterion_KeepsProseBeforeTheFirstKeywordOutsideTheLineStructure()
    {
        var html = GherkinStyler.StyleCriterion("Preamble text. <strong>Given</strong> a thing");

        Assert.StartsWith("Preamble text. <span class=\"gherkin-line\">", html);
    }

    [Fact]
    public void StyleCriterion_ReturnsEmptyInputUnchanged()
        => Assert.Equal(string.Empty, GherkinStyler.StyleCriterion(string.Empty));

    [Fact]
    public void KeywordSpan_LowercasesTheClassButKeepsTheLabel()
        => Assert.Equal("<span class=\"gherkin-kw kw-when\">When</span>", GherkinStyler.KeywordSpan("When"));

    [Fact]
    public void StyleCriterion_StylesTheButKeyword()
    {
        var html = GherkinStyler.StyleCriterion("<strong>Then</strong> ok <strong>But</strong> not empty");

        Assert.Contains("<span class=\"gherkin-kw kw-but\">But</span>not empty", html);
    }

    [Fact]
    public void StyleCriterion_SplitsLinesWithinEachParagraphOfAMultiParagraphCriterion()
    {
        // A criterion with a trailing note keeps Markdig's <p> wrappers (RenderInline only strips one
        // enclosing pair). The clause paragraph must still get per-line chips; the note paragraph must
        // pass through untouched as its own paragraph — not glued into the last clause line.
        var html = GherkinStyler.StyleCriterion(
            "<p><strong>Given</strong> a surface <strong>When</strong> I view it</p>\n<p><strong>Origin &amp; scope:</strong> a note.</p>");

        Assert.Equal(2, Count(html, "<span class=\"gherkin-line\">"));
        Assert.Contains("<p><span class=\"gherkin-line\"><span class=\"gherkin-kw kw-given\">Given</span>a surface</span>", html);
        Assert.Contains("<p><strong>Origin &amp; scope:</strong> a note.</p>", html);
        Assert.DoesNotContain("<strong>Given</strong>", html);
    }

    [Fact]
    public void StyleCriterion_DegradeStaysScopedToTheParagraphWithTheNestedKeyword()
    {
        // Nested keyword in paragraph two degrades that paragraph only; paragraph one still gets lines.
        var html = GherkinStyler.StyleCriterion(
            "<p><strong>Given</strong> a <strong>Then</strong> b</p>\n<p><em>see <strong>And</strong> this</em></p>");

        Assert.Equal(2, Count(html, "<span class=\"gherkin-line\">"));
        Assert.Contains("<p><em>see <span class=\"gherkin-kw kw-and\">And</span> this</em></p>", html);
    }

    [Fact]
    public void StyleCriterion_DegradesToInPlaceStyling_WhenAKeywordIsNested()
    {
        // A keyword nested inside another inline element would make line-slicing emit overlapping tags,
        // so the styler falls back to styling the markers in place (no gherkin-line wrapping) — never
        // producing invalid HTML, and never mangling the surrounding <em>.
        var html = GherkinStyler.StyleCriterion("<em>note <strong>Given</strong> setup</em> <strong>Then</strong> result");

        Assert.DoesNotContain("gherkin-line", html);
        Assert.Contains("<em>note <span class=\"gherkin-kw kw-given\">Given</span> setup</em>", html);
        Assert.Contains("<span class=\"gherkin-kw kw-then\">Then</span> result", html);
    }

    private static int Count(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }
}
