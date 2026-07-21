---
title: 'Story 6.5 deferred-work cleanup (webview light-theme block + test hardening)'
type: 'chore'
created: '2026-07-20'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '8edfa701689ed9c62245efba755b42bd8e1de155'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Six items are open in `deferred-work.md` under Story 6.5's code review. One is the section-header/`source_spec` line with no distinct defect of its own. Two are genuine gaps: `.vscode-light` has no dedicated contrast-tuning block (the story's own decision to reuse the warm-light accent values was never verified against a real host light background), and `SiteGeneratorWebviewTests.FullGenerateThenWebviewPass_LeavesSourceArtifactsUntouched` only compares the source file set + `LastWriteTimeUtc`, so a same-mtime content mutation would pass undetected. Two are test-quality issues: `WebviewHelpersTests`/`WebviewThemingTests` pin the read-only prompt's exact English wording with ordinal string matching, coupling test stability to copy-editing instead of the behavioral contract; and no test exercises `<`/`>` in `siteTitle` reaching `data-ss-prompt` (only the double-quote case is covered, even though the same `PathUtil.Html` path is relied on there). The sixth — the debounced live-refresh spawning the renderer post-disposal — describes `extension.ts` as it existed at Story 6.5; the Story 6.9/6.11 `SpecScribeStore` refactor since replaced that logic: `onWatchEvent` now gates every spawn on `anyConsumerVisible()` *before* calling `load()`, and the panel's own `dataChanged` subscriber checks `disposed` before pushing — the exact ordering this item asked for already exists structurally.

**Approach:** Fix the two genuine gaps and the two test-quality issues at their cited sites. Verify item 6 is truly resolved by reading the current `extension.ts` flow end-to-end; if a residual gap surfaces, patch it narrowly — otherwise close it as an audit finding, no code change. Close all 6 `deferred-work.md` entries with the file's existing `RESOLVED`-strikethrough convention.

## Boundaries & Constraints

**Always:**
- Keep the generated HTML surface's byte-parity guarantee: `specscribe.css` must never gain a `.vscode-*` scope or `--vscode-*` variable (`ProductionStylesheet_CarriesNoWebviewThemeScope_SoTheHtmlSurfaceCannotInheritIt` stays green).
- The new `.vscode-light` block only re-values existing tokens (same set the dark/HC blocks re-value) — no new markup, no new CSS classes.
- Update `deferred-work.md` in place using its existing convention; do not delete or renumber other entries.

**Ask First:** If reading `extension.ts` turns up an actual live gap in item 6 beyond what the store refactor already guards, confirm the narrow patch's scope before widening beyond the cited debounce path.

