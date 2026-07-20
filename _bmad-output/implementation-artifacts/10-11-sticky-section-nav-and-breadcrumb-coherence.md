---
baseline_commit: e929766
---

# Story 10.11: Sticky Section Nav & Breadcrumb Coherence

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader on a long interior page,
I want sticky in-page section navigation plus consistent breadcrumb and prev/next controls,
so that orientation and traversal feel the same everywhere instead of improvised per page.

## Acceptance Criteria

1.
**Given** a long page (extending Story 10.5's grouped TOC)
**When** it renders
**Then** a sticky section nav tracks the current section, and breadcrumb plus the existing `EntityPager` prev/next are unified into one coherent wayfinding treatment across page types.

2.
**Given** keyboard and reduced-motion users
**When** they use section or breadcrumb navigation
**Then** focus and scroll behavior honor the existing a11y and reduced-motion conventions
**And** there is no per-visitor state (FR31 determinism).

## Context & Why This Story Exists

Epic 10 has already built two of this story's three ingredients — this story's job is to close the remaining gap (active-section tracking) and *coherently join* pieces that today render correctly but separately:

1. **The sticky TOC sidebar already exists** ([Toc.cs](src/SpecScribe/Toc.cs), Story 10.5). `.page-rail` is already `position: sticky` ([specscribe.css:811-821](src/SpecScribe/assets/specscribe.css)) and `Toc.RenderSidebar` already groups h2/h3 into collapsible `<details>` groups. **What's missing:** it never highlights *which* section the reader has scrolled to — clicking a TOC link jumps correctly, but the rail gives no ambient "you are here" feedback while scrolling. That is this story's core net-new surface (AC1 "tracks the current section").
2. **`EntityPager` prev/next already exists** ([EntityPager.cs](src/SpecScribe/EntityPager.cs)) and is wired on 8+ page kinds (epics, stories, code files, ADRs, commits, retros, generic docs). Today each templater renders it *independently*, inline near the top of `<header class="doc-header">`, absolutely positioned so it floats top-right without affecting the centered title flow (see `pager?.Render()` call sites in [CodeFileTemplater.cs:703](src/SpecScribe/CodeFileTemplater.cs), [HtmlTemplater.cs:38](src/SpecScribe/HtmlTemplater.cs), [CommitDayTemplater.cs:50](src/SpecScribe/CommitDayTemplater.cs), [CommitDetailTemplater.cs:55](src/SpecScribe/CommitDetailTemplater.cs), [RetroTemplater.cs:73](src/SpecScribe/RetroTemplater.cs), [HtmlRenderAdapter.Epics.cs:172,499,611](src/SpecScribe/HtmlRenderAdapter.Epics.cs)).
3. **Breadcrumb already exists as a `PageView`-level concept**, not a per-templater one: `PageView.Breadcrumb` ([PageView.cs](src/SpecScribe/PageView.cs)) is rendered ONCE, centrally, by `HtmlRenderAdapter.Render` ([HtmlRenderAdapter.cs:32](src/SpecScribe/HtmlRenderAdapter.cs)) — a chrome-level strip that runs nav → breadcrumb → body. This was Story 6.1's hoist: breadcrumb used to be scattered per-templater string-building and got promoted to one typed field rendered by one adapter method.

**The gap AC1 names ("unified into one coherent wayfinding treatment") is exactly the asymmetry between #2 and #3**: breadcrumb was hoisted to a single chrome-level render call; the pager never was. Today a reader sees the breadcrumb trail in one visual register (a full-width strip above the header) and the prev/next control in a completely different one (floated inside the body's own header, positioned and styled independently per templater) — two controls answering the same "where am I / where can I go" question, presented as unrelated features. This story's job is to bring the pager into the same treatment the breadcrumb already has, and to make the sticky TOC an active participant (not just a static jump-list) in that same "orientation" story.

Serves FR27–29 / UX-DR25,27–30 (Epic 10's onboarding + legibility mission) and directly extends Story 10.5's `Toc` seam and the existing `BreadcrumbTrail`/`EntityPager` view models. Load-bearing constraints: **FR31** (no per-visitor persisted state — active-section highlighting must be derived live from scroll position, never stored/remembered across visits), the existing **reduced-motion contract** ([specscribe.css:5278](src/SpecScribe/assets/specscribe.css) `@media (prefers-reduced-motion: reduce)`), and **three-surface parity** (HTML/webview/SPA).

## Design Direction

**This is the #1 review checkpoint** — two independent decisions, each with a recommended default and a lighter fallback if the recommended path proves too invasive during implementation. Confirm the choice actually taken at review, mirroring how Stories 10.1/10.5/10.7/10.10 flagged their own owner-latitude calls.

### Decision A — How the sticky section nav "tracks the current section"

**Recommended: a small progressive-enhancement script**, consistent with the precedent this codebase already has (the nav's `NavToggleScript` inline script, and the existing "one small tooltip/copy script" — see [charting-is-pure-svg-no-js] memory). A minimal `IntersectionObserver` over the same heading/section elements `Toc.ExtractHeadings` already anchors (`scroll-margin-top: var(--nav-offset)` elements) toggles an `.is-current` class on the matching `.toc-link`. This is **purely a visual enhancement, never a gate**: every TOC link is a real `<a href="#...">` that works with JS off exactly as it does today (Story 10.5's behavior is the JS-off floor) — only the ambient highlighting is JS-dependent, which satisfies NFR8 ("progressive enhancement, never a gate on seeing the data", the same framing Story 10.9 uses for its sort/filter controls). No `localStorage`/cookie/session write anywhere — the active section is recomputed from live scroll position on every load, satisfying FR31 by construction (nothing persisted, nothing that would make a from-scratch regeneration diverge, since this is client-side runtime behavior, not generated-output content).

- Keep the script tiny and inline (mirrors `NavToggleScript`'s pattern of a small IIFE appended near its target markup) rather than a new bundled asset.
- Debounce/throttle is unnecessary at this scale (`IntersectionObserver` is already event-coalesced); don't add a scroll listener.
- The webview's strict CSP blocks non-nonce'd inline scripts (Story 6.4) — either nonce the script the way the webview bridge does, or degrade webview to the static (non-tracking) TOC, matching NFR8's "clean fallback" pattern. Confirm at review which; the static TOC (today's Story 10.5 behavior) is a legitimate, correct floor for webview if nonce-plumbing proves out of scope.
- **Fallback if scripting is ruled out entirely:** ship AC1's sticky positioning alone (already true today) and treat "tracks the current section" as satisfied by scroll-margin-corrected anchor jumps rather than ambient highlighting — but confirm this reading with the reviewer before taking it, since the AC's wording ("tracks the current section") most naturally reads as active-highlighting, not just sticky positioning that already existed pre-story.

### Decision B — Unifying breadcrumb + pager into "one coherent wayfinding treatment"

**Recommended: hoist `EntityPager` onto `PageView`** the same way Story 6.1 hoisted breadcrumb — add `PageView.Pager` (nullable `EntityPager`, default null) and have `HtmlRenderAdapter.Render` render breadcrumb and pager together as one wayfinding block (e.g. breadcrumb trail on one line, prev/next controls sharing the same strip or immediately adjacent, one CSS treatment) instead of the pager floating independently inside each body's `<header class="doc-header">`. This:
- Removes the scattered `pager?.Render()` calls from the 8 templater call sites listed above and their absolute-positioning CSS hack (`position: relative` on `.doc-header` solely to anchor the pager — [specscribe.css:426](src/SpecScribe/assets/specscribe.css)), replacing them with one `PageView.Pager = pager` assignment per call site (mechanical, matches how `Breadcrumb` is already assigned per `PageView` builder in `EpicsTemplater`/etc.).
- Only applies cleanly to the `PageView`-based render paths. Confirm which templaters actually route through `PageView`/`HtmlRenderAdapter.Render` today vs. the standalone-`StringBuilder`-via-`RenderNavBar`/`SiteNav.RenderBreadcrumb` family (10.10's Dev Notes named this split for the nav-bar seam; the same split likely applies here) — for any templater that isn't `PageView`-based, either migrate that one call site's pager rendering to sit adjacent to its own `SiteNav.RenderBreadcrumb` call (same visual outcome, no `PageView` migration needed) or leave it as today's inline placement and note the exception.
- **Lighter fallback if the `PageView.Pager` hoist is too invasive for this story's scope:** leave each pager's *code* location exactly as today (no `PageView` field, no call-site rewiring) and unify only the *visual* treatment — move the pager's CSS from "absolutely positioned top-right of `.doc-header`" to sit in the same row/strip as the breadcrumb via a shared CSS class family, so the two read as one control without moving where either is rendered from. This is lower-risk (no new `PageView` field, no adapter changes, no golden-diff churn beyond CSS) but achieves less of AC1's "coherent treatment" than the hoist. Pick whichever fits the story's time budget and note the decision in Completion Notes.

### Guardrails (do not violate)

- **No new authoring schema.** Every element here (`BreadcrumbTrail`, `EntityPager`, `Toc.Entry`) already exists; this story recomposes their *rendering*, not their data.
- **JS-off floor stays exactly today's behavior.** Every link (TOC, breadcrumb, pager) is a real anchor with a real `href` regardless of scripting; only ambient highlighting is script-dependent (NFR8).
- **No per-visitor persisted state** (FR31) — no `localStorage`, cookies, or session state for "current section" or "last visited."
- **Reduced motion:** any new transition (e.g. the `.is-current` highlight fade) must ride the existing `--motion-fast`/`--motion-ease` tokens and be neutralized by the existing `@media (prefers-reduced-motion: reduce)` block — do not hand-roll a new animation outside that contract ([motion-token-system]). Scroll jumps triggered by clicking a TOC/breadcrumb/pager link must remain instant under reduced motion (the existing `scroll-behavior: auto !important` override already covers this if no page introduces its own `scroll-behavior: smooth` — don't add one).
- **Keyboard operability:** the active-section highlight must not interfere with `:focus-visible` styling on TOC links (both states can coexist — active-by-scroll and focused-by-keyboard are visually distinguishable, neither hides the other).
- **`RenderParity` stays green, no new `HostRenderException`.** If the pager hoist changes what HTML the webview/SPA render relative to the breadcrumb, `RenderParity`'s nav-fact extraction must not start misreading pager links as nav items (same discipline Stories 10.1/10.10 already established for keeping non-nav anchors out of `site-nav-links`'s scope — the pager/breadcrumb strip is a *different* container, so this should already be a non-issue, but confirm).
- **Golden fingerprint changes on every page with a pager and/or TOC** (breadcrumb+pager markup moves; TOC gains an `.is-current`-capable class or `id` hooks) — regenerate deliberately, confirm stability across ≥2 runs before locking the constant ([golden-diff-normalization-gotchas]).

## Tasks / Subtasks

- [x] **Task 1 — Active-section tracking on the sticky TOC** (AC: 1, 2)
  - [x] Per Decision A: added the minimal `IntersectionObserver` enhancement (`Toc.ActiveSectionScript`) that toggles `.is-current` on the `.toc-link` matching the section currently in view, targeting the same `scroll-margin-top` heading/section elements the TOC already anchors (via `document.getElementById` on each `.toc-link`'s own `href`).
  - [x] New CSS for `.toc-link.is-current` — color+bold+left-accent-bar (never color-only), distinct from `:hover`'s underline and `:focus-visible`'s outline (both can coexist), transition rides `--motion-fast`/`--motion-ease` and is neutralized for free by the existing global `prefers-reduced-motion: reduce` block (no new reduced-motion rule needed).
  - [x] Confirmed the clean degrade: every TOC link is a real anchor regardless of scripting; the script no-ops silently if `.toc-sidebar`/`IntersectionObserver` is absent.

- [x] **Task 2 — Unify breadcrumb + pager** (AC: 1)
  - [x] Implemented Decision B's recommended hoist for the `PageView` family (`PageView.Pager`, rendered by a new `HtmlRenderAdapter.RenderWayfinding` alongside the breadcrumb) — AND extended the identical strip to the 5 non-`PageView` templaters via a new `SiteNav.RenderWayfinding` static delegate (both call the same `HtmlRenderAdapter.RenderWayfinding` core), so every pager-bearing page family gets the coherent strip, not just the `PageView` half. Noted in Completion Notes below.
  - [x] Updated every `pager?.Render()`/`view.Pager.Render()` call site: [CodeFileTemplater.cs](src/SpecScribe/CodeFileTemplater.cs), [HtmlTemplater.cs](src/SpecScribe/HtmlTemplater.cs), [CommitDayTemplater.cs](src/SpecScribe/CommitDayTemplater.cs), [CommitDetailTemplater.cs](src/SpecScribe/CommitDetailTemplater.cs), [RetroTemplater.cs](src/SpecScribe/RetroTemplater.cs), [HtmlRenderAdapter.Epics.cs](src/SpecScribe/HtmlRenderAdapter.Epics.cs) (all 3 sites: epic/story/placeholder bodies).
  - [x] Retired the `.doc-header { position: relative }` anchor hack and `.entity-pager`'s `position: absolute` — the pager is a normal flex item inside `.page-wayfinding` everywhere now.
  - [x] One shared `.page-wayfinding` CSS treatment (flex row, breadcrumb left/pager right) reusing `.breadcrumb`'s existing tokens; `.entity-pager`'s own teal/rust link styling untouched.

- [x] **Task 3 — a11y + reduced-motion pass** (AC: 2)
  - [x] Confirmed DOM/tab order reads sensibly: nav → breadcrumb → pager (one `.page-wayfinding` strip) → TOC → body content — no element became unreachable (verified live via the accessibility tree).
  - [x] Confirmed `.is-current` (color+weight+border) and `:focus-visible` (outline) are visually distinguishable and both apply cleanly to the same link.
  - [x] The reduced-motion block already neutralizes `.toc-link.is-current`'s transition for free (the existing global `*, *::before, *::after { transition-duration: 0.01ms !important }` rule under `@media (prefers-reduced-motion: reduce)` covers any new transition without a dedicated addition).

- [x] **Task 4 — Guardrails** (AC: 1, 2)
  - [x] No new authoring schema.
  - [x] Grepped the full diff for `localStorage`/`sessionStorage`/cookie writes — none found (only a doc-comment mentions the term to explain FR31 compliance).
  - [x] Full suite green (1720/1720) including `RenderParity`/webview/SPA parity suites; no new `HostRenderException`.
  - [x] JS-off floor unchanged — every TOC/breadcrumb/pager link is still a real `<a href>`; only `.is-current` requires JS.

- [x] **Task 5 — Tests + golden** (AC: 1, 2)
  - [x] Added unit tests for `PageView.Pager`/`HtmlRenderAdapter.RenderWayfinding`/the chrome-level active-section script (`HtmlRenderAdapterTests`) and for the webview's pager-carrying, still-script-free content region (`WebviewRenderAdapterTests`): absent-pager byte-identity to `RenderBreadcrumb` alone, present-pager coherent strip ordering, script placement after body/before footer, script omission when no TOC.
  - [x] Existing `EntityPager`/`Toc`/`BreadcrumbTrail` tests pass unchanged.
  - [x] Golden fingerprint regenerated and confirmed stable across 3 repeated runs before locking in.
  - [x] `dotnet test` from repo root: 1720 passed, 0 failed.

- [x] **Task 6 — Verify end-to-end on the real repo** (AC: 1, 2)
  - [x] Ran `dotnet run --project src/SpecScribe -- generate --deep-git` against this repo (624 pages); confirmed via direct HTML inspection that epic/story pages (`PageView` family) and ADR pages (non-`PageView` family) both render the `.page-wayfinding` strip with breadcrumb + pager together, and the TOC gains the `IntersectionObserver` script.
  - [x] Confirmed in the Browser pane: nav → breadcrumb → pager → TOC accessibility-tree order reads correctly; the pager's sibling name ("Epic 2: …") surfaces as the `title` tooltip; no console errors.
  - [x] Reduced-motion neutralization confirmed structurally (the existing global rule covers the new transition — see Task 3); JS-off floor confirmed by design (every link is real regardless of scripting).
  - [x] Confirmed via `SiteGeneratorWebviewTests`/`SiteGeneratorSpaTests` (both green) that the webview/SPA surfaces carry the pager+breadcrumb strip with zero `<script>` in the content region — the CSP-safe/`innerHTML`-safe degrade this story's design relies on. Live IntersectionObserver *firing* could not be directly observed through this session's Browser-pane preview tooling (a known flakiness in this environment, not specific to this change); the script's construction, DOM targeting, and non-execution-when-absent were verified directly against the real generated HTML instead.

## Dev Notes

### Architecture patterns & constraints (must follow)

- **Breadcrumb's Story 6.1 hoist is the precedent, not a novelty.** `PageView.Breadcrumb` + one central `RenderBreadcrumb` call in `HtmlRenderAdapter.Render` is exactly the shape to mirror for the pager if Decision B's recommended path is taken — this is "do what we already did once, to the next scattered concern," not new architecture.
- **`Toc`/`EntityPager`/`BreadcrumbTrail` view models are NOT the thing changing.** This story recomposes *where and how* they render, not their own record shapes — resist the temptation to add fields to them for something that's really a rendering concern (e.g. "current section" is DOM/CSS/JS state, not a new `Toc.Entry` property).
- **NFR8 is the fallback contract, not a new failure mode** (same framing 10.10 used): active-highlighting off = the sticky TOC readers already have today, not a new degraded state to design.
- **Golden byte-identity is a gate, expected to move** on every page with a pager (breadcrumb+pager markup changes) and/or TOC (new class hooks) — this is not a CSS-only story.

### Source tree — files likely touched

| File | Change |
|------|--------|
| `src/SpecScribe/PageView.cs` | Add `Pager` (nullable `EntityPager`) if Decision B's hoist is taken (**UPDATE**) |
| `src/SpecScribe/HtmlRenderAdapter.cs` | Render breadcrumb+pager together in `Render` (**UPDATE**) |
| `src/SpecScribe/EpicsTemplater.cs` / `HtmlRenderAdapter.Epics.cs` | Assign `PageView.Pager` instead of inline `view.Pager.Render()` (**UPDATE**) |
| `src/SpecScribe/CodeFileTemplater.cs`, `HtmlTemplater.cs`, `CommitDayTemplater.cs`, `CommitDetailTemplater.cs`, `RetroTemplater.cs` | Same, per call site — or, for any non-`PageView` templater, adjust CSS placement only (**UPDATE**) |
| `src/SpecScribe/Toc.cs` | Possible small hook (e.g. an `id`/`data-` attribute on TOC links) for the active-tracking script to target, if not already reachable via `AnchorId` (**UPDATE**, may be unnecessary) |
| `src/SpecScribe/assets/specscribe.css` | `.toc-link.is-current`, unified breadcrumb+pager strip styling, retire `.doc-header`'s pager-anchor `position: relative` if superseded (**UPDATE**) |
| New or existing inline script (mirroring `NavToggleScript`'s pattern) | The `IntersectionObserver` active-section enhancement (**NEW**, small) |
| `tests/SpecScribe.Tests/*` | Per-change assertions + golden regen (**UPDATE**) |

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Heading/section anchors for the TOC | `Toc.ExtractHeadings` / the existing `scroll-margin-top` selector list | [Toc.cs:118](src/SpecScribe/Toc.cs), [specscribe.css:869-874](src/SpecScribe/assets/specscribe.css) |
| Sticky positioning (already done) | `.page-rail { position: sticky; ... }` | [specscribe.css:811](src/SpecScribe/assets/specscribe.css) |
| Prev/next control | `EntityPager.Render()` | [EntityPager.cs](src/SpecScribe/EntityPager.cs) |
| Breadcrumb trail + central render | `PageView.Breadcrumb` → `HtmlRenderAdapter.RenderBreadcrumb` | [PageView.cs](src/SpecScribe/PageView.cs), [HtmlRenderAdapter.cs:32,246](src/SpecScribe/HtmlRenderAdapter.cs) |
| Motion tokens for any new transition | `--motion-fast`/`--motion-ease` + the paired reduced-motion block | [specscribe.css:72-76,5278](src/SpecScribe/assets/specscribe.css), [motion-token-system] |
| Inline-script precedent (small, targeted, no bundle) | `NavToggleScript` | [HtmlRenderAdapter.cs:240](src/SpecScribe/HtmlRenderAdapter.cs) |
| Webview CSP-safe scripting precedent | The webview bridge's nonce'd script pattern (Story 6.4/6.5) | `WebviewRenderAdapter.cs` |

### Guardrails & invariants

- **FR31**: no per-visitor persisted state — active section is live-computed, never stored.
- **NFR8**: JS-off / no-rich-context degrades to today's exact static behavior, never a broken or empty control.
- **No new authoring schema.**
- **Reduced motion**: every new transition rides `--motion-*` tokens and is neutralized by the existing `@media (prefers-reduced-motion: reduce)` block.
- **`RenderParity`**: pager/breadcrumb anchors must not be misread as `NavigationView.Items` nav facts.

### Previous story intelligence

- **From 10.5 (review):** `Toc.RenderSidebar`/`WrapWithSidebar` is the ONE seam every TOC-bearing page shares — this story extends that seam (adding active-tracking), it does not fork a second TOC implementation. The dedupe-by-`AnchorId` and stray-h3-degrades-to-plain-link rules already there are load-bearing; don't disturb them.
- **From 10.10 (ready-for-dev, not yet built; closely related but independent):** proves the "one render seam, three surfaces" argument for the *nav bar's* white band — a different band than this story's breadcrumb+pager strip, but the same discipline (extend the ONE seam that already reaches HTML/webview/SPA, don't fork per-surface). If 10.10 lands first, its `NavLocalContext` work and this story's pager hoist are independent additions to different parts of the page chrome and shouldn't conflict, but both touch `HtmlRenderAdapter`/`PageView` — rebase carefully if both are in flight.
- **From the prev/next sibling nav work (spec-entity-prev-next-navigation-done memory):** the pager's absolute-positioning-inside-`.doc-header` trick exists specifically so the pager doesn't shift the centered title/kicker flow — if Decision B's hoist moves the pager OUT of `.doc-header` entirely, that specific CSS concern goes away, but confirm no page's header layout regresses without it (some headers may rely on that flow assumption).
- **From golden-diff-normalization-gotchas:** confirm a regenerated fingerprint is stable across ≥2 runs before locking it in (known stale-first-hash trap).

### Testing standards

- xUnit; `Assert.Contains`/`DoesNotContain` on emitted HTML, matching existing `HtmlRenderAdapterTests`/`EntityPagerTests`/`TocTests` patterns.
- Cover both the presence case (pager+breadcrumb render together; TOC carries active-tracking hooks) and the absence case (no pager → breadcrumb-only, byte-identical to today's no-pager render).
- Full suite green including golden + `RenderParity` + SPA/webview parity suites.

### Project Structure Notes

- No new authoring schema, no `sprint-status.yaml`/epics.md shape changes.
- Output dir is `SpecScribeOutput` (never `docs/live`) — see [generate-output-dir-is-specscribeoutput].
- If working in a git worktree, target the worktree path (main has a background auto-committer) — see [worktree-edits-must-target-worktree-path].

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.11] — this story's ACs.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 10: Portal Legibility for Every Audience] — FR27–29, UX-DR25/27–30, NFR8; FR31 (determinism) at epics.md line 75.
- [Source: _bmad-output/implementation-artifacts/10-5-document-rendering-legibility.md] — the grouped-TOC seam this story extends.
- [Source: _bmad-output/implementation-artifacts/10-10-context-aware-navigation-bar.md] — sibling "one render seam, three surfaces" story; independent surface, coordinate if both in flight.
- [Source: src/SpecScribe/Toc.cs]
- [Source: src/SpecScribe/EntityPager.cs]
- [Source: src/SpecScribe/BreadcrumbTrail.cs]
- [Source: src/SpecScribe/PageView.cs]
- [Source: src/SpecScribe/HtmlRenderAdapter.cs]
- [Source: src/SpecScribe/assets/specscribe.css] (`.page-rail`/`.toc-sidebar`/`.entity-pager`/`.breadcrumb`/motion tokens/reduced-motion block)
- [Source: docs/UserJourneys.md] — journeys this story continues to serve (J5 onboarding, J6 health).

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

- Initial approach embedded `Toc.ActiveSectionScript` inside `Toc.WrapWithSidebar` (so it rode along with the TOC markup, self-locating via `document.currentScript.previousElementSibling` like `NavToggleScript`). This broke `SiteGeneratorWebviewTests.EverySurface_CarriesTheChromeAndNoScript` — that region is `PageView.BodyHtml` verbatim (not a `<main>`-only slice) for the webview/SPA-family pages, so anything inside `WrapWithSidebar`'s output reaches the webview content region regardless of its position relative to `</main>`. Traced `WebviewRenderAdapter.RenderContent`/`JsonSpaRenderAdapter.RenderContent`/`SiteGenerator.AddSpaSurface` to confirm they consume `page.BodyHtml` directly (unlike the "every other page" SPA path, which slices via `SpaDelivery.ExtractContentRegion` — a true string-index cut at `</main>` that DOES exclude trailing content). Fixed by moving the script to the SAME chrome-level seam `Mermaid.InitScript()` already uses in `HtmlRenderAdapter.Render` — appended after `page.BodyHtml`, never inside it — so `RenderContent`/`AddSpaSurface` (which never call `Render`) naturally never see it. The two non-`PageView` TOC templaters (`HtmlTemplater.RenderPage`, `RetroTemplater.RenderPage`) append it directly after their own `</main>`, which IS excluded by `ExtractContentRegion`'s literal-index cut.

### Completion Notes List

Closed the one real gap Story 10.5 left open (active-section tracking on the already-sticky TOC) and unified the breadcrumb + `EntityPager` prev/next — previously rendered in two unrelated visual registers — into one coherent wayfinding strip.

**Decision A (tracking mechanism):** took the recommended path — a minimal `IntersectionObserver` progressive-enhancement script (`Toc.ActiveSectionScript`) toggling `.toc-link.is-current`. No nonce-plumbing was needed for the webview's CSP: the script never becomes part of the webview/SPA-family content region at all (see Debug Log), so it degrades cleanly to today's static TOC on those two surfaces by construction, not by a webview-specific branch. No `localStorage`/cookie/session write anywhere (FR31 by construction — grepped the diff to confirm).

**Decision B (breadcrumb+pager unification):** took the recommended hoist for the `PageView` family (`PageView.Pager`, rendered by a new `HtmlRenderAdapter.RenderWayfinding` alongside the breadcrumb) — AND extended the SAME strip to the 5 non-`PageView` templaters via a new `SiteNav.RenderWayfinding` static delegate that calls the identical `HtmlRenderAdapter.RenderWayfinding` core, so every pager-bearing page family (not just the 3 `PageView` ones) gets the coherent strip. This went further than the story's "recommended path only covers the `PageView` half, CSS-only fallback for the rest" framing — the two paths turned out to share one trivial method (`RenderWayfinding` is pure string composition, no `PageView`-specific state), so unifying both halves cost nothing extra and gives a stronger AC1 outcome (literally one render path for the whole site, not two visually-matching-but-separately-coded ones). `RenderWayfinding` degrades to byte-identical `RenderBreadcrumb` output when there's no pager (verified by test), so the vast majority of pages are untouched. Retired `.doc-header`'s `position: relative` anchor hack and `.entity-pager`'s `position: absolute` — the pager is now a normal flex item everywhere.

Also updated `WebviewRenderAdapter.RenderContent` and `JsonSpaRenderAdapter.RenderContent` to call the new `RenderWayfinding` (was `RenderBreadcrumb`) so the webview/SPA surfaces get the SAME coherent strip as HTML — just never the tracking script, which structurally can't reach them.

Verified live against this repo's own history (`--deep-git generate`, 624 pages): confirmed via direct HTML inspection and the Browser pane's accessibility tree that epic/story pages (`PageView` family) and ADR pages (non-`PageView` family) both render the strip correctly, in the right DOM order (nav → breadcrumb+pager → TOC → body), with no console errors. Live `IntersectionObserver` firing could not be directly observed through this session's Browser-pane preview tooling (a pre-existing environment flakiness, not specific to this change — same issue noted in a prior story's session); verified the script's construction, DOM targeting, and clean non-execution-when-absent against the real generated HTML and the automated test suite instead.

Golden fingerprint regenerated and confirmed stable across 3 repeated runs. Full suite: 1720 tests green (8 new this story). A concurrent, unrelated session (Story 10.1 code-review patches) landed on `main` mid-session — the shared-main gotcha this repo's memory already documents; re-verified the golden hash and full suite against the settled combined state before finishing.

### File List

- `src/SpecScribe/PageView.cs` — added `Pager` (nullable `EntityPager`)
- `src/SpecScribe/HtmlRenderAdapter.cs` — new `RenderWayfinding` (breadcrumb+pager strip, byte-identical to `RenderBreadcrumb` alone when pager absent); `Render` now emits it plus the chrome-level active-section script
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` — removed the 3 inline `view.Pager.Render()` header calls (epic/story/story-placeholder bodies)
- `src/SpecScribe/EpicsTemplater.cs` — assign `PageView.Pager = pager` on the 3 `Build*Page` methods
- `src/SpecScribe/SiteNav.cs` — new static `RenderWayfinding` delegate for the non-`PageView` templater family
- `src/SpecScribe/HtmlTemplater.cs` — `RenderPage` uses `SiteNav.RenderWayfinding`; appends the active-section script after `</main>` when a TOC is present
- `src/SpecScribe/CodeFileTemplater.cs`, `src/SpecScribe/CommitDetailTemplater.cs`, `src/SpecScribe/CommitDayTemplater.cs` — same wayfinding-strip wiring (no TOC on these pages, no script)
- `src/SpecScribe/RetroTemplater.cs` — same wayfinding-strip wiring; appends the active-section script after `</main>` when a TOC is present
- `src/SpecScribe/Toc.cs` — new public `ActiveSectionScript` (`IntersectionObserver` active-section enhancement)
- `src/SpecScribe/WebviewRenderAdapter.cs`, `src/SpecScribe/JsonSpaRenderAdapter.cs` — `RenderContent` now calls `RenderWayfinding` instead of `RenderBreadcrumb`
- `src/SpecScribe/assets/specscribe.css` — new `.page-wayfinding`/`.toc-link.is-current`; retired `.doc-header`'s `position: relative` and `.entity-pager`'s `position: absolute`
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs` — 8 new tests (wayfinding byte-identity/composition, chrome-level script placement/omission)
- `tests/SpecScribe.Tests/WebviewRenderAdapterTests.cs` — 1 new test (pager in webview content region, still script-free)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden fingerprint constant regenerated

## Change Log

- 2026-07-19: Story implemented (dev-story). New `PageView.Pager` + `HtmlRenderAdapter.RenderWayfinding` unify the breadcrumb and `EntityPager` prev/next into one coherent strip across BOTH the `PageView` family (epic/story/placeholder, via the hoist) and the 5 non-`PageView` templaters (via a new `SiteNav.RenderWayfinding` delegate onto the same core) — broader than the story's "PageView-only, CSS-fallback for the rest" framing, since the render logic turned out to be shareable outright. New `Toc.ActiveSectionScript` (`IntersectionObserver`) toggles `.toc-link.is-current`, emitted at the SAME chrome-level seam as the Mermaid init script so it structurally never reaches the webview/SPA content regions (clean NFR8 degrade, no CSP nonce plumbing needed). Retired `.doc-header`'s `position: relative` pager-anchor hack. Golden fingerprint regenerated (stable across 3 runs). 1720 tests green (8 new).
