---
title: 'GSD Pending-Phase Actions'
type: 'feature'
created: '2026-08-08'
baseline_commit: fdcc3b5fb6e22624e0f948e14bee2691eb1f4ad8
status: 'done'
review_loop_iteration: 0
context:
  - 'CLAUDE.md'
  - 'docs/adrs/0038-framework-adapter-selection-and-neutral-source-root-discovery.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An unplanned GSD Core phase is shown as pending, but its next-step surfaces expose only `/gsd:plan-phase`. Users cannot see the installed preparatory commands that help establish phase context before planning.

**Approach:** Discover GSD's installed discussion, UI-specification, and research commands alongside its existing workflow commands. Use the shared pending-epic selector to recommend discussion first, then the other installed preparatory actions and planning, so every existing next-step projection stays consistent.

## Boundaries & Constraints

**Always:** Show an action only when its corresponding `.claude/commands/gsd/<stem>.md` file exists. Pass GSD's native phase identifier verbatim, including decimal and zero-padded identifiers, never the synthetic epic ordinal. Discussion is the primary action whenever installed; UI specification, research, and planning follow in that order. Keep deferred-work alternatives and the established rendering grammar intact.

**Ask First:** Ask before adding a command not named here, changing the displayed action order, deriving recommendations from phase companion artifacts, or changing non-GSD framework behavior.

**Never:** Do not create GSD command definition files, parse phase context/UI/research documents to infer state, alter lifecycle classification, add frontend or extension-specific behavior, or expose an unavailable slash command.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|---------------|----------------------------|----------------|
| Full GSD installation | Pending phase `2.1`; all four command files exist | Discussion is primary; UI spec, research, and planning appear as alternatives with `2.1` | N/A |
| Partial installation | Pending phase; one or more preparatory command files are absent | Omit only unavailable actions; first installed action is primary | Never render a nonexistent command |
| No native phase argument | Pending GSD epic lacks `WorkflowCommandArgument` | Do not render a malformed command or substitute the synthetic ordinal | Omit phase-scoped actions |
| Non-GSD catalog | Pending BMad epic | Existing create-epics-and-stories recommendation remains unchanged | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/GsdCoreArtifactAdapter.cs` -- maps installed GSD command definition files to the framework-neutral command catalog.
- `src/SpecScribe/BmadCommands.cs` -- selects ordered next-step commands for pending epics and projects them to dashboard, epic, webview, and related-work consumers.
- `tests/SpecScribe.Tests/GsdCoreArtifactAdapterTests.cs` -- hermetic temporary-repository coverage for GSD command discovery.
- `tests/SpecScribe.Tests/RelatedWorkTests.cs` -- primary-epic-command behavior consumed by related-work actions.
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` -- rendered pending-epic guidance regression coverage.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/GsdCoreArtifactAdapter.cs` -- discover installed `discuss-phase`, `ui-phase`, and `research-phase` definitions as distinct GSD workflow steps, retaining file-presence gating.
- [x] `src/SpecScribe/BmadCommands.cs` -- treat the new GSD workflow steps as phase-scoped, and centralize pending-epic suggestions so project and epic consumers share the ordered GSD sequence without changing BMad's fallback behavior.
- [x] `tests/SpecScribe.Tests/GsdCoreArtifactAdapterTests.cs` -- cover discovery of the new command steps and omission of a missing definition.
- [x] `tests/SpecScribe.Tests/RelatedWorkTests.cs` -- prove a pending GSD phase selects discussion as its primary command with the native phase argument.
- [x] `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` -- prove rendered pending-phase guidance carries the installed GSD action order and no unavailable command.

**Acceptance Criteria:**
- Given all four GSD phase command definitions are installed, when a phase has no plans, then dashboard, epic, webview, and related-work consumers derive discussion as the primary action and retain the same ordered alternatives.
- Given a GSD phase number such as `2.1`, when a phase-scoped next step is rendered, then every command uses that exact argument.
- Given an individual command definition is unavailable, when next steps render, then that command alone is omitted and the remaining first action becomes primary.
- Given a non-GSD command catalog, when a pending epic renders, then it preserves the existing create-epics-and-stories behavior.

## Spec Change Log

## Design Notes

The command catalog is the capability boundary. It already suppresses missing BMad commands and GSD Core discovers definitions from the repository; extending that catalog keeps unavailable commands out of every consumer without adding GSD checks to renderers.

The pending-epic selector currently exists twice: once for an epic and once for the project dashboard. One helper should build the pending action sequence, so the dashboard, epic detail, webview payload, and related-work primary action cannot drift apart.

## Verification

**Commands:**
- `dotnet test SpecScribe.slnx --filter "FullyQualifiedName~GsdCoreArtifactAdapterTests|FullyQualifiedName~RelatedWorkTests|FullyQualifiedName~HtmlTemplaterTests"` -- expected: focused GSD discovery, primary-action, and rendered-guidance tests pass.
- `dotnet build SpecScribe.slnx` -- expected: solution builds without warnings or errors caused by this change.

## Suggested Review Order

**Shared Action Selection**

- Centralizes the phase-aware sequence, preventing dashboard and detail surfaces from drifting.
  [BmadCommands.cs:472](../../src/SpecScribe/BmadCommands.cs#L472)

- Routes compact pending cards through the shared primary-action decision.
  [HtmlRenderAdapter.Epics.cs:217](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs#L217)

**Capability Discovery**

- Maps preparatory GSD commands only when their installed definition files exist.
  [GsdCoreArtifactAdapter.cs:79](../../src/SpecScribe/GsdCoreArtifactAdapter.cs#L79)

**Regression Coverage**

- Covers installed-only discovery, including an unavailable preparatory command.
  [GsdCoreArtifactAdapterTests.cs:232](../../tests/SpecScribe.Tests/GsdCoreArtifactAdapterTests.cs#L232)

- Pins discussion primacy and rejects malformed native phase arguments.
  [RelatedWorkTests.cs:353](../../tests/SpecScribe.Tests/RelatedWorkTests.cs#L353)

- Verifies ordered panels and the compact epics-index card path.
  [HtmlTemplaterTests.cs:1131](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs#L1131)