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
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(VariantsWithMetrics(), Nav()));

        using var island = JsonDocument.Parse(Island(html));
        Assert.False(island.RootElement.GetProperty("config").TryGetProperty("treemapMaxDepth", out _));

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
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(VariantsWithoutMetrics(("src/A.cs", 10L)), Nav()));

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

        var linked = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav(),
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null));
        Assert.Contains("<a href=\"code/src/A.cs.html\">src/A.cs</a>", linked);

        var plain = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav(), fileHref: null));
        Assert.DoesNotContain("code/src/A.cs.html", plain);
    }

    [Fact]
    public void RenderPage_FileTableIsASetMatchAgainstTheChartPayload_NotJustACountMatch()
    {
        // Every path the "full" view's chart draws must have a resolving row in the (now shared) table.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(VariantsWithMetrics(), Nav()));

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
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(VariantsWithMetrics(), Nav()));

        Assert.Single(Regex.Matches(html, Regex.Escape("\"path\":\"src/A.cs\"")));
        Assert.Single(Regex.Matches(html, Regex.Escape("\"changes\":\"8\"")));
        Assert.Single(Regex.Matches(html, "<tr class=\"[^\"]*\"><th scope=\"row\">(?:<a[^>]*>)?src/A\\.cs"));
        // And only ONE chart host + ONE island on the whole page (Story 20.10 D2).
        Assert.Single(Regex.Matches(html, Regex.Escape(HierarchyExplorer.HostMarker + "></div>")));
        Assert.Single(Regex.Matches(html, "ss-hierarchy-data"));
    }

    [Fact]
    public void RenderPage_EmitsEightServerDeclaredViewsAndThreePureCssFilterCheckboxes()
    {
        var variants = VariantsWithoutMetrics(
            (".agents/skills/bmad-dev/workflow.md", 10L),
            ("tests/SpecScribe.Tests/GitMetricsTests.cs", 20L),
            ("src/SpecScribe/GitMetrics.cs", 30L));

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav()));

        // The two checkboxes drive BOTH the (pure CSS) table row filter and the (JS) view switch — Story 20.10
        // Task 2.3's data-hierarchy-view-toggle, alongside the still-live data-hierarchy-reveal deferred-mount hook.
        Assert.Contains("<input type=\"checkbox\" id=\"cm-exclude-spec\" class=\"codemap-filter-checkbox\" data-hierarchy-reveal data-hierarchy-view-toggle>", html);
        Assert.Contains("<label for=\"cm-exclude-spec\"", html);
        Assert.Contains("<input type=\"checkbox\" id=\"cm-exclude-tests\" class=\"codemap-filter-checkbox\" data-hierarchy-reveal data-hierarchy-view-toggle>", html);
        Assert.Contains("<label for=\"cm-exclude-tests\"", html);
        Assert.Contains("<input type=\"checkbox\" id=\"cm-exclude-agent\" class=\"codemap-filter-checkbox\" data-hierarchy-reveal data-hierarchy-view-toggle>", html);
        Assert.Contains("<label for=\"cm-exclude-agent\"", html);

        // All eight views are server-declared in the ONE shared island, each with its own title.
        using var doc = JsonDocument.Parse(Island(html));
        var keys = doc.RootElement.GetProperty("views").EnumerateArray().Select(v => v.GetProperty("key").GetString()).ToList();
        Assert.Equal(new[] { "full", "no-spec", "no-tests", "no-agent", "no-spec-no-tests", "no-spec-no-agent", "no-tests-no-agent", "no-spec-no-tests-no-agent" }, keys);

        Assert.Equal("Source Code Map — excluding spec-driven development directories", ViewOf(doc, "no-spec").GetProperty("title").GetString());
        Assert.Equal("Source Code Map — excluding tests", ViewOf(doc, "no-tests").GetProperty("title").GetString());
        Assert.Equal("Source Code Map — excluding spec-driven development directories and tests", ViewOf(doc, "no-spec-no-tests").GetProperty("title").GetString());
        Assert.Equal("Source Code Map — excluding agent and tooling directories", ViewOf(doc, "no-agent").GetProperty("title").GetString());
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

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav()));

        // JS-off: the table's per-view lead line says so for the "no-tests"/"no-spec-no-tests" views.
        Assert.Contains("No files match this filter.", html);
    }

    // ---- Merged shape (Treemap/Sunburst) x dimension toggle (Story 7.12 review) ------------

    [Fact]
    public void RenderPage_OneInstanceWithTheStandardSelector_NotFourStackedPanels()
    {
        // Story 20.10 D2: four instances (Story 20.9's shape) become ONE, with four server-declared views over
        // its shared payload instead of four independently-serialized ones.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(VariantsWithMetrics(), Nav()));

        Assert.Single(Regex.Matches(html, Regex.Escape(HierarchyExplorer.HostMarker + "></div>")));
        Assert.Single(Regex.Matches(html, "ss-hierarchy-data"));

        // The retired pure-CSS shape toggle and both SVG wrappers are gone by name.
        Assert.DoesNotContain("class=\"codemap-sunburst\"", html);
        Assert.DoesNotContain("cs-sunburst-radio", html);
        // The four independently-wrapped panels are gone too (F5/Task 6.1) — `data-codemap-view` is a DIFFERENT,
        // new attribute (the per-view lead-text toggle), so check for the retired wrapper CLASS specifically.
        Assert.DoesNotContain("class=\"codemap-view\"", html);
        Assert.DoesNotContain("data-hierarchy-reveal-when", html);
        // Story 20.9's retired bespoke drill breadcrumb, restored as a guard. [Review][Patch] Story 20.10 dropped
        // `Assert.DoesNotContain("codemap-drill", html)` along with the panel-structure rewrite; unlike its
        // `codemap-shape` sibling it had no reason to go — the component's own breadcrumb is `ss-hierarchy-drill`,
        // so nothing legitimate emits `codemap-drill` and a regression that reintroduced it would have passed.
        //
        // The `codemap-shape` guard genuinely COULD NOT survive and is recorded as a real coverage loss rather than
        // reinstated: with `DomId: "codemap"` the shared component now legitimately emits `name="codemap-shape"` and
        // `id="codemap-shape-treemap"` for its own shape radios, so the string no longer distinguishes the retired
        // pure-CSS toggle from the live control. The `codemap-sunburst`/`cs-sunburst-radio` guards above cover the
        // retired markup that still has a unique name.
        Assert.DoesNotContain("codemap-drill", html);

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

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav()));

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

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav()));
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
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav()));
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
    public void RenderPage_EachViewRollsUpToTheSameParentValuesASingleVariantServerRenderWouldProduce()
    {
        // AC#2's THIRD clause — "the same rolled-up values a from-scratch server render of that variant would have
        // produced" — and the one clause the story shipped with no coverage at all. [Review][Patch]
        //
        // `ProjectCodeMapViews` deliberately does NOT roll up: a shared payload cannot carry four sets of directory
        // values, so every scaffold directory ships `value: 0` and the roll-up is the client's job (ADR 0012
        // Ratified decision #8, as amended by this review). That makes the invariant a property of
        // scaffold + membership + the roll-up RULE, not of the emitted bytes — so it is asserted by reconstructing
        // each view's node list exactly as the client does and running it through the PRODUCTION rule
        // (`HierarchyExplorer.RollUp`), never a mirrored copy of it (anti-patterns 1 and 2).
        //
        // `RenderPage_EachViewsFourInvariantsHold` covers the other two clauses (one root, branchvalues); this one
        // covers rolled-up values and no-null-values.
        var variants = VariantsWithMetrics();
        var config = new HierarchyExplorerConfig("codemap-rollup", "treemap", HierarchyMode.Navigate, "cm-rollup", 640, true,
            new Charts.ChartMeta("rollup"), Dimensions: HierarchyExplorer.CodeMapDimensions(true));

        var shared = HierarchyExplorer.ProjectCodeMapViews(variants, config);
        Assert.NotNull(shared.Views);

        var asserted = 0;
        foreach (var view in shared.Views!)
        {
            if (view.Scaffold.Count == 0) continue; // an empty view (no files survive that combination)

            // Reparent the shared file nodes under THIS view's scaffold, in the client's own order:
            // all scaffold directories (parent-before-child) then the view's files.
            var reconstructed = view.Scaffold.ToList();
            for (var i = 0; i < view.Files.Count; i++)
            {
                var parent = view.Scaffold[view.ParentScaffoldIndex[i]];
                reconstructed.Add(shared.Nodes[view.Files[i]] with { ParentId = parent.Id });
            }

            var rolled = HierarchyExplorer.RollUp(reconstructed);
            var byId = rolled.ToDictionary(n => n.Id, n => n, StringComparer.Ordinal);

            // The "no null in values" invariant is structural here — `HierarchyNode.Value` is a non-nullable `int`,
            // so the payload cannot carry a null; what IS worth pinning is that no roll-up produced a negative or
            // absent total, which is how a broken reparent would show up.
            Assert.All(rolled, n => Assert.True(n.Value >= 0, $"view '{view.Key}' node '{n.Id}' rolled up to {n.Value}"));

            // parent == Σ children, over the rolled result.
            foreach (var group in rolled.Where(n => n.ParentId is not null).GroupBy(n => n.ParentId!))
            {
                Assert.True(byId.ContainsKey(group.Key), $"view '{view.Key}' has a child pointing at absent parent '{group.Key}'");
                Assert.Equal(group.Sum(c => c.Value), byId[group.Key].Value);
            }

            // And the values match what a from-scratch single-variant server render produces for the same variant —
            // the literal words of AC#2. Compared on DIRECTORIES and the root, which are the only nodes a roll-up
            // changes; files keep their own line counts and are already covered byte-identically by the colour
            // -neutrality test below.
            var variant = variants.Single(v => v.Key == view.Key);
            var oracle = HierarchyExplorer.ProjectCodeMap(variant, config).Nodes
                .Where(n => n.Kind != "file")
                .ToDictionary(n => n.Id, n => n.Value, StringComparer.Ordinal);
            Assert.NotEmpty(oracle);
            foreach (var pair in oracle)
            {
                Assert.True(byId.ContainsKey(pair.Key), $"view '{view.Key}' is missing directory '{pair.Key}' the single-variant projection produced");
                Assert.Equal(pair.Value, byId[pair.Key].Value);
            }
            asserted++;
        }

        Assert.True(asserted >= 2, $"expected at least two non-empty views to assert against, got {asserted}");
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
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav()));
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
        var linked = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(VariantsWithMetrics(), Nav(),
            fileHref: p => p == "src/A.cs" ? "code/src/A.cs.html" : null));
        var linkedIsland = Island(linked);
        Assert.Contains("\"href\":\"code/src/A.cs.html\"", linkedIsland);
        // src/B.cs resolves to nothing, so it carries no href at all rather than a dead one.
        Assert.Contains("\"path\":\"src/B.cs\"", linkedIsland);
        Assert.Single(Regex.Matches(linkedIsland, "\"href\":"));

        var plain = Island(JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(VariantsWithMetrics(), Nav(), fileHref: null)));
        Assert.DoesNotContain("\"href\":", plain);
    }

    [Fact]
    public void RenderPage_WithoutMetrics_ColorizesByFileTypeFromOnePayload()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(VariantsWithoutMetrics(("src/A.cs", 10L)), Nav()));
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

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(new[] { variant }, Nav()));

        Assert.Contains($"The {cap:N0} most significant files in the treemap", html);
        Assert.Contains("+7 more files not shown in this listing", html);
        Assert.Contains("still has its own colored, focusable rectangle in the treemap above", html);
        // The smallest file (never in the top-`cap` by size, the significance order when metrics are absent)
        // has no row at all — the cap actually removed rows, not just appended a note.
        Assert.DoesNotContain("src/file-00001.cs<", html);
        // The cap is applied ONCE — exactly one truncation notice on the whole page.
        Assert.Single(Regex.Matches(html, "codemap-table-truncated"));
    }

    [Fact]
    public void RenderPage_AboveTheDetailCap_ANonDefaultViewReportsItsOWNOmissionsNotTheDistinctSets()
    {
        // [Review][Patch] The multi-variant-above-cap fixture Task 8.7 admitted it did not write — and the case the
        // review found broken. Rows are capped ONCE against the DISTINCT set (F7's better rule), so a view SMALLER
        // than the cap can still lose rows: its members simply rank below the global top-`cap`. The old lead
        // arithmetic was `variant.Map.FileCount - cap`, which reports ZERO omissions for exactly that view and
        // printed "Every file in the treemap" over a table missing rows — breaking the ADR 0013 §2 twin-completeness
        // claim AC#3 makes for EVERY variant, silently, on any repo past 4,000 files.
        //
        // Fixture: `cap + 40` distinct files. Significance order without metrics is by SIZE, so the smallest files
        // are the ones cut. The 40 smallest are all tests, so the `no-tests` view (cap files, i.e. NOT above the cap
        // itself) keeps every one of its own files, while the `full` view loses exactly those 40.
        var cap = Charts.MaxDetailedCodeMapFiles;
        var all = new List<(string, long)>();
        for (var i = 0; i < cap; i++) all.Add(($"src/file-{i:00000}.cs", 5_000L + i)); // big → always inside the cap
        for (var i = 0; i < 40; i++) all.Add(($"tests/small-{i:00000}.cs", 1L + i));   // smallest → cut first

        var fullMap = CodeMap.Build(all.ToArray(), NoMetrics);
        var noTestsMap = CodeMap.Build(all.Where(f => !CodeMap.IsTestPath(f.Item1)).ToArray(), NoMetrics);
        var variants = new[]
        {
            new CodeMapVariant("full", ExcludesSpecDev: false, ExcludesTests: false, fullMap),
            new CodeMapVariant("no-tests", ExcludesSpecDev: false, ExcludesTests: true, noTestsMap),
        };

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav()));

        // `full` genuinely lost its 40 smallest, and says so with ITS own number. The notice is a <p> since the
        // listing became a tree: with one table per directory there is no single table for it to be a row of, and a
        // statement about the whole listing does not belong inside one directory's table.
        Assert.Contains("data-codemap-view=\"full\">+40 more files not shown", html);
        Assert.Contains($"The {cap:N0} most significant files in the treemap", html);

        // `no-tests` lost NOTHING — every file it contains has a row — so it gets no truncation notice and its lead
        // is allowed to say "Every file". Under the old `FileCount - cap` arithmetic this view ALSO reported 0
        // omissions while sharing `full`'s single un-markered "+40 more files" row, which is the defect.
        Assert.Contains("data-codemap-view=\"no-tests\">Every file in the treemap", html);
        Assert.DoesNotContain("data-codemap-view=\"no-tests\">+", html);

        // One truncation notice PER VIEW THAT OMITS SOMETHING — not one for the whole page, and never un-markered.
        Assert.Single(Regex.Matches(html, "codemap-table-truncated"));
        Assert.DoesNotContain("\"chart-lead codemap-table-truncated\">", html); // i.e. always view-tagged
    }

    [Fact]
    public void RenderPage_AboveTheDetailCap_AViewWhoseOwnFilesRankBelowTheGlobalTopCapReportsThemAsOmitted()
    {
        // The sharper half of the same defect: a view that is SMALLER than the cap but whose files are the LOWEST
        // -ranked ones. `no-tests` here holds only small files, so the distinct-set cap cuts them even though the
        // view has far fewer than `cap` files. `FileCount - cap` is negative → clamped to 0 → "Every file", over a
        // table with no rows for them at all. The correct figure is |view| - |view ∩ shown|. [Review][Patch]
        var cap = Charts.MaxDetailedCodeMapFiles;
        var all = new List<(string, long)>();
        for (var i = 0; i < cap; i++) all.Add(($"tests/big-{i:00000}.cs", 5_000L + i));  // tests, all large
        for (var i = 0; i < 30; i++) all.Add(($"src/tiny-{i:00000}.cs", 1L + i));        // non-test, all tiny → cut

        var fullMap = CodeMap.Build(all.ToArray(), NoMetrics);
        var noTestsMap = CodeMap.Build(all.Where(f => !CodeMap.IsTestPath(f.Item1)).ToArray(), NoMetrics);
        var variants = new[]
        {
            new CodeMapVariant("full", ExcludesSpecDev: false, ExcludesTests: false, fullMap),
            new CodeMapVariant("no-tests", ExcludesSpecDev: false, ExcludesTests: true, noTestsMap),
        };

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(variants, Nav()));

        // The no-tests view holds 30 files and EVERY ONE was cut by the distinct-set cap.
        Assert.Contains("data-codemap-view=\"no-tests\">+30 more files not shown", html);
        // And its lead must NOT claim completeness. Zero of its files are shown, so "Every file" would be a lie.
        Assert.DoesNotContain("data-codemap-view=\"no-tests\">Every file in the treemap", html);
        Assert.Contains("data-codemap-view=\"no-tests\">The 0 most significant files in the treemap", html);
    }

    // ---- "All files" is a DIRECTORY TREE (owner feedback 2026-08-01) ----------------------
    //
    // Replaced the paginated flat table. The pager tests that lived here are gone with it: collapsed directories
    // answer the Story 7.12 complaint ("no way to page through it") structurally, and do it with JavaScript off,
    // which the pager could not. What the flat table was protecting — Design Direction #5's real <thead scope="col">
    // and the pure-CSS spec/test filter — is asserted below to still hold.

    [Fact]
    public void RenderPage_AllFiles_NestsPerDirectoryTablesInsideDisclosures_NotOneFlatTable()
    {
        var files = new[]
        {
            ("src/app/Main.cs", 300L),
            ("src/app/util/Helper.cs", 120L),
            ("docs/Guide.md", 40L),
        };
        var map = CodeMap.Build(files, NoMetrics);
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(
            new[] { new CodeMapVariant("full", false, false, map) }, Nav()));

        // One tree container, and a disclosure per directory that owns files.
        Assert.Single(Regex.Matches(html, "<div class=\"codemap-tree\">"));
        Assert.Contains("<details class=\"codemap-tree-dir", html);
        // A real <table> with a real column header survives at EVERY level — the load-bearing half of Design
        // Direction #5. `<details>` cannot wrap `<tr>`, but its content model is flow content, so a `<table>`
        // nests legally as a sibling of the child disclosures.
        Assert.True(Regex.Matches(html, "<table class=\"codemap-table\">").Count >= 2,
            "one table per file-owning directory, not one flat table for the page");
        Assert.Contains("<th scope=\"col\">File</th>", html);
        // The disclosure summary carries a TEXT weight, never color alone (UX-DR17).
        Assert.Contains("codemap-tree-meta", html);
        Assert.Matches(@"codemap-tree-meta"">\d+ files? · \d+ lines?", html);
    }

    [Fact]
    public void RenderPage_AllFiles_RowShapeAndFullPathAreUnchangedFromTheFlatTable()
    {
        // The two preservations that make the CSS filter and the ADR 0013 §2 completeness claim survive the
        // reshape, asserted as their own contract rather than left implicit in the tests that happen to rely on
        // them. The filter selector is a DESCENDANT of the section, so nesting is a no-op for rows — but only
        // while the row markup itself is untouched. And the cell keeps the FULL repo-relative path: indentation
        // carries the hierarchy, so it must not also have to carry the identity.
        var map = CodeMap.Build(new[] { ("src/app/Widget.cs", 10L), ("tests/WidgetTests.cs", 5L) }, NoMetrics);
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(
            new[] { new CodeMapVariant("full", false, false, map) }, Nav()));

        Assert.Matches(@"<tr class=""codemap-table-row""><th scope=""row"">(?:<a[^>]*>)?src/app/Widget\.cs", html);
        Assert.Matches(@"<tr class=""codemap-table-row is-test""><th scope=""row"">(?:<a[^>]*>)?tests/WidgetTests\.cs", html);
    }

    [Fact]
    public void RenderPage_AllFiles_DirectoryFilterMarkersDeriveFromDescendantFiles_NotTheDirectorysOwnPath()
    {
        // The case a naive per-directory-path predicate gets wrong, and the reason the markers exist at all.
        //
        // `CodeMap.IsSpecDevPath` matches on `prefix + "/"`, so it is FALSE for the directory `.claude` itself
        // while being true for everything inside it — a marker derived from the directory's own path would fail to
        // hide exactly the directories the filter exists to hide. Conversely `src/fixtures` is not test-NAMED but
        // holds only test files, so it must still vanish under "exclude tests" or the reader is left with an empty
        // disclosure. Both are answered by computing over descendant FILE paths.
        var map = CodeMap.Build(new[]
        {
            (".claude/skills/thing.md", 10L),
            ("src/fixtures/AlphaTests.cs", 20L),
            ("src/app/Main.cs", 30L),
        }, NoMetrics);
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(
            new[] { new CodeMapVariant("full", false, false, map) }, Nav()));

        var claude = Regex.Match(html, @"<details class=""codemap-tree-dir([^""]*)""[^>]*>\s*<summary><span class=""codemap-tree-path"">\.claude");
        Assert.True(claude.Success, "the .claude directory renders a disclosure");
        Assert.Contains("dir-all-spec", claude.Groups[1].Value);
        Assert.Contains("dir-all-excluded", claude.Groups[1].Value);

        var fixtures = Regex.Match(html, @"<details class=""codemap-tree-dir([^""]*)""[^>]*>\s*<summary><span class=""codemap-tree-path"">(?:src / )?fixtures");
        Assert.True(fixtures.Success, "the all-test fixtures directory renders a disclosure");
        Assert.Contains("dir-all-test", fixtures.Groups[1].Value);

        // The ordinary directory carries NO marker — it must never be hidden by either checkbox.
        var app = Regex.Match(html, @"<details class=""codemap-tree-dir([^""]*)""[^>]*>\s*<summary><span class=""codemap-tree-path"">(?:src / )?app");
        Assert.True(app.Success, "the ordinary directory renders a disclosure");
        Assert.DoesNotContain("dir-all", app.Groups[1].Value);
    }

    [Fact]
    public void RenderPage_AllFiles_OpensTheAncestorsOfTheMostSignificantFilesAndLeavesTheRestClosed()
    {
        // All-collapsed would open on a page showing nothing — a regression from the flat table's 18 visible rows.
        // First-level-only would open one directory holding hundreds of files. The rule is significance-driven:
        // the ancestors of the busiest files are open, everything else is one click away.
        var metrics = new Dictionary<string, CodeFileMetrics>(StringComparer.OrdinalIgnoreCase)
        {
            ["hot/Busy.cs"] = new(Changes: 99, TotalChurn: 990, AvgCoChanged: null, FirstDate: null, LastDate: null),
        };
        var map = CodeMap.Build(new[] { ("hot/Busy.cs", 10L), ("cold/Quiet.cs", 10L) }, metrics);
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(
            new[] { new CodeMapVariant("full", false, false, map) }, Nav()));

        Assert.Matches(@"<details class=""codemap-tree-dir"" open>\s*<summary><span class=""codemap-tree-path"">hot", html);
        // Both are within the auto-expand budget on a two-file fixture, so assert the ORDER instead: the busy
        // directory's rolled-up change count outranks the quiet one, and directories sort by that rollup — which
        // OrderBySignificance alone could not do, since every directory's own Metrics is null.
        Assert.True(
            html.IndexOf("codemap-tree-path\">hot", StringComparison.Ordinal)
                < html.IndexOf("codemap-tree-path\">cold", StringComparison.Ordinal),
            "the directory with the higher rolled-up change count sorts first");
    }

    [Fact]
    public void RenderPage_AllFiles_ADirectoryWhoseFilesWereAllCutByTheCapEmitsNoEmptyDisclosure()
    {
        // A disclosure a reader can open to find nothing inside is worse than an absent one, and the truncation
        // notice already accounts for those files.
        var cap = Charts.MaxDetailedCodeMapFiles;
        var all = new List<(string, long)>();
        for (var i = 0; i < cap; i++) all.Add(($"big/file-{i:00000}.cs", 5_000L + i));
        for (var i = 0; i < 5; i++) all.Add(($"tiny/file-{i:00000}.cs", 1L + i)); // all below the cap → all cut

        var map = CodeMap.Build(all.ToArray(), NoMetrics);
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeMapTemplater.BuildPage(
            new[] { new CodeMapVariant("full", false, false, map) }, Nav()));

        Assert.DoesNotContain("codemap-tree-path\">tiny", html);
        Assert.Contains("+5 more files not shown in this listing", html);
    }
}
