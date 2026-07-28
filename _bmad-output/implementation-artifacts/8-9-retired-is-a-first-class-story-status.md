---
baseline_commit: b696485
amends_decision: none yet — AC #8 owes ADR 0025 (this story amends Story 8.2's canonical lifecycle vocabulary, a cross-cutting contract)
reopens_epic: 8 # epic-8 was `done` (retro 2026-07-15); reopened 2026-07-28 by owner decision D4 below
owner_decisions: 2026-07-28 # D1 terminal stage · D2 inline demoted card · D3 six-word vocabulary · D4 seed as 8.9, reopen Epic 8
provoked_by: 22-3-static-html-rendered-from-the-ir # the retired story that renders as "Unrecognized" today
---

# Story 8.9: `retired` Is a First-Class Story Status

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer reading the portal after retiring a story,
I want a retired story to read as **Retired** everywhere — badge, counts, epic roll-up, charts and diagnostics — instead of as an unrecognized status word,
So that a deliberate, documented planning decision stops being reported as a defect, and an epic that retires a story can still reach "done".

## The defect, measured in code — READ FIRST

Story 8.2 built **two** status classifiers over **one** shared css vocabulary. They disagree about the word `retired`:

| classifier | reads | `retired` → | evidence |
|---|---|---|---|
| [`StatusStyles.ForSprint`](../../src/SpecScribe/StatusStyles.cs) | `sprint-status.yaml` ledger values | **`"retired"`** — a real stage, with a label, glyph, meaning, legend row, sprint lane, donut handling and a `TrackedStageOrder` bucket | `StatusStyles.cs:292` |
| [`StatusStyles.ForStatus`](../../src/SpecScribe/StatusStyles.cs) | a **story artifact's `Status:` line** | **`"unrecognized"`** | `StatusStyles.cs:48` — the switch has no `retired` arm, so it falls to `ForStatusFromTokens`, whose token set is `review/progress/active/wip/ready/draft` only |

`StoryInfo.Status` **is** the artifact's `Status:` line ([`EpicsModel.cs:44`](../../src/SpecScribe/EpicsModel.cs)), and `ForStory` is just `ForStatus(story.Status)`. So a story file carrying `Status: retired` — [Story 22.3](22-3-static-html-rendered-from-the-ir.md) does, today, deliberately — takes the second row.

### Five consequences, in ascending severity

1. **The badge lies.** `StoryLabel` has no `retired` arm either, so even the css class doesn't save it — the visible word is **"Unrecognized"** on `epics/story-22-3.html` and on the Epic 22 card.
2. **Generation reports a deliberate decision as a problem.** `IsUnrecognizedStatus("retired")` is true, so [`BmadArtifactAdapter.CollectUnrecognizedStoryStatuses`](../../src/SpecScribe/BmadArtifactAdapter.cs) emits an `AdapterDiagnostic(Unsupported)` reading *"Unrecognized status 'retired' — no canonical lifecycle mapping; rendered as unrecognized"*. It lands on the diagnostics page. **SpecScribe's own portal currently flags its own documented planning artifact as unsupported.**
3. **The two count ledgers disagree about the same story.** `ProjectCounts.TrackedStoryStages` (sprint side, `ForSprint`) files 22.3 under **Retired**; `BuildDefinedStoryStages` (epics.md side, iterating `StatusStyles.StoryStages`) files it under **Unrecognized** — `StoryStages` has no `retired` member at all (`StatusStyles.cs:104`). Story 8.3's "every count agrees" invariant is broken by construction, not by a bug in either counter.
4. **Charts paint it as unrecognized.** `HierarchyExplorer.PaintedStatusTokens` (`HierarchyExplorer.cs:332`) lists `unrecognized` but not `retired`, so `PlanningColorClass("retired")` falls to its last-resort `sb-unrecognized`. Same for `Charts.DeliverySentence` / `DeliverySegments`, which iterate `StoryStages`.
5. **⚠ The one that actually bites: an epic that retires a story can never read "done".** `ForEpic` gates on `storyClasses.All(c => c == "done")` (`StatusStyles.cs:115`). A retired story is never `done`, so **Epic 22 will stay off "done" forever** — on the epics index, the epic badge, the Epic Status donut and the sunburst — even after 22.1/22.2/22.4/22.5/22.6 all close. This is not specific to Epic 22; it is a trap for every future epic that retires a story, and it silently propagates into `RequirementsParser.DeriveStatus`, which rolls requirement satisfaction off epic status.

### What already exists — this is routing, not building

The `retired` stage is **already fully designed**. Almost nothing needs inventing:

