---
title: 'Harden Story 9.6 follow-up provenance heuristics'
type: 'bugfix'
created: '2026-07-18T02:11:40-04:00'
status: 'done'
baseline_commit: 'd3ad128da3a88f24fdd24e8736c0f6b71f2782ac'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five deferred Story 9.6 review findings leave follow-up surfaces under-linked or mislabeled: narrow list markers, path-prefixed `source_spec` tokens failing `StoryIdFromKey`, narrow RESOLVED→story extraction, first-match-only near-dupe epics, and a resolving-chip heuristic that can prefix `Story` onto filenames like `readme.md`.

**Approach:** Best-effort widen those five heuristics in the existing parser/ref/templater path — still derive from authored prose, prefer false negatives, and keep NFR8 degrade-to-absent. Close the matching deferred-work bullets when done.

## Boundaries & Constraints

**Always:**
- No new authoring schema / frontmatter / YAML fields — prose heuristics only.
- Prefer false negatives over wrong resolving links or false near-dupes.
- Pure, deterministic parsers/templater helpers (no I/O, stable ordering).
- Near-dupe epic lists sorted ascending and de-duped; render one “also raised…” note per counterpart epic.
- `Story {id}` chip label only when the ref is a real dotted story id (`N.M`); otherwise show the raw ref.
- Resolve the five Story 9.6 deferred bullets in `deferred-work.md` without rewriting unrelated entries.

**Ask First:**
- Changing Jaccard / shared-token near-dupe thresholds.
- Treating bare unbracketed `RESOLVED` prose as resolved status (today only `<del>` or bracketed `[RESOLVED`).

**Never:**
- Merging near-duplicate action items into one card.
- Whole-page linkification of action-item copy payloads.
- Broadening list detection to indented/nested markers (column-0 only).
- Touching watch/regenerate writers, slug algorithms, or sunburst geometry beyond what these heuristics feed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Foreign list markers | Deferred section with `*`, `+`, or `1. `/`1) ` items at col 0 | Each becomes a `DeferredWorkItem` (same as `- `) | Indented markers stay continuation; no items → unstructured |
| Path-prefixed source_spec | `source_spec: \`_bmad-output/.../8-8-slug.md\``; map miss | `StoryIdFromKey` → `8.8`; placeholder story href when map empty | Non-story tokens → null id |
| Alt closure phrasing | Resolved (`<del>` or `[RESOLVED`) + `RESOLVED in Story 6.4` / trailing punctuation / `` (`spec-…`) `` | `ResolvingRef`/`Href` populated when id or map/spec resolvable | Prefer miss over wrong story |
| Triple-epic near-dupe | Same obligation in epics 1, 2, and 3 | Each item lists both other epics (sorted) | No match → no cross chrome |
| Hyphen-free filename chip | `ResolvingRef = readme.md` | Label `readme.md` (no `Story ` prefix) | `6.4` → `Story 6.4` |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/DeferredWorkParser.cs` -- `ParseItems` col-0 `- `/`-\t` only; Flush strip; resolved + resolving promotion
- `src/SpecScribe/FollowUpRefs.cs` -- `StoryIdFromKey` (no path strip); `ResolvingStoryIdFromText` / `ResolvedInStory`; `ResolveHref` stem fallback; `SourceSpecFileFromText`
- `src/SpecScribe/ActionItemsTemplater.cs` -- `FindNearDuplicates` `TryAdd` first-wins → `int`; list render singular cross-link
- `src/SpecScribe/FollowUpDetailTemplater.cs` -- action detail singular cross-link; deferred resolving chip heuristic
- `src/SpecScribe/DeferredWorkTemplater.cs` -- resolving chip `Contains('.') && !Contains('-')`
- `src/SpecScribe/SiteGenerator.cs` -- `WriteFollowUpDetails` consumes near-dupe map
- `tests/SpecScribe.Tests/DeferredWorkParserTests.cs` -- parse/templater coverage for deferred
- `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` -- near-dupe + surface integration
- `_bmad-output/implementation-artifacts/deferred-work.md` -- five open 9.6 bullets to resolve

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/FollowUpRefs.cs` -- Strip path via filename/stem in `StoryIdFromKey`; widen `ResolvingStoryIdFromText` for RESOLVED-adjacent `N.M` and backtick story/spec tokens; add shared resolving-label helper (`Story N.M` only for dotted ids) -- provenance/resolution accuracy
- [x] `src/SpecScribe/DeferredWorkParser.cs` -- Recognize col-0 `*`/`+`/numbered list markers; strip matched marker in Flush -- foreign-framework list parity
- [x] `src/SpecScribe/ActionItemsTemplater.cs` + `FollowUpDetailTemplater.cs` (+ `SiteGenerator` map type) -- Accumulate sorted distinct counterpart epics; emit one cross-link/note each -- multi-epic provenance
- [x] `src/SpecScribe/DeferredWorkTemplater.cs` + deferred branch of `FollowUpDetailTemplater.cs` -- Use shared resolving-label helper -- stop false `Story readme.md`
- [x] `tests/SpecScribe.Tests/DeferredWorkParserTests.cs` + `FollowUpSurfacesTests.cs` -- Cover I/O matrix rows (markers, path prefix, alt closure, 3-epic near-dupe, chip labels)
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Mark the five 9.6 bullets resolved with this spec

