# Story 20.1: Interactive Explorer Architecture Spike

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer introducing the project's first rich client-interactive surface,
I want the client-interactivity boundary, data payload, and degrade-to-static contract scoped before any explorer ships,
So that we cross the "pure SVG, no JS" line deliberately and once, with a named budget rather than by accretion.

## Why this story exists (read first)

Seated 2026-07-19 by the correct-course SCP (`_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md`): the owner wants the static remaining-work sunburst turned into a **fluid, explorable map** — click a wedge to zoom in place, reveal nested children, breadcrumb back out — paired with a **live side pane** showing the work-graph nodes related to the current selection. That is Epic 20's payoff, delivered in Stories 20.2 (zoomable drill-in) and 20.3 (related-work pane).

Epic 20 is **SpecScribe's first substantial client-side interactive surface** — a deliberate, one-time, budgeted crossing of the project's "charts are pure SVG + links, no JS" value ([[charting-is-pure-svg-no-js]]; `charting-is-pure-svg-no-js.md`). The SCP made this an **owner-approved architectural decision**, and made Story 20.1 the **spike that fixes the terms of that crossing before any explorer code lands**. Story 10.9's client-light list sort/filter also depends on this same interactivity budget existing rather than a second JS stack.

**The one-line test for "is this in scope?":** if the change *scopes* the interactivity boundary, *specifies* the single generation-time payload (reusing existing geometry + Epic 19 edges), *names* the JS/dependency/framework budget, and *documents* the degrade-to-static contract and HTML/SPA parity rules → in. If it *builds* the zoomable chart, the side pane, an SVG, a new payload emitter, or a new authoring schema → out; that is Story 20.2 / 20.3.

**Exploratory / not release-blocking, but budget-setting.** Unlike a pure inventory spike, this spike's output is a **contract the next two stories are built against**. The static Story 10.7 sunburst (in active dev) and Story 9.13 linked pages remain the no-JS baseline the explorer enhances — **not retired** (NFR8).

## Acceptance Criteria

1.
**Given** the existing static sunburst geometry and Epic 19's directed-edge model
**When** the spike defines the explorer's data contract
**Then** it specifies a **single generation-time payload** (node hierarchy + related-edge adjacency) that the client hydrates, **names the JS size and dependency budget** and **whether any framework is introduced**, and **confirms the payload reuses `FollowUpGeometry` / sunburst weights** rather than deriving a second geometry.

2.
**Given** JavaScript-off, reduced-motion, and assistive-technology visitors
**When** the spike documents the degrade contract
**Then** the static Story 10.7 sunburst plus Story 9.13 linked pages remain the **no-JS baseline**, and the interactive layer is defined as a **progressive enhancement over that exact markup** — not a parallel site or a second authoring schema — with **HTML/SPA parity rules** named for any new payload.

## Context & Scope

### What already exists (reuse — do NOT rebuild)

The spike confirms these by **tracing real code**, not by re-deriving from epics.md prose. Every column below is a claim the spike must verify against `src/**` before the contract can lean on it.

