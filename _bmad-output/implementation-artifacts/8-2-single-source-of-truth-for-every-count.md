# Story 8.2: Single Source of Truth for Every Count

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer doing the daily pulse,
I want all summary counts derived from one generator-side source,
so that summary widgets and detail views can never disagree.

## Acceptance Criteria

1.
**Given** entity counts (stories, epics, deferred items, action items) appear on multiple surfaces
**When** the portal is generated
**Then** every widget consumes the same generator-side count source
**And** a dashboard total always equals the sum of its own breakdown segments. [Source: epics.md#Story 8.2; FR21]

2.
**Given** a dashboard card links to a detail page
**When** I follow the link
**Then** the count on the card matches what the detail page shows
**And** the historical 38-vs-39 story-count class of clash is structurally impossible. [Source: epics.md#Story 8.2; FR21]

---

## Developer Context

**This is the counts story. Its whole job is to make it impossible for two places in the portal to report different numbers for the same thing.** It is a data-integrity / single-source refactor, not a new visual surface — the closest sibling in spirit is Story 8.1 (which locked the *status vocabulary* to one seam); this story locks the *counts* to one seam. Read [8-1-canonical-status-model-with-portal-wide-legend.md](8-1-canonical-status-model-with-portal-wide-legend.md) first — it is `ready-for-dev`, touches the same dashboard/sprint/epics surfaces, and establishes the "one documented seam, audit the call sites, surface divergence as a non-fatal diagnostic" pattern you will mirror here.

### The good news: half the seam already exists

SpecScribe already has **one** computed count model for the epics.md-derived family: [`ProgressModel`](../../src/SpecScribe/ProgressModel.cs), built once by [`ProgressCalculator.Compute`](../../src/SpecScribe/ProgressCalculator.cs) from the parsed `epics.md`. Every epics.md-derived count already flows from it and therefore already agrees across surfaces:

- Dashboard stat tiles + progress bars — [`DashboardViewBuilder`](../../src/SpecScribe/DashboardViewBuilder.cs:72-119) reads `p.StoriesTotal` / `p.EpicsTotal` / `p.EpicsDrafted` / `p.TasksDone` / `p.TasksTotal`.
- Epics-index stat card + Epic-Status donut — [`HtmlRenderAdapter.Epics.cs:76,88`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs).
- The refinement funnel / status stacked bar — [`Charts.RefinementFunnel`](../../src/SpecScribe/Charts.cs:692-719) (`total = p.StoriesTotal`, segments summed from `EpicProgress.StoryStatusCounts`).

**Do NOT rebuild that.** `ProgressModel` is the model you are promoting into the project-wide ledger, not replacing.

### The actual bug this story kills

There are **two independent count derivations** for the same conceptual quantities, from two different files, and nothing structurally stops them drifting:

| Quantity | Source A (epics.md → `ProgressModel`) | Source B (sprint-status.yaml → `SprintStatus`) |
|---|---|---|
| # stories | `progress.StoriesTotal` | `sprint.Entries.Count(e => e.Kind == Story)` — [`SprintTemplater.cs:62`](../../src/SpecScribe/SprintTemplater.cs:62) |
| # epics | `progress.EpicsTotal` | `sprint.Entries.Count(e => e.Kind == Epic)` — [`SprintTemplater.cs:61`](../../src/SpecScribe/SprintTemplater.cs:61) |
| story lifecycle tally | `EpicProgress.StoryStatusCounts` (from each story's `Status:` frontmatter) | `StoryStageCounts(sprint)` / the sprint wheel (from the yaml ledger) — [`SprintTemplater.cs:38-44,231-251`](../../src/SpecScribe/SprintTemplater.cs) |

`sprint-status.yaml` is *generated from* `epics.md` (by `sprint-planning`), so the two **should** agree — but they drift the moment one file is edited without the other (a story added to `epics.md` but not seeded in the yaml, or a yaml row for a renamed/removed story). When they drift, the **home page shows both numbers at once**: the "Stories defined: N" stat tile (epics.md) sits on the same page as the Now & Next sprint board / wheel (yaml) — see memory [[now-and-next-is-the-sprint-board]]. Reader sees N stories in one widget and a different total in the widget right below it. That is the "38-vs-39 story-count class of clash" the AC names. [Source: epics.md:1134; the concrete clash mechanism verified in this codebase]

> The literal "38 vs 39" in the ecosystem lore was a *commit*-count artifact (shallow CI checkout — [spec-ci-full-git-history-for-commit-activity.md](spec-ci-full-git-history-for-commit-activity.md)); the AC generalizes it to the **class** — "one conceptual number, two derivations, silent drift." Your fix must make that class structurally impossible for the four enumerated families, not just patch one instance.

### The four count families in scope (AC enumerates them explicitly)

Scope the ledger to exactly the AC's list — **stories, epics, deferred items, action items** — plus **tasks** (they ride along inside `ProgressModel` already and appear on ≥2 surfaces). Their current derivation sites:

- **Stories / Epics / Tasks** — `ProgressModel` (epics.md) AND the sprint yaml recounts above. **This is the clash surface.**
- **Deferred items** — `work.Deferred.OpenItemCount`, single site today ([`DashboardViewBuilder.cs:88`](../../src/SpecScribe/DashboardViewBuilder.cs:88)); centralize it so a second surface can't fork it later.
- **Action items** — `sprint.OpenActionItems.Count`, read at THREE sites already ([dashboard callout / `DashboardView.OpenRetroActionItems`](../../src/SpecScribe/DashboardViewBuilder.cs:58), [sprint flag `SprintTemplater.cs:219`](../../src/SpecScribe/SprintTemplater.cs:219), [action-items page header `ActionItemsTemplater.cs:32`](../../src/SpecScribe/ActionItemsTemplater.cs:32)). All read the same `SprintStatus` instance today so they agree — but they *recompute* independently. Route all three through the ledger so agreement is structural, not incidental.

Explicitly **out of scope:** requirements counts, coverage-matrix counts, git/commit counts (their own single sources: `RequirementsModel`, `ArtifactCoverage`, `GitPulse`). Don't fold them into this ledger — that would balloon the story and they aren't in the AC's list. (Requirements single-count is Epic 9's concern; leave it.)

### Owner-selected design decisions (do not re-litigate)

**1. Deliverable form → one generator-side `ProjectCounts` ledger, built once, threaded everywhere; no surface recounts.** Introduce a small immutable `ProjectCounts` record computed once in [`SiteGenerator.GenerateAll`](../../src/SpecScribe/SiteGenerator.cs:116-124) from the already-assembled `_progress` (`ProgressModel`), `_sprint` (`SprintStatus`), and the `workInventory` (`WorkInventory.Build(...)`, [SiteGenerator.cs:227](../../src/SpecScribe/SiteGenerator.cs:227)). Every count-displaying surface reads a **named field** off this ledger; every ad-hoc `.Count(...)` / `.Entries.Count(Kind==…)` at a render site is deleted and replaced with a ledger read. `ProgressModel` stays the *input* to the ledger (it still owns per-epic detail and the git pulse); `ProjectCounts` is the thin, documented, portal-wide count authority layered over it. This mirrors 8.1's "`StatusStyles` *is* the one seam" discipline. [Owner decision, this story]

**2. Name the two story/epic datasets distinctly — don't silently merge them.** The epics.md count and the yaml count are *genuinely different quantities* (the plan vs the tracking ledger — memory records they are deliberately "labeled and separate", [[story-6-2-section-view-models-live]] and Story 1.5 truthfulness). So the ledger carries BOTH, precisely named:
   - `StoriesDefined` / `EpicsDefined` — from `epics.md` (`ProgressModel`). The **plan of record**; this is what "Stories defined" and the epics-index/dashboard headline numbers show.
   - `StoriesTracked` / `EpicsTracked` — from `sprint-status.yaml`, but computed **once in the ledger** (not re-counted at the sprint template). This is what the sprint page subtitle + wheel total show, still labeled "from sprint-status.yaml".
   
   Because both numbers now come from the **one** ledger pass, no two surfaces can print different values for the *same named* quantity — that is the structural guarantee AC #2 asks for. Two *differently-named* numbers (Defined vs Tracked) may legitimately differ; that difference is not a clash, it is signal (see decision 3).

**3. Reconcile the two datasets in the ledger and surface real divergence as ONE non-fatal diagnostic — coordinated with 8.1 / Story 4.1's channel.** The ledger cross-checks the yaml story rows against the epics.md story set (match by story id). It exposes the reconciliation result (`UntrackedDefinedStories` = defined but absent from the yaml; `OrphanTrackedRows` = yaml rows with no matching defined story). When either is non-empty, emit **exactly one** non-fatal generation notice (never two, never fatal — NFR2), using the same diagnostics channel Story 8.1 uses for unrecognized status:
   - If Story 4.1's `AdapterDiagnostic` channel has landed → route via a `Skipped`/`Unsupported`-category diagnostic naming the count mismatch.
   - Else → emit a `GenerationEvent` through [`ConsoleUi.PrintInitialSummary`](../../src/SpecScribe/ConsoleUi.cs) and leave a `// TODO(4.1): fold into AdapterDiagnostic` marker.
   
   Record the choice in Completion Notes. This is the truthful answer to "structurally impossible": we don't paper over a real data drift with one averaged number — we compute each named count once and make the drift itself visible. [Owner decision, this story; see "Relationship to Story 8.1" below]

**4. Keep the sprint board yaml-faithful; do NOT reconcile the board's cards to the epics.md set (this story).** The board still renders every yaml `development_status` story row (including an orphan row as today's plain-text card — [`SprintTemplater.ResolveStory`](../../src/SpecScribe/SprintTemplater.cs:359-369)). Only its **totals** (subtitle, wheel "X/Y done") switch to reading the ledger's `StoriesTracked`/stage tally rather than recounting inline. Unifying the board's card set onto the epics.md entity collection is a bigger behavior change — explicitly deferred (noted as the open question at the end). [Owner decision, this story]

### The internal-consistency rule (AC #1, second clause): total == Σ segments, structurally

"A dashboard total always equals the sum of its own breakdown segments." Today the funnel/donut/wheel totals are computed *independently* of their segments (e.g. `total = p.StoriesTotal` beside segments summed from `StoryStatusCounts`) — they agree only because both trace to the same parse. Make it structural:

- Where a total is displayed **beside** its breakdown, DERIVE the total as the sum of the exact segment values rendered (`segments.Sum(s => s.Count)`), not from a parallel field. The sprint wheel already does this ([`SprintTemplater.cs:234`](../../src/SpecScribe/SprintTemplater.cs:234) `var total = counts.Sum(c => c.Count);`) — extend the same discipline to the ledger and any total/segment pair.
- Add a **generation-time invariant assertion** in the ledger constructor: the per-stage story tally must sum to the corresponding total (`Σ StoryStatusCounts == StoriesDefined`; `Σ tracked stage counts == StoriesTracked`). A mismatch is a bug in the classifier partition, not user data — assert in `Debug` and cover with a unit test. (Every story lands in exactly one `StatusStyles` stage — [`Charts.cs:697`](../../src/SpecScribe/Charts.cs:697) already relies on this partition property; the assertion just makes the reliance explicit.)

### Relationship to Story 8.1 (both touch dashboard/sprint/epics — coordinate)

- **Shared diagnostics channel.** 8.1's unrecognized-status notice and 8.2's count-divergence notice use the *same* non-fatal channel (`AdapterDiagnostic` if 4.1 landed, else `GenerationEvent`/`ConsoleUi`). If 8.1 lands first, follow its established wiring verbatim; if 8.2 lands first, use the `GenerationEvent` path and leave the `// TODO(4.1)` marker. **Do not create a third parallel notice mechanism.**
- **No logic overlap.** 8.1 changes how a *status word/class* is chosen; 8.2 changes where *counts* are sourced. They meet only at the diagnostics channel and at the fact that a count of "unrecognized" stories (if 8.1 introduces that stage) must be summable into the ledger's stage tally like any other stage — if 8.1 adds an `"unrecognized"` stage, the ledger's `Σ stages == total` assertion must tolerate it (it will, since the partition still covers every story). Note the ordering you built against in Completion Notes.

### Scope boundaries (read carefully)

- **This story does not change what any number *means*** — only where it is computed. If the refactor changes a *rendered* number, that's a divergence you just surfaced (fine, and expected on a repo whose yaml and epics.md have drifted) — call it out, don't silently "fix" data. Rendered *bytes* may change; see the golden-fingerprint note under Testing.
- **Don't touch requirements / coverage / git counts** (out of scope above).
- **Don't unify the sprint board's card set** onto epics.md (decision 4).
- **Don't add a new page or widget.** The ledger is plumbing; no new visible surface. (The status *legend* is 8.1's; the empty states are 8.5's; recency markers are 8.7's.)
- **Don't write back to any source.** Local-first, read-only invariant.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Create one `ProjectCounts` record** ([`src/SpecScribe/ProjectCounts.cs`](../../src/SpecScribe/ProjectCounts.cs), new) — an immutable `sealed record` with a static `Build(ProgressModel progress, SprintStatus? sprint, WorkInventory work)` factory and a `ProjectCounts.Empty`. Fields (name precisely per decision 2): `EpicsDefined`, `EpicsDrafted`, `EpicsPending`, `StoriesDefined`, `StoriesWithArtifact`, `TasksDone`, `TasksTotal`, `EpicsTracked`, `StoriesTracked`, per-stage tracked story tally (reuse the `StoryStageCounts` shape), `DeferredOpenItems`, `DirectChanges` (quick-dev count), `OpenActionItems`, and the reconciliation result (`UntrackedDefinedStories`, `OrphanTrackedRows` — id lists or counts). Pure, no IO, unit-testable exactly like `ProgressModel`.
- **Build it once** in `SiteGenerator.GenerateAll` right after `_progress`/`_sprint`/`workInventory` are known, cache it on a `_counts` field (mirroring `_progress`/`_sprint`), and thread it into: the dashboard render ([`WriteIndex`/`HtmlTemplater.RenderIndex` → `DashboardViewBuilder.Build`](../../src/SpecScribe/SiteGenerator.cs:742-743)), the sprint render ([`SprintTemplater.RenderIndex`, SiteGenerator.cs:849](../../src/SpecScribe/SiteGenerator.cs:849)), the epics-index build ([`EpicsViewBuilder.BuildIndex`](../../src/SpecScribe/EpicsViewBuilder.cs:20)), and the action-items render ([SiteGenerator.cs:957-961](../../src/SpecScribe/SiteGenerator.cs:957)). Add a `ProjectCounts` parameter to those entry points (defaulting to `ProjectCounts.Empty` where a null-safe default is needed, matching the `ProgressModel.Empty`/`SprintStatus.Empty` idiom).
- **Delete every render-site recount and read the ledger instead.** The audit teeth for AC #1 — replace at minimum:
  - `SprintTemplater.cs:61-62` `epicCount`/`storyCount` → `counts.EpicsTracked`/`counts.StoriesTracked`.
  - `SprintTemplater.StoryStageCounts` — keep the method but have it (and the wheel) read the ledger's precomputed tracked-stage tally, or make `StoryStageCounts` the ledger's builder input. One computation, consumed by both page summary and wheel.
  - `EpicsViewBuilder.BuildIndex:24` `EpicCount = model.Epics.Count` / `DraftedCount` → ledger fields (the epics-index subtitle "N epics · M with stories drafted", [`HtmlRenderAdapter.Epics.cs:23`](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs:23)).
  - `DashboardViewBuilder.BuildStatTiles`/`BuildProgressBars` — `p.StoriesTotal`, `p.EpicsTotal`, `work.QuickDev.Count`, `deferredCount` → ledger fields.
  - `DashboardView.OpenRetroActionItems = sprint?.OpenActionItems.Count` → `counts.OpenActionItems`.
  - `SprintTemplater.cs:219` sprint flag `sprint.OpenActionItems.Count` → `counts.OpenActionItems`.
  - `ActionItemsTemplater.cs:32` header `openItems.Count` → `counts.OpenActionItems`.
  - `HtmlRenderAdapter.Epics.cs:76` "Stories defined" stat card → ledger field.
  Grep the whole `src/SpecScribe` tree for `.Count(` / `.Count ` / `.Entries.Count` at *render/view-builder* sites and route each in-scope one through the ledger. Note any deliberately-left recount (per-lane `col.Count`, per-epic tallies) in Completion Notes with why.
