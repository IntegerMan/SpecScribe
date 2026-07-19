---
baseline_commit: b89d6f339d77506cbe6f4b8c1f097869fa1028fe
---

# Story 10.2: Chart Metadata Standard

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a tech lead reading insight charts,
I want every chart to carry a legend with real values, its analysis time window as a number, and one framing sentence — and every ranked list to state its ranking metric,
so that charts deliver insight rather than trivia, and Journey 6 (health & hotspots) reads as information rather than decoration.

## Context & Why This Story Exists

This story is the code side of **feedback T5 / MissingFeature C2** (the "EXTENDS" item): _"Charts ship without legends, time windows, or 'why this matters'."_ ([docs/Epic3UXFeedback.md:42-48](docs/Epic3UXFeedback.md), [docs/MissingFeatures.md:54-55](docs/MissingFeatures.md)). Three concrete gaps exist today:

1. **The commit heatmap's only scale cue is "Less … More"** with undefined color steps — a reader cannot tell what a level-2 cell _means_. ([Charts.cs:608-613](src/SpecScribe/Charts.cs)).
2. **Ranked lists don't state their ranking metric or window.** Git Insights says "top 50 of 781 files" without saying _by what_ ([GitInsightsTemplater.cs:52-55](src/SpecScribe/GitInsightsTemplater.cs)); Deep Analytics says the hotspots are "across recent history" — an unquantified window ([DeepAnalyticsTemplater.cs:87](src/SpecScribe/DeepAnalyticsTemplater.cs)); the dashboard's "Top changed files" bars carry neither an "of N" total nor a metric ([Charts.cs:650-668](src/SpecScribe/Charts.cs)).
3. **Insight charts never say why the reader should care** (churn ≈ defect risk; unexpected coupling ≈ hidden dependency).

**The load-bearing requirement is AC2, not AC1.** Fixing the heatmap legend and adding a caption or two would satisfy AC1 by copy-paste — and immediately rot, because the _next_ chart added would forget them. AC2 forbids that: the metadata must come from **a shared chart-frame by construction, not per-chart copy**. Today the panel chrome — the `<h3>` title, the `deep-page-lead`/`deep-page-note` framing sentence, the `coupling-legend` caption — is hand-appended at every call site ([HtmlRenderAdapter.Dashboard.cs:59-121](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs), [DeepAnalyticsTemplater.cs:44-89](src/SpecScribe/DeepAnalyticsTemplater.cs), [GitInsightsTemplater.cs:81-83](src/SpecScribe/GitInsightsTemplater.cs)). This story routes those slots through one seam so a new chart _inherits_ the standard.

**"Extends, not duplicates."** AC2 names [spec-commit-heatmap-contrast-and-day-drilldown](_bmad-output/implementation-artifacts/spec-commit-heatmap-contrast-and-day-drilldown.md) (done, 2026-07-06) — it established the heatmap's `level-0..4` swatch treatment and the `:target` drill-down. The real-value legend **replaces the `"Less … More"` text on the same swatches**; it does not add a parallel legend or re-tune the level fills. ([docs/Epic3UXFeedback.md:111](docs/Epic3UXFeedback.md): "Heatmap contrast already has a spec … this extends, not duplicates, that work.")

**Scope note — this is not "rewrite every chart."** Several charts already satisfy AC1's legend + framing by construction (their legend/hint lives _inside_ the builder): the sunburst family (`SunburstLegend` + `sunburst-hint`), the Epic Status donut (`DonutLegend` with `Label (Count)`), the Story Pipeline funnel (`funnel-hint` + per-stage counts + %), the requirements flow (`req-flow-hint` + status tiles). **Do not re-flow those** beyond routing their _panel-level_ title/framing through the new frame where it's cheap and non-disruptive. The real work is: (a) the heatmap legend, (b) the ranked-list metric+window standard, (c) the "why it matters" framing on the insight charts, and (d) the shared frame that makes all of it by-construction.

## Acceptance Criteria

