---
title: 'Story 6.1 deferred-review cleanup: build-date determinism, product-metadata validation, and non-actionable closures'
type: 'chore'
created: '2026-07-20'
status: 'in-progress'
review_loop_iteration: 0
baseline_commit: '4d2106c'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 6.1's code review deferred four items. Two are real (the About-page build date is stamped with `UtcNow` at every compile, so identical commits built on different days differ byte-for-byte; and `ProductMetadata`'s commit-hash / pre-release parsing is naive — `sha[..7]` with no hex check, `Version.Contains('-')`), and two are non-code (a `source_spec:` header line, and a scoping observation about changes that predate 6.1's diff window).

**Approach:** Make the build-date stamp honor `SOURCE_DATE_EPOCH` (the reproducible-builds standard) so builds are deterministic exactly when it matters, with `UtcNow` as the unchanged local-dev fallback. Harden `ProductMetadata` parsing so only a plausible hex sha is surfaced as a commit hash and pre-release detection requires a real trailing label, with unit tests pinning the edges. Close the two non-code items in `deferred-work.md` with a documented rationale.

## Boundaries & Constraints

**Always:** Keep the generated HTML surface byte-identical for a normal local build (no `SOURCE_DATE_EPOCH`, no SourceLink sha) — this is metadata/build plumbing only. Keep `ProductMetadata.FromAssembly` reflection-only (no file/network I/O). New/tightened parsing must have unit coverage. Existing `AboutTemplaterTests` must stay green.

**Ask First:** (none — dispositions already chosen: honor `SOURCE_DATE_EPOCH`; add validation + tests; document-close items 1 & 4.)

**Never:** Do not add a git invocation to the build. Do not change the `Version`/`Authors`/`Description` csproj values. Do not attempt to "fix" item 4's predating changes (explicitly not actionable via 6.1). Do not strike existing `deferred-work.md` entries other than these two closures.

## I/O & Edge-Case Matrix

Applies to `ProductMetadata.ParseInformationalVersion(informational)` → `(version, commitHash)` and `IsPrerelease`.

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Deterministic build w/ full sha | `"0.1.0-preview+9f8e7d6c5b4a…"` (40 hex) | version `0.1.0-preview`, commitHash `9f8e7d6` (first 7) | N/A |
| Short hex suffix | `"1.0.0+abcd"` | version `1.0.0`, commitHash `abcd` (kept whole, <7) | N/A |
| Non-hex `+` suffix | `"1.0.0+branch-x"` | version `1.0.0`, commitHash `null` (Build row shows date only) | drop silently |
| No `+` suffix | `"1.0.0"` | version `1.0.0`, commitHash `null` | N/A |
| Null / empty informational | `null` or `""` | version `""` (caller falls back to `AssemblyName.Version`), commitHash `null` | N/A |
| Pre-release label present | version `"0.1.0-preview"` | `IsPrerelease == true` | N/A |
| Stable version | version `"1.0.0"` | `IsPrerelease == false` | N/A |
| Trailing bare dash | version `"1.0.0-"` | `IsPrerelease == false` (no label content) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SpecScribe.csproj` -- the `AssemblyMetadata Include="BuildDate"` item (line ~29) stamps `UtcNow`; source of item 2.
- `src/SpecScribe/AboutTemplater.cs` -- `ProductMetadata` record: `IsPrerelease` (line 27), `FromAssembly` informational-version split (lines 50-61); source of item 3.
- `tests/SpecScribe.Tests/AboutTemplaterTests.cs` -- existing About/metadata coverage; extend with parsing edge cases.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- the story-6-1 section (lines 344-349); close items 1 & 4, mark 2 & 3 resolved.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SpecScribe.csproj` -- introduce a `SpecScribeBuildDate` property that resolves from `SOURCE_DATE_EPOCH` (Unix seconds → `yyyy-MM-dd` UTC) when set, else `UtcNow.ToString('yyyy-MM-dd')`, and feed it to the `BuildDate` `AssemblyMetadata`. Update the surrounding comment. -- makes the stamp deterministic under reproducible builds without a git call.
- [x] `src/SpecScribe/AboutTemplater.cs` -- extract an `internal static (string version, string? commitHash) ParseInformationalVersion(string?)` helper with a hex-sha guard (surface commitHash only when the `+` suffix is all-hex; truncate to 7); tighten `IsPrerelease` to require a hyphen with non-empty content after it; wire `FromAssembly` to the helper keeping the `AssemblyName.Version` fallback. -- hardens display without changing trusted-source behavior.
- [x] `tests/SpecScribe.Tests/AboutTemplaterTests.cs` -- add unit tests covering every I/O Matrix row for `ParseInformationalVersion` and `IsPrerelease`. -- pins the edge behavior.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- in the story-6-1 section, mark items 2 & 3 resolved (this pass), and close item 1 (header line, not a defect) and item 4 (predates 6.1's diff, not actionable via 6.1) with a dated rationale using the existing strike-through convention. -- keeps the deferred log honest.

**Verification status (2026-07-20):** csproj MSBuild eval confirmed both branches (`2026-07-20` default; `2023-11-14` for `SOURCE_DATE_EPOCH=1700000000`), and a `--no-incremental` build with the fixed epoch stamped `BuildDate=2023-11-14` read back via reflection (AC #1/#2 ✓). The 11 `AboutTemplaterTests` (incl. all new parsing/pre-release cases) passed when the suite was last runnable (AC #4 partial ✓). The **full-suite `dotnet test` is currently blocked by a concurrent session's in-flight, non-compiling edits on shared `main`** (`SiteGeneratorHowToReadTests.cs:385` references a removed `RenderPage(methodPresent:)` overload; the golden-fingerprint drift is from their `specscribe.css/.js`/dashboard/nav changes). This is unrelated to this spec's diff (`AboutTemplater.cs`, `SpecScribe.csproj`, `AboutTemplaterTests.cs`, `deferred-work.md`). Re-run the full suite once the tree is coherent.

**Acceptance Criteria:**
- Given no `SOURCE_DATE_EPOCH` in the environment, when the project builds, then the stamped `BuildDate` is today's UTC date and a normal `specscribe generate` produces byte-identical HTML to before this change.
- Given `SOURCE_DATE_EPOCH` is set to a fixed Unix timestamp, when the project builds twice, then both builds stamp the same `BuildDate` (the epoch's UTC calendar date), independent of wall-clock day.
- Given an informational version whose `+` suffix is not hex, when `ProductMetadata` is read, then `CommitHash` is null and the About Build row shows the date only (never a truncated non-hash).
- Given the full test suite, when `dotnet test` runs, then all tests pass including the new parsing edges and the unchanged `AboutTemplaterTests`.

## Design Notes

MSBuild expression (no static-field access — construct the epoch explicitly so date-only `ToString` needs no timezone conversion):
```xml
<PropertyGroup>
  <SpecScribeBuildDate Condition="'$(SOURCE_DATE_EPOCH)' != ''">$([System.DateTime]::new(1970, 1, 1).AddSeconds($(SOURCE_DATE_EPOCH)).ToString('yyyy-MM-dd'))</SpecScribeBuildDate>
  <SpecScribeBuildDate Condition="'$(SOURCE_DATE_EPOCH)' == ''">$([System.DateTime]::UtcNow.ToString('yyyy-MM-dd'))</SpecScribeBuildDate>
</PropertyGroup>
```
`IsShaLike` = non-empty and every char in `[0-9a-fA-F]`. The hex guard is a no-op for real deterministic builds (SourceLink appends a hex sha) and only suppresses garbage; no existing test asserts a non-null `CommitHash`, so no regression.

## Verification

**Commands:**
- `dotnet build src/SpecScribe/SpecScribe.csproj` -- expected: succeeds; the MSBuild `SOURCE_DATE_EPOCH` expression evaluates without error.
- `SOURCE_DATE_EPOCH=1700000000 dotnet build src/SpecScribe/SpecScribe.csproj -p:… ` then read the `BuildDate` `AssemblyMetadata` back via reflection -- expected: `2023-11-14` (the epoch's UTC date), not today.
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: all green, new parsing tests included.

**Manual checks:**
- Confirm the story-6-1 block in `deferred-work.md` shows all four items dispositioned (2 & 3 resolved, 1 & 4 closed with rationale).
