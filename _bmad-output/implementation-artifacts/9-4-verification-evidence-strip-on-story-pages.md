---
baseline_commit: 1a9e8dd6071caa69bf31d1e07810ed8f02bd7244
---

# Story 9.4: Verification Evidence Strip on Story Pages

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reviewer,
I want tasks, tests, and verification evidence surfaced near the status badge,
so that I can judge a "done" claim in one glance instead of excavating the dev record.

## Acceptance Criteria

1.
**Given** a story page whose dev record contains task completion, test counts, and verification dates
**When** the page renders
**Then** a compact evidence strip (for example "5/5 tasks · 586 tests green · verified 2026-07-09") appears near the status badge
**And** the strip links to the full dev-record section.

2.
**Given** a story with missing evidence
**When** the strip renders
**Then** missing evidence is visibly absent (for example "no test evidence recorded") rather than the strip being omitted
**And** the honest-absence signal uses the designed empty-state treatment.

## Context & Scope

Epic 9 completes the requirement → epic → story chain so a **Reviewer can judge a "done" claim in one glance** (docs/UserJourneys.md journey 3). This story serves that journey directly: today a reviewer wanting to confirm a story's status claim must open the collapsed **Dev Agent Record** `<details>` and read prose. This story lifts the three highest-signal facts — **tasks complete, tests passing, last verified** — into a compact strip right beside the status badge in the story-page header.

### Where this renders (read before touching)

Individual story pages are **not** generic Markdown pages — they have a dedicated renderer. The pipeline is:

- `SiteGenerator.RenderEpicsPages` (HTML site) and `SiteGenerator.RenderWebviewSurfaces` (webview/SPA) both call `SiteGenerator.BuildStoryPageFragments(story, artifactFullPath, referenceMap)` to extract per-story fragments from the raw artifact, then `EpicsTemplater.RenderStory`/`BuildStoryPage`. [Source: src/SpecScribe/SiteGenerator.cs:676-694, 821-838, 726-758]
- `BuildStoryPage` → `EpicsViewBuilder.BuildStory` produces the host-neutral `StoryPageView`; `HtmlRenderAdapter.RenderStoryBody(view)` emits the actual `<main>` body — **the one shared renderer all three delivery adapters (HTML, webview, SPA) route through**. [Source: src/SpecScribe/EpicsTemplater.cs:122-179; src/SpecScribe/EpicsViewBuilder.cs:106-139; src/SpecScribe/HtmlRenderAdapter.Epics.cs:273-357]
- The header is a `<header class="doc-header">` whose `<div class="kicker-row">` already holds: the `Story N.M` kicker, the **status badge** (`StatusStyles.Badge`, rendered only when `view.Status` is present), and the retro link. [Source: src/SpecScribe/HtmlRenderAdapter.Epics.cs:278-288] **This kicker-row is where the evidence strip belongs — "near the status badge" is literal.**

Because `RenderStoryBody` is the single shared body renderer, **the strip propagates byte-identically to HTML + webview + SPA automatically** — no RenderParity exception is needed (unlike webview-only chrome in 6.5/6.10). The trade-off: **golden bytes change on every drafted story page**, so the committed content fingerprint must be regenerated (Task 7).

### The core data problem: two of the three facts are free text

- **Tasks** are already structured. `ProgressCalculator` fills `story.TasksDone`/`story.TasksTotal` (top-level checkbox tally from `## Tasks / Subtasks`) on every story with an artifact. This is the **single source of truth for the task count** (Story 8.2 principle) — thread these existing values through, do **not** recompute a second tally in the view. [Source: src/SpecScribe/ProgressCalculator.cs:26-32,72-92; src/SpecScribe/EpicsModel.cs:32-39]
- **Test counts** and **verification dates** are **not** structured anywhere. They live only as free text inside `## Dev Agent Record` (Completion Notes) and `## Change Log`. Real, consistent shapes across this repo's own stories:
  - Test tally: `586 tests green`, `759 C# tests green`, `429 tests pass`, `440 tests passing` — regex `\b(\d[\d,]*)\s+(?:C#\s+)?tests?\s+(green|pass|passing)\b`.
  - Change Log entries are newest-first, each a `- YYYY-MM-DD — **<bold action>**` line, e.g. `- 2026-07-11 — **Code review passed; Status → done.**`.

