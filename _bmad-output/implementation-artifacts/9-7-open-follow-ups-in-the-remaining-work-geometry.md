---
baseline_commit: 65756cde15c1405f1398ab2d0835a02aee2118f0
---

# Story 9.7: Open Follow-Ups in the Remaining-Work Geometry

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer scanning what's left to work on,
I want open action items and retro commitments represented in the sunburst and related remaining-work surfaces,
so that process follow-through is visible in the same primary attention surface as stories and tasks — not only on the dedicated follow-ups pages.

## Acceptance Criteria

1.
**Given** open retrospective action items (and deferred-work entries when present) exist in the project
**When** the epic/project remaining-work geometry renders (sunburst and any sibling "what's left" summaries that feed the Driver's daily scan)
**Then** those open follow-ups appear as first-class remaining work — countable and navigable into their detail/provenance surfaces
**And** counts agree with the Story 8.3 `ProjectCounts` ledger (`OpenActionItems` and related) rather than a parallel recount.

2.
**Given** follow-up items are not stories or tasks
**When** the visualization is designed
**Then** they are not silently mislabeled as stories; the treatment is distinct (shape, label, or ring) and never color-only
**And** Story 9.6 remains the provenance/resolution owner on follow-up pages — this story does not absorb 9.6's card/grouping work.

3.
**Given** a project with zero open action items and no deferred-work surface
**When** generation runs
**Then** the sunburst/remaining-work geometry degrades cleanly (no empty fake wedges, no broken links) per NFR8.

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

### Owner-selected design direction (locked at create-story)

Elicited per Epic 3/7/8 visual-intent practice (Epic 8 retro action #3). Three named directions were considered:

1. **Follow-up outer band (4th ring)** — when ledger open counts > 0, add an outermost ring outside the task ring. Wedges are labeled Action item / Deferred work (never "Story"), link to follow-up detail pages, use a **distinct stroke/shape treatment** (dashed kinship with `.sb-noplan`) plus legend text. Epic-attributed when `EpicNumber` / source story is known.
2. **Fixed "Follow-ups" angular slice** — one dedicated pie of the circle regardless of epic — simpler, but loses epic context in the primary scan.
3. **Epic-ring markers only** — dots/ticks on epic arcs — weaker "first-class remaining work" signal; easy to miss.

**Locked choice: #1 Follow-up outer band (4th ring).**

Concrete silhouette rules:

- **Ring placement:** shrink existing epic/story/task radii slightly to make room for `followupInner`/`followupOuter` outside the task ring (same proportional style as today's rings in `Charts.Sunburst` ~167–172). When open follow-up count is 0, **omit the ring entirely** (do not draw a zero wedge).
- **Action-item wedges:** one wedge per `SprintStatus.OpenActionItems` entry. Place under the matching epic's angular sweep when `EpicNumber` is set; otherwise into a trailing unattributed arc (do not drop items). Equal weight within the follow-up ring (or weight by epic group — equal-per-item is fine and deterministic).
- **Deferred wedges:** when `ProjectCounts.DeferredOpenItems > 0` and a deferred surface exists (`WorkInventory.Deferred`), emit deferred follow-up geometry. Prefer **one aggregate wedge** linking to `Deferred.OutputPath` with aria/title `"N open deferred items"` when per-item epic attribution is awkward without re-parsing; if `DeferredWorkModel` is already available at render time (optional cache from 9.6's parser), per-open-item wedges attributed via `SourceStoryId` → epic are allowed — **do not re-count** against the ledger either way.
- **Navigation:** `<a href>` to `SiteNav.ActionItemsOutputPath` / `work.Deferred.OutputPath` (page-level is enough; 9.6 owns in-page provenance). Real links in SVG — no JS required (NFR5 / webview CSP).
- **Distinct treatment (AC #2, never color-only):** new classes e.g. `.sb-followup-action`, `.sb-followup-deferred` with **dashed stroke** (reuse `.sb-noplan` stroke vocabulary) + legend entries with **word labels** ("Action item", "Deferred work"). Do **not** paint follow-ups as `sb-pending`/`sb-active` story stages. **No new `--status-*` token** (six stage tokens remain the stage→color source). Accent fill may reuse parchment/`--ink-light`/`journey-followup` border cues, but shape+label must disambiguate without color.
- **Legend + hint:** extend `SunburstLegend` (or append a second legend row) with follow-up entries **only when the ring is present**. Update `sunburst-hint` when follow-ups exist: include "outermost: open follow-ups".
- **aria-label / `<title>`:** `"Action item: {truncated text}"` / `"Deferred work: N open items"` — never `"Story …"`. Truncate long action text for title readability; full text can live on the destination page.
- **EpicSunburst:** same follow-up ring, filtered to that epic's open action items (+ deferred attributed to that epic when known). Zero for that epic → omit ring (even if project has other follow-ups).

## Tasks / Subtasks

- [ ] **Task 1 — Plumb follow-up inputs into sunburst callers without recounting (AC: #1, #3)**
  - [ ] Introduce a small pure input type (e.g. `FollowUpGeometry` / params on `Charts.Sunburst`) carrying: open action items (`IReadOnlyList<SprintActionItem>` or a slim projection), deferred open count + href (from ledger + `WorkInventory.Deferred`), optional deferred per-item summaries. **Counts rendered must equal** `ProjectCounts.OpenActionItems` and `ProjectCounts.DeferredOpenItems` — assert in tests; never `OpenActionItems.Count` at a second parse site that could drift.
  - [ ] Thread from `DashboardViewBuilder` / `DashboardView` (and epics-index path) using existing `_sprint.OpenActionItems`, `view.Counts`, `view.Work` — do not rebuild `WorkInventory` or re-parse `sprint-status.yaml` inside `Charts`.
  - [ ] Update call sites: `HtmlRenderAdapter.Dashboard.cs` (~47), `HtmlRenderAdapter.Epics.cs` (~33 project sunburst, ~195 `EpicSunburst`). Keep `TaskSunburst` untouched.
  - [ ] If `DashboardView` gains fields, keep serialization/parity green (`SectionViewModelSerializationTests` / existing dashboard JSON round-trips). Prefer optional/default-empty so zero-follow-up fixtures stay byte-stable aside from intentional geometry absence.

- [ ] **Task 2 — Project sunburst follow-up outer band (AC: #1, #2, #3)**
  - [ ] Extend `Charts.Sunburst` to draw the 4th ring per locked silhouette when open follow-ups exist; omit entirely when both ledger fields are 0 / deferred surface absent.
  - [ ] Reuse `AnnularSector`, `AppendNoPlanArc`-style dashed look (new class, don't overload `.sb-noplan` semantics), `SunburstLegend`, `Html`/`PathUtil.StripHtmlTags` helpers.
  - [ ] Update center/hint/aria as needed so the chart still reads as epic-first; follow-ups are an **additional** outer signal, not a replacement for the epic count center.
  - [ ] Deterministic angular order (epic number ascending, then file order within epic, unattributed last) — regeneration byte-identical.

- [ ] **Task 3 — Epic sunburst sibling (AC: #1, #3)**
  - [ ] Extend `Charts.EpicSunburst` with the same follow-up ring filtered to the current epic. Same CSS classes / legend rules. Degrade to no ring when that epic has zero open follow-ups.

- [ ] **Task 4 — CSS + legend (AC: #2)**
  - [ ] Add `.sb-followup-*` rules in `src/SpecScribe/assets/specscribe.css` near the existing sunburst block (~2281–2399). Dashed stroke + legend swatch that is **not** a lifecycle stage swatch. Wire `:has()` legend emphasis if the existing sunburst legend pattern extends cleanly; if not, visible label+stroke at rest is enough (never color-only).
  - [ ] Extend `StylesheetTests` for the new classes / non-color-only signal.

- [ ] **Task 5 — Guardrails: do not absorb 9.6; do not break StatCards (AC: #2)**
  - [ ] Do **not** edit `ActionItemsTemplater` / `DeferredWorkTemplater` / `FollowUpRefs` / deferred parser for this story except optional **read-only** reuse of an already-built model if SiteGenerator caches one. Geometry links are normal SVG `<a href>` — no action-items copy-payload risk.
  - [ ] Confirm `AppendWorkSummaryCards` still gates independently and still reads `counts.DeferredOpenItems` / `OpenRetroActionItems` (already ledger-backed).

- [ ] **Task 6 — Tests + golden (AC: #1, #2, #3)**
  - [ ] `ChartsTests`: sunburst emits follow-up segments + distinct classes + correct hrefs when open items exist; **no** follow-up ring / no fake wedges when ledger is zero; aria text does not say "Story" for follow-up wedges; segment count matches ledger.
  - [ ] `EpicSunburst_*`: epic-filtered follow-ups only.
  - [ ] `HtmlRenderAdapterTests` or generation E2E: home sunburst contains follow-up geometry when fixtures have open retro items / deferred open count.
  - [ ] `ProjectCountsTests` untouched unless a new assertion documents geometry-must-read-ledger (optional regression comment/test).
  - [ ] Golden fingerprint will move (dashboard + epics index + epic pages with follow-ups) → regen `SiteGeneratorAdapterTests` expected hash per `golden-diff-normalization-gotchas`. Shared `BodyHtml` path → HTML/webview/SPA stay aligned; **no** new `HostRenderException`. Confirm three `Render*ParityTests` green.
  - [ ] Run `dotnet test` from repo root.

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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

---

**Ultimate context engine analysis completed — comprehensive developer guide created.**
