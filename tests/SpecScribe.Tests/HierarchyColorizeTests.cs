using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 20.9's own net: the two colorize-driven projectors, the dimension contract they declare, and the
/// honest empty states on both converted surfaces.
///
/// <para><b>The golden fingerprint is NOT this story's regression net</b>, and saying so plainly matters more than
/// it might look. The golden fixture is not a git repository and cites no real files, so neither
/// <c>code-map.html</c> nor <c>git-insights.html</c> renders in it at all (Story 20.6 Task 4.1, re-confirmed here:
/// the constant did not move when this story landed). Leaning on that hash would have implied coverage that does
/// not exist. What actually covers this story is this file plus the templater tests, plus live-browser
/// verification for everything client-side.</para>
///
/// <para><b>And the client rules themselves are NOT unit-tested</b>, because this repo is SSR-first and has no JS
/// harness (Task 6.9). What is asserted here is the CONTRACT the emitter publishes — the cut points, the class
/// prefixes, the honest wording — because that is where a drift would originate. The resolution of that contract
/// into pixels was verified in a real browser across all eleven dimensions, and the Completion Notes say which.</para></summary>
public class HierarchyColorizeTests
{
    // ---- Fixtures --------------------------------------------------------------------------------------------

    private static readonly Dictionary<string, CodeFileMetrics> Metrics = new()
    {
        ["src/Charts.cs"] = new CodeFileMetrics(9, 120, new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 6),
            AvgCoChanged: 3.4,
            Contributors: new[]
            {
                new FileContributor("Alice", 7, new DateOnly(2026, 7, 6)),
                new FileContributor("Bob", 2, new DateOnly(2026, 6, 1)),
            }, TotalContributors: 2),
        ["src/Deep/Nested/Widget.cs"] = new CodeFileMetrics(2, 10, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 2),
            Contributors: new[] { new FileContributor("Bob", 2, new DateOnly(2026, 5, 2)) }, TotalContributors: 1),
    };

    private static CodeMap Map() => CodeMap.Build(
        new (string, long)[] { ("src/Charts.cs", 300L), ("src/Deep/Nested/Widget.cs", 40L), ("README.md", 5L) },
        Metrics);

    private static CodeMapVariant Variant() =>
        CodeMap.BuildVariants(
            new (string, long)[] { ("src/Charts.cs", 300L), ("src/Deep/Nested/Widget.cs", 40L), ("README.md", 5L) },
            Metrics).First(v => v.Key == "full");

    private static HierarchyExplorerConfig Config(
        IReadOnlyList<HierarchyDimension>? dims = null,
        IReadOnlyDictionary<string, string>? constants = null) =>
        new("probe", "treemap", HierarchyMode.Navigate, "probe", 480, true,
            new Charts.ChartMeta("Probe"), HierarchyTwinDisplay.Details, false, dims, constants);

    // ---- Task 6.2: the four Story 20.4 data-contract invariants, per new projector ----------------------------

    public static TheoryData<string> Projectors() => new() { "codemap", "ownership" };

    private static HierarchyExplorerModel Project(string which) => which == "codemap"
        ? HierarchyExplorer.ProjectCodeMap(Variant(), Config(HierarchyExplorer.CodeMapDimensions(hasMetrics: true)),
            fileHref: p => p.EndsWith(".cs", StringComparison.Ordinal) ? $"code/{p}.html" : null, prefix: "")
        : HierarchyExplorer.ProjectOwnership(Map().Roots, new[] { "Alice", "Bob" },
            Config(HierarchyExplorer.OwnershipDimensions()),
            fileHref: p => p.EndsWith(".cs", StringComparison.Ordinal) ? $"code/{p}.html" : null);

    [Theory]
    [MemberData(nameof(Projectors))]
    public void Projector_SatisfiesTheFourPlotlyDataContractInvariants(string which)
    {
        // Story 20.4's spike findings, which every projector must satisfy BY CONSTRUCTION rather than by luck.
        // Three of the four fail SILENTLY when violated — a blank or wrong chart with at most a console warning —
        // which is exactly why they are asserted here and not left to the browser to reveal.
        var model = Project(which);
        var nodes = model.Nodes;
        Assert.NotEmpty(nodes);

        // Finding A: EXACTLY one root. Plotly refuses a forest outright.
        Assert.Single(nodes.Where(n => n.ParentId is null));
        Assert.Equal(HierarchyExplorer.ProjectRootId, nodes[0].Id);

        // Finding B: no null in `values`. The type makes it unrepresentable; this pins that no node slipped
        // through at zero either, which Plotly draws as an unreachable hairline.
        Assert.All(nodes.Where(n => n.Kind == "file"), n => Assert.True(n.Value >= 1, $"{n.Id} sized {n.Value}"));

        // Finding C / owner D2: every parent is the EXACT sum of its drawn children — children win.
        var byParent = nodes.Where(n => n.ParentId is not null)
            .GroupBy(n => n.ParentId!)
            .ToDictionary(g => g.Key, g => g.Sum(n => n.Value));
        foreach (var n in nodes)
        {
            if (byParent.TryGetValue(n.Id, out var sum)) Assert.Equal(sum, n.Value);
        }

        // The emitted `branchvalues`, which must be decided together with the payload shape and therefore travels
        // with it rather than being a shared assumption between C# and JS.
        Assert.Contains($"\"branchvalues\":\"{HierarchyExplorer.BranchValues}\"", HierarchyExplorer.IslandHtml(model));
    }

    [Theory]
    [MemberData(nameof(Projectors))]
    public void Projector_NodeSetIsEveryFileInTheMap_AndOnlyLinksWhatResolves(string which)
    {
        // The completeness half: the chart draws every file the map holds, so the twin (or, on the Code Map, the
        // file table) can be checked against it. And Story 7.1's link guard survives the conversion — a resolver
        // returning null leaves a plain, focusable node rather than a dead link.
        var model = Project(which);
        var files = model.Nodes.Where(n => n.Kind == "file").ToList();

        Assert.Equal(3, files.Count);
        Assert.Contains(files, f => f.Id == "src/Charts.cs");
        Assert.Contains(files, f => f.Id == "src/Deep/Nested/Widget.cs");
        Assert.Contains(files, f => f.Id == "README.md");

        Assert.Equal("code/src/Charts.cs.html", files.Single(f => f.Id == "src/Charts.cs").Href);
        Assert.Null(files.Single(f => f.Id == "README.md").Href);
    }

    [Fact]
    public void ProjectCodeMap_AboveTheDetailCap_LongTailKeepsGeometryButLosesTheCard()
    {
        // [Review][Patch] The one payload-layer regression net for MaxDetailedCodeMapFiles/SelectDetailedCodeMapFiles
        // — the mechanism responsible for the real code-map.html ~82.5MB incident (Story 6.6). The old SVG-layer
        // test asserting this (the retired CodeTreemap's own detail-cap test) was correctly deleted with the SVG
        // renderer; nothing replaced it at the payload layer this story introduced until now.
        var cap = Charts.MaxDetailedCodeMapFiles;
        var fileCount = cap + 5;
        var files = Enumerable.Range(1, fileCount).Select(i => ($"src/file-{i:00000}.cs", (long)i)).ToArray();
        var variant = CodeMap.BuildVariants(files, new Dictionary<string, CodeFileMetrics>()).First(v => v.Key == "full");

        var model = HierarchyExplorer.ProjectCodeMap(variant, Config(HierarchyExplorer.CodeMapDimensions(hasMetrics: false)));
        var fileNodes = model.Nodes.Where(n => n.Kind == "file").ToList();

        // Every file still gets its own node — geometry (a real Value/size) and accessible name never drop.
        Assert.Equal(fileCount, fileNodes.Count);
        Assert.All(fileNodes, n => Assert.True(n.Value >= 1, $"{n.Id} sized {n.Value}"));

        // …but only the top `cap` most-significant (highest Lines here, since none carry Changes) keep the
        // expensive rich hover card — the per-node cost this cap exists to bound.
        Assert.Equal(cap, fileNodes.Count(n => n.TipHtml is not null));
        var smallest = fileNodes.Single(n => n.Id == "src/file-00001.cs");
        Assert.Null(smallest.TipHtml);
        var largest = fileNodes.Single(n => n.Id == $"src/file-{fileCount:00000}.cs");
        Assert.NotNull(largest.TipHtml);
    }

    [Fact]
    public void Projector_LiftsTheMetricBagFromTheSameValuesTheRetiredSvgEmbedded()
    {
        // "Lift, do not re-derive" (Task 1.1). The keys and the UNITS both matter: a date carried as an ISO
        // string instead of a day-number, or a co-change rounded differently, would re-bucket the ramp and
        // recolour the chart with nothing to show for it.
        var codeMap = Project("codemap").Nodes.Single(n => n.Id == "src/Charts.cs");
        Assert.NotNull(codeMap.Metrics);
        Assert.Equal("9", codeMap.Metrics!["changes"]);
        Assert.Equal("120", codeMap.Metrics["churn"]);
        Assert.Equal("3.4", codeMap.Metrics["cochanged"]);
        Assert.Equal("csharp", codeMap.Metrics["filetype"]);
        Assert.Equal("C#", codeMap.Metrics["filetype-label"]);
        Assert.Equal(new DateOnly(2026, 6, 1).DayNumber.ToString(), codeMap.Metrics["first"]);
        Assert.Equal(new DateOnly(2026, 7, 6).DayNumber.ToString(), codeMap.Metrics["last"]);

        var own = Project("ownership").Nodes.Single(n => n.Id == "src/Charts.cs");
        Assert.NotNull(own.Metrics);
        Assert.Equal("78", own.Metrics!["share"]);        // Alice 7/9 -> 78%, the SVG's own arithmetic
        Assert.Equal("Alice", own.Metrics["dominant"]);
        Assert.Equal("2", own.Metrics["contributors"]);
        // The compact [name, commits, lastDayNumber] triple array both the spotlight rule and the roster read.
        Assert.StartsWith("[[\"Alice\",7,", own.Metrics["owner"]);

        // A file with NO contributor record carries no ownership keys at all, so the rules see an honest absence
        // rather than a zero that would colour it as a real 0% share.
        var unowned = Project("ownership").Nodes.Single(n => n.Id == "README.md");
        Assert.False(unowned.Metrics!.ContainsKey("share"));
        Assert.Equal("No git history", unowned.StatusLabel);
    }

    [Fact]
    public void Projector_StructuralNodesCarryNoMetricBag_SoNoDimensionEverRecoloursADirectory()
    {
        // A directory has no change frequency and no dominant author, and the shipped SVG never recoloured a
        // directory rect either. The client rule keys off exactly this: no bag, no dimension.
        foreach (var which in new[] { "codemap", "ownership" })
        {
            var structural = Project(which).Nodes.Where(n => n.Kind is "directory" or HierarchyExplorer.ProjectRootKind).ToList();
            Assert.NotEmpty(structural);
            Assert.All(structural, n => Assert.Null(n.Metrics));
            // Both surfaces' structural class names carry the `-dir` marker, so a directory can never be
            // mistaken for a leaf by a rule that only ever reads the metric bag.
            Assert.All(structural, n => Assert.Contains("-dir", n.ColorClass, StringComparison.Ordinal));
        }
    }

    // ---- Task 6.3: the eleven dimension declarations reproduce the shipped rules ------------------------------

    [Fact]
    public void CodeMapDimensions_AreTheShippedSevenInTheShippedOrder_WithTheShippedScaling()
    {
        var dims = HierarchyExplorer.CodeMapDimensions(hasMetrics: true);

        Assert.Equal(
            new[] { "changes", "last", "created", "avgchange", "churn", "cochange", "filetype" },
            dims.Select(d => d.Key).ToArray());

        // The two DATE dimensions scale against the file set's own [min,max] window; everything else scales from
        // zero. Absolute day-numbers are ~739,000 and differ by hundreds, so a from-zero ramp would put every
        // file in the top bucket — the distinction is the difference between a readable chart and a flat one.
        Assert.All(dims.Where(d => d.Key is "last" or "created"),
            d => Assert.Equal(HierarchyDimensionKind.RampWindow, d.Kind));
        Assert.All(dims.Where(d => d.Key is "changes" or "churn" or "cochange" or "avgchange"),
            d => Assert.Equal(HierarchyDimensionKind.Ramp, d.Kind));

        // Average change size is churn / changes, with the shipped `!ch` guard expressed as a declared divisor.
        var avg = dims.Single(d => d.Key == "avgchange");
        Assert.Equal("churn", avg.Metric);
        Assert.Equal("changes", avg.Divisor);

        // File type is the one categorical dimension and the only one that needs no git data.
        var type = dims.Single(d => d.Key == "filetype");
        Assert.Equal(HierarchyDimensionKind.Categorical, type.Kind);
        Assert.Equal("type-", type.ClassPrefix);
        Assert.Equal("filetype-label", type.LabelMetric);

        // Six numeric dimensions share one ramp legend; the categorical one owns the discrete legend. Sharing is
        // the point — a legend per dimension would be six copies of one scale.
        Assert.All(dims.Where(d => d.Key != "filetype"),
            d => Assert.Equal(HierarchyExplorer.CodeMapRampLegend, d.LegendKey));
        Assert.Equal(HierarchyExplorer.CodeMapDiscreteLegend, type.LegendKey);
    }

    [Fact]
    public void CodeMapDimensions_WithoutGitMetrics_OfferFileTypeAlone()
    {
        // There is nothing for the six git-derived ramps to quantize, and offering an option that can only ever
        // paint "no data" would be a control that lies. Same rule the shipped dropdown followed.
        var dims = HierarchyExplorer.CodeMapDimensions(hasMetrics: false);
        Assert.Single(dims);
        Assert.Equal("filetype", dims[0].Key);
    }

    [Fact]
    public void OwnershipDimensions_KeepTheFixedCutPointsThatMakeALevelMeanTheSameThingOnEveryRepo()
    {
        var dims = HierarchyExplorer.OwnershipDimensions();
        Assert.Equal(new[] { "share", "top", "spotlight", "staleness" }, dims.Select(d => d.Key).ToArray());

        // Deliberately FIXED cut points rather than a data-relative quartile split: a share percentage is
        // meaningful on its own scale, so "76-100%" means the same thing on every repo's chart, never a moving
        // target (Charts.OwnershipShareLevel's reasoning, which the conversion had to carry rather than restate).
        var share = dims.Single(d => d.Key == "share");
        Assert.Equal(HierarchyDimensionKind.Cutoff, share.Kind);
        Assert.Equal(new[] { 25, 50, 75 }, share.Cutoffs);

        // Real-unit day boundaries, for the same reason.
        var spotlight = dims.Single(d => d.Key == "spotlight");
        Assert.Equal(new[] { 30, 90, 180 }, spotlight.Cutoffs);
        // The spotlight's own second channel, layered on top of the reused level ramp — without it the dimension
        // would be signalled by hue alone (UX-DR17).
        Assert.Equal("spotlight-touched", spotlight.ExtraClass);
        // "Not tracked here" and "tracked, date unknown" are different facts, and the shipped renderer told them
        // apart. Collapsing them would claim more than the data supports.
        Assert.Equal("owner-spotlight-off", spotlight.OffClass);
        Assert.NotEqual(spotlight.OffClass, spotlight.NoneClass);

        // The bounded top-author roster is a colour PALETTE resolved from a panel-wide constant, never a ranking.
        var top = dims.Single(d => d.Key == "top");
        Assert.Equal(HierarchyDimensionKind.Roster, top.Kind);
        Assert.Equal(HierarchyExplorer.ConstantTopAuthors, top.RosterConstant);
        Assert.Equal("owner-author-", top.ClassPrefix);

        // The two that cannot be precomputed (owner decision D1) each declare the runtime control they take.
        Assert.Equal(HierarchyDimensionArg.Roster, spotlight.Arg);
        Assert.Equal(HierarchyDimensionArg.Threshold, dims.Single(d => d.Key == "staleness").Arg);
        Assert.All(dims.Where(d => d.Key is "share" or "top"), d => Assert.Equal(string.Empty, d.Arg));
    }

    [Fact]
    public void EveryDimension_ResolvesToAClassTheShippedCascadeActuallyPaints()
    {
        // AD-7 made checkable. A dimension declares a CLASS and the client resolves it through the shipped
        // stylesheet — so a declared class the stylesheet never paints is a dimension that renders as nothing,
        // and no test below the browser would see it. Every prefix + level combination the eleven rules can
        // produce is checked against the real CSS here.
        var css = Stylesheet();

        foreach (var (family, dims) in new (string, IReadOnlyList<HierarchyDimension>)[]
                 {
                     ("codemap-cell", HierarchyExplorer.CodeMapDimensions(hasMetrics: true)),
                     ("ownership-wedge", HierarchyExplorer.OwnershipDimensions()),
                 })
        {
            foreach (var d in dims)
            {
                Assert.Contains($".{family}.{d.NoneClass}", css);
                if (d.ExtraClass.Length > 0) Assert.Contains($".{family}.{d.ExtraClass}", css);
                if (d.OffClass.Length > 0) Assert.Contains($".{family}.{d.OffClass}", css);

                switch (d.Kind)
                {
                    case HierarchyDimensionKind.Ramp:
                    case HierarchyDimensionKind.RampWindow:
                        for (var level = 0; level <= 4; level++)
                            Assert.Contains($".{family}.{d.ClassPrefix}{level}", css);
                        break;
                    case HierarchyDimensionKind.Cutoff:
                        for (var band = 1; band <= (d.Cutoffs?.Count ?? 0) + 1; band++)
                            Assert.Contains($".{family}.{d.ClassPrefix}{band}", css);
                        break;
                    case HierarchyDimensionKind.Spotlight:
                        for (var level = 1; level <= (d.Cutoffs?.Count ?? 0) + 1; level++)
                            Assert.Contains($".{family}.{d.ClassPrefix}{level}", css);
                        break;
                    case HierarchyDimensionKind.Roster:
                        for (var i = 0; i < Charts.OwnershipTopAuthorPaletteSize; i++)
                            Assert.Contains($".{family}.{d.ClassPrefix}{i}", css);
                        Assert.Contains($".{family}.{d.ClassPrefix}other", css);
                        break;
                    case HierarchyDimensionKind.Threshold:
                        Assert.Contains($".{family}.{d.ClassPrefix}fresh", css);
                        Assert.Contains($".{family}.{d.ClassPrefix}stale", css);
                        break;
                    case HierarchyDimensionKind.Categorical:
                        foreach (var cat in CodeFileType.AllCategories)
                            Assert.Contains($".{family}.{d.ClassPrefix}{cat.Key}", css);
                        break;
                }
            }
        }
    }

    [Fact]
    public void TheFiveFillOpacityStates_AreStillDeclared_AndTheResolverStillReadsThem()
    {
        // Five of the eleven dimensions' states carry a partial fill-opacity, and Plotly needs ONE paint per
        // sector. A resolver returning only `fill` would draw all five at full strength — a silent fidelity
        // regression no test in this repo could see, because the family renders, just wrong.
        var css = Stylesheet();
        foreach (var (selector, opacity) in new[]
                 {
                     (".codemap-cell.level-0", "0.35"),
                     (".codemap-cell.level-none", "0.55"),
                     (".codemap-cell.type-other", "0.55"),
                     (".ownership-wedge.owner-author-other", "0.55"),
                     (".ownership-wedge.owner-spotlight-off", "0.35"),
                 })
        {
            Assert.Matches(new Regex(Regex.Escape(selector) + @"\s*\{[^}]*fill-opacity:\s*" + Regex.Escape(opacity)), css);
        }

        // The composing half, over the shipped asset (the StylesheetTests pattern for a JS fact).
        var js = Script();
        Assert.Contains("withOpacity(cs.fill, cs.fillOpacity)", js);
    }

    [Fact]
    public void TheThreeStrokeDasharrayStates_AreCarriedByHatching_BecauseMarkerLineHasNoDash()
    {
        // The limit Story 20.5 already hit, reached again by three more states. `marker.line` has no dash, so the
        // non-colour channel has to become `marker.pattern` hatching or it simply disappears — and a state
        // distinguished by hue alone is a UX-DR17 failure that ships green.
        var css = Stylesheet();
        var js = Script();
        foreach (var token in new[] { "type-other", "owner-author-other", "owner-stale" })
        {
            Assert.Matches(new Regex(@"\." + Regex.Escape(token) + @"\s*\{[^}]*stroke-dasharray:"), css);
            Assert.Matches(new Regex("\"" + Regex.Escape(token) + "\": \"[^\"]+\""), js);
        }
    }

    // ---- Task 6.4: the non-colour channel, per dimension ------------------------------------------------------

    [Fact]
    public void EveryDimension_DeclaresItsOwnAccessibleNameText_ForEveryOutcomeItsRuleCanProduce()
    {
        // AC#1's "the non-colour channel holds across every dimension", made testable. A dimension whose fill
        // changes and whose accessible name does not is a UX-DR17 failure that ships green, so every OUTCOME each
        // rule can reach must have wording — not just the happy path.
        var required = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [HierarchyDimensionKind.Ramp] = new[] { "value", "none" },
            [HierarchyDimensionKind.RampWindow] = new[] { "value", "none" },
            [HierarchyDimensionKind.Categorical] = new[] { "value" },
            [HierarchyDimensionKind.Cutoff] = new[] { "value", "none" },
            [HierarchyDimensionKind.Roster] = new[] { "value", "none" },
            [HierarchyDimensionKind.Spotlight] = new[] { "hit", "unknown", "off" },
            [HierarchyDimensionKind.Threshold] = new[] { "fresh", "stale", "none" },
        };

        var all = HierarchyExplorer.CodeMapDimensions(hasMetrics: true)
            .Concat(HierarchyExplorer.OwnershipDimensions()).ToList();
        Assert.Equal(11, all.Count);

        foreach (var d in all)
        {
            Assert.NotEmpty(d.Label);
            foreach (var outcome in required[d.Kind])
            {
                Assert.True(d.Text.TryGetValue(outcome, out var text) && text!.Length > 0,
                    $"dimension '{d.Key}' has no '{outcome}' wording — that outcome would recolour silently");
            }
        }

        // Every dimension's wording is DISTINGUISHABLE from every other's, or switching would change the fill and
        // leave a screen-reader user reading the same sentence.
        var valueTexts = all.Where(d => d.Text.ContainsKey("value"))
            .Select(d => d.Text["value"].Replace("{label}", d.Label, StringComparison.Ordinal)).ToList();
        Assert.Equal(valueTexts.Count, valueTexts.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TheCarefullyHedgedWording_SurvivedThePortVerbatim()
    {
        // Three phrasings that were each a deliberate correction in a prior review, and each of which a rewrite
        // would quietly undo. They are the reason the rules carry their text as DATA rather than the component
        // composing sentences it does not have the context to get right.
        var spotlight = HierarchyExplorer.OwnershipDimensions().Single(d => d.Key == "spotlight");

        // 1. Absence means "not in this file's own capped list", never the stronger, sometimes-false claim.
        Assert.Contains("most-active tracked contributors", spotlight.Text["off"]);
        Assert.DoesNotContain("has not worked on this file", spotlight.Text["off"]);

        // 2. An unknown last-touch date says so, rather than being coerced into the oldest recency bucket, which
        //    would fabricate a "long ago" the embedded data never supports.
        Assert.Contains("date unknown", spotlight.Text["unknown"]);

        // 3. The ramp dimensions report the bucket LEVEL, which is exactly what the colour encodes — never the
        //    raw day-number or count the colour does not literally represent.
        var ramp = HierarchyExplorer.CodeMapDimensions(hasMetrics: true).Single(d => d.Key == "changes");
        Assert.Contains("{level}", ramp.Text["value"]);
        Assert.DoesNotContain("{value}", ramp.Text["value"]);

        // And staleness measures the FILE's own last-touch date — `last` carries no author, so no wording here
        // may imply a per-contributor signal.
        var stale = HierarchyExplorer.OwnershipDimensions().Single(d => d.Key == "staleness");
        Assert.Equal("last", stale.Metric);
        Assert.DoesNotContain("contributor", stale.Text["stale"]);
        Assert.DoesNotContain("contributor", stale.Text["fresh"]);
    }

    [Fact]
    public void NoDimensionRanksContributors_InAnyMode()
    {
        // FR-10 / ADR 0010 §4, and rendering technology does not change it: "top contributors" is a colour
        // palette, not a leaderboard, and the spotlight is a filter, not a score. Asserted over the whole
        // reader-facing surface of the contract — the labels and every phrasing.
        var reader = string.Join(" ", HierarchyExplorer.OwnershipDimensions()
            .SelectMany(d => d.Text.Values.Append(d.Label)));

        foreach (var banned in new[] { "leaderboard", "rank", "top performer", "productivity", "most commits", "score" })
        {
            Assert.DoesNotContain(banned, reader, StringComparison.OrdinalIgnoreCase);
        }

        // The spotlight roster is built by the CLIENT from the alphabetical union of every node's own list, never
        // from the bounded top-N palette — so the picker can offer a contributor the palette has no colour for.
        var js = Script();
        Assert.Contains("function dimRoster", js);
        Assert.Contains("a.localeCompare(b)", js);
    }

    // ---- Task 6.8: the honest empty states -------------------------------------------------------------------

    [Fact]
    public void AnEmptyVariant_ProducesNoModel_SoTheCallSiteCanRenderItsOwnHonestNotice()
    {
        // NFR8: a missing panel is not an empty state. The projector returns nothing and the templater says "No
        // files match this filter." in words, rather than the component drawing an empty chart frame.
        var onlyTests = CodeMap.BuildVariants(
            new (string, long)[] { ("tests/OnlyTests/FooTests.cs", 10L) },
            new Dictionary<string, CodeFileMetrics>());
        var excluded = onlyTests.Single(v => v.Key == "no-tests");

        var model = HierarchyExplorer.ProjectCodeMap(excluded, Config(HierarchyExplorer.CodeMapDimensions(false)));
        Assert.Empty(model.Nodes);
        Assert.Equal(string.Empty, HierarchyExplorer.Render(model));
        Assert.Equal(string.Empty, HierarchyExplorer.IslandHtml(model));
    }

    [Fact]
    public void AnEmptyTree_ProducesNoOwnershipModel()
    {
        var model = HierarchyExplorer.ProjectOwnership(
            Array.Empty<CodeMapNode>(), Array.Empty<string>(), Config(HierarchyExplorer.OwnershipDimensions()));
        Assert.Empty(model.Nodes);
        Assert.Equal(string.Empty, HierarchyExplorer.Render(model));
    }

    [Fact]
    public void TheCodeMapEmitsNoGenericTwin_BecauseItsFileTableIsARicherOne()
    {
        // Story 20.6 D1. Emitting both would ship two complete listings of the same file set on one page — a byte
        // cost AND on-screen duplication, for strictly less information than the table already carries.
        // `External` is a named mode rather than a null check precisely so this is a decision on the record.
        var model = HierarchyExplorer.ProjectCodeMap(
            Variant(),
            Config(HierarchyExplorer.CodeMapDimensions(true)) with { TwinDisplay = HierarchyTwinDisplay.External });

        Assert.Equal(string.Empty, HierarchyExplorer.TextTwinHtml(model));
        Assert.DoesNotContain("ss-hierarchy-twin", HierarchyExplorer.Render(model));
        // The island is unaffected — the twin's presentation is a server-only concern the client never sees.
        Assert.Contains("ss-hierarchy-data", HierarchyExplorer.Render(model));
    }

    // ---- The payload shape is gated, so the six already-shipped surfaces are byte-identical -------------------

    [Fact]
    public void ASurfaceWithNoDimensions_KeepsTheExactPayloadShapeItAlreadyEmitted()
    {
        // The compact shape (null-skipping + relaxed encoding) exists for the two pages whose byte accounting
        // this story settles. Applying it everywhere would also drop `"parentId":null` from the six surfaces
        // Story 20.7 converted and move the golden fingerprint for a reason unrelated to this work — so it is
        // gated on "does this instance declare dimensions", and that gate is asserted rather than trusted.
        var plain = HierarchyExplorer.ProjectStoryTasks(
            "1.1", "Sample", new[] { new TaskItem("Do it", true, Array.Empty<TaskItem>()) }, Config());
        var island = Regex.Match(
            HierarchyExplorer.IslandHtml(plain),
            "<script type=\"application/json\"[^>]*>(?<j>.*?)</script>",
            RegexOptions.Singleline).Groups["j"].Value;

        Assert.Contains("\"parentId\":null", island);
        Assert.DoesNotContain("\"metrics\"", island);
        Assert.DoesNotContain("\"dimensions\"", island);
        // The DEFAULT encoder is still in force here, so an angle bracket anywhere in the payload stays a
        // six-byte escape - which is the shape these six surfaces already ship and the golden fingerprint holds.
        Assert.DoesNotContain("<", island);
        Assert.DoesNotContain(">", island);
    }

    [Fact]
    public void ADimensionBearingPayload_IsSafeToEmbedAndStillParses()
    {
        // The compact shape's safety argument, checked rather than argued: `</` and `<!` are the only two
        // sequences that can end or re-frame a <script type="application/json"> element, and neither survives.
        var model = Project("codemap");
        var html = HierarchyExplorer.Render(model);
        var island = Regex.Match(html, "<script type=\"application/json\"[^>]*>(?<j>.*?)</script>", RegexOptions.Singleline).Groups["j"].Value;

        Assert.DoesNotContain("</", island);
        Assert.DoesNotContain("<!", island);

        using var doc = JsonDocument.Parse(island);
        var nodes = doc.RootElement.GetProperty("nodes");
        Assert.Equal(model.Nodes.Count, nodes.GetArrayLength());
        // The rich hover card round-trips back to real markup rather than escaped text.
        var tip = nodes.EnumerateArray().First(n => n.TryGetProperty("tip", out _)).GetProperty("tip").GetString();
        Assert.Contains("<div class='codemap-card'>", tip);
    }

    // ---- helpers ---------------------------------------------------------------------------------------------

    private static string Stylesheet() => EmbeddedAsset("SpecScribe.assets.specscribe.css");

    private static string Script() => EmbeddedAsset("SpecScribe.assets.specscribe.js");

    private static string EmbeddedAsset(string name)
    {
        using var stream = typeof(Charts).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded asset '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
