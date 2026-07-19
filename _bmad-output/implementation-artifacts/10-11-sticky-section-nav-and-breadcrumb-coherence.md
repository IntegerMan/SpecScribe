---
baseline_commit: e929766
---

# Story 10.11: Sticky Section Nav & Breadcrumb Coherence

Status: ready-for-dev

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

- [ ] **Task 1 — Active-section tracking on the sticky TOC** (AC: 1, 2)
  - [ ] Per Decision A: add the minimal `IntersectionObserver` enhancement (or the confirmed-with-reviewer fallback) that toggles `.is-current` on the `.toc-link` matching the section currently in view, targeting the same heading/section elements `Toc.ExtractHeadings`/the existing `scroll-margin-top` selectors already anchor.
  - [ ] New CSS for `.toc-link.is-current` (or equivalent) — visually distinct from `:hover`/`:focus-visible`, riding `--motion-fast` for any transition, neutralized under `prefers-reduced-motion: reduce`.
  - [ ] Confirm the script degrades cleanly (no `.is-current` class, static list) when JS is off or the webview CSP blocks it — never breaks the link itself.

- [ ] **Task 2 — Unify breadcrumb + pager** (AC: 1)
  - [ ] Implement Decision B's recommended hoist (`PageView.Pager`, rendered by `HtmlRenderAdapter.Render` alongside `RenderBreadcrumb`) — or the confirmed lighter CSS-only fallback if the hoist proves out of scope. Note which was taken in Completion Notes.
  - [ ] Update every current `pager?.Render()` call site accordingly: [CodeFileTemplater.cs:703](src/SpecScribe/CodeFileTemplater.cs), [HtmlTemplater.cs:38](src/SpecScribe/HtmlTemplater.cs), [CommitDayTemplater.cs:50](src/SpecScribe/CommitDayTemplater.cs), [CommitDetailTemplater.cs:55](src/SpecScribe/CommitDetailTemplater.cs), [RetroTemplater.cs:73](src/SpecScribe/RetroTemplater.cs), [HtmlRenderAdapter.Epics.cs:172,499,611](src/SpecScribe/HtmlRenderAdapter.Epics.cs).
  - [ ] Retire the now-unneeded `position: relative` doc-header anchor hack ([specscribe.css:426](src/SpecScribe/assets/specscribe.css)) if the hoist path is taken (the pager no longer floats inside the header).
  - [ ] One shared CSS treatment for the combined breadcrumb+pager strip — reuse `.breadcrumb`'s existing visual language (color tokens, spacing) rather than inventing new ones; keep `.entity-pager`'s own link styling (teal/rust hover) intact.

- [ ] **Task 3 — a11y + reduced-motion pass** (AC: 2)
  - [ ] Confirm keyboard traversal (Tab order) through breadcrumb → pager → TOC (or whatever the new DOM order is) reads sensibly, and no element becomes unreachable.
  - [ ] Confirm `.is-current` and `:focus-visible` are visually distinguishable when both apply to the same link.
  - [ ] Confirm the reduced-motion block neutralizes every new transition this story adds.

- [ ] **Task 4 — Guardrails** (AC: 1, 2)
  - [ ] No new authoring schema.
  - [ ] No per-visitor persisted state anywhere (FR31) — grep the diff for `localStorage`/`sessionStorage`/cookie writes before calling this done.
  - [ ] `RenderParity` green, no new `HostRenderException`.
  - [ ] JS-off floor is byte-for-byte the same *link* behavior as today (only ambient highlighting is new).

- [ ] **Task 5 — Tests + golden** (AC: 1, 2)
  - [ ] Unit tests for the new `PageView.Pager` field (if the hoist path is taken) — breadcrumb+pager render together, absent pager degrades to breadcrumb-only (byte-identical to today's no-pager pages).
  - [ ] Confirm existing `EntityPager`/`Toc`/`BreadcrumbTrail` tests still pass unchanged (their own view-model contracts don't change, only where/how they're composed).
  - [ ] Golden fingerprint regen for every page kind that carries a pager and/or TOC — confirm stability across ≥2 runs before locking the constant ([golden-diff-normalization-gotchas]).
  - [ ] `dotnet test` from repo root, full suite green.

- [ ] **Task 6 — Verify end-to-end on the real repo** (AC: 1, 2)
  - [ ] `dotnet run --project src/SpecScribe -- generate --deep-git` against this repo; open a long doc page (e.g. an epic or ADR with several sections), scroll and confirm the TOC highlights the current section.
  - [ ] Open a page with both a breadcrumb and a pager (e.g. a code file, a commit page) and confirm they read as one coherent strip, not two unrelated controls.
  - [ ] Tab through the new layout with a keyboard; toggle OS-level reduced motion and confirm no lurching; disable JS and confirm every link still works with only the ambient highlight missing.
  - [ ] Confirm `--spa` and the webview render coherently (open the SPA/webview in the preview browser; confirm the CSP-safe fallback if Decision A's script is nonce-gated there).

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

### Completion Notes List

Ultimate context engine analysis completed — comprehensive developer guide created. This story closes the one real gap Story 10.5 left open (active-section tracking on the already-sticky TOC, via a minimal progressive-enhancement script that never gates content) and unifies the breadcrumb and `EntityPager` prev/next — today rendered in two unrelated visual registers — into one coherent wayfinding treatment, recommended via the same `PageView`-level hoist Story 6.1 already used for breadcrumb. Two explicit owner-latitude decisions (the tracking mechanism's scripting approach, and how deep the breadcrumb+pager unification goes) are flagged for confirmation at review, each with a recommended default and a lower-risk fallback.

### File List
