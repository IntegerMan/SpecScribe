---
baseline_commit: eaa2348370b18dd40cb0ab06afeef9701f9b03fc
---

# Story 21.2: Delivery Cadence & Story Cycle-Time

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer reflecting on throughput,
I want to see how work has flowed over time — completion cadence and, where derivable, story cycle-time,
so that delivery rhythm becomes a visible property of the project.

## Acceptance Criteria

1.
**Given** git history and story / sprint-status change data
**When** the cadence view renders
**Then** it shows completion-over-time and, where first-touch → done dates are derivable, a cycle-time distribution, each clearly labeled with its analysis window per Story 10.2.

2.
**Given** projects where transition history isn't reliably derivable (NFR8, honesty)
**When** cycle-time can't be trusted
**Then** that metric is omitted or explicitly marked approximate rather than fabricated
**And** the whole surface is generation-time deterministic (FR31) — no per-visitor "now" drift, identical output on a from-scratch CI regen.

## Context & Scope

Epic 21 gives first-time visitors and stakeholders a few high-impact displays that make the product's value legible at a glance. Story 21.1 built the traceability matrix (requirement × epic coverage); Story 21.2 is the second — **delivery cadence**: how story *completions* have flowed over time, and, where trustworthy, how long a story takes from first-touch to done.

**This is distinct from the existing `ChartMetric.ActivityCadence`** (`Charts.CommitHeatmap`, live on `git-insights.html` and `timeline.html`): that chart is raw **commit activity** (every commit, any purpose). This story is **story-completion cadence** — a different, sparser signal keyed on when *stories* (not commits) reached done. Do not conflate the two or reuse `ActivityCadence`'s enum case/copy for this story's chart.

21.1 is independent of this story (no hard dependency either direction) and, as of this writing, is **not yet implemented** (`ready-for-dev`, no code landed) — so there is no "previous story" code to build on; treat its story file as a sibling design precedent only (page-shell pattern, dashboard-strip pattern, honesty conventions), not as existing plumbing.

### Owner-selected design directions (locked at create-story — do not re-litigate)

Elicited up front per the standing project rule that a new visual surface gets its silhouette chosen by the owner, not the dev (memory `create-story-elicit-visual-intent`; precedent Stories 9.2, 7.12, 21.1):

