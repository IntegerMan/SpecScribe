---
title: SpecScribe Experience
status: final
created: 2026-07-05
updated: 2026-07-05
project: SpecScribe
design-ref: DESIGN.md
---

# SpecScribe Experience

## Foundation

**Product:** SpecScribe — a static-site generator and project-status portal for spec-driven development repositories.

**Form factors (primary → tertiary):**

| Surface | Form factor | Context |
|---|---|---|
| Generated Portal | Responsive web (static HTML/CSS/JS) | Browser, offline-capable; conference demos, shared links, local dev |
| VS Code WebView | Embedded web panel (VS Code API) | VS Code IDE, read-only v1; adapts to VS Code theme variables |
| CLI | Terminal (interactive + non-interactive) | One-shot generate and live watch mode |

**Visual identity reference:** `DESIGN.md` owns all color tokens, type scales, spacing, and component visual specs. This document specifies behavior, states, interactions, IA, and accessibility.

**UI system:** No external component framework. Custom CSS with tokens defined in `DESIGN.md`. VS Code WebView uses `--vscode-*` variables for chrome chrome, layering SpecScribe accent tokens on top. [ASSUMPTION: VS Code WebView inherits `--vscode-editor-background` and `--vscode-foreground` as base; SpecScribe injects a `<style>` block mapping its tokens to VS Code variables where available.]

---

## Information Architecture

### Generated Portal

Six top-level surfaces, all reachable from the sticky nav:

| Surface | URL pattern | Content |
|---|---|---|
| Dashboard (Home) | `index.html` | Stat row, sunburst, now-next, progress summary |
| Epics & Stories | `epics.html` | Epic overview chips + full story index |
| Requirements | `requirements.html` | Requirement list grouped by functional area |
| ADRs | `adrs/index.html` → `adrs/{slug}.html` | ADR index + individual decision records |
| Epic/Story detail | `epics/{slug}.html` | Story spec, task list, requirement refs |
| README / Source doc | `README.html`, etc. | Rendered source markdown documents |

**Missing-section degradation:** If a section has no source artifacts (e.g. no ADRs), its nav link is omitted and the index dashboard card is hidden — no dead-end pages.

**Depth model:** Max 2 levels of nav hierarchy. Epic index → story detail. ADR index → ADR detail. No deeper nesting in v1.

### VS Code WebView

Mirrors Dashboard and Epics surfaces only in v1. No ADR detail, no README rendering. Accessed from VS Code command palette: `SpecScribe: Show Project Status`.

### CLI

No IA — linear command surface. See Key Flows.

---

## Voice and Tone

**Register:** Informative and precise. SpecScribe is a clarity tool; copy must model that clarity.

**Active voice.** "14 of 22 epics drafted" not "22 epics exist, of which 14 are drafted."

**Numbers over adjectives.** Show the fraction; skip "most" or "many."

**No jargon inflation.** Call things what they are: "Stories", "Tasks", "Epics". Never "items", "artifacts", "entities" in UI copy.

**Error messages:** State what happened + what the user can do. "Could not parse `epics.md` — check for unclosed frontmatter block." Not "An error occurred."

**Watch mode feedback:** Present tense, brief. "Rebuilt in 240 ms." "Watching 3 source paths."

**Tooltip copy:** One clause, no period. "14 of 22 epics have a drafted story list." Max 12 words.

---

## Component Patterns (Behavioral)

### Sticky Navigation

**Render rule:** Always present on every generated page. On scroll, `position: sticky; top: 0` keeps it in viewport. Active page link marked with `aria-current="page"` and visual indicator (see `DESIGN.md.components.nav`).

**Theme toggle:** Rightmost nav item. On click: toggles `data-theme` attribute on `<html>` between `"light"` and `"dark"`, persists to `localStorage["specscribe-theme"]`. On page load: reads `localStorage`; falls back to `prefers-color-scheme`. Transition: `color-scheme` CSS property changes; no JS-class thrash.

**Mobile (< 600px):** Links collapse behind hamburger icon. Tap hamburger → links expand in a drawer overlay (right-anchored, `backdrop-filter: blur(8px)`). Tap outside or hamburger again → close. Focus trap active while drawer is open.

### Dashboard Stat Row

Four stat cards in a responsive grid. Cards are **not interactive links** — they are informational read-outs with tooltips. Tooltip appears on hover/focus (120 ms delay) explaining the metric. Tab-navigable via `tabindex="0"` on the card container. `role="region"` with descriptive `aria-label`.

