---
baseline_commit: bba1ef445ab61dbfb64ff0e344182284270d6e5f
---

# Story 2.3: Sprint Status Page and Dashboard Widget

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer tracking delivery,
I want a sprint status view in the portal plus an at-a-glance widget on the home page,
so that I can see where every epic and story sits without opening the tracking file.

## Acceptance Criteria

1. **Given** a sprint-status tracking file exists
   **When** the site is generated
   **Then** a sprint status page lists epics and stories with their lifecycle status (backlog → ready-for-dev → in-progress → review → done) and surfaces open retrospective action items
   **And** missing or partial tracking data degrades gracefully without broken navigation.

2. **Given** the dashboard home page
   **When** it loads
   **Then** a compact sprint widget summarizes current status (counts by lifecycle stage and what is in progress) and links to the full sprint page
   **And** the widget is omitted cleanly when no tracking file exists.

> **Origin & scope:** Third story of Epic 2 (Complete and Faithful BMad Artifact Representation).
> This is the FIRST feature to read `sprint-status.yaml` — today no code in `src/` touches it (the only
> `sprint` hits are command-name strings in `BmadCommands.cs`). The lifecycle vocabulary this story surfaces
> (**backlog → ready-for-dev → in-progress → review → done**) is the yaml's own `development_status` values —
> it is *authoritative sprint tracking*, distinct from the status the existing dashboard *derives* from each
> story artifact's `Status:` frontmatter. Read [Two status sources — do not conflate](#two-status-sources-read-this-first)
> before writing a line. Reuse the shared page/nav/chart seams (`SiteNav.Build`, a new templater mirroring
> `RequirementsTemplater`, `Charts`, `StatusStyles`, the `hasX`-gated nav pattern) — additively and centrally.

## Tasks / Subtasks

