---
baseline_commit: 12ecce126a6af041b0bca945fc3ed4e76af3589a
---

# Story 7.12: Code Freshness / Age Map

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a newcomer orienting to a codebase,
I want to see which areas are actively evolving versus long-untouched,
so that I can tell load-bearing hot code from stable or possibly-dead corners.

## Acceptance Criteria

1.
**Given** each file's last-commit date from the deep-git path
**When** the freshness map renders
**Then** files are shaded by recency of last change, reusing the `--status-*` / heat token system (not a new palette) with a real-value legend per the Story 10.2 chart-metadata standard
**And** color is never the sole signal (path + date remain available as text / tooltip). [Source: epics.md#Story 7.12 (lines 1519-1523); FR19]

2.
**Given** generation-time determinism (FR31, NFR3)
**When** freshness is computed
**Then** it derives from git timestamps only — no per-visitor "now" drift — and a from-scratch CI regeneration produces identical output
**And** non-git repos omit the surface cleanly (NFR8). [Source: epics.md#Story 7.12 (lines 1525-1529); FR31, NFR8]

---

## OWNER-SELECTED VISUAL (locked this session — the #1 review checkpoint)

The owner was offered three directions for this story's new visual surface (per the standing "elicit visual intent" directive, [[create-story-elicit-visual-intent]]): promoting the Code Map treemap's existing JS-only "Recently changed" dimension in place, a second dedicated freshness treemap, or a ranked staleness list. **The owner rejected all three and chose a fourth: a directory-structure sunburst, colored by recency, sized by lines of code.** This is the most novel, highest-risk build in the 7.10–7.12 batch — there is no existing general-purpose hierarchical sunburst in this codebase (`Charts.Sunburst`/`EpicSunburst` are hard-coded to the 3-ring epic→story→follow-up work model, not a reusable N-level tree renderer) — so this story **writes a new, from-scratch recursive sunburst builder**, not an extension of an existing one. Treat the geometry approach below as a strong, reasoned default; flag anything that reads wrong for review.

**Explicitly out of scope, even though the user floated it in conversation:** Epic 20's interactive drill-in/zoom sunburst explorer. That epic is a *different* hierarchy (the epic/story/follow-up work graph, gated behind a not-yet-started JS-budget spike, Story 20.1) and a *different* interaction model (click-to-zoom, client JS). This story's sunburst is **static, pure-SVG, no-JS** — one baked rendering per generation, matching every other chart on this site — with only leaf (file) wedges optionally clickable via a plain `<a>` (no zoom, no re-center). Do not reach for Epic 20's JS budget or its `Charts.Sunburst` ring math; they solve a different problem.

---

## Developer Context

**Read this first — this is a new-chart-type rendering story on the Code Map page, not a new git computation, and not the same page 7.10/7.11 use.** The three concurrently-seated Stories 7.10/7.11/7.12 each render on whichever existing page already has their data joined: 7.10 (risk quadrant) → the Code Map page, because size+churn are already joined on `CodeMapNode`; 7.11 (ownership) → the Git Insights hub, because contributor data lives in `GitInsightsData.Files`. **This story's data — per-file last-commit date — is ALSO already joined on `CodeMapNode.Metrics.LastDate`** (`GitMetrics.CodeFileMetrics.LastDate`, `GitMetrics.cs:98`), so **this story lands on the Code Map page too**, as a second new chart section sibling to 7.10's risk quadrant. **Crucially, `CodeMapNode` also already carries the directory hierarchy itself** (`Roots`/`Children`/`IsDirectory`) — the exact tree structure the owner's chosen sunburst visual needs — so both axes of this story's design (the hierarchy AND the per-file freshness data) are already fully built by `CodeMap.Build` for the treemap. **There is no new fetch, no new parse, no new tree-walk.**

The four highest risks are: **(1)** re-deriving the directory tree or the per-file last-changed date instead of consuming `CodeMap.Roots` (there is **no** new git call and **no** new source-file walk in this story — `CodeMap.Build`'s tree and `GitMetrics.BuildCodeMapMetrics`'s `LastDate` both already exist and are already wired into `SiteGenerator.WriteCodeMap`); **(2)** **misreading AC #1's "reusing the `--status-*` / heat token system" literally.** This codebase's own convention — stated repeatedly in CSS comments and [[specscribe-status-token-system]] — is that the six-stage `--status-*` lifecycle tokens are **off-limits on code surfaces** (`specscribe.css:1161,1415,1520`: "the `--status-*` lifecycle tokens are off-limits on code surfaces"). The AC's phrasing is loose; the actual established precedent for "heat" on a code surface is the **level-0..4 sequential ramp** shared by the commit heatmap and the Code Map treemap's churn dimension (`specscribe.css:3541-3550`: "Code mass/churn is NOT a lifecycle stage, so nothing here routes through the `--status-*` tokens... the ramp reuses the commit-heatmap sequential levels"). **Use the level-0..4 ramp, not `--status-*`.** This is not a judgment call — it is what "not a new palette" in the same AC sentence already implies, and it is the only reading consistent with the rest of the codebase; **3)** writing a genuinely new N-level recursive sunburst geometry — the existing `Charts.Sunburst` (`Charts.cs:270`) is a **fixed 3-ring** layout hard-coded to epic/story/follow-up semantics (status-colored wedges, `StatusStyles.ForEpicWithRetrospective` fill) and is not reusable for an arbitrary-depth directory tree; you are writing new rendering code, not extending an existing shape; **(4)** repeating the Code Map treemap's own legend defect — its `AppendLegend` (`CodeMapTemplater.cs:169-179`) still renders a literal "Less … More" label, which **predates and violates** the Story 10.2 real-value-legend standard (10.2 explicitly targeted "kill the heatmap's 'Less … More'" but never retrofitted the treemap). AC #1 requires a **real-value legend** — do not copy the treemap legend's placeholder text; follow `Charts.HeatLevelRange`'s pattern (real count/date ranges per swatch) instead.

### What already exists (reuse / consume — do NOT rebuild)

- **The directory tree + per-file freshness, already joined: `CodeMap.Roots` / `CodeMapNode`.** `CodeMap.Build` (`CodeMap.cs:144-199`) already walks the source tree, collapses single-child directory chains, and joins each file leaf to its `CodeFileMetrics?` (nullable — `null` for a file with no git record). `CodeMapNode.Metrics?.LastDate` (`GitMetrics.cs:98`) is exactly AC #1's "each file's last-commit date." **This is the one and only data source for this chart.** No new fetch, no new parse, no new tree-walk. [Source: [CodeMap.cs:13-19,144-199](../../src/SpecScribe/CodeMap.cs) (`CodeMapNode`, `Build`); [GitMetrics.cs:98](../../src/SpecScribe/GitMetrics.cs) (`CodeFileMetrics.LastDate`); [[deep-git-single-numstat-path]]]
- **The page and its already-built variants: `CodeMapTemplater.RenderPage`/`SiteGenerator.WriteCodeMap`.** The Code Map page (`code-map.html`) already computes all four filter variants once per generation (`CodeMap.BuildVariants`) and gates the whole page on the unfiltered `"full"` variant being non-empty. This story's sunburst is **one more new section on this existing page**, sourced from `full.Map.Roots` — the same unfiltered tree the treemap and (per 7.10) the risk quadrant already use. Not a new page. [Source: [CodeMapTemplater.cs:23-70](../../src/SpecScribe/CodeMapTemplater.cs) (`RenderPage`), [SiteGenerator.cs:2759-2782](../../src/SpecScribe/SiteGenerator.cs) (`WriteCodeMap`)]
- **The guarded code-page resolver: `CodeItemHref` (already `RenderPage`'s `fileHref` parameter).** Resolves a repo-relative path to `code/<path>.html` (or an external base fallback), `null` when neither exists — never a dead link (Story 7.2 seam). Thread the same delegate into your new section; don't invent a second resolver. [Source: [SiteGenerator.cs:1341-1366](../../src/SpecScribe/SiteGenerator.cs)]
- **The existing text-equivalent table already has a "Last" column.** `CodeMapTemplater.AppendFileTable` (`CodeMapTemplater.cs:190-`) already renders every file's `First`/`Last` git dates as plain text, sourced from the same `Metrics`. **This already satisfies most of AC #1's "path + date remain available as text" requirement — do not build a second table.** Point the sunburst's caption/legend at this existing table rather than duplicating it (mirror how 7.10's risk-quadrant text-equivalent is a *new, narrower* ranked list only because no existing table already carried that specific pairing — here one already does).
- **The Story 10.2 chart-metadata standard: `Charts.ChartMeta`/`Framed`/`WhyText`/`ChartMetric`.** Add a new case (e.g. `ChartMetric.CodeFreshness`) with its own `WhyText`. **Coordinate on the enum**: Stories 7.10 (`RefactorRisk`) and 7.11 (`AuthorConcentration`) are concurrent siblings from the same SCP batch, independently adding their own cases. Add yours additively — the `WhyText` switch throws `ArgumentOutOfRangeException` on an unmapped value (`Charts.cs:45`), so a missing case fails loudly at render time. Check `main`/the working tree for whichever of 7.10/7.11 landed first before you branch, and don't reorder or collide with their case. [Source: [Charts.cs:13-92](../../src/SpecScribe/Charts.cs)]
- **The level-0..4 heat ramp: `Charts.HeatLevel`/`HeatThresholds`/`FormatHeatRange`/`HeatLevelRange` (count-based) as the pattern to mirror for dates.** These are `private` helpers on `Charts` used by the commit heatmap and (baked default) the treemap's churn dimension — count in, quartile-bucketed level 0-4 out, plus a real-range legend string. There is **no existing date-based analog** (the treemap's client-side "Recently changed" dimension buckets dates entirely in JavaScript at `specscribe.js:774-811`, never server-side) — you are writing a new C# date→level function modeled on this exact shape, not extending it. Semantics: **most-recently-changed = hottest (level 4, gold)**, **long-untouched = coolest (level 1)**, **no git record = neutral `level-none`** (mirrors `.codemap-cell.level-none`, `specscribe.css:3550`). [Source: [Charts.cs:2716-2746](../../src/SpecScribe/Charts.cs); [specscribe.css:3541-3550](../../src/SpecScribe/assets/specscribe.css)]
- **The shared level-0..4 CSS ramp values** (`.codemap-cell.level-0..4`, `.heatmap-cell.level-0..4` — identical fill values at both sites so the two ramps never desync). Reuse these exact color values for the sunburst's file wedges (new selector, same fills) rather than inventing a fifth copy of the palette. [Source: [specscribe.css:3545-3550,3851-3855](../../src/SpecScribe/assets/specscribe.css)]
- **The "no directory text label at any depth" convention.** The treemap deliberately never labels directory rectangles — identity lives in the tooltip + text table (`CodeMap.cs:3-12`; `specscribe.css:3557-3558`: "no text label at any depth"). Follow the same discipline for directory wedges on the sunburst: neutral fill, no on-wedge text, rich `<title>` tooltip only (directory path + descendant file count).

### Proposed geometry (create-story judgment call, owner locked the CHART TYPE not the geometry — confirm/adjust at dev-story or review)

- **A recursive angular partition (the standard sunburst/icicle algorithm), NOT `Charts.Sunburst`'s fixed 3-ring approach.** Each `CodeMapNode` occupies one ring band at its own tree depth; a node's angular span is proportional to its `Lines` weight (same sizing convention as the treemap) and is subdivided among its `Children` recursively, exactly the way the treemap's squarified layout subdivides *area* among children — this subdivides *angle* instead. Root-level nodes start at `-π/2` (12 o'clock, matching `Charts.Sunburst`'s own convention) and fill the full circle.
- **Cap the ring count with a new, documented constant** (e.g. `Charts.FreshnessSunburstMaxDepth`, a small number like 5-6 — pick and document your own value; `CodeMap.Build` already collapses single-child directory chains so real repos are shallower than their raw path depth suggests, but a defensive cap still matters for pathological trees). Nodes beyond the cap render their *file* leaves flush into the outermost ring (still individually colored/linked) without further nesting — never an unbounded ring count.
- **Only file (leaf) wedges are colored by the freshness ramp**; directory wedges are neutral (the `--ink-light`/`--border`/`--parchment` family — never `--gold`, never `--status-*`, matching the code-surface neutral-token rule), unlabeled, bounded by a hairline stroke — matching the treemap's directory-boundary convention (`specscribe.css:3557-3563`).
- **A file wedge with `Metrics is null`** (no git record) renders `level-none` (the same dim neutral fill the treemap uses for the identical case) — still present in the wheel (its `Lines` still occupies angular space so siblings don't visually inflate), just uncolored by recency.
- **Guarded `<a>` wrap** on file wedges only, via `fileHref`/`CodeItemHref` (never a dead link) — directory wedges are never links (matches the treemap; no drill-down/zoom in this static build).
- **Rich `<title>` tooltip on every wedge**: file wedges get the full path + exact last-changed date (e.g. `"src/SpecScribe/Charts.cs — last changed 2026-07-13"`, or "no git history" when `Metrics` is null); directory wedges get their collapsed label/path + file count.
- **Legend: a real-value date-range/day-count per level-0..4 swatch** (mirroring `HeatLevelRange`'s shape, not the treemap's broken "Less … More" — see risk #4 above), plus the Story 10.2 `Why` framing sentence via the new `ChartMetric` case, all through `Charts.Framed`.
- **Accessible text equivalent:** point to the existing `AppendFileTable`'s `Last` column (already renders below the treemap on this same page) rather than building a second table — a short caption near the sunburst ("see the Last column below for every file's exact date") is sufficient; the sunburst's own `role="img"` `aria-label` should summarize file/directory counts and the freshest/stalest dates found, matching how `CommitHeatmap`'s `aria-label` summarizes its own chart (`Charts.cs:1155`).

### Scope boundary (read carefully)

- **IN scope:**
  - A new pure-SVG recursive sunburst builder in `Charts.cs`, walking `CodeMapNode`'s tree, sizing wedges by `Lines`, coloring file leaves by a new date-based level-0..4 ramp (recency), directories neutral/unlabeled.
  - A new date→level-0..4 function + a real-value legend function (modeled on `HeatLevel`/`HeatLevelRange`, operating on dates instead of counts).
  - A documented ring-depth cap constant.
  - Guarded clickthrough on file wedges (`CodeItemHref`), rich tooltips, Story 10.2-compliant framing (new `ChartMetric` case + `WhyText`).
  - Wiring the new section into the existing Code Map page (`CodeMapTemplater.RenderPage`), sourced from the unfiltered `"full"` variant's `Roots`.
  - CSS for the sunburst (file-wedge ramp classes reusing the existing level-0..4 fill values, directory-wedge neutral classes, legend).
  - Graceful degradation: no deep-git / no metric-bearing files → section omitted (AC #2, NFR8).
- **OUT of scope:**
  - **Any new git call, fetch-format change, or parse.** Consume `CodeMap.Roots`. [[deep-git-single-numstat-path]]
  - **Epic 20's interactive drill-in/zoom sunburst, or any JavaScript.** This is a static, pure-SVG chart like every other one on this site. [[charting-is-pure-svg-no-js]]
  - **`--status-*` lifecycle tokens** anywhere on this chart (see risk #2 above). [[specscribe-status-token-system]]
  - **Retrofitting the treemap's own "Less … More" legend.** Out of scope for this story — flag it as a follow-up if you want, but don't touch `CodeMapTemplater.AppendLegend`'s existing behavior.
  - **A second file text-equivalent table.** Reuse the existing `Last` column in `AppendFileTable`.
  - **Extending `Charts.Sunburst`/`EpicSunburst`.** Those are a different hierarchy and a different color scheme (epic/story status). Write a new, separate builder.
  - **Per-filter-variant duplication** (exclude-spec-dev / exclude-tests) — source from the unfiltered `"full"` variant only, matching 7.10's identical scope call.
  - Stories 7.10 (risk quadrant) and 7.11 (ownership) — sibling stories in the same batch, rendering elsewhere. Only coordinate on the shared `ChartMetric` enum if landing concurrently.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Consume `CodeMap.Roots` from the Code Map page's unfiltered `"full"` variant** — the same `variants` list `CodeMapTemplater.RenderPage` already receives. No new computation, no new `SiteGenerator` wiring beyond passing the already-available `fileHref` through. [Source: [CodeMapTemplater.cs:23-70](../../src/SpecScribe/CodeMapTemplater.cs)]
- **Weight wedges by `Lines`**, recursively subdividing a parent's angular span among its `Children` (directories and files alike) — the same size semantics the treemap and 7.10's quadrant both use for "size."
- **Color ONLY file-leaf wedges** by a new date-based level-0..4 ramp derived from `Metrics?.LastDate`; most-recent = level 4 (hottest/gold), oldest = level 1, `Metrics is null` = `level-none` (neutral, matching the treemap's identical no-data case). Directory wedges stay neutral/unlabeled.
- **Reuse the existing level-0..4 CSS fill values** (`.codemap-cell.level-0..4`/`.heatmap-cell.level-0..4`) for the new wedge classes — same colors, new selector; not a new palette (AC #1).
- **Write a real-value legend** (date range or "N days ago" per level, derived from the SAME thresholds the level function uses, so swatch and text can never disagree — mirror `Charts.HeatLevelRange`'s shape). **Do NOT copy the treemap's "Less … More" placeholder legend** — that predates and violates Story 10.2.
- **Define and document a ring-depth cap** (e.g. `Charts.FreshnessSunburstMaxDepth`) so a pathologically deep tree can't produce an unbounded ring count; beyond the cap, flatten remaining file leaves into the outermost ring.
- **Guard the clickthrough (AC #1).** A file wedge is a link only when `fileHref(path)` resolves; otherwise a plain, non-link, still-tooltipped wedge. Directory wedges are never links. Never a dead link.
- **Rich tooltips.** File wedge `<title>`: full path + exact last-changed date (or "no git history"). Directory wedge `<title>`: collapsed label/path + descendant file count.
- **Route all chrome through `Charts.Framed`/`ChartMeta`.** Add a new `ChartMetric` case (coordinate with 7.10/7.11's concurrent additions) with its own framework-neutral `WhyText` (NFR8). [Source: [Charts.cs:13-92](../../src/SpecScribe/Charts.cs)]
- **Point the accessible text equivalent at the EXISTING `AppendFileTable`'s `Last` column** rather than building a second table.
- **Escape everything.** Paths and tooltip text are free-text/injection surfaces → `Html(...)` on every derived string, mirroring `CodeTreemap`/`CouplingGraph`.
- **Keep it pure SVG + CSS, no JS.** [[charting-is-pure-svg-no-js]]
- **Degrade non-fatally (AC #2, NFR2, NFR8).** No deep-git → every `Metrics` is null → the whole map still renders (all neutral `level-none` wedges) UNLESS `CodeMap.IsEmpty`/no source files at all, matching the existing page-level gate; a genuinely empty/thin repo → section omitted, never a broken page.
- **Deterministic layout (FR31).** Angular partitioning, level bucketing, and legend derive purely from already-fetched git timestamps — no `DateTime.Now`, no wall-clock. Same input → byte-identical SVG across regeneration runs.

### DON'T

- **DON'T add a git call, change the fetch format, or re-parse.** Consume `CodeMap.Roots`/`CodeMapNode.Metrics`. [[deep-git-single-numstat-path]]
- **DON'T use `--status-*` lifecycle tokens for the freshness ramp.** Files don't carry a workflow status; the level-0..4 heat ramp is the correct, already-established precedent. [[specscribe-status-token-system]]
- **DON'T literally render "Less … More."** Real-value date/day-count ranges per level, per Story 10.2 and this repo's own stated intent for that standard.
- **DON'T extend `Charts.Sunburst`/`EpicSunburst`.** Different hierarchy, different color model (epic/story status vs. file recency), fixed 3-ring vs. arbitrary depth. Write a new builder.
- **DON'T add JavaScript or reach for Epic 20's interactive/zoom pattern.** This chart is static, matching every other chart on the site.
- **DON'T build a second file/date table.** `AppendFileTable`'s `Last` column already exists.
- **DON'T duplicate the chart per filter variant** (exclude-spec-dev/exclude-tests). Source from `"full"` only.
- **DON'T invent a second code-page resolver.** Thread the existing `fileHref`/`CodeItemHref` delegate through.
- **DON'T force the Core/Adapters package split** — extend `Charts`/`CodeMapTemplater`/`specscribe.css` in place. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **DON'T use `--output docs/live`** in any manual-verify step — vestigial/gitignored; default is `SpecScribeOutput/`. [[generate-output-dir-is-specscribeoutput]]
- **DON'T silently reorder or remove another `ChartMetric` enum case** if 7.10's `RefactorRisk` or 7.11's `AuthorConcentration` has landed concurrently — add yours additively.

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../../_bmad-output/specs/spec-specscribe/rendering-architecture.md)]:

- **AD-4 — optional insight providers are additive, non-blocking, never own baseline success.** The freshness sunburst is opt-in (`--deep-git` for coloring; the underlying tree needs only source files) and additive: absence yields an omitted section, not a broken Code Map page. [#AD-4; AC #2 degradation]
- **NFR8 — insight surfaces degrade gracefully, absent not broken or misleadingly empty, when data is thin/absent.** Non-git repos omit the surface cleanly (AC #2).
- **FR31 / generation-time determinism.** Freshness levels, ring geometry, and legend all derive purely from `CodeMap.Roots`' already-fetched git timestamps — no wall-clock "now," identical output across regeneration runs.
- **Local-only, read-only.** No new reads or writes; output stays under `OutputRoot`. [Inherited Invariants]
- **Seed, not invariant.** Extend `Charts`/`CodeMapTemplater` in place; no `IInsightProvider`/package split. [Source: ARCHITECTURE-SPINE.md#Seed, Not Invariant]
- **Pure-SVG / no-JS.** New builder follows the `CodeTreemap`/`CouplingGraph` idiom, not Epic 20's not-yet-started interactive pattern. [[charting-is-pure-svg-no-js]]

### Delivery-adapter note (does this touch webview/SPA parity?)

The Code Map page is a synthesized page built directly by `CodeMapTemplater` (like `CommitDayTemplater`/`CodeFileTemplater`), **not** routed through `HtmlRenderAdapter.RenderPage`/the `IRenderAdapter` view-model path — so this change does **not** add a `HostRenderException` or a RenderParity registry exception, and doesn't touch webview/SPA body seams. It **does** change `specscribe.css` bytes and the `code-map.html` body, so the **golden content fingerprint** will shift — regenerate it with the standard normalizations after implementation, and be aware 7.10 (if it lands first/concurrently) also moves this same fingerprint on this same page; don't assume your diff is the only source of drift. [[golden-diff-normalization-gotchas]]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **No new git library, no git call.** The one git seam (`GitMetrics.TryComputeDeep`) is untouched. [[deep-git-single-numstat-path]]
- **Existing infra to reuse (do not reinvent):**
  - [CodeMap.cs:13-19,144-199,296-311](../../src/SpecScribe/CodeMap.cs) — `CodeMapNode`, `Build`, `Files()` (the joined tree+freshness source).
  - [GitMetrics.cs:98](../../src/SpecScribe/GitMetrics.cs) — `CodeFileMetrics.LastDate`.
  - [Charts.cs:2716-2746](../../src/SpecScribe/Charts.cs) — `HeatLevel`/`HeatThresholds`/`FormatHeatRange`/`HeatLevelRange`, the pattern to mirror for a date-based analog.
  - [Charts.cs:13-92](../../src/SpecScribe/Charts.cs) — `ChartMeta`/`Framed`/`WhyText`/`ChartMetric` (Story 10.2 standard — add a new enum case).
  - [Charts.cs:2748,2752,2754](../../src/SpecScribe/Charts.cs) — `F`/`Plural`/`Html` formatting/escaping helpers.
  - [CodeMapTemplater.cs:23-70,190-](../../src/SpecScribe/CodeMapTemplater.cs) — `RenderPage` (where the new section is wired in), `AppendFileTable` (the existing `Last`-column text equivalent to point at, not duplicate).
  - [SiteGenerator.cs:1341-1366](../../src/SpecScribe/SiteGenerator.cs) — `CodeItemHref`, the guarded resolver already threaded as `fileHref`.
  - [specscribe.css:3541-3563](../../src/SpecScribe/assets/specscribe.css) — the level-0..4 ramp + directory-boundary neutral convention to mirror.

---

## File Structure Requirements

**New files:**

- *(None required.)* This extends existing renderers. If the recursive angular-partition math grows large, a small private helper method/struct inside `Charts.cs` is fine, but no new production file or output path is expected.

**Modified files (read fully before editing):**

- `src/SpecScribe/Charts.cs` — add the new recursive sunburst SVG builder (angular partition by `Lines`, depth-capped, file leaves colored by the new date-level ramp, directories neutral/unlabeled), the new date→level-0..4 function + real-value legend function (modeled on `HeatLevel`/`HeatLevelRange`), guarded `<a>`-or-plain wedge rendering for files, rich `<title>` tooltips, `role="img"` summary `aria-label`, `chart-empty` degrade. Add the new `ChartMetric` case + `WhyText`.
- `src/SpecScribe/CodeMapTemplater.cs` — add a new section (sourced from the unfiltered `"full"` variant's `Roots`) wrapping the sunburst in `Charts.Framed`, with a caption pointing at the existing `AppendFileTable`'s `Last` column as the text equivalent. Preserve all existing panel/table rendering untouched.
- `src/SpecScribe/assets/specscribe.css` — add classes for the sunburst's file-wedge ramp (reuse existing level-0..4 fill values), directory-wedge neutral fill/stroke, and legend. **Edit only** `src/SpecScribe/assets/specscribe.css` (any `docs/live` copy is vestigial). [[generate-output-dir-is-specscribeoutput]]

**Output layout:** No new paths. The new section renders inside the existing `SpecScribeOutput/code-map.html` page; file wedges link to sibling `code/…html` pages via `CodeItemHref`.

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Chart/templater tests call the render methods directly with synthetic inputs; generation-level tests use the temp-`_bmad-output`-tree (+ a temp git repo for `--deep-git`) with `AssertNoErrors(gen.GenerateAll())`. [Source: [ChartsTests.cs](../../tests/SpecScribe.Tests/ChartsTests.cs), [CodeMapTemplaterTests.cs](../../tests/SpecScribe.Tests/CodeMapTemplaterTests.cs), [SiteGeneratorCodeMapTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorCodeMapTests.cs)]

**New `Charts` builder tests (unit, no IO) — add to `ChartsTests.cs`:**
- **Wedges render for the full tree**, sized proportionally to `Lines`, nested to match `CodeMapNode.Children` structure.
- **File wedges are colored by recency level**; a synthetic set with a clearly-most-recent and a clearly-oldest file produce different level classes (level 4 vs. level 1), and a file with `Metrics == null` renders `level-none`.
- **Directory wedges are neutral and unlabeled** (no `--gold`/`--status-*` class, no on-wedge text) regardless of their descendants' freshness.
- **Depth cap**: a synthetic tree deeper than the documented cap flattens its deepest file leaves into the outermost ring rather than growing an unbounded ring count.
- **Guarded link**: a file wedge with a resolvable `fileHref` → `<a href=…>`; without → no `<a>`. Directory wedges never render as `<a>`.
- **Rich tooltip**: a file wedge's `<title>` includes the full path and its exact last-changed date; a no-git-record file's `<title>` says so plainly.
- **Real-value legend**: the level-0..4 legend swatches carry actual date-range/day-count text, never the literal "Less" or "More" strings.
- **Empty/degrade**: an empty tree or a tree with zero metric-bearing files → `chart-empty`-style output (or a documented equivalent), never a broken SVG.
- **Escaping**: a path with `<`/`&`/`"` is escaped.
- **Determinism**: same input twice → byte-identical SVG.

**`CodeMapTemplaterTests.cs` — extend:**
- **Populated data** → the new section renders with the sunburst + a caption referencing the existing file table.
- **No metrics / thin data** → section degrades per the documented behavior; rest of the page (treemap, table) unaffected.

**Generation-level tests (extend `SiteGeneratorCodeMapTests.cs`):**
- **Opt-in on**: temp git repo + `DeepGitAnalytics = true` → `code-map.html` includes the new sunburst section with at least one file wedge resolving to its own code page.
- **Opt-out off (AC #2)**: `DeepGitAnalytics = false` → the sunburst still renders (all-neutral wedges) or omits per your documented degrade choice — pick one behavior and assert it consistently; no exception either way.
- **Non-git / no source files**: section/page omits cleanly, no exception.
- **Determinism**: two runs over the same repo produce identical `code-map.html` output (new section included).

**Golden fingerprint:** CSS + `code-map.html` body change → regenerate the golden content fingerprint (`SiteGeneratorAdapterTests.cs`, search `GoldenContentFingerprint`) with the standard normalizations — expect it to move again if 7.10 already touched this same page/fingerprint first. [[golden-diff-normalization-gotchas]]

**Run:** `dotnet test` from repo root. Then two real passes against this repo (default `SpecScribeOutput/`; **do not** pass `--output docs/live`):
1. **Baseline:** `dotnet run --project src/SpecScribe` (no `--deep-git`) → open `code-map.html` → confirm the sunburst section's degrade behavior matches what you documented (all-neutral or omitted), rest of the page unaffected.
2. **Deep:** `dotnet run --project src/SpecScribe --deep-git` → open `code-map.html` → confirm: a real directory sunburst of this repo's own files, file wedges shaded by actual recency (recently-touched files read hot, this repo's own recent commits should show clearly), directories neutral/unlabeled, guarded links to code pages that exist, rich tooltips with real dates, real-value legend (not "Less…More"), Story 10.2 chrome (title/why), no JS, escaped content.

---

## Previous Story Intelligence

- **Story 7.10 (Refactor-Target Risk Quadrant, ready-for-dev, uncommitted in this working tree) — the direct sibling to model this story's create-story pattern on.** Same batch, same page (`CodeMapTemplater`/Code Map), same "no existing threshold to reuse" finding, same `ChartMetric` coordination need. Its dev notes independently confirm: no scatter/XY builder existed before it (true here too — no hierarchical sunburst existed before this story either), the elevated-risk-style flag must never be color-only, and the delivery-adapter note (synthesized page, no `HostRenderException`, golden fingerprint moves) applies identically. **Coordinate the golden fingerprint regeneration order** — if 7.10 lands first, its regen already moved the fixture; don't assume your diff is the only source of drift. [Source: [7-10 story](7-10-refactor-target-risk-quadrant-churn-x-size.md)]
- **Story 7.11 (Code Ownership & Bus-Factor Insights, ready-for-dev, committed) — confirms the "render wherever the data is already joined" placement logic** this story also follows (Git Insights hub for 7.11, because contributor data lives there; Code Map for 7.10 and this story, because size/churn/freshness all live on `CodeMapNode`). Its dev notes explicitly flag "no conflicts with 7.10/7.12 — those render elsewhere/differently," confirming the three were designed not to collide on file lists. [Source: [7-11 story](7-11-code-ownership-and-bus-factor-insights.md)]
- **Story 7.8 (Related Files in the Reference Graph, done) — the project's most recent "two-populations, shape+edge-not-color-alone" precedent.** Its discipline (never distinguish by color alone; a rendering-only story should regenerate the golden fingerprint deliberately, not accidentally) applies directly to the directory-vs-file wedge distinction here. [Source: [7-8 story](7-8-related-files-in-the-reference-graph.md)]
- **Story 7.6 (Source Code Treemap, done) — the page and tree structure this story extends.** Built `CodeMap`/`CodeMapNode`/`CodeMapTemplater`, the "no directory text label" convention, and the level-0..4 churn ramp this story's freshness ramp reuses the CSS values from. [Source: [CodeMap.cs](../../src/SpecScribe/CodeMap.cs)]
- **Recurring lessons to apply:** escaping and stale-output are this renderer family's two most common regressions; keep layout deterministic for golden/parity; grep in-flight/recent story files for stale repeated commands (`--output docs/live`) before closing; extend the monolith in place (no package split). [Source: [7-4 story](7-4-advanced-code-and-git-coverage.md); project memory]

## Git Intelligence Summary

Recent commits (`12ecce1`, `280c351`, `b87a47e`, `d274cee`, `cb9fc4b`) are small, surgical diffs to individual templater/renderer files plus paired golden-fingerprint regeneration and targeted `*Tests.cs` additions — no large refactors; follow the same shape. `280c351` bumped 7.9/7.11 to ready-for-dev and recorded 7.10's create-story summary directly in `sprint-status.yaml`. The working tree currently has an **uncommitted** `7-10-refactor-target-risk-quadrant-churn-x-size.md` (this session's sibling) and an uncommitted `src/SpecScribe/HtmlRenderAdapter.cs` change unrelated to Epic 7 — neither overlaps this story's file list, but the tree is not clean when you branch, and **`main` has a background auto-committer**. [[worktree-edits-must-target-worktree-path]]

## Latest Technical Information

No external libraries or APIs are introduced — nothing to version-check. Platform notes:
- **Native SVG `<title>` is the tooltip** — no JS, no library; the same idiom every existing chart on this site uses.
- **Recursive angular partition**: for a node with total child weight `W` and an allotted angular span `[a0, a1]`, each child `i` with weight `w_i` gets span `[a0 + Σ(w_<i)/W * (a1-a0), a0 + Σ(w_<=i)/W * (a1-a0)]` — this is the entire algorithm; no charting library needed (this project has none and shouldn't gain one for a single chart).
- **Date-level bucketing**: convert each file's `LastDate` to "days before the most-recent last-changed date in the set" (an integer "staleness" count), then reuse the exact `HeatThresholds`/quartile-bucket shape already proven for commit counts — just invert which end is "hot" (fewest days-ago = level 4, most days-ago = level 1). This keeps the two ramps' underlying math identical, only the input transform differs.

## Project Context Reference

- FR19 (Epic 7 advanced code-and-git coverage); FR31 (generation-time determinism); NFR8 (graceful degradation on thin data): [Source: [epics.md:75,99,181](../../_bmad-output/planning-artifacts/epics.md)]
- Epic 7 goal + Stories 7.10-7.12 grouping note + Story 7.12's ACs: [Source: [epics.md:1235,1466-1469,1511-1529](../../_bmad-output/planning-artifacts/epics.md)]
- The tree + freshness joined data source: [Source: [CodeMap.cs:13-19,144-199](../../src/SpecScribe/CodeMap.cs)]
- The freshness field: [Source: [GitMetrics.cs:98](../../src/SpecScribe/GitMetrics.cs)]
- The chart-metadata standard to route through: [Source: [Charts.cs:13-92](../../src/SpecScribe/Charts.cs)]
- The count-based heat-ramp pattern to mirror for dates: [Source: [Charts.cs:2716-2746](../../src/SpecScribe/Charts.cs)]
- The page this extends + its existing gate + the existing text-equivalent table: [Source: [CodeMapTemplater.cs:23-70,190-](../../src/SpecScribe/CodeMapTemplater.cs), [SiteGenerator.cs:2759-2782](../../src/SpecScribe/SiteGenerator.cs)]
- The guarded code-page resolver: [Source: [SiteGenerator.cs:1341-1366](../../src/SpecScribe/SiteGenerator.cs)]
- The level-0..4 ramp + directory-neutral convention: [Source: [specscribe.css:3541-3563](../../src/SpecScribe/assets/specscribe.css)]
- Architecture invariants (AD-4, NFR8, FR31, local/read-only, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)]
- Project memory: [[deep-git-single-numstat-path]], [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[epic-7-code-link-strategy]], [[golden-diff-normalization-gotchas]], [[generate-output-dir-is-specscribeoutput]], [[create-story-elicit-visual-intent]], [[worktree-edits-must-target-worktree-path]].

---

## Tasks / Subtasks

- [ ] **Task 1 — Add the date-based freshness level ramp to `Charts.cs` (AC: #1, #2)**
  - [ ] New private helper(s) modeled on `HeatLevel`/`HeatThresholds`/`FormatHeatRange`/`HeatLevelRange` (`Charts.cs:2716-2746`), operating on "days since most-recent last-changed date in the set" rather than commit count; most-recent = level 4, oldest = level 1, no data = level 0/`level-none`.
  - [ ] A real-value legend function returning actual date-range/day-count text per level — never "Less"/"More".
- [ ] **Task 2 — Add the recursive directory sunburst SVG builder to `Charts.cs` (AC: #1, #2)**
  - [ ] Signature roughly `public static string CodeFreshnessSunburst(IReadOnlyList<CodeMapNode> roots, int size = ..., Func<string, string?>? fileHref = null)`.
  - [ ] Recursive angular partition by `Lines` weight, depth-capped at a new documented constant (e.g. `Charts.FreshnessSunburstMaxDepth`); beyond the cap, flatten remaining file leaves into the outermost ring.
  - [ ] File-leaf wedges: colored by Task 1's level function (`level-none` when `Metrics is null`), guarded `<a>`-wrap via `fileHref`, rich `<title>` (path + exact date or "no git history").
  - [ ] Directory wedges: neutral fill/stroke, unlabeled, never linked, rich `<title>` (label/path + descendant file count).
  - [ ] `role="img"` SVG with a summary `aria-label` (file/directory counts, freshest/stalest dates found).
  - [ ] Empty/no-source-files degrade → `chart-empty`-style output.
  - [ ] Add the new `ChartMetric` case (e.g. `CodeFreshness`) + its `WhyText`.
- [ ] **Task 3 — Wire the new section into `CodeMapTemplater.RenderPage` (AC: #1, #2)**
  - [ ] Add the section sourced from the unfiltered `"full"` variant's `Roots`.
  - [ ] Wrap in `Charts.Framed` with the new `ChartMeta`, including Task 1's real-value legend.
  - [ ] Add a caption pointing at the existing `AppendFileTable`'s `Last` column as the accessible text equivalent — do not build a second table.
- [ ] **Task 4 — Styling (AC: #1)**
  - [ ] Reuse the existing level-0..4 fill values for new file-wedge classes; add neutral directory-wedge classes. Verify recency distinctions read clearly. Update `StylesheetTests` if it asserts class presence.
- [ ] **Task 5 — Tests (AC: #1, #2)**
  - [ ] `ChartsTests.cs`: wedge nesting/sizing, recency-level coloring + `level-none`, directory neutrality, depth-cap flattening, guarded link, rich tooltip, real-value legend (not "Less…More"), empty/degrade, escaping, determinism.
  - [ ] `CodeMapTemplaterTests.cs`: populated section render + caption, degrade behavior.
  - [ ] `SiteGeneratorCodeMapTests.cs`: opt-in-with-data, opt-out/non-git degrade, determinism.
  - [ ] Regenerate the golden content fingerprint; confirm the diff is scoped to `code-map.html`/CSS (account for 7.10 if it landed first).
- [ ] **Task 6 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green. Baseline generate (no `--deep-git`) → documented degrade behavior. Deep generate (`--deep-git`) → real directory sunburst of this repo, recency-colored file wedges, neutral unlabeled directories, guarded links, tooltips with real dates, real-value legend, Story 10.2 chrome, no JS, escaped content.

## Dev Notes

- **This is a new-chart-type rendering story — no git work, no new data model.** Both the tree AND the per-file freshness data already exist and are already joined on `CodeMapNode` via `CodeMap.Build`. If you find yourself editing `GitMetrics` or re-walking source files, stop. [[deep-git-single-numstat-path]]
- **AC #1's "`--status-*`/heat token system" phrase is loosely worded — use the level-0..4 heat ramp, NOT `--status-*`.** The codebase's own CSS comments are explicit that `--status-*` lifecycle tokens are off-limits on code surfaces; "not a new palette" in the same sentence confirms reuse of the *existing* ramp is what's meant. [[specscribe-status-token-system]]
- **The treemap's own legend ("Less … More") is a pre-existing Story 10.2 violation — don't copy it.** Build a real-value legend per this story's own AC #1, modeled on `HeatLevelRange`.
- **This sunburst is brand new — `Charts.Sunburst` doesn't generalize.** It's a fixed 3-ring, status-colored, epic/story-specific layout. Write new recursive angular-partition code; don't try to bend the existing method to fit.
- **Reuse the existing `Last`-column table as the text equivalent** rather than building a second one — it already exists on this exact page.
- **No JS. Neutral/heat-ramp tokens only (never `--status-*`). Escape everything. Deterministic layout.** [[charting-is-pure-svg-no-js]] [[specscribe-status-token-system]]
- **Coordinate on `ChartMetric`** with 7.10's `RefactorRisk` and 7.11's `AuthorConcentration` — additive only.
- **The CHART TYPE (directory sunburst) is owner-locked this session; the geometry constants (depth cap, exact colors-per-level mapping) are create-story defaults** — flag anything that reads wrong for review rather than treating the geometry as fixed.
- **Do not reach for Epic 20.** That epic's interactive drill-in sunburst is a different hierarchy, a different interaction model (JS, zoom), and hasn't even had its architecture spike (20.1) run yet. This story stays static pure-SVG.

### Project Structure Notes

- All changes extend existing renderers: `Charts` (new builder + new date-ramp helpers + new enum case), `CodeMapTemplater` (new section), `specscribe.css`. No new production file, no new output path, no package restructure (deferred seed, Epics 4/6).
- The Code Map page is synthesized (not `IRenderAdapter`-routed) → no new `HostRenderException`/RenderParity registry exception; only the golden content fingerprint moves.

### References

- [Source: [epics.md:1511-1529](../../_bmad-output/planning-artifacts/epics.md)] — Story 7.12 user story + both ACs.
- [Source: [epics.md:1235,1466-1469](../../_bmad-output/planning-artifacts/epics.md)] — Epic 7 goal, FR19 coverage, Stories 7.10-7.12 shared constraints note.
- [Source: [src/SpecScribe/CodeMap.cs:13-19,144-199,296-311](../../src/SpecScribe/CodeMap.cs)] — `CodeMapNode`, `Build`, `Files()`.
- [Source: [src/SpecScribe/GitMetrics.cs:98](../../src/SpecScribe/GitMetrics.cs)] — `CodeFileMetrics.LastDate`.
- [Source: [src/SpecScribe/Charts.cs:13-92,2716-2746](../../src/SpecScribe/Charts.cs)] — chart-metadata standard + the count-based heat-ramp pattern to mirror.
- [Source: [src/SpecScribe/CodeMapTemplater.cs:23-70,145-179,190-](../../src/SpecScribe/CodeMapTemplater.cs)] — the page to extend, the existing (broken) legend NOT to copy, the existing `Last`-column table to point at.
- [Source: [src/SpecScribe/SiteGenerator.cs:1341-1366,2759-2782](../../src/SpecScribe/SiteGenerator.cs)] — `CodeItemHref` resolver + `WriteCodeMap` call site.
- [Source: [src/SpecScribe/assets/specscribe.css:3541-3563](../../src/SpecScribe/assets/specscribe.css)] — level-0..4 ramp + directory-neutral convention.
- [Source: [7-10-refactor-target-risk-quadrant-churn-x-size.md](7-10-refactor-target-risk-quadrant-churn-x-size.md)] — direct sibling, same page, same batch, `ChartMetric` coordination.
- [Source: [7-11-code-ownership-and-bus-factor-insights.md](7-11-code-ownership-and-bus-factor-insights.md)] — sibling story, placement-logic precedent.
- [Source: [_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — invariants (AD-4, NFR8, FR31, local/read-only, seed-not-invariant).
- [[deep-git-single-numstat-path]] / [[charting-is-pure-svg-no-js]] / [[specscribe-status-token-system]] / [[epic-7-code-link-strategy]] / [[golden-diff-normalization-gotchas]] / [[generate-output-dir-is-specscribeoutput]] / [[create-story-elicit-visual-intent]] / [[worktree-edits-must-target-worktree-path]] — project memory.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log
