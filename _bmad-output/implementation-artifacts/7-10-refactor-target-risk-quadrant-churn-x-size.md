---
baseline_commit: b87a47eadf8ef499888f62d4bf0b8597ca3ea9a5
---

# Story 7.10: Refactor-Target Risk Quadrant (Churn × Size)

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a tech lead deciding where to invest cleanup,
I want files plotted by how often they change against how large they are,
so that the high-churn, high-size quadrant surfaces refactor targets instead of me guessing.

## Acceptance Criteria

1.
**Given** deep-git numstat change-frequency data and per-file size already computed
**When** the quadrant renders
**Then** each file is a point on change-frequency × size axes with the high/high quadrant visually flagged as elevated risk
**And** points link to their code page via the Story 7.2 seam, with a Story 10.2-compliant legend, axes, and framing sentence. [Source: epics.md#Story 7.10 (lines 1479-1483); FR19]

2.
**Given** a shallow or non-git repo, or a repo too small to be meaningful (NFR8)
**When** the underlying data is thin
**Then** the chart is omitted or shows a designed empty state rather than an axis of one dot
**And** "complexity" remains a **size proxy only** — this story does not add a cyclomatic-complexity analyzer; a real complexity metric would be a separate story. [Source: epics.md#Story 7.10 (lines 1485-1489); NFR8]

---

## Developer Context

**Read this first — this is a new-chart-type rendering story on the Code Map page, not a new git computation.** Both axes' data already exist and are already joined per-file: **size** (lines of code) is `CodeMapNode.Lines`, the exact key `CodeMap`/`Charts.CodeTreemap` (Story 7.6) already uses; **churn frequency** is `CodeMapNode.Metrics?.Changes`, sourced from `GitMetrics.CodeFileMetrics` (Story 7.6/7.9's `--deep-git` join). `CodeMap.Build` already joins size↔metrics by repo-relative path (`CodeMap.cs:150-153,180`), and `CodeMap.Files()` (`CodeMap.cs:298-311`) already hands back a flat, deterministic list of `CodeMapNode` — each carrying `.RepoRelativePath`, `.Lines`, `.Metrics` — for every file. **This is the one and only data source for both axes.** There is no new fetch, no new parse, and (per the epic's explicit constraint) no cyclomatic-complexity analyzer: "size" means lines of code, nothing else, both now and in this story's scope.

The three highest risks are: **(1)** re-deriving size or churn instead of consuming `CodeMap.Files()`'s already-joined `(Lines, Metrics)` pairs (there is **no** new git call and **no** new line-counting pass in this story — `CodeMap.Build`'s source-file walk and `GitMetrics.BuildCodeMapMetrics` both already exist and are already wired into `SiteGenerator.WriteCodeMap`); **(2)** rendering "an axis of one dot" on a thin repo — this codebase has **no existing shared "too few files to be meaningful" threshold** (confirmed: `GitMetrics.cs` and `CodeMap.cs` degrade per-file — null `Metrics` → neutral fill — or per-page — zero files → whole page omitted — never a minimum-N-for-a-chart-to-be-meaningful gate), so this story must define and document its own small constant, the way `RefGraphArtifactNodeCap`/`FileInsightCoupledCap` were each introduced by the story that needed them; **(3)** building a genuinely new SVG chart type — **no scatter/XY-plot/quadrant builder exists anywhere in `Charts.cs` today** (confirmed via full-file read), so you are writing new rendering code, not extending an existing shape. The closest analogs to model it on are `Charts.CodeTreemap` (domain: per-file rects, size + churn dimensions, `data-tip-html` tooltip card, guarded `fileHref` link pattern — [Charts.cs:2154-2299](../../src/SpecScribe/Charts.cs)) and `Charts.CouplingGraph` (geometry: arbitrary computed XY point placement, `role="img"` SVG, per-point `<title>`, guarded `<a>` wrap — [Charts.cs:1696-1780](../../src/SpecScribe/Charts.cs)).

### Proposed visual design (create-story judgment call — confirm/adjust at dev-story or review; not owner-elicited this session)

No owner elicitation happened for this story's visual (the standing "elicit visual intent" directive — [[create-story-elicit-visual-intent]] — normally applies at create-story time, but this run is fully automated per workflow config). The following is a reasoned default, not a locked contract; treat it as a strong starting point the dev agent may refine, and flag anything that reads wrong for review:

- **A pure-SVG scatter plot**, one point per source file that has `Metrics is not null` (files with no git history plot nowhere — there's nothing to place them at on the churn axis). X axis = **size** (`Lines`, log-scaled — file sizes are heavy-tailed, and a linear scale would crush every file into the left edge behind a handful of huge outliers). Y axis = **churn frequency** (`Metrics.Changes`, the "commits touching this file" count — see the "which churn signal" note below — linear scale is fine here since the range is much tighter).
- **Quadrant split at the median** of each axis (median size, median churn among the plotted files) — median is robust to the same heavy-tailed distribution that motivates the log X-scale, and needs no configuration. The **top-right (high size, high churn) quadrant is the "elevated risk" quadrant** (AC #1) — shade its background rect in a light, low-saturation tone from the existing `--rust`/attention family (never a new hue — [[specscribe-status-token-system]] governs lifecycle tokens specifically, but the broader "don't invent ad-hoc hues" discipline in this codebase applies to any new accent too) with a text label inside the shaded region ("Elevated risk" or similar) so the flagging is never color-only.
- **Points**: small filled circles, guarded `<a>` wrap when `CodeItemHref` resolves a code page (never a dead link — Story 7.2 seam), each with a `<title>` tooltip = full path + "N changes · M lines" (mirrors `CouplingGraph`'s `<title>` pattern). Points inside the elevated-risk quadrant get a **second CSS class** (not just the shaded background) so they're identifiable by shape/stroke as well as position — e.g. a heavier stroke or a small ring — consistent with this project's "never color/position alone" a11y discipline for flags.
- **Text equivalent** (mandatory, matching how every other chart on this site pairs a visual with a text/table equivalent — `CodeTreemap` + `AppendFileTable`, the graph + sr-only `.ref-list` in Story 7.8, etc.): a small ranked list/table under the chart of the files actually in the elevated-risk quadrant (path, size, churn), linked the same guarded way. If the quadrant is empty, say so plainly ("No files currently fall in the high-churn, high-size quadrant") rather than omitting the list silently.
- Legend: axis labels ("Lines of code (log scale)" / "Changes in the analyzed window"), a swatch explaining the shaded quadrant, and the Story 10.2 `Why` framing sentence via a new `ChartMetric` case (see below) — all through `Charts.Framed`/`FrameWhySlot`, not hand-rolled copy.

### Which churn signal is "change-frequency" (a real ambiguity — resolve it explicitly)

`GitMetrics.CodeFileMetrics` (`GitMetrics.cs:98`) carries **two** distinct churn signals: `Changes` (commits touching the file, once per commit — a **frequency** count) and `TotalChurn` (Σ added+deleted lines across those commits — a **volume** count). The epic's AC #1 says "change-frequency × size," which maps cleanly to `Changes`, not `TotalChurn` — use `Changes` for the Y axis. Don't derive a third "avg change size" metric for this story; that's `TotalChurn / Changes` and is already surfaced elsewhere (the Code Map file table). If you find yourself wanting `TotalChurn`, re-read the AC — it says frequency, not volume.

### What already exists (reuse / consume — do NOT rebuild)

- **The joined (size, churn) data per file: `CodeMap.Files()`.** Already returns every file's `CodeMapNode` with `.Lines` and `.Metrics` (nullable `CodeFileMetrics`) already joined by `CodeMap.Build` (`CodeMap.cs:150-153,180`). This is precisely both axes' source data, already computed, already deterministic. **Consume it; do not re-walk source files or re-parse git.** [Source: [CodeMap.cs:13-19](../../src/SpecScribe/CodeMap.cs) (`CodeMapNode`), [CodeMap.cs:298-311](../../src/SpecScribe/CodeMap.cs) (`Files()`); [[deep-git-single-numstat-path]]]
- **The churn data: `GitMetrics.CodeFileMetrics`/`BuildCodeMapMetrics`.** Already produces `(Changes, TotalChurn, FirstDate, LastDate, AvgCoChanged)` per file from the single shared `--deep-git` numstat parse, joined into `DeepGitPulse.CodeMapMetrics` and from there into `CodeMap` nodes. [Source: [GitMetrics.cs:98](../../src/SpecScribe/GitMetrics.cs) (`CodeFileMetrics`), [GitMetrics.cs:918-971](../../src/SpecScribe/GitMetrics.cs) (`BuildCodeMapMetrics`); [[deep-git-single-numstat-path]]]
- **The page and its already-built variants: `CodeMapTemplater.RenderPage`/`SiteGenerator.WriteCodeMap`.** The Code Map page (`code-map.html`) already computes all four filter variants once per generation (`CodeMap.BuildVariants`) and gates the whole page on the unfiltered `"full"` variant being non-empty. This story's chart is **one new section on this existing page**, sourced from `full.Map.Files()` — the same unfiltered set the page's headline stats already use — not a new page, not per-filter-variant duplication (the epic AC has no per-filter requirement; the treemap's own checkboxes are a Story 7.6 concern, orthogonal to this chart). [Source: [CodeMapTemplater.cs:23-70](../../src/SpecScribe/CodeMapTemplater.cs) (`RenderPage`), [SiteGenerator.cs:2759-2782](../../src/SpecScribe/SiteGenerator.cs) (`WriteCodeMap`)]
- **The guarded code-page resolver: `CodeItemHref` (already the `fileHref` parameter `CodeMapTemplater.RenderPage` receives).** Resolves a repo-relative path to its in-portal `code/<path>.html` page (or an external source URL fallback), returning `null` when neither exists — never a dead link (Story 7.2 seam). `RenderPage` already receives this as its `fileHref` parameter from `WriteCodeMap`'s call site; thread the same delegate into your new section, don't invent a second resolver. [Source: [SiteGenerator.cs:1341-1366](../../src/SpecScribe/SiteGenerator.cs) (`CodePageHref`/`CodeItemHref`), [SiteGenerator.cs:2781](../../src/SpecScribe/SiteGenerator.cs) (call site)]
- **The Story 10.2 chart-metadata standard: `Charts.ChartMeta`/`Framed`/`WhyText`/`ChartMetric`.** Every framed chart on this site carries Title/Window/Ranking/Why/Note through `Charts.Framed`, so a new chart inherits the standard by construction rather than hand-rolled copy. The three existing `ChartMetric` cases (`ActivityCadence`/`FileChurn`/`ChangeCoupling`) don't quite fit "size × churn combined" framing — add a new case (e.g. `ChartMetric.RefactorRisk`) with its own `WhyText`, matching how sibling Story 7.11 is independently adding `ChartMetric.AuthorConcentration` for its own framing need. **Coordinate on the enum:** if 7.11 lands first and adds its case, add yours as an additive new case, not a reordering — the `switch` in `WhyText` throws `ArgumentOutOfRangeException` on an unmapped value (`Charts.cs:45`), so a missing case fails loudly at render time, not silently. [Source: [Charts.cs:13-92](../../src/SpecScribe/Charts.cs)]
- **The nearest chart-type analogs for the new SVG builder.** `Charts.CodeTreemap` ([Charts.cs:2154-2299](../../src/SpecScribe/Charts.cs)) for the domain (per-file rects sized/colored by size+churn, guarded link, rich tooltip card) and `Charts.CouplingGraph` ([Charts.cs:1696-1780](../../src/SpecScribe/Charts.cs)) for the geometry (arbitrary computed XY placement, `role="img"`, per-point `<title>`, `<a>`-wrap-when-linked). Follow their idioms: `F(double)` (`Charts.cs:2748`) for every SVG coordinate, `Html(string)` (`Charts.cs:2754`) for every escaped string, `Plural(int, string, string)` (`Charts.cs:2752`) for count words.
- **The 5-level heat-ramp token family** (`.codemap-cell.level-0..4`/`.codemap-legend-swatch.level-0..4`, `.heatmap-cell.level-0..4` — the same gold/parchment ramp used at 3+ existing surfaces) is this project's one shared "intensity" palette. If the quadrant chart wants any color gradation beyond the flat elevated-risk shading (it doesn't strictly need to — a flat two-tone quadrant split is simpler and sufficient for AC #1), reuse this family rather than inventing a new one. [Source: [specscribe.css](../../src/SpecScribe/assets/specscribe.css) — `level-0..4` classes]

### Scope boundary (read carefully)

- **IN scope:**
  - A new pure-SVG scatter/quadrant chart builder in `Charts.cs` plotting size (X) × churn frequency (Y) per file, with the high/high quadrant visually flagged.
  - A documented, local "too few files to be meaningful" constant (this story's own threshold — none exists to reuse) gating the chart to an omitted/empty state per AC #2.
  - Guarded clickthrough to code pages (`CodeItemHref`), rich tooltips, Story 10.2-compliant framing (new `ChartMetric` case + `WhyText`), a text-equivalent ranked list of the elevated-risk quadrant's files.
  - Wiring the new section into the existing Code Map page (`CodeMapTemplater.RenderPage`), sourced from the unfiltered `"full"` variant's `Files()`.
  - CSS for the new chart (points, quadrant shading, legend) using existing token families.
  - Graceful degradation: no deep-git / too few metric-bearing files → chart omitted or a designed empty state (never "an axis of one dot").
- **OUT of scope:**
  - **Any new git call, fetch-format change, or parse.** Consume `CodeMap.Files()` / `GitMetrics.CodeFileMetrics`. [[deep-git-single-numstat-path]]
  - **A cyclomatic-complexity analyzer or any other non-LOC size metric** (explicit AC #2 boundary — "size" stays lines of code, a proxy, not a real complexity measure).
  - **Per-filter-variant duplication** (exclude-spec-dev / exclude-tests checkboxes) — the epic AC does not ask for this; source from the unfiltered `"full"` variant only, matching how the page's headline stats already work. If this later turns out wrong, that's a follow-up story, not scope creep here.
  - **Touching `GitMetrics.CodeFileMetrics`/`BuildCodeMapMetrics`** to add a new field — both axes' data already exists on the record as-is.
  - **Any JavaScript.** Pure HTML/CSS + inline SVG, like every other chart on this site. [[charting-is-pure-svg-no-js]]
  - **`--status-*` lifecycle tokens** for the quadrant shading — files don't carry a workflow status; use the neutral/attention accent family instead. [[specscribe-status-token-system]]
  - Story 7.11 (ownership/bus-factor) and Story 7.12 (freshness map) — sibling stories in the same append-only backlog note, extending the same `GitMetrics` deep-git path but rendering elsewhere (Git Insights hub / a recency shading). Don't absorb their scope; only coordinate on the shared `ChartMetric` enum if landing concurrently.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Consume `CodeMap.Files()` from the Code Map page's unfiltered `"full"` variant** — the same `variants` list `CodeMapTemplater.RenderPage` already receives. No new computation, no new SiteGenerator wiring beyond passing the already-available `fileHref` through to your new section. [Source: [CodeMapTemplater.cs:23-70](../../src/SpecScribe/CodeMapTemplater.cs)]
- **Use `Metrics.Changes` for churn frequency (Y axis), `Lines` for size (X axis).** Plot only files where `Metrics is not null` (a file with no deep-git record has no churn coordinate to place). [Source: [GitMetrics.cs:98](../../src/SpecScribe/GitMetrics.cs)]
- **Define and document a local minimum-plottable-files constant** (e.g. `Charts.RiskQuadrantMinFiles`, a small number like 5-8 — pick and document your own value; there is no existing NFR8 threshold to inherit) gating AC #2's degradation. Below the threshold → omit the chart section entirely or render a clearly-labeled empty state; never plot 1-2 points on live axes.
- **Median-split both axes to define the four quadrants**, flag the high-size/high-churn quadrant as elevated risk. Shade that quadrant's background AND give its points a distinguishing class (never position/color alone — this project's a11y convention).
- **Guard the clickthrough (AC #1).** A point is a link only when `fileHref(path)` (the `CodeItemHref` resolver already threaded into `RenderPage`) returns a target; otherwise a plain, non-link, still-tooltipped point. Never a dead link. [Source: [SiteGenerator.cs:1341-1366](../../src/SpecScribe/SiteGenerator.cs)]
- **Rich tooltips.** Each point's `<title>`/summary: full repo-relative path + size + change count, e.g. `"src/SpecScribe/SiteGenerator.cs — 2,840 lines, 47 changes"`. Use `Charts.Plural` for count words.
- **Route all chrome through `Charts.Framed`/`ChartMeta`.** Add a new `ChartMetric` case with its own `WhyText` (framework-neutral copy per NFR8 — no project-specific wording). [Source: [Charts.cs:13-92](../../src/SpecScribe/Charts.cs)]
- **Provide a text-equivalent ranked list** of the elevated-risk quadrant's files (path, size, churn), guarded-linked the same way as the chart points. Empty risk quadrant → say so plainly, don't omit the list silently.
- **"Size" stays lines of code, explicitly documented as a proxy, never a real complexity metric** (AC #2). Say so in a code comment at the builder's declaration so a future reader doesn't "improve" it into a complexity analyzer without a new story.
- **Log-scale the size axis** (file-size distributions are heavy-tailed); linear is fine for the churn axis (tighter range). Guard the log transform against zero/near-zero `Lines` (shouldn't occur — every source file has ≥0 lines, but a defensive `Math.Max(lines, 1)` before `Math.Log` avoids a `-Infinity`/NaN edge case).
- **Escape everything.** Paths and tooltip text are free-text/injection surfaces → `Html(...)` on every derived string, mirroring `CouplingGraph`/`CodeTreemap`.
- **Keep it pure SVG + CSS, no JS.** [[charting-is-pure-svg-no-js]]
- **Degrade non-fatally (AC #2, NFR2).** No deep-git → every `Metrics` is null → zero plottable files → chart omitted/empty state, rest of the Code Map page renders unaffected. Below the minimum-files threshold → same. A file with metrics but no code page → non-link point (never a dead link, never an exception).
- **Deterministic layout** (same input → same SVG) so golden/parity fixtures stay stable — no randomness, no wall-clock in placement or the median calculation.

### DON'T

- **DON'T add a git call, change the fetch format, or re-parse.** Consume `CodeMap.Files()`/`GitMetrics.CodeFileMetrics` as-is. [[deep-git-single-numstat-path]]
- **DON'T build a cyclomatic-complexity analyzer or any non-LOC size signal.** Out of scope per AC #2; a real complexity metric is a separate future story.
- **DON'T reuse `TotalChurn` (volume) where the AC asks for change-**frequency** (`Changes`).** They're different fields on the same record — pick `Changes` deliberately, don't default to whichever is more convenient.
- **DON'T duplicate the chart per filter variant** (exclude-spec-dev/exclude-tests). Source from `"full"` only, unless you find a concrete reason the epic AC requires otherwise (it doesn't, as written).
- **DON'T invent a second code-page resolver.** Thread the existing `fileHref`/`CodeItemHref` delegate through.
- **DON'T flag the elevated-risk quadrant by color alone.** Shape/stroke/label must also distinguish it (this project's standing a11y convention — see Story 7.8's shape+edge precedent).
- **DON'T use `--status-*` lifecycle tokens** for the risk shading — files aren't workflow artifacts. [[specscribe-status-token-system]]
- **DON'T add JavaScript.** [[charting-is-pure-svg-no-js]]
- **DON'T force the Core/Adapters package split** — extend `Charts`/`CodeMapTemplater`/`specscribe.css` in place. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **DON'T use `--output docs/live`** in any manual-verify step — it's vestigial/gitignored; default is `SpecScribeOutput/`. [[generate-output-dir-is-specscribeoutput]]
- **DON'T silently reorder or remove another `ChartMetric` enum case** if Story 7.11's `AuthorConcentration` case has landed concurrently on `main` by the time you branch — add yours additively.

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md)]:

- **AD-4 — optional insight providers are additive, non-blocking, never own baseline success.** The quadrant chart is opt-in (`--deep-git`) and additive: absence (or thin data) yields an omitted section, not a broken Code Map page. [#AD-4; AC #2 degradation]
- **NFR8 — insight surfaces degrade gracefully, absent not broken or misleadingly empty, when data is thin.** This is the direct source of AC #2's "omitted or a designed empty state rather than an axis of one dot" requirement.
- **Local-only, read-only.** No new reads or writes; output stays under `OutputRoot`. [Inherited Invariants]
- **Seed, not invariant.** Extend `Charts`/`CodeMapTemplater` in place; no `IInsightProvider`/package split. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **Pure-SVG / no-JS.** New builder follows the `CouplingGraph`/`CodeTreemap` idiom. [[charting-is-pure-svg-no-js]]
- **FR31 / generation-time determinism.** Median split, log-scale transform, and point placement must all derive purely from the already-fetched `CodeMap.Files()` data — no wall-clock "now," identical output across regeneration runs.

### Delivery-adapter note (does this touch webview/SPA parity?)

The Code Map page is a synthesized page built directly by `CodeMapTemplater` (like `CommitDayTemplater`/`CodeFileTemplater`), **not** routed through `HtmlRenderAdapter.RenderPage`/the `IRenderAdapter` view-model path — so this change does **not** add a `HostRenderException` or a RenderParity registry exception, and doesn't touch the webview/SPA body seams. It **does** change `specscribe.css` bytes and the `code-map.html` body, so the **golden content fingerprint** will shift — regenerate it with the standard normalizations after implementation. [Source: [7-8 story](7-8-related-files-in-the-reference-graph.md) — same delivery-adapter shape; [[golden-diff-normalization-gotchas]]]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **No new git library, no git call.** The one git seam (`GitMetrics.TryComputeDeep`) is untouched. [[deep-git-single-numstat-path]]
- **Existing infra to reuse (do not reinvent):**
  - [CodeMap.cs:298-311](../../src/SpecScribe/CodeMap.cs) — `CodeMap.Files()`, the joined (path, Lines, Metrics) source.
  - [GitMetrics.cs:98](../../src/SpecScribe/GitMetrics.cs) — `CodeFileMetrics` (`Changes` is the churn-frequency field to use).
  - [Charts.cs:1696-1780](../../src/SpecScribe/Charts.cs) — `CouplingGraph`, the closest existing free-XY-point SVG builder to model the new one on.
  - [Charts.cs:2154-2299](../../src/SpecScribe/Charts.cs) — `CodeTreemap`, the closest existing size+churn-per-file builder (tooltip card pattern, guarded link pattern).
  - [Charts.cs:13-92](../../src/SpecScribe/Charts.cs) — `ChartMeta`/`Framed`/`WhyText`/`ChartMetric` (Story 10.2 standard — add a new enum case).
  - [Charts.cs:2748,2752,2754](../../src/SpecScribe/Charts.cs) — `F`/`Plural`/`Html` formatting/escaping helpers.
  - [CodeMapTemplater.cs:23-70](../../src/SpecScribe/CodeMapTemplater.cs) — `RenderPage`, where the new section is wired in.
  - [SiteGenerator.cs:1341-1366](../../src/SpecScribe/SiteGenerator.cs) — `CodeItemHref`, the guarded resolver already threaded as `fileHref`.

---

## File Structure Requirements

**New files:**

- *(None required.)* This extends existing renderers. If the quadrant-math/layout logic grows large, a small private helper struct/method inside `Charts.cs` is fine, but no new production file or output path is expected.

**Modified files (read fully before editing):**

- `src/SpecScribe/Charts.cs` — add the new `RiskQuadrant` (or similarly named) SVG builder: median-split quadrant math, log-scaled X / linear Y point placement, guarded `<a>`-or-plain point rendering, elevated-risk quadrant shading + distinguishing point class, rich `<title>` tooltips, `role="img"` summary `aria-label`, `chart-empty` degrade at/under the documented minimum-files threshold. Add the new `ChartMetric` case + `WhyText`. [Source: [Charts.cs:13-92,1696-1780,2154-2299](../../src/SpecScribe/Charts.cs)]
- `src/SpecScribe/CodeMapTemplater.cs` — add a new section (after the four filtered variant panels, sourced from the unfiltered `"full"` variant's `Files()`) wrapping the chart in `Charts.Framed`, plus the text-equivalent ranked list of the elevated-risk quadrant's files. Preserve all existing panel/table rendering untouched. [Source: [CodeMapTemplater.cs:23-70](../../src/SpecScribe/CodeMapTemplater.cs)]
- `src/SpecScribe/assets/specscribe.css` — add classes for the new chart's points, quadrant shading, and legend (existing neutral/attention/heat-ramp token families only — no new hues). **Edit only** `src/SpecScribe/assets/specscribe.css` (any `docs/live` copy is vestigial). [[generate-output-dir-is-specscribeoutput]]

**Output layout:** No new paths. The new section renders inside the existing `SpecScribeOutput/code-map.html` page; points link to sibling `code/…html` pages via `CodeItemHref`.

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Chart/templater tests call the render methods directly with synthetic inputs; generation-level tests use the temp-`_bmad-output`-tree (+ a temp git repo for `--deep-git`) with `AssertNoErrors(gen.GenerateAll())`. [Source: [ChartsTests.cs](../../tests/SpecScribe.Tests/ChartsTests.cs), [CodeMapTemplaterTests.cs](../../tests/SpecScribe.Tests/CodeMapTemplaterTests.cs), [SiteGeneratorCodeMapTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorCodeMapTests.cs)]

**New `Charts` builder tests (unit, no IO) — add to `ChartsTests.cs`, following the existing `CodeTreemap_*` naming/degradation-test block (~lines 1782-1912):**
- **Points render for every metric-bearing file**, positioned by size (X) and churn (Y); files with `Metrics == null` are excluded from the plot.
- **Elevated-risk quadrant is visually flagged**: given a synthetic set with a clear high-size/high-churn cluster, those points carry a distinguishing class (not just background shading) and the quadrant background rect has its own class.
- **Guarded link**: a point with a resolvable `fileHref` → `<a href=…>`; without → no `<a>`, tooltip/point still render. No dead links.
- **Rich tooltip**: a point's `<title>`/summary includes the full path, line count, and change count.
- **Below-threshold degrade (AC #2)**: fewer than the documented minimum plottable files → `chart-empty`-style output, never a live-axis chart with 1-2 dots.
- **Zero metric-bearing files (deep-git off)**: same degrade as above.
- **Escaping**: a path with `<`/`&`/`"` is escaped.
- **Determinism**: same input twice → byte-identical SVG.

**`CodeMapTemplaterTests.cs` — extend:**
- **Populated, above-threshold data** → the new section renders with the chart + the text-equivalent risk list.
- **Below-threshold / no metrics** → the section is omitted (or shows the designed empty state — pick one behavior and assert it consistently) while the rest of the page (treemap panels, table) is unaffected.
- **Elevated-risk list contents**: a synthetic high-size/high-churn file appears in the ranked list, linked when `fileHref` resolves.

**Generation-level tests (extend `SiteGeneratorCodeMapTests.cs`):**
- **Opt-in on, enough files**: temp git repo + `DeepGitAnalytics = true` with enough distinct files/commits → `code-map.html` includes the new chart section with at least one plotted point resolving to its own code page.
- **Opt-out off / thin repo (AC #2)**: `DeepGitAnalytics = false`, or a repo below the minimum-files threshold → the section degrades cleanly (omitted/empty state), no exception, rest of the page renders.
- **Determinism**: two runs over the same repo produce identical `code-map.html` output (new section included).

**Golden fingerprint:** CSS + `code-map.html` body change → regenerate the golden content fingerprint (`SiteGeneratorAdapterTests.cs`, search `GoldenContentFingerprint`) with the standard normalizations. [[golden-diff-normalization-gotchas]]

**Run:** `dotnet test` from repo root. Then two real passes against this repo (default `SpecScribeOutput/`; **do not** pass `--output docs/live`):
1. **Baseline:** `dotnet run --project src/SpecScribe` (no `--deep-git`) → open `code-map.html` → confirm the quadrant section is omitted/empty-stated, rest of the page unaffected.
2. **Deep:** `dotnet run --project src/SpecScribe --deep-git` → open `code-map.html` → confirm: a real scatter of this repo's own files, elevated-risk quadrant shaded + distinguishable by more than color, guarded links to code pages that exist, rich tooltips, text-equivalent risk list, Story 10.2 chrome (title/why/legend), no JS, escaped content.

---

## Previous Story Intelligence

- **Story 7.8 (Related Files in the Reference Graph, done) — the most recent completed Epic 7 story; a rendering-only, no-new-git-call, new-node-type-on-an-existing-chart pattern to mirror.** Its own retrospective lesson: don't let a rendering-only story move the golden fingerprint unpredictably — regenerate deliberately at the end and verify the diff is scoped to your actual change. Its "two populations distinguished by shape + edge, never color alone" design discipline is the same discipline this story's elevated-risk quadrant should follow (shading alone is not enough). [Source: [7-8 story](7-8-related-files-in-the-reference-graph.md)]
- **Story 7.11 (Code Ownership & Bus-Factor Insights, ready-for-dev, uncommitted in this working tree) — a sibling story from the same 2026-07-19 correct-course batch, useful as a live template for this exact create-story pattern.** It independently adds a new `ChartMetric` case (`AuthorConcentration`) for its own framing need — the same move this story makes (`RefactorRisk` or similar) — so **coordinate on the enum** if both land close together (additive cases only, no reordering). Its dev notes also confirm: `--deep-git`-gated pages need no *new* gating logic beyond the existing page-level gate; a per-file "flag" should reuse existing vocabulary/tone rather than inventing a bespoke one; and NFR8 solo/thin-repo handling is a rendering branch, not a new detection mechanism to build from scratch. [Source: [7-11 story](7-11-code-ownership-and-bus-factor-insights.md)]
- **Story 7.6 (Source Code Treemap, done) and Story 7.9 (Code Map File-Type Colorize, backlog) — the page this story extends.** 7.6 built `CodeMap`/`CodeMapTemplater`/`Charts.CodeTreemap` and the size-as-`Lines` convention this story's X axis reuses. 7.9 (not yet implemented as of this story's creation) will add a discrete file-type colorize dimension to the *treemap* — orthogonal to this story's *new, separate* scatter chart; don't conflate the two, and don't wire this story's quadrant into the treemap's `AppendColorizeControls` dropdown. [Source: [CodeMapTemplater.cs](../../src/SpecScribe/CodeMapTemplater.cs)]
- **Recurring lessons to apply:** escaping and stale-output are this renderer family's two most common regressions; keep layout deterministic for golden/parity; grep in-flight/recent story files for stale repeated commands (`--output docs/live`) before closing; extend the monolith in place (no package split). [Source: [7-4 story](7-4-advanced-code-and-git-coverage.md); project memory]

## Git Intelligence Summary

Recent commits (`b87a47e`, `d274cee`, `cb9fc4b`, `1edc996`, `f0f30bd`) are small, surgical diffs to individual templater/renderer files plus paired golden-fingerprint regeneration and targeted `*Tests.cs` additions — no large refactors. Follow the same shape: touch `Charts.cs` + `CodeMapTemplater.cs` + `specscribe.css` + their test files + the golden fixture, nothing else. The working tree currently has **uncommitted, unrelated changes** to `HtmlRenderAdapter.cs`/`SiteGeneratorAdapterTests.cs`, plus the just-created 7.11 story file and sprint-status/epics edits from this create-story session's own SCP batch — none of these overlap this story's file list, but be aware the tree is not clean when you branch, and **`main` has a background auto-committer** ([[worktree-edits-must-target-worktree-path]]).

## Latest Technical Information

No external libraries or APIs are introduced — nothing to version-check. Platform notes:
- **Native SVG `<title>` is the tooltip** — no JS, no library; reuse the same idiom `CouplingGraph`/`CodeTreemap` already use.
- **Log-scale transform**: `Math.Log(Math.Max(lines, 1))` mapped linearly onto the SVG X range is sufficient — no need for a charting library's log-axis machinery; this project has none and shouldn't gain one for a single chart.
- **Median** of a small in-memory list: sort + middle-element(s) is adequate; no statistics package needed (`ImplicitUsings`/BCL only).

## Project Context Reference

- FR19 (Epic 7 advanced code-and-git coverage); NFR8 (graceful degradation on thin data): [Source: [epics.md:99,181](../../_bmad-output/planning-artifacts/epics.md)]
- Epic 7 goal + Stories 7.10-7.12 grouping note + Story 7.10's ACs: [Source: [epics.md:1235,1466-1489](../../_bmad-output/planning-artifacts/epics.md)]
- The size×churn joined data source: [Source: [CodeMap.cs:13-19,298-311](../../src/SpecScribe/CodeMap.cs)]
- The churn record: [Source: [GitMetrics.cs:98](../../src/SpecScribe/GitMetrics.cs)]
- The chart-metadata standard to route through: [Source: [Charts.cs:13-92](../../src/SpecScribe/Charts.cs)]
- The nearest SVG-builder analogs: [Source: [Charts.cs:1696-1780,2154-2299](../../src/SpecScribe/Charts.cs)]
- The page this extends + its existing gate: [Source: [CodeMapTemplater.cs:23-70](../../src/SpecScribe/CodeMapTemplater.cs), [SiteGenerator.cs:2759-2782](../../src/SpecScribe/SiteGenerator.cs)]
- The guarded code-page resolver: [Source: [SiteGenerator.cs:1341-1366](../../src/SpecScribe/SiteGenerator.cs)]
- Architecture invariants (AD-4, NFR8, local/read-only, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)]
- Project memory: [[deep-git-single-numstat-path]], [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[epic-7-code-link-strategy]], [[golden-diff-normalization-gotchas]], [[generate-output-dir-is-specscribeoutput]], [[create-story-elicit-visual-intent]], [[worktree-edits-must-target-worktree-path]].

---

## Tasks / Subtasks

- [x] **Task 1 — Add the `RiskQuadrant` SVG builder to `Charts.cs` (AC: #1, #2)**
  - [x] Signature roughly `public static string RiskQuadrant(IReadOnlyList<CodeMapNode> files, int width = ..., int height = ..., Func<string, string?>? fileHref = null)` (or an equivalent tuple-based input) — consume `.Lines`/`.Metrics.Changes` directly, filter to `Metrics is not null`.
  - [x] Below the documented `RiskQuadrantMinFiles` threshold (new constant — pick and document a value; no existing threshold to reuse) → return the `chart-empty` degrade markup.
  - [x] Log-scale X (size), linear Y (churn frequency = `Changes`); median-split both axes into four quadrants.
  - [x] Shade the high/high quadrant's background rect + give its points a distinguishing class (never color-only).
  - [x] Per-point: `<a>`-wrap when `fileHref(path)` resolves, plain otherwise (never a dead link); rich `<title>` = path + lines + changes.
  - [x] `role="img"` SVG with a summary `aria-label` (files plotted, files flagged elevated-risk).
  - [x] Add the new `ChartMetric` case (e.g. `RefactorRisk`) + its `WhyText` (framework-neutral, NFR8).
- [x] **Task 2 — Wire the new section into `CodeMapTemplater.RenderPage` (AC: #1, #2)**
  - [x] Add the section after the four filtered variant panels, sourced from the unfiltered `"full"` variant's `Files()`.
  - [x] Wrap in `Charts.Framed` with the new `ChartMeta`.
  - [x] Add the text-equivalent ranked list of elevated-risk-quadrant files (path, size, churn; guarded link); empty risk quadrant → say so plainly, don't omit silently.
  - [x] Below-threshold / no metrics → omit the section or render its designed empty state (pick one, be consistent, document the choice). **Decision: always render the section (title + Why sentence via `Charts.Framed`), swapping only the body between the live scatter and the `chart-empty` note — mirrors `DeepAnalyticsTemplater`'s framed `CouplingGraph` precedent.**
- [x] **Task 3 — Styling (AC: #1)**
  - [x] Add point/quadrant-shading/legend classes to `specscribe.css` (existing neutral/attention/heat-ramp token families only). Verify the elevated-risk distinction reads without relying on color. Update `StylesheetTests` if it asserts class presence. (No existing `StylesheetTests` assertions touched these new classes; none needed updating.)
- [x] **Task 4 — Tests (AC: #1, #2)**
  - [x] `ChartsTests.cs`: point placement, quadrant flagging + distinguishing class, guarded link, rich tooltip, below-threshold/zero-metrics degrade, escaping, determinism.
  - [x] `CodeMapTemplaterTests.cs`: populated section render, below-threshold omission/empty-state, risk-list contents.
  - [x] `SiteGeneratorCodeMapTests.cs`: opt-in-with-enough-data, opt-out/thin-repo degrade, determinism.
  - [x] Regenerate the golden content fingerprint; confirm the diff is scoped to `code-map.html`/CSS.
- [x] **Task 5 — Full generation pass + manual verify (AC: #1, #2)**
  - [x] `dotnet test` green. Baseline generate (no `--deep-git`) → section omitted/empty-stated. Deep generate (`--deep-git`) → real scatter of this repo's files, elevated-risk quadrant shaded + shape/label-distinguished, guarded links, tooltips, text-equivalent list, Story 10.2 chrome, no JS, escaped content.

## Dev Notes

- **This is a new-chart-type rendering story — no git work, no complexity analyzer.** Both axes' data already exist and are already joined per-file on `CodeMapNode` via `CodeMap.Files()`. If you find yourself editing `GitMetrics` or measuring code complexity, stop — both are explicitly out of scope. [[deep-git-single-numstat-path]]
- **`Changes` (frequency), not `TotalChurn` (volume), is the Y axis** — the AC says "change-frequency," and the record has both; pick deliberately.
- **No existing NFR8 threshold to reuse for "too few files to be meaningful."** This story introduces its own small documented constant — say so explicitly in a code comment near the constant, so a future reader doesn't assume it's shared with another surface.
- **Elevated-risk quadrant flagged by more than color** — shading plus a distinguishing point class/shape, matching this project's established a11y discipline (Story 7.8's shape+edge precedent).
- **Text equivalent is mandatory** — a ranked list of the risk quadrant's files, not just an SVG with `<title>` tooltips, matching how every other chart on this site pairs a visual with text.
- **No JS. Neutral/attention tokens only (never `--status-*`). Escape everything. Deterministic layout.** [[charting-is-pure-svg-no-js]] [[specscribe-status-token-system]]
- **Coordinate on `ChartMetric`** if Story 7.11's concurrent `AuthorConcentration` case has landed — additive only.
- **Visual design in this story is a create-story default, not owner-locked** (see "Proposed visual design" above) — flag anything that reads wrong for review rather than treating it as fixed.

### Project Structure Notes

- All changes extend existing renderers: `Charts` (new builder + enum case), `CodeMapTemplater` (new section), `specscribe.css`. No new production file, no new output path, no package restructure (deferred seed, Epics 4/6).
- The Code Map page is synthesized (not `IRenderAdapter`-routed) → no new `HostRenderException`/RenderParity registry exception; only the golden content fingerprint moves.

### References

- [Source: [epics.md:1471-1489](../../_bmad-output/planning-artifacts/epics.md)] — Story 7.10 user story + both ACs.
- [Source: [epics.md:1235,1466-1469](../../_bmad-output/planning-artifacts/epics.md)] — Epic 7 goal, FR19 coverage, Stories 7.10-7.12 shared constraints note.
- [Source: [src/SpecScribe/CodeMap.cs:13-19,298-311](../../src/SpecScribe/CodeMap.cs)] — `CodeMapNode`, `Files()` (the joined size+churn source).
- [Source: [src/SpecScribe/GitMetrics.cs:98,918-971](../../src/SpecScribe/GitMetrics.cs)] — `CodeFileMetrics`, `BuildCodeMapMetrics`.
- [Source: [src/SpecScribe/Charts.cs:13-92,1696-1780,2154-2299](../../src/SpecScribe/Charts.cs)] — chart-metadata standard + the two closest existing SVG-builder analogs.
- [Source: [src/SpecScribe/CodeMapTemplater.cs:23-70](../../src/SpecScribe/CodeMapTemplater.cs)] — the page to extend.
- [Source: [src/SpecScribe/SiteGenerator.cs:1341-1366,2759-2782](../../src/SpecScribe/SiteGenerator.cs)] — `CodeItemHref` resolver + `WriteCodeMap` call site.
- [Source: [7-8-related-files-in-the-reference-graph.md](7-8-related-files-in-the-reference-graph.md)] — nearest-precedent rendering-only story shape (delivery-adapter note, golden-fingerprint handling, shape+edge a11y discipline).
- [Source: [7-11-code-ownership-and-bus-factor-insights.md](7-11-code-ownership-and-bus-factor-insights.md)] — sibling story from the same SCP batch; concurrent `ChartMetric` coordination.
- [Source: [_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — invariants (AD-4, NFR8, local/read-only, seed-not-invariant).
- [[deep-git-single-numstat-path]] / [[charting-is-pure-svg-no-js]] / [[specscribe-status-token-system]] / [[epic-7-code-link-strategy]] / [[golden-diff-normalization-gotchas]] / [[generate-output-dir-is-specscribeoutput]] / [[create-story-elicit-visual-intent]] / [[worktree-edits-must-target-worktree-path]] — project memory.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — no failing builds/tests required debugging beyond one expected `int`/`double` cast compile error (fixed inline) and one test-fixture rebalance (an anti-correlated churn/size fixture was needed so exactly one file, not two, sat above both axis medians).

### Completion Notes List

**Initial implementation:**
- Added `Charts.RiskQuadrant` (the SVG scatter builder) and `Charts.RiskQuadrantElevatedFiles` (the shared median-split computation reused by the text-equivalent list) plus `Charts.RiskQuadrantMinFiles = 6` (this story's own documented "too few files" threshold — no existing one to reuse). Both derive from one private `BuildRiskPoints`/`Median` pair so the SVG and the ranked list can never disagree about which files are flagged.
- X = `Math.Log(Math.Max(Lines, 1))` (log-scaled size, zero/near-zero guarded), Y = `Metrics.Changes` (churn frequency, deliberately not `TotalChurn`). Median-split both axes; the high/high quadrant is shaded AND its points carry a second class — never a color-only flag, per Story 7.8's shape+edge precedent.
- No new git call, no complexity analyzer — consumed `CodeMap.Files()`/`CodeFileMetrics` exactly as they already existed.
- Full suite green (1893 tests at that point).

**Review-pass rework (owner feedback on the first pass):** the chart was getting buried at the bottom of the (already long) Code Map page, and the owner asked for (1) its own Insights nav page, (2) the elevated-risk list as a paginated grid, (3) more color gradation on the scatter points, and (4) richer tooltips.
- **New standalone page**: `risk-quadrant.html` via a new `RiskQuadrantTemplater` (mirrors `DeepAnalyticsTemplater`/`CodeMapTemplater`'s synthesized-page-shell pattern). Removed the section from `CodeMapTemplater` entirely. New `SiteNav.RiskQuadrantOutputPath` rides the SAME gating signal as `CodeMapOutputPath` (`hasCodeMap`) as a sibling Insights entry — no second flag, so the two pages can never dangle independently. New `SiteGenerator.WriteRiskQuadrant` (parallel to `WriteCodeMap`, same never-throw/AD-4 discipline). New `Icons.ForConcept("Risk Quadrant")` glyph.
- **Gradation**: every point now also carries a `level-0..4` class — the SAME 5-level gold ramp the treemap/heatmap already use (via `Charts.Bucket` over a combined normalized size+churn position) — as an additional, non-load-bearing signal. The `risk-point-elevated` class (heavier rust stroke) stays the accessible, never-color-alone flag; gradation and elevation are visually independent (a gradient fill + an optional stroke ring on top).
- **Richer tooltips**: replaced the native `<title>` with the SAME rich `data-tip-html` card the treemap's cells use (`Charts.BuildTreemapCard` — lines, type, changes, churn, avg change size, files changed together, first/last dates whenever each metric exists), served through the existing shared body-level tooltip script (no new JS needed for this part — reused sitewide `.js-tip` infra). A plain-text `aria-label` stays the always-present accessible name.
- **Paginated grid**: the elevated-risk list is now a CSS grid (`.risk-grid`/`.risk-grid-item`) instead of an `<ol>`. The FULL ranked list always renders in the markup (the no-JS truth); a Prev/Next pager (`.risk-pager`, emitted `hidden`) is a new scoped `specscribe.js` enhancement (`initRiskGridPager`) that only reveals itself and chunks the list once there's more than one page's worth (12/page) — never a truncation, matching this project's established progressive-enhancement discipline (mirrors the codemap dimension-switch/zoom pattern).
- Golden content fingerprint regenerated again for the new page + nav entry (new page in `GoldenOutputInventory`; verified stable across repeated runs, including after several unrelated concurrent sessions landed on this shared working tree in between).
- Manual verification (generated to an isolated `--output` dir since `SpecScribeOutput` was locked by another concurrent session's process): `code-map.html` no longer contains the risk quadrant; `risk-quadrant.html` renders standalone with all 5 gradient levels present, 255 elevated points/grid items (matching 1:1), rich `data-tip-html` on every point, guarded links to real code pages, and the pager markup correctly emitted `hidden`. `node -c` confirmed the new JS is syntactically valid. Live in-browser DOM verification wasn't possible this pass (the Browser pane's preview_start timed out unresponsive); verification used direct HTML/grep inspection instead.
- Full suite: 1930/1932 green. The 2 remaining failures (`HtmlTemplaterTests.RenderIndex_PresentFamilyCardIsAWholeCardLinkToItsPage`/`RenderIndex_EveryCanonicalFamilyLabelGetsItsExpectedAccentClass`) are a pre-existing, unrelated regression in the "coverage-card" planning-artifact feature from other concurrent work on this shared `main` (confirmed `HtmlTemplater.cs` no longer contains any "coverage-card" string at all, and this story never touches that file) — out of scope for Story 7.10, flagged here rather than fixed blind.
- This working tree is shared (non-worktree) `main` with a background auto-committer and multiple genuinely concurrent sessions active throughout this story (Stories 7.9/7.11/7.12, an 8.1 spike, and others landed mid-session). Verified via `git diff --stat`/`git log` at each checkpoint that this story's own changes were correctly isolated and none of its file-list entries were touched by the concurrent work.

### File List

- `src/SpecScribe/Charts.cs` — `ChartMetric.RefactorRisk` case + `WhyText`; `RiskQuadrantMinFiles` constant; `RiskPoint`/`BuildRiskPoints`/`Median`/`RiskQuadrant`/`RiskQuadrantElevatedFiles` (rewritten in the review pass for gradient `level-0..4` classes + rich `data-tip-html` tooltips via `BuildTreemapCard`).
- `src/SpecScribe/RiskQuadrantTemplater.cs` — **new file**, the standalone `risk-quadrant.html` page templater (chart + paginated elevated-risk grid).
- `src/SpecScribe/CodeMapTemplater.cs` — the risk-quadrant section added in the initial pass was fully reverted/removed here (moved to its own page).
- `src/SpecScribe/SiteNav.cs` — new `RiskQuadrantOutputPath` const + Insights nav/quick-link entry (shares `hasCodeMap`'s gating signal).
- `src/SpecScribe/SiteGenerator.cs` — new `WriteRiskQuadrant` (parallel to `WriteCodeMap`), called right after it.
- `src/SpecScribe/Icons.cs` — new `"Risk Quadrant"` concept glyph.
- `src/SpecScribe/assets/specscribe.css` — `.risk-quadrant`/`.risk-point` (+ `level-0..4` gradient)/`.risk-quadrant-elevated`/`.risk-grid`/`.risk-grid-item`/`.risk-pager` (and related) rules; the initial pass's `.risk-quadrant-list` rules were removed (replaced by the grid).
- `src/SpecScribe/assets/specscribe.js` — new `initRiskGridPager` progressive-enhancement pagination for `.risk-grid`.
- `tests/SpecScribe.Tests/ChartsTests.cs` — `RiskQuadrant`/`RiskQuadrantElevatedFiles` coverage, updated for the gradient level classes + rich-tooltip markup.
- `tests/SpecScribe.Tests/RiskQuadrantTemplaterTests.cs` — **new file**, page-level coverage for the standalone page.
- `tests/SpecScribe.Tests/CodeMapTemplaterTests.cs` — the initial pass's risk-quadrant assertions reverted (section no longer lives here).
- `tests/SpecScribe.Tests/SiteGeneratorCodeMapTests.cs` — risk-quadrant generation-level coverage moved from `code-map.html` assertions to `risk-quadrant.html` assertions.
- `tests/SpecScribe.Tests/SiteNavTests.cs` — updated for the new Insights nav entry (Code Map + Risk Quadrant no longer collapses to a flat link).
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — `risk-quadrant.html` added to `GoldenOutputInventory`; golden content fingerprint constant regenerated.

### Bug-fix pass (post-review-pass, owner-reported)

Three issues found live in the reworked page:

1. **Missing nav icon.** The "Risk Quadrant" glyph I'd added to `Icons.cs` during the review-pass rework was lost — a concurrent auto-commit on this shared, non-worktree `main` landed a version of `Icons.cs` without it (the same class of shared-main clobber documented in project memory). Re-added the `"Risk Quadrant" => Svg(...)` case; `IconsTests.ForConcept_EveryEmittedLabelHasAGlyph` (data-driven over every emitted nav label) now covers it, so a repeat loss fails loudly instead of silently shipping an unlabeled-looking link.
2. **Pager showed all N items on every page instead of 12.** `.risk-grid-item` sets its own `display: flex`, which — being a class selector — beats the UA stylesheet's attribute-only `[hidden] { display: none }` rule specificity-wise. `specscribe.js`'s pager was correctly setting `item.hidden = true` on off-page items, but the CSS override meant nothing actually disappeared. Added `.risk-grid-item[hidden] { display: none; }`; new `StylesheetTests.Stylesheet_RiskGridItem_HasAnExplicitHiddenOverride` regression guard.
3. **`sprint-status.yaml`/`epics.md` (and other BMad artifact files) linked to a raw `code/…html` view instead of their real rendered page.** These files are walked as ordinary source-code-walk entries (this repo's own `_bmad-output` is version-controlled) AND already have a dedicated portal page — `CodeItemHref` was routing purely off `_codePages`/external-source fallback with no awareness of that page existing. Added a new `SiteGenerator.ArtifactHrefByRepoRel()` (mirrors `BuildArtifactsByDay`'s `Track` helper — same `_referenceMap`/`_docs`/`_adrs` sources, reconciled to repo-relative paths) checked FIRST in `CodeItemHref`, plus hand-known special cases for `sprint-status.yaml` → `SiteNav.SprintOutputPath` and `epics.md` → `SiteNav.EpicsOutputPath` (neither goes through the generic doc pipeline, so neither is in `_docs`). This is a shared resolver used by every git-analytics surface (Code Map, Deep Analytics, Git Insights, dashboard Git Pulse, Risk Quadrant) — fixing it once benefits all of them, not just this story's own page. New `SiteGeneratorCodeMapTests` coverage generates a fixture with churned `sprint-status.yaml`/`epics.md` and asserts the code-map table links to `sprint.html`/`epics.html`, never a `code/...html` page.

Golden fingerprint + `GoldenOutputInventory` regenerated again (both `risk-quadrant.html`'s addition to the inventory and the nav-icon/CodeItemHref changes had been silently dropped by the same concurrent-commit clobber described in #1 above, so this also re-applied those). 1942/1944 tests green — the 2 remaining failures are the same pre-existing, unrelated `HtmlTemplaterTests` coverage-card regression from other concurrent work, confirmed again via `git diff`/`grep` that this story never touches `HtmlTemplater.cs`.

## Change Log

- 2026-07-20: Story 7.10 implemented — refactor-target risk quadrant (size × churn-frequency scatter) added to the Code Map page. 1893 tests green; golden fingerprint regenerated; manually verified against this repo's own `--deep-git` history.
- 2026-07-21: Review-pass rework (owner feedback) — moved the chart off the Code Map page onto its own new Insights page (`risk-quadrant.html`, `RiskQuadrantTemplater`), added a 5-level color gradient to the scatter points (independent of the elevated flag), replaced native `<title>` tooltips with the treemap's richer `data-tip-html` card, and rebuilt the elevated-risk list as a paginated CSS grid (progressive-enhancement JS, full list always in the no-JS markup). 1930/1932 tests green (2 pre-existing, unrelated `HtmlTemplaterTests` failures from other concurrent work); golden fingerprint regenerated.
- 2026-07-21: Bug-fix pass (owner-reported, live on the reworked page) — restored a nav icon lost to a concurrent shared-main clobber, fixed the pager's `[hidden]` CSS specificity bug (was showing every item on every page), and taught the shared `CodeItemHref` resolver to route recognized BMad artifact files (`sprint-status.yaml`, `epics.md`, other docs/ADRs) to their real rendered page instead of a raw code view. 1942/1944 tests green; golden fingerprint regenerated; manually verified all three fixes against this repo's own `--deep-git` history.