**Data freshness indicator:** Small monospace timestamp below the stat row: "Generated 2026-07-05 · 12:43". In watch mode, this updates in-place on every rebuild (JS `fetch` of a `meta.json` sidecar, injected without full page reload).

### Interactive Sunburst

The hero component. See `DESIGN.md.components.sunburst` for visual spec.

**States:**

| State | Description |
|---|---|
| Default | Full project view — all epics as inner arc segments, stories as middle ring, task fill as outer ring |
| Hovered segment | Segment at full opacity; others dimmed to `0.85`; tooltip visible |
| Drilled — epic | Sunburst re-renders showing one epic's stories. Breadcrumb updates. Stat row scopes to this epic |
| Drilled — story | Sunburst shows story's task breakdown ring. Breadcrumb includes story. |
| Leaf — task | Click shows task detail in a side panel (right of sunburst on wide, below on narrow). Source file link if available |
| Empty | "No data yet — run `specscribe generate`" centered placeholder with a ghost-ring outline |

**Drill behavior:**
- Drill is client-side state only (no page navigation). URL hash updated (`#epic-3`, `#epic-3-story-2`) so back button and deep-link work.
- Transition: 200 ms ease-in-out. Selected segment expands to fill inner ring; sibling segments fan around the perimeter.
- Breadcrumb trail renders above the sunburst on drill. Each crumb is a clickable link (including "All Epics" root).
- On mobile, sunburst scales to fill container width; touch-tap equivalent of hover shows tooltip on first tap, drills on second tap.

**Keyboard:**
- Tab moves focus through segments in document order (inner ring first, then middle, then outer).
- Enter/Space on a segment drills in. Escape navigates up one level.
- `aria-label` on each `<path>`: "{Epic/Story/Task name} — {status}, {n} {children}".
- `role="group"` on each ring with `aria-label="Epic level"` / `"Story level"` / `"Task level"`.

### Progress Bars

Animate in on first paint (CSS `width` transition from `0` to target value). Triggered by `IntersectionObserver` — only animates when bar enters viewport. Re-animate on drill scope change (sunburst drill updates the progress bars to scoped data).

Tooltip on hover: exact fraction text (e.g. "76 of 84 tasks complete across 2 stories").

### Now & Next Cards

Two cards: current focus (story/epic in-development or in-review) + next action (next epic to draft or story to implement). Derived from artifact status fields. If nothing is in-progress, show the next unstarted item.

**Missing state:** "Nothing in progress. Check your epics list to pick up work." — renders as a subtle banner, not an error state.

**Clickable:** Whole card is an `<a>` wrapping the content. No nested interactive elements inside.

### Index Cards (Artifact grid)

Used on: epic overview, requirements index, ADR index, source doc list. Cards are `<a>` elements — entire surface is the click target. Focus ring uses `outline: 2px solid {teal}, outline-offset: 2px`.

Keyboard: Tab through grid in DOM order. No arrow-key navigation required (grid is not a composite widget).

### Story / Epic Detail Pages

**Kicker row:** `{Epic N}` or `{Story N.N}` in mono gold above the H1 title. Status pill inline in the kicker row.

**Task list:** GitHub-flavored checkbox rendering. Completed tasks styled `text-decoration: line-through; color: ink-light`. Counts summarized above the list ("76 of 84 tasks complete").

**Requirement backlinks:** Recognized FR-IDs in body text auto-linkified to requirement detail pages. Broken IDs rendered as plain text with a subtle `⚠` icon.

**Source link:** If the page was generated from a file, a "View source" link in the footer bar pointing to the raw markdown file (relative path).

---

## State Patterns

### Page Load

1. HTML renders immediately (no JS required for initial paint).
2. CSS loads synchronously — layout and colors are present before JS.
3. JS initializes: reads theme preference, applies `data-theme`, wires tooltip listeners, initializes sunburst, starts progress bar animations.
4. Watch mode: JS polls `meta.json` every 5 seconds; on change, re-fetches and patches stat row + timestamp.

### Generation States (Portal content)

| State | Indicator |
|---|---|
| Fresh / current | Normal rendering |
| Stale (watch mode, file changed) | Subtle pulsing border on stat row while rebuild runs (< 1 s typical); resolves on next `meta.json` update |
| Empty project | Dashboard shows placeholder with "Get started" instructions |
| Parse error (non-fatal) | Banner on relevant section: "Some artifacts could not be parsed — see console output" |
| No git history | Git pulse card shows "—" with tooltip "Run in a git repository to enable commit stats" |

### Sunburst Drill State

Managed as client-side state. Serialized to URL hash for deep-linking and browser back/forward navigation. Hash format: `#epic-{n}` or `#epic-{n}-story-{m}` or `#epic-{n}-story-{m}-task-{k}`.

