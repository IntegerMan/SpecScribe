---
title: 'Story 10.4 deferred-debt cleanup'
type: 'chore'
created: '2026-07-20T11:00:00-04:00'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '97346d3'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 10.4's code review left four open items in `deferred-work.md`: (1) linked Code Map treemap cells wrap a real `<a>` around a still-`tabindex="0"` `<rect>`, nesting focusables for keyboard/AT; (2) `CollapseSummary` truncates ADR one-line summaries by UTF-16 code units and can split ZWJ/combining grapheme clusters; (3) `FileInsight.TotalContributors = 0` fails open so an overlooked construction path hides truncation disclosure; (4) `TryGetDefaultBranch` takes the segment after the last `/` of `origin/HEAD`, collapsing slashy branch names (e.g. `feature/foo` → `foo`).

**Approach:** Align linked treemap cells with the Tile/heatmap link-owns-focus pattern (no tabindex on the geometry child; tip/name on the `<a>`); truncate ADR summaries on grapheme boundaries; make `TotalContributors` required like `FileChangeStat`; resolve default branch by stripping the `refs/remotes/origin/` prefix. Mark all four items RESOLVED in `deferred-work.md`.

## Boundaries & Constraints

**Always:**
- Linked treemap file cells: omit `tabindex` on the inner `<rect>`; put `js-tip`, `aria-label`, and `data-tip-html` on the wrapping `<a>` so keyboard tip reach and AT naming stay intact (hover still finds `.js-tip` via `closest` from the rect). Keep metric `data-*` / `.codemap-cell` / fill classes on the rect for the colorize JS. Unlinked cells stay as today (`tabindex="0"` + `role="img"` + tip attrs on the rect).
- `CollapseSummary` only: when collapsing past 160 UTF-16 chars, cut on a grapheme-cluster boundary (e.g. `StringInfo.GetTextElementEnumerator`) so ZWJ/combining sequences are kept whole or omitted whole; keep the existing surrogate-pair safety subsumed by grapheme walk. Do not change other truncators in this pass.
- `FileInsight.TotalContributors` becomes a required positional parameter (drop `= 0`), matching `FileChangeStat`. Update every `new FileInsight(...)` call site (prod + tests) to pass an explicit count.
- `TryGetDefaultBranch`: if the trimmed symref starts with `refs/remotes/origin/`, return the remainder (preserving internal slashes); otherwise return null. Do not use `LastIndexOf('/')`.
- Strike through all four story-10-4 deferred entries with resolution notes pointing at this spec.

**Ask First:** none — decisions above close the deferred items as written.