| already exists | where |
|---|---|
| `retired` in the portal-wide legend roster | `StatusStyles.LegendStages` (`:349`) |
| Human label "Retired" | `SprintLabel("retired")` (`:313`), reached via `LegendWord` (`:392`) |
| Meaning string | `StageMeaning("retired")` = *"Removed from the active plan; kept for ledger history"* (`:340`) |
| Crossed-circle glyph | `Icons.ForStatus("retired")` (`Icons.cs:30`) |
| Badge / lane / card / donut / swatch CSS | `.status-badge.retired` (`specscribe.css:3185`), `.donut-seg.retired` + `.donut-legend .swatch.retired` (`:5348`), `.status-legend-key-swatch.retired` (`:5677`), `.sprint-card.retired` (`:6714`), `.sprint-lane.retired` (`:6806`) |
| Documented token-sharing rationale | `DesignSystemTemplater.cs:137-144` — *"`retired` borrows deferred's… the vocabulary is carried by language, and colour is only ever shorthand for it"* |
| Sprint-side tally bucket | `ProjectCounts.TrackedStageOrder` (`:165`) |

**The gap is exactly the artifact-status half of the seam, plus the roll-up rule.**

### The owner locked four decisions on 2026-07-28 (create-story elicitation)

| # | Decision | Consequence |
|---|---|---|
| **D1** | **`retired` is its own TERMINAL stage**, alongside `done`. An epic whose stories are all done-or-retired reads **done**. Retired stays *in* the roll-up counts — it is not silently dropped from the denominator. | Diverges deliberately from `SprintTemplater`'s delivery-wheel rule, which *excludes* retired from its denominator `M` (`SprintTemplater.cs:473`). **Do not "align" them** — see Trap 3. |
| **D2** | **Inline, visibly demoted card.** The card stays in story order with a grey Retired badge, the crossed-circle glyph and a muted treatment. | Reuses `--status-deferred`; **no 7th `--status-*` token** (holds the Story 9.3 owner decision). Not hoisted into the collapsed `retired-section` — see Scope guard #2. |
| **D3** | **Six-word vocabulary**: `retired`, `superseded`, `deprecated`, `cancelled`, `obsolete`, `wontfix`. | The first three are exactly `EpicsParser.RetirementKeyword`; the last three are the owner's widening. **The two seams must share one list** — see AC #1. |
| **D4** | **Seed as Story 8.9, reopening Epic 8** (`done` since the 2026-07-15 retro). | Structural change → `epics.md` and `sprint-status.yaml` in the same change (CLAUDE.md § Decision records), and an ADR is owed because this amends Story 8.2's canonical lifecycle vocabulary (AC #8). |

## Acceptance Criteria

1. **The retirement vocabulary is ONE list, consumed by both seams.**
   **Given** `EpicsParser.RetirementKeyword` (`retired|superseded|deprecated`) and `StatusStyles.ForStatus` both need to know what retirement means,
   **When** the vocabulary is defined,
   **Then** it lives in **exactly one** authored place and both consume it — a second hand-maintained copy is a finding, not an implementation detail,
   **And** it contains exactly `retired`, `superseded`, `deprecated`, `cancelled`, `obsolete`, `wontfix` (D3),
   **And** `ForStatus` returns `"retired"` for every one of them, in every casing/separator form `ForStatus` already tolerates for its other words — `Retired`, `RETIRED`, `wont-fix`, `wont_fix`, `retired.`,
   **And** the apostrophe form `won't fix` is handled **or** explicitly documented as unsupported with a test pinning that choice (`Normalize` lowercases and swaps spaces for hyphens but does **not** strip apostrophes, so `won't-fix` is what actually arrives).

2. **A retired status is not a diagnostic.**
   **Given** a story artifact whose `Status:` line is any word from AC #1's vocabulary,
   **When** generation runs,
   **Then** `IsUnrecognizedStatus` is false and **no** `AdapterDiagnostic` is emitted for it,
   **And** a genuinely unmapped word (`frobnicated`) still is — the unrecognized path from Story 8.2 AC #3 is narrowed, never removed,
   **And** a test asserts both halves against the same code path.

3. **`retired` is a bucket in the defined-story ledger, and the two ledgers agree.**
   **Given** `StatusStyles.StoryStages` is the partition every defined-story consumer iterates,
   **When** it gains `retired`,
   **Then** `StoryLabel("retired")` returns **"Retired"** (today it falls through `_ =>` to **"Pending"**),
   **And** `ProjectCounts.BuildDefinedStoryStages` files a retired story under `retired`, so for the *same* story the defined tally and the tracked tally (`TrackedStoryStages`, already correct) name the **same** stage,
   **And** the `Debug.Assert` partition invariant at `ProjectCounts.cs:190` still holds,
   **And** `Charts.DeliverySentence` reads e.g. *"5 of 6 done, 1 retired"* rather than *"…1 unrecognized"*.

