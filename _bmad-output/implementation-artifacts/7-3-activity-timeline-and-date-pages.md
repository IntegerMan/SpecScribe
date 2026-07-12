---
baseline_commit: 279d27e230ff391ce9f73bb1e438ca24775ceaa4
---

# Story 7.3: Activity Timeline and Date Pages

Status: in-progress

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want a timeline of project activity with per-date pages,
so that I can see what happened on any given day.

## Acceptance Criteria

1.
**Given** git history and artifact timestamps are available
**When** I view the timeline surface
**Then** activity is shown over time and each active date links to a date page
**And** dates with no activity are not misrepresented as activity. [Source: epics.md#Story 7.3 (lines 883-895); FR16, FR19]

2.
**Given** a date page
**When** I open it
**Then** it summarizes what happened that day (commits and artifact changes) and links back to the related epics, stories, code pages, and per-commit detail pages
**And** it degrades gracefully when history is unavailable. [Source: epics.md#Story 7.3 (lines 897-901); FR16, FR19]

---

## Developer Context

**Read this first — most of this story's foundation already exists. This is an enrichment + one-new-surface story, not a from-scratch build.** SpecScribe already generates per-day pages and an activity surface; 7.3 generalizes the day pages into true "date pages" and adds a dedicated chronological **timeline** surface on top of the same data. The two biggest risks are (a) reinventing infrastructure that already exists, and (b) over-reaching into deep-git territory that must stay out of the baseline path.

### What already exists (reuse, do not rebuild)

- **Per-day pages: `commits/{yyyy-MM-dd}.html`** — rendered by [CommitDayTemplater.RenderPage](../../src/SpecScribe/CommitDayTemplater.cs:12) and emitted by [SiteGenerator.GenerateCommitDaysInternal](../../src/SpecScribe/SiteGenerator.cs:364). Each already lists that day's commits (short hash, author-local time, author, subject) with prev/next links to adjacent active days, a synthesized shell (skip-link → single `<main id="main-content">`, nav, breadcrumb), and it already runs through `ApplyReferenceLinks` so "Story N.M"/"FR-9" mentions in commit subjects become links. **This IS the date page — generalize it, don't replace it.** Its own doc comment says so: *"this is the durable route future epics can enrich."* [Source: [CommitDayTemplater.cs:5-9](../../src/SpecScribe/CommitDayTemplater.cs)]
- **The activity-over-time visual: `Charts.CommitHeatmap`** — [Charts.cs:468](../../src/SpecScribe/Charts.cs) renders the GitHub-style heatmap on the dashboard and links each active cell to `commits/{date}.html`. Reuse it verbatim on the timeline page for the "activity shown over time" half of AC #1; do not author a second time chart.
- **The single day-set source of truth: `Charts.LinkedCommitDays`** — [Charts.cs:896](../../src/SpecScribe/Charts.cs). Both the heatmap (which cells link) and the generator (which pages exist) call this so a link can never point at a missing page. Your timeline's git spine and your date-page set must derive from the same helper so the three surfaces (heatmap, timeline, date pages) never disagree.
- **The git data: `GitPulse`** — [GitMetrics.cs:18](../../src/SpecScribe/GitMetrics.cs). Already carries `DailySeries` (ascending per-day counts) and `CommitsByDay` (per-day `CommitInfo` lists), threaded to the generator via `ProgressModel.Git`. The date pages already consume it. No new git call is needed for the commit half.
- **Artifact freshness (the "artifact timestamps" half): filesystem last-write dates** — the generator already stats source files in [BuildArtifactCoverage](../../src/SpecScribe/SiteGenerator.cs:885) (`File.GetLastWriteTime` → `DateOnly`, [line 903](../../src/SpecScribe/SiteGenerator.cs:903), future-skew-clamped). Today it stats only the ~8 canonical family files; this story widens that to all recognized source artifacts and groups them by day. This is the read-only, git-free "artifact timestamps" signal the AC names — distinct from git history, and the thing that keeps the timeline working when git is absent (AC #2's "degrades gracefully when history is unavailable").
- **Artifact → generated-page resolution: `BuildReferenceMap`** ([SiteGenerator.cs:1047](../../src/SpecScribe/SiteGenerator.cs)) and **`ResolveFamilyHref`** ([SiteGenerator.cs:941](../../src/SpecScribe/SiteGenerator.cs)) — the established source-path → output-URL maps (epics.md → epics.html, a consumed story artifact → its story page, everything else → its mirrored render). Reuse this to turn "artifact X changed this day" into a link to X's page (which satisfies AC #2's "links back to the related epics, stories").

### What 7.3 actually adds

1. **A new chronological timeline surface** (`timeline.html`, root-level): the heatmap at the top (reused) over a newest-first list of active dates, each with a compact activity summary and a link to its date page. This is the "timeline" the epic goal calls for ("turning ... dates into activity timelines") — a narrative/chronological complement to the calendar-grid heatmap.
2. **Date-page enrichment** (extend `CommitDayTemplater`): (a) link each commit's short hash to its **per-commit detail page** `commit/{shortHash}.html` when that page exists (guarded — Story 7.5's target); (b) add an **"Artifacts updated this day"** section listing that day's artifact changes as links to their pages; the existing commit-subject reference-linkification continues to cover epics/stories.
3. **A union date-page set**: date pages are generated for the union of commit days **and** artifact-last-modified days, so an artifact-only day (edit with no commit) still has a date page for the timeline to link to. Days with neither are never generated (AC #1: "dates with no activity are not misrepresented").
4. **Discovery/entry points**: a "View activity timeline →" link on the dashboard's Git Pulse panel (guarded on the timeline actually being generated) plus breadcrumbs.

### The core design in one paragraph

Widen the generator's per-file mtime gather to cover every recognized source artifact and group it into an `artifactsByDay` map (`DateOnly → list of (label, output-href)`), computed once alongside the existing coverage pass. Compute the **date-page day set** as the union of `Charts.LinkedCommitDays(...)` (git) and the keys of `artifactsByDay` (artifact timestamps), ascending. Rename the emission phase to `GenerateDatePagesInternal`, iterating that union: for each day, render the (generalized) `CommitDayTemplater` page passing the day's commits (may be empty) **and** the day's artifact changes (may be empty), with prev/next walking the union. In the templater, conditionally render the commit list and a new "Artifacts updated" section, link each commit hash to `commit/{shortHash}.html` **only when that page exists** (guarded via a cached commit-page set, plain `<code>` otherwise, exactly as today), and keep everything else intact. Then add a synthesized `TimelineTemplater.RenderPage` (cloned from `CommitDayTemplater`'s shell) that renders `Charts.CommitHeatmap` over a newest-first `<ol>`/`<ul>` of the union days, each linking to its date page with a "N commits · M artifacts updated" summary; emit it as its own gated phase and cache `_timelinePath` so the dashboard Git Pulse panel can render "View activity timeline →" only when it exists. The whole thing is pure SVG + links + lists — **no JavaScript** ([[charting-is-pure-svg-no-js]]) — never throws, uses invariant date formatting, neutral tokens (never `--status-*`), and HTML-escapes every derived string.

---

## Dependencies / Sequencing

- **No hard blocking dependency.** Unlike 7.2 (blocked on 7.1) or 3.8 (blocked on 3.2), 7.3 builds on already-merged/available infrastructure (the commit-day pages, heatmap, `GitPulse`, mtime gather). It is implementable **now** on its own.
- **Soft link targets — guard every outgoing link on target existence** (the established renderer discipline, same as 3.8/7.5/`ResolveSpecCompanions`):
  - **Per-commit detail pages** `commit/{shortHash}.html` are **Story 7.5** (status `backlog`, not merged). Link a commit hash to its page **only when that page exists**; otherwise keep the current plain `<code>` hash. 7.5's own story explicitly names this hook: *"link the commit's short hash from the existing `commits/{date}.html` day pages ... make it a link when the commit page exists."* [Source: [7-5 story:33](7-5-per-commit-detail-pages.md)] AC #2's "per-commit detail pages" link lights up automatically as 7.5 lands — do **not** block on it.
  - **Code pages** `code/<path>.html` are **Story 7.1** (status `ready-for-dev`, not merged). On the date page, code pages are reached **transitively via the per-commit detail pages** (a per-commit page lists its changed files and links them to code/file pages — 7.5/7.4 territory). Direct per-day changed-code-file links on the date page would require per-commit file data, which is the deep-git numstat path (see the boundary below) — **out of scope here.** AC #2's "code pages" is satisfied by the reachable graph (date page → per-commit page → code page), consistent with how 3.8 guards code links.
- **Relationship to 3.8 (Git Insights Hub):** 3.8 also reuses `Charts.CommitHeatmap` for "activity over time" and also links to commit-day pages. Keep them complementary: 3.8 is the deep-git-gated aggregate hub; 7.3's timeline is a baseline, always-available chronological surface. Do not fold one into the other. If both ship, both may link to the same date pages — that's fine.
- **Relationship to 7.5:** 7.5 links date pages → per-commit pages (it modifies `CommitDayTemplater` to link the hash). If 7.5 lands first, that hook may already exist — reuse it, don't duplicate. If this story lands first, add the guarded hash-link here and 7.5 consumes it. Either way there is **one** hash-link seam.

### Scope boundary (read carefully)

- **IN scope:** the `timeline.html` chronological surface (heatmap + dated list); generalizing the commit-day page into a date page (union day set, "Artifacts updated" section, guarded per-commit hash links); widening the mtime gather to all artifacts; the dashboard "View activity timeline →" entry link; graceful degradation when git and/or artifacts are absent.
- **OUT of scope:**
  - **Per-day changed *code*-file listings and direct code-page links on the date page.** That needs per-commit file data, which is the shared `--numstat` deep-git path owned by Story 3.2 and extended by 7.4/7.5 — **gated on `--deep-git`** ([[deep-git-single-numstat-path]]). 7.3 must work at baseline (AC #2 degrades gracefully, does not require `--deep-git`), so do **not** add a second per-commit-file git call here. Code pages are reached transitively (above).
  - **Per-commit detail pages themselves** (7.5), **code pages themselves** (7.1), and the **deep-git hub** (3.8). This story only *links* to them (guarded).
  - **Any JavaScript.** Unlike 3.8 (which is the one sanctioned progressive-enhancement surface), 7.3 is pure SVG + links + native markup. Do not add table sort/filter or any script. [[charting-is-pure-svg-no-js]]
  - **A top-nav "Timeline" entry.** Follow 7.1/3.8's call: nav is built before git is computed ([SiteGenerator.cs:66](../../src/SpecScribe/SiteGenerator.cs)), so a nav item risks dangling. Reach the timeline from the dashboard Git Pulse panel + breadcrumb instead. (Optional stretch only if you can guarantee it never points at a missing page.)

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Derive the date-page day set from one shared computation.** Union of `Charts.LinkedCommitDays(git.DailySeries, git.CommitsByDay, today)` and the keys of the new `artifactsByDay` map, sorted ascending. This same union drives the date pages (which exist), the timeline list (which links), and the prev/next nav — so no surface can link a day that has no page. Model the "one source of truth for the day set" discipline on how `LinkedCommitDays` is shared today. [Source: [Charts.cs:892-907](../../src/SpecScribe/Charts.cs)]
- **Widen the mtime gather, reusing the existing pattern.** In the generator, build `artifactsByDay: IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>>` by statting each *recognized* source artifact (`File.GetLastWriteTime` → `DateOnly`, future-skew-clamped to `today` exactly as [BuildArtifactCoverage does at line 903/149](../../src/SpecScribe/SiteGenerator.cs)) and mapping its source path to its output page via the same resolution `BuildReferenceMap`/`ResolveFamilyHref` use. Never-throw per file (a single unreadable file degrades that one entry, never aborts the pass — AD-4). Keep the gather pure/testable where possible (IO in the generator, grouping logic in a pure helper). [Source: [SiteGenerator.cs:885-914](../../src/SpecScribe/SiteGenerator.cs), [1047-1074](../../src/SpecScribe/SiteGenerator.cs)]
  - **"Recognized artifact" = a source path that resolves to a generated page** (so the link is real). Reuse the reference map's key set rather than inventing a new file filter. Label = a human-readable name (the epic/story id or the file's display title, mirroring how the artifact is titled elsewhere); do not surface raw paths where a page title exists.
- **Generalize `CommitDayTemplater` into a date-page templater** (rename optional; keep the `commits/{date}.html` output path to avoid rippling the heatmap/`LinkedCommitDays` hrefs). Extend `RenderPage` to also accept the day's `artifactsByDay` entry and a `commitPageExists` predicate/set. Render:
  - The commit list **only when non-empty** (a git-absent, artifact-only day omits it — no empty "0 commits" list). Keep the existing row shape.
  - A new **"Artifacts updated"** section **only when non-empty**: a semantic list of links to each changed artifact's page (`<a href>` via the resolved href; escaped label). Omit the section (no empty heading) when the day has no artifact changes.
  - Each commit's short hash as an `<a href="{prefix}commit/{shortHash}.html">` **only when `commitPageExists(shortHash)`**; otherwise the current plain `<code class="commit-hash">`. Never a dead link. [Source: [CommitDayTemplater.cs:50](../../src/SpecScribe/CommitDayTemplater.cs), [7-5 story:33](7-5-per-commit-detail-pages.md)]
  - Adjust the header pill / heading so a day reads sensibly whether it has commits, artifacts, or both (e.g. "Commits on {date}" when commits exist; a neutral "Activity on {date}" when it's artifact-only). Keep the single-`<main>`/skip-link/breadcrumb a11y contract intact. [Source: [CommitDayTemplater.cs:36-42](../../src/SpecScribe/CommitDayTemplater.cs)]
- **Rename the emission method to `GenerateDatePagesInternal`** (from `GenerateCommitDaysInternal`), iterating the union day set with prev/next across the union (not just commit days). Preserve: wipe+recreate the `commits/` dir, `File.WriteAllText`, `ApplyReferenceLinks(html, outputRelative)` per page (so subject/artifact references linkify), the `try/catch → GenerationEvent(Error, …)` per page, and recording entries (extend `CommitDayEntry` or add a sibling `DatePageEntry` capturing the day + path so the timeline and tests can enumerate them). [Source: [SiteGenerator.cs:364-405](../../src/SpecScribe/SiteGenerator.cs)]
- **Guard the per-commit hash link with a cached commit-page set.** 7.5 will emit `commit/{shortHash}.html` and (like 7.1's `_codePages`) should cache the set of generated commit hashes. Consume that set when present; when 7.5 hasn't landed, the set is empty and every hash stays plain `<code>`. Do not probe the filesystem per hash — use the cached set (mirrors 3.8/7.1's guarded-link discipline). If 7.5's cache field doesn't exist yet, add a small nullable `IReadOnlySet<string>? _commitPages` defaulting to empty so this story is correct today and 7.5 populates it later.
- **Add `TimelineTemplater.RenderPage`** — a synthesized page cloned from `CommitDayTemplater`'s shell (`PathUtil.RenderHeadOpen` + `nav.RenderNavBar` + `SiteNav.RenderBreadcrumb("Home / Timeline")` + one `<main id="main-content">` + footer). Render `Charts.CommitHeatmap(git.DailySeries, git.CommitsByDay)` (when git present) at the top, then a **newest-first** dated list: each row = `DReadable(day)` linking to `commits/{D(day)}.html`, plus a summary like `"{n} {commit|commits}"` and/or `"{m} artifacts updated"` from the union data. Skip the heatmap gracefully (git absent) and still render the artifact-driven list. [Source: [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs), [Charts.cs:468](../../src/SpecScribe/Charts.cs)]
- **Add a `SiteNav.TimelineOutputPath = "timeline.html"` constant** (root-level, matching `SprintOutputPath`/`StructureOutputPath`/`DeepAnalyticsOutputPath` conventions). [Source: [SiteNav.cs:10-28](../../src/SpecScribe/SiteNav.cs)]
- **Gate + cache the timeline like the deep-analytics/commit-days phases.** Generate it in `GenerateAll` right after the date-pages phase, only when there is data to show (git pulse present OR `artifactsByDay` non-empty). Cache `_timelinePath` (null when not generated) so `WriteIndex`/the Git Pulse panel renders the "View activity timeline →" link **only when the page exists**. Mirror the `if (_progress?.Git is { } gitPulse)` gate + the `deep-analytics` cache-and-link pattern. [Source: [SiteGenerator.cs:129-154](../../src/SpecScribe/SiteGenerator.cs), [SiteNav.cs:19-22](../../src/SpecScribe/SiteNav.cs)]
- **Add a `GenerationPhase.Timeline`** (and keep date pages under the existing `CommitDays` phase, or rename it to `DatePages` — pick one and update the `Descriptions` map). Wrap `BeginPhase`/`EndPhase` around the new work. [Source: [GenerationReporter.cs:5,20-28](../../src/SpecScribe/GenerationReporter.cs)]
- **Invariant, culture-safe formatting everywhere.** Reuse `Charts.D` (ISO, for filenames/hrefs) and `Charts.DReadable` (display) — never a culture-sensitive format; this renderer was bitten by non-Gregorian date parsing before. Reuse `Charts.Plural` for count labels ("1 commit"/"2 commits"). [Source: [Charts.cs:884-890,980-982](../../src/SpecScribe/Charts.cs), [GitMetrics.cs:100-106](../../src/SpecScribe/GitMetrics.cs)]
- **Neutral tokens only.** Timeline/date styling uses the `--ink`/`--border`/`--parchment`-family chart tokens like the git panels — **never** `--status-*` lifecycle tokens (activity is not a backlog/ready/done status). [[specscribe-status-token-system]]
- **HTML-escape every derived string** via `PathUtil.Html(...)` — dates, counts, artifact labels, hrefs, and (already) commit subjects/authors are all injection surfaces.
- **Degrade non-fatally at every branch (NFR-2, AC #2).** No git and no artifact mtimes → no timeline page, no date pages, no dashboard link, no error, baseline site still generates. Git present but `CommitsByDay` empty, or artifacts present but none resolve → render what exists; omit empty sections; never throw.

### DON'T

- **DON'T add a second git invocation or reach into `--numstat`/deep-git data.** The commit half comes from the existing `GitPulse`; the artifact half comes from filesystem mtimes. Per-commit file lists are the deep-git path (3.2), gated on `--deep-git` and owned by 7.4/7.5. [[deep-git-single-numstat-path]]
- **DON'T add any JavaScript.** Pure SVG + links + native lists. [[charting-is-pure-svg-no-js]]
- **DON'T change the `commits/{date}.html` output path or the heatmap's hrefs.** Keeping the path stable means the heatmap, `LinkedCommitDays`, and any 7.5 hooks keep working untouched; the page is *conceptually* a date page even though the dir is named `commits/`. If you feel strongly about a `dates/` path, that's a larger ripple (heatmap + `LinkedCommitDays` + 7.5) — not worth it for this story.
- **DON'T co-opt `--status-*` tokens** for timeline/activity styling. [[specscribe-status-token-system]]
- **DON'T misrepresent inactive dates (AC #1).** Only union days (real commit OR real artifact activity) get list rows and pages. Never emit a row/page for a day with no activity, and never render a future-dated day (clock/timezone skew) — apply the same future-skew guard the heatmap uses (`day <= today`). [Source: [Charts.cs:480-483,562](../../src/SpecScribe/Charts.cs)]
- **DON'T add a dangling top-nav entry** (nav is built before git is computed). Dashboard link + breadcrumb only. [Source: [SiteGenerator.cs:66](../../src/SpecScribe/SiteGenerator.cs), [3-8 story:81,145-149](3-8-git-insights-hub-page.md)]
- **DON'T write back to source** (local-first, read-only invariant). Reading git and file mtimes is read-only; emit only under `OutputRoot`. [Source: ARCHITECTURE-SPINE.md — Inherited Invariants]
- **DON'T leave stale pages.** The full-rebuild slate-wipe of `OutputRoot` ([SiteGenerator.cs:53-56](../../src/SpecScribe/SiteGenerator.cs)) plus wiping/recreating `commits/` each pass already covers staleness; ensure a run that produces no timeline leaves no stale `timeline.html` (the full wipe handles `GenerateAll`; note it for any partial/watch path).

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md); [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md)]:

- **AD-4 — optional insight providers are additive, non-blocking, never own baseline success.** The timeline and the artifact-mtime signal are additive: any failure yields no page/no section and the baseline site still generates. [#AD-4]
- **Local-only, read-only** — git and file-mtime reads are read-only; output lands only under `OutputRoot`; no source mutation. [Inherited Invariants]
- **Graceful degradation is contractual (NFR-2, AC #2)** — absent/partial git and/or artifact data degrades to omitted pages/sections, never a thrown exception or a broken link.
- **Baseline stays responsive (NFR-1)** — no new git process; the mtime widening is O(source files) `stat` calls already bounded by the enumerated source set; the timeline is O(active days). No `--deep-git` dependency.
- **Accessibility is part of the rendering contract (NFR-6, UX-DR16/17)** — the timeline and date pages carry the site skip-link → single `<main id="main-content">` landmark, nav, and breadcrumb like every page; the heatmap keeps its whole-chart `aria-label` and per-link accessible names; lists use semantic markup; links carry meaningful visible text (dates/titles), never "click here"; no color-only cues. [Source: [CommitDayTemplater.cs:36-42](../../src/SpecScribe/CommitDayTemplater.cs); Stories 1.4/1.5]
- **Seed, not invariant** — extend the monolith in place (a peer `TimelineTemplater`, extensions to `CommitDayTemplater`/`GitMetrics`/`SiteGenerator`); do **not** introduce the aspirational `IInsightProvider`/`SpecScribe.Core` package split. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant; echoed in 3.1/3.2/3.8/7.1 Dev Notes]
- **Pure-SVG / no-JS charts** — timeline visuals are the reused heatmap SVG plus native links/lists; the one small existing tooltip/copy script is not extended here. [[charting-is-pure-svg-no-js]]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **Existing infra to reuse (do not reinvent):**
  - [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) — `GitPulse` (`DailySeries`, `CommitsByDay`), `CommitInfo`; the never-throw / pure-parser discipline (no new git call needed).
  - [Charts.cs](../../src/SpecScribe/Charts.cs) — `CommitHeatmap` (reuse for activity-over-time), `LinkedCommitDays` (the shared day-set source), `D`/`DReadable` date formatters, `Plural`, `Html`, `F`.
  - [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs) — the synthesized-page templater to generalize (shell + a11y contract + escaping + prev/next).
  - [PathUtil.cs](../../src/SpecScribe/PathUtil.cs) — `RenderHeadOpen`/`RenderFooter`/`Html`/`NormalizeSlashes`/`RelativePrefix`/`ToOutputRelative`.
  - [SiteNav.cs](../../src/SpecScribe/SiteNav.cs) — `RenderNavBar`/`RenderBreadcrumb`; output-path constants live here.
  - [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs) — `GenerateCommitDaysInternal` (generalize), `BuildArtifactCoverage` mtime gather (widen), `BuildReferenceMap`/`ResolveFamilyHref` (artifact→page links), the deep-analytics cache-and-link pattern (mirror for `_timelinePath`), the phase-gate + wipe orchestration.
  - [GenerationReporter.cs](../../src/SpecScribe/GenerationReporter.cs) — `GenerationPhase` enum + `Descriptions` map to extend.
  - [ForgeOptions.cs](../../src/SpecScribe/ForgeOptions.cs) — `SourceRoot`/`OutputRoot`/`StylesheetName`/`ScriptName`.

---

## File Structure Requirements

**New files:**

- `src/SpecScribe/TimelineTemplater.cs` — `static RenderPage(GitPulse? git, IReadOnlyList<...> unionDaysNewestFirst, IReadOnlyDictionary<DateOnly, ...> commitsByDay, IReadOnlyDictionary<DateOnly, IReadOnlyList<(string,string)>> artifactsByDay, SiteNav nav)` (or a small view-model record) returning the full HTML. Model directly on [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs) (own shell, nav/breadcrumb, one `<main>`, footer). Renders the reused heatmap + the newest-first dated list. IO-free and unit-testable.
- `tests/SpecScribe.Tests/TimelineTemplaterTests.cs` — unit (no IO): a11y contract (skip-link first, single `<main id="main-content">`, `Home / Timeline` breadcrumb); each union day renders a row linking to `commits/{date}.html` with a correct summary; newest-first order; git-absent still renders the artifact-driven list (no heatmap); escaping; empty union → friendly note or no list (no crash).
- `tests/SpecScribe.Tests/CommitDayTemplaterTests.cs` (if not already present, else extend) — the "Artifacts updated" section, the guarded per-commit hash link (link when present, plain `<code>` when absent), artifact-only day (no commit list, neutral heading), escaping of artifact labels.

**Modified files (read fully before editing):**

- `src/SpecScribe/CommitDayTemplater.cs` — extend `RenderPage` for the "Artifacts updated" section, the guarded hash link, and the commits-optional / artifacts-optional / heading logic. **Preserve** the shell, prev/next nav, and existing row shape.
- `src/SpecScribe/SiteGenerator.cs` — (1) widen the mtime gather into an `artifactsByDay` map (reuse the reference map for hrefs); (2) rename `GenerateCommitDaysInternal` → `GenerateDatePagesInternal`, iterate the union day set, pass artifacts + `commitPageExists`; (3) add `GenerateTimelineInternal` gated on data present, cache `_timelinePath`; (4) add a nullable `IReadOnlySet<string>? _commitPages` (empty default) for the guarded hash link (populated by 7.5 later). **Preserve** the full-rebuild wipe, per-phase reporter calls, `commits/` dir wipe, `ApplyReferenceLinks` per page, and `WriteIndex` running last.
- `src/SpecScribe/CommitDayEntry.cs` — either generalize to `DatePageEntry(DateOnly Date, string OutputRelativePath)` or add a sibling record; keep the generator's ability to enumerate generated date pages for tests/timeline. [Source: [CommitDayEntry.cs](../../src/SpecScribe/CommitDayEntry.cs)]
- `src/SpecScribe/SiteNav.cs` — add `TimelineOutputPath = "timeline.html"` constant.
- `src/SpecScribe/GenerationReporter.cs` — add `Timeline` (and optionally rename `CommitDays` → `DatePages`) to the enum + `Descriptions`.
- `src/SpecScribe/Charts.cs` and/or `src/SpecScribe/HtmlTemplater.cs` — add the dashboard Git Pulse panel's "View activity timeline →" link, rendered **only** when `_timelinePath` is set. Coordinate with Story 3.1's consolidated Git Pulse panel and 3.8's "View all git insights →" link (they sit in the same panel — keep them consistent and both guarded).
- `src/SpecScribe/assets/specscribe.css` — add `.timeline`/`.timeline-list`/date-row styles using neutral tokens (not `--status-*`); wide content scrolls inside its own container, never the body. **Edit only `src/SpecScribe/assets/specscribe.css`** — any `docs/live` copy is vestigial/gitignored ([[generate-output-dir-is-specscribeoutput]]). If `StylesheetTests.cs` asserts class presence, add companion assertions.

**Output layout:**

- `SpecScribeOutput/timeline.html` (root-level standalone, like `sprint.html`/`structure.html`).
- `SpecScribeOutput/commits/{yyyy-MM-dd}.html` — unchanged path, now generalized to date pages (union of commit + artifact days). Links to `commit/{hash}.html` (7.5, guarded) are root-relative from `commits/` (i.e. `../commit/{hash}.html` via `PathUtil.RelativePrefix`).

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Follow the temp-`_bmad-output`-tree + `AssertNoErrors(gen.GenerateAll())` pattern for generation tests ([SiteGeneratorTraceabilityTests.cs:106-154](../../tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs)) and direct `RenderPage` calls for templater unit tests. Add pure-grouping tests for any pure `artifactsByDay`/union helper (mirror `GitMetricsTests`' feed-input-assert-structure style).

**`TimelineTemplaterTests` (unit, no IO):**
- Renders the a11y contract (skip-link first, single `<main id="main-content">`, nav, `Home / Timeline` breadcrumb).
- Each union day renders one list row linking to `commits/{D(day)}.html` with the right summary (`"3 commits"`, `"1 commit"`, `"2 artifacts updated"`, or both), newest-first.
- Git present → heatmap SVG appears; git absent → no heatmap but the artifact-driven list still renders.
- Escaping: a day summary / any derived label with `<`/`&`/`"` is escaped.
- Empty union → a friendly "No activity to show yet." note (or no list), no crash.

**`CommitDayTemplater` / date-page tests (unit, no IO):**
- **Guarded per-commit link:** with a commit-page set containing the hash → the hash renders as `<a href="{prefix}commit/{hash}.html">`; with an empty set → plain `<code class="commit-hash">` (no dead link).
- **Artifacts-updated section:** given a day's artifact list → a semantic list of `<a href>` to each artifact page with escaped labels; empty list → section omitted (no empty heading).
- **Artifact-only day:** commits empty, artifacts present → no commit list, neutral "Activity on {date}" heading, artifacts section present.
- **Escaping:** artifact label / commit subject with markup-like content is escaped.

**Generation-level tests (extend a `SiteGenerator…Tests`):**
- **Timeline exists when data does:** a temp repo with git history (or artifact mtimes) → `GenerateAll` emits `timeline.html` (positive control: contains a known date row + a `commits/…html` link) and the dashboard renders "View activity timeline →".
- **Union day set:** a day with an artifact change but no commit still gets a `commits/{date}.html` date page and a timeline row; a day with neither gets **neither** (AC #1 — no misrepresented inactivity).
- **Guarded links resolve as targets land:** with no `commit/` pages, hashes are plain; simulate a populated `_commitPages` set → hashes link. (No dead links in either case.)
- **Graceful degradation (AC #2):** no git and no artifact mtimes → no `timeline.html`, no date pages, no dashboard link, no `Error` outcome, rest of site generates.
- **Determinism:** two runs over the same input produce identical `timeline.html` and identical date pages.
- **Regression:** the heatmap still links to `commits/{date}.html`; existing commit-day page content (rows, prev/next, subject linkification) is unchanged for commit-bearing days.

**Run:** `dotnet test` from repo root. Then a real generation pass against this repo: `dotnet run --project src/SpecScribe` — output lands in the default `SpecScribeOutput/`; **do not** pass `--output docs/live` (vestigial/gitignored — [[generate-output-dir-is-specscribeoutput]]). Open `SpecScribeOutput/timeline.html`: confirm the heatmap renders, the dated list is newest-first, each date links to its page, and inactive dates are absent. Open a `commits/{date}.html` date page: confirm commits list, the "Artifacts updated" section (if any that day), and that commit hashes are plain `<code>` today (they will become links once 7.5 lands). Confirm the dashboard Git Pulse panel shows "View activity timeline →". Disable git (or run against a non-git dir with only artifacts) and confirm the timeline degrades to the artifact-driven list with no error.

---

## Previous Story Intelligence

- **Story 7.2 (Source-Citation & Comment Linking) — immediate predecessor in Epic 7.** Reinforced two lessons that bite this renderer: **escaping** and **stale-output** are the two most common regressions (both covered in the test list), and the codebase's linking discipline is *guard every outgoing link on target existence, degrade to plain text/`<code>`, never a dead link* — which is exactly how 7.3 must treat the per-commit hash link and the artifact links. [Source: [7-2 story:184](7-2-source-citation-and-comment-linking-to-code-pages.md)]
- **Story 7.5 (Per-Commit Detail Pages) — your link target and the mirror-image seam.** 7.5 emits `commit/{shortHash}.html` and explicitly plans to link the hash from these date pages. Coordinate: one guarded hash-link seam, whoever lands first. 7.5 also confirms per-commit file/churn data is the `--numstat` (deep) path — reinforcing why per-day code-file listings stay out of 7.3's baseline scope. [Source: [7-5 story:18-23,33](7-5-per-commit-detail-pages.md)]
- **Story 3.8 (Git Insights Hub) — sibling that reuses the same primitives.** It also reuses `CommitHeatmap` for "activity over time", also guards detail-page links on existence, and also caches an entry so the dashboard renders a "View all →" link only when the page exists. Mirror its `_gitInsightsPath` pattern for your `_timelinePath`, and keep the two dashboard links consistent. [Source: [3-8 story:31,64,81,147](3-8-git-insights-hub-page.md)]
- **Stories 3.1/3.2 (Git pipeline) — the data you consume.** 3.1 established `GitPulse`/`GitMetrics.TryCompute` (never-throw, bounded, invariant dates, pure parsers, degrade-a-signal-not-the-pulse); 3.2 established the `--deep-git` gate and the numstat path you must NOT duplicate. The commit half of 7.3 rides 3.1's already-computed pulse. [Source: [3-1 story](3-1-baseline-git-pulse-insights-on-dashboard.md), [3-2 story](3-2-optional-deep-git-analytics-controls.md)]
- **Recurring lessons to apply here:** invariant/culture-safe formatting for every derived date/count ([GitMetrics.cs:100-106](../../src/SpecScribe/GitMetrics.cs)); extend the monolith in place (no package split); grep in-flight/recent story files for stale repeated commands before closing (the `--output docs/live` foot-gun burned three Epic 2 stories). [Source: [sprint-status.yaml:126-129](sprint-status.yaml); project memory]

## Git Intelligence Summary

Recent history is Epic-3 git-insights and Epic-7 planning churn (`Adjustments and planning`, `Status`, `3.2 / 3.3`, `Deep commit analysis`, `3.2`). The commit-day pages, heatmap, and `GitPulse` this story generalizes are already on `main` (Story 3.1 / the commit-days phase). No timeline code exists yet. Story 7.5's `commit/` pages and 7.1's `code/` pages are not merged, which is why every outgoing link here is guarded — the timeline is correct today and lights up as those land.

> **Worktree note:** if you run this in a worktree, edit files at the worktree path — do **not** re-root relative paths back at `C:\Dev\SpecScribe`. `main` has a background auto-committer. [[worktree-edits-must-target-worktree-path]]

## Latest Technical Information

No external libraries or APIs are introduced — nothing to version-check. Platform notes:
- **`System.IO.File.GetLastWriteTime`** returns local time; convert to `DateOnly` and clamp future-dated results to `today` exactly as `BuildArtifactCoverage` already does (clock/timezone skew must not read as "edited in the future"). [Source: [SiteGenerator.cs:903](../../src/SpecScribe/SiteGenerator.cs), [ArtifactCoverage.cs:147-150](../../src/SpecScribe/ArtifactCoverage.cs)]
- **Filesystem mtime is a coarse signal** (a fresh clone stamps every file with checkout time). That's acceptable and honest for "artifact timestamps" (it's a timestamp, not full history) and it is what keeps the timeline working with no git. The richer "per-day artifact change history from git" would require per-commit file data = the deep-git numstat path, deliberately out of scope (see Open Questions).

## Project Context Reference

- FR16 (activity timeline + per-date pages) and FR19 (Epic 7 code/git exploration surface): [Source: [epics.md:841](../../_bmad-output/planning-artifacts/epics.md)]
- Epic 7 goal ("turning ... dates into activity timelines") + Story 7.3 ACs: [Source: [epics.md:837-901](../../_bmad-output/planning-artifacts/epics.md)]
- Existing date-page + heatmap + day-set infra: [Source: [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs), [Charts.cs:468,896](../../src/SpecScribe/Charts.cs), [SiteGenerator.cs:364-405](../../src/SpecScribe/SiteGenerator.cs)]
- Artifact freshness / mtime gather to widen: [Source: [SiteGenerator.cs:885-914](../../src/SpecScribe/SiteGenerator.cs), [ArtifactCoverage.cs](../../src/SpecScribe/ArtifactCoverage.cs)]
- Guarded-link + cache-and-dashboard-link precedents: [Source: [3-8 story](3-8-git-insights-hub-page.md), [7-1 story](7-1-in-portal-code-file-browsing.md), [SiteGenerator.cs:139-154](../../src/SpecScribe/SiteGenerator.cs)]
- Architecture invariants (AD-4, local/read-only, graceful degradation, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)]
- Project memory: [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[deep-git-single-numstat-path]], [[generate-output-dir-is-specscribeoutput]], [[worktree-edits-must-target-worktree-path]], [[epic-7-code-link-strategy]], [[story-1-4-a11y-seams-for-1-5]].

---

## Tasks / Subtasks

- [ ] **Task 1 — Artifact-timestamp day map (AC: #1, #2)**
  - [ ] In `SiteGenerator`, widen the mtime gather beyond canonical families: stat every *recognized* source artifact (one that resolves to a generated page via the reference map), `File.GetLastWriteTime` → `DateOnly`, future-skew-clamped to `today`, never-throw per file.
  - [ ] Build `artifactsByDay: IReadOnlyDictionary<DateOnly, IReadOnlyList<(string Label, string Href)>>` mapping each source path to its output page (reuse `BuildReferenceMap`/`ResolveFamilyHref`). Keep the grouping logic in a pure, testable helper.
- [ ] **Task 2 — Union day set + generalized date pages (AC: #1, #2)**
  - [ ] Compute the date-page day set = `LinkedCommitDays(...)` ∪ `artifactsByDay.Keys`, ascending, `<= today`.
  - [ ] Rename `GenerateCommitDaysInternal` → `GenerateDatePagesInternal`; iterate the union with prev/next across the union; pass each day's commits (maybe empty) + artifacts (maybe empty) + `commitPageExists`. Preserve wipe/recreate `commits/`, `ApplyReferenceLinks`, per-page try/catch, entry recording.
  - [ ] Extend/generalize `CommitDayEntry` → date-page entry so the timeline + tests can enumerate generated pages.
- [ ] **Task 3 — Date-page templater enrichment (AC: #2)**
  - [ ] Extend `CommitDayTemplater.RenderPage`: commit list only when non-empty; new "Artifacts updated" section (links to artifact pages) only when non-empty; neutral heading for artifact-only days; keep the a11y shell + prev/next.
  - [ ] Guarded per-commit hash link: `<a href="{prefix}commit/{shortHash}.html">` when `commitPageExists(hash)`, else plain `<code>`. Add nullable `_commitPages` set (empty default) in the generator for 7.5 to populate.
- [ ] **Task 4 — Timeline surface (AC: #1)**
  - [ ] Add `TimelineTemplater.RenderPage` (cloned shell): reused `Charts.CommitHeatmap` (git present) over a newest-first dated list, each row linking to its date page with a "N commits · M artifacts updated" summary. Degrade to artifact-only list when git absent.
  - [ ] Add `SiteNav.TimelineOutputPath`; add `GenerateTimelineInternal` gated on data present; cache `_timelinePath`; add `GenerationPhase.Timeline` + description.
- [ ] **Task 5 — Discovery / entry point (AC: #1)**
  - [ ] Dashboard Git Pulse panel "View activity timeline →" link, rendered only when `_timelinePath` is set (mirror the deep-analytics guarded link). Breadcrumb `Home / Timeline` on the page.
- [ ] **Task 6 — Styling (AC: #1, #2)**
  - [ ] Add `.timeline`/date-row/artifacts-section styles in `src/SpecScribe/assets/specscribe.css` using neutral tokens (not `--status-*`); wide content scrolls in its own container. Update `StylesheetTests` if it asserts class presence. **No JavaScript.**
- [ ] **Task 7 — Tests (AC: #1, #2)**
  - [ ] `TimelineTemplaterTests`: a11y contract, per-day rows + links, newest-first, git-absent fallback, escaping, empty union.
  - [ ] Date-page tests: guarded hash link (present/absent), artifacts-updated section (present/empty), artifact-only day, escaping.
  - [ ] Generation-level: timeline exists when data does + dashboard link; union day set (artifact-only day gets a page/row, dead day gets neither); graceful degradation (no git + no artifacts → nothing, no error); determinism; heatmap-link + commit-day regression.
- [ ] **Task 8 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green. Real generate (default `SpecScribeOutput/`): timeline heatmap + newest-first dated list, each date links to its page, inactive dates absent; date page shows commits + artifacts-updated; dashboard shows "View activity timeline →". Verify graceful degradation with git disabled.

## Dev Notes

- **The single biggest win is reuse.** The date page, heatmap, day-set helper, git pulse, and mtime gather all exist. Your net-new code is one templater (`TimelineTemplater`), one map (`artifactsByDay`), two templater sections (artifacts + guarded hash link), and the phase/gate/cache/dashboard-link wiring. Resist rebuilding any of the reused pieces.
- **One day-set source of truth.** The heatmap, the timeline list, and the generated date pages must all derive from the same union computation, exactly as `LinkedCommitDays` keeps heatmap-links and generated-pages in lockstep today. A drift there = broken links.
- **Guard, don't probe.** Per-commit and code links resolve only when their target pages exist; use cached sets (`_commitPages`, and the transitive path via per-commit pages), never filesystem probes. The page is correct today and improves as 7.5/7.1 land.
- **Baseline, not deep-git.** No second git call, no numstat. The artifact half is filesystem mtimes; the commit half is the existing pulse. Per-day *code-file* listings are deep-git territory (3.2/7.4/7.5) — out of scope. [[deep-git-single-numstat-path]]
- **Honesty about inactivity.** Only real activity gets rows and pages; future-skew days are suppressed; empty sections are omitted, not shown as "0". [Source: [Charts.cs:480-483,562](../../src/SpecScribe/Charts.cs)]
- **No JS.** [[charting-is-pure-svg-no-js]] Neutral tokens only. [[specscribe-status-token-system]]

### Project Structure Notes

- New `TimelineTemplater` sits beside `CommitDayTemplater`/`DeepAnalyticsTemplater` as a peer synthesized-page templater; the generator gains a `GenerateTimelineInternal` phase and generalizes `GenerateCommitDaysInternal` → `GenerateDatePagesInternal`. No package restructure (deferred seed, Epics 4/6).
- Output: adds root-level `timeline.html`; keeps `commits/{date}.html` (now general date pages). No other output-path changes.

### References

- [Source: [epics.md:883-901](../../_bmad-output/planning-artifacts/epics.md)] — Story 7.3 user story + both ACs.
- [Source: [epics.md:837-841](../../_bmad-output/planning-artifacts/epics.md)] — Epic 7 goal + FR16/FR19 coverage.
- [Source: [src/SpecScribe/CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs)] — the date-page templater to generalize (shell, a11y, prev/next, escaping).
- [Source: [src/SpecScribe/Charts.cs:468,884-907,980-982](../../src/SpecScribe/Charts.cs)] — `CommitHeatmap`, `D`/`DReadable`, `LinkedCommitDays`, `Plural`.
- [Source: [src/SpecScribe/GitMetrics.cs:9,18-27](../../src/SpecScribe/GitMetrics.cs)] — `CommitInfo`, `GitPulse` (`DailySeries`, `CommitsByDay`).
- [Source: [src/SpecScribe/SiteGenerator.cs:129-154,364-405,885-914,941-950,1047-1074](../../src/SpecScribe/SiteGenerator.cs)] — phase gate + deep-analytics cache/link pattern; commit-days phase to generalize; mtime gather to widen; artifact→page resolution.
- [Source: [src/SpecScribe/SiteNav.cs:10-28](../../src/SpecScribe/SiteNav.cs)] — output-path constants; nav-build timing (why no nav entry).
- [Source: [src/SpecScribe/GenerationReporter.cs:5,20-28](../../src/SpecScribe/GenerationReporter.cs)] — `GenerationPhase` enum + descriptions to extend.
- [Source: [src/SpecScribe/CommitDayEntry.cs](../../src/SpecScribe/CommitDayEntry.cs)] — the recorded-entry shape to generalize.
- [Source: [7-5-per-commit-detail-pages.md:18-23,33](7-5-per-commit-detail-pages.md)] — per-commit page target + the hash-link seam; numstat = deep path.
- [Source: [3-8-git-insights-hub-page.md:31,64,81,147](3-8-git-insights-hub-page.md)] — heatmap reuse, guarded links, cache-and-dashboard-link pattern.
- [Source: [7-2-source-citation-and-comment-linking-to-code-pages.md:184](7-2-source-citation-and-comment-linking-to-code-pages.md)] — escaping/stale-output/guarded-link lessons.
- [Source: [_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — invariants (AD-4, local/read-only, graceful degradation, seed-not-invariant).
- [Source: [tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs:106-154](../../tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs)] — generation-level temp-tree + `AssertNoErrors` test pattern.
- [[charting-is-pure-svg-no-js]] / [[specscribe-status-token-system]] / [[deep-git-single-numstat-path]] / [[generate-output-dir-is-specscribeoutput]] / [[worktree-edits-must-target-worktree-path]] / [[epic-7-code-link-strategy]] — project memory.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
