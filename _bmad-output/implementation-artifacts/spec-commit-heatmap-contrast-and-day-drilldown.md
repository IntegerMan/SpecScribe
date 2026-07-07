---
title: 'Commit heatmap: fainter zero-commit days + click-a-day commit drill-down'
type: 'feature'
created: '2026-07-06'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'fdcb6967838f3c402a8026a3c163838568bcbe09'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On the Commit Activity heatmap, days with a few commits (level-1) are nearly indistinguishable from zero-commit days (level-0). There is also no way to see *what* landed on a given day.

**Approach:** Render zero-commit cells much fainter so any activity pops. Make each active day's cell a link revealing an inline details panel below the chart (date, count, short hash + subject per commit) with previous/next links jumping between active days — pure-CSS `:target` visibility so it works with JS disabled, consistent with the project's no-JS-engine chart ethos.

## Boundaries & Constraints

**Always:**
- Panels work with JavaScript disabled (`:target` + plain anchors). Only days with ≥1 commit (and ≤ today) are clickable/get panels.
- Commit subjects HTML-escaped via the existing `Html()` helper.
- Keep per-cell `<title>` tooltips and the whole-chart accessible name; reuse the shared teal `:focus-visible` convention for the new in-SVG links (mirror `.sunburst a:focus-visible .sb-seg`).
- `GitMetrics` stays never-throw / 3s-timeout; ONE `git log` call supplies both daily series and per-day details (same `%ad --date=short` field so grouping matches).
- Legend level-0 swatch matches the new fainter cell treatment.

