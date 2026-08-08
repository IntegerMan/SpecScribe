using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SpecScribe;

/// <summary>The Story 24.2 per-file <b>ego coupling graph</b> component — the code page's single relationship
/// surface. A focal file pinned dead-centre, its citing artifacts and its most-coupled files on a relaxed ring
/// around it, drawn client-side by the already-vendored Plotly <c>scatter</c> trace over the generation-time
/// layout <see cref="CouplingLayout"/> computes (<see href="../../docs/adrs/0030-epic-24-graph-engine.md">ADR
/// 0030</see>).
///
/// <para><b>A sibling of <see cref="HierarchyExplorer"/>, not a reuse of it.</b> The data shape here is nodes +
/// edges, not a hierarchy: <c>HierarchyNode</c>'s <c>ParentId</c>/<c>Value</c> model cannot express an edge, and
/// forcing it to would produce a tree that lies about a graph. What IS identical — deliberately, because it is the
/// part that matters (ADR 0012 §2, §4) — is the <em>contract</em>: one datasource per instance, one control idiom,
/// one framing block, one mandatory text twin, tokens rather than an engine colorway, and determinism computed at
/// generation time.</para>
///
/// <para><b>The text twin is not optional and is not built here.</b> <see cref="RelationshipGraphModel.TwinHtml"/>
/// is required and <see cref="Render"/> throws on a blank one. The code page already ships the canonical twin —
/// Story 24.1 AC #3's sr-only ranked listing, which carries support, directional confidence, cross-boundary as
/// WORDS and lift on the row title — so this component takes that listing rather than emitting a second, poorer
/// one. ADR 0013 §2's contract is enforced by construction: an instance cannot render without one.</para>
///
/// <para><b>Emphasis is never carried by hue</b> (UX-DR17, ADR 0030 §5). Because Plotly's line style is a
/// TRACE-level attribute, per-edge styling means one trace per style class — which necessarily
/// <em>quantises stroke width into bands</em>. The legend therefore describes bands, because a legend showing a
/// continuous scale beside a banded chart is the misdescribing-entry class Stories 10.7 and 21.1 each closed.
/// <b>Confidence is consequently never encoded in stroke width alone</b>: it reads from the tooltip and from the
/// text twin, and the graph's own continuous channel for it is RADIUS — a stronger couple sits nearer the hub.</para>
/// [Story 24.2 Task 3]</summary>
public static class RelationshipGraph
{
    /// <summary>The chart host's opt-in marker — the ONE string naming the class ↔ script ↔ asset-flag contract, so
    /// no consumer re-types it. Mirrors <see cref="HierarchyExplorer.HostMarker"/>; deliberately its own family so
    /// the two components' CSS, purge registries and reveal handshakes cannot entangle.</summary>
    public const string HostMarker = "data-relgraph";

    /// <summary>Set on the PANEL by the client the moment a mount succeeds. Ends the boot placeholder and disarms
    /// the inline script's expiry timer.</summary>
    public const string MountedMarker = "data-relgraph-mounted";

    /// <summary>Set on the panel by the client when a mount DECLINES or throws, so the boot placeholder clears at
    /// once instead of the reader watching it until <see cref="BootTimeoutMs"/>. What stands behind a failed mount
    /// is the text twin, which <see cref="Render"/> emits regardless of mount outcome.</summary>
    public const string FailedMarker = "data-relgraph-failed";

    /// <summary>Marks a control whose toggling may REVEAL a zero-width graph host — the code page's tab radios.
    /// Plotly cannot lay out in a zero-width container and does not complain: it draws a chart of the wrong size.
    /// The Relationships panel is <c>display:none</c> at mount time whenever an Insights panel exists (the tabs are
    /// pure-CSS radios and the first tab is default-checked), so without this the graph mounts at zero width
    /// forever. Same mechanism <c>data-hierarchy-reveal</c> already implements, not a reinvention.</summary>
    public const string RevealMarker = "data-relgraph-reveal";

    /// <summary>How long the boot placeholder may stand before it gives up and hands the page back. Long enough for
    /// a 1.2 MB bundle to parse and plot on a slow machine, short enough that a blocked script is not mistaken for
    /// a slow one. Matches <see cref="HierarchyExplorer.BootTimeoutMs"/> — one perceived behaviour site-wide.</summary>
    public const int BootTimeoutMs = 5000;