### VS Code WebView State

Controlled by the extension host. On panel open, extension injects current artifact data as JSON via `postMessage`. WebView JS renders from that data. On artifact file save (watched by extension), host sends updated data; WebView re-renders in-place without flicker.

---

## Interaction Primitives

| Primitive | Trigger | Behavior |
|---|---|---|
| Hover tooltip | `mouseenter` (120 ms delay) | Show tooltip anchored to element; dismiss on `mouseleave` (80 ms) |
| Focus tooltip | `focus` | Show tooltip immediately; dismiss on `blur` |
| Card lift | `mouseenter` / `:hover` | `translateY(-2px)` + elevation-2 shadow; 150 ms ease-out |
| Drill | Click / Enter on sunburst segment | Animate to drilled view; update breadcrumb + hash |
| Drill up | Click breadcrumb crumb / Escape | Animate back to parent view |
| Theme toggle | Click toggle icon | Swap `data-theme`; persist to localStorage |
| Progress animate | `IntersectionObserver` entry | Animate bar width from 0 to target over 600 ms |
| Mobile nav open | Tap hamburger | Slide-in drawer; focus trap |
| Mobile nav close | Tap outside / hamburger / Escape | Dismiss drawer; return focus to trigger |
| Watch refresh | `meta.json` poll hit | Patch stat row text in-place; no full reload |

**Touch equivalents:**
- Hover tooltip → first tap shows tooltip; second tap on same element triggers click action (drill)
- Card hover → no equivalent; cards are full-surface tap targets

---

## Accessibility Floor

Behavioral requirements (visual contrast lives in `DESIGN.md`):

**Keyboard navigation:**
- All interactive elements reachable by Tab in logical DOM order.
- Sunburst segments Tab-navigable, Enter/Space to drill, Escape to ascend.
- Modal/drawer focus trapped while open; focus returns to trigger on close.
- Skip-to-content link as first focusable element on every page: `<a class="skip-link" href="#main-content">Skip to content</a>` (visually hidden until focused).

**Screen reader:**
- Sunburst SVG: each segment has `aria-label` with name, status, and count. Rings have `role="group"` with descriptive `aria-label`.
- Stat cards: `role="region"`, `aria-label="Project statistics"`. Each card value has an `aria-label` combining number and label text.
- Progress bars: `role="progressbar"`, `aria-valuenow`, `aria-valuemin="0"`, `aria-valuemax="100"`, `aria-label` matching the visible label.
- Tooltips: `role="tooltip"`, `id` referenced via `aria-describedby` on trigger.
- Nav: `role="navigation"`, `aria-label="Site navigation"`. Active link has `aria-current="page"`.
- Theme toggle: `aria-label="Switch to dark mode"` / `"Switch to light mode"` (dynamic).

**Motion:**
```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    transition-duration: 0.01ms !important;
    animation-duration: 0.01ms !important;
  }
}
```
Sunburst drill in reduced-motion: instant swap, no animated transition.

**Semantic HTML:**
- `<nav>`, `<main>`, `<header>`, `<footer>`, `<section>`, `<article>` used correctly.
- Heading hierarchy: one `<h1>` per page; no skipped levels.
- Tables use `<th scope="col/row">` and `<caption>` where meaningful.

**Color independence:** Status is never communicated by color alone — always paired with a text label or icon.

---

## Key Flows

### UJ-1 · Matt checks project health before a coding session

**Protagonist:** Matt — solo open-source maintainer, spec-driven workflow, frequent coder. Tools: browser + VS Code.

1. Matt runs `specscribe watch` in the project root. Terminal confirms: "Watching 3 source paths. Generated in 180 ms. Open `docs/live/index.html`."
2. He opens `index.html` in his browser. The sticky nav loads immediately; the stat row appears. Progress bars animate in.
3. His eye goes to the sunburst — the gold "in-review" wedge catches attention. He hovers it: tooltip shows "Story 1.2 · Render a Tiled Map — 35 of 43 tasks done."
4. He clicks the wedge. The sunburst drills into Epic 1. The breadcrumb reads "All Epics › Epic 1: World Rendering." Stat row scopes to this epic.
5. He clicks "Story 1.2" in the middle ring. Task breakdown fills the outer ring. A side panel lists tasks with checkboxes. One task links to `GameMap.cs`.
6. **Climax:** In under 60 seconds, Matt knows exactly what is blocked, what is next, and which file to open. He clicks the source link and VS Code opens the file.
7. He saves a planning file. The stat row pulses once, then updates in-place. No reload needed.

