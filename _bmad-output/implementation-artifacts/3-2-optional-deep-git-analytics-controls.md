---
baseline_commit: bd66175a6d19a6dcbad9abea4397986010df3609
---

# Story 3.2: Optional Deep Git Analytics Controls

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an advanced user,
I want deeper git analytics available as an opt-in mode,
so that I can inspect hotspots without degrading default performance.

## Acceptance Criteria

1. **Given** deep analytics are disabled **When** baseline generation runs **Then** default performance remains within defined responsiveness expectations **And** deep analysis does not run implicitly. [Source: epics.md#Story 3.2; PRD FR-10]
2. **Given** deep analytics are enabled explicitly **When** generation completes **Then** additional insights are surfaced distinctly from baseline metrics **And** failures in deep analysis remain non-fatal. [Source: epics.md#Story 3.2; PRD FR-10]

**Testable consequences carried from PRD FR-10** [Source: prd.md:129-137]:
- Deep metrics can be **toggled independently** of baseline generation.
- With deep metrics **disabled**, baseline generation runs no additional git work (the >10% regression budget is met by *not invoking* the deep path at all — the gate is the guarantee).
- **Out of scope (non-goal):** individual productivity scoring or ranking, and any per-author leaderboard. "Hotspots / coupling" means **file-path** signals only. [Source: prd.md:137, prd.md:178]

## Tasks / Subtasks

- [x] Task 1: Add the opt-in toggle through the settings/options plumbing (AC: #1)
  - [x] Subtask 1.1: Add a `--deep-git` boolean `[CommandOption]` to [SiteSettings.cs](../../src/SpecScribe/SiteSettings.cs), mirroring the existing `--no-readme` bool pattern exactly (option + `[Description]`). Suggested description: `"Enable deeper git analytics (change coupling and hotspots) as an opt-in dashboard panel. Default: off, so baseline generation performance is unaffected."` Default is `false` (opt-in), so the positive flag form is correct — do **not** add a `--no-*` inverse.
  - [x] Subtask 1.2: Add `public required bool DeepGitAnalytics { get; init; }` to [ForgeOptions.cs](../../src/SpecScribe/ForgeOptions.cs) and a `bool deepGitAnalytics = false` parameter to `ForgeOptions.Resolve(...)`, threading it into the returned object — mirror precisely how `includeReadme` / `IncludeReadme` already flows. Update `SiteSettings.Resolve()` (line 31) to pass `deepGitAnalytics: DeepGit`.
  - [x] Subtask 1.3: **Gate the deep computation at the single call site.** In `SiteGenerator.GenerateEpicsInternal` ([SiteGenerator.cs:402](../../src/SpecScribe/SiteGenerator.cs)), immediately after the existing `var gitPulse = GitMetrics.TryCompute(_options.RepoRoot);`, add: `var deepGit = _options.DeepGitAnalytics ? GitMetrics.TryComputeDeep(_options.RepoRoot) : null;`. This ternary **is** AC #1's guarantee — when the flag is off, `TryComputeDeep` is never called, so no extra git process runs and baseline timing cannot regress. Git is invoked once per full pass here, not per page (same as the baseline pulse) — do not call it anywhere else.

- [x] Task 2: Compute the deep signals as a never-throw, bounded provider (AC: #1, #2)
  - [x] Subtask 2.1: Add a `DeepGitPulse` positional `record` to [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) alongside `GitPulse` / `CommitInfo`. Suggested shape: `public sealed record DeepGitPulse(IReadOnlyList<(string Path, int Changes)> Hotspots, IReadOnlyList<(string FileA, string FileB, int CoChanges)> Coupling);`
  - [x] Subtask 2.2: Add `public static DeepGitPulse? TryComputeDeep(string repoRoot)` **inside the existing `GitMetrics` class** so it reuses the private `RunGit` helper and the shared 3-second `Timeout` — do **not** write a second process-invocation helper. It must obey the same never-throw contract (`try { … } catch { return null; }`) as `TryCompute` ([GitMetrics.cs:20-22, 27-60](../../src/SpecScribe/GitMetrics.cs)): any failure yields `null`, which the dashboard treats as "no deep data", never an error. [Source: ARCHITECTURE-SPINE.md#AD-4]
  - [x] Subtask 2.3: Fetch commit→file-set data with a **single, bounded** name-only log call, e.g. `log --name-only --pretty=format:%x01%H -n 300` (leading `\x01` sentinel per commit lets the parser find commit boundaries without a blank-line ambiguity). **Cap history** with `-n` (or `--since`) — [deferred-work.md:36-38](../../_bmad-output/implementation-artifacts/deferred-work.md) and Story 3.1's notes both flag that an uncapped `git log` blows the 3s `RunGit` budget on mature repos. Reuse `RunGit`'s already-solved UTF-8 stdout encoding ([GitMetrics.cs:119](../../src/SpecScribe/GitMetrics.cs)) so non-ASCII file paths don't mojibake.
  - [x] Subtask 2.4: Parse into a **pure, static, repo-free helper** (mirror `ParseLog`'s pattern — takes raw git text, returns parsed data, skips malformed lines). Suggested: `public static DeepGitPulse ParseNameOnlyLog(string logText, ...)`. Group file paths by commit; **Hotspots** = per-path change frequency, sorted desc, top ~10; **Coupling** = for each commit's file set, count unordered co-changed pairs, sorted desc, keep pairs with `CoChanges >= 2`, top ~10.
  - [x] Subtask 2.5: **Guard the pairwise cost.** A single bulk/merge/vendored-import commit touching thousands of files would explode the O(n²) pair count. Skip commits whose file-set size exceeds a cap (e.g. `> 50` files) when building coupling pairs — they are almost always bulk imports, not meaningful co-change signal. Document the cap inline.
  - [x] Subtask 2.6: Keep deep failure isolated from baseline. `TryComputeDeep` is a separate call from `TryCompute`, so a deep failure returns `null` deep data while the baseline `GitPulse` (Story 3.1's signals) stays intact — partial data beats no data (AC #2 "failures remain non-fatal"). [Source: ARCHITECTURE-SPINE.md#AD-4]

- [x] Task 3: Thread deep data into the progress model and render a distinct panel (AC: #2)
  - [x] Subtask 3.1: Add `public DeepGitPulse? DeepGit { get; init; }` to [ProgressModel.cs](../../src/SpecScribe/ProgressModel.cs) (nullable, init-only, defaulting `null` — mirror the existing `GitPulse? Git` field at line 32; add `DeepGit = null` to `ProgressModel.Empty`). Thread it through `ProgressCalculator.Compute` with a new optional `DeepGitPulse? deep = null` parameter so construction stays in one place, and pass `deepGit` at the `SiteGenerator.cs:403` call site.
  - [x] Subtask 3.2: Add a rendering helper to [Charts.cs](../../src/SpecScribe/Charts.cs) — **pure inline SVG/HTML + CSS variables, no JS** ([Charts.cs:6-8](../../src/SpecScribe/Charts.cs) class doc; this is a deliberate, established convention, not an oversight). Render the hotspots list and the coupling pairs. Reuse `Charts.Plural` for count labels, the existing `Html()`/escaping helper for paths, and existing chart/design-system CSS variables (parchment/ink/rust neutral chart tokens) — do not invent new colors. Coupling/hotspots are **not** statuses, so the `--status-*` token system does not apply here.
  - [x] Subtask 3.3: Render the panel from `AppendDashboard` in [HtmlTemplater.cs](../../src/SpecScribe/HtmlTemplater.cs) (in the dashboard body, e.g. after the existing `chart-row` that holds the epic-status donut + Commit Activity heatmap, ~line 260-269). Make it **visibly distinct from baseline metrics** (AC #2): its own `chart-panel` with a heading that names it as deeper/opt-in analytics (e.g. `"Change Coupling"` / `"Git Hotspots"` under a "Deep Analytics" framing) so it never reads as part of the baseline Git Pulse / Commit Activity surface.
  - [x] Subtask 3.4: **Omit the panel entirely when `p.DeepGit is null`** (flag off, or deep computation failed) — unlike the baseline stat cards that show a `"—"` empty state, the deep panel should simply not exist when not opted into, so the default dashboard is byte-for-byte unchanged for users who never pass `--deep-git`. When the flag is on but the repo has no significant coupling, render the panel with a friendly in-panel note (e.g. `"No significant change coupling detected."`) rather than a broken/empty chart.

- [x] Task 4: Interactive + CLI parity and persistence (NFR7) (AC: #1)
  - [x] Subtask 4.1: Add an interactive toggle in `InteractiveCommand.ConfigurePaths` ([Commands.cs:142-160](../../src/SpecScribe/Commands.cs)) — a `Confirm`-style prompt ("Enable deep git analytics?") defaulting to the current `settings.DeepGit`. NFR7 makes menu/CLI parity contractual: every configurable feature must be reachable from both surfaces. [Source: prd.md NFR7; settings-and-signals.md#Git-insights]
  - [x] Subtask 4.2: Persist the toggle in [SettingsStore.cs](../../src/SpecScribe/SettingsStore.cs) so it survives between runs (directory-scoped `.specscribe`). Because `SavedSettings` uses **nullable** fields for tri-state ("unset" vs set) and `ApplyTo` uses `??=`, add `public bool? DeepGit { get; set; }`, include it in `IsEmpty`, wire it in `TrySave`, and in `ApplyTo` apply the saved value only when the CLI did not request it: `if (!settings.DeepGit && saved.DeepGit == true) settings.DeepGit = true;`. Rationale: the CLI bool defaults `false` and there is no `--no-deep-git`, so `settings.DeepGit == false` unambiguously means "not requested on this run", making it safe to restore a persisted `true`. (See open question in Dev Notes before finalizing this semantics.)

- [x] Task 5: Test coverage (AC: #1, #2)
  - [x] Subtask 5.1: Pure-parser tests in [GitMetricsTests.cs](../../tests/SpecScribe.Tests/GitMetricsTests.cs) for `ParseNameOnlyLog`: hotspot frequency ordering + top-N truncation; coupling pair counting + `>= 2` threshold + top-N; the file-set-size cap skips huge commits without counting pairs; malformed/empty lines skipped (never-throw); a repo with zero file changes yields empty lists. Mirror the existing feed-raw-git-text-assert-structure pattern.
  - [x] Subtask 5.2: Assert the gate: a `ForgeOptions.Resolve()` test proving `DeepGitAnalytics` defaults to `false`, and that `SiteSettings { DeepGit = true }.Resolve()` produces `DeepGitAnalytics == true`. This pins AC #1's "does not run implicitly" at the option boundary.
  - [x] Subtask 5.3: Templater coverage in [HtmlTemplaterTests.cs](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs): with `ProgressModel.DeepGit` populated the distinct deep panel renders (heading + at least one coupling pair / hotspot path); with `DeepGit = null` the panel is **absent** and the rest of the dashboard is unchanged. Extend the existing `ProgressWithCommits` helper ([HtmlTemplaterTests.cs:149-172](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs)) or add a sibling that also sets `DeepGit`.

- [x] Task 6 (owner follow-up): Promote deep analytics from a dashboard panel to a dedicated page with a change-coupling graph (AC: #2)
  - [x] Subtask 6.1: Move the deep analytics content off the dashboard onto a dedicated `deep-analytics.html` page (new `DeepAnalyticsTemplater`, generated from `SiteGenerator` only when `DeepGit` data exists, gated by `--deep-git`). Add `SiteNav.DeepAnalyticsOutputPath` as the shared path so the generator and the dashboard link can't disagree.
  - [x] Subtask 6.2: Replace the inline dashboard panel with a "View Deep Analytics →" link in the **Git Pulse panel header** (upper right, reusing the `chart-panel-header-row`/`view-epic-link` affordance from the sunburst panel); shown only when `DeepGit` data exists, so the default dashboard is unchanged.
  - [x] Subtask 6.3: Represent change coupling as a node-link **graph** (`Charts.CouplingGraph`) — files as nodes (sized by coupling degree), coupled pairs as weighted edges (width + opacity by co-change count), laid out deterministically on a circle. Pure inline SVG computed at generation time (no JS). Coupling is symmetric, so edges are undirected. Keep the ranked text list (`Charts.CouplingList`) beside it as the precise, screen-reader-friendly equivalent so the graph is never the sole information carrier. Note: this is a **first-cut** visualization; the fuller/formalized version (and the broader hub scope) is deferred to Story 3.8 per owner direction.

## Dev Notes

- **This story is FR-10 only — the opt-in depth layer. It sits on top of Story 3.1 (FR-9, baseline pulse), which is `ready-for-dev` but NOT yet merged.** Do not re-implement baseline signals (last-commit timestamp, 30-day count, top-changed files) here; those are 3.1's job. See the sequencing note below.
- **Sequencing / reuse with Story 3.1 (important).** Story 3.1 introduces a bounded `git log --name-only` parse to compute its "top changed files" ([3-1 story Subtask 1.3](3-1-baseline-git-pulse-insights-on-dashboard.md)). This story needs the *same* raw data (commit→file sets) for hotspots and coupling. If 3.1 has already landed, **reuse/extend its name-only parser** rather than adding a second one, and layer hotspots/coupling on the same fetch. If 3.1 has not landed, introduce the bounded name-only fetch here and keep the parser pure and general so 3.1 can consume it. Either way there must be **one** name-only-log code path, not two. (See open question in the workflow summary.)
- **[Re-plan 2026-07-08] Shared numstat foundation + expanded gate.** Following the Epic-3 git-insights re-plan (owner-directed), this story's bounded name-only parse should be introduced (or upgraded) as a `git log --numstat --pretty=format:…` parse so a **single** git code path feeds not just this story's hotspots/coupling but also the downstream re-plan stories: 3.8 (Git Insights hub — file change frequency, activity over time, contributor attribution), 7.4 (per-file change frequency + attribution), and 7.5 (per-commit files + line churn). `--numstat` adds per-file added/deleted line counts on top of the commit→file-set data. Also: the `--deep-git` opt-in flag introduced here is the intended gate for the heavier **hub + per-file/per-commit detail-page generation** too (not just this panel), keeping the FR-10 performance budget intact. Contributor attribution is now **permitted** by the amended PRD non-goal (attribution, not ranking) — but still keep `%an` out of *this* story's hotspots/coupling panels, which remain file-path signals; author attribution surfaces on the file/commit detail pages, not here. See [[worktree-edits-must-target-worktree-path]] re: the active auto-committer on `main`.
- **Reuse, don't reinvent.** `GitMetrics` in [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) already owns the single git integration point: private `RunGit` (3s `Timeout`, UTF-8 stdout, never-throw), the `ParseLog` pure-parser pattern, and the `TryCompute` orchestration. Add `TryComputeDeep` + `ParseNameOnlyLog` **inside this class** so they inherit `RunGit` and `Timeout`. Adding a parallel git module would duplicate the encoding/timeout/never-throw discipline that already exists.
- **Never-throw contract is architectural, not optional.** `GitMetrics` yields `null` on any failure by design ([GitMetrics.cs:20-22](../../src/SpecScribe/GitMetrics.cs)); callers treat `null` as "no data". AD-4 in the spine: "Optional insight providers may enrich output but never own baseline success." [Source: ARCHITECTURE-SPINE.md#AD-4]. Deep analytics are the canonical example of an additive, non-blocking provider — a deep failure must never break baseline generation or the baseline pulse.
- **Performance gate is the whole point of the story.** FR-10's testable consequence is "baseline generation time with deep metrics disabled does not regress more than 10%" [Source: prd.md:134]. You satisfy this by *not running* deep work when the flag is off — the `_options.DeepGitAnalytics ? TryComputeDeep(...) : null` gate (Subtask 1.3) means zero extra git process invocations in the default path. Do not attempt a flaky wall-clock timing test; pin the gate at the option boundary (Subtask 5.2) and the panel-absence at the render boundary (Subtask 5.3) instead.
- **Bound history and pair cost (two distinct scaling traps).** (1) An uncapped `git log --name-only` embeds/reads full history — [deferred-work.md:36-38](../../_bmad-output/implementation-artifacts/deferred-work.md) already flags this as a 3s-timeout risk on mature repos; cap with `-n`/`--since`. (2) Coupling is O(files²) per commit — one bulk-import/merge commit with thousands of files would generate millions of pairs; skip oversized commits (Subtask 2.5).
- **Distinct-from-baseline is an explicit AC.** AC #2 requires deep insights be "surfaced distinctly from baseline metrics". Give them their own labeled panel; do not fold hotspots into 3.1's baseline "top changed files" stat/list or the existing Commit Activity heatmap. If both a baseline top-changed-files list (3.1) and deep hotspots (this story) are visible, differentiate them clearly (e.g. baseline = simple top-5 recency list; deep = ranked churn hotspots + coupling), so they don't read as redundant.
- **No people-ranking.** PRD non-goal [Source: prd.md:137, prd.md:178]: no per-author productivity metrics or leaderboards. Hotspots and coupling are **file-path** signals (which files change often / together), never author signals. Do not surface `%an` in these panels.
- **Charts are pure inline SVG/HTML + CSS variables — no JS** ([Charts.cs:6-8](../../src/SpecScribe/Charts.cs)). This is a deliberate, established project convention (the only sanctioned script is the tiny tooltip/copy enhancer). Render the deep panel the same way; reuse existing neutral chart CSS variables and `Charts.Plural` / `Html()`. Coupling/hotspots are not lifecycle statuses, so the `--status-*` token system does not apply.
- **Aspirational architecture is NOT this story.** [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md) and [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md) describe a target `IInsightProvider` / `SpecScribe.Core` package split that **does not exist yet** — the codebase is the single monolithic `src/SpecScribe` project. Per the spine's "Seed, Not Invariant": "the current monolithic implementation can be refactored as long as the shared-core contract stays intact" [Source: ARCHITECTURE-SPINE.md:100]. Do **not** introduce `IInsightProvider` or restructure into the seed layout here — extend `GitMetrics.cs` in place, matching the code's actual shape.
- **Settings/CLI/persistence plumbing already exists** — reuse it, don't build a parallel one: `SiteSettings : CommandSettings` (Spectre.Console.Cli options) → `ForgeOptions.Resolve` (resolved run config) → `SettingsStore` (`.specscribe` JSON persistence, best-effort, `ApplyTo` merges saved onto live where CLI didn't set). `--no-readme`/`IncludeReadme` is the exact bool precedent to copy for the CLI→ForgeOptions flow.

### Project Structure Notes

- All changes land in the existing single-project layout — no new files/folders:
  - `src/SpecScribe/SiteSettings.cs` — `--deep-git` option
  - `src/SpecScribe/ForgeOptions.cs` — `DeepGitAnalytics` resolved flag
  - `src/SpecScribe/GitMetrics.cs` — `DeepGitPulse` record, `TryComputeDeep`, `ParseNameOnlyLog`
  - `src/SpecScribe/ProgressModel.cs` — `DeepGit` field + `Empty` default
  - `src/SpecScribe/ProgressCalculator.cs` — optional `deep` param threaded into construction
  - `src/SpecScribe/SiteGenerator.cs` — gated `TryComputeDeep` call (~line 402-403)
  - `src/SpecScribe/Charts.cs` — pure-SVG deep panel helper
  - `src/SpecScribe/HtmlTemplater.cs` — dashboard wiring (`AppendDashboard`, ~line 260-269)
  - `src/SpecScribe/Commands.cs` — interactive `ConfigurePaths` toggle
  - `src/SpecScribe/SettingsStore.cs` — `bool? DeepGit` persistence
  - `tests/SpecScribe.Tests/GitMetricsTests.cs`, `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — tests
- The UX docs (DESIGN.md / EXPERIENCE.md) do **not** specify deep-analytics copy or layout (deep git was never storyboarded), so the dev has latitude on panel wording/visuals — but must stay within the established antiquarian design system and accessibility conventions from Stories 1.4/1.5 (text labels never color-only, focusable/tooltip semantics consistent with other chart panels).
- No `architecture.md` exists; the closest analogs are the spec companions cited above.

### References

- [Source: epics.md#Story 3.2 (lines 419-437)] — Epic 3 goal, FRs covered (FR9-FR11, FR14), and this story's user story + acceptance criteria.
- [Source: prd.md#FR-10 (lines 129-137)] — "Optional deeper git insights": opt-in-to-control-performance assumption, independent-toggle + ≤10% regression consequences, and the productivity-ranking non-goal.
- [Source: prd.md#FR-9 (lines 122-127)] — Baseline pulse (Story 3.1) that this story sits on top of and must not duplicate.
- [Source: prd.md line 178, line 193] — Explicit non-goals: no people-ranking git metrics; "deep git metrics remain optional".
- [Source: settings-and-signals.md#Directory-Scoped Settings] — Git insights controls ("baseline pulse on/off, depth tier, time window, hotspots/coupling toggles") required across "interactive options + CLI parameters"; grounds the NFR7 parity + persistence tasks.
- [Source: ARCHITECTURE-SPINE.md#AD-4] — Optional insight providers are additive/non-blocking and never own baseline success.
- [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant (line 100)] — Current monolithic implementation is intentional; don't force the seed package/interface split in this story.
- [Source: src/SpecScribe/GitMetrics.cs] — `GitPulse`/`CommitInfo` records, `TryCompute`, `ParseLog` (pure-parser pattern), `RunGit` (3s timeout, UTF-8 stdout, never-throw) to extend.
- [Source: src/SpecScribe/SiteSettings.cs:9-32] / [src/SpecScribe/ForgeOptions.cs:53-99] — CLI option → resolved-config flow; `--no-readme`/`IncludeReadme` bool precedent.
- [Source: src/SpecScribe/Commands.cs:142-160] / [src/SpecScribe/SettingsStore.cs] — Interactive `ConfigurePaths` + `.specscribe` persistence (`ApplyTo` `??=` merge) for the NFR7 parity task.
- [Source: src/SpecScribe/SiteGenerator.cs:400-403] — Single git-invocation site feeding `ProgressCalculator.Compute`; where the gated deep call belongs.
- [Source: src/SpecScribe/ProgressModel.cs:32-45] — `GitPulse? Git` field + `Empty` default; pattern to mirror for `DeepGit`.
- [Source: src/SpecScribe/HtmlTemplater.cs:210-269] — `AppendDashboard`, including the `chart-row` (epic-status donut + Commit Activity heatmap) the new deep panel sits after.
- [Source: src/SpecScribe/Charts.cs:6-19] — Pure-SVG-no-JS chart convention and `Plural`/`StatCard` helpers to reuse.
- [Source: tests/SpecScribe.Tests/HtmlTemplaterTests.cs:149-172] — `ProgressWithCommits` helper to extend for `DeepGit` templater coverage.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md lines 36-38] — Prior review flagged uncapped `git log` history as a 3s-timeout scaling risk; bound the new name-only call.
- [Source: _bmad-output/implementation-artifacts/3-1-baseline-git-pulse-insights-on-dashboard.md] — Baseline pulse story; its name-only-log parser is the reuse target (see sequencing note).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Opus 4.8)

### Debug Log References

- Verified `git log --numstat --pretty=format:%x01%H` output shape empirically before writing the parser (sentinel header line, then `added\tdeleted\tpath` rows, blank line between commits, `-\t-\tpath` for binaries).
- End-to-end: `dotnet run --project src/SpecScribe -- generate --deep-git` renders the "Deep Analytics" panel with real repo coupling (co-change counts 19→15 descending) + hotspots; `dotnet run ... -- generate` (no flag) emits no `deep-git-panel` while the baseline `<h3>Git Pulse</h3>` panel remains.

### Completion Notes List

- **AC #1 (opt-in / no implicit deep work):** `--deep-git` → `SiteSettings.DeepGit` → `ForgeOptions.DeepGitAnalytics`, gated at the single git-invocation site in `SiteGenerator` with `_options.DeepGitAnalytics ? GitMetrics.TryComputeDeep(...) : null`. When the flag is off, `TryComputeDeep` is never called, so no extra git process runs and baseline timing cannot regress — the gate *is* the performance guarantee. Pinned at the option boundary (`ForgeOptionsTests`) and the render boundary (`HtmlTemplaterTests`), not a flaky wall-clock test.
- **AC #2 (distinct + non-fatal):** deep signals render in their own labeled "Deep Analytics (opt-in)" `chart-panel` — Git Hotspots (proportional bars) + Change Coupling (ranked pairs) — visibly separate from the baseline Git Pulse. `TryComputeDeep` is a separate never-throw call from `TryCompute`, so a deep failure returns `null` (panel omitted) while the baseline pulse stays intact (AD-4).
- **Panel omission vs. empty state (Subtask 3.4):** unlike the baseline stat cards' `—` placeholder, the deep panel does not exist at all when `p.DeepGit is null`, so the default dashboard is byte-for-byte unchanged for users who never pass `--deep-git`. When the flag is on but a repo has no significant coupling, each half degrades to a friendly in-panel note.
- **Design decision — `--numstat` over bare `--name-only` (deviation from Subtask 2.3 literal text, per this story's [Re-plan 2026-07-08] Dev Note and downstream Story 3.8 which references "3.2's numstat parse"):** the single bounded git code path is `git log --numstat --pretty=format:%x01%H -n 300`, so downstream hub/detail stories (3.8, 7.4, 7.5) can extend the same fetch instead of adding a second `git log`. This story's `DeepGitPulse` and parser intentionally use only the file-set (ignoring the added/deleted columns) — the record stays scoped to the file-path signals 3.2 renders; no unused line-churn fields were added. Parser named `ParseNumstatLog`.
- **Scaling guards:** history bounded with `-n 300` (deferred-work.md's 3s-timeout risk); coupling's O(n²) pair cost guarded by skipping commits touching more than `CouplingFileSetCap` (50) files — those still count toward hotspot frequency, only their pairs are skipped.
- **No people-ranking:** hotspots/coupling are file-path signals only; `%an` is never surfaced in these panels (PRD non-goal upheld).
- **NFR7 parity + persistence:** `--deep-git` is also reachable via the interactive "Configure paths" Confirm prompt and persisted to `.specscribe` (`SavedSettings.DeepGit` bool?, tri-state; only `true` is written; `ApplyTo` restores a saved `true` when the CLI didn't request it — CLI still wins).
- **Owner follow-up (Task 6): dedicated page + coupling graph.** Per owner direction, the deep analytics moved off the dashboard onto a dedicated `deep-analytics.html` page (`DeepAnalyticsTemplater`, generated only when `DeepGit` data exists). The dashboard's inline panel was replaced by a "View Deep Analytics →" link in the Git Pulse panel header (upper right). Change coupling is now a node-link **graph** (`Charts.CouplingGraph`) — circular deterministic layout, nodes sized by degree, edges weighted by co-change count, pure inline SVG (no JS), with the ranked `CouplingList` beside it as the text equivalent. `DeepGitPanel` was refactored into reusable `HotspotBars` + `CouplingList`. **Scope note:** this is a first-cut visualization; the formalized/expanded version and the broader Git Insights hub are Story 3.8's job (owner-directed split), so I intentionally did not gold-plate the graph here.
- **Concurrency note:** Story 3.3 (`ArtifactCoveragePanel` / Planning Coverage) landed in the same shared files (`Charts.cs`, `HtmlTemplater.cs`, tests) via the working tree during this session; my edits were kept surgical and both coexist. Only 3.2's files are claimed below.
- **Verification:** full suite green — 480 tests pass (18 new for 3.2 across both passes). Rendered end-to-end and screenshot-verified: the graph, the ranked list, the hotspot bars, and the dashboard "View Deep Analytics →" link all render on this repo's real data.

### File List

- `src/SpecScribe/SiteSettings.cs` — `--deep-git` CLI option; `Resolve()` passes `deepGitAnalytics`
- `src/SpecScribe/ForgeOptions.cs` — `DeepGitAnalytics` resolved flag + `deepGitAnalytics` param
- `src/SpecScribe/SiteGenerator.cs` — gated `TryComputeDeep` call; threads `deepGit` into `Compute`
- `src/SpecScribe/GitMetrics.cs` — `DeepGitPulse` record, `TryComputeDeep`, pure `ParseNumstatLog`, `CouplingFileSetCap`
- `src/SpecScribe/ProgressModel.cs` — `DeepGit` field + `Empty` default
- `src/SpecScribe/ProgressCalculator.cs` — optional `DeepGitPulse? deep` param threaded into construction
- `src/SpecScribe/Charts.cs` — refactored `DeepGitPanel` into reusable `HotspotBars` + `CouplingTable` (aligned ranked table); added the pure-SVG `CouplingGraph` node-link diagram with scalable user-unit labels (+ `Basename`/`Shorten` helpers)
- `src/SpecScribe/DeepAnalyticsTemplater.cs` — **new** dedicated `deep-analytics.html` page (coupling graph + expand/zoom lightbox + ranked table + hotspots)
- `src/SpecScribe/SiteNav.cs` — `DeepAnalyticsOutputPath` shared constant
- `src/SpecScribe/SiteGenerator.cs` — gated `TryComputeDeep` call; threads `deepGit` into `Compute`; generates the deep-analytics page when `DeepGit` data exists
- `src/SpecScribe/HtmlTemplater.cs` — dashboard wiring: "View Deep Analytics →" link in the Git Pulse header when `DeepGit` non-null (inline panel removed)
- `src/SpecScribe/ProgressModel.cs` — `DeepGit` field + `Empty` default
- `src/SpecScribe/ProgressCalculator.cs` — optional `DeepGitPulse? deep` param threaded into construction
- `src/SpecScribe/Commands.cs` — interactive deep-git Confirm toggle in `ConfigurePaths`
- `src/SpecScribe/SettingsStore.cs` — `SavedSettings.DeepGit` persistence (`IsEmpty`/`TrySave`/`ApplyTo`)
- `src/SpecScribe/assets/specscribe.css` — `.deep-git-*` list styles, `.coupling-*` graph styles, `.deep-page-*` page-layout styles (neutral chart tokens, no new colors)
- `tests/SpecScribe.Tests/GitMetricsTests.cs` — `ParseNumstatLog` coverage (hotspots, coupling, cap, malformed, empty)
- `tests/SpecScribe.Tests/ForgeOptionsTests.cs` — deep-git gate default-off + flag-flow tests
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — "View Deep Analytics" link present when populated / absent when null
- `tests/SpecScribe.Tests/DeepAnalyticsTemplaterTests.cs` — **new** page shell + coupling-graph/list/hotspots coverage
- `tests/SpecScribe.Tests/SettingsStoreTests.cs` — `DeepGit` persistence + `ApplyTo` precedence tests
- `.github/workflows/publish-docs-live-pages.yml` — enable `--deep-git` in the live-docs CI pipeline (relies on the existing `fetch-depth: 0` full-history checkout)
- `README.md` — document the `--deep-git` option (options table + GitHub Actions example YAML, with `fetch-depth: 0` added so the example produces real git data)
- `.claude/launch.json` — **new** dev-preview static-server config for `SpecScribeOutput` (used to screenshot-verify the page)

## Change Log

- 2026-07-08 — Story 3.2 implemented: opt-in `--deep-git` deep git analytics (change coupling + hotspots), gated so baseline generation is unaffected when off. Shared bounded `git log --numstat` foundation, never-throw provider, interactive/CLI parity + `.specscribe` persistence. (claude-opus-4-8)
- 2026-07-08 — Owner follow-up: promoted deep analytics to a dedicated `deep-analytics.html` page with a node-link change-coupling graph; replaced the dashboard panel with a "View Deep Analytics →" link in the Git Pulse header. First-cut visualization; formalization deferred to Story 3.8. Full suite 480 green. (claude-opus-4-8)
- 2026-07-08 — Owner polish round on the page: graph labels enlarged and made scalable (SVG user-unit font-size) so they read clearly and grow when zoomed; added a pure-CSS `:target` expand/zoom lightbox (no JS, mirrors the heatmap drill-down); converted the ranked pairs from a list to a proper aligned `<table>` (`Charts.CouplingTable`, replacing `CouplingList`); tidied the panel header ("Ranked Pairs" + count) and added a graph legend. DOM/computed-style verified (labels 13px inline → 20px enlarged; lightbox opens on `:target`). Full suite 494 green. (claude-opus-4-8)