4. **Epic roll-up: done-or-retired reads done; all-retired reads retired (D1).**
   **Given** `ForEpic`'s `All(c => c == "done")` gate,
   **When** an epic's stories are every one of them `done` or `retired`, with at least one `done`,
   **Then** the epic reads **`done`**,
   **And** an epic whose stories are **all** retired reads **`retired`** — `EpicStages` gains the member so no consumer can silently drop it (the same reason `unrecognized` is already there),
   **And** `ForEpicWithRetrospective`'s retro gate still applies to the done case and does **not** apply to the all-retired case (a fully-abandoned epic is not awaiting a retrospective),
   **And** a test pins Epic 22's actual shape: five live stories + one retired ⇒ `done` once the five close.

5. **The card is inline and visibly demoted, and never colour-alone (D2).**
   **Given** a retired story on `epics.html` and on its epic page,
   **When** the page renders,
   **Then** the card keeps its position in story order and carries the **word** "Retired", the crossed-circle glyph and the `--status-deferred` grey — all three, per UX-DR17,
   **And** `.coverage-story-card.retired` joins the per-stage `border-left-color` family at `specscribe.css:5910-5916`, which has no `retired` member today,
   **And** `HierarchyExplorer.PaintedStatusTokens` gains `retired` with a matching `.sunburst .sb-seg.sb-retired` rule and `.ss-hierarchy-sw.sb-retired` swatch, so a retired wedge stops falling back to `sb-unrecognized` (`HierarchyExplorer.cs:332-345`),
   **And** the webview theme file gets the same treatment as its sibling stages if `--status-deferred` is remapped there,
   **And** **no 7th `--status-*` token is introduced** — if the design appears to need one, that is a signal to bring back to the owner, not a paperwork step.

6. **The golden fingerprint moves, and the delta is enumerated rather than re-blessed.**
   **Given** 22.3's badge sits on golden-covered pages (`epics.html`, `epics/epic-22.html`, `epics/story-22-3.html`),
   **When** the suite runs,
   **Then** `GoldenContentFingerprint` **is expected to move**, and the mover set is enumerated **page-by-page with the reason** before regeneration,
   **And** every mover is explained by this story (a badge word/class, a roll-up class, or a delivery sentence) — **any unexplained mover is a defect to diagnose, not a constant to re-bless**,
   **And** the regenerated hash is confirmed **stable across two repeated runs**, with the concurrent session's changes it sat on top of named in the story record (CLAUDE.md § Verification).

7. **Verified in a live browser, JS off and on.**
   **Given** ADR 0013 §Decision 3 requires a real browser rather than a test assertion,
   **When** the regenerated site is opened,
   **Then** the retired card, the epic badge, the legend row and the sunburst wedge are checked **rendered** — naming the pages checked and the mechanism used to block scripts,
   **And** the sunburst's accessible text equivalent names the retired story's stage in words (CLAUDE.md § Verification: every chart needs a text equivalent).

