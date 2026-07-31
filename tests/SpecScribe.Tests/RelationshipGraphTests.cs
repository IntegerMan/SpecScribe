using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for the Story 24.2 relationship-graph component: the ADR 0012 §2 contract (one
/// datasource, one control idiom, one framing block, one MANDATORY text twin), the ADR 0030 §5 non-colour emphasis
/// and its banding disclosure, and the derived asset flag.</summary>
public class RelationshipGraphTests
{
    private static RelationshipGraph.GraphNode Node(
        string id, RelationshipGraph.NodeKind kind, int weight = 1, double strength = 0, string? href = null) =>
        new(id, id, id, kind, href, weight, strength, id + " detail.");

    private static RelationshipGraph.RelationshipGraphModel Model(
        IReadOnlyList<RelationshipGraph.GraphNode>? nodes = null,
        IReadOnlyList<RelationshipGraph.GraphEdge>? edges = null,
        string twin = "<ul class=\"ref-list sr-only\"><li>twin</li></ul>\n") =>
        new(new Charts.ChartMeta("Relationships"), "relgraph-x",
            nodes ?? new[]
            {
                Node("focal", RelationshipGraph.NodeKind.Focal),
                Node("a", RelationshipGraph.NodeKind.Artifact, href: "a.html"),
                Node("c", RelationshipGraph.NodeKind.Coupled, weight: 4, strength: 0.8),
            },
            edges ?? new[]
            {
                new RelationshipGraph.GraphEdge(0, 1, RelationshipGraph.EdgeKind.Citation, 0, false, false, "cites"),
                new RelationshipGraph.GraphEdge(0, 2, RelationshipGraph.EdgeKind.Coupling, 4, false, false, "couples"),
            },
            twin);

    [Fact]
    public void Render_WithoutATextTwin_ThrowsRatherThanShippingAChartNobodyCanRead()
    {
        // ADR 0013 §2 makes the server-rendered twin the no-JS contract, not a nicety — it is what a JS-off,
        // blocked-bundle or assistive-technology reader actually gets. Enforcing it by construction is the whole
        // reason TwinHtml is a required member instead of an optional slot: a chart with no twin must fail at
        // GENERATION, loudly, rather than ship and be discovered by a reader who cannot see it.
        var ex = Assert.Throws<InvalidOperationException>(() => RelationshipGraph.Render(Model(twin: "   ")));

        Assert.Contains("text twin", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ADR 0013", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EmptyModel_EmitsNothingAtAll()
    {
        // NFR8: no island, no host, no inert controls, no empty framed panel promising a chart that is not coming.
        var empty = Model(nodes: Array.Empty<RelationshipGraph.GraphNode>(), edges: Array.Empty<RelationshipGraph.GraphEdge>());

        Assert.Equal(string.Empty, RelationshipGraph.Render(empty));
        Assert.Equal(string.Empty, RelationshipGraph.IslandHtml(empty));
    }

    [Fact]
    public void Render_EmitsTheComponentSkeletonInTheOrderTheHierarchyComponentEstablished()
    {
        var html = RelationshipGraph.Render(Model(), showCrossFilter: true);

        // controls -> boot placeholder -> host -> live region -> legend -> island -> twin, all inside Charts.Framed.
        var order = new[]
        {
            "ss-relgraph-controls",
            "ss-relgraph-booting",
            "data-relgraph></div>",
            "ss-relgraph-live",
            "ss-relgraph-legend",
            "<script type=\"application/json\"",
            "ref-list sr-only",
        };
        var last = -1;
        foreach (var needle in order)
        {
            var at = html.IndexOf(needle, StringComparison.Ordinal);
            Assert.True(at > last, $"'{needle}' is out of order in the emitted skeleton");
            last = at;
        }
        Assert.StartsWith("<div class=\"chart-panel\"", html, StringComparison.Ordinal);
        Assert.Contains("<h3>Relationships</h3>", html);
    }

    [Fact]
    public void Render_ControlBarIsHiddenAndOmittedEntirelyWhenNeitherFilterGovernsAnything()
    {
        // A JS-off reader must never see an inert control, and a checkbox that toggles nothing is inert even with
        // JS on.
        Assert.DoesNotContain("ss-relgraph-controls", RelationshipGraph.Render(Model()));
        Assert.Contains("<div class=\"ss-relgraph-controls\" hidden>", RelationshipGraph.Render(Model(), showEpicFilter: true));
    }

    [Fact]
    public void Island_CarriesTokenNamesNeverLiteralColours()
    {
        // ADR 0012 §6: presentation comes from SpecScribe's tokens resolved through the real cascade, never a
        // Plotly colorway — which is also what makes the graph follow a theme switch for free. And the --status-*
        // lifecycle tokens stay off code surfaces, a rule the retired SVG stated and this component keeps.
        var json = RelationshipGraph.IslandHtml(Model());

        Assert.Contains("\"tokens\":{", json);
        Assert.Contains("\"--gold\"", json);
        Assert.Contains("\"--ink-light\"", json);
        Assert.DoesNotContain("--status-", json);
        Assert.DoesNotContain("#", json);
        Assert.DoesNotContain("rgb(", json);
    }

    [Fact]
    public void Island_ResolvesEveryEdgeStyleServerSideSoTheLegendAndTheChartCannotDisagree()
    {
        var edges = new[]
        {
            new RelationshipGraph.GraphEdge(0, 1, RelationshipGraph.EdgeKind.Citation, 0, false, false, "cites"),
            new RelationshipGraph.GraphEdge(0, 2, RelationshipGraph.EdgeKind.Coupling, 10, true, false, "cross-boundary couple"),
            new RelationshipGraph.GraphEdge(0, 2, RelationshipGraph.EdgeKind.Coupling, 1, false, true, "process couple"),
        };
        var json = RelationshipGraph.IslandHtml(Model(edges: edges));

        Assert.Contains("\"styles\":[", json);
        // Cross-boundary takes a LONGER dash; process coupling takes a DOT pattern. Neither is a hue change.
        Assert.Contains("\"dash\":\"9px,4px\"", json);
        Assert.Contains("\"dash\":\"1.5px,3px\"", json);
        Assert.Contains("\"dash\":\"solid\"", json);
        // Widths are rounded, so no binary float artifact ships into the island.
        Assert.DoesNotContain("999999", json);
    }

    [Fact]
    public void Legend_DisclosesTheBandingAndOmitsChannelsThisInstanceDoesNotDraw()
    {
        // ADR 0030 §5. Trace-level line styling forces stroke width into bands; a legend that showed a continuous
        // scale beside a banded chart is the misdescribing-entry class Stories 10.7 and 21.1 each closed.
        var withCoupling = RelationshipGraph.LegendHtml(Model(edges: new[]
        {
            new RelationshipGraph.GraphEdge(0, 2, RelationshipGraph.EdgeKind.Coupling, 4, true, true, "x"),
            new RelationshipGraph.GraphEdge(0, 2, RelationshipGraph.EdgeKind.Coupling, 1, false, false, "y"),
        }));
        Assert.Contains($"banded into {RelationshipGraph.WidthBands} steps", withCoupling);
        Assert.Contains("not a continuous scale", withCoupling);
        Assert.Contains("Longer dashes mark a pair that crosses a directory boundary", withCoupling);
        Assert.Contains("Dotted spokes are process coupling", withCoupling);

        // Citations only: no coupling channels at all, so no rows pointing at zero edges.
        var citationsOnly = RelationshipGraph.LegendHtml(Model(
            nodes: new[] { Node("focal", RelationshipGraph.NodeKind.Focal), Node("a", RelationshipGraph.NodeKind.Artifact) },
            edges: new[] { new RelationshipGraph.GraphEdge(0, 1, RelationshipGraph.EdgeKind.Citation, 0, false, false, "c") }));
        Assert.Contains("gold circle on a solid spoke", citationsOnly);
        Assert.DoesNotContain("banded into", citationsOnly);
        Assert.DoesNotContain("Longer dashes", citationsOnly);
        Assert.DoesNotContain("Dotted spokes", citationsOnly);
    }

    [Fact]
    public void Legend_StatesThatConfidenceIsNotReadableFromTheDrawnThickness()
    {
        // The named ADR 0030 consequence, told to the READER and not only to the implementer: a banded stroke
        // cannot carry an exact confidence, so the legend says where the exact number actually is.
        var legend = RelationshipGraph.LegendHtml(Model());

        Assert.Contains("the drawn thickness is a band, not a reading", legend);
        Assert.Contains("full listing below", legend);
    }

    [Fact]
    public void Render_ShipsEveryChartDescribingElementHiddenSoAJsOffReaderSeesNoneOfIt()
    {
        // Found by the ADR 0013 §3 live JS-off audit, not by the suite — the whole class of defect this test now
        // guards. Everything that DESCRIBES the chart (the filter bar, the legend, the banding caveat) must ship
        // `hidden` and be revealed only on a successful mount, because with JS off there is no chart: the host is
        // display:none and the text twin IS the page. Eight legend rows explaining gold circles, dash patterns and
        // width bands were rendering above an empty box.
        var html = RelationshipGraph.Render(Model(), showEpicFilter: true);

        Assert.Contains("<div class=\"ss-relgraph-controls\" hidden>", html);
        Assert.Contains("<ul class=\"ss-relgraph-legend\" hidden>", html);
        Assert.Contains("<p class=\"ss-relgraph-legend-note\" hidden>", html);

        // The twin, by contrast, is NEVER hidden — it is the no-JS contract, not chrome describing one.
        Assert.Contains("ref-list sr-only", html);
        Assert.DoesNotContain("<ul class=\"ref-list sr-only\" hidden", html);
    }

    [Fact]
    public void ContainsHost_DerivesTheAssetFlagFromTheRenderedBody()
    {
        // A flag computed from the page cannot disagree with the page — the failure mode a hand-set boolean invites,
        // and the reason the hierarchy component does the same.
        Assert.True(RelationshipGraph.ContainsHost(RelationshipGraph.Render(Model())));
        Assert.False(RelationshipGraph.ContainsHost("<p>an ordinary page</p>"));
    }

    [Fact]
    public void Island_IsStableAcrossRepeatedSerialisation()
    {
        Assert.Equal(RelationshipGraph.IslandHtml(Model()), RelationshipGraph.IslandHtml(Model()));
    }
}
