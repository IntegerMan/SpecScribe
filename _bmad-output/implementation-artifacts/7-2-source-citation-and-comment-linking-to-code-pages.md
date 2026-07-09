# Story 7.2: Source-Citation and Comment Linking to Code Pages

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a contributor,
I want source citations and "View source" links to resolve to in-portal code pages,
so that traceability leads somewhere useful instead of to a raw or dead link.

## Acceptance Criteria

1.
**Given** artifacts contain source citations (for example `[Source: path:line]`) and view-source links
**When** pages render
**Then** recognized references link to the corresponding code file page, including a line anchor when a line is cited
**And** unresolved references degrade to plain text without broken links. [Source: epics.md#Story 7.2; FR15]

2.
**Given** a code reference resolves to a code page
**When** I follow it
**Then** I land on the cited file at the cited location
**And** I can navigate back to the citing artifact. [Source: epics.md#Story 7.2; FR15]

---

## Developer Context

**This is the payoff story for Story 7.1.** 7.1 created the code-file *pages* (`code/<repo-rel-path>.html`), the per-line `id="L{n}"` anchors, the `_codePages` map (repo-relative source path → code-page output-relative path), the `CodeReferenceScanner`, and the `CodeSourceBaseUrl` mode gate — but deliberately **left every existing `[Source: …]` citation and "View source" link untouched**, still pointing at dead `../../src/…` paths. **This story is the linkifier that finally wires citations to those pages** (or, in external mode, to `{CodeSourceBaseUrl}/<path>#L{n}`), plus back-navigation from a code page to the artifacts that cite it. [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:31-37,103-105`]

> ⚠️ **Hard dependency on Story 7.1 — sequence check first.** 7.1 is `ready-for-dev`, **not `done`**. Every seam this story consumes (`SiteGenerator._codePages`, `ForgeOptions.CodeSourceBaseUrl`, `CodeReferenceScanner`, `CodeFileTemplater`, the `code/…html#L{n}` output layout) is *created by 7.1* and does not exist in the tree yet. **Confirm 7.1 has landed on your base branch before starting.** If it hasn't, either implement 7.1 first or coordinate — do not re-invent 7.1's scanner/map/pages here. Every "reuse `_codePages`/`CodeReferenceScanner`" instruction below is written against 7.1's contract; if 7.1's final names differ, adapt to the real ones. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:102-103`]

### What "recognized references" actually look like in the corpus

Do **not** design against the theoretical `[Source: path:line]` shape alone. In the real artifacts, code citations overwhelmingly use the **markdown-link form**, which Markdig has already turned into an `<a href>` by the time any post-process runs:

- `[Source: [SiteGenerator.cs:38](../../src/SpecScribe/SiteGenerator.cs:38)]` → Markdig emits `<a href="../../src/SpecScribe/SiteGenerator.cs:38">SiteGenerator.cs:38</a>` — **line number is in the href suffix**.
- `[Source: [ModuleContext.cs:76-99](../../src/SpecScribe/ModuleContext.cs)]` → `<a href="../../src/SpecScribe/ModuleContext.cs">…</a>` — **line range is only in the label; href has no line**.
- `[Source: [WorkInventory.cs](../../src/SpecScribe/WorkInventory.cs)]` → `<a href="../../src/SpecScribe/WorkInventory.cs">…</a>` — **no line at all**.

[Source: `_bmad-output/implementation-artifacts/3-3-agent-and-workflow-structure-coverage-insights.md:33,81-87`]

So the **primary mechanism is an href rewriter** — the exact shape of `AdrLinkRewriter`, but targeting repo *code* files instead of `_bmad-output/*.md`. Read the line number **from the `:N` href suffix** when present; otherwise link the file with no line anchor (do **not** try to parse line ranges out of the free-text label — that's brittle and out of scope).

The secondary form is the **inline code-span / plain-text citation** that never became a link: `` [Source: `src/SpecScribe/Foo.cs:15`] `` renders as text/`<code>`, and — critically — the same citation appearing **inside a rendered markdown comment** (`<aside class="md-comment">`, Story 2.6) is emitted as HTML-escaped raw text with literal backticks and **no anchor at all**. That is the "**and Comment Linking**" half of this story's title: comments were made *visible* in 2.6; this story makes any citation inside them *clickable*. [Source: `src/SpecScribe/CommentAnnotationRenderer.cs:29-31,85-87`]

### The core design in one paragraph

Add code-citation resolution to the **whole-page** post-process (`ApplyReferenceLinks`, the same pass that already runs `RequirementLinkifier`/`StoryEpicLinkifier` on *every* generated page — story, doc, ADR, retro, sprint, index), so a citation resolves no matter which template emitted it and no matter whether it sits in body prose or inside a comment aside. Resolution has two matchers sharing one resolver: **(A)** an `<a href>` rewriter that catches Markdig-emitted `href="…/src/…ext(:line)?"` view-source links, and **(B)** a plain-text matcher (anchor-aware, mirroring `RequirementLinkifier`'s split-on-`<a>` skip) that catches inert `[Source: …code path…]` / code-span citations, including inside `md-comment` asides. Both resolve a candidate the same way `CodeReferenceScanner` does (→ repo-relative path, confirmed inside `RepoRoot`), then emit either an in-portal href (`{prefix}code/<repo-rel>.html#L{n}`, gated on membership in `_codePages`) or an external href (`{CodeSourceBaseUrl}/<repo-rel>#L{n}`). Anything that doesn't resolve **degrades to plain text** — form (A) drops the dead anchor and keeps its inner text; form (B) is already plain text. For AC #2's back-navigation, the code page grows a "Referenced by" list built from a file→citing-artifacts reverse map.

### Two resolution modes (inherited from 7.1's `CodeSourceBaseUrl` gate)

The `#L{n}` fragment is **identical in both modes** — 7.1 locked that on purpose so this story's fragment logic never branches. Only the base differs: [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:49-59,267`]

- **`CodeSourceBaseUrl` unset (in-portal):** href = `{pagePrefix}code/<repo-rel>.html#L{n}`. Resolution is gated on the target being present in `_codePages` (which, since 7.1 built pages for exactly the scanned/referenced set, is the correct "does a page exist to link to?" test — and is what keeps in-portal links from 404ing).
- **`CodeSourceBaseUrl` set (external):** 7.1 skipped in-portal page generation, so `_codePages` is empty. href = `{CodeSourceBaseUrl.TrimEnd('/')}/<repo-rel>#L{n}`. Gate resolution on the candidate resolving to an existing, non-ignored file inside `RepoRoot` (reuse `CodeReferenceScanner`'s resolution), so external links never point at a path that isn't really in the repo. Back-navigation (AC #2) is **N/A** in external mode — GitHub owns the destination page — and that's acceptable; don't try to inject a back-link into an external site.

### Scope boundary (read carefully)

- **IN scope:** resolving code citations + view-source links to code pages / external URLs with correct `#L{n}`; graceful plain-text degradation for unresolved refs; the code page's "Referenced by" back-link block (in-portal mode).
- **OUT of scope:** anything 7.1 owns (page rendering, anchors, the scanner, the mode gate, the `_codePages` map). Do **not** touch the `.md`-citation path — `SourceLinkifier`'s existing `_bmad-output/…\.md` behavior stays exactly as-is (this story adds a *parallel* code resolver, it does not rewrite `.md` citation handling). Do **not** build 7.3's timeline/date links or 7.4's blame/hotspot annotations. Do **not** auto-derive `CodeSourceBaseUrl` from `git remote` (7.1 already ruled that out).

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Resolve in the whole-page pass.** Add the code resolver to `ApplyReferenceLinks(html, outputRelativePath, …)` so it runs on every page (it already computes `prefix = PathUtil.RelativePrefix(outputRelativePath)` — reuse that). This single insertion point covers docs, ADRs, retros, story pages, and index in one place, and reaches inside `<aside class="md-comment">` for the "comment linking" requirement. [Source: `src/SpecScribe/SiteGenerator.cs:714-731`]
- **Model the href rewriter on `AdrLinkRewriter`.** Same regex-over-rendered-HTML shape, but match repo *code* hrefs and map them via `_codePages`/`CodeSourceBaseUrl` instead of the `_bmad-output` `.md` swap. Read the line from a trailing `:N` (or `:N-M`, take the first N) on the href; strip it before resolving the path. [Source: `src/SpecScribe/AdrLinkRewriter.cs:9-52`]
- **Make the plain-text matcher anchor-aware.** Mirror `RequirementLinkifier`: `Regex.Split` on `(<a\b[^>]*>.*?</a>)` and only rewrite the even (non-anchor) segments, so you never double-link an href the rewriter already produced and never rewrite text inside an existing link. [Source: `src/SpecScribe/RequirementLinkifier.cs:12-38`]
- **Share one resolver with `CodeReferenceScanner`.** The path-resolution rules (resolve candidate relative to the citing artifact's dir for `../../src/…` hrefs, or as repo-relative for `src/…` code-spans; confirm inside `RepoRoot`; exclude `_bmad-output`/`SourceRoot`; strip `:line`; honor `IsIgnored`) must be **identical** to 7.1's scanner so the set of things that link is exactly the set of pages that exist. Reuse the scanner's resolution helper rather than re-deriving it; if it's private, expose a small pure static method. [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:144,234-237`]
- **Emit `#L{n}` exactly.** GitHub-compatible, and 7.1's in-portal anchors are `id="L{n}"`. When no line is cited, link the file with **no** fragment (lands at top of page). [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:56,70,263`]
- **Degrade unresolved references to plain text (AC #1).** For the href form, when the target doesn't resolve, replace the whole `<a …>label</a>` with just `label` (kills the dead `../../src/…` link) — never leave a broken link, never throw. For the plain-text form, leave the text as-is (it's already unlinked). Verify with a test that a citation to a non-existent / excluded file yields no `<a href="…src…">` and no thrown error (NFR2). [Source: epics.md#Story 7.2 AC1; `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` — graceful degradation]
- **Add back-navigation on code pages (AC #2, in-portal only).** Build a file→citing-artifacts reverse map during discovery (the scanner already visits every citation; capture `(resolvedRepoRelPath → citing artifact output URL + label)`), dedupe, and render a "Referenced by" list on each code page. Extend `CodeFileTemplater.RenderPage` (7.1's templater) and the `GenerateCodePagesInternal` loop to accept and render it. The breadcrumb (already on every page) is the minimum fallback; the "Referenced by" list is the first-class affordance the AC asks for.
- **Keep it pure/JS-free and self-contained.** Linkifiers are pure string→string; styling for "Referenced by" uses existing neutral tokens (`--ink`/`--border`/`--parchment` family — **not** `--status-*`). [Source: project memory — status-token system; charts are pure SVG + links, no JS]
- **Preserve every existing linkifier's output.** `RequirementLinkifier`/`StoryEpicLinkifier` run in `ApplyReferenceLinks` today; your addition must be additive and order-safe. Run the code linkifier so it doesn't fight them: FR/Story tokens don't overlap file-path/href tokens, but confirm ordering leaves both correct (add code resolution *after* the existing two, and keep it anchor-aware so it won't touch links they emitted). [Source: `src/SpecScribe/SiteGenerator.cs:719-731`]

### DON'T

- **DON'T change `SourceLinkifier`'s `.md` behavior.** It matches `_bmad-output/…\.md` and links those; that's Story 1.2 and stays. Add a *new* code resolver (e.g. `CodeReferenceLinkifier`) next to it. [Source: `src/SpecScribe/SourceLinkifier.cs:12-14`]
- **DON'T parse line ranges out of free-text labels.** Only trust a `:N` in the **href** (view-source form) or immediately after the path in a code-span citation. A label like `SiteGenerator.cs:38-64 (GenerateAll), :527-533` is not a reliable single-line signal — link the file, first line only if a clean `:N` is present on the href, else no fragment.
- **DON'T render code pages or the scanner here** — 7.1 owns them. This story consumes `_codePages` and extends `CodeFileTemplater` only for the "Referenced by" block.
- **DON'T create broken or duplicate links.** Anchor-awareness (skip inside `<a>`) is mandatory so the plain-text matcher never re-wraps the rewriter's output or the nav's links.
- **DON'T add a top-nav "Code" entry** (still 7.1's deferred call; pages are reached via these citation links). [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:89,264`]
- **DON'T write back to source** (local-first, read-only invariant). [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` — Inherited Invariants]

---

## Architecture Compliance

Relevant invariants [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`]:

- **Graceful degradation is contractual** — an unresolved citation becomes plain text; a malformed href is left/stripped, never a thrown exception and never a broken link (NFR2, AC #1). [Inherited Invariants]
- **Local-only, read-only** — resolution is a pure string transform over already-rendered HTML plus the in-memory `_codePages`/reverse map; no source mutation. [Inherited Invariants]
- **Baseline generation stays responsive** — resolution is O(rendered HTML) regex work on pages already being written; the reverse map is built once during 7.1's discovery pass, not per-page (NFR1). [AD-4]
- **Accessibility is part of the rendering contract** — the "Referenced by" block lives inside the existing `<main>` landmark and uses semantic list markup; links carry visible, meaningful text (the citing artifact's title), never "click here". [NFR6, UX-DR16]
- **Seed, not invariant** — keep the current monolith-plus-linkifier pattern; a new `CodeReferenceLinkifier` static class is the right shape, not a package split. [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:87`]

### Reference-map seam (how this consumes 7.1)

7.1's `BuildReferenceMap` maps `.md` sources → output URLs for `SourceLinkifier`; `_codePages` is the *parallel* map for code files that 7.1 built and cached but did not wire into any linkifier. **This story wires it.** The forward map (`_codePages`) drives citation → page resolution; a new reverse map (file → citing artifacts) drives page → citation back-navigation. Both are generator-instance state, directly reachable from the instance method `ApplyReferenceLinks` and from `GenerateCodePagesInternal`. [Source: `src/SpecScribe/SiteGenerator.cs:915-946`; `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:77,103-105`]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` on. **No new NuGet packages.** [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **`System.Text.RegularExpressions` only** — same toolkit as every existing linkifier (`RegexOptions.Compiled`, `Singleline` + `IgnoreCase` for the anchor split). No HTML parser dependency; these are deliberate whole-HTML regex post-processes. [Source: `src/SpecScribe/RequirementLinkifier.cs:1,12-17`, `src/SpecScribe/AdrLinkRewriter.cs:1,13-15`]
- **Existing infra to reuse (do not reinvent):**
  - `PathUtil.RelativePrefix / NormalizeSlashes / Html / ToOutputRelative` — link math + escaping. [Source: `src/SpecScribe/PathUtil.cs`]
  - `CodeReferenceScanner` resolution (7.1) — the single source of truth for "citation string → repo-relative code path (or reject)". [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:144`]
  - `SiteGenerator._codePages`, `ForgeOptions.CodeSourceBaseUrl` (7.1). [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:77,152`]
  - `CodeFileTemplater.RenderPage` (7.1) — extend its signature for the "Referenced by" list. [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:143`]

---

## File Structure Requirements

**New files:**

- `src/SpecScribe/CodeReferenceLinkifier.cs` — pure static class exposing `Linkify(html, forwardMap, codeSourceBaseUrl, prefix, resolver)` (or equivalent). Contains both matchers (href rewriter + anchor-aware plain-text) and the shared emit logic. Model the anchor-aware split on `RequirementLinkifier`, and the href regex on `AdrLinkRewriter`. Keep it IO-free and unit-testable in isolation.
- `tests/SpecScribe.Tests/CodeReferenceLinkifierTests.cs` — pure unit tests (see Testing Requirements).
- (Possibly) `tests/SpecScribe.Tests/SiteGeneratorCodeCitationTests.cs` — generation-level end-to-end (citation in a temp artifact → resolved link in emitted page + "Referenced by" on the code page).

**Modified files (read fully before editing):**

- `src/SpecScribe/SiteGenerator.cs` — (1) call `CodeReferenceLinkifier.Linkify(...)` from `ApplyReferenceLinks`, guarded so it's a no-op when neither in-portal pages nor external mode apply (i.e. `_codePages` empty AND `CodeSourceBaseUrl` null). (2) Build the file→citing-artifacts reverse map during 7.1's discovery/generation phase and pass it into `CodeFileTemplater`. **Preserve** the existing `ApplyReferenceLinks` behavior (FR/Story linkification order, the `prefix` computation) and the full-rebuild/phase orchestration 7.1 added. [Source: `src/SpecScribe/SiteGenerator.cs:714-731`, and 7.1's `GenerateCodePagesInternal`]
- `src/SpecScribe/CodeFileTemplater.cs` (7.1) — add an optional "Referenced by" section (list of `<a href>` to citing artifacts) rendered inside `<main>`; empty/absent list → omit the section (no empty heading). [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:143`]
- `src/SpecScribe/CodeReferenceScanner.cs` (7.1) — if resolution is private, expose a pure static resolver (`TryResolve(citationTargetOrHref, citingArtifactDir, repoRoot, out repoRelPath)`) so the linkifier and the scanner cannot drift. Additive only.
- `src/SpecScribe/assets/specscribe.css` — add a small `.code-referenced-by` (or reuse an existing list/aside style) using neutral tokens; check `StylesheetTests` for a companion assertion if it asserts class presence. [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:151`]

**No output-layout changes:** links target 7.1's existing `code/<repo-rel>.html#L{n}` (or the external base URL). This story adds no new output files beyond what 7.1 already emits.

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Follow the temp-`_bmad-output`-tree + `AssertNoErrors(gen.GenerateAll())` pattern for generation tests and direct `Linkify` calls for unit tests. [Source: `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs:106-154`, `tests/SpecScribe.Tests/RequirementLinkifierTests.cs` if present]

**`CodeReferenceLinkifierTests` (unit, no IO) — in-portal mode (forward map + null base URL):**

- **View-source href, line in href:** `<a href="../../src/SpecScribe/Foo.cs:42">Foo.cs:42</a>` (target in map) → `<a href="{prefix}code/src/SpecScribe/Foo.cs.html#L42">…</a>`.
- **View-source href, no line:** `<a href="../../src/SpecScribe/Foo.cs">…</a>` → resolves to `code/…Foo.cs.html` with **no** `#L`.
- **Line range in href → first line only:** `…Foo.cs:42-60` → `#L42`.
- **Plain-text / code-span citation:** `` [Source: `src/SpecScribe/Foo.cs:15`] `` (as text, and as `<code>src/SpecScribe/Foo.cs:15</code>`) → linked with `#L15`.
- **Citation inside a comment aside:** input containing `<aside class="md-comment">[Source: \`src/SpecScribe/Foo.cs:9\`]</aside>` → the citation inside the aside is linked (the "comment linking" requirement).
- **Unresolved href → plain text (AC #1):** `<a href="../../src/SpecScribe/DoesNotExist.cs">DoesNotExist.cs</a>` (not in map) → inner text `DoesNotExist.cs` with **no** anchor, no exception.
- **Anchor-awareness / idempotence:** running `Linkify` twice yields identical output; an already-resolved `code/…html#L42` link is not re-wrapped or altered; nav/`req-ref` links are untouched.
- **Escaping:** produced hrefs go through `PathUtil.Html`; a path with characters needing escaping stays valid HTML.

**`CodeReferenceLinkifierTests` — external mode (`CodeSourceBaseUrl` set, empty forward map):**

- `<a href="../../src/SpecScribe/Foo.cs:42">…</a>` → `<a href="https://github.com/IntegerMan/SpecScribe/blob/main/src/SpecScribe/Foo.cs#L42">…</a>` (base URL trailing slash normalized; `#L42` preserved).
- A citation to a path **not** under `RepoRoot` → degrades to plain text (no external link to a non-repo path).

**`SiteGeneratorCodeCitationTests` (generation-level, in-portal):**

- A story/doc artifact whose body cites `src/SpecScribe/Foo.cs:42` (both md-link and code-span forms) → emitted page contains a link to `code/src/SpecScribe/Foo.cs.html#L42`, and no residual `href="../../src/…"` dead link.
- **Back-navigation (AC #2):** the generated `code/src/SpecScribe/Foo.cs.html` contains a "Referenced by" entry linking back to the citing artifact's page.
- **Cross-page coverage:** a citation on a *doc* page (not just a story page) resolves too (proves it's in `ApplyReferenceLinks`, not the story-only `SourceLinkifier` block).
- **Determinism:** two runs produce identical output.

**Regression guard:** an existing `.md` `[Source: _bmad-output/…md]` citation still links exactly as before (this story didn't disturb `SourceLinkifier`). [Source: `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs`]

**Run:** `dotnet test` from repo root. Then a full generation pass against this repo (`dotnet run --project src/SpecScribe` — output lands in `SpecScribeOutput/`, the default; **do not** pass `--output docs/live`, that flag is vestigial/gitignored). Open a story with code citations, confirm they now click through to `code/…html` at the right line (page scrolls + `:target` highlights), confirm the code page lists its "Referenced by" artifacts, and confirm an unresolved citation shows as plain text (no dead link). Then re-run with `--code-url https://github.com/IntegerMan/SpecScribe/blob/main` and confirm citations become GitHub links with `#L{n}` and no `code/` pages are generated. [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:187`; project memory — Generate output dir is SpecScribeOutput]

---

## Previous Story Intelligence

**Story 7.1 (`In-Portal Code File Browsing`) is the direct predecessor and the contract this story is written against.** Re-read it in full before starting — especially its "Scope split with 7.2" notes, which were authored to hand off cleanly to this story. [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md`]

Load-bearing hand-offs from 7.1:

- **The `L{n}` anchor is locked** — 7.1 covered it with a test precisely so this story's `#L{n}` fragments resolve. Don't invent a different fragment scheme. [7.1 Dev Notes:263]
- **Mode is already selectable** — 7.1 added `CodeSourceBaseUrl` + the generation gate; this story only *consumes* the mode for citation resolution. SpecScribe itself will likely run in **external** mode (Pages-hosted docs, GitHub-hosted code), so give external mode real test coverage, not an afterthought. [7.1:49-59,267]
- **`_codePages` + `CodeReferenceScanner` exist to be reused** — the whole point of caching them in 7.1 was to let this story linkify without re-discovering. If you find yourself re-writing file discovery, stop and reuse. [7.1:77,144]

**Recurring lessons from prior stories (apply here):**

- **Escaping and stale-output are the two most common regressions in this renderer** — route every emitted href through `PathUtil.Html`; the stale-output axis is 7.1's concern (page rebuild), but ensure a citation *removed* between runs doesn't leave a stale "Referenced by" entry (the reverse map is rebuilt each pass, so this is automatic if you don't cache it across runs). [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md:203`]
- **Several prior stories were "the seams already exist, so the work is a linkifier + regression coverage."** This story is largely that — a new linkifier + a small templater extension — *provided 7.1 has landed*. The novel risk here is matcher precision (two citation shapes, comments, unresolved-degrade), not new page infrastructure.
- **Culture-safe / invariant formatting** for anything derived; no culture-sensitive parsing. [Source: `_bmad-output/implementation-artifacts/deferred-work.md`]
- **Grep in-flight/recent story files for stale repeated commands before closing** (e.g. the `--output docs/live` foot-gun already burned three Epic 2 stories). [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:126-129`; project memory]

---

## Git Intelligence Summary

Recent history is planning/review churn (`Status`, `3.2 / 3.3`, `Deep commit analysis`, `3.2`, `fix: close Epic 1 deferred heatmap tech debt`) — no Epic 7 code exists on `main` yet, consistent with 7.1 being `ready-for-dev`/unstarted. There is **no in-flight code-page work to coordinate with, which is exactly the risk**: this story cannot begin until 7.1's files (`CodeFileTemplater`, `CodeReferenceScanner`, `_codePages`, `CodeSourceBaseUrl`) are on your base branch. Keep this story's change additive: one new linkifier class + a call site in `ApplyReferenceLinks` + a "Referenced by" extension to 7.1's templater + tests. [Source: git log; `_bmad-output/implementation-artifacts/sprint-status.yaml:102-103`]

> **Worktree note:** if you run this in a worktree, edit files at the worktree path — do **not** re-root relative paths back at `C:\Dev\SpecScribe`. `main` has a background auto-committer. [Source: project memory — worktree edits must target the worktree path]

---

## Latest Technical Information

No external libraries or APIs are introduced — nothing to version-check. Two platform notes:

- **Regex over rendered HTML is the established, deliberate pattern here** (`AdrLinkRewriter`, `RequirementLinkifier`, `SourceLinkifier`, `ColorSwatchRewriter` all do it). Do not reach for an HTML parser; match the codebase.
- **`CodeSourceBaseUrl` normalization:** trim a single trailing `/` before appending `/<repo-rel>#L{n}` so `.../blob/main` and `.../blob/main/` both produce one clean slash. Emit the repo-relative path with forward slashes (`PathUtil.NormalizeSlashes`).

---

## Project Context Reference

- FR15 (in-portal code browsing **+ resolve source citations and "View source" links** to those pages): [Source: `_bmad-output/planning-artifacts/epics.md:46,121`]
- Epic 7 goal + Story 7.2 ACs: [Source: `_bmad-output/planning-artifacts/epics.md:837-882`]
- 7.1 contract this story consumes (scope split, `_codePages`, `CodeSourceBaseUrl`, `L{n}` anchor, `CodeFileTemplater`, `CodeReferenceScanner`): [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md`]
- Architecture invariants (local-only/read-only, graceful degradation, seed-not-invariant): [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`]
- Status-token discipline + pure-render/no-JS conventions: project memory (status-token system; charts are pure SVG + links, no JS).

---

## Tasks / Subtasks

- [ ] **Task 0 — Confirm 7.1 has landed (blocking gate)**
  - [ ] Verify `SiteGenerator._codePages`, `ForgeOptions.CodeSourceBaseUrl`, `CodeReferenceScanner`, `CodeFileTemplater`, and the `code/…html#L{n}` output all exist on the base branch. If not, implement/merge 7.1 first — do not re-create its seams here.
- [ ] **Task 1 — `CodeReferenceLinkifier` (AC: #1)**
  - [ ] New pure static class. Href rewriter (model on `AdrLinkRewriter`): match `href="(…/)?<repo code path>.<ext>(:N(-M)?)?"`, take first `N` as the line, strip the suffix, resolve the path via the shared resolver.
  - [ ] Anchor-aware plain-text matcher (model on `RequirementLinkifier`'s `<a>`-split): match inert `[Source: …code path…]` / code-span citations in non-anchor segments only — this is what reaches inside `md-comment` asides.
  - [ ] Shared emit: in-portal → `{prefix}code/<repo-rel>.html#L{n}` gated on `_codePages` membership; external → `{baseUrl.TrimEnd('/')}/<repo-rel>#L{n}` gated on file-exists-in-repo. `PathUtil.Html` every href.
  - [ ] Unresolved: href form → drop anchor, keep inner text; plain-text form → leave as-is. Never throw.
- [ ] **Task 2 — Wire into the whole-page pass (AC: #1)**
  - [ ] Call `CodeReferenceLinkifier.Linkify(...)` from `ApplyReferenceLinks`, after the existing FR/Story linkifiers, using the already-computed `prefix`. Guard: no-op when `_codePages` is empty and `CodeSourceBaseUrl` is null.
  - [ ] Share `CodeReferenceScanner`'s resolution (expose a pure static resolver if it's private).
- [ ] **Task 3 — Back-navigation "Referenced by" (AC: #2, in-portal)**
  - [ ] Build a file→citing-artifacts reverse map during 7.1's discovery (capture citing artifact output URL + title per resolved code path); dedupe; deterministic order.
  - [ ] Extend `CodeFileTemplater.RenderPage` to render a "Referenced by" list inside `<main>` (omit when empty). Pass the reverse map from `GenerateCodePagesInternal`.
- [ ] **Task 4 — Styling (AC: #2)**
  - [ ] Add/`reuse` a neutral-token style for "Referenced by" (not `--status-*`). Update `StylesheetTests` if it asserts class presence.
- [ ] **Task 5 — Tests (AC: #1, #2)**
  - [ ] `CodeReferenceLinkifierTests`: href-with-line, href-no-line, range→first-line, code-span, comment-aside, unresolved→plain-text, idempotence/anchor-awareness, escaping; external-mode base-URL + non-repo-path degrade.
  - [ ] Generation-level: citation on story *and* doc page resolves; code page shows "Referenced by"; `.md` citation regression intact; determinism.
- [ ] **Task 6 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green. Real generate (default `SpecScribeOutput/`): citations click through to `code/…html#L{n}`, code pages list "Referenced by", unresolved refs are plain text. Re-run with `--code-url …` and confirm external links + no `code/` pages.

## Dev Notes

- **This story's whole value is precision of matching + honest degradation.** The two citation shapes (Markdig-emitted `<a href>` view-source links vs. inert code-span/comment citations) need two matchers over one resolver. Get the href form right first — it's the dominant real-world shape.
- **Line number lives in the href suffix** for the common form (`…Foo.cs:42`), not the label. Don't try to parse the label. When there's no `:N`, link the file with no fragment.
- **`ApplyReferenceLinks` is the single home** — it runs on every page and reaches inside comment asides, which is exactly why "source-citation" and "comment linking" collapse into one insertion point. Resist adding a per-template linkify call.
- **Mode symmetry:** keep `#L{n}` identical across in-portal and external; only the base differs. 7.1 locked this on purpose.
- **Back-nav is in-portal only.** In external mode the destination is GitHub; there's no page to inject a back-link into, and that's fine.
- **Don't co-opt `--status-*` tokens** for the "Referenced by" styling. [Source: project memory — status-token system]

### Project Structure Notes

- New code lands as a peer linkifier (`CodeReferenceLinkifier`) beside `SourceLinkifier`/`RequirementLinkifier`/`AdrLinkRewriter`, plus a small extension to 7.1's `CodeFileTemplater` and a call site in `SiteGenerator.ApplyReferenceLinks`. No package restructure (deferred seed, Epics 4/6).
- Output targets 7.1's existing `code/` subdir; this story adds no new output paths.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:863-882`] — Story 7.2 user story + both ACs.
- [Source: `_bmad-output/planning-artifacts/epics.md:46,121`] — FR15 (citation/view-source resolution to code pages).
- [Source: `_bmad-output/implementation-artifacts/7-1-in-portal-code-file-browsing.md`] — the predecessor contract: `_codePages`, `CodeReferenceScanner`, `CodeFileTemplater`, `CodeSourceBaseUrl`, `L{n}` anchor, scope split with 7.2.
- [Source: `src/SpecScribe/AdrLinkRewriter.cs:9-52`] — the href-rewriter shape to model form (A) on.
- [Source: `src/SpecScribe/RequirementLinkifier.cs:12-38`] — the anchor-aware `<a>`-split whole-page pass to model form (B) on.
- [Source: `src/SpecScribe/SourceLinkifier.cs:10-33`] — the `.md` citation linkifier to sit *beside* (and not disturb).
- [Source: `src/SpecScribe/SiteGenerator.cs:714-731`] — `ApplyReferenceLinks`: the whole-page pass + `prefix` computation to extend.
- [Source: `src/SpecScribe/SiteGenerator.cs:490-501`] — the story-page `SourceLinkifier` block, for context on current `.md` linkification (leave as-is).
- [Source: `src/SpecScribe/SiteGenerator.cs:915-946`] — `BuildReferenceMap`: the `.md` map the code map runs parallel to.
- [Source: `src/SpecScribe/CommentAnnotationRenderer.cs:29-31,85-87`] — how comment annotations render (escaped raw text) — the "comment linking" target.
- [Source: `src/SpecScribe/PathUtil.cs`] — `RelativePrefix`/`NormalizeSlashes`/`Html`/`ToOutputRelative`.
- [Source: `_bmad-output/implementation-artifacts/3-3-agent-and-workflow-structure-coverage-insights.md:33,81-87`] — real-world code-citation shapes (md-link form, line-in-href vs line-in-label).
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`] — invariants (graceful degradation, local/read-only, seed-not-invariant).
- [Source: `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs:106-154`] — generation-level temp-tree + `AssertNoErrors` test pattern.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
