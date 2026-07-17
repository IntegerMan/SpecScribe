---
baseline_commit: 525328146cbb248b660afdbcc6ea3d7204c29eb7
---

# Story 9.8: Authoring and Delivery Workflow Coherence

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer using SpecScribe to drive work,
I want the portal's create-story, next-step, empty-state, and related Driver surfaces to form one coherent workflow from requirements gathering through story creation and development,
so that the tool actively guides daily journeys rather than only reflecting completed artifacts.

## Acceptance Criteria

1.
**Given** the existing next-step command surface (Story 8.5), designed empty states (Story 8.6), and undrafted/create-story affordances
**When** this story audits the Driver path (requirements → story creation → development → review)
**Then** gaps, dead ends, and contradictory guidance are identified and closed with concrete portal changes
**And** the work extends those shipped seams rather than duplicating a parallel command/empty-state system.

2.
**Given** a maintainer starting from Home or an epic with undrafted / ready / in-progress work
**When** they follow the portal's primary recommended path
**Then** each step's primary affordance matches the lifecycle state and leads to the next sensible unit of work
**And** framework-specific commands remain adapter-supplied (NFR8) with degrade-to-absent when a step is unsupported.

3.
**Given** visual or interaction changes this story introduces
**When** create-story / implementation proceeds
**Then** owner-selected silhouette directions are elicited up front (Epic 3/7/8 visual-intent practice) and not re-litigated at review.

## Context & Scope

Epic 9 completes traceability and review follow-through. Stories **8.5** and **8.6** shipped the next-step hierarchy and designed empty states, but the Driver's daily path (User Journeys 1–2) still has dead ends and contradictory "what's next" signals. Epic 8 retro seated this story as a **holistic coherence pass** — not a new command system and not Story 9.9's satisfaction scan.

**This story closes Journey 2 guidance continuity** across Home → epic → story, and adds a thin Home focus strip so Drivers can hyper-focus without abandoning Journey 1's full pulse.

### Critical gap already confirmed (must close)

`BmadCommands.RenderProjectNextSteps` / `ForProject` is **implemented and unit-tested** (`ModuleContextTests`) but has **zero production call sites** under `src/`. Journey 2 docs assume Home "Next Steps" commands; Home today shows Now & Next + Story Pipeline **without** project next-step commands. That orphan is the #1 deliverable.

### Surfaces in scope

