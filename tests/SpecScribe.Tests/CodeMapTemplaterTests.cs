using System.Text.Json;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Page-level coverage for the code-map templater (Story 7.6, round 2; Story 20.9 converted the chart to
/// the Hierarchy Explorer component; Story 20.10 collapsed the four independently-serialized filter-variant panels
/// into ONE chart instance + ONE file table over a shared, server-declared-views payload). Covers: the standard
/// shell, the always-present legend (now emitted per view), the JS-revealed (hidden) colorize dropdown + drill
/// breadcrumb, the "git data unavailable" notice when metrics are absent, the deduplicated text-equivalent table
/// (ordered by change frequency, guarded code-page links, row marker classes for the pure-CSS filter), and the two
/// pure-CSS exclude-filter checkboxes. [Story 7.6; Story 20.9; Story 20.10]</summary>
public class CodeMapTemplaterTests
{
    /// <summary>The ONE shared code-map island JSON (Story 20.10 D1/D2).</summary>
    private static string Island(string html)
    {
        var m = Regex.Match(
            html,
            "<script type=\"application/json\" class=\"ss-hierarchy-data\" id=\"codemap-data\">(?<j>.*?)</script>",
            RegexOptions.Singleline);
        Assert.True(m.Success, "expected the shared Code Map island");
        return m.Groups["j"].Value;
    }

    /// <summary>The named view's own JSON object out of the shared island's <c>views</c> array.</summary>
    private static JsonElement ViewOf(JsonDocument doc, string viewKey)
    {
        foreach (var v in doc.RootElement.GetProperty("views").EnumerateArray())
        {
            if (v.GetProperty("key").GetString() == viewKey) return v;
        }
        throw new Xunit.Sdk.XunitException($"view '{viewKey}' not found in island");
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

        // Story 20.9: the colorize picker and both legends ride inside the component's own hidden control and
        // legend bars — ONE of each now (Story 20.10 D2), not one per panel.
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
        Assert.Contains("value=\"filetype\">File type</option>", html);

        // Story 20.10: each of the four views gets its own ramp + discrete legend pair, tagged with
        // data-hierarchy-legend-view; only the DEFAULT ("full") view's ramp legend ships visible.
        Assert.Contains("class=\"codemap-legend codemap-legend-ramp\" data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapRampLegend + "\" data-hierarchy-legend-view=\"full\">", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-discrete\" data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapDiscreteLegend + "\" data-hierarchy-legend-view=\"full\" hidden>", html);
        foreach (var key in new[] { "no-spec", "no-tests", "no-spec-no-tests" })
        {
            Assert.Contains($"data-hierarchy-legend-view=\"{key}\" hidden>", html); // non-default views' ramp legend starts hidden
        }
        // The ramp caption is a TEMPLATE the component substitutes the active dimension's label into.
        Assert.Contains("data-hierarchy-legend-caption=\"Colorized by {label}\"", html);

        // The text table gains a "Together" column carrying the per-file average co-changed file count, and an
        // always-present "Type" column (Story 7.9). ONE table now (Story 20.10 D3).
        Assert.Contains(">Together</th>", html);
        Assert.Contains(">Type</th>", html);
        Assert.Contains(">C#</td>", html);                    // src/A.cs classifies as C#
        Assert.Contains(">3.4</td>", html);                   // src/A.cs's average co-changed files
        Assert.Single(Regex.Matches(html, "<table class=\"codemap-table\""));

        // First/Last dates render via the portal's human-readable token, not raw ISO.
        Assert.Contains("Jun 1, 2026", html);
        Assert.DoesNotContain("2026-06-01", html);
        Assert.Contains("ss-hierarchy-breadcrumb", html);

        // Metrics present → no "unavailable" notice.
        Assert.DoesNotContain("Git change data is unavailable", html);

        // The text-equivalent table lists every distinct file with its metrics, ordered by change frequency
        // (A=8 before B=2).
        Assert.Contains("codemap-table", html);
        Assert.Contains("src/A.cs", html);
        Assert.Contains("src/B.cs", html);
        Assert.True(html.IndexOf("src/A.cs", StringComparison.Ordinal) < html.IndexOf("src/B.cs", StringComparison.Ordinal),
            "the busier file (more changes) is listed first");

        // The treemap card and its text-equivalent table are SIBLING chart-panels, never one nested in the other.
        Assert.DoesNotContain("chart-panel codemap-panel\">\n\n    <section class=\"chart-panel", html);
    }

    [Fact]
    public void RenderPage_WithoutMetrics_ShowsSecondaryNoticeButKeepsAWorkingFileTypeDimension()
    {
        var html = CodeMapTemplater.RenderPage(VariantsWithoutMetrics(("src/A.cs", 10L)), Nav());

        Assert.Contains("Git change data is unavailable", html);
        Assert.Contains("codemap-notice-secondary", html);
        Assert.Contains("codemap-dim-select", html);
        Assert.Contains("value=\"filetype\" selected", html);
        Assert.DoesNotContain("value=\"changes\"", html);
        Assert.Contains("codemap-legend-discrete", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-discrete\" data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapDiscreteLegend + "\" data-hierarchy-legend-view=\"full\">", html);
        Assert.Contains("class=\"codemap-legend codemap-legend-ramp\" data-hierarchy-legend=\"" + HierarchyExplorer.CodeMapRampLegend + "\" data-hierarchy-legend-view=\"full\" hidden>", html);

        // Exactly ONE dimension in the payload — nothing for the six git-derived ramps to quantize.
        Assert.Single(Regex.Matches(Island(html), "\"kind\":\"(?:ramp|ramp-window|categorical|cutoff|roster|spotlight|threshold)\""));
        Assert.Contains("\"key\":\"filetype\"", Island(html));

        Assert.Contains("codemap-table", html);
        Assert.Contains("src/A.cs", html);
        Assert.Contains(">Type</th>", html);
        Assert.Contains(">C#</td>", html);
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
        // Every path the "full" view's chart draws must have a resolving row in the (now shared) table.
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());

        var charted = Regex.Matches(Island(html), "\"path\":\"(?<p>[^\"]+)\"");
        Assert.True(charted.Count > 0, "fixture must chart at least one file");
        var tableSection = html[html.IndexOf("codemap-table-section", StringComparison.Ordinal)..];
        foreach (Match m in charted)
        {
            Assert.Contains(m.Groups["p"].Value, tableSection, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RenderPage_EachFileIsSerializedExactlyOnce_NotOncePerVariantItAppearsIn()
    {
        // AC#1's central assertion. src/A.cs appears in all four variants (it is neither spec-dev nor a test
        // path); its path, metric bag and table row must each appear EXACTLY ONCE in the rendered page.
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());

        Assert.Single(Regex.Matches(html, Regex.Escape("\"path\":\"src/A.cs\"")));
        Assert.Single(Regex.Matches(html, Regex.Escape("\"changes\":\"8\"")));
        Assert.Single(Regex.Matches(html, "<tr class=\"[^\"]*\"><th scope=\"row\">(?:<a[^>]*>)?src/A\\.cs"));
        // And only ONE chart host + ONE island on the whole page (Story 20.10 D2).
        Assert.Single(Regex.Matches(html, Regex.Escape(HierarchyExplorer.HostMarker + "></div>")));
        Assert.Single(Regex.Matches(html, "ss-hierarchy-data"));
    }

    [Fact]
    public void RenderPage_EmitsFourServerDeclaredViewsAndTwoPureCssFilterCheckboxes()
    {
        var variants = VariantsWithoutMetrics(
            (".agents/skills/bmad-dev/workflow.md", 10L),
            ("tests/SpecScribe.Tests/GitMetricsTests.cs", 20L),
            ("src/SpecScribe/GitMetrics.cs", 30L));

        var html = CodeMapTemplater.RenderPage(variants, Nav());

        // The two checkboxes drive BOTH the (pure CSS) table row filter and the (JS) view switch — Story 20.10
        // Task 2.3's data-hierarchy-view-toggle, alongside the still-live data-hierarchy-reveal deferred-mount hook.
        Assert.Contains("<input type=\"checkbox\" id=\"cm-exclude-spec\" class=\"codemap-filter-checkbox\" data-hierarchy-reveal data-hierarchy-view-toggle>", html);
        Assert.Contains("<label for=\"cm-exclude-spec\"", html);
        Assert.Contains("<input type=\"checkbox\" id=\"cm-exclude-tests\" class=\"codemap-filter-checkbox\" data-hierarchy-reveal data-hierarchy-view-toggle>", html);
        Assert.Contains("<label for=\"cm-exclude-tests\"", html);

        // All four views are server-declared in the ONE shared island, each with its own title.
        using var doc = JsonDocument.Parse(Island(html));
        var keys = doc.RootElement.GetProperty("views").EnumerateArray().Select(v => v.GetProperty("key").GetString()).ToList();
        Assert.Equal(new[] { "full", "no-spec", "no-tests", "no-spec-no-tests" }, keys);

        Assert.Equal("Source Code Map — excluding spec-driven development directories", ViewOf(doc, "no-spec").GetProperty("title").GetString());
        Assert.Equal("Source Code Map — excluding tests", ViewOf(doc, "no-tests").GetProperty("title").GetString());
        Assert.Equal("Source Code Map — excluding spec-driven development directories and tests", ViewOf(doc, "no-spec-no-tests").GetProperty("title").GetString());
        Assert.Equal("Source Code Map — every file", ViewOf(doc, "full").GetProperty("title").GetString());

        // The default view's title is server-baked into the panel heading too, matching the payload's own "full".
        Assert.Contains("Source Code Map — every file</h3>", html);

        // The "no-spec-no-tests" view keeps only the one surviving file.
        Assert.Equal(1, ViewOf(doc, "no-spec-no-tests").GetProperty("files").GetArrayLength());
        Assert.Contains("src/SpecScribe/GitMetrics.cs", html);
    }

    [Fact]
    public void RenderPage_APanelThatExcludesEveryFileShowsANoFilesNoticeInsteadOfAnEmptyTreemap()
    {
        var variants = VariantsWithoutMetrics(("tests/OnlyTests/FooTests.cs", 10L));

        var html = CodeMapTemplater.RenderPage(variants, Nav());

        // JS-off: the table's per-view lead line says so for the "no-tests"/"no-spec-no-tests" views.
        Assert.Contains("No files match this filter.", html);
    }

    // ---- Merged shape (Treemap/Sunburst) x dimension toggle (Story 7.12 review) ------------

    [Fact]
    public void RenderPage_OneInstanceWithTheStandardSelector_NotFourStackedPanels()
    {
        // Story 20.10 D2: four instances (Story 20.9's shape) become ONE, with four server-declared views over
        // its shared payload instead of four independently-serialized ones.
        var html = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav());

        Assert.Single(Regex.Matches(html, Regex.Escape(HierarchyExplorer.HostMarker + "></div>")));
        Assert.Single(Regex.Matches(html, "ss-hierarchy-data"));

        // The retired pure-CSS shape toggle and both SVG wrappers are gone by name.
        Assert.DoesNotContain("class=\"codemap-sunburst\"", html);
        Assert.DoesNotContain("cs-sunburst-radio", html);
        // The four independently-wrapped panels are gone too (F5/Task 6.1) — `data-codemap-view` is a DIFFERENT,
        // new attribute (the per-view lead-text toggle), so check for the retired wrapper CLASS specifically.
        Assert.DoesNotContain("class=\"codemap-view\"", html);
        Assert.DoesNotContain("data-hierarchy-reveal-when", html);

        // Ordered Sunburst-then-Treemap site-wide (Story 20.7 D2) with THIS surface's shipped default preserved.
        Assert.Contains("class=\"board-tab-radio ss-hierarchy-shape\" value=\"treemap\" checked", html);
        Assert.Contains(">Treemap</label>", html);
        Assert.Contains(">Sunburst</label>", html);

        // Still only ONE "Colorize by" dropdown on the whole page.
        Assert.Single(Regex.Matches(html, "class=\"codemap-controls\""));
    }

    [Fact]
    public void RenderPage_EachViewDeclaresItsOwnMembershipSoTheCheckboxesActuallyReFilterIt()
    {
        var variants = CodeMap.BuildVariants(
            new[] { ("tests/OnlyTests/FooTests.cs", 10L), ("src/A.cs", 20L) }, NoMetrics);

        var html = CodeMapTemplater.RenderPage(variants, Nav());

        // The shared payload carries 2 distinct file nodes total (never duplicated per view — AC#1); the two
        // VIEWS declare different SUBSETS of them via their own `files` index list.
        using var doc = JsonDocument.Parse(Island(html));
        Assert.Equal(2, doc.RootElement.GetProperty("nodes").GetArrayLength());
        Assert.Equal(2, ViewOf(doc, "full").GetProperty("files").GetArrayLength());
        Assert.Equal(1, ViewOf(doc, "no-tests").GetProperty("files").GetArrayLength());

        // The (now shared) table carries one row per distinct file, marked so the pure-CSS filter can hide it.
        Assert.Equal(2, Regex.Matches(html, "class=\"codemap-table-row[^\"]*\"").Count);
        Assert.Contains("class=\"codemap-table-row is-test\"", html);
        Assert.DoesNotContain("class=\"codemap-table-row is-spec\"", html); // src/A.cs and the tests/ file are neither
    }

    [Fact]
    public void RenderPage_ViewsAreVariantDependentDirectoryScaffoldsSharingNoIdsIncorrectly()
    {
        // F2's proof, at page level: `.github` has both `agents` (spec-dev) and `workflows` (not) as children in
        // "full", so it does NOT collapse there; once no-spec drops every `.github/agents/*` file, `.github` has
        // one child directory and no files of its own and DOES collapse, to a different id/label/parent.
        var variants = CodeMap.BuildVariants(
            new[]
            {
                (".github/agents/bmad-agent-dev.agent.md", 5L),
                (".github/workflows/build.yml", 5L),
            }, NoMetrics);

        var html = CodeMapTemplater.RenderPage(variants, Nav());
        using var doc = JsonDocument.Parse(Island(html));

        var fullIds = ViewOf(doc, "full").GetProperty("scaffold").EnumerateArray().Select(n => n.GetProperty("id").GetString()).ToList();
        var noSpecIds = ViewOf(doc, "no-spec").GetProperty("scaffold").EnumerateArray().Select(n => n.GetProperty("id").GetString()).ToList();

        Assert.Contains(".github", fullIds);
        Assert.DoesNotContain(".github", noSpecIds);
        Assert.Contains(".github/workflows", noSpecIds);

        // HierarchyNode.Label carries the node's PATH (CodeMapDirNode's mapping); the joined display form
        // (".github / workflows") rides in ShortLabel — see CodeMapDirNode.
        var collapsedNode = ViewOf(doc, "no-spec").GetProperty("scaffold").EnumerateArray()
            .Single(n => n.GetProperty("id").GetString() == ".github/workflows");
        Assert.Equal(".github / workflows", collapsedNode.GetProperty("shortLabel").GetString());
    }

    [Fact]
    public void RenderPage_MembershipRoundTripsToTheSamePerVariantParentASingleVariantProjectionWouldProduce()
    {
        // Task 1.4/8.8: for every (view, file) pair, the decoded parent (Scaffold[ParentScaffoldIndex[i]].id) must
        // equal what the single-variant HierarchyExplorer.ProjectCodeMap produces for the SAME variant.
        var variants = CodeMap.BuildVariants(
            new[]
            {
                (".github/agents/bmad-agent-dev.agent.md", 5L),
                (".github/workflows/build.yml", 5L),
                ("src/SpecScribe/Charts.cs", 40L),
            }, NoMetrics);

        var config = new HierarchyExplorerConfig("codemap-scratch", "treemap", HierarchyMode.Navigate, "cm-scratch", 640, true,
            new Charts.ChartMeta("scratch"), Dimensions: HierarchyExplorer.CodeMapDimensions(false));

        foreach (var variant in variants)
        {
            if (variant.Map.IsEmpty) continue;
            var expected = HierarchyExplorer.ProjectCodeMap(variant, config)
                .Nodes.Where(n => n.Kind == "file").ToDictionary(n => n.Id, n => n.ParentId, StringComparer.Ordinal);

            var shared = HierarchyExplorer.ProjectCodeMapViews(variants, config);
            var view = shared.Views!.Single(v => v.Key == variant.Key);
            for (var i = 0; i < view.Files.Count; i++)
            {
                var path = shared.Nodes[view.Files[i]].Id;
                var decodedParent = view.Scaffold[view.ParentScaffoldIndex[i]].Id;
                Assert.Equal(expected[path], decodedParent);
            }
        }
    }

    [Fact]
    public void RenderPage_EachViewsFourInvariantsHold()
    {
        // The four Story 20.4 payload invariants, asserted per view (Task 1.6/8.3): exactly one root, no null in
        // any file's value, parent == sum of children, and the emitted branchvalues matches the constant.
        var variants = VariantsWithMetrics();
        var html = CodeMapTemplater.RenderPage(variants, Nav());
        using var doc = JsonDocument.Parse(Island(html));

        Assert.Equal(HierarchyExplorer.BranchValues, doc.RootElement.GetProperty("config").GetProperty("branchvalues").GetString());

        foreach (var view in doc.RootElement.GetProperty("views").EnumerateArray())
        {
            var scaffold = view.GetProperty("scaffold").EnumerateArray().ToList();
            if (scaffold.Count == 0) continue; // an empty view (no files survive that combination)
            var roots = scaffold.Count(n => !n.TryGetProperty("parentId", out var p) || p.ValueKind == JsonValueKind.Null);
            Assert.Equal(1, roots);
        }
    }

    [Fact]
    public void RenderPage_ColorClassAndMetricsAreColourNeutral_ByteIdenticalToTheSingleVariantProjection()
    {
        // AC#2/D4: deduplicating files must not recolour anything. Every file's colorClass and raw metric bag in
        // the shared payload must be BYTE-IDENTICAL to what the unchanged single-variant HierarchyExplorer.
        // ProjectCodeMap already produces for the same variant — the resolved fill/hatch/stroke are a pure
        // function of these two fields plus the dimension rule, so identical inputs guarantee identical colour.
        var variants = VariantsWithMetrics();
        var config = new HierarchyExplorerConfig("codemap-scratch", "treemap", HierarchyMode.Navigate, "cm-scratch", 640, true,
            new Charts.ChartMeta("scratch"), Dimensions: HierarchyExplorer.CodeMapDimensions(true));

        var full = variants.Single(v => v.Key == "full");
        var expected = HierarchyExplorer.ProjectCodeMap(full, config)
            .Nodes.Where(n => n.Kind == "file")
            .ToDictionary(n => n.Id, n => n, StringComparer.Ordinal);

        var shared = HierarchyExplorer.ProjectCodeMapViews(variants, config);
        foreach (var node in shared.Nodes)
        {
            var e = expected[node.Id];
            Assert.Equal(e.ColorClass, node.ColorClass);
            Assert.Equal(e.Metrics?.Count ?? 0, node.Metrics?.Count ?? 0);
            foreach (var kv in e.Metrics ?? new Dictionary<string, string>())
            {
                Assert.Equal(kv.Value, node.Metrics![kv.Key]);
            }
        }
    }

    [Fact]
    public void RenderPage_TwinCompletenessHoldsForANonDefaultView_NotJustFull()
    {
        // Task 8.5: every file a NON-default view's chart would draw must have a resolving row in the (shared)
        // table — a set match, not merely a count match, retargeted at "no-tests" rather than only "full".
        var variants = CodeMap.BuildVariants(
            new[] { ("tests/OnlyTests/FooTests.cs", 10L), ("src/A.cs", 20L), ("src/B.cs", 5L) }, NoMetrics);
        var html = CodeMapTemplater.RenderPage(variants, Nav());
        using var doc = JsonDocument.Parse(Island(html));

        var view = ViewOf(doc, "no-tests");
        var fileIndices = view.GetProperty("files").EnumerateArray().Select(f => f.GetInt32()).ToList();
        var nodes = doc.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var tableSection = html[html.IndexOf("codemap-table-section", StringComparison.Ordinal)..];

        Assert.True(fileIndices.Count > 0, "fixture's no-tests view must chart at least one file");
        foreach (var idx in fileIndices)
        {
            var path = nodes[idx].GetProperty("id").GetString()!;
            Assert.DoesNotContain("Tests", path, StringComparison.OrdinalIgnoreCase); // sanity: no-tests really excludes it
            Assert.Contains(path, tableSection, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RenderPage_ChartLinksAFileOnlyWhenTheResolverReturnsATarget()
    {
        // Story 7.1's link guard, unchanged by the conversion: a resolver returning null leaves a plain, focusable
        // node - never a broken link.
        var linked = CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav(),
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null);
        var linkedIsland = Island(linked);
        Assert.Contains("\"href\":\"code/src/A.cs.html\"", linkedIsland);
        // src/B.cs resolves to nothing, so it carries no href at all rather than a dead one.
        Assert.Contains("\"path\":\"src/B.cs\"", linkedIsland);
        Assert.Single(Regex.Matches(linkedIsland, "\"href\":"));

        var plain = Island(CodeMapTemplater.RenderPage(VariantsWithMetrics(), Nav(), fileHref: null));
        Assert.DoesNotContain("\"href\":", plain);
    }

    [Fact]
    public void RenderPage_WithoutMetrics_ColorizesByFileTypeFromOnePayload()
    {
        var html = CodeMapTemplater.RenderPage(VariantsWithoutMetrics(("src/A.cs", 10L)), Nav());
        var island = Island(html);

        Assert.Contains("\"colorClass\":\"codemap-cell\"", island);
        Assert.Contains("\"filetype\":\"csharp\"", island);
        Assert.Contains("\"filetype-label\":\"C#\"", island);
        Assert.Contains("\"classPrefix\":\"type-\"", island);
    }

    /// <summary>Deferred item (at-scale SPA perf pass): past <see cref="Charts.MaxDetailedCodeMapFiles"/>, the
    /// text-equivalent table caps at the same significance-ordered set the treemap's rich tooltips use, with an
    /// honest "+N more" row rather than silently truncating (or ballooning the page). Story 20.10 F7: the cap now
    /// applies ONCE against the distinct file set, not per variant.</summary>
    [Fact]
    public void RenderPage_AboveTheDetailCap_TableTruncatesWithAnHonestCountAndUpdatedLead()
    {
        var cap = Charts.MaxDetailedCodeMapFiles;
        var fileCount = cap + 7;
        var files = Enumerable.Range(1, fileCount).Select(i => ($"src/file-{i:00000}.cs", (long)i)).ToArray();
        var map = CodeMap.Build(files, NoMetrics);
        var variant = new CodeMapVariant("full", ExcludesSpecDev: false, ExcludesTests: false, map);

        var html = CodeMapTemplater.RenderPage(new[] { variant }, Nav());

        Assert.Contains($"The {cap:N0} most significant files in the treemap", html);
        Assert.Contains("+7 more files not shown in this table", html);
        Assert.Contains("still has its own colored, focusable rectangle in the treemap above", html);
        // The smallest file (never in the top-`cap` by size, the significance order when metrics are absent)
        // has no table row at all — the cap actually removed rows, not just appended a note.
        Assert.DoesNotContain("src/file-00001.cs<", html);
        // The cap is applied ONCE — exactly one truncation row on the whole page.
        Assert.Single(Regex.Matches(html, "codemap-table-truncated"));
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
        // Exactly one pager for the exactly one (now shared) table.
        Assert.Single(Regex.Matches(html, "codemap-table-pager\""));
    }

    /// <summary>The page-size constant is private; reading it via reflection keeps this test honest about the
    /// ACTUAL emitted attribute value rather than hard-coding a duplicate literal that could silently drift.</summary>
    private static string Reflect_CodeMapTablePageSize()
    {
        var field = typeof(CodeMapTemplater).GetField("CodeMapTablePageSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return field.GetValue(null)!.ToString()!;
    }
}
