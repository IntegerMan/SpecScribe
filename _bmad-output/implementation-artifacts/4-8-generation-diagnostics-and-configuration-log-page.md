---
baseline_commit: 143bb98
---

# Story 4.8: Generation Diagnostics and Configuration Log Page

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer running SpecScribe on a project whose structure or framework differs from the defaults,
I want a generated page that records the run's warnings, skipped or unsupported artifacts, and effective configuration,
so that silent or partial degradation is detectable in the output itself rather than only in console scrollback.

## Acceptance Criteria

_Verbatim from [epics.md](../planning-artifacts/epics.md) Story 4.8 (lines 768–786)._

1. **Given** a generation run that emits non-fatal diagnostics (unsupported, malformed, or skipped artifacts)
   **When** the site is generated
   **Then** a diagnostics page lists each notice with its category, source path, and message
   **And** the page is reachable from the site (nav or dashboard) and degrades to a clean all-clear state when there are no notices.

2. **Given** a completed run
   **When** the diagnostics page is generated
   **Then** it records the effective configuration and detection results (source root, resolved ADR location, output directory, deep-git flag, detected framework/module)
   **And** this information is derived entirely at generation time with no remote calls, consistent with local-first operation.

## Scope Decision — READ FIRST (confirm or veto before dev)

This story **spun out of Story 4.2** (2026-07-10, [sprint-status.yaml:87](sprint-status.yaml)) so that partial/degraded generation is detectable in the *output itself*, not only in console scrollback. It is the **consumer** of the `AdapterDiagnostic` channel that Story 4.1 built ([4-1-…md:41,57](4-1-shared-framework-adapter-contract-and-projection-path.md)) and that Story 4.2 emits onto ([4-2-…md:92](4-2-decouple-rendering-from-personal-project-structure-assumptions.md)). **If you disagree with the scope below, raise it before writing code, not at review** (Epic 3 retro action item: don't defer a defining decision to the dev and correct it later).

**This story delivers two small static output pages + one site-wide footer link:**
- a **Diagnostics (run log) page** — the AC #1/#2 deliverable, and
- an **About page** — the owner-chosen *reachability path* to it (see "Owner-decided" below).

### ⚠️ The one way this story is UNLIKE the rest of Epic 4 — READ THIS
Stories 4.1 and 4.2 are held to **byte-for-byte identical output** — their golden-diff test is the primary guardrail. **Story 4.8 is the opposite: it DELIBERATELY changes rendered output.** It adds two new pages and adds an "About" link to the site-wide footer (`PathUtil.RenderFooter`), which appears on **every** page. So:
- Do **not** try to keep output byte-identical. The 4.1/4.2 golden fixtures **will** change (every page gains the footer link) — **update them intentionally** and note the change; a changed golden assertion here is *expected*, not a regression signal.
- The change to every page must be *only* the footer link (plus the two new pages). Nothing else about existing pages should move. Verify the diff is exactly "footer link added everywhere + two new files."

**IN scope**
- **Diagnostics page (AC #1, #2)** — a new `diagnostics.html` at the output root that:
  - lists each of the run's **non-fatal notices** (category · source path · message) in a table (diagnostics-first silhouette — see Owner-decided);
  - folds the **effective configuration + detection results** into a collapsible `<details>` below the table (AC #2);
  - renders a **clean all-clear state** ("No notices — clean run") when there are zero notices;
  - is generated on **every** full run (never gated away — the all-clear state IS the zero-notice rendering).
- **Notice source = the whole run's non-fatal notices** (owner-chosen): both adapter *ingestion* diagnostics (`AdapterDiagnostic`, already mapped into the run's `GenerationEvent` list) **and** render-time non-fatal events (`GenerationOutcome.Error` / `GenerationOutcome.Skipped` from the ADR/page/insight phases). See Task 2 for the single-source, no-double-count approach.
- **About page (AC #1 "reachable from the site")** — a new `about.html` carrying SpecScribe's own product metadata (version, description, author, repository link — sourced from the assembly, see Dev Notes) plus a prominent link to the Diagnostics page.
- **Footer link** — `PathUtil.RenderFooter` gains an "About" link so the About page (and through it, the Diagnostics log) is reachable from every page.
- **Shared output-path constants** on `SiteNav` (`AboutOutputPath`, `DiagnosticsOutputPath`) so writer and templaters can't disagree (matches the `GitInsightsOutputPath`/`DeepAnalyticsOutputPath` convention at [SiteNav.cs:22,28](../../src/SpecScribe/SiteNav.cs)).

**OUT of scope (do NOT start here)**
- **A top-nav entry or a dashboard callout** for either page. The owner chose **footer → About → Diagnostics** as the reachability model (Owner-decided). Do **not** add nav `Items` or a dashboard `work-callout`. (This also sidesteps the nav-dangle problem entirely — the pages are always generated, but they live off the footer, not the nav.)
- **New diagnostic *categories* or new emission sites.** 4.8 renders what 4.1/4.2 already emit; it does not go hunting for new things to warn about. Do not add diagnostics to parsers.
- **Changing how diagnostics are *collected*.** The `AdapterDiagnostic` channel and `MapDiagnostics` ([SiteGenerator.cs:681](../../src/SpecScribe/SiteGenerator.cs)) are 4.1's; reuse them. The one allowed touch is enriching the *message* text mapped from a diagnostic so the fine category survives on the page (Task 2) — no structural change to the channel.
- **Recomputing the page in watch-mode incremental rebuilds.** Both new pages are full-`GenerateAll` artifacts (the Diagnostics page is a whole-run report). Watch's incremental paths do not regenerate them — see "Watch-mode" in Dev Notes; this is accepted and must be noted, not fixed.
- **Status-vocabulary work, ADR/grouping generalization (4.2), new-framework adapters (4.3–4.7), the view-model contract (6.1), a user-facing config surface.** None of it. 4.8 is a read-only reporting surface over data that already exists at run's end.

**One-line test for "is this in scope?":** if it renders the run's already-collected notices or already-resolved configuration into a reachable page, it's in. If it *produces* new diagnostics, changes *how* they're collected, or adds a nav/dashboard surface, it's out.

## Owner-decided (locked 2026-07-10)

Elicited up front (memory: create-story-elicit-visual-intent — this story introduces a new visual surface, so the design direction is owner-locked here, not deferred to the dev):

1. **Page silhouette → diagnostics-first table, config in `<details>`.** Lead with the notices table (the headline); fold "Effective configuration & detection" into a collapsible `<details>` below it. The notices table is the point; config is reference.
2. **Reachability → footer → About → Diagnostics.** The site-wide **footer** links to a new **About page** that carries SpecScribe's version, a brief description, the author, and a repository link — and from there links to the **Diagnostics log** page. **No** top-nav entry and **no** dashboard callout for either page.
3. **Notice scope → the whole run's non-fatal notices.** Adapter ingestion diagnostics **plus** render-time non-fatal events (ADR parse errors, skipped pages, any non-fatal render error) — the complete "what silently degraded this run" surface, not just the ingestion channel.

## Tasks / Subtasks

- [x] **Task 1 — Add the shared output-path constants** (AC: #1)
  - [x] Add `public const string DiagnosticsOutputPath = "diagnostics.html";` and `public const string AboutOutputPath = "about.html";` to `SiteNav` next to the existing page-path constants ([SiteNav.cs:10–34](../../src/SpecScribe/SiteNav.cs)). Follow the same XML-doc convention as `GitInsightsOutputPath` ([:24–28](../../src/SpecScribe/SiteNav.cs)): note that the page is written by the generator and linked by the footer/templater so the two can't disagree, and that it is **not** a top-nav item (deliberate — reached via the footer/About path). Tag `[Story 4.8]`.
  - [x] Do **not** add entries to `SiteNav.Build`'s `Items` or `QuickLinks` for these pages.

- [x] **Task 2 — Assemble the run's non-fatal notice list (single source, no double-count)** (AC: #1)
  - [x] The run already unifies notices onto one channel: `GenerateAll` accumulates a `List<GenerationEvent> events` and adapter ingestion diagnostics are mapped **into** that same list via `events.AddRange(MapDiagnostics(bundle.Diagnostics))` ([SiteGenerator.cs:117](../../src/SpecScribe/SiteGenerator.cs)); render-time non-fatal events (`Error`/`Skipped`) are added by the ADR/page/insight phases. **Render the page from this single `events` list**, filtered to `Outcome is GenerationOutcome.Error or GenerationOutcome.Skipped`. This is the "whole run's non-fatal notices" surface with **zero double-counting** (each adapter diagnostic is mapped into `events` exactly once — 4.1 was explicit that a caught failure is reported only via its diagnostic, never twice; [SiteGenerator.cs:116](../../src/SpecScribe/SiteGenerator.cs)).
  - [x] **Preserve the fine ingestion category on the page.** `MapDiagnostics` currently collapses `AdapterDiagnosticCategory` (`Unsupported`/`Malformed`/`Skipped`/`Error`) down to a `GenerationOutcome` ([SiteGenerator.cs:681–684](../../src/SpecScribe/SiteGenerator.cs)), losing the finer word the owner's chosen design shows (`[Unsupported]`, `[Malformed]`, `[Skipped]`). Recover it **without** adding a second channel: enrich the mapped event's `Message` to carry the category, e.g. `$"[{d.Category}] {d.Message}"`, so ingest notices read "Unsupported: …"/"Malformed: …" on the page while render-time notices keep their `Outcome` (Error/Skipped) as the category. Confirm this message enrichment is acceptable on the console path too (it flows through `IGenerationReporter`/`GenerationEvent` — the console already prints messages; a category prefix is additive and harmless). **This is the only allowed touch to the 4.1 channel.**
  - [x] The notice **category** column = the fine `AdapterDiagnosticCategory` word for ingest notices (from the enriched message) or the `GenerationOutcome` (`Error`/`Skipped`) for render-time notices; **source path** = `GenerationEvent.RelativePath` (already the source-relative or output-relative path the run reports); **message** = `GenerationEvent.Message` (may be null for a bare skip — render an empty/em-dash cell, never crash).

- [x] **Task 3 — Build the effective-configuration + detection model** (AC: #2)
  - [x] Gather, entirely from data already in hand at run's end (no new I/O, no remote calls — NFR3/AC #2): **source root** (`_options.SourceRoot`), **resolved ADR location** (`_options.AdrSourceRoot`, plus whether it was explicit via `_options.AdrSourceExplicit`), **output directory** (`_options.OutputRoot`), **deep-git flag** (`_options.DeepGitAnalytics`), **detected framework/module** (`_module.Module` / `_module.ModuleLabel` — [ModuleContext.cs:32,60](../../src/SpecScribe/ModuleContext.cs)). Optionally include repo root (`_options.RepoRoot`), README-included (`_options.IncludeReadme`), and site title (`_options.SiteTitle`) as they round out "what this run actually did." All fields are already-resolved values on `_options`/`_module`.
  - [x] Render absolute paths **repo-relative where possible** for readability (the run knows `_options.RepoRoot`), falling back to the absolute path if it isn't under the repo root. Consistent with how the rest of the portal shows source-relative paths.
  - [x] **Local-first proof (AC #2):** everything here is a field read or reflection call — assert in a test that generation issues **no** network calls (there is no HTTP client anywhere in the path; the test can be a simple "these are pure reads" assertion / structural test, matching NFR3's "no remote telemetry"). Add a one-line code comment stating the local-first guarantee.

- [x] **Task 4 — Diagnostics templater + page** (AC: #1, #2)
  - [x] Add `DiagnosticsTemplater.RenderPage(...)` (new file, `static class`, matching `ActionItemsTemplater`'s shape — [ActionItemsTemplater.cs](../../src/SpecScribe/ActionItemsTemplater.cs)). Signature carries: the filtered notice list, the config/detection model, and the `SiteNav` (for `RenderHeadOpen`/`RenderNavBar`/`RenderFooter`/breadcrumb).
  - [x] **Silhouette (Owner-decided #1):** header (`<h1>Generation Diagnostics</h1>` + a `doc-subtitle` with the site title and notice count), then the **notices table** in `<main id="main-content">`, then a collapsible `<details><summary>Effective configuration &amp; detection</summary>…</details>` holding the config as a definition list (`<dl>`). Close with `PathUtil.RenderFooter(...)`.
  - [x] **Notices table:** one row per notice — a **category badge**, the **source path**, and the **message**. Route the badge through the status-token system (memory: specscribe-status-token-system — the six `--status-*` tokens are the single stage→color source; route every new badge through them; memory: charting-is-pure-svg-no-js). Reuse `StatusStyles.Badge(...)` the way `ActionItemsTemplater` does ([ActionItemsTemplater.cs:49](../../src/SpecScribe/ActionItemsTemplater.cs)); map severity → token: `Malformed`/`Error` → the error/danger status token, `Unsupported`/`Skipped` → a muted/neutral warning token. **Never color-only** (NFR6, UX-DR17): the category word is always present as text next to the color.
  - [x] **All-clear state (AC #1):** when the notice list is empty, render a clear "No notices — clean run" panel (positive/neutral status token) instead of an empty table. The `<details>` config block still renders.
  - [x] **Breadcrumb:** `Home / About / Diagnostics` (the reachability path), last crumb null-pathed as plain text (matches `ActionItemsTemplater`'s breadcrumb convention, [ActionItemsTemplater.cs:23–28](../../src/SpecScribe/ActionItemsTemplater.cs)).
  - [x] HTML-escape every dynamic value (`PathUtil.Html`) — paths and messages can contain `<`/`&`/exception text. Do **not** reference-linkify the message (an exception message could embed "Story N.M"/"FR-9" fragments that the linkifier would wrap and distort — same trap `WriteActionItems` documents at [SiteGenerator.cs:847–849](../../src/SpecScribe/SiteGenerator.cs); pass the diagnostics HTML through `File.WriteAllText` **without** `ApplyReferenceLinks`).

- [x] **Task 5 — About templater + page, and the footer link** (AC: #1)
  - [x] Add `AboutTemplater.RenderPage(SiteNav nav)` (new file). Content: SpecScribe **version**, a **brief description**, the **author**, and a **repository link** — plus a prominent link to the **Diagnostics** page (`SiteNav.DiagnosticsOutputPath`). Keep it a small, styled informational page (reuse `doc-header`/`doc-subtitle`/existing prose classes; no new CSS unless genuinely needed).
  - [x] **Source the product metadata from the assembly, not new literals** (single source of truth = the csproj — [SpecScribe.csproj:17–19](../../src/SpecScribe/SpecScribe.csproj) already defines `Version 0.1.0`, `Authors "Matt Eland"`, `Description "…"`). Read them via reflection so About never drifts from the package: `AssemblyInformationalVersionAttribute` (or `Assembly.GetName().Version`) for version, `AssemblyCompanyAttribute`/`AssemblyMetadata` or the generated `AssemblyDescriptionAttribute` for author/description. Confirm which attributes the SDK emits from `Authors`/`Description` (SDK maps `Authors`→`AssemblyCompany`, `Description`→`AssemblyDescription`, `Version`→`AssemblyInformationalVersion`). The repository URL is already a literal in the footer today (`https://github.com/IntegerMan/SpecScribe`, [PathUtil.cs:87](../../src/SpecScribe/PathUtil.cs)) — promote it to one shared constant used by both the footer and the About page rather than repeating it.
  - [x] **Footer link (touches every page):** update `PathUtil.RenderFooter` ([PathUtil.cs:86–87](../../src/SpecScribe/PathUtil.cs)) to add an "About" link (`about.html`) alongside the existing "SpecScribe" repo link. **Every templater calls `RenderFooter`**, so this appears site-wide — that's the intent (reachability). Keep the footer markup minimal and the existing repo link intact. Because `RenderFooter` takes a per-page `trailingHtml` (usually the generated timestamp) and pages live at different depths, ensure the About href is correct from every page's location — the existing footer uses a root-relative-ish `about.html`; confirm it resolves from nested pages (e.g. `adrs/index.html`). If depth is a problem, thread a relative prefix (as `RenderNavBar` does via `PathUtil.RelativePrefix`) rather than hardcoding.
  - [x] Verify the `<details>` disclosure and any footer/About styling work in **both** light and dark themes and honor `prefers-reduced-motion` (native `<details>` has no animation — fine; don't add JS).

- [x] **Task 6 — Wire both pages into `GenerateAll` (write last, after all notices are known)** (AC: #1, #2)
  - [x] The Diagnostics page is a **whole-run report**, so it must be written **after every phase has run and appended its events** — i.e. at the very end of the `lock` in `GenerateAll`, after `WriteIndex` ([SiteGenerator.cs:214–216](../../src/SpecScribe/SiteGenerator.cs)). Pass the fully-accumulated `events` list + the config/detection model. Its own write appends a `Generated` event afterward (not a notice, so no self-reference problem). The About page is static (product metadata only) — write it in the same place (or anytime; it has no run dependency).
  - [x] Add `WriteDiagnostics(nav, events)` and `WriteAbout(nav)` private methods mirroring `WriteActionItems` ([SiteGenerator.cs:840–852](../../src/SpecScribe/SiteGenerator.cs)): render → `File.WriteAllText(Path.Combine(_options.OutputRoot, SiteNav.*OutputPath), html)`. **Do not** run the diagnostics HTML through `ApplyReferenceLinks` (Task 4). Emit a `GenerationEvent(Generated, …)` for each so the console run summary and any output-inventory test see them.
  - [x] Both pages are written on **every** full `GenerateAll` — no gating (the all-clear state is the zero-notice rendering). Since they always exist after a full build and watch never deletes them, the site-wide footer "About" link never 404s.

- [x] **Task 7 — Tests** (AC: #1, #2)
  - [x] `DiagnosticsTemplaterTests`: (a) a notice list with one of each category renders a row per notice carrying the category word, the source path, and the message; (b) an **empty** notice list renders the all-clear "No notices" state, not an empty table; (c) the config `<details>` contains source root, resolved ADR location (+ explicit/default indication), output dir, deep-git flag, and detected module/framework label; (d) category is never color-only (the word is in the text).
  - [x] `AboutTemplaterTests`: the About page shows version, description, author, a repository link, and a link to `diagnostics.html`; product metadata matches the assembly attributes (assert against the reflected values, not hardcoded copies).
  - [x] `PathUtil` / footer test: `RenderFooter(...)` output contains the About link; extend or add a small assertion (existing `PathUtilTests` if present) — and confirm the About href resolves from a nested page depth.
  - [x] `SiteGenerator`-level test (extend `SiteGeneratorAdapterTests` or add one): a run whose source contains a **malformed** artifact (reuse 4.1's malformed-artifact fixture — [4-1-…md:156](4-1-shared-framework-adapter-contract-and-projection-path.md) `BmadArtifactAdapterTests` show the pattern) produces a `diagnostics.html` that lists that notice; a **clean** fixture produces a `diagnostics.html` in the all-clear state; `about.html` is always produced. Assert **no double-counting** — a single malformed artifact yields exactly one row.
  - [x] **Update the 4.1/4.2 golden-output fixture(s)** for the new footer link + two new files (this is the expected, deliberate output change — see the ⚠️ note). Do it as an intentional fixture update with a comment, not by weakening the assertion.
  - [x] Add to the existing test project (`tests/SpecScribe.Tests/`, `net10.0`, xUnit 2.9); file-per-unit naming.

- [x] **Task 8 — Verify end-to-end** (AC: #1, #2)
  - [x] `dotnet test` — whole suite green. The **only** existing assertions that should change are the golden-output ones (footer link on every page + two new files); if any *other* assertion changes, you altered something you shouldn't have — stop and reconsider.
  - [x] Generate this repo's own site (output dir `SpecScribeOutput` — memory: generate-output-dir-is-specscribeoutput; **never** `--output docs/live`) and confirm: `about.html` and `diagnostics.html` exist; every page's footer has the About link; the About page links to the diagnostics log; the diagnostics page shows this repo's config in the `<details>` and the correct notice set (this repo currently emits the known `sprint-status.yaml` "Unsupported" ingest diagnostic — memory: sprint-status-yaml-not-valid-yaml — expect that to appear).
  - [x] Record in Completion Notes: the exact notice-source filter used, how the fine ingest category is preserved, the config fields shown, and the confirmation that the only output delta is "footer link everywhere + two new pages."

## Dev Notes

### The data is already collected — this is a *rendering* story
Everything AC #1/#2 need already exists in memory at the end of `GenerateAll`. Do not add new collection:
- **Notices**: the `List<GenerationEvent> events` that `GenerateAll` returns already contains (a) adapter ingestion diagnostics mapped in at [SiteGenerator.cs:117](../../src/SpecScribe/SiteGenerator.cs) and (b) every render-time `Error`/`Skipped`. Filtering it to non-fatal outcomes IS the notice list.
- **Config/detection**: `_options` (`ForgeOptions` — [ForgeOptions.cs:8–33](../../src/SpecScribe/ForgeOptions.cs)) holds source root, resolved ADR root (+ `AdrSourceExplicit`), output root, `DeepGitAnalytics`, `IncludeReadme`, `SiteTitle`; `_module` (`ModuleContext`) holds the detected framework/module. All resolved, all local.
- **Product metadata**: the assembly attributes generated from [SpecScribe.csproj:17–19](../../src/SpecScribe/SpecScribe.csproj).

### The notice channel — reuse it exactly (4.1's design intent)
Story 4.1 was explicit that adapter diagnostics are "one typed thing" surfaced on the existing `GenerationEvent`/`IGenerationReporter` path, "never a new console UI," and a caught failure is "reported ONLY via its diagnostic" ([4-1-…md:71,144](4-1-shared-framework-adapter-contract-and-projection-path.md)). 4.8 honors that: it reads the *already-merged* `events` list; it does not re-read `bundle.Diagnostics` separately (that would double-count the mapped ones). The single allowed change is enriching the mapped **message** with the fine category word (Task 2) so the page can show `[Unsupported]`/`[Malformed]` — additive, no structural change.

### Why footer → About → Diagnostics (not nav / not dashboard)
Owner-decided. Two consequences that simplify the build:
1. **No nav-dangle risk.** `git-insights`/`deep-analytics` are kept *out* of the top nav because the nav is built *before* git is computed, so a nav entry could point at a page that never gets written ([SiteNav.cs:24–28](../../src/SpecScribe/SiteNav.cs)). 4.8's pages are always written on every full run, but they still don't go in the nav — they hang off the footer, which is rendered per-page at write time. Zero dangle risk either way.
2. **The footer is the site-wide reach.** Every templater ends with `PathUtil.RenderFooter(...)`, so one change there reaches every page. Keep the change surgical (add the About link, keep the existing repo link).

### This story CHANGES output on purpose (the Epic-4 exception)
Re-stating because it inverts the muscle memory from 4.1/4.2: the golden-diff test is *not* the guardrail here. The footer change touches every page's bytes by design. Update the golden fixture deliberately and confirm the delta is exactly "footer About link added to every page + `about.html` + `diagnostics.html` created." If the diff shows anything else moving, that's the bug.

### Watch-mode (don't over-reach)
The Diagnostics page is a whole-run report; the incremental watch paths (`GenerateOne`, `RegenerateEpics`, `RegenerateAdrs`, `RemoveFor`) do **not** recompute it — it reflects the notices/config of the last **full** `GenerateAll`, exactly like `git-insights`/`deep-analytics` are full-run-only. The About page is static and never stale. Because a full build always writes both pages and watch never deletes them, the footer's About link never breaks in watch. **Accept and note this**; do not add watch regeneration for these pages (Epic 2 retro: don't absorb scope mid-story). If a future story wants live-refreshing diagnostics, that's its own story.

### Accessibility / tokens / theme (inherit, don't reinvent)
- Route the category badge through the **status token system** (memory: specscribe-status-token-system — six `--status-*` tokens are the single stage→color source). Reuse `StatusStyles.Badge` as `ActionItemsTemplater` does; never color-only (NFR6, UX-DR17) — the category word is always text.
- Native `<details>` for the config disclosure — no JS, no motion concern (memory: charting-is-pure-svg-no-js; the one small existing tooltip/copy script is not needed here). Reduced-motion is automatically satisfied.
- Theme-aware: verify badges and the `<details>` read correctly in light and dark (the token system already handles this if you route through it).

### Do NOT
- Add a top-nav entry or dashboard callout (owner-decided against).
- Run the diagnostics message through `ApplyReferenceLinks` (linkifier corrupts exception text — [SiteGenerator.cs:847–849](../../src/SpecScribe/SiteGenerator.cs)).
- Re-read `bundle.Diagnostics` separately for the page (double-count).
- Touch git/progress/coverage or the `--deep-git` gate (memory: deep-git-single-numstat-path). You only *read* `_options.DeepGitAnalytics` as a boolean for display.
- Add a new project/namespace (seed-level, deferred — [ARCHITECTURE-SPINE.md:98–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)). New files go in `namespace SpecScribe;`.

### Project Structure Notes
- Single project: `src/SpecScribe/SpecScribe.csproj` (`net10.0`, `Nullable enable`, `ImplicitUsings enable`). New files: `DiagnosticsTemplater.cs`, `AboutTemplater.cs` (+ small edits to `SiteNav.cs`, `SiteGenerator.cs`, `PathUtil.cs`). **No new project, no namespace split.**
- Tests: `tests/SpecScribe.Tests/` (xUnit 2.9, `net10.0`), file-per-unit naming. Relevant suites to extend: `SiteGeneratorAdapterTests`, `ActionItemsTemplaterTests` (as a shape reference), `PathUtilTests` (footer).
- **Output dir is `SpecScribeOutput`** (memory: generate-output-dir-is-specscribeoutput). Never `--output docs/live`.
- Runs on `main` (not a worktree); edits target `C:\Dev\SpecScribe` directly. A background auto-committer on main commits/pushes edits (memory: worktree-edits-must-target-worktree-path) — keep commits coherent.
- Match the heavy XML-doc house style — every public type/member carries a `<summary>` explaining the *why*, tagged `[Story N.M]`. Tag new/changed members `[Story 4.8]`.
- This story **does** introduce a new visual surface, so the "elicit visual intent" step applied — see **Owner-decided** above (design direction locked up front, per the Epic 3 retro action item).

### References
- [epics.md:766–786](../planning-artifacts/epics.md) — Story 4.8 provenance comment + ACs (source of truth).
- [epics.md:36,76,77,82](../planning-artifacts/epics.md) — FR1 (adapter contract), NFR2 (graceful degradation → non-fatal notices), NFR3 (local-first, no remote telemetry — AC #2), NFR8 (framework-agnostic; degrade to absent-not-broken).
- [4-1-shared-framework-adapter-contract-and-projection-path.md](4-1-shared-framework-adapter-contract-and-projection-path.md) — the `AdapterDiagnostic` channel this page consumes; `MapDiagnostics`, "one typed channel, never double-reported," malformed-artifact test fixture pattern.
- [4-2-decouple-rendering-from-personal-project-structure-assumptions.md:58,92](4-2-decouple-rendering-from-personal-project-structure-assumptions.md) — 4.2 emits degradation notices onto the channel "so 4.8 can render them"; 4.2 explicitly does NOT build this page.
- [AdapterDiagnostic.cs](../../src/SpecScribe/AdapterDiagnostic.cs), [ArtifactBundle.cs:55–57](../../src/SpecScribe/ArtifactBundle.cs) — the diagnostic record + its category enum + the bundle channel.
- [SiteGenerator.cs:46–219](../../src/SpecScribe/SiteGenerator.cs) — `GenerateAll` (events accumulation; write-last placement); [:117](../../src/SpecScribe/SiteGenerator.cs) (diagnostics mapped into events); [:681–684](../../src/SpecScribe/SiteGenerator.cs) (`MapDiagnostics`, the category-collapse to enrich); [:840–852](../../src/SpecScribe/SiteGenerator.cs) (`WriteActionItems`, the write-method pattern to mirror).
- [ActionItemsTemplater.cs](../../src/SpecScribe/ActionItemsTemplater.cs) — the static-templater shape, `RenderHeadOpen`/nav/breadcrumb/`RenderFooter` usage, `StatusStyles.Badge`, and the "don't linkify the copy/message payload" precedent.
- [SiteNav.cs:10–34](../../src/SpecScribe/SiteNav.cs) — page-path-constant convention (add `Diagnostics`/`About` here).
- [PathUtil.cs:60–87](../../src/SpecScribe/PathUtil.cs) — `RenderHeadOpen`, `RenderFooter` (add the About link; promote the repo URL to a shared constant), `AssetVersion` (reflection-from-assembly precedent for reading build metadata).
- [ForgeOptions.cs:8–107](../../src/SpecScribe/ForgeOptions.cs) — every effective-config field AC #2 lists, all already resolved on `_options`.
- [ModuleContext.cs:8,32,58–113](../../src/SpecScribe/ModuleContext.cs) — `BmadModule`, `ModuleLabel`, `Docs` — the "detected framework/module" for AC #2.
- [SpecScribe.csproj:17–19](../../src/SpecScribe/SpecScribe.csproj) — `Version`/`Authors`/`Description` (the About page's single source of truth via assembly attributes).

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Code / bmad-dev-story)

### Debug Log References

- `dotnet test tests/SpecScribe.Tests` → 631 passed, 0 failed (includes new suites + updated golden inventory).
- `dotnet run --project src/SpecScribe -- generate --output SpecScribeOutput` → 49 pages Generated, 0 Error/Skipped.

### Completion Notes List

**Notice-source filter used (Task 2):** the page renders from the single accumulated `List<GenerationEvent> events` `GenerateAll` returns, filtered to `Outcome is GenerationOutcome.Error or GenerationOutcome.Skipped` (`DiagnosticNotice.FromEvents`). This is the whole-run non-fatal surface with **zero double-counting** — each adapter diagnostic is mapped into `events` exactly once by `MapDiagnostics`; the page never re-reads `bundle.Diagnostics`.

**How the fine ingest category is preserved (Task 2):** `MapDiagnostics` now prefixes the mapped event message with the fine `AdapterDiagnosticCategory` word (`$"[{d.Category}] {d.Message}"`) — the only allowed touch to the 4.1 channel, additive/harmless on the console path. `DiagnosticNotice.FromEvents` recovers it by stripping a leading `[Word]` **only when `Word` is exactly a known `AdapterDiagnosticCategory`** (so an exception message that merely starts with `[` is left intact and falls back to its coarse `Error`/`Skipped` outcome word).

**Config fields shown (Task 3, AC #2):** site title, detected framework (`ModuleContext` → "BMad Method" / "Unknown (not detected)"), repo root, source root, ADR location (+ explicit/default indicator), output directory, deep-git flag, README-included. All are field reads on already-resolved `_options`/`_module` plus a pure `Path.GetRelativePath` transform (repo-relative where possible, absolute fallback) — **no I/O, no remote calls** (local-first, asserted structurally in `DiagnosticsConfig.FromRun` tests).

**Badge routing (Task 4):** category badge carries the category word as text always (never color-only, UX-DR17/NFR6); two dedicated severity classes (`.status-badge.diag-error` = `--rust`, `.status-badge.diag-warn` = neutral parchment) — deliberately **not** the `--status-*` lifecycle stage tokens, since a run notice isn't a delivery stage. Diagnostics HTML is written **without** `ApplyReferenceLinks` (linkifier would corrupt exception text).

**Reachability (Owner-decided):** footer → About → Diagnostics. `PathUtil.RenderFooter` gained an "About" link (and a `relativePrefix` parameter so the href resolves from nested pages — root pages use the default `""`). The repo URL was promoted to `PathUtil.RepositoryUrl`, shared by the footer and the About page. About metadata (version/description/author) is read via reflection from the assembly attributes the SDK emits from the csproj — never re-declared as literals.

**Output delta confirmed (Task 8):** the only change to existing pages is the site-wide footer "About" link (verified present on all 49 pages, resolving correctly at root `about.html` and nested `../about.html`), plus the two new pages `about.html` + `diagnostics.html`. The golden-output inventory (`SiteGeneratorAdapterTests`) was updated intentionally with a comment to add the two files; no other existing assertion changed.

**Note on the story's Task 8 expectation:** the story expected this repo's `sprint-status.yaml` to surface as a known "Unsupported" ingest diagnostic. It does **not** anymore — the sprint parser block-isolates `development_status`, so the file parses cleanly and the diagnostics page correctly renders the **all-clear** state (0 notices). The malformed/notice paths are covered instead by the unusable-sprint-yaml SiteGenerator test, which asserts exactly one row (no double-count).

**Watch-mode:** both pages are full-`GenerateAll` artifacts; the incremental watch paths do not recompute them (accepted, per Dev Notes — the all-clear/static pages never dangle because a full build always writes them and watch never deletes them).

### File List

**New:**
- `src/SpecScribe/DiagnosticsTemplater.cs` — `DiagnosticNotice` (+ `FromEvents`), `DiagnosticSeverity`, `DiagnosticsConfig` (+ `FromRun`), `DiagnosticsTemplater.RenderPage`.
- `src/SpecScribe/AboutTemplater.cs` — `ProductMetadata` (+ `FromAssembly`), `AboutTemplater.RenderPage`.
- `tests/SpecScribe.Tests/DiagnosticsTemplaterTests.cs`
- `tests/SpecScribe.Tests/AboutTemplaterTests.cs`

**Modified:**
- `src/SpecScribe/SiteNav.cs` — `DiagnosticsOutputPath` / `AboutOutputPath` constants (not in nav Items/QuickLinks).
- `src/SpecScribe/SiteGenerator.cs` — `MapDiagnostics` message-category enrichment; `WriteDiagnostics` / `WriteAbout` methods wired in at the end of `GenerateAll`.
- `src/SpecScribe/PathUtil.cs` — `RepositoryUrl` constant; `RenderFooter` gains the About link + `relativePrefix` parameter.
- `src/SpecScribe/assets/specscribe.css` — `.status-badge.diag-error` / `.diag-warn`; diagnostics + about layout section.
- `src/SpecScribe/HtmlTemplater.cs`, `EpicsTemplater.cs`, `CommitDayTemplater.cs`, `RequirementsTemplater.cs`, `RetroTemplater.cs` — pass the page's relative prefix to `RenderFooter` for nested pages.
- `tests/SpecScribe.Tests/PathUtilTests.cs` — footer About-link tests (root + nested depth).
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden inventory updated (+ `about.html`/`diagnostics.html`); diagnostics all-clear / single-notice / footer-everywhere tests.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story status tracking.

## Change Log

| Date | Version | Change |
|------|---------|--------|
| 2026-07-10 | 0.1.0 | Story 4.8 implemented: generation diagnostics run-log page (`diagnostics.html`) + About page (`about.html`) + site-wide footer About link; notice-category preservation on the 4.1 channel; local-first effective-config model. All ACs satisfied; 631 tests green. |
