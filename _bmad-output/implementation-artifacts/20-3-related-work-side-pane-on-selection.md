# Story 20.3: Related-Work Side Pane on Selection

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a Driver inspecting one item,
I want a side pane that lists the work-graph nodes related to my current selection,
So that "what stemmed from what" is visible beside the map instead of buried in per-page reverse panels.

## ⛔ Read first — sequencing gate (do NOT skip)

This is the **third and final** story of Epic 20 and it is **gated on two siblings that are not yet done**. Confirm the gate before writing code:

| Prerequisite | Status at create-story (2026-07-22) | Why it gates this story |
|--------------|-------------------------------------|-------------------------|
| **Story 20.2** — Zoomable Drill-In Sunburst Navigation | `ready-for-dev` (seeded 2026-07-22, **not yet built**) | Owns the explorer root marker (`data-explorer` on the sunburst-panel div) + the **payload island** + the **selection/zoom mechanism**. Its story file **explicitly reserves `edges: []` in that island for this story** (20-2 lines 75, 188). AC #1's "updates as the selection changes" binds to 20.2's client state — real but unimplemented. |
| **Story 20.1** — Interactive Explorer Architecture Spike | `ready-for-dev`, **Completion Notes EMPTY** | The boundary/budget/degrade spike. Its formal notes are still unrecorded, **but 20.2's create-story already committed the concrete payload+id+root contract** (below), so 20.3 has a real seam to build against even before 20.1's notes land. |
| **Epic 19** — Directed Work Graph | ✅ **DONE / merged to main** (`WorkGraph.cs` at HEAD `38044b1`; merge `7d8882e`) | The pane's relationship data source. The edges ship — `SiteGenerator._workGraph` is already computed and cached. |

