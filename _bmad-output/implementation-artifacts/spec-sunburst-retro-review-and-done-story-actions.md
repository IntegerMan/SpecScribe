---
title: 'Retro-gated epic "In review" across visual surfaces + celebratory done-story actions pane'
type: 'feature'
created: '2026-07-10'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: 'bfad1b8cf2d28d571414e409dbdbbd9db81d7b11'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Two "done" states overclaim completion. (1) An epic whose every story is done reads as fully done across every epic-status surface (sunburst, Epic Status donut, epics-index chips, epic page/card badges) even when no retrospective has closed it out. (2) A `done` story still shows a "code review" suggestion in its actions pane, nagging a finished story instead of celebrating it.

**Approach:** (1) Add a retro-gated epic classifier: an all-stories-done epic with no retrospective renders in the existing "review" (deep-teal) tier instead of "done" on every *visual* epic-status surface, and the epic retro-suggestion guidance re-gates onto that state. Requirements roll-up math stays on plain `ForEpic` (implementation-completeness — retros must never affect whether a requirement reads as built). (2) Split the story actions pane so `done` stories drop code-review and render a positive "All done" panel with a checkmark and success styling; `review`/`in-progress` stories are unchanged.

## Boundaries & Constraints

**Always:**
- Keep `StatusStyles.ForEpic` as the implementation-completeness classifier. Add a SEPARATE `ForEpicWithRetrospective` for the retro-gated tier.
- Use the retro-gated classifier on the visual epic-status surfaces only: project sunburst, Epic Status donut, epics-index chips, epic page + epic-card badges, epic page StatusStage metadata, and the epic Next Steps / retro affordance.
- "Retro performed" = a parsed retrospective exists for that epic. `EpicInfo.HasRetrospective` and the epic/story pages' `epicRetroPath` MUST both derive from the same `_epicRetroMap` so they can never disagree.
- "Done story" = `StatusStyles.ForStory(story) == "done"`.
- Reuse existing tokens/glyphs: `--status-review`, `--status-done`, `Icons.ForStatus("done")` (checkmark), `Icons.ForStatus("review")`. No new color tokens.

**Ask First:**
- Retro-gating anything in the requirements family (roll-up math OR the requirement-flow epic chip) — both stay on plain `ForEpic` in this spec.

