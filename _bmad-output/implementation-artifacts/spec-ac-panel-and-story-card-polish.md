---
title: 'AC display polish: paragraph-aware Gherkin lines, epic-card AC numbering, story-card task indicator'
type: 'feature'
created: '2026-07-06'
status: 'draft'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Three display defects undermine the new AC treatment. (1) Story-page criteria with a trailing note paragraph (e.g. story 2.1 AC #2's "Origin & scope") render chips inline instead of per-line — the styler's depth guard degrades on multi-paragraph bodies. (2) Epic-card AC blocks render the authored bare "1."/"2." lines as stray empty `<ol>` fragments, and lack the story page's "AC #N" labeling. (3) The story card shows tasks twice — a header badge and a bottom progress bar — and the bottom bar is worthless while the header counter over-serves the common all-or-nothing case.

**Approach:** Make `GherkinStyler` paragraph-aware so per-line chips work inside `<p>`-wrapped criteria (trailing notes become their own paragraphs). Rework epic-card AC parsing to consume bare number lines into an "AC #N" label and emit the same `gherkin-line` structure the story page uses, laid out with a shared label+body row. Drop the story card's bottom progress bar and make the header task badge adaptive (checkmark when complete, muted count when untouched, mini donut only for the informative partial case). Align both surfaces with a hanging indent so wrapped clause text lines up after the chip column.

## Boundaries & Constraints

**Always:**
- Never emit mis-nested HTML: `gherkin-line` spans may wrap clause runs only within a single paragraph's content; the cross-paragraph degrade path stays as the last-resort fallback.
- Task-completion indicators stay in neutral/ink tones — green remains reserved for lifecycle *done* status (project color-truthfulness rule); the story's status badge is the only stage color on the card.
- The "AC #N" number comes from the authored bare number line; blocks without one get no invented number.
- Both AC surfaces (story-page criterion panel, epic-card block) share the same visual grammar: number label column, chips column, hanging-indent clause text.

**Ask First:**
- Any change to what the story card links to, panel ordering on the story page, or chart/sunburst behavior.

**Never:**
- Don't alter AC anchor ids (`ac-N`) or the "(AC: #N)" deep-link behavior.
- Don't reintroduce a second task-progress visual on the story card.
- No JS.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Multi-paragraph criterion | `<p>**Given** a **When** b</p><p>**Origin:** note</p>` | Chips per-line inside the first paragraph; note paragraph untouched below | N/A |
| Single-flow criterion | No `<p>` wrappers (current AC #1 shape) | Unchanged per-line behavior | N/A |
| Nested keyword | Keyword inside `<em>`/`<a>` within a paragraph | Degrade to in-place chip for that flow, valid HTML | N/A |
| Bare number line | `1.` on its own line before `**Given**` in epics.md | Consumed into `AC #1` label; no empty `<ol>` emitted | N/A |
| Missing number line | AC block starts directly with `**Given**` | Block renders without a number label | N/A |
| Tasks all done | TasksDone == TasksTotal > 0 | Badge: `✓ N tasks`, no donut | N/A |
| Tasks untouched | TasksDone == 0, TasksTotal > 0 | Badge: muted `0/N tasks`, no donut | N/A |
| Tasks partial | 0 < TasksDone < TasksTotal | Badge: mini donut + `D/N tasks` (unchanged) | N/A |
| No tasks | TasksTotal == 0 | No badge, no bar (unchanged) | N/A |
| Bottom bar | Any story card | `per-story-progress` bar no longer rendered | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/GherkinStyler.cs` -- `StyleCriterion` + `AllTopLevel` depth guard; needs a paragraph-content pass before the whole-string slice.
- `src/SpecScribe/EpicsParser.cs` -- `ParseStory` AC loop (bare "N." lines currently become their own blocks → Markdig empty `<ol>`), `RenderAcLine`; `AcBlocksHtml` remains `IReadOnlyList<string>` with label+lines baked into each block's HTML.
- `src/SpecScribe/EpicsTemplater.cs` -- `AppendStoryCard` (`task-badge` emission, `per-story-progress` block to delete); `RenderStoryPlaceholder` reuses `AcBlocksHtml` so it inherits the fix.
- `src/SpecScribe/assets/specscribe.css` -- `.gherkin-line` (add hanging indent), `.ac-block`/`.ac-list`, `.ac-anchor` (style to share with new `.ac-num`), `.story-card .per-story-progress` rule (line ~1414, delete), `.status-badge.task-badge` (~line 867).
- `tests/SpecScribe.Tests/GherkinStylerTests.cs`, `EpicsParserTests.cs`, `SiteGeneratorStoryEpicPagesTests.cs` -- existing homes for the new cases.

## Tasks & Acceptance

**Execution:**
- [ ] `src/SpecScribe/GherkinStyler.cs` -- when the criterion HTML contains top-level `<p>…</p>` blocks, run the line-slicing pass on each paragraph's inner content (depth guard relative to that content); otherwise keep the current single-flow path -- multi-paragraph criteria regain per-line chips without nesting hazards.
- [ ] `src/SpecScribe/EpicsParser.cs` -- in `ParseStory`'s AC loop: treat a bare `^\d+\.$` line as the number of the following block (flush current block first, don't emit the number line as content); compose each block as an optional `<span class="ac-num">AC #N</span>` label plus per-line `<span class="gherkin-line">` spans (replacing the `<br>` join) -- kills the empty `<ol>` artifacts and matches the story page's grammar.
- [ ] `src/SpecScribe/EpicsTemplater.cs` -- `AppendStoryCard`: delete the `per-story-progress` block; make the task badge adaptive per the I/O matrix (`✓ N tasks` / muted `0/N tasks` / donut + `D/N`) -- one honest indicator instead of two redundant ones.
- [ ] `src/SpecScribe/assets/specscribe.css` -- hanging indent on `.gherkin-line` (chip column + aligned wrap, `text-indent` reset on the chip); `.ac-num` styled like `.ac-anchor` (gold mono, no link); `.ac-block` as label+body row consistent with `.ac-criterion`; delete the `per-story-progress` rule; muted/complete task-badge variants -- shared visual grammar across both surfaces.
- [ ] `tests/SpecScribe.Tests/GherkinStylerTests.cs` -- cases: multi-paragraph criterion gets gherkin-lines inside `<p>` and leaves the note paragraph alone; nested-keyword degrade still holds within a paragraph.
- [ ] `tests/SpecScribe.Tests/EpicsParserTests.cs` -- cases: bare number line becomes `AC #N` label, no `<ol>` in `AcBlocksHtml`; numberless block renders without label.
- [ ] `tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs` -- end-to-end: epic page has no `per-story-progress` and no empty `<ol>` in AC blocks; a multi-paragraph AC on the story page contains `gherkin-line` spans.

**Acceptance Criteria:**
- Given a story artifact criterion with a trailing note paragraph, when the story page renders, then each Gherkin clause sits on its own line with a chip and the note renders as its own paragraph below.
- Given epics.md ACs authored with bare number lines, when an epic page renders, then each block shows an "AC #N" label with per-line chips and no empty list fragments.
- Given a story with 33/33 tasks done, when its card renders on the epic page, then exactly one task indicator appears (✓ badge in the header) and no bottom progress bar.

## Spec Change Log

## Verification

**Commands:**
- `dotnet build SpecScribe.slnx` -- expected: clean.
- `dotnet test SpecScribe.slnx` -- expected: all pass including new cases.

**Manual checks (if no CLI):**
- Regenerate; on epic-2.html check story 2.1's card: AC #1/AC #2 labels, chips aligned, no stray numbers, single header task badge, no bottom bar. On story-2-1.html check AC #2: chips per-line, "Origin & scope" as its own paragraph, wrapped text aligned after the chip column.
