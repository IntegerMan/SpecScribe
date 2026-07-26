---
baseline_commit: 611097d
---

# Story 20.8: Dashboard Details Pane — `select` Mode in Practice

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

**Epic:** [Epic 20 — Interactive Project Explorer, Standardized Hierarchy Explorer on Plotly](../planning-artifacts/epics.md#epic-20-interactive-project-explorer--standardized-hierarchy-explorer-on-plotly)
**Design-locked by:** [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) §3 (the `navigate` | `select` mode contract) and [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md)
**Depends on:** Story 20.5 (`select` mode + the payload) and Story 20.3 (the card rail and the ONE relationship vocabulary)
**Baseline commit:** `611097d`

## Story

As a visitor exploring the project from the home page,
I want clicking a node in the explorer to populate a details pane beside it rather than navigating away,
so that I can survey the project's structure and read about each part without losing my place.

---

## ⛔ Read first — the tree is mid-flight and AC #1 already shipped

**Story 20.5's owner verify round (round 2) is sitting UNCOMMITTED in the working tree right now and it
delivered this story's AC #1.** `epics.md` § Story 20.8 was amended in the same change to say so. Read that note
before anything else. Concretely, already present in the tree at create-story time:

| Already done (20.5 round 2, uncommitted) | Where |
|---|---|
| Story leaves raise `specscribe:explorer-select` and do **not** navigate | `specscribe.js` `activate()` — `state.selected`, `applyA11yLayer()`, `publishSelection(id)`, `announce(...)` |
| A card per **selectable** node, story leaves included | `RelatedWorkCards.Build(..., selectableIslandIds:)` |
| One primary BMad command per story card | `BmadCommands.PrimaryStoryCommand` via `Resolve(...)` |
| A `View details →` link per card | `RelatedCard.DetailHref` (pre-existing, 20.3) |
| A card for a selectable node with **no** work-graph edges | `RelatedWorkCards.SynthesizeNode` |
| `HierarchyNode.Detail` — "3 of 8 tasks done" / "No task plan yet" — replacing reader-facing `weight` | `HierarchyExplorer.WithDetails` |
| The portal's own rich tooltip on chart sectors (`.ss-hierarchy-sector` joined the shared `SEG` selector) | `specscribe.js`, `specscribe.css` |

**The story→epic relationship fold was REMOVED in that same round** so a story's relationships live on the story's
own card. That is the change this story reverses — see D1. It is a reversal of an owner-directed decision made
four hours earlier on better information, not a defect: nobody had the byte number yet.

**Therefore, before you write a line:** re-read `RelatedWorkCards.cs`, `HierarchyExplorer.cs`, `specscribe.js` and
`specscribe.css` **as they stand**, and `git diff` them against `611097d`. Every line reference in this file was
taken from an in-flight working copy. **Grep-verify every symbol before relying on it** (CLAUDE.md § Concurrent
work — a `Charts.cs` edit has silently vanished this way). **Never `git reset --hard`, `git checkout --`, or
`git clean`**: another session's uncommitted work is in this tree right now and that has already destroyed real
work mid-story.

### What this story IS and IS NOT

**IS:** finish `select` mode on the dashboard — pull the rail's byte cost back down (D1), make the story card
genuinely richer in the ways that matter (D2), close the last card-less selectable wedges (D3), and prove the
JS-off path and the empty state.

**IS NOT:**

| Not this story | Whose it is |
|---|---|
| Retiring any server-rendered chart SVG | 20.6 gate, then 20.7 |
| The golden-fingerprint **replacement** assertions | 20.6 AC#2 |
| Converting the other six hierarchy call sites | 20.7 |
| Deleting `Charts.cs` entry points / the three JS arc renderers | 20.7 AC#2 |
| The webview mount / the ADR 0005 CSP amendment | 20.7, jointly with 23.4 |
| Making the component twin `sr-only` on the dashboard | 20.6 D4 |
| Adding `covers` / `cites` relationship kinds | Epic 19, if ever — `WorkEdgeKind` has four members and 20.3 established that manufacturing a fifth is a phantom |

---

## Owner decisions locked at create-story (2026-07-25)

Elicited against the **actual in-flight tree**, not against the epic text. They constrain **how** the ACs are met;
they do not amend them. Recorded in `epics.md` under Story 20.8 in the same change (CLAUDE.md § Decision records).

**D1 — Payload ceiling: restore the fold, story cards go minimal.** A story's relationship groups return to living
**once**, folded into its epic's card as a labelled `RelatedWorkSubject` (Story 20.3's shipped design). The story's
own card keeps title, summary, command affordance and `View details →` — and carries **no relationship block at
all**. This is the answer to the ceiling question `epics.md` assigns to this story. Measured starting point: the
rail is **283,263 B of a 742,107 B dashboard (38.2%)**, 104 cards, 78 of them stories, up from 101,435 B / 21.5%
before round 2. `RelatedWork.MaxEntriesPerGroup` stays at **12** — the lever is not pulled, because removing the
duplication is the honest fix and truncating further trades away JS-off completeness for every node including
epics. Accepted cost: a reader who selects a story sees its relationships only by opening its epic, its own page,
or `work-graph.html` — all one click, all stated.

