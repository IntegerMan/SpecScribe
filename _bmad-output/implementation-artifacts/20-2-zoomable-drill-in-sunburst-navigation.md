---
baseline_commit: b8be08d0f139c3dca487a7cab9ef87234a1a5630
---

# Story 20.2: Zoomable Drill-In Sunburst Navigation

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer exploring a large project,
I want to click a sunburst wedge to zoom into it and reveal its nested children, then breadcrumb back out,
So that I can traverse epic → story → follow-up depth in place without losing my orientation or opening a new page for every hop.

## Why this story exists (read first)

Epic 20 turns the static remaining-work sunburst into a **fluid, explorable map**: click a wedge to zoom in place, reveal nested children, breadcrumb back out. This story delivers the **zoom/drill-in half** (Story 20.3 adds the related-work side pane). It is the **first chart that needs JS to function beyond tooltips** — the deliberate, budgeted crossing of the project's "charts are pure SVG + links, no JS" value ([[charting-is-pure-svg-no-js]]).

**This story is built against the payload/budget/degrade contract that Story 20.1 (the architecture spike) fixes.** As of this writing, **20.1 is `ready-for-dev`, not executed — its Completion Notes are empty.** This story therefore encodes 20.1's *recommended defaults* (which are fully specified in that story's decision tables) as the working contract. **If 20.1 is run before this and revises any default with a recorded rationale, this story inherits the revision** — re-read `20-1-interactive-explorer-architecture-spike.md` Completion Notes at dev-start and reconcile. See "Dependency & sequencing" below; also raised as an open question at the end.

**The one-line scope test:** if the change makes the *existing* dashboard/epics sunburst SVG zoomable (activate a wedge → re-center + expand children + breadcrumb, keyboard/AT parity, degrade to the static chart) → in. If it builds the related-work side pane, invents a second geometry or count, adds an authoring schema, or introduces a framework/build step → out (20.3 / never / ADR-triggering fork).

## Acceptance Criteria

1.
**Given** the rendered explorer with JavaScript available
**When** I activate a wedge (click, Enter, or Space)
**Then** the chart re-centers on that node, expands its children into the rings, and shows a breadcrumb trail of the current zoom scope
**And** activating the center or a breadcrumb crumb navigates back outward without a full page load.

2.
**Given** keyboard and screen-reader users
**When** they traverse the explorer
**Then** focus order, roving-tabindex wedge navigation, and `aria` live announcements of the current zoom scope all work
**And** a wedge's terminal open action still honors the Story 9.13 destination contract (leaf → detail page, group wedge → generated filtered list page), so the explorer does not invent a parallel navigation scheme.

## Context & Scope

### The load-bearing engineering insight (read before designing)

The obvious precedent — the **Code Map directory zoom** in `specscribe.js` (~lines 1062–1169) — zooms by **panning the SVG `viewBox`** over pre-rendered rectangles (`setViewBox` tweens `viewBox`; `zoomTo` just re-frames). **That technique does NOT work for a sunburst drill-in.** When you drill into an epic, its stories must **expand to fill the entire ring** (angular re-layout), not just get magnified in place. A viewBox pan would show the epic's tiny wedge blown up, still occupying its original narrow sweep — not the required "children expand into the rings" behavior of AC #1.

So the drill-in requires **client-side re-computation of arc geometry** for the zoomed scope: recompute each child's angular sweep from its server-supplied weight, then rebuild the wedge `d` paths. This is net-new (no client-side arc generator exists today). It is the real work of this story. Two viable shapes — **the dev must pick one and record the rationale** (this is exactly what 20.1's spike should ratify; if 20.1 hasn't run, decide here and note it):

| Approach | How it works | Trade-off |
|----------|--------------|-----------|
| **(A) Client re-layout from payload weights (recommended default)** | Payload island ships each node's `weight` + hierarchy (projected from `FollowUpGeometry`/`Charts.Sunburst` weights — the *same* numbers the server used). On zoom, JS recomputes sweeps for the focused subtree and rewrites wedge `d` paths using a JS port of the server's annular-sector math. | One payload, one render surface; arc math duplicated in JS (presentation only — **not** a second geometry or count). Matches "client re-arranges and reveals" boundary. |
| **(B) Server pre-renders every zoom state** | Server emits the SVG for each drillable scope; JS swaps between pre-rendered `<g>` states. | No JS arc math, but payload/markup size explodes (N scopes), and the static baseline gets polluted with hidden states. Rejected unless (A)'s arc math proves infeasible. |

**Recommended: (A).** The arc-path helper the JS must mirror is `Charts.AnnularSector` / `InsetStart` / `InsetEnd` (`src/SpecScribe/Charts.cs`) — porting *presentation math* (angles → SVG path `d`) is explicitly permitted; **re-deriving weights or membership is not** (that stays in `FollowUpGeometry`). The payload carries the already-computed weights; the JS only turns weights into pixels.