**Never:**
- Do not retro-gate `RequirementsParser.DeriveStatus` — an all-done-no-retro covering epic must still roll its requirement up to Done, never Planned.
- Do not add JS to the sunburst; do not change the sunburst legend set (already lists "In review").
- Do not alter `review` or `in-progress` story actions.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior |
|----------|--------------|---------------------------|
| Epic all-done, no retro | every story `done`, `HasRetrospective==false` | `ForEpicWithRetrospective`→`"review"`; sunburst `sb-review`, donut "review" bucket, chip/badge `.review` "In review" |
| Epic all-done, retro exists | every story `done`, `HasRetrospective==true` | `"done"` everywhere (unchanged) |
| Epic partially done / pending | any non-done story | `ForEpicWithRetrospective` == `ForEpic` (active/ready/drafted/pending) |
| Requirement over all-done-no-retro epic | covering epic all done, no retro | requirement still rolls up to **Done** (plain `ForEpic`, unaffected) |
| Epic retro suggestion — all-done-no-retro | `"review"` | Next Steps + affordance suggest `retrospective` |
| Epic retro suggestion — retro exists | `"done"` | no retrospective suggestion; affordance shows "View Retrospective" link |
| Done story actions pane | `ForStory=="done"` | "All done" panel: checkmark + success styling; NO code-review command |
| Review / in-progress story | status `review`/`in-progress` | code-review suggestion present (unchanged) |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/EpicsModel.cs` -- `EpicInfo`; add settable `HasRetrospective` (matches post-construction `Status`/`TasksDone`).
- `src/SpecScribe/SiteGenerator.cs` -- after `_epicsModel = bundle.Epics;` (~L118), set `epic.HasRetrospective = _epicRetroMap.ContainsKey(epic.Number)` per epic (same source `epicRetroPath` uses at L637).
- `src/SpecScribe/StatusStyles.cs` -- add `ForEpicWithRetrospective(EpicInfo)`; add `"review"` to `EpicStages` (after `"done"`) and `EpicLabel` (`"In review"`).
- `src/SpecScribe/Charts.cs` -- `Sunburst` (L153) → retro-gated class + `EpicLabel`.
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- Epic Status donut (L139) → retro-gated; `EpicStages` now yields a "review" bucket. Note: center number `Count("done")/total` no longer counts all-done-no-retro epics (intended).
- `src/SpecScribe/EpicsViewBuilder.cs` -- chip class (L33) + epic page `StatusClass` (L41) → retro-gated; `RenderRetroAffordance` re-gate: show the "capture the lessons" prompt when class is `"review"` (was `"done"`).
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` -- `AppendEpicCard` (L111) epic-card badge → retro-gated.
- `src/SpecScribe/EpicsTemplater.cs` -- epic page `StatusStage` metadata (L49) → retro-gated (keep it consistent with the badge).
- `src/SpecScribe/BmadCommands.cs` -- `ForEpic` (L251) re-gate: `"review"`→suggest `retrospective`, `"done"`→no suggestion. `ForStory` split `done`/`complete` (no suggestions) from `review` (code-review). `RenderNextSteps`: `done`→"All done" panel via `Icons.ForStatus("done")`.
- `src/SpecScribe/assets/specscribe.css` -- add `.epic-chip.review` + `.epic-status.review` (mirror `.status-badge.review` teal-deep); add success styling for the done actions panel near `.next-steps`/`.all-done-note`.
- Do NOT touch `RequirementsParser.cs:194` or `RequirementsTemplater.cs:162`.
- Tests: `StatusStylesTests.cs`, `ChartsTests.cs`, `HtmlTemplaterTests.cs` (donut/requirements), `ModuleContextTests.cs`.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/EpicsModel.cs` -- add `public bool HasRetrospective { get; set; }` to `EpicInfo` with a one-line comment.
- [x] `src/SpecScribe/SiteGenerator.cs` -- wire `HasRetrospective` from `_epicRetroMap` right after the epics model is cached.
- [x] `src/SpecScribe/StatusStyles.cs` -- add `ForEpicWithRetrospective` (returns `"review"` when `ForEpic==` `"done"` && `!HasRetrospective`, else `ForEpic`); add `"review"` to `EpicStages` and `EpicLabel`.
- [x] `src/SpecScribe/Charts.cs` + `HtmlRenderAdapter.Dashboard.cs` + `EpicsViewBuilder.cs` + `HtmlRenderAdapter.Epics.cs` + `EpicsTemplater.cs` -- swap the six visual-surface `ForEpic` sites to `ForEpicWithRetrospective`; re-gate `RenderRetroAffordance` onto `"review"`.
- [x] `src/SpecScribe/BmadCommands.cs` -- re-gate `ForEpic` retro suggestion (`"review"`→suggest, `"done"`→none); split `ForStory` done/review; `RenderNextSteps` renders the "All done" panel for done stories.
- [x] `src/SpecScribe/assets/specscribe.css` -- add `.epic-chip.review`, `.epic-status.review`, and success styling for the all-done actions panel.
- [x] Tests -- cover every I/O row, including the guard that a requirement over an all-done-no-retro epic still reads **Done**, and that the donut gains a "review" bucket.

**Acceptance Criteria:**
- Given an all-stories-done epic with no retro, when any visual epic-status surface renders (sunburst / donut / chip / badge), then it shows the "In review" (deep-teal) tier; once a retro exists all four show "Done".
- Given a partially-done or pending epic, when those surfaces render, then the class is exactly what `ForEpic` returns (no regression).
- Given a requirement whose covering epic is all-done-no-retro, when requirements status derives, then the requirement is **Done** (not Planned).
- Given an all-done-no-retro epic, when its Next Steps / retro affordance render, then a `retrospective` prompt is offered; once a retro exists, the affordance shows the "View Retrospective" link and no retro prompt.
- Given a `done` story, when its actions pane renders, then it shows an "All done" message with a checkmark and success styling and no `code-review` command; `review`/`in-progress` stories are unchanged.

## Design Notes

The retro-gated tier is deliberately NOT a replacement of `ForEpic`: `RequirementsParser.DeriveStatus` maps epic classes to requirement completeness, and a global downgrade would silently drop an all-done-no-retro epic through every branch to `Planned` — reporting a fully-built feature as not-started. Retros are a closure ritual, not an implementation signal, so completeness math keeps the plain classifier.

Re-gating the retro suggestion is a net correctness win: today `BmadCommands.ForEpic` and `RenderRetroAffordance` prompt "run a retrospective" off `"done"`, which also fires for an epic that already has one. Keying on `"review"` moves the prompt to exactly the state that needs it and stops nagging a retro'd epic.

`EpicLabel`/`EpicStages` gaining `"review"` is required for the donut, whose buckets iterate `EpicStages` (an unbucketed class is silently dropped — the code comments warn about this).

All-done panel shape (~6 lines):
```html
<div class="chart-panel next-steps all-done">
  <h3>Next Steps</h3>
  <p class="all-done-complete"><span class="all-done-icon">{done-icon}</span>All done — this story is complete.</p>