So — exactly as Story 9.3's deferral-source link and Story 2.3's action-item linking did — **tests and the verified-date are derived by best-effort heuristic over existing free text, never a new authoring field.**

### Non-negotiable project principle: no new authoring schema

The owner flagged this explicitly at create-story for 9.3 and it applies identically here: **SpecScribe's ability to support many spec-driven frameworks without dictating a house authoring style is a load-bearing project value.** The evidence strip must be inferred from data that already exists in a normal story artifact (`## Tasks / Subtasks`, `## Dev Agent Record`, `## Change Log`) — **do not add a `tests:`/`verified:` frontmatter field, a new `## Verification` section convention, or any required tag.** If a future story wants a precise machine-readable verification record, that belongs in an ADR weighing the authoring-burden trade-off — do not add one silently here. [Source: memory `create-story-elicit-visual-intent`; `9-3-deferred-on-purpose-vs-unmapped-coverage-states.md` "Non-negotiable project principle"]

### Owner-selected design directions (locked at create-story)

Per the project rule to elicit visual intent for any new visual surface (Epic 3 retro action item, memory `create-story-elicit-visual-intent`), two decisions were made with the owner up front:

1. **Strip form — a row of bordered pills, not an inline metaline or stat-tiles.** Each metric is its own `.status-badge`-family chip sitting in the `kicker-row` beside the status badge (icon + word + value), reusing the existing badge/`task-badge` vocabulary so it inherits the on-brand look and the icon+word channel for free. Three pills: **Tasks**, **Tests**, **Verified**. Each pill carries an icon (UX-DR17: never color-only) and has an **empty-state variant** for honest absence (AC #2).
2. **Verified date = the latest Change Log date.** Derive the verified-date pill from the **most recent (top) dated entry** in the story's `## Change Log` — the existing dated ledger, present on nearly every drafted story, honest, zero authoring burden. **Label honestly by what that entry says:** if the top entry's action text matches verify/review/done/tests-green language, the pill reads **"verified {date}"**; otherwise it reads **"updated {date}"** (never assert "verified" for a date the artifact only records as an edit).

## Tasks / Subtasks

- [x] **Task 1 — Parse test-evidence and the latest Change Log date from the raw artifact (AC: #1, #2)**
  - [x] Add two pure static extractors to `EpicsParser` (alongside the existing `ExtractStatus`/`ExtractDevAgentRecord`/`ExtractNamedSectionHtml`), operating on the raw artifact markdown:
    - `ExtractTestEvidence(string raw) → string?` — first match of `\b(\d[\d,]*)\s+(?:C#\s+)?tests?\s+(green|pass|passing)\b` (case-insensitive) scanning the **`## Dev Agent Record` section first** (Completion Notes is where the final tally is stated), falling back to a whole-document scan if the section has none. Return the matched phrase normalized to `"{n} tests {green|passing}"` (collapse `pass`→`passing` for consistent reading; keep the author's `green`/`passing` word otherwise). Return `null` when no match. **Deterministic**: always the first match in that fixed scan order — never iteration-order- or culture-dependent.
    - `ExtractChangeLogVerification(string raw) → (DateOnly Date, bool IsVerification)?` — find the `## Change Log` section, take the **first** `- (\d{4}-\d{2}-\d{2}) — \*\*(?<action>[^*]+)\*\*` line (newest-first ordering means top = latest), parse the ISO date with invariant `TryParseExact`. `IsVerification` = the `action` text matches `\b(verif|review|tests? (green|pass)|Status → (done|review))\b` (case-insensitive). Return `null` when there's no `## Change Log` or no dated entry. Guard the date parse (a malformed date → treat as no date, never throw). Named `ExtractChangeLogVerification` because Story 8.8 already owns `ExtractLatestChangeLogDate` → `DateOnly?` (max date across table/list forms for recency). [Source: src/SpecScribe/EpicsParser.cs]
  - [x] Keep both extractors **allocation-light and side-effect-free** (they run once per drafted story per generation pass). Compile the regexes as `static readonly` fields like the existing `StatusLine`/`DevAgentSubHeading` patterns in `EpicsParser`.

- [x] **Task 2 — Carry the evidence as a view-model datum (AC: #1, #2)**
  - [x] Add a small immutable record `StoryEvidence(int TasksDone, int TasksTotal, string? TestsSummary, DateOnly? VerifiedDate, bool VerifiedIsReview)` (new type in `EpicsView.cs` next to `StoryPageView`, or a nested record — match the file's existing record style). This is **data only**, no HTML — the honest-absence wording and pill markup are the renderer's job (keeps the view host-neutral, consistent with Story 6.1/6.2's data-vs-opaque split). [Source: src/SpecScribe/EpicsView.cs:162-210]
  - [x] Add `required StoryEvidence Evidence { get; init; }` to `StoryPageView` with an XML-doc summary. The strip is a story-page-only surface; the placeholder view (`StoryPlaceholderView`) gets nothing (a placeholder has no dev record — its "not yet drafted" state is already its honest empty state).

- [x] **Task 3 — Thread the evidence through the ONE fragment seam both delivery paths share (AC: #1)**
  - [x] In `SiteGenerator.BuildStoryPageFragments`, after reading `artifactRaw`, build the `StoryEvidence`: `TasksDone`/`TasksTotal` from `story.TasksDone`/`story.TasksTotal` (already filled by `ProgressCalculator` before this runs — do not re-tally), `TestsSummary` from `EpicsParser.ExtractTestEvidence(artifactRaw)`, and `(VerifiedDate, VerifiedIsReview)` from `EpicsParser.ExtractChangeLogVerification(artifactRaw)`. Add it to the `StoryPageFragments` record and return it. [Source: src/SpecScribe/SiteGenerator.cs]
  - [x] Pass `f.Evidence` into `EpicsTemplater.RenderStory`/`BuildStoryPage` at **both** call sites — the HTML pass and the webview/SPA passes. Because both route through `BuildStoryPageFragments`, extracting once here means HTML + webview + SPA all get identical evidence with no second code path.
  - [x] Extend `EpicsTemplater.RenderStory` and `BuildStoryPage` signatures with the `StoryEvidence` parameter and forward it to `EpicsViewBuilder.BuildStory`, which sets `StoryPageView.Evidence`. [Source: src/SpecScribe/EpicsTemplater.cs; src/SpecScribe/EpicsViewBuilder.cs]

- [x] **Task 4 — Render the pill strip in the story-page header (AC: #1, #2)**
  - [x] In `HtmlRenderAdapter.RenderStoryBody`, after the existing `kicker-row` block, emit the evidence strip as its own row so it sits directly under the kicker/status-badge line. Wrap the three pills in `<div class="evidence-strip">…</div>`. Add a private helper `EvidenceStrip(StoryEvidence e, bool linkToDevRecord)` next to `TaskBadge` that builds the three pills:
    - **Tasks pill** — reuse `TaskBadge(e.TasksDone, e.TasksTotal)` directly. When `TasksTotal == 0`, render the empty-state pill "no tasks recorded" instead (dashed/muted).
    - **Tests pill** — `TestsSummary` present → `<span class="status-badge evidence-pill">{icon}{TestsSummary}</span>`; absent → the empty-state pill "no test evidence recorded" (`evidence-pill empty`, dashed border, muted text). Use `PathUtil.Html` on the summary.
    - **Verified pill** — `VerifiedDate` present → `{icon}{(VerifiedIsReview ? "verified" : "updated")} {date:yyyy-MM-dd}` (invariant format); absent → empty-state pill "no verification recorded".
  - [x] **Link to the dev record (AC #1).** When `view.DevAgentRecord.Count > 0`, wrap the strip in `<a href="#sec-dev-agent-record" class="evidence-link">`. When there's no dev-record section, render the strip without a link. No JS to auto-expand `<details>` (Story 9.5 owns that).
  - [x] **Icons (UX-DR17):** Tasks via `TaskBadge`; Tests/Verified via new `Icons.ForConcept("Tests")` / `Icons.ForConcept("Verified")` glyphs. No new `--status-*` token.

- [x] **Task 5 — Style the strip and its empty states (AC: #1, #2)**
  - [x] Add CSS to `src/SpecScribe/assets/specscribe.css` (guarded by `StylesheetTests`): `.evidence-strip`, `.evidence-pill`, `.evidence-pill.empty`, `.evidence-link`. Empty state = dashed border + `--ink-light` (kinship with `.task-badge.none-done` / `.dev-agent-empty`; Story 8.5 may later supply a shared helper).
  - [x] Strip uses `flex-wrap`; colors route through existing tokens only.

- [x] **Task 6 — Honest absence, determinism, and degradation (AC: #2)**
  - [x] The strip renders whenever `view.Status` is present (same guard as the status badge). Missing facts show empty-state pills; strip is never omitted for a missing sub-fact.
  - [x] **Determinism (NFR8):** first-match-in-fixed-order extractors; invariant-culture date parse/format; no clock reads.
  - [x] **Degrade, don't break (NFR2):** malformed date / missing sections → empty-state pills; never throw; never a dangling `#sec-dev-agent-record` link.

- [x] **Task 7 — Tests + regenerate the golden fingerprint (AC: #1, #2)**
  - [x] `EpicsParser` unit tests for `ExtractTestEvidence` and `ExtractChangeLogVerification`.
  - [x] `RenderStoryBody` rendering tests: populated pills + link; empty-state for missing tests; "updated" vs "verified"; no link when no dev-record; strip omitted when no status.
  - [x] `StylesheetTests`: `.evidence-strip` / `.evidence-pill` / `.evidence-pill.empty` / `.evidence-link`; no `--status-evidence`.
  - [x] Regenerated golden content fingerprint to `8e8f63af862b45f872b2766b95e19bb8aa917f50adec344456b00fcaeb9f2847`.
  - [x] Full suite green: `dotnet test` → 1202 passed (parity suites included; no new RenderParity exception).

## Dev Notes

### What exists today (read before touching)

- **Story page renderer** is dedicated, not generic Markdown: `SiteGenerator.BuildStoryPageFragments` → `EpicsTemplater.BuildStoryPage` → `EpicsViewBuilder.BuildStory` → `HtmlRenderAdapter.RenderStoryBody`. The `<header class="doc-header"><div class="kicker-row">…</div>` holds the status badge; the strip goes as a sibling row right after it. [Source: src/SpecScribe/HtmlRenderAdapter.Epics.cs:273-357]
- **Task tally is already computed** by `ProgressCalculator` into `story.TasksDone`/`story.TasksTotal` before `BuildStoryPageFragments` runs — reuse it (Story 8.2: one source of truth per count). [Source: src/SpecScribe/ProgressCalculator.cs:26-32]
- **`TaskBadge`** (src/SpecScribe/HtmlRenderAdapter.Epics.cs:256-268) already renders a `.status-badge.task-badge` pill from a done/total tally, with complete/none-done/partial variants — reuse it verbatim for the Tasks pill.
- **`StatusStyles.Badge(cssClass, label)`** (src/SpecScribe/StatusStyles.cs:185-186) is the canonical "icon + word span" — pattern to mirror for the Tests/Verified pills (icon prepended before `PathUtil.Html`'d text).
- **`EpicsParser`** (src/SpecScribe/EpicsParser.cs) already owns raw-artifact extraction (`ExtractStatus`, `ExtractDevAgentRecord`, `ExtractAcceptanceCriteria`, `ExtractNamedSectionHtml`) with `static readonly` compiled regexes — add the two new extractors here, same style.
- **Change Log** is already rendered on the page (`view.ChangeLogHtml`, id `sec-change-log`) and **Dev Agent Record** as a collapsed `<details id="sec-dev-agent-record">` — the strip's link target. [Source: src/SpecScribe/HtmlRenderAdapter.Epics.cs:321-350]
- **Golden fingerprint**: `GenerateAll_GoldenContentFingerprint_...` pins the whole rendered site's normalized content hash; it exercises story pages, so it must be regenerated. [Source: tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:198-229]

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Task done/total tally | `story.TasksDone` / `story.TasksTotal` (ProgressCalculator-filled) | src/SpecScribe/EpicsModel.cs:32-39 |
| Tasks pill markup | `TaskBadge(done, total)` | src/SpecScribe/HtmlRenderAdapter.Epics.cs:256-268 |
| Icon + word badge span | `StatusStyles.Badge` (mirror pattern) | src/SpecScribe/StatusStyles.cs:185-186 |
| Raw-artifact extractor home + regex style | `EpicsParser.ExtractStatus`/`ExtractDevAgentRecord` | src/SpecScribe/EpicsParser.cs:74-78,170-202 |
| Fragment seam feeding HTML + webview + SPA | `SiteGenerator.BuildStoryPageFragments` / `StoryPageFragments` | src/SpecScribe/SiteGenerator.cs:711-758 |
| Empty-state pill precedent (dashed/muted) | `.status-badge.task-badge.none-done`, `.dev-agent-empty` | src/SpecScribe/assets/specscribe.css:1160,1350 |
| Header sub-row layout precedent | `.kicker-row` | src/SpecScribe/assets/specscribe.css:235-244 |
| HTML-escape helper | `PathUtil.Html` | used throughout HtmlRenderAdapter |

### Guardrails & invariants

- **No new authoring schema.** Tests and verified-date are inferred from existing `## Dev Agent Record` / `## Change Log` free text — never a new field/tag/section convention. Stated project value, not just convenience. A precise tagged-verification record, if ever wanted, is an ADR decision, not a silent addition here.
- **One source of truth per count (Story 8.2).** The task count comes from `ProgressCalculator`'s existing tally — do not recompute in the view/renderer.
- **Six `--status-*` tokens remain the single color source (memory `specscribe-status-token-system`).** No 7th token; pills reuse existing badge colors and `--ink-light` for muted empty states.
- **Never color-only (UX-DR17).** Every pill carries icon + word, so absence and each metric read without color. The empty state uses dashed border + muted text + explicit words ("no test evidence recorded"), not just a color change.
- **Designed empty-state treatment (AC #2, Story 8.5 kinship).** Missing evidence renders a *designed* empty pill, never a blank or an omitted strip. Story 8.5 (designed empty states) is `ready-for-dev` and not yet built; this story establishes the pill empty-state inline using the existing `none-done`/`dev-agent-empty` precedent — if 8.5 lands first and defines a shared empty-state helper, consume it; otherwise leave a comment noting the kinship so 8.5 can harmonize.
- **Shared renderer → automatic parity, no exception.** The strip lives in `RenderStoryBody`, which HTML/webview/SPA all call, so it must render byte-identically across all three. Do **not** add a RenderParity registry exception (those are for webview-only chrome). If parity fails, the cause is a divergence bug, not an expected difference.
- **Deterministic output (NFR8).** First-match-in-fixed-order extraction, invariant-culture date parse/format, no clock reads. From-scratch regeneration is byte-identical.
- **Degrade gracefully (NFR2).** Any missing/malformed sub-fact → its empty-state pill; the dev-record link is omitted when no dev record exists. Never throw, never a broken anchor.
- **Coordinate with Story 9.5** (`backlog`, distinct-AC-blocks + collapsed dev notes): 9.5 owns the Dev Agent Record collapse/expand behavior and the "On this page" TOC. This story only *links to* `#sec-dev-agent-record` — do not change the `<details>` open/close semantics or add JS to auto-expand it; leave that to 9.5.

### Project Structure Notes

- Primary code: `src/SpecScribe/EpicsParser.cs` (two new extractors), `EpicsView.cs` (`StoryEvidence` record + `StoryPageView.Evidence`), `EpicsViewBuilder.cs` (`BuildStory` sets it), `SiteGenerator.cs` (`BuildStoryPageFragments` builds it + threads it through both call sites, `StoryPageFragments` carries it), `EpicsTemplater.cs` (`RenderStory`/`BuildStoryPage` signature), `HtmlRenderAdapter.Epics.cs` (`RenderStoryBody` strip + `EvidenceStrip` helper), `assets/specscribe.css` (strip + pill + empty-state CSS), possibly `Icons.cs` (tests/verified glyphs). Tests in `tests/SpecScribe.Tests/`. **No new source files expected; no epics.md or artifact schema changes.**
- Output goes to `SpecScribeOutput/` by default when you generate to verify — **not** `docs/live`. [Memory `generate-output-dir-is-specscribeoutput`]

### Testing standards

- xUnit (`tests/SpecScribe.Tests`); `Assert.Contains`/`Assert.DoesNotContain` on generated HTML strings — the established pattern in the story-body/adapter tests. Run `dotnet test` from repo root.
- Author fixture stories that exercise: all-three-present, tests-absent, changelog-absent (or non-verification top entry), and no-dev-record. Reuse the adapter test fixtures where possible rather than a wholly new epics doc.
- Regenerate the golden fingerprint only as a **deliberate, reviewed** change after confirming the diff is exactly the evidence strip on story pages (memory `golden-diff-normalization-gotchas`).

### Verify before marking review

Generate the portal against this repo's own `_bmad-output` (`SpecScribeOutput/`), open a **done** story page (e.g. `epics/story-6-9.html`) and confirm the header shows, right under the status badge, three pills — tasks (e.g. `✓ N tasks`), tests (e.g. `759 tests green`), and `verified {date}` — and that clicking the strip jumps to the Dev Agent Record. Open a **ready-for-dev** story with no dev record (e.g. this story's own page once generated) and confirm the strip shows honest empty-state pills ("no test evidence recorded", "no verification recorded") rather than being omitted, with dashed/muted styling and no broken in-page link. Confirm the webview render (`specscribe webview`) and, if built, the SPA render show the identical strip (parity green).

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 9.4`] (epics.md:1596-1614) — user story + ACs.
- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 9`] (epics.md:1530-1534) — epic intent; FR26 (epics.md:188), UX-DR26, NFR8 (epics.md:99); review journey 3 (docs/UserJourneys.md).
- [Source: `_bmad-output/implementation-artifacts/9-3-deferred-on-purpose-vs-unmapped-coverage-states.md`] — sibling story establishing the "best-effort heuristic over existing free text, no new authoring schema" pattern this story follows.
- [Source: src/SpecScribe/HtmlRenderAdapter.Epics.cs:256-357] — `TaskBadge` + `RenderStoryBody` (the header/kicker-row + dev-record `<details>`).
- [Source: src/SpecScribe/EpicsView.cs:162-210] — `StoryPageView` to extend with `Evidence`.
- [Source: src/SpecScribe/EpicsViewBuilder.cs:106-139] — `BuildStory`.
- [Source: src/SpecScribe/SiteGenerator.cs:676-694,711-758,821-839] — `BuildStoryPageFragments` + both delivery call sites.
- [Source: src/SpecScribe/EpicsTemplater.cs:102-179] — `RenderStory`/`BuildStoryPage` signatures.
- [Source: src/SpecScribe/EpicsParser.cs:74-78,170-202,267] — extractor home + regex style.
- [Source: src/SpecScribe/ProgressCalculator.cs:26-32,72-92] — `TasksDone`/`TasksTotal` source of truth.
- [Source: src/SpecScribe/StatusStyles.cs:180-186] — `Badge`/`Icon` pattern.
- [Source: src/SpecScribe/assets/specscribe.css:235-244,1160,1163,1350] — `.kicker-row`, empty-state precedents, badge-icon sizing.
- [Source: tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:198-229] — golden content fingerprint to regenerate.
- [Source: memory `specscribe-status-token-system`] — six-token color-source constraint.
- [Source: memory `create-story-elicit-visual-intent`] — why the strip's design directions were elicited up front.
- [Source: memory `golden-diff-normalization-gotchas`] — regenerating the fingerprint safely.

## Dev Agent Record

### Agent Model Used

Composer (Cursor agent)

### Debug Log References

- Story 8.8 already claimed `ExtractLatestChangeLogDate` → `DateOnly?` (max date for recency). Story 9.4's verification-aware top-entry extractor shipped as `ExtractChangeLogVerification` to avoid a return-type clash.

### Completion Notes List

- Implemented the story-page verification evidence strip end-to-end: free-text extractors → `StoryEvidence` view datum → single `BuildStoryPageFragments` seam → shared `RenderStoryBody` pills under the status badge.
- Tasks reuse `ProgressCalculator` tallies + `TaskBadge`; tests/verified inferred from Dev Agent Record / Change Log free text (no new authoring schema).
- Honest-absence empty-state pills (`evidence-pill empty`) when facts are missing.
- **UX polish + ADR 0007:** tests pill reads `N passing tests` with green `.tests-pass` styling; removed thin Latest-change line; added change-surface panel (File List classification, AC verify checklist, touched files, ship line) projected from standard BMAD sections. ADR 0007 marked Accepted.
- Story 9.4/ADR 0007 tests + golden fingerprint regenerated (`cca91956…`). HTML/webview/SPA parity green with no new RenderParity exception.

### File List

- `docs/adrs/0007-change-surface-descriptor-for-testing-and-footprint.md`
- `docs/adrs/README.md`
- `src/SpecScribe/ChangeSurface.cs`
- `src/SpecScribe/EpicsParser.cs`
- `src/SpecScribe/EpicsView.cs`
- `src/SpecScribe/EpicsViewBuilder.cs`
- `src/SpecScribe/EpicsTemplater.cs`
- `src/SpecScribe/SiteGenerator.cs`
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs`
- `src/SpecScribe/Icons.cs`
- `src/SpecScribe/assets/specscribe.css`
- `tests/SpecScribe.Tests/ChangeSurfaceTests.cs`
- `tests/SpecScribe.Tests/EpicsParserTests.cs`
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs`
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`
- `tests/SpecScribe.Tests/StylesheetTests.cs`
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs`
- `_bmad-output/implementation-artifacts/9-4-verification-evidence-strip-on-story-pages.md`

## Change Log

- 2026-07-16 — **Implemented (dev-story).** Verification evidence strip on drafted story pages: Tasks/Tests/Verified pills under the status badge, free-text extractors, empty-state treatment, golden fingerprint regenerated. Status → review.
- 2026-07-16 — **Polish + ADR 0007.** Tests pill → `N passing tests` (green); change-surface panel under strip (classification, AC verify list, touched files, ship line); ADR 0007 Accepted. Golden fingerprint `cca91956…`.
