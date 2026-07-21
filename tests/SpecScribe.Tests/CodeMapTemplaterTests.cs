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
}
