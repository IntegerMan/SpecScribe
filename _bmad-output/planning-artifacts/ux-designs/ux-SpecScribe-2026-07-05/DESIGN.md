---
title: SpecScribe Design
status: final
created: 2026-07-05
updated: 2026-07-05
project: SpecScribe
colors:
  # Light mode primitives
  parchment: "#f4ead5"
  parchment-dark: "#e8d5b0"
  parchment-deep: "#d4b896"
  cream: "#f5f0e8"
  warm-white: "#faf7f2"
  ink: "#2a1f0e"
  ink-faded: "#5a4535"
  ink-light: "#7a6250"
  rust: "#8b3a1a"
  rust-light: "#c4622d"
  moss: "#4a6741"
  moss-light: "#6b8f62"
  teal-deep: "#1e4a5a"
  teal: "#2e6b7a"
  gold: "#b8860b"
  gold-light: "#d4a017"
  border: "#d4c4a8"
  shadow: "rgba(42,31,14,0.15)"
  # Dark mode primitives
  dark-bg: "#1a1208"
  dark-surface: "#241a0d"
  dark-surface-raised: "#2e2010"
  dark-border: "#4a3520"
  dark-ink: "#f0e4c8"
  dark-ink-faded: "#c8aa88"
  dark-ink-light: "#a07850"
  dark-rust: "#e05a30"
  dark-gold: "#d4a017"
  dark-teal: "#4a9ab0"
  dark-moss: "#6b8f62"
typography:
  family-serif: "'Palatino Linotype', 'Book Antiqua', Palatino, Georgia, serif"
  family-mono: "'Courier New', Courier, monospace"
  family-nav: "Georgia, 'Times New Roman', serif"
  scale-xs: "0.68rem"
  scale-sm: "0.78rem"
  scale-base: "0.88rem"
  scale-md: "1rem"
  scale-lg: "1.1rem"
  scale-xl: "1.3rem"
  scale-2xl: "1.6rem"
  scale-3xl: "2rem"
  leading-tight: "1.3"
  leading-base: "1.65"
  tracking-wide: "0.05em"
  tracking-caps: "0.2em"
rounded:
  none: "0"
  sm: "3px"
  md: "4px"
  lg: "6px"
  full: "999px"
spacing:
  1: "0.25rem"
  2: "0.5rem"
  3: "0.75rem"
  4: "1rem"
  5: "1.3rem"
  6: "1.5rem"
  8: "2rem"
  10: "2.5rem"
  12: "3rem"
components:
  nav:
    bg-light: "rgba(26,18,8,0.94)"
    bg-dark: "rgba(10,7,3,0.96)"
    height: "42px"
    brand-color: "{colors.gold-light}"
    link-color: "{colors.parchment-deep}"
    link-active-color: "#f5c842"
    blur: "blur(4px)"
  stat-card:
    bg: "{colors.warm-white}"
    bg-dark: "{colors.dark-surface-raised}"
    border: "{colors.border}"
    number-color: "{colors.rust}"
    label-color: "{colors.ink-light}"
    hover-lift: "translateY(-2px)"
  sunburst:
    seg-active: "{colors.teal}"
    seg-done: "{colors.moss}"
    seg-review: "{colors.gold}"
    seg-drafted: "{colors.parchment-deep}"
    seg-pending: "{colors.rust-light}"
    seg-hover-opacity: "0.85"
    tooltip-bg: "rgba(26,18,8,0.92)"
    tooltip-color: "{colors.parchment}"
    tooltip-radius: "{rounded.md}"
    drill-ring-color: "{colors.gold-light}"
    drill-ring-width: "2px"
  tooltip:
    bg-light: "rgba(42,31,14,0.92)"
    bg-dark: "rgba(240,228,200,0.96)"
    color-light: "{colors.parchment}"
    color-dark: "{colors.dark-bg}"
    radius: "{rounded.md}"
    padding: "0.35rem 0.7rem"
    font-size: "{typography.scale-sm}"
    shadow: "0 4px 12px rgba(0,0,0,0.3)"
    arrow-size: "6px"
  progress-bar:
    track-bg: "{colors.parchment-dark}"
    fill-complete: "{colors.moss}"
    fill-partial: "{colors.gold}"
    fill-empty: "{colors.parchment-deep}"
    height: "8px"
    radius: "{rounded.full}"
    transition: "width 0.6s cubic-bezier(0.4,0,0.2,1)"
  pill:
    bg: "{colors.warm-white}"
    border: "{colors.border}"
    complete: "{colors.moss}"
    in-progress: "{colors.gold}"
    review: "{colors.teal}"
    superseded: "{colors.ink-light}"
  index-card:
    bg: "{colors.warm-white}"
    border: "{colors.border}"
    hover-border: "{colors.rust-light}"
    shadow: "0 2px 8px {colors.shadow}"
    hover-shadow: "0 4px 14px {colors.shadow}"
    hover-lift: "translateY(-2px)"
    transition: "border-color 0.2s, box-shadow 0.2s, transform 0.15s"
  now-next:
    done-accent: "{colors.moss}"
    review-accent: "{colors.gold}"
    pending-accent: "{colors.teal}"
  cli-output:
    bg: "{colors.dark-surface}"
    color: "{colors.dark-ink}"
    success: "{colors.moss-light}"
    warn: "{colors.gold-light}"
    error: "{colors.rust-light}"
    dim: "{colors.dark-ink-light}"
    border-left-accent: "{colors.teal}"
