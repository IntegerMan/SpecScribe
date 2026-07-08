# Story 3.2: Optional Deep Git Analytics Controls

Status: ready-for-dev

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

- [ ] Task 1: Add the opt-in toggle through the settings/options plumbing (AC: #1)
  - [ ] Subtask 1.1: Add a `--deep-git` boolean `[CommandOption]` to [SiteSettings.cs](../../src/SpecScribe/SiteSettings.cs), mirroring the existing `--no-readme` bool pattern exactly (option + `[Description]`). Suggested description: `"Enable deeper git analytics (change coupling and hotspots) as an opt-in dashboard panel. Default: off, so baseline generation performance is unaffected."` Default is `false` (opt-in), so the positive flag form is correct — do **not** add a `--no-*` inverse.
  - [ ] Subtask 1.2: Add `public required bool DeepGitAnalytics { get; init; }` to [ForgeOptions.cs](../../src/SpecScribe/ForgeOptions.cs) and a `bool deepGitAnalytics = false` parameter to `ForgeOptions.Resolve(...)`, threading it into the returned object — mirror precisely how `includeReadme` / `IncludeReadme` already flows. Update `SiteSettings.Resolve()` (line 31) to pass `deepGitAnalytics: DeepGit`.
  - [ ] Subtask 1.3: **Gate the deep computation at the single call site.** In `SiteGenerator.GenerateEpicsInternal` ([SiteGenerator.cs:402](../../src/SpecScribe/SiteGenerator.cs)), immediately after the existing `var gitPulse = GitMetrics.TryCompute(_options.RepoRoot);`, add: `var deepGit = _options.DeepGitAnalytics ? GitMetrics.TryComputeDeep(_options.RepoRoot) : null;`. This ternary **is** AC #1's guarantee — when the flag is off, `TryComputeDeep` is never called, so no extra git process runs and baseline timing cannot regress. Git is invoked once per full pass here, not per page (same as the baseline pulse) — do not call it anywhere else.

- [ ] Task 2: Compute the deep signals as a never-throw, bounded provider (AC: #1, #2)
  - [ ] Subtask 2.1: Add a `DeepGitPulse` positional `record` to [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) alongside `GitPulse` / `CommitInfo`. Suggested shape: `public sealed record DeepGitPulse(IReadOnlyList<(string Path, int Changes)> Hotspots, IReadOnlyList<(string FileA, string FileB, int CoChanges)> Coupling);`
  - [ ] Subtask 2.2: Add `public static DeepGitPulse? TryComputeDeep(string repoRoot)` **inside the existing `GitMetrics` class** so it reuses the private `RunGit` helper and the shared 3-second `Timeout` — do **not** write a second process-invocation helper. It must obey the same never-throw contract (`try { … } catch { return null; }`) as `TryCompute` ([GitMetrics.cs:20-22, 27-60](../../src/SpecScribe/GitMetrics.cs)): any failure yields `null`, which the dashboard treats as "no deep data", never an error. [Source: ARCHITECTURE-SPINE.md#AD-4]
  - [ ] Subtask 2.3: Fetch commit→file-set data with a **single, bounded** name-only log call, e.g. `log --name-only --pretty=format:%x01%H -n 300` (leading `\x01` sentinel per commit lets the parser find commit boundaries without a blank-line ambiguity). **Cap history** with `-n` (or `--since`) — [deferred-work.md:36-38](../../_bmad-output/implementation-artifacts/deferred-work.md) and Story 3.1's notes both flag that an uncapped `git log` blows the 3s `RunGit` budget on mature repos. Reuse `RunGit`'s already-solved UTF-8 stdout encoding ([GitMetrics.cs:119](../../src/SpecScribe/GitMetrics.cs)) so non-ASCII file paths don't mojibake.
  - [ ] Subtask 2.4: Parse into a **pure, static, repo-free helper** (mirror `ParseLog`'s pattern — takes raw git text, returns parsed data, skips malformed lines). Suggested: `public static DeepGitPulse ParseNameOnlyLog(string logText, ...)`. Group file paths by commit; **Hotspots** = per-path change frequency, sorted desc, top ~10; **Coupling** = for each commit's file set, count unordered co-changed pairs, sorted desc, keep pairs with `CoChanges >= 2`, top ~10.
  - [ ] Subtask 2.5: **Guard the pairwise cost.** A single bulk/merge/vendored-import commit touching thousands of files would explode the O(n²) pair count. Skip commits whose file-set size exceeds a cap (e.g. `> 50` files) when building coupling pairs — they are almost always bulk imports, not meaningful co-change signal. Document the cap inline.
  - [ ] Subtask 2.6: Keep deep failure isolated from baseline. `TryComputeDeep` is a separate call from `TryCompute`, so a deep failure returns `null` deep data while the baseline `GitPulse` (Story 3.1's signals) stays intact — partial data beats no data (AC #2 "failures remain non-fatal"). [Source: ARCHITECTURE-SPINE.md#AD-4]

- [ ] Task 3: Thread deep data into the progress model and render a distinct panel (AC: #2)
  - [ ] Subtask 3.1: Add `public DeepGitPulse? DeepGit { get; init; }` to [ProgressModel.cs](../../src/SpecScribe/ProgressModel.cs) (nullable, init-only, defaulting `null` — mirror the existing `GitPulse? Git` field at line 32; add `DeepGit = null` to `ProgressModel.Empty`). Thread it through `ProgressCalculator.Compute` with a new optional `DeepGitPulse? deep = null` parameter so construction stays in one place, and pass `deepGit` at the `SiteGenerator.cs:403` call site.
  - [ ] Subtask 3.2: Add a rendering helper to [Charts.cs](../../src/SpecScribe/Charts.cs) — **pure inline SVG/HTML + CSS variables, no JS** ([Charts.cs:6-8](../../src/SpecScribe/Charts.cs) class doc; this is a deliberate, established convention, not an oversight). Render the hotspots list and the coupling pairs. Reuse `Charts.Plural` for count labels, the existing `Html()`/escaping helper for paths, and existing chart/design-system CSS variables (parchment/ink/rust neutral chart tokens) — do not invent new colors. Coupling/hotspots are **not** statuses, so the `--status-*` token system does not apply here.
  - [ ] Subtask 3.3: Render the panel from `AppendDashboard` in [HtmlTemplater.cs](../../src/SpecScribe/HtmlTemplater.cs) (in the dashboard body, e.g. after the existing `chart-row` that holds the epic-status donut + Commit Activity heatmap, ~line 260-269). Make it **visibly distinct from baseline metrics** (AC #2): its own `chart-panel` with a heading that names it as deeper/opt-in analytics (e.g. `"Change Coupling"` / `"Git Hotspots"` under a "Deep Analytics" framing) so it never reads as part of the baseline Git Pulse / Commit Activity surface.
  - [ ] Subtask 3.4: **Omit the panel entirely when `p.DeepGit is null`** (flag off, or deep computation failed) — unlike the baseline stat cards that show a `"—"` empty state, the deep panel should simply not exist when not opted into, so the default dashboard is byte-for-byte unchanged for users who never pass `--deep-git`. When the flag is on but the repo has no significant coupling, render the panel with a friendly in-panel note (e.g. `"No significant change coupling detected."`) rather than a broken/empty chart.

- [ ] Task 4: Interactive + CLI parity and persistence (NFR7) (AC: #1)
  - [ ] Subtask 4.1: Add an interactive toggle in `InteractiveCommand.ConfigurePaths` ([Commands.cs:142-160](../../src/SpecScribe/Commands.cs)) — a `Confirm`-style prompt ("Enable deep git analytics?") defaulting to the current `settings.DeepGit`. NFR7 makes menu/CLI parity contractual: every configurable feature must be reachable from both surfaces. [Source: prd.md NFR7; settings-and-signals.md#Git-insights]
  - [ ] Subtask 4.2: Persist the toggle in [SettingsStore.cs](../../src/SpecScribe/SettingsStore.cs) so it survives between runs (directory-scoped `.specscribe`). Because `SavedSettings` uses **nullable** fields for tri-state ("unset" vs set) and `ApplyTo` uses `??=`, add `public bool? DeepGit { get; set; }`, include it in `IsEmpty`, wire it in `TrySave`, and in `ApplyTo` apply the saved value only when the CLI did not request it: `if (!settings.DeepGit && saved.DeepGit == true) settings.DeepGit = true;`. Rationale: the CLI bool defaults `false` and there is no `--no-deep-git`, so `settings.DeepGit == false` unambiguously means "not requested on this run", making it safe to restore a persisted `true`. (See open question in Dev Notes before finalizing this semantics.)

- [ ] Task 5: Test coverage (AC: #1, #2)
  - [ ] Subtask 5.1: Pure-parser tests in [GitMetricsTests.cs](../../tests/SpecScribe.Tests/GitMetricsTests.cs) for `ParseNameOnlyLog`: hotspot frequency ordering + top-N truncation; coupling pair counting + `>= 2` threshold + top-N; the file-set-size cap skips huge commits without counting pairs; malformed/empty lines skipped (never-throw); a repo with zero file changes yields empty lists. Mirror the existing feed-raw-git-text-assert-structure pattern.
  - [ ] Subtask 5.2: Assert the gate: a `ForgeOptions.Resolve()` test proving `DeepGitAnalytics` defaults to `false`, and that `SiteSettings { DeepGit = true }.Resolve()` produces `DeepGitAnalytics == true`. This pins AC #1's "does not run implicitly" at the option boundary.
  - [ ] Subtask 5.3: Templater coverage in [HtmlTemplaterTests.cs](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs): with `ProgressModel.DeepGit` populated the distinct deep panel renders (heading + at least one coupling pair / hotspot path); with `DeepGit = null` the panel is **absent** and the rest of the dashboard is unchanged. Extend the existing `ProgressWithCommits` helper ([HtmlTemplaterTests.cs:149-172](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs)) or add a sibling that also sets `DeepGit`.

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

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List
