---
baseline_commit: 8fa1c1d8380daae77cec84f4ba66da9c5179a211
---

# Story 2.1: Accurate Work Representation and Authoring Guidance

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer using multiple BMad workflows,
I want the portal to represent all work types accurately and to guide me in adding more,
so that deferred items and quick-dev work stay visible and new contributors know how to extend the plan.

## Acceptance Criteria

1. **Given** the project contains deferred-work notes and quick-dev spec artifacts alongside epics, stories, and tasks
   **When** the site is generated
   **Then** those work items are represented as first-class, navigable entries with their status
   **And** task and progress figures account for them without misrepresenting epic or story completion.

2. **Given** an epics or stories surface (including empty or partial states)
   **When** I view it
   **Then** clear inline guidance explains how to add an epic or a story, with the relevant commands
   **And** sunburst and task visuals distinguish "no plan yet" from "no data" so gaps read as next actions.

> **Origin & scope:** This is the first story of Epic 2 (Complete and Faithful BMad Artifact Representation).
> It picks up the two truthfulness items **Story 1.5 explicitly routed here** — the "Progress by Epic"
> delivery mosaic (`[UXO A6]`) and the unplanned-story placeholder arc (`[UXO E4]`) — and adds the
> work-type representation (quick-dev / deferred work) and inline authoring guidance that neither Story 1.4
> nor 1.5 touch. **Sequence this after Story 1.5** (see Previous Story Intelligence): 1.5 lands the
> one-token-per-lifecycle-stage color system that A6/E4 must reuse. Do not reintroduce per-chart color
> literals here.

## Tasks / Subtasks

