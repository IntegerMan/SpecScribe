---
title: 'Code references default to in-portal code pages (external as a per-page button)'
type: 'feature'
created: '2026-07-13'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '19553fe8404e152b3adc7d11f243f63fcfcb3eb2'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When an external source base is configured or auto-detected from the git remote (Story 7.7 — true for this repo), every git-analytics file link (Deep Analytics coupling + hotspots, Git Insights file table, Code Map treemap) jumps straight to GitHub instead of the in-portal code page whose value is showing each file's related files and insights. Separately, the dashboard Git Pulse "Top changed files" render as plain text with no link.

**Approach:** Flip the shared analytics file-link seam (`CodeItemHref`) to resolve in-portal first, falling back to the external URL only when no page exists; the code page keeps its existing "View on GitHub" button. Expand up-front code-page discovery to also include the git-analytics file sets (top-changed, hotspots, coupled) so those pages exist, and thread the same seam into Git Pulse so its top-changed files link too.

## Boundaries & Constraints

**Always:**
- In-portal code page is the DEFAULT target for any resolvable repository source file; external URL is only a fallback (no page) and the per-page "View on GitHub" action.
- Never emit a dead link — a path resolving to neither a page nor an external URL degrades to plain text (existing AC #1 discipline).
- Only add pages for files passing `CodeReferenceScanner.TryResolveRepoFile` (inside repoRoot, NOT under sourceRoot, exists, not ignored, under size cap). `_bmad-output` docs get no code page.
- Analytics-only pages (no citing artifact) render cleanly with an empty "Referenced by"; the file's 7.4 insights / 7.8 related files remain the value.
- Preserve bytes where behavior is unchanged: the webview dashboard path (no resolver supplied) renders Git Pulse exactly as today.

**Ask First:**
- Linking `_bmad-output` docs to their doc pages instead of external — out of scope; confirm before adding.

**Never:**
- Don't alter the `--code-url`/auto-detection settings, `BuildExternalSourceUrl`, or the citation linkifier's in-portal behavior (already passes `codeSourceBaseUrl: null`).
- Don't walk the filesystem to create pages — only the bounded top-N analytics sets plus existing citations.
- Don't touch `.md` doc-citation handling (`SourceLinkifier`) or `#L{n}` fragments.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Behavior |
|----------|--------------|-------------------|
| Cited source file, base set | `src/…/Foo.cs` has a page | `CodeItemHref` → `code/…Foo.cs.html`, not GitHub |
| Analytics-only source file | Top-changed/hotspot, uncited | Page generated; link in-portal; empty "Referenced by" |
| Non-code analytics file | `_bmad-output/…/sprint-status.yaml` | No page → external URL when base set; plain text otherwise |
| No base, no page | In-portal mode, non-resolvable file | null → plain text (never dead link) |
| Git Pulse top-changed, resolver set | `git.TopChangedFiles` | Each label links via the same seam |
| Git Pulse, resolver omitted | webview / null `fileHref` | Plain text — byte-identical to current output |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteGenerator.cs` -- `CodeItemHref` (~1096): the seam to flip. `DiscoverCodeReferences` (~1113): add analytics sets here; `_progress.Git`/`.DeepGit` are set (~190) before discovery (~212). Index render (~2012): pass the resolver to the dashboard.
- `src/SpecScribe/Charts.cs` -- `GitPulsePanel` (~633, top-changed ~675) renders plain `<span>`; add optional `fileHref` and use `CodeItemLink` like `HotspotBars` (~962).
- `src/SpecScribe/HtmlTemplater.cs` -- `RenderIndex`/`BuildIndexPage` (~104/111) and the `DashboardViewBuilder.Build` call (~115) must carry the optional resolver.
- `src/SpecScribe/DashboardView.cs` + `DashboardViewBuilder.cs` + `HtmlRenderAdapter.Dashboard.cs` -- host-neutral seam conveying the resolver to the `GitPulsePanel` call (~108).
- `src/SpecScribe/GitMetrics.cs` -- `GitPulse.TopChangedFiles`, `DeepGitPulse.Hotspots`, `FileInsight.CoupledFiles` are the `(Path,count)` sets.
- `src/SpecScribe/CodeReferenceScanner.cs` -- `TryResolveRepoFile` gate. `src/SpecScribe/CodeFileTemplater.cs` -- existing "View on GitHub" button (~481); confirm empty "Referenced by" degrades.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SiteGenerator.cs` -- Flipped `CodeItemHref` to `CodePageHref(path) ?? BuildExternalSourceUrl(path)`; doc comment updated.
- [x] `src/SpecScribe/SiteGenerator.cs` -- In `DiscoverCodeReferences`, added repo-relative paths from `_progress?.Git.TopChangedFiles`, `_progress?.DeepGit.Hotspots`, `_progress?.DeepGit.Coupling` (both pair files), and `Insights.Files` (the Git Insights hub table) — each admitted only via `TryResolveRepoFile`, into the sorted `referenced` set. Coupling pair files subsume the 7.8 related-files (`FileInsight.CoupledFiles`). Whole-codebase code-map metrics deliberately excluded (NFR1). Null-safe, bounded, deterministic.
- [x] `src/SpecScribe/Charts.cs` -- Added optional `Func<string,string?>? fileHref = null` to `GitPulsePanel`; top-changed labels render via `CodeItemLink(path, fileHref)`, plain `<span>` (with `title=`) when null/unresolved.
- [x] `src/SpecScribe/HtmlTemplater.cs` + `HtmlRenderAdapter.Dashboard.cs` -- Threaded an optional `codeItemHref` resolver from `RenderIndex`/`BuildIndexPage` → `RenderDashboardBody` → `AppendDashboardSection` → the `GitPulsePanel` call; default null preserves current/webview bytes. (No `DashboardView`/`DashboardViewBuilder` change needed — kept the record pure to avoid the webview JSON-serialization landmine; the delegate rides the render methods only.)
- [x] `src/SpecScribe/SiteGenerator.cs` -- Index render passes `CodeItemHref` as the dashboard resolver.
- [x] `src/SpecScribe/CodeFileTemplater.cs` -- Verified (no change needed): `BuildRelationshipsPanel` already returns "" when there are no refs and no related nodes, and `BuildInsightsPanel` returns "" when nothing to show — analytics-only pages (empty "Referenced by") render cleanly.
- [x] `tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs` -- Updated `CoupledFileWithoutCodePage_RendersNonLinkChip` to use a coupled file deleted from disk (still exercises the chip/never-dead-link path under the new feature) and added `CoupledUncitedFile_NowGetsInPortalCodePage` (a coupled on-disk file now gets a page + links). Full suite verified GREEN in an isolated clean-base worktree: 1016 passed, only the PRE-EXISTING golden-fingerprint drift failing (see Spec Change Log).

**Acceptance Criteria:**
- Given a repo with a detected/`--code-url` base, when Deep Analytics, Git Insights, or Code Map render, then their file links point at in-portal `code/….html` pages, not GitHub.
- Given a source file appearing only in git-analytics, when the site generates, then it has an in-portal page and its links resolve there, showing insights/related files with no "Referenced by" entries.
- Given the Git Pulse panel with a resolver, when it renders, then each top-changed source file links to its code page (external/plain fallback for `_bmad-output`/non-code); with no resolver it is byte-identical to current output.
- Given an in-portal page with a configured base, when it renders, then it still shows the "View on GitHub" action.
- Given a file resolvable to neither page nor external URL, when any surface renders it, then it degrades to plain text.

## Spec Change Log

- **2026-07-13 — adversarial review (Blind Hunter + Edge Case Hunter), no loopback.** No intent_gap / bad_spec findings. Patches applied:
  - **(medium, Edge #1)** Git Pulse newly makes top-changed files clickable, but a deleted/renamed hot file fell through to an un-existence-checked external URL → a 404 link on the home dashboard. Fixed: `CodeItemHref` now existence-gates the external fallback (`File.Exists` on the repo path) so a vanished file degrades to plain text; `_bmad-output` docs (real files) keep their external link. Locked by new test `GenerateAll_DeletedHotFile_ExternalMode_DegradesToPlainTextNotDeadLink`.
  - **(docs, Blind #1/#3)** Corrected three now-contradictory comments that still claimed "_codePages empty in external mode".
  - **(low, Blind #6)** Renamed `CoupledFileWithoutCodePage_RendersNonLinkChip` → `DeletedCoupledFile_RendersNonLinkChip`.
  - Rejected/verified-safe: broad `--code-url` inward shift (intended), `Insights.Files` top-50 inclusion (bounded), subdir-open degradation (graceful/pre-existing), case-colliding siblings on case-sensitive FS (pre-existing), golden byte-identical (proven).
  - Re-verified in a clean-base worktree after patches: **1017 passed / 1 failed** (only the pre-existing golden drift).

## Design Notes

The seam is already `fileHref: CodeItemHref` on Deep Analytics, Git Insights, and Code Map; only its preference order changes — from `base ? external : in-portal` to `in-portal ?? external`. Because pages now exist for analytics files, the common case resolves in-portal. `SiteSettings` already documents "the pages are always generated" and the `--code-url` link as "additive" — this makes the analytics surfaces honor that stated contract.

**Implementation notes (verification + concurrent work):**
- The shared working tree became dirty mid-session with a **parallel session's half-applied "entity prev/next navigation" work** (new `EntityPager.cs`; `CommitDayTemplater.RenderPage` gained an `EntityPager? pager` param but its caller + `CommitDayTemplaterTests` weren't updated → the tree didn't compile independent of my change). To let the main src build, I added a **temporary** `EntityPager.None` at the commit-day caller in `SiteGenerator.cs` (clearly commented) — that story owns wiring the real day-sequence pager. The main **test project still won't compile** until that session updates `CommitDayTemplaterTests.cs`.
- Because of that, I verified my change in an **isolated worktree off the last clean commit (19553fe)**: build clean (0/0), full suite **1016 passed / 1 failed**.
- The one failure — `GoldenContentFingerprint` — is a **PRE-EXISTING drift on main**, not from this change: the pristine base produced the identical new hash `0326270f…` (committed constant `7696b72…` was already stale). The golden fixture is non-git, so all of this change's analytics/Git-Pulse code is dormant for it (byte-neutral). I did **not** update the constant — its correct value in `main` also depends on the concurrent EntityPager rendering, so it must be regenerated once that work stabilizes.

## Verification

**Commands:**
- `dotnet test` -- expected: all green (updated goldens + new unit tests pass).
- `dotnet run --project src/SpecScribe -- generate <this-repo> --deep-git --output SpecScribeOutput` then open `index.html`, `deep-analytics.html`, `git-insights.html`, `code-map.html` -- expected: file links open in-portal `code/….html` pages that carry a "View on GitHub" button; Git Pulse top-changed source files are links.

**Manual checks:**
- An uncited hotspot/top-changed source file now has a `code/….html` page its analytics link points to.
- A `_bmad-output/*.yaml|md` top-changed entry falls back to the external link (or plain text with no base) — never a dead link.

## Suggested Review Order

**Link resolution (the core flip)**

- Entry point: the seam every analytics surface uses — now in-portal-first, external existence-gated (Patch B).
  [`SiteGenerator.cs:1104`](../../src/SpecScribe/SiteGenerator.cs#L1104)

- Where in-portal pages get minted for the git-analytics file sets (top-changed, hotspots, coupling, hub table).
  [`SiteGenerator.cs:1184`](../../src/SpecScribe/SiteGenerator.cs#L1184)

- The one call that actually wires the resolver into the dashboard (only the main HTML index).
  [`SiteGenerator.cs:2061`](../../src/SpecScribe/SiteGenerator.cs#L2061)

**Git Pulse linking**

- The panel gains an optional resolver; null keeps webview/SPA byte-identical.
  [`Charts.cs:633`](../../src/SpecScribe/Charts.cs#L633)

- Top-changed label routed through the shared `CodeItemLink` (same as hotspots).
  [`Charts.cs:688`](../../src/SpecScribe/Charts.cs#L688)

- Resolver threaded through the dashboard render path into the panel call.
  [`HtmlRenderAdapter.Dashboard.cs:108`](../../src/SpecScribe/HtmlRenderAdapter.Dashboard.cs#L108)

**Tests (peripheral)**

- Locks Patch B: a deleted hot file must never become a dead external link.
  [`SiteGeneratorCodeInsightsTests.cs:173`](../../tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs#L173)

- The new positive behavior: an uncited on-disk coupled file now gets a page + links.
  [`SiteGeneratorCodeInsightsTests.cs:152`](../../tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs#L152)

- The preserved chip / never-dead-link path (coupled file deleted from disk).
  [`SiteGeneratorCodeInsightsTests.cs:125`](../../tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs#L125)
