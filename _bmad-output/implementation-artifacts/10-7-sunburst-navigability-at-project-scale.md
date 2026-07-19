---
baseline_commit: 33c89ea4bbc2f3d2e42f7115a47254ce78e2317a
---

# Story 10.7: Sunburst Navigability at Project Scale

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer scanning remaining work on a large project,
I want the project and epic sunbursts to stay readable and drillable when dozens of stories and follow-ups share a ring,
so that wedge density never becomes a wall of unreadable slices and I can still reach the item I care about.

## Context & Why This Story Exists

Epic 9 made remaining-work geometry trustworthy (9.7 hierarchy → 9.12 Unplanned → **9.13 filtered group pages**). Project glance already **aggregates** follow-ups into outer open/done wedges that land on `follow-ups/group-*.html`. What it did **not** solve:

| Pain | Where | Today |
|------|-------|-------|
| **Tiny story wedges** | `Charts.Sunburst` middle ring | One `<a>` per story under every epic — dozens of peers → unhittable slices |
| **Opaque orange band** | `Charts.EpicSunburst` middle ring | Every epic-level action / deferred / attributed QD is still a **leaf** peer of stories — a large follow-up set paints a dashed-orange wall |
| **No always-visible twin** | Glance panels | Legend + hint exist; there is no scannable list twin beside the chart for keyboard / low-vision users when wedges shrink |

**9.13 already prepared the destination:** `follow-ups/group-epic-{n}.html` exists for epic follow-up aggregates. 9.13 explicitly deferred density/drill to **this story** and forbade a parallel click scheme.

**This story owns readability at scale.** It does **not** own click-destination invention, chart metadata (10.2), insight-context polish (10.6), or TaskSunburst.

Serves Journey 6 (health / remaining work) and Epic 10's "legible for every audience" mission. Load-bearing: **NFR8** (absent, not broken) + **9.13 contract**.

### What the code does today (read before designing)

**Project glance — `Charts.Sunburst`** ([`Charts.cs`](src/SpecScribe/Charts.cs) ~158–309):

1. **Inner:** epics → `epics/epic-{n}.html`; Follow-ups / Unplanned roots → group pages when non-empty  
2. **Middle:** **per-story** wedges (`AppendWeightedStorySlot`, `nestStoryChildren: false`) — density pain lives here  
3. **Outer:** open/done **aggregates** via `AppendOpenDoneAggregateRing` → `FollowUpGroupPages.EpicPath(n)` / Follow-ups / Unplanned group hrefs  

Epic angular weight already includes epic-level peers (`EpicWeight` = stories + actions + epic-level deferred + attributed QD) — [spec-9-13-deferred-glance-weight-noplan-sourcekey](_bmad-output/implementation-artifacts/spec-9-13-deferred-glance-weight-noplan-sourcekey.md). Soft-cap was **Ask First**; this story leaves weight **unbounded** (honesty > clamp).

**Epic page — `Charts.EpicSunburst`** (~579–703):

- Stories as individual wedges; story-child deferred nested on outer under parent story  
- Epic-level actions / deferred / QD still **`AppendActionItemSlot` / `AppendDeferredItemSlot` / `AppendQuickDevSlot`** on the **same** middle ring → opaque orange band at scale  

**Call sites:** [`HtmlRenderAdapter.Dashboard.cs`](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs) (home); [`HtmlRenderAdapter.Epics.cs`](src/SpecScribe/HtmlRenderAdapter.Epics.cs) (epics index glance + epic page). Pure SVG `<a href>` — **no JS drill / hash** (Story 1.4 deliberate divergence from UX-DR5/6). Bound tooltips in `specscribe.js` stay as-is.

## Acceptance Criteria

