# ADR 0030 — Epic 24's Graph Engine Is the Already-Shipped Plotly `scatter` Trace Over a Generation-Time Layout

**Status:** Accepted — 2026-07-29
**Decided by:** [Story 24.6](../../_bmad-output/implementation-artifacts/24-6-graph-engine-spike.md), the Epic 24
graph-engine spike · evidence in
[24-6-spike-report.md](../../_bmad-output/implementation-artifacts/24-6-spike-report.md)
**Closes:** [ADR 0012](0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) **§4**'s named open
question. **ADR 0012 is EXTENDED, not superseded, and its status is unchanged (`Accepted`).**
**Companion:** [ADR 0013](0013-text-twin-is-the-no-js-contract.md) — the text twin remains the no-JS contract
**Stands on:** [ADR 0010](0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) **§3** (generation-time
computation) · [ADR 0011](0011-directed-graph-edge-direction-carrier-to-target.md) (edge direction)

---

## Context

ADR 0012 adopted Plotly for the **hierarchy** chart family and deliberately left one question open:

> **Epic 24's graph engine is a named open question**, deferred to Epic 24's own spike. It may be Plotly `scatter`
> with a hand-rolled layout, a second library, or bespoke — decided on evidence, not assumed here.
> **Two engine families are permitted. A third requires an ADR.**

Epic 24 contained **no spike story**. Stories 24.1–24.5 are all implementation stories, so that open question had
no owner, and a dev agent implementing Story 24.2 would have had to select SpecScribe's **second third-party
runtime dependency** mid-implementation — the failure mode
[ADR-creation-trigger-gap](../../_bmad-output/implementation-artifacts/) and CLAUDE.md § Decision records forbid.
Story 24.6 was seated on 2026-07-24 to be that spike. This ADR is its decision.