**D2 — Richer card: more command, more children, not more relationships.** The story card gains (a) the full
status-gated command set from `BmadCommands.StoryCommands` behind a **collapsed** disclosure, with
`PrimaryStoryCommand` staying as the always-visible primary badge, and (b) its **open deferred / action children
listed by name**. This is `epics.md`'s "task-level detail, deferred children, the full per-story command set rather
than one primary" minus "relationship depth", which D1 deliberately removes.

**D1 and D2 pull in opposite directions and that is deliberate.** D1 removes the heavy thing (relationship rows
— 348 links on this portal) and D2 adds light things (2–4 command entries and a short deferred list per story).
The net must be **measured, not assumed**, and reported honestly even if it is up rather than down (Task 5.4).

**D3 — Aggregates: the follow-up ones get cards, `~summary` does not.** `epic-N~open`, `epic-N~done`,
`orphan~open`, `orphan~done`, `unplanned~open`, `unplanned~done` **and the `unplanned` ROOT** get cards. They link
to real follow-up group pages and represent work with no other card. `epic-N~summary` does not: it is the epic
restated and its `href` **is** the epic page, so selecting it resolves to its parent epic's card instead.

> **The `unplanned` root is a gap neither 20.3 nor 20.5 noticed.** `RelatedWork.IslandIdFor` maps only
> `WorkNodeKind.Epic` → `epic-{N}` / `orphan` and `WorkNodeKind.Story` → the story id. The `unplanned` root
> (`SunburstExplorer.cs`, id `"unplanned"`, kind `"unplanned"`) matches neither, and `SynthesizeNode` returns
> `null` for it. So **drilling into Unplanned today shows the rail's empty state** — on shipped, reviewed code.
> Verify this in the browser before fixing it, then fix it.

---

## Acceptance Criteria

*Verbatim from [`epics.md` § Story 20.8](../planning-artifacts/epics.md). D1–D3 constrain how they are satisfied.*

1.
**Given** the dashboard explorer configured in `select` mode
**When** I activate a node
**Then** a details pane beside the chart populates with that node's high-level details, its **recommended-prompt button** (reusing the existing `BmadCommands` next-step command surface — not a second command vocabulary), and a **view-more link** to the node's own detail page
**And** the page does not navigate, and the selection is announced to assistive technology as a live region.

2.
**Given** ADR 0006's read-only helper constraint and the no-JS contract
**When** the pane renders
**Then** the recommended-prompt button remains a read-only helper that generates a prompt and never mutates planning artifacts (AD-6)
**And** with JavaScript unavailable the same details and links remain reachable through the server-rendered text twin, so `select` mode never becomes the only path to the information (ADR 0013).

3.
**Given** no selection yet, or a node with no details to show (NFR8)
**When** the pane would be empty
**Then** it shows a designed empty state per UX-DR22 rather than an incidental blank region
**And** the pane reuses Story 20.3's related-work groupings rather than introducing a second relationship vocabulary.

**AC #1 is already met by the in-flight 20.5 round-2 work** for epics, stories and the orphan root. This story
completes it for the remaining selectable wedges (D3) and must **re-verify it live** rather than inherit the claim.

---

## Tasks / Subtasks

### Task 1 — Restore the fold; make the story card minimal (AC: #3, D1)

- [ ] 1.1 Re-read `RelatedWorkCards.Build` as it currently stands. Round 2 replaced the 20.3 projection with a
      `selectableIslandIds`-driven loop plus a `relationships.Nodes` sweep for anything the chart did not draw.
      **Keep that loop** — it is what makes a card exist for a node with no edges — and change only what each
      card *carries*.
- [ ] 1.2 Reinstate the story→epic subject fold that round 2 deleted, in `RelatedWorkCards.Build`:
      for every `relationships.Nodes` entry whose `IslandId` is a bare story id, derive its epic
      (`20.5` → `epic-20`, via `IslandId.IndexOf('.')` — the shipped derivation, do not invent a second),
      drop its restated outgoing `Contains` group with the existing `RelatedWork.IsRestatedContainsGroup`, and
      attach the remainder to the epic card as a `RelatedWorkSubject`. **Restore the two ordering guarantees the
      deleted code carried and the reviewer will look for:** an explicit first-seen epic-order list (dictionary
      enumeration order is not a contract — FR31), and the fallback pass that creates a host card for an epic that
      has story subjects but no scope node of its own.
- [ ] 1.3 The story card's `Relationships` becomes an **empty** `RelatedWorkNode` (no `Groups`, no `Subjects`).
      `RelatedWorkTemplater.RenderCard` already guards on `rel.Groups.Count > 0 || rel.Subjects.Count > 0`, so the
      `<details class="related-card-full">` block simply does not render — **no templater change is needed for
      this, and adding one would be the wrong fix.**
- [ ] 1.4 A story card must still be **reachable to its relationships**: its `View details →` link goes to its own
      page as today, and the epic's card carries the folded subject. Do not add a second link vocabulary.
- [ ] 1.5 Update the XML doc-comment on `RelatedWorkCards.Build` and the block comment round 2 wrote
      (`"So the fold is gone rather than kept alongside a story card…"`) — it will be **factually wrong** after
      this task. State the reversal and its reason (the measured 38.2%) in place; do not leave two contradictory
      comments in one file.

### Task 2 — The richer story card (AC: #1, D2)

