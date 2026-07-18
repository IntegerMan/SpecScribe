---
title: 'Story 3.1 deferred — rename ranking, pulse labels, last-commit time, aria, comment CSS, TryCompute e2e'
type: 'bugfix'
created: '2026-07-18T16:34:10-04:00'
status: 'done'
baseline_commit: '5f4bca0'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Six open Story 3.1 review deferrals still leave rename-split file rankings, unlabeled mismatched pulse windows, order-fragile last-commit time, file-bar a11y gaps, unresolved comment-CSS scope risk, and no real-git `TryCompute` wiring test.

**Approach:** Close all six under this spec: `-M` + rename collapse for top files (keep Ordinal keys); label the 200-commit file window; pick max time on the last day; unify file-bar `aria-label`; document intentional global `.md-comment` hosts; add a temp-repo `TryCompute` happy-path test; mark every bullet RESOLVED.

## Boundaries & Constraints

**Always:**
- Name-only `TryCompute` call gains `-M` (or `--find-renames`); `ParseChangedFiles` collapses `old => new` / brace-abbrev forms via existing `ResolveRenamedPath` (same as numstat). Path keys stay `StringComparer.Ordinal` — case-only splits remain (consistent with the rest of the git layer; do not switch to IgnoreCase).
- Git Pulse title (and empty copy if it implies a shared “recent” window) names the file window as last 200 commits; do not change `-n 200` or the 30-day calendar signal.
- `LastCommitTimestamp` uses the maximum parseable `HH:mm` on the last series day (order-independent); invariant parse; midnight fallback unchanged.
- Each `GitPulsePanel` file-bar `<li>` gets `aria-label="{path}: {n} change(s)"` matching visible count text (ProgressBar pattern). Scope is GitPulsePanel only.
- `.md-comment` / `.md-comment-inline` stay global; strengthen the CSS comment to list known emission hosts (`.doc-body`, story cards/leads, AC/inline fragments) and the `.md-table` rationale. No selector narrowing that drops card/AC styling.
- Add a focused temp-git `GitMetrics.TryCompute` test (copy SiteGenerator* `RunGit` / `user.name` / `commit.gpgsign=false` idiom; assert if git missing — no silent skip) proving `LastCommitTimestamp`, `Last30DayCommitCount`, and `TopChangedFiles` wire from a real repo. Second-call degrade stays the reviewed ternary; no new `RunGit` seam.
- Mark all six story-3-1 deferred bullets RESOLVED citing this spec key (case half: accepted Ordinal consistency).

**Ask First:**
- Switching top-file keys to `OrdinalIgnoreCase`.
- Unifying the file window with the 30-day calendar window.
- Injectable `RunGit` seam solely to unit-test name-only failure.
- Propagating the same `aria-label` to `HotspotBars`.