</div>
```

## Verification

**Commands:**
- `dotnet test` -- expected: all pass (incl. new epic-status, requirements-guard, and actions-pane assertions; re-baseline any epic-status golden/parity fixtures with an all-done-no-retro epic).
- `dotnet build` -- expected: clean.

**Manual checks:**
- Generate the sample site: an all-done-no-retro epic reads deep-teal "In review" in the sunburst, Epic Status donut, epics-index chip, and epic page badge; its requirements are unaffected; a `done` story page shows the "All done" panel instead of a code-review prompt.

## Review Outcome

Adversarial review (Blind Hunter + Edge Case Hunter, opus) on the my-files diff. Triage:

- **Patched — watch-mode staleness (real regression):** `RegenerateEpics` rebuilt the epics model without re-stamping `HasRetrospective`, so a retro'd all-done epic would flip to "In review" after an incremental edit. Fixed via a shared `TagEpicRetrospectives()` helper called from both `GenerateAll` and `RegenerateEpics`, plus a regression test (`RegenerateEpics_KeepsRetroGatedEpicDone_DoesNotFlipToReviewInWatchMode`).
- **Patched — stale docs:** `EpicsView.cs` XML docs updated to name `ForEpicWithRetrospective` and its reachable classes.
- **Accepted boundary (not changed) — requirements coverage card** (`RequirementsTemplater.cs:162`): still on plain `ForEpic`, so an all-done-no-retro epic shows green "Done" there while "In review" elsewhere. This is the deliberate scope choice (declined the "requirement-flow chip" option). Trivial to extend later (retro-gate + add `.coverage-card.review`) if the cross-page divergence is unwanted.
- **Accepted (intended, disclosed) — donut center number:** `Count("done")/total` no longer counts all-done-no-retro epics. Only visible in the rare "everything built, nothing retro'd" state.
- **Rejected as noise:** dual-keyword status precedence (malformed input, self-consistent); the defensive `done` branch in `ForStory` (safe guard, accurately commented).

## Suggested Review Order

**The retro-gated classifier (design intent)**

- Start here — the whole rule in one method: all-done + no retro ⇒ "review", else `ForEpic`.
  [`StatusStyles.cs:74`](../../src/SpecScribe/StatusStyles.cs#L74)

- The "review" tier added to the donut's bucket list + its label.
  [`StatusStyles.cs:84`](../../src/SpecScribe/StatusStyles.cs#L84)

- The data seam the classifier reads — settable, default false.
  [`EpicsModel.cs:50`](../../src/SpecScribe/EpicsModel.cs#L50)

**Wiring the retro signal (single source, both entry paths)**

- The one place the flag is stamped, from the same map the retro links use.
  [`SiteGenerator.cs:828`](../../src/SpecScribe/SiteGenerator.cs#L828)

- Full-generate call site.
  [`SiteGenerator.cs:120`](../../src/SpecScribe/SiteGenerator.cs#L120)

- Watch-mode call site — the review-found regression fix.
  [`SiteGenerator.cs:339`](../../src/SpecScribe/SiteGenerator.cs#L339)

**Visual epic-status surfaces (the swaps)**

- Project sunburst inner ring.
  [`Charts.cs:153`](../../src/SpecScribe/Charts.cs#L153)

- Epic Status donut (buckets by `EpicStages`; zero segments skipped).
  [`HtmlRenderAdapter.Dashboard.cs:139`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs#L139)

- Epics-index chip + epic page header badge.
  [`EpicsViewBuilder.cs:33`](../../src/SpecScribe/EpicsViewBuilder.cs#L33)

- Epic-card badge on the epics index.
  [`HtmlRenderAdapter.Epics.cs:111`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs#L111)

- Epic page StatusStage metadata.
  [`EpicsTemplater.cs:49`](../../src/SpecScribe/EpicsTemplater.cs#L49)

**Workflow guidance re-gating**

- Epic Next Steps: "review" now suggests the retrospective; "done" suggests nothing.
  [`BmadCommands.cs:281`](../../src/SpecScribe/BmadCommands.cs#L281)

- Retro affordance shows the "capture the lessons" nudge on the "review" state.
  [`EpicsViewBuilder.cs:193`](../../src/SpecScribe/EpicsViewBuilder.cs#L193)

**Done-story actions pane**

- `RenderNextSteps` routes a done story to the celebratory panel.
  [`BmadCommands.cs:32`](../../src/SpecScribe/BmadCommands.cs#L32)

- The "All done" panel (shared done checkmark + success class).
  [`BmadCommands.cs:50`](../../src/SpecScribe/BmadCommands.cs#L50)

- `ForStory` split: `review` keeps code-review, `done` returns nothing.
  [`BmadCommands.cs:244`](../../src/SpecScribe/BmadCommands.cs#L244)

**Peripherals — CSS + tests**

- Epic "review" chip/badge styling (deep-teal) + the all-done panel success styling.
  [`specscribe.css:851`](../../src/SpecScribe/assets/specscribe.css#L851)

- Classifier + label unit tests.
  [`StatusStylesTests.cs:100`](../../tests/SpecScribe.Tests/StatusStylesTests.cs#L100)

- Requirements guard: all-done-no-retro epic still rolls up to Done, not Planned.
  [`RequirementsAndProgressTests.cs:220`](../../tests/SpecScribe.Tests/RequirementsAndProgressTests.cs#L220)

- Watch-mode regression test.
  [`SiteGeneratorStoryEpicPagesTests.cs:177`](../../tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs#L177)
