---
baseline_commit: dd343ec40d6fa4efa04d622dee38f4a4d77d1dd7
---

# Story 10.1: Insights Navigation and Structure Page Retirement

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a returning user on any interior page,
I want the top navigation reimagined into a hierarchical, journey-organized structure — with insight pages and follow-up pages reachable directly from the nav,
so that Git Insights, Deep Analytics, Action Items, and Deferred Work no longer require a round-trip through Home, and the Structure slot (retired in the 2026-07-08 correct-course) stops occupying a top-nav position.

## Context & Why This Story Exists

Today the top nav (`SiteNav.Build` → `NavigationView.Items`) is a **flat, ordered list** of pure-static links: `Home · [Readme] · [PRD] · [Architecture] · [ADRs] · Epics · Requirements · [Sprint] · [Structure]`. Four data-dense pages are **not** in the nav at all and are reachable only via dashboard callouts, forcing the "round-trip through Home" this story eliminates:

- **Git Insights** (`git-insights.html`) — reached only from the dashboard Git Pulse panel's "View all git insights →" link ([HtmlRenderAdapter.Dashboard.cs:90](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)).
- **Deep Analytics** (`deep-analytics.html`) — reached only from the same panel's "View Deep Analytics →" link ([HtmlRenderAdapter.Dashboard.cs:92](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)).
- **Action Items** (`action-items.html`) — reached from a dashboard retro-callout + the Sprint page flag button ([HtmlRenderAdapter.Dashboard.cs:293](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)).
- **Deferred Work** (`deferred-work.html`, a generic page from `deferred-work.md`) — reached from a dashboard work-callout ([HtmlRenderAdapter.Dashboard.cs:282](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)).

This directly serves **Journey 6 (health & hotspots)** and **Journey 7 (debt & follow-ups)** from [docs/UserJourneys.md](docs/UserJourneys.md), both of which explicitly call out "these pages reachable from the top nav, not only via dashboard deep links."

**Owner direction (elicited at create-story, 2026-07-12):** rather than bolt a single flat "Insights" item onto the existing list, *reimagine the top-level nav into a more hierarchical, organized structure — organized by primary tasks, roles, or journeys.* The owner separately selected a dedicated **"Follow-ups"** group for Action Items + Deferred Work. So this story delivers a **grouped nav model** (top-level groups → child links), with Insights and Follow-ups as two of the groups, and the group taxonomy grounded in the seven journeys.

## Acceptance Criteria

**AC1 (Insights + Follow-ups reachable from a hierarchical nav)**
Given the portal has `git-insights.html` and `deep-analytics.html` pages,
When navigation renders,
Then an **"Insights"** nav group surfaces them as direct children (no Home round-trip),
And **Action Items** and **Deferred Work** are reachable under a dedicated **"Follow-ups"** nav group,
And the top nav is organized into journey-oriented groups (not a single flat list), with group headers that disclose their child links.

**AC2 (Structure retirement + data-gated entries, NFR8)**
Given the Structure page's scope was retired (2026-07-08 correct-course; treemap re-seated as Story 7.6),
When navigation renders,
Then **Structure no longer holds a top-nav slot** (nav item + dashboard quick-link removed),
And **every nav group and child renders only when its underlying data exists** — so a shallow repo (no deep-git data, no sprint, no action items, no deferred work) gets no empty groups and no dead links (NFR8),
And an empty group (all children unavailable) does not render at all.

## Design Direction — Journey-Organized Nav Taxonomy

**This taxonomy is the #1 review checkpoint.** It is the "silhouette" of the new nav; confirm group names + membership with the owner before/at review rather than after. It is grounded in the seven journeys in [docs/UserJourneys.md](docs/UserJourneys.md).

Recommended top-level structure (each group renders only children whose data is available; an all-unavailable group is omitted entirely):

| Top-level | Kind | Children (in order) | Journey grounding | Availability gate |
|-----------|------|---------------------|-------------------|-------------------|
| **Home** | flat link | — (`index.html`) | J1/J2 daily pulse — the entry point | always |
| **Delivery** | group | Epics, Requirements, Sprint | J1 pulse, J2 work-selection, J4 traceability | Epics/Requirements gate on epics.md; Sprint gates on `SprintAvailable` |
| **Insights** | group | Git Insights, Deep Analytics | J6 health & hotspots | each child gates on the deep-git data signal (see Critical Timing Note) |
| **Follow-ups** | group | Action Items, Deferred Work | J7 debt & follow-ups | Action Items gates on `_sprint.OpenActionItems`; Deferred Work gates on `WorkInventory.Deferred` |
| **Project** | group | Readme, PRD, Architecture, Spec, ADRs | J5 onboarding | each child gates on its existing discovery signal |

