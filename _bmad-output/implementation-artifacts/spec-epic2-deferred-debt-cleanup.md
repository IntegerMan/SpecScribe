---
title: 'Epic 2 deferred — multi-match diagnostics, sprint parse edges, renderer setup'
type: 'bugfix'
created: '2026-07-18T16:28:41-04:00'
status: 'done'
baseline_commit: '636a30a'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Seven open Epic 2 deferred items still leave silent first-match picks (nav / sprint file), fragile hand-rolled YAML edges, and per-fragment Markdig renderer swaps. Two of the seven are already fixed or half-obsolete after later refactors but remain open on the ledger.

**Approach:** Close all seven under this spec: multi-kernel Spec quick-links; duplicate well-known-filename + multi-sprint Skipped diagnostics (reuse unused `AdapterDiagnosticCategory.Skipped`); IO-safe sprint discovery; fail-closed sprint parser edges; one-time Markdig renderer wrapping via pipeline extensions; ledger-resolve the already-fixed contrast item and the deleted IndexCardTitle/FindByFileName halves with accurate notes.

## Boundaries & Constraints

**Always:**
- Multi-kernel: SiteNav emits one Spec quick-link per `specs/**/SPEC.md`. Single kernel keeps label `Spec`; two+ use `Spec — {parent folder}` (folder of the SPEC.md). Index-card half is already gone (home declutter) — mark that half RESOLVED as obsolete, nav half as fixed here.
- Duplicate well-known module filenames: SiteNav keeps alphabetical-first match for the nav/quick-link entry and emits one `AdapterDiagnosticCategory.Skipped` naming the chosen path and how many duplicates were skipped. Plumb an optional diagnostics sink into `SiteNav.Build`; SiteGenerator merges via existing `MapDiagnostics`. Original `FindByFileName` is deleted — relocate the deferred bullet to this SiteNav behavior when resolving.
- Sprint discovery (`BmadArtifactAdapter.IngestSprint`): wrap EnumerateFiles in the repo IO idiom (`IOException`/`UnauthorizedAccessException` → empty); keep OrdinalIgnoreCase alphabetical-first; when count > 1 emit `Skipped` (chosen path + count). Do not abort generation on inaccessible subdirs.
- Contrast (`.md-comment` / `--ink-light`): already deepened to `#6b5442` and pinned by `StylesheetTests.MutedInk_ClearsWcagAA_OnBothParchmentSurfaces` — mark RESOLVED with no CSS change. Do not retune `--status-deferred`.
- Renderer swaps: register mermaid + comment wrappers once via Markdig `IMarkdownExtension` on the static `Pipeline` so `Pipeline.Setup(renderer)` installs them; remove per-call `UseMermaidCodeBlocks` / `UseCommentAnnotations` from `RenderDocumentHtml`. Correctness of mermaid fences and `<!-- -->` annotations must stay identical.
- Duplicate top-level YAML key in `ExtractTopLevelBlock`: if the same `key:` header appears again after the block started, return `null` (fail closed) instead of truncating. Block-scalar `last_updated` (`>`, `|`, optional `+`/`-` chomp): treat as null (no date), never surface the indicator as the value.
- Mark all seven deferred bullets RESOLVED citing this spec key.

**Ask First:**
- Changing first-wins selection to something other than alphabetical OrdinalIgnoreCase.
- Parsing YAML block-scalar folded body for `last_updated` (null-degrade is enough unless you hit a real authoring need).
- Sharing a single `HtmlRenderer` instance across calls (extensions-on-Pipeline only unless proven reusable).