---

# SpecScribe Design

## Brand & Style

SpecScribe is a **spec-driven clarity tool** — it turns dense agent planning artifacts into a readable project portal. The design voice is **scholarly but alive**: warm antiquarian roots (parchment, ink, rust, gold) elevated with purposeful interactivity. The portal should feel like a well-crafted living document, not a generic dashboard.

**Core personality:** Precise. Warm. Trustworthy. Quietly confident.

**What we are:** A portal that makes invisible project structure visible at a glance. The sunburst is the icon of this product — it telegraphs structure and completeness before a word is read.

**What we are not:** A sterile dev-ops monitor. Not dark-mode-first. Not enterprise gray. Not a Notion clone.

**Conference demo principle:** Any demo screenshot should communicate "there is a project here, with this much done, organized this way" within three seconds. The sunburst and stat row are the hero frame.

**Color mode:** Light-first, with a warm dark mode that preserves the antiquarian character (dark parchment, not blue-gray). System preference detected via `prefers-color-scheme`; user toggle persisted in `localStorage`.

**Motion philosophy:** Purposeful and brief. Progress bars animate in on page load (single pass, 600 ms ease). Sunburst segments highlight on hover (opacity shift, no layout change). Drill transitions use a 200 ms ease-in-out zoom-and-fade centered on the clicked segment. Tooltips appear in 120 ms, dismiss in 80 ms. No looping animations. Respect `prefers-reduced-motion`.

---

## Colors

### Light Mode

| Token | Value | Role |
|---|---|---|
| `cream` | `#f5f0e8` | Page background |
| `warm-white` | `#faf7f2` | Card surfaces |
| `parchment` | `#f4ead5` | Elevated panels, toc-strip |
| `parchment-dark` | `#e8d5b0` | Table headers, code inline bg |
| `parchment-deep` | `#d4b896` | Borders variant, muted labels |
| `ink` | `#2a1f0e` | Body text, strong |
| `ink-faded` | `#5a4535` | Body paragraph text |
| `ink-light` | `#7a6250` | Secondary labels, metadata |
| `rust` | `#8b3a1a` | Headings, brand highlights |
| `rust-light` | `#c4622d` | Hover accent, card hover border |
| `moss` | `#4a6741` | Complete / done status |
| `moss-light` | `#6b8f62` | Done segment fill |
| `teal-deep` | `#1e4a5a` | h3 color |
| `teal` | `#2e6b7a` | Links, active state, review segment |
| `gold` | `#b8860b` | In-progress, partial status |
| `gold-light` | `#d4a017` | Kicker labels, link hover accent |
| `border` | `#d4c4a8` | Card/table borders |
| `shadow` | `rgba(42,31,14,0.15)` | Box shadows |

