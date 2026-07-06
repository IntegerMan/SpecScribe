# Deferred Work

Real-but-not-now items surfaced during reviews. Each is safe to leave; revisit when the related area is next touched.

## Deferred from: code review of story-1-2 (2026-07-06)

- **Case-insensitive requirement-ID matching is not actually implemented.** `RequirementLinkifier.RefPattern` (`\b(FR|NFR)(\d+)\b`, `src/SpecScribe/RequirementLinkifier.cs:17`) is compiled without `RegexOptions.IgnoreCase`, so a lowercase `fr6`/`nfr2` reference in an artifact is silently never linkified — even though `RequirementsModel.ById` lookup itself is case-insensitive. The story's "case-insensitive requirement lookup" must-preserve constraint is only half-true and has no test. Pre-existing behavior, not introduced by Story 1.2. Decide whether lowercase references should link (add `IgnoreCase` + a test) or whether uppercase-only is intended (document it) when this seam is next touched.
- **Multi-digit partial-token boundary is unpinned.** No test proves `FR60` does not match when only `FR6` is known (and vice-versa); the existing partial-token test covers only non-digit adjacency (`FR1x`, `XFR1`, `tests/SpecScribe.Tests/LinkifierTests.cs:61`). Behavior is correct today thanks to the greedy `\d+` inside `\b…\b`, but a dedicated regression pin would guard against a future `\d?`-style slip.