**Never:**
- Full multi-framework doc promotion / Epic 4 adapter rewrite.
- Throwing fatally from ingest/parse on these edges.
- Broader muted-token / brand color redesign beyond ledger-closing the already-fixed `--ink-light`.
- Leaving stale deferred bullets open when the code path is already gone or fixed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| One SPEC.md | Single kernel path | One quick-link labeled `Spec` | N/A |
| Two SPEC.md | Two `specs/*/SPEC.md` | Two links `Spec — {folder}` each | N/A |
| Dup prd.md | Two `prd.md` under source | First (alpha) linked; one Skipped diagnostic | N/A |
| Multi sprint yaml | Two `sprint-status.yaml` | Alpha-first parsed; one Skipped diagnostic | N/A |
| Inaccessible subdir during sprint enum | EnumerateFiles throws IO/Unauthorized | Treat as no candidates (or skip that tree); generation continues | catch → empty |
| Dup `development_status:` | Malformed yaml with two headers | Block extract null → sprint omit/unsupported path as today | fail closed |
| `last_updated: >` | Block-scalar indicator only | `LastUpdated` null | N/A |
| Fragment comment/mermaid | RenderInline/RenderBlock with `<!-- x -->` / \`\`\`mermaid | Same HTML as full Convert | N/A |
| Contrast regression | `--ink-light` vs parchment tokens | Existing WCAG AA stylesheet test still passes | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteNav.cs` -- Spec kernel FirstOrDefault (~164); module-doc FirstOrDefault (~98); optional diagnostics sink
- `src/SpecScribe/SiteGenerator.cs` -- SiteNav.Build call sites (~176, ~187, ~3424); MapDiagnostics merge
- `src/SpecScribe/BmadArtifactAdapter.cs` -- IngestSprint EnumerateFiles (~195–217)
- `src/SpecScribe/AdapterDiagnostic.cs` -- unused `Skipped` category
- `src/SpecScribe/SprintStatusParser.cs` -- ExtractTopLevelBlock; ExtractLastUpdated / LastUpdatedLine
- `src/SpecScribe/MarkdownConverter.cs` -- Pipeline; RenderDocumentHtml; UseMermaid*/UseComment*
- `src/SpecScribe/assets/specscribe.css` -- `--ink-light` already fixed (ledger only)
- `tests/SpecScribe.Tests/SiteNavTests.cs` -- single-kernel only today
- `tests/SpecScribe.Tests/BmadArtifactAdapterTests.cs` -- sprint Unsupported path; add multi/IO
- `tests/SpecScribe.Tests/SprintStatusParserTests.cs` -- no dup-key / block-scalar cases
- `tests/SpecScribe.Tests/MarkdownConverterTests.cs` -- mermaid/comment fidelity
- `tests/SpecScribe.Tests/StylesheetTests.cs` -- MutedInk WCAG pin (no change expected)
- `_bmad-output/implementation-artifacts/deferred-work.md` -- story-2-2 / 2-3 / 2-4 / 2-6 open bullets (~320–345)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SiteNav.cs` + `SiteGenerator.cs` + `SiteNavTests.cs` -- Multi-kernel Spec links; duplicate module-doc Skipped via optional diagnostics sink; pin tests
- [x] `src/SpecScribe/BmadArtifactAdapter.cs` + `BmadArtifactAdapterTests.cs` -- IO-safe sprint enum; multi-file Skipped; pin tests
- [x] `src/SpecScribe/SprintStatusParser.cs` + `SprintStatusParserTests.cs` -- Dup top-level key → null; block-scalar last_updated → null; pin tests
- [x] `src/SpecScribe/MarkdownConverter.cs` + `MarkdownConverterTests.cs` -- Pipeline extensions replace per-call swaps; assert fragment fidelity unchanged
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- RESOLVED all seven bullets (note obsolete IndexCardTitle/FindByFileName + already-fixed contrast)

**Acceptance Criteria:**
- Given the seven listed Epic 2 deferred bullets, when this work ships, then each is marked RESOLVED under its existing review section with this spec key.
- Given two SPEC.md kernels, when SiteNav builds, then both appear as distinguishable Spec quick-links.
- Given duplicate well-known filenames or multiple sprint-status.yaml files, when ingest/nav runs, then alphabetical-first wins and exactly one Skipped diagnostic is emitted for that choice.
- Given inaccessible directories during sprint discovery or malformed duplicate/block-scalar sprint YAML edges, when parsed, then generation does not throw and values degrade null/omit per matrix.
- Given RenderInline/RenderBlock and full Convert, when mermaid or HTML comments are present, then annotation/fence HTML matches prior behavior with no per-call ObjectRenderers swap helpers left on the hot path.

## Spec Change Log

## Design Notes

`AdapterDiagnosticCategory.Skipped` was reserved for "must choose between candidates" and is unused — prefer it over Unsupported for multi-match. Markdig `IMarkdownExtension.Setup(pipeline, renderer)` runs inside `Pipeline.Setup(renderer)`, so registering wrappers there is the idiomatic once-per-pipeline fix without sharing HtmlRenderer instances.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~SiteNav|FullyQualifiedName~BmadArtifactAdapter|FullyQualifiedName~SprintStatusParser|FullyQualifiedName~MarkdownConverter|FullyQualifiedName~StylesheetTests.MutedInk"` -- expected: all pass
- `dotnet test` -- expected: full suite green

## Suggested Review Order

**Multi-match nav + diagnostics**

- Entry: optional diagnostics sink + duplicate module-doc Skipped notice
  [`SiteNav.cs:87`](../../src/SpecScribe/SiteNav.cs#L87)

- Multi-kernel Spec links with collision-aware folder suffixes
  [`SiteNav.cs:192`](../../src/SpecScribe/SiteNav.cs#L192)

- Merge nav diagnostics into the generation event stream
  [`SiteGenerator.cs:176`](../../src/SpecScribe/SiteGenerator.cs#L176)

- Display labels like `Spec — alpha` still resolve the Spec glyph
  [`Icons.cs:46`](../../src/SpecScribe/Icons.cs#L46)

**Sprint discovery + parse edges**

- IO-safe enum with IgnoreInaccessible + multi-file Skipped
  [`BmadArtifactAdapter.cs:236`](../../src/SpecScribe/BmadArtifactAdapter.cs#L236)

- Duplicate top-level YAML key fails closed
  [`SprintStatusParser.cs:190`](../../src/SpecScribe/SprintStatusParser.cs#L190)

- Block-scalar / comment `last_updated` degrades to null
  [`SprintStatusParser.cs:169`](../../src/SpecScribe/SprintStatusParser.cs#L169)

**Renderer setup**

- Mermaid + comment wrappers registered once via pipeline extension
  [`MarkdownConverter.cs:17`](../../src/SpecScribe/MarkdownConverter.cs#L17)

**Ledger + tests**

- Seven Epic 2 deferred bullets marked RESOLVED
  [`deferred-work.md:320`](./deferred-work.md#L320)

- Multi-kernel, collision, and Skipped coverage
  [`SiteNavTests.cs:161`](../../tests/SpecScribe.Tests/SiteNavTests.cs#L161)
