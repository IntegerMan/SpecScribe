using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the in-portal code file page (Story 7.1): the a11y shell, the locked <c>id="L{n}"</c>
/// line-anchor convention, 1:1 numbering (blank lines included), HTML escaping, and the placeholder page.</summary>
public class CodeFileTemplaterTests
{
    private static SiteNav Nav() =>
        SiteNav.Build(new[] { "planning-artifacts/epics.md" }, "SpecScribe", hasAdrs: false);

    private const string OutputPath = "code/src/SpecScribe/Sample.cs.html";
    private const string RepoRelative = "src/SpecScribe/Sample.cs";

    [Fact]
    public void RenderPage_RendersTitleBreadcrumbAndA11yShell()
    {
        var page = CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav());
        var html = RegionAssert.Of(page);

        RegionAssert.HasTitle(page, $"{RepoRelative} — SpecScribe");
        Assert.Contains($"<h1>{RepoRelative}</h1>", html);
        Assert.Contains("<div class=\"story-kicker\">Source File</div>", html);
        // Site a11y contract: skip-link first, single main landmark.
        // [Story 23.6 AC #8] The skip link assertion lived here and is NOT lost: it is chrome, emitted by the
        // head, and the region carries no head. `npm run check:a11y` owns `skip-link` over every EMITTED page —
        // which is the only place it can be checked honestly now that no C# path composes a whole page — and
        // `check:parity`'s pageSha hashes the whole document for the pinned corpus.
        Assert.Contains("<main id=\"main-content\">", html);
        // Breadcrumb: Home / <file path>. The nested page's Home link carries the correct ../ depth prefix.
        Assert.Contains("Home", html);
        // [Story 23.6 AC #8] skip-link-precedes-main moved to `npm run check:a11y`, which asserts it over
        // the emitted page. The region begins at the nav, so relative ordering against the head is not
        // decidable here at all — keeping the assertion would have meant weakening it to something true.
        // Exactly one main landmark.
        RegionAssert.HasSingleMainLandmark(html);
    }

    [Fact]
    public void RenderPage_EmitsOneAnchoredLinePerSourceLineNumberedFromOne()
    {
        var lines = new[] { "line one", "line two", "line three" };

        var page = CodeFileTemplater.BuildPage(RepoRelative, OutputPath, lines, Nav());
        var html = RegionAssert.Of(page);

        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("id=\"L2\"", html);
        Assert.Contains("id=\"L3\"", html);
        Assert.DoesNotContain("id=\"L4\"", html);
        // Count matches the input line count exactly (1:1).
        Assert.Equal(lines.Length, CountOccurrences(html, "class=\"code-line\""));
        // Each line carries its 1-based number in data-ln (a CSS gutter counter, not tokenized text) and the source
        // text sits directly in the anchored span so Prism's tokenizer sees pure source.
        Assert.Contains("<span class=\"code-line\" id=\"L1\" data-ln=\"1\">line one</span>", html);
        // A .cs file routes to the csharp grammar so Prism highlights it.
        Assert.Contains("<code class=\"language-csharp\">", html);
        // Prism's stylesheet + highlighter are loaded on a rendered code page.
        Assert.Contains("prism.css", page.Assets.ExtraHead);
        Assert.Contains("prism.js", page.Assets.ExtraHead);
        // Line-count meta pill.
        Assert.Contains("<span class=\"pill\">3 lines</span>", html);
    }

    [Fact]
    public void RenderPage_BlankLineStillEmitsAnchoredRowSoNumberingStays1To1()
    {
        var lines = new[] { "before", "", "after" };

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, lines, Nav()));

        // Three lines, three anchors — the blank middle line is not collapsed away.
        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("id=\"L2\"", html);
        Assert.Contains("id=\"L3\"", html);
        Assert.Equal(3, CountOccurrences(html, "class=\"code-line\""));
        // The blank line renders an empty (but present) anchored span.
        Assert.Contains("<span class=\"code-line\" id=\"L2\" data-ln=\"2\"></span>", html);
    }

    [Fact]
    public void RenderPage_EscapesHtmlMetacharactersInSource()
    {
        var lines = new[] { "if (a < b && c > d) return \"x\";" };

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, lines, Nav()));

        Assert.Contains("if (a &lt; b &amp;&amp; c &gt; d) return &quot;x&quot;;", html);
        // The raw, unescaped angle bracket form must never reach the output.
        Assert.DoesNotContain("a < b", html);
    }

    [Fact]
    public void RenderPage_SingleLineUsesSingularPill()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "only" }, Nav()));

        Assert.Contains("<span class=\"pill\">1 line</span>", html);
    }

    [Fact]
    public void RenderPage_UnknownExtensionRendersPlainCodeBlockWithoutLanguageClass()
    {
        // A file type not in the vendored grammar bundle falls back to plain monospace rather than a wrong grammar.
        const string path = "docs/notes.unknownext";
        const string output = "code/docs/notes.unknownext.html";

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(path, output, new[] { "just text" }, Nav()));

        Assert.Contains("<pre class=\"code-file\"><code>", html);
        Assert.DoesNotContain("language-", html);
    }

    [Fact]
    public void RenderPlaceholder_RendersShellAndReasonWithoutLineTable()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPlaceholderPage(RepoRelative, OutputPath, "This file is too large to render inline.", Nav()));

        Assert.Contains("<main id=\"main-content\">", html);
        Assert.Contains($"<h1>{RepoRelative}</h1>", html);
        Assert.Contains("<p class=\"code-placeholder\">This file is too large to render inline.</p>", html);
        // No line table on a placeholder page.
        Assert.DoesNotContain("class=\"code-line\"", html);
        // A placeholder renders no <code> block, so it does not pull in the highlighter.
        Assert.DoesNotContain("prism.js", html);
        Assert.Contains("<span class=\"pill\">Not rendered</span>", html);
    }

    private static readonly (string OutputUrl, string Title, (int Number, string Title)? Epic)[] Refs =
    {
        ("epics/story-7-1.html", "Story 7.1: In-Portal Code File Browsing", null),
        ("epics/epic-8.html", "Epic 8: Dashboard Command Center", null),
    };

    [Fact]
    public void RenderPage_WithReferences_LeadsWithRelationshipGraphThenSecondarySource()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs));

        // The relationships block (graph component + accessible list) is present and is the hero — it precedes the
        // source. Story 24.2: the card is now the component's FRAMED panel, not a hand-written <section>.
        Assert.Contains("class=\"chart-panel code-relationships\"", html);
        Assert.Contains("data-relgraph", html);
        Assert.Contains("<section class=\"code-source-section\"", html);
        var relIndex = html.IndexOf("code-relationships", StringComparison.Ordinal);
        var srcIndex = html.IndexOf("code-source-section", StringComparison.Ordinal);
        Assert.True(relIndex >= 0 && relIndex < srcIndex, "relationships must lead the page, source is secondary");

        // Each citing artifact is a graph node in the island, carrying its full title, its compact ring label and a
        // real href to its own page.
        Assert.Contains("\"l\":\"Story 7.1\"", html);
        Assert.Contains("\"p\":\"Story 7.1: In-Portal Code File Browsing\"", html);
        Assert.Contains("\"h\":\"../../../epics/story-7-1.html\"", html);

        // The always-present accessible list carries the FULL titles and meaningful link text — visually hidden
        // (sr-only) so the visible surface is just the graph, but present in the DOM for assistive tech.
        Assert.Contains("class=\"ref-list sr-only\"", html);
        Assert.Contains(">Story 7.1: In-Portal Code File Browsing</a>", html);
        Assert.Contains(">Epic 8: Dashboard Command Center</a>", html);

        // The locked line anchors survive the redesign (source is de-emphasized, never removed).
        Assert.Contains("id=\"L1\"", html);
        Assert.Contains("data-code-path=\"src/SpecScribe/Sample.cs\"", html);
    }

    // ---- Story 24.2: the interactive ego graph replaces the pure-SVG reference graph ----

    [Fact]
    public void RenderPage_Cited_EmitsGraphHostIslandAndEngineButNoRetiredSvg()
    {
        var page = CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs);
        var html = RegionAssert.Of(page);

        // Host, island and boot handshake — the three halves of the component's contract.
        Assert.Contains("data-relgraph></div>", html);
        Assert.Contains("<script type=\"application/json\" id=\"relgraph-", html);
        Assert.True(page.Assets.GraphBootInline, "a graph host must request the anti-flash boot handshake");
        // The engine is pulled ONLY because the rendered body carries a host (the flag is derived, never hand-set).
        Assert.True(page.Assets.GraphEngineNeeded, "the graph host must pull the vendored engine");
        // Nothing of the retired SVG survives — this is the ADR 0013 §1/§4 retirement, not a coexistence.
        Assert.DoesNotContain("ref-graph", html);
        Assert.DoesNotContain("ref-edge", html);
        Assert.DoesNotContain("ref-dot", html);
        Assert.DoesNotContain("refgraph-toggle", html);
        Assert.DoesNotContain("data-view=\"flat-flat\"", html);
    }

    [Fact]
    public void RenderPage_Uncited_NoGraph_DoesNotPullTheEngineOrTheBootScript()
    {
        // The engine flag is derived from the rendered body, so a page with no graph must stay clean of a 1.2 MB
        // bundle it cannot use — and of the boot marker that would otherwise show "Initializing…" over nothing.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav()));

        Assert.DoesNotContain("data-relgraph", html);
        Assert.DoesNotContain("plotly-hierarchy.min.js", html);
        Assert.DoesNotContain("data-ss-relgraph-boot", html);
    }

    [Fact]
    public void RenderPage_TabRadios_CarryTheRevealMarkerOnlyWhenAGraphIsHosted()
    {
        // ⚠ THE ZERO-WIDTH MOUNT TRAP. The Relationships panel is display:none at mount whenever an Insights panel
        // exists, and Plotly draws a WRONG-SIZED chart rather than complaining. The marker is what routes the tab
        // radios into the deferred-mount handshake, so its absence is a silent rendering defect — pinned here
        // because no rendering assertion can see it.
        var withGraph = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));
        Assert.Contains("data-relgraph-reveal", withGraph);
        // One per tab radio, so whichever tab the reader arrives on flushes the pending mount.
        Assert.Equal(4, CountOccurrences(withGraph, "data-relgraph-reveal"));

        // A tabbed page with NO graph (deep-git insight but no citers and no coupling) must not carry it.
        var noGraph = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(),
            insight: new FileInsight(3, new[] { ("Alice", 3) }, Array.Empty<CoupledFile>(), Array.Empty<CommitTouch>(), 1)));
        Assert.DoesNotContain("data-relgraph-reveal", noGraph);
    }

    [Fact]
    public void RenderPage_Graph_CarriesTheStory102FramingFromTheSharedSource()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        Assert.Contains("<h3>Relationships</h3>", html);
        // The framing sentence comes from Charts.WhyText, never hand-rolled at the call site.
        Assert.Contains(Charts.WhyText(Charts.ChartMetric.ChangeCoupling), html);
        Assert.Contains("class=\"chart-frame-ranking\"", html);
    }

    [Fact]
    public void RenderPage_CitationsOnly_OmitsTheCouplingFramingItCannotDraw()
    {
        // A citations-only card (no --deep-git) must not carry a sentence about change coupling: a frame that
        // describes a metric the chart does not draw is the misdescribing-frame class Story 10.2 exists to prevent.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs));

        Assert.DoesNotContain(Charts.WhyText(Charts.ChartMetric.ChangeCoupling), html);
        Assert.DoesNotContain("chart-frame-why", html);
    }

    [Fact]
    public void RenderPage_Legend_DisclosesTheWidthBandingAndNamesNonColourChannels()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        // ADR 0030 §5: Plotly's line style is trace-level, so stroke width is QUANTISED. A legend claiming a
        // continuous scale beside a banded chart is the misdescribing-entry class 10.7 and 21.1 each closed.
        Assert.Contains("banded into 3 steps", html);
        Assert.Contains("not a continuous scale", html);
        // Every channel is named in prose, so the reading survives colour removal (UX-DR17).
        Assert.Contains("gold circle on a solid spoke", html);
        Assert.Contains("neutral diamond on a dashed spoke", html);
        Assert.Contains("Longer dashes mark a pair that crosses a directory boundary", html);
        // …and confidence is explicitly NOT claimed to be readable from the drawn width.
        Assert.Contains("the drawn thickness is a band, not a reading", html);
    }

    [Fact]
    public void RenderPage_Legend_OmitsEntriesForChannelsThisInstanceDoesNotDraw()
    {
        // A legend row must never point at zero edges. Citations-only: no coupling, no boundary, no process, no
        // cross entries.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs));

        Assert.Contains("gold circle on a solid spoke", html);
        Assert.DoesNotContain("neutral diamond on a dashed spoke", html);
        Assert.DoesNotContain("banded into 3 steps", html);
        Assert.DoesNotContain("Dotted spokes are process coupling", html);
        Assert.DoesNotContain("Dash-dot edges relate", html);
    }

    [Fact]
    public void RenderPage_Filters_AreEmittedHiddenAndOnlyWhenTheirEdgesExist()
    {
        // Owner decision D3: both toggles survive as CLIENT edge filters, inside the component's hidden control bar
        // so a JS-off reader never sees an inert checkbox — and only when they govern something. The retired card
        // shipped both unconditionally, which meant a checkbox that toggled nothing.
        var both = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), RefsWithEpics, insight: SampleInsight(),
            storyRelatedEdges: new[] { (0, 0) }));
        Assert.Contains("<div class=\"ss-relgraph-controls\" hidden>", both);
        Assert.Contains("data-relgraph-filter=\"epic\"", both);
        Assert.Contains("data-relgraph-filter=\"cross\"", both);

        // No epic membership and no cross edges → no control bar at all.
        var neither = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs));
        Assert.DoesNotContain("ss-relgraph-controls", neither);
        Assert.DoesNotContain("data-relgraph-filter", neither);
    }

    [Fact]
    public void RenderPage_Island_CarriesEveryEdgeWithItsGoverningFilterAndServerResolvedStyle()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), RefsWithEpics, insight: SampleInsight(),
            storyRelatedEdges: new[] { (0, 0) }, relatedRelatedEdges: new[] { (0, 1) }));

        // Style classes are resolved SERVER-side and shipped, so legend, payload and drawn chart cannot disagree.
        Assert.Contains("\"styles\":[", html);
        Assert.Contains("\"k\":\"cite\",\"dash\":\"solid\"", html);
        Assert.Contains("\"k\":\"cross\",\"dash\":\"5px,2px,1px,2px\"", html);
        // Cross-boundary takes a LONGER dash and process coupling a DOT pattern — never a hue change (UX-DR17).
        Assert.Contains("\"dash\":\"9px,4px\"", html);
        // Each edge names its KIND; the filter that governs a kind and the phrase describing it live in ONE
        // config row per kind rather than on every edge (measured: the per-edge form was 56% repetition).
        Assert.Contains("\"kinds\":[", html);
        Assert.Contains("{\"k\":\"epic\",\"f\":\"epic\"", html);
        Assert.Contains("{\"k\":\"xcite\",\"f\":\"cross\"", html);
        Assert.Contains("{\"k\":\"cite\",\"phrase\":", html);
        Assert.Contains("\"e\":\"epic\"", html);
        Assert.Contains("\"e\":\"xcite\"", html);
        // No float artifact reaches the payload.
        Assert.DoesNotContain("999999", html);
    }

    [Fact]
    public void RenderPage_Island_PinsTheFocalNodeDeadCentre()
    {
        // Owner decision D1: the focal file is pinned at the canvas centre and excluded from the relaxation, so the
        // hub-and-spoke read cannot drift. Asserted on the emitted GEOMETRY, not on a configuration flag.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        Assert.Contains("\"id\":\"focal\",\"l\":\"Sample.cs\",\"p\":\"src/SpecScribe/Sample.cs\",\"x\":\"0.5\",\"y\":\"0.5\"", html);
    }

    [Fact]
    public void RenderPage_Island_IsIdenticalAcrossRepeatedRenders()
    {
        // Node position is DATA (ADR 0030 §2), so the same input must produce the same coordinates. In-process
        // repetition cannot see string-hash randomization across processes — that is verified separately in
        // CouplingLayoutTests — but it does catch an accumulator whose order depends on a dictionary walk.
        var a = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), RefsWithEpics, insight: SampleInsight(),
            storyRelatedEdges: new[] { (0, 0) }, relatedRelatedEdges: new[] { (0, 1) }));
        var b = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), RefsWithEpics, insight: SampleInsight(),
            storyRelatedEdges: new[] { (0, 0) }, relatedRelatedEdges: new[] { (0, 1) }));

        Assert.Equal(a, b);
    }

    [Fact]
    public void PlaceholderPage_WithCiters_RendersTabsNotAnAsideGraph()
    {
        // Pins the reachability fact Story 24.2 acted on: `BuildAside`'s citing-artifact graph — the SECOND
        // Charts.ReferenceGraph call site — could never draw, because a page with citers always has a
        // relationships panel and therefore always takes the TABBED branch. Recorded as a test rather than left as
        // a reasoning claim, since the whole decision to delete that branch rests on it.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs));

        Assert.Contains("code-tab--relationships", html);
        Assert.DoesNotContain("<aside class=\"code-aside\">", html);
    }

    [Fact]
    public void RenderPage_NoReferences_OmitsRelationshipsBlockButKeepsSource()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav()));

        Assert.DoesNotContain("code-relationships", html);
        Assert.DoesNotContain("ref-graph", html);
        // Source still renders with its anchors.
        Assert.Contains("<section class=\"code-source-section\"", html);
        Assert.Contains("id=\"L1\"", html);
    }

    [Fact]
    public void RenderPage_WithExternalUrl_AddsAdditiveViewSourceLink()
    {
        const string external = "https://github.com/owner/repo/blob/main/src/SpecScribe/Sample.cs";
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, external));

        Assert.Contains("class=\"code-external-link\"", html);
        Assert.Contains($"href=\"{external}\"", html);
        Assert.Contains("View on GitHub", html);
        // The in-portal source is still fully rendered — the external link is additive, not a replacement.
        Assert.Contains("class=\"code-file\"", html);
        Assert.Contains("<span class=\"code-line\" id=\"L1\" data-ln=\"1\">using System;</span>", html);
    }

    [Fact]
    public void RenderPage_NoExternalUrl_OmitsViewSourceLink()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs));

        Assert.DoesNotContain("code-external-link", html);
    }

    // ---- Story 7.4: opt-in "Advanced coverage" section ----

    private static FileInsight SampleInsight() => new(
        ChangeCount: 7,
        Contributors: new[] { ("Alice", 5), ("Bob", 2) },
        // Story 24.1: directional. Other.cs rides 4 of this file's 7 changes (57%) and is same-module; notes.md
        // rides 2 of 7 (29%) and is cross-boundary (docs/ vs src/) — so one fixture exercises both markers.
        CoupledFiles: new[]
        {
            new CoupledFile("src/SpecScribe/Other.cs", Support: 4, Confidence: 4.0 / 7, Lift: 1.4,
                CrossBoundary: false, Kind: GitMetrics.CouplingKind.Code),
            new CoupledFile("docs/notes.md", Support: 2, Confidence: 2.0 / 7, Lift: null,
                CrossBoundary: true, Kind: GitMetrics.CouplingKind.Code),
        },
        History: new[]
        {
            new CommitTouch("abc1234", new DateOnly(2026, 7, 3), "Alice", "Refine the thing"),
            new CommitTouch("def5678", new DateOnly(2026, 7, 1), "Bob", "Seed the thing"),
        },
        TotalContributors: 2);

    [Fact]
    public void RenderPage_NullInsight_RendersNoAdvancedCoverageSection()
    {
        // A null insight (deep-git off / no data) must leave the page byte-identical to a plain render.
        var baseline = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs));
        var withNull = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: null));

        Assert.DoesNotContain("code-insights", withNull);
        Assert.Equal(baseline, withNull);
    }

    [Fact]
    public void RenderPage_PopulatedInsight_RendersContributorsFrequencyAndHistory()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        Assert.Contains("<section class=\"code-insights\"", html);
        // Change frequency line.
        Assert.Contains("Changed in <strong>7</strong> commits", html);
        // Contributors — "N commits" attribution wording, no ranking language.
        Assert.Contains(">Alice</span> <span class=\"contributor-count\">5 commits</span>", html);
        Assert.Contains(">Bob</span> <span class=\"contributor-count\">2 commits</span>", html);
        // Scoped to the INSIGHTS panel. The prohibition is on ranking PEOPLE — a contributor leaderboard is the
        // thing Story 7.4 refused to build. Story 24.2's relationship card legitimately says "ranked by" of
        // CO-CHANGED FILES in its Story 10.2 ranking slot, and a whole-document substring search cannot tell the
        // two apart, so it was quietly forbidding a different thing than it meant to.
        var insightsPanel = Between(html, "code-tabpanel--insights", "code-tabpanel--relationships");
        Assert.DoesNotContain("rank", insightsPanel.ToLowerInvariant());
        Assert.DoesNotContain("leaderboard", html.ToLowerInvariant());
        Assert.DoesNotContain("top developer", html.ToLowerInvariant());
        // Story 7.8 (AC #2): coupled files are NO LONGER a visible list in the coverage section — the graph owns that
        // relationship now. The redundant visible "Often changed with" list must be gone.
        Assert.DoesNotContain("Often changed with", html);
        Assert.DoesNotContain("code-insight-coupled", html);
        Assert.DoesNotContain("coupled-count", html);
        // History rows: date, hash, author, subject, newest-first.
        Assert.Contains("<table class=\"code-history-table\">", html);
        Assert.Contains("2026-07-03", html);
        Assert.Contains("Refine the thing", html);
        var newer = html.IndexOf("Refine the thing", StringComparison.Ordinal);
        var older = html.IndexOf("Seed the thing", StringComparison.Ordinal);
        Assert.True(newer >= 0 && newer < older, "history must be newest-first");
    }

    // ---- Story 7.8: related-file nodes on the reference graph ----

    [Fact]
    public void RenderPage_PopulatedInsight_RendersRelatedFilesAsGraphNodesAndSrOnlyEntries()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        // The co-changed files render as the graph's second node population (Story 24.2: neutral diamonds in the
        // island's node array, on dashed coupling spokes) …
        Assert.Contains("\"p\":\"src/SpecScribe/Other.cs\"", html);
        Assert.Contains("\"k\":\"coupled\"", html);
        Assert.Contains("\"dash\":\"4px,3px\"", html);
        // … each carrying the full path + the directional metric in ONE server-composed sentence, which is the
        // node's tooltip AND its accessible name AND its spoke's hover text, so they cannot disagree.
        Assert.Contains("src/SpecScribe/Other.cs \\u2014 changed together 4 times, confidence 57%, lift 1.4\\u00D7.", html);
        Assert.Contains("docs/notes.md \\u2014 changed together 2 times, confidence 29%, cross-boundary.", html);
        // The ranking caption frames the coupled population and states how it is ranked.
        Assert.Contains("Co-changed files are ranked by", html);
        // The sr-only list carries the related files as a labelled text equivalent (path + co-change count).
        Assert.Contains("Files changed alongside this one:", html);
        Assert.Contains("src/SpecScribe/Other.cs &#8212; changed together 4 times", html);
        Assert.Contains("docs/notes.md &#8212; changed together 2 times", html);
    }

    // ---- Story 24.1: the sr-only coupled list is the canonical directional text twin ----

    [Fact]
    public void RenderPage_SrOnlyRelatedList_CarriesDirectionalConfidencePerEntry()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        // 4 of the focal file's 7 changes = 57%; 2 of 7 = 29%. Read from THIS file's side, per AC #3.
        Assert.Contains("changed together 4 times &#183; confidence 57%", html);
        Assert.Contains("changed together 2 times &#183; confidence 29%", html);
    }

    [Fact]
    public void RenderPage_SrOnlyRelatedList_MarksCrossBoundaryCouplesAsWordsNotColour()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        // docs/notes.md crosses from src/ into docs/; src/SpecScribe/Other.cs does not. The marker must be readable
        // text (UX-DR19/NFR8) and must attach only to the crossing entry.
        Assert.Contains("confidence 29% &#183; cross-boundary", html);
        Assert.DoesNotContain("confidence 57% &#183; cross-boundary", html);
    }

    [Fact]
    public void RenderPage_SrOnlyRelatedList_CarriesLiftOnTheRowTitleAndOmitsItWhenUndefined()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        // Other.cs has lift 1.4; notes.md's lift is null (undefined denominator) and must simply not appear —
        // never as "NaN"/"∞", which is what an unguarded division would have rendered.
        //
        // The caption NAMES the coupled file rather than saying "this file's usual rate". CoupledFile.Lift divides
        // by the COUPLED file's base rate (GitMetrics passes OtherChangeCount), so the old wording attributed the
        // number to the focal file — and the hub's table worded the identical number the other way. One number
        // cannot belong to two different files. [Story 24.1 code review]
        Assert.Contains("Lift 1.4&#215; src/SpecScribe/Other.cs's usual rate", html);
        Assert.DoesNotContain("this file's usual rate", html);
        Assert.Equal(1, CountOccurrences(html, "usual rate"));
        Assert.DoesNotContain("NaN", html);
        Assert.DoesNotContain("Infinity", html);
    }

    [Fact]
    public void RenderPage_NullInsight_GraphIsCitationsOnly_ByteIdenticalToBaseline()
    {
        // With no insight there are no related-file nodes: the relationships card must be exactly the citations-only
        // card. Byte-identity proves the additive overload and the no-deep-git degradation path.
        var baseline = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs));
        var withNull = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: null));

        Assert.Equal(baseline, withNull);
        Assert.DoesNotContain("\"k\":\"coupled\"", baseline);
        Assert.DoesNotContain("Files changed alongside this one:", baseline);
        // No coupling edges, so no coupling framing and no dashed-spoke legend entry.
        Assert.DoesNotContain("Co-changed files are ranked by", baseline);
        Assert.DoesNotContain("neutral diamond on a dashed spoke", baseline);
    }

    [Fact]
    public void RenderPage_RelatedFileLink_GuardedOnCodePageExistence()
    {
        // Only src/SpecScribe/Other.cs has a code page; docs/notes.md does not → non-link node, never a dead link.
        string? Resolve(string path) => path == "src/SpecScribe/Other.cs" ? "code/src/SpecScribe/Other.cs.html" : null;

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: SampleInsight(), coupledFileHref: Resolve));

        // Resolved coupled file → a linked graph node + a linked sr-only entry (prefixed to the code page's depth).
        Assert.Contains("\"h\":\"../../../code/src/SpecScribe/Other.cs.html\"", html);
        Assert.Contains("<a href=\"../../../code/src/SpecScribe/Other.cs.html\">src/SpecScribe/Other.cs</a>", html);
        // Unresolved coupled file → a null href (the client renders it non-activatable) + plain sr-only text.
        Assert.Contains("\"p\":\"docs/notes.md\",\"x\":", html);
        Assert.DoesNotContain("href=\"../../../code/docs/notes.md", html);
        Assert.DoesNotContain("<a href=\"../../../docs/notes.md", html);
    }

    [Fact]
    public void RenderPage_HistoryHashLink_GuardedOnCommitPageExistence()
    {
        // abc1234 has a per-commit page; def5678 does not → plain <code>, never a dead link.
        string? Resolve(string shortHash) => shortHash == "abc1234" ? "commit/abc1234.html" : null;

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: SampleInsight(), commitHref: Resolve));

        Assert.Contains("<a href=\"../../../commit/abc1234.html\"><code>abc1234</code></a>", html);
        Assert.Contains("<code>def5678</code>", html);
        Assert.DoesNotContain("<a href=\"../../../commit/def5678", html);
    }

    [Fact]
    public void RenderPage_HistoryDateLink_GuardedOnDayPageExistence()
    {
        // 2026-07-03 has a day page; 2026-07-01 does not → plain date text, never a dead link.
        string? Resolve(DateOnly date) => date == new DateOnly(2026, 7, 3) ? "commits/2026-07-03.html" : null;

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: SampleInsight(), dayHref: Resolve));

        Assert.Contains("<a href=\"../../../commits/2026-07-03.html\">2026-07-03</a>", html);
        Assert.DoesNotContain("<a href=\"../../../commits/2026-07-01.html\"", html);
    }

    [Fact]
    public void RenderPage_Insight_EscapesAuthorSubjectAndPath()
    {
        var insight = new FileInsight(
            ChangeCount: 1,
            Contributors: new[] { ("A<b>&\"lice", 1) },
            CoupledFiles: new[]
            {
                new CoupledFile("src/<x>&.cs", Support: 1, Confidence: 1.0, Lift: null,
                    CrossBoundary: false, Kind: GitMetrics.CouplingKind.Code),
            },
            History: new[] { new CommitTouch("aaa1111", new DateOnly(2026, 7, 1), "E<v>il", "sub&<ject>\"") },
            TotalContributors: 1);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: insight));

        Assert.Contains("A&lt;b&gt;&amp;&quot;lice", html);
        Assert.Contains("src/&lt;x&gt;&amp;.cs", html);
        Assert.Contains("E&lt;v&gt;il", html);
        Assert.Contains("sub&amp;&lt;ject&gt;&quot;", html);
        // No raw metacharacters from the insight leak through.
        Assert.DoesNotContain("E<v>il", html);
    }

    [Fact]
    public void RenderPage_Insight_OmitsEmptySubPartsWithoutEmptyHeadings()
    {
        // Contributors present, but no coupling and no history → only the contributors + frequency parts render.
        var insight = new FileInsight(
            ChangeCount: 3,
            Contributors: new[] { ("Alice", 3) },
            CoupledFiles: Array.Empty<CoupledFile>(),
            History: Array.Empty<CommitTouch>(),
            TotalContributors: 1);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: insight));

        Assert.Contains("code-insights", html);
        Assert.Contains("Contributors to this file", html);
        Assert.DoesNotContain("Often changed with", html);
        Assert.DoesNotContain("Change history", html);
        Assert.DoesNotContain("code-history-table", html);
    }

    [Fact]
    public void RenderPage_Insight_DisclosesTruncatedContributorList()
    {
        // Shown list is capped at 2, but TotalContributors (12) says the file really has 12 — the page must not
        // let the capped list read as complete (code review addition, mirrors FileChangeStat.TotalContributors).
        var insight = new FileInsight(
            ChangeCount: 12,
            Contributors: new[] { ("Alice", 5), ("Bob", 4) },
            CoupledFiles: Array.Empty<CoupledFile>(),
            History: Array.Empty<CommitTouch>(),
            TotalContributors: 12);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: insight));

        Assert.Contains("+10 more contributors", html);
    }

    [Fact]
    public void RenderPage_Insight_OmitsMoreContributorsNoteWhenListIsComplete()
    {
        // TotalContributors equals the shown count — nothing was truncated, so no note.
        var insight = new FileInsight(
            ChangeCount: 2,
            Contributors: new[] { ("Alice", 2) },
            CoupledFiles: Array.Empty<CoupledFile>(),
            History: Array.Empty<CommitTouch>(),
            TotalContributors: 1);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: insight));

        Assert.DoesNotContain("code-insight-more", html);
    }

    // ---- Tab split: Insights | Relationships | History | Code, each iconed ----

    [Fact]
    public void RenderPage_FullData_RendersFourIconedTabsWithInsightsDefault()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        // Four tabs, in order, each with its modifier + a visible text label.
        foreach (var (mod, label) in new[] { ("insights", "Insights"), ("relationships", "Relationships"), ("history", "History"), ("source", "Code") })
        {
            Assert.Contains($"code-tab code-tab--{mod}", html);
            Assert.Contains($"code-tabpanel code-tabpanel--{mod}", html);
            Assert.Contains($"<span>{label}</span>", html);
        }

        // Each tab carries a decorative icon before its label — count within the tablist only.
        var tablist = Between(html, "code-tablist", "</fieldset>");
        Assert.Equal(4, CountOccurrences(tablist, "class=\"ss-icon\""));

        // Insights is the first tab and the only one checked (leads by default).
        var insightsTab = html.IndexOf("code-tab--insights", StringComparison.Ordinal);
        var relTab = html.IndexOf("code-tab--relationships", StringComparison.Ordinal);
        Assert.True(insightsTab >= 0 && insightsTab < relTab, "Insights must be the first tab");
        // Structural: exactly one radio carries the checked attribute (matching the input's closing bracket avoids
        // false hits on fixture text — an author or path containing the word "checked").
        Assert.Equal(1, CountOccurrences(html, " checked>"));
        // The checked attribute sits on the Insights radio (before the relationships tab appears).
        var checkedIndex = html.IndexOf("checked", StringComparison.Ordinal);
        Assert.True(checkedIndex > insightsTab && checkedIndex < relTab, "the checked radio must be the Insights tab");
    }

    [Fact]
    public void RenderPage_Graph_LivesInRelationshipsTabOnly_NotInsights()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        // The relationship graph renders exactly once, inside the relationships panel. Exactly-once matters more
        // than it used to: a second host would mean a second island id, and the client resolves its payload BY id.
        Assert.Equal(1, CountOccurrences(html, "class=\"chart-panel code-relationships\""));
        Assert.Equal(1, CountOccurrences(html, "data-relgraph></div>"));
        var insightsPanel = Between(html, "code-tabpanel--insights", "code-tabpanel--relationships");
        var relPanel = Between(html, "code-tabpanel--relationships", "code-tabpanel--history");
        Assert.DoesNotContain("code-relationships", insightsPanel);
        Assert.DoesNotContain("data-relgraph", insightsPanel);
        Assert.Contains("code-relationships", relPanel);
        Assert.Contains("data-relgraph", relPanel);
    }

    [Fact]
    public void RenderPage_History_LivesInHistoryTabOnly_NotInsights()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight()));

        // The history table renders exactly once, inside the history panel — not in Insights.
        Assert.Equal(1, CountOccurrences(html, "code-history-table"));
        var insightsPanel = Between(html, "code-tabpanel--insights", "code-tabpanel--relationships");
        var historyPanel = Between(html, "code-tabpanel--history", "code-tabpanel--source");
        Assert.DoesNotContain("code-history-table", insightsPanel);
        Assert.Contains("code-history-table", historyPanel);
        Assert.Contains("Change history", historyPanel);
    }

    [Fact]
    public void RenderPage_RefsOnlyNoInsight_ShowsRelationshipsAndCodeWithRelationshipsDefault()
    {
        // No deep-git insight → no Insights tab and no History tab. Relationships leads (first surviving tab).
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs));

        Assert.Contains("code-tab--relationships", html);
        Assert.Contains("code-tab--source", html);
        Assert.DoesNotContain("code-tab--insights", html);
        Assert.DoesNotContain("code-tab--history", html);
        // Exactly one checked radio, and it is the (leading) relationships tab.
        // Structural: exactly one radio carries the checked attribute (matching the input's closing bracket avoids
        // false hits on fixture text — an author or path containing the word "checked").
        Assert.Equal(1, CountOccurrences(html, " checked>"));
        var relTab = html.IndexOf("code-tab--relationships", StringComparison.Ordinal);
        var checkedIndex = html.IndexOf("checked", StringComparison.Ordinal);
        var sourceTab = html.IndexOf("code-tab--source", StringComparison.Ordinal);
        Assert.True(checkedIndex > relTab && checkedIndex < sourceTab, "relationships must be the default-checked tab");
    }

    [Fact]
    public void RenderPage_Uncited_NoInsight_RendersNoTabChrome()
    {
        // Only the source has content → no tabs at all; the source spans full width exactly as pre-tab.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav()));

        Assert.DoesNotContain("code-tabs", html);
        Assert.DoesNotContain("code-tablist", html);
        Assert.Contains("<section class=\"code-source-section\"", html);
    }

    // ---- reference-graph epic grouping + relationships: checkboxes, 4 variants, sr-only enumeration ----

    private static readonly (string OutputUrl, string Title, (int Number, string Title)? Epic)[] RefsWithEpics =
    {
        ("epics/story-7-1.html", "Story 7.1: In-Portal Code File Browsing", (7, "Code Insights")),
        ("epics/epic-8.html", "Epic 8: Dashboard Command Center", null),
    };

    [Fact]
    public void RenderPage_EpicGrouping_EmitsAnEpicHubNodeAndAFilteredMembershipEdge()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), RefsWithEpics));

        // Story 24.2 D3: "Group by epic" is no longer a pre-rendered variant. ONE layout is solved, the epic hub is
        // in it, and the filter governs whether its membership edge (and therefore the hub) is drawn.
        Assert.Contains("\"id\":\"epic7\"", html);
        Assert.Contains("\"k\":\"epic\"", html);
        Assert.Contains("Epic 7: Code Insights \\u2014 1 citing story.", html);
        Assert.Contains("\"e\":\"epic\",\"s\":\"epic\"", html);
        // The non-story citer (Epic 8 page, no resolved Epic) stays an ordinary artifact node with no hub.
        Assert.Contains("\"p\":\"Epic 8: Dashboard Command Center\"", html);
        Assert.DoesNotContain("\"id\":\"epic8\"", html);

        // The sr-only twin always discloses epic membership, regardless of the filter's state — that is what makes
        // it "complete" under ADR 0013 §2 when the drawn chart can hide the hub.
        Assert.Contains("(Epic 7: Code Insights)", html);
    }

    [Fact]
    public void RenderPage_ShowRelationships_StoryRelatedEdgeIsAFilteredCrossEdgeAndAlwaysInTheTwin()
    {
        var related = new[] { (0, 0) }; // story index 0 also cites related-file index 0
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight(),
            storyRelatedEdges: related));

        Assert.Contains("\"e\":\"xcite\",\"s\":\"cross\"", html);
        // The sentence itself is authored ONCE, as the kind's phrase, instead of on every edge.
        Assert.Contains("{\"k\":\"xcite\",\"f\":\"cross\",\"phrase\":\"{a} also cites {b}.\"}", html);
        // The sr-only equivalent enumerates the cross edge unconditionally — the filter cannot hide a fact.
        Assert.Contains("also cites src/SpecScribe/Other.cs", html);
    }

    [Fact]
    public void RenderPage_ShowRelationships_RelatedToRelatedEdgeIsAFilteredCrossEdge()
    {
        var pairEdges = new[] { (0, 1) }; // related-file index 0 <-> related-file index 1
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs, insight: SampleInsight(),
            relatedRelatedEdges: pairEdges));

        Assert.Contains("\"e\":\"xcouple\",\"s\":\"cross\"", html);
        Assert.Contains("{a} and {b} are themselves frequently co-changed.", html);
        Assert.Contains("also co-changed with", html);
    }

    [Fact]
    public void RenderPage_NoDeepGitInsight_EmitsNoFilterableEdgesAndThereforeNoControls()
    {
        // "--deep-git off / no FileInsight" degradation: no coupled population, no epic data, no cross edges. The
        // control bar disappears entirely rather than shipping two checkboxes that toggle nothing — the correction
        // to the retired card, which rendered both unconditionally.
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), Refs));

        Assert.DoesNotContain("ss-relgraph-controls", html);
        Assert.DoesNotContain("\"e\":\"epic\"", html);
        Assert.DoesNotContain("\"e\":\"xcite\"", html);
        Assert.DoesNotContain("\"e\":\"xcouple\"", html);
        Assert.DoesNotContain("\"k\":\"epic\",\"h\"", html);
        // The graph itself still renders — citations alone are a perfectly good ego graph.
        Assert.Contains("data-relgraph></div>", html);
    }

    [Fact]
    public void RenderPage_CrossEdges_OutOfRangeIndicesAreDroppedNotDrawnAgainstTheWrongNode()
    {
        // The two cross-edge builders are INDEX-ALIGNED with the citer list and the coupled list, and Story 24.2
        // widened the coupled cap — so an index that no longer resolves must be dropped, never silently rebound to
        // whatever node happens to sit at that ordinal.
        var ex = Record.Exception(() => JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: SampleInsight(),
            storyRelatedEdges: new[] { (99, 0), (0, 99), (-1, -1) },
            relatedRelatedEdges: new[] { (0, 99), (5, 5), (-2, 0) })));
        Assert.Null(ex);

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(
            RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs, insight: SampleInsight(),
            storyRelatedEdges: new[] { (99, 0), (0, 99) },
            relatedRelatedEdges: new[] { (0, 99), (5, 5) }));
        Assert.DoesNotContain("\"s\":\"cross\"", html);
    }

    [Fact]
    public void RenderPage_EpicGroupingAndRelationships_NeverThrowsWithoutInsightOrEpics()
    {
        // A minimal citer set with no epic info and no insight at all — the whole card must render without
        // throwing, exactly the graceful-degradation contract.
        var ex = Record.Exception(() =>
            JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "x" }, Nav(), Refs)));
        Assert.Null(ex);
    }

    [Fact]
    public void RenderPlaceholder_WithInsight_RendersInsightsAndHistoryTabs()
    {
        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPlaceholderPage(
            RepoRelative, OutputPath, "This file is too large to render inline.", Nav(),
            insight: SampleInsight()));

        Assert.Contains("class=\"code-placeholder\">This file is too large to render inline.</p>", html);
        Assert.DoesNotContain("class=\"code-line\"", html);
        Assert.Contains("code-tab--insights", html);
        Assert.Contains("code-tab--history", html);
        Assert.Contains("Changed in <strong>7</strong> commits", html);
        Assert.Contains("<table class=\"code-history-table\">", html);
        Assert.Contains("name=\"code-view-", html);
    }

    [Fact]
    public void SoftSlugify_PathSeparatorAndHyphen_ProduceDistinctSlugs()
    {
        var withSlash = CodeFileTemplater.SoftSlugify("code/a/b.html");
        var withHyphen = CodeFileTemplater.SoftSlugify("code/a-b.html");
        var literalX2f = CodeFileTemplater.SoftSlugify("code/ax2fb.html");

        Assert.NotEqual(withSlash, withHyphen);
        Assert.Equal("codex2fax2fb-html", withSlash);
        Assert.Equal("codex2fa-b-html", withHyphen);
        // Literal "x2f" is escaped before slash encoding, so it cannot collide with an encoded '/'.
        Assert.NotEqual(withSlash, literalX2f);
        Assert.Equal("codex2fax2fx2fb-html", literalX2f);
    }

    [Fact]
    public void RenderPage_TabGroupNames_DifferForSlashVsHyphenPaths()
    {
        var slash = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage("a/b.cs", "code/a/b.cs.html", new[] { "x" }, Nav(), Refs, insight: SampleInsight()));
        var hyphen = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage("a-b.cs", "code/a-b.cs.html", new[] { "x" }, Nav(), Refs, insight: SampleInsight()));

        Assert.Contains("name=\"code-view-codex2fax2fb-cs-html\"", slash);
        Assert.Contains("name=\"code-view-codex2fa-b-cs-html\"", hyphen);
        Assert.DoesNotContain("name=\"code-view-codex2fax2fb-cs-html\"", hyphen);
    }

    [Fact]
    public void RenderPage_WithPager_RoutesThroughSiteNavRenderWayfinding()
    {
        // Story 10.11: the sibling pager rides SiteNav.RenderWayfinding's coherent strip alongside the
        // breadcrumb, not the body's own header — confirms this non-PageView templater's call-site wiring.
        var pager = new EntityPager(
            new PagerLink("../code/a.cs.html", "a.cs"),
            new PagerLink("../code/c.cs.html", "c.cs"));

        var html = JsonSpaRenderAdapter.Shared.RenderContent(CodeFileTemplater.BuildPage(RepoRelative, OutputPath, new[] { "using System;" }, Nav(), pager: pager));

        Assert.Contains("<div class=\"page-wayfinding\">", html);
        var wrapperIdx = html.IndexOf("page-wayfinding", StringComparison.Ordinal);
        var crumbIdx = html.IndexOf("class=\"breadcrumb\"", StringComparison.Ordinal);
        var pagerIdx = html.IndexOf("class=\"entity-pager\"", StringComparison.Ordinal);
        Assert.True(wrapperIdx < crumbIdx && crumbIdx < pagerIdx, "expected wrapper, then breadcrumb, then pager");
    }

    /// <summary>The HTML slice between the first occurrence of <paramref name="startMarker"/> and the next occurrence
    /// of <paramref name="endMarker"/> — a coarse but reliable way to assert which tab panel a fragment lands in,
    /// since the panels render as ordered siblings.</summary>
    private static string Between(string html, string startMarker, string endMarker)
    {
        var start = html.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"marker not found: {startMarker}");
        var end = html.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0) end = html.Length;
        return html[start..end];
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
