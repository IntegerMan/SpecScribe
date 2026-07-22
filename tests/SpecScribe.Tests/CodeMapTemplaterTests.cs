using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Page-level coverage for the code-map templater (Story 7.6, round 2): the standard shell, the
/// always-present legend, the JS-revealed (hidden) colorize dropdown + drill breadcrumb, the "git data unavailable"
/// notice when metrics are absent, the text-equivalent table (ordered by change frequency, guarded code-page links),
/// and the four precomputed exclude-filter panels behind the pure-CSS checkbox toggle. [Story 7.6]</summary>
public class CodeMapTemplaterTests
{
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

        // Legend is always visible (explains the baked-in default colors); the interactive dropdown + drill
        // breadcrumb are present but hidden (revealed by the enhancement script — no inert control with JS off).
        Assert.Contains("codemap-legend", html);
        Assert.Contains("class=\"codemap-controls\" hidden", html);
        Assert.Contains("class=\"codemap-dim-select\"", html);
        Assert.Contains("value=\"changes\" selected", html);
        Assert.Contains("value=\"avgchange\"", html);
        Assert.Contains("value=\"cochange\"", html);          // "Files changed together" colorize dimension
        Assert.Contains("value=\"churn\"", html);              // round 2: churn is a colorize option
        Assert.Contains(">Churn</option>", html);
        // Story 7.9: "File type" is a 7th option, unselected — the sequential default (change frequency) is
        // unchanged (AC #3), and its ramp legend ships visible while the discrete legend ships pre-rendered hidden.
        Assert.Contains("value=\"filetype\">File type</option>", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-ramp\">", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-discrete\" hidden>", html);

        // The text table gains a "Together" column carrying the per-file average co-changed file count, and an
        // always-present "Type" column (Story 7.9).
        Assert.Contains(">Together</th>", html);
        Assert.Contains(">Type</th>", html);
        Assert.Contains(">C#</td>", html);                    // src/A.cs classifies as C#
        Assert.Contains(">3.4</td>", html);                   // src/A.cs's average co-changed files

        // First/Last dates render via the portal's human-readable token, not raw ISO.
        Assert.Contains("Jun 1, 2026", html);
        Assert.DoesNotContain("2026-06-01", html);
        Assert.Contains("class=\"codemap-drill\" aria-label=\"Treemap zoom\" hidden", html);

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
        Assert.Contains("class=\"codemap-legend codemap-legend-discrete\">", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-ramp\" hidden>", html); // ramp legend pre-rendered but hidden

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
    public void RenderPage_EmitsFourPanelsAndTwoPureCssFilterCheckboxes()
    {
        var variants = VariantsWithoutMetrics(
            (".agents/skills/bmad-dev/workflow.md", 10L),
            ("tests/SpecScribe.Tests/GitMetricsTests.cs", 20L),
            ("src/SpecScribe/GitMetrics.cs", 30L));

        var html = CodeMapTemplater.RenderPage(variants, Nav());

        // The two checkboxes are unwrapped siblings of the four panels (the CSS toggle depends on this), each
        // with a real id the CSS/JS reference and an associated label (not nested — for/id association instead).
        Assert.Contains("<input type=\"checkbox\" id=\"cm-exclude-spec\" class=\"codemap-filter-checkbox\">", html);
        Assert.Contains("<label for=\"cm-exclude-spec\"", html);
        Assert.Contains("<input type=\"checkbox\" id=\"cm-exclude-tests\" class=\"codemap-filter-checkbox\">", html);
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
        Assert.Contains("spec-driven development directories excluded", html);
        Assert.Contains("tests excluded", html);
        Assert.Contains("spec-driven development directories and tests excluded", html);

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

    // ---- Code freshness sunburst section (Story 7.12) -------------------------------------

    [Fact]
    public void RenderPage_WithMetrics_RendersTheFreshnessSunburstSectionWithARealValueLegendAndATableCaption()
    {
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());

        Assert.Contains("Code Freshness", html);
        Assert.Contains("freshness-sunburst", html);
        Assert.Contains("freshness-legend", html);
        Assert.DoesNotContain("Less …", html);
        // The caption points at the existing Last-column table rather than duplicating it.
        Assert.Contains("<strong>Last</strong>", html);
        // The section lives INSIDE each filtered .codemap-view panel (not once ahead of all four), so it
        // re-filters along with the treemap/table when a checkbox is toggled.
        var fullPanelStart = html.IndexOf("data-view=\"full\"", StringComparison.Ordinal);
        var nextPanelStart = html.IndexOf("data-view=\"no-spec\"", StringComparison.Ordinal);
        var freshnessInFullPanel = html.IndexOf("Code Freshness", StringComparison.Ordinal);
        Assert.InRange(freshnessInFullPanel, fullPanelStart, nextPanelStart);
    }

