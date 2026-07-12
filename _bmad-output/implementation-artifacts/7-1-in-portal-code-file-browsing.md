---
baseline_commit: 37ec9802d53358c0ace6800de12da160c807283b
---

# Story 7.1: In-Portal Code File Browsing

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reviewer,
I want project source files rendered as readable pages,
so that I can inspect referenced code without leaving the portal.

## Acceptance Criteria

1.
**Given** the project has source files referenced by planning or implementation artifacts
**When** the site is generated
**Then** referenced code files render as syntax-readable, navigable pages
**And** non-referenced or excluded files are omitted gracefully without broken navigation. [Source: epics.md#Story 7.1; FR15]

2.
**Given** a rendered code file page
**When** I open it
**Then** I can navigate to specific lines via stable anchors
**And** the page degrades safely for very large, binary, or unreadable files. [Source: epics.md#Story 7.1; FR15]

---

## Developer Context

**This is the first story of Epic 7 (Code and Git Exploration) and the foundation for the whole epic.** It introduces a brand-new artifact class — **code file pages** — into a portal that until now has only rendered `*.md` artifacts, ADRs, and synthesized commit-day pages. Get the page shape, the referenced-file discovery, and the output-path/anchor conventions right, because Stories 7.2–7.4 all build directly on them:

- **7.2** rewrites `[Source: path:line]` citations and "View source" links to point at the code pages this story creates (using the line anchors this story defines) and adds back-navigation.
- **7.3** adds date/timeline pages that will cross-link to code pages.
- **7.4** enriches code pages with opt-in blame/hotspot annotations.

**Scope boundary (read this carefully):** This story **renders the pages and defines the referenced-file set + anchor scheme**. It does **NOT** rewrite the existing citations in story/ADR/doc bodies to link to these pages — that is Story 7.2. Today those citations render as dead links to `../../src/...` (relative paths that point outside `SpecScribeOutput`); leave them alone here. Your job is to make the target pages exist, be reachable by stable URL, and carry line anchors. Provide a clean, reusable seam (a cached path map keyed by repo-relative source path → code-page output path) that 7.2 will consume. Resist building the citation linkifier now.

### The core design in one paragraph

Enumerate the source-artifact markdown corpus, extract every citation that points at a **real repository file** (a code/source file under `RepoRoot` that is *not* one of the generated `_bmad-output/*.md` docs), resolve each to a repo-relative path, dedupe, and render each referenced+readable file into `code/<repo-relative-path>.html` — a line-numbered, HTML-escaped, monospace page with a stable `id="L{n}"` anchor per line, wrapped in the standard nav/breadcrumb/`<main>` a11y shell. Wipe-and-recreate the `code/` output directory each full pass (atomic-rebuild parity). Files that are not referenced, don't exist, escape the repo root, are binary, are too large, or can't be read are omitted or replaced with a safe placeholder — never a thrown exception, never a broken nav entry.

### Why the "referenced" set (not "all files")

FR15 and AC #1 both scope this to files **"referenced by planning or implementation artifacts."** Do **not** walk the whole repo tree and render every `.cs`/`.ts`/etc. — that would be huge, slow (NFR1), and mostly noise. The referenced set is small, purposeful, and exactly the set 7.2's citations will want to link to. "Non-referenced ... files are omitted gracefully" is satisfied precisely *because* you only render the referenced set.

### Configuration — code link strategy (in-portal pages vs. external base URL)

A project like SpecScribe hosts its **docs** on GitHub Pages but its **code** on GitHub proper. For such a project, linking a source citation to the real GitHub file (`https://github.com/IntegerMan/SpecScribe/blob/main/src/SpecScribe/SiteGenerator.cs#L42`) is more useful than an in-portal render — and it hands syntax highlighting to GitHub for free, which is exactly why we take no highlighter dependency. So code linking is a **configurable strategy**, driven by one optional setting:

- **`CodeSourceBaseUrl` unset (default) → in-portal pages.** Generate `code/<path>.html` as this story specifies. Citations resolve to those pages (in 7.2).
- **`CodeSourceBaseUrl` set → external links, no in-portal pages.** **Skip** the in-portal code-page generation phase entirely (don't render pages nothing will link to), and citations resolve to `{CodeSourceBaseUrl}/<repo-relative-path>#L{n}` (in 7.2).

Two facts make this a small, clean addition rather than a fork:

1. **The line-anchor convention is identical in both modes.** GitHub uses `#L{n}`; so does our in-portal page. 7.2's fragment logic is mode-agnostic — only the base (a local `code/…html` href vs. the configured absolute URL) differs. This is another reason to lock `id="L{n}"` now.
2. **The settings mechanism already exists.** `SiteSettings` (CLI `[CommandOption]`) → `ForgeOptions`, with `SavedSettings`/`SettingsStore` persistence and "CLI wins over saved" semantics. Add `CodeSourceBaseUrl` following the exact `--adrs`/`--output` precedent — this does **not** depend on the not-yet-done Story 5.2. [Source: `src/SpecScribe/SiteSettings.cs`, `src/SpecScribe/SettingsStore.cs`, `src/SpecScribe/ForgeOptions.cs:53-99`]

**Scope split with 7.2 (important):** this story **introduces the setting, plumbs it through, and gates in-portal generation on it** (skip when a base URL is set). The *citation resolution* — turning a `[Source: …]` into either an in-portal-page link or an external URL — is Story 7.2. So 7.1 must not build the external-URL linkifier; it just makes the mode selectable and honors it at the generation gate, and caches enough for 7.2 (the mode/base URL live on `ForgeOptions`, already available to 7.2). Because this setting spans both stories, **flag it into Story 7.2's context when 7.2 is drafted** (and consider a one-line epic note on FR15).

**Out of scope (note, don't build):** auto-deriving the base URL from `git remote get-url origin` + branch/commit. The user asked for *configuration-based*, so keep it an explicit setting; git auto-detect is a reasonable later enhancement but adds branch/commit-resolution complexity now.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Render only referenced, readable repo files.** The referenced set is derived from source-artifact citations, not a filesystem walk.
- **Use stable per-line anchors:** `<span class="code-line" id="L{n}">…` (GitHub-style `L1`, `L2`, …). 7.2 will emit `code/<path>.html#L42`, so the anchor id **must** be `L42` — lock this convention now and cover it with a test.
- **HTML-escape every line** via `PathUtil.Html(...)`. Source files contain `<`, `>`, `&`, `"` constantly; unescaped output is both broken and an injection vector.
- **Read files with shared, read-only access** — never hold a write lock (NFR5, and watch mode observes these files). Reuse the `FileStream(..., FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)` pattern from `MarkdownConverter.ReadAllTextShared`. For binary sniffing, read the leading bytes with the same share flags.
- **Wipe + recreate the `code/` output dir each full pass**, mirroring `GenerateCommitDaysInternal`/`GenerateAdrsInternal`, so a citation removed between runs can't leave a stale code page behind (AD-5, atomic rebuild). [Source: `src/SpecScribe/SiteGenerator.cs:331-372` (commit-days), `:266-324` (ADRs)]
- **Guard against path traversal.** A citation could contain `../../../etc/passwd` or an absolute path. Resolve every candidate to a full path and confirm it is inside `RepoRoot` before rendering; reject anything else silently (omit). The output file path under `code/` must likewise stay inside `OutputRoot`. This is a security boundary, not just robustness.
- **Degrade non-fatally on every failure** (missing, binary, oversized, `IOException`, decode error): omit the page or write a safe placeholder page, and surface a `GenerationOutcome` event — never let one bad file throw out of the phase (NFR2). Follow the per-item `try/catch → GenerationEvent(Error, …)` pattern already used in `GenerateCommitDaysInternal`/`GenerateAdrsInternal`.
- **Keep it self-contained and JS-free.** Styling comes from the embedded `specscribe.css` using existing tokens; no client-side syntax-highlight library, no external assets, no CDN. See "Syntax-readable without JS" below.
- **Cache a `code/` path map** (repo-relative source path → code-page output-relative path) on the generator instance (e.g. `_codePages`), the same way `_commitDays`/`_adrs` are cached, so 7.2 can reuse it for citation linkification without re-discovering.
- **Add a `GenerationPhase.CodePages` reporter phase** and wire `BeginPhase/Tick/EndPhase` around the new loop, matching the existing phases. [Source: `src/SpecScribe/GenerationReporter.cs:5,22-27`]
- **Gate the in-portal generation phase on the code link strategy.** When `ForgeOptions.CodeSourceBaseUrl` is set, skip `GenerateCodePagesInternal` entirely (external mode). Add the setting as an **optional/nullable, non-`required`** `ForgeOptions` property so existing `new ForgeOptions { … }` constructions (tests included) keep compiling, and plumb it: `SiteSettings` CLI option → `Resolve()`, plus `SavedSettings`/`SettingsStore.ApplyTo`/`TrySave` for persistence. [Source: `src/SpecScribe/SiteSettings.cs:29-31`, `src/SpecScribe/SettingsStore.cs:56-88`, `src/SpecScribe/ForgeOptions.cs:53-99`]

### DON'T

- **DON'T rewrite existing source citations to link to code pages** — that's Story 7.2. No changes to `SourceLinkifier`, `AdrLinkRewriter`, or citation rendering in this story. This includes the **external-URL** resolution: 7.1 makes the mode selectable and honors it at the generation gate, but building the `{base}/<path>#L{n}` citation linker is 7.2.
- **DON'T auto-detect the base URL from the git remote** in this story — it's an explicit configuration setting only (see "Configuration — code link strategy").
- **DON'T pull in a syntax-highlighting dependency** (highlight.js, Prism, ColorCode, TextMateSharp, etc.) or any client JS. "Syntax-readable" here means *legible as code* (monospace, line numbers, preserved whitespace, horizontal scroll for long lines), not tokenized coloring. Full server-side tokenized highlighting is explicitly **out of scope / deferred to a later polish** — do not add it opportunistically. [Ref: memory — charts/rendering are pure, self-contained; the site ships one tiny sanctioned JS file only.]
- **DON'T render the whole repo** or add a filesystem walk of `RepoRoot`. Only the citation-referenced set.
- **DON'T force the Core/Adapters package split** from `rendering-architecture.md`. That split is a *seed, not an invariant* (ARCHITECTURE-SPINE "Seed, Not Invariant"); the current monolithic `SiteGenerator` + per-page templater is the established pattern — follow it. New code page logic lives in a `CodeFileTemplater` (page shell) + a small discovery helper + a phase method on `SiteGenerator`.
- **DON'T write anything back to source** (local-first, read-only invariant).
- **DON'T add a top-nav "Code" entry** unless the referenced set is non-empty *and* you gate it exactly like `hasSprint`/`hasAdrs` — but this is optional (see AC-mapping note). The pages become reachable via 7.2's citation links; a broken/empty "Code" nav item would violate AC #1's "without broken navigation."

---

## Architecture Compliance

Relevant invariants from the spec spine and inherited invariants [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`]:

- **Local-only, read-only** — code pages read source files with shared access and never mutate them. [Inherited Invariants]
- **Graceful degradation is contractual** — malformed/binary/oversized/unreadable files degrade non-fatally; generation always succeeds. [Inherited Invariants; NFR2]
- **Baseline generation stays responsive** — cap total work by rendering only the referenced set; no deep analysis here (blame/hotspots are 7.4, opt-in). [NFR1; AD-4]
- **AD-5 (watch scope / atomic output)** — full rebuild wipes `code/`; there is no incremental code-page watch entry point in this story (a source-file edit isn't an `_bmad-output` `.md` change, so it won't route through the existing watch handlers — that's acceptable for 7.1; note it as a known limitation, don't build watch wiring for code files now).
- **Accessibility is part of the rendering contract** (NFR6, UX-DR16): every code page carries the site's skip-link → single `<main id="main-content">` landmark, nav, and breadcrumb, exactly like every other page. [Source: `src/SpecScribe/PathUtil.cs:47-71`, `src/SpecScribe/CommitDayTemplater.cs:24-42`]

### Reference-map seam (how citations will resolve in 7.2)

Today `BuildReferenceMap` maps every `.md` source → its output URL, and `SourceLinkifier` only matches `_bmad-output/…\.md` citations. [Source: `src/SpecScribe/SiteGenerator.cs:780-811`, `src/SpecScribe/SourceLinkifier.cs:12-14`] Code files are a *parallel* map (repo-relative source path → `code/…html`). Build and cache that map in this story; **do not** wire it into the linkifier yet. This keeps 7.1's blast radius to "pages + map" and lets 7.2 focus purely on citation rewriting + back-links.

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. No new NuGet packages. [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **No Markdig involvement** — code files are not markdown; render them as raw text, not through `MarkdownConverter`. (Rendering a `.cs` file through a markdown pipeline would mangle it.)
- **Existing infra to reuse (do not reinvent):**
  - `PathUtil.RenderHeadOpen / RenderFooter / Html / NormalizeSlashes / RelativePrefix` — the page shell + escaping + relative-link math. [Source: `src/SpecScribe/PathUtil.cs`]
  - `SiteNav.RenderNavBar / RenderBreadcrumb` — nav + breadcrumb. [Source: `src/SpecScribe/SiteNav.cs:130-176`]
  - `ForgeOptions.StylesheetName / ScriptName / OutputRoot / RepoRoot / SourceRoot`. [Source: `src/SpecScribe/ForgeOptions.cs`]
  - `MarkdownConverter.ReadAllTextShared` (shared-access read pattern) — reuse or mirror for the binary-sniff byte read. [Source: `src/SpecScribe/MarkdownConverter.cs:109-115`]
  - `SiteGenerator.IsIgnored` (dotfiles, `~$`, `.tmp`, `.crswap`) as a starting exclusion filter. [Source: `src/SpecScribe/SiteGenerator.cs:891-898`]

### Syntax-readable without JS (the rendering approach)

Emit a line table. Minimal, robust shape:

```html
<pre class="code-file"><code>
<span class="code-line" id="L1"><span class="code-ln">1</span><span class="code-src">using System.Text;</span></span>
<span class="code-line" id="L2"><span class="code-ln">2</span><span class="code-src"></span></span>
...
</code></pre>
```

- Split on `\n`; normalize `\r\n`/`\r` first so line numbers match editors.
- `PathUtil.Html(...)` each line's source; an empty line still emits its `<span class="code-src"></span>` so anchors stay 1:1 with real line numbers.
- The `.code-ln` gutter is `user-select: none` and non-linking here (7.2/7.4 may make it a self-link later); `:target` highlights the addressed `.code-line` (mirror the `.ac-criterion :target` highlight pattern). [Source: `src/SpecScribe/assets/specscribe.css` AC-anchor `:target` rules]
- Long lines: the `<pre>` scrolls horizontally (`overflow-x: auto`), the page body never does — same rule the doc-body `<pre>` already uses. [Source: `src/SpecScribe/assets/specscribe.css:422-431`]

---

## File Structure Requirements

**New files:**

- `src/SpecScribe/CodeFileTemplater.cs` — `static RenderPage(...)` returning the full HTML string for one code file page. Model it directly on `CommitDayTemplater` (synthesized page, builds its own shell via `PathUtil.RenderHeadOpen` rather than going through `HtmlTemplater.RenderPage`). [Source: `src/SpecScribe/CommitDayTemplater.cs`]
- `src/SpecScribe/CodeReferenceScanner.cs` (or similar) — pure static helper that, given the source-artifact corpus (raw markdown text + each file's source-relative dir) and `RepoRoot`, returns the deduped set of repo-relative referenced code paths. Keep IO minimal and testable; prefer passing in already-read text so the extraction logic is unit-testable without a temp tree (mirror how `GitMetrics.ParseLog` is a pure, repo-free parse). [Source: `src/SpecScribe/GitMetrics.cs:62-104`]
- `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs` and `tests/SpecScribe.Tests/SiteGeneratorCodePagesTests.cs`.

**Modified files (read them fully before editing):**

- `src/SpecScribe/SiteGenerator.cs` — add `_codePages` field + a `GenerateCodePagesInternal(...)` phase invoked from `GenerateAll` (place it near the commit-days phase, after `Pages`/`Adrs`), plus the discovery call. **Current behavior to preserve:** the full-rebuild slate-wipe of `OutputRoot`, the per-phase reporter calls, the `_docs`/`_nav`/`_progress` lifecycle, and the ordering that `WriteIndex` runs last. Your new phase is additive and must not disturb the epics/ADR/commit-day flow. [Source: `src/SpecScribe/SiteGenerator.cs:38-144`]
- `src/SpecScribe/GenerationReporter.cs` — add `CodePages` to the `GenerationPhase` enum and a label in the phase-label map. [Source: `src/SpecScribe/GenerationReporter.cs:5,22-27`]
- `src/SpecScribe/assets/specscribe.css` — add `.code-file`, `.code-line`, `.code-ln`, `.code-src`, and the `:target` highlight, using existing tokens (`--status-*` are lifecycle tokens — do **not** co-opt them; use neutral `--ink`/`--border`/`--parchment`-family tokens). Note: `StylesheetTests.cs` asserts on stylesheet content — check whether adding classes needs a companion assertion. [Source: memory — status-token system]
- `src/SpecScribe/ForgeOptions.cs` — add optional `CodeSourceBaseUrl` (nullable, **non-`required`**) property + wire it through `Resolve(...)` (new optional param, defaulting to null). **Current behavior to preserve:** the repo-root auto-discovery walk and every existing default; the new option is purely additive. [Source: `src/SpecScribe/ForgeOptions.cs:53-99`]
- `src/SpecScribe/SiteSettings.cs` — add a `[CommandOption("--code-url <BASE>")]` (name is a seed; e.g. `--code-url`/`--code-base-url`) with a clear `[Description]`, and pass it in `Resolve()`. [Source: `src/SpecScribe/SiteSettings.cs`]
- `src/SpecScribe/SettingsStore.cs` — add `CodeSourceBaseUrl` to `SavedSettings` (+ `IsEmpty`) and to `TrySave`/`ApplyTo` so it persists to `.specscribe` and CLI still wins over saved. [Source: `src/SpecScribe/SettingsStore.cs:8-18,56-88`]

**Output layout:**

- `SpecScribeOutput/code/<repo-relative-path>.html` (e.g. `code/src/SpecScribe/SiteGenerator.cs.html`). Append `.html` to the *full* path including original extension so `X.cs` and `X.ts` in the same dir never collide, and normalize slashes. Create intermediate dirs (`Directory.CreateDirectory`). Prefix/relative-link math via `PathUtil.RelativePrefix(outputRelative)` — these pages are nested several levels deep, so the CSS/JS/nav hrefs need the correct `../` depth (this is exactly what `RelativePrefix` handles; test a deep path). [Source: `src/SpecScribe/PathUtil.cs:15-20`]

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Generation-level tests build a temp `_bmad-output` tree + real source files and assert on emitted HTML files; templater tests call `RenderPage` directly. Follow the temp-dir `IDisposable` pattern and the `AssertNoErrors(gen.GenerateAll())` guard from `SiteGeneratorTraceabilityTests`. [Source: `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs:106-154`, `tests/SpecScribe.Tests/CommitDayTemplaterTests.cs`]

**`CodeFileTemplaterTests` (unit, no IO):**

- Renders a title, the site a11y contract (`skip-link` first, single `<main id="main-content">`), nav, and breadcrumb (`Home / … / <file path>`).
- Emits one `id="L{n}"` per source line, numbered from 1, with `L1` present and the count matching the input line count (including a trailing/blank line).
- **Escapes** HTML metacharacters in source (`<div>`, `&`, `"`) — assert the escaped form appears and the raw `<div>` does not (mirror `CommitDayTemplaterTests.RenderPage_EscapesCommitFields`).
- A blank line still emits its anchored `.code-line` so numbering stays 1:1.

**`SiteGeneratorCodePagesTests` (generation-level):**

- A code file **referenced** by a story citation gets a `code/…html` page after `GenerateAll` (positive control: the page exists and contains a known line).
- A repo file that exists but is **not referenced** by any artifact gets **no** page (AC #1 omission).
- **Non-fatal degradation:** a referenced **binary** file (write bytes with an embedded `\0`) and a referenced **oversized** file (exceeds the size cap) each produce no thrown error — `GenerateAll` reports no `Error` outcome, and either no page or a clearly-marked placeholder page is emitted.
- **Path-traversal safety:** a citation targeting a path that resolves outside `RepoRoot` (e.g. `../../secret.txt`) produces no page and no error, and nothing is written outside `OutputRoot`.
- **Stale-output safety:** after a referenced file's citation is removed and `GenerateAll` re-runs, the previously-generated code page is gone (atomic `code/` rebuild) — mirror `RegenerateAdrs_RemovesStalePageAndIndexCard_WhenAdrDeleted`.
- **External mode:** with `CodeSourceBaseUrl` set on `ForgeOptions`, `GenerateAll` produces **no** `code/` pages (and no error), while every other page still generates.
- Determinism: two runs over the same input produce identical `code/` output.

**Settings round-trip (extend `SettingsStoreTests`):**

- `CodeSourceBaseUrl` survives `TrySave` → `TryLoad`, and `ApplyTo` leaves an explicit CLI `--code-url` value untouched (CLI wins over saved).

**Run:** `dotnet test` from repo root (or the `tests/SpecScribe.Tests` project). Do a full generation pass against this actual repo (`dotnet run --project src/SpecScribe` from the repo root — output lands in `SpecScribeOutput/`, the default; **do not** pass `--output docs/live`, that flag is vestigial/gitignored) and eyeball a generated `code/…html` page: line numbers align, anchors work (`…#L42` scrolls + highlights), long lines scroll inside the block, escaping is correct.

---

## Previous Story Intelligence

This is **Story 7.1 — the first story in Epic 7**, so there is no prior story *in this epic* to inherit from. The most relevant precedents are the two synthesized/first-class-page-class stories already shipped; study them as your templates:

- **Commit-day pages (Epic 3 git-pulse work):** the closest structural analogue — a brand-new page class rendered from non-markdown data, with its own `CommitDayTemplater`, a wipe+recreate output dir, per-item try/catch → `GenerationEvent`, prev/next sibling nav, and full a11y shell. **Clone this shape.** [Source: `src/SpecScribe/CommitDayTemplater.cs`, `src/SpecScribe/SiteGenerator.cs:326-372`, `tests/SpecScribe.Tests/CommitDayTemplaterTests.cs`]
- **ADR pages:** the other "render a folder of source files into a dedicated output subdir, rebuilt each pass, gracefully omitting non-qualifying files" precedent. [Source: `src/SpecScribe/SiteGenerator.cs:263-324`]
- **Traceability / reference-map (Story 1.2):** shows how the reference map and citation linkification already work — you are adding a *parallel* code map without touching the existing `.md` one. Read it so 7.2 slots in cleanly. [Source: `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs`, `src/SpecScribe/SiteGenerator.cs:780-811`]

**Recurring lessons from prior stories (apply here):**

- Several prior stories turned out to be "the seams already exist, so the work is a renderer + regression coverage" — but this one is genuinely new production code (no code-page path exists yet). Expect real implementation, not just verification.
- Use invariant/culture-safe formatting for anything derived (line counts, sizes) — the codebase was bitten by culture-sensitive date parsing before. [Source: `src/SpecScribe/GitMetrics.cs:76-82`; `_bmad-output/implementation-artifacts/deferred-work.md`]
- Escaping bugs and stale-output bugs are the two most common regressions in this renderer — both are explicitly covered in the test list above.

---

## Git Intelligence Summary

Recent history is planning/iteration and review churn (`Reviews`, `Iterating`, `Iterating and planning`, `Overnight planning work`) plus a UX fix (`sunburst links undrafted stories to their placeholder page`). No code-page work exists yet; the tree is clean on `main`. The last feature commits touched the sunburst and next-steps affordances — unrelated to this story, so there is no in-flight code to coordinate with. Work on the current worktree branch and keep the change additive (new files + narrow, additive edits to `SiteGenerator`/`GenerationReporter`/`specscribe.css`).

---

## Latest Technical Information

No external libraries or APIs are introduced, so there is no version/security research to fold in. The only "latest" note that matters is a platform one: **.NET's `System.Text` + `System.IO` are sufficient** for shared-access reads and byte sniffing — no third-party file/type detection needed.

**Binary detection (recommended, dependency-free):** read the first N bytes (e.g. 8 KB) with shared access; treat the file as binary if it contains a `\0` NUL byte or fails a strict UTF-8 decode. This is the same heuristic git uses and needs no library. **Size cap:** pick a sane byte threshold (e.g. ~512 KB–1 MB) above which the file is treated as "too large to render" and degraded to a placeholder/omission — document the constant with a comment. Both thresholds are *seed values*, not contracts; put them where a future settings toggle (Epic 5 / AD-3) could pick them up, but do not build a settings knob now.

---

## Project Context Reference

- Epic 7 goal + FR15/FR16/FR19 mapping: [Source: `_bmad-output/planning-artifacts/epics.md:817-902`]
- Requirement FR15 (in-portal code browsing + source-citation resolution): [Source: `_bmad-output/planning-artifacts/epics.md:46`]
- Architecture invariants (local-only, read-only, graceful degradation, seed-not-invariant): [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`]
- Rendering-layer intent (shared core; the package split is aspirational/deferred, current monolith is the pattern): [Source: `_bmad-output/specs/spec-specscribe/rendering-architecture.md`]
- Status-token discipline + pure-render/no-JS conventions: project memory (status-token system; charts are pure SVG + links, no JS).

---

## Tasks / Subtasks

- [x] **Task 1 — Discover the referenced code-file set (AC: #1)**
  - [x] Add `CodeReferenceScanner` (pure helper): given each source artifact's raw markdown + its source-relative directory + `RepoRoot`, extract citation targets and resolve them to repo-relative paths.
  - [x] Handle both citation shapes seen in real artifacts: markdown-link form `[Source: [X.cs:76-99](../../src/SpecScribe/X.cs)]` (resolve href relative to the artifact dir) and inline/code-span form `` [Source: `src/SpecScribe/X.cs:15-17`] `` / `[Source: src/SpecScribe/X.cs:15]` (repo-relative). [Source: `_bmad-output/implementation-artifacts/3-3-agent-and-workflow-structure-coverage-insights.md:77-84`, `1-3-markdown-fidelity-for-core-artifact-patterns.md:88-90`]
  - [x] Keep only candidates that (a) resolve inside `RepoRoot`, (b) are **not** under `SourceRoot` (`_bmad-output`) — those are already rendered as doc pages — and (c) exist on disk and aren't `IsIgnored`. Strip any `:line`/`#fragment` suffix when resolving the path.
  - [x] Dedupe (ordinal-ignore-case) and return a deterministic, sorted set.
- [x] **Task 2 — `CodeFileTemplater.RenderPage` (AC: #1, #2)**
  - [x] Build the page shell via `PathUtil.RenderHeadOpen` + `SiteNav.RenderNavBar` + `SiteNav.RenderBreadcrumb` + single `<main id="main-content">`, mirroring `CommitDayTemplater`.
  - [x] Render the line table: one `.code-line` per source line with `id="L{n}"`, a `.code-ln` gutter number, and an HTML-escaped `.code-src`. Normalize newlines; empty lines still emit an anchored row.
  - [x] Header: file path as `<h1>`, a kicker ("Source File"), and a meta pill with line count (invariant formatting).
- [x] **Task 3 — Generation wiring (AC: #1)**
  - [x] Add `_codePages` map field + `GenerationPhase.CodePages` (enum + label).
  - [x] Add `GenerateCodePagesInternal(...)`: wipe+recreate `code/`, iterate the referenced set, render each, write to `code/<path>.html`, collect `GenerationEvent`s, and populate `_codePages` (repo-relative → output-relative). Invoke it from `GenerateAll` with `BeginPhase/EndPhase` (near the commit-days phase).
- [x] **Task 3b — Code link strategy configuration (AC: #1)**
  - [x] Add optional `CodeSourceBaseUrl` to `ForgeOptions` (nullable, non-`required`) + `Resolve(...)` param.
  - [x] Add the `--code-url` CLI option to `SiteSettings` and pass it through `Resolve()`; add it to `SavedSettings`/`SettingsStore.TrySave`/`ApplyTo` (CLI wins over saved).
  - [x] Gate generation: when `CodeSourceBaseUrl` is set, **skip** `GenerateCodePagesInternal` entirely (external mode). Still leave the anchor scheme unchanged (`#L{n}` is GitHub-compatible for 7.2's external links).
- [x] **Task 4 — Safety & graceful degradation (AC: #1, #2)**
  - [x] Path-traversal guard: reject candidates resolving outside `RepoRoot`; confirm the output path stays inside `OutputRoot`.
  - [x] Binary detection (NUL byte / non-UTF-8) and size cap → omit or emit a clearly-marked placeholder page; never throw.
  - [x] Wrap per-file rendering in try/catch → `GenerationEvent(Error, …)`; the phase always completes.
- [x] **Task 5 — Styling (AC: #2)**
  - [x] Add `.code-file`/`.code-line`/`.code-ln`/`.code-src` + `:target` line highlight to `specscribe.css` using neutral tokens (not `--status-*`); horizontal scroll on the `<pre>`, never the body. Update `StylesheetTests` if it asserts on class presence.
- [x] **Task 6 — Tests (AC: #1, #2)**
  - [x] `CodeFileTemplaterTests`: anchors, numbering, escaping, a11y shell, blank-line handling.
  - [x] `SiteGeneratorCodePagesTests`: referenced→page, non-referenced→omitted, binary/oversized→non-fatal, path-traversal→no page/no leak, stale-output rebuild, determinism.
- [x] **Task 7 — Full generation pass + manual verify (AC: #1, #2)**
  - [x] `dotnet test` green; run a real generation against this repo (default `SpecScribeOutput/`), open a `code/…html` page, confirm anchors/escaping/line-up/scroll and that non-referenced files produced no pages.

## Dev Notes

- **The single most important convention to lock:** line anchor id = `L{n}` (1-based). Story 7.2's citation links depend on it. Cover it with a test so a later refactor can't silently break cross-story linking.
- **Reachability in 7.1 alone:** because citation rewriting is 7.2, these pages are only reachable by direct URL until 7.2 lands. That's expected and acceptable — do **not** add a half-built "Code" nav entry to compensate (it would risk the "no broken navigation" clause). If you want a smoke-test entry point, that's what the manual verify step's direct-URL open is for.
- **Watch mode:** a source *code* file edit does not flow through the existing `_bmad-output` watch handlers, so code pages only refresh on a full generate in this story. This is a deliberate 7.1 limitation; don't build code-file watch wiring (revisit if a later story calls for it).
- **Don't co-opt lifecycle status tokens** for code styling — they mean backlog/ready/in-progress/etc. Use neutral ink/border/parchment tokens. [Source: project memory — status-token system]
- **Code link strategy is a cross-story setting.** 7.1 owns the setting + the generation gate; 7.2 owns citation resolution (in-portal page vs. `{CodeSourceBaseUrl}/<path>#L{n}`). SpecScribe itself is the motivating case: Pages-hosted docs, GitHub-hosted code → it will likely run in external mode. Keep the `#L{n}` anchor identical across modes so 7.2's fragment logic doesn't branch. Carry this setting into 7.2's story context when it's drafted.

### Project Structure Notes

- New page class lands as a peer templater (`CodeFileTemplater`) + a phase method on `SiteGenerator` + a pure scanner helper — the established monolith-plus-per-page-templater pattern. No package restructure (that's a deferred seed, Epics 4/6).
- Output subdir `code/` sits beside `adrs/`, `commits/`, `epics/`, `requirements/` under `OutputRoot`.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:823-841`] — Story 7.1 user story + both ACs; Epic 7 framing.
- [Source: `_bmad-output/planning-artifacts/epics.md:46,121,125`] — FR15/FR16/FR19 and Epic 7 coverage.
- [Source: `src/SpecScribe/CommitDayTemplater.cs`] — the synthesized-page templater to clone (shell, a11y contract, escaping).
- [Source: `src/SpecScribe/SiteGenerator.cs:38-144`] — `GenerateAll` phase orchestration + full-rebuild wipe; where the new phase slots in.
- [Source: `src/SpecScribe/SiteGenerator.cs:326-372`] — `GenerateCommitDaysInternal`: wipe+recreate dir, per-item try/catch → event, cache entries.
- [Source: `src/SpecScribe/SiteGenerator.cs:263-324`] — `GenerateAdrsInternal`: render a folder of source files into a dedicated output subdir, omit non-qualifying files.
- [Source: `src/SpecScribe/SiteGenerator.cs:780-811`] — `BuildReferenceMap`: the `.md` reference map that the code map runs parallel to (seam for 7.2).
- [Source: `src/SpecScribe/SourceLinkifier.cs`] — current citation linkifier (only `_bmad-output/…\.md`); untouched by 7.1, extended in 7.2.
- [Source: `src/SpecScribe/PathUtil.cs`] — `RenderHeadOpen`/`RenderFooter`/`Html`/`RelativePrefix`/`NormalizeSlashes`.
- [Source: `src/SpecScribe/SiteNav.cs:130-176`] — `RenderNavBar`/`RenderBreadcrumb`.
- [Source: `src/SpecScribe/MarkdownConverter.cs:109-115`] — `ReadAllTextShared` shared-access read pattern (NFR5).
- [Source: `src/SpecScribe/GitMetrics.cs:62-104`] — pure, repo-free parse pattern for the testable scanner helper.
- [Source: `src/SpecScribe/GenerationReporter.cs:5,22-27`] — `GenerationPhase` enum + labels to extend.
- [Source: `src/SpecScribe/SiteSettings.cs`] — CLI-option → `ForgeOptions` mapping to extend with `--code-url`.
- [Source: `src/SpecScribe/SettingsStore.cs`] — `.specscribe` persistence + CLI-wins-over-saved `ApplyTo` to extend with `CodeSourceBaseUrl`.
- [Source: `src/SpecScribe/ForgeOptions.cs:53-99`] — `Resolve(...)` construction to extend with the optional base-URL param.
- [Source: `tests/SpecScribe.Tests/CommitDayTemplaterTests.cs`] — templater test shape (a11y, escaping, anchors).
- [Source: `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs`] — generation-level temp-tree + `AssertNoErrors` pattern; stale-output regression shape.
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`] — invariants + "Seed, Not Invariant" (don't force the package split).

## Dev Agent Record

### Agent Model Used

Claude Opus 4.8 (GitHub Copilot)

### Debug Log References

- Full generation pass against this repo (`generate --source _bmad-output --adrs docs/adrs --output SpecScribeOutput`): 142 generated, 1 skipped. The single skip is `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — it genuinely contains embedded NUL bytes (a deliberate test sentinel, confirmed at line 239), so binary detection correctly degraded it to a clearly-marked placeholder page rather than rendering garbage or throwing. 82 code pages rendered; non-referenced repo files produced none.
- Manual verify on `code/src/SpecScribe/ForgeOptions.cs.html`: 225 `.code-line` rows with anchors `L1`…`L225` (1:1), 57 escaped HTML metacharacters, `../../../specscribe.css` relative prefix (correct depth), skip-link → single `<main id="main-content">`, "Source File" kicker, `<h1>` file path.

### Completion Notes List

- **New artifact class: in-portal code pages.** Referenced source files (discovered from `[Source: …]` citations, never a filesystem walk) render at `code/<repo-relative-path>.html` as line-numbered, HTML-escaped, monospace pages. The `.html` suffix is appended to the full path (extension included) so `X.cs`/`X.ts` never collide.
- **Locked cross-story convention:** per-line anchor id = `L{n}` (1-based), GitHub-compatible, covered by `CodeFileTemplaterTests` so Story 7.2's `code/<path>.html#L42` links can't silently break.
- **Code link strategy (`CodeSourceBaseUrl` / `--code-url`):** default (unset) → in-portal pages; set → the in-portal phase is skipped entirely (external mode). Plumbed through `ForgeOptions.Resolve`, `SiteSettings`, and `SavedSettings`/`SettingsStore` (CLI wins over saved). This story owns the setting + the generation gate; citation resolution (in-portal page vs. `{base}/<path>#L{n}`) is deliberately left to Story 7.2. The `_codePages` map (repo-relative → output-relative) is cached for 7.2 but not wired into any linkifier here.
- **Safety / graceful degradation:** path-traversal guard (candidate must resolve inside `RepoRoot`, output must stay inside `OutputRoot`); binary detection (NUL byte / strict-UTF-8 failure, git's heuristic) and a ~1 MB size cap degrade to a placeholder page; every per-file render is wrapped in try/catch → `GenerationEvent`, so one bad file never throws out of the phase. `code/` is wiped+recreated each full pass (atomic rebuild).
- **Deliberate golden-fingerprint update:** `SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint…` was rebaselined because `specscribe.css` gained the new `.code-file`/`.code-line`/`.code-ln`/`.code-src`/`:target`/`.code-placeholder` rules. The page-*inventory* golden was unaffected (the fixture cites no real repo files), confirming the drift is purely the intended CSS addition.
- **Known limitation (per Dev Notes):** a source *code* file edit doesn't flow through the `_bmad-output` watch handlers, so code pages refresh only on a full generate — deliberate for 7.1; no code-file watch wiring was added.

### File List

**New:**
- `src/SpecScribe/CodeReferenceScanner.cs`
- `src/SpecScribe/CodeFileTemplater.cs`
- `tests/SpecScribe.Tests/CodeReferenceScannerTests.cs`
- `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs`
- `tests/SpecScribe.Tests/SiteGeneratorCodePagesTests.cs`

**Modified:**
- `src/SpecScribe/SiteGenerator.cs` — `_codePages` field, `GenerateCodePagesInternal(...)` phase + `TryReadCodeText`/`SplitCodeLines` helpers + size/sniff constants, invoked from `GenerateAll` after the ADRs phase.
- `src/SpecScribe/GenerationReporter.cs` — `GenerationPhase.CodePages` enum value + label.
- `src/SpecScribe/ForgeOptions.cs` — optional `CodeSourceBaseUrl` property + `Resolve(...)` param.
- `src/SpecScribe/SiteSettings.cs` — `--code-url` CLI option + passed through `Resolve()`.
- `src/SpecScribe/SettingsStore.cs` — `CodeUrl` on `SavedSettings` (+ `IsEmpty`), `TrySave`, and `ApplyTo` (CLI wins).
- `src/SpecScribe/assets/specscribe.css` — `.code-file`/`.code-line`/`.code-ln`/`.code-src`/`.code-line:target`/`.code-placeholder` rules.
- `tests/SpecScribe.Tests/StylesheetTests.cs` — assertion for the new code-page classes.
- `tests/SpecScribe.Tests/SettingsStoreTests.cs` — `CodeUrl` round-trip, CLI-wins, and `IsEmpty` coverage.
- `tests/SpecScribe.Tests/ForgeOptionsTests.cs` — `CodeSourceBaseUrl` default + `--code-url` flow-through coverage.
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden content fingerprint rebaselined for the new CSS.

### Change Log

- 2026-07-12: Implemented Story 7.1 (In-Portal Code File Browsing, FR15). Added referenced-source-file discovery, `code/<path>.html` page rendering with stable `L{n}` line anchors, the `--code-url` external-link strategy gate, and full safety/degradation handling. Full suite green (815 tests).

