# Deferred Work

Real-but-not-now items surfaced during reviews. Each is safe to leave; revisit when the related area is next touched.

## Deferred from: code review of story-1-2 (2026-07-06)

- **Case-insensitive requirement-ID matching is not actually implemented.** `RequirementLinkifier.RefPattern` (`\b(FR|NFR)(\d+)\b`, `src/SpecScribe/RequirementLinkifier.cs:17`) is compiled without `RegexOptions.IgnoreCase`, so a lowercase `fr6`/`nfr2` reference in an artifact is silently never linkified — even though `RequirementsModel.ById` lookup itself is case-insensitive. The story's "case-insensitive requirement lookup" must-preserve constraint is only half-true and has no test. Pre-existing behavior, not introduced by Story 1.2. Decide whether lowercase references should link (add `IgnoreCase` + a test) or whether uppercase-only is intended (document it) when this seam is next touched.
- **Multi-digit partial-token boundary is unpinned.** No test proves `FR60` does not match when only `FR6` is known (and vice-versa); the existing partial-token test covers only non-digit adjacency (`FR1x`, `XFR1`, `tests/SpecScribe.Tests/LinkifierTests.cs:61`). Behavior is correct today thanks to the greedy `\d+` inside `\b…\b`, but a dedicated regression pin would guard against a future `\d?`-style slip.

## Deferred from: code review of story-1-3 (2026-07-06)

- **Inconsistent `scroll-margin-top` offsets for sticky-nav clearance.** Three magic values target the same "land below the sticky nav" intent: `var(--nav-offset)` (3.75rem) on TOC-targeted sections (`src/SpecScribe/assets/specscribe.css:227`), `5rem` on `.ac-criterion` (`:686`), `4.5rem` on `.req-index .section-divider[id]` (`:1340`). Each still clears the nav, so no correctness bug — but if the nav height changes only the `var()`-driven anchors follow. Unify onto `var(--nav-offset)` when this CSS is next touched.
- **Diff carries changes unrelated to Story 1.3 scope.** The GitHub Pages deploy-retry workflow (`.github/workflows/publish-docs-live-pages.yml`) and `README.md` edits are harmless and match a prior commit's intent, but are not part of Story 1.3's ACs. Confirm they belong in this story's commit or split them out.

## Deferred from: code review of spec-home-next-steps-label-and-code-review (2026-07-06)

- source_spec: `spec-home-next-steps-label-and-code-review.md`
- **Retrospective fallback can mislabel a project that has review work.** In `BmadCommands.ForProject` (`src/SpecScribe/BmadCommands.cs`), the project "Next Steps" panel falls back to a project-wide retrospective suggestion when `suggestions.Count == 0`, printing "Every epic is drafted and every story detailed." If a module does **not** expose a `code-review` command (so the new review-prompt loop adds nothing) and a story is sitting in `review` status with no other actionable work (no ready/active story, no undrafted epic story, no pending epic), the fallback still fires — claiming the project is fully detailed while a change awaits review. Pre-existing behavior (review status never fed the home panel before this change) and narrow (needs a code-review-less module). Evidence: `suggestions.Count == 0` is the sole fallback guard and the review loop is a no-op when `Command("code-review")` is null. Guard the fallback on "no review work either" when this panel is next touched.
