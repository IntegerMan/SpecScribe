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
        })
    {
        DirectedCoupling = DirectedFrom(
            ("src/SpecScribe/Charts.cs", "src/SpecScribe/HtmlTemplater.cs", 5),
            ("src/SpecScribe/Charts.cs", "src/SpecScribe/SiteGenerator.cs", 3)),
    };

    /// <summary>Story 24.1: the page's Ranked Pairs panel reads <see cref="DeepGitPulse.DirectedCoupling"/>, not the
    /// symmetric <see cref="DeepGitPulse.Coupling"/> the graph draws — so a hand-built pulse must carry both, exactly
    /// as <c>ParseNumstatLog</c> populates them from one parse. Confidence here is synthetic (these fixtures have no
    /// per-file change counts to divide by); the metric math itself is covered in <c>GitMetricsFileInsightsTests</c>,
    /// so what these page tests need is only a populated, correctly-shaped directed view.</summary>
    private static IReadOnlyList<DirectedCouple> DirectedFrom(params (string A, string B, int Support)[] pairs) =>
        pairs.Select((p, i) => new DirectedCouple(
            p.A, p.B, p.Support,
            Confidence: 0.9 - (0.1 * i),
            Lift: null,
            CrossBoundary: GitMetrics.IsCrossBoundary(p.A, p.B),
            Kind: GitMetrics.ClassifyCoupling(p.A, p.B))).ToList();

    private static int Count(string haystack, string needle)
    {
        int n = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    [Fact]
    public void RenderPage_HasSiteChromeAndBothSections()
    {
        var page = DeepAnalyticsTemplater.BuildPage(SampleDeep(), Nav());
        var html = RegionAssert.Of(page);

        // Full page shell: skip link + single main landmark + breadcrumb, like the other synthesized pages.
        // [Story 23.6 AC #8] The skip-link assertion lived here and is NOT lost — it is head-emitted chrome,
        // and the region carries no head. `npm run check:a11y` owns `skip-link` over every EMITTED page,
        // which is the only place it can be asserted honestly now that no C# path composes a whole page.
        Assert.Contains("<main id=\"main-content\" class=\"deep-page\">", html);
        Assert.Contains("Deep Git Analytics", html);        // h1
        Assert.Contains(">Change Coupling</h3>", html);     // framed panel title (Story 10.2)
        // Hotspots sit in the lower row beside the ranked pairs as a framed panel <h3>.
        Assert.Contains(">Git Hotspots</h3>", html);
        Assert.Contains("chart-frame-why", html);
        Assert.Contains(Charts.WhyText(Charts.ChartMetric.ChangeCoupling), html);
        Assert.Contains(Charts.WhyText(Charts.ChartMetric.FileChurn), html);
        Assert.DoesNotContain("recent history", html);
        Assert.DoesNotContain("deep-page-lead", html);
        Assert.DoesNotContain("deep-page-note", html);
    }

    [Fact]
    public void RenderPage_CarriesNumericWindowAndRankingMetricFromSharedFrame()
    {
        // AnalyzedCommits + Insights.TotalFilesTouched drive honest window/ranking captions (Story 10.2).
        var deep = new DeepGitPulse(
            Hotspots: new (string, int)[] { ("src/A.cs", 9), ("src/B.cs", 4) },
            Coupling: new (string, string, int)[] { ("src/A.cs", "src/B.cs", 5) },
            AnalyzedCommits: 42)
        {
            Insights = new GitInsightsData(
                Files: Array.Empty<FileChangeStat>(),
                Activity: Array.Empty<(DateOnly, int)>(),
                CommitCount: 42,
                ContributorCount: 1,
                TotalFilesTouched: 100),
            DirectedCoupling = DirectedFrom(("src/A.cs", "src/B.cs", 5)),
        };

        var html = JsonSpaRenderAdapter.Shared.RenderContent(DeepAnalyticsTemplater.BuildPage(deep, Nav()));

        Assert.Contains("Last 42 commits", html);
        Assert.Contains("Top 2 of 100 files by change count", html);
        // Story 24.1: the ranked panel is directed and confidence-ranked, so its caption names that ranking rather
        // than the graph's shared-commit one — the two panels are deliberately different populations.
        Assert.Contains("Top 1 directed couple by confidence", html);
        Assert.Contains("class=\"chart-frame-window\"", html);
        Assert.Contains("class=\"chart-frame-ranking\"", html);
        // Framing sentences come from Charts.WhyText — no project-specific filenames in the why copy.
        Assert.DoesNotContain("SpecScribe", Charts.WhyText(Charts.ChartMetric.FileChurn));
        Assert.DoesNotContain("SpecScribe", Charts.WhyText(Charts.ChartMetric.ChangeCoupling));
    }

    [Fact]
    public void RenderPage_RendersCouplingGraphListAndHotspots()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(DeepAnalyticsTemplater.BuildPage(SampleDeep(), Nav()));

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
        var html = JsonSpaRenderAdapter.Shared.RenderContent(DeepAnalyticsTemplater.BuildPage(SampleDeep(), Nav(), fileHref: ChartsOnlyResolver()));

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
        var html = JsonSpaRenderAdapter.Shared.RenderContent(DeepAnalyticsTemplater.BuildPage(SampleDeep(), Nav()));
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

    // ---- Story 10.6 AC1: process-vs-code coupling annotation ----

    [Fact]
    public void RenderPage_MarksProcessPairsWithKindBadgeAndDashedEdgeAndExplanatoryNote()
    {
        var deep = new DeepGitPulse(
            Hotspots: Array.Empty<(string, int)>(),
            Coupling: new (string, string, int)[]
            {
                ("src/A.cs", "src/B.cs", 5),               // code <-> code
                ("sprint-status.yaml", "theme.css", 4),      // process <-> process
            })
        {
            DirectedCoupling = DirectedFrom(
                ("src/A.cs", "src/B.cs", 5),
                ("sprint-status.yaml", "theme.css", 4)),
        };

        var html = JsonSpaRenderAdapter.Shared.RenderContent(DeepAnalyticsTemplater.BuildPage(deep, Nav()));

        // The explanatory note appears once, via the shared frame's Note slot.
        Assert.Contains("chart-frame-note", html);
        Assert.Contains(Charts.ProcessCouplingNote, html);
        // Table: the process pair carries a visible "Process" badge; the code pair's Kind cell is empty.
        Assert.Contains("coupling-kind-badge", html);
        Assert.Contains(">Process<", html);
        // Graph: the process edge is dashed (a second class, never color-only); the code edge is not. The graph
        // renders twice (main panel + :target lightbox), so each edge kind appears once per render.
        Assert.Equal(2, Count(html, "class=\"coupling-edge process-edge\""));
        Assert.Equal(2, Count(html, "class=\"coupling-edge\""));
        // NFR8: the shared note text never names a SpecScribe-specific file.
        Assert.DoesNotContain("sprint-status.yaml", Charts.ProcessCouplingNote);
        Assert.DoesNotContain("specscribe.css", Charts.ProcessCouplingNote);
    }

    [Fact]
    public void RenderPage_NoProcessPairsOmitsNoteAndDashedEdges()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(DeepAnalyticsTemplater.BuildPage(SampleDeep(), Nav()));

        Assert.DoesNotContain("chart-frame-note", html);
        Assert.DoesNotContain("process-edge", html);
        Assert.DoesNotContain("coupling-kind-badge", html);
    }

    [Fact]
    public void CouplingTable_ProcessPairGetsBadgeCodePairDoesNot()
    {
        var coupling = new[]
        {
            Directed("src/A.cs", "src/B.cs", 3, 0.75),
            Directed("src/A.cs", "package-lock.json", 2, 0.5, kind: GitMetrics.CouplingKind.Process),
        };

        var table = Charts.CouplingTable(coupling);

        Assert.Contains("<th scope=\"col\" class=\"coupling-kind\">Kind</th>", table);
        Assert.Equal(1, Count(table, "coupling-kind-badge"));
    }

    // ---- Story 24.1: the hub table is directed, confidence-ranked, and marks cross-boundary couples ----

    private static DirectedCouple Directed(
        string from, string to, int support, double confidence, double? lift = null,
        bool crossBoundary = false, GitMetrics.CouplingKind kind = GitMetrics.CouplingKind.Code)
        => new(from, to, support, confidence, lift, crossBoundary, kind);

    [Fact]
    public void CouplingTable_RendersADirectionalConfidenceColumnAlongsideTheSharedCommitCount()
    {
        var table = Charts.CouplingTable(new[] { Directed("src/A.cs", "src/B.cs", 4, 0.8) });

        Assert.Contains("<th scope=\"col\" class=\"coupling-num\">Confidence</th>", table);
        Assert.Contains(">80%</td>", table);
        // Support is kept, not replaced — it is what makes a confidence trustworthy.
        Assert.Contains(">4&times;</td>", table);
    }

    [Fact]
    public void CouplingTable_CrossBoundaryCoupleCarriesAWordBadgeNotColourAlone()
    {
        var table = Charts.CouplingTable(new[] { Directed("src/A.cs", "tests/B.cs", 3, 0.6, crossBoundary: true) });

        Assert.Contains("coupling-boundary-badge", table);
        Assert.Contains(">Cross-boundary</span>", table);
    }

    [Fact]
    public void CouplingTable_ProcessAndCrossBoundaryAreIndependentAndCanBothAppear()
    {
        // The two lenses are orthogonal: a config file in another module is both routine upkeep AND a boundary
        // crossing. Neither badge may suppress the other.
        var table = Charts.CouplingTable(new[]
        {
            Directed("src/A.cs", "config/app.yaml", 3, 0.6, crossBoundary: true, kind: GitMetrics.CouplingKind.Process),
        });

        Assert.Contains("coupling-kind-badge", table);
        Assert.Contains("coupling-boundary-badge", table);
    }

    [Fact]
    public void CouplingTable_OrdinaryCodePairLeavesTheKindCellBlank()
    {
        var table = Charts.CouplingTable(new[] { Directed("src/A.cs", "src/B.cs", 3, 0.6) });

        Assert.Contains("<td class=\"coupling-kind\"></td>", table);
    }

    [Fact]
    public void CouplingTable_UndefinedLift_OmitsItFromTheTooltipRatherThanRenderingNaN()
    {
        var withLift = Charts.CouplingTable(new[] { Directed("src/A.cs", "src/B.cs", 4, 0.8, lift: 2.5) });
        var without = Charts.CouplingTable(new[] { Directed("src/A.cs", "src/B.cs", 4, 0.8, lift: null) });

        Assert.Contains("2.5&#215; its usual rate", withLift);
        Assert.DoesNotContain("usual rate", without);
        Assert.DoesNotContain("NaN", without);
        Assert.DoesNotContain("Infinity", without);
    }

    [Fact]
    public void CouplingTable_EmptyDirectedList_StillRendersTheFriendlyEmptyState()
    {
        var table = Charts.CouplingTable(Array.Empty<DirectedCouple>());

        Assert.Contains("chart-empty", table);
        Assert.Contains("No significant change coupling detected.", table);
    }

    [Fact]
    public void RenderPage_RankingCaptionNamesTheConfidenceRankingTheTableActuallyUsed()
    {
        // The graph above ranks by shared commits and the table by confidence — two different populations, so the
        // caption must describe THIS panel's ranking rather than inherit the graph's.
        var deep = SampleDeep() with
        {
            DirectedCoupling = new[] { Directed("src/A.cs", "src/B.cs", 3, 0.75) },
        };

        var html = JsonSpaRenderAdapter.Shared.RenderContent(DeepAnalyticsTemplater.BuildPage(deep, Nav()));

        Assert.Contains("directed couple", html);
        Assert.Contains("by confidence", html);
        Assert.DoesNotContain("coupled pairs by shared commits", html);
    }

    [Fact]
    public void CouplingGraph_ProcessEdgeIsDashedWithTitleSuffix()
    {
        var coupling = new (string, string, int)[] { ("config.yaml", "src/A.cs", 4) };

        var svg = Charts.CouplingGraph(coupling);

        Assert.Contains("class=\"coupling-edge process-edge\"", svg);
        Assert.Contains("(process-coupling)", svg);
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
