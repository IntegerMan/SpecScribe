# SpecScribe UX & Consistency Feedback

Date: 2026-07-09
Scope: page-by-page review of the live portal at https://integerman.github.io/SpecScribe/ (home, sprint, epics, epic/story detail, requirements index + FR detail, git-insights, deep-analytics, structure, ADR index, readme, action-items, PRD rendering). The deferred-work and retros pages were not individually reviewed. Despite the filename (requested as the Epic 3 feedback drop), this covers the **whole portal**, not just Epic 3 surfaces. All counts (39 stories, 43 deferred items, 29× coupling, etc.) are a 2026-07-09 snapshot and will drift with regeneration.
Companion: journeys referenced below are defined in [UserJourneys.md](UserJourneys.md).

Findings are grouped as **application-wide themes** first (these repeat across pages and should be fixed once, centrally), then **per-page feedback**. Each item is tagged with severity: 🔴 HIGH — hurts a core journey; 🟡 MED — polish that compounds; 🟢 LOW — nice-to-have.

---

## Application-wide themes

### T1. 🔴 Status vocabulary is fragmented across surfaces

The portal uses at least three overlapping status vocabularies:

- Sprint/story lifecycle: `backlog`, `ready for dev`, `in progress`, `in review`, `done`
- Epic/story authoring: `Drafted`, `Pending`, plus "no task plan yet" as an implicit state
- Requirements: `Planned`, `Partially implemented`, `Ready for dev`, `Done`, `Deferred`

Concrete confusions observed: Epic 3's card shows dual counts ("Done: 6 · In review: 1") with no framing of what that means for the epic itself; "Drafted" applies to both epics and stories with different implications; requirements say "Planned" where the covering epic says "backlog." A reader following Journey 4 (traceability) has to mentally map between vocabularies at every hop.

**Recommendation:** publish one canonical lifecycle per entity type (requirement / epic / story), document the mapping between them, and route every badge through the existing `--status-*` token system so a given state always gets the same word *and* the same color everywhere. Add a small "status legend" popover reachable from any badge.

### T2. 🔴 The same data is presented multiple ways on one page

