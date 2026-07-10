# Story 9.1: Requirement Pages Link to Their Covering Stories

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a stakeholder tracing a requirement,
I want FR/NFR detail pages to list the stories delivering them with current status,
so that I can go from a requirement ID to its stories without reading an epics document.

## Acceptance Criteria

1.
**Given** a requirement covered by one or more stories in the coverage map
**When** its detail page renders
**Then** the page lists each covering story with its canonical status, linked to the story page
**And** the listing is built from existing coverage-map data with no new authoring burden.

2.
**Given** a requirement with no covering stories
**When** its detail page renders
**Then** the page states that explicitly rather than omitting the section
**And** the statement distinguishes deferred from unmapped when Story 9.3's states are available.

## Context & Scope

Epic 9 completes the requirement → epic → story chain. This first story closes the **last hop**: the FR/NFR detail page already resolves and shows the *covering epic*, but a stakeholder still has to open that epic and scan for the relevant stories. This story surfaces the covering **stories** directly on the requirement page — linked, status-badged — so a requirement ID leads straight to the work delivering it.

**This is almost entirely a rendering story.** Every data primitive already exists and is unit-tested:

- `RequirementsParser.StoriesFor(req, epics)` already resolves a requirement's `CoverageEpicNumbers` to their stories, in deterministic order (epics in coverage order, stories in each epic's declared order), skipping missing epics and returning empty for deferred/unmapped requirements. [Source: src/SpecScribe/RequirementsParser.cs:164]
- `StoryInfo` already carries `Id`, `Title`, `Status` (raw artifact status), `TasksDone`/`TasksTotal`, and `ArtifactOutputPath`. [Source: src/SpecScribe/EpicsModel.cs:7]
- `StatusStyles.ForStory` / `StoryLabel` / `Badge` already produce the canonical status css-class + human label + color-icon-word badge. [Source: src/SpecScribe/StatusStyles.cs:10]

**Do not build a new parser, a new status classifier, or a second FR→story resolver.** Wire the existing `StoriesFor` output into `RequirementsTemplater.RenderRequirement`.

### Chosen visual direction — small cards grouped by epic (owner-selected)

The covering-stories listing renders as **small story cards, grouped under each covering epic**. Each covering epic (from `req.CoverageEpicNumbers`, in order) becomes a group: a linked epic header carrying the epic's status + a one-line tally, followed by that epic's stories rendered as compact cards (a small task donut, Story id + linked title, and a canonical status badge). Reuse the existing `epic-mosaic-card` / `coverage-card` visual language and `Charts.MiniDonut` — do **not** invent a new card style or new CSS tokens.

### Truthfulness caveat you MUST honor (epic-level granularity)

The FR Coverage Map is **epic-level**, so `StoriesFor` returns **every story in the covering epic(s)** — not a per-requirement-mapped subset (that data does not exist in the source). This is the same honesty caveat documented throughout `RequirementInfo`/`RequirementsParser`. [Source: src/SpecScribe/RequirementsModel.cs:5-16, src/SpecScribe/RequirementsParser.cs:173-184]

Therefore the section's framing must be truthful: label it in a way that says **"stories in the covering epic(s)"**, not "stories that deliver *only* this requirement." Grouping by epic (the chosen direction) makes this granularity visible and honest — the reader sees the stories belong to Epic N, not to the FR directly. Do not phrase copy as if the mapping were per-story. (This directly serves the recurring project value that visuals must never over-claim precision — see Story 1.5 truthfulness, and Epic 3's repeated "parser silently drops / over-claims a real input shape" retro item.)

## Tasks / Subtasks