Epic 24 needs four shapes: an **ego** force-directed graph (24.2), a **whole-repo** force-directed graph (24.3), a
**chord/arc** view (24.4), and an **adjacency-matrix heatmap** (24.5, which ADR 0012 §4 already freed to ride
Plotly's `heatmap`).

Two facts, both measured rather than assumed, dominate the decision.

**First: Plotly's `scatter` trace cannot be excluded from any Plotly bundle, and Plotly is already shipped.**
`src/SpecScribe/assets/plotly-hierarchy.min.js` is vendored (**1,223,563 B**) and embedded at
`SpecScribe.csproj:67`. Its registered trace modules, parsed out of the shipped asset, are exactly
**`heatmap, scatter, sunburst, treemap`**. Plotly's own documented network-graph recipe is *edges as a `scatter`
trace with `mode:'lines'`, nodes as a `scatter` trace with `mode:'markers'`, with the layout computed externally*.
So a node-link graph drawn with `scatter` has a marginal bundle cost of **zero bytes** — not amortised to zero,
but zero, because the bytes are already in the shipped tool and already paid for by Epic 20.

**Second: node position can be treated as data.** ADR 0010 §3 — *"computed once at generation time and embedded"* —
survives ADR 0012 (§7). A seeded Fruchterman–Reingold pass in C# over a bounded node set is deterministic, costs
**zero client bytes**, makes FR31 trivially true, and reduces the client's job to drawing points and lines.

The honest counter-argument, and the axis the spike was told was most likely to break the choice: 24.3 requires
**clutter controls** (a support/confidence threshold and directory grouping), and filtering **changes the graph**,
so a precomputed layout must either be recomputed per filter state or abandoned. That axis was measured and
resolved (see *Filtering* below).

---

## Decision

**Epic 24's force-directed views (24.2, 24.3) are drawn with Plotly's `scatter` trace over node coordinates
computed in C# at generation time and embedded as a JSON payload island. No new engine family is introduced and no
new runtime dependency is added.**

Six ratified points:

1. **Engine.** The already-vendored Plotly bundle, using `scatter` with `mode:'lines'` for edges and
   `mode:'markers'` for nodes. **No second library.** Every Epic 24 surface routes through the same component
   contract ADR 0012 §2 defines and the `navigate` | `select` mode grammar of ADR 0012 §3, and `select` uses the
   **shipped `specscribe:explorer-select` seam**, never a parallel event.

2. **Node position is DATA, not presentation.** The layout is solved once, in C#, at generation time, and embedded
   as coordinates. There is **no client-side force simulation, no iterative solver, and no physics** in the browser.
   This is the ADR 0010 §3 reading applied to position, and it makes FR31 determinism a property of the generator
   rather than a hope about the client.

3. **The layout must be deterministic by construction, and the construction is normative:** a seeded PRNG that is
   **not** `System.Random` (its algorithm is documented as an implementation detail that may change between .NET
   versions, which would make determinism expire silently on an SDK bump); **no dictionary or set iteration order
   may reach a floating-point accumulation** — collections are materialised through an explicit ordinal sort first,
   because floating-point addition is not associative; no wall-clock, no environment, no parallelism; invariant
   formatting. Verified by **repetition across separate processes**, not by assertion.

4. **Filtering hides; it never re-lays-out.** The layout is solved once at the most inclusive threshold. Clutter
   controls change **visibility**, and surviving nodes **do not move**. Precomputing a layout per filter state is
   rejected, and so is re-solving client-side.

5. **Per-edge emphasis is carried by dash pattern, width band, node shape and accessible text — never by hue**
   (UX-DR17, mirroring the shipped `Charts.CouplingGraph` precedent). Because `scatter` line style is a
   **trace-level** attribute, per-edge styling is achieved by grouping edges into one trace per style class. This
   quantises stroke width into bands; **confidence must therefore be legible from the tooltip and the text twin, and
   must not be encoded in stroke width alone.**

6. **24.5 is unchanged:** the adjacency-matrix heatmap rides Plotly's `heatmap` trace, already registered in the
   shipped bundle, exactly as ADR 0012 §4 permitted.

**Engine-family accounting, stated plainly because it is a governance fact:** this decision **extends family 1**.
It adds **no** family. ADR 0012 §4's allowance of a second family is **unspent** — and a **third** family still
requires its own ADR.

### The one gap, and how it is handled

**Plotly has no chord trace.** Story 24.4's chord/arc view is therefore **not** served by this decision. 24.4 must
either hand-draw SVG arcs — and it must **read `docs/adrs/` first**, because three arc renderers already exist in
this codebase and consolidating them was Epic 20's work — or come back and re-price a second engine **with an
amendment to this ADR**. What 24.4 must **not** do is improvise a dependency inside an implementation story. That is
the exact failure this ADR exists to prevent, and naming the gap here is what keeps the prevention honest.

---

## Options considered

| Option | Bundle (min / gzip, ×`prism.js`) | Covers 24.2/24.3 | 24.4 chord | 24.5 matrix | Family consequence | Verdict |
|---|---|---|---|---|---|---|
| **(a) Plotly `scatter` + generation-time C# layout** | **0 B / 0 B — 0.00×** | ✅ | ❌ | ✅ (`heatmap`) | **no new family** | **ACCEPTED** |
| (b) Apache ECharts `graph`+`chord`, SVG renderer | 552,268 / 188,594 — 5.50× / 1.88× | ✅ | ✅ native | ✅ | **second family** | Rejected — adds a dependency and a family to buy what (a) gives for 0 B |
| (b″) ECharts **unified** (also replaces Plotly for Epic 20) | 657,660 / 223,108 — 6.55× / 2.22× | ✅ | ✅ native | ✅ | **one family; SUPERSEDES ADR 0012** | Rejected on cost-of-change, not on merit — see below |
| (c) Cytoscape.js | 443,319 / 141,961 — 4.42× / 1.41× | ✅ | ❌ | ❌ | second family | **Rejected — UX-DR7 FAIL** |
| (d) Bespoke SVG + a layout solver | 0 B vendored, hand-written renderer | ✅ | ✅ | ✅ | no new family | Rejected — reimplements what a shipped, CSP-cleared, a11y-verified engine already does |

All sizes are custom tree-shaken **IIFE** builds (minified, no sourcemap), measured this session, reported against
the already-accepted `prism.js` (100,409 B). Versions: echarts **6.1.0** (Apache-2.0), cytoscape **3.34.0** (MIT).

**Why (b″) was rejected even though ADR 0012 pre-authorised it.** ADR 0012's options table records ECharts as
*"considered and deferred, not dismissed… if [Epic 24's spike] selects ECharts, superseding this ADR is the expected
outcome rather than a failure."* The spike took that seriously and measured it. **ECharts is the technically better
graph engine**: genuinely per-edge `lineStyle` (83 distinct stroke widths measured, versus (a)'s 5 bands), a
**native `chord` series** confirmed rendering live, `aria.decal` pattern fills, an SVG renderer for ~4 KB gzip, and
a unified bundle **566 KB smaller than the Plotly bundle it would replace**. It was rejected because:

* **Epic 20 is complete, not in flight.** Stories 20.1–20.9 are `done` and 20.10 is in review. Superseding ADR 0012
  would reopen a spike, a component, a **text-twin audit**, a **site-wide rollout**, a details pane, and colorized
  hierarchies. A 566 KB saving does not buy that.
* **Adopting ECharts for graphs only is strictly worse:** a second dependency and a second family, 552 KB on top of
  Plotly's 1,223 KB, to obtain what (a) provides for 0 B.
* **Two defects were found that a config-level review would not have.** `echarts.init()` on a **zero-height
  container throws an uncaught `TypeError`** — reproduced deterministically, and SpecScribe actively creates that
  condition via the `specscribe:content-swapped` re-init seam, where Plotly survived every zero-size case. And
  **all geometry is animation-frame-gated**: at initial render every link path carries `d=""` and every symbol
  `scale(0)` **while every accessibility attribute passes**, so an attribute-only audit certifies a chart drawing
  nothing.
* **`scatter` would have shipped anyway** if Plotly shipped at all.

**This rejection is time-dependent and that is recorded on purpose.** Had Epic 20 still been in flight, the
recommendation would plausibly have inverted. If ADR 0012 is ever reopened for an independent reason, ECharts
should be re-priced against these measurements rather than re-argued from scratch.

**Why (c) failed.** Cytoscape's DOM is **1 `<div>` + 3 `<canvas>`, zero SVG, zero per-node elements**. A canvas
renderer emits no per-node DOM, so a roving-tabindex layer has nothing to attach to; reaching UX-DR7 would mean
building and continuously synchronising a parallel focusable overlay on every pan, zoom and filter — writing a
second renderer, not configuring around the first. Cytoscape core ships no live SVG renderer (`cytoscape-svg` is an
export plugin). Under ADR 0013 there is no SVG beneath the chart to fall back on, so this is decision-grade.

---

## Consequences

### Good

* **No new dependency and no new engine family.** SpecScribe's third-party runtime dependency count stays at
  **one**. Nothing to vendor, no `tools/*-vendor/`, no `<EmbeddedResource>`, no conditional-emission guard, and
  `specscribe generate` still needs no Node. The NFR10 supply-chain surface is unchanged.
* **FR31 determinism is structural, not aspirational.** Verified byte-identical across **three separate processes**
  over eleven fixtures.
* **Filtering is cheap and stable.** Measured 44–75 ms with surviving nodes provably not moving — which reads better
  than a re-settling graph, not worse.
* **Accessibility is verified against the strictest realistic condition.** UX-DR7 **PASS (configured around)**,
  UX-DR16/17/18 **PASS**, with the layer surviving **8/8** re-render events including a re-render the component did
  not initiate, applied only through Plotly's public `plotly_afterplot` event with **no internal patched or forked**.
  Real `ArrowRight` / `Enter` / `Escape` key events verified; zero painted foreign colours.
* **The webview CSP needs no relaxation.** `script-src 'nonce-…'` alone suffices, header **and** meta delivered; no
  `'unsafe-eval'`; and `style-src 'unsafe-inline'` was shown **not** to be load-bearing.

### Bad, or at least costly

* **Stroke width is quantised into bands**, because `scatter` line style is trace-level. Confidence must be readable
  from the tooltip and the text twin. ECharts would have given a continuous encoding.
* **24.4's chord view is unserved** and will cost hand-drawn SVG arc work, or an amendment here.
* **Per-edge hover needs an auxiliary invisible midpoint trace**, because a `lines` trace hovers on vertices rather
  than segments.
* **The generation-time solver is O(n²)** as implemented: measured 2.6 s at 391 nodes and 4.2 s at 489, projecting
  ≈17 s at 1,000 nodes. Story 24.3 must bound the node count or adopt Barnes–Hut above ~500 nodes. This cost is paid
  once by the generator and never by a reader — but it is a real generation-time budget on a large repository.
* **The whole-repo graph is a hairball at the shipped support floor.** 391 nodes / 4,864 edges at support ≥ 2, with
  one file (`sprint-status.yaml`) coupled to **92%** of the graph, 46% of edges Process-class and 62%
  cross-boundary. Story 24.3 should default to **support ≥ 5** with the Story 10.6 Code-only lens on, and should say
  in the UI what has been filtered rather than let a reader infer the repository is that entangled.
* **The ego neighbourhood is not small either** — 360 nodes uncapped — so Story 24.2 must cap (top-20 by confidence
  recommended, 20,253 B).

### Consequential and still open — named, not resolved here

* **Retiring `Charts.ReferenceGraph`'s SVG is gated on an ADR 0013 §3 per-surface text-twin audit that no Epic 24
  story currently owns.** Story 20.6's audit covered the *hierarchy* surfaces. Story 24.6 recommends that Story 24.2
  **supersede** `ReferenceGraph` rather than add a second graph to the code page — the shipped code already points
  that way, since Story 24.1 built the `ToGraphNodes` projection seam and its doc comment self-attributes the graph
  to 24.2 — but the twin audit must be owned before any SVG is removed.
* **`WebviewRenderAdapter.StripDataIslands` removes every `<script type="application/json">` island, so the webview
  cannot receive a graph payload today.** Either that exception is narrowed, or Epic 24 surfaces take the
  ADR 0013 §7 text-twin fallback in the webview. This is the same open decision Story 20.4 §4.4 left for the
  hierarchy family, and it should be decided **once, for both**.
* **The ADR 0005 CSP amendment is still owed and still lands once**, jointly with Story 23.4 (ADR 0012 §5). This
  ADR's contribution is the evidence that no relaxation of the policy string is required.

### Boundaries on the evidence behind this decision

Recorded so a later reader does not over-read it: **`file://` was not run live** (the preview pane refuses a live
`file://` context — the same limitation Story 20.4 recorded); structural evidence is strong but the run is owed.
**No screenshot and no pixel verification** — the pane never composited a frame, so visual claims rest on computed
styles, DOM geometry and the focus model, and a human eyeball is owed at Story 24.2's elicitation. **`Tab` traversal
itself was not verified**, though the focus model and real arrow/Enter/Escape keys were. **No screen-reader run.**
**`vscode-resource:` delivery and an Electron paint remain untested**, so the webview verdict is a **lower bound**.