- [ ] 2.1 Keep `BmadCommands.PrimaryStoryCommand` as the visible primary badge, rendered exactly as today through
      `BmadCommands.RenderPrimaryActionBadge` — same copy-to-clipboard `cmd-badge` / `data-copy` surface, AD-6
      read-only, never a mutation.
- [ ] 2.2 Add the full set behind a collapsed disclosure: `BmadCommands.StoryCommands(story, commands, openDeferred)`
      returns `IReadOnlyList<OutlineStoryCommand>` (`Command`, `Description`) in the story page's own order, with
      the primary first. **Render entries 2..n only** — repeating the primary inside the disclosure is the
      "EpicEpic 19" class of duplication the 20.3 live round caught. Omit the disclosure entirely when there are
      none (a done story with no Address-deferred primary legitimately has zero — never a dead control).
- [ ] 2.3 Prefer `BmadCommands.RenderCommandMenu(label, items)` (`BmadCommands.cs:348`) if its markup fits the
      card; otherwise render a native `<details>` whose entries reuse the same badge helper. **Do not author a
      command string, a description, or a gating rule here** — all of it is decided in `BmadCommands` (AD-2), and
      the VS Code Quick Pick and the story page's Next Steps panel read the same list.
- [ ] 2.4 List the story's **open deferred / action children by name**: `geometry.DeferredForSource(story.Id)`
      filtered to open (the round-2 code already computes exactly this list to feed `PrimaryStoryCommand` — reuse
      that variable, do not call twice). Each is a real resolving link where the slot has an href; plain text where
      it does not. Cap the list and state the remainder, mirroring the shipped `+N more` idiom rather than
      truncating silently.
- [ ] 2.5 Keep the summary line single-sourced. Round 2 composes it as
      `StatusStyles.StoryLabel(stage) · "N of M tasks done" | "no task plan yet"`, which is the same phrasing
      `HierarchyExplorer.WithDetails` puts in `HierarchyNode.Detail` and in the chart's tooltip. **Do not add a
      third phrasing.** If you touch either, change both or route both through one helper.

### Task 3 — Close the card-less selectable wedges (AC: #1, #3, D3)

- [ ] 3.1 Extend `RelatedWorkCards.SynthesizeNode` to produce a node for the follow-up aggregates
      (`epic-N~open`, `epic-N~done`, `orphan~open`, `orphan~done`, `unplanned~open`, `unplanned~done`) and for the
      **`unplanned` root**. Their labels and hrefs already exist on the payload — the aggregate href is
      `geometry.LinkPrefix + FollowUpGroupPages.EpicPath(n)` (`FollowUpGroupPages.cs:47`), the orphan's is
      `geometry.FollowUpsGroupHref`, the unplanned root's is `unplannedGeo.GroupRootHref`
      (`SunburstExplorer.cs:136,153,180`).
      **Take the label from the payload node, do not re-compose it** — `Charts.Sunburst`'s wedge `<title>` and the
      explorer breadcrumb both use that exact wording, and a drift here reads as two names for one wedge.
- [ ] 3.2 Card content for an aggregate: title = the payload label; summary = the node's `StatusLabel` prose when
      its label already carries the count (which it does — `WithDetails` deliberately leaves `Detail` empty for
      these kinds); `PrimaryCommand` = **null** (there is no single artifact to act on — the same rule the orphan
      card already follows); `DetailHref` = the group page.
- [ ] 3.3 `epic-N~summary` gets **no card of its own**. Selecting it resolves to its parent epic's card. Do this in
      the **emitter or the projection**, not with a second string-munging rule in JS — the cleanest place is a
      normalization applied where `selectableIslandIds` is consumed, so C# and JS cannot disagree about it.
      Announce it honestly: the live region must not claim a different node was selected than the one activated.
- [ ] 3.4 **The completeness invariant, and it is the headline test of this story (Task 5.1):** every id in the
      explorer payload's *selectable* set (leaves in `select` mode, plus drill scopes) either has a card or has a
      documented, tested redirect. This is the rail's analogue of
      `SunburstExplorerTests.Projector_NodeSet_EqualsTheWedgesTheSvgDrew`, and it is what stops the next payload
      change from silently reintroducing a wedge that selects to nothing.
- [ ] 3.5 Keep `RelatedWorkPaneModel.IsEmpty`'s NFR8 omit gate honest: a project whose chart draws nothing relatable
      still renders **no rail**, not a rail of empty aggregate cards.

### Task 4 — The empty state, the announcement, and the JS-off path (AC: #2, #3)

- [ ] 4.1 With D3 satisfied, `revealRelatedCard`'s empty state should become **unreachable in normal use**. Keep it
      — it is the honest fallback for an id the rail has no card for — but verify it still renders and still
      announces (`"No related work items for X."`) by forcing a synthetic selection in the browser. A designed
      empty state that has silently stopped working is worse than none (UX-DR22).
- [ ] 4.2 Re-check the **one-announcement rule** the 20.3 review established: the explorer's own live region is the
      single authoritative per-activation announcement, and the rail's speaks only for information the explorer
      never conveys (today: the empty-state case). Round 2 added `announce("Selected " + label + ". " + statusLabel
      + ...)` in the component. Confirm in the browser that a story selection produces **exactly one** spoken
      message, not two.
