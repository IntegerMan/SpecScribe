---
title: 'DeferredWorkParser: stop treating bare source_spec bullets as phantom open items'
type: 'bugfix'
created: '2026-07-22'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '929c33dcdc2ad360c2efa8d524078ee2b5845a96'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `DeferredWorkParser.ParseItems` treats every column-0 `- ` bullet as a standalone deferred-work item, including a bare `- source_spec: \`X.md\`` header bullet with no summary/evidence. That bullet is pure group-provenance metadata (already captured separately via `FollowUpRefs.SourceSpecFileFromText` on the whole section), but because it's bulleted it gets flushed as a real, empty, always-"Open" item — surfacing as a phantom follow-up entry with no content (e.g. `deferred-code-review-of-story-4-8-2026-07.html`, `deferred-code-review-of-story-4-2-2026-07.html`). 12 sections across `deferred-work.md` currently have this shape (stories 3-6, 3-7, 4-2, 4-8, 6-1, 6-3, 6-6, 6-9, 7-2, and 3 freeform specs); every one produces a phantom "Open" item even though the section's real findings are already resolved.

**Approach:** In `ParseItems`, when a flushed item's entire content is a single line matching a bare `source_spec: \`file\`` pattern (nothing else on that line, no continuation lines), skip it instead of adding it to `items` — matching the function's own existing comment ("pre-list prose... is ignored as an item"), which today is only true when the line isn't bulleted. Group-level provenance (`SourceStoryId`/`SourceStoryHref`/`SourceKey`) is unaffected since it's derived independently from the whole section text, not from parsed items.

## Boundaries & Constraints

**Always:**
- The standard multi-line `- source_spec: X\n  summary: ...\n  evidence: ...` format (the ~100 normal entries in the file) must keep parsing exactly as today — the skip only applies when the bulleted line is *only* `source_spec: ...` with no other content and no continuation lines.
- Group-level `SourceStoryId` / `SourceStoryHref` / `SourceKey` resolution must be unchanged — still derived from `FollowUpRefs.SourceSpecFileFromText(section)` over the whole section.
- `deferred-work.md` itself is not edited — this is a parser fix; existing file content stays as-is.

**Ask First:** None anticipated — this is a narrow, single-function change with existing test coverage as a safety net.

**Never:**
- Do not touch `WorkInventory.CountOpenItems` — it counts raw `<li>` elements in rendered HTML independently of `DeferredWorkParser`, and its divergence from per-item counts is already documented as legitimate (`FollowUpGeometry.cs:119`). Out of scope.
- Do not attempt to rewrite the 12 affected sections of `deferred-work.md` by hand — the parser fix alone resolves the phantom-item display for all of them on the next regenerate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bare source_spec bullet alone | `- source_spec: \`4-8-....md\`` as its own top-level bullet, next bullet is a separate already-resolved item | No item is created for the bare bullet; group still resolves `SourceStoryId`/`Href` from section text; the resolved item remains the section's only item | N/A |
| Standard schema bullet | `- source_spec: X\n  summary: ...\n  evidence: ...` (single bullet, multi-line) | Parses exactly as today — one item containing all three fields | N/A |
| Bare source_spec is the section's only bullet | `## Deferred from: ...` heading followed solely by `- source_spec: \`X.md\`` | Group has zero items → filtered out by existing `nonEmpty` logic; if it's the only group, falls back to `Unstructured` (existing behavior, unchanged) | N/A |
| Bare source_spec bullet with trailing prose on the same line | `- source_spec: \`X.md\` — some note` | Still becomes a real item (line has content beyond the bare token) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/DeferredWorkParser.cs` -- `ParseItems`'s `Flush()` local function needs the new bare-source_spec-only skip check
- `tests/SpecScribe.Tests/DeferredWorkParserTests.cs` -- add regression coverage; existing tests (`Parse_SpecHeading_PreservesSourceKeyWithoutStoryId`, `Parse_PathPrefixedSourceSpec_ResolvesStoryIdAndPlaceholderHref`) already exercise this shape but never asserted item count, so they should keep passing unchanged

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/DeferredWorkParser.cs` -- add a `BareSourceSpecLine` regex (`^source_spec:\s*` + backtick-optional file token + `\s*$`, case-insensitive) and a check in `Flush()`: when `current.Count == 1` and the stripped first line matches it, clear `current` and return without adding an item -- closes the phantom-item gap described in the Problem statement
- [x] `tests/SpecScribe.Tests/DeferredWorkParserTests.cs` -- add a test asserting a section with a bare `- source_spec: \`X.md\`` bullet followed by one real item yields exactly 1 item (not 2), and a test confirming a bare source_spec bullet with trailing prose still becomes an item

