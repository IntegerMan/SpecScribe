# Deferred Work

Real-but-not-now items surfaced during reviews. Each is safe to leave; revisit when the related area is next touched.

## Deferred from: code review of story-1-2 (2026-07-06)

- **Case-insensitive requirement-ID matching is not actually implemented.** `RequirementLinkifier.RefPattern` (`\b(FR|NFR)(\d+)\b`, `src/SpecScribe/RequirementLinkifier.cs:17`) is compiled without `RegexOptions.IgnoreCase`, so a lowercase `fr6`/`nfr2` reference in an artifact is silently never linkified — even though `RequirementsModel.ById` lookup itself is case-insensitive. The story's "case-insensitive requirement lookup" must-preserve constraint is only half-true and has no test. Pre-existing behavior, not introduced by Story 1.2. Decide whether lowercase references should link (add `IgnoreCase` + a test) or whether uppercase-only is intended (document it) when this seam is next touched.
- **Multi-digit partial-token boundary is unpinned.** No test proves `FR60` does not match when only `FR6` is known (and vice-versa); the existing partial-token test covers only non-digit adjacency (`FR1x`, `XFR1`, `tests/SpecScribe.Tests/LinkifierTests.cs:61`). Behavior is correct today thanks to the greedy `\d+` inside `\b…\b`, but a dedicated regression pin would guard against a future `\d?`-style slip.

## Deferred from: code review of story-1-3 (2026-07-06)

- **Inconsistent `scroll-margin-top` offsets for sticky-nav clearance.** Three magic values target the same "land below the sticky nav" intent: `var(--nav-offset)` (3.75rem) on TOC-targeted sections (`src/SpecScribe/assets/specscribe.css:227`), `5rem` on `.ac-criterion` (`:686`), `4.5rem` on `.req-index .section-divider[id]` (`:1340`). Each still clears the nav, so no correctness bug — but if the nav height changes only the `var()`-driven anchors follow. Unify onto `var(--nav-offset)` when this CSS is next touched.
- **Diff carries changes unrelated to Story 1.3 scope.** The GitHub Pages deploy-retry workflow (`.github/workflows/publish-docs-live-pages.yml`) and `README.md` edits are harmless and match a prior commit's intent, but are not part of Story 1.3's ACs. Confirm they belong in this story's commit or split them out.

## Deferred from: code review of spec-home-next-steps-label-and-code-review (2026-07-06)

- source_spec: `spec-home-next-steps-label-and-code-review.md`
- **Retrospective fallback can mislabel a project that has review work.** In `BmadCommands.ForProject` (`src/SpecScribe/BmadCommands.cs`), the project "Next Steps" panel falls back to a project-wide retrospective suggestion when `suggestions.Count == 0`, printing "Every epic is drafted and every story detailed." If a module does **not** expose a `code-review` command (so the new review-prompt loop adds nothing) and a story is sitting in `review` status with no other actionable work (no ready/active story, no undrafted epic story, no pending epic), the fallback still fires — claiming the project is fully detailed while a change awaits review. Pre-existing behavior (review status never fed the home panel before this change) and narrow (needs a code-review-less module). Evidence: `suggestions.Count == 0` is the sole fallback guard and the review loop is a no-op when `Command("code-review")` is null. Guard the fallback on "no review work either" when this panel is next touched.

## Deferred from: code review of story-1-4 (2026-07-06)

- **Detail-page `<h1>` titles are emitted unescaped.** `EpicsTemplater.RenderEpic`/`RenderStory` write `<h1>{epic.Title}</h1>` / `<h1>{story.Title}</h1>` without the `Html()` wrapper that the index/card paths use. Story 1.4 touched these exact lines (`sb.`→`main.` prefix) but did not introduce the escaping gap. Titles are parsed from controlled artifact headings, so injection risk is low — but the inconsistency means a title containing a literal `<` renders unescaped. Wrap in `PathUtil.Html(...)` (matching the index pages) when this templater is next touched.
- **Heatmap dates use ambient culture instead of `InvariantCulture`.** `Charts.CommitHeatmap` formats `heatAria` (`src/SpecScribe/Charts.cs:435`) and the per-cell `<title>` (`:473`) as `{date:yyyy-MM-dd}` under the ambient culture, while the month labels (`:451`) use `CultureInfo.InvariantCulture`. Under a non-Gregorian ambient calendar on the build host the aria/cell dates would drift from the month axis. Pre-existing pattern (cell titles already do this). Normalize both to `InvariantCulture` when the heatmap is next touched.

