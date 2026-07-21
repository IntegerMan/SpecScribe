---
title: 'Story 7.5 deferred-debt cleanup'
type: 'bugfix'
created: '2026-07-21T02:00:00-04:00'
status: 'done'
review_loop_iteration: 0
context: []
route: 'one-shot'
---

# Story 7.5 deferred-debt cleanup

## Intent

**Problem:** Story 7.5's code review left three open items in `deferred-work.md`: a malformed entry (a bare `source_spec:` line with no `summary:`/`evidence:` keys, followed by two freeform bullets, unlike every other entry in the file); git-dependent tests in `SiteGeneratorCommitDetailsTests.cs` that hard-fail (`Assert.True`) instead of skipping when git is unavailable on the host; and the determinism test's footer-stripping regex hardcoding a signed `UTC[+-]\d{2}:\d{2}` offset shape that would silently degrade into an un-normalized string compare if `PortalDates`' zone-label format ever shifted.

**Approach:** Reformatted the malformed entry into two properly-keyed `summary:`/`evidence:` entries matching the file's convention. Added the `Xunit.SkippableFact` package and converted the six git-gated `[Fact]`s to `[SkippableFact]`. Split the environment probe from fixture setup: a new `GitAvailable()` (`git --version`) drives `Skip.IfNot`, while `TryCreateGitHistory()` — only called once git is confirmed present — still hard-fails via `Assert.True` if the fixture's own `init`/`commit` steps break, so a real regression in the test harness can't be silently swallowed into a Skip. Generalized the determinism regex's zone-label token to `[\w+\-:]+` instead of the hardcoded signed-offset shape. All three items marked RESOLVED in `deferred-work.md`, scoped to `SiteGeneratorCommitDetailsTests.cs` only — six sibling files share the same hard-fail pattern and three share the same regex, left open as documented, separate debt.

## Code Map

- `tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs` — `[Fact]` → `[SkippableFact]` on 6 git-gated tests; new `GitAvailable()`; generalized determinism regex
- `tests/SpecScribe.Tests/SpecScribe.Tests.csproj` — added `Xunit.SkippableFact` 1.4.13
- `_bmad-output/implementation-artifacts/deferred-work.md` — story-7-5 section (~645–652), all 3 items resolved

## Suggested Review Order

**Skip vs. hard-fail split**

- `GitAvailable()` gates `Skip.IfNot`; `TryCreateGitHistory()` still hard-fails on a genuine fixture-setup break once git is confirmed present.
  [`SiteGeneratorCommitDetailsTests.cs:241`](../../tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs#L241)

**Determinism regex generalization**

- Zone-label token matched generically instead of assuming the current signed `UTC±HH:MM` shape.
  [`SiteGeneratorCommitDetailsTests.cs:218`](../../tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs#L218)

**Tracking**

- All three story-7-5 deferrals struck through and resolved, scope of the still-open sibling-file instances noted.
  [`deferred-work.md:645`](./deferred-work.md#L645)
