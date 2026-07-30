---
baseline_commit: 94b8e56fc297e94640f5bcdc5b568ed1394033ea
---

# Story 24.2: Per-File Ego Coupling Graph (Force-Directed) on Code Pages

Status: ready-for-dev

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

- [ ] **Task 1 — Widen the coupled population to the graph cap** (AC: #1, D2)
  - [ ] Add a named const for the code-page relationship surface, e.g. `public const int RelationshipGraphCoupledCap = 20`, beside `FileInsightCoupledCap` ([GitMetrics.cs:926](src/SpecScribe/GitMetrics.cs)). **Leave `FileInsightCoupledCap = 8` at its current value** — it stays the default for any caller that does not ask for more.
  - [ ] Thread `coupledCap` through `ParseNumstatLog` → `BuildFileInsights` **exactly as Story 24.1 threaded `minSupport`** ([GitMetrics.cs:660](src/SpecScribe/GitMetrics.cs) is the single call site; `BuildFileInsights` already takes `coupledCap` at [GitMetrics.cs:956/970](src/SpecScribe/GitMetrics.cs)). One optional parameter with a default, no new CLI flag — same shape 24.1's Q3 settled on.
  - [ ] **Do NOT add a second git call or a second commit scan** ([[deep-git-single-numstat-path]]). The wider cap is a `Take` bound on an already-computed, already-sorted, already-floored list.
  - [ ] The support floor (`CouplingMinSupport = 2`) and the confidence sort are applied **before** the cap — that ordering is already correct in `BuildFileInsights`; verify it still is after the cap change rather than assuming.

- [ ] **Task 2 — Generation-time layout solver (node position is DATA)** (AC: #1, D1)
  - [ ] New file `src/SpecScribe/CouplingLayout.cs`. Pure, repo-free, no I/O. Input: the focal file + the two node populations + the edge set. Output: embedded coordinates.
  - [ ] **D1 shape:** focal node pinned at the canvas center and excluded from the relaxation. Citing artifacts and coupled files relax on a ring so labels do not collide; the hub-and-spoke read must survive.
  - [ ] **ADR 0030 §3's construction is NORMATIVE, not advisory. All four apply:**
    - [ ] **No `System.Random`.** Use a private seeded PRNG (the spike used xorshift128+ with a compile-time seed). `Random`'s algorithm is a documented implementation detail that may change between .NET versions — determinism would expire silently on an SDK bump.
    - [ ] **No `Dictionary`/`HashSet` iteration order may reach a floating-point accumulation.** Materialize every collection through an explicit **ordinal sort** first. Floating-point addition is not associative; an order change moves the last bits of every coordinate.
    - [ ] No wall-clock, no environment, no parallelism.
    - [ ] All formatting through `CultureInfo.InvariantCulture` with a fixed format string.
  - [ ] **Choose the coordinate/confidence rounding precision deliberately and say why in a comment.** The spike found 4-decimal rounding *collapses distinct confidence values* (452 survive where 453 exist upstream). Harmless at this precision, but it makes precision a **data** decision, not a cosmetic one.
  - [ ] Verify determinism **by repetition across separate processes**, not by assertion — in-process repetition cannot see string-hash randomization, allocation-order effects, or tiered JIT changing float contraction.

- [ ] **Task 3 — The graph component (server side)** (AC: #1)
  - [ ] New file `src/SpecScribe/RelationshipGraph.cs` — a **sibling of `HierarchyExplorer`, not a reuse of it**. The data shape is nodes + edges, not a hierarchy; `HierarchyNode`/`HierarchyExplorerModel` do not fit. What must be **identical** is the *contract* (ADR 0012 §2, §4): one datasource per instance, one selector/control idiom, one framing block, one mandatory text twin.
  - [ ] Mirror `HierarchyExplorer.Render`'s emitted skeleton ([HierarchyExplorer.cs:594](src/SpecScribe/HierarchyExplorer.cs)) — read it in full before writing this: `hidden` control bar → boot placeholder (`role="status"`, sized to reserve height) → empty chart host carrying a host marker → `aria-live="polite"` region → legend → payload island → text twin, all wrapped by `Charts.Framed`.
  - [ ] **Framing (Story 10.2): reuse `Charts.WhyText(ChartMetric.ChangeCoupling)`** ([Charts.cs:63](src/SpecScribe/Charts.cs)). Do NOT hand-roll new "why" copy at the call site. Ranking caption goes in `ChartMeta.Ranking`; data caveat in `ChartMeta.Note`.
  - [ ] **Mode is `navigate`** (ADR 0012 §3) — activating a node follows its `href` to that file's code page. `select` mode and a details pane are **not** in scope here; if the verify round wants one, it must use the shipped `specscribe:explorer-select` seam, never a parallel event (ADR 0030 §1).
  - [ ] **Legend must describe the channel actually on screen.** ADR 0030 §5: emphasis is carried by **dash pattern, width band, node shape and accessible text — never by hue** (UX-DR17). Because `scatter` line style is a **trace-level** attribute, per-edge styling means **one trace per style class**, which **quantizes stroke width into bands**. A legend showing a continuous scale beside a banded chart is the "misdescribing entry" class Stories 10.7 and 21.1 each closed.
  - [ ] **Therefore: confidence must be legible from the tooltip and the text twin, and must NOT be encoded in stroke width alone.** This is a named ADR 0030 consequence, not a nicety.
  - [ ] Add a `ContainsHost(string bodyHtml)` analogue so the `AssetManifest` engine flag is derived **from the rendered body**, never hand-set — mirroring `HierarchyExplorer.ContainsHost` ([HierarchyExplorer.cs:1098](src/SpecScribe/HierarchyExplorer.cs)) and `Mermaid.ContainsBlock`. A flag derived from the page cannot disagree with the page.

- [ ] **Task 4 — Client renderer + accessibility layer** (AC: #1, #2)
  - [ ] Extend `src/SpecScribe/assets/specscribe.js`. Read the Hierarchy Explorer block ([specscribe.js:998–1150+](src/SpecScribe/assets/specscribe.js)) first and follow its idioms — mount registry, purge-on-removal, cleanup handle, failure unwind.
  - [ ] Plotly's documented network recipe: **edges = a `scatter` trace with `mode:'lines'`, nodes = a `scatter` trace with `mode:'markers'`**, layout supplied externally. **No client-side force simulation, no iterative solver, no physics** (ADR 0030 §2).
  - [ ] **Per-edge hover needs an auxiliary invisible midpoint trace** — a `lines` trace hovers on *vertices*, not segments (ADR 0030 "Bad, or at least costly").
  - [ ] **Hang the a11y layer on `plotly_afterplot`, never on the promise `Plotly.react` returns.** The spike verified the layer survives **8/8** re-render events including a **bare `Plotly.react` the component did not initiate** and the shipped **`specscribe:content-swapped`** SPA seam. A layer surviving only the component's own redraw is a **FAIL** under Story 20.4's decision rule.
  - [ ] **Clamp the roving `tabindex` index on EVERY reapply.** Story 20.4's sixth finding was an unclamped roving index leaving the chart Tab-unreachable after the node count shrank. The 24.6 probe fixed it by construction; **24.2 must keep the clamp.**
  - [ ] **Reading order = degree-descending, then weight, then ordinal path — deliberately matching the text twin's order** (Story 24.1's Q4 ordering), not the DOM order Plotly happens to emit. Twin and graph must agree.
  - [ ] Survival predicate to assert after every event: *nodes > 0 **and** every node carries a role **and** every node carries a non-empty `aria-label` **and** exactly one node holds `tabindex="0"`.*
  - [ ] Tooltips route through the **body-level `.ss-tooltip` node**, not a CSS `::after` ([[tooltip-clipping-use-ss-tooltip-node]]). Opt the graph markers into the existing `SEG` selector family the way `.ss-hierarchy-sector` does ([specscribe.js:103](src/SpecScribe/assets/specscribe.js)) — one tooltip system site-wide.
  - [ ] **Presentation is SpecScribe's tokens, never Plotly's colorways** (ADR 0012 §6). Neutral ink/gold/border tokens only — the `--status-*` lifecycle tokens are **off-limits on code surfaces** (existing `ReferenceGraph` doc comment states this rule; keep it).
  - [ ] **Reduced motion (UX-DR18):** there is no settle animation to suppress (position is precomputed), but any transition used for filtering must snap under `prefers-reduced-motion`. Drive it from the `--motion-*` tokens ([[motion-token-system]]); never `transition` a Plotly-owned property ([[story-20-5-hierarchy-explorer-done]]).

- [ ] **Task 5 — Absorb the two toggles as client edge filters; delete the four variants** (AC: #1, D3)
  - [ ] Delete `RefGraphVariants` and the four-panel pre-render loop ([CodeFileTemplater.cs:441](src/SpecScribe/CodeFileTemplater.cs), [CodeFileTemplater.cs:506–516](src/SpecScribe/CodeFileTemplater.cs)) together with the pure-CSS `~`-sibling show/hide rules in `specscribe.css` and the `RefGraphGroupSlug` id-uniqueness helper if nothing else needs it.
  - [ ] Re-implement **both** affordances as edge-visibility filters over the **single** layout: "Show relationships" toggles the cross-edge traces; "Group by epic" toggles the epic-hub edges. **Surviving nodes do not move** (ADR 0030 §4).
  - [ ] Controls go **inside** the component's `hidden` control bar so they inherit the reveal handshake — a JS-off visitor must never see an inert control. This is the same convention `ss-hierarchy-controls` / `codemap-controls` already follow.
  - [ ] The cross-edge data (`BuildStoryRelatedEdges` / `BuildRelatedRelatedEdges`, [SiteGenerator.cs:2809/2842](src/SpecScribe/SiteGenerator.cs)) is **index-aligned** with the related-node list ([SiteGenerator.cs:2806](src/SpecScribe/SiteGenerator.cs) doc comment). Widening the cap in Task 1 changes that list's length — **re-verify the alignment holds**, do not assume.

- [ ] **Task 6 — ⛔ GATING: ADR 0013 §3 text-twin audit, then the fingerprint replacement** (AC: #3)
  - [ ] **This task blocks Task 7. Nothing is deleted until it passes.**
  - [ ] Audit the code page's server-rendered twin against ADR 0013 §2's four properties — **server-rendered · complete · navigable · non-color** — for **BOTH** populations: citing artifacts (`ref-list` items with epic membership and `BuildStoryCrossSuffix`) **and** coupled files (`ref-list-related` sub-list with support, confidence, cross-boundary words, lift-on-title, `BuildRelatedCrossSuffix`). See [CodeFileTemplater.cs:518–556](src/SpecScribe/CodeFileTemplater.cs).
  - [ ] **Verify in a live browser with JavaScript disabled** ([[browser-pane-five-server-cap-file-url-fallback]] — verify over `file://` rather than stopping another session's server; note `navigate` STRIPS the hash). CLAUDE.md § Verification applies with full force: *the test suite structurally cannot see what a JS-off visitor actually gets.*
  - [ ] Also audit the **`+N more`** honesty disclosure. `RefGraphArtifactNodeCap = 14` bounds what the graph **draws**; the sr-only list already enumerates **all** citers. Confirm that survives the rework — assistive tech must never have less information than the richest sighted view.
  - [ ] **Land the ADR 0013 §6 fingerprint replacement in this same change.** Move assertions to the **embedded payload, the component configuration, and the twin**. The golden fingerprint stops covering this chart's geometry; if nothing replaces it, chart regressions go un-netted.
  - [ ] Record the audit result in the story's Dev Agent Record. **An incomplete twin keeps its SVG** — that is the ADR's rule, and reporting the gap is the correct outcome, not a failure of the story.

- [ ] **Task 7 — Retire the `ReferenceGraph` SVG (only after Task 6 passes)** (AC: #2, #3)
  - [ ] **`Charts.ReferenceGraph` has TWO call sites, not one.** The obvious one is `BuildRelationshipsCard` ([CodeFileTemplater.cs:509](src/SpecScribe/CodeFileTemplater.cs)). The second is **`BuildAside`** ([CodeFileTemplater.cs:417](src/SpecScribe/CodeFileTemplater.cs)), reached from the **placeholder** page path when a file has no extra tabs ([CodeFileTemplater.cs:781](src/SpecScribe/CodeFileTemplater.cs)) — a citations-only graph with **no coupling data at all**. Decide its fate explicitly (keep the SVG for that path, or give it the component with an empty coupled population) and **state the decision in the completion notes**. Missing it ships a compile error or a silently blank sidebar.
  - [ ] `Charts.ReferenceGraph` is referenced by **37 assertions across `tests/SpecScribe.Tests/ChartsTests.cs`**. Retiring it means deleting or rewriting them — count and account for them; do not leave orphaned dead tests.
  - [ ] **Attribution handoff, per CLAUDE.md § Scoping a code review:** `RelatedNode`'s metric members (`Support`, `Confidence`, `Lift`, `CrossBoundary`) and **`ToGraphNodes`** sit in **Story 24.1's File List** but their doc comment **self-attributes them to Story 24.2** ([CodeFileTemplater.cs:285–317](src/SpecScribe/CodeFileTemplater.cs)). **This story's code review must cover them.** Recorded here so they cannot fall between the two reviews.
  - [ ] Sonar already flags `external_roslyn:CA1859` on `ToGraphNodes` (return type could be concrete). Since 24.2 owns that symbol, resolve or explicitly waive it while you are there.

- [ ] **Task 8 — Wiring, tests, and live-browser verification** (AC: #1, #2, #3)
  - [ ] **Asset manifest:** code pages need the Plotly bundle. Extend `AssetManifest` ([AssetManifest.cs:24](src/SpecScribe/AssetManifest.cs)) and wire it in `EndShell` ([CodeFileTemplater.cs:827–852](src/SpecScribe/CodeFileTemplater.cs)). Note the code page **already uses `ExtraHead`** for Prism — do not clobber it. Keep the flag `false`-defaulted so a page without a graph stays byte-identical (1.2 MB is not a rounding error).
  - [ ] **Measure the total output-size delta across a real `--deep-git` portal.** ~20 KB of island per code page across a large code-page population is a real number; the true page inventory is **1,408 pages** ([[specscribe-true-page-inventory-1408]]) and two pages are already on the `oversizedPages` list. Report the before/after, the way ADR 0012's spike concern #2 required for hierarchies.
  - [ ] Full suite. **Golden fingerprint WILL move.** Regenerate deliberately, `dotnet build --no-incremental` first (embedded `.css`/`.js` assets are cached by an incremental build, so the hash you measure is stale), confirm stable across **two repeated runs**, and split the provenance — say whose changes yours sat on top of ([[golden-diff-normalization-gotchas]], CLAUDE.md § Concurrent work).
  - [ ] **Live-browser verification is mandatory and is where the real defects will be.** Story 24.1's live pass caught two rendered-geometry defects the suite structurally could not see, both in this same panel. Verify: the ~320–360px sidebar/tab width, label collision at 21 nodes, no clipped cells, focus ring visibility, real `ArrowRight`/`Enter`/`Escape` keys (not synthetic dispatch), and the tooltip's zero clipping ancestors.
  - [ ] **Assert on GEOMETRY, not attributes.** The spike's hand-off: an attribute-only audit certified an ECharts chart that was **drawing nothing** (every path `d=""`, every symbol `scale(0)`) while every a11y attribute passed. And per Story 20.4, do not assert on the console either.

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

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-29 | Story 24.2 created (baseline `94b8e56`). All three gates verified clear: 24.1 `review`, 20.7 `done`, 24.6 `review` with ADR 0030 Accepted. Four owner decisions elicited up front and all taken at their recommended defaults — D1 evolved hub-and-spoke, D2 top-20 via a graph-scoped cap, D3 both toggles as client edge filters (four pre-rendered variants deleted), D4 the unowned ADR 0013 §3 twin audit folded in as gating Task 6. AC #2 amended (SVG retired, not retained) and AC #3 added for the audit + fingerprint replacement. Status → ready-for-dev. |
