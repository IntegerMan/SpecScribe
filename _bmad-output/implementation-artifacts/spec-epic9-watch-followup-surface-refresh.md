---
title: 'Watch-mode refresh of deferred / follow-up surfaces'
type: 'bugfix'
created: '2026-07-17T23:56:11-04:00'
status: 'done'
baseline_commit: '5bdb0806053de45e97db438877b21bf5efe3a80e'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Editing `deferred-work.md` under watch leaves `deferred-work.html` and `follow-ups/*` detail/group pages stale until a full `GenerateAll`. Ledger text blames `GenerateOne`, but watch routes `implementation-artifacts/**` (including that note) to `RegenerateEpics`, which never runs those writers. `GenerateOne` still skips `WriteDeferredWork` / `WriteFollowUpDetails` (group pages already patched in 9.13).

**Approach:** Run the GenerateAll follow-up write sequence on the watch paths that can change deferred membership or deep links — primarily `RegenerateEpics`, plus `GenerateOne` for parity — without a full site wipe.

## Boundaries & Constraints

**Always:**
- After `RegenerateEpics` and `GenerateOne`, rewrite deferred list + follow-up detail + group pages from current on-disk deferred content.
- Keep existing `GenerateOne` quick-dev + index refresh; add the same follow-up refresh to `RegenerateEpics` before `WriteIndex`.
- Inventory/open tallies for those writes must not prefer a stale `_docs` BodyHtml open-count when the note changed on disk (reuse the source-read discipline already used for sunburst).
- Preserve NFR8 (no empty group pages) and existing `group-*.html` prune; no OutputRoot wipe on watch.
- AD-5: `RegenerateEpics` still does not re-parse sprint; action-item pages may stay sprint-stale until data-source / full generate.

**Ask First:**
- Calling `WriteActionItems` from `RegenerateEpics` (sprint intentionally not re-parsed there).
- Changing FileWatcher routing so `deferred-work.md` takes a different path than other impl-artifacts.

**Never:**
- Full `GenerateAll` on every story save under `implementation-artifacts/`.
- Changing deferred authoring, slug algorithms, or sunburst membership rules beyond refreshed writers.
- Closing unrelated Epic 9 deferred items (geometry weight, parsing, coverage maps, etc.).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| deferred-work edit in watch | GenerateAll; append open bullet; `RegenerateEpics()` | New `follow-ups/{slug}.html`; deferred list includes it; groups match | Unreadable note: NFR2 degrade |
| item removed / resolved | Resolve or delete bullet; `RegenerateEpics()` | No longer shown as open on list/detail/group; epic/story geometry matches | Do not invent aggressive non-group wipe |
| GenerateOne parity | Non–impl-artifact `.md` change; `GenerateOne` | Deferred list + detail writers run (+ existing group/quick-dev/index) | Same as GenerateAll writers |
| Sprint-only change | `sprint-status.yaml` edit | Still full `GenerateAll` via data-source route | N/A |
| No deferred note | Missing `deferred-work.md` | Writers no-op; epic regen succeeds | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteGenerator.cs` -- `GenerateAll` follow-up block; `GenerateOne` (group only); `RegenerateEpics` (no follow-up writers); `WriteDeferredWork` / `WriteFollowUpDetails` / `WriteFollowUpGroupPages` / `RewriteQuickDevPages`; `ResolveFollowUpWork` / `TryConvertDeferredDoc`
- `src/SpecScribe/FileWatcherService.cs` -- `IsEpicsRelated` → `RegenerateEpics`; else `GenerateOne`
- `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` -- extend for watch routes
- `_bmad-output/implementation-artifacts/deferred-work.md` -- watch-mode ledger bullets to resolve

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SiteGenerator.cs` -- Private helper for deferred/follow-up writes (`WriteDeferredWork`, `WriteFollowUpDetails`, `WriteFollowUpGroupPages`, `RewriteQuickDevPages`) with on-disk-fresh inventory; call from `GenerateOne` and from `RegenerateEpics` before `WriteIndex`
- [x] `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` -- After GenerateAll, mutate deferred-work, `RegenerateEpics` → new detail + updated list; `GenerateOne` also refreshes deferred/detail
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Resolve the two watch-mode follow-up writer bullets without rewriting unrelated entries

**Acceptance Criteria:**
- Given GenerateAll then a new open deferred bullet, when `RegenerateEpics()` runs, then deferred list + matching `follow-ups/{slug}.html` reflect it without another full generate.
- Given the same baseline, when `GenerateOne` runs for a non–impl-artifact markdown file, then deferred list + detail writers run (not only group/quick-dev/index).
- Given watch follow-up refresh, when group membership shrinks, then stale `group-*.html` pages are still pruned.
- Given `RegenerateEpics`, when sprint yaml was not the changed file, then sprint is not re-parsed (AD-5).

## Spec Change Log

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~FollowUpSurfaces"` -- expected: pass
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green

## Suggested Review Order

**Shared refresh helper**

- Entry point: deferred list + detail + group + quick-dev rewrite for watch and full generate
  [`SiteGenerator.cs:2729`](../../../src/SpecScribe/SiteGenerator.cs#L2729)

- Clear stale `_docs` deferred entries, then re-convert from disk (delete/move safe)
  [`SiteGenerator.cs:2755`](../../../src/SpecScribe/SiteGenerator.cs#L2755)

**Watch call sites**

- Real deferred-work watch path: sync tallies, null ledger, refresh before index
  [`SiteGenerator.cs:531`](../../../src/SpecScribe/SiteGenerator.cs#L531)

- `GenerateOne` parity: full follow-up refresh, not only group/quick-dev
  [`SiteGenerator.cs:398`](../../../src/SpecScribe/SiteGenerator.cs#L398)

- `GenerateAll` uses the same helper to avoid writer drift
  [`SiteGenerator.cs:365`](../../../src/SpecScribe/SiteGenerator.cs#L365)

**Tests & ledger**

- RegenerateEpics after append + resolve asserts list/detail/group freshness
  [`FollowUpSurfacesTests.cs:583`](../../../tests/SpecScribe.Tests/FollowUpSurfacesTests.cs#L583)

- GenerateOne parity for non–impl-artifact markdown
  [`FollowUpSurfacesTests.cs:660`](../../../tests/SpecScribe.Tests/FollowUpSurfacesTests.cs#L660)
