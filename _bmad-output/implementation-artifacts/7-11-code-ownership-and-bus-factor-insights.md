---
baseline_commit: 50c5185d6f5858bee285de9eb2a0690fc421a6c1
---

# Story 7.11: Code Ownership & Bus-Factor Insights

Status: review

<!-- RE-SCOPED 2026-07-21 via correct-course (see sprint-change-proposal-2026-07-21-git-insights-ownership-view.md).
     The story was previously implemented as a plain ranked HTML table (see Change Log) and shipped to "review".
     The owner found that display underwhelming and redirected it toward a graphical, interactive sunburst. This
     rewrite REPLACES the AC/Tasks/Dev Notes below wholesale — do not treat the old table-based sections as
     "done, extend them"; the first task is to REMOVE them. ADR 0010 (client-side charting JS for opt-in
     deep-analytics surfaces) was ratified alongside this rescope and governs the JS approach this story now
     requires — read it before starting. -->

## Story

As a maintainer assessing project resilience,
I want to see how concentrated authorship is across the codebase as an interactive, graphical map — not a list —
so that knowledge silos, orphaned areas nobody active understands, and who is active where all become visible at a
glance, before they become a risk.

## Acceptance Criteria

1.
**Given** deep-git author attribution across every source file (not just a top-N subset)
**When** the Git Insights hub renders
**Then** it shows ONE interactive sunburst over the full source-code tree (same hierarchy/geometry family as the
Story 7.12 `Charts.CodeFreshnessSunburst` engine — reused or generalized, not reinvented) in place of BOTH the
prior "Files & Contributors" master-detail table and the prior "Ownership & Bus-Factor" ranked table, which this
story removes
**And** file-leaf wedges are sized/positioned by the same tree the Code Map uses, so directory structure reads
identically across pages.

2.
**Given** the sunburst is rendered
**When** the reader chooses a coloring mode
**Then** they can switch between at least: (a) dominant-author commit share % on a sequential ramp, (b) top-N
authors on a discrete/categorical palette with a bounded "Others" bucket, (c) an individual-author spotlight that
highlights every file a chosen contributor has touched (any contributor, not just a capped top-N — per ADR 0010,
this needs live client-side JS, not a pre-rendered fixed set), and (d) a staleness view recoloring by "months
since any current contributor last committed to this file," with a configurable threshold (not just 3 fixed
options)
**And** switching modes never triggers a page navigation or reload (client-side recolor of the already-rendered
sunburst).

3.
**Given** ADR 0010's ratified constraint that these opt-in analytics surfaces need a real no-JS baseline, not a
blanket exemption
**When** JavaScript is disabled
**Then** the sunburst still renders, pre-colored in the default mode (dominant-author share %), fully legible and
non-broken
**And** every file's dominant author, share %, contributor count, and last-active date remain available as an
accessible text-equivalent table alongside the chart (the same "chart pairs with a text list" convention every
other chart on this page already follows) — disabling JS must not remove information, only the live mode
controls (NFR-5).

4.
**Given** a solo-maintainer repo (the common OSS case, NFR8)
**When** ownership would trivially be "one person everywhere" in every mode
**Then** the surface reframes honestly (e.g., a "single-maintainer project" statement) rather than rendering a
sunburst where every wedge is flagged as at-risk in the same color
**And** the classification underlying every mode is generation-time deterministic (FR31) — no wall-clock "now" or
per-visitor state; whatever "current" means for the staleness mode is computed once at generation time from git
timestamps, embedded, and never re-derived client-side from live repository state.

5.
**Given** entries in any mode
**When** a reader wants to inspect a specific file
**Then** wedges link to their code page via the existing Story 7.2 seam (guarded — no resolver/no target renders
plain, never a dead link), exactly as the prior table-based rows did.

6.
**Given** FR-10's explicit "no per-author productivity ranking or leaderboard" constraint
**When** any mode renders (including the individual-author spotlight)
**Then** author information stays descriptive attribution — share of commits on a file, last-active date, "how
much of this area they've touched" — never a cross-repo ranked list of people by output; the spotlight highlights
where ONE chosen person's work lives, it does not rank people against each other.

7.
**Given** a shallow or non-git repository (NFR8)
**When** `--deep-git` is off or produced no usable history
**Then** the whole Git Insights hub (and this section with it) is omitted exactly as today — no partial render, no
new gating decision needed beyond the existing page-level gate.

## Tasks / Subtasks