**Acceptance Criteria:**
- Given a `## Deferred from:` section containing only `- source_spec: \`4-8-generation-diagnostics-and-configuration-log-page.md\`` followed by an already-resolved item bullet, when parsed, then the group has exactly one item (the resolved one) and `SourceStoryId`/`SourceStoryHref` are still resolved correctly
- Given the standard `- source_spec: X\n  summary: ...\n  evidence: ...` single-bullet format, when parsed, then behavior is unchanged (one item containing all three fields)
- Given the full current `deferred-work.md`, when regenerated, then none of the 12 known bare-source_spec sections (including story-4-8 and story-4-2) produce a phantom empty "Open" follow-up item

## Spec Change Log

## Design Notes

The fix is intentionally scoped to `Flush()`'s single-line case rather than a broader rewrite, because the multi-line `source_spec`/`summary`/`evidence` schema (used ~100 times in the file) must be untouched. The distinguishing signal is `current.Count == 1` — a bare header bullet has no continuation lines, while every standard-schema item does.

## Verification

**Commands:**
- `dotnet test --filter DeferredWorkParserTests` -- expected: all existing tests plus the two new ones pass
- `dotnet test` -- expected: full suite green, no regression elsewhere (golden-diff tests in particular, since deferred-work.html/follow-up pages render fewer items)

**Manual checks (if no CLI):**
- Run `specscribe generate` against this repo and confirm `SpecScribeOutput/follow-ups/deferred-code-review-of-story-4-8-2026-07.html` and `...story-4-2-2026-07.html` are no longer emitted, and the Epic 4 "follow-ups" local-context pills no longer show a `(no deferred text)` entry

## Suggested Review Order

**Parser fix**

- The regex defining what counts as "bare" — widened during review to also match a missing filename token.
  [`DeferredWorkParser.cs:55`](../../src/SpecScribe/DeferredWorkParser.cs#L55)

- The actual skip check in `Flush()` — hardened during review to tolerate blank continuation lines (loose-list CommonMark), not just `current.Count == 1`.
  [`DeferredWorkParser.cs:145`](../../src/SpecScribe/DeferredWorkParser.cs#L145)

**Regression coverage**

- Core case: the repo's own real story-4-8 section, verbatim, yields exactly one (already-resolved) item.
  [`DeferredWorkParserTests.cs:358`](../../tests/SpecScribe.Tests/DeferredWorkParserTests.cs#L358)

- The review-caught gap: a blank line between the bare bullet and the next bullet must not resurrect the phantom item.
  [`DeferredWorkParserTests.cs:379`](../../tests/SpecScribe.Tests/DeferredWorkParserTests.cs#L379)

- Edge case: a section whose *only* bullet is bare source_spec correctly falls back to the pre-existing Unstructured path.
  [`DeferredWorkParserTests.cs:399`](../../tests/SpecScribe.Tests/DeferredWorkParserTests.cs#L399)

- Simplified version of the same shape, for a quick read before the verbatim fixture.
  [`DeferredWorkParserTests.cs:337`](../../tests/SpecScribe.Tests/DeferredWorkParserTests.cs#L337)
