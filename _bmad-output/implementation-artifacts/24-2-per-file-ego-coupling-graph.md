---
baseline_commit: 94b8e56fc297e94640f5bcdc5b568ed1394033ea
---

# Story 24.2: Per-File Ego Coupling Graph (Force-Directed) on Code Pages

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer opening one file's page,
I want an interactive node-link graph of that file and the files it changes with,
so that the relationship reads as a picture I can explore — not a flat list — answering "what changes alongside THIS file" at a glance.

## Acceptance Criteria

1. **Given** a code page for a file with coupling data and JavaScript available
   **When** the ego graph renders
   **Then** the focal file sits at the center with its coupled neighbors (bounded to a sensible degree, 1–2 hops) as a force-directed node-link graph, nodes sized by change frequency and edges weighted/colored by the Story 24.1 confidence (cross-boundary edges emphasized), with rich hover/focus tooltips (UX-DR19) and nodes linking to their own code pages
   **And** the graph reuses the Story 24.1 metric and routes through a component honoring the same mode / legend / text-twin contract as the Epic 20 Hierarchy Explorer — using **this epic's chosen graph engine** ([ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md): the already-vendored Plotly `scatter` trace over a generation-time C# layout), not a per-story reinvention.

2. **Given** a JavaScript-off, reduced-motion, or assistive-technology visitor (NFR8)
   **When** the ego graph cannot hydrate
   **Then** it degrades to the Story 24.1 ranked list as the text equivalent, with every node/edge metric available as non-color text
   **And** a file with no qualifying couples shows a designed empty state, never a broken or misleading empty graph.

   > **AC #2 amendment, owner-approved at create-story 2026-07-29.** The epic's original wording said "degrades to a static SVG rendering (evolving the Story 7.8 `Charts.ReferenceGraph`) **plus** the Story 24.1 ranked list". [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) §1/§4 supersedes that: the text twin **is** the no-JS contract and ADR 0010 §2's "a useful chart must render with JS off" no longer holds. Keeping the SVG *and* adding the interactive graph is the dual-renderer option ADR 0013's options table explicitly rejected. The SVG is therefore **retired**, gated on AC #3.

3. **Given** ADR 0013 §3's hard per-surface gate and the fact that **no Epic 24 story owned it** (surfaced by [ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md) "Consequential and still open" and spike report §8.3)
   **When** `Charts.ReferenceGraph`'s server-rendered SVG is retired from the code page
   **Then** the code page's server-rendered text twin has first been **audited complete for BOTH node populations** — citing artifacts *and* coupled files, including epic membership and the story↔related / related↔related cross-edges — **in a live browser with JavaScript disabled**, not by test assertion alone
   **And** the golden-fingerprint replacement required by ADR 0013 §6 lands **in this same story**: assertions move to the embedded payload, the component configuration, and the twin, rather than SVG path geometry.

   > **Owner decision, create-story 2026-07-29:** the unowned audit is **folded into this story as a gating task** (Task 6) rather than seated as a separate Story 24.7. The SVG is not deleted until Task 6 passes.

## Owner decisions taken at create-story (2026-07-29)

These four were elicited up front (CLAUDE.md § Story lifecycle step 1) so the verify round does not have to spend a round on them. **Do not re-litigate them in dev-story; implement them.**

| # | Decision | Consequence for implementation |
|---|---|---|
| **D1 — Visual direction** | **A: Evolved hub-and-spoke.** Focal file pinned dead-center and never moving; both populations on a relaxed ring around it. Today's shape/edge vocabulary is preserved: citing artifacts = gold circles on solid edges, coupled files = neutral diamonds on dashed edges. | The C# solver **pins the focal node at the center** and relaxes only the ring, rather than running an unconstrained Fruchterman–Reingold settle. Directions B (free constellation) and C (concentric orbit) were offered and **not** chosen. |
| **D2 — Node cap** | **Top-20 by confidence, via a graph-scoped cap.** `GitMetrics.FileInsightCoupledCap = 8` stays as the const default; a new named cap governs the code page's relationship surface. | Measured by the spike: 21 nodes / 210 edges / **20,253 B** — ≈ Story 23.1's already-accepted 20,915 B sunburst island. **See "The cap has a twin-completeness consequence" in Dev Notes — the twin moves with the graph, not the other way around.** |
| **D3 — The two toggles** | **Both survive as client-side edge-visibility filters.** "Group by epic" and "Show relationships" are kept; the four pre-rendered `RefGraphVariants` are deleted. | One layout solved at the most inclusive state; filters **hide, never re-lay-out** (ADR 0030 §4, measured `nodePositionsMoved: false` in 44–75 ms). Both toggles are edge-visibility only — spike §7.2 is what makes this cheap. Controls ride inside the component's `hidden` control bar so a JS-off reader never sees an inert checkbox. |
| **D4 — The twin audit** | **Folded into this story as a gating task**, not seated as Story 24.7. | Task 6. Coexisting (keeping the SVG) was offered and rejected as the ADR 0013 dual-renderer anti-pattern. |

## Tasks / Subtasks

