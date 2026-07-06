---
baseline_commit: 53432af8b8410d16eacd7dda01025098153d6067
---

# Story 1.5: Dashboard Insight Polish and Visual Truthfulness

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a stakeholder scanning the dashboard,
I want charts and stats that are visually polished and tell the truth,
so that I can trust what I see and read it at a glance.

## Acceptance Criteria

1. **Given** the dashboard renders stats and charts
   **When** I view any panel
   **Then** status is shown in one consistent color vocabulary with on-brand, instant tooltips reachable by keyboard, focus, and touch
   **And** no chart overstates progress (epic status reflects the story roll-up, task counts are clearly scoped, and future dates are not shown as zero-activity).

2. **Given** I am looking for what to do next
   **When** the dashboard loads
   **Then** the most active and next work is surfaced ahead of secondary link grids
   **And** key next-step commands can be copied in a single action.

> **Origin & scope:** This story carries the *polish and truthfulness* half of the Story 1.4 UX review
> (`docs/Story1_4_UX_Observations.md`), split out from the accessibility/motion baseline (Story 1.4). Each
> item is traced to its observation ID (`[UXO …]`). **Sequence this after Story 1.4** — it extends the same
> focus-visible / reduced-motion / segment-`aria-label` seams 1.4 establishes. The *accessibility* items
> (whole-chart aria-labels, contrast, micro-text) live in 1.4; **new work-type representation and sunburst
> task-accuracy (UXO A6/E4)** live in Story 2.1 — do not re-implement those here.

## Tasks / Subtasks

