# Deferred Work

Real-but-not-now items surfaced during reviews. Each is safe to leave; revisit when the related area is next touched.

## Deferred from: code review of spec-3-1-deferred-debt-cleanup.md (2026-07-18)

- source_spec: `spec-3-1-deferred-debt-cleanup.md`
  summary: Git Pulse file-window copy hard-codes "200" separately from `TryCompute`'s `-n 200`, so changing the git cap can leave a lying panel title/empty state.
  evidence: Blind Hunter — label and RunGit argument are not a shared constant.
- source_spec: `spec-3-1-deferred-debt-cleanup.md`
  summary: File-bar `<li aria-label>` plus nested path link and visible count can double-announce to some AT; track alone is aria-hidden.
  evidence: Blind Hunter — ProgressBar-style unifying name on a list row that still exposes interactive/text children.
- source_spec: `spec-3-1-deferred-debt-cleanup.md`
  summary: `HotspotBars` still lacks the unifying file-bar `aria-label` applied to `GitPulsePanel` (Ask First left that propagation out of this batch).
  evidence: Blind Hunter — twin markup path unchanged by design.

## Deferred from: code review of spec-9-7-deferred-angular-weight-and-ledger-assert.md (2026-07-18)

- source_spec: `spec-9-7-deferred-angular-weight-and-ledger-assert.md`
  summary: `Math.Max(1, TasksTotal + nestedCount)` leaves a TasksTotal=0 story with exactly one nested deferred at weight 1 — identical to a peer with none — so the floor swallows the smallest crowding case.
  evidence: Blind Hunter — frozen formula max(1, sum) vs intent to grow with any nested children; would need max(1, TasksTotal) + count (intent renegotiation).

## Deferred from: code review of spec-9-5-deferred-collapse-and-evidence-polish.md (2026-07-18)

- source_spec: `spec-9-5-deferred-collapse-and-evidence-polish.md`
  summary: H3 `Dev Agent Record` section bounds still end only at the next `## `, so peer `###` headings stay inside File List / Dev Agent Record extraction windows (same FindSection quirk as pre-existing `ExtractTestEvidence`).
  evidence: Edge Case Hunter + Blind Hunter — spreading H3 DAR support without H3-aware section end; Ask First left SplitStoryArtifact ##-only remainder stop out of this batch.

## Deferred from: code review of spec-9-6-deferred-followup-heuristics.md (2026-07-18)

- source_spec: `spec-9-6-deferred-followup-heuristics.md`
  summary: `NormalizeProvenanceKey` / geometry `SourceKey` still keep path-prefixed `_bmad-output/.../stem` tokens after `StoryIdFromKey` gained filename stripping — stem equality for non-story `spec-*` joins can miss.
  evidence: Blind Hunter — path strip scoped to StoryIdFromKey only; geometry/quick-dev joins use SourceKey as stored.
