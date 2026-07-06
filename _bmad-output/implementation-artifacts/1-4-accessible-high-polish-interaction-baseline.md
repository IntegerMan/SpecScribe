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

## Tasks / Subtasks

- [ ] Task 1: Establish a consistent visible focus-state system for every interactive element (AC: #1)
  - [ ] Add a single shared `:focus-visible` treatment (on-brand: `outline: 2px solid var(--teal); outline-offset: 2px;` per the UX index-card focus spec) covering the interactive surfaces that today have only `:hover` styling: `.index-card`, `.now-next-card`, `.quick-link-card`, `.epic-mosaic-card`, `.site-nav a`, `.toc-strip a`, `.breadcrumb a`, `.ac-anchor`, `.ac-ref`, and the `view-epic-link` CTAs
  - [ ] Add a focus-visible outline for the SVG segment links (`.sunburst a:focus-visible .sb-seg`, `.donut a:focus-visible` where segments are wrapped in `<a>`) so a keyboard user can see which chart segment is focused — SVG focus rings are unreliable across browsers, so drive the indicator off the segment path (e.g. `stroke`/`stroke-width` bump in `--gold-light`, mirroring the DESIGN "drill ring" affordance) rather than relying on the default outline
  - [ ] Use `:focus-visible` (not bare `:focus`) so a mouse click does not leave a lingering outline; verify no existing rule sets `outline: none` without a visible replacement (only `.site-nav-toggle:focus-visible` does today, and it swaps in a border+color change — preserve that pattern)
  - [ ] Verify keyboard Tab order is logical on the dashboard (nav → skip target → stat/now-next/quick-link/sunburst segment/donut/mosaic links) and that every currently-clickable card and chart segment is reachable and visibly focused
- [ ] Task 2: Give interactive chart segments an accessible name so hover-only info is reachable without a pointer (AC: #1)
  - [ ] `Charts.Sunburst` and `Charts.EpicSunburst`: each segment is an `<a href>` wrapping a `<path>` whose only descriptive text is a `<title>` (shown on mouse hover only). Add an `aria-label` to the `<a>` element carrying the same "Epic N: Title — status, N stories" / "Story N.N: Title — status" / task done/remaining text, so a keyboard or screen-reader user gets the name+status+count on focus, not just on hover
  - [ ] Keep the `<title>` element too (native pointer tooltip) — the `aria-label` is the non-pointer equivalent, satisfying "hover alternatives available without pointer-only interaction"
  - [ ] `Charts.TaskSunburst` segments are tooltip-only `<path>`s (no link, no page to drill to). Do NOT force them to be focusable; instead confirm the story page already renders the task checklist as real text (the `.doc-body` GitHub-style checkboxes + the "Tasks / Subtasks" list) — that visible text list is the non-pointer equivalent for the task ring. Keep the chart's `role="img"` + whole-chart `aria-label`, and note this equivalence rather than adding redundant focus stops
  - [ ] Preserve the existing swatch+text legends (`.sunburst-legend`) and status pills — status is already conveyed by shape/label, not color alone; do not regress that redundancy (UX-DR17)
- [ ] Task 3: Add reduced-motion compliance (AC: #2)
  - [ ] Add a single `@media (prefers-reduced-motion: reduce)` block to `specscribe.css` that neutralizes non-essential motion: the card-lift `transform`/`transition` on `.index-card`, `.now-next-card`, `.quick-link-card`, `.epic-mosaic-card`, `.stat-card`; the `.sb-seg` opacity transition; the `.progress-fill` `width` transition; and any card `box-shadow`/`border-color` transitions. Use the pattern from EXPERIENCE.md's Accessibility Floor (`transition-duration`/`animation-duration` collapsed to ~0.01ms, or `transition: none`), but scope it so essential layout is untouched
  - [ ] If Story 1.3's in-progress TOC-sidebar work introduces `scroll-behavior: smooth` or any sticky-scroll animation, gate it under the same reduced-motion block (coordinate — 1.3 is `in-progress` on the same CSS file; rebase onto its final state and neutralize any smooth-scroll it adds)
  - [ ] Confirm no information is motion-dependent: status is conveyed by color+text+shape, progress by the filled width + the `progress-value` fraction text — all readable when transitions are disabled. Document this (a comment in the media block or a note in Completion Notes) so the "information remains clear without animation" half of AC #2 is demonstrably met, not just asserted
- [ ] Task 4: Reinforce the keyboard/screen-reader accessibility floor these ACs depend on (AC: #1; UX-DR16)
  - [ ] Add a skip-to-content link as the first focusable element on every page (`<a class="skip-link" href="#main-content">Skip to content</a>`, visually hidden until focused) via the shared shell (`PathUtil.RenderHeadOpen` or immediately after `<body>`), with matching `.skip-link` CSS
  - [ ] Wrap each page's primary content in a `<main id="main-content">` landmark so the skip link has a target and keyboard users land past the nav: the dashboard (`HtmlTemplater.RenderIndex` — the `.dashboard` section / page body), generic doc pages (`HtmlTemplater.RenderPage` — currently `<article class="doc-body">` is not inside a `<main>`), and epic/story detail pages (`EpicsTemplater`). `RequirementsTemplater` already emits `<main class="req-index">` — add `id="main-content"` there for consistency; ensure exactly one `<main>` per page
  - [ ] Add `role="progressbar"` + `aria-valuenow`/`aria-valuemin="0"`/`aria-valuemax="100"` + an `aria-label` matching the visible label to `Charts.ProgressBar`, so the progress bars have a screen-reader-readable value equivalent (UX-DR16); keep the visible `progress-value` fraction text
  - [ ] Confirm the nav already exposes `aria-current="page"` on the active link and the mobile drawer already handles Escape + focus-move (it does — `SiteNav.RenderNavBar`); do not rebuild that, just verify it is not regressed
- [ ] Task 5: Add regression coverage for the accessibility/motion behavior (AC: #1, #2)
  - [ ] Extend `HtmlTemplaterTests` (rendered-HTML string assertions, the established pattern) to assert: the skip link and `<main id="main-content">` are present on the index/dashboard; progress bars carry `role="progressbar"` with `aria-valuenow`; the dashboard is emitted with the interactive cards intact
  - [ ] Add focused chart tests (a new `ChartsTests` or alongside existing ones) asserting sunburst/epic-sunburst segment `<a>` elements carry an `aria-label` (accessible name), and that the legend text labels remain present (color-not-alone)
  - [ ] Add a CSS-content assertion (the stylesheet is an embedded asset — read it via the same mechanism the generator uses, or assert on generated output) that a `@media (prefers-reduced-motion: reduce)` block exists and that a focus-visible outline rule exists — a cheap guard against silent removal
  - [ ] Do not add public API surface just to reach private helpers; prefer generation-/render-level assertions (mirrors Story 1.2/1.3 guidance)
- [ ] Task 6: Validate end-to-end with a real generation pass (AC: #1, #2)
  - [ ] Run the focused test filter for the touched behavior
  - [ ] Run a real generation pass and manually verify: Tab reaches every card and chart segment with a visible focus indicator; focusing a sunburst segment announces its name/status (aria-label); enabling OS "reduce motion" removes card-lift/opacity/width transitions while all status and progress information stays legible; the skip link appears on first Tab and jumps to `#main-content`

## Developer Context Section

### Epic Context and Business Value

Epic 1 delivers a polished, immediately-useful portal for current BMad projects. Story 1.1 built navigation and dashboard wayfinding; Story 1.2 layered in traceability links; Story 1.3 hardened markdown fidelity (Mermaid, task checklists, AC deep-links, TOC sidebar). Story 1.4 is the **accessibility + interaction-polish gate** — and the final story of Epic 1 before the retrospective. The dashboard's hero interactive surfaces (the sunburst, the donuts, the status/now-next/quick-link/mosaic cards, the progress bars) must be fully usable by keyboard and screen-reader users and must respect motion preferences, without losing their visual punch.

This story realizes the epic's UX accessibility floor: **UX-DR16** (skip link, landmarks, progressbar ARIA), **UX-DR17** (status never color-only), **UX-DR18/UX-DR8** (reduced-motion compliance), and the keyboard-focusability halves of **UX-DR4/UX-DR7/UX-DR10** (focusable cards and chart segments with visible focus). It also satisfies the cross-surface accessibility contract **NFR6** ("keyboard drill behavior, labels, status text redundancy are contractual behavior, not optional styling").

### Story Foundation Extract

- Primary concern: keyboard-focusability with **visible** focus states on every interactive card/chart, non-pointer access to hover-only chart info, and reduced-motion compliance.
- User outcome: a keyboard-only or screen-reader user can reach and understand every interactive dashboard element; a reduced-motion user gets a calm, fully-informative page.
- Success boundary: interaction happens with the existing static-HTML + pure-SVG-links architecture — no new client-side drill engine required.
- Regression boundary: existing nav/breadcrumb/drawer behavior (1.1), traceability links (1.2), and markdown/TOC rendering (1.3) are preserved; the visual design (colors, card hover polish for pointer users) is preserved.

### Current Implementation Reality (READ THIS FIRST)

The single most important fact for this story: **the dashboard's interactivity is built as pure SVG + real `<a>` links with no JavaScript drill engine.** This deliberately diverges from the elaborate client-side drill/hover-tooltip/URL-hash sunburst described in `DESIGN.md`/`EXPERIENCE.md` (UX-DR5/UX-DR6/UX-DR7). **Do not build that JS drill engine.** The links-based approach is already largely keyboard-accessible (anchors are focusable and activate on Enter), and it is the correct, simpler substrate for satisfying AC #1. This story hardens that substrate; it does not replace it.

Confirmed current behavior at baseline commit `53432af`:

- **Sunburst / epic-sunburst are real link maps.** `Charts.Sunburst` / `Charts.EpicSunburst` emit each epic/story/task segment as `<a href="…"><path class="sb-seg …"><title>…</title></path></a>`. Clicking (or Enter on) a segment navigates to the epic/story page — that IS the "drill," via page navigation, and it is keyboard-operable today. [Source: `src/SpecScribe/Charts.cs:92-182`, `:204-273`]
- **The `<title>` on each segment is the only descriptive text, and it shows on mouse hover only.** The wrapping `<a>` has no `aria-label`, so a keyboard/screen-reader user focusing a segment gets a weak/absent accessible name. This is the concrete AC #1 "hover alternative without pointer-only" gap. [Source: `src/SpecScribe/Charts.cs:121-154`, `:232-247`]
- **`TaskSunburst` segments are `<path>` + `<title>` only (no links)** — there is no task page to drill to. The whole chart has `role="img"` + `aria-label`. The story page separately renders the task checklist as real text, which is the non-pointer equivalent. [Source: `src/SpecScribe/Charts.cs:279-338`]
- **Cards are links with hover polish but no explicit focus style.** `.index-card`, `.now-next-card`, `.quick-link-card`, `.epic-mosaic-card` are `<a>` elements with `:hover` lift/shadow rules but **no `:focus-visible` rule** — they fall back to the browser default outline, which is inconsistent and easy to lose against the parchment palette. Stat cards (`Charts.StatCard`) are **plain non-interactive `<div>`s** (no tooltip, no tabindex) — they are read-outs, not interactive, so AC #1 does not require them to be focusable. [Source: `src/SpecScribe/Charts.cs:11-15`, `src/SpecScribe/assets/specscribe.css:360-375`, `:445-455`, `:840-900`, `:1085-1102`]
- **The only `:focus-visible` rule today is on `.site-nav-toggle`** (hamburger), which swaps border+color and sets `outline: none` — an acceptable visible replacement to preserve as the pattern. [Source: `src/SpecScribe/assets/specscribe.css:78-83`]
- **There is NO `@media (prefers-reduced-motion: reduce)` block anywhere.** This is the hard gap for AC #2. Active transitions that must be neutralized: card lift `transform`/`transition` (`:360-375`, `:1095`), `.sb-seg { transition: opacity 0.12s }` (`:724-725`), `.progress-fill { transition: width 0.3s }` (`:1049`). [Source: `src/SpecScribe/assets/specscribe.css` — grep confirms zero `prefers-reduced-motion` matches]
- **Progress bars set their fill width inline** (`style="width:X%"`) with no from-zero load animation — so there is no JS `IntersectionObserver` animation to worry about; the width transition only fires on runtime width changes (watch mode). Reduced-motion still needs to neutralize it, but the "animate-in on load" from the design spec is not implemented and is not required here. `Charts.ProgressBar` has no `role="progressbar"`/ARIA today. [Source: `src/SpecScribe/Charts.cs:17-31`, `src/SpecScribe/assets/specscribe.css:1041-1052`]
- **No skip link; `<main>` landmark is inconsistent.** `PathUtil.RenderHeadOpen` emits `<body>` then straight into nav with no skip link. Only `RequirementsTemplater` wraps content in `<main class="req-index">` (no `id`); the dashboard (`HtmlTemplater.RenderIndex`), generic doc pages (`HtmlTemplater.RenderPage` → `<article class="doc-body">`), and epic/story pages (`EpicsTemplater`) have **no `<main>`**. [Source: `src/SpecScribe/PathUtil.cs:26-36`, `src/SpecScribe/HtmlTemplater.cs:9-57`, `src/SpecScribe/RequirementsTemplater.cs:36`]
- **Good news already in place (verify, don't rebuild):** nav exposes `aria-current="page"` on the active link and `aria-label` on the `<nav>`; the mobile drawer handles open/close, Escape-to-close, and moves focus to the first link on open; the sunburst SVGs carry `role="img"` + descriptive `aria-label`; legends pair a color swatch with a text label; status pills carry text (never color-only). [Source: `src/SpecScribe/SiteNav.cs:96-116`, `src/SpecScribe/Charts.cs:111`, `:169-178`]

### Scope Boundaries (prevent the two most likely disasters)

- **OUT OF SCOPE — do not build the JS client-side drill sunburst** (UX-DR5/6/7: animated zoom drill, custom hover tooltip popovers, `#epic-N-story-M` URL-hash state, breadcrumb-on-drill, scoped stat-row recompute). The links-based navigation is the accepted implementation and satisfies AC #1. Building the JS engine is a large net-new effort, out of this story's ACs, and risks regressing the working static output.
- **OUT OF SCOPE — dark mode / theme toggle** (UX-DR2/UX-DR3). Despite the DESIGN/EXPERIENCE specs describing a `data-theme` toggle and full dark palette, **no dark-mode CSS or theme-toggle button exists in the codebase**, and no Epic 1 story (including this one) has it in its ACs. Do not add `prefers-color-scheme`, a `data-theme` system, `localStorage` theming, or a toggle button here — that is a separate, larger feature. If encountered, note it as deferred, don't build it.
- **IN SCOPE:** visible focus states (AC #1), accessible names on chart segment links (AC #1), reduced-motion media query (AC #2), and the UX-DR16 keyboard/AT floor (skip link, `<main>` landmark, progressbar ARIA) that AC #1's keyboard-navigation requirement rests on.

### Previous Story Intelligence

Stories 1.2 and 1.3 set the working pattern to follow:

- Prefer **verify-and-harden** over greenfield: confirm what already works (links are focusable, legends have text, nav has aria-current) and change production code only where a concrete gap exists (focus-visible CSS, segment aria-labels, reduced-motion block, skip link/landmark/progressbar ARIA). [Source: `_bmad-output/implementation-artifacts/1-2-…md`, `1-3-…md`]
- Keep behavior in the existing central seams (`Charts`, `HtmlTemplater`, `PathUtil`, `SiteNav`, `specscribe.css`) rather than duplicating per page. Add one shared focus-visible rule and one shared reduced-motion block; add the skip link once in the shared shell.
- Prefer generation-/render-level string assertions over new public API (`HtmlTemplaterTests` is the model: build model → call templater → `Assert.Contains`).
- Generated output publishes to GitHub Pages — keep everything static-host-safe (no runtime JS dependency, relative-correct links/anchors). The skip-link target `#main-content` is an in-page anchor and is static-safe.
- **Story 1.3 is `in-progress` and edits the same `specscribe.css`** (and `HtmlTemplater`/`EpicsTemplater` for the TOC sidebar). Sequence this story after 1.3 lands, or rebase onto its final state; specifically fold any `scroll-behavior: smooth` it introduces into this story's reduced-motion block, and make the new `<main>` landmark coexist with 1.3's two-column TOC layout wrapper.
- Environment note carried from 1.3: BMAD helper Python scripts run under `py -3` on this Windows host (`python`/`python3` shims are not functional interpreters even though the resolver JSON prints).

### Architecture Compliance

- Accessibility semantics are **contractual behavior, not optional styling** — keyboard drill/focus, labels, and status-text redundancy must be first-class and must carry across surfaces. Implement them in the shared rendering core (Charts/templater/CSS), not as host-specific afterthoughts, so the future VS Code webview inherits them. [Source: `_bmad-output/planning-artifacts/epics.md` NFR6; `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1/AD-2]
- Host-neutral view models are the contract; keep interaction/semantic meaning (focus, aria-labels, reduced-motion intent) in the shared core so HTML and webview stay equivalent. [Source: `docs/adrs/0004-cross-surface-interaction-and-theme-contract.md`, `docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md`]
- Graceful degradation is contractual: the accessibility additions must not break rendering for zero/low-data states (empty sunburst, no git, no ADRs) — every `Charts` builder already degrades to a `.chart-empty` placeholder; preserve that. [Source: `src/SpecScribe/Charts.cs:95`, `:206`, `:281`, `:345`, `:382`]
- Charts are intentionally **pure inline SVG + CSS, no JS, no external deps** (the Mermaid CDN is the one sanctioned exception, and it is unrelated here). Do not introduce a client-side charting/interaction dependency to satisfy these ACs. [Source: `src/SpecScribe/Charts.cs:6-8`]

## Technical Requirements

- Every interactive element (card links and chart-segment links) must have a **visible** focus indicator via `:focus-visible`; the indicator must be perceivable against both light card surfaces and dark nav (on-brand teal/gold, ≥2px).
- Sunburst and epic-sunburst segment `<a>` elements must carry an `aria-label` giving name + status + count (the same text currently only in the hover `<title>`); keep the `<title>` for pointer users.
- A single `@media (prefers-reduced-motion: reduce)` block must neutralize non-essential transitions/transforms/animations (card lift, segment opacity, progress-fill width, and any smooth-scroll) without hiding information or breaking layout.
- All status/progress information must remain fully legible with motion disabled and with color perception unavailable (text labels + shapes + fractions already provide this — preserve, don't regress).
- A skip-to-content link must be the first focusable element on every page and target a single `<main id="main-content">` landmark present on every page type.
- `Charts.ProgressBar` output must carry `role="progressbar"` with `aria-valuenow`/`aria-valuemin`/`aria-valuemax` and an `aria-label`.
- No new runtime JS interaction engine; no dark-mode/theme system; changes stay host-neutral and static-host-safe.

## File Structure Requirements

Primary UPDATE candidates (touch only as needed):

- `src/SpecScribe/assets/specscribe.css`
  - Current state: `:hover` polish on all cards; `.sb-seg` opacity transition; `.progress-fill` width transition; the only `:focus-visible` rule is on `.site-nav-toggle`; **no** reduced-motion block; **no** `.skip-link`.
  - Story change focus: add one shared `:focus-visible` outline rule set for interactive cards, nav links, chart-segment links, and in-page anchor links; add SVG-segment focus indication driven off the path (stroke) since default SVG outlines are unreliable; add `.skip-link` (visually-hidden-until-focused) styles; add the `@media (prefers-reduced-motion: reduce)` block.
  - Must preserve: existing hover polish for pointer users; the `.site-nav-toggle:focus-visible` pattern; token/color system; the `.toc-strip`/(1.3) `.toc-sidebar` layout.

- `src/SpecScribe/Charts.cs`
  - Current state: `Sunburst`/`EpicSunburst` segment `<a>`s have `<title>` but no `aria-label`; `ProgressBar` has no ARIA; `TaskSunburst` segments are tooltip-only paths.
  - Story change focus: add `aria-label` to segment `<a>` elements (reuse the text already composed for `<title>`); add `role="progressbar"`/`aria-value*`/`aria-label` to `ProgressBar`. Keep everything HTML-escaped via `PathUtil.Html`.
  - Must preserve: pure-SVG-no-JS design, `<title>` tooltips, `role="img"`+chart `aria-label`, graceful empty states, link hrefs and geometry.

- `src/SpecScribe/PathUtil.cs`
  - Current state: `RenderHeadOpen` emits `<body>` with no skip link; `RenderFooter` closes the shell.
  - Story change focus: emit the skip-to-content link at the very start of `<body>` (or provide a shared helper the templaters call first) so every page gets it once.
  - Must preserve: existing head/meta/`<title>`/stylesheet-link output; keep the shell single-source so all page types inherit the skip link.

- `src/SpecScribe/HtmlTemplater.cs`
  - Current state: `RenderPage` wraps body in `<article class="doc-body">` (no `<main>`); `RenderIndex` emits the `.dashboard` section directly (no `<main>`).
  - Story change focus: introduce a single `<main id="main-content">` landmark around the primary content on both the index/dashboard and generic doc pages.
  - Must preserve: TOC strip/(1.3) sidebar, header/breadcrumb order, dashboard composition, `HasMermaid` init-script injection.

- `src/SpecScribe/EpicsTemplater.cs`
  - Story change focus: wrap epic/story detail primary content in `<main id="main-content">` (coexisting with 1.3's TOC-sidebar layout and the AC panels).
  - Must preserve: AC `id="ac-N"` anchors, kicker/status-pill layout, sunburst wiring, section ordering.

- `src/SpecScribe/RequirementsTemplater.cs`
  - Current state: already `<main class="req-index">`.
  - Story change focus: add `id="main-content"` to the existing `<main>` for skip-link parity; ensure exactly one `<main>` per page.

Primary TEST candidates:

- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — assert skip link + `<main id="main-content">` on index/dashboard; assert `role="progressbar"`/`aria-valuenow` on rendered progress bars.
- `tests/SpecScribe.Tests/` (new `ChartsTests.cs` or extend an existing suite) — assert sunburst/epic-sunburst segment `<a>` carry `aria-label`; assert legend text labels present.
- A stylesheet/generation-level assertion that a `@media (prefers-reduced-motion: reduce)` block and a focus-visible rule exist (guard against silent removal).

## Library and Framework Requirements

- Stay on the existing .NET / inline-SVG / CSS stack. No new dependencies. No JS framework, no charting library, no client-side interaction engine.
- Do not add a dark-mode/theming library or `prefers-color-scheme` handling (out of scope, see Scope Boundaries).
- Focus and reduced-motion are pure CSS (`:focus-visible`, `@media (prefers-reduced-motion: reduce)`); ARIA is static attributes emitted by `Charts`/templaters.

## Testing Requirements

- Preserve existing coverage in `HtmlTemplaterTests`, `SiteNavTests`, `SiteGeneratorTraceabilityTests`, and the `Charts`-adjacent tests.
- Add focused coverage for the gaps this story closes:
  - skip link (`class="skip-link" href="#main-content"`) present and `<main id="main-content">` present on index/dashboard (and, ideally, one detail page + one doc page),
  - `Charts.ProgressBar` output contains `role="progressbar"` with `aria-valuenow`/`aria-valuemin="0"`/`aria-valuemax="100"` and an `aria-label`,
  - sunburst/epic-sunburst segment `<a>` elements carry a non-empty `aria-label` (accessible name), and legend text labels remain,
  - a `@media (prefers-reduced-motion: reduce)` block and a `:focus-visible` outline rule exist in the emitted stylesheet.
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~Charts|FullyQualifiedName~SiteNav|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Manually verify in generated output (no automated a11y harness is set up in this repo): Tab reaches every card and chart segment with a visible focus ring; a focused sunburst segment exposes its name/status via `aria-label`; the skip link appears on first Tab and jumps to main content; enabling OS reduce-motion removes card-lift/opacity/width transitions while all status and progress text stays legible; no console errors; no broken anchors.

## UX and Accessibility Requirements

- Keyboard: every interactive element reachable by Tab in logical DOM order, with a **visible** focus state; chart "drill" happens via real link activation (Enter), and chart info is available on focus via `aria-label` (not pointer-only). [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md` Accessibility Floor; `epics.md` UX-DR7, UX-DR10, Story 1.4 AC #1]
- Skip link + semantic landmarks + progressbar ARIA are the accessibility floor this story cements. [Source: `epics.md` UX-DR16; `EXPERIENCE.md` Accessibility Floor]
- Status is **never color-only** — always paired with text label/shape/icon; the legends, pills, and progress fractions already do this. Preserve and verify. [Source: `epics.md` UX-DR17; `DESIGN.md` Status Semantic Colors]
- Motion respects `prefers-reduced-motion` with near-instant transitions and no looping animation; reduced-motion must not remove information (status/progress conveyed by text+shape+fraction, not motion). [Source: `epics.md` UX-DR18, UX-DR8; `EXPERIENCE.md` Accessibility Floor `@media (prefers-reduced-motion: reduce)` example; `DESIGN.md` Motion philosophy]
- Use the established token/color system and existing component styles; the new focus ring should read as on-brand (teal/gold), not a foreign default. [Source: `DESIGN.md` — index-card focus `outline: 2px solid teal, outline-offset: 2px`]

## Reinvention and Regression Guardrails

- Do NOT build a JavaScript client-side drill/hover/URL-hash sunburst — the links-based static implementation is the accepted design and satisfies AC #1. (Biggest disaster risk.)
- Do NOT add dark mode / a theme toggle / `prefers-color-scheme` handling — out of scope and net-new. (Second-biggest scope-creep risk.)
- Do NOT strip existing `<title>` tooltips when adding `aria-label`; keep both (pointer + non-pointer paths).
- Do NOT set `outline: none` anywhere without a visible replacement; use `:focus-visible` so mouse clicks don't leave lingering outlines.
- Do NOT make stat-card `<div>`s focusable just to "cover" AC #1 — they are non-interactive read-outs; adding tabindex would add empty Tab stops. (If a tooltip is ever added to stat cards per UX-DR4, that is a separate story.)
- Preserve zero/low-data graceful degradation in every `Charts` builder and the missing-section nav omission from Story 1.1.
- Do not regress Story 1.1 nav/breadcrumb/drawer, Story 1.2 traceability links, or Story 1.3 markdown/TOC rendering while touching shared CSS/templaters. Coordinate the shared-CSS and `<main>`-landmark changes with 1.3's in-flight TOC work.
- Keep everything host-neutral and static-host-safe so HTML/webview parity is not made harder and GitHub Pages output stays correct.

## Git Intelligence Summary

- `53432af` / `54e880f` are the Story 1.3 in-flight commits (details + planning) — this story's baseline; expect uncommitted 1.3 CSS/templater edits in the tree, so rebase/coordinate rather than assuming a clean `HtmlTemplater`/`specscribe.css`.
- `fb9fb88` (Story 1.2) established the verify-and-harden pattern with regression tests and a clean generation pass — emulate it.
- `3b0227e` hardened the color-swatch rewriter (a post-render HTML pass) — a reminder that fragment rendering and CSS are shared seams; make additive, centralized changes.
- `43c87cd` added CI retry for GitHub Pages deploys — generated output is published; keep anchors/links/skip-target static-host-safe.

## Latest Technical Information

- `:focus-visible` and `@media (prefers-reduced-motion: reduce)` are broadly supported in all current evergreen browsers; no polyfill or vendor-prefix work is needed. SVG focus-ring rendering is inconsistent across browsers, which is why the segment focus indicator should be expressed on the `<path>` (stroke/stroke-width) rather than relying on the `<a>`'s default outline. No external version considerations apply (charts are dependency-free; Mermaid CDN is unrelated to this story).

## Project Context Reference

- PRD: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`
- Epics: `_bmad-output/planning-artifacts/epics.md` (Story 1.4; UX-DR4/7/8/10/16/17/18; NFR6)
- Previous story: `_bmad-output/implementation-artifacts/1-3-markdown-fidelity-for-core-artifact-patterns.md`
- Architecture spine: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`
- Rendering architecture: `_bmad-output/specs/spec-specscribe/rendering-architecture.md`
- ADR 0002 (shared rendering core / host-neutral view models): `docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md`
- ADR 0004 (cross-surface interaction & theme contract): `docs/adrs/0004-cross-surface-interaction-and-theme-contract.md`
- UX design: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`
- UX behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md`
- Key source seams: `src/SpecScribe/Charts.cs`, `HtmlTemplater.cs`, `EpicsTemplater.cs`, `RequirementsTemplater.cs`, `PathUtil.cs`, `SiteNav.cs`, `assets/specscribe.css`

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8

### Debug Log References

- create-story workflow run for story `1-4-accessible-high-polish-interaction-baseline`
- workflow customization resolved via `resolve_customization.py`; on this Windows host use `py -3` for BMAD helper scripts (the `python`/`python3` shim prints the resolver JSON but is not a working interpreter)
- planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~Charts|FullyQualifiedName~SiteNav|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Treat as verify-and-harden over the existing pure-SVG-links + static-HTML substrate; do NOT build a JS drill engine or a dark-mode/theme system.
- AC #1: add a shared `:focus-visible` outline system for all interactive cards and chart-segment links (path-driven indicator for SVG); add `aria-label` accessible names to sunburst/epic-sunburst segment links so hover-only info is reachable without a pointer.
- AC #2: add one `@media (prefers-reduced-motion: reduce)` block neutralizing card-lift/segment-opacity/progress-width (and any 1.3 smooth-scroll); confirm information stays legible without motion.
- UX-DR16 floor supporting AC #1: skip link + single `<main id="main-content">` landmark per page; progressbar ARIA on `Charts.ProgressBar`.
- Add render-/generation-level regression tests; re-run targeted tests + a real generation pass; coordinate shared-CSS/templater edits with in-flight Story 1.3.

### Completion Notes List

- Story context assembled from epics.md (Story 1.4 ACs + UX-DR4/7/8/10/16/17/18 + NFR6), PRD, UX DESIGN/EXPERIENCE, architecture spine (AD-1/AD-2), ADRs 0002/0004, Story 1.2/1.3 intelligence, current code seams, and recent git history.
- Key finding: the dashboard uses pure-SVG `<a>`-link charts with no JS, deliberately diverging from the UX drill spec (UX-DR5/6/7). AC #1 is satisfiable by hardening this substrate (visible focus + segment aria-labels), not by building the JS engine.
- Hard AC #2 gap confirmed: no `@media (prefers-reduced-motion: reduce)` exists anywhere in `specscribe.css`.
- Explicitly scoped OUT: the JS client-side drill sunburst and dark-mode/theme toggle (no CSS/toggle exists; not in any Epic 1 AC).
- Coordination flag: Story 1.3 is `in-progress` and edits the same `specscribe.css`/`HtmlTemplater`/`EpicsTemplater`; sequence after or rebase onto 1.3, and fold any smooth-scroll it adds into this story's reduced-motion block.

### File List

- _bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-07-06: Created Story 1.4 implementation context — accessible high-polish interaction baseline. Documented the pure-SVG-links (no-JS) charting reality vs. the UX drill spec, the concrete AC #1 gaps (no focus-visible on cards/segments; segment info hover-only via `<title>`), the AC #2 gap (no reduced-motion media query), and the UX-DR16 floor (no skip link; inconsistent `<main>`; no progressbar ARIA). Set hard scope boundaries against building a JS drill engine or a dark-mode/theme system, and flagged coordination with in-flight Story 1.3 on shared CSS/templaters.