- [x] Task 0 — Remove the superseded implementation (AC: #1)
  - [x] Subtask 0.1: Remove `GitInsightsTemplater.AppendFilesAndContributorsSection` (the master-detail file→contributor table) and `AppendOwnershipSection` (the ranked ownership table shipped by the prior pass of this story) and their call sites in `RenderPage`.
  - [x] Subtask 0.2: Remove `Charts.ChartMetric.AuthorConcentration` and its `WhyText` case (superseded by whatever new `ChartMetric` case the sunburst needs — decide the replacement name as part of Task 2, don't leave both).
  - [x] Subtask 0.3: Remove the now-dead `.gi-risk-badge`/`.gi-solo-repo-note`/`.gi-master-detail`/`.gi-table`/`.gi-contributors-panel` CSS and their `StylesheetTests` guards ONLY if nothing else on the page still uses them — check first (some `.gi-*` classes may be shared scaffolding worth keeping for the new section's own table/legend markup).
  - [x] Subtask 0.4: Remove or rewrite the test fixtures in `GitInsightsTemplaterTests.cs`/`SiteGeneratorGitInsightsTests.cs` that assert on the removed sections' markup (e.g. `RenderPage_WholeRowSelectsThePerFileContributorPanel`, the Story 7.11 ownership-table tests) — replace with tests against the new sunburst section (Task 6).
  - [x] Subtask 0.5: Confirm via `git log`/`git diff` against `baseline_commit` in this file's frontmatter exactly what the prior pass touched (Charts.cs, GitInsightsTemplater.cs, specscribe.css, GitInsightsTemplaterTests.cs, SiteGeneratorGitInsightsTests.cs, StylesheetTests.cs, SiteGeneratorAdapterTests.cs) before starting, so nothing from that diff is missed.

- [x] Task 1 — Full-tree per-file author data (AC: #1, #2)
  - [x] Subtask 1.1: `GitInsightsData.Files`/`FileChangeStat` is TOP-N-CAPPED (`topFiles` parameter of `GitMetrics.BuildInsights`, default 50) — insufficient for a whole-tree sunburst, which needs EVERY file. Extend `GitMetrics.BuildCodeMapMetrics` (the uncapped per-file accumulator already feeding `CodeMapNode.Metrics` for every file, [GitMetrics.cs:918](src/SpecScribe/GitMetrics.cs:918)) to also tally per-author commit counts and each author's last-commit date for that file, mirroring the accumulator shape `GitMetrics.BuildInsights`'s private `FileAccum`/`Authors` dictionary already uses ([GitMetrics.cs:762](src/SpecScribe/GitMetrics.cs:762)) — do not duplicate the numstat parse, extend the existing single accumulation pass.
  - [x] Subtask 1.2: Decide the join shape: either add optional fields to `CodeFileMetrics` (`Changes, TotalChurn, FirstDate, LastDate, AvgCoChanged` today, [GitMetrics.cs:98](src/SpecScribe/GitMetrics.cs:98)) — e.g. `IReadOnlyList<FileContributor>? Contributors, int TotalContributors` — so `CodeMapNode.Metrics` carries author data by construction with no new lookup dictionary, OR thread a second `IReadOnlyDictionary<string, ...>` author lookup alongside `gitMetrics` into `CodeMap.Build` ([CodeMap.cs:226](src/SpecScribe/CodeMap.cs:226)). Prefer extending `CodeFileMetrics` (one record, one join, matches how `Category` was added in Story 7.9) unless that record is already unreasonably wide.
  - [x] Subtask 1.3: Reuse `FileContributor` ([GitMetrics.cs:120](src/SpecScribe/GitMetrics.cs:120)) for the per-file per-author shape — don't invent a parallel record for the same (name, commits, last-commit-date) triple `GitInsightsData` already uses.
  - [x] Subtask 1.4: Compute, once at generation time, the repo-wide top-N author roster (bounded, e.g. top 12 by total commits across the analyzed window) used for the discrete-palette mode's fixed color assignment — the individual-author spotlight mode is NOT bounded to this roster (any contributor can be spotlighted per AC #2c), but the discrete *palette* mode's fixed colors are (AC #2b's "Others" bucket exists precisely because that mode IS bounded).

- [x] Task 2 — Sunburst engine: reuse or generalize `Charts.CodeFreshnessSunburst` (AC: #1)
  - [x] Subtask 2.1: Read `Charts.CodeFreshnessSunburst` ([Charts.cs:2747](src/SpecScribe/Charts.cs:2747)) and its ring-geometry/depth-saturation helpers (`FreshnessSunburstMaxDepth`, [Charts.cs:2719](src/SpecScribe/Charts.cs:2719); the recursive wedge-walk ~[Charts.cs:2825](src/SpecScribe/Charts.cs:2825)) in full before writing anything — this is the ONE existing angular-partition tree-walk in the codebase and this story's chart is geometrically identical (same `CodeMapNode` tree, same ring-per-depth model), differing only in per-leaf color source.
  - [x] Subtask 2.2: Refactor the geometry/recursion into a shared, color-source-agnostic builder (e.g. `Charts.TreeSunburst(roots, colorSelector, fileHref, ...)` or similar) that `CodeFreshnessSunburst` becomes a thin wrapper over, and this story's new builder becomes a second thin wrapper over — do not fork/copy-paste the recursive wedge math into a second near-duplicate function. If a clean generalization isn't achievable without distorting 7.12's already-shipped, tested code, document why in Completion Notes and build a sibling instead, but attempt the shared extraction first.
  - [x] Subtask 2.3: New `ChartMetric` case (name it for what it frames, e.g. `ChartMetric.CodeOwnership` — replaces the removed `AuthorConcentration`) + its `WhyText` (framework-neutral, NFR8).
  - [x] Subtask 2.4: The share-% sequential ramp and the discrete top-author palette are two DIFFERENT color models over the same wedges — reuse the level-bucketing pattern `FreshnessLevel`/`HeatLevel` already establish for "map a continuous value to N discrete visual buckets with a real-value legend," rather than a bespoke gradient. The discrete-author palette needs its OWN bounded categorical color set (distinct hues per top-N author + a neutral "Others"/no-data fill) — this is a new, small palette, not a reuse of `--status-*` (non-lifecycle data) or the file-type palette (Story 7.9, different dimension).

- [x] Task 3 — Render all pre-rendered (no-JS) modes server-side (AC: #3, #4, #7)
  - [x] Subtask 3.1: Thread the already-built `CodeMap`/`CodeMap.Roots` (or the author-annotated equivalent from Task 1) into `GitInsightsTemplater.RenderPage` — it isn't currently a parameter; `SiteGenerator` already builds a `CodeMap` once for the Code Map page ([SiteGenerator.cs] — locate the existing `CodeMap.Build`/`CodeMap.BuildVariants` call site), so this is passing an already-computed value to a second call site, not a new build.
  - [x] Subtask 3.2: The DEFAULT server-rendered mode (share % sequential) must be a complete, legible, real chart — not a degraded placeholder that only "works" once JS runs. This is the no-JS baseline ADR 0010 requires.
  - [x] Subtask 3.3: Build the accessible text-equivalent table (file, dominant author, share %, contributor count, last-active date) alongside the chart, following this page's existing pattern of pairing every chart with a text/table equivalent — this table is what AC #3 relies on to prove "disabling JS removes no information."
  - [x] Subtask 3.4: Solo-repo reframe (AC #4) — reuse the existing `insights.ContributorCount == 1` global check (Story 10.6 precedent, previously at [GitInsightsTemplater.cs:154](src/SpecScribe/GitInsightsTemplater.cs:154) before Task 0's removal) for whichever section now needs it; render the honest single-maintainer statement instead of an all-one-color sunburst.
  - [x] Subtask 3.5: Empty/degrade states — no files, no git data — reuse the existing `chart-empty` convention (never a broken or empty sunburst).

- [x] Task 4 — Client-side JS controls, per ADR 0010 (AC: #2, #3, #6)
  - [x] Subtask 4.1: Read [ADR 0010](docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) in full before writing any JS — it sets the constraints this task must satisfy (opt-in-page-only script, generation-time-embedded bounded data, no-JS baseline stays real, one shared engine across 7.11/7.12).
  - [x] Subtask 4.2: Embed the per-file author data needed for live recoloring (bounded — every file's already-capped `Contributors` list + `TotalContributors`, from Task 1) once at generation time as inline JSON or `data-*` attributes on each wedge — never a live git call or client-side recomputation. Determinism (FR31): identical repo state must produce byte-identical embedded data across regenerations.
  - [x] Subtask 4.3: Decide the shared script's home (new `specscribe-analytics.js`, loaded only on opt-in deep-git pages, vs. adding to the existing `specscribe.js`) — per ADR 0010 §6, this must be ONE module other opt-in-analytics stories (7.12, and later Epic 20) can build on, not a one-off. Document the choice in Completion Notes since it's the first exercise of that shared-engine decision.
  - [x] Subtask 4.4: Build the mode selector + per-mode contextual options (individual-author picker populated from the full contributor roster present in the embedded data, not just the bounded top-N; staleness threshold input) and the client-side recolor logic (walk the embedded per-file data, recompute each wedge's fill + its `aria-label`/title text so the text-equivalent and tooltip stay in sync with the active mode — this is new a11y surface ADR 0010 flags as a real cost, don't let the visual recolor drift out of sync with its accessible text).
  - [x] Subtask 4.5: Keep the JS additive per NFR-5/AC #3 — verify the page still passes with JS disabled (no console-error-driven blank states, the pre-rendered default mode and text table are what a no-JS reader sees).

- [x] Task 5 — CSS (AC: #1, #2, #3)
  - [x] Subtask 5.1: Sunburst wedge/legend styling — reuse `CodeFreshnessSunburst`'s existing `.freshness-*` CSS as a base where the shape matches (ring/wedge geometry is shared); add new classes only for the discrete-author palette swatches, the mode-selector control strip, and the individual-author-spotlight highlight treatment (distinguished by more than color alone — e.g. a stroke/opacity change on non-spotlighted wedges, not just a hue swap, matching this project's color-is-never-the-sole-signal convention).
  - [x] Subtask 5.2: `StylesheetTests` coverage for every new selector, scoped narrowly (mirror existing patterns in that file) — same discipline the removed Task 4 of the prior pass followed.

- [x] Task 6 — Tests (AC: all)
  - [x] Subtask 6.1: Server-rendered (no-JS) coverage in `GitInsightsTemplaterTests.cs`: default-mode sunburst renders over a multi-file/multi-author fixture, the text-equivalent table carries every file's dominant author/share/contributor-count/last-active date, code-page links are guarded (mirror the existing guarded-link test pattern), solo-repo reframe fires on `ContributorCount == 1`, empty-file-list degrades to `chart-empty`.
  - [x] Subtask 6.2: `GitMetrics`-level unit tests for the Task 1 per-file author accumulation (mirrors existing `BuildCodeMapMetrics`/`BuildInsights` test conventions) — confirm it covers ALL files, not just a top-N subset, and that determinism holds (same input commits → byte-identical output across repeated calls).
  - [x] Subtask 6.3: `ChartsTests.cs` coverage for the new/generalized sunburst builder (geometry correctness at various tree shapes/depths, mirroring `CodeFreshnessSunburst`'s existing test conventions) and the new `ChartMetric` case (the existing `WhyText_IsMetricGenericAndDefinedOnce` enum-iteration test picks it up automatically).
  - [x] Subtask 6.4: JS behavior needs its own test strategy — this codebase has no browser/DOM-execution test harness today. Decide and document an approach (e.g. a lightweight Node-based test against the extracted recolor logic, or an explicit "manually verified in a real browser, not covered by the automated suite" disclosure) rather than silently shipping untested client logic; flag this choice for review.
  - [x] Subtask 6.5: Regenerate the golden content fingerprint (`SiteGeneratorAdapterTests.cs`) — confirm the diff is scoped to this story's HTML/CSS/JS-asset changes, verified stable across 2 repeated runs, per [Golden-diff normalization gotchas].

## Dev Notes

**This is a full rewrite, not an extension.** The prior pass of this story shipped a plain ranked table (`GitInsightsTemplater.AppendOwnershipSection` + `Charts.ChartMetric.AuthorConcentration`) that is being entirely replaced, along with the OLDER "Files & Contributors" master-detail table that predates this story. Task 0 removes both. Do not treat any of that prior code as reusable scaffolding beyond what's explicitly called out below (e.g. `FileContributor`, the `insights.ContributorCount == 1` solo-repo check, the guarded-link discipline).

**Read ADR 0010 first.** [docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md](docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) is the ratified decision that makes Task 4 possible at all — it supersedes this codebase's long-standing "pure SVG + CSS, no charting JS" default, but ONLY for opt-in deep-analytics pages, and ONLY with a real no-JS baseline preserved (AC #3). Every constraint in Tasks 3–4 traces back to that ADR; re-read it if a task seems to be asking for two contradictory things (pre-rendered AND client-JS) — that's intentional layering, not a mistake.

**The top-N cap problem is real and easy to miss.** `GitInsightsData.Files` (what the removed sections used) is capped at `topFiles` (default 50) — fine for a ranked list, wrong for a whole-tree sunburst, which must cover every file the Code Map does. This is why Task 1 extends `BuildCodeMapMetrics` (the UNCAPPED per-file accumulator) rather than reusing `BuildInsights`'s output.

**Reuse `Charts.CodeFreshnessSunburst`'s geometry — don't refork it.** Story 7.12 just built the one recursive angular-partition tree-walk this codebase has, over the exact same `CodeMapNode`/`CodeMap.Roots` tree this story needs. The only real difference is *what colors each file wedge* — recency (7.12) vs. author concentration/spotlight/staleness (this story). Task 2 asks for a genuine attempt at extracting the shared math before falling back to a sibling function; a second independent angular-partition implementation would be a real maintenance liability (any geometry bug would need fixing twice).

**FR-10 still applies, in every mode.** "No per-author productivity ranking or leaderboard" doesn't stop applying just because the surface got more interactive. The individual-author spotlight mode answers "where has this person worked," not "who has contributed the most" — never render authors in a ranked list against each other, even as a UI convenience for picking who to spotlight (an alphabetical or most-recently-active list is fine; a "top contributors" leaderboard is not).

**FR31 determinism spans both the server and client halves now.** The old story only had to worry about server-side determinism. Now: (1) the embedded per-file author JSON must be generation-time computed and byte-identical across regenerations of the same repo state (server half), and (2) the client JS must never fetch live data or use wall-clock `Date.now()` to decide what "stale" means — the staleness comparison basis (e.g. "months since generation time" or "months since the file's last commit," whichever the design lands on) must itself be embedded at generation time, not computed in the browser at view time.

**NFR8 degrade is unchanged in shape.** `git-insights.html` is still only generated when `--deep-git` produced data (the existing page-level gate). This story doesn't add a new gating decision — Task 3's solo-repo/empty-state branches are rendering choices within an already-gated page, exactly as before.

**Solo-repo (Story 10.6) and guarded-link (Story 7.2) precedents survive Task 0's removal as PATTERNS, not code.** The `insights.ContributorCount == 1` check and the resolver-returns-target-or-plain-text link discipline are still the right approach — they just need to be re-applied inside the NEW section rather than assumed still-present in code that Task 0 deletes.

### Previous Story Intelligence (Story 7.12, most recently completed sibling in this trio)

Story 7.12 (Code Freshness / Age Map, done/review) is the single most relevant precedent: it built `Charts.CodeFreshnessSunburst` from scratch over `CodeMap.Roots`, with a bounded ring-depth (`FreshnessSunburstMaxDepth`), a real-value legend (never "Less…More"), guarded code-page links, and a from-scratch dev-time bug it found and fixed (a missed `fileHref` threading site that left wedges silently unlinked) — worth deliberately re-checking for the equivalent mistake here (this story threads `CodeMap`/`fileHref` into a DIFFERENT page, `git-insights.html`, for the first time; the exact same "forgot to thread the resolver into the new call site" mistake is plausible here too).

The owner has separately noted (this correct-course session) that Story 7.12 is "also going" toward client-side JS controls — implying a follow-on symmetry pass on 7.12 is likely, but that is NOT part of this story's scope; don't touch 7.12's code as part of this story unless the shared-engine extraction (Task 2.2/4.3) requires touching `CodeFreshnessSunburst` itself.

### Git Intelligence

The prior pass of this exact story (now being removed) touched: `src/SpecScribe/Charts.cs`, `src/SpecScribe/GitInsightsTemplater.cs`, `src/SpecScribe/assets/specscribe.css`, `tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs`, `tests/SpecScribe.Tests/StylesheetTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — all still sitting as UNCOMMITTED working-tree changes as of this rewrite (nothing from that pass has been committed). Task 0 works against that live working-tree state, not a prior commit — there is no separate "revert commit" to make.

### Project Structure Notes

- Expected touch points: `src/SpecScribe/GitMetrics.cs` (Task 1 — per-file author accumulation), `src/SpecScribe/CodeMap.cs` (Task 1 — `CodeFileMetrics`/`CodeMapNode` join), `src/SpecScribe/Charts.cs` (Task 2 — sunburst generalization + new `ChartMetric`), `src/SpecScribe/GitInsightsTemplater.cs` (Task 3 — new section, `CodeMap` threaded in), `src/SpecScribe/SiteGenerator.cs` (Task 3.1 — pass the already-built `CodeMap` to the `GitInsightsTemplater.RenderPage` call site), `src/SpecScribe/assets/specscribe.css` (Task 5), a new or extended JS asset (Task 4 — decide new file vs. extending `specscribe.js`, per ADR 0010 §6).
- Tests: `GitMetricsTests.cs` (or equivalent) for Task 1, `ChartsTests.cs` for Task 2, `GitInsightsTemplaterTests.cs`/`SiteGeneratorGitInsightsTests.cs` for Task 3/6, `StylesheetTests.cs` for Task 5, `SiteGeneratorAdapterTests.cs` golden fingerprint for Task 6.5.
- Coordinate with Story 7.12 if its own JS follow-on is scheduled concurrently (per the owner's note above) — the shared JS module (Task 4.3) and any `CodeFreshnessSunburst` generalization (Task 2.2) are the two places a collision is likely.

### References

- [Sprint Change Proposal 2026-07-21 — Git Insights Ownership View](_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-21-git-insights-ownership-view.md) — the correct-course record that produced this rewrite.
- [ADR 0010 — Client-Side Charting JS for Opt-In Deep-Analytics Surfaces](docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) — governs Task 4 in full.
- [Source: _bmad-output/planning-artifacts/epics.md#Story 7.11: Code Ownership & Bus-Factor Insights] — original epic-level AC language (largely superseded by the ACs above; epics.md itself is not being edited by this correct-course, per the checklist's Section 2 finding that no epic-level change is needed).
- [Source: src/SpecScribe/Charts.cs#CodeFreshnessSunburst, FreshnessSunburstMaxDepth, FreshnessLevel, FreshnessLegend] (~lines 2714-2965) — the engine to reuse/generalize.
- [Source: src/SpecScribe/CodeMap.cs#CodeMapNode, CodeFileMetrics, CodeMap.Build] (lines 16-30, 226+) — the tree and per-file metrics record Task 1 extends.
- [Source: src/SpecScribe/GitMetrics.cs#FileContributor, CodeFileMetrics, BuildCodeMapMetrics, BuildInsights] (lines 98-137, 660-738, 918-967) — existing per-file author and metrics computation this story extends/reuses.
- [Source: tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs] — fixture/assertion conventions to adapt for the new section.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5), via `bmad-dev-story`.

### Debug Log References

- This session picked the story up mid-implementation ("halfway through development" per the user's own framing at handoff): Tasks 0–5 and the bulk of Task 6 were already present, uncommitted, on this shared working tree from a prior pass — the rewrite's sunburst engine, JS mode switcher, and CSS all already existed. The story file's own checkboxes had not been updated to reflect that work, so this session's first job was verification, not authorship: read every task/subtask against the actual code, confirmed each via `dotnet test`/manual inspection, then closed the real gaps found.
- Two transient compile failures (`Charts.CodeFreshnessTree` undefined, then `CodeMapTemplater.CodeMapTablePageSize` undefined) surfaced and self-resolved within ~15s across successive `dotnet build` calls with no local edits in between — a concurrent session was actively editing shared files (`CodeMapTemplater.cs`, `ChartsTests.cs`) on this same non-worktree main during this session, consistent with prior incidents in [[shared-main-concurrent-edit-loss-verify-after-edit]]. Re-verified with a stable full-suite pass (twice) before trusting any result.
- Gaps found and closed this session:
  1. **Subtask 0.3 (dead CSS)**: `.gi-table` CSS + its `StylesheetTests` assertion were still present though nothing renders that class since Task 0's table removal (confirmed via grep across every `.cs` file); `.gi-sort-btn`/`.gi-filter`/`.gi-row-hidden` were correctly left alone — still live, shared by the generic `.js-listable` sort/filter enhancer, not git-insights-specific scaffolding.
  2. **Subtask 4.5/6.4 (manual browser verification + JS test-strategy disclosure)**: generated this repo's own site with `--deep-git` and drove `git-insights.html` in a real browser (Browser pane). Confirmed: no-JS raw server HTML pre-colors all 992 file wedges in the share-% default mode with the full text-equivalent tree alongside it and the controls shipping `hidden` (AC #3); with JS, all four modes (share/top/spotlight/staleness) recolor live with correct data (spotlight roster is alphabetical per FR-10, staleness math checked against real day-numbers); guarded links resolve to real code pages; zero console errors. This codebase still has no browser/DOM-execution test harness — per Subtask 6.4's explicit fallback, that manual verification (not an automated DOM test) is the disclosed test strategy for the JS-driven states; the golden-HTML fingerprint remains the automated net for the no-JS baseline only, per ADR 0010's own stated trade-off.
  3. **Real bug found via that manual verification**: `Charts.WalkSunburstWedges` (the geometry/wedge-writer `BuildSunburstSvg` shares between `CodeFreshnessSunburst` and `CodeOwnershipSunburst`, Task 2.2's shared extraction) never baked an `aria-label` onto file wedges — only a `<title>` child. `specscribe.js`'s `initOwnershipSunburst` snapshots `aria-label` off each wedge's accessible-name host (the `<a>` when linked, else the wedge itself) exactly once, before its first recolor, specifically so repeated mode switches append to the original text instead of stacking suffixes. With no `aria-label` ever present, that snapshot silently captured `""`, and the very first mode switch permanently overwrote both `aria-label` and the wedge's `<title>` with a suffix-only string missing the file path — an accessible-name regression the JS itself could never detect, since it only ever reads back what the server gave it. This is the same category of defect Story 7.12's own Dev Notes flagged as "worth deliberately re-checking" here (a missed threading site), just for `aria-label` instead of `fileHref`. Fixed by baking `aria-label="{RepoRelativePath}"` (bare path, no mode-specific suffix — matching the JS's own "append to the original" intent) onto the accessible-name host in the shared wedge writer; verified the fix by round-tripping through a full mode switch in-browser and confirming the path survives. This also gives the (currently non-interactive) `CodeFreshnessSunburst` a real per-file accessible name it lacked before, at no cost — a byproduct of fixing the shared function, not separate scope.
  4. Updated three existing exact-string-match tests (`CodeOwnershipSunburst_LinksAFileWedgeOnlyWhenTheResolverReturnsATarget`, `CodeFreshnessSunburst_LinksAFileWedgeOnlyWhenTheResolverReturnsATarget`, `CodeMapTemplaterTests.RenderPage_FreshnessSunburstLinksAFileWedgeOnlyWhenTheResolverReturnsATarget`) that broke from the new attribute, and added a new regression test (`CodeOwnershipSunburst_BakesTheFilePathAsAriaLabelOnWhicheverElementIsTheAccessibleNameHost`) pinning the fix.
- Subtask 4.3 (shared JS home decision, made by the earlier pass, documented here per the subtask's own request): `initOwnershipSunburst` lives in the existing `specscribe.js`, not a new `specscribe-analytics.js` — this is now the one shared engine 7.11's JS runs through; any future opt-in-analytics JS (7.12's own live controls if/when built, later Epic 20) should extend this same file rather than starting a second one, per ADR 0010 §6.
- Final verification: full suite green twice in a row (2006/2006, ~58s each) after all fixes; golden content fingerprint regenerated and confirmed stable across 2 repeated runs before locking in (`754c2aaeb4682f8b714d1424d6ce34c4b228ea3ee0f2d54baf6e753ad8a0eaf6`), per [[golden-diff-normalization-gotchas]].

**Design-correction pass (same day, after the above landed in review):** the owner reviewed the shipped result and flagged two issues: (1) they wanted a genuine sunburst-OR-treemap TOGGLE, but what shipped was a sunburst with the text-equivalent tree permanently stacked below it — no treemap alternative existed at all; (2) they wanted Activity Over Time to render before Code Ownership, not after. Root cause of #1: Story 7.12's own precedent (`AppendFreshnessSunburstSection`, `CodeMapTemplater.cs`) already establishes the exact pattern this story should have mirrored — a pure-CSS `.board-tabs` radio toggle between a sunburst and a squarified treemap of the SAME data, with the page's accessible text-equivalent pointing elsewhere (7.12 points at the Code Map's own pre-existing file table) rather than being a third view. Task 2.2/AC #1 explicitly called for reusing/generalizing `Charts.CodeFreshnessSunburst`'s "geometry family," and 7.12 already generalizes to a treemap sibling — this story's first pass reused the sunburst geometry but never built the treemap sibling or the toggle, so `Charts.CodeOwnershipTree` (a nested list, not a treemap) ended up filling the "second view" role by default, which reads exactly as the owner described: sunburst, then a tree.
- Fixed by adding `Charts.CodeOwnershipTreemap` (new, mirrors `Charts.CodeFreshnessTreemap` exactly: same `CodeMap.Layout()` geometry, same `DescribeOwnershipFile`/`BuildOwnershipDataAttrs` color/data source as the sunburst, separate `ownership-cell`/`ownership-cell-dir` class family so no other panel's colorize JS can mistake these cells for its own) and a pure-CSS `.board-tabs` toggle in `GitInsightsTemplater.AppendOwnershipSection` (`#ownership-view-sunburst`/`#ownership-view-treemap` radios, `:has()`-driven visibility — zero JS required for the toggle itself, matching 7.12's no-JS-safe pattern).
- Since git-insights.html has no OTHER pre-existing file table to point readers at (unlike code-map.html, which 7.12 leans on), AC #3's accessible text-equivalent requirement still needed satisfying — resolved by keeping `Charts.CodeOwnershipTree` but demoting it from a permanently-visible block to ONE collapsed `<details class="ownership-tree-details">` disclosure below the toggle: present and complete in the no-JS DOM (AC #3 holds), but visually secondary to the two chart forms rather than competing with them (owner's actual complaint).
- Extended the live JS mode switcher (`initOwnershipSunburst`) to query `.ownership-wedge, .ownership-cell` together instead of `.ownership-wedge` alone, and extended every `owner-*`/`level-*` CSS rule to cover both class families — without this, switching color modes while the treemap was toggled visible would have left it frozen on the default share-% coloring (a new inconsistency the toggle itself would have introduced). Verified live in-browser: switching to "Top contributors" mode recolors both the visible treemap AND the hidden sunburst identically (992/992 elements each), so toggling back and forth never shows a stale mode.
- Reordered `GitInsightsTemplater.RenderPage` to call `AppendActivitySection` before `AppendOwnershipSection` (was the reverse); updated the class doc-comment accordingly.
- Manually re-verified in a real browser against this repo's own `--deep-git` output: Activity Over Time now renders first; the Sunburst/Treemap tabs render and both toggle correctly; the collapsed "Full file list" disclosure opens to the same tree as before; zero console errors.
- Added `Charts.CodeOwnershipTreemap` test coverage in `ChartsTests.cs` (7 tests, mirroring `CodeFreshnessTreemap`'s own suite: cell/dir counts, agreement with the sunburst on levels, guarded links, embedded data-attrs, empty degrade, escaping, determinism); added `StylesheetTests.Stylesheet_HasOwnershipTreemapToggleStyles`; updated the discrete-palette and spotlight/staleness `StylesheetTests` to match the now-combined `.ownership-wedge, .ownership-cell` selectors; added `GitInsightsTemplaterTests` coverage for the section order, the toggle markup, and the collapsed-details wrapper.
- Golden content fingerprint regenerated again (`cf5cf5a4d40538cf714dc8e34f4242ef9ab48b9a2d3de26ebfd3bda612db7ad6`), confirmed stable across 2 repeated runs. 2017/2017 tests green (twice in a row).

### Completion Notes List

- Rewrite fully implemented: the Git Insights hub's "Code Ownership & Bus-Factor" section is now one whole-tree interactive sunburst (`Charts.CodeOwnershipSunburst`, sharing `BuildSunburstSvg`'s geometry with Story 7.12's `CodeFreshnessSunburst`) over every source file (not top-N-capped), replacing both the old master-detail file→contributor table and the old ranked ownership table.
- Four color modes work end-to-end: dominant-author share % (sequential, pre-rendered server-side default), top-N authors (discrete palette + bounded "Others"), an unbounded individual-author spotlight (alphabetical roster, never a ranking — FR-10), and a configurable staleness threshold (months since any current contributor last touched the file, generation-time "as of" embedded per FR31). Mode switching is a pure client-side recolor of already-embedded `data-*` attributes — no live git calls, no wall-clock reads.
- No-JS baseline (AC #3, ADR 0010) verified for real: raw server HTML pre-colors every wedge in share-% mode; the mode-selector controls ship `hidden` and are only unhidden by JS. **Note (superseded by the second design-correction pass below):** this originally also shipped a full accessible text-equivalent tree (`Charts.CodeOwnershipTree`); the owner asked for it to be removed entirely, which walks back part of AC #3's literal "text-equivalent table" requirement — see the second design-correction pass entry and this story's Change Log for the tradeoff as flagged to the owner.
- Solo-maintainer repos (AC #4) get an honest single-maintainer statement instead of an all-flagged sunburst, reusing the Story 10.6 `ContributorCount == 1` precedent.
- FR-10 held throughout: author info stays descriptive attribution (share %, contributor count, last-active date) in every mode; a dedicated test (`CodeOwnershipSunburst_NeverRendersALeaderboardOrRankingVocabulary`) guards this.
- One real accessibility bug found and fixed this session (see Debug Log References #3) — the shared sunburst wedge writer now bakes a real `aria-label` per file wedge, closing a silent, permanent accessible-name loss on first JS mode switch. This benefits both this story's ownership sunburst and (as a side effect of the shared function) Story 7.12's freshness sunburst.
- One piece of dead CSS from the old table implementation (`.gi-table`) was cleaned up along with its test guard, per Subtask 0.3's explicit instruction to check for orphaned `.gi-*` scaffolding.
- 2006/2006 tests green (twice in a row); golden content fingerprint regenerated and locked after confirming stability across repeated runs.
- **Design-correction pass:** the Code Ownership section now offers a genuine sunburst/treemap toggle (`Charts.CodeOwnershipTreemap`, new — mirrors Story 7.12's own toggle pattern exactly) instead of a sunburst with the accessible text-equivalent permanently stacked below it. The live JS mode switcher recolors both views together (`.ownership-wedge, .ownership-cell`), verified live in-browser. Activity Over Time now renders before Code Ownership, per owner feedback that it's the page's most orienting chart. 2017/2017 tests green (twice in a row); golden fingerprint regenerated and locked again.
- **Second design-correction pass:** every file wedge/cell now carries a rich `.codemap-card` hover tooltip (`Charts.BuildOwnershipCard`, reusing `BuildTreemapCard`'s established convention) instead of a plain `<title>`; the toggle's active tab now has a visible pressed state (matching the sprint board's own convention); the discrete top-author palette was reduced to and now byte-for-byte matches Story 7.9's 7-hue file-type categorical palette (`Charts.OwnershipTopAuthorPaletteSize = 7`); the legend area is now ONE shared instance with four mode-specific blocks (share/top/spotlight/staleness) that the live JS switcher shows one-at-a-time, closing the "colors don't match up" gap and adding the staleness mode's previously-missing green "fresh" swatch; and the collapsed "full file list" from the previous pass was removed entirely (not demoted) per direct owner instruction — a real, acknowledged walk-back of AC #3's literal text-equivalent-table wording, flagged to the owner rather than silently dropped. The owner's further request for Plotly-style click-to-drill directory filtering was explicitly deferred, not implemented — flagged as a separate scope decision (new charting-JS dependency, ties to Epic 20). 2007/2009 tests green (2 pre-existing unrelated skips); golden fingerprint regenerated and locked again.

### File List

- src/SpecScribe/Charts.cs
- src/SpecScribe/GitInsightsTemplater.cs
- src/SpecScribe/GitMetrics.cs
- src/SpecScribe/SiteGenerator.cs
- src/SpecScribe/assets/specscribe.css
- src/SpecScribe/assets/specscribe.js
- tests/SpecScribe.Tests/ChartsTests.cs
- tests/SpecScribe.Tests/CodeMapTemplaterTests.cs
- tests/SpecScribe.Tests/GitInsightsTemplaterTests.cs
- tests/SpecScribe.Tests/GitMetricsTests.cs
- tests/SpecScribe.Tests/StylesheetTests.cs
- tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs
- tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs
- tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs

## Change Log

- 2026-07-22 (second design-correction pass, this session): Further owner feedback on the rewrite: (1) enhance the tooltips, (2) make it clear which toggle view is currently selected, (3) remove the collapsed "full file list" entirely (not just demote it — a full walk-back of that fallback from the previous pass), (4) discrete author colors should reuse "our" existing categorical scheme, not a bespoke set, and (5) the staleness mode's legend never listed its own "fresh"/green state. Also flagged, explicitly NOT implemented this pass: click-to-drill-into-a-directory filtering on the sunburst, which the owner described wanting via Plotly. Addressed 1–5:
  1. **Tooltips**: every ownership file wedge/cell now carries a rich `data-tip-html`/`js-tip` card (new `Charts.BuildOwnershipCard`) reusing the SAME `.codemap-card` class family `Charts.BuildTreemapCard` already established (RiskQuadrant already reuses that exact card too — one shared style, not a parallel one) — dominant author + share %, contributor count, last-active date, and the full per-author commit breakdown. Replaces the native `<title>` entirely for ownership wedges/cells (never both — `Charts.SunburstWedgeInfo` gained an optional `TipHtml` field the shared `WalkSunburstWedges` writer branches on; freshness/other sunburst consumers pass none and are unaffected).
  2. **Active-tab indicator**: added the SAME pressed-tab CSS (`background`/`box-shadow` on the checked radio's sibling label) the sprint board's and requirements panel's own `.board-tabbar` toggles already use, scoped to the new `#ownership-view-sunburst`/`#ownership-view-treemap` ids — previously neither toggle had ever gotten this treatment.
  3. **Removed the file list entirely**: deleted `Charts.CodeOwnershipTree`/`AppendOwnershipTreeNodes` and all `.ownership-tree-*` CSS — no code left, not merely hidden. This is a real, acknowledged walk-back of AC #3's explicit "accessible text-equivalent table" requirement — the surface now relies on the sunburst/treemap toggle's per-element `aria-label` (bare file path, keyboard-focusable) plus the new rich hover card for information, not a scannable table. Flagged to the owner in the same-session response rather than silently dropped.
  4. **Discrete author palette → the established scheme**: found the actual "our discrete color scheme" the owner meant — Story 7.9's file-type legend (`.codemap-cell.type-csharp/python/script/styles/markup/config/other-lang`, exactly 7 hues: `--teal-deep`, `--moss-light`, `--teal`, `--rust`, `--rust-light`, `--moss`, `--violet`). Reduced the ownership discrete palette from a bespoke 12-hue set (which included `--gold`/`--gold-light` and 3 `color-mix` blends absent from the file-type scheme) down to the SAME 7 hues in the SAME order, and reduced the bounded top-author roster to match: new `Charts.OwnershipTopAuthorPaletteSize = 7` constant, `SiteGenerator`'s `GitMetrics.BuildTopAuthors` call now passes `capN: Charts.OwnershipTopAuthorPaletteSize` instead of the default 12 (a DIFFERENT cap from `GitMetrics.CodeMapFileContributorCap`, which still bounds per-file contributor lists — untouched). Verified live: `owner-author-0`'s computed fill is byte-identical to `--teal-deep`.
  5. **Mode-aware legend, including the missing "fresh" green**: the legend was a single static share-% block that stayed frozen once JS switched to any other mode (root cause of "colors don't always match up" — the visible legend and the actual coloring could disagree). Replaced with FOUR server-rendered mode-specific blocks (`Charts.OwnershipLegend`/`OwnershipTopAuthorsLegend`/`OwnershipSpotlightLegend`/`OwnershipStalenessLegend`), rendered ONCE (not duplicated per toggled view — the colors mean the same thing in both charts), with `specscribe.js`'s `initOwnershipSunburst` now showing exactly the one matching the active mode (`swapLegend`). The staleness legend now has a real `owner-fresh` (green, `--moss-light`) swatch alongside `owner-stale` — the specific gap called out.
  - Manually verified every change live in a real browser: rich tooltip card content confirmed, active-tab pressed state confirmed on both tabs (and flips correctly on toggle), mode switch → legend swap confirmed for all 4 modes, `owner-author-0`'s fill confirmed byte-identical to `--teal-deep`, staleness legend's green swatch confirmed, zero console errors.
  - 9 tests removed (the deleted `CodeOwnershipTree_*` suite), ~10 updated (exact-string assertions broken by the new `js-tip`/`data-tip-html` markup, the palette size change), 4 added (no-tree assertion, rich-tooltip assertion, shared-legend-with-four-blocks assertion, discrete-palette-legend-swatch coverage). 2007/2009 tests green (2 pre-existing skips, unrelated platform-gated symlink tests). Golden content fingerprint regenerated, confirmed stable across 2 repeated runs.
  - **Deferred, not implemented this pass**: the owner's "click and drill into a directory and filter down to that level... via Plotly" request is a genuinely different scope — it implies either a new charting-JS dependency (Plotly) or a hand-rolled drill/filter interaction, both bigger decisions than a same-session fix (this codebase's charting has been zero-dependency since ADR 0010; a real dependency would need its own ADR, and ties directly into Epic 20's Interactive Explorer spike, which exists specifically to make this kind of call). Not actioned here — flagged back to the owner as a candidate for its own story/ADR rather than silently scoped in or silently dropped.
- 2026-07-22 (design-correction pass, this session): Owner feedback after the rewrite reached review: wanted a genuine sunburst/treemap TOGGLE (mirroring Story 7.12's own toggle), not a sunburst with the text-equivalent tree permanently stacked below it; also wanted Activity Over Time to render before Code Ownership. Added `Charts.CodeOwnershipTreemap` (new, mirrors `CodeFreshnessTreemap`'s geometry/pattern exactly, shares `DescribeOwnershipFile`/data-attrs with the sunburst) + a pure-CSS `.board-tabs` toggle in `GitInsightsTemplater`; demoted `Charts.CodeOwnershipTree` to a collapsed `<details>` disclosure (still satisfies AC #3's no-JS text-equivalent requirement, just no longer a competing third view); extended the live JS mode switcher and every `owner-*`/`level-*` CSS rule to cover both the sunburst's `.ownership-wedge` and the treemap's `.ownership-cell` so a mode switch never leaves the toggled-away view stale; swapped the two sections' render order. 11 new/updated tests; manually re-verified live in a real browser (toggle switches correctly, both views recolor together, collapsed disclosure opens, zero console errors); golden content fingerprint regenerated and confirmed stable across 2 repeated runs. 2017/2017 tests green.
- 2026-07-21 (this session): Verified the rewrite's Tasks 0–6 against the actual code (found substantially complete from a prior, uncommitted pass), closed the real gaps: removed dead `.gi-table` CSS/test guard (Subtask 0.3), manually verified all four JS color modes live in a real browser against this repo's own `--deep-git` output and documented that as the Subtask 6.4 JS test-strategy disclosure (no DOM test harness in this codebase), found and fixed a real accessible-name regression in the shared sunburst wedge writer (`aria-label` was never baked, so the JS mode-switcher's "snapshot the base label once" logic silently captured `""` and permanently dropped the file path from every wedge's accessible name after the first mode switch), added a regression test, updated 3 existing tests whose exact-string assertions broke from the new attribute, and regenerated the golden content fingerprint (verified stable across 2 repeated runs). All ACs satisfied; 2006/2006 tests green. Status → review.
- 2026-07-21: Story re-scoped via `correct-course` before its prior implementation reached "done" — the shipped plain-table design (Change Log entry below) is superseded wholesale by an interactive sunburst with a client-JS mode selector (share %, top-authors, individual-author spotlight, staleness), per owner direction and [ADR 0010](docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md). Status reset to `ready-for-dev`; AC/Tasks/Dev Notes rewritten in full; prior implementation's Dev Agent Record entry preserved below for history only — its content no longer describes what this story does.
- 2026-07-21 (superseded): Story 7.11 implemented as a ranked HTML table — `ChartMetric.AuthorConcentration`; `GitInsightsTemplater.AppendOwnershipSection` over `GitInsightsData.Files`/`FileChangeStat` (top-N capped, no new git call); solo-maintainer reframe reused the Story 10.6 `ContributorCount == 1` gate; risk flag reused the "Sole contributor:" vocabulary and the coupling-kind rust/dashed "at-risk" tone. 6 new/extended tests; golden content fingerprint regenerated. This entire implementation is removed by Task 0 of the rewrite above.
