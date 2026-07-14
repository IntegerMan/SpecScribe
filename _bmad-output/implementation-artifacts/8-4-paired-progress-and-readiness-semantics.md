---
baseline_commit: f4017dd1ab26b835dc6432ad39bbdd26f055fea0
---

# Story 8.4: Paired Progress and Readiness Semantics

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want task progress and workflow state always shown together,
so that "5/5 tasks done" while in review reads as one coherent fact, not a contradiction.

## Acceptance Criteria

1.
**Given** a story surface shows task completion and the story has a workflow state
**When** both are available
**Then** they render paired (for example "5/5 tasks · awaiting review") everywhere both appear
**And** epic dual-count badges restate as sentences (for example "6 of 7 done, 1 in review"). [Source: epics.md#Story 8.4; UX-DR23]

2.
**Given** the sprint board columns Backlog and Ready for dev
**When** I hover or focus a column header
**Then** a tooltip distinguishes them (for example "Ready = task plan exists and dependencies met")
**And** stories lacking task plans are visually separated from actionable ones. [Source: epics.md#Story 8.4; UX-DR24]

---

## Developer Context

**This is a presentation / legibility story, not a data or model change.** Every number this story pairs already exists on the models (`StoryInfo.TasksDone/TasksTotal`, the story's `Status`, `EpicProgress.StoryStatusCounts`). Nothing is recomputed. The entire job is to make two already-shown facts read as **one coherent statement** instead of two disconnected ones, and to make the sprint board's "not started" columns self-explaining. It is the direct fix for two live UX-review findings graded 🔴/🟡:

> 🔴 **Progress semantics clash:** stories showing "5 of 5 tasks done" while badged `review` or sitting in backlog make task-percent and workflow-state look contradictory. Pair the numbers ("5/5 tasks · awaiting review") wherever both appear.
> 🔴 **Epic dual-count badge** ("Done: 6 · In review: 1") — restate as a sentence: "6 of 7 done, 1 in review."
> 🟡 **Backlog vs Ready for dev** both read as "not started" to outsiders; a column-header tooltip ("Ready = task plan exists and dependencies met") fixes it cheaply.
> [Source: [docs/Epic3UXFeedback.md:70,78,83](../../docs/Epic3UXFeedback.md); UX-DR23/UX-DR24]

It sits between its two Epic 8 siblings: **8.2 (canonical status vocabulary + status legend)** locks the *words and colors* this story pairs; **8.3 (`ProjectCounts` ledger)** locks the *counts* this story reads. Both are `ready-for-dev`. Read the coordination notes below — you extend the same badge/tooltip seams they touch, but on a different axis (arrangement + readiness, not vocabulary or count-sourcing).

### The four surfaces in scope (each has a precise site)

| # | Surface | Today | AC | Fix |
|---|---|---|---|---|
| A | **Epic-page story card header** | Two separate sibling badges: status badge + task badge, side by side | #1 | Pair them into one grouped unit joined by a `·` separator |
| B | **Epic delivery dual-count** (epic mosaic donut) | Delivery counts appear only as disconnected per-segment `<title>`s ("Done: 6", "In review: 1"); the visible sub-label says "N/N stories detailed" | #1 | Add a single-sentence delivery statement ("6 of 7 done, 1 in review") as the donut's accessible name + a visible restatement |
| C | **Sprint board column headers** | `.sprint-lane-head` is a bare label + count, no tooltip, not focusable | #2 | Per-column hover/focus tooltip; Backlog vs Ready is the load-bearing pair |
| D | **Sprint board cards without a task plan** | A no-plan card silently omits its progress bar — indistinguishable from an actionable one at a glance | #2 | Dashed/muted card treatment separating no-plan from actionable |

### Owner-selected design decisions (do not re-litigate)

**1. Badge pairing form → grouped pair with a separator (Surface A).** Do **not** merge the status badge and task badge into one fused pill, and do **not** demote the task tally to a sub-line. Keep the two existing badges — `StatusStyles.Badge(...)` and `TaskBadge(...)` — with their current styling intact, but wrap them in **one paired group** (`<span class="story-status-pair">…</span>`) that renders them joined by a visible middot separator so the pair reads as a single fact: `( [In review] · [✓ 5/5 tasks] )`. This is the lowest-risk silhouette: it preserves every existing `.status-badge` / `.task-badge` rule (and the six lifecycle colors), so the only new CSS is the wrapper's fl[ex] gap + separator. [Owner decision, this story — visual intent elicited at create-story]

**2. No-task-plan separation → dashed/muted card treatment (Surface D).** A sprint card for a story with **no task plan** (`story is null` OR `story.TasksTotal == 0`) gets a **dashed border + muted fill**, mirroring the sunburst's existing `.sb-noplan` dashed no-plan arc ([`Charts.AppendNoPlanArc`](../../src/SpecScribe/Charts.cs:269-279)). Actionable cards keep their current solid treatment. This reuses a treatment the portal already teaches ("dashed = no plan yet") rather than inventing a new visual language, and it needs no per-card text (that keeps it clear of Story 8.6's "no task plan yet" consolidation banner — see boundaries). [Owner decision, this story — visual intent elicited at create-story]

**3. Epic dual-count → one accessible sentence, built from the same tally (Surface B).** The epic mosaic donut currently passes **no `ariaLabel`** (it renders decorative/`aria-hidden` with per-segment `<title>`s only — that is the "Done: 6 · In review: 1" the review saw as disconnected counts). Give it a single, ordered, plain-language sentence built from the same `EpicProgress.StoryStatusCounts` it already rings — "6 of 7 done, 1 in review" — as the donut's `aria-label` **and** as a visible restatement line, so the dual counts read as one framed statement for pointer, keyboard, and screen-reader users alike. Keep the existing "N/N stories detailed" sub-label (it answers a different question — planning depth, not delivery) OR fold it in; decide by which reads cleaner and note the choice. [Owner decision, this story]

**4. Column-meaning tooltip → all five columns, Backlog/Ready load-bearing (Surface C).** Give every board column header a one-line meaning tooltip via the body-level `.ss-tooltip`/`js-tip`/`data-tip` node, with **Backlog** and **Ready for dev** carrying the distinguishing text the AC names ("Backlog = not yet ready to pick up"; "Ready = task plan exists and dependencies met"). A per-column meaning key is coherent and coordinates with 8.2's `StageMeaning` seam (below); do it for all five so the board is self-teaching, not just the two named columns. [Owner decision, this story]

### Relationship to Story 8.2 (status vocabulary + `StageMeaning` — both `ready-for-dev`, coordinate)

8.2 is adding a **`StatusStyles.StageMeaning(cssClass)`** (or `LegendEntries`) one-line-meaning source plus a per-badge hover/focus tooltip and a page-level status legend key. 8.4's column-header tooltips (Surface C) are **the same kind of "what does this stage mean" affordance**, so:

- **If 8.2 has landed first:** source the column-header meaning text from `StatusStyles.StageMeaning(...)` (extending it if the sprint-column phrasing — "Ready = task plan exists and dependencies met" — needs to be richer than the generic stage meaning). Do **not** author a second, parallel stage→meaning map. One seam.
- **If 8.4 lands first:** put the column-meaning strings in a small local helper in `SprintTemplater` and leave a `// TODO(8.2): fold into StatusStyles.StageMeaning` marker so 8.2 subsumes it. Record the choice in Completion Notes.
- **No vocabulary change here.** 8.2 owns which *word/class* a status gets and the "unrecognized" state; 8.4 never changes a status word — it only arranges words+counts and explains columns. If 8.2 adds an `"unrecognized"` stage, Surface B's sentence and Surface D's no-plan gate must tolerate it (they will: the sentence iterates `StoryStages`, and the no-plan gate keys on `TasksTotal`, not on stage). [Source: [8-2-canonical-status-model-with-portal-wide-legend.md](8-2-canonical-status-model-with-portal-wide-legend.md)]

### Relationship to Story 8.3 (`ProjectCounts` ledger — `ready-for-dev`)

8.3 centralizes the four count families (stories, epics, deferred, action items) into a `ProjectCounts` ledger and **explicitly scopes OUT per-epic story tallies** (decision 4 in that story). Surface B's "6 of 7 done, 1 in review" is exactly a per-epic delivery tally — so **read it directly from `EpicProgress.StoryStatusCounts`, the way `Charts.EpicMosaic`/`DeliverySegments` already do**; do not route it through 8.3's ledger and do not add a new recount. Per-story task tallies (Surfaces A/D) likewise come straight off `StoryInfo`/`StoryCardView`, untouched by 8.3. The two stories don't overlap on data; note in Completion Notes that Surface B intentionally reads the per-epic tally, not the ledger. [Source: [8-3-single-source-of-truth-for-every-count.md](8-3-single-source-of-truth-for-every-count.md)]

### Scope boundaries (read carefully)

- **Do NOT build Story 9.4's verification evidence strip.** 9.4 owns the story-*page* "5/5 tasks · 586 tests green · verified 2026-07-09" evidence strip near the status badge ([epics.md#Story 9.4](../planning-artifacts/epics.md); [Epic3UXFeedback.md:97](../../docs/Epic3UXFeedback.md)). On the story **page** header ([`RenderStoryBody`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:274-284)) the workflow-state badge is present but the task tally lives in the Task Breakdown sunburst below — pairing there is 9.4's evidence-strip job, not this story's. **8.4's Surface A pairing is the epic-page story CARD** (where both badges already sit together) — do not pre-build the page-header evidence strip. If you touch the story-page header at all, keep it to arrangement of what's already there. Flag the seam for 9.4 in Completion Notes.
- **Do NOT build Story 8.6's empty-state consolidation.** 8.6 owns consolidating per-story "no task plan yet" hints into one per-epic banner and the designed empty-column copy ([epics.md#Story 8.6](../planning-artifacts/epics.md)). 8.4's Surface D is a **visual treatment of individual no-plan cards** (dashed/muted), not a consolidated banner and not empty-column copy. No per-card "no task plan" text (the tooltip already says it — [`BuildCardTip`](../../src/SpecScribe/SprintTemplater.cs:301-303)).
- **Do NOT change any status word, color, or count value.** Only arrangement (pairing), one derived sentence, one dashed treatment, and column tooltips. If a *rendered number* changes, you have a bug — the values are all pre-existing.
- **Do NOT add a client-side script or NuGet package.** Pure SVG + CSS + the one sanctioned `specscribe.js` tooltip node. [memory: [[charting-is-pure-svg-no-js]]]
- **Do NOT write back to any source.** Local-first, read-only invariant.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Surface A — pair the two badges on the epic-page story card.** In [`HtmlRenderAdapter.AppendStoryCard`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:211-250), the status badge ([:220](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:220)) and the task badge ([:224](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:224)) currently emit as two independent siblings inside `.story-card-header`. Wrap the two (when both are present) in a single `<span class="story-status-pair">` and render a visible `·` separator between them so they read as one paired fact. When only one is present (status but no tasks, or the reverse), render the single badge without an orphan separator. Keep the existing `StatusStyles.Badge(...)` and `TaskBadge(...)` calls and their output bytes intact — you are grouping, not rewriting them.
- **Surface B — restate the epic delivery dual-count as a sentence.** Add a small helper (e.g. `Charts.DeliverySentence(IReadOnlyDictionary<string,int> counts)` or a method on `StatusStyles`) that turns `EpicProgress.StoryStatusCounts` into an ordered, plain sentence over `StatusStyles.StoryStages` — "6 of 7 done, 1 in review" (total = Σ segments, leading with done, omitting zero stages, using `StatusStyles.StoryLabel` for each stage word and `Charts.Plural` for agreement). In [`Charts.EpicMosaic`](../../src/SpecScribe/Charts.cs:440-467), pass that sentence as the mosaic `Donut(...)`'s `ariaLabel` (so the ring stops being `aria-hidden`) **and** render it as a visible restatement in `.epic-mosaic-label`. Derive the total from the summed segments, never a parallel field (structural total==Σsegments discipline, per 8.3).
- **Surface C — column-header meaning tooltips + focus reach.** In [`SprintTemplater.RenderBoard`](../../src/SpecScribe/SprintTemplater.cs:115-131), give each `.sprint-lane-head` a `js-tip` class + `data-tip="{meaning}"` (escaped via `PathUtil.Html`) and `tabindex="0"` so it is hover- AND focus-reachable (five headers is a fine tab-order cost — this is the deliberate exception to 8.2's "don't tabindex dozens of badges" rule, because it's a small, fixed, meaning-bearing set). Backlog → "Backlog = not yet ready to pick up"; Ready for dev → "Ready = task plan exists and dependencies met"; the other three get their one-line meanings too. Source the strings from 8.2's `StageMeaning` if landed, else a local helper with the `// TODO(8.2)` marker.
- **Surface D — dashed/muted no-plan cards.** In [`SprintTemplater.AppendBoardCard`](../../src/SpecScribe/SprintTemplater.cs:257-286), add a `no-plan` modifier class to the `.sprint-card` when the story has no task plan (`story is null` OR `story.TasksTotal == 0` — the inverse of the existing progress-bar gate at [:280](../../src/SpecScribe/SprintTemplater.cs:280)). Add `.sprint-card.no-plan` CSS (dashed border + muted fill) mirroring `.sb-noplan`. Keep the existing `BuildCardTip` "No task plan yet" line — it already names the state for the tooltip; do not add visible per-card text.
- **Route every new swatch/color through the `--status-*` tokens.** The paired separator, the no-plan muted fill, and any accent must use existing tokens/neutrals — never literal hex. [memory: [[specscribe-status-token-system]]]
- **Reuse the body-level tooltip node.** Column tooltips use the existing `.ss-tooltip` / `js-tip` / `data-tip` plumbing served by `specscribe.js` (no JS change — `HOVER = SEG + ", .js-tip"` already picks up `.js-tip` elements). [memory: [[tooltip-clipping-use-ss-tooltip-node]]]
- **Keep the section view-model split intact.** Surfaces A/B render from the host-neutral section view models (`StoryCardView`, `EpicsIndexView`/`ProgressModel`) via the adapter (Story 6.2). The paired arrangement + delivery sentence are ADAPTER render changes over the SAME view-model data (`StatusStage`, `Status`, `TasksDone`, `TasksTotal`, `StoryStatusCounts`) — do not add new fields unless a fact genuinely isn't present. The `RenderParity.SectionFacts` harness asserts on facts (id/status/task tally), which are unchanged, so it should still hold; the byte-level golden fingerprint WILL move (see Testing). [memory: [[story-6-2-section-view-models-live]]]

### DON'T

- **DON'T fuse the two badges into one pill or move tasks to a sub-line** — the owner picked the grouped-pair-with-separator silhouette (decision 1). A single merged badge or a sub-line is a different, rejected design.
- **DON'T recompute any count.** Task tallies come off `StoryInfo`/`StoryCardView`; the epic delivery sentence comes off `EpicProgress.StoryStatusCounts`. No new `.Count(...)` at a render site (that is 8.3's anti-pattern too).
- **DON'T change status words, the six lifecycle colors, or chart legends.** That's 8.2's territory. You arrange and explain; you don't reclassify.
- **DON'T add per-card "no task plan" text** (Surface D is visual-only; the text consolidation is 8.6).
- **DON'T build the story-page verification evidence strip** (Story 9.4).
- **DON'T add `tabindex="0"` to the badges** — only the five column headers get focus (a small, meaning-bearing set); badges stay hover/pointer progressive enhancements as 8.2 established.
- **DON'T add JS or a NuGet package.** Pure SVG/CSS + the existing tooltip node.

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)]:

