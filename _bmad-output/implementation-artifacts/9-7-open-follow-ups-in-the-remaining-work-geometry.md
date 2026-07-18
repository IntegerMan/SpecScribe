---
baseline_commit: 65756cde15c1405f1398ab2d0835a02aee2118f0
---

# Story 9.7: Open Follow-Ups in the Remaining-Work Geometry

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer scanning what's left to work on,
I want open action items and retro commitments represented in the sunburst and related remaining-work surfaces,
so that process follow-through is visible in the same primary attention surface as stories and tasks — not only on the dedicated follow-ups pages.

## Acceptance Criteria

1.
**Given** retrospective action items and/or deferred-work entries exist in the project
**When** the epic/project remaining-work geometry renders (sunburst and any sibling "what's left" summaries that feed the Driver's daily scan)
**Then** those follow-ups appear as first-class remaining work — countable and navigable into their detail/provenance surfaces (open and completed)
**And** open tallies agree with the Story 8.3 `ProjectCounts` ledger (`OpenActionItems`, `DeferredOpenItems`) rather than a parallel recount
**And** when the ledger reports open deferred items but per-item slots cannot be built, an aggregate deferred wedge still links to the deferred surface (do not drop ledger debt)
**And** unattributable deferred items render under the Unplanned root (Story 9.12), while unattributed action items stay on the Follow-ups orphan.

2.
**Given** follow-up items are not stories or tasks
**When** the visualization is designed
**Then** they are not silently mislabeled as stories; the treatment is distinct (dashed stroke + dedicated classes + word labels) and never color-only
**And** completed follow-ups use a dedicated follow-up-done class — never reuse story-stage `.sb-done` alone
**And** Story 9.6 remains the provenance/resolution owner on follow-up pages — this story does not absorb 9.6's card/grouping work.

3.
**Given** a project with no action items and no deferred-work surface (empty follow-up inventory)
**When** generation runs
**Then** the sunburst/remaining-work geometry degrades cleanly (no empty fake wedges, no broken links) per NFR8
**And** when only completed follow-ups remain, done wedges may still render (green follow-up-done) — omit geometry only when there is nothing to show.

## Context & Scope

Epic 9 completes the requirement → epic → story chain and closes review follow-through. Story **9.6** (status `review`) already owns FR30 on the **standalone follow-up pages** (`action-items.html`, `deferred-work.html`): provenance, resolution links, grouping, cross-links, structured deferred cards. The Epic 8 retrospective seated **this** story because the Driver's primary "what's left" scan — the **project sunburst** — still only knows epics → stories → tasks. Open process commitments live only on follow-up pages and home StatCards, which is a traceability hole for debt follow-through (user journey 7).

**This story extends FR30 into geometry visibility only.** It does **not** redo 9.6's card/grouping/linkify work, does **not** invent a new authoring schema, and does **not** absorb Stories 9.8/9.9.

### Surfaces in scope

| Surface | Role | Change |
|---------|------|--------|
| **Project sunburst** (`Charts.Sunburst`) | Home "Project at a Glance" + `epics.html` index | **Primary deliverable** — add follow-up geometry |
| **Epic sunburst** (`Charts.EpicSunburst`) | Epic detail pages | Sibling: that epic's open action items (and deferred attributed to its stories when known) |
| **Follow-up StatCards** (`AppendWorkSummaryCards`) | Home tile band | **Already correct** (ledger counts + links). Keep; do not replace with geometry. Optional one-line sunburst hint may cross-reference them — not required. |

### Surfaces explicitly OUT of scope

| Surface | Why out |
|---------|---------|
| **Story Pipeline** (`Charts.RefinementFunnel`) | Defined-story lifecycle only — mixing follow-ups would violate AC #2 (mislabeled as stories) |
| **Sprint progress wheel** (`SprintTemplater.RenderProgressWheel`) | Yaml-tracked stories only — same mislabel risk |
| **Task sunburst** (`Charts.TaskSunburst`) | Per-story task breakdown — follow-ups are not tasks |
| **Action-items / deferred-work page chrome** | Story **9.6** owns these |
| **Story Pipeline / Now & Next command surface** | Story **9.8** |

### Owner-selected design direction (locked — code-review 2026-07-17)

Elicited per Epic 3/7/8 visual-intent practice; create-story considered three directions (outer band / fixed slice / epic markers). **Owner-accepted redesign (post-implementation):** follow-ups as **story-ring peers** under their epic (not a 4th outer band).

Concrete silhouette rules:

- **Ring placement:** attributed action items and epic-level deferred sit in the **story ring** as peers of stories. Story-child deferred nest in a thin outer ring under the parent story. When the follow-up inventory is empty, omit follow-up wedges entirely (no fake zero wedges).
- **Action-item wedges:** one wedge per sprint action item (open + done). Place under the matching epic when `EpicNumber` resolves to a known epic; otherwise into the trailing **Follow-ups** orphan arc (including `EpicNumber` values missing from the epic model). Equal weight within the peer set is fine and deterministic.
- **Deferred wedges:** when a deferred surface exists (`WorkInventory.Deferred`), emit per-item wedges from the deferred model when available. Unattributable deferred go to the **Unplanned** root (Story 9.12). When `DeferredOpenItems > 0` but slots cannot be built, emit one **aggregate** wedge linking to `Deferred.OutputPath` with aria/title `"N open deferred items"` — never drop ledger debt. Do not re-count against the ledger.
- **Navigation:** `<a href>` to detail pages / deferred list / group pages. Real links in SVG — no JS required (NFR5 / webview CSP).
- **Distinct treatment (AC #2, never color-only):** `.sb-followup-open` / `.sb-followup-done` with **dashed stroke** (kinship with `.sb-noplan`) + legend word labels ("Open follow-up", "Done follow-up"). Do **not** paint follow-ups as story-stage `.sb-done` / `.sb-pending` / etc. **No new `--status-*` token.**
- **Legend + hint:** extend `SunburstLegend` with follow-up entries when any follow-up wedges are present. Hint describes story-ring peers + dashed treatment — not "outermost: open follow-ups".
- **aria-label / `<title>`:** `"Action item: …"` / `"Action item (done): …"` / `"Deferred item: …"` — never `"Story …"`. Truncate long text; empty text gets a fallback label.
- **EpicSunburst:** same peer treatment, filtered to that epic. Empty for that epic → omit follow-up wedges (even if the project has others). Center label stays story-first when stories exist; when an epic has follow-ups/direct work but zero stories, do not claim "0 stories" as the only center signal.

## Tasks / Subtasks

- [x] **Task 1 — Plumb follow-up inputs into sunburst callers without recounting (AC: #1, #3)**
  - [x] Introduce a small pure input type (e.g. `FollowUpGeometry` / params on `Charts.Sunburst`) carrying: open action items (`IReadOnlyList<SprintActionItem>` or a slim projection), deferred open count + href (from ledger + `WorkInventory.Deferred`), optional deferred per-item summaries. **Counts rendered must equal** `ProjectCounts.OpenActionItems` and `ProjectCounts.DeferredOpenItems` — assert in tests; never `OpenActionItems.Count` at a second parse site that could drift.
  - [x] Thread from `DashboardViewBuilder` / `DashboardView` (and epics-index path) using existing `_sprint.OpenActionItems`, `view.Counts`, `view.Work` — do not rebuild `WorkInventory` or re-parse `sprint-status.yaml` inside `Charts`.
  - [x] Update call sites: `HtmlRenderAdapter.Dashboard.cs` (~47), `HtmlRenderAdapter.Epics.cs` (~33 project sunburst, ~195 `EpicSunburst`). Keep `TaskSunburst` untouched.
  - [x] If `DashboardView` gains fields, keep serialization/parity green (`SectionViewModelSerializationTests` / existing dashboard JSON round-trips). Prefer optional/default-empty so zero-follow-up fixtures stay byte-stable aside from intentional geometry absence.

- [x] **Task 2 — Project sunburst follow-up outer band (AC: #1, #2, #3)**
  - [x] Extend `Charts.Sunburst` to draw the 4th ring per locked silhouette when open follow-ups exist; omit entirely when both ledger fields are 0 / deferred surface absent.
  - [x] Reuse `AnnularSector`, `AppendNoPlanArc`-style dashed look (new class, don't overload `.sb-noplan` semantics), `SunburstLegend`, `Html`/`PathUtil.StripHtmlTags` helpers.
  - [x] Update center/hint/aria as needed so the chart still reads as epic-first; follow-ups are an **additional** outer signal, not a replacement for the epic count center.
  - [x] Deterministic angular order (epic number ascending, then file order within epic, unattributed last) — regeneration byte-identical.

- [x] **Task 3 — Epic sunburst sibling (AC: #1, #3)**
  - [x] Extend `Charts.EpicSunburst` with the same follow-up ring filtered to the current epic. Same CSS classes / legend rules. Degrade to no ring when that epic has zero open follow-ups.

- [x] **Task 4 — CSS + legend (AC: #2)**
  - [x] Add `.sb-followup-*` rules in `src/SpecScribe/assets/specscribe.css` near the existing sunburst block (~2281–2399). Dashed stroke + legend swatch that is **not** a lifecycle stage swatch. Wire `:has()` legend emphasis if the existing sunburst legend pattern extends cleanly; if not, visible label+stroke at rest is enough (never color-only).
  - [x] Extend `StylesheetTests` for the new classes / non-color-only signal.

- [x] **Task 5 — Guardrails: do not absorb 9.6; do not break StatCards (AC: #2)**
  - [x] Do **not** edit `ActionItemsTemplater` / `DeferredWorkTemplater` / `FollowUpRefs` / deferred parser for this story except optional **read-only** reuse of an already-built model if SiteGenerator caches one. Geometry links are normal SVG `<a href>` — no action-items copy-payload risk.
  - [x] Confirm `AppendWorkSummaryCards` still gates independently and still reads `counts.DeferredOpenItems` / `OpenRetroActionItems` (already ledger-backed).

- [x] **Task 6 — Tests + golden (AC: #1, #2, #3)**
  - [x] `ChartsTests`: sunburst emits follow-up segments + distinct classes + correct hrefs when open items exist; **no** follow-up ring / no fake wedges when ledger is zero; aria text does not say "Story" for follow-up wedges; segment count matches ledger.
  - [x] `EpicSunburst_*`: epic-filtered follow-ups only.
  - [x] `HtmlRenderAdapterTests` or generation E2E: home sunburst contains follow-up geometry when fixtures have open retro items / deferred open count.
  - [x] `ProjectCountsTests` untouched unless a new assertion documents geometry-must-read-ledger (optional regression comment/test).
  - [x] Golden fingerprint will move (dashboard + epics index + epic pages with follow-ups) → regen `SiteGeneratorAdapterTests` expected hash per `golden-diff-normalization-gotchas`. Shared `BodyHtml` path → HTML/webview/SPA stay aligned; **no** new `HostRenderException`. Confirm three `Render*ParityTests` green.
  - [x] Run `dotnet test` from repo root.

### Review Findings

- [x] [Review][Decision] Accept story-ring redesign vs restore locked 4th outer band — resolved: accept redesign; update AC/locked design to story-ring peers.
- [x] [Review][Decision] Distinct non-color-only treatment for follow-ups (esp. done) — resolved: add distinct classes/stroke for open + done follow-ups (not story-stage fills).
- [x] [Review][Decision] Completed follow-ups when open ledger is zero — resolved: show done always; fix deferred gating so resolved items still appear.
- [x] [Review][Decision] Unattributable deferred on Unplanned vs Follow-ups orphan — resolved: ratify Unplanned placement; update 9.7 AC to match 9.12.

- [x] [Review][Patch] Update AC + locked silhouette to story-ring peers (not 4th outer band); ratify unattributable deferred → Unplanned [`9-7-open-follow-ups-in-the-remaining-work-geometry.md`]
- [x] [Review][Patch] Distinct CSS/SVG classes + stroke for open and done follow-ups (never reuse `.sb-done` alone) [`Charts.cs` / `specscribe.css`]
- [x] [Review][Patch] Build deferred slots whenever deferred surface/model has items — do not gate on `DeferredOpenItems > 0` [`FollowUpGeometry.cs:89`]
- [x] [Review][Patch] Emit deferred aggregate (or reconcile) when `DeferredOpenCount > 0` but slots empty [`FollowUpGeometry.cs:89` / `Charts.cs`]
- [x] [Review][Patch] Prefix `DeferredListHref` with story page depth in `BuildStory` [`EpicsViewBuilder.cs:198`]
- [x] [Review][Patch] Treat action items whose `EpicNumber` is missing from the epic model as unattributed orphans [`Charts.cs:173` / `FollowUpGeometry.cs:162`]
- [x] [Review][Patch] Fallback label when action/deferred text is empty [`Charts.cs:439`]
- [x] [Review][Patch] Include story-child deferred in epic aria follow-up count [`Charts.cs:220`]
- [x] [Review][Patch] EpicSunburst center should not read “0 stories” when only follow-ups/direct work exist [`Charts.cs:619`]
- [x] [Review][Patch] Clamp annular pad to ≤ half sweep so tiny wedges cannot invert [`Charts.cs:205` / `Charts.cs:490`]

- [x] [Review][Defer] Epic/story weight ignores nested story-child deferred crowding [`Charts.cs:195`] — deferred, pre-existing
- [x] [Review][Defer] `FollowUpGeometry.From` does not assert list lengths vs ledger open counts [`FollowUpGeometry.cs:81`] — deferred, pre-existing

## Dev Notes

### Why this exists (product gap)

From Epic 8 retrospective: *"Action items / retro commitments invisible in the sunburst. Project Lead's primary 'what's left' surface only knows epics/stories/tasks."* Epic 8 made status trustworthy; it did not make open retro commitments visible in remaining-work geometry. FR30 on dedicated pages (9.6) is necessary but not sufficient for Driver daily scan (journeys 1–2 + 7).

### Current code reality (read before editing)

```160:255:src/SpecScribe/Charts.cs
// Charts.Sunburst(EpicsModel, size, CommandCatalog?) — 3 rings only; legend = 6 lifecycle stages;
// hint = "Inner ring: epics · middle: stories · outer: task completion."
```

```47:47:src/SpecScribe/HtmlRenderAdapter.Dashboard.cs
sb.Append(Charts.Sunburst(epicsForSunburst, commands: view.Commands));
```

```135:137:src/SpecScribe/ProjectCounts.cs
DeferredOpenItems = work.Deferred?.OpenItemCount ?? 0,
OpenActionItems = sprint?.OpenActionItems.Count ?? 0,
```

```390:417:src/SpecScribe/HtmlRenderAdapter.Dashboard.cs
// AppendWorkSummaryCards — already shows Deferred / Action Items StatCards from the ledger.
```

Follow-ups are **not** in sunburst today. StatCards already satisfy count visibility on home — this story makes them **geometry-first-class**.

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Ledger counts (authority) | `ProjectCounts.OpenActionItems`, `DeferredOpenItems` | `ProjectCounts.cs` |
| Open action item list | `SprintStatus.OpenActionItems` | `SprintStatus.cs:34` |
| Action item fields | `SprintActionItem(Action, Status, EpicNumber, Owner)` | `SprintStatus.cs:16` |
| Deferred presence + href + open count | `WorkInventory.Deferred` / `DeferredWorkEntry` | `WorkInventory.cs` |
| Action-items page path | `SiteNav.ActionItemsOutputPath` | SiteNav |
| Project / epic sunburst (extend) | `Charts.Sunburst`, `Charts.EpicSunburst` | `Charts.cs:160+`, `:335+` |
| Dashed non-story arc precedent | `AppendNoPlanArc` + `.sb-noplan` | `Charts.cs:302+`, css ~2314 |
| Legend + keyboard emphasis | `SunburstLegend` | `Charts.cs:263+` |
| Status badges on **detail** pages only | `StatusStyles.ForSprint` / `SprintLabel` | `StatusStyles.cs` — do **not** map follow-up wedges onto story stage `.sb-*` fills |
| Home StatCards (leave working) | `AppendWorkSummaryCards` | `HtmlRenderAdapter.Dashboard.cs:390` |
| Provenance/grouping on detail pages | Story 9.6 surfaces | `ActionItemsTemplater`, `DeferredWorkTemplater`, `FollowUpRefs` |
| Dashboard ledger wiring | `DashboardViewBuilder` / `OpenRetroActionItems` | `DashboardViewBuilder.cs:61` |

### Guardrails & invariants

- **Single count ledger (Story 8.3 / FR21).** Geometry must agree with `ProjectCounts` — never a parallel recount from a second parse of yaml/markdown that can diverge from `DeferredOpenItems` (WorkInventory HTML count) vs structured parser open count.
- **Split, don't absorb 9.6.** No card redesign, no grouping/cross-link changes, no copy-payload work on action-items.
- **Never color-only (UX-DR17); no new `--status-*` token.**
- **Never mislabel as stories (AC #2).** Aria, titles, legend, and CSS class names must say action item / deferred work.
- **NFR8 degrade:** zero open follow-ups → no 4th ring, no empty legend entries, no broken hrefs. No deferred note → no deferred wedges (even if somehow a count leaked).
- **No JS** for essential navigation (webview CSP; NFR5). Pure SVG + CSS.
- **Shared BodyHtml path:** sunburst changes on dashboard/epics propagate to HTML + webview + SPA automatically — do not fork a webview-only chart.
- **Golden moves on purpose** — regen fingerprint; don't fight it.
- **Coordinate:** 9.6 may still be `review` — consume its shipped APIs; if deferred structured model isn't cached on the generator yet, aggregate deferred wedge from ledger + `WorkInventory.Deferred` is the safe default.

### Previous story intelligence (9.6)

- Standalone follow-up pages are **not** `IRenderAdapter` bodies; this story **is** on the shared dashboard/epics BodyHtml path — parity matters here.
- Never whole-page linkify action-items (copy payload trap) — irrelevant to SVG hrefs, but don't "helpfully" run sunburst HTML through `ApplyReferenceLinks` in a way that corrupts attributes.
- No new authoring schema — derive from existing `action_items:` + deferred note / inventory.
- Home callout counts must keep working; geometry should match those numbers.

### Git intelligence

Recent work (9.3–9.6, home layout) put sunburst first on the dashboard and made follow-up StatCards more visible (`65756cd`). This story completes the scan path by putting open follow-ups **inside** that sunburst, not beside it only.

### Project Structure Notes

- Primary edits: `Charts.cs`, `HtmlRenderAdapter.Dashboard.cs`, `HtmlRenderAdapter.Epics.cs`, optionally `DashboardView.cs` / `DashboardViewBuilder.cs`, `assets/specscribe.css`, tests under `tests/SpecScribe.Tests/`.
- Likely **no** new top-level types file unless `FollowUpGeometry` deserves its own small record file next to `ProjectCounts` — keep it next to Charts or as a nested record if tiny.
- Do **not** change `sprint-status.yaml` / `deferred-work.md` schemas, `package.json`, or 9.6 templaters' public contracts except additive optional parameters.

### Testing standards

- xUnit; `Assert.Contains` / `DoesNotContain` on SVG/HTML strings (`ChartsTests` pattern).
- Pin: ledger agreement, distinct classes, degrade-to-absent, epic filter on `EpicSunburst`, no "Story" aria on follow-up wedges.
- Full suite green including golden + three parity suites.

### Verify before marking review

Generate to `SpecScribeOutput/` (not `docs/live`). Open **home**: sunburst shows outermost follow-up band when this repo has open action items; click a wedge → action-items or deferred-work page; StatCards still match the wedge counts. Open an **epic page** that owns open action items → epic sunburst shows them; an epic with none → no fake ring. Zero-follow-up fixture / test → sunburst identical to pre-story 3-ring shape (no empty band). `dotnet test` green.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.7] — user story + ACs (epics.md:1748–1771)
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9] — FR30 extension into sunburst; NFR8
- [Source: _bmad-output/implementation-artifacts/epic-8-retro-2026-07-15.md] — why 9.7 was seated; visual-intent requirement
- [Source: _bmad-output/implementation-artifacts/9-6-follow-up-item-provenance-and-resolution-paths.md] — scope boundary (9.6 owns detail pages)
- [Source: _bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md] — `ProjectCounts` ledger
- [Source: src/SpecScribe/Charts.cs] — `Sunburst` / `EpicSunburst` / `AppendNoPlanArc` / `SunburstLegend`
- [Source: src/SpecScribe/ProjectCounts.cs] — `OpenActionItems`, `DeferredOpenItems`
- [Source: src/SpecScribe/HtmlRenderAdapter.Dashboard.cs] — sunburst call + StatCards
- [Source: src/SpecScribe/HtmlRenderAdapter.Epics.cs] — epics index + epic sunburst
- [Source: src/SpecScribe/SprintStatus.cs] — `OpenActionItems`, `SprintActionItem`
- [Source: src/SpecScribe/WorkInventory.cs] — deferred entry + open count
- [Source: src/SpecScribe/assets/specscribe.css] — `.sunburst` / `.sb-noplan` / `.journey-followup`
- [Source: tests/SpecScribe.Tests/ChartsTests.cs] — sunburst test patterns
- [Source: tests/SpecScribe.Tests/FollowUpSurfacesTests.cs] — 9.6 degrade / home callout fixtures to reuse

## Dev Agent Record

### Agent Model Used

Composer (Auto)

### Debug Log References

### Completion Notes List

- Added `FollowUpGeometry` ledger-backed input; threaded through `DashboardView` / `EpicsIndexView` / `EpicPageView` and HTML/webview/SPA SiteGenerator call sites without re-parsing yaml.
- Follow-ups render as **story-ring peers under their epic** (orange open / green done), not an outer band. Unattributed action items + deferred aggregate share a synthetic **Follow-ups** epic-level slice. Aria never says "Story".
- Epic sunburst filters to that epic's action items; deferred aggregate stays on the project unattributed slice.
- StatCards / 9.6 templaters untouched. Golden regenerated. `dotnet test` green after redesign.

### File List

- src/SpecScribe/FollowUpGeometry.cs
- src/SpecScribe/Charts.cs
- src/SpecScribe/DashboardView.cs
- src/SpecScribe/DashboardViewBuilder.cs
- src/SpecScribe/EpicsView.cs
- src/SpecScribe/EpicsViewBuilder.cs
- src/SpecScribe/EpicsTemplater.cs
- src/SpecScribe/HtmlRenderAdapter.Dashboard.cs
- src/SpecScribe/HtmlRenderAdapter.Epics.cs
- src/SpecScribe/SiteGenerator.cs
- src/SpecScribe/assets/specscribe.css
- tests/SpecScribe.Tests/ChartsTests.cs
- tests/SpecScribe.Tests/FollowUpSurfacesTests.cs
- tests/SpecScribe.Tests/StylesheetTests.cs
- tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs
- _bmad-output/implementation-artifacts/9-7-open-follow-ups-in-the-remaining-work-geometry.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

### Change Log

- 2026-07-16: Implemented Story 9.7 — open follow-ups as outermost sunburst band (project + epic), ledger-agreed counts, dashed distinct treatment, tests + golden.
- 2026-07-16: Redesign per owner feedback — follow-ups as story-ring peers under epics (orange/green) + unattributed Follow-ups epic slice; removed outer band.
- 2026-07-17: Code review — accepted story-ring redesign + Unplanned for unattributable deferred in AC; patches: `.sb-followup-done` dashed distinct treatment, deferred gating/aggregate, orphan unknown-epic, DeferredListHref prefix, empty-label fallback, aria/center/pad fixes; golden regenerated.

---

**Ultimate context engine analysis completed — comprehensive developer guide created.**
