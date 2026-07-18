---
title: 'Epic 9 deferred — stem boundaries, LI extract, row/test pins, epic ToDictionary, undrafted CTA'
type: 'bugfix'
created: '2026-07-18'
status: 'done'
baseline_commit: 'bd132f70893cfbe0005a2b730ed6476495a36ded'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Eight open Epic 9 deferred items remain: overlapping `ContainsSpecName` stems; nested/unclosed top-level `<li>` extraction that truncates or aborts; unstructured-with-`<li>` rows vs old fallback; missing empty-primary `FollowUpRow.Render` pin; duplicate epic `ToDictionary` aborting requirement pages; epic story cards putting undrafted guidance after AC while placeholders put it above; golden undrafted story lacking AC so reorder is invisible to byte-parity.

**Approach:** Harden stem cue matching and balanced LI extraction; close the unstructured-row note as intentional 9.11; add the empty-primary Render assert; first-wins epic maps in requirement rendering; align epic-card undrafted note above AC and give the adapter golden Story 1.2 a minimal AC block; reconcile all eight deferred bullets under this spec key.

## Boundaries & Constraints

**Always:**
- Spec-stem cues match as hyphen-aware tokens (`spec-a` ↛ `spec-ab`); bare stem and `stem.md`/`stem.html` still match; multi-epic cue ties still return null.
- `ExtractTopLevelListItems` balances nested `<li>` (full inner HTML kept); an unclosed top-level `<li>` skips or consumes that item only — later siblings still become slots. Structured Deferred-from path untouched.
- Item 3: ledger close only — keep Story 9.11 unstructured `.followup-row` + slugs when top-level `<li>` parse; prose-only still uses `deferred-work-fallback`.
- `FollowUpRow.Render` with null href + empty body omits primary link/disclosure; existing href/disclosure tests stay green.
- `RenderRequirement` / coverage helpers that currently `ToDictionary(e => e.Number)` use first-wins on duplicate numbers so page generation continues (source-order `First()`).
- Undrafted epic story cards: user-story → `not-detailed-note` → AC (match placeholder). Drafted `ViewPlanHref` placement unchanged except as needed to keep undrafted order correct.
- Golden `EpicsMd` Story 1.2 gains a minimal Given/When/Then AC so placeholder + epic-card AC branches appear in the fingerprint; re-baseline the adapter golden hash only for that intentional churn.
- Mark all eight deferred bullets resolved under their existing review sections with this spec key.

**Ask First:**
- Restoring pre-9.11 plain-body fallback for unstructured notes that contain `<li>`.
- Site-wide epic-number de-dupe at parse time (`EpicsParser` / all `ToDictionary(e => e.Number)` call sites).
- Soft-fail / diagnostic emission when duplicate epic numbers are collapsed.

