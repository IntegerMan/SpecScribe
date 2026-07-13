---
title: 'Code-item clickthrough on Deep Analytics & Git Insights'
type: 'feature'
created: '2026-07-12'
status: 'in-review'
review_loop_iteration: 0
context: []
baseline_commit: 2d9ae3544fef07edf7baf0b3936651dee5e7edc8
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On `deep-analytics.html` the coupling-graph nodes, ranked-pairs table cells, and hotspot list items render file paths as inert text, and on `git-insights.html` the `fileHref` link seam is never wired — so a reader cannot click a file to reach its code (in-portal `code/…html` page or, in external `--code-url` mode, the hosted source). On the live GitHub Pages site (external mode) nothing links at all.

**Approach:** Reuse Story 7.2's dual-mode code-link resolution — combine the existing in-portal `CodePageHref` and external `BuildExternalSourceUrl` into one guarded file→href resolver, wire it into the already-present `GitInsightsTemplater.fileHref` seam, and thread it through `DeepAnalyticsTemplater` into the three `Charts` primitives (`CouplingGraph`, `CouplingTable`, `HotspotBars`) so each file name/node becomes a link when the resolver returns a target, and plain text otherwise.

## Boundaries & Constraints

**Always:**
- Guard every link on the resolver returning a non-empty href — no target → render exactly today's plain text/`<code>`/`<span>`/SVG node. Never a dead link ([[epic-7-code-link-strategy]]).
- Resolver = in-portal `CodePageHref(path)` first, then external `BuildExternalSourceUrl(path)` — mirroring 7.2's two modes. Works whether the site is in-portal or `--code-url` external.
- New resolver params are optional (`Func<string,string?>? fileHref = null`), so existing callers/tests compile and behave identically. HTML-escape every href and label via `PathUtil.Html`.
- Pure SVG + HTML links only — no JavaScript ([[charting-is-pure-svg-no-js]]). Neutral tokens only ([[specscribe-status-token-system]]).

**Ask First:**
- Any need to change output paths, the `#L{n}` anchor scheme, or the `_codePages`/external-mode gating.

**Never:**
- Do not build a new resolver mechanism or re-scan files — consume the existing `_codePages` map / `CodeSourceBaseUrl`.
- Do not touch citation linkification (`CodeReferenceLinkifier`), code-page generation (7.1), or the git data pipeline.
- Do not add per-line fragments to graph/table/hotspot links (these are whole-file links, like `BuildExternalSourceUrl`).
- Do not disturb the git-insights `:target` contributor drill-down (the row-overlay `gi-row-link` stays; only the file *name* becomes a nav link).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| In-portal, file has code page | `_codePages` has `src/A.cs` | Node/cell/list-item/file-name wraps in `<a href="code/src/A.cs.html">` | N/A |
| External mode | `CodeSourceBaseUrl` set, `_codePages` empty | Link → `{baseUrl}/src/A.cs` | N/A |
| Unresolvable path | resolver returns null | Plain text/`<code>`/`<span>`/bare SVG node (today's markup) | N/A |
| Empty data | no coupling / no hotspots / no files | Existing friendly "chart-empty" notes unchanged | N/A |
| Path with markup chars | `a<b.cs` | Escaped in both href and label | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteGenerator.cs` -- has `CodePageHref` (in-portal) + `BuildExternalSourceUrl` (external); add a combined `CodeItemHref` resolver; pass it into both templater calls (deep-analytics ~L268, git-insights ~L1291).
- `src/SpecScribe/DeepAnalyticsTemplater.cs` -- `RenderPage(deep, nav)` → add optional `fileHref`; pass to the three Charts calls (incl. the lightbox graph copy at L106).
- `src/SpecScribe/Charts.cs` -- `HotspotBars` (L921), `CouplingTable` (L946), `CouplingGraph` (L978): add optional `fileHref`; wrap file label/cell/SVG node in a guarded `<a>`.
- `src/SpecScribe/GitInsightsTemplater.cs` -- `fileHref` seam already implemented; no change (verify).
- `tests/SpecScribe.Tests/DeepAnalyticsTemplaterTests.cs`, `GitInsightsTemplaterTests.cs`, `SiteGeneratorGitInsightsTests.cs` -- extend for resolved/guarded cases.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/Charts.cs` -- Added `Func<string,string?>? fileHref = null` to `HotspotBars`, `CouplingTable`, `CouplingGraph` + a shared `CodeItemLink` helper. Each resolves per file path; when non-empty, wraps the escaped label/cell text (and, for the graph, the `<circle>`+`<text>` node) in a guarded `<a>`; else emits today's markup verbatim. `<title>` tooltips and `role="img"` preserved.
- [x] `src/SpecScribe/DeepAnalyticsTemplater.cs` -- Added optional `fileHref` param to `RenderPage`; forwarded to all `CouplingGraph`/`CouplingTable`/`HotspotBars` calls (including the lightbox graph copy).
- [x] `src/SpecScribe/SiteGenerator.cs` -- Added `CodeItemHref` resolver (mode-selected like `CodeReferenceLinkifier`: external `--code-url` base when set, else in-portal `CodePageHref`). Passed `fileHref: CodeItemHref` into the `DeepAnalyticsTemplater.RenderPage(...)` call and added it to the `GitInsightsTemplater.RenderPage(...)` call.
- [x] `tests/SpecScribe.Tests/DeepAnalyticsTemplaterTests.cs` -- Added unit tests for the I/O matrix: resolved→`<a>`, null→plain, per-item guarding, SVG-node anchor, escaping. (GitInsights `fileHref` seam already covered by `RenderPage_GuardsDetailLinksOnTargetExistence`.)

**Verification result:** Verified on a clean worktree at baseline `2d9ae35` (isolated from the concurrent Story 7.3/7.4 WIP that was breaking the main build): full suite **886 passed / 0 failed**; real `generate --deep-git --code-url …` emitted 14 external graph-node links + 50 external git-insights file links with **0** `code/` leakage and **0** dead hrefs; in-portal `CodePageHref` branch covered by unit + existing 7.1/7.2 tests. `src/SpecScribe` also builds clean on current `main`.

**Acceptance Criteria:**
- Given `--deep-git` data and an in-portal build, when I open `deep-analytics.html`, then each coupling-graph node, ranked-pairs cell, and hotspot item whose file has a code page is a link to `code/<path>.html`, and files without a page stay plain text.
- Given the same build, when I open `git-insights.html`, then each file name in the change-frequency table and each "View file page" link resolves to the file's code page, without breaking the contributor `:target` drill-down.
- Given an external `--code-url` build (as on the live site), when I open either page, then file items link to `{CodeSourceBaseUrl}/<path>` instead.
- Given any path the resolver cannot resolve, when the page renders, then no dead link is emitted and existing tests still pass.

## Verification

**Commands:**
- `dotnet test` -- expected: all green, including new guarded/resolved link cases.
- `dotnet run --project src/SpecScribe -- --deep-git` -- expected: `SpecScribeOutput/deep-analytics.html` and `git-insights.html` contain `href="code/…"` on file nodes/cells/items.
- `dotnet run --project src/SpecScribe -- --deep-git --code-url https://github.com/integerman/SpecScribe/blob/main` -- expected: those same items link to the external base.
