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
        Assert.Contains("<main id=\"main-content\" class=\"deep-page\">", html);
        Assert.Contains("Deep Git Analytics", html);        // h1
        Assert.Contains(">Change Coupling</h2>", html);
        // Hotspots moved into the lower row beside the ranked pairs, so it now reads as a panel <h3>.
        Assert.Contains(">Git Hotspots</h3>", html);
    }

    [Fact]
    public void RenderPage_RendersCouplingGraphListAndHotspots()
    {
        var html = DeepAnalyticsTemplater.RenderPage(SampleDeep(), Nav());

        // The graph is present...
        Assert.Contains("class=\"coupling-graph\"", html);
        // ...alongside its precise text companion (the ranked pairs table) under a headed panel, and a hotspot.
        Assert.Contains("Ranked Pairs", html);
        Assert.Contains("class=\"coupling-table\"", html);
        Assert.Contains("<th scope=\"col\" class=\"coupling-num\">Together</th>", html);
        Assert.Contains("src/SpecScribe/HtmlTemplater.cs", html);
        Assert.Contains("git-pulse-bar-fill", html); // hotspot bars
        // The expand-to-lightbox affordance + its :target lightbox are wired (pure CSS, no JS).
        Assert.Contains("href=\"#coupling-zoom\"", html);
        Assert.Contains("id=\"coupling-zoom\"", html);
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

    // A resolver that lights up exactly one of the sample's files, so each surface can be checked for
    // per-item guarding (resolved → link, unresolved → plain) rather than all-or-nothing behavior.
    private static Func<string, string?> ChartsOnlyResolver() =>
        path => path == "src/SpecScribe/Charts.cs" ? "code/src/SpecScribe/Charts.cs.html" : null;

    [Fact]
    public void RenderPage_WithFileHref_LinksResolvedFilesAndLeavesOthersPlain()
    {
        var html = DeepAnalyticsTemplater.RenderPage(SampleDeep(), Nav(), fileHref: ChartsOnlyResolver());

        // Coupling table cell, hotspot list item, and graph node for the resolvable file all become links.
        Assert.Contains("<a href=\"code/src/SpecScribe/Charts.cs.html\">src/SpecScribe/Charts.cs</a>", html); // table + hotspot
        Assert.Contains("<a class=\"coupling-node-link\" href=\"code/src/SpecScribe/Charts.cs.html\">", html);  // graph node
        // The unresolved file stays plain text everywhere — per-item guarding, no dead link.
        Assert.DoesNotContain("<a href=\"code/src/SpecScribe/HtmlTemplater.cs.html\"", html);
        Assert.DoesNotContain("<a class=\"coupling-node-link\" href=\"code/src/SpecScribe/HtmlTemplater.cs.html\"", html);
    }

    [Fact]
    public void RenderPage_WithoutFileHref_RendersNoCodeLinks()
    {
        // The default (no resolver) path — the live behavior before this change — emits plain file text only.
        var html = DeepAnalyticsTemplater.RenderPage(SampleDeep(), Nav());
        Assert.DoesNotContain("href=\"code/", html);
        Assert.DoesNotContain("coupling-node-link", html);
    }

    [Fact]
    public void CouplingGraph_FileHref_WrapsResolvedNodeInSvgAnchorOnly()
    {
        var coupling = new (string, string, int)[] { ("src/A.cs", "src/B.cs", 5) };
        var svg = Charts.CouplingGraph(coupling, fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null);

        // Resolved node wrapped in an SVG <a>; the circle/label/title survive inside it.
        Assert.Contains("<a class=\"coupling-node-link\" href=\"code/src/A.cs.html\">", svg);
        Assert.Contains("role=\"img\"", svg);
        Assert.Equal(2, Count(svg, "class=\"coupling-node\""));       // both nodes still render
        Assert.Equal(1, Count(svg, "class=\"coupling-node-link\"")); // only the resolvable one is linked
    }

    [Fact]
    public void HotspotBars_FileHref_LinksResolvedPathsOnly()
    {
        var bars = Charts.HotspotBars(
            new (string, int)[] { ("src/A.cs", 9), ("src/B.cs", 4) },
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null);

        Assert.Contains("<a href=\"code/src/A.cs.html\">src/A.cs</a>", bars);
        Assert.DoesNotContain("href=\"code/src/B.cs.html\"", bars);
        Assert.Contains(">src/B.cs<", bars); // still present, just plain
    }

    [Fact]
    public void CodeItemLink_EscapesHrefAndLabel()
    {
        // A path with markup-significant characters must be escaped in both the href and the visible text.
        var bars = Charts.HotspotBars(
            new (string, int)[] { ("src/a<b>.cs", 3) },
            fileHref: _ => "code/a&b.html");

        Assert.Contains("href=\"code/a&amp;b.html\"", bars);
        Assert.Contains(">src/a&lt;b&gt;.cs<", bars);
        Assert.DoesNotContain("<b>", bars);
    }
}