**Never:**
- Do not retune the six `specscribe.status.*` activity-bar tree-icon accents or start a branding/iconography pass — that debt is tracked separately (`spec-vscode-sidebar-shortcuts-and-story-command-quickpick.md`'s deferred entry), not this spec.
- Do not touch the persistent `--serve` renderer lifecycle, nav-toggle bridge, or scratch-key logic — already hardened in the Story 6.4 cleanup.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Webview opened under a light host theme | `body` carries `.vscode-light` | Status/insight accents come from an explicit, verified token block (not implicit inheritance of the base sheet) | N/A |
| A generated webview artifact's content changes but its mtime is spoofed/coincidentally equal | `FullGenerateThenWebviewPass_LeavesSourceArtifactsUntouched` runs | Content-hash comparison catches the mutation | Test fails instead of passing silently |
| `siteTitle` contains `<script>` | `Render()` builds `data-ss-prompt` | Escaped to `&lt;script&gt;`; no raw tag reaches the attribute | N/A |
| Prompt copy is edited (wording only, same intent) | `WebviewHelpers.CodeReviewPrompt` changes | Tests assert the read-only contract via one shared constant, not a duplicated literal sentence | Test suite doesn't need a manual string update |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/assets/specscribe-webview-theme.css` -- Section 2 (~line 118) -- add a `.vscode-light { ... }` block mirroring `.vscode-dark`/`.vscode-high-contrast`/`.vscode-high-contrast-light`, re-valuing the same status/chart tokens
- `src/SpecScribe/WebviewHelpers.cs` -- `CodeReviewPrompt` (~line 20) -- extract the read-only sentence into a named `public const string` the method composes from
- `tests/SpecScribe.Tests/WebviewThemingTests.cs` -- `ThemeBridge_ContrastTunesTheStatusAndInsightAccents...` (~line 96) -- extend to assert `.vscode-light {` exists and carries the token set; `Render_HelperPath_HandsOffTextOnly...` (~line 158) -- assert against `WebviewHelpers.ReadOnlyDirective` instead of the literal sentence; add a new fact exercising `<`/`>` in `siteTitle` alongside the existing quote test (~line 168)
- `tests/SpecScribe.Tests/WebviewHelpersTests.cs` -- replace the two ordinal literal-sentence asserts (~lines 29, 165) with asserts against the new constant
- `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` -- `FullGenerateThenWebviewPass_LeavesSourceArtifactsUntouched` (~line 350) -- add a SHA-256 content-hash dictionary alongside the existing mtime dictionary
- `extension/src/extension.ts` -- read-only audit of the `dataChanged` subscriber (~line 419) and `SpecScribeStore.onWatchEvent` (~line 673); patch only if a residual gap is found
- `_bmad-output/implementation-artifacts/deferred-work.md` -- close all 6 Story 6.5 entries

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/assets/specscribe-webview-theme.css` -- add the `.vscode-light` contrast-tuning block -- closes the "no dedicated contrast-tuning block" gap
- [x] `src/SpecScribe/WebviewHelpers.cs` -- expose `ReadOnlyDirective` as a named constant -- single source of truth for product + tests
- [x] `tests/SpecScribe.Tests/WebviewThemingTests.cs` -- extend the contrast-tuning test for `.vscode-light`; switch the literal-wording assert to the constant; add a `<`/`>` siteTitle-escaping fact
- [x] `tests/SpecScribe.Tests/WebviewHelpersTests.cs` -- switch the two literal-wording asserts to the constant
- [x] `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` -- add content-hash comparison to the untouched-artifacts test
- [x] `extension/src/extension.ts` -- audit the debounce/disposal ordering; patch narrowly only if a gap is confirmed -- audit found the Story 6.9/6.11 `SpecScribeStore` refactor already guards this correctly; no code change made
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- close all 6 Story 6.5 entries with the RESOLVED-strikethrough convention

**Acceptance Criteria:**
- Given the webview renders under `.vscode-light`, when the stylesheet is inspected, then an explicit `.vscode-light { ... }` block re-values the same status/chart tokens the dark/HC blocks re-value.
- Given `WebviewHelpers.CodeReviewPrompt`'s wording changes (same intent, different phrasing), when the test suite runs, then no test needs a manual literal-string update to stay green.
- Given a `siteTitle` containing `<` or `>`, when the helper button is rendered, then the emitted `data-ss-prompt` attribute contains no raw `<`/`>` character.
- Given `RenderWebviewSurfaces`/`GenerateAll` write the site, when a source artifact's content changes but its `LastWriteTimeUtc` does not, then `FullGenerateThenWebviewPass_LeavesSourceArtifactsUntouched` fails.
- Given the live-refresh debounce audit, when the current `extension.ts` flow is traced end-to-end, then either a narrow patch closes a confirmed gap, or the finding is closed as already-resolved with the reasoning recorded in `deferred-work.md`.

## Spec Change Log

## Design Notes

Item 4 (live-refresh post-disposal spawn): `onWatchEvent` (extension.ts ~673) already gates every reload on `anyConsumerVisible()` *before* calling `load()`, and the panel's `dataChanged` subscriber (~419) checks `disposed` before pushing — both guards run before the async work, not after. This is the exact ordering the deferred item asked for; it arrived as a side effect of the Story 6.9/6.11 `SpecScribeStore` refactor, which didn't exist yet at Story 6.5. Confirm this holds under implementation and close without a code change if so.

Item 2 (`.vscode-light`): pin the base sheet's warm-light token values explicitly (literal, not `var()` re-reference) in a `.vscode-light` block, matching the audit-trail convention the other three blocks already use. The verification is a computed test (`StylesheetTests.VscodeLightBlock_MatchesRootValues_AndRealTextTokensClearWcagAA`, reusing the existing `ContrastRatio`/`TokenValue` helpers from the spec-scribes-nib-branding contrast pass), not a prose claim: it asserts zero drift from `:root`, and that the tokens actually used as body text color (teal, teal-deep, moss, rust) clear WCAG AA (≥4.5:1) against VS Code's white Light+ canvas. `--gold`/`--gold-light`/`--rust-light`/`--moss-light` are deliberately NOT contrast-asserted — every usage pairs them with their own literal pastel badge background, so "vs the page canvas" isn't the adjacent surface WCAG actually cares about for them (an earlier review draft incorrectly claimed all tokens "clear AA," which is false for `--gold-light` at ~2.4:1 against white — corrected during self-review by computing rather than eyeballing).

## Verification

**Commands:**
- `dotnet test` -- expected: full suite green, including the new/extended `.vscode-light`, escaping, content-hash, and constant-based assertions
- `cd extension && npm run typecheck` -- expected: compiles clean (only if item 4's audit produces a code change)

**Manual checks (if no CLI):**
- Open the extension against this repo under VS Code's Light+ theme and visually confirm status badges/accents stay legible (no automated coverage for the CSP-restricted webview host).

## Suggested Review Order

**The `.vscode-light` theme block**

- The dedicated light-theme scope, byte-identical to `:root`, added first among the four `.vscode-*` blocks.
  [`specscribe-webview-theme.css:135`](../../src/SpecScribe/assets/specscribe-webview-theme.css#L135)

- Computed (not eyeballed) verification: zero drift from `:root` + the tokens actually used as text clear WCAG AA on a white host canvas.
  [`StylesheetTests.cs:631`](../../tests/SpecScribe.Tests/StylesheetTests.cs#L631)

**The `ReadOnlyDirective` constant (decoupling tests from copy-editing)**

- Directive extracted to a named constant so wording edits don't require a duplicated-literal test update.
  [`WebviewHelpers.cs:16`](../../src/SpecScribe/WebviewHelpers.cs#L16)

- Structural check (prompt carries the constant) — review patch: paired with a semantic check below since this alone is tautological.
  [`WebviewHelpersTests.cs:23`](../../tests/SpecScribe.Tests/WebviewHelpersTests.cs#L23)

- Semantic check on the constant itself (loose keyword match) — the piece that actually protects the read-only contract.
  [`WebviewHelpersTests.cs:36`](../../tests/SpecScribe.Tests/WebviewHelpersTests.cs#L36)

**Content-hash hardening + the `<`/`>` escaping test**

- Both mtime and hash snapshotted from one file listing per phase — review patch: avoids a key-set-divergence risk between two dictionaries.
  [`SiteGeneratorWebviewTests.cs:367`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L367)

- New test pinning that a `<script>`-bearing site title reaches `data-ss-prompt` HTML-encoded, never as a raw tag.
  [`WebviewThemingTests.cs:189`](../../tests/SpecScribe.Tests/WebviewThemingTests.cs#L189)

**Peripherals**

- Deferred-work audit trail: all 6 Story 6.5 entries closed, plus one new deferred item for the untested disposed-panel ordering (no TS test harness exists).
  [`deferred-work.md`](./deferred-work.md)

