---
baseline_commit: f0f30bdfaa942b377f6413ec67264a618a4ff958
---

# Story 10.9: Client-Light Sort, Group & Filter on List Pages

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer hunting one item in a long list,
I want to sort, group, and text-filter a list page in place,
so that a hundred-row index becomes reachable without scrolling the whole thing.

## Acceptance Criteria

1.
**Given** a list page with JavaScript available
**When** I sort (status / date / name), toggle grouping, or type into a filter
**Then** rows reorder or hide live client-side within the existing progressive-enhancement JS budget (not a second client stack)
**And** the controls are keyboard-operable with `aria` state.

2.
**Given** JavaScript off (NFR8)
**When** the page loads
**Then** it renders in a sensible server-defined default order with every row present
**And** the sort/group/filter controls are a progressive enhancement, never a gate on seeing the data.

## Context & Why This Story Exists

Story 10.8 just gave five list-shaped pages one shared row grammar (`ListRow`/`FollowUpRow`). This story is the payoff: layer a live, client-side sort/group/filter bar on top of that shared grammar so a long list (dozens of action items, deferred-work entries, or ADR records) becomes scannable without a full-page scroll. It must be a **progressive enhancement** — the server-rendered order is always correct and complete on its own (NFR8) — and it must **reuse the existing client-JS pattern already proven in this codebase**, not invent a second one.

### Correction to the epics text — there is no "Epic 20" JS budget yet

The epics.md note for this story says it "reuses the Epic 20 interactivity budget." **Epic 20 (Interactive Project Explorer) is still `backlog` — its architecture spike (Story 20.1, which is where a JS size/dependency budget would actually get *named*) has not run.** There is nothing in Epic 20 to reuse yet; treat that epics.md phrasing as aspirational, not a real dependency, and do **not** block or wait on Epic 20.

The actual, already-shipped budget this story must extend is `src/SpecScribe/assets/specscribe.js` — one dependency-free, static-host-safe file, explicitly headed "the ONE sanctioned client-side addition" (comment at `specscribe.js:1`). It already contains **two directly-reusable precedents for this exact feature**:

- **`enhanceSortableTable`** (`specscribe.js:329-420`, Story 3.8, Git Insights hub) — progressive-enhancement sortable/filterable `<table>`: turns `<th>` into buttons that re-sort the already-present `<tbody>` rows by `data-sort-value` (or `data-sort="num"`), announces via `aria-sort`, and optionally injects a labeled text filter (`data-filter-label`) that hides non-matching rows and reports "N of M rows" via an `aria-live` counter (`.gi-filter`, CSS at `specscribe.css:5127`).
- **`enhanceSprintEpicFilter`** (`specscribe.js:426-587`, sprint board) — a similar pattern for grouping/filtering card-shaped rows (not just table rows) by a data-driven category, with an "All" reset and live counts.

This story's job is to **generalize that same pattern to `<li>`-shaped list rows** (`.followup-row` / `.list-row`) instead of `<table>` rows — same discipline (opt-in via a data attribute so no dead control ships in the no-JS page, `aria-live` counts, display-based hiding so reduced-motion is satisfied by construction), new function, same file, same budget. This is a "new function in the existing sanctioned file," not a new script or framework.

### What "list page" means here — scope call (flag for review)

Story 10.8 found that only **some** of the six index-shaped pages actually render through the shared `<li>` row grammar (`ListRow`/`FollowUpRow`); the rest (Requirements cards, Epics/Story cards, Code Map/Code File `<table>`s) already conform to the row-*anatomy* contract but keep their own established markup. This story's client-side sort/group/filter mechanism can only be written once, generically, against a **shared DOM shape** — so it targets the pages that actually share one:

