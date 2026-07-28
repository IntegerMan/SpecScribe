using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Page-level coverage for the code-map templater (Story 7.6, round 2): the standard shell, the
/// always-present legend, the JS-revealed (hidden) colorize dropdown + drill breadcrumb, the "git data unavailable"
/// notice when metrics are absent, the text-equivalent table (ordered by change frequency, guarded code-page links),
/// and the four precomputed exclude-filter panels behind the pure-CSS checkbox toggle. [Story 7.6]</summary>
public class CodeMapTemplaterTests
{
    /// <summary>One variant panel's island JSON. Story 20.9 moved every per-file fact these tests assert out of
    /// two SVGs' markup and into one payload per panel, so the assertions moved with them.</summary>
    private static string Island(string html, string variantKey)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            html,
            "<script type=\"application/json\" class=\"ss-hierarchy-data\" id=\"codemap-" + System.Text.RegularExpressions.Regex.Escape(variantKey) + "-data\">(?<j>.*?)</script>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(m.Success, "expected an island for the '" + variantKey + "' panel");
        return m.Groups["j"].Value;
    }

    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasCodeMap: true);

    private static readonly IReadOnlyDictionary<string, CodeFileMetrics> NoMetrics = new Dictionary<string, CodeFileMetrics>();

    private static IReadOnlyList<CodeMapVariant> VariantsWithMetrics() => CodeMap.BuildVariants(
        new[] { ("src/A.cs", 300L), ("src/B.cs", 50L) },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/A.cs"] = new CodeFileMetrics(8, 200, new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 10), AvgCoChanged: 3.4),
            ["src/B.cs"] = new CodeFileMetrics(2, 20, new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 20), AvgCoChanged: 1.0),
        });

    private static IReadOnlyList<CodeMapVariant> VariantsWithoutMetrics(params (string Path, long Lines)[] files) =>
        CodeMap.BuildVariants(files, NoMetrics);

    [Fact]
    public void RenderPage_WithMetrics_HasShellLegendHiddenControlsAndOrderedTable()
    {
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());

        // Standard standalone-page shell.
        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Contains("class=\"breadcrumb\"", html);
        Assert.Contains("Code Map", html);

        // Story 20.9: the colorize picker and both legends now ride inside the component's own hidden control and
        // legend bars, so the "no inert control with JS off" guarantee comes from ONE place for every surface
        // instead of a per-panel reveal. The inner `hidden` is deliberately gone - two nested layers would have
        // left the select invisible after a successful mount.
        Assert.Contains("codemap-legend", html);
        Assert.Contains("<div class=\"ss-hierarchy-controls\" hidden>", html);
        Assert.Contains("<div class=\"ss-hierarchy-legends\" hidden>", html);
        Assert.Contains("class=\"codemap-controls\">", html);
        Assert.Contains("class=\"codemap-dim-select\" data-hierarchy-dimension", html);
        Assert.Contains("value=\"changes\" selected", html);
        Assert.Contains("value=\"avgchange\"", html);
        Assert.Contains("value=\"cochange\"", html);          // "Files changed together" colorize dimension
        Assert.Contains("value=\"churn\"", html);              // round 2: churn is a colorize option
        Assert.Contains(">Churn</option>", html);
        // Story 7.9: "File type" is a 7th option, unselected — the sequential default (change frequency) is
        // unchanged (AC #3), and its ramp legend ships visible while the discrete legend ships pre-rendered hidden.
        Assert.Contains("value=\"filetype\">File type</option>", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-ramp\" data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapRampLegend + "\">", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-discrete\" data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapDiscreteLegend + "\" hidden>", html);
        // The ramp caption is a TEMPLATE the component substitutes the active dimension's label into, so the words
        // stay this surface's and the shared component never learns them (Task 1.8).
        Assert.Contains("data-hierarchy-legend-caption=\"Colorized by {label}\"", html);

        // The text table gains a "Together" column carrying the per-file average co-changed file count, and an
        // always-present "Type" column (Story 7.9).
        Assert.Contains(">Together</th>", html);
        Assert.Contains(">Type</th>", html);
        Assert.Contains(">C#</td>", html);                    // src/A.cs classifies as C#
        Assert.Contains(">3.4</td>", html);                   // src/A.cs's average co-changed files

        // First/Last dates render via the portal's human-readable token, not raw ISO.
        Assert.Contains("Jun 1, 2026", html);
        Assert.DoesNotContain("2026-06-01", html);
        // The bespoke `#dir=` drill breadcrumb is GONE - the component supplies the breadcrumb, and AC#2's "drill
        // behavior preserved" means the behaviour, not this markup.
        Assert.DoesNotContain("codemap-drill", html);
        Assert.Contains("ss-hierarchy-breadcrumb", html);

        // Metrics present → no "unavailable" notice for the full (default) view.
        Assert.DoesNotContain("Git change data is unavailable", html);

        // The text-equivalent table lists every file with its metrics, ordered by change frequency (A=8 before B=2).
        Assert.Contains("codemap-table", html);
        Assert.Contains("src/A.cs", html);
        Assert.Contains("src/B.cs", html);
        Assert.True(html.IndexOf("src/A.cs", StringComparison.Ordinal) < html.IndexOf("src/B.cs", StringComparison.Ordinal),
            "the busier file (more changes) is listed first");

        // The treemap card and its text-equivalent table are SIBLING chart-panels, never one nested in the other.
        Assert.DoesNotContain("chart-panel codemap-panel\">\n\n    <section class=\"chart-panel\"", html);
    }

    [Fact]
    public void RenderPage_WithoutMetrics_ShowsSecondaryNoticeButKeepsAWorkingFileTypeDimension()
    {
        // Story 7.9: file type needs no git data, so the controls/legend are no longer fully hidden when
        // hasMetrics is false — only the six git-derived dimensions are unavailable, which the (now secondary)
        // notice explains.
        var html = CodeMapTemplater.RenderPage(VariantsWithoutMetrics(("src/A.cs", 10L)), Nav());

        Assert.Contains("Git change data is unavailable", html);           // secondary graceful-degradation notice (AC #2)
        Assert.Contains("codemap-notice-secondary", html);                 // demoted from a full-replacement block
        Assert.Contains("codemap-dim-select", html);                       // colorize dropdown IS present (file type works)
        Assert.Contains("value=\"filetype\" selected", html);              // and it's the sole, baked-in default option
        Assert.DoesNotContain("value=\"changes\"", html);                  // the six git-derived options are absent
        Assert.Contains("codemap-legend-discrete", html);                  // discrete legend renders (visible, not hidden)
        Assert.Contains("class=\"codemap-legend codemap-legend-discrete\" data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapDiscreteLegend + "\">", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-ramp\" data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapRampLegend + "\" hidden>", html);

        // And the payload offers exactly ONE dimension, because there is nothing for the six git-derived ramps to
        // quantize - the same rule the dropdown has always followed, now stated once in the dimension contract.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(Island(html, "full"), "\"kind\":\"(?:ramp|ramp-window|categorical|cutoff|roster|spotlight|threshold)\""));
        Assert.Contains("\"key\":\"filetype\"", Island(html, "full"));

        // The text table still lists the file (sized-by-LOC is always meaningful) with its Type column populated.
        Assert.Contains("codemap-table", html);
        Assert.Contains("src/A.cs", html);
        Assert.Contains(">Type</th>", html);
        Assert.Contains(">C#</td>", html); // src/A.cs classifies as C#
    }

    [Fact]
    public void RenderPage_TableLinksFilesOnlyWhenResolverReturnsATarget()
    {
        var variants = VariantsWithoutMetrics(("src/A.cs", 10L));

        var linked = CodeMapTemplater.RenderPage(variants, Nav(),
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null);
        Assert.Contains("<a href=\"code/src/A.cs.html\">src/A.cs</a>", linked);

        var plain = CodeMapTemplater.RenderPage(variants, Nav(), fileHref: null);
        Assert.DoesNotContain("code/src/A.cs.html", plain);
    }

    [Fact]
    public void RenderPage_FileTableIsASetMatchAgainstTheChartPayload_NotJustACountMatch()
    {
        // [Review][Patch] Git Insights' own twin test does this SET match (Story 20.6 Task 1.3b's predicate: a
        // count match is not a set match), but Code Map's file table — this surface's declared twin (D1) — had no
        // equivalent; only row-count assertions existed at the detail-cap boundary. Every path the "full" panel's
        // chart payload charts must have a resolving row in that SAME panel's table.
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());
        var fullTable = html[html.IndexOf("data-view=\"full\"", StringComparison.Ordinal)..html.IndexOf("data-view=\"no-spec\"", StringComparison.Ordinal)];

        var charted = System.Text.RegularExpressions.Regex.Matches(Island(html, "full"), "\"path\":\"(?<p>[^\"]+)\"");
        Assert.True(charted.Count > 0, "fixture must chart at least one file");
        foreach (System.Text.RegularExpressions.Match m in charted)
        {
            Assert.Contains(m.Groups["p"].Value, fullTable, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RenderPage_EmitsFourPanelsAndTwoPureCssFilterCheckboxes()
    {
        var variants = VariantsWithoutMetrics(
            (".agents/skills/bmad-dev/workflow.md", 10L),
            ("tests/SpecScribe.Tests/GitMetricsTests.cs", 20L),
            ("src/SpecScribe/GitMetrics.cs", 30L));

        var html = CodeMapTemplater.RenderPage(variants, Nav());

        // The two checkboxes are unwrapped siblings of the four panels (the CSS toggle depends on this), each
        // with a real id the CSS/JS reference and an associated label (not nested — for/id association instead).
        // Owner decision D2 of Story 20.9 KEEPS this pure-CSS - it is the one filter on this page that works with
        // JavaScript off. `data-hierarchy-reveal` is the only addition: three of the four panels are
        // `display:none` at load and Plotly cannot lay out in a zero-width container (F1), so the component needs
        // a signal that a mount may have become possible. It says nothing about this page.
        Assert.Contains("<input type=\"checkbox\" id=\"cm-exclude-spec\" class=\"codemap-filter-checkbox\" data-hierarchy-reveal>", html);
        Assert.Contains("<label for=\"cm-exclude-spec\"", html);
        Assert.Contains("<input type=\"checkbox\" id=\"cm-exclude-tests\" class=\"codemap-filter-checkbox\" data-hierarchy-reveal>", html);
        Assert.Contains("<label for=\"cm-exclude-tests\"", html);

        // All four filter-combination panels are present, each self-contained (no shared ids to collide across
        // panels — the JS enhancement scopes every lookup per panel via class selectors).
        Assert.Contains("data-view=\"full\"", html);
        Assert.Contains("data-view=\"no-spec\"", html);
        Assert.Contains("data-view=\"no-tests\"", html);
        Assert.Contains("data-view=\"no-spec-no-tests\"", html);
        Assert.DoesNotContain("id=\"codemap-svg\"", html); // no global svg id (would collide across panels)

        // Each filtered (non-"full") panel that still has content notes what was excluded — the honest, text
        // equivalent of the visual filter (color/visibility is never the sole signal here either).
        // The old bespoke `.codemap-view-note` paragraph became each panel's own framed TITLE, which is the
        // frame's own slot for exactly this and stops four identically-headed panels sitting in one document.
        Assert.Contains("Source Code Map — excluding spec-driven development directories</h3>", html);
        Assert.Contains("Source Code Map — excluding tests</h3>", html);
        Assert.Contains("Source Code Map — excluding spec-driven development directories and tests</h3>", html);
        Assert.Contains("Source Code Map — every file</h3>", html);

        // The "no-spec-no-tests" panel's table lists only the one surviving file.
        Assert.Contains("src/SpecScribe/GitMetrics.cs", html);
    }

    [Fact]
    public void RenderPage_APanelThatExcludesEveryFileShowsANoFilesNoticeInsteadOfAnEmptyTreemap()
    {
        var variants = VariantsWithoutMetrics(("tests/OnlyTests/FooTests.cs", 10L));

        var html = CodeMapTemplater.RenderPage(variants, Nav());

        Assert.Contains("No files match this filter.", html);
    }

    // ---- Merged shape (Treemap/Sunburst) x dimension toggle (Story 7.12 review) ------------

    [Fact]
    public void RenderPage_EachPanelIsOneInstanceWithTheStandardSelector_NotTwoStackedShapes()
    {
        // Story 7.12's owner-directed merge made "what to view" (colorize) and "how to view it" (shape) orthogonal
        // axes on ONE panel. Story 20.9 keeps that framing and deletes the mechanism: two server-rendered SVGs
        // behind a `display:none` pair become one instance whose selector re-types the trace in place. So 8 charts
        // across four panels become 4 instances - which is the real shape of this conversion.
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());

        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(html, System.Text.RegularExpressions.Regex.Escape(HierarchyExplorer.HostMarker + "></div>")).Count);
        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(html, "ss-hierarchy-data").Count);

        // The retired pure-CSS shape toggle and both SVG wrappers are gone by name.
        Assert.DoesNotContain("codemap-shape", html);
        Assert.DoesNotContain("class=\"codemap-sunburst\"", html);
        Assert.DoesNotContain("cs-sunburst-radio", html);

        // Ordered Sunburst-then-Treemap site-wide (Story 20.7 D2) with THIS surface's shipped default preserved.
        Assert.Contains("class=\"board-tab-radio ss-hierarchy-shape\" value=\"treemap\" checked", html);
        Assert.Contains(">Treemap</label>", html);
        Assert.Contains(">Sunburst</label>", html);

        // Still only ONE "Colorize by" dropdown per panel - it governs both shapes, not a copy per shape.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(
            html[html.IndexOf("data-view=\"full\"", StringComparison.Ordinal)..html.IndexOf("data-view=\"no-spec\"", StringComparison.Ordinal)],
            "class=\"codemap-controls\""));
    }

    [Fact]
    public void RenderPage_EachFilterPanelGetsItsOwnSunburstSoTheCheckboxesActuallyReFilterIt()
    {
        // Owner feedback: a separate freshness-only section sourced from the unfiltered tree looked "frozen" next
        // to a treemap/table that visibly changed when a checkbox was toggled. Both shapes must now come from the
        // SAME per-variant Roots/Layout as everything else in the panel.
        var variants = CodeMap.BuildVariants(
            new[] { ("tests/OnlyTests/FooTests.cs", 10L), ("src/A.cs", 20L) }, NoMetrics);

        var html = CodeMapTemplater.RenderPage(variants, Nav());

        // Each panel now carries ONE payload covering both shapes, so a file appears ONCE per panel rather than
        // once per shape: "full" charts 2 files, and "no-tests" excludes the test file, leaving 1.
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(Island(html, "full"), "\"kind\":\"file\"").Count);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(Island(html, "no-tests"), "\"kind\":\"file\"").Count);

        // ...and the per-variant file TABLE - this surface's text twin (Story 20.6 D1) - re-filters with it, so
        // the JS-off reading of each panel matches the chart it stands in for.
        var fullSection = html[html.IndexOf("data-view=\"full\"", StringComparison.Ordinal)..html.IndexOf("data-view=\"no-spec\"", StringComparison.Ordinal)];
        var noTestsSection = html[html.IndexOf("data-view=\"no-tests\"", StringComparison.Ordinal)..html.IndexOf("data-view=\"no-spec-no-tests\"", StringComparison.Ordinal)];
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(fullSection, "class=\"codemap-table-row\"").Count);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(noTestsSection, "class=\"codemap-table-row\"").Count);
    }

    [Fact]
    public void RenderPage_EachPanelGetsADistinctDomIdAndHashKeySoDeepLinksCannotCollide()
    {
        // Story 20.9 F4: four component instances coexist in one document. Each needs its own DomId (it drives
        // the host id, the island id, the selector radio ids and the twin) AND its own HashKey, or drilling one
        // panel would rewrite the fragment another panel reads back. The variant key is the natural discriminator
        // and is already in the markup as `data-view`.
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());

        foreach (var key in new[] { "full", "no-spec", "no-tests", "no-spec-no-tests" })
        {
            Assert.Contains("id=\"codemap-" + key + "\" " + HierarchyExplorer.HostMarker, html);
            Assert.Contains("id=\"codemap-" + key + "-data\"", html);
            Assert.Contains("\"hashKey\":\"cm-" + key + "\"", Island(html, key));
        }

        var domIds = System.Text.RegularExpressions.Regex.Matches(Island(html, "full") + Island(html, "no-spec") + Island(html, "no-tests") + Island(html, "no-spec-no-tests"), "\"domId\":\"(?<d>[^\"]+)\"")
            .Select(m => m.Groups["d"].Value).ToList();
        Assert.Equal(4, domIds.Count);
        Assert.Equal(4, domIds.Distinct(StringComparer.Ordinal).Count());

        // Every selector radio id is scoped to its instance, so no two panels' toggles cross-wire.
        var radioNames = System.Text.RegularExpressions.Regex.Matches(html, "name=\"(codemap-[^\"]*-shape)\"")
            .Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.Equal(4, radioNames.Count);
    }

    [Fact]
    public void RenderPage_WithoutMetrics_BothShapesColorizeByFileTypeFromOnePayload()
    {
        // The FACT the retired assertion carried: with no git data, file type is what colours a file, and it does
        // so identically in both shapes. One payload now serves both, so the class family and the categorical
        // metric are what the test can honestly check - the level is resolved client-side per dimension.
        var html = CodeMapTemplater.RenderPage(VariantsWithoutMetrics(("src/A.cs", 10L)), Nav());
        var island = Island(html, "full");

        Assert.Contains("\"colorClass\":\"codemap-cell\"", island);
        Assert.Contains("\"filetype\":\"csharp\"", island);
        Assert.Contains("\"filetype-label\":\"C#\"", island);
        Assert.Contains("\"classPrefix\":\"type-\"", island);
    }

    [Fact]
    public void RenderPage_ChartLinksAFileOnlyWhenTheResolverReturnsATarget()
    {
        // Story 7.1's link guard, unchanged by the conversion: a resolver returning null leaves a plain, focusable
        // node - never a broken link - and the chart must thread the SAME guarded resolver the file table does.
        var linked = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav(),
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null);
        var linkedIsland = Island(linked, "full");
        Assert.Contains("\"href\":\"code/src/A.cs.html\"", linkedIsland);
        // src/B.cs resolves to nothing, so it carries no href at all rather than a dead one.
        Assert.Contains("\"path\":\"src/B.cs\"", linkedIsland);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(linkedIsland, "\"href\":"));

        var plain = Island(CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav(), fileHref: null), "full");
        Assert.DoesNotContain("\"href\":", plain);
    }

    /// <summary>Deferred item (at-scale SPA perf pass): past <see cref="Charts.MaxDetailedCodeMapFiles"/>, the
    /// text-equivalent table caps at the same significance-ordered set the treemap's rich tooltips use, with an
    /// honest "+N more" row rather than silently truncating (or ballooning the page). Built as a single
    /// hand-assembled <see cref="CodeMapVariant"/> (not <see cref="CodeMap.BuildVariants"/>'s four combinations)
    /// to keep this test's file count manageable while still exceeding the real cap.</summary>
    [Fact]
    public void RenderPage_AboveTheDetailCap_TableTruncatesWithAnHonestCountAndUpdatedLead()
    {
        var cap = Charts.MaxDetailedCodeMapFiles;
        var fileCount = cap + 7;
        var files = Enumerable.Range(1, fileCount).Select(i => ($"src/file-{i:00000}.cs", (long)i)).ToArray();
        var map = CodeMap.Build(files, NoMetrics);
        var variant = new CodeMapVariant("full", ExcludesSpecDev: false, ExcludesTests: false, map, map.Layout());

        var html = CodeMapTemplater.RenderPage(new[] { variant }, Nav());

        Assert.Contains($"The {cap:N0} most significant files in the treemap", html);
        Assert.Contains("+7 more files not shown in this table", html);
        Assert.Contains("still has its own colored, focusable rectangle in the treemap above", html);
        // The smallest file (never in the top-`cap` by size, the significance order when metrics are absent)
        // has no table row at all — the cap actually removed rows, not just appended a note.
        Assert.DoesNotContain("src/file-00001.cs<", html);
    }

    // ---- File table pagination (owner feedback, Story 7.12 review) ------------------------

    [Fact]
    public void RenderPage_FileTableCarriesAPageSizeAndAHiddenPagerControlForClientSidePagination()
    {
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());

        Assert.Contains($"<table class=\"codemap-table\" data-page-size=\"{Reflect_CodeMapTablePageSize()}\">", html);
        Assert.Contains("class=\"codemap-table-row\"", html);
        // Emitted hidden — progressive enhancement only reveals it once there's more than one page's worth.
        Assert.Contains("<div class=\"codemap-table-pager\" hidden>", html);
        Assert.Contains("codemap-table-pager-prev", html);
        Assert.Contains("codemap-table-pager-next", html);
        Assert.Contains("codemap-table-pager-status", html);
    }

    /// <summary>The page-size constant is private; reading it via reflection keeps this test honest about the
    /// ACTUAL emitted attribute value rather than hard-coding a duplicate literal that could silently drift.</summary>
    private static string Reflect_CodeMapTablePageSize()
    {
        var field = typeof(CodeMapTemplater).GetField("CodeMapTablePageSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return field.GetValue(null)!.ToString()!;
    }
}
