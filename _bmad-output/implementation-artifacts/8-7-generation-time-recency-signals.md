# Story 8.7: Generation-Time Recency Signals

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer returning to the portal,
I want "last updated" markers on dashboard widgets and story cards,
so that I can spot recent movement without diffing pages.

## Acceptance Criteria

1.
**Given** git timestamps and artifact change logs are available at generation time
**When** the dashboard and story cards render
**Then** they carry "last updated" recency markers derived **solely from that input data**
**And** a from-scratch CI regeneration of the same inputs produces **identical output** (no per-visitor or cross-build state). [Source: [epics.md:1310-1314](../planning-artifacts/epics.md:1302)]

2.
**Given** a source lacks git data or change-log entries
**When** generation runs
**Then** the affected surface shows **no recency marker** rather than a wrong one
**And** generation remains **non-fatal**. [Source: [epics.md:1316-1320](../planning-artifacts/epics.md:1302)]

---

## Developer Context

**This story adds `Updated <date>` recency markers to two surfaces and removes a live determinism violation.** The whole story turns on one non-negotiable constraint from AC #1: **every marker must be a pure function of input data (git commit dates, change-log dates) — never of the build clock.** The existing recency line proves why this matters: [`DashboardViewBuilder.CommitStatSub`](../../src/SpecScribe/DashboardViewBuilder.cs:101) already renders "last commit **3d ago**" by subtracting `git.LastCommitDate` from `DateOnly.FromDateTime(DateTime.Now)` — so the *same repo* regenerated tomorrow prints "4d ago". That is exactly the "cross-build state" AC #1 forbids. This story replaces relative-to-now phrasing everywhere with **absolute dates from the input data**, and fixes that existing line as part of the work.

Two surfaces, mapped to the two AC nouns:

1. **Dashboard widget → the Commits stat tile** ([`BuildStatTiles`](../../src/SpecScribe/DashboardViewBuilder.cs:80) sub-line via `CommitStatSub`). Today its sub-line is `"{N} active days · last commit {daysAgo}d ago"` where `daysAgo` uses `DateTime.Now`. Change the recency clause to an **absolute date** off `git.LastCommitDate` (already a `DateOnly` on `GitPulse`, deterministic). Keep the "N active days ·" prefix.
2. **Story cards → the epic-page story cards** ([`AppendStoryCard`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:211), built by [`EpicsViewBuilder.BuildStoryCard`](../../src/SpecScribe/EpicsViewBuilder.cs:72)). Add an `Updated <date>` marker to the card header, with the date resolved **per the owner's precedence: prefer the story file's per-file git commit date; otherwise fall back to the latest date in the story's `## Change Log` section; otherwise show nothing** (AC #2).

### Owner-selected design decisions (visual/scope intent elicited at create-story — do not re-litigate)

**1. Marker format → absolute date (owner pick).** All recency markers render as a deterministic **absolute date** (e.g. `Updated Jul 9, 2026`), formatted with `InvariantCulture` — the same invariant discipline the footer clock uses ([PathUtil.cs:104-106](../../src/SpecScribe/PathUtil.cs:104)). Relative-to-now phrasing ("3d ago") is explicitly **rejected** because it is not reproducible across builds and would break AC #1. Chosen over deterministic-relative ("3 days before latest activity") for clarity. [Owner decision, this story; memory: [[create-story-elicit-visual-intent]]]

**2. Fix the existing "Nd ago" line (owner pick).** The Commits stat tile's `CommitStatSub` recency clause is **in scope** — convert it from the `DateTime.Now`-relative "Nd ago" to the same absolute-date format. Rationale: it is a recency signal on a dashboard widget that violates the very determinism contract this story establishes; shipping new deterministic markers next to an old non-deterministic one would be incoherent. [Owner decision, this story]