Nav background: `rgba(26,18,8,0.94)` with `backdrop-filter: blur(4px)`.

### Dark Mode

Activated by `[data-theme="dark"]` on `<html>` (or via `@media (prefers-color-scheme: dark)` before JS hydration).

| Token | Light value | Dark value |
|---|---|---|
| Page bg | `cream` | `#1a1208` |
| Card surface | `warm-white` | `#241a0d` |
| Elevated panel | `parchment` | `#2e2010` |
| Body text | `ink` | `#f0e4c8` |
| Paragraph text | `ink-faded` | `#c8aa88` |
| Secondary labels | `ink-light` | `#a07850` |
| Headings | `rust` | `#e05a30` |
| Links | `teal` | `#4a9ab0` |
| Done | `moss` | `#6b8f62` |
| In-progress | `gold` | `#d4a017` |
| Border | `#d4c4a8` | `#4a3520` |
| Nav bg | `rgba(26,18,8,0.94)` | `rgba(10,7,3,0.96)` |

All dark values preserve hue family — the palette warms down, it does not shift to blue-gray.

### Status Semantic Colors

| Status | Dot / Segment | Pill border | Text |
|---|---|---|---|
| done / complete / accepted | `moss-light` | `moss-light` | `moss` |
| in-development / in-progress | `teal` | `teal` | `teal-deep` |
| review | `gold-light` | `gold-light` | `gold` |
| drafted / proposed | `parchment-deep` | `border` | `ink-light` |
| pending / not-started | `rust-light` | `rust-light` | `rust` |
| superseded / deprecated | `border` | `border` | `ink-light` (strikethrough) |

---

## Typography

**Serif stack** — body text, headings, prose: `'Palatino Linotype', 'Book Antiqua', Palatino, Georgia, serif`
**Mono stack** — IDs, labels, kickers, code: `'Courier New', Courier, monospace`
**Nav stack** — nav brand and links: `Georgia, 'Times New Roman', serif`

| Role | Size | Weight | Style |
|---|---|---|---|
| H1 page title | `2rem` | bold | serif, rust, border-bottom |
| H1 in doc-body | `1.6rem` | bold | serif, rust, border-bottom 2px |
| H2 | `1.3rem` | bold | serif, rust, border-bottom 1px |
| H3 | `1.1rem` | bold | serif, teal-deep |
| H4 | `1rem` | bold | serif, ink-faded |
| Body paragraph | `1rem` / `line-height 1.65` | normal | serif, ink-faded |
| Kicker / label | `0.85rem` | normal | mono, gold, `letter-spacing 0.05em` |
| Pill / tag | `0.72rem` | normal | mono, `letter-spacing 0.03em` |
| Nav brand | `0.85rem` | bold | serif, gold-light, `letter-spacing 0.05em` |
| Nav links | `0.8rem` | normal | serif, parchment-deep |
| Stat number | `1.8rem` | bold | serif, rust |
| Stat label | `0.78rem` | normal | mono, ink-light |
| Breadcrumb | `0.78rem` | normal | serif |
| Footer | `0.78rem` | normal | serif italic, ink-light |
| Tooltip text | `0.78rem` | normal | mono |
| Code inline | `0.85em` | normal | mono, rust |
| Code block | `0.9rem` / `line-height 1.5` | normal | mono, `#f0e4c8` on `#241a0d` |

Anti-aliasing: `-webkit-font-smoothing: antialiased` on `body`.

---

## Layout & Spacing

**Max content width:** `860px` (article, doc-body). **Max wide grid:** `1100px` (index grid, epic overview, dashboard).

**Base unit:** `0.25rem` (4 px). All spacing values are multiples.