- **Derive every displayed total from its own segments.** Any total shown beside a breakdown = `segments.Sum(...)`. Don't leave a parallel independent total field feeding a total-with-breakdown widget.
- **Assert the partition invariant in the ledger** (`Σ stage counts == story total`) in `Debug`, and back it with a unit test.
- **Reconcile epics.md ↔ yaml in the ledger** (match by story id) and emit exactly one non-fatal notice on divergence, via the 8.1/4.1-coordinated channel. Derive the notice **only** from input data so a from-scratch CI regen is identical (deterministic). Generation always completes (NFR2).
- **Keep `StoriesTracked`/`EpicsTracked` labeled "from sprint-status.yaml"** wherever shown (the sprint subtitle already does — preserve that phrasing) so a legitimate Defined≠Tracked difference never reads as a contradiction.

### DON'T

- **DON'T merge `StoriesDefined` and `StoriesTracked` into one "stories" field.** They are different quantities; collapsing them re-hides the drift instead of surfacing it.
- **DON'T fold requirements / coverage / commit counts into the ledger.** Out of scope; separate single sources.
- **DON'T re-count anything at a render site after this.** If a view needs a count, it comes from the ledger. A new inline `.Count()` in a templater is the exact anti-pattern this story exists to kill.
- **DON'T reconcile the sprint board's card set to epics.md** (decision 4 — deferred).
- **DON'T make a count divergence fatal or throw.** It degrades to a visible notice; generation completes (NFR2).
- **DON'T change the six lifecycle colors, chart legends, or status vocabulary** — that's 8.1's territory. Numbers only.
- **DON'T add a client-side script or new NuGet package.** Pure C# string/collection work over existing models. [memory: charts are pure SVG + links, no JS]