- Home shows requirements three ways (inline badge list, coverage matrix, per-epic breakdown).
- Deep Analytics shows change coupling as a graph, a ranked pairs table, *and* explanatory legend prose. (The table is the graph's deliberate accessibility text-twin per Story 3.7's ACs — keep it; it's the third rendering, the prose, that can shrink to a caption.)
- Story counts appear in the epics header and again per epic card.
- Concrete cost of un-reconciled views: the home page says "38 Stories defined" while its sprint block sums to 39 (13+8+0+1+17), and 38 story pages exist. A reader doing Journey 1 hits this immediately.

Each view is individually defensible, but stacked on one page they force the reader to verify the views agree (Journey 1's failure mode). **Recommendation:** per page, pick one primary representation and demote alternates behind a toggle — the sprint page's existing By Status / By Epic radio toggle is the right pattern to copy — never cut a chart's text-twin table, which the portal's accessibility contract requires.

### T3. 🔴 Insight pages are orphaned from the top nav

The top nav is `Home · Readme · PRD · Architecture · ADRs · Epics · Requirements · Sprint · Structure`. Git Insights, Deep Analytics, Action Items, and Deferred Work are reachable only via dashboard deep links. Journeys 6 and 7 have no stable entry point — a user on any interior page cannot reach them without going Home first. **Recommendation:** add an "Insights" nav item (grouping git-insights + deep-analytics) and put Action Items / Deferred Work under Sprint or a "Follow-ups" entry. Meanwhile `Structure` — the weakest page in the portal (see below) — holds a permanent nav slot.

### T4. 🟡 Empty states read as clutter or error, not guidance

"no task plan yet — run /bmad-create-story 4.2" repeats inline on every planless story across the epics page, and "In progress: 0" sits unexplained on the sprint board. The CLI hint is genuinely useful to the Driver but is noise to a Stakeholder and reads like an error to a New Contributor. **Recommendation:** collapse repeated per-story CLI hints into one banner per epic ("3 stories need task plans — run /bmad-create-story"), style empty columns as intentional ("Nothing in progress — pick from Ready"), and keep the copy-able command affordance in one place.

### T5. 🟡 Charts ship without legends, time windows, or "why this matters"

- The commit heatmap's only scale cue is "Less … More" with undefined color steps.
- Git Insights says "top 50 of 781 files" without stating the ranking metric or the time window ("recent history" is unquantified on Deep Analytics).
- Coupling/hotspot sections never say why the reader should care (churn ≈ defect risk; unexpected coupling ≈ hidden dependency).

Every chart should carry: a legend with real values, the time window analyzed, and one framing sentence. This is the difference between Journey 6 delivering insight versus delivering trivia.

### T6. 🟡 The portal assumes BMAD fluency

FR/NFR, AC, ADR, BMad, `/bmad-*` commands, "spec kernel," "quick-dev" all appear undefined. The Driver knows them; Journey 5's personas do not. **Recommendation:** expand acronyms on first use per page (`<abbr>` is enough), add a one-line caption under each surfaced command ("Runs an adversarial review of this story's code"), and consider a short "How to read this portal" page linked from Home's Explore Key Views.

### T7. 🟡 Dates and recency are inconsistently formatted and used

Formats vary ("Thu, Jul 9, 2026" vs heatmap abbreviations vs bare `2026-07-09`). The ADR *index* shows no dates (the ADR bodies have them — surface them in the listing). Change log entries within a story can share a date with nothing but prose ("review revision") to order them. **Recommendation:** one date format token used everywhere; add dates to ADR listings; add sequence markers where multiple events share a day.

### T8. 🟢 Single-contributor phrasing reads oddly

"People to talk to about this file: Matt Eland" on every expanded file row is comic in a solo repo and repetitive even in a team one. Suppress the section when there's one contributor, or reword ("Sole contributor: …").

---

## Per-page feedback

### Home dashboard

The strongest page — dense but genuinely useful for Journey 1. Issues:

- 🔴 **Progress semantics clash:** stories showing "5 of 5 tasks done" while badged `review` or sitting in backlog make task-percent and workflow-state look contradictory. Pair the numbers ("5/5 tasks · awaiting review") wherever both appear.
- 🟡 **Requirements triple-render** (see T2) — the coverage matrix is the keeper; link out for the rest.
- 🟡 **Bare counts without doorways:** "Deferred items (43)" and "Retro Action Items: 5 open" appear with no explanation of what qualifies an item or what the reader should do. One clause of context each.
- 🟢 **"View X →" links** are visually uniform across very different destinations; small icons or grouping would help scanning.

### Sprint page

- 🟢 The By Status / By Epic radio toggle works well and only shows one view at a time — this is the pattern T2 asks other pages to adopt. (Both views live in the DOM, which doubles page weight; an implementation nit, not a UX issue.)
- 🟡 **Backlog vs Ready for dev** both read as "not started" to outsiders; a column-header tooltip ("Ready = task plan exists and dependencies met") fixes it cheaply.
- 🟢 The four `/bmad-*` command cards with Copy / Open in Cursor are excellent Driver ergonomics — add the one-line captions per T6. Epic lane headings already link to their epic pages — good.

### Epics page

- 🔴 **Epic 3 dual-count badge** ("Done: 6 · In review: 1") — restate as a sentence: "6 of 7 done, 1 in review."
- 🟡 Per-story "no task plan yet" repetition — consolidate per T4.
- 🟡 The FR coverage map has no NFR or UX-DR counterpart, implying NFRs are second-class (hurts Journey 4). Add parallel maps or a combined one.
- 🟢 The sunburst gives no cue that it's interactive/clickable; one caption line suffices. (Check `spec-sunburst-epic-focus-and-ready-rollup` / `spec-sunburst-story-link-and-placeholder-alignment` for overlap first.)

### Epic detail pages (reviewed: epic-3)

- 🟡 Story 3.4's retirement notice sits inline among active stories; a collapsed "Retired" section preserves the history without the clutter.
- 🟢 The breadcrumb (Home / Epics / 3 · …) covers upward navigation well — keep it consistent as new detail-page types (dates, commits, code) arrive in Epic 7.

### Story detail pages (reviewed: 3-7 in-review, 1-1 done)

These are the portal's longest pages — roughly 1,900 words (story 1.1) to 6,700 words (story 3.7). The 3.5× variance, more than the average, is what justifies the collapse recommendations below. All story pages already carry an "On this page" TOC — good; keep that invariant as templates evolve.

- 🔴 **Verification evidence is buried:** test counts and completion claims live deep in the Dev Agent Record. Surface a compact "5/5 tasks · 586 tests green · verified 2026-07-09" strip near the status badge for the Reviewer's first glance.
- 🟡 ACs lack visual distinction from surrounding prose; a bordered/tinted block (via existing tokens) would let a Reviewer diff contract-vs-claim quickly. (Overlaps `spec-ac-panel-and-story-card-polish` in implementation-artifacts — verify what that spec already shipped before re-scoping.)
- 🟡 Repeated constraints ("never color-only," reduced-motion) appear in ACs, subtasks, and dev notes; fine in the source spec, but the rendered page could collapse Dev Notes by default, especially on the longest pages.
- 🟡 On a `done` story, `/bmad-code-review` is still the surfaced next step, implying review never happened. Next-step commands should be state-aware (done → retro or "no action needed"). (Related shipped/specced work exists: `spec-hide-code-review-button-ready-for-dev`, `spec-story-next-steps-review-command`, `spec-home-next-steps-label-and-code-review` — audit those before writing a new story; the gap observed live is specifically the *done* state.)
- 🟢 Raw `[[wiki-link]]` names and file:line reference syntax leak into prose; render them as styled chips or move to a references appendix.

### Requirements index + FR detail pages (reviewed: fr1)

- 🔴 **FR detail pages stop at the epic hop** — no story links, no ACs, no code references. This is the weakest link in Journey 4, the product's differentiator. Even before Epic 7's code citations, listing the covering stories (data already exists in the coverage map) would complete the chain.
- 🟡 NFR section shows all seven as "Planned" with no per-item granularity — if NFRs genuinely have no implementation state, say how they *will* be verified instead.
- 🟡 "No coverage" in the requirements-flow diagram merges "deferred on purpose" with "nobody mapped it" — split those.

### Git Insights

- 🟡 Heatmap legend and ranking-metric gaps per T5. (Heatmap contrast already has a spec — `spec-commit-heatmap-contrast-and-day-drilldown` — this extends, not duplicates, that work.)
- 🟡 The March–June dead zone renders as months of empty cells; annotate project start ("First commit Jul 4") or trim the window.
- 🟢 "People to talk to" per T8.

### Deep Analytics

- 🟡 Coupling triple-render per T2: keep the graph *and* the ranked-pairs table (the table is the graph's required accessibility text-twin) but shrink the explanatory prose to a one-line caption.
- 🟡 `sprint-status.yaml ↔ specscribe.css` as the top coupled pair (29×) is an artifact of committing generated/status files with code; a note distinguishing process-coupling from code-coupling would preempt wrong conclusions.
- 🟡 Define the analysis window; "recent history" is not a number.

### Structure page

- 🔴 **This page's scope was retired** by the 2026-07-08 correct-course (Story 3.4 retired; the artifact tree concept was dropped and the source treemap re-seated as Story 7.6). What's live is a flat directory listing (~60 files, self-reported; the rendered list has a few more entries) with inconsistent linking — the least useful page in the portal, occupying a top-nav slot (T3). Recommend removing it from the nav (or redirecting to the epics/artifacts views) until the Epic 7 treemap replaces it.

### ADR index

- 🟡 No dates, no one-line summaries — a reader must open each ADR to learn anything beyond its title. Add both; the ADR body already contains them.
- 🟢 All four ADRs show "Accepted"; fine now, but confirm superseded/deprecated states render distinctly when they arrive.

### Readme page

- 🟢 Renders cleanly; long code blocks (GitHub Actions workflow) need `overflow-x` containment on narrow viewports. Lowest-priority page in the set.

### Action Items page

- 🟡 Two items reference the same Epic 1 heatmap debt from different retros without saying whether they're the same obligation — merge or cross-link them.
- 🟡 Items carry no resolution criteria or destination; each should link to the backlog entry or spec that will close it (Journey 7).
- 🟢 Every item gets an identical "Resolve with AI" affordance, which flattens priority; consider ordering by age or blocking status.

### PRD / planning-artifact rendering

- 🟡 The "On this page" TOC lists 28+ flat entries; group subsections under collapsible parents.
- 🟢 `[ASSUMPTION: …]` tags are semantically important but visually plain; style them like the annotation-comments treatment from Story 2.6.

---

## Suggested priority order

Ordered to serve the daily journeys (1–2) first, per UserJourneys.md, then the traceability differentiator.

1. **T1 status vocabulary + Epic 3 dual-count + task-vs-state pairing + the 38-vs-39 story count clash** — every journey touches status and counts; this is the single highest-leverage fix (Journeys 1–2).
2. **State-aware next-step commands + readiness clarity (Backlog vs Ready tooltip, T4 empty-state consolidation)** — the work-selection journey (Journey 2) ends in these affordances daily.
3. **FR detail story links** — completes the traceability chain (Journey 4) with data the generator already has.
4. **Verification strip + AC styling on story pages** — makes the review journey (Journey 3) fast.
5. **Nav: add Insights, demote/remove Structure** (T3) — cheap, fixes orphaned pages.
6. **Chart legends/time windows** (T5); remaining 🟢 polish as opportunistic work.