| Surface | Role | Change |
|---------|------|--------|
| **Home dashboard** | Journey 1 pulse + Journey 2 entry | Wire project Next Steps; add work-stage focus strip (silhouette #2); align empty/next guidance with Now & Next |
| **`BmadCommands.ForProject` / `ForEpic`** | State → primary command | Align create-story gating so Home and epic pages recommend the same next draft when undrafted work exists mid-epic |
| **Sprint empty lanes** (`SprintTemplater`) | Designed empty states (8.6) | Ready (and related) empty copy that says "draft or refine" should carry a real command badge when catalog + undrafted target exist — extend `InlineGuidance`, do not invent a second empty-state renderer |
| **Epic Up Next / Next Steps / undrafted banner** | Already shipped 8.5/8.6 | Audit for contradictory primaries; fix only where Home/epic disagree on the same undrafted target |
| **Story Pipeline / Now & Next** | State visibility | May gain a one-line pointer into the focus strip / Next Steps — **not** a new command matrix inside the funnel |

### Surfaces explicitly OUT of scope

| Surface | Why out |
|---------|---------|
| **Full tabbed Home that hides sections by mode** | Owner considered and rejected for 9.8 (silhouette #3). Defer to a later IA story if still wanted |
| **Requirement satisfaction at a glance** | Story **9.9** |
| **FR/NFR detail covering-story lists, Unmapped tier, coverage maps** | Stories **9.1–9.3** |
| **Follow-up page chrome / sunburst 4th ring** | Stories **9.6 / 9.7** |
| **Evidence strip / AC blocks / collapsed dev notes** | Stories **9.4 / 9.5** |
| **New authoring schema for workflow stages** | Forbidden — derive from existing status + `ArtifactOutputPath` + `CommandCatalog` |
| **Parallel next-step / empty-state system** | AC #1 — extend 8.5/8.6 only |
| **Webview `stageCommand` / Open-in-Terminal** | Still deferred (8.5 note / R4.3); do not reopen unless a coherence fix requires it |

### Owner-selected design direction (locked at create-story)

Elicited 2026-07-16. Three named directions were considered:

1. **Coherence pass only** — wire/align next-steps and empty states; no new Home chrome.
2. **Thin focused strip + full Overview** — pure-CSS work-stage strip that **highlights** the stage-relevant panels and promotes the matching primary command; Overview remains default; **nothing is hidden**.
3. **Full tabbed work-mode Home** — mode tabs replace the stack with hyper-focused views (hides panels). Strongest focus; breaks Journey 1 unless Overview is mandatory; high parity/golden/8.7 risk.

**Locked choice: #2 Thin focused strip + full Overview.**

Concrete silhouette rules:

- **Stages (labels locked):** `Overview` (default) · `Gather` · `Draft` · `Develop` · `Review`.
- **Chrome grammar:** Reuse `.board-tabs` / radio + `:has()` patterns from Story 8.7 (`RenderRequirementsTabs`) and sprint board tabs — **pure CSS, zero JS** (webview CSP / NFR5). Unique radio `name`/`id` prefix (e.g. `wm-overview`, `wm-gather`, …) so it never collides with `req-view` or sprint radios.
- **Overview default:** Full existing section stack remains visible and scannable (Journey 1 wins). Selecting Overview clears stage emphasis.
- **Non-Overview stages highlight, never hide:**
  - Add a stage class on a dashboard wrapper (e.g. `.dashboard[data-work-mode]` or `.dashboard.wm-gather`) driven by `:has(#wm-gather:checked)`.
  - Emphasize the stage's primary panel(s) (border/accent/scroll-anchor treatment using existing tokens — parchment/`--gold`/`journey-*` accents). Dim or de-emphasize non-focus panels **without** `display:none` / removing them from the DOM / accessibility tree.
  - Optional: `scroll-margin` + in-page hash/fragment on the strip label is allowed if it stays CSS-only and does not break golden stability across hosts.
- **Stage → panel map (locked):**

  | Stage | Emphasize | Promote command from |
  |-------|-----------|----------------------|
  | **Overview** | (none — full pulse) | Project Next Steps primary (still visible) |
  | **Gather** | Requirements panel + Planning Artifacts (when present) + Requirements journey tiles | First applicable planning command from catalog if already surfaced nearby; do **not** invent req→create-story deep links into FR pages (9.1–9.3 / 9.9 territory). Soft handoff: Requirements CTA + project next-steps when pending epic exists |
  | **Draft** | Story Pipeline + undrafted / "next to draft" Now & Next signals | `create-story {id}` (or `create-epics-and-stories` / `sprint-planning` per `ForProject`/`ForEpic`) |
  | **Develop** | Now & Next (active / ready) + Project at a Glance sunburst + Overall Progress tiles | `dev-story {id}` |
  | **Review** | Now & Next review lane + Follow-up StatCards (when present) | `code-review` (and demoted `correct-course` where 8.5 already does) |

- **Project Next Steps placement (locked):** A visible **Next Steps** panel on Home, fed by `BmadCommands.RenderProjectNextSteps` (or the same `RenderInner` fragment stored on `DashboardView`), placed **immediately above or beside Now & Next** — not buried under Git Pulse. Prefer: after sunburst, **before** Now & Next, so Journey 2 sees command then board. If that feels tall, a compact header-aside on the Now & Next panel (command primary + "Other actions") is acceptable **only if** it still uses `RenderInner` / primary+alternates CSS from 8.5 (`.next-steps-primary` / `.next-steps-alternates`).
- **Never color-only** for stage identity: strip labels are words; emphasis uses border/weight/opacity, not hue alone.
- **NFR8:** If `Commands` lacks a step, degrade that suggestion to absent (existing `Add` discipline). If epics model is null, omit Next Steps and omit stages that have nothing to emphasize (or keep Overview-only strip — prefer omit empty stages rather than empty emphasized panels).
- **Do not** absorb Stakeholder/Reviewer persona switching — stages are **Driver work modes**, not audience modes.

## Tasks / Subtasks

- [x] **Task 1 — Audit matrix + close documented contradictions (AC: #1, #2)**
  - [x] Produce a short in-story or test-comment matrix: surface × lifecycle state × primary affordance × destination for Home / epic / story / empty Ready lane. Use it to drive fixes; keep the matrix in Dev Notes or a focused test so regressions are visible.
  - [x] Confirm and fix: Home missing project Next Steps; Ready empty lane copy without command; `ForProject` create-story only under `EpicStatus.Drafted` while `ForEpic(active)` still offers create-story for next undrafted — **align Home with epic** so mid-epic undrafted work is recommended from Home too (extend `ForProject`, do not weaken `ForEpic`).
  - [x] Audit epic Up Next card vs epic Next Steps primary: if Up Next spotlights an undrafted story while primary is only `sprint-status`, ensure create-story is not buried when that undrafted story is the coherent next draft (prefer promoting create-story when the Up Next target is undrafted — extend 8.5, don't fork a second epic command picker).

- [x] **Task 2 — Wire `RenderProjectNextSteps` into Home (AC: #1, #2)**
  - [x] Thread via `DashboardView` / `DashboardViewBuilder` (opaque HTML fragment **or** structured suggestions if you can stay host-neutral — prefer matching how epic/story already carry `NextStepsHtml` on `EpicsView` for consistency).
  - [x] Call from `HtmlRenderAdapter.Dashboard.cs` next to Now & Next per locked placement.
  - [x] Omit panel when catalog yields zero suggestions or epics model is null (byte-load-bearing conditional).
  - [x] Keep HTML/webview/SPA on shared `RenderDashboardBody` — no host fork.

- [x] **Task 3 — Work-stage focus strip on Home (AC: #2, #3)**
  - [x] Implement silhouette #2 exactly: Overview default; Gather / Draft / Develop / Review highlight maps above; pure-CSS radios + `:has()`; no section hiding.
  - [x] CSS in `assets/specscribe.css` near existing `.board-tabs` / dashboard panel rules. Reuse journey accents where they already mark tile groups — do not invent a second color system.
  - [x] `StylesheetTests`: assert new classes / non-color-only stage labels exist.
  - [x] Dashboard tests: default Overview shows full stack; selecting Draft emphasizes pipeline + next-steps create-story path without removing Now & Next from DOM.

- [x] **Task 4 — Empty-state command continuity (AC: #1, #2)**
  - [x] Extend `SprintTemplater` empty Ready (and any other lane whose copy already implies drafting) to use `BmadCommands.InlineGuidance` + catalog when an undrafted target is knowable from the board's epics model. When no command/target, keep designed copy (8.6) — never a wrong badge.
  - [x] Do not regress `.sprint-lane-empty` / `.epic-undrafted-banner` styling.

- [x] **Task 5 — Guardrails (AC: #1)**
  - [x] Do **not** reimplement `ForStory` matrices; only fix project/epic alignment holes.
  - [x] Do **not** start 9.9 satisfaction chrome or re-open 9.1–9.3 page deliverables.
  - [x] Do **not** put create-story commands inside Story Pipeline wedges (funnel stays status geometry; commands live in Next Steps / empty states / undrafted banner).
  - [x] Sunburst create-story tooltips on no-plan arcs may stay; optional one-line cross-reference from Draft stage emphasis is fine.

- [x] **Task 6 — Tests + golden (AC: #1, #2, #3)**
  - [x] Extend `ModuleContextTests` / new dashboard tests: Home HTML contains project Next Steps when fixtures have review/ready/undrafted work; absent when catalog empty.
  - [x] `ForProject` mid-epic undrafted: create-story appears (aligned with `ForEpic`).
  - [x] Empty Ready lane: command badge present when undrafted target + catalog allow; absent otherwise.
  - [x] Focus strip markup present; Overview radio checked by default; emphasis classes exist; no `display:none` on sibling panels for non-Overview stages.
  - [x] Golden fingerprint will move (home body) → regen `SiteGeneratorAdapterTests` expected hash per `golden-diff-normalization-gotchas`. Confirm three `Render*ParityTests` green.
  - [x] Run `dotnet test` from repo root.

## Dev Notes

### Why this exists (product gap)

From Epic 8 retro: holistic **authoring/delivery workflow** (req gathering → create-story → development) belongs in Epic 9 as Story 9.8. Epic 8 made status trustworthy and shipped next-steps/empty states, but did not prove those seams form one Driver path from Home. Journey 2 still ends with "copy the right command" — Home currently cannot.

### Current code reality (read before editing)

```78:79:src/SpecScribe/BmadCommands.cs
    public static string RenderProjectNextSteps(EpicsModel model, CommandCatalog commands) =>
        RenderPanel(ForProject(model, commands));
```

Production call sites for `RenderProjectNextSteps`: **none** (tests only).

```51:55:src/SpecScribe/HtmlRenderAdapter.Dashboard.cs
        // Now & Next: sprint board when tracked, else the derived cards; omitted entirely when the view is null.
        if (view.NowNext is { } nowNext)
        {
            AppendNowAndNext(sb, nowNext, view.Epics, view.Counts);
        }
```

```423:462:src/SpecScribe/BmadCommands.cs
    // ForProject — create-story only when EpicStatus.Drafted + undrafted story.
    // ForEpic(active) — still offers create-story for next undrafted. Align these.
```

```30:37:src/SpecScribe/SprintTemplater.cs
    // EmptyLaneCopy("ready") says "draft or refine" but emits no InlineGuidance badge today.
```

Home section order today: tile band → sunburst → Now & Next → Story Pipeline → Git Pulse → Planning Artifacts → Requirements → Progress by Epic.

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Story/epic/project command matrices | `BmadCommands.ForStory` / `ForEpic` / `ForProject`, `RenderInner`, `InlineGuidance`, `RenderCommandBadge` | `BmadCommands.cs` |
| Command presence | `CommandCatalog.Command(step, id?)` via `Add` | `ModuleContext.cs` |
| Epic undrafted banner / cards | `EpicsViewBuilder.RenderUndraftedBanner`, `BuildStoryCard` | `EpicsViewBuilder.cs` |
| Sprint board + empty lanes | `SprintTemplater.RenderBoard`, `EmptyLaneCopy` | `SprintTemplater.cs` |
| Pure-CSS tabs grammar | `RenderRequirementsTabs` / `RenderBoardTabs` | `HtmlRenderAdapter.Dashboard.cs`, `SprintTemplater.cs` |
| Primary/alternate next-steps CSS | `.next-steps-primary`, `.next-steps-alternates` | `assets/specscribe.css` |
| Status vocabulary | `StatusStyles.ForStory` / `ForEpicWithRetrospective` | `StatusStyles.cs` |
| Dashboard composition | `DashboardView` / `DashboardViewBuilder` / `AppendDashboardSection` | Dashboard*.cs, HtmlRenderAdapter.Dashboard.cs |

### Guardrails & invariants

- **Extend 8.5/8.6 — never fork.** One primary per state; designed empties; adapter-supplied commands.
- **NFR8 degrade-to-absent** for missing catalog steps and unsupported framework workflows.
- **Journey 1 wins design conflicts** — Overview default; no hiding Home pulse panels.
- **Story 8.7 kinship** — focus strip is a *stage lens*, not a second rendering of the same dataset. Do not re-duplicate Requirements Flow vs Status grid.
- **Shared BodyHtml path** — HTML + webview + SPA stay byte-aligned; no new `HostRenderException`.
- **No new `--status-*` token**; no new authoring schema.
- **Split, don't absorb 9.9 / 9.1–9.3 / 9.6–9.7.**
- **Golden moves on purpose** — regen fingerprint; don't fight it.
- **Webview CSP:** pure CSS only for strip behavior; copy badges still rely on existing `specscribe.js` / native fallbacks already accepted in Epic 8.

### Previous story intelligence

- **9.7** parked "Story Pipeline / Now & Next command surface" explicitly on **9.8** — this is that work.
- **8.5** established primary vs "Other actions" and demoted `correct-course`; do not re-litigate done-story celebration panels.
- **8.6** consolidated undrafted banners and sprint empty lanes — extend with badges where copy already implies an action.
- **8.7** proved pure-CSS `:has()` radios work in webview — copy that pattern for the focus strip.
- Epic 8 process: silhouettes elicited at create-story survive review better than post-hoc redesign.

### Git intelligence

Recent commits (`7589495`, `65756cd`, `1a9e8dd`, `3c50269`) moved Home layout (sunburst-first, follow-up visibility, panel tweaks). This story should treat Home composition as intentional and **insert** Next Steps + focus strip without reshuffling the whole stack again unless required for Journey 2.

### Project Structure Notes

- Primary edits: `BmadCommands.cs`, `HtmlRenderAdapter.Dashboard.cs`, `DashboardView.cs`, `DashboardViewBuilder.cs`, `SprintTemplater.cs`, `assets/specscribe.css`, tests under `tests/SpecScribe.Tests/`.
- Possible light touch: `EpicsViewBuilder.cs` if Up Next vs Next Steps primary alignment needs a one-line priority tweak.
- Likely **no** new top-level feature folder; keep strip helpers as private methods on the dashboard adapter (mirror `RenderRequirementsTabs`).

### Testing standards

- xUnit; `Assert.Contains` / `DoesNotContain` on HTML strings.
- Pin: Home Next Steps wired; ForProject/ForEpic create-story alignment; empty-lane badge degrade; focus strip Overview default + no panel removal; golden + three parity suites.
- Full `dotnet test` green.

### Verify before marking review

Generate to `SpecScribeOutput/` (not `docs/live`). Open **Home**: Overview shows full pulse; Next Steps shows a primary command matching the project's front line; Draft stage emphasizes pipeline + create-story path without hiding Now & Next; Develop emphasizes active work + `dev-story`; Review emphasizes review + `code-review` when applicable. Open an **epic** with undrafted stories mid-flight — Home and epic recommend the same next draft id. Empty Ready lane shows a command badge when undrafted work exists. `dotnet test` green.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.8] — user story + ACs
- [Source: _bmad-output/planning-artifacts/epics.md#Story 8.5 / 8.6 / 8.7] — seams to extend
- [Source: _bmad-output/implementation-artifacts/epic-8-retro-2026-07-15.md] — why 9.8 was seated; visual-intent rule
- [Source: _bmad-output/implementation-artifacts/9-7-open-follow-ups-in-the-remaining-work-geometry.md] — parks Pipeline/Now & Next commands on 9.8
- [Source: docs/UserJourneys.md] — Journeys 1–2 win design conflicts
- [Source: src/SpecScribe/BmadCommands.cs] — ForStory / ForEpic / ForProject / RenderProjectNextSteps
- [Source: src/SpecScribe/HtmlRenderAdapter.Dashboard.cs] — home composition + requirements tabs pattern
- [Source: src/SpecScribe/SprintTemplater.cs] — empty lanes + board tabs
- [Source: src/SpecScribe/DashboardView.cs] / DashboardViewBuilder.cs — host-neutral home VM
- [Source: tests/SpecScribe.Tests/ModuleContextTests.cs] — RenderProjectNextSteps coverage (orphan today)

## Dev Agent Record

### Agent Model Used

Composer (Auto)

### Debug Log References

### Completion Notes List

- Wired orphaned `BmadCommands.RenderProjectNextSteps` into Home via `DashboardView.NextStepsHtml` (built in `DashboardViewBuilder`, rendered before Now & Next on shared `RenderDashboardBody`).
- Aligned `ForProject` undrafted scan with drafted/ready/active epics; promoted `ForEpic(active)` create-story to primary when Up Next would spotlight undrafted (no front line).
- Added silhouette #2 work-stage focus strip (Overview · Gather · Draft · Develop · Review) — pure-CSS `wm-*` radios + `:has()` opacity/border emphasis; never hides panels.
- Extended Ready empty lane with `InlineGuidance` create-story badge when undrafted + catalog allow (NFR8 degrade otherwise).
- Coherence matrix pinned as test comments in `BmadCommandsTests`; golden fingerprint regenerated; **1263** tests green.

### File List

- src/SpecScribe/BmadCommands.cs
- src/SpecScribe/DashboardView.cs
- src/SpecScribe/DashboardViewBuilder.cs
- src/SpecScribe/HtmlRenderAdapter.Dashboard.cs
- src/SpecScribe/SprintTemplater.cs
- src/SpecScribe/assets/specscribe.css
- tests/SpecScribe.Tests/ModuleContextTests.cs
- tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs
- tests/SpecScribe.Tests/SprintTemplaterTests.cs
- tests/SpecScribe.Tests/StylesheetTests.cs
- tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs
- _bmad-output/implementation-artifacts/9-8-authoring-and-delivery-workflow-coherence.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-07-16: create-story — ready-for-dev; owner locked silhouette #2 (thin work-stage focus strip + Overview default); documented orphaned `RenderProjectNextSteps` as primary gap.
- 2026-07-16: implemented — Home Next Steps + work-mode strip + ForProject/ForEpic alignment + Ready-lane InlineGuidance; status → review.
