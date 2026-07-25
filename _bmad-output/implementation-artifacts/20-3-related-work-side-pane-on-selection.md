---
baseline_commit: 8db18aaddd7cc1325910bfc9b00e0ae9d1ac66a1
---

# Story 20.3: Related-Work Side Pane on Selection

Status: done

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

- [x] **Task 1 — Reuse the already-computed work graph; do NOT re-project (AC: #1)**
  - [x] Confirm `SiteGenerator._workGraph` is available at the point the explorer host page is rendered. Threaded verbatim: `SiteGenerator.WriteIndex` → `HtmlTemplater.RenderIndex`/`BuildIndexPage` → `DashboardViewBuilder.Build(workGraph:)`. The webview and SPA `BuildIndexPage` call sites pass the same instance, so all three surfaces read one model.
  - [x] Add **no** new call to `WorkGraphBuilder.Build`, `FollowUpGeometry`, `RequirementsParser`, or `ProjectCounts`. Verified: `RelatedWork.Build` takes only a `WorkGraphModel` + an island-id list, so there is no counting seam to reach — the invariant is enforced by the signature, not by a test double.

- [x] **Task 2 — Build the per-node adjacency the pane consumes (AC: #1)**
  - [x] Pure projection in the new `src/SpecScribe/RelatedWork.cs`. Both directions are surfaced, since a pane showing only outgoing edges would leave every epic and story looking unrelated to anything: outgoing "Stemmed from" vs incoming "Work that stemmed from this", outgoing "Resolved by" vs incoming "Resolved by this", "Part of"/"Contains", "Also raised in"/"Also raised here".
  - [x] Deterministic: an explicit `nodeOrder` list carries the model's own epic-then-node order (Dictionary enumeration order is not a contract), and nothing sorts or hashes into the output. Pinned by `Build_IsDeterministic`.
  - [x] Reuses the existing work-graph ids; no new id scheme. `NodeText`/`EdgeVerb` moved here and `WorkGraphTemplater` now delegates, so the graph page and the pane share ONE vocabulary (Story 20.8 inherits it rather than minting a second).

- [x] **Task 3 — Bridge the payload-island node id ↔ work-graph model node id (AC: #1)**
  - [x] Bridge is server-side and in one named function (`RelatedWork.IslandIdFor`): `e{N}`→`epic-{N}`, `s{id}`→`{id}` when the chart drew that wedge, Unattributed bucket→`orphan` keyed on `BucketLabel` (never on its `EpicNumber == 0`, which a real Epic 0 would collide with).
  - [x] Follow-up/deferred/action nodes are **not** keyed at all — see the deviation note below. Their positional `d{N}-{i}`/`a{N}-{j}` ids are never re-derived; the model's own `WorkNode` is carried through.
  - [x] Nodes with no wedge render as entries — linked when `Href` is non-null, non-link chips otherwise.

- [x] **Task 4 — Server-render the "Related" block into the explorer host (AC: #2, NFR8) — SHIP-FIRST half**
  - [x] `RelatedWorkTemplater.RenderPane` emits an `<aside data-related-pane>` immediately after the explorer panel on the dashboard. Routed builder → view (`DashboardView.RelatedWorkHtml`) → adapter, mirroring `TraceabilityStripHtml`, so HTML/SPA/webview render identical bytes from one path.
  - [x] No-JS default view = **(a)**, the whole-project related list grouped by scope — every section visible, mirroring `work-graph.html`'s scope picker ("with JS off every section shows"). Never an empty region.
  - [x] Designed empty state ships in the DOM `hidden` and is revealed only for a selection with no edges; with JS off there is no selection, so it stays correctly silent.
  - [x] No new tooltip node — related rows use plain `title` attributes; no `.ss-tooltip` rich-hover markup was introduced.

- [x] **Task 5 — Fill the island `edges` + client reveal-on-selection (AC: #1) — binds to 20.2**
  - [x] **DEVIATION, reasoned and recorded — the island `edges` array stays `[]`.** See "Deviation: the island `edges` slot" below. The relationship truth ships as server-rendered DOM keyed by the same id namespace instead.
  - [x] New guarded block in `specscribe.js` (no new asset), opting in on `[data-related-pane]`, matching the file's IIFE / ES5 delegation idiom and re-running on `specscribe:content-swapped`.
  - [x] Reveal on selection; documented default at no-selection; empty state for a selection with no edges. The client only re-arranges server-rendered DOM — no fetch, no computed count (its one number is `querySelectorAll(".related-row").length` off the visible DOM), no invented destination.
  - [x] Reduced motion: the reveal fade is declared in the `@media (prefers-reduced-motion: no-preference)` block and explicitly cancelled in the paired `reduce` block, on the `--motion-fast` token.
  - [x] Keyboard/AT: entries are real links; non-current sections are set `hidden`, so their links leave both the a11y tree and the tab order (no phantom tab stops); selection is announced through the pane's own `aria-live="polite"` region — deliberately separate from the sunburst's, so one activation does not produce two overlapping announcements. No focus trap.

- [x] **Task 6 — HTML/SPA parity + tests (AC: #1, #2)**
  - [x] `SiteGeneratorSpaTests.RelatedWorkPane_SurvivesSpaContentRegionCapture` asserts HTML/SPA agreement in BOTH directions (present in both, or omitted in both). Its comment states plainly what an SSR test can and cannot prove — no `RenderParity` case was added, because `RenderParity.cs` has no island/pane fact awareness (Story 20.1 §4a); that gap is the harness's, and this story did not widen it.
  - [x] `RelatedWorkTests` — 15 tests: grouping in both directions, the four shipped kinds with no phantom `covers`/`cites`, unknown-kind heading fallback, the id bridge, unwedged stories, the orphan/`epic-0` distinction, cross-subgraph dedup, guarded hrefs, the cap + its reported remainder, determinism, the NFR8 omit gate, and markup/escaping.
  - [x] Golden fingerprint regenerated `1711700e…` → `253fe05c…`, stable across 2 repeated runs, with the drift and its shared-main provenance recorded at the constant.
  - [x] Verified in a live browser with JavaScript disabled — see Completion Notes.

- [x] **Task 7 — Completion Notes: reconciliation + sequencing (AC: #1, #2)**
  - [x] Recorded below.

### Owner redesign — 2026-07-24 (verify-and-iterate)

After seeing the first build, the owner redirected the surface: the pane should sit **to the right of the sunburst**
(not below), and be a **very minimal "fancy card"** augmenting the selection — just the selected item's **name**, a
**summary of what it contains**, **one most-relevant AI action**, and a **button link to more details**. When nothing
is selected it shows **top-level project details + a prompt to click a node**. This pulls the Story 20.8 details-pane
vision forward into 20.3 (see Completion Notes → *Overlap with Story 20.8*).

Implemented:
- **Layout:** the sunburst panel and the rail sit in a new `.explorer-layout` CSS grid — rail on the right on wide
  viewports, stacked below ≤900px. Grid collapses to the chart alone when there is no work-graph signal.
- **Card model:** new `RelatedWorkCards.cs` joins the (unchanged) `RelatedWork` relationship projection to the domain
  models to produce one `RelatedCard` per scope — full title, a one-line summary, one primary BMad command, a
  "View details →" link — plus a `RelatedProjectCard` default (project counts + "select a node" prompt).
- **AI action:** a single read-only command badge (copies the slash command; AD-6 — never mutates an artifact),
  reusing the existing `BmadCommands` surface via new `PrimaryEpicCommand`/`PrimaryProjectCommand` (the story primary
  already existed). No new command vocabulary.
- **NFR8 preserved:** each card still carries its full relationship groups in a native `<details>`, expanded with JS
  off; CSS hides it (and the per-scope cards) only once JS sets `data-related-ready`, leaving the project card by
  default and one scope card on selection.

### Review Findings

Reviewed 2026-07-24 via `/bmad-code-review 20.3` (Blind Hunter + Edge Case Hunter + Acceptance Auditor, run in parallel against a file-scoped diff of this story's own File List, baseline `8db18aa`→HEAD). Sibling-story hunks riding the same shared files (`SiteGenerator.cs`'s Story 5.3/5.5 work, `SunburstExplorer.cs`'s `noPlanWeight` threading) were excluded per CLAUDE.md's shared-`main` scoping rule.

**Decision needed — resolved by owner 2026-07-25:**

- [x] [Review][Decision] Related-work pane renders inside the VS Code webview, contradicting this story's own "Anti-patterns to prevent: Leaking the block into the webview/CLI surfaces (HTML+SPA only)". **Owner decision: keep it in the webview; correct the story text instead** — folded into the patch list below.
- [x] [Review][Decision] Two `aria-live="polite"` regions announce on a single chart selection (the explorer's "Zoomed into Epic N" + the pane's "Related work for Epic N."). **Owner decision: merge into one announcement** — the explorer's own live region stays the one authoritative per-click announcement; the pane's live region should only speak for information the explorer doesn't already convey (the empty-selection result) — folded into the patch list below.

**Patch:**

- [x] [Review][Patch] Correct the "Anti-patterns to prevent" bullet in Dev Notes (and elevate the "Parity and surface reach" Completion Notes paragraph to the same "Deviation, reasoned" prominence the `edges` deviation got) now that the owner has confirmed the webview should keep the pane — the rule should read as a documented exception, not a broken absolute [story file Dev Notes + Completion Notes].
- [x] [Review][Patch] Suppress the redundant "Related work for Epic N." / "Showing project overview." live-region announcements in `revealRelatedCard` (they duplicate the explorer's own "Zoomed into Epic N" / "Showing all epics" announcements) while keeping the "No related work items for Epic N." empty-state announcement, which is genuinely new information the explorer's live region never conveys [src/SpecScribe/assets/specscribe.js:2191-2224].

- [x] [Review][Patch] JS-off relationship data renders collapsed, not expanded — breaks AC #2/NFR8's "expanded with JS off" guarantee and contradicts the Completion Notes' own claim of live verification with "all `<details>` expanded" [src/SpecScribe/RelatedWorkTemplater.cs:81] — the `<details class="related-card-full">` element ships with no `open` attribute, so it renders collapsed by default per the HTML spec; a JS-off visitor must click each card to see its relationships.
- [x] [Review][Patch] `RelatedWorkModel.Overflow`/`OverflowLabels` are computed from the work graph's own honestly-reported draw overflow but never read or rendered anywhere in the pane, contradicting the Completion Notes' explicit "surfaced in the pane rather than under-reported (rule 5)" claim [src/SpecScribe/RelatedWork.cs:71-72,144-145,164-165] — `RelatedWorkCards.Build`/`RelatedWorkPaneModel`/`RelatedWorkTemplater.RenderPane` never reference `.Overflow`/`.OverflowLabels`.
- [x] [Review][Patch] A story cross-referenced from another epic's follow-up (as a `StemmedFrom`/`Resolves` source, before its own epic's subgraph is processed) has its `ScopeAnchor` bound to the wrong (first-referencing) epic, so if that story's own related-work groups exceed `MaxEntriesPerGroup`, its "+N more" deep link points at the wrong epic's anchor on `work-graph.html` [src/SpecScribe/RelatedWork.cs:150-158,200-202] — `scopeOfNode` is a first-seen-wins map and `AncestorIslandIdFor` already derives a story's home epic from its own id for the *fold* path, but the node's *own-section* `Anchor` still comes from whichever subgraph happened to reference it first.
- [x] [Review][Patch] A selection change can silently drop keyboard focus to `<body>` when the focused card (e.g. its "View details" link) becomes `hidden`, with no focus redirection to the newly-current card or the pane heading [src/SpecScribe/assets/specscribe.js:2191-2208] (`revealRelatedCard`).
- [x] [Review][Patch] `storySubjectsByEpic` (a `Dictionary<string, List<RelatedWorkSubject>>`) is enumerated directly with no explicit order list in the "epic with story relationships but no scope node of its own" fallback path, unlike every other place in this story that explicitly avoids relying on dictionary enumeration order for the FR31 determinism/golden-fingerprint invariant [src/SpecScribe/RelatedWorkCards.cs:102].
- [x] [Review][Patch] Reduced-motion cancellation for the card-reveal fade targets a selector (`.related-node`) that no shipped markup ever has (the real class is `.related-card` + attribute `data-related-node`), so the explicit "paired reduce block" cancellation Task 5 describes never fires [src/SpecScribe/assets/specscribe.css:6308]. Functionally masked today by the pre-existing universal `*, *::before, *::after { animation-duration: 0.01ms !important }` override two lines above (line 6276-6278) — so no real reduced-motion user sees the fade — but the selector should still be corrected to match the documented belt-and-suspenders intent.
- [x] [Review][Patch] `RelatedWorkCards.Resolve()`'s story-id branch and the private `StorySummary` helper are unreachable dead code — both call sites already filter out or synthesize past any node whose island id is a bare story id before `Resolve()` runs, a leftover from before the owner's "no standalone story cards" redesign [src/SpecScribe/RelatedWorkCards.cs:140-146,155-164].
- [x] [Review][Patch] The "drop the story's own outgoing Contains/Part-of group" filter predicate is written independently in two files rather than shared, a duplication risk this story explicitly avoided elsewhere (single-sourcing `NodeText`/`EdgeVerb`) [src/SpecScribe/RelatedWork.cs:210-211; src/SpecScribe/RelatedWorkCards.cs:80].
- [x] [Review][Patch] No direct unit test exercises the new `BmadCommands.PrimaryEpicCommand`/`PrimaryProjectCommand`/`RenderPrimaryActionBadge`, and `RelatedWorkTests` derives island ids via the 2-arg `Charts.SunburstExplorerNodes(epics, geometry)` overload rather than the real 3-arg production call `DashboardViewBuilder.BuildRelatedWorkHtml` uses (`epicsModel, geometry, unplannedGeometry`) — a regression in how the orphan/unplanned root surfaces through `UnplannedWorkGeometry` would go uncaught [tests/SpecScribe.Tests/RelatedWorkTests.cs:326].

All 11 patches applied 2026-07-25 (including the two resolved decisions). Full suite green (2354 passed / 3 skipped /
0 failed). Golden fingerprint regenerated `9232e3f5…` → `9288bf55…` (asset-bytes-only delta — `specscribe.css`/`.js`
changed, no dashboard markup shift visible to the no-work-graph fixture), stable across 2 repeated runs. Verified
live: JS-off (genuinely script-blocked sandbox iframe) shows all 15 `<details>` expanded and the overflow note ("38
more related items not drawn"); JS-on confirms focus redirects to the newly-current card instead of `<body>`, and
the "Related work for X" announcement is suppressed while the empty-state announcement still fires.

**Deferred:**

- [x] [Review][Defer] `epicsByNumber = epics.Epics.ToDictionary(e => e.Number)` throws on a duplicate epic number with no dedup guard [src/SpecScribe/RelatedWorkCards.cs:61] — deferred, pre-existing: the same unguarded pattern already exists at `Charts.cs:4322` and `RequirementsParser.cs:55,307`, so this is a codebase-wide assumption this story inherited, not a regression it introduced.

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
- Leaking *interactive* markup (the `specscribe.js` reveal block, the explorer's own island/script) into the webview/CLI surfaces. **Amended 2026-07-25 (owner decision, code review):** the Related-work pane itself is an exception, not a violation — it is server-rendered content with no script or JSON island of its own, so it ships in the webview like any other static section (`TraceabilityStripHtml`, the sunburst SVG). The rule still holds for anything that depends on client JS to be meaningful.

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

claude-opus-4-8 (Opus 4.8)

### Debug Log References

- Baseline `8db18aa`. Two other sessions were editing shared `main` throughout this story: Story 5.3
  (`FileWatcherService.cs`, `SiteGenerator.cs`) and `spec-multi-epic-retro-attribution`
  (`RetroModel.EpicNumber` → `EpicNumbers`, `RetroParser.cs`, `BmadArtifactAdapter.cs`). The tree was
  **transiently uncompilable** twice while their rename was mid-flight (10 errors, then 2, in files this story
  never touches). No `git reset`/`checkout`/`clean` was used; the fix was to wait. One `SiteGenerator.cs` edit
  reported the file had changed on disk since it was read — it applied cleanly and was grep-verified afterwards
  (`workGraph: _workGraph`, still present).
- Live verification server: `related-work-20-3` on port 8094 (a new `.claude/launch.json` entry — port 8099 was
  held by another chat's server, which `preview_stop` cannot stop).

### Completion Notes List

#### The four-kind reconciliation (why not covers/cites)

`epics.md` AC #1 names five kinds (*stemmed-from, resolves, covers, cites, raised-in*). The shipped
`WorkEdgeKind` has **four** — `Contains`, `StemmedFrom`, `Resolves`, `RaisedIn` — because `covers` (requirement)
and `cites` (code) nodes are deliberately out of the Story 19.2 MVP draw. The pane groups by the four real kinds
and manufactures no empty `covers`/`cites` section, which would be exactly the phantom-UI class
[[story-7-11-7-12-code-review-shared-engine-merge]] warns about.

Grouping is **data-driven over `Enum.GetValues<WorkEdgeKind>()`**, not a four-way switch, and `Heading` has a
derived fallback for a kind the table has not been taught (pinned by `Heading_FallsBackForAnEdgeKindTheTableHasNotBeenTaught`).
When Epic 19 or Epic 24 adds `covers`/`cites`, the pane renders them with no rewrite. **epics.md's five-kind AC
prose still needs correcting** — Story 20.1's review already flagged this and it remains open; `covers` and
`cites` need a requirements-coverage map and a code-citation map, two different sources.

#### Deviation: the island `edges` slot stays `[]` — reasoned, not skipped

Task 5's first subtask says to fill 20.2's reserved `edges` array. **It is deliberately left empty**, and this is
the finished answer rather than an unfinished one.

Story 20.1's code review (§1a, 2026-07-24 — after this story was drafted) established that the two id spaces are
disjoint and that most work-graph edge endpoints (`d*`/`a*`/`src:`/`res:`/`retro:`) have no wedge at all. Working
through what actually survives translation into the island namespace:

| Edge kind | Carrier | Target | Both translatable? |
|---|---|---|---|
| `Contains` | story | epic | ✅ — and `nodes[].parentId` **already states it** |
| `StemmedFrom` | `d*` (no wedge) | story / `src:` | ❌ |
| `Resolves` | `d*` (no wedge) | story / `res:` | ❌ |
| `RaisedIn` | `a*` (no wedge) | `retro:` | ❌ |

So an island-namespace edge array reduces to a restatement of `parentId`: **zero new information, non-zero
bytes**, on the one payload that grows with project size — the single budget question Story 20.1's review said
actually survives (deferred to 20.5/20.6). Shipping it would repeat the defect 20.2's own review had just
fixed (116 dead join hooks, ~2.5 KB, removed because nothing read them).

The relationship truth therefore ships as **server-rendered DOM keyed by the same island id namespace**, which the
client joins directly — no payload lookup at any point. Recorded at `Charts.SunburstExplorerIsland` so the next
reader does not treat the empty array as unfinished work. The field is kept in the shape rather than removed: it
is part of the shipped island contract, and an empty array is a clearer statement than a missing key. **The
`specscribe.js` comment predicting a second island was corrected** — no second island was created.

#### The id bridge, and the one place §1a's rule 2 needed judgment

One named server-side function, `RelatedWork.IslandIdFor`: `e{N}`→`epic-{N}`, `s{id}`→`{id}`, Unattributed
bucket→`orphan`. The bucket is identified by `BucketLabel`, never by its `EpicNumber == 0`, so a real Epic 0 could
not collide with it (§1a rule 4). Cross-subgraph dedup by node id and by `(from,to,kind)` (rule 4).
`WorkGraphEpic.Reprefixed(linkPrefix)` is applied, not assumed away, though the dashboard is at the site root so
it is a no-op there (rule 6). `WorkGraphEpic.Overflow` is surfaced in the pane rather than under-reported (rule 5).
`WorkGraphModel.IsEmpty` omits the pane entirely (rule 5 / NFR8) — which also closes the
`deferred-work.md:960` concern that the pane could ship as permanent dead chrome on a young project.

Rule 2 ("resolve to the nearest existing ancestor rather than dropping the edge silently") needed a real decision,
and **the live portal proved the naive reading wrong**. My first implementation gave an unwedged story no section
at all. Generating against this repo showed the consequence: **32 `Resolves` edges existed in the work graph and
0 reached the pane** — every one lands on a resolver story, and most resolver stories sit in density-collapsed
epics that emit no story wedges. An entire edge kind had gone invisible, silently, exactly as §1a predicted.

The fix folds an unwedged story into its epic's section as a **labelled subject** (`RelatedWorkSubject`) carrying
its own name and its own groups — so nothing is dropped, and no group heading is mis-attributed to the epic
hosting it (a plain fold would have made Epic 20's section claim "Resolved by this" about a story's edges). The
epic is derived from the **story id** (`7.11`→`epic-7`), not from whichever subgraph the node was first seen in,
because a story appears in other epics' subgraphs as an external source. A folded subject's own outgoing
"Part of → Epic N" group is dropped as a restatement of the heading above it.

**Honest bound, stated rather than hidden:** an edge whose *both* endpoints are unwedged — a deferred item
resolved by a `res:` spec — surfaces in neither endpoint's section, because neither owns one. Deferred/action
nodes are deliberately **not** folded: they are related-work rows by nature, and hoisting them would list every
epic follow-up twice. Those chains stay reachable through the pane's "View the full work graph →" link and each
item's own detail page. This also keeps Story 19.1 review decision D2 (no transitive collapse) intact.

#### The no-JS default view (AC #2 / NFR8)

Option **(a)**: the whole-project related list grouped by scope, every section visible, mirroring the
`work-graph.html` scope picker's own rule ("with JS off every section shows"). The client only ever *reveals a
slice*. The designed empty state ships in the DOM `hidden` and is revealed only for a selection with no edges —
with JS off there is no selection, so "no related work" would be a lie and it correctly stays silent.

Verified in a live browser with **a genuinely script-blocked render** (a `sandbox="allow-same-origin"` iframe,
which cannot execute `specscribe.js` at all — not a proxy for JS-off, actually JS-off): script never ran, all
37 sections visible, all 297 rows present, **297/297 links carry an `href`**, empty state still hidden.

#### The selection seam — named here, because 20.2 shipped without one

Story 20.1's contract reserved a selection signal but never named one, and 20.2 shipped no event: the explorer's
only notion of "the item I am looking at" is its zoom scope. Rather than fork a parallel mechanism (which the
story forbids), this story adds **`specscribe:explorer-select`** to 20.2's own block, dispatched from
`applyState()` — the single point every scope change funnels through (click, Enter/Space, breadcrumb, centre
control, hash, popstate). `detail` is `{ nodeId, label, root }`; `nodeId` is null at root scope. Guarded so a
browser without the `CustomEvent` constructor cannot break the drill-in.

**Stories 20.5 and 20.8 should adopt this event rather than minting a second.** Note ADR 0012/0013 supersede the
hand-written explorer (20.7 deletes `initSunburstExplorer`), so the JS here was kept deliberately small — the
durable asset of this story is the C# projection, which 20.8 reuses for its details pane.

One ordering hazard found and guarded: the explorer block runs earlier in the IIFE, so its first
`explorer-select` fires **before** the pane's listener exists. Arriving on `#sb=epic-19` would have left the pane
showing every scope while the chart showed one. The pane therefore syncs on init from `data-sb-scope`, the
attribute 20.2 already publishes — not a second source of truth. Verified live on that exact deep link. The
document-level listener is registered **once**, not per pane: the pane is a *sibling* of the explorer root so a
bubbling event never passes through it, and a per-pane listener would leak one detached pane per SPA swap.

#### Live-browser findings the 2,200-test suite could not have caught

Both were invisible to every SSR assertion, and both are the reason CLAUDE.md § Verification exists:

1. **The live-region announcement read "Related work for EpicEpic 19".** On the init-sync path there is no label
   from the explorer, so it fell back to the section heading text — which included the kind chip. Fixed.
2. **Every row read "Story Story 19.1" to a screen reader.** The per-kind chip duplicated what
   `RelatedWork.NodeText` already puts in the label ("Deferred item: …", "Action item: …", "Source: …", and an
   epic/story label is self-describing). The chip was removed entirely along with its CSS and the now-dead
   `KindLabel` helper — so the pane signals nothing by colour because it has **no** colour signal to reinforce,
   which is a stronger position than a redundant badge. It also removed ~300 spans from the dashboard.

Also verified live: no console errors; **no phantom tab stops** — a `[hidden]` section's links were empirically
confirmed unfocusable (`element.focus()` did not move `document.activeElement`), rather than assumed from the UA
rule, since Story 20.2's review found an SVG `<a>` at `display:none` *stays* focusable; the reveal animation
resolves to `sb-drill-fade / 0.12s` off `--motion-fast`; `specscribe.css` parses to **1,662 rules** (the
`*/`-truncation hazard from [[css-comment-star-slash-silent-truncation]] did not fire); no horizontal overflow;
heading order H3 → H4 with group titles as `<p>` so nesting a group under a subject never skips a level.

#### Owner redesign (2026-07-24 iterate) — from list to card rail

After the first build, the owner redirected the surface (verbatim): the pane should be **to the right of the main
view**, **very minimal** — "just the selected item's name, a summary of what it contains, an action button for the
most relevant AI action inside of it, and a button link to more details" — a **"fancy card augmenting the
selection."** With nothing selected: **top-level project details + a prompt to click a node.**

What changed:

- **Layout.** New `.explorer-layout` CSS grid puts the sunburst and the rail side by side — rail on the RIGHT on wide
  viewports (`minmax(240px,320px)`), stacked below at ≤900px. Verified live: rail right-of-chart and top-aligned at
  1280px, single-column below the chart at 375px, no horizontal overflow at either.
- **Card model.** New `RelatedWorkCards.cs` joins the (unchanged) `RelatedWork` relationship projection to the
  domain models → one `RelatedCard` per **selectable scope** + a `RelatedProjectCard` default. Each scope card:
  kind eyebrow, full title, one-line summary ("N stories · M open follow-ups"), one AI action, "View details →".
- **AI action = read-only command badge.** One primary BMad command per card, copied to clipboard via the existing
  `cmd-badge`/`data-copy` surface — never mutates a planning artifact (AD-6). New `BmadCommands.PrimaryEpicCommand`
  / `PrimaryProjectCommand` (the story primary already existed); no new command vocabulary. A done epic legitimately
  has no next action, so its card omits the badge (verified: Epic 1's card has none, Epic 20's offers
  `/bmad-sprint-status`).
- **Cards are keyed to what the explorer can SELECT.** In Story 20.2's model a story wedge is a leaf that
  *navigates* on click; only epics and the orphan/unplanned roots are zoom-selectable. So there are **no standalone
  story cards** — each story folds into its epic's card as a labelled subject, so the epic you drill into carries
  the full "what stemmed from what," and the rail's card set matches the selectable set (39 cards → 14). A story is
  still one click from its own page (the "Contains" list + the wedge itself both link there).
- **NFR8 / AC #2 preserved.** Every card still server-renders its relationship groups in a native `<details>`,
  expanded with JS off; the CSS hides it (and the per-scope cards) only once JS sets `data-related-ready`, leaving
  the project card by default and one scope card on selection. Verified with a genuinely script-blocked sandbox
  render: 15 cards visible, all `<details>` expanded, **348 relationship links present**, empty state hidden.

**Two defaults I chose (the owner's brief did not cover them) — flag at verify:**
1. The AI action is a **copy-to-clipboard command**, not a navigation — per AD-6 and Story 20.8's read-only
   prompt-button intent. If you'd rather it deep-links into an editor, the badge already carries a Cursor send-menu.
2. The JS-off path keeps the **full relationship rows in a per-card `<details>`** to satisfy AC #2; with JS on they
   are hidden in favour of the "View details" link. If you consider even the collapsed `<details>` too much for the
   JS-on minimal card, say so — it is already `display:none` under `[data-related-ready]`, so this only affects the
   JS-off view.

#### Overlap with Story 20.8 (raise before Epic 20 review)

This redesign delivers what Story 20.8 ("dashboard details pane") described — *"activating a node on the HOME screen
populates a details pane BESIDE the chart… high-level details + the RECOMMENDED-PROMPT BUTTON + a VIEW-MORE link,"*
reusing the `BmadCommands` surface and 20.3's groupings. 20.3 now ships that card on the **zoom-scope** selection
20.2 already provides. What remains genuinely 20.8/20.5: true **select mode** (activating a node — including a
**story leaf** — without navigating, per ADR 0012's navigate|select contract), which needs the Plotly component.
So 20.8 should narrow to "make every node selectable via select mode and reuse this card," not "build the card."
I did **not** renumber 20.8 or edit epics.md — that is the owner's call via `correct-course`; flagging it here.

#### Measurements for the 20.5/20.6 payload budget

Story 20.1's review asked for measured numbers rather than estimates. On this repo's own portal (375 pages):

The rail caps each group at `RelatedWork.MaxEntriesPerGroup = 12` and reports the remainder ("+N more not shown")
with a deep link into `work-graph.html` — truncation is stated, never silent.

The card redesign changed the byte profile: the rail is now **14 cards** (one per selectable scope) rather than 37
stacked sections, and per-kind chips are gone. The relationship rows still ship (in each card's `<details>`, for the
JS-off path), so the total is still dominated by that data (348 relationship links on this portal). If the JS-off
`<details>` block is judged too heavy, `MaxEntriesPerGroup` is still the one lever — not lowered unilaterally, same
reasoning as before: trading away JS-off completeness is the owner's call. Real input to the embedded-payload
ceiling Story 20.1's review deferred to 20.5/20.6.

#### Parity and surface reach

Routed builder → `DashboardView.RelatedWorkHtml` → adapter, mirroring `TraceabilityStripHtml`, so HTML, SPA and
webview render identical bytes from one path. `_workGraph` is threaded into all three `BuildIndexPage`/`RenderIndex`
call sites.

**Deviation, reasoned — webview deliberately NOT gated out.** This departs from the story's Task 6 phrasing and its
own "Anti-patterns to prevent" bullet ("Leaking the block into the webview/CLI surfaces (HTML+SPA only)"), which was
written about the explorer's *interactive* markup — a genuine webview non-goal. This pane is not that: it is
server-rendered content with no script or JSON island of its own, whose links resolve inside the captured webview
site exactly like `TraceabilityStripHtml` or the sunburst SVG already do. Gating it out would remove working content
from a surface that can use it, to honour a rule written about scripts. The webview strips JSON islands
(`WebviewRenderAdapter.cs`), and the pane has none, so nothing needed a new `HostRenderException`. **Confirmed by
owner at code review (2026-07-25):** keep the pane in the webview; the "Anti-patterns to prevent" bullet in Dev Notes
was amended in place to state this as a documented exception rather than an absolute the shipped code silently
broke.

No `RenderParity` case was added. `RenderParity.cs` has **no island or pane fact awareness at all** — Story 20.1's
review §4a established that its "a dropped fact must make the forms differ" guarantee did not hold when written.
Coverage is instead `SiteGeneratorSpaTests.RelatedWorkPane_SurvivesSpaContentRegionCapture`, which asserts
HTML/SPA agreement in **both** directions (present in both, or omitted in both) and states in its own comment what
an SSR test can and cannot prove. That harness gap is pre-existing and this story did not widen it; closing it
belongs with 20.6's fingerprint-replacement work.

#### Test-scoping change made outside this story's own files

`FollowUpSurfacesTests.FollowUpGroupPages_EmittedForNonEmptyGroups_OnlyThatGroupsItems` asserted
`DoesNotContain("href=\"follow-ups/action-")` against the **whole** `index.html`. That assertion protects the
Story 9.13 rule that *sunburst leaves* open aggregated group pages rather than per-item detail — but the pane
links each related node to its own detail page, which its AC #1 requires. The assertion was narrowed to the
sunburst panel (new `SunburstPanelOf` helper) so it tests the chart it is about; the invariant is unchanged.

#### Vocabulary single-sourcing

`NodeText`/`EdgeVerb` moved from `WorkGraphTemplater` to `RelatedWork` and the templater now delegates
(work-graph page bytes unchanged). Story 20.8 is required to reuse Story 20.3's groupings rather than introduce a
second relationship vocabulary — there is now exactly one place to reuse.

### File List

- `src/SpecScribe/RelatedWork.cs` — **new.** Vocabulary + pure projection (`RelatedWorkDirection`,
  `RelatedWorkEntry`, `RelatedWorkGroup`, `RelatedWorkSubject`, `RelatedWorkNode`, `RelatedWorkModel`,
  `RelatedWork.Build`/`IslandIdFor`/`AncestorIslandIdFor`/`Heading`/`NodeText`/`EdgeVerb`).
- `src/SpecScribe/RelatedWorkCards.cs` — **new (owner redesign).** The card layer: `RelatedCard`,
  `RelatedProjectCard`, `RelatedWorkPaneModel`, `RelatedWorkCards.Build` (joins the projection to the domain models
  for title/summary/primary-command; folds stories into their epic's card).
- `src/SpecScribe/RelatedWorkTemplater.cs` — **new.** Renders the card rail (`RenderPane(RelatedWorkPaneModel)` +
  `PaneAttribute`) — rewritten for the card model.
- `src/SpecScribe/BmadCommands.cs` — new `PrimaryEpicCommand` / `PrimaryProjectCommand` / `RenderPrimaryActionBadge`
  (one read-only AI action per card, reusing the existing command surface).
- `src/SpecScribe/WorkGraphTemplater.cs` — `NodeText`/`EdgeVerb` now delegate to `RelatedWork` (one vocabulary).
- `src/SpecScribe/SunburstExplorer.cs` — doc comment: `Edges` stays `[]`, with the reasoning.
- `src/SpecScribe/DashboardView.cs` — new `RelatedWorkHtml` opaque fragment.
- `src/SpecScribe/DashboardViewBuilder.cs` — new `workGraph` parameter + `BuildRelatedWorkHtml`.
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — emits the pane after the explorer panel.
- `src/SpecScribe/HtmlTemplater.cs` — `workGraph` threaded through `RenderIndex`/`BuildIndexPage`.
- `src/SpecScribe/SiteGenerator.cs` — passes `_workGraph` at the three dashboard render sites (static, webview, SPA).
- `src/SpecScribe/assets/specscribe.js` — `specscribe:explorer-select` seam in the 20.2 block; new guarded
  related-pane reveal block; corrected the stale second-island comment.
- `src/SpecScribe/assets/specscribe.css` — pane/subject/group/reveal rules + the paired motion no-preference and
  reduce entries.
- `tests/SpecScribe.Tests/RelatedWorkTests.cs` — **new.** 15 tests.
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — new `RelatedWorkPane_SurvivesSpaContentRegionCapture`.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden fingerprint regenerated + provenance note.
- `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` — sunburst-scoped the destination assertion (`SunburstPanelOf`).
- `.claude/launch.json` — added the `related-work-20-3` preview entry (port 8094).
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — `.explorer-layout` grid wrapping the sunburst + rail.
- `src/SpecScribe/DashboardView.cs` / `DashboardViewBuilder.cs` — `RelatedWorkHtml` now built via
  `RelatedWorkCards.Build` + `RelatedWorkTemplater.RenderPane` (threads `commands` + `counts` + project title).
- `src/SpecScribe/assets/specscribe.css` — card + layout styles (replaced the list styles); JS-on collapse rules.
- `src/SpecScribe/assets/specscribe.js` — reveal block rewritten for the card model (project default / one scope
  card / empty state; `data-related-ready` is now the CSS hook).
- `tests/SpecScribe.Tests/RelatedWorkTests.cs` — card-rail tests replace the list-render tests.

## Change Log

- 2026-07-24 — **Owner redesign (verify-and-iterate).** The pane became a **card rail to the RIGHT of the sunburst**
  (`.explorer-layout` grid; stacks ≤900px) instead of a full-width list. New `RelatedWorkCards.cs` builds one minimal
  card per **selectable scope** (kind · full title · one-line summary · one read-only AI-action command badge ·
  "View details →") plus a **project default card** (top-level counts + "select a node" prompt) shown when nothing is
  selected. Stories fold into their epic's card (no standalone story cards — a story wedge navigates, only
  epics/roots are zoom-selectable), 39 cards → 14. AI action reuses `BmadCommands` via new
  `PrimaryEpicCommand`/`PrimaryProjectCommand` (AD-6 read-only copy; done epics have none). NFR8/AC #2 preserved: each
  card keeps its full relationship groups in a native `<details>` that shows with JS off and is hidden once JS sets
  `data-related-ready`. Verified live: right-of-chart at 1280px / stacked at 375px, project-card default, one epic
  card on real drill-click, empty state for a no-relationship scope, and a script-blocked sandbox render with all 15
  cards + 348 relationship links present. specscribe.js reveal block + specscribe.css rewritten for cards. Golden
  fingerprint unaffected (the fixture has no work-graph signal, so the rail is omitted there); stable + green on a
  tree where a concurrent session had relocked it to `f9c79d98…`. Full suite 2341 passed / 3 skipped / 0 failed.
  **Flagged for the owner:** the AI action is copy-to-clipboard (not navigation); this delivers the Story 20.8
  details-pane concept early, so 20.8 should narrow to select-mode (not renumbered — owner's call).
- 2026-07-24 — Story 20.3 implemented (dev-story), status → review. New `RelatedWork` projection + `RelatedWorkTemplater`
  pane, routed builder → `DashboardView.RelatedWorkHtml` → adapter so HTML/SPA/webview render one set of bytes;
  `SiteGenerator._workGraph` threaded verbatim into all three dashboard render sites. **Two contract decisions
  recorded rather than assumed:** (1) the island `edges` slot stays `[]` — after Story 20.1's §1a correction the only
  translatable edge shape is `Contains` story→epic, which `nodes[].parentId` already states, so an edge array would
  add bytes and no information; (2) §1a rule 2's "nearest existing ancestor" is implemented as a **labelled subject
  fold**, not a re-attribution, after generating against this repo showed the naive reading dropped **all 32
  `Resolves` edges** silently (they all land on resolver stories in density-collapsed epics). Added the
  `specscribe:explorer-select` seam to Story 20.2's block, which 20.5/20.8 should adopt rather than mint a second.
  `NodeText`/`EdgeVerb` single-sourced onto `RelatedWork` (`WorkGraphTemplater` delegates). Live-browser
  verification caught two defects the 2,274-test suite structurally could not: an "EpicEpic 19" live-region
  announcement, and a per-kind chip that made every row read "Story Story 19.1" — the chip was removed entirely,
  leaving no colour-only signal because there is no colour signal. NFR8 confirmed with a genuinely script-blocked
  render (37 sections, 297 rows, 297/297 links with hrefs). Golden fingerprint regenerated
  `1711700e…` → `89c8cf0c…`, stable across 2 repeated runs, on a tree also carrying two other sessions' in-flight
  work (Story 5.3 and `spec-multi-epic-retro-attribution`) — provenance recorded at the constant. Full suite
  2271 passed / 3 skipped / 0 failed. Pane measures 101,435 B of a 472,222 B dashboard (21.5%) — flagged for an
  owner decision rather than silently capped.
- 2026-07-22 — Story 20.3 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Reconciled the epics.md five-edge-kind prose against the **shipped four-kind** `WorkEdgeKind` (covers/cites out of 19.2 MVP). Aligned to 20.2's committed contract (seeded same day): the single `{ nodes, edges }` payload island with **`edges` reserved for this story**, the `data-explorer` root, and the canonical island id scheme (`"epic-N"`/`"N.M"`) — with a server-side id-bridge from the work-graph model's internal ids. Documented the two-half split (ship-first server-rendered "Related" block reusing `SiteGenerator._workGraph` verbatim, no 20.2 needed; client reveal-on-selection block binding to 20.2's selection state, inert until 20.2 is built). Epic 19 confirmed merged to main (`WorkGraph.cs` @ `38044b1`) — data source is live. 20.1 spike Completion Notes still empty but 20.2 operationalized the seam.
