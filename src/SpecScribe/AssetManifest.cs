namespace SpecScribe;

/// <summary>The shared assets a page needs, as host-neutral data — the AD-2 "asset manifest" a delivery adapter
/// wires into its host's head. It NAMES the stylesheet and script hrefs (output-relative, already carrying the
/// caller's <c>../</c> prefix, WITHOUT the build cache-bust token — <see cref="PathUtil.RenderHeadOpen"/> still
/// owns appending <c>?v=</c>) and flags whether the page carries a mermaid diagram (so the client init module is
/// injected only when one landed). It deliberately models WHICH assets, not HOW a host themes them — host-aware
/// theming / VS Code chrome variables are Story 6.5 (AD-7), delivered webview-side as a separate inline theme
/// layer (see <see cref="WebviewRenderAdapter"/>), not through this manifest. [Story 6.1]</summary>
public sealed record AssetManifest
{
    /// <summary>The output-relative stylesheet href (prefix applied, no <c>?v=</c>).</summary>
    public required string StylesheetHref { get; init; }

    /// <summary>The output-relative enhancement-script href (prefix applied, no <c>?v=</c>).</summary>
    public required string ScriptHref { get; init; }

    /// <summary>Whether this page carries at least one mermaid diagram block and therefore needs the client-side
    /// init module. The producer computes it from the rendered body (<see cref="Mermaid.ContainsBlock"/>), so
    /// the flag can never disagree with what the page actually contains.</summary>
    public required bool MermaidNeeded { get; init; }

    /// <summary>Whether this page carries at least one Hierarchy Explorer (Story 20.5) and therefore needs the
    /// vendored plotly.js bundle. Computed by the producer from the RENDERED BODY
    /// (<see cref="HierarchyExplorer.ContainsHost"/>), exactly like <see cref="MermaidNeeded"/> — so the flag can
    /// never disagree with what the page actually contains, which is the failure mode a hand-set boolean invites.
    /// <para>Optional with a <c>false</c> default rather than <c>required</c>: every page that does not host a
    /// hierarchy chart must stay byte-identical, and 1.2 MB is not a rounding error.</para></summary>
    public bool HierarchyEngineNeeded { get; init; }

    /// <summary>Whether <see cref="HierarchyExplorer.BootScript"/> is emitted INLINE, between the wayfinding band
    /// and the body. Split from <see cref="HierarchyEngineNeeded"/> by Story 23.4 because the two placements are
    /// genuinely independent and BOTH ship today: the dashboard/epics families emit the boot marker inline (its
    /// anti-flash handshake has to run while the body is still parsing), while the Impact Map — the newer Story
    /// 21.3 convention — puts the SAME marker in <see cref="ExtraHead"/>, and the Code Map emits no boot marker at
    /// all and only pulls the engine. One flag could not express three shapes, and collapsing them would have
    /// moved bytes on pages this story must leave untouched.
    /// <para>Either placement is chrome-level and therefore OUTSIDE the IR content region
    /// (<see cref="JsonSpaRenderAdapter.RenderContent"/> composes nav + wayfinding + body only) — which is why
    /// <c>IrSurface.vue</c> re-emits it from the head. [Story 23.4 AC #3; Trap 3]</para>
    ///
    /// <para>⚠️ <b>WRITE-ONLY since Story 23.6. Nothing reads this.</b> [Story 23.4 code review, finding F-6]
    /// Its only consumer was <c>HtmlRenderAdapter.Render</c>, deleted with the C# page writer. The rendered site
    /// now derives the boot decision structurally on the Nuxt side — <c>web/ir/adapter.ts</c>'s
    /// <c>chromeNeeds()</c> tests the region for a <c>data-hierarchy</c> attribute, and
    /// <c>IrSurface.vue</c> injects the boot script on that one flag — so the three-shape distinction this
    /// property was split out to express is collapsed back into a single derived boolean downstream. Concretely:
    /// <c>CodeMapTemplater</c> states it emits no boot marker and keeps this false, and the rendered
    /// <c>code-map.html</c> gets the boot script anyway because the page does carry a mount.</para>
    ///
    /// <para><b>Do not read this as live configuration.</b> Either route it into the IR head projection so the
    /// Nuxt side stops re-deriving it, or delete it and its five setters. That choice is deliberately left to
    /// the owner rather than taken inside a code review — it is a contract change, not a fix.</para></summary>
    public bool HierarchyBootInline { get; init; }

    /// <summary>Whether this page carries at least one Story 24.2 relationship graph
    /// (<see cref="RelationshipGraph"/>) and therefore needs the vendored plotly.js bundle. Computed by the producer
    /// from the RENDERED BODY (<see cref="RelationshipGraph.ContainsHost"/>), exactly like
    /// <see cref="HierarchyEngineNeeded"/>.
    ///
    /// <para><b>A second flag rather than a widened first one, and the bundle is still emitted once.</b> The two
    /// components share the engine (ADR 0030: <c>scatter</c> was already registered in the bundle
    /// <see cref="HierarchyEngineNeeded"/> ships, so the marginal cost is zero bytes) but they are DIFFERENT
    /// components with different hosts, and folding the graph into the hierarchy flag would make
    /// <c>HierarchyExplorer.ContainsHost</c> disagree with a page that has no hierarchy on it. The adapter emits the
    /// <c>&lt;script src&gt;</c> when EITHER flag is set, so a page carrying both still gets exactly one tag.</para>
    /// <para>Optional with a <c>false</c> default, for the same reason as the hierarchy flag: a code page with no
    /// graph must stay byte-identical, and 1.2 MB is not a rounding error.</para></summary>
    public bool GraphEngineNeeded { get; init; }

    /// <summary>Whether <see cref="RelationshipGraph.BootScript"/> is emitted INLINE, between the wayfinding band
    /// and the body — the graph's anti-flash handshake, which has to run while the body is still parsing. Split from
    /// <see cref="GraphEngineNeeded"/> for exactly the reason <see cref="HierarchyBootInline"/> is split from
    /// <see cref="HierarchyEngineNeeded"/>: placement and need are independent facts.
    /// <para>Chrome-level, and therefore OUTSIDE the IR content region — the webview and SPA surfaces consume
    /// <see cref="PageView.BodyHtml"/> directly and must carry no script. [Story 24.2]</para></summary>
    public bool GraphBootInline { get; init; }

    /// <summary>Page-specific <c>&lt;head&gt;</c> additions, emitted verbatim as
    /// <see cref="PathUtil.RenderHeadOpen"/>'s <c>extraHead</c>. The producer owns the exact tags. Two real users:
    /// a code page's Prism stylesheet + highlighter, and the Impact Map's head-placed hierarchy boot marker.
    /// Null on every other page, which is why it is optional rather than required. [Story 23.4 AC #3]
    ///
    /// <para>⚠️ <b>WRITE-ONLY in production since Story 23.6</b>, exactly like <see cref="HierarchyBootInline"/>
    /// — see that property for the full account. [Story 23.4 code review, finding F-6] <c>RenderHeadOpen</c>'s
    /// caller was the deleted C# page writer; the only remaining read of this property anywhere is an assertion
    /// in <c>CodeFileTemplaterTests</c>. Nuxt re-derives both users heuristically instead: the Prism head from
    /// <c>class="language-…"</c> in the region (which its own comment records as over-firing on ~20 pages the C#
    /// side did not highlight), and the Impact Map's boot marker from <c>data-hierarchy</c>. Route it into the
    /// IR head projection or delete it — but do not extend it believing it reaches the page.</para></summary>
    public string? ExtraHead { get; init; }
}