| Page | Renderer | Row shape | In scope for 10.9? |
|------|----------|-----------|---------------------|
| `action-items.html` | `FollowUpRow.Render` | `<li class="followup-row">` in `<ul class="followup-rows-list">` | **Yes** |
| `deferred-work.html` | `FollowUpRow.Render` | same | **Yes** |
| `follow-ups/group-*.html` | `FollowUpRow.Render` | same | **Yes** |
| ADR landing (synthesized branch, no README) | `ListRow.Render` | `<li class="list-row">` in `<ul class="list-rows-list">` | **Yes** |
| `timeline.html` | day-grouped, own wrapper | `.list-row-scan`/`.list-row-meta` but already grouped-by-day and chronological by construction | **Out** — already grouped/ordered the way a maintainer wants; re-sorting inside a day doesn't add value. Flag for review if the owner disagrees. |
| Requirements index, Epics/Story cards, Code Map/Code File tables | own card/table grammar (10.8 left unchanged) | `<div class="…-card">` / `<table><tr>` | **Out of this story** — no shared `<li>` shape to hook a single generic enhancement onto without page-specific wiring. Code Map/Code File are already sortable-by-column via native `<table>` semantics; Requirements/Epics card density hasn't been reported as a legibility problem the way long follow-up/ADR lists have. Flagged as a follow-on if the owner wants it — **do not silently expand scope to card grids without confirming**, since it would mean writing a second, card-shaped enhancement, not reusing this one. |

