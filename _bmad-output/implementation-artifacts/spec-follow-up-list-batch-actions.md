---
title: 'Follow-up list batch Address/Close actions'
type: 'feature'
created: '2026-07-18T16:28:36-04:00'
status: 'done'
baseline_commit: '636a30aa831c7ecb4a6489563998d7b88d61da5a'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Follow-up list pages (`deferred-work.html`, `action-items.html`, and generated group pages like “Epic 2 follow-ups”) are scan-first with no batch AI entry — drivers must open each detail to Address/Close, even when many open items share one list.

**Approach:** Add a horizontal Next Steps–style pane (three cards) above each list: **Address all**, **Address first 5**, and **Close all** (Generate AI prompt). Each card uses the epic/story labeled copy/send chrome. When a page has open items of both kinds, each card holds a **deferred | action items** button pair; when only one kind is present, each card shows a single button for that kind.

## Boundaries & Constraints

**Always:**
- Surfaces: whole-site deferred backlog, whole-site open action-items list, and generated follow-up group pages.
- Gate the whole pane on `CommandCatalog.Command("quick-dev")`; omit entirely when missing or when the page has zero open items in scope (NFR8).
- Open only: deferred `!Resolved`; actions not done (`!FollowUpGeometry.IsDone` / open inventory already filtered). Ignore `Kind == "direct"` members for both buttons.
- Three cards in display order: Address all → Address first 5 → Close all. Omit **Address first 5** for a kind when that kind has fewer than 6 open items (Address all already covers them). On mixed pages, a card may show only one of the pair when the other kind is omitted or empty.
- Address prompts mirror detail/story batch: quick-dev + Address/Resolve wording + numbered discoverable cues (summary, provenance/`SourceKey`, detail href or backlog pointer). Close prompts mirror detail Close-with-AI (mark RESOLVED in `deferred-work.md` / done in `sprint-status.yaml`) for every open item in that card’s scope.
- “First 5” = first five open items of that kind in the page’s existing display order.
- `data-copy` payloads stay raw/un-linkified. Once deferred-work embeds copy payloads, do **not** run whole-page `ApplyReferenceLinks` on it (same trap as action-items / group pages).
- Zero new client JS; reuse `RenderLabeledCommand` / send-menu chrome.

**Ask First:**
- Changing per-item detail Next Steps (Address/Close/Resolve).
- Adding filter/query UI or auto-running commands.

**Never:**
- Invent associations not already on the page/ledger.
- Put Resolve `data-copy` on individual list rows (detail pages own per-item AI).
- Auto-execute IDE commands or invent new deep-link schemes.
- Render empty/broken command chrome when quick-dev is absent.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Deferred-only backlog, ≥6 open | Structured deferred-work; quick-dev present | 3 cards; each card one **Deferred** button; Address all / first 5 / Close list open deferred in display order | N/A |
| Action-items list, 1–5 open | Open actions only; quick-dev present | Address all + Close cards only (no first-5); each card one **Action items** button | N/A |
| Mixed group, both kinds ≥6 open | Group members action+deferred open | 3 cards; each card **Deferred \| Action items** pair; scopes independent | N/A |
| Mixed group, deferred open, actions none | Only deferred open on group | Cards show deferred-only singles (same as deferred page) | N/A |
| No quick-dev | Open items exist; catalog empty | Entire pane omitted | Silent omit |
| Zero open in scope | All resolved / no open actions passed | Entire pane omitted | Silent omit |
| Copy payload safety | Any batch card rendered | `data-copy` contains raw text, no `<a` | Assert in tests |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/BmadCommands.cs` -- Add list-batch prompt builders + a 3-card renderer that supports one or two labeled commands per card (do not overload `RenderInner`’s single-badge cards blindly)
- `src/SpecScribe/DeferredWorkTemplater.cs` -- Insert pane above the list; accept `CommandCatalog` (+ open deferred model already on page)
- `src/SpecScribe/ActionItemsTemplater.cs` -- Insert pane above the list (commands already threaded)
- `src/SpecScribe/FollowUpGroupTemplater.cs` -- Insert pane above rows; derive open deferred vs action from `FollowUpGroupMember.Kind` + `Resolved`
- `src/SpecScribe/SiteGenerator.cs` -- Pass `CommandCatalog` into deferred + group renders; stop `ApplyReferenceLinks` on deferred-work once copy payloads exist
- `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` -- E2E HTML + `data-copy` unlinkify scan for list batch panes
- `tests/SpecScribe.Tests/FollowUpGroupPagesTests.cs` (or nearest) -- Mixed/single-kind card pair coverage
- `src/SpecScribe/assets/specscribe.css` -- Only if dual-button-in-card needs a thin layout hook; prefer existing `.next-steps-cards` tokens

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/BmadCommands.cs` -- Build Address-all / Address-first-5 / Close-all suggestions per kind; render horizontal 3-card pane with optional deferred|actions command pair per card; gate on quick-dev + open counts
- [x] `src/SpecScribe/DeferredWorkTemplater.cs` + `ActionItemsTemplater.cs` + `FollowUpGroupTemplater.cs` -- Slot the pane above the list body; thread catalog where missing
- [x] `src/SpecScribe/SiteGenerator.cs` -- Wire catalog into deferred/group writers; drop whole-page `ApplyReferenceLinks` on deferred-work when payloads land
- [x] `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` (+ group tests as needed) -- Cover I/O matrix rows including mixed pair, first-5 omit, NFR8 omit, raw `data-copy`
- [x] `src/SpecScribe/assets/specscribe.css` -- Dual-button card layout only if markup cannot reuse existing tokens cleanly

