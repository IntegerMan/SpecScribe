---
title: 'Prev/next sibling navigation in entity page headers'
type: 'feature'
created: '2026-07-13'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: '19553fe8404e152b3adc7d11f243f63fcfcb3eb2'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A generated portal page for a commit, date, epic, story, ADR, retro, or code file dead-ends — to reach the adjacent entity you must climb back up via the breadcrumb and pick again. There is no one-hop way to walk a sequence. (Date pages already have an ad-hoc bottom prev/next; nothing else does, and it's inconsistent.)

**Approach:** Add a single shared "sibling pager" — a compact `‹ Prev` / `Next ›` control — rendered inline in every leaf entity page's title header, driven by each page family's natural sibling ordering. One reusable component + CSS, its data computed per family in the generator, and it replaces the existing bottom-of-page date-page nav so all families behave identically.

## Boundaries & Constraints

**Always:**
- Render the pager in the existing `.doc-header` band, **inline with the title**. It must add **zero page height** (absolute-positioned within the header's existing top padding) and **never collide horizontally** with the centered `<h1>` at any viewport width.
- Text is exactly `‹ Prev` and `Next ›`; the sibling's real name rides a `title=` tooltip. Every tooltip/label is escaped via `PathUtil.Html` (commit subjects, story/epic/ADR titles, filenames are free text).
- Ordering = predecessor/successor in each family's **canonical display order**, disabled (shown greyed, `aria-disabled`) at the ends — never wrap:
  - **Chronological families (commits, dates): newest-first**, so Prev = newer, Next = older.
  - **Numbered families (epics, stories, ADRs, retros): ascending by number**, so Prev = lower/earlier, Next = higher/later. Stories order globally across epics by (epic, story) number; retros by epic number.
  - **Code files: alphabetical within the same directory** (siblings = files in that directory).
- A family member with no siblings (a lone page, or the only file in its directory) renders **no pager** at all.
- Pages that gain a pager change bytes (expected); every page that does **not** gain one (Home, index pages, requirements, sprint, singletons, generic non-ADR docs) stays **byte-identical**. Keep the webview/SPA `RenderParity` harness green.

**Ask First:**
- If code-file sibling grouping (same-directory ordering) meaningfully expands beyond a lookup in the existing code-page loop, confirm before adding new git/FS walks.

**Never:**
- No wrap-around, no cross-family jumps (epic↔epic and story↔story only, never epic→story).
- No new JS — pure HTML/CSS, matching the site's no-JS charting discipline.
- Don't add pagers to index/landing/singleton pages (epics index, retros index, ADR landing, requirements, sprint, dashboard, git/about/diagnostics/timeline/code-map) or to generic module/planning docs and ADR README/template scaffolding — none are an ordered sibling series.
- Don't introduce `--status-*` colors here; the pager is neutral chrome.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Middle of family | entity has both neighbors | Both `‹ Prev` and `Next ›` are real links with tooltips | N/A |
| First in canonical order | newest commit/date; epic/story/adr/retro #1; first file in dir | `‹ Prev` greyed + `aria-disabled`; `Next ›` links | N/A |
| Last in canonical order | oldest commit/date; highest-numbered; last file in dir | `Next ›` greyed + `aria-disabled`; `‹ Prev` links | N/A |
| Only member | single commit / single retro / lone file in its dir | Whole pager omitted (no empty nav) | N/A |
| Chronological direction | any commit/date mid-sequence | Prev target is the NEWER sibling, Next the OLDER | N/A |
| Special chars in name | commit subject/story title with `<`, `&`, `"` | Escaped inside `title=` and any visible text | N/A |
| Date page migration | any active day | Inline header pager present; old bottom `commit-day-nav` gone | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/EntityPager.cs` -- NEW: the pager view model (`Prev`/`Next` nullable links) + the single render helper producing the inline markup; families build it via a small `FromSequence` factory.
- `src/SpecScribe/assets/specscribe.css` -- `.doc-header` gains `position: relative`; add `.entity-pager*` rules; remove the retired `.commit-day-nav` block.
- `src/SpecScribe/HtmlTemplater.cs` -- `RenderPage` gains optional `EntityPager?`; emit it in the `doc-header` (used by ADRs; null elsewhere = byte-identical).
- `src/SpecScribe/CommitDetailTemplater.cs`, `CommitDayTemplater.cs`, `RetroTemplater.cs`, `CodeFileTemplater.cs` -- each `RenderPage` gains optional `EntityPager?`, emitted in its `doc-header`. CommitDay also **drops** its old `prevDay/nextDay` bottom-nav.
- `src/SpecScribe/EpicsViewBuilder.cs` + `HtmlRenderAdapter.Epics.cs` + view models (`EpicsView.cs`) -- thread a pager into `EpicPageView`/`StoryPageView`; adapter injects it into the epic/story `doc-header`.
- `src/SpecScribe/SiteGenerator.cs` -- for each family loop (commit-detail, commit-day, ADR, retro, epic/story, code-file), build the ordered sibling list and pass each page its `EntityPager`.
- `tests/SpecScribe.Tests/` -- new `EntityPagerTests.cs`; update `CommitDayTemplaterTests`, `CommitDetailTemplaterTests`, family/`SiteGenerator*` tests; regenerate the `GenerateAll_GoldenContentFingerprint` constant in `SiteGeneratorAdapterTests.cs`.

## Tasks & Acceptance

**Execution:**
- [ ] `src/SpecScribe/EntityPager.cs` -- Create `EntityPager` (record with `Prev`/`Next` nullable `PagerLink(Href, Label)`), a `FromSequence<T>(IReadOnlyList<T> canonicalOrder, int index, Func<T,string> href, Func<T,string> label)` factory (null at ends), and a `Render()` helper emitting `<nav class="entity-pager" aria-label="Sibling navigation">` with a link or `aria-disabled` `<span>` per side; returns empty string when both sides are null.
- [ ] `src/SpecScribe/assets/specscribe.css` -- Add `position: relative` to `.doc-header`; add `.entity-pager` (absolute top-right within header padding, flex, small, focus-visible + hover mirroring old `.commit-day-nav`) and `.entity-pager-link.is-disabled { opacity:.4; pointer-events:none }`; delete the `.commit-day-nav` rules.
- [ ] `src/SpecScribe/HtmlTemplater.cs` -- Add optional `EntityPager? pager = null` to `RenderPage`; render `pager?.Render()` inside `doc-header` (top of header). Null callers unchanged.
- [ ] `src/SpecScribe/CommitDetailTemplater.cs` -- Add `EntityPager? pager` param; emit in `doc-header`.
- [ ] `src/SpecScribe/CommitDayTemplater.cs` -- Add `EntityPager? pager` param, emit in `doc-header`; remove `prevDay`/`nextDay` params and the bottom `commit-day-nav` block.
- [ ] `src/SpecScribe/RetroTemplater.cs` -- Add `EntityPager? pager` param; emit in `retro-header`.
- [ ] `src/SpecScribe/CodeFileTemplater.cs` -- Add `EntityPager? pager` param; emit in its `doc-header`.
- [ ] `src/SpecScribe/EpicsViewBuilder.cs`, `HtmlRenderAdapter.Epics.cs`, `EpicsView.cs` -- Add a pager field to `EpicPageView`/`StoryPageView`; `EpicsViewBuilder.BuildEpic/BuildStory/BuildStoryPlaceholder` accept the page's neighbors; adapter renders it in the epic/story `doc-header`.
- [ ] `src/SpecScribe/SiteGenerator.cs` -- In each family's render loop, build the canonical ordered sibling list (commits newest-first as logged; days newest-first; `_adrs` record subset; retros by epic; a flat global story list across epics; code files grouped+sorted per directory) and pass each page its `EntityPager` (hrefs relative to that page). Flip the day loop to newest-first ordering.
- [ ] `tests/SpecScribe.Tests/EntityPagerTests.cs` (new) + family/generator test updates -- Cover the I/O matrix (ends disabled, lone-member omission, chronological direction, escaping); update templater signatures; regenerate `GenerateAll_GoldenContentFingerprint`.

**Acceptance Criteria:**
- Given a portal generated with git history and epics, when I open any commit/date/epic/story/ADR/retro/code-file page that has a sibling, then its title header shows an inline `‹ Prev` / `Next ›` control linking to the correct adjacent entity, with the sibling's name in a tooltip.
- Given a page at the start or end of its family's canonical order, when it renders, then the unavailable direction is shown disabled (greyed, `aria-disabled`) and never links or wraps.
- Given a commit or date page, when I follow `‹ Prev`, then I move to the **newer** sibling (Next → older).
- Given the pager is present, when the page renders at desktop and mobile widths, then no page height was added and the control does not overlap the `<h1>`.
- Given generation of any page family that does NOT gain a pager, when compared to the prior output, then those pages are byte-identical (fingerprint changes only for pager-bearing pages), and `RenderParity` stays green.

## Design Notes

Single canonical rule keeps every family one code path: **pager = predecessor/successor in that family's display order.** Chronological families are displayed newest-first, so "predecessor" is automatically the newer item — no special-casing beyond choosing each family's sort. `FromSequence` takes the already-sorted list + the current index and returns the two neighbors (or null), so families only decide sort + label + href.

Placement sketch (header stays centered; pager floats in the existing top padding, adds no height):
```html
<header class="doc-header">          <!-- position: relative -->
  <nav class="entity-pager" aria-label="Sibling navigation">
    <a class="entity-pager-link entity-pager-prev" href="a1b2c3d.html" title="Fix ADR landing 404s" rel="prev">&lsaquo; Prev</a>
    <span class="entity-pager-link entity-pager-next is-disabled" aria-disabled="true">Next &rsaquo;</span>
  </nav>
  <div class="story-kicker">Commit Detail</div>
  <h1>…</h1>
</header>
```
Sibling hrefs are same-directory filenames for every family (`commit/`, `commits/`, `epics/`, `adrs/`, retro dir, `code/…`), so the link is just the neighbor's file name relative to the current page — no output-root prefix juggling.

## Verification

**Commands:**
- `dotnet build src/SpecScribe/SpecScribe.csproj` -- expected: builds clean.
- `dotnet test` -- expected: all green (baseline ~965+), including the regenerated fingerprint and new `EntityPagerTests`.

**Manual checks:**
- Generate against this repo (has deep git + epics + ADRs + retros): open a mid-sequence commit page and a date page — confirm `‹ Prev` goes to the **newer** sibling; open Epic 1 and the last epic — confirm the correct end is disabled; open a lone-sibling page — confirm no pager; shrink to mobile width — confirm the control sits in the header with no `<h1>` overlap and no added height.
