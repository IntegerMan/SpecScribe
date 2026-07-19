---
title: 'Story 3.8 deferred-debt cleanup'
type: 'bugfix'
created: '2026-07-19'
status: 'in-review'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.8's code review deferred five real-but-not-now items (`deferred-work.md`): two silent-data-integrity edge cases in the deep-git numstat parser, one documented-but-untested churn/change-count divergence, one missing focus-management for a `:target`-revealed panel, and one missing regression test for stale-output removal.

**Approach:** Fix the two genuine parsing bugs (embedded control-char truncation, undated-commit count divergence) at the source; pin the intentional churn/dedup tradeoff and the stale-hub-removal guarantee with regression tests (no behavior change — both already worked); add script-driven focus management to the affected `:target` panel.

## Boundaries & Constraints

**Always:** Every fix stays inside `GitMetrics.cs`/`GitInsightsTemplater.cs`/`specscribe.js` (the files the deferred items named); no behavior change to any case not explicitly covered by the five items; production output stays byte-identical for every real-world commit (git always emits well-formed dates and never embeds control bytes in message text, so these are defensive-correctness fixes, not visible feature changes).

**Ask First:** None triggered during execution.

**Never:** Do not touch the sibling `:target` panels in the commit-heatmap/coupling-graph (explicitly called out in the deferred note as "inherited, not introduced by this story" — out of scope here).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Body has embedded 0x1F | Enriched numstat record whose body text contains a raw field-sentinel byte | All numstat rows still parsed; body reconstructed | N/A (never throws) |
| Commit has unparseable date | `DeepCommit` with `Timestamp: null` mixed with dated commits | `GitInsightsData.CommitCount` always equals `Activity`'s summed counts | N/A |
| Same path listed twice in one commit | Rename+modify numstat pair resolving to the same path | `Changes` counts once; `Added`/`Deleted` sum both rows (pinned, not changed) | N/A |
| File-contributors panel revealed via `:target` | User activates a file row's fragment link, JS enabled | Focus moves into the revealed `.gi-contributors-panel` | No-JS: panel still reveals via CSS, no focus jump |
| `--deep-git` later disabled | Prior run produced `git-insights.html`, next run omits `--deep-git` | Stale hub page removed (pinned via `GenerateAll`'s existing full output wipe) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/GitMetrics.cs` -- `ParseNumstatRecords` (item 1 fix), `BuildInsights` (item 2 fix + doc)
- `src/SpecScribe/GitInsightsTemplater.cs` -- `AppendContributorPanel` (item 4: `tabindex="-1"`)
- `src/SpecScribe/assets/specscribe.js` -- new `focusHashTarget` enhancement (item 4)
- `tests/SpecScribe.Tests/GitMetricsTests.cs` -- new tests for items 1, 2, 3
- `tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs` -- new test for item 5

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/GitMetrics.cs` -- rejoin body fields and take the LAST split segment as the numstat block -- an embedded 0x1F in a commit body no longer truncates the file set
- [x] `src/SpecScribe/GitMetrics.cs` -- derive `CommitCount` from `Activity`'s own summed counts instead of raw `commits.Count` -- the two totals can never structurally diverge
- [x] `tests/SpecScribe.Tests/GitMetricsTests.cs` -- pin the intentional churn-sums-every-row/changes-dedups-per-commit tradeoff -- closes the coverage gap without changing behavior
- [x] `src/SpecScribe/GitInsightsTemplater.cs` + `src/SpecScribe/assets/specscribe.js` -- `tabindex="-1"` on the contributor panel + a `hashchange`/load-time focus jump -- keyboard/AT users land on the revealed panel
- [x] `tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs` -- assert a stale `git-insights.html` is removed when a later run has `--deep-git` off -- closes the coverage gap (behavior already correct via `GenerateAll`'s full wipe)

**Acceptance Criteria:**
- Given a numstat record whose body contains a raw 0x1F byte, when parsed, then every numstat row is still present in `DeepCommit.Files`.
- Given a mix of dated and undated commits, when `BuildInsights` runs, then `CommitCount == Activity.Sum(a => a.Count)` always.
- Given a file-contributors panel revealed via `:target` with JS enabled, when the panel becomes visible, then it receives focus.
- Given a prior `--deep-git` run followed by one with `--deep-git` off into the same output dir, then `git-insights.html` no longer exists.

## Spec Change Log

(none — first pass)

## Verification

**Commands:**
- `dotnet build src/SpecScribe/SpecScribe.csproj` -- expected: succeeds, 0 errors
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~GitMetricsTests|FullyQualifiedName~SiteGeneratorGitInsightsTests|FullyQualifiedName~GitInsightsTemplaterTests"` -- expected: all pass (97/97, confirmed)

**Manual checks:**
- Ran `specscribe generate --deep-git` against this repo's own history; confirmed `git-insights.html` renders with `tabindex="-1"` on every `.gi-contributors-panel`.
- Full suite (`dotnet test`) is currently unreliable as a whole-repo signal: `main` has a large, uncommitted, in-progress feature from a concurrent session (Story 10.11-shaped work touching `HtmlTemplater.cs`/`StatusStyles.cs`/`SiteGenerator.cs`/etc.) that is shifting the `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` fingerprint independently of this change — it flipped pass/fail between two consecutive runs with no code of mine changing in between. Verified by grep that none of my edited symbols were touched by that concurrent work. Re-run the full suite once that session's work lands to get a clean signal; do not update the golden constant based on today's readings.
