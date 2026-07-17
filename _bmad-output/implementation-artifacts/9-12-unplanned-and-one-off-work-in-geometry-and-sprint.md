---
baseline_commit: 8cc95353c91ec1b8ee61029a2b021676263dd6f5
---

# Story 9.12: Unplanned and One-Off Work in Geometry and Sprint

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer scanning remaining work,
I want quick-dev / one-shot specs and other unattributable one-offs to appear as first-class unplanned work — both on the project sunburst and on the sprint board —
so that parked direct work is visible beside the epic plan instead of vanishing into an opaque Follow-ups bucket or living only as a Home tile.

## Acceptance Criteria

1.
**Given** open quick-dev (`route: one-shot`) specs and/or deferred items whose provenance cannot resolve to an epic
**When** the project sunburst renders
**Then** those items appear under a dedicated synthetic root slice (e.g. Unplanned / Direct work), separate from epic-attributed stories and from retro action items that do have an epic
**And** when provenance or sprint timing can identify an epic, the item prefers that epic's story ring over the Unplanned root
**And** counts remain ledger-agreed (Story 8.3); NFR8 omits the Unplanned slice when empty.

2.
**Given** the same unplanned / one-off set
**When** the sprint board renders
**Then** those items also appear in an unplanned / one-off lane (or equivalent board grouping) so the sprint view and the sunburst describe the same residual work
**And** no new authoring schema is required — attribution derives from existing provenance, frontmatter, and sprint data.

## Context & Scope

Epic 9 extends FR30 from standalone follow-up pages into the Driver's primary remaining-work surfaces. Story **9.7** put action items + deferred items into the sunburst as story-ring peers (epic-attributed) plus a synthetic **Follow-ups** slice for unattributed orphans. Story **9.11** deep-linked those wedges to per-item detail pages. Quick-dev / one-shot specs (`WorkInventory.QuickDev`, ledger `DirectChanges`) still live only as a Home "Direct changes" tile — invisible in the sunburst and absent from the sprint board. Unattributable deferred items share the opaque Follow-ups orphan bucket with unattributed action items, so direct work and process debt look the same.

**This story makes unplanned / one-off work first-class in both geometry and sprint.** It does **not** build Story 9.13's generated filtered group pages (group-wedge destinations stay page-level / best-effort until 9.13), does **not** absorb 9.6/9.10/9.11 list/detail chrome, and does **not** invent a new authoring schema.

### Owner prefs (locked 2026-07-17 in sprint-status.yaml)

1. **BOTH** a dedicated Unplanned sunburst root **AND** a sprint unplanned lane — not one or the other.
2. Generated filtered group pages are **Story 9.13** (not hash/query filters on full lists). Leave group-root hrefs rewirable; do not invent filtered pages here.

### Surfaces in scope

| Surface | Role | Change |
|---------|------|--------|
| **Project sunburst** (`Charts.Sunburst`) | Home + `epics.html` | **Primary** — add synthetic **Unplanned / Direct work** root; move unattributable deferred + open quick-dev into it; keep epic-attributed deferred/action under epics |
| **Sprint board** (`SprintTemplater.RenderBoard` / `RenderBoardByEpic`) | Sprint page + Home Now & Next board | **Required sibling** — same unplanned set in an Unplanned lane / grouping |
| **`FollowUpGeometry` / WorkInventory plumbing** | Geometry input | Extend (or sibling type) so Charts stays a pure renderer; counts stay ledger-backed |

### Surfaces explicitly OUT of scope

| Surface | Why out |
|---------|---------|
| **Generated filtered group pages** | Story **9.13** |
| **Action-items / deferred-work list chrome** | Stories **9.6 / 9.10 / 9.11** |
| **Home Direct-changes StatTile / Follow-up StatCards** | Already correct (ledger). Keep; optional hint text only |
| **Story Pipeline / sprint progress wheel** | Yaml-tracked stories only — do not mislabel one-offs as stories |
| **Epic sunburst** | Optional: only epic-attributed one-offs that land under that epic via attribution preference; do **not** draw the project-level Unplanned root on epic pages |
| **New frontmatter / yaml schema** | Forbidden (Epic 9 principle) |

### Owner-selected design direction (locked at create-story)

**Silhouette — synthetic Unplanned root (project sunburst):**

