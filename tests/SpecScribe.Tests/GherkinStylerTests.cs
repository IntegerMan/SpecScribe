using SpecScribe;

namespace SpecScribe.Tests;

public class GherkinStylerTests
{
    [Fact]
    public void StyleCriterion_SplitsEachKeywordOntoItsOwnLine()
    {
        var html = GherkinStyler.StyleCriterion(
            "<strong>Given</strong> a plan <strong>When</strong> it renders <strong>Then</strong> it reads <strong>And</strong> it links");

        Assert.Equal(4, Count(html, "<span class=\"gherkin-line\">"));
        Assert.Contains("<span class=\"gherkin-kw kw-given\">Given</span> a plan", html);
        Assert.Contains("<span class=\"gherkin-kw kw-when\">When</span> it renders", html);
        Assert.Contains("<span class=\"gherkin-kw kw-then\">Then</span> it reads", html);
        Assert.Contains("<span class=\"gherkin-kw kw-and\">And</span> it links", html);
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