8. **ADR 0025 is proposed.**
   **Given** this amends Story 8.2's canonical lifecycle — a cross-cutting contract — and CLAUDE.md § Decision records requires an ADR rather than an owner-locked note in a story file,
   **When** the story completes,
   **Then** ADR **0025** is proposed recording: the two-classifier seam and why `retired` belongs to both, the six-word vocabulary and its single-source rule, `retired` as a terminal stage in the epic roll-up, and the deliberate divergence from `SprintTemplater`'s excluded denominator,
   **And** it is cross-referenced from `docs/adrs/README.md` and `epics.md`,
   **And** it uses **0025** — `0019` is **not** free (still claimed-but-unwritten by Story 18.3; see ADR 0021's numbering note).

9. **The structural change lands in both artifacts.**
   **Given** Epic 8 was `done` and is being reopened for this story (D4),
   **When** the change lands,
   **Then** `epics.md` § Epic 8 carries Story 8.9 **and** a dated note recording the reopening, and `sprint-status.yaml` carries `epic-8: in-progress` plus the `8-9-…` key — **in the same change** (CLAUDE.md § Decision records),
   **And** `epic-8-retrospective` is addressed explicitly: either re-opened or left `done` with a stated reason.

## Tasks / Subtasks

- [ ] **Task 1 — Define the shared retirement vocabulary (AC #1).**
  - [ ] One authored list. Decide its home deliberately: `StatusStyles` is the classification seam and `EpicsParser` already depends on `StatusStyles`' vocabulary conceptually, so exposing it from `StatusStyles` and having `EpicsParser.RetirementKeyword` build its `Regex` from it is the shape that matches the file. Do **not** leave two hand-maintained lists.
  - [ ] Add the `ForStatus` arm. Put it in the **exact-match switch** (`StatusStyles.cs:48`), not in `ForStatusFromTokens` — the token fallback exists to avoid substring traps, and a bare word list belongs above it.
  - [ ] ⚠ Widening `EpicsParser.RetirementKeyword` from 3 words to 6 changes **which HTML comments get hoisted** into an epic's `RetiredNoticesHtml`. Check the existing corpus for a comment containing `cancelled`/`obsolete`/`wontfix` that would newly divert, and enumerate any under AC #6.
  - [ ] Settle the `won't fix` apostrophe question with a test either way.
- [ ] **Task 2 — Stop the diagnostic (AC #2).** `IsUnrecognizedStatus` follows `ForStatus`, so this should need no edit of its own — **verify that, do not assume it.** Add the paired test (retired → no notice; `frobnicated` → notice).
- [ ] **Task 3 — Add the `retired` bucket to the defined-story ledger (AC #3).**
  - [ ] `StatusStyles.StoryStages` += `retired`. Place it deliberately in narrative order — after `done`, since D1 makes it terminal.
  - [ ] `StatusStyles.StoryLabel` += `"retired" => "Retired"`. **Without this the class lands in the `_ =>` arm and prints "Pending"** — a silent mislabel worse than the current one.
  - [ ] Update `StatusStylesTests.StoryLabel_MapsEachStage` (`:146`) and `StoryStages_IncludesUnrecognized` (`:361`); add `ForStatus_MapsRawStatusText` rows (`:157`).
  - [ ] Confirm `ProjectCounts.cs:190`'s partition `Debug.Assert` still holds, and that defined vs tracked now name the same stage for one story.
- [ ] **Task 4 — The epic roll-up rule (AC #4). This is the highest-value change in the story.**
  - [ ] `ForEpic`: all done-or-retired (≥1 done) → `done`; all retired → `retired`; retired otherwise does not block the `active`/`ready` tiers.
  - [ ] `EpicStages` += `retired`, so the Epic Status donut and every roll-up consumer has a bucket (`StatusStyles.cs:143`).
  - [ ] `EpicLabel` += `"retired" => "Retired"` — same `_ =>` "Pending" trap as `StoryLabel`.
  - [ ] Check `ForEpicWithRetrospective` (`:133`) does not turn an all-retired epic into `review`.
  - [ ] ⚠ Trace the change into `RequirementsParser.DeriveStatus`, which rolls requirement satisfaction off epic status — an epic newly able to read `done` moves requirement badges too. Expected and correct; **enumerate the movers** under AC #6.
- [ ] **Task 5 — Visual treatment (AC #5).**
  - [ ] `.coverage-story-card.retired` border-left (`specscribe.css:5910-5916`).
  - [ ] `HierarchyExplorer.PaintedStatusTokens` += `retired` + `.sunburst .sb-seg.sb-retired` + `.ss-hierarchy-sw.sb-retired` (`specscribe.css:390` is the sibling pattern).
  - [ ] The muted card treatment per D2. Reuse `--status-deferred`; do not add a token.
  - [ ] Check `specscribe-webview-theme.css` — its `.vscode-dark` / `.vscode-high-contrast` blocks remap `unrecognized` explicitly (`:233`, `:249`); confirm whether `retired`/`deferred` needs the same and say so either way.
  - [ ] `DesignSystemTemplater`'s `retired` note (`:137-144`) already says the right thing — **verify it still reads true after the change** rather than editing it reflexively.
- [ ] **Task 6 — Verify (AC #6).**
  - [ ] `dotnet test SpecScribe.slnx`. Expect the golden fingerprint to move; enumerate the mover set page-by-page **before** regenerating.
  - [ ] Re-run to confirm the new hash is stable across two runs; name the concurrent session's changes it sits on.
  - [ ] `npm run check:links`, `npm run measure:parity`, `npm run check:ir-content` under `web/` — a changed badge class is exactly what ADR 0018's class/id-bound extraction gate is built to catch.
- [ ] **Task 7 — Live-browser verification, JS off and on (AC #7).**
- [ ] **Task 8 — Propose ADR 0025 and cross-reference it from `docs/adrs/README.md` and `epics.md` (AC #8).**
- [ ] **Task 9 — Record the structural change in `epics.md` AND `sprint-status.yaml` in the same change (AC #9).** *(Both files were already seeded at create-story — verify they are still consistent at dev time rather than re-applying.)*

## Dev Notes

### Scope guard — five things this story is NOT

1. **Not a change to `ForSprint`.** The yaml ledger classifier is **already correct** and must stay untouched. ⚠ It is also load-bearing for a *different* surface: `FreeTextBadge` (`StatusStyles.cs:216`) calls `ForSprint` first, and only falls through to the slugged `.pill.status-*` degradation when it returns `unrecognized`. Teaching `ForSprint` the word `superseded` would flip an ADR whose status line is exactly `Superseded` from the muted strikethrough pill (`specscribe.css:1249-1250`) to a canonical Retired badge. No ADR carries a bare `Superseded` today — every one reads `Accepted` — so this is latent, not live. **Keep it latent.**
2. **Not a move into the collapsed `retired-section`.** That `<details>` (`HtmlRenderAdapter.Epics.cs:239-248`) collects pre-rendered *notice HTML* hoisted from epics.md **comments** — a different mechanism from a story card, and 22.3 is a full story section with a blockquote banner, not a comment. D2 chose the inline demoted card; hoisting cards into that section is a larger change and was not chosen.
3. **Not a change to `AdrAccentToken`.** It already maps `retired`/`superseded`/`deprecated`/`obsolete` → `deferred` for the ADR list-row accent (`:245`), and it reads a **different vocabulary** (an ADR's free-text status) on purpose — its own doc comment says so of `IdeaAccentToken`. Note it also includes `rejected`, which is deliberately **not** in D3's story vocabulary. Leave the divergence; record it in ADR 0025.
4. **Not a re-litigation of the `--status-*` token count.** D2 holds the Story 9.3 owner decision: no 7th token.
5. **Not a revival of Story 22.3.** 22.3 stays retired (owner decision D4, 2026-07-27). This story is what makes its retirement *render honestly*; it changes nothing about its scope, and the file's retention banner is load-bearing for [Story 23.4](23-4-migrate-remaining-surfaces-retire-c-sharp-html-adapter.md) — **do not edit `22-3-*.md`.**

### Trap 1 — `StoryLabel`'s and `EpicLabel`'s `_ =>` arm is "Pending", not "Unknown"

Both switches end `_ => "Pending"`. Adding `retired` to `StoryStages`/`EpicStages` **without** adding the matching label arm produces a *worse* bug than today's: a retired story would carry `class="status-badge retired"` with the visible word **"Pending"** — grey chrome, active-plan language, and no test would necessarily catch it because most assertions check the class, not the word. Add the class and the label in the same edit.

### Trap 2 — three roll-up call sites read `ForStory` with an implicit "not done means outstanding" assumption

`DashboardViewBuilder.cs:356-361` buckets Now & Next by `active`/`review`/`ready`/`drafted`; `BmadCommands.cs:572/627/641` picks the next actionable story the same way. A retired story matches **none** of those buckets today (it is `unrecognized`) and must match none of them after (it is `retired`) — so these sites should need **no change**. **Verify that rather than assuming it**, because the failure mode is a retired story being offered as the next unit of work, which is exactly the thing this story exists to prevent.

### Trap 3 — `SprintTemplater` excludes retired from its denominator; D1 says the epic roll-up includes it

`SprintTemplater.DeliveryWheel` computes `M` over `stages.Where(c => c.CssClass != "retired")` with the comment *"ledger history must not inflate incomplete work"* (`:463-473`). D1 makes the **epic** roll-up treat retired as a counted terminal stage instead. **These two rules are deliberately different and both are right**: the sprint wheel answers "how much of the active plan is done", the epic roll-up answers "is this epic closed". A dev who notices the asymmetry and "fixes" it will break one of them. ADR 0025 owes this distinction in writing (AC #8).

### Trap 4 — the golden fixture does not cover the git-derived surfaces

`git-insights.html`, `impact-map.html`, `timeline.html` and `commits/` are **absent** from the golden fixture (honest-scope-limit comment at `HierarchyExplorerTests.cs:615-633`). A green — or a correctly-moved — fingerprint says nothing about them. This story touches the sunburst colour roster, which reaches the dashboard; verify those surfaces separately.

### Trap 5 — `StoryStages` has a mirror test

`SiteGeneratorOutlineTests.StoryStages_MatchStatusStyles` (`:208`) and `ProjectCountsTests` (`:139`, `:158-162`) both assert partition sums over the stage lists. `ProjectCountsTests:162` currently asserts the *tracked* `unrecognized` bucket is **0** while a retired story sits in the tracked `retired` bucket — that test is already the proof that the sprint half works, and it is the natural template for the defined half's new assertion.

### Trap 6 — `Icons.ForStatus("retired")` and `("deferred")` are byte-identical glyphs

Both are the same crossed circle (`Icons.cs:24` and `:30`), by design — the comment says *"class+label keep retired distinct"*. So an assertion of the form `Assert.Contains(Icons.ForStatus("retired"), html)` **cannot distinguish retired from deferred**. Assert on the class and the word, or the test is vacuous. This is the same vacuous-assertion class that produced 22.4's `822/822 identical` false pass.

### The blast radius: every `ForStory` consumer

`ForStory` is just `ForStatus(story.Status)`, so **every one of these sites changes behaviour for a retired story**. Walk the list; do not sample it.

| site | what it does with the class | expected after this story |
|---|---|---|
| `EpicsViewBuilder.cs:260/309/432` | `StatusStage` on the story card / list row view models | reads `retired`; the card renders demoted (AC #5) |
| `EpicsViewBuilder.cs:358` | `storyStatusClass:` passed into a row builder | same |
| `EpicsViewBuilder.cs:514-528` | picks the epic's "current" story (`active`/`review`, else `ready`) | **unchanged** — retired matches neither. Verify. |
| `EpicsTemplater.cs:173/280` | the second (non-`PageView`) card path | must match `EpicsViewBuilder`'s output |
| `SiteGenerator.cs:3193` | `StoriesDone = Count(ForStory == "done")` | **unchanged** — retired is not done, and D1 does not make it done at the *story* level |
| `SiteGenerator.cs:3201` | per-story `storyStage` for the outline/IR | reads `retired` |
| `Charts.cs:3453` | *"{id} ({StoryLabel}) — {title}"* text equivalent | reads *"(Retired)"* — **this is a chart text twin; AC #7 covers it** |
| `Charts.cs:790/794/808` | `DeliverySentence` + `DeliverySegments` over `StoryStages` | gains a retired segment (AC #3) |
| `HierarchyExplorer.Projectors.cs:96`, `SunburstExplorer.cs:112` | `noPlan ? "noplan" : ForStory(story)` | needs `PaintedStatusTokens` + CSS (AC #5) |
| `RelatedWorkCards.cs:267` | related-work card rail stage | reads `retired`; confirm the rail has a swatch for it |
| `RequirementsTemplater.cs:397` | story chip on a requirement page | reads `retired` |
| `RetroTemplater.cs:113` | story class on a retrospective page | reads `retired` |
| `DeliveryCadence.cs:57` | `Where(ForStory == "done")` for cycle-time | **unchanged** — a retired story never shipped, so it must stay out of cadence. Verify. |
| `DashboardViewBuilder.cs:356-361`, `BmadCommands.cs:42/69/105/572/627/641` | Now & Next buckets + next-step commands | **unchanged** — see Trap 2 |

### Trap 7 — `ForStatus` also reads quick-dev doc frontmatter

Its own doc comment (`StatusStyles.cs:28`) says it maps *"a story's, **or a quick-dev doc's frontmatter status**"*. Widening the vocabulary to six words therefore changes how a quick-dev doc whose frontmatter reads `superseded`/`obsolete`/`cancelled` classifies — from `unrecognized` to `retired`. That is the correct outcome and is **in scope**, but it is a second surface the ACs do not name explicitly: check the quick-dev corpus for such a doc and enumerate any mover under AC #6.

### Trap 8 — after this change the two classifiers are asymmetric *the other way*

`ForStatus` will know all six words; `ForSprint` will still know only `retired` (the one value `sprint-status.yaml` actually uses). **That asymmetry is correct and deliberate** — `ForSprint` reads a closed set of yaml ledger values, `ForStatus` reads free text authored by a human — and scope guard #1 explains what breaks if you "fix" it. Record it in ADR 0025 so the next reader does not rediscover it as a bug.

### Architecture invariants that bound this work

| Invariant | What it requires here |
|---|---|
| **Story 8.2 / FR20** | Native vocabulary → canonical lifecycle mapping lives in `StatusStyles`, never in a templater. This story extends that seam; it must not push a keyword into a rendering site. |
| **Story 8.2 AC #3** | An unmapped native word still renders visibly `unrecognized` with a non-fatal notice. This story **narrows** that path (retired is now mapped); it must not remove it. |
| **Story 8.3** | Every count derives from one generator-side source. AC #3 is precisely restoring that invariant across the defined/tracked pair. |
| **UX-DR17 / CLAUDE.md § Verification** | No state signalled by colour alone; every chart needs a text equivalent. Retired carries colour **+** glyph **+** word, and the sunburst's text twin must name it. |
| **Story 9.3 owner decision #1** | No 7th `--status-*` token. Retired shares `--status-deferred`, as the design-system page already documents. |
| **ADR 0013 §Decision 3** | Live-browser JS-off verification, not a test assertion (AC #7). |
| **ADR 0018** | The `ir-content.css` extraction is class/id-bound — a new `.retired` modifier on an emitted region turns `check:ir-content` red until re-extracted. |
| **NFR8** | Graceful degradation: an absent status still means `drafted` with no notice. Unchanged. |

### Test gates, ranked by how likely this change trips them

1. **`GoldenContentFingerprint`** — `SiteGeneratorAdapterTests.cs:237`, constant at `:1107`. **Expected to move.** Read the current value from the file, never from a story record: it moved twice during Epic 22/23 alone. The ~850-line comment block above the constant is the regeneration audit trail — follow its ritual, and enumerate this story's movers into it.
2. **`StatusStylesTests`** — `:146` (`StoryLabel_MapsEachStage`), `:157` (`ForStatus_MapsRawStatusText`), `:342` (`IsUnrecognizedStatus_…`), `:361` (`StoryStages_IncludesUnrecognized`). All four are directly in scope; extend them, do not replace them.
3. **`ProjectCountsTests`** — `:139`, `:158-162`. Partition sums plus the existing tracked-side retired assertion.
4. **`SiteGeneratorOutlineTests`** — `:208` (`StoryStages_MatchStatusStyles`), `:217` (`EpicStages_UseRetroGatedClassifier`) — the latter is where AC #4's retro-gate carve-out gets pinned.
5. **`EpicsParserTests`** — `:384` (`[InlineData("RETIRED")]` and siblings) exercise `RetirementKeyword` case-insensitivity; widening the word list belongs here.
6. **`IconsTests`** — `:14-17` enumerates known css classes; `retired` should join it.
7. **`RenderParity` / `SpaDelivery` / `CanonicalIrSerialization`** — a changed badge class ships into the IR region. **These are not in the documented flake family** and must be treated as real.
8. **The `HostRenderExceptions.Registry` ceiling.** Four hygiene tests cap it: `WebviewRenderAdapterTests.cs:403` (exactly 4 webview entries), `RenderSpaParityTests.cs:197` (exactly 1 spa entry), `RenderParityTests.cs:207` (**zero** `html` entries), `RenderSectionParityTests.cs:303` (**never** a `section.*` entry). **This story may not add a single new exception** — a status class is shared vocabulary, so if one surface needs a carve-out for `retired`, that is a design signal to bring back, not a paperwork step.

**Flake discipline.** A red `SiteGenerator*` generate-to-disk test should be re-run **in isolation** before being called a regression — the documented rotating file-write-contention family (`FileWatcherServiceTests.BurstOfSaves`, `SiteGeneratorTimelineTests` ×3, `SiteGeneratorCodeMapTests` determinism, `SiteGeneratorGitInsightsTests` hub, `SiteGeneratorReadmeTests`, `SiteGeneratorImpactMapTests`, `SiteGeneratorGroupedNavTests`). A red `RenderParity*` / `SpaDelivery*` / `GoldenContentFingerprint` is **not** in that family.

### Previous-story intelligence

**From Story 8.2 (the story this one amends):** it built the two-classifier design on purpose — `ForStatus` reads free text from an artifact, `ForSprint` reads a closed set from yaml — and its AC #3 deliberately chose "visible unrecognized + non-fatal notice" over silent coercion. **That design is right and this story keeps it.** The defect is not the two classifiers; it is that a word promoted to first-class in one was never promoted in the other.

**From Story 9.3:** owner decision #1 — Unmapped reuses `--status-pending` rather than earning a 7th token, staying distinct by icon + word. D2 applies the identical principle to retired, which is why the CSS already exists.

**From Story 21.1's review (2026-07-22):** the closest prior defect in kind — a phantom-covered requirement was *classified* covered by the ledger but *rendered* as a silent blank row, because the matrix classified locally instead of routing through the shared classifier. Same shape as this one: a state that exists in one seam and not the other. The fix there was to route through the shared source and add a caution badge, explicitly **without** a 7th token.

**From Story 22.4 (delivered 2026-07-28):** *"Do not trust a bundle diff without asserting the field name exists"* — the first parity attempt reported a vacuous `822/822 identical` because it compared `undefined` to `undefined`. Trap 6 above is the same hazard in this story's test surface.

### Git intelligence

- Baseline `b696485` ("Morning batch"); working tree otherwise clean, with one untracked file from a concurrent session (`25-4-agent-consumable-findings-channel.md`).
- ⚠ **Concurrent sessions are active on Epics 22/23/25.** Stories 22.2, 22.4, 23.2, 23.3, 23.5 all sit in `review` and 23.4 is `ready-for-dev` — 23.4 in particular rewrites how every page is rendered. **Grep-verify every symbol you add before relying on it** (CLAUDE.md § Concurrent work); a `Charts.cs` edit has silently vanished this way before.
- Commits routinely bundle several stories (`261b300` carried 20.5, 20.7, 22.2, 23.2). **Scope any later review by this story's File List and symbols, never by a commit range** (CLAUDE.md § Scoping a code review).
- CI is live (`build-test-analyze`, Story 25.1) on Windows and Ubuntu; the Sonar quality gate is enforced (Story 25.2).

### Project Structure Notes

- Production code: `src/SpecScribe/` (single project, .NET 10). Tests: `tests/SpecScribe.Tests/` (flat, xUnit). `SpecScribe.slnx` has exactly two projects — nothing new joins it.
- **No new NuGet dependencies.** This is an internal classification change; the package set (`Markdig`, `Spectre.Console`, `Spectre.Console.Cli`, `YamlDotNet`) is unchanged, so no external version research applies to this story.
- ADRs live in `docs/adrs/` with a `README.md` index. **Next free number is 0025** — `0019` is claimed-but-unwritten by Story 18.3 (ADR 0021's numbering note records this), `0023` went to Story 25.3 and `0024` to Story 22.4.
- Generate to `SpecScribeOutput/` (the default). Never `--output docs/live`.
- Epic 8 is being reopened from `done` for this story; `epic-8-retrospective` is `done` and AC #9 requires an explicit decision about it.

### References

- [epics.md § Epic 8](../planning-artifacts/epics.md) — the epic goal and Story 8.2's original ACs; § Story 8.9 is this story's entry.
- [Story 8.2](8-2-canonical-status-model-with-portal-wide-legend.md) — the canonical status model this story amends; AC #3 is the unrecognized-path contract being narrowed.
- [Story 8.3](8-3-single-source-of-truth-for-every-count.md) — the "every count agrees" invariant broken by the defined/tracked disagreement.
- [Story 22.3](22-3-static-html-rendered-from-the-ir.md) — the retired story that surfaced this. **Read-only for this story.**
- [`StatusStyles.cs`](../../src/SpecScribe/StatusStyles.cs) — `ForStatus` (`:34`), `StoryStages` (`:104`), `ForEpic` (`:110`), `EpicStages` (`:143`), `ForSprint` (`:277`), `StageMeaning` (`:326`), `LegendStages` (`:349`).
- [`BmadArtifactAdapter.cs`](../../src/SpecScribe/BmadArtifactAdapter.cs) — `CollectUnrecognizedStoryStatuses`, the diagnostic AC #2 silences.
- [`ProjectCounts.cs`](../../src/SpecScribe/ProjectCounts.cs) — `TrackedStageOrder` (`:158`), `BuildDefinedStoryStages` (`:294`).
- [`HierarchyExplorer.cs`](../../src/SpecScribe/HierarchyExplorer.cs) — `PaintedStatusTokens` (`:332`), `PlanningColorClass` (`:342`).
- [`DesignSystemTemplater.cs`](../../src/SpecScribe/DesignSystemTemplater.cs) — `:137-144`, the documented token-sharing rationale.
- [`EpicsParser.cs`](../../src/SpecScribe/EpicsParser.cs) — `RetirementKeyword` (`:38`), the second half of AC #1's shared vocabulary.
- [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) — §Decision 3 (live JS-off browser gate).
- [ADR 0018](../../docs/adrs/0018-transitional-ir-content-style-layer.md) — the class/id-bound extraction gate.
- [ADR 0021](../../docs/adrs/0021-carrying-foreign-artifacts-verbatim-into-the-portal.md) — its numbering note is the evidence that `0019` is not free.
- [CLAUDE.md](../../CLAUDE.md) — § Concurrent work on shared `main`, § Decision records, § Verification, § Scoping a code review.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Note |
|---|---|
| 2026-07-28 | Story created (baseline `b696485`), **reopening Epic 8** (`done` since its 2026-07-15 retrospective) per owner decision D4. Provoked by [Story 22.3](22-3-static-html-rendered-from-the-ir.md) rendering as **"Unrecognized"**: `StatusStyles.ForSprint` maps `retired` to a first-class stage while `StatusStyles.ForStatus` — the classifier that actually reads a story artifact's `Status:` line — has no arm for it and falls through to `unrecognized`. Five measured consequences, the load-bearing one being that `ForEpic`'s `All(c => c == "done")` gate means **an epic that retires a story can never read done**. Owner locked four decisions: **D1** retired is a terminal stage (done-or-retired ⇒ done; all-retired ⇒ retired), **D2** inline visibly-demoted card reusing `--status-deferred` with no 7th token, **D3** a six-word vocabulary (`retired`/`superseded`/`deprecated`/`cancelled`/`obsolete`/`wontfix`) shared as ONE list with `EpicsParser.RetirementKeyword`, **D4** seed as 8.9 and reopen Epic 8. The golden fingerprint is **expected to move** (AC #6) — 22.3's badge is on golden-covered pages — and the delta must be enumerated page-by-page rather than re-blessed. ADR **0025** owed (`0019` is not free — still claimed by Story 18.3). |
