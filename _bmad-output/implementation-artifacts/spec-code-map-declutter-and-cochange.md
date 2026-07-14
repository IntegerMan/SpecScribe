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

## Boundaries & Constraints

**Always:**
- Accumulate co-change inside the existing `BuildCodeMapMetrics` over the records `ParseNumstatLog` already folds — no second `git log`, no new git module. [[deep-git-single-numstat-path]]
- Everything works with JS off (server treemap + legend + full text table, incl. the new column); JS only enhances. Color is never the sole signal — the card and table carry every metric as text. [[charting-is-pure-svg-no-js]]
- Keep the ramp off `--status-*`; reuse the `.codemap-cell level-N` heatmap ramp. [[specscribe-status-token-system]]
- The HTML tooltip is opt-in per element (`data-tip-html`); every existing plain-text `data-tip`/`<title>` tip must render exactly as before. Card content is server-built, dynamic parts escaped via `PathUtil.Html`.

**Ask First:**
- Dropping directory boundary strokes entirely (boundaries stay even with no labels — AC #1 "clear boundaries").
- Changing the co-change definition (denominator, cap, solo-commit handling) from the I/O matrix.

**Never:**
- Do not route co-change color through `--status-*`; do not build a second tooltip node (reuse `.ss-tooltip`). [[tooltip-clipping-use-ss-tooltip-node]]
- Do not add incremental treemap regeneration or edit `docs/live/specscribe.css` — only the embedded `src/SpecScribe/assets/specscribe.css`. [[generate-output-dir-is-specscribeoutput]]

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

</frozen-after-approval>

## Code Map

- `src/SpecScribe/GitMetrics.cs` -- `CodeFileMetrics` record (add `double? AvgCoChanged`); `BuildCodeMapMetrics` (accumulate co-change over non-bulk commits, `CouplingFileSetCap` already here); `CodeMapAccum`.
- `src/SpecScribe/CodeMap.cs` -- squarified layout inset: replaced the asymmetric `DirHeader`(16px)+`Pad`(2px) with a uniform `Pad`(3px) on all sides, now that no directory reserves header space for a label.
- `src/SpecScribe/Charts.cs` -- `AppendTreemapDir` (no label at any depth, boundary rect only); `AppendTreemapFile` (emit `data-cochanged`, swap `data-tip` → `data-tip-html`); `BuildTreemapTip` → `BuildTreemapCard` (styled HTML card, dates via `DReadable`).
- `src/SpecScribe/CodeMapTemplater.cs` -- `AppendControls` (new "Files changed together" radio); `AppendFileTable` (new "Together" column; First/Last via `PortalDates.Day`, not raw ISO).
- `src/SpecScribe/assets/specscribe.js` -- tip `activate`/`showTip` (opt-in `data-tip-html` → innerHTML branch); treemap `metricFor`/`DIM_LABELS`/`recolor` (add `cochange` dim; drop dead `data-tip` mutation since the card is static, keep aria-label + legend refresh).
- `src/SpecScribe/assets/specscribe.css` -- `.ss-tooltip` card styles (heading, mono path, metric rows); removed the now-dead `.codemap-dir-label` rule.
- `tests/SpecScribe.Tests/GitMetricsTests.cs`, `ChartsTests.cs`, `CodeMapTemplaterTests.cs` -- co-change math, no-label-at-any-depth, card markup, new column, readable-date assertions.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- golden inventory + whole-site content fingerprint (regenerated). [[golden-diff-normalization-gotchas]]

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/GitMetrics.cs` -- Added `double? AvgCoChanged` to `CodeFileMetrics`; `BuildCodeMapMetrics` accumulates `(distinctFileCount - 1)` + a qualifying-commit counter per non-bulk commit (distinct set ≤ `CouplingFileSetCap`), `AvgCoChanged = total / qualifyingCommits` (null when 0). Solo commits contribute 0 and count in the denominator; bulk (> cap) excluded.
- [x] `src/SpecScribe/Charts.cs` -- `AppendTreemapDir` draws boundary rects only, no `<text>` at any depth. `AppendTreemapFile` emits `data-cochanged` (when present) and `data-tip-html` (was `data-tip`). `BuildTreemapTip` → `BuildTreemapCard`: name heading, mono path, `<dl>` metric rows incl. **files changed together** and `DReadable`-formatted first/last dates (rows present only when the metric exists).
- [x] `src/SpecScribe/CodeMap.cs` -- Replaced the 16px header + 2px pad inset with a uniform 3px inset on all sides (round 2: no label means no reserved header band; the old asymmetric inset showed through as an unfilled "blank" strip since `.codemap-dir` has no fill).
- [x] `src/SpecScribe/CodeMapTemplater.cs` -- Added the `cochange` radio and the "Together" column (`N1`, em-dash fallback); First/Last cells now use `PortalDates.Day` (was raw ISO `Charts.D`).
- [x] `src/SpecScribe/assets/specscribe.js` -- Shared tip `activate`/`showTip` gained a `data-tip-html` → `innerHTML` branch (all other tips unchanged). Treemap `metricFor`/`DIM_LABELS` gained `cochange`; `recolor` no longer mutates the tooltip (static card), still refreshes aria-label + legend.
- [x] `src/SpecScribe/assets/specscribe.css` -- Styled `.codemap-card*` inside `.ss-tooltip` (heading, mono path, two-column `<dl>` grid); removed the dead `.codemap-dir-label` rule (round 2).
- [x] `tests/SpecScribe.Tests/*` -- Co-change math tests (bulk exclusion / solo=0 / null-when-no-qualifying), no-label-at-any-depth + rich-card `CodeTreemap` tests, "Together" column + readable-date templater assertions. **Full suite green: 1034/1034, golden fingerprint regenerated and stable.**

**Acceptance Criteria:**
- Given a deep-git run, when I open the code map, then no directory rect at any depth carries a text label, boundary rects are still drawn at every depth, and every directory/file is identifiable via its tooltip or the text table.
- Given I hover a file cell, when the tooltip appears, then it is a styled card showing the path and all available metrics as labeled text (dates in the portal's readable format), including "files changed together".
- Given deep-git metrics exist, when I pick the "Files changed together" colorize option, then cells recolor on the sequential ramp by their average co-change value, the legend reads "Colorized by files changed together", aria-labels name that dimension, and the text table shows the per-file value.
- Given `--deep-git` is off or a file has no git record, when the code map renders, then no co-change value is shown for it (card/table "—", neutral fill) and generation still succeeds.
- Given JS is disabled, when I load the page, then the treemap, legend, and the full text table (with the "Together" column, readable dates) render correctly with no tooltip dependency.

## Design Notes

Co-change denominator is *all* non-bulk commits touching the file (solo commits count as 0), so it reads as "typical blast radius per change," not "blast radius when not alone." `CouplingFileSetCap` (50) already lives in `GitMetrics`, so the exclusion stays local to `BuildCodeMapMetrics`.

The card is per-element opt-in: only treemap cells set `data-tip-html`; the shared node's `textContent` branch stays the default for every other chart, so sunburst/heatmap/coupling tips can't regress. Because the card lists all metrics as text, AC #4 holds for any active dimension without per-dimension tooltip rewriting — so `recolor` stops mutating tooltip text (still refreshes aria-label + legend). Card = file-name heading, mono path, and a `<dl>` of the metric rows (each "—" when absent).

**Escaping:** the card is built with dynamic parts `Html`-escaped, then the whole card is `Html`-escaped again into `data-tip-html`. The browser decodes the attribute once (getAttribute) and the HTML parser decodes again (innerHTML), so the double-escape is exactly right — verified live in a browser (attribute → real card DOM, all rows present).

**Round 2 — the "blank areas" root cause:** removing only nested `<text>` elements (round 1) didn't remove the space reserved FOR them. `CodeMap.cs`'s squarified layout always insets a directory's children by `DirHeader`(16px) at the top + `Pad`(2px) elsewhere, regardless of whether a label renders into that space — and `.codemap-dir` has `fill: none`, so an unlabeled reserved band shows through as visually blank (uncolored) parchment. Fix: once no directory anywhere carries a label, the header reservation is pointless — collapsed to a uniform 3px gutter on all sides (verified live: the gutter above every directory's children measures exactly 3px in the rendered SVG, down from the old 16px top band).

**Round 2 — dates:** the card and table First/Last previously used `Charts.D` (the raw ISO machine token, `PortalDates.IsoDay`, e.g. "2026-07-05") — fine for URLs/filenames but not meant for human reading. Card now uses `Charts.DReadable` (`PortalDates.DayWithWeekday`, e.g. "Mon, Jul 5, 2026") — its own doc comment calls out "tooltips" as its designated use. The table uses plain `PortalDates.Day` ("Jul 5, 2026", no weekday) since a dense multi-column table favors compactness over the weekday prefix.

**Golden fingerprint:** a background auto-committer landed two commits mid-session (`537ff8c`, `a5aca55`) that bundled this story's round-1 code together with a concurrent "entity prev/next navigation" story and reconciled the tree (see [worktree-edits-must-target-worktree-path]). Once `main` compiled cleanly and no other session was contending, the fingerprint was regenerated directly and is now stable at `d004d31cba855143d78ee9c6461957e9e1162f20a5e808d9042c03e99613c116`.

## Verification

**Commands:**
- `dotnet test SpecScribe.slnx` -- result: **1034/1034 green**, golden fingerprint stable at `d004d31cba855143d78ee9c6461957e9e1162f20a5e808d9042c03e99613c116`.
- `dotnet run --project src/SpecScribe -- generate --deep-git` then open `code-map.html` -- result (verified live in-browser): zero `<text>` elements in the SVG, 205 directory boundary rects still drawn, the top gutter above every directory's children measures 3px (was 16px), hovering a cell renders the styled card (dates e.g. "Mon, Jul 6, 2026 · Mon, Jul 13, 2026"), "Files changed together" recolors the map + updates the legend, the "All files" table has a "Together" column with `Jul 5, 2026`-style dates.

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
