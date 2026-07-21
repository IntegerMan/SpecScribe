using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Page-level coverage for the standalone risk-quadrant.html templater (Story 7.10, review pass): the
/// standard shell, the always-framed chart (live scatter or below-threshold empty state), and the paginated
/// elevated-risk grid (full ranked list always in the markup; the pager is a progressive-enhancement reveal).</summary>
public class RiskQuadrantTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasCodeMap: true);

    private static CodeMap MapAboveThreshold() => CodeMap.Build(
        new[]
        {
            ("src/BigHot.cs", 5000L), ("src/B.cs", 200L), ("src/C.cs", 180L),
            ("src/D.cs", 150L), ("src/E.cs", 120L), ("src/F.cs", 100L),
        },
        new Dictionary<string, CodeFileMetrics>
        {
            ["src/BigHot.cs"] = new CodeFileMetrics(50, 900, null, null),
            ["src/B.cs"] = new CodeFileMetrics(1, 10, null, null),
            ["src/C.cs"] = new CodeFileMetrics(2, 20, null, null),
            ["src/D.cs"] = new CodeFileMetrics(3, 30, null, null),
            ["src/E.cs"] = new CodeFileMetrics(4, 40, null, null),
            ["src/F.cs"] = new CodeFileMetrics(5, 50, null, null),
        });

    [Fact]
    public void RenderPage_AboveThreshold_HasShellChartAndTheElevatedGrid()
    {
        var html = RiskQuadrantTemplater.RenderPage(MapAboveThreshold(), Nav());

        Assert.Contains("<main id=\"main-content\"", html);
        Assert.Contains("class=\"breadcrumb\"", html);
        Assert.Contains("Refactor-Target Risk Quadrant", html);
        Assert.Contains("<svg class=\"risk-quadrant\"", html);
        Assert.Contains("chart-frame-why", html); // Story 10.2 chrome via Charts.Framed

        Assert.Contains("class=\"risk-grid\"", html);
        Assert.Contains("class=\"risk-grid-item\"", html);
        Assert.Contains("src/BigHot.cs", html);
        // Pager markup is present but hidden by default (progressive enhancement only).
        Assert.Contains("class=\"risk-pager\" hidden", html);
    }

    [Fact]
    public void RenderPage_BelowThreshold_ShowsTheChartEmptyStateButKeepsTheShellIntact()
    {
        var thin = CodeMap.Build(
            new[] { ("src/A.cs", 10L), ("src/B.cs", 20L) },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/A.cs"] = new CodeFileMetrics(5, 50, null, null),
                ["src/B.cs"] = new CodeFileMetrics(2, 20, null, null),
            });

        var html = RiskQuadrantTemplater.RenderPage(thin, Nav());

        Assert.Contains("Refactor-Target Risk Quadrant", html);
        Assert.Contains("chart-empty", html);
        Assert.DoesNotContain("<svg class=\"risk-quadrant\"", html);
        // No git-derived metrics on either file above their combined median → no elevated quadrant either.
        Assert.Contains("risk-quadrant-empty", html);
    }

    [Fact]
    public void RenderPage_NoElevatedFiles_SaysSoPlainlyRatherThanOmittingTheSectionSilently()
    {
        // Six files with IDENTICAL size and churn: every point sits exactly ON the median, so none is strictly
        // ABOVE both axes — the elevated quadrant is genuinely empty even though the chart itself renders live.
        var uniform = CodeMap.Build(
            new[] { ("src/A.cs", 100L), ("src/B.cs", 100L), ("src/C.cs", 100L), ("src/D.cs", 100L), ("src/E.cs", 100L), ("src/F.cs", 100L) },
            new Dictionary<string, CodeFileMetrics>
            {
                ["src/A.cs"] = new CodeFileMetrics(3, 30, null, null),
                ["src/B.cs"] = new CodeFileMetrics(3, 30, null, null),
                ["src/C.cs"] = new CodeFileMetrics(3, 30, null, null),
                ["src/D.cs"] = new CodeFileMetrics(3, 30, null, null),
                ["src/E.cs"] = new CodeFileMetrics(3, 30, null, null),
                ["src/F.cs"] = new CodeFileMetrics(3, 30, null, null),
            });

        var html = RiskQuadrantTemplater.RenderPage(uniform, Nav());

        Assert.Contains("<svg class=\"risk-quadrant\"", html); // the chart itself is live (6 files, at threshold)
        Assert.Contains("No files currently fall in the high-churn, high-size quadrant.", html);
        Assert.DoesNotContain("class=\"risk-grid\"", html);
    }

    [Fact]
    public void RenderPage_GridLinksAFileOnlyWhenTheResolverReturnsATarget()
    {
        var linked = RiskQuadrantTemplater.RenderPage(MapAboveThreshold(), Nav(),
            fileHref: p => p == "src/BigHot.cs" ? "code/src/BigHot.cs.html" : null);
        Assert.Contains("<a href=\"code/src/BigHot.cs.html\">src/BigHot.cs</a>", linked);

        var plain = RiskQuadrantTemplater.RenderPage(MapAboveThreshold(), Nav(), fileHref: null);
        Assert.DoesNotContain("code/src/BigHot.cs.html", plain);
        Assert.Contains("src/BigHot.cs", plain); // still listed, just not linked
    }

    [Fact]
    public void RenderPage_GridItemsCarryRankAndMetaText()
    {
        var html = RiskQuadrantTemplater.RenderPage(MapAboveThreshold(), Nav());

        Assert.Contains("class=\"risk-grid-rank\">1</span>", html);
        Assert.Contains("5,000 lines", html);
        Assert.Contains("50 changes", html);
        Assert.Contains("900 churn", html);
    }
}
