---
title: 'Sprint epic filter and home sunburst-first layout'
type: 'feature'
created: '2026-07-14'
status: 'done'
baseline_commit: '259591ffe7c0b63fdfde917093ca66cab9d86a08'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The sprint board on the home widget and sprint page shows every epic's cards, so the eye is drowned in finished or not-yet-started work. The home layout also puts the sprint widget above the sunburst, but the sunburst is what you look at first.

**Approach:** Add an epics selector on both sprint surfaces that filters displayed story cards, defaulting to *active* epics. On the home dashboard, place Project at a Glance (sunburst) above the Now & Next / sprint widget.

## Boundaries & Constraints

**Always:**
- Active epic = has ≥1 tracked story in `in-progress`, `review`, or `done`, AND that epic's `epic-N-retrospective` yaml status is not `done`.
- If no epic meets that rule, the active set is the first epic in sprint-status file order whose `epic-N` status is not `done`.
- Selector + filter ship on both the home sprint board widget and the full `sprint.html` page (status and by-epic views).
- Use yaml sprint ledger only for active/fallback (not `HasRetrospective` / artifact roll-ups).
- Progressive enhancement: boards remain readable with JS off (cards present; filter control may be JS-injected like `js-sortable` tables).
- Home board `capPerColumn` must apply **after** the current epic filter so capped columns aren't empty of the selected epics.
- Progress wheel / stage counts stay full-sprint (unfiltered); only cards/lanes filter.

**Ask First:**
- Changing the active-epic definition or fallback rule.
- Persisting filter selection across pages/sessions (default is session/page load only).

**Never:**
- Do not filter the sunburst, epic donut, or overall progress panels.
- Do not invent a second "Sprint Status" donut panel.
- Do not gate "active" on parsed retro markdown (`HasRetrospective`).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy — active set | Stories for epic 3 in `in-progress`; epic-3-retrospective `optional`; epic 1 all done + retro `done` | Default selection = epic 3 only; boards hide epic 1 cards; epic 3 remains selectable | N/A |
| Fallback — none active | No stories in in-progress/review/done with open retro; epic 2 is first `epic-N` ≠ `done` in file order | Default selection = epic 2 | N/A |
| Empty selection | User clears all epic checkboxes/chips | Boards show empty columns/lanes with a clear empty hint (not a broken layout) | N/A |
| No sprint data | SprintStatus absent | No widget, no sprint page, no selector | Same omit path as today |
| Home order | Epics + sprint present | Sunburst panel appears before Now & Next / sprint board in DOM | N/A |
| Cap after filter | Home cap=3; epic A has 5 in-progress; other epics also in-progress | With default A selected, column shows up to 3 of A's cards (not 3 from mixed epics) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SprintStatus.cs` / `SprintStatusParser.cs` -- entry kinds (Epic / Story / Retrospective) and status strings
- `src/SpecScribe/SprintTemplater.cs` -- `RenderBoard`, `RenderBoardByEpic`, `GroupByEpic`, `AppendBoardCard` (add `data-epic`, active-set helper, selector hook)
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- panel order (~50–64); home `RenderBoard(..., capPerColumn: 3)`
- `src/SpecScribe/StatusStyles.cs` -- `ForSprint` labels (do not repurpose `ForEpicWithRetrospective` for this filter)
- `src/SpecScribe/assets/specscribe.js` -- progressive filter injection pattern (`enhanceSortableTable` / `data-filter-label`)
- `src/SpecScribe/assets/specscribe.css` -- board / epic-lane styles; selector chrome
- `tests/SpecScribe.Tests/SprintTemplaterTests.cs` -- board + active-set / filter markup
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` -- home widget + panel order
- `tests/SpecScribe.Tests/SiteGeneratorSprintTests.cs` -- generation smoke if needed

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SprintTemplater.cs` (and/or small helper beside it) -- compute active epic numbers (rule + fallback); tag cards/lanes with `data-epic`; emit epic selector markup/hooks; filter-before-cap on home board; hide non-selected cards/lanes for default selection when JS enhances -- rationale: single ownership of sprint board UX
- [x] `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- swap sunburst and Now & Next order (sunburst first) -- rationale: home scan path
- [x] `src/SpecScribe/assets/specscribe.js` + `specscribe.css` -- progressive epic selector (multi-select; default = active set; empty-selection empty state); apply filter to status columns and by-epic lanes; optional home column cap on *visible* cards -- rationale: PE parity with table filters
- [x] `tests/SpecScribe.Tests/SprintTemplaterTests.cs` + `HtmlTemplaterTests.cs` -- cover I/O matrix: active default, fallback, markup hooks, sunburst-before-sprint DOM order, cap-after-filter -- rationale: lock behavior

**Acceptance Criteria:**
- Given sprint data with one active epic and older done+retro'd epics, when the home widget and sprint page load, then only the active epic's cards are shown by default and a selector lists available epics.
- Given no epic matches the active rule, when boards render, then the first non-`done` epic (file order) is the default filter.
- Given the user changes the selector, when they toggle epics on/off, then visible cards/lanes update without a page reload (JS on); progress wheel counts remain the full-sprint totals.
- Given epics and sprint data on the home dashboard, when `index.html` is generated, then the sunburst panel precedes the Now & Next / sprint widget in the document order.
- Given JS is disabled, when viewing sprint surfaces, then story cards remain readable and navigable (no blank board).

## Spec Change Log

## Design Notes

**Active-set (yaml only):**

```
active = epic N where
  any story of N has ForSprint status in {in-progress, review, done}
  AND (no epic-N-retrospective entry OR its status ≠ done)