Notes & latitude:
- **Home stays a flat top-level link** (not a group) — it is the always-present anchor of Journeys 1/2.
- **A group with exactly one available child MAY render that child as a flat top-level link** instead of a single-item disclosure (avoids a pointless one-item dropdown). This keeps a shallow repo's nav clean. Treat this as a recommended refinement; confirm at review.
- **Group headers are NOT landing pages** — this story adds **no new landing/index pages**. A group header is a disclosure control (see Disclosure Mechanism). Git Insights already exists as the aggregate hub for its own page; we are not building an `insights.html` index.
- Module-doc labels (PRD/Architecture/GDD/Narrative/etc.) come from `ModuleContext.Docs` and vary by detected framework — keep them adapter-supplied inside the **Project** group; do not hard-code BMAD labels.
- If the owner prefers different group names (e.g. "Plan" vs "Delivery", "Docs" vs "Project", "Health" vs "Insights"), that is a pure label change in `SiteNav.Build` — cheap to adjust.

### Disclosure Mechanism — pure-CSS, cross-surface-safe (load-bearing constraint)

The nav is rendered **once** by `HtmlRenderAdapter.RenderNavMarkup` and that exact markup is reused by **all three delivery surfaces** — HTML (`RenderNav` = markup + inline toggle script), the **webview** (`RenderNavMarkup` directly, under a strict CSP that blocks non-nonce'd inline scripts — [HtmlRenderAdapter.cs:48-52](src/SpecScribe/HtmlRenderAdapter.cs)), and the **SPA** (`HtmlRenderAdapter.Shared.RenderNavMarkup(...)` per swap — [SiteGenerator.cs:997](src/SpecScribe/SiteGenerator.cs)). Therefore the group disclosure **MUST NOT depend on any information-bearing JavaScript.**

**Use native `<details>`/`<summary>` disclosure** (or a pure-CSS `:focus-within`/`:hover` menu) for each group. Rationale:
- Works with zero JS, so webview CSP and SPA innerHTML-swaps are both satisfied with no exception needed.
- Natively keyboard-accessible (Story 1.4/1.5 a11y baseline) — `<summary>` is focusable and toggles on Enter/Space for free.
- **Precedent exists in this codebase**: the retired structure tree already used native nested `<details>` (`class="structure-tree"`, [SiteGenerator.cs:1318-1320](src/SpecScribe/SiteGenerator.cs)) and the existing nav already has a mobile collapse (`.site-nav-toggle`, ≤640px). Nested `<details>` inside the mobile menu is fine.
- **Keep group headers as non-anchor elements** (`<summary>`, not `<a>`). This is critical for parity: `RenderParity.ExtractNav` recovers nav facts from `<a>` anchors inside `site-nav-links`, so non-anchor group headers are transparent to it and the flat-leaf nav-fact check keeps passing (see Testing).

## Critical Timing Note — git-page availability IS knowable at nav-build time

The doc comments on `GitInsightsOutputPath` / `DeepAnalyticsOutputPath` claim these can't be nav items because "nav is built before git is computed, so a nav entry could dangle" ([SiteNav.cs:17-26](src/SpecScribe/SiteNav.cs)). **That comment is stale** and is the reason AC1 was previously blocked. Since the Story 4.1 ingestion refactor:

- `ComputeProgress` computes **both** the git pulse **and** deep-git ([SiteGenerator.cs:1125-1133](src/SpecScribe/SiteGenerator.cs)).
- It runs via the `Ingest` callback at [SiteGenerator.cs:92](src/SpecScribe/SiteGenerator.cs) — the `progress` **local** is populated **before** `SiteNav.Build` at [SiteGenerator.cs:100](src/SpecScribe/SiteGenerator.cs).

So the availability signal for both git pages is already in hand at nav-build time. Thread it in exactly like the existing `hasSprint`/`hasStructure`/`hasAdrs` gates:

- Add `hasGitInsights` (⇐ `progress?.DeepGit?.Insights is not null`) and `hasDeepAnalytics` (⇐ `progress?.DeepGit is not null`) parameters to `SiteNav.Build`.
- Pass them from **both** `SiteNav.Build` call sites — the main build ([SiteGenerator.cs:100](src/SpecScribe/SiteGenerator.cs)) **and** the README-error rebuild ([SiteGenerator.cs:111](src/SpecScribe/SiteGenerator.cs)) — plus any `BuildNav` helper used by the watch-mode paths (`RegenerateEpics` rebuilds nav; ensure it recomputes/carries the same flags from the current `_progress`).
- **Update the stale doc comments** on `GitInsightsOutputPath`/`DeepAnalyticsOutputPath` to reflect that they now ride the nav via the Insights group.

**Known, accepted tradeoff (document it so the reviewer doesn't flag it):** the git pages are *rendered* after nav is built (lines 188-214) and their `try/catch` clears `_progress.DeepGit`/`.Insights` on a render failure to prevent the *dashboard* link dangling. If a git page fails to render *after* the nav was already embedded in earlier pages, the Insights child link would point at a page that wasn't written. This is the identical data-signal-vs-render-success contract that Structure, Sprint, and ADRs already accept (they gate the nav on the input signal, not on successful render), and a git-page render failure is an NFR2-exceptional degradation. Gate on the data signal; do **not** attempt a post-render nav rebuild (it can't retroactively fix already-written pages). State this tradeoff in a code comment.

**Also note:** `--deep-git` is strictly opt-in and **off by default** ([SiteGenerator.cs:1131](src/SpecScribe/SiteGenerator.cs) — `_options.DeepGitAnalytics ? ... : null`). So in the common run there are **no** git-insight pages and the **Insights group simply does not appear** — this is correct data-gated behavior (AC2/NFR8), not a bug. Your tests must cover both the deep-git-on and deep-git-off cases.

## Tasks / Subtasks

- [x] **Task 1 — Introduce the hierarchical nav model** (AC: 1)
  - [x] In [NavigationView.cs](src/SpecScribe/NavigationView.cs), add a `NavGroup` record: `(string Label, string ConceptKey, IReadOnlyList<NavItem> Children)`. Keep `NavItem` as-is (it is the leaf).
  - [x] Add `IReadOnlyList<NavGroup> Groups` to `NavigationView` as the hierarchical structure the renderer consumes. **Keep `Items` as a FLATTENED, in-render-order projection of every leaf** (group children flattened, plus any flat top-level links like Home). This preserves every existing flat consumer: `RenderParity.FromPageView` (nav-fact check), the SPA manifest (`SpaBundle(... nav.Items ...)`, [SiteGenerator.cs:1004](src/SpecScribe/SiteGenerator.cs)), and the dashboard active-item logic. Populate `Groups` and `Items` from one source so they can never disagree.
  - [x] Update `SiteNav.ToNavigationView` ([SiteNav.cs:178](src/SpecScribe/SiteNav.cs)) to project the grouped structure into `NavGroup`s (concept key = group label, mirroring the existing per-item convention) and the flattened `Items`.

- [x] **Task 2 — Assemble the grouped taxonomy in `SiteNav.Build`** (AC: 1, 2)
  - [x] Restructure `SiteNav.Build` ([SiteNav.cs:71-165](src/SpecScribe/SiteNav.cs)) to emit the journey-organized groups from the Design Direction table. Every child is added **only when its existing availability signal is true** (reuse the exact gates: `hasReadme`, module-doc filename match, `hasAdrs`, epics presence, `hasSprint`, and the new `hasGitInsights`/`hasDeepAnalytics`/action-items/deferred signals).
  - [x] Add `hasGitInsights`, `hasDeepAnalytics` parameters (default `false`). Add signals for the Follow-ups children — **decide the seam**: the cleanest is to pass `hasActionItems` (⇐ `_sprint?.OpenActionItems.Count > 0`) and `hasDeferredWork` (⇐ `WorkInventory.Build(...).Deferred is not null`) into `Build`, computed by the generator before the nav build. Note `WorkInventory`/action-items are currently computed **late** ([SiteGenerator.cs:236-237](src/SpecScribe/SiteGenerator.cs)); you will need the deferred/action-item presence signal **before** nav build — either compute a lightweight presence check early, or (preferred) compute the `WorkInventory` and open-action-item count up front and reuse the instance later (it is already built from `_docs`/`_sprint`, both available by line 100 after ingest). Do not double-build wastefully.
  - [x] Retire **Structure**: remove the `hasStructure` gate, the Structure `Items`/`QuickLinks` entries, and the `HasStructure` convenience. (See Task 5 for the page itself.)
  - [x] Implement the "single available child collapses to a flat top-level link" refinement (recommended) so shallow repos don't get one-item dropdowns.
  - [x] Preserve the existing dashboard **QuickLinks** superset behavior for the entries that remain (Readme, module docs, Epics, Requirements, Sprint, ADRs, Spec) **minus Structure**. QuickLinks stay flat (grid) — grouping is a top-nav concern this story owns; do not restructure the "Explore Key Views" grid.

- [x] **Task 3 — Render grouped markup in `RenderNavMarkup`** (AC: 1, 2)
  - [x] Update `HtmlRenderAdapter.RenderNavMarkup` ([HtmlRenderAdapter.cs:53-76](src/SpecScribe/HtmlRenderAdapter.cs)) to walk `nav.Groups`: flat top-level links (Home) render as today's `<a>`; each group renders a **non-anchor** disclosure header (`<summary>` inside `<details class="site-nav-group">`, header carries `Icons.ForConcept(group.ConceptKey)` + label) with its child `<a>` links inside. Preserve the active-page marking (`class="active" aria-current="page"`) on the matching leaf, and — recommended — mark the *containing group* open/current when one of its children is active so the active page is visible without expanding.
  - [x] Preserve `RenderNav` = `RenderNavMarkup` + `NavToggleScript` split exactly ([HtmlRenderAdapter.cs:46](src/SpecScribe/HtmlRenderAdapter.cs)); the webview keeps calling `RenderNavMarkup` only. The disclosure must need **no** new script (native `<details>`).
  - [x] Add a new concept-key glyph for each new group in [Icons.cs](src/SpecScribe/Icons.cs) (`ForConcept` — e.g. "Insights", "Follow-ups", "Delivery", "Project"). Fall back gracefully (the method already returns empty for unknown keys).

- [x] **Task 4 — Thread the git-page availability signal & correct the stale comments** (AC: 1, 2)
  - [x] In [SiteGenerator.cs](src/SpecScribe/SiteGenerator.cs), pass `hasGitInsights`/`hasDeepAnalytics` (derived from the `progress` local's `DeepGit`/`DeepGit.Insights`) to **both** `SiteNav.Build` calls (lines 100 and 111) and ensure the watch-mode `BuildNav`/`RegenerateEpics` path carries the same signal from `_progress`.
  - [x] Rewrite the stale "not in the top nav — nav is built before git is computed" doc comments on `GitInsightsOutputPath`/`DeepAnalyticsOutputPath` ([SiteNav.cs:17-26](src/SpecScribe/SiteNav.cs)) to state they now ride the Insights group, gated on the deep-git data signal available at nav-build time.
  - [x] Add the accepted-tradeoff comment (render-failure-after-nav-build) described in the Critical Timing Note.

- [x] **Task 5 — Complete the Structure retirement (the "pending revert")** (AC: 2)
  - [x] Remove the Structure nav item + quick-link (Task 2). This alone satisfies AC2's nav requirement.
  - [x] **Recommended (confirm at review):** finish the Story 3.4 "pending revert" — stop generating `structure.html`: remove the `WriteStructure` call ([SiteGenerator.cs:233](src/SpecScribe/SiteGenerator.cs)), `WriteStructure`/`RenderStructurePage`/`BuildStructureHrefMap`, `SiteNav.StructureOutputPath`, and the now-unused `ProjectTree` / `Charts.ProjectStructureTree` structure-tree code **only if** nothing else consumes them (grep first — the webview/outline work reused some tree concepts; verify `ProjectTree`/`Charts.ProjectStructureTree` have no other caller before deleting). The source-code treemap (Story 7.6) is a **new** surface, not this artifact tree, so the artifact tree has no future. If any shared helper is still referenced elsewhere, retire only the nav + page emission and leave the helper.
  - [x] Do **not** leave an orphaned `structure.html` with no inbound links — if you keep generating it, that is dead output; prefer full removal.

- [x] **Task 6 — CSS + host theming for the disclosure groups** (AC: 1)
  - [x] Style `.site-nav-group` / its `<summary>` / the child link list in [specscribe.css](src/SpecScribe/assets/specscribe.css) to match the existing nav pill language (Story 1.4/1.5 polish, focus rings, reduced-motion). Ensure the disclosure works within the ≤640px mobile collapse.
  - [x] Verify the new nav elements theme correctly under the webview's `.vscode-*` theme bridge (Story 6.5 — [see story-6-5-webview-theming-live]) so chrome maps to `--vscode-*` host variables. The theme bridge is a **separate inline `<style>`** and must not leak into the generated HTML surface (byte-parity guardrail). Do not add nav colors that the bridge can't retune.

- [x] **Task 7 — Update parity, golden, and nav tests** (AC: 1, 2)
  - [x] **`RenderParity`**: confirm the flat-leaf nav-fact path still holds (group headers are non-anchor `<summary>`, so `ExtractNav`'s `<a>`-anchor recovery returns exactly the flattened leaves in document order = `page.Nav.Items`). Add/extend tests in [RenderParityTests.cs](tests/SpecScribe.Tests/RenderParityTests.cs) for a grouped nav: leaves recovered in order, active leaf inside a group still marked, no group header mistaken for a nav fact. If any real divergence surfaces on webview/SPA, register a documented `HostRenderException` — but the shared `RenderNavMarkup` should mean **none is needed** (all three surfaces emit identical grouped markup).
  - [x] **`SiteNavTests`** ([SiteNavTests.cs](tests/SpecScribe.Tests/SiteNavTests.cs)): the existing assertions compare `nav.Items.Select(i => i.Label)` against flat arrays — update them to the new flattened order, and add group-structure assertions (`nav.Groups`), Insights-group-present-only-with-deep-git, Follow-ups-group gating, Structure-absent, empty-group-omitted, and single-child-collapse cases.
  - [x] **`SiteGeneratorStructureTests`** ([SiteGeneratorStructureTests.cs](tests/SpecScribe.Tests/SiteGeneratorStructureTests.cs)): this whole file asserts the *presence* of `structure.html` + the Structure nav/quick-link. Rework it to assert the **retired** behavior (no Structure nav item, no quick-link, and — if you remove the page — no `structure.html`). Keep the `AssertNoBrokenLocalLinks` helper pattern; it is exactly the NFR8 guard you want on the new nav.
  - [x] Add generation-level tests (temp-dir fixture style, mirroring `SiteGeneratorStructureTests`/`SiteGeneratorGitInsightsTests`) proving: with `--deep-git` producing insights, `index.html` (and an interior page) carries an Insights group linking `git-insights.html` + `deep-analytics.html`; with `--deep-git` off, no Insights group and no dead links; with open action items / deferred work, a Follow-ups group links `action-items.html` + `deferred-work.html`; with none, no Follow-ups group.
  - [x] **Golden byte regen**: the nav markup changes on **every page**, so every committed golden/byte-parity fingerprint that pins nav bytes must be regenerated (see [golden-diff-normalization-gotchas]). Regenerate intentionally, and eyeball the diff to confirm it is *only* the nav restructure (plus Structure removal), not an unintended body change. Run the webview + SPA parity suites ([SiteGeneratorWebviewTests.cs](tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs), [SiteGeneratorSpaTests.cs](tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs), [RenderSpaParityTests.cs](tests/SpecScribe.Tests/RenderSpaParityTests.cs)) — they must stay green (the shared `RenderNavMarkup` is why they should).

- [x] **Task 8 — Verify end-to-end on the real repo** (AC: 1, 2)
  - [x] `dotnet run` a full generate on this repo **without** `--deep-git`: nav shows Home + Delivery + (Follow-ups if action items/deferred exist) + Project; **no** Insights group; no dead links; no Structure.
  - [x] Full generate **with** `--deep-git`: Insights group appears with Git Insights + Deep Analytics; open both from an interior page (not Home). Confirm `--spa` and the webview both render the grouped nav (open the SPA in the preview browser).
  - [x] Confirm keyboard/AT: `<summary>` group headers are focusable and toggle on Enter/Space; the active page's group is discoverable.

## Dev Notes

### Architecture patterns & constraints (must follow)

- **One render seam, three surfaces.** `HtmlRenderAdapter.RenderNavMarkup` is the single nav renderer; HTML adds a mobile toggle script (`RenderNav`), webview reuses the markup under a nonce-locked CSP (no non-nonce'd inline JS), SPA re-renders it per content swap. Any interactivity you add to groups must be pure CSS / native HTML — **no information-bearing JS** (rendering-architecture.md §Client-Side Enhancement Policy). This is why native `<details>` is the recommended disclosure.
- **Data-gated nav is the existing idiom.** Every nav item already appears only when its source signal is present (`hasReadme`, `hasSprint`, `hasAdrs`, epics presence, module-doc filename match). Groups extend this: a child appears on its existing signal; a group appears only if ≥1 child is available. This IS NFR8 ("nav entries render only when the corresponding data exists").
- **Flattened `Items` is the compatibility contract.** Do not remove `NavigationView.Items`. Keep it as the flattened leaf list so `RenderParity`, the SPA manifest, and dashboard active-item logic keep working unchanged. `Groups` is additive structure the renderer walks.
- **Golden byte-identity is a gate.** Nav bytes on every page change → committed fingerprints regenerate. Regenerate deliberately and diff to prove the change is nav-only (plus Structure removal). See [golden-diff-normalization-gotchas] for the footer-clock / `?v=` / subtitle normalizations the diff harness applies.
- **Never-throw / graceful omission (NFR2).** A missing signal omits its child/group; it never errors. Match the existing `try/catch → omit` posture (e.g. `WriteStructure`'s degrade-to-omit).

### Source tree — files to touch

- `src/SpecScribe/NavigationView.cs` — add `NavGroup`; add `Groups` to `NavigationView` (UPDATE).
- `src/SpecScribe/SiteNav.cs` — restructure `Build` into groups, add git/follow-up gates, retire Structure, update `ToNavigationView` + stale comments (UPDATE — the heart of the story).
- `src/SpecScribe/HtmlRenderAdapter.cs` — grouped `RenderNavMarkup` (UPDATE).
- `src/SpecScribe/SiteGenerator.cs` — pass new gates to both `SiteNav.Build` calls + watch path; compute action-item/deferred presence early; remove `WriteStructure` (+ helpers) per Task 5 (UPDATE).
- `src/SpecScribe/Icons.cs` — new group concept glyphs (UPDATE).
- `src/SpecScribe/assets/specscribe.css` — `.site-nav-group` disclosure styling + reduced-motion/focus (UPDATE).
- `src/SpecScribe/WebviewHelpers.cs` / theming (Story 6.5) — verify `.vscode-*` bridge covers new nav elements (VERIFY, likely no change if you reuse existing nav variables).
- Tests: `SiteNavTests.cs`, `RenderParityTests.cs`, `SiteGeneratorStructureTests.cs` (rework), new grouped-nav generation tests; regenerate golden fingerprints across the `SiteGenerator*`/webview/SPA suites (UPDATE).

### UPDATE files — current state & what must be preserved

- **`SiteNav.Build`** ([SiteNav.cs:71](src/SpecScribe/SiteNav.cs)): today returns `Items` (flat top nav) + `QuickLinks` (dashboard superset). Preserve: Readme-right-after-Home ordering, module docs `InNav` vs quick-link-only split, ADR/Sprint/epics gating, the Spec-kernel quick-link, and the `IsUnderSpecs` disjointness rule. Reorganize `Items` into `Groups`; **keep `QuickLinks` behavior** minus Structure.
- **`RenderNavMarkup`** ([HtmlRenderAdapter.cs:53](src/SpecScribe/HtmlRenderAdapter.cs)): today emits `<nav class="site-nav"><div class="site-nav-inner"><span class="site-nav-brand">…<button class="site-nav-toggle">…<div class="site-nav-links">[<a>…]`. Preserve the outer shell (brand, toggle, `site-nav-links` container, sticky bar) exactly — `RenderParity`'s `NavRegion`/`NavLinks` regexes and the SPA content-region slicer key off `site-nav`/`site-nav-links`. Only the *contents* of `site-nav-links` gain group structure.
- **`SiteGenerator.GenerateAll`** ([SiteGenerator.cs:64](src/SpecScribe/SiteGenerator.cs)): preserve the ingest → nav → README → epics → pages → git → sprint/structure/index ordering. Your only insertions: derive the new gates from the `progress` local before line 100; compute action-item/deferred presence early (reuse the `WorkInventory`/open-action-item instance later rather than rebuilding — it's currently built at lines 236-237 and 342-346); drop the `WriteStructure` call.

### Testing standards

- xUnit; temp-dir fixture pattern (`Directory.CreateTempSubdirectory`, `IDisposable`) as in `SiteGeneratorStructureTests`/`SiteGeneratorSprintTests`/`SiteGeneratorGitInsightsTests`.
- Assert both **presence** (group appears with the right children in the right order when data exists) and **absence** (no empty group, no dead link when data is missing) — the NFR8 pair.
- Reuse the `AssertNoBrokenLocalLinks` pattern from `SiteGeneratorStructureTests` against `index.html` **and** an interior page for the deep-git-on case.
- The `RenderParity.FindDivergences(page, html, "html")` call must return empty for a grouped nav (no new `HostRenderException` for the `nav`/`nav.active` facts). If it doesn't, your group headers leaked into the anchor scope — fix the markup, don't add an exception.

### Out of scope (do not build)

- No new landing/index pages (no `insights.html`, no `follow-ups.html`) — groups are disclosure headers, not pages.
- No restructuring of the dashboard "Explore Key Views" quick-link grid into groups (nav-only story).
- No new chart legends/metadata (that is Story 10.2), no glossary (10.3), no date-format unification (10.4).
- No source-code treemap (Story 7.6) — it is a future, separate surface; this story only removes the retired artifact-tree Structure page.
- No breadcrumb changes.

### Project Structure Notes

- Output dir is `SpecScribeOutput` (never `docs/live`) — see [generate-output-dir-is-specscribeoutput].
- If working in a git worktree, target the worktree path (main has a background auto-committer) — see [worktree-edits-must-target-worktree-path].

### References

- [Source: docs/UserJourneys.md] — Journeys 1–7, personas; J6/J7 explicitly demand top-nav reach for insight + follow-up pages.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 10: Portal Legibility for Every Audience] — Story 10.1 ACs; FR27–29, UX-DR25/27/28/29/30, NFR8.
- [Source: _bmad-output/specs/spec-specscribe/rendering-architecture.md#Client-Side Enhancement Policy] — no-info-bearing-JS rule; feature-parity rules (core-first, adapters map only).
- [Source: src/SpecScribe/SiteNav.cs] — `Build`, gating idiom, stale git-page comments to correct.
- [Source: src/SpecScribe/SiteGenerator.cs#GenerateAll] — nav-build-after-ingest ordering; `ComputeProgress` runs before nav.
- [Source: src/SpecScribe/HtmlRenderAdapter.cs#RenderNavMarkup] — single shared nav render seam.
- [Source: src/SpecScribe/RenderParity.cs] — flat-anchor nav-fact recovery; keep group headers non-anchor.
- [Source: src/SpecScribe/HostRenderException.cs] — sanctioned-divergence registry (expect no new entry).

## Dev Agent Record

### Agent Model Used

Composer (Cursor agent)

### Debug Log References

- Full suite: 1556 passed (0 failed)
- E2E: generate without --deep-git → Delivery/Follow-ups/Project, no Insights, no structure.html
- E2E: generate with --deep-git → Insights group on epics.html with git-insights + deep-analytics + code-map

### Completion Notes List

- Introduced `NavGroup` + `NavigationView.Groups`; `Items` remains the flattened leaf projection for RenderParity/SPA.
- `SiteNav.Build` emits Home / Delivery / Insights / Follow-ups / Project; single-child groups collapse flat; Structure already retired (Story 7.6 Code Map).
- Insights gated on deep-git data at nav-build time; Follow-ups on open action items + deferred-work.md presence (`FindDeferredWorkOutputPath`).
- `RenderNavMarkup` walks Groups with native `<details class="site-nav-group">` / `<summary>` (no JS); active group opens.
- Spec kernels now ride the Project group (and stay quick-links); Code Map sits under Insights.
- CSS + webview theme bridge cover `.site-nav-group-summary`; golden fingerprint regenerated.
- New `SiteGeneratorGroupedNavTests`; Structure retirement already done by 7.6 (asserted absent).

### File List

- src/SpecScribe/NavigationView.cs
- src/SpecScribe/SiteNav.cs
- src/SpecScribe/HtmlRenderAdapter.cs
- src/SpecScribe/HtmlRenderAdapter.Dashboard.cs
- src/SpecScribe/SiteGenerator.cs
- src/SpecScribe/Icons.cs
- src/SpecScribe/RenderParity.cs
- src/SpecScribe/assets/specscribe.css
- src/SpecScribe/assets/specscribe-webview-theme.css
- tests/SpecScribe.Tests/SiteNavTests.cs
- tests/SpecScribe.Tests/RenderParityTests.cs
- tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs
- tests/SpecScribe.Tests/SiteGeneratorGroupedNavTests.cs
- tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs
- tests/SpecScribe.Tests/FollowUpSurfacesTests.cs
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/10-1-insights-navigation-and-structure-page-retirement.md

### Review Findings

*Code review 2026-07-19 (3-layer: Blind Hunter, Edge Case Hunter, Acceptance Auditor).*

**Decisions (resolved same session — owner chose "keep as-is" for all three):**

- [x] [Review][Decision] Insights group includes Code Map, not just Git Insights/Deep Analytics [SiteNav.cs:214] — owner confirmed: keep as-is, Code Map belongs in Insights.
- [x] [Review][Decision] Deferred Work nav entry gated on file-existence, not `WorkInventory.Deferred` content [SiteGenerator.cs:183, SiteNav.cs:335] — owner confirmed: keep the file-existence gate (page still renders when empty, not a dead link).
- [x] [Review][Decision] Quick-link-only module docs (Product Brief, UX Design, UX Experience) dropped from dark-bar dropdown [HtmlRenderAdapter.cs:118] — owner confirmed: keep new behavior, it correctly matches the documented `InNav` contract.

**Patch findings (all applied 2026-07-19, 1720 tests green, golden fingerprint regenerated):**

- [x] [Review][Patch] Project group child ordering: ADRs render before Spec kernels; taxonomy table specifies Spec before ADRs [SiteNav.cs:180,254] — moved the ADRs `project.Add` to after the Spec-kernel loop.
- [x] [Review][Patch] Follow-ups quick links (Action Items, Deferred Work) never added to `quickLinks`, so `KeyViewGroupOrder`'s "Follow-ups" entry is permanently dead — the white key-views band can never show a Follow-ups group [SiteNav.cs Build; HtmlRenderAdapter.cs:174] — added `quickLinks.Add(...)` for both.
- [x] [Review][Patch] `NavigationView.Groups` should be `required` like `Items` — currently defaults to empty, so a construction site that forgets to set it silently renders zero top-nav links while `Items`-driven RenderParity still passes [NavigationView.cs:253] — made `Groups` `required`; only production construction site (`SiteNav.ToNavigationView`) already set it.
- [x] [Review][Patch] `FindDeferredWorkOutputPath` silently drops a duplicate `deferred-work.md` with no diagnostic, unlike the sibling module-doc duplicate handling in the same file [SiteNav.cs:335] — added a `Skipped` `AdapterDiagnostic`, threaded `diagnostics` through both call sites.
- [x] [Review][Patch] Follow-ups has no distinct CSS family tint — `QuickLinkFamily` maps it to `family-epics` (Delivery's color), so the newest journey group is visually indistinguishable from Delivery [HtmlRenderAdapter.Dashboard.cs:433; specscribe.css] — added `family-followups` (`--rust-light`) for the dark-bar summary, white-band pill, and key-view-group.
- [x] [Review][Patch] Accepted-tradeoff XML remarks on `SiteNav.Build` only describe git-insights/deep-analytics gating; the identical data-signal-before-render risk for Action Items/Deferred Work isn't mentioned [SiteNav.cs:382] — extended the `<remarks>` block.
- [x] [Review][Patch] Dead `"Docs"` icon-key branch remains in `Icons.ForConcept` after the taxonomy fully migrated "Docs" → "Project" everywhere else [Icons.cs:214] — removed the `"Docs"` arm; updated `IconsTests.cs`'s three stale `"Docs"` references to `"Project"`.

**Deferred findings:**

- [x] [Review][Defer] Two independently hand-maintained label→group classifiers (`SiteNav.Build`'s inline grouping vs. `HtmlRenderAdapter.KeyViewGroup`) can drift as new nav labels are added [SiteNav.cs; HtmlRenderAdapter.cs:311] — deferred, architectural observation not a concrete bug today
- [x] [Review][Defer] `RenderParityTests` doesn't exercise all four nav groups populated simultaneously in one page [RenderParityTests.cs] — deferred, test-coverage gap not a defect
- [x] [Review][Defer] `.list-batch-actions` CSS layout change (3-col grid, stacked command groups) is unrelated scope bundled into this story's commit [specscribe.css:740] — deferred, flagged for hygiene, not broken

## Change Log

- 2026-07-18: Implemented journey-organized hierarchical nav (Delivery/Insights/Follow-ups/Project) with native details disclosure; threaded deep-git + follow-up gates; regenerated golden fingerprint; status → review.
- 2026-07-19: Code review (3-layer adversarial). 3 decisions resolved (all kept as-is), 7 patch findings logged, 3 deferred to deferred-work.md, 7 dismissed as false-positive/by-design/verified-non-issue.