**Never:**
- Merge baseline name-only and deep `--numstat` into one call (Story 3.2 two-call deferral).
- People-ranking / contributor leaderboards from pulse data.
- Re-scope comment CSS to `.doc-body` alone.
- Retune muted comment contrast / brand tokens in this pass.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Rename commit | name-only/`-M` line `a => b` or brace form | one count bump under resolved new path | skip malformed |
| Out-of-order times | last day commits `10:00` then `14:00` in list | timestamp uses 14:00 | midnight if none parse |
| File bars | path `src/A.cs`, 3 changes | `aria-label="src/A.cs: 3 changes"` on `<li>` | N/A |
| Window label | populated pulse | title mentions last 200 commits; 30-day signal unchanged | N/A |
| TryCompute e2e | temp repo with ≥2 commits touching files | non-null pulse; 30-day ≥1; TopChangedFiles non-empty; last timestamp on last day | Assert.Fail if git unavailable |
| Name-only null | second `RunGit` returns null (code path) | empty TopChangedFiles; rest of pulse kept | degrade, never null pulse |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/GitMetrics.cs` -- `TryCompute` name-only cmd (~214); `ParseChangedFiles` (~316); `LastCommitTimestamp` (~287); `ResolveRenamedPath` (~864)
- `src/SpecScribe/Charts.cs` -- `GitPulsePanel` title/empty + file-bar `<li>` (~1049–1106)
- `src/SpecScribe/assets/specscribe.css` -- `.md-comment` / `.md-comment-inline` (~926–942)
- `tests/SpecScribe.Tests/GitMetricsTests.cs` -- `ParseChangedFiles_*`; add rename + max-time + TryCompute e2e
- `tests/SpecScribe.Tests/ChartsTests.cs` -- `GitPulsePanel_*` label + aria pins
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` -- dashboard `"Top changed files"` string assert
- `tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs` -- temp-git `RunGit` idiom to copy
- `_bmad-output/implementation-artifacts/deferred-work.md` -- story-3-1 bullets (~347–355)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/GitMetrics.cs` + `GitMetricsTests.cs` -- `-M` + `ResolveRenamedPath` in ParseChangedFiles; max-time LastCommitTimestamp; temp-repo TryCompute wiring test; pin rename/out-of-order/e2e
- [x] `src/SpecScribe/Charts.cs` + `ChartsTests.cs` + `HtmlTemplaterTests.cs` -- 200-commit title/empty copy; file-bar aria-label; update string asserts
- [x] `src/SpecScribe/assets/specscribe.css` -- document intentional global comment hosts (no selector drop)
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- RESOLVED all six story-3-1 bullets citing this spec

**Acceptance Criteria:**
- Given a rename under `-M`, when ranking top files, then frequency counts under the new path once (Ordinal keys).
- Given commits on the last day in any list order, when computing last-commit time, then the latest parseable HH:mm wins.
- Given a Git Pulse with file bars, when rendered, then each row has a unifying aria-label and the files title names the 200-commit window beside the unchanged 30-day signal.
- Given a real temp git repo, when `TryCompute` runs, then the three Story 3.1 fields are populated from live git output.
- Given the six deferred bullets, when this land, then each is marked RESOLVED under the existing story-3-1 section.

## Spec Change Log

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~GitMetrics|FullyQualifiedName~ChartsTests|FullyQualifiedName~HtmlTemplaterTests"` -- expected: all pass
- `dotnet test` -- expected: full suite green

## Suggested Review Order

**Rename ranking (`-M`)**

- Bounded name-only call gains `-M` so rename commits count under the destination path
  [`GitMetrics.cs:216`](../../src/SpecScribe/GitMetrics.cs#L216)

- Defensive arrow/brace collapse + Ordinal keys; trim after resolve
  [`GitMetrics.cs:347`](../../src/SpecScribe/GitMetrics.cs#L347)

**Last-commit time**

- Max parseable HH:mm on last day; empty-series guard for the public helper
  [`GitMetrics.cs:290`](../../src/SpecScribe/GitMetrics.cs#L290)

**Pulse labels + a11y**

- Title/empty copy name the 200-commit window beside the unchanged 30-day signal
  [`Charts.cs:1091`](../../src/SpecScribe/Charts.cs#L1091)

- Unifying `aria-label` on each file-bar row; decorative track hidden
  [`Charts.cs:1108`](../../src/SpecScribe/Charts.cs#L1108)

**Comment CSS**

- Keep global selectors; document intentional emission hosts
  [`specscribe.css:924`](../../src/SpecScribe/assets/specscribe.css#L924)

**Tests + ledger**

- Temp-repo `TryCompute` pins the three Story 3.1 fields with exact counts
  [`GitMetricsTests.cs:869`](../../tests/SpecScribe.Tests/GitMetricsTests.cs#L869)

- Six story-3-1 bullets marked RESOLVED; three residual review deferrals appended
  [`deferred-work.md:1`](./deferred-work.md#L1)
