---
title: '9.5 deferred — Markdig collision slugs + evidence-strip extractor polish'
type: 'bugfix'
created: '2026-07-18'
status: 'done'
baseline_commit: '1ea474d139c22b19b7c0a2046aee417d42795ad7'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Three open deferred items from Story 9.5 leave (1) a second same-titled remainder H2 expanded when Markdig emits `references-1` / `dev-notes-1`, (2) the verified-date pill picking a later Change Log row after a malformed top dated-shape row, and (3) test-evidence reading `### Dev Agent Record` while File List / Dev Agent panel extractors ignore that heading — asymmetric empty surfaces.

**Approach:** Normalize Markdig collision ids before the collapse slug check; abort Change Log verification on the first dated-shape row with an unparseable date; extend File List and Dev Agent Record extractors to accept `### Dev Agent Record` the same way test evidence already does. Close the three deferred entries when done.

## Boundaries & Constraints

**Always:**
- Keep collapse match case-sensitive / Ordinal and preserve raw Markdig ids on `<details id="{slug}-section">` and summary H2 ids (unique anchors).
- Empty-action Change Log rows may still skip; only a dated-shape match with failed `yyyy-MM-dd` parse returns null.
- Prefer extending siblings to H3 over removing H3 from `ExtractTestEvidence` (Story 9.4 intentional).
- Deterministic, never-throw parsers; NFR8 degrade-to-absent when nothing matches.
- Mark the three deferred-work.md bullets under the 9.5 review section as resolved with this spec key.

**Ask First:**
- Changing `SplitStoryArtifact` remainder-stop / DAR excision to also key on `### Dev Agent Record` (rarer remainder-duplication case; out of this batch unless you say expand).

**Never:**
- Broad prefix matching (`references-foo`) or unbounded slug enumeration.
- New authoring schema or evidence-strip UI redesign.
- Skipping past a malformed dated-shape top row to “helpfully” use an older valid row.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Collision slug collapse | Remainder HTML with `id="references"` and `id="references-1"` H2s | Both wrap in `collapsible-section`; Context/Tasks H2s stay expanded | N/A |
| Base slug unchanged | Single `id="dev-notes"` | Still wraps (existing behavior) | N/A |
| Malformed top dated row | Change Log: `- 2026-13-99 — **…**` then a valid later row | `ExtractChangeLogVerification` → null | No throw |
| Empty action after valid date | Dated row with empty action, then later valid row | Skip empty-action; later valid row may win | N/A |
| H3 DAR + File List | Artifact with `### Dev Agent Record` and `### File List` paths | `ExtractFileListEntries` returns paths; `ExtractDevAgentRecord` non-empty; test evidence still works | Absent → empty/null as today |
| H3 DAR tests only | `### Dev Agent Record` with test phrase, no File List | Tests pill may fill; File List empty (honest absence) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/CollapsibleSections.cs` -- exact `headingSlugs.Contains(slug)` at wrap time; `StoryRemainderSlugs` = `dev-notes`/`references`
- `src/SpecScribe/EpicsParser.cs` -- `ExtractChangeLogVerification` (`continue` on bad date); `ExtractTestEvidence` (already H3); `ExtractDevAgentRecord` (## only)
- `src/SpecScribe/ChangeSurface.cs` -- `ExtractFileListEntries` keys only `## Dev Agent Record`
- `tests/SpecScribe.Tests/CollapsibleSectionsTests.cs` -- no duplicate-slug case today
- `tests/SpecScribe.Tests/EpicsParserTests.cs` -- `SkipsMalformedDate` encodes continue; H3 test-evidence exists; DAR H3 gap
- `tests/SpecScribe.Tests/ChangeSurfaceTests.cs` -- File List under ## only
- `_bmad-output/implementation-artifacts/deferred-work.md` -- lines 91–93 under 9.5 review

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/CollapsibleSections.cs` -- Before `Contains`, normalize Markdig collision form by stripping a trailing `-\d+` from the id for the set check only; keep raw slug for details/summary ids -- so `references-1` collapses like `references`
- [x] `src/SpecScribe/EpicsParser.cs` -- On first `ChangeLogTopEntry` match with failed `TryParseExact`, `return null` (not `continue`); align XML doc with abort semantics; add `### Dev Agent Record` fallback to `ExtractDevAgentRecord` mirroring test-evidence heading order
- [x] `src/SpecScribe/ChangeSurface.cs` -- Accept `### Dev Agent Record` when `##` is absent (same File List subsection rules inside the section)
- [x] `tests/SpecScribe.Tests/CollapsibleSectionsTests.cs` -- Dual `references` + `references-1` both wrap; non-target H2s untouched
- [x] `tests/SpecScribe.Tests/EpicsParserTests.cs` -- Flip malformed-date test to expect null; add H3 `ExtractDevAgentRecord` coverage
- [x] `tests/SpecScribe.Tests/ChangeSurfaceTests.cs` -- H3 DAR + File List yields paths
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Resolve the three 9.5 bullets with this spec key

