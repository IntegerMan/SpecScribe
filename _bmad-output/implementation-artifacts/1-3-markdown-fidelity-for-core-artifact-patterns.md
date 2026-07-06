---
baseline_commit: fb9fb88b7d5ba6d07055a42cbf84a37b21cb4fd0
---

# Story 1.3: Markdown Fidelity for Core Artifact Patterns

Status: ready-for-dev

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

- [ ] Task 1: Verify and harden Mermaid client-side rendering across every rendered page type (AC: #1)
  - [ ] Confirm ` ```mermaid ` fences on full document pages (generic docs + ADRs) still emit `<pre class="mermaid">` via `MermaidCodeBlockRenderer` and are decoded correctly by the client init (`Mermaid.InitScript`)
  - [ ] Confirm the init script is injected exactly on pages that need it: generic/ADR pages when `DocModel.HasMermaid` is true, and the epics index (which always emits the roadmap diagram)
  - [ ] Resolve the fragment-render gap: Mermaid fences authored **inside story/epic artifact bodies** are rendered through `MarkdownConverter.RenderBlock`/`RenderInline`, which do NOT apply the mermaid code-block renderer, so they emit `<code class="language-mermaid">` and never render as diagrams. Decide and implement the smallest correct fix (route fragment rendering through the mermaid-aware renderer and inject the init script on story/epic pages when a fragment actually contains mermaid), or explicitly document it as out-of-scope with rationale if BMad artifacts never carry mermaid in bodies
- [ ] Task 2: Verify and harden task-checklist completion-state rendering (AC: #1)
  - [ ] Confirm GitHub-style `- [ ]`/`- [x]` lists in document bodies render as disabled checkboxes (Markdig `UseTaskLists` via the shared pipeline) with the completed/incomplete swatch styling from `specscribe.css`
  - [ ] Confirm the "## Tasks / Subtasks" section on story pages shows completion states both as the styled checklist (inside `.doc-body`) and as the task sunburst fed by `TaskListParser`
  - [ ] Confirm mixed-case marks (`- [X]`) and nested/subtask indentation render completion state correctly and that non-checkbox bullets are unaffected
- [ ] Task 3: Verify and harden AC-reference deep-linking and tooltips (AC: #2)
  - [ ] Confirm each acceptance criterion renders in its own panel row with a stable `id="ac-N"` anchor and an `.ac-anchor` self-link, and that `:target` highlight styling still fires on navigation
  - [ ] Confirm `EpicsParser.LinkifyAcReferences` rewrites `(AC: #N)` / `(AC #N, #M)` references in the story remainder to `<a class="ac-ref" href="#ac-N" title="…">#N</a>` with the criterion's plain text as the `title` tooltip
  - [ ] Confirm unresolved AC numbers (no matching criterion) remain plain text and never become broken anchors, and that references already inside other markup are not double-linkified
- [ ] Task 4: Add/extend regression coverage for markdown-fidelity behavior at unit and generation levels (AC: #1, #2)
  - [ ] Extend `MarkdownConverterTests` for task-list checkbox output (checked vs. unchecked) and, if Task 1's fix lands, for mermaid inside `RenderBlock` fragments
  - [ ] Extend `EpicsParserTests` for `LinkifyAcReferences` edge cases (comma-grouped numbers, unresolved numbers, tooltip text) and AC-anchor extraction
  - [ ] Add a focused generation-level assertion (alongside `SiteGeneratorTraceabilityTests`) that a rendered story page contains `id="ac-N"` anchors and matching `href="#ac-N"` references, and that a document page with a mermaid fence carries the init script
- [ ] Task 5: Validate end-to-end rendered behavior with the real generation path (AC: #1, #2)
  - [ ] Run the focused test filter for the touched behavior
  - [ ] Run a real generation pass and confirm mermaid diagrams, task checklists, and AC deep-links render correctly with no console errors and no broken anchors

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
  - Current state: `.doc-body`-wrapped body; `HasMermaid`-gated init script on full pages.
  - Story change focus: none expected unless the init-script gating strategy changes.

- `src/SpecScribe/assets/specscribe.css`
  - Current state: styles for task-list checkboxes (`.doc-body ul li input[type="checkbox"]`), `.ac-criterion`/`.ac-anchor`/`.ac-ref`, `.mermaid`, and `.ac-criterion:target`.
  - Story change focus: adjust only if a rendering fidelity/visibility defect is found.

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
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~MarkdownConverter|FullyQualifiedName~EpicsParser|FullyQualifiedName~TaskList|FullyQualifiedName~SiteGenerator"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Verify in generated output: mermaid diagrams render (no console errors), task checklists show correct states, and AC references jump to (and `:target`-highlight) the right criterion.

## UX and Accessibility Requirements

- Mermaid, task lists, and AC deep-links are part of the reviewer reading flow; they must remain readable and keyboard-usable within existing portal semantics. AC references are real anchors (focusable, activatable by keyboard) and carry a `title` tooltip for context. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md`]
- Status/state must never be color-only. Task completion pairs the green fill with a checkmark glyph (shape, not just color); keep that redundancy. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR17; `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`]
- Motion: mermaid initialization and any highlight transitions must respect `prefers-reduced-motion` and stay non-looping. Do not introduce new unbounded animation. [Source: `_bmad-output/planning-artifacts/epics.md` UX-DR18]
- Use the established token/color system and existing component styles rather than ad hoc per-feature styling. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`]

## Reinvention and Regression Guardrails

- Do not add a second markdown pipeline or a bespoke checkbox/diagram renderer; reuse the shared pipeline and `MermaidCodeBlockRenderer`.
- Do not duplicate AC-anchor or AC-reference logic in templates; it lives in `EpicsTemplater` (anchors) + `EpicsParser.LinkifyAcReferences` (references).
- Do not turn unresolved AC numbers into anchors with placeholder targets.
- Do not make baseline generation depend on the mermaid CDN being reachable.
- Do not regress Story 1.1 navigation/breadcrumbs or Story 1.2 requirement/source/ADR linkification while touching rendering.
- Keep changes host-neutral so HTML/webview parity is not made harder.

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
- Primary open work is the fragment-render mermaid gap (`RenderBlock`/`RenderInline` bypass the mermaid renderer; story/epic pages omit the init script) plus regression coverage — production changes may be minimal or none if BMad artifacts never carry mermaid in bodies.

### File List

- _bmad-output/implementation-artifacts/1-3-markdown-fidelity-for-core-artifact-patterns.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-07-06: Created Story 1.3 implementation context with markdown-fidelity-specific architecture, current-state analysis (both ACs largely implemented), the mermaid-in-artifact-body hardening gap, code-seam guidance, and testing requirements.