**3. Story-card date source → git-first, change-log fallback (owner pick).** For a story card's marker, **prefer the per-file git commit date** of the story's markdown file; when that is unavailable (no `--deep-git`, or the file isn't matched in the deep-git file map), **fall back to the latest date parsed from the story's `## Change Log` section**; when neither exists, render **no marker** (AC #2). [Owner decision, this story]

### Why this is the natural seam (read carefully — this is where the change lives)

**Per-file git dates already exist, but only under `--deep-git`.** The deep-git pass produces [`FileChangeStat`](../../src/SpecScribe/GitMetrics.cs:75) records — one per file — carrying `LastChangeDate` (a `DateOnly?`) keyed by **repo-root-relative path**, reachable at [`DeepGitPulse.Insights.Files`](../../src/SpecScribe/GitMetrics.cs:91). The baseline [`GitPulse`](../../src/SpecScribe/GitMetrics.cs:18) has only the **whole-repo** `LastCommitDate` (no per-file granularity). So per-story git dates are a **deep-git-gated** signal; the whole-repo date on the Commits tile is always available when there's any git history.

**`ProgressCalculator` is the one place that already has both inputs a story needs.** [`ProgressCalculator.Compute`](../../src/SpecScribe/ProgressCalculator.cs:8) already (a) receives the `DeepGitPulse? deep` argument and (b) reads **each story's raw artifact markdown** to tally tasks/status (`ReadArtifactProgress` → `MarkdownConverter.ReadAllTextShared`), setting `story.TasksDone/TasksTotal/Status` as a side effect. Resolve the recency date in the **same pass, off the same read** — no second file read, no new plumbing — and set it on a new `StoryInfo.LastUpdatedDate` property (mirroring how `Status`/`TasksDone` are already back-filled onto `StoryInfo`). The epic-page story-card builder then just reads `story.LastUpdatedDate`.

**Path reconciliation is the sharp edge for the git branch.** `story.ArtifactSourcePath` is relative to `_bmad-output/` (e.g. `implementation-artifacts/8-7-….md`), but git (`FileChangeStat.Path`) reports **repo-root-relative** paths (e.g. `_bmad-output/implementation-artifacts/8-7-….md`). [`ForgeOptions.Resolve`](../../src/SpecScribe/ForgeOptions.cs:108) always sets `SourceRoot = RepoRoot/_bmad-output`, so the repo-relative key is `ForgeOptions.SourceDirName + "/" + ArtifactSourcePath`, normalized via [`PathUtil.NormalizeSlashes`](../../src/SpecScribe/PathUtil.cs). Match ordinally after normalizing both sides. **Heed the rename gotcha** (memory: [[deep-git-single-numstat-path]]): git's numstat rename display is already collapsed to the *current* path by `ResolveRenamedPath`, so a recently-renamed story file's date resolves against its new name — but a story whose current file has *no* commits under its current path simply won't match, and correctly falls back to the change-log date (AC #2). Don't force a match; unmatched → fallback.

**Change-log dates are authored strings in the artifact — deterministic by construction.** The story `## Change Log` section (already extracted for display via [`EpicsParser.ExtractNamedSectionHtml(raw, "## Change Log")`](../../src/SpecScribe/SiteGenerator.cs:711)) contains ISO `yyyy-MM-dd` dates in two shapes seen across the repo's own stories: **table rows** (`| 2026-07-08 | … |`) and **list rows** (`- 2026-07-08 (polish #8): …`). Add a pure `EpicsParser.ExtractLatestChangeLogDate(raw)` → `DateOnly?` that scans the change-log section and returns the **maximum** ISO date (invariant parse), null when none. Since it reads from the committed artifact, it is identical across builds.

### Scope boundaries (read carefully)

- **Do NOT introduce any new `DateTime.Now` / `DateTime.UtcNow` / `DateOnly.FromDateTime(DateTime.Now)` read.** Every marker is a function of `git.LastCommitDate`, `FileChangeStat.LastChangeDate`, or a parsed change-log date. The one new determinism rule for this story: **no wall-clock in a recency marker.** (The footer's own generation clock at [PathUtil.cs:106](../../src/SpecScribe/PathUtil.cs:106) is a separate, already-normalized concern — leave it alone.)
- **Do NOT add a per-visitor or JS-computed "time ago".** No script, no NuGet — markers are static generation-time text (memory: [[charting-is-pure-svg-no-js]]).
- **Do NOT change any count, status word, or status color.** Recency is orthogonal to the count seam (8.2) and the status-token seam (8.1) (memory: [[specscribe-status-token-system]]). This story adds a date label; it touches no tally.
- **Do NOT add markers to the dashboard Now & Next / sprint-board cards, or the standalone story detail page header.** Those are navigation cards and a different seam; the sprint page already surfaces `sprint-status.yaml`'s `last_updated` ([SprintTemplater.cs:63](../../src/SpecScribe/SprintTemplater.cs:63)). Scope is the **Commits stat tile** + the **epic-page story cards** only. (Out-of-scope surfaces are a deliberate boundary, not an oversight.)
- **Do NOT block generation on any recency failure.** A malformed change-log date, a null deep pulse, an unmatched path → skip the marker (AC #2). Never throw from the resolver (mirror `ReadArtifactProgress`'s `catch`).
- **Do NOT write back to any source.** Local-first, read-only invariant.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Add a single deterministic date formatter (one source of truth).** A small pure helper — e.g. a `Recency` static class or a `PathUtil.FormatRecencyDate(DateOnly)` method — returning `date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)` (→ `Jul 9, 2026`). Every recency marker routes through it so the format is defined once (the same single-source discipline `RenderFooter` uses for its clock). Invariant culture is **required** (non-Gregorian default calendars would otherwise corrupt the output — the same reason `GitMetrics` parses invariantly, see [GitMetrics.cs:176-178](../../src/SpecScribe/GitMetrics.cs:176)).
- **Add `EpicsParser.ExtractLatestChangeLogDate(string raw)` → `DateOnly?`.** Pure, repo-free, unit-testable (mirror `EpicsParser.ExtractStatus`'s shape). Isolate the `## Change Log` section (reuse the same section-boundary logic `ExtractNamedSectionHtml` uses), scan each line for a leading ISO `yyyy-MM-dd` token in **both** the table form (`| 2026-07-08 | … |`) and the list form (`- 2026-07-08 …`), `DateTime.TryParseExact(…, "yyyy-MM-dd", InvariantCulture, …)`, and return the **max** date found (null when none / no section). Malformed rows are skipped, never thrown.
- **Add `StoryInfo.LastUpdatedDate { get; set; }` (`DateOnly?`).** Back-filled by `ProgressCalculator`, exactly like `Status`/`TasksDone` are today (settable, not `init`). Default null.
- **Resolve recency in `ProgressCalculator.Compute`, off the existing artifact read.** Build the deep-git per-file date map **once** at the top of `Compute` from `deep?.Insights?.Files` → `Dictionary<string,DateOnly>` keyed by normalized repo-relative path (only entries whose `LastChangeDate` is non-null). For each story **with an artifact**: (1) look up the git date at `NormalizeSlashes(SourceDirName + "/" + story.ArtifactSourcePath)`; (2) else extract the change-log date from the raw markdown the pass already read; (3) set `story.LastUpdatedDate` to the first that exists, else leave null. Have `ReadArtifactProgress` also return the change-log date so the raw is read once.
- **Fix `DashboardViewBuilder.CommitStatSub` to be deterministic.** Replace the `DateTime.Now`-relative recency clause with `$"last commit {Recency.FormatDate(git.LastCommitDate)}"` (keep the `"{ActiveDays} active {day/days} · "` prefix). Remove the `daysAgo`/`DateTime.Now` lines entirely.
- **Render the story-card marker.** Add `DateOnly? UpdatedDate` to [`StoryCardView`](../../src/SpecScribe/EpicsView.cs), populate it in `BuildStoryCard` from `story.LastUpdatedDate`, and in `AppendStoryCard` emit a muted meta span (e.g. `<span class="story-card-updated">Updated {FormatRecencyDate}</span>`) inside `.story-card-header` **only when `UpdatedDate` is non-null**. Escape not needed (formatter output is a fixed date literal), but keep it consistent with sibling spans.
- **Add the marker CSS + a `StylesheetTests` assertion.** A small muted/right-aligned rule for `.story-card-updated`, tokens only, no literal hex (memory: [[specscribe-status-token-system]]). `StylesheetTests` asserts embedded-stylesheet content — add a companion assertion for the new rule.

### DON'T

- **DON'T read the wall clock** anywhere in a recency path — that reintroduces the exact AC #1 violation this story removes.
- **DON'T thread deep-git into `DashboardViewBuilder`** for the Commits tile — it needs only the whole-repo `git.LastCommitDate` already on `GitPulse`.
- **DON'T force per-story markers to require `--deep-git`** — the change-log fallback means baseline (no deep-git) runs still show a date for any story that has a `## Change Log` (AC #1 "change logs are available" branch).
- **DON'T reformat the footer clock or the About build row** — those are separately normalized volatile tokens (see the golden drill); this story neither reads nor changes them.
- **DON'T add markers to out-of-scope surfaces** (Now & Next cards, sprint board, story detail header) — scope creep past the two named surfaces.

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]:

- **Deterministic, generation-time-only output (AC #1's core).** "Server/generation-time ordering is the source of truth" ([rendering-architecture.md:90](../specs/spec-specscribe/rendering-architecture.md:90)) — this story extends that to *dates*: markers are pure functions of committed input (git author dates, authored change-log dates), so a from-scratch CI regen of identical inputs is byte-identical. This story also **improves** determinism by removing the pre-existing `DateTime.Now` recency read.
- **Optional insight providers enrich but never own baseline success (AD-4).** Per-file git dates ride the opt-in deep-git pulse; when it's absent the marker degrades to the change-log date or to nothing — baseline generation is never blocked or errored (AC #2). [Source: [ARCHITECTURE-SPINE.md#AD-4](../specs/spec-specscribe/ARCHITECTURE-SPINE.md:58)]
- **Unsupported/malformed artifacts degrade gracefully.** A missing change-log section, an unparseable date, or an unmatched git path yields *no marker*, never a fatal — mirroring the repo-wide "degrade, don't block" invariant and `ReadArtifactProgress`'s existing `catch`. [Source: [ARCHITECTURE-SPINE.md#Inherited Invariants](../specs/spec-specscribe/ARCHITECTURE-SPINE.md:27)]
- **Truthfulness over convenience.** Prefer the *authoritative* signal (the file's actual git commit date) and fall back to the *authored* signal (change-log date) only when git is unavailable — never invent or approximate. A wrong "updated" date is worse than none (AC #2).
- **One shared core; host-neutral view models (AD-1/AD-2).** The date resolves in the core (`ProgressCalculator` → `StoryInfo` → `StoryCardView`); the adapter only maps the resolved `DateOnly?` to bytes. No wall-clock or host state enters the view model, so the webview/HTML adapters stay byte-parity-safe. [memory: [[story-6-1-delivery-seam-live]]; [[story-6-2-section-view-models-live]]]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: [SpecScribe.Tests.csproj](../../tests/SpecScribe.Tests/SpecScribe.Tests.csproj)]
- **Reuse, don't reinvent (all already in-repo):**
  - `GitPulse.LastCommitDate` + `DeepGitPulse.Insights.Files` (`FileChangeStat.LastChangeDate`, `.Path`) — the recency data already computed by [`GitMetrics`](../../src/SpecScribe/GitMetrics.cs). Do **not** add a new git call; consume what `ProgressModel` already carries.
  - `EpicsParser.ExtractNamedSectionHtml` / `ExtractStatus` — the pattern to mirror for the new `ExtractLatestChangeLogDate` (same section isolation, same invariant-parse discipline).
  - `PathUtil.NormalizeSlashes` — for the repo-relative path key; `ForgeOptions.SourceDirName` — the `_bmad-output` prefix constant.
  - `CultureInfo.InvariantCulture` date formatting/parsing — the established invariant discipline ([GitMetrics.cs:176](../../src/SpecScribe/GitMetrics.cs:176), [PathUtil.cs:106](../../src/SpecScribe/PathUtil.cs:106)).
- **No external libraries or APIs** — pure in-repo C# string/date work — so there is no version/security research to fold in.

---

## File Structure Requirements

**One new pure parser method, one new `StoryInfo` field, one small formatter, plus the two render edits + CSS.** No new page, no new adapter contract, no view-model *section* (only a scalar field on the existing `StoryCardView`), no package restructure.

**Modified files (read fully before editing):**

- [`src/SpecScribe/EpicsModel.cs`](../../src/SpecScribe/EpicsModel.cs:7) — add `StoryInfo.LastUpdatedDate { get; set; }` (`DateOnly?`).
- [`src/SpecScribe/EpicsParser.cs`](../../src/SpecScribe/EpicsParser.cs) — add pure `ExtractLatestChangeLogDate(string raw) → DateOnly?` (mirror `ExtractStatus`/`ExtractNamedSectionHtml`).
- [`src/SpecScribe/ProgressCalculator.cs`](../../src/SpecScribe/ProgressCalculator.cs:8) — build the deep-git per-file date map once; back-fill `story.LastUpdatedDate` (git date → change-log date → null) from the existing per-story artifact read.
- [`src/SpecScribe/DashboardViewBuilder.cs`](../../src/SpecScribe/DashboardViewBuilder.cs:101) — rewrite `CommitStatSub` to use the absolute `git.LastCommitDate`; delete the `DateTime.Now`/`daysAgo` lines.
- [`src/SpecScribe/EpicsView.cs`](../../src/SpecScribe/EpicsView.cs) — add `DateOnly? UpdatedDate` to `StoryCardView`.
- [`src/SpecScribe/EpicsViewBuilder.cs`](../../src/SpecScribe/EpicsViewBuilder.cs:72) — `BuildStoryCard`: set `UpdatedDate = story.LastUpdatedDate`.
- [`src/SpecScribe/HtmlRenderAdapter.Epics.cs`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:211) — `AppendStoryCard`: emit the `story-card-updated` span when `UpdatedDate` is set.
- **New (small) or existing util** — the `Recency.FormatDate` / `PathUtil.FormatRecencyDate` formatter (single source).
- [`src/SpecScribe/assets/specscribe.css`](../../src/SpecScribe/assets/specscribe.css) — add the `.story-card-updated` muted rule (tokens only, no hex).

**Tests to update / add:**

- [`tests/SpecScribe.Tests/EpicsParserTests.cs`](../../tests/SpecScribe.Tests/EpicsParserTests.cs) (or the nearest parser suite) — `ExtractLatestChangeLogDate`: picks the **max** date; parses the **table** form and the **list** form; returns null with no `## Change Log` section and with a section but no ISO date; ignores non-date rows; invariant parse.
- [`tests/SpecScribe.Tests/ProgressCalculatorTests.cs`](../../tests/SpecScribe.Tests/ProgressCalculatorTests.cs) (or nearest) — `LastUpdatedDate` resolution: **git date wins** when the deep map has the story's repo-relative path; **change-log fallback** when deep is null / path unmatched; **null** when neither exists; path key = `SourceDirName + "/" + ArtifactSourcePath` matches a `FileChangeStat.Path`.
- [`tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs) — `AppendStoryCard` renders `story-card-updated` with the formatted date when `UpdatedDate` is set, and **omits** it when null (AC #2).
- Dashboard tile test (nearest `DashboardViewBuilder`/`HtmlRenderAdapterTests` coverage) — the Commits tile sub-line contains `"last commit "` + an absolute date and contains **no** `"d ago"` / `"ago"` text (proves the determinism fix). Assert determinism by building the tile twice and comparing (no clock dependence).
- [`tests/SpecScribe.Tests/StylesheetTests.cs`](../../tests/SpecScribe.Tests/StylesheetTests.cs) — assert the `.story-card-updated` rule ships in the embedded stylesheet.
- **Golden fingerprint:** [`SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_…`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:197) — **may change**. The committed fixture is **not** run with `--deep-git` and its source is not a git repo, so (a) the Commits tile shows "no git history" (the `CommitStatSub` fix produces **no byte change** there) and (b) story-card markers come **only** from change-log dates *if* the fixture's story `.md` files have a `## Change Log`. If any fixture story gains an `Updated` line, the fingerprint moves — regenerate the constant per the drill and confirm the **only** diffs are the new `Updated <date>` spans. If no fixture story has a change log, the fingerprint is unchanged. Verify which case applies before touching the constant. [memory: [[golden-diff-normalization-gotchas]]]
- **Section parity (must stay green):** [`RenderSectionParityTests.cs`](../../tests/SpecScribe.Tests/RenderSectionParityTests.cs) — `UpdatedDate` is a new scalar on `StoryCardView`, not a tracked dashboard `SectionFact`; parity holds. Confirm no `SectionViewModelSerialization` break (a `DateOnly?` serializes fine; check the epics/webview serialization path). [memory: [[story-6-2-section-view-models-live]]]

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). The recency logic is pure and directly unit-testable — cover each branch without a real repo.

Cover explicitly:

- **AC #1 determinism (the crux):** the same `GitPulse`/story inputs produce identical marker text across repeated builds — no clock dependence. The Commits tile sub-line is an **absolute date**, never "N d ago"/"ago".
- **AC #1 both sources:** a story matched in the deep-git file map shows its **git** date; a story with only a `## Change Log` shows the **latest change-log** date; per-precedence, git wins when both exist.
- **AC #2 degradation:** null deep pulse + no change log → **no** `story-card-updated` span, generation non-fatal; a malformed change-log date row is skipped, not thrown; an unmatched git path falls back cleanly.
- **Parser correctness:** `ExtractLatestChangeLogDate` returns the max ISO date across table and list forms; null when absent.
- **Path reconciliation:** `SourceDirName + "/" + ArtifactSourcePath` (normalized) matches a `FileChangeStat.Path`.

**Run:** `dotnet test` from repo root. Then a full generation **against this repo** (which *does* have git history and `## Change Log` sections): `dotnet run --project src/SpecScribe` (output → `SpecScribeOutput/`, the default — **do not** pass `--output docs/live`; vestigial/gitignored). Eyeball: the home page Commits tile reads "… active days · last commit **Jul N, 2026**" (a fixed date, not "Nd ago"); open an epic page — drafted story cards show "**Updated <date>**". Then run **with `--deep-git`** and confirm story-card dates shift to the per-file git commit dates where those differ from the change-log dates. [memory: [[generate-output-dir-is-specscribeoutput]]]

**Golden-diff drill (rendered bytes may change — confirm before regenerating):** freeze a fixture copy of `_bmad-output` + `docs/adrs` + `README.md` + `_bmad` in scratchpad, `git init` with fixed-date commits (+`--deep-git`), generate before/after, apply the 5 volatile-token normalizations (footer clock → invariant date, `?v=` token, subtitle+Version rows, About Build row, git-worktree CRLF), and confirm the ONLY diffs are the new `Updated <date>` spans on story cards and the Commits-tile recency clause switching from relative to absolute. Note the **committed** golden fixture (no git, no `--deep-git`) will only move if its story `.md` fixtures carry a `## Change Log`; check that first. Run twice for portability. [memory: [[golden-diff-normalization-gotchas]]]

---

## Previous Story Intelligence

**Story 8.6 (One Primary View per Dashboard Dataset — `ready-for-dev`, sibling)** established the create-story discipline this story follows: elicit visual/scope intent up front and record owner picks as non-re-litigable; keep additive presentation off the count/status/view-model *section* seams; expect a possible golden-fingerprint move and confirm the byte diff is *only* the intended change before regenerating. No file overlap in the render layer, but **both** may edit `HtmlRenderAdapter.Epics.cs` (8.6 = the epics-index header subtitle; 8.7 = `AppendStoryCard`) and `specscribe.css` — **non-overlapping hunks**; re-run the golden drill against whichever lands second. [Source: [8-6-one-primary-view-per-dashboard-dataset.md](8-6-one-primary-view-per-dashboard-dataset.md)]

**Story 8.2 (Single Source of Truth for Every Count — `ready-for-dev`, sibling)** owns count *correctness*; this story adds a date label and touches **no** count. Keep the seams distinct: recency ≠ tally.

**Story 3.1 / 3.2 / 3.8 (git pulse + deep-git — done)** built exactly the data this story consumes: `GitPulse.LastCommitDate` (3.1), the opt-in deep pass (3.2), and the per-file `FileChangeStat.LastChangeDate` on `Insights.Files` (3.8). Consume them; do **not** add a second git call (memory: [[deep-git-single-numstat-path]] — 3.2/3.8/7.4/7.5 all share the one numstat fetch; recency reads its output, adds no fetch). The renamed-file undercount caveat (3.1/3.2) is why an unmatched current path must fall back to the change-log date rather than mismatch. [Source: [3-1-baseline-git-pulse-insights-on-dashboard.md](3-1-baseline-git-pulse-insights-on-dashboard.md); [3-8-git-insights-hub-page.md](3-8-git-insights-hub-page.md)]

**Story 1.5 (dashboard insight polish — done)** authored the very `CommitStatSub` "Nd ago" line this story fixes ([Story 1.5 F3], relocated to `DashboardViewBuilder`). Its intent — a recency signal on the Commits tile — is preserved; only the *non-deterministic mechanism* changes. [Source: [DashboardViewBuilder.cs:99-108](../../src/SpecScribe/DashboardViewBuilder.cs:99)]

**Recurring lessons that apply here:**

- **Elicit visual intent up front** (Epic 3 retro, open action) — the marker format (absolute vs. relative), the fix-the-existing-line decision, and the story-card date source were offered as named directions; the owner picked *absolute date*, *fix it in 8.7*, and *git-first with change-log fallback*. Build those. [memory: [[create-story-elicit-visual-intent]]]
- **Split, don't absorb** — if this tempts you into re-pairing counts (8.2), restyling badges (8.1), or adding dates to sprint/Now-Next cards, stop; 8.7 is the Commits tile + epic-page story cards only. [Source: Epic 2/3 retros; [[epic-2-retro-scope-and-debt]]]
- **The "what real-world input shape does this parser silently drop?" check** (Epic 3 review action) — apply it to `ExtractLatestChangeLogDate`: cover both the table and list change-log shapes the repo actually uses; don't silently drop one.

---

## Git Intelligence Summary

Recent history is planning/spike/merge churn on `main` (`Merge branch 'worktree-bmad-dev-story-6-4'`, `Review notes`, `Story 6.4 …`) — no in-flight code touches `CommitStatSub`, `ProgressCalculator`, `EpicsParser`'s section extractors, or `AppendStoryCard`, so this change is additive and uncontended against siblings 8.1/8.2/8.6. Light adjacency with 8.6 on `HtmlRenderAdapter.Epics.cs` + `specscribe.css` (non-overlapping hunks). **Heed the worktree rule:** if this runs in a worktree, edit files at the **worktree path** — `main` has a background auto-committer, so never re-root paths at `C:\Dev\SpecScribe`. [memory: [[worktree-edits-must-target-worktree-path]]]

---

## Latest Technical Information

No external libraries or APIs are introduced — pure in-repo C# date/string work over already-computed git + parsed-artifact data. Two discipline notes:

1. **Invariant everything.** Format with `CultureInfo.InvariantCulture` ("MMM d, yyyy") and parse change-log dates with `DateTime.TryParseExact(…, "yyyy-MM-dd", InvariantCulture, DateTimeStyles.None, …)` — a culture-sensitive parse/format reinterprets dates under non-Gregorian default calendars (th-TH, fa-IR), which is exactly why `GitMetrics` and `PathUtil` already pin invariant ([GitMetrics.cs:176](../../src/SpecScribe/GitMetrics.cs:176)).
2. **`DateOnly` is the right type.** Both git dates (`LastCommitDate`, `FileChangeStat.LastChangeDate`) and change-log dates are day-granular; carry `DateOnly?` end-to-end and only stringify at the adapter edge via the single formatter. No `DateTime`, no time-of-day, no timezone — nothing that could vary a build.

---

## Project Context Reference

- Epic 8 goal + FR/UX-DR/NFR coverage: [Source: [epics.md:1165-1169](../planning-artifacts/epics.md:1165)]
- Story 8.7 user story + both ACs: [Source: [epics.md:1302-1320](../planning-artifacts/epics.md:1302)]
- The recency data sources: [Source: [GitMetrics.cs:18-27](../../src/SpecScribe/GitMetrics.cs:18) (GitPulse.LastCommitDate), [GitMetrics.cs:75-83](../../src/SpecScribe/GitMetrics.cs:75) (FileChangeStat.LastChangeDate), [GitMetrics.cs:91-96](../../src/SpecScribe/GitMetrics.cs:91) (Insights.Files)]
- The existing non-deterministic recency line to fix: [Source: [DashboardViewBuilder.cs:99-108](../../src/SpecScribe/DashboardViewBuilder.cs:99)]
- The change-log extraction already in the pipeline: [Source: [SiteGenerator.cs:711](../../src/SpecScribe/SiteGenerator.cs:711); story `## Change Log` sections, e.g. [3-1](3-1-baseline-git-pulse-insights-on-dashboard.md) table form, [2-3](2-3-sprint-status-page-and-dashboard-widget.md) list form]
- The recency-resolution seam: [Source: [ProgressCalculator.cs:8-70](../../src/SpecScribe/ProgressCalculator.cs:8) (has raw artifact + deep pulse; back-fills StoryInfo)]
- Path reconciliation: [Source: [ForgeOptions.cs:44](../../src/SpecScribe/ForgeOptions.cs:44) (SourceDirName), [ForgeOptions.cs:108-115](../../src/SpecScribe/ForgeOptions.cs:108) (SourceRoot = RepoRoot/_bmad-output), [EpicsModel.cs:20-22](../../src/SpecScribe/EpicsModel.cs:20) (ArtifactSourcePath)]
- Story-card render target: [Source: [HtmlRenderAdapter.Epics.cs:211-250](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:211); [EpicsViewBuilder.cs:72-101](../../src/SpecScribe/EpicsViewBuilder.cs:72)]
- Golden fingerprint + volatile-token normalizations: [Source: [SiteGeneratorAdapterTests.cs:197-255](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:197)]
- Architecture invariants (deterministic output, AD-4 optional insights, graceful degradation, truthfulness, shared core): [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md:90](../specs/spec-specscribe/rendering-architecture.md:90)]
- Determinism / deep-git-single-fetch / pure-render / golden-fingerprint / output-dir / worktree / visual-intent / status-token discipline: project memory ([[story-6-1-delivery-seam-live]]; [[story-6-2-section-view-models-live]]; [[deep-git-single-numstat-path]]; [[charting-is-pure-svg-no-js]]; [[golden-diff-normalization-gotchas]]; [[generate-output-dir-is-specscribeoutput]]; [[worktree-edits-must-target-worktree-path]]; [[create-story-elicit-visual-intent]]; [[specscribe-status-token-system]]).

---

## Tasks / Subtasks

- [ ] **Task 1 — Deterministic date formatter (AC: #1)**
  - [ ] Add a single pure `Recency.FormatDate(DateOnly)` (or `PathUtil.FormatRecencyDate`) → `date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)`. Route every marker through it.
- [ ] **Task 2 — Change-log date parser (AC: #1, #2)**
  - [ ] Add pure `EpicsParser.ExtractLatestChangeLogDate(string raw)` → `DateOnly?`: isolate the `## Change Log` section, scan for leading ISO dates in both table (`| 2026-07-08 |`) and list (`- 2026-07-08 …`) forms, invariant `TryParseExact`, return the max (null when none). Skip malformed rows, never throw.
- [ ] **Task 3 — Resolve per-story recency in `ProgressCalculator` (AC: #1, #2)**
  - [ ] Add `StoryInfo.LastUpdatedDate { get; set; }` (`DateOnly?`).
  - [ ] In `Compute`: build the deep-git per-file date map once (`deep?.Insights?.Files` → dict keyed by normalized repo-relative path, non-null dates only). Per story with an artifact: set `LastUpdatedDate` = git date at `NormalizeSlashes(SourceDirName + "/" + ArtifactSourcePath)` → else change-log date (from the raw the pass already read) → else null.
- [ ] **Task 4 — Fix the Commits stat tile (AC: #1)**
  - [ ] Rewrite `DashboardViewBuilder.CommitStatSub` to `"{ActiveDays} active {day/days} · last commit {Recency.FormatDate(git.LastCommitDate)}"`; delete the `DateTime.Now`/`daysAgo` lines.
- [ ] **Task 5 — Render the story-card marker (AC: #1, #2)**
  - [ ] Add `DateOnly? UpdatedDate` to `StoryCardView`; set it in `BuildStoryCard` from `story.LastUpdatedDate`.
  - [ ] In `AppendStoryCard`, emit `<span class="story-card-updated">Updated {formatted}</span>` in the card header only when `UpdatedDate` is non-null.
  - [ ] Add the `.story-card-updated` muted CSS (tokens only, no hex) + a `StylesheetTests` assertion.
- [ ] **Task 6 — Tests (AC: #1, #2)**
  - [ ] Parser: max-date, table + list forms, null cases. Resolution: git-wins / change-log-fallback / null; path-key match. Render: span present when set / absent when null. Commits tile: absolute date, no "ago", determinism (build twice, compare).
  - [ ] Confirm `RenderSectionParity` + section serialization green; run the golden drill and regenerate `GoldenContentFingerprint` **only** if the fixture actually moves (verify the diff is only `Updated` spans).
- [ ] **Task 7 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green; real generation to `SpecScribeOutput/` (with and without `--deep-git`); eyeball the Commits tile (absolute date, no "Nd ago") and drafted story cards ("Updated <date>", shifting to git dates under `--deep-git`).

## Dev Notes

- **The sharp edge is determinism + path reconciliation, not difficulty.** Every edit is small, but two disciplines constrain them: **no wall clock in any marker** (the whole point of AC #1 — and why the existing "Nd ago" is being replaced, not extended), and the **git-path key** must be `SourceDirName + "/" + ArtifactSourcePath` normalized, matched ordinally, with an unmatched path falling back rather than mismatching.
- **Resolve once, in `ProgressCalculator`.** It already reads each artifact and holds the deep pulse — do the change-log parse off that same read and set `StoryInfo.LastUpdatedDate`, exactly like `Status`/`TasksDone`. Don't scatter recency logic across the view builders.
- **Precedence is git → change-log → nothing.** Git is the authoritative "when did this file actually change"; change-log is the authored fallback; nothing is correct when neither exists (AC #2). Never invent a date.
- **The Commits-tile fix is byte-invisible in the committed golden fixture** (no git there) but is the *reason* the story exists — cover it with a targeted `GitPulse`-backed unit test, and confirm the whole-repo generation shows the absolute date.
- **No new section fact, no wall clock in the view model.** `UpdatedDate` is a resolved `DateOnly?` scalar on `StoryCardView`; the adapter stringifies it at the edge. `RenderParity` (semantic) stays green; only the byte-level golden may move. [memory: [[story-6-2-section-view-models-live]]]
- **Scope guard:** Now & Next cards, the sprint board, and the story detail-page header are **out of scope** — the two surfaces are the Commits stat tile and the epic-page story cards.

### Project Structure Notes

- Change concentrates in `EpicsParser` (new pure parser), `ProgressCalculator` (resolution), `DashboardViewBuilder` (tile fix), `EpicsView`/`EpicsViewBuilder`/`HtmlRenderAdapter.Epics.cs` (story-card marker), a shared formatter, and `specscribe.css` — plus tests. No new page, no adapter-contract change, one scalar view-model field. The Story 6.1/6.2 delivery seam and `RenderParity`/`SectionFacts` are untouched (a resolved `DateOnly?` carries no wall-clock or host state).
- Per-file git dates ride the existing shared deep-git numstat fetch (no second git call); the whole-repo date is already on `GitPulse`.

### References

- [Source: [epics.md:1302-1320](../planning-artifacts/epics.md:1302)] — Story 8.7 user story + both ACs.
- [Source: [epics.md:1165-1169](../planning-artifacts/epics.md:1165)] — Epic 8 goal; FRs; UX-DRs; NFR8.
- [Source: [DashboardViewBuilder.cs:99-108](../../src/SpecScribe/DashboardViewBuilder.cs:99)] — `CommitStatSub` (the `DateTime.Now` recency line to fix).
- [Source: [GitMetrics.cs:18-27](../../src/SpecScribe/GitMetrics.cs:18), [75-96](../../src/SpecScribe/GitMetrics.cs:75)] — `GitPulse.LastCommitDate`, `FileChangeStat.LastChangeDate`, `Insights.Files`.
- [Source: [ProgressCalculator.cs:8-92](../../src/SpecScribe/ProgressCalculator.cs:8)] — the resolution seam (raw artifact read + deep pulse + StoryInfo back-fill).
- [Source: [EpicsModel.cs:7-32](../../src/SpecScribe/EpicsModel.cs:7)] — `StoryInfo` (add `LastUpdatedDate`; note `ArtifactSourcePath`).
- [Source: [ForgeOptions.cs:44](../../src/SpecScribe/ForgeOptions.cs:44), [108-115](../../src/SpecScribe/ForgeOptions.cs:108)] — `SourceDirName`, `SourceRoot = RepoRoot/_bmad-output` (path key).
- [Source: [HtmlRenderAdapter.Epics.cs:211-250](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:211); [EpicsViewBuilder.cs:72-101](../../src/SpecScribe/EpicsViewBuilder.cs:72)] — story-card render + build.
- [Source: [SiteGenerator.cs:697-733](../../src/SpecScribe/SiteGenerator.cs:697)] — `## Change Log` extraction already in the pipeline (`ExtractNamedSectionHtml`).
- [Source: [PathUtil.cs:102-108](../../src/SpecScribe/PathUtil.cs:102)] — footer's invariant date-format precedent (single-source discipline).
- [Source: [SiteGeneratorAdapterTests.cs:197-255](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:197)] — golden fingerprint + volatile-token normalizations.
- [Source: [ARCHITECTURE-SPINE.md#AD-4](../specs/spec-specscribe/ARCHITECTURE-SPINE.md:58), [rendering-architecture.md:90](../specs/spec-specscribe/rendering-architecture.md:90)] — optional-insight, deterministic-output invariants.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
