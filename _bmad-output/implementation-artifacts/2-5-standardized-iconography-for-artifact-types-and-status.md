---
baseline_commit: 9fbca5e3aaa8593e0b9e188919acdbe1c130b5ad
---

# Story 2.5: Standardized Iconography for Artifact Types and Status

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user scanning the portal,
I want consistent icons for standardized concepts where they aid recognition,
so that artifact types and statuses are quicker to parse without adding clutter.

## Acceptance Criteria

1. **Given** recurring standardized concepts (artifact types, statuses, navigation sections)
   **When** pages render
   **Then** appropriate, consistent icons accompany labels where they aid recognition
   **And** icons are always paired with text (never icon-only) so meaning is preserved for all users.

2. **Given** the antiquarian design system and the accessibility conventions from Stories 1.4 and 1.5
   **When** icons are used
   **Then** they follow the established visual language and remain crisp and theme-consistent
   **And** decorative icons are hidden from assistive technology while meaningful icons carry accessible labels.

> **Origin & scope:** Fifth story of Epic 2 (Complete and Faithful BMad Artifact Representation).
> This story introduces a **single, self-contained inline-SVG icon library** and threads it through the
> concepts the portal already names in code — **status badges** (the `StatusStyles` vocabulary),
> **navigation / quick-link families** (the `SiteNav` items and `ModuleContext` doc kinds), and the
> **home index cards / section headers / work-type cards**. It advances **FR2** (first-class BMad support),
> **FR5** (coherent, legible navigation and artifact representation), and the accessibility contract
> **NFR6/UX-DR16/UX-DR17**. It is a **presentation-layer enrichment** over surfaces that are already
> rendered today: no new artifact discovery, no new page, no new nav item, no new dependency, no CDN, no
> icon font, no JS. Every icon is inline SVG (the same technique `Charts.cs` already uses), colored with
> `currentColor` so it inherits the surrounding label/badge color and stays theme-consistent, always paired
> with its text label, and marked `aria-hidden` because the text carries the meaning.

## Tasks / Subtasks