    [Fact]
    public void RenderPage_EachFilterPanelGetsItsOwnFreshnessSunburstSoTheCheckboxesActuallyReFilterIt()
    {
        // Owner feedback: a single sunburst sourced only from the unfiltered tree looked "frozen" next to a
        // treemap/table that visibly changed when a checkbox was toggled. Each of the four precomputed panels
        // must carry its OWN filtered sunburst, exactly like the treemap already does.
        var variants = CodeMap.BuildVariants(
            new[] { ("tests/OnlyTests/FooTests.cs", 10L), ("src/A.cs", 20L) }, NoMetrics);

        var html = CodeMapTemplater.RenderPage(variants, Nav());

        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(html, "Code Freshness").Count);
        // The "no-tests" panel excludes the test file, so its sunburst has one fewer file wedge than "full"'s.
        var fullSection = html[html.IndexOf("data-view=\"full\"", StringComparison.Ordinal)..html.IndexOf("data-view=\"no-spec\"", StringComparison.Ordinal)];
        var noTestsSection = html[html.IndexOf("data-view=\"no-tests\"", StringComparison.Ordinal)..html.IndexOf("data-view=\"no-spec-no-tests\"", StringComparison.Ordinal)];
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(fullSection, "class=\"freshness-wedge level-").Count);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(noTestsSection, "class=\"freshness-wedge level-"));
    }

    [Fact]
    public void RenderPage_RendersASunburstTreemapViewTogglePerPanelWithUniqueRadioIdsAcrossAllFourPanels()
    {
        // Owner feedback: a "view as treemap" alternative to the sunburst (owner correction: "Tree" means the
        // size-by-area treemap shape, not a folder-list view). The pure-CSS radio pair must have variant-unique
        // ids/names (all four panels' markup coexists in the DOM) so toggling one panel's Sunburst/Treemap
        // radios can never affect another panel.
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());

        Assert.Contains("id=\"cf-sunburst-full\"", html);
        Assert.Contains("id=\"cf-treemap-full\"", html);
        Assert.Contains("id=\"cf-sunburst-no-spec\"", html);
        Assert.Contains("id=\"cf-treemap-no-spec\"", html);
        Assert.Contains(">Sunburst</label>", html);
        Assert.Contains(">Treemap</label>", html);
        Assert.Contains("class=\"freshness-treemap\"", html); // the treemap view itself renders (always in the DOM, CSS-hidden by default)
        // Every radio name is unique per panel — no two panels' toggles can cross-wire.
        var names = System.Text.RegularExpressions.Regex.Matches(html, "name=\"(cf-view-[^\"]+)\"")
            .Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.Equal(4, names.Count);
    }

    [Fact]
    public void RenderPage_WithoutMetrics_StillRendersTheFreshnessSunburstAllNeutral()
    {
        var html = CodeMapTemplater.RenderPage(VariantsWithoutMetrics(("src/A.cs", 10L)), Nav());

        Assert.Contains("Code Freshness", html);
        Assert.Contains("freshness-wedge level-none", html);
        Assert.Contains("freshness-legend-empty", html);
    }

    [Fact]
    public void RenderPage_FreshnessSunburstLinksAFileWedgeOnlyWhenTheResolverReturnsATarget()
    {
        // Regression guard: the freshness section's fileHref must be the SAME guarded resolver every other
        // Code Map surface (the treemap, the file table) already threads through — not silently dropped.
        var linked = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav(),
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null);
        Assert.Contains("<a href=\"code/src/A.cs.html\" aria-label=\"src/A.cs\"><path class=\"freshness-wedge", linked);

        var plain = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav(), fileHref: null);
        Assert.DoesNotContain("<a href=\"code/src/A.cs.html\">", plain);
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