- [x] Task 1: Model quick-dev + deferred work as first-class work types (AC: #1)
  - [x] **Identify the artifacts by convention, and disambiguate from Story 2.2.** Quick-dev artifacts are `implementation-artifacts/spec-*.md` files carrying frontmatter `route: one-shot` (plus `type`, `status`, `created`) — the outputs of the `bmad-quick-dev` workflow. The deferred-work note is `implementation-artifacts/deferred-work.md` (no frontmatter; a bulleted list grouped by `## Deferred from: …`). **These are NOT the spec kernel** in `_bmad-output/specs/spec-specscribe/*.md` (SPEC.md, ARCHITECTURE-SPINE.md, rendering-architecture.md, requirements-catalog.md, settings-and-signals.md) — that kernel is **Story 2.2's** domain; do not build kernel handling here. [Source: `_bmad-output/implementation-artifacts/spec-readme-supported-frameworks-table.md:1-7`, `_bmad-output/implementation-artifacts/deferred-work.md:1-5`]
  - [x] Capture the quick-dev identity. `Frontmatter` is a fixed-field record (`Title/Project/Date/Author/Version/Status` only) — `route` and `type` are parsed into the YAML `map` in `MarkdownConverter` but then dropped. Extend `Frontmatter` with `Route` (and optionally `Type`) via the existing `GetString(map, "route")` seam so a doc can be classified as quick-dev/one-shot; keep every field optional (BMad docs vary). [Source: `src/SpecScribe/Frontmatter.cs`, `src/SpecScribe/MarkdownConverter.cs:143-152`]
  - [x] Build a lightweight work-inventory (recommended: a small model computed in `SiteGenerator` where `_docs` is already populated, or a helper alongside `ProgressCalculator`). It should expose: the quick-dev entries (title, output path, status) and the deferred-work entry (output path + a count of open items parsed from its `- ` bullets). Keep it non-fatal: a missing/partial/empty file yields an empty inventory, never an exception (NFR2).
- [x] Task 2: Surface the work types as first-class, navigable index entries with status (AC: #1)
  - [x] Today `spec-*.md` and `deferred-work.md` render as plain cards in the generic **"Implementation Artifacts"** group of the home index (via `GenerateOneInternal` → `_docs` → `RenderIndex` grouping) — navigable but undifferentiated, with status shown only as flat text in the card `<p>`. Give them a **dedicated, labeled section** (e.g. "Direct & Quick-Dev Work" and a deferred-work callout) so they read as distinct work classes, and show each one's status as a **status badge** using the site's status semantics (`status-badge`/`pill` + `StatusStyles`), not plain text. [Source: `src/SpecScribe/HtmlTemplater.cs:58-120`, `:374-391`]
  - [x] Avoid double-listing: once an artifact is promoted into the dedicated section, exclude it from the generic "Implementation Artifacts" grid (mirror how README is kept out of `_docs`, or filter it out of the grouping loop). Keep the generated standalone page for each so it stays navigable. [Source: `src/SpecScribe/HtmlTemplater.cs:80-112`, `SiteGenerator.cs:447-466` README precedent]
  - [x] Preserve Story 1.1's graceful omission: when there are no quick-dev/deferred artifacts, the section is omitted cleanly (no empty header, no broken link).
- [x] Task 3: Account for the new work in progress figures WITHOUT misrepresenting epic/story completion (AC: #1)
  - [x] The dashboard stat cards + "Overall Progress" bars tally **only** epics/stories/tasks from `epics.md` today. Add the quick-dev/deferred counts as a **separate** signal (e.g. a "Direct changes" stat card and/or a small count in the new section) so the whole-project picture is complete — but **never** fold them into `TasksDone/TasksTotal`, `StoriesTotal`, or the epic roll-up. [Source: `src/SpecScribe/HtmlTemplater.cs:122-144`, `src/SpecScribe/ProgressModel.cs`, `src/SpecScribe/ProgressCalculator.cs`]
  - [x] **Hard regression guard:** the existing epic/story/task numbers and their denominators must be byte-for-byte unchanged (Story 1.5's `[UXO A5]` task-stat reframe — "N/N planned tasks", "across X of Y stories" — must survive verbatim). Prove it with a test asserting the epic/story tallies are unaffected by the presence of quick-dev/deferred artifacts.
  - [x] If you extend `ProgressModel`/`EpicProgress`, keep `ProgressModel.Empty` valid and all existing `required` initializers satisfied.
- [x] Task 4: `[UXO A6]` "Progress by Epic" mosaic shows real delivery status, not "stories detailed" (AC: #2 truthfulness)
  - [x] `Charts.EpicMosaic` currently draws a two-segment ring of Detailed vs Not-detailed (`StoriesWithArtifact`/`StoryCount`), so a **mid-development** epic renders a full gold ring and reads as "complete." Change the ring to segment by **per-story delivery status** (done / active / review / ready / drafted / pending), same palette + tokens as the sunburst (`StatusStyles.ForStory`), and keep "N/N detailed" as the **sub-label only**. [Source: `src/SpecScribe/Charts.cs:366-398`]
  - [x] The mosaic needs per-status story counts per epic. `EpicProgress` today carries only `StoryCount`/`StoriesWithArtifact` (no per-status breakdown). Either add a per-status tally to `EpicProgress` (computed in `ProgressCalculator` from `StatusStyles.ForStory`) or have the mosaic take the `EpicInfo`/story list. Pending epics (no stories) keep the empty ring + "Not yet drafted" — never a misleading 0%/full fill. [Source: `src/SpecScribe/ProgressModel.cs:3-12`, `src/SpecScribe/ProgressCalculator.cs:38-47`, `src/SpecScribe/Charts.cs:374-393`]
  - [x] This mosaic is rendered in **two** places — the home dashboard (`HtmlTemplater.AppendDashboard` → `Charts.EpicMosaic`, `HtmlTemplater.cs:187-192`) and the epics index (`EpicsTemplater.AppendProgressPanel` → `Charts.EpicMosaic`, `EpicsTemplater.cs:394-396`). Fixing `Charts.EpicMosaic` fixes both; verify both surfaces.
- [x] Task 5: `[UXO E4]` Sunburst distinguishes "no task plan yet" from "no data" with a placeholder arc (AC: #2)
  - [x] Today the outer task ring is drawn **only when `story.TasksTotal > 0`**, so a story with no plan and a story off the edge of the data look identical. For a story with `TasksTotal == 0`, render a **faint dashed placeholder arc** in the task ring with a tooltip/aria-label like **"No task plan yet — run /bmad-create-story N.N"**, turning the gap into a call to action. Apply to both `Charts.Sunburst` (project) and `Charts.EpicSunburst` (per-epic). [Source: `src/SpecScribe/Charts.cs:156-172`, `:254-269`]
  - [x] Pure SVG + CSS only — this is NOT the JS drill engine ([[charting-is-pure-svg-no-js]]). The dashed arc is a styled `<path>`; keep a `<title>` and (once Story 1.5's tooltip script is present) the `data-*` tooltip hook, with `<title>`/`aria-label` as the no-JS fallback. Keep the story segment a real link so the placeholder is still clickable to the story/epic. [Source: `src/SpecScribe/Charts.cs:99-199`]
  - [x] Keep whole-chart `role="img"` aria-label truthful and preserve Story 1.4's segment `aria-label`s. The `sunburst-hint` line and legend stay; the placeholder makes the "outer ring = task completion" statement true even where a plan is missing.
- [x] Task 6: Inline authoring guidance on epics/stories surfaces, including empty and partial states (AC: #2)
  - [x] Route all commands through the existing module-aware seam so they match the detected module (`/bmad-*` vs `/gds-*`) and are **omitted** when not installed: `CommandCatalog.Command("create-epics-and-stories")`, `Command("create-story", "N.N")`. Reuse/extend `BmadCommands` rather than hardcoding command strings. [Source: `src/SpecScribe/BmadCommands.cs`, `src/SpecScribe/ModuleContext.cs:43-51`]
  - [x] **Empty state:** `EpicsTemplater.RenderIndex` has no empty-state today (a zero-epic model still emits headers + an empty sunburst). When `model.Epics.Count == 0`, render guidance on how to add the first epic (`create-epics-and-stories`). Note the home page's `epics.html` link itself only appears when `epics.md` exists (`SiteNav` gate) — the empty-epics case is "epics.md exists but has no epics." [Source: `src/SpecScribe/EpicsTemplater.cs:9-64`, `src/SpecScribe/SiteNav.cs:78-86`]
  - [x] **Partial states — convert dead-end notes into next actions:** `AppendEpicCard`'s "Stories not yet drafted." (`EpicsTemplater.cs:426-429`) and `AppendStoryCard`'s "No detailed story plan yet." (`:363-366`) currently state a gap without the command to close it. Pair each with the relevant guidance (`create-epics-and-stories` for a pending epic; `create-story N.N` for an undrafted story). Keep it de-emphasized so it reads as help, not clutter. `AppendUpNextCard`'s "Not yet drafted" (`:471-496`) is deliberately left untouched: it sits beside the Next Steps panel, which already surfaces the same `create-story` command, so pairing it there too would duplicate the CTA in one panel.
  - [x] Guidance must degrade to nothing when the module exposes no such command (never print a command that doesn't exist) — the same rule `BmadCommands.Add` already enforces.
- [x] Task 7: Test coverage (AC: #1, #2)
  - [x] `ChartsTests`: `EpicMosaic` renders per-status segments from the story roll-up (a mid-dev epic is NOT a full ring); pending epic keeps the empty ring + "Not yet drafted"; `Sunburst`/`EpicSunburst` emit a dashed placeholder arc with the "No task plan yet" tooltip when `TasksTotal == 0` and still keep the story link + segment aria-labels. [Source: `tests/SpecScribe.Tests/ChartsTests.cs`]
  - [x] `HtmlTemplaterTests` (render-level string assertions, the house pattern): quick-dev + deferred artifacts appear in their own labeled section with a status badge and are absent from the generic "Implementation Artifacts" grid; the work-count stat is present; **epic/story/task tallies are unchanged** when quick-dev/deferred docs are present. [Source: `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`]
  - [x] Epics-surface guidance: `EpicsTemplater`/`BmadCommands` tests that empty and partial states emit the create-epic/create-story command when the module exposes it and emit nothing when it does not. Prefer generation-/render-level assertions over new public API.
  - [x] If a frontmatter `Route` field is added: a `MarkdownConverterTests` case that `route:` is parsed (and absent → null).
- [x] Task 8: End-to-end validation with a real generation pass (AC: #1, #2)
  - [x] Run the focused test filter, then a real generation pass against this repo (it contains 5 `spec-*.md`, a `deferred-work.md`, epics in every state, and stories with and without plans — a live fixture for every branch).
  - [x] Manually verify on `docs/live/index.html` + `epics.html`: quick-dev/deferred work shows as first-class with status; the whole-project counts include them without changing epic/story numbers; the epic mosaic shows Epic 1 mid-development (not a full ring); undrafted stories show a dashed placeholder arc with the create-story CTA; empty/partial epic surfaces show the add-epic/add-story guidance.

### Review Findings

- [x] [Review][Patch] New heatmap quick-dev spec is missing `route: one-shot` frontmatter, so `WorkInventory` will never classify it as quick-dev, contradicting its own completion notes' verification claim [_bmad-output/implementation-artifacts/spec-commit-heatmap-contrast-and-day-drilldown.md]
- [x] [Review][Patch] `EpicProgress.StoryStatusCounts` is not `required` unlike its sibling properties — a future caller that omits it gets a silently empty mosaic ring for a mid-dev epic, defeating the "no misleading empty ring" guarantee [src/SpecScribe/ProgressModel.cs:16]
- [x] [Review][Patch] `WorkInventory.Build` classifies quick-dev/deferred files by filename only with no check they live under `implementation-artifacts/`, contradicting the story's own stated convention [src/SpecScribe/WorkInventory.cs:34-49]
- [x] [Review][Patch] `WorkInventory.Build` silently last-write-wins if more than one `deferred-work.md` exists, with no warning or aggregation [src/SpecScribe/WorkInventory.cs:41-44]
- [x] [Review][Patch] `CountOpenItems` naively counts `<li`/`<del` across the whole rendered body, which over/under-counts with nested lists or unrelated strikethrough [src/SpecScribe/WorkInventory.cs:63-73]
- [x] [Review][Patch] Dev notes and the new Story 2.4 spec repeat the known-stale `--output docs/live` command as the "verified" generation target, contradicting the project's actual output directory convention [story completion notes; Story 2.4 spec]
- [x] [Review][Patch] `Charts.Sunburst`/`EpicSunburst` insert the new `commands` parameter before `size` instead of appending at the end — a latent breaking-change risk for future positional callers [src/SpecScribe/Charts.cs:126, :268]
- [x] [Review][Patch] `StatusStyles.StoryStages` lists `"pending"` as a valid stage but `ForStatus` can never return it — dead/unreachable stage in the mosaic segment logic [src/SpecScribe/StatusStyles.cs:46]
- [x] [Review][Patch] Task 6's own subtask names `AppendUpNextCard`'s "Not yet drafted" case as one of three sites to convert to guidance; only two were touched, yet Task 6 is checked complete without reflecting the deviation [story file Task 6]

## Developer Context Section

### Epic Context and Business Value

Epic 2 — "Complete and Faithful BMad Artifact Representation" — makes the portal reflect the **whole**
project, not just epics and stories. Story 2.1 is its opener and its truthfulness anchor: it (a) surfaces
the two work classes the portal currently hides or under-represents — quick-dev/one-shot changes and
deferred-work notes — as first-class, navigable, status-bearing entries; (b) finishes the dashboard-honesty
arc Story 1.5 started by making the "Progress by Epic" mosaic show real delivery (not "stories detailed")
and the sunburst distinguish "no plan yet" from "no data"; and (c) turns every empty/partial planning
surface into a signposted next action with the exact command to run. Advances FR2 (first-class BMad
support), FR5 (coherent dashboards + complete artifact-class representation), and the truthfulness/never-
color-only conventions (UX-DR17). Later Epic 2 stories build on this: specs kernel (2.2), sprint status
(2.3), planning grouping (2.4), iconography (2.5), comment annotations (2.6).

### Story Foundation Extract

- **Primary concern:** truthful, complete work representation — no work type invisible, no chart overstating
  progress, and every gap rendered as a next action rather than a dead end.
- **User outcome:** a maintainer sees quick-dev and deferred work as real, tracked work with status; trusts
  that the epic mosaic and sunburst mean what they show; and, from any empty/partial surface, knows the one
  command that moves the plan forward.
- **Success boundary:** built on the static-HTML + pure-SVG-links substrate; reuses Story 1.5's status
  tokens and (if present) its single sanctioned tooltip script; adds no new JS engine.
- **Regression boundary:** epic/story/task tallies (incl. Story 1.5's A5 reframe) are unchanged; Story 1.4's
  accessibility (focus, aria-labels, reduced-motion, skip link/landmark/progressbar) is preserved and
  extended, never undone; 1.1–1.3 behavior preserved; antiquarian visual identity preserved.

### Current Implementation Reality (READ THIS FIRST)

- **Generation flow:** `SiteGenerator.GenerateAll` scans `_bmad-output/**/*.md`; `epics.md` and any matched
  `implementation-artifacts/N-M-*.md` story artifacts are consumed into the epics/story pages; **everything
  else** (including `spec-*.md` and `deferred-work.md`) renders as a generic standalone page via
  `GenerateOneInternal` and lands in `_docs`, which feeds the home index grid. [Source: `src/SpecScribe/SiteGenerator.cs:35-110`, `:392-430`, `:432-438`]
- **Quick-dev + deferred artifacts render TODAY as plain cards** in the generic "Implementation Artifacts"
  group (`RenderIndex` groups by path prefix; `implementation-artifacts/` is a named group). Status shows as
  flat text in the card body, not a badge. They are counted in **no** progress figure. [Source: `src/SpecScribe/HtmlTemplater.cs:58-120`, `:374-391`]
- **Frontmatter is a fixed-field record.** `MarkdownConverter` fully parses the YAML into a `map`, then
  copies only `title/project/date/author/version/status` into `Frontmatter`. `route`/`type` are available in
  `map` but dropped. Extending is a one-line `GetString(map, "route")` per field. [Source: `src/SpecScribe/Frontmatter.cs`, `src/SpecScribe/MarkdownConverter.cs:143-152`]
- **`ProgressCalculator` tallies only epics/stories/tasks** from `epics.md` + resolved artifacts, and
  side-effects each `StoryInfo`'s `TasksDone/TasksTotal/Status`. `EpicProgress` carries counts only, **no
  per-status story breakdown** — which A6 needs. [Source: `src/SpecScribe/ProgressCalculator.cs`, `src/SpecScribe/ProgressModel.cs`]
- **`StatusStyles` is the single status→stage source** (`ForStory`/`ForEpic`/`ForRequirement`), returning the
  css class per lifecycle stage (`done`/`review`/`active`/`ready`/`drafted`/`pending`). A6 and the placeholder
  arc route through it — no new status logic. [Source: `src/SpecScribe/StatusStyles.cs`]
- **`Charts.EpicMosaic` shows "detailed", not delivery** — a two-segment ready/pending ring, so a mid-dev
  epic is a full gold ring reading as "complete." [UXO A6] [Source: `src/SpecScribe/Charts.cs:366-398`]
- **`Charts.Sunburst`/`EpicSunburst` draw the outer task ring only when `TasksTotal > 0`** — absence is
  ambiguous (no-plan vs no-data). [UXO E4] [Source: `src/SpecScribe/Charts.cs:156-172`, `:254-269`]
- **Epics surfaces have partial-state notes but no guidance.** `EpicsTemplater` says "Stories not yet
  drafted." / "No detailed story plan yet." / "Not yet drafted" without the command to fix it; `RenderIndex`
  has no zero-epic empty state. [Source: `src/SpecScribe/EpicsTemplater.cs:9-64`, `:363-366`, `:426-429`, `:471-496`]
- **Commands are module-aware and self-omitting.** `BmadCommands` builds status→step suggestions and
  `CommandCatalog.Command(step, arg)` returns null when the module lacks that skill, so callers skip it. Use
  this for all authoring guidance; never hardcode `/bmad-*`. [Source: `src/SpecScribe/BmadCommands.cs`, `src/SpecScribe/ModuleContext.cs:19-52`]
- **Already correct (verify, don't rebuild):** `EpicMosaic` already handles pending epics (empty ring, "Not
  yet drafted"); every `Charts` builder degrades to `.chart-empty`; `AppendStoryCard` already renders a
  `MiniDonut` + task badge for planned stories.

### Scope Boundaries

- **IN (this story):** quick-dev (`spec-*.md`, `route: one-shot`) + `deferred-work.md` as first-class,
  status-bearing, navigable entries; whole-project progress that includes them without inflating epic/story
  numbers; A6 delivery mosaic; E4 placeholder arc; inline add-epic/add-story guidance on empty & partial
  epics/stories surfaces.
- **OUT — spec KERNEL rendering** (`_bmad-output/specs/spec-specscribe/*` first-class section, TOC, cross-refs)
  → **Story 2.2**. Do not conflate the quick-dev `spec-*.md` files with the kernel.
- **OUT — sprint-status page/widget** → Story 2.3; **planning-artifact grouping + PRD prominence + status
  badges on planning docs** → Story 2.4; **iconography** → Story 2.5; **comment annotations** → Story 2.6.
- **OUT — JS drill/zoom/hash sunburst engine, dark mode/theme toggle, velocity/deep-git analytics, richer
  interactive legends/flashy insight visuals** (Epic 3 / already-scoped-out guardrails).
- **DEPENDENCY, not scope:** the status-token system, one sanctioned tooltip/copy script, and the
  epic-status-donut/heatmap/meta polish all belong to **Story 1.5** — reuse them, don't rebuild them, and
  don't re-fix 1.5's items here.

### Previous Story Intelligence

- **Sequence 2.1 after Story 1.5 and rebase onto it.** Story 1.5 (currently `ready-for-dev`) introduces one
  CSS variable per lifecycle stage and routes every chart/legend/badge through it; it *explicitly deferred
  A6 and E4 to this story* on the assumption those tokens exist. The A6 mosaic and E4 arc must consume 1.5's
  per-status tokens — **do not reintroduce per-chart color literals.** If 1.5 has not landed when you start,
  coordinate: either rebase after it or land the tokens as part of this work, but keep "one token per stage."
  [Source: `_bmad-output/implementation-artifacts/1-5-dashboard-insight-polish-and-visual-truthfulness.md:64`, `:116`, `:194`]
- Story 1.4 (`review`) owns the accessibility floor (focus-visible, whole-chart/segment aria-labels,
  reduced-motion `reduce` block, skip link/landmark/progressbar). Build on those seams; the placeholder arc's
  aria-label and the mosaic's status text must keep status **never color-only** (UX-DR17).
- Keep behavior in the central seams (`Charts`, `HtmlTemplater`, `EpicsTemplater`, `StatusStyles`,
  `BmadCommands`, `ProgressCalculator`/`ProgressModel`, `Frontmatter`/`MarkdownConverter`) — the same
  shared-seam discipline 1.2/1.3/1.5 followed. Prefer render-level string assertions over new public API.
- Environment: use `py -3` for BMAD helper scripts on this Windows host.
- **Deferred-work heads-up:** `deferred-work.md` records an open item for a `BmadCommands.ForProject`
  retrospective-fallback edge case (a code-review-less module with a story stuck in `review`). It is **not**
  in scope here, but you are editing `BmadCommands` — don't regress it, and don't try to fix it in this
  story. [Source: `_bmad-output/implementation-artifacts/deferred-work.md:15-18`]

### Architecture Compliance

- **Status→color semantics live in one place** (`StatusStyles` + Story 1.5's tokens), consumed by every
  chart/badge — enforce it for the A6 mosaic and the new status badges rather than adding per-widget colors.
  [Source: `src/SpecScribe/StatusStyles.cs`; epics.md "status semantics everywhere"]
- **Graceful degradation is contractual (NFR2).** Missing/partial/empty quick-dev or deferred files, a
  zero-epic model, stories with no plans, and no-git all degrade to non-fatal notices / omitted sections /
  `.chart-empty` — never an exception or a broken link (Story 1.1). [Source: `src/SpecScribe/Charts.cs:106`, `:301`, `:368`; `SiteGenerator.cs` try/catch per page]
- **Host-neutral output (NFR6, future webview).** The new work-type representation, placeholder arc, and
  guidance are static HTML/SVG/CSS + `data-*`/aria attributes — no host-specific behavior, so the Epic 6
  webview inherits them. [Source: `_bmad-output/specs/spec-specscribe/rendering-architecture.md`; ADR 0002/0004]
- **Self-contained packaging.** Any CSS added for the placeholder arc / status section ships in the embedded
  `specscribe.css` (the asset-copy seam in `SiteGenerator.EnsureScaffold`), not loose files. No third-party
  deps. [Source: `src/SpecScribe/SiteGenerator.cs:500-509`]

## Technical Requirements

- Classify quick-dev artifacts by `implementation-artifacts/spec-*.md` + frontmatter `route: one-shot`
  (extend `Frontmatter` with `Route`); classify the deferred note by filename `deferred-work.md`. Never
  confuse either with the spec kernel (Story 2.2).
- Surface both as a dedicated, labeled, navigable index section with per-entry **status badges** (site status
  semantics), excluded from the generic "Implementation Artifacts" grid; omitted cleanly when absent.
- Whole-project progress includes a **separate** quick-dev/deferred signal; epic/story/task tallies and
  denominators are **unchanged** (Story 1.5's A5 reframe preserved verbatim).
- `Charts.EpicMosaic` ring segments by per-story delivery status (`StatusStyles.ForStory`, Story 1.5 tokens),
  "N/N detailed" demoted to sub-label; pending epics keep the empty ring. Fix once in `Charts`; verify home +
  epics surfaces.
- `Charts.Sunburst`/`EpicSunburst` render a faint dashed placeholder outer arc for `TasksTotal == 0` stories
  with a "No task plan yet — run /bmad-create-story N.N" tooltip/aria, keeping the story link and no-JS
  fallback; whole-chart + segment aria-labels stay truthful.
- Inline add-epic/add-story guidance on empty and partial epics/stories surfaces, via the module-aware
  `BmadCommands`/`CommandCatalog` seam (self-omitting when the command doesn't exist); dead-end notes become
  next actions.
- No JS drill engine; no dark mode/theme; no velocity/deep-git; host-neutral, static-host-safe; Story 1.4
  accessibility intact.

## File Structure Requirements

Primary UPDATE candidates:

- `src/SpecScribe/Charts.cs` — `EpicMosaic` per-status delivery ring (A6); `Sunburst`/`EpicSunburst`
  dashed placeholder arc for unplanned stories (E4). Preserve pure-SVG design, `<title>`s + Story 1.4
  aria-labels, real `<a>` links, empty-state degradation, geometry/hrefs.
- `src/SpecScribe/HtmlTemplater.cs` — dedicated first-class section for quick-dev + deferred work with status
  badges; exclude them from the generic grid; the whole-project work-count stat. Preserve dashboard
  composition, `<main>` landmark, Story 1.5's ordering/stat reframes.
- `src/SpecScribe/EpicsTemplater.cs` — empty-state guidance in `RenderIndex`; convert `AppendEpicCard` /
  `AppendStoryCard` / `AppendUpNextCard` partial-state notes into signposted next actions. Preserve TOC/
  landmark/section structure.
- `src/SpecScribe/BmadCommands.cs` — a helper for add-epic/add-story guidance (or extend existing `For*`
  builders); keep the self-omitting `Add` discipline; **don't regress the documented `ForProject` fallback
  edge case.**
- `src/SpecScribe/ProgressModel.cs` + `src/SpecScribe/ProgressCalculator.cs` — per-status story counts on
  `EpicProgress` for the mosaic (or feed the mosaic story lists); optional quick-dev/deferred counts. Keep
  `ProgressModel.Empty` and all `required` initializers valid.
- `src/SpecScribe/Frontmatter.cs` + `src/SpecScribe/MarkdownConverter.cs` — add `Route` (and optionally
  `Type`) via the existing `GetString(map, …)` seam; keep all fields optional.
- `src/SpecScribe/SiteGenerator.cs` — where the work-inventory is assembled (`_docs` is populated here) and
  passed to `RenderIndex`; possibly exclude promoted artifacts from the generic grid the way README is kept
  out of `_docs`.
- `src/SpecScribe/assets/specscribe.css` — styles for the dashed placeholder arc, the new work-type section/
  badges. Reuse Story 1.5's status tokens; preserve 1.4 focus/reduce blocks and the token system.

Primary TEST candidates:

- `tests/SpecScribe.Tests/ChartsTests.cs` — mosaic delivery segments + pending empty ring; placeholder arc +
  "No task plan yet" tooltip; segment links/aria retained.
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — first-class work section + status badges + no
  double-listing; work-count stat; epic/story/task tallies unchanged with quick-dev/deferred present.
- `tests/SpecScribe.Tests/EpicsParserTests.cs` or a templater test — empty/partial guidance emission (and
  omission when the module lacks the command).
- `tests/SpecScribe.Tests/MarkdownConverterTests.cs` — `route:` parsed (present → value, absent → null), if
  the field is added.

## Library and Framework Requirements

- Stay on the existing .NET / inline-SVG / CSS stack. **No new runtime dependencies.** No JS added by this
  story: the placeholder arc is pure SVG+CSS; tooltips reuse `<title>` and (if Story 1.5 has landed) its
  single sanctioned progressive-enhancement script — do not add a second script or any library. YAML
  frontmatter already uses the in-repo deserializer (`MarkdownConverter`); reuse it.

## Testing Requirements

- Preserve existing coverage and **Story 1.4's accessibility assertions** and **Story 1.5's truthfulness
  assertions** — none may regress.
- Add coverage (see Task 7): A6 mosaic delivery segments; E4 placeholder arc; first-class work-type section +
  badges + no double-listing; work-count stat; **epic/story/task tallies invariant** under quick-dev/deferred
  presence; empty/partial guidance emission + omission; `route` parse.
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~Charts|FullyQualifiedName~HtmlTemplater|FullyQualifiedName~EpicsParser|FullyQualifiedName~Progress|FullyQualifiedName~MarkdownConverter|FullyQualifiedName~SiteGenerator|FullyQualifiedName~StatusStyles"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Manual verification on the generated `index.html` + `epics.html`: quick-dev/deferred first-class with
  status; whole-project counts include them without changing epic/story numbers; Epic 1 mosaic shows
  mid-development (not a full ring); undrafted stories show the dashed placeholder + create-story CTA;
  empty/partial epic surfaces show add-epic/add-story guidance.

## UX and Accessibility Requirements

- Status never color-only: the mosaic keeps a text status/sub-label; the placeholder arc carries a text
  tooltip/aria-label; the new work-type badges pair color with the status word. [Source: epics.md UX-DR17]
- The placeholder arc reads as a **call to action**, not an error — de-emphasized dashed stroke, helpful copy
  ("No task plan yet — run /bmad-create-story N.N"). [Source: UXO E4]
- Authoring guidance uses numbers/active voice, short copy, and the exact command; de-emphasized so it helps
  without cluttering. [Source: EXPERIENCE.md Voice and Tone]
- Reuse the antiquarian token/color system; new section/badges/arc read on-brand (teal/gold/parchment), never
  a foreign default. Motion (if any) respects `prefers-reduced-motion` — the placeholder is static. [Source: DESIGN.md; epics.md UX-DR18]
- Preserve Story 1.4's whole-chart `role="img"` names and segment aria-labels; keep them truthful after the
  A6/E4 changes.

## Reinvention and Regression Guardrails

- Do NOT fold quick-dev/deferred work into epic/story/task counts — that is exactly the "misrepresenting
  completion" the AC forbids. Keep them a separate signal.
- Do NOT build spec-kernel handling (Story 2.2), a sprint page/widget (2.3), planning grouping (2.4),
  iconography (2.5), or comment annotations (2.6).
- Do NOT reintroduce per-chart color literals — the mosaic and arc use Story 1.5's one-token-per-stage system.
- Do NOT build a JS drill/zoom/hash sunburst; do NOT add a second JS file or any third-party lib; do NOT add
  dark mode/theme.
- Do NOT hardcode `/bmad-*` commands — route through `CommandCatalog` so they match the module and self-omit.
- Do NOT regress Story 1.4 accessibility (focus, aria-labels, reduced-motion, skip link/landmark/progressbar),
  Story 1.5's truthfulness (esp. the A5 task-stat reframe), or Story 1.1's missing-section omission.
- Do NOT regress the documented `BmadCommands.ForProject` retrospective-fallback edge case while editing that
  file.
- Preserve zero/low-data graceful degradation and static-host safety (relative links/anchors correct).

## Git Intelligence Summary

- Baseline `8fa1c1d` (main). Recent commits `6a8b4c8`/`4813709` reworked the home "Next Steps" panel
  (`BmadCommands.ForProject`) — read them before editing `BmadCommands` so the authoring guidance composes
  with (not against) the existing project-level suggestions. `8ca412d` is the in-flight 1.3/1.4 line.
- Story 1.2 (`fb9fb88`) is the verify-and-harden pattern; shared rendering seams (CSS/Charts/templaters) are
  changed **additively and centrally** — the whole point of the single-source seams.
- Generated output publishes to GitHub Pages — keep all anchors/links/SVG static-host-safe.

## Latest Technical Information

- Dashed SVG strokes (`stroke-dasharray`) and `<title>`/`data-*`/aria attributes are universally supported in
  evergreen browsers — no polyfills, no vendor prefixes; the placeholder arc needs no new capability. The
  Mermaid CDN and any Story 1.5 tooltip script are unrelated to this work.
- No framework/library version decisions are introduced by this story; it stays entirely within the existing
  .NET + inline-SVG + CSS stack.

## Project Context Reference

- Epic + story source: `_bmad-output/planning-artifacts/epics.md` (Epic 2, Story 2.1; FR2/FR5/FR7; UX-DR17)
- UX review that scoped A6/E4 here: `docs/Story1_4_UX_Observations.md` (rows A6, E4)
- Predecessor that hands A6/E4 + work-type representation to this story: `_bmad-output/implementation-artifacts/1-5-dashboard-insight-polish-and-visual-truthfulness.md`
- Accessibility baseline (shared seams): `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`
- Successor (spec kernel — do NOT do here): Story 2.2 `2-2-first-class-rendering-of-spec-artifacts`
- Fixture artifacts in this repo: `_bmad-output/implementation-artifacts/spec-*.md` (quick-dev, `route: one-shot`), `deferred-work.md`
- Architecture spine / rendering: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`, `rendering-architecture.md`; ADR 0002/0004 (`docs/adrs/`)
- UX design/behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`, `EXPERIENCE.md`
- Key source seams: `src/SpecScribe/Charts.cs`, `HtmlTemplater.cs`, `EpicsTemplater.cs`, `BmadCommands.cs`, `StatusStyles.cs`, `ProgressModel.cs`, `ProgressCalculator.cs`, `Frontmatter.cs`, `MarkdownConverter.cs`, `SiteGenerator.cs`, `assets/specscribe.css`
- Memory: [[charting-is-pure-svg-no-js]], [[story-1-4-a11y-seams-for-1-5]], [[story-1-4-split-into-1-4-1-5-1-6]]

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed — comprehensive developer guide created for
  Epic 2's opener, absorbing the A6/E4 items Story 1.5 routed here and adding quick-dev/deferred work-type
  representation, progress accounting, and inline authoring guidance.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8

### Debug Log References

- Verified quick-dev artifacts are `implementation-artifacts/spec-*.md` with `route: one-shot` frontmatter and
  `deferred-work.md` has no frontmatter (identify by filename); confirmed both currently render as generic
  "Implementation Artifacts" cards and are counted in no progress figure.
- Confirmed A6 (`Charts.EpicMosaic`) and E4 (`Charts.Sunburst`/`EpicSunburst`) were explicitly deferred from
  Story 1.5 to this story, and that `StatusStyles.ForStory` already supplies the per-status roll-up A6 needs.
- Confirmed `Frontmatter` is a fixed-field record but `MarkdownConverter` parses the whole YAML map, so adding
  `Route` is a one-line extension.
- Confirmed the spec KERNEL (`_bmad-output/specs/spec-specscribe/*`) is Story 2.2, not this story.
- Environment: use `py -3` for BMAD helper scripts on this Windows host.
- Planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~Charts|FullyQualifiedName~HtmlTemplater|FullyQualifiedName~EpicsParser|FullyQualifiedName~Progress|FullyQualifiedName~MarkdownConverter|FullyQualifiedName~SiteGenerator|FullyQualifiedName~StatusStyles"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Sequence after Story 1.5 (reuse its status tokens + sanctioned tooltip script) and rebase onto it.
- Land the work-inventory model + `Frontmatter.Route` first (Task 1), then first-class rendering + status
  badges (Task 2) and separate progress accounting with the invariant epic/story tallies (Task 3); then the
  A6 mosaic (Task 4) and E4 placeholder arc (Task 5) on the shared tokens; then the inline authoring guidance
  on empty/partial surfaces (Task 6); then tests (Task 7) and a real generation pass (Task 8).
- Keep every change in the central shared seams; prefer render-/Charts-level string assertions over new
  public API; keep everything host-neutral and static-host-safe.

### Completion Notes List

- First story of Epic 2. Absorbs UXO A6 (delivery mosaic) and E4 (unplanned-story placeholder arc) that Story
  1.5 routed here, and adds the new work: quick-dev (`spec-*.md`, `route: one-shot`) + `deferred-work.md`
  first-class representation with status, whole-project progress accounting that never inflates epic/story
  completion, and inline add-epic/add-story guidance on empty and partial surfaces.
- Explicitly kept out: spec-kernel rendering (2.2), sprint page/widget (2.3), planning grouping/PRD prominence
  (2.4), iconography (2.5), comment annotations (2.6); JS drill engine, dark mode, velocity/deep-git.
- Coordination flags: depends on Story 1.5's status-token system + tooltip script; do not reintroduce
  per-chart color literals; do not regress the documented `BmadCommands.ForProject` fallback edge case while
  editing that file.

**Implementation summary (2026-07-06):**

- **Task 1 — work types modeled.** Added optional `Route`/`Type` to `Frontmatter` and parsed them via the
  existing `GetString(map, …)` seam in `MarkdownConverter` (all fields stay optional). Added
  `WorkInventory` (new): classifies quick-dev as `implementation-artifacts/spec-*.md` + `route: one-shot`
  and the deferred note by filename `deferred-work.md`; deferred open-item count = rendered `<li>` minus
  struck-through (`<del>`) items. Non-fatal by construction (missing/partial ⇒ fewer entries, never an
  exception). Deliberately does NOT match the spec kernel under `specs/spec-specscribe/` (that's Story 2.2).
- **Task 2 — first-class, navigable, badged.** New "Direct & Quick-Dev Work" index band in
  `HtmlTemplater`: quick-dev cards carry a real `status-badge` (site status semantics via new
  `StatusStyles.ForStatus`), plus a deferred-work callout with its open-item count. Promoted docs are
  excluded from the generic "Implementation Artifacts" grid (added to `used` up front, mirroring the README
  precedent) so nothing is double-listed; their standalone pages still generate. Section omitted cleanly
  when empty (Story 1.1).
- **Task 3 — separate progress signal, tallies invariant.** A conditional "Direct changes" stat card
  (quick-dev count + deferred sub-line) makes the whole-project picture complete WITHOUT touching
  `TasksDone/Total`, `StoriesTotal`, or the epic roll-up. Regression pinned by a test asserting the
  epic/story/task stat strings are byte-identical with and without quick-dev/deferred docs present, and that
  the card is absent (four-card row preserved) when there's no such work.
- **Task 4 — A6 delivery mosaic.** `EpicProgress` gained `StoryStatusCounts` (per-status tally computed in
  `ProgressCalculator` from `StatusStyles.ForStory`). `Charts.EpicMosaic` now segments the ring by real
  delivery status (done/review/active/ready/drafted/pending, shared status tokens) instead of
  detailed/not-detailed; "N/N detailed" demoted to the sub-label only; pending epics keep the empty ring +
  "Not yet drafted". Fixed once in `Charts`, verified on both the home dashboard and the epics index.
- **Task 5 — E4 placeholder arc.** `Charts.Sunburst`/`EpicSunburst` render a faint dashed `.sb-noplan`
  outer-ring arc for `TasksTotal == 0` stories, kept a real link with a `<title>`/`aria-label` reading "No
  task plan yet — run /…-create-story N.N". The command is module-aware (routed through the `CommandCatalog`
  now threaded into both sunbursts) and self-omits when absent — no hardcoded `/bmad-*`. Pure SVG + CSS.
- **Task 6 — inline authoring guidance.** New `BmadCommands.InlineGuidance` turns dead-end notes into next
  actions via the module-aware seam (self-omitting): empty-epics state + pending-epic card
  (`create-epics-and-stories`) and undrafted-story card (`create-story N.N`), each with the shared copy
  button. `AppendUpNextCard`'s undrafted case already sits beside the Next Steps panel that surfaces the same
  create-story command, so it was left as-is to avoid duplicating the command in one panel. Did NOT touch the
  documented `BmadCommands.ForProject` retrospective-fallback edge case.
- **Tasks 7–8 — tests + generation pass.** Added coverage in `ChartsTests` (mosaic delivery segments +
  pending empty ring; placeholder arc + CTA + retained links; command-omission), `HtmlTemplaterTests`
  (first-class section + badge + no double-listing; Direct-changes stat; tally invariance; empty/partial
  guidance emission + omission), `MarkdownConverterTests` (`route`/`type` parse), `StatusStylesTests`
  (`ForStatus`/`StoryLabel`), and new `WorkInventoryTests`. Full suite: **227 passing**. Real generation pass
  against this repo produced 26 pages cleanly; verified on the generated `index.html` + `epics.html`:
  3 quick-dev cards + deferred callout (9 open items) surfaced first-class and promoted out of the generic
  grid; Direct-changes stat present with epic/story/task tallies unchanged; Epic 1 mosaic reads green "done"
  (real delivery, not a "detailed" ring); 26 dashed placeholder arcs with module-aware create-story CTAs on
  undrafted stories; pending/undrafted surfaces show the add-epic/add-story guidance. (Generated into the
  gitignored `SpecScribeOutput/` dev output dir, per [[generate-output-dir-is-specscribeoutput]] — not
  `docs/live`, which is vestigial; CI builds `SpecScribeOutput` and deploys it to Pages directly — so the
  pass leaves no committed diff.)

### File List

- _bmad-output/implementation-artifacts/2-1-accurate-work-representation-and-authoring-guidance.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/SpecScribe/Frontmatter.cs
- src/SpecScribe/MarkdownConverter.cs
- src/SpecScribe/WorkInventory.cs (new)
- src/SpecScribe/StatusStyles.cs
- src/SpecScribe/ProgressModel.cs
- src/SpecScribe/ProgressCalculator.cs
- src/SpecScribe/Charts.cs
- src/SpecScribe/HtmlTemplater.cs
- src/SpecScribe/EpicsTemplater.cs
- src/SpecScribe/BmadCommands.cs
- src/SpecScribe/SiteGenerator.cs
- src/SpecScribe/assets/specscribe.css
- tests/SpecScribe.Tests/ChartsTests.cs
- tests/SpecScribe.Tests/HtmlTemplaterTests.cs
- tests/SpecScribe.Tests/MarkdownConverterTests.cs
- tests/SpecScribe.Tests/StatusStylesTests.cs
- tests/SpecScribe.Tests/WorkInventoryTests.cs (new)

## Change Log

- 2026-07-06: Implemented Story 2.1. Modeled quick-dev (`spec-*.md`, `route: one-shot`) and `deferred-work.md`
  as first-class work via a new `WorkInventory` + `Frontmatter.Route/Type`; surfaced them in a dedicated
  "Direct & Quick-Dev Work" index band with status badges and a deferred callout, promoted out of the generic
  grid (no double-listing); added a separate "Direct changes" progress stat that leaves epic/story/task
  tallies invariant. Reworked `Charts.EpicMosaic` to segment by real per-story delivery status (UXO A6, via
  new `EpicProgress.StoryStatusCounts` + `StatusStyles.ForStatus/StoryLabel/StoryStages`) and added the dashed
  `.sb-noplan` placeholder arc with a module-aware create-story CTA to `Charts.Sunburst`/`EpicSunburst` (UXO
  E4). Converted empty/partial epics-and-stories dead-end notes into next actions via new
  `BmadCommands.InlineGuidance` (self-omitting through `CommandCatalog`). Added 12 tests across 5 files
  (227 passing). Status → review.
- 2026-07-06: Created Story 2.1 as Epic 2's opener. Scoped: first-class representation of quick-dev
  (`spec-*.md`, `route: one-shot`) and `deferred-work.md` work with status badges and navigable entries;
  whole-project progress accounting that includes them without altering epic/story/task tallies (preserving
  Story 1.5's A5 reframe); the "Progress by Epic" delivery mosaic (UXO A6) and the unplanned-story dashed
  placeholder arc (UXO E4) both routed here from Story 1.5; and inline add-epic/add-story authoring guidance
  on empty and partial epics/stories surfaces via the module-aware command seam. Documented the dependency on
  Story 1.5's status tokens, the disambiguation from Story 2.2's spec kernel, and the regression guardrails
  around epic/story tallies, Story 1.4 accessibility, and the documented BmadCommands fallback edge case.
