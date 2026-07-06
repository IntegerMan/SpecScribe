---
title: 'Home Next Steps: drop module label and surface pending code reviews'
type: 'feature'
created: '2026-07-06'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '8ca412d83066bc7ebc0d97ed0f9f87da696f029e'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The "Next Steps" panels render their heading as `Next Steps (BMad Method)` — the module label is noise. Separately, the home/project panel deliberately omits code-review, so a reader can't see that a story is sitting in `review` status awaiting an adversarial review from the dashboard.

**Approach:** Drop the `({ModuleLabel})` suffix from the shared Next Steps heading (affects home, epic, and story panels uniformly). In the project-level suggestions, add one `/bmad-code-review` (module-appropriate) entry for each story currently in `review` status, naming the story so the reviewer knows which change to review.

## Boundaries & Constraints

**Always:** Use `commands.Command("code-review")` so the module's own prefix is honored (`/bmad-code-review` for BMad Method, the GDS equivalent otherwise); route the "awaiting review" test through `StatusStyles.ForStory(s) == "review"` — the single source of truth for status. Omit the code-review entry when the active module doesn't expose that command (reuse the existing `Add` null-drop). Keep the heading text exactly `Next Steps`.

**Ask First:** (none — decisions resolved: remove the label on every panel, not just home.)

**Never:** Do not add code-review to the per-epic or per-story next steps (those already handle it). Do not add a story-id argument to the code-review command — `module-help.csv` gives it no `args`. Do not change status vocabulary or `StatusStyles`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Any Next Steps panel | any suggestions | Heading is `<h3>Next Steps</h3>` — no `(BMad Method)` suffix | N/A |
| Home, one story in `review` | EpicsModel with a story whose status maps to `review`; catalog exposes code-review | Project Next Steps list includes a `/bmad-code-review` item whose description names that story | N/A |
| Home, multiple stories in `review` | several review-status stories | One code-review item per review story, each naming its story | N/A |
| Home, no stories in `review` | no review-status stories | No code-review item appears among project suggestions | N/A |
| Module lacks code-review command | catalog without `code-review` key | Code-review item omitted; no broken command rendered | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/BmadCommands.cs` -- `RenderInner` builds the heading (line ~42); `ForProject` (line ~166) builds the home-page suggestions and currently excludes code-review by design.
- `src/SpecScribe/StatusStyles.cs` -- `ForStory` returns `"review"` for a story awaiting code review; reuse it, don't re-derive.
- `src/SpecScribe/ModuleContext.cs` -- `CommandCatalog.Command("code-review")` yields the module-correct slash command or null.
- `tests/SpecScribe.Tests/ModuleContextTests.cs` -- holds `BmadCommandsTests`; the `Next Steps (BMad Method)` assertion at line 138 must change, and new project-level tests belong here.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/BmadCommands.cs` -- In `RenderInner`, change the heading to `<h3>Next Steps</h3>` (remove the `({ModuleLabel})` interpolation). In `ForProject`, before the existing front-line logic, iterate `model.Epics.SelectMany(e => e.Stories)`, and for each story where `StatusStyles.ForStory(s) == "review"`, `Add(...)` a code-review suggestion described like `"Story {s.Id} is awaiting code review — adversarial multi-layer review of its changes."`. Update the `ForProject` doc comment so it no longer claims code review is excluded here.
- [x] `tests/SpecScribe.Tests/ModuleContextTests.cs` -- Update the story-level test: replace `Assert.Contains("Next Steps (BMad Method)", html)` with `Assert.Contains("Next Steps", html)` plus `Assert.DoesNotContain("(BMad Method)", html)`. Add `RenderProjectNextSteps` tests (with a small EpicsModel/EpicInfo helper modeled on `ChartsTests`): (a) a `review`-status story yields a `/bmad-code-review` item naming that story; (b) no review story yields no code-review item; (c) an empty-of-code-review catalog omits it.

**Acceptance Criteria:**
- Given any populated Next Steps panel, when rendered, then the heading is exactly `Next Steps` with no module-label suffix on any page.
- Given the home page with a story in `review` status and a module exposing code-review, when the project Next Steps render, then a `/bmad-code-review` entry appears whose text identifies the awaiting-review story.
- Given the home page with no story in `review` status, when the project Next Steps render, then no code-review entry appears.
- Given a module whose catalog lacks the code-review command, when a review story exists, then no code-review entry is rendered (no broken command).

## Verification

**Commands:**
- `dotnet build SpecScribe.slnx` -- expected: builds with no new warnings.
- `dotnet test SpecScribe.slnx` -- expected: all tests pass, including the updated and new `BmadCommandsTests`.

## Suggested Review Order

**Code-review prompts on the home panel (the feature)**

- Entry point: the loop that surfaces one named review prompt per story sitting in `review`.
  [`BmadCommands.cs:174`](../../src/SpecScribe/BmadCommands.cs#L174)

- Reuses `StatusStyles.ForStory` "review" as the single source of truth for "awaiting review".
  [`StatusStyles.cs:16`](../../src/SpecScribe/StatusStyles.cs#L16)

- `Add` drops the suggestion when the module exposes no code-review command — no broken command rendered.
  [`BmadCommands.cs:53`](../../src/SpecScribe/BmadCommands.cs#L53)

**Heading label removal**

- Shared heading now renders bare `Next Steps`; `commands` plumbing dropped from the private renderers.
  [`BmadCommands.cs:42`](../../src/SpecScribe/BmadCommands.cs#L42)

- Stale doc corrected: `ModuleLabel` no longer drives the heading (kept as parsed metadata).
  [`ModuleContext.cs:29`](../../src/SpecScribe/ModuleContext.cs#L29)

**Tests**

- Single review story yields a named `/bmad-code-review`; none when absent; none when command missing.
  [`ModuleContextTests.cs:170`](../../tests/SpecScribe.Tests/ModuleContextTests.cs#L170)

- Multi-review: one prompt per story, ordered before the front-line dev-story prompt.
  [`ModuleContextTests.cs:190`](../../tests/SpecScribe.Tests/ModuleContextTests.cs#L190)

- Heading assertion updated to require `Next Steps` and forbid `(BMad Method)`.
  [`ModuleContextTests.cs:138`](../../tests/SpecScribe.Tests/ModuleContextTests.cs#L138)