**Never:**
- Re-introduce nested `role="link"` on the linked rect.
- Change unlinked treemap focus/role behavior or the colorize `data-*` contract on `.codemap-cell`.
- Broaden grapheme truncation to `FollowUpRow` / Charts / nav-label truncators.
- Hardcode a default branch name or invent remote names other than `origin`.
- Touch unrelated 10.4 date/sequencing code or other deferred sections.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Linked treemap cell | `fileHref` returns a non-empty path | `<a href=… class="… js-tip" aria-label=… data-tip-html=…>` wraps a `<rect class="codemap-cell …">` with no `tabindex` / no `role` / no tip attrs | N/A |
| Unlinked treemap cell | `fileHref` returns null | Focusable `role="img"` rect with `tabindex="0"` + tip attrs (unchanged) | N/A |
| ADR summary at grapheme cut | Collapsed text >160 chars; a ZWJ/combining cluster straddles the cut | Ellipsis after a whole grapheme; no half-cluster before `…` | N/A |
| Short ADR summary | Collapsed text ≤160 chars | Returned unchanged (no ellipsis) | N/A |
| Truncated contributors | `Contributors.Count == 3`, `TotalContributors == 12` | Insights panel shows `+9 more` disclosure | N/A |
| Complete contributors | Counts equal | No `+N more` note | N/A |
| Overlooked FileInsight ctor | Omitting `TotalContributors` | Compile error (required param) | Fail closed at build |
| Slashy default branch | symref `refs/remotes/origin/feature/foo` | `"feature/foo"` | N/A |
| Simple default branch | symref `refs/remotes/origin/main` | `"main"` | N/A |
| Missing/odd symref | empty, or not under `refs/remotes/origin/` | `null` | Soft fail → no guessed branch |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/Charts.cs` — `AppendTreemapFile` linked vs unlinked markup (~2252–2269)
- `src/SpecScribe/assets/specscribe.js` — tip `focusin` / hover `.js-tip` + SEG path (verify no JS change needed once tip attrs move to `<a>`)
- `src/SpecScribe/SiteGenerator.cs` — `CollapseSummary` / `ExtractAdrSummary`
- `src/SpecScribe/GitMetrics.cs` — `FileInsight` record; `TryGetDefaultBranch`; prod `BuildFileInsights` assignment
- `src/SpecScribe/CodeFileTemplater.cs` — truncation disclosure when `TotalContributors > Contributors.Count`
- `src/SpecScribe/CodeSourceUrlResolver.cs` — consumer of `TryGetDefaultBranch`
- `tests/SpecScribe.Tests/ChartsTests.cs` — `CodeTreemap_LinksFileOnlyWhenResolverReturnsATarget` (today asserts linked `tabindex="0"` — flip)
- `tests/SpecScribe.Tests/SiteGeneratorAdrToleranceTests.cs` — add grapheme truncation pin
- `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs` — `SampleInsight` + fixtures must pass required `TotalContributors`
- `tests/SpecScribe.Tests/GitMetricsFileInsightsTests.cs` — existing TotalContributors pin
- `tests/SpecScribe.Tests/GitMetricsTests.cs` or new focused cases — origin/HEAD prefix parse (extract a tiny pure helper if `RunGit` can't be mocked)
- `_bmad-output/implementation-artifacts/deferred-work.md` — story-10-4 section (four bullets)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/Charts.cs` -- linked cells: tip/name on `<a>`, no `tabindex` on rect; unlinked unchanged -- closes nested-focusable a11y debt
- [x] `tests/SpecScribe.Tests/ChartsTests.cs` -- assert linked rect has no `tabindex`; tip/`aria-label` live on the wrapping `<a>` -- locks the fix (replaces the buggy assertion)
- [x] `src/SpecScribe/SiteGenerator.cs` -- grapheme-aware cut in `CollapseSummary` -- closes ZWJ/combining split
- [x] `tests/SpecScribe.Tests/SiteGeneratorAdrToleranceTests.cs` -- pin a straddling grapheme cluster at the 160-char ellipsis cut -- coverage for the Unicode edge
- [x] `src/SpecScribe/GitMetrics.cs` -- required `FileInsight.TotalContributors`; fix `TryGetDefaultBranch` prefix strip (+ optional pure parse helper for tests) -- fail closed + slashy branches
- [x] `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs` (and any other `new FileInsight` sites) -- pass explicit `TotalContributors` -- compile + honest fixtures
- [x] `tests/SpecScribe.Tests/GitMetricsTests.cs` (or adjacent) -- `refs/remotes/origin/feature/foo` → `feature/foo`; `main`; non-origin → null -- locks branch parse
- [x] `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- re-lock golden content fingerprint for linked-treemap markup delta -- keep byte-parity gate green
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- mark the four 10.4 entries RESOLVED with this spec -- ledger hygiene
- [x] `tests/SpecScribe.Tests/SiteGeneratorCodeMapTests.cs` -- assert `href=` (not bare `<a href=…>`) so tip attrs on the wrapping `<a>` don't break the external-source link pin

**Acceptance Criteria:**
- Given a Code Map file with a resolvable `fileHref`, when the treemap renders, then the cell is one focusable `<a>` (no nested `tabindex` on the rect) and keyboard focus still surfaces the rich tip.
- Given an ADR Context paragraph whose collapsed form exceeds 160 chars with a multi-code-unit grapheme on the cut, when summarized, then the ellipsis does not split that grapheme.
- Given any `new FileInsight(...)`, when building without `TotalContributors`, then the project does not compile; when truncated lists are rendered with an explicit total > list count, then `+N more` appears.
- Given `origin/HEAD` → `refs/remotes/origin/feature/foo`, when resolving the default branch, then the result is `feature/foo` (not `foo`).
- Given the four deferred-work bullets under the 10.4 review section, when this ships, then each is struck through as RESOLVED pointing at this spec.

## Spec Change Log

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~ChartsTests|FullyQualifiedName~SiteGeneratorAdrToleranceTests|FullyQualifiedName~CodeFileTemplaterTests|FullyQualifiedName~GitMetrics"` -- expected: all matching tests green
- `dotnet test` -- expected: full suite green

## Suggested Review Order

**Linked treemap focus + tip**

- Entry: linked cells put tip/name on `<a>`; rect drops nested `tabindex`
  [`Charts.cs:2261`](../../src/SpecScribe/Charts.cs#L2261)

- Colorize JS writes `aria-label` on the link host when present
  [`specscribe.js:803`](../../src/SpecScribe/assets/specscribe.js#L803)

- Keyboard focus ring follows the `<a>` onto the child rect
  [`specscribe.css:3603`](../../src/SpecScribe/assets/specscribe.css#L3603)

**ADR grapheme truncation**

- Ellipsis cut walks grapheme clusters, not raw UTF-16 indices
  [`SiteGenerator.cs:4108`](../../src/SpecScribe/SiteGenerator.cs#L4108)

**Fail-closed contributors + slashy default branch**

- `TotalContributors` required (no `= 0` fail-open default)
  [`GitMetrics.cs:160`](../../src/SpecScribe/GitMetrics.cs#L160)

- Strip `refs/remotes/origin/` prefix; keep slashy branch names
  [`GitMetrics.cs:1029`](../../src/SpecScribe/GitMetrics.cs#L1029)

**Tests + ledger**

- Linked-cell a11y assertions (no nested tabindex; tip on `<a>`)
  [`ChartsTests.cs:1806`](../../tests/SpecScribe.Tests/ChartsTests.cs#L1806)

- ZWJ family emoji straddling the 160-char cut
  [`SiteGeneratorAdrToleranceTests.cs:234`](../../tests/SpecScribe.Tests/SiteGeneratorAdrToleranceTests.cs#L234)

- Origin/HEAD prefix parse pins (slashy + whitespace-only → null)
  [`GitMetricsTests.cs:993`](../../tests/SpecScribe.Tests/GitMetricsTests.cs#L993)

- Four 10.4 deferrals struck RESOLVED
  [`deferred-work.md:14`](./deferred-work.md#L14)