- [x] Task 1: Locate and parse `sprint-status.yaml` (AC: #1, #2)
  - [x] **Discover the file by well-known name, gated on existence** — the same "matched by presence, omit when absent" discipline `README.md`/`epics.md`/ADRs use. The file lives at `_bmad-output/implementation-artifacts/sprint-status.yaml` in this repo, but resolve it by searching `SourceRoot` for a file named `sprint-status.yaml` (top hit) rather than hard-coding the subfolder, so a differently-placed tracking file still resolves. It is a `.yaml`, so it is **NOT** in `EnumerateSourceFiles` (that globs `*.md`) — locate it separately with an explicit `File.Exists`/enumerate-by-name. [Source: `src/SpecScribe/SiteGenerator.cs:517-523`, `:440-445`, `_bmad-output/implementation-artifacts/sprint-status.yaml`]
  - [x] **Reuse YamlDotNet — it is already a dependency.** `MarkdownConverter` builds an `IDeserializer` (`DeserializerBuilder`, line 19) and deserializes into `Dictionary<string, object>`. Add a new `SprintStatusParser` that deserializes the yaml the same way and reads the `development_status:` map (a `Dictionary<string, string>` of `key → status`) plus the optional `last_updated:` scalar and optional `action_items:` list. Do **not** add a new package. [Source: `src/SpecScribe/MarkdownConverter.cs:7-8`, `:19`, `:143`]
  - [x] **Preserve source order.** YamlDotNet preserves mapping order into the deserialized dictionary; keep entries in file order so epics and their stories render top-to-bottom exactly as tracked (epic-1, its 1-*, epic-2, its 2-*, …). Do not re-sort.
  - [x] **Classify each `development_status` key** into: **epic** (`epic-<N>`), **retrospective** (`epic-<N>-retrospective`), or **story** (`<N>-<M>-<slug>`). Ignore keys that match none (forward-compat). Parse the leading `epic`/`story` numbers so rows can link and group. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:50-105`]
  - [x] **Graceful/robust parse (AC#1 "missing or partial tracking data degrades gracefully").** No file → parser returns `null`/empty and every downstream surface omits (no page, no nav item, no widget). Malformed yaml → catch `YamlDotNet.Core.YamlException` (as `MarkdownConverter.SplitFrontmatter` does) and treat as "no sprint data" rather than throwing. Missing `action_items:`/`last_updated:` → empty/absent, never an error. [Source: `src/SpecScribe/MarkdownConverter.cs:155`]

- [x] Task 2: Model the sprint status + the lifecycle→visual mapping (AC: #1, #2)
  - [x] Add a small model (e.g. `SprintStatus.cs`): an ordered list of entries `(SprintEntryKind Kind, string RawKey, int? EpicNumber, int? StoryMinor, string Status)`, the optional `LastUpdated`, and the open `ActionItems`. Keep it a plain record set — no rendering in the model. Mirror the shape/size of `RequirementsModel`/`AdrEntry`.
  - [x] **Map the yaml lifecycle onto the existing six-stage color vocabulary — do NOT invent a parallel one.** `StatusStyles` + the CSS already define done/active/review/ready/pending/deferred. Map: `done`→`done` (green), `review`→`review`, `in-progress`→`active` (teal), `ready-for-dev`→`ready` (gold), `backlog`→`pending` (parchment). For **retrospective** entries map `optional`→`pending`, `done`→`done`; for **action-item** status map `open`→`ready`, `in-progress`→`active`, `done`→`done`. Add a `StatusStyles.ForSprint(string status)` (and a matching human `SprintLabel`) so this mapping lives in the one status-semantics file, next to `ForStory`/`ForEpic`/`ForRequirement`. [Source: `src/SpecScribe/StatusStyles.cs:1-62`, `src/SpecScribe/assets/specscribe.css:829-833`, `:1175-1180`, `:1475-1477`]
  - [x] **CSS check — `backlog`/`pending` badge.** `.status-badge` rules exist for done/active/review/ready/drafted/deferred but there is **no `.status-badge.pending`** (only `.swatch.pending`/`.donut-seg.pending`). Since `backlog` maps to the `pending` class, add a single minimal `.status-badge.pending { background: var(--parchment-dark); color: var(--ink-light); border-color: var(--border); }` rule (mirroring the `deferred` badge) so backlog rows read on-brand. Keep it one small addition; reuse existing `--status-*`/parchment vars — no new palette. [Source: `src/SpecScribe/assets/specscribe.css:829-835`, `:1475`]

- [x] Task 3: Render the full sprint status page (AC: #1)
  - [x] **Add a `SprintTemplater.RenderIndex(SprintStatus, EpicsModel?, SiteNav)` mirroring `RequirementsTemplater.RenderIndex`** — same page shell: `RenderHeadOpen` → `nav.RenderNavBar` → `RenderBreadcrumb(Home / Sprint Status)` → single `<main id="main-content">` → `RenderFooter`. Do not fork a new page skeleton. [Source: `src/SpecScribe/RequirementsTemplater.cs:10-71`]
  - [x] **Group stories under their epic in file order.** For each epic entry render a section header with the epic's status badge, then its stories as rows, each row = title + a `status-badge <class>` for its lifecycle stage. Show the epic's retrospective entry (if present) as a de-emphasized note on the epic. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:50-105`]
  - [x] **Human titles + working links, enriched from `EpicsModel`.** The yaml key is a slug (`2-3-sprint-status-page-and-dashboard-widget`). Prefer the real title from the parsed `EpicsModel` (match `epic-<N>` → epic Number N; `<N>-<M>-…` → story id "N.M"); fall back to prettifying the slug (strip the `N-M-` prefix, replace `-` with spaces, title-case) when no epics match. Link each story row to its generated page (`epics/story-N-M.html`) when that story has an `ArtifactOutputPath`, and each epic header to `epics/epic-N.html`; render as plain text (no link) when no target exists — **never a broken link** (Story 1.1 / NFR2). [Source: `src/SpecScribe/EpicsModel.cs:7-43`, `src/SpecScribe/SiteGenerator.cs:314`, `:343`]
  - [x] **Surface open retrospective action items (AC#1).** If `action_items:` is present, render a section listing the **open** (and in-progress) items with their status badge; omit the section entirely when there are none. The current repo `sprint-status.yaml` has **no `action_items:` block yet**, so this must render nothing (not an empty header) in that case — that IS the graceful-degradation path, and your fixture must cover both. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:29-41` (header documents the `action_items` shape)]
  - [x] Optionally lead the page with a compact lifecycle-count summary (reuse `Charts.StatCard`/a stacked bar or a `Donut` over the five stages) — the same summary the home widget shows, so the page and widget speak one number. Keep it on-brand (reuse `dashboard`/`chart-row` classes), not a new visual language. [Source: `src/SpecScribe/RequirementsTemplater.cs:38-41`, `src/SpecScribe/Charts.cs:14-88`]
  - [x] Add `SiteNav.SprintOutputPath = "sprint.html"` and write the page in a new `SiteGenerator.WriteSprint(...)` gated on the sprint file existing, called from `GenerateAll` (and run `ApplyRequirementLinks` over it like every other page so FR/NFR tokens still resolve). [Source: `src/SpecScribe/SiteGenerator.cs:432-438`, `:470-488`, `src/SpecScribe/SiteNav.cs:10-14`]

- [x] Task 4: Add the dashboard sprint widget (AC: #2)
  - [x] **Add a compact "Sprint Status" panel in `AppendDashboard`, gated on sprint data being present.** Thread the parsed `SprintStatus` into `RenderIndex` → `AppendDashboard` the same way `progress`/`epicsModel`/`requirements` are already passed. The widget shows **counts by lifecycle stage** (backlog/ready-for-dev/in-progress/review/done — reuse a `Charts.Donut` or a `req-stacked` bar with the mapped classes, non-zero legend rows only per Story 1.5 B4) **and what is in progress** (the in-progress + review story titles, linked), with a `view-epic-link` CTA to `sprint.html`. Reuse `chart-panel`/`chart-panel-header-row`/`view-epic-link`. [Source: `src/SpecScribe/HtmlTemplater.cs:128-196`, `:301-308`, `:334-368`]
  - [x] **Omit cleanly when no tracking file exists (AC#2).** When `SprintStatus` is null/empty, append nothing — no empty panel, matching the `AppendRequirementsPanel`/`AppendNowAndNext` early-return pattern. [Source: `src/SpecScribe/HtmlTemplater.cs:297-299`, `:390-391`]
  - [x] **Placement + de-duplication.** The dashboard already has a **Now & Next** panel that derives in-dev/review/up-next from story-artifact `Status:` (a *different* source — see below). Place the sprint widget so it reads as the **tracking-file** counterpart, not a duplicate: give it a clear heading ("Sprint Status") and label its source ("from sprint-status.yaml"). Do not remove or restyle Now & Next. [Source: `src/SpecScribe/HtmlTemplater.cs:370-432`]

- [x] Task 5: Wire nav + generation plumbing (AC: #1, #2)
  - [x] **Gate a Sprint nav item + quick-link on presence**, threaded through `SiteNav.Build` exactly like `hasAdrs`/`hasReadme`: add a `hasSprint` parameter, push `("Sprint", SprintOutputPath)` into `Items` and `("Sprint", SprintOutputPath, "See where every epic and story sits.")` into `QuickLinks`. Place it near Epics/Requirements (delivery-tracking neighborhood). [Source: `src/SpecScribe/SiteNav.cs:33-94`]
  - [x] **Compute the presence signal once** (a `SprintAvailable`/`SprintSourcePath` member like `ReadmeAvailable`) and pass it into `BuildNav`/`SiteNav.Build`. Parse the sprint yaml once in `GenerateAll` and cache it in a `_sprint` field (like `_progress`/`_requirements`), so both `WriteSprint` and `WriteIndex`→`AppendDashboard` use the same parsed instance. [Source: `src/SpecScribe/SiteGenerator.cs:23-28`, `:35-110`, `:536-540`]
  - [x] **Watch-mode note (known limitation — keep MVP on the full-generate path).** `FileWatcherService` filters `*.md` and its `Debounce` early-returns non-`.md`, so **editing `sprint-status.yaml` will not live-reload** the sprint page in `--watch`. The ACs are scoped to "when the site is generated" (full rebuild), which is fully covered. Either (a) document this limitation and defer live-reload to Epic 5's watch work, or (b) make a **small, contained** extension: also react to the specific `sprint-status.yaml` filename in the watcher and route it to a full `GenerateAll`/index+sprint rewrite. Default: (a) document; only do (b) if trivial and well-tested. Do not broaden the watcher's `*.md` filter wholesale. [Source: `src/SpecScribe/FileWatcherService.cs:35`, `:65`]

- [x] Task 6: Test coverage (AC: #1, #2)
  - [x] **`SprintStatusParserTests`** (new): a valid yaml parses into ordered epic/story/retrospective entries with correct statuses; a missing file → null/empty; malformed yaml → null/empty (no throw); `action_items:` present → parsed, absent → empty; keys that match no pattern are ignored. Use inline yaml strings (no disk needed) mirroring `MarkdownConverterTests` frontmatter cases. [Source: `tests/SpecScribe.Tests/MarkdownConverterTests.cs`]
  - [x] **`SprintTemplater`/render tests** (render-level string assertions — the house pattern): the page emits a status badge per epic and story with the mapped class (e.g. `in-progress`→`active`, `backlog`→`pending`); stories group under their epic; an `action_items` section appears only when open items exist and is absent otherwise; a story with an `ArtifactOutputPath` links to `epics/story-N-M.html` while one without renders as plain text (no broken link). [Source: `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`]
  - [x] **`SiteNavTests`**: the Sprint nav item + quick-link appear when `hasSprint` is true and are omitted when false; existing labels (Home/Readme/PRD/Architecture/Epics/Requirements/ADRs) are unchanged (no duplicates). [Source: `tests/SpecScribe.Tests/SiteNavTests.cs`]
  - [x] **Dashboard-widget test**: `RenderIndex` with sprint data emits a "Sprint Status" panel with per-stage counts and a link to `sprint.html`; with null sprint data the panel is absent AND the existing Now & Next / Requirements panels are unaffected. Assert the home index still has exactly one `<main id="main-content">` and the skip link (Story 1.4). [Source: `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`]
  - [x] **`SiteGenerator`-level test** (temp-dir fixture — follow `SiteGeneratorFidelityTests`/`SiteGeneratorTraceabilityTests`): with a `_bmad-output/implementation-artifacts/sprint-status.yaml` present, a `sprint.html` is produced and the home index carries the widget + Sprint nav; with no yaml, no `sprint.html`, no widget, no Sprint nav — and no broken links either way. [Source: `tests/SpecScribe.Tests/SiteGeneratorFidelityTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs`]

- [x] Task 7: End-to-end validation with a real generation pass (AC: #1, #2)
  - [x] Run the focused test filter, then a real generation pass against this repo — it ships a live `sprint-status.yaml` (epics 1–7, backlog→in-progress→done spread) as a full fixture.
  - [x] Manually verify on `docs/live/index.html`: a "Sprint Status" widget with per-stage counts + in-progress items, linking to `sprint.html`; a "Sprint" nav entry. On `docs/live/sprint.html`: epics with their status badges, stories grouped underneath with lifecycle badges and working links, and (since the live file has none yet) **no** action-items section. Temporarily rename the yaml and re-generate to confirm the page, widget, and nav all omit cleanly with no broken links.

### Review Findings

- [x] [Review][Patch] Build the compact dashboard sprint widget (AC #2) — `HtmlTemplater.AppendDashboard` never calls `SprintTemplater.RenderBoard`/`StoryStageCounts`; only a nav link to sprint.html existed. Fixed: added `SprintTemplater.RenderDashboardWidget` (donut + non-zero legend + in-progress/review list + CTA), wired into `AppendDashboard`. [src/SpecScribe/HtmlTemplater.cs, src/SpecScribe/SprintTemplater.cs]
- [x] [Review][Patch] Restore the whole-project "Now & Next" panel on the home dashboard — `AppendNowAndNext`/`AppendNowNextCard` were deleted from `HtmlTemplater.AppendDashboard` with no whole-project replacement. Fixed: restored verbatim from baseline, placed above the sprint widget. [src/SpecScribe/HtmlTemplater.cs]
- [x] [Review][Patch] `SprintStatusParser` can crash the whole site generation on malformed input. Fixed: `int.Parse` → `int.TryParse` (skip unparseable entries) and `ParseFile`'s catch broadened to `IOException or UnauthorizedAccessException`. [src/SpecScribe/SprintStatusParser.cs:62,66,70,36]
- [x] [Review][Patch] Unmatched sprint board cards are keyboard-unreachable. Fixed: added `tabindex="0" role="group"` when rendering the non-link `<div>` variant. [src/SpecScribe/SprintTemplater.cs:272]
- [x] [Review][Patch] `RetroActionStyler.RemoveColumn` silently misaligns ragged table rows. Fixed: a row is only column-stripped when its cell count matches the header's; otherwise left fully unchanged. [src/SpecScribe/RetroActionStyler.cs]
- [x] [Review][Patch] `SiteGenerator.EpicRetroMap` recomputed per-epic in a loop. Fixed: computed once in `ParseRetros` and cached in a field. [src/SpecScribe/SiteGenerator.cs:430,572]
- [x] [Review][Patch] `WorkInventory.Build(...)` invoked twice per generation pass. Fixed: computed once in `GenerateAll` and passed into both `WriteActionItems`/`WriteIndex`. [src/SpecScribe/SiteGenerator.cs:535,619]
- [x] [Review][Patch] `ActionItemsTemplater.IsDebtRelated` is a bare substring match. Fixed: switched to a whole-word regex (`\b(deferred|tech(nical)?\s+debt)\b`). [src/SpecScribe/ActionItemsTemplater.cs]
- [x] [Review][Patch] Stale duplicate XML doc comment above `SprintTemplater.RenderBoardTabs`. Fixed: removed the stale duplicate. [src/SpecScribe/SprintTemplater.cs]
- [x] [Review][Defer] `ExtractTopLevelBlock` silently truncates on a duplicate top-level key (e.g. two `development_status:` blocks) rather than erroring or merging — deferred, pre-existing shape of the hand-rolled block-slicer; requires malformed hand-authored yaml, low likelihood. [src/SpecScribe/SprintStatusParser.cs]
- [x] [Review][Defer] `ExtractLastUpdated` breaks on a YAML block-scalar `last_updated` value (`>`/`|`) — would display the literal indicator character instead of a date — deferred, pre-existing shape of the hand-rolled parser; low-likelihood authoring pattern. [src/SpecScribe/SprintStatusParser.cs]

## Developer Context Section

### Epic Context and Business Value

Epic 2 — "Complete and Faithful BMad Artifact Representation" — makes the portal reflect the **whole**
project rather than only epics and stories. Story 2.1 surfaced quick-dev/deferred work; Story 2.2 surfaced
the spec kernel; **Story 2.3 surfaces the sprint tracking file** — the one artifact a maintainer opens by
hand today to answer "where does everything sit?" Turning `sprint-status.yaml` into a first-class page plus
an at-a-glance home widget advances **FR2** (first-class BMad support), **FR5** (coherent navigation +
complete artifact-class representation), **FR7** (surfacing all work/status truthfully), and the
graceful-degradation guarantee (**NFR2**). It is also the first time the portal reads the *authoritative*
lifecycle state instead of inferring it — a meaningful truthfulness upgrade over the derived status the
dashboard shows today. Later Epic 2 stories continue the arc: planning grouping + PRD prominence (2.4),
iconography (2.5), comment annotations (2.6).

### Story Foundation Extract

- **Primary concern:** a maintainer sees the full delivery ledger (every epic + story with its lifecycle
  stage, plus open retrospective action items) in the portal, and a compact home widget that answers
  "what's the sprint doing right now?" without opening the yaml.
- **User outcome:** land on Home → the Sprint Status widget shows per-stage counts and what's in progress →
  click through to `sprint.html` for the full per-epic/story breakdown.
- **Success boundary:** built on the existing static-HTML substrate and the shared page/nav/chart seams
  (`SiteNav`, a `RequirementsTemplater`-shaped templater, `Charts`, `StatusStyles`, YamlDotNet). Additive
  and central; no new engine, no new stack, no new runtime dependency.
- **Regression boundary:** epic/story/requirement/ADR/quick-dev/spec surfaces and tallies unchanged;
  Story 1.4 accessibility (skip link, single `main`, focus, aria, reduced-motion) and Story 1.5
  truthfulness preserved; the existing Now & Next panel not removed or restyled; antiquarian identity kept.

### Current Implementation Reality (READ THIS FIRST)

- **Nothing reads `sprint-status.yaml` today.** The only `sprint` references in `src/` are the
  `sprint-status`/`sprint-planning` **command-name strings** in `BmadCommands.cs` (next-steps suggestions).
  This story introduces the first parse. [Source: `src/SpecScribe/BmadCommands.cs:137`, `:153`]
- **The yaml is not a source file.** `EnumerateSourceFiles` globs `*.md` under `SourceRoot`, so the `.yaml`
  is invisible to the normal pipeline — locate it separately by name + `File.Exists`. [Source: `src/SpecScribe/SiteGenerator.cs:517-523`]
- **YamlDotNet is already wired.** `MarkdownConverter` deserializes frontmatter via an `IDeserializer`
  (`DeserializerBuilder`) into `Dictionary<string, object>` and catches `YamlException`. Reuse the same
  approach; do not add a package. [Source: `src/SpecScribe/MarkdownConverter.cs:7-8`, `:19`, `:143-155`]
- **New standalone pages follow one precedent.** `RequirementsTemplater.RenderIndex` + `WriteRequirements`
  is the template: build the page shell with `RenderHeadOpen`/`RenderNavBar`/`RenderBreadcrumb`/single
  `<main>`/`RenderFooter`, then `WriteX(...)` writes it under the output root wrapped in
  `ApplyRequirementLinks`. Mirror it for sprint. [Source: `src/SpecScribe/RequirementsTemplater.cs:10-71`, `src/SpecScribe/SiteGenerator.cs:470-488`]
- **Nav gating is a fixed pattern.** `SiteNav.Build` takes `hasAdrs`/`hasReadme` booleans and pushes items
  only when true; the home dashboard's quick-links come from `nav.QuickLinks`. Add `hasSprint` the same way.
  [Source: `src/SpecScribe/SiteNav.cs:33-94`, `src/SpecScribe/HtmlTemplater.cs:257-274`]
- **Dashboard panels are gated early-returns.** `AppendRequirementsPanel` and `AppendNowAndNext` bail when
  their data is empty, so the layout self-heals. The sprint widget follows the same shape. [Source: `src/SpecScribe/HtmlTemplater.cs:297-299`, `:390-391`]
- **Status color semantics live in exactly one place.** `StatusStyles` maps story/epic/requirement status to
  done/active/review/ready/pending/deferred classes; the CSS defines those on `.status-badge`, `.swatch`,
  `.donut-seg`. Add the sprint mapping here, not inline in the templater. Note the missing
  `.status-badge.pending` rule (Task 2). [Source: `src/SpecScribe/StatusStyles.cs:1-62`, `src/SpecScribe/assets/specscribe.css:829-835`]

### Two Status Sources (READ THIS FIRST)

The portal will now carry **two** status signals that can legitimately disagree — be deliberate about it:

| Signal | Source | Used by |
|---|---|---|
| **Derived** story/epic status | each story artifact's `Status:` frontmatter line, read by `ProgressCalculator` → `StatusStyles.ForStory`/`ForEpic` | existing sunburst, Epic Status donut, **Now & Next** |
| **Tracked** lifecycle status (backlog→…→done) | `sprint-status.yaml` `development_status` | **new** sprint page + widget (this story) |

- These are **different sources** and may differ (e.g. the yaml marks `2-3-…: in-progress` while the story
  artifact frontmatter still says `ready-for-dev`). That is expected — the yaml is the sprint *tracking*
  ledger; the artifact frontmatter is the story's own self-reported state.
- **Do not "reconcile" them into one number, and do not repoint the existing panels at the yaml.** Keep the
  new surfaces clearly labeled as coming from the sprint tracking file (Story 1.5 truthfulness: name what a
  number counts; never let two panels silently contradict by pretending to be the same measure). The sprint
  page/widget are the yaml view; Now & Next stays the derived view. [Source: `_bmad-output/implementation-artifacts/1-5-dashboard-insight-polish-and-visual-truthfulness.md`, `src/SpecScribe/StatusStyles.cs:5`]

### Scope Boundaries

- **IN (this story):** parse `sprint-status.yaml` (YamlDotNet, existing dep); a **`sprint.html` page** listing
  epics + stories grouped, each with a lifecycle status badge, plus a linked title, plus an **open
  retrospective action-items** section; a **compact home dashboard widget** (per-stage counts + what's in
  progress + CTA to the page); a **gated Sprint nav item + quick-link**; the lifecycle→visual mapping in
  `StatusStyles` (+ the one missing `.status-badge.pending` CSS rule); **graceful degradation** for
  missing/partial/malformed tracking data; render-level + generation-level tests.
- **OUT — repointing existing panels (sunburst / Epic Status donut / Now & Next) at the yaml.** They keep
  their derived source. This story only *adds* the yaml-sourced surfaces.
- **OUT — live-reload of the yaml in `--watch`** beyond (optionally) the small contained extension in Task 5;
  the watcher's `*.md`-only filter and Epic 5's watch scope own the general case.
- **OUT — editing/writing the yaml** (the portal is read-only over BMad artifacts), a burndown/velocity chart
  or historical trend (needs history the file doesn't carry — Epic 3), planning grouping/PRD prominence
  (2.4), iconography (2.5), comment annotations (2.6), the JS drill sunburst, dark mode.

### Previous Story Intelligence

- Stories 2.1 and 2.2 (both `ready-for-dev`) are the immediate predecessors; both established the **classify-
  by-directory / discover-by-well-known-name** and **shared-seam, additive-and-central** discipline this
  story continues. 2.2 in particular is the closest structural analog: it added a gated nav affordance, a
  labeled section, and list-aware YAML parsing — all patterns reused here. [Source: `_bmad-output/implementation-artifacts/2-2-first-class-rendering-of-spec-artifacts.md:163-191`]
- 2.1 established the "distinguish *no plan yet* from *no data* so gaps read as next actions" empty-state
  ethic — apply it: a present-but-mostly-`backlog` sprint reads as an honest early-stage ledger, not an error.
  [Source: `_bmad-output/planning-artifacts/epics.md:287-291`]
- Story 1.1 established graceful omission of missing artifact classes; Story 1.4 established the a11y floor
  (skip link, single `main`, focus, aria, reduced-motion); Story 1.5 established truthfulness (name what a
  number counts, one status vocabulary, non-zero-only legends). All are contracts to preserve — the new page,
  widget, and nav item inherit them by reusing the existing templater/nav/`Charts`/`StatusStyles` seams.
- Environment: use `py -3` for BMAD helper scripts on this Windows host (`python`/`python3` are not on PATH).

### Architecture Compliance

- **One nav seam, one status-semantics seam, one page precedent.** Sprint nav = a new `hasSprint`-gated entry
  in `SiteNav.Build`; the lifecycle→color mapping = a new method in `StatusStyles`; the page = a templater
  mirroring `RequirementsTemplater`. No parallel rendering path, no inline color logic. [Source: `src/SpecScribe/SiteNav.cs:33-94`, `src/SpecScribe/StatusStyles.cs`, `src/SpecScribe/RequirementsTemplater.cs`]
- **Graceful degradation is contractual (NFR2).** Missing/partial/malformed tracking data → omitted page,
  nav, and widget, and empty/absent action-items — never an exception or a broken link. Reuse the
  `YamlException` catch, the `hasX` omit-when-absent nav gate, and the empty-panel early return. [Source: `src/SpecScribe/MarkdownConverter.cs:155`, `src/SpecScribe/SiteNav.cs:57-60`, `src/SpecScribe/HtmlTemplater.cs:297-299`]
- **Host-neutral output (NFR6, future webview).** The page, widget, nav item, and badges are static HTML/CSS
  + relative hrefs (correct depth via `PathUtil.RelativePrefix`) — no host-specific behavior, so the Epic 6
  webview inherits them. GitHub-Pages-safe. [Source: `src/SpecScribe/PathUtil.cs`]
- **Self-contained packaging.** Any CSS reuses the embedded `specscribe.css` (one small `.status-badge.pending`
  rule at most); no loose asset files, no new deps. Reuse `chart-panel`/`view-epic-link`/`status-badge`/
  `swatch`/`req-stacked`/`epic-mosaic` classes over new ones. [Source: `src/SpecScribe/SiteGenerator.cs:500-509`, `src/SpecScribe/assets/specscribe.css`]

## Technical Requirements

- Locate `sprint-status.yaml` by well-known name under `SourceRoot`, gated on existence; it is not in the
  `*.md` source enumeration. Parse with the already-referenced YamlDotNet (`Dictionary<string,object>`),
  reading `development_status` (order-preserving), optional `last_updated`, optional `action_items`.
- Classify each `development_status` key as epic / retrospective / story; ignore unrecognized keys. Keep file
  order. Catch `YamlException` and treat malformed/missing as "no sprint data".
- Add `StatusStyles.ForSprint(status)` + `SprintLabel(status)` mapping the yaml lifecycle
  (`backlog/ready-for-dev/in-progress/review/done`, plus retrospective `optional/done` and action-item
  `open/in-progress/done`) onto the existing done/active/review/ready/pending classes — no new palette.
- Add the missing `.status-badge.pending` CSS rule (mirror `.status-badge.deferred`) so `backlog` rows read
  on-brand.
- Add `SprintTemplater.RenderIndex(...)` (mirroring `RequirementsTemplater`): epics with status badges,
  stories grouped underneath with lifecycle badges + resolvable links (story page when it exists, else plain
  text — never broken), an open-action-items section rendered only when non-empty, and an optional lifecycle-
  count summary consistent with the widget.
- Add a gated dashboard **Sprint Status** widget in `AppendDashboard` (per-stage counts via `Charts.Donut`/
  `req-stacked`, non-zero legend only; in-progress/review items linked; CTA to `sprint.html`), omitted when
  no data; do not remove/restyle Now & Next.
- Add `SiteNav.SprintOutputPath` + a `hasSprint`-gated nav item and quick-link. Parse once and cache in a
  `_sprint` field; pass the parsed instance into both `WriteSprint` and `RenderIndex`.
- Preserve Story 1.4 accessibility and Story 1.5 truthfulness; keep the two status sources labeled and
  distinct. No new JS; static-host-safe.

## File Structure Requirements

Primary NEW files:

- `src/SpecScribe/SprintStatus.cs` — the parsed model (ordered entries + `LastUpdated` + `ActionItems`),
  sized like `RequirementsModel`/`AdrEntry`.
- `src/SpecScribe/SprintStatusParser.cs` — YamlDotNet parse of `development_status`/`last_updated`/
  `action_items`, key classification, robust `YamlException`/missing handling.
- `src/SpecScribe/SprintTemplater.cs` — `RenderIndex(SprintStatus, EpicsModel?, SiteNav)` mirroring
  `RequirementsTemplater`.
- `tests/SpecScribe.Tests/SprintStatusParserTests.cs` and `tests/SpecScribe.Tests/SprintTemplaterTests.cs`
  (or fold render assertions into `HtmlTemplaterTests`).

Primary UPDATE candidates:

- `src/SpecScribe/StatusStyles.cs` — add `ForSprint`/`SprintLabel`.
- `src/SpecScribe/SiteNav.cs` — `SprintOutputPath` const + `hasSprint` param + gated item/quick-link.
- `src/SpecScribe/SiteGenerator.cs` — locate the yaml (`SprintAvailable`/`SprintSourcePath`), parse once into
  a `_sprint` field in `GenerateAll`, `WriteSprint(...)`, pass `hasSprint` into `BuildNav`/`SiteNav.Build`,
  and pass `_sprint` into `WriteIndex`→`RenderIndex`.
- `src/SpecScribe/HtmlTemplater.cs` — thread `SprintStatus?` into `RenderIndex`/`AppendDashboard`; add the
  gated Sprint Status widget.
- `src/SpecScribe/assets/specscribe.css` — the single `.status-badge.pending` rule (and only a small sprint-
  page/widget style if strictly needed; prefer reusing existing classes).
- `src/SpecScribe/FileWatcherService.cs` — only if you take Task 5's optional small watch extension.

Primary TEST updates:

- `tests/SpecScribe.Tests/SiteNavTests.cs` — Sprint item/quick-link present/absent; labels unchanged.
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — widget present/absent; single `main`; skip link intact.
- A `SiteGenerator`-level test (new sibling or existing fidelity/traceability file) — `sprint.html` produced
  with a yaml fixture, omitted without; no broken links either way.

## Library and Framework Requirements

- Stay on the existing .NET / Markdig / **YamlDotNet** / inline-SVG / CSS stack. **No new runtime
  dependencies, no new JS.** The parser uses the YamlDotNet deserializer already wired in `MarkdownConverter`;
  the page/widget are static HTML + the existing `Charts`/`StatusStyles`/CSS. TOC, requirement linkification,
  and nav are existing seams — reused, not re-implemented.

## Testing Requirements

- Preserve existing coverage and **Story 1.4's accessibility assertions** and **Story 1.5's truthfulness
  assertions** — none may regress. The home index must keep exactly one `<main id="main-content">` and the
  skip link, and must not gain a contradictory status number.
- Add coverage (see Task 6): parser (valid/missing/malformed/partial/action-items present-absent/unknown
  keys); sprint page (per-stage badges with mapped classes, grouping, action-items only when present,
  resolve-or-omit story links); nav gating (present/absent, labels unchanged); dashboard widget
  (present/absent, counts, CTA, non-regression of Now & Next / Requirements panels).
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~Sprint|FullyQualifiedName~HtmlTemplater|FullyQualifiedName~SiteNav|FullyQualifiedName~SiteGenerator|FullyQualifiedName~StatusStyles"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Manual verification on `docs/live/index.html` (Sprint Status widget + Sprint nav) and `docs/live/sprint.html`
  (epics + grouped stories with lifecycle badges + working links; no action-items section since the live file
  has none). Rename the yaml and re-generate to confirm the page, widget, and nav all omit with no broken links.

## UX and Accessibility Requirements

- The sprint page and widget read on-brand: reuse the antiquarian `dashboard`/`chart-panel`/`status-badge`/
  `swatch`/`epic-mosaic` treatment (teal/gold/parchment/moss), never a foreign default. Lifecycle badges use
  the shared status colors so a reader who learned the vocabulary on the sunburst reads the sprint page for
  free. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`]
- Every status is a **text badge**, never color-only (UX-DR17) — the badge carries the lifecycle word, and
  color is redundant reinforcement. Counts pluralize correctly (`Charts.Plural`). [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR17, `src/SpecScribe/Charts.cs:526`]
- Preserve Story 1.4 accessibility: the sprint page uses one `<main id="main-content">`; the widget lives
  inside the home page's existing single `<main>`; all links/CTAs are real focusable `<a>`s; any donut carries
  a `role="img"` accessible name (via `Charts.Donut(ariaLabel:)`) or is decorative behind a labeled legend.
  [Source: `src/SpecScribe/HtmlTemplater.cs:79`, `src/SpecScribe/Charts.cs:48-57`]
- The widget reads as a helpful summary, de-emphasized enough not to crowd Now & Next; it is static (no
  motion), so `prefers-reduced-motion` is trivially satisfied. Label its source ("from sprint-status.yaml")
  so it doesn't read as contradicting the derived Now & Next panel. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR18]

## Reinvention and Regression Guardrails

- Do NOT add a YAML/serialization package — YamlDotNet is already referenced and used in `MarkdownConverter`.
- Do NOT invent a new status color vocabulary — map the yaml lifecycle onto the existing
  done/active/review/ready/pending classes in `StatusStyles`, and add only the one missing
  `.status-badge.pending` CSS rule.
- Do NOT repoint the sunburst / Epic Status donut / Now & Next at the yaml, and do NOT reconcile the two
  status sources into one number — keep the sprint surfaces labeled as the tracking-file view.
- Do NOT emit a link to a story/epic page that doesn't exist — resolve-or-plain-text, never a broken link
  (AC#1, NFR2).
- Do NOT render an empty action-items header when there are none (the live file has none today).
- Do NOT broaden the file watcher's `*.md` filter wholesale; keep any watch change to the specific
  `sprint-status.yaml` filename, or defer to Epic 5.
- Do NOT regress Story 1.4 accessibility (skip link, single `main`, focus, aria, reduced-motion), Story 1.5
  truthfulness, or Story 1.1's missing-section omission. Keep all links/anchors static-host-safe (GitHub Pages).

## Git Intelligence Summary

- Baseline `bba1ef4` (main, "1.4 Code Review"). Recent commits reworked the home "Next Steps" panel
  (`BmadCommands.RenderProjectNextSteps`) and the dashboard composition (`AppendDashboard`) — read them if the
  sprint widget touches dashboard layout so it composes with, not against, the existing Now & Next / stat-grid
  / quick-link order. [Source: `src/SpecScribe/HtmlTemplater.cs:128-196`]
- Shared seams (`SiteNav`/`HtmlTemplater`/`StatusStyles`/`SiteGenerator`/`RequirementsTemplater`) are the
  single-source points — change them additively and centrally, the same pattern 1.2–2.2 followed.
- Generated output publishes to GitHub Pages — keep every href/anchor static-host-safe (relative, correct
  depth via `PathUtil.RelativePrefix`).

## Latest Technical Information

- No framework/library version decisions are introduced by this story; it stays entirely within the existing
  .NET + Markdig + YamlDotNet + inline-SVG + CSS stack. YamlDotNet already deserializes yaml into
  `Dictionary<string,object>`, so reading `development_status` (a nested map) and `action_items` (a list)
  needs no new package — the same list/scalar handling 2.2 used for `companions:`/`sources:` applies.
- Relative `<a href>` navigation and static badge/section markup are universally supported — no polyfills,
  no new capability. The Mermaid CDN and the Story 1.5 tooltip/copy script are unrelated to this work.

## Project Context Reference

- Epic + story source: `_bmad-output/planning-artifacts/epics.md` (Epic 2, Story 2.3; FR2/FR5/FR7; NFR2)
- The tracking file itself (live fixture): `_bmad-output/implementation-artifacts/sprint-status.yaml`
  (`development_status` across epics 1–7; header documents `action_items`/retrospective/lifecycle semantics)
- Closest structural precedents: `src/SpecScribe/RequirementsTemplater.cs` (new standalone page + `WriteX`),
  `src/SpecScribe/SiteNav.cs` (`hasX`-gated nav), `src/SpecScribe/MarkdownConverter.cs` (YamlDotNet usage)
- Status semantics (single source): `src/SpecScribe/StatusStyles.cs`; CSS badges/swatches: `src/SpecScribe/assets/specscribe.css:829-835`, `:1175-1180`, `:1475-1477`
- Predecessors: `_bmad-output/implementation-artifacts/2-1-accurate-work-representation-and-authoring-guidance.md`, `2-2-first-class-rendering-of-spec-artifacts.md`
- Accessibility baseline: `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`
- Truthfulness baseline (two-source discipline): `_bmad-output/implementation-artifacts/1-5-dashboard-insight-polish-and-visual-truthfulness.md`
- Successors (do NOT do here): 2.4 planning grouping/PRD prominence, 2.5 iconography, 2.6 comment annotations
- Key source seams: `src/SpecScribe/SiteGenerator.cs`, `HtmlTemplater.cs`, `SiteNav.cs`, `StatusStyles.cs`, `RequirementsTemplater.cs`, `Charts.cs`, `MarkdownConverter.cs`, `FileWatcherService.cs`, `EpicsModel.cs`, `assets/specscribe.css`
- UX design/behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`, `EXPERIENCE.md`
- Memory: [[charting-is-pure-svg-no-js]], [[story-1-4-a11y-seams-for-1-5]], [[story-1-4-split-into-1-4-1-5-1-6]]

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed — comprehensive developer guide created for
  Epic 2's sprint-status story: the first parse of `sprint-status.yaml` (via the already-referenced
  YamlDotNet), a `sprint.html` page listing epics + grouped stories with lifecycle status badges and
  resolvable links plus an open-action-items section, a gated home dashboard Sprint Status widget (per-stage
  counts + what's in progress + CTA), a gated Sprint nav item/quick-link, the lifecycle→visual mapping added
  to the single `StatusStyles` seam (+ one missing `.status-badge.pending` CSS rule), the explicit
  two-status-sources discipline (tracked yaml vs. derived artifact status) to protect Story 1.5 truthfulness,
  and full graceful degradation for missing/partial/malformed tracking data.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8

### Debug Log References

- Confirmed no code in `src/` reads `sprint-status.yaml` today — the only `sprint` hits are command-name
  strings in `BmadCommands.cs` (`sprint-status`/`sprint-planning` next-step suggestions).
- Confirmed the `.yaml` is invisible to `EnumerateSourceFiles` (`*.md` only) and to `FileWatcherService`
  (`*.md` filter + non-`.md` early-return in `Debounce`) — must be located by name and watched separately if
  live-reload is wanted.
- Confirmed YamlDotNet is already referenced/used (`MarkdownConverter` `DeserializerBuilder` +
  `Dictionary<string,object>` + `YamlException` catch) — no new package needed.
- Confirmed the status-color vocabulary is centralized in `StatusStyles` + CSS
  (done/active/review/ready/pending/deferred) and that `.status-badge.pending` is the one missing rule for
  `backlog`.
- Confirmed the new-page precedent (`RequirementsTemplater.RenderIndex` + `SiteGenerator.WriteRequirements`)
  and the `hasX`-gated nav pattern (`SiteNav.Build` with `hasAdrs`/`hasReadme`).
- Confirmed the dashboard already derives in-dev/review/up-next from story-artifact `Status:` (Now & Next),
  a different source than the yaml — the two must stay labeled and distinct (Story 1.5).
- Environment: use `py -3` for BMAD helper scripts on this Windows host.
- Planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~Sprint|FullyQualifiedName~HtmlTemplater|FullyQualifiedName~SiteNav|FullyQualifiedName~SiteGenerator|FullyQualifiedName~StatusStyles"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Task 1 (locate + parse yaml, robust) → Task 2 (model + `StatusStyles.ForSprint` mapping + `.status-badge.pending`
  CSS) → Task 3 (`SprintTemplater` page + `WriteSprint` + `SprintOutputPath`) → Task 4 (gated dashboard widget)
  → Task 5 (nav gating + generation plumbing + watch-limitation note) → Task 6 (tests) → Task 7 (real
  generation pass).
- Keep every change in the shared seams; prefer render-/generation-level string assertions over new public
  API; keep everything host-neutral and static-host-safe; keep the tracked vs. derived status sources
  labeled and distinct.

### Completion Notes List

- Third story of Epic 2. First feature to read `sprint-status.yaml`. Surfaces the tracking ledger as a
  first-class `sprint.html` page (epics + grouped stories with lifecycle badges + open action items) and a
  compact home Sprint Status widget, both gated on the file existing and degrading gracefully when it does
  not.
- Explicitly kept out: repointing existing panels at the yaml, reconciling the two status sources,
  burndown/velocity/history (Epic 3), general watch live-reload of `.yaml` (Epic 5), planning grouping/PRD
  prominence (2.4), iconography (2.5), comment annotations (2.6), a JS engine, dark mode.
- Coordination flags: reuse YamlDotNet (no new dep); map lifecycle onto the existing `StatusStyles` colors
  (+ one `.status-badge.pending` rule); resolve-or-plain-text links (never broken); keep tracked vs. derived
  status labeled and distinct (Story 1.5).

**Implementation completion (2026-07-07):**

- Delivered all 7 tasks. New `SprintStatus`/`SprintStatusParser`/`SprintTemplater`; `StatusStyles.ForSprint`/
  `SprintLabel`; `.status-badge.pending` CSS + a small sprint page/widget style block; `hasSprint`-gated nav
  in `SiteNav`; `SprintSourcePath`/`SprintAvailable`/`WriteSprint` + `_sprint` cache in `SiteGenerator`; a
  gated `AppendSprintPanel` widget in `HtmlTemplater`.
- **Parser robustness (key finding):** the real repo `sprint-status.yaml` carries
  `story_location: {project-root}/…`, whose unquoted `{` is INVALID YAML — a whole-document YamlDotNet parse
  throws on that sibling key. The parser therefore isolates the `development_status`/`action_items` blocks
  (via `ExtractTopLevelBlock`) and only feeds those to YamlDotNet, so a malformed unrelated key can't lose the
  ledger. Still no new dependency. Locked in by `Parse_SurvivesMalformedSiblingKeys`.
- **Two status sources kept distinct (Story 1.5):** the sprint page/widget read the tracked yaml lifecycle and
  are explicitly labeled "from sprint-status.yaml"; the derived Now & Next panel is untouched and both render
  together (verified: the live widget shows 2.1/2.2 "In review", 2.3 "In progress" while Now & Next stays).
- **Graceful degradation verified end-to-end:** with the yaml renamed away, no `sprint.html`, no widget, no
  Sprint nav, and no broken links; restored cleanly. Live generation: `sprint.html` with 7 epics / 35 grouped
  stories, mapped lifecycle badges, working epic/story links, no action-items section (live file has none),
  and zero broken local links. A11y: sprint page carries the skip link + exactly one `<main id="main-content">`.
- Watch-mode: took Task 5 option (a) — editing `sprint-status.yaml` in `--watch` does NOT live-reload (the
  watcher filters `*.md`); full-generate is fully covered. Documented as a known limitation; deferred to Epic 5.
- Tests: 337 green (was 335 pre-story). New `SprintStatusParserTests`, `SprintTemplaterTests`,
  `SiteGeneratorSprintTests`; added `ForSprint`/`SprintLabel` cases to `StatusStylesTests`, Sprint gating to
  `SiteNavTests`, and widget present/absent to `HtmlTemplaterTests`.
- Note: developed alongside a concurrent commit-heatmap→per-day-pages refactor in the working tree; all sprint
  work was layered additively on the shared seams (`SiteNav`/`SiteGenerator`/`HtmlTemplater`) without touching
  that refactor.

### File List

- _bmad-output/implementation-artifacts/2-3-sprint-status-page-and-dashboard-widget.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/SpecScribe/SprintStatus.cs (new)
- src/SpecScribe/SprintStatusParser.cs (new)
- src/SpecScribe/SprintTemplater.cs (new)
- src/SpecScribe/StatusStyles.cs (added ForSprint + SprintLabel)
- src/SpecScribe/SiteNav.cs (SprintOutputPath const, HasSprint, hasSprint-gated item/quick-link)
- src/SpecScribe/SiteGenerator.cs (_sprint cache, SprintSourcePath/SprintAvailable, WriteSprint, nav + index threading)
- src/SpecScribe/HtmlTemplater.cs (SprintStatus? threaded into RenderIndex/AppendDashboard; gated AppendSprintPanel; Now & Next → sprint board when active sprint)
- src/SpecScribe/BmadCommands.cs (public RenderCommandBar for the sprint command buttons — redesign)
- src/SpecScribe/EpicsTemplater.cs (undrafted story cards link to their placeholder page — redesign)
- src/SpecScribe/assets/specscribe.css (.status-badge.pending + sprint board/lane/card/toggle styles + .now-next-card.done)
- tests/SpecScribe.Tests/SprintStatusParserTests.cs (new)
- tests/SpecScribe.Tests/SprintTemplaterTests.cs (new)
- tests/SpecScribe.Tests/SiteGeneratorSprintTests.cs (new)
- tests/SpecScribe.Tests/StatusStylesTests.cs (ForSprint/SprintLabel cases)
- tests/SpecScribe.Tests/SiteNavTests.cs (Sprint gating cases)
- tests/SpecScribe.Tests/HtmlTemplaterTests.cs (sprint widget present/absent cases)
- docs/live/** (regenerated output — includes the new sprint.html + widget)

## Change Log

- 2026-07-08 (polish #8): Extended the owner removal to the retro **body's** `## Action Items` table —
  `RetroActionStyler` now drops the whole **Owner** column (header + every owner cell) as well as badging the
  Status cells (the earlier owner removal only covered the separate action-items page). Also de-duplicated the
  retro **header**: the h1 strips the redundant "Epic N Retrospective" prefix that the kicker line already
  carries (e.g. "Epic 1 Retrospective: High-Clarity BMad Portal Experience" → h1 "High-Clarity BMad Portal
  Experience"), falling back to "Retrospective" when nothing follows and leaving unrecognized titles untouched.
  429 tests pass.
- 2026-07-08 (polish #7): Removed the retro **Personas** section entirely (and the `Personas` classifier +
  `Icons.ForPersona` + persona CSS it needed) and dropped the **owner** pill from open action items — retro
  participants/owners are LLM-generated personas for the retrospective exercise, not real assignees, so they're
  noise once the doc exists. Reworked "Stories in this Epic" from custom rows into the shared Kanban
  **`.sprint-card`** (id + title, status color on the left border) laid out in a responsive
  `.retro-story-grid`, so they read exactly like the sprint board's cards. 429 tests pass.
- 2026-07-08 (polish #6): Humanized the retro pages and cross-linked them with stories/epics. Participants now
  render as a labeled **Personas** block: a new `Personas` classifier splits "Name (Role)" and maps the role to
  a css-class, driving a role icon (`Icons.ForPersona`) + a general-palette tint per person (NOT `--status-*` —
  personas aren't lifecycle states). `RetroParser` strips the leading duplicate `<h1>` (the styled header
  already carries the title). Each retro now lists **"Stories in this Epic"** (linked to each story's real or
  placeholder page, with a status badge + TOC entry), and every **story page** carries a reciprocal "Epic N
  retro →" back-link. **Epic pages** gained a retro affordance: a "View Epic N Retrospective →" link when a
  retro exists, or — for a complete epic (`StatusStyles.ForEpic == "done"`) with none — a `/bmad-retrospective`
  suggestion (omitted when the command isn't installed). The **action-items page** got a wider wrapper
  (`.action-items-wrap`, ~1040px) and, on debt-related items only (text mentions deferred / tech debt), an "In
  deferred-work backlog →" link to the Deferred Work page. Generation was reordered: `WriteRetros` split into
  `ParseRetros` (before the epics phase, so `EpicRetroMap` is available to epic/story pages) + `RenderRetroPages`
  (after, since the page needs the epics model). 429 tests pass; isolated-dir generation verified — Personas
  block with distinct role colors, no duplicate body `<h1>`, linked stories, epic/story retro links resolving
  (200), deferred link debt-only, and no new broken links on any touched page.
- 2026-07-07 (polish #5): Replaced the retrospectives **modal** with real, linkable pages to match the site's
  page-based UX. The sprint page's top strip collapsed to a single `.sprint-topbar` control row (title/subtitle +
  progress wheel + By-status/By-epic tabs + button cluster); the board view toggle now switches with
  `:has()` (`.sprint-page:has(#sv-epic:checked) .board-view-status{display:none}`) so the tabs no longer need to
  sit beside the views. Relabelled "Sprint commands"→**Commands ▾** and "Retrospectives"→a **Retros** link
  (`<a class="cmd-menu-toggle js-tip" href="retros.html">` with a rich `N retrospectives / Latest: …` tooltip,
  gated on retros existing). Open action items became a **⚑ N flag** link (`.sprint-flag`, shown only when open
  items exist) to a new page. New `RetroTemplater.RenderIndex` → **`retros.html`** (index-grid of retro cards)
  and new `ActionItemsTemplater` → **`action-items.html`** (per open item: action text, Epic/owner pills, status
  badge, a "From Epic N retrospective" link via `EpicRetroMap`, and a **"Resolve with AI"** command —
  `BmadCommands.RenderLabeledCommand` whose visible label is "Resolve with AI" but whose copy/deeplink payload is
  `/bmad-quick-dev Resolve this retrospective action item (Epic N): {action}`, omitted when the module exposes no
  `quick-dev`). `SiteGenerator` gained `WriteRetroIndex`/`WriteActionItems` (both gated); `SiteNav` gained
  `RetrosOutputPath`/`ActionItemsOutputPath`. The home "Retro Action Items — N open" callout now points at
  `action-items.html`. Deleted `RenderRetrospectivesModal` + all `.retro-menu`/`.retro-pop`/modal CSS.
  **`action-items.html` is intentionally NOT reference-linkified** — the linkifier would rewrite "Epic N" inside
  the resolve command's `data-copy` attribute into `<a>` tags and corrupt the copyable command. 423 tests pass;
  isolated-dir generation verified (single control row, `:has()` toggle swaps views, Retros→retros.html,
  ⚑→action-items.html, clean resolve payload, zero broken links).
- 2026-07-06: Created Story 2.3 as Epic 2's sprint-status story. Scoped: the first parse of
  `sprint-status.yaml` (via the already-referenced YamlDotNet) into an order-preserving model; a `sprint.html`
  page mirroring `RequirementsTemplater` that lists epics and their grouped stories with lifecycle status
  badges (backlog→ready-for-dev→in-progress→review→done, mapped onto the existing status colors) and
  resolvable-or-plain-text links, plus an open retrospective action-items section rendered only when present;
  a compact, gated home dashboard Sprint Status widget (per-stage counts + what's in progress + CTA to the
  page); a `hasSprint`-gated Sprint nav item/quick-link; the lifecycle→visual mapping added to the single
  `StatusStyles` seam plus the one missing `.status-badge.pending` CSS rule; the explicit two-status-sources
  discipline (tracked yaml vs. derived artifact `Status:`) to protect Story 1.5 truthfulness; and full
  graceful degradation for missing/partial/malformed tracking data (no page, nav, widget, or broken links).
  Documented the watch-mode `.yaml` live-reload limitation as a known, optionally-extendable boundary.
- 2026-07-07 (polish #4 + retrospectives): Made BMad retrospective notes (`epic-N-retro-*.md`) a first-class
  artifact class — new `RetroModel`/`RetroParser`/`RetroActionStyler`/`RetroTemplater` render each as a
  dedicated stylized page (epic-retro kicker, date, epic link, participant pills, status-badged Action Items
  table); `SiteGenerator` discovers/consumes them (out of the generic grid) via `WriteRetros` and caches an
  epic→retro map. The sprint page's bottom action-items list became a header-triggered **centered modal**
  ("Retrospectives ▾", `<details class="cmd-menu retro-menu">` + a pop-`::before` dim backdrop) listing past
  retros AND open action items, each item linked to its epic's retro page. Home: removed the redundant
  standalone "Sprint Status" pane; added a "Retrospectives" section + a "Retro Action Items — N open" callout
  beside Deferred Work. Fixed tooltip clipping by routing rich card/wheel tooltips through the body-level
  (never-clipped) `.ss-tooltip` JS node via a `data-tip` attribute (`white-space: pre-line`, multi-line: epic +
  story name + task info); cards now read "Story N.M" and dropped the epic badge; `Charts.Donut` gained a
  `segmentTitles` toggle so the tiny wheel shows one clean tooltip. 422 tests pass.
- 2026-07-07 (polish #3): Popout command badges size to content instead of stretching the panel; dropped the
  crammed center number from the tiny status wheel (a "N / M done" label carries it — new `Charts.Donut`
  `showCenterText` option); the home Now & Next board folds its "from sprint-status.yaml" label inline into the
  header and gains the shared status progress wheel (public `SprintTemplater.RenderProgressWheel`). 416 tests pass.
- 2026-07-07 (polish, post-review feedback #2): Compressed the sprint page's top chrome into one strip
  (title/subtitle + a compact donut + a header "Sprint commands ▾" popout that holds each command's
  description behind a native `<details>` dropdown), moved the header inside the board column so it aligns,
  and retuned the full-height board (thin scrollbars, fixed column headers). Redesigned the board cards
  (shared by the sprint + home boards): story id top-left, an "E{n}" epic badge top-right with an "Epic N"
  tooltip, and a hairline task-completion progress bar at the bottom (colored done/partial/empty, `data-tooltip`
  "N of M tasks done (P%)", gated on a task plan). Capped the home board at 3/column. Fixed the "not yet
  drafted" placeholder story page's edge-to-edge AC panel by wrapping it in `dashboard-narrow`. New public
  `BmadCommands.RenderCommandMenu`; extended the specscribe.js menu-dismissal to the new popout. 409 tests pass.
- 2026-07-07 (redesign, post-review feedback): Reworked the sprint surfaces into a Jira/Kanban board.
  The home **Now & Next** now *becomes* the sprint board when an active sprint exists — five lifecycle columns
  (Backlog → Done), each capped at 5 cards with a "+N more →" link to the full page (falls back to the derived
  Now & Next when no sprint); the donut overview widget stays (trimmed of its redundant list). The **sprint
  page** gained a Jira look: standard sprint command buttons (`sprint-planning`/`sprint-status`/
  `correct-course`/`retrospective` via a new public `BmadCommands.RenderCommandBar`), the lifecycle donut, and
  a **pure-CSS status↔epic toggle** (no JS) between a status-column board and per-epic swimlanes. Every board
  card links to its story page via `StoryEpicLinkifier.StoryPagePath` — including **placeholder pages for
  not-yet-drafted stories** — and the epic page now links undrafted stories to those placeholder pages too.
  Reused the `now-next-card`/`chart-panel`/`status-badge` vocabulary and the `--status-*` tokens (no new
  palette, no new dependency). Verified: 367 tests pass; live generation shows a 5-column board (home capped
  with "+20 more", sprint page uncapped), a working CSS toggle, 4 command buttons, and zero broken local links.
- 2026-07-07: Implemented Story 2.3. Added `SprintStatus`/`SprintStatusParser` (order-preserving
  `development_status` classification into epic/story/retrospective; robust block-isolated YamlDotNet parse
  that survives the invalid `story_location: {project-root}/…` sibling line in real tracking files; optional
  `last_updated`/`action_items`; missing/malformed → null). Added `StatusStyles.ForSprint`/`SprintLabel`
  mapping the lifecycle onto the existing six-stage colors + the one missing `.status-badge.pending` CSS rule.
  Added `SprintTemplater` rendering `sprint.html` (lifecycle-count summary, epics with tracked badges + links,
  stories grouped underneath with resolve-or-plain-text links, open action-items section only when non-empty),
  written by a new `SiteGenerator.WriteSprint` gated on a cached `_sprint`. Added a `hasSprint`-gated Sprint
  nav item + quick-link (`SiteNav`) and a gated home "Sprint Status" widget (`HtmlTemplater.AppendSprintPanel`)
  labeled "from sprint-status.yaml" and left the derived Now & Next panel untouched. Full graceful degradation
  and no-broken-links verified by generation-level tests and a real generation pass. 337 tests pass. Status →
  review.
