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

**Approach:** Keep directory labels only on the outermost (top-level project) rects, moving all per-file identity into the tooltip; upgrade the shared tip node to render a styled HTML card for treemap cells; and add a fifth colorize dimension — the average number of *other* files changed in the same commits as each file — computed in the existing single deep-git parse and surfaced in the tooltip, table, legend, and dimension switch.

## Boundaries & Constraints

**Always:**
- Accumulate co-change inside the existing `BuildCodeMapMetrics` over the records `ParseNumstatLog` already folds — no second `git log`, no new git module. [[deep-git-single-numstat-path]]
- Everything works with JS off (server treemap + legend + full text table, incl. the new column); JS only enhances. Color is never the sole signal — the card and table carry every metric as text. [[charting-is-pure-svg-no-js]]
- Keep the ramp off `--status-*`; reuse the `.codemap-cell level-N` heatmap ramp. [[specscribe-status-token-system]]
- The HTML tooltip is opt-in per element (`data-tip-html`); every existing plain-text `data-tip`/`<title>` tip must render exactly as before. Card content is server-built, dynamic parts escaped via `PathUtil.Html`.

**Ask First:**
- Removing labels from anything other than nested directory rects, or dropping directory boundary strokes.
- Changing the co-change definition (denominator, cap, solo-commit handling) from the I/O matrix.

**Never:**
- Do not route co-change color through `--status-*`; do not build a second tooltip node (reuse `.ss-tooltip`). [[tooltip-clipping-use-ss-tooltip-node]]
- Do not add incremental treemap regeneration or edit `docs/live/specscribe.css` — only the embedded `src/SpecScribe/assets/specscribe.css`. [[generate-output-dir-is-specscribeoutput]]

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Nested directory rect | `TreemapRect.Depth > 0`, is directory | Boundary rect drawn, **no** `<text>` label | N/A |
| Top-level directory rect | `TreemapRect.Depth == 0`, is directory | Boundary rect + `<text>` label (existing truncation) | N/A |
| Co-change average | File touched by commits with sizes {1, 3, 6} (all ≤ cap) | avg other files = ((1-1)+(3-1)+(6-1))/3 = 7/3 ≈ 2.3 | N/A |
| Bulk commit present | File also in a 200-file commit (> cap) | That commit excluded from BOTH numerator and denominator | N/A |
| File only ever alone | Every touching commit has file set size 1 | avg = 0 (contributes, denominator counts solo commits) | N/A |
| No git record for file | metrics null / `--deep-git` off | No co-change value; `data-cochanged` omitted; card/table show "—"; cell neutral on this dimension | Degrades to neutral |
| Hover treemap cell (JS on) | Cell has `data-tip-html` | `.ss-tooltip` renders the styled card via innerHTML | N/A |
| Hover other chart segment | Element has plain `data-tip` or `<title>` | Renders plain text via textContent, unchanged | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/GitMetrics.cs` -- `CodeFileMetrics` record (add `double? AvgCoChanged`); `BuildCodeMapMetrics` (accumulate co-change over non-bulk commits, `CouplingFileSetCap` already here); `CodeMapAccum`.
- `src/SpecScribe/Charts.cs` -- `AppendTreemapDir` (label only `Depth == 0`); `AppendTreemapFile` (emit `data-cochanged`, swap `data-tip` → `data-tip-html`); `BuildTreemapTip` → build the styled HTML card.
- `src/SpecScribe/CodeMapTemplater.cs` -- `AppendControls` (new "Files changed together" radio); `AppendFileTable` (new "Together" column, header + cell + em-dash fallback).
- `src/SpecScribe/assets/specscribe.js` -- tip `activate`/`showTip` (opt-in `data-tip-html` → innerHTML branch); treemap `metricFor`/`DIM_LABELS`/`recolor` (add `cochange` dim; drop dead `data-tip` mutation since the card is static, keep aria-label + legend refresh).
- `src/SpecScribe/assets/specscribe.css` -- `.ss-tooltip` card styles (heading, mono path, metric rows); confirm `.codemap-dir-label` unaffected.
- `tests/SpecScribe.Tests/GitMetricsTests.cs`, `ChartsTests.cs`, `CodeMapTemplaterTests.cs`, `SiteGeneratorCodeMapTests.cs` -- co-change math, label-suppression, card markup, new column.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- golden inventory + whole-site content fingerprint (regenerate after the HTML/CSS change). [[golden-diff-normalization-gotchas]]

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/GitMetrics.cs` -- Added `double? AvgCoChanged` to `CodeFileMetrics`; `BuildCodeMapMetrics` accumulates `(distinctFileCount - 1)` + a qualifying-commit counter per non-bulk commit (distinct set ≤ `CouplingFileSetCap`), `AvgCoChanged = total / qualifyingCommits` (null when 0). Solo commits contribute 0 and count in the denominator; bulk (> cap) excluded.
- [x] `src/SpecScribe/Charts.cs` -- `AppendTreemapDir` labels only `Depth == 0`; boundary rects at all depths. `AppendTreemapFile` emits `data-cochanged` (when present) and `data-tip-html` (was `data-tip`). `BuildTreemapTip` → `BuildTreemapCard`: name heading, mono path, `<dl>` metric rows incl. **files changed together** (rows present only when the metric exists).
- [x] `src/SpecScribe/CodeMapTemplater.cs` -- Added the `cochange` radio and the "Together" column (`N1`, em-dash fallback) to `AppendFileTable`.
- [x] `src/SpecScribe/assets/specscribe.js` -- Shared tip `activate`/`showTip` gained a `data-tip-html` → `innerHTML` branch (all other tips unchanged). Treemap `metricFor`/`DIM_LABELS` gained `cochange`; `recolor` no longer mutates the tooltip (static card), still refreshes aria-label + legend.
- [x] `src/SpecScribe/assets/specscribe.css` -- Styled `.codemap-card*` inside `.ss-tooltip` (heading, mono path, two-column `<dl>` grid).
- [x] `tests/SpecScribe.Tests/*` -- Added co-change math tests (bulk exclusion / solo=0 / null-when-no-qualifying), the label-suppression + rich-card `CodeTreemap` tests, and the "Together" column templater assertions. **Full suite green in an isolated baseline worktree: 1020 pass, only the whole-site golden fingerprint regenerates (see note below).**

**Acceptance Criteria:**
- Given a deep-git run, when I open the code map, then nested directory rects carry no text label while top-level project rects still do, and every file is identifiable via its tooltip.
- Given I hover a file cell, when the tooltip appears, then it is a styled card showing the path and all available metrics as labeled text, including "files changed together".
- Given deep-git metrics exist, when I pick the "Files changed together" colorize option, then cells recolor on the sequential ramp by their average co-change value, the legend reads "Colorized by files changed together", aria-labels name that dimension, and the text table shows the per-file value.
- Given `--deep-git` is off or a file has no git record, when the code map renders, then no co-change value is shown for it (card/table "—", neutral fill) and generation still succeeds.
- Given JS is disabled, when I load the page, then the treemap, legend, and the full text table (with the "Together" column) render correctly with no tooltip dependency.

## Design Notes

Co-change denominator is *all* non-bulk commits touching the file (solo commits count as 0), so it reads as "typical blast radius per change," not "blast radius when not alone." `CouplingFileSetCap` (50) already lives in `GitMetrics`, so the exclusion stays local to `BuildCodeMapMetrics`.

The card is per-element opt-in: only treemap cells set `data-tip-html`; the shared node's `textContent` branch stays the default for every other chart, so sunburst/heatmap/coupling tips can't regress. Because the card lists all metrics as text, AC #4 holds for any active dimension without per-dimension tooltip rewriting — so `recolor` stops mutating tooltip text (still refreshes aria-label + legend). Card = file-name heading, mono path, and a `<dl>` of the metric rows (each "—" when absent).

**Escaping:** the card is built with dynamic parts `Html`-escaped, then the whole card is `Html`-escaped again into `data-tip-html`. The browser decodes the attribute once (getAttribute) and the HTML parser decodes again (innerHTML), so the double-escape is exactly right — verified live in a browser (attribute → real card DOM, all rows present).

**Golden fingerprint / concurrent-session note:** during implementation a *second session* was actively landing an unrelated "entity prev/next navigation" feature (`EntityPager.cs`, many page-template edits) into this shared `main` checkout, so the tree did not compile and the whole-site golden fingerprint was a moving target that both sessions touch. This slice was therefore validated in an **isolated worktree at baseline `19553fe` + only these 8 files** → 1020 tests pass, and the fingerprint regenerates cleanly to `b768cada378d0fe6a10550ccd5475448aa1f7abfee991ca04d486b967bc04d50` (correct for this change alone). The fingerprint constant in `SiteGeneratorAdapterTests.cs` was **not** written from here — it is a contested shared artifact and needs one final **uncontested** regeneration once the concurrent work lands and the tree compiles (the documented pattern for this repo).

## Verification

**Commands:**
- `dotnet test SpecScribe.slnx` -- expected: full suite green (co-change math, label suppression, card markup, table column; golden fingerprint regenerated once and stable).
- `dotnet run --project src/SpecScribe -- generate --deep-git` then open `SpecScribeOutput/code-map.html` -- expected: nested tiles unlabeled, top-level labeled; hovering a cell shows the styled card; "Files changed together" recolors the map and updates the legend; the "All files" table has a "Together" column.

**Manual checks:**
- Confirm a sweeping bulk commit does not inflate co-change for the files it touched (spot-check a file also edited in a large commit against the table value).

## Suggested Review Order

**Co-change metric (data layer)**

- Entry point — the new metric's shape on the per-file record (nullable; null when no non-bulk commit touched the file).
  [`GitMetrics.cs:85`](../../src/SpecScribe/GitMetrics.cs#L85)

- The averaging: credit `(distinctFiles − 1)` per non-bulk commit, solo commits count as 0, bulk (> cap) excluded.
  [`GitMetrics.cs:777`](../../src/SpecScribe/GitMetrics.cs#L777)

**Treemap render (declutter + rich card)**

- Directory labels now only on top-level (`Depth == 0`) rects; boundaries still drawn at every depth.
  [`Charts.cs:1347`](../../src/SpecScribe/Charts.cs#L1347)

- The stylized card builder — dynamic parts escaped once here; the attribute re-escapes for the innerHTML round-trip.
  [`Charts.cs:1415`](../../src/SpecScribe/Charts.cs#L1415)

- The rect now carries `data-tip-html` (was `data-tip`) plus `data-cochanged`.
  [`Charts.cs:1396`](../../src/SpecScribe/Charts.cs#L1396)

**Shared tooltip node (highest blast radius)**

- Additive `data-tip-html` → `innerHTML` branch; every existing plain-text tip path is untouched.
  [`specscribe.js:37`](../../src/SpecScribe/assets/specscribe.js#L37)

- New `cochange` colorize dimension; `recolor` no longer mutates the (now-static) tooltip.
  [`specscribe.js:394`](../../src/SpecScribe/assets/specscribe.js#L394)

**Controls, table, styles**

- The `cochange` radio and the "Together" text-table column (no-JS truth of the metric).
  [`CodeMapTemplater.cs:100`](../../src/SpecScribe/CodeMapTemplater.cs#L100)

- The card CSS (two-column `<dl>` grid) inside the shared `.ss-tooltip`.
  [`specscribe.css:3288`](../../src/SpecScribe/assets/specscribe.css#L3288)

**Tests**

- Co-change math: bulk exclusion, solo=0, null-when-only-bulk.
  [`GitMetricsTests.cs:700`](../../tests/SpecScribe.Tests/GitMetricsTests.cs#L700)

- Label suppression + rich-card/`data-cochanged` render.
  [`ChartsTests.cs:927`](../../tests/SpecScribe.Tests/ChartsTests.cs#L927)