| Seam | Primary types / files | What it gives the explorer |
|------|----------------------|----------------------------|
| **Static sunburst geometry** | `Charts.Sunburst`, `Charts.EpicSunburst`, `Charts.SunburstCompanionList` (`src/SpecScribe/Charts.cs`) | Pure-SVG, two-level (epic → story/aggregate) wedge geometry with click-to-page links; the exact markup the explorer enhances. **No zoom/drill today.** |
| **Hierarchy + weights** | `FollowUpGeometry` (`FollowUpDeferredSlot`: `EpicNumber`, `SourceStoryId`, `SourceKey`, `DetailHref`), `UnplannedWorkGeometry.SunburstUnplannedWeight` | The single source of ring weights + membership; AC #1 requires the payload project from THIS, not a second geometry. |
| **Click-destination contract** | `FollowUpGroupTemplater` (`group-epic-*.html`, `group-unplanned.html`, `group-follow-ups.html`), Story 9.11 detail pages | Story 9.13's locked rule: **leaf wedge → detail page; group wedge → generated filtered list page** (never the unfiltered whole-site dump). The explorer's terminal open action must honor this exactly (20.2 AC #2). |
| **Work-graph edges (planned)** | Epic 19 (Stories 19.1 model-spike / 19.2 build) — proto-record today is `FollowUpDeferredSlot` + citation maps | The related-work side pane's edge source (`stemmed-from`, `resolves`, `covers`, `cites`, `raised-in`). **19.1/19.2 are `ready-for-dev`, not `done`** — see dependency caveat below. |
| **The sanctioned client script** | `src/SpecScribe/assets/specscribe.js` (~1058 lines), copied via `CopyEmbeddedAsset("SpecScribe.assets.specscribe.js", ForgeOptions.ScriptName)` in `SiteGenerator.cs` | The existing progressive-enhancement layer (tooltips, copy buttons, list sort/filter, codemap zoom+recolor, risk-grid pager, sprint filter). The explorer is either a **new block here or a new asset** — the spike decides (see below). |
| **Second embedded JS asset (precedent)** | `specscribe-spa.js` copied via `CopyEmbeddedAsset(..., SpaDelivery.ScriptName)` **only under `--spa`** (`SiteGenerator.cs` ~2722) | Prior art that a **second, purpose-scoped JS asset delivered as an embedded resource** is already an accepted pattern — informs the new-block-vs-new-asset decision. |
| **SPA / JSON delivery** | `JsonSpaRenderAdapter`, `SpaBundle`, `SpaDelivery`, `RenderParity`, `IRenderAdapter` | Story 6.7 prior art for a JS delivery surface + the parity harness the new payload's HTML/SPA parity rules plug into (AC #2). |
| **Motion tokens** | `--motion-*` CSS tokens + paired reduced-motion blocks ([[motion-token-system]]) | Any zoom/drill animation reads timing from here + honors `prefers-reduced-motion` (codemap zoom already does this — `motionFastMs()` in specscribe.js). |
| **Counts ledger** | `ProjectCounts` (Story 8.3) | Single source of open/deferred/direct counts — the payload and pane must **not** re-count against this. |
| **Tooltip seam** | body-level `.ss-tooltip` node + `data-tip` / `data-tip-html` ([[tooltip-clipping-use-ss-tooltip-node.md]]) | Rich hover/focus cards route through the existing never-clipped tooltip node, not a new one. |

### The core tension the spike must resolve (load-bearing)

The project's stated value is **"charts are pure SVG + links, no JS"** ([[charting-is-pure-svg-no-js]]). The **reality** is that `specscribe.js` is already ~1058 lines of sanctioned progressive enhancement, and `specscribe-spa.js` is a second embedded asset shipped under `--spa`. So "the ONE script" is aspirational, not literal. The spike's job is to **name where the explorer sits on that spectrum honestly** and set a ceiling, rather than let JS grow by accretion (the SCP's exact stated fear). AC #1's "named budget" is the antidote.

### Decisions the spike MUST make (AC #1) — with a recommended default for each

The dev may revise any recommendation **with a recorded rationale**, but must land on one concrete answer per row:

| Decision | Recommended default | Why / guardrail |
|----------|---------------------|-----------------|
| **Payload shape** | ONE generation-time JSON payload: `{ nodes: [hierarchy], edges: [related adjacency] }`, node ids = existing canonical identities (`EpicInfo.Number`, `StoryInfo.Id`, follow-up slug, code path), projected from `FollowUpGeometry` + Epic 19 edges. | AC #1: reuse geometry + weights, do NOT derive a second geometry or a second count model. |
| **Payload delivery** | Inline as a `<script type="application/json">` island in the sunburst's host page (hydrated in place), mirroring how the SPA inlines its entry region; a sidecar `.json` file only if size forces it. | Keeps the enhancement co-located with the exact markup it upgrades; static-host-safe (no fetch on `file://`). Confirm against SPA precedent. |
| **JS home: new block in `specscribe.js` vs new asset** | Recommend a **new, lazily-relevant block in `specscribe.js`** guarded by presence of an explorer root element (mirrors `.codemap-view` / `.js-listable` opt-in), UNLESS the size budget below is exceeded, in which case a second embedded asset (like `specscribe-spa.js`) delivered only when an explorer page is generated. | Single delivery path is simpler; the codemap zoom block is the closest existing precedent for comparable interactivity. Decide explicitly. |
| **JS size budget** | Name a concrete added-KB ceiling (recommend ≤ ~8–10 KB minified-equivalent of hand-written ES5-compatible code, in the style of the existing script — no build step). | The SCP demands a *named* budget; pick a number and justify it against the codemap block's footprint. |
| **Dependency budget** | **Zero runtime dependencies. No framework. No build step.** Hand-written, dependency-free, `file://`-safe ES5-compatible JS matching `specscribe.js`'s existing idiom. | The whole script today is dependency-free by deliberate design; introducing a framework here would be the accretion the SCP warns against. If the spike believes a framework is warranted, that is an **ADR-triggering architectural fork** — escalate via correct-course, do not decide silently ([[adr-creation-trigger-gap-epic-10-retro]]). |
| **Interactivity boundary** | Zoom/drill + breadcrumb + related-pane hydration are client-only enhancements; **all destinations, counts, and relationship data are server-rendered first**. The client re-arranges and reveals; it never fetches, never computes a count, never invents a destination. | Mirrors every existing block (server ships complete truth, JS enhances). Keeps NFR8/NFR5 satisfied by construction. |

### Degrade + parity contract the spike MUST document (AC #2)

| Visitor / mode | Required behavior | Existing pattern to mirror |
|----------------|-------------------|----------------------------|
| **JS off (NFR8)** | Static Story 10.7 sunburst renders fully; every wedge's link resolves via the Story 9.13 destination contract; the related-work data ships as a **server-rendered "Related" block**, never JS-gated (20.3 AC #2). | `.js-sortable` / `.js-listable`: complete server truth, JS never required. |
| **Reduced motion** | Zoom/drill **snaps** instead of tweening; timing (when allowed) reads `--motion-*`. | codemap `setViewBox` reduce branch + `motionFastMs()` in specscribe.js. |
| **Keyboard / AT** | Roving-tabindex wedge nav, Enter/Space activation, `aria` live announcement of zoom scope; terminal open still honors the 9.13 destination contract (20.2 AC #2). | codemap dir rects: `role=button`, `tabindex=0`, keydown Enter/Space; donut `tabindex` a11y precedent. |
| **HTML vs SPA parity** | The new payload island + explorer root must render **identically** through the HTML and SPA adapters; add coverage to `RenderParity` (or state why not). Webview and CLI are **non-goals** for the explorer unless the spike records a reason. | `RenderParity` harness; Story 6.7 body-capture; the 19.1 surface-reach table. |

### First-class questions the spike MUST answer (AC #1/#2)

1. **Where does the payload come from?** Cite the concrete `FollowUpGeometry` / `Charts.Sunburst` API the payload projects from, and confirm it introduces **no second geometry** and **no second count ledger** (`ProjectCounts`).
2. **What is the exact JS/dependency/framework budget?** A number, a "zero deps", and a yes/no on framework — with the escalation rule if the answer is "yes, framework."
3. **New block vs new asset?** One choice, with a size rationale and the delivery mechanism (`CopyEmbeddedAsset` + `ForgeOptions.ScriptName` vs a new `SpaDelivery.ScriptName`-style constant).
4. **How does 20.3 consume Epic 19 edges** given 19.1/19.2 are not yet `done`? (Sequencing caveat below — the spike must state the dependency and a fallback if 19.x slips.)
5. **What parity coverage** does 20.2/20.3 owe (`RenderParity` cases), and what are the explicit webview/CLI non-goals?

### Epic 19 dependency caveat (must be stated in findings)

Epic 20's related-work pane (20.3) consumes **Epic 19's directed edges** as its relationship source, and the SCP explicitly sequences **19.1 (work-graph model spike) before 20.3**. As of this story, `19-1-*` and `19-2-*` are `ready-for-dev`, **not `done`**. The spike must:
- Treat the Epic 19 edge vocabulary (`stemmed-from`, `resolves`, `covers`, `cites`, `raised-in`) as the **planned** contract (see `19-1-work-graph-model-and-coverage-spike.md`), not shipped code.
- Recommend a build sequence: 20.1 (this) → 20.2 (zoom, needs only geometry, **not** blocked on Epic 19) → 20.3 (pane, **blocked on** Epic 19 edges landing).
- Name a fallback for 20.3 if Epic 19 slips (e.g. hydrate the pane from the existing `FollowUpDeferredSlot` / citation reverse maps as a reduced edge set), or state that 20.3 simply waits on Epic 19.

### Deliberate non-goals (seed list — spike may extend with rationale)

- **Building the zoomable chart, the side pane, or the payload emitter** — Stories 20.2 / 20.3.
- **Introducing a framework or a build step** without an ADR — that is an architectural fork; escalate, don't decide in a spike ([[adr-creation-trigger-gap-epic-10-retro]]).
- **A second geometry** — no re-derivation of ring weights/membership outside `FollowUpGeometry`/`Charts.Sunburst`.
- **A second count ledger** — no re-counting open items against `ProjectCounts`.
- **A new authoring schema** — no YAML fields, frontmatter keys, or graph DSL for the payload (Epic 9/19 principle; AC #2).
- **Retiring Story 10.7** — the static sunburst stays the no-JS baseline this enhances.
- **A parallel navigation scheme** — the explorer's terminal opens reuse the Story 9.13 destination contract, not new targets.
- **Webview/CLI explorer support** — HTML+SPA only unless the spike records a reason (mirror 19.1's surface-reach honesty).
- **Client-side data fetching** — no XHR/fetch; the payload is delivered at generation time (static-host / `file://`-safe).

### Surfaces / process note (Epic 6 Action #3)

Epic 20 introduces a **net-new client-interactive surface class**. This spike is boundary/payload/degrade scoping, not a full cross-surface integration spike — but AC #2's recommendation **must** state expected surface reach for 20.2/20.3:

| Surface | Expectation for 20.2/20.3 (confirm or revise in findings) |
|---------|-----------------------------------------------------------|
| HTML | Primary host for the explorer (enhances the existing sunburst host page). |
| SPA | Parity via shared body / `RenderParity` — the payload island must survive SPA consolidation. |
| Webview | Dashboard/epics only today — explorer likely **HTML+SPA only** unless the spike finds a reason to promote it. |
| CLI | Notices only; no explorer render. |

If a true surface-coverage gate is needed before 20.2/20.3, say so explicitly in Completion Notes (do not silently expand 20.1 into building webview support).

## Tasks / Subtasks

- [ ] **Task 1 — Trace the geometry + destination seams the payload projects from (AC: #1)**
  - [ ] Read `Charts.Sunburst` / `EpicSunburst` / `SunburstCompanionList` and `FollowUpGeometry` / `UnplannedWorkGeometry`; document the exact API the payload's node hierarchy + weights derive from. Confirm **no second geometry** is needed.
  - [ ] Read `FollowUpGroupTemplater` + the Story 9.13 destination contract; record the precise leaf-vs-group open rule the explorer's terminal action must honor.
  - [ ] Confirm the payload can carry existing canonical node ids (`EpicInfo.Number`, `StoryInfo.Id`, follow-up slug, code path) without a new identity scheme, and does **not** touch `ProjectCounts`.

- [ ] **Task 2 — Define the single generation-time payload shape (AC: #1)**
  - [ ] Specify `{ nodes, edges }` (or the chosen shape): fields, ids, how ring hierarchy + related adjacency are expressed, and how it maps to the rendered SVG wedges.
  - [ ] Decide delivery: inline JSON island vs sidecar `.json`; justify against the SPA precedent and static-host/`file://` safety.
  - [ ] Affirm **no new authoring schema** and **no second count ledger**; list which existing parsers/geometry the emitter (in 20.2/20.3) must call, not fork.

- [ ] **Task 3 — Name the JS / dependency / framework budget (AC: #1)**
  - [ ] Decide **new block in `specscribe.js` vs new embedded asset**; justify with a size estimate against the codemap block's footprint and the `specscribe-spa.js` precedent.
  - [ ] State a concrete **added-KB ceiling** and **zero-runtime-dependency, no-build-step** stance; give a yes/no on **framework** (recommend no; if yes → ADR escalation, not a silent spike decision).
  - [ ] Confirm the delivery mechanism (`CopyEmbeddedAsset` + `ForgeOptions.ScriptName` for a shared block, or a new `SpaDelivery.ScriptName`-style constant + guarded copy for a separate asset).

- [ ] **Task 4 — Document the degrade + parity contract (AC: #2)**
  - [ ] Write the JS-off / reduced-motion / keyboard-AT behaviors, each mapped to the existing pattern it mirrors (table above).
  - [ ] Name the **HTML/SPA parity rules** for the new payload island (which `RenderParity` cases 20.2/20.3 owe), and the explicit webview/CLI non-goals.
  - [ ] Confirm the interactive layer enhances the **exact** Story 10.7 sunburst + Story 9.13 linked-page markup — not a parallel render.

- [ ] **Task 5 — Resolve the Epic 19 dependency + recommend build sequence (AC: #1/#2)**
  - [ ] State the 20.3-needs-Epic-19-edges dependency and the SCP's 19.1-before-20.3 sequencing.
  - [ ] Recommend the concrete order (20.2 unblocked by geometry alone; 20.3 gated on Epic 19) and a fallback edge set for 20.3 if Epic 19 slips.
  - [ ] Cross-reference `19-1-work-graph-model-and-coverage-spike.md`'s edge vocabulary as the planned (not shipped) contract.

- [ ] **Task 6 — Record findings; no production code (AC: #1, #2)**
  - [ ] Write the payload contract + budget + degrade/parity contract + sequencing into this story's **Completion Notes** (same convention as Story 8.1 / 19.1).
  - [ ] Do **not** land production `src/**` / `tests/**` changes from this story. Throwaway notes/fixtures under `_bmad-output/` are OK; Completion Notes are the canonical deliverable.
  - [ ] Escalate via `correct-course` if the spike concludes a framework/build step is warranted (ADR-triggering fork). FR38 PRD sync remains "when convenient" — not a blocker for this spike.

### Review Findings

_(populated during code-review)_

## Dev Notes

### Spike constraints (load-bearing)

- **Scoping only.** Like Story 8.1 / 19.1: confirm/deny by reading code paths; do not refactor `Charts.*`, `FollowUpGeometry`, or `specscribe.js` "while you're there."
- **The budget is the deliverable.** The SCP's stated fear is JS growing "by accretion." A vague answer fails AC #1 — land a *number* for size, a *yes/no* for framework, and a *named home* for the code.
- **Enhance the exact markup.** The explorer is a progressive enhancement over the existing sunburst SVG + 9.13 destinations — not a parallel site, not a second authoring schema.
- **Single ledger / single geometry.** 20.2/20.3 will be judged against "does not invent a second geometry or re-count against `ProjectCounts`" — the payload contract must forbid both.
- **NFR8 / NFR5.** JS-off gets the full static sunburst + 9.13 pages + a server-rendered Related block. The client never fetches and never owns a destination or a count.
- **Framework = ADR, not spike.** If the recommendation is "introduce a framework/build step," that is an architectural fork; escalate via correct-course ([[adr-creation-trigger-gap-epic-10-retro]]) rather than baking it into a spike's Completion Notes.

### Architecture compliance

- Shared-core projection: any future payload emitter is a **pure projection** over existing models (`FollowUpGeometry`, `UnplannedWorkGeometry`, Epic 19 edges) — not a per-surface re-parse. [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1/AD-2]
- Insight/interactive surfaces are additive and non-blocking (AD-4). A missing or malformed payload must never fail generation; degrade to the static sunburst.
- Graceful degradation for JS-off / reduced-motion / AT is an inherited invariant, not an add-on.

### Suggested Completion-Notes shape

Use tables, not prose walls (mirror 19.1):

1. **Payload** — field | source API | reuses geometry? | notes
2. **Budget** — dimension (size / deps / framework / home) | decision | rationale
3. **Degrade** — mode | required behavior | mirrored pattern
4. **Parity** — surface | expectation | `RenderParity` owed by 20.2/20.3
5. **Sequencing** — story | depends on | fallback if dependency slips
6. **Non-goals confirmed** — item | rationale

### Known seam caveats (spike must classify, not "fix")

- **"Pure SVG, no JS" is aspirational.** `specscribe.js` already houses substantial interactivity (codemap zoom, list sort/filter, risk pager). Name the explorer's place on that spectrum honestly rather than pretending it is the first JS.
- **Epic 19 not yet done.** The related-edge half of the payload leans on a contract that ships in Epic 19; the spike must not assume shipped edges.
- **Story 10.7 in active dev.** The static sunburst markup the explorer enhances is itself moving (density/collapse work — see [[story-10-7-sunburst-navigability-project-scale-review]]); the enhancement contract must key off stable seams (`.sb-seg`, wedge links) not in-flight details.
- **Charts render pure SVG + links today** ([[charting-is-pure-svg-no-js]]) — the drill-in is the first chart that *needs* JS to function beyond tooltips; that is the line being crossed, and the spike names the terms.
- **A second sunburst family has now asked for the same interaction.** Owner feedback logged 2026-07-22 during Story 7.11's (Code Ownership & Bus-Factor Insights) design-review session: "click and drill into a directory and filter down to that level — at least in the sunburst. You can do this via Plotly and it's amazing." That's against `git-insights.html`'s Code Ownership sunburst (Epic 7's code-structure/git-analytics family, not this epic's epic/story/follow-up remaining-work sunburst) — explicitly NOT actioned as part of 7.11, deferred and cross-referenced back to this epic (also noted in `epics.md`'s Epic 20 section). The spike should fold in two questions this raises: whether the interactivity boundary/JS budget this story names is meant to generalize across BOTH sunburst families (this epic's + Epic 7's) or whether Epic 7's family gets its own follow-on story instead of piggybacking on Epic 20's budget; and that the owner named **Plotly** specifically — a real charting-library dependency, a materially bigger departure from the zero-runtime-dependency default (Dev Notes table above, and ADR 0010's existing zero-dep JS posture for Epic 7's own opt-in analytics surfaces) than anything else considered here, so it should be weighed explicitly rather than assumed in or out.

### Anti-patterns to prevent

- Reimplementing `FollowUpGeometry` / `Charts.Sunburst` weights as a second "explorer geometry."
- Re-counting open items against `ProjectCounts` in the payload or the pane.
- Introducing a framework, bundler, or build step by default (accretion the SCP warns against) — that is an ADR-triggering fork.
- Client-side `fetch`/XHR for the payload (breaks `file://` / static-host delivery).
- A parallel navigation scheme instead of the Story 9.13 leaf/group destination contract.
- Expanding 20.1 into building `Charts.*` SVG, the payload emitter, or the side pane.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/20-1-interactive-explorer-architecture-spike.md`
- Sprint key: `20-1-interactive-explorer-architecture-spike`
- Downstream story keys (not created by this spike): `20-2-zoomable-drill-in-sunburst-navigation`, `20-3-related-work-side-pane-on-selection`
- No `src/` touches expected for 20.1
- Client assets live at `src/SpecScribe/assets/specscribe.js` (+ `.css`), embedded resources copied via `CopyEmbeddedAsset` in `SiteGenerator.cs`; the SPA's second asset is `src/SpecScribe/assets/specscribe-spa.js`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` — Epic 20 + Story 20.1 / 20.2 / 20.3 ACs (lines ~3034–3097)]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md` — Epic 20 seating, owner-approved first-client-JS decision, JS budget rationale, 19.1-before-20.3 sequencing]
- [Source: `src/SpecScribe/Charts.cs` — `Sunburst`, `EpicSunburst`, `SunburstCompanionList`; pure-SVG two-level geometry]
- [Source: `src/SpecScribe/FollowUpGeometry.cs`, `UnplannedWorkGeometry.cs` — ring weights + membership (single geometry source)]
- [Source: `src/SpecScribe/assets/specscribe.js` — the sanctioned progressive-enhancement layer (~1058 lines); codemap zoom + `motionFastMs()` are the closest interactivity precedent]
- [Source: `src/SpecScribe/SiteGenerator.cs` ~3731 (`CopyEmbeddedAsset` for specscribe.js/css) and ~2722 (`specscribe-spa.js` second-asset precedent)]
- [Source: `src/SpecScribe/JsonSpaRenderAdapter.cs`, `SpaBundle.cs`, `SpaDelivery.cs`, `RenderParity.cs`, `IRenderAdapter.cs` — SPA delivery + parity harness (Story 6.7)]
- [Source: `_bmad-output/implementation-artifacts/9-13-generated-filtered-follow-up-group-pages-and-sunburst-click-destinations.md` — leaf/group click-destination contract the explorer must honor]
- [Source: `_bmad-output/implementation-artifacts/19-1-work-graph-model-and-coverage-spike.md` — planned work-graph edge vocabulary for the 20.3 pane]
- [Source: `_bmad-output/implementation-artifacts/8-1-integration-spike-cross-surface-status-verification.md` — spike deliverable convention (findings in Completion Notes)]
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` — shared core, graceful degrade, insight/interactive providers]
- [Source: epics.md NFR8 (degrade to absent/static) + NFR5 (progressive enhancement); NFR8 named in Epic 20 header]
- [Source: `_bmad-output/implementation-artifacts/7-11-code-ownership-and-bus-factor-insights.md` — Change Log / Dev Agent Record, 2026-07-22 entries: the owner's click-to-drill/filter-by-directory request against the Code Ownership sunburst, explicitly deferred to this epic, Plotly named as the desired interaction model]

### Previous story intelligence

- **Story 19.1 (`ready-for-dev`):** Sibling spike from the same SCP — its edge vocabulary is Epic 20's pane data source. Its "findings in Completion Notes, no ADR unless a fork, name absence/NFR8 rules" convention is the template for 20.1.
- **Story 6.7 (`done`, SPA adapter):** Established a second embedded JS asset + a body-capture parity harness — the direct precedent for both the "new asset" delivery option and the HTML/SPA parity rules AC #2 requires. See [[story-6-7-spa-adapter-live]].
- **Story 7.6 codemap (`done`):** The zoom + breadcrumb + reduced-motion `setViewBox` + directory drill in `specscribe.js` is the **closest existing interactivity to a drill-in sunburst** — 20.2 should study it as the pattern to generalize, not a thing to fork.
- **Story 9.13 (`done`):** Locked the leaf/group click-destination contract; Epic 20 must "keep this destination contract — do not invent a parallel scheme."
- **Story 10.7 (in active dev):** The static baseline; its density/collapse work means the enhancement must key off stable wedge seams. See [[story-10-7-sunburst-navigability-project-scale-review]].
- **Charts-are-pure-SVG value ([[charting-is-pure-svg-no-js]]):** The deliberate divergence being budgeted here — the spike names the crossing rather than letting it happen by accretion.

### Git intelligence summary

Recent commits (as of create-story) landed Epic 7 code-insight work (7.9 colorize, 7.10 risk quadrant, 7.11 ownership) and their client-side pagers/recolor blocks in `specscribe.js` — evidence the progressive-enhancement layer is actively growing block-by-block, which is exactly why the SCP wants a *named budget* before the explorer adds the largest block yet. No explorer/drill-in code exists; the spike starts from the static sunburst + a rich `specscribe.js` precedent set.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-21 — Story 20.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: interactivity-boundary + single-payload contract + JS/dependency/framework budget + degrade-to-static & HTML/SPA parity contract + Epic 19 sequencing; no production code. Epic 20 → in-progress (first story).
