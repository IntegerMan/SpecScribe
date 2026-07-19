---
title: 'Story 3.2 deferred-debt cleanup'
type: 'chore'
created: '2026-07-19'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '33c89ea4bbc2f3d2e42f7115a47254ce78e2317a'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Three items were deferred from Story 3.2's code review (`deferred-work.md`, "code review of story-3-2"). Investigation shows two of the three (nav/quick-link gating, `AppendTreeNode` recursion cap) target `WriteStructure`/`ProjectTree`/`structure.html`, which no longer exist — Story 3.4 was later retired and its tree surface replaced by Story 7.6's Code Map (`WriteCodeMap`/`CodeMap.BuildVariants`). A stale orphaned doc comment for the deleted `WriteStructure` still sits directly above `WriteCodeMap`'s real doc comment in `SiteGenerator.cs`, which is what the reviewer's tooling actually matched against. The third item flags an ambiguous Dev Note in the story record that a reviewer read as calling for merging `TryCompute`'s always-on `--name-only` call with `TryComputeDeep`'s opt-in `--numstat` call — but the Dev Note's "single git code path" language is scoped to the deep-git family only (3.2/3.8/7.4/7.5); merging with the separate, lighter, always-on baseline call would risk the FR-10 performance gate for no functional benefit.

**Approach:** Resolve all three as documentation/comment fixes with zero behavior change: (1) delete the orphaned `WriteStructure`-era doc comment block in `SiteGenerator.cs`; (2) clarify the Story 3.2 Dev Notes to explicitly scope "single git code path" to the deep-git family and note the baseline call is deliberately separate; (3) record all three as resolved in `deferred-work.md` with the retirement/clarification rationale.

## Boundaries & Constraints

**Always:** Zero functional/behavior change — no git invocation, gating logic, or rendering output may change. Preserve existing golden/test output byte-for-byte.

**Ask First:** If, contrary to investigation, `ProjectTree`/`AppendTreeNode`/`structure.html` are found to still exist anywhere in `src/` reachable from a live code path (not just the stale doc comment), HALT — the deferred items would then be real bugs requiring an actual code fix, not a doc cleanup.

**Never:** Do not merge `TryCompute`'s `--name-only` call with `TryComputeDeep`'s `--numstat` call. Do not touch `WriteCodeMap`'s actual logic. Do not resurrect `WriteStructure`.

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteGenerator.cs:2698-2704` -- orphaned `WriteStructure`/`ProjectTree` doc comment stacked directly above `WriteCodeMap`'s real summary; delete it
- `src/SpecScribe/GitMetrics.cs:437,449` -- `TryComputeDeep` doc comment already says "a separate call from `TryCompute`"; add one sentence making the "single git code path" scope (deep-git family only) explicit so it can't be misread as covering the baseline call too
- `_bmad-output/implementation-artifacts/3-2-optional-deep-git-analytics-controls.md:75` -- Dev Notes "[Re-plan 2026-07-08]" bullet; add a clarifying clause scoping "single git code path" to 3.2/3.8/7.4/7.5, excluding Story 3.1's baseline call
- `_bmad-output/implementation-artifacts/deferred-work.md` -- mark all three story-3-2 items resolved

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SiteGenerator.cs` -- delete the stale orphaned doc-comment block (lines ~2698-2704) describing the retired `WriteStructure`/`structure.html`/`ProjectTree` -- it precedes `WriteCodeMap`'s real, current doc comment and misdescribes it
- [x] `src/SpecScribe/GitMetrics.cs` -- add one clarifying sentence to `TryComputeDeep`'s doc comment stating the "single shared" fetch is scoped to the deep-git family (3.2/3.8/7.4/7.5) and deliberately excludes Story 3.1's separate, always-on baseline call
- [x] `_bmad-output/implementation-artifacts/3-2-optional-deep-git-analytics-controls.md` -- append a clarifying clause to the "[Re-plan 2026-07-08]" Dev Notes bullet scoping "single git code path" to the deep-git family only
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- mark all three story-3-2 deferred items resolved (strikethrough + RESOLVED note) with the rationale: items 2/3 target code retired when Story 3.4 was superseded by Story 7.6; item 1 was a Dev Notes ambiguity, now clarified, no merge performed

**Acceptance Criteria:**
- Given the current codebase, when searched for `ProjectTree`, `AppendTreeNode`, or `structure.html` in `src/`, then no live (non-comment, non-binary) reference exists, confirming the doc-comment deletion removes only dead documentation
- Given `dotnet build`/`dotnet test` after the change, then the build succeeds and the full test suite remains green with no golden-fingerprint changes
- Given the updated `deferred-work.md`, when read, then all three story-3-2 entries show a RESOLVED annotation naming this spec and the rationale

## Design Notes

No design decisions — this is a documentation/comment correction with no runtime behavior touched. The only judgment call is *not* performing the literal git-invocation merge the original deferred item's evidence text suggested, because doing so would trade a real, tested FR-10 performance guarantee for a purely cosmetic "one code path" tidiness gain, and the underlying Dev Note never actually asked for that scope.

## Verification

**Commands:**
- `dotnet build` -- expected: succeeds with no new warnings
- `dotnet test` -- expected: full suite green, no golden fingerprint diffs

## Suggested Review Order

**Dev Notes ambiguity (the "single git code path" scope note)**

- Entry point: the clarified Dev Notes bullet, now explicit that "single git code path" scopes to the deep-git family only, not Story 3.1's baseline call.
  [`3-2-optional-deep-git-analytics-controls.md:75`](../../_bmad-output/implementation-artifacts/3-2-optional-deep-git-analytics-controls.md#L75)

- The stale sequencing bullet this scope note supersedes is now marked superseded in place, so a reader hitting it first isn't misled.
  [`3-2-optional-deep-git-analytics-controls.md:74`](../../_bmad-output/implementation-artifacts/3-2-optional-deep-git-analytics-controls.md#L74)

- Matching code-side clarification: `TryComputeDeep`'s doc comment now states the scope and cross-references the two calls' differing bounds (`-n 200` vs `-n 300`).
  [`GitMetrics.cs:436`](../../src/SpecScribe/GitMetrics.cs#L436)

**Retired-code cleanup (Story 3.4 → 7.6 handoff)**

- Orphaned `WriteStructure`/`ProjectTree`/`structure.html` doc comment deleted from directly above `WriteCodeMap`'s real, current doc comment.
  [`SiteGenerator.cs:2695`](../../src/SpecScribe/SiteGenerator.cs#L2695)

**Deferred-work ledger**

- All three original story-3-2 items marked RESOLVED with verification method cited (grep confirmation, not just assertion).
  [`deferred-work.md:370`](../../_bmad-output/implementation-artifacts/deferred-work.md#L370)

- New forward-looking entry spun off separately: the legitimate future optimization (reuse numstat hotspots for top-changed-files when `--deep-git` is on) preserved as its own open item rather than buried by the RESOLVED note.
  [`deferred-work.md:377`](../../_bmad-output/implementation-artifacts/deferred-work.md#L377)

- Pre-existing stale "Story 3.1 NOT yet merged" status line, surfaced incidentally during review, recorded as a separate deferred item (not this cleanup's problem to fix).
  [`deferred-work.md:380`](../../_bmad-output/implementation-artifacts/deferred-work.md#L380)