**AC1 (Every chart self-explains: real-value legend + numeric window + one framing sentence; ranked lists state their metric)**
Given any chart in the portal,
When it renders,
Then it carries a legend with **real value ranges** (not only "Less … More"), the **analysis time window as a number**, and **one sentence of why the metric matters**,
And **ranked lists state their ranking metric** (for example "top 50 of 781 by commit count").

**AC2 (The standard is structural — metadata comes from a shared chart-frame by construction)**
Given the standard is implemented,
When a new chart is added,
Then the metadata (title, window, framing sentence, ranking caption) comes from a **shared chart-frame by construction, not per-chart copy**,
And the work **extends** [spec-commit-heatmap-contrast-and-day-drilldown](_bmad-output/implementation-artifacts/spec-commit-heatmap-contrast-and-day-drilldown.md) (reuses its `level-0..4` swatches and drill-down) rather than duplicating it.

## Design Direction — the shared ChartFrame (AC2 is the #1 review checkpoint)

**Confirm the frame shape at review, before wiring every call site.** This is the "silhouette" of the story. Recommended design (latitude noted):

### The seam: `Charts.ChartFrame`

Introduce a small metadata record + one render helper in [Charts.cs](src/SpecScribe/Charts.cs) (co-located with the builders it frames, so no new file and no cross-assembly hop):

```csharp
/// The standard metadata every framed chart carries. Slots are optional so a chart uses
/// only what applies (a status donut has no time window; a heatmap has no ranking caption).
public sealed record ChartMeta(
    string Title,                 // the panel <h3> — e.g. "Git Hotspots"
    string? Window = null,        // the analysis window AS A NUMBER — e.g. "Last 300 commits · Jul 4 – Jul 12"
    string? Ranking = null,       // ranked-list metric+total — e.g. "Top 50 of 781 files by commit count"
    string? Why = null);          // ONE framing sentence — e.g. "Files changing together often hint at a hidden dependency."

// Wraps a chart body in the standard panel scaffold. The ONE place the title/window/ranking/why
// slots are rendered, so every framed chart is metadata-consistent by construction.
public static string Framed(ChartMeta meta, string body, string panelClass = "chart-panel") { … }
```

Rendered scaffold (exact class names are your call; keep them stable once chosen — they pin golden bytes and CSS):

```
<div class="chart-panel {extra}">
  <div class="chart-frame-head">
    <h3>{Title}</h3>
    {Window  → <span class="chart-frame-window">{Window}</span>}
  </div>
  {Ranking → <p class="chart-frame-ranking">{Ranking}</p>}
  {body}
  {Why → <p class="chart-frame-why">{Why}</p>}
</div>
```

**Latitude / constraints:**
- **Do not force every panel through `Framed` in one pass.** Panels that already have bespoke header rows (`chart-panel-header-row` with a "View …" link, the coverage meter, the sprint-board wheel) may keep their header and adopt only the slots they lack (a `chart-frame-why` line, a `chart-frame-window` caption). The frame is the _source of the slot markup_, not a mandate to homogenize every header. What AC2 requires is that **the slot content and its markup come from one shared place** — so grep proves there is no second hand-rolled "why this matters" `<p>` after this story.
- **Chart-intrinsic legends stay inside their builder.** The heatmap's real-value legend, the sunburst/donut/funnel legends, and the coupling node/edge legend are _part of the chart_ and already render by construction. `Framed` supplies the _panel_ chrome (title / window / ranking / why); it does not absorb a chart's own legend. A chart that carries its legend internally is already AC1-compliant for the legend clause.
- **Framing sentences must be metric-generic, never project-specific (NFR8).** "Files that change together often may hide a dependency" ✅. "SpecScribe's Charts.cs couples with SiteGenerator.cs" ❌. Epic 10's whole theme is adapter-supplied, framework-neutral copy — the "why" sentences describe the _metric_, not this repo.
- **No new information-bearing JS.** Every slot is static HTML/CSS, matching the pure-SVG chart ethos ([see charting-is-pure-svg-no-js]). The frame renders at generation time.

### AC1 gap-by-gap (what actually changes)