- source_spec: `spec-9-6-deferred-followup-heuristics.md`
  summary: `ResolveHref` map/stem lookup does not normalize `\` → `/` before `GetFileNameWithoutExtension`, so backslash path-prefixed tokens can miss on non-Windows hosts.
  evidence: Blind Hunter — adjacent to StoryIdFromKey path fix; ResolveHref still raw-token-first.
- source_spec: `spec-9-6-deferred-followup-heuristics.md`
  summary: Column-0 `* `/`+ ` thematic-break-shaped lines (e.g. `* * *`) can parse as deferred list items.
  evidence: Edge Case Hunter — CommonMark ambiguity; live BMAD notes use `-`; prefer revisit with marker polish.

## Deferred from: code review of spec-epic9-watch-followup-surface-refresh.md (2026-07-18)

- source_spec: `spec-epic9-watch-followup-surface-refresh.md`
  summary: Every `RegenerateEpics` (any impl-artifact story save) now rewrites deferred list + all follow-up detail/group pages + quick-dev chrome even when `deferred-work.md` did not change — correctness over scoped I/O; revisit if watch lag shows up.
  evidence: Blind Hunter — intentional breadth of RefreshFollowUpSurfaces on the epics watch route; AD-5 still avoids full GenerateAll.
- source_spec: `spec-epic9-watch-followup-surface-refresh.md`
  summary: Non-group orphan `follow-ups/deferred-*.html` after item remove/slug rewrite still not pruned (group-* prune only); SPA capture can keep serving stale detail files in long watch sessions.
  evidence: Blind Hunter — out of scope per spec Never/aggressive wipe; same GenerateAll-without-wipe behavior.

## Deferred from: code review of 9-12-unplanned-and-one-off-work-in-geometry-and-sprint.md (2026-07-17)

- ~~Watch-mode `GenerateOne` rewrites quick-dev + index but does not call `WriteFollowUpGroupPages` — Unplanned/Follow-ups group destinations can stay stale until full `GenerateAll`. Same incremental family as 9.11 / artifact-review deferrals.~~ **RESOLVED 2026-07-18** (`spec-epic9-watch-followup-surface-refresh`): `RefreshFollowUpSurfaces` runs group pages (and deferred list/details) from `GenerateOne` and `RegenerateEpics` — the real watch route for `deferred-work.md`. [`SiteGenerator.cs`]
- ~~`ContainsSpecName` uses `text.Contains(stem)` without token boundaries — overlapping stems (e.g. `spec-a` vs `spec-ab`) can mis-attribute or multi-hit→null. Cleanup when next touching cue matching. [`UnplannedWorkGeometry.cs`]~~ **RESOLVED 2026-07-18** (`spec-epic9-deferred-debt-cleanup`): hyphen-aware token match (`(?<![\w-])stem(?:\.(?:md|html))?(?![\w-])`). [`UnplannedWorkGeometry.cs`]

## Deferred from: code review of 9-13-generated-filtered-follow-up-group-pages-and-sunburst-click-destinations.md (2026-07-17)

- ~~Epic angular weight on project glance is `max(1, Σ max(1, TasksTotal))` and no longer includes follow-up slot counts, so follow-up-heavy / task-light epics get thin wedges that compress open/done aggregates. Hierarchy design; revisit with 10.7 density.~~ **RESOLVED 2026-07-18** (`spec-9-13-deferred-glance-weight-noplan-sourcekey`): glance `EpicWeight` includes EpicSunburst peer set (actions + epic-level deferred + attributed QD) without double-counting story-child deferred. [`Charts.cs`]
- ~~Project glance removal of the no-plan outer create-story CTA leaves `TasksTotal == 0` stories looking like planned peers (weight still `max(1, …)`). Hierarchy rewrite; not a 9.13 group-page bug.~~ **RESOLVED 2026-07-18** (`spec-9-13-deferred-glance-weight-noplan-sourcekey`): middle-ring `.sb-noplan` + “no task plan yet” on glance and epic detail; no outer create-story fringe. [`Charts.cs`]
- ~~Source-key normalization is duplicated across Charts / Unplanned cards / FollowUpGeometry, and `FindQuickDev` still has a tautological stem/`.md` comparison branch — cleanup when next touching provenance matching.~~ **RESOLVED 2026-07-18** (`spec-9-13-deferred-glance-weight-noplan-sourcekey`): shared `NormalizeSourceKey`; tautology deleted. [`FollowUpGeometry.cs` / `Charts.cs` / `SprintTemplater.cs`]

## Deferred from: code review of 9-7-open-follow-ups-in-the-remaining-work-geometry.md (2026-07-17)

- ~~Epic/story angular weight ignores nested story-child deferred crowding — many deferred under a thin `max(1, TasksTotal)` story share a small outer arc without growing the parent sweep.~~ **RESOLVED 2026-07-18** (`spec-9-7-deferred-angular-weight-and-ledger-assert`): `StoryWeight = max(1, TasksTotal + StoryChildDeferred.Count)` on project glance + epic detail. [`Charts.cs`]
- ~~`FollowUpGeometry.From` documents ledger agreement but does not assert `OpenActionItems` / deferred open tallies against `ProjectCounts` at build time (tests cover happy paths only).~~ **RESOLVED 2026-07-18** (`spec-9-7-deferred-angular-weight-and-ledger-assert`): `Debug.Assert` open-from-list == `counts.OpenActionItems` (deferred slot count intentionally not equated to ledger). [`FollowUpGeometry.cs`]

## Deferred from: code review of 9-10-scannable-follow-up-list-pages.md (2026-07-17)

- ~~Nested / unclosed top-level `<li>` handling in `ExtractTopLevelListItems` can truncate nested lists or drop later siblings; structured Deferred-from path unaffected. Related to prior 9.11 deferral. [`FollowUpGeometry.cs:367`]~~ **RESOLVED 2026-07-18** (`spec-epic9-deferred-debt-cleanup`): balanced nested `<li>` + unclosed skips item only (siblings kept). [`FollowUpGeometry.cs`]
- ~~Unstructured deferred notes that contain `<li>` items render as `.followup-row` + slugs instead of the prior plain-body `deferred-work-fallback` article (prose-only fallback remains). 9.11 overlay; out of pure-9.10 scope. [`DeferredWorkTemplater.cs:38`]~~ **RESOLVED 2026-07-18** (`spec-epic9-deferred-debt-cleanup`): accepted intentional Story 9.11 overlay — keep rows+slugs; prose-only fallback unchanged.
- ~~`FollowUpRowTests` covers `Summarize*` only — no direct asserts for `Render` empty-primary / href-vs-disclosure branches. [`FollowUpRowTests.cs`]~~ **RESOLVED 2026-07-18** (`spec-epic9-deferred-debt-cleanup`): empty-primary Render assert added (href/disclosure already covered). [`FollowUpRowTests.cs`]

## Deferred from: code review of spec-artifact-review-nav-and-deferred.md (2026-07-17)

- source_spec: `spec-artifact-review-nav-and-deferred.md`
  summary: Watch-mode `GenerateOne` rewrites quick-dev chrome but does not refresh story `DeferredFromThis` panels or regenerate `follow-ups/*` detail/group pages after deferred-work edits.
  evidence: Blind Hunter + Edge Case Hunter — incremental family shared with prior watch gaps; full GenerateAll stays correct.
- source_spec: `spec-artifact-review-nav-and-deferred.md`
  summary: No SiteGenerator integration test asserts rewritten quick-dev breadcrumbs / story reverse panels on a full GenerateAll (unit coverage only).
  evidence: Blind Hunter — live example still relies on manual regenerate check.
- source_spec: `spec-artifact-review-nav-and-deferred.md`
  summary: Stopping `ApplyReferenceLinks` on deferred detail pages (data-copy safety) also drops requirement/story linkification inside deferred body prose.
  evidence: Blind Hunter — forward-path navigation regression risk; tests pin data-copy only.
- source_spec: `spec-artifact-review-nav-and-deferred.md`
  summary: Unstructured deferred items get per-item detail slugs and Unplanned membership without SourceKey reverse-index benefit.
  evidence: Blind Hunter — expands Unplanned surface relative to earlier 9.12 structured-only reverse path.

## Deferred from: code review of 9-11-follow-up-detail-pages-and-deep-links.md (2026-07-17)

- ~~Watch-mode `GenerateOne` does not call `WriteFollowUpDetails` / `WriteDeferredWork` (GenerateAll-only) — editing `deferred-work.md` in watch can leave sunburst/list deep links pointing at stale or missing `follow-ups/*.html` until a full generate. Same incremental family as prior watch gaps.~~ **RESOLVED 2026-07-18** (`spec-epic9-watch-followup-surface-refresh`): `RegenerateEpics` (watch path for impl-artifacts / deferred-work) and `GenerateOne` both call `RefreshFollowUpSurfaces` (`WriteDeferredWork` + `WriteFollowUpDetails` + group/quick-dev). [`SiteGenerator.cs`]
- ~~`ExtractTopLevelListItems` yields break on an unclosed top-level `<li>` so later siblings never become deferred slots. Rare malformed Markdig output; structured path is unaffected. [`FollowUpGeometry.cs:266`]~~ **RESOLVED 2026-07-18** (`spec-epic9-deferred-debt-cleanup`): unclosed top-level `<li>` skips that item only; later siblings still extract. [`FollowUpGeometry.cs`]

## Deferred from: code review of 9-8-authoring-and-delivery-workflow-coherence.md (2026-07-17)

- ~~Accent/kicker slug heuristics (`AccentForCommand` / `KickerForCommand`) default unknown catalog slugs to accent `ready` and kicker "Also consider"; `sprint-status` painted `active`.~~ **RESOLVED 2026-07-17** (`spec-accent-kicker-slug-heuristics`): unknown slugs fail closed to accent `pending` + kicker "Also consider"; known families covered by unit tests; `sprint-status` accent stays `active`. [`BmadCommands.cs`]

## Deferred from: code review of spec-accent-kicker-slug-heuristics.md (2026-07-17)

- source_spec: `spec-accent-kicker-slug-heuristics.md`
  summary: Accent/kicker known-family coverage is a hand-maintained theory table, not derived from `For*` `Command(...)` call sites.
  evidence: Blind Hunter — a newly suggested step can ship fail-closed with no test failure until the table is extended.
- source_spec: `spec-accent-kicker-slug-heuristics.md`
  summary: `CommandSlug`/`Contains` use Ordinal matching (and `Split` without trim); mixed-case or leading-space catalog values miss known maps and fail-close.
  evidence: Blind Hunter + Edge Case Hunter — pre-existing heuristic shape; live `module-help.csv` skills are normally lowercase.

## Deferred from: code review of 9-6-follow-up-item-provenance-and-resolution-paths.md (2026-07-17)

- ~~Top-level deferred bullets only recognized as column-0 `- ` / `-\t`; `*` / `+` / numbered lists never become items. Live notes use `-`; foreign frameworks may not.~~ **[RESOLVED in `spec-9-6-deferred-followup-heuristics`]** Column-0 CommonMark unordered `[-*+]` and ordered `N.`/`N)` markers parse as items. [`DeferredWorkParser.cs`]
- ~~Path-prefixed `source_spec` tokens can fail `StoryIdFromKey` (and skip placeholder href) when the href map misses; map lookup by filename stem usually still works.~~ **[RESOLVED in `spec-9-6-deferred-followup-heuristics`]** `StoryIdFromKey` strips to filename stem before matching. [`FollowUpRefs.cs`]
- ~~`ResolvingStoryIdFromText` only accepts `RESOLVED in [Story] N.M` — other closure phrasings leave `Resolved` true without a resolving link. Intentional best-effort heuristic.~~ **[RESOLVED in `spec-9-6-deferred-followup-heuristics`]** Also extracts `N.M` with trailing punctuation and backtick story/spec tokens after RESOLVED. [`FollowUpRefs.cs`]
- ~~Near-dupe cross-link keeps only the first counterpart epic (“first match wins for stability”); multi-epic repeats understate provenance.~~ **[RESOLVED in `spec-9-6-deferred-followup-heuristics`]** Accumulates sorted distinct counterpart epics; one cross-link each. [`ActionItemsTemplater.cs`]
- ~~Resolving chip label heuristic (`Contains('.') && !Contains('-')`) can emit `Story readme.md` for hyphen-free filenames. Rare.~~ **[RESOLVED in `spec-9-6-deferred-followup-heuristics`]** `FollowUpRefs.ResolvingLabel` prefixes `Story` only for dotted `N.M` ids. [`DeferredWorkTemplater.cs` / `FollowUpDetailTemplater.cs`]

## Deferred from: code review of 9-5-distinct-acceptance-criteria-blocks-and-collapsed-dev-notes.md (2026-07-16)

- ~~Markdig duplicate-slug forms (`references-1`) never match exact `StoryRemainderSlugs` (`dev-notes`/`references`) — second same-titled H2 stays expanded. Rare; revisit if foreign-framework artifacts collide.~~ **[RESOLVED in `spec-9-5-deferred-collapse-and-evidence-polish`]** Collision suffix stripped for set membership only. [`CollapsibleSections.cs`]
- ~~Adjacent 9.4 polish in the same working tree: `ExtractChangeLogVerification` continues past malformed dated rows instead of returning null on the first dated-shape match. Revisit in 9.4 review.~~ **[RESOLVED in `spec-9-5-deferred-collapse-and-evidence-polish`]** First dated-shape unparseable date → null. [`EpicsParser.cs`]
- ~~Adjacent 9.4 polish: `ExtractTestEvidence` falls back to `### Dev Agent Record` while sibling file-list / change-surface extractors may not — asymmetric empty surfaces. Revisit in 9.4 review.~~ **[RESOLVED in `spec-9-5-deferred-collapse-and-evidence-polish`]** File List + Dev Agent Record accept H3 DAR. [`ChangeSurface.cs` / `EpicsParser.cs`]

## Deferred from: code review of 9-1-requirement-pages-link-to-their-covering-stories.md (2026-07-16)

- ~~`epics.Epics.ToDictionary(e => e.Number)` in `RenderRequirement` can throw on duplicate epic numbers and abort requirement-page generation — same pre-existing pattern as `StoriesFor` / parser `DeriveStatus`; revisit if epic-list de-dupe is added. [`RequirementsTemplater.cs:155`]~~ **RESOLVED 2026-07-18** (`spec-epic9-deferred-debt-cleanup`): first-wins `GroupBy` + `First()` in requirement/coverage render helpers. [`RequirementsTemplater.cs`]

## Deferred from: code review of 8-8-generation-time-recency-signals.md (2026-07-15)

- ~~Path map for story git dates uses `StringComparer.Ordinal`, matching the git layer's Ordinal path keys — case-only mismatches between git and `ArtifactSourcePath` on Windows silently miss the git date. Low likelihood; revisit if IgnoreCase is adopted git-wide. [`ProgressCalculator.cs`]~~ **RESOLVED 2026-07-18** (`spec-epic8-deferred-debt-cleanup`): accepted Ordinal consistency with the git layer / Story 3.1 path-key policy — IgnoreCase deferred until a deliberate git-wide comparer change.

## Deferred from: code review of 8-3-single-source-of-truth-for-every-count.md (2026-07-15)

- ~~Incremental `WriteIndex` / `WriteSprint` / `WriteActionItems` rebuild `_counts` when null but have no events list to re-emit the Unsupported divergence notice — same watch-mode notice-gap pattern as prior stories. [`SiteGenerator.cs`]~~ **RESOLVED 2026-07-18** (`spec-epic8-deferred-debt-cleanup`): `RegenerateEpics` re-emits Unsupported via shared `AppendCountDivergenceNotice` after follow-up surfaces rebuild `_counts`; summary message carries the notice for watch callers.
- ~~`Reconcile` builds defined ids with `ToHashSet`, so a duplicated story id in `epics.md` is silently collapsed for untracked/orphan reporting. [`ProjectCounts.cs`]~~ **RESOLVED 2026-07-18** (`spec-epic8-deferred-debt-cleanup`): first-wins membership retained; duplicates listed on `DuplicateDefinedStoryIds` and named in `DivergenceMessage` / HasDivergence.
- ~~`DivergenceMessage` joins every untracked/orphan id with no cap — large drift sets produce unbounded diagnostic strings. [`ProjectCounts.cs`]~~ **RESOLVED 2026-07-18** (`spec-epic8-deferred-debt-cleanup`): each id list capped at 10 with `+N more`; prose totals stay accurate.
- ~~Generation-level 8.3 tests regex HTML for "Stories defined" / sprint subtitle but never assert Story Pipeline funnel drafted total == `StoriesDefined` or that Defined vs Tracked stay distinct on every surface under drift. [`SiteGeneratorSprintTests.cs`]~~ **RESOLVED 2026-07-18** (`spec-epic8-deferred-debt-cleanup`): funnel drafted aria/count pinned to Stories defined; Defined≠Tracked asserted under orphan drift on index + sprint; watch re-emit covered.

## Deferred from: code review of 8-2-canonical-status-model-with-portal-wide-legend.md (2026-07-15)

- ~~Substring `Contains` classifiers in `ForStatus` can still invent lifecycle stages (e.g. `incomplete` → done) and bypass the unrecognized safety net — pre-existing fuzzy matching, not introduced by 8.2's absent-vs-unmapped change. [`StatusStyles.cs`]~~ **RESOLVED 2026-07-18** (`spec-epic8-deferred-debt-cleanup`): ForSprint-shaped exact/synonym match + kebab-token fallback; `incomplete` → unrecognized.
- ~~`LegendKey()` keeps an inline stage→word switch beside `StoryLabel` / siblings — drift risk if labels diverge later; words currently match. [`StatusStyles.cs`]~~ **RESOLVED 2026-07-18** (`spec-epic8-deferred-debt-cleanup`): legend words route through `StoryLabel` / `RequirementLabel` / `SprintLabel`.

## Deferred from: code review of 8-1-integration-spike-cross-surface-status-verification.md (2026-07-14)

- ~~Epic 6 standing surface-coverage action lacks a machine-readable split between the Epic 8 instance (claimed executed) and the standing rule (still open); readers can misread the whole `in-progress` item. Pre-existing process/status shape; wording fix already flagged on the same action item. [`sprint-status.yaml:315`]~~ **RESOLVED 2026-07-18** (`spec-epic8-deferred-debt-cleanup`): split into two action items — Epic 8 instance `done` + standing surface-coverage gate as its own row.

## Deferred from: code review of spec-webview-doc-page-surfaces (2026-07-13)

- source_spec: `spec-webview-doc-page-surfaces.md`
  summary: ADR-landing hrefs (and most filename-derived hrefs site-wide) are HTML-entity-escaped but never
  percent-encoded — a source `.md`/ADR filename containing `#`, `?`, or a space produces a link that truncates or
  misresolves. Plausibly a real contributor to the `.md`-related 404s the owner reported, but it's a pre-existing,
  site-wide pattern, not something novel to this spec.
  evidence: Edge Case Hunter. [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs)

## Deferred from: code review of story-7-3 (2026-07-13)

- source_spec: `7-3-activity-timeline-and-date-pages.md`
  summary: Watch-mode (`GenerateOne`) never refreshes the timeline/date-page signal — editing an artifact's mtime in watch mode doesn't recompute `BuildArtifactsByDay`/date pages/timeline, only the next full generate does.
  evidence: Acceptance Auditor. Owner decision (2026-07-13): accept documented staleness for now; revisit alongside other watch-mode work. [SiteGenerator.cs:337-351](../../src/SpecScribe/SiteGenerator.cs)
- source_spec: `7-3-activity-timeline-and-date-pages.md`
  summary: `ArtifactLabel` silently swallows read failures (permission/I-O errors) into a bare filename stem with no `GenerationEvent`/diagnostic, unlike most other per-item failure paths in the same file.
  evidence: Blind Hunter. Pre-existing degrade-silently pattern extended here, low impact. [SiteGenerator.cs:853-864](../../src/SpecScribe/SiteGenerator.cs)
- source_spec: `7-3-activity-timeline-and-date-pages.md`
  summary: `ActivityModel.GroupArtifactsByDay` sorts/dedups by `(Label, Href)` text only; two distinct artifacts sharing a generic title (e.g. two docs both titled "Overview") render as adjacent, visually indistinguishable list entries with no path/type disambiguation.
  evidence: Edge Case Hunter + Blind Hunter (independently flagged). Cosmetic edge case, low likelihood. [ActivityModel.cs:9-36](../../src/SpecScribe/ActivityModel.cs)
- source_spec: `7-3-activity-timeline-and-date-pages.md`
  summary: The unbounded `GitPulse.CommitsByDay` used for date pages/timeline (unlike the 300-commit-capped `_commitPages`/deep-git correlation from Story 7.5) means date pages beyond that window can never resolve their commit hashes to a detail page — degrades safely to plain `<code>`.
  evidence: Blind Hunter. Completeness gap inherited from 7.5's cap, extended (not introduced) by this story. [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs)

## Deferred from: code review of story-7-7 (2026-07-13)

- ~~A query string or fragment on the git remote URL itself (e.g. `repo.git?ref=x`) leaks into the generated
  repo name, since `ParseRemote` only strips a literal trailing `.git`.~~ **RESOLVED 2026-07-19**
  (`bmad-quick-dev` deferred-work pass): `ParseRemote` now strips any `?query`/`#fragment` from the remote's
  path segment before splitting into `(host, owner, repo)`, so `repo.git?ref=x` and `repo.git#readme` both parse
  to `repo`. [`CodeSourceUrlResolver.cs`]
- ~~An explicit `--code-url` value that already contains its own `#...` fragment isn't sanitized before the
  repo-relative path is appended, corrupting the resulting link.~~ **RESOLVED 2026-07-19** (`bmad-quick-dev`
  deferred-work pass): `ForgeOptions.TryValidateCodeUrl` now strips a trailing `#...` fragment from the validated
  base (a fragment is only valid at the end of a URL, and `BuildExternalSourceUrl` appends the repo-relative path
  after this base). [`ForgeOptions.cs`]
- ~~`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` on the webview JSON payload and the unconditional
  `generator.CapturePages = true` in `WebviewCommand.Execute` are real considerations (JSON-escaping relaxation on a
  payload that embeds arbitrary HTML; forced whole-site page capture on every webview run) but belong to the
  bundled webview-doc-page-surfaces work riding the same commit/files, not Story 7.7 itself.~~ **CLOSED as
  misattributed 2026-07-19** (`bmad-quick-dev` deferred-work pass, no code change): both considerations are
  already tracked under their real owning story — the relaxed JSON escaping is documented-safe in
  `WebviewCommand.CamelCase`'s own summary (payload is only ever `JSON.parse`d, never embedded into markup), and
  the unconditional whole-site capture is the same full-regeneration behavior already deferred under
  "Scoped re-render not implemented" (Story 6.4) and the `spec-webview-doc-page-surfaces` per-save-cost deferral
  above. No Story 7.7 file was touched by either concern.

## Deferred from: code review of story-7-1 (2026-07-13)

- source_spec: `7-1-in-portal-code-file-browsing.md`
  summary: `StringComparer.OrdinalIgnoreCase` on repo-relative path dictionaries (`_codePages`, `_codeReverseMap`, discovery's `SortedSet`) risks silently colliding two case-differing files on a case-sensitive (Linux) checkout.
  evidence: Blind Hunter + Edge Case Hunter (independently flagged). Mirrors the codebase-wide convention already used for `_docs` and the markdown reference map since before this story; deferred as pre-existing, not novel to 7.1. [SiteGenerator.cs:78,87,1109](../../src/SpecScribe/SiteGenerator.cs)
- source_spec: `7-1-in-portal-code-file-browsing.md`
  summary: `CommitHref`'s abbreviated-hash fallback does a linear `Dictionary` scan with no ambiguity check, so two same-prefix short hashes resolve non-deterministically.
  evidence: Blind Hunter + Edge Case Hunter (independently flagged). `CommitHref`/`_commitPages` are Story 7.5's seam, reused here for the code page's History tab; not introduced by 7.1. [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs)
- source_spec: `7-1-in-portal-code-file-browsing.md`
  summary: `EnumerateCodeFiles`/`MaxCodeFileBytes` reuse for the code-map's per-file line count (Story 7.6) reads every tracked file fully into memory just to count lines, and silently omits >1MB files from the size visualization.
  evidence: Blind Hunter. Out of 7.1's scope (Story 7.6 code, bundled into this diff only because it shares `SiteGenerator.cs`). [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs)
- source_spec: `7-1-in-portal-code-file-browsing.md`
  summary: Placeholder pages (binary/oversized/unreadable files) never receive `FileInsight`/History-tab data even when `--deep-git` has it, because `RenderPlaceholder` doesn't accept those parameters.
  evidence: Edge Case Hunter. Out of 7.1's scope — the Insights/History tabs are Story 7.4/7.8/10.x work bundled into `CodeFileTemplater.cs`. [CodeFileTemplater.cs](../../src/SpecScribe/CodeFileTemplater.cs)
- source_spec: `7-1-in-portal-code-file-browsing.md`
  summary: `TabGroupName` can theoretically collide across two paths that differ only in `/` vs. a literal `-` in a directory name, cross-wiring radio-group tab state.
  evidence: Blind Hunter. Low likelihood; only affects the SPA/webview whole-document capture mode. [CodeFileTemplater.cs](../../src/SpecScribe/CodeFileTemplater.cs)

## Deferred from: code review of story-7-6 (2026-07-13)

- source_spec: `7-6-source-code-treemap-for-codebase-exploration.md`
  summary: Per-file exception guard in `SiteGenerator.EnumerateCodeFiles` only catches `IOException`/`UnauthorizedAccessException`; any other exception type from the shared `TryReadCodeText`/`SplitCodeLines` helpers bubbles to the outer catch and empties the whole code-map result instead of skipping just that one file.
  evidence: Blind Hunter + Edge Case Hunter (independently flagged). `TryReadCodeText`/`SplitCodeLines` pre-date this diff (shared with Story 7.1 code pages) — this story only enlarges the blast radius by also gating the "Code Map" nav item on the result. [SiteGenerator.cs:2307](../../src/SpecScribe/SiteGenerator.cs)
- source_spec: `7-6-source-code-treemap-for-codebase-exploration.md`
  summary: `GitMetrics.TryListFiles` splits `git ls-files` output on `\n` and `.Trim()`s each line rather than using `-z`/NUL-delimited output, so a tracked filename containing a literal newline or significant leading/trailing whitespace would corrupt the parsed path.
  evidence: Edge Case Hunter. Extremely rare in practice; matches the line-based parsing discipline already used elsewhere in `GitMetrics`. [GitMetrics.cs:850](../../src/SpecScribe/GitMetrics.cs)

## Deferred from: quick-dev scope decision — website nib favicon (2026-07-13)

- source_spec: `spec-website-nib-favicon.md`
  summary: Nib-branded social-preview card (`og:image`/`twitter:card`) for the generated site, so shared links render the brand mark rather than a bare title.
  evidence: Owner chose (2026-07-13) to ship the favicon now and defer the card. A *functional* preview needs infrastructure SpecScribe lacks: (1) an absolute canonical site URL — the generator only knows the source repo / `CodeSourceBaseUrl` (Story 7.7) and the footer `RepositoryUrl`, not its own deployed URL — so social crawlers (which don't resolve relative `og:image`/`og:url`) need a new `--site-url`/base-url config; and (2) a raster card — X/Facebook/LinkedIn/Slack don't render SVG `og:image`, and the project ships self-contained with no image dependency/pipeline (ADR 0005/0006), so it needs a committed PNG asset (new manual-sync debt, adjacent to Story 16.5's asset-pipeline scope). Independently shippable as its own story once site-URL config + an asset path exist.

## Deferred from: inline review of spec-website-nib-favicon (2026-07-13)

- source_spec: `spec-website-nib-favicon.md`
  summary: The new favicon is a 4th rendition of the Scribe's Nib mark; its geometry is single-sourced from `HtmlRenderAdapter.NibPathData`, but its three brand hex values (`#2e6b7a` tile, `#f5f0e8` nib, `#d4a017` vent) are hand-copied from `extension/media/specscribe.svg` with no sync guard.
  evidence: Extends the existing 3-rendition geometry sync-debt (see the spec-scribes-nib-branding deferral below → Story 16.5's asset pipeline). Lower risk than that item — the risky part (geometry) IS single-sourced here, and only the stable brand palette is duplicated — but a future palette change to `specscribe.svg` would silently leave the favicon behind. Fold the favicon into 16.5's asset/token pipeline (or a shared brand-palette constant) when that work lands.

- source_spec: `spec-scribes-nib-branding-and-vs-contrast-pass.md`
  summary: The Scribe's Nib geometry exists in three hand-kept renditions (`HtmlRenderAdapter.NibPathData`, `extension/media/specscribe-outline.svg`, and a 16-box scaled variant in `extension/media/specscribe.svg`) with no sync guard — a future mark tweak can silently drift them.
  evidence: Review finding (Blind Hunter #9). The in-app path is now single-sourced as a C# const and both SVG comments point at it, but nothing mechanical pins the extension assets to the const (tests run against the compiled assembly, not repo-relative asset files, and no build step spans C#/SVG). Add a repo-relative asset test or a generation step for the SVGs when extension packaging (Story 16.5) builds its asset pipeline.

## Deferred from: spec-webview-doc-page-surfaces implementation (2026-07-12)

- source_spec: `spec-webview-doc-page-surfaces.md`
  ~~summary: When ADR records exist but the ADR root has no `README.md`, the nav links `adrs/index.html` which is never generated — a 404 in the static site and a dead surface key in the webview.~~ **RESOLVED same day (owner decision at the review checkpoint):** `GenerateAdrsInternal` now synthesizes a minimal landing from the ordered `_adrs` records when no root README exists, so the nav link always resolves; repos WITH a README are byte-identical (golden unaffected). Pinned by `SiteGeneratorWebviewTests.AdrLandingIsSynthesized_WhenTheAdrRootHasNoReadme`.
- source_spec: `spec-webview-doc-page-surfaces.md`
  summary: The shared 6.7 landmark extraction truncates a captured page whose `<main>` body contains a literal `</main>` (e.g. a raw-HTML code sample) — the region ends at the first closer, shipping unbalanced markup to the SPA and webview.
  evidence: `SpaDelivery.ExtractContentRegion` takes the FIRST `</main>` at-or-after the opener (the 6.7 review only fixed close-before-open). Inherent to string slicing; affects SPA and captured webview surfaces equally; no live page hits it today. Revisit if a doc legitimately embeds that literal — a depth-counting scan or a sentinel comment emitted by the templaters would fix both consumers at once.
- source_spec: `spec-webview-doc-page-surfaces.md`
  summary: Watch-mode incremental passes can leave stale entries in the page capture (`_spaCapture`) — e.g. `RegenerateAdrs` wipes the adrs/ output dir but doesn't prune capture keys for deleted ADRs — so a long-lived generator's SPA/webview bundle can ship a page that no longer exists.
  evidence: Extends the existing 6.7 deferral ("watch-mode `_spaCapture`/bundle can drift on doc rename or delete") to the ADR/epics wipe-and-rebuild routes. The VS Code panel is UNAFFECTED (each refresh is a fresh `specscribe webview` spawn → fresh GenerateAll → fresh capture); only core `watch`-mode with `--spa` (or a future long-lived webview server) can hit it. Prune per wiped subdir when the rename/removal path is next touched.
- source_spec: `spec-webview-doc-page-surfaces.md`
  summary: Every watcher-driven panel refresh now re-renders and re-parses the whole-site payload (~7.8 MB on this repo after exclusions + relaxed escaping) — the per-save cost grew with the surface breadth and there is no incremental path.
  evidence: Extends the existing 6.4 deferral ("Scoped re-render not implemented") and 6.11's full-`GenerateAll` note: the shim spawns per refresh and `JSON.parse`s the full bundle. Fine at current sizes; the R6.4 scoped re-render / `--serve` warm-process idea remains the perf follow-up, now with a bigger payoff.

## Deferred from: code review of 6-3-vs-code-integration-spike (2026-07-11)

- source_spec: `6-3-vs-code-integration-spike.md` — three throwaway-spike-code defects carried to the Story 6.4 runtime build. **All three are now RESOLVED in Story 6.4** (verified 2026-07-11, 701 tests green); kept here for the audit trail.
- **[RESOLVED in 6.11 — the 6.4 claim was STALE]** ~~Live-push watcher glob anchored to the workspace folder while the renderer walks up to the repo root~~ — this bullet **claimed** 6.4 added `repoRoot` to the payload and anchored the watchers to it, but that was never true (6.9's Task-0 recon and 6.10 fact #11 both proved the committed payload had no such field). **Story 6.11 actually implemented it:** `WebviewCommand.ResolveSourceRoot`/`ResolveAdrRoot`/`ResolveRepoRootOffset` now emit `sourceRoot`/`adrRoot`/`repoRoot` on the `webview` payload, the shim builds its `RelativePattern` watchers from the resolved absolute repo root, and Story 6.10's reveal-source join was harmonized onto the same root — so both live-push and reveal-source fire correctly even when VS Code is opened on a subdirectory. Pinned by `WebviewCommandTests` (the three pure resolvers, incl. the subdir `../..` offset) + `SiteGeneratorWebviewTests.SerializePayload_EmitsResolvedWatchRoots_CamelCase`.
- **[RESOLVED in 6.4]** ~~Overlapping debounced re-renders race~~ — the runtime shim coalesces concurrent spawns via an in-flight guard (`loading ??= runRenderer(...).finally(() => loading = undefined)`), so rapid saves + nav-during-load share one render and no stale result overwrites a fresher one.
- **[RESOLVED in 6.4]** ~~Re-invoking the command leaks watchers/handlers and resets the panel~~ — the runtime early-returns and reveals the existing panel (`if (panel) { panel.reveal(); return; }`); watchers/handlers register once per panel.

## Deferred from: code review of 6-4-...-webview-runtime (2026-07-11)

- source_spec: `6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md` — surfaced during the 6.4 review; not a correctness defect, left for a follow-up.
- **Scoped re-render not implemented — every live-push does a full site regeneration.** `WebviewCommand` runs `generator.GenerateAll()` into a temp scratch dir on **every** debounced source change, then `RenderWebviewSurfaces()`; ADR 0005 §3 explicitly said *"Story 6.4 **must** add scoped re-render to feel live on large repos"* (~1.8–2.0 s warm full pass; coalesced by the shim's in-flight guard so never concurrent, but still full each time). Correctness is fine and small/medium repos feel fine; on large repos live refresh lags. Follow-up: scope the re-render to the changed artifact family (mirror `SiteGenerator.RegenerateEpics`) so refresh trends sub-second. Track as a 6.4 polish item or a dedicated story.

## Deferred from: code review of 6-4-...-webview-runtime (2026-07-11, parallel adversarial review)

- source_spec: `6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md` — surfaced by the parallel adversarial review; real but not blocking, left for a follow-up.
- **Webview nav-toggle bridge drops keyboard/focus affordances — deferred to Story 6.5 (owner decision 2026-07-11).** The HTML surface's `NavToggleScript` (`HtmlRenderAdapter.cs`) implements Escape-to-close-with-focus-return, focus-first-link-on-open, close-on-navigate at mobile widths, and resize-reset; the webview bridge (`WebviewRenderAdapter.cs` DocumentTemplate) reimplements only class-toggle + `aria-expanded`. No `RenderParity` fact covers these affordances, so the harness cannot catch the NFR6/AC#2 fidelity loss. Owner routed this to Story 6.5, whose host-aware theming + explicit-helper-actions scope already owns the webview interaction chrome — fold the keyboard/focus affordances into the bridge there.
- **Concurrent same-repo `specscribe webview` spawns collide on the scratch dir.** `Commands.cs` `RedirectOutputToScratch` keys the temp output only by a hash of the repo root, so two spawns for the same project (two VS Code windows, or a manual CLI run alongside the extension) `File.WriteAllText` the same files concurrently → IOException fails one render. The in-panel `loading` coalescer only prevents overlap within a single panel. Uncommon and retriable; give each spawn a unique subdir (or a lock) when this is next touched.
- **Story artifact deleted mid-render aborts the whole webview bundle.** `SiteGenerator.BuildStoryPageFragments` → `MarkdownConverter.ReadAllTextShared` (reached from `RenderWebviewSurfaces`) reads a path cached in `_storyArtifactsById`; if the `.md` is deleted between `GenerateAll()` and `RenderWebviewSurfaces()` the read throws `FileNotFoundException` and fails the entire bundle rather than degrading that one story to a placeholder (as the HTML path's per-epic try/catch does). Sub-second same-process window; next refresh recovers. Mirror the placeholder fallback when this seam is next touched.
- **Renderer timeout SIGTERM + unbounded stdout.** `extension.ts` `runRenderer` uses `proc.kill()` (default SIGTERM, may not reliably terminate the `dotnet` host on Windows → orphaned process) and accumulates stdout with no size cap. Not fatal; harden the kill (SIGKILL escalation) and cap the buffer when the shim is next touched.
- **Scratch key case-folds the repo root.** `RedirectOutputToScratch` hashes `RepoRoot.ToUpperInvariant()`, so two distinct repos differing only in path case map to the same scratch dir on a case-sensitive filesystem (Linux). Negligible in practice; drop the case-fold (or use the OS path comparer) when this is next touched.

## Deferred from: code review of story-6-7 (2026-07-11)

- source_spec: `6-7-json-and-spa-delivery-adapter.md`
- **Watch-mode `_spaCapture`/bundle can drift from the real static file set on doc rename or delete.** `_spaCapture` entries are only explicitly removed on doc deletion (`SiteGenerator.cs:295`); a rename that changes a doc's output-relative path mid-watch has no guaranteed purge of the stale old-path entry, so a rebuilt SPA bundle could include an orphaned page no longer part of the static site. Pre-existing watch/doc-lifecycle behavior, not introduced fresh by this diff; revisit when the rename/removal path is next touched.
- **`MaxPagesPerChunk` (75) batch-splitting boundary has zero test coverage.** `SiteGeneratorSpaTests.GenerateWithSpa_EmitsABoundedFewFiles_FarFewerThanPages` only exercises a small fixture; the "split oversized groups into numbered `-2.json` files" branch in `SpaDelivery.BuildDataFiles` is never exercised at the 75-page boundary. Add a boundary test when next touching chunking.
- **Chunk-batch assignment depends on unstated stable enumeration order.** `SpaDelivery.BuildDataFiles`/`SiteGenerator.BuildSpaBundle` assign pages to batch files in `_docs.Values` iteration order; a future change to upstream enumeration could shuffle which numbered chunk a page lands in between otherwise-identical generations, which matters for teams diffing committed generated output. Cosmetic; note if generated output is ever committed to git.

## Deferred from: code review of story-6-6 (2026-07-10)

- source_spec: `6-6-delivery-architecture-and-distribution-spike.md`
- **Client render/interaction perf at scale unmeasured (AC #1) → carry into Story 6.7.** The 6.6 spike measured client render only on this small repo (fetch 35 ms / render ~7–8 ms) and extrapolated the Epic-7 blow-up for file count alone (+863 → ~1,060 files). Owner decision 2026-07-10: not load-bearing for the re-affirm-C# outcome, so the large-dataset render/interaction measurement belongs on **Story 6.7 (JSON+SPA delivery adapter)** where the adapter is built — make it an explicit AC/task there.
- **Out-of-scope planning edits rode along in the spike's commit range.** AC #6 constrains what lands on `main` to the decision's own artifacts (ADR 0006, README index, 0005 amend-note, story record, sprint-status update for 6.6). The `1c9270b`..`HEAD` range also carries an `8-4-state-aware-next-step-command-surface` flip (backlog→ready-for-dev in `sprint-status.yaml`), `epics.md` edits, the 6-4/6-5 story-file edits, and a dependabot `spike/vscode` esbuild bump (0.24.2→0.28.1). These are already committed on `main` and are most likely intentional planning bundling, not spike leakage — none touches `src/**` or `tests/**`. Left as-is; if a future review wants a clean per-story boundary, split planning-status edits from spike close-out commits.

## Deferred from: code review of story-6-1 (2026-07-10)

- source_spec: `6-1-shared-view-model-contract-for-html-and-webview-adapters.md`
- **`SpecScribe.csproj`'s `BuildDate` MSBuild stamp is non-deterministic.** `$([System.DateTime]::UtcNow.ToString('yyyy-MM-dd'))` is evaluated at every compile, so two builds of the identical commit on different days produce different assembly bytes — a build-determinism tradeoff. Revisit if reproducible builds become a requirement.
- **`ProductMetadata.IsPrerelease`/`CommitHash` have no format validation.** `Version.Contains('-')` and `sha[..7]` (no hex check) are naive but low-risk since the version string is developer-controlled via csproj. Add validation if the version source ever becomes less trusted.
- **ADR-extraction rework, `BmadArtifactAdapter.TryParse` hardening, and `ForgeOptions` README exclusion predate Story 6.1's actual diff window** (landed in an earlier "4.x review" commit, not this story's work) — surfaced during 6.1's review only because the review diff spanned more than this story's own commits. Not actionable via 6.1; revisit if that earlier commit's changes are reviewed directly.

## Deferred from: code review of story-4-8 (2026-07-10)

- source_spec: `4-8-generation-diagnostics-and-configuration-log-page.md`
- ~~**`UnrecognizedTopLevelFolders`'s well-known-folder set omits `adrs`/`retros`.**~~ **Resolved 2026-07-18 as misdiagnosed** (`spec-close-known-index-groups-misdiagnosis`). `UnrecognizedTopLevelFolders` walks `SourceRoot` only; ADRs live on separate `AdrSourceRoot` (`docs/adrs`) and never enter `sourceRelatives`; retros live under already-well-known `implementation-artifacts/`. Adding `adrs`/`retros` to `KnownIndexGroups` would be a no-op for normal BMad layout and would not unlock Story 4.8 all-clear. Guarded by `GenerateAll_NormalBmadLayout_DoesNotEmitUnrecognizedNoticeForAdrsDocsOrRetros` (asserts no skipped `adrs/`/`docs/`/`retros/` structure notices on the normal separate-root fixture).

## Deferred from: code review of story-3-6 (2026-07-09)

- source_spec: `3-6-refinement-funnel-on-the-dashboard.md`
- **No-href coverage card lost its focus tooltip for keyboard/AT users.** When `tabindex="0"` was removed from the present-but-no-href coverage-card `<div>` (`src/SpecScribe/Charts.cs:853-856`), the card became non-focusable, so the body-level `js-tip` tooltip — which fires on `focusin` (`specscribe.js`) — can no longer be reached without a mouse. The tooltip carries the decision-journal (`.memlog`) date, which is **not** duplicated in the card body (the body shows only the primary source mtime), so keyboard/AT users lose that secondary date that hover users still get. The justifying code comment ("tooltip content is already present in the card body … either way") is inaccurate. Scoped to the rare present-without-href state (page failed to generate) and to out-of-scope Story 3.3 code incidentally touched here. When this card is next revisited, decide between keeping it focusable (accepting a non-actionable tab stop) vs. surfacing the memlog date in the card body.
- **Story markdown AC/Tasks/Dev Notes are stale relative to the shipped pivot.** AC #1, Subtasks 1.2/1.3, and the Dev Notes still describe the pre-pivot "requirements maturation" design (epics → drafted stories → stories with a task plan → tasks, sourced from `EpicsTotal/StoriesTotal/StoriesWithArtifact/TasksTotal`). Only the Change Log's third entry records the owner-directed pivot to the cumulative "Story Pipeline" (Drafted → Ready for dev → In development → In review → Done) that the code actually implements. Against the literal written AC the code would "fail"; against the documented final intent it is correct. Update the story's AC/Tasks/Dev Notes to match the shipped design so the artifact is internally consistent for traceability.

## Deferred from: code review of story-1-2 (2026-07-06)

- ~~**Case-insensitive requirement-ID matching is not actually implemented.**~~ **Resolved 2026-07-18** (`spec-epic1-deferred-debt-cleanup`). `RefPattern` now compiles with `RegexOptions.IgnoreCase` so lowercase/mixed-case known FR/NFR/UX-DR tokens linkify while authored casing is preserved in link text. Pinned by `Linkify_LinksLowercaseAndMixedCaseKnownIds_PreservingAuthoredCasing` / `Linkify_LeavesUnknownLowercaseIdsAlone`.
- ~~**Multi-digit partial-token boundary is unpinned.**~~ **Resolved 2026-07-18** (`spec-epic1-deferred-debt-cleanup`). Regression pin `Linkify_DoesNotPartialMatchMultiDigitIds` asserts `FR60` stays plain when only `FR6` is known and vice-versa (alongside the existing non-digit adjacency test).

## Deferred from: code review of story-1-3 (2026-07-06)

- ~~**Inconsistent `scroll-margin-top` offsets for sticky-nav clearance.**~~ **Resolved 2026-07-18** (`spec-epic1-deferred-debt-cleanup`). `.ac-criterion`, `.req-index .section-divider[id]`, and `.code-line` now use `scroll-margin-top: var(--nav-offset)` (currently 6.5rem / mobile 5.5rem) alongside the other sticky-nav clearances — no parallel rem hardcodes remain for that intent.
- ~~**Diff carries changes unrelated to Story 1.3 scope.**~~ **Resolved 2026-07-18** (`spec-epic1-deferred-debt-cleanup`). Closed as accepted historical scope bleed: the Pages deploy-retry workflow and README landed in dedicated commit `43c87cd` (ancestor of Story 1.3), remain intentional, and need no revert or split after the long-merged story.

## Deferred from: code review of spec-home-next-steps-label-and-code-review (2026-07-06)

- source_spec: `spec-home-next-steps-label-and-code-review.md`
- **Retrospective fallback can mislabel a project that has review work.** In `BmadCommands.ForProject` (`src/SpecScribe/BmadCommands.cs`), the project "Next Steps" panel falls back to a project-wide retrospective suggestion when `suggestions.Count == 0`, printing "Every epic is drafted and every story detailed." If a module does **not** expose a `code-review` command (so the new review-prompt loop adds nothing) and a story is sitting in `review` status with no other actionable work (no ready/active story, no undrafted epic story, no pending epic), the fallback still fires — claiming the project is fully detailed while a change awaits review. Pre-existing behavior (review status never fed the home panel before this change) and narrow (needs a code-review-less module). Evidence: `suggestions.Count == 0` is the sole fallback guard and the review loop is a no-op when `Command("code-review")` is null. Guard the fallback on "no review work either" when this panel is next touched.

## Deferred from: code review of story-1-4 (2026-07-06)

- ~~**Detail-page `<h1>` titles are emitted unescaped.**~~ **Resolved 2026-07-18** (`spec-epic1-deferred-debt-cleanup`). Superseded by Story 6.2: epic/story titles are opaque `TitleHtml` from `MarkdownConverter.RenderInline`, emitted verbatim into `<h1>` (and chips/cards). Wrapping in `PathUtil.Html` would double-escape markdown. Pinned by `RenderEpicBody_EmitsTitleHtmlInH1_WithoutPathUtilDoubleEscape`.
- ~~**Heatmap dates use ambient culture instead of `InvariantCulture`.**~~ **Resolved** (triaged 2026-07-08, `spec-epic1-heatmap-debt-triage`). Verified during the Epic 1 heatmap-debt triage: the heatmap was refactored (Story 3.1 consolidation) so every date now routes through the invariant helpers `Charts.D` (`yyyy-MM-dd`) and `Charts.DReadable` (`ddd, MMM d, yyyy`), both hard-coded to `InvariantCulture`, and month labels use `InvariantCulture` — no ambient-culture date format remains in `Charts.cs`. Pinned by `ChartsTests.CommitHeatmap_FormatsDatesWithInvariantHelpers`.

## Deferred from: code review of 1-5-dashboard-insight-polish-and-visual-truthfulness (2026-07-06)

- ~~**`AppendEpicStatusPanel` silently drops epics with an unmapped roll-up class.**~~ **Resolved 2026-07-06** as part of `spec-sunburst-epic-focus-and-ready-rollup`: adding the `ready` tier to `ForEpic` would have triggered this drop, so `AppendEpicStatusPanel` now buckets all five `ForEpic` outputs (done/active/ready/drafted/pending). The donut and its center fraction no longer disagree for any class `ForEpic` currently returns. **Hardened 2026-07-08** (`spec-epic1-heatmap-debt-triage`): the five buckets are no longer hand-listed — `AppendEpicStatusPanel` now iterates the single `StatusStyles.EpicStages` list (mirroring `StoryStages`), so a future `ForEpic` tier cannot silently drop. Guarded by `StatusStylesTests.EpicStages_CoversEveryForEpicOutputAndEachHasALabel`.
- ~~**`HeatLevel` collapses the scale when `maxCount <= 1`.**~~ **Resolved 2026-07-08** (`spec-epic1-heatmap-debt-triage`). `Charts.HeatLevel` returned level 4 (darkest) whenever `maxCount <= 1`, so a uniform one-commit-per-active-day history rendered every cell at maximum intensity — indistinguishable from heavy activity, a visual-truthfulness violation made common by the Story 1.5 E1 ~15-week window. Now returns level 1 (light) for that degenerate case; graded histories (`maxCount >= 2`) keep the unchanged ratio bucketing. Guarded by `ChartsTests.CommitHeatmap_UniformSingleCommitHistoryRendersLightNotMaxed` / `_GradedHistoryStillReachesLevel4ForBusiestDay`.

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

- ~~**Single-spec-kernel assumption in nav quick-link and index-card title.** `SiteNav.Build`'s `specKernelHub` lookup (`src/SpecScribe/SiteNav.cs:111-112`) uses `FirstOrDefault` to pick one `specs/*/SPEC.md`, so a project with more than one spec-kernel bundle only gets a nav quick-link to whichever one enumerates first; `HtmlTemplater.IndexCardTitle` (`src/SpecScribe/HtmlTemplater.cs:697-700`) similarly rewrites the card title to the fixed string "SPEC — Canonical Contract" for any doc with an `id:` starting `SPEC-`, so two kernels would render identical, indistinguishable index cards. Out of AC scope, single-kernel only today — ACs describe exactly one spec kernel per project, this repo has one, no current use case needs more. Revisit if a second kernel is ever authored.~~ **RESOLVED 2026-07-18** (`spec-epic2-deferred-debt-cleanup`): nav half fixed — `SiteNav.Build` now emits one Spec quick-link per `specs/**/SPEC.md` (plain "Spec" for one, `Spec — {folder}` for two+). The index-card half is separately obsolete: the home dashboard's index-card grid (and `IndexCardTitle`) is gone after a later declutter pass — no remaining call site to fix. [`SiteNav.cs`]

## Deferred from: code review of story-2-4 (2026-07-08)

- ~~**`FindByFileName` silently keeps only the first doc matching a well-known filename.** `HtmlTemplater.FindByFileName` (`src/SpecScribe/HtmlTemplater.cs:689-691`) uses `FirstOrDefault`, so a project with two `prd.md` (or `DESIGN.md`/`EXPERIENCE.md`/`brief.md`) files anywhere under `planning-artifacts/` promotes only the first into the primary/paired treatment; every other same-named doc silently falls into the generic "others" grid with no diagnostic. Graceful (not broken), and the code comment already flags "Epic 4 will generalize" as the eventual multi-framework fix point. No test covers the duplicate case today. Revisit if a project surfaces more than one doc per well-known filename.~~ **RESOLVED 2026-07-18** (`spec-epic2-deferred-debt-cleanup`): `HtmlTemplater.FindByFileName` is gone — the well-known-filename first-wins pick now lives in `SiteNav.Build`'s module-doc loop, which keeps alphabetical OrdinalIgnoreCase first-wins but no longer drops the duplicate silently: one `AdapterDiagnosticCategory.Skipped` diagnostic names the chosen path and how many siblings were skipped. [`SiteNav.cs`]
- ~~**`SprintSourcePath` picks the alphabetically-first `sprint-status.yaml` with no diagnostic and no try/catch.** `SiteGenerator.SprintSourcePath` (`src/SpecScribe/SiteGenerator.cs:566`) uses `Directory.EnumerateFiles(..., AllDirectories)` with no exception handling (an inaccessible subdirectory anywhere under `SourceRoot` would throw and abort generation) and no warning when more than one `sprint-status.yaml` exists (a monorepo/multi-module layout would silently render an arbitrary one). This is Story 2.3 scope (sprint tracking), not Story 2.4's — flagged here because it surfaced during the 2.4 review. Revisit when Story 2.3's area is next touched.~~ **RESOLVED 2026-07-18** (`spec-epic2-deferred-debt-cleanup`): sprint discovery now lives in `BmadArtifactAdapter.IngestSprint` (relocated from `SiteGenerator.SprintSourcePath` by Story 4.1), wrapped in `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)` → no candidates for this pass; alphabetical OrdinalIgnoreCase first-wins is unchanged but a 2+ match now emits one `Skipped` diagnostic (chosen path + count). [`BmadArtifactAdapter.cs`]

## Deferred from: code review of story-2-5 (2026-07-08)

- ~~**`Icons.ForConcept` key/label dual-representation for ampersand labels.** `HtmlTemplater.cs:481` calls `Icons.ForConcept("Direct & Quick-Dev Work")` (raw ampersand) right next to the separately hand-typed, HTML-escaped display text `Direct &amp; Quick-Dev Work`. Nothing enforces the two literals stay in sync; drift between them silently drops the icon (graceful degrade, no crash/broken markup). Extract to a shared constant when this section header is next touched.~~ **RESOLVED 2026-07-18** (`spec-2-5-deferred-iconography-hardening`): home-band call site already gone; orphaned `"Direct & Quick-Dev Work"` glyph + InlineData removed; stale DashboardView doc fixed; IconKey≠DisplayLabel / `PathUtil.Html` remains the ampersand rule. [`Icons.cs` / `DashboardView.cs`]
- ~~**Weakened test assertions no longer prove icon+label share one span.** Across `HtmlTemplaterTests.cs` and sibling test files, single strict `Assert.Contains("<span class=\"status-badge done\">Final</span>", html)`-style assertions were split into two independent checks (class-presence + `>Label</tag>` suffix) to accommodate the new icon markup between them. This no longer proves the icon and label live inside the *same* span — a future markup-nesting regression could still pass. Tighten back to single combined assertions (e.g. regex or a class+icon+label helper) when these tests are next touched.~~ **RESOLVED 2026-07-18** (`spec-2-5-deferred-iconography-hardening`): StatusStyles Badge unit tests + Requirements coverage-card drafted assert use full `Badge(...)` / combined class+icon+label Contains. [`StatusStylesTests.cs` / `RequirementsAndProgressTests.cs`]
- ~~**`StatusStyles.Badge(cssClass, label)` interpolates `cssClass` without HTML-escaping.** `StatusStyles.cs:164` escapes `label` via `PathUtil.Html` but not `cssClass`. Latent only — every current caller feeds `cssClass` from a closed, fixed status-vocabulary set (`done/active/review/ready/drafted/pending/deferred`) — but there's no guard preventing a future caller from passing unsanitized input. Escape `cssClass` too when this seam is next touched.~~ **RESOLVED 2026-07-18** (`spec-2-5-deferred-iconography-hardening`): `Badge` 3-arg overload escapes `cssClass` via `PathUtil.Html`; hostile-class unit test pins attribute safety. [`StatusStyles.cs`]
- ~~**Icon "no hard-coded hex" tests only assert absence of `#`.** `IconsTests.cs`/`StylesheetTests.cs` check `DoesNotContain("#", svg)` / the `.ss-icon` CSS slice, which would still pass a glyph using a named color (`fill="black"`) or `rgb()`/`hsl()` value — it doesn't verify `currentColor` is actually wired. Strengthen to a positive assertion (`stroke="currentColor"` present, or a regex excluding all color literals) when this test file is next touched.~~ **RESOLVED 2026-07-18** (`spec-2-5-deferred-iconography-hardening`): `AssertWellFormedIcon` requires `stroke="currentColor"`, bans `#`/`rgb(`/`rgba(`/`hsl(`/`hsla(`/named fill|stroke; `.ss-icon` CSS rule also bans rgb/hsl family. [`IconsTests.cs` / `StylesheetTests.cs`]
- ~~**No reverse-coverage test that every emitted label has a curated icon.** `IconsTests.ForConcept_EveryKnownLabelReturnsAGlyph` only checks the labels hardcoded into its own `[InlineData]` table; nothing walks `SiteNav`/`ModuleContext`/index section-title sources to confirm every label the generator actually emits has a matching `Icons.ForConcept` case. A new nav item or index band added later would silently render icon-less with no test failure. Add the reverse-direction test when `Icons.ForConcept`'s call sites next change.~~ **RESOLVED 2026-07-18** (`spec-2-5-deferred-iconography-hardening`): `ForConcept_EveryEmittedLabelHasAGlyph` MemberData walks SiteNav (full fixture), ModuleContext BMad docs, ArtifactCoverage ConceptIconKeys, nav groups, work-mode pills, evidence + change-surface keys. [`IconsTests.cs`]

## Deferred from: code review of story-2-6 (2026-07-08)

- ~~**`.md-comment` contrast (~3.94:1, `--ink-light` on `--parchment-dark`) falls short of WCAG AA 4.5:1 for normal-size text.** `src/SpecScribe/assets/specscribe.css:391`. Measured via relative-luminance calc on the two hex tokens (`#7a6250` on `#e8d5b0`). Not a new regression — the same token pairing is already used pre-existing in `.epic-status.pending` (`:850`) and `.status-badge.pending`/`.status-badge.deferred` (`:1080`, `:1890`), so this is inherited design-system debt, not something Story 2.6 introduced. Revisit as part of a broader muted-text-token contrast pass.~~ **RESOLVED 2026-07-18** (`spec-epic2-deferred-debt-cleanup`): already fixed by a prior branding pass — `--ink-light` is now `#6b5442`, clearing WCAG AA 4.5:1 on both parchment surfaces, pinned by `StylesheetTests.MutedInk_ClearsWcagAA_OnBothParchmentSurfaces`. No further CSS change made here (ledger-close only; `--status-deferred` untouched). [`specscribe.css`]
- ~~**Renderer-swap scan repeats on every `RenderInline`/`RenderBlock` call.** `UseCommentAnnotations` (`src/SpecScribe/MarkdownConverter.cs:92-107`) does two `OfType<T>().FirstOrDefault()` + `Remove`/`Add` passes on `renderer.ObjectRenderers` per call, on top of the pre-existing `UseMermaidCodeBlocks` pass. `RenderInline`/`RenderBlock` are invoked per AC line, per goal, per title (9+ call sites in `EpicsParser`/`RequirementsParser`), so this doubles per-fragment setup cost. Pre-existing pattern from Story 2.6's mermaid precedent, not a new inefficiency this story introduced — but the story compounds it with a second wrapping pass. Revisit if fragment-heavy pages show measurable render-time cost.~~ **RESOLVED 2026-07-18** (`spec-epic2-deferred-debt-cleanup`): `UseMermaidCodeBlocks`/`UseCommentAnnotations` are now installed once per `MarkdownPipeline.Setup(renderer)` call via a `DocumentRendererWrappersExtension : IMarkdownExtension` registered on the static `Pipeline`, not re-applied as a separate per-call pass in `RenderDocumentHtml`. Fragment fidelity (mermaid fences, HTML-comment annotations) is unchanged and pinned by the existing `MarkdownConverterTests`. [`MarkdownConverter.cs`]

## Deferred from: code review of story-2-3 (2026-07-08)

- ~~**`SprintStatusParser.ExtractTopLevelBlock` silently truncates on a duplicate top-level key.** `src/SpecScribe/SprintStatusParser.cs` — if a malformed `sprint-status.yaml` had two `development_status:` (or `action_items:`) blocks, the block-slicer treats the second occurrence as the end of the first block rather than erroring or merging, silently dropping entries after it. Requires malformed hand-authored yaml (duplicate top-level key); low likelihood. Revisit if the hand-rolled block-slicer is next touched.~~ **RESOLVED 2026-07-18** (`spec-epic2-deferred-debt-cleanup`): a duplicate top-level `key:` header occurring after the block has started now fails the block closed (returns `null`) instead of truncating, so a malformed duplicate degrades to the existing "no usable map" path rather than silently dropping entries. [`SprintStatusParser.cs`]
- ~~**`SprintStatusParser.ExtractLastUpdated` breaks on a YAML block-scalar `last_updated` value.** `src/SpecScribe/SprintStatusParser.cs` — a `last_updated: >` (or `|`) block-scalar form would capture the literal indicator character instead of the date, since the regex only reads the same-line value. Low-likelihood authoring pattern for a date field. Revisit if the hand-rolled parser is next touched.~~ **RESOLVED 2026-07-18** (`spec-epic2-deferred-debt-cleanup`): a bare `>`/`|` block-scalar indicator (optionally chomp-marked with `+`/`-`) now degrades `LastUpdated` to `null` instead of surfacing the indicator character; parsing the folded/literal body itself is intentionally out of scope (null-degrade is enough for this non-load-bearing metadata field). [`SprintStatusParser.cs`]