- **Single source of truth** — this story reads the existing single sources (`StatusStyles` for words/colors, `StoryInfo`/`EpicProgress` for tallies); it adds NO parallel model. The delivery sentence is a pure projection of `StoryStatusCounts`. [memory: [[specscribe-status-token-system]]]
- **Truthfulness over convenience** — the whole point: "5/5 tasks" beside `review` is *true* but reads as a contradiction until paired; pairing makes the true relationship legible without hiding either fact. Never "resolve" the tension by dropping the task count or the state. [Source: [StatusStyles.cs:3-5](../../src/SpecScribe/StatusStyles.cs)]
- **Accessibility is part of the rendering contract** (NFR6, UX-DR17): status stays color + icon + word (badges unchanged); the epic dual-count gains a real `aria-label` sentence (today it's `aria-hidden`); column tooltips are focus-reachable, not hover-only. Never make the tooltip the sole channel — the column label + count stay visible at rest.
- **Deterministic, generation-time-only output** — every string derives solely from parsed input; a from-scratch regen of identical inputs is byte-identical. No per-visitor/cross-build state.
- **Framework-agnostic (NFR8)** — all inputs are the projected domain models a future adapter (Epic 4) already feeds; no per-framework branching. The column-meaning strings describe the canonical lifecycle stages, not BMad-specific vocabulary.
- **Seed, not invariant** — no Core/Adapters package split; changes stay in the existing `HtmlRenderAdapter.Epics` / `SprintTemplater` / `Charts` / `StatusStyles` files. [Source: [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: [SpecScribe.Tests.csproj](../../tests/SpecScribe.Tests/SpecScribe.Tests.csproj)]
- **Reuse, don't reinvent (all already in-repo):**
  - [`StatusStyles.Badge` / `StoryLabel` / `SprintLabel` / `Icon` / `StoryStages`](../../src/SpecScribe/StatusStyles.cs:34,49,180,185) — the badge renderer + stage words + canonical stage order the pairing and the delivery sentence build on. (If 8.2 landed: `StatusStyles.StageMeaning` for the column tooltips.)
  - [`HtmlRenderAdapter.TaskBadge`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:253-264) — the existing task-completion badge to group beside the status badge (keep as-is).
  - [`Charts.EpicMosaic` / `DeliverySegments` / `Donut` / `Plural`](../../src/SpecScribe/Charts.cs:440,472,48) — the epic delivery ring + segment builder + the `Donut(ariaLabel:)` parameter to populate; `Plural` for singular/plural agreement.
  - [`Charts.AppendNoPlanArc` / `.sb-noplan`](../../src/SpecScribe/Charts.cs:269-279) + [specscribe.css](../../src/SpecScribe/assets/specscribe.css) — the established dashed "no plan yet" treatment to mirror for Surface D.
  - [`SprintTemplater.RenderBoard` / `AppendBoardCard` / `BuildCardTip`](../../src/SpecScribe/SprintTemplater.cs:103,257,290) — the board columns + cards + the existing "No task plan yet" tooltip line.
  - The body-level tooltip: `.js-tip` + `data-tip` → `.ss-tooltip`, served by `specscribe.js` (no JS change). [Source: [specscribe.js](../../src/SpecScribe/assets/specscribe.js); memory: [[tooltip-clipping-use-ss-tooltip-node]]]
  - [`PathUtil.Html`](../../src/SpecScribe/PathUtil.cs) — escape all tooltip / aria text.

---

## File Structure Requirements

**No new production classes expected** — all changes extend existing render sites. A tiny `Charts.DeliverySentence(...)` helper (or a `StatusStyles` method) is the only likely new *method*.

**Modified files (read fully before editing):**

- [`src/SpecScribe/HtmlRenderAdapter.Epics.cs`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs) — **Surface A:** group the status + task badges in `AppendStoryCard` ([:211-250](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:211)) into `.story-status-pair` with a `·` separator; handle the one-present-not-both cases. **Preserve** the `StatusStyles.Badge`/`TaskBadge` output. **Do NOT** touch `RenderStoryBody`'s header beyond arrangement (leave the evidence strip to 9.4).
- [`src/SpecScribe/Charts.cs`](../../src/SpecScribe/Charts.cs) — **Surface B:** add the `DeliverySentence(...)` helper; in `EpicMosaic` ([:440-467](../../src/SpecScribe/Charts.cs:440)) pass it to the `Donut` `ariaLabel` and render the visible restatement in `.epic-mosaic-label`. **Preserve** the ring, the delivery segments, and the "N/N stories detailed" sub-label decision.
- [`src/SpecScribe/SprintTemplater.cs`](../../src/SpecScribe/SprintTemplater.cs) — **Surface C:** `.sprint-lane-head` gains `js-tip` + `data-tip` + `tabindex="0"` in `RenderBoard` ([:119](../../src/SpecScribe/SprintTemplater.cs:119)); add the column-meaning helper. **Surface D:** `AppendBoardCard` ([:257-286](../../src/SpecScribe/SprintTemplater.cs:257)) adds the `no-plan` class when `story is null || story.TasksTotal == 0`.
- [`src/SpecScribe/assets/specscribe.css`](../../src/SpecScribe/assets/specscribe.css) — add `.story-status-pair` (flex gap + `·` separator, via `::after` or an inline dot span) near `.status-badge.task-badge` ([:1151-1160](../../src/SpecScribe/assets/specscribe.css:1151)); add `.sprint-card.no-plan` (dashed border + muted fill) near `.sprint-card-progress` ([:2996-3001](../../src/SpecScribe/assets/specscribe.css:2996)); any lane-head `js-tip` cursor affordance near `.sprint-lane-head` ([:3019-3050](../../src/SpecScribe/assets/specscribe.css:3019)); the epic-mosaic delivery-sentence line near the mosaic styles. Route colors through `--status-*` / existing neutrals. **`StylesheetTests` asserts on stylesheet content — add companion assertions for any new class.**
- [`src/SpecScribe/StatusStyles.cs`](../../src/SpecScribe/StatusStyles.cs) — **only if** the delivery-sentence or column-meaning logic reads best as a `StatusStyles` method (vs. `Charts`/`SprintTemplater`). If 8.2's `StageMeaning` isn't landed and you add a local column-meaning map, prefer a private helper in `SprintTemplater` with the `// TODO(8.2)` marker (don't pre-empt 8.2's public seam).

**Tests to update / add:**

- [`tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs) — Surface A: a story card with both a status and a task tally emits one `.story-status-pair` wrapping both badges with the separator; a card with status-only or tasks-only emits the single badge with no orphan separator; both badges' inner HTML is unchanged.
- [`tests/SpecScribe.Tests/ChartsTests.cs`](../../tests/SpecScribe.Tests/ChartsTests.cs) — Surface B: `DeliverySentence` maps a `{done:6, review:1}` tally to "6 of 7 done, 1 in review" (order, plural, zero-omission); `EpicMosaic` emits the sentence as the donut `aria-label` (ring no longer `aria-hidden`) and as a visible line.
- [`tests/SpecScribe.Tests/SprintTemplaterTests.cs`](../../tests/SpecScribe.Tests/SprintTemplaterTests.cs) — Surface C: each `.sprint-lane-head` carries `js-tip` + a `data-tip` (Backlog and Ready get the distinguishing strings) + `tabindex="0"`. Surface D: a story with `TasksTotal == 0` (and an unmatched yaml card) renders `.sprint-card.no-plan`; a story with a plan does not.
- [`tests/SpecScribe.Tests/StylesheetTests.cs`](../../tests/SpecScribe.Tests/StylesheetTests.cs) — assert `.story-status-pair` and `.sprint-card.no-plan` (+ any lane-head tip affordance) are present.
- [`tests/SpecScribe.Tests/RenderSectionParityTests.cs`](../../tests/SpecScribe.Tests/RenderSectionParityTests.cs) / [`RenderParityTests.cs`](../../tests/SpecScribe.Tests/RenderParityTests.cs) — confirm the section FACTS (id, status stage, task tally) are unchanged; update only if a fact genuinely shifted (it shouldn't — this is arrangement).
- **Golden fingerprint:** [`SiteGeneratorAdapterTests.GoldenContentFingerprint_...`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) **WILL change** (the epic-page card HTML, the mosaic aria/label, and the sprint board markup all change bytes). Regenerate the constant per the drill below and confirm every diff is exactly one of the four intended surfaces — no accidental change elsewhere. [memory: [[golden-diff-normalization-gotchas]]]

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). Surfaces A/B/C/D are string-building over existing models — unit-testable directly against the adapter/templater/charts helpers, mirroring the existing `HtmlRenderAdapterTests` / `SprintTemplaterTests` / `ChartsTests`. Generation-level assertions build a temp `_bmad-output` and read emitted HTML (`AssertNoErrors` pattern — [`SiteGeneratorTraceabilityTests`](../../tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs)).

Cover explicitly:

- **Pairing (Surface A):** both-present → one `.story-status-pair` with status badge + `·` + task badge, inner badge bytes unchanged; status-only and tasks-only → single badge, no dangling separator; no-status/no-tasks → neither (unchanged).
- **Delivery sentence (Surface B):** `{done:6, review:1}` → "6 of 7 done, 1 in review"; a single-stage epic → "7 of 7 done" (no trailing clause); zero stages omitted; the mosaic donut carries the sentence as `aria-label` (asserted present + `role="img"`, not `aria-hidden`) and as visible text.
- **Column tooltips (Surface C):** every lane head has `js-tip` + `data-tip` + `tabindex="0"`; the Backlog and Ready `data-tip` strings distinguish "not yet ready to pick up" vs "task plan exists and dependencies met".
- **No-plan separation (Surface D):** `TasksTotal == 0` story → `.sprint-card.no-plan`; unmatched yaml card (no model story) → `.no-plan`; a planned story (`TasksTotal > 0`) → no `.no-plan`; the existing progress bar still renders only for planned stories.
- **No value/vocabulary drift:** every rendered status word, color class, and count value is identical to before; only arrangement + the derived sentence + the dashed class + tooltips are new. Regression: all pre-existing adapter/sprint/charts/stylesheet/parity tests pass.
- **Determinism:** two generations over identical input produce identical output.

**Run:** `dotnet test` from repo root. Then a full generation against this repo: `dotnet run --project src/SpecScribe` (output → `SpecScribeOutput/`, the default — **do not** pass `--output docs/live`; vestigial/gitignored). Eyeball: an epic page's story card shows the paired `[state] · [tasks]` unit; the epics-index "Progress by Epic" mosaic reads "N of M done…" as a sentence; the sprint board's Backlog and Ready headers show distinguishing tooltips on hover AND keyboard focus; a no-task-plan story card is visibly dashed/muted next to a solid actionable one. [memory: [[generate-output-dir-is-specscribeoutput]]]

**Golden-diff drill (rendered bytes change here, so expect a fingerprint update):** freeze a fixture copy of `_bmad-output` + `docs/adrs` + `README.md` + `_bmad` in scratchpad, `git init` with fixed-date commits (+`--deep-git`), generate before/after, apply the 5 volatile-token normalizations, and confirm the ONLY diffs are the four intended surfaces (paired badges, mosaic sentence, board column tips, no-plan cards). Then regenerate the `GoldenContentFingerprint` constant (the test prints the new hash). Run twice for portability. [memory: [[golden-diff-normalization-gotchas]]]

---

## Previous Story Intelligence

**Story 8.2 (Canonical Status Model — `ready-for-dev`, sibling)** locks the status words/colors (`StatusStyles`) this story pairs and adds the `StageMeaning` seam + per-badge tooltips + status legend. Coordinate the `StageMeaning` reuse (column tooltips) and tolerate a possible `"unrecognized"` stage. Whichever lands second reconciles the meaning-source. [Source: [8-2-canonical-status-model-with-portal-wide-legend.md](8-2-canonical-status-model-with-portal-wide-legend.md)]

**Story 8.3 (Single Source of Truth for Every Count — `ready-for-dev`, sibling)** centralizes the four count families but scopes OUT per-epic tallies — so Surface B reads `EpicProgress.StoryStatusCounts` directly (as the mosaic already does), not the ledger. No overlap on data. [Source: [8-3-single-source-of-truth-for-every-count.md](8-3-single-source-of-truth-for-every-count.md)]

**Story 6.2 (Section View Models — `review`)** decomposed the epics/dashboard bodies into builder→adapter section view models with a byte-parity + `RenderParity.SectionFacts` harness. Surfaces A/B are adapter render changes over unchanged view-model DATA — keep the split; don't push arrangement logic back into the builder. [Source: [[story-6-2-section-view-models-live]]]

**Story 2.3 (Sprint Status)** built `SprintTemplater`, the board columns, the per-card progress bar, and the existing "No task plan yet" tooltip line — the exact seams Surfaces C/D extend. Its progress bar already gates on `TasksTotal > 0`; Surface D is the visual inverse of that same gate. [Source: [`SprintTemplater.cs`](../../src/SpecScribe/SprintTemplater.cs); [2-3-sprint-status-page-and-dashboard-widget.md](2-3-sprint-status-page-and-dashboard-widget.md)]

**Story 2.5 (Standardized Iconography)** built `StatusStyles.Badge` (icon + text, never icon-only, UX-DR17) — the badge you group but do not alter. [Source: [`StatusStyles.cs:185`](../../src/SpecScribe/StatusStyles.cs:185)]

**Recurring lessons that apply here:**

- **Truthfulness over convenience** — pair the two true facts; never suppress one to remove the apparent contradiction. [Source: [`StatusStyles.cs:3-5`](../../src/SpecScribe/StatusStyles.cs)]
- **Elicit visual intent up front** (Epic 3 retro, open action) — the two new visual surfaces (badge pairing, no-plan treatment) were offered as named directions and the owner picked *grouped-pair-with-separator* and *dashed/muted card*; the dev builds those, not a re-invented silhouette. [memory: [[create-story-elicit-visual-intent]]]
- **Split, don't absorb** — if pairing tempts you into building 9.4's evidence strip or 8.6's empty-state banner, stop: those are separate stories. [Source: Epic 2/3 retros]

---

## Git Intelligence Summary

Recent history is planning/retro churn on `main` (`Review`; `6.2, planning 6.3/6.4`; `Addressed UX issues and future planning`; `Epic 4 Retro`) — no in-flight code touches `HtmlRenderAdapter.Epics`, `SprintTemplater`, `Charts.EpicMosaic`, or `StatusStyles`, so this change is additive and uncontended against its siblings 8.2/8.3 (which touch adjacent seams, not the same lines). **Heed the worktree rule:** if this runs in a worktree, edit files at the **worktree path** — `main` has a background auto-committer, so never re-root paths at `C:\Dev\SpecScribe`. [memory: [[worktree-edits-must-target-worktree-path]]]

---

## Latest Technical Information

No external libraries or APIs are introduced — pure in-repo C# string-building over existing models + CSS — so there is no version/security research to fold in. Discipline note: keep all derived text (the delivery sentence, column meanings) built with `System.Globalization.CultureInfo.InvariantCulture` where casing/formatting is involved (matching `StatusStyles.TitleCase`), and use `Charts.Plural` for singular/plural agreement, exactly as the current subtitles do.

---

## Project Context Reference

- Epic 8 goal + FR/UX-DR/NFR coverage: [Source: [epics.md:1084-1088](../planning-artifacts/epics.md:1084)]
- Story 8.4 user story + both ACs: [Source: [epics.md:1136-1154](../planning-artifacts/epics.md:1136)]
- UX-DR23 (progress + state always paired; dual-count epic badges restated as sentences): [Source: [epics.md:135](../planning-artifacts/epics.md:135)]
- UX-DR24 (readiness self-explanatory: column tooltips distinguish backlog vs ready; no-plan stories visually separated): [Source: [epics.md:136](../planning-artifacts/epics.md:136)]
- The concrete UX-review findings this story fixes (progress clash, epic dual-count, backlog-vs-ready): [Source: [docs/Epic3UXFeedback.md:70,78,83](../../docs/Epic3UXFeedback.md); [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md)]
- Architecture invariants (single-source, truthfulness, accessibility, deterministic, seed-not-invariant): [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]
- Status-token / tooltip-node / pure-render / section-view-model / golden-fingerprint / output-dir / worktree / visual-intent discipline: project memory ([[specscribe-status-token-system]]; [[tooltip-clipping-use-ss-tooltip-node]]; [[charting-is-pure-svg-no-js]]; [[story-6-2-section-view-models-live]]; [[golden-diff-normalization-gotchas]]; [[generate-output-dir-is-specscribeoutput]]; [[worktree-edits-must-target-worktree-path]]; [[create-story-elicit-visual-intent]]).

---

## Tasks / Subtasks

- [x] **Task 1 — Surface A: pair the story-card badges (AC: #1)**
  - [x] In `HtmlRenderAdapter.AppendStoryCard`, wrap the status badge + task badge in one `.story-status-pair` joined by a visible `·` separator when both are present; render the single badge with no orphan separator when only one is. Preserve the `StatusStyles.Badge`/`TaskBadge` inner output.
  - [x] Add `.story-status-pair` CSS (flex gap + separator) near `.status-badge.task-badge`; route colors through tokens/neutrals.
- [x] **Task 2 — Surface B: epic dual-count as a sentence (AC: #1)**
  - [x] Add `Charts.DeliverySentence(counts)` → ordered plain sentence over `StatusStyles.StoryStages` ("6 of 7 done, 1 in review"; total = Σ segments; zero stages omitted; `StoryLabel` + `Plural`).
  - [x] In `Charts.EpicMosaic`, pass the sentence as the mosaic donut `ariaLabel` (ring becomes `role="img"`) and render it as a visible line in `.epic-mosaic-label`; keep/fold the "N/N stories detailed" sub-label and note the choice.
- [x] **Task 3 — Surface C: column-header meaning tooltips (AC: #2)**
  - [x] In `SprintTemplater.RenderBoard`, add `js-tip` + escaped `data-tip` + `tabindex="0"` to each `.sprint-lane-head`; Backlog and Ready carry the distinguishing strings. Source meanings from 8.2's `StageMeaning` if landed, else a local helper + `// TODO(8.2)` marker (record choice in Completion Notes).
  - [x] Add any lane-head `js-tip` cursor affordance CSS.
- [x] **Task 4 — Surface D: dashed/muted no-plan cards (AC: #2)**
  - [x] In `AppendBoardCard`, add `no-plan` to `.sprint-card` when `story is null || story.TasksTotal == 0`. Add `.sprint-card.no-plan` CSS (dashed border + muted fill) mirroring `.sb-noplan`. No visible per-card text (tooltip already says "No task plan yet").
- [x] **Task 5 — Tests (AC: #1, #2)**
  - [x] `HtmlRenderAdapterTests` (pairing cases), `ChartsTests` (`DeliverySentence` + mosaic aria/visible), `SprintTemplaterTests` (column tips + focus; no-plan class), `StylesheetTests` (new classes).
  - [x] Confirm `RenderSectionParity` facts unchanged; regenerate `GoldenContentFingerprint` after confirming the byte diff is exactly the four surfaces.
- [x] **Task 6 — Full generation pass + manual verify (AC: #1, #2)**
  - [x] `dotnet test` green; real generation to `SpecScribeOutput/`; eyeball the paired badge on an epic-page story card, the mosaic delivery sentence, the Backlog/Ready column tooltips (hover AND keyboard focus), and a dashed no-plan card beside a solid one.

## Dev Notes

### Cross-surface note from Story 8.1 (2026-07-14)

Surfaces A/B/D (paired badges, mosaic sentence, dashed no-plan cards) are **shared-path** — they live in `BodyHtml` / shared CSS consumed by HTML, webview, and SPA. Surface C column tooltips via `js-tip`/`specscribe.js` are progressive enhancement: **HTML + SPA**; webview will not show the rich tip. Prefer pairing Column meanings with a visible/accessible affordance that works without JS (native `title`, or text already in the header), and/or fold meanings through 8.2’s always-visible legend when available. No CLI projection.

- **The sharp edge is scope, not difficulty.** Every surface is a small string/CSS change, but two adjacent temptations belong to other stories: the story-*page* evidence strip is **9.4**, and the "no task plan yet" *consolidation banner* is **8.6**. 8.4 pairs what's already shown, restates one count as a sentence, adds four tooltips, and dashes the no-plan cards — nothing more.
- **Byte parity moves on purpose.** Unlike 8.3 (which may be byte-identical if inputs agree), 8.4 deliberately changes rendered HTML on the epic page, the mosaic, and the sprint board. Expect a golden-fingerprint regen and verify the diff is exactly the four intended surfaces. [memory: [[golden-diff-normalization-gotchas]]]
- **Section facts vs. bytes.** The `RenderParity.SectionFacts` harness checks meaning (id/status/task tally), which is unchanged; the byte harness/golden fingerprint checks HTML, which changes. Both must end green — facts unchanged, fingerprint regenerated. [memory: [[story-6-2-section-view-models-live]]]
- **Coordinate the meaning-source with 8.2.** If 8.2's `StageMeaning` is in the tree, use it for the column tooltips; if not, keep a local helper with a `// TODO(8.2)` marker so 8.2 folds it in — never two parallel stage→meaning maps.
- **Scope guard for later 8.x/9.x:** state-aware next-steps (8.5), empty states (8.6), one-view-per-dataset (8.7), recency (8.8), and the verification evidence strip (9.4) all sit near these surfaces but are NOT this story. 8.4 makes progress+state read as one fact and makes readiness self-explaining.

### Project Structure Notes

- All change concentrates in four existing files (`HtmlRenderAdapter.Epics.cs`, `Charts.cs`, `SprintTemplater.cs`, `specscribe.css`) plus tests, with at most one small helper method and possibly a `StatusStyles` touch. No new page, no new view-model field (data is already present), no package restructure, no adapter contract.
- The section view-model split (Story 6.2) stays intact: Surfaces A/B are adapter-render arrangements over unchanged `StoryCardView` / `EpicsIndexView` data.

### References

- [Source: [epics.md:1136-1154](../planning-artifacts/epics.md:1136)] — Story 8.4 user story + both ACs.
- [Source: [epics.md:1084-1088](../planning-artifacts/epics.md:1084), [epics.md:135-136](../planning-artifacts/epics.md:135)] — Epic 8 goal; UX-DR23; UX-DR24.
- [Source: [docs/Epic3UXFeedback.md:70,78,83](../../docs/Epic3UXFeedback.md)] — the three UX-review findings (progress clash, epic dual-count, backlog-vs-ready) this story fixes.
- [Source: [HtmlRenderAdapter.Epics.cs:211-264](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:211)] — `AppendStoryCard` + `TaskBadge` (Surface A).
- [Source: [Charts.cs:440-476,48-98,250-261](../../src/SpecScribe/Charts.cs:440)] — `EpicMosaic`/`DeliverySegments`/`Donut`/`DonutLegend` (Surface B).
- [Source: [SprintTemplater.cs:103-135,257-317](../../src/SpecScribe/SprintTemplater.cs:103)] — `RenderBoard`/`AppendBoardCard`/`BuildCardTip`/`AppendCardProgress` (Surfaces C/D).
- [Source: [StatusStyles.cs:34-49,138-149,180-186](../../src/SpecScribe/StatusStyles.cs:34)] — `StoryLabel`/`StoryStages`/`SprintLabel`/`Icon`/`Badge`.
- [Source: [assets/specscribe.css:1151-1160,2996-3001,3019-3050](../../src/SpecScribe/assets/specscribe.css:1151)] — `.task-badge`, `.sprint-card-progress`, `.sprint-lane-head` (styling seams).
- [Source: [assets/specscribe.js](../../src/SpecScribe/assets/specscribe.js)] — the `.ss-tooltip`/`js-tip`/`data-tip` node (no JS change).
- [Source: [EpicsView.cs:12-55](../../src/SpecScribe/EpicsView.cs)] — `StoryCardView` (StatusStage/Status/TasksDone/TasksTotal — the data Surface A pairs).
- [Source: [8-2-canonical-status-model-with-portal-wide-legend.md](8-2-canonical-status-model-with-portal-wide-legend.md), [8-3-single-source-of-truth-for-every-count.md](8-3-single-source-of-truth-for-every-count.md)] — sibling coordination (StageMeaning; per-epic tally scoped out of the ledger).
- [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)] — single-source, truthfulness, accessibility, deterministic, seed-not-invariant.
- [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md)] — the UX review that seeded Epics 8–10.

## Dev Agent Record

### Agent Model Used

Composer (Auto / agent router)

### Debug Log References

None.

### Completion Notes List

- **Surface A:** Epic-page story cards wrap status + task badges in `.story-status-pair` with a `·` separator when both present; single-badge cases have no orphan separator. Badge inner HTML unchanged (`StatusStyles.Badge` / `TaskBadge`). Story-page evidence strip left to 9.4.
- **Surface B:** Added `Charts.DeliverySentence` over `EpicProgress.StoryStatusCounts` / `StoryStages` (Σ segments; zero-omission; `StoryLabel` lowercased). Passed as mosaic donut `ariaLabel` + visible `.epic-mosaic-delivery`. **Kept** "N/N stories detailed" as a separate sub-label (planning depth ≠ delivery). Intentionally reads per-epic tally, not 8.3's `ProjectCounts` ledger.
- **Surface C:** 8.2's `StageMeaning` had already landed — column tips source from it (format `"{Label} = {StageMeaning}"`), plus native `title` for non-JS/webview. Enriched pending/ready StageMeaning text to the AC distinguishing phrases (one seam for badges, legend, and columns). All board columns (incl. unrecognized) get `js-tip` + `tabindex="0"`.
- **Surface D:** `.sprint-card.no-plan` when `story is null || TasksTotal == 0`; dashed + muted CSS mirroring `.sb-noplan`. No per-card text (8.6 owns consolidation).
- **Verification:** 1121 tests green; golden fingerprint regenerated to `0674f5c5…`; real gen to `SpecScribeOutput/` eyeballed all four surfaces.

### File List

- `src/SpecScribe/HtmlRenderAdapter.Epics.cs`
- `src/SpecScribe/Charts.cs`
- `src/SpecScribe/SprintTemplater.cs`
- `src/SpecScribe/StatusStyles.cs`
- `src/SpecScribe/assets/specscribe.css`
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs`
- `tests/SpecScribe.Tests/ChartsTests.cs`
- `tests/SpecScribe.Tests/SprintTemplaterTests.cs`
- `tests/SpecScribe.Tests/StylesheetTests.cs`
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs`
- `_bmad-output/implementation-artifacts/8-4-paired-progress-and-readiness-semantics.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-14: Implemented Story 8.4 — paired progress/state badges, epic delivery sentence, column meaning tips, dashed no-plan cards; tests + golden fingerprint updated.