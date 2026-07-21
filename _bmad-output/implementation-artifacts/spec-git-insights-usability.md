---
title: 'Git Insights page usability: narrow columns, no paging, oversized heatmap'
type: 'bugfix'
created: '2026-07-21'
status: 'in-progress'
review_loop_iteration: 0
context: []
baseline_commit: '34d681b21dab4e49ca647d5ff24d6fe553323ec1'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `git-insights.html`'s file table wraps paths one character per line (`.gi-table td code { word-break: break-all }`) instead of scrolling horizontally; all files render unpaginated in one long table; the Activity Over Time heatmap SVG stretches to fill its panel width regardless of week count, so short-history repos get huge disproportionate tiles.

**Approach:** Drop the char-wrap in `.gi-table` code cells so long paths use the existing `.table-scroll` horizontal scroll; extend `enhanceSortableTable` (specscribe.js) with client-side pagination over the sorted/filtered rows; cap the heatmap SVG's rendered width via inline style proportional to its own grid size, instead of always stretching to 460px.

## Boundaries & Constraints

**Always:** No-JS page stays fully functional (NFR-5) — full table ships server-rendered; pagination is JS-only, never required to read all rows. `.table-scroll` horizontal-scroll fallback preserved. Heatmap cell/gap/gutter constants (11/3/26/16) unchanged — only outer size capped; long-history repos (grid already near/over 460px) render unchanged. Pagination composes with existing sort/filter (Story 3.8/10.9 pattern) — filtering/sorting a paginated table keeps the correct total and never strands the reader on an empty page. Leave `deferred-work.md`, `spec-7-3-deferred-debt-cleanup.md`, `CommitDayTemplater.cs`, `SiteGenerator.cs`, `SiteGeneratorTimelineTests.cs` untouched (unrelated in-progress work from a concurrent session).

**Ask First:** none — default page size (20 rows) and pager styling are reasonable defaults.

**Never:** Don't restructure `.gi-master-detail` or the `:target` drill-down. No JS dependency/framework — vanilla JS only. Don't change heatmap cell constants or add JS to the heatmap (stays pure server-rendered SVG).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Long file path, JS off | Deeply nested path in `.gi-table` | Renders on as few lines as the column allows; overflows via horizontal scroll, never char-wraps | N/A |
| Large table, JS on | Git Insights hub, 50 files | Paginated (20/page) with Prev/Next + page indicator; page 1 default | N/A |
| Paginate + filter | User types filter query | Matches across ALL rows; pager reflects filtered count, resets to page 1 | 0 matches: existing "0 of N rows" state, pager hides |
| Paginate + sort | User clicks sort header | Rows re-sort globally; pager resets to page 1 | N/A |
| Small table, JS on | Rows <= page size | No pager rendered | N/A |
| Young repo heatmap | History spans < 15 weeks | Rendered `max-width` scales down proportionally, cells stay near natural ~11-14px size | N/A |
| Old repo heatmap | Natural width already >= ~460px | Rendering unchanged (still capped at 460px) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/assets/specscribe.css:5267` -- `.gi-table td code { word-break: break-all }`, root cause of char-wrapped file column; shared with the Ownership & Bus-Factor table.
- `src/SpecScribe/assets/specscribe.js:329` -- `enhanceSortableTable` (js-sortable enhancer): sort + filter live here; pagination composes with both via the shared `rows()` helper.
- `src/SpecScribe/assets/specscribe.css:5289` -- `.gi-filter` styles, the pattern to follow for new `.gi-pager` styling.
- `src/SpecScribe/Charts.cs:1167-1206` -- `CommitHeatmap`: `cell`/`gap`/`leftGutter`/`topGutter` constants and the `<svg class="heatmap" ...>` tag where a capped inline `style="max-width:...px"` gets added.
- `tests/SpecScribe.Tests/ChartsTests.cs` -- extend with the new width-cap cases.
- `tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs` -- confirm no server-side HTML contract breaks.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/assets/specscribe.css` -- replace `.gi-table td code { word-break: break-all }` with `white-space: nowrap;` (falls back to the existing `.table-scroll` horizontal scroll for long paths) -- fixes the char-by-char wrap without touching table structure.
- [x] `src/SpecScribe/assets/specscribe.js` -- in `enhanceSortableTable`, add pagination: page size 20, recompute the current row window after every sort/filter (via a `gi-filtered-out` marker class distinct from the final `gi-row-hidden`), render Prev/Next + "Page X of Y" beneath the table (only when the matching set exceeds the page size), reset to page 1 on sort or filter change -- delivers client-side paging without touching the no-JS server output.
- [x] `src/SpecScribe/assets/specscribe.css` -- add `.gi-pager` styles (flex row, buttons matching `.gi-sort-btn`/`.gi-filter` visual language, disabled state for Prev on page 1 / Next on last page) -- keeps the new control on-brand.
- [x] `src/SpecScribe/Charts.cs` -- in `CommitHeatmap`, after computing `width`/`height`, compute `var maxRenderWidth = Math.Min(460, (int)Math.Round(width * 1.8));` and add `style="max-width:{maxRenderWidth}px"` to the `<svg class="heatmap" ...>` tag -- caps short-grid stretch while leaving long-grid rendering effectively unchanged.
- [x] `tests/SpecScribe.Tests/ChartsTests.cs` -- added cases asserting the emitted `style="max-width:...px"` value for a short (young-repo, few-week) series vs. a long (many-week) series, confirming the cap formula.

**Acceptance Criteria:**
- Given the Git Insights hub with JS disabled, when the page loads, then every file path in the table renders on as few lines as its column allows (no single-character line wraps) and the full unpaginated table is present in the HTML.
- Given the Git Insights hub with JS enabled and more than 20 files, when the page loads, then only the first page of rows is visible with Prev/Next controls, and clicking Next reveals the next page of rows without a full page reload.
- Given a paginated, sorted, and filtered table, when the user changes the filter text, then the pager's total reflects only matching rows and returns to page 1.
- Given a repo whose commit history spans fewer than 15 weeks, when its Activity Over Time heatmap renders, then the SVG's `max-width` inline style is smaller than the default 460px cap, proportional to the grid's own width.

## Spec Change Log

## Verification

**Commands:**
- `dotnet test` -- expected: all existing + new tests pass, including `ChartsTests`, `GitInsightsTemplaterTests`, and `StylesheetTests`.

**Manual checks (if no CLI):**
- Run `specscribe generate --deep-git` (or the project's existing generate command) against this repo, open `git-insights.html` in a browser: confirm file paths no longer wrap character-by-character, the file table paginates once JS runs, and the Activity Over Time heatmap tiles render at a reasonable size (not the current oversized tiles seen for this repo's short history).