**AC1 (Project glance stays navigable at story-ring density; 9.13 destinations preserved)**  
**Given** a project sunburst whose story ring has enough peers that individual wedges become hard to hit or read  
**When** it renders  
**Then** the chart offers a clear navigability path — progressive page drill (epic → epic page), per-epic middle-ring density collapse, a companion scannable list twin, and keyboard-surviving legend emphasis — rather than relying on ever-tinier SVG wedges alone  
**And** leaf and group click destinations remain the Story 9.13 contract (detail page vs generated filtered group page) — this story does not invent a parallel navigation scheme.

**AC2 (Epic sunburst: follow-ups reachable without opaque orange band; NFR8)**  
**Given** an epic-scoped sunburst with a large attributed follow-up set  
**When** a maintainer opens that epic  
**Then** follow-ups remain attributable and reachable without collapsing into an opaque orange band  
**And** the solution degrades cleanly when follow-ups are absent (NFR8) and does not invent a new authoring schema.

## Design Direction — OWNER-LOCKED (2026-07-18)

**Polish bar:** Journey 6 remaining-work hero — density fixes must feel intentional (clear silhouette, honest counts, same destinations), not a bolted-on “More…” dump.

### Locked membership + click destinations (parity rules)

| Wedge | Destination after 10.7 | Notes |
|-------|------------------------|-------|
| Epic inner (glance) | `epics/epic-{n}.html` | Unchanged |
| Story leaf (glance, sparse mode) | story / placeholder page | Unchanged |
| Story **summary** middle (dense mode) | `epics/epic-{n}.html` | **Same as epic inner** — not a new scheme |
| Outer open/done (glance) | `group-epic-{n}` / Follow-ups / Unplanned group pages | Unchanged 9.13 |
| Story leaf (epic chart) | story page | Unchanged |
| Story-child deferred (epic outer) | detail `follow-ups/{slug}.html` | Unchanged leaves |
| Epic-level follow-up **aggregates** (epic chart) | `follow-ups/group-epic-{n}.html` | **Uses pages 9.13 already emits** |
| Follow-ups / Unplanned roots | existing group pages | Unchanged |

**Membership sets stay ledger-agreed** with `FollowUpGeometry` / `UnplannedWorkGeometry` / `FollowUpGroupPages.Enumerate`. Do not invent a second deferred parse or new authoring fields.

### AC1 — OWNER-LOCKED compose: density collapse + page drill + companion list

**Do NOT** implement UX-DR5/6 JS progressive zoom + URL-hash drill. Progressive drill = **existing page navigation** (inner epic → epic page → story / group pages).

#### 1) Per-epic middle-ring density collapse

Constant (name it; keep in one place near `Sunburst`):

```text
StoryDensityCollapseThreshold = 8   // stories under a single epic
```

| Mode | When | Middle ring under that epic |
|------|------|-----------------------------|
| **Sparse** | `epic.Stories.Count < 8` | Current per-story wedges (`AppendWeightedStorySlot`, `nestStoryChildren: false`) |
| **Dense** | `epic.Stories.Count >= 8` | **One** summary wedge spanning the epic’s story-weight sweep: aria/title like `Epic N: K stories (sized by tasks[+ nested deferred])` → href `epics/epic-{n}.html` |

- Outer open/done aggregates **always** still draw for that epic’s follow-up counts (unchanged).  
- Mix of sparse + dense epics on one chart is allowed.  
- Update `BuildSunburstHint` so dense mode is explained in one short clause (no jargon).  
- Distinct class for the summary wedge (e.g. `sb-story-summary`) — text + class, never color-only; reuse existing status/epic tokens where possible; **no new `--status-*`**.

#### 2) Companion scannable list (project glance only)

Under the glance sunburst panel (home + epics-index), after SVG + legend + hint, render a compact list twin:

- Heading e.g. “Remaining work by epic” (or equivalent framework-neutral copy)  
- One row per epic (and Follow-ups / Unplanned roots when present): link to the **same** destinations as the chart (epic page / group page)  
- Counts: stories + open follow-ups (reuse `CountEpicFollowUpAggregates` / existing orphan + unplanned counts — do not recount ledgers)  
- Pure HTML list — keyboard-reachable anchors; NFR8 omit empty synthetic roots  
- Shared helper preferred (Charts or small pure helper) so Dashboard + Epics index stay identical  

