---
title: 'Declutter the home dashboard'
type: 'refactor'
created: '2026-07-14'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: 'f7c68592845319f0e53670307dbb100836dead52'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The home dashboard carries duplicated "card list" clutter that competes with the primary-journey pulse panels: a grid of quick-dev work cards and a long run of bottom index bands (planning-doc, spec, implementation, overview, ADR, and retro card lists), all of which are reachable elsewhere. On a 30-second daily-pulse dashboard they add noise without adding a primary-journey glance.

**Approach:** Remove two render surfaces from the dashboard body — the "Direct & Quick-Dev Work" quick-dev **card grid** and **all home index bands** — while keeping the chart/summary pulse panels, the compact Deferred/Retro count callouts, the Planning Artifacts coverage panel, and the "Explore Key Views" quick-link pills (the pills stay as the reachability net for docs no longer listed on home). Remove the now-dead view-model surface and parity facts so no dead data or unrendered projections remain.

## Boundaries & Constraints

**Always:**
- Keep every chart/summary panel unchanged: stat tiles, Sunburst, Now & Next / sprint board, Epic Status donut, Overall Progress, Story Pipeline funnel, Git Pulse, Planning Artifacts coverage, Requirements panel (flow/grid toggle), Progress-by-Epic mosaic.
- Keep the "Explore Key Views" quick-link pills (`AppendDashboardQuickLinks` / `DashboardView.QuickLinks`) exactly as-is — they are the reachability net.
- Keep the compact Deferred Work and Retro Action Items **count callouts**.
- Preserve determinism and the RenderParity semantic contract: after removal, the view-model no longer carries index-band/quick-dev-card data, and RenderParity no longer projects `IndexCards`/`WorkCards` facts (expected == actual == removed).
- Pure C# string-building + view-model changes only — no JS, no NuGet package, no CSS behavior change (dead CSS may be left in place).

**Ask First:**
- Retitling or restructuring the "Direct & Quick-Dev Work" section heading (default: keep the heading, drop only its card grid).
- Any change that would delete a canonical panel or a quick-link.

