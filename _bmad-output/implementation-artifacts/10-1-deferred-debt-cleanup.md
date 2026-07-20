---
title: 'Story 10.1 deferred-debt cleanup'
type: 'chore'
created: '2026-07-19T00:00:00-04:00'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: 'd8ee85e7b65bd2ee2303283dddd34c3ac8e54edf'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 10.1's code review left three real-but-not-blocking items in `deferred-work.md`: (1) two hand-maintained label→group classifiers can silently disagree, (2) `RenderParityTests` never exercises all four nav groups (Delivery/Insights/Follow-ups/Project) rendered together on one page, and (3) a CSS layout change to the list-batch pane (`.next-steps-cards`/`.next-step-command-group`) rode in on the nav-restructuring commit with no test coverage and a comment that references an unrelated guard.

**Approach:** (1) Make `SiteNav.QuickLinks` itself carry the group tag it was classified into at `Build()` time, and have `HtmlRenderAdapter`'s white key-views band read that tag instead of re-deriving it from a parallel label switch — one classifier, not two. (2) Add a `RenderParityTests` case that builds a `SiteNav` with all four groups populated simultaneously and asserts full parity + all four group `<summary>` labels present. (3) Pin the shipped list-batch layout (3-column grid, stacked Deferred|Action items per card) with a `StylesheetTests` case, and replace the CSS comment's stray reference to the footer-legend guard with an accurate one.

## Boundaries & Constraints