    /// <summary>The cap on citing-artifact nodes DRAWN on the ring. Inherited unchanged from the retired
    /// <c>Charts.RefGraphArtifactNodeCap</c>, and for the same reason: a heavily-cited hub file would otherwise crowd
    /// the ring into illegibility, and Story 7.8's second (co-changed) population shares that ring.
    ///
    /// <para><b>It bounds what is DRAWN, never what is DISCLOSED.</b> The card's sr-only twin still enumerates every
    /// citer, and the overflow count is stated in the server-rendered ranking caption — so assistive technology and
    /// a JS-off reader both keep strictly more information than the graph shows, which is what ADR 0013 §2's
    /// "complete" requires. A seed value, not a contract.</para></summary>
    public const int ArtifactNodeCap = 14;

    /// <summary>Reserved height for the boot placeholder and the mounted host, in CSS pixels. Sized here rather
    /// than in the JS so the height is never a literal in the client.
    ///
    /// <para><b>Why 520 and not something smaller.</b> The aspect is locked to a circle, so the drawn area is a
    /// square of THIS side length however wide the panel is — which makes the height the sole budget for ring
    /// circumference. At 420 the live page put 40 markers (14 citing artifacts + 6 epic hubs + 20 coupled files, the
    /// D2 cap) on a ring whose innermost arc gave each of them ~20 px against markers up to 30 px wide: 20
    /// overlapping pairs, measured. Ring density is a function of the caps this story CHOSE, so the canvas has to be
    /// sized to them rather than to a round number.</para></summary>
    public const int Size = 520;

    /// <summary>The anti-flash handshake, injected by <see cref="HtmlRenderAdapter"/> BEFORE the page body so it
    /// runs while the body is still parsing. It lives on the CHROME seam rather than in this component's markup for
    /// the same hard reason <see cref="HierarchyExplorer.BootScript"/> does: the webview and SPA surfaces consume
    /// <see cref="PageView.BodyHtml"/> directly and must carry NO script.
    ///
    /// <para>The expiry is what keeps hide-first honest. If the bundle is blocked or missing, nothing ever mounts —
    /// and a hide-first with no timeout would leave a permanent "Initializing…" over nothing. So the marker is
    /// removed from any panel that has neither mounted nor reported failure, and the text twin is simply the
    /// page.</para></summary>
    public static readonly string BootScript =
        "<script>(function(){var r=document.documentElement;r.setAttribute('data-ss-relgraph-boot','1');"
        + $"setTimeout(function(){{r.removeAttribute('data-ss-relgraph-boot');}},{BootTimeoutMs});}})();</script>\n";

    /// <summary>The host div's closing signature — <see cref="HostMarker"/> as the LAST attribute, immediately before
    /// the tag closes. This, not the bare marker, is what <see cref="ContainsHost"/> matches.
    ///
    /// <para><b>Why the bare marker was not safe.</b> <c>data-relgraph</c> is a PREFIX of seven sibling attributes
    /// (<c>-panel</c>, <c>-filter</c>, <c>-reveal</c>, <c>-mounted</c>, <c>-failed</c>, <c>-href</c>, <c>-ready</c>),
    /// and — the reachable half — a code page embeds the file's own text through <c>BuildSource</c>, so every page
    /// rendering this component's own source, or <c>specscribe.js</c>, or <c>specscribe.css</c>, contained the
    /// literal string and claimed a host it did not have. That page would then pull the 1.2 MB engine bundle and
    /// emit the boot handshake for a chart that does not exist, against the byte-identical contract
    /// <see cref="AssetManifest"/> states. Escaping is what makes the narrow match sound: <c>BuildSource</c> runs
    /// source text through <c>PathUtil.Html</c>, so an embedded <c>&gt;</c> arrives as <c>&amp;gt;</c> and cannot
    /// complete this signature. [code review 24.2]</para></summary>
    internal const string HostSignature = HostMarker + ">";

    /// <summary>Whether a rendered body carries a relationship-graph host. The producer of an
    /// <see cref="AssetManifest"/> calls this over the FINISHED body, mirroring
    /// <see cref="HierarchyExplorer.ContainsHost"/> and <c>Mermaid.ContainsBlock</c> — a flag derived from the page
    /// cannot disagree with the page.</summary>
    public static bool ContainsHost(string bodyHtml) =>
        bodyHtml.Contains(HostSignature, StringComparison.Ordinal);