- [ ] **Task 1 — Thread `EpicsModel` into `RenderRequirement` (AC: #1)**
  - [ ] Change `RequirementsTemplater.RenderRequirement` signature from `(RequirementInfo req, EpicInfo? coveringEpic, ProgressModel progress, SiteNav nav)` to add `EpicsModel epics` (keep `coveringEpic`/`progress` — the per-epic donut still needs `progress.PerEpic`). [Source: src/SpecScribe/RequirementsTemplater.cs:92]
  - [ ] Update the sole caller `SiteGenerator.WriteRequirements` (which already has `model`) to pass it. [Source: src/SpecScribe/SiteGenerator.cs:850,865] — confirmed the only caller via grep.

- [ ] **Task 2 — Render covering stories grouped by epic (AC: #1)**
  - [ ] In the Coverage section (`RequirementsTemplater.RenderRequirement`, currently src/SpecScribe/RequirementsTemplater.cs:123-149), replace the single primary-epic coverage card with **per-covering-epic groups**. Iterate `req.CoverageEpicNumbers` in order (not just the primary `CoverageEpicNumber`); resolve each to an `EpicInfo` from `epics.Epics` (skip numbers with no matching epic, mirroring `StoriesFor`'s best-effort resolution). This intentionally also fixes the pre-existing gap where a multi-epic requirement (e.g. FR2: "Epics 1 & 2") showed only Epic 1's coverage.
  - [ ] For each epic group, render a linked epic header reusing the existing coverage-card data (status badge + `{done}/{total} tasks · {detailed}/{count} stories detailed` tally, from `progress.PerEpic.FirstOrDefault(p => p.Number == epic.Number)`). You may keep `AppendCoverageCard` as the group header, or factor a lighter header — either is fine; do not duplicate the donut logic, call the existing helper. [Source: src/SpecScribe/RequirementsTemplater.cs:160-202]
  - [ ] Under each group header, render every `story` in `epic.Stories` as a compact card. Card contents:
    - A small per-story donut. Reuse `Charts.MiniDonut(story.TasksDone, story.TasksTotal)` (src/SpecScribe/Charts.cs:101) or `Charts.Donut([("Done", story.TasksDone, "done"), ("Remaining", story.TasksTotal - story.TasksDone, "pending")], size: 64)` for stories with a task plan; render an empty ring for `TasksTotal == 0`.
    - Story id + title, linked to the story page: `story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id)`, prefixed with the page's relative `prefix`. **Every story always has a page** (real artifact page or generated placeholder) — never emit a dead-ended plain-text title. [Source: src/SpecScribe/EpicsTemplater.cs:407-416]
    - A canonical status badge via `StatusStyles.Badge(StatusStyles.ForStory(story), StatusStyles.StoryLabel(StatusStyles.ForStory(story)))`. Using `StoryLabel` gives the uniform canonical vocabulary ("Done" / "In review" / "In development" / "Ready for dev" / "Drafted") the AC's "canonical status" calls for; `Badge` already emits color + icon + word, so the badge is never color-only. [Source: src/SpecScribe/StatusStyles.cs:34-42,166-172]
  - [ ] Reuse existing CSS classes (`epic-mosaic-card`, `coverage-cards`, `coverage-card`, `epic-mosaic-donut`, `epic-mosaic-label`, `status-badge`) — they already exist in the stylesheet. [Source: src/SpecScribe/assets/*.css:1755,1969,2347] If a small amount of new CSS is genuinely needed for the grouped layout, route colors through the existing `--status-*` tokens only (never a raw hex) — status color is a single-source system.

- [ ] **Task 3 — Explicit, distinct empty states (AC: #2)**
  - [ ] When a group's epic has zero stories (`epic.Stories.Count == 0`), render an explicit per-group note ("No stories drafted in this epic yet.") rather than an empty group.
  - [ ] When `req.CoverageEpicNumbers` is empty (so `StoriesFor` yields nothing), state it explicitly and **distinguish deferred from unmapped now** — the data already supports it:
    - `req.Deferred == true` → keep/extend the existing "Deferred — not yet assigned…" note, and append its `CoverageNote` when present. [Source: src/SpecScribe/RequirementsTemplater.cs:124-131]
    - `req.Deferred == false` and no covering epics → distinct **unmapped** message (e.g. "Not yet mapped to any epic or story."). Do not reuse the deferred wording.
  - [ ] Leave a seam for Story 9.3: 9.3 will give deferred vs unmapped **distinct visual treatment** and link deferred items to their deferral source. Keep the two branches structurally separate and comment the seam; do not attempt 9.3's visual/linking work here. [Source: _bmad-output/planning-artifacts/epics.md:1260-1278]

- [ ] **Task 4 — Tests (AC: #1, #2)**
  - [ ] Add tests exercising `RenderRequirement` HTML output. The existing `RequirementsAndProgressTests` already builds `RequirementsModel` + `EpicsModel` from `MultiEpicEpicsMd` and asserts `StoriesFor` for FR2 (→ 1.1, 2.1), FR3 (deferred, empty), FR4 (unmapped, empty). Reuse that fixture. [Source: tests/SpecScribe.Tests/RequirementsAndProgressTests.cs:140-193]
  - [ ] Assert a covered requirement's page contains links to each covering story's page and its status badge, grouped under each covering epic (verify a **multi-epic** requirement like FR2 lists stories from *both* epics — the regression this story also fixes).
  - [ ] Assert the deferred requirement (FR3) renders the deferred empty-state text and the unmapped requirement (FR4) renders the distinct unmapped text — and that they differ.
  - [ ] Run the full suite; confirm no existing requirement/traceability test breaks on the signature change (`SiteGeneratorTraceabilityTests`, `RequirementsAndProgressTests`, `StylesheetTests`). `dotnet test` from repo root.

## Dev Notes

### What exists today (read before touching)

The requirement detail page (`RequirementsTemplater.RenderRequirement`, src/SpecScribe/RequirementsTemplater.cs:92-155) renders: a header with the requirement's rolled-up status badge, the requirement text, and a **Coverage** section that today shows only the *primary* covering epic as one `coverage-card` (via `AppendCoverageCard`). It never lists stories, and it silently omits secondary covering epics. This story replaces that single-card Coverage body with per-epic groups + story cards. Preserve the surrounding header, status badge, requirement text, and the trailing "← All requirements" link.

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Requirement → covering stories | `RequirementsParser.StoriesFor(req, epics)` | src/SpecScribe/RequirementsParser.cs:164 |
| All covering epic numbers (not just primary) | `req.CoverageEpicNumbers` | src/SpecScribe/RequirementsModel.cs:50 |
| Story lifecycle css-class | `StatusStyles.ForStory(story)` | src/SpecScribe/StatusStyles.cs:10 |
| Canonical status label | `StatusStyles.StoryLabel(cssClass)` | src/SpecScribe/StatusStyles.cs:34 |
| Color+icon+word badge | `StatusStyles.Badge(cssClass, label)` | src/SpecScribe/StatusStyles.cs:171 |
| Small per-story task donut | `Charts.MiniDonut(done, total)` | src/SpecScribe/Charts.cs:101 |
| Epic-group header w/ donut+tally | `AppendCoverageCard(...)` | src/SpecScribe/RequirementsTemplater.cs:160 |
| Story page URL (real or placeholder) | `story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id)` | src/SpecScribe/EpicsTemplater.cs:407-416 |
| Per-epic task progress | `progress.PerEpic.First(p => p.Number == epic.Number)` | src/SpecScribe/RequirementsTemplater.cs:134 |

### Guardrails & invariants

- **Deterministic output (NFR8 / CI reproducibility).** `StoriesFor` is already deterministic. Do not introduce dictionary-iteration-order dependence, timestamps, or per-visitor state into the new markup. A from-scratch regeneration must be byte-identical.
- **Status is a single-source system.** Every stage→color decision must route through `StatusStyles` and the six `--status-*` CSS tokens. Never hard-code a status color or re-map status words locally. [Memory: specscribe-status-token-system]
- **Never color-only (UX-DR17).** `StatusStyles.Badge` already pairs color + icon + word — use it; don't emit a bare colored chip.
- **Links use the page's relative prefix.** The detail page's `prefix = PathUtil.RelativePrefix("requirements/{slug}.html")` (i.e. `../`). Prepend it to every epic/story href, as `AppendCoverageCard` already does.
- **Never dead-end a story title.** Undrafted stories still have a generated placeholder page — always link.
- **Non-fatal generation.** Missing epics/progress must degrade gracefully (skip / empty ring), never throw — matches `StoriesFor`'s best-effort resolution.
- **Accessibility.** Give each new donut an `ariaLabel` (Donut ariaLabel convention from Story 1.4). Keep the section reachable in the existing `<main id="main-content">` landmark. [Memory: story-1-4-a11y-seams-for-1-5]

### Project Structure Notes

- All work is in `src/SpecScribe/RequirementsTemplater.cs` (primary), a one-line signature-call update in `src/SpecScribe/SiteGenerator.cs`, and tests in `tests/SpecScribe.Tests/`. No new files expected; no new source primitives.
- If (and only if) new CSS is unavoidable for the grouped card layout, add it to the existing stylesheet under `src/SpecScribe/assets/` using `--status-*` tokens; `StylesheetTests` guards the stylesheet.
- Output goes to `SpecScribeOutput/` by default when you generate to verify — **not** `docs/live` (vestigial/gitignored). [Memory: generate-output-dir-is-specscribeoutput]

### Testing standards

- xUnit (`tests/SpecScribe.Tests`), `Assert.Contains`/`Assert.DoesNotContain` on generated HTML strings — the established pattern in `RequirementsAndProgressTests` and `SiteGeneratorTraceabilityTests`. Run `dotnet test` from repo root.
- Reuse the `MultiEpicEpicsMd` fixture (FR2 = multi-epic covered, FR3 = deferred, FR4 = unmapped) rather than authoring a new one.

### Verify before marking review

Generate the portal against this repo's own `_bmad-output` and open a requirement detail page (e.g. `requirements/fr2.html` for the multi-epic case, plus one deferred and one unmapped FR) to confirm: covering stories appear grouped by epic, each links to its story page, each shows a canonical status badge, and the deferred vs unmapped empty states read distinctly.

### Dependency note (forward-looking, not a blocker)

Story 9.1's AC #2 references "when Story 9.3's states are available." Story 9.3 (deferred-on-purpose vs unmapped as distinct **visual** states, with deferral-source links) is a *later* story. 9.1 already has the raw `req.Deferred` distinction, so it distinguishes the two **in copy** now and leaves a clean structural seam for 9.3 to add the visual treatment and source links. Epic 8's "canonical status vocabulary" (FR20) is likewise not yet built; until it is, `StatusStyles` is the canonical status source this story uses. Neither is a blocker.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.1] (epics.md:1220-1238) — user story + ACs.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9] (epics.md:1214-1218) — epic intent; FR22 (epics.md:160).
- [Source: src/SpecScribe/RequirementsTemplater.cs] — page to modify (RenderRequirement, AppendCoverageCard).
- [Source: src/SpecScribe/RequirementsParser.cs:164] — `StoriesFor` (the FR→story resolver; do not duplicate).
- [Source: src/SpecScribe/RequirementsModel.cs:40-60] — `CoverageEpicNumber` vs `CoverageEpicNumbers` + honesty caveats.
- [Source: src/SpecScribe/EpicsModel.cs:7-32] — `StoryInfo` fields.
- [Source: src/SpecScribe/StatusStyles.cs:10-42,166-172] — canonical status class/label/badge.
- [Source: src/SpecScribe/SiteGenerator.cs:850-867] — the only caller, `WriteRequirements`.
- [Source: tests/SpecScribe.Tests/RequirementsAndProgressTests.cs:140-193] — reusable fixture + `StoriesFor` tests.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
