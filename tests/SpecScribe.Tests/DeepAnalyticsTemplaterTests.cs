using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the dedicated deep-analytics page and the change-coupling graph chart it hosts.
/// [Story 3.2]</summary>
public class DeepAnalyticsTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private static DeepGitPulse SampleDeep() => new(
        Hotspots: new (string, int)[] { ("src/SpecScribe/HtmlTemplater.cs", 9), ("src/SpecScribe/Charts.cs", 4) },
        Coupling: new (string, string, int)[]
        {
            ("src/SpecScribe/Charts.cs", "src/SpecScribe/HtmlTemplater.cs", 5),
            ("src/SpecScribe/Charts.cs", "src/SpecScribe/SiteGenerator.cs", 3),
        });

    private static int Count(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    [Fact]
    public void RenderPage_HasSiteChromeAndBothSections()
    {
        var html = DeepAnalyticsTemplater.RenderPage(SampleDeep(), Nav());

        // Full page shell: skip link + single main landmark + breadcrumb, like the other synthesized pages.
        Assert.Contains("<a class=\"skip-link\" href=\"#main-content\">Skip to content</a>", html);
        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Contains("Deep Git Analytics", html);        // h1
        Assert.Contains(">Change Coupling</h2>", html);
        Assert.Contains(">Git Hotspots</h2>", html);
    }

    [Fact]
    public void RenderPage_RendersCouplingGraphListAndHotspots()
    {
        var html = DeepAnalyticsTemplater.RenderPage(SampleDeep(), Nav());

        // The graph is present...
        Assert.Contains("class=\"coupling-graph\"", html);
        // ...alongside its precise text companion (the ranked list) and a hotspot path.
        Assert.Contains("5&times; together", html);
        Assert.Contains("src/SpecScribe/HtmlTemplater.cs", html);
        Assert.Contains("git-pulse-bar-fill", html); // hotspot bars
    }

    [Fact]
    public void CouplingGraph_EmitsOneEdgePerPairAndOneNodePerDistinctFile()
    {
        var coupling = new (string, string, int)[]
        {
            ("src/A.cs", "src/B.cs", 5),
            ("src/A.cs", "src/C.cs", 3),
        };

        var svg = Charts.CouplingGraph(coupling);

        Assert.Equal(2, Count(svg, "class=\"coupling-edge\""));  // two pairs -> two edges
        Assert.Equal(3, Count(svg, "class=\"coupling-node\""));  // three distinct files -> three nodes
        // Node labels use the basename; the edge tooltip carries the co-change count.
        Assert.Contains(">A.cs<", svg);
        Assert.Contains(">B.cs<", svg);
        Assert.Contains("A.cs &harr; B.cs: 5&times; together", svg);
        // role="img" so the whole graph is announced as one named figure.
        Assert.Contains("role=\"img\"", svg);
    }

    [Fact]
    public void CouplingGraph_DegeneratesToFriendlyNoteWhenEmpty()
    {
        var svg = Charts.CouplingGraph(Array.Empty<(string, string, int)>());
        Assert.Contains("chart-empty", svg);
        Assert.Contains("No significant change coupling detected.", svg);
        Assert.DoesNotContain("<svg", svg);
    }

    [Fact]
    public void HotspotBars_EmptyDegradesToNote()
    {
        var bars = Charts.HotspotBars(Array.Empty<(string, int)>());
        Assert.Contains("chart-empty", bars);
        Assert.DoesNotContain("git-pulse-bar-fill", bars);
    }
}