This mirrors the existing `table.js-sortable` precedent, which is *also* opt-in per page (Git Insights hub tables have it; other tables don't) — this story keeps that same discretion.

Serves the onboarding/legibility mission (FR27–29) and directly extends Story 10.8. Load-bearing: **NFR8** (JS-off renders complete + correctly ordered), **NFR5** (progressive enhancement only — mirrors the `js-sortable`/`sprint-epic-filter` doc comments verbatim), **Story 8.2** (`StatusStyles` is the only status vocabulary — grouping by status must use the same tokens/labels, never a parallel scheme).

## Design Direction (read before implementing)

1. **One new function in `specscribe.js`**, e.g. `enhanceListRows(container)`, modeled directly on `enhanceSortableTable`/`enhanceSprintEpicFilter`'s shape (create controls only when opting in, mutate in place, `try { } catch { /* degrade silently */ }` around each container). Opt in via a new class on the `<ul>` wrapper, e.g. `.followup-rows-list.js-listable` / `.list-rows-list.js-listable` — **only add the class on pages actually wired for this** (per the scope table above), exactly like `table.js-sortable` is opt-in per table today.
2. **Sort** reuses the `data-sort-value` convention `enhanceSortableTable` already established (don't invent a second attribute name): each `<li>` carries `data-sort-name`, and — only where the underlying data actually has it — `data-sort-date` (machine-sortable, `PortalDates.IsoDay`/ISO form, mirroring the existing heatmap-href ISO convention) and `data-sort-status` (the `StatusStyles` token, or an explicit rank if visual severity order — not alphabetical — is the intended read; confirm with `StatusStyles`' existing ordering rather than inventing a new one). Render a labeled `<select>` of only the sort keys the page's rows actually populate — a page with no dates must not offer a dead "Sort by date" option. Sorting reorders the existing `<li>` elements in place (`appendChild`, same trick `enhanceSortableTable`'s `applySort` uses) — no re-render, no data refetch.
3. **Group** toggles rows into `<h3>`/`role="group"` clusters keyed by the row's existing status token (reuse `StatusStyles`' label/order — do not author a second status vocabulary for grouping labels). Off by default (server order stands); toggling is a `<button>`/checkbox with `aria-pressed` state. Grouping and sorting compose (group first, then sort within each group) rather than being mutually exclusive.
4. **Filter** is the exact `enhanceSortableTable` text-filter pattern (`data-filter-label`-style labeled `<input type="search">`, injected only when opted in, `aria-live` "N of M rows" count, `row.textContent` substring match, hide via a class not `display:none`-in-CSS-only so it participates the same way `.gi-row-hidden` does) applied to `<li>` instead of `<tr>`.
5. **Server side stays untouched for ordering.** `ListRow.Render`/`FollowUpRow.Render` gain **new, additive, default-valued optional parameters** to emit the `data-sort-*` attributes (do not touch existing required parameters or the ~40 tests from Story 10.8/9.10 pinning current call signatures and markup). Passing nothing keeps a page in its current, unenhanced state — the mechanism degrades per-page, not globally.
6. **Keyboard + `aria` (AC #1's explicit teeth):** sort control is a real `<select>`/button (native keyboard support for free); group toggle is a real `<button>` with `aria-pressed`; filter input has an associated `<label>`; the live row count uses `aria-live="polite"` exactly like `.gi-filter-count` does today. No custom widget, no keydown reimplementation needed if native controls are used — mirror `enhanceSprintEpicFilter`'s use of real `<input type=checkbox>`/`<button>` rather than building ARIA-pattern widgets from `<div>`s.
7. **No new CSS token, no color-only state.** Reuse `--status-*`/`StatusStyles` for any grouped-by-status heading badge; the new control bar chrome can reuse `.gi-filter`'s existing visual language (label + input + count) rather than inventing new chrome.

## Tasks / Subtasks

- [ ] **Task 1 — Server-side sort/group data attributes** (AC: 1, 2)
  - [ ] Add additive, default-valued optional parameters to `ListRow.Render` (`src/SpecScribe/ListRow.cs`) and `FollowUpRow.Render` (`src/SpecScribe/FollowUpRow.cs`) to emit `data-sort-name`, and — only when the caller has the data — `data-sort-date`/`data-sort-status` on the `<li>`. Zero existing call sites change behavior if they pass nothing.
  - [ ] Wire the new parameters at the four in-scope call sites: `ActionItemsTemplater.cs`, `DeferredWorkTemplater.cs`, `FollowUpGroupTemplater.cs` (status + name; no per-item date field exists on action items/deferred work today — do not fabricate one), and `SiteGenerator.cs`'s synthesized ADR landing list (`ListRow.Render` call, ~SiteGenerator.cs:859) which does have `AdrEntry.Date` — thread it through via `PortalDates.IsoDay`.
  - [ ] Add the opt-in `js-listable` class to the four in-scope pages' `<ul class="followup-rows-list">`/`<ul class="list-rows-list">` wrapper only — leave `timeline.html` and every other list/table untouched (per the scope table).

- [ ] **Task 2 — `enhanceListRows` client enhancement** (AC: 1, 2)
  - [ ] Add `enhanceListRows(container)` to `src/SpecScribe/assets/specscribe.js`, modeled on `enhanceSortableTable`: reads `data-sort-*` presence to decide which sort options to offer, injects a labeled `<select>` (sort) + `<button aria-pressed>` (group toggle) + `<input type="search">` (filter, with `aria-live` count) above the `<ul>`, all created only at enhancement time (nothing dead ships in the no-JS page).
  - [ ] Sort reorders `<li>` via `appendChild` (no re-render). Group wraps/reveals `<h3>` cluster headings keyed by the row's status token + label (via the row's existing badge text/class — do not duplicate `StatusStyles` vocabulary into JS). Filter hides non-matching `<li>` via a class, updates the live count.
  - [ ] Wire `Array.prototype.forEach.call(document.querySelectorAll(".js-listable"), enhanceListRows)` with the same `try { } catch { /* degrade silently */ }` guard the other enhancers use.

- [ ] **Task 3 — CSS for the new control bar** (AC: 1)
  - [ ] Add a small `.list-controls`/`.list-controls-*` rule set to `specscribe.css`, reusing `.gi-filter`'s visual language (label/input/count spacing, focus-visible outline) rather than inventing new chrome. No new `--status-*` token.

- [ ] **Task 4 — Tests** (AC: 1, 2)
  - [ ] C# tests confirming the new `data-sort-*` attributes render correctly at each of the four call sites (and are absent/omitted where no data exists, e.g. no `data-sort-date` on action items) — extends `FollowUpSurfacesTests`/`ListRowTests`/whatever ADR-list test Story 10.8 added.
  - [ ] Confirm the `js-listable` class appears only on the four in-scope pages and NOT on `timeline.html` or any other list — a regression test pinning the opt-in boundary the same way `table.js-sortable` scope is implicitly pinned today.
  - [ ] JS-off path: existing HTML-only assertions (row order, row presence) must need zero changes — this is the AC #2 proof; if any existing test needs to change to keep passing, that is itself a signal the feature broke the no-JS baseline.
  - [ ] If a JS test harness exists in this repo for `specscribe.js` (check first — Story 3.8/7.6 landed JS with no JS unit-test harness historically, verification was manual/browser-based); if none exists, verify `enhanceListRows` manually in the Browser pane per the Testing standards below rather than inventing a new JS test framework for one function.