**Page structure (top → bottom):**
1. Sticky nav — `42px` tall, full-bleed dark, `z-index: 100`
2. Page header — centered, `padding-top: 2.5rem`
3. Breadcrumb (where applicable) — `0.8rem` above header
4. Dashboard / main content — `max-width: 1100px` or `860px`
5. Footer — `border-top`, centered, italic

**Dashboard layout:**
- Stat row: `display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 1rem;` — 4 up on wide, wraps gracefully
- Chart panels: full `1100px` width, `padding: 1.5rem`, white card surface, `border-radius: 6px`
- Sunburst panel: centered SVG, max `420px × 420px`, with drill breadcrumb above and detail panel below on drill
- Now & Next: two-column flex row, wraps to single column at `< 600px`

**Index grid:** `repeat(auto-fill, minmax(260px, 1fr))` — 3–4 up on wide, 1 up on mobile.

**Responsive breakpoints:**
- `< 600px`: single-column layouts, stat row 2-up, nav collapses to hamburger or wraps
- `600px–900px`: 2-up grids, sunburst scales to container width
- `> 900px`: full layout

---

## Elevation & Depth

Three levels:

| Level | Use | Shadow |
|---|---|---|
| 0 | Flat — toc-strip, banner, inline panels | none |
| 1 | Cards at rest (index-card, stat-card, chart-panel) | `0 2px 8px rgba(42,31,14,0.15)` |
| 2 | Cards on hover, dropdowns, tooltips | `0 4px 14px rgba(42,31,14,0.22)` |

Sticky nav uses `backdrop-filter: blur(4px)` — not a box-shadow.

Sunburst drill tooltip: `0 4px 12px rgba(0,0,0,0.3)`.

Dark mode shadows shift alpha to `rgba(0,0,0,0.4)` / `rgba(0,0,0,0.55)` to compensate for darker backgrounds.

---

## Shapes

**Border radius:**
- `3px` — inline code, small badges
- `4px` — epic chips, pills, toc-strip, progress bars, banner
- `6px` — cards (index-card, stat-card, chart-panel, now-next-card), code blocks, tooltip panels
- `999px` — pill tags with `border-radius: 999px` (fully rounded)

**No sharp `0px` radius** anywhere in the product — the antiquarian warmth extends to gentle rounding throughout.

**Borders:**
- 1 px solid `border` color on cards at rest
- Left accent border: `3px solid gold-light` on banners and alert callouts
- Sunburst drill ring: `2px` ring in `gold-light` on selected segment (SVG `stroke`, not CSS border)

---

## Components

### Sticky Navigation

Full-bleed dark bar, `position: sticky; top: 0`. Brand name left-aligned in `gold-light` serif bold. Links right-side flex row. Active link: `color: #f5c842`, `border-bottom: 2px solid currentColor`. Hover: same color as active. Theme toggle icon (sun/moon) rightmost, `24px`, no label on narrow viewports.

On `< 600px`: brand remains, links collapse behind a hamburger icon. Drawer slides in from right, `backdrop-filter: blur(8px)`.

### Stat Cards

4-up grid row. Each card: `warm-white` bg, `1px solid border`, `border-radius: 6px`, elevation-1. On hover: elevation-2, `translateY(-2px)`, `border-color: rust-light`. Contains: large number (`1.8rem` serif rust), label (`0.78rem` mono ink-light), optional sub-line.

**Tooltip trigger:** `<abbr>` or `data-tooltip` attribute on the label. Tooltip explains what the metric counts. Appears on hover with 120 ms delay.

### Interactive Sunburst

SVG-based radial chart. Three rings: outer (task completion fill), middle (story segments), inner (epic arcs).

**Hover state:** Segment brightens (`opacity: 1` vs rest at `0.85`), cursor `pointer`, tooltip appears within 120 ms — content: epic/story/task name + status + count.

**Drill interaction:**
1. Click an epic segment → sunburst re-renders showing only that epic's stories. Breadcrumb above: `All Epics > Epic N: Title`. Stat row updates to scoped counts.
2. Click a story segment → shows task breakdown ring. Breadcrumb: `All Epics > Epic N > Story N.N: Title`.
3. Click a leaf task → side panel or tooltip shows task text + optional source-file link.
4. Click center or breadcrumb to navigate up.