**Acceptance Criteria:**
- Given a Deferred-from section using `*`, `+`, or numbered col-0 lists, when parsed, then items appear as structured cards (not unstructured fallback solely for marker shape).
- Given a path-prefixed story `source_spec` and a missing href map entry, when parsed, then `SourceStoryId` / resolving placeholder still derive `N.M` when the filename is a story key.
- Given a resolved item whose closure names Story `N.M` or a resolvable `` `spec-…` `` without the exact `RESOLVED in [Story] N.M` template, when rendered, then a resolving link/chip appears when resolution is unambiguous.
- Given near-duplicate open actions across three epics, when action-items list and detail render, then each shows the other counterpart epics (not only the first match).
- Given `ResolvingRef` values `6.4` and `readme.md`, when chips render, then labels are `Story 6.4` and `readme.md` respectively.

## Design Notes

**Closure widening (prefer miss):** Keep bracketed/`<del>` status rules unchanged. Only enrich id extraction when already resolved (or from RESOLVED-adjacent text the extractor already scopes). Accept: `RESOLVED in Story N.M`, `RESOLVED in N.M` with trailing punctuation/em-dash, and a backtick story-key or `spec-*` immediately after a RESOLVED marker. Do not invent “fixed in …” as status; optional id scrape from those phrases is OK only if still clearly closure-adjacent and deterministic.

**Near-dupe map shape:** `IReadOnlyDictionary<SprintActionItem, IReadOnlyList<int>>` (reference equality keys unchanged). Values: distinct epic numbers ascending. Thresholds untouched.

**List markers:** Column 0 only: unordered `[-*+][ \t]` and ordered `\d+[.)][ \t]`. Nested/indented lines remain continuations.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~DeferredWorkParser|FullyQualifiedName~FollowUpSurfaces"` -- expected: pass
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green

## Suggested Review Order

**Resolving extraction (gate + widen)**

- Gate resolving links on resolved status — prefer false negatives on open prose
  [`DeferredWorkParser.cs:144`](../../../src/SpecScribe/DeferredWorkParser.cs#L144)

- Path-strip in StoryIdFromKey; backtick RESOLVED tokens; ResolvingLabel for N.M only
  [`FollowUpRefs.cs:101`](../../../src/SpecScribe/FollowUpRefs.cs#L101)

**List markers**

- Column-0 `*`/`+`/numbered markers; skip blank remainder after strip
  [`DeferredWorkParser.cs:171`](../../../src/SpecScribe/DeferredWorkParser.cs#L171)

**Multi-epic near-dupes**

- Accumulate sorted distinct counterpart epics (not first-match-only)
  [`ActionItemsTemplater.cs:99`](../../../src/SpecScribe/ActionItemsTemplater.cs#L99)

- Shared AppendCrossLinks used by list and detail
  [`ActionItemsTemplater.cs:200`](../../../src/SpecScribe/ActionItemsTemplater.cs#L200)

**Chip labels**

- Deferred list + detail chips call ResolvingLabel
  [`DeferredWorkTemplater.cs:132`](../../../src/SpecScribe/DeferredWorkTemplater.cs#L132)

**Tests & ledger**

- I/O matrix coverage including open-item RESOLVED+backtick negative
  [`DeferredWorkParserTests.cs`](../../../tests/SpecScribe.Tests/DeferredWorkParserTests.cs)

- Three-epic near-dupe sorted counterparts
  [`FollowUpSurfacesTests.cs`](../../../tests/SpecScribe.Tests/FollowUpSurfacesTests.cs)