This is the a11y / scannable path when wedges shrink — **not** a second nav IA.

#### 3) Keyboard / focus emphasis

Preserve existing legend `:has(.sb-*-item:hover/:focus-visible)` CSS that dims non-matching `.sb-seg`. Ensure dense-mode summary wedges participate if they use a new class. Do **not** add a JS roving-tabindex sunburst engine (webview CSP; charts stay pure SVG/CSS + real links).

### AC2 — OWNER-LOCKED: aggregate epic-level peers (mirror glance)

On **`EpicSunburst`**, stop painting epic-level actions / epic-level deferred / attributed QD as individual middle-ring leaves.

| Ring | Content after 10.7 |
|------|--------------------|
| **Inner (stories)** | Unchanged per-story wedges + outer nested story-child deferred leaves |
| **Outer aggregate** | Open vs done counts for **epic-level peers only** (actions + epic-level deferred + attributed QD — **not** double-counting story-child deferred already nested) → href `geometry.LinkPrefix + FollowUpGroupPages.EpicPath(epic.Number)` via `AppendOpenDoneAggregateRing` |

- When peer open+done == 0 → omit aggregate ring entirely (**NFR8**).  
- Update epic hint copy: stories (+ nested deferred) · outer open/done follow-ups (aggregated) → group page.  
- Optional one-line link under the epic chart: “All follow-ups for this epic” → same group page when non-empty (nice; not a substitute for the aggregate wedges).  
- **Do not** rewire story-child deferred into the aggregate (they stay under their story).  
- **Do not** change leaf detail destinations for items that remain leaves.

**Critical count helper split (do not conflate):**

| Helper | Includes | Use for |
|--------|----------|---------|
| `CountEpicFollowUpAggregates` (existing) | actions + **all** deferred for epic (incl. story-child) + attributed QD | Project-glance **outer** ring only |
| **New** (or inline) epic-level peer open/done | actions + `EpicLevelDeferred` + `unplanned.ForEpic` — **exclude** `StoryChildDeferred` | `EpicSunburst` aggregate ring |

If the epic chart reused `CountEpicFollowUpAggregates`, story-child items would appear both as nested leaves **and** inside the aggregate — dishonest double-count.

### Soft-cap decision (resolves Ask First)

**Leave `EpicWeight` / peer angular inflation unbounded.** Density is solved by collapsing story middles (AC1) and aggregating epic peers (AC2), not by clamping honest remaining-work weight.

### Shine / audience bar

1. Teach without jargon — hint + companion list readable by a non-BMAD stakeholder.  
2. Look finished — dense summary wedge + aggregate ring + list twin compose with existing sunburst chrome.  
3. Stay honest — never drop membership; never invent destinations; never color-only.  
4. Reach every glance surface (home + epics index) and epic pages; webview + SPA via existing `WriteOutput` / adapter seams.

## Tasks / Subtasks

- [x] **Task 1 — Density collapse on project glance** (AC: 1)
  - [x] Add `StoryDensityCollapseThreshold = 8` and per-epic sparse/dense branch in `Charts.Sunburst`.
  - [x] Dense: single summary middle wedge → `epics/epic-{n}.html`; preserve outer aggregates + `EpicWeight`.
  - [x] Update hint + legend participation for `sb-story-summary` (or chosen class).
  - [x] Unit tests: epic with 8+ stories → one summary href to epic page, no per-story middle hrefs under that epic; epic with 7 → per-story unchanged.

- [x] **Task 2 — Companion scannable list on glance panels** (AC: 1)
  - [x] Pure helper rendering epic (+ Follow-ups / Unplanned) rows with counts and 9.13 destinations.
  - [x] Wire under sunburst in `HtmlRenderAdapter.Dashboard` + epics-index glance (same markup).
  - [x] NFR8: omit empty synthetic roots; never link group rows to unfiltered whole-site dumps.

