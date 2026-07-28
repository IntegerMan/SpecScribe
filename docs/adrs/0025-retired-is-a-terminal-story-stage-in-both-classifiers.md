# ADR 0025: `retired` Is a Terminal Story Stage, and the Two Status Classifiers Are Deliberately Asymmetric

**Status:** Proposed (authored 2026-07-28 by Story 8.9; ratification is the owner's)
**Date:** 2026-07-28
**Deciders:** Matthew-Hope Eland
**Relates to:** Story 8.2 (the canonical status model this record **amends** — its two-classifier design is kept, its unrecognized path is narrowed, not removed); Story 8.3 (the "every count derives from one source" invariant this restores); Story 9.3 (owner decision #1 — no 7th `--status-*` token, held here); [ADR 0013 — The Text Twin Is the No-JS Contract](0013-text-twin-is-the-no-js-contract.md) (§3's live-browser gate, exercised); [ADR 0018 — Transitional `ir-content` Style Layer](0018-transitional-ir-content-style-layer.md) (the class-bound extraction a new stage modifier moves); Epic 8 (reopened for Story 8.9), Epic 22 (the epic that could not reach `done`)

## Context

Story 8.2 built **two** status classifiers over **one** shared CSS vocabulary, on purpose:

- `StatusStyles.ForSprint` reads a **closed set** of `sprint-status.yaml` ledger values.
- `StatusStyles.ForStatus` reads **free text** — a story artifact's `Status:` line, or a quick-dev doc's frontmatter.

That split is right, and this record keeps it. The defect was narrower: **a word promoted to first-class in one classifier was never promoted in the other.** `ForSprint` mapped `retired` to a real stage with a label, glyph, meaning, legend row, sprint lane, donut handling and a tally bucket. `ForStatus` had no arm for it at all, so a story file carrying `Status: retired` fell through to `unrecognized`.

Story 22.3 was retired on 2026-07-27 and carries exactly that line. Five consequences followed, none of them cosmetic:

1. **The visible word was "Unrecognized"** wherever a surface labelled a story by its stage — the sunburst, its text twin, the related-work rail, the requirements coverage card, the delivery sentence, the IR outline.
2. **Generation reported a documented planning decision as a problem.** `IsUnrecognizedStatus("retired")` was true, so `BmadArtifactAdapter.CollectUnrecognizedStoryStatuses` emitted an `AdapterDiagnostic(Unsupported)` onto the diagnostics page. SpecScribe's own portal flagged SpecScribe's own artifact as unsupported.
3. **The two count ledgers disagreed about the same story.** `ProjectCounts.TrackedStoryStages` (yaml side) filed 22.3 under **Retired**; `BuildDefinedStoryStages` (epics.md side, iterating `StatusStyles.StoryStages`) filed it under **Unrecognized**, because `StoryStages` had no `retired` member. Story 8.3's invariant was broken **by construction**, not by a bug in either counter.
4. **Charts painted it as unrecognized.** `HierarchyExplorer.PaintedStatusTokens` did not list `retired`, so `PlanningColorClass` fell to its last-resort `sb-unrecognized`.
5. **⚠ The load-bearing one: an epic that retired a story could never read `done`.** `ForEpic` gated on `storyClasses.All(c => c == "done")`. A retired story is never `done`, so Epic 22 would have stayed off `done` **forever** — on the epics index, the epic badge, the Epic Status donut and the sunburst — even after every live story closed. This is not specific to Epic 22; it was a trap for every future epic that retires a story, and it propagated into `RequirementsParser.DeriveStatus`, which rolls requirement satisfaction off epic status.

Almost none of the `retired` *stage* needed inventing — the legend roster, label, meaning, glyph, badge/lane/card/donut/swatch CSS and the sprint tally bucket all already existed. **The gap was the artifact-status half of the seam, plus the roll-up rule.**

## Decision

**1. `retired` is a canonical lifecycle stage reachable from BOTH classifiers, and it is TERMINAL.**

It joins `StatusStyles.StoryStages` immediately after `done` — the second terminal stage, with everything below it being work still owed. It joins `EpicStages` for the same reason `unrecognized` is already there: `ForEpic` can now return it, and a consumer that buckets by iterating the list would otherwise draw nothing.

A stage added to a partition list **must** gain its label arm in the same edit. `StoryLabel` and `EpicLabel` both end `_ => "Pending"`, so a class without a word renders active-plan language under a terminal badge — a quieter mislabel than the one being removed. `StatusStylesTests.StoryLabel_AndEpicLabel_CoverEveryStageTheirOwnListDeclares` enforces this for any future stage.

**2. The epic roll-up is done-or-retired ⇒ done; all-retired ⇒ retired.**

```
All(done or retired) → Any(done) ? "done" : "retired"
```

Retired stays **in** the roll-up rather than being dropped from the denominator. An all-retired epic reads `retired`, not `done`: abandoned is not delivered, and no surface may claim otherwise.

Retired is **neutral** for the lower tiers — it never lifts a live epic, and it is excluded from the all-unrecognized test rather than blocking it. Terminal ledger history says nothing about whether the *rest* of an epic is merely unmapped, and letting one retired story downgrade an otherwise all-unrecognized epic to `drafted` would hide the notice Story 8.2 AC #3 exists to raise.

`ForEpicWithRetrospective`'s retro gate keys on `done` alone, so the all-retired tier passes through untouched: **a fully-abandoned epic is not awaiting a retrospective**, and reading it "In review" would put it back on the list of things someone owes work on.

**3. This divergence from `SprintTemplater.DeliveryWheel` is deliberate, and both rules are right.**

`SprintTemplater.DeliveryWheel` computes its denominator `M` over `stages.Where(c => c.CssClass != "retired")`, commented *"ledger history must not inflate incomplete work"*. Decision 2 does the opposite for the epic roll-up.

They answer **different questions**:

| surface | question | retired is |
|---|---|---|
| sprint delivery wheel | *how much of the active plan is done?* | **excluded** — it is not active plan |
| epic roll-up | *is this epic closed?* | **counted** — it is a closed outcome |

A reader who notices the asymmetry and "aligns" them will break one of the two. This paragraph exists so that reader stops here instead.

**4. The retirement vocabulary is SIX words, authored in exactly ONE place.**

`StatusStyles.RetirementStatusWords` = `retired`, `superseded`, `deprecated`, `cancelled`, `obsolete`, `wontfix` (owner decision D3, 2026-07-28). `ForStatus` maps every one to `retired`; `EpicsParser.RetirementKeyword` **builds its regex from this same array** rather than keeping the hand-maintained three-word copy it had. A second list is a finding, not an implementation detail — a second list is how the two halves of the retirement question came to disagree in the first place.

The match is on the whole normalized word, checked **before** the token fallback and never inside it: the token pass exists to avoid substring traps, not to grow a second word list. `not-retired` and `retired?maybe` stay `unrecognized`.

Separator and apostrophe forms are **supported, not merely tolerated**. `Normalize` lowercases and kebabs but leaves apostrophes, so `won't fix` arrives as `won't-fix`; the comparison strips `-`, `'` and `’` before matching, making the six authored words cover `wont fix` / `wont_fix` / `WontFix` / `won't fix` / `won’t fix`. A smart-quoting editor must not silently change a status line's meaning.

The two seams are asymmetric on punctuation, and that is stated rather than accidental: the regex side matches each word on its own boundaries and does **not** accept the spaced `won't fix`, because it reads authored *prose about* a story where the canonical spelling gets written, while `ForStatus` reads a machine-ish `Status:` value a human may punctuate freely.

**5. `ForSprint` stays narrower than `ForStatus`, permanently.**

After this change `ForStatus` knows all six words and `ForSprint` knows only `retired` — the one value `sprint-status.yaml` actually uses. That asymmetry is correct and must not be "fixed":

`StatusStyles.FreeTextBadge` calls `ForSprint` **first**, falling through to the slugged `.pill.status-*` degradation only when it returns `unrecognized`. Teaching `ForSprint` the word `superseded` would flip an ADR whose status line reads exactly `Superseded` from its muted strikethrough pill to a canonical Retired badge. No ADR carries a bare `Superseded` today — every one reads `Accepted` — so the hazard is **latent, not live**. `StatusStylesTests.ForSprint_StaysNarrowerThanForStatus_OnPurpose` keeps it latent.

`AdrAccentToken` reads a **third** vocabulary (an ADR's free-text status) and includes `rejected`, which is deliberately **not** a story-retirement word. It is not unified either.

**6. `retired` shares `--status-deferred`. There is no 7th `--status-*` token.**

This holds Story 9.3's owner decision #1 (Unmapped shares `--status-pending` on the same principle). Retired stays distinct from deferred by **word and class**, never by colour — and `Icons.ForStatus("retired")` and `("deferred")` are byte-identical glyphs *by design*, which makes an icon-only assertion vacuous. Tests must assert the class **and** the word.

Because retired owns no token of its own, it inherits every theme remap for free: the webview theme remaps `--status-deferred` in all four of its blocks and `--parchment-dark` host-wide, so **no webview carve-out is needed** — verified, not assumed.

**7. A retired story card stays INLINE and demotes itself visibly (owner decision D2).**

It keeps its position in story order with a grey `--status-deferred` edge, a softened title, the crossed-circle glyph and the word — all three channels, per UX-DR17. It is **not** hoisted into the collapsed retired-notices `<details>`: that section collects pre-rendered notice HTML lifted from epics.md *comments*, a different mechanism from a story card.

Only `retired` earns a card-level modifier. Emitting every stage's class on `.story-card` would rewrite every story card on every epic page for one story's sake, and no other stage has card-level styling today.

## Consequences

**Good.**

- An epic that retires a story can reach `done`. Epic 22 is unblocked, and so is every future epic that retires one.
- SpecScribe stops reporting its own documented planning decision as an `Unsupported` diagnostic.
- The defined and tracked ledgers name the **same** stage for the same story, restoring Story 8.3's invariant at the place it was broken rather than papering over it in a consumer.
- The retirement vocabulary widened from three words to six **and** shrank from two authored lists to one, in the same change.
- Story 8.2's unrecognized path is **narrowed, not removed**: `frobnicated` still renders visibly unrecognized with its non-fatal notice.

**Bad, and accepted.**

- `ForStatus` now has three ways to reach a stage (exact switch, retirement list, token fallback) where it had two. The ordering between them is load-bearing and commented, which is a real increase in what a reader must hold.
- Widening `EpicsParser.RetirementKeyword` from three words to six changes **which epics.md HTML comments get hoisted** into `RetiredNoticesHtml`. Today's corpus contains no comment carrying `cancelled` / `obsolete` / `wontfix`, so nothing moves — but an author writing "cancelled" in a seat-mapping comment will now see it diverted.
- `ForStatus` also classifies quick-dev doc frontmatter (its own doc comment says so), so a quick-dev doc reading `superseded` / `obsolete` / `cancelled` reclassifies from `unrecognized` to `retired`. That is the correct outcome and is in scope, but it is a second surface the story's ACs did not name.
- `.story-card` gains a conditional class, which required widening `RenderParity.StoryCardRegex` from an exact class match to a class-attribute match. A parity extractor that pins exact markup is brittle by construction; this is the second time it has had to loosen.
- Retired and deferred are now **two** stages sharing one token and one glyph. The word is the only channel that separates them. That is the accepted cost of no-7th-token, and it is why every assertion in this area must read the word.

**Neutral.**

- `ForStory` is still just `ForStatus(story.Status)`, so no consumer had to learn a new entry point — every one of the ~15 `ForStory` sites changed behaviour for a retired story without changing a line.
- Three roll-up sites that pick "the next actionable story" (`DashboardViewBuilder`'s Now & Next buckets, `BmadCommands`' next-step picker, `EpicsViewBuilder`'s current-story pick) matched retired under **neither** the old classification nor the new one, so they needed no change — verified rather than assumed, because the failure mode would have been offering a retired story as the next unit of work.
- `DeliveryCadence` filters on `done` for cycle time, so a retired story stays out of cadence. A retired story never shipped.

## Alternatives considered

**Teach `ForSprint` the same six words and have one classifier.** Rejected — Decision 5. The yaml ledger is a closed set, the artifact status line is free text, and `FreeTextBadge` routes ADR status lines through `ForSprint`. One classifier means an ADR reading "Superseded" gets a story-lifecycle badge.

**Drop retired from the epic roll-up denominator, matching `SprintTemplater`.** Rejected — Decision 3. An epic's badge answers "is this closed", and an epic of five done stories plus one retired is closed. Excluding retired from the denominator gives the same answer for `done` but loses the all-retired case entirely, which would then read `drafted`.

**Coerce `retired` to `done` at the story level so the existing `All(c => c == "done")` gate just works.** Rejected: it is a lie at the exact place the portal is supposed to be honest. A retired story would appear in `StoriesDone`, in the delivery sentence, in cadence cycle-time, and on the done ring — green creep of the precise kind `StatusStyles` exists to prevent.

**Hoist retired story cards into the collapsed retired-notices section.** Rejected — Decision 7, and the owner chose the inline demoted card. The two are different mechanisms (a parsed story section with a blockquote banner vs. a hoisted HTML comment), and collapsing a story out of order hides a decision the reader should be able to see in place.

**Earn a 7th `--status-*` token for retired.** Rejected — Decision 6, holding Story 9.3's owner decision #1. If the design ever genuinely needs one, that is a signal to bring back to the owner, not a paperwork step.