## Deferred from: code review of story-3-1 (2026-07-08)

- ~~**`ParseChangedFiles` undercounts renamed/moved files and is case-sensitive.** `src/SpecScribe/GitMetrics.cs:193-214` — the name-only git call has no `-M`/`--find-renames`, so a renamed file's history splits across an old-path delete and a new-path add, undercounting its true change frequency; the path dictionary is also case-sensitive, so `Foo.cs`/`foo.CS` on a case-insensitive filesystem count as two entries. Low-impact polish on the "top changed files" ranking, not required by AC #1.~~ **RESOLVED 2026-07-18** (`spec-3-1-deferred-debt-cleanup`): name-only call uses `-M` so a rename commit counts once under the destination path; `ParseChangedFiles` also defensively collapses arrow/brace rename forms via `ResolveRenamedPath` (those forms are from name-status/numstat, not `--name-only`). Case half kept Ordinal (git-layer path-key consistency). [`GitMetrics.cs`]
- ~~**"Top changed files" and "commits in the last 30 days" use different windows with no distinguishing label.** `src/SpecScribe/GitMetrics.cs:64`, `src/SpecScribe/Charts.cs` `GitPulsePanel` — the file ranking is bounded by commit count (`-n 200`, per deferred-work.md's existing uncapped-history caution), while the adjacent signal is a 30-calendar-day window. On a low-activity repo 200 commits could span years; on a high-activity repo, days. Both figures sit in the same panel implying the same "recent" lens. Worth a distinguishing label in a future pass.~~ **RESOLVED 2026-07-18** (`spec-3-1-deferred-debt-cleanup`): title + empty copy name the last-200-commits window; 30-day signal unchanged. [`Charts.cs`]
- ~~**`LastCommitTimestamp` assumes each day's commit list is strictly newest-first.** `src/SpecScribe/GitMetrics.cs:135-146` — true for git's default linear log order (documented in the code comment) but unverified for merge-commit/clock-skew scenarios, and no test pins the assumption. A wrong ordering would show a slightly-off "last commit" time, not a crash.~~ **RESOLVED 2026-07-18** (`spec-3-1-deferred-debt-cleanup`): max parseable HH:mm on the last series day (order-independent); unit-tested. [`GitMetrics.cs`]
- ~~**Git Pulse file-bar rows have no `aria-label` uniting label + bar + count.** `src/SpecScribe/Charts.cs` `GitPulsePanel` — each row is three separate spans; the exact count text already satisfies the "never color/size-only" truthfulness convention, but a single accessible-name grouping would read more coherently to a screen reader. Minor a11y polish.~~ **RESOLVED 2026-07-18** (`spec-3-1-deferred-debt-cleanup`): `<li aria-label="{path}: {n} change(s)">` + decorative track `aria-hidden`. [`Charts.cs`]
- ~~**`.md-comment`/`.md-comment-inline` CSS selectors were widened from `.doc-body`-scoped to global.** `src/SpecScribe/assets/specscribe.css:385-399` — bundled into the same commit as Story 3.1's work but is Story 2.6 cleanup, out of scope here. Flagged independently by two review layers as a latent global-class-collision risk; worth a follow-up look at what else might use `.md-comment` outside `.doc-body`.~~ **RESOLVED 2026-07-18** (`spec-3-1-deferred-debt-cleanup`): kept global; CSS comment documents emission hosts (`.doc-body`, story cards/leads, AC/inline fragments) and `.md-table` rationale — `.doc-body`-only would drop card styling. [`specscribe.css`]
- ~~**No end-to-end test for `GitMetrics.TryCompute`'s new field wiring.** `src/SpecScribe/GitMetrics.cs` — nothing proves `Last30DayCommitCount`/`LastCommitTimestamp`/`TopChangedFiles` are correctly wired from a real git invocation, or that a failing second (`--name-only`) call degrades to an empty list rather than nulling the whole pulse (the AD-4 contract). The pure helpers (`ParseChangedFiles`, `CountCommitsInLastDays`, `LastCommitTimestamp`) and the render-layer branches (`GitPulsePanel`, `CommitHeatmap(showHeadline: false)`) are directly tested; only the `TryCompute` shell-out wiring itself is not, since the codebase has no temp-git-repo test fixture to build one on. Revisit if such a fixture is added for another story.~~ **RESOLVED 2026-07-18** (`spec-3-1-deferred-debt-cleanup`): temp-repo `TryCompute` happy-path pins the three fields; second-call degrade remains the reviewed ternary (no new RunGit seam). [`GitMetricsTests.cs`]

## Deferred from: code review of story-3-2 (2026-07-08)

- ~~**`TryComputeDeep`'s `--numstat` call and `TryCompute`'s `--name-only` call are two separate git-log invocations, not the single shared code path the story's Dev Notes call for.**~~ **RESOLVED 2026-07-19** (`spec-3-2-deferred-debt-cleanup`): confirmed this is a Dev Notes ambiguity, not a bug — the "single git code path" language is scoped to the deep-git family (3.2/3.8/7.4/7.5) only. Merging with Story 3.1's separate, always-on, lighter `--name-only` call would risk the FR-10 performance gate for no functional gain on the default (deep-git off) path, so no merge was performed; both story record and code doc comments clarified instead. The two invocations still exist as designed — confirmed via direct read of `GitMetrics.cs` (`TryCompute`'s `-n 200 --name-only` call and `TryComputeDeep`'s `-n 300 --numstat` call remain separate, independently-bounded fetches). Whether the `--deep-git`-on path could reuse `TryComputeDeep`'s numstat data to skip `TryCompute`'s redundant `--name-only` call is a distinct, still-open optimization — see the new entry below (code-review follow-up, 2026-07-19) rather than being silently closed here. [`GitMetrics.cs`, `3-2-optional-deep-git-analytics-controls.md`]
- ~~**Structure nav/quick-link is gated on `sourceRelatives.Count > 0`, not on the tree build actually succeeding.**~~ **RESOLVED 2026-07-19** (`spec-3-2-deferred-debt-cleanup`): moot — `WriteStructure`/`ProjectTree`/`structure.html` no longer exist; Story 3.4 was retired and its tree surface replaced by Story 7.6's Code Map (`WriteCodeMap`/`CodeMap.BuildVariants`, gated on its own `_codeFiles.Count == 0` / empty-map check). Deleted the stale orphaned `WriteStructure` doc comment left stacked above `WriteCodeMap`'s real one in `SiteGenerator.cs`. Confirmed via `grep -rn "ProjectTree|AppendTreeNode" src/SpecScribe --include=*.cs` (2026-07-19): 0 hits outside historical doc-comment mentions noting the retirement. [`SiteGenerator.cs`]
- ~~**`Charts.AppendTreeNode` has no recursion depth cap.**~~ **RESOLVED 2026-07-19** (`spec-3-2-deferred-debt-cleanup`): moot — `AppendTreeNode`/`ProjectTree` no longer exist anywhere in `src/`, superseded by Story 7.6's `CodeMap` (squarified layout, no recursive tree-node append). Same grep confirmation as above. [`Charts.cs`]

## Deferred from: code review of spec-3-2-deferred-debt-cleanup (2026-07-19)

- source_spec: `spec-3-2-deferred-debt-cleanup.md`
  summary: When `--deep-git` is on, `TryCompute`'s `-n 200 --name-only` call and `TryComputeDeep`'s `-n 300 --numstat` call both fetch overlapping commit→file-set data as two separate git invocations; `TryComputeDeep`'s hotspots already contain per-file change counts that could serve `TryCompute`'s "top changed files" signal instead of a second git process, if the differing window sizes (200 vs 300) and rename-detection flags (`-M` on the name-only call, absent on the numstat call) were reconciled first.
  evidence: Blind Hunter + Edge Case Hunter (independently flagged) on the 3.2 deferred-debt cleanup review. Deliberately not attempted in that cleanup — it's a real design/perf question, not the Dev Notes wording ambiguity that cleanup resolved, and reconciling the window/rename mismatch is nontrivial cross-story surgery.
- source_spec: `spec-3-2-deferred-debt-cleanup.md`
  summary: Story 3.2's record still states elsewhere ("This story is FR-10 only... Story 3.1... is `ready-for-dev` but NOT yet merged") even though Story 3.1 has since landed — a stale cross-story status line left over from this story's original drafting.
  evidence: Blind Hunter, surfaced incidentally while reviewing the 3.2 deferred-debt cleanup; pre-existing, not caused by that cleanup's diff.

## Deferred from: code review of story-3.3 (2026-07-08)

- **No direct test for `SiteGenerator.BuildMemlogMap`'s ancestor-matching logic.** `src/SpecScribe/SiteGenerator.cs:957-999` — the pure `ArtifactCoverage.Build` memlog wiring is well tested with hand-built maps (`Build_MemlogEnrichmentAttachesToMatchingFamily`, `Build_MemlogIsStrictlyAdditive...`), but the directory-prefix/closest-ancestor selection that actually builds those maps — the one genuinely tricky piece (multiple candidate `.memlog.md` files, `StartsWith`-based containment, tie-breaking by directory-string length) — has zero coverage anywhere in the diff.
- **`HtmlTemplaterTests.RenderIndex_RendersPlanningCoveragePanelWithPresentDateAndMissingChip` isn't fully hermetic.** `tests/SpecScribe.Tests/HtmlTemplaterTests.cs` — the fixture builds `ArtifactCoverage` with a fixed `today`, but `HtmlTemplater.AppendDashboard` internally recomputes staleness against the real `DateTime.Now`, not that fixed date. The test passes today only because wall-clock time hasn't pushed the fixture's family past the 30-day staleness threshold, not because the date is actually pinned.

## Deferred from: code review of story-3.5 (2026-07-08)

- **Both epic-level and story-level `Sunburst` call `Charts.SunburstLegend` with an identical hardcoded 6-tuple array.** `src/SpecScribe/Charts.cs` — nothing keeps the two literal `("pending","Pending"), ("drafted","Drafted"), ...` arrays in sync; a future edit to one label and not the other would silently desync the two legends, and no test catches that divergence. Minor maintainability nit, no functional impact today.

## Deferred from: code review of story-3-8 (2026-07-09)

- **Commit body containing a literal `0x1F` control char could truncate numstat rows.** `src/SpecScribe/GitMetrics.cs:386-399` — `ParseNumstatRecords` splits each record on the `FieldSentinel` (`0x1F`); if a commit's free-text subject/body happened to embed that raw control byte, `fields.Length` would exceed 6 and only `fields[5]` is taken as the numstat block, silently dropping anything after it. Extremely unlikely in real commit text (non-printable control char), and the parser's never-throw contract still holds — silent undercount only, no crash.
- **Commits with unparseable dates diverge `CommitCount` from summed `Activity`.** `src/SpecScribe/GitMetrics.cs:469-470` — in `BuildInsights`, a commit whose `%ad` timestamp fails `DateTime.TryParseExact` is still counted in `insights.CommitCount`/`ContributorCount` but excluded from the per-day `Activity` series (`day is null` skips the `byDay` bump), so the two totals can diverge. Same rarity caveat as the control-char finding above — git always emits a well-formed date in this pipeline.
- **Churn sums every numstat row per commit while `Changes` dedups per commit-path.** `src/SpecScribe/GitMetrics.cs:482-486` — `accum.Added`/`accum.Deleted` accumulate unconditionally per numstat row, while `accum.Changes` and per-author attribution are deduped via `seenInCommit`. Documented as an intentional tradeoff in the code's own comment; only manifests when a rename/copy pair resolves to the same path within one commit, inflating churn without inflating the change-frequency count.
- **`:target`-revealed contributor panel has no explicit focus management for keyboard/AT users.** `src/SpecScribe/GitInsightsTemplater.cs:154` — activating a file row's fragment link reveals `.gi-contributors-panel` via pure CSS `:target`, but nothing moves focus into the newly-shown region for non-visual navigation. Mirrors the same gap in the pre-existing commit-heatmap and coupling-graph `:target` drill-downs, so it's inherited, not introduced by this story.
- **No test asserts a stale `git-insights.html` is removed after `--deep-git` is later disabled on the same output dir.** `tests/SpecScribe.Tests/SiteGeneratorGitInsightsTests.cs` — coverage gap only; `SiteGenerator.GenerateAll` already does a full recursive wipe of the output root on every run (`SiteGenerator.cs:55`), so a dangling hub page from a prior run cannot actually survive to the next.

## Deferred from: code review of story-3-7 (2026-07-09)

- source_spec: `3-7-requirements-flow-and-status-blocks.md`
- **Sankey layout doesn't scale height for very large requirement counts.** `Charts.RequirementFlow`'s `unitH = Math.Max(2.0, (usableH - gap * (maxNodes - 1)) / n)` floors at 2px, but the SVG `height`/`viewBox` is a fixed constant (`topPad + usableH + 26`) that never grows to compensate. Once `n` (or the node/gap count) is large enough to hit the floor — roughly 150+ requirements at current column widths — a column's total layout height can exceed `usableH`, overlapping the header/footer. At today's project scale (26 requirements) this is nowhere close to triggering. Pre-existing pattern shared with `CouplingGraph`/`RefinementFunnel`, not unique to this story. Revisit if a project's requirement count grows into that range, or when the shared custom-SVG-layout helpers are next touched.
- **Dashboard requirements panel lacks the per-epic/per-status text-equivalent that requirements.html has.** `HtmlTemplater.AppendRequirementsPanel` renders the tile grid + the Sankey on the home dashboard, but (unlike `requirements.html`) has no requirement cards below it. Screen-reader users on the dashboard get the whole-diagram `aria-label` (overall totals) and per-tile tooltips (per-requirement id/status), but not the per-epic breakdown a sighted user gets by hovering individual ribbons/nodes. Low severity — the totals and per-requirement status are still accessible as text, just not the epic-level detail. Revisit if the dashboard panel is next touched.

## Deferred from: code review of spec-sprint-board-card-tooltip-html-corruption (2026-07-09)

- source_spec: `spec-sprint-board-card-tooltip-html-corruption.md`
  summary: `RequirementLinkifier` (`src/SpecScribe/RequirementLinkifier.cs`) has the same attribute-corruption exposure that was just fixed in `StoryEpicLinkifier` — an FR/NFR mention sitting inside a non-anchor tag's attribute value (e.g. a tooltip or aria-label) would get a raw `<a>` injected into it, breaking the tag.
  evidence: `RequirementLinkifier.AnchorSplit` only protects `<a>…</a>` spans (`RequirementLinkifier.cs:13-15`), unlike the fixed `StoryEpicLinkifier.ProtectedSplit`, which now also skips every standalone tag so only real text nodes are scanned. Not caused by this change — pre-existing since the class was written — and not currently known to be triggered by any live template (no site content today puts "FR"/"NFR" text inside a non-anchor attribute), so it's latent rather than reproducible. Apply the same `<[^>]*>` catch-all alternative to `RequirementLinkifier.AnchorSplit` when that seam is next touched.

## Deferred from: code review of story-4-2 (2026-07-10)

- source_spec: `4-2-decouple-rendering-from-personal-project-structure-assumptions.md`
- ~~**`AdrLinkRewriter`'s `rootPrefix`/`climbToAdrRoot` arithmetic only holds for the current one-level-deep ADR recursion bound.**~~ **Resolved 2026-07-19** (`deferred-adrlinkrewriter-climb-arithmetic`). `src/SpecScribe/AdrLinkRewriter.cs` now carries a `Debug.Assert` in `MapTarget` tying the arithmetic to the one-level bound directly: it fails loudly if `rootPrefix` ever exceeds `"../../"` (i.e. if `SiteGenerator.EnumerateAdrFiles` is ever deepened past one level without this formula being revisited), instead of silently mis-resolving README links.
- ~~**Diagnostic-severity bucketing conflates benign "unrecognized top-level folder" notices with genuine anomalies under the same `Skipped` outcome.**~~ **Resolved 2026-07-19** (`deferred-diagnostic-severity-bucketing`). Added `AdapterDiagnosticCategory.Informational` (`src/SpecScribe/AdapterDiagnostic.cs`) and switched `UnrecognizedTopLevelFolders` (`src/SpecScribe/SiteGenerator.cs`) to emit it instead of `Unsupported`. The Story 4.8 diagnostics page now gives it its own `DiagnosticSeverity.Info` → `.status-badge.diag-info` badge (dashed, no fill — `src/SpecScribe/DiagnosticsTemplater.cs`, `src/SpecScribe/assets/specscribe.css`), so a benign structural notice no longer shares a category *or* a severity color with a genuine per-artifact ingestion failure (e.g. an unusable `sprint-status.yaml`, still `Unsupported`/Warning). Covered by new tests in `DiagnosticsTemplaterTests` and `SiteGeneratorAdapterTests`.

## Deferred from: code review of story-6-3 (2026-07-11)

- source_spec: `6-3-vs-code-integration-spike.md`
- _All items are throwaway spike-code defects under `spike/vscode/` — carry to Story 6.4 (the webview runtime that supersedes this spike), not fixable-in-place with value. The ADR-accuracy patches are tracked separately in the story's Review Findings, not here._
- **Default renderer spawn path is never populated + dead `exe` fallback.** `spike/vscode/src/extension.ts:120-125` — default spawns `dotnet <extensionPath>/renderer/specscribe-webview-spike.dll`, but no build step places a dll there (`npm run build` bundles only `dist/extension.js`; the README builds the renderer to `spike-out/`). The computed `exe` path (line 120) is never referenced in command selection. Consequence: the one remaining manual `F5` verification requires `SPECSCRIBE_SPIKE_RENDERER` to be set; the "just works" default resolves nothing. The 6.4 runtime owns real renderer-path wiring (Story 16.5 owns bundling).
- **Panel-reuse re-runs the whole `openStatus` handler.** `spike/vscode/src/extension.ts:49-88` — `panel ??= createPanel` reuses the panel, but the body re-registers `onDidReceiveMessage`, creates a second `FileSystemWatcher`, and re-assigns `webview.html` (a full reset, contra AC#2 "never resets"). Register watcher/handler once in `createPanel`; early-return + `reveal()` on reuse. (Re-confirmed from the prior review pass.)
- **Live-push watcher glob decoupled from the renderer's resolved source root.** `spike/vscode/src/extension.ts:72-73` watches `RelativePattern(workspaceFolders[0], '_bmad-output/**/*.md')` while the renderer resolves `SourceRoot` by walking up from `cwd` (`Program.cs:30`). When the opened folder is a subdir of the repo (or a multi-root workspace where SpecScribe isn't folder[0], or a relocated source root), first paint works but the watcher never fires — live-push is silently dead, invalidating the AD-8 evidence for that layout. Derive the watched path from the resolved source root. (Re-confirmed.)
- **Overlapping debounced re-renders race with no generation guard.** `spike/vscode/src/extension.ts:75-82` — the 400 ms debounce only coalesces sub-400 ms bursts; two saves within one ~1.8 s render window spawn concurrent renders, and a slow one can overwrite fresher content into the shared `cache`. Add a generation/in-flight token. (Re-confirmed.)
- **Surface-switch `await load()` has no error handling.** `spike/vscode/src/extension.ts:52-58` — asymmetric with `refresh`'s try/catch; a renderer failure on toggle is an unhandled rejection with no user feedback.
- **`withRuntime` does a whole-document token `split/join`.** `spike/vscode/src/extension.ts:110-113` — rendered content containing the literal `__NONCE__`/`__CSP_SOURCE__` tokens would be silently rewritten; undercuts the "exactly two opaque strings" framing (negligible probability today).
- **Live-push scoped to `*.md` only.** `spike/vscode/src/extension.ts:73` — edits to `sprint-status.yaml` and other non-`.md` inputs feeding the dashboard never trigger a refresh (the spike AC literally names `.md`, so the AC still passes).
- **No spawn timeout / kill.** `spike/vscode/src/extension.ts:127-142` — a hung renderer or `git` subprocess leaves the webview blank indefinitely; debounced refreshes pile up.
- **Renderer arg/enumeration edges.** `spike/vscode/renderer/Program.cs:27,33-38` — `--out <dir>`'s value is also matched by `FirstOrDefault(a => !a.StartsWith("--"))`, so `renderer --out X` with no project dir mis-reads `X` as the project dir (dead via the shim, which always passes `[dll, cwd]`); a missing `_bmad-output` silently renders an empty dashboard rather than signalling not-a-SpecScribe folder; an unreadable subdir aborts the entire `EnumerateFiles(..., AllDirectories)`.

## Deferred from: code review of story-6-5 (2026-07-11)

- source_spec: `6-5-host-aware-theming-and-explicit-helper-actions.md`
- **`.vscode-light` has no dedicated contrast-tuning block.** `src/SpecScribe/assets/specscribe-webview-theme.css` — the story's own design decision sanctions reusing today's warm-light accent values for the light webview theme, but its "must still be verified against the actual host light background" clause has no recorded check or test. Revisit as a manual/QA verification pass, not a code patch.
- **`SiteGeneratorWebviewTests.FullGenerateThenWebviewPass_LeavesSourceArtifactsUntouched` only compares file set + `LastWriteTimeUtc`, not content.** A write that preserves mtime would pass this "leaves source artifacts untouched" guard undetected. Strengthen with a content hash comparison when this test is next touched.
- **Live-refresh debounce spawns the renderer even after the panel is disposed.** `extension/src/extension.ts` — the `disposed` guard in the debounced refresh callback is checked only after `await load()` resolves, not before calling `load()`, so a refresh timer firing post-disposal still spawns a wasted `specscribe webview` child process (discarded result, no user-visible effect).
- **Test assertions in `WebviewHelpersTests`/`WebviewThemingTests` pin exact prompt wording with ordinal string matching** (e.g. `"Do NOT modify any files"`), coupling test stability to copy-editing rather than to the read-only behavioral contract they're meant to protect.
- **No test exercises `<`/`>` in `siteTitle` reaching the `data-ss-prompt` attribute** — only a double-quote case is covered, even though the same `PathUtil.Html` path is relied on to prevent markup injection there.

## Deferred from: spec-comment-block-rendering review (2026-07-11)

- source_spec: `spec-comment-block-rendering.md`
  summary: A block comment whose body quotes another `<!--` renders the inner marker as a literal escaped `&lt;!--` inside the `.md-comment` aside.
  evidence: Pre-existing (present at baseline `0a0d0f7`, ~59 site-wide occurrences, unchanged by the user-story-region fix): `CommentAnnotationRenderer.StripCommentMarkers` only strips the outer `<!--`/`-->` pair, so a nested opener in comments like `epics.md:1065`/`1077` survives escaping. Standalone/AC-region comment path, orthogonal to the user-story split. Fix belongs in the shared renderer (strip/normalize inner markers), not this spec.

## Deferred from: code review of story-6-10 (2026-07-12)

- source_spec: `6-10-editor-artifact-bridges-reveal-source.md`
- **`RepoRelative` silently returns an absolute path (not repo-relative) when the artifact isn't under `RepoRoot`.** `src/SpecScribe/SiteGenerator.cs:226` — `Path.GetRelativePath(_options.RepoRoot, absolutePath)` returns `absolutePath` unchanged when there's no common root (e.g. a misconfigured `RepoRoot`, or a different drive on Windows), silently violating the "always repo-relative" contract the TS-side `resolveWorkspacePath` depends on. The practical effect is the reveal button silently disappears (the TS guard rejects the absolute path via `path.isAbsolute`) rather than a diagnosable error. Low likelihood — `epicsFullPath`/`artifactFullPath` derive from the repo scan itself — but nothing asserts or surfaces the mismatch if it ever occurs. Revisit if `RepoRoot` resolution is ever made more permissive (e.g. cross-drive project configs).

## Deferred from: code review of story-6-9 (2026-07-12)

- source_spec: `6-9-native-project-outline-tree-view-and-status-bar.md`
- **`resolveWorkspacePath`'s containment guard doesn't dereference symlinks.** `extension/src/extension.ts` — the check is purely lexical (`path.resolve` + `startsWith`), so a symlink physically inside the workspace pointing outside it would pass containment while `showTextDocument` opens content outside the workspace. This helper is Story 6.10-owned (in-file tag `[Story 6.10]`); 6.9's `openSource` reuses it. Fix belongs with 6.10, still in-progress.
- **`toolCommandLine` doesn't escape embedded quote characters**, only whitespace-triggers quoting. `extension/src/extension.ts` — a `toolPath`/prefix-arg containing both a space and a `"` could produce a malformed staged terminal command. Story 6.8-owned (already reviewed/done); low severity since the command is staged, never executed.
- **`getOrCreateTerminal` reuses the one "SpecScribe" terminal by name only**, with no check for a still-running process. `extension/src/extension.ts` — staging a new Generate/Setup command into a terminal where `watch` is still consuming stdin could produce a confusing terminal state. Story 6.8-owned, not touched by 6.9.
- **`openGeneratedSite` has no containment guard on `configuredOutputRoot`**, unlike `resolveWorkspacePath`. `extension/src/extension.ts` — a resolved output root that escapes the repo root (e.g. an explicit `--output ../elsewhere`) would open external content unguarded. `ResolveConfiguredOutputRoot`/`openGeneratedSite` are Story 6.8-owned (AC #3, R2.4), already reviewed/done.
- **Multi-root workspaces get no detection/support when the SpecScribe project isn't `workspaceFolders[0]`**, with no user-facing explanation. `extension/src/extension.ts` — explicitly out of scope per the story text and in-code comments ("Multi-root support itself stays out of scope (Story 6.11)").

## Deferred from: quick-dev intent split — VS Code extension polish (2026-07-12)

- source_spec: none
  summary: Webview navigation breadth — export the doc pages (Readme, GDD, Narrative, Architecture, ADRs, Requirements index) as navigable webview surfaces so the page-header nav links work inside the VS Code panel instead of dead-ending.
  evidence: Split from the 2026-07-12 extension-polish intent (owner chose sidebar/command polish first). Today `specscribe webview` bundles only dashboard + epics + per-epic/per-story surfaces; the bridge script posts `navigate` for header links like GDD/Narrative and the shim rejects them (`extension.ts` "isn't part of the in-editor status view" toast), which the owner experiences as links that do nothing. Independently shippable: widen `SiteGenerator.RenderWebviewSurfaces` + the bundle, no extension-manifest changes required.

- source_spec: none
  summary: Stronger SpecScribe branding/iconography — a distinctive brand mark in the sidebar (activity bar/tree) and inside the rendered application (site + webview header), beyond the current two `extension/media` SVGs.
  evidence: Split from the 2026-07-12 extension-polish intent (owner chose sidebar/command polish first). Touches the C# renderer's chrome (nav/header), so it carries golden-fingerprint churn (see golden-diff normalization gotchas) and per the create-story-elicit-visual-intent lesson the owner should be offered 2-3 named design directions before any silhouette is built — deserves its own focused spec.

## Deferred from: owner F5 review of the VS Code experience (2026-07-12)

- source_spec: `spec-vscode-sidebar-shortcuts-and-story-command-quickpick.md`
  summary: Owner feedback after F5: "color use and contrast feels a little off in the VS experience" — run the deferred Story 6.5 webview-theme contrast/light-palette verification pass and tune the `.vscode-*` bridge (and the six `specscribe.status.*` tree accents, which must be re-tuned TOGETHER with the webview accents per Story 6.9's constraint) against real host themes.
  evidence: Story 6.5 shipped the theme bridge but its own spec deferred the light-palette check and contrast verification ("no dedicated `.vscode-light` contrast-tuning block", see the 6.5 deferred section above); the owner has now visually confirmed the debt matters in daily use. Seat alongside (or inside) the deferred branding/iconography goal so accents and brand marks land as one visual pass.

## Deferred from: code review of spec-vscode-sidebar-shortcuts-and-story-command-quickpick (2026-07-12)

- source_spec: `spec-vscode-sidebar-shortcuts-and-story-command-quickpick.md`
  summary: `StatusStyles.ForStory` and `BmadCommands.ForStory` classify compound statuses in different precedence order (StatusStyles checks "review" before "ready"; BmadCommands checks "ready" first), so a status like "ready-for-review" renders as stage In review while its next-step commands come from the ready branch (dev-story only, no code-review).
  evidence: Pre-existing divergence between `StatusStyles.cs` and `BmadCommands.cs:229-252` — the story page has always paired the same two classifiers, but the new tree node (stage icon + Quick Pick on one element) is the first surface to juxtapose them visibly. Flagged independently by both review layers. No fixture uses a compound status today. Fix belongs in a shared status-classification seam (route BmadCommands.ForStory's branching through StatusStyles.ForStory), not in this spec's additive projection.

## Deferred from: story-6-11 file-change reactivity hardening (2026-07-12)

- source_spec: `6-11-file-change-reactivity-hardening.md`
- **Core `watch`-mode cannot re-brand on a `config.toml` project-name change without a restart.** `src/SpecScribe/ForgeOptions.cs` resolves `SiteTitle` once at `Resolve()` time into the long-lived `_options`; the new data-source route (`SiteGenerator.RegenerateFromDataSource`) re-renders on a `_bmad/config.toml` change but the in-memory `SiteTitle` stays fixed, so a *renamed project* keeps its old name in `specscribe watch` until the process restarts. **The extension is unaffected** (it re-spawns `specscribe webview` per refresh, re-running `ForgeOptions.Resolve()`), so the "panel stays live" promise holds where it matters. Re-resolving `ForgeOptions` mid-watch would rebuild the generator — a larger change; do it if/when core `watch`-mode branding-liveness is wanted.
- **The data-source route uses a full `GenerateAll()` (R6.4 scoped re-render stays deferred).** `RegenerateFromDataSource` re-parses everything (correctness-first, matching the extension's per-refresh spawn) rather than scoping the re-render to just the sprint/dashboard family. This is the same full-pass tradeoff as the existing 6.4 "Scoped re-render not implemented" item above; R6.4 (scope the re-render to the changed family + the `--serve` warm-process variant) remains the perf follow-up, not bundled here ("split, don't absorb").

## Deferred from: code review of 6-11-file-change-reactivity-hardening (2026-07-12)

- source_spec: `6-11-file-change-reactivity-hardening.md`
- **`_bmad` config-dir watcher only registers if the directory exists at `FileWatcherService` construction time.** `src/SpecScribe/FileWatcherService.cs:40-44` — if `_bmad/` is created after `specscribe watch` starts, config.toml changes are never observed for the remainder of that watch session (no re-check/poll). Narrow: real projects have `_bmad/config.toml` in place before `watch` starts.
- **`IsProjectConfigFile`'s doc comment claims "any depth" `_bmad`-segment matching, wider than what's actually watched.** `src/SpecScribe/SiteGenerator.cs:328-343` vs `FileWatcherService.cs:40` — only the repo-root `_bmad` dir is ever watched; a nested `_bmad`-named directory elsewhere in the repo would classify as a data source but never fire. Theoretical — one `_bmad` dir per project in practice.
- **`publishDiagnostics` (Story 6.12) doesn't reuse this story's `lastRepoRoot` convention.** `extension/src/extension.ts:1034-1056` — anchors Problems-panel paths via `folder.uri.fsPath` instead of `lastRepoRoot`, so a subdir-open workspace gets wrong-path Problems entries — the exact bug class this story eliminated for reveal-source/tree-open, reintroduced by new 6.12 code that shipped in the same commit. Out of scope for 6.11 itself; flag for Story 6.12's own review.
- **`SerializeDiagnostics` (Story 6.12) assumes every source-anchored notice lives under `resolved.SourceRoot`.** `src/SpecScribe/Commands.cs` (`SerializeDiagnostics`) — would mis-resolve an ADR-anchored notice (a different root). Out of scope for 6.11; flag for Story 6.12's own review.
- **`parseDiagnostics`/severity mapping (Story 6.12) silently downgrades any unrecognized severity to `Warning`, with no `message`-type validation.** `extension/src/extension.ts` (`parseDiagnostics`/`publishDiagnostics`) — out of scope for 6.11; flag for Story 6.12's own review.

## Deferred from: code review of spec-vscode-any-workspace-and-processing-indicators (2026-07-13)

- source_spec: `spec-vscode-any-workspace-and-processing-indicators.md`
  summary: A `webview` spawn in a very large or drive-root non-git workspace can exhaust the 60s renderer timeout and surface an error toast instead of the promised clean README + Code Map degrade.
  evidence: `ForgeOptions.Resolve` tolerant mode now anchors `RepoRoot` to the cwd with no size bound (ForgeOptions.cs:153); `FallbackCodeWalk` (SiteGenerator.cs:2325) caps the FILE count at `MaxCodeMapFiles` but still traverses directories until that cap or exhaustion, so a huge tree with few matching files can run past the shim's 60s hard kill (extension.ts). Before the tolerant change such a folder threw instantly. Real edge (Edge Case Hunter). Fix belongs in the Story 7.6 code-map walk (bound/short-circuit directory traversal, not just the file cap), out of this spec's scope.

- source_spec: `spec-vscode-any-workspace-and-processing-indicators.md`
  summary: `generate`/`watch` commands now appear in the Command Palette for any open folder and dead-end in the terminal (the CLI still throws) when the folder is not a BMad project.
  evidence: The gate rename to `specscribe.available` (a folder is open) intentionally removed bmad detection, so `specscribe.generateSite`/`specscribe.watch` are offered everywhere (package.json). Their terminal handoff correctly still hits the CLI's actionable `DirectoryNotFoundException` (CLI-honesty boundary), relocating the "not detected" dead-end into the terminal for these two commands. Minor discoverability wart; a targeted soft-gate (a separate bmad-project context key for just these two) would fix it without reintroducing detection as the main gate.

- source_spec: `spec-vscode-any-workspace-and-processing-indicators.md`
  summary: In a non-bmad workspace, editing actual source files does not refresh the Code Map / panel — only manual Refresh does.
  evidence: The store's watch globs remain `_bmad-output/**`, `docs/adrs/**`, `_bmad/config.toml` (frozen by Story 6.11). In a plain code repo the Code Map is the headline value (Goal A), yet source-file edits fire no watcher, so live-update lags behind the "renders value from source code" framing. Widening the globs to source trees intersects the frozen Story 6.11 watch scope and its perf/debounce assumptions, so it belongs in a focused follow-up rather than this spec.

- source_spec: `spec-code-page-relationships-history-tabs.md`
  summary: In the Relationships tab, the "Referenced by" graph card (h2) and the "Often changed with" coupled card (h3) sit as visual peers in one grid, so the document outline reads the coupled h3 as a child of "Referenced by".
  evidence: Blind Hunter finding (low severity). BuildRelationshipsCard emits <h2> while the shared BuildCoupledCard emits <h3> (locked at h3 by its correct nesting under "Advanced coverage" in the Insights tab). Defensible as-is (coupling is a secondary relationship signal), but the heading level is inconsistent with how the same card nests in Insights. A fix needs a small design call (panel-level umbrella heading for Relationships, or a level-agnostic coupled card) and is not worth expanding this diff.

- source_spec: `spec-code-page-relationships-history-tabs.md`
  summary: Two dynamic tab-state combinations from the I/O matrix have no dedicated test: insight-with-coupled-but-no-refs (Relationships tab holds only the coupled card) and history-only (History default-checked).
  evidence: Edge Case Hunter enumerated both as correct-but-unverified; the history-only case is likely unreachable (non-empty History implies ChangeCount>0, which renders the Insights frequency card). Behavior was proven correct by path enumeration; adding the two targeted tests would close the matrix-coverage gap cheaply in a follow-up.

- source_spec: `spec-7-3-10-4-honest-navigable-portal-dates.md`
  summary: BuildArtifactsByDay's repo↔source path map uses OrdinalIgnoreCase, which on a case-sensitive filesystem (Linux) could match a git path to a wrong-cased artifact or collide two keys differing only in case.
  evidence: Edge Case Hunter finding (low). Correct on Windows (the primary/local-first target); the collision requires two artifacts whose paths differ only in case, which is pathological. A cross-platform-correct fix would pick the comparer by OS (Ordinal on non-Windows). Not worth expanding this diff.

- source_spec: `spec-7-3-10-4-honest-navigable-portal-dates.md`
  summary: Git-derived artifact-change days do not follow renames — a commit that touched an artifact under its pre-rename path is not attributed to the current reference-map key, so that artifact's change days are silently incomplete.
  evidence: Edge Case Hunter finding (low). This under-reports (shows fewer change days) but never mis-attributes, so it is an honest degradation consistent with the story's stance. Following renames needs git rename-detection (a --follow-style map) that is out of scope for this pass.

## Deferred from: code review of 7-5-per-commit-detail-pages (2026-07-13)

- source_spec: `7-5-per-commit-detail-pages.md`
- **Git-dependent generation tests hard-fail instead of skipping when git is unavailable on the host.** `tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs` (`Assert.True(TryCreateGitHistory(), ...)`) — pre-existing repo-wide test convention mirrored from `SiteGeneratorGitInsightsTests`/`SiteGeneratorTimelineTests`/`SiteGeneratorCodeInsightsTests`, not introduced by this story. A CI runner or dev machine without git on PATH gets outright failures rather than skips.
- **Determinism test's footer-stripping regex hardcodes a signed UTC offset shape (`UTC[+-]\d{2}:\d{2}`).** `tests/SpecScribe.Tests/SiteGeneratorCommitDetailsTests.cs:167` — same regex duplicated verbatim in 3 sibling test files; if the actual offset is ever rendered without a sign (e.g. `+00:00` shown as `00:00`) or `PortalDates`' footer format shifts slightly, the "determinism" test degrades into comparing two un-normalized strings. Pre-existing convention, not introduced by this story.

## Deferred from: code review of spec-entity-prev-next-navigation (2026-07-13)

- source_spec: `spec-entity-prev-next-navigation.md`
  summary: The sibling pager sequences each family from its full ordered list, so a Prev/Next link can point to a
  sibling whose page was Skipped/Errored and never written (a code file that fails the output-root escape check or
  throws mid-render; likewise a day/commit whose render throws) → a 404. In a normal run every sibling has a page
  (too-large/unreadable code files still get placeholder pages), so this only fires under an exceptional
  mid-generation race (file deleted/renamed) or a traversal attempt — conditions that already break citations and
  heatmap links too. A proportionate fix (guard each pager href against the set of actually-written pages, or a
  post-write second pass) was out of scope for the feature; it bypasses the codebase's otherwise-strict
  "never a dead link" discipline.
  evidence: Blind Hunter + Edge Case Hunter agreed. [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs)

## Deferred from: code review of story-7.2 (2026-07-13)

- source_spec: `7-2-source-citation-and-comment-linking-to-code-pages.md`
- **Code-reference resolution requires an extension on the last path segment and doesn't percent-decode paths.** `src/SpecScribe/CodeReferenceLinkifier.cs:165-183` (`IsRelativeCodeHref`) — citations to extensionless files (`Dockerfile`, `Makefile`, `LICENSE`) or filenames with percent-encoded characters (e.g. `%20` for a space) never resolve via the href form and silently degrade to plain text. Pre-existing completeness gap in the new linkifier, not required by AC #1/#2 and not a regression.
  evidence: Edge Case Hunter finding (low), reviewed and reasoned about directly against the merged code.
- **`_codePages` forward-map entries are pruned (on a code-page write failure/output-escape) AFTER story/doc/ADR pages were already linkified against the optimistic pre-prune map.** `src/SpecScribe/SiteGenerator.cs:711-777,829-877` (`DiscoverCodeReferences` runs before all citing pages render; `GenerateCodePagesInternal`'s `_codePages.Remove(...)` runs after) — an already-written citing page can carry a dead `#L{n}` link to a code page that never got generated. Same risk class already accepted in `spec-entity-prev-next-navigation`'s deferred sibling-pager finding: only fires under an exceptional mid-generation race (a discovered/validated file becomes unreadable or its output path escapes the root between discovery and render) that discovery's own existence/inside-repo checks make very unlikely. A proportionate fix (a post-write second linkification pass, or reordering code-page generation before all other pages) is out of scope for this story.
  evidence: Acceptance Auditor + Blind Hunter agreed; verified against the actual `GenerateAll` call order (`DiscoverCodeReferences` → epics/story/doc pages → `GenerateAdrsInternal` → `GenerateCodePagesInternal`).
- **`IsRelativeCodeHref` is unscoped to genuine `[Source: …]` citations — it matches ANY relative `<a href>` with a non-`.html`/`.md` extension.** `src/SpecScribe/CodeReferenceLinkifier.cs:77-96,165-183` — a legitimate prose link to an unrelated asset (image, download) that doesn't resolve gets silently stripped to plain text by `RewriteHrefs`. Two candidate fixes were evaluated and rejected: requiring a leading `../` climb breaks the bare `[label](src/Foo.cs)` citation shape genuinely used in the live corpus (`10-1-insights-navigation-and-structure-page-retirement.md:17`) and in `CodeReferenceLinkifierTests.EscapesEmittedHref`; gating on real filesystem existence breaks in-portal mode's deliberate disk-free design and the `UnresolvedHref_DegradesToPlainText` test. A safe fix needs citation context threaded from the markdown-parse stage through to rendered HTML, not a rewrite of the HTML-stage matcher.
  evidence: Blind Hunter finding, confirmed and reasoned through directly with two rejected fix attempts against the real test suite and corpus.
- **`BuildReferencedBy`'s fallback (`PathUtil.ToOutputRelative`) guesses a URL for a citing artifact missing from `_referenceMap`, rather than confirming it.** `src/SpecScribe/SiteGenerator.cs:1523-1539` — attempted an "omit rather than guess" patch during this review, but reverted after it regressed `SiteGeneratorCodeCitationTests.CodePage_HasReferencedByBackToCitingArtifacts`: without an `epics.md`, `_referenceMap` is never populated, and the naive fallback formula is exactly correct for ordinary docs in that case (identical to `GenerateOneInternal`'s own output-path computation) — so omitting throws away legitimate back-links. The guess is only unreliable for entities with a non-naive output path, and those (drafted stories) are always correctly overridden in `_referenceMap` when present. No unambiguous safe fix found within this pass.
  evidence: Acceptance Auditor + Blind Hunter agreed; verified directly against the real test suite (one attempted fix reverted after a confirmed regression).

## Deferred from: code review of spec-reference-graph-epic-grouping-and-relationships (2026-07-13)

- source_spec: `spec-reference-graph-epic-grouping-and-relationships.md`
  summary: Co-change pair counts are computed twice from the same commit list — `GitMetrics.ParseNumstatLog`'s
  top-level `pairCounts` (feeding `DeepGitPulse.Coupling`, the hub's top-10 hotspots) and `BuildFileInsights`'s own
  separate `pairCounts` (feeding both `FileInsight.CoupledFiles` and the new `DeepGitPulse.CoChangePairs`) are two
  independent in-memory tallies over the same `commits`, rather than the codebase converging on one shared
  computation. Pre-existing duplication from Story 7.4, surfaced incidentally while this feature exposed the second
  tally as `CoChangePairs` — not a new git call/scan (this feature's own "Never" constraint), just an existing
  redundant pass this feature builds on top of instead of first consolidating.
  evidence: Blind Hunter. [GitMetrics.cs](../../src/SpecScribe/GitMetrics.cs)
- source_spec: `spec-reference-graph-epic-grouping-and-relationships.md`
  summary: An epic hub with many member stories packs them into a fixed angular arc
  (`halfWidth = min(pi/(mainSlotCount+relCount), 0.5)`) with no per-hub sub-cap or "+N more" overflow marker, so a
  hub with most/all of the artifact cap's members could read as visually crowded (mathematically distinct
  positions, but tightly packed labels/nodes) — no test asserts node-position legibility, only node count.
  evidence: Blind Hunter + Edge Case Hunter agreed. Accepted as a documented seed-value limitation (mirrors the
  project's existing "seed, not contract" stance on graph caps) rather than fixed now; revisit if a real repo
  surfaces a crowded hub. [Charts.cs](../../src/SpecScribe/Charts.cs)

## Deferred from: code review of spec-3-4-retired-status-first-class (2026-07-14)

- source_spec: `_bmad-output/implementation-artifacts/spec-3-4-retired-status-first-class.md`
  summary: `SprintTemplater.StageOrder` is a dead private field (counts come from `ProjectCounts.TrackedStoryStages`); keeping it "in sync" still risks silent drift until the dead field is deleted or wired.
  evidence: Blind Hunter. Pre-existing unused field; this change only extended it.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-4-retired-status-first-class.md`
  summary: No generator/adapter test asserts a real `retired` yaml row no longer emits the Story 8.2 Unsupported diagnostic — coverage stops at `IsUnrecognizedSprintStatus("retired") == false`.
  evidence: Blind Hunter.
- source_spec: `_bmad-output/implementation-artifacts/spec-3-4-retired-status-first-class.md`
  summary: SiteGeneratorAdapter golden fingerprint fixture has no retired sprint rows, so it locks chrome/CSS/legend delta but not the happy-path Retired lane content on a real board.
  evidence: Blind Hunter.

## Deferred from: code review of story-7-8 (2026-07-13)

- source_spec: `7-8-related-files-in-the-reference-graph.md`
  summary: No test exercises the combined worst case of an artifact-ring overflow (>14 citers) plus a full related-file population (8) on the same graph — whether the honest "+N more artifacts" footnote visually collides with a related-file diamond near the bottom of the ring is unverified.
  evidence: Blind Hunter. [ChartsTests.cs](../../tests/SpecScribe.Tests/ChartsTests.cs)
- source_spec: `7-8-related-files-in-the-reference-graph.md`
  summary: `Charts.ReferenceGraph`'s new `artifactCap` parameter (default `RefGraphArtifactNodeCap = 14`) has no lower-bound validation; a caller passing a negative value that exactly offsets `relCount` drives `total` to zero, causing a divide-by-zero in the ring-angle math (`Ang(i)`) and NaN coordinates in the emitted SVG. Unreachable today — both production call sites use the default — but it's latent robustness debt on a public API surface.
  evidence: Edge Case Hunter. [Charts.cs:1126,1146](../../src/SpecScribe/Charts.cs)

## Deferred from: code review of spec-sprint-epic-filter-and-home-layout (2026-07-14)

- source_spec: `spec-sprint-epic-filter-and-home-layout.md`
  summary: No automated browser/JS coverage for enhanceSprintEpicFilter (toggle, All, empty selection, live aria-label/cap recount) — regressions in the PE half rely on manual checks.
  evidence: Blind Hunter. [specscribe.js](../../src/SpecScribe/assets/specscribe.js)

## Deferred from: code review of spec-declutter-home-dashboard (2026-07-14)

- source_spec: `spec-declutter-home-dashboard.md`
  summary: Both delivery/webview spikes (`spike/delivery/exporter/Program.cs:56`, `spike/vscode/renderer/Program.cs:54`) call `SiteNav.Build(..., hasStructure: ...)`, but `SiteNav.Build` no longer has a `hasStructure` parameter (replaced by `hasCodeMap` in an earlier story) — the spikes were already non-compiling against the current signature before this change. Not caused by the declutter; surfaced when re-checking spike compilation.
  evidence: Edge Case Hunter. Pre-existing; baseline `SiteNav.Build` call already used the stale `hasStructure` arg.

## Deferred from: code review of 9-2-nfr-and-ux-dr-coverage-maps.md (2026-07-16)

- ~~source_spec: `9-2-nfr-and-ux-dr-coverage-maps.md`
  summary: Epic 1 header back-fill tags UX-DR1–13 and 16–18 as delivered by Epic 1 — owner deferred confirmation/trim; mappings left as-is. Reason: deferred by owner during 9.2 review.
  evidence: Blind Hunter + Acceptance Auditor (over-claim risk vs under-claiming guardrail).~~ **RESOLVED 2026-07-18** (`spec-9-2-deferred-coverage-maps-cleanup`): owner confirmed Epic 1 UX-DR1–13 and 16–18 header tags as-is — no trim; `epics.md` unchanged.
- ~~source_spec: `9-2-nfr-and-ux-dr-coverage-maps.md`
  summary: `ParseUxDrs` is a near-copy of `ParseDefs` (coverage resolve, epic title lookup, `RequirementInfo` construction, `DeriveStatus`); kind-specific regex alone did not require a second path, so future coverage/status fixes can drift between FR/NFR and UX-DR.
  evidence: Blind Hunter. [`RequirementsParser.cs`](../../src/SpecScribe/RequirementsParser.cs)~~ **RESOLVED 2026-07-18** (`spec-9-2-deferred-coverage-maps-cleanup`): UX-DR folds into shared `ParseDefs` via unified `DefLine` (`FR|NFR|UX-DR`); `ParseUxDrs` removed. [`RequirementsParser.cs`]
- ~~source_spec: `9-2-nfr-and-ux-dr-coverage-maps.md`
  summary: `AppendCoverageRow` rebuilds `epics.Epics.ToDictionary` on every NFR/UX-DR row — needless repeated allocation on the requirements index.
  evidence: Blind Hunter. [`RequirementsTemplater.cs`](../../src/SpecScribe/RequirementsTemplater.cs)~~ **RESOLVED 2026-07-18** (`spec-9-2-deferred-coverage-maps-cleanup`): dictionary built once per NFR/UX-DR coverage section and passed into rows. [`RequirementsTemplater.cs`]
- ~~source_spec: `9-2-nfr-and-ux-dr-coverage-maps.md`
  summary: `RequirementInfo.Id`’s switch defaults unknown `RequirementKind` values to `"NFR" + Number` instead of failing closed.
  evidence: Blind Hunter. Pre-existing fail-open pattern; Design was added as an explicit arm.~~ **RESOLVED 2026-07-18** (`spec-9-2-deferred-coverage-maps-cleanup`): explicit FR/NFR/Design arms; unknown kind throws. [`RequirementsModel.cs`]
- ~~source_spec: `9-2-nfr-and-ux-dr-coverage-maps.md`
  summary: Task 7 asked to prove FR flow/grid/donut HTML stayed byte-identical; tests only locked FR coverage epic numbers / updated golden fingerprints rather than an explicit FR HTML baseline assertion. Superseded in practice by Story 9.3’s intentional Unmapped-tier changes to those surfaces.
  evidence: Acceptance Auditor + Blind Hunter.~~ **RESOLVED 2026-07-18** (`spec-9-2-deferred-coverage-maps-cleanup`): closed as superseded by Story 9.3 Unmapped-tier surface changes — no pre-9.3 FR HTML byte-identity golden restored.

## Deferred from: code review of spec-undrafted-create-story-panel-above-ac (2026-07-16)

- ~~source_spec: `spec-undrafted-create-story-panel-above-ac.md`
  summary: Epic-page story cards still render undrafted guidance after AC blocks, while placeholder pages now put create-story guidance above AC — sibling surfaces disagree on CTA placement.
  evidence: Blind Hunter. `AppendStoryCard` leaves `not-detailed-note` after AC; placeholder reorder was intentional scope for the full story page only.~~ **RESOLVED 2026-07-18** (`spec-epic9-deferred-debt-cleanup`): undrafted epic cards emit `not-detailed-note` above AC (match placeholder). [`HtmlRenderAdapter.Epics.cs`]
- ~~source_spec: `spec-undrafted-create-story-panel-above-ac.md`
  summary: SiteGeneratorAdapter golden fixture's undrafted story has no Acceptance Criteria, so the reordered AC branch is invisible to the byte-parity gate.
  evidence: Blind Hunter. Pre-existing fixture gap; only the new unit test covers note-above-AC ordering.~~ **RESOLVED 2026-07-18** (`spec-epic9-deferred-debt-cleanup`): Story 1.2 golden fixture gains minimal AC; fingerprint re-baselined. [`SiteGeneratorAdapterTests.cs`]

## Deferred from: code review of spec-address-deferred-next-steps.md (2026-07-17)

- source_spec: `spec-address-deferred-next-steps.md`
  summary: Cap Address-deferred multi-item prompt length when many open items (truncate with an "and N more…" tail).
  evidence: Blind Hunter — BuildAddressDeferredSuggestion concatenates every open slot (~200 chars each) with no length budget; large backlogs can produce impractical clipboard/deep-link payloads.

## Deferred from: code review of spec-9-2-deferred-coverage-maps-cleanup.md (2026-07-18)

- source_spec: `spec-9-2-deferred-coverage-maps-cleanup.md`
  summary: `DefLine` and `CoverageMapLine` are now identical `FR|NFR|UX-DR` patterns under two names — a fix to one can drift from the other without a compile error.
  evidence: Blind Hunter. Pre-existing dual-regex shape; unify made them identical. Consolidate when next touching RequirementsParser regexes.

## Deferred from: code review of spec-2-5-deferred-iconography-hardening.md (2026-07-18)

- source_spec: `spec-2-5-deferred-iconography-hardening.md`
  summary: `"Action Items"` remains a curated `Icons.ForConcept` arm with no production call site and no reverse-emitter membership — same orphan class as the removed Direct & Quick-Dev key.
  evidence: Blind Hunter. Grep shows no `ForConcept("Action Items")`; forward InlineData also omits it. Remove or wire when next touching Icons.ForConcept.
