using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Coverage for the aggregate Git Insights hub page: the site a11y contract, the whole-tree code-ownership
/// sunburst + its accessible text-equivalent tree (Story 7.11 rewrite — replaces the earlier files-and-contributors
/// master-detail table AND the earlier plain ranked ownership table), escaping of repo-derived text, the guarded
/// file links (link when a resolver produces a target, plain text when not — never a dead link), the solo-repo
/// reframe, and friendly empty states.</summary>
public class GitInsightsTemplaterTests
{
    /// <summary>The rendered island's JSON body — Story 20.9 moved every per-file fact this page asserts out of
    /// 1,420 elements' `data-*` attributes and into one payload, so the assertions moved with them.</summary>
    private static string Island(string html)
    {
        var m = Regex.Match(html, "<script type=\"application/json\" class=\"ss-hierarchy-data\"[^>]*>(?<j>.*?)</script>", RegexOptions.Singleline);
        Assert.True(m.Success, "expected a Hierarchy Explorer island on this page");
        return m.Groups["j"].Value;
    }

    /// <summary>Every charted file's own `path` metric, read back through a real JSON parse - so a test that
    /// claims the payload round-trips is actually parsing it rather than pattern-matching the text.</summary>
    private static List<string?> IslandPaths(string html)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(Island(html));
        return doc.RootElement.GetProperty("nodes").EnumerateArray()
            .Where(n => n.TryGetProperty("metrics", out _))
            .Select(n => n.GetProperty("metrics").GetProperty("path").GetString())
            .ToList();
    }

    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private static GitInsightsData SampleInsights() => new(
        Files: new[]
        {
            new FileChangeStat("src/SpecScribe/Charts.cs", 9, 120, 40, "abc1234def", new DateOnly(2026, 7, 6),
                new[]
                {
                    new FileContributor("Alice", 7, new DateOnly(2026, 7, 6)),
                    new FileContributor("Bob", 2, new DateOnly(2026, 7, 2)),
                }, TotalContributors: 2),
            new FileChangeStat("src/SpecScribe/HtmlTemplater.cs", 4, 33, 12, "fff9999aaa", new DateOnly(2026, 7, 3),
                new[] { new FileContributor("Bob", 4, new DateOnly(2026, 7, 3)) }, TotalContributors: 1),
        },
        Activity: new[]
        {
            (new DateOnly(2026, 7, 2), 3),
            (new DateOnly(2026, 7, 6), 6),
        },
        CommitCount: 9,
        ContributorCount: 2,
        TotalFilesTouched: 2);

    /// <summary>The whole-tree CodeMap the ownership sunburst/tree render from — mirrors what
    /// <c>CodeMap.Build(_codeFiles, DeepGitPulse.CodeMapMetrics)</c> produces in the generator. Charts.cs: 9
    /// changes, Alice 7 -> 78% dominant share, 2 contributors (multi-author). HtmlTemplater.cs: 4 changes, Bob
    /// 4 -> 100% dominant share, 1 contributor (sole).</summary>
    private static CodeMap SampleCodeMap() => CodeMap.Build(
        new (string RepoRelativePath, long Lines)[]
        {
            ("src/SpecScribe/Charts.cs", 100),
            ("src/SpecScribe/HtmlTemplater.cs", 50),
        },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/SpecScribe/Charts.cs"] = new CodeFileMetrics(9, 160, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 6),
                Contributors: new[]
                {
                    new FileContributor("Alice", 7, new DateOnly(2026, 7, 6)),
                    new FileContributor("Bob", 2, new DateOnly(2026, 7, 2)),
                }, TotalContributors: 2),
            ["src/SpecScribe/HtmlTemplater.cs"] = new CodeFileMetrics(4, 45, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 3),
                Contributors: new[] { new FileContributor("Bob", 4, new DateOnly(2026, 7, 3)) }, TotalContributors: 1),
        });

    private static IReadOnlyList<string> SampleTopAuthors() => new[] { "Alice", "Bob" };

    private static GitPulse SamplePulse()
    {
        var day = new DateOnly(2026, 7, 6);
        var commits = new[] { new CommitInfo("abc1234", "Fix", "Alice", "10:00") };
        return new GitPulse(
            TotalCommits: 1,
            ActiveDays: 1,
            FirstCommitDate: day,
            LastCommitDate: day,
            DailySeries: new[] { (day, 1) },
            CommitsByDay: new Dictionary<DateOnly, IReadOnlyList<CommitInfo>> { [day] = commits },
            LastCommitTimestamp: new DateTime(2026, 7, 6, 10, 0, 0),
            Last30DayCommitCount: 1,
            TopChangedFiles: Array.Empty<(string, int)>());
    }

    [Fact]
    public void RenderPage_HasSiteChromeAndBothSections()
    {
        var page = GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors());
        var html = RegionAssert.Of(page);

        // Full page shell: skip link + single main landmark + breadcrumb, like the other synthesized pages.
        // [Story 23.6 AC #8] The skip-link assertion lived here and is NOT lost — it is head-emitted chrome,
        // and the region carries no head. `npm run check:a11y` owns `skip-link` over every EMITTED page,
        // which is the only place it can be asserted honestly now that no C# path composes a whole page.
        Assert.Contains("<main id=\"main-content\" class=\"deep-page git-insights\">", html);
        Assert.Contains("Git Insights</h1>", html);
        Assert.Contains(">Code Ownership &amp; Bus-Factor</h2>", html);
        Assert.Contains(">Activity Over Time</h2>", html);
        Assert.Contains("chart-frame-why", html);
        Assert.Contains(Charts.WhyText(Charts.ChartMetric.CodeOwnership), html);
        Assert.Contains(Charts.WhyText(Charts.ChartMetric.ActivityCadence), html);
        Assert.DoesNotContain("deep-page-lead", html);
        Assert.Contains("crumb-current", html); // breadcrumb trail back home

        // Owner feedback: Activity Over Time is the page's most immediately orienting chart, so it leads —
        // Code Ownership follows below it.
        Assert.True(html.IndexOf(">Activity Over Time</h2>", StringComparison.Ordinal)
            < html.IndexOf(">Code Ownership &amp; Bus-Factor</h2>", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderPage_DisclosesWhenTheFileCountPillIsTruncated()
    {
        // Insights.TotalFilesTouched exceeds Files.Count, so the header pill must say so rather than presenting
        // the capped count as the full total. [Review fix 2026-07-09, still load-bearing after the 7.11 rewrite]
        var insights = new GitInsightsData(
            Files: new[]
            {
                new FileChangeStat("src/SpecScribe/Charts.cs", 9, 120, 40, "abc1234def", new DateOnly(2026, 7, 6),
                    new[] { new FileContributor("Alice", 7, new DateOnly(2026, 7, 6)) }, TotalContributors: 5),
            },
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 9,
            ContributorCount: 5,
            TotalFilesTouched: 60);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(insights, null, Nav(), SampleCodeMap(), SampleTopAuthors()));

        Assert.Contains("top 1 of 60 files by commit count", html);
    }

    [Fact]
    public void RenderPage_RendersTheWholeTreeExplorerAndItsRealValueLegend()
    {
        // Story 20.9: the hand-rolled SVG is gone; the chart is the ONE Hierarchy Explorer over a
        // ProjectOwnership payload. The FACT this test has always asserted — the whole tree is charted, and its
        // legend prints real share ranges rather than a "Less ... More" placeholder — is unchanged and just moved.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));

        Assert.Contains(HierarchyExplorer.HostMarker, html);
        Assert.Contains("ss-hierarchy-data", html);
        Assert.DoesNotContain("<svg class=\"ownership-sunburst\"", html);
        Assert.DoesNotContain("<svg class=\"ownership-treemap\"", html);
        // Real-value legend (Story 10.2) — never the literal "Less … More" placeholder.
        Assert.Contains("ownership-legend", html);
        Assert.Contains("76–100%", html);
        Assert.DoesNotContain("Less", html);
        Assert.DoesNotContain("…More", html);
    }

    [Fact]
    public void RenderPage_OwnershipIsOneInstanceWithTheStandardSelector_NotTwoStackedCharts()
    {
        // Owner feedback (Story 7.11): sunburst OR treemap behind a toggle, never both stacked. Story 20.9 keeps
        // the affordance and deletes the mechanism: TWO server-rendered SVGs behind a pure-CSS `display:none`
        // pair become ONE instance whose selector re-types the trace in place. That collapse is the real shape of
        // the conversion, so it is asserted rather than merely described.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));

        Assert.Single(Regex.Matches(html, Regex.Escape(HierarchyExplorer.HostMarker + "></div>")));
        Assert.Single(Regex.Matches(html, "ss-hierarchy-data"));

        // The component's own selector, ordered Sunburst-then-Treemap site-wide (Story 20.7 D2), with THIS
        // surface's shipped default shape preserved.
        Assert.Contains("class=\"board-tab-radio ss-hierarchy-shape\" value=\"sunburst\" checked", html);
        Assert.Contains("class=\"board-tab-radio ss-hierarchy-shape\" value=\"treemap\"", html);

        // The retired pure-CSS view toggle and its two hidden view wrappers are gone by name.
        Assert.DoesNotContain("ownership-view-sunburst", html);
        Assert.DoesNotContain("ownership-view-treemap", html);
        Assert.DoesNotContain("ownership-cell", html);
    }

    [Fact]
    public void RenderPage_EmbedsTheSameGenerationTimeDataTheRetiredSvgCarried()
    {
        // ADR 0010 3 / ADR 0012 7: every mode's data is computed ONCE at generation time and embedded — nothing
        // re-derives from live git state or wall-clock `now`. The values are identical to the `data-*` the retired
        // SVG wrote (they were LIFTED, not re-derived); only their carrier changed, from attributes on 1,420
        // elements to one JSON island. Same numbers, same units, so no dimension can silently re-bucket.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));
        var island = Island(html);

        Assert.Contains("\"share\":\"78\"", island);   // Charts.cs: Alice 7/9 -> 78%
        Assert.Contains("\"share\":\"100\"", island);  // HtmlTemplater.cs: Bob 4/4 -> 100%
        Assert.Contains("\"dominant\":\"Alice\"", island);
        Assert.Contains("\"dominant\":\"Bob\"", island);
        Assert.Contains("\"contributors\":\"2\"", island);
        Assert.Contains("\"owner\":", island);

        // Panel-wide, so they live on the config rather than being repeated on every node.
        Assert.Contains("\"" + HierarchyExplorer.ConstantTopAuthors + "\":", island);
        Assert.Contains("\"" + HierarchyExplorer.ConstantAsOf + "\":", island);
    }

    [Fact]
    public void RenderPage_DeclaresTheFourOwnershipDimensions_WithTheShippedRulesIntact()
    {
        // AC#1: a surface may offer several dimensions and switching one re-colours in place. The rules ported
        // here are the ones whose arithmetic must NOT drift — share's fixed 25/50/75 cut points and the
        // spotlight's 30/90/180-day recency boundaries were both deliberate "meaningful on their own scale, never
        // a moving target" decisions, and re-deriving either would recolour every repo's chart.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));
        var island = Island(html);

        foreach (var key in new[] { "share", "top", "spotlight", "staleness" })
        {
            Assert.Contains("\"key\":\"" + key + "\"", island);
        }

        Assert.Contains("\"cutoffs\":[25,50,75]", island);
        Assert.Contains("\"cutoffs\":[30,90,180]", island);

        // The two dimensions owner decision D1 says cannot be precomputed declare the runtime control they take.
        Assert.Contains("\"arg\":\"" + HierarchyDimensionArg.Roster + "\"", island);
        Assert.Contains("\"arg\":\"" + HierarchyDimensionArg.Threshold + "\"", island);

        // The honest wording is DATA, ported verbatim — the softened spotlight-absence phrasing especially, which
        // must never harden back into the stronger and sometimes-false "has not worked on this file".
        Assert.Contains("most-active tracked contributors", island);
        Assert.Contains("(date unknown)", island);
        Assert.DoesNotContain("has not worked on this file", island);
    }

    [Fact]
    public void RenderPage_BuildsTheTextTwinThisSurfaceHasNeverHad()
    {
        // AC#3 and the reason it exists: Story 20.6's audit recorded this page as the one surface with NO twin at
        // all, because Story 7.11 deleted both prior ownership tables on owner feedback. Owner decision D3 gives
        // it the component's generic nested twin — every node the chart draws, nested by directory, each file
        // carrying its ownership facts as PROSE and a real resolving link.
        //
        // The completeness predicate is a SET match, not a count (Story 20.6 Task 1.3b): every file the payload
        // charts has to appear in the twin, not merely the same NUMBER of things.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));

        Assert.Contains("<details class=\"ss-hierarchy-twin\"", html);
        var twin = html[html.IndexOf("<details class=\"ss-hierarchy-twin\"", StringComparison.Ordinal)..];

        var charted = Regex.Matches(Island(html), "\"kind\":\"file\"").Count;
        Assert.True(charted > 0, "fixture must chart at least one file");

        foreach (Match m in Regex.Matches(Island(html), "\"path\":\"(?<p>[^\"]+)\""))
        {
            Assert.Contains(m.Groups["p"].Value, twin, StringComparison.Ordinal);
        }

        // Prose, not colour: the dominant author, the share %, the contributor count.
        Assert.Contains("Alice", twin, StringComparison.Ordinal);
        Assert.Contains("78% share", twin, StringComparison.Ordinal);
        Assert.Contains("contributor", twin, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderPage_ModeSelectorControlsShipHiddenForTheNoJsBaseline()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));

        // NFR-5 / ADR 0013: no inert control ships in the no-JS page. Story 20.9 moved these INSIDE the
        // component's own hidden control bar rather than giving them a second reveal of their own, so the
        // guarantee now comes from one place for every surface. Two nested `hidden` layers would have left the
        // select invisible after a successful mount, so the inner one is deliberately gone.
        Assert.Contains("<div class=\"ss-hierarchy-controls\" hidden>", html);
        Assert.Contains("ownership-mode-select", html);
        Assert.Contains("data-hierarchy-dimension", html);
        Assert.Contains("data-hierarchy-arg-wrap=\"" + HierarchyDimensionArg.Roster + "\" hidden>", html);
        Assert.Contains("data-hierarchy-arg-wrap=\"" + HierarchyDimensionArg.Threshold + "\" hidden>", html);

        // The legend bar too: a colour key for a chart that never renders is chrome for nothing.
        Assert.Contains("<div class=\"ss-hierarchy-legends\" hidden>", html);
    }

    [Fact]
    public void RenderPage_HasNoSeparateTextEquivalentTree()
    {
        // Owner feedback: the collapsible text-equivalent tree was removed entirely (not demoted) — the two
        // chart forms plus their rich per-file tooltips are the surface now.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));

        Assert.DoesNotContain("ownership-tree-details", html);
        Assert.DoesNotContain("ownership-tree-file", html);
        Assert.DoesNotContain("Full file list", html);
    }

    [Fact]
    public void RenderPage_EachFileWedgeCarriesARichHoverCardInsteadOfAPlainTooltip()
    {
        // Owner feedback: enhance the tooltips — every file wedge/cell gets the same rich .codemap-card hover
        // card BuildTreemapCard/RiskQuadrant already established, not a bare native <title>.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));

        // Story 20.9: the card survives the engine swap verbatim — Story 20.5 made `.ss-tooltip` +
        // `data-tip-html` the ONE tooltip system site-wide precisely so swapping the renderer never swaps the
        // tooltip's look. It now rides in the payload as a JSON string instead of a doubly-escaped attribute.
        var island = Island(html);
        Assert.Contains("\"tip\":", island);
        Assert.Contains("codemap-card", island);
        Assert.Contains("Dominant author", island);
        Assert.Contains("Alice (78%)", island);
        Assert.Contains("By commits", island);
    }

    [Fact]
    public void RenderPage_HasOneSharedLegendAreaWithAllFourModeSpecificBlocks()
    {
        // Owner feedback: the legend must always match what's actually colored — one shared legend area (not
        // duplicated per view) with four mode-specific blocks; the live JS switcher shows exactly one at a time.
        // Only the share-% block is visible without JS; the rest ship hidden (their mode selector is too).
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));

        // Story 20.9 ROUTES these four through the component's framing block rather than rewriting them, and
        // adds the marker that pairs each with the dimension that owns it — so "exactly one visible" became a
        // property of the shared component rather than a per-surface loop.
        Assert.Contains("class=\"ownership-legend ownership-legend-share\" data-hierarchy-legend=\"share\">", html);
        Assert.Contains("class=\"ownership-legend ownership-legend-top\" hidden data-hierarchy-legend=\"top\">", html);
        Assert.Contains("class=\"ownership-legend ownership-legend-spotlight\" hidden data-hierarchy-legend=\"spotlight\">", html);
        Assert.Contains("class=\"ownership-legend ownership-legend-staleness\" hidden data-hierarchy-legend=\"staleness\">", html);
        // The staleness legend's own "fresh" swatch — previously missing from every legend (owner feedback).
        Assert.Contains("ownership-legend-swatch owner-fresh", html);
        Assert.Contains("Touched within the threshold", html);
        // Exactly ONE instance of each — not duplicated per toggled view.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "ownership-legend-share"));
    }

    [Fact]
    public void RenderPage_IsNotFramedAsARankingOrScoreboard()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));

        // FR-10: descriptive attribution only, never a cross-repo people ranking — in every mode, including
        // the spotlight (recolorSpotlight answers "where has this person worked", never "who did the most").
        Assert.DoesNotContain("leaderboard", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("top performer", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("productivity", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Rank<", html);
    }

    [Fact]
    public void RenderPage_ReusesTheCommitHeatmapForActivity()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(SampleInsights(), SamplePulse(), Nav(), SampleCodeMap(), SampleTopAuthors()));

        // Activity over time = the existing accessible heatmap (whose active days link to per-day pages).
        // The headline is derived from the SAME pulse data as the heatmap (not insights.Activity), so the
        // two can never disagree — SamplePulse has 1 commit across 1 active day. [Review fix 2026-07-09]
        Assert.Contains("class=\"heatmap\"", html);
        Assert.Contains("commits/2026-07-06.html", html);
        Assert.Contains("1 commit across 1 active day", html);
    }

    [Fact]
    public void RenderPage_EscapesRepoDerivedText()
    {
        var codeMap = CodeMap.Build(
            new (string, long)[] { ("src/<weird> & \"odd\".cs", 10) },
            new Dictionary<string, CodeFileMetrics>
            {
                // TWO contributors on the same file (Review 2026-07-22: was one contributor here plus a
                // deliberately-inconsistent insights.ContributorCount of 2 to bypass the solo-repo gate — but the
                // gate now reads the SAME codeMap-derived contributor population the chart itself colors from, so
                // the fixture's own data must genuinely have >1 contributor for the section under test to render).
                ["src/<weird> & \"odd\".cs"] = new CodeFileMetrics(1, 10, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1),
                    Contributors: new[]
                    {
                        new FileContributor("<b>Eve</b> & Co", 1, new DateOnly(2026, 7, 1)),
                        new FileContributor("Second Contributor", 1, new DateOnly(2026, 6, 1)),
                    }, TotalContributors: 2),
            });
        var insights = new GitInsightsData(
            Files: Array.Empty<FileChangeStat>(),
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 1,
            ContributorCount: 2,
            TotalFilesTouched: 1);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(insights, null, Nav(), codeMap, Array.Empty<string>()));

        // The VISIBLE half - the text twin and every other rendered string - is HTML-escaped exactly as before.
        var visible = html.Replace(Island(html), string.Empty, StringComparison.Ordinal);
        Assert.Contains("src/&lt;weird&gt; &amp; &quot;odd&quot;.cs", visible);
        Assert.Contains("&lt;b&gt;Eve&lt;/b&gt; &amp; Co", visible);
        Assert.DoesNotContain("<weird>", visible);
        Assert.DoesNotContain("<b>Eve</b>", visible);

        // The ISLAND half is a different contract, and Story 20.9 changed it deliberately, so it is asserted
        // rather than assumed. The payload is JSON inside `<script type="application/json">`, i.e. RAW TEXT: a
        // bare `<` is not markup there, and escaping every one to a six-byte unicode sequence cost 2.8 MB on
        // code-map.html for no safety at all. What DOES matter is that the payload can never end or re-frame its
        // own element, so the two sequences that can - `</` and `<!` - are neutralized, and nothing else is.
        var island = Island(html);
        Assert.DoesNotContain("</", island, StringComparison.Ordinal);
        Assert.DoesNotContain("<!", island, StringComparison.Ordinal);

        // And it still round-trips: a consumer gets the ORIGINAL characters back, so neutralizing is not lossy.
        Assert.Contains("src/<weird> & \"odd\".cs", IslandPaths(html));
    }

    [Fact]
    public void RenderPage_APathThatLooksLikeAClosingScriptTag_CannotBreakOutOfTheIsland()
    {
        // The hostile case for the encoder change above, given its own test because "it happens not to occur in
        // this fixture" is not the same as "it cannot occur". A repo may legally contain a path with `</script`
        // in it, and if it did, the island would end early and the remainder of the payload would land in the
        // document as live markup.
        var evil = "src/</script><img src=x onerror=alert(1)>/<!--x.cs";
        var codeMap = CodeMap.Build(
            new (string, long)[] { (evil, 10) },
            new Dictionary<string, CodeFileMetrics>
            {
                [evil] = new CodeFileMetrics(1, 10, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1),
                    Contributors: new[]
                    {
                        new FileContributor("A", 1, new DateOnly(2026, 7, 1)),
                        new FileContributor("B", 1, new DateOnly(2026, 6, 1)),
                    }, TotalContributors: 2),
            });
        var insights = new GitInsightsData(
            Files: Array.Empty<FileChangeStat>(), Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 1, ContributorCount: 2, TotalFilesTouched: 1);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(insights, null, Nav(), codeMap, Array.Empty<string>()));
        var island = Island(html);

        // Neither sequence survives anywhere in the payload, so the element cannot be closed or re-framed...
        Assert.DoesNotContain("</", island, StringComparison.Ordinal);
        Assert.DoesNotContain("<!", island, StringComparison.Ordinal);
        // ...and the value is still recoverable in full, character for character.
        Assert.Contains(evil, IslandPaths(html));
    }

    [Fact]
    public void RenderPage_GuardsFileLinksOnTargetExistence()
    {
        var insights = SampleInsights();
        var codeMap = SampleCodeMap();

        // No resolver: every file link stays plain text/no href — no dead links.
        var unresolved = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(insights, null, Nav(), codeMap, SampleTopAuthors()));
        Assert.DoesNotContain("href=\"code/", unresolved);

        // With a resolver, the resolved file's wedge/tree entry becomes a real link; the unresolved file stays
        // plain text — per-entry guarding, not all-or-nothing.
        var resolved = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(
            insights, null, Nav(), codeMap, SampleTopAuthors(),
            fileHref: path => path == "src/SpecScribe/Charts.cs" ? "code/src/SpecScribe/Charts.cs.html" : null));
        Assert.Contains("href=\"code/src/SpecScribe/Charts.cs.html\"", resolved);
        Assert.Contains("src/SpecScribe/HtmlTemplater.cs", resolved);
        Assert.DoesNotContain("href=\"code/src/SpecScribe/HtmlTemplater.cs", resolved);
    }

    // ---- Story 7.11: solo-repo reframe (AC #4) ----

    [Fact]
    public void RenderPage_SoloRepoOwnershipReframesInsteadOfAnAllFlaggedSunburst()
    {
        var codeMap = CodeMap.Build(
            new (string, long)[] { ("src/A.cs", 10), ("src/B.cs", 5) },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/A.cs"] = new CodeFileMetrics(5, 10, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1),
                    Contributors: new[] { new FileContributor("Alice", 5, new DateOnly(2026, 7, 1)) }, TotalContributors: 1),
                ["src/B.cs"] = new CodeFileMetrics(3, 4, new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 2),
                    Contributors: new[] { new FileContributor("Alice", 3, new DateOnly(2026, 7, 2)) }, TotalContributors: 1),
            });
        var insights = new GitInsightsData(
            Files: Array.Empty<FileChangeStat>(),
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 8,
            ContributorCount: 1,
            TotalFilesTouched: 2);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(insights, null, Nav(), codeMap, new[] { "Alice" }));

        Assert.Contains("Single-maintainer project", html);
        Assert.Contains("gi-solo-repo-note", html);
        // No sunburst/mode-selector in the solo case — that would flag every wedge at-risk, noise not signal.
        Assert.DoesNotContain(HierarchyExplorer.HostMarker, html);
        Assert.DoesNotContain("ownership-controls", html);
    }

    [Fact]
    public void RenderPage_OwnershipSectionDegradesToFriendlyNoteWhenCodeMapIsEmpty()
    {
        var empty = new GitInsightsData(
            Files: Array.Empty<FileChangeStat>(),
            Activity: Array.Empty<(DateOnly, int)>(),
            CommitCount: 0,
            ContributorCount: 0,
            TotalFilesTouched: 0);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(GitInsightsTemplater.BuildPage(empty, null, Nav(), CodeMap.Empty, Array.Empty<string>()));

        Assert.Contains("No file change data available.", html);
        Assert.Contains("No activity data available.", html);
        Assert.DoesNotContain(HierarchyExplorer.HostMarker, html);
        Assert.DoesNotContain("<tbody>", html);
    }
}
