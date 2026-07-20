---
title: 'Story 7.1 deferred-debt cleanup'
type: 'chore'
created: '2026-07-20T10:16:00-04:00'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: 'dcb279b'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 7.1's code review left five open items in `deferred-work.md`: (1) IgnoreCase path maps can collapse distinct Linux files, (2) `CommitHref` prefix-matches without ambiguity checks, (3) the code-map walk fully loads every file just to count lines and drops >1MB files from the treemap, (4) placeholder code pages never get Insights/History tabs even when deep-git has the data, and (5) `TabGroupName`/`Slugify` can collide paths that differ only by `/` vs `-`.

**Approach:** Align code-page path maps with the git Ordinal path-key policy; make abbreviated-hash resolution fail closed on ambiguity; stream line counts for the map (including oversized files); thread insight/href params into placeholders so tabs render; encode path separators distinctly in slugification. Mark all five items RESOLVED in `deferred-work.md`.

## Boundaries & Constraints

**Always:**
- `_codePages`, `_codeReverseMap`, `_citerToFiles`, and the code-page discovery `SortedSet` use `StringComparer.Ordinal` (match git / Epic 8 path-key policy). Do not flip git-layer maps to IgnoreCase.
- `CommitHref`: exact match first; on prefix fallback, return the unique match or `null` when 0 or ≥2 keys share the prefix (plain hash text beats a wrong link).
- Code-map enumeration counts lines without allocating the full file string; oversized files still contribute LOC to the map. The 1MB cap remains for **inline code-page rendering** only. Binary/unreadable files still skip.
- `RenderPlaceholder` accepts the same optional `insight` / `commitHref` / `dayHref` / `coupledFileHref` / edge params as `RenderPage` needs for Insights/History/Relationships, and emits those tabs when content exists; Code panel stays the placeholder reason. Call sites pass the already-built locals.
- `Slugify` (shared by `TabGroupName` and `RefGraphGroupSlug`) encodes `/` as a stable alphanumeric token before collapsing other non-alnum, so `a/b` and `a-b` never share a radio/checkbox group id.
- Strike through all five story-7-1 deferred entries with resolution notes pointing at this spec.

**Ask First:** none — decisions above close the deferred items as written.

**Never:**
- Change git-layer path comparers (`GitMetrics`, `ProgressCalculator` date maps) or adopt IgnoreCase git-wide.
- Revert the 1MB inline render cap for full source pages.
- Invent new tab UX; reuse existing `AppendTabs` / panel builders.
- Touch the separate 7.6 deferrals (broader `EnumerateCodeFiles` catch; `git ls-files -z`).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Linux case variants | Two tracked paths `Foo.cs` and `foo.cs` | Both get distinct `_codePages` entries | N/A |
| Ambiguous short hash | `_commitPages` has `abc111…` and `abc222…`; resolve `"abc"` | `null` (plain text) | N/A |
| Unique prefix | Only `abc111…` starts with `"abc"` | That page href | N/A |
| Oversized tracked text file | File >1MB, UTF-8 text | Present on code map with streamed line count; code page still placeholder | N/A |
| Placeholder + deep-git | Binary/oversized with `FileInsight` | Insights and/or History tabs present; Code shows reason | N/A |
| Slug collision pair | Paths `code/a/b.html` vs `code/a-b.html` | Distinct `TabGroupName` / ref-graph slug values | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteGenerator.cs` — `_codePages` / `_codeReverseMap` / `_citerToFiles` / discovery `SortedSet` comparers; `CommitHref`; `EnumerateCodeFiles` + `MaxCodeFileBytes` gate; placeholder call sites (~1691, ~1705)
- `src/SpecScribe/CodeFileTemplater.cs` — `RenderPlaceholder`, `Slugify` / `TabGroupName` / `RefGraphGroupSlug`, shared tab/panel helpers
- `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs` — placeholder + slug/tab pins
- `tests/SpecScribe.Tests/SiteGeneratorCodePagesTests.cs` / `SiteGeneratorCodeMapTests.cs` / `SiteGeneratorCommitDetailsTests.cs` — generation seams (extend or add focused cases)
- `_bmad-output/implementation-artifacts/deferred-work.md` — story-7-1 section (lines ~208–224)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SiteGenerator.cs` -- switch the four code-path collections to `Ordinal`; fix `CommitHref` ambiguity; stream-count lines in `EnumerateCodeFiles` and stop skipping oversized for the map; pass insight/hrefs into `RenderPlaceholder`
- [x] `src/SpecScribe/CodeFileTemplater.cs` -- extend `RenderPlaceholder` to reuse tab assembly when insight/relationships warrant it; fix `Slugify` separator encoding
- [x] `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs` -- pin placeholder+insight tabs and `a/b` vs `a-b` slug distinctness
- [x] `tests/SpecScribe.Tests/*` -- add/extend generation tests for Ordinal dual-case keys (or dictionary insert), ambiguous `CommitHref`, and oversized-on-map (prefer unit-level seams where generation is heavy)
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- mark the five story-7-1 items RESOLVED with this spec's id