- [ ] 4.3 **AC #2, stated precisely so it is not over-claimed.** Two server-rendered surfaces carry the JS-off path
      and they carry different things: `HierarchyExplorer.TextTwinHtml` is the **chart's** no-JS contract (every
      node's label, prose status, `Detail`, and a resolving `href`); the **rail's own stacked view** — every card
      visible because `data-related-ready` is absent — is where the summary, the command badge and the
      `View details →` link live. Verify **both**, in a genuinely script-blocked context, and say in the Dev Agent
      Record which surface carries which fact. Do not write that "the twin carries the details" if the rail is what
      actually carries them.
- [ ] 4.4 The command disclosure from Task 2.2 must be a **native `<details>`** (or equivalent) so it is openable
      with JS off — a JS-only disclosure would hide the commands from exactly the reader AC #2 protects. Note that
      `[data-related-ready] .related-card-full { display: none; }` exists for the relationship block; if you add a
      parallel rule for the command disclosure, make sure JS-off still reaches it.
- [ ] 4.5 No phantom tab stops. Non-current cards are `hidden`, which the 20.3 round verified empirically drops
      their links from the tab order — re-verify after adding the disclosure and the deferred list, because both
      add focusable nodes inside a card that spends most of its life hidden.

### Task 5 — Tests (AC: #1, #2, #3)

- [ ] 5.1 **The completeness test (Task 3.4).** In `tests/SpecScribe.Tests/RelatedWorkTests.cs`: build the explorer
      payload and the pane from one fixture and assert every selectable id resolves to a card or a named redirect.
      Assert the redirect for `~summary` explicitly rather than letting it pass as "covered".
- [ ] 5.2 D1: a story card carries **no** relationship groups/subjects and emits no `related-card-full` block; its
      epic's card carries that story as a subject; **no relationship entry appears twice** in the rendered rail.
      That last assertion is the one that pins the reversal — a future well-meaning change that "restores" story
      relationships will break it loudly.
- [ ] 5.3 D2: the primary badge is present and is `PrimaryStoryCommand`; the disclosure holds `StoryCommands`
      entries 2..n and **never repeats the primary**; a done story with no commands emits no disclosure; open
      deferred children appear by name with the remainder stated when capped.
- [ ] 5.4 D3: cards exist for each aggregate kind and for the `unplanned` root, with a group-page href and a null
      command; `~summary` has none. Add a regression note naming the `unplanned` gap so it reads as a fix, not
      as new behaviour.
- [ ] 5.5 Byte accounting, asserted as a **reported number, not a threshold** — measure the rail's rendered size
      and the dashboard's total on the real portal and record both in the Dev Agent Record against the 283,263 B /
      742,107 B / 38.2% starting point. A hard-coded byte assertion in a test would be a fixture-shaped lie.