- [ ] **Task 5 — Verify end-to-end** (AC: 1, 2)
  - [ ] Generate against this repo's own history (`dotnet run --project src/SpecScribe -- generate --deep-git`) and open `action-items.html`/`deferred-work.html`/the ADR landing (only fires when no README occupies the slot — this repo has one, so verify via a README-less fixture or a temporary local check) in the Browser pane.
  - [ ] Confirm: sort/group/filter work with JS on; disabling JS (or reading the raw generated HTML) shows every row present in server order with no dead controls; keyboard-only operation of the select/button/input; `aria-live` count updates on filter.

## Dev Notes

### Architecture patterns & constraints (must follow)

- **Extend, don't duplicate** (repo-wide principle) — `enhanceListRows` is a fourth sibling of `enhanceSortableTable`/`enhanceSprintEpicFilter`/`initCodeMapPanel` in the ONE sanctioned `specscribe.js`, not a new script, not a bundler, not a framework.
- **Progressive enhancement is non-negotiable (NFR5/NFR8)** — every in-scope page already renders completely and correctly ordered server-side (Story 10.8's shared grammar). This story must never become a gate on seeing the data; verify by reading the generated HTML with the `<script>` tag stripped, not just by disabling JS in a browser.
- **`StatusStyles` is the only status vocabulary** (Story 8.2) — grouping-by-status headings must reuse the badge's existing label/token, never a second hardcoded string list in JS.
- **Never color-only** (UX-DR17) — group headings need visible text (status label), not just a colored divider.
- **No new authoring schema** — every field surfaced (name, date, status) already exists on today's view models; this story only exposes it as a `data-*` attribute for the client to read.

### Source tree — files to touch

| File | Change |
|------|--------|
| `src/SpecScribe/ListRow.cs` | Additive optional `data-sort-*` params on `Render` (**UPDATE**) |
| `src/SpecScribe/FollowUpRow.cs` | Additive optional `data-sort-*` params on `Render` (**UPDATE**) |
| `src/SpecScribe/ActionItemsTemplater.cs` | Pass sort/status data + opt the list into `js-listable` (**UPDATE**) |
| `src/SpecScribe/DeferredWorkTemplater.cs` | Same (**UPDATE**) |
| `src/SpecScribe/FollowUpGroupTemplater.cs` | Same (**UPDATE**) |
| `src/SpecScribe/SiteGenerator.cs` | ADR landing list: pass `AdrEntry.Date` + opt in (**UPDATE**, ~line 859) |
| `src/SpecScribe/assets/specscribe.js` | New `enhanceListRows` function + wiring (**UPDATE**) |
| `src/SpecScribe/assets/specscribe.css` | New `.list-controls*` rules (**UPDATE**) |
| `tests/SpecScribe.Tests/*` | New/extended assertions per Task 4 (**UPDATE**) |

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Table sort/filter precedent | `enhanceSortableTable` | `specscribe.js:329-420` |
| Card-group filter precedent | `enhanceSprintEpicFilter` | `specscribe.js:426-587` |
| Live row-count pattern | `.gi-filter-count` + `aria-live="polite"` | `specscribe.js:412-413`, `specscribe.css:5127-5139` |
| Machine-sortable date | `PortalDates.IsoDay(DateOnly)` | `PortalDates.cs:45` |
| Status vocabulary | `StatusStyles.Badge`/`ForSprint`/`SprintLabel` | `StatusStyles.cs` |
| Shared row grammar this builds on | `ListRow.Render` / `FollowUpRow.Render` | `ListRow.cs`, `FollowUpRow.cs` |

### Guardrails & invariants

- **Do not touch `timeline.html` or the Requirements/Epics/Code Map/Code File pages** — out of scope per the table above; flag to the owner in review if broader coverage is actually wanted (that would mean a second, differently-shaped enhancement, not scope creep on this one).
- **Do not rename or make required any existing `ListRow.Render`/`FollowUpRow.Render` parameter** — Story 10.8's and Story 9.10's pinned tests must stay green with zero edits beyond additive new assertions.
- **Do not fabricate a date field on action items/deferred-work items that don't have one** — omit the date sort option there rather than inventing a fake date.
- **The copy-payload trap from 9.10/10.8 still applies** — `data-copy` on action items must stay raw; this story's filter/sort must not touch or re-render that attribute.

### Previous story intelligence

- **From 10.8 (review):** the shared `<li>` grammar exists in exactly four places today (action items, deferred work, follow-up groups, synthesized ADR landing) — everything else is a different DOM shape by deliberate, reviewed decision. Golden fingerprint has a stale-first-hash trap — confirm a hash is stable across ≥2 repeated runs before locking it in ([golden-diff-normalization-gotchas]).
- **From 3.8 (Git Insights hub, shipped):** the exact sort/filter JS pattern this story generalizes already exists and is battle-tested — read `enhanceSortableTable` fully before writing `enhanceListRows`; don't redesign the interaction model, port it.
- **From this session's own investigation:** the epics.md "Epic 20 interactivity budget" reference is aspirational — Epic 20 is still `backlog` with its spike (20.1) not started. Do not treat it as a real dependency or wait on it.

### Testing standards

- xUnit; `Assert.Contains`/`DoesNotContain` on emitted HTML for the new `data-sort-*` attributes and `js-listable` opt-in class, matching existing `FollowUpSurfacesTests`/`ListRowTests` style.
- No JS unit-test harness exists in this repo historically (Story 3.8/7.6's client JS was verified manually/via the Browser pane, not an automated JS test suite) — follow that precedent for `enhanceListRows` rather than introducing a new JS test framework for one function.
- Full suite green including golden + `RenderParity` + SPA/webview parity suites (the four in-scope pages are standalone `WriteOutput` templaters, same family as Story 9.10/10.8 — no `PageView`/`RenderParity` involvement expected, but confirm).

### Project Structure Notes

- No new authoring schema, no `sprint-status.yaml`/epics.md shape changes.
- One new JS function in the existing sanctioned file; no new script tag, no bundler, no framework introduced.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.9]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 10]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 20] — confirms Epic 20 is spike-led and not yet started; this story does not depend on it
- [Source: _bmad-output/implementation-artifacts/10-8-unified-list-page-grammar-across-every-index.md] — the shared row grammar this story layers controls on top of
- [Source: _bmad-output/implementation-artifacts/9-10-scannable-follow-up-list-pages.md] — the copy-payload trap
- [Source: src/SpecScribe/assets/specscribe.js] — `enhanceSortableTable` (Story 3.8), `enhanceSprintEpicFilter`, the file-header "ONE sanctioned client-side addition" comment
- [Source: src/SpecScribe/ListRow.cs] / [src/SpecScribe/FollowUpRow.cs]
- [Source: src/SpecScribe/StatusStyles.cs]
- [Source: src/SpecScribe/PortalDates.cs]
- [Source: src/SpecScribe/assets/specscribe.css#.gi-filter]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md] — SCP seating this story

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

### Completion Notes List

Ultimate context engine analysis completed — comprehensive developer guide created. This story layers a client-light sort/group/filter bar onto the four list pages Story 10.8 already unified onto the shared `ListRow`/`FollowUpRow` grammar (action items, deferred work, follow-up groups, synthesized ADR landing), generalizing the existing `enhanceSortableTable`/`enhanceSprintEpicFilter` progressive-enhancement pattern from `specscribe.js` (Story 3.8) rather than the not-yet-built "Epic 20" the epics.md text mistakenly names — Epic 20 is still backlog with its budget-defining spike unstarted, so this story is actually establishing that precedent's next application, not consuming an existing one.

### File List
