# Story 7.4: Advanced Code and Git Coverage

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an advanced user exploring the codebase,
I want deeper code-and-git coverage on code pages,
so that I can see how files have changed and where change concentrates.

## Acceptance Criteria

1.
**Given** code pages and git history are available
**When** advanced coverage is enabled
**Then** code pages surface history/blame-style annotations, per-file change frequency, contributor attribution (who changed the file, not a productivity ranking), and change-coupling/hotspot signals as an opt-in extension
**And** baseline code and portal generation performance is unaffected when it is disabled. [Source: epics.md#Story 7.4 (lines 903-915); FR19]

2.
**Given** git history is unavailable or partial
**When** advanced coverage runs
**Then** it degrades non-fatally
**And** code pages still render their baseline content. [Source: epics.md#Story 7.4 (lines 917-921); FR19, NFR-2]

---

## Developer Context

**Read this first — this is an enrichment story on two existing seams, not a new page class.** 7.4 adds a **per-file advanced-coverage section** to the code file pages that Story 7.1 renders, populated from the **single deep-git numstat pass** that Story 3.2 landed. Almost everything you need already exists in some form; your net-new work is (a) extending the one deep-git fetch/parse to also produce *per-file* signals, and (b) an opt-in section on the code page that renders them. The three highest risks are: **(1)** adding a second git invocation instead of extending the one shared path, **(2)** over-reading "blame-style" as literal per-line `git blame` (a heavy, unbounded per-file git call that breaks the perf invariant), and **(3)** building against a render target (7.1's code pages) that is **not merged yet** — this story is sequenced *after* 7.1.

### Two hard truths about sequencing (read before estimating)

- **Story 7.1 (In-Portal Code File Browsing) is a genuine prerequisite, not a guarded soft-link.** 7.4 enriches code pages — if there is no `CodeFileTemplater` and no `_codePages` map, there is *nothing to attach annotations to*. 7.1 is currently `ready-for-dev` (not merged): `src/SpecScribe/CodeFileTemplater.cs` **does not exist on disk yet.** Do not start 7.4's render half until 7.1 has landed (or is being landed in the same worktree). The *data* half (extending the deep-git parse) can proceed independently and is even worth landing first. [Source: [7-1 story](7-1-in-portal-code-file-browsing.md); `Glob src/SpecScribe/*.cs` shows no `CodeFileTemplater.cs`]
- **The deep-git fetch does not currently carry author or date.** Today `TryComputeDeep` runs `git log --numstat --pretty=format:%x01%H -n 300` — the `%x01%H` header line carries only the hash, and `ParseNumstatLog` uses only the *file paths*, ignoring even the hash. Contributor attribution and per-file history need `%an`/`%ad` (and `%s`), so **the shared fetch format must be extended** (add tab-delimited fields to the sentinel header line). This is the same fetch Story 7.5 must also extend (it needs `%b`, the body). Coordinate: **one fetch, one format, one parse** — see [[deep-git-single-numstat-path]]. [Source: [GitMetrics.cs:213](../../src/SpecScribe/GitMetrics.cs), [GitMetrics.cs:270-286](../../src/SpecScribe/GitMetrics.cs)]

### What already exists (reuse / extend, do not rebuild)

- **The single deep-git code path: `GitMetrics.TryComputeDeep` + `ParseNumstatLog` → `DeepGitPulse`.** One bounded `git log --numstat --pretty=format:%x01%H -n 300`, feeding a pure parser that produces repo-wide `Hotspots` (per-path change frequency) and `Coupling` (co-changed file pairs). Gated behind `--deep-git` (`ForgeOptions.DeepGitAnalytics`); when off, `TryComputeDeep` never runs — *that gate IS the FR-19/AC-#1 perf guarantee*. **Extend this in place; do not add a second git log or a parallel git module.** [Source: [GitMetrics.cs:197-306](../../src/SpecScribe/GitMetrics.cs); [[deep-git-single-numstat-path]]]
- **The deep-git wiring on the generator.** Deep git is computed once inside `GenerateEpicsInternal` (`_options.DeepGitAnalytics ? GitMetrics.TryComputeDeep(...) : null`), threaded via `ProgressCalculator.Compute(..., deepGit)` onto `ProgressModel.DeepGit`, and consumed at generation time (the deep-analytics page gate at `SiteGenerator.cs:139`). Your per-file map rides the **same** `DeepGitPulse`/`ProgressModel.DeepGit` instance — no new compute call, no new threading. [Source: [SiteGenerator.cs:438-443](../../src/SpecScribe/SiteGenerator.cs), [ProgressCalculator.cs:8,68](../../src/SpecScribe/ProgressCalculator.cs), [ProgressModel.cs:37](../../src/SpecScribe/ProgressModel.cs)]
- **The deep-analytics render precedents.** `DeepAnalyticsTemplater` already renders hotspots (`Charts.HotspotBars`) and coupling (`Charts.CouplingTable`/`Charts.CouplingGraph`) as pure HTML/CSS + inline SVG, no JS. Reuse these *primitives* (and their neutral styling) for the per-file section; do not invent new chart shapes. [Source: [DeepAnalyticsTemplater.cs](../../src/SpecScribe/DeepAnalyticsTemplater.cs), [Charts.cs:793,818,850](../../src/SpecScribe/Charts.cs)]
- **The code-page render target + path map (Story 7.1).** `CodeFileTemplater.RenderPage(...)` renders one code file; `SiteGenerator._codePages` caches repo-relative source path → `code/<path>.html` output path. 7.4 hooks the annotation section into that templater and uses `_codePages` to link a file's *coupled files* to their own code pages (guarded on existence). [Source: [7-1 story:37,77](7-1-in-portal-code-file-browsing.md)]
- **The per-commit detail pages (Story 7.5) as a guarded link target.** A file's change-history entries link each commit to `commit/{shortHash}.html` **when that page exists** (7.5's output). 7.5 is `backlog`; guard the link on a cached commit-page set exactly as 7.3 does — plain `<code>` hash otherwise, never a dead link. [Source: [7-5 story:30-33](7-5-per-commit-detail-pages.md), [7-3 story:88](7-3-activity-timeline-and-date-pages.md)]
- **Invariant/culture-safe formatting + escaping + no-JS conventions.** `Charts.D`/`DReadable` (dates), `Charts.Plural` (counts), `PathUtil.Html` (escape every derived string). [Source: [Charts.cs:935,939,1031](../../src/SpecScribe/Charts.cs); [[charting-is-pure-svg-no-js]]]

### The core design in one paragraph

Extend the **one** deep-git fetch's pretty-format from `%x01%H` to a tab-delimited sentinel header `%x01%H%x09%an%x09%ad` (add `%s` too if cheap) with `--date=format:%Y-%m-%d`, so each commit boundary line now carries hash, author, and date alongside the following `added\tdeleted\tpath` numstat lines. Extend the pure parser to also build a **per-file insight map** — `IReadOnlyDictionary<string, FileInsight>` keyed by repo-relative path — where each `FileInsight` carries the file's change count, its distinct **contributors** (author → commits-touching count, framed as attribution not ranking), its top **coupled files** (derived from the same co-change pair data, filtered to pairs involving this path), and a bounded **change history** (recent commits that touched the file: short hash, date, author, subject). Hang this map on `DeepGitPulse` (a new property) so it rides the existing `ProgressModel.DeepGit` with no new git call and no new threading. On the render side, `CodeFileTemplater` (7.1) gains an optional `FileInsight?` parameter; when advanced coverage is on **and** the file has an insight, it renders a neutral-token "Advanced coverage" section under the code — contributors, change frequency/hotspot standing, coupled-file links (to their `code/…html` pages, guarded), and the change-history list (each commit → `commit/{hash}.html`, guarded on 7.5's page set). Everything is opt-in on `--deep-git`, pure HTML/CSS (no JS, no per-line blame call), HTML-escaped, invariant-formatted, and degrades to *nothing rendered* (baseline code page intact) whenever git is off/absent/partial or the file has no insight.

### "History/blame-style annotations" — scope this deliberately (critical)

The AC phrase is **"history/blame-style annotations."** Do **not** implement literal per-line `git blame`:

- **Per-line blame is a separate, unbounded git call per file** (`git blame <file>`), executed once for every referenced code page. That is N extra git processes with no shared-fetch story, directly violating the single-deep-git-path invariant ([[deep-git-single-numstat-path]]) and the NFR-1 / AC-#1 "baseline performance unaffected" contract. It also can't be derived from `--numstat` data.
- **The bounded, honest reading is a per-file *change history*:** the list of recent commits that touched this file (short hash, date, author, subject), which *is* derivable from the shared numstat pass and answers "how has this file changed" without a blame call. This is what "history/blame-style" means here. Render it as a compact list on the code page.
- **Literal per-line last-author blame is explicitly deferred** (a later polish, if ever — it needs its own bounded strategy and settings knob). Note this in Dev Notes; do not add it opportunistically. If the owner specifically wants per-line blame, that is a scope change to raise, not absorb.

---

## Dependencies / Sequencing

- **Hard prerequisite — Story 7.1 (code pages).** 7.4's render half attaches to `CodeFileTemplater` + `_codePages`. Sequence 7.4 after 7.1 lands. The data half (deep-git parse extension) is independent and may land first.
- **Extend, don't fork, the deep-git path (Story 3.2).** `TryComputeDeep`/`ParseNumstatLog`/`DeepGitPulse` is THE deep-git fetch. Add the per-file map to it; keep the single `git log --numstat` call. [[deep-git-single-numstat-path]]
- **Shared fetch-format change coordinates with Story 7.5.** 7.5 also extends this exact fetch (it needs `%b`, the multi-line body, which requires a record-boundary sentinel). 7.4 needs only single-line fields (`%an`/`%ad`/`%s`) on the header line. Whoever lands first owns extending the pretty-format; the other reuses it. Design the header line so 7.5's later `%b` addition is additive (body goes *after* the single-line fields with its own field sentinel, or on its own sentinel-delimited record — coordinate). Do **not** create a second fetch for bodies. [Source: [7-5 story:20,28,40](7-5-per-commit-detail-pages.md); [[deep-git-single-numstat-path]]]
- **Guarded outgoing links** (established renderer discipline):
  - **Coupled-file links** → the coupled file's `code/<path>.html` page, **only when it's in `_codePages`** (a coupled file may not itself be a referenced code page). Plain text otherwise.
  - **Change-history commit links** → `commit/{shortHash}.html` (Story 7.5, `backlog`), **only when the commit-page set contains the hash**. Plain `<code>` otherwise. Reuse the same nullable `_commitPages` set seam 7.3 introduces (empty default until 7.5 populates it). [Source: [7-3 story:88](7-3-activity-timeline-and-date-pages.md)]
- **External code-link mode (Story 7.1's `CodeSourceBaseUrl`).** When a base URL is set, 7.1 skips in-portal code-page generation entirely — so there are **no code pages to enrich**, and 7.4's annotations simply aren't rendered (there's no page). That's correct graceful behavior; do not try to render annotations onto external GitHub links. (Optional, only if trivial: surface the per-file insight on the deep-analytics hub instead — but the AC targets code pages, so treat hub surfacing as out of scope unless the owner asks.) [Source: [7-1 story:48-59](7-1-in-portal-code-file-browsing.md); [[epic-7-code-link-strategy]]]

### Scope boundary (read carefully)

- **IN scope:**
  - Extending the **single** deep-git fetch format (`%an`/`%ad` on the header line) + the pure parser to also produce a **per-file `FileInsight` map** (change count, contributors, coupled files, bounded change history).
  - An **opt-in per-file "Advanced coverage" section** on the code page (7.1's `CodeFileTemplater`), rendering those four signals, gated on `--deep-git` + data present.
  - Guarded links (coupled files → code pages; history commits → per-commit pages).
  - Graceful degradation everywhere (git off/absent/partial, no insight for a file, external mode → no section, baseline page intact).
- **OUT of scope:**
  - **Literal per-line `git blame`** (unbounded per-file git call; deferred — see above).
  - **Any second git invocation** or a parallel git module. [[deep-git-single-numstat-path]]
  - **Author productivity ranking / leaderboards.** Contributors are file-scoped attribution ("who has changed this file"), never a cross-repo ranking. This is a standing PRD non-goal. [Source: [GitMetrics.cs:29-33](../../src/SpecScribe/GitMetrics.cs); [7-5 story:41](7-5-per-commit-detail-pages.md)]
  - **Rebuilding the code page, the hotspot/coupling charts, or the deep-analytics hub.** Reuse 7.1's templater and 3.2/3.8's primitives.
  - **Any JavaScript.** Pure HTML/CSS + (reused) inline SVG. [[charting-is-pure-svg-no-js]]
  - **A new nav entry.** The section lives *inside* existing code pages; nothing new to link from the top nav.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Extend the one deep-git fetch format, minimally and back-compatibly.** Change `TryComputeDeep`'s command from `log --numstat --pretty=format:%x01%H -n 300` to carry author + date on the sentinel header line, tab-delimited: e.g. `log --numstat --pretty=format:%x01%H%x09%an%x09%ad --date=format:%Y-%m-%d -n 300` (add `%s` if you also want the subject in history — recommended for readable history rows). The `%x01` sentinel and `-n 300` bound stay. The date format must use `T`-free, whitespace-free tokens exactly as `TryCompute` does (a space in `--date=format:` splits the single argument string). [Source: [GitMetrics.cs:64-66,213](../../src/SpecScribe/GitMetrics.cs)]
  - **Keep the existing hotspot/coupling parse intact.** `ParseNumstatLog` currently detects a commit boundary by `line[0] == ''` and never parses the header's content — so adding tab fields after `%H` is backward-compatible for that logic. Preserve `Hotspots`/`Coupling` behavior and their tests exactly; add the per-file map alongside.
- **Add a `FileInsight` record and a per-file map to the deep pulse.** Suggested shape (adjust names to house style):
  ```csharp
  public sealed record CommitTouch(string ShortHash, DateOnly Date, string Author, string Subject);
  public sealed record FileInsight(
      int ChangeCount,
      IReadOnlyList<(string Author, int Commits)> Contributors,   // attribution, not ranking; file-scoped
      IReadOnlyList<(string Path, int CoChanges)> CoupledFiles,    // pairs involving this file, desc
      IReadOnlyList<CommitTouch> History);                        // bounded, newest-first
  ```
  Add `IReadOnlyDictionary<string, FileInsight> FileInsights` to `DeepGitPulse` (empty dict, never null, when unavailable). Keyed by the same repo-relative path strings the numstat lines already carry, so it joins cleanly to `_codePages`. [Source: [GitMetrics.cs:34-36](../../src/SpecScribe/GitMetrics.cs), [ProgressModel.cs:37](../../src/SpecScribe/ProgressModel.cs)]
- **Build the per-file map in the pure parser** (`ParseNumstatLog`, or a sibling pure `ParseFileInsights` consuming the same `logText` — a sibling keeps the existing method's contract/tests untouched and is preferred). Per commit: parse the header line's hash/author/date(/subject); for each touched path accumulate change count, add the author to that file's contributor tally, append a `CommitTouch` to its history (cap history per file, e.g. top ~15 newest), and derive coupled files from the same pair data the coupling logic already computes. **Bound everything** — cap contributors and coupled-file lists (e.g. top 5–10), cap history length; document the constants like `CouplingFileSetCap`. Pure, repo-free, never-throw, skip-malformed — mirror `ParseNumstatLog`. [Source: [GitMetrics.cs:234-306](../../src/SpecScribe/GitMetrics.cs)]
- **Reuse the coupling pair data for per-file coupling.** The parser already builds canonical unordered pair counts; a file's coupled list is just those pairs containing the file, mapped to the *other* file + co-change count, sorted desc. Don't recompute pairs a second way. Respect the same `CouplingFileSetCap` skip (bulk commits) so per-file coupling matches the hub's. [Source: [GitMetrics.cs:251-265](../../src/SpecScribe/GitMetrics.cs)]
- **Thread it with zero new git calls.** The map lives on the existing `DeepGitPulse` returned by `TryComputeDeep`, already threaded through `ProgressCalculator.Compute` → `ProgressModel.DeepGit`. Nothing new to compute or pass through the generator for the *data*; the render half reads `_progress.DeepGit.FileInsights`. [Source: [SiteGenerator.cs:442-443](../../src/SpecScribe/SiteGenerator.cs), [ProgressCalculator.cs:8,68](../../src/SpecScribe/ProgressCalculator.cs)]
- **Attach the section via an optional `CodeFileTemplater` parameter.** Add an optional `FileInsight?` (default null) to `CodeFileTemplater.RenderPage(...)`. When non-null, render a distinct `<section class="code-insights">` under the code block: contributors (name + "N commits" attribution), a change-frequency line (this file's change count, and optionally its hotspot standing), coupled files (`<a>` to `code/<path>.html` when in `_codePages`, else plain), and the change history (each `CommitTouch` as a row: date · author · subject, hash linking to `commit/{hash}.html` when present). When null, render nothing extra — the baseline page is byte-identical to 7.1's output. Keep the single-`<main>`/skip-link/breadcrumb a11y shell untouched. [Source: [7-1 story:143,239](7-1-in-portal-code-file-browsing.md), [DeepAnalyticsTemplater.cs:31-92](../../src/SpecScribe/DeepAnalyticsTemplater.cs)]
- **Pass the insight in from the code-pages phase.** In `GenerateCodePagesInternal` (7.1's phase), look up `_progress?.DeepGit?.FileInsights.GetValueOrDefault(repoRelativePath)` per file and pass it to `RenderPage`. When `DeepGit` is null (flag off / no data), every lookup is null → no sections → baseline unaffected. This is the *only* generator change on the render side, and it's additive. [Source: [SiteGenerator.cs:139](../../src/SpecScribe/SiteGenerator.cs) (gate pattern)]
- **Guard both link kinds.** Coupled-file link only when the target path is a generated code page (`_codePages.ContainsKey`); commit-history hash link only when the hash is in the cached commit-page set (the `_commitPages` seam from 7.3 — add it as a nullable empty-default set if 7.3 hasn't landed it yet). Never emit a dead link. [Source: [7-3 story:88](7-3-activity-timeline-and-date-pages.md)]
- **Frame contributors as attribution, not ranking.** File-scoped ("Contributors to this file"), showing who touched it and how many times *for this file*. No global leaderboard, no cross-file aggregation, no "top developer" language. [Source: [GitMetrics.cs:29-33](../../src/SpecScribe/GitMetrics.cs); PRD FR-10 non-goal]
- **Neutral tokens only.** The insights section uses the `--ink`/`--border`/`--parchment`-family chart tokens like the deep-analytics page — **never** `--status-*` lifecycle tokens. [[specscribe-status-token-system]]
- **Invariant formatting + escaping everywhere.** `Charts.D`/`DReadable` for dates, `Charts.Plural` for counts, `PathUtil.Html` for every author/subject/path/hash (git author names and commit subjects are free text and injection surfaces). [Source: [Charts.cs:935,939,1031](../../src/SpecScribe/Charts.cs)]
- **Degrade non-fatally at every branch (AC #2, NFR-2, AD-4).** Git absent → `DeepGit` null → no sections, baseline pages render. Partial data (a file with no history, no contributors, or no coupling) → omit the empty sub-parts, render what exists, never an empty heading, never a throw. Deep pass fails mid-parse → skip the malformed commit, keep going.

### DON'T

- **DON'T add a second git invocation** — no `git blame`, no extra `git log`, no per-file git call. One bounded `--numstat` fetch feeds everything. [[deep-git-single-numstat-path]]
- **DON'T implement literal per-line blame.** "History/blame-style" = per-file change history from the shared fetch (see the scope note). Defer per-line blame.
- **DON'T rank authors or build a leaderboard.** Attribution only, file-scoped. [Source: [GitMetrics.cs:29-33](../../src/SpecScribe/GitMetrics.cs)]
- **DON'T run any of this when `--deep-git` is off.** The section, the map, and the fetch are all behind the existing gate — that gate is the AC-#1 perf guarantee. Never compute per-file insights on the baseline path. [Source: [SiteGenerator.cs:439-442](../../src/SpecScribe/SiteGenerator.cs)]
- **DON'T change baseline code-page output when insights are absent.** A null `FileInsight` must produce exactly 7.1's page. Cover this with a test so a later refactor can't leak an empty section.
- **DON'T add JavaScript.** Pure HTML/CSS + reused inline SVG. [[charting-is-pure-svg-no-js]]
- **DON'T co-opt `--status-*` tokens.** [[specscribe-status-token-system]]
- **DON'T force the Core/Adapters package split.** Extend the monolith in place (extend `GitMetrics`, add to `CodeFileTemplater`) — seed, not invariant. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant; echoed across Epic 3/7 stories]
- **DON'T write back to source** (local-first, read-only invariant). Git + file reads only; output under `OutputRoot`.
- **DON'T use the `--output docs/live` flag** in any manual-verify step — it's vestigial/gitignored; the default is `SpecScribeOutput/`. [[generate-output-dir-is-specscribeoutput]]

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md); [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md)]:

- **AD-4 — optional insight providers are additive, non-blocking, never own baseline success.** Advanced coverage is opt-in (`--deep-git`) and additive: any failure or absence yields no section, and the baseline code page + whole site still generate. [#AD-4]
- **FR-19 / NFR-1 — baseline stays responsive; opt-in depth is gated.** The deep fetch (and thus all per-file computation) runs only under `--deep-git`; the format extension adds two `%`-tokens to the *same* single bounded call, not a new process. Baseline generation timing is unchanged when the flag is off (AC #1). [Source: [GitMetrics.cs:203-222](../../src/SpecScribe/GitMetrics.cs)]
- **Graceful degradation is contractual (NFR-2, AC #2).** Absent/partial git degrades to omitted sections and plain (never dead) links, never a thrown exception; baseline code content always renders.
- **Local-only, read-only.** Git and source reads are read-only; output lands only under `OutputRoot`; no source mutation. [Inherited Invariants]
- **Accessibility is part of the rendering contract (NFR-6, UX-DR16/17).** The insights section inherits the code page's skip-link → single `<main id="main-content">` shell (from 7.1); its lists/tables use semantic markup, links carry meaningful visible text (file paths, dates, subjects — never "click here"), and no signal is color-only. [Source: [7-1 story:101,168](7-1-in-portal-code-file-browsing.md)]
- **Seed, not invariant.** Extend `GitMetrics` (data) and `CodeFileTemplater` (render) in place; do not introduce the aspirational `IInsightProvider`/`SpecScribe.Core` package split. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **Pure-SVG / no-JS.** Reuse the deep-analytics primitives (bars/lists); the one small existing tooltip/copy script is not extended here. [[charting-is-pure-svg-no-js]]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **No new git library** — `GitMetrics.RunGit` (3s timeout, UTF-8 stdout, never-throw) is the one git seam; extend its existing `TryComputeDeep` call. [Source: [GitMetrics.cs:308-344](../../src/SpecScribe/GitMetrics.cs)]
- **Existing infra to reuse (do not reinvent):**
  - [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) — `TryComputeDeep`, `ParseNumstatLog`, `DeepGitPulse`, `CouplingFileSetCap`, `RunGit`; the pure-parser / never-throw / bounded discipline.
  - [ProgressModel.cs](../../src/SpecScribe/ProgressModel.cs) / [ProgressCalculator.cs](../../src/SpecScribe/ProgressCalculator.cs) — `DeepGit` is already carried and threaded; nothing new to add for the data path.
  - [DeepAnalyticsTemplater.cs](../../src/SpecScribe/DeepAnalyticsTemplater.cs) + [Charts.cs](../../src/SpecScribe/Charts.cs) — `HotspotBars`, `CouplingTable`, `Plural`, `D`/`DReadable`; the neutral-token, no-JS render idiom to mirror for the file section.
  - [CodeFileTemplater.cs](../../src/SpecScribe/CodeFileTemplater.cs) *(Story 7.1 — add the optional `FileInsight?` param here)*.
  - [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs) — `GenerateCodePagesInternal` (7.1; pass the per-file insight in), `_codePages` (coupled-file link resolution), the `_commitPages` seam (7.3; commit-history link guard), the deep-git gate.
  - [PathUtil.cs](../../src/SpecScribe/PathUtil.cs) — `Html`/`RelativePrefix`/`NormalizeSlashes`.

---

## File Structure Requirements

**New files:**

- `tests/SpecScribe.Tests/GitMetricsFileInsightsTests.cs` — pure-parser unit tests for the per-file map (feed synthetic `--numstat` log text with the new header format; assert change counts, contributor tallies, coupled-file lists, history rows, bounding, malformed-skip, empty→empty). Mirror `GitMetricsTests` feed-input-assert-structure style. [Source: [GitMetrics.cs:234-306](../../src/SpecScribe/GitMetrics.cs)]

**Modified files (read fully before editing):**

- `src/SpecScribe/GitMetrics.cs` — (1) extend `TryComputeDeep`'s pretty-format to carry `%an`/`%ad`(/`%s`) on the `%x01` header line + `--date=format:%Y-%m-%d`; (2) add the `FileInsight`/`CommitTouch` records; (3) add `FileInsights` to `DeepGitPulse`; (4) build the per-file map in a pure helper (sibling `ParseFileInsights`, or extend `ParseNumstatLog`), bounded and never-throw. **Preserve** the existing `Hotspots`/`Coupling` output and `ParseNumstatLog`'s current contract/tests exactly. [Source: [GitMetrics.cs:34-36,197-306](../../src/SpecScribe/GitMetrics.cs)]
- `src/SpecScribe/CodeFileTemplater.cs` *(Story 7.1)* — add optional `FileInsight? insight = null` to `RenderPage`; when non-null, render the `code-insights` section (contributors / change frequency / coupled files / history) under the code block, with guarded links; when null, render nothing extra. **Preserve** 7.1's shell, anchors, escaping, and baseline output. [Source: [7-1 story:143,238-241](7-1-in-portal-code-file-browsing.md)]
- `src/SpecScribe/SiteGenerator.cs` — in `GenerateCodePagesInternal` (7.1), pass `_progress?.DeepGit?.FileInsights.GetValueOrDefault(path)` per file into `RenderPage`; pass `_codePages` (coupled-file link resolution) and the `_commitPages` set (commit-link guard; add the nullable empty-default field if 7.3 hasn't). **Preserve** the code-pages phase, wipe/recreate `code/`, per-item try/catch, and the deep-git gate. [Source: [SiteGenerator.cs:41-177](../../src/SpecScribe/SiteGenerator.cs); [7-1 story:149,244](7-1-in-portal-code-file-browsing.md)]
- `src/SpecScribe/assets/specscribe.css` — add `.code-insights` (+ sub-part) styles using neutral tokens (not `--status-*`); wide content (history/coupling tables) scrolls inside its own container, never the body. **Edit only `src/SpecScribe/assets/specscribe.css`** — any `docs/live` copy is vestigial/gitignored ([[generate-output-dir-is-specscribeoutput]]). If `StylesheetTests.cs` asserts class presence, add companion assertions. [Source: [7-1 story:151,156](7-1-in-portal-code-file-browsing.md)]

**Output layout:**

- No new output paths. The advanced-coverage section renders *inside* existing `SpecScribeOutput/code/<repo-relative-path>.html` pages (7.1). Coupled-file links resolve to sibling `code/…html` pages; commit-history links to `commit/{hash}.html` (7.5), all relative via `PathUtil.RelativePrefix`.

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Pure-parser tests feed synthetic log text (no repo); templater tests call `RenderPage` directly; generation-level tests use the temp-`_bmad-output`-tree + `AssertNoErrors(gen.GenerateAll())` pattern. [Source: [SiteGeneratorTraceabilityTests.cs:106-154](../../tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs), [GitMetricsTests](../../tests/SpecScribe.Tests) (pure-parser shape)]

**`GitMetricsFileInsightsTests` (pure, no IO):**
- Given synthetic numstat log text in the **new** header format (`\x01<hash>\t<author>\t<date>[\t<subject>]` + `added\tdeleted\tpath` lines), the per-file map has: correct **change count** per path; **contributors** tallied per file (author → commits-touching, file-scoped); **coupled files** = the other file of each pair involving this path, desc, respecting `CouplingFileSetCap`; **history** rows (hash/date/author/subject), newest-first, capped.
- **Attribution not ranking:** contributors are per-file only; assert no cross-file aggregation leaks in.
- **Malformed/partial:** a commit with a header missing author/date is skipped or degraded (never throws); a file touched by a binary-only (`-\t-\tpath`) line still counts; empty log → empty map.
- **Bounding:** contributor/coupled/history lists are capped to their documented limits.
- **Back-compat:** the existing `Hotspots`/`Coupling` assertions in `GitMetricsTests` still pass with the new header format (add a header-format case if needed).

**`CodeFileTemplater` tests (unit, no IO) — extend 7.1's `CodeFileTemplaterTests`:**
- **Null insight → baseline page:** `RenderPage(..., insight: null)` produces no `code-insights` section (byte-compatible with 7.1's output for the same inputs).
- **Populated insight → section renders:** contributors (with "N commits" attribution wording, no ranking language), change-frequency line, coupled files, and history rows all appear.
- **Guarded links:** a coupled file *in* `_codePages` → `<a href=…code/…html>`; *not* in the map → plain text. A history hash *in* the commit-page set → `<a href=…commit/{hash}.html>`; *not* → plain `<code>`. No dead links either way.
- **Escaping:** author names / commit subjects / paths with `<`/`&`/`"` are escaped.
- **Empty sub-parts omitted:** an insight with contributors but no coupling renders the contributors part and omits the coupling heading (no empty heading), no crash.

**Generation-level tests (extend `SiteGeneratorCodePagesTests` from 7.1):**
- **Opt-in on:** a temp git repo + `DeepGitAnalytics = true` → a referenced code file's `code/…html` page contains the advanced-coverage section (positive control: a known author/commit/coupled path appears). Dashboard/other pages unaffected.
- **Opt-out off (perf/baseline guarantee, AC #1):** same repo with `DeepGitAnalytics = false` → code pages render **no** advanced-coverage section and are identical to the baseline; `TryComputeDeep` is not invoked.
- **Graceful degradation (AC #2):** non-git dir (or git failure) with `DeepGitAnalytics = true` → `DeepGit` null → no sections, no `Error` outcome, baseline code pages still render.
- **External mode:** `CodeSourceBaseUrl` set → no `code/` pages at all (7.1 gate) → no sections, no error.
- **Determinism:** two runs over the same repo produce identical `code/…html` output (including the insights section).

**Run:** `dotnet test` from repo root. Then two real passes against this repo (default `SpecScribeOutput/`; **do not** pass `--output docs/live` — [[generate-output-dir-is-specscribeoutput]]):
1. **Baseline:** `dotnet run --project src/SpecScribe` (no `--deep-git`) → open a `code/…html` page, confirm **no** advanced-coverage section and the page matches 7.1's baseline.
2. **Deep:** `dotnet run --project src/SpecScribe --deep-git` → open the same `code/…html` page, confirm the advanced-coverage section shows contributors (attribution wording), this file's change frequency, coupled files (linking to other `code/…html` pages where they exist), and a change-history list (hashes plain `<code>` until 7.5 lands, then links). Confirm no JS, invariant dates, escaped content, and that a non-git run degrades cleanly.

---

## Previous Story Intelligence

- **Story 7.3 (Activity Timeline & Date Pages) — immediate predecessor in Epic 7.** Established the exact guarded-link seams 7.4 reuses: the nullable `_commitPages` set (empty default, populated by 7.5) for guarding per-commit hash links, and the "one source of truth, guard every outgoing link on target existence, degrade to plain `<code>`/text, never a dead link" discipline. 7.3 also re-confirmed the boundary you must respect: **per-commit file data is the deep-git numstat path, not a second git call.** [Source: [7-3 story:58-62,88,100](7-3-activity-timeline-and-date-pages.md)]
- **Story 7.1 (In-Portal Code Browsing) — your render target and hard prerequisite.** It creates `CodeFileTemplater`, the `_codePages` map, the `code/<path>.html` output layout, the `#L{n}` anchor convention, and the `CodeSourceBaseUrl` external-mode gate. 7.4 attaches to all of these; read 7.1 before touching the render half. [Source: [7-1 story:31-59,143,149](7-1-in-portal-code-file-browsing.md)]
- **Story 3.2 (Deep Git Analytics) — the data foundation you extend.** It landed the single `TryComputeDeep`/`ParseNumstatLog`/`DeepGitPulse` path, the `--deep-git` gate (the perf guarantee), the `CouplingFileSetCap` bulk-commit guard, and the `DeepAnalyticsTemplater` render idiom. Extend in place; the record explicitly designates `--numstat` as the shared foundation for 7.4/7.5. [Source: [GitMetrics.cs:197-306](../../src/SpecScribe/GitMetrics.cs); [[deep-git-single-numstat-path]]]
- **Story 7.5 (Per-Commit Detail Pages) — the mirror-image seam on the same fetch.** 7.5 also extends this numstat fetch (it needs `%b`). Coordinate the pretty-format so both stories share one fetch; whoever lands first extends it. 7.5's `commit/{hash}.html` pages are 7.4's change-history link targets (guarded). [Source: [7-5 story:20,28,33,40](7-5-per-commit-detail-pages.md)]
- **Recurring lessons to apply:** escaping and stale-output are this renderer's two most common regressions (covered above); invariant/culture-safe formatting for every derived date/count; extend the monolith in place (no package split); grep in-flight/recent story files for stale repeated commands before closing (the `--output docs/live` foot-gun burned three Epic 2 stories). [Source: [sprint-status.yaml:126-129](sprint-status.yaml); [7-2 story:184](7-2-source-citation-and-comment-linking-to-code-pages.md); project memory]

## Git Intelligence Summary

Recent history is Epic-3 git-insights and Epic-7 planning churn (`Tweaks`, `Adjustments and planning`, `Status`, `3.2 / 3.3`, `Deep commit analysis`). The deep-git path (`TryComputeDeep`/`ParseNumstatLog`/`DeepGitPulse`, `DeepAnalyticsTemplater`) is on `main` (Story 3.2, in review). **7.1's code pages and 7.5's per-commit pages are not merged**, which is why 7.4 is sequenced after 7.1 and guards its commit links. No per-file-insight code exists yet.

> **Worktree note:** if you run this in a worktree, edit files at the worktree path — do **not** re-root relative paths back at `C:\Dev\SpecScribe`. `main` has a background auto-committer. [[worktree-edits-must-target-worktree-path]]

## Latest Technical Information

No external libraries or APIs are introduced — nothing to version-check. Platform notes:
- **`git log --numstat` with a custom header format:** the `%x01` byte sentinel is what makes the parse unambiguous; tab (`%x09`) separates single-line fields on the header. `--date=format:%Y-%m-%d` must contain no spaces (the single argument string is whitespace-tokenized by git — same trap `TryCompute` documents at [GitMetrics.cs:64-66](../../src/SpecScribe/GitMetrics.cs)). Author names and subjects are free text → always `PathUtil.Html`-escape.
- **`--numstat` binary rows** show `-\t-\tpath`; the path is still taken (the file changed), the added/deleted counts are ignored — consistent with the existing parser. [Source: [GitMetrics.cs:224-232](../../src/SpecScribe/GitMetrics.cs)]
- **Why not `git blame`:** per-line blame is a per-file, unbounded git process with no shared-fetch story — it would break the single-deep-git-path invariant and the NFR-1 perf guarantee. The bounded "change history from the shared numstat fetch" is the sanctioned reading of "blame-style." (Documented in the scope note above.)

## Project Context Reference

- FR19 (Epic 7 advanced code-and-git coverage on code pages): [Source: [epics.md:125](../../_bmad-output/planning-artifacts/epics.md)]
- Epic 7 goal ("advanced code-and-git coverage as an opt-in depth") + Story 7.4 ACs: [Source: [epics.md:837-841,903-921](../../_bmad-output/planning-artifacts/epics.md)]
- Deep-git single-path foundation to extend: [Source: [GitMetrics.cs:197-306](../../src/SpecScribe/GitMetrics.cs); [[deep-git-single-numstat-path]]]
- Deep-git wiring (compute → ProgressModel.DeepGit → generation gate): [Source: [SiteGenerator.cs:139-154,438-443](../../src/SpecScribe/SiteGenerator.cs), [ProgressCalculator.cs:8,68](../../src/SpecScribe/ProgressCalculator.cs), [ProgressModel.cs:37](../../src/SpecScribe/ProgressModel.cs)]
- Code-page render target + path map + external-mode gate: [Source: [7-1 story](7-1-in-portal-code-file-browsing.md); [[epic-7-code-link-strategy]]]
- Guarded-link + `_commitPages` seam precedent: [Source: [7-3 story:58-62,88](7-3-activity-timeline-and-date-pages.md)]
- Deep-analytics render idiom to mirror: [Source: [DeepAnalyticsTemplater.cs](../../src/SpecScribe/DeepAnalyticsTemplater.cs), [Charts.cs:793,818,850](../../src/SpecScribe/Charts.cs)]
- Architecture invariants (AD-4, local/read-only, graceful degradation, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)]
- Project memory: [[deep-git-single-numstat-path]], [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[epic-7-code-link-strategy]], [[generate-output-dir-is-specscribeoutput]], [[worktree-edits-must-target-worktree-path]].

---

## Tasks / Subtasks

- [ ] **Task 1 — Extend the shared deep-git fetch + parse for per-file signals (AC: #1, #2)**
  - [ ] Change `TryComputeDeep`'s pretty-format to carry `%an`/`%ad`(/`%s`) on the `%x01` header line + `--date=format:%Y-%m-%d`; keep the `-n 300` bound and the single `--numstat` call. Coordinate the format with Story 7.5 (which adds `%b`). [Source: [GitMetrics.cs:213](../../src/SpecScribe/GitMetrics.cs)]
  - [ ] Add `FileInsight` + `CommitTouch` records and a `FileInsights` map (empty, never null) to `DeepGitPulse`.
  - [ ] Build the map in a pure helper (sibling `ParseFileInsights`, preferred — leaves `ParseNumstatLog` untouched): per commit, tally each touched file's change count, contributors (author → per-file commit count), coupled files (from the existing pair data, respecting `CouplingFileSetCap`), and a bounded newest-first history. Never-throw, skip malformed, document all caps.
  - [ ] Keep existing `Hotspots`/`Coupling` output + tests intact.
- [ ] **Task 2 — Render the advanced-coverage section on code pages (AC: #1)** *(after Story 7.1)*
  - [ ] Add optional `FileInsight? insight = null` to `CodeFileTemplater.RenderPage`; render a neutral-token `code-insights` section (contributors as attribution, change frequency, coupled files, change history) only when non-null; render nothing extra when null (baseline-identical).
  - [ ] Guard links: coupled file → `code/<path>.html` only when in `_codePages`; history hash → `commit/{hash}.html` only when in the commit-page set; plain otherwise.
- [ ] **Task 3 — Wire the insight into the code-pages phase (AC: #1, #2)** *(after Story 7.1)*
  - [ ] In `GenerateCodePagesInternal`, pass `_progress?.DeepGit?.FileInsights.GetValueOrDefault(path)` per file into `RenderPage`, plus `_codePages` and the `_commitPages` set (add the nullable empty-default field if 7.3 hasn't). Additive only; preserve the phase, wipe/recreate, try/catch, and the deep-git gate.
- [ ] **Task 4 — Styling (AC: #1)**
  - [ ] Add `.code-insights` (+ sub-part) styles in `src/SpecScribe/assets/specscribe.css` using neutral tokens (not `--status-*`); tables scroll inside their own container. Update `StylesheetTests` if it asserts class presence. **No JavaScript.**
- [ ] **Task 5 — Tests (AC: #1, #2)**
  - [ ] `GitMetricsFileInsightsTests`: per-file change count / contributors / coupling / history, attribution-not-ranking, bounding, malformed/partial/empty, `Hotspots`/`Coupling` back-compat.
  - [ ] `CodeFileTemplater` tests: null→baseline, populated→section, guarded links (present/absent both), escaping, empty sub-parts omitted.
  - [ ] Generation-level: opt-in on → section present; opt-out off → no section + baseline identical + `TryComputeDeep` not called; graceful degradation (no git → no section, no error); external mode → no pages/sections; determinism.
- [ ] **Task 6 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green. Baseline generate (no `--deep-git`) → code page shows no section, matches 7.1 baseline. Deep generate (`--deep-git`) → section shows contributors/frequency/coupled-files/history with guarded links, no JS, invariant dates, escaped content. Non-git run degrades cleanly.

## Dev Notes

- **Sequence after 7.1; land the data half first if you want.** The `GitMetrics` extension (Task 1) is fully independent and testable without code pages. The render half (Tasks 2–3) needs 7.1's `CodeFileTemplater`/`_codePages` on disk — don't start it until 7.1 lands.
- **One fetch, one gate.** The per-file map rides the existing `--deep-git`-gated `DeepGitPulse`; there is no new git call and no new threading. When the flag is off, none of this runs — that's the perf guarantee. [[deep-git-single-numstat-path]]
- **"Blame-style" = bounded per-file history, not per-line blame.** Do not add a `git blame` call. The change-history list from the shared numstat fetch is the honest, bounded signal. Defer literal per-line blame.
- **Attribution, never ranking.** Contributors are file-scoped ("who changed this file"). No leaderboard, no cross-file aggregation. [Source: [GitMetrics.cs:29-33](../../src/SpecScribe/GitMetrics.cs)]
- **Guard, don't probe.** Coupled-file and commit links resolve only when their targets exist (cached sets), never filesystem probes. Correct today, richer as 7.5 lands.
- **No JS. Neutral tokens. Escape everything. Invariant dates.** [[charting-is-pure-svg-no-js]] [[specscribe-status-token-system]]

### Project Structure Notes

- Data extension lives in `GitMetrics` (new records + a pure `ParseFileInsights` helper + a `DeepGitPulse` property); render extension is an optional param on 7.1's `CodeFileTemplater` + one additive lookup in `GenerateCodePagesInternal`. No new files beyond the test, no new output paths, no package restructure (deferred seed, Epics 4/6).
- Coordinated fetch-format change is shared with Story 7.5 on the single `TryComputeDeep` call.

### References

- [Source: [epics.md:903-921](../../_bmad-output/planning-artifacts/epics.md)] — Story 7.4 user story + both ACs.
- [Source: [epics.md:837-841,125](../../_bmad-output/planning-artifacts/epics.md)] — Epic 7 goal + FR19 coverage.
- [Source: [src/SpecScribe/GitMetrics.cs:34-36,197-306](../../src/SpecScribe/GitMetrics.cs)] — `DeepGitPulse`, `TryComputeDeep`, `ParseNumstatLog`, `CouplingFileSetCap` to extend.
- [Source: [src/SpecScribe/DeepAnalyticsTemplater.cs](../../src/SpecScribe/DeepAnalyticsTemplater.cs), [src/SpecScribe/Charts.cs:793,818,850,935,939,1031](../../src/SpecScribe/Charts.cs)] — the deep render idiom + primitives to mirror.
- [Source: [src/SpecScribe/ProgressModel.cs:37](../../src/SpecScribe/ProgressModel.cs), [src/SpecScribe/ProgressCalculator.cs:8,68](../../src/SpecScribe/ProgressCalculator.cs), [src/SpecScribe/SiteGenerator.cs:139-154,438-443](../../src/SpecScribe/SiteGenerator.cs)] — `DeepGit` threading + generation gate.
- [Source: [7-1-in-portal-code-file-browsing.md:31-59,143,149,239-244](7-1-in-portal-code-file-browsing.md)] — code-page templater, `_codePages`, output layout, external-mode gate (render target).
- [Source: [7-3-activity-timeline-and-date-pages.md:58-62,88](7-3-activity-timeline-and-date-pages.md)] — guarded-link discipline + `_commitPages` seam.
- [Source: [7-5-per-commit-detail-pages.md:20,28,33,40](7-5-per-commit-detail-pages.md)] — shared numstat/body fetch coordination + commit-page link target.
- [Source: [_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — invariants (AD-4, local/read-only, graceful degradation, seed-not-invariant).
- [Source: [tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs:106-154](../../tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs)] — generation-level temp-tree + `AssertNoErrors` pattern.
- [[deep-git-single-numstat-path]] / [[charting-is-pure-svg-no-js]] / [[specscribe-status-token-system]] / [[epic-7-code-link-strategy]] / [[generate-output-dir-is-specscribeoutput]] / [[worktree-edits-must-target-worktree-path]] — project memory.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
