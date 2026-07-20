---
baseline_commit: d08e19c3fec5b162baad8622ddcd91fb0056661c
---

# Story 10.5: Document Rendering Legibility

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reader of long artifacts (a tech lead on Journey 6, a first-time visitor on Journey 5, a reviewer on Journey 3),
I want **references, assumption tags, and long-page navigation rendered as designed elements — chips instead of raw `[[wiki]]`/`file:line` syntax, `[ASSUMPTION: …]` styled like the Story 2.6 annotation treatment, subsections grouped under collapsible TOC parents, and retired work tucked into a collapsed section**,
so that raw syntax, flat 28-entry TOCs, and inline retirement notices no longer obstruct reading.

## Context & Why This Story Exists

This is the code side of the site-wide UX review's **document-rendering** cluster — MissingFeatures **F3, F4, F5, F6** and their Epic3UXFeedback twins. Each is a small, self-contained legibility fix; together they finish "long artifacts read like designed documents, not raw markdown."

| AC | Feedback item | Verbatim complaint | Type |
|---|---|---|---|
| **AC1 (refs)** | [F3](docs/MissingFeatures.md:96-97) / [🟢 story-page](docs/Epic3UXFeedback.md:101) | _"Raw `[[wiki-link]]` names and `file:line` syntax leak into rendered prose. Missing: styled chips or a references appendix."_ | NET-NEW |
| **AC1 (assumption)** | [F5](docs/MissingFeatures.md:102-103) / [🟢 requirements](docs/Epic3UXFeedback.md:143) | _"`[ASSUMPTION: …]` tags are semantically important but visually plain; style them like the annotation-comments treatment from Story 2.6."_ | EXTENDS |
| **AC2 (TOC)** | [F4](docs/MissingFeatures.md:99-100) / [🟡 story-page](docs/Epic3UXFeedback.md:142) | _"The 'On this page' TOC lists 28+ flat entries; group subsections under collapsible parents. Keep the on-every-long-page TOC invariant as templates evolve."_ | NET-NEW |
| **AC3 (retired)** | [F6](docs/MissingFeatures.md:105-106) / [🟡 epic-page](docs/Epic3UXFeedback.md:90) | _"Story 3.4's retirement notice sits inline among active stories; a collapsed 'Retired' section preserves the history without the clutter — a pattern that will recur as the roadmap evolves."_ | NET-NEW |

Serves the onboarding (5) and health-insight (6) journeys plus the reviewer journey (3) — the pages in question are the portal's **longest** (roughly 1,900 → 6,700 words; the 3.5× variance is what justifies collapse — [Epic3UXFeedback.md:95](docs/Epic3UXFeedback.md)).

### What renders wrong today (read these before designing)

