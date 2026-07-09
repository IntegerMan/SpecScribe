---
baseline_commit: 80443ba34aa8ef068e7f233663ae9910e66c2c18
---

# Story 3.3: Agent and Workflow Structure Coverage Insights

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a contributor,
I want visibility into planning artifact coverage and freshness,
so that I can identify missing or stale process artifacts quickly.

## Acceptance Criteria

1. **Given** canonical planning and workflow files exist **When** insights are computed **Then** the portal reports discovered artifact families and key missing families **And** freshness or staleness indicators are shown clearly. [Source: epics.md#Story 3.3; PRD FR-11]
2. **Given** memlog and related journals are present **When** structure insights run **Then** memlog data is used as optional enrichment **And** source-artifact-derived insights remain primary. [Source: epics.md#Story 3.3; PRD FR-11]

## Tasks / Subtasks

- [x] Task 1: Add the pure artifact-coverage model + builder (AC: #1, #2)
  - [x] Subtask 1.1: Create `src/SpecScribe/ArtifactCoverage.cs` with two records and a static `Build` — mirror the shape of [WorkInventory.cs](../../src/SpecScribe/WorkInventory.cs) (pure `Build` over already-gathered inputs, an `Empty` singleton, an `IsEmpty` flag callers use to omit the section). Define:
    - `ArtifactFamily(string Label, string ConceptIconKey, bool Present, DateOnly? LastModified, string? SourcePath, DateOnly? MemlogUpdated)` — one canonical family row. `ConceptIconKey` is the exact string [Icons.ForConcept](../../src/SpecScribe/Icons.cs:31) already curates ("PRD", "Architecture", "Product Brief", "UX Design", "Epics", "Requirements", "Spec") so the panel reuses the existing glyphs with no new icon vocabulary.
    - `ArtifactCoverage { IReadOnlyList<ArtifactFamily> Families; bool IsEmpty; static readonly Empty; }` plus derived helpers `PresentCount`, `MissingCount`, and `StaleCount` for the panel headline.
  - [x] Subtask 1.2: Define the canonical family list in ONE place inside `ArtifactCoverage`, keyed off filename constants from [ModuleContext.WellKnownDocs](../../src/SpecScribe/ModuleContext.cs:76) rather than re-hard-coding strings (single source of truth — the same discipline `SiteNav`/`ModuleContext`/`Icons` already follow). The V1 "Core + Orchestration" set per [PRD FR-11 consequences](../../_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md:143): **PRD** (`prd.md`), **Product Brief** (`brief.md`), **Architecture** (`ARCHITECTURE-SPINE.md`), **UX** (`DESIGN.md` OR `EXPERIENCE.md` — present if either exists), **Spec Kernel** (`SPEC.md` under a `specs/` path), **Epics** (`epics.md`), **Stories** (≥1 file matching `implementation-artifacts/<n>-<n>-*.md`), **Requirements** (`requirements.md` or `requirements-catalog.md`). Match each family by filename **anywhere in the source tree** — folder layout varies and Epic 4 will generalize this (same rationale as [ModuleContext.cs:89-91](../../src/SpecScribe/ModuleContext.cs)). Add an XML-doc comment stating this list is the coverage seam Epic 4 generalizes, so a future adapter swaps the family set, not the panel.
  - [x] Subtask 1.3: `Build` signature takes only already-resolved inputs — NO disk access in this method (keeps it unit-testable without a repo, exactly like `ProgressCalculator.Compute` and `WorkInventory.Build`): `Build(IReadOnlyList<string> sourceRelativePaths, IReadOnlyDictionary<string, DateOnly> lastModifiedByPath, IReadOnlyDictionary<string, DateOnly> memlogUpdatedByFamilyLabel, DateOnly today)`. For each canonical family, resolve the first matching normalized source path (deterministic first-match, like `WorkInventory`'s "first match wins"), set `Present`, look up its `LastModified`, and attach any `MemlogUpdated` enrichment. Normalize paths through [PathUtil.NormalizeSlashes](../../src/SpecScribe/PathUtil.cs) and compare case-insensitively (`OrdinalIgnoreCase`) — Windows/Git-Bash path-case parity.
  - [x] Subtask 1.4: Staleness is derived, not stored: add `bool IsStale(DateOnly today)` on `ArtifactFamily` = `Present && LastModified is {} d && today.DayNumber - d.DayNumber > StalenessThresholdDays`. Define `public const int StalenessThresholdDays = 30;` with a comment that this is a sensible default the settings surface may later expose ([settings-and-signals.md](../../_bmad-output/specs/spec-specscribe/settings-and-signals.md) reserves insight controls; do NOT build settings plumbing here — that parity work is out of scope for this story). A missing `LastModified` is never "stale" (unknown ≠ old).

- [x] Task 2: Gather coverage inputs in the generator and cache the model (AC: #1, #2)
  - [x] Subtask 2.1: In [SiteGenerator.GenerateAll](../../src/SpecScribe/SiteGenerator.cs:38), after `_module`/`sourceRelatives` are available (alongside where `_sprint` is parsed, ~line 61-64), compute coverage once and cache it in a new `private ArtifactCoverage _coverage = ArtifactCoverage.Empty;` field. Wrap the whole gather-and-build in try/catch → `ArtifactCoverage.Empty` (AD-4: insight providers are additive, non-blocking, and never own baseline success [Source: [ARCHITECTURE-SPINE.md#AD-4](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:58)]; NFR2: never throw). Do this in a small private helper `BuildArtifactCoverage(IReadOnlyList<string> sourceRelatives)` next to `BuildNav`.
  - [x] Subtask 2.2: The generator (not the pure `Build`) owns the disk reads. For each canonical family's matched source path, read `File.GetLastWriteTime(fullPath)` and convert to `DateOnly` for the `lastModifiedByPath` map. Reuse `ToSourceRelative`/`_options.SourceRoot` to map source-relative ↔ full path. Guard each stat individually (a single unreadable file degrades that one family's freshness to `null`, never aborts the pass — partial data beats no data, per AD-4).
  - [x] Subtask 2.3: Memlog enrichment (AC #2) — `.memlog.md` files are **excluded** from `EnumerateSourceFiles` because [IsIgnored](../../src/SpecScribe/SiteGenerator.cs:875) drops every dotfile. Discover them separately, the same way [SprintSourcePath](../../src/SpecScribe/SiteGenerator.cs:566) scans for the `.yaml` outside the `*.md` set: `Directory.EnumerateFiles(_options.SourceRoot, ".memlog.md", SearchOption.AllDirectories)`. Parse only the `updated:` frontmatter timestamp (a single regex line-read like [ForgeOptions.ReadProjectName](../../src/SpecScribe/ForgeOptions.cs:107), NOT a full YAML parse) and associate each memlog with the family whose canonical file shares its directory subtree (e.g. `prds/.memlog.md` → PRD, `specs/spec-specscribe/.memlog.md` → Spec Kernel). Feed `memlogUpdatedByFamilyLabel`. **This must be strictly additive**: if zero memlogs are found, `Build` receives an empty map and every primary Present/LastModified value is unchanged — AC #2's "source-artifact-derived insights remain primary."
  - [x] Subtask 2.4: Thread `_coverage` into the index render. Add a trailing optional parameter `ArtifactCoverage? coverage = null` to [HtmlTemplater.RenderIndex](../../src/SpecScribe/HtmlTemplater.cs:91) (keep it last + defaulted so no other call site breaks and a null coverage renders byte-for-byte the current dashboard), and pass `_coverage` from [WriteIndex](../../src/SpecScribe/SiteGenerator.cs:527).

- [x] Task 3: Render the coverage panel on the dashboard (AC: #1)
  - [x] Subtask 3.1: Add a pure builder `Charts.ArtifactCoveragePanel(ArtifactCoverage coverage, DateOnly today)` in [Charts.cs](../../src/SpecScribe/Charts.cs) — inline HTML + CSS only, **no JS** (`Charts.cs:6-8` class-doc convention; not an oversight — see [charting-is-pure-svg-no-js]). One row per family: `Icons.ForConcept(family.ConceptIconKey)` glyph + label, a present/missing indicator, and a freshness sub-line. For a present family show `"Updated {Charts.DReadable(LastModified)}"` using the existing **invariant** date helper [Charts.DReadable](../../src/SpecScribe/Charts.cs:595) — do NOT introduce a culture-sensitive date format (the heatmap's non-invariant date formatting is already logged tech debt in [deferred-work.md](../../_bmad-output/implementation-artifacts/deferred-work.md); don't add a second instance). Present families flagged `IsStale(today)` get a "Stale" chip; missing families get a "Missing" chip and a dimmed row. Reuse `Charts.Plural` for the "N of M families present" headline.
  - [x] Subtask 3.2: Call it from [AppendDashboard](../../src/SpecScribe/HtmlTemplater.cs:210) as a new `chart-panel` (title e.g. "Planning Coverage"). Place it beside the git-facing panels — a natural home is the existing `chart-row` (line 260-269) next to "Commit Activity", or as its own panel after it. Render the panel **only** `if (!coverage.IsEmpty)` (graceful omission, Story 1.1) so a repo with no recognized families keeps today's dashboard unchanged. Pass `DateOnly.FromDateTime(DateTime.Now)` as `today` (same "now" convention the heatmap uses at [Charts.cs:474](../../src/SpecScribe/Charts.cs)).
  - [x] Subtask 3.3: Add the panel's CSS to [assets/specscribe.css](../../src/SpecScribe/assets/specscribe.css). Present/missing/stale is a **coverage** axis, NOT a lifecycle stage — do **not** route it through the `--status-*` tokens (those are the single stage→color source; see [specscribe-status-token-system] and the CSS comment at [specscribe.css:953](../../src/SpecScribe/assets/specscribe.css) that keeps non-lifecycle concepts off the status tokens). Use neutral palette tones already defined (e.g. `--ink-light` for the dimmed "missing" row, an existing warn/amber tone for "stale"). Family glyphs inherit `currentColor` via `.ss-icon` — no per-row hex.

- [x] Task 4: Test coverage (AC: #1, #2)
  - [x] Subtask 4.1: Add `tests/SpecScribe.Tests/ArtifactCoverageTests.cs` (pure `Build`, no disk — mirror [WorkInventoryTests.cs](../../tests/SpecScribe.Tests/WorkInventoryTests.cs) and [RequirementsAndProgressTests.cs](../../tests/SpecScribe.Tests/RequirementsAndProgressTests.cs)): all families present; some families missing (assert exact `Present=false` set); a story family present only when ≥1 `implementation-artifacts/<n>-<n>-*.md` path is supplied; UX family present when EITHER `DESIGN.md` or `EXPERIENCE.md` exists; unknown/custom source paths are ignored and never throw (AC #2 "unknown or custom files do not cause generation failure" [Source: prd.md:147]); staleness boundary — a family last modified exactly `StalenessThresholdDays` days ago is NOT stale, one day older IS.
  - [x] Subtask 4.2: Memlog-enrichment tests: with a `memlogUpdatedByFamilyLabel` entry the matching family carries `MemlogUpdated`; with an **empty** memlog map, every family's primary `Present`/`LastModified` is byte-for-byte identical to the no-memlog case (proves enrichment is additive/secondary — AC #2).
  - [x] Subtask 4.3: `HtmlTemplaterTests.cs` coverage ([HtmlTemplaterTests.cs](../../tests/SpecScribe.Tests/HtmlTemplaterTests.cs)): the panel renders present families with their `DReadable` date and a "Missing" chip for absent ones when `RenderIndex` gets a populated `ArtifactCoverage`; the panel is omitted entirely (dashboard unchanged) when coverage is `ArtifactCoverage.Empty`/null.
  - [x] Subtask 4.4: Optional guard in [StylesheetTests.cs](../../tests/SpecScribe.Tests/StylesheetTests.cs) asserting the new coverage panel CSS class exists (matches the existing "cheap guard so a seam can't be silently deleted" pattern) — only if you add a distinctively named class worth guarding.

### Review Findings

- [x] [Review][Patch] Watch-mode/incremental regen paths leave the coverage panel stale [src/SpecScribe/SiteGenerator.cs:72,186,207,246,253,284] — `_coverage` is computed once in `GenerateAll` and cached; `GenerateOne`, `RemoveFor`, `RegenerateEpics`, and `RegenerateAdrs` all call `WriteIndex` without recomputing it, so a newly created or edited planning artifact doesn't move the Planning Artifacts panel's Present/Missing/freshness state until the next full `generate`. **Fixed**: new private `RefreshCoverage()` helper, called by all four incremental paths before their `WriteIndex`. Verified with new `SiteGeneratorCoverageTests.GenerateOne_RefreshesCoveragePanelWithoutAFullRegenerate` / `RegenerateEpics_RefreshesCoveragePanelForNewlyAddedStoryArtifacts`.
- [x] [Review][Patch] `ResolveFamilyHref` can produce a broken link on a present family [src/SpecScribe/SiteGenerator.cs:919-950] — the href is built purely from the matched source path, without checking `_docs` (populated only on successful page conversion) the way the sibling `structure.html` tree explicitly does. If `MarkdownConverter.Convert` throws or the file is locked for one canonical doc, its coverage card still renders "Present" with a whole-card link to a page that was never written. The hardcoded `"Epics" or "Stories" => SiteNav.EpicsOutputPath` branch has the same gap if `GenerateEpicsInternal` fails before writing `epics.html`. **Fixed**: `ResolveFamilyHref` now checks `_docs`/`_epicsModel`/`_requirements` before returning an href (degrading to non-linked text otherwise), and the whole `_coverage` build was moved from before to after epics/page generation in `GenerateAll` so those fields are actually populated when it runs. Caught and fixed a path-format bug during verification (`_docs` keys use raw OS separators vs. the normalized-slash `SourcePath` — now compared via `PathUtil.NormalizeSlashes`). Verified via `dotnet run -- generate` on the real repo (all 8 families link correctly) and `SiteGeneratorCoverageTests.GenerateAll_PresentFamilyCardLinksToTheActualGeneratedPage`.
- [x] [Review][Patch] `ArtifactCoverage.Build`'s "deterministic first-match" isn't actually guaranteed [src/SpecScribe/ArtifactCoverage.cs:140-142] — `normalized.FirstOrDefault(spec.Matches)` depends on `sourceRelativePaths`' incoming order, which callers don't sort; two files matching the same family (e.g. two `prd.md`) can resolve to a different "canonical" match across runs/platforms, contradicting the doc comment's claim. **Fixed**: new `SelectCanonicalMatch` orders candidates ordinally and (combined with the next fix) picks the freshest by mtime, with an ordinal tiebreak — stable regardless of input order. Verified by new `Build_DuplicateFamilyFileResolvesDeterministicallyRegardlessOfInputOrder`.
- [x] [Review][Patch] UX family freshness only reflects whichever of DESIGN.md/EXPERIENCE.md matches first [src/SpecScribe/ArtifactCoverage.cs:94-96] — the OR-predicate stops at the first match, so when both exist, staleness/freshness/href for the one not matched is silently dropped. **Fixed**: new `ArtifactCoverage.AllCandidatePaths` lets the generator stat every candidate matching any family (not just the provisional first match); `SelectCanonicalMatch` then picks whichever candidate has the most recent mtime. Verified by new `Build_UxFamilyWithBothFilesPicksTheMoreRecentlyModifiedOne` / `AllCandidatePaths_ReturnsEveryMatchNotJustTheWinner`.
- [x] [Review][Patch] Root-level `.memlog.md` enriches every family, not just related ones [src/SpecScribe/SiteGenerator.cs:990-994] — `ml.Dir.Length == 0` treats a root journal as an ancestor of every family. A family with no closer memlog silently inherits the root journal's date, and the tooltip ("Decision journal (.memlog) updated …") doesn't disclose it's a root-level fallback rather than family-specific enrichment. **Fixed**: `BuildMemlogMap` now only lets a root-level memlog blanket-apply when it's the *only* memlog in the tree (the project's one journal); once any nested, family-scoped memlog exists, the root one no longer overrides families with no closer match.
- [x] [Review][Patch] Coverage cards create dead keyboard focus stops [src/SpecScribe/Charts.cs — `ArtifactCoveragePanel`] — a present family with no `Href`, and every missing family, gets `tabindex="0"` purely to expose the hover/focus tooltip; pressing Enter/Space does nothing, and missing cards additionally nest the `BmadCommands.InlineGuidance` control inside that already-focusable div, doubling tab stops with no `role` marking the outer element as inert. **Fixed**: removed `tabindex="0"` from both non-interactive card divs; the real interactive control on a missing card (`InlineGuidance`) is now the only tab stop. Verified via `dotnet run -- generate` — zero `coverage-card` elements carry `tabindex` in the generated output.
- [x] [Review][Patch] Coverage meter progressbar lacks `aria-valuetext` [src/SpecScribe/Charts.cs — `CoverageMeter`] — `role="progressbar"` has `aria-valuenow`/min/max and a static `aria-label`, but nothing ties it to the adjacent visible percentage text for screen readers. **Fixed**: added `aria-valuetext="{pct}% ({present} of {total} present)"`. Verified in generated output.
- [x] [Review][Patch] `IsStoryArtifact` regex accepts an empty title segment [src/SpecScribe/ArtifactCoverage.cs:175] — `^\d+-\d+-.*\.md$` matches a literal `1-2-.md`; no test or guard excludes a malformed/empty-title filename. **Fixed**: tightened to `^\d+-\d+-.+\.md$` (require ≥1 title char). Verified by new `Build_StoryFilenameWithEmptyTitleSegmentIsNotAStoryArtifact`.
- [x] [Review][Patch] `FamilyAccentClass` and `ArtifactCoverage.Specs` are two hand-maintained label lists with no link between them [src/SpecScribe/Charts.cs, src/SpecScribe/ArtifactCoverage.cs] — an unmatched label in the accent-class switch silently falls through to a default color instead of failing loudly, so a future family rename/add can go silently miscolored. **Fixed** with a test-level seam guard rather than a runtime change (consistent with the "never throw" insight-provider discipline): new `HtmlTemplaterTests.RenderIndex_EveryCanonicalFamilyLabelGetsItsExpectedAccentClass` derives its label set from `ArtifactCoverage.Build` (not a hardcoded list), so a family added to `Specs` without a matching accent-class entry fails the test's own count assertion.
- [x] [Review][Defer] No direct test for `SiteGenerator.BuildMemlogMap`'s ancestor-matching logic [src/SpecScribe/SiteGenerator.cs:957-999] — deferred, pre-existing test-coverage gap; the pure `ArtifactCoverage.Build` memlog wiring is well tested with hand-built maps, but the directory-prefix/closest-ancestor selection itself (the one genuinely tricky piece) has zero coverage.
- [x] [Review][Defer] `HtmlTemplaterTests.RenderIndex_RendersPlanningCoveragePanelWithPresentDateAndMissingChip` isn't fully hermetic [tests/SpecScribe.Tests/HtmlTemplaterTests.cs] — deferred, test-quality nit; `AppendDashboard` recomputes staleness against real `DateTime.Now` rather than the fixture's fixed `today`, so the test only passes because wall-clock time hasn't crossed the 30-day threshold, not because the date is pinned.

<details><summary>Dismissed as noise (6)</summary>

- `BuildArtifactCoverage`'s catch blocks swallow failures without a `GenerationEvent` — matches the established insight-provider convention (`GitMetrics.cs` also silently swallows via bare `catch` with no event), and the Dev Notes explicitly direct preserving that exact contract. By design, not a deviation.
- Two independent `DateTime.Now` reads (`SiteGenerator.BuildArtifactCoverage`'s clamp-today vs `HtmlTemplater.AppendDashboard`'s display-today) — real, but the two calls land milliseconds apart in the same generation pass serving different purposes (clamp vs. display); divergence requires an exact midnight boundary between them.
- `MemlogUpdatedPattern` matches `updated:` anywhere in the file rather than only inside frontmatter — matches the established single-regex-line-read convention (`ForgeOptions.ReadProjectName` does the same, and the Dev Notes explicitly cite it as the pattern to imitate).
- Family filename matching (anywhere-in-tree) untested against realistic collisions — an explicitly stated design choice from the story and `ModuleContext`'s existing convention, not a new deviation.
- Coverage headline text ("N of M families present") was superseded by the `CoverageMeter` redesign — disclosed in the story's own Change Log as an intentional review-driven pass.
- `.coverage-meter-fill` reuses the `--moss-light` base-palette variable — technically satisfies the "no `--status-*` token" constraint; a minor visual-echo observation, not a violation.

</details>

## Dev Notes

- **This is CAP-4's second half.** The spec kernel's memlog names the exact deliverable: "dashboard shows baseline git pulse **and artifact family coverage**, while insight failures degrade gracefully and do not block generation" [Source: [.memlog.md](../../_bmad-output/specs/spec-specscribe/.memlog.md):14, CAP-4 Success]. Story 3.1 delivered the git-pulse half; this story delivers the artifact-family-coverage half. Build the panel as a peer of the git-pulse panel, not a rewrite of the dashboard.
- **Reuse the existing classifier seams; don't invent parallel ones.** Family filenames already live in [ModuleContext.WellKnownDocs](../../src/SpecScribe/ModuleContext.cs:76) (`Prd`, `ArchitectureSpine`, `Brief`, `UxDesign`, `UxExperience`) — key off those constants. Family glyphs already live in [Icons.ForConcept](../../src/SpecScribe/Icons.cs:31) under the labels "PRD"/"Architecture"/"Product Brief"/"UX Design"/"Epics"/"Requirements"/"Spec" — pass those exact keys. This is the project's "one classifier / one seam" discipline (stated in the `Icons`/`StatusStyles`/`ModuleContext` class docs); a second copy of the filename or glyph vocabulary is exactly the drift those seams exist to prevent.
- **Insight modules are never-throw by design.** Preserve the [GitMetrics](../../src/SpecScribe/GitMetrics.cs) contract: any failure (missing files, IO, permission, malformed frontmatter) degrades to `ArtifactCoverage.Empty` → the panel omits, generation still succeeds. This is AD-4 ("Optional insight providers may enrich output but never own baseline success" — it explicitly binds "agent-file structure signals") [Source: [ARCHITECTURE-SPINE.md#AD-4](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:58)] and NFR2 (graceful degradation, never a hard failure on partial/unknown input).
- **Keep IO in the generator, keep `Build` pure.** `GitMetrics` shells out but `ParseLog` is a pure, unit-tested helper fed raw text; `ProgressCalculator.Compute` and `WorkInventory.Build` are pure over already-parsed inputs. Follow the same split: `SiteGenerator` reads `File.GetLastWriteTime` and scans for memlogs; `ArtifactCoverage.Build` is a pure function over (paths, timestamp map, memlog map, today) so every coverage/freshness/staleness rule is testable without a repo on disk.
- **Freshness signal = source-file last-write-time (best effort).** It is the deterministic, dependency-free signal available without extra git calls. Its known limitation: a fresh `git clone` resets every mtime to clone time, so freshness is only meaningful in the primary local-editing use case (a maintainer working the repo) — which is exactly SpecScribe's target user (solo OSS maintainer editing in browser + VS Code [Source: [EXPERIENCE.md:244](../../_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md)]). Do NOT reach for per-file `git log` dates here: that means another shelled `git` call under the 3s `RunGit` budget, and deep/opt-in git analytics are deliberately Story 3.2's scope, not this one. The memlog `updated:` timestamp (Subtask 2.3) is the more authoritative "last decision recorded" enrichment for families that have a memlog — hence memlog is the *secondary* signal, source mtime the *primary* one, satisfying AC #2's ordering.
- **Memlog is enrichment, never the primary source.** [PRD FR-11 consequences](../../_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md:143-146): "Primary insight summaries are derivable from source artifacts even when memlog files are absent. Memlog and related journals are consumed only as secondary enrichment when available." Concretely: coverage (present/missing) and freshness (mtime) come 100% from source artifacts; memlog adds only an optional "last recorded" date on a family. A repo with no memlogs must produce an identical primary coverage picture — Subtask 4.2 tests exactly this.
- **Present/missing/stale is a different axis from the lifecycle status tokens.** The six `--status-*` tokens are the single source of truth for *lifecycle* stage color (pending→done) and "planned work must never read as finished" [Source: [StatusStyles.cs:3-5](../../src/SpecScribe/StatusStyles.cs), [specscribe-status-token-system]]. Coverage is orthogonal — a "present, stale PRD" is not a lifecycle stage. Style it with neutral palette tones, mirroring the CSS discipline at [specscribe.css:953](../../src/SpecScribe/assets/specscribe.css) ("keywords are grammar, not lifecycle stages, so they must never [use `--status-*`]"). Do not add new `--status-*`-shaped tokens.
- **Scope boundaries (do not creep into sibling Epic 3 stories):**
  - Interactive/expandable tree views of the directory or artifact structure are **Story 3.4**, not this one. This story is a flat coverage/freshness panel, not a tree. "Workflow output trees when present" (FR-11) here means *recognizing that the `_bmad-output` family set is present*, not walking/rendering a directory tree.
  - Deeper/opt-in git analytics (hotspots, coupling) are **Story 3.2**. Add no git calls.
  - Motion/animation polish for insight visuals is **Story 3.5**. Keep the panel static (reduced-motion is then trivially satisfied, matching every existing chart).
  - Settings/CLI toggles for insight coverage are a later configurability concern ([settings-and-signals.md](../../_bmad-output/specs/spec-specscribe/settings-and-signals.md)); this story ships the always-on baseline panel with a hard-coded staleness default. No settings plumbing.
- **No `architecture.md` exists** for this project. The governing docs are the spec kernel: [SPEC.md](../../_bmad-output/specs/spec-specscribe/SPEC.md), [ARCHITECTURE-SPINE.md](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md), and companions. Per the spine's "Seed, Not Invariant" section, the current monolithic `src/SpecScribe` project is intentional — do **not** introduce the aspirational `IInsightProvider`/`SpecScribe.Core` package split for this story [Source: [ARCHITECTURE-SPINE.md:98-100](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)]. Add `ArtifactCoverage.cs` in place, matching the shape of the existing sibling files (`WorkInventory`, `ProgressCalculator`, `GitMetrics`).
- **Windows/encoding gotchas already solved — reuse them.** Read any file via [MarkdownConverter.ReadAllTextShared](../../src/SpecScribe/MarkdownConverter.cs) (shared-read, no lock side effects on a repo being edited — the watch-mode contract). Use `CultureInfo.InvariantCulture` for any date parse/format (the heatmap/`GitMetrics` do this to avoid non-Gregorian-calendar corruption on th-TH/fa-IR hosts); `Charts.DReadable`/`Charts.D` already are invariant — reuse them rather than formatting dates inline.

### Project Structure Notes

- New files: `src/SpecScribe/ArtifactCoverage.cs` (model + pure builder) and `tests/SpecScribe.Tests/ArtifactCoverageTests.cs`. Everything else is an in-place extension of already-integrated code: `SiteGenerator.cs` (gather inputs + cache + wire), `Charts.cs` (panel renderer), `HtmlTemplater.cs` (`RenderIndex` param + `AppendDashboard` call), `assets/specscribe.css` (panel styles), and the existing `HtmlTemplaterTests.cs`/`StylesheetTests.cs`. This matches the single-project layout every prior Epic 1–3 story has extended — no new folders, no new project.
- The default generate output directory is `SpecScribeOutput` (never `docs/live`, which is vestigial/gitignored) [Source: [generate-output-dir-is-specscribeoutput]]. The live `docs/live/specscribe.css` is a stale copy — edit only `src/SpecScribe/assets/specscribe.css` (the embedded resource; `StylesheetTests` loads it from the assembly manifest).
- No conflict detected between epics.md's AC wording and PRD FR-11 — both specify "discovered artifact families and key missing families," "freshness/staleness," and "memlog as optional/secondary enrichment with source-derived insights primary."

### References

- [Source: [epics.md#Story 3.3 (lines 439-457)](../../_bmad-output/planning-artifacts/epics.md)] — user story + both acceptance criteria; Epic 3 goal and FRs covered (FR9-FR11, FR14).
- [Source: [prd.md#FR-11 (lines 139-148)](../../_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md)] — "Agent-file structure insights": the V1 Core + Orchestration canonical family policy, "discovered artifact families and missing expected artifacts," source-first / memlog-secondary ordering, and "unknown or custom files do not cause generation failure."
- [Source: [prd.md line 205 (SM-4)](../../_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md)] — success metric this story serves: "Users can answer at least three common status questions from dashboard + insights without opening raw markdown." Validates FR-9 and FR-11.
- [Source: [prd.md line 188 (MVP in-scope)](../../_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md)] — "Initial artifact/agent-file insight pass focused on structure and completeness using Core + Orchestration policy, with memlog as secondary enrichment."
- [Source: [.memlog.md CAP-4 (lines 13-14)](../../_bmad-output/specs/spec-specscribe/.memlog.md)] — "artifact family coverage" named as the CAP-4 dashboard deliverable alongside git pulse; insight failures degrade gracefully.
- [Source: [ARCHITECTURE-SPINE.md#AD-4 (lines 58-64)](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — insight providers (agent-file structure signals named explicitly) are additive, non-blocking, never own baseline success.
- [Source: [ARCHITECTURE-SPINE.md#Seed, Not Invariant (lines 98-100)](../../_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md)] — monolithic implementation is intentional; don't force the `IInsightProvider`/`SpecScribe.Core` split in this story.
- [Source: [ModuleContext.cs:76-99](../../src/SpecScribe/ModuleContext.cs)] — `WellKnownDocs` filename constants + the "matched by filename anywhere in the source tree; Epic 4 will generalize" convention to reuse for the family list.
- [Source: [Icons.cs:31-57](../../src/SpecScribe/Icons.cs)] — `ForConcept` glyph keys ("PRD"/"Architecture"/"Product Brief"/"UX Design"/"Epics"/"Requirements"/"Spec") to reuse; unknown key → empty string (graceful).
- [Source: [Charts.cs:6-8, 595, 632](../../src/SpecScribe/Charts.cs)] — pure-SVG/HTML-no-JS convention; invariant `DReadable` date helper; `Plural` for count labels.
- [Source: [WorkInventory.cs](../../src/SpecScribe/WorkInventory.cs)] — the model+`Build`+`Empty`+`IsEmpty` shape to mirror for `ArtifactCoverage`; deterministic first-match; NFR2 never-throw framing.
- [Source: [ProgressCalculator.cs](../../src/SpecScribe/ProgressCalculator.cs)] — the pure-`Compute`-over-parsed-inputs pattern (IO stays in the caller).
- [Source: [SiteGenerator.cs:38-64 (GenerateAll), :527-533 (WriteIndex), :566-570 (SprintSourcePath), :697-703 (EnumerateSourceFiles), :875-882 (IsIgnored)](../../src/SpecScribe/SiteGenerator.cs)] — where to compute/cache `_coverage`, how to scan for dot-prefixed `.memlog.md` outside the `*.md` set, and how the index render is wired.
- [Source: [ForgeOptions.cs:101-122](../../src/SpecScribe/ForgeOptions.cs)] — the single-line frontmatter regex-read pattern to imitate for the memlog `updated:` field (no full YAML parser).
- [Source: [HtmlTemplater.cs:91 (RenderIndex), :210-285 (AppendDashboard), :253-269 (chart-panel/chart-row placement), :225-228 (git-pulse fallback pattern to mirror)](../../src/SpecScribe/HtmlTemplater.cs)] — add the optional `coverage` param and the new panel here.
- [Source: [StatusStyles.cs:3-5](../../src/SpecScribe/StatusStyles.cs) / [specscribe.css:34-40, 953](../../src/SpecScribe/assets/specscribe.css)] — the `--status-*` lifecycle-token system coverage must NOT co-opt; neutral tones for present/missing/stale.
- [Source: [3-1-baseline-git-pulse-insights-on-dashboard.md](../../_bmad-output/implementation-artifacts/3-1-baseline-git-pulse-insights-on-dashboard.md)] — the sibling git-pulse insight story; same panel-on-dashboard, never-throw, pure-render pattern this story parallels.
- [Source: [deferred-work.md](../../_bmad-output/implementation-artifacts/deferred-work.md)] — prior review flagged non-invariant heatmap date formatting as tech debt; use `Charts.DReadable` (invariant) for all coverage dates, don't repeat it.

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 (Claude Code / bmad-dev-story)

### Debug Log References

- `dotnet test` (full suite): 480 passed, 1 failed. The single failure —
  `HtmlTemplaterTests.RenderIndex_RendersDeepAnalyticsPanelDistinctlyWhenDeepGitPopulated` — is **NOT caused by
  this story**. It is a stale assertion belonging to Story 3.2: the shared working tree contains uncommitted
  3.2 rework (new `DeepAnalyticsTemplater.cs`, `SiteNav.cs` changes) that moved deep analytics from an inline
  dashboard panel to a dedicated page and removed the `deep-git-panel`, without updating that test. Per user
  decision, it is left for Story 3.2 to reconcile. Every other test (including all Story 3.3 tests) passes:
  `dotnet test --filter FullyQualifiedName!=...RenderIndex_RendersDeepAnalyticsPanelDistinctlyWhenDeepGitPopulated`
  → **480 passed, 0 failed**.
- End-to-end: `dotnet run --project src/SpecScribe -- generate` renders the "Planning Coverage" panel on the
  dashboard with the headline "8 of 8 families present", per-family "Present" chips, invariant DReadable
  freshness dates, and additive memlog "journal" enrichment only on families that have a `.memlog.md`.

### Completion Notes List

- **CAP-4's second half delivered as a peer of the git-pulse panel.** New always-on "Planning Coverage" panel
  on the dashboard reporting discovered artifact families and key missing families, with freshness/staleness
  indicators (AC #1).
- **Source-derived primary, memlog secondary (AC #2).** Coverage (present/missing) and freshness (source-file
  mtime) come 100% from source artifacts; the memlog `updated:` date is strictly-additive enrichment. Unit test
  `Build_MemlogIsStrictlyAdditive_PrimaryCoverageIdenticalWithOrWithoutMemlog` proves the primary picture is
  byte-for-byte identical with or without memlogs.
- **Pure/IO split preserved.** `ArtifactCoverage.Build` is pure over (paths, mtime map, memlog map, today) —
  fully unit-testable without a repo; `SiteGenerator` owns all disk reads (mtimes + `.memlog.md` discovery) and
  degrades any failure to `ArtifactCoverage.Empty` (AD-4 / NFR2 never-throw).
- **Reused existing seams, invented none.** Family filenames key off `ModuleContext.WellKnownDocs`; glyphs reuse
  `Icons.ForConcept`; dates use the invariant `Charts.DReadable`; the panel is pure HTML+CSS, no JS.
- **Coverage axis ≠ lifecycle axis.** Present/missing/stale is styled with neutral base-palette tones
  (`--ink-light`, `--rust`/`--rust-light`), never the `--status-*` lifecycle tokens.
- **Family list is the Epic-4 seam.** The canonical family set lives in one place in `ArtifactCoverage` with an
  XML-doc note that a future framework adapter swaps the family set, not the panel.
- **Incidental cross-story touch (disclosed):** tightened one assertion in
  `SiteGeneratorSpecKernelTests.GenerateAll_MissingSpecFolderDegradesGracefully` from a blanket
  `DoesNotContain("Spec Kernel")` to target the section *band* specifically, because the new coverage panel
  legitimately names "Spec Kernel" as a *missing* family (AC #1). This is Story 3.3's own behavior, not 3.2's.

### File List

- `src/SpecScribe/ArtifactCoverage.cs` (new — model + pure builder)
- `src/SpecScribe/SiteGenerator.cs` (gather inputs, cache `_coverage`, memlog discovery, wire into `WriteIndex`)
- `src/SpecScribe/Charts.cs` (new `ArtifactCoveragePanel` pure builder)
- `src/SpecScribe/HtmlTemplater.cs` (`RenderIndex`/`AppendDashboard` optional `coverage` param + panel render)
- `src/SpecScribe/assets/specscribe.css` (coverage-panel styles, neutral tones)
- `tests/SpecScribe.Tests/ArtifactCoverageTests.cs` (new — pure Build + staleness + memlog-additivity tests)
- `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` (panel render + omission tests)
- `tests/SpecScribe.Tests/StylesheetTests.cs` (coverage-panel CSS seam guard)
- `tests/SpecScribe.Tests/SiteGeneratorSpecKernelTests.cs` (tightened Spec-Kernel-band omission assertion)

### Review-Driven Enhancement (actionable panel)

Review feedback: the panel read as a passive status list — unclear what it was, why it mattered, or what to do
next. Reworked it into an **actionable 2-column card grid** (no new architecture; same never-throw / neutral-tone
/ pure-vs-IO discipline):

- **Panel intro line + per-family one-line description** so every card is self-explanatory.
- **Present family → whole-card link** to its page. Hrefs resolved in the generator (`ResolveFamilyHref`):
  Epics/Stories → `epics.html`, Requirements → `requirements.html`, the rest → `ToOutputRelative(source)`.
  Verified all eight link targets exist in the generated output (no broken links).
- **Missing family → "what it is" sentence + copyable create-command**, reusing `BmadCommands.InlineGuidance`
  (the Next Steps command badge). Step keys live in the new `ArtifactCoverage.CreateStepKeys` single-source map,
  resolved against the detected module via `CommandCatalog.Command`; a step the module doesn't expose (e.g. Spec
  Kernel) degrades to plain guidance text — no invented commands.
- Model: `ArtifactFamily` gained `Description` (static, set by pure `Build`) and generator-resolved nullable
  `Href`/`CreateCommand` (left null by `Build`, so it stays purely source-derived).
- Verified end-to-end: `generate` shows the 2-col grid with linked present cards; an isolated run with a family
  removed renders its card as missing with `Create it with /bmad-product-brief` (copyable). Confirmed 2-col
  desktop / 1-col mobile via computed styles.
- Full suite: **502 passed, 0 failed** (the earlier external Story 3.2 deep-git test has since been reconciled).

### Change Log

- 2026-07-08: Implemented Story 3.3 — artifact/workflow structure coverage insight ("Planning Coverage"
  dashboard panel), pure `ArtifactCoverage` model + builder, generator gathering (mtimes + memlog enrichment),
  panel renderer + CSS, and full test coverage. Status → review.
- 2026-07-08: Review-driven enhancement — reworked the panel into an actionable 2-column card grid with an
  intro, per-family descriptions, whole-card links for present families, and copyable create-commands for
  missing ones. Extended `ArtifactCoverage` (Description/Href/CreateCommand + `CreateStepKeys`), generator href
  and command resolution, and tests. Full suite green (502).
- 2026-07-08: Second review pass — renamed the panel "Planning Artifacts"; added a header coverage meter
  (% present, `role=progressbar`); dropped the noisy "Present" chip (only Missing/Stale chip now); applied the
  "Explore Key Views" family accent colors (planning=gold, architecture=teal, epics=moss, requirements=rust) as
  a card left-edge; bumped card font sizes; moved the secondary decision-journal date off the card into a rich
  body-level `js-tip` tooltip that spells out "Decision journal (.memlog) updated …" (clarifying the previously
  cryptic "journal" bullet). Verified via computed styles + generated output; full suite green (502).
- 2026-07-08: Layout tweak — moved the coverage meter into the panel header row (top-right, via the shared
  `chart-panel-header-row` pattern) and slimmed it (88px, thinner) so it's an unobtrusive at-a-glance summary,
  not a dominant band; made the intro paragraph full-width (removed the measure cap). New `Charts.CoverageMeter`
  helper. Full suite green (510).