**Ask First:**
- Any new JavaScript (the existing tooltip script's link-wrapped-segment handling must suffice untouched).
- Hard-truncating a day's commit list (prefer CSS max-height + scroll).

**Never:**
- No modal/overlay; no invisible zero-cells (grid shape stays readable); no tab stops on zero-commit cells; no changes to sunburst/donut or the tooltip script.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior |
|----------|--------------|---------------------------|
| Active day clicked | Cell, 3 commits | Panel `#heat-day-{yyyy-MM-dd}` visible: date heading, "3 commits", list of `hash subject` |
| Zero-commit day | Cell, 0 commits | Faint cell, no link, no panel; `<title>` retained |
| Panel navigation | Active day with/without active neighbors | Prev/next links target chronologically adjacent *active* days (skip empty days); omitted on earliest/latest |
| Subject with markup | `fix <div> & "quotes"` | Escaped, renders literally |
| Details unavailable | `CommitHeatmap` called without details arg | Grid renders as today: no links, no panels, no throw |
| Malformed log line | Line missing hash/subject | Skipped for details; never-throw contract holds |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/GitMetrics.cs` -- `GitPulse` record + `TryCompute`; extend log format and add `CommitsByDay`.
- `src/SpecScribe/Charts.cs:417-528` -- `CommitHeatmap` + `HeatLevel`; SVG grid, headline, legend.
- `src/SpecScribe/HtmlTemplater.cs:174-178` -- sole `CommitHeatmap` call site.
- `src/SpecScribe/assets/specscribe.css:1287-1327` -- heatmap styles incl. `.level-0` fills; `:110-113` sunburst SVG-link focus ring to mirror.
- `src/SpecScribe/assets/specscribe.js` -- NO changes; already handles link-wrapped segments (focusin, touch first-tap).
- `tests/SpecScribe.Tests/ChartsTests.cs:128-200` -- heatmap tests; `role="img"` assertion must change. No GitMetrics tests exist yet.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/GitMetrics.cs` -- Add `public sealed record CommitInfo(string ShortHash, string Subject)`; add `IReadOnlyDictionary<DateOnly, IReadOnlyList<CommitInfo>> CommitsByDay` to `GitPulse`; switch to `log --pretty=format:%h%x09%ad%x09%s --date=short`. Extract parsing into a pure `public static` helper (log text → series + commits-by-day) so it's unit-testable; `TryCompute` calls it.
- [x] `src/SpecScribe/Charts.cs` -- `CommitHeatmap(series, commitsByDay = null)`: wrap each cell with `Count > 0` and details present in `<a href="#heat-day-{date}" aria-label="{date}: N commits — view details">`; change `<svg>` `role="img"` → `role="group"` (keep `aria-label`) so inner links reach AT; after the legend emit one `<section class="heatmap-day" id="heat-day-{date}">` per active day: `<h4>` date + count, `<ul>` of `<code>{hash}</code> {subject}`, nav row with prev/next active-day anchors (omit at ends). Null details ⇒ output identical to today except the role change.
- [x] `src/SpecScribe/HtmlTemplater.cs` -- Pass `gitPulse.CommitsByDay` at the call site.
- [x] `src/SpecScribe/assets/specscribe.css` -- (a) `.heatmap-cell.level-0` + `.heatmap-legend-swatch.level-0` much fainter (e.g. `fill-opacity: 0.3` on `var(--parchment-dark)` / rgba equivalent for the swatch); (b) `.heatmap-day` hidden by default, shown via `:target`, `scroll-margin-top: var(--nav-offset)`, list `max-height` + `overflow-y: auto`; (c) focus ring for heatmap SVG links mirroring the sunburst pattern + hover stroke/cursor on linked cells only.
- [x] `tests/SpecScribe.Tests/ChartsTests.cs` -- Update role assertion to `role="group"`; add I/O-matrix tests: active-day anchors present, zero-day cells unwrapped, panels with escaped subjects, prev/next presence/absence at ends and skipping empty days, no-details call emits no anchors/panels.
- [x] `tests/SpecScribe.Tests/GitMetricsTests.cs` -- New file testing the parse helper: tab-separated lines → correct series + per-day commit lists; malformed line skipped without throwing.

**Acceptance Criteria:**
- Given the dashboard, when comparing a 1-commit day to a 0-commit day, then the 0-commit cell is much fainter (clear opacity difference, not a subtle hue shift) and the "Less" legend swatch matches.
- Given JS disabled, when an active day's cell is clicked, then only `#heat-day-{date}`'s panel becomes visible, listing that day's commits as short hash + subject.
- Given an open panel, when "next active day" is clicked, then the next chronologically active day's panel opens, skipping zero-commit days.
- Given keyboard navigation, when tabbing through the heatmap, then only active-day cells receive focus, showing the shared teal focus treatment.
- Given `git` fails or times out, when the dashboard is generated, then the page renders "No git history available." exactly as today.

## Spec Change Log

## Verification

**Commands:**
- `dotnet build` -- expected: clean, no new warnings.
- `dotnet test` -- expected: all pass, including new heatmap + GitMetrics tests.

**Manual checks:**
- Regenerate the site against this repo; confirm faint zero-days, click-to-open panels, prev/next hops across the active days, and identical behavior with JS disabled.

## Suggested Review Order

**Data: one git call → series + per-day details**

- New `CommitInfo` + `CommitsByDay` on the pulse — the drill-down's data contract.
  [`GitMetrics.cs:8`](../../src/SpecScribe/GitMetrics.cs#L8)

- Pure, invariant-culture, malformed-line-tolerant parse of the tab-separated log format.
  [`GitMetrics.cs:61`](../../src/SpecScribe/GitMetrics.cs#L61)

- UTF-8 stdout so non-ASCII commit subjects survive Windows codepages.
  [`GitMetrics.cs:107`](../../src/SpecScribe/GitMetrics.cs#L107)

**Rendering: linked cells, role, panels**

- Linked days resolved up front; they drive cell links, panels, and the SVG role.
  [`Charts.cs:503`](../../src/SpecScribe/Charts.cs#L503)

- `role="group"` only when links exist; link-free renders keep `role="img"`.
  [`Charts.cs:520`](../../src/SpecScribe/Charts.cs#L520)

- Active cells get anchor + aria-label; zero-cells stay unlinked and aria-hidden.
  [`Charts.cs:564`](../../src/SpecScribe/Charts.cs#L564)

- Panels: date heading, hash+subject list, dated prev/next, Close back to the chart.
  [`Charts.cs:598`](../../src/SpecScribe/Charts.cs#L598)

- One-line call-site wiring on the dashboard.
  [`HtmlTemplater.cs:223`](../../src/SpecScribe/HtmlTemplater.cs#L223)

**Styling: faintness + :target drill-down**

- Zero-commit cells drop to 0.3 fill-opacity — the discernment fix.
  [`specscribe.css:1391`](../../src/SpecScribe/assets/specscribe.css#L1391)

- Rust hover stroke (gold would vanish on gold-filled cells); teal focus ring.
  [`specscribe.css:1400`](../../src/SpecScribe/assets/specscribe.css#L1400)

- `:target`-driven panel visibility — the whole no-JS interaction model.
  [`specscribe.css:1430`](../../src/SpecScribe/assets/specscribe.css#L1430)

- Legend swatch reuses the token via opacity, never a hand-copied rgba.
  [`specscribe.css:1422`](../../src/SpecScribe/assets/specscribe.css#L1422)

**Peripherals**

- Drill-down, escaping, nav-edge, and no-details regression tests.
  [`ChartsTests.cs:238`](../../tests/SpecScribe.Tests/ChartsTests.cs#L238)

- Parse-contract tests incl. Buddhist-calendar culture pin and empty subjects.
  [`GitMetricsTests.cs:1`](../../tests/SpecScribe.Tests/GitMetricsTests.cs#L1)

- Consistent fixture + dashboard wiring integration test.
  [`HtmlTemplaterTests.cs:149`](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs#L149)

- Two stale comments corrected (behavior untouched) — cells are now link-wrapped.
  [`specscribe.js:86`](../../src/SpecScribe/assets/specscribe.js#L86)