- [x] **Task 1 — Widen the coupled population to the graph cap** (AC: #1, D2)
  - [x] Add a named const for the code-page relationship surface, e.g. `public const int RelationshipGraphCoupledCap = 20`, beside `FileInsightCoupledCap` ([GitMetrics.cs:926](src/SpecScribe/GitMetrics.cs)). **Leave `FileInsightCoupledCap = 8` at its current value** — it stays the default for any caller that does not ask for more.
  - [x] Thread `coupledCap` through `ParseNumstatLog` → `BuildFileInsights` **exactly as Story 24.1 threaded `minSupport`** ([GitMetrics.cs:660](src/SpecScribe/GitMetrics.cs) is the single call site; `BuildFileInsights` already takes `coupledCap` at [GitMetrics.cs:956/970](src/SpecScribe/GitMetrics.cs)). One optional parameter with a default, no new CLI flag — same shape 24.1's Q3 settled on.
  - [x] **Do NOT add a second git call or a second commit scan** ([[deep-git-single-numstat-path]]). The wider cap is a `Take` bound on an already-computed, already-sorted, already-floored list.
  - [x] The support floor (`CouplingMinSupport = 2`) and the confidence sort are applied **before** the cap — that ordering is already correct in `BuildFileInsights`; verify it still is after the cap change rather than assuming.

- [x] **Task 2 — Generation-time layout solver (node position is DATA)** (AC: #1, D1)
  - [x] New file `src/SpecScribe/CouplingLayout.cs`. Pure, repo-free, no I/O. Input: the focal file + the two node populations + the edge set. Output: embedded coordinates.
  - [x] **D1 shape:** focal node pinned at the canvas center and excluded from the relaxation. Citing artifacts and coupled files relax on a ring so labels do not collide; the hub-and-spoke read must survive.
  - [x] **ADR 0030 §3's construction is NORMATIVE, not advisory. All four apply:**
    - [x] **No `System.Random`.** Use a private seeded PRNG (the spike used xorshift128+ with a compile-time seed). `Random`'s algorithm is a documented implementation detail that may change between .NET versions — determinism would expire silently on an SDK bump.
    - [x] **No `Dictionary`/`HashSet` iteration order may reach a floating-point accumulation.** Materialize every collection through an explicit **ordinal sort** first. Floating-point addition is not associative; an order change moves the last bits of every coordinate.
    - [x] No wall-clock, no environment, no parallelism.
    - [x] All formatting through `CultureInfo.InvariantCulture` with a fixed format string.
  - [x] **Choose the coordinate/confidence rounding precision deliberately and say why in a comment.** The spike found 4-decimal rounding *collapses distinct confidence values* (452 survive where 453 exist upstream). Harmless at this precision, but it makes precision a **data** decision, not a cosmetic one.
  - [x] Verify determinism **by repetition across separate processes**, not by assertion — in-process repetition cannot see string-hash randomization, allocation-order effects, or tiered JIT changing float contraction.

- [x] **Task 3 — The graph component (server side)** (AC: #1)
  - [x] New file `src/SpecScribe/RelationshipGraph.cs` — a **sibling of `HierarchyExplorer`, not a reuse of it**. The data shape is nodes + edges, not a hierarchy; `HierarchyNode`/`HierarchyExplorerModel` do not fit. What must be **identical** is the *contract* (ADR 0012 §2, §4): one datasource per instance, one selector/control idiom, one framing block, one mandatory text twin.
  - [x] Mirror `HierarchyExplorer.Render`'s emitted skeleton ([HierarchyExplorer.cs:594](src/SpecScribe/HierarchyExplorer.cs)) — read it in full before writing this: `hidden` control bar → boot placeholder (`role="status"`, sized to reserve height) → empty chart host carrying a host marker → `aria-live="polite"` region → legend → payload island → text twin, all wrapped by `Charts.Framed`.
  - [x] **Framing (Story 10.2): reuse `Charts.WhyText(ChartMetric.ChangeCoupling)`** ([Charts.cs:63](src/SpecScribe/Charts.cs)). Do NOT hand-roll new "why" copy at the call site. Ranking caption goes in `ChartMeta.Ranking`; data caveat in `ChartMeta.Note`.
  - [x] **Mode is `navigate`** (ADR 0012 §3) — activating a node follows its `href` to that file's code page. `select` mode and a details pane are **not** in scope here; if the verify round wants one, it must use the shipped `specscribe:explorer-select` seam, never a parallel event (ADR 0030 §1).
  - [x] **Legend must describe the channel actually on screen.** ADR 0030 §5: emphasis is carried by **dash pattern, width band, node shape and accessible text — never by hue** (UX-DR17). Because `scatter` line style is a **trace-level** attribute, per-edge styling means **one trace per style class**, which **quantizes stroke width into bands**. A legend showing a continuous scale beside a banded chart is the "misdescribing entry" class Stories 10.7 and 21.1 each closed.
  - [x] **Therefore: confidence must be legible from the tooltip and the text twin, and must NOT be encoded in stroke width alone.** This is a named ADR 0030 consequence, not a nicety.
  - [x] Add a `ContainsHost(string bodyHtml)` analogue so the `AssetManifest` engine flag is derived **from the rendered body**, never hand-set — mirroring `HierarchyExplorer.ContainsHost` ([HierarchyExplorer.cs:1098](src/SpecScribe/HierarchyExplorer.cs)) and `Mermaid.ContainsBlock`. A flag derived from the page cannot disagree with the page.

- [x] **Task 4 — Client renderer + accessibility layer** (AC: #1, #2)
  - [x] Extend `src/SpecScribe/assets/specscribe.js`. Read the Hierarchy Explorer block ([specscribe.js:998–1150+](src/SpecScribe/assets/specscribe.js)) first and follow its idioms — mount registry, purge-on-removal, cleanup handle, failure unwind.
  - [x] Plotly's documented network recipe: **edges = a `scatter` trace with `mode:'lines'`, nodes = a `scatter` trace with `mode:'markers'`**, layout supplied externally. **No client-side force simulation, no iterative solver, no physics** (ADR 0030 §2).
  - [x] **Per-edge hover needs an auxiliary invisible midpoint trace** — a `lines` trace hovers on *vertices*, not segments (ADR 0030 "Bad, or at least costly").
  - [x] **Hang the a11y layer on `plotly_afterplot`, never on the promise `Plotly.react` returns.** The spike verified the layer survives **8/8** re-render events including a **bare `Plotly.react` the component did not initiate** and the shipped **`specscribe:content-swapped`** SPA seam. A layer surviving only the component's own redraw is a **FAIL** under Story 20.4's decision rule.
  - [x] **Clamp the roving `tabindex` index on EVERY reapply.** Story 20.4's sixth finding was an unclamped roving index leaving the chart Tab-unreachable after the node count shrank. The 24.6 probe fixed it by construction; **24.2 must keep the clamp.**
  - [x] **Reading order = degree-descending, then weight, then ordinal path — deliberately matching the text twin's order** (Story 24.1's Q4 ordering), not the DOM order Plotly happens to emit. Twin and graph must agree.
  - [x] Survival predicate to assert after every event: *nodes > 0 **and** every node carries a role **and** every node carries a non-empty `aria-label` **and** exactly one node holds `tabindex="0"`.*
  - [x] Tooltips route through the **body-level `.ss-tooltip` node**, not a CSS `::after` ([[tooltip-clipping-use-ss-tooltip-node]]). Opt the graph markers into the existing `SEG` selector family the way `.ss-hierarchy-sector` does ([specscribe.js:103](src/SpecScribe/assets/specscribe.js)) — one tooltip system site-wide.
  - [x] **Presentation is SpecScribe's tokens, never Plotly's colorways** (ADR 0012 §6). Neutral ink/gold/border tokens only — the `--status-*` lifecycle tokens are **off-limits on code surfaces** (existing `ReferenceGraph` doc comment states this rule; keep it).
  - [x] **Reduced motion (UX-DR18):** there is no settle animation to suppress (position is precomputed), but any transition used for filtering must snap under `prefers-reduced-motion`. Drive it from the `--motion-*` tokens ([[motion-token-system]]); never `transition` a Plotly-owned property ([[story-20-5-hierarchy-explorer-done]]).

- [x] **Task 5 — Absorb the two toggles as client edge filters; delete the four variants** (AC: #1, D3)
  - [x] Delete `RefGraphVariants` and the four-panel pre-render loop ([CodeFileTemplater.cs:441](src/SpecScribe/CodeFileTemplater.cs), [CodeFileTemplater.cs:506–516](src/SpecScribe/CodeFileTemplater.cs)) together with the pure-CSS `~`-sibling show/hide rules in `specscribe.css` and the `RefGraphGroupSlug` id-uniqueness helper if nothing else needs it.
  - [x] Re-implement **both** affordances as edge-visibility filters over the **single** layout: "Show relationships" toggles the cross-edge traces; "Group by epic" toggles the epic-hub edges. **Surviving nodes do not move** (ADR 0030 §4).
  - [x] Controls go **inside** the component's `hidden` control bar so they inherit the reveal handshake — a JS-off visitor must never see an inert control. This is the same convention `ss-hierarchy-controls` / `codemap-controls` already follow.
  - [x] The cross-edge data (`BuildStoryRelatedEdges` / `BuildRelatedRelatedEdges`, [SiteGenerator.cs:2809/2842](src/SpecScribe/SiteGenerator.cs)) is **index-aligned** with the related-node list ([SiteGenerator.cs:2806](src/SpecScribe/SiteGenerator.cs) doc comment). Widening the cap in Task 1 changes that list's length — **re-verify the alignment holds**, do not assume.

- [x] **Task 6 — ⛔ GATING: ADR 0013 §3 text-twin audit, then the fingerprint replacement** (AC: #3)
  - [x] **This task blocks Task 7. Nothing is deleted until it passes.**
  - [x] Audit the code page's server-rendered twin against ADR 0013 §2's four properties — **server-rendered · complete · navigable · non-color** — for **BOTH** populations: citing artifacts (`ref-list` items with epic membership and `BuildStoryCrossSuffix`) **and** coupled files (`ref-list-related` sub-list with support, confidence, cross-boundary words, lift-on-title, `BuildRelatedCrossSuffix`). See [CodeFileTemplater.cs:518–556](src/SpecScribe/CodeFileTemplater.cs).
  - [x] **Verify in a live browser with JavaScript disabled** ([[browser-pane-five-server-cap-file-url-fallback]] — verify over `file://` rather than stopping another session's server; note `navigate` STRIPS the hash). CLAUDE.md § Verification applies with full force: *the test suite structurally cannot see what a JS-off visitor actually gets.*
  - [x] Also audit the **`+N more`** honesty disclosure. `RefGraphArtifactNodeCap = 14` bounds what the graph **draws**; the sr-only list already enumerates **all** citers. Confirm that survives the rework — assistive tech must never have less information than the richest sighted view.
  - [x] **Land the ADR 0013 §6 fingerprint replacement in this same change.** Move assertions to the **embedded payload, the component configuration, and the twin**. The golden fingerprint stops covering this chart's geometry; if nothing replaces it, chart regressions go un-netted.
  - [x] Record the audit result in the story's Dev Agent Record. **An incomplete twin keeps its SVG** — that is the ADR's rule, and reporting the gap is the correct outcome, not a failure of the story.

- [x] **Task 7 — Retire the `ReferenceGraph` SVG (only after Task 6 passes)** (AC: #2, #3)
  - [x] **`Charts.ReferenceGraph` has TWO call sites, not one.** The obvious one is `BuildRelationshipsCard` ([CodeFileTemplater.cs:509](src/SpecScribe/CodeFileTemplater.cs)). The second is **`BuildAside`** ([CodeFileTemplater.cs:417](src/SpecScribe/CodeFileTemplater.cs)), reached from the **placeholder** page path when a file has no extra tabs ([CodeFileTemplater.cs:781](src/SpecScribe/CodeFileTemplater.cs)) — a citations-only graph with **no coupling data at all**. Decide its fate explicitly (keep the SVG for that path, or give it the component with an empty coupled population) and **state the decision in the completion notes**. Missing it ships a compile error or a silently blank sidebar.
  - [x] `Charts.ReferenceGraph` is referenced by **37 assertions across `tests/SpecScribe.Tests/ChartsTests.cs`**. Retiring it means deleting or rewriting them — count and account for them; do not leave orphaned dead tests.
  - [x] **Attribution handoff, per CLAUDE.md § Scoping a code review:** `RelatedNode`'s metric members (`Support`, `Confidence`, `Lift`, `CrossBoundary`) and **`ToGraphNodes`** sit in **Story 24.1's File List** but their doc comment **self-attributes them to Story 24.2** ([CodeFileTemplater.cs:285–317](src/SpecScribe/CodeFileTemplater.cs)). **This story's code review must cover them.** Recorded here so they cannot fall between the two reviews.
  - [x] Sonar already flags `external_roslyn:CA1859` on `ToGraphNodes` (return type could be concrete). Since 24.2 owns that symbol, resolve or explicitly waive it while you are there.

- [x] **Task 8 — Wiring, tests, and live-browser verification** (AC: #1, #2, #3)
  - [x] **Asset manifest:** code pages need the Plotly bundle. Extend `AssetManifest` ([AssetManifest.cs:24](src/SpecScribe/AssetManifest.cs)) and wire it in `EndShell` ([CodeFileTemplater.cs:827–852](src/SpecScribe/CodeFileTemplater.cs)). Note the code page **already uses `ExtraHead`** for Prism — do not clobber it. Keep the flag `false`-defaulted so a page without a graph stays byte-identical (1.2 MB is not a rounding error).
  - [x] **Measure the total output-size delta across a real `--deep-git` portal.** ~20 KB of island per code page across a large code-page population is a real number; the true page inventory is **1,408 pages** ([[specscribe-true-page-inventory-1408]]) and two pages are already on the `oversizedPages` list. Report the before/after, the way ADR 0012's spike concern #2 required for hierarchies.
  - [x] Full suite. **Golden fingerprint WILL move.** Regenerate deliberately, `dotnet build --no-incremental` first (embedded `.css`/`.js` assets are cached by an incremental build, so the hash you measure is stale), confirm stable across **two repeated runs**, and split the provenance — say whose changes yours sat on top of ([[golden-diff-normalization-gotchas]], CLAUDE.md § Concurrent work).
  - [x] **Live-browser verification is mandatory and is where the real defects will be.** Story 24.1's live pass caught two rendered-geometry defects the suite structurally could not see, both in this same panel. Verify: the ~320–360px sidebar/tab width, label collision at 21 nodes, no clipped cells, focus ring visibility, real `ArrowRight`/`Enter`/`Escape` keys (not synthetic dispatch), and the tooltip's zero clipping ancestors.
  - [x] **Assert on GEOMETRY, not attributes.** The spike's hand-off: an attribute-only audit certified an ECharts chart that was **drawing nothing** (every path `d=""`, every symbol `scale(0)`) while every a11y attribute passed. And per Story 20.4, do not assert on the console either.

## Dev Notes

### What this story IS and is NOT

- **IS**: the interactive ego graph on code pages, **superseding** `Charts.ReferenceGraph` in place; the first Epic 24 rendering surface; the C# layout solver Stories 24.3/24.4 will build on; and — folded in by owner decision D4 — the ADR 0013 §3 twin audit for the code-page surface.
- **IS NOT**: the whole-repo explorer (24.3), the chord/arc view (24.4), the adjacency matrix (24.5), any new page, any new nav entry, any new runtime dependency, or the metric itself (24.1, shipped). Not the ownership/bus-factor half either (Story 7.11 — do not touch it).

### The engine is decided. Do not re-open it.

[ADR 0030](../../docs/adrs/0030-epic-24-graph-engine.md), **Accepted 2026-07-29**: the already-vendored **Plotly `scatter`** trace over a **generation-time C# layout**. Marginal bundle cost **zero bytes** — `src/SpecScribe/assets/plotly-hierarchy.min.js` (1,223,563 B, plotly.js **3.7.0**, MIT, embedded at `SpecScribe.csproj:67`) was measured to register exactly `heatmap, scatter, sunburst, treemap`, so `scatter` is already in the shipped tool.

**ADR 0012 is EXTENDED, not superseded.** No new engine family; §4's allowance of a second family is left **unspent**; a third still needs its own ADR. SpecScribe's third-party runtime dependency count stays at **one**.

ECharts was measured and rejected on **cost-of-change, not merit** — it is the better graph engine, and that rejection is recorded as **time-dependent**. **Do not re-argue it inside this story.** If the work genuinely demands it, the correct move is a focused re-opening of ADR 0030, never a quiet dependency.

> **Read `docs/adrs/` before declaring you are crossing a project rule.** Story 21.3 described its interactive treemap as "a deliberate crossing of the pure-SVG, no-JS rule" citing a stale memory, when ADR 0010 already permitted it ([[adr-consultation-gap-three-arc-renderers]]). [[charting-is-pure-svg-no-js]] is **SUPERSEDED** for this family. The ratified ADR is the authority.

### The cap has a twin-completeness consequence — read this before implementing D2

D2 keeps `FileInsightCoupledCap = 8` as a const default and gives the graph its own cap of 20. But `FileInsight.CoupledFiles` is the **single** source feeding **three** consumers, and all three are the *same* surface — the Relationships card:

1. the graph's related-node population,
2. the **sr-only text twin** ([CodeFileTemplater.cs:537–555](src/SpecScribe/CodeFileTemplater.cs)),
3. the two index-aligned cross-edge builders in `SiteGenerator`.

ADR 0013 §2 requires the twin to be **complete**: *"no fact may exist only inside the chart."* So a graph showing 20 files with a twin listing 8 **fails the contract** — and Task 6 is the gate that would catch it.

**Therefore the twin follows the graph, not the other way around.** The graph cap governs the whole Relationships card: graph, twin, and edge builders move together at 20. Concretely: `ParseNumstatLog` is called with `coupledCap: RelationshipGraphCoupledCap`, and the sr-only list grows 8 → 20 on code pages. That is a **visible behaviour change** and a deliberate consequence of D2 — record it in the completion notes the way Story 24.1 recorded the support floor reaching the per-file list. `FileInsightCoupledCap`'s value is unchanged for anything that does not ask.

The visible "Often changed with" list was **already removed** by Story 7.8; the only list on this surface is the sr-only twin. So this change is confined to the accessible listing, not to visible page density.

### ⚠️ The zero-width mount trap — the most likely way this ships broken

**Plotly cannot lay out in a zero-width container, and it does not complain: it draws a chart of the wrong size.** The code page's tabs are **pure-CSS radios**, and when an Insights panel exists it is the default-checked tab — so the **Relationships panel is `display:none` at mount time**, i.e. zero width.

The Hierarchy Explorer already solved exactly this. Reuse the mechanism, do not reinvent it:

- Width is measured on the **panel**, not the host — the host's own rule is `display:none` until reveal ([specscribe.js:1038–1043](src/SpecScribe/assets/specscribe.js)).
- `deferHierarchyMount` / `flushHierarchyReveals` queue a zero-width root and retry ([specscribe.js:1092–1116](src/SpecScribe/assets/specscribe.js)).
- The trigger is **one delegated `change` listener on `[data-hierarchy-reveal]` controls** ([specscribe.js:1128](src/SpecScribe/assets/specscribe.js)). **The code-page tab radios must carry the reveal marker** (or its graph analogue) or the graph mounts at zero width forever.
- A host plotted while visible and later resized gets `Plotly.Plots.resize` ([specscribe.js:1120–1123](src/SpecScribe/assets/specscribe.js)).

Also mirror the **failure unwind** ([specscribe.js:1063–1080](src/SpecScribe/assets/specscribe.js)): a throw *after* `Plotly.newPlot` succeeded previously left the reader with both charts mounted, the instance absent from the purge registry, and the ready flag still set so re-init skipped that root forever.

### Boot handshake and the anti-flash rule

`HierarchyExplorer.BootScript` sets a root attribute and **removes it on a timeout** ([HierarchyExplorer.cs:1091](src/SpecScribe/HierarchyExplorer.cs)). The expiry is what keeps the hide-first honest: if the bundle is blocked or missing, a hide-first with no timeout leaves a permanent "Initializing…" placeholder with nothing behind it. Follow the same shape.

The boot marker is **chrome-level** and therefore **outside the IR content region** — `JsonSpaRenderAdapter.RenderContent` composes nav + wayfinding + body only, which is why `IrSurface.vue` re-emits it from the head. Whichever placement you pick, it must not land inside the content region. ([AssetManifest.cs:31–43](src/SpecScribe/AssetManifest.cs), Story 23.4 Trap 3.)

### Webview and SPA — two named, unresolved boundaries

- **Webview: `WebviewRenderAdapter.StripDataIslands` removes every `<script type="application/json">` island** ([WebviewRenderAdapter.cs:101](src/SpecScribe/WebviewRenderAdapter.cs), applied at [WebviewRenderAdapter.cs:81](src/SpecScribe/WebviewRenderAdapter.cs) and [SiteGenerator.cs:3672](src/SpecScribe/SiteGenerator.cs)). **The webview cannot receive a graph payload today.** ADR 0030 leaves this open; it is the same decision Story 20.4 §4.4 left for hierarchies and it "should be decided once, for both." **In scope for this story:** take the **ADR 0013 §7 text-twin fallback** in the webview and *verify the webview code page does not ship an empty box*. Narrowing the exception is a separate, joint decision — propose an ADR if you find yourself needing it (CLAUDE.md § Decision records).
  - The spike measured what skipping this costs: a **client-built** twin contributed **0 bytes** under a half-applied CSP. **Server-render the twin.**
  - CSP itself is fine: `script-src 'nonce-…'` alone suffices, header **and** meta, no `'unsafe-eval'`, and `style-src 'unsafe-inline'` is **not** load-bearing. Read the policy from `WebviewRenderAdapter.cs` at runtime rather than citing a line — it drifted `:116 → :140` during the spike ([[cite-adrs-by-symbol-not-line-number]]).
- **SPA:** the `specscribe:content-swapped` seam re-inits components after a content swap ([[story-20-2-zoomable-drill-in-done]]). The spike verified the a11y layer survives it (8/8). Confirm the graph mounts after an SPA navigation into a code page, and that removed mounts are purged.

### Existing surfaces to reuse — do not reinvent

| Need | Reuse | Location |
|---|---|---|
| Story 10.2 framing | `Charts.ChartMeta` + `Charts.Framed` + `Charts.WhyText(ChartMetric.ChangeCoupling)` | [Charts.cs:13–168](src/SpecScribe/Charts.cs) |
| Percent / plural formatting | `Charts.Percent` (new in 24.1), `Charts.Plural` | Charts.cs |
| Cross-boundary flag | `GitMetrics.IsCrossBoundary` — **call it, never re-derive it** (AC #2 of 24.1: computed once, shared) | [GitMetrics.cs](src/SpecScribe/GitMetrics.cs) |
| Support floor | `GitMetrics.CouplingMinSupport` — shared const, not a literal | [GitMetrics.cs:275](src/SpecScribe/GitMetrics.cs) |
| Code/Process classification | **the real `ClassifyCoupling`**, not the probe's path-shape approximation | [GitMetrics.cs:271](src/SpecScribe/GitMetrics.cs) |
| Link resolution | the `coupledFileHref` `Func<string,string?>` dual-mode resolver already threaded in — a null return means "no in-portal page" → plain chip, **never a dead link** | [CodeFileTemplater.cs:48](src/SpecScribe/CodeFileTemplater.cs), wired at SiteGenerator |
| Component skeleton | `HierarchyExplorer.Render` / `IslandHtml` / `TextTwinHtml` / `BootScript` / `ContainsHost` — **as a pattern to mirror**, not to call | [HierarchyExplorer.cs:594/810/976/1091/1098](src/SpecScribe/HierarchyExplorer.cs) |
| Tooltip | body-level `.ss-tooltip` via the `SEG` selector family | [specscribe.js:103](src/SpecScribe/assets/specscribe.js) |
| Non-color emphasis precedent | the shipped `Charts.CouplingGraph` dash/width vocabulary | Charts.cs |

### Files being modified — read current state before editing

- `src/SpecScribe/GitMetrics.cs` — new graph cap const; `coupledCap` threaded through `ParseNumstatLog`.
- `src/SpecScribe/CouplingLayout.cs` — **NEW.** The deterministic solver.
- `src/SpecScribe/RelationshipGraph.cs` — **NEW.** The component (render skeleton, island, legend, twin wiring, `ContainsHost`).
- `src/SpecScribe/CodeFileTemplater.cs` — `BuildRelationshipsCard` rewritten to emit the component; `RefGraphVariants` + the 4-panel loop deleted; `RelatedNode`/`ToGraphNodes` (**handed over from 24.1**); `BuildAside`'s second `ReferenceGraph` call site; `EndShell` asset manifest. ⚠️ **This file has 12 open Sonar observations incl. two `S3776` cognitive-complexity errors and several `S107` 14-parameter warnings — it is already at its complexity ceiling. Extract rather than inline.**
- `src/SpecScribe/Charts.cs` — `ReferenceGraph` + `RefGraphArtifactNodeCap` retired (Task 7, gated). ⚠️ 49 open Sonar observations.
- `src/SpecScribe/SiteGenerator.cs` — cross-edge builders re-verified against the wider cap.
- `src/SpecScribe/AssetManifest.cs` — engine flag for the graph family.
- `src/SpecScribe/assets/specscribe.js` — the client renderer + a11y layer.
- `src/SpecScribe/assets/specscribe.css` — graph tokens; deletion of the `~`-sibling variant rules. ⚠️ A CSS comment containing `*/` silently truncates ~1000 rules ([[css-comment-star-slash-silent-truncation]]).

### Preservation invariants — leave the system working end-to-end

- **Baseline output byte-identical WITHOUT `--deep-git`.** Coupling data is null → the coupled population is empty. A citations-only page must still work.
- **Every chart needs an accessible text equivalent, and no state may be signalled by color alone** (CLAUDE.md § Verification, UX-DR17/19).
- The `+N more` overflow disclosure stays honest; the sr-only list keeps enumerating **all** citers.
- `CouplingFileSetCap = 50`'s bulk-commit skip already excludes merge/vendored sweeps from pair counts — inherited for free, do not re-implement.
- Output dir is `SpecScribeOutput` ([[generate-output-dir-is-specscribeoutput]]). Never `--output docs/live`.

### Previous-story intelligence (Story 24.1 + Story 24.6)

- **The metric spine exists and is correct.** `CoupledFile`, `DirectedCouple`, `DeepGitPulse.DirectedCoupling`, `IsCrossBoundary`, `CouplingMinSupport`, `Lift()`, `Charts.Percent` all shipped. `GitMetrics.Lift` is the **one** place the division happens so no surface can forget the divide-by-zero guard — it returns `null`, never `NaN`/`Infinity` (which would reach markup as literal text).
- **24.1's Q4 watch item, still open for your verify round:** on this repo the entire visible top-10 comes back at **100% confidence**, so a Confidence column does no ranking work in the visible window — while **lift** genuinely separates those rows (15.0× vs 2.16×) but is tooltip-only. At 20 nodes this may resolve itself, or it may mean the graph should encode **lift**, not confidence, in its width bands. **Measure it on real data and raise it to the owner; do not change the ranking policy unilaterally** — 24.1 correctly declined to.
- **24.1's live pass caught two defects the suite structurally could not see**, both pure rendered geometry in this same ~455px panel: a new column starved the path columns to 60px; the fix then exposed headers overrunning under `table-layout: fixed`. Expect the same class of defect here.
- **The deep-git 3s-timeout flake is real and it silently produces no deep surfaces at all** ([[gitmetrics-3s-timeout-silent-deep-git-loss]]). It cost 24.1 two generation attempts. If a `--deep-git` run comes back with no coupling, suspect the timeout before suspecting your code.
- **Suite "flake" is usually a running preview server** ([[suite-flake-cause-is-a-running-preview-server]]) — git SPAWN starvation. Stop previews before the full suite. The browser pane also caps dev servers at **5 per folder across all chats**.

### Shared-main discipline (CLAUDE.md § Concurrent work)

Another agent may be editing these files right now. **Grep-verify every new symbol after writing it** — a `Charts.cs` edit has silently vanished this way before ([[shared-main-concurrent-edit-loss-verify-after-edit]]; note a zero-grep can also be a transient mid-write). **Never `git reset --hard`, `git checkout --`, or `git clean`.** Expect the golden fingerprint to move under you from other sessions — **establish causality before regenerating**; audit `GoldenNormalization.NormalizeVolatile` / `FoldToday` first if you did not touch rendering. Bisect into a throwaway tree (`git archive HEAD` into the scratchpad) rather than resetting the shared tree.

### Analysis observations

`.specscribe/analysis/` exists but its `evaluatedAtRevision` is **`630ae25`** while HEAD is **`94b8e56`** — per CLAUDE.md's read-time rule, **the digest is stale regardless of `isStale`**. Re-run `node tools/analysis-digest/index.mjs` before trusting a line number; the counts above are directionally right, the lines are approximate. Confirm by symbol.

### Project Structure Notes

Two new `src/SpecScribe/*.cs` files (`CouplingLayout.cs`, `RelationshipGraph.cs`) plus their test siblings; everything else lands in existing files. No new page, no nav entry, no new CLI surface, no new dependency. If working in a worktree, target the worktree path — `main` has a background auto-committer ([[worktree-edits-must-target-worktree-path]]).

### Measured numbers you can rely on (Story 24.6, this repository, `-n 300`)

| Fixture | Nodes | Edges | Payload | C# solve |
|---|---:|---:|---:|---:|
| ego, **top-20** (D2) | **21** | **210** | **20,253 B** | 15.1 ms |
| ego, top-8 (today's cap) | 9 | 36 | 4,297 B | 3.0 ms |
| ego, 1 hop **uncapped** | **360** | **4,782** | 449,346 B | 2,223 ms |

The uncapped ego neighbourhood is **360 nodes**. **Never ship uncapped.** For reference, Story 23.1's already-accepted sunburst island measured 20,915 B — top-20 costs what an accepted payload costs. Filter timings: **44–75 ms** with `nodePositionsMoved: false`.

### References

- [Source: docs/adrs/0030-epic-24-graph-engine.md] — **the engine decision.** §1 engine, §2 position-is-data, §3 normative determinism construction, §4 filters-hide, §5 per-edge emphasis + width banding, §6 24.5 unchanged; "Consequential and still open" for the twin audit and `StripDataIslands`.
- [Source: _bmad-output/implementation-artifacts/24-6-spike-report.md] — §6.1/§6.2 a11y table + UX-DR7 evidence, §6.5 CSP, §7.1 determinism, §7.2 filter interaction, §7.3 at-scale, §7.4 defaults, §8 supersede + the twin-audit gap, §10 the hand-off table.
- [Source: docs/adrs/0012-…-standardized-explorer-component.md] — §2 component contract, §3 `navigate`|`select` mode grammar, §4 engine-family boundary, §6 tokens-not-colorways, §7 generation-time determinism.
- [Source: docs/adrs/0013-text-twin-is-the-no-js-contract.md] — §1 amended NFR-5, §2 the four twin properties, **§3 the hard per-surface gate**, §4 supersedes ADR 0010 §2, §6 fingerprint replacement, §7 webview fallback.
- [Source: docs/adrs/0011-directed-graph-edge-direction-carrier-to-target.md] — edge direction convention.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 24] — epic charter, FR40, UX-DR19/20/21, NFR8, execution order 24.1 → 24.6 → 24.2 → 24.3 → 24.4/24.5.
- [Source: _bmad-output/implementation-artifacts/24-1-directional-coupling-metric-foundation.md] — the metric spine, its four owner answers, and the `RelatedNode`/`ToGraphNodes` handoff.
- [Source: src/SpecScribe/CodeFileTemplater.cs] — `BuildRelationshipsPanel` (256), `RelatedNode` (289), `BuildRelatedNodes` (292), `ToGraphNodes` (315), `BuildAside` (393), `RefGraphVariants` (441), `BuildRelationshipsCard` (467), the sr-only twin (518–556), `EndShell` (827).
- [Source: src/SpecScribe/Charts.cs] — `ReferenceGraph` (1934), `RefGraphArtifactNodeCap` (1918), `ChartMetric.ChangeCoupling` (20/63).
- [Source: src/SpecScribe/HierarchyExplorer.cs] — `Render` (594), `LegendHtml` (712), `IslandHtml` (810), `TextTwinHtml` (976), `BootScript` (1091), `ContainsHost` (1098).
- [Source: src/SpecScribe/assets/specscribe.js] — tooltip `SEG` (103), the Hierarchy Explorer block (998+), zero-width defer/flush (1092–1128), failure unwind (1063–1080), `plotly_afterplot` a11y layer (1780+).
- Prior art: Story 7.1/7.8 (the reference graph being superseded), Story 10.2 (chart framing), Story 10.6 (Code-vs-Process lens), Story 20.5/20.7/20.9 (the component this mirrors), Story 20.4 (the a11y decision rule and the roving-index finding), Story 23.1 (payload-size baseline), Story 23.4 (`PageView`, and the joint ADR 0005 CSP amendment still owed).

### Open questions for the owner — do NOT block dev-start

1. **Lift vs confidence in the width bands.** 24.1's Q4 watch item, now at 20 nodes instead of 10. Measure on real data; if confidence still reads as a constant, propose the swap rather than taking it.
2. **`BuildAside`'s citations-only graph** (the placeholder-page path). Give it the component with an empty coupled population, or leave that one path on the retired SVG? Recommended: give it the component, so exactly one relationship renderer exists. Confirm in the verify round.
3. **The webview `StripDataIslands` decision** is deliberately taken here as "text twin fallback for now" (ADR 0013 §7). Narrowing the exception is a joint decision with the hierarchy family and would want its own ADR — flag it if this story makes the case obvious.

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (`claude-opus-5`), dev-story 2026-07-30. Baseline `94b8e56` (preserved from create-story); executed
against working tree at HEAD `70b72ab`.

### Debug Log References

- Determinism, cross-process: the rendered code page hashed **byte-identical across three separate `dotnet test`
  processes** (`500BD1FB…`). In-process repetition cannot see string-hash randomization, allocation-order effects or
  tiered JIT changing float contraction, so this is the check ADR 0030 §3 actually asks for.
- Golden fingerprint: `dbfa172b…` → `142a2132…` (implementation) → `70070c39…` (after the live-verification fixes).
  Each measured twice after `dotnet build --no-incremental`, both pairs stable. Provenance recorded in the constant's
  own comment block in `SiteGeneratorAdapterTests.cs`.
- Full suite: **2,859 passed / 0 failed / 3 skipped.** One earlier run lost
  `FileWatcherServiceTests.SprintStatusYaml_AddedThenEditedThenRemoved_RefreshesTheSprintSurfaceEachTime` to the
  documented git-SPAWN-starvation flake with the preview pane open; green 11/11 in isolation.

### Completion Notes List

**What shipped.** The code page's relationship surface is now one interactive ego graph
(`RelationshipGraph` + `CouplingLayout`, drawn by the already-vendored Plotly `scatter` trace over a
generation-time C# layout) plus the server-rendered sr-only twin it carries. `Charts.ReferenceGraph`, its
`RefGraphArtifactNodeCap`, the four pre-rendered `.ref-graph-view` panels, the pure-CSS `~`-sibling show/hide and
the whole `.ref-*` CSS family are **retired**. Marginal bundle cost is zero bytes — `scatter` was already
registered in the 1.2 MB hierarchy bundle.

**AC #3's gate PASSED — the ADR 0013 §3 twin audit, in a live browser with every `<script>` removed from the
document.** Measured on this repository's own `src/SpecScribe/Charts.cs` page, 0 scripts present:

| ADR 0013 §2 property | Result |
|---|---|
| **Server-rendered** | Twin present with no script having run; `display: block`, `visibility: visible`, clip-rect `sr-only` (in the a11y tree, not `display:none`) |
| **Complete — population 1** | **35** citing artifacts enumerated while the graph draws **14** (`ArtifactNodeCap`); 34 carry epic membership, 32 carry cross-edge disclosure |
| **Complete — population 2** | **20** coupled files (the D2 cap), 20/20 with support **and** directional confidence, 20/20 with lift on the row title, 9 marked cross-boundary, 2 marked process coupling, 20 carrying cross-edge disclosure |
| **Navigable** | 52 resolving anchors |
| **Non-colour** | every drawn distinction (dash, shape, width band, distance) also present as words |

The `+N more` disclosure survives the renderer change and is **stronger** than before: the ranking caption states
"21 further citing artifacts are listed in full below but not drawn", and 14 + 21 = 35 reconciles exactly. Assistive
technology and a JS-off reader both hold strictly more information than the chart shows.

**A twin-completeness DEFECT the audit found and this story fixed.** The graph draws process coupling as a dotted
spoke, and the twin could not say it: `RelatedNode` did not carry `CouplingKind`. That is precisely the "a fact
existing only inside the chart" ADR 0013 §2 forbids. `RelatedNode.ProcessCoupling` and the twin's
"· process coupling" words are the fix, not a note.

**D2's twin-completeness consequence, as predicted and recorded.** `ParseNumstatLog`/`TryComputeDeep` now take
`coupledCap`, and `SiteGenerator` passes `GitMetrics.RelationshipGraphCoupledCap = 20`. **The sr-only coupled list
therefore grows 8 → 20 on every code page** — a deliberate, visible behaviour change, exactly as Story 24.1
recorded the support floor reaching the per-file list. `FileInsightCoupledCap = 8` is unchanged for every caller
that does not ask. Still ONE git call and ONE commit scan: the wider cap is a `Take` on an already-computed,
already-floored, already-confidence-sorted list, and a test pins that the floor and sort are applied BEFORE the cap.

**The story's open question #2 has a third answer: `BuildAside`'s "second `Charts.ReferenceGraph` call site" was
UNREACHABLE.** Its only caller is `BuildPlaceholderPage`'s `!hasExtraTabs` branch, and `hasExtraTabs` is false only
when the relationships panel is empty — which requires no citers at all. So a page with citers always took the
tabbed branch and that graph could never draw. Neither offered option ("keep the SVG there" / "give it the
component") applied; the dead branch was deleted with the SVG, leaving exactly ONE relationship renderer. Pinned by
`CodeFileTemplaterTests.PlaceholderPage_WithCiters_RendersTabsNotAnAsideGraph` rather than left as a reasoning claim.

**Test accounting for the retirement.** 15 test methods / 43 assertion statements referencing `Charts.ReferenceGraph`
or `RefGraphArtifactNodeCap` were deleted from `ChartsTests.cs` (the story estimated 37 assertions; the measured
count is 43). No orphaned dead tests remain — `grep` for both symbols returns zero across the repository. The
`CountOccurrences` helper in that file was used only by the deleted block and went with it.

**Sonar `external_roslyn:CA1859` on `ToGraphNodes`: RESOLVED by deletion.** The projection seam existed only to feed
`Charts.ReferenceGraph`'s 4-tuple. Retiring the SVG removed its only consumer. The attribution handoff is discharged:
`RelatedNode`'s metric members and `ToGraphNodes` sat in Story 24.1's File List while self-attributing to 24.2, and
this story's review covers them — `RelatedNode` is now documented as 24.2-owned, `ToGraphNodes` is gone. Deleting
`ReferenceGraph` also retired the single worst finding in `Charts.cs`: an `S3776` at **cognitive complexity 89**,
plus its `S107` (9 parameters). `Charts.cs` shrank 3,969 → 3,647 lines.

**ADR 0013 §6's fingerprint replacement landed in this same change**, as three named generation-level tests over a
real generated site (`SiteGeneratorCodeInsightsTests.GoldenReplacement_*`): the embedded **payload** (every node and
edge, the pinned focal coordinate, no `NaN`/`Infinity`), the component **configuration** (domId, title, size, token
names, the server-resolved style table, no `--status-*`), and the **twin** (both populations, server-rendered,
navigable, non-colour). Deliberately three tests and not one hash: a fingerprint says *something* moved, which is
what made it noisy; these say *what*.

**Payload size: measured, then halved.** The first working island was **55,012 B** on `Charts.cs`, of which
**30,820 B (56%) was 203 cross-edge sentences** each re-spelling two full repository paths already present in the
node array. Moving the per-kind facts (governing filter + describing phrase) into ONE config row per kind, with
`{a}`/`{b}` substitution client-side, took it to **27,003 B** — the wording still authored once, in C#. A coupling
spoke keeps its own sentence because support/confidence/lift are facts about the pair and no template can express
them.

**Output-size delta across a real `--deep-git` portal** (working tree vs a `git archive HEAD` throwaway tree, both
generated with `--deep-git`; compared over the **263 code pages present in both**, since the two trees have
different page populations and a whole-portal total would be meaningless):

| | Before | After | Delta |
|---|---:|---:|---:|
| 263 common code pages | 21,842,637 B | 29,801,495 B | **+7,958,858 B (+36.4%), avg +30,262 B/page** |
| `specscribe.css` | 321,960 B | 322,005 B | +45 B (the retired SVG's rules ≈ the new graph's) |
| `specscribe.js` | 150,627 B | 182,992 B | +32,365 B, once, site-wide |

The portal has **266 code pages**. Everything else that differs between the two trees (`code-map.html` +1.15 MB,
`risk-quadrant.html` +0.95 MB, the concurrent session's new story pages) is population drift from the archived tree
having fewer source files — **not this story**, and stated as such rather than folded into the number.

**THE LIVE-BROWSER PASS FOUND FOUR DEFECTS THE SUITE STRUCTURALLY COULD NOT SEE.** Story 24.1 predicted this class
in this same panel; all four are rendered geometry or rendered honesty:

1. **The control bar never un-hid.** It was emitted `hidden` correctly and no code revealed it on mount, so both
   filters were invisible to every scripted reader. Every assertion about the `hidden` attribute passed — emitting
   it was never the missing half.
2. **The legend rendered with JS off** — eight rows explaining gold circles, dash patterns and width bands, above a
   `display:none` chart host. Now `hidden` until mount (with the `[hidden]`-vs-author-`display` specificity override
   the control bar already needed), and the ranking caption dropped "the strongest are drawn nearest the centre",
   which misdescribed a page where nothing is drawn.
3. **Nodes drawn outside the host.** `yaxis.scaleanchor: 'x'` on a wide-short panel (886×420) makes Plotly SHRINK
   the y range to match x's px-per-unit — 764 px/unit leaves y showing 0.55 units, so everything outside
   0.225–0.775 fell beyond the visible box. Measured: a 618 px vertical spread inside a 420 px host. Anchoring
   **x to y** keeps the short axis whole. Zero markers outside the host now.
4. **20 overlapping marker pairs**, worst at **40%** of the separation its two markers needed. Two causes, both
   fixed: the 203 ring-to-ring cross edges dragged the coupled arc into a knot (attraction is now normalised by ring
   degree, and drift is **bounded to ±35% of the natural spacing around each node's evenly-spaced home**, which makes
   non-collision arithmetic rather than empirical); and 40 markers simply did not fit a 420 px ring (host height
   420 → **520**, marker band capped at 24 px, radius band widened to 0.30–0.46). Re-measured: **4** grazing pairs,
   worst ratio **0.82**. The bound is now pinned by a test over a fully-connected 35-node ring.

**Live verification results** (all on the real generated portal, `file://`):

- **Zero-width mount trap handled.** With Insights as the default tab the graph host is `display:none` at mount; the
  component **deferred** rather than drawing a wrong-size chart, and the tab radios' `data-relgraph-reveal` marker
  flushed the pending mount on reveal. Confirmed `ready=null` before, `mounted=1` after.
- **A11y layer survives 5/5 re-render events**, including the adversarial **bare `Plotly.react` the component did
  not initiate**, a `Plotly.Plots.resize`, a `Plotly.relayout`, and the shipped `specscribe:content-swapped` SPA
  seam. Survival predicate held every time: nodes > 0, every node has a role, every node a non-empty `aria-label`,
  exactly one `tabindex="0"`, and every node a non-zero bounding box.
- **Asserted on GEOMETRY, not attributes** (the spike's hand-off): 0 edge segments with `d=""`, 0 markers with a
  zero box, real bounding-box overlap arithmetic. The ECharts failure mode — every a11y attribute passing over a
  chart drawing nothing — cannot pass here.
- **Real keys, not synthetic dispatch.** `ArrowRight` moved the roving focus, announced through the live region,
  with exactly one tabbable node and a 2 px solid focus ring. `Enter` on a linked node fired `beforeunload` and its
  href resolves to a real page (HTTP 200) — the preview pane's static-snapshot context blocks the document swap
  itself, so navigate mode is verified by intent + reachable target rather than by a completed navigation.
- **Tooltip: ZERO clipping ancestors**, parented to `BODY`, inside the viewport. Routed through the shared
  `.ss-tooltip` node via the `SEG` family, never a CSS `::after`.
- **0 PAINTED foreign colours**, under a hardened predicate that also checks channel opacity and the ancestor
  opacity chain. One raw hit — Plotly's `rect.bg` at `fill-opacity: 0` — is reported rather than filtered away, so
  the 0 is defensible rather than lucky.
- **Filters hide, they never re-lay-out (ADR 0030 §4), verified on real pixels**: toggling both filters took 35→40
  nodes and 34→250 edges, and of the 35 survivors **0 moved a single pixel**. An earlier mid-flight reading showed
  20 "moved" — that was a genuine transient (the node trace briefly held 40 positions and 35 marker entries between
  two `restyle` calls, and `plotly_afterplot` fires inside that window), now closed by putting the node trace's
  geometry and marker arrays in ONE call.
- **Reduced motion (UX-DR18):** there is no settle animation, and **0** elements inside the chart carry any
  transition — so nothing Plotly owns is animated from CSS.
- **Mobile 375 px:** host resizes to 280×420, all 35 markers inside it, panel does not overflow (325/325), controls
  wrap. The page's horizontal overflow is `.code-tablist` at 449 px — **inherited**, recorded by project memory as
  measuring identical on the golden site, and not from this panel (verified by walking every overflowing element).

**Webview / SPA, per the story's two named open boundaries.** `WebviewRenderAdapter.StripDataIslands` removes every
`<script type="application/json">`, so the webview cannot receive a graph payload today; this story takes the
**ADR 0013 §7 text-twin fallback** and the twin is SERVER-rendered — the spike measured a client-built twin
contributing **0 bytes** under a blocked script, which is why that is not negotiable. The webview code page does not
ship an empty box: the host is `display:none` until a mount that cannot happen there, and the boot placeholder is
gated on an inline chrome script the webview never receives. Narrowing the exception remains a joint decision with
the hierarchy family and would want its own ADR; this story did not make the case obvious enough to propose one.

**Story open question #1 (lift vs confidence in the width bands): MEASURED on real data, and it resolved itself.**
Story 24.1's Q4 watch item was that this repository's visible top-10 came back at 100% confidence, so the column did
no ranking work. At the D2 cap of 20 that is no longer true. Measured on `src/SpecScribe/Charts.cs`'s own twin, the
20 coupled files read:

> 75, 73, 69, 67, 45, 44, 35, 31, 27, 18, 18, 18, 18, 17, 16, 15, 15, 14, 13, 13 (%) — **15 distinct values across
> a 13%–75% range**

Confidence genuinely discriminates at 20 where it did not at 10, so **no ranking-policy change is proposed** and the
graph does not need to swap to lift. Raised for the owner's verify round rather than taken unilaterally. Separately, the graph does **not** encode confidence in width at all: width is
banded by shared commits (ADR 0030 §5 forces banding), and confidence rides the continuous RADIUS channel plus the
tooltip and the twin.

**Deviation, recorded.** The story specified the roving-tabindex reading order as "degree-descending, then weight,
then ordinal path". Implemented instead as the **server's own emission order**, because the requirement that clause
serves — "twin and graph must agree" — is met exactly by it and *not* by degree ordering: the twin lists citing
artifacts first and then coupled files confidence-desc, whereas a degree ranking would put a high-degree coupled
file ahead of the citers and disagree with the listing directly beneath it. The degree-desc recommendation came from
the spike's whole-repo fixture, where there is no two-population ordering to match.

### File List

**New**

- `src/SpecScribe/CouplingLayout.cs` — the deterministic generation-time solver (pinned focal, bounded ring relaxation).
- `src/SpecScribe/RelationshipGraph.cs` — the component (skeleton, legend, style table, island, twin enforcement, `ContainsHost`, `BootScript`).
- `tests/SpecScribe.Tests/CouplingLayoutTests.cs`
- `tests/SpecScribe.Tests/RelationshipGraphTests.cs`

**Modified**

- `src/SpecScribe/GitMetrics.cs` — `RelationshipGraphCoupledCap`; `coupledCap` threaded through `ParseNumstatLog` and `TryComputeDeep`.
- `src/SpecScribe/CodeFileTemplater.cs` — relationships card rewritten onto the component; `RelationshipGraphModel`/`BuildRelationshipsTwin`/`BuildRankingCaption`/`RelatedDetail` extracted; `RefGraphVariants`, `ToGraphNodes` and `BuildAside`'s dead graph branch deleted; `RelatedNode.ProcessCoupling` added; `RefGraphGroupSlug` → `RelGraphDomSlug`; tab radios carry the reveal marker; `EndShell` derives the asset flags.
- `src/SpecScribe/Charts.cs` — `ReferenceGraph` + `RefGraphArtifactNodeCap` retired (322 lines).
- `src/SpecScribe/SiteGenerator.cs` — passes `RelationshipGraphCoupledCap` to the single deep-git fetch.
- `src/SpecScribe/AssetManifest.cs` — `GraphEngineNeeded` / `GraphBootInline`.
- `src/SpecScribe/HtmlRenderAdapter.cs` — emits the graph boot script; the bundle tag is emitted once for either engine flag.
- `src/SpecScribe/EpicsViewBuilder.cs` — doc-comment reference repointed to `RelationshipGraph.ArtifactNodeCap`.
- `src/SpecScribe/assets/specscribe.js` — the client renderer, filters and a11y layer; `.ss-relgraph-node`/`-edge` joined the shared tooltip `SEG` family.
- `src/SpecScribe/assets/specscribe.css` — `.ss-relgraph*` family added; the whole retired `.ref-*` / `.refgraph-toggle*` family and `.code-relationships > h2` / `.code-relationships-note` deleted.
- `tests/SpecScribe.Tests/ChartsTests.cs` — 15 retired `ReferenceGraph` tests (43 assertions) removed.
- `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs` — rewritten onto the component contract; new coverage for the reveal marker, framing, legend honesty, filters, island geometry and the `BuildAside` reachability pin.
- `tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs` — updated to the new markup; the three ADR 0013 §6 `GoldenReplacement_*` tests added.
- `tests/SpecScribe.Tests/SiteGeneratorCodeCitationTests.cs` — updated to the new markup.
- `tests/SpecScribe.Tests/StylesheetTests.cs` — asserts the new family and the absence of the retired one.
- `tests/SpecScribe.Tests/GitMetricsFileInsightsTests.cs` — cap-threading and floor-before-cap coverage.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden fingerprint regenerated twice, with provenance.
- `_bmad-output/implementation-artifacts/sprint-status.yaml`, `24-2-per-file-ego-coupling-graph.md` — status records.

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-30 | Story 24.2 implemented (dev-story, baseline `94b8e56`, executed at HEAD `70b72ab`): in-progress → review. All 8 tasks complete. AC #3's gating twin audit **PASSED** in a live browser with 0 scripts on the page (35 citers vs 14 drawn, 20 coupled, 52 links, every metric as words) — so `Charts.ReferenceGraph`, `RefGraphArtifactNodeCap`, the four SVG variants and the whole `.ref-*` CSS family were retired; 15 tests / 43 assertions deleted and accounted for. The audit itself found a twin-completeness defect (process coupling was drawn but unsayable) and this story fixed it. The live pass found **four** further defects the suite structurally could not see — an unrevealed control bar, a legend rendering with JS off, nodes drawn outside the host from an inverted aspect anchor, and 20 overlapping markers — all fixed and re-measured. ADR 0013 §6's fingerprint replacement landed as three `GoldenReplacement_*` tests over a real site. Payload halved 55,012 B → 27,003 B by single-sourcing per-kind edge phrasing. Golden fingerprint `dbfa172b…` → `142a2132…` → `70070c39…`, each verified twice after a non-incremental rebuild, provenance recorded. Suite 2,859 passed / 0 failed / 3 skipped. |
| 2026-07-29 | Story 24.2 created (baseline `94b8e56`). All three gates verified clear: 24.1 `review`, 20.7 `done`, 24.6 `review` with ADR 0030 Accepted. Four owner decisions elicited up front and all taken at their recommended defaults — D1 evolved hub-and-spoke, D2 top-20 via a graph-scoped cap, D3 both toggles as client edge filters (four pre-rendered variants deleted), D4 the unowned ADR 0013 §3 twin audit folded in as gating Task 6. AC #2 amended (SVG retired, not retained) and AC #3 added for the audit + fingerprint replacement. Status → ready-for-dev. |