- [ ] Task 1: Create the single inline-SVG icon library (`Icons`) as the one source of truth (AC: #1, #2)
  - [ ] **Add a new `src/SpecScribe/Icons.cs` static class** that returns small inline-SVG strings keyed by a stable concept name. This mirrors the codebase's "one classifier / one status-color seam" discipline (`StatusStyles`, `ModuleContext`) — there must be exactly **one** place the SVG markup lives, so every surface renders the same glyph for the same concept. Do **not** scatter SVG literals into `HtmlTemplater`/`EpicsTemplater`/`SiteNav`. [Source: `src/SpecScribe/StatusStyles.cs:1-6`, `src/SpecScribe/ModuleContext.cs:54-96`]
  - [ ] **Two keyed taxonomies, both curated (not exhaustive — "where they aid recognition", AC#1):**
    - *Artifact-type / section icons* keyed by the concepts the portal already names: `PRD`, `Product Brief`, `UX Design`, `UX Experience`, `Architecture`, `Epics`, `Requirements`, `ADRs`, `Readme`, `Home`, plus `Spec`, `Sprint`, `Quick-dev`, `Deferred`. Key these off the **same labels `SiteNav`/`ModuleContext` already emit** (e.g. `ModuleDoc.Label`, the nav item labels, the quick-link family) so there is no third taxonomy to keep in sync. [Source: `src/SpecScribe/ModuleContext.cs:74-81`, `src/SpecScribe/SiteNav.cs:40-91`]
    - *Status icons* keyed by the **`StatusStyles` css-class vocabulary** (`done`, `active`, `review`, `ready`, `drafted`, `pending`, `deferred`) — one glyph per lifecycle stage, 1:1 with the six `--status-*` color tokens plus `deferred`. [Source: `src/SpecScribe/StatusStyles.cs:34-89`, `src/SpecScribe/assets/specscribe.css:34-40`]
  - [ ] **Every returned SVG must:** carry `aria-hidden="true"` and `focusable="false"` (the text label always accompanies it, so the icon is decorative reinforcement); use `viewBox` + `stroke`/`fill` set to **`currentColor`** (never a hard-coded hex) so it inherits the label/badge color and remains theme-consistent (light now, dark in a later story); carry a shared CSS hook class (e.g. `ss-icon`) for sizing. Keep the glyphs simple line/solid marks in the antiquarian spirit — legible at ~0.8–1em, crisp on HiDPI. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR17; `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`]
  - [ ] **Unknown key → empty string (graceful, NFR2):** an unrecognized concept returns no icon and the caller simply renders the label alone — never a broken glyph, never a placeholder box. [Source: `_bmad-output/planning-artifacts/epics.md` NFR2]

- [ ] Task 2: Anchor the *status* icon to the single status seam (AC: #1, #2)
  - [ ] **Add `StatusStyles.Icon(string cssClass)`** as a sibling to `StoryLabel`/`EpicLabel`, delegating to `Icons` for the status glyph keyed by the same css-class it already maps colors to. This keeps the status vocabulary anchored to the one seam every chart/badge already routes through (memory: route every new badge through the `--status-*` tokens), rather than letting callers reach into `Icons` with ad-hoc status strings. [Source: `src/SpecScribe/StatusStyles.cs:31-46`; [[specscribe-status-token-system]]]
  - [ ] **No new status color vocabulary.** The icon is a *shape* channel added alongside the existing color+text — it must not introduce a parallel palette or remap any stage. [Source: `src/SpecScribe/StatusStyles.cs:3-5`]

- [ ] Task 3: Thread status icons into every `.status-badge` (AC: #1, #2)
  - [ ] **Prepend the status glyph inside the badge span, before the text**, everywhere a `.status-badge` is emitted, so color + icon + word are the three redundant channels UX-DR17 asks for. Insertion points (all already render a `StatusStyles`-classed badge — reuse the css-class you already have in hand):
    - `HtmlTemplater.AppendWorkTypesSection` quick-dev card badge [Source: `src/SpecScribe/HtmlTemplater.cs:507-512`]
    - `EpicsTemplater` epic kicker badge, story kicker badge, and story-card header badge/task-badge [Source: `src/SpecScribe/EpicsTemplater.cs:102-104`, `:221-225`, `:340-346`]
    - Any `.status-badge` that Story 2.4 adds to the home index cards (`AppendIndexCard` / planning cards) and Story 2.3 adds to the sprint page — **compose with them, don't fight them**; if those stories have landed, route their badge through the same icon-prepend helper. [Source: `_bmad-output/implementation-artifacts/2-4-planning-artifacts-grouping-status-badges-and-prd-prominence.md:45-47`, `2-3-sprint-status-page-and-dashboard-widget.md`]
  - [ ] **Recommended: a tiny render helper** (e.g. `StatusStyles.Badge(cssClass, label)` or a `HtmlTemplater` local that emits `<span class="status-badge {cssClass}">{Icon}{Html(label)}</span>`) so the icon+text pairing is defined once and every badge site calls it, instead of hand-inlining the icon at ~5 sites and risking drift. The `task-badge` variant (which already contains a `MiniDonut`) should **not** also get a status glyph — leave it as-is to avoid double-marking. [Source: `src/SpecScribe/EpicsTemplater.cs:346`, `src/SpecScribe/assets/specscribe.css:839-844`]
  - [ ] **Text stays.** The badge must still contain its status word — the icon is *added*, never a replacement (AC#1 "never icon-only"). [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md:236`]

- [ ] Task 4: Thread artifact-type / section icons into nav, quick-links, and index cards (AC: #1)
  - [ ] **Nav bar + quick-link pills:** prepend the section/family icon before each `SiteNav.RenderNavBar` item label and each `AppendDashboardQuickLinks` pill, keyed by the same label the `QuickLinkFamily` accent already uses (planning/architecture/epics/requirements). The pills already carry a family accent color (`family-*` border) — the icon reinforces the same family. [Source: `src/SpecScribe/SiteNav.cs:96-116`, `src/SpecScribe/HtmlTemplater.cs:286-322`, `src/SpecScribe/assets/specscribe.css:1687-1690`]
  - [ ] **Home index cards + section headers:** prepend a type icon on `index-section-title` bands (Overview / Planning Artifacts / Implementation Artifacts / Direct & Quick-Dev Work / ADRs) and, where it aids recognition, on the card kicker/path. Prefer section-level icons over per-card clutter — the AC explicitly warns against "adding clutter", so lead with the section header and the badge, not an icon on every line. [Source: `src/SpecScribe/HtmlTemplater.cs:114-134`, `:497`, `:570-587`]
  - [ ] **Curate, don't carpet-bomb.** Add icons only where they speed recognition (section identity, status, nav family). Do **not** inject icons into body prose, requirement-ID backlinks, breadcrumbs, or the dashboard chart internals (donut/sunburst/heatmap already have their own visual language). [Source: `_bmad-output/planning-artifacts/epics.md` Story 2.5 AC#1; memory [[charting-is-pure-svg-no-js]]]

- [ ] Task 5: Self-contained CSS for icon sizing/alignment (AC: #2)
  - [ ] **Add a small `.ss-icon` rule to the embedded `src/SpecScribe/assets/specscribe.css`** — em-relative sizing (e.g. `width:1em;height:1em`), `vertical-align`/flex alignment so the icon sits on the text baseline inside badges/pills, and a small right gap. Reuse the badge's existing `inline-flex` + `gap` pattern (the `.status-badge.task-badge` rule already does exactly this) so badges align cleanly. **Do not** set an icon color here — `currentColor` inheritance is the whole point (theme-consistency, dark-mode-ready). [Source: `src/SpecScribe/assets/specscribe.css:820-844`]
  - [ ] **No loose asset files, no new stylesheet, no icon font.** Everything ships inside the already-embedded `specscribe.css` + inline SVG, preserving self-contained packaging (the tool embeds its CSS/JS as resources). [Source: `src/SpecScribe/SiteGenerator.cs:500-509`, `_bmad-output/planning-artifacts/epics.md` "Keep stylesheet delivery self-contained"]
  - [ ] **Motion:** icons are static, so `prefers-reduced-motion` is trivially satisfied — do not animate them. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR18]

- [ ] Task 6: Test coverage (AC: #1, #2)
  - [ ] **New `tests/SpecScribe.Tests/IconsTests.cs`**: every known artifact-type/section key and every status css-class returns a non-empty SVG; every returned SVG contains `aria-hidden="true"`, `focusable="false"`, and `currentColor` and contains **no** hard-coded hex color; an unknown key returns empty string (graceful). Mirror the `[InlineData]` table style used in `StatusStylesTests`. [Source: `tests/SpecScribe.Tests/StatusStylesTests.cs`]
  - [ ] **`StatusStylesTests`**: `Icon("done")`/`Icon("active")`/…/`Icon("deferred")` each return a status glyph; `Icon` of an unknown class returns empty. [Source: `tests/SpecScribe.Tests/StatusStylesTests.cs`]
  - [ ] **`HtmlTemplaterTests` + `EpicsTemplater` render-level assertions (the house string-assertion pattern)**: a rendered status badge contains **both** an `aria-hidden` icon and its status text (never one without the other); nav items and quick-link pills carry their section/family icon; the home index still has exactly one `<main id="main-content">` and the skip link (Story 1.4), and no icon replaces a text label anywhere. [Source: `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`]
  - [ ] **`StylesheetTests`**: `.ss-icon` rule is present; assert the icon CSS does **not** hard-code a per-status hex (relies on `currentColor`); reduced-motion / focus-visible / status-token assertions still pass unchanged. [Source: `tests/SpecScribe.Tests/StylesheetTests.cs:21-49`]
  - [ ] **Generation-level (temp-dir fixture, following `SiteGeneratorFidelityTests`)**: a full pass renders icons in badges/nav without malformed HTML; unknown/degenerate concepts render label-only; output stays static-host-safe. [Source: `tests/SpecScribe.Tests/SiteGeneratorFidelityTests.cs`]

- [ ] Task 7: End-to-end validation with a real generation pass (AC: #1, #2)
  - [ ] Run the focused test filter, then a real generation pass against this repo.
  - [ ] Manually verify on `docs/live/index.html` and `docs/live/epics.html`: status badges show **icon + colored word** (color, shape, and text all agree); nav/quick-link families carry recognizable icons; section headers read faster; **no icon-only affordance anywhere**; icons are crisp on HiDPI and pick up the surrounding text color (spot-check by toggling `[data-theme]` / a dark container to confirm `currentColor` follows). Confirm assistive-tech: icons are `aria-hidden` so screen readers announce the label text only, once.

## Developer Context Section

### Epic Context and Business Value

Epic 2 — "Complete and Faithful BMad Artifact Representation" — makes the portal reflect the **whole** project
truthfully and legibly. Story 2.1 surfaced quick-dev/deferred work; 2.2 surfaced the spec kernel; 2.3 surfaced
the sprint tracking; 2.4 gave planning docs prominence and status-as-badge. **Story 2.5 adds a recognition
layer**: consistent iconography for the concepts the portal already names — artifact types, statuses, and nav
sections — so a reader parses "what is this and what state is it in" faster, without reading every word. It is
the natural companion to 2.4's status badges (which added color + word; 2.5 adds the shape channel) and
advances **FR2/FR5** plus the accessibility contract (**UX-DR16/DR17**, **NFR6**). Only 2.6 (comment
annotations) remains in Epic 2.

### Story Foundation Extract

- **Primary concern:** recurring standardized concepts (artifact type, lifecycle status, nav section) get a
  consistent icon *paired with their text label*, so scanning the portal is faster and status reads through
  three redundant channels (color + icon + word) instead of two.
- **User outcome:** glance at a badge and know the stage from its shape before reading; glance at nav/section
  headers and recognize the artifact family instantly — with zero loss of meaning for screen-reader or
  color-blind users.
- **Success boundary:** a presentation enrichment built on existing seams — one new `Icons` library, a status
  glyph anchored to `StatusStyles`, and icon-prepend at the badge/nav/section render sites. No new parser,
  page, nav item, dependency, CDN, icon font, or JS.
- **Regression boundary:** every existing text label stays; Story 1.4 accessibility (single `main`, skip link,
  focus rings, aria) and Story 1.5 truthfulness preserved; the status-color single-source and `--status-*`
  tokens untouched; the sunburst/donut/heatmap visual language untouched; antiquarian identity kept.

### Current Implementation Reality (READ THIS FIRST)

- **There is no artifact/status icon system today.** The only SVG in output is the *chart* family (donut,
  sunburst, heatmap, mini-donut) in `Charts.cs`, a checkbox check embedded as a CSS `data:` URI, and
  decorative disclosure markers (`▸`/`▾`) via CSS `::before content`. Nothing marks *artifact type* or
  *status* with an icon. This story adds that, net-new. [Source: `src/SpecScribe/Charts.cs`, `src/SpecScribe/assets/specscribe.css:708-715`, `:1093`]
- **Inline SVG is the established, dependency-free technique.** `Charts.cs` composes SVG strings directly in
  C#; follow the same approach for `Icons` — no icon font, no external sprite, no CDN (local-first, NFR3).
  [Source: `src/SpecScribe/Charts.cs`, memory [[charting-is-pure-svg-no-js]]]
- **Status semantics live in exactly one place.** `StatusStyles` maps story/epic/requirement/quick-dev status
  onto `done/active/review/ready/drafted/pending/deferred`; the CSS defines those on `--status-*` tokens and
  `.status-badge.*`. The status *icon* must be anchored here (`StatusStyles.Icon`), keyed by the same class,
  so it stays 1:1 with the color vocabulary. [Source: `src/SpecScribe/StatusStyles.cs:16-89`, `src/SpecScribe/assets/specscribe.css:34-40`, `:820-844`]
- **`.status-badge` is emitted from ~5 sites, all already carrying a `StatusStyles` css-class:** quick-dev
  cards (`HtmlTemplater.AppendWorkTypesSection`), epic/story kickers and story-card headers (`EpicsTemplater`),
  and — once 2.3/2.4 land — the sprint page and home index cards. A single badge-render helper is the clean
  place to add the icon once. [Source: `src/SpecScribe/HtmlTemplater.cs:507-512`, `src/SpecScribe/EpicsTemplater.cs:102-104`, `:221-225`, `:340-346`]
- **Artifact-family identity is already named in two seams.** `SiteNav` builds nav items and quick-links with
  stable labels (Home/Readme/PRD/Architecture/Epics/Requirements/ADRs/…); `HtmlTemplater.QuickLinkFamily`
  already maps those labels onto `family-*` accent colors. Key the type/section icons off those same labels —
  don't invent a third naming scheme. [Source: `src/SpecScribe/SiteNav.cs:40-91`, `src/SpecScribe/HtmlTemplater.cs:311-322`]
- **`ModuleContext.BmadMethodDocs` already classifies the planning doc filenames** (prd/brief/DESIGN/EXPERIENCE
  /ARCHITECTURE-SPINE → labels). Reuse those labels as icon keys so the doc icons match the doc labels. [Source: `src/SpecScribe/ModuleContext.cs:74-81`]
- **Accessibility floor is contractual (Story 1.4).** `aria-hidden` on decorative marks, real focus rings,
  single `<main id="main-content">`, skip link — all must survive. Because every icon here is paired with text,
  every icon is decorative → `aria-hidden="true"` + `focusable="false"`. [Source: `src/SpecScribe/HtmlTemplater.cs:48-50`, `:77-81`, `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`]

### Scope Boundaries

- **IN (this story):** a new `Icons` inline-SVG library (single source of truth); `StatusStyles.Icon` anchoring
  the status glyph to the one status seam; a badge-render helper that emits `icon + text` for every
  `.status-badge`; artifact-type/section icons on nav items, quick-link pills, and index section headers; a
  self-contained `.ss-icon` CSS rule using `currentColor`; full graceful degradation (unknown concept →
  label-only); Icons/StatusStyles/render/stylesheet/generation tests.
- **OUT — comment annotations (Story 2.6), sprint-page/derived-status changes beyond adding the icon to its
  badge (Story 2.3 owns the page), planning-grouping changes (Story 2.4 owns those), the dashboard chart
  internals** (donut/sunburst/heatmap/mini-donut keep their own visual language — do not add these icons to
  chart segments or legends).
- **OUT — new artifact discovery/parsing, a new page, a new nav item, a new dependency, an icon font, a CDN,
  or any JS.** Every surface here is already rendered.
- **OUT — dark-mode *implementation* (Epic-later).** But `currentColor` makes these icons dark-mode-ready by
  construction — do not hard-code light-theme hex into any glyph.

### Previous Story Intelligence

- **2.4 (`ready-for-dev`) is the immediate predecessor** and the tightest coupling: it renders index-card and
  planning-card status as `.status-badge` (adding `StatusStyles.ForDoc`/`DocLabel`). 2.5 adds the icon to
  those same badges — **compose through one badge-render helper**, don't fork. If 2.4 hasn't landed yet, still
  build the helper; the badges it introduces will pick it up. [Source: `_bmad-output/implementation-artifacts/2-4-planning-artifacts-grouping-status-badges-and-prd-prominence.md:41-47`]
- **2.3** adds the sprint status page and `StatusStyles.ForSprint`/`SprintLabel`; its badges are more badge
  sites for the same icon helper. Coordinate, don't duplicate. [Source: `_bmad-output/implementation-artifacts/2-3-sprint-status-page-and-dashboard-widget.md`]
- **2.1** established the quick-dev/deferred work-type cards (status-badged) and the "distinguish *no plan yet*
  from *no data*" principle — apply the graceful-unknown rule (missing concept → label-only) in the same
  spirit. [Source: `src/SpecScribe/HtmlTemplater.cs:493-536`]
- **1.4** is the accessibility contract (skip link, single `main`, focus, `aria-hidden` for decoration) and
  **1.5** the truthfulness + single-status-color-vocabulary contract — both inherited by anchoring the status
  icon to `StatusStyles` and marking every icon `aria-hidden`. [Source: `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`, `1-5-dashboard-insight-polish-and-visual-truthfulness.md`]
- **Environment:** use `py -3` for BMAD helper scripts on this Windows host (`python`/`python3` are not on
  PATH — the create-story `resolve_customization.py` step failed for exactly this reason).

### Architecture Compliance

- **One icon library, one status seam.** SVG markup lives only in `Icons`; the status glyph is exposed via
  `StatusStyles.Icon` keyed by the existing status css-class; badge sites call one shared render helper. No
  scattered SVG literals, no second status vocabulary, no third artifact-naming scheme (reuse `SiteNav`/
  `ModuleContext` labels). [Source: `src/SpecScribe/StatusStyles.cs`, `src/SpecScribe/ModuleContext.cs:74-81`, `src/SpecScribe/SiteNav.cs`]
- **`currentColor`, not literals — theme-consistency + single-source colors.** Icons inherit the label/badge
  color so they are correct in light today and dark later, and so status color continues to come only from the
  `--status-*` tokens (memory: route every new badge through the status tokens). [Source: `src/SpecScribe/assets/specscribe.css:34-40`; [[specscribe-status-token-system]]]
- **Graceful degradation is contractual (NFR2).** Unknown concept → no icon, label renders alone; never a
  broken glyph, missing-image box, or exception. [Source: `_bmad-output/planning-artifacts/epics.md` NFR2]
- **Host-neutral, static-host-safe, self-contained (NFR6, GitHub Pages, Epic 6 webview).** Inline SVG + one
  embedded CSS rule; no host-specific behavior, no loose assets, no runtime asset fetch — so the Epic 6 webview
  inherits the icons unchanged. [Source: `src/SpecScribe/SiteGenerator.cs:500-509`, `src/SpecScribe/PathUtil.cs`]

## Technical Requirements

- Add `src/SpecScribe/Icons.cs`: a static inline-SVG library keyed by (a) artifact-type/section labels reused
  from `SiteNav`/`ModuleContext` and (b) the `StatusStyles` status css-classes. Every SVG carries
  `aria-hidden="true"`, `focusable="false"`, a shared `ss-icon` class, and `currentColor` (no hard-coded hex).
  Unknown key → empty string.
- Add `StatusStyles.Icon(string cssClass)` delegating to `Icons` for the status glyph, keyed by the same class
  it maps colors to (no new palette).
- Introduce one badge-render helper that emits `<span class="status-badge {cssClass}">{icon}{Html(label)}</span>`
  and route every `.status-badge` site through it (quick-dev cards, epic/story kickers, story-card headers, and
  the 2.3/2.4 sprint/index badges). Leave `task-badge` (which has a `MiniDonut`) un-iconed.
- Prepend section/family icons to `SiteNav.RenderNavBar` items, `AppendDashboardQuickLinks` pills, and
  `index-section-title` headers, keyed off the labels those seams already use (`QuickLinkFamily`/`ModuleDoc`).
- Add a small self-contained `.ss-icon` rule (em sizing + baseline alignment + gap) to the embedded
  `specscribe.css`; do not set an icon color (rely on `currentColor`).
- Preserve every existing text label (never icon-only), Story 1.4 accessibility (single `main`, skip link,
  focus, `aria-hidden` on decoration), Story 1.5 truthfulness and single-status-color-source. No new JS, no new
  dependency, static-host-safe.

## File Structure Requirements

Primary NEW:

- `src/SpecScribe/Icons.cs` — the inline-SVG icon library (single source of truth).
- `tests/SpecScribe.Tests/IconsTests.cs` — glyph presence, `aria-hidden`/`focusable`/`currentColor` invariants,
  graceful unknown key.

Primary UPDATE candidates:

- `src/SpecScribe/StatusStyles.cs` — add `Icon(cssClass)` (delegates to `Icons`); optionally a `Badge(...)`
  render helper.
- `src/SpecScribe/HtmlTemplater.cs` — route quick-dev card badges + index section headers + quick-link pills
  through the icon helpers.
- `src/SpecScribe/EpicsTemplater.cs` — route epic/story kicker + story-card header badges through the badge
  helper (skip `task-badge`).
- `src/SpecScribe/SiteNav.cs` — prepend section icons to nav item labels.
- `src/SpecScribe/assets/specscribe.css` — add the `.ss-icon` sizing/alignment rule (no color).

Primary TEST updates:

- `tests/SpecScribe.Tests/StatusStylesTests.cs` — `Icon(cssClass)` table.
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — badges contain icon **and** text; nav/pills carry icons;
  single `main` + skip link intact; nothing icon-only.
- `tests/SpecScribe.Tests/StylesheetTests.cs` — `.ss-icon` present, no per-status hex in icon CSS, existing
  a11y/token assertions unchanged.
- A generation-level test (extend `SiteGeneratorFidelityTests`) — icons render, unknown concepts degrade to
  label-only, no malformed HTML.

## Library and Framework Requirements

- Stay on the existing .NET / Markdig / YamlDotNet / inline-SVG / CSS stack. **No new runtime dependency, no
  icon font, no external sprite/CDN, no JS.** Icons are inline SVG composed in C# exactly like `Charts.cs`.
  Local-first / privacy-preserving (NFR3) forbids any remote asset fetch. [Source: `src/SpecScribe/Charts.cs`, `_bmad-output/planning-artifacts/epics.md` NFR3]

## Testing Requirements

- Preserve existing coverage and **Story 1.4's accessibility assertions** and **Story 1.5's truthfulness
  assertions** — none may regress. The home index keeps exactly one `<main id="main-content">` and the skip
  link; every status badge keeps its status word.
- Add coverage (see Task 6): `Icons` library (glyph presence per key, `aria-hidden`/`focusable`/`currentColor`
  invariants, graceful unknown); `StatusStyles.Icon`; render-level (badge = icon + text, never icon-only; nav/
  pills carry icons); stylesheet (`.ss-icon` present, no per-status hex, reduced-motion/focus/token rules
  intact); generation-level (icons render, unknown degrades to label-only, no malformed output).
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~Icons|FullyQualifiedName~StatusStyles|FullyQualifiedName~HtmlTemplater|FullyQualifiedName~Stylesheet|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Manual verification on `docs/live/index.html` + `docs/live/epics.html` (badges show icon + colored word; nav/
  quick-link families carry icons; section headers read faster; nothing icon-only; crisp on HiDPI; icons follow
  the surrounding text color / `currentColor`).

## UX and Accessibility Requirements

- Icons follow the **antiquarian visual language**: simple, calm line/solid marks that sit comfortably with the
  parchment/rust/teal/gold palette and serif type; they reinforce, never dominate. The sunburst remains "the
  icon of this product" — these are small recognition aids, not competing hero graphics. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md:154`]
- **Status is never color-only and never icon-only (UX-DR17):** every status carries color + icon + text word,
  three redundant channels. Every artifact/section icon is paired with its label. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR17, `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md:236`]
- **Decorative icons hidden from assistive tech (AC#2, Story 1.4):** because the text label always accompanies
  the icon, every icon here is decorative → `aria-hidden="true"` + `focusable="false"`, so screen readers
  announce the label once, not "image, image". [Source: `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`]
- **Theme-consistent + crisp (AC#2):** `currentColor` + `viewBox` keep icons on-hue in light (and dark later)
  and sharp on HiDPI. Icons are static, so `prefers-reduced-motion` is trivially satisfied. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR18]
- **Don't add clutter (AC#1):** curate insertion points (status badges, nav/section identity) — do not icon
  every card line, breadcrumb, or prose run.

## Reinvention and Regression Guardrails

- Do NOT scatter SVG literals across templaters — all glyphs live in one `Icons` class.
- Do NOT invent a new status color vocabulary or a third artifact-naming scheme — status icons key off the
  `StatusStyles` css-classes; type/section icons key off the `SiteNav`/`ModuleContext` labels.
- Do NOT hard-code hex into any glyph — use `currentColor` (theme-consistency + single `--status-*` source).
- Do NOT add an icon font, external sprite, CDN, or JS. Do NOT fetch any remote asset (NFR3 local-first).
- Do NOT render any icon-only affordance — every icon is paired with its text label.
- Do NOT touch the dashboard chart internals (donut/sunburst/heatmap/mini-donut) or the status-color tokens.
- Do NOT regress Story 1.4 accessibility (skip link, single `main`, focus, aria), Story 1.5 truthfulness, or
  the self-contained CSS packaging. Keep all output static-host-safe (GitHub Pages).

## Git Intelligence Summary

- Baseline `9fbca5e` (main, "2.1 Dev"). Note the sprint file has 2.2/2.3/2.4 `ready-for-dev` and 2.1 in
  `review`; on this host the *code* for 2.2–2.4 may not yet be merged onto `main`. Build the badge-render
  helper and icon library so they **compose with** whatever badge sites exist when you implement — don't
  assume 2.3/2.4's new badges are present, and don't fork the badge path if they are. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:60-63`]
- Shared seams (`StatusStyles`/`SiteNav`/`ModuleContext`/`HtmlTemplater`/`EpicsTemplater`/`Charts`) are the
  single-source points — add the icon library and `StatusStyles.Icon` additively and centrally, the same
  pattern 1.2–2.4 followed.
- Inline-SVG composition already exists in `Charts.cs`; the checkbox check is a `data:` URI in the CSS — both
  confirm the "SVG, no external assets, no JS" house style to mirror. [Source: `src/SpecScribe/Charts.cs`, `src/SpecScribe/assets/specscribe.css:1093`]
- Generated output publishes to GitHub Pages — keep everything static (relative, no asset fetch, `currentColor`).

## Latest Technical Information

- No framework/library version decisions are introduced by this story; it stays entirely within the existing
  .NET + Markdig + YamlDotNet + inline-SVG + CSS stack. Inline SVG with `currentColor`, `aria-hidden`, and
  `focusable="false"` is universally supported across evergreen browsers and the VS Code webview — no polyfill,
  no new capability, no build step. `currentColor` is the standard mechanism for theme-inheriting icons and is
  what makes the set dark-mode-ready without duplicated assets.

## Project Context Reference

- Epic + story source: `_bmad-output/planning-artifacts/epics.md` (Epic 2, Story 2.5; FR2/FR5; NFR2/NFR3/NFR6; UX-DR16/DR17/DR18)
- Single status seam (anchor the status glyph here): `src/SpecScribe/StatusStyles.cs`; status tokens: `src/SpecScribe/assets/specscribe.css:34-40`, badges `:820-844`
- Artifact/section naming to reuse as icon keys: `src/SpecScribe/SiteNav.cs:40-116`, `src/SpecScribe/ModuleContext.cs:74-81`, `HtmlTemplater.QuickLinkFamily` `src/SpecScribe/HtmlTemplater.cs:311-322`
- Badge render sites to route through the helper: `src/SpecScribe/HtmlTemplater.cs:507-512`, `src/SpecScribe/EpicsTemplater.cs:102-104`, `:221-225`, `:340-346`
- Inline-SVG house pattern to mirror: `src/SpecScribe/Charts.cs`; embedded-CSS packaging: `src/SpecScribe/SiteGenerator.cs:500-509`
- Predecessors: `_bmad-output/implementation-artifacts/2-1-accurate-work-representation-and-authoring-guidance.md`, `2-2-first-class-rendering-of-spec-artifacts.md`, `2-3-sprint-status-page-and-dashboard-widget.md`, `2-4-planning-artifacts-grouping-status-badges-and-prd-prominence.md`
- Accessibility baseline: `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`
- Truthfulness baseline: `_bmad-output/implementation-artifacts/1-5-dashboard-insight-polish-and-visual-truthfulness.md`
- Successor (do NOT do here): 2.6 comment annotations
- UX design/behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`, `EXPERIENCE.md`
- Memory: [[specscribe-status-token-system]], [[story-1-4-a11y-seams-for-1-5]], [[charting-is-pure-svg-no-js]]

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed — comprehensive developer guide created for
  Epic 2's iconography story: a presentation-layer enrichment that adds one self-contained inline-SVG `Icons`
  library (the single source of truth), anchors the status glyph to the `StatusStyles` css-class vocabulary via
  `StatusStyles.Icon`, routes every `.status-badge` through one icon+text render helper, and adds artifact-type/
  section icons to nav items, quick-link pills, and index section headers — every icon paired with its text
  label, `aria-hidden` and `currentColor` so it's decorative, theme-consistent, and dark-mode-ready, with full
  graceful degradation (unknown concept → label-only), Story 1.4 accessibility and Story 1.5 truthfulness/
  single-status-color-source preserved, and no new parser, page, nav item, dependency, icon font, CDN, or JS.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8

### Debug Log References

- Confirmed there is **no** artifact/status icon system today: the only SVG in output is the chart family in
  `Charts.cs`, a checkbox check as a CSS `data:` URI, and decorative `▸`/`▾` disclosure markers via
  `::before content` — nothing marks artifact type or status. [`src/SpecScribe/Charts.cs`, `src/SpecScribe/assets/specscribe.css:708-715`, `:1093`]
- Confirmed `StatusStyles` is the single status→semantics seam (`ForStatus`/`ForStory`/`ForEpic`/`ForRequirement`
  + `*Label`) and `--status-*` tokens are the single color source, so the status glyph belongs on
  `StatusStyles.Icon` keyed by the same css-class. [`src/SpecScribe/StatusStyles.cs:16-89`, `src/SpecScribe/assets/specscribe.css:34-40`]
- Confirmed `.status-badge` is emitted from ~5 sites, all already carrying a `StatusStyles` css-class (quick-dev
  cards, epic/story kickers, story-card header/task-badge), so a single badge-render helper adds the icon once.
  [`src/SpecScribe/HtmlTemplater.cs:507-512`, `src/SpecScribe/EpicsTemplater.cs:102-104`, `:221-225`, `:340-346`]
- Confirmed artifact-family identity is already named in `SiteNav` (nav items + quick-links) and
  `HtmlTemplater.QuickLinkFamily` (family-* accents) and `ModuleContext.BmadMethodDocs` (doc labels) — reuse
  those labels as icon keys, no third taxonomy. [`src/SpecScribe/SiteNav.cs:40-116`, `src/SpecScribe/HtmlTemplater.cs:311-322`, `src/SpecScribe/ModuleContext.cs:74-81`]
- Confirmed the accessibility floor (`aria-hidden` decoration, single `<main id="main-content">`, skip link,
  focus-visible) in `HtmlTemplater`/`StylesheetTests` — every paired icon is decorative → `aria-hidden`.
  [`src/SpecScribe/HtmlTemplater.cs:48-50`, `:77-81`, `tests/SpecScribe.Tests/StylesheetTests.cs:21-49`]
- Confirmed self-contained CSS packaging (embedded `specscribe.css`) and inline-SVG-only house style (Charts,
  `data:` URI check) → no icon font, no CDN, no loose asset, no JS. [`src/SpecScribe/SiteGenerator.cs:500-509`]
- Environment: `python`/`python3` absent on PATH (create-story `resolve_customization.py` step failed); use
  `py -3` for BMAD helper scripts on this Windows host.
- Planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~Icons|FullyQualifiedName~StatusStyles|FullyQualifiedName~HtmlTemplater|FullyQualifiedName~Stylesheet|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Task 1 (new `Icons` library: type/section + status glyphs, `aria-hidden`/`focusable`/`currentColor`/`ss-icon`,
  unknown→empty) → Task 2 (`StatusStyles.Icon` anchoring the status glyph) → Task 3 (one badge-render helper +
  route every `.status-badge` site through it, skip `task-badge`) → Task 4 (nav/quick-link/section-header
  artifact icons keyed off existing labels) → Task 5 (`.ss-icon` CSS, no color) → Task 6 (tests) → Task 7 (real
  generation pass).
- Keep every change in the shared seams; prefer render-/generation-level string assertions; keep everything
  static-host-safe, self-contained, and dark-mode-ready via `currentColor`; keep every icon paired with text
  and `aria-hidden`.

### Completion Notes List

- Fifth story of Epic 2. A presentation-layer recognition enrichment: one inline-SVG `Icons` library, a status
  glyph anchored to `StatusStyles`, a single badge-render helper (icon + text) routed across all `.status-badge`
  sites, and artifact-type/section icons on nav/quick-links/section headers — every icon paired with its label,
  decorative (`aria-hidden`), and theme-consistent (`currentColor`).
- Explicitly kept out: comment annotations (2.6), sprint/planning page structure (2.3/2.4 own those), dashboard
  chart internals, dark-mode implementation, new discovery/parsing/page/nav/dependency, icon font, CDN, JS.
- Coordination flags: reuse `StatusStyles` (add `Icon`, no new palette); reuse `SiteNav`/`ModuleContext` labels
  as icon keys (no third taxonomy); one badge-render helper so 2.3/2.4 badges compose (don't fork); `currentColor`
  only (no hard-coded hex, dark-mode-ready); graceful unknown → label-only; never icon-only (UX-DR17).

### File List

- _bmad-output/implementation-artifacts/2-5-standardized-iconography-for-artifact-types-and-status.md

## Change Log

- 2026-07-06: Created Story 2.5 as Epic 2's iconography story. Scoped: a presentation-layer enrichment that
  introduces one self-contained inline-SVG `Icons` library (single source of truth for artifact-type/section
  and status glyphs), anchors the status glyph to the `StatusStyles` css-class vocabulary via `StatusStyles.Icon`,
  routes every `.status-badge` through one icon+text render helper (skipping `task-badge`), and adds artifact/
  section icons to nav items, quick-link pills, and home index section headers — keyed off the labels
  `SiteNav`/`ModuleContext` already use. Every icon is paired with its text label, `aria-hidden`/`focusable=false`
  (decorative), and colored with `currentColor` (theme-consistent, dark-mode-ready); a small self-contained
  `.ss-icon` CSS rule handles sizing/alignment. Full graceful degradation (unknown concept → label-only);
  Story 1.4 accessibility, Story 1.5 truthfulness, and the single `--status-*` color source preserved; no new
  parser, page, nav item, dependency, icon font, CDN, or JS. Baseline `9fbca5e`.