    // -----------------------------------------------------------------------------------------------------------
    // Model
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>What a node IS. Shape and edge vocabulary are keyed off this, preserving the Story 7.1/7.8 reading
    /// the retired SVG established (owner decision D1: "today's shape/edge vocabulary is preserved").</summary>
    public enum NodeKind
    {
        /// <summary>The file whose page this is. Pinned at the centre; exactly one per instance.</summary>
        Focal,
        /// <summary>A citing artifact — story/epic/ADR/doc. Gold circle on a solid spoke.</summary>
        Artifact,
        /// <summary>An epic hub grouping citing stories. Neutral square; its edges are what the "Group by epic"
        /// filter shows and hides.</summary>
        EpicHub,
        /// <summary>A file this file most often changes alongside. Neutral diamond on a dashed spoke.</summary>
        Coupled,
    }

    /// <summary>What an edge MEANS — and, because the two filters are edge-visibility filters (owner decision D3),
    /// also which filter governs it and which server-authored phrase describes it.</summary>
    public enum EdgeKind
    {
        /// <summary>focal → citing artifact. Always visible.</summary>
        Citation,
        /// <summary>focal → coupled file. Always visible.</summary>
        Coupling,
        /// <summary>citing artifact → its epic hub. Governed by "Group by epic".</summary>
        EpicMembership,
        /// <summary>citing artifact ↔ a coupled file that artifact ALSO cites. Governed by "Show relationships".</summary>
        CrossCitation,
        /// <summary>coupled file ↔ coupled file, when that pair is itself frequently co-changed. Governed by
        /// "Show relationships".</summary>
        CrossCoupling,
    }

    /// <summary>The per-kind phrase describing an edge, with <c>{a}</c>/<c>{b}</c> standing for its two endpoints'
    /// titles. <b>The wording lives here, in C#, exactly as every other string on this surface does</b> — the client
    /// only substitutes two values it is already holding, which is a different thing from the client inventing
    /// prose.
    ///
    /// <para><b>Why templates rather than a composed sentence per edge.</b> Measured on this repository's
    /// <c>Charts.cs</c> code page: the fully-composed form put the island at <b>55,012 B</b>, of which
    /// <b>30,820 B — 56% — was 203 cross-edge sentences</b>, each re-spelling two full repository paths that were
    /// already in the node array a few hundred bytes earlier. That is pure repetition, and it repeats again on every
    /// code page in the portal. A coupling spoke keeps its own composed sentence (see
    /// <see cref="GraphEdge.Detail"/>) because its numbers are NOT derivable from its endpoints.</para></summary>
    private static string? PhraseFor(EdgeKind kind) => kind switch
    {
        EdgeKind.Citation => "{a} cites {b}.",
        EdgeKind.EpicMembership => "{a} belongs to {b}.",
        EdgeKind.CrossCitation => "{a} also cites {b}.",
        EdgeKind.CrossCoupling => "{a} and {b} are themselves frequently co-changed.",
        _ => null,
    };

    private static string KindKey(EdgeKind kind) => kind switch
    {
        EdgeKind.Citation => "cite",
        EdgeKind.Coupling => "couple",
        EdgeKind.EpicMembership => "epic",
        EdgeKind.CrossCitation => "xcite",
        EdgeKind.CrossCoupling => "xcouple",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unrecognized RelationshipGraph.EdgeKind."),
    };

    /// <summary>One node. <paramref name="Weight"/> drives marker size (shared commits for a coupled file, member
    /// count for an epic hub, the file's own citation count for the focal node); <paramref name="Strength"/> is the
    /// 0..1 pull toward the hub <see cref="CouplingLayout"/> turns into radius. <paramref name="Detail"/> is the
    /// already-composed prose the tooltip and the accessible name both use — composed HERE, in C#, so the graph's
    /// wording cannot drift from the twin's by living in two languages.</summary>
    public sealed record GraphNode(
        string Id,
        string Label,
        string Title,
        NodeKind Kind,
        string? Href,
        int Weight,
        double Strength,
        string Detail);

