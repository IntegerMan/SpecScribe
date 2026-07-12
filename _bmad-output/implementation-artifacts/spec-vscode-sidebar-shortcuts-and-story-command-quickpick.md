---
title: 'VS Code Sidebar Shortcuts and Status-Gated Story Command Quick Pick'
type: 'feature'
created: '2026-07-12'
status: 'done'
review_loop_iteration: 0
baseline_commit: '875eb0f303940adc71863acbc15757a3087e0ace'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The SpecScribe sidebar exposes only the Project Outline tree — every other surface (dashboard, epics, generated site, generate/watch, settings) needs the command palette. The tree's "Copy Helper Prompt" never says which command it copies, and its single command doesn't match the status-gated Next Steps set the story page shows (code-review must not be offered for un-reviewed work).

**Approach:** (1) A "Shortcuts" tree section at the top of the SpecScribe sidebar with one-click nodes for the existing host commands. (2) Additively extend the core outline so each story carries the FULL status-gated command list (the exact `BmadCommands.ForStory` set the story page renders); replace the copy action with "Copy BMad Command…" — a Quick Pick listing literal command strings + descriptions; picking one copies it, and the toast names the copied command verbatim.

## Boundaries & Constraints

**Always:**
- Thin shim (AD-1/AD-2): all command strings/descriptions composed in C#; no command text or status logic in TypeScript. Shortcut labels/icons are host chrome (same class as existing `package.json` titles).
- Additive payload: keep `helperCommand`; new `commands` field optional TS-side, falling back to a one-item list from `helperCommand` (older core).
- Read-only (AD-6): Quick Pick only copies to clipboard; nothing executes.
- Generated site stays byte-identical (data-only outline change).
- Context-action gating derives solely from the core list being empty/non-empty (done → empty via `ForStory`).
- Shortcuts view gated on `specscribe.projectDetected` via the view's `when`.

**Ask First:** epic-level command lists on the outline; any new/changed SVG or branding asset (deferred goal).