| Chart / list | Legend (real values) | Window (a number) | Why sentence | Ranking caption |
|---|---|---|---|---|
| **Commit heatmap** ([Charts.cs:484](src/SpecScribe/Charts.cs)) | **CHANGE** — replace "Less … More" with per-level count ranges on the same `level-0..4` swatches | **ADD** — weeks shown + date span | **ADD** (frame) | n/a |
| **Git Pulse "Top changed files"** ([Charts.cs:650](src/SpecScribe/Charts.cs)) | counts in text ✅ | **ADD** | **ADD** (frame) | **ADD** — "by files changed per commit, recent window" |
| **Deep Analytics — Hotspots** ([HotspotBars, Charts.cs:921](src/SpecScribe/Charts.cs); note [DeepAnalyticsTemplater.cs:87](src/SpecScribe/DeepAnalyticsTemplater.cs)) | counts ✅ | **ADD** — replace "recent history" with the analyzed-commit count | keep/route via frame | **ADD** — "Top N of M files by change count" |
| **Deep Analytics — Ranked Pairs** ([CouplingTable, Charts.cs:946](src/SpecScribe/Charts.cs)) | header ✅ | **ADD** | route via frame | **ADD** — "Top N coupled pairs by shared commits" |
| **Deep Analytics — Coupling graph** ([CouplingGraph, Charts.cs:978](src/SpecScribe/Charts.cs)) | `coupling-legend` ✅ (move to frame or keep) | **ADD** | keep/route via frame | n/a |
| **Git Insights — Files table** ([GitInsightsTemplater.cs:52](src/SpecScribe/GitInsightsTemplater.cs)) | table header ✅ | present ("commits analyzed" pill) — make the window a number | route via frame | **CHANGE** — "top 50 of 781 files" → add "**by commit count**" |
| Sunburst / Donut / Funnel / Req-flow | ✅ already | n/a | ✅ hint lines already | n/a |

