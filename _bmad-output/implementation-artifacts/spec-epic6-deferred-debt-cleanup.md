---
title: 'Epic 6 deferred-work cleanup pass (17 tracked items)'
type: 'bugfix'
created: '2026-07-22'
status: 'done'
baseline_commit: '41b541cd032523f3b593ea0b8a0a6c783adce3b5'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `deferred-work.md` carries 17 owner-flagged Epic 6 (VS Code extension + delivery) items across several review passes; most are already closed (audit trail / re-affirmed accepted tradeoffs) but 4 are genuinely open code gaps: `quoteCommandArg`'s PowerShell-incompatible escaping, the `busyTerminals` reuse-guard scope, the session-wide multi-root notice latch, and `SiteGenerator.RepoRelative`'s non-symlink-aware path math.

**Approach:** Triage all 17 against the current `deferred-work.md` state, fix the 4 still-open gaps narrowly (matching each item's own "fix direction" where one was suggested), and re-review the remaining 13 (already struck-through/closed, or explicitly re-affirmed-deferred) to confirm they still hold — updating the ledger only where a decision needed restating, not re-litigating settled calls.

## Boundaries & Constraints

**Always:** Keep each fix narrowly scoped to what its deferred item actually asks; preserve staged-not-executed terminal semantics (AD-6/ADR 0003); keep `RepoRelative`'s existing null-degrade-on-escape contract intact.

**Ask First:** None — all 4 fixable items have an owner-endorsed "fix direction" already written into their deferred-item text.

**Never:** Re-litigate items already closed with a recorded decision (e.g. the 6.11 re-branding/full-`GenerateAll` items, the `busyTerminals`-precise-correlation tradeoff) — re-affirm only.

</frozen-after-approval>

## Code Map

- `extension/src/extension.ts` -- `quoteCommandArg` (platform-aware quoting), `multiRootNoticeShownForFolder` + `bindWorkspace` (re-fire on bound-folder change)
- `src/SpecScribe/PathUtil.cs` -- new `ResolveRealPath` (per-segment symlink resolution)
- `src/SpecScribe/SiteGenerator.cs` -- `RepoRelative` now resolves both sides through `ResolveRealPath`
- `_bmad-output/implementation-artifacts/deferred-work.md` -- ledger updates for all 17 items

## Tasks & Acceptance

**Execution:**
- [x] `extension/src/extension.ts` -- switch `quoteCommandArg` to platform-aware escaping (doubled `"` on win32, backslash elsewhere) -- fixes the PowerShell-incompatibility the item names directly
- [x] `extension/src/extension.ts` -- replace the `multiRootNoticeShown` boolean latch with `multiRootNoticeShownForFolder` (URI-keyed), re-firing on bound-folder identity change and resetting on drop-to-single-root -- matches the item's own "track bound identity, not just count" ask
- [x] `src/SpecScribe/PathUtil.cs` -- add `ResolveRealPath`, walking a path root-to-leaf resolving each existing segment's symlink target
- [x] `src/SpecScribe/SiteGenerator.cs` -- route `RepoRelative`'s repo-root and target through `ResolveRealPath` before the relative-path math (repo-root cached once per instance)
- [x] `tests/SpecScribe.Tests/PathUtilTests.cs` -- pin `ResolveRealPath`'s no-symlink pass-through and its symlinked-ancestor-directory resolution (skippable if symlink creation isn't permitted on the host)
- [x] `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` -- integration regression: a symlink-aliased `RepoRoot` against the artifact's real path still yields the true relative `sourcePath` instead of degrading to null
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- close items 1/3/17 as RESOLVED with the fix detail; re-review item 2 (`busyTerminals` scope) and re-affirm the existing bounded/non-corrigible tradeoff with no code change; confirm items 4-16 (already closed/re-affirmed) still hold

**Acceptance Criteria:**
- Given a `toolPath` containing both a space and an embedded `"`, when staged on a PowerShell/cmd-style Windows terminal profile, then the emitted command line double-quotes the embedded quote (`""`) instead of backslash-escaping it
- Given the same `toolPath`, when staged on a bash-family Windows terminal profile (Git Bash/WSL), then the emitted command line still backslash-escapes the embedded quote (doubling would silently drop it, since adjacent quoted strings concatenate in bash)
- Given a multi-root workspace whose bound (`workspaceFolders[0]`) folder changes, when `bindWorkspace` re-runs, then the notice fires again naming the new bound folder (not silently staying on the stale name)
- Given a `RepoRoot` reachable via a symlink alias while an artifact's absolute path is the real (non-aliased) path, when `RepoRelative` computes the source path, then it returns the true repo-relative string instead of degrading to null
- Given the existing null-degrade-on-escape behavior (misconfigured `RepoRoot`, different drive), when `RepoRelative` runs, then it still returns null exactly as before (no regression)