**AC1 — references leak as raw syntax.** Markdig passes `[[name]]`, bare `file:line`, and `[ASSUMPTION: …]` through as **literal text** (double brackets aren't a Markdig construct; `[ASSUMPTION: …]` isn't a valid link because there's no `(url)`), so they render verbatim in prose. Story 7.2's [`CodeReferenceLinkifier`](src/SpecScribe/CodeReferenceLinkifier.cs) already makes `[Source: path]` citations and view-source hrefs **clickable**, but F3 is explicit that the clickability (7.2) and the **rendering treatment** (this story — chip vs raw) are separate missing pieces. There is **no** `[[…]]` or `[ASSUMPTION:]` handling anywhere in `src/` today (grep confirms: only `prism.js`/`Charts.cs`/`WebviewHelpers.cs` mention `wiki`/`[[`, all unrelated).

**AC1 — the annotation treatment to extend already exists.** Story 2.6 shipped [`CommentAnnotationRenderer`](src/SpecScribe/CommentAnnotationRenderer.cs): an `<!-- … -->` HTML comment renders as a muted `<aside class="md-comment">` (block) or `<span class="md-comment-inline">` (inline), styled at [specscribe.css:417-431](src/SpecScribe/assets/specscribe.css). F5 says: give `[ASSUMPTION: …]` **that same visual vocabulary**. But `[ASSUMPTION:]` is a bracketed **inline prose token**, not an HTML comment — Story 2.6's renderers key off `HtmlBlockType.Comment` / `HtmlInline` tags and will never fire on it. So this is a **new matcher that reuses the existing `.md-comment(-inline)` CSS class**, not a change to `CommentAnnotationRenderer`.

**AC2 — the TOC is one shared flat renderer.** [`Toc.RenderSidebar`](src/SpecScribe/Toc.cs:18) emits a flat `<nav class="toc-sidebar">` of `<a class="toc-link">` links; level-3 entries get an indented `.toc-h3` class ([specscribe.css:337](src/SpecScribe/assets/specscribe.css)) but are **not grouped** under their parent — a 28-entry PRD TOC is 28 flat links. This is the **single** TOC seam ([Toc.cs:6-9](src/SpecScribe/Toc.cs) doc-comment: "The TOC is never forked per page"), consumed by **every** TOC-bearing page: generic docs/ADRs/README ([HtmlTemplater.cs:47-54](src/SpecScribe/HtmlTemplater.cs)), epic pages ([HtmlRenderAdapter.Epics.cs:206](src/SpecScribe/HtmlRenderAdapter.Epics.cs)), story pages ([HtmlRenderAdapter.Epics.cs:354](src/SpecScribe/HtmlRenderAdapter.Epics.cs)), and retros ([RetroTemplater.cs:116](src/SpecScribe/RetroTemplater.cs)). A change here reaches all of them by construction — and it must stay **pure CSS** (the webview CSP blocks non-nonce'd JS).

**AC3 — the retirement notice renders inline among active stories.** Story 3.4 is retired via a free **HTML comment** in epics.md ([epics.md:588-591](_bmad-output/planning-artifacts/epics.md)): `<!-- Story 3.4 retired 2026-07-08 … -->`. That comment sits just before the next story, so [`EpicsParser.BuildStoryCard`](src/SpecScribe/EpicsParser.cs:437-451) **peels it as the next story's leading comment** and renders it as that story card's `userStoryNoteHtml` — a `.md-comment` aside **inline above an active story** ([HtmlRenderAdapter.Epics.cs:228-231](src/SpecScribe/HtmlRenderAdapter.Epics.cs)). That is exactly F6's "retirement notice sits inline among active stories." (Retired stories themselves produce **no** story card — 3.4 is fully commented out — so AC3 is about the *notice*, not a card.)

### The load-bearing constraint: no new authoring schema

Epic 9/10's repeated, owner-flagged principle (`spec` memories for 9.3/9.4/9.6/10.4): **derive from what artifacts already contain; do not invent authoring markup.** So AC1 recognizes the syntax authors *already* type (`[[name]]`, `file:line`, `[ASSUMPTION: …]`), AC3 recognizes the retirement **comment** authors already write, and everything **degrades to as-is** when the shape isn't recognized (NFR8). No `retired:` frontmatter, no `<!-- @retired -->` directive — recognize the existing free comment.

## Acceptance Criteria

**AC1 (References and assumption tags render as designed elements, never raw syntax)**
Given prose containing `[[wiki-link]]` names or `file:line` reference syntax,
When the page renders,
Then references render as **styled chips** (or collect into a references appendix), **never as raw syntax** — the chip treatment is independent of and composes with Story 7.2's clickability (a chip that 7.2 resolved stays a link; one it didn't is a non-link chip),
And `[ASSUMPTION: …]` tags are **styled via the Story 2.6 annotation treatment** (the muted `.md-comment` visual vocabulary),
And the treatment never touches text inside code spans / `<pre>` / existing `<a>` anchors / HTML attributes (a `file:line` inside a code sample stays verbatim), degrading to as-is on any unrecognized shape (NFR8).

**AC2 (Long-page "On this page" TOCs group subsections under collapsible parents; the on-page-TOC invariant is preserved)**
Given a long artifact with many sections,
When its "On this page" TOC renders,
Then level-3 subsections **group under collapsible level-2 parents** (a parent with children is a native, pure-CSS `<details>` disclosure; parents with no children stay plain links),
And the disclosure uses **no information-bearing JavaScript** (webview-CSP-safe),
And **every long page keeps an on-page TOC** — the invariant that a TOC-bearing page still renders a TOC is preserved (the empty-entries → single-column fallback at [Toc.cs:20,45](src/SpecScribe/Toc.cs) is unchanged), and no TOC link becomes a dead anchor.

**AC3 (Retired/superseded work items render in a collapsed section that preserves history)**
Given retired or superseded work items on a parent page (for example Story 3.4's retirement notice on the epic-3 page),
When the parent page renders,
Then they render in a **collapsed `<details>` section** ("Retired") that preserves the history **without cluttering the active list** — the notice is no longer an inline `.md-comment` aside pinned above an active story,
And the collapse is native/pure-CSS (no JS), degrading to today's inline rendering when no retirement/superseded notice is recognized (NFR8), and never dropping or reordering active content.

## Design Direction — the four seams (review checkpoints)

Four small, orthogonal changes. **AC1's post-process seam and AC3's detection heuristic are the #1 review checkpoints** (confirm before wiring). Recommended shapes below; latitude noted.

### AC1 — a reference/annotation post-process, alongside the existing linkifiers

The cleanest seam is the **whole-page post-process** that already runs every prose treatment on every generated page: [`SiteGenerator.ApplyReferenceLinks`](src/SpecScribe/SiteGenerator.cs:2212), where `RequirementLinkifier` → `StoryEpicLinkifier` → `CodeReferenceLinkifier` already run in sequence, anchor-aware, over rendered HTML. Add the new treatment **there** so it reaches doc bodies, story remainders, ADRs, epics overview, and rendered comment asides uniformly — the same reasoning that put `CodeReferenceLinkifier` there ([CodeReferenceLinkifier.cs:8-11](src/SpecScribe/CodeReferenceLinkifier.cs)).

Recommend **one new static class** with the same anchor-split / code-span-aware shape the existing linkifiers use ([CodeReferenceLinkifier.cs:36-45](src/SpecScribe/CodeReferenceLinkifier.cs) `AnchorSplit` + `<code>`-tolerant `InlineCitation` is the exact model to copy):

```csharp
public static class ReferenceChipRenderer   // name at latitude
{
    // [[name]]                → <span class="ref-chip">name</span>   (F3 wiki-link chip)
    // [ASSUMPTION: text]      → <span class="md-comment-inline assumption-tag">…</span>  (F5, reuse 2.6 vocab)
    // bare file:line in prose → styled chip IF not already an <a> (7.2 may have linked it) and not in code
    public static string Render(string html) { /* anchor-split; skip <code>/<pre>; regex-replace non-anchor parts */ }
}
```

Decisions to confirm at review:
1. **Ordering vs `CodeReferenceLinkifier`.** Run this **after** `CodeReferenceLinkifier` ([SiteGenerator.cs:2228](src/SpecScribe/SiteGenerator.cs)) and make the `file:line` matcher **anchor-aware** so a citation 7.2 already turned into `<a href="code/…#L{n}">…</a>` keeps its link and only gets chip *styling* (or is left alone), while a bare unresolved `file:line` in prose gets non-link chip styling. Never re-wrap or double-link. **Confirm the interaction with 7.2 explicitly** — this is the trickiest boundary. (Latitude: a chip that is *purely* styling on the existing link, applied by adding a class in the href rewriter, is also acceptable — but a separate post-process keeps 7.2 untouched.)
2. **`file:line` false-positive guard.** `file:line` in prose is ambiguous (`foo.cs:42`, but also `note: 3`). Gate on an **extension-bearing filename + `:` + digits** (mirror [`CodeReferenceLinkifier.IsRelativeCodeHref`](src/SpecScribe/CodeReferenceLinkifier.cs:165-182): require a `.ext` on the last segment) and **never** match inside `<code>`/`<pre>`. When in doubt, degrade to raw (NFR8) — a missed chip is far cheaper than mangling a code sample.
3. **`[[wiki-link]]` target.** These are memory/cross-doc names that usually have **no** portal page. Render as a **non-link chip** (`<span class="ref-chip">`), not a broken link. If a `[[name]]` happens to resolve to a known page (a story/epic/doc), linking is a *nice-to-have* — recommend **chip-only** for this story to avoid a second resolver; the AC only requires "never raw syntax."
4. **`[ASSUMPTION: …]` styling.** Reuse the existing `.md-comment-inline` class (muted, italic — [specscribe.css:427-431](src/SpecScribe/assets/specscribe.css)) plus a small `.assumption-tag` modifier if the owner wants the word "ASSUMPTION" to read as a label. **No new `--status-*` token, not color-only** — keep the word visible. Match case-insensitively; you may generalize to a small tag set (`ASSUMPTION`, `TODO`, `NOTE`, `DECISION`) at latitude, but AC only requires `ASSUMPTION`.

**Alternative (references appendix) — do NOT build unless the owner asks.** The AC offers "styled chips **or** collect into a references appendix." Chips are strictly simpler, reach every page, and need no per-page collection pass. Recommend **chips**; note the appendix as the rejected alternative.

### AC2 — group the TOC under collapsible parents (one change to `Toc.RenderSidebar`)

Change **only** [`Toc.RenderSidebar`](src/SpecScribe/Toc.cs:18) — the shared seam — so grouping propagates to all page types for free. Keep the ordered `Entry` list input and the dedupe-by-anchor guard ([Toc.cs:28-31](src/SpecScribe/Toc.cs)) exactly as-is; only the **emission** changes:

- Walk the ordered entries. A level-2 entry that is **followed by** one or more level-3 entries becomes a `<details class="toc-group">` whose `<summary>` is the level-2 link and whose body holds the child `.toc-h3` links. A level-2 with **no** children stays a plain `<a class="toc-link">` (don't wrap solitary parents — no empty carets). Level-3 entries never appear at top level (they always have a parent in practice; if a stray leading level-3 appears, render it as a plain link — degrade, never drop).
- **Pure CSS**, native `<details>` — reuse the shared caret pattern already used for `.dev-agent-details` ([specscribe.css:1731-1732](src/SpecScribe/assets/specscribe.css): hidden native marker, `▸`→`▾`). Add `.toc-group`/`.toc-group > summary` rules; recommend **open by default** on wide screens so the invariant "the TOC is visible" holds at a glance, or collapsed with the parent still clickable — **confirm default state at review** (open-by-default is safest for the "keep the TOC" invariant).
- **Preserve the invariant + a11y:** the `<summary>` parent must stay a working jump link to the section (keep the `<a href="#id">` inside the `<summary>`, mirroring the story/epic pattern where a `<h2 id>` lives inside a `<summary>` — see the 9.5 TOC-invariant note). Keep the `aria-label="On this page"` and `.toc-label`. Empty-entry fallback ([Toc.cs:20](src/SpecScribe/Toc.cs)) unchanged.
- **Narrow-screen mode:** the TOC collapses to a horizontal strip under 900px ([specscribe.css:348-368](src/SpecScribe/assets/specscribe.css)). Confirm the grouped markup still lays out as a readable strip there (the `<details>` may need to render flattened/inline under the breakpoint — test it).

> **⚠️ Coordinate with Story 9.5 (`ready-for-dev`, not yet built).** Its `spec` memory says it will **strip `id`s from buried H3s so they drop out of the sidebar** (because `ExtractHeadings` is id-gated) and collapse Dev Notes/References into `<details>`. That is the **opposite** lever from AC2 (9.5 removes H3s from the TOC; AC2 groups them under parents). **This is a direct interaction** — flag it: if 9.5 lands first, some H3s won't reach the TOC and AC2 grouping simply has fewer children (fine, degrades). If AC2 lands first, 9.5's id-strip reduces the child sets. Neither breaks the other, but **do not both "fix the long TOC" in conflicting ways** — confirm the division with the owner (recommend: 9.5 owns *content* collapse of Dev Notes/References bodies; 10.5 owns the *TOC sidebar* grouping). Do not touch `RenderStoryBody`'s `<details>`/`<summary>` content semantics that 9.5 owns.

### AC3 — hoist retirement/superseded notices into a collapsed "Retired" section

The notice is an authored **HTML comment**, currently peeled as the next story's `userStoryNoteHtml` ([EpicsParser.cs:437-451](src/SpecScribe/EpicsParser.cs)) and rendered inline ([HtmlRenderAdapter.Epics.cs:228-231](src/SpecScribe/HtmlRenderAdapter.Epics.cs)). Recommended shape (confirm at review — this is the AC3 checkpoint):

- **Detect** a retirement/superseded notice with a **tolerant** matcher over the already-peeled leading-comment text (`retired` / `superseded` / `deprecated`, case-insensitive, word-boundaried) — reusing the existing `LeadingHtmlComments` peel and `HasCommentText` guard ([EpicsParser.cs:24-29,530](src/SpecScribe/EpicsParser.cs)), so **no new authoring schema**. A matched comment is classified as a "retired notice" and **not** attached to the following story as its `userStoryNoteHtml`.
- **Collect** matched notices per epic into a new `EpicPageView` field (e.g. `RetiredNoticesHtml`, a list of rendered `.md-comment` fragments) — additive to the view model, mirroring how the existing `userStoryNoteHtml` is a rendered fragment.
- **Render** them at the **end** of the epic body ([HtmlRenderAdapter.Epics.cs:196-202](src/SpecScribe/HtmlRenderAdapter.Epics.cs), after the story cards) as a single `<details class="chart-panel retired-section">` (collapsed by default) with a `<summary>Retired</summary>` and the notices inside. Reuse the shared caret pattern (`.dev-agent-details` / `.toc-group`). Add **one** TOC entry ("Retired") so the section is reachable and the on-page-TOC invariant holds.
- **Degrade (NFR8):** no matched notice → **no** Retired section, and leading comments that aren't retirement notices stay exactly where they are (an ordinary seat-mapping note above a story is untouched). Never drop or reorder active story cards.
- **Latitude:** scope AC3 to **epic pages** (the concrete F6 case). If the owner wants the pattern generalized (deferred-work page, sprint board), note it but keep this story to the epic-page surface — F6 itself says "a pattern that will recur," i.e. seed it here.

## Tasks / Subtasks

- [x] **Task 1 — `ReferenceChipRenderer`: wiki-link + file:line chips + assumption tags** (AC: 1)
  - [x] Add `src/SpecScribe/ReferenceChipRenderer.cs` (name at latitude). Anchor-split + `<code>`/`<pre>`-skipping over rendered HTML, modeled on [`CodeReferenceLinkifier`](src/SpecScribe/CodeReferenceLinkifier.cs:36-130). Matchers: `[[name]]` → `<span class="ref-chip">`; `[ASSUMPTION: …]` (case-insensitive) → `<span class="md-comment-inline assumption-tag">`; bare extension-bearing `file:line` in non-anchor, non-code text → chip styling (anchor-aware so 7.2's links are preserved). Pure, no I/O, HTML-escape-safe.
  - [x] Wire into [`SiteGenerator.ApplyReferenceLinks`](src/SpecScribe/SiteGenerator.cs:2212), **after** `CodeReferenceLinkifier` ([:2228](src/SpecScribe/SiteGenerator.cs)). Confirm the `file:line` matcher never re-links or double-wraps 7.2's output (the #1 boundary).
  - [x] Add `.ref-chip` + `.assumption-tag` CSS reusing existing tokens (`--parchment-dark`/`--rust`/`--ink-light`, the `.md-comment-inline` muted-italic vocabulary) — no new `--status-*` token, not color-only.

- [x] **Task 2 — Collapsible grouped TOC** (AC: 2)
  - [x] In [`Toc.RenderSidebar`](src/SpecScribe/Toc.cs:18) only: emit level-2 entries with following level-3 children as `<details class="toc-group">` (summary = the parent link, body = child `.toc-h3` links); childless level-2 stays a plain `<a>`. Preserve the anchor-dedupe guard, `aria-label`, empty-fallback, and the summary-as-jump-link (invariant + a11y).
  - [x] Add `.toc-group`/`.toc-group > summary` CSS reusing the `.dev-agent-details` caret pattern ([specscribe.css:1731-1733](src/SpecScribe/assets/specscribe.css)); confirm the narrow-screen strip ([specscribe.css:348-368](src/SpecScribe/assets/specscribe.css)) still reads. Decide default open/collapsed state (recommend open on wide) and record it.
  - [x] **Coordinate with Story 9.5** (see Design Direction warning) — do not both restructure the long TOC in conflicting ways.

- [x] **Task 3 — Collapsed "Retired" section on epic pages** (AC: 3)
  - [x] In `EpicsParser`, add a tolerant retirement/superseded detector over the peeled leading-comment text ([EpicsParser.cs:437-451,24-29,530](src/SpecScribe/EpicsParser.cs)); a matched comment is classified as a retired notice, **not** attached as the next story's `userStoryNoteHtml`.
  - [x] Add an additive `RetiredNoticesHtml` (rendered-fragment list) to the epic view model; populate it from the matched notices.
  - [x] Render a collapsed `<details class="chart-panel retired-section"><summary>Retired</summary>…</details>` after the story cards in [`RenderEpicBody`](src/SpecScribe/HtmlRenderAdapter.Epics.cs:196-202); add a single "Retired" TOC entry. Degrade to no-section when empty; leave non-retirement leading comments in place.
  - [x] Add `.retired-section` CSS (reuse the shared caret + muted vocabulary).

- [x] **Task 4 — Tests** (AC: 1, 2, 3)
  - [x] **`ReferenceChipRenderer`** unit tests (pure-function style, like `CodeReferenceLinkifier`/`RequirementLinkifier` tests): `[[name]]`→chip; `[ASSUMPTION: x]`→annotation span; `file:line`→chip; **negatives** — `file:line` inside `<code>`/`<pre>` untouched; text inside an existing `<a>` untouched; a 7.2-resolved code link is not double-wrapped; ambiguous `word: 3` (no extension) left raw.
  - [x] **TOC grouping** tests: an entry list with h2→h3→h3 renders one `<details class="toc-group">` with two children; a childless h2 stays a plain `<a>`; anchor-dedupe still holds; empty list → `""` (invariant); the summary keeps its `#id` jump link.
  - [x] **Retired section** tests: an epic whose story is preceded by a `<!-- Story N retired … -->` comment renders a collapsed `Retired` `<details>` after the cards and **no** inline `.md-comment` note above the following story; an epic with an ordinary seat-note comment renders it inline as today (degrade); an epic with no retirement notice renders no Retired section; active cards preserved and in order.
  - [x] **Parity + golden.** These touch shared render paths: AC1 post-process rides `ApplyReferenceLinks` (HTML write path — **confirm whether the webview/SPA adapters also apply it**, matching the existing linkifiers' behavior; flag any `HostRenderException`/parity-registry need); AC2's `Toc.RenderSidebar` and AC3's `RenderEpicBody` are the shared adapters that HTML+webview+SPA all use → keep `RenderParity`/SPA/webview green (**no new exception expected** — confirm). **Regenerate `GoldenContentFingerprint`** at [SiteGeneratorAdapterTests.cs:213](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) — TOC changes hit every long page, chips hit prose, the Retired section hits epic pages. **Confirm the baseline is green first** — a known unrelated pre-existing golden drift (`977cb973`, spec-comment-block work) has ridden recent commits; do not inherit or mask it. Eyeball the diff to prove it is only chips/TOC-grouping/Retired-section.

- [x] **Task 5 — Verify end-to-end on the real repo** (AC: 1, 2, 3)
  - [x] `dotnet run` a full generate: open a long page (PRD or story 3.7) → the "On this page" TOC groups subsections under collapsible parents and is still present; open a story/doc with prose containing `[[…]]`/`file:line`/`[ASSUMPTION: …]` → all render as chips/annotations, no raw brackets; open the **epic-3** page → the Story 3.4 retirement notice is in a collapsed "Retired" section, **not** pinned above an active story.
  - [x] Confirm `specscribe webview` (CSP — the `<details>` disclosures must work with **no** JS) and `--spa` render the same (they ride the shared TOC/adapter seams).

### Review Findings

- [x] [Review][Decision] Retirement-keyword false-positive tolerance — Decision resolved (option 1): keep the tolerant `\b(retired|superseded|deprecated)\b` matcher as designed; accept incidental-mention risk over inventing a stricter authoring schema (NFR8). No code change.
- [x] [Review][Patch] Align `ReferenceChipRenderer.ProtectedSplit` with `AbbreviationExpander` — also skip `<script>`/`<style>`/`<head>` spans [`src/SpecScribe/ReferenceChipRenderer.cs:23`]
- [x] [Review][Patch] After anchor-dedupe drops a duplicate level-2, its following level-3 children group under the previous parent [`src/SpecScribe/Toc.cs:31`]
- [x] [Review][Patch] Restrict `HoistBetweenStoryRetiredComments` to true between-story placement (next non-blank is a story heading or EOF) so mid-narrative / mid-AC keyword comments are not blanked [`src/SpecScribe/EpicsParser.cs:656`]
- [x] [Review][Patch] Add TOC regression for orphaned h3s after a duplicate level-2 is deduped [`tests/SpecScribe.Tests/TocTests.cs`]
- [x] [Review][Defer] `<kbd>`/`<samp>` inner text not protected by chip ProtectedSplit [`src/SpecScribe/ReferenceChipRenderer.cs:23`] — deferred, pre-existing; same open-tag-only split pattern as sibling post-processors
- [x] [Review][Defer] Retirement comments in the epic preamble (before the first story) are never hoisted [`src/SpecScribe/EpicsParser.cs:755`] — deferred, pre-existing scope; AC3 targets leading-comment and between-story shapes (F6), not preamble goal text

## Dev Notes

### Architecture patterns & constraints (must follow)

- **One shared TOC seam (AC2).** `Toc.RenderSidebar` is the *only* place TOC markup is produced ([Toc.cs:6-9](src/SpecScribe/Toc.cs) doc-comment). Change it once; every page type inherits grouping. Do **not** fork per-page TOC logic. Keep the ordered-`Entry` contract, the anchor-dedupe ([Toc.cs:28-31](src/SpecScribe/Toc.cs)), and the empty→single-column fallback ([Toc.cs:20,45](src/SpecScribe/Toc.cs)).
- **Reuse the existing linkifier architecture (AC1).** The post-process lives beside `RequirementLinkifier`/`StoryEpicLinkifier`/`CodeReferenceLinkifier` in `ApplyReferenceLinks`, using the same anchor-split + `<code>`-aware pattern ([CodeReferenceLinkifier.cs:36-45,103-130](src/SpecScribe/CodeReferenceLinkifier.cs)). Do not re-implement HTML parsing; copy the proven split.
- **Extend Story 2.6, don't rebuild it (AC1 assumption).** Reuse the `.md-comment-inline` CSS class ([specscribe.css:427-431](src/SpecScribe/assets/specscribe.css)) for `[ASSUMPTION:]`. `CommentAnnotationRenderer` ([CommentAnnotationRenderer.cs](src/SpecScribe/CommentAnnotationRenderer.cs)) stays untouched — it only handles real `<!-- -->` comments; assumption tags are a new matcher wearing the same clothes.
- **No new authoring schema (the Epic 9/10 load-bearing principle).** Recognize what's already typed — `[[…]]`, `file:line`, `[ASSUMPTION:]`, and the retirement **comment** — never add `retired:` frontmatter or a directive. Degrade to as-is on any unrecognized shape.
- **No information-bearing JavaScript; pure CSS / native `<details>`; no color-only signals.** Both disclosures (AC2 TOC groups, AC3 Retired section) are native `<details>` reusing the shared `.dev-agent-details` caret ([specscribe.css:1731-1733](src/SpecScribe/assets/specscribe.css)) — this is mandatory for the webview CSP. Chips and the assumption tag carry visible text/shape, never color alone.
- **Determinism + golden fingerprint.** Output is byte-pinned ([SiteGeneratorAdapterTests.cs:213](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)); regenerate the constant **deliberately** and diff to prove only the intended changes. Confirm the baseline is green first (mind the known `977cb973` drift).
- **Anchor/code safety is the AC1 correctness core.** The post-process must never mutate text inside `<a>…</a>`, `<code>`/`<pre>`, or HTML attributes — a `file:line` in a code sample, or a `[[x]]` inside an already-emitted link, must survive verbatim. This is what the tests' negative cases pin.

### Source tree — files to touch

- `src/SpecScribe/ReferenceChipRenderer.cs` — wiki-link/file:line chips + assumption tags (NEW — the AC1 heart).
- `src/SpecScribe/SiteGenerator.cs` — wire the new renderer into `ApplyReferenceLinks` after `CodeReferenceLinkifier` ([:2212,2228](src/SpecScribe/SiteGenerator.cs)) (UPDATE).
- `src/SpecScribe/Toc.cs` — `RenderSidebar` emits collapsible `<details class="toc-group">` for parents with children ([:18](src/SpecScribe/Toc.cs)) (UPDATE).
- `src/SpecScribe/EpicsParser.cs` — tolerant retirement/superseded detection over the peeled leading comment; classify-not-attach ([:24-29,437-451,530](src/SpecScribe/EpicsParser.cs)) (UPDATE).
- `src/SpecScribe/EpicsView.cs` — additive `RetiredNoticesHtml` on the epic view model (UPDATE).
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` — render the collapsed Retired section + its TOC entry in `RenderEpicBody` ([:196-202](src/SpecScribe/HtmlRenderAdapter.Epics.cs)); ensure a retirement comment no longer lands as the next card's `userStoryNoteHtml` ([:228-231](src/SpecScribe/HtmlRenderAdapter.Epics.cs)) (UPDATE).
- `src/SpecScribe/assets/specscribe.css` — `.ref-chip`, `.assumption-tag`, `.toc-group`(+summary), `.retired-section`; reuse existing tokens + the `.dev-agent-details` caret (UPDATE).
- Tests: new `ReferenceChipRendererTests.cs`; `Toc` grouping tests; epic retired-section tests; **regenerate `GoldenContentFingerprint`**; keep parity/SPA/webview suites green (UPDATE/NEW).

### UPDATE files — current state & what must be preserved

- **`Toc.RenderSidebar`** ([Toc.cs:18-37](src/SpecScribe/Toc.cs)): flat nav; dedupes by anchor so a browser never lands the wrong first match; `.toc-h3` = indented child. **Preserve** the dedupe, the `aria-label`/`toc-label`, and the empty→`""` return; only the emission (grouping) changes. Every page type reads this — a regression hits all of them, so the grouping must degrade to today's flat behavior when there are no h3 children.
- **`Toc.WrapWithSidebar` / `ExtractHeadings`** ([Toc.cs:43-85](src/SpecScribe/Toc.cs)): the two-column shell and the id-gated remainder-heading extractor. **Do not** change these — grouping is a `RenderSidebar` concern; the entry list (and which headings carry ids) is unchanged here. (Story 9.5 plans to change *which* H3s carry ids — that's 9.5's lever, coordinate, don't pre-empt it.)
- **`EpicsParser.BuildStoryCard` leading-comment peel** ([EpicsParser.cs:437-451](src/SpecScribe/EpicsParser.cs)): peels leading `<!-- -->` comment(s) into `userStoryNoteHtml` (a legit seat-mapping note above a story) via `RenderBlock`; `HasCommentText` ([:530](src/SpecScribe/EpicsParser.cs)) guards empty asides. **Preserve** the ordinary-note path — only comments that match the retirement/superseded pattern get diverted to the Retired section; everything else renders inline exactly as today.
- **`RenderEpicBody`** ([HtmlRenderAdapter.Epics.cs:146-209](src/SpecScribe/HtmlRenderAdapter.Epics.cs)): header → overview → progress/sunburst → retro affordance → story cards, building the TOC inline. **Preserve** the story-card loop, the sunburst/progress, and the `WrapWithSidebar` call; append the Retired section after the cards and add exactly one TOC entry. Shared by HTML+webview+SPA — no parity exception expected.
- **`ApplyReferenceLinks`** ([SiteGenerator.cs:2212-2231](src/SpecScribe/SiteGenerator.cs)): runs FR/Story/code linkifiers in order, each anchor-aware and idempotent, on every page. **Preserve** the ordering and idempotence; the new renderer runs last and must be equally anchor-aware so it never disturbs the links already emitted.

### Interactions & coordination

- **Story 7.2 (`review`) — `file:line` clickability vs chip styling.** 7.2 makes `[Source: path]`/view-source links clickable; AC1 adds chip *styling* and covers **bare** `file:line` in prose that 7.2 doesn't touch. Run after 7.2's linkifier, anchor-aware; **confirm no double-linking** at review.
- **Story 9.5 (`ready-for-dev`) — the long-TOC lever.** 9.5 collapses Dev Notes/References **content** and strips buried-H3 `id`s from the sidebar; AC2 groups sidebar H3s under parents. Complementary but must not conflict — recommend 9.5 owns content collapse, 10.5 owns TOC-sidebar grouping. Neither should touch the other's `<details>` semantics. **This is the #1 cross-story checkpoint.**
- **Story 2.6 (`done`) — the annotation vocabulary** reused for `[ASSUMPTION:]`. Don't modify `CommentAnnotationRenderer`.

### Testing standards

- xUnit. `ReferenceChipRenderer` and the `Toc` grouping are pure functions — unit-test directly against strings (the `CodeReferenceLinkifier`/`RequirementLinkifier`/`StatusStyles` test style). Assert both **presence** (chip/annotation emitted; group `<details>` formed; Retired section present) and **absence/degrade** (code-span/anchor text untouched; childless h2 stays plain; no retirement notice → no section; ordinary seat-note stays inline).
- Generation-level page tests use the temp-root fixture pattern (`Directory.CreateTempSubdirectory`, `IDisposable`) as in the `SiteGenerator*` suites; assert the epic-3-shaped retirement comment lands in the Retired section and not above a card.
- Regenerate `GoldenContentFingerprint` **deliberately** and diff to prove the change is only chips/TOC-grouping/Retired-section. Confirm the baseline is green first (mind the known unrelated pre-existing drift).

### Out of scope (do not build)

- No nav grouping / Insights nav / Structure retirement (Story 10.1). No chart metadata standard (10.2). No glossary/how-to-read/`<abbr>` work (10.3 — the acronym-expansion clause of F5's neighbors belongs there, not here). No date/sequencing/ADR-state work (10.4). No insight-chart coupling/heatmap-dead-zone polish (10.6).
- **No references appendix** — chips are the chosen treatment (the AC's "or"); only build the appendix if the owner explicitly asks.
- **No `[[wiki-link]]` resolution to portal pages** — non-link chips only for this story; resolving them to real pages is a possible follow-up, not a requirement.
- **No new authoring schema** — recognize `[[…]]`/`file:line`/`[ASSUMPTION:]`/the retirement comment that artifacts already carry; degrade to as-is when absent.
- **No JavaScript** for either disclosure — native `<details>` only (webview CSP). Do not touch `Toc.WrapWithSidebar`/`ExtractHeadings` or Story 9.5's content-collapse `<details>` semantics.
- No generalization of the Retired section beyond epic pages (deferred-work page, sprint board) — F6 seeds the pattern on epic pages; broader reuse is a later story.

### Project Structure Notes

- Output dir is `SpecScribeOutput` (never `docs/live`) — [see generate-output-dir-is-specscribeoutput].
- `src/SpecScribe/assets/specscribe.css` is the styling source of truth; the generated `docs/live/specscribe.css` is untracked output — never edit the generated copy.
- If working in a git worktree, target the worktree path (main has a background auto-committer) — [see worktree-edits-must-target-worktree-path].
- The golden fingerprint constant lives at [SiteGeneratorAdapterTests.cs:213](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs); its volatile-token normalizers are at [SiteGeneratorAdapterTests.cs:220-256](tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) — [see golden-diff-normalization-gotchas].
- Route any new stage/badge color through the six `--status-*` tokens — [see specscribe-status-token-system]; but AC1/AC2/AC3 add **no** status color (chips/annotations reuse muted parchment/rust/ink vocabulary), so no `--status-*` change is expected.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.5: Document Rendering Legibility] (lines 1799-1822) — the three ACs; Epic 10 FR27/28/29, UX-DR25/27/28/29/30, NFR8.
- [Source: _bmad-output/planning-artifacts/epics.md#UX-DR27] (line 154) — wiki-link/file:line → chips/appendix, never raw. [#UX-DR28] (155) — long-TOC collapsible grouping + on-page-TOC invariant. [#UX-DR29] (156) — `[ASSUMPTION:]` via annotation treatment + retired-work collapse.
- [Source: docs/MissingFeatures.md#F3] (96-97) — reference-rendering treatment (separate from 7.2 clickability). [#F4] (99-100) — grouped collapsible TOC + invariant. [#F5] (102-103) — `[ASSUMPTION:]` extends Story 2.6. [#F6] (105-106) — retired-work collapsed section on epic pages.
- [Source: docs/Epic3UXFeedback.md] (lines 90, 95, 101, 142, 143) — the live-review twins: retirement notice inline; TOC invariant "keep it"; raw wiki/file:line leak; 28-flat-entry TOC; plain assumption tags.
- [Source: src/SpecScribe/Toc.cs#RenderSidebar] (18-37) — the single flat TOC renderer to make collapsible; the anchor-dedupe + empty-fallback to preserve.
- [Source: src/SpecScribe/CommentAnnotationRenderer.cs] + [src/SpecScribe/assets/specscribe.css] (417-431) — Story 2.6's `.md-comment(-inline)` treatment to reuse for `[ASSUMPTION:]`.
- [Source: src/SpecScribe/CodeReferenceLinkifier.cs] (8-11, 36-45, 103-182) — the anchor-split + `<code>`-aware post-process model; the `file:line` clickability (7.2) to compose with, not duplicate.
- [Source: src/SpecScribe/SiteGenerator.cs#ApplyReferenceLinks] (2212-2231) — the whole-page post-process where the new renderer slots after `CodeReferenceLinkifier`.
- [Source: src/SpecScribe/EpicsParser.cs] (24-29, 437-451, 530) — the leading-comment peel + `HasCommentText` guard where retirement-notice detection slots.
- [Source: src/SpecScribe/HtmlRenderAdapter.Epics.cs#RenderEpicBody] (146-209, 228-231) — the epic body + story-card loop where the Retired section renders and the inline `userStoryNoteHtml` currently pins the notice.
- [Source: src/SpecScribe/assets/specscribe.css] (1731-1733) — the shared `.dev-agent-details` `▸`→`▾` caret to reuse for both new `<details>` disclosures.
- [Source: _bmad-output/planning-artifacts/epics.md] (588-591) — Story 3.4's retirement comment — the exact AC3 case.
- [Source: _bmad-output/implementation-artifacts/10-4-consistent-dates-and-event-sequencing.md] — the immediately-preceding Epic 10 story; the "single shared seam + degrade-to-as-is + golden regen" discipline this story mirrors.

## Dev Agent Record

### Agent Model Used

Claude (Sonnet 5), via the `bmad-dev-story` workflow.

### Debug Log References

- Real-repo `dotnet run generate` on `epic-3.html` surfaced a live parsing bug the design doc's simplified example didn't anticipate: the actual Story 3.4 retirement comment sits BETWEEN two stories (after Story 3.3's last AC line, before "### Story 3.5"), not as the leading comment of either story's own region. Without a fix it was silently swept into Story 3.3's trailing AC-block text as literal gherkin-line junk (a pre-existing quirk, now also fixed as a side effect). Added `EpicsParser.HoistBetweenStoryRetiredComments` — a pre-pass that blanks a matched between-story retirement comment's lines before per-story parsing runs, so it neither pollutes the preceding story's AC content nor becomes the following story's leading-comment note.
- Real-repo verification also surfaced that `ReferenceChipRenderer`'s protected split didn't exclude `<svg>...</svg>` subtrees: chart tooltips (e.g. the sunburst's `<title>` text) carry raw, unrendered text inside SVG, where SVG `<title>` has no HTML sub-parsing — an injected `<span>` there would show as literal visible markup instead of a styled chip. Fixed by adding `<svg>...</svg>` to the protected-span alternation (mirrors `AbbreviationExpander.ProtectedSplit`'s existing `<svg>` protection).

### Completion Notes List

- AC1: `ReferenceChipRenderer` (new) matches `[[wiki-link]]` → non-link `.ref-chip`, `[ASSUMPTION: …]` → `.md-comment-inline.assumption-tag` (Story 2.6 vocabulary), and bare extension-bearing `file:line` → `.ref-chip`, in one combined regex (no double-wrap risk between the three shapes). Wired into `SiteGenerator.ApplyReferenceLinks` after `CodeReferenceLinkifier`, before `AbbreviationExpander`. A citation 7.2 already resolved into a real `<a>` is left untouched (Decision #1 from the design doc — the simplest boundary, keeps 7.2 untouched). Protected-span split mirrors `CodeReferenceLinkifier`/`AbbreviationExpander` (`<a>`/`<code>`/`<pre>`/`<svg>` pairs + any standalone tag never rewritten).
- AC2: `Toc.RenderSidebar` groups a level-2 entry followed by level-3 children into `<details class="toc-group" open>` (summary = the parent's own jump link); a childless level-2 stays a plain `<a>` (no empty caret); a stray leading level-3 degrades to a plain link (NFR8). Anchor-dedupe, `aria-label`, and the empty-fallback are unchanged. Narrow-screen strip gets `.toc-group { flex-basis: 100% }` so a grouped parent takes its own row instead of cramming between single-link chips.
- AC3: retirement/superseded notices are detected in TWO shapes — (a) a story's own leading comment (existing `ParseStory` peel, now classify-not-attach via a word-boundaried `retired|superseded|deprecated` regex), and (b) the REAL-WORLD shape discovered during Task 5 verification — a standalone comment sitting between two stories (`HoistBetweenStoryRetiredComments`, new). Both feed an additive `EpicInfo.RetiredNoticesHtml` → `EpicPageView.RetiredNoticesHtml`, rendered as one collapsed `<details class="chart-panel retired-section">` after the story-card loop in `RenderEpicBody`, with a single "Retired" TOC entry. Verified against the real Story 3.4 notice on `epics/epic-3.html`.
- Golden fingerprint regenerated twice (once for the three ACs, once more for the SVG-protection fix — the fixture used by `SiteGeneratorAdapterTests` doesn't exercise SVG tooltips, so only the first regen actually changed that constant); baseline confirmed green before each regen (stashed working changes, ran the fingerprint test alone, popped the stash) per the story's review checkpoint.
- Full suite: 1612/1612 green. Verified end-to-end via `dotnet run generate`, `--spa`, and `webview` on this actual repo — all three ACs reach the SPA/webview surfaces (no new parity/`HostRenderException` needed, confirmed by grep on the emitted JSON).
- Flagged (out of scope, spawned as a separate background task): a pre-existing bug on baseline `main`, unrelated to this story — `*emphasis*` text inside a heading renders as the literal C# type name `Markdig.Syntax.Inlines.EmphasisInline` in the "On this page" TOC (reproduced on `docs/adrs/0006-delivery-architecture-and-distribution.html`).

### File List

- `src/SpecScribe/ReferenceChipRenderer.cs` — NEW (AC1: `[[wiki-link]]`/`[ASSUMPTION: …]`/bare `file:line` → chips/annotation)
- `src/SpecScribe/SiteGenerator.cs` — UPDATE (wire `ReferenceChipRenderer.Render` into `ApplyReferenceLinks`, after `CodeReferenceLinkifier`, before `AbbreviationExpander`)
- `src/SpecScribe/Toc.cs` — UPDATE (`RenderSidebar` groups level-3 children under a collapsible level-2 `<details class="toc-group">`)
- `src/SpecScribe/EpicsParser.cs` — UPDATE (retirement/superseded classification on a story's own leading comment; new `HoistBetweenStoryRetiredComments` for the between-story real-world shape; `SectionEntry.RetiredNoticesHtml`)
- `src/SpecScribe/EpicsModel.cs` — UPDATE (additive `EpicInfo.RetiredNoticesHtml`)
- `src/SpecScribe/EpicsView.cs` — UPDATE (additive `EpicPageView.RetiredNoticesHtml`)
- `src/SpecScribe/EpicsViewBuilder.cs` — UPDATE (`BuildEpic` threads `epic.RetiredNoticesHtml` onto the view)
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` — UPDATE (`RenderEpicBody` renders the collapsed Retired `<details>` after story cards + one TOC entry)
- `src/SpecScribe/assets/specscribe.css` — UPDATE (`.ref-chip`, `.assumption-tag`, `.toc-group`(+narrow-screen), `.retired-section`)
- `tests/SpecScribe.Tests/ReferenceChipRendererTests.cs` — NEW (12 tests: chip/annotation matching + anchor/code/svg-safety negatives)
- `tests/SpecScribe.Tests/TocTests.cs` — UPDATE (+3 grouping tests: grouped parent, childless parent, stray-h3 degrade)
- `tests/SpecScribe.Tests/EpicsParserTests.cs` — UPDATE (+5 tests: leading-comment retirement, between-story retirement, ordinary-note non-classification, keyword case-insensitivity)
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs` — UPDATE (+2 tests: Retired section present/absent + TOC entry)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — UPDATE (regenerated `GoldenContentFingerprint`)

## Change Log

- 2026-07-18: dev-story — implemented all three ACs (`ReferenceChipRenderer` chips/annotations, grouped collapsible TOC, collapsed epic Retired section); discovered and fixed two real-repo-only gaps beyond the design doc's simplified examples: (1) the actual Story 3.4 retirement notice sits BETWEEN two stories, not as either story's leading comment — added `EpicsParser.HoistBetweenStoryRetiredComments`; (2) `ReferenceChipRenderer` needed to skip `<svg>...</svg>` subtrees so chart-tooltip text (SVG `<title>`, no HTML sub-parsing) never gets literal `<span>` markup injected. Golden fingerprint regenerated (confirmed baseline green first). 1612/1612 tests green. Verified end-to-end via `dotnet run generate`, `--spa`, and `webview` against this repo's own `epics/epic-3.html` and `story-10-5.html`. Status → review.
- 2026-07-20: code-review — adversarial review (Blind/Edge/Acceptance); decision kept tolerant retirement matcher; applied 4 patches (script/style/head protect, TOC orphan-after-dedupe, between-story hoist guard, regression tests). Status → done.
