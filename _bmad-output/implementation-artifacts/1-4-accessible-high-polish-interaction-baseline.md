---
baseline_commit: 53432af8b8410d16eacd7dda01025098153d6067
---

# Story 1.4: Accessible High-Polish Interaction Baseline

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user scanning project status,
I want interactive dashboard components that are both striking and accessible,
so that I can quickly understand progress regardless of input method.

## Acceptance Criteria

1. **Given** the dashboard contains interactive cards and charts
   **When** I use keyboard navigation
   **Then** all interactive elements are focusable with visible focus states
   **And** drill and hover alternatives are available without pointer-only interaction.

2. **Given** motion preferences vary by user
   **When** reduced-motion preference is enabled
   **Then** non-essential animation is minimized
   **And** information remains clear without relying on animation.

> **Scope:** This is the **accessibility + motion baseline** only. Dashboard visual polish, on-brand
> tooltips, status-color tokenization, chart truthfulness, IA reordering, and head/meta polish split out
> into **Story 1.5** (Dashboard Insight Polish and Visual Truthfulness). Representing new work types
> (deferred work, quick-dev) and inline authoring guidance split out into **Story 2.1**. A handful of items
> from the UX review (`docs/Story1_4_UX_Observations.md`) that are strictly accessibility — whole-chart
> `aria-label`s (E6/H3), `.index-card-path` contrast (H1), and chart micro-text sizing (H2) — stay here;
> the rest are traced into 1.5/2.1. Keep this story tight to the two ACs so an accessibility regression and
> a visual-polish regression are never entangled in one change.

## Tasks / Subtasks