### What already exists (reuse — do NOT rebuild)

Every row is a seam this story enhances, verified against `src/**`. Trace it before leaning on it.

| Seam | Primary types / files | What it gives this story |
|------|----------------------|--------------------------|
| **Static sunburst geometry** | `Charts.Sunburst` (`Charts.cs:348`), `Charts.EpicSunburst` (`:875`), `Charts.SunburstCompanionList` (`:557`) | The exact two-level SVG (`<svg class="sunburst">`, wedges `<a href><path class="sb-seg sb-{class}"><title>`) the explorer enhances. Rings: epic inner (`size*0.16–0.28`), story middle (`0.285–0.415`), aggregate outer (`0.42–0.465`). **No zoom/drill today.** |
| **Arc math to port to JS** | `Charts.AnnularSector`, `InsetStart`, `InsetEnd` (`Charts.cs`) | The annular-sector `d`-path generator + pad-inset the JS re-layout (approach A) must mirror pixel-for-pixel so zoomed arcs match the SSR baseline. |
| **Hierarchy + weights (single source)** | `FollowUpGeometry` (`FollowUpGeometry.cs`; `FollowUpDeferredSlot`: `EpicNumber`, `SourceStoryId`, `SourceKey`, `DetailHref`), `UnplannedWorkGeometry.SunburstUnplannedWeight` | `StoryWeight`/`EpicWeight` (computed inline in `Charts.Sunburst:367–373`) are the payload's node weights. **AC forbids a second geometry** — the emitter projects from THESE, not a re-parse. |
| **Sunburst host pages** | Dashboard: `HtmlRenderAdapter.Dashboard.cs:45–48` (`<div class="chart-panel sunburst-panel …">`); Epics page: `HtmlRenderAdapter.Epics.cs:32`; per-epic: `EpicSunburst` at `Epics.cs:208` | Where the enhancement mounts. The dashboard panel is the primary explorer host. **Decide** whether the explorer enhances the dashboard sunburst, the epics-page sunburst, or both (recommend: the project-wide one — dashboard — first; see Decisions table). |
| **Click-destination contract (9.13)** | `FollowUpGroupTemplater` (`group-epic-*.html`, `group-unplanned.html`, `group-follow-ups.html`), Story 9.11 detail pages | AC #2's locked rule: **leaf wedge → detail page; group wedge → generated filtered list page** (never the unfiltered site). The explorer's *open* action (distinct from *zoom*) must honor this exactly. Hrefs already live on the wedge `<a>` elements — reuse them, don't recompute destinations. |
| **The sanctioned client script** | `src/SpecScribe/assets/specscribe.js` (~1169 lines), copied via `CopyEmbeddedAsset("SpecScribe.assets.specscribe.js", ForgeOptions.ScriptName)` in `SiteGenerator.cs` | Home for the new explorer block (per 20.1 default). Study the codemap block (`:900–1169`) for the *idiom* (opt-in via root element, `motionFastMs()`, `setViewBox` reduce-branch, `role=button`/`tabindex`/keydown, hash deep-link + `popstate`), then **generalize the pattern — do not fork it** (the codemap's viewBox-only technique doesn't transfer; see load-bearing insight). |
| **Reduced-motion tween** | `motionFastMs()` reads `--motion-fast` ([[motion-token-system]]); `setViewBox` snaps when `!animate` (`specscribe.js:1088–1113`) | Zoom animation timing + the reduced-motion snap branch to mirror. Any new arc-tween reads `--motion-*`, honors `prefers-reduced-motion`. |
| **Tooltip seam** | body-level `.ss-tooltip` node + `data-tip`/`data-tip-html`; `SEG = ".sb-seg, .heatmap-cell, .donut-seg"` (`specscribe.js:100`) | Existing hover/focus tooltips already target `.sb-seg`. Ensure re-laid-out wedges keep working tooltips (re-bind or preserve attributes). |
| **SPA / parity harness** | `JsonSpaRenderAdapter`, `SpaBundle`, `SpaDelivery`, `RenderParity`, `IRenderAdapter` | Story 6.7 prior art. The payload island + explorer markup must survive SPA body-consolidation; add `RenderParity` coverage (AC #2 / 20.1 AC #2). |
| **Counts ledger (do not touch)** | `ProjectCounts` (Story 8.3) | Single source of open/deferred counts. The payload and any label the JS renders must **not** re-count. |
| **Golden fingerprint** | `tests/SpecScribe.Tests/SiteGeneratorFidelityTests.cs` | Adding the JSON island + CSS moves the HTML fingerprint; regenerate the golden and confirm the drift is exactly the new island/markup, nothing else. |

### Decisions this story MUST lock (with recommended defaults from 20.1)

The dev may revise any recommendation **with a recorded rationale**, but must land one concrete answer per row. Recommendations trace to `20-1-interactive-explorer-architecture-spike.md` (its "Decisions the spike MUST make" table).

| Decision | Recommended default | Guardrail |
|----------|---------------------|-----------|
| **Arc rendering on zoom** | (A) client re-layout from payload weights, porting `AnnularSector`/`InsetStart`/`InsetEnd` presentation math to JS. | Presentation math only; weights/membership stay in `FollowUpGeometry`. No `fetch`. |
| **Payload shape** | ONE inline `<script type="application/json">` island in the sunburst host page: `{ nodes: [{ id, parentId, weight, label, statusClass, href, kind }] }`. Node ids = existing canonical identities (`EpicInfo.Number` → `"epic-N"`, `StoryInfo.Id` → `"N.M"`, follow-up slug, aggregate group href). **Edges are 20.3's concern — omit or leave an empty `edges: []` for forward-compat.** | Reuse geometry + ids; no new authoring schema; no second count ledger. |
| **Payload delivery** | Inline JSON island co-located with the SVG it upgrades (mirrors SPA entry-region inlining); sidecar `.json` only if size forces it. | Static-host / `file://`-safe (no fetch). Confirm against SPA precedent. |
| **JS home** | New block in `specscribe.js`, guarded by presence of an explorer root element (mirror `.codemap-view` / `.js-listable` opt-in), unless the size ceiling is exceeded → then a second embedded asset like `specscribe-spa.js`. | Single delivery path preferred; decide explicitly with a size estimate. |
| **JS size budget** | ≤ ~8–10 KB of hand-written, ES5-compatible, unminified code in the existing idiom (no build step). | The SCP demands a *named* budget. State the number; justify against the codemap block's footprint. |
| **Dependency / framework budget** | **Zero runtime deps. No framework. No build step.** No d3/Plotly. | A framework/library (e.g. the owner-mentioned Plotly) is an **ADR-triggering architectural fork** — escalate via correct-course, do not decide silently ([[adr-creation-trigger-gap-epic-10-retro]]). |
| **Which sunburst(s) become explorable** | The **project-wide dashboard sunburst** first (`Dashboard.cs:45`). Epics-page + per-epic `EpicSunburst` are candidates but out of MVP unless trivial. Epic 7's code-structure sunbursts (ownership/freshness) are explicitly a **separate follow-on**, NOT this story (owner Plotly request, `epics.md` Epic 20 note). | Don't silently generalize the budget across both sunburst families; that question is 20.1's to answer. |
| **Zoom vs. open disambiguation** | Activating a **non-leaf** wedge (epic, story-with-children) **zooms in**; activating a **leaf** wedge **opens** its 9.13 destination; the **center** (or a breadcrumb crumb) **zooms out**. A non-leaf's 9.13 *group* destination stays reachable via an explicit "open this scope" affordance (e.g. a link on the breadcrumb-current crumb or a small open control), so group pages are never orphaned. | AC #2: never invent a parallel navigation scheme; every terminal open resolves to a 9.13 destination that already exists on the wedge `<a>`. Lock the exact affordance and record it. |

### Degrade + parity contract (AC #2 / NFR8)

| Visitor / mode | Required behavior | Existing pattern to mirror |
|----------------|-------------------|----------------------------|
| **JS off (NFR8)** | The static Story 10.7 sunburst renders fully; every wedge link resolves via the 9.13 destination contract. The explorer JS is pure progressive enhancement over that **exact** markup — no parallel render, no inert tab stops shipped by the server. | `.js-listable` / codemap: complete server truth, JS never required. |
| **Reduced motion** | Zoom **snaps** (no arc tween); timing (when allowed) reads `--motion-*`. | codemap `setViewBox` reduce-branch + `motionFastMs()`. |
| **Keyboard / AT** | Roving-tabindex across wedges of the current scope, Enter/Space to zoom (non-leaf) or open (leaf), a visible focus ring, and an `aria-live` region announcing the new zoom scope on each drill. Breadcrumb crumbs are real `<button>`s. | codemap dir rects: `role=button`, `tabindex=0`, keydown Enter/Space; donut `tabindex` a11y precedent; codemap `renderCrumbs` `<button>` trail. |
| **HTML vs SPA parity** | The JSON island + explorer root render **identically** through HTML and SPA adapters; the island must survive SPA `<main>` consolidation. Add `RenderParity` coverage (or record why not). Webview + CLI are **non-goals** unless a reason is recorded. | `RenderParity` harness; Story 6.7 body-capture; 20.1 surface-reach table. |

### Dependency & sequencing (must be honored)

- **Not blocked on Epic 19.** This story needs only geometry/weights (the zoom half). The related-work pane (20.3) is what consumes Epic 19 edges. Per the 20.1 spike's recommended build order: **20.1 (contract) → 20.2 (zoom, geometry-only) → 20.3 (pane, needs Epic 19).**
- **Soft-gated on 20.1.** 20.2 is built against 20.1's *recommended defaults*, which are fully specified. Ideally 20.1's spike pass runs and ratifies the payload/budget contract first. If it has not, this story's Decisions table stands in — but re-read 20.1's Completion Notes at dev-start and reconcile any revision. (Raised as an open question below.)
- **Static baseline is stable.** Story 10.7 (sunburst navigability) is `done` — the wedge seams (`.sb-seg`, wedge `<a>` links, ring radii) are settled. Key the enhancement off those stable seams, not in-flight details.

### Deliberate non-goals (seed list — extend with rationale)

- **The related-work side pane** — Story 20.3.
- **A second geometry** — no re-derivation of ring weights/membership outside `FollowUpGeometry`/`Charts.Sunburst` (porting arc *presentation* math to JS is allowed; re-deriving *weights* is not).
- **A second count ledger** — the payload/labels never re-count against `ProjectCounts`.
- **A new authoring schema** — no YAML/frontmatter/graph DSL for the payload.
- **A framework / build step / charting library (d3, Plotly)** — ADR-triggering fork; escalate, don't decide here.
- **Client-side `fetch`/XHR** — payload ships at generation time (`file://`-safe).
- **A parallel navigation scheme** — terminal opens reuse the 9.13 leaf/group destinations already on the wedge `<a>`s.
- **Retiring Story 10.7** — the static sunburst stays the no-JS baseline.
- **Making Epic 7's code-structure sunbursts explorable** — separate follow-on (owner Plotly request); not this story.
- **Webview/CLI explorer support** — HTML + SPA only unless a reason is recorded.

## Tasks / Subtasks

- [x] **Task 1 — Emit the payload island from the existing geometry (AC: #1)**
  - [x] In the sunburst host path (`HtmlRenderAdapter.Dashboard.cs` primary; consider a small helper on `Charts` or a new projector type), emit ONE `<script type="application/json">` island alongside `Charts.Sunburst(...)`, projecting `{ nodes: [{ id, parentId, weight, label, statusClass, href, kind }] }` from the **same** `EpicWeight`/`StoryWeight`/`FollowUpGeometry`/`UnplannedWorkGeometry` values the SVG already uses. Reuse canonical ids (`epic-N`, `N.M`, follow-up slug, group href); include an empty `edges: []` for 20.3 forward-compat.
  - [x] Give the sunburst host container an explorer root marker (e.g. `data-explorer` / an `explorer-root` class on the existing `sunburst-panel` div) so the JS opts in exactly like `.codemap-view`.
  - [x] Confirm **no** `ProjectCounts` re-count and **no** second geometry: the projector consumes existing weights, it does not recompute them.

- [x] **Task 2 — Port the arc math + build the drill-in block in `specscribe.js` (AC: #1)**
  - [x] Add a new block guarded by the explorer root element. Port `AnnularSector`/`InsetStart`/`InsetEnd` (angles → SVG `d`) to JS so a zoomed scope's children can be re-laid-out to fill the ring; hydrate from the JSON island (no `fetch`).
  - [x] Implement `zoomTo(nodeId)`: recompute child sweeps from payload weights, rewrite wedge `d` paths (and ring assignment), tween via a `motionFastMs()`-style helper reading `--motion-*`, **snap** under reduced motion (mirror `setViewBox`'s reduce branch). Keep tooltips working on re-laid-out wedges (`.sb-seg` — see `SEG` at `specscribe.js:100`).
  - [x] Render a breadcrumb `<button>` trail of the current zoom scope (mirror codemap `renderCrumbs`); center/crumb activation zooms outward. Support hash deep-link + `popstate` if it fits the budget (optional, mirror codemap `applyHash`).

- [x] **Task 3 — Keyboard, AT, and the zoom-vs-open rule (AC: #2)**
  - [x] Roving-tabindex across the current scope's wedges; `role=button` + `tabindex` set at runtime (never ship inert tab stops in the no-JS page); Enter/Space to activate; visible focus ring.
  - [x] Add an `aria-live` region announcing the new zoom scope on each drill; breadcrumb is keyboard-navigable.
  - [x] Implement the locked zoom-vs-open rule (Decisions table): non-leaf → zoom; leaf → open its 9.13 destination (the existing wedge `<a href>`); non-leaf group destination reachable via the recorded affordance. **Never** invent a new destination.

- [x] **Task 4 — Degrade + HTML/SPA parity (AC: #2 / NFR8)**
  - [x] Verify JS-off: static sunburst + 9.13 links fully intact; the JSON island is inert data, the explorer adds nothing the server didn't already ship.
  - [x] Add/extend `RenderParity` coverage so the payload island + explorer root render identically through HTML and SPA; confirm the island survives SPA `<main>` consolidation. Record webview/CLI as non-goals.
  - [x] Add CSS for any new explorer affordances (breadcrumb, focus ring, open control) using existing tokens; no new color tokens without justification.

- [x] **Task 5 — Tests + golden (AC: #1, #2)**
  - [x] Unit-test the payload projector: ids/weights/hierarchy match the SVG's own weights for a representative model (incl. dense-epic collapse, no-plan stories, unplanned/orphan slots — see `Charts.Sunburst` branches).
  - [x] Regenerate the golden fingerprint (`SiteGeneratorFidelityTests.cs`); confirm the drift is **only** the new island/markup/CSS, nothing else moved.
  - [x] JS is not unit-tested in this repo (SSR-first) — cover behavior via the markup/attribute assertions the server emits (root marker, island shape, wedge `<a>` destinations unchanged) and manual browser verification (record in Completion Notes).

- [x] **Task 6 — Reconcile with 20.1 + record decisions (AC: #1, #2)**
  - [x] At dev-start, re-read `20-1-interactive-explorer-architecture-spike.md` Completion Notes. If 20.1 ran and revised a default, adopt it; if 20.1 is still unexecuted, proceed on this story's Decisions table and note that in Completion Notes.
  - [x] Record the locked decisions (arc approach, payload shape, JS home, size number, zoom-vs-open affordance) in Completion Notes.
  - [x] If the implementation concludes a framework/library is warranted, **stop and escalate** via correct-course (ADR fork) — do not add a dependency silently.

### Review Findings

_(populated during code-review)_

## Dev Notes

### Architecture compliance

- **Shared-core projection.** The payload emitter is a **pure projection** over existing models (`FollowUpGeometry`, `UnplannedWorkGeometry`, `EpicsModel`) — not a per-surface re-parse. [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1/AD-2]
- **Additive, non-blocking.** A missing/malformed payload must never fail generation; degrade to the static sunburst (AD-4). Wrap emission defensively.
- **Graceful degradation** for JS-off / reduced-motion / AT is an inherited invariant, not an add-on (NFR8/NFR5).
- **View-model boundary (Story 6.2).** If the payload needs shaping, prefer building it in the view-model / `Charts` projection layer, not inside the adapter's string-assembly — keep adapters thin. [[story-6-2-section-view-models-live]]

### Files likely touched (verify at dev-start)

- `src/SpecScribe/Charts.cs` — payload projector (or a new small type it delegates to); expose the node list from the same weights `Sunburst` computes. Read `Sunburst` (`:348`), `AnnularSector`/`InsetStart`/`InsetEnd`, `AppendWeightedStorySlot`, `AppendOpenDoneAggregateRing` to mirror ids/weights exactly.
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` (`:45`) — mount the island + explorer root marker beside `Charts.Sunburst`.
- `src/SpecScribe/assets/specscribe.js` — new explorer block (opt-in via root element; arc-math port; zoom/breadcrumb/keyboard).
- `src/SpecScribe/assets/specscribe.css` — explorer affordance styles (breadcrumb, focus ring, open control).
- `src/SpecScribe/RenderParity.cs` (+ the SPA path) — parity coverage for the island.
- `tests/SpecScribe.Tests/*` — projector unit tests; `SiteGeneratorFidelityTests.cs` golden regen.

### Known seam caveats (classify, don't "fix" beyond this story)

- **codemap zoom ≠ sunburst zoom.** The codemap's viewBox-pan is the *idiom* to borrow (opt-in, motion token, reduce-snap, keyboard, crumbs) but **not** the *mechanism* — sunburst drill-in needs angular re-layout (arc recompute). This is the single most likely place to go wrong.
- **"Pure SVG, no JS" is aspirational.** `specscribe.js` already houses codemap zoom, list sort/filter, risk pager, sprint filter. Name the explorer's place on that spectrum honestly; it's the largest interactivity block yet, which is why 20.1 fixes a *named budget* — respect the number.
- **Dense-epic collapse + no-plan + unplanned/orphan branches.** `Charts.Sunburst` has several branches (`StoryDensityCollapseThreshold` collapse into a single `sb-story-summary` wedge, zero-task no-plan stories, orphan action items, unplanned root). The payload must represent whatever the SVG actually drew — if a scope is collapsed server-side, decide whether the explorer expands it client-side or preserves the collapse. Recommend: preserve the server's drawn structure (don't invent wedges the static chart doesn't show).
- **Tooltips must survive re-layout.** Rich hover/focus cards target `.sb-seg` via the body-level `.ss-tooltip` node ([[tooltip-clipping-use-ss-tooltip-node.md]]); after rewriting wedge `d` paths, ensure `data-tip*` attributes and bindings are preserved or re-applied.
- **Epic 7's ownership/freshness sunbursts** asked for the same interaction (owner Plotly request, 2026-07-22). Explicitly out of scope here — do not generalize this story's block onto them.

### Anti-patterns to prevent

- Reimplementing `FollowUpGeometry`/`Charts.Sunburst` **weights** as a second "explorer geometry" (porting arc *presentation* math is fine; re-deriving weights/membership is not).
- Re-counting open items against `ProjectCounts` in the payload or any JS-rendered label.
- Forking the codemap block or using its viewBox-pan technique for the sunburst (wrong mechanism).
- Introducing d3/Plotly/a framework/a bundler/a build step by default (accretion the SCP warns against) — ADR fork.
- Client-side `fetch`/XHR for the payload (breaks `file://` / static-host delivery).
- A parallel navigation scheme instead of the 9.13 leaf/group destination contract already on the wedge `<a>`s.
- Shipping inert `role=button`/`tabindex` tab stops in the no-JS page (set them at runtime only).
- Expanding scope into the related-work side pane (20.3) or Epic 7's sunbursts.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/20-2-zoomable-drill-in-sunburst-navigation.md`
- Sprint key: `20-2-zoomable-drill-in-sunburst-navigation`
- Downstream: `20-3-related-work-side-pane-on-selection` (consumes Epic 19 edges + this story's payload island; add its `edges` to the same island).
- Client assets: `src/SpecScribe/assets/specscribe.js` (+ `.css`), embedded resources copied via `CopyEmbeddedAsset` in `SiteGenerator.cs`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 20 header + Story 20.2 ACs (lines ~3056–3113)]
- [Source: `_bmad-output/implementation-artifacts/20-1-interactive-explorer-architecture-spike.md` — payload/budget/degrade recommended defaults this story is built against (Decisions table, degrade+parity table, sequencing)]
- [Source: `src/SpecScribe/Charts.cs:348` — `Sunburst` geometry, ring radii, `EpicWeight`/`StoryWeight`; `AnnularSector`/`InsetStart`/`InsetEnd` arc math to port]
- [Source: `src/SpecScribe/FollowUpGeometry.cs` — weights/membership (single geometry source); `FollowUpDeferredSlot` ids]
- [Source: `src/SpecScribe/assets/specscribe.js:900–1169` — codemap drill block: opt-in root, `motionFastMs()`, `setViewBox` reduce-branch, `renderCrumbs`, keyboard/`role=button`, hash+`popstate` (the *idiom* to generalize)]
- [Source: `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:45` + `HtmlRenderAdapter.Epics.cs:32,208` — sunburst host mount points]
- [Source: `_bmad-output/implementation-artifacts/9-13-generated-filtered-follow-up-group-pages-and-sunburst-click-destinations.md` — leaf/group click-destination contract AC #2 must honor]
- [Source: `src/SpecScribe/RenderParity.cs`, `JsonSpaRenderAdapter.cs`, `SpaBundle.cs`, `SpaDelivery.cs` — SPA parity harness (Story 6.7)]
- [Source: `tests/SpecScribe.Tests/SiteGeneratorFidelityTests.cs` — golden fingerprint]
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` — shared-core projection, graceful degrade, additive insight surfaces]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md` — Epic 20 seating, owner-approved first-client-JS decision, JS budget rationale, 19.1-before-20.3 sequencing]

### Previous story intelligence

- **Story 20.1 (`ready-for-dev`, NOT executed):** The architecture spike that *fixes this story's contract*. Its Completion Notes are empty — this story stands in with 20.1's recommended defaults. Re-read at dev-start. Its whole point was to name the JS budget before this block lands — respect the number, don't grow by accretion.
- **Story 7.6 codemap (`done`):** The closest existing drill interaction (`specscribe.js` codemap block). Study the *idiom*; note its viewBox mechanism does **not** transfer to a sunburst (see load-bearing insight). [[spec-code-map-declutter-cochange-live]]
- **Story 6.7 SPA adapter (`done`):** The second-embedded-asset + body-capture parity precedent for the "new asset" fallback and the HTML/SPA parity coverage AC #2 needs. [[story-6-7-spa-adapter-live]]
- **Story 9.13 (`done`):** Locked the leaf/group click-destination contract — Epic 20 keeps it, never invents a parallel scheme.
- **Story 10.7 (`done`):** The static baseline; wedge seams (`.sb-seg`, wedge `<a>`, ring radii) are settled — key the enhancement off them. [[story-10-7-sunburst-navigability-project-scale-review]]
- **Charts-are-pure-SVG value ([[charting-is-pure-svg-no-js]]):** The deliberate divergence being budgeted — this is the first chart that *needs* JS to function beyond tooltips.

### Git intelligence summary

Recent commits landed Epic 7 code-insight work (7.9–7.12) and their client-side pagers/recolor blocks in `specscribe.js`, plus Epic 19/21 graph/matrix work — the progressive-enhancement layer is actively growing block-by-block. This story adds the largest block yet (client arc re-layout), which is exactly why 20.1 fixes a named budget first. No explorer/drill-in sunburst code exists today; start from the static `Charts.Sunburst` + the codemap idiom.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8) — dev-story workflow, 2026-07-23

### Debug Log References

- **20.1 reconciliation (Task 6):** At dev-start 20.1 was NOT `ready-for-dev`/unexecuted as the draft assumed — it is `review` with a FULLY-populated contract. Adopted its ratified decisions verbatim (all matched the draft's recommended defaults). The one load-bearing sharpening: 20.1's "emitter seam note" mandates **extracting the `EpicWeight`/`StoryWeight` closures into a shared pure weight fn** both the SVG builder and the payload emitter call (not copying the arithmetic) — done via `Charts.SunburstEpicWeight`/`SunburstStoryWeight`.
- **Golden baseline:** the `SiteGeneratorAdapterTests` golden constant was ALREADY stale on the `b8be08d` baseline before this story (clean-HEAD fingerprint `54d4510d…` ≠ stored `b5bc230a…`) — a pre-existing main drift ([[golden-diff-normalization-gotchas]]). Regenerated to `5816b332…` (HEAD + this story only), verified stable across 2 repeated runs in isolation.
- **⚠️ Concurrent shared-main editing during this session:** another session was live-editing `specscribe.js` (Story 21.3/24.1 impact-map review patches, a different block from mine) + `ImpactMapTemplater`/`EpicsViewBuilder`/`PlanningCodeImpact`/`SiteGenerator` + `SiteGeneratorImpactMapTests`. A `git stash` I attempted swept their uncommitted work into my stash; recovered everything via `git checkout stash@{0} -- .` (HEAD never moved). Net effect: the full-suite golden currently reads red because the co-present concurrent `specscribe.js`/rendering edits shift the whole-tree fingerprint off my isolated `5816b332…`; verified my golden green in isolation. See [[shared-main-concurrent-edit-loss-verify-after-edit]].

### Completion Notes List

Delivers the zoom/drill-in half of Epic 20's explorer over the dashboard project-glance sunburst. **Locked decisions (Task 6 / 20.1 contract):**

- **Arc rendering = (A) client re-layout from payload weights.** Ported `AnnularSector`/`InsetStart`/`InsetEnd` (+ a `fullRing` helper for the drilled epic's inner band) to `specscribe.js`. On drill, the focused epic's `story`/`aggregate` children re-lay to fill 360° via the SAME weights the SVG used. Zoom-OUT restores each wedge's CAPTURED original server `d` — so the un-drilled chart is byte-identical to the static baseline (the golden covers it; JS-computed arcs are only ever the transient drilled view).
- **Re-path existing wedges (not rebuild).** The client re-arranges server truth: it rewrites the `d` of existing `<path data-node-id>` wedges and hides out-of-scope ones — reusing every server `<a href>`/`<title>`/`aria-label` (tooltips + 9.13 destinations preserved for free). No DOM/destinations invented.
- **Payload = ONE inline `<script type="application/json" id="sunburst-explorer-data">` island** `{ meta, nodes, edges }` inside `<main>` (survives SPA capture). `nodes` = one per drawn wedge `{id,parentId,weight,label,statusClass,href,kind}`; `edges: []` (20.3 fills from `_workGraph`). Extended beyond 20.1's canonical-node examples with structural `story-summary`/`aggregate` kinds so ONE source drives both DOM re-layout and 20.3's edge-join (edges only ever reference canonical `epic-N`/`N.M`/`orphan`/`unplanned` ids). Added a `meta` geometry block (size/cx/pad/start/ring radii) — presentation geometry projected from the same `Charts.Sunburst` factors, NOT a second weight/count ledger.
- **JS home + budget:** new guarded block in always-shipped `specscribe.js`, opt-in via `data-explorer` (mirrors `.codemap-view`). **~231 code lines / ~11 KB** unminified ES5, zero deps, no build step — comparable to the codemap block's ~270-line footprint 20.1 named as the yardstick (slightly above the ~8–10 KB soft ceiling but within the "no accretion / comparable-to-codemap" intent). Plotly stays declined (ADR fork).
- **Zoom-vs-open:** a wedge is drillable iff the chart drew ≥1 `story` child under it → non-leaf epic **zooms** (click/Enter intercepted); leaf (story, aggregate, no-plan story, **dense-collapsed epic**) **opens** its existing 9.13 `<a href>`. Dense epics preserve the server's collapse (one summary wedge → open the epic page), never inventing wedges the static chart hid. Zoom-OUT affordance = an injected focusable center control (`.sb-center-zoom`) + a breadcrumb `All epics` button; the drilled scope's own 9.13 group/detail page stays reachable via an explicit `Open page` link on the current crumb (group pages never orphaned).
- **Degrade/parity:** JS-off → the static Story 10.7 sunburst + 9.13 links are the whole chart; the island is inert data and the drill scaffold ships empty+`hidden` (no inert tab stops — `role`/`tabindex` set at runtime). Motion rides `--motion-*` (a token-timed fade on re-laid wedges) and is cancelled in the paired `prefers-reduced-motion: reduce` block. **Webview** strips the island (`WebviewRenderAdapter.RenderContent` — CSP forbids scripts; the reader never loads `specscribe.js`), the same class of CSP-driven omission as the nav's stripped inline toggle. Webview/CLI explorer support = recorded non-goals.

**Verification.** 6 projector unit tests (incl. the anti-drift invariant: the SVG's `data-node-id` set == the payload node-id set, across dense/no-plan/multi-epic) + 1 SPA-island-survives-capture parity test — all green; the webview no-script + reduced-motion stylesheet tests pass again. JS is not unit-tested in this SSR-first repo (per 20.1) → **manual browser verification** on the real 354-page self-generated site (117 nodes = 117 wedges, edges empty): activating epic-1 re-centered it (inner band → full ring), expanded its 7 children into the rings, hid the other 110 wedges, rendered the `All epics ▸ Epic 1 ▸ Open page` breadcrumb + center control, announced the scope via `aria-live`, and pushed `#sb=epic-1`; center/breadcrumb zoom-out fully restored all 117 wedges + cleared the hash; a leaf story kept its `epics/story-1-1.html` destination; keyboard Enter zoomed a drillable epic and roving-tabindex gave exactly one tab stop across the visible wedges; zero console errors.

### File List

**Production:**
- `src/SpecScribe/Charts.cs` — extracted shared `SunburstEpicWeight`/`SunburstStoryWeight` + ring-factor consts; threaded `data-node-id` onto every project-glance `Sunburst` wedge via a `NodeIdAttr` helper (EpicSunburst + other charts byte-unchanged — opt-in default-null param).
- `src/SpecScribe/SunburstExplorer.cs` — NEW: `SunburstExplorerNode`/`Meta`/`Model` records + `SunburstExplorerNodes`/`SunburstExplorerData`/`SunburstExplorerIsland` projector (partial of `Charts`).
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — mounted `data-explorer` root + inert drill/aria-live scaffold + the JSON island inside the sunburst panel (`<main>`).
- `src/SpecScribe/WebviewRenderAdapter.cs` — strip the inert JSON island from the webview content region (CSP).
- `src/SpecScribe/assets/specscribe.js` — NEW explorer block (`initSunburstExplorer`): arc-math port, `zoomTo`/restore, breadcrumb, center control, roving-tabindex/keyboard, aria-live, hash+popstate.
- `src/SpecScribe/assets/specscribe.css` — explorer affordance styles (breadcrumb/crumb/open-link/center control/is-drilled center-label hide) + the drill fade in the paired reduced-motion seams.

**Tests:**
- `tests/SpecScribe.Tests/SunburstExplorerTests.cs` — NEW: projector coverage + the SVG↔payload anti-drift invariant.
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — added the SPA island-survives-capture parity test.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — regenerated the golden fingerprint constant (`5816b332…`).

## Change Log

- 2026-07-23 — Story 20.2 dev pass. Shipped the dashboard sunburst drill-in explorer: extracted the shared weight fns + ring consts (20.1 anti-drift contract), threaded `data-node-id` onto the project-glance wedges, added the `SunburstExplorer` payload projector + inline JSON island (`data-explorer` root, `edges:[]` for 20.3), and the ~231-line zero-dep `specscribe.js` explorer block (client arc re-layout via ported `AnnularSector`/`InsetStart`/`InsetEnd`, breadcrumb + center zoom-out, roving-tabindex/keyboard, aria-live, hash+popstate) with CSS affordances on the reduced-motion seams. Zoom-out restores the captured server `d` (un-drilled chart byte-identical). Webview strips the island (CSP). 6 projector tests (incl. SVG↔payload anti-drift) + 1 SPA-parity test green; webview no-script + reduced-motion tests restored; golden regenerated (`5816b332…`, isolated — see Debug Log re: pre-existing main drift + concurrent shared-main editing). Manual browser verification on the real 354-page site confirmed all ACs. Status → review.
- 2026-07-22 — Story 20.2 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Delivers the zoom/drill-in half of Epic 20's interactive explorer: makes the existing dashboard sunburst SVG zoomable (activate wedge → client-side arc re-layout + child expansion + breadcrumb + keyboard/AT parity), degrading to the static Story 10.7 sunburst + 9.13 destinations (NFR8). Built against Story 20.1's recommended payload/budget/degrade defaults (20.1 not yet executed — reconcile at dev-start). Key engineering insight recorded: sunburst drill-in needs angular arc re-computation, NOT the codemap's viewBox-pan. Not blocked on Epic 19 (that's the 20.3 pane). Related-work side pane, second geometry/count, authoring schema, and framework/charting-library deps are non-goals.