**"The analysis time window as a number" — the honest sources (do not invent one):**
- **Heatmap:** the grid already computes its window internally — `~15 weeks` minimum, extended back to `firstCommit` if older ([Charts.cs:500-507](src/SpecScribe/Charts.cs)). State the _actual_ span: weeks rendered (`weeks` local) and the date range (`firstCommit`..`lastCommit`, via `DReadable`), e.g. `"15 weeks · Jul 4 – Jul 12, 2026"`. This lives in the builder (it's where the window is known).
- **Baseline Git Pulse files:** `TopChangedFiles` is bounded by `git log -n 200` ([GitMetrics.cs:139](src/SpecScribe/GitMetrics.cs)) → the honest window is "the last 200 commits" (capped at the repo's actual commit count).
- **Deep Analytics hotspots/coupling:** bounded by `git log --numstat -n 300` ([GitMetrics.cs:277](src/SpecScribe/GitMetrics.cs)) → "the last 300 commits". **Data gap:** `DeepGitPulse` ([GitMetrics.cs:34](src/SpecScribe/GitMetrics.cs)) does **not** carry the analyzed-commit count today; `GitInsightsData.CommitCount` ([GitMetrics.cs:91-96](src/SpecScribe/GitMetrics.cs)) does but is only present when Insights ran. **Decide the seam:** the cleanest is to add an `int AnalyzedCommits` to `DeepGitPulse` (set from `commits.Count` in `TryComputeDeep`/`ParseNumstatLog`), so the window figure is a real datum, not a hard-coded "300". Do not print a literal "300" that lies when the repo has 40 commits.
- **Git Insights hub:** `CommitCount` (commits analyzed) + `TotalFilesTouched` are already on `GitInsightsData` — reuse them; the "of 781" total is already correct, only the metric phrase is missing.

## Tasks / Subtasks

- [x] **Task 1 — Introduce the shared `ChartFrame` seam** (AC: 2)
  - [x] Add `ChartMeta` record + `Charts.Framed(meta, body, panelClass)` to [Charts.cs](src/SpecScribe/Charts.cs), rendering the title/window/ranking/why scaffold above. Every slot HTML-escaped via the existing `Html()`; optional slots omit their element entirely when null/empty (no empty `<p>` noise).
  - [x] Add the frame CSS (`.chart-frame-head`, `.chart-frame-window`, `.chart-frame-ranking`, `.chart-frame-why`) to [specscribe.css](src/SpecScribe/assets/specscribe.css) in the existing chart-panel language (muted caption type for window/ranking, a slightly emphasized but quiet `chart-frame-why`). Reduced-motion/focus not applicable (static text). Verify it themes under the webview `.vscode-*` bridge ([see story-6-5-webview-theming-live]) — reuse existing muted-text variables so the bridge needs no new mapping.
  - [x] **Decide and document the "one shared place" contract:** a single `Charts.WhyText(...)` (or a small static table of metric→sentence) so the framing sentences are defined once and referenced, not re-typed per call site. This is what makes "not per-chart copy" _verifiable by grep_.

- [x] **Task 2 — Heatmap real-value legend + window (extends the drill-down spec)** (AC: 1, 2)
  - [x] In `CommitHeatmap` ([Charts.cs:608-613](src/SpecScribe/Charts.cs)), replace the `"Less " … " More"` legend with **per-level count ranges** derived from `HeatLevel`'s buckets ([Charts.cs:1400-1416](src/SpecScribe/Charts.cs)) and the visible `maxCount`. Keep the exact `level-0..4` swatch classes (the drill-down spec's contrast treatment). Each swatch gets a real label — e.g. `0` · `1` · `2–3` · `4–6` · `7+` when `maxCount` warrants, degrading sensibly at low data (the `maxCount <= 1` uniform-history case must not print nonsense ranges — mirror `HeatLevel`'s special-case). The count is real text beside the swatch → never color-only (the codebase's standing rule).
  - [x] Derive the ranges from the **same** `HeatLevel` thresholds the cells use, so legend and cells can never disagree — factor a shared `HeatLevelRange(level, maxCount)` (or similar) that both the legend and any test can call. Do **not** hand-copy the `0.25 / 0.5 / 0.75` ratios into the legend.
  - [x] Emit the window as a number: `weeks` rendered + `firstCommit`..`lastCommit` span (`DReadable`, invariant). Decide placement: the compact spot is beside the legend row (the heatmap renders both standalone _and_ inside the Git Pulse panel where its headline is suppressed — the window caption must appear in **both** contexts, so it belongs in the builder, not the frame). Ensure it doesn't duplicate the Git Pulse signal strip's 30-day count ([Charts.cs:634-640](src/SpecScribe/Charts.cs)) — the window is the _grid span_, a distinct figure.
  - [x] Update the drill-down spec's legend assumption in [spec-commit-heatmap-contrast-and-day-drilldown.md](_bmad-output/implementation-artifacts/spec-commit-heatmap-contrast-and-day-drilldown.md) is **frozen** — do **not** edit that spec; this story _extends_ its rendering. Note the extension in _this_ story's Dev Agent Record instead.

- [x] **Task 3 — Ranked-list metric + window standard** (AC: 1, 2)
  - [x] Add `int AnalyzedCommits` to `DeepGitPulse` ([GitMetrics.cs:34](src/SpecScribe/GitMetrics.cs)); set it in `TryComputeDeep`/`ParseNumstatLog` from the parsed commit count (the `-n 300` window's actual size). This is the honest window number for the deep pages.
  - [x] Route the ranking captions through `ChartMeta.Ranking` (Task 1's frame) at the deep-analytics + git-insights + dashboard call sites: Hotspots ("Top {N} of {M} files by change count"), Ranked Pairs ("Top {N} coupled pairs by shared commits"), Git Insights files ("top {N} of {M} files **by commit count**" — extend the existing string at [GitInsightsTemplater.cs:52-54](src/SpecScribe/GitInsightsTemplater.cs)), Git Pulse top-changed-files. Where a "top N of M" already exists, keep the wording, just add the metric + move it into the frame slot so it's not a second hand-rolled string.
  - [x] Replace "across recent history" ([DeepAnalyticsTemplater.cs:87](src/SpecScribe/DeepAnalyticsTemplater.cs)) with the `AnalyzedCommits` number via `ChartMeta.Window`.

- [x] **Task 4 — "Why this matters" framing on the insight charts** (AC: 1, 2)
  - [x] Supply one metric-generic framing sentence per insight chart via `ChartMeta.Why` (Task 1's shared source): heatmap (activity cadence), Git Pulse files / Hotspots (churn ≈ where defects cluster), Coupling (files changing together ≈ hidden dependency worth a look). Reuse/relocate the sentences already sitting as per-page copy in [DeepAnalyticsTemplater.cs:46,75,87](src/SpecScribe/DeepAnalyticsTemplater.cs) — move them into the shared source, do not leave a duplicate.
  - [x] Keep them **framework-neutral** (NFR8): about the metric, never naming this repo's files.

- [x] **Task 5 — Wire the call sites through the frame** (AC: 1, 2)
  - [x] [HtmlRenderAdapter.Dashboard.cs](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs): Git Pulse panel (heatmap window/why now by-construction), and any other insight panel that lacked a why/window. Panels with a `chart-panel-header-row` "View …" link keep that header; adopt only the missing slots.
  - [x] [DeepAnalyticsTemplater.cs](src/SpecScribe/DeepAnalyticsTemplater.cs): Change Coupling, Ranked Pairs, Git Hotspots → title/window/ranking/why from `ChartMeta`. Remove the now-duplicated hand-rolled `deep-page-lead`/`deep-page-note`/`coupling-legend` copy that the frame subsumes (keep the coupling graph's node/edge legend — it's chart-intrinsic).
  - [x] [GitInsightsTemplater.cs](src/SpecScribe/GitInsightsTemplater.cs): files table metric caption + activity window via the frame.
  - [x] Leave the already-compliant charts (sunburst/donut/funnel/req-flow) as-is unless routing their title/why through the frame is a clean, byte-reviewable change.

- [x] **Task 6 — Tests** (AC: 1, 2)
  - [x] [ChartsTests.cs](tests/SpecScribe.Tests/ChartsTests.cs): heatmap legend now carries real count ranges (assert the range text, not "Less"/"More"); the `More.` assertion around line 952 and any "Less"/"More" expectation must be updated to the new legend. Add: legend ranges match `HeatLevel` buckets (drive both from the shared range helper); low-data/uniform-history legend degrades sanely; window caption present with weeks + span. Add `Framed`/`ChartMeta` unit tests: each slot renders when supplied and is omitted when null; all slots HTML-escaped; a chart with no window/ranking still renders cleanly.
  - [x] [GitMetricsTests.cs](tests/SpecScribe.Tests/GitMetricsTests.cs): `AnalyzedCommits` reflects the parsed commit count (and the `-n 300` cap semantics).
  - [x] Deep-analytics/git-insights render tests (mirror the existing templater test fixtures): hotspots/pairs carry the metric + numeric window; no "recent history" literal remains; one `chart-frame-why` per insight chart; no framework-specific string in any framing sentence.
  - [x] **Anti-duplication guard (the AC2 teeth):** a test (or a grep-style assertion) proving the framing/ranking slots come from the shared source — e.g. the "why" sentences are defined once. At minimum, assert the deep-analytics page no longer contains the old hand-rolled `deep-page-note`/`deep-page-lead` strings that moved into the frame.
  - [x] **Golden + parity:** dashboard charts render through `HtmlRenderAdapter` → the heatmap/frame markup changes on `index.html` (and anywhere the Git Pulse panel appears) → **regenerate the committed golden fingerprints** and eyeball the diff to confirm it's only the chart-metadata change ([see golden-diff-normalization-gotchas]). Keep `RenderParity` green (the frame is shared `Charts` output, identical across HTML/webview/SPA — no new `HostRenderException` should be needed). Run the webview + SPA suites.

- [x] **Task 7 — Verify end-to-end on the real repo** (AC: 1, 2)
  - [x] `dotnet run` a full generate **with** `--deep-git` (Insights group + deep pages exist per [Story 10.1]): open `index.html`, `git-insights.html`, `deep-analytics.html` in the preview browser. Confirm: heatmap legend shows real count ranges + a numeric window; hotspots/pairs state "top N of M by …"; every insight chart carries one why-sentence; no "Less … More", no bare "recent history".
  - [x] Full generate **without** `--deep-git`: the baseline Git Pulse heatmap still shows the real-value legend + window + why; no deep pages (correct — data-gated).
  - [x] Confirm the same frame markup renders in the webview (`specscribe webview`) and `--spa` (grouped nav story proved the shared-render seam; this rides it).

## Dev Notes

### Architecture patterns & constraints (must follow)

- **Charts are pure inline SVG/CSS, no info-bearing JS** ([see charting-is-pure-svg-no-js], [Charts.cs:6-8](src/SpecScribe/Charts.cs) header). Every metadata slot is static text rendered at generation time. The one sanctioned tooltip/sort script is not extended here.
- **The frame is the AC2 contract.** After this story, there must be exactly **one** source of each metadata slot's markup and copy. "Per-chart copy" is the failure mode — the reviewer will grep for a second hand-rolled "why this matters" `<p>` or a duplicated ranking string. Route through `Charts.Framed` / the shared why-source.
- **Extend the heatmap, don't fork it.** Reuse `level-0..4` swatches and the `HeatLevel` thresholds ([spec-commit-heatmap-contrast-and-day-drilldown](_bmad-output/implementation-artifacts/spec-commit-heatmap-contrast-and-day-drilldown.md) is frozen — extend its _rendering_, don't edit the spec). The legend ranges must be **derived from** `HeatLevel`, not hand-copied, so cell shade and legend range can't drift apart.
- **Never color/size-only** — the real-value legend puts the count range in text beside every swatch; ranking/window are text. This is the same redundancy rule the whole chart suite already honors ([ChartsTests.cs:7](tests/SpecScribe.Tests/ChartsTests.cs) documents it).
- **NFR8 — framing is framework-neutral.** Epic 10's spine is adapter-supplied, non-BMAD-specific copy. The "why" sentences describe the metric (churn, coupling, cadence), never this repo's files or BMAD vocabulary. A future framework adapter must not have to rewrite them.
- **Invariant formatting.** Windows/dates use `Charts.DReadable` / invariant `N()` (the existing discipline — [Charts.cs:1061-1067](src/SpecScribe/Charts.cs)) so a th-TH/fa-IR host doesn't emit non-Gregorian spans or grouped digits.
- **Three surfaces, one render seam, golden bytes are a gate.** Dashboard/epics charts go through `HtmlRenderAdapter` → HTML + webview + SPA get identical `Charts` output. Changing `Charts.cs` changes golden fingerprints on every page carrying the affected chart. Regenerate deliberately; keep `RenderParity` green ([see golden-diff-normalization-gotchas]). The synthesized pages (git-insights, deep-analytics) build their own shell and don't go through `RenderPage`, but any committed golden/snapshot that pins them must be regenerated too.
- **Never-throw / graceful omission (NFR2).** A null/empty slot omits its element; low/zero data degrades to a friendly note (the builders already do this — `chart-empty`). The legend must render sanely for a one-commit repo (`maxCount <= 1`, mirroring `HeatLevel`'s special case at [Charts.cs:1407](src/SpecScribe/Charts.cs)).

### Source tree — files to touch

- `src/SpecScribe/Charts.cs` — add `ChartMeta` + `Framed` + shared why-source; rework the heatmap legend into real-value ranges + window; factor `HeatLevelRange` shared with `HeatLevel` (UPDATE — the heart of the story).
- `src/SpecScribe/GitMetrics.cs` — add `DeepGitPulse.AnalyzedCommits`, set it in `TryComputeDeep`/`ParseNumstatLog` (UPDATE).
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — route Git Pulse (+ any why/window-lacking) panel through the frame slots (UPDATE).
- `src/SpecScribe/DeepAnalyticsTemplater.cs` — coupling/pairs/hotspots title+window+ranking+why from `ChartMeta`; delete the copy the frame subsumes (UPDATE).
- `src/SpecScribe/GitInsightsTemplater.cs` — files-table metric caption + activity window via the frame (UPDATE).
- `src/SpecScribe/assets/specscribe.css` — `.chart-frame-*` styles + updated heatmap-legend styling (UPDATE).
- Tests: `ChartsTests.cs`, `GitMetricsTests.cs`, `DeepAnalyticsTemplaterTests.cs`/`GitInsightsTemplaterTests.cs` (if present — otherwise add render assertions where those pages are tested); regenerate golden fingerprints across `SiteGenerator*`/webview/SPA suites (UPDATE).

### UPDATE files — current state & what must be preserved

- **`Charts.CommitHeatmap`** ([Charts.cs:484-616](src/SpecScribe/Charts.cs)): renders headline (suppressible via `showHeadline`), the SVG grid with `:target`-linked active-day cells, and the `level-0..4` legend. **Preserve:** the `showHeadline` fork (the Git Pulse panel suppresses the headline — your new window caption must still appear there), the `role="group"` vs `role="img"` fork on linked days, the per-cell `<title>` + whole-chart aria-label, the `LinkedCommitDays` shared-source contract, and the `--col` stagger. **Only the legend block (lines 608-613) and an added window caption change.**
- **`Charts.GitPulsePanel`** ([Charts.cs:625-678](src/SpecScribe/Charts.cs)): the signal strip carries the 30-day count / last-commit / active-days figures and suppresses the heatmap headline to avoid duplication. **Do not** re-introduce a duplicate count — the heatmap _window_ (grid span) is a different figure from the 30-day signal.
- **`DeepAnalyticsTemplater.RenderPage`** ([DeepAnalyticsTemplater.cs:13-114](src/SpecScribe/DeepAnalyticsTemplater.cs)): section headers, the `deep-page-lead`/`deep-page-note` sentences, the `coupling-legend`, and the `:target` expand-lightbox. **Preserve** the lightbox and the coupling graph's own node/edge legend; **relocate** the framing sentences into the shared why-source (don't leave both).
- **`GitInsightsTemplater.RenderPage`** ([GitInsightsTemplater.cs:23-66](src/SpecScribe/GitInsightsTemplater.cs)): the meta-pills already show "commits analyzed" / "top N of M files" / "contributors" and the files table caption. **Preserve** the master-detail `:target` drill-down and the guarded links; only add the metric phrase + make the window a number.
- **`DeepGitPulse`** ([GitMetrics.cs:34-44](src/SpecScribe/GitMetrics.cs)): a mutable `Insights` property is set late by `SiteGenerator` and cleared on render failure — follow that same pattern if you need to; the new `AnalyzedCommits` is a plain init-time datum from the parse.

### Testing standards

- xUnit; the chart builders are pure functions over model records — unit-test `Framed`/`ChartMeta` and the heatmap legend directly against strings (the existing [ChartsTests.cs](tests/SpecScribe.Tests/ChartsTests.cs) style). Deep-git parse via the pure `ParseNumstatLog` helper ([GitMetricsTests.cs](tests/SpecScribe.Tests/GitMetricsTests.cs)).
- Assert both **presence** (real ranges, numeric window, one why-sentence, ranking metric) and **absence** (no "Less"/"More", no bare "recent history", no second hand-rolled framing string) — the AC1/AC2 pair.
- Drive the legend ranges and the cell shades from the **same** helper in the test, so a future threshold change can't silently desync them.
- Regenerate `GoldenContentFingerprint` deliberately and diff to prove the change is chart-metadata-only ([see golden-diff-normalization-gotchas] for the footer-clock / `?v=` / subtitle normalizations the harness applies). **Heads-up:** a pre-existing `GoldenContentFingerprint` drift from spec-comment-block work has been noted in prior stories as _not_ owned by that story — confirm the baseline is green before you start so you don't inherit someone else's red, and regenerate cleanly.

### Out of scope (do not build)

- No new charts, no new chart _types_ — this standardizes the metadata around the existing ones.
- No re-tuning of the heatmap `level-*` fills or the drill-down mechanism (frozen spec — extend, don't modify).
- No glossary / acronym expansion (that's [Story 10.3](_bmad-output/planning-artifacts/epics.md), feedback T6), no date-format unification (Story 10.4, T7), no coupling process-vs-code classification or dead-zone annotation (Story 10.6, T5-adjacent).
- No author/productivity signals — deep-git stays file-path-only (PRD non-goal, preserved on `DeepGitPulse`).
- No nav / structure changes (Story 10.1 owns those).

### Project Structure Notes

- Output dir is `SpecScribeOutput` (never `docs/live`) — [see generate-output-dir-is-specscribeoutput].
- If working in a git worktree, target the worktree path (main has a background auto-committer) — [see worktree-edits-must-target-worktree-path].

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.2: Chart Metadata Standard] — the two ACs; Epic 10 FR28, UX-DR25/27/28/29/30, NFR8.
- [Source: docs/Epic3UXFeedback.md#T5] — "Charts ship without legends, time windows, or 'why this matters'"; the "top 50 of 781" and "recent history" examples; "extends, not duplicates" the heatmap spec.
- [Source: docs/MissingFeatures.md#C2] — the chart-metadata standard as an EXTENDS item; churn ≈ defect risk / coupling ≈ hidden dependency framings.
- [Source: _bmad-output/implementation-artifacts/spec-commit-heatmap-contrast-and-day-drilldown.md] — frozen; the `level-0..4` swatch contrast + `:target` drill-down this story extends.
- [Source: src/SpecScribe/Charts.cs#CommitHeatmap] — the "Less … More" legend, `HeatLevel` thresholds, window computation.
- [Source: src/SpecScribe/GitMetrics.cs] — `-n 200` / `-n 300` window bounds; `DeepGitPulse` / `GitInsightsData` records (the honest window numbers).
- [Source: src/SpecScribe/DeepAnalyticsTemplater.cs] / [GitInsightsTemplater.cs] — the per-page framing copy to consolidate; the "recent history" / "top N of M" sites.
- [Source: src/SpecScribe/HtmlRenderAdapter.Dashboard.cs] — the dashboard chart-panel call sites and `chart-panel-header-row` pattern.

## Dev Agent Record

### Agent Model Used

Composer (Auto)

### Debug Log References

- Golden fingerprint regenerated after chart-frame CSS + Git Pulse/heatmap markup; production CSS comment avoided `.vscode-` substring (webview theming guard).
- Extends frozen `spec-commit-heatmap-contrast-and-day-drilldown`: same `level-0..4` swatches; legend text now carries `HeatLevelRange` counts beside them (no parallel legend, no fill retune).

### Completion Notes List

- Introduced `Charts.ChartMeta` + `Framed` + `FrameWindowSlot`/`FrameRankingSlot`/`FrameWhySlot` + `WhyText(ChartMetric)` as the single metadata seam (AC2).
- Heatmap legend replaced "Less … More" with per-level ranges from shared `HeatThresholds`/`HeatLevelRange` (also drives `HeatLevel`); grid-span window lives in the builder so it appears with/without headline.
- `DeepGitPulse.AnalyzedCommits` set from parsed commit count; deep analytics + git insights + Git Pulse ranked lists state metric + numeric window; hand-rolled `deep-page-lead`/`deep-page-note`/`recent history` removed.
- Dashboard Git Pulse header-row kept; why/ranking/window adopted inside `GitPulsePanel` (by construction). Coupling graph node/edge legend retained as chart-intrinsic.
- Tests: Charts/GitMetrics/DeepAnalytics/GitInsights + full suite 1566 green; RenderParity green; E2E generate with/without `--deep-git` verified on real repo.

### File List

- `src/SpecScribe/Charts.cs`
- `src/SpecScribe/GitMetrics.cs`
- `src/SpecScribe/DeepAnalyticsTemplater.cs`
- `src/SpecScribe/GitInsightsTemplater.cs`
- `src/SpecScribe/assets/specscribe.css`
- `tests/SpecScribe.Tests/ChartsTests.cs`
- `tests/SpecScribe.Tests/GitMetricsTests.cs`
- `tests/SpecScribe.Tests/DeepAnalyticsTemplaterTests.cs`
- `tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs`
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs`
- `_bmad-output/implementation-artifacts/10-2-chart-metadata-standard.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-18: Story 10.2 implemented — shared chart-frame metadata (legend ranges, numeric windows, ranking metrics, why-sentences); status → review.
