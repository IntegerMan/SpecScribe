---
baseline_commit: 2dde59de7a33bf20d566e64094da6945e83a6894
---

# Story 7.6: Source Code Treemap for Codebase Exploration

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a project reviewer exploring an unfamiliar codebase,
I want a treemap of the source tree sized by lines of code and colorable by git-derived change signals,
so that I can see at a glance where the code mass and the churn live, and drill into any area.

## Acceptance Criteria

1. **(Sizing & structure)** **Given** a repository with source files **When** I open the code-map surface **Then** a treemap renders each source file as a rectangle whose area is proportional to its line count, nested within its directory **And** the layout is deterministic, with directory labels and clear boundaries. [Source: epics.md#Story 7.6 AC1; FR14; UX-DR19]
2. **(Colorize dimensions)** **Given** deep-git analysis is available **When** I choose a colorize dimension **Then** files are shaded by that dimension — change frequency (commit count), relative creation date, relative last-modified date, or average change size — on a **non-lifecycle sequential scale** with a legend **And** when git data is unavailable the treemap still renders sized-by-LOC with a neutral fill and a clear notice (graceful degradation). [Source: epics.md#Story 7.6 AC2; FR14; FR19]
3. **(Tooltips, links & zoom)** **Given** I hover or focus a rectangle **When** the tooltip appears **Then** it shows the file path, line count, and available git metrics **And** selecting a file routes to its in-portal code page (Story 7.1) **when available**, and I can zoom into a directory and back out via a breadcrumb, with drill state deep-linkable (mirroring the sunburst conventions). [Source: epics.md#Story 7.6 AC3; FR15]
4. **(Accessibility & truthfulness)** **Given** keyboard and screen-reader navigation **When** I traverse the treemap **Then** rectangles are focusable with descriptive labels announcing name and metric value **And** color is never the sole signal (every metric is available as text) **And** reduced-motion is respected, preserving the Story 1.4/1.5 conventions and NFR6. [Source: epics.md#Story 7.6 AC4; NFR6; UX-DR16/UX-DR18]

## Tasks / Subtasks

> **⚠ READ FIRST — this story is a REVERT + REPURPOSE + BUILD, not a greenfield add.** The retired Story 3.4
> disclosure-tree (`ProjectTree` + `Charts.ProjectStructureTree` + `structure.html`) is **still on `main`** and
> must be replaced by the treemap. Keep the *visualization-agnostic integration seams* (page-write + nav gate +
> `Structure`/"Code Map" glyph + graceful-omission shape); replace the *model + renderer + styles + viz-specific
> tests*. Per-file disposition is in [SCP §4E](../planning-artifacts/sprint-change-proposal-2026-07-08.md). Do NOT
> keep the `<details>` tree as a second surface. [Source: [[story-3-4-redefined-as-source-treemap]]]

- [x] **Task 1: Extend the shared deep-git parse with an untruncated per-file metric view (AC: #2)**
  - [x] Subtask 1.1: In [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs), add a pure aggregator `public static IReadOnlyDictionary<string, CodeFileMetrics> BuildCodeMapMetrics(IReadOnlyList<DeepCommit> commits)` that folds the SAME `IReadOnlyList<DeepCommit>` records `BuildInsights` already consumes into **one entry per file that appears in the window (NOT truncated to top-N)**. `CodeFileMetrics(int Changes, int TotalChurn, DateOnly? FirstDate, DateOnly? LastDate)` where `TotalChurn = Σ (Added ?? 0) + (Deleted ?? 0)` and avg change size = `TotalChurn / Changes` (compute at render, guard divide-by-zero). Records arrive **newest-first**, so `LastDate` = the first parsed day seen for a file, `FirstDate` = the last (oldest) parsed day seen (keep overwriting). `Changes` counts once per commit per file (a file listed twice in one commit is one change — mirror `BuildInsights`'s `seenInCommit` guard). Pure, repo-free, never throws, empty input → empty map. [Source: [[deep-git-single-numstat-path]]; [GitMetrics.BuildInsights](../../src/SpecScribe/GitMetrics.cs:465)]
  - [x] Subtask 1.2: Surface the result on `DeepGitPulse` as a new **settable** property `public IReadOnlyDictionary<string, CodeFileMetrics> CodeMapMetrics { get; set; }` (default empty dict, mirroring how `Insights` is settable so `SiteGenerator` can clear/ignore it — [DeepGitPulse.Insights](../../src/SpecScribe/GitMetrics.cs:43)). Populate it inside `ParseNumstatLog` from the same `commits` list that already feeds `BuildInsights` — **one fetch, one parse, one more view.** Do NOT add a second `git log`, a second parse, or a parallel git module. [Source: [[deep-git-single-numstat-path]]; [GitMetrics.ParseNumstatLog:354](../../src/SpecScribe/GitMetrics.cs)]
  - [x] Subtask 1.3: **Honesty about the window.** The shared fetch is bounded `-n 300` ([GitMetrics.cs:277](../../src/SpecScribe/GitMetrics.cs)), so `FirstDate` is "earliest commit **within the analyzed window** touching this file", not true repository creation — matching the AC's deliberate "**relative** creation date" wording. Document this in the record's XML-doc and reflect it in the tooltip/legend copy (e.g. "recency within recent history"), never claim absolute file age. A file with no git record at all (outside the window, or `--deep-git` off) simply has no metrics → neutral fill (per-file graceful degradation, AC #2). [Source: epics.md#Story 7.6 AC2]
  - [x] Subtask 1.4: Tests in [GitMetricsTests / the deep-git test file](../../tests/SpecScribe.Tests) (find the existing `ParseNumstatLog`/`BuildInsights` tests and sit beside them): `BuildCodeMapMetrics` counts changes once-per-commit-per-file; sums churn across rows (binary rows contribute 0, never throw); `FirstDate`/`LastDate` resolve correctly from a newest-first record stream (including a record whose newest date is null → backfill from an older one); untruncated (a 60-file window yields 60 entries, unlike `BuildInsights`'s top-50); empty input → empty map.

- [x] **Task 2: Add the pure code-map model + squarified layout (AC: #1, #2, #3)**
  - [x] Subtask 2.1: **Replace** [ProjectTree.cs](../../src/SpecScribe/ProjectTree.cs) with `CodeMap.cs` (a rename/rewrite — the retired disclosure model is not reused). Mirror the SAME shape every pure model in this repo uses ([WorkInventory](../../src/SpecScribe/WorkInventory.cs), [ArtifactCoverage](../../src/SpecScribe/ArtifactCoverage.cs), the old `ProjectTree`): a pure static `Build` over already-gathered inputs (**NO disk access**), an `Empty` singleton, an `IsEmpty` flag callers use to omit the surface, never-throw (NFR2). Define `CodeMapNode` — a directory or file node carrying `Label` (segment name, single-child dir chains joined with `" / "` exactly as `ProjectTree.BuildDir` did), `RepoRelativePath` (full repo-relative path, forward-slash), `bool IsDirectory`, `long Lines` (a directory's `Lines` = Σ descendant file lines — the treemap size key), the optional per-file git metrics (`CodeFileMetrics?`, null for directories and for files with no git record), and `Children`. [Source: [ProjectTree.cs](../../src/SpecScribe/ProjectTree.cs) trie/collapse logic — reuse it; [WorkInventory.cs](../../src/SpecScribe/WorkInventory.cs) shape]
  - [x] Subtask 2.2: `CodeMap.Build(IReadOnlyList<(string RepoRelativePath, long Lines)> sourceFiles, IReadOnlyDictionary<string, CodeFileMetrics> gitMetrics)` — pure. Nest paths into a trie (reuse the `ProjectTree.Build` trie + `OrdinalIgnoreCase` deterministic ordering + single-child-chain collapse, which are directly portable), roll `Lines` up each directory (Σ children), and attach `gitMetrics[path]` to each file node (normalize both sides through [PathUtil.NormalizeSlashes](../../src/SpecScribe/PathUtil.cs:10), compare via a normalized `OrdinalIgnoreCase` lookup — same Windows/Git-Bash path-case discipline the old builder used). Files with zero lines or no metric still appear (sized minimally / neutral). Never throws.
  - [x] Subtask 2.3: Add a **pure, deterministic squarified-treemap layout** — `CodeMap` (or a sibling `Treemap` static) that maps the node tree to positioned rectangles `TreemapRect(CodeMapNode Node, double X, double Y, double W, double H, int Depth)` within a fixed viewBox. Use the standard **squarified** algorithm (Bruls, Huizing & van Wijk 2000) so aspect ratios stay near-1 and the layout is legible; ordering is a pure function of the (already deterministic) node order so output is byte-stable for snapshot tests. This computes at **generation time** in C# — the same "layout is computed once, no client layout math" discipline [Charts.CouplingGraph](../../src/SpecScribe/Charts.cs) already follows. Guard against zero/negative sizes and pathological depth (cap recursion, never throw). [Source: [[charting-is-pure-svg-no-js]]]
  - [x] Subtask 2.4: XML-doc that the input file set is a **source-code walk (repo-relative paths)** — a DIFFERENT path space from the retired artifact tree's `_bmad-output`-relative `*.md` set. The git-metric keys are repo-relative (git emits repo-relative paths), so the two align; the `_docs` output-href map is NOT the link source here (that was the artifact tree). Code-page links come from a guarded resolver (Task 4), not `_docs`.

- [x] **Task 3: Enumerate source files + line counts in the generator; wire the git-metric join (AC: #1, #2)**
  - [x] Subtask 3.1: In [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs), add `private IReadOnlyList<(string RepoRelativePath, long Lines)> EnumerateCodeFiles()` — the ONE new disk read. **Prefer `git ls-files` via the existing `GitMetrics` shell path** (tracked files only → automatically excludes `bin/`, `obj/`, `.git/`, `node_modules/`, and everything `.gitignore` covers, and defines "the codebase" the same way git does). If git is unavailable / not a repo, fall back to a bounded `Directory.EnumerateFiles` walk with an explicit exclude list (`bin`, `obj`, `.git`, `node_modules`, `.vs`, `.claude/worktrees`) — or simply yield empty (surface omitted) rather than risk an unbounded walk. Read each file's line count with a **per-file size cap** (skip/estimate files above e.g. 2 MB) and a **binary guard** (skip files with NUL bytes / non-text extensions — they have no meaningful LOC). Bound total file count defensively. Respect NFR1: this runs once per full generation, wrapped never-throw. [Source: NFR1; [GitMetrics.RunGit](../../src/SpecScribe/GitMetrics.cs:567) for the shell pattern — but do NOT add git logic to `GitMetrics`; a thin `git ls-files` helper is fine]
  - [x] Subtask 3.2: The git-metric join: pull `CodeMapMetrics` from `_progress?.DeepGit` (non-null only when `--deep-git` gated `TryComputeDeep` on — [SiteGenerator.cs:503](../../src/SpecScribe/SiteGenerator.cs)). When `DeepGit` is null (flag off or deep pass failed), pass an **empty** metric dict → the treemap renders sized-by-LOC with neutral fill + the "git data unavailable" notice (AC #2 degradation). The colorize controls/legend are present only when metrics exist.
  - [x] Subtask 3.3: **Repurpose** the `WriteStructure` seam into `WriteCodeMap`. Rename [SiteNav.StructureOutputPath](../../src/SpecScribe/SiteNav.cs:34) `"structure.html"` → `CodeMapOutputPath = "code-map.html"`, `HasStructure` → `HasCodeMap`, the `hasStructure` param → `hasCodeMap`, and the nav/quick-link label `"Structure"` → `"Code Map"` (description e.g. "Explore the codebase by size and change activity."). Keep the `Icons.ForConcept("Structure")` glyph but **re-key it `"Code Map"`** (or add a `"Code Map"` key) so `RenderNavBar`'s auto-prefix resolves ([SiteNav.cs:178](../../src/SpecScribe/SiteNav.cs)). [Source: [SCP §4E disposition table](../planning-artifacts/sprint-change-proposal-2026-07-08.md)]
  - [x] Subtask 3.4: **Gate = one signal, but a NEW signal.** The old gate was `sourceRelatives.Count > 0` (the `_bmad-output` md set). The code-map gates on **code files found** (`EnumerateCodeFiles().Count > 0`). Compute `CodeMapAvailable` once and pass it to BOTH `SiteNav.Build(..., hasCodeMap: CodeMapAvailable)` (in [BuildNav](../../src/SpecScribe/SiteGenerator.cs:925)) AND the `WriteCodeMap` guard, exactly as `SprintAvailable`/the old `StructureAvailable` paired nav+page. A repo with no readable source files omits the nav item, quick link, and page together — no broken link. Update the two `SiteNav.Build(...)` call sites at [SiteGenerator.cs:77](../../src/SpecScribe/SiteGenerator.cs) and [:931](../../src/SpecScribe/SiteGenerator.cs).
  - [x] Subtask 3.5: Wrap gather+build in try/catch → `CodeMap.Empty` so any failure degrades to "surface omitted, generation still succeeds" (AD-4; NFR2 never-throw), matching the old `WriteStructure` and every insight provider. Call `WriteCodeMap(nav)` from `GenerateAll` where `WriteStructure` was called ([SiteGenerator.cs:181](../../src/SpecScribe/SiteGenerator.cs)) — after the pages phase so any future `_docs`/code-page dependency is populated. [Source: [ARCHITECTURE-SPINE.md#AD-4](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:58)]

- [x] **Task 4: Render `code-map.html` — server-rendered SVG treemap + progressive-enhancement controls (AC: #1, #2, #3, #4)**
  - [x] Subtask 4.1: Add `CodeMapTemplater.cs` (a page templater like [GitInsightsTemplater](../../src/SpecScribe/GitInsightsTemplater.cs) / [DeepAnalyticsTemplater](../../src/SpecScribe/DeepAnalyticsTemplater.cs)) with `public static string RenderPage(CodeMap map, IReadOnlyList<TreemapRect> layout, SiteNav nav, Func<string, string?>? fileHref = null)`. Reuse the standard page shell ([PathUtil.RenderHeadOpen](../../src/SpecScribe/PathUtil.cs:47) + nav + breadcrumb + `<main id="main-content">` + footer) that `RenderStructurePage` used ([SiteGenerator.cs:763](../../src/SpecScribe/SiteGenerator.cs)). Delete `RenderStructurePage`/`BuildStructureHrefMap`. The SVG treemap builder can live in `Charts` (`Charts.CodeTreemap(...)`) or the templater — keep it pure SVG.
  - [x] Subtask 4.2: **Server-render the DEFAULT view with JS off (AC #1 + degradation).** Emit one `<rect>` per file positioned by the precomputed `layout`, filled sized-by-LOC with the **default colorize dimension baked in** (change frequency when metrics exist, else neutral). Carry every metric on the rect as `data-*` attributes (`data-lines`, `data-changes`, `data-churn`, `data-first`, `data-last`, `data-path`) so the enhancement layer re-fills without a round-trip and the text equivalent can be derived. Directory group boundaries + directory `<text>` labels give "clear boundaries" (AC #1). All labels/paths HTML-escaped via [PathUtil.Html](../../src/SpecScribe/PathUtil.cs:24). No `<script>` is required for this baseline to be correct.
  - [x] Subtask 4.3: **Colorize on a non-`--status-*` sequential ramp + legend (AC #2).** Structure/churn is NOT a lifecycle stage — do NOT route through the six `--status-*` tokens (single source of truth for lifecycle color only; guard at [specscribe.css:953](../../src/SpecScribe/assets/specscribe.css), [StatusStyles.cs:3](../../src/SpecScribe/StatusStyles.cs)). Reuse the **commit-heatmap ramp discipline**: a quantized sequential scale (the heatmap uses `--parchment-dark`→`#ecd18f`→`#dfb455`→`--gold-light`→`--gold` at [specscribe.css:2032-2036](../../src/SpecScribe/assets/specscribe.css)) with a matching legend ("Less … More"). Bucket each dimension into levels (`class="codemap-cell level-N"`) so the ramp is CSS-tokenized, not hand-coded hex per rect. [Source: [[specscribe-status-token-system]]; [[charting-is-pure-svg-no-js]] (heatmap ramp precedent)]
  - [x] Subtask 4.4: **Rich tooltip via the body-level tip node (AC #3).** Route the hover/focus tooltip (path + line count + available git metrics) through the shared body-level `js-tip`/`data-tip` node, NOT a CSS `::after` on the rect — SVG rects inside a panel clip an `::after` tooltip. Reuse the existing tip mechanism. [Source: [[tooltip-clipping-use-ss-tooltip-node]]]
  - [x] Subtask 4.5: **Code-page link seam is GUARDED (AC #3).** A file rect routes to its in-portal code page **only when `fileHref?.Invoke(path)` returns non-null** — exactly the guarded-resolver pattern [GitInsightsTemplater](../../src/SpecScribe/GitInsightsTemplater.cs:19) already uses (`Func<string,string?>? fileHref`: no resolver / no target → plain, focusable rect, never a broken link). **Story 7.1 (in-portal code pages) is NOT yet on `main`** (it's `ready-for-dev`), so `SiteGenerator` passes `fileHref: null` today and rects render as non-links — the seam is wired but dormant until 7.1 lands and supplies the resolver. Do NOT build 7.1's code pages or the `CodeSourceBaseUrl` resolver here; just accept the param. [Source: [[epic-7-code-link-strategy]]; [[deep-git-single-numstat-path]] (7.5 owns `_commitPages`, 7.1 owns `_codePages` — neither merged)]
  - [x] Subtask 4.6: **Text-equivalent metrics table (AC #4, no-JS/a11y fallback).** Render a companion `<table>` (or definition list) listing every file with its path, line count, and git metrics as **text** — so color is never the sole signal and the surface is fully usable with JS off and by screen readers. This is the no-JS truth of the visualization (same "text equivalent beside the chart" discipline as [Charts.CouplingList](../../src/SpecScribe/Charts.cs) beside the coupling graph). Order it by the default dimension (descending) so the reading order is meaningful.
  - [x] Subtask 4.7: **a11y for the rects (AC #4).** Each file rect is focusable (`tabindex="0"`) with a descriptive `aria-label` announcing name + the active metric value (e.g. "GitMetrics.cs, 604 lines, 12 changes"). Preserve the shared `:focus-visible` ring (do NOT `outline:none` without a visible replacement — [[story-1-4-a11y-seams-for-1-5]]). Directory labels are `aria-hidden` decoration over the labelled rects, or grouped semantically — pick one and keep it consistent with the sunburst's `ariaLabel` convention (UX-DR7). [Source: [[story-1-4-a11y-seams-for-1-5]]]

- [x] **Task 5: Scoped, progressively-enhancing JS for zoom + dimension-switch (AC: #2, #3, #4)**
  - [x] Subtask 5.1: **ADOPTED architecture decision (SCP §3, recommended ruling): a scoped, degrading JS module is admitted as a deliberate, bounded exception to the "charts are pure SVG + links, no JS" principle** — the same way the mobile-nav toggle script ([SiteNav.cs:181](../../src/SpecScribe/SiteNav.cs)) and the tooltip/copy script already were. The exception is **strictly bounded**: everything in Tasks 4.2–4.7 works with JS OFF (server-rendered treemap sized-by-LOC + default color + full text-equivalent table + tooltips-on-focus via the tip node). JS only **enhances**. [Source: [SCP §3](../planning-artifacts/sprint-change-proposal-2026-07-08.md); [[charting-is-pure-svg-no-js]] (records this one small script now exists)]
  - [x] Subtask 5.2: **Dimension switch** — a small control (radio group / segmented buttons, keyboard-operable, present only when git metrics exist) that re-fills the rects by reading their `data-*` attributes and re-bucketing into the ramp levels client-side. No server round-trip; degrades to "default dimension only" with JS off.
  - [x] Subtask 5.3: **Directory zoom/drill + breadcrumb, deep-linkable — mirror the sunburst (AC #3).** Clicking/activating a directory zooms the viewBox to that subtree and pushes a breadcrumb; a back affordance zooms out. Encode drill state in the URL hash so it's deep-linkable and back/forward works — **reuse the sunburst's drill + URL-hash-state conventions (UX-DR5/UX-DR6/UX-DR7)**, do not invent a new state model. With JS off, no zoom — the full tree is visible and the table covers navigation. [Source: epics.md#Story 7.6 AC3 "mirroring the sunburst conventions"; UX-DR5/6/7]
  - [x] Subtask 5.4: **Reduced-motion (AC #4).** Any zoom transition must respect `prefers-reduced-motion` — reuse the two paired reduced-motion blocks and the `--motion-*` tokens (Story 3.5) rather than a bare transition; the "reduce" branch snaps instead of animating. [Source: [[motion-token-system]]; [[story-1-4-a11y-seams-for-1-5]]; UX-DR18]

- [x] **Task 6: Styles + revert the retired disclosure CSS (AC: #1, #4)**
  - [x] Subtask 6.1: Edit ONLY [src/SpecScribe/assets/specscribe.css](../../src/SpecScribe/assets/specscribe.css) — the embedded resource [StylesheetTests](../../tests/SpecScribe.Tests/StylesheetTests.cs) loads from the assembly manifest; the `docs/live/specscribe.css` copy is stale, never edit it. **Remove** the retired structure-tree CSS (the disclosure-tree block Story 3.4 added). **Add** treemap styles: `.codemap-cell` fills via the sequential `level-N` ramp (Subtask 4.3), directory-boundary strokes, the focus ring, legend, dimension-switch controls, and the responsive rule (the panel scrolls internally / SVG scales — no horizontal PAGE scroll on mobile, per the overflow discipline). Keep it OFF the `--status-*` tokens. [Source: [[generate-output-dir-is-specscribeoutput]]; [specscribe.css:2030-2067](../../src/SpecScribe/assets/specscribe.css) heatmap ramp]
  - [x] Subtask 6.2: Update the [StylesheetTests](../../tests/SpecScribe.Tests/StylesheetTests.cs) CSS guard: change the guarded structure-tree class name to a distinctively-named treemap class (e.g. `.codemap-cell`) so the seam can't be silently deleted.

- [x] **Task 7: Test coverage — replace disclosure tests, add treemap tests (AC: #1, #2, #3, #4)**
  - [x] Subtask 7.1: **Replace** `ProjectTreeTests.cs` with `CodeMapTests.cs` (pure `Build`, no disk — mirror [WorkInventoryTests](../../tests/SpecScribe.Tests/WorkInventoryTests.cs)): LOC rolls up directories (parent `Lines` = Σ children); deterministic ordering + single-child-chain collapse (port the retained `ProjectTree` assertions); a file with a git metric gets it, a file without one gets null (per-file degradation); empty input → `CodeMap.Empty` / `IsEmpty`; odd paths never throw (NFR2).
  - [x] Subtask 7.2: Squarified-layout tests: rectangles tile their parent without overlap and within the viewBox; total file-rect area is proportional to total lines; layout is byte-stable across runs (deterministic); zero-line / single-file / deeply-nested inputs never throw.
  - [x] Subtask 7.3: `CodeMapTemplater` / `Charts.CodeTreemap` render tests (replace the 3 `Charts.ProjectStructureTree` tests in [ChartsTests](../../tests/SpecScribe.Tests/ChartsTests.cs)): renders `<rect>` per file with `data-*` metric attributes + the legend; a file with a non-null `fileHref` resolves to an `<a>`/link, a null resolver → non-link focusable rect (guard the 7.1-dormant seam); the text-equivalent table lists every file with metrics as text; labels/paths HTML-escaped; the "git data unavailable" notice appears when the metric dict is empty.
  - [x] Subtask 7.4: Generation/nav integration (update [SiteNavTests](../../tests/SpecScribe.Tests/SiteNavTests.cs) `hasStructure`→`hasCodeMap` cases; update the `SiteGeneratorStructureTests` → code-map): `hasCodeMap: true` adds the "Code Map" nav item + quick link, `false` omits both; a repo with source files produces `code-map.html` with the SVG treemap + text table + the standard `<main id="main-content">`/nav/breadcrumb shell; a repo with no readable source files omits the page and the nav item together (one signal). Update the [IconsTests](../../tests/SpecScribe.Tests/IconsTests.cs) case for the re-keyed glyph.
  - [x] Subtask 7.5: `BuildCodeMapMetrics` tests already in Task 1.4. Ensure the FULL suite is green (the retired 3.4 added ~18 tests across ProjectTree/Charts/SiteNav/Icons/Stylesheet/SiteGeneratorStructure — all must be migrated, not left dangling against deleted code).

## Dev Notes

### The single most important framing: this REPLACES the retired Story 3.4 code on `main`

- Two same-day correct-course passes (2026-07-08) turned "Story 3.4" into this story. **First**, the delivered
  Story 3.4 — a zero-JS `<details>` disclosure tree over the `_bmad-output/**/*.md` **artifact** set →
  `structure.html` — was found to be a misinterpretation of an intended **source-code treemap**. **Second**, on
  reflection the treemap is a **code + per-file-git** feature, so it was re-seated into **Epic 7 as Story 7.6**;
  the Epic 3 artifact structural tree was **retired** (SpecScribe ships **no planning-artifact structural view**).
  Story numbers 3.4 and 3.7 are intentionally vacant. [Source: [[story-3-4-redefined-as-source-treemap]];
  [SCP + Amendment A](../planning-artifacts/sprint-change-proposal-2026-07-08.md)]
- **The disclosure-tree code is STILL ON `main` and this story reverts it.** `ProjectTree.cs`,
  `Charts.ProjectStructureTree`/`AppendTreeNode`, the structure-tree CSS, and their tests all exist. Do the
  revert + repurpose per the [SCP §4E disposition table](../planning-artifacts/sprint-change-proposal-2026-07-08.md):
  **keep** the visualization-agnostic seams (page-write, nav gate, glyph, graceful-omission try/catch→Empty),
  **replace** the model + renderer + styles + viz-specific tests. Read [the retired story file](3-4-interactive-tree-views-for-project-and-artifact-structure.md) for exactly what was built (its File List + Completion Notes are the revert checklist).
- **Do NOT keep the `<details>` tree as a second surface.** There is no artifact structural view in the product
  anymore; the code-map is the only structural visualization.

### Data seam — extend the ONE deep-git parse, never add a second `git log`

- All git integration lives in the single `GitMetrics` class. Story 3.2 landed the shared deep-git path
  (`TryComputeDeep` → one bounded `git log --numstat … -n 300` → pure `ParseNumstatRecords`/`ParseNumstatLog`);
  Story 3.8 enriched the fetch (author/date/subject/body sentinels) and added `BuildInsights`. Story 7.6 adds
  **one more view over the same parsed records** (`BuildCodeMapMetrics`), surfaced on `DeepGitPulse`. **Do NOT add
  a second `git log`, a parallel git module, or re-extend the fetch command** — the command is already rich enough.
  [Source: [[deep-git-single-numstat-path]]; [GitMetrics.cs:257-538](../../src/SpecScribe/GitMetrics.cs)]
- `BuildInsights` truncates to top-50 files — **not** usable to colorize a whole-codebase treemap. That's exactly
  why Task 1 adds an **untruncated** per-file map. Reuse `BuildInsights`'s once-per-commit-per-file counting and
  churn-summing logic; add `FirstDate` (oldest day, since records are newest-first) which `BuildInsights` doesn't track.
- The gate for colorize is `--deep-git` (`ForgeOptions.DeepGitAnalytics`). When off, `_progress.DeepGit` is null,
  `TryComputeDeep`'s extra process never runs (that IS the FR-10 perf guarantee), and the treemap must render
  sized-by-LOC with neutral fill + notice. Never call `TryComputeDeep` yourself — read `_progress.DeepGit`.
  [Source: [SiteGenerator.cs:499-503](../../src/SpecScribe/SiteGenerator.cs)]

### Two path spaces — don't confuse them

- The retired tree keyed off **`_bmad-output`-relative `*.md`** paths and linked via the `_docs`
  (source→output-href) map. The treemap keys off **repo-relative source-code** paths (`src/SpecScribe/Foo.cs`) —
  the same space git's `--numstat` emits, so LOC and git metrics join cleanly. The `_docs` href map is irrelevant
  here; code-page links come from the guarded `fileHref` resolver (dormant until Story 7.1). Normalize every path
  through [PathUtil.NormalizeSlashes](../../src/SpecScribe/PathUtil.cs:10) and compare `OrdinalIgnoreCase`
  (Windows/Git-Bash parity) — the same discipline the old builder used.

### The zero-JS exception — adopted, but strictly bounded

- SpecScribe's charts are deliberately pure SVG + links, no JS — with two admitted small scripts already (the
  mobile-nav toggle and the tooltip/copy helper). Zoom/drill + live dimension-switch realistically need JS, so
  this story **adopts the SCP-recommended ruling**: a **scoped, progressively-enhancing** JS module, admitted as a
  bounded exception. **Everything is correct and usable with JS off** (server-rendered treemap + default color +
  text-equivalent table + focus tooltips); JS only enhances zoom + dimension-switch. Mirror the sunburst's drill +
  URL-hash-state conventions; do NOT hand-roll a new interaction model. If the Architect (Winston) has not yet
  formally ruled, this is the recommended path and is safe to build — but flag it at review. [Source: [SCP §3](../planning-artifacts/sprint-change-proposal-2026-07-08.md); [[charting-is-pure-svg-no-js]]]

### Color, motion, tooltips, a11y — reuse the established seams

- **Color:** non-`--status-*` sequential ramp (structure isn't a lifecycle stage). Reuse the commit-heatmap ramp
  tokens/levels. [Source: [[specscribe-status-token-system]]]
- **Motion:** route the zoom transition through the `--motion-*` tokens + the two paired reduced-motion blocks
  (Story 3.5); the reduce branch snaps. [Source: [[motion-token-system]]]
- **Tooltips:** body-level `js-tip`/`data-tip` node, never a CSS `::after` on an SVG rect (it clips in the panel).
  [Source: [[tooltip-clipping-use-ss-tooltip-node]]]
- **a11y:** focusable rects with descriptive `aria-label`s (mirror the sunburst `ariaLabel` UX-DR7), shared
  `:focus-visible` ring (never `outline:none` without a replacement), color never the sole signal (text table +
  tooltip text). [Source: [[story-1-4-a11y-seams-for-1-5]]; NFR6]

### Architecture & structure conventions (non-negotiable)

- **No `architecture.md`.** The governing docs are the spec kernel
  ([SPEC.md](../../_bmad-output/specs/spec-specscribe/SPEC.md),
  [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)). The monolithic
  `src/SpecScribe` single project is intentional — do NOT introduce the aspirational `IInsightProvider` /
  `SpecScribe.Core` split. Add `CodeMap.cs` / `CodeMapTemplater.cs` in place, matching `WorkInventory` /
  `GitInsightsTemplater`. [Source: [ARCHITECTURE-SPINE.md#Seed-Not-Invariant](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:98)]
- **Insight providers are additive and never own baseline success (AD-4).** The whole code-map gather+build is
  wrapped never-throw → `CodeMap.Empty`; any failure omits the surface and generation still succeeds. [Source:
  [ARCHITECTURE-SPINE.md#AD-4](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:58); NFR2]
- **Pure model / IO in the generator.** `CodeMap.Build` and the squarified layout are pure functions over
  already-gathered inputs (paths+LOC+metrics) — unit-testable without a repo. `SiteGenerator` owns the disk walk,
  line-counting, and the git-metric read. Same split as `ProgressCalculator` / `WorkInventory` / `ArtifactCoverage`.
- **Output invariance:** `OrdinalIgnoreCase` ordering, `InvariantCulture` any number/date formatting (culture-
  sensitive parses corrupt dates under non-Gregorian calendars — the reason `GitMetrics`/`Charts` are invariant).
- **Default generate output dir is `SpecScribeOutput`** — never `docs/live` (vestigial/gitignored). Edit only the
  embedded `src/SpecScribe/assets/specscribe.css`. [Source: [[generate-output-dir-is-specscribeoutput]]]
- **Watch mode:** the treemap changes on file add/rename/delete AND on content edits (LOC changes), but like the
  retired tree it's acceptable in v1 to regenerate it only on the full-rebuild path (topology change), not on every
  single-file content edit — do not add incremental treemap regeneration. [Source: [ARCHITECTURE-SPINE.md#AD-5](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:66)]

### Testing standards

- xUnit tests under `tests/SpecScribe.Tests`. Pure-model tests take literal inputs (no disk, no repo) and assert
  exact deterministic output — mirror [WorkInventoryTests](../../tests/SpecScribe.Tests/WorkInventoryTests.cs) and
  the existing `ParseNumstatLog`/`BuildInsights` tests. Migrate (don't orphan) every retired-3.4 test against the
  new model/renderer/nav/glyph/CSS. Run the FULL suite; it was green at 502 tests when 3.4 landed — keep it green.

### Project Structure Notes

- **New:** `src/SpecScribe/CodeMap.cs` (model + squarified layout), `src/SpecScribe/CodeMapTemplater.cs` (page),
  `tests/SpecScribe.Tests/CodeMapTests.cs`.
- **Renamed/replaced:** `ProjectTree.cs`→`CodeMap.cs`; `ProjectTreeTests.cs`→`CodeMapTests.cs`;
  `SiteGeneratorStructureTests.cs`→ code-map generation tests.
- **Modified in place:** `GitMetrics.cs` (`BuildCodeMapMetrics` + `CodeFileMetrics` + `DeepGitPulse.CodeMapMetrics`),
  `Charts.cs` (treemap SVG builder replaces `ProjectStructureTree`), `SiteNav.cs` (`CodeMapOutputPath`/`HasCodeMap`/
  `hasCodeMap`/"Code Map" nav+quick-link), `Icons.cs` (re-key glyph), `SiteGenerator.cs` (`EnumerateCodeFiles` +
  `WriteCodeMap` + gate, delete `WriteStructure`/`BuildStructureHrefMap`/`RenderStructurePage`),
  `assets/specscribe.css` (treemap styles replace structure-tree styles), and `ChartsTests`/`SiteNavTests`/
  `IconsTests`/`StylesheetTests`.
- Single-project layout — no new folders, no new project.

### References

- [Source: [epics.md#Story 7.6 (lines 966-997)](../planning-artifacts/epics.md)] — user story + the 4 ACs.
- [Source: [epics.md#FR14 (line 45)](../planning-artifacts/epics.md)] — "source-code treemap … code mass (lines of code) … git-derived change signals."
- [Source: [epics.md#FR15 (line 46)](../planning-artifacts/epics.md), [#FR19 (line 50)](../planning-artifacts/epics.md)] — in-portal code pages (drill target); advanced code-and-git coverage (the git dimensions).
- [Source: [epics.md#UX-DR19 (line 102)](../planning-artifacts/epics.md)] — treemap sized by LOC, colorable by git signals, rich tooltips, directory drill/zoom + breadcrumb, focusable rects, non-color text equivalent.
- [Source: [sprint-change-proposal-2026-07-08.md](../planning-artifacts/sprint-change-proposal-2026-07-08.md)] — full reinterpretation: §2 technical impact (source walk, LOC, single numstat extension, non-status ramp, zero-JS exception, a11y burden shift), §3 recommended approach + Architect decision, §4E per-file revert/repurpose disposition, Amendment A (re-seated to Epic 7).
- [Source: [3-4-interactive-tree-views…md](3-4-interactive-tree-views-for-project-and-artifact-structure.md)] — the retired disclosure-tree story; its File List + Completion Notes are the revert checklist.
- [Source: [GitMetrics.cs:257-538](../../src/SpecScribe/GitMetrics.cs)] — `TryComputeDeep`/`ParseNumstatRecords`/`ParseNumstatLog`/`BuildInsights`/`DeepGitPulse`: the shared deep-git path to extend (one fetch, one parse, several views).
- [Source: [ProjectTree.cs](../../src/SpecScribe/ProjectTree.cs)] — the trie build + deterministic ordering + single-child-chain collapse to port into `CodeMap`; the `Empty`/`IsEmpty`/pure-`Build`/never-throw shape.
- [Source: [SiteGenerator.cs:44-45 (source enum), :132-181 (deep-git wiring + Write* phase), :499-503 (git pulse + `--deep-git` gate), :696-770 (`WriteSprint`/`WriteStructure`/`RenderStructurePage`), :906-931 (`EnumerateSourceFiles`/`BuildNav`/gate)](../../src/SpecScribe/SiteGenerator.cs)] — where enumeration, gating, and page-write live.
- [Source: [SiteNav.cs:30-55 (StructureOutputPath/HasStructure), :57-64 (Build signature), :117-130 (Sprint/Structure gate), :178 (RenderNavBar Icons.ForConcept auto-prefix)](../../src/SpecScribe/SiteNav.cs)] — the seam to rename to Code Map.
- [Source: [GitInsightsTemplater.cs:19-28, :117, :205](../../src/SpecScribe/GitInsightsTemplater.cs)] — the guarded `Func<string,string?>? fileHref` resolver pattern (no resolver / no target → plain text) to mirror for the dormant 7.1 code-page link.
- [Source: [Charts.cs:6-8 (pure SVG/no-JS class doc), :478-611 (CommitHeatmap ramp + legend), CouplingGraph/CouplingList (generation-time deterministic layout + text equivalent)](../../src/SpecScribe/Charts.cs)] — the pure-render + non-status ramp + text-equivalent conventions.
- [Source: [specscribe.css:953 (status-token guard), :2030-2067 (heatmap sequential ramp + legend)](../../src/SpecScribe/assets/specscribe.css)] — the ramp to reuse; keep off `--status-*`.
- [Source: [ARCHITECTURE-SPINE.md#AD-4 (58), #AD-5 (66), #Seed-Not-Invariant (98)](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — additive/non-blocking providers, watch-rebuild scope, monolith-is-intentional.
- [Source: [[deep-git-single-numstat-path]], [[story-3-4-redefined-as-source-treemap]], [[epic-7-code-link-strategy]], [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[motion-token-system]], [[tooltip-clipping-use-ss-tooltip-node]], [[story-1-4-a11y-seams-for-1-5]], [[generate-output-dir-is-specscribeoutput]], [[funnel-is-sideways-conventional-silhouettes]]] — project memory feeding this story.

### Latest Technical Information

- Stack: **.NET 10** (`net10.0`), C#, xUnit. **No external libraries** — the treemap is pure inline SVG + CSS + a
  scoped vanilla-JS enhancement module, consistent with every existing chart. No package to add.
- **Squarified treemap algorithm:** Bruls, Huizing & van Wijk, "Squarified Treemaps" (2000) — the standard
  aspect-ratio-optimizing tiling. Implement it directly (it's ~40 lines of pure recursion); no dependency needed.
  Deterministic given a deterministic node order → byte-stable snapshot output.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Opus 4.8)

### Debug Log References

- Full suite green: **954 tests passing** (`dotnet test SpecScribe.slnx`). New/migrated Story 7.6 tests: 48
  (BuildCodeMapMetrics ×6, CodeMapTests ×18, CodeTreemap ×5, CodeMapTemplaterTests ×3, SiteGeneratorCodeMapTests
  ×2, SiteNav Code Map ×2, plus the Icons/Stylesheet re-key guards).
- Real-repo generation verified end-to-end (`generate --deep-git`): `code-map.html` = 705 file cells + 201 labeled
  directory rects, every cell colorized by real git metrics (`data-changes`), legend + colorize controls present,
  831-row text-equivalent table (the SVG omits sub-pixel rects below the tiling threshold; the table is the full
  truth), nav "Code Map" link + quick-link description wired. SVG well-formed (11 `<svg>`/11 `</svg>`).
- Golden fingerprint regenerated twice: once for this story's own changes, then re-pinned to
  `1296c595…` after a **concurrent session** landed the `spec-scribes-nib-branding` contrast pass into
  `specscribe.css` (+326) / `prism.js` (+142) mid-run — that shared-asset drift shifts the whole-site hash and is
  outside this story's diff. If it drifts again before review, regenerate the constant (it is a whole-site
  fingerprint, not a code-map defect).

### Completion Notes List

- **Task 1 (data):** `GitMetrics.CodeFileMetrics(Changes, TotalChurn, FirstDate, LastDate)` + pure
  `BuildCodeMapMetrics` (untruncated, one entry per file in the window; once-per-commit-per-file change count;
  churn sums every numstat row incl. binary=0; newest-first date resolution with null-backfill) surfaced as the
  settable `DeepGitPulse.CodeMapMetrics`, populated from the SAME `commits` list `ParseNumstatLog` already parses —
  no second `git log`. Added the thin `GitMetrics.TryListFiles` (`git ls-files`) helper for the source walk.
- **Task 2 (model):** `CodeMap.cs` replaces `ProjectTree.cs` — pure `CodeMapNode`/`CodeMap.Build` (trie + roll-up
  Lines Σ + single-child-chain collapse + `Empty`/`IsEmpty`, ported from the retired trie), plus a pure
  deterministic **squarified** layout (`TreemapRect`, Bruls et al. 2000) computed at generation time.
- **Task 3 (generator):** `SiteGenerator.EnumerateCodeFiles` (git ls-files → bounded dot-dir/build-dir-excluding
  fallback walk; per-file size cap + NUL/UTF-8 guard reusing `TryReadCodeText`/`SplitCodeLines`; cached in
  `_codeFiles`), `WriteCodeMap` replacing `WriteStructure`/`BuildStructureHrefMap`/`RenderStructurePage`, one
  `CodeMapAvailable` gate feeding both `SiteNav.Build(hasCodeMap:)` and the page write; `SiteNav`
  `StructureOutputPath→CodeMapOutputPath`/`HasStructure→HasCodeMap`/`"Structure"→"Code Map"`; `Icons` glyph
  re-keyed to `"Code Map"`.
- **Task 4 (render):** `Charts.CodeTreemap` (server-rendered SVG, one focusable `<rect>` per file with `data-*`
  metrics, default change-frequency fill on the heatmap ramp levels, `js-tip`/`data-tip` rich tooltip,
  `aria-label`, guarded `fileHref` link seam) + `CodeMapTemplater` (shell + always-on legend + hidden colorize
  controls + hidden drill breadcrumb + git-unavailable notice + full text-equivalent table).
- **Task 5 (JS):** scoped enhancement in `specscribe.js` — reveals the hidden controls/breadcrumb, re-fills cells
  by dimension (client bucket mirrors `Charts.Bucket` exactly; dates scaled against the file-set min/max window),
  directory zoom via rAF viewBox tween (snaps under `prefers-reduced-motion`), deep-linkable `#dir=` hash with
  popstate. Everything degrades: JS off ⇒ server treemap + legend + table stand.
- **Task 6 (CSS):** removed the retired `.structure-tree` block; added `.codemap-*` (ramp levels reuse the
  heatmap values, `level-none` neutral for metric-less files, off the `--status-*` tokens), focus rings, controls,
  breadcrumb, legend, table, internal-scroll responsive rule; updated the paired reduced-motion / no-preference
  blocks (`.structure-tree`→`.codemap`).
- **Task 7 (tests):** migrated `ProjectTreeTests`→`CodeMapTests`, `SiteGeneratorStructureTests`→
  `SiteGeneratorCodeMapTests`, the 3 `ProjectStructureTree` Charts tests→`CodeTreemap` tests, `SiteNavTests`/
  `IconsTests`/`StylesheetTests` re-keys, plus new `CodeMapTemplaterTests`; regenerated the golden inventory
  (structure.html→code-map.html) + content fingerprint.
- **Deviation flagged for review (Subtask 4.5):** the story (written before Story 7.1 merged) said pass
  `fileHref: null`. Story 7.1/7.2 code pages + the guarded `CodePageHref`/`CodeItemHref` resolvers now DO exist on
  this branch, so AC #3's "routes to its code page when available" could be satisfied by wiring the existing
  guarded resolver into `WriteCodeMap`. I followed the story literally (`fileHref: null`, seam dormant) to honor
  "NO DEVIATION"; **owner decision:** wire `CodeItemHref` here as a fast follow if the code-page linkthrough is
  wanted (guarded → uncited files stay plain, no broken links).

### File List

- **Added:** `src/SpecScribe/CodeMap.cs`, `src/SpecScribe/CodeMapTemplater.cs`,
  `tests/SpecScribe.Tests/CodeMapTests.cs`, `tests/SpecScribe.Tests/CodeMapTemplaterTests.cs`,
  `tests/SpecScribe.Tests/SiteGeneratorCodeMapTests.cs`
- **Deleted:** `src/SpecScribe/ProjectTree.cs`, `tests/SpecScribe.Tests/ProjectTreeTests.cs`,
  `tests/SpecScribe.Tests/SiteGeneratorStructureTests.cs`
- **Modified:** `src/SpecScribe/GitMetrics.cs` (`CodeFileMetrics` + `BuildCodeMapMetrics` +
  `DeepGitPulse.CodeMapMetrics` + `TryListFiles`), `src/SpecScribe/CodeMap.cs` (new), `src/SpecScribe/Charts.cs`
  (`CodeTreemap`/`Bucket` replace `ProjectStructureTree`/`AppendTreeNode`), `src/SpecScribe/SiteNav.cs`
  (Structure→Code Map seam), `src/SpecScribe/Icons.cs` (glyph re-key), `src/SpecScribe/SiteGenerator.cs`
  (`EnumerateCodeFiles`/`FallbackCodeWalk`/`WriteCodeMap`/`_codeFiles` gate; removed `WriteStructure`/
  `BuildStructureHrefMap`/`RenderStructurePage`), `src/SpecScribe/assets/specscribe.css` (treemap styles),
  `src/SpecScribe/assets/specscribe.js` (treemap enhancement module), `tests/SpecScribe.Tests/GitMetricsTests.cs`,
  `tests/SpecScribe.Tests/ChartsTests.cs`, `tests/SpecScribe.Tests/SiteNavTests.cs`,
  `tests/SpecScribe.Tests/IconsTests.cs`, `tests/SpecScribe.Tests/StylesheetTests.cs`,
  `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` (golden inventory + fingerprint)

### Change Log

- 2026-07-12 — Story 7.6 implemented: replaced the retired Story 3.4 artifact structure tree with a source-code
  treemap (`code-map.html`) sized by lines of code and colorized by git-derived change signals, with directory
  zoom, dimension-switch, rich tooltips, a full text-equivalent table, and graceful degradation (git-off / no-JS).
  Extended the single deep-git parse with an untruncated per-file metric view; reverted the disclosure model,
  renderer, CSS, and tests. 954 tests green. Status → review.
