---
baseline_commit: 47ff3ea15365ec928ac5ffdb13a6ea985c2ee516
---

# Story 3.8: Git Insights Hub Page

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want a dedicated aggregate "Git Insights" page,
so that I can explore repository activity in depth without cluttering the dashboard.

## Acceptance Criteria

1. **Given** deep git insights are enabled **When** generation completes **Then** the portal produces an aggregate Git Insights page summarizing file change frequency, activity over time, and contributor attribution **And** its tables can be sorted and filtered client-side as a progressive enhancement while remaining readable and navigable without JavaScript. [Source: epics.md#Story 3.8, lines 519-537; PRD FR-10, line 135; PRD NFR-5, line 220]
2. **Given** the Git Insights page references individual files and commits **When** I select an entry **Then** I navigate to the corresponding per-file or per-commit detail page **And** when deep insights are disabled the heavier hub and detail-page generation does not run and baseline generation performance is unaffected. [Source: epics.md#Story 3.8, lines 533-537; PRD FR-10, lines 133-136]

---

## Developer Context

This story adds a **new synthesized page class** — the aggregate `git-insights.html` hub — to a portal that already renders dashboard git panels (Story 3.1) and per-day commit pages (`commits/{date}.html`). It is the "click in to see more" destination for the dashboard's git pulse: a data-dense page with three sections (file change frequency, activity over time, contributor attribution), each an accessible table (or reused chart) that is **fully readable without JavaScript** and **optionally sortable/filterable with JavaScript** as a progressive enhancement.

Two things make this story different from the pure-SVG chart stories that came before it, and you must internalize both:

1. **This is the one surface where progressive-enhancement JavaScript is explicitly sanctioned.** The project's long-standing "charts are pure SVG, no JS" rule ([[charting-is-pure-svg-no-js]]) is deliberately relaxed here — but *only* as enhancement. See "Progressive enhancement is the whole trick" below. This is not a license to build a SPA; it is permission to add client-side table sort/filter over already-rendered rows.
2. **This story sits on top of an unmerged dependency stack.** Read the Dependencies section before writing any code — the shared git data foundation (`--deep-git` gate + numstat parse) belongs to Story 3.2, and the detail pages you link to belong to Stories 7.1/7.4/7.5, none of which are merged yet.

### The core design in one paragraph

When `--deep-git` is enabled (Story 3.2's `ForgeOptions.DeepGitAnalytics`), and only then, `SiteGenerator` runs one additional gated phase that reads the shared bounded numstat log (one git call, from 3.2), aggregates it into (a) per-file change frequency + churn, (b) an activity-over-time view, and (c) per-contributor attribution counts, then renders a single synthesized `git-insights.html` page — a bespoke shell built the same way `CommitDayTemplater` builds its own (`PathUtil.RenderHeadOpen` + `nav.RenderNavBar` + `SiteNav.RenderBreadcrumb` + one `<main id="main-content">`). The three sections are accessible `<table>`s (or a reused `Charts.CommitHeatmap` for activity), server-sorted at generation time so they read correctly with JS off; a small progressive-enhancement script re-orders/filters the already-present rows when JS is on. File and commit entries link to their detail pages **only when those pages exist** (guarded via the cached `_codePages`/commit maps). When the flag is off, the phase never runs — no extra git work, no page — so baseline generation is byte-for-byte unaffected (AC #2).

---

## Dependencies / Sequencing

**Read this first — this story is contexted and implementable, but it builds on plumbing that is `ready-for-dev`/`backlog`, not merged.** Epic 3 is deliberately being carried with several stories `ready-for-dev` at once (3.2–3.6, 3.8), so `ready-for-dev` here means "fully specified and buildable", not "all upstreams merged." The dev agent owns coordinating the sequence.

- **Story 3.2 (Optional Deep Git Analytics Controls) — the hard dependency.** 3.2 introduces:
  - the `--deep-git` CLI flag → `ForgeOptions.DeepGitAnalytics` resolved flag ([SiteSettings.cs](../../src/SpecScribe/SiteSettings.cs), [ForgeOptions.cs](../../src/SpecScribe/ForgeOptions.cs)),
  - the gated `TryComputeDeep(...)` call at the single git-invocation site ([SiteGenerator.cs:407](../../src/SpecScribe/SiteGenerator.cs)),
  - the **shared bounded `git log --numstat` parse** that the re-plan explicitly designates as the one data path feeding this hub (3.8), per-file pages (7.4), and per-commit pages (7.5). [Source: 3-2 story Dev Notes, "Re-plan 2026-07-08: Shared numstat foundation + expanded gate"]
  - `ProgressModel.DeepGit` (nullable) threaded through `ProgressCalculator.Compute`.
  If 3.2 has merged, **extend** its numstat parse + `DeepGitPulse`/deep path — do **not** add a second `git log`. If 3.2 has not merged when you pick this up, land 3.2's flag+gate+numstat plumbing first (or as part of this work), then build the hub on it. Either way there must be **one** deep-git code path and **one** `--deep-git` gate.
- **The `--deep-git` flag gates this whole story (AC #2).** The hub page *and* any heavier detail-page generation this hub links into must only run when `DeepGitAnalytics` is true. The re-plan made this flag the gate for "the heavier hub + per-file/per-commit detail-page generation too, not just [3.2's] panel." [Source: 3-2 story Dev Notes]
- **Detail-page targets are not merged yet — guard every outgoing link on existence.**
  - Per-file links (AC #2) resolve to **Story 7.4** (per-file change-frequency + attribution pages) or, in the in-portal code-browsing model, **Story 7.1**'s `code/<repo-relative-path>.html` pages. 7.1 is `ready-for-dev`; 7.4 is `backlog` (no story file yet).
  - Per-commit links (AC #2) resolve to **Story 7.5**'s `commit/{shortHash}.html` pages. 7.5 is `backlog`.
  Because none are guaranteed present, follow the established **"guard all links on target availability"** pattern ([Source: 7-5 story Dependencies; 7-1 story reference-map seam]): if the target page exists (its path is in the generator's cached map), render a link; otherwise render the file path / commit hash as plain, escaped text. AC #2's "when I select an entry I navigate to the detail page" is satisfied incrementally — the hub is correct today (no broken links) and lights up automatically as 7.1/7.4/7.5 land. Do **not** hard-block this story on those.
- **Reuse for activity-over-time:** the existing `Charts.CommitHeatmap` ([Charts.cs](../../src/SpecScribe/Charts.cs)) already renders a full-history activity heatmap and already links each active day to its `commits/{date}.html` page (via `Charts.LinkedCommitDays`). Reuse it for the hub's "activity over time" section rather than authoring a new time chart — those per-day pages already exist (Story 3.1 / commit-days phase).

---

## Tasks / Subtasks

- [x] **Task 1: Land/confirm the `--deep-git` gate + shared numstat foundation (AC: #2)** — prerequisite from Story 3.2
  - [x] Subtask 1.1: Confirm `ForgeOptions.DeepGitAnalytics` and the gated `TryComputeDeep(...)` call exist ([ForgeOptions.cs](../../src/SpecScribe/ForgeOptions.cs), [SiteGenerator.cs:407](../../src/SpecScribe/SiteGenerator.cs)). If Story 3.2 has not merged, land its `--deep-git` flag + resolved option + single-call gate first — do **not** duplicate this plumbing. [Source: 3-2 story Tasks 1-2]
  - [x] Subtask 1.2: Confirm the shared bounded `git log --numstat` parse exists in [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs). There must be exactly **one** deep-git code path; this story extends it, never adds a second `git log`.

- [x] **Task 2: Aggregate the hub data from the one shared parse (AC: #1)**
  - [x] Subtask 2.1: Extend the shared numstat fetch format to carry `%an` (author) and `%ad` (author-date) via `\x01`/`\x1f` record/field sentinels (also `%s`/`%b` so 7.5 can share the fetch). Bound the window (`-n`/`--since`) exactly as 3.1/3.2 do — never uncapped ([deferred-work.md:36-38](../../_bmad-output/implementation-artifacts/deferred-work.md)).
  - [x] Subtask 2.2: Add a pure, static, repo-free aggregation helper in [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) (mirror `ParseLog`/`ParseChangedFiles`: raw text in, parsed out, skip malformed, never throw, invariant parse) producing three views: (a) per-file change frequency + churn (+added/−deleted), (b) an activity-over-time series, (c) per-contributor attribution counts (commits / files touched). Frame contributors as attribution, **never** a ranked leaderboard. [Source: prd.md:141, 182]
  - [x] Subtask 2.3: Surface the aggregates on the progress model — enrich 3.2's `DeepGitPulse` or add a sibling nullable field on [ProgressModel.cs](../../src/SpecScribe/ProgressModel.cs) (mirror `GitPulse? Git`, add to `Empty`); thread through `ProgressCalculator.Compute`. Prefer **one fetch, one parse, two views** (reuse `DeepGit` if it already carries enough).

- [x] **Task 3: Render the hub page (`GitInsightsTemplater`) (AC: #1)**
  - [x] Subtask 3.1: Add `src/SpecScribe/GitInsightsTemplater.cs` (`static RenderPage(...)`), cloning [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs)'s synthesized-shell pattern: `PathUtil.RenderHeadOpen` + `nav.RenderNavBar` + `SiteNav.RenderBreadcrumb` (`Home / Git Insights`) + one `<main id="main-content">` + `PathUtil.RenderFooter`.
  - [x] Subtask 3.2: Render the three sections. File frequency + contributors as accessible `<table>`s with `<caption>` and `<th scope="col">` ([EXPERIENCE.md:234](../../_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md)); activity-over-time by reusing `Charts.CommitHeatmap`. Server-sort every table at generation time (frequency desc, ordinal tie-break). HTML-escape every path/author/subject via `PathUtil.Html`. Empty section → friendly in-panel note, not a broken table.
  - [x] Subtask 3.3: Guard every outgoing detail-page link on target existence — file rows → `code/<path>.html` (7.1) / per-file page (7.4); commit refs → `commit/{hash}.html` (7.5). Link only when the target's path is in the generator's cached map; otherwise plain escaped text. No dead links.

- [x] **Task 4: Wire the gated generation phase (AC: #1, #2)**
  - [x] Subtask 4.1: Add `GenerationPhase.GitInsights` (enum + label) to [GenerationReporter.cs](../../src/SpecScribe/GenerationReporter.cs).
  - [x] Subtask 4.2: Add `GenerateGitInsightsInternal(...)` to [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs), invoked from `GenerateAll` **only when** `_options.DeepGitAnalytics && <deep data present>` (mirror the `if (_progress?.Git is { } gitPulse)` commit-days gate, [SiteGenerator.cs:121-126](../../src/SpecScribe/SiteGenerator.cs)); place it after the commit-days phase, before `WriteIndex`. Write `git-insights.html`, run `ApplyReferenceLinks`, wrap render in `try/catch → GenerationEvent`, and record `_gitInsightsPath` when produced.
  - [x] Subtask 4.3: Add `SiteNav.GitInsightsOutputPath = "git-insights.html"` constant. (A gated top-nav entry is optional — see "Discovery / entry points"; skip it if it could dangle.)

- [x] **Task 5: Entry point from the dashboard (AC: #2)**
  - [x] Subtask 5.1: Render a "View all git insights →" link on Story 3.1's consolidated Git Pulse panel ([Charts.cs](../../src/SpecScribe/Charts.cs)/[HtmlTemplater.cs](../../src/SpecScribe/HtmlTemplater.cs)), shown **only** when `_gitInsightsPath` is set (the hub was generated). This is the primary route in; the breadcrumb is the route back.

- [x] **Task 6: Progressive-enhancement table sort/filter (AC: #1)**
  - [x] Subtask 6.1: Extend `src/SpecScribe/assets/specscribe.js` (the one sanctioned script) with a dependency-free, delegated enhancer: opt-in tables (e.g. `class="js-sortable"`) get activatable column headers (button semantics + `aria-sort`) that re-order **already-present** `<tbody>` rows, plus an optional labeled filter `<input>` that hides non-matching rows. No fetch, no new information. Degrade silently with JS off.
  - [x] Subtask 6.2: Add `.git-insights`/table/sort-control/filter styles to `src/SpecScribe/assets/specscribe.css` using neutral tokens (not `--status-*`); wide tables scroll inside their own `overflow-x: auto` container, body never scrolls horizontally; sort direction via glyph/`aria-sort`, never color alone; any row transition under the reduced-motion `no-preference`/`reduce` seam. Edit **only** `src/SpecScribe/assets/specscribe.css` ([[generate-output-dir-is-specscribeoutput]]). Update `StylesheetTests` if it asserts class presence.

- [x] **Task 7: Test coverage (AC: #1, #2)**
  - [x] Subtask 7.1: Pure-aggregation tests (extend [GitMetricsTests.cs](../../tests/SpecScribe.Tests/GitMetricsTests.cs)): frequency ordering/top-N/churn; binary (`-`/`-`) numstat; per-contributor counts; activity bucketing; `\x01`/`\x1f` multi-line body/subject parsing; malformed skipped; empty → empty.
  - [x] Subtask 7.2: `GitInsightsTemplaterTests.cs` (unit): a11y contract, `<caption>`/`<th scope>`, escaping, guarded-link (present→`<a>`, absent→plain text), attribution-not-ranking framing, empty-section note.
  - [x] Subtask 7.3: Generation-level: **gate test** (`DeepGitAnalytics == false` → no `git-insights.html`, no error, unchanged dashboard — the load-bearing AC #2 pin); enabled → page generated + dashboard link present; deep-unavailable-with-flag-on → no hub, no error, rest of site generates; determinism (two runs identical).
  - [x] Subtask 7.4: Full pass: `dotnet test`; then `dotnet run --project src/SpecScribe -- generate --deep-git`, open `SpecScribeOutput/git-insights.html`, **disable JS and reload** (tables still complete/ordered/navigable — the NFR-5 gate), re-enable and confirm sort/filter announce state.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Gate the entire hub on `ForgeOptions.DeepGitAnalytics`.** The hub phase, its git aggregation, and any detail-page generation it drives must be skipped wholesale when the flag is off. This ternary/`if` **is** AC #2's performance guarantee: with the flag off, zero extra git process invocations and zero extra pages, so baseline timing cannot regress. Mirror the existing `if (_progress?.Git is { } gitPulse)` gate that guards the commit-days phase in `GenerateAll` ([SiteGenerator.cs:121-126](../../src/SpecScribe/SiteGenerator.cs)) — add a `&& _options.DeepGitAnalytics` condition (and a present-deep-data check).
- **Compute from the one shared numstat parse (3.2).** File frequency, activity-over-time, and contributor attribution are three *views* of the same bounded `git log --numstat` fetch. Aggregate in a pure, static, repo-free helper in [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) (mirror `ParseLog`/`ParseChangedFiles`: raw git text in, parsed data out, malformed lines skipped, never throws). Reuse `RunGit` (3s `Timeout`, UTF-8 stdout, never-throw — [GitMetrics.cs:182-218](../../src/SpecScribe/GitMetrics.cs)); do not write a second process helper.
- **Include author (`%an`) and author-date (`%ad`) in the shared fetch format so attribution and activity share it.** 3.2's suggested format is `log --numstat --pretty=format:%x01%H …`; extend the record/field sentinels to also carry `%an` and `%ad` (and, for 7.5's benefit, `%s`/`%b`) so **one** parse feeds attribution here and detail pages later. Use unambiguous `\x01` (record) / `\x1f` (field) sentinels — bodies and subjects are free text, so a blank-line or tab delimiter is not safe. Parse invariantly (`CultureInfo.InvariantCulture`) for the same non-Gregorian-calendar reasons `ParseLog` is invariant ([GitMetrics.cs:100-106](../../src/SpecScribe/GitMetrics.cs)).
- **Contributor attribution, never ranking.** Show per-contributor "who changed what" (author name + commit count / files touched) as collaboration context. Do **not** present a "most productive" leaderboard, sort primarily to crown a top performer, or add any productivity score. This is the explicit, amended PRD boundary: attribution is in scope, ranking/scoring people is not. [Source: prd.md:135, 137, 141; prd.md:182]
- **Render accessible tables.** Each tabular section is a `<table>` with a `<caption>` and `<th scope="col">` headers (and `scope="row"` where a row header is meaningful). [Source: EXPERIENCE.md:234 — "Tables use `<th scope="col/row">` and `<caption>` where meaningful."]
- **Server-sort every table at generation time.** Generation-time ordering is the source of truth (file frequency: change-count desc, ordinal path tie-break, mirroring `ParseChangedFiles`; contributors: by commit count desc, name tie-break; but framed as attribution, not a ranking podium). The no-JS reading must already be sensible and ordered. [Source: rendering-architecture.md:90 — "Server/generation-time ordering is the source of truth."]
- **Guard every outgoing detail-page link on target existence** (see Dependencies). Use the generator's cached page maps (`_codePages` from 7.1 when present; a commit-detail map from 7.5 when present). No target → plain escaped text, never a dead link. Guarding-on-existence is the same discipline `ResolveSpecCompanions` ([SiteGenerator.cs:827-869](../../src/SpecScribe/SiteGenerator.cs)) and the nav's well-known-file lookups already use.
- **HTML-escape everything user/repo-derived** via `PathUtil.Html(...)` — file paths, author names, commit subjects. Author names and subjects are free text and a routine `<`/`&` injection surface.
- **Reuse `Charts.CommitHeatmap` for "activity over time"** (it already exists, is accessible, and links to the commit-day pages). Do not author a parallel time chart.
- **Degrade non-fatally at every step** (NFR-2). Deep computation returning `null` (git missing, timeout, parse failure) → no hub page, no error, baseline generation still succeeds. A section with no data → a friendly in-panel note ("No contributor data available."), not a broken/empty table. Wrap the per-page render in the same `try/catch → GenerationEvent(Error, …)` pattern as `GenerateCommitDaysInternal` ([SiteGenerator.cs:353-369](../../src/SpecScribe/SiteGenerator.cs)).
- **Wipe/rebuild deterministically.** If the hub becomes a single root-level file (`git-insights.html`), the full-rebuild slate-wipe of `OutputRoot` ([SiteGenerator.cs:50-53](../../src/SpecScribe/SiteGenerator.cs)) already handles staleness — but if a run has the flag *off*, ensure no stale `git-insights.html` from a prior flag-*on* run survives (the full wipe covers `GenerateAll`; note this for any partial/watch path). If you emit a subdirectory instead, wipe+recreate it each pass like `commits/` and `adrs/`.
- **Add a `GenerationPhase.GitInsights` reporter phase** and wrap `BeginPhase/EndPhase` around the new work, matching the existing phases. [Source: [GenerationReporter.cs:5,20-28](../../src/SpecScribe/GenerationReporter.cs)]
- **Keep the sort/filter script a pure progressive enhancement** in the existing embedded `src/SpecScribe/assets/specscribe.js` — the one sanctioned script, shipped via `ForgeOptions.ScriptName` and already loaded on every page.

### DON'T

- **DON'T run any hub git work when `--deep-git` is off.** No "compute it anyway and just hide the page." AC #2 requires the deep path not to execute at all in the default configuration. [Source: prd.md:133-134 — deep metrics toggle independent of baseline; ≤10% regression met by *not invoking* the deep path]
- **DON'T add a second `git log` invocation.** All hub data comes from the shared numstat parse (3.2). [Source: 3-2 story Dev Notes — "One name-only-log code path, not two"]
- **DON'T let disabling JavaScript remove information or break navigation.** JS only re-orders/filters rows already in the HTML and toggles expand/collapse. Every fact and every link must be present and reachable with JS off. [Source: prd.md:220 (NFR-5); rendering-architecture.md:88-92]
- **DON'T build people-ranking / productivity scoring / leaderboards.** Attribution only. [Source: prd.md:141, 182]
- **DON'T co-opt the `--status-*` lifecycle tokens** for git styling — git activity is not a backlog/ready/done status. Use neutral `--ink`/`--border`/`--parchment`-family chart tokens, matching how Stories 3.1/3.2 style their git panels. [[specscribe-status-token-system]]
- **DON'T emit an uncapped-history git call.** Reuse 3.2's bounded window (`-n`/`--since`) — an uncapped `git log` blows the 3s `RunGit` budget and bloats the HTML on mature repos. [Source: [deferred-work.md:36-38](../../_bmad-output/implementation-artifacts/deferred-work.md)]
- **DON'T introduce the aspirational `IInsightProvider` / `SpecScribe.Core` package split.** The codebase is the single monolithic `src/SpecScribe`; the seed layout in the spec companions "does not exist yet" and is a seed, not an invariant. Extend `GitMetrics.cs` and add a peer `GitInsightsTemplater` in place. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant, line 100; echoed in 3-1/3-2/7-1 Dev Notes]
- **DON'T add a top-nav "Git Insights" entry that could dangle.** The hub only exists when `--deep-git` is on *and* the repo has git history — neither is known at nav-build time (nav is built before git is computed, [SiteGenerator.cs:63,738-742](../../src/SpecScribe/SiteGenerator.cs)). Prefer reaching the hub from the dashboard's Git Pulse panel ("View all git insights →", rendered only when the hub was generated) plus the hub's own breadcrumb — exactly how the commit-day pages are reached via the heatmap rather than a nav item. A gated top-nav entry is a possible stretch only if you can guarantee it never points at a missing page (see "Discovery / entry points" below).

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md); rendering-architecture.md]:

- **AD-4 — optional insight providers are additive, non-blocking, and never own baseline success.** Deep git analytics is the canonical example: a deep failure yields `null` and the hub is simply absent; baseline generation and the baseline pulse are untouched. [Source: ARCHITECTURE-SPINE.md#AD-4]
- **Local-only, read-only** — read git via shared read-only process calls; never write to source. [Inherited Invariants]
- **Graceful degradation is contractual (NFR-2)** — malformed/partial/absent history degrades non-fatally; generation always succeeds. [Inherited Invariants]
- **Baseline stays responsive (NFR-1)** — the whole hub is behind the `--deep-git` gate; the default path does no hub work. [Source: prd.md:216]
- **Progressive enhancement (NFR-5) — explicitly names this surface.** The rendering architecture's Client-Side Enhancement Policy calls out "the aggregate Git Insights hub" by name as a surface that MAY use JS for client-side sorting/filtering/expand-collapse, with guardrails: core content+nav work without JS; server ordering is source of truth; text/non-color cues and reduced-motion still apply; webview parity must not depend on the enhancement script. [Source: rendering-architecture.md:84-92; prd.md:220]
- **Accessibility is part of the rendering contract (NFR-6, UX-DR16/17)** — the hub carries the site skip-link → single `<main id="main-content">` landmark, nav, and breadcrumb like every page; tables carry `<caption>`/`<th scope>`; interactive controls expose `aria-sort`/labels; no color-only cues. [Source: [CommitDayTemplater.cs:36-42](../../src/SpecScribe/CommitDayTemplater.cs); EXPERIENCE.md:234; Stories 1.4/1.5]

---

## Progressive enhancement is the whole trick (read before writing the script)

This is the first genuinely *interactive* JS in the project (beyond the existing tooltip/copy/menu enhancers). Get the layering right:

- **No-JS baseline first.** Render each table complete and correctly server-sorted. A reader with JS disabled sees every row, every value, every (guarded) link, in a sensible order. This is not a fallback afterthought — it is the primary artifact, and it is what the webview adapter (Epic 6) will read. [Source: rendering-architecture.md:89-92]
- **JS enhances, never supplies.** Extend `src/SpecScribe/assets/specscribe.js` with a small, dependency-free enhancer that: on load, finds tables opted in via a marker (e.g. `class="js-sortable"` / `data-*` hooks on `<th>`), makes their column headers activatable (button semantics + `aria-sort` reflecting current state), and re-orders the **already-present** `<tbody>` rows on activation; and wires an optional per-table filter `<input>` that hides non-matching rows. Nothing is fetched, nothing is created that carries new information.
- **Follow the existing script's shape.** `specscribe.js` is a single IIFE of small `document.addEventListener(...)` delegated handlers, guarded against missing elements, degrading silently ([specscribe.js:7,176-234](../../src/SpecScribe/assets/specscribe.js)). Add the table enhancer as another delegated block in the same file and style; do not add a second script file or any external dependency (strict no-CDN, self-contained output).
- **A11y + motion still bind.** Sort controls announce state via `aria-sort`; the filter input has a real `<label>`. Any row show/hide or reorder animation must live under the existing `@media (prefers-reduced-motion: no-preference)` seam and be neutralized by the `reduce` block — reuse the pattern, don't invent one. Never signal sort direction by color alone; use a glyph/`aria-sort`. [Source: [[story-1-4-a11y-seams-for-1-5]]; prd.md:220]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **Existing infra to reuse (do not reinvent):**
  - [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) — `RunGit` (3s timeout, UTF-8 stdout, never-throw), the `ParseLog`/`ParseChangedFiles` pure-parser pattern, and (from 3.2) the shared numstat parse + `DeepGitPulse` + `TryComputeDeep`.
  - [Charts.cs](../../src/SpecScribe/Charts.cs) — `CommitHeatmap` (reuse for activity-over-time), `LinkedCommitDays`, `D`/`DReadable` date formatters, `F(double)` invariant number formatter, `Plural`, `Html`.
  - [PathUtil.cs](../../src/SpecScribe/PathUtil.cs) — `RenderHeadOpen`/`RenderFooter`/`Html`/`NormalizeSlashes`/`RelativePrefix` (the page shell + escaping + relative-link math).
  - [SiteNav.cs](../../src/SpecScribe/SiteNav.cs) — `RenderNavBar`/`RenderBreadcrumb`; output-path constants live here.
  - [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs) — the synthesized-page templater to clone for shell + a11y contract + escaping.
  - [ForgeOptions.cs](../../src/SpecScribe/ForgeOptions.cs) — `StylesheetName`/`ScriptName`/`OutputRoot`/`RepoRoot`; and (from 3.2) `DeepGitAnalytics`.

---

## File Structure Requirements

**New files:**

- `src/SpecScribe/GitInsightsTemplater.cs` — `static RenderPage(...)` returning the full HTML for the hub. Model directly on [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs) (builds its own shell via `PathUtil.RenderHeadOpen`, its own nav/breadcrumb, one `<main>`, footer). Renders the three sections. Takes the aggregated data plus the guarded link resolvers (delegates or a small lookup) so it never reaches into `SiteGenerator` internals.
- `tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs` — unit (no IO): sections present, escaping, a11y contract (skip-link first, single `<main id="main-content">`, `<caption>`/`<th scope>`), attribution-not-ranking framing, guarded-link behavior (link when target present, plain text when absent).
- Pure-aggregation tests can extend `tests/SpecScribe.Tests/GitMetricsTests.cs` (mirror its feed-raw-git-text-assert-structure pattern) rather than a new file.

**Modified files (read fully before editing):**

- `src/SpecScribe/GitMetrics.cs` — extend the shared numstat parse (3.2) to also produce the hub aggregates (per-file frequency+churn, per-contributor attribution, activity series), and carry `%an`/`%ad` in the fetch format. Keep the parse pure and never-throwing. **Preserve:** the never-throw contract, `RunGit`'s 3s timeout / UTF-8 / bounded window, and 3.2's existing deep record shape — extend, don't fork.
- `src/SpecScribe/ProgressModel.cs` — if the hub needs data beyond 3.2's `DeepGit` field, either enrich `DeepGitPulse` or add a sibling nullable field (mirror the existing `GitPulse? Git` at [ProgressModel.cs:32](../../src/SpecScribe/ProgressModel.cs) and 3.2's `DeepGit`; add to `ProgressModel.Empty`). Prefer **one fetch, one parse, two views** — reuse 3.2's `DeepGit` payload if it already carries enough.
- `src/SpecScribe/SiteGenerator.cs` — add a `GenerateGitInsightsInternal(...)` phase invoked from `GenerateAll`, gated on `_options.DeepGitAnalytics && <deep data present>`, placed near the commit-days phase (after `Adrs`/`CommitDays`, before `WriteIndex`). Cache a flag/entry (e.g. `_gitInsightsPath`) so the dashboard panel can render the "View all →" link only when the page exists. Run `ApplyReferenceLinks` on the rendered HTML so "Story N.M"/"FR-9" mentions in commit subjects become links, exactly like the commit-day and ADR phases. **Preserve:** the full-rebuild wipe, per-phase reporter calls, the `_docs`/`_nav`/`_progress` lifecycle, and `WriteIndex` running last.
- `src/SpecScribe/GenerationReporter.cs` — add `GitInsights` to the `GenerationPhase` enum and a description in the label map. [Source: [GenerationReporter.cs:5,20-28](../../src/SpecScribe/GenerationReporter.cs)]
- `src/SpecScribe/SiteNav.cs` — add a `GitInsightsOutputPath = "git-insights.html"` constant (root-level, matching `SprintOutputPath`/`EpicsOutputPath` conventions). A gated top-nav entry is optional (see "Discovery / entry points").
- `src/SpecScribe/Charts.cs` and/or `src/SpecScribe/HtmlTemplater.cs` — add the dashboard Git Pulse panel's "View all git insights →" link, rendered **only** when the hub was generated (see `_gitInsightsPath`). This is the primary entry point. Coordinate with Story 3.1's consolidated Git Pulse panel.
- `src/SpecScribe/assets/specscribe.js` — add the progressive-enhancement table sort/filter enhancer (see "Progressive enhancement is the whole trick").
- `src/SpecScribe/assets/specscribe.css` — add `.git-insights`/table/sort-control/filter styles using neutral tokens (not `--status-*`); horizontal scroll on wide tables inside their own `overflow-x: auto` container, never the body ([[tooltip-clipping-use-ss-tooltip-node]] for any rich tooltips; body must never scroll horizontally). Reduced-motion seam for any row transition. Note `StylesheetTests.cs` asserts on stylesheet content — add companion assertions if it checks class presence. **Edit only `src/SpecScribe/assets/specscribe.css`** — the `docs/live` copy is vestigial/gitignored ([[generate-output-dir-is-specscribeoutput]]).

**Output layout:**

- `SpecScribeOutput/git-insights.html` (root-level standalone hub, like `sprint.html`/`epics.html`/`requirements.html`). Detail-page links point at `code/<path>.html` (7.1) / `commit/{hash}.html` (7.5) relative to root — `PathUtil.RelativePrefix("git-insights.html")` is empty, so hrefs are direct. If you prefer a subdirectory (`git/index.html`), wipe+recreate that dir each pass like `commits/`; the root-level single file is simpler and recommended.

### Discovery / entry points

- **Primary:** a "View all git insights →" link on the dashboard's consolidated Git Pulse panel, rendered only when `_gitInsightsPath` is set (i.e. the hub was actually generated). This mirrors how the heatmap is the entry point to commit-day pages, and it sidesteps the nav-timing problem below.
- **Breadcrumb:** the hub's own `Home / Git Insights` trail.
- **Optional stretch — a gated top-nav entry.** Only if you can guarantee it never dangles. Nav is built at [SiteGenerator.cs:63](../../src/SpecScribe/SiteGenerator.cs) *before* git is computed, so you cannot know at build time whether the hub will exist. The README precedent rebuilds nav after its render decision ([SiteGenerator.cs:68-77](../../src/SpecScribe/SiteGenerator.cs)); a similar "rebuild nav once the hub is known" is possible but adds complexity. If unsure, skip the nav entry (7.1 makes the same call for code pages) — a dashboard link + breadcrumb fully satisfies AC #2's "select an entry → navigate."

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Follow the temp-dir `IDisposable` + `AssertNoErrors(gen.GenerateAll())` pattern from `SiteGeneratorTraceabilityTests`, and the pure-parser feed-text pattern from `GitMetricsTests`.

**Pure-aggregation tests (extend `GitMetricsTests.cs`, no repo):**
- File frequency: change-count ordering + top-N truncation + ordinal tie-break; churn (+added/−deleted) summed per file; binary files (`-`/`-` numstat) handled.
- Contributor attribution: per-author commit/file counts aggregated correctly; framed as counts, no "rank" field.
- Activity series: correct per-period bucketing.
- Robustness: malformed/short lines skipped (never-throw); empty history → empty aggregates; the fetch format's `\x01`/`\x1f` sentinels parse multi-line bodies/subjects without bleeding into the next record.

**`GitInsightsTemplaterTests.cs` (unit, no IO):**
- Renders the a11y contract (skip-link first, single `<main id="main-content">`, nav, `Home / Git Insights` breadcrumb).
- All three sections render with populated data; each table has a `<caption>` and `<th scope="col">`.
- **Escaping:** an author name / commit subject / file path containing `<`, `&`, `"` appears escaped, raw does not (mirror `CommitDayTemplaterTests.RenderPage_EscapesCommitFields`).
- **Guarded links:** a file/commit whose detail page exists renders an `<a>`; one whose target is absent renders plain escaped text (no dead link).
- **Attribution-not-ranking:** contributor section presents counts/attribution, with no leaderboard/"top performer"/score wording (assert the framing copy).
- Empty section → friendly in-panel note, not a broken table.

**Generation-level tests (extend a `SiteGenerator…Tests`):**
- **Gate (AC #2, the load-bearing test):** with `ForgeOptions.DeepGitAnalytics == false`, `GenerateAll` emits **no** `git-insights.html` and reports no error, and the default dashboard is unchanged (no "View all →" link). Pin this at the option/render boundary — do **not** write a flaky wall-clock timing test.
- **Enabled:** with `DeepGitAnalytics == true` and git history available, `git-insights.html` is generated (positive control: contains a known section heading + a known file path/author), and the dashboard panel's "View all →" link is present.
- **Non-fatal degradation:** deep computation unavailable (no git / forced failure) with the flag on → no hub, no `Error` outcome, rest of the site still generates.
- **Determinism:** two runs over the same input produce identical `git-insights.html`.

**Run:** `dotnet test` from repo root. Then a real generation pass against this repo **with the flag**: `dotnet run --project src/SpecScribe -- generate --deep-git` (output lands in the default `SpecScribeOutput/`; **do not** pass `--output docs/live`, it is vestigial/gitignored — [[generate-output-dir-is-specscribeoutput]]). Open `SpecScribeOutput/git-insights.html`: confirm the three sections read correctly, then **disable JavaScript and reload** — tables must still be complete, ordered, and navigable (this is the NFR-5 acceptance gate), then re-enable and confirm sort/filter work and announce state.

---

## Previous Story Intelligence

- **Story 3.1 (Baseline Git Pulse) — merged/in review.** Established the git pipeline this extends: `GitPulse` + `GitMetrics.TryCompute` shell out once (in `GenerateEpicsInternal`, [SiteGenerator.cs:407](../../src/SpecScribe/SiteGenerator.cs)), flow via `ProgressModel.Git`, and render a consolidated dashboard Git Pulse panel. That panel is where the hub's "View all →" entry link belongs. 3.1's discipline — never-throw, bounded `-n 200` window, degrade a signal to empty rather than nulling the pulse, invariant date parsing, pure testable parsers — is the exact template. [Source: [3-1 story](3-1-baseline-git-pulse-insights-on-dashboard.md)]
- **Story 3.2 (Deep Git Analytics) — the direct upstream.** Owns the `--deep-git` flag, `TryComputeDeep`, `DeepGitPulse`, and the shared numstat parse the re-plan designates as this hub's data source. Read it in full before starting; you are extending its foundation, not paralleling it. [Source: [3-2 story](3-2-optional-deep-git-analytics-controls.md)]
- **Story 7.5 (Per-Commit Pages) / 7.1 (Code File Browsing) — your link targets.** 7.5 emits `commit/{shortHash}.html`; 7.1 emits `code/<path>.html` and caches a repo-relative→output-path map (`_codePages`) precisely so downstream stories can resolve links without re-discovering. Consume those maps when present; guard on absence. Both mirror `CommitDayTemplater`/`GenerateCommitDaysInternal` — the same synthesized-page shape you're cloning. [Source: [7-5 story](7-5-per-commit-detail-pages.md), [7-1 story](7-1-in-portal-code-file-browsing.md)]
- **Recurring lessons that bite this renderer:** escaping bugs and stale-output bugs are the two most common regressions (both covered in the test list). Use invariant/culture-safe formatting for every derived string (dates, counts) — the codebase was bitten by culture-sensitive date parsing before ([GitMetrics.cs:100-106](../../src/SpecScribe/GitMetrics.cs)). Extend the monolith in place; the seed package split is deferred.

## Git Intelligence Summary

Recent history is Epic-3 git-insights build-out and dashboard iteration (`Dashboard adjustments`, `3.1 work`, `Reviews`, `Iterating and planning`) plus the Story 7.1 spec merge. Story 3.1's git pulse work (`GitMetrics`, `Charts.GitPulsePanel`, dashboard consolidation) is the immediately-preceding change and is the surface you attach the hub entry link to. No hub/deep-git code exists on `main` yet; 3.2's flag+numstat foundation is the piece to land first. **Note the active auto-committer on `main`** — do this work on a worktree branch and target the worktree path for edits, don't re-root at `C:\Dev\SpecScribe`. [[worktree-edits-must-target-worktree-path]]

## Latest Technical Information

No external libraries or APIs are introduced. Platform notes: `System.Diagnostics.Process` + `System.Text` cover the git call and UTF-8 decoding already; the sort/filter enhancer uses only standard DOM APIs (`addEventListener`, `sort`, `dataset`, `aria-*`) — no framework, no CDN, self-contained output. `\x01`/`\x1f` are safe field/record sentinels for `git log --pretty=format:` (git emits them literally and they never occur in paths, names, or messages).

## Project Context Reference

- Epic 3 goal + Story 3.8 ACs: [Source: [epics.md:393-397, 519-537](../../_bmad-output/planning-artifacts/epics.md)]
- FR-10 (aggregate Git Insights surface + navigable detail pages + attribution): [Source: [prd.md:129-141](../../_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md)]
- NFR-1 (deep analysis separable from baseline), NFR-2 (graceful degradation), NFR-5 (progressive enhancement): [Source: prd.md:216-220]
- Client-Side Enhancement Policy (names the Git Insights hub explicitly): [Source: rendering-architecture.md:84-92]
- Architecture invariants (AD-4 additive insight providers; local-only, read-only; seed-not-invariant): [Source: ARCHITECTURE-SPINE.md#AD-4, #Seed, Not Invariant]
- Accessible tables (`<caption>`/`<th scope>`): [Source: EXPERIENCE.md:234]
- Status-token discipline + pure-render conventions (and the sanctioned relaxation for insight surfaces): project memory — [[specscribe-status-token-system]], [[charting-is-pure-svg-no-js]].

## References

- [Source: [epics.md#Story 3.8](../../_bmad-output/planning-artifacts/epics.md) (lines 519-537)] — user story + both acceptance criteria.
- [Source: prd.md#FR-10 (lines 129-141)] — aggregate Git Insights surface (file change frequency, activity over time), navigable per-commit/per-file detail pages, contributor attribution; attribution-in / ranking-out boundary.
- [Source: prd.md#NFR-5 (line 220)] — progressive-enhancement contract: core content+nav work without JS; JS = client sort/filter/expand only.
- [Source: prd.md#NFR-1 (line 216), #Non-Goals (line 182), §6.2 (line 197)] — deep analysis separable from baseline; no people-ranking; deep git remains optional.
- [Source: [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md):84-92] — Client-Side Enhancement Policy naming the Git Insights hub and its guardrails.
- [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)#AD-4, #Seed Not Invariant] — additive non-blocking insight providers; extend the monolith, don't force the package split.
- [Source: [EXPERIENCE.md](../../_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md):234] — accessible-table convention.
- [Source: [src/SpecScribe/GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs)] — `RunGit`, `ParseLog`/`ParseChangedFiles` pure-parser pattern, never-throw contract to extend (and 3.2's numstat parse/`TryComputeDeep`).
- [Source: [src/SpecScribe/Charts.cs](../../src/SpecScribe/Charts.cs)] — `CommitHeatmap` (reuse for activity), `LinkedCommitDays`, `D`/`DReadable`/`F`/`Plural`/`Html`.
- [Source: [src/SpecScribe/CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs)] — synthesized-page templater to clone (shell, a11y contract, escaping).
- [Source: [src/SpecScribe/SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs):38-146, 333-374, 407] — `GenerateAll` phase orchestration + wipe; `GenerateCommitDaysInternal` (mirror); single git-invocation site + `_progress.Git` gate.
- [Source: [src/SpecScribe/SiteNav.cs](../../src/SpecScribe/SiteNav.cs):10-17, 130-176] — output-path constants; nav/breadcrumb rendering; nav-build timing.
- [Source: [src/SpecScribe/GenerationReporter.cs](../../src/SpecScribe/GenerationReporter.cs):5,20-28] — `GenerationPhase` enum + labels to extend.
- [Source: [src/SpecScribe/ProgressModel.cs](../../src/SpecScribe/ProgressModel.cs):32-45] — `GitPulse? Git` field + `Empty`; pattern to mirror for deep/hub data.
- [Source: [src/SpecScribe/assets/specscribe.js](../../src/SpecScribe/assets/specscribe.js):7,176-234] — the one sanctioned script; IIFE + delegated-handler shape to extend for table sort/filter.
- [Source: [deferred-work.md](../../_bmad-output/implementation-artifacts/deferred-work.md):36-38] — uncapped `git log` history is a 3s-timeout/HTML-bloat risk; keep the window bounded.
- [Source: [3-1-baseline-git-pulse-insights-on-dashboard.md](3-1-baseline-git-pulse-insights-on-dashboard.md), [3-2-optional-deep-git-analytics-controls.md](3-2-optional-deep-git-analytics-controls.md), [7-1-in-portal-code-file-browsing.md](7-1-in-portal-code-file-browsing.md), [7-5-per-commit-detail-pages.md](7-5-per-commit-detail-pages.md)] — upstream/sibling stories: git pipeline, `--deep-git` gate + numstat foundation, code/commit detail pages + cached path maps to link into.
- [[charting-is-pure-svg-no-js]] / [[specscribe-status-token-system]] / [[story-1-4-a11y-seams-for-1-5]] / [[generate-output-dir-is-specscribeoutput]] / [[worktree-edits-must-target-worktree-path]] / [[epic-7-code-link-strategy]] — project memory.

## Dev Agent Record

### Agent Model Used

Claude Fable 5 (claude-fable-5)

### Debug Log References

- `dotnet test` full suite: 557/557 passing (was 526 before this story; +31 new tests).
- Real generation passes against this repo: `generate --deep-git` → 44 pages incl. `git-insights.html`; default `generate` → 42 pages, no hub, no dashboard hub link, no stale page (full-wipe covers).
- Browser verification (served static output): three sections render; sort buttons toggle `aria-sort` + glyph and re-order rows; filter hides non-matching rows with a live "N of M rows" count; zero console errors; at 375px the body does not scroll horizontally while both `.table-scroll` containers do.

### Completion Notes List

- **One fetch, one parse, several views.** Extended Story 3.2's single deep-git fetch to `log --numstat --date=format:%Y-%m-%dT%H:%M --pretty=format:%x01%H%x1f%an%x1f%ad%x1f%s%x1f%b%x1f -n 300` (still bounded, still one `git log`). New pure `GitMetrics.ParseNumstatRecords` splits on the `\x01`/`\x1f` sentinels (trailing `\x1f` closes the body so multi-line bodies can never bleed into the file set) and also accepts the old `%x01%H`-only shape; `ParseNumstatLog` was refactored to compute hotspots/coupling as one view over those records (all 3.2 tests untouched and green) and now carries the hub aggregates alongside. `DeepCommit` retains `%s`/`%b` so Story 7.5 can reuse the same fetch.
- **Model seam:** rather than a new `ProgressModel` field, the hub aggregates ride as `DeepGitPulse.Insights` (`GitInsightsData`: per-file frequency+churn top-50, per-contributor attribution counts, per-day activity series, window commit count). Deviation from Subtask 4.2's `_gitInsightsPath` field: the generator instead clears `DeepGit.Insights` when the hub write fails — the exact pattern 3.2 established for `DeepGit` itself — so one signal gates both the page and the dashboard link and can never dangle.
- **Hub page (`GitInsightsTemplater`)**: CommitDayTemplater-style synthesized shell (`deep-page git-insights` main). File-frequency + contributor sections are accessible, server-sorted `<table>`s (`<caption>`, `<th scope="col">`, contributor names as `scope="row"`); activity-over-time reuses `Charts.CommitHeatmap` (baseline pulse series, so its day links always match the generated `commits/{date}.html` set) with a headline derived from the deep window's activity series. Guarded links via injectable `fileHref`/`commitHref` resolvers, unwired until 7.1/7.4/7.5 land their page maps → plain escaped text today, links light up automatically later. Contributor section framed as attribution ("not a scoreboard"), no rank column.
- **Gating (AC #2):** `GenerateGitInsightsInternal` runs only when `DeepGit.Insights` exists (which itself requires `--deep-git`); flag off ⇒ zero extra git work, zero pages, dashboard byte-identical (pinned by generation tests, not wall-clock timing). Non-fatal render failure → Error event + link cleared, run still succeeds.
- **Progressive enhancement:** the sanctioned `specscribe.js` gained one `js-sortable` table enhancer — headers become real `<button>`s announcing `aria-sort` (+ glyph, never color-only), rows re-order in place; the labeled filter `<input>` (+ `aria-live` row count) is **created by JS**, so no dead control ships in the no-JS page. Row hiding is `display`-based — no new motion, so the reduced-motion seam needs no new entries.
- **CSS:** neutral-token `.gi-table`/`.gi-sort-btn`/`.gi-filter` styles; `.table-scroll { overflow-x: auto; contain: inline-size; }` — the `contain` is load-bearing: without it the tables' nowrap min-content propagates through the shrink-to-fit `.deep-page` main and forces body-level horizontal scroll on narrow viewports (found and fixed during browser verification).
- **NFR-5 no-JS gate:** verified structurally — every row, value, and (guarded) link is server-rendered and server-sorted in the static HTML; the sort/filter controls do not exist without JS, so nothing dangles.

### File List

- src/SpecScribe/GitMetrics.cs (modified — enriched shared fetch format; `DeepCommit`/`DeepFileChange`/`FileChangeStat`/`ContributorStat`/`GitInsightsData` records; `ParseNumstatRecords`; `BuildInsights`; `ParseNumstatLog` refactored onto the record parse; `DeepGitPulse.Insights`)
- src/SpecScribe/GitInsightsTemplater.cs (new — the hub page renderer)
- src/SpecScribe/SiteGenerator.cs (modified — `GenerateGitInsightsInternal` gated phase, invoked from `GenerateAll` after commit-days/deep-analytics, before `WriteIndex`)
- src/SpecScribe/GenerationReporter.cs (modified — `GenerationPhase.GitInsights` + label)
- src/SpecScribe/SiteNav.cs (modified — `GitInsightsOutputPath` constant; no top-nav entry by design)
- src/SpecScribe/HtmlTemplater.cs (modified — Git Pulse header "View all git insights →" link gated on `DeepGit.Insights`)
- src/SpecScribe/assets/specscribe.js (modified — `js-sortable` sort/filter enhancer appended to the one sanctioned script)
- src/SpecScribe/assets/specscribe.css (modified — Git Insights hub section: `.table-scroll`, `.gi-table`, `.gi-sort-btn`, `.gi-filter`, `.gi-row-hidden`, `.git-pulse-header-links`)
- tests/SpecScribe.Tests/GitMetricsTests.cs (modified — 11 new record-parse/aggregation tests)
- tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs (new — 7 hub-page unit tests)
- tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs (new — 4 generation-level gate/enabled/degradation/determinism tests)
- tests/SpecScribe.Tests/HtmlTemplaterTests.cs (modified — hub-link gating test)
- tests/SpecScribe.Tests/StylesheetTests.cs (modified — Story 3.8 CSS/JS seam guards)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified — story status tracking)
- _bmad-output/implementation-artifacts/3-8-git-insights-hub-page.md (modified — this story file)

## Change Log

- 2026-07-09: Implemented Story 3.8 — aggregate Git Insights hub (`git-insights.html`) behind the `--deep-git` gate: enriched the one shared numstat fetch with author/date/subject/body sentinels, added pure record-parse + hub aggregation views in GitMetrics, new GitInsightsTemplater page (accessible server-sorted tables, reused commit heatmap, guarded detail links), gated SiteGenerator phase + dashboard "View all git insights →" entry link, progressive-enhancement table sort/filter in the sanctioned script, and full unit/templater/generation test coverage (557 tests green).