- [x] **Task 3 — EpicSunburst aggregate epic-level peers** (AC: 2)
  - [x] Remove per-item middle slots for actions / epic-level deferred / attributed QD.
  - [x] Count open/done from epic-level peers only (exclude `StoryChildDeferred`); draw aggregate → `FollowUpGroupPages.EpicPath(n)`; omit when empty.
  - [x] Keep story wedges + nested story-child deferred leaves.
  - [x] Update hint; optional “All follow-ups…” link.
  - [x] Tests: many peers → aggregate hrefs to `group-epic-N`, no `action-`/`deferred-` leaf hrefs for epic-level peers; zero peers → no aggregate; story-child deferred still detail-linked; assert aggregate count ≠ glance `CountEpicFollowUpAggregates` when story-child deferred exist.

- [x] **Task 4 — Guardrails** (AC: 1, 2)
  - [x] No JS hash drill; no `?filter=` / `#group=` on full lists; no new authoring schema; no new `--status-*`.
  - [x] Do not absorb 10.2 ChartMeta, 10.6 polish, or TaskSunburst redesign.
  - [x] Membership parity with `FollowUpGroupPages` / sprint Unplanned set unchanged.

- [x] **Task 5 — Tests + golden** (AC: 1, 2)
  - [x] Extend `ChartsTests` (sparse/dense matrix, epic aggregate matrix, destination matrix).
  - [x] Extend `FollowUpSurfacesTests` / adapter tests if companion markup appears in GenerateAll HTML.
  - [x] Golden fingerprint moves → regen per `golden-diff-normalization-gotchas`; RenderParity + SPA/webview green.
  - [x] `dotnet test` from repo root.

- [x] **Task 6 — Verify end-to-end** (AC: 1, 2)
  - [x] Large-fixture or synthetic model with ≥8 stories under one epic + many epic-level follow-ups: wedges hittable, companion list works, group pages open from aggregates.
  - [x] Empty follow-ups / small projects: no empty rings, no dead companion rows (NFR8).

## Dev Notes

### Architecture patterns & constraints (must follow)

- **Pure SVG/CSS charts** — no info-bearing JS; real `<a href>` navigation only (webview CSP).  
- **9.13 click contract is sacred** — group → `follow-ups/group-*.html`; leaf → detail/story/spec; epic arc → epic page.  
- **NFR8** — omit empty aggregates / empty companion roots; degrade absent, not broken.  
- **Never color-only** (UX-DR17) — dashed classes + legend text + aria/`<title>`.  
- **No new authoring schema**; StatusStyles / existing `--status-*` only.  
- Charts stay a **pure renderer** — hrefs from geometry / `FollowUpGroupPages` helpers.  
- Accessibility research note: at density, prefer **coarser marks + companion list** over hundreds of focusable wedges (aligns with locked silhouette; do not add roving-tabindex JS).

### Source tree — files to touch

| File | Change |
|------|--------|
| `src/SpecScribe/Charts.cs` | Density collapse in `Sunburst`; aggregate peers in `EpicSunburst`; hint/legend helpers; optional companion markup helper (**UPDATE**) |
| `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` | Emit companion list under glance (**UPDATE**) |
| `src/SpecScribe/HtmlRenderAdapter.Epics.cs` | Companion on index glance; epic panel optional follow-ups link (**UPDATE**) |
| `src/SpecScribe/assets/specscribe.css` | Summary-wedge + companion-list chrome only (**UPDATE**) |
| `tests/SpecScribe.Tests/ChartsTests.cs` | Sparse/dense + epic aggregate destination matrix |
| `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` (and/or adapter tests) | Companion + GenerateAll href sanity |
| Golden / parity suites | Regen if fingerprints move |

Reuse as-is (do **not** reinvent): `FollowUpGroupPages`, `AppendOpenDoneAggregateRing`, `CountEpicFollowUpAggregates`, `FollowUpRow` (group pages already use it — companion list is lighter counts, not a full row dump unless you deliberately reuse rows).

### UPDATE files — current state / change / preserve

