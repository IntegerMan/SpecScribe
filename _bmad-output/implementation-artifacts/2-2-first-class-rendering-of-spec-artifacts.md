---
baseline_commit: bba1ef445ab61dbfb64ff0e344182284270d6e5f
---

# Story 2.2: First-Class Rendering of Spec Artifacts

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer using the spec-driven workflow,
I want the spec kernel and its companion documents surfaced as a first-class artifact class,
so that specs are navigable and understandable rather than dumped in a generic "Other" list.

## Acceptance Criteria

1. **Given** the project contains a specs folder with a SPEC kernel and companion documents (for example architecture spine, rendering architecture, requirements catalog, settings and signals)
   **When** the site is generated
   **Then** specs render under their own labeled section and navigation with clear titles
   **And** they no longer fall into the generic "Other" bucket.

2. **Given** spec documents cross-reference each other and other artifacts
   **When** I open a spec page
   **Then** its structure is readable (headings and table of contents) and recognized references resolve
   **And** a missing or partial spec set degrades gracefully without broken navigation.

> **Origin & scope:** Second story of Epic 2 (Complete and Faithful BMad Artifact Representation).
> Story 2.1 explicitly **routed the spec KERNEL here** and drew a hard line: the quick-dev `spec-*.md`
> files under `implementation-artifacts/` (`route: one-shot`) and `deferred-work.md` are **2.1's** domain;
> the **spec kernel** under `_bmad-output/specs/spec-specscribe/*.md` (`SPEC.md` + companions) is **this
> story's** domain. Do not conflate them — see [Disambiguation from Story 2.1](#disambiguation-from-story-21-read-this-first).
> This story reuses the same shared rendering seams (`HtmlTemplater.RenderIndex` grouping, `SiteNav`,
> `Frontmatter`/`MarkdownConverter`, `Toc`) that 1.1–2.1 established — additively and centrally.

## Tasks / Subtasks

