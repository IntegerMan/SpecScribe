---
title: 'Story 6.7 deferred-work cleanup'
type: 'bugfix'
created: '2026-07-21T00:00:00-04:00'
status: 'done'
review_loop_iteration: 0
context: []
route: 'one-shot'
---

# Story 6.7 deferred-work cleanup

## Intent

**Problem:** Story 6.7 (JSON+SPA delivery adapter) and its follow-on at-scale perf pass left 5 substantive items open in `deferred-work.md`, all tagged `6-7-json-and-spa-delivery-adapter`: (1) watch-mode `_spaCapture` could drift from the real static file set on doc rename/delete; (2) `MaxPagesPerChunk` (75) batch-splitting had zero boundary test coverage; (3) chunk-batch assignment was claimed to depend on unstated enumeration order; (4) the SPA chunker capped chunks by page COUNT only, so one mega-page (e.g. an 82.5 MB `code-map.html` at large-repo scale) could drag its whole group into a pathological 112.9 MB chunk; (5) `code-map.html` itself balloons to ~82.5 MB at large-repo/`--deep-git` scale. (Two other list entries were bare `source_spec:` section-metadata lines, not defects — confirmed non-actionable and annotated as such.)

**Approach:** Investigated each item against current code before touching anything. Item 1 traced real: `RegenerateAdrs` wipes+rebuilds the physical `adrs/` directory each pass but never pruned the separate in-memory `_spaCapture` map — fixed by tracking every ADR-family path actually written each pass and pruning anything else under the ADR prefix at the end of the method. Item 3 traced to already being false — `SpaDelivery.BuildDataFiles` already sorts pages by `OutputRelativePath` (Ordinal) before assigning batch numbers, so it's order-independent by construction; closed as misdiagnosed, pinned by a new order-independence test. Item 2 was pure test-coverage addition (boundary cases at 74/75/76/150/151 pages). Items 4 and 5 were real feature work per the deferred notes' own proposed fix directions: a byte-budget (2 MB) added alongside the page-count cap in the SPA chunker, isolating any oversized page into its own dedicated chunk; and a file-count cap (4000) on the code-map treemap's expensive per-file rich tooltip card + file-table row, keeping every file's geometry/color/accessible-name intact (no AC #4 loss) while dropping only the convenience hover popup for the long tail. Both new caps are no-ops at this repo's normal scale (byte-identical default generation).

**Adversarial review (Blind Hunter) and follow-up hardening.** A scoped review pass (correctly excluding a concurrent, unrelated in-progress session's changes mixed into the same working tree) surfaced 10 real findings, all patched: (a) the ADR prune's live-path set was built from `_adrs` alone, which only ever holds *record* entries — a template scaffold file or a nested (non-root) README render real pages via a different branch and were being evicted the SAME pass they were rewritten; fixed by tracking every write site, not just records. (b) the landing page (`adrs/index.html`) was unconditionally protected from pruning even when nothing writes it this pass (the last ADR removed, no synthesized fallback) — fixed by only protecting paths actually written. (c) the "aria-label already carries everything" claim for capped code-map files was false for metric-bearing files (churn/avg/co-change/dates lived only in the dropped card) — fixed by folding the same metrics into the aria-label as compact text, so AC #4 holds in text form too. (d) the treemap's and the file table's significance-ordering formulas were hand-duplicated — unified into one shared `Charts.OrderBySignificance`. (e) the treemap's and table's cap-trigger could disagree on repos deep enough that `CodeMap.Layout()`'s recursion cap omits a nested file — fixed by threading the true file count explicitly from the one shared source. (f)-(j): culture-invariant number formatting, an honest doc-comment caveat on the byte budget being an approximation (raw HTML bytes, not JSON-escaped size), and three added test cases (first-in-group / last-in-group oversized-page isolation, an actual rename via `File.Move` rather than only delete).

## Code Map

- `src/SpecScribe/SiteGenerator.cs` — `GenerateAdrsInternal`: tracks every ADR-family output path actually written this pass (`writtenAdrPaths`), prunes stale `_spaCapture` entries against that set at the end of the method (not against `_adrs` alone).
- `src/SpecScribe/SpaDelivery.cs` — `MaxChunkBytes` (2 MB) + `GroupBatchState`; `BuildDataFiles` starts a new batch when either the page-count or byte-budget cap would be exceeded, isolating an oversized page alone.
- `src/SpecScribe/Charts.cs` — `MaxDetailedCodeMapFiles` (4000), shared `OrderBySignificance`, `SelectDetailedCodeMapFiles`, `CompactMetricsTail`; `CodeTreemap`/`AppendTreemapFile` drop the rich `data-tip-html` card past the cap but fold its metrics into `aria-label` as text.
- `src/SpecScribe/CodeMapTemplater.cs` — `AppendFileTable` caps rows at the same set/order as the treemap, with an honest "+N more" row; `AppendVariantPanel` threads the true file count into `CodeTreemap`.
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — 4 new ADR-prune tests (delete, rename, non-record survival, landing-page pruning).
- `tests/SpecScribe.Tests/SpaDeliveryTests.cs` — boundary, order-independence, and oversized-page isolation tests (mid/first/last position).
- `tests/SpecScribe.Tests/ChartsTests.cs` / `CodeMapTemplaterTests.cs` — below/above-cap treemap and file-table tests.
- `_bmad-output/implementation-artifacts/deferred-work.md` — all 5 story-6-7 items resolved/closed with evidence; the 2 bare `source_spec:` lines annotated as non-actionable section metadata.

## Suggested Review Order

**ADR `_spaCapture` staleness fix**
- The core fix: track every ADR-family path actually written this pass, prune anything else at the end.
  [`SiteGenerator.cs:911`](../../src/SpecScribe/SiteGenerator.cs#L911)
- Tracking buffer introduced up front, populated at each of the 3 ADR write sites (non-record, record, synthesized landing).
  [`SiteGenerator.cs:676`](../../src/SpecScribe/SiteGenerator.cs#L676)
- Regression tests: delete, an actual rename, non-record (template/nested-README) survival, and landing-page pruning when the last ADR is removed.
  [`SiteGeneratorSpaTests.cs:311`](../../tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs#L311)

**SPA chunker byte-budget**
- `MaxChunkBytes` + the batching loop that closes a batch on either cap, isolating an oversized page alone.
  [`SpaDelivery.cs:56`](../../src/SpecScribe/SpaDelivery.cs#L56)
  [`SpaDelivery.cs:183`](../../src/SpecScribe/SpaDelivery.cs#L183)
- Isolation pinned at all three positions (mid/first/last-in-group).
  [`SpaDeliveryTests.cs:120`](../../tests/SpecScribe.Tests/SpaDeliveryTests.cs#L120)
- Boundary + order-independence tests (items 2 and 3).
  [`SpaDeliveryTests.cs:89`](../../tests/SpecScribe.Tests/SpaDeliveryTests.cs#L89)
  [`SpaDeliveryTests.cs:214`](../../tests/SpecScribe.Tests/SpaDeliveryTests.cs#L214)

**code-map.html size cap**
- The cap + shared significance order + compact aria-label fallback (the accessibility fix from review).
  [`Charts.cs:2209`](../../src/SpecScribe/Charts.cs#L2209)
  [`Charts.cs:2215`](../../src/SpecScribe/Charts.cs#L2215)
  [`Charts.cs:2393`](../../src/SpecScribe/Charts.cs#L2393)
- File table caps at the SAME shared order, with an honest "+N more" row.
  [`CodeMapTemplater.cs:264`](../../src/SpecScribe/CodeMapTemplater.cs#L264)

**Tracking**
- All 5 story-6-7 deferrals (plus 2 non-actionable metadata lines) closed in `deferred-work.md`.
  [`deferred-work.md:376`](./deferred-work.md#L376)

## Verification

- `dotnet build src/SpecScribe/SpecScribe.csproj` — clean throughout, including after the review-hardening pass.
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` — 1957/1960 passed. The 3 failures (`HtmlTemplaterTests` family-card/accent-class assertions tied to a new "Risk Quadrant" nav item, and the pinned golden-fingerprint constant) are pre-existing and attributable to a concurrent, unrelated in-progress session's uncommitted Story 7.10 work on the same shared `main` — confirmed via `git diff` (none of this change's files are load-bearing for any of the three) and none of my changed files appear in their failure traces.
- Confirmed each of the two behavior-changing fixes (ADR pruning, oversized-page isolation) actually catches its bug: reverted the ADR fix and re-ran its test (failed as expected) before restoring it; the isolation/boundary tests were written test-first against the byte-budget logic.
- Adversarial review (Blind Hunter) pass applied — see Intent § "Adversarial review and follow-up hardening"; all 10 findings patched, none deferred or rejected.
