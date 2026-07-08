---
baseline_commit: f1781b1e22ce7367a66e65c3652390666fd09704
---

# Story 2.6: Render Markdown Comments as Visible Annotations

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader of generated documents,
I want authored HTML comments surfaced as visible, de-emphasized annotations,
so that the context authors leave in comments (for example "sync this back into the PRD later") is not lost in the rendered portal.

## Acceptance Criteria

1. **Given** a source document contains HTML comments (`<!-- ... -->`) that today render as invisible raw HTML
   **When** the page is generated
   **Then** those comments render as visible, de-emphasized annotations (italicized or blockquote-styled asides) in their original document position
   **And** both multi-line block comments and inline comments render coherently.

2. **Given** a document mixes prose, headings, and comments
   **When** it renders
   **Then** comment annotations use a consistent side-note style clearly distinct from body text and do not disrupt the surrounding markdown
   **And** malformed, nested, or unterminated comments degrade non-fatally without breaking the page.

> **Origin & scope:** Sixth and final story of Epic 2 (Complete and Faithful BMad Artifact Representation).
> Today Markdig parses `<!-- ... -->` into `HtmlBlock`/`HtmlInline` nodes and the **default** Markdig HTML
> renderer writes the raw comment text straight into the output — browsers then hide it as an actual HTML
> comment, so authored context (e.g. `epics.md`'s own `<!-- FR15–FR19 added post-PRD ... -->` note) silently
> vanishes from every generated page. This story swaps in two small custom renderers (mirroring the existing
> `MermaidCodeBlockRenderer` wrap/fallback pattern) that intercept only the comment case and render it as a
> visible, muted annotation; every other HTML block/inline passes through to Markdig's default renderer
> unchanged. It advances **FR7** (markdown fidelity) and is a **pure rendering-layer enrichment**: no new
> parser, no new artifact class, no new page, no new dependency, no JS.

## Tasks / Subtasks

- [ ] Task 1: Add the comment-aware HTML renderers (AC: #1, #2)
  - [ ] Create `src/SpecScribe/CommentAnnotationRenderer.cs` with two `HtmlObjectRenderer<T>` subclasses, following the exact wrap/fallback shape of `MermaidCodeBlockRenderer` (`src/SpecScribe/MermaidCodeBlockRenderer.cs:12-51`): a private `_fallback` field set from the constructor, `Write` overrides that special-case the comment and otherwise call `_fallback.Write(renderer, obj)` unchanged.
    - `HtmlBlockCommentRenderer : HtmlObjectRenderer<HtmlBlock>` wraps the default `Markdig.Renderers.Html.HtmlBlockRenderer`. When `obj.Type == HtmlBlockType.Comment`, extract the raw lines (same `block.Lines.Lines` iteration `MermaidCodeBlockRenderer.ExtractSource` uses, `MermaidCodeBlockRenderer.cs:39-50`), strip the leading `<!--` / trailing `-->` markers and surrounding whitespace, HTML-encode the remaining text with `PathUtil.Html(...)` (`src/SpecScribe/PathUtil.cs:24`), and write `<aside class="md-comment">{encoded text, internal newlines as &lt;br&gt;}</aside>`. Any other `HtmlBlockType` delegates to `_fallback.Write(renderer, obj)`.
    - `HtmlInlineCommentRenderer : HtmlObjectRenderer<HtmlInline>` wraps the default `Markdig.Renderers.Html.Inlines.HtmlInlineRenderer`. When `obj.Tag` starts with `"<!--"` and ends with `"-->"`, strip the markers, HTML-encode the inner text, and write `<span class="md-comment-inline">{encoded text}</span>`. Otherwise delegate to `_fallback.Write(renderer, obj)` (which itself only writes when `renderer.EnableHtmlForInline` — preserve that check by calling the fallback rather than reimplementing it).
  - [ ] **Graceful degradation is automatic, not bolted on:** an unterminated `<!-- never closed` does not parse as `HtmlBlockType.Comment`/a `<!--...-->`-shaped `HtmlInline.Tag` in Markdig's CommonMark-conformant HTML grammar, so it simply falls through to the existing fallback renderer (today's raw-passthrough behavior) — no exception path to write, just confirm this with a test (Task 4).

- [ ] Task 2: Wire both renderers into the one shared pipeline (AC: #1, #2)
  - [ ] In `src/SpecScribe/MarkdownConverter.cs`, add a `UseCommentAnnotations(HtmlRenderer renderer)` method mirroring `UseMermaidCodeBlocks` (`MarkdownConverter.cs:76-83`): swap out the registered `HtmlBlockRenderer` for `HtmlBlockCommentRenderer` and the registered `HtmlInlineRenderer` for `HtmlInlineCommentRenderer` via `renderer.ObjectRenderers.OfType<T>().FirstOrDefault()` → `Remove` → `Add(new Wrapper(existing))`. Call it from `RenderDocumentHtml` (`MarkdownConverter.cs:62-71`) right after `UseMermaidCodeBlocks(renderer)`, before `renderer.Render(document)` — same "must run after `Pipeline.Setup`" ordering constraint the mermaid swap already documents.
  - [ ] **Fix the second, currently-unswapped rendering path.** `RenderInline` (`MarkdownConverter.cs:102-112`) calls `Markdown.ToHtml(markdown, Pipeline)` directly — a shortcut that builds its own throwaway `HtmlRenderer` internally and never goes through `RenderDocumentHtml`, so it would keep leaking raw `<!-- -->` even after Task 1/2. Change it to `var document = Markdown.Parse(markdown, Pipeline); var html = RenderDocumentHtml(document).Trim();` so it reuses the exact same renderer-swap path as `Convert`/`RenderBlock`. This is not optional: `EpicsParser` calls `MarkdownConverter.RenderInline` for epic/story titles, goals, blurbs, AC lines, Gherkin steps, and user-story text (9 call sites — `EpicsParser.cs:48,49,228,435,485,500,502,528,529`), and `RequirementsParser.cs:124` uses it for requirement text — all of these are realistic places an author's inline comment could appear. Leave the existing paragraph-unwrap logic (lines 106-110) unchanged — it operates on the resulting HTML string regardless of which call produced it.
  - [ ] Update the `RenderDocumentHtml` XML doc comment (`MarkdownConverter.cs:57-61`) — it currently says "Shared by `Convert` and `RenderBlock`"; after this task it is also shared by `RenderInline`.

- [ ] Task 3: Self-contained CSS for the annotation style (AC: #2)
  - [ ] Add `.doc-body .md-comment` and `.doc-body .md-comment-inline` rules to `src/SpecScribe/assets/specscribe.css`, placed near the existing `.doc-body blockquote` rule (`specscribe.css:373-380`). Reuse the established muted-text vocabulary (`var(--ink-light)`, `font-style: italic` — the same pattern `.pending-note`/`.dev-agent-empty`/`.sprint-retro-note` already use at `specscribe.css:828,1186,2245`) but keep it **visually distinct from `.doc-body blockquote`** so a reader doesn't mistake an authored comment for an authored quote — e.g. a different border-left token (`--ink-light` or `--border` rather than blockquote's `--gold-light`) and a smaller `font-size`. Do not introduce a new color token; only reuse existing `--ink-*`/`--warm-white`/`--parchment-dark`/`--border` variables (`specscribe.css:6-41`).
  - [ ] No new stylesheet, no icon, no JS. Icons are explicitly out of scope here — Story 2.5's `Icons`/`StatusStyles.Badge` seam is for artifact-type/status concepts, not prose annotations; do not touch it.

- [ ] Task 4: Test coverage in the existing `MarkdownConverterTests.cs` (AC: #1, #2)
  - [ ] Follow this file's exact house pattern (no new test class — mirror how `Convert_RendersMermaidFencesAsClientRenderBlocks` / `RenderBlock_RendersTaskListCheckboxesWithCompletionState` sit directly in `tests/SpecScribe.Tests/MarkdownConverterTests.cs` rather than a dedicated renderer test file):
    - A block comment (`<!--\nnote\n-->` on its own lines) renders `class="md-comment"` and the output does **not** contain the literal `<!--`/`-->` markers.
    - An inline comment (`text <!-- aside --> more text`) renders `class="md-comment-inline"` with the surrounding prose intact.
    - `RenderInline` also converts a comment (regression test for the `Markdown.ToHtml` → `Markdown.Parse`/`RenderDocumentHtml` fix in Task 2) — e.g. `RenderInline("Some **bold** <!-- todo --> text")` contains `md-comment-inline` and no raw `<!--`.
    - `RenderBlock` also converts a comment (fragment path, mirroring `RenderBlock_RendersMermaidFencesAsClientRenderBlocks`).
    - An unterminated comment (`"<!-- never closed\n\nMore text."`) does not throw and the surrounding text still renders — mirror the existing degrade-gracefully pattern at `MarkdownConverterTests.cs:139-148` (`StripFrontmatter_HandlesPresenceAbsenceAndUnterminatedBlocks`) and `:150-157` (`Convert_MalformedYamlFallsBackToBodyContent`).
    - A comment containing HTML-special characters (e.g. `<!-- see <Foo> & "bar" -->`) is HTML-encoded in the annotation output (`&lt;Foo&gt;`, `&amp;`) — no raw tag injection, no broken markup.
    - Ordinary content with no comments is unaffected: assert `Assert.DoesNotContain("md-comment", ...)` for a plain doc, mirroring `RenderBlock_LeavesNonCheckboxBulletsUnaffected` (`MarkdownConverterTests.cs:217-224`).
    - Existing mermaid tests (`Convert_RendersMermaidFencesAsClientRenderBlocks`, `RenderBlock_RendersMermaidFencesAsClientRenderBlocks`, `Convert_OrdinaryCodeFencesAreUntouched`) and task-list tests must keep passing unchanged — the renderer-swap chain now carries three swapped renderer types (code block, HTML block, HTML inline) instead of one; nothing about that changes the code-block path.

- [ ] Task 5: End-to-end validation with a real generation pass (AC: #1, #2)
  - [ ] Run the focused filter, then a full-suite run, then a real generation pass:
    - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~MarkdownConverter"`
    - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` (full suite — no regressions)
    - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output SpecScribeOutput --project-name SpecScribe` (per memory: `SpecScribeOutput` is the real output dir; `docs/live` is stale/vestigial — do not use it)
  - [ ] Manually verify: this repo's own `_bmad-output/planning-artifacts/epics.md:52` comment (`<!-- FR15–FR19 added post-PRD (2026-07-06) to seat the reordered roadmap... -->`) now renders as a visible muted annotation wherever the Requirements Inventory section is rendered, instead of vanishing. Spot-check a story/epic page for an inline comment rendering coherently mid-sentence, and confirm no malformed HTML anywhere in the generated output. Delete the scratch `SpecScribeOutput` directory after inspection (gitignored).

## Developer Context Section

### Epic Context and Business Value

Epic 2 — "Complete and Faithful BMad Artifact Representation" — makes the portal reflect the **whole** project
truthfully. Story 2.1 surfaced quick-dev/deferred work; 2.2 surfaced the spec kernel; 2.3 the sprint tracking;
2.4 planning-doc prominence and status badges; 2.5 added a recognition layer via consistent iconography.
**Story 2.6 closes Epic 2** by making authored HTML comments — a real authoring convention already used in
this very repo (see `epics.md:52`) — visible instead of silently discarded, so context authors leave for
future readers/maintainers survives into the generated portal. It advances **FR7** (markdown fidelity,
alongside Mermaid and task lists from Epic 1) and is a **pure rendering-layer fix**: unlike 2.1–2.5, it touches
no templater, no new artifact class, no new nav/page — it lives entirely inside the one shared Markdig pipeline.

### Story Foundation Extract

- **Primary concern:** `<!-- ... -->` HTML comments in source markdown are parsed by Markdig but rendered
  invisibly (as literal HTML comments) by its default renderer. This story makes them visible, de-emphasized,
  in-place annotations instead.
- **User outcome:** a reader browsing a generated page sees an author's aside (e.g. "sync this back into the
  PRD later") as a small muted note, in the exact spot it was authored, rather than losing it entirely.
- **Success boundary:** two small custom Markdig HTML renderers (block + inline) that intercept only the
  comment case and fall back to Markdig's defaults for everything else, wired into the one shared pipeline so
  every render path (full page, block fragment, inline fragment) picks it up automatically.
- **Regression boundary:** every other HTML block/inline (there are none currently exercised in this codebase
  per the research pass — no existing test relies on raw HTML passthrough) must be untouched; mermaid fences,
  task-list checkboxes, tables, and color swatches must keep rendering exactly as before.

### Current Implementation Reality (READ THIS FIRST)

- **There is no comment-handling code today.** Grepping `src/` and `tests/` for `comment`, `HtmlBlock`,
  `HtmlInline`, `IMarkdownExtension` returns nothing in production/test C# files. This is genuinely new ground —
  no existing behavior to preserve beyond "don't break other renderer-swap consumers" (mermaid).
- **The one shared pipeline.** `MarkdownConverter.Pipeline` (`MarkdownConverter.cs:15-17`) is a single
  `private static readonly MarkdownPipeline` built with only `.UseAdvancedExtensions()` — no `.DisableHtml()`
  call anywhere, so raw HTML (including comments) passes through by default. Every conversion entry point
  (`Convert`, `RenderInline`, `RenderBlock`, `RenderDocumentHtml`) references this one instance — a
  pipeline/renderer-level fix here reaches every caller with no per-templater changes.
- **Markdig parses `<!-- -->` into two different node types depending on position:** a comment that starts its
  own line(s) becomes an `HtmlBlock` with `Type == HtmlBlockType.Comment`; a comment embedded mid-line becomes
  an `HtmlInline` whose `.Tag` is the literal `"<!--...-->"` string. Both must be handled — that's why AC#1
  calls out "both multi-line block comments and inline comments."
  [Verified against Markdig 1.3.2 source: `HtmlBlockType` enum has `Comment` as one of seven values;
  `HtmlInlineRenderer.Write` is `if (renderer.EnableHtmlForInline) renderer.Write(obj.Tag);` — no comment-aware
  branching in either default renderer today, confirming the raw-passthrough bug.]
- **The established idiomatic pattern for a targeted renderer override already exists in this codebase:**
  `MermaidCodeBlockRenderer` (`src/SpecScribe/MermaidCodeBlockRenderer.cs`) wraps `CodeBlockRenderer`, special-
  cases mermaid fences, and delegates everything else to the wrapped `_fallback`. It is registered by removing
  the default renderer from `renderer.ObjectRenderers` and adding the wrapper in its place
  (`MarkdownConverter.UseMermaidCodeBlocks`, `MarkdownConverter.cs:76-83`), called from
  `RenderDocumentHtml` (`MarkdownConverter.cs:62-71`) *after* `Pipeline.Setup(renderer)` registers the
  defaults. **Follow this exact shape** for the comment renderers — do not write a custom `IMarkdownExtension`
  or parser (none exist in this repo; all customization happens at the renderer layer).
- **A second, separate render path bypasses the swap entirely today.** `RenderInline` (`MarkdownConverter.cs:
  102-112`) calls `Markdown.ToHtml(markdown, Pipeline)`, which internally builds its own `HtmlRenderer` and
  never touches `RenderDocumentHtml`/the mermaid-or-comment renderer swaps. This is why `RenderInline` still
  renders `language-mermaid` code fences as inert `<code>` rather than client diagrams if ever asked to (not a
  bug in scope here) — but it **is** directly in scope for this story, because `RenderInline` is the single
  most-used entry point for short text fragments (titles, goals, AC lines, user-story text) where an author's
  inline comment is realistically going to appear. Task 2 fixes this by routing `RenderInline` through
  `RenderDocumentHtml` like the other two entry points.
- **HTML-escaping precedent already exists.** `PathUtil.Html(string s) => WebUtility.HtmlEncode(s)`
  (`src/SpecScribe/PathUtil.cs:24`) is the house helper for encoding user/authored text into safe HTML — reuse
  it for the comment's inner text rather than writing a new encoder (comment text is untrusted authored
  content and must not inject raw markup into the page).

### Scope Boundaries

- **IN (this story):** two new comment-aware `HtmlObjectRenderer` wrappers (block + inline); wiring them into
  `RenderDocumentHtml`; fixing `RenderInline` to route through the same swapped-renderer path; a small
  `.md-comment`/`.md-comment-inline` CSS rule reusing existing muted-text tokens; test coverage in
  `MarkdownConverterTests.cs`; a real generation pass confirming the repo's own `epics.md` comment now renders.
- **OUT:** any icon on the annotation (Story 2.5's `Icons`/`StatusStyles.Badge` seam is for artifact-type/status
  concepts, not prose asides — do not touch it); any new artifact class, page, or nav entry; any change to how
  Mermaid fences, task lists, or tables render; any change to `ColorSwatchRewriter`/`TagTables` (unrelated
  post-processing passes, leave them exactly as chained today); dark-mode implementation (icon/comment CSS
  already inherits theme via existing tokens — no new work needed here); any new Markdig extension package or
  `.DisableHtml()` change (would silently break other raw-HTML passthrough this repo may still rely on
  elsewhere, e.g. within existing docs — out of scope to audit that here, so keep the change additive/targeted).

### Previous Story Intelligence

- **2.5 (icons, `review`)** is the immediate predecessor but has almost no coupling to this story — it added a
  `.status-badge` icon-render helper and nav/section icons, all orthogonal to prose-comment rendering. The one
  relevant carryover: 2.5 confirmed and reused the codebase's existing muted-text CSS vocabulary
  (`--ink-light`, italic) rather than inventing new tokens — do the same here for `.md-comment`.
  [`_bmad-output/implementation-artifacts/2-5-standardized-iconography-for-artifact-types-and-status.md`]
- **Environment gotcha (confirmed again in 2.5's own dev notes):** `python`/`python3` are not reliably on PATH
  on this Windows host for the `resolve_customization.py` helper script — this create-story run used the
  Microsoft Store `python3` alias fallback (`python _bmad/scripts/resolve_customization.py ...` worked; plain
  `python3` did not). Not relevant to implementation, only to future BMAD tooling runs.
- **1.4 (accessibility contract)** and **1.5 (truthfulness)** remain the accessibility/regression floor:
  single `<main id="main-content">`, skip link, focus-visible, `aria-hidden` on purely decorative marks. The
  new `<aside>`/`<span>` annotation markup carries **visible, meaningful text** (not decorative), so it should
  **not** be `aria-hidden` — it should read normally to assistive tech, same as any other prose. No new
  landmark is introduced (`<aside>` here is a local flow element inside `.doc-body`, not a page landmark).

### Architecture Compliance

- **One shared pipeline, renderer-layer customization only.** All comment-handling logic lives in the two new
  wrapper classes; no new `IMarkdownExtension`, no new parser, no second Markdig pipeline instance. This
  matches the only existing precedent (`MermaidCodeBlockRenderer`) and keeps `MarkdownConverter.Pipeline` the
  single source of Markdown configuration truth. [Source: `src/SpecScribe/MarkdownConverter.cs:15-17`]
- **Every render path must carry the fix, not just full-page conversion.** `Convert`, `RenderBlock`, and (after
  Task 2) `RenderInline` all funnel through `RenderDocumentHtml`, so the comment renderer applies uniformly to
  full pages, block fragments (Overview sections, story remainders, dev-agent records), and inline fragments
  (titles, goals, AC lines) alike — this was explicitly verified as a gap in the current `RenderInline`
  implementation and is why Task 2 is not optional.
- **HTML-escape authored text — untrusted content boundary.** Comment text is authored freeform prose; reuse
  `PathUtil.Html` rather than writing raw text into the output, matching how the rest of the codebase treats
  authored strings destined for HTML (e.g. `StatusStyles.Badge`'s `Html(label)` call per Story 2.5).
- **Self-contained, static-host-safe.** One new small CSS rule in the already-embedded `specscribe.css`; no
  loose asset, no JS, no external dependency — consistent with NFR6/GitHub Pages/Epic 6 webview constraints
  every prior Epic 2 story has preserved.

## Technical Requirements

- Add `src/SpecScribe/CommentAnnotationRenderer.cs`: `HtmlBlockCommentRenderer` (wraps `HtmlBlockRenderer`,
  intercepts `HtmlBlockType.Comment`) and `HtmlInlineCommentRenderer` (wraps `HtmlInlineRenderer`, intercepts
  `HtmlInline.Tag` starting with `<!--` and ending with `-->`), both following the
  `MermaidCodeBlockRenderer` wrap/fallback shape exactly (constructor-injected `_fallback`, delegate for the
  non-matching case).
- Comment content is HTML-encoded via `PathUtil.Html(...)` before being written; block comments render as
  `<aside class="md-comment">...</aside>` (internal newlines as `<br>`), inline comments as
  `<span class="md-comment-inline">...</span>`.
- Wire both into `MarkdownConverter.RenderDocumentHtml` via a new `UseCommentAnnotations(HtmlRenderer renderer)`
  method mirroring `UseMermaidCodeBlocks`, called after `Pipeline.Setup(renderer)` and `UseMermaidCodeBlocks`.
- Change `RenderInline` to parse + call `RenderDocumentHtml` (instead of `Markdown.ToHtml(markdown, Pipeline)`)
  so the renderer swap applies to inline fragments too; keep the existing single-paragraph unwrap logic
  unchanged.
- Add `.doc-body .md-comment` / `.doc-body .md-comment-inline` to `specscribe.css`, reusing existing
  `--ink-light`/muted-text tokens, visually distinct from `.doc-body blockquote` (different border/size), no
  new color tokens, no icon.
- Unterminated/malformed comments must not throw — verify via test that they fall through to existing raw-HTML
  passthrough behavior rather than crashing generation (NFR2).

## File Structure Requirements

Primary NEW:

- `src/SpecScribe/CommentAnnotationRenderer.cs` — the two comment-aware renderer wrappers.

Primary UPDATE:

- `src/SpecScribe/MarkdownConverter.cs` — add `UseCommentAnnotations`, call it from `RenderDocumentHtml`, fix
  `RenderInline` to route through `RenderDocumentHtml`, update the `RenderDocumentHtml` doc comment.
- `src/SpecScribe/assets/specscribe.css` — add `.md-comment` / `.md-comment-inline` rules near the existing
  `.doc-body blockquote` rule.

Primary TEST updates:

- `tests/SpecScribe.Tests/MarkdownConverterTests.cs` — block/inline comment rendering, `RenderInline`/
  `RenderBlock` regression coverage, unterminated-comment non-fatal degrade, HTML-escaping of comment text,
  no-comment-present unaffected case. No new test file — mirror the existing mermaid/task-list tests living
  directly in this file.

## Library and Framework Requirements

- Stay on Markdig 1.3.2 (`src/SpecScribe/SpecScribe.csproj`), the existing `.UseAdvancedExtensions()` pipeline,
  and `Markdig.Renderers.Html`/`Markdig.Renderers.Html.Inlines` namespaces already referenced by
  `MermaidCodeBlockRenderer.cs`. No new NuGet package, no new Markdig extension. Confirmed against Markdig
  1.3.2 source: `HtmlBlockType` is a 7-value enum including `Comment`; the default `HtmlBlockRenderer.Write`
  calls `renderer.WriteLeafRawLines(obj, true, false)` uniformly (no type branching) — that undifferentiated
  passthrough is exactly what the block wrapper must intercept for `HtmlBlockType.Comment` only; the default
  `HtmlInlineRenderer.Write` is a one-line `if (renderer.EnableHtmlForInline) renderer.Write(obj.Tag);` — the
  inline wrapper must preserve that `EnableHtmlForInline` gate by delegating to the fallback, not reimplementing
  it.

## Testing Requirements

- Preserve all existing coverage — full suite (`dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj`, no
  filter) must stay green, including every mermaid, task-list, table, and frontmatter test in
  `MarkdownConverterTests.cs`.
- Add (Task 4): block comment → `md-comment` + no raw markers; inline comment → `md-comment-inline`;
  `RenderInline` comment regression; `RenderBlock` comment coverage; unterminated comment does not throw;
  comment text is HTML-escaped; plain content without comments has no `md-comment` anywhere.
- Run targeted then full suite, then a real generation pass (Task 5 commands) and manually confirm this repo's
  own `epics.md:52` comment renders visibly, with `SpecScribeOutput` (not `docs/live`, per memory) as the
  output directory, deleting the scratch output after inspection.

## UX and Accessibility Requirements

- Comment annotations are **visible, meaningful content** — not decorative — so they are **not** `aria-hidden`;
  screen readers announce them like any other prose. No `role="note"` or extra ARIA is required beyond standard
  semantic HTML; do not over-engineer this into a landmark.
- Antiquarian visual language: muted italic text reusing existing `--ink-light`/`--warm-white`/`--parchment-
  dark`/`--border` tokens (no new hex values), visually distinct from the existing `.doc-body blockquote` style
  so a reader can tell "this is an author's aside" from "this is a quoted excerpt."
- No motion, no icon, no interactive element — trivially satisfies `prefers-reduced-motion` (UX-DR18) with zero
  additional work, and needs no `aria-hidden`/focus-ring handling since nothing is decorative or interactive.
- Malformed/nested/unterminated comments must degrade non-fatally (NFR2) — never a thrown exception, never
  broken/unbalanced HTML in the output.

## Reinvention and Regression Guardrails

- Do NOT write a custom `IMarkdownExtension` or a second `MarkdownPipeline` — this codebase's only precedent
  (Mermaid) customizes at the renderer layer on the one shared pipeline; follow that shape exactly.
- Do NOT call `.DisableHtml()` on the pipeline or otherwise broadly change how raw HTML is handled — the fix
  must be narrowly scoped to the comment case; every other `HtmlBlock`/`HtmlInline` must fall through to
  Markdig's default renderer unchanged.
- Do NOT skip the `RenderInline` fix — it is the highest-traffic entry point (9+ call sites across
  `EpicsParser`/`RequirementsParser`) and currently bypasses the renderer-swap chain entirely via
  `Markdown.ToHtml`.
- Do NOT write raw (non-HTML-encoded) comment text into the output — reuse `PathUtil.Html`.
- Do NOT add an icon to the annotation or touch `Icons.cs`/`StatusStyles.Badge` — that seam is for artifact-
  type/status concepts (Story 2.5), not prose asides.
- Do NOT introduce a new CSS color token — reuse existing `--ink-*`/`--warm-white`/`--parchment-dark`/`--border`
  variables, and keep the new rule visually distinct from `.doc-body blockquote`.
- Do NOT regress mermaid fence rendering, task-list checkboxes, `md-table` tagging, or `ColorSwatchRewriter` —
  all three renderer-swap/post-process passes must keep composing correctly with the new comment renderers.

## Git Intelligence Summary

- Baseline `f1781b1` (main, "2.5 dev" — the most recent commit at story-creation time). Story 2.5's icon work
  (`Icons.cs`, `StatusStyles.Badge`) has already landed on `main`; it is unrelated to this story's scope and
  should not be touched.
- `MarkdownConverter.cs` and `MermaidCodeBlockRenderer.cs` are the two files this story extends the pattern
  from — both are stable, unmodified by any pending Epic 2 story, safe to build on directly.
- No test file currently exercises raw HTML comment behavior anywhere in the suite — this is genuinely new
  ground with no risk of colliding with another in-flight story's test expectations.

## Latest Technical Information

- Markdig 1.3.2 is already pinned (`src/SpecScribe/SpecScribe.csproj`) — no version change needed. Verified
  directly against the 1.3.2 source on GitHub (`xoofx/markdig` tag `1.3.2`):
  `src/Markdig/Syntax/HtmlBlockType.cs` defines `DocumentType`, `CData`, `Comment`, `ProcessingInstruction`,
  `ScriptPreOrStyle`, `InterruptingBlock`, `NonInterruptingBlock`; `src/Markdig/Renderers/Html/
  HtmlBlockRenderer.cs`'s `Write` method is `renderer.WriteLeafRawLines(obj, true, false)` with no per-type
  branching; `src/Markdig/Renderers/Html/Inlines/HtmlInlineRenderer.cs`'s `Write` method is
  `if (renderer.EnableHtmlForInline) renderer.Write(obj.Tag);`. Both confirm there is no existing comment-aware
  logic to conflict with or reuse — the wrapper renderers are purely additive.

## Project Context Reference

- Epic + story source: `_bmad-output/planning-artifacts/epics.md:373-391` (Epic 2, Story 2.6; FR7)
- Shared pipeline to extend: `src/SpecScribe/MarkdownConverter.cs` (`Pipeline`, `RenderDocumentHtml`,
  `UseMermaidCodeBlocks`, `RenderInline`, `RenderBlock`)
- Renderer-wrap pattern to mirror: `src/SpecScribe/MermaidCodeBlockRenderer.cs`
- HTML-encode helper to reuse: `src/SpecScribe/PathUtil.cs:24` (`PathUtil.Html`)
- CSS muted-text vocabulary to reuse: `src/SpecScribe/assets/specscribe.css:6-41` (tokens), `:373-380`
  (`.doc-body blockquote`, closest existing aside pattern)
- Render-path callers that benefit automatically once the pipeline is fixed: `src/SpecScribe/SiteGenerator.cs`
  (`Convert` call sites), `src/SpecScribe/EpicsParser.cs` (`RenderInline`/`RenderBlock`, 13 call sites),
  `src/SpecScribe/RequirementsParser.cs:124` (`RenderInline`)
- Predecessor: `_bmad-output/implementation-artifacts/2-5-standardized-iconography-for-artifact-types-and-status.md`
- Accessibility baseline: `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`
- Real-world example this story fixes: `_bmad-output/planning-artifacts/epics.md:52` (the FR15–FR19 HTML
  comment currently vanishes from every rendered page that surfaces the Requirements Inventory)
- Memory: [[generate-output-dir-is-specscribeoutput]] (use `--output SpecScribeOutput`, not `docs/live`)

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed — comprehensive developer guide created for
  Epic 2's closing story: a pure rendering-layer fix that adds two small comment-aware Markdig renderer
  wrappers (block + inline) mirroring the existing `MermaidCodeBlockRenderer` pattern, wires them into the one
  shared pipeline, fixes the previously-unswapped `RenderInline` path so the fix reaches every render surface
  (full pages, block fragments, inline fragments — 13+ call sites across `EpicsParser`/`RequirementsParser`),
  and adds one small self-contained CSS rule reusing existing muted-text tokens. Verified directly against
  Markdig 1.3.2 source for the exact default-renderer behavior being intercepted. No new dependency, page, nav
  item, icon, or JS.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-07: Created Story 2.6 as Epic 2's closing story. Scoped: two new Markdig `HtmlObjectRenderer`
  wrappers (block comment → `<aside class="md-comment">`, inline comment → `<span class="md-comment-inline">`)
  following the existing `MermaidCodeBlockRenderer` wrap/fallback pattern, wired into the one shared
  `MarkdownConverter.Pipeline` via `RenderDocumentHtml`; `RenderInline` fixed to route through the same swapped
  path (previously bypassed it via `Markdown.ToHtml`, the highest-traffic entry point across
  `EpicsParser`/`RequirementsParser`); one small CSS rule reusing existing muted-text tokens, visually distinct
  from `.doc-body blockquote`; comment text HTML-encoded via the existing `PathUtil.Html` helper; malformed/
  unterminated comments degrade non-fatally by construction (Markdig's own HTML-comment grammar). No new
  dependency, page, nav item, icon, or JS. Baseline `f1781b1`.