**`Charts.Sunburst`**
- *Today:* per-story middle; outer open/done aggregates; peer-inflated `EpicWeight`.  
- *Change:* per-epic dense summary middle when `Stories.Count >= 8`; companion list emission (or return string append).  
- *Preserve:* inner epic hrefs; outer group hrefs; Follow-ups / Unplanned roots; pad/inset geometry; NFR8 empty omit; no task fringe.

**`Charts.EpicSunburst`**
- *Today:* epic-level peers as middle leaves.  
- *Change:* peers → open/done aggregate → `group-epic-{n}`.  
- *Preserve:* story wedges; nested story-child deferred leaves; empty → `chart-empty`; no project Unplanned root.

**`HtmlRenderAdapter.Dashboard` / `.Epics`**
- *Today:* panel wraps `Charts.Sunburst` / `EpicSunburst`.  
- *Change:* append companion list on glance; optional epic follow-ups link.  
- *Preserve:* headings, panel structure, parity with webview/SPA capture.

**`specscribe.css`**
- *Change:* minimal classes for summary wedge + companion list.  
- *Preserve:* existing `.sb-seg` hover/focus, legend `:has()` rules, follow-up/unplanned treatments. Do not refactor the whole stylesheet (17.5).

### Out of scope (do not build)

| Item | Owner |
|------|-------|
| JS sunburst drill + URL hash (UX-DR5/6) | Deferred / not this story |
| Hash/query filters on full action/deferred lists | Forbidden (9.13) |
| New authoring schema / `--status-*` token | Forbidden |
| Chart metadata frame / real-value legend | 10.2 |
| Coupling / heatmap / sole-contributor polish | 10.6 |
| TaskSunburst redesign | Hierarchy spec / out |
| Soft-cap clamp on `EpicWeight` | Explicitly not chosen |
| Changing Unplanned ↔ sprint membership | 9.12 frozen |
| Nav IA / Structure retirement | 10.1 |

### Previous story intelligence

**From 9.13 (done):** Generated `group-*` pages; Charts pure renderer; epic group pages exist **for this story’s aggregate wedges**; “10.7 keeps this click-destination contract.” Review accepted glance hierarchy aggregates as intentional.

**From 10.6 (ready-for-dev):** Owner-lock silhouettes at create-story; pure SVG/CSS; NFR8; do not absorb sunburst density; shine bar (teach / finished / honest / every surface).

**From hierarchy + glance-weight specs:** No task fringe; story weight includes nested deferred; glance `EpicWeight` includes peers; soft-cap Ask First → **resolved here as leave unbounded**.

**Git recent:** `1210a41` glance weight / no-plan / source-key; `4224712` nested deferred story weight — extend those seams, don’t rewrite.

### Latest tech notes (accessibility)

Dense charts should expose **coarser focusable marks** and pair with a **list/table twin** rather than forcing Tab through every micro-wedge. SpecScribe already uses real `<a>` segments + legend focus; this story’s density collapse + companion list matches that guidance without adding a JS keyboard widget.

### Project context reference

Follow `_bmad-output/project-context.md` when populated; until then, follow patterns already established in Charts / FollowUpGeometry / Epic 9 stories.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.7]
- [Source: _bmad-output/implementation-artifacts/9-13-generated-filtered-follow-up-group-pages-and-sunburst-click-destinations.md]
- [Source: _bmad-output/implementation-artifacts/spec-sunburst-remaining-work-hierarchy.md]
- [Source: _bmad-output/implementation-artifacts/spec-9-13-deferred-glance-weight-noplan-sourcekey.md]
- [Source: _bmad-output/implementation-artifacts/10-6-insight-chart-context-polish.md]
- [Source: _bmad-output/implementation-artifacts/epic-9-retro-2026-07-18.md]
- [Source: src/SpecScribe/Charts.cs#Sunburst] / `#EpicSunburst` / `#AppendOpenDoneAggregateRing`
- [Source: src/SpecScribe/FollowUpGroupPages.cs]
- [Source: UX-DR5/6/7 context — links-based reality from Story 1.4; NFR8; UX-DR17/18]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed — comprehensive developer guide created. Owner-locked 2026-07-18: per-epic density collapse at 8 stories; companion glance list; EpicSunburst peer aggregates → `group-epic-N`; preserve 9.13 destinations; no JS hash drill; EpicWeight unbounded.

Implemented 2026-07-19. AC1: `Charts.StoryDensityCollapseThreshold = 8` gates a new per-epic sparse/dense branch in `Charts.Sunburst` — epics at/above the threshold render one `AppendStorySummarySlot` wedge (class `sb-story-summary sb-{epicClass}`, reusing the epic's own status fill — no new `--status-*` token — plus a distinguishing hatch stroke) spanning the same sweep the per-story wedges would have occupied, linking to `epics/epic-{n}.html` (identical to the epic's own inner-ring destination, not a new scheme); epics below the threshold are untouched. `BuildSunburstHint` gained a `hasDenseEpics` clause explaining the collapse in one sentence. A new `Charts.SunburstCompanionList` helper renders a plain, keyboard-reachable `<ul>` — one row per epic (story count + open follow-ups, reusing `CountEpicFollowUpAggregates`) plus Follow-ups/Unplanned rows only when non-empty (NFR8) — called identically from `HtmlRenderAdapter.Dashboard` and `HtmlRenderAdapter.Epics` (epics-index glance) so both surfaces render byte-identical markup by construction, not by convention.

AC2: `Charts.EpicSunburst` no longer emits per-item middle wedges for epic-level actions/deferred/quick-dev — `AppendActionItemSlot`/`AppendQuickDevSlot` were deleted (their only call sites) — the peer count now draws ONE open/done aggregate wedge via the existing `AppendOpenDoneAggregateRing` helper, on a new outermost ring (`peerAggInner`/`peerAggOuter`, beyond the existing story-child-deferred ring so the two never overlap) linking to `geometry.LinkPrefix + FollowUpGroupPages.EpicPath(epic.Number)` — the same `group-epic-N` page 9.13 already emits. The open/done split deliberately excludes `StoryChildDeferred` (those stay nested leaves under their parent story, unchanged) — a dedicated test proves the epic-chart aggregate count differs from the project-glance's `CountEpicFollowUpAggregates`, which *does* include story-child items, confirming the two counts are intentionally different. The aggregate omits entirely when there are zero epic-level peers (NFR8). Hint/legend updated to describe the aggregate instead of the removed per-item peer classes.

Verified end-to-end via `dotnet run --project src/SpecScribe -- generate --deep-git` against this repo's own history (which already has 5 epics at 8+ stories — 6, 7, 8, 9, 16 — a real large-fixture exercise, not a synthetic one): the dense epics render exactly one `sb-story-summary` wedge each with the expected aria-label (e.g. "Epic 9: 13 stories (sized by tasks + nested deferred)"), sparse epics (e.g. Epic 1, 5 stories) still render per-story wedges unchanged (mixed sparse+dense on one chart confirmed), the companion list renders "Remaining work by epic" with per-epic counts plus an "Unplanned: 53 open items" row, and epic-9's own chart draws a single "Epic 9: 3 open follow-ups" aggregate linking to `../follow-ups/group-epic-9.html` (which exists and is populated) with zero remaining `Action item:` leaf text.

Two pre-existing tests asserted the now-removed per-item EpicSunburst behavior and were updated to assert the aggregate instead: `EpicSunburst_FollowUps_AreStoryRingPeers_FilteredToEpic` (renamed `EpicSunburst_FollowUps_AreAggregated_FilteredToEpic`) and `FollowUpSurfacesTests.HomeAndEpicSunburst_ShowFollowUpGeometry_WhenOpenItemsExist`. Golden fingerprint regenerated per `golden-diff-normalization-gotchas` (confirmed stable across 3 repeated runs after an explicit rebuild — the first post-change hash was stale, exactly Gotcha 6's known trap). 1655/1655 tests green (12 new: dense/sparse boundary matrix, companion-list matrix incl. empty-project, peer-aggregate omit-when-empty, peer-aggregate-excludes-story-child-differs-from-glance). RenderParity/SPA/webview suites green in the same run — no adapter-specific changes needed since Dashboard/Epics-index route through the shared `HtmlRenderAdapter`. Status → review.

### File List

- `src/SpecScribe/Charts.cs` (UPDATE) — `StoryDensityCollapseThreshold` + per-epic sparse/dense branch + `AppendStorySummarySlot` in `Sunburst`; new `SunburstCompanionList` helper; `BuildSunburstHint` gains `hasDenseEpics`; `EpicSunburst` epic-level peers collapse to one `AppendOpenDoneAggregateRing` wedge (new `peerAggInner`/`peerAggOuter` ring) instead of per-item leaves; deleted now-dead `AppendActionItemSlot`/`AppendQuickDevSlot`.
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` (UPDATE) — emits `Charts.SunburstCompanionList` under the glance sunburst.
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` (UPDATE) — emits `Charts.SunburstCompanionList` under the epics-index glance sunburst (same call, same markup as Dashboard).
- `src/SpecScribe/assets/specscribe.css` (UPDATE) — `.sb-story-summary` (dense-wedge hatch stroke) + `.sunburst-companion-list` (list/heading/link chrome).
- `tests/SpecScribe.Tests/ChartsTests.cs` (UPDATE) — dense/sparse boundary matrix, `SunburstCompanionList` matrix (incl. empty-project), `EpicSunburst` peer-aggregate omit-when-empty + excludes-story-child-differs-from-glance; renamed/rewrote `EpicSunburst_FollowUps_AreStoryRingPeers_FilteredToEpic` → `EpicSunburst_FollowUps_AreAggregated_FilteredToEpic`.
- `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` (UPDATE) — `HomeAndEpicSunburst_ShowFollowUpGeometry_WhenOpenItemsExist` updated for the aggregate destination (story-child deferred assertions unchanged).
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` (UPDATE) — golden content fingerprint regenerated (`96d557ff…`).
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (UPDATE) — status transitions for `10-7-sunburst-navigability-at-project-scale`.

## Change Log

- 2026-07-19: dev-story — implemented both ACs. AC1: `Charts.Sunburst` collapses any epic at/above `StoryDensityCollapseThreshold` (8) stories to one `sb-story-summary` summary wedge → `epics/epic-{n}.html` (same destination as the epic's own inner-ring wedge — no new click scheme), reusing the epic's status fill (no new `--status-*`); epics below the threshold render per-story wedges unchanged, and mixed sparse+dense charts are supported. A new `Charts.SunburstCompanionList` renders a plain keyboard-reachable list (epic rows with story + open-follow-up counts, Follow-ups/Unplanned rows only when non-empty per NFR8) called identically from the Dashboard and Epics-index glance panels. AC2: `Charts.EpicSunburst` collapses epic-level actions/deferred/quick-dev into one open/done aggregate wedge (reusing `AppendOpenDoneAggregateRing` on a new outer ring) linking to the existing `follow-ups/group-epic-N.html` page instead of one leaf wedge per peer — deliberately excluding story-child deferred (which stay nested under their story), proven by a test asserting the epic-chart count differs from the glance's own `CountEpicFollowUpAggregates`. Verified end-to-end via `dotnet run generate --deep-git` against this repo's own history, which already has 5 epics at 8+ stories (6, 7, 8, 9, 16) plus real follow-up data — confirmed dense wedges, sparse wedges, companion list, and the epic-9 aggregate all render and link correctly. Two pre-existing tests asserting the removed per-item EpicSunburst leaves were updated for the new aggregate. Golden fingerprint regenerated (`96d557ff…`, confirmed stable across repeated runs per the known stale-first-hash gotcha). 1655/1655 tests green (12 new). Status → review.
