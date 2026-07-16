---
title: 'Home welcome screen flow and navigation'
type: 'feature'
created: '2026-07-16'
status: 'in-review'
review_loop_iteration: 0
context: []
baseline_commit: '26e24c76ed07a7b835b3e0709129c95df3f5e319'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The home page's first-row card tooltips can be hidden under the sticky white key-view bar, the key-view bar carries too many peer chips, and redundant "View epics/sprint" links add visual noise even though the cards and panels already navigate. The header also says "Delivery" where the intended user journey is broader work tracking.

**Approach:** Make home stats use the shared body-level tooltip path, group secondary documentation links into a compact Docs menu on the white band, rename the dark-bar Delivery group to Work, remove redundant "View epics"/"View sprint" CTAs from home tiles/panels, and visually segment the dashboard tile band into the application-flow journey: requirements, epics/stories, execution, review/follow-ups, architecture/insights.

## Boundaries & Constraints

**Always:**
- Preserve every generated page and every existing count source; this is layout/navigation presentation only.
- Keep all home cards/panels that already have a meaningful `href` clickable.
- Use existing static C# rendering, CSS, and the sanctioned `specscribe.js` body-level tooltip mechanism; no new package or framework.
- Keep the nav accessible by hover, focus, and mobile toggle; dropdown/group labels must keep text plus decorative icon.
- Keep the white band useful as a "where do I go next?" launchpad, but reduce peer-chip overload by grouping related docs.

**Ask First:**
- Removing any page from `SiteNav.QuickLinks` entirely instead of only grouping it visually.
- Changing status words, count semantics, or canonical journey names beyond `Delivery` -> `Work`.
- Adding new generated pages or a new persistent data model for homepage grouping.

**Never:**
- Do not reintroduce the removed bottom home index card bands or quick-dev card grid.
- Do not make tooltips depend on clipped CSS `::after` positioning for first-row cards.
- Do not leave the white band with README, PRD, Product Brief, UX Design, and UX Experience as five separate always-visible peer chips.
- Do not add redundant `View epics`, `View sprint`, or `View sprint board` links where the surrounding tile/panel already has direct click targets.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|----------------------------|----------------|
| Full project | README, PRD, brief, UX docs, architecture, epics, requirements, sprint, ADRs, code map | White key-view band shows compact groups for docs/flow/work/architecture/insights instead of every doc as a peer; the dark bar shows `Work`; all generated targets remain reachable | Missing optional targets are omitted from their groups with no dangling links |
| Home stat tooltip near top | First-row stat card has tooltip text and sits below sticky nav | Hover/focus shows the shared body-level tooltip above page content and not hidden under the white bar | With JS off, keep native/fallback title or accessible text sufficient for the card |
| No sprint file | Epics/requirements exist but `sprint-status.yaml` absent | Work group and home panels omit sprint-specific links; no broken sprint target | Existing guards still control output |
| Minimal project | Only a subset of docs exists | Key-view groups collapse or omit empty groups without leaving empty labels/dropdowns | No exceptions; generated nav remains valid |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/Charts.cs` -- `StatCard` emits stat tile tooltip attributes; switch tooltip-capable stat cards to the `.js-tip` / `data-tip` path while preserving link/static forks.
- `src/SpecScribe/HtmlRenderAdapter.cs` -- `RenderNavMarkup`, `AppendNavMenu`, `AppendKeyViewsBand`, `NavGroupOrder`, and `NavGroup` define the dark journey menu and white key-view band grouping.
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- home tile/panel CTAs and tile-band composition live here; remove redundant home "View..." links and add journey segment wrappers/classes if needed.
- `src/SpecScribe/assets/specscribe.css` -- sticky nav, key-view pills/dropdowns, stat/tile band, and tooltip styles; add compact white-band grouping and visual segment treatments.
- `src/SpecScribe/assets/specscribe.js` -- shared body-level tooltip already handles `.js-tip`; only touch if stat-card fallback needs a small compatibility tweak.
- `tests/SpecScribe.Tests/SiteNavTests.cs` -- nav grouping, display labels, quick-link reachability, and icon/text assertions.
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs`, `tests/SpecScribe.Tests/RenderSectionParityTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorSprintTests.cs` -- dashboard/nav rendered-output expectations and parity facts.
- `tests/SpecScribe.Tests/StylesheetTests.cs` -- CSS invariants for nav/tooltips/contrast if existing selectors are asserted.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/Charts.cs` -- change tooltip-capable stat cards from `data-tooltip` CSS tips to `.js-tip` + `data-tip` and preserve keyboard focus semantics.
- [x] `src/SpecScribe/HtmlRenderAdapter.cs` -- rename the dark-bar `Delivery` group to `Work`, and render the white key-view band as compact grouped controls so README/PRD/Brief/UX docs sit under Docs instead of all being top-level chips.
- [x] `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- remove home-only `View epics`, `View sprint`, and `View sprint board` CTAs where surrounding elements already navigate; add journey grouping classes/markup around tile rows if the CSS needs semantic hooks.
- [x] `src/SpecScribe/assets/specscribe.css` -- style grouped key-view controls, ensure body-level stat tooltips layer above sticky nav, and add restrained visual grouping for home tiles using existing palette tokens.
- [x] Tests -- update/add focused assertions for Work label, Docs grouping, tooltip markup, removed CTAs, and preserved reachability/clickability.