    /// <summary>One edge, by node ORDINAL into the model's own node list.
    ///
    /// <para><paramref name="Detail"/> is the server-composed hover/accessible text, and it is <b>null for every
    /// kind whose description is derivable from its two endpoints</b> — those take <see cref="PhraseFor"/>'s
    /// template instead, so the sentence is authored once rather than repeated per edge. A
    /// <see cref="EdgeKind.Coupling"/> spoke supplies it, because support/confidence/lift are facts about the PAIR
    /// and cannot be recovered from either endpoint alone.</para></summary>
    public sealed record GraphEdge(
        int A,
        int B,
        EdgeKind Kind,
        int Support,
        bool CrossBoundary,
        bool ProcessCoupling,
        string? Detail);

    /// <summary>A complete instance. <paramref name="TwinHtml"/> is the mandatory server-rendered text equivalent
    /// (ADR 0013 §2) — supplied by the call site because the code page's own sr-only listing already IS the
    /// canonical one; <see cref="Render"/> refuses to emit a chart without it.</summary>
    public sealed record RelationshipGraphModel(
        Charts.ChartMeta Meta,
        string DomId,
        IReadOnlyList<GraphNode> Nodes,
        IReadOnlyList<GraphEdge> Edges,
        string TwinHtml);

    // -----------------------------------------------------------------------------------------------------------
    // Edge style classes — the ONE table the payload, the client and the legend all read
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>How many stroke-width bands the coupling spokes are quantised into. <b>Three, and the legend says
    /// three</b>: Plotly's <c>line</c> is a trace-level attribute, so a continuous width would need one trace per
    /// distinct support value. Naming the quantisation is ADR 0030 §5's requirement, not a limitation being
    /// hidden.</summary>
    public const int WidthBands = 3;

    /// <summary>One resolved edge style: a dash signature, a stroke width and a COLOUR TOKEN NAME. The token name
    /// (never a literal colour) is what the client resolves through the real cascade, so the graph is painted by
    /// SpecScribe's tokens and never by a Plotly colorway (ADR 0012 §6). Only the neutral ink/gold/border family
    /// appears here — the <c>--status-*</c> lifecycle tokens are off-limits on code surfaces, a rule the retired
    /// <c>Charts.ReferenceGraph</c> stated and this component keeps.</summary>
    private readonly record struct EdgeStyle(string Key, string Dash, double Width, string Token);

    /// <summary>Resolves an edge to its style class key. The classification lives server-side so the legend, the
    /// payload and the drawn chart cannot disagree — the client never re-derives a style.</summary>
    private static string StyleKeyFor(GraphEdge e, int maxSupport)
    {
        switch (e.Kind)
        {
            case EdgeKind.Citation: return "cite";
            case EdgeKind.EpicMembership: return "epic";
            case EdgeKind.CrossCitation:
            case EdgeKind.CrossCoupling: return "cross";
            default:
                var band = WidthBandFor(e.Support, maxSupport);
                var kind = e.ProcessCoupling ? "proc" : "code";
                var boundary = e.CrossBoundary ? "xb" : "in";
                return string.Create(CultureInfo.InvariantCulture, $"couple-{kind}-{boundary}-{band}");
        }
    }

    /// <summary>Support → width band. Linear over the instance's own maximum, so a file whose couples are all weak
    /// still shows relative differences rather than three empty bands.</summary>
    private static int WidthBandFor(int support, int maxSupport)
    {
        if (maxSupport <= 1) return 0;
        var t = (double)(support - 1) / (maxSupport - 1);
        var band = (int)Math.Floor(t * WidthBands);
        return Math.Clamp(band, 0, WidthBands - 1);
    }

