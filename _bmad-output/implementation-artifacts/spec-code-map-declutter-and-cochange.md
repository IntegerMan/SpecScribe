---
title: 'Code Map: declutter tiles, rich tooltips, and a co-change dimension'
type: 'feature'
created: '2026-07-13'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: 19553fe8404e152b3adc7d11f243f63fcfcb3eb2
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The `code-map.html` treemap prints a clipped text label on every wide-enough directory rect, which crowds the boxes and competes with the color signal; the hover tooltip is only plain multi-line text; and there is no way to see how "coupled" a file is (how much else tends to change with it).

**Approach:** Remove directory text labels entirely, at every depth — moving all per-file/per-directory identity into the tooltip and text table; upgrade the shared tip node to render a styled HTML card for treemap cells (dates via the portal's human-readable token, not raw ISO); and add a fifth colorize dimension — the average number of *other* files changed in the same commits as each file — computed in the existing single deep-git parse and surfaced in the tooltip, table, legend, and dimension switch.

*(Renegotiated 2026-07-13, round 2: the human owner found the label-reserved header gutter still rendered as a blank, uncolored band even after nested labels were suppressed — since `.codemap-dir` has no fill, the reserved space for the (now-removed) label text showed through as visual noise. Resolution: remove labels at ALL depths, including the top-level rects that originally kept one, and shrink the directory inset from an asymmetric 16px header + 2px pad to a uniform 3px gutter on all sides. Also requested: tooltip/table dates use the app's standard human-readable format, not raw ISO.)*

*(Renegotiated 2026-07-13, round 3: the human owner asked for the colorize control as a dropdown instead of a radio list, "Churn" added as a colorize dimension, and two checkboxes to exclude (a) spec-driven development directories and (b) tests from the treemap. Resolution: the colorize radios became a `<select>`; churn reuses the already-emitted `data-churn` attribute. The exclude filters required genuine re-tiling — not just hiding cells, which would recreate the round-2 blank-area problem — so the generator precomputes all four filter combinations (`CodeMap.BuildVariants`) server-side and the page renders four self-contained panels, toggled by two checkboxes via pure CSS sibling selectors (no JavaScript needed for the toggle itself). "Spec-driven development directories" = `.agents/`, `.claude/`, `_bmad/`, `_bmad-output/`, and `.github/agents/` (a GitHub Copilot mirror of the same BMad agent definitions — found and fixed during browser verification, since `.github/workflows/` is legitimate CI config and must stay). "Tests" = any path segment (directory or file name) containing "test" case-insensitively.)*

## Boundaries & Constraints

**Always:**
- Accumulate co-change inside the existing `BuildCodeMapMetrics` over the records `ParseNumstatLog` already folds — no second `git log`, no new git module. [[deep-git-single-numstat-path]]
- Everything works with JS off (server treemap + legend + full text table, incl. the new column); JS only enhances. Color is never the sole signal — the card and table carry every metric as text. [[charting-is-pure-svg-no-js]]
- Keep the ramp off `--status-*`; reuse the `.codemap-cell level-N` heatmap ramp. [[specscribe-status-token-system]]
- The HTML tooltip is opt-in per element (`data-tip-html`); every existing plain-text `data-tip`/`<title>` tip must render exactly as before. Card content is server-built, dynamic parts escaped via `PathUtil.Html`.
- Round 3: the exclude-filter checkboxes must genuinely re-tile the treemap (compute a fresh squarified layout for the filtered file set) — never just hide cells while leaving the layout geometry unchanged, which would recreate the round-2 blank-area problem at filter scale.

**Ask First:**
- Dropping directory boundary strokes entirely (boundaries stay even with no labels — AC #1 "clear boundaries").
- Changing the co-change definition (denominator, cap, solo-commit handling) from the I/O matrix.
- Changing which directories count as "spec-driven development" or what "test" matches (round 3, see I/O matrix).

**Never:**
- Do not route co-change color through `--status-*`; do not build a second tooltip node (reuse `.ss-tooltip`). [[tooltip-clipping-use-ss-tooltip-node]]
- Do not add incremental treemap regeneration or edit `docs/live/specscribe.css` — only the embedded `src/SpecScribe/assets/specscribe.css`. [[generate-output-dir-is-specscribeoutput]]
- Round 3: do not port the squarified layout algorithm to client-side JS (two implementations of the same algorithm risk silently diverging); do not make the exclude-filter toggle itself depend on JavaScript — it must work with JS off.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Any directory rect | Any `TreemapRect.Depth`, is directory | Boundary rect drawn, **no** `<text>` label at any depth | N/A |
| Co-change average | File touched by commits with sizes {1, 3, 6} (all ≤ cap) | avg other files = ((1-1)+(3-1)+(6-1))/3 = 7/3 ≈ 2.3 | N/A |
| Bulk commit present | File also in a 200-file commit (> cap) | That commit excluded from BOTH numerator and denominator | N/A |
| File only ever alone | Every touching commit has file set size 1 | avg = 0 (contributes, denominator counts solo commits) | N/A |
| No git record for file | metrics null / `--deep-git` off | No co-change value; `data-cochanged` omitted; card/table show "—"; cell neutral on this dimension | Degrades to neutral |
| Hover treemap cell (JS on) | Cell has `data-tip-html` | `.ss-tooltip` renders the styled card via innerHTML | N/A |
| Hover other chart segment | Element has plain `data-tip` or `<title>` | Renders plain text via textContent, unchanged | N/A |
| Path under `.agents/`, `.claude/`, `_bmad/`, `_bmad-output/`, or `.github/agents/` | "Exclude spec-driven development directories" checked | File dropped from that panel's tree; layout re-tiles | N/A |
| Path with any segment containing "test" (case-insensitive) | "Exclude tests" checked | File/directory-and-descendants dropped; layout re-tiles | N/A |
| Both filters checked | | The intersection (files failing BOTH predicates) shown | N/A |
| A filter combination excludes every file | E.g. a repo that is 100% tests | That panel shows "No files match this filter." instead of an empty treemap | Never throws, never a blank SVG |
| Filter checkboxes with JS off | No script runs | Checking a box still swaps the visible panel (pure CSS) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/GitMetrics.cs` -- `CodeFileMetrics` record (add `double? AvgCoChanged`); `BuildCodeMapMetrics` (accumulate co-change over non-bulk commits, `CouplingFileSetCap` already here); `CodeMapAccum`.
- `src/SpecScribe/CodeMap.cs` -- squarified layout inset (round 2: uniform 3px `Pad`); round 3: `IsSpecDevPath`/`IsTestPath` pure predicates + `CodeMapVariant` record + `CodeMap.BuildVariants` (computes all four filter-combination `Build`+`Layout` pairs in one pass).
- `src/SpecScribe/Charts.cs` -- `AppendTreemapDir` (no label, boundary only); `AppendTreemapFile`/`BuildTreemapCard` (rich card, `data-tip-html`, `DReadable` dates); round 3: dropped the SVG's `id="codemap-svg"` (up to four render per page now — a duplicate id is invalid HTML).
- `src/SpecScribe/SiteGenerator.cs` -- round 3: `WriteCodeMap` now calls `CodeMap.BuildVariants` once and gates the page/nav on the "full" variant only.
- `src/SpecScribe/CodeMapTemplater.cs` -- round 3: `RenderPage` takes `IReadOnlyList<CodeMapVariant>`; colorize control is a `<select>` (was radios) with a new "Churn" option; two pure-CSS filter checkboxes + four `AppendVariantPanel` panels (each a self-contained `.codemap-view` — treemap card and table card as sibling `.chart-panel`s, never nested).
- `src/SpecScribe/assets/specscribe.js` -- round 2: `data-tip-html` tip branch, `cochange` dim. Round 3: `churn` dim; `initCodeMap` refactored from a single-SVG IIFE into `initCodeMapPanel(panel)`, called once per `.codemap-view` (every lookup scoped via `panel.querySelector`, no `getElementById`); dropdown wiring (`select.addEventListener("change", ...)`) replaces the old radio-group form listener.
- `src/SpecScribe/assets/specscribe.css` -- round 2: `.ss-tooltip` card styles, dropped `.codemap-dir-label`. Round 3: `.codemap-dim-select` (dropdown), `.codemap-filter-checkbox`/`-label`, and the `#cm-exclude-spec`/`#cm-exclude-tests` sibling-combinator rules that show exactly one `.codemap-view` panel.
- `tests/SpecScribe.Tests/CodeMapTests.cs` -- round 3: `IsSpecDevPath`/`IsTestPath` theory tests (incl. the `.github/agents/` vs `.github/workflows/` distinction found during browser verification) + `BuildVariants` tests.
- `tests/SpecScribe.Tests/CodeMapTemplaterTests.cs`, `ChartsTests.cs`, `SiteGeneratorCodeMapTests.cs` -- rewritten for the `CodeMapVariant`-based `RenderPage` signature; new dropdown/churn/four-panel/checkbox assertions.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- golden inventory + whole-site content fingerprint (regenerated each round). [[golden-diff-normalization-gotchas]]

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/GitMetrics.cs` -- Added `double? AvgCoChanged` to `CodeFileMetrics`; `BuildCodeMapMetrics` accumulates `(distinctFileCount - 1)` + a qualifying-commit counter per non-bulk commit (distinct set ≤ `CouplingFileSetCap`), `AvgCoChanged = total / qualifyingCommits` (null when 0). Solo commits contribute 0 and count in the denominator; bulk (> cap) excluded.
- [x] `src/SpecScribe/Charts.cs` -- `AppendTreemapDir` draws boundary rects only, no `<text>` at any depth. `AppendTreemapFile` emits `data-cochanged` (when present) and `data-tip-html` (was `data-tip`). `BuildTreemapTip` → `BuildTreemapCard`: name heading, mono path, `<dl>` metric rows incl. **files changed together** and `DReadable`-formatted first/last dates (rows present only when the metric exists).
- [x] `src/SpecScribe/CodeMap.cs` -- Replaced the 16px header + 2px pad inset with a uniform 3px inset on all sides (round 2: no label means no reserved header band; the old asymmetric inset showed through as an unfilled "blank" strip since `.codemap-dir` has no fill).
- [x] `src/SpecScribe/CodeMapTemplater.cs` -- Added the `cochange` radio and the "Together" column (`N1`, em-dash fallback); First/Last cells now use `PortalDates.Day` (was raw ISO `Charts.D`).
- [x] `src/SpecScribe/assets/specscribe.js` -- Shared tip `activate`/`showTip` gained a `data-tip-html` → `innerHTML` branch (all other tips unchanged). Treemap `metricFor`/`DIM_LABELS` gained `cochange`; `recolor` no longer mutates the tooltip (static card), still refreshes aria-label + legend.
- [x] `src/SpecScribe/assets/specscribe.css` -- Styled `.codemap-card*` inside `.ss-tooltip` (heading, mono path, two-column `<dl>` grid); removed the dead `.codemap-dir-label` rule (round 2).
- [x] `src/SpecScribe/CodeMap.cs` -- Round 3: `IsSpecDevPath`/`IsTestPath` pure predicates; `CodeMapVariant(Key, ExcludesSpecDev, ExcludesTests, Map, Layout)`; `BuildVariants` computes all four combinations (`full`/`no-spec`/`no-tests`/`no-spec-no-tests`), each its own `Build`+`Layout` call so a filtered view genuinely re-tiles.
- [x] `src/SpecScribe/SiteGenerator.cs` -- Round 3: `WriteCodeMap` calls `BuildVariants` once, gates on the `full` variant, passes all four to `CodeMapTemplater.RenderPage`.
- [x] `src/SpecScribe/CodeMapTemplater.cs` -- Round 3: colorize control is now a `<select>` with a "Churn" option added; two unwrapped `<input type=checkbox>`+`<label for>` filter toggles; `AppendVariantPanel` renders each of the four panels as sibling `.chart-panel` cards (treemap + table), with a "No files match this filter." notice when a combination filters down to nothing.
- [x] `src/SpecScribe/assets/specscribe.js` -- Round 3: `churn` added to `metricFor`/`DIM_LABELS`; `initCodeMap` refactored to `initCodeMapPanel(panel)`, invoked once per `.codemap-view` via `querySelectorAll` + `forEach` (no global ids); dropdown `change` listener replaces the radio-group form listener.
- [x] `src/SpecScribe/assets/specscribe.css` -- Round 3: dropdown + checkbox styles; the `#cm-exclude-spec`/`#cm-exclude-tests` sibling-combinator rules toggling exactly one `.codemap-view`.
- [x] `tests/SpecScribe.Tests/*` -- Co-change math tests; no-label-at-any-depth + rich-card tests; round 3: `IsSpecDevPath`/`IsTestPath`/`BuildVariants` tests, rewritten templater tests for the variant-list `RenderPage` signature, dropdown/churn/checkbox/four-panel assertions. **Full suite green: 1053/1053, golden fingerprint regenerated and stable.**

**Acceptance Criteria:**
- Given a deep-git run, when I open the code map, then no directory rect at any depth carries a text label, boundary rects are still drawn at every depth, and every directory/file is identifiable via its tooltip or the text table.
- Given I hover a file cell, when the tooltip appears, then it is a styled card showing the path and all available metrics as labeled text (dates in the portal's readable format), including "files changed together".
- Given deep-git metrics exist, when I open the colorize dropdown, then I can pick "Churn" or "Files changed together" among six dimensions; cells recolor on the sequential ramp, the legend and aria-labels name the active dimension, and the text table shows the per-file value.
- Given `--deep-git` is off or a file has no git record, when the code map renders, then no co-change value is shown for it (card/table "—", neutral fill) and generation still succeeds.
- Given JS is disabled, when I load the page, then the treemap, legend, full text table, AND the two exclude-filter checkboxes all work correctly with no script dependency — only the colorize dropdown and directory zoom require JS.
- Given I check "Exclude spec-driven development directories" and/or "Exclude tests", when the corresponding panel becomes visible, then it shows a genuinely re-tiled treemap (no leftover blank gaps) containing only the surviving files, with a text note of what was excluded.

## Design Notes

Co-change denominator is *all* non-bulk commits touching the file (solo commits count as 0), so it reads as "typical blast radius per change," not "blast radius when not alone." `CouplingFileSetCap` (50) already lives in `GitMetrics`, so the exclusion stays local to `BuildCodeMapMetrics`.

The card is per-element opt-in: only treemap cells set `data-tip-html`; the shared node's `textContent` branch stays the default for every other chart, so sunburst/heatmap/coupling tips can't regress. Because the card lists all metrics as text, AC #4 holds for any active dimension without per-dimension tooltip rewriting — so `recolor` stops mutating tooltip text (still refreshes aria-label + legend). Card = file-name heading, mono path, and a `<dl>` of the metric rows (each "—" when absent).

**Escaping:** the card is built with dynamic parts `Html`-escaped, then the whole card is `Html`-escaped again into `data-tip-html`. The browser decodes the attribute once (getAttribute) and the HTML parser decodes again (innerHTML), so the double-escape is exactly right — verified live in a browser (attribute → real card DOM, all rows present).

**Round 2 — the "blank areas" root cause:** removing only nested `<text>` elements (round 1) didn't remove the space reserved FOR them. `CodeMap.cs`'s squarified layout always insets a directory's children by `DirHeader`(16px) at the top + `Pad`(2px) elsewhere, regardless of whether a label renders into that space — and `.codemap-dir` has `fill: none`, so an unlabeled reserved band shows through as visually blank (uncolored) parchment. Fix: once no directory anywhere carries a label, the header reservation is pointless — collapsed to a uniform 3px gutter on all sides (verified live: the gutter above every directory's children measures exactly 3px in the rendered SVG, down from the old 16px top band).

**Round 2 — dates:** the card and table First/Last previously used `Charts.D` (the raw ISO machine token, `PortalDates.IsoDay`, e.g. "2026-07-05") — fine for URLs/filenames but not meant for human reading. Card now uses `Charts.DReadable` (`PortalDates.DayWithWeekday`, e.g. "Mon, Jul 5, 2026") — its own doc comment calls out "tooltips" as its designated use. The table uses plain `PortalDates.Day` ("Jul 5, 2026", no weekday) since a dense multi-column table favors compactness over the weekday prefix.

**Golden fingerprint:** a background auto-committer landed several commits mid-session (`537ff8c`, `a5aca55`, `2903b9f`, `85b5491`) that bundled this story's round-1/round-2 code together with concurrent "entity prev/next navigation" and "prism.js" stories and reconciled the tree (see [[worktree-edits-must-target-worktree-path]]). Round 3 landed once `main` was uncontested; the fingerprint was regenerated directly each time it drifted and is now stable at `06702d484947448f406982fee45922a1c4dc79315207707a74bbdd16b0933058`.

**Round 3 — why four precomputed panels, not client-side relayout:** the exclude checkboxes need the treemap to genuinely re-tile (freed space must be reclaimed, or excluding a large directory just recreates round 2's blank-area bug at filter scale). The codebase's established principle is "layout computed once in C#, no client layout math" (repeated throughout Story 7.6's own notes). Porting the squarified algorithm to JS would violate that, double the surface area for layout bugs (two implementations to keep in sync), AND make the filter depend on JavaScript. Instead, `CodeMap.BuildVariants` computes all four filter combinations server-side in one pass; the page ships four self-contained panels and a pure-CSS sibling-combinator toggle (`#cm-exclude-spec:checked ~ #cm-exclude-tests:checked ~ .codemap-view[data-view="no-spec-no-tests"] { display: block; }`) — the ONLY feature on this page that needs zero JavaScript, not even as a progressive enhancement.

**Round 3 — no shared ids across panels:** with up to four copies of the same markup shape on one page, nothing that used to be `id="codemap-svg"`/`id="codemap-controls"`/`id="codemap-legend-dim"`/`id="codemap-breadcrumb"` can keep that id (duplicate ids are invalid HTML and `getElementById` would only ever find the first). Every one of those became a class, and `specscribe.js`'s `initCodeMap` IIFE became `initCodeMapPanel(panel)`, called once per `.codemap-view` via `querySelectorAll`+`forEach`, with every internal lookup scoped through `panel.querySelector(...)`.

**Round 3 — "spec-driven development directories" scope:** initially `.agents/`, `.claude/`, `_bmad/`, `_bmad-output/` (the repo's own BMad Method scaffolding, inferred from the directory listing + the original screenshot showing them dominating the treemap). Browser verification caught a gap: `.github/agents/` mirrors the same BMad agent definitions for GitHub Copilot, while `.github/workflows/` (CI config) is genuinely not spec-dev — so the filter needed a sub-path exclusion, not a blanket `.github/` exclusion, added as its own prefix in `SpecDevPathPrefixes`.

## Verification

**Commands:**
- `dotnet test SpecScribe.slnx` -- result: **1053/1053 green**, golden fingerprint stable at `06702d484947448f406982fee45922a1c4dc79315207707a74bbdd16b0933058`.
- `dotnet run --project src/SpecScribe -- generate --deep-git` then open `code-map.html` -- result (verified live in-browser, round 2): zero `<text>` elements in the SVG, 205 directory boundary rects still drawn, the top gutter above every directory's children measures 3px, hovering a cell renders the styled card, "Files changed together" recolors the map + updates the legend, the "All files" table has a "Together" column with readable dates.
- Round 3 (verified live in-browser): zero duplicate `id`s across the 4 panels; only the "full" panel visible by default; checking "Exclude spec-driven development directories" alone shows the "no-spec" panel (847→243 cells, 94.9% SVG area coverage — a genuine re-tile, not a blank void); checking both shows "no-spec-no-tests" (160 cells, zero spec-dev/test path leaks after the `.github/agents/` fix); the dropdown's "Churn" option recolors the currently-visible panel and updates its legend/aria-labels correctly, scoped to that panel only.

**Manual checks:**
- Confirm a sweeping bulk commit does not inflate co-change for the files it touched (spot-check a file also edited in a large commit against the table value).

## Suggested Review Order

**Co-change metric (data layer)**

- Entry point — the new metric's shape on the per-file record (nullable; null when no non-bulk commit touched the file).
  [`GitMetrics.cs:85`](../../src/SpecScribe/GitMetrics.cs#L85)

- The averaging: credit `(distinctFiles − 1)` per non-bulk commit, solo commits count as 0, bulk (> cap) excluded.
  [`GitMetrics.cs:777`](../../src/SpecScribe/GitMetrics.cs#L777)

**Treemap render (declutter + rich card)**

- Round 2 — no directory label at any depth; boundaries still drawn everywhere.
  [`Charts.cs:1340`](../../src/SpecScribe/Charts.cs#L1340)

- Round 2 — the actual "blank areas" root cause: the label's reserved header space, not the label text itself. Collapsed to a uniform 3px inset now that no label needs room.
  [`CodeMap.cs:234`](../../src/SpecScribe/CodeMap.cs#L234)

- The stylized card builder — dynamic parts escaped once here; the attribute re-escapes for the innerHTML round-trip; dates via `DReadable` (round 2).
  [`Charts.cs:1430`](../../src/SpecScribe/Charts.cs#L1430)

- The rect now carries `data-tip-html` (was `data-tip`) plus `data-cochanged`.
  [`Charts.cs:1396`](../../src/SpecScribe/Charts.cs#L1396)

**Shared tooltip node (highest blast radius)**

- Additive `data-tip-html` → `innerHTML` branch; every existing plain-text tip path is untouched.
  [`specscribe.js:37`](../../src/SpecScribe/assets/specscribe.js#L37)

- New `cochange` colorize dimension; `recolor` no longer mutates the (now-static) tooltip.
  [`specscribe.js:394`](../../src/SpecScribe/assets/specscribe.js#L394)

**Controls, table, styles**

- The `cochange` radio and the "Together" text-table column (no-JS truth of the metric); First/Last now via `PortalDates.Day` (round 2).
  [`CodeMapTemplater.cs:164`](../../src/SpecScribe/CodeMapTemplater.cs#L164)

- The card CSS (two-column `<dl>` grid) inside the shared `.ss-tooltip`.
  [`specscribe.css:3288`](../../src/SpecScribe/assets/specscribe.css#L3288)

**Tests**

- Co-change math: bulk exclusion, solo=0, null-when-only-bulk.
  [`GitMetricsTests.cs:700`](../../tests/SpecScribe.Tests/GitMetricsTests.cs#L700)

- Label suppression + rich-card/`data-cochanged` render.
  [`ChartsTests.cs:927`](../../tests/SpecScribe.Tests/ChartsTests.cs#L927)

**Round 3 — exclude filters (entry point for this round)**

- The filter predicates — the `.github/agents/` vs `.github/workflows/` distinction is the one subtlety here.
  [`CodeMap.cs:107`](../../src/SpecScribe/CodeMap.cs#L107)

- `BuildVariants` — the single place all four filter combinations are computed, each with its own genuine re-tile.
  [`CodeMap.cs:209`](../../src/SpecScribe/CodeMap.cs#L209)

- `WriteCodeMap` now builds variants once and gates on the "full" one.
  [`SiteGenerator.cs:2333`](../../src/SpecScribe/SiteGenerator.cs#L2333)

- `RenderPage`'s new signature + the four-panel render loop; the checkboxes must stay unwrapped siblings of the panels.
  [`CodeMapTemplater.cs:23`](../../src/SpecScribe/CodeMapTemplater.cs#L23)

- The colorize control is now a `<select>` (was radios) with "Churn" added.
  [`CodeMapTemplater.cs:86`](../../src/SpecScribe/CodeMapTemplater.cs#L86)

- The pure-CSS sibling-combinator toggle — the only feature on this page needing zero JavaScript.
  [`specscribe.css:2567`](../../src/SpecScribe/assets/specscribe.css#L2567)

- `initCodeMap` → `initCodeMapPanel(panel)`, invoked once per panel; every lookup is now scoped, not global-id-based.
  [`specscribe.js:368`](../../src/SpecScribe/assets/specscribe.js#L368)

**Round 3 tests**

- Filter predicates + `BuildVariants` (including the all-excluded and empty-input edge cases).
  [`CodeMapTests.cs`](../../tests/SpecScribe.Tests/CodeMapTests.cs)

- Four-panel render + checkbox markup + dropdown/churn assertions.
  [`CodeMapTemplaterTests.cs`](../../tests/SpecScribe.Tests/CodeMapTemplaterTests.cs)