**Never:**
- Do NOT remove or alter the stat tiles (including the "Direct changes" tile), the coverage panel, the requirements panel, or any git/timeline panel.
- Do NOT touch the standalone pages themselves (`adrs/index.html`, `retros.html`, epics/sprint/requirements pages) — only the home dashboard's duplicated listings.
- Accepted residual: docs with no other index (e.g. `docs/` analysis files, misc implementation artifacts, quick-dev spec pages) become reachable by direct URL only. This is an accepted consequence of removing the index bands, not a bug to work around.
- Do NOT change any count, count source, or status token.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full project | Requirements + epics + docs + ADRs + retros + quick-dev + deferred | Dashboard renders all pulse panels + Explore Key Views + Deferred callout + Retro callout; NO quick-dev card grid; NO index bands anywhere below | N/A |
| Only quick-dev work, no deferred/retro | `work.QuickDev` non-empty, `work.Deferred == null`, `openRetro == 0` | Work section is OMITTED entirely (no orphan "Direct & Quick-Dev Work" heading); "Direct changes" stat tile still shows its count | N/A |
| Deferred only | `work.Deferred` set, no quick-dev, `openRetro == 0` | Work section renders heading + Deferred callout only | N/A |
| Retro only | `openRetro > 0`, `work.IsEmpty` | Work section renders heading + Retro callout only | N/A |
| No docs/adrs/retros | empty index inputs | No index bands (same as before, now unconditional) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- `RenderDashboardBody` (drop the `IndexBands` loop), `AppendWorkTypesSection` (drop the quick-dev grid + fix the omit gate), delete now-dead `AppendIndexBand`/`AppendPlanningBand`/`AppendIndexCard`/`AppendCardStatusBadge`/`AppendCardMeta`.
- `src/SpecScribe/DashboardView.cs` -- remove the `IndexBands` field; delete the now-unused `IndexBand`, `IndexCardView`, `PlanningLayout`, `IndexCardStyle` types.
- `src/SpecScribe/DashboardViewBuilder.cs` -- drop `IndexBands` from `Build`; remove `BuildIndexBands`/`BuildPlanningBand`/`BuildDocCard`/`BuildPrimaryPrdCard`/`BuildAdrCard`/`BuildRetroCard` and helpers made dead (`IndexCardTitle`, `CardMeta`, `TopLevelFolder`, `HumanizeFolderName`, `FindByFileName` — keep any still referenced by kept code, e.g. `IsWellKnownTopLevelFolder`). Remove the now-unused `docs`/`adrs`/`retros` params from `Build` if nothing else uses them.
- `src/SpecScribe/HtmlTemplater.cs` -- update the `DashboardViewBuilder.Build(...)` call site to the trimmed signature.
- `src/SpecScribe/RenderParity.cs` -- remove the `IndexCards` and `WorkCards` fact fields, their `From(view)` projections, their HTML extractors, and their `Check(...)` calls. Keep `QuickLinks`.
- `spike/vscode/renderer/Program.cs`, `spike/delivery/exporter/Program.cs` -- update any `DashboardViewBuilder.Build(...)` call to the trimmed signature (compile-only; spikes are not shipped).
- Tests: `HtmlRenderAdapterTests.cs`, `RenderSectionParityTests.cs`, `SectionViewModelSerializationTests.cs`, `RenderSpaParityTests.cs`, `WebviewRenderAdapterTests.cs`, `HtmlTemplaterTests.cs`, `SiteGeneratorAdapterTests.cs` (golden fingerprint).

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` -- remove the `IndexBands` render loop; in `AppendWorkTypesSection` remove the quick-dev card grid and change the omit gate to `if (work.Deferred is null && openRetro == 0) return;` so a quick-dev-only project renders no orphan heading; delete the dead index-band/card helper methods.
- [x] `src/SpecScribe/DashboardView.cs` -- remove the `IndexBands` field and the four dead index-card types.
- [x] `src/SpecScribe/DashboardViewBuilder.cs` -- stop building `IndexBands`; remove dead builder methods + params; keep stat/progress/now-next/quick-links/work building intact.
- [x] `src/SpecScribe/HtmlTemplater.cs` -- adjust the `Build(...)` call to the trimmed signature.
- [x] `src/SpecScribe/RenderParity.cs` -- drop the `IndexCards` + `WorkCards` facts (fields, projections, extractors, checks); leave `QuickLinks`, `StatTiles`, `NowNextCards`, `ProgressBars`, `StoryRows` untouched.
- [x] `spike/vscode/renderer/Program.cs`, `spike/delivery/exporter/Program.cs` -- fix the `Build(...)` call sites so the solution compiles.
- [x] Tests -- delete/adjust assertions for the removed grid + bands + parity facts; add coverage for the four work-section gate scenarios; regenerate `GoldenContentFingerprint` after confirming the byte diff is only the removed grid + removed bands.

**Acceptance Criteria:**
- Given a full project, when the dashboard body is rendered, then it contains no `quick-dev-card` markup and no `index-section-title`/`index-grid`/`index-card` markup below the pulse panels, while the Sunburst, sprint board, donut, funnel, git pulse, coverage panel, requirements panel, Progress-by-Epic mosaic, Explore Key Views pills, and Deferred/Retro callouts are all still present.
- Given a project whose only extra work is quick-dev (no deferred, no open retro items), when the dashboard is rendered, then the "Direct & Quick-Dev Work" section is omitted entirely (no heading) and the "Direct changes" stat tile still renders its count.
- Given the removed surfaces, when RenderParity compares the view model to the rendered HTML, then parity passes with no `IndexCards`/`WorkCards` facts and the `QuickLinks` fact unchanged.
- Given `dotnet test`, when the suite runs, then it is green (including regenerated golden fingerprint and updated SPA/webview parity).

## Design Notes

The "Explore Key Views" pills are deliberately kept (not cut) because they are the only remaining aggregated reachability path on home for docs that lack a dedicated index — this reconciles the decluttering goal with the project's truthfulness/"don't orphan pages" invariant.

Removal is done at the view-model level (not render-only) so no unrendered `IndexBands`/quick-dev-card data lingers and the RenderParity semantic contract stays honest — consistent with the Story 6.2 single-source discipline. `WorkInventory`/`DashboardView.Work` stays intact (the Deferred callout and the "Direct changes" stat tile still read it); only the card-grid *rendering* and the `WorkCards` *parity fact* are removed.

## Verification

**Commands:**
- `dotnet build src/SpecScribe/SpecScribe.csproj` -- expected: 0 warnings, 0 errors.
- `dotnet test` -- expected: all green (golden fingerprint regenerated; SPA/webview/section parity updated).
- `dotnet run --project src/SpecScribe -- generate` -- expected: generation succeeds; output to `SpecScribeOutput/`.

**Manual checks:**
- Open the generated home page: the pulse panels, Explore Key Views pills, and Deferred/Retro callouts are present; there is no quick-dev card grid and no bottom index-card sections. ADRs/Retros/epics still reachable via nav and the pills.

## Dev Agent Record

### File List

Production (`src/SpecScribe/`):
- `HtmlRenderAdapter.Dashboard.cs` — removed the `IndexBands` render loop from `RenderDashboardBody`; removed the quick-dev card grid from `AppendWorkTypesSection` and changed its omit gate to `if (work.Deferred is null && openRetro == 0) return;`; deleted the now-dead `AppendIndexBand` / `AppendPlanningBand` / `AppendIndexCard` / `AppendCardStatusBadge` / `AppendCardMeta` methods.
- `DashboardView.cs` — removed the `IndexBands` property; deleted the now-unused `IndexBand`, `IndexCardView`, `PlanningLayout`, and `IndexCardStyle` types; trimmed the class-doc `<see cref>` list.
- `DashboardViewBuilder.cs` — stopped setting `IndexBands` in `Build`; removed the now-unused `docs` / `adrs` / `retros` parameters; deleted `BuildIndexBands`, `BuildPlanningBand`, `BuildDocCard`, `BuildPrimaryPrdCard`, `BuildAdrCard`, `BuildRetroCard`, `CardMeta`, `TopLevelFolder`, `HumanizeFolderName`, `FindByFileName`, `IndexCardTitle`. Kept `IsWellKnownTopLevelFolder` + `KnownIndexGroups` (still used by `HtmlTemplater.IsWellKnownTopLevelFolder` → `SiteGenerator`'s unrecognized-structure notice).
- `HtmlTemplater.cs` — updated the `DashboardViewBuilder.Build(...)` call site to the trimmed signature. (`RenderIndex`/`BuildIndexPage` keep their public `docs`/`adrs`/`retros` params — no longer forwarded to `Build`; no compiler warning, public surface unchanged.)
- `StatusStyles.cs` — removed the now-orphaned `ForDoc` + `DocLabel` methods (step-04 review patch) and the class-doc reference to them.
- `RenderParity.cs` — removed the `IndexCards` + `WorkCards` `SectionFacts` fields, their `Empty` initializers, their `From(view)` projections, the `FlattenBandCards` helper, the `IndexCardRegex` / `PrimaryCardRegex` / `WorkCardRegex` regexes, the `ExtractIndexCards` / `ExtractWorkCards` extractors, and the `Check("section.indexCards", …)` / `Check("section.workCards", …)` calls. Kept `QuickLinks`, `StatTiles`, `NowNextCards`, `ProgressBars`, `StoryRows`.

Spikes (compile-only):
- `spike/vscode/renderer/Program.cs`, `spike/delivery/exporter/Program.cs` — updated the `DashboardViewBuilder.Build(...)` call sites to the trimmed signature.

Tests (`tests/SpecScribe.Tests/`):
- `HtmlRenderAdapterTests.cs` — dropped `IndexBands` from the `DashboardWithRequirements` fixture; added the four work-section gate-scenario tests (quick-dev-only omits the section; deferred-only; retro-only; full project shows both callouts but no `quick-dev-card`/`index-card` markup).
- `RenderSectionParityTests.cs` — removed `IndexBands` from the fixtures, the `IndexCards`/`WorkCards` fact assertions, and the index-card-drill-target divergence test; narrowed the quick-link/work-card divergence test to quick-links only.
- `SectionViewModelSerializationTests.cs` — removed the `IndexBand`-round-trip test.
- `RenderSpaParityTests.cs`, `WebviewRenderAdapterTests.cs` — dropped `IndexBands` from the dashboard fixtures.
- `HtmlTemplaterTests.cs` — deleted the home index-band tests (unrecognized-folder bands, well-known-folder order, Spec Kernel band, the four planning-section band tests) and rewrote the quick-dev/deferred test to assert the surviving work section + absence of the removed grid/cards.
- `SiteGeneratorSpecKernelTests.cs`, `PlanningArtifactsGenerationTests.cs`, `SiteGeneratorTraceabilityTests.cs`, `SiteGeneratorAdrToleranceTests.cs` — repointed home-index-card assertions to the surviving surfaces (standalone ADR pages' status classes, page-on-disk existence, the ADRs reachability link, the Spec quick-link pill) and deleted the tests that asserted the removed ADR listing-card date/summary rendering.
- `SiteGeneratorAdapterTests.cs` — repointed the unrecognized-folder test to the structure notice + page-on-disk (band assertions removed); regenerated `GoldenContentFingerprint` from `f3e85799…` to `25d81efe…`.
- `StatusStylesTests.cs` — removed the `ForDoc_*` and `DocLabel_*` theory tests (step-04 review patch; the methods they covered are gone).

### Completion Notes

- `dotnet build src/SpecScribe/SpecScribe.csproj` → 0 warnings, 0 errors. `dotnet build` (whole solution incl. spikes + tests) → succeeds. `dotnet test` → **1158 passed, 0 failed, 0 skipped**. `dotnet run --project src/SpecScribe -- generate` → succeeds (167 generated, 2 skipped; output to `SpecScribeOutput/`).
- Golden fingerprint: old `f3e85799b44b9a6128e27fe0c3b7044aa623bf85d55919deb149a775e4351a63` → new `25d81efe0dd291230bf51a76536c2ecd8e423d47c57ffe829bd35e73bb1def4f`. Confirmed the delta is only the intended removals by inspecting the generated home page: `quick-dev-card`, `index-grid`, `index-card--primary`, `index-card-branch`, and `class="index-card"` all occur **0** times, while every pulse panel (stat-grid, sunburst, coverage, requirements, funnel, git-pulse, Progress-by-Epic), the Explore Key Views pills, and the Deferred + Retro callouts are all present. The single remaining `index-section-title` is the retained work-section heading.
- Parameter cleanup deviation (within spec latitude): `docs`/`adrs`/`retros` were removed from `DashboardViewBuilder.Build` (no remaining use in the trimmed method) and all three call sites updated. `HtmlTemplater.RenderIndex`/`BuildIndexPage` keep those params in their public signatures (no longer forwarded); leaving them is warning-free and avoids a large public-API/caller cascade the spec did not scope.
- **Review patches (step-04 adversarial review — no loopback):** (1) `spike/delivery/exporter/Program.cs` — removed a leftover `dashboardView.IndexBands` reference in the JSON data-layer object that the initial pass missed (compile break in that spike); (2) `StatusStyles.cs` — removed `ForDoc` + `DocLabel`, orphaned once `AppendCardStatusBadge` was deleted (their only production caller), plus the class-doc `<see cref="ForDoc">` line, and removed their two theory tests in `StatusStylesTests.cs`; (3) `DashboardViewBuilder.cs` — trimmed the stale class-doc summary that still described the removed band/grouping logic. Both spikes still fail to build on a **pre-existing** stale `SiteNav.Build(hasStructure:)` call (not caused by this change) — recorded in `deferred-work.md`. Post-patch: `dotnet build src/SpecScribe` → 0 warnings; `dotnet test` → **1139 passed, 0 failed** (19 fewer cases = the removed `ForDoc`/`DocLabel` theory data rows).
- Test-scope deviation: beyond the test files the spec listed, four generation-level suites (`SiteGeneratorSpecKernelTests`, `PlanningArtifactsGenerationTests`, `SiteGeneratorTraceabilityTests`, `SiteGeneratorAdrToleranceTests`) also asserted home index-band content and had to be updated. Assertions for surviving behavior (standalone ADR-page status, page generation, reachability links, quick-link pills) were preserved/repointed; tests that asserted only the removed ADR listing-card **date/one-line-summary** rendering (Story 10.4's home-card surface) were deleted — that render surface no longer exists (the `AdrEntry.Date`/`Summary` data is still parsed but no longer rendered on home; the standalone ADR pages are untouched per the spec's Never-touch-standalone-pages boundary).

## Suggested Review Order

**Render removal (start here)**

- The dashboard body now ends after the work section — no `IndexBands` loop; grasp the whole shape here first.
  [`HtmlRenderAdapter.Dashboard.cs:16`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs#L16)

- The one logic change: the work section is omitted unless a Deferred callout or an open retro item exists (no orphan heading).
  [`HtmlRenderAdapter.Dashboard.cs:305`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs#L305)

**View-model + builder contract**

- `DashboardView` no longer carries `IndexBands`; the four index-card types are gone. `Work`/`QuickLinks` stay.
  [`DashboardView.cs:1`](../../src/SpecScribe/DashboardView.cs#L1)

- `Build` no longer computes bands (trimmed params + deleted helpers); confirm no dead builder code remains.
  [`DashboardViewBuilder.cs:33`](../../src/SpecScribe/DashboardViewBuilder.cs#L33)

**Parity contract symmetry**

- `IndexCards`/`WorkCards` facts removed on both sides (projection + extractor + check); `QuickLinks` fact kept.
  [`RenderParity.cs:337`](../../src/SpecScribe/RenderParity.cs#L337)

**Status seam cleanup (review patch)**

- `ForDoc`/`DocLabel` removed — orphaned once the doc-card badge renderer was deleted.
  [`StatusStyles.cs:189`](../../src/SpecScribe/StatusStyles.cs#L189)

**Tests & golden gate**

- The four work-section gate scenarios (quick-dev-only omits; deferred-only; retro-only; full = both callouts, no card markup).
  [`HtmlRenderAdapterTests.cs:499`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs#L499)

- Golden fingerprint regenerated — the byte gate proving only the intended removals changed.
  [`SiteGeneratorAdapterTests.cs:207`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs#L207)