1. **Placement — dedicated page + compact dashboard preview.** Build a dedicated **`cadence.html`** page (mirrors 21.1's `traceability.html` exactly: full-size cadence heatmap + cycle-time histogram, framed, with their own legends) plus a **compact preview strip on the home dashboard** linking to it ("View delivery cadence →"). No second full-size copy embedded elsewhere.
2. **"Completion over time" chart — calendar heatmap.** Reuse the visual language of `Charts.CommitHeatmap` (day-grid, week columns, month labels, `--status-*`-free heat-level ramp, real-value legend) but **keyed on story-completion days, not commit days**, and cells link to the completed story's page(s) for that day instead of a commit page. See "Data sources" below for exactly what feeds it and the code-reuse decision point.
3. **Cycle-time distribution — histogram (bucketed bars).** Bucket completed stories with a derivable cycle-time into day-range buckets (e.g. `0–3d`, `4–7d`, `8–14d`, `15–30d`, `30d+` — exact bucket edges are your call, but keep them human-readable and stated in the chart's caption/legend) and render as a ranked/ordered bar chart in the same visual family as `Charts.HotspotBars` (proportional-width bars, real counts, never color-only).

### Data sources — reuse before you build (read this before writing any new git-parsing code)

This is a rendering **and** light-computation story, not a brand-new git subsystem. Most of what "completion over time" needs already exists; "cycle-time" needs exactly one new, narrow piece.

- **The story roster, already resolved.** `EpicsModel.Epics[].Stories` (`StoryInfo`, [`EpicsModel.cs:7-52`]) is already the full list of stories with, per story: `Id`, `EpicNumber`, `ArtifactSourcePath` (e.g. `"implementation-artifacts/7-9-....md"`), `Status` (raw string), and **`LastUpdatedDate`** (`DateOnly?`). Iterate this roster — do **not** build a second filesystem walk of `implementation-artifacts/`.
- **"Is this story done" — single classifier, don't hand-roll.** `StatusStyles.ForStory(story)` (→ `StatusStyles.ForStatus(story.Status)`, [`StatusStyles.cs:26,46-50`]) already collapses `"done"`/`"complete"`/`"completed"` → `"done"`. A story counts toward completion-cadence / cycle-time iff `StatusStyles.ForStory(story) == "done"`. Never compare `story.Status` to a literal string yourself.
- **The "done" date — already computed, zero new git work.** `StoryInfo.LastUpdatedDate` ([`EpicsModel.cs:48-51`], set by `ProgressCalculator.ResolveLastUpdated`, [`ProgressCalculator.cs:83-94`]) is: the story artifact file's last git-touch date (from the deep-git per-file map) when `--deep-git` ran and matched the path; **else** the latest `## Change Log` ISO date in the artifact; **else** `null`. This is your "completed around this date" signal for a done story — reuse it directly as the cadence heatmap's per-day bucket key. It degrades honestly already (null when nothing is derivable) — a done story with a null `LastUpdatedDate` simply can't be placed on the cadence chart; exclude it rather than guessing.
- **`sprint-status.yaml` has NO history — don't try to diff it.** `SprintStatusParser` ([`SprintStatusParser.cs:6-13`]) parses the file as a **snapshot** (current status per key only). There is no per-story transition timeline recoverable from it. Use the per-story **artifact markdown file** (`StoryInfo.ArtifactSourcePath`) for anything date-related, not the yaml.
- **"First touch" (cycle-time start) — the one genuinely new piece.** Nothing in the codebase currently answers "when was this story file first created." Verified concretely against this repo: `git log --follow --diff-filter=A --format=%ad --date=short -- "_bmad-output/implementation-artifacts/7-9-....md"` returns the exact commit (`280c351`, 2026-07-19) that seeded that story `ready-for-dev` — a real, reliable first-touch signal. Add ONE small new git lookup (a new `GitMetrics` method, e.g. `TryGetFirstCommitDate(repoRoot, path)`, mirroring the existing single-purpose shell-outs `TryGetCurrentBranch`/`TryGetRemoteUrl`/`LastCommitTimestamp` at [`GitMetrics.cs:378,1061,1069`] — same `RunGit` helper, same `Timeout`, never-throws-null-on-failure contract) rather than extending the bounded numstat fetch.
  - **Do NOT** derive first-touch from `FileInsight.History` — that list is an explicitly **bounded/capped, newest-first** "change history" (Story 7.4, [`GitMetrics.cs:153-174`]). Its oldest entry is the oldest entry *within the cap*, not necessarily the file's true first commit; using it would silently under- or over-state cycle-time for any story with more touches than the cap.
  - Cycle-time (days) = done-date (`LastUpdatedDate`) − first-touch date, for stories where both resolve. This is bounded work (one story roster, dozens of entries, not thousands of commits) — a per-story git shell-out is acceptable here in a way it wouldn't be repo-wide.
- **`ChartMetric` enum — current real state (verify before assuming 21.1's coordination note still applies).** As of this session the enum ([`Charts.cs:13-26`]) has exactly `ActivityCadence`, `FileChurn`, `ChangeCoupling`, `RefactorRisk`, `CodeOwnership`. Story 21.1's own coordination note about a pending `ChartMetric.CodeFreshness` case (Story 7.12) is now **moot** — 7.12/7.11 shipped via `Charts.CodeMapSunburst` without ever adding a `ChartMetric` case (verified: no `CodeFreshness` symbol exists anywhere in `Charts.cs`). Add exactly one new case, `ChartMetric.DeliveryCadence`, with its own `WhyText` sentence.
- **"Today" / determinism.** There is no shared `ResolveToday()` helper yet (Story 5.5, which would add one, is still only `ready-for-dev` — not implemented). The current, accepted, codebase-wide pattern (see `CommitHeatmap`, [`Charts.cs:1139`]) is a single `DateOnly.FromDateTime(DateTime.Now)` call per generation run, used to bound the grid so it never renders past "today." Follow that same pattern (one `today` value computed once, threaded through, never re-queried mid-render) — this is what FR31 determinism means in this codebase today: byte-identical output for a fixed set of inputs/clock, not "never touch the clock."

### The truthfulness caveat you MUST honor (the project's cardinal rule)

- **Cadence dates are "artifact last-touched," not a tracked workflow-transition timestamp.** No event log of "this story flipped from ready-for-dev → done at time T" exists in this project. `LastUpdatedDate` is the best honest proxy (file's own git history / Change Log), so frame the chart accordingly ("story activity recorded around this date"), never "completed at exactly this timestamp."
- **Cycle-time measures story-FILE age, not tracked developer effort.** First-touch = when the file was created (often the moment a story was *seeded*, sometimes well before real dev work starts); done = last touch while status reads done. This repo's own history shows stories reopened and reworked across multiple sessions/days after first reaching a status (e.g. `7-10`/`7-11`/`7-12`'s sprint-status entries record repeated correct-course rework passes) — the measured "cycle time" can overstate or understate real effort. AC #2 explicitly permits (and, given this repo's real data, effectively requires) marking the cycle-time chart **approximate** in its caption/why-text rather than omitting it outright, since the numbers ARE derivable — just imprecise. Never present it as a precise SLA metric.
- **Single-source status, never a parallel classifier.** Route every "is this done" check through `StatusStyles.ForStory`, exactly as the rest of the portal does (Sprint page, dashboard Now & Next, epic/story badges).

## Tasks / Subtasks

- [x] **Task 1 — Delivery-cadence data builder (AC: #1, #2)**
  - [x] New static class (e.g. `src/SpecScribe/DeliveryCadence.cs`, mirroring the `ArtifactCoverage`/`WorkInventory` shape: static class + plain records, no DI) that, given `EpicsModel` + a repo root/git accessor:
    - Filters `epics.Epics.SelectMany(e => e.Stories)` to `StatusStyles.ForStory(s) == "done"`.
    - For each, resolves a completion day from `story.LastUpdatedDate` (skip — don't fabricate — when null).
    - Buckets completions by day into a `(DateOnly Day, int Count)` series (same shape `Charts.CommitHeatmap` already consumes) plus a day→story-list map (for cell links/tooltips, mirroring `commitsByDay`'s role).
    - For cycle-time: resolves first-touch via the new `GitMetrics.TryGetFirstCommitDate` (or equivalent) per story with a resolvable done-date; computes day-deltas; skips (never negative-clamps or fabricates) any story where first-touch fails to resolve or would produce a negative/zero-inflated result from a clearly bad match.
    - Returns something like `DeliveryCadenceData(IReadOnlyList<(DateOnly Day, int Count)> CompletionSeries, IReadOnlyDictionary<DateOnly, IReadOnlyList<StoryInfo>> CompletionsByDay, IReadOnlyList<(string StoryId, int Days)> CycleTimes)` — exact shape is your call, but keep it a plain, pure, git-repo-root-taking function (no `SiteGenerator` coupling) so it unit-tests like `GitMetrics.ParseNumstatLog` does (pure over pre-fetched/injectable data where feasible).
    - Never throws (AD-4/NFR2) — any resolution failure for a single story just excludes that story; total absence of data is a normal, expected empty-result case, not an exception.
  - [x] Add `GitMetrics.TryGetFirstCommitDate(string repoRoot, string repoRelativePath)` (or equivalent) per the "Data sources" section above — a bounded, per-file, never-throwing git shell-out using the existing `RunGit`/`Timeout` plumbing.

- [x] **Task 2 — Chart builders + Story 10.2 framing (AC: #1)**
  - [x] Add `ChartMetric.DeliveryCadence` to the enum ([`Charts.cs:13-26`]) + its `WhyText` case: a metric-generic (NFR8), framework-neutral sentence distinct from `ActivityCadence`'s copy — e.g. *"How often stories reach done reveals the project's real delivery rhythm — steady drips and bursts both tell you something commit activity alone doesn't."*
  - [x] **Cadence heatmap.** Either (a) extract a shared private grid-drawing engine out of `Charts.CommitHeatmap` that both it and a new `Charts.DeliveryCadenceHeatmap(...)` call (parameterized by per-day counts + a per-day href/tooltip resolver), or (b) write `Charts.DeliveryCadenceHeatmap` as its own sibling builder modeled closely on `CommitHeatmap`'s structure (grid sizing, week/month labels, `HeatLevel`/`HeatLevelRange` reuse, real-value legend, "never color-only" `<title>`s). **If you extract a shared engine, `CommitHeatmap`'s own existing output and its own tests must stay byte-identical** — that chart is live on `git-insights.html`/`timeline.html` today and any regression there is out of this story's scope. When in doubt, prefer (b) — a sibling builder — over a risky refactor of shipped code. Cells link to the completed story's page when exactly one story completed that day; multiple same-day completions get a rich tooltip listing all of them (mirror the `js-tip`/`data-tip` pattern, never CSS `::after` inside an overflow container — memory `tooltip-clipping-use-ss-tooltip-node`).
  - [x] **Cycle-time histogram.** New `Charts.CycleTimeHistogram(IReadOnlyList<(string StoryId, int Days)> cycleTimes, Func<string, string?>? storyHref = null)` modeled on `Charts.HotspotBars` ([`Charts.cs:1666-1684`]): bucket into human-readable day ranges, one bar per bucket, proportional width by count, real count label (never color/width-only). Degrades to a `chart-empty`-style note when no story has a derivable cycle-time (AC #2) — this is an expected, common case for young/small projects, not an error state.
  - [x] Render both through `Charts.Framed` with `ChartMeta(Title, Window, Ranking, Why: WhyText(ChartMetric.DeliveryCadence))` — separate `Framed` calls for the two charts (they have different windows: the heatmap's window is a date span like `CommitHeatmap`'s; the histogram's "window" is the set of completed stories it covers, e.g. `"{n} completed stories with a derivable cycle-time"`). The cycle-time frame's `Why`/caption text is where the "approximate — measured story-file age, not a tracked workflow timestamp" honesty caveat belongs (Note slot or folded into Why — your call, but it must be visible, not buried).

- [x] **Task 3 — Dedicated `cadence.html` page (AC: #1)**
  - [x] Add `SiteNav.CadenceOutputPath = "cadence.html"` (doc-commented like its siblings, [`SiteNav.cs:8-50`]) and a new `CadenceTemplater.RenderPage(EpicsModel, DeliveryCadenceData?, SiteNav, Func<string,string?>? storyHref)` mirroring `RiskQuadrantTemplater`'s shell exactly (head open → nav bar → breadcrumb → `<main id="main-content">` → two framed charts → footer) — it is the freshest "new standalone insights page" precedent in this codebase. [Source: `src/SpecScribe/RiskQuadrantTemplater.cs`]
  - [x] Wire a `WriteCadence(nav, epics)` in `SiteGenerator`, called near `WriteSprint`/`WriteRiskQuadrant` (after `_epicsModel`/`_progress` are populated — `StoryInfo.LastUpdatedDate` is filled in by `ProgressCalculator`, which must have already run). `WriteOutput(SiteNav.CadenceOutputPath, ApplyReferenceLinks(html, SiteNav.CadenceOutputPath))` — auto-captured into `_spaCapture` for SPA/webview parity for free; add a coherence test asserting it's present (mirror Story 7.9's `code-map.html` coherence check). [Source: `SiteGenerator.cs:2702-2710` `WriteOutput`; `SiteGenerator.cs:3120-3126` `WriteRiskQuadrant` call-site precedent]
  - [x] **Nav registration — Delivery group, `hasEpics` gate (no new flag).** `StoryInfo`/`LastUpdatedDate` come from `EpicsModel`, so gate on the SAME `hasEpics` signal that already gates Epics/Requirements ([`SiteNav.cs:200-208`]) — mirror Story 21.1's shared-gate reasoning exactly. Do **not** add a `hasCadence`/`hasSprint`-style second flag; the page's own internal AC #2 degrade (honest empty-state) handles the case where `hasEpics` is true but zero stories are done or no dates resolve.
  - [x] Confirm `BuildInsightsLocalContext`/`BuildDeliveryLocalContext`-equivalent local-context sub-header still resolves for the new page if the Delivery group participates in one (mirror whichever of Sprint/Requirements does). [Source: `SiteNav.cs:411` `BuildNavLocalContextFor…`]

- [x] **Task 4 — Compact preview strip on the dashboard (AC: #1)**
  - [x] Add a small dashboard panel/strip (e.g. via `DashboardViewBuilder` producing a pre-rendered HTML fragment field on `DashboardView`, mirroring the existing `NextStepsHtml` fragment precedent, [`DashboardViewBuilder.cs:79-82`]) summarizing recent cadence — e.g. completions in the last N weeks + a link "View delivery cadence →" to `cadence.html`. Route through the section-view-model builder path so HTML/webview/SPA stay parity-identical (memory `story-6-2-section-view-models-live`) — do not hand-append raw HTML in one adapter only.
  - [x] Gated on the same signal as the page (at least one done story with a resolvable date); absent, not an empty panel, when there's nothing to show (NFR8).

- [x] **Task 5 — Tests (AC: #1, #2)**
  - [x] `DeliveryCadence`-builder tests (or wherever Task 1's class lands): correctly filters to `StatusStyles.ForStory == "done"`; excludes stories with a null `LastUpdatedDate`; excludes cycle-time entries where first-touch fails to resolve; never throws on a repo/story with zero done stories (returns an honest empty result).
  - [x] `GitMetrics.TryGetFirstCommitDate` (or equivalent): returns the correct earliest date for a file with multiple commits (assert against a fixture repo, not this real repo, so the test is hermetic); returns null (never throws) for a nonexistent path or non-repo directory.
  - [x] `ChartsTests`: `DeliveryCadenceHeatmap` — empty series → `chart-empty`; a day with one completion links to that story; a day with multiple completions gets a rich tooltip listing all; legend/window match the rendered grid. `CycleTimeHistogram` — empty → honest note; buckets sum to the total input count; never color-only (state/count text beside every bar). `WhyText(ChartMetric.DeliveryCadence)` returns a non-empty, framework-neutral, `ActivityCadence`-distinct sentence.
  - [x] Page + wiring: a `SiteGenerator*Tests` fixture with epics + done stories (with resolvable dates) writes `cadence.html`, appears in the Delivery nav, deep-links resolve; a fixture with **no** epics writes **no** `cadence.html` and shows no nav entry (shared `hasEpics` gate); a fixture with epics but zero done stories still writes the page with its honest empty-state (not omitted — matches the `hasEpics`-only gate decision above). SPA/webview coherence: `cadence.html` present in `_spaCapture`.
  - [x] Golden + parity: regenerate `GoldenContentFingerprint`/inventory (new page + dashboard strip change committed pages); eyeball the diff is scoped to this story's surfaces; confirm `CommitHeatmap`'s own existing tests are UNCHANGED and still green if you extracted a shared engine (Task 2 note). Keep `RenderParity`/SPA/webview suites green. [memory `golden-diff-normalization-gotchas`]

- [x] **Task 6 — Verify end-to-end on this repo (AC: #1, #2)**
  - [x] `dotnet run` a full `--deep-git` generate to `SpecScribeOutput/` (never `--output docs/live` — memory `generate-output-dir-is-specscribeoutput`). Open `cadence.html`: the completion heatmap shows real done-story dates from this repo's own sprint-status/story-file history, cells link to the right story pages, the cycle-time histogram shows real buckets (or its honest empty/approximate state) with its "approximate" framing visible. Open `index.html`: the compact strip shows and links through. Confirm the same page renders correctly in `specscribe webview` and `--spa`.
  - [x] Spot-check at least 2-3 real completed stories' derived cycle-time against this repo's own git log by hand (the way this story's Dev Notes validated `7-9`'s 2026-07-19 → 2026-07-21 span) to confirm the first-touch/done-date resolution is actually correct, not just non-throwing.

## Dev Notes

### What exists today (read before touching)

- **`EpicsModel`/`StoryInfo`** ([`EpicsModel.cs:7-52`]) — the resolved story roster: `Status`, `ArtifactSourcePath`, `LastUpdatedDate` all already populated by `ProgressCalculator` by the time `SiteGenerator` reaches the point where `WriteSprint`/`WriteCodeMap`/etc. run. This is your primary input — do not re-parse `implementation-artifacts/*.md` yourself.
- **`ProgressCalculator.ResolveLastUpdated`** ([`ProgressCalculator.cs:83-94`]) — the exact git-date-else-changelog-else-null resolution logic for `LastUpdatedDate`, added by Story 8.8. Read this before assuming what populates the date you're bucketing on.
- **`StatusStyles.ForStory`/`ForStatus`** ([`StatusStyles.cs:26,46-50`]) — the single "done" classifier.
- **`Charts.CommitHeatmap`** ([`Charts.cs:1129-1320`]) — the calendar-heatmap engine and visual/a11y conventions (grid sizing, month/day labels, `HeatLevel`/`HeatLevelRange`, real-value legend, first-commit marker, young-repo trim, whole-chart `aria-label`, per-cell `<title>`, linked-vs-unlinked cell a11y handling) to mirror for `DeliveryCadenceHeatmap`. Also the current codebase's accepted "today" pattern (`DateOnly.FromDateTime(DateTime.Now)`, called once).
- **`Charts.HotspotBars`** ([`Charts.cs:1666-1684`]) — the proportional-bar-list pattern to mirror for `CycleTimeHistogram`.
- **`Charts.Framed`/`ChartMeta`/`WhyText`/`ChartMetric`** ([`Charts.cs:13-100`]) — the mandatory Story 10.2 frame; both new charts render through it.
- **`GitMetrics`** ([`GitMetrics.cs`]) — `RunGit`/`Timeout` shell-out plumbing (`TryGetCurrentBranch`, `TryGetRemoteUrl`, `LastCommitTimestamp` at lines 1069, 1061, 378) to mirror for the new `TryGetFirstCommitDate`; `FileInsight.History`'s bounded/capped nature (lines 153-174) — the trap to avoid.
- **`SprintStatusParser`** ([`SprintStatusParser.cs:6-13`]) — confirms `sprint-status.yaml` is snapshot-only, no history.
- **`RiskQuadrantTemplater`/`SiteGenerator.WriteRiskQuadrant`** ([`RiskQuadrantTemplater.cs`; `SiteGenerator.cs:3120-3126`]) — the freshest "new synthesized insights page" precedent to mirror for `CadenceTemplater`/`WriteCadence`.
- **`DashboardViewBuilder`'s `NextStepsHtml`** ([`DashboardViewBuilder.cs:79-82`]) — precedent for a pre-rendered HTML-fragment dashboard field when a full typed sub-view-model would be overkill for a compact strip.

### Guardrails & invariants (must follow)

- **Single-source status.** Every "done" check routes through `StatusStyles.ForStory`. Never compare `story.Status` to a raw string yourself.
- **Reuse `LastUpdatedDate`, don't re-derive it.** It already encodes the git-date-else-Change-Log-else-null fallback chain (Story 8.8) — do not write a second, competing "when was this done" resolver.
- **Bounded new git work only.** The one new per-file git call (`TryGetFirstCommitDate`) runs at most once per done story with a resolvable done-date — bounded by story count (dozens), not commits or files repo-wide. Never call it for every story regardless of status, and never call it inside a hot loop over commits.
- **Never fabricate a date or a cycle-time.** Missing/unresolvable → exclude that one story from the relevant chart, don't zero-fill, don't clamp negative deltas into 0, don't guess.
- **Cycle-time is approximate — say so.** Its frame's caption/why-text must carry the "story-file age, not a tracked workflow timestamp" caveat (see "Truthfulness" above) every time it renders real data.
- **Never color-only (UX-DR17).** Every heatmap cell and histogram bar pairs its state/count with text (`<title>`, visible label, or both).
- **Pure SVG/HTML + links, no info-bearing JS** (memory `charting-is-pure-svg-no-js`). Rich same-day-multi-completion tooltips route through the shared `js-tip`/`data-tip` body-level node, never CSS `::after` inside an overflow container (memory `tooltip-clipping-use-ss-tooltip-node`).
- **Deterministic (NFR8/FR31/CI reproducibility).** One `today` value per generation run; no dictionary-iteration-order dependence; invariant date/number formatting (`Charts.D`/`DReadable`, invariant culture). A from-scratch regen on unchanged inputs is byte-identical.
- **Degrade, don't break (NFR8).** Zero done stories, zero resolvable dates, zero resolvable cycle-times — each is a normal, expected, honestly-labeled empty state, never an exception and never a misleading chart.
- **Section-view-model discipline (Story 6.2).** The dashboard strip goes through `DashboardViewBuilder` → adapter so HTML/webview/SPA stay parity-identical.
- **Don't touch `Charts.CommitHeatmap`'s shipped output.** If you extract a shared grid engine, its existing tests and golden output for `git-insights.html`/`timeline.html` must be byte-identical afterward. If that's remotely risky, write `DeliveryCadenceHeatmap` as an independent sibling instead — duplication is cheaper than regressing a live chart.
- **Accessibility.** Everything inside `<main id="main-content">`; semantic structure + text equivalents for both charts (a screen-reader user must be able to learn completion dates and cycle-time buckets without the SVG); keyboard-reachable links, focus ring (Story 1.4).

### Project Structure Notes

- Primary code: new `src/SpecScribe/DeliveryCadence.cs` (or similar name) for the data builder, `src/SpecScribe/GitMetrics.cs` (+ `TryGetFirstCommitDate`), `src/SpecScribe/Charts.cs` (new `DeliveryCadenceHeatmap` + `CycleTimeHistogram` + `ChartMetric.DeliveryCadence` + `WhyText` case), new `src/SpecScribe/CadenceTemplater.cs`, `src/SpecScribe/SiteNav.cs` (new output-path const + Delivery nav entry), `src/SpecScribe/SiteGenerator.cs` (`WriteCadence` + call site), `src/SpecScribe/DashboardViewBuilder.cs` (+ dashboard strip field), `src/SpecScribe/assets/specscribe.css` (any new chart-specific classes — reuse `heatmap-*`/`git-pulse-bar-*` classes where the visuals genuinely match rather than inventing near-duplicates). Tests in `tests/SpecScribe.Tests/`.
- Output dir is `SpecScribeOutput/` by default when you generate to verify — never `docs/live` (memory `generate-output-dir-is-specscribeoutput`).
- **Watch the CSS-comment `*/` truncation gotcha** if you add any comment mentioning `--status-*`/similar tokens without a space before `*/` (memory `css-comment-star-slash-silent-truncation`).

### Testing standards

- xUnit (`tests/SpecScribe.Tests`), `Assert.Contains`/`Assert.DoesNotContain` over generated HTML strings — the established `ChartsTests`/`GitMetricsTests` pattern. New pure builders (`DeliveryCadence`, `Charts.DeliveryCadenceHeatmap`, `Charts.CycleTimeHistogram`) are unit-testable directly over constructed fixtures — no real repo or disk needed except for `TryGetFirstCommitDate`, which should get its own small hermetic fixture-repo test (init a temp git repo, commit twice, assert the first date) rather than asserting against this real repo's live history (which will drift).
- Regenerate `GoldenContentFingerprint` deliberately (new page + dashboard strip) and confirm the diff is only this story's surfaces; confirm the baseline is green before starting. [memory `golden-diff-normalization-gotchas`]

### Out of scope (do not build)

- **No new story-status data model or workflow-transition event log.** This story reads existing `StoryInfo`/`LastUpdatedDate`/git history — it does not add tracking of individual status transitions.
- **No traceability matrix** (Story 21.1) and **no planning↔code impact map** (Story 21.3) — sibling stories, separate surfaces.
- **No changes to `Charts.CommitHeatmap`'s own rendered output**, the six-tier requirement/story status vocabulary, or `ProjectCounts`.
- **No client-side interactivity** (zoom/drill) — static generation-time HTML/CSS only, per this project's default (memory `charting-is-pure-svg-no-js`).
- **No PRD edit** (FR39 PRD sync deferred "when convenient" per SCP 2026-07-19).
- **No repo-wide git-history re-fetch.** The new first-commit lookup is scoped per-story, not a second full `--deep-git` pass.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:3145-3162`] — Epic 21 intent + Story 21.2 user story & ACs; FR39; NFR8; FR31.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-19.md`] — Epic 21 seating.
- [Source: `_bmad-output/implementation-artifacts/21-1-traceability-coverage-matrix.md`] — sibling story: page-shell pattern, dashboard-strip pattern, honesty/degrade conventions (not-yet-implemented at time of writing — design precedent only).
- [Source: `_bmad-output/implementation-artifacts/10-2-chart-metadata-standard.md`] — `Charts.Framed`/`ChartMeta`/`WhyText`/`ChartMetric` frame both charts must use.
- [Source: `_bmad-output/implementation-artifacts/8-3-single-source-of-truth-for-every-count.md`] — single-source-count discipline (this story doesn't touch `ProjectCounts`, but the discipline still applies: no competing "done" tally).
- [Source: `src/SpecScribe/EpicsModel.cs:7-52`] — `StoryInfo` (`Status`, `ArtifactSourcePath`, `LastUpdatedDate`).
- [Source: `src/SpecScribe/EpicsParser.cs:23,84-88`] — the `Status:` line extraction already in place.
- [Source: `src/SpecScribe/ProgressCalculator.cs:83-94`] — `ResolveLastUpdated` (Story 8.8) fallback chain.
- [Source: `src/SpecScribe/StatusStyles.cs:26,46-50`] — `ForStory`/`ForStatus` single classifier.
- [Source: `src/SpecScribe/SprintStatusParser.cs:6-13`] — confirms sprint-status.yaml is a snapshot, no history.
- [Source: `src/SpecScribe/Charts.cs:13-100,1129-1320,1666-1684`] — `ChartMetric`/`Framed`; `CommitHeatmap` (heatmap engine to mirror); `HotspotBars` (bar-list pattern to mirror).
- [Source: `src/SpecScribe/GitMetrics.cs:153-174,378,1061,1069`] — `FileInsight.History`'s bounded-list trap; single-purpose shell-out precedents for the new `TryGetFirstCommitDate`.
- [Source: `src/SpecScribe/SiteNav.cs:8-50,200-208,411`] — output-path const pattern; the `hasEpics` Delivery gate; local-context resolution.
- [Source: `src/SpecScribe/SiteGenerator.cs:350-372,2702-2710,3120-3126`] — `GenerateAll` phase ordering (`WriteSprint`/`WriteCodeMap`/`WriteRiskQuadrant` call sites — `WriteCadence` belongs in this same neighborhood, after `_epicsModel`/progress are populated); `WriteOutput`/`_spaCapture` auto-capture; `WriteRiskQuadrant` new-page precedent.
- [Source: `src/SpecScribe/RiskQuadrantTemplater.cs`] — the page-shell template to mirror.
- [Source: `src/SpecScribe/DashboardViewBuilder.cs:39-84`] — dashboard section-view-model assembly; `NextStepsHtml` fragment precedent.
- Verified live against this repo: `git log --follow --diff-filter=A --format="%h %ad %s" --date=short -- "_bmad-output/implementation-artifacts/7-9-code-map-file-type-colorize-discrete-palette.md"` → first-touch `280c351` (2026-07-19); `git log -S"Status: done" --format="%h %ad %s" --date=short -- <same file>` → done `4e47648` (2026-07-21) — confirms the first-touch/done-date resolution approach is sound on real data.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Code / bmad-dev-story)

### Debug Log References

- Full `dotnet run -- generate --deep-git` on this repo: 644 pages, 0 errors. `cadence.html` renders 78 story completions across 16 active days (Jul 5–22 2026), a cycle-time histogram of 51/22/5/0/0 across the five buckets (78 total), and the visible "Approximate…" caveat.
- Spot-checked first-touch/done resolution against real git history: 7.9 = 2026-07-19 → 2026-07-21 (2d, 0–3d bucket); 10.2 = 2026-07-12 → 2026-07-20 (8d, 8–14d); 19.1 = 2026-07-18 → 2026-07-22 (4d, 4–7d) — all correct.
- Golden content fingerprint regenerated (`abbffbeff3ff96c1c694579a64c14eb107e963eb1a78aa71e67a7c19ef676a30`), verified stable across two repeated runs; golden output inventory gained `cadence.html`. Full suite: 2081 passed / 3 pre-existing skips.

### Completion Notes List

- **Task 1** — `DeliveryCadence.Build` (new `DeliveryCadence.cs`): pure over `EpicsModel` + an injectable first-touch resolver. Done-classification routes only through `StatusStyles.ForStory`; completion days reuse the already-computed `StoryInfo.LastUpdatedDate` (zero new git work for the cadence half); a null date excludes the story (never fabricated). `GitMetrics.TryGetFirstCommitDate` added (bounded `git log --follow --diff-filter=A`, never-throws, mirrors `TryGetCurrentBranch`/`TryGetRemoteUrl`); cycle-time = done − first-touch, negative spans skipped (never clamped), zero-day spans kept.
- **Task 2** — `ChartMetric.DeliveryCadence` + its distinct `WhyText`; `Charts.DeliveryCadenceHeatmap` written as an INDEPENDENT sibling of `CommitHeatmap` (reusing the private `HeatLevel`/`HeatLevelRange`/`IsHeatLevelUnreachable` helpers) so the shipped commit heatmap stays byte-identical. Single-completion days link to the story; multi-completion days carry a body-level `js-tip` rich tooltip; a text-equivalent completion log below the SVG is the a11y/no-JS twin. `Charts.CycleTimeHistogram` mirrors `HotspotBars` (proportional bars, real counts, never color-only); empty → honest note. `Charts.DeliveryCadenceStrip` for the dashboard reuses the Git Pulse signal-strip classes.
- **Task 3** — new `CadenceTemplater` mirrors `TraceabilityTemplater`'s shell; both charts framed via `Charts.Framed`, the cycle-time frame carrying the "approximate — story-file age, not a tracked workflow timestamp" caveat in its Note slot. `SiteNav.CadenceOutputPath` + a "Cadence" Delivery nav entry on the shared `hasEpics` gate (no new flag); `SiteGenerator.WriteCadence` (auto-captured into `_spaCapture` for SPA/webview parity); `Icons.ForConcept("Cadence")` glyph.
- **Task 4** — `DashboardView.CadenceStripHtml` fragment built by `DashboardViewBuilder` and rendered by the one `HtmlRenderAdapter`, so HTML/webview/SPA stay parity-identical; `cadence` threaded through `HtmlTemplater.RenderIndex`/`BuildIndexPage` and all three dashboard call-sites. Omitted (no empty panel) when there's nothing to show.
- **Task 5** — new tests: `DeliveryCadenceTests` (builder honesty rules), `GitMetricsFirstCommitDateTests` (hermetic temp-repo, earliest-date + null degradation), `ChartsTests` additions (heatmap link/tooltip/log, histogram bucket-sum + never-color-only, why-text distinctness, strip), `SiteGeneratorCadenceTests` (page + nav gate + dashboard strip + no-broken-links + honest empty-state + SPA coherence). Fixtures updated: `SiteNavTests`, `RenderParityTests`, golden inventory + fingerprint.
- **Task 6** — end-to-end verified on this repo (see Debug Log); dedicated page, dashboard strip, deep-links, cycle-time buckets, and the approximate framing all confirmed against real data.

### File List

**Added**
- `src/SpecScribe/DeliveryCadence.cs`
- `src/SpecScribe/CadenceTemplater.cs`
- `tests/SpecScribe.Tests/DeliveryCadenceTests.cs`
- `tests/SpecScribe.Tests/GitMetricsFirstCommitDateTests.cs`
- `tests/SpecScribe.Tests/SiteGeneratorCadenceTests.cs`

**Modified**
- `src/SpecScribe/GitMetrics.cs` (`TryGetFirstCommitDate`)
- `src/SpecScribe/Charts.cs` (`ChartMetric.DeliveryCadence` + `WhyText`; `DeliveryCadenceHeatmap` + `AppendCadenceLog`; `CycleTimeHistogram` + buckets; `DeliveryCadenceStrip`)
- `src/SpecScribe/SiteNav.cs` (`CadenceOutputPath` + Delivery nav entry/quick-link)
- `src/SpecScribe/SiteGenerator.cs` (`_cadence` field + build + `WriteCadence` + threading to WriteIndex/webview/SPA dashboards)
- `src/SpecScribe/DashboardView.cs` (`CadenceStripHtml`)
- `src/SpecScribe/DashboardViewBuilder.cs` (`cadence` param + strip build)
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` (cadence panel render)
- `src/SpecScribe/HtmlTemplater.cs` (thread `cadence` through `RenderIndex`/`BuildIndexPage`)
- `src/SpecScribe/Icons.cs` (`Cadence` glyph)
- `src/SpecScribe/assets/specscribe.css` (cadence strip/log/histogram rules)
- `tests/SpecScribe.Tests/ChartsTests.cs`, `SiteNavTests.cs`, `RenderParityTests.cs`, `SiteGeneratorAdapterTests.cs` (fixtures + new chart tests)

## Change Log

- 2026-07-22: Implemented Story 21.2 (Delivery Cadence & Story Cycle-Time) — dedicated `cadence.html` (story-completion heatmap + cycle-time histogram) + compact dashboard strip; new `DeliveryCadence` builder + `GitMetrics.TryGetFirstCommitDate`; `ChartMetric.DeliveryCadence`. 2081 tests green; golden fingerprint regenerated. Status → review.
- 2026-07-22: Owner review-feedback polish (in review) — (1) completion log collapsed into a `<details>` so the tile grid is the primary view; (2) removed the second `js-tip`/`data-tip` tooltip on multi-completion cells so each cell shows ONE native `<title>`; (3) legend now carries a "Stories completed / day" unit label so the shade ramp isn't misread as commits/day; (4) recolored the cycle-time bars off rust to teal (a short cycle-time is good, not an alert); (5) added an orientation lede under the H1 explaining what's measured + how to read it, with external links (lead/cycle time, Little's Law). Full suite green (2104); golden fingerprint regenerated + stable ×2.