## Deferred from: code review of 1-5-dashboard-insight-polish-and-visual-truthfulness (2026-07-06)

- ~~**`AppendEpicStatusPanel` silently drops epics with an unmapped roll-up class.**~~ **Resolved 2026-07-06** as part of `spec-sunburst-epic-focus-and-ready-rollup`: adding the `ready` tier to `ForEpic` would have triggered this drop, so `AppendEpicStatusPanel` now buckets all five `ForEpic` outputs (done/active/ready/drafted/pending). The donut and its center fraction no longer disagree for any class `ForEpic` currently returns.
- **`HeatLevel` collapses the scale when `maxCount <= 1`.** `Charts.HeatLevel` returns level 4 (darkest) whenever `maxCount <= 1` (`src/SpecScribe/Charts.cs:508`), so a project with a uniform one-commit-per-active-day history renders every cell at maximum intensity, visually indistinguishable from heavy activity. Pre-existing, but the Story 1.5 E1 widening to a ~15-week window makes uniform-low histories far more common on screen. Revisit the low-end bucketing when the heatmap scale is next touched.

## Deferred from: code review of spec-gherkin-styling-and-story-epic-links (2026-07-06)

- source_spec: `spec-gherkin-styling-and-story-epic-links.md`
  summary: Undrafted-story placeholder pages are only reachable from inline "Story N.M" prose mentions — the epic-page story cards render undrafted titles as unlinked text and the home "Now & Next" card falls back to the epic page, so the primary navigational surfaces don't route to the placeholder.
  evidence: `EpicsTemplater.AppendStoryCard` leaves an artifact-less story's title as a plain `<span>` (only artifact-backed titles become `story-title-link`), and `HtmlTemplater`'s next-story card uses `story.ArtifactOutputPath ?? epic page`. This story deliberately scoped the placeholder as a link target for mentions; wiring the epic cards / next-story card to placeholders was called out as an "Ask First" boundary in the spec, so it was left for a focused follow-up rather than expanded here.

- source_spec: `_bmad-output/implementation-artifacts/spec-commit-heatmap-contrast-and-day-drilldown.md`
  summary: Bound the commit heatmap's history payload for mature repos (git log window or per-day cap) — the grid and drill-down panels currently span the full history, embedding every commit hash+subject in the dashboard HTML.
  evidence: Review found `git log` runs uncapped and every active day emits a full panel; fine for young BMAD projects, multi-MB dashboard risk (and 3s git-timeout risk) on repos with tens of thousands of commits. Windowing is a product decision (grid has always spanned full history), so it needs its own intent.

- source_spec: `_bmad-output/implementation-artifacts/spec-commit-heatmap-contrast-and-day-drilldown.md`
  summary: Add a keyboard bypass affordance around the commit heatmap for dense histories where nearly every day is an active-day link (~100 sequential tab stops).
  evidence: Review noted the zero-commit-cells-unlinked mitigation only helps sparse histories; a repo with daily commits over the 15+ week window produces ~105 tab stops with no skip link before the panels.

## Deferred from: code review of story-2-2 (2026-07-07)

- **Single-spec-kernel assumption in nav quick-link and index-card title.** `SiteNav.Build`'s `specKernelHub` lookup (`src/SpecScribe/SiteNav.cs:111-112`) uses `FirstOrDefault` to pick one `specs/*/SPEC.md`, so a project with more than one spec-kernel bundle only gets a nav quick-link to whichever one enumerates first; `HtmlTemplater.IndexCardTitle` (`src/SpecScribe/HtmlTemplater.cs:697-700`) similarly rewrites the card title to the fixed string "SPEC — Canonical Contract" for any doc with an `id:` starting `SPEC-`, so two kernels would render identical, indistinguishable index cards. Out of AC scope, single-kernel only today — ACs describe exactly one spec kernel per project, this repo has one, no current use case needs more. Revisit if a second kernel is ever authored.