**Acceptance Criteria:**
- Given a generated home page with stat tooltips, when a user hovers or focuses a first-row stat card, then the tooltip is visible above the sticky white key-view bar and remains keyboard reachable.
- Given README/PRD/Product Brief/UX docs are present, when the nav renders, then those related docs are grouped under Docs on the white band instead of appearing as five separate peer chips.
- Given epics/requirements/sprint views are present, when the header renders, then the dark-bar group label is `Work` and its targets remain reachable with no broken links.
- Given the home page renders, when scanning Epic Status, Overall Progress, Sunburst, and Now & Next, then redundant `View epics`, `View sprint`, and `View sprint board` CTAs are absent while the existing clickable cards/panels still navigate.
- Given the home tile band renders, when scanning left-to-right/top-to-bottom, then related cards are visually grouped into the intended flow from requirements through epics/stories, execution, review/follow-ups, architecture, and insights without changing counts or status colors.
- Given `dotnet test` runs, when the suite completes, then all updated nav/dashboard/style tests pass.

## Design Notes

Keep the home screen as a map of user journeys, not a file index. The dark bar is the broad site navigation (`Home`, `Docs`, `Architecture`, `Work`), while the white band should act as compact wayfinding into grouped supporting surfaces. The dashboard tiles should read as an application flow, but the grouping should stay lightweight: token-colored left rails, subtle separators, or small segment labels are safer than heavy boxes that compete with the actual cards.

For the tooltip fix, prefer reusing `.js-tip` because `specscribe.js` already creates a body-level `.ss-tooltip` with viewport clamping. CSS `data-tooltip::after` is the source of the clipping/layering problem near sticky nav, so new card tooltip work should not extend that path.

## Verification

**Commands:**
- `dotnet test` -- expected: all tests pass.
- `dotnet run --project src/SpecScribe -- generate` -- expected: site generation succeeds and `SpecScribeOutput/index.html` shows the updated nav/home layout.

**Manual checks:**
- Open the generated home page around the first tile row: stat-card tooltips are visible over the sticky nav; Docs grouping reduces the white-band item count; `Work` replaces `Delivery`; redundant `View epics/sprint` links are gone; tile grouping reads as a flow without obscuring counts.

## Dev Agent Record

### File List

- `src/SpecScribe/Charts.cs` -- stat-card tooltips now use `.js-tip` / `data-tip` with `title` fallback instead of clipped CSS `data-tooltip`.
- `src/SpecScribe/HtmlRenderAdapter.cs` -- dark-bar journey group renamed from `Delivery` to `Work`; white key-view band renders grouped Docs / Architecture / Work controls.
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- home tile band now renders lightweight journey segments; redundant home `View epics` / `View sprint` CTAs removed from compact tiles and panels.
- `src/SpecScribe/Icons.cs` -- added `Work` icon mapping while retaining `Delivery` as an alias.
- `src/SpecScribe/assets/specscribe.css` -- added grouped key-view dropdown styling, tile journey segment styling, and focus treatment for stat-card `.js-tip` links.
- `src/SpecScribe/assets/specscribe.js` -- suppresses native `title` tooltips while body-level `data-tip` / `data-tip-html` tooltips are active, then restores them.
- `tests/SpecScribe.Tests/ChartsTests.cs` -- updated stat-card tooltip markup expectations.
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs` -- updated home/dashboard/nav rendering assertions for grouped key views, journey tiles, and removed CTAs.
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` -- updated generated home expectations.
- `tests/SpecScribe.Tests/IconsTests.cs` -- added/updated icon coverage for `Work`.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- regenerated intentional golden fingerprint for nav/home/asset output changes.
- `tests/SpecScribe.Tests/StylesheetTests.cs` -- added/updated CSS invariant coverage for grouped key views and journey styling.

### Completion Notes

- Full `dotnet test` passed: 1169 passed, 0 failed, 0 skipped.
- `dotnet run --project src/SpecScribe -- generate` succeeded: 170 generated, 2 skipped.
- Generated `SpecScribeOutput/index.html` contains `Work` in the dark nav, grouped key-view controls, `tile-journey` segments, and stat-card `.js-tip` markup; no home `View epics` / `View sprint` CTAs were found in the targeted home checks.
