---
title: 'Story 7.2 bug: source-file citations render markdown syntax on story pages'
type: 'bugfix'
created: '2026-07-13'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: '094a73d191888fb86ba1307b151230a82a9046dc'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On rendered story/epic pages, source-file citations whose inner content is markdown-link syntax — e.g. `[Source: [GitMetrics.cs:213](../../src/SpecScribe/GitMetrics.cs), [GitMetrics.cs:270-286](../../src/SpecScribe/GitMetrics.cs)]` — render with raw markdown visible (`[GitMetrics.cs:213(../../src/SpecScribe/GitMetrics.cs)`) instead of as clickable links. The decorative-bracket stripper in `EpicsParser` (`SourceCitationBrackets`, regex `\[Source:\s*(.*?)\]`) is non-greedy and matches the citation's *first inner* `]` (the link label's closing bracket), truncating the link and corrupting it before Markdig ever renders it.

**Approach:** Make `SourceCitationBrackets` balance-aware for one level of inner `[...]` brackets so it strips the *outer* `[Source: … ]` wrapper while leaving inner markdown links intact. Fix the shared regex once; it is consumed by all three story-path strip sites (remainder, acceptance-criteria bodies, named sections).

## Boundaries & Constraints

**Always:**
- Fix the single shared `SourceCitationBrackets` regex so all three call sites (`EpicsParser.cs` lines 137, 231, 273) benefit.
- Preserve existing behavior for every non-markdown-link citation: plain path (`[Source: path.md — note]`), code-span (`[Source: \`src/X.cs:15\`]`), and multiple separate `[Source: …]` citations on one line must strip exactly as before.
- After the strip, inner markdown links must survive to Markdig and then to the page-level `CodeReferenceLinkifier`, resolving to in-portal code pages (or external `--code-url`) exactly like citations on doc pages already do.

**Ask First:**
- If any real citation in the corpus nests brackets deeper than one level (a `[...]` inside a `[...]` label), which the one-level fix would not fully handle — surface it rather than expanding scope silently.

**Never:**
- Do not touch the doc-page citation path (`MarkdownConverter.Convert`) — doc pages do not strip the wrapper and already render correctly.
- Do not change `CodeReferenceLinkifier`, `CodeReferenceScanner`, `SourceLinkifier`, or Markdig pipeline config — the corruption is upstream of all of them.
- Do not attempt to fix the unrelated `[[wiki-link]]` literals leaking from story authoring (out of scope).

## I/O & Edge-Case Matrix

Applied to `SourceCitationBrackets.Replace(input, "$1")` (raw markdown, pre-render):

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Multi-link citation | `[Source: [A.cs:1](../a.cs), [B.cs:2-3](../b.cs)]` | `[A.cs:1](../a.cs), [B.cs:2-3](../b.cs)` (both links intact) | N/A |
| Single-link citation | `[Source: [A.cs:1](../a.cs)]` | `[A.cs:1](../a.cs)` | N/A |
| Link + trailing note | `[Source: [A.cs:1](../a.cs); [[note]]]` | `[A.cs:1](../a.cs); [[note]]` | N/A |
| Plain path citation | `[Source: _bmad-output/x.md — note]` | `_bmad-output/x.md — note` | N/A |
| Code-span citation | `` [Source: `src/X.cs:15`] `` | `` `src/X.cs:15` `` | N/A |
| Two citations, one line | `a [Source: x.md] b [Source: y.md]` | `a x.md b y.md` (no over-match across them) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/EpicsParser.cs` -- line 163 defines `SourceCitationBrackets`; used at line 137 (story remainder), 231 (AC bodies), 273 (named sections). The only file to change.
- `tests/SpecScribe.Tests/EpicsParserTests.cs` -- home for the regression test; `SplitStoryArtifact` returns rendered remainder HTML, a clean seam.
- `src/SpecScribe/CodeReferenceLinkifier.cs` -- downstream page-level resolver (unchanged); the fix restores the anchors it needs.
- `src/SpecScribe/MarkdownConverter.cs` -- Markdig pipeline (unchanged); confirmed to render nested `[Source: [x](u)]` correctly on its own.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/EpicsParser.cs` -- Replace the `SourceCitationBrackets` pattern `\[Source:\s*(.*?)\]` with a balance-aware form that matches the outer close bracket over one level of inner brackets: `\[Source:\s*((?:[^\[\]]|\[[^\]]*\])*)\]`. Keep the `"$1"` replacement and all three call sites unchanged. Update the adjacent comment (lines 160-162) to note that inner markdown-link brackets are preserved. **Done** ([EpicsParser.cs:163](../../src/SpecScribe/EpicsParser.cs)).
- [x] `tests/SpecScribe.Tests/EpicsParserTests.cs` -- Add a regression test driving `SplitStoryArtifact` with a story whose remainder holds both a multi-link and a single-link `[Source: …]` citation; assert the remainder HTML contains the expected `<a href="…">…</a>` anchors, contains no raw `](` / `(../` link fragments as literal text, and no literal `[Source:`. **Done** — `SplitStoryArtifact_PreservesMarkdownLinkCitationsWhenStrippingSourceWrapper`.

**Acceptance Criteria:**
- Given a story whose body cites source files with markdown-link `[Source: …]` citations (single or multiple links), when the site is generated, then those citations render as clickable links to the code pages with no visible markdown/URL syntax.
- Given a story with plain-path or code-span `[Source: …]` citations, when the site is generated, then their rendering is byte-identical to before this fix (no regression).
- Given the full test suite, when it runs, then it passes (see Change Log: the sole failure — `GoldenContentFingerprint` — is pre-existing and unrelated to this fix).

## Spec Change Log

- **`GoldenContentFingerprint` failure is pre-existing, NOT caused by this fix — do not regenerate its constant here.** The golden fixture (`Story11Md`/`Story21Md`/`EpicsMd` in `SiteGeneratorAdapterTests`) contains no `[Source:` citations, so the `SourceCitationBrackets` change is a provable no-op on it, and my diff is only the regex + its comment. The failure traces to substantial pre-existing uncommitted work in the tree at session start — `src/SpecScribe/assets/specscribe.css` (+24 lines, embedded on every page), `Icons.cs`, and a +258-line `CodeFileTemplater.cs` refactor (the untracked `spec-code-page-relationships-history-tabs` work) — compounded by the per-build MVID `?v=` token that flaps across rebuilds. All 967 other tests pass, including the new regression test and 46/46 citation/linkifier tests. Regenerating the constant now would bake unrelated in-flight work into this fix's baseline.

## Verification

**Commands:**
- `dotnet build src/SpecScribe/SpecScribe.csproj -c Debug` -- expected: build succeeds.
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj -c Debug` -- expected: all tests pass, including the new regression test.

**Manual checks:**
- Regenerate the site (`dotnet run --project src/SpecScribe -- generate --deep-git --output <tmp>`) and inspect `epics/story-7-4.html`: the `GitMetrics.cs:213` / `GitMetrics.cs:270-286` citation renders as two links with no literal `(../../src/SpecScribe/GitMetrics.cs)` text.