---

## Architecture Compliance

Relevant invariants [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)]:

- **Single source of truth** — this story's raison d'être: `ProjectCounts` becomes the single count authority the way `StatusStyles` (stage) + `--status-*` (color) are the single status authorities. It layers over `ProgressModel`, it does not add a competing parallel model. [memory: [[specscribe-status-token-system]] discipline, applied to counts]
- **Graceful degradation is contractual** — missing/partial `sprint-status.yaml` → `StoriesTracked`/`EpicsTracked`/action counts are 0/empty and the tracked surfaces omit cleanly (they already gate on `SprintStatus.IsEmpty`); a count divergence → visible non-fatal notice, never a throw. Generation always completes (NFR2).
- **Deterministic, generation-time-only output** — the ledger + notice derive solely from parsed input artifacts; a from-scratch regen of the same inputs is byte-identical. No per-visitor/cross-build state. (Same discipline Story 8.7 will formalize for recency.)
- **Framework-agnostic (NFR8)** — counts come from the projected domain models (`ProgressModel`/`SprintStatus`/`WorkInventory`), which a future framework adapter (Epic 4) already feeds; the ledger reads those models, so it inherits framework-neutrality with no per-framework branching.
- **Seed, not invariant** — do NOT force the Core/Adapters package split; `ProjectCounts` is a plain class beside `ProgressModel` in the monolith, following the established per-model pattern. [Source: [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]
- **Section view-model contract (Story 6.2)** — the dashboard/epics bodies render from host-neutral section view models built by `DashboardViewBuilder`/`EpicsViewBuilder` ([[story-6-2-section-view-models-live]]). Feed the ledger INTO those builders; keep the builder→adapter split intact so the byte-parity + `RenderParity.SectionFacts` harness still holds. The ledger is JSON-serializable data (a clean record), so it fits the 6.2 "data records" half — a plus for 6.4's export.

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: [SpecScribe.Tests.csproj](../../tests/SpecScribe.Tests/SpecScribe.Tests.csproj)]
- **Reuse, don't reinvent (all already in-repo):**
  - [`ProgressModel`](../../src/SpecScribe/ProgressModel.cs) / [`ProgressCalculator`](../../src/SpecScribe/ProgressCalculator.cs) — the epics.md count source; the ledger's primary input. `EpicProgress.StoryStatusCounts` is the ready-made per-stage partition.
  - [`SprintStatus`](../../src/SpecScribe/SprintStatus.cs) — the yaml ledger; `Entries` (filter `Kind`), `OpenActionItems`, `IsEmpty`. [`SprintTemplater.StoryStageCounts`](../../src/SpecScribe/SprintTemplater.cs:38-44) is the existing tracked-stage tally to move into / feed the ledger.
  - [`WorkInventory`](../../src/SpecScribe/WorkInventory.cs) — `QuickDev.Count` (Direct changes) + `Deferred.OpenItemCount` (deferred items).
  - [`StatusStyles.ForStory`/`ForSprint`](../../src/SpecScribe/StatusStyles.cs) — the stage classifiers the tallies group by (don't re-implement classification).
  - [`ConsoleUi.PrintInitialSummary`](../../src/SpecScribe/ConsoleUi.cs) + `GenerationEvent`/`GenerationOutcome` ([`GenerationReporter.cs`](../../src/SpecScribe/GenerationReporter.cs)) — the non-fatal notice surface; and [`AdapterDiagnostic`](../../src/SpecScribe/AdapterDiagnostic.cs) if 4.1's channel is live.

---

## File Structure Requirements

**New file:**

- [`src/SpecScribe/ProjectCounts.cs`](../../src/SpecScribe/ProjectCounts.cs) — the ledger record + `Build(...)` factory + `Empty` + the `Σ stages == total` assertion + the epics.md↔yaml reconciliation. Model its shape/XML-doc on `ProgressModel.cs`.

**Modified files (read fully before editing):**

- [`src/SpecScribe/SiteGenerator.cs`](../../src/SpecScribe/SiteGenerator.cs) — build `_counts` once after `_progress`/`_sprint`/`workInventory` are set (near [:116-124](../../src/SpecScribe/SiteGenerator.cs:116) and [:227](../../src/SpecScribe/SiteGenerator.cs:227)); thread it into the dashboard render ([:742-743](../../src/SpecScribe/SiteGenerator.cs:742)), sprint render ([:849](../../src/SpecScribe/SiteGenerator.cs:849)), action-items render ([:957-961](../../src/SpecScribe/SiteGenerator.cs:957)); emit the divergence notice on the events channel. **Preserve** phase ordering and the mid-chain caching semantics (only cache when the parse completed).
- [`src/SpecScribe/DashboardViewBuilder.cs`](../../src/SpecScribe/DashboardViewBuilder.cs) — `Build` takes `ProjectCounts`; `BuildStatTiles`/`BuildProgressBars` read ledger fields; `OpenRetroActionItems` from the ledger. **Preserve** the byte-load-bearing conditional (the 5th "Direct changes" tile only when `!work.IsEmpty`) — read the count from the ledger but keep the same gate.
- [`src/SpecScribe/HtmlTemplater.cs`](../../src/SpecScribe/HtmlTemplater.cs) — `RenderIndex` passes the ledger to `DashboardViewBuilder.Build` (thin thread-through).
- [`src/SpecScribe/EpicsViewBuilder.cs`](../../src/SpecScribe/EpicsViewBuilder.cs) — `BuildIndex` takes/reads the ledger for `EpicCount`/`DraftedCount`.
- [`src/SpecScribe/SprintTemplater.cs`](../../src/SpecScribe/SprintTemplater.cs) — `RenderIndex` + `RenderProgressWheel` take/read the ledger for subtitle counts + wheel total; `StoryStageCounts` either moves into the ledger or is fed by it (one computation). `AppendRetroButtons` flag reads `counts.OpenActionItems`.
- [`src/SpecScribe/ActionItemsTemplater.cs`](../../src/SpecScribe/ActionItemsTemplater.cs) — header count from the ledger (thread a `ProjectCounts` or an `int openCount` param sourced from it).
- [`src/SpecScribe/DashboardView.cs`](../../src/SpecScribe/DashboardView.cs) / [`EpicsView.cs`](../../src/SpecScribe/EpicsView.cs) — if any count field on these view models is now ledger-sourced, keep the field but populate from the ledger (don't add a redundant parallel field).

**Tests to update / add:**

- [`tests/SpecScribe.Tests/`](../../tests/SpecScribe.Tests) — new `ProjectCountsTests.cs`: `Build` maps each family correctly from `ProgressModel`/`SprintStatus`/`WorkInventory`; `StoriesDefined` vs `StoriesTracked` computed independently and both surfaced; reconciliation flags an untracked defined story and an orphan yaml row; `Σ stage counts == total` holds (and the `Debug` assertion doesn't fire on valid input); `Empty` is all-zero; determinism (two builds over identical input equal).
- Add a **generation-level** test (extend a `SiteGenerator*Tests`) proving cross-surface agreement: a temp `_bmad-output` where epics.md and sprint-status.yaml **agree** → the dashboard "Stories defined", epics-index "Stories defined", and sprint subtitle all render the same number, and **no** divergence notice. Then a fixture where they **disagree** (yaml has one extra/renamed story row) → a single non-fatal notice is reported, `GenerateAll` returns **no** `Error` outcome, and each surface shows its own correctly-named number.
- Update any existing `SprintTemplaterTests` / `HtmlTemplaterTests` / dashboard/epics rendering tests whose expected subtitle/stat text is unaffected in value but now flows through the ledger — most should be unchanged (same numbers); fix only genuine expectation shifts.
- **Golden fingerprint:** `SiteGeneratorAdapterTests.GoldenContentFingerprint_...` will change **only if** a rendered number changes (it changes iff this repo's epics.md and yaml currently disagree). If it changes, regenerate the constant per the drill below and confirm the change is a real, explained count reconciliation — not an accidental one. [memory: [[golden-diff-normalization-gotchas]]]

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). `ProjectCounts` is pure and unit-testable directly (no IO) — `ProgressCalculator`/`StatusStyles` tests are your model. Generation-level tests build a temp `_bmad-output` tree and assert on emitted HTML / `GenerateAll` outcomes (`AssertNoErrors` pattern — see [`SiteGeneratorTraceabilityTests`](../../tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs)).

Cover explicitly:

- **One source, many surfaces:** with agreeing inputs, the story/epic/action/deferred numbers on dashboard, epics-index, and sprint page are all equal AND all trace to the ledger (assert on rendered HTML for at least dashboard + sprint + epics-index).
- **Defined vs Tracked, named + independent:** `StoriesDefined` (epics.md) and `StoriesTracked` (yaml) are computed independently; a fixture where they differ keeps both correct and does not throw.
- **Total == Σ segments:** the ledger's story total equals the sum of its stage tally; the sprint wheel's "/Y" equals the sum of its lane counts; the funnel's drafted total equals `StoriesDefined`.
- **Divergence → one non-fatal notice:** a yaml with an orphan story row (or a missing one) yields exactly one reported notice, `GenerateAll` reports **no** `Error`, generation completes; agreeing inputs yield **no** notice.
- **Graceful degradation:** absent/empty `sprint-status.yaml` → tracked counts 0, tracked surfaces omit, no notice, no throw.
- **Determinism:** two generations over identical input produce identical ledger + identical rendered counts + identical notice text.
- **Regression:** all pre-existing progress/sprint/dashboard/epics/stylesheet tests pass; no rendered number changes except where a real reconciliation makes it (documented).

**Run:** `dotnet test` from repo root. Then a full generation against this repo: `dotnet run --project src/SpecScribe` (output → `SpecScribeOutput/`, the default — **do not** pass `--output docs/live`; that flag is vestigial/gitignored). Eyeball: the home "Stories defined" tile, the epics-index "Stories defined" stat, and the sprint page subtitle; confirm the story/epic numbers are coherent (and if this repo's epics.md/yaml have drifted, confirm the console prints the single reconciliation notice). [memory: [[generate-output-dir-is-specscribeoutput]]]

**Golden-diff drill (only if a rendered number changed):** freeze a fixture copy of `_bmad-output` + `docs/adrs` + `README.md` + `_bmad` in scratchpad, `git init` with fixed-date commits (+`--deep-git`), generate before/after, apply the 5 volatile-token normalizations, expect zero diffs **except** the intended count reconciliation; then regenerate the `GoldenContentFingerprint` constant (the test prints the new hash). Run twice for portability. [memory: [[golden-diff-normalization-gotchas]]]

---

## Previous Story Intelligence

**Story 8.1 (Canonical Status Model — `ready-for-dev`, sibling)** is the direct pattern to follow: one documented seam (`StatusStyles` there, `ProjectCounts` here), audit-and-reroute the call sites, surface divergence as a single non-fatal diagnostic on the shared channel, no new JS, no adapter contract. Coordinate the diagnostics channel (see Relationship above) and the ordering (whichever lands second reconciles). [Source: [8-1-canonical-status-model-with-portal-wide-legend.md](8-1-canonical-status-model-with-portal-wide-legend.md)]

**Story 6.2 (Section View Models — `review`)** decomposed the dashboard/epics bodies into builder→adapter section view models with a byte-parity + `RenderParity.SectionFacts` harness. Feed the ledger through those builders; don't collapse the split. The ledger is a clean JSON-serializable record, which is exactly the "data record" shape 6.2 established (and a gift to 6.4's export). [Source: [[story-6-2-section-view-models-live]]]

**Story 2.3 (Sprint Status)** built `SprintTemplater`/`StoryStageCounts` and the deliberate "the yaml is the *tracking* ledger, labeled separate from the derived Now & Next" stance — this story honors that (Defined vs Tracked stay distinct) while removing the *recount*. [Source: [`SprintTemplater.cs`](../../src/SpecScribe/SprintTemplater.cs); [2-3-sprint-status-page-and-dashboard-widget.md](2-3-sprint-status-page-and-dashboard-widget.md)]

**Recurring lessons that apply here:**

- **"What real input shape does this parser silently drop?"** (standing Epic 3 review lesson, [sprint-status.yaml action_items](sprint-status.yaml)). The count analog: *what real drift does this silently average away?* — AC #2's antidote is to compute each named count once and make the drift visible, never to hide it behind one number.
- **Truthfulness over convenience** (the whole status/count system's reason to exist). Don't "fix" a Defined≠Tracked difference by picking one — surface it. [Source: [`StatusStyles.cs:3-5`](../../src/SpecScribe/StatusStyles.cs)]
- **Split, don't absorb** (Epic 2/3 retro): if the sprint-board-set-unification (decision 4) tempts you mid-implementation, it's a *new* story, not a numbered addition to this one.

---

## Git Intelligence Summary

Recent history is planning/retro churn on `main` (`6.2, planning 6.3/6.4`; `Addressed UX issues and future planning`; `Epic 4 Retro`) — no in-flight code touches `ProgressModel`/`SprintTemplater`/`DashboardViewBuilder`, so this refactor is additive and uncontended. **Heed the worktree rule:** if this runs in a worktree, edit files at the **worktree path** — `main` has a background auto-committer, so never re-root paths at `C:\Dev\SpecScribe`. [memory: [[worktree-edits-must-target-worktree-path]]]

---

## Latest Technical Information

No external libraries or APIs are introduced — pure in-repo C# over existing models — so there is no version/security research to fold in. The only discipline note: any casing/formatting in derived text stays `CultureInfo.InvariantCulture` (matching `StatusStyles.TitleCase`), and count formatting reuses [`Charts.Plural`](../../src/SpecScribe/Charts.cs) for singular/plural agreement, exactly as the current subtitle does.

---

## Project Context Reference

- Epic 8 goal + FR/UX-DR/NFR coverage: [Source: [epics.md:1084-1088](../planning-artifacts/epics.md:1084)]
- Story 8.2 user story + both ACs: [Source: [epics.md:1116-1134](../planning-artifacts/epics.md:1116)]
- FR21 (single generator-side count source consumed by every widget): [Source: [epics.md:168](../planning-artifacts/epics.md:168)]
- NFR8 (insight/guidance surfaces framework-agnostic; degrade gracefully): [Source: [epics.md:82](../planning-artifacts/epics.md:82)]
- Site-wide UX review that seeded Epic 8 (the count-clash observation): [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md), [docs/UserJourneys.md](../../docs/UserJourneys.md)]
- Architecture invariants (single-source, graceful degradation, seed-not-invariant, deterministic): [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)]
- Single-source/section-view-model/golden-fingerprint/worktree/output-dir discipline: project memory ([[specscribe-status-token-system]]; [[story-6-2-section-view-models-live]]; [[golden-diff-normalization-gotchas]]; [[now-and-next-is-the-sprint-board]]; [[worktree-edits-must-target-worktree-path]]; [[generate-output-dir-is-specscribeoutput]]).

---

## Tasks / Subtasks

- [ ] **Task 1 — `ProjectCounts` ledger record (AC: #1, #2)**
  - [ ] Add `src/SpecScribe/ProjectCounts.cs`: immutable record, `Build(ProgressModel, SprintStatus?, WorkInventory)` factory, `Empty`. Fields for all four families, `Defined` vs `Tracked` named distinctly, per-stage tracked tally, reconciliation result. XML-doc it as THE portal-wide count authority (the seam future adapters/surfaces read).
  - [ ] Derive every total as `Σ` of its own segments; add the `Debug` invariant assertion `Σ stage counts == story total`.
  - [ ] Implement epics.md↔yaml reconciliation (match by story id) → `UntrackedDefinedStories` / `OrphanTrackedRows`.
- [ ] **Task 2 — Build once + thread through generation (AC: #1)**
  - [ ] In `SiteGenerator.GenerateAll`, build `_counts` after `_progress`/`_sprint`/`workInventory`; add `ProjectCounts` params to the dashboard/sprint/epics-index/action-items entry points and pass it (default `ProjectCounts.Empty` where needed). Preserve phase ordering + mid-chain caching.
- [ ] **Task 3 — Delete render-site recounts, read the ledger (AC: #1, #2)**
  - [ ] Replace `SprintTemplater` epic/story recounts + wheel total; `EpicsViewBuilder.BuildIndex` epic/drafted counts; `DashboardViewBuilder` stat-tile/progress-bar/action-item counts; `ActionItemsTemplater` header count; `HtmlRenderAdapter.Epics` "Stories defined" — all with ledger reads. Preserve the "Direct changes" tile gate.
  - [ ] Grep `src/SpecScribe` for render/view-builder-site `.Count(`/`.Entries.Count`; route in-scope ones through the ledger; document any deliberately-left recount in Completion Notes.
- [ ] **Task 4 — Divergence notice (AC: #2)**
  - [ ] Emit exactly one non-fatal notice when reconciliation finds drift; **coordinate with 8.1/4.1**: `AdapterDiagnostic` (`Skipped`/`Unsupported`) if the channel exists, else `GenerationEvent`/`ConsoleUi.PrintInitialSummary` + `// TODO(4.1)` marker. Deterministic, input-only. Record the choice in Completion Notes.
- [ ] **Task 5 — Tests (AC: #1, #2)**
  - [ ] `ProjectCountsTests` (mapping, Defined-vs-Tracked, reconciliation, Σ==total, Empty, determinism).
  - [ ] Generation-level cross-surface agreement test (agreeing fixture → equal numbers, no notice) + divergence test (disagreeing fixture → one notice, no `Error`, correctly-named numbers).
  - [ ] Fix any expectation-shifted rendering tests; regenerate the golden fingerprint **only** if a rendered number legitimately changed.
- [ ] **Task 6 — Full generation pass + manual verify (AC: #1, #2)**
  - [ ] `dotnet test` green; real generation to `SpecScribeOutput/`; eyeball dashboard/epics-index/sprint story+epic numbers for coherence; if this repo's inputs have drifted, confirm the single reconciliation notice prints.

## Dev Notes

- **The sharp edge:** don't collapse `StoriesDefined` and `StoriesTracked` into one number to "make them agree." The AC wants *structural* agreement for the *same named* quantity, and *honest surfacing* of the (legitimate) difference between two *different* quantities. Collapsing re-buries the exact drift this story exists to expose.
- **Byte parity:** if this repo's `epics.md` and `sprint-status.yaml` currently agree, the refactor should produce **byte-identical** output (same numbers, just re-sourced) — the golden fingerprint won't move. If they've drifted, expect a count change + a new notice; regenerate the fingerprint and explain the diff. Either way, run the golden test to know which case you're in. [memory: [[golden-diff-normalization-gotchas]]]
- **Keep the ledger a plain data record** (JSON-serializable) — it slots into Story 6.2's "data records" half and helps 6.4's webview export; don't hang rendering or IO off it.
- **Scope guard for later 8.x:** paired progress/readiness (8.3), state-aware next-steps (8.4), empty states (8.5), one-view-per-dataset (8.6), recency (8.7) all consume the counts this ledger centralizes — they are NOT this story. 8.2 gives them one trustworthy count source.

### Project Structure Notes

- Almost all change is one new record (`ProjectCounts.cs`) + thin thread-through edits at the four render entry points and their view builders. No new page, no new visible surface, no package restructure (deferred seed, Epics 4/6), no adapter contract (Epic 4).
- The ledger sits beside `ProgressModel` in the monolith, following the established per-model pattern — deliberately not forcing the aspirational Core/Adapters split.

### References

- [Source: [epics.md:1116-1134](../planning-artifacts/epics.md:1116)] — Story 8.2 user story + both ACs.
- [Source: [epics.md:1084-1088](../planning-artifacts/epics.md:1084), [epics.md:168,82](../planning-artifacts/epics.md:168)] — Epic 8 goal; FR21; NFR8.
- [Source: [ProgressModel.cs](../../src/SpecScribe/ProgressModel.cs), [ProgressCalculator.cs](../../src/SpecScribe/ProgressCalculator.cs)] — the epics.md count source (ledger input); `EpicProgress.StoryStatusCounts` partition.
- [Source: [SprintStatus.cs](../../src/SpecScribe/SprintStatus.cs), [SprintTemplater.cs:38-64,219,231-251](../../src/SpecScribe/SprintTemplater.cs)] — the yaml recounts + wheel to reroute.
- [Source: [WorkInventory.cs](../../src/SpecScribe/WorkInventory.cs)] — deferred + direct-change counts.
- [Source: [DashboardViewBuilder.cs:58,68-97,113-119](../../src/SpecScribe/DashboardViewBuilder.cs), [HtmlRenderAdapter.Epics.cs:23,76,88](../../src/SpecScribe/HtmlRenderAdapter.Epics.cs), [EpicsViewBuilder.cs:20-33](../../src/SpecScribe/EpicsViewBuilder.cs), [ActionItemsTemplater.cs:32](../../src/SpecScribe/ActionItemsTemplater.cs)] — the render sites to route through the ledger.
- [Source: [SiteGenerator.cs:80-124,227,742-743,849,957-961](../../src/SpecScribe/SiteGenerator.cs)] — where models are assembled and pages rendered; where `_counts` builds + threads.
- [Source: [Charts.cs:692-719](../../src/SpecScribe/Charts.cs)] — funnel total/segment partition property to preserve.
- [Source: [AdapterDiagnostic.cs](../../src/SpecScribe/AdapterDiagnostic.cs), [ConsoleUi.cs](../../src/SpecScribe/ConsoleUi.cs), [GenerationReporter.cs](../../src/SpecScribe/GenerationReporter.cs)] — the non-fatal notice channel (coordinate with 8.1/4.1).
- [Source: [8-1-canonical-status-model-with-portal-wide-legend.md](8-1-canonical-status-model-with-portal-wide-legend.md)] — sibling pattern (one seam, audit call sites, shared diagnostic channel).
- [Source: [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md), [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md)] — single-source, graceful degradation, seed-not-invariant.
- [Source: [spec-site-ux-review-journeys-and-feedback.md](spec-site-ux-review-journeys-and-feedback.md)] — the UX review that seeded Epic 8.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