**Acceptance Criteria:**
- Given a deferred-work page with ≥6 open deferred and quick-dev, when the page renders, then a Next Steps pane shows three cards (Address all, Address first 5, Close all) each with a Deferred copy/send control whose payload lists the correct open items with discoverable cues.
- Given an action-items page with open actions and quick-dev, when the page renders, then the same three-card pattern appears for action items (first-5 omitted when &lt;6 open).
- Given a mixed follow-up group with ≥6 open deferred and ≥6 open actions, when the page renders, then each of the three cards exposes both Deferred and Action items controls with independent scopes.
- Given open follow-ups but no quick-dev (or zero open in scope), when any of these list pages render, then the batch pane is absent.
- Given any rendered batch control, when `data-copy` is inspected, then it contains no linkified `<a` markup.

## Design Notes

Card chrome mirrors epic/story Next Steps (`chart-panel next-steps` + `.next-steps-cards`). Dual-kind cards keep **one column per action** (still three columns): two labeled badges side-by-side inside the card, not six separate cards.

Golden prompt shape (Address deferred all):

```
{quickDev} Address open deferred work on {pageTitle} (N items). Find writeups in deferred-work.md and follow-up detail pages:
1. {summary} [{sourceKey}] → {detailHref}
…
```

Close deferred swaps the lead verb/target to mark RESOLVED in `deferred-work.md` (audit trail kept). Action-item Address uses Resolve-with-AI wording; Close targets `sprint-status.yaml` done — same spirit as `ForActionItem` / `ForDeferredItem`.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~FollowUp"` -- expected: all matching tests green, including new list-batch cases
- `dotnet build src/SpecScribe/SpecScribe.csproj` -- expected: zero errors

**Manual checks (if no CLI):**
- Regenerate portal; open deferred-work, action-items, and a mixed epic follow-ups group; confirm three-card pane, pair buttons when mixed, and Copy payloads look right in the clipboard.

## Suggested Review Order

**Pane renderer**

- Entry point: three-card Address all / first 5 / Close with optional Deferred|Action pair
  [`BmadCommands.cs:826`](../../src/SpecScribe/BmadCommands.cs#L826)

- Dual-button card layout reuses labeled copy/send chrome
  [`BmadCommands.cs:874`](../../src/SpecScribe/BmadCommands.cs#L874)

**List surfaces**

- Deferred backlog: pane above list + selective visible Story/Epic linkify
  [`DeferredWorkTemplater.cs:81`](../../src/SpecScribe/DeferredWorkTemplater.cs#L81)

- Action items: same pane for open actions only
  [`ActionItemsTemplater.cs:57`](../../src/SpecScribe/ActionItemsTemplater.cs#L57)

- Group pages: split open deferred vs action; ignore Kind=direct
  [`FollowUpGroupTemplater.cs:42`](../../src/SpecScribe/FollowUpGroupTemplater.cs#L42)

- Group provenance prefers SourceKey for prompt cues
  [`FollowUpGroupPages.cs:175`](../../src/SpecScribe/FollowUpGroupPages.cs#L175)

**Generator wiring**

- Drop whole-page ApplyReferenceLinks once deferred embeds data-copy
  [`SiteGenerator.cs:2821`](../../src/SpecScribe/SiteGenerator.cs#L2821)

- Thread CommandCatalog into group page writes
  [`SiteGenerator.cs:2907`](../../src/SpecScribe/SiteGenerator.cs#L2907)

**Chrome + tests**

- Thin dual-button CSS hook on existing next-steps tokens
  [`specscribe.css:2646`](../../src/SpecScribe/assets/specscribe.css#L2646)

- List-batch I/O matrix + raw data-copy scans
  [`FollowUpSurfacesTests.cs:696`](../../tests/SpecScribe.Tests/FollowUpSurfacesTests.cs#L696)

- Mixed Deferred|Action pair coverage on group pages
  [`FollowUpGroupPagesTests.cs:223`](../../tests/SpecScribe.Tests/FollowUpGroupPagesTests.cs#L223)