## Spec Change Log

- **2026-07-22, review loop 1 (patch findings, no loopback needed):** Blind Hunter + Edge Case Hunter independently found that the first `quoteCommandArg` pass keyed doubled-quote escaping off `process.platform === 'win32'` alone, which silently drops the embedded quote on a bash-family Windows terminal profile (Git Bash/WSL) instead of escaping it. Amended to detect the actual configured `terminal.integrated.defaultProfile.windows` (new `usesPosixStyleQuoting()`), defaulting to PowerShell-style only when the profile isn't bash-like. Also patched: an inaccurate "classic Windows argv convention" doc-comment claim, an inaccurate "broken link" claim on `ResolveRealPath`'s catch blocks (a dangling symlink degrades earlier via `Directory.Exists`/`File.Exists`, never reaching those catches), a missing test for a symlink partway down an artifact path (only "root is the link" was covered), and the multi-root notice's folder-identity check (now realpath-normalized via new `tryRealpath`, so two paths to the same real folder don't read as different identities). KEEP: the `ResolveRealPath` per-segment-walk design and the `RepoRelative` caching approach both held up under review — not touched.

## Verification

**Commands:**
- `dotnet build` -- 0 errors, 0 warnings (confirmed; briefly blocked earlier in this session by an unrelated concurrent in-flight edit to `Charts.cs`/`GitInsightsTemplater.cs` on this shared `main` working tree, outside this spec's file set — resolved once that landed)
- `dotnet test` (full suite) -- **2007 passed, 3 skipped, 0 failed.** The 3 skips are the new symlink-creation-gated `[SkippableFact]` tests, confirmed skipping (not failing) on this session's own non-elevated Windows host
- `cd extension && npm run typecheck` -- 0 errors, confirmed green after both the initial implementation and the post-review patches

## Suggested Review Order

**Symlink-aware repo-relative paths (C#)**

- The actual fix: both the repo root and the artifact path now resolve through `ResolveRealPath` before the relative-path math.
  [`SiteGenerator.cs:2436`](../../src/SpecScribe/SiteGenerator.cs#L2436)

- New per-segment symlink resolver — the C#-side equivalent of the TS `fs.realpathSync` containment guard.
  [`PathUtil.cs:49`](../../src/SpecScribe/PathUtil.cs#L49)

- Repo root is resolved once and cached per generator instance, not recomputed per artifact.
  [`SiteGenerator.cs:2445`](../../src/SpecScribe/SiteGenerator.cs#L2445)

**Cross-shell terminal quoting (TS)**

- The corrected fix: quoting choice now comes from the configured terminal profile, not raw OS platform.
  [`extension.ts:1265`](../../extension/src/extension.ts#L1265)

- Consumer of the above — unchanged call shape, only the escaping source changed.
  [`extension.ts:1280`](../../extension/src/extension.ts#L1280)

**Multi-root notice re-fire (TS)**

- Folder-identity check now realpath-normalized so two paths to the same real folder don't double-fire.
  [`extension.ts:312`](../../extension/src/extension.ts#L312)

- New realpath helper backing the identity check above.
  [`extension.ts:283`](../../extension/src/extension.ts#L283)

**Tests**

- Symlinked-ancestor-directory resolution, plus the post-review addition for a symlink partway down the tree.
  [`PathUtilTests.cs:61`](../../tests/SpecScribe.Tests/PathUtilTests.cs#L61) · [`PathUtilTests.cs:93`](../../tests/SpecScribe.Tests/PathUtilTests.cs#L93)

- Integration regression: a symlink-aliased `RepoRoot` against the artifact's real path.
  [`SiteGeneratorWebviewTests.cs:341`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L341)

- The full ledger: 4 items resolved (1 hardened after review), 1 re-affirmed, 4 new residual items logged, 13 confirmed already closed.
  [`deferred-work.md:5`](deferred-work.md#L5)