**Acceptance Criteria:**
- Given two repo-relative paths that differ only by case, when code pages are discovered, then both remain addressable under Ordinal keys (no silent overwrite).
- Given an abbreviated hash matching two full hashes, when `CommitHref` runs, then it returns null; a unique prefix still resolves.
- Given a tracked text file over 1MB, when the code map builds, then the file appears with a correct line count; its code page remains a placeholder and does not load the full body for the map walk.
- Given a placeholder page with deep-git `FileInsight`, when rendered, then Insights and/or History tabs appear as on a full page.
- Given output paths that collide under today's Slugify, when tab groups are named, then their radio `name`s differ.
- Given the five deferred entries, when this ships, then each is struck through with a resolution note.

## Design Notes

**Ordinal vs IgnoreCase:** Epic 8 already accepted Ordinal for git path keys. Code-page maps are repo-relative paths from the same universe — Ordinal stops Linux collisions. Windows citation case mismatches degrade to unlink (same class as `ProgressCalculator` git-date misses), not silent merge.

**CommitHref fail-closed:** Wrong History link is worse than plain hash text; null matches the documented "no page" path.

**Slug encoding example:** before the non-alnum collapse, replace each `/` with a fixed token such as `x2f` so `foo/bar` → `foox2fbar` while `foo-bar` → `foo-bar`.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~CodeFileTemplater|FullyQualifiedName~CodeMap|FullyQualifiedName~CodePages|FullyQualifiedName~Commit"` -- expected: all matching tests pass
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green before marking done

## Suggested Review Order

**Ordinal path maps**

- Code-page dictionaries switch to Ordinal so Linux case variants stay distinct.
  [`SiteGenerator.cs:81`](../../src/SpecScribe/SiteGenerator.cs#L81)

- Discovery rebuild uses the same Ordinal comparer for maps and the SortedSet.
  [`SiteGenerator.cs:1415`](../../src/SpecScribe/SiteGenerator.cs#L1415)

**CommitHref fail-closed**

- Ambiguous short-hash prefixes return null instead of a non-deterministic first hit.
  [`SiteGenerator.cs:1319`](../../src/SpecScribe/SiteGenerator.cs#L1319)

**Streamed code-map LOC**

- Map walk streams line counts; oversized text still contributes.
  [`SiteGenerator.cs:3695`](../../src/SpecScribe/SiteGenerator.cs#L3695)

- Shared streaming counter with BOM peek + CRLF state machine.
  [`SiteGenerator.cs:1915`](../../src/SpecScribe/SiteGenerator.cs#L1915)

**Placeholder Insights/History**

- Call sites pass insight/hrefs into placeholders.
  [`SiteGenerator.cs:1702`](../../src/SpecScribe/SiteGenerator.cs#L1702)

- Placeholder reuses tab assembly; Code panel keeps the reason.
  [`CodeFileTemplater.cs:668`](../../src/SpecScribe/CodeFileTemplater.cs#L668)

**Slug uniqueness**

- Escape literal `x2f` then encode `/` so slash and hyphen paths never collide.
  [`CodeFileTemplater.cs:576`](../../src/SpecScribe/CodeFileTemplater.cs#L576)

**Tracking + tests**

- Five story-7-1 deferrals struck through; two residual risks deferred.
  [`deferred-work.md:5`](./deferred-work.md#L5)

- Unit pins for CommitHref, LOC streaming, Ordinal keys, slug escape, placeholder tabs.
  [`Story71DeferredDebtCleanupTests.cs:1`](../../tests/SpecScribe.Tests/Story71DeferredDebtCleanupTests.cs#L1)
