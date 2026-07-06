---
baseline_commit: fb9fb88b7d5ba6d07055a42cbf84a37b21cb4fd0
---

# Story 1.3: Markdown Fidelity for Core Artifact Patterns

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reviewer,
I want markdown patterns rendered faithfully,
so that generated pages preserve planning intent and implementation context.

## Acceptance Criteria

1. **Given** source artifacts contain Mermaid blocks and task checklists
   **When** the site is generated
   **Then** Mermaid diagrams render client-side and checklists show completion states
   **And** rendering works without manual post-processing.

2. **Given** story details include acceptance-criteria references
   **When** I open a story page
   **Then** AC references deep-link to criteria anchors
   **And** links include readable tooltip context when available.

3. **Given** a rendered page that has (or should have) a table of contents — generic document pages, ADRs, README, and epic/story detail pages
   **When** I view the page on a wide viewport
   **Then** the table of contents appears as a sidebar alongside the main content, with the main content scrolling independently of the sidebar so the TOC stays in view
   **And** the TOC entries follow the page's actual rendered section order (which may differ from the markdown source order), each linking to the correct in-page section
   **And** the layout degrades gracefully to a usable stacked/collapsed form on narrow viewports.

## Tasks / Subtasks

- [x] Task 1: Verify and harden Mermaid client-side rendering across every rendered page type (AC: #1)
  - [x] Confirm ` ```mermaid ` fences on full document pages (generic docs + ADRs) still emit `<pre class="mermaid">` via `MermaidCodeBlockRenderer` and are decoded correctly by the client init (`Mermaid.InitScript`)
  - [x] Confirm the init script is injected exactly on pages that need it: generic/ADR pages when `DocModel.HasMermaid` is true, and the epics index (which always emits the roadmap diagram)
  - [x] Resolve the fragment-render gap: routed `MarkdownConverter.RenderBlock` through the shared mermaid-aware renderer (`RenderDocumentHtml`) so a fence inside a story/epic artifact body now emits `<pre class="mermaid">`, and made `EpicsTemplater.RenderStory`/`RenderEpic` inject `Mermaid.InitScript()` when the composed page actually contains a mermaid block (`Mermaid.ContainsBlock`). `RenderInline` intentionally left untouched — mermaid is a block construct that cannot appear in a single-line inline fragment
- [x] Task 2: Verify and harden task-checklist completion-state rendering (AC: #1)
  - [x] Confirm GitHub-style `- [ ]`/`- [x]` lists in document bodies render as disabled checkboxes (Markdig `UseTaskLists` via the shared pipeline) with the completed/incomplete swatch styling from `specscribe.css`
  - [x] Confirm the "## Tasks / Subtasks" section on story pages shows completion states both as the styled checklist (inside `.doc-body`) and as the task sunburst fed by `TaskListParser`
  - [x] Confirm mixed-case marks (`- [X]`) and nested/subtask indentation render completion state correctly and that non-checkbox bullets are unaffected
- [x] Task 3: Verify and harden AC-reference deep-linking and tooltips (AC: #2)
  - [x] Confirm each acceptance criterion renders in its own panel row with a stable `id="ac-N"` anchor and an `.ac-anchor` self-link, and that `:target` highlight styling still fires on navigation
  - [x] Confirm `EpicsParser.LinkifyAcReferences` rewrites `(AC: #N)` / `(AC #N, #M)` references in the story remainder to `<a class="ac-ref" href="#ac-N" title="…">#N</a>` with the criterion's plain text as the `title` tooltip
  - [x] Confirm unresolved AC numbers (no matching criterion) remain plain text and never become broken anchors, and that references already inside other markup are not double-linkified
- [x] Task 4: Add/extend regression coverage for markdown-fidelity behavior at unit and generation levels (AC: #1, #2)
  - [x] Extend `MarkdownConverterTests` for task-list checkbox output (checked vs. unchecked) and, if Task 1's fix lands, for mermaid inside `RenderBlock` fragments
  - [x] Extend `EpicsParserTests` for `LinkifyAcReferences` edge cases (comma-grouped numbers, unresolved numbers, tooltip text) and AC-anchor extraction
  - [x] Add a focused generation-level assertion (in `SiteGeneratorFidelityTests`) that a rendered story page contains `id="ac-N"` anchors and matching `href="#ac-N"` references, and that a document page with a mermaid fence carries the init script
- [x] Task 5: Validate end-to-end rendered behavior with the real generation path (AC: #1, #2, #3)
  - [x] Run the focused test filter for the touched behavior
  - [x] Run a real generation pass and confirm mermaid diagrams, task checklists, AC deep-links, and the sidebar TOC render correctly with no console errors and no broken anchors
- [x] Task 6: Relocate the table of contents into an independently-scrolling sidebar across all TOC-bearing pages (AC: #3)
  - [x] Introduce a two-column page layout (main content + TOC sidebar) shared by the generic page shell and the epic/story detail templater, so the TOC lives beside the content instead of as a top strip
  - [x] Make the sidebar sticky beneath the sticky nav and give it its own vertical overflow so it stays in view and scrolls independently when longer than the viewport; the main content scrolls normally
  - [x] Replace `.toc-strip` with a `.toc-sidebar` presentation via one shared TOC-rendering seam that takes an ordered list of `(level, text, anchorId)` entries — do not fork per page type
  - [x] Generic doc pages, ADRs, and README: source render order already equals `DocModel.Headings` order, so feed those headings to the shared TOC seam (verify heading ids exist for every entry so no link is dead)
  - [x] Epic/story detail pages: build the TOC entries in the templater's **actual section-emission order** (e.g. User Story → Task Breakdown → Acceptance Criteria → Dev Agent Record → Review Findings → remainder headings → Change Log). Give each templater-emitted panel a stable `id` + human label, and collect the remainder fragment's rendered headings in order so they appear as TOC entries too
  - [x] Add `scroll-margin-top` (accounting for the sticky nav height) to headings/section anchors so clicking a TOC link lands the target below the nav rather than hidden under it
  - [x] Implement responsive behavior: on narrow (mobile/tablet) breakpoints the sidebar collapses to a stacked/top or toggled form that remains usable and does not crush the main content
  - [x] Preserve the accessible `<nav aria-label="On this page">` semantics; keep entries as real in-page anchor links (keyboard-activatable). Scroll-spy/active-section highlighting is optional polish, not required by AC #3

## Developer Context Section

### Epic Context and Business Value

Epic 1 delivers a polished, immediately-useful portal for current BMad projects. Story 1.1 built navigation and dashboard wayfinding; Story 1.2 layered in traceability links. Story 1.3 is the **fidelity** gate: the generated pages must faithfully reproduce the markdown patterns that carry planning intent — Mermaid diagrams, task checklists, and the internal `(AC: #N)` cross-references that connect a story's tasks back to its acceptance criteria.

This story realizes **FR7** (markdown fidelity including Mermaid and task-list rendering) and reinforces the AC-deep-linking half of the reading flow that Story 1.2 began. If diagrams silently drop, checklists lose their state, or AC references are dead text, the portal stops preserving the author's intent and readers fall back to reading raw markdown.

### Story Foundation Extract

- Primary concern: faithful rendering of Mermaid blocks, task checklists (with completion state), and in-story AC references (with anchors + tooltips).
- User outcome: a reviewer sees diagrams, checked/unchecked tasks, and one-click jumps from a task's `(AC: #N)` to the criterion it satisfies.
- Success boundary: rendering happens automatically during generation with no manual post-processing; unresolved AC numbers degrade to plain text.
- Regression boundary: ordinary fenced code stays untouched; non-checkbox bullets are unaffected; existing anchor/traceability behavior from 1.1/1.2 is preserved.

### Current Implementation Reality (READ THIS FIRST)

Both acceptance criteria are **already substantially implemented** in the codebase at the baseline commit. Like Story 1.2, this is expected to be primarily a **verify-and-harden** pass rather than a greenfield build. Do not reinvent any of the following existing seams — strengthen and test them, and only change production code where a concrete defect or the Task 1 gap requires it.

Existing behavior confirmed present:

- **Mermaid (full pages):** `MarkdownConverter.Convert` swaps in `MermaidCodeBlockRenderer`, which emits ` ```mermaid ` fences as `<pre class="mermaid">` (HTML-encoded source) and sets `DocModel.HasMermaid`. `HtmlTemplater.RenderPage` injects `Mermaid.InitScript()` (CDN `mermaid@11`, themed) when `HasMermaid` is true. Ordinary code fences fall through to the default renderer. [Source: `src/SpecScribe/MarkdownConverter.cs`, `src/SpecScribe/MermaidCodeBlockRenderer.cs`, `src/SpecScribe/Mermaid.cs`, `src/SpecScribe/HtmlTemplater.cs:51`]
- **Mermaid (epics index):** `EpicsTemplater.RenderIndex` emits a generated roadmap diagram via `Mermaid.Block(Mermaid.RoadmapDiagram(...))` and appends `Mermaid.InitScript()`. [Source: `src/SpecScribe/EpicsTemplater.cs:47`, `:58`]
- **Task checklists:** the shared Markdig pipeline uses `UseAdvancedExtensions()` (which includes task lists), so `- [ ]`/`- [x]` render as disabled checkboxes; `specscribe.css` restyles them into an empty box vs. a filled green checkmark. `TaskListParser` additionally parses the "## Tasks / Subtasks" section into a two-level tree for the task sunburst. [Source: `src/SpecScribe/MarkdownConverter.cs:15-17`, `src/SpecScribe/assets/specscribe.css:904-926`, `src/SpecScribe/TaskListParser.cs`]
- **AC anchors:** `EpicsTemplater.RenderStory` renders each criterion in `<div class="ac-criterion" id="ac-N">` with an `.ac-anchor` self-link; CSS provides a `:target` highlight. [Source: `src/SpecScribe/EpicsTemplater.cs:207-218`, `src/SpecScribe/assets/specscribe.css:627-661`]
- **AC references + tooltips:** `SiteGenerator` builds `criteriaByNumber` (number → plain text) and calls `EpicsParser.LinkifyAcReferences(remainderHtml, …)`, which rewrites `(AC: #N)` groups into `<a class="ac-ref" href="#ac-N" title="{plain text}">#N</a>`, leaving unknown numbers as plain text. `AcceptanceCriterion.PlainText` is the tooltip source. [Source: `src/SpecScribe/SiteGenerator.cs:372-374`, `src/SpecScribe/EpicsParser.cs:201-251`, `src/SpecScribe/EpicsModel.cs:47-48`]

Existing tests to preserve and build on:

- `tests/SpecScribe.Tests/MarkdownConverterTests.cs` — mermaid fence → `<pre class="mermaid">`, ordinary code fence untouched, `HasMermaid` flag.
- `tests/SpecScribe.Tests/EpicsParserTests.cs` — `LinkifyAcReferences_LinksKnownNumbersOnly`.
- `tests/SpecScribe.Tests/TaskListParserTests.cs`, `SiteGeneratorTraceabilityTests.cs`, `HtmlTemplaterTests.cs`.

### Known Gap / Primary Hardening Target (AC #1)

`MarkdownConverter.RenderBlock` and `RenderInline` render fragments (story remainder, epic goals, dev-agent record, review findings, change log, requirements inventory) through the **plain** Markdig pipeline — they do **not** apply the `UseMermaidCodeBlocks` renderer override, which is wired only inside `Convert()`'s manual `HtmlRenderer` setup. Consequences:

1. A ` ```mermaid ` fence authored inside a **story or epic artifact body** renders as `<code class="language-mermaid">` (inert), not `<pre class="mermaid">`.
2. `EpicsTemplater.RenderStory` / `RenderEpic` never call `Mermaid.InitScript()`, so even a correctly-classed block would not initialize on those pages.

For this repository's own artifacts the primary mermaid case is `ARCHITECTURE-SPINE.md` (a generic doc page routed through `Convert()`), which renders fine. The dev must decide whether BMad implementation artifacts realistically carry mermaid in their bodies. If yes, fix the fragment path (share the mermaid-aware renderer and inject the init script on story/epic pages when a fragment contains mermaid). If no, document it explicitly as an accepted, non-fatal limitation with rationale. Either way, make the decision deliberately and cover it with a test or a note — do not leave it silently ambiguous.

### Table-of-Contents Sidebar (AC #3)

A reading-fidelity enhancement layered onto this story per direction: the on-page TOC must move from its current top strip to an independently-scrolling sidebar, and must be extended to detail pages that lack one today. Current state at baseline:

- **Existing TOC:** `HtmlTemplater.RenderPage` emits `<nav class="toc-strip">` — a horizontal strip of links above the article — for docs with level-2/3 headings, sourced from `DocModel.Headings` (collected in document order by `MarkdownConverter.Convert`). Styled at `specscribe.css:167-198` (`max-width: 860px`, centered, flex-wrapped). [Source: `src/SpecScribe/HtmlTemplater.cs:33-44`, `src/SpecScribe/assets/specscribe.css:167-198`]
- **Which pages have it:** generic document pages, **ADRs**, and **README** all render through `RenderPage`, so all three already carry the top-strip TOC. Requirements index/detail pages use `RequirementsTemplater` (no TOC). **Epic and story detail pages** render through `EpicsTemplater` and currently have **no TOC**.
- **No layout wrapper exists:** `RenderPage` and the detail templaters emit a flat sequence (nav → breadcrumb → header → [toc] → article/panels → footer); the article/`.doc-body` is a single 860px-centered column. The site nav is `position: sticky` at the top. A sidebar requires introducing a two-column layout container around the main content + TOC. [Source: `src/SpecScribe/PathUtil.cs:26-39`, `src/SpecScribe/assets/specscribe.css` layout rules]

Design guidance (keep it central, do not fork per page type):

- Add one shared TOC-rendering seam (e.g. a `RenderTocSidebar(IReadOnlyList<(int Level, string Text, string AnchorId)>)` helper on `PathUtil`/`HtmlTemplater`) plus a two-column layout wrapper (grid or flex): main content in one column, `<nav class="toc-sidebar" aria-label="On this page">` in the other.
- Sidebar is `position: sticky; top: <nav height>; max-height: calc(100vh - <nav height>); overflow-y: auto;` so it stays visible and scrolls on its own; the main column scrolls with the page. That is the mechanism for "main content scrolls independently of the TOC."
- Add `scroll-margin-top` to heading/section anchors so a clicked TOC link is not hidden behind the sticky nav.
- Responsive: on mobile/tablet breakpoints (UX-DR13 already establishes breakpoints), collapse the sidebar to a stacked top position or a toggle — do not squeeze the content column.

The hard part — "rendered order" (AC #3, and the reason source order is insufficient):

- **Generic / ADR / README pages:** `RenderPage` renders `doc.BodyHtml` straight through, so `DocModel.Headings` order already equals rendered order. These just need the layout swap; no reordering logic.
- **Detail pages (epic/story):** `EpicsTemplater` composes the page out of order relative to the source markdown — the Acceptance Criteria panel leads, Dev Agent Record is a collapsed table, Review Findings ride high, the markdown remainder renders in the middle (`RenderBlock`, which does NOT populate a Headings list), and Change Log anchors the bottom. So the detail-page TOC must be assembled from the templater's **actual emission order**, not from any single source-order heading list. Concretely: give each emitted panel a stable `id` and label (Task Breakdown, Acceptance Criteria, Dev Agent Record, Review Findings, Change Log), and additionally collect the remainder fragment's rendered headings (they receive ids from Markdig's auto-identifier extension, but are not currently gathered) so they slot into the TOC between the panels in the order they render. [Source: `src/SpecScribe/EpicsTemplater.cs:143-256`, `src/SpecScribe/SiteGenerator.cs:341-379`]

Scope note: this is fundamentally a navigation/readability layout change (closer in spirit to Story 1.1 / UX-DR13, UX-DR16) rather than markdown-content fidelity, but it is scoped into Story 1.3 by the product owner's direction. It can be implemented independently of Tasks 1–5; sequence it after the verify-and-harden work so a rendering regression and a layout regression are never entangled in one change.

### Previous Story Intelligence

Story 1.2 (`review`) set the working pattern this story should follow:

- It turned out to be a verify-and-harden pass: the seams already satisfied both ACs, so the work was **regression coverage + a real generation pass**, with zero production changes. Expect the same shape here and only touch production code for the Task 1 gap or a concrete defect. [Source: `_bmad-output/implementation-artifacts/1-2-traceability-links-across-requirements-stories-and-adrs.md`]
- Keep behavior in the existing central seam (`MarkdownConverter`, `EpicsParser`, `MermaidCodeBlockRenderer`) rather than duplicating logic in templates.
- Preserve omission-safe / graceful-degradation behavior: unresolved references stay plain text.
- Prefer generation-level integration tests over adding public API surface just to reach private helpers.
- Generated output is published to GitHub Pages, so all links/anchors must remain static-host-safe and relative-correct.
- Story 1.1 established navigation/breadcrumb behavior that must not regress.

### Architecture Compliance

- User-visible rendering features live in the shared rendering core (parsing → projection → view models), not in host-specific adapters; keep markdown-fidelity behavior there so future HTML/webview surfaces inherit it. [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1, AD-2; `_bmad-output/specs/spec-specscribe/rendering-architecture.md`]
- Host-neutral view models are the contract; do not leak host-only rendering assumptions into core conversion. [Source: `docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md`]
- Cross-surface interaction/semantic consistency: anchor targets, tooltip semantics, and diagram/checklist meaning must not diverge between static HTML and any future webview. [Source: `docs/adrs/0004-cross-surface-interaction-and-theme-contract.md`]
- Graceful degradation is contractual: malformed/unsupported markdown (bad mermaid syntax, orphan AC numbers) must degrade non-fatally, never blocking generation. [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` (Inherited Invariants); `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`]
- Mermaid is the one intentional internet-dependent extra (CDN module); the offline SVG/CSS charts remain the backbone. Do not make baseline generation depend on the CDN. [Source: `src/SpecScribe/Mermaid.cs`]

## Technical Requirements

- ` ```mermaid ` fences must render client-side as `<pre class="mermaid">`; ordinary fenced code must remain `<code class="language-…">` and untouched.
- The mermaid init script must be present on any page that emits a mermaid block, and absent where none exist (keep the `HasMermaid`-gated injection on full pages; extend to story/epic pages only if Task 1's fix routes mermaid there).
- Task-list items must render completion state faithfully: `- [x]` → checked/filled, `- [ ]` → empty; mixed-case `[X]` treated as done; nested subtasks preserve their own state; non-checkbox bullets unaffected.
- Each acceptance criterion must carry a stable `id="ac-N"` anchor and a self-referencing `.ac-anchor` link.
- `(AC: #N)` and comma-grouped `(AC #N, #M)` references in the story remainder must become `<a class="ac-ref" href="#ac-N" title="…">#N</a>`; the tooltip is the criterion's plain text; unresolved numbers stay plain text.
- Rendering must require no manual post-processing — it happens entirely within the generation pass.
- HTML-escaping must remain correct for mermaid source (encoded, decoded by the client) and for tooltip text (`PathUtil.Html`).

## File Structure Requirements

Primary UPDATE candidates (touch only as needed):

- `src/SpecScribe/MarkdownConverter.cs`
  - Current state: single shared Markdig pipeline; `Convert()` wires the mermaid renderer via a manual `HtmlRenderer`; `RenderInline`/`RenderBlock` use the plain `Markdown.ToHtml` path (no mermaid override).
  - Story change focus (Task 1 gap): if fragments must support mermaid, share the mermaid-aware renderer with `RenderBlock`/`RenderInline` (and surface a "contains mermaid" signal so the caller can inject the init script). Keep the change minimal and central.
  - Must preserve: `FileShare.ReadWrite` shared reads, frontmatter split, heading/ToC collection, table tagging, color-swatch rewrite, `HasMermaid` detection.

- `src/SpecScribe/MermaidCodeBlockRenderer.cs`
  - Current state: wraps the default code-block renderer; emits `<pre class="mermaid">` for `mermaid` fences, delegates all others.
  - Story change focus: reuse (do not fork) if enabling mermaid in fragment rendering.
  - Must preserve: case-insensitive `mermaid` info-string match; passthrough for non-mermaid blocks.

- `src/SpecScribe/EpicsTemplater.cs`
  - Current state: `RenderStory`/`RenderEpic` render fragments and AC panels; neither injects `Mermaid.InitScript()`; `RenderIndex` does.
  - Story change focus: inject the init script on story/epic pages **only if** Task 1 routes mermaid into their bodies.
  - Must preserve: AC panel anchors (`id="ac-N"`, `.ac-anchor`), header/kicker layout, sunburst wiring, section ordering.

- `src/SpecScribe/EpicsParser.cs`
  - Current state: `LinkifyAcReferences`, `ExtractAcceptanceCriteria` (with `PlainText` tooltip source), `SplitStoryArtifact`.
  - Story change focus: only if AC-reference matching or anchor extraction has a concrete defect.
  - Must preserve: unresolved-number plain-text fallback; comma-group handling; source-citation bracket stripping.

- `src/SpecScribe/HtmlTemplater.cs`
  - Current state: `.doc-body`-wrapped body; `HasMermaid`-gated init script on full pages; emits the top-strip TOC (`.toc-strip`) from `doc.Headings`.
  - Story change focus (AC #3): wrap the main content + TOC in a two-column layout and render the TOC via the shared sidebar seam instead of the top strip.

- `src/SpecScribe/EpicsTemplater.cs` (AC #3, in addition to the AC-panel note above)
  - Story change focus: add a rendered-order TOC sidebar to `RenderEpic`/`RenderStory` using the same two-column layout and shared TOC seam; assign stable ids + labels to each emitted panel and include remainder headings in emission order.

- `src/SpecScribe/PathUtil.cs` and/or a small TOC helper/model
  - Story change focus (AC #3): host the shared TOC-sidebar rendering seam and the two-column page-shell wrapper so every page type reuses one implementation. Keep `RenderHeadOpen`/`RenderFooter` shell conventions intact.

- `src/SpecScribe/DocModel.cs` / `MarkdownConverter.cs`
  - Story change focus (AC #3): if detail-page remainder headings must feed the TOC, surface an ordered heading list for fragment rendering (extend `RenderBlock` to optionally return headings, or extract heading ids from the rendered fragment). Preserve existing `Headings` collection for full-page conversion.

- `src/SpecScribe/assets/specscribe.css`
  - Current state: styles for task-list checkboxes (`.doc-body ul li input[type="checkbox"]`), `.ac-criterion`/`.ac-anchor`/`.ac-ref`, `.mermaid`, `.ac-criterion:target`, and the top-strip TOC (`.toc-strip`, lines 167-198).
  - Story change focus (AC #3): add `.toc-sidebar` + two-column layout styles (sticky, independent overflow, responsive collapse) and `scroll-margin-top` on anchors; retire/repurpose `.toc-strip`. Also adjust only if a rendering fidelity/visibility defect is found for AC #1/#2.

Primary TEST candidates:

- `tests/SpecScribe.Tests/MarkdownConverterTests.cs` — mermaid + task-list checkbox rendering; extend for fragment-mermaid if Task 1 lands.
- `tests/SpecScribe.Tests/EpicsParserTests.cs` — `LinkifyAcReferences` edge cases + AC extraction/tooltips.
- `tests/SpecScribe.Tests/TaskListParserTests.cs` — checklist parsing (preserve).
- `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs` (or a sibling generation-level test) — assert rendered story page has matching `id="ac-N"` / `href="#ac-N"` and that a mermaid doc page carries the init script.

## Library and Framework Requirements

- Stay on the existing .NET / Markdig / YamlDotNet stack. Task lists come from Markdig's `UseAdvancedExtensions()`; do not add a separate task-list extension or a parallel markdown renderer.
- Mermaid renders client-side from `https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs`. Keep the version pin; do not bundle a local mermaid build unless a concrete offline requirement is raised (out of scope here).
- No new dependencies are expected for this story.

## Testing Requirements

- Preserve existing coverage in `MarkdownConverterTests` (mermaid fence → `<pre class="mermaid">`, ordinary fence untouched, `HasMermaid`) and `EpicsParserTests.LinkifyAcReferences_LinksKnownNumbersOnly`.
- Add focused coverage for the gaps this story exists to close:
  - task-list checkbox output for checked vs. unchecked (and nested/mixed-case),
  - `LinkifyAcReferences` comma-grouped numbers, unresolved numbers left as text, and tooltip text correctness,
  - a generation-level assertion tying `(AC: #N)` references to their `id="ac-N"` anchors on a real rendered story page,
  - if Task 1's fix lands: mermaid inside a `RenderBlock` fragment renders as `<pre class="mermaid">` and the hosting page gets the init script.
  - AC #3: the shared TOC seam renders a `.toc-sidebar` (not `.toc-strip`); a generic/ADR page's TOC entries match `doc.Headings` order; a detail page's TOC entries follow the templater's emission order (panel labels + remainder headings) with every entry pointing at an id that actually exists on the page (no dead links).
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~MarkdownConverter|FullyQualifiedName~EpicsParser|FullyQualifiedName~TaskList|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Verify in generated output: mermaid diagrams render (no console errors), task checklists show correct states, and AC references jump to (and `:target`-highlight) the right criterion.

## UX and Accessibility Requirements

- Mermaid, task lists, and AC deep-links are part of the reviewer reading flow; they must remain readable and keyboard-usable within existing portal semantics. AC references are real anchors (focusable, activatable by keyboard) and carry a `title` tooltip for context. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md`]
- Status/state must never be color-only. Task completion pairs the green fill with a checkmark glyph (shape, not just color); keep that redundancy. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR17; `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`]
- Motion: mermaid initialization and any highlight transitions must respect `prefers-reduced-motion` and stay non-looping. Do not introduce new unbounded animation. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR18]
- Use the established token/color system and existing component styles rather than ad hoc per-feature styling. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`]
- The TOC sidebar (AC #3) must keep its accessible `<nav aria-label="On this page">` landmark with real, keyboard-activatable in-page anchor links, and must remain usable across mobile/tablet/desktop breakpoints (sidebar collapses/stacks on narrow viewports rather than crushing content). Sticky positioning and any scroll behavior must respect `prefers-reduced-motion`. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR13, UX-DR16, UX-DR18]

## Reinvention and Regression Guardrails

- Do not add a second markdown pipeline or a bespoke checkbox/diagram renderer; reuse the shared pipeline and `MermaidCodeBlockRenderer`.
- Do not duplicate AC-anchor or AC-reference logic in templates; it lives in `EpicsTemplater` (anchors) + `EpicsParser.LinkifyAcReferences` (references).
- Do not turn unresolved AC numbers into anchors with placeholder targets.
- Do not make baseline generation depend on the mermaid CDN being reachable.
- Do not regress Story 1.1 navigation/breadcrumbs or Story 1.2 requirement/source/ADR linkification while touching rendering.
- Keep changes host-neutral so HTML/webview parity is not made harder.
- For the TOC sidebar (AC #3): do not fork the TOC renderer per page type — build one shared seam. Do not build the detail-page TOC from source-order headings (it will mis-order); assemble it from the templater's actual emission order. Do not emit TOC entries whose anchor id is not present on the page. Do not break the existing sticky-nav offset when adding a sticky sidebar.

## Git Intelligence Summary

- `fb9fb88` (Story 1.2) is the pattern to emulate: verify-and-harden with regression tests and a clean generation pass, minimal/no production change.
- `3b0227e` hardened the color-swatch rewriter (also a post-render HTML pass in `MarkdownConverter.RenderBlock`/`RenderInline`) — a reminder that fragment rendering has its own passes distinct from `Convert()`; this is exactly where the mermaid-in-fragment gap lives.
- `08c210e` enhanced generation robustness/fault tolerance — preserve that graceful-degradation posture for malformed markdown.
- Earlier CI commits publish `docs/live` to GitHub Pages; keep generated anchors/links static-host-safe.

## Latest Technical Information

- Mermaid is pinned to the `@11` ESM CDN module; no version-driven change is required. If touching the init script, keep `startOnLoad: true` and the existing theme variables so diagrams stay on-brand. No other external version considerations apply.

## Project Context Reference

- PRD: `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`
- Epics: `_bmad-output/planning-artifacts/epics.md`
- Previous story: `_bmad-output/implementation-artifacts/1-2-traceability-links-across-requirements-stories-and-adrs.md`
- Architecture spine: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`
- Rendering architecture: `_bmad-output/specs/spec-specscribe/rendering-architecture.md`
- ADR 0002 (shared rendering core / host-neutral view models): `docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md`
- ADR 0004 (cross-surface interaction & theme contract): `docs/adrs/0004-cross-surface-interaction-and-theme-contract.md`
- UX design: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`
- UX behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md`
- Key source seams: `src/SpecScribe/MarkdownConverter.cs`, `MermaidCodeBlockRenderer.cs`, `Mermaid.cs`, `TaskListParser.cs`, `EpicsParser.cs`, `EpicsTemplater.cs`, `HtmlTemplater.cs`, `SiteGenerator.cs`, `assets/specscribe.css`

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed - comprehensive developer guide created.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8

### Debug Log References

- create-story workflow run for story `1-3-markdown-fidelity-for-core-artifact-patterns`
- workflow customization resolved via `resolve_customization.py`; `python3`/`python` shim printed the resolver JSON but is not a working interpreter on this Windows host (repo convention: use `py -3` for BMAD helper scripts)
- planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~MarkdownConverter|FullyQualifiedName~EpicsParser|FullyQualifiedName~TaskList|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Treat this as verify-and-harden (mirroring Story 1.2): confirm each AC behavior against the existing seams before writing any production code.
- Make an explicit decision on the mermaid-in-artifact-body gap (Task 1): fix centrally in `MarkdownConverter` fragment rendering + story/epic init-script injection, or document as an accepted non-fatal limitation.
- Add regression coverage for task-list completion states, AC-reference/tooltip edge cases, and a generation-level anchor↔reference check.
- Re-run targeted tests and a real generation pass before marking implementation complete.

### Completion Notes List

- Story context assembled from epics.md (FR7 + Story 1.3 ACs + UX-DR17/18), PRD, UX design/experience, architecture spine (AD-1/AD-2, inherited invariants), rendering architecture, ADRs 0002/0004, Story 1.2 intelligence, current code seams, and recent git history.
- Confirmed at baseline commit `fb9fb88`: Mermaid full-page rendering, epics-index roadmap diagram, task-list checkbox styling + parser, AC `id="ac-N"` anchors, and `(AC: #N)` → `#ac-N` linkification with plain-text tooltips are all already implemented.
- Primary open work for AC #1/#2 is the fragment-render mermaid gap (`RenderBlock`/`RenderInline` bypass the mermaid renderer; story/epic pages omit the init script) plus regression coverage — production changes may be minimal or none if BMad artifacts never carry mermaid in bodies.
- AC #3 (added per product-owner direction) is net-new build, not verify-and-harden: relocate the top-strip TOC (`.toc-strip`) into an independently-scrolling sidebar shared across generic/ADR/README pages and add a rendered-order TOC to epic/story detail pages (which have none today). The detail-page TOC must follow the templater's emission order, not source-heading order. This is a navigation/readability change and is sequenced after Tasks 1–5.

### Implementation Completion Notes (dev-story run 2026-07-06)

**AC #1 — Mermaid fidelity (Task 1):** Resolved the fragment-render gap centrally. Extracted a shared `MarkdownConverter.RenderDocumentHtml(MarkdownDocument)` helper (mermaid-aware `HtmlRenderer` setup) now used by both `Convert()` and `RenderBlock()`, so a ` ```mermaid ` fence authored inside a story/epic artifact body renders as `<pre class="mermaid">` instead of inert `<code class="language-mermaid">`. Added `Mermaid.BlockMarker` + `Mermaid.ContainsBlock(html)`, and `EpicsTemplater.RenderStory`/`RenderEpic` now inject `Mermaid.InitScript()` only when the composed page actually contains a diagram — mirroring the existing `HasMermaid` gate on full pages. `RenderInline` intentionally left untouched (mermaid is a block construct). Verified in the real generation pass: epics roadmap + `ARCHITECTURE-SPINE` render to SVG (`data-processed="true"`), and no page carries a spurious init script.

**AC #1 — Task checklists (Task 2):** Confirmed no production change needed — Markdig `UseAdvancedExtensions()` renders `- [x]`/`- [X]`/`- [ ]` as disabled checkboxes (checked vs. unchecked) with the existing `specscribe.css` swatch styling; non-checkbox bullets unaffected. Added regression coverage (`RenderBlock` checkbox + non-checkbox theories) and confirmed 42 rendered checkboxes on the real story-1-3 page.

**AC #2 — AC deep-linking (Task 3):** Confirmed no production change needed — `id="ac-N"` anchors + `.ac-anchor` self-links + `:target` highlight and `EpicsParser.LinkifyAcReferences` all behave. Added edge-case coverage (comma groups, colonless/spaceless forms, unresolved-number plain-text fallback, tooltip escaping, idempotence/no double-linkify, empty-criteria no-op). Verified in-browser: `#ac-1` targets the criterion and the `:target` highlight fires.

**AC #3 — TOC sidebar (Task 6, net-new):** Introduced one shared seam `Toc` (`RenderSidebar`, `WrapWithSidebar`, `ExtractHeadings`) plus a two-column `.page-shell`/`.toc-sidebar` layout — no per-page-type fork. Generic docs/ADRs/README feed `DocModel.Headings` (source==render order); epic/story detail pages build the TOC in the templater's actual emission order (panels get stable `sec-*` ids + labels; remainder-fragment headings are gathered via `Toc.ExtractHeadings` and slotted in rendered order). Sidebar is `position: sticky` under the nav with its own `overflow-y: auto` (independent scroll); `scroll-margin-top: var(--nav-offset)` on anchors; collapses to a stacked strip below 900px. Verified in-browser at 1280px (grid `876px 224px`, sticky top 60px, 0 horizontal overflow) and 375px (collapsed, widest TOC link 274px, 0 overflow); 310 TOC anchors across 28 generated pages with **0 dead links**; no console errors.

**Root-cause fix (found during AC #3 validation):** `PathUtil.StripHtmlTags` used a non-`Singleline` `<.*?>` regex, so a heading whose linkified `(AC: #N)` reference carried a multi-line `title` attribute wasn't stripped — leaking raw anchor markup into the TOC entry text (and a ~3500px overflowing link on mobile). Made the strip regex `Singleline`; added a `Toc.ExtractHeadings` regression test for a heading anchor whose attribute spans newlines.

### File List

Production:
- src/SpecScribe/MarkdownConverter.cs — extracted shared mermaid-aware `RenderDocumentHtml`; `RenderBlock` now mermaid-aware
- src/SpecScribe/Mermaid.cs — added `BlockMarker` + `ContainsBlock`
- src/SpecScribe/EpicsTemplater.cs — `RenderStory`/`RenderEpic` compose main content + emission-order TOC into the shared two-column shell; conditional mermaid init injection
- src/SpecScribe/HtmlTemplater.cs — `RenderPage` uses the shared TOC sidebar/shell instead of the top strip
- src/SpecScribe/Toc.cs — NEW shared TOC seam (sidebar renderer, two-column shell wrapper, rendered-order heading extractor)
- src/SpecScribe/PathUtil.cs — `StripHtmlTags` regex made `Singleline` (multi-line-attribute tag stripping)
- src/SpecScribe/assets/specscribe.css — `--nav-offset`; `.page-shell`/`.page-main`/`.toc-sidebar` two-column layout, sticky/independent-scroll, scroll-margin, responsive collapse; retired `.toc-strip`

Tests:
- tests/SpecScribe.Tests/MarkdownConverterTests.cs — fragment-mermaid + task-list checkbox coverage
- tests/SpecScribe.Tests/EpicsParserTests.cs — `LinkifyAcReferences` edge cases
- tests/SpecScribe.Tests/TocTests.cs — NEW seam unit coverage
- tests/SpecScribe.Tests/SiteGeneratorFidelityTests.cs — NEW generation-level fidelity + TOC coverage

Other:
- .claude/launch.json — NEW static-preview config (serves docs/live) used for AC #3 visual validation
- docs/live/** — regenerated site output
- _bmad-output/implementation-artifacts/1-3-markdown-fidelity-for-core-artifact-patterns.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

### Review Findings

Adversarial code review (2026-07-06, three parallel layers: Blind Hunter, Edge Case Hunter, Acceptance Auditor). All three ACs verified satisfied; no high/medium defects. 6 findings dismissed as false positives/by-design.

- [x] [Review][Patch] TOC seam has no duplicate-anchor-id guard when panel ids and remainder-heading auto-ids share a namespace [src/SpecScribe/Toc.cs:37], [src/SpecScribe/EpicsTemplater.cs:281] — Low. **FIXED 2026-07-06:** added a keep-first `HashSet<string>` dedupe by `AnchorId` in `Toc.RenderSidebar` + regression test `RenderSidebar_DeduplicatesEntriesSharingAnAnchorIdKeepingTheFirst`. Detail pages build the TOC as `[hardcoded sec-*/ac-N panel ids] + Toc.ExtractHeadings(remainderHtml)`. Neither `WrapWithSidebar`/`RenderSidebar` nor `ExtractHeadings` dedupes `AnchorId`. If an artifact author writes a remainder heading with an explicit `{#id}` (or one that slugs to a `sec-*`/`ac-N`/`story-<id>` id), the page emits two elements with the same id and two sidebar links to it; browsers jump only to the first. Reachability is low (the `sec-` prefix is deliberately namespaced and Markdig auto-dedupes identical slugs), but a keep-first dedupe in the seam is cheap and aligns with the story's "no dead links" theme.
- [x] [Review][Defer] Inconsistent `scroll-margin-top` offsets for sticky-nav clearance [src/SpecScribe/assets/specscribe.css:686] — deferred, pre-existing. Three magic values target the same "land below the sticky nav" intent: `var(--nav-offset)` (3.75rem) on TOC-targeted sections, `5rem` on `.ac-criterion`, `4.5rem` on `.req-index .section-divider[id]`. Each still clears the nav, so no correctness bug — but if the nav height changes only the `var()`-driven anchors follow. Unify onto `var(--nav-offset)` when this CSS is next touched.
- [x] [Review][Defer] Diff carries changes unrelated to Story 1.3 scope [.github/workflows/publish-docs-live-pages.yml], [README.md] — deferred, out-of-scope. The GitHub Pages deploy-retry workflow and README edits are harmless and match a prior commit's intent, but are not part of Story 1.3's ACs. Confirm they belong in this story's commit or split them out.

## Change Log

- 2026-07-06: Created Story 1.3 implementation context with markdown-fidelity-specific architecture, current-state analysis (both ACs largely implemented), the mermaid-in-artifact-body hardening gap, code-seam guidance, and testing requirements.
- 2026-07-06: Added AC #3 and Task 6 per product-owner direction — relocate the on-page TOC from a top strip into an independently-scrolling sidebar across generic/ADR/README pages and extend a rendered-order TOC to epic/story detail pages. Documented current TOC state (`.toc-strip` in `HtmlTemplater.RenderPage`; detail pages have none), the two-column/sticky layout approach, the rendered-vs-source order requirement (critical for the reordered detail-page layout), and responsive/accessibility guardrails.
- 2026-07-06: Implemented Story 1.3. AC #1: closed the mermaid-in-fragment gap (shared mermaid-aware `RenderBlock` + gated init-script injection on detail pages) and confirmed task-checklist fidelity. AC #2: confirmed AC anchors/deep-links, added edge-case coverage. AC #3: built the shared `Toc` seam + two-column sticky sidebar across all TOC-bearing pages with rendered-order detail-page TOCs. Fixed a latent `PathUtil.StripHtmlTags` multi-line-tag bug surfaced during validation. Added 32 tests (164 total, all green); real generation pass clean (20 pages, 310 TOC anchors, 0 dead links, no console errors).