if active empty:
  first epic-N entry in file order with status ≠ done
```

**Home cap:** Prefer rendering cards for the filtered set then applying `capPerColumn`, or rendering all cards with `data-epic` and applying filter+cap in one enhance pass — pick whichever keeps default-active home columns non-empty without a second network round-trip.

**Selector shape:** Multi-select of epics that appear on the board (file order). Default checked = active set. An "All" affordance is fine if it stays one control, not a second panel.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~SprintTemplater|FullyQualifiedName~HtmlTemplater"` -- expected: pass, including new active-filter and order assertions
- `dotnet run --project src/SpecScribe -- --source . --out SpecScribeOutput` (or project-typical generate) -- expected: `index.html` sunburst before sprint widget; both boards show epic selector + active-default filter

**Manual checks:**
- Open home and sprint page: confirm default cards match active epics; toggle another epic and see cards appear; clear all and see empty state; wheel numbers unchanged.

## Suggested Review Order

**Active-epic default**

- Yaml-only active set + fallback to first non-done epic with stories
  [`SprintTemplater.cs:125`](../../src/SpecScribe/SprintTemplater.cs#L125)

- Cap applies after filter; non-default cards stay hidden for JS reveal
  [`SprintTemplater.cs:164`](../../src/SpecScribe/SprintTemplater.cs#L164)

**Selector PE**

- Catalog + defaults as data attrs; checkbox UI injected only with JS
  [`SprintTemplater.cs:344`](../../src/SpecScribe/SprintTemplater.cs#L344)

- Inject filter, recount lanes/aria, empty-selection + cap behavior
  [`specscribe.js:361`](../../src/SpecScribe/assets/specscribe.js#L361)

- Filter chrome styles (including All focus-visible)
  [`specscribe.css:4046`](../../src/SpecScribe/assets/specscribe.css#L4046)

**Home layout**

- Sunburst before Now & Next / sprint widget
  [`HtmlRenderAdapter.Dashboard.cs:50`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs#L50)

**Tests**

- Active set, fallback, cap-after-filter, shared filter root
  [`SprintTemplaterTests.cs:409`](../../tests/SpecScribe.Tests/SprintTemplaterTests.cs#L409)

- Home filter hooks + sunburst-before-sprint order
  [`HtmlTemplaterTests.cs:823`](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs#L823)