- [ ] 5.6 `SiteGeneratorSpaTests.RelatedWorkPane_SurvivesSpaContentRegionCapture` must still pass; extend it rather
      than adding a parallel case. `SunburstExplorerTests.WebviewAdapter_StripsTheIsland_ButKeepsTheChartAndItsLinks`
      must pass **unchanged** — the pane deliberately stays in the webview (owner-confirmed at 20.3's code review)
      and has no island of its own.
- [ ] 5.7 **Do not unit-test the JS.** This codebase is SSR-first with no JS harness. Task 6 is the verification for
      every client-side claim, and the Dev Agent Record must say so plainly rather than implying coverage.

### Task 6 — Live-browser verification (AC: #1, #2, #3)

- [ ] 6.1 Generate to `SpecScribeOutput/` (never `--output docs/live` — vestigial and gitignored) and serve it
      (`.claude/launch.json` → `specscribe-output`, port 8099). **CLAUDE.md § Verification applies at full force:**
      this epic has now shipped five defects that a 2,300-test suite structurally could not see.
- [ ] 6.2 Select, in this order, and record what the rail shows for each: an **epic** (drill scope) · a **story
      leaf** with edges · a **story leaf with none** · `epic-N~open` · `epic-N~done` · `epic-N~summary` (expect the
      parent epic's card) · the **orphan** root and its aggregates · the **`unplanned` root** and its aggregates.
      Every one must populate. Zero console errors.
- [ ] 6.3 Confirm **the page does not navigate** on any leaf activation (AC #1) and the browser URL changes only by
      the explorer's own `#sb=` fragment semantics.
- [ ] 6.4 Copy a command from a story card's primary badge **and** from inside the disclosure; confirm the
      clipboard carries the literal slash command and that nothing on disk changed (AD-6).
- [ ] 6.5 **JS-off pass in a genuinely script-blocked context** (the sandboxed-iframe technique 20.5 and 20.3 both
      used, `sandbox="allow-same-origin"` with no `allow-scripts`): every card visible, command disclosures
      openable, the deferred lists present, every relationship set present exactly once, the twin complete and
      navigable. Count links rather than eyeballing.
- [ ] 6.6 **Take a screenshot.** 20.4 and 20.5 both owed one and neither could composite a frame — *"the owner has
      still never seen a pixel of this chart."* Try first; if the pane again refuses, say so plainly and fall back
      to computed-geometry evidence. Do not quietly skip it a third time.
- [ ] 6.7 Re-run 20.5's survival predicate after a selection (sectors > 0 · `role="treeitem"` on every sector ·
      non-empty `aria-label` on every sector · exactly one `tabindex="0"`) — the rail's DOM churn happens inside the
      same activation that re-applies the a11y layer.
- [ ] 6.8 **Golden fingerprint.** Read the current constant at the start of your work rather than assuming
      `9dad8c5b` — 20.5 round 2 is uncommitted and moves it again. Regenerate, **confirm stable across two repeated
      runs**, and name whose concurrent changes it sits on top of (CLAUDE.md § Concurrent work).
- [ ] 6.9 Run the full suite and report real numbers. Two git-fixture tests are known to flake under parallel load
      (a different one each run, green in isolation, pre-existing and unclaimed) — distinguish them from anything
      you caused.

### Task 7 — Record the decisions where they belong

- [ ] 7.1 D1's reversal of round 2's fold removal, D2, and D3 land in **`epics.md` under Story 20.8** and in
      **`sprint-status.yaml`** in the same change. A structural decision recorded in only one artifact is a drift
      bug (CLAUDE.md § Decision records).
- [ ] 7.2 No ADR is proposed. D1–D3 are surface decisions inside a design already locked by ADR 0012 §3 and
      ADR 0013 — they change no cross-cutting contract. **If implementation turns up a genuine contract change**
      (for example: the rail's JS-off completeness rule needs relaxing, or the pane needs its own IR shape ahead of
      Epic 22), propose an ADR rather than burying it as an owner-locked story note.

---

### Review Findings

*(populated by code-review at epic end — Epic 20's review runs once every story is complete and the owner is satisfied)*

---

## Dev Notes

### The selectable id space — the whole map, in one table

Derived from `Charts.SunburstExplorerNodes` (`SunburstExplorer.cs:94-196`). Under 20.5's grammar, a node **with
children drills**; a **leaf activates** (and in `select` mode raises a selection without navigating).

| Payload id | Kind | Behaviour | Card today | After this story |
|---|---|---|---|---|
| `__project__` | `project` | drill root; normalized to *no scope* | project card | unchanged |
| `epic-{N}` | `epic` | drills | ✅ | ✅ (regains folded story subjects — D1) |
| `{storyId}` (e.g. `20.5`) | `story` | leaf → select | ✅ (round 2) | ✅ minimal + richer commands (D1, D2) |
| `epic-{N}~summary` | `story-summary` | leaf → select | ❌ empty state | resolves to parent epic (D3) |
| `epic-{N}~open` / `~done` | `aggregate` | leaf → select | ❌ empty state | ✅ (D3) |
| `orphan` | `follow-up` | drills | ✅ | unchanged |
| `orphan~open` / `~done` | `aggregate` | leaf → select | ❌ empty state | ✅ (D3) |
| `unplanned` | `unplanned` | drills | ❌ **empty state — the unnoticed gap** | ✅ (D3) |
| `unplanned~open` / `~done` | `aggregate` | leaf → select | ❌ empty state | ✅ (D3) |

`RelatedWork.IslandIdFor` (`RelatedWork.cs:308`) maps only `WorkNodeKind.Epic` → `epic-{N}`/`orphan` and
`WorkNodeKind.Story` → the bare story id. Everything else reaches the rail only via `selectableIslandIds` +
`SynthesizeNode`, which is why the gaps above exist.

### Architecture compliance

- **ADR 0012 §3** — `select` mode raises a selection without navigating; the dashboard's details pane is that
  mode's named payoff. Drill-in stays a distinct affordance from activation.
- **ADR 0013** — JS-off may lose the *visualization*; it must never lose **information** or **navigation**. Task 4.3
  names exactly which surface carries which fact, so the claim is checkable.
- **ADR 0006 / AD-6** — the command badge is a read-only helper that generates a prompt. Nothing here mutates a
  planning artifact, and no new command vocabulary is authored.
- **ADR 0002 / AD-2** — the pane is host-neutral view-model data built in `DashboardViewBuilder` → `DashboardView`
  → adapter, never string-built inside `HtmlRenderAdapter.Dashboard.cs`. Story 6.2's guardrail; the 21.1 review had
  to patch exactly that.
- **NFR8** — degrade cleanly when data is absent: no rail at all rather than dead chrome; no card rather than an
  empty card; a stated `+N more` rather than silent truncation.
- **UX-DR22** — designed empty states. **UX-DR17/19** — status as a word, never colour alone; the rail has *no*
  colour signal at all, which 20.3 established is a stronger position than a redundant badge.
- **FR31** — generation-time determinism. Explicit ordering lists, never dictionary enumeration order (Task 1.2).

### Anti-patterns to prevent

1. **Re-deriving the story→epic mapping.** `IslandId.IndexOf('.')` is the shipped derivation; reuse it.
2. **Authoring a command string, description, or gating rule outside `BmadCommands`.** The story page, the VS Code
   Quick Pick and this rail must never suggest different next steps.
3. **Repeating the primary command inside the "More actions" disclosure.** The "Story Story 19.1" / "EpicEpic 19"
   defect class — both were found live, neither by a test.
4. **A third phrasing of a story's progress.** The chart tooltip, the twin and the card summary must agree.
5. **Adding a `RelatedWorkTemplater` branch for "story cards have no relationships."** The existing guard already
   handles an empty node; a new branch is a second rule to keep in sync.
6. **Minting a second selection event or a second SPA re-init hook.** `specscribe:explorer-select` and
   `specscribe:content-swapped` both exist and are both adopted.
7. **A second relationship vocabulary.** `RelatedWork.NodeText` / `EdgeVerb` / `Heading` are the single source, and
   `WorkGraphTemplater` already delegates to them.
8. **Manufacturing `covers` / `cites` groups.** `WorkEdgeKind` has four members; the other two are deliberately out
   of Epic 19's MVP draw.
9. **A hard byte threshold in a test.** Measure and report; the fixture is not the portal.
10. **Touching a legend or swatch node from JS.** `StylesheetTests.Script_DoesNotImplementLegendEmphasis` forbids
    the strings `emphasize`, `sunburst-legend`, `sb-legend-item` — *including in comments*.
11. **`git reset --hard` / `git checkout --` / `git clean`.** Another session's uncommitted work is in this tree
    right now. This has already destroyed real work mid-story.
12. **Trusting a line number in this file.** Every one came from an in-flight working copy. Grep first.

### Seams you must adopt, not re-mint

| Seam | Where | Contract |
|---|---|---|
| `specscribe:explorer-select` | published by the component (`specscribe.js` `publishSelection`) | detail `{nodeId, label, root}`; `nodeId` null at root scope |
| `data-sb-scope` | published on the panel root | the DRILL scope (never a leaf selection); the rail re-syncs from it on init, so dropping it silently breaks a deep-linked page |
| `data-related-ready` | set by the rail's init | the CSS hook that flips the rail from the JS-off all-cards view to the single-card view — not merely an init guard |
| `specscribe:content-swapped` | dispatched by `specscribe-spa.js` after every region swap | every content-enhancing block must listen |
| `BmadCommands.PrimaryStoryCommand` / `StoryCommands` / `RenderPrimaryActionBadge` / `RenderCommandMenu` | `BmadCommands.cs:65,101,136,348` | the one command surface; gating lives here, never in a host |
| `RelatedWork.NodeText` / `EdgeVerb` / `Heading` / `IsRestatedContainsGroup` / `AnchorForIslandId` | `RelatedWork.cs` | **the** relationship vocabulary |
| `FollowUpGeometry.DeferredForSource` | `FollowUpGeometry.cs:237` | a story's deferred children — the same call `EpicsViewBuilder.cs:205` uses |
| `StatusStyles.StoryLabel` / `EpicLabel` / `ForStory` | `StatusStyles.cs:26,88,146` | prose status; the six `--status-*` tokens' single source |
| `HierarchyNode.Detail` | `HierarchyExplorer.WithDetails` | the human-meaningful size sentence that replaced reader-facing `weight` |
| `.ss-tooltip` body-level node + `data-tip-html` | `specscribe.js` `SEG` selector | one tooltip system site-wide; CSS `::after` tooltips clip inside `chart-panel` overflow |

### Files being modified — current state

*Verify every line reference before relying on it. This tree is being edited by another session.*

- **`src/SpecScribe/RelatedWorkCards.cs` (~176 lines pre-round-2, larger now) — UPDATE, and it is the centre of this
  story.** Round 2 rewrote `Build` to iterate `selectableIslandIds` (draw order) and added `SynthesizeNode` +
  a story branch in `Resolve`. Tasks 1, 2 and 3 all land here. **Preserve** the `selectableIslandIds` loop, the
  `relationships.Nodes` sweep for undrawn nodes, and `RelatedWorkPaneModel.IsEmpty`'s omit gate.
- **`src/SpecScribe/RelatedWorkTemplater.cs` (~200 lines) — UPDATE (additively).** `RenderPane` at :24,
  `RenderCard` at :73, `AppendAction` at :114, `AppendGroups` at :123, the empty state at :57. The
  `rel.Groups.Count > 0 || rel.Subjects.Count > 0` guard at :89 is what makes Task 1.3 free.
- **`src/SpecScribe/DashboardViewBuilder.cs` — UPDATE (small).** `BuildRelatedWorkHtml` at ~:191 already threads
  `selectableIslandIds: islandIds` (round 2). `BuildHierarchyExplorerHtml` at :125 carries the `select` mode and
  the `DashboardHierarchyDomId` / `DashboardHierarchySize` constants. Its doc-comment still says *"activating a
  story leaf raises a selection with no card"* — **stale after round 2; correct it.**
- **`src/SpecScribe/RelatedWork.cs` (401 lines) — READ, extend only if forced.** `Build` at :123 already folds
  *unwedged* stories into ancestors; the fold this story restores is the **card-layer** one, a different thing in a
  different file. Do not conflate them.
- **`src/SpecScribe/assets/specscribe.js` — UPDATE (small, if at all).** The component block and its `activate()` /
  `publishSelection()` at ~:2081-2134; the rail block and `revealRelatedCard` at ~:2813-2907. Most of this story is
  C#; resist adding client logic.
- **`src/SpecScribe/assets/specscribe.css` — UPDATE (additively).** `.related-card*` at :355-382, the JS-on collapse
  rules at :404-411, `.explorer-layout` at :335. Any new rule for the command disclosure joins that family.
- **`src/SpecScribe/HierarchyExplorer.cs` (516 lines) — READ.** `HierarchyNode` at :41, `WithDetails` at :146,
  `TextTwinHtml` at :414. Touch only if Task 3.3's `~summary` normalization is cleanest here.
- **`tests/SpecScribe.Tests/RelatedWorkTests.cs` — UPDATE.** Already carries a `selectableIslandIds` case (~:419).
- **`src/SpecScribe/WebviewRenderAdapter.cs` — DO NOT TOUCH.** The pane is deliberately in the webview
  (owner-confirmed 2026-07-25) and has no island to strip.

### Project Structure Notes

No new page, no new nav entry, no new asset, no new NuGet package. Likely no new source file — this is a change to
three existing C# files plus tests. If `RelatedWorkCards.cs` grows past comfortable, the command/deferred rendering
is the natural extraction, not the projection.

### Testing standards summary

xUnit, `tests/SpecScribe.Tests`. SSR-first: C# emitters and rendered markup are unit-tested; JS is verified in a
live browser (Task 6) and its *content* asserted by string tests over the shipped asset (`StylesheetTests` is the
established pattern for both CSS and JS guards). Golden fingerprint =
`SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` — read the
current constant, regenerate deliberately, confirm stability across two runs, record provenance.

### Previous story intelligence

**Story 20.5 (`review`, round 2 uncommitted)** — delivered AC #1 early and is the reason this story exists in its
current shape. Its round-2 record carries two things you need: the **measured 283,263 B / 742,107 B / 38.2%** that
D1 answers, and a debugging note worth its weight — *a `transition: stroke-width` made a correctly-applied
selection ring read back through `getComputedStyle` as its START value until frames advanced*, which produced
several wrong conclusions. If a computed style disagrees with what you just set, suspect a transition before
suspecting the code. Round 1's durable hazards still hold: an **SVG `<a>` at `display:none` stays focusable**;
Plotly resolves promises **off an animation frame** so `await Plotly.react(…)` never settles in a non-compositing
tab; CSP violations **do not appear in console captures**.

**Story 20.3 (`review`, owner-redesigned)** — the card rail, `RelatedWorkCards`, the one relationship vocabulary,
and the two live-browser defects the 2,200-test suite could not catch (**"Related work for EpicEpic 19"** and
**"Story Story 19.1"**, both label duplication). Its record also states the fold's rationale, which D1 restores,
and the webview exception, which stands.

**Story 20.6 (`ready-for-dev`, not started)** — its D4 makes the component twin `sr-only` on the dashboard while
`Charts.SunburstCompanionList` stays visible, and D3 adds a **twin-presentation setting on
`HierarchyExplorerConfig` that does not exist yet**. If 20.6 lands first, expect that record to have gained a
field. Neither story blocks the other, but do not both invent that setting.

**Story 20.2 (`done`, 22 review patches)** — story ids come from `### Story N.M:` headings with **no dedupe
anywhere**, so duplicate ids are reachable from authoring input; the projector keeps the first and the card layer
must not assume uniqueness either. `SVGAElement` has no `.click()`.

**Owner workflow (`CLAUDE.md`)** — the post-implementation round where the owner drives the live surface and
comments extensively is the **designed gate**, not rework. Round 2 of 20.5 is precisely that gate producing five
points, one of which turned out to be this story's AC. Expect the same here and leave the card easy to tune.

### Git intelligence summary

Recent commits (`611097d` ← `92fa581` ← `9369ca4` ← `5a96f71`) each bundle several stories — Epic 20 work landed
alongside Epic 5 CLI hardening and Epic 25/26 seeding. That is structural: code review runs at epic end, so
**scope any later review by this story's own File List and declared symbols, never by a commit range.**

`git status` at create-story showed **14 modified files uncommitted**, including `RelatedWorkCards.cs`,
`HierarchyExplorer.cs`, `specscribe.js`, `specscribe.css`, `epics.md`, `sprint-status.yaml` and three test files —
Story 20.5's round 2, plus unrelated edits to `18-1-…md`, `5-3-…md` and `deferred-work.md`. `RelatedWorkCards.cs`
changed **between two reads during this create-story session**. Plan for the same: grep-verify, expect a
transiently broken build from someone else's rename, and **wait rather than reset**.

### Latest technical information

No external dependency changes. plotly.js stays pinned at **3.7.0** (vendored, MIT, `displayModeBar: false` is a
privacy requirement rather than a default — its `sendDataToCloud` button uploads charts to Plotly Cloud). This
story adds no library, no CDN reference, and no Node to the `specscribe generate` path.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 20 → Story 20.8] — the three ACs verbatim **and the
  2026-07-25 amendment recording that AC #1 shipped early in Story 20.5 and what 20.8 still owns**
- [Source: `docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md`] — §3 the
  `navigate` | `select` mode contract, §6 tokens, §7 generation-time determinism
- [Source: `docs/adrs/0013-text-twin-is-the-no-js-contract.md`] — §2 the twin contract, §3 the per-surface live
  JS-off gate
- [Source: `_bmad-output/implementation-artifacts/20-5-hierarchy-explorer-component.md`] — the four blocking
  data-contract defects, the seams table, and **the round-2 owner-verify record carrying the 38.2% measurement**
- [Source: `_bmad-output/implementation-artifacts/20-3-related-work-side-pane-on-selection.md`] — the card rail,
  the fold's rationale, the "Overlap with Story 20.8" section, the webview exception, the two label-duplication
  defects
- [Source: `_bmad-output/implementation-artifacts/20-6-text-twin-audit-and-fingerprint-replacement.md`] — D3/D4 and
  the `HierarchyExplorerConfig` twin-presentation setting that does not exist yet
- [Source: `CLAUDE.md`] — § Concurrent work on shared `main`, § Verification, § Decision records
- Code: `RelatedWorkCards.cs`, `RelatedWorkTemplater.cs:24,57,73,89,114,123`, `RelatedWork.cs:112,123,257,308`,
  `DashboardViewBuilder.cs:101,125,191,201,210`, `HierarchyExplorer.cs:41,146,414`,
  `SunburstExplorer.cs:94,117,136,153,178`, `BmadCommands.cs:65,101,121,129,136,348`,
  `FollowUpGeometry.cs:170,237`, `StatusStyles.cs:26,88,146`, `specscribe.js:2081,2126,2825,2855`,
  `specscribe.css:335,355,404`

### Open questions (non-blocking — recommended answers stated; raise at the owner's verify round)

1. **Does `epic-N~summary` resolving to its parent epic's card read correctly, or as the rail ignoring the click?**
   Recommended as specified (D3), because a `~summary` card would duplicate its epic's almost exactly. If it reads
   as unresponsive, the cheap alternative is a card that says plainly "this is Epic N's stories" and links onward.
2. **Should the "More actions" disclosure be per-card or one shared control?** Recommended per-card (native
   `<details>`, JS-off openable). A shared control would need client state the rail deliberately does not have.
3. **Is `MaxEntriesPerGroup = 12` still right after the fold is restored?** Recommended: leave it. D1 removes the
   duplication, which is the honest fix; if the measured rail is still uncomfortable, 12 → 8 is a one-line change
   and remains the owner's call.
4. **Does the rail want its own scroll container now that cards are numerous?** Not addressed here. Recommended to
   look at it during the verify round with real content rather than pre-emptively.

---

## Dev Agent Record

### Agent Model Used

*(populated by dev-story)*

### Debug Log References

### Completion Notes List

### File List

---

## Change Log

- 2026-07-25 — Story 20.8 drafted (create-story), baseline `611097d`. Context assembled from the live source rather
  than the epic text, which turned out to be essential: **Story 20.5's owner verify round (round 2) is sitting
  uncommitted in the working tree and has already delivered this story's AC #1** — story leaves raise a selection
  without navigating, the rail carries a card per selectable node with `BmadCommands.PrimaryStoryCommand` and a
  View-details link, `SynthesizeNode` covers nodes with no work-graph edges, and `HierarchyNode.Detail` replaced
  reader-facing `weight`. `epics.md` § Story 20.8 was amended in that same change to record it. `RelatedWorkCards.cs`
  changed **between two reads during this session**, so every line reference in this file is flagged as from an
  in-flight working copy and must be grep-verified. **Three owner decisions elicited and locked against the real
  tree, not the epic text: (D1)** the payload ceiling `epics.md` assigns to this story is answered by **restoring
  the story→epic relationship fold that round 2 removed** and making the story card minimal — a reversal of an
  owner-directed decision made hours earlier on worse information, taken because the measurement arrived after it:
  the rail is **283,263 B of a 742,107 B dashboard (38.2%)**, 104 cards, 78 of them stories, up from 21.5%.
  `MaxEntriesPerGroup` stays at 12 — removing duplication is the honest fix, truncating further trades away JS-off
  completeness for every node. **(D2)** "richer" means more *command* and more *children*, not more relationships:
  the primary badge stays visible, the full `BmadCommands.StoryCommands` set sits behind a collapsed native
  `<details>` with the primary never repeated, and the story's open deferred children are listed by name — with the
  explicit instruction that D1 and D2 pull opposite ways and the net must be **measured and reported even if it is
  up**. **(D3)** the follow-up aggregates (`~open`/`~done`, orphan and unplanned) get cards and `~summary` resolves
  to its parent epic instead. D3 also closes a gap **neither 20.3 nor 20.5 noticed and that is live on reviewed
  code**: `RelatedWork.IslandIdFor` maps only Epic and Story kinds, so the **`unplanned` ROOT has no card at all**
  and drilling into Unplanned today shows the rail's empty state. The full selectable-id map is tabled in Dev Notes
  so the gap cannot recur, and Task 3.4 makes it a **completeness invariant test** — the rail's analogue of
  `Projector_NodeSet_EqualsTheWedgesTheSvgDrew`. AC #2 is stated precisely rather than loosely: the chart's twin and
  the rail's own JS-off stacked view carry **different** facts, and the Dev Agent Record must say which carries
  which rather than claiming the twin carries the details. Twelve anti-patterns and a ten-row seams table are
  recorded so the dev adopts existing contracts instead of minting parallel ones, including the two live-only
  label-duplication defects 20.3 found ("EpicEpic 19", "Story Story 19.1") and 20.5 round 2's `getComputedStyle`
  transition trap. The screenshot 20.4 and 20.5 both owed is carried forward as Task 6.6 with an explicit
  instruction not to skip it quietly a third time.
