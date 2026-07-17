---
baseline_commit: 0ea1dd8e3bc033a06c1e394559054727e4d5840e
---

# Story 9.13: Generated Filtered Follow-Up Group Pages and Sunburst Click Destinations

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer clicking a sunburst wedge,
I want every click to land on either that item's detail page or a generated list page that contains only the relevant group,
so that group wedges never dump me into the full deferred-work or action-items dump.

## Acceptance Criteria

1. **OWNER-LOCKED — generated filtered pages (not hash/query filters on the full list).**
**Given** a follow-up group that appears in the sunburst (an epic's attributed follow-ups, the Unplanned / Direct root, unattributed action items, etc.)
**When** the site generates
**Then** a dedicated filtered list page is written for that group (e.g. under `follow-ups/…`, sibling to Story 9.11 detail pages), using the shared Story 9.10 row grammar
**And** the page lists only that group's items; NFR8: no empty group pages.

2.
**Given** the project or epic sunburst
**When** a leaf wedge is clicked (story, action item, deferred item, quick-dev item)
**Then** it links to that item's detail page (Story 9.11 / story page / spec page)
**And** when a group wedge is clicked (epic follow-up aggregate, Unplanned root, Follow-ups slice), it links to that group's generated filtered list page — never the unfiltered whole-site deferred-work or action-items index.

## Context & Scope

Epic 9 extends FR30 into the Driver's remaining-work geometry. Stories **9.10**/**9.11** made list rows scannable and gave every action/deferred item a stable `follow-ups/{slug}.html` detail page; leaf sunburst wedges already deep-link there. Story **9.12** (implemented in working tree — `UnplannedWorkGeometry` + Unplanned sunburst root / sprint lane) split **Follow-ups** (unattributed action items only) from **Unplanned** (open unattributable quick-dev + unattributable deferred). Group-root hrefs are still temporary:

- Follow-ups orphan arc → `geometry.ActionItemsHref` (`action-items.html`)
- Unplanned root → `UnplannedWorkGeometry.GroupRootHref` (deferred list / first leaf — documented swappable seam)

**This story generates filtered group list pages and rewires those group-root clicks.** It does **not** absorb 9.6 provenance chrome, does **not** change leaf destinations, does **not** invent hash/query filters on full lists, does **not** own 10.7 density/drill-down, and does **not** invent a new authoring schema.

### Owner prefs (locked 2026-07-17 in sprint-status.yaml + epics AC)

1. **Generated filtered pages** under `follow-ups/…` — **not** `?filter=` / `#group=` on `action-items.html` / deferred-work.
2. **Separate groups:** Follow-ups orphan ≠ Unplanned root (9.12 membership stays).
3. **10.7** keeps this click-destination contract; do not invent a parallel scheme.

### Surfaces in scope

| Surface | Role | Change |
|---------|------|--------|
| **Generated group pages** | New `follow-ups/group-*.html` (or equivalent) | **Primary** — one page per non-empty sunburst group; 9.10 `FollowUpRow` grammar |
| **Project sunburst group roots** | Follow-ups orphan + Unplanned root arcs | Rewire href → generated group page |
| **Group path helpers** | `FollowUpSlug` / small group-id helper | Stable paths; no collision with `action-` / `deferred-` detail slugs |
| **`SiteGenerator`** | Emission beside `WriteFollowUpDetails` | `WriteFollowUpGroupPages` via `WriteOutput` |

### Surfaces explicitly OUT of scope

| Surface | Why out |
|---------|---------|
| **Leaf wedge hrefs** | Already 9.11 / story / `QuickDevEntry.OutputPath` — leave alone |
| **Epic inner-ring → `epics/epic-{n}.html`** | Epic navigation stays; see owner silhouette below |
| **Whole-site `action-items.html` / deferred list** | Still generated for nav / StatCards / Journey 7 — sunburst **group** clicks stop using them |
| **9.6 provenance / Resolve command content** | Lives on detail pages; group pages are scan lists |
| **10.7 density / drill-down** | Different story; consumes this contract |
| **New authoring schema / `--status-*` token** | Forbidden |
| **Hash/query filters on full lists** | Owner-locked anti-pattern |

### Owner-selected design direction (locked at create-story)

**Groups to emit (AC #1) — only when membership non-empty (NFR8):**

| Group key | Membership (same as sunburst) | Suggested path |
|-----------|-------------------------------|----------------|
| **Follow-ups** | `FollowUpGeometry.UnattributedActionItems` | `follow-ups/group-follow-ups.html` |
| **Unplanned** | `UnplannedWorkGeometry.UnplannedSet` (open unattributable quick-dev + unattributable deferred) | `follow-ups/group-unplanned.html` |
| **Epic N follow-ups** | `ForEpicNumber(N)` actions + `DeferredForEpicNumber(N)` deferred (+ optional epic-attributed quick-dev for completeness — prefer matching what the epic's story-ring follow-up/deferred peers are; quick-dev already has spec pages as leaves) | `follow-ups/group-epic-{n}.html` |

Path names are illustrative — keep under `FollowUpSlug.Folder` (`follow-ups/`), use a `group-` prefix so they never collide with `action-*` / `deferred-*` detail pages. Prefer **fixed** group keys (not content-hash) so bookmarks survive membership churn within the same group identity.

**Click destinations (AC #2):**

| Wedge | Destination after this story |
|-------|------------------------------|
| Story / task leaf | Story page (unchanged) |
| Action / deferred leaf | `follow-ups/{slug}.html` (unchanged) |
| Quick-dev leaf | Spec `OutputPath` (unchanged) |
| **Follow-ups** orphan arc | `follow-ups/group-follow-ups.html` |
| **Unplanned** root arc | `follow-ups/group-unplanned.html` |
| **Epic** inner arc | **Stay** `epics/epic-{n}.html` — not a follow-up dump |
| Epic follow-up **aggregate** | There is **no** separate aggregate wedge today (attributed follow-ups are leaves under the epic). **Generate** `group-epic-{n}.html` for AC #1 / 10.7 / future links. **Do not** invent a new sunburst wedge in this story and **do not** rewire the epic arc away from the epic page. If a dedicated aggregate wedge appears later (10.7), it must use this page. |

**Page silhouette (reuse, don't reinvent):**

- Standalone `StringBuilder` templater (sibling to `ActionItemsTemplater` / `FollowUpDetailTemplater`) — **not** `IRenderAdapter` / `PageView`.
- Shell: head / nav / breadcrumb / `<main id="main-content">` so SPA/webview capture works via `WriteOutput`.
- Body: title naming the group + count; one `<ul class="followup-rows-list">` of `FollowUpRow.Render` rows.
- Each row's primary link = item detail (action/deferred) or spec page (quick-dev). Source chip = group-appropriate label (e.g. "Unattributed", "Direct change", "Epic N", deferral provenance).
- Mixed Unplanned page: action-style rows for deferred + rows for quick-dev (same `.followup-row` grammar; status via `StatusStyles`; never mislabel as stories).
- Breadcrumb: Home → Follow-ups / Sprint → group title. Optional secondary link to the whole-site list ("All open action items") — fine; primary sunburst path must not land there.
- **Copy-payload:** group pages that embed Resolve commands must keep `data-copy` raw (prefer linking to detail and **not** embedding Resolve on the group list — detail already has it). Safest: scan row + detail href only; no Resolve on group pages.
- Zero JS (webview CSP; NFR5).

**Href plumbing:**

- Replace Follow-ups `orphanHref = geometry.ActionItemsHref` with group path (+ `ApplyLinkPrefix` on epic pages).
- Replace `UnplannedWorkGeometry.GroupRootHref` temporary logic with the stable Unplanned group path (keep the property or rename — Charts already consumes it).
- Thread group hrefs from geometry helpers so Charts stays a pure renderer.

## Tasks / Subtasks

- [ ] **Task 1 — Group identity + paths (AC: #1)**
  - [ ] Add pure helpers (e.g. on `FollowUpSlug` or `FollowUpGroupPages`) for `group-follow-ups`, `group-unplanned`, `group-epic-{n}` output paths under `follow-ups/`.
  - [ ] Unit-test path stability and non-collision with `action-` / `deferred-` prefixes.

- [ ] **Task 2 — Membership projection (AC: #1)**
  - [ ] Pure functions that, given `FollowUpGeometry` + `UnplannedWorkGeometry` (+ epics), enumerate non-empty groups and their members — same sets the sunburst already uses (no second deferred parse; ledger-consistent).
  - [ ] Pin: Follow-ups members = unattributed actions only; Unplanned = `UnplannedSet`; epic group = attributed actions + deferred for that epic.

- [ ] **Task 3 — Group list templater (AC: #1)**
  - [ ] New templater rendering the page shell + `FollowUpRow` list for a filtered membership set (title, crumb, rows).
  - [ ] Support mixed Unplanned members (deferred + quick-dev) without mislabeling as stories.
  - [ ] NFR8: caller never asks for an empty group page.

- [ ] **Task 4 — Emit pages (AC: #1)**
  - [ ] `SiteGenerator.WriteFollowUpGroupPages` beside `WriteFollowUpDetails`; call from `GenerateAll` after geometry inputs exist.
  - [ ] Use `WriteOutput` (SPA/webview capture). Coordinate folder creation with detail writer (group-only repo still creates `follow-ups/`).
  - [ ] No empty files.

- [ ] **Task 5 — Rewire sunburst group roots (AC: #2)**
  - [ ] Follow-ups orphan → group-follow-ups path (prefixed on epic-depth pages).
  - [ ] Unplanned root → group-unplanned path (replace temporary `GroupRootHref` body).
  - [ ] Leave leaf hrefs and epic→epic.html unchanged.
  - [ ] Assert group hrefs never equal `action-items.html` or the unfiltered deferred list path when a group page exists.

- [ ] **Task 6 — Guardrails**
  - [ ] No hash/query filters on full lists.
  - [ ] No new authoring schema; no new `--status-*` token; never color-only.
  - [ ] Do not absorb 9.6/9.10/9.11 chrome beyond row reuse.
  - [ ] Do not implement 10.7 density modes.
  - [ ] No JS navigation.

- [ ] **Task 7 — Tests + golden (AC: #1, #2)**
  - [ ] Path/membership unit tests.
  - [ ] `FollowUpSurfacesTests` / new tests: group pages exist with only that group's items; empty → no page.
  - [ ] `ChartsTests`: orphan + Unplanned roots href to `follow-ups/group-…`; leaves still `follow-ups/action-` / `deferred-` / spec paths; epic arc still `epics/epic-`.
  - [ ] Golden fingerprint moves → regen per `golden-diff-normalization-gotchas`; three parity suites green.
  - [ ] `dotnet test` from repo root.

## Dev Notes

### Why this exists (product gap)

Leaf clicks already land on one item (9.11). Group arcs still dump into the whole-site action-items or deferred index (or a random first leaf via temporary Unplanned href), so the sunburst lies about scope. Owner locked **generated** filtered pages so the destination list matches the wedge membership.

### Current code reality (read before editing — 9.12 already in tree)

```258:293:src/SpecScribe/Charts.cs
// Follow-ups orphan: orphanHref = geometry.ActionItemsHref  ← rewire
// Unplanned root: rootHref = unplannedGeo.GroupRootHref     ← rewire (temporary seam)
```

```73:94:src/SpecScribe/UnplannedWorkGeometry.cs
// GroupRootHref — temporary until 9.13; document says "Swappable seam"
```

```2798:2852:src/SpecScribe/SiteGenerator.cs
// WriteFollowUpDetails — mirror pattern for WriteFollowUpGroupPages beside it
```

```8:83:src/SpecScribe/FollowUpRow.cs
// Shared 9.10 row grammar — group pages MUST call this
```

```17:18:src/SpecScribe/FollowUpSlug.cs
// Folder = "follow-ups"; OutputPath(slug) => follow-ups/{slug}.html
// Add group-* paths; do not collide with action-/deferred-
```

### What must be preserved

- Leaf deep-links (9.11) and quick-dev `OutputPath`.
- 9.12 membership split: Follow-ups ≠ Unplanned; `UnplannedSet` sunburst↔sprint equality.
- Epic arc → epic page.
- Whole-site list pages for nav.
- Ledger authority (`ProjectCounts`); no parallel deferred recount.
- Copy-payload discipline if any Resolve command appears on a page that embeds `data-copy`.
- `ApplyLinkPrefix` / `../follow-ups/` from epic pages.
- Shared BodyHtml / `WriteOutput` path → HTML+webview+SPA aligned.

### What this story changes

- New generated filtered group HTML files under `follow-ups/`.
- Group-root sunburst hrefs only.
- Path helpers + emission + tests.

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Row grammar | `FollowUpRow.Render` | `FollowUpRow.cs` |
| Detail hrefs | `FollowUpGeometry.HrefFor` / `FollowUpDeferredSlot.DetailHref` | `FollowUpGeometry.cs` |
| Unplanned membership | `UnplannedWorkGeometry.UnplannedSet` / `GroupRootHref` seam | `UnplannedWorkGeometry.cs` |
| Follow-ups membership | `UnattributedActionItems` | `FollowUpGeometry.cs` |
| Epic attributed set | `ForEpicNumber` + `DeferredForEpicNumber` | `FollowUpGeometry.cs` |
| Per-item page emission precedent | `WriteFollowUpDetails` / `WriteRequirements` | `SiteGenerator.cs` |
| Write + SPA capture | `WriteOutput` | `SiteGenerator.cs` |
| Slug folder | `FollowUpSlug.Folder` / `OutputPath` | `FollowUpSlug.cs` |
| Status badges | `StatusStyles` | `StatusStyles.cs` |
| Prefix from depth | `FollowUpGeometry.ApplyLinkPrefix` | `FollowUpGeometry.cs` |

### Guardrails & invariants

- **OWNER-LOCKED:** generated pages, not hash/query on full lists.
- **NFR8:** no empty group pages; omit group root when empty (already true for wedges).
- **Single membership truth** with 9.12 geometry — do not invent a second Unplanned set.
- **Never mislabel** follow-ups / direct changes as stories (aria, titles, chips).
- **Never color-only; no new `--status-*` token.**
- **No new authoring schema** (Epic 9 principle).
- **Leaf contract unchanged** — 10.7 depends on this.
- **Epic arc stays epic page** — epic group pages exist for AC #1 / future aggregate links only.
- **Golden moves on purpose** — regen.

### Previous story intelligence

- **9.12** left `GroupRootHref` and Follow-ups `ActionItemsHref` intentionally temporary; this story is the swap. Working tree already has `UnplannedWorkGeometry.cs` (untracked/modified Charts) — build on it; do not re-split membership.
- **9.11** established `follow-ups/` + `WriteOutput` + leaf hrefs — mirror emission, don't fork.
- **9.10** owns `.followup-row` — group pages are filtered siblings of the full lists, not a new visual language.
- **9.7** put follow-ups in geometry; this story finishes honest **group** navigation (leaves already done in 9.11).
- **10.7** must keep this destination contract — do not invent alternate group click targets.

### Git intelligence

Recent work (`0ea1dd8`, `8cc9535`, `f857f8e`, `302ab40`, `89ad78a`) concentrated on sunburst follow-ups, 9.8/9.11, and Unplanned geometry. Prefer extending `UnplannedWorkGeometry.GroupRootHref` + `Charts` orphan href + a small group templater over a parallel chart system.

### Project Structure Notes

- New: group templater (+ optional path helper type), tests.
- Edit: `Charts.cs` (group-root hrefs only), `UnplannedWorkGeometry.cs` (`GroupRootHref`), `FollowUpSlug.cs` or sibling, `SiteGenerator.cs` (`WriteFollowUpGroupPages` + `GenerateAll`), possibly thin geometry fields for Follow-ups group href.
- Do **not** change adapter contracts, sprint yaml / deferred-work schemas, or leaf detail templater public APIs except additive optional crumbs.

### Testing standards

- xUnit; `Assert.Contains` / `DoesNotContain` on HTML/SVG.
- Pin: group page membership ⊆ sunburst group; empty → absent; group root href under `follow-ups/group-`; ≠ `action-items.html`; leaves unchanged; epic arc unchanged.
- Full suite green including golden + three parity suites.

### Verify before marking review

Generate to `SpecScribeOutput/`. Confirm `follow-ups/group-follow-ups.html` / `group-unplanned.html` / `group-epic-*.html` exist only when non-empty. Click Follow-ups orphan → filtered unattributed actions only (not full dump). Click Unplanned root → only Unplanned members. Click a leaf → still detail/spec. Click an epic arc → still epic page. Empty fixture → no empty group files. `dotnet test` green.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.13] — user story + ACs
- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.7] — must keep 9.13 click contract
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml] — owner lock: generated filtered pages
- [Source: _bmad-output/implementation-artifacts/9-12-unplanned-and-one-off-work-in-geometry-and-sprint.md] — membership + temporary href seam
- [Source: _bmad-output/implementation-artifacts/9-11-follow-up-detail-pages-and-deep-links.md] — `follow-ups/{slug}.html` + WriteOutput
- [Source: _bmad-output/implementation-artifacts/9-10-scannable-follow-up-list-pages.md] — `FollowUpRow` grammar
- [Source: src/SpecScribe/Charts.cs] — orphanHref / GroupRootHref attach points
- [Source: src/SpecScribe/UnplannedWorkGeometry.cs] — UnplannedSet + GroupRootHref seam
- [Source: src/SpecScribe/FollowUpGeometry.cs] — UnattributedActionItems / epic attribution
- [Source: src/SpecScribe/FollowUpRow.cs] / [FollowUpSlug.cs] / [SiteGenerator.cs]
- [Source: tests/SpecScribe.Tests/ChartsTests.cs] / [FollowUpSurfacesTests.cs]

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed — comprehensive developer guide created.

### File List
