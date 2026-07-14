using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Page-level coverage for the code-map templater (Story 7.6): the standard shell, the always-present
/// legend, the JS-revealed (hidden) colorize controls + drill breadcrumb, the "git data unavailable" notice when
/// metrics are absent, and the text-equivalent table (ordered by change frequency, guarded code-page links).
/// [Story 7.6]</summary>
public class CodeMapTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasCodeMap: true);

    private static CodeMap MapWithMetrics() => CodeMap.Build(
        new[] { ("src/A.cs", 300L), ("src/B.cs", 50L) },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/A.cs"] = new CodeFileMetrics(8, 200, new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 10), AvgCoChanged: 3.4),
            ["src/B.cs"] = new CodeFileMetrics(2, 20, new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 20), AvgCoChanged: 1.0),
        });

    [Fact]
    public void RenderPage_WithMetrics_HasShellLegendHiddenControlsAndOrderedTable()
    {
        var map = MapWithMetrics();
        var html = CodeMapTemplater.RenderPage(map, map.Layout(), Nav());

        // Standard standalone-page shell.
        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Contains("class=\"breadcrumb\"", html);
        Assert.Contains("Code Map", html);

        // Legend is always visible (explains the baked-in default colors); the interactive controls + drill
        // breadcrumb are present but hidden (revealed by the enhancement script — no inert control with JS off).
        Assert.Contains("codemap-legend", html);
        Assert.Contains("id=\"codemap-controls\"", html);
        Assert.Contains("name=\"codemap-dim\"", html);
        Assert.Contains("value=\"changes\"", html);
        Assert.Contains("value=\"avgchange\"", html);
        Assert.Contains("value=\"cochange\"", html);          // the new "Files changed together" colorize dimension

        // The text table gains a "Together" column carrying the per-file average co-changed file count.
        Assert.Contains(">Together</th>", html);
        Assert.Contains(">3.4</td>", html);                   // src/A.cs's average co-changed files

        // First/Last dates render via the portal's human-readable token, not raw ISO.
        Assert.Contains("Jun 1, 2026", html);
        Assert.DoesNotContain("2026-06-01", html);
        Assert.Contains("class=\"codemap-controls\" id=\"codemap-controls\" aria-label=\"Colorize the treemap by\" hidden", html);
        Assert.Contains("class=\"codemap-drill\" aria-label=\"Treemap zoom\" hidden", html);

        // Metrics present → no "unavailable" notice.
        Assert.DoesNotContain("codemap-notice", html);

        // The text-equivalent table lists every file with its metrics, ordered by change frequency (A=8 before B=2).
        Assert.Contains("codemap-table", html);
        Assert.Contains("src/A.cs", html);
        Assert.Contains("src/B.cs", html);
        Assert.True(html.IndexOf("src/A.cs", StringComparison.Ordinal) < html.IndexOf("src/B.cs", StringComparison.Ordinal),
            "the busier file (more changes) is listed first");
    }

    [Fact]
    public void RenderPage_WithoutMetrics_ShowsNoticeAndOmitsControlsAndLegend()
    {
        var map = CodeMap.Build(new[] { ("src/A.cs", 10L) }, new Dictionary<string, CodeFileMetrics>());
        var html = CodeMapTemplater.RenderPage(map, map.Layout(), Nav());

        Assert.Contains("codemap-notice", html);            // graceful degradation notice (AC #2)
        Assert.DoesNotContain("codemap-controls", html);    // no colorize controls without git data
        Assert.DoesNotContain("codemap-legend", html);      // no ramp legend without git data
        // The text table still lists the file (sized-by-LOC is always meaningful).
        Assert.Contains("codemap-table", html);
        Assert.Contains("src/A.cs", html);
    }

    [Fact]
    public void RenderPage_TableLinksFilesOnlyWhenResolverReturnsATarget()
    {
        var map = CodeMap.Build(new[] { ("src/A.cs", 10L) }, new Dictionary<string, CodeFileMetrics>());

        var linked = CodeMapTemplater.RenderPage(map, map.Layout(), Nav(),
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null);
        Assert.Contains("<a href=\"code/src/A.cs.html\">src/A.cs</a>", linked);

        var plain = CodeMapTemplater.RenderPage(map, map.Layout(), Nav(), fileHref: null);
        Assert.DoesNotContain("code/src/A.cs.html", plain);
    }
}