**Never:**
- Change structured Deferred-from parsing or authoring schema.
- Revert unstructured list → detail-page / Unplanned membership behavior from Story 9.11.
- Absorb Epic 10 nav IA or Story 10.7 density work.
- Invent epic de-dupe UI or rewrite Markdig itself.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Overlapping stems | cue body names `spec-ab`; stem `spec-a` | no ContainsSpecName hit | N/A |
| Exact stem + ext | body `spec-a.md` / `spec-a.html`; stem `spec-a` | hit | N/A |
| Nested LI | top-level `<li>…<ul><li>inner</li></ul>…</li>` then sibling | first body includes nested list; sibling extracted | N/A |
| Unclosed LI | missing `</li>` then later top-level `<li>` | later sibling still a slot | skip/consume broken item only |
| Empty primary | `detailHref` null, empty body | no primary link/`details` | N/A |
| Dup epic numbers | two epics share Number in RenderRequirement | page renders; first epic wins coverage lookup | no throw |
| Undrafted card order | card has NoteHtml + AcBlocksHtml | `not-detailed-note` before AC markup | N/A |
| Golden AC | Story 1.2 with AC block | fingerprint includes AC panel/list on story + epic surfaces | re-baseline hash |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/UnplannedWorkGeometry.cs` — `ContainsSpecName` cue matching
- `src/SpecScribe/FollowUpGeometry.cs` — `ExtractTopLevelListItems`
- `src/SpecScribe/CollapsibleSections.cs` — `FindBalancedDetailsEnd` balance pattern to mirror
- `src/SpecScribe/DeferredWorkTemplater.cs` — unstructured row vs fallback (no behavior change; ledger close)
- `src/SpecScribe/FollowUpRow.cs` — empty-primary `Render` branch
- `src/SpecScribe/RequirementsTemplater.cs` — `ToDictionary(e => e.Number)` in requirement/coverage render
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` — `AppendStoryCard` undrafted note vs AC order; placeholder already correct
- `tests/SpecScribe.Tests/UnplannedWorkGeometryTests.cs` — stem overlap cue cases
- `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` / new geometry tests — nested + unclosed LI
- `tests/SpecScribe.Tests/FollowUpRowTests.cs` — empty-primary Render
- `tests/SpecScribe.Tests/RequirementsAndProgressTests.cs` — dup epic number no-throw
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs` — epic-card note-above-AC
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — Story 1.2 AC + golden fingerprint
- `_bmad-output/implementation-artifacts/deferred-work.md` — mark eight bullets resolved

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/UnplannedWorkGeometry.cs` -- hyphen-aware token match in `ContainsSpecName` -- stop overlapping-stem mis-hits
- [x] `tests/SpecScribe.Tests/UnplannedWorkGeometryTests.cs` -- assert `spec-a` ↛ `spec-ab`; exact + ext still hit -- pin cue I/O
- [x] `src/SpecScribe/FollowUpGeometry.cs` -- balance nested `<li>`; do not `yield break` the whole scan on unclosed -- fix truncate/abort
- [x] `tests/SpecScribe.Tests/*` -- nested LI keeps inner HTML + sibling; unclosed does not drop later siblings -- pin extractor I/O
- [x] `tests/SpecScribe.Tests/FollowUpRowTests.cs` -- null href + empty body omits primary -- close Render coverage gap
- [x] `src/SpecScribe/RequirementsTemplater.cs` -- first-wins epic-number dictionary at throw sites -- keep requirement pages generating
- [x] `tests/SpecScribe.Tests/RequirementsAndProgressTests.cs` -- duplicate epic Number still renders -- no ArgumentException
- [x] `src/SpecScribe/HtmlRenderAdapter.Epics.cs` -- move undrafted `not-detailed-note` above AC in `AppendStoryCard` -- match placeholder
- [x] `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs` -- undrafted card note index before AC -- pin sibling surfaces
- [x] `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- add Story 1.2 AC; update golden fingerprint -- expose reorder to byte-parity
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- resolve eight bullets with this spec key -- ledger truth
- [x] Item 3 -- ledger-only accepted close (no code revert of 9.11 unstructured rows) -- intentional overlay

**Acceptance Criteria:**
- Given cue text that only names `spec-ab`, when matching stem `spec-a`, then no attribution hit; exact stem / `.md` / `.html` still match.
- Given nested or unclosed top-level `<li>` HTML in unstructured deferred body, when extracting items, then nested content is preserved and later siblings become slots; structured Deferred-from unchanged.
- Given duplicate epic numbers, when rendering a requirement page, then generation succeeds with first-wins coverage lookup.
- Given an undrafted epic story card with note + AC, when rendering, then create-story guidance appears above AC (same order as placeholder).
- Given adapter golden Story 1.2 with AC, when `GenerateAll` fingerprint runs, then the AC reorder branch is present in the locked bytes (hash updated once).
- Given the eight deferred bullets, when this work lands, then each is marked resolved citing `spec-epic9-deferred-debt-cleanup`.

## Spec Change Log

## Design Notes

- Item 3 closes as accepted: Story 9.11 deliberately overlays per-item rows/slugs for unstructured lists; prose-only fallback remains. Revert only via Ask First.
- LI balance should mirror `CollapsibleSections.FindBalancedDetailsEnd` (open/close depth), not Markdig re-parse.
- Epic-number first-wins matches `SiteGenerator.SetRetros` GroupBy pattern; full parse-time de-dupe is out of scope.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~UnplannedWorkGeometry|FullyQualifiedName~FollowUp|FullyQualifiedName~RequirementsAndProgress|FullyQualifiedName~HtmlRenderAdapter|FullyQualifiedName~SiteGeneratorAdapter"` -- expected: all pass; golden fingerprint updated once if hash assertion fails after fixture AC add
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green

## Suggested Review Order

**Stem cue matching**

- Hyphen-aware token regex so `spec-a` never hits `spec-ab`
  [`UnplannedWorkGeometry.cs:340`](../../src/SpecScribe/UnplannedWorkGeometry.cs#L340)

**List-item extraction**

- Balanced nested `<li>` + skip-unclosed without aborting siblings
  [`FollowUpGeometry.cs:439`](../../src/SpecScribe/FollowUpGeometry.cs#L439)

- Real `<li>` opens only — `<link>`/`listing` must not inflate depth
  [`FollowUpGeometry.cs:503`](../../src/SpecScribe/FollowUpGeometry.cs#L503)

**Requirement pages**

- First-wins epic-number map so duplicate numbers never throw
  [`RequirementsTemplater.cs:626`](../../src/SpecScribe/RequirementsTemplater.cs#L626)

**Undrafted CTA alignment**

- Epic card create-story note above AC (match placeholder)
  [`HtmlRenderAdapter.Epics.cs:271`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs#L271)

**Pins & ledger**

- Overlap + exact-stem cue tests
  [`UnplannedWorkGeometryTests.cs:741`](../../tests/SpecScribe.Tests/UnplannedWorkGeometryTests.cs#L741)

- Nested / unclosed / `<link>` extractor pins
  [`FollowUpGeometryNormalizeTests.cs:41`](../../tests/SpecScribe.Tests/FollowUpGeometryNormalizeTests.cs#L41)

- Empty-primary Render omit
  [`FollowUpRowTests.cs:118`](../../tests/SpecScribe.Tests/FollowUpRowTests.cs#L118)

- Dup epic Number no-throw
  [`RequirementsAndProgressTests.cs:1105`](../../tests/SpecScribe.Tests/RequirementsAndProgressTests.cs#L1105)

- Epic-card note-above-AC assert
  [`HtmlRenderAdapterTests.cs:193`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs#L193)

- Golden Story 1.2 AC + fingerprint
  [`SiteGeneratorAdapterTests.cs:60`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs#L60)

- Eight deferred bullets marked resolved
  [`deferred-work.md`](./deferred-work.md)