**The concrete integration seam (from 20.2's committed contract — build against this):**
- **Payload island:** ONE inline `<script type="application/json">` in the sunburst host page, shape `{ nodes: [{ id, parentId, weight, label, statusClass, href, kind }], edges: [] }`. **20.2 leaves `edges` empty on purpose — 20.3 fills it.** (20-2 line 75.)
- **Island node ids are canonical display ids:** epic → `"epic-N"`, story → `"N.M"`, follow-up → slug, aggregate → group href. **These are NOT the work-graph model's internal ids** (`e{N}`, `s{id}`, `d{epic}-{i}`…) — the id-bridge (Task 3) maps between them.
- **Explorer root:** `data-explorer` / an `explorer-root` class on the existing sunburst-panel container; JS opts in on it (mirrors `.codemap-view`).

**Consequence — the story splits into two halves with different readiness:**

1. **Server-rendered "Related" data + block (AC #2, the no-JS baseline).** Buildable **now** — projects `SiteGenerator._workGraph` (already computed) into per-node related-lists in the explorer host page. Does **not** require 20.2 to be built. Ship-first, load-bearing (NFR8).
2. **Client-side pane hydration (AC #1's "updates as selection changes") + the island `edges` array.** Binds to 20.2's selection state + payload island. The client only *reveals the slice* of the already-server-rendered relationship data that matches the current selection.

**Recommended action for the dev agent:** build **Half 1 in full now**, and add the `edges` array to 20.2's payload island + the reveal-on-selection block. If 20.2 has not been *implemented* when you pick this up, still emit the `edges` into the island (20.2 reserved the slot) and gate the reveal block behind the `data-explorer` root check — it no-ops cleanly with no explorer, exactly like every other `specscribe.js` opt-in block, so shipping ahead of 20.2 is inert and safe. **Do not invent a selection mechanism or a second island** — coordinate with 20.2. If the owner wants to hold until 20.2 is built, raise it via `correct-course`.

## Acceptance Criteria

1.
**Given** a selected explorer node and Epic 19's directed edges
**When** the pane renders
**Then** it groups related nodes by edge kind, each entry linking to its detail page
**And** the pane updates as the selection changes, reusing Epic 19's edges and Epic 9's parsers without re-counting open items against `ProjectCounts`.

2.
**Given** a selection with no work-graph edges, or a JavaScript-off visitor (NFR8)
**When** the pane would otherwise be empty or unhydrated
**Then** an empty selection shows a designed empty state
**And** with JS off the relationship data is still delivered as a **server-rendered "Related" block**, never JS-gated.

### AC interpretation notes the dev MUST honor (reconciled against shipped code)

- **"group related nodes by edge kind" — use the FOUR kinds that ship, not the epics.md list.** epics.md AC #1 prose says *"stemmed-from, resolves, covers, cites, raised-in"*, but the **shipped** `WorkEdgeKind` (Story 19.2, `src/SpecScribe/WorkGraph.cs:24`) has **only** `Contains`, `StemmedFrom`, `Resolves`, `RaisedIn`. **`covers` (requirement) and `cites` (code) nodes/edges are DELIBERATELY out of the 19.2 MVP draw** (`WorkGraph.cs:5-8`, `WorkNodeKind` doc-comment: *"covers/requirement and cites/code nodes are deliberately out of the MVP draw"*). Group the pane by the **four real kinds**; surface `covers`/`cites` **only if/when Epic 19 adds them** (see "Forward-compatibility" below). Do **not** manufacture a `covers`/`cites` grouping the graph can't populate — that would be a phantom, exactly the class of over-claim [[story-7-11-7-12-code-review-shared-engine-merge]] warns about. State this reconciliation in Completion Notes so the reviewer doesn't flag it as a missed AC.
- **"the pane updates as the selection changes"** is the **interactive** clause — it binds to 20.2's selection signal (Half 2). With JS off there is no selection to change; AC #2 governs that path (the server-rendered block).
- **"reusing Epic 19's edges and Epic 9's parsers without re-counting":** the pane data is a **pure read** of `_workGraph` (which is itself a pure projection over `FollowUpGeometry` + `EpicsModel` that already *"never re-counts open items against `ProjectCounts`"* — `WorkGraph.cs:80-84`). Do **not** call `ProjectCounts`, do **not** re-run Epic 9 parsers, do **not** re-project the graph. Reuse `SiteGenerator._workGraph` **verbatim**.

## Tasks / Subtasks

- [ ] **Task 1 — Reuse the already-computed work graph; do NOT re-project (AC: #1)**
  - [ ] Confirm `SiteGenerator._workGraph` (`SiteGenerator.cs:63`, populated at `:206` via `BuildWorkGraphModel`) is available at the point the explorer host page (dashboard `index.html` and/or `epics.html`) is rendered. It is computed once and cached *"reused verbatim by WriteWorkGraph"* (`:205-206`) — the pane reads the same instance.
  - [ ] Add **no** new call to `WorkGraphBuilder.Build`, `FollowUpGeometry`, `RequirementsParser`, or `ProjectCounts` from this story. If the host-page render site can't see `_workGraph`, thread the existing model in — do not recompute.

- [ ] **Task 2 — Build the per-node adjacency the pane consumes (AC: #1)**
  - [ ] Add a **pure projection** (recommended home: a static helper next to the model, e.g. `WorkGraph.RelatedFor(...)` in `WorkGraph.cs`, or a small `RelatedWorkView` builder) that, given the `WorkGraphModel`, produces for each **selectable node** a grouped set: `{ nodeId → { StemmedFrom[], Resolves[], RaisedIn[], Contains[] } }` of the **other endpoint** `WorkNode`s (with `Href`, `Label`, `Title`, `Kind`). Direction is carrier → target (`WorkGraph.cs:21-23`); the pane should present both *"this stemmed from X"* (outgoing) and *"Y stemmed from this"* (incoming) meaningfully — decide per edge kind and document it (e.g. for a **story** node, incoming `StemmedFrom`/`Resolves` from deferred items = "work this spawned / that resolved here").
  - [ ] Keep it deterministic (stable node + group order) — mirror `WorkGraphBuilder`'s ordered construction. Golden output must be reproducible (FR31).
  - [ ] Node ids are the **existing work-graph ids** (`e{N}`, `s{storyId}`, `d{epic}-{i}`, `a{epic}-{j}`, `src:{key}`, `res:{key}`, `retro:{N}` — see `WorkGraph.cs:188-273`). Do **not** invent a new id scheme.

- [ ] **Task 3 — Bridge the payload-island node id ↔ work-graph model node id (AC: #1)**
  - [ ] Two id namespaces are in play: **20.2's payload-island ids** (`"epic-N"`, `"N.M"`, follow-up slug, aggregate href) that the client selection speaks, and the **work-graph model's internal ids** (`e{N}`, `s{storyId}`, `d{epic}-{i}`, `a{epic}-{j}`, `src:…`, `res:…`, `retro:N` — `WorkGraph.cs:188-273`) that the edges reference. The `edges` array 20.3 emits into the island (Task 5) must be expressed in **the island's id namespace** so the client can join edge→node without a translation table. So do the bridge **server-side**: when projecting `_workGraph` into island edges, map `e{N}`→`"epic-N"`, `s{id}`→`"N.M"`, and follow-up/spec/retro nodes to a stable island id (slug or href-derived) — matching whatever id 20.2 emitted for the corresponding wedge.
  - [ ] Follow-up/deferred/action nodes: the model's `d{epic}-{i}`/`a{epic}-{j}` ids are **positional within an epic subgraph**. Do **not** re-derive them from a second ordering — carry the model's own `WorkNode` through and key its island id off a **stable identity** (the follow-up slug / `DetailHref`), the same identity 20.2's island node uses. Verify 20.2's actual slug choice at dev time; if 20.2 hasn't emitted follow-up nodes into the island yet, record the id convention in Completion Notes for 20.2 to match.
  - [ ] Nodes with no corresponding island wedge (e.g. a `Spec`/`Retro` source that isn't a sunburst node) are still valid **edge endpoints** — render them in the pane as linked chips (they have `Href`), they just aren't selectable. That is fine; the pane shows *related* nodes, not only sunburst nodes.

- [ ] **Task 4 — Server-render the "Related" block into the explorer host (AC: #2, NFR8) — SHIP-FIRST half**
  - [ ] Render, beside/below the explorer sunburst on its host page(s), a **`<aside>`/section "Related work"** containing the per-node grouped lists from Task 2, present in the DOM **without JS**. Each entry is an `<a href>` to its detail page when `WorkNode.Href` is non-null, else a non-link chip (mirror `WorkGraph.cs:36-39` guarded-href discipline).
  - [ ] Choose the no-JS default view honestly: either (a) the whole-project related list grouped by epic (like a compact `work-graph.html` embed), or (b) a "select an item to see its connections; meanwhile here is everything" full list. **Never** an empty region that only fills in via JS (that would violate AC #2). Document the choice.
  - [ ] **Designed empty state** for a node with zero edges (AC #2): a real message ("No related work items for this selection."), styled — not a blank pane. Reuse existing empty-state styling idiom (e.g. `.chart-empty` / follow-up empty states) rather than inventing one.
  - [ ] Route rich hover text through the existing body-level `.ss-tooltip` node via `data-tip` / `data-tip-html` if used — do not add a new tooltip node ([[tooltip-clipping-use-ss-tooltip-node.md]]).

- [ ] **Task 5 — Fill the island `edges` + client reveal-on-selection (AC: #1) — binds to 20.2**
  - [ ] Emit the `edges` array into **20.2's existing payload island** (`{ nodes, edges }`) — one entry per work-graph edge in the island id namespace: `{ from, to, kind }` where `kind ∈ {contains, stemmed-from, resolves, raised-in}`. Do **not** create a second island or a sidecar `.json`. This is the slot 20.2 reserved (`edges: []`).
  - [ ] Add a **new opt-in block in `src/SpecScribe/assets/specscribe.js`** (not a new asset — see budget) guarded by presence of the `data-explorer` root element (mirror the `.codemap-view` / `.js-listable` opt-in idiom; `specscribe.js` is an IIFE of ES5-compatible `document.addEventListener` delegation — match it exactly).
  - [ ] On the **20.2 selection-change signal**, reveal the matching node's related group in the pane and hide the others; on "no selection", show the documented default; on "selection with no edges", show the empty state. The client **only re-arranges/reveals server-rendered DOM** — it never fetches, never computes a count, never invents a destination (the interactivity-boundary rule from 20.1's Dev Notes). The client MAY read the island `edges` to know which group to reveal, but the **rendered link content already exists in the server DOM** (Task 4) — the island is a lookup index, not the source of the displayed links.
  - [ ] Honor `prefers-reduced-motion`: any reveal transition snaps; timing (when allowed) reads `--motion-*` ([[motion-token-system]]).
  - [ ] Keyboard/AT: the pane's contents are real focusable links; when selection changes, announce via an `aria-live="polite"` region (mirror 20.2's zoom-scope announcement pattern). Do not trap focus.

- [ ] **Task 6 — HTML/SPA parity + tests (AC: #1, #2)**
  - [ ] The "Related" block + any payload island must render **identically** through the HTML and SPA adapters; add a `RenderParity` case (Story 6.7 harness) or record why not. **Webview/CLI are non-goals** for the explorer (mirror 20.1's surface-reach table) — confirm the block is gated so it doesn't leak into the webview dashboard.
  - [ ] Unit-test the Task 2 projection: grouping by the four edge kinds, guarded-href behavior, empty-state selection, deterministic order, and the "does not touch `ProjectCounts`" invariant (assert via construction, not a mock — the projection takes only the model).
  - [ ] Golden fingerprint **will move** (new server-rendered block on the dashboard/epics host). Regenerate and confirm the drift is only the intended block. Follow [[golden-diff-normalization-gotchas]] — run twice, confirm stable, before locking any constant.
  - [ ] Verify NFR8 by loading the host page with JS disabled: the Related block and all links are fully present and usable.

- [ ] **Task 7 — Completion Notes: reconciliation + sequencing (AC: #1, #2)**
  - [ ] Record: the four-kind reconciliation (why not covers/cites), the id-bridge decision (coordinate w/ 20.1), the no-JS default-view choice, the parity coverage owed, and the 20.2 selection-seam contract you built against.

### Review Findings

_(populated during code-review)_

## Dev Notes

### The data source is real and already computed — reuse it (do not rebuild)

Epic 19 shipped. The whole relationship model this pane needs is `SiteGenerator._workGraph` — a `WorkGraphModel` (one `WorkGraphEpic` per epic-with-signal), **computed once** at `SiteGenerator.cs:206` and already *"reused verbatim by WriteWorkGraph"*. It is a **pure projection** over `FollowUpGeometry` + `EpicsModel` + the epic→retro map that *"never re-parses deferred markdown / sprint yaml and never re-counts open items against `ProjectCounts`"* (`WorkGraph.cs:80-84`). **The pane is a read over this model — nothing more.**

**Shipped node + edge vocabulary (the exact contract — `src/SpecScribe/WorkGraph.cs`):**

| Type | Values | Notes |
|------|--------|-------|
| `WorkNodeKind` | `Epic`, `Story`, `Deferred`, `Action`, `Spec`, `Retro` | `Spec` = a quick-dev/`spec-*` one-shot or any non-story source/resolver. `covers`(requirement) + `cites`(code) nodes are **NOT** here — out of 19.2 MVP. |
| `WorkEdgeKind` | `Contains`, `StemmedFrom`, `Resolves`, `RaisedIn` | Direction always **carrier → target**. `RaisedIn` is the soft/heuristic cross-epic-retro link (dashed in the graph). |
| `WorkNode` | `record(Kind, Id, Label, Href?, Title?)` | `Href` null → render as non-link chip. `Title` = full hover/aria text. |
| `WorkEdge` | `record(FromId, ToId, Kind)` | |
| `WorkGraphEpic` | `Nodes`, `Edges`, `Cycles`, `Overflow`, `BucketLabel?` | Edges are **within one epic's subgraph** — there are no cross-epic edges except `RaisedIn`. The synthetic `Unattributed` bucket (`BucketLabel != null`) hosts orphan follow-ups (Story 19.1 code-review D1). |

**Node ids** (from `WorkGraph.cs:188-273`): `e{epicNumber}`, `s{storyId}`, `d{epicNumber}-{i}`, `a{epicNumber}-{j}`, `src:{normalizedKey}`, `res:{normalizedKey}`, `retro:{epicNumber}`. Reuse these; do not mint new ones.

### Where the pane lives — the explorer host page

The remaining-work sunburst (`Charts.Sunburst`, Story 10.7) is rendered on:
- **Dashboard** — `HtmlRenderAdapter.Dashboard.cs:47` (project-wide sunburst + `SunburstCompanionList` at `:52`).
- **Epics page** — `HtmlRenderAdapter.Epics.cs:32` (+ per-epic `EpicSunburst` at `:208`).

Epic 20's explorer enhances **that exact markup**. The pane is a **sibling region of the explorer sunburst** on its host page — the dashboard is the primary host (project-wide selection makes most sense there). Confirm with whatever 20.2 chose as the explorer root; do not create a third sunburst.

**Stable enhancement seams:** wedge markup is `<a href="…"><path class="sb-seg sb-{status}">…</path></a>` (`Charts.cs:414-417`). The explorer keys off `.sb-seg` + the wrapping `<a href>`. Story 10.7 (sunburst navigability) is now **`done`** — the wedge seams (`.sb-seg`, wedge `<a>`, ring radii) are settled per 20.2's Dev Notes ([[story-10-7-sunburst-navigability-project-scale-review]]); still key off `.sb-seg` + the payload island rather than inner path geometry.

### Client-JS budget (from the 20.1 spike's recommended defaults — honor unless 20.1 recorded otherwise)

| Dimension | Value | Source |
|-----------|-------|--------|
| **Home** | New opt-in block in `src/SpecScribe/assets/specscribe.js`, guarded by explorer-root presence | 20.1 Decisions table; mirrors `.codemap-view`/`.js-listable` opt-in |
| **Dependencies** | **Zero.** No framework, no build step. Hand-written, `file://`-safe, ES5-compatible — match the existing IIFE idiom | [[charting-is-pure-svg-no-js]]; ADR 0010 zero-dep posture |
| **Data delivery** | Server-rendered DOM (the "Related" block) — **no fetch/XHR** (breaks static/`file://`) | 20.1 interactivity-boundary rule |
| **Framework** | **No.** If you conclude one is warranted → **ADR-triggering fork, escalate via `correct-course`**, do not decide silently | [[adr-creation-trigger-gap-epic-10-retro]] |

`specscribe.js` is already ~1058 lines of sanctioned progressive enhancement (tooltips, copy, list sort/filter, codemap zoom, risk pager). Adding a small selection-reveal block is consistent with that; it is **not** the first JS. The **codemap zoom + `motionFastMs()`** block is the closest existing interactivity precedent — study it, don't fork it.

### Client contract (the seam Half 2 binds to — 20.2 committed most of it)

20.2's create-story committed the payload island + explorer root; 20.3 fills the reserved `edges` and adds the reveal block:
- **Explorer root:** `data-explorer` (/ `explorer-root` class) on the sunburst-panel container (20-2 Task 1). The pane block opts in on it and no-ops when absent — so shipping ahead of 20.2's implementation is safe and inert.
- **Payload island:** the single `<script type="application/json">` `{ nodes, edges }` island 20.2 emits. 20.3 populates `edges` (`{ from, to, kind }`, island id namespace). One island, shared.
- **Selection source:** 20.2 owns "current selection" (the zoomed/activated wedge). If 20.2 exposes it as a custom DOM event (recommend `explorer:select` with `detail.nodeId` in the island namespace) or a `data-selected-node-id` attribute, bind to that. **20.2's file does not yet name the exact selection event** — confirm at dev time and, if unnamed, propose one in Completion Notes for 20.2 to adopt; do not guess silently or fork a parallel one.
- **Pane DOM contract:** the server-rendered pane carries `data-related-pane` with one group per selectable node keyed by its **island id** (`data-related-node="epic-N"` / `"N.M"` / slug); the client reveals the group matching the current selection. No selection → documented default view; unknown/edge-less node → empty state.

### Architecture compliance

- **Shared-core projection (AD-1/AD-2):** the pane's data is a **pure projection** over the existing `WorkGraphModel` — not a per-surface re-parse, not a second geometry, not a second count ledger. [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`]
- **Additive & non-blocking (AD-4):** a missing/empty `_workGraph` must never fail generation — the pane simply omits or shows its empty state (same NFR8 gate the work-graph page uses: `WorkGraphModel.IsEmpty`).
- **Graceful degradation is inherited, not added:** JS-off / reduced-motion / AT support are invariants. The server-rendered "Related" block is the no-JS truth; JS only reveals slices of it.
- **Single ledger / single geometry:** forbidden to call `ProjectCounts` or re-derive ring weights. The reviewer will check this explicitly (it's an Epic 20 judged invariant, 20.1 Dev Notes).

### Forward-compatibility: covers/cites (out of scope, but leave the door open)

The pane groups by the four shipped edge kinds. If/when Epic 19 (or Epic 24's code-coupling work) adds `covers`(requirement) / `cites`(code) nodes+edges to `WorkGraphModel`, the pane's grouping should extend by iterating `WorkEdgeKind` rather than hard-coding four cases — so a future kind renders without a pane rewrite. Build the grouping data-driven over the enum; render only kinds that have entries.

### Anti-patterns to prevent

- Grouping the pane by five edge kinds when the model ships four — **do not** create empty `covers`/`cites` sections (phantom UI). Grep `WorkEdgeKind` before trusting the epics.md prose.
- Re-projecting the work graph or calling `WorkGraphBuilder.Build` a second time from the host-page render (reuse `_workGraph`).
- Calling `ProjectCounts` / re-running Epic 9 parsers to "count" related items.
- Inventing a selection mechanism or a new sunburst instead of binding to 20.2's explorer.
- A JS-gated pane that is empty with JS off (violates AC #2 / NFR8).
- `fetch`/XHR for pane data (breaks `file://` / static-host).
- A new tooltip node instead of the body-level `.ss-tooltip` seam.
- Introducing a framework/bundler by default (ADR-triggering — escalate).
- Positional-id fragility: keying the pane on `d{epic}-{i}` derived from a *second* ordering that can drift from `WorkGraphBuilder`'s — prefer a stable slug/href identity.
- Leaking the block into the webview/CLI surfaces (HTML+SPA only).

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/20-3-related-work-side-pane-on-selection.md`
- Sprint key: `20-3-related-work-side-pane-on-selection`
- Data model (reuse): `src/SpecScribe/WorkGraph.cs` (`WorkGraphModel`, `WorkGraphEpic`, `WorkNode`, `WorkEdge`, `WorkNodeKind`, `WorkEdgeKind`, `WorkGraphBuilder`)
- Model instance (reuse): `SiteGenerator._workGraph` (`SiteGenerator.cs:63`, populated `:206`)
- Host render sites: `HtmlRenderAdapter.Dashboard.cs:47` (primary), `HtmlRenderAdapter.Epics.cs:32`
- Wedge markup seam: `Charts.cs:414-417` (`.sb-seg` + wrapping `<a>`)
- Client asset: `src/SpecScribe/assets/specscribe.js` (new opt-in block) + `src/SpecScribe/assets/specscribe.css` (pane + empty-state styling)
- Reference precedent for a server-rendered graph-adjacency page: `src/SpecScribe/WorkGraphTemplater.cs` (`work-graph.html`) — the pane is a compact, selection-aware cousin of this
- Parity: `src/SpecScribe/RenderParity.cs` (add case), `JsonSpaRenderAdapter.cs` (SPA host)
- Nav: `SiteNav.WorkGraphOutputPath = "work-graph.html"`, gated `HasWorkGraph` (`SiteNav.cs:82,145,296`) — the pane does **not** add a nav entry (it lives on the dashboard/epics host, not a new page)

### Testing standards summary

- xUnit; ~2146 tests green on main at create-story. Add unit coverage for the Task 2 projection (pure, model-only input) + a `RenderParity` case.
- Golden fingerprint moves (new dashboard/epics block) — regenerate deliberately; run generation twice to confirm the hash is stable before locking ([[golden-diff-normalization-gotchas]]).
- Verify NFR8 manually with JS off (browser `preview_start` on the generated dashboard, or inspect the emitted HTML) — the Related block + links must be fully present.
- Determinism (FR31): identical input → identical block; no per-visitor `now`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 20 header (~L3056) + Story 20.3 ACs (~L3114-3132)]
- [Source: `src/SpecScribe/WorkGraph.cs` — `WorkGraphModel`/`WorkGraphEpic`/`WorkNode`/`WorkEdge`; `WorkNodeKind` (Epic/Story/Deferred/Action/Spec/Retro); `WorkEdgeKind` (Contains/StemmedFrom/Resolves/RaisedIn); covers/cites explicitly out of MVP (L5-8); carrier→target direction (L21-23); node ids (L188-273); no ProjectCounts re-count (L80-84)]
- [Source: `src/SpecScribe/SiteGenerator.cs` — `_workGraph` field (L63), populated once + reused (L205-206), `BuildWorkGraphModel` (L3207-3226), `WriteWorkGraph` NFR8 gate (L3239)]
- [Source: `src/SpecScribe/WorkGraphTemplater.cs` — the server-rendered `work-graph.html` (per-epic adjacency, sr-only enumeration, plain-anchor nav) — the pane's server-render precedent]
- [Source: `src/SpecScribe/Charts.cs` — `Sunburst` (L348), wedge markup `.sb-seg` + `<a>` (L414-417), `SunburstCompanionList` (L557), `EpicSunburst` (L875)]
- [Source: `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:47` + `HtmlRenderAdapter.Epics.cs:32` — the sunburst host render sites the pane sits beside]
- [Source: `src/SpecScribe/assets/specscribe.js` — IIFE / ES5 delegation idiom; `.sb-seg` already referenced (L100); codemap zoom + `motionFastMs()` = closest interactivity precedent]
- [Source: `_bmad-output/implementation-artifacts/20-1-interactive-explorer-architecture-spike.md` — the payload/id/budget/degrade contract (Completion Notes must be consulted once recorded; empty at create-story)]
- [Source: `_bmad-output/implementation-artifacts/19-2-*` work-graph build (shipped model) + `19-1-work-graph-model-and-coverage-spike.md` — edge vocabulary + D1/D2/D4 review guardrails]
- [Source: `src/SpecScribe/RenderParity.cs`, `JsonSpaRenderAdapter.cs`, `SpaDelivery.cs` — Story 6.7 HTML/SPA parity harness]
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` — AD-1/AD-2 shared-core projection, AD-4 additive/non-blocking; NFR8 degrade; FR31 determinism]

### Previous story intelligence

- **Story 20.2 (`ready-for-dev`, seeded 2026-07-22, not yet built):** Owns the payload island, the `data-explorer` root, and the zoom/selection state. Its create-story **explicitly reserved `edges: []` in the island for this story** and committed the canonical island id scheme (`"epic-N"`/`"N.M"`/slug). Build against that; do not fork a second island or selection mechanism. Its LOAD-BEARING insight: drill-in needs client-side arc RE-COMPUTATION (not codemap viewBox-pan) — irrelevant to the pane but confirms the explorer is a substantial new block the pane sits beside.
- **Story 20.1 (`ready-for-dev`, spike):** Named the interactivity boundary, the single `{ nodes, edges }` payload, zero-dep/no-framework budget, and the JS-off/reduced-motion/AT degrade contract this story inherits. **Its Completion Notes are empty at create-story** — but 20.2 already operationalized the payload+id+root contract, so 20.3 has a real seam regardless. Flag in Completion Notes anything 20.1 must ratify.
- **Story 19.2 (`done`, merged):** Shipped `WorkGraphModel` + `WorkGraphTemplater` `work-graph.html`. Honored 19.1 review D1 (Unattributed bucket), D2 (distinct-endpoint nodes), D4 (no phantom node from an unresolved `SourceKey` — cf. the a16ca0f fix). The pane inherits all three by reusing the model. [[story-19-2-work-graph-done]]
- **Story 19.1 (`done`, spike):** Locked the edge direction (carrier→target) and the four-kind provenance vocabulary; covers/cites deferred out of MVP. [[story-19-1-work-graph-spike-done]]
- **Story 6.7 (`done`, SPA adapter):** The HTML/SPA parity harness + body-capture the new block's parity rule plugs into. [[story-6-7-spa-adapter-live]]
- **Story 9.13 (`done`):** Leaf→detail / group→filtered-list destination contract — the explorer's terminal opens honor it (20.2), and the pane's entries link to the same detail pages, never a parallel scheme.
- **Charts-are-pure-SVG value ([[charting-is-pure-svg-no-js]]):** Epic 20 is the sanctioned crossing; keep the pane's data server-rendered so the crossing stays honest (JS reveals, never owns truth).

### Git intelligence summary

At create-story HEAD is `38044b1` ("Ready to go"); recent history merged Story 19.2's work-graph (`7d8882e Merge branch 'worktree-story-19-2-work-graph'`, `6d5f4cf Worktree`) and dev spikes (`d23b89a`). So **Epic 19's model is on main** — the pane's data source is live, `WorkGraph.cs` reads cleanly from `src/SpecScribe/`. No explorer/pane code exists yet; Epic 20 remains contract (20.1) + unbuilt explorer (20.2). Recent Epic 7/21 work (7.9–7.12, 21.1–21.3) continues to grow `specscribe.js` block-by-block and the standalone-insight-page shell (`TraceabilityTemplater`, `WorkGraphTemplater`) — the pane follows those established idioms.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-22 — Story 20.3 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Reconciled the epics.md five-edge-kind prose against the **shipped four-kind** `WorkEdgeKind` (covers/cites out of 19.2 MVP). Aligned to 20.2's committed contract (seeded same day): the single `{ nodes, edges }` payload island with **`edges` reserved for this story**, the `data-explorer` root, and the canonical island id scheme (`"epic-N"`/`"N.M"`) — with a server-side id-bridge from the work-graph model's internal ids. Documented the two-half split (ship-first server-rendered "Related" block reusing `SiteGenerator._workGraph` verbatim, no 20.2 needed; client reveal-on-selection block binding to 20.2's selection state, inert until 20.2 is built). Epic 19 confirmed merged to main (`WorkGraph.cs` @ `38044b1`) — data source is live. 20.1 spike Completion Notes still empty but 20.2 operationalized the seam.