- [x] Task 1: Data-correctness quick fix (AC: #1 trust) `[UXO A2]`
  - [x] Pluralize count-bearing stat labels: the hardcoded `"Commits"` reads "1 Commits". Pluralize `Commit`/`Commits` and audit every `Charts.StatCard` caller in `HtmlTemplater.AppendDashboard` for the same defect [Source: `src/SpecScribe/HtmlTemplater.cs:124-131`]
  - [x] (Verify-only) `[UXO A1]` The live-site "1 commit / empty heatmap" bug was the CI shallow clone; `fetch-depth: 0` is already present in `.github/workflows/publish-docs-live-pages.yml:34`. Confirm a local full-history generation shows real counts so the truthfulness work is validated against correct data
- [x] Task 2: Unify status vocabulary and tokenize one color per lifecycle stage (AC: #1) `[UXO B1–B5]`
  - [x] `[UXO B2/B3]` Introduce one CSS variable per lifecycle stage (`--status-pending`, `--status-drafted`, `--status-ready`, `--status-active`, `--status-review`, `--status-done`) and route every chart/legend/badge/accent through it. Today the sunburst and donuts disagree (sunburst drafted `#e8d9a8` vs donut `.ready`/`.drafted` both `--gold-light`; sunburst pending `#b8b2a8` vs donut pending `--parchment-deep`). One stage → one token everywhere; keep `StatusStyles` as the single status→stage source [Source: `src/SpecScribe/assets/specscribe.css:724-731`, `:1062-1069`; `src/SpecScribe/StatusStyles.cs`]
  - [x] `[UXO B1]` Adopt the sunburst legend's six-stage lifecycle (Pending → Drafted → Ready for dev → In development → In review → Done) as the single vocabulary in all panels; alias requirements "Planned" → "Pending" in labels
  - [x] `[UXO B4]` Suppress zero-count legend rows (e.g. requirements donuts listing "Done (0), Ready (0), Deferred (0)") — render only non-zero entries (or collapse/dim zeros)
  - [x] `[UXO B5]` Accent the "Explore Key Views" quick-link cards by artifact family (planning=gold, architecture=teal, epics/stories=moss, requirements=rust) so color literacy carries across the site
- [x] Task 3: On-brand instant tooltip system replacing native `<title>`-only behavior (AC: #1) `[UXO C1 (flagship), C2, C4, D5]`
  - [x] **Guardrail note:** Story 1.4 forbids new JS; **this story deliberately introduces exactly one small, dependency-free, progressive-enhancement script** for tooltips (and the Task 6 copy buttons). This is NOT the drill engine the top-level guardrail targets. Native `<title>` + the 1.4 segment `aria-label`s remain the no-JS / screen-reader fallback, so output stays static-host-safe
  - [x] `[UXO C2]` HTML elements (stat cards, legend items) get a **CSS-only** tooltip via `data-tooltip` + `::after` (instant, focus-accessible, on-brand parchment card) — no JS. Give each stat card a definition tooltip (UX-DR4): "3 active days" (since first commit), "4 with a task plan" (stories with a BMad task checklist), "Tasks done" (checkbox items across planned stories)
  - [x] `[UXO C1/D5]` SVG chart segments and heatmap cells get an on-brand tooltip via a ~30-line shared vanilla script (`data-*` on the segments; the script positions a single styled tooltip element near the pointer/focus). Keep `<title>`+`aria-label` present as fallback
  - [x] `[UXO C4]` Touch: the script shows the tooltip on first tap and navigates on second (or long-press), so touch users finally get chart detail
  - [x] Deliver the script as a self-contained embedded asset alongside the CSS (add `ForgeOptions.ScriptName`; mirror the CSS asset-copy seam in `SiteGenerator`), linked once from the shared shell. No third-party JS
- [x] Task 4: Consistent interaction grammar and entrance animations (AC: #1, #2 companion) `[UXO D1–D4, D2]`
  - [x] `[UXO D1]` Define one interaction grammar and apply it uniformly: *interactive → lift + accent-border + shadow; data-hover → highlight + tooltip; static → nothing.* Give now-next cards the lift they lack; give donut segments a stroke-width bump on hover
  - [x] `[UXO D3]` Invert the sunburst hover: today `.sb-seg:hover` fades the hovered segment to 0.75 (reads as dimming what you point at). Instead dim siblings and emphasize the hovered/focused segment (`.sunburst:hover .sb-seg { opacity:.5 } .sb-seg:hover { opacity:1 }` + the `--gold-light` stroke) — CSS only, matched to the 1.4 focus treatment [Source: `src/SpecScribe/assets/specscribe.css:724-725`]
  - [x] `[UXO D4]` Emphasize the "In development" Now card (the single most important answer on the page): subtle background wash / bolder accent; any pulsing must be reduced-motion-gated
  - [x] `[UXO D2]` Add one-time entrance animations gated behind `@media (prefers-reduced-motion: no-preference)` — progress bar width `0 → N%`, donut `stroke-dashoffset` sweep — pure CSS `@keyframes`, no JS. This is the complementary half of Story 1.4's `reduce` block (leave the two media queries side by side)
- [x] Task 5: Chart truthfulness for existing metrics (AC: #1) `[UXO A3, A4, A5, E1, E2, E3]`
  - [x] `[UXO A3]` Fix the Epic Status donut so it stops contradicting the sunburst: derive its segments from `StatusStyles.ForEpic` (the same story-roll-up the sunburst uses: done/active/drafted/pending), not the binary `ProgressModel.EpicsDrafted/EpicsPending`. This makes the donut genuinely multi-segment (also resolves E2 for that donut) [Source: `src/SpecScribe/HtmlTemplater.cs:163-172`, `src/SpecScribe/StatusStyles.cs:27-35`]
  - [x] `[UXO A5]` Reframe "64/103 Tasks done" so the big number doesn't imply 62% of the project is done (the denominator only covers 4 of 18 stories): make the number "64/103 planned tasks" and/or pair with a second stat ("4/18 stories planned") [Source: `src/SpecScribe/HtmlTemplater.cs:126-128`]
  - [x] `[UXO A4]` Mute future days in the commit heatmap (cells after the generation date) — no fill, no tooltip — so they don't read as real zero-commit days [Source: `src/SpecScribe/Charts.cs:431-446`]
  - [x] `[UXO E1]` Let the heatmap scale up (`width:100%; max-width:420px; height:auto`; render 12–16 weeks) and add a headline ("N commits · M active days · last commit {date}"). It is the primary "how has the work gone" visual and is currently the smallest thing on the page [Source: `src/SpecScribe/Charts.cs:401-458`]
  - [x] `[UXO E3]` Donut center numbers should read as progress, not scores: show a `done/total` fraction or percentage (e.g. "4/14") rather than a bare total [Source: `src/SpecScribe/Charts.cs:63`]
  - [x] `[UXO E2]` For requirements, prefer compact stacked bars (one row per group) over two single-value donuts where a group has <2 non-zero segments — denser and comparable (coordinate with B4 zero-row suppression)
  - [x] Note: the **"Progress by Epic" mosaic showing delivery status (UXO A6)** and the **unplanned-story placeholder arc (UXO E4)** are **Story 2.1**, not here — they are sunburst/task-representation accuracy. Do not implement them in this story
- [x] Task 6: Dashboard information architecture and "what's next" storytelling (AC: #2) `[UXO F1, F2, F3]`
  - [x] `[UXO F1]` Reorder the dashboard so the most valuable panel isn't buried: stats → Now & Next → Project at a Glance (sunburst) → progress panels. Slim "Explore Key Views" (it duplicates the top nav and the bottom index sections) to a compact single row of pills [Source: `src/SpecScribe/HtmlTemplater.cs:119-192`]
  - [x] `[UXO F2]` Add a per-command copy button to the "Next Steps" commands (`/bmad-dev-story 1.4` is exactly what the user wants on their clipboard) — same tiny progressive-enhancement JS budget as Task 3, static-safe. Fix the grammar: "implements it per its plan" → "implement it per its plan" [Source: `src/SpecScribe/BmadCommands.cs`]
  - [x] `[UXO F3]` Add a recency signal above the fold: "last commit N days ago" in the Commits stat sub-line and/or an "updated today" chip on active Now & Next cards
- [x] Task 7: Head and meta polish (AC: #1 trust) `[UXO G1, G2]`
  - [x] `[UXO G1]` Emit a small inline-SVG favicon (parchment quill/spark, gold on dark) from the generator so tabs aren't the browser default and `/favicon.ico` stops 404-ing
  - [x] `[UXO G2]` Home page `<title>` should be "SpecScribe — Project Dashboard" (sub-pages are already suffixed); add `<meta name="description">` and minimal Open Graph tags (title/description/type) so shared links don't render bare [Source: `src/SpecScribe/PathUtil.cs:26-36`, `src/SpecScribe/HtmlTemplater.cs:69`]
- [x] Task 8: Regression and new test coverage (AC: #1, #2)
  - [x] Extend `HtmlTemplaterTests`: stat "Commit(s)" pluralizes correctly (1 vs many); favicon + home `<title>`/description present; dashboard order (Now & Next before Explore Key Views); quick-link family accents present
  - [x] Add `Charts` tests: Epic-Status donut segments reflect `StatusStyles.ForEpic` roll-up (A3); heatmap mutes future days (A4); donut center shows a fraction (E3); per-status token classes used
  - [x] Add stylesheet-content assertions: `@media (prefers-reduced-motion: no-preference)` entrance block exists; per-status color tokens exist; on-brand `data-tooltip`/`::after` styles exist
  - [x] Prefer generation-/render-level assertions over new public API
- [x] Task 9: End-to-end validation with a real generation pass (AC: #1, #2) `[UXO F4]`
  - [x] Run the focused test filter, then a real generation pass
  - [x] Manually verify: one consistent status color across all charts; on-brand tooltip appears instantly on hover/focus/touch; entrance animations play once and are removed under reduce-motion; charts are truthful (multi-segment epic donut, scaled heatmap with muted future days, fraction donut centers, scoped task stat); Now & Next is above the link grid; Next Steps commands copy in one click
  - [x] `[UXO F4]` Definition-of-polish pass: for each dashboard panel, confirm it answers one of the four questions — where things **stand**, what's **left**, what's **next**, how the work has **gone** (the dashboard is now the demo of the dashboard)

## Developer Context Section

### Epic Context and Business Value

Epic 1 delivers a polished, immediately-useful portal for current BMad projects. Story 1.4 makes the dashboard accessible and motion-respectful; **Story 1.5 makes it polished and truthful** — one consistent status-color language, on-brand instant tooltips, charts that don't overstate progress, and a "what's next" narrative that surfaces the most valuable panel first. This is the story where the dashboard becomes the *proof* of the product (per the UX review, "the dashboard is the demo of the dashboard"). Advances the epic's High-Clarity Portal goal directly (FR2 first-class BMad support, FR5 coherent dashboards, UX-DR4/17/18).

### Story Foundation Extract

- Primary concern: visual truthfulness (no chart overstates progress), one status-color vocabulary, an on-brand tooltip layer, and dashboard IA that leads with "what's active / what's next."
- User outcome: a stakeholder trusts every number and reads the dashboard's story in seconds; a user can copy the exact next command in one click.
- Success boundary: built on the static-HTML + pure-SVG-links substrate, with **one** small progressive-enhancement script (tooltips + copy) as the sole sanctioned JS addition, with a `<title>`/`aria-label` no-JS fallback.
- Regression boundary: Story 1.4's accessibility (focus, aria-labels, reduced-motion, skip link/landmark/progressbar) is preserved and *extended*, never undone; 1.1/1.2/1.3 behavior preserved; antiquarian visual identity preserved.

### Current Implementation Reality (READ THIS FIRST — shared with Story 1.4)

- **Charts are pure SVG + real `<a>` links, no JS.** Do NOT build the UX-spec client-side drill engine (UX-DR5/6/7). The one sanctioned JS addition is the small tooltip/copy script. [Source: `src/SpecScribe/Charts.cs:6-8`, `:92-273`]
- **Status color tokens are inconsistent across charts** — sunburst uses literal hex per stage (`.sb-drafted #e8d9a8`, `.sb-pending #b8b2a8`); donuts collapse `ready`+`drafted` to `--gold-light` and pending to `--parchment-deep`. [UXO B2/B3] [Source: `src/SpecScribe/assets/specscribe.css:724-731`, `:1062-1069`]
- **`StatusStyles` is already the single source of truth** for status→stage (`ForStory`/`ForEpic`/`ForRequirement`), and `ForEpic` already computes the correct multi-stage roll-up — so [UXO A3] is a *wiring* change (feed the Epic Status donut from it), not new logic. [Source: `src/SpecScribe/StatusStyles.cs`]
- **Epic Status donut uses binary `ProgressModel.EpicsDrafted/EpicsPending`**, contradicting the sunburst. [UXO A3] [Source: `src/SpecScribe/HtmlTemplater.cs:163-172`]
- **Task stat over-promises**: "64/103 Tasks done" denominator only covers planned stories; the `stat-sub` caveat is dominated by the big number. [UXO A5] [Source: `src/SpecScribe/HtmlTemplater.cs:126-128`]
- **Heatmap is a fixed-size postage stamp** and renders future days as zero-commit cells. [UXO E1/A4] [Source: `src/SpecScribe/Charts.cs:401-458`]
- **Donut center shows a bare total** that reads like a score. [UXO E3] [Source: `src/SpecScribe/Charts.cs:63`]
- **Every chart tooltip is a native `<title>`** (≈1s delay, OS-default look, no touch) — the hero visual hides its labels behind it. [UXO C1] [Source: `src/SpecScribe/Charts.cs`]
- **No JS asset-delivery path exists** — only `ForgeOptions.StylesheetName = "specscribe.css"`; the tooltip/copy script needs a new embedded asset delivered self-contained the way the CSS is. [Source: `src/SpecScribe/ForgeOptions.cs:29`]
- **Dashboard order buries Now & Next** below a 9-card "Explore Key Views" grid that duplicates the top nav and the bottom index sections. [UXO F1] [Source: `src/SpecScribe/HtmlTemplater.cs:119-192`]
- **"Next Steps" commands can't be copied**; one has a grammar slip. [UXO F2] [Source: `src/SpecScribe/BmadCommands.cs`]
- **No favicon; home `<title>` is bare "SpecScribe"; no description/OG.** [UXO G1/G2] [Source: `src/SpecScribe/PathUtil.cs:26-36`, `src/SpecScribe/HtmlTemplater.cs:69`]
- **Already correct (verify, don't rebuild):** CI full-history fetch ([UXO A1] done); the accessibility floor Story 1.4 adds (focus-visible, segment/whole-chart aria-labels, reduced-motion `reduce` block, skip link, progressbar ARIA) — build the `no-preference` entrance block and tooltips *on top of* those seams. [Source: `.github/workflows/publish-docs-live-pages.yml:34`]

### Scope Boundaries

- **RELAXED (this story) — one small progressive-enhancement script** for tooltips + copy buttons; dependency-free, static-host-safe, degrades to `<title>`+`aria-label`.
- **OUT — JS client-side drill/zoom/URL-hash sunburst** (UX-DR5/6/7); **OUT — dark mode / theme toggle** (UX-DR2/3, no CSS/toggle exists).
- **OUT — velocity/burn-up/deep git analytics** [UXO E5] → Story 3.1; **richer interactive-legend dimming** [UXO C3] and flashy insight visuals → Story 3.5.
- **NOT THIS STORY (Story 2.1):** representing deferred-work + quick-dev artifacts, the "Progress by Epic" delivery mosaic [UXO A6], and the unplanned-story placeholder arc [UXO E4], and inline authoring guidance.
- **NOT THIS STORY (Story 1.4):** focus-visible, whole-chart/segment aria-labels, reduced-motion `reduce` block, skip link/landmark/progressbar ARIA, contrast (H1), micro-text (H2). Depend on them; don't redo them.
- **DEFERRED (backlog):** footer SHA/timezone [UXO A7], `404.html` [UXO G3], print styles [UXO G4].

### Previous Story Intelligence

- Land the **status-token system (Task 2) first** — every later chart/color task routes through it, preventing per-chart color drift (the whole point of `StatusStyles`). Then tooltips (Task 3), interaction/animation (Task 4), truthfulness (Task 5), IA (Task 6), meta (Task 7).
- Keep behavior in central seams (`Charts`, `HtmlTemplater`, `StatusStyles`, `PathUtil`, `BmadCommands`, `specscribe.css`); the new script is one embedded asset, not inline-per-page.
- Prefer generation-/render-level string assertions (`HtmlTemplaterTests` model); avoid new public API just to reach helpers.
- Static-host-safe (GitHub Pages): the tooltip/copy JS is progressive enhancement with a no-JS fallback; all links/anchors relative-correct.
- **Sequence after Story 1.4** (same files) and after/rebased onto in-flight **Story 1.3** (TOC sidebar on the same CSS/templaters). Reconcile the dashboard reorder with 1.3's layout and 1.4's landmark.
- Environment: use `py -3` for BMAD helper scripts on this Windows host.

### Architecture Compliance

- Status→color semantics belong in one shared place (`StatusStyles` + the new tokens), consumed by every chart/badge — this is the codebase's stated "status semantics everywhere" ambition; enforce it rather than adding per-chart colors. [Source: `src/SpecScribe/StatusStyles.cs`, `assets/specscribe.css` ~line 515 comment]
- Keep interaction/semantic meaning host-neutral so the future VS Code webview inherits it; the tooltip data (`data-*`) and status tokens live in the shared output. [Source: `docs/adrs/0002-…md`, `0004-…md`; `epics.md` NFR6]
- Graceful degradation is contractual: truthfulness fixes must keep zero/low-data states safe (empty sunburst, no git, single-category donut) — every `Charts` builder degrades to `.chart-empty`; A4 future-day muting and E2 stacked-bar fallback are degradation-aware. [Source: `src/SpecScribe/Charts.cs:95`,`:206`,`:345`,`:382`]
- The one sanctioned JS addition must stay dependency-free and self-contained in tool packaging (mirror the self-contained stylesheet delivery). [Source: `epics.md` "Keep stylesheet delivery self-contained"]

## Technical Requirements

- One CSS token per lifecycle stage, used by every chart/legend/badge/accent; `StatusStyles` remains the single status→stage source; zero-count legend rows suppressed; quick-links accented by artifact family.
- On-brand tooltip system: CSS-only for HTML elements (`data-tooltip`+`::after`); one small vanilla script for SVG segments/heatmap cells with `<title>`+`aria-label` fallback and touch support. Copy buttons on Next Steps use the same script budget. No third-party JS.
- Entrance animations gated under `@media (prefers-reduced-motion: no-preference)` (complementing Story 1.4's `reduce` block); consistent hover grammar; sunburst sibling-dim matched to the focus treatment; Now-card emphasis (reduced-motion-safe).
- Charts truthful: epic-status donut from `StatusStyles.ForEpic` roll-up; task-stat reframed; heatmap mutes future days, scales up, gains a headline; donut centers show fractions; requirements use stacked bars where single-value.
- Dashboard order: stats → Now & Next → sunburst → progress panels; Explore Key Views slimmed; copy buttons + grammar fix on Next Steps; recency signal above the fold.
- Favicon + home `<title>`/description/OG tags emitted.
- No JS drill engine; no dark-mode/theme system; no velocity/deep-git; host-neutral, static-host-safe. Story 1.4's accessibility must remain intact.

## File Structure Requirements

Primary UPDATE candidates:

- `src/SpecScribe/assets/specscribe.css` — per-status tokens; on-brand `data-tooltip`/`::after` styles; unified hover grammar + sunburst sibling-dim; entrance `@keyframes` under `no-preference`; Now-card emphasis; zero-row legend handling. Preserve 1.4's focus-visible + `reduce` block, token/color system, 1.3 TOC layout.
- `src/SpecScribe/Charts.cs` — per-status token classes; A3 epic-status donut input; A4 future-day muting; E1 heatmap scale/headline; E3 donut center fractions; E2 requirements stacked bars; `data-*` tooltip hooks. Preserve pure-SVG design, `<title>`s + 1.4 aria-labels, empty-state degradation, geometry, hrefs. (A6 mosaic + E4 placeholder arc are Story 2.1 — leave them.)
- `src/SpecScribe/HtmlTemplater.cs` — A2 pluralized stat labels; A5 task-stat reframing; dashboard reordering (F1) + slimmed quick-links with family accents (B5); epic-status donut wiring (A3); requirements stacked-bar option (E2); recency stat-sub (F3); home `<title>`/description/OG (G2). Preserve dashboard composition, `HasMermaid` injection, header/breadcrumb order, 1.4 `<main>` landmark.
- `src/SpecScribe/PathUtil.cs` (`RenderHeadOpen`) — favicon + meta/OG in `<head>`; link the new script asset once. Preserve head/title/stylesheet + 1.4 skip link.
- `src/SpecScribe/BmadCommands.cs` — Next Steps copy buttons (F2) + grammar fix.
- `src/SpecScribe/StatusStyles.cs` — the status→token mapping stays the single source; extend labels for the unified six-stage vocabulary (B1) if needed.
- `src/SpecScribe/ForgeOptions.cs` — add `ScriptName` const; `src/SpecScribe/SiteGenerator.cs` — copy/emit the JS asset self-contained (mirror the CSS asset seam).
- `src/SpecScribe/assets/specscribe.js` (**new**) — the ~30-line dependency-free tooltip + copy-button progressive-enhancement script.

Primary TEST candidates:

- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — pluralization; favicon/title/description; dashboard order; quick-link accents.
- `tests/SpecScribe.Tests/ChartsTests.cs` (new or extend) — epic-status roll-up (A3); future-day muting (A4); donut center fraction (E3); per-status tokens.
- Stylesheet-content assertions — `no-preference` entrance block, per-status tokens, `data-tooltip` styles present.

## Library and Framework Requirements

- Stay on the existing .NET / inline-SVG / CSS stack. The **only** new runtime code is one small hand-written dependency-free JS file (tooltip positioning + clipboard copy) delivered as an embedded asset; no JS framework, no charting/tooltip library, no drill engine, no dark-mode/theming, no `prefers-color-scheme`.
- Clipboard: `navigator.clipboard.writeText` with a `document.execCommand('copy')` fallback for non-secure contexts. Tooltips/tokens/animations are pure CSS; ARIA/`data-*` are static attributes.

## Testing Requirements

- Preserve existing coverage and **Story 1.4's accessibility assertions** (focus-visible, aria-labels, reduced-motion `reduce`, skip link, progressbar) — none must regress.
- Add coverage (see Task 8): pluralization; epic-status roll-up (A3); future-day muting (A4); donut fraction (E3); per-status tokens + `no-preference` entrance + `data-tooltip` CSS present; dashboard order; favicon/title/description.
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~Charts|FullyQualifiedName~SiteNav|FullyQualifiedName~SiteGenerator|FullyQualifiedName~StatusStyles|FullyQualifiedName~Progress"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Manual verification: one consistent status color across charts; instant on-brand tooltip on hover/focus/touch; entrance animation plays once and is gone under reduce-motion; truthful charts; Now & Next surfaced first; one-click command copy. Close with the F4 four-questions pass.

## UX and Accessibility Requirements

- Status never color-only, and now one color per stage — legends/pills/fractions keep the text/shape redundancy while colors unify. [Source: `epics.md` UX-DR17; UXO B1–B4]
- Tooltips on-brand, instant, focus- and touch-accessible, with a `<title>`/`aria-label` no-JS fallback. [Source: `DESIGN.md` Tooltips; UXO C1/C2/C4]
- Motion: entrance animations live only under `prefers-reduced-motion: no-preference`; the `reduce` path (Story 1.4) removes them; no looping; never remove information. [Source: `epics.md` UX-DR18/8; UXO D2]
- Voice/tone: numbers over adjectives; active voice; short tooltip copy (≤12 words) — matches the reframed stats and command guidance. [Source: `EXPERIENCE.md` Voice and Tone]
- Use the established token/color system; new colors/tooltips read on-brand (teal/gold/parchment), never a foreign default. [Source: `DESIGN.md`]

## Reinvention and Regression Guardrails

- Do NOT build a JS drill/zoom/hash sunburst; do NOT add dark mode / theme toggle; do NOT pull in a third-party JS/tooltip/charting library.
- Do NOT regress Story 1.4's accessibility (focus, aria-labels, reduced-motion, skip link/landmark/progressbar) — extend those seams, don't overwrite them.
- Do NOT reintroduce per-chart color drift — one token per lifecycle stage (B2/B3).
- Do NOT let charts lie: never imply the whole project is 62% done (A5); never render future days as zero-commit (A4); make the epic donut reflect the real roll-up (A3).
- Do NOT strip `<title>`s when adding tooltips; keep pointer + non-pointer + no-JS paths.
- Do NOT implement the A6 delivery mosaic or E4 placeholder arc here — those are Story 2.1.
- Preserve zero/low-data graceful degradation and Story 1.1 missing-section nav omission.
- Coordinate shared-CSS/templater edits with in-flight Story 1.3 and just-landed Story 1.4; keep everything host-neutral and static-host-safe.

## Git Intelligence Summary

- Current branch `fix/ci-full-git-history-commit-activity`: `5d5ec99` already lands [UXO A1] (`fetch-depth: 0`) — verify, don't redo. `53432af`/`27106f3` are Story 1.3 in-flight commits (baseline).
- `fb9fb88` (Story 1.2) is the verify-and-harden pattern; `3b0227e` hardened the color-swatch rewriter (a shared post-render pass — CSS/rendering are shared seams; change additively and centrally).
- Generated output is published to GitHub Pages; keep anchors/script static-host-safe.

## Latest Technical Information

- `data-*` + `::after` CSS tooltips, `@media (prefers-reduced-motion: no-preference)`, and the Clipboard API (`navigator.clipboard.writeText`, with `document.execCommand('copy')` fallback for non-secure contexts) are broadly supported in current evergreen browsers — no polyfills, no vendor prefixes. Charts remain dependency-free; the Mermaid CDN is unrelated.

## Project Context Reference

- UX review driving this story: `docs/Story1_4_UX_Observations.md`
- Predecessor (accessibility/motion baseline; shared seams): `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`
- Successor (work-representation + authoring guidance; owns A6/E4): `2-1-accurate-work-representation-and-authoring-guidance` (Epic 2)
- PRD: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`
- Epics: `_bmad-output/planning-artifacts/epics.md` (Story 1.5; UX-DR4/17/18)
- Architecture spine / rendering: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`, `rendering-architecture.md`
- ADR 0002 / 0004: `docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md`, `docs/adrs/0004-cross-surface-interaction-and-theme-contract.md`
- UX design/behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`, `EXPERIENCE.md`
- Key source seams: `src/SpecScribe/Charts.cs`, `HtmlTemplater.cs`, `StatusStyles.cs`, `PathUtil.cs`, `BmadCommands.cs`, `ForgeOptions.cs`, `SiteGenerator.cs`, `assets/specscribe.css`, new `assets/specscribe.js`
- CI: `.github/workflows/publish-docs-live-pages.yml`

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed - comprehensive developer guide created from the Story 1.4 UX review's polish/truthfulness set, split out from the accessibility/motion baseline (Story 1.4).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8

### Debug Log References

- created by splitting the Story 1.4 UX-review scope; this story carries the polish/truthfulness half (Story 1.4 = accessibility/motion; Story 2.1 = work representation + authoring guidance + A6/E4)
- verified [UXO A1] already fixed on this branch and [UXO A3] backed by existing `StatusStyles.ForEpic`
- environment: use `py -3` for BMAD helper scripts on this Windows host
- planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~Charts|FullyQualifiedName~SiteNav|FullyQualifiedName~SiteGenerator|FullyQualifiedName~StatusStyles|FullyQualifiedName~Progress"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Sequence after Story 1.4 (extends its focus-visible / reduced-motion / aria-label seams) and after/rebased onto in-flight Story 1.3.
- Land the status-token system first (Task 2), then tooltips (Task 3), interaction grammar + entrance animations (Task 4), chart truthfulness (Task 5), IA reorder + copy/recency (Task 6), favicon/meta (Task 7).
- Introduce exactly one small progressive-enhancement script (tooltips + copy) with a `<title>`/`aria-label` no-JS fallback; keep drill-engine and dark-mode guardrails intact; leave A6/E4 for Story 2.1.
- Add render-/Charts-/stylesheet-content tests; re-run targeted tests + a real generation pass; close with the F4 four-questions dogfood pass.

### Completion Notes List

- Split from the Story 1.4 UX review (`docs/Story1_4_UX_Observations.md`): this story owns the polish/truthfulness "Pull into 1.4" items (B1–B5 tokens/vocabulary, C1/C2/C4/D5 tooltips, D1–D4/D2 interaction+animation, A2/A3/A4/A5/E1/E2/E3 truthfulness, F1–F3 IA, G1/G2 meta), each traced to a `[UXO …]` id.
- Verified [UXO A1] already fixed (CI full history) and [UXO A3] backed by existing `StatusStyles.ForEpic` (a wiring change, not new logic).
- Explicitly routed A6 (delivery mosaic) and E4 (unplanned-story placeholder arc) to Story 2.1, and the accessibility items (E6/H3/H1/H2) to Story 1.4, to keep clean seams.
- Kept out per the review + guardrails: JS drill engine, dark mode/theme toggle, velocity/deep git (Story 3.1), interactive legends/flashy visuals (Story 3.5), footer SHA/404/print (backlog).
- Coordination flags: sequence after Story 1.4; rebase onto in-flight Story 1.3; reconcile dashboard reorder with 1.3's TOC layout and 1.4's `<main>` landmark.

#### Implementation summary (this session)

- **Task 1 (A2/A1):** Pluralized the count-bearing `Commit(s)` stat label via a now-public `Charts.Plural`; audited all `StatCard` callers. Verified [A1] against a real full-history generation — the heatmap shows 53 commits / real active days, so the truthfulness work validated against correct data.
- **Task 2 (B1–B5):** Added six per-lifecycle-stage CSS tokens (`--status-pending/…/--status-done` + `--status-deferred`) in `:root` and routed every chart fill, legend swatch, and status accent (sunburst, donuts, now-next, epic-chip, req-card, coverage-card, progress-fill) through them — the sunburst and donuts no longer disagree. Adopted the sunburst's six-stage vocabulary everywhere (Epic Status donut now says "In development"/"Stories drafted"; requirements alias "Planned"→"Pending"). Zero-count legend rows suppressed (Epic Status + Requirements). Quick-links accented by artifact family (planning/architecture/epics/requirements).
- **Task 3 (C1/C2/C4/D5):** Introduced exactly one dependency-free progressive-enhancement script (`assets/specscribe.js`) delivered as an embedded asset (new `ForgeOptions.ScriptName`, mirrored the CSS copy seam in `SiteGenerator.EnsureScaffold`, linked once with `defer` from `RenderHeadOpen`). HTML elements get CSS-only `data-tooltip`/`::after` tooltips (stat-card definitions, quick-link pills); SVG segments/heatmap cells get an on-brand tooltip positioned by the script reading their existing `<title>` — native `<title>`+aria-labels remain the no-JS/SR fallback. Touch: first tap shows, second follows.
- **Task 4 (D1–D4/D2):** One interaction grammar — now-next cards get the lift they lacked, donut slices bump stroke on hover; inverted the sunburst hover so it emphasizes (not dims) the hovered/focused segment with the gold focus stroke; the "In development" Now card gets a subtle teal wash + bolder accent; added `@media (prefers-reduced-motion: no-preference)` entrance animations (progress bars sweep 0→N%, donuts/sunburst fade+scale) side-by-side with Story 1.4's `reduce` block.
- **Task 5 (A3/A4/A5/E1/E2/E3):** Epic Status donut now derives from the `StatusStyles.ForEpic` story roll-up (multi-segment, fraction center) instead of binary drafted/pending; task stat reframed to "Planned tasks done" + "N/M stories planned" so it can't read as whole-project %; heatmap mutes future days (no fill/tooltip), scales up to a ~15-week window with a headline; donut centers show a done/total fraction; requirements use a compact stacked bar when a group sits in a single status. Left A6/E4 for Story 2.1.
- **Task 6 (F1–F3):** Reordered the dashboard so stats → Now & Next → Project at a Glance → progress panels lead, with "Explore Key Views" slimmed to a pill row and moved below the substance; added per-command copy buttons to Next Steps (+ grammar fix "implements"→"implement"); added a "last commit N days ago" recency signal to the Commits stat sub-line (chose the honest commit-recency path over a fabricated per-story "updated today" chip).
- **Task 7 (G1/G2):** Emit an inline-SVG favicon (gold quill-spark on dark) so tabs aren't default and `/favicon.ico` stops 404-ing; home `<title>` is now "SpecScribe — Project Dashboard"; added `<meta name="description">` + minimal Open Graph tags to every page.
- **Tasks 8–9:** Added render-/Charts-/stylesheet-/generation-level tests (pluralization, favicon/title/description, dashboard order, quick-link family accents, epic-status roll-up, donut fraction, future-day muting, heatmap headline, per-status tokens, no-preference entrance block, tooltip/copy-button styles, embedded+emitted script asset). Full suite: **197 passing**. Real generation pass: 24 pages, 0 errors; verified F4 four-questions coverage (every panel answers where things stand / what's left / what's next / how the work has gone).
- Story 1.4 accessibility preserved and extended, not rewritten: the `reduce` block, `:focus-visible` ring, skip link, single `<main>` landmark, whole-chart/segment aria-labels and `<title>`s all remain (guarded by the existing + new tests).

### File List

Story/tracking:
- _bmad-output/implementation-artifacts/1-5-dashboard-insight-polish-and-visual-truthfulness.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

Source (modified):
- src/SpecScribe/ForgeOptions.cs (new `ScriptName` const)
- src/SpecScribe/SiteGenerator.cs (emit JS asset via shared `CopyEmbeddedAsset`)
- src/SpecScribe/PathUtil.cs (`RenderHeadOpen`: favicon + description/OG + script link)
- src/SpecScribe/HtmlTemplater.cs (dashboard reorder; epic-status roll-up; stat pluralization/reframe/recency; stat-card tooltips; quick-link pills + family accents; requirement stacked-bar/fraction/zero-row suppression; home title/description)
- src/SpecScribe/Charts.cs (Donut `centerText`; heatmap future-day muting + ~15-week scale + headline; public `Plural`; stat-card `data-tooltip`)
- src/SpecScribe/BmadCommands.cs (Next Steps copy buttons + grammar fix)
- src/SpecScribe/EpicsTemplater.cs (script href in `RenderHeadOpen` calls)
- src/SpecScribe/RequirementsTemplater.cs (script href in `RenderHeadOpen` calls)
- src/SpecScribe/SpecScribe.csproj (embed `specscribe.js`)
- src/SpecScribe/assets/specscribe.css (status tokens; tooltip system; sunburst hover invert; interaction grammar; entrance animations; quick-link pills; copy buttons; heatmap scale/headline; stacked-bar styles)

Source (new):
- src/SpecScribe/assets/specscribe.js (dependency-free tooltip + copy-button progressive-enhancement script)

Tests (modified):
- tests/SpecScribe.Tests/HtmlTemplaterTests.cs (pluralization; favicon/title/description; dashboard order; quick-link family accents; epic-status roll-up)
- tests/SpecScribe.Tests/ChartsTests.cs (donut fraction; future-day muting; heatmap headline)
- tests/SpecScribe.Tests/StylesheetTests.cs (no-preference block; per-status tokens; tooltip/copy-button styles; embedded script)
- tests/SpecScribe.Tests/SiteGeneratorReadmeTests.cs (self-contained script asset emitted + linked)
- tests/SpecScribe.Tests/PathUtilTests.cs (new `RenderHeadOpen` signature; favicon/description/OG/script)
- tests/SpecScribe.Tests/ModuleContextTests.cs (copy-button markup adjusts the code-review row count assertion)

## Change Log

- 2026-07-06: Code review (adversarial multi-layer). Diagnosed the three reported visual artifacts as a stale cached pre-1.5 `specscribe.css` (not a source defect). Applied 8 patches: build-versioned cache-busting on the css/js hrefs (the durable fix); percent-encoded favicon data URI; copy-button `aria-label` no longer sticks on "Copied" after rapid re-clicks; fallback Epic-Status donut no longer fabricates a "0/total" fraction; heatmap grid no longer extends into the future and its heat scale excludes suppressed future days; empty requirement group renders an explicit empty state; tooltip positioning fixed for horizontal scroll + touch dismissal; and the on-brand tooltip is now keyboard-focusable (donut `tabindex`) and touch-reachable on donut/heatmap segments, not just the sunburst. 2 items deferred (unmapped `ForEpic` class; `HeatLevel` maxCount≤1 collapse), 5 dismissed. Full suite 197 passing; regenerated site verified in-browser. Status → done.
- 2026-07-06: Implemented Story 1.5. Landed the six per-stage status tokens and routed every chart/legend/badge through them (B1–B5); added the one sanctioned progressive-enhancement script for on-brand tooltips + Next Steps copy buttons, delivered as a self-contained embedded asset (C1/C2/C4/D5, F2); unified interaction grammar with sunburst hover-invert, In-development card emphasis, and `no-preference` entrance animations beside 1.4's `reduce` block (D1–D4/D2); made the existing charts truthful — epic-status donut from the story roll-up, reframed task stat, future-day-muted + rescaled heatmap with a headline, fraction donut centers, requirements stacked bars, zero-row suppression (A2/A3/A4/A5/E1/E2/E3); reordered the dashboard to lead with Now & Next + the sunburst and slimmed Explore Key Views to a pill row, added a commit-recency signal (F1/F3); emitted a favicon + home title/description/OG (G1/G2). Preserved and extended Story 1.4's accessibility floor. Added render/Charts/stylesheet/generation tests (197 passing); real generation pass produced 24 pages with 0 errors. Set Status → review.
- 2026-07-06: Created Story 1.5 by splitting the Story 1.4 UX review into its polish/truthfulness half. Scoped: status-color tokenization + unified vocabulary + zero-row suppression + quick-link accents (B1–B5); on-brand tooltip system with one sanctioned progressive-enhancement script (C1/C2/C4/D5); interaction grammar + sunburst sibling-dim + Now emphasis + `no-preference` entrance animations (D1–D4/D2); chart truthfulness for existing metrics (A2 pluralization, A3 epic-donut roll-up, A4 future-day muting, A5 task-stat reframe, E1 heatmap rescale/headline, E2 requirements stacked bars, E3 donut fractions); dashboard reorder + Next Steps copy/grammar + recency (F1–F3); favicon + home title/meta/OG (G1/G2). Routed A6/E4 to Story 2.1 and the accessibility items to Story 1.4; documented dependency on Story 1.4's seams and coordination with in-flight Story 1.3.

## Review Findings

Code review 2026-07-06 (adversarial multi-layer: Blind Hunter, Edge Case Hunter, Acceptance Auditor + inline diagnosis). Triaged: 1 decision-needed, 7 patch, 2 defer, 5 dismissed.

> **Root cause of the three reported visual artifacts (unstyled copy buttons, inconsistent epic/story colors, "Explore Key Views" as raw links):** a **stale, browser-/CDN-cached pre-1.5 `specscribe.css`** served against freshly-regenerated post-1.5 HTML. Verified: fresh CSS/JS are byte-identical to source and render correctly when served clean; the pre-1.5 CSS has 0 `.copy-btn`/`.quick-link-pill`/`--status-*` rules, so new markup falls back to browser defaults and old collapsed colors. Not a defect in the 1.5 source — see Patch P1 for the durable fix (cache-busting) and hard-refresh for the immediate remedy.

### decision-needed

_(resolved 2026-07-06 → reclassified as patch P8 below: extend the on-brand tooltip to bare donut/heatmap segments.)_

### patch

- [x] [Review][Patch] Extend the on-brand tooltip to keyboard/touch on the donut & heatmap charts (AC #1 / Task 3 [UXO C1/C4/D5]): make the JS `focusin`/`touchstart` handlers also match bare `.donut-seg`/`.heatmap-cell` (not just `closest("a")`) and add `tabindex="0"` so those segments are focusable — today only the `<a>`-wrapped sunburst delivers the on-brand tooltip to keyboard/touch users [src/SpecScribe/assets/specscribe.js:1047-1071; src/SpecScribe/Charts.cs (donut/heatmap segment emit)]
- [x] [Review][Patch] Assets linked without cache-busting → stale cached CSS produces the three reported artifacts on redeploy/regeneration; add a content-hash or version query to the css/js hrefs (e.g. `specscribe.css?v={hash}`) [src/SpecScribe/PathUtil.cs:47-50]
- [x] [Review][Patch] Copy-button `aria-label` permanently stuck on "Copied" for screen readers after rapid re-click within 1600ms (pending timeout not cleared; `prev` captured while already "Copied") [src/SpecScribe/assets/specscribe.js:1103-1111]
- [x] [Review][Patch] Fallback Epic Status donut hard-codes center to "0/total" done (unreachable-when-`epicsModel`-null path shows zero progress even for complete epics) [src/SpecScribe/HtmlTemplater.cs:239]
- [x] [Review][Patch] Future-dated commits (clock/timezone skew) break the heatmap: headline names a date the grid suppresses, `maxCount` includes suppressed future days (depresses the visible heat scale), a lone future-dated commit renders an all-blank grid, and `CommitStatSub` reports "last commit today" — A4 future-day suppression not reconciled with E1 headline & F3 recency [src/SpecScribe/Charts.cs:435,453,484; src/SpecScribe/HtmlTemplater.cs:202]
- [x] [Review][Patch] Favicon data URI is emitted without attribute/percent encoding — raw spaces and `<`/`>` inside `href="data:image/svg+xml,…"` (only `#` was `%23`-encoded), so the icon may fail to load in stricter browsers; run the SVG through `Uri.EscapeDataString` / percent-encode spaces [src/SpecScribe/PathUtil.cs:29-33,48]
- [x] [Review][Patch] Empty requirement group (a kind with 0 requirements) renders a hollow empty stacked bar + empty legend instead of an explicit empty state — the `nonZero.Count < 2` branch conflates "one status" with "no statuses" [src/SpecScribe/HtmlTemplater.cs:337]
- [x] [Review][Patch] Tooltip positioning edge cases in specscribe.js: first-show reads a stale bounding rect; `left` omits `window.scrollX` while `top` adds `scrollY` (mispositions on horizontal scroll); touch second-tap leaves a stale tooltip on same-page anchor nav [src/SpecScribe/assets/specscribe.js:1011-1023,1060-1071]

### defer

- [x] [Review][Defer] `AppendEpicStatusPanel` silently drops epics whose `ForEpic` class is outside {done,active,drafted,pending} — latent only; no unmapped epic class exists today [src/SpecScribe/HtmlTemplater.cs:221-226] — deferred, pre-existing
- [x] [Review][Defer] `HeatLevel` collapses every cell to the darkest level when `maxCount <= 1` (a uniform 1-commit/day history reads as heavy activity) — pre-existing; the E1 15-week window makes it more visible [src/SpecScribe/Charts.cs:508] — deferred, pre-existing