**Never:** change `ForStory`'s suggestion logic (page behavior untouched); execute commands in a terminal from the pick; scrape surface HTML for commands.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Ready story | ready-for-dev | Pick shows only dev-story (no code-review) | N/A |
| In-progress story | in-progress | dev-story + code-review | N/A |
| In-review story | in-review | code-review only | N/A |
| Done story | done | Empty list → no copy action in menu | N/A |
| Undrafted story | no artifact | create-story (+ readiness check on X.1 if exposed) | N/A |
| Module exposes none | empty catalog | Empty list → action hidden | N/A |
| Older core payload | no `commands` field | Fallback one-item list from `helperCommand` | N/A |
| Pick cancelled | Esc | Nothing copied, no toast | N/A |
| Clipboard rejects | headless host | Existing `copyToClipboard` error toast | error toast |
| Non-SpecScribe folder | no markers | Shortcuts view hidden | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/BmadCommands.cs:224` -- private `ForStory` = the status-gated (Command, Description) list; expose via new public `StoryCommands`; `PrimaryStoryCommand` becomes its first
- `src/SpecScribe/ProjectOutline.cs:34` -- `OutlineStory`: add `Commands` + `OutlineStoryCommand(Command, Description)` record
- `src/SpecScribe/SiteGenerator.cs:900` -- outline build in `RenderWebviewSurfaces`; populate both fields from one call
- `src/SpecScribe/Commands.cs:86` -- `SerializePayload` camelCases nested records automatically; pin shape in test
- `extension/package.json` -- views (add `specscribe.shortcuts` above outline), commands/menus (add `specscribe.copyStoryCommand`, retire `copyHelperPrompt`)
- `extension/src/extension.ts` -- TS `OutlineStory` interface, `contextValue` gate (:655), Quick Pick handler, static `ShortcutsTreeProvider`
- `tests/SpecScribe.Tests/SiteGeneratorOutlineTests.cs` -- the outline/payload suite all new tests landed in (corrected during review: the originally-named `BmadCommandsTests.cs` doesn't exist; this file already owns the outline fixture + `SerializePayload` pins)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/BmadCommands.cs` -- public `StoryCommands(StoryInfo, CommandCatalog)` returning the full ordered list (empty for done); `PrimaryStoryCommand` delegates -- one source of truth
- [x] `src/SpecScribe/ProjectOutline.cs` -- `OutlineStoryCommand` record + `OutlineStory.Commands` (doc: mirrors the page's Next Steps) -- shim contract
- [x] `src/SpecScribe/SiteGenerator.cs` -- populate `Commands` + `HelperCommand` from one `StoryCommands` call -- can't disagree with the page
- [x] `tests/SpecScribe.Tests` -- unit-test matrix rows (done→empty, ready→no code-review) + `SerializePayload` emits camelCase `commands` -- pins wire contract
- [x] `extension/package.json` -- shortcuts view (`when: specscribe.projectDetected`), `specscribe.copyStoryCommand` "Copy BMad Command…" + menu rewiring on `-helper` nodes, remove retired command
- [x] `extension/src/extension.ts` -- `ShortcutsTreeProvider` (nodes: Open Dashboard, Open Epics, Refresh, Open Generated Site, Generate Full Site, Watch, Open Project Settings; codicon + `TreeItem.command`); Quick Pick (label = literal command, detail = description; toast names it); `commands`-driven `contextValue` gate w/ `helperCommand` fallback

**Acceptance Criteria:**
- Given a ready-for-dev story node, when "Copy BMad Command…" opens, then the pick lists exactly the dev-story command — matching the story page's panel.
- Given a done story node, when its context menu opens, then no copy action is present.
- Given a command is picked, then the clipboard holds the literal string and the toast names it verbatim.
- Given a SpecScribe workspace, then Shortcuts renders above Project Outline and each node triggers its command.
- Given `dotnet test` before/after, then no NEW failures (known pre-existing golden-fingerprint failure excepted).

## Spec Change Log

## Verification

**Commands:**
- `dotnet test` (repo root) -- expected: green except the known pre-existing `GoldenContentFingerprint` failure (confirm it fails identically BEFORE starting)
- `npm run typecheck` then `npm run build` (in `extension/`) -- expected: clean

**Manual checks (if no CLI):**
- F5 Extension Development Host: Shortcuts section works; "Copy BMad Command…" only on non-done stories; pick strings match the same story's page panel; toast names the copied command.

## Suggested Review Order

**The core contract — one status-gated command list, shared with the page**

- Entry point: the page's private `ForStory` list, now projected as data (empty when done; whitespace filtered).
  [`BmadCommands.cs:45`](../../src/SpecScribe/BmadCommands.cs#L45)

- The wire record + the `Commands` list on `OutlineStory` (`HelperCommand` kept as first-entry back-compat).
  [`ProjectOutline.cs:9`](../../src/SpecScribe/ProjectOutline.cs#L9)

- One `StoryCommands` call feeds both fields — tree and page can never disagree.
  [`SiteGenerator.cs:923`](../../src/SpecScribe/SiteGenerator.cs#L923)

**The Quick Pick — exact commands, core-gated visibility**

- Shape-defensive list accessor with older-core `helperCommand` fallback; empty means no action.
  [`extension.ts:850`](../../extension/src/extension.ts#L850)

- The pick itself: label = literal command, detail = page description; toast names the copied string.
  [`extension.ts:868`](../../extension/src/extension.ts#L868)

- The `-helper` contextValue gate is just "core list non-empty" — done stories expose nothing.
  [`extension.ts:675`](../../extension/src/extension.ts#L675)

**The Shortcuts section**

- Seven static host-chrome nodes; Generate/Watch stay staged-terminal handoffs (read-only, AD-6).
  [`extension.ts:732`](../../extension/src/extension.ts#L732)

- Leaf `TreeItem`s with codicons; command fires on click.
  [`extension.ts:746`](../../extension/src/extension.ts#L746)

**Manifest wiring**

- New view above the outline, gated on `specscribe.projectDetected`.
  [`package.json:106`](../../extension/package.json#L106)

- `copyHelperPrompt` retired → `copyStoryCommand` ("Copy BMad Command…") across commands + menus.
  [`package.json:88`](../../extension/package.json#L88)

**Peripherals — tests + docs**

- Page↔tree parity asserted against the ACTUAL rendered panel (badge count == list count), incl. the readiness-leads X.1 case.
  [`SiteGeneratorOutlineTests.cs:395`](../../tests/SpecScribe.Tests/SiteGeneratorOutlineTests.cs#L395)

- Status-matrix pins on the shared fixture (one generation) + the camelCase wire shape.
  [`SiteGeneratorOutlineTests.cs:371`](../../tests/SpecScribe.Tests/SiteGeneratorOutlineTests.cs#L371)

- The new F5 smoke checklist (the extension's manual regression net) + stale 6.9 prose corrected.
  [`README.md:150`](../../extension/README.md#L150)