- Add a dedicated epic-level synthetic slice labeled **"Unplanned"** / **"Direct work"** (aria + title must say that — never "Follow-ups", never "Story").
- **Membership (the unplanned set):**
  1. **Open** quick-dev entries: `WorkInventory.QuickDev` where `route: one-shot` already classified them, and status is **not** done/resolved (case-insensitive). Null/empty status counts as open (remaining work).
  2. **Unattributable deferred items:** `FollowUpGeometry.UnattributedDeferredItems` (epic number null after existing `ResolveEpicNumber` / `SourceStoryId` attribution).
- **Split from today's Follow-ups orphan slice:** unattributed **action items** stay in the existing synthetic **Follow-ups** slice (9.13 treats "Unplanned root" and "unattributed action items" as separate groups). Do **not** dump quick-dev into Follow-ups.
- **Attribution preference (AC #1):** when an item can resolve to an epic, place it under that epic's story ring instead of Unplanned:
  - Deferred: keep today's `SourceStoryId` → epic path (`FollowUpGeometry.ResolveEpicNumber`) — already correct.
  - Quick-dev: best-effort over **existing** text only — e.g. `FollowUpRefs.StoryIdFromKey` / Story N.M / Epic N mentions in title or filename stem; optional: a deferred note that names the `spec-*` file and carries a source story. No new frontmatter fields. If unresolved → Unplanned.
- **Leaf wedges:** one wedge per open quick-dev (href = `QuickDevEntry.OutputPath`) and one per unattributable deferred item (href = existing `FollowUpDeferredSlot.DetailHref` / 9.11). Aria labels: `"Direct change: {title}"` / `"Deferred item: …"` — never `"Story …"`.
- **Group root href (temporary until 9.13):** link the Unplanned epic-level arc to a sensible existing page (prefer deferred page when only deferred members; else first quick-dev page or deferred list). Document the seam so 9.13 can swap to `follow-ups/…` filtered group pages without reshaping geometry membership.
- **Distinct treatment (never color-only):** reuse `.sb-followup-open` / done green for deferred peers if they already read as follow-ups; give quick-dev a **distinct label + class** (e.g. `.sb-unplanned` / `.sb-direct`) with word legend entry **"Direct change"** / **"Unplanned"** — not a lifecycle stage swatch. No new `--status-*` token.
- **Weighting:** Unplanned root weight = max(1, member count), same pattern as today's orphan Follow-ups slice (`Charts.Sunburst` ~235–268).
- **NFR8:** zero members → omit Unplanned root entirely (no empty wedge, no legend entry, no broken link).
- **Hint text:** when Unplanned is present, extend `sunburst-hint` to name it (e.g. "Unplanned = direct / one-shot work outside the epic plan").

**Silhouette — sprint unplanned lane (AC #2):**

- Render the **same membership set** as the sunburst Unplanned root (shared pure helper — one source of truth for "what is unplanned").
- **By-status board:** add a trailing lane (or clearly labeled grouping) **"Unplanned"** that holds cards for those items — not fake `SprintEntry` stories. Cards link to the same hrefs as sunburst leaves (spec page / deferred detail). Status badge from existing quick-dev `Status` / deferred resolved flag via `StatusStyles` — no new token.
- **By-epic board:** add a trailing synthetic swimlane **"Unplanned / Direct work"** (always after real epics; ignore epic filter defaults so it stays visible when the set is non-empty — or treat like `n < 0` "Ungrouped" but with the Unplanned label). Same cards.
- **Home board cap:** include unplanned cards in the home board when the set is non-empty; respect existing cap patterns without hiding the entire Unplanned lane.
- **NFR8:** empty set → no Unplanned lane / swimlane.
- Do **not** inject one-offs into yaml `SprintStatus.Entries` or invent sprint keys.

## Tasks / Subtasks

- [x] **Task 1 — Shared unplanned-set projection (AC: #1, #2)**
  - [x] Introduce a pure helper (extend `FollowUpGeometry` or add `UnplannedWorkGeometry` / methods on `WorkInventory`) that, given `WorkInventory` + deferred slots + optional `EpicsModel`, returns:
    - epic-attributed quick-dev (for epic story-ring peers)
    - unplanned members (open unattributable quick-dev + unattributable deferred slots)
  - [x] Open-filter for quick-dev: status not done/resolved; null/empty → open.
  - [x] Attribution: deferred via existing epic resolution; quick-dev via best-effort text/filename only (reuse `FollowUpRefs` where possible).
  - [x] Assert member counts are consistent with ledger fields used for display (`DirectChanges` / `DeferredOpenItems` are authorities for *totals*; geometry may show the open subset — document and test the relationship; never invent a second parse of deferred markdown that can drift from `ProjectCounts.DeferredOpenItems`).

- [x] **Task 2 — Project sunburst Unplanned root (AC: #1)**
  - [x] Extend `Charts.Sunburst` to render the Unplanned synthetic root when the set is non-empty; omit when empty.
  - [x] Move unattributable deferred wedges out of the Follow-ups orphan slice into Unplanned; keep unattributed action items on Follow-ups.
  - [x] Render open quick-dev leaf wedges under Unplanned (or under the preferred epic when attributed).
  - [x] Update legend + `sunburst-hint`; aria/titles never say "Story".
  - [x] Thread inputs from existing `DashboardView` / epics index call sites (`HtmlRenderAdapter.Dashboard.cs`, `HtmlRenderAdapter.Epics.cs`) — Charts stays pure.

- [x] **Task 3 — Sprint board Unplanned lane (AC: #2)**
  - [x] Extend `SprintTemplater.RenderBoard` with an Unplanned lane using the shared set; distinct card markup (not story cards mislabeled).
  - [x] Extend `RenderBoardByEpic` with a trailing Unplanned swimlane.
  - [x] Wire Home board (`HtmlRenderAdapter.Dashboard` Now & Next) so the same set appears; keep epic filter behavior coherent (Unplanned not filtered away by epic multi-select unless intentionally hidden when empty).
  - [x] CSS: `.sprint-lane.unplanned` / card classes near existing sprint-board rules; never color-only.

- [x] **Task 4 — Guardrails (AC: #1, #2)**
  - [x] Do **not** change `sprint-status.yaml` / `deferred-work.md` / quick-dev frontmatter schemas.
  - [x] Do **not** build 9.13 filtered group pages; keep group-root hrefs simple and documented.
  - [x] Do **not** fold DirectChanges into epic/story/task tallies (Story 2.1 / WorkInventory invariant).
  - [x] Leave 9.6/9.10/9.11 templaters alone except read-only reuse of deferred slots / detail hrefs.
  - [x] No JS for navigation (webview CSP; NFR5).

- [x] **Task 5 — Tests + golden (AC: #1, #2)**
  - [x] `ChartsTests`: Unplanned root present with open quick-dev + unattributable deferred; omitted when empty; attributed quick-dev/deferred prefer epic ring; Follow-ups orphan still holds unattributed action items only; aria never "Story" for direct/unplanned wedges.
  - [x] Sprint board tests (`HtmlTemplaterTests` / sprint tests): Unplanned lane/swimlane shows same membership; omitted when empty; cards href to spec/detail pages.
  - [x] Ledger agreement pins where counts are displayed.
  - [x] Golden fingerprint will move (home + sprint + epics index) → regen `SiteGeneratorAdapterTests` expected hash per `golden-diff-normalization-gotchas`. Confirm three `Render*ParityTests` green; no new `HostRenderException`.
  - [x] Run `dotnet test` from repo root.

## Dev Notes

### Why this exists (product gap)

Quick-dev one-shots are first-class artifacts (`WorkInventory` / Story 2.1) and counted on Home (`DirectChanges`), but the Driver's primary "what's left" surfaces — sunburst + sprint board — only know epics/stories/tasks/follow-ups. Unattributable deferred items are buried in the Follow-ups orphan slice beside unattributed action items, so direct work disappears into process debt. Owner locked both geometry root **and** sprint lane (2026-07-17).

### Current code reality (read before editing)

```153:278:src/SpecScribe/Charts.cs
// Project sunburst: epic ring + story-ring peers (stories, action items, deferred).
// Unattributed action + unattributed deferred share ONE synthetic "Follow-ups" epic-level slice (~235–268).
// Quick-dev is NOT rendered here at all.
```

```15:87:src/SpecScribe/FollowUpGeometry.cs
// FollowUpGeometry.From(actionItems, counts, work, deferredModel, epics)
// UnattributedDeferredItems / UnattributedActionItems already split by EpicNumber.
// No QuickDev slots today — extend or sibling.
```

```1:63:src/SpecScribe/WorkInventory.cs
// QuickDevEntry(Title, OutputPath, Status, Type) from route: one-shot under implementation-artifacts.
// DirectChanges = QuickDev.Count (ALL statuses) in ProjectCounts — do not fold into epic/story tallies.
```

```176:259:src/SpecScribe/SprintTemplater.cs
// RenderBoard: lifecycle lanes from SprintEntry stories only.
// GroupByEpic uses EpicNumber ?? -1 as "Ungrouped" — no Unplanned membership yet.
```

```129:138:src/SpecScribe/DashboardViewBuilder.cs
// Home "Direct changes" StatTile already surfaces QuickDev + deferred sub-line from the ledger.
```

### What must be preserved

- Epic-attributed action items + deferred under their epic story ring (9.7).
- Unattributed action items' Follow-ups synthetic slice (distinct from Unplanned for 9.13).
- Per-item deferred/action detail hrefs (9.11 `FollowUpGeometry.HrefFor` / deferred detail slots).
- Ledger authority (`ProjectCounts`) — no parallel recount from a second deferred parse.
- WorkInventory invariant: quick-dev never inflates epic/story/task completion.
- Copy-payload discipline on action-items pages (irrelevant to SVG/board cards, but do not "helpfully" whole-page linkify).

### What this story changes

- Sunburst membership + a new Unplanned synthetic root.
- Sprint board gains an Unplanned lane / swimlane fed by the same set.
- Geometry input plumbing to carry quick-dev + the split between Follow-ups vs Unplanned.

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Quick-dev inventory | `WorkInventory.QuickDev` / `QuickDevEntry` | `WorkInventory.cs` |
| Ledger totals | `ProjectCounts.DirectChanges`, `DeferredOpenItems`, `OpenActionItems` | `ProjectCounts.cs` |
| Deferred attribution + slots | `FollowUpGeometry` / `FollowUpDeferredSlot` | `FollowUpGeometry.cs` |
| Story/Epic id from text | `FollowUpRefs.StoryIdFromKey`, `StoryEpicLinkifier` | `FollowUpRefs.cs` |
| Sunburst wedge helpers | `AppendFollowUpSlot`, `AppendDeferredItemSlot`, orphan-slice pattern | `Charts.cs` |
| Board lane pattern | `RenderBoard` / `RenderBoardByEpic` / `AppendBoardCard` | `SprintTemplater.cs` |
| Status badges | `StatusStyles` | `StatusStyles.cs` |
| Spec page href | `QuickDevEntry.OutputPath` | already generated standalone pages |
| Deferred detail href | `FollowUpDeferredSlot.DetailHref` | Story 9.11 |

### Guardrails & invariants

- **Owner-locked:** Unplanned sunburst root **and** sprint Unplanned lane — both required.
- **Same set on both surfaces** — one pure membership helper; tests pin equality.
- **Follow-ups ≠ Unplanned.** Unattributed action items stay Follow-ups; unattributable deferred + open quick-dev → Unplanned.
- **Attribution prefers epic** when resolvable; else Unplanned.
- **No new authoring schema.** Derive from `route: one-shot`, existing status/type frontmatter, deferred provenance, and optional text mentions.
- **Single count ledger (8.3 / FR21).** Displayed counts agree with `ProjectCounts`.
- **Never mislabel as stories.** Aria, titles, legend, lane labels, card classes.
- **Never color-only; no new `--status-*` token.**
- **NFR8:** empty → omit Unplanned root and lane.
- **9.13 seam:** do not invent filtered group pages; keep group-root hrefs swappable.
- **Shared BodyHtml path** for dashboard/epics sunburst → HTML+webview+SPA stay aligned.
- **Golden moves on purpose** — regen.

### Previous story intelligence

- **9.7 shipped story-ring peers + Follow-ups orphan**, not the originally locked 4th outer ring — match *current* geometry, not the outdated 9.7 create-story silhouette.
- **9.7 completion:** deferred items became per-item wedges when `DeferredWorkModel` is available; unattributed deferred share Follow-ups with unattributed actions — **this story splits that**.
- **9.11:** leaf deferred/action wedges already have stable detail URLs; quick-dev already has standalone pages via `OutputPath` — reuse, don't rebuild.
- **9.10/9.6:** leave list/detail chrome alone.
- **WorkInventory / Story 2.1:** Direct changes are a *separate* signal from epic/story completion — geometry visibility must not break that.

### Git intelligence

Recent commits (`89ad78a`, `302ab40`, `f857f8e`, `8cc9535`) concentrated on 9.8/9.11 follow-up geometry, deferred attribution, and dashboard layout. Prefer extending `FollowUpGeometry` + `Charts.Sunburst` + `SprintTemplater` over new parallel chart systems.

### Project Structure Notes

- Primary edits: `FollowUpGeometry.cs` (or small new pure type beside it), `Charts.cs`, `SprintTemplater.cs`, `HtmlRenderAdapter.Dashboard.cs` / `Epics.cs`, possibly `DashboardView` / builders if threading needs it, `assets/specscribe.css`, tests under `tests/SpecScribe.Tests/`.
- Do **not** change adapter contracts, package.json, or follow-up list/detail templaters' public APIs except additive optional parameters.

### Testing standards

- xUnit; `Assert.Contains` / `DoesNotContain` on SVG/HTML.
- Pin: Unplanned membership, epic-preference attribution, Follow-ups vs Unplanned split, sprint↔sunburst set equality, degrade-to-absent, aria labels.
- Full suite green including golden + three parity suites.

### Verify before marking review

Generate to `SpecScribeOutput/`. Open **home** sunburst: open `route: one-shot` specs and unattributable deferred appear under **Unplanned**; epic-attributed deferred stay under their epic; unattributed action items stay under **Follow-ups**. Click a quick-dev wedge → its spec page. Open **sprint** page: Unplanned lane/swimlane shows the same items. Empty fixture → no Unplanned root/lane. `dotnet test` green.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.12] — user story + ACs
- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.13] — filtered group pages (out of scope; destination contract)
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml] — owner prefs locked 2026-07-17
- [Source: _bmad-output/implementation-artifacts/9-7-open-follow-ups-in-the-remaining-work-geometry.md] — current geometry (story-ring + Follow-ups orphan)
- [Source: _bmad-output/implementation-artifacts/9-11-follow-up-detail-pages-and-deep-links.md] — per-item deferred/action hrefs
- [Source: _bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md] — `ProjectCounts` ledger
- [Source: src/SpecScribe/Charts.cs] — `Sunburst` / orphan Follow-ups slice
- [Source: src/SpecScribe/FollowUpGeometry.cs] — attribution + deferred slots
- [Source: src/SpecScribe/WorkInventory.cs] — quick-dev / deferred inventory
- [Source: src/SpecScribe/SprintTemplater.cs] — board lanes
- [Source: src/SpecScribe/ProjectCounts.cs] — `DirectChanges` / deferred / action counts
- [Source: src/SpecScribe/FollowUpRefs.cs] — story-key / spec-key resolution
- [Source: tests/SpecScribe.Tests/ChartsTests.cs] — `Sunburst_FollowUps_*` patterns to extend
- [Source: tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs] — golden fingerprint

## Dev Agent Record

### Agent Model Used

Composer (Cursor agent router)

### Debug Log References

### Completion Notes List

- Added `UnplannedWorkGeometry` as the shared pure membership helper: open quick-dev (not done/resolved) + `FollowUpGeometry.UnattributedDeferredItems`; epic attribution for quick-dev via title/filename/`FollowUpRefs`/optional deferred-spec cross-ref.
- Project sunburst: synthetic **Unplanned** root (NFR8 omit when empty); Follow-ups orphan = unattributed action items only; attributed quick-dev as story-ring peers; legend + hint name Direct change / Unplanned.
- Sprint by-status **Unplanned** lane + by-epic **Unplanned / Direct work** swimlane; distinct `unplanned-card` markup (never "Story …"); Home Now & Next and `WriteSprint` wired to the same set.
- Group-root href temporary/swappable for 9.13; no new authoring schema; no filtered group pages; CSS uses `--sb-unplanned` (not a `--status-*` token).
- `dotnet test`: 1332 passed. Golden fingerprint regenerated.

### File List

- src/SpecScribe/UnplannedWorkGeometry.cs
- src/SpecScribe/Charts.cs
- src/SpecScribe/FollowUpGeometry.cs
- src/SpecScribe/SprintTemplater.cs
- src/SpecScribe/DashboardView.cs
- src/SpecScribe/DashboardViewBuilder.cs
- src/SpecScribe/EpicsView.cs
- src/SpecScribe/EpicsViewBuilder.cs
- src/SpecScribe/EpicsTemplater.cs
- src/SpecScribe/HtmlTemplater.cs
- src/SpecScribe/HtmlRenderAdapter.Dashboard.cs
- src/SpecScribe/HtmlRenderAdapter.Epics.cs
- src/SpecScribe/SiteGenerator.cs
- src/SpecScribe/assets/specscribe.css
- tests/SpecScribe.Tests/UnplannedWorkGeometryTests.cs
- tests/SpecScribe.Tests/ChartsTests.cs
- tests/SpecScribe.Tests/SprintTemplaterTests.cs
- tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs
- _bmad-output/implementation-artifacts/9-12-unplanned-and-one-off-work-in-geometry-and-sprint.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-07-17: Story 9.12 — Unplanned sunburst root + sprint Unplanned lane from shared `UnplannedWorkGeometry`; Follow-ups orphan split; golden regen.