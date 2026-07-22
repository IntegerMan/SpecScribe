---
title: 'Story 6.10 deferred-work cleanup: RepoRelative escape hardening'
type: 'bugfix'
created: '2026-07-21'
status: 'done'
route: 'one-shot'
---

# Story 6.10 deferred-work cleanup: RepoRelative escape hardening

## Intent

**Problem:** Two open deferred-work items for Story 6.10 (`6-10-editor-artifact-bridges-reveal-source.md`): (1) `SiteGenerator.RepoRelative` silently returned the input path unchanged (still absolute) instead of a repo-relative path when the target had no common root with `RepoRoot`, violating the "always repo-relative" contract the VS Code extension's `resolveWorkspacePath` containment guard depends on; (2) a standalone `- source_spec: ...` bullet in `deferred-work.md`'s 6-10 section was rendering as its own spurious, content-free deferred-work item — a known `DeferredWorkParser` artifact already annotated (but not fixed) elsewhere in the same file.

**Approach:** Made `RepoRelative` return `string?` — null whenever the computed path is rooted or escapes upward — via a new pure, directly-testable `PathUtil.EscapesRepoRoot` helper (segment-boundary check, not a bare substring match, so an in-repo path merely named like an up-level segment isn't misclassified). Centralized this into the one shared method so all three call sites degrade identically. Resolved the spurious deferred-work item by folding it into the real finding as a single structured `source_spec`/`summary`/`evidence` entry, matching the schema an in-flight cleanup pass is already rolling out elsewhere in the file — no parser code change, per the owner's explicit choice to keep this narrowly scoped.

## Suggested Review Order

**Escape-detection fix**

- The new pure predicate: checks the leading path segment, not a bare `..` substring, so `..cache/notes.md` isn't misclassified as an escape.
  [`PathUtil.cs:31`](../../src/SpecScribe/PathUtil.cs#L31)

- `RepoRelative` now returns `string?`, degrading to null (button hidden) instead of silently shipping an absolute/escaping path.
  [`SiteGenerator.cs:2421`](../../src/SpecScribe/SiteGenerator.cs#L2421)

- `BuildCapturedSourceMap.Add`'s local guard simplified to rely on the now-centralized check instead of duplicating it.
  [`SiteGenerator.cs:2616`](../../src/SpecScribe/SiteGenerator.cs#L2616)

**Test coverage**

- Direct unit coverage of the escape predicate itself, including the `..cache` false-positive guard the fix targets.
  [`PathUtilTests.cs:37`](../../tests/SpecScribe.Tests/PathUtilTests.cs#L37)

- Integration regression: a misconfigured `RepoRoot` degrades all three call sites (incl. `CapturePages`) to a null `sourcePath`, through JSON serialization.
  [`SiteGeneratorWebviewTests.cs:298`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L298)

**Deferred-work bookkeeping**

- The 6-10 entry folded into one structured item and marked hardened, with an honest note on which branch (cross-drive) has no automated test.
  [`deferred-work.md:573`](../../_bmad-output/implementation-artifacts/deferred-work.md#L573)

- New deferred item from this cleanup's own adversarial review: `RepoRelative` stays symlink-unaware, unlike the TS-side containment guard it feeds — out of scope for this narrow fix.
  [`deferred-work.md:819`](../../_bmad-output/implementation-artifacts/deferred-work.md#L819)