**Always:**
- `SiteNav.QuickLinks`'s new 4th tuple element is a plain `string Group` populated inline at each existing `quickLinks.Add(...)` call site in `SiteNav.Build` — no new classification logic, no behavior change to which group any label lands in (must match today's `HtmlRenderAdapter.KeyViewGroup` switch exactly for every existing case: Delivery/Insights/Follow-ups default-Project).
- `HtmlRenderAdapter.AppendKeyViewsBand`'s `Select` reads `q.Group` directly; delete the now-dead `KeyViewGroup` method and its doc comment.
- Existing 3-tuple call sites (`FollowUpSurfacesTests.cs`) must be updated to the 4-tuple shape; do not change their test intent.
- The new RenderParityTests case must assert zero divergences AND the exact flat nav order AND all four group summary labels appear as non-anchor facts (mirrors `Extract_GroupedNav_RecoversOnlyLeafAnchors_NotGroupSummaries`'s existing assertion style).
- Treat the list-batch CSS layout (3-column grid + vertically-stacked Deferred|Action items pair) as the accepted, intentional current design — it has shipped and survived multiple subsequent stories untouched. Do not revert it toward the older side-by-side wrap from `spec-follow-up-list-batch-actions`; only add coverage and fix the comment.

**Ask First:** none anticipated — all three items are additive test coverage or a behavior-preserving refactor.

**Never:**
- Change which group any existing quick-link label renders under.
- Touch `NavQuickLink` (the `DashboardView`/webview record) — it only reads `Label`/`OutputRelativePath`/`Description` off the tuple today and stays untouched.
- Re-litigate the `.list-batch-actions` layout decision itself.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Every existing quick-link label | `SiteNav.Build` with all signals on | Same Delivery/Insights/Follow-ups/Project grouping as today's `KeyViewGroup` switch, byte-identical rendered HTML | N/A |
| All four nav groups populated on one page | `hasEpics/hasSprint/hasGitInsights/hasDeepAnalytics/hasCodeMap/hasActionItems/hasDeferredWork/hasAdrs/hasReadme` all true | `RenderParity.FindDivergences` empty; `facts.Nav` never includes the four group `<summary>` labels as anchor facts | N/A |
| List-batch CSS | Rendered stylesheet | `.list-batch-actions .next-steps-cards` 3-column rule and `.next-step-command-group` column-stack rule both present | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteNav.cs` -- add `Group` as the 4th named element of the `QuickLinks` tuple; set it inline at each `quickLinks.Add(...)` call
- `src/SpecScribe/NavigationView.cs` -- `NavQuickLink` gains a `Group` (default `"Project"`) so `ToNavigationView` can carry it through
- `src/SpecScribe/HtmlRenderAdapter.cs` -- read `q.Group` in `AppendKeyViewsBand`'s `Select` (falling back to "Project" for an unrecognized value, preserving the old switch's exhaustive default); delete `KeyViewGroup`, keep `KeyViewGroupOrder` (render order only)
- `src/SpecScribe/DashboardViewBuilder.cs` -- its own independent `NavQuickLink` construction site also needs `q.Group` threaded through (missed on the first pass; caught by review)
- `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` -- update the three `Array.Empty<(string, string, string)>()` `QuickLinks` sites to the 4-tuple shape
- `tests/SpecScribe.Tests/IconsTests.cs` -- update the one `QuickLinks` tuple deconstruction to the 4-tuple shape
- `tests/SpecScribe.Tests/SiteNavTests.cs` -- new test pinning every known label's `Group` against the taxonomy (review found the boundary claim wasn't independently spot-checked)
- `tests/SpecScribe.Tests/RenderParityTests.cs` -- new test: all four dark-bar `<details>` groups populated + parity + summary-label assertions
- `tests/SpecScribe.Tests/StylesheetTests.cs` -- new test pinning the list-batch 3-column/stacked rules, scoped to each rule's own body
- `src/SpecScribe/assets/specscribe.css` -- fix the `.list-batch-actions .next-steps-cards` comment (currently references an unrelated footer-legend guard)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SiteNav.cs` -- give `QuickLinks` a `Group` field, populated per-call-site to match today's grouping exactly -- single source of truth for label→group
- [x] `src/SpecScribe/NavigationView.cs` -- thread `Group` onto `NavQuickLink` (default `"Project"` so pre-existing 3-arg construction sites keep compiling) -- needed carrier for the HTML surface, which consumes `NavigationView`, not `SiteNav`, directly
- [x] `src/SpecScribe/HtmlRenderAdapter.cs` -- consume `q.Group`, delete `KeyViewGroup` -- removes the second hand-maintained classifier
- [x] `tests/SpecScribe.Tests/FollowUpSurfacesTests.cs` -- fix the 3 now-broken `QuickLinks` array literals -- keep build green
- [x] `tests/SpecScribe.Tests/IconsTests.cs` -- fix the 1 now-broken `QuickLinks` deconstruction -- keep build green
- [x] `tests/SpecScribe.Tests/RenderParityTests.cs` -- add the four-groups-at-once case -- closes the coverage gap
- [x] `tests/SpecScribe.Tests/StylesheetTests.cs` -- add the list-batch layout pin -- closes the coverage gap
- [x] `src/SpecScribe/assets/specscribe.css` -- correct the stray comment -- hygiene
- [x] `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- re-lock the golden content fingerprint (the corrected CSS comment shifts the shared stylesheet's byte count, which every page's hash includes) -- keep the byte-parity gate green
- [x] `src/SpecScribe/DashboardViewBuilder.cs` -- thread `q.Group` through its own `NavQuickLink` construction -- Blind Hunter + Edge Case Hunter both independently caught this second, missed 3-arg call site
- [x] `src/SpecScribe/HtmlRenderAdapter.cs` -- fall back an unrecognized `Group` to "Project" -- restores the old switch's exhaustive-default safety net that the consolidation had silently dropped
- [x] `tests/SpecScribe.Tests/RenderParityTests.cs` -- rewrite the four-groups case to target the dark-bar `<details>` groups it actually exercises (not the white key-views band its original comment wrongly named), asserting the full flat nav order instead of unscoped substring checks
- [x] `tests/SpecScribe.Tests/StylesheetTests.cs` -- rescope the list-batch pin to each rule's own body via regex, not whole-file `Contains`
- [x] `tests/SpecScribe.Tests/SiteNavTests.cs` -- add a direct `Group`-per-label pin against the full known label set

**Acceptance Criteria:**
- Given the full existing quick-link label set, when the site regenerates, then every label lands in the same Delivery/Insights/Follow-ups/Project group as before this change — markup is byte-identical apart from the one corrected CSS comment (golden fingerprint re-locked, not preserved verbatim).
- Given a `SiteNav` built with every group-gating signal true, when rendered, then `RenderParity.FindDivergences` returns empty and all four group summary labels are absent from `facts.Nav`.
- Given the embedded stylesheet, when inspected, then both the 3-column list-batch grid rule and the stacked command-group rule are present and their comment names the real reason (list-batch pane layout), not the footer-legend guard.

## Design Notes

`HtmlRenderAdapter.KeyViewGroupOrder` (the display-order array) stays — it decides render order among groups, which is orthogonal to per-label classification and not part of the duplication the deferred item flagged.

## Spec Change Log

- The CSS comment fix (task 8) shifts the shared stylesheet's byte count, which the golden content-fingerprint test hashes across every generated page — so the originally-stated "byte-identical" AC was corrected to "re-locked" once that test failed as expected. The constant was re-locked against a live `main` working tree while an unrelated concurrent session (Story 10.2 chart-metadata work) was actively editing `Charts.cs`/`HtmlRenderAdapter.Dashboard.cs` in the same directory, which made the hash a moving target for several attempts; it was finally locked in once that session's edits settled (confirmed unchanged across repeated re-runs). Several full-suite runs during this work also showed unrelated, non-reproducing failures in deep-git/commit-detail tests (git CLI contention under parallel execution against this repo's real history) — none touch files this spec changed, and each passes cleanly in isolation.
- **Review pass (Blind Hunter + Edge Case Hunter, all findings triaged as `patch` — no intent gap or spec defect):** both reviewers independently caught a second, missed `NavQuickLink` construction site in `DashboardViewBuilder.cs` still using the old 3-arg form (would have silently defaulted every dashboard quick-link's `Group` to "Project" regardless of its real classification — exactly the kind of silent-drift risk this story exists to eliminate). Edge Case Hunter also caught that consolidating the label→group switch into per-call-site data dropped the switch's exhaustive `_ => "Project"` default safety net; `HtmlRenderAdapter` now falls back explicitly. Blind Hunter caught that the new `RenderParityTests` case's own comment named the wrong DOM element (claimed `<summary>`/white key-views band; it actually exercises the dark-bar `<details>` groups) and that its trailing assertions were unscoped substring checks against the whole document; the test was rewritten to assert the full flat nav order instead. Both reviewers independently flagged the same weakness in the new `StylesheetTests` case (unscoped `Contains` instead of checking the property lives inside its own rule body); rescoped to a per-rule regex. Added `SiteNavTests.Build_QuickLinkGroups_MatchTheKeyViewsBandTaxonomy` to directly substantiate the "no behavior change to which group any label lands in" boundary the spec asserted but never independently tested. Findings about `.chart-frame-head`/`.heatmap-window` CSS changes and the golden-fingerprint comment's Story 10.2 entry are the concurrent session's own work bleeding into a file-scoped `git diff` of a shared file, not part of this spec — excluded from action here.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: all tests green, no new failures
- `dotnet build src/SpecScribe/SpecScribe.csproj` -- expected: zero errors

**Manual checks (if no CLI):**
- Regenerate the portal against this repo and confirm the white key-views band on a non-Home page is visually unchanged (Delivery/Insights/Follow-ups/Project pills identical to before).

## Suggested Review Order

**Single classifier consolidation**

- Entry point: `Group` becomes the single source of truth for a quick-link's key-views band, set once per `Build()` call site instead of re-derived by a parallel switch.
  [`SiteNav.cs:84`](../../src/SpecScribe/SiteNav.cs#L84)

- `HtmlRenderAdapter` now reads `q.Group` directly, with a fallback for any value outside the four known groups (restores the old switch's exhaustive default).
  [`HtmlRenderAdapter.cs:181`](../../src/SpecScribe/HtmlRenderAdapter.cs#L181)

- The deleted classifier's replacement: display-order-only array, no longer a label→group mapping.
  [`HtmlRenderAdapter.cs:324`](../../src/SpecScribe/HtmlRenderAdapter.cs#L324)

- `NavQuickLink` carries `Group` through to the webview/dashboard-facing typed view (defaults to "Project" so old 3-arg call sites keep compiling).
  [`NavigationView.cs:20`](../../src/SpecScribe/NavigationView.cs#L20)

- The second, independent `NavQuickLink` construction site — missed on the first pass, caught by review — now threads `Group` too.
  [`DashboardViewBuilder.cs:72`](../../src/SpecScribe/DashboardViewBuilder.cs#L72)

- Direct pin: every known label's `Group` against the taxonomy, substantiating the "no behavior change" boundary.
  [`SiteNavTests.cs:8`](../../tests/SpecScribe.Tests/SiteNavTests.cs#L8)

**Test coverage gap (all four nav groups at once)**

- New case builds a `SiteNav` with every group-gating signal on and asserts the full flat nav order plus zero render-parity divergences across all four dark-bar `<details>` groups simultaneously.
  [`RenderParityTests.cs:102`](../../tests/SpecScribe.Tests/RenderParityTests.cs#L102)

**List-batch CSS layout (confirmed intentional, now covered)**

- The corrected comment: explains the fixed 3-column grid's actual purpose instead of a stray unrelated-guard reference.
  [`specscribe.css:2857`](../../src/SpecScribe/assets/specscribe.css#L2857)

- New pin scoped to each rule's own body (not whole-file `Contains`), so a regression that moves the property elsewhere would be caught.
  [`StylesheetTests.cs:652`](../../tests/SpecScribe.Tests/StylesheetTests.cs#L652)

**Peripherals**

- Golden content fingerprint re-locked (the CSS comment change shifts every page's byte count).
  [`SiteGeneratorAdapterTests.cs:580`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs#L580)

- Mechanical tuple-shape fixups for the three now-4-tuple `QuickLinks` construction sites.
  [`FollowUpSurfacesTests.cs`](../../tests/SpecScribe.Tests/FollowUpSurfacesTests.cs)

- Mechanical tuple-shape fixup for the one now-4-tuple `QuickLinks` deconstruction.
  [`IconsTests.cs`](../../tests/SpecScribe.Tests/IconsTests.cs)