    /// <summary>The style for a key. Dash signatures reproduce the retired SVG's vocabulary exactly (owner decision
    /// D1 keeps the shape/edge language): solid citation spoke, <c>4 3</c> dashed coupling spoke, <c>1.5 2.5</c>
    /// dotted epic spoke, <c>5 2 1 2</c> dash-dot cross edge. Cross-boundary coupling takes a LONGER dash rather
    /// than a different colour, and process coupling takes a DOT pattern — both non-colour channels, both also
    /// spelled out in words in the tooltip and the twin.</summary>
    private static EdgeStyle StyleFor(string key)
    {
        switch (key)
        {
            case "cite": return new EdgeStyle(key, "solid", 1.5, "--border");
            case "epic": return new EdgeStyle(key, "1.5px,2.5px", 1.2, "--ink-light");
            case "cross": return new EdgeStyle(key, "5px,2px,1px,2px", 1, "--ink-light");
            default:
                // couple-{code|proc}-{in|xb}-{band}
                var parts = key.Split('-');
                var proc = parts.Length > 1 && string.Equals(parts[1], "proc", StringComparison.Ordinal);
                var xb = parts.Length > 2 && string.Equals(parts[2], "xb", StringComparison.Ordinal);
                var band = parts.Length > 3 && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)
                    ? Math.Clamp(b, 0, WidthBands - 1)
                    : 0;
                var dash = proc
                    ? (xb ? "2px,4px" : "1.5px,3px")
                    : (xb ? "9px,4px" : "4px,3px");
                // Rounded, because `1.2 + 2 * 0.7` is 2.5999999999999996 in binary floating point and that string
                // would ship into the island verbatim — 16 wasted bytes per style class, on a surface that repeats
                // across every code page, for a width no display can tell apart from 2.6.
                return new EdgeStyle(key, dash, Math.Round(1.2 + band * 0.7, 2), "--ink-light");
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    // Render
    // -----------------------------------------------------------------------------------------------------------

    /// <summary>Renders the complete relationship-graph block: the Story 10.2 framing panel (via
    /// <see cref="Charts.Framed"/>), the <c>hidden</c> control bar carrying the two filters, the boot placeholder,
    /// the empty chart host, the polite live region, the legend, the payload island, and the text twin — in that
    /// order, mirroring <see cref="HierarchyExplorer.Render"/>'s emitted skeleton so one shape serves both
    /// components.
    ///
    /// <para>Returns "" for a model with no nodes: no island, no host, no inert controls (NFR8). A file with
    /// couples but no citers, or citers but no couples, is a perfectly good instance and renders normally.</para>
    ///
    /// <para><paramref name="showEpicFilter"/>/<paramref name="showCrossFilter"/> suppress a control whose edge
    /// population is empty. A checkbox that toggles nothing is exactly the inert control the <c>hidden</c> bar
    /// exists to prevent — and the retired SVG's card shipped both checkboxes unconditionally, so this is a
    /// deliberate correction rather than a port.</para></summary>
    /// <exception cref="InvalidOperationException">The model carries nodes but no text twin. ADR 0013 §2 makes the
    /// twin the contract, not a nicety, so a missing one fails loudly at generation rather than shipping a chart
    /// that no JS-off or assistive-technology reader can read.</exception>
    public static string Render(
        RelationshipGraphModel model,
        string panelClass = "chart-panel",
        string panelAttributes = "",
        bool showEpicFilter = false,
        bool showCrossFilter = false)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Nodes.Count == 0) return string.Empty;

        if (string.IsNullOrWhiteSpace(model.TwinHtml))
        {
            throw new InvalidOperationException(
                $"RelationshipGraph instance '{model.DomId}' has {model.Nodes.Count} nodes but no text twin. " +
                "ADR 0013 §2 makes the server-rendered twin the no-JS contract for every chart surface — it is " +
                "what a JS-off, blocked-bundle or assistive-technology reader gets — so an instance cannot render " +
                "without one. Pass the surface's own listing as RelationshipGraphModel.TwinHtml.");
        }

        var id = PathUtil.Html(model.DomId);
        var body = new StringBuilder();

        // --- Control bar. Emitted `hidden`: both filters need script, so with JS off these would be inert
        // controls. Same convention `ss-hierarchy-controls` and `codemap-controls` already follow — the component
        // reveals the bar on a successful mount. Owner decision D3: BOTH toggles survive, as client-side
        // edge-visibility filters over the ONE solved layout. They hide; they never re-lay-out (ADR 0030 §4).
        if (showEpicFilter || showCrossFilter)
        {
            body.Append("<div class=\"ss-relgraph-controls\" hidden>\n");
            if (showEpicFilter)
            {
                body.Append($"  <input type=\"checkbox\" id=\"{id}-filter-epic\" class=\"ss-relgraph-filter\" data-relgraph-filter=\"epic\">");
                body.Append($"<label for=\"{id}-filter-epic\" class=\"ss-relgraph-filter-label\">Group by epic</label>\n");
            }
            if (showCrossFilter)
            {
                body.Append($"  <input type=\"checkbox\" id=\"{id}-filter-cross\" class=\"ss-relgraph-filter\" data-relgraph-filter=\"cross\">");
                body.Append($"<label for=\"{id}-filter-cross\" class=\"ss-relgraph-filter-label\">Show relationships</label>\n");
            }
            body.Append("</div>\n");
        }