- [x] Task 1: Identify the spec kernel by folder convention (AC: #1)
  - [x] **Identify by directory, not filename.** The spec kernel is every `*.md` under `_bmad-output/specs/**` (in this repo `_bmad-output/specs/spec-specscribe/`: `SPEC.md`, `ARCHITECTURE-SPINE.md`, `rendering-architecture.md`, `requirements-catalog.md`, `settings-and-signals.md`). The source-relative path starts with `specs/`. **Do NOT key off the `spec-` filename prefix** — that collides with Story 2.1's quick-dev `implementation-artifacts/spec-*.md`. The `.memlog.md` in the folder is already excluded by `SiteGenerator.IsIgnored` (leading `.`). [Source: `src/SpecScribe/SiteGenerator.cs:614-621`; `_bmad-output/specs/spec-specscribe/`]
  - [x] Confirm the current reality: these files are enumerated by `EnumerateSourceFiles` (all `*.md` under `SourceRoot = _bmad-output`), are neither `epics.md` nor under `implementation-artifacts/`, so they render as generic standalone pages via `GenerateOneInternal` and land in `_docs`. On the home index they group by path prefix — `specs/` matches **none** of the three groups (`""`/`planning-artifacts`/`implementation-artifacts`), so they fall into the **"Other"** bucket. `ARCHITECTURE-SPINE.md` additionally rides the top nav as **"Architecture"** because `ModuleContext.BmadMethodDocs` lists it. [Source: `src/SpecScribe/HtmlTemplater.cs:60-112`, `src/SpecScribe/ModuleContext.cs:74-81`]

- [x] Task 2: Give the kernel its own labeled home-index section, out of "Other" (AC: #1)
  - [x] Add a dedicated **"Spec Kernel"** group to the grouping array in `HtmlTemplater.RenderIndex` keyed on the `specs` path prefix, so the kernel docs render in their own titled band instead of "Other". Place it after **Planning Artifacts** and before **Implementation Artifacts** (the kernel is the canonical contract, conceptually adjacent to planning). The existing `used` set already prevents any doc being listed twice. [Source: `src/SpecScribe/HtmlTemplater.cs:60-101`]
  - [x] Verify the empty-group guard still holds: when there is no `specs/` content, `inGroup.Count == 0` skips the section cleanly (no empty header) — this IS the AC#2 "missing spec set degrades gracefully" path for the home index. [Source: `src/SpecScribe/HtmlTemplater.cs:91`]
  - [x] **Clear titles (AC#1 "with clear titles").** Card titles come from `Frontmatter.Title` → first `# H1` → prettified filename. Four of the five read well (`SpecScribe Architecture Spine`, `Rendering Architecture Sketch`, `Requirements Catalog`, `Settings and Signals`), but **`SPEC.md`'s H1 is just "SpecScribe"** — identical to the site/project name and meaningless as a card title in a Spec Kernel band. Give the kernel hub a clear, disambiguating label (recommended: detect the `id: SPEC-*` frontmatter and title its card **"Spec Kernel"** / **"SpecScribe SPEC (canonical contract)"** for the index card only; do not rewrite the page's own `<h1>`). Keep this a small, well-contained title rule, not a general renamer. [Source: `_bmad-output/specs/spec-specscribe/SPEC.md:1-17`, `src/SpecScribe/MarkdownConverter.cs:41-43`]
  - [x] Optional but on-brand: reuse the ADR section's titled-row treatment (`index-section-title-row` + a `view-epic-link`) only if you also add a navigation target in Task 3; otherwise the plain `index-section-title` band (as the other groups use) is correct. Reuse existing CSS — do not invent new section classes. [Source: `src/SpecScribe/HtmlTemplater.cs:344-359`, `src/SpecScribe/assets/specscribe.css:450-476`]

- [x] Task 3: Surface the kernel in site navigation (AC: #1 "and navigation")
  - [x] The AC pairs "labeled section **and navigation**." Add a **dashboard quick-link** for the kernel, gated on the specs folder existing, pointing at the `SPEC.md` kernel page (the natural entry point). Thread a "specs present + kernel output path" signal into `SiteNav.Build` the same way `hasAdrs`/`hasReadme` are passed, and add the quick-link in the same block that adds Epics/Requirements/ADRs quick-links. Keep the existing **"Architecture"** top-nav entry (`ARCHITECTURE-SPINE.md`) untouched — do not duplicate it. [Source: `src/SpecScribe/SiteNav.cs:33-94`, `src/SpecScribe/SiteGenerator.cs:530-534`]
  - [x] **Decision (default: do NOT build a new `specs.html` landing page).** A separate specs index page is out of scope for this story's minimum — the labeled home section + each doc's standalone page + the quick-link + the existing Architecture nav entry together satisfy "navigation." Only add a top-nav "Specs" item if it points somewhere real; since there is no landing page, prefer the quick-link. If you decide a landing page adds enough value, treat it as an explicit, additive extension (mirror the ADR landing precedent) — but keep it optional and out of the critical path.
  - [x] Graceful omission (Story 1.1 contract, AC#2): when no `specs/` docs exist, the quick-link is omitted and no section renders — no broken link. Mirror `SiteNav`'s "matched by well-known presence, omit when absent" discipline. [Source: `src/SpecScribe/SiteNav.cs:5-8`, `:53-69`]

- [x] Task 4: Resolve cross-references on spec pages (AC: #2 "recognized references resolve")
  - [x] **The real cross-reference is frontmatter `companions:`.** `SPEC.md` declares `companions: [requirements-catalog.md, settings-and-signals.md, rendering-architecture.md]` and `sources: [../../planning-artifacts/.../prd.md]`; `ARCHITECTURE-SPINE.md` declares `sources: [prd, SPEC.md, EXPERIENCE.md, DESIGN.md, .memlog.md]`. There are **no inline `[text](*.md)` links between the kernel docs** — the relationships live only in frontmatter, and today `Frontmatter` drops everything except title/project/date/author/version/status. Surface these as a **"Companion documents" / related-docs cross-link block** on the spec page so the kernel is navigable as a set. [Source: `_bmad-output/specs/spec-specscribe/SPEC.md:1-9`, `ARCHITECTURE-SPINE.md:1-11`, `src/SpecScribe/Frontmatter.cs`, `src/SpecScribe/MarkdownConverter.cs:143-160`]
  - [x] Extend `Frontmatter` with `Companions` (and optionally `Sources`) as `IReadOnlyList<string>`, and parse them in `MarkdownConverter.SplitFrontmatter`. **These are YAML lists, not scalars** — `GetString(map, …)` will not work; add a list-aware reader (the value deserializes as a `List<object>`; project each element via `ToString()`). Keep both optional and defaulted to empty so every non-spec doc is unaffected. [Source: `src/SpecScribe/MarkdownConverter.cs:143-163`, `src/SpecScribe/Frontmatter.cs`]
  - [x] **Resolve each companion/source to a real generated page, or drop it — never a broken link (AC#2).** Resolve the reference relative to the spec doc's source path; if the resolved path is inside `SourceRoot` and the file exists on disk, compute its output URL (`PathUtil.ToOutputRelative` + the current page's `RelativePrefix`); otherwise omit that entry. This is the same source→output mapping idea as `SiteGenerator.BuildReferenceMap`/`SourceLinkifier`; resolving by file existence (not by `_docs` membership) is order-independent during the full-rebuild pass. Do the resolution in `SiteGenerator` (it knows `SourceRoot` and can `File.Exists`), attach the resolved `(Label, Href)` list to the `DocModel`, and have `HtmlTemplater.RenderPage` render the block only when non-empty. [Source: `src/SpecScribe/SiteGenerator.cs:392-430`, `:557-609`, `src/SpecScribe/PathUtil.cs`, `src/SpecScribe/HtmlTemplater.cs:9-56`]
  - [x] **Do not re-fix requirement-ID linkification.** `ApplyRequirementLinks` already runs on every generated page (including these standalone spec pages) via `GenerateOneInternal`, so `FR7`/`NFR2`-style tokens already resolve site-wide. **Known limitation (leave out of scope):** `requirements-catalog.md` writes hyphenated `FR-1`/`FR-2`, which `RequirementLinkifier.RefPattern` (`\b(FR|NFR)(\d+)\b`) does **not** match — changing that tokenization affects every page and belongs to a requirements-linkifier story, not here. Note it; do not widen the regex in this story. [Source: `src/SpecScribe/SiteGenerator.cs:415`, `:493-498`, `src/SpecScribe/RequirementLinkifier.cs:17`, `_bmad-output/specs/spec-specscribe/requirements-catalog.md:7-11`]

- [x] Task 5: Verify readable structure and graceful degradation (AC: #2)
  - [x] **Verify — don't rebuild — the TOC.** Standalone spec pages already get the "On this page" sidebar: `HtmlTemplater.RenderPage` feeds level-2/3 headings into `Toc.WrapWithSidebar`. Confirm the kernel pages render headings + TOC correctly (they have clear `##` structure) and that Mermaid, if present, still initializes. No new TOC work. [Source: `src/SpecScribe/HtmlTemplater.cs:41-47`, `src/SpecScribe/Toc.cs:42-54`]
  - [x] **Partial/malformed spec set degrades gracefully (AC#2).** Missing folder → no section, no quick-link. Only `SPEC.md` present (companions missing) → the "Companion documents" entries that don't resolve are omitted, leaving no broken links; if none resolve, omit the whole block. Malformed frontmatter → existing `SplitFrontmatter` try/catch falls back to treating it as body (empty companions). No exception, no broken nav (NFR2). [Source: `src/SpecScribe/MarkdownConverter.cs:141-159`]

- [x] Task 6: Test coverage (AC: #1, #2)
  - [x] `HtmlTemplaterTests` (render-level string assertions — the house pattern): with spec `DocModel`s whose `SourceRelativePath` starts with `specs/`, `RenderIndex` emits a **"Spec Kernel"** section title and their cards, and those cards are **absent from the "Other" band**; with no spec docs the section is omitted. Assert the `SPEC.md` kernel card carries the clear title (not a bare "SpecScribe"). [Source: `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`]
  - [x] `SiteNavTests`: the spec quick-link appears when specs are present and is omitted when absent; the existing nav labels (`Home`/`PRD`/`Architecture`/`Epics`/`Requirements`) are unchanged (no duplicate "Specs"/"Architecture"). [Source: `tests/SpecScribe.Tests/SiteNavTests.cs:50-72`]
  - [x] `MarkdownConverterTests`: `companions:`/`sources:` YAML lists parse into the new `Frontmatter` fields (present → items, absent → empty), and a scalar/malformed value degrades to empty rather than throwing. [Source: `tests/SpecScribe.Tests/MarkdownConverterTests.cs`]
  - [x] A `SiteGenerator`-level test (fixture with a `specs/` folder — see `SiteGeneratorFidelityTests`/`SiteGeneratorTraceabilityTests` for the temp-dir generation pattern): the SPEC page renders a companion cross-link that resolves to a companion's generated `.html`, and a companion listed but absent from disk does **not** emit a link. [Source: `tests/SpecScribe.Tests/SiteGeneratorFidelityTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs`]

- [x] Task 7: End-to-end validation with a real generation pass (AC: #1, #2)
  - [x] Run the focused test filter, then a real generation pass against this repo (it ships the full five-file kernel under `_bmad-output/specs/spec-specscribe/` — a live fixture for every branch).
  - [x] Manually verify on `docs/live/index.html`: the five kernel docs appear under a **"Spec Kernel"** section with clear titles (no "Other" bucket, no bare-"SpecScribe" card); the kernel quick-link is present. On `docs/live/specs/spec-specscribe/SPEC.html`: the "On this page" TOC renders, and the "Companion documents" links resolve to the three companion pages. Confirm removing/renaming a companion leaves no broken link.

### Review Findings

- [x] [Review][Patch] PrettyLabel produces shouty labels for real, live all-caps filenames — `ARCHITECTURE-SPINE.md`'s own `sources:` list references `EXPERIENCE.md` and `DESIGN.md` (both exist under `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/`). `PrettyLabel` (`src/SpecScribe/SiteGenerator.cs:825-835`) preserves any all-caps filename token verbatim to protect acronyms like PRD/SPEC, but that same rule renders these two full English words as bare "EXPERIENCE"/"DESIGN" companion-rail labels instead of "Experience"/"Design" — reachable today in this repo's own generated site. **Resolution:** replace the blanket "all-caps → preserve" rule with a short, explicit acronym allowlist (PRD, SPEC, UX, API, ...); everything else is title-cased regardless of authored casing.
- [x] [Review][Defer] Single-spec-kernel assumption in nav + index-card title [`src/SpecScribe/SiteNav.cs:111-112`, `src/SpecScribe/HtmlTemplater.cs:697-700`] — deferred, out of AC scope, single-kernel only today. `SiteNav.Build`'s `specKernelHub` lookup uses `FirstOrDefault` to pick one `specs/*/SPEC.md`, so a project with more than one spec-kernel bundle only gets a nav quick-link to whichever one enumerates first; `HtmlTemplater.IndexCardTitle` similarly rewrites the card title to the fixed string "SPEC — Canonical Contract" for any doc with an `id:` starting `SPEC-`, so two kernels would render identical, indistinguishable index cards. ACs describe exactly one spec kernel per project; this repo has one; no current use case needs more.
- [x] [Review][Patch] ResolveSpecCompanions omits the `.md`-only filter, allowing a broken-link regression — `src/SpecScribe/SiteGenerator.cs:806-808` documents "never a broken link" but only checks `File.Exists(candidateFull) && !IsIgnored(candidateFull)`. A `companions:`/`sources:` entry pointing at a real non-markdown file (image, JSON, etc.) passes both checks and gets emitted as a link via `PathUtil.ToOutputRelative` (`Path.ChangeExtension(..., ".html")`), pointing to a page the `*.md`-only generation pipeline never produces — a genuine broken link, contradicting the method's own documented contract.



### Epic Context and Business Value

Epic 2 — "Complete and Faithful BMad Artifact Representation" — makes the portal reflect the **whole**
project rather than only epics and stories. Story 2.1 surfaced quick-dev and deferred work; **Story 2.2
surfaces the spec kernel** — the `SPEC.md` canonical contract and its companion documents that today
scatter into the home index's generic "Other" bucket. Making the kernel a first-class, labeled,
navigable artifact class with resolvable cross-references advances **FR2** (first-class BMad support),
**FR5** (coherent navigation + complete artifact-class representation), and the graceful-degradation
guarantee (**NFR2**). This is the artifact class most central to a spec-driven workflow, so "dumped in
Other" is exactly the credibility gap this story closes. Later Epic 2 stories continue the arc: sprint
status (2.3), planning grouping + PRD prominence (2.4), iconography (2.5), comment annotations (2.6).

### Story Foundation Extract

- **Primary concern:** the spec kernel reads as a deliberate, first-class artifact class — labeled section,
  clear titles, reachable from navigation, and internally cross-linked as a set — not an anonymous pile in
  "Other."
- **User outcome:** a maintainer lands on the home page, sees "Spec Kernel" as its own band, opens `SPEC.md`,
  reads it with a working TOC, and steps to its companions via resolved cross-links.
- **Success boundary:** built on the existing static-HTML substrate and the shared rendering seams
  (`RenderIndex` grouping, `SiteNav`, `Frontmatter`/`MarkdownConverter`, `Toc`, the site-wide requirement
  linkifier). Additive and central; no new engine, no new stack.
- **Regression boundary:** epic/story/requirement/ADR/quick-dev surfaces and tallies unchanged; Story 1.4
  accessibility (skip link, single `main`, focus, aria, reduced-motion) and Story 1.5 truthfulness preserved;
  the existing "Architecture" nav entry (ARCHITECTURE-SPINE) not duplicated; antiquarian identity preserved.

### Current Implementation Reality (READ THIS FIRST)

- **Generation flow:** `SiteGenerator.GenerateAll` scans `_bmad-output/**/*.md`. `epics.md` and matched
  `implementation-artifacts/N-M-*.md` stories feed the epics/story pages; **everything else** — including all
  five `specs/spec-specscribe/*.md` files — renders as a generic standalone page via `GenerateOneInternal`
  and lands in `_docs`. [Source: `src/SpecScribe/SiteGenerator.cs:35-110`, `:392-430`]
- **Home index grouping is prefix-based.** `RenderIndex` groups `_docs` by three prefixes: `""` (Overview),
  `planning-artifacts`, `implementation-artifacts`. `specs/…` matches none, so the kernel lands in the
  catch-all **"Other"** band. Adding a `("Spec Kernel", "specs")` group is the exact, minimal fix; the `used`
  set already prevents double-listing. [Source: `src/SpecScribe/HtmlTemplater.cs:60-112`]
- **ARCHITECTURE-SPINE.md is already promoted to nav.** `ModuleContext.BmadMethodDocs` lists it as the
  in-nav **"Architecture"** doc, so it also appears in the top nav and quick links. It is still ALSO a plain
  card (currently in "Other", after this story in "Spec Kernel"). Do not remove or duplicate its nav entry.
  [Source: `src/SpecScribe/ModuleContext.cs:74-81`, `src/SpecScribe/SiteNav.cs:53-69`]
- **Frontmatter is a fixed-field record.** `MarkdownConverter` deserializes the whole YAML into a `map`, then
  copies only `title/project/date/author/version/status` into `Frontmatter`; `companions`/`sources`/`id` are
  in `map` but dropped. `companions`/`sources` are **YAML lists** — needs list-aware reading, not the scalar
  `GetString`. [Source: `src/SpecScribe/MarkdownConverter.cs:143-163`, `src/SpecScribe/Frontmatter.cs`]
- **Only `SPEC.md` and `ARCHITECTURE-SPINE.md` carry frontmatter.** The other three companions start directly
  with an `# H1` (no frontmatter), so their titles come from the H1 and read clearly. `SPEC.md`'s H1 is the
  generic **"SpecScribe"** — the one title that needs disambiguation. [Source: `_bmad-output/specs/spec-specscribe/*.md`]
- **TOC already works for standalone pages.** `RenderPage` extracts level-2/3 headings and calls
  `Toc.WrapWithSidebar`, so kernel pages already get the "On this page" sidebar. Verify, don't rebuild.
  [Source: `src/SpecScribe/HtmlTemplater.cs:41-47`]
- **Requirement-ID links already run site-wide.** `ApplyRequirementLinks` post-processes every page, so
  `FR7`-style tokens on spec pages already resolve. The catalog's hyphenated `FR-1` form does not match the
  linkifier's `\b(FR|NFR)\d+\b` pattern — a known, out-of-scope limitation. [Source: `src/SpecScribe/SiteGenerator.cs:415`, `src/SpecScribe/RequirementLinkifier.cs:17`]
- **Graceful-degradation seams to reuse (don't rebuild):** empty groups are skipped in `RenderIndex`;
  `SiteNav` omits any doc it can't match; `SplitFrontmatter` try/catches malformed YAML; each page renders in
  its own try/catch in `GenerateOneInternal`. [Source: `src/SpecScribe/HtmlTemplater.cs:91`, `src/SpecScribe/SiteNav.cs:53-69`, `src/SpecScribe/MarkdownConverter.cs:141-159`, `src/SpecScribe/SiteGenerator.cs:407-429`]

### Disambiguation from Story 2.1 (READ THIS FIRST)

| Concept | Lives in | Owned by |
|---|---|---|
| **Spec KERNEL** — `SPEC.md` + companions (`ARCHITECTURE-SPINE`, `rendering-architecture`, `requirements-catalog`, `settings-and-signals`) | `_bmad-output/specs/spec-specscribe/*.md` | **Story 2.2 (this story)** |
| **Quick-dev / one-shot** — `spec-*.md` with `route: one-shot` frontmatter | `_bmad-output/implementation-artifacts/spec-*.md` | Story 2.1 |
| **Deferred work** — bulleted note, no frontmatter | `_bmad-output/implementation-artifacts/deferred-work.md` | Story 2.1 |

- Key both classes off the **directory**, never the `spec` substring: kernel = under `specs/`; quick-dev =
  under `implementation-artifacts/`. This is why Story 2.1 built no kernel handling and told you to expect it
  here. [Source: `_bmad-output/implementation-artifacts/2-1-accurate-work-representation-and-authoring-guidance.md:40`, `:139`]
- Coordination: if 2.1 has landed, its quick-dev section already lifts `implementation-artifacts/spec-*.md`
  out of the generic grid; that is orthogonal to your `specs/` grouping. Neither section should claim the
  other's docs — verify with a generation pass that both bands are correct and disjoint.

### Scope Boundaries

- **IN (this story):** the `specs/**` kernel as a first-class **labeled home-index section** with clear
  titles (out of "Other"); a **navigation** affordance (kernel quick-link, keeping the existing Architecture
  nav); **cross-reference resolution** via frontmatter `companions:`/`sources:` rendered as resolvable
  related-doc links on spec pages; **graceful degradation** for missing/partial/malformed spec sets; verify
  (don't rebuild) TOC + site-wide requirement linkification on spec pages.
- **OUT — quick-dev `spec-*.md` / `deferred-work.md`** → Story 2.1. Do not touch that classification here.
- **OUT — a new `specs.html` landing page** unless you deliberately choose to add it as an additive
  extension (default: don't; the section + quick-link + per-doc pages are the "navigation").
- **OUT — widening `RequirementLinkifier` to match hyphenated `FR-1`** (site-wide tokenization change; not
  this story).
- **OUT — sprint page/widget (2.3), planning grouping + PRD prominence (2.4), iconography (2.5), comment
  annotations (2.6); the JS drill/zoom sunburst engine, dark mode/theme, velocity/deep-git analytics.**

### Previous Story Intelligence

- Story 2.1 (`ready-for-dev`) is the immediate predecessor and the authority on the 2.1/2.2 boundary: it
  documented the kernel as "**Story 2.2's** domain; do not build kernel handling here" and identified the
  exact five kernel files. Read its "Scope Boundaries" and Task 1 note before you start. [Source: `_bmad-output/implementation-artifacts/2-1-accurate-work-representation-and-authoring-guidance.md:40`, `:135-147`]
- Shared-seam discipline from 1.2–2.1: change behavior **additively and centrally** in the single-source
  seams (`HtmlTemplater`, `SiteNav`, `Frontmatter`/`MarkdownConverter`, `Toc`, `SiteGenerator`), prefer
  render-/generation-level string assertions over new public API, and never fork a per-page path.
- Story 1.1 established graceful omission of missing artifact classes; Story 1.4 established the a11y floor
  (skip link, single `main`, focus, aria, reduced-motion). Both are contracts to preserve here — the new
  section and quick-link inherit them for free by reusing the existing templater seams.
- Environment: use `py -3` for BMAD helper scripts on this Windows host.
- If 2.1 has **not** landed when you start, you can still proceed — your `specs/` grouping does not depend on
  2.1's quick-dev grouping. Just rebase/re-verify once both are present so the two bands stay disjoint.

### Architecture Compliance

- **One grouping seam, one nav seam.** The labeled section is a new entry in `RenderIndex`'s existing group
  array; the navigation affordance is a new gated entry in `SiteNav.Build`'s existing quick-link block. No
  parallel rendering path. [Source: `src/SpecScribe/HtmlTemplater.cs:60-101`, `src/SpecScribe/SiteNav.cs:33-94`]
- **Graceful degradation is contractual (NFR2).** Missing/partial/malformed spec sets degrade to omitted
  sections/links and non-fatal fallbacks — never an exception or a broken link (Story 1.1). Reuse the empty-
  group skip, `SiteNav` omit-missing, and `SplitFrontmatter` fallback rather than adding new guards.
- **Host-neutral output (NFR6, future webview).** The section, quick-link, and companion cross-links are
  static HTML/CSS + relative hrefs — no host-specific behavior, so the Epic 6 webview inherits them. Keep all
  links/anchors static-host-safe (GitHub Pages). [Source: `_bmad-output/specs/spec-specscribe/rendering-architecture.md`]
- **Self-contained packaging.** Any CSS reuses the embedded `specscribe.css`; do not add loose asset files or
  third-party deps. Prefer reusing existing `index-section-title`/`index-card`/`view-epic-link` classes over
  new ones. [Source: `src/SpecScribe/SiteGenerator.cs:500-509`, `src/SpecScribe/assets/specscribe.css:450-476`]

## Technical Requirements

- Classify the kernel by the `specs/` source-path prefix (directory), never the `spec` filename substring;
  keep it disjoint from Story 2.1's `implementation-artifacts/spec-*.md`.
- Add a **"Spec Kernel"** group to `HtmlTemplater.RenderIndex` (after Planning Artifacts) so the kernel docs
  render in a labeled band and out of "Other"; empty-group omission preserved.
- Give the `SPEC.md` kernel card a **clear, disambiguated title** (its H1 is the generic "SpecScribe"); do
  not alter the page's own `<h1>`. Other four docs already title clearly from their H1.
- Add a **kernel quick-link** in `SiteNav`, gated on specs presence, pointing at the SPEC page; **keep** the
  existing "Architecture" (ARCHITECTURE-SPINE) nav entry and do not duplicate it.
- Extend `Frontmatter` with list-aware `Companions` (and optionally `Sources`), parse them in
  `MarkdownConverter`; render a **resolvable related-docs block** on spec pages where each entry links only if
  its target source file exists (else omitted) — no broken links.
- Verify (do not rebuild) the standalone-page TOC and the site-wide requirement-ID linkifier on spec pages.
  Do not widen the requirement-ID regex for hyphenated `FR-1`.
- Missing/partial/malformed spec set degrades gracefully (no section, no quick-link, no broken companion
  links, no exception). No new JS; host-neutral, static-host-safe; Story 1.4 accessibility intact.

## File Structure Requirements

Primary UPDATE candidates:

- `src/SpecScribe/HtmlTemplater.cs` — add the `("Spec Kernel", "specs")` group in `RenderIndex`; the clear-
  title rule for the SPEC hub card; render the companion/related-docs block in `RenderPage` when the
  `DocModel` carries resolved companions. Preserve the single `<main>` landmark, skip link, and existing
  group order/`used` dedupe.
- `src/SpecScribe/SiteNav.cs` — a gated kernel quick-link (and the presence signal it needs). Preserve the
  omit-when-absent discipline and the current nav item set/labels.
- `src/SpecScribe/Frontmatter.cs` + `src/SpecScribe/MarkdownConverter.cs` — list-aware `Companions`/`Sources`
  parsing; keep all fields optional and defaulted so non-spec docs are unaffected.
- `src/SpecScribe/SiteGenerator.cs` — pass the specs-present signal into `BuildNav`/`SiteNav.Build`; resolve
  each companion/source to an existing generated page and attach the `(Label, Href)` list to the spec
  `DocModel` before `RenderPage`. Reuse the `BuildReferenceMap`/`ToOutputRelative`/`RelativePrefix` idiom.
- `src/SpecScribe/DocModel.cs` — an optional related-docs list (e.g. `IReadOnlyList<(string Label,string Href)> Companions`), defaulted empty so ADR/README/other pages are unaffected.
- `src/SpecScribe/assets/specscribe.css` — only if a small style is needed for the companion block; prefer
  reusing existing card/section/link classes. No new third-party deps.

Primary TEST candidates:

- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — "Spec Kernel" section emitted + kernel cards present +
  absent from "Other"; SPEC card carries the clear title; section omitted when no spec docs.
- `tests/SpecScribe.Tests/SiteNavTests.cs` — kernel quick-link present/absent; nav labels unchanged (no
  duplicate Architecture/Specs).
- `tests/SpecScribe.Tests/MarkdownConverterTests.cs` — `companions:`/`sources:` list parse (present/absent/
  malformed → empty, no throw).
- `tests/SpecScribe.Tests/SiteGeneratorFidelityTests.cs` (or a new sibling) — a `specs/`-folder fixture:
  SPEC page renders a companion link resolving to a generated companion `.html`; an absent companion emits no
  link.

## Library and Framework Requirements

- Stay on the existing .NET / Markdig / YamlDotNet / inline-SVG / CSS stack. **No new runtime dependencies,
  no new JS.** The companion block is static HTML; frontmatter lists use the in-repo YamlDotNet deserializer
  already wired in `MarkdownConverter`. TOC and requirement linkification are existing seams — reused, not
  re-implemented.

## Testing Requirements

- Preserve existing coverage and **Story 1.4's accessibility assertions** and **Story 1.5's truthfulness
  assertions** — none may regress. The home index must keep exactly one `main` landmark and the skip link.
- Add coverage (see Task 6): Spec Kernel section + clear title + no "Other" leakage + omission-when-empty;
  kernel quick-link present/absent + nav labels unchanged; `companions`/`sources` list parse; companion
  cross-link resolves to a real page and omits absent targets.
- Run targeted tests, then a real generation pass:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~SiteNav|FullyQualifiedName~MarkdownConverter|FullyQualifiedName~SiteGenerator|FullyQualifiedName~Toc"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`
- Manual verification on `docs/live/index.html` + `docs/live/specs/spec-specscribe/SPEC.html`: kernel under a
  "Spec Kernel" section with clear titles (not "Other", not a bare "SpecScribe" card); kernel quick-link
  present; TOC on the SPEC page; companion links resolve; a removed companion leaves no broken link.

## UX and Accessibility Requirements

- The "Spec Kernel" section reads on-brand: reuse the antiquarian `index-section-title`/`index-card` treatment
  used by the other groups (teal/gold/parchment), never a foreign default. [Source: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`]
- Clear, human titles (AC#1): the SPEC hub card must not read as a bare, generic "SpecScribe". Card labels are
  short, descriptive, active. [Source: EXPERIENCE.md Voice and Tone]
- Preserve Story 1.4 accessibility: the new section lives inside the existing single `<main id="main-content">`;
  the quick-link and companion links are real focusable `<a>`s; no icon-only affordances (UX-DR17). [Source: epics.md UX-DR16, UX-DR17]
- The companion cross-link block reads as helpful navigation ("Companion documents"), de-emphasized so it
  aids without cluttering; it is static (no motion), so `prefers-reduced-motion` is trivially satisfied.
  [Source: epics.md UX-DR18]

## Reinvention and Regression Guardrails

- Do NOT key the kernel off the `spec` filename substring — use the `specs/` directory, keeping it disjoint
  from Story 2.1's `implementation-artifacts/spec-*.md`.
- Do NOT duplicate or remove the existing "Architecture" (ARCHITECTURE-SPINE) nav entry; do NOT rewrite any
  spec page's own `<h1>` (only the index-card title for the SPEC hub).
- Do NOT emit a companion/source link to a target that doesn't exist on disk — resolve-or-omit, never a
  broken link (AC#2, NFR2).
- Do NOT widen `RequirementLinkifier` for hyphenated `FR-1` (site-wide change, out of scope); do NOT build a
  JS engine, dark mode, or a specs landing page (unless deliberately, additively chosen).
- Do NOT fold spec docs into epic/story/requirement/quick-dev tallies — they are a navigable class, not a
  progress input.
- Do NOT regress Story 1.4 accessibility (skip link, single `main`, focus, aria, reduced-motion), Story 1.5
  truthfulness, or Story 1.1's missing-section omission. Keep all links/anchors static-host-safe (GitHub Pages).

## Git Intelligence Summary

- Baseline `bba1ef4` (main, "1.4 Code Review"). Recent commits `4813709`/`6a8b4c8`/`db9fc1c` reworked the home
  "Next Steps" panel (`BmadCommands.ForProject`) and the dashboard — read them if your quick-link work touches
  `SiteNav`/`HtmlTemplater` dashboard composition so the kernel quick-link composes with, not against, the
  existing quick-link/next-steps layout.
- Shared rendering seams (`HtmlTemplater`/`SiteNav`/`MarkdownConverter`/`SiteGenerator`) are the single-source
  points — change them additively and centrally, the same pattern 1.2–2.1 followed.
- Generated output publishes to GitHub Pages — keep every href/anchor static-host-safe (relative, correct
  depth via `PathUtil.RelativePrefix`).

## Latest Technical Information

- No framework/library version decisions are introduced by this story; it stays entirely within the existing
  .NET + Markdig + YamlDotNet + inline-SVG + CSS stack. YamlDotNet already deserializes the frontmatter map,
  so reading `companions`/`sources` as a `List<object>` needs no new package.
- Relative `<a href>` navigation and static section markup are universally supported — no polyfills, no new
  capability. The Mermaid CDN and the Story 1.5 tooltip/copy script are unrelated to this work.

## Project Context Reference

- Epic + story source: `_bmad-output/planning-artifacts/epics.md` (Epic 2, Story 2.2; FR2/FR5; NFR2)
- Predecessor that routed the kernel here and drew the 2.1/2.2 line: `_bmad-output/implementation-artifacts/2-1-accurate-work-representation-and-authoring-guidance.md`
- The kernel itself (live fixture): `_bmad-output/specs/spec-specscribe/` — `SPEC.md` (+ `companions:`),
  `ARCHITECTURE-SPINE.md`, `rendering-architecture.md`, `requirements-catalog.md`, `settings-and-signals.md`
- Accessibility baseline (shared seams): `_bmad-output/implementation-artifacts/1-4-accessible-high-polish-interaction-baseline.md`
- Truthfulness baseline: `_bmad-output/implementation-artifacts/1-5-dashboard-insight-polish-and-visual-truthfulness.md`
- Successors (do NOT do here): 2.3 sprint status, 2.4 planning grouping/PRD prominence, 2.5 iconography, 2.6 comment annotations
- Key source seams: `src/SpecScribe/HtmlTemplater.cs`, `SiteNav.cs`, `Frontmatter.cs`, `MarkdownConverter.cs`, `SiteGenerator.cs`, `DocModel.cs`, `Toc.cs`, `ModuleContext.cs`, `RequirementLinkifier.cs`, `assets/specscribe.css`
- Architecture spine / rendering: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`, `rendering-architecture.md`
- UX design/behavior: `_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md`, `EXPERIENCE.md`
- Memory: [[charting-is-pure-svg-no-js]], [[story-1-4-a11y-seams-for-1-5]], [[story-1-4-split-into-1-4-1-5-1-6]]

## Story Completion Status

- Status set to `ready-for-dev`.
- Completion note: Ultimate context engine analysis completed — comprehensive developer guide created for
  Epic 2's spec-kernel story: a labeled "Spec Kernel" home-index section (out of "Other") with clear titles,
  a gated kernel navigation quick-link (without duplicating the existing Architecture entry), frontmatter
  `companions:`/`sources:` cross-links resolved to generated pages, verified TOC + requirement linkification,
  and full graceful degradation for missing/partial/malformed spec sets.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8

### Debug Log References

- Confirmed the spec kernel is `_bmad-output/specs/spec-specscribe/*.md` (five files; `.memlog.md` ignored),
  keyed by the `specs/` directory — disjoint from Story 2.1's `implementation-artifacts/spec-*.md` quick-dev
  files.
- Confirmed the kernel renders today as standalone pages that fall into the home index's "Other" bucket, and
  that `ARCHITECTURE-SPINE.md` is separately promoted to the "Architecture" nav via `ModuleContext`.
- Confirmed cross-references between kernel docs live only in frontmatter (`companions:`/`sources:`), which
  `Frontmatter` currently drops; both are YAML lists (need list-aware parsing, not scalar `GetString`).
- Confirmed `SPEC.md`'s H1 is the generic "SpecScribe" (needs a clear index-card title); the other four
  companions title clearly from their H1.
- Confirmed the standalone-page TOC (`RenderPage` → `Toc.WrapWithSidebar`) and the site-wide requirement-ID
  linkifier (`ApplyRequirementLinks`) already run on spec pages — verify, don't rebuild; the catalog's
  hyphenated `FR-1` form is a known, out-of-scope linkifier limitation.
- Environment: use `py -3` for BMAD helper scripts on this Windows host.
- Planned validation commands:
  - `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HtmlTemplater|FullyQualifiedName~SiteNav|FullyQualifiedName~MarkdownConverter|FullyQualifiedName~SiteGenerator|FullyQualifiedName~Toc"`
  - `dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs --output docs/live --project-name SpecScribe`

### Implementation Plan

- Task 1 (classify by `specs/` directory) → Task 2 (labeled "Spec Kernel" home section + clear SPEC title) →
  Task 3 (gated kernel quick-link, keep Architecture nav) → Task 4 (list-aware `Companions`/`Sources`
  frontmatter + resolve-or-omit cross-link block) → Task 5 (verify TOC + degradation) → Task 6 (tests) →
  Task 7 (real generation pass).
- Keep every change in the shared seams; prefer render-/generation-level string assertions over new public
  API; keep everything host-neutral and static-host-safe.

### Completion Notes List

- Second story of Epic 2. Surfaces the spec kernel (`_bmad-output/specs/spec-specscribe/*`) as a first-class,
  labeled, navigable artifact class with clear titles and resolvable frontmatter cross-references, out of the
  generic "Other" bucket; degrades gracefully for missing/partial/malformed spec sets.
- Explicitly kept out: quick-dev/deferred work (2.1), sprint page/widget (2.3), planning grouping/PRD
  prominence (2.4), iconography (2.5), comment annotations (2.6); a `specs.html` landing page, hyphenated
  `FR-1` linkifier widening, JS engine, dark mode.
- Coordination flags: classify by `specs/` directory (disjoint from 2.1's `implementation-artifacts/spec-*`);
  keep — do not duplicate — the existing "Architecture" nav entry; resolve-or-omit companion links (never a
  broken link).

**Implemented (2026-07-06):**

- **Task 1 (classify by directory):** kernel keyed off the `specs/` source-path prefix throughout — `SiteNav.IsUnderSpecs`, the `HtmlTemplater.RenderIndex` group prefix, and `SiteGenerator.ResolveSpecCompanions` — never the `spec` filename substring, keeping it disjoint from 2.1's `implementation-artifacts/spec-*.md`.
- **Task 2 (labeled section + clear title):** added the `("Spec Kernel", "specs")` group to `RenderIndex` between Planning and Implementation Artifacts (empty-group guard omits it cleanly when absent). `IndexCardTitle` gives a doc carrying `id: SPEC-*` the clear card label **"SPEC — Canonical Contract"** — index card only, the page `<h1>` is untouched.
- **Task 3 (navigation):** `SiteNav.Build` adds a presence-gated **"Spec Kernel"** dashboard quick-link pointing at the SPEC hub's generated page, matched by well-known presence (omitted when absent). No top-nav "Specs" item; the existing ARCHITECTURE-SPINE "Architecture" nav entry is kept and not duplicated.
- **Task 4 (cross-references):** extended `Frontmatter` with `Id` + list-aware `Companions`/`Sources` (scalar/malformed → empty, never throws); `DocModel.Companions` carries the resolved `(Label, Href)` list; `SiteGenerator.ResolveSpecCompanions` resolves each reference relative to the spec doc's directory and links it only when the target exists on disk, sits inside `SourceRoot`, and isn't ignored (so `.memlog.md` and missing targets are silently omitted — never a broken link). `RenderPage` renders a de-emphasized `.companion-docs` nav block only when non-empty. Labels prettify the filename, preserving all-caps acronyms (SPEC/PRD/EXPERIENCE/DESIGN).
- **Task 5 (verify + degradation):** confirmed the standalone-page TOC still renders on kernel pages (no TOC work) and that a missing folder / listed-but-absent companion / malformed frontmatter all degrade with no section, no quick-link, no broken links, and one `<main>`/skip link preserved (Story 1.4 a11y).
- **Task 6/7 (tests + real pass):** added coverage across `HtmlTemplaterTests`, `SiteNavTests`, `MarkdownConverterTests`, and a new `SiteGeneratorSpecKernelTests`; full suite **283/283** green. Real generation pass against this repo's five-file kernel: home shows the "Spec Kernel" band + quick-link with the clear SPEC card, and `SPEC.html` shows resolvable "Companion documents" links (with `.memlog.md`/missing targets omitted) and the "On this page" TOC.
- **Left out of scope as planned:** hyphenated `FR-1` linkifier widening (site-wide tokenization change), a `specs.html` landing page. Confirmed the site-wide requirement-ID linkifier already runs on spec pages (verified, not re-touched).

**UX polish pass (2026-07-07):** three review-driven refinements to the spec-page presentation.

- **Capabilities → definition-list cards:** new `CapabilityStyler` (a scoped body-HTML post-processor mirroring `GherkinStyler`) rewrites `SPEC.md`'s `## Capabilities` nested bullet list into `.capability` cards (a gold `CAP-N` id header + `intent`/`success` `dt`/`dd` rows). Anchored on the `id="capabilities"` heading, tolerant of Markdig's loose-list `<p>` wrappers and nested `<ul>`s, and returns the body unchanged when the authored pattern is absent or incomplete. Applied in `RenderPage`, gated on spec pages; TOC (which reads `Headings`) is untouched.
- **Companion documents → sidebar rail:** the block moved out of the content column into the sticky sidebar rail beneath the "On this page" TOC. `Toc.WrapWithSidebar` now wraps the TOC + optional rail extra in a `.page-rail` container (the sticky/overflow migrated off `.toc-sidebar`), and `RenderPage` passes the companion nav as `railExtra` rather than appending it to `main`. Links restyled to the muted rail treatment (`--rust`, underline-on-hover) — no more foreign blue (`--teal-deep`) web-link look (DESIGN.md link rules).
- **Quick-link pill "Spec Kernel" → "Spec":** friendlier Explore Key Views label; the home-index section band stays the more descriptive "Spec Kernel".
- Full suite **327/327** green (incl. new `CapabilityStylerTests` + rail-placement/label assertions); validated with a real generation pass — all five capabilities render as cards, the companion block sits in the rail after the TOC, and the pill reads "Spec".

### File List

- _bmad-output/implementation-artifacts/2-2-first-class-rendering-of-spec-artifacts.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- src/SpecScribe/Frontmatter.cs
- src/SpecScribe/MarkdownConverter.cs
- src/SpecScribe/DocModel.cs
- src/SpecScribe/HtmlTemplater.cs
- src/SpecScribe/SiteNav.cs
- src/SpecScribe/SiteGenerator.cs
- src/SpecScribe/CapabilityStyler.cs (new)
- src/SpecScribe/Toc.cs
- src/SpecScribe/assets/specscribe.css
- tests/SpecScribe.Tests/HtmlTemplaterTests.cs
- tests/SpecScribe.Tests/SiteNavTests.cs
- tests/SpecScribe.Tests/MarkdownConverterTests.cs
- tests/SpecScribe.Tests/SiteGeneratorSpecKernelTests.cs (new)
- tests/SpecScribe.Tests/CapabilityStylerTests.cs (new)

## Change Log

- 2026-07-06: Created Story 2.2 as Epic 2's spec-kernel story. Scoped: a labeled "Spec Kernel" home-index
  section (lifting `_bmad-output/specs/**` out of the generic "Other" bucket) with clear titles including a
  disambiguated card for the generic-H1 `SPEC.md`; a presence-gated kernel navigation quick-link that keeps
  (without duplicating) the existing ARCHITECTURE-SPINE "Architecture" nav entry; list-aware
  `companions:`/`sources:` frontmatter parsing rendered as resolve-or-omit cross-links on spec pages; and
  verified reuse of the existing standalone-page TOC and site-wide requirement-ID linkifier. Documented the
  hard disambiguation from Story 2.1's quick-dev `spec-*.md`, the classify-by-directory rule, the graceful-
  degradation contract, and the out-of-scope hyphenated `FR-1` linkifier limitation.
- 2026-07-06: Implemented Story 2.2. Added the "Spec Kernel" home-index group (out of "Other") with the clear
  `id: SPEC-*` card title "SPEC — Canonical Contract"; a presence-gated kernel dashboard quick-link (keeping,
  not duplicating, the Architecture nav); list-aware `Frontmatter.Id`/`Companions`/`Sources` parsing; a
  resolve-or-omit "Companion documents" cross-link block on spec pages (`DocModel.Companions` +
  `SiteGenerator.ResolveSpecCompanions`, with a `.companion-docs` style); and verified reuse of the standalone
  TOC and requirement linkifier. Added tests across HtmlTemplater/SiteNav/MarkdownConverter and a new
  SiteGeneratorSpecKernel suite (283/283 pass); validated with a real generation pass. Status → review.
- 2026-07-07: UX polish pass on the spec pages (review feedback). Reformatted `SPEC.md`'s Capabilities into
  definition-list cards via a new scoped `CapabilityStyler`; moved the "Companion documents" block into the
  sidebar rail beneath the TOC (new `.page-rail` wrapper in `Toc.WrapWithSidebar`) with muted rust links
  instead of the foreign-blue `--teal-deep` treatment; and renamed the Explore Key Views quick-link pill from
  "Spec Kernel" to "Spec" (the home-index section band unchanged). Added `CapabilityStylerTests` and
  rail-placement/label assertions; full suite 327/327 green; validated with a real generation pass.