### UJ-2 · A new contributor evaluates the repo before opening a PR

**Protagonist:** Priya — contributor, no prior SpecScribe familiarity, arrives from a GitHub link.

1. Priya opens the shared `index.html` URL. The sunburst immediately signals "this project is organized" — she sees the ring structure and status colors.
2. She reads the stat row: "14/22 Epics drafted, 102 Stories defined." She understands scope without reading a single doc.
3. She clicks "Epics" in the nav. The epic overview chips load — she scans titles, spots Epic 3: NPC Presence. She clicks the chip.
4. The Epic 3 detail page loads. She reads the story list with status pills. Story 3.4 is "drafted." She opens it.
5. **Climax:** The story detail page shows the requirement cross-links. Priya follows FR-9 to the requirements page, reads the spec, and understands the scope of what she'd be contributing to. No raw markdown file opened.

### UJ-3 · Matt shares progress externally

**Protagonist:** Matt — conference talk prep or blog post.

1. Matt runs `specscribe generate --output docs/live`. Generation completes: "Generated 48 pages in 320 ms."
2. He opens `index.html` for a final check. The sunburst looks sharp — 14 green wedges, 8 amber.
3. He copies the `docs/live/` folder to a static host (GitHub Pages or a USB drive for offline demo).
4. **Climax:** At the conference, he opens the page on a projector. The sunburst loads. He hovers a segment — tooltip appears. He clicks to drill. The audience sees the project structure unfold in three clicks. No explanation needed. The design carries the message.

---

## Responsive & Platform

### Generated Portal — Responsive

| Breakpoint | Changes |
|---|---|
| `< 600px` (mobile) | Single-column layouts; stat grid 2-up; nav collapses to hamburger; sunburst scales to container width; touch-tap = hover then drill |
| `600–900px` (tablet) | 2-up grids; sunburst + detail panel stack vertically |
| `> 900px` (desktop) | Full layout; sunburst + side panel side-by-side; 4-up stat grid |

Sunburst always renders at SVG `viewBox="0 0 420 420"` with `width="100%"` on the container — scales cleanly at any size.

### VS Code WebView

Inherits VS Code editor chrome (panel chrome, title bar, activity bar) — SpecScribe does not replicate these. SpecScribe's rendered content fills the webview panel.

**Behavioral delta from portal:**
- No sticky nav (VS Code provides the panel tab and title bar)
- No theme toggle (respects VS Code's own `data-vscode-theme-kind` attribute — `vscode-light`, `vscode-dark`, `vscode-high-contrast`)
- No page navigation — all content rendered in a single scrollable panel with internal anchor links
- Stat row and sunburst present; drill works identically via client-side state
- "View source" links open the file in the VS Code editor (via `vscode.open` URI), not a browser tab
- Watch mode: extension host pushes data; no polling needed

**Token mapping:** SpecScribe maps its color tokens to VS Code variables:
```
--ss-bg: var(--vscode-editor-background);
--ss-surface: var(--vscode-editorWidget-background, var(--ss-bg));
--ss-fg: var(--vscode-editor-foreground);
--ss-border: var(--vscode-widget-border, rgba(255,255,255,0.1));
```
Rust/gold/moss accent tokens remain fixed — they are SpecScribe brand, not inherited from VS Code theme.

### CLI Surface

**Interactive mode (TTY detected):**
- ANSI color support for status lines (see `DESIGN.md.components.cli-output`)
- In-place line refresh for watch mode progress (cursor-up + overwrite)
- Spinner during generation (`⣾⣽⣻⢿⡿⣟⣯⣷`)
- Summary line on completion: `✓ Generated 48 pages in 320 ms → docs/live/`
- Warning lines prefixed `⚠ ` in gold
- Error lines prefixed `✗ ` in rust

**Non-interactive mode (piped / CI):**
- Plain text output, no ANSI, no spinner
- Auto-detected via `Console.IsOutputRedirected` / TTY check
- Machine-parseable summary line: `SPECSCRIBE OK pages=48 ms=320 out=docs/live/`
- Errors to stderr, exit code 1 on fatal failure

**Watch mode UX:**
```
SpecScribe  watching 3 paths
  last build  2026-07-05 12:43:02  →  48 pages  ·  312 ms
  changed     epics.md  (12:43:08)
  rebuilding  ⣾
  rebuilt     48 pages  ·  188 ms
```
Status block re-renders in-place. Ctrl+C exits cleanly with "Stopped watching."

**Help text:** `--help` output styled with section headers (uppercase, bold where terminal supports it), aligned flags column, examples section at the bottom.
