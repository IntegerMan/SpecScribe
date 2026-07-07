---
title: 'Next Steps send-command split button'
type: 'feature'
created: '2026-07-06T00:00:00Z'
status: 'done'
route: 'one-shot'
---

# Next Steps send-command split button

> Retroactive record for work implemented out of band. Captured to match the quick-dev
> record-keeping convention (see `spec-github-pages-publish-docs-live.md`).

## Intent

**Problem:** The "Next Steps" panels surface a workflow prompt (e.g. `/bmad-dev-story 2.1`)
with only a single Copy button. There was no one-click way to hand that command to an
external editor.

**Approach:** Turn the Copy button into a split-button — Copy stays the primary action, and
a caret opens a small menu of per-destination actions. The one destination with a real
"open with the prompt pre-filled" URL is Cursor (`cursor://anysphere.cursor-deeplink/prompt?text=`);
every other tool (Copilot, Claude Code, Codex) has no such scheme, so it rides the Copy
button. The delivery is data-driven off a `SendTargets` list so more destinations drop in later.

## Boundaries & Constraints

**Always:** Honor the minimal-JS rule — the toggle is a native `<details>/<summary>`, the deep
link is a plain `<a>`, and Copy items reuse the existing `.copy-btn` handler. Zero new client JS.
URL-encode the command into each deep link, then HTML-escape the attribute.

**Never:** Do not auto-execute a command in any IDE. Do not invent a deep link for a tool that
has no public one — those copy instead.

## Code Map

- `src/SpecScribe/BmadCommands.cs` -- `SendTarget` record + `SendTargets` list; `RenderCommandActions` builds the split-button; `RenderInner` calls it per suggestion.
- `src/SpecScribe/assets/specscribe.css` -- `.cmd-actions` / `.send-menu` / `.send-toggle` / `.send-link` styles + a `:has()` clip-lift so the popover isn't cut off by the panel's `overflow-x:auto`.
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` -- encoding + markup assertions.
- `tests/SpecScribe.Tests/StylesheetTests.cs` -- selector guard for the send menu.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/BmadCommands.cs` -- add data-driven `SendTargets` + `RenderCommandActions` split-button markup with URL-encoded deep links.
- [x] `src/SpecScribe/assets/specscribe.css` -- style the split-button/popover on existing tokens; lift the panel clip only while a menu is open.
- [x] `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` -- assert Copy is preserved and the Cursor href is correctly encoded.
- [x] `tests/SpecScribe.Tests/StylesheetTests.cs` -- assert the send-menu selectors ship in the embedded stylesheet.

**Acceptance Criteria:**
- Given a Next Steps prompt, when the page renders, then it shows a primary Copy button (with its `data-copy` payload intact) plus a `<details class="send-menu">` caret.
- Given the command `/bmad-dev-story 2.1`, when the Cursor menu item renders, then its href is `cursor://anysphere.cursor-deeplink/prompt?text=%2Fbmad-dev-story%202.1`.
- Given JavaScript is disabled, when the caret is activated, then the menu still expands (native `<details>`) and the Cursor link still works.

## Spec Change Log

_None — implemented directly; no review loopback._

## Design Notes

Data-driven expansion point: once SpecScribe is hosted as a VS Code extension, a Copilot target
can post the command straight into Copilot Chat rather than copy — added by appending to
`SendTargets` (documented in that record's doc-comment).

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: all pass (includes the new markup/encoding + stylesheet guards).

**Manual checks:**
- Generate the site and inspect a Next Steps prompt: caret opens the menu, Copy copies the raw command, "Open in Cursor" carries the URL-encoded href, no console errors.

## Suggested Review Order

**Split-button rendering**

- Entry point — the per-command split-button builder (Copy + native `<details>` deep-link menu).
  [`BmadCommands.cs:77`](../../src/SpecScribe/BmadCommands.cs#L77)

- The data-driven destination list; Cursor is the one real deep link, others copy.
  [`BmadCommands.cs:25`](../../src/SpecScribe/BmadCommands.cs#L25)

**Styling**

- Split-button + popover styles, plus the `:has()` clip-lift that keeps the menu from being cut off.
  [`specscribe.css:1759`](../../src/SpecScribe/assets/specscribe.css#L1759)

**Tests**

- Encoding + markup assertions for the Cursor deep link.
  [`HtmlTemplaterTests.cs:374`](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs#L374)

- Stylesheet guard for the send-menu selectors.
  [`StylesheetTests.cs:67`](../../tests/SpecScribe.Tests/StylesheetTests.cs#L67)
