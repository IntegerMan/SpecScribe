---
baseline_commit: 279d27e230ff391ce9f73bb1e438ca24775ceaa4
---

# Story 7.5: Per-Commit Detail Pages

Status: review

<!-- Context-engineered 2026-07-09 (create-story). Supersedes the 2026-07-08 owner draft, whose Task 1 ("extend the shared fetch to add %b + --numstat, produce a CommitDetail record") is now STALE: Story 3.8 already landed the enriched fetch AND the per-commit DeepCommit record. This story now exposes that existing data and renders/wires the pages. Validation optional: run validate-create-story before dev-story. -->

## Story

As a contributor,
I want a page for each significant commit,
so that I can read what changed and why without leaving the portal.

## Acceptance Criteria

1. **Given** git history is available and detail pages are enabled **When** I open a commit's page **Then** it shows the commit subject, full commit message body, author and date, and the files changed with per-file line churn **And** recognized references in the message (for example "Story N.M" or "FR-9") link to their artifacts. [Source: epics.md#Story 7.5 (lines 946-958); PRD FR-10]
2. **Given** a commit page lists changed files and its author **When** I follow those links **Then** file entries lead to the corresponding file page and the author is shown as attribution, never as a productivity ranking **And** page generation is bounded and degrades non-fatally when history is unavailable or partial. [Source: epics.md#Story 7.5 (lines 960-964); PRD FR-10, non-goal amended 2026-07-08]

---

## Developer Context

**Read this first — the data you need already exists; this is mostly a render + wiring story.** Story 3.8 (Git Insights hub, on `main`) already extended the single deep-git fetch to carry the full commit **body** (`%b`) and per-file **numstat**, and already exposes a public pure parser, `GitMetrics.ParseNumstatRecords`, that returns one **`DeepCommit`** record per commit — carrying exactly what AC #1 asks for: `Hash`, `Author`, `Timestamp`, `Subject`, `Body`, and `Files` (each a `DeepFileChange` with `Added`/`Deleted`, null for binary). **Do not re-extend the fetch and do not invent a new `CommitDetail` record** — the 2026-07-08 draft's Task 1 predates 3.8 and is now wrong. Your net-new work is: (a) **expose** the already-parsed `DeepCommit` list on `DeepGitPulse` (one property, populated from a local `ParseNumstatLog` already computes), (b) a **`CommitDetailTemplater`** that mirrors `CommitDayTemplater` to render `commit/{shortHash}.html`, (c) a **generation phase** mirroring `GenerateCommitDaysInternal` that emits those pages and records a `_commitPages` set, and (d) **wiring the two guarded `commitHref` seams that already exist and today render plain** — the per-day pages' hashes and the Git Insights hub's "latest {hash}" link.

### Three things that are already done for you (verify, then reuse — do not rebuild)

- **The fetch already carries body + numstat.** `GitMetrics.TryComputeDeep` runs `log --numstat --date=format:%Y-%m-%dT%H:%M --pretty=format:%x01%H%x1f%an%x1f%ad%x1f%s%x1f%b%x1f -n 300` — hash, author, date, subject, **body**, then the numstat rows, all sentinel-delimited. This is THE one shared fetch (3.2/3.8/7.4/7.5). **There is nothing to add to the git command.** [Source: [GitMetrics.cs:271-272](../../src/SpecScribe/GitMetrics.cs)]
- **The per-commit record already exists and is public.** `GitMetrics.ParseNumstatRecords(logText)` → `IReadOnlyList<DeepCommit>`, where `DeepCommit(Hash, Author, Timestamp, Subject, Body, Files)` and `DeepFileChange(Path, Added, Deleted)` are exactly the shape a commit page needs. `ParseNumstatLog` *already calls it* (its `commits` local) — it just doesn't surface it. [Source: [GitMetrics.cs:46-61,294-302,371-436](../../src/SpecScribe/GitMetrics.cs)]
- **Both link seams already exist, guarded, rendering plain.** `CommitDayTemplater` renders each hash as plain `<code class="commit-hash">` ([CommitDayTemplater.cs:50](../../src/SpecScribe/CommitDayTemplater.cs)); `GitInsightsTemplater.RenderPage` already takes an optional `Func<string,string?>? commitHref` and its `GuardedLink` renders `file.LatestHash` as a link *when the resolver returns a target, plain otherwise* — but `SiteGenerator` calls it without the resolver today, so it's plain. 7.5 supplies the resolver (backed by `_commitPages`) to both. [Source: [GitInsightsTemplater.cs:23-28,161-164,234-239](../../src/SpecScribe/GitInsightsTemplater.cs), [SiteGenerator.cs:452](../../src/SpecScribe/SiteGenerator.cs)]

### The core design in one paragraph

Add an `IReadOnlyList<DeepCommit> Commits` property to `DeepGitPulse` (empty list, never null) and populate it in `ParseNumstatLog` from the `commits` records it already parses — zero new git work, no threading changes (it rides the existing `--deep-git`-gated `ProgressModel.DeepGit`). Add a `CommitDetailTemplater.RenderPage(DeepCommit commit, SiteNav nav, Func<string,string?>? fileHref = null)` that mirrors `CommitDayTemplater` exactly (synthesized shell, skip-link, single `<main id="main-content">`, breadcrumb, `doc-header`/`doc-body`): subject as `<h1>`, an author + date meta pill (attribution framing — "by {author} on {date}", never a ranking), the full body rendered as readable escaped prose preserving paragraph breaks, and a files-changed table (path, +added, −deleted) with each path a **guarded** link to its `code/…html` page (7.1, unmerged → plain `<code>` for now). Emit the pages from a new `GenerateCommitDetailsInternal` phase that mirrors `GenerateCommitDaysInternal`: gate on `_progress?.DeepGit?.Commits`, wipe+recreate `commit/`, `File.WriteAllText` each `commit/{shortHash}.html`, run `ApplyReferenceLinks` so "Story N.M"/"FR-9" mentions in the subject **and** body become links (AC #1), record a `CommitDetailEntry`, and build a `_commitPages` lookup (hash → output path). Run this phase **before** the commit-days phase and git-insights hub so both can consume `_commitPages` via a `commitHref` resolver. Bounding is automatic and contractual: the fetch is `-n 300` and only runs under `--deep-git`, so ≤300 pages are ever generated and none when the flag is off (AC #2). Everything degrades to *no pages, plain hashes, baseline site intact* when git is off/absent/partial.

### The one real integration hazard: `%h` (short) vs `%H` (full) hash reconciliation (read carefully)

The two link sources carry **different hash forms**, and your `_commitPages` lookup must bridge them or the links silently won't resolve:
- **The Git Insights hub** links `FileChangeStat.LatestHash`, which is the **full `%H`** hash (`BuildInsights` sets it from `DeepCommit.Hash`). [Source: [GitMetrics.cs:487-490](../../src/SpecScribe/GitMetrics.cs)]
- **The per-day commit pages** carry `CommitInfo.ShortHash`, which is the **abbreviated `%h`** hash from the *baseline* `TryCompute`/`ParseLog` fetch (a different git call). [Source: [GitMetrics.cs:9,121](../../src/SpecScribe/GitMetrics.cs)]
- **Your commit pages** are keyed off `DeepCommit.Hash` (**full `%H`**), and the draft's filename convention is `commit/{shortHash}.html`.

**Recommended reconciliation:** build `_commitPages` keyed on the **full** hash → output path, and expose the resolver as `commitHref(hash)` that (1) tries an exact match, then (2) falls back to matching any registered full hash that **starts with** the passed hash (handles the day page's `%h` prefix; ≤300 entries so the linear scan is trivial). Derive the page's short hash for the filename/display from `DeepCommit.Hash[..7]` (git's default abbreviation floor is 7). Flag in Dev Notes that a 7-char filename collision across two full hashes in the same 300-commit window is astronomically unlikely but, if you want belt-and-suspenders, dedupe by lengthening the abbreviation on collision. **Do not** assume the day page's `%h` equals `Hash[..7]` — git can auto-widen `%h` past 7 on collision, which is exactly why a prefix match (not equality) is the safe resolver.

---

## Dependencies / Sequencing

- **Data foundation is already merged (Story 3.2 + 3.8).** The single `TryComputeDeep`/`ParseNumstatLog`/`ParseNumstatRecords` path is THE git fetch. Extend it *only* by surfacing the existing `DeepCommit` list on `DeepGitPulse`. Do **not** add a second `git log`. [[deep-git-single-numstat-path]] [Source: [GitMetrics.cs:258-350](../../src/SpecScribe/GitMetrics.cs)]
- **Gated + bounded via `--deep-git` (AC #2).** Body and numstat exist only on the deep fetch, which runs only when `ForgeOptions.DeepGitAnalytics` is set and is capped at `-n 300`. That gate+cap *is* the bounding and the perf guarantee — per-commit pages inherit it for free. Never generate per-commit pages on the baseline path. [Source: [GitMetrics.cs:272](../../src/SpecScribe/GitMetrics.cs), [ForgeOptions.cs:33](../../src/SpecScribe/ForgeOptions.cs), [SiteGenerator.cs:134](../../src/SpecScribe/SiteGenerator.cs)]
- **Reference links (AC #1) reuse `ApplyReferenceLinks`.** Running the rendered page HTML through `SiteGenerator.ApplyReferenceLinks` (as every other page write does, including commit-day pages) turns "Story N.M"/"FR-9" mentions in the subject and body into links via `RequirementLinkifier` + `StoryEpicLinkifier`, guarded on target existence. Escape first (in the templater), linkify after (in the generator) — same order the day pages use. [Source: [SiteGenerator.cs:422,875-887](../../src/SpecScribe/SiteGenerator.cs)]
- **File links (AC #2) resolve to Story 7.1's per-file `code/…html` pages — which are NOT merged.** There is no `_codePages` map on `main` yet (7.1 is `ready-for-dev`). Provide a **guarded `fileHref` resolver** param on the templater exactly like the hub's `fileHref`; wire it from `_codePages` when 7.1 has landed, else pass null → file rows render as plain `<code>` paths. This is correct graceful degradation and forward-compatible; do **not** block 7.5 on 7.1. [Source: [7-1 story](7-1-in-portal-code-file-browsing.md); [GitInsightsTemplater.cs:114-117](../../src/SpecScribe/GitInsightsTemplater.cs)]
- **`_commitPages` is NEW — 7.3 did not merge it.** The 7.4 draft references a `_commitPages` seam "from 7.3", but 7.3 is `review`, not on `main` — `SiteGenerator` today has only `_commitDays` ([SiteGenerator.cs:31](../../src/SpecScribe/SiteGenerator.cs)). **7.5 owns introducing `_commitPages`.** Later stories (7.3/7.4) consume it; design it as the shared seam (a `Dictionary<string,string>` or `HashSet<string>`+path map on the generator, empty default).
- **Relates to 7.3 (date pages) and 3.8 (hub) as link *consumers*.** 7.5 makes the hub's already-present guarded hash link light up; when 7.3 lands its date pages, they link to `commit/{hash}.html` through the same seam.

### Scope boundary (read carefully)

- **IN scope:**
  - Expose `DeepCommit` records on `DeepGitPulse` (one property, populated from the existing parse).
  - `CommitDetailTemplater` → `commit/{shortHash}.html`: subject `<h1>`, author+date attribution pill, full body as escaped prose (paragraphs preserved), files-changed table (path, +added, −deleted), guarded file links.
  - `GenerateCommitDetailsInternal` phase: gated/bounded, wipe+recreate `commit/`, `ApplyReferenceLinks`, `CommitDetailEntry` + `_commitPages`.
  - Wire the guarded `commitHref` resolver into **`CommitDayTemplater`** (new optional param) and **`GitInsightsTemplater.RenderPage`** (existing param, currently unwired).
  - `.commit-detail` styling (neutral tokens); graceful degradation everywhere.
- **OUT of scope:**
  - **Re-extending the git fetch or adding a second `git log`.** The fetch already carries everything. [[deep-git-single-numstat-path]]
  - **A `CommitDetail`/`CommitInfo`-style new record for the data.** Reuse `DeepCommit`/`DeepFileChange`.
  - **Author productivity ranking / leaderboards.** Author is shown as single-commit attribution ("by X on Y"), never aggregated or ranked. Standing PRD non-goal. [Source: [GitMetrics.cs:63-66](../../src/SpecScribe/GitMetrics.cs)]
  - **Generating a page per commit for *all* history.** Bounded to the `-n 300` deep window; gated on `--deep-git`. Never unbounded. [Source: [Charts.cs:1073-1085](../../src/SpecScribe/Charts.cs) bounding philosophy]
  - **Building 7.1's code pages or the `_codePages` map.** File links stay guarded/plain until 7.1 lands.
  - **A new top-nav entry.** Per-commit pages are linked *into* (from day pages / hub / later date pages), like the `commits/` day pages — nothing added to the nav bar.
  - **Any JavaScript.** Pure HTML/CSS. [[charting-is-pure-svg-no-js]]

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Surface the existing per-commit records — do not re-parse.** Add `IReadOnlyList<DeepCommit> Commits` to `DeepGitPulse` (empty list default, never null). Populate it in `ParseNumstatLog` from the `commits` local it already computes via `ParseNumstatRecords`. Consider capping/ordering to the newest-first git-log order the records already arrive in (records are newest-first). Preserve `Hotspots`/`Coupling`/`Insights` output and their existing tests **exactly**. [Source: [GitMetrics.cs:294-350](../../src/SpecScribe/GitMetrics.cs)]
- **Mirror `CommitDayTemplater` precisely for the shell.** Synthesized page (no markdown source): `PathUtil.RenderHeadOpen(...)` with a `<title>`/description, `nav.RenderNavBar(outputPath)`, `SiteNav.RenderBreadcrumb(...)` (`Home → Commit {shortHash}`), a single `<main id="main-content">` skip-link target, `<header class="doc-header">` (kicker + `<h1>` subject + meta pills), `<article class="doc-body">`, `PathUtil.RenderFooter(...)`. Output path `commit/{shortHash}.html`; compute `prefix = PathUtil.RelativePrefix(outputPath)` for the stylesheet/script/asset hrefs (one level up). [Source: [CommitDayTemplater.cs:12-77](../../src/SpecScribe/CommitDayTemplater.cs)]
- **Render the four AC-#1 signals:**
  - **Subject** → `<h1>` (escaped).
  - **Author + date** → a `meta-pills` pill framed as attribution, e.g. `by {author} · {DReadable(date)}`. Use `Charts.DReadable` for the date; guard when `DeepCommit.Timestamp` is null (show author only). Attribution, never a count/rank.
  - **Body** → readable escaped prose in `doc-body`, preserving paragraph breaks (split on blank lines → `<p>`, or `<br>` for single newlines — pick one and test it). Body is free text and an injection surface → **`PathUtil.Html` every segment before adding `<p>`/`<br>`**. Empty body → omit the prose block (no empty `<p>`).
  - **Files changed** → a table (scroll container) with columns Path / Lines added / Lines deleted; render `DeepFileChange.Added`/`Deleted` as `+N`/`−N`, and for binary rows (both null) show a "binary" marker or "—" rather than `+0`/`−0`. Path is a **guarded** `fileHref` link (`code/…html` when resolvable, else plain `<code>`).
- **Emit pages via a new phase mirroring `GenerateCommitDaysInternal`.** `GenerateCommitDetailsInternal(IReadOnlyList<DeepCommit> commits, SiteNav nav)`: build `commitDir = Path.Combine(OutputRoot, "commit")`, delete-if-exists + recreate (clean-slate like the day/adr phases), per-commit `Stopwatch`, `outputRelative = PathUtil.NormalizeSlashes($"commit/{shortHash}.html")`, `File.WriteAllText(..., ApplyReferenceLinks(html, outputRelative))`, record a `CommitDetailEntry(hash, outputRelative)`, add a `Generated`/`Error` event per page (per-item try/catch — one bad commit never fails the run), and populate `_commitPages`. [Source: [SiteGenerator.cs:394-435](../../src/SpecScribe/SiteGenerator.cs)]
- **Sequence the phase before the day-pages and hub phases.** So `_commitPages` is populated when `GenerateCommitDaysInternal` (currently [SiteGenerator.cs:124-129](../../src/SpecScribe/SiteGenerator.cs)) and `GenerateGitInsightsInternal` ([SiteGenerator.cs:162](../../src/SpecScribe/SiteGenerator.cs)) render their guarded hash links. `_progress.DeepGit` is already available by then (computed in the epics phase). Gate the new phase on `_progress?.DeepGit?.Commits is { Count: > 0 }`.
- **Wire the guarded `commitHref` resolver into both consumers:**
  - Add an optional `Func<string,string?>? commitHref = null` param to `CommitDayTemplater.RenderPage`; use it to wrap the `commit-hash` `<code>` in an `<a>` when it resolves, plain otherwise (mirror the hub's `GuardedLink`). Update `GenerateCommitDaysInternal` to pass the resolver.
  - Pass the resolver as the existing `commitHref` argument in the `GitInsightsTemplater.RenderPage(insights, _progress.Git, nav, commitHref: ...)` call at [SiteGenerator.cs:452](../../src/SpecScribe/SiteGenerator.cs). No templater change needed there — the seam already exists.
- **Resolve hashes by prefix, not equality** (the `%h` vs `%H` hazard above): the resolver matches full-hash keys, falling back to `StartsWith` for abbreviated inputs.
- **Attribution, not ranking.** One author shown per commit as "who made this change." No aggregation, no per-author totals on this page. [Source: [GitMetrics.cs:63-66](../../src/SpecScribe/GitMetrics.cs); PRD FR-10 non-goal]
- **Neutral tokens only.** The commit page and its files table use `--ink`/`--border`/`--parchment`-family chart tokens like the day/deep pages — **never** `--status-*` lifecycle tokens. [[specscribe-status-token-system]]
- **Invariant formatting + escaping everywhere.** `Charts.D` (ISO, for filenames/anchors), `Charts.DReadable` (display dates), `Charts.Plural` (file counts), `PathUtil.Html` for **every** author/subject/body-segment/path/hash — all git free text. [Source: [Charts.cs](../../src/SpecScribe/Charts.cs), [PathUtil.cs](../../src/SpecScribe/PathUtil.cs)]
- **Degrade non-fatally at every branch (AC #2, NFR-2, AD-4).** Flag off / git absent → `DeepGit` null → no `commit/` dir, no pages, day pages render with plain `<code>` hashes, baseline site intact. Partial commit (no body, null timestamp, binary-only files) → render what exists, never an empty heading, never a throw. One malformed commit → skip via per-item try/catch, keep generating.

### DON'T

- **DON'T add or re-extend a git fetch.** The fetch already carries `%H %an %ad %s %b` + numstat. There is nothing to add to the git command. [[deep-git-single-numstat-path]]
- **DON'T invent a `CommitDetail` record.** Reuse `DeepCommit`/`DeepFileChange` (the 2026-07-08 draft's `CommitDetail(hash, author, date, subject, body, files-with-churn)` is literally `DeepCommit`).
- **DON'T generate per-commit pages on the baseline path or unbounded.** They exist only under `--deep-git` and only for the `-n 300` window — that gate+cap is AC #2's bounding + perf guarantee.
- **DON'T rank or aggregate authors.** Single-commit attribution only. [Source: [GitMetrics.cs:63-66](../../src/SpecScribe/GitMetrics.cs)]
- **DON'T emit a dead link.** File paths link to `code/…html` only when `_codePages` has them (null resolver until 7.1 → plain); day/hub hashes link only when `_commitPages` has them. Plain `<code>` otherwise.
- **DON'T let raw commit body/subject HTML through.** Escape before wrapping in `<p>`/`<br>`; the body may contain `<`, `>`, `&`, markup-looking text.
- **DON'T add JavaScript.** Pure HTML/CSS. [[charting-is-pure-svg-no-js]]
- **DON'T co-opt `--status-*` tokens.** [[specscribe-status-token-system]]
- **DON'T force the Core/Adapters package split.** Extend the monolith in place (add to `GitMetrics`, add `CommitDetailTemplater`, extend `SiteGenerator`). [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **DON'T write back to source** (local-first, read-only). Output only under `OutputRoot`.
- **DON'T pass `--output docs/live`** in any manual-verify step — vestigial/gitignored; default is `SpecScribeOutput/`. [[generate-output-dir-is-specscribeoutput]]

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md); [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md)]:

- **AD-4 — optional insight providers are additive, non-blocking, never own baseline success.** Per-commit pages are opt-in (`--deep-git`) and additive: any absence/failure yields no pages and guarded plain links, and the baseline site still generates.
- **FR-10 / bounded performance.** Body + numstat ride the one shared bounded (`-n 300`) `--deep-git` fetch — no new git process; per-commit page count is capped by that window. Baseline generation is untouched when the flag is off (AC #2).
- **Graceful degradation is contractual (NFR-2, AC #2).** Absent/partial git → omitted pages + plain (never dead) links, never a thrown exception; day pages and the whole site still render.
- **Local-only, read-only.** Git + source reads only; output lands under `OutputRoot`; no source mutation.
- **Accessibility is part of the rendering contract (NFR-6, UX-DR16/17).** The page inherits the synthesized-shell a11y contract: skip-link → single `<main id="main-content">`, semantic `<header>`/`<article>`/`<table>` with a `<caption>`, links carrying meaningful visible text (paths, hashes, subjects — never "click here"), and no color-only signal. Mirror `CommitDayTemplater`. [Source: [CommitDayTemplater.cs:36-37](../../src/SpecScribe/CommitDayTemplater.cs)]
- **Seed, not invariant.** Extend `GitMetrics` (data) + `SiteGenerator` (wiring) in place and add one templater; no `IInsightProvider`/`SpecScribe.Core` package split. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **Pure-SVG / no-JS.** The one small existing tooltip/copy script is not extended here. [[charting-is-pure-svg-no-js]]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **No new git library** — the shared `GitMetrics.RunGit`/`TryComputeDeep` path (3s timeout, UTF-8, never-throw) already fetches everything. [Source: [GitMetrics.cs:258-281,555-591](../../src/SpecScribe/GitMetrics.cs)]
- **Existing infra to reuse (do not reinvent):**
  - [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs) — `DeepCommit`, `DeepFileChange`, `ParseNumstatRecords`, `ParseNumstatLog`, `DeepGitPulse` (add `Commits` here); pure-parser / never-throw discipline.
  - [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs) — the exact synthesized-page template to mirror for `CommitDetailTemplater`.
  - [CommitDayEntry.cs](../../src/SpecScribe/CommitDayEntry.cs) — the entry-record precedent for `CommitDetailEntry`.
  - [GitInsightsTemplater.cs](../../src/SpecScribe/GitInsightsTemplater.cs) — `GuardedLink` idiom + the already-present `commitHref` seam to wire.
  - [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs) — `GenerateCommitDaysInternal` (phase precedent), `_commitDays` (field precedent → add `_commitPages`), `ApplyReferenceLinks`, the deep-git gate, the phase orchestration in `GenerateAll`.
  - [ProgressModel.cs](../../src/SpecScribe/ProgressModel.cs) / [ProgressCalculator.cs](../../src/SpecScribe/ProgressCalculator.cs) — `DeepGit` is already carried/threaded; nothing new for the data path.
  - [Charts.cs](../../src/SpecScribe/Charts.cs) — `D`/`DReadable`/`Plural`, `LinkedCommitDays` (bounding philosophy).
  - [PathUtil.cs](../../src/SpecScribe/PathUtil.cs) — `Html`/`RelativePrefix`/`NormalizeSlashes`/`RenderHeadOpen`/`RenderFooter`.
  - [SiteNav.cs](../../src/SpecScribe/SiteNav.cs) — `RenderNavBar`/`RenderBreadcrumb`; no new nav constant needed (per-commit pages aren't a top-nav destination, like the `commits/` day pages).

---

## File Structure Requirements

**New files:**

- `src/SpecScribe/CommitDetailTemplater.cs` — the synthesized `commit/{shortHash}.html` page (subject, author+date attribution, body prose, files-changed table with churn + guarded file links). Mirror `CommitDayTemplater`.
- `src/SpecScribe/CommitDetailEntry.cs` — `public sealed record CommitDetailEntry(string Hash, string OutputRelativePath);` (mirror `CommitDayEntry`).
- `tests/SpecScribe.Tests/CommitDetailTemplaterTests.cs` — unit tests calling `RenderPage` directly (mirror `CommitDayTemplaterTests`).
- `tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs` — generation-level tests over a temp git repo (mirror `SiteGeneratorGitInsightsTests`).

**Modified files (read fully before editing):**

- `src/SpecScribe/GitMetrics.cs` — add `IReadOnlyList<DeepCommit> Commits` to `DeepGitPulse`; populate it in `ParseNumstatLog` from the existing `commits` records. **Preserve** `Hotspots`/`Coupling`/`Insights` and every existing test. [Source: [GitMetrics.cs:34-44,294-350](../../src/SpecScribe/GitMetrics.cs)]
- `src/SpecScribe/CommitDayTemplater.cs` — add optional `Func<string,string?>? commitHref = null`; wrap the `commit-hash` `<code>` in a guarded link. **Preserve** the shell, rows, prev/next nav, escaping. [Source: [CommitDayTemplater.cs:50](../../src/SpecScribe/CommitDayTemplater.cs)]
- `src/SpecScribe/SiteGenerator.cs` — add the `_commitPages` field + `_commitDetails` list; add `GenerateCommitDetailsInternal`; call it in `GenerateAll` **before** the commit-days and git-insights phases, gated on `_progress?.DeepGit?.Commits`; pass the `commitHref` resolver into `GenerateCommitDaysInternal` and the `GitInsightsTemplater.RenderPage` call. **Preserve** phase ordering semantics, per-item try/catch, wipe/recreate, and the deep-git gate. [Source: [SiteGenerator.cs:31,124-162,394-435,452](../../src/SpecScribe/SiteGenerator.cs)]
- `src/SpecScribe/assets/specscribe.css` — add `.commit-detail` (+ files table, body prose, meta pill) styles using **neutral tokens** (not `--status-*`); the files table scrolls inside its own `overflow-x` container, never the body. **Edit only `src/SpecScribe/assets/specscribe.css`** — any `docs/live` copy is vestigial/gitignored ([[generate-output-dir-is-specscribeoutput]]). `StylesheetTests` asserts specific class names for other features but doesn't require a `.commit-detail` assertion; add a companion assertion only if you want the coverage. [Source: [StylesheetTests.cs:44-118](../../tests/SpecScribe.Tests/StylesheetTests.cs)]

**Output layout:**

- New output dir `SpecScribeOutput/commit/{shortHash}.html` (singular `commit/`, distinct from the plural `commits/{date}.html` day pages). `prefix` is one level up; reference links, stylesheet, and script resolve via `PathUtil.RelativePrefix`. File links resolve to sibling `code/…html` (7.1) when present.

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Pure-parser tests feed synthetic log text (no repo); templater tests call `RenderPage` directly; generation-level tests use the temp-dir + real-git-repo fixture with `AssertNoErrors`. [Source: [CommitDayTemplaterTests.cs](../../tests/SpecScribe.Tests/CommitDayTemplaterTests.cs), [SiteGeneratorGitInsightsTests.cs:13-171](../../tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs)]

**`GitMetrics` — extend `GitMetricsTests` (pure, no IO):**
- `ParseNumstatLog(...).Commits` exposes the parsed `DeepCommit` records (hash/author/timestamp/subject/body/files) from synthetic sentinel-format log text, newest-first; empty log → empty `Commits` (never null). Binary-only (`-\t-\tpath`) rows keep the file with null churn. Existing `Hotspots`/`Coupling`/`Insights` assertions still pass (back-compat).

**`CommitDetailTemplaterTests` (unit, no IO) — mirror `CommitDayTemplaterTests`:**
- **Renders the four signals:** `<h1>` subject; author+date attribution pill; body prose with paragraph breaks preserved (multi-paragraph body → multiple `<p>`); files table with `+added`/`−deleted` per row.
- **Site a11y contract:** skip-link first, single `<main id="main-content">`, breadcrumb present (mirror the day-page assertions).
- **Escaping:** subject / body / author / path containing `<`/`>`/`&`/`"` are escaped; `Assert.DoesNotContain` the raw form.
- **Binary + partial:** a binary file row (null added/deleted) shows a marker, not `+0`/`−0`; a commit with an empty body omits the prose block (no empty `<p>`); a null timestamp shows author without a date, no crash.
- **Guarded file link:** a path with a resolvable `fileHref` → `<a href=…code/…html>`; null resolver → plain `<code>`. No dead links either way.

**`SiteGeneratorCommitDetailsTests` (generation-level) — mirror `SiteGeneratorGitInsightsTests`:**
- **Opt-in on (real git repo, `DeepGitAnalytics = true`):** `commit/{shortHash}.html` pages exist and are bounded; a known commit's subject/author appears; the per-day page's hash is now a link into `commit/…`; the git-insights hub's "latest {hash}" resolves to a `commit/…` link. Use the `TryCreateGitHistory()` fixture pattern (no-op return when git CLI is absent).
- **Opt-out off (`DeepGitAnalytics = false`) — AC #2 perf/baseline pin:** **no** `commit/` dir/pages, no error; day pages render with **plain** `<code>` hashes (no `commit/` links); baseline output otherwise identical. Assert `Directory.Exists(commit/)` is false and `TryComputeDeep` isn't invoked (the flag gate).
- **Graceful degradation (`DeepGitAnalytics = true`, non-git dir):** `DeepGit` null → no `commit/` pages, no `Error` event, day pages + site still render (NFR-2).
- **Reference linkification:** a commit whose subject/body mentions "Story 1.1"/"FR-9" → the rendered `commit/…html` contains the linkified anchor (via `ApplyReferenceLinks`), guarded on target existence.
- **Determinism:** two runs over the same repo produce byte-identical `commit/…html` (strip the footer timestamp, as `SiteGeneratorGitInsightsTests` does).

**Run:** `dotnet test` from repo root. Then two real passes against this repo (default `SpecScribeOutput/`; **do not** pass `--output docs/live` — [[generate-output-dir-is-specscribeoutput]]):
1. **Baseline:** `dotnet run --project src/SpecScribe` (no `--deep-git`) → confirm no `SpecScribeOutput/commit/` dir, and per-day pages show plain `<code>` hashes.
2. **Deep:** `dotnet run --project src/SpecScribe --deep-git` → open a `commit/{hash}.html` page: confirm subject `<h1>`, author+date attribution, readable body, files table with per-file +/− churn, "Story N.M"/"FR" mentions linkified, no JS, invariant dates, escaped content; confirm a day page hash and the git-insights "latest" hash now link here; confirm a non-git run degrades cleanly.

---

## Previous Story Intelligence

- **Story 3.8 (Git Insights hub) — the load-bearing predecessor; it already did most of 7.5's foundation.** It landed the enriched shared fetch (`%b` + numstat), the public `ParseNumstatRecords` → `DeepCommit`, the `DeepFileChange` churn record, AND the guarded `commitHref`/`fileHref` resolver seams on `GitInsightsTemplater` (rendered plain, explicitly "unwired until Stories 7.1/7.4/7.5 land"). 7.5 is the story that lights up the `commitHref` seam. **Re-reading 3.8's `GitMetrics` + `GitInsightsTemplater` before starting will save you from re-implementing the data layer.** [Source: [GitMetrics.cs:46-61,371-436](../../src/SpecScribe/GitMetrics.cs), [GitInsightsTemplater.cs:18-20,161-164](../../src/SpecScribe/GitInsightsTemplater.cs), [SiteGenerator.cs:154-162](../../src/SpecScribe/SiteGenerator.cs)]
- **Story 3.1/3.2 (git pulse + deep git) — the fetch/gate/bounding foundation.** The `--deep-git` gate is the perf guarantee; the `-n 300` cap is the bounding; `TryComputeDeep`/`ParseNumstatLog` is the one path. [[deep-git-single-numstat-path]] [Source: [GitMetrics.cs:258-350](../../src/SpecScribe/GitMetrics.cs)]
- **The commit-day pages (Story 1.5/heatmap) — the exact template + phase + entry-record trio to mirror.** `CommitDayTemplater` (synthesized shell), `GenerateCommitDaysInternal` (wipe+recreate, per-item try/catch, `ApplyReferenceLinks`, record entries), `CommitDayEntry`. Copy the structure, swap the payload. [Source: [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs), [SiteGenerator.cs:394-435](../../src/SpecScribe/SiteGenerator.cs), [CommitDayEntry.cs](../../src/SpecScribe/CommitDayEntry.cs)]
- **Story 7.4 (Advanced code coverage) — the sibling on the same fetch, and its `_commitPages` reference is aspirational.** 7.4's draft says the `_commitPages` seam comes "from 7.3", but 7.3 is unmerged — **7.5 owns creating `_commitPages`**, and 7.4 (also unmerged) will consume it. Coordinate the seam shape so 7.4's change-history commit links reuse it. [Source: [7-4 story:44,68,109](7-4-advanced-code-and-git-coverage.md)]
- **Recurring lessons to apply:** escaping (git author/subject/**body** are all free text + injection surfaces) and stale-output are this renderer's two most common regressions; invariant/culture-safe formatting for every derived date; extend the monolith in place (no package split); grep in-flight/recent story files for stale repeated commands (`--output docs/live` burned three Epic 2 stories) before closing. [Source: [sprint-status.yaml:120-135](sprint-status.yaml); project memory]

## Git Intelligence Summary

Recent `main` history is Epic-3 git-insights delivery (Story 3.8: "whole-row select + fixed-width detail column", "Deep commit analysis") plus Epic-7 planning. The enriched deep-git fetch, `DeepCommit`/`ParseNumstatRecords`, and the `GitInsightsTemplater` guarded seams are **on `main`** (3.8 landed even though `sprint-status.yaml` still marks 3-8 `ready-for-dev` — the code is present; verify with `Grep` before assuming). **7.1's code pages (`_codePages`) and 7.3's date pages/`_commitPages` are NOT merged** — which is why 7.5 owns `_commitPages` and guards its file links (plain until 7.1). No per-commit page code exists yet.

> **Worktree note:** if you run this in a worktree, edit files at the worktree path — do **not** re-root relative paths back at `C:\Dev\SpecScribe`. `main` has a background auto-committer. [[worktree-edits-must-target-worktree-path]]

## Latest Technical Information

No external libraries or APIs are introduced — nothing to version-check. Platform notes:
- **The shared deep-git fetch format is already correct and multi-line-body-safe:** `%x01` (record sentinel) marks each commit; `%x1f` (field sentinel) separates hash/author/date/subject/body, with a trailing `%x1f` closing the body so multi-line bodies can't bleed into the numstat rows. `ParseNumstatRecords` handles both this enriched shape and the legacy `%x01%H`-only shape. You consume its output; you don't touch the format. [Source: [GitMetrics.cs:271-272,352-436](../../src/SpecScribe/GitMetrics.cs)]
- **`%h` (abbreviated, baseline `ParseLog`) vs `%H` (full, deep `ParseNumstatRecords`)** is the one cross-fetch reconciliation you must handle in the `commitHref` resolver — match by prefix, not equality (git can widen `%h` past 7 chars on collision). See the Developer Context hazard note.
- **`--numstat` binary rows** are `-\t-\tpath`; the path counts as a change but the counts are null — render a "binary"/"—" marker, not `+0`/`−0`. [Source: [GitMetrics.cs:414-423](../../src/SpecScribe/GitMetrics.cs)]

## Project Context Reference

- FR-10 (per-commit detail pages; attribution-not-ranking, amended 2026-07-08): [Source: PRD FR-10; epics.md#Story 7.5]
- Epic 7 goal ("advanced code-and-git coverage as opt-in depth") + Story 7.5 ACs: [Source: [epics.md:946-964](../../_bmad-output/planning-artifacts/epics.md)]
- Shared deep-git fetch + per-commit record to expose: [Source: [GitMetrics.cs:46-61,271-350,371-436](../../src/SpecScribe/GitMetrics.cs); [[deep-git-single-numstat-path]]]
- Deep-git threading + gate: [Source: [SiteGenerator.cs:124-162](../../src/SpecScribe/SiteGenerator.cs), [ProgressModel.cs:39](../../src/SpecScribe/ProgressModel.cs)]
- Template/phase/entry trio to mirror: [Source: [CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs), [SiteGenerator.cs:394-435](../../src/SpecScribe/SiteGenerator.cs), [CommitDayEntry.cs](../../src/SpecScribe/CommitDayEntry.cs)]
- Guarded `commitHref` seam to wire: [Source: [GitInsightsTemplater.cs:23-28,161-164,234-239](../../src/SpecScribe/GitInsightsTemplater.cs)]
- Reference linkification path: [Source: [SiteGenerator.cs:875-887](../../src/SpecScribe/SiteGenerator.cs)]
- Generation-level test fixture (temp git repo): [Source: [SiteGeneratorGitInsightsTests.cs:13-171](../../tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs)]
- Architecture invariants (AD-4, local/read-only, graceful degradation, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)]
- Project memory: [[deep-git-single-numstat-path]], [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[epic-7-code-link-strategy]], [[generate-output-dir-is-specscribeoutput]], [[worktree-edits-must-target-worktree-path]].

---

## Tasks / Subtasks

- [x] **Task 1 — Expose the existing per-commit records on the deep pulse (AC: #1)**
  - [x] Add `IReadOnlyList<DeepCommit> Commits` (empty default, never null) to `DeepGitPulse`; populate it in `ParseNumstatLog` from the `commits` records it already parses via `ParseNumstatRecords`. No fetch change, no new record type. Preserve `Hotspots`/`Coupling`/`Insights` + all existing tests. [Source: [GitMetrics.cs:34-44,294-350](../../src/SpecScribe/GitMetrics.cs)]
- [x] **Task 2 — `CommitDetailTemplater` → `commit/{shortHash}.html` (AC: #1, #2)**
  - [x] New `CommitDetailTemplater` mirroring `CommitDayTemplater`'s synthesized shell (skip-link, single `<main>`, breadcrumb, `doc-header`/`doc-body`, footer). Render subject `<h1>`, author+date attribution pill (guard null timestamp), full body as escaped paragraph-preserving prose (omit when empty), and a scrollable files-changed table (path, +added, −deleted; binary marker for null churn). Path is a guarded `fileHref` link (plain until 7.1). Escape everything; invariant dates; neutral tokens.
  - [x] New `CommitDetailEntry(Hash, OutputRelativePath)` record.
- [x] **Task 3 — Generation phase + `_commitPages` seam (AC: #1, #2)**
  - [x] Add `_commitPages` (hash→path) and `_commitDetails` fields. Add `GenerateCommitDetailsInternal` mirroring `GenerateCommitDaysInternal`: gate on `_progress?.DeepGit?.Commits`, wipe+recreate `commit/`, per-commit try/catch, `File.WriteAllText(..., ApplyReferenceLinks(html, rel))`, record entries, populate `_commitPages`. Call it in `GenerateAll` **before** the commit-days and git-insights phases. Filename short hash = `Hash[..7]` (document the collision fallback).
- [x] **Task 4 — Wire the guarded `commitHref` resolver into both consumers (AC: #1, #2)**
  - [x] Add optional `commitHref` param to `CommitDayTemplater.RenderPage`; render the `commit-hash` as a guarded link; pass the resolver from `GenerateCommitDaysInternal`.
  - [x] Pass the resolver as `commitHref:` into the existing `GitInsightsTemplater.RenderPage(...)` call. Resolver matches full hash then `StartsWith` prefix (handles `%h` vs `%H`).
- [x] **Task 5 — Styling (AC: #1)**
  - [x] Add `.commit-detail` (+ files table, body prose, meta pill) styles in `src/SpecScribe/assets/specscribe.css` using neutral tokens (not `--status-*`); table scrolls in its own container. **No JavaScript.** Optional companion `StylesheetTests` assertion.
- [x] **Task 6 — Tests (AC: #1, #2)**
  - [x] `GitMetricsTests`: `Commits` exposed (shape, newest-first, binary rows, empty→empty); `Hotspots`/`Coupling`/`Insights` back-compat.
  - [x] `CommitDetailTemplaterTests`: four signals render; a11y shell; escaping; binary/empty-body/null-date partials; guarded file link (present/absent).
  - [x] `SiteGeneratorCommitDetailsTests`: opt-in on → pages exist + day/hub hashes link in; opt-out off → no `commit/` + plain hashes + baseline identical; graceful degradation (no git → no pages, no error); reference linkification present; determinism.
- [x] **Task 7 — Full generation pass + manual verify (AC: #1, #2)**
  - [x] `dotnet test` green (859 tests). Baseline generate (no `--deep-git`) → no `commit/` dir, day-page hashes plain. Deep generate (`--deep-git`) → `commit/{hash}.html` (143 pages, bounded) shows subject/author+date/body/files+churn with linkified references, no new JS, invariant dates, escaped content; day + hub hashes link in. Non-git run degrades cleanly. (Default `SpecScribeOutput/`; never `--output docs/live`.)

## Dev Notes

- **The data already exists — resist re-fetching.** The single biggest risk on this story is following the stale 2026-07-08 draft and re-extending the git fetch / inventing a `CommitDetail` record. Story 3.8 landed the enriched fetch and `DeepCommit`. Your Task 1 is one property. [[deep-git-single-numstat-path]]
- **One fetch, one gate.** Per-commit pages ride the existing `--deep-git`-gated, `-n 300`-bounded `DeepGitPulse`; that gate+cap IS the AC #2 bounding + perf guarantee. Nothing per-commit runs on the baseline path.
- **Wire, don't build, the link seams.** `GitInsightsTemplater.commitHref` already exists and renders plain; `CommitDayTemplater` needs the param added. Both use the same guarded-link discipline (`GuardedLink`). 7.5 owns `_commitPages`; 7.3/7.4 will consume it.
- **`%h` vs `%H`:** resolve by prefix, not equality — the day pages carry abbreviated hashes from a *different* (baseline) fetch. This is the one non-obvious integration bug waiting to happen.
- **Escape the body.** Commit bodies are the largest free-text injection surface on the site — `PathUtil.Html` every segment before wrapping in `<p>`/`<br>`. Attribution, never ranking. No JS. Neutral tokens. Invariant dates. [[charting-is-pure-svg-no-js]] [[specscribe-status-token-system]]

### Project Structure Notes

- Data change is one `DeepGitPulse` property + its population in `ParseNumstatLog`. Render is one new templater + one entry record. Wiring is one new `SiteGenerator` phase + the `_commitPages` field + two resolver hand-offs (day pages, hub). New output dir `commit/`. No package restructure (deferred seed, Epics 4/6), no new nav entry, no new git call.
- `_commitPages` is the shared seam later stories (7.3 date pages, 7.4 change-history) consume — design it as a generator field with an empty default, populated only under `--deep-git`.

### References

- [Source: [epics.md:946-964](../../_bmad-output/planning-artifacts/epics.md)] — Story 7.5 user story + both ACs.
- [Source: [src/SpecScribe/GitMetrics.cs:46-61,271-350,371-436,460-526](../../src/SpecScribe/GitMetrics.cs)] — `DeepCommit`/`DeepFileChange`, the shared fetch, `ParseNumstatLog` (add `Commits`), `ParseNumstatRecords`, `BuildInsights` (`LatestHash` = full `%H`).
- [Source: [src/SpecScribe/CommitDayTemplater.cs](../../src/SpecScribe/CommitDayTemplater.cs), [src/SpecScribe/CommitDayEntry.cs](../../src/SpecScribe/CommitDayEntry.cs)] — template + entry-record trio to mirror.
- [Source: [src/SpecScribe/SiteGenerator.cs:31,124-162,394-435,452,875-887](../../src/SpecScribe/SiteGenerator.cs)] — phase precedent, gate, `commitHref` call site, `ApplyReferenceLinks`.
- [Source: [src/SpecScribe/GitInsightsTemplater.cs:23-28,161-164,234-239](../../src/SpecScribe/GitInsightsTemplater.cs)] — the guarded `commitHref` seam + `GuardedLink` idiom.
- [Source: [src/SpecScribe/ProgressModel.cs:39](../../src/SpecScribe/ProgressModel.cs), [src/SpecScribe/Charts.cs:1073-1085](../../src/SpecScribe/Charts.cs)] — `DeepGit` threading; `LinkedCommitDays` bounding philosophy.
- [Source: [tests/SpecScribe.Tests/CommitDayTemplaterTests.cs](../../tests/SpecScribe.Tests/CommitDayTemplaterTests.cs), [tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs)] — templater + temp-git-repo generation test patterns.
- [Source: [7-4-advanced-code-and-git-coverage.md:44,68,109](7-4-advanced-code-and-git-coverage.md)] — the `_commitPages` consumer (7.5 owns creating it).
- [Source: [_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — invariants (AD-4, local/read-only, graceful degradation, seed-not-invariant).
- [[deep-git-single-numstat-path]] / [[charting-is-pure-svg-no-js]] / [[specscribe-status-token-system]] / [[epic-7-code-link-strategy]] / [[generate-output-dir-is-specscribeoutput]] / [[worktree-edits-must-target-worktree-path]] — project memory.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8) — bmad-dev-story workflow, 2026-07-12.

### Debug Log References

- `dotnet build src/SpecScribe` → clean (0 warnings, 0 errors) throughout.
- `dotnet test` → **859 passed, 0 failed** (18 new Story-7.5 tests; real git available on host so the gated generation tests ran, not no-op'd).
- Golden `GenerateAll_GoldenContentFingerprint` flipped once (expected): adding `.commit-detail` styles changed `specscribe.css` bytes. Regenerated the constant `31ef6fdd… → 1cd24db9…`; the output FILE SET is unchanged for the non-deep-git golden fixture (commit pages are `--deep-git`-gated), so only the stylesheet moved. [[golden-diff-normalization-gotchas]]
- Manual verify against this repo: baseline `generate` → no `SpecScribeOutput/commit/`, day-page hashes plain (`0` `commit-hash-link`, `2` plain `commit-hash`). `generate --deep-git` → 143 `commit/*.html` pages (bounded ≤ 300); a page shows `<h1>` subject, `by Matt Eland · Thu, Jul 9, 2026 at 15:10` attribution, files-changed caption, `+N`/`&minus;N` churn; day pages link `../commit/{hash}.html`; the Git Insights hub's "latest" links `commit/{hash}.html`; no new `<script>` (the two script tags are the pre-existing shared shell — a day page carries the identical pair).
- **⚠️ Concurrent-tree note for the reviewer:** the full suite reached **859 passed / 0 failed** on completion of Story 7.5. A subsequent, *concurrent and unrelated* effort (**Story 7.1 rework** — a "relationships block / hub-and-spoke reference graph" replacing the `.code-referenced-by` back-nav) began editing shared files on `main` mid-session (`Charts.cs` +65, `CodeFileTemplater.cs` +124, `Commands.cs`, `specscribe.css` +210 with `.code-referenced-by` removed). That work introduced **3 failures OUTSIDE Story 7.5's scope** — `StylesheetTests.Stylesheet_HasReferencedByStyles`, `SiteGeneratorCodeCitationTests.CodePage_HasReferencedByBackToCitingArtifacts` (both assert the now-removed 7.1 `.code-referenced-by` surface), and `GenerateAll_GoldenContentFingerprint` (moved again by those 7.1-rework rendered-byte changes). None are caused by 7.5's diff (verified: the css diff removes `.code-referenced-by` under a `Story 7.1 (rework)` comment; the templater diff restructures that aside). The golden constant here (`1cd24db9…`) is regenerated for **7.5's `.commit-detail` CSS only**; it was deliberately NOT re-bumped to absorb the concurrent 7.1-rework output — that story owns its own golden regen.

### Completion Notes List

- **Data layer was already present (as the context predicted).** Task 1 was one property: `DeepGitPulse.Commits` (`init`, empty-list default), populated in `ParseNumstatLog` from the `commits` records it already parsed via `ParseNumstatRecords`. No git-command change, no new record type; `Hotspots`/`Coupling`/`Insights` and all existing tests preserved (verified back-compat assertions in the same test).
- **`CommitDetailTemplater`** mirrors `CommitDayTemplater`'s synthesized shell exactly (skip-link → single `<main id="main-content">`, breadcrumb, `doc-header`/`doc-body`, footer). Renders the four AC-#1 signals: subject `<h1>` (`(no subject)` fallback), author+date attribution pill (guards null timestamp → author only, never a rank), full body as escaped paragraph-preserving prose (blank line → `<p>`, single newline → `<br>`; omitted entirely when empty), and a scrollable files-changed table (path, `+N`/`&minus;N`, binary rows show a `binary` marker + em dashes, never `+0`/`&minus;0`). Every git free-text field escaped via `PathUtil.Html`.
- **`_commitPages` seam owned here** (full `%H` → `commit/{shortHash}.html`). `GenerateCommitDetailsInternal` runs BEFORE the commit-days and git-insights phases, gated on `_progress?.DeepGit is { Commits.Count: > 0 }`, wipe+recreates `commit/`, per-item try/catch, `ApplyReferenceLinks` (so "Story N.M"/"FR-9" in the subject **and** body linkify), records `CommitDetailEntry`. Filename stem = `Hash[..7]` with a `UniqueShortHash` collision-widening fallback (belt-and-suspenders).
- **`%h` vs `%H` reconciliation:** the `CommitHref` resolver matches full-hash keys exactly, then by `StartsWith` prefix — so the hub's full-`%H` `LatestHash` and the day page's abbreviated-`%h` `ShortHash` both resolve. Verified live on this repo.
- **Deviation from the (pre-7.1-merge) context, deliberately:** the story context (written 2026-07-09) said file links stay plain until 7.1 lands and to "pass null." Stories 7.1/7.2 have since landed on `main` (`_codePages` exists and is populated), so I wired the guarded `CodePageHref` resolver into the file-path cells — strictly better for AC #2 ("file entries lead to the corresponding file page"), still no dead links (plain `<code>` when a file has no in-portal page, e.g. external `--code-url` mode). The templater stays resolver-agnostic; this is a SiteGenerator wiring choice.
- **Deviation on one test assertion:** the Task-6 opt-out subtask suggested asserting "`TryComputeDeep` not called." There is no seam to observe that; I assert the observable outcome instead (no `commit/` dir + plain day-page hashes), exactly as the sibling `SiteGeneratorGitInsightsTests` does for its own gate. Task text updated to match what was implemented.
- **Graceful degradation** holds at every branch: flag off / git absent → `DeepGit` null → `Commits` empty → no `commit/` dir, no pages, day/hub hashes plain, baseline site intact (proven by `GenerateAll_FlagOff…` and `…WithoutGitHistory…` tests). Neutral tokens only (no `--status-*`); no JavaScript added.

### Change Log

- 2026-07-12 — Implemented Story 7.5 (per-commit detail pages). Added `DeepGitPulse.Commits`; new `CommitDetailTemplater` + `CommitDetailEntry` rendering `commit/{shortHash}.html`; new `GenerateCommitDetailsInternal` phase + `_commitPages`/`_commitDetails` seam; wired the guarded `commitHref` resolver into `CommitDayTemplater` (new optional param) and the Git Insights hub, and a guarded `CodePageHref` file-link resolver; `.commit-detail` styling. +18 tests (859 total, green); golden fingerprint regenerated for the CSS change. Status → review.

### File List

**New:**
- `src/SpecScribe/CommitDetailTemplater.cs`
- `src/SpecScribe/CommitDetailEntry.cs`
- `tests/SpecScribe.Tests/CommitDetailTemplaterTests.cs`
- `tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs`

**Modified:**
- `src/SpecScribe/GitMetrics.cs` — `DeepGitPulse.Commits` property + population in `ParseNumstatLog`.
- `src/SpecScribe/CommitDayTemplater.cs` — optional `commitHref` param; guarded hash link.
- `src/SpecScribe/SiteGenerator.cs` — `_commitPages`/`_commitDetails` fields; `GenerateCommitDetailsInternal`; `UniqueShortHash`, `CommitHref`, `CodePageHref` resolvers; phase call before commit-days/hub; resolver hand-offs into `GenerateCommitDaysInternal` and `GitInsightsTemplater.RenderPage`.
- `src/SpecScribe/assets/specscribe.css` — `.commit-detail` (+ files table, body prose, meta pill, `.commit-hash-link`) styles, neutral tokens.
- `tests/SpecScribe.Tests/GitMetricsTests.cs` — `Commits`-exposure + empty-log tests.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden fingerprint constant regenerated for the CSS change.
- `_bmad-output/implementation-artifacts/7-5-per-commit-detail-pages.md` — this story (frontmatter `baseline_commit`, tasks, Dev Agent Record, status).