**Drill animation:** 200 ms ease-in-out. Selected segment zooms to fill inner ring; siblings re-arrange. No layout shift outside the SVG container.

**Keyboard:** Tab through segments, Enter to drill, Escape to go up. `aria-label` on every segment path.

### Progress Bars

Track: `parchment-dark`, `8px` tall, `border-radius: 999px`. Fill: status color (moss for complete, gold for partial). Animate in on page load with `width` transition `0.6s cubic-bezier(0.4,0,0.2,1)`. Tooltip on hover: raw fraction (e.g. "14 of 22 epics drafted").

### Status Pills / Badges

`border-radius: 999px`, mono `0.72rem`, `1px` border. Colors per status semantic table. No fill — border + text only (accessible contrast preserved). Superseded: `text-decoration: line-through`.

### Tooltips

Dark popover anchored to trigger element. Arrow pointer. Appear after 120 ms hover; dismiss on mouse-leave after 80 ms. Max width `220px`, wraps gracefully. Never overlap the nav. `role="tooltip"`, `aria-describedby` wiring on trigger.

In dark mode: light tooltip (cream bg, dark text) to create contrast inversion.

### Index Cards

Artifact-type or story/epic cards on grid pages. Rest: elevation-1, `border-radius: 6px`. Hover: elevation-2, `translateY(-2px)`, `border-color: rust-light`. Transition: `border-color 0.2s, box-shadow 0.2s, transform 0.15s`. Title in `rust`, description in `ink-light`, path in mono `0.68rem parchment-deep`.

### Now & Next Cards

Two-card row on dashboard. Each: full-height flex card with left accent border (color = status). Kicker line (`0.72rem` mono, status color), title (`1rem` serif). Hover: elevation-2 + lift.

### Code Blocks

Dark island: `#241a0d` bg, `#f0e4c8` text, `border-radius: 6px`, horizontal scroll on overflow. Mermaid blocks rendered via Mermaid.js — output SVG inherits `teal` / `rust` / `gold` for node fills where theme is applied.

### Theme Toggle

Icon button (sun ↔ moon), rightmost in nav. No label. `aria-label="Toggle dark mode"`. Stores preference in `localStorage` as `"dark"` or `"light"`. Reads `prefers-color-scheme` as default when no stored value.

### CLI Output (Terminal Surface)

Monochrome-first, color via ANSI codes. Structure:

- **Header line:** `[SpecScribe]` prefix in bold teal, timestamp dim
- **Progress indicator:** spinner or `[=====>   ]` bar with percentage during generation
- **Success line:** `✓` in green, file count + elapsed ms
- **Warning line:** `⚠` in gold, non-fatal notice
- **Error line:** `✗` in rust/red, path + message
- **Watch mode:** persistent status line refreshed in-place (ANSI cursor control); shows last-changed file + regeneration timestamp

Non-interactive mode (CI / piped output): plain text, no spinner, no color (auto-detect TTY).

---

## Do's and Don'ts

**Do:**
- Use serif body text — it is the warmth signature; do not swap to sans-serif for "modern" reasons
- Animate progress bars and sunburst transitions to signal that data is live and current
- Keep tooltip copy short — one line preferred, two max
- Use `parchment` / `warm-white` as backgrounds, not pure white
- Apply status semantic colors consistently — do not invent new status colors
- Preserve the dark nav bar even in dark mode (it goes darker, not lighter)
- Apply `prefers-reduced-motion` media query to disable or halve all transitions

**Don't:**
- Use blue as an accent — this is an antiquarian palette; blue reads as foreign
- Add drop-shadows heavier than elevation-2 values
- Use all-caps for body text or headings (all-caps is reserved for section labels and kicker text only)
- Introduce flat cards without any border — the warm backgrounds need the `1px border` to maintain separation
- Render the sunburst as a static image — interactivity is the product's demo hook
- Show error states with red fills on large surfaces — use rust accent + border only