- [ ] Task 1: Establish a consistent visible focus-state system for every interactive element (AC: #1)
  - [ ] Add a single shared `:focus-visible` treatment (on-brand: `outline: 2px solid var(--teal); outline-offset: 2px;` per the UX index-card focus spec) covering the interactive surfaces that today have only `:hover` styling: `.index-card`, `.now-next-card`, `.quick-link-card`, `.epic-mosaic-card`, `.site-nav a`, `.toc-strip a` (or 1.3's `.toc-sidebar a`), `.breadcrumb a`, `.ac-anchor`, `.ac-ref`, and the `.view-epic-link` CTAs
  - [ ] Add a focus-visible indicator for the SVG segment links driven off the segment path (e.g. a `stroke`/`stroke-width` bump in `--gold-light`), since default outlines are unreliable on SVG across browsers
  - [ ] Use `:focus-visible` (not bare `:focus`) so a mouse click leaves no lingering outline; verify no rule sets `outline: none` without a visible replacement (only `.site-nav-toggle:focus-visible` does today, swapping in border+color — preserve that pattern)
  - [ ] Verify Tab order is logical on the dashboard and that every clickable card and chart segment is reachable and visibly focused
- [ ] Task 2: Give charts accessible names so hover-only info is reachable without a pointer (AC: #1)
  - [ ] `Charts.Sunburst` / `Charts.EpicSunburst`: each segment is an `<a href>` wrapping a `<path>` whose descriptive text is a hover-only `<title>`. Add an `aria-label` to the `<a>` carrying the same "Epic N: Title — status, N stories" / "Story N.N: Title — status" / task done/remaining text, so keyboard/screen-reader users get name+status+count on focus. Keep the `<title>` too (pointer tooltip) [Source: `src/SpecScribe/Charts.cs:121-154`, `:232-247`]
  - [ ] `[UXO E6/H3]` Add whole-chart `role="img"` + `aria-label` to the donut and heatmap SVGs, which lack them today (only the sunburst has them). Examples: donut → "Epic status: N drafted, M pending"; heatmap → "Commit activity: N commits across M active days, {first}–{last}" [Source: `src/SpecScribe/Charts.cs:43` (Donut), `:409` (CommitHeatmap)]
  - [ ] `Charts.TaskSunburst` segments are tooltip-only `<path>`s (no page to drill to). Do NOT make them focusable; the story page already renders the task checklist as real text — that is the non-pointer equivalent. Keep the chart's `role="img"`+`aria-label` and note the equivalence
  - [ ] Preserve swatch+text legends and text-bearing status pills — status is conveyed by shape/label, not color alone; do not regress (UX-DR17). (Unifying the *colors* across charts is Story 1.5's job — do not tackle it here; just keep the text redundancy intact.)
- [ ] Task 3: Reinforce the keyboard/screen-reader accessibility floor (AC: #1; UX-DR16)
  - [ ] Add a skip-to-content link as the first focusable element on every page (`<a class="skip-link" href="#main-content">Skip to content</a>`, visually hidden until focused) via the shared shell (`PathUtil.RenderHeadOpen` / start of `<body>`), with matching `.skip-link` CSS
  - [ ] Wrap each page's primary content in a single `<main id="main-content">` landmark: dashboard (`HtmlTemplater.RenderIndex`), generic doc pages (`HtmlTemplater.RenderPage`), epic/story detail (`EpicsTemplater`). `RequirementsTemplater` already emits `<main class="req-index">` — add `id="main-content"`. Ensure exactly one `<main>` per page and that it coexists with 1.3's TOC-sidebar layout
  - [ ] Add `role="progressbar"` + `aria-valuenow`/`aria-valuemin="0"`/`aria-valuemax="100"` + an `aria-label` matching the visible label to `Charts.ProgressBar`; keep the visible `progress-value` fraction text [Source: `src/SpecScribe/Charts.cs:17-31`]
  - [ ] Verify (do not rebuild) the existing nav `aria-current="page"` and the mobile drawer's Escape/focus handling are not regressed [Source: `src/SpecScribe/SiteNav.cs:96-116`]
- [ ] Task 4: Add reduced-motion compliance (AC: #2)
  - [ ] Add a single `@media (prefers-reduced-motion: reduce)` block to `specscribe.css` neutralizing non-essential motion: the card-lift `transform`/`transition` on `.index-card`/`.now-next-card`/`.quick-link-card`/`.epic-mosaic-card`/`.stat-card`; the `.sb-seg` opacity transition; the `.progress-fill` width transition; and any smooth-scroll 1.3 adds. Follow the EXPERIENCE.md Accessibility-Floor pattern (durations collapsed to ~0.01ms) [Source: `src/SpecScribe/assets/specscribe.css:360-375`, `:724-725`, `:1049`, `:1095`]
  - [ ] Confirm no information is motion-dependent: status is color+text+shape, progress is filled width + `progress-value` fraction text — all readable with transitions off. Document this so the "information remains clear without animation" half of AC #2 is demonstrably met
  - [ ] Note: Story 1.5 adds `@media (prefers-reduced-motion: no-preference)` entrance animations that pair with this block — leave a clear seam (this block is the "reduce" half) so 1.5 can add the complementary half without rework
- [ ] Task 5: Accessibility contrast and chart micro-text fixes (AC: #1) `[UXO H1, H2]`
  - [ ] `[UXO H1]` Fix `.index-card-path` contrast: `#d4b896` on `#faf7f2` is ≈1.6:1 (WCAG needs 4.5:1). Darken to at least `--ink-light` (`#7a6250`) [Source: `src/SpecScribe/assets/specscribe.css` `.index-card-path`]
  - [ ] `[UXO H2]` Bump the smallest chart text for legibility: `.sunburst-hint` to `0.78rem` non-italic; heatmap 8px day/month labels (a minimal bump — the full heatmap rescale is Story 1.5's E1, so keep this to a readability nudge, not a layout change) [Source: `src/SpecScribe/assets/specscribe.css:734`]
- [ ] Task 6: Regression and new test coverage (AC: #1, #2)
  - [ ] Extend `HtmlTemplaterTests` (rendered-HTML assertions): skip link + `<main id="main-content">` present on index/dashboard; progress bars carry `role="progressbar"` with `aria-valuenow`
  - [ ] Add `Charts`-level tests: sunburst/epic-sunburst segment `<a>` carry a non-empty `aria-label`; donut/heatmap SVGs carry `role="img"`+`aria-label`; legend text labels remain
  - [ ] Add a stylesheet-content assertion that a `@media (prefers-reduced-motion: reduce)` block and a `:focus-visible` outline rule exist (cheap guards against silent removal)
  - [ ] Prefer generation-/render-level assertions over new public API (mirrors Story 1.2/1.3)
- [ ] Task 7: End-to-end validation with a real generation pass (AC: #1, #2)
  - [ ] Run the focused test filter, then a real generation pass
  - [ ] Manually verify: Tab reaches every card and chart segment with a visible focus ring; a focused sunburst segment announces its name/status via `aria-label`; the skip link appears on first Tab and jumps to `#main-content`; enabling OS reduce-motion removes card-lift/opacity/width transitions while all status and progress information stays legible; no console errors; no broken anchors

## Developer Context Section

### Epic Context and Business Value

Epic 1 delivers a polished, immediately-useful portal for current BMad projects. Story 1.1 built navigation and dashboard wayfinding; 1.2 layered in traceability links; 1.3 hardened markdown fidelity (Mermaid, checklists, AC deep-links, TOC sidebar). Story 1.4 is the **accessibility + motion gate**: the dashboard's interactive surfaces (sunburst, donuts, status/now-next/quick-link/mosaic cards, progress bars) must be fully usable by keyboard and screen-reader users and must respect motion preferences, without losing their visual punch.

Realizes the epic's accessibility floor — **UX-DR16** (skip link, landmarks, progressbar ARIA), **UX-DR17** (status never color-only), **UX-DR18/UX-DR8** (reduced motion), and the keyboard halves of **UX-DR4/UX-DR7/UX-DR10** — plus **NFR6** (accessibility semantics are contractual behavior, not optional styling). The dashboard's *polish and truthfulness* (tooltips, unified color, chart correctness, IA) is Story 1.5; representing *new work types and authoring guidance* is Story 2.1.

### Story Foundation Extract

- Primary concern: keyboard-focusability with **visible** focus states on every interactive card/chart, non-pointer access to hover-only chart info, and reduced-motion compliance.
- User outcome: a keyboard-only, screen-reader, or reduced-motion user can reach and understand every interactive dashboard element.
- Success boundary: interaction happens with the existing static-HTML + pure-SVG-links architecture — no new client-side drill engine and (for this story) no new JavaScript at all.
- Regression boundary: nav/breadcrumb/drawer (1.1), traceability links (1.2), and markdown/TOC (1.3) are preserved; visual design (colors, pointer-user hover polish) is preserved.

### Current Implementation Reality (READ THIS FIRST)

The single most important fact: **the dashboard's interactivity is pure SVG + real `<a>` links with no JavaScript drill engine.** This deliberately diverges from the elaborate client-side drill/hover/URL-hash sunburst in `DESIGN.md`/`EXPERIENCE.md` (UX-DR5/6/7). **Do not build that JS drill engine.** The links substrate is already largely keyboard-accessible (anchors focus and activate on Enter) and is the correct base for AC #1; this story hardens it.

Confirmed at baseline commit `53432af`:

- **Sunburst/epic-sunburst are link maps.** Each segment is `<a href="…"><path class="sb-seg …"><title>…</title></path></a>`; Enter/click navigates to the epic/story page (that IS the "drill," via navigation). The wrapping `<a>` has **no `aria-label`**, and the `<title>` shows on mouse hover only → the AC #1 hover-alternative gap. [Source: `src/SpecScribe/Charts.cs:92-182`, `:204-273`]
- **Donut and heatmap SVGs lack `role="img"`/`aria-label`** (only the sunburst has them). [Source: `src/SpecScribe/Charts.cs:43`, `:409`]
- **Cards are links with hover polish but no `:focus-visible`.** `.index-card`/`.now-next-card`/`.quick-link-card`/`.epic-mosaic-card` have `:hover` lift/shadow but no focus rule (default outline only); the only `:focus-visible` rule is on `.site-nav-toggle` (border+color swap). Stat cards are **non-interactive `<div>`s** — read-outs, not Tab stops, so AC #1 does not require them to be focusable. [Source: `src/SpecScribe/Charts.cs:11-15`, `src/SpecScribe/assets/specscribe.css:78-83`, `:360-375`]
- **No `@media (prefers-reduced-motion)` anywhere** (hard AC #2 gap). Active transitions: card lift transform (`:360-375`,`:1095`), `.sb-seg` opacity (`:724-725`), `.progress-fill` width (`:1049`). Progress bars set width inline (no from-0 load animation today), so AC #2 is mostly "add the media query." [Source: grep confirms zero matches]
- **No skip link; `<main>` inconsistent** (only `RequirementsTemplater`, without `id`). `RenderHeadOpen` opens `<body>` straight into nav. [Source: `src/SpecScribe/PathUtil.cs:26-36`, `src/SpecScribe/HtmlTemplater.cs:9-57`]
- **`Charts.ProgressBar` has no `role="progressbar"`/ARIA.** [Source: `src/SpecScribe/Charts.cs:17-31`]
- **`.index-card-path` fails contrast** (`#d4b896` on `#faf7f2` ≈1.6:1). [UXO H1]
- **Already correct (verify, don't rebuild):** nav `aria-current`/drawer Escape+focus; sunburst `role="img"`+`aria-label`; swatch+text legends; text-bearing status pills. [Source: `src/SpecScribe/SiteNav.cs:96-116`, `src/SpecScribe/Charts.cs:111`, `:169-178`]

### Scope Boundaries

- **OUT OF SCOPE — the JS client-side drill/zoom/URL-hash sunburst engine** (UX-DR5/6/7). Links-based navigation stays. This story adds **no** JavaScript (the sanctioned tooltip/copy script is Story 1.5's).
- **OUT OF SCOPE — dark mode / theme toggle** (UX-DR2/3). No dark CSS or toggle exists; not in any Epic 1 AC.
- **DEFERRED TO STORY 1.5:** on-brand tooltips (UXO C1/C2/C4/D5), status-color tokenization + vocabulary (B1–B5), interaction grammar + sunburst sibling-dim + Now emphasis + entrance animations (D1–D4/D2), chart truthfulness (A2/A3/A4/A5/E1/E2/E3), IA reorder + copy buttons + recency (F1–F3), favicon/meta (G1/G2). Do not pull these in — 1.5 owns them and several depend on the status-token system 1.5 introduces.
- **DEFERRED TO STORY 1.6:** representing deferred-work + quick-dev artifacts, sunburst task-representation accuracy (unplanned-story placeholder arc UXO E4, delivery-status mosaic UXO A6), and inline authoring/empty-state guidance.
- **IN SCOPE:** visible focus states, chart accessible names (segment + whole-chart), reduced-motion, skip link + `<main>` + progressbar ARIA, and the strictly-accessibility contrast/text fixes (UXO H1/H2).

### Previous Story Intelligence

- Follow 1.2/1.3's **verify-and-harden** rhythm: confirm what already works (links are focusable, legends have text, nav has aria-current) and change production code only where a concrete gap exists (focus-visible CSS, segment/whole-chart aria-labels, reduced-motion block, skip link/landmark/progressbar ARIA, contrast).
- Keep behavior in the central seams (`Charts`, `HtmlTemplater`, `PathUtil`, `SiteNav`, `specscribe.css`); add one shared focus-visible rule and one shared reduced-motion block; add the skip link once in the shared shell.
- Prefer generation-/render-level string assertions over new public API (`HtmlTemplaterTests` is the model).
- Generated output publishes to GitHub Pages — keep everything static-host-safe; `#main-content` is an in-page anchor and is safe.
- **Story 1.3 is `in-progress` on the same `specscribe.css`/`HtmlTemplater`/`EpicsTemplater`** (TOC sidebar). Sequence after 1.3 or rebase onto its final state; fold any smooth-scroll it adds into Task 4's reduced-motion block; make the new `<main>` landmark coexist with 1.3's two-column TOC layout.
- **Story 1.5 follows this story on the same files** — leave clean seams (the reduced-motion "reduce" block, the focus-visible system, the segment aria-labels) that 1.5 extends rather than rewrites.
- Environment: BMAD helper Python scripts run under `py -3` on this Windows host.

### Architecture Compliance

- Accessibility semantics are **contractual, not optional styling**, and must live in the shared rendering core so the future VS Code webview inherits them. [Source: `epics.md` NFR6; `ARCHITECTURE-SPINE.md` AD-1/AD-2; `docs/adrs/0004-…md`]
- Keep interaction/semantic meaning host-neutral (aria-labels, reduced-motion intent live in the core output, not host-only code). [Source: `docs/adrs/0002-…md`]
- Graceful degradation is contractual: additions must not break zero/low-data states (empty sunburst, no git, no ADRs) — every `Charts` builder degrades to `.chart-empty`; preserve that. [Source: `src/SpecScribe/Charts.cs:95`,`:206`,`:281`,`:345`,`:382`]
- Charts are pure inline SVG + CSS, no JS. This story keeps them JS-free; do not introduce a client-side dependency to satisfy these ACs. [Source: `src/SpecScribe/Charts.cs:6-8`]

## Technical Requirements

- Every interactive element (card links, chart-segment links) must have a **visible** `:focus-visible` indicator, on-brand (teal/gold, ≥2px), perceivable on light cards and dark nav; SVG segment focus driven off the path.
- Sunburst/epic-sunburst segment `<a>`s carry `aria-label` (name+status+count); donut & heatmap SVGs carry `role="img"`+`aria-label`; `<title>`s retained for pointer users.
- One `@media (prefers-reduced-motion: reduce)` block neutralizes all non-essential motion without hiding information or breaking layout; no information is motion-dependent.
- A skip link is first-focusable → single `<main id="main-content">` landmark per page type.
- `Charts.ProgressBar` output carries `role="progressbar"` with `aria-valuenow`/`aria-valuemin`/`aria-valuemax` and an `aria-label`.
- `.index-card-path` contrast ≥4.5:1; `.sunburst-hint`/heatmap micro-text nudged for legibility.
- No new JavaScript; no dark-mode/theme system; changes stay host-neutral and static-host-safe.

## File Structure Requirements

Primary UPDATE candidates (touch only as needed):

- `src/SpecScribe/assets/specscribe.css` — shared `:focus-visible` outline system; SVG-segment focus indicator; `.skip-link` styles; `@media (prefers-reduced-motion: reduce)` block; `.index-card-path` contrast; `.sunburst-hint`/heatmap-label sizing. Preserve hover polish, `.site-nav-toggle:focus-visible` pattern, token/color system, 1.3 TOC layout.
- `src/SpecScribe/Charts.cs` — `aria-label` on sunburst/epic-sunburst segment `<a>`s; `role="img"`+`aria-label` on donut/heatmap SVGs; `role="progressbar"`/`aria-value*`/`aria-label` on `ProgressBar`. Keep pure-SVG design, `<title>`s, empty-state degradation, geometry, hrefs. (Do **not** change chart colors or data here — that's Story 1.5.)
- `src/SpecScribe/PathUtil.cs` (`RenderHeadOpen`) — emit the skip link at `<body>` start. Preserve head/meta/title/stylesheet output.
- `src/SpecScribe/HtmlTemplater.cs` — `<main id="main-content">` around index/dashboard and generic doc-page content. Preserve TOC, header/breadcrumb order, dashboard composition, `HasMermaid` injection.
- `src/SpecScribe/EpicsTemplater.cs` — `<main id="main-content">` around detail content (coexist with 1.3 TOC). Preserve AC anchors, kicker/pill, sunburst wiring, ordering.
- `src/SpecScribe/RequirementsTemplater.cs` — add `id="main-content"` to existing `<main>`; ensure one `<main>` per page.

Primary TEST candidates:

- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — skip link + `<main id>` on index/dashboard; progressbar ARIA on rendered bars.
- `tests/SpecScribe.Tests/ChartsTests.cs` (new or extend) — segment `aria-label`s; donut/heatmap `role="img"`+`aria-label`; legend text labels present.
- A stylesheet-content assertion — `@media (prefers-reduced-motion: reduce)` + a `:focus-visible` rule exist.

## Library and Framework Requirements

- Stay on the existing .NET / inline-SVG / CSS stack. **No new dependencies and no new JavaScript** in this story. Focus and reduced-motion are pure CSS (`:focus-visible`, `@media (prefers-reduced-motion: reduce)`); ARIA is static attributes emitted by `Charts`/templaters.
- Do not add dark mode / theming (out of scope).

## Testing Requirements

- Preserve existing coverage (`HtmlTemplaterTests`, `SiteNavTests`, `SiteGeneratorTraceabilityTests`).
- Add focused coverage (see Task 6): skip link + `<main id="main-content">`; `role="progressbar"`+`aria-valuenow`; segment & whole-chart `aria-label`s; `@media (prefers-reduced-motion: reduce)` + `:focus-visible` present in the stylesheet.
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~Charts|FullyQualifiedName~SiteNav|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Manual verification (no automated a11y harness in-repo): keyboard focus rings on every interactive element; focused sunburst segment announces name/status; skip link works; reduced-motion strips card-lift/opacity/width transitions while status/progress text stays legible; no console errors; no broken anchors.

## UX and Accessibility Requirements

- Keyboard: every interactive element Tab-reachable in logical DOM order with a visible focus state; chart "drill" via real link activation (Enter); chart info available on focus via `aria-label`. [Source: `EXPERIENCE.md` Accessibility Floor; `epics.md` UX-DR7/10; AC #1]
- Skip link + landmarks + progressbar ARIA + whole-chart names are the floor this story cements. [Source: `epics.md` UX-DR16; UXO E6/H3]
- Status never color-only — legends/pills pair color with text/shape; preserve the redundancy (color *unification* is 1.5). [Source: `epics.md` UX-DR17; `DESIGN.md`]
- Motion respects `prefers-reduced-motion`, no looping, and never removes information; leave a seam for 1.5's `no-preference` entrance animations. [Source: `epics.md` UX-DR18/8; `EXPERIENCE.md`]
- Use the established token/color system; the new focus ring reads on-brand (teal/gold), not a foreign default. [Source: `DESIGN.md`]

## Reinvention and Regression Guardrails

- Do NOT build a JS client-side drill/zoom/hash sunburst — links-based navigation satisfies AC #1 (biggest disaster risk).
- Do NOT add any JavaScript in this story (the tooltip/copy script belongs to Story 1.5).
- Do NOT add dark mode / theme toggle / `prefers-color-scheme` (out of scope).
- Do NOT change chart colors, data, ordering, or add tooltips/animations here — those are Story 1.5; keeping them out prevents entangling an a11y regression with a visual-polish regression.
- Do NOT strip `<title>`s when adding `aria-label`; keep both pointer + non-pointer paths.
- Do NOT set `outline: none` without a visible replacement; use `:focus-visible`.
- Do NOT make non-interactive stat-card `<div>`s focusable (empty Tab stops).
- Preserve zero/low-data graceful degradation and Story 1.1 missing-section nav omission.
- Do not regress 1.1 nav/breadcrumb/drawer, 1.2 traceability links, or 1.3 markdown/TOC while touching shared CSS/templaters; coordinate with 1.3's in-flight work.
- Keep everything host-neutral and static-host-safe (GitHub Pages).

## Git Intelligence Summary

- `53432af`/`27106f3` are Story 1.3 in-flight commits (this story's baseline) — expect uncommitted 1.3 CSS/templater edits; rebase/coordinate.
- `fb9fb88` (Story 1.2) is the verify-and-harden pattern to emulate; `3b0227e` hardened the color-swatch rewriter (a shared post-render pass — CSS/rendering are shared seams; change additively and centrally).
- Generated output is published to GitHub Pages; keep anchors/skip-target static-host-safe.

## Latest Technical Information

- `:focus-visible` and `@media (prefers-reduced-motion: reduce)` are broadly supported in current evergreen browsers; no polyfill or vendor-prefix work needed. SVG focus-ring rendering is inconsistent, so express segment focus on the `<path>` (stroke), not the `<a>` default outline. No external version considerations apply (charts are dependency-free; the Mermaid CDN is unrelated to this story).

## Project Context Reference

- UX review (drives 1.5/2.1, and the H1/H2 items kept here): `docs/Story1_4_UX_Observations.md`
- PRD: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`
- Epics: `_bmad-output/planning-artifacts/epics.md` (Story 1.4; UX-DR4/7/8/10/16/17/18; NFR6)
- Previous story: `_bmad-output/implementation-artifacts/1-3-markdown-fidelity-for-core-artifact-patterns.md`
- Next stories on the same files: `1-5-dashboard-insight-polish-and-visual-truthfulness` (polish), `2-1-accurate-work-representation-and-authoring-guidance` (Epic 2)
- Architecture spine: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`; rendering: `_bmad-output/specs/spec-specscribe/rendering-architecture.md`
- ADR 0002 / ADR 0004: `docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md`, `docs/adrs/0004-cross-surface-interaction-and-theme-contract.md`
- UX design/behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`, `EXPERIENCE.md`
- Key source seams: `src/SpecScribe/Charts.cs`, `HtmlTemplater.cs`, `EpicsTemplater.cs`, `RequirementsTemplater.cs`, `PathUtil.cs`, `SiteNav.cs`, `assets/specscribe.css`

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed - comprehensive developer guide created (narrowed to the accessibility + motion baseline after the 1.4 UX review was split into Story 1.5 and Epic 2's Story 2.1).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8

### Debug Log References

- create-story workflow run for story `1-4-accessible-high-polish-interaction-baseline`
- scope narrowed to accessibility + motion after splitting the UX-review polish work into Story 1.5 and the work-representation/authoring work into Story 2.1
- environment: use `py -3` for BMAD helper scripts on this Windows host
- planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~Charts|FullyQualifiedName~SiteNav|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Verify-and-harden the pure-SVG-links + static-HTML substrate for the two ACs: shared focus-visible system + SVG-segment focus; segment & whole-chart aria-labels; skip link + single `<main id="main-content">` + progressbar ARIA; one reduced-motion block; H1 contrast + H2 micro-text.
- Add no JavaScript and no chart color/data/ordering changes (those are Story 1.5); leave clean seams for 1.5 to extend.
- Add render-/Charts-/stylesheet-level tests; re-run targeted tests + a real generation pass. Coordinate shared-file edits with in-flight Story 1.3.

### Completion Notes List

- Context assembled from epics.md (Story 1.4 ACs + UX-DR4/7/8/10/16/17/18 + NFR6), PRD, UX DESIGN/EXPERIENCE, architecture spine, ADRs 0002/0004, Story 1.2/1.3 intelligence, current code seams, git history, and the Story 1.4 UX review.
- Core finding: pure-SVG `<a>`-link charts, no JS — AC #1 is satisfiable by hardening this substrate (focus + aria-labels), not by building the UX-spec drill engine.
- Hard AC #2 gap: no `@media (prefers-reduced-motion)` exists anywhere.
- Scope split: this story is a11y + motion; Story 1.5 owns tooltips/tokens/truthfulness/IA/meta; Story 2.1 owns deferred/quick-dev representation + authoring guidance + sunburst task accuracy (E4/A6). Only strictly-accessibility UX items (E6/H3 whole-chart aria-labels, H1 contrast, H2 micro-text) remain here.
- Coordination flag: Story 1.3 is `in-progress` on the same CSS/templaters — sequence after or rebase; fold its smooth-scroll into the reduced-motion block; make `<main>` coexist with its TOC sidebar. Leave seams for Story 1.5.

### File List

- _bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-07-06: Created Story 1.4 implementation context — accessible high-polish interaction baseline. Documented the pure-SVG-links (no-JS) charting reality vs. the UX drill spec, the AC #1 gaps (no focus-visible; segment info hover-only), the AC #2 gap (no reduced-motion query), and the UX-DR16 floor.
- 2026-07-06: Briefly expanded to absorb the full Story 1.4 UX review, then split by product-owner direction: this story narrowed back to the accessibility + motion baseline; dashboard polish/truthfulness moved to new Story 1.5; work-type representation + authoring guidance moved to new Story 2.1. Retained only the strictly-accessibility UX-review items here (whole-chart aria-labels E6/H3, index-card-path contrast H1, chart micro-text H2). Added explicit scope boundaries and seam notes so Story 1.5 can extend the focus-visible/reduced-motion/aria-label work without rework.