**Acceptance Criteria:**
- Given remainder HTML with Markdig collision ids for Dev Notes/References, when WrapStoryRemainder runs, then every collision form of those base slugs collapses and other H2s stay expanded
- Given a Change Log whose first dated-shape row has an impossible calendar date, when ExtractChangeLogVerification runs, then it returns null even if a later row is valid
- Given an artifact whose only Dev Agent Record heading is H3, when File List / Dev Agent Record / test evidence extractors run, then all three use that heading (empty only when their own content is absent)
- Given the three deferred bullets for 9.5, when this work lands, then they are marked resolved pointing at this spec

## Spec Change Log

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~CollapsibleSectionsTests|FullyQualifiedName~EpicsParserTests|FullyQualifiedName~ChangeSurfaceTests"` -- expected: all matching tests green
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green before done

## Design Notes

Markdig AutoIdentifier collision form is base slug + `-` + decimal counter (`references-1`). Normalize only that trailing `-\d+` for membership; do not treat `references-2024`-style tokens specially beyond that rule (acceptable rare false positive if someone authored a literal id that ends in `-digits` matching a remainder base slug).

Item A intentionally reverses the 9.5-era “skip malformed” patch back to top-row abort: the evidence strip’s “top entry” claim is false if a dated-shape head row is invalid.

## Suggested Review Order

**Markdig collision collapse**

- Strip trailing `-N` for set membership only; keep raw ids on details/summary
  [`CollapsibleSections.cs:47`](../../src/SpecScribe/CollapsibleSections.cs#L47)

- Membership check uses normalized base slug against `StoryRemainderSlugs`
  [`CollapsibleSections.cs:74`](../../src/SpecScribe/CollapsibleSections.cs#L74)

**Change Log top-row abort**

- First dated-shape row with unparseable calendar → null (no skip-to-later)
  [`EpicsParser.cs:188`](../../src/SpecScribe/EpicsParser.cs#L188)

**H3 Dev Agent Record symmetry**

- File List accepts `### Dev Agent Record` when `##` absent
  [`ChangeSurface.cs:31`](../../src/SpecScribe/ChangeSurface.cs#L31)

- Dev Agent panel extractor mirrors the same H3 fallback
  [`EpicsParser.cs:303`](../../src/SpecScribe/EpicsParser.cs#L303)

**Tests & deferred close-out**

- Collision forms for both `dev-notes-1` and `references-1`
  [`CollapsibleSectionsTests.cs:75`](../../tests/SpecScribe.Tests/CollapsibleSectionsTests.cs#L75)

- Malformed top date + H3 DAR extractor coverage
  [`EpicsParserTests.cs:511`](../../tests/SpecScribe.Tests/EpicsParserTests.cs#L511)

- Three 9.5 deferred bullets marked resolved
  [`deferred-work.md:95`](./deferred-work.md#L95)