        // --- Boot placeholder, sized to the chart it stands in for so the swap costs no reflow.
        body.Append($"<div class=\"ss-relgraph-booting\" role=\"status\" style=\"min-height:{Size.ToString(CultureInfo.InvariantCulture)}px\">")
            .Append("<span>Initializing graph&hellip;</span></div>\n");

        // --- Chart host. EMPTY at render time and `display:none` until the component reveals it. Reserving its
        // height server-side would leave a JS-off visitor staring at a blank box the size of a chart that is never
        // coming; the client sets the height from `config.size` at mount, so a JS-on page still does not reflow.
        body.Append($"<div class=\"ss-relgraph\" id=\"{id}\" {HostMarker}></div>\n");
        body.Append("<div class=\"ss-relgraph-live sr-only\" aria-live=\"polite\"></div>\n");

        body.Append(LegendHtml(model));
        body.Append(IslandHtml(model));
        body.Append(model.TwinHtml);

        return Charts.Framed(model.Meta, body.ToString(), panelClass, panelAttributes);
    }

    /// <summary>The component's own legend — AC #1's "one framing block", and the place ADR 0030 §5's banding is
    /// disclosed rather than hidden.
    ///
    /// <para><b>It describes the channels actually on screen, and only those.</b> Entries are emitted from what the
    /// model ACTUALLY carries — no epic entry without an epic hub, no process-coupling entry on a repository whose
    /// couples are all code-to-code — so a legend row can never point at zero edges. That is the phantom-entry
    /// class Stories 10.7 and 21.1 each closed.</para>
    ///
    /// <para><b>Every entry names a non-colour channel</b> (shape, dash, width band, distance) in prose, so the
    /// whole reading survives with colour removed (UX-DR17) and survives with the CHART removed, since the same
    /// facts are in the twin.</para></summary>
    internal static string LegendHtml(RelationshipGraphModel model)
    {
        var hasArtifacts = model.Nodes.Any(n => n.Kind == NodeKind.Artifact);
        var hasCoupled = model.Nodes.Any(n => n.Kind == NodeKind.Coupled);
        var hasEpics = model.Nodes.Any(n => n.Kind == NodeKind.EpicHub);
        var hasCross = model.Edges.Any(e => e.Kind is EdgeKind.CrossCitation or EdgeKind.CrossCoupling);
        var hasProcess = model.Edges.Any(e => e.Kind == EdgeKind.Coupling && e.ProcessCoupling);
        var hasBoundary = model.Edges.Any(e => e.Kind == EdgeKind.Coupling && e.CrossBoundary);
        var couplingEdges = model.Edges.Count(e => e.Kind == EdgeKind.Coupling);

        var sb = new StringBuilder();
        // Emitted `hidden`, revealed by the component on a successful mount — the same handshake the control bar
        // takes, for the same reason. A legend describes a CHART, and on this surface the chart only exists once
        // the client draws it; with JS off the text twin carries the information and a key to a picture nobody can
        // see is chrome for nothing. Caught in the JS-off audit: eight legend rows explaining gold circles, dash
        // patterns and width bands were rendering on a page whose chart host is `display:none`.
        sb.Append("<ul class=\"ss-relgraph-legend\" hidden>\n");
        sb.Append("  <li class=\"ss-relgraph-legend-item\"><span class=\"ss-relgraph-swatch ss-relgraph-swatch-focal\" aria-hidden=\"true\"></span>This file, at the centre</li>\n");
        if (hasArtifacts)
            sb.Append("  <li class=\"ss-relgraph-legend-item\"><span class=\"ss-relgraph-swatch ss-relgraph-swatch-artifact\" aria-hidden=\"true\"></span>Citing artifact &#8212; gold circle on a solid spoke</li>\n");
        if (hasEpics)
            sb.Append("  <li class=\"ss-relgraph-legend-item\"><span class=\"ss-relgraph-swatch ss-relgraph-swatch-epic\" aria-hidden=\"true\"></span>Epic hub &#8212; neutral square on a dotted spoke</li>\n");
        if (hasCoupled)
        {
            sb.Append("  <li class=\"ss-relgraph-legend-item\"><span class=\"ss-relgraph-swatch ss-relgraph-swatch-coupled\" aria-hidden=\"true\"></span>Co-changed file &#8212; neutral diamond on a dashed spoke, drawn nearer the centre the stronger the coupling</li>\n");
            // The banding disclosure. Named as a band count, never as a scale: the chart cannot draw a continuous
            // width, so a legend claiming one would misdescribe it (ADR 0030 §5).
            if (couplingEdges > 1)
                sb.Append($"  <li class=\"ss-relgraph-legend-item\"><span class=\"ss-relgraph-swatch ss-relgraph-swatch-band\" aria-hidden=\"true\"></span>Spoke thickness is banded into {WidthBands.ToString(CultureInfo.InvariantCulture)} steps by shared commits &#8212; not a continuous scale</li>\n");
            if (hasBoundary)
                sb.Append("  <li class=\"ss-relgraph-legend-item\"><span class=\"ss-relgraph-swatch ss-relgraph-swatch-boundary\" aria-hidden=\"true\"></span>Longer dashes mark a pair that crosses a directory boundary</li>\n");
            if (hasProcess)
                sb.Append("  <li class=\"ss-relgraph-legend-item\"><span class=\"ss-relgraph-swatch ss-relgraph-swatch-process\" aria-hidden=\"true\"></span>Dotted spokes are process coupling &#8212; config, lockfile, build-output or stylesheet upkeep rather than a code dependency</li>\n");
        }
        if (hasCross)
            sb.Append("  <li class=\"ss-relgraph-legend-item\"><span class=\"ss-relgraph-swatch ss-relgraph-swatch-cross\" aria-hidden=\"true\"></span>Dash-dot edges relate two ring items to each other</li>\n");
        sb.Append("</ul>\n");
        // The consequence ADR 0030 names, stated to the reader rather than only to the implementer: the precise
        // confidence figure is not recoverable from a banded stroke, so it is given where it IS exact. Hidden with
        // the legend it belongs to — it is a caveat about a drawn channel.
        sb.Append("<p class=\"ss-relgraph-legend-note\" hidden>Exact confidence and shared-commit counts for every pair are in the full listing below and in each spoke&#8217;s hover text &#8212; the drawn thickness is a band, not a reading.</p>\n");
        return sb.ToString();
    }

    /// <summary>The inline JSON island — the component's ONLY data source. No fetch, so it is <c>file://</c>-safe
    /// and survives the SPA's content capture.
    ///
    /// <para>It carries the component CONFIG alongside nodes and edges (ADR 0013 §5), and it carries the resolved
    /// EDGE STYLE TABLE, so the client never re-derives a style the legend already described. Colours travel as
    /// TOKEN NAMES, resolved by the client through the real cascade — the graph is painted by SpecScribe's tokens,
    /// never by a Plotly colorway (ADR 0012 §6), and it therefore follows a theme switch for free.</para>
    ///
    /// <para><see cref="JsonSerializer"/>'s default encoder escapes <c>&lt; &gt; &amp;</c>, so the payload is safe
    /// to embed directly in a <c>&lt;script&gt;</c> element — the same reasoning
    /// <see cref="HierarchyExplorer.IslandHtml"/>'s non-dimension branch relies on. This island carries no HTML
    /// field, so it takes the default encoder rather than the relaxed one.</para></summary>
    /// <summary>Null-skipping serialization for this island. Every derivable edge carries a null
    /// <see cref="GraphEdge.Detail"/> and every hub-less node a null <c>href</c>; emitting <c>"t":null</c> and
    /// <c>"h":null</c> on hundreds of edges per code page, across every code page in the portal, is bytes spent on
    /// nothing. The client reads a missing key and an explicit null identically.</summary>
    private static readonly JsonSerializerOptions EdgeCompactJson = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string IslandHtml(RelationshipGraphModel model)
    {
        if (model.Nodes.Count == 0) return string.Empty;

        var maxSupport = 1;
        foreach (var e in model.Edges)
        {
            if (e.Kind == EdgeKind.Coupling && e.Support > maxSupport) maxSupport = e.Support;
        }

        // Style keys in first-appearance order, then the resolved table. First-appearance rather than a dictionary
        // walk so the emitted order is a pure function of the edge list (see CouplingLayout's remarks on why
        // dictionary order must never reach a rendered artifact).
        var styleKeys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var edgeStyleKeys = new string[model.Edges.Count];
        for (var i = 0; i < model.Edges.Count; i++)
        {
            var key = StyleKeyFor(model.Edges[i], maxSupport);
            edgeStyleKeys[i] = key;
            if (seen.Add(key)) styleKeys.Add(key);
        }

        var positions = CouplingLayout.Solve(
            model.Nodes.Select(n => new CouplingLayout.LayoutNode(n.Id, n.Kind == NodeKind.Focal, n.Strength)).ToList(),
            model.Edges.Select(e => new CouplingLayout.LayoutEdge(e.A, e.B)).ToList());

        // The FOCAL node is excluded from the weight normalisation, and drawn at a fixed hub size by the client.
        // Including it would let the hub's own degree set the scale that every ring marker is measured against, so
        // a widely-connected file would flatten its own ring into uniform dots — and the hub would then read as
        // merely the biggest node rather than as the thing the graph is about (owner decision D1).
        var maxWeight = 1;
        foreach (var n in model.Nodes)
        {
            if (n.Kind == NodeKind.Focal) continue;
            if (n.Weight > maxWeight) maxWeight = n.Weight;
        }

        var payload = new
        {
            config = new
            {
                domId = model.DomId,
                // The framed title travels with the payload so the chart's accessible name is the SAME string the
                // visible heading uses, rather than the client inventing a second name for the panel.
                title = model.Meta.Title,
                size = Size,
                maxWeight,
                // Token NAMES, never literals (ADR 0012 §6).
                tokens = new
                {
                    focal = "--gold",
                    artifact = "--gold",
                    epic = "--ink",
                    coupled = "--ink-light",
                    surface = "--parchment",
                    ink = "--ink",
                    border = "--border",
                },
                // The per-KIND table: which filter governs an edge of this kind (null = always visible) and the
                // server-authored phrase describing it. One row per kind rather than two fields per edge — the
                // facts are properties of the KIND, and repeating them 250 times per code page was measured, not
                // guessed (see PhraseFor).
                kinds = new[]
                {
                    EdgeKind.Citation, EdgeKind.Coupling, EdgeKind.EpicMembership,
                    EdgeKind.CrossCitation, EdgeKind.CrossCoupling,
                }.Select(k => new { k = KindKey(k), f = FilterKey(k), phrase = PhraseFor(k) }).ToList(),
            },
            styles = styleKeys.Select(k =>
            {
                var s = StyleFor(k);
                return new { k = s.Key, dash = s.Dash, w = s.Width, tok = s.Token };
            }).ToList(),
            nodes = model.Nodes.Select((n, i) => new
            {
                id = n.Id,
                l = n.Label,
                p = n.Title,
                // Rounded through the ONE invariant-culture formatter; see CouplingLayout.Format for why 4 decimals
                // is a deliberate DATA decision here and why confidence deliberately does not take this path.
                x = CouplingLayout.Format(positions[i].X),
                y = CouplingLayout.Format(positions[i].Y),
                k = KindKey(n.Kind),
                h = n.Href,
                w = n.Weight,
                // The one composed sentence the tooltip, the accessible name and the live region all use.
                t = n.Detail,
            }).ToList(),
            edges = model.Edges.Select((e, i) => new
            {
                a = e.A,
                b = e.B,
                // Kind (which drives filter + phrase, via `config.kinds`) and the resolved style class.
                e = KindKey(e.Kind),
                s = edgeStyleKeys[i],
                // Present ONLY when the sentence is not derivable from the endpoints — i.e. on coupling spokes,
                // whose numbers are facts about the pair. Null-skipped so the other kinds cost nothing.
                t = e.Detail,
            }).ToList(),
        };

        var json = JsonSerializer.Serialize(payload, EdgeCompactJson);
        return $"<script type=\"application/json\" id=\"{PathUtil.Html(model.DomId)}-data\">{json}</script>\n";
    }

    private static string KindKey(NodeKind kind) => kind switch
    {
        NodeKind.Focal => "focal",
        NodeKind.Artifact => "artifact",
        NodeKind.EpicHub => "epic",
        NodeKind.Coupled => "coupled",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unrecognized RelationshipGraph.NodeKind."),
    };

    /// <summary>Which client-side filter governs an edge kind, or null for "always visible". Owner decision D3's
    /// two toggles are exactly these two keys — nothing in the client knows what the words mean, only that a
    /// control publishes a key that matches one here.</summary>
    private static string? FilterKey(EdgeKind kind) => kind switch
    {
        EdgeKind.EpicMembership => "epic",
        EdgeKind.CrossCitation or EdgeKind.CrossCoupling => "cross",
        _ => null,
    };
}
