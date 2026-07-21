---
title: 'Story 7.2 deferred-work cleanup: code-reference resolution hardening'
type: 'bugfix'
created: '2026-07-20'
status: 'done'
review_loop_iteration: 0
baseline_commit: 'c3d7fee18f0a7afed9d26a9dd95f82e98fe95f2d'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 7.2's code-review (2026-07-13) parked 4 findings in `deferred-work.md` about
`CodeReferenceLinkifier`/`SiteGenerator`'s `[Source: …]` code-citation resolver: (1) `IsRelativeCodeHref`
requires an extension on the last path segment and never percent-decodes, so extensionless files
(`Dockerfile`, `Makefile`, `LICENSE`) and encoded paths (`%20`) silently degrade to plain text; (2) `_codePages`
entries pruned after a code-page write failure/escape leave already-rendered citing pages holding a dead link;
(3) `IsRelativeCodeHref` is unscoped to genuine citations, so an unrelated relative link (image/asset) with a
non-`.html`/`.md` extension can be silently stripped to plain text if it doesn't resolve; (4) `BuildReferencedBy`'s
doc comment now claims a citer missing from `_referenceMap` is "omitted rather than guessed", but the code was
reverted back to the guessing fallback during the same review — the comment misdescribes the shipped behavior.

**Approach:** Fix what's safely fixable (1, 4) with small, scoped, no-regression patches. Re-verify (2) against
the current discovery code and close it with corrected, narrower evidence (the "output escapes root" prune branch
is provably unreachable given how `repoRel` is derived; only a genuine mid-run I/O exception can still trigger a
prune, an accepted rare race matching this codebase's existing precedent for similar findings) — no code change.
Leave (3) deferred as-is: two prior fix attempts were already rejected against the real corpus/tests in the
original review, and a safe fix needs citation context threaded from the markdown-parse stage, which is out of
scope for this cleanup pass.

## Boundaries & Constraints

**Always:**
- Fix (1) in `CodeReferenceLinkifier.cs` only: percent-decode the href/path candidate before matching, and accept
  a documented set of conventional extensionless filenames (`Dockerfile`, `Makefile`, `Rakefile`, `Gemfile`,
  `Procfile`, `Vagrantfile`, `LICENSE`, `README`, `CHANGELOG`, `CONTRIBUTING`, `NOTICE` — case-insensitive, no
  extension) as a valid last path segment.
- Fix (4) by correcting the doc comment on `SiteGenerator.BuildReferencedBy` (~line 1750-1756) to describe the
  actual kept behavior (guess via `PathUtil.ToOutputRelative` when missing from `_referenceMap`) instead of the
  reverted "omit" patch.
- Update `deferred-work.md`'s story-7.2 section: mark (1) and (4) resolved with a short pointer to this spec;
  narrow (2)'s evidence to the reconfirmed, smaller residual risk; leave (3) exactly as-is.
- Preserve every existing `CodeReferenceLinkifierTests` behavior byte-for-byte (bare-path citations, `../`-relative
  hrefs, unresolved-degrades-to-plain-text, idempotency, escaping).

**Ask First:** None anticipated — both fixes are additive/narrowing and match precedent already reasoned through
in the original review.

**Never:**
- Do not touch `IsRelativeCodeHref`'s extension-gate philosophy beyond the named allow-list (no general removal of
  the "must look like a file" check — that reopens finding (3)'s over-matching risk).
- Do not attempt a second linkification pass or reorder code-page generation ahead of story/doc/ADR pages for (2)
  — both were evaluated in the original review and are disproportionate to the residual (already-narrow) risk.
- Do not change `BuildReferencedBy`'s actual fallback behavior — the original review already tried "omit" and
  reverted it after a confirmed test regression.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Extensionless view-source href | `<a href="../../Dockerfile">Dockerfile</a>`, `Dockerfile` in codePages map | Resolves to its code page | N/A |
| Extensionless inline citation | `[Source: Makefile:3]`, `Makefile` in codePages map | Resolves with `#L3` | N/A |
| Percent-encoded href | `<a href="../../src/My%20File.cs">My File.cs</a>`, `src/My File.cs` in codePages map | Resolves to its code page | N/A |
| Still-extensionless, still unmapped | `<a href="../../overview">overview</a>`, no codePages entry | Left exactly as the original anchor (not a candidate — no dot, not on the allow-list) | N/A |
| Unrelated extensioned asset (finding 3, unchanged) | `<a href="assets/logo.png">logo</a>`, not in codePages map | Degrades to plain text (pre-existing, documented, still deferred) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/CodeReferenceLinkifier.cs` -- `IsRelativeCodeHref` (extension/allow-list + percent-decode), `RewriteHrefs`/`RewriteInline` (decode the candidate before matching).
- `src/SpecScribe/SiteGenerator.cs` -- `BuildReferencedBy` doc comment only (~line 1750-1756); no behavior change.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- close (1) and (4), narrow (2)'s evidence, leave (3) untouched.
- `tests/SpecScribe.Tests/CodeReferenceLinkifierTests.cs` -- add regression coverage for the extensionless allow-list and percent-decoding.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/CodeReferenceLinkifier.cs` -- Add a small `ExtensionlessAllowList` (`HashSet<string>`, ordinal-ignore-case) of conventional no-extension filenames; in `IsRelativeCodeHref`, accept the last segment when it's on the allow-list even without a `.`. Percent-decode the href in `RewriteHrefs` and the raw path in `RewriteInline` (via `Uri.UnescapeDataString`, wrapped in a `SafeUnescape` helper) before running `StripLocator`/the extension check/dictionary lookup. **Done.**
- [x] `src/SpecScribe/SiteGenerator.cs` -- Rewrite the `BuildReferencedBy` doc comment to describe the actual guess-fallback behavior (why it's correct for ordinary docs, why entities with a non-naive output path are always overridden in `_referenceMap`) instead of the reverted "omit" wording. **Done.**
- [x] `tests/SpecScribe.Tests/CodeReferenceLinkifierTests.cs` -- Add cases: extensionless href resolves via the allow-list; an extensionless, non-allow-listed candidate (`overview`) is still left untouched; percent-encoded href and inline citation resolve against a codePages key with a literal space. **Done** (4 new tests, all pass; 1893/1893 full suite green).
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Under "Deferred from: code review of story-7.2 (2026-07-13)": mark findings 1 and 4 `RESOLVED — see spec-7-2-deferred-work-cleanup.md`; replace finding 2's evidence with the reconfirmed narrower risk; leave finding 3 as-is. **Done.**

**Acceptance Criteria:**
- Given a citation to an extensionless conventional filename that has a generated code page, when the site renders, then the citation becomes a working link.
- Given a citation whose href/path is percent-encoded and matches a real codePages key once decoded, when the site renders, then the citation resolves.
- Given every existing `CodeReferenceLinkifierTests` case, when the suite runs after this change, then all pass unmodified (no behavior regression for `.html`/`.md`/extensioned/unresolved citations).
- Given `BuildReferencedBy`'s doc comment, when read after this change, then it accurately describes the shipped fallback behavior with no reference to a reverted patch.

## Design Notes

Finding (2)'s "output escapes root" prune branch (`SiteGenerator.cs` GenerateCodePagesInternal) is provably
unreachable today: `repoRel` is always produced by `CodeReferenceScanner`'s `TryResolvePath` via
`Path.GetRelativePath(repoFull, full)` after an `IsInside(full, repoFull)` check, so it can never contain a `..`
segment — `code/{repoRel}.html` therefore can never climb above `OutputRoot`. The only real residual prune trigger
is the `catch (Exception ex)` branch around the per-file render/write (genuine I/O failure between discovery's
`File.Exists` check and the actual read/write) — a true but very rare TOCTOU race, consistent with this codebase's
already-accepted precedent for the sibling-pager dead-link finding. No code change; deferred-work.md gets the
tighter evidence.

## Spec Change Log

- **Review loop 1 (patch findings, no loopback).** Blind Hunter + Edge Case Hunter independently converged on the
  same real gap: `SafeUnescape`'s `catch (FormatException)` guarded against a throw `Uri.UnescapeDataString`
  doesn't actually produce (verified directly against .NET 10 — malformed `%` sequences pass through unchanged,
  never throw). Removed the dead try/catch and corrected the doc comment; added a regression test
  (`MalformedPercentSequence_DoesNotThrowAndDegradesToPlainText`). Also patched: `ExtensionlessAllowList`'s
  case-insensitive comparer only governs the "is this a candidate" gate, not final resolution (still gated by the
  pre-existing case-sensitive `codePages`/repo-file lookup, unchanged by this spec) — clarified the code comment so
  this isn't mistaken for an end-to-end case-insensitivity guarantee. Tightened one new test's assertion
  (`Assert.Contains` → `Assert.Equal`) to match the file's existing convention. Reworded `deferred-work.md`'s
  finding (2) evidence to not overstate the residual risk as narrowly "an I/O exception" (the remaining catch is a
  broad `catch (Exception ex)`, not I/O-scoped) and to keep the still-present escape-check code path explicit
  rather than implying it was removed. KEEP: the core approach (allow-list + percent-decode, comment-only
  `BuildReferencedBy` fix, re-verified-not-refixed finding 2/3) was validated as correctly scoped — no loopback to
  step-02 was needed.
- **Pre-existing test failure, not caused by this change.** `SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`
  fails in this working tree, but the golden fixtures (`EpicsMd`/`Story11Md`/`Story21Md`) contain no `[Source: …]`
  citations, so this diff's `CodeReferenceLinkifier`/`SiteGenerator` changes are a provable no-op on them. The
  failure traces to unrelated, pre-existing uncommitted work already in the tree at session start (`Charts.cs`,
  `CodeMap.cs`, `CodeMapTemplater.cs`, `specscribe.css`, and their tests) — the same root cause the sibling spec
  `spec-7-2-source-citation-link-fidelity.md` already documented for an identical failure. Not regenerating the
  golden constant here; that in-flight work isn't this spec's to bake in.

## Suggested Review Order

**Extensionless-filename + percent-decode fix (finding 1)**

- Entry point: the allow-list gate itself, and why case-insensitivity here doesn't imply case-insensitive resolution.
  [`CodeReferenceLinkifier.cs:180`](../../src/SpecScribe/CodeReferenceLinkifier.cs#L180)

- The allow-list definition and its scoping comment.
  [`CodeReferenceLinkifier.cs:47`](../../src/SpecScribe/CodeReferenceLinkifier.cs#L47)

- Decode-before-match in the href (view-source link) path.
  [`CodeReferenceLinkifier.cs:97`](../../src/SpecScribe/CodeReferenceLinkifier.cs#L97)

- Decode-before-match in the inline/code-span citation path.
  [`CodeReferenceLinkifier.cs:129`](../../src/SpecScribe/CodeReferenceLinkifier.cs#L129)

- `SafeUnescape` simplified after confirming `Uri.UnescapeDataString` never throws for malformed input.
  [`CodeReferenceLinkifier.cs:205`](../../src/SpecScribe/CodeReferenceLinkifier.cs#L205)

**Stale doc comment fix (finding 4)**

- `BuildReferencedBy`'s comment now matches the shipped guess-fallback behavior instead of a reverted patch.
  [`SiteGenerator.cs:1761`](../../src/SpecScribe/SiteGenerator.cs#L1761)

**Deferred-work ledger updates (findings 1, 2, 4)**

- Findings 1 and 4 closed; finding 2 re-verified with narrower, more precise evidence; finding 3 left untouched.
  [`deferred-work.md:661`](deferred-work.md#L661)

**Regression tests (peripheral)**

- Extensionless allow-listed href resolves; non-allow-listed extensionless href still degrades.
  [`CodeReferenceLinkifierTests.cs:215`](../../tests/SpecScribe.Tests/CodeReferenceLinkifierTests.cs#L215)

- Percent-encoded href/inline citation resolve against a decoded key.
  [`CodeReferenceLinkifierTests.cs:237`](../../tests/SpecScribe.Tests/CodeReferenceLinkifierTests.cs#L237)

- Malformed `%` sequence doesn't throw and degrades gracefully.
  [`CodeReferenceLinkifierTests.cs:265`](../../tests/SpecScribe.Tests/CodeReferenceLinkifierTests.cs#L265)

## Verification

**Commands:**
- `dotnet build src/SpecScribe/SpecScribe.csproj -c Debug` -- expected: build succeeds.
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj -c Debug` -- expected: all tests pass, including new regression cases.
