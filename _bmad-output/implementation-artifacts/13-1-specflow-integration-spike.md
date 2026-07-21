# Story 13.1: SpecFlow Integration Spike

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer preparing to support SpecFlow,
I want the SpecFlow artifact set mapped against the shared adapter contract before coverage work begins,
so that baseline coverage starts with a defined scope, known gaps, and no surprise conventions.

## Why this story exists (read first)

Epic 4 (`done`) built the framework-agnostic **foundation** — the `IArtifactAdapter` contract, `ArtifactBundle` projection carrier, and `BmadArtifactAdapter` as the one concrete implementation — but deliberately deferred all per-framework coverage. Its original Stories 4.3–4.7 (one per framework) were extracted 2026-07-10 into five appended, spike-led epics (11–15; see `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`) so each framework gets its own upfront mapping exercise instead of guessing. **This is the third of those five spikes** — Story 11.1 (Spec Kit) and Story 12.1 (GSD/GSD-Pi) both ran first (both `ready-for-dev`); Epic 13 is currently `backlog` (this story creation moves it to `in-progress`). Epics 14–15 (Squad, Superpowers) are untouched and repeat this same shape later.

**The one-line test for "is this in scope?":** if the change *surveys* SpecFlow repos, *classifies* their artifacts against the existing `ArtifactBundle`/model shapes, or *writes* a coverage map + non-goals list → in. If it *builds* a `SpecFlowArtifactAdapter`, parses a single SpecFlow file into a real model, or lands any `src/`/`tests/` change → out; that is Story 13.2 (SpecFlow Baseline Adapter Coverage, not yet created).

**Precedent for this shape — read both:** Story 11.1 (`11-1-spec-kit-integration-spike.md`, `ready-for-dev`) and Story 12.1 (`12-1-gsd-and-gsd-pi-integration-spike.md`, `ready-for-dev`) are the two immediately-preceding sibling spikes with the identical AC skeleton — mirror their structure and their Completion-Notes-as-deliverable discipline. Story 19.1 (Work-Graph Model and Coverage Spike) is the older pure-tracing, no-code precedent. None of them built production code, and neither should this. If you find yourself wanting to write a branch and a scaffold, stop — you have drifted into 13.2.

## ⚠️ Read this before anything else: which "SpecFlow" is this?

**There are two completely unrelated tools named SpecFlow, and confusing them will produce a worthless coverage map.**

1. **Classic SpecFlow** (`techtalk/SpecFlow`, now Reqnroll's predecessor) — a long-established, extremely well-known .NET **BDD test framework**: Gherkin `.feature` files, step-definition C# classes, test runners. This is what 95%+ of search results and prior knowledge about "SpecFlow" will surface. **It is not a spec-driven-development planning/tracking methodology and has no epics/stories/sprint/retro concept at all — it is a testing tool.** It is almost certainly NOT what Epic 13 means (FR17 groups it explicitly with Squad and Superpowers as "additional spec-driven frameworks," alongside Spec Kit/GSD in the same FR-cluster of AI-assisted planning tools).
2. **Modern SpecFlow CLI** (`@ceatoleii/specflow` on npm, `github.com/ceatoleii/specflow`, docs at `ceatoleii.github.io/specflow`) — a small, young (first public write-up May 2026) **spec-driven-development orchestration CLI** for Cursor: a 4-phase, 4-agent workflow (Refiner → SDD → Implementer → Reviewer) that persists its contract as markdown files in the repo instead of only in chat history. **This is almost certainly Epic 13's actual target** — it is the same *kind* of tool as Spec Kit/GSD (AI-assisted spec-driven planning), unlike the classic BDD tool.

**This spike's Task 1 must state explicitly which SpecFlow it investigated and why**, and flag unambiguously if evidence points the other way. Do not silently assume; the two tools share nothing but a name (this is a sharper version of Story 12.1's GSD-vs-BMad-GDS anagram trap — here the collision is with a far more famous, completely unrelated tool, so the risk of a wrong-tool coverage map is higher, not lower).

## What makes this spike different from 11.1 and 12.1 (do not just copy their answers)

1. **No epic/story hierarchy exists at all — the sharpest structural mismatch of the three spikes so far.** Spec Kit had a flat feature-folder layer (11.1's central question: does a feature map to an epic or a story?). GSD had a real three-level Milestone→Slice→Task hierarchy (12.1's central question: which level maps to which). **SpecFlow (modern CLI) has neither — it manages ONE active task at a time** (`task.md`/`plan.md`/`tasks.md`/`review.md` under `.agents-state/current/`), and completed work is archived to `history/YYYY-MM-DD-slug/` by date, with **no numbering, no epic grouping, and (per the available documentation) no evidence of multiple concurrent tracked features.** The central modeling question this spike must resolve: does a `history/` date-slug entry map to a flat `StoryInfo` with no epic parent (and if so, what synthesizes the required `EpicInfo` wrapper — a single implicit catch-all epic?), or does the shared `EpicsModel` simply not fit SpecFlow at all, pointing instead toward a lesser/generic-docs projection? Do not assume either; decide it and state the consequence for 13.2.
2. **The interesting artifacts may be gitignored, not just possibly stale (GSD) or absent-by-design (Spec Kit).** GSD's authority problem was "the DB is truth, the `.md` files are a possibly-stale projection." SpecFlow's is different and, in one sense, more severe: per the tool's own documented project layout, `.agents-state/` — the ONLY location containing `task.md`, `plan.md`, `tasks.md`, `review.md`, and the entire `history/` archive — is explicitly **gitignored runtime state** ("safe to delete when no active task"). This spike must resolve two distinct sub-questions, not conflate them: (a) on a fresh clone with no local flow history, is `.agents-state/` simply absent (honest NFR8 absence, nothing to render)? (b) on a machine where a developer HAS run flows locally, would SpecScribe's own file walk actually pick up `.agents-state/**/*.md`, given that `PathUtil.IsIgnoredSourceFile` [src/SpecScribe/PathUtil.cs:29-36] filters only by **file basename** (dotfiles, `~$` lock files, `.tmp`/`.crswap`) and does **not** consult `.gitignore` at all for the markdown source walk [`SiteGenerator.EnumerateSourceFiles`, src/SpecScribe/SiteGenerator.cs:3692-3698] — meaning `task.md`, `plan.md`, etc. (filenames with no leading dot) inside a dot-prefixed `.agents-state/current/` folder would NOT be filtered and WOULD be discovered on disk if physically present. (The `.gitignore`-aware walk is a *different*, unrelated feature — `GitMetrics.TryListFiles`/`EnumerateCodeFiles`, used only for the source-code treemap, src/SpecScribe/SiteGenerator.cs:3704-3718 — do not conflate the two file-discovery paths.) This is a genuine reproducibility finding to verify against a real repo: the *same commit* could render differently on two machines depending on whose local `.agents-state/` happens to be present, which is a materially different failure mode than "may be stale" — decide whether that argues for treating any discovered `.agents-state/` content as `Informational`/best-effort-only rather than a first-class rendered surface.
3. **No pre-existing About-SDD placeholder exists for SpecFlow at all — unlike all three of Spec Kit, GSD, and GSD-Pi.** `AboutSddTemplater.Frameworks` [src/SpecScribe/AboutSddTemplater.cs:10-18] lists exactly six entries: `bmad`, `gds`, `speckit`, `gsd`, `gsd-pi`, `superpowers` — **there is no `specflow` (or `squad`) row.** Correspondingly, `SiteNav.cs` has no `AboutSddSpecFlowOutputPath`, and `README.md`'s "Supported frameworks" table [README.md:19-24] lists BMad/BMad GDS/Spec Kit/GSD/GSD-Pi/Superpowers only — SpecFlow and Squad appear in neither the table nor the roadmap note [README.md:177-178]. This is a concrete, verifiable gap 11.1 and 12.1 did not have to name (their frameworks already had placeholder pages). Record it as an explicit Task 6 finding: 13.2 (or a small prerequisite chore) must add the `specflow` roster row, output path, and README table row mirroring the other five, before or alongside baseline coverage.
4. **The tool itself is thin on public documentation.** Unlike Spec Kit (a GitHub org project with a full README) or GSD (an established getting-started guide), the modern SpecFlow CLI's own docs site (`ceatoleii.github.io/specflow`) does not surface its full project-layout content on the landing page — the actual file/folder table only appeared after fetching the dedicated `/project-layout` sub-page directly, and no CLI-reference page content was retrievable at create-story. Treat every finding below as **lower-confidence than 11.1/12.1's**, and weight Task 2's live-repo verification accordingly — this is a smaller, younger tool with more documentation churn risk.

## Acceptance Criteria

1. **Given** representative current-version SpecFlow repositories, **when** the SpecFlow artifact set is surveyed against the shared adapter contract's `ArtifactBundle` and projection model, **then** a written coverage map classifies each SpecFlow artifact type as mappable, partially-mappable, or unsupported, **and** the target shared-model projection is named for each mappable type.

2. **Given** SpecFlow conventions that exceed the shared projection model or that SpecScribe will deliberately not support, **when** the spike documents its findings, **then** framework-extra data is recorded as candidate projection extensions or explicit non-goals, **and** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

[Source: `_bmad-output/planning-artifacts/epics.md:2435-2453`]

**Note on scope:** this story's own AC text does not require a mandatory declared "coverage tier" the way Story 12.1's does — its shape matches Story 11.1's exactly. Adopt the rendered/summarized/unsupported tier vocabulary (FR-4, introduced for GSD) only if it genuinely clarifies a finding here; don't force it in to match Epic 12 prematurely (mirroring 11.1's own guidance on this point).

## Context & Scope

### The contract this spike maps against (read the real code, not just the epic prose)

- **`IArtifactAdapter`** [src/SpecScribe/IArtifactAdapter.cs:19-38] — two methods: `AppliesTo(ForgeOptions, sourceFiles)` (cheap self-selection sniff, never throws) and `Ingest(ForgeOptions, sourceFiles, ProgressProjection?)` → `ArtifactBundle` (never throws; per-artifact failures ride `Diagnostics` instead).
- **`ArtifactBundle`** [src/SpecScribe/ArtifactBundle.cs:10-58] — the ONLY shape a new adapter must produce. Its fields, verbatim:
  | Field | Type | Null/empty-safe? |
  |---|---|---|
  | `Module` | `ModuleContext` | Never null — absent detection is `ModuleContext.None` |
  | `Sprint` | `SprintStatus?` | Null when absent |
  | `Retros` | `IReadOnlyList<RetroModel>` | Empty when none |
  | `Epics` | `EpicsModel?` | Null when absent/unparseable |
  | `Requirements` | `RequirementsModel?` | Null when absent |
  | `EpicsSourceFullPath` | `string?` | For generic-page exclusion |
  | `StoryArtifactsById` | `IReadOnlyDictionary<string,string>` | Story id → detail-artifact path |
  | `ConsumedSourceRelatives` | `IReadOnlyCollection<string>` | Files claimed by dedicated surfaces |
  | `Diagnostics` | `IReadOnlyList<AdapterDiagnostic>` | Non-fatal problems |
- **`AdapterDiagnostic(Category, RelativePath, Message)`** with `enum AdapterDiagnosticCategory` [src/SpecScribe/AdapterDiagnostic.cs:7-32] — `Category` is one of `Unsupported` (recognized but wrong shape), `Malformed` (should have parsed, didn't), `Skipped` (deliberately not ingested), `Error` (non-artifact-specific I/O), `Informational` (FYI, no action needed — the natural fit for the "content is present but machine-local only" finding above). **These five categories are the entire non-fatal vocabulary AC #2's "non-fatal notice" must map onto** — do not invent a sixth.
- **The one existing adapter to mirror, not copy verbatim:** `BmadArtifactAdapter` [src/SpecScribe/BmadArtifactAdapter.cs:11-344, `AppliesTo` at line 76-77]. Read it end to end — it is the working example of "self-selection sniff → discover files by well-known name/location → parse each family independently → never let one family's failure kill the others → diagnose non-fatally." Its `AppliesTo` sniffs a marker **directory** (`_bmad/`) at the repo root. SpecFlow's equivalent self-selection signal is almost certainly a marker **file** instead — `.specflow-version` or `.specflow-config.json` at the repo root (verify against real repos; note `AGENTS.md` alone is a poor signal since it is a cross-tool convention used by multiple unrelated CLIs, not SpecFlow-specific).

### The load-bearing gap this spike must surface, not solve — shared with 11.1 and 12.1

**No adapter registry exists yet.** `SiteGenerator` holds a single hardcoded field — `private readonly BmadArtifactAdapter _adapter = new();` [src/SpecScribe/SiteGenerator.cs:51] — with a comment stating that *"the adapter registry that selects among `IArtifactAdapter` implementations arrives with Stories 4.3+."* Those stories are exactly the ones relocated into Epics 11–15, so **the registry has no owner today.** `ARCHITECTURE-SPINE.md` explicitly leaves this open: *"Exact adapter loading mechanics... are implementation seeds"* [`_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:101`].

**Both 11.1 and 12.1 already raise this exact same gap.** Do not independently re-derive a competing registry design or propose a *third* ADR for it. Confirm the gap still exists, record that it is a **shared prerequisite across all five per-framework epics** (not specific to any one), and defer to whichever of 11.1/12.1's conclusions lands first (both are currently `ready-for-dev`, neither `done`, as of this story's creation — check their current status and Completion Notes before finalizing this section). Recommend (not build) the same minimal shape they describe (an ordered list of `IArtifactAdapter`s, first `AppliesTo` match wins, `BmadArtifactAdapter` stays the fallback). Per this project's ADR-creation-trigger discipline ([[adr-creation-trigger-gap-epic-10-retro]]), if an ADR is warranted it is **one** shared registry ADR, not one per spike.

### The "host-neutral" models are less framework-neutral than they sound

Read these before assuming any SpecFlow artifact maps cleanly:

- **`EpicsModel`/`EpicInfo`/`StoryInfo`** [src/SpecScribe/EpicsModel.cs] bake in BMad vocabulary: `EpicStatus { Drafted, Pending }`, `EpicSection { VerticalSlice, FurtherDevelopment }`, and `StoryInfo.Id` hard-typed as BMad's `"N.M"` two-level numbering with a resolved `ArtifactOutputPath`/`ArtifactSourcePath` pointing at a discrete per-story markdown file. **SpecFlow has no epic tier and no numbering at all** — see "What makes this different," point 1. Resolve explicitly whether a `history/YYYY-MM-DD-slug/` entry becomes a flat `StoryInfo` under one synthesized catch-all `EpicInfo`, or whether `EpicsModel` should be left null with SpecFlow content flowing through a different, lesser surface (e.g. treated like generic project docs). Do not leave this ambiguous — it is the central question, sharper here than in either prior spike because there is no natural grouping tier to fall back on at all.
- **`RequirementsModel`/`RequirementInfo`** [src/SpecScribe/RequirementsModel.cs:1-73] is BMad's numbered `FR`/`NFR`/`UX-DR`-with-a-Coverage-Map convention. SpecFlow's `task.md` carries per-task acceptance criteria (`AC1`, `AC2`, ...) scoped to a single in-flight task, not a cross-project numbered requirements catalog. Expect `Requirements = null` (mirroring Spec Kit's answer) unless the spike finds evidence of a project-wide requirements document this story's research did not surface — verify, don't assume either way.
- **`SprintStatus`** [src/SpecScribe/SprintStatus.cs] is `sprint-status.yaml`-shaped (a `development_status` map + `action_items`). No sprint/kanban/status-tracking file convention is documented for SpecFlow. Expect `Sprint = null`, confirmed against a real repo.
- **`RetroModel`** [src/SpecScribe/RetroModel.cs] is BMad's `epic-N-retro-*.md` convention. SpecFlow's `review.md` is a per-task PASS/FAIL verification-evidence artifact (acceptance-criteria checklist against the just-implemented task), not a retrospective — do not conflate the two. Expect `Retros = []`, confirmed against a real repo.

**Net implication to verify, not assume:** SpecFlow coverage will likely land almost entirely on a lesser/flattened `Epics`-adjacent surface (if any structured mapping is chosen at all) fed by the `history/` archive, with `Sprint`/`Retros`/`Requirements` staying structurally absent — the cleanest of the three spikes so far in that sense, but only because SpecFlow's granularity is coarser (one task, not a project), not because its content maps more richly.

### SpecFlow's real shape — freshly researched 2026-07-20 via the tool's own docs and public write-ups; treat as a starting hypothesis to confirm against real repos (per AC #1's literal "representative current-version SpecFlow repositories")

Live-fetched from `ceatoleii.github.io/specflow` (landing page + `/project-layout`) plus two independent 2026 write-ups (Medium, DEV Community) during create-story — **this is a small, young tool; re-verify, don't trust this table blindly:**

| SpecFlow concept | Path / shape | Committed or gitignored? | Closest `ArtifactBundle` candidate |
|---|---|---|---|
| Install markers | `AGENTS.md`, `.specflow-version`, `.specflow-config.json`, `.specflow-tools.json` at repo root; `.agents/` (engine: `rules/`, `templates/`) | Committed | `AppliesTo` self-selection signal — prefer `.specflow-version`/`.specflow-config.json` over bare `AGENTS.md` (collision risk — other tools also ship an `AGENTS.md`) |
| Team-maintained project docs | `.agents-docs/architecture.md`, `conventions.md`, `verification.md`, optional `design-system.md` | Committed | Closest thing to planning/architecture docs; no numbered-requirements shape |
| Optional Linear sync config | `.specflow-linear.json` | Committed | Informational only; out of scope |
| Active task state | `.agents-state/.flow-enabled`, `.agents-state/current/{phase.md,task.md,plan.md,tasks.md,review.md,linear.json}` | **Gitignored** ("safe to delete when no active task") | `task.md`≈requirement/AC, `plan.md`≈design, `tasks.md`≈checklist, `review.md`≈verification evidence — all runtime-only |
| Completed task archive | `.agents-state/history/YYYY-MM-DD-slug/` | **Gitignored** (nested under the same gitignored `.agents-state/`) | Closest `StoryInfo` candidate — flat, dated, no epic parent, no numbering |
| Cursor IDE adapter | `.cursor/rules/_specflow.mdc` | Committed | Informational only — SpecScribe reads artifacts, not IDE routing rules |
| Epic/project hierarchy | **None found** — one active task at a time | — | No `EpicInfo` analog exists; central modeling decision (see above) |
| Sprint/kanban file | **None found** | — | `Sprint` stays null — not a gap |
| Retrospective notes | **None found** (`review.md` is per-task verification, not a retro) | — | `Retros` stays empty — not a gap |
| Numbered FR/NFR requirements | **None found** — `task.md` has per-task `AC1`/`AC2`... prose, scoped to one task | — | `Requirements` likely null or a lesser per-task substitute — spike decides |

**Do not treat this table as ground truth.** The docs site itself is thin (the full layout only surfaced from a direct sub-page fetch, and a CLI-reference page referenced by the site was not retrievable at all during create-story) — clone or inspect a real `specflow init`-scaffolded repository before finalizing the coverage map, and re-confirm the gitignore status of `.agents-state/` directly (e.g. check a real repo's `.gitignore` file) rather than trusting the docs prose alone.

### Existing SpecFlow surfaces already in the portal: there are none — this is itself a finding

Unlike Spec Kit, GSD, and GSD-Pi (all of which already have an `AboutSddTemplater.Frameworks` roster entry, a routed output page, and a README table row awaiting real content), **SpecFlow has no placeholder anywhere in the codebase:**

- `AboutSddTemplater.Frameworks` [src/SpecScribe/AboutSddTemplater.cs:10-18] lists `bmad`, `gds`, `speckit`, `gsd`, `gsd-pi`, `superpowers` only — no `specflow` row, and (incidentally) no `squad` row either.
- `SiteNav.cs` has no `AboutSddSpecFlowOutputPath` constant and no routed page.
- `README.md`'s "Supported frameworks" table [README.md:19-24] and its roadmap note [README.md:177-178] list Spec Kit/GSD/GSD-Pi/Superpowers as "🧭 Planned" — SpecFlow and Squad are absent from both.

Record this explicitly as a Task 6 finding: **13.2 (or a small standalone prerequisite chore) must add the `specflow` roster entry, output path, and README row, mirroring the existing five-framework pattern**, before or alongside baseline coverage — there is no pre-wired route to build into the way 11.2/12.2 have.

**ADRs are a side-channel, not part of `ArtifactBundle` at all.** There is no dedicated ADR parser class and no `ArtifactBundle` field for them — `docs/adrs/*.md` are hand-authored, dated, numbered decision records read via a separate, always-optional `ForgeOptions.AdrSourceRoot` path, entirely outside the `IArtifactAdapter` contract. **Classify `.agents-docs/architecture.md` explicitly** against this side-channel: it is a single, continuously-updated living document (stack conventions), not a directory of discrete numbered decision records — very likely NOT a fit for the ADR side-channel (unlike Spec Kit's `constitution.md` and GSD's `DECISIONS.md`, which at least resemble one governance/decision document each). State plainly whether it should instead be treated as a generic planning/module doc, a framework-extra candidate, or out of scope — don't leave it unclassified, and don't force-fit it into the ADR side-channel just because the two prior spikes found an ADR-shaped candidate.

### Deliberate non-goals (seed list — spike may extend with rationale)

- **Building `SpecFlowArtifactAdapter`** or any parser — that's 13.2.
- **Adding the missing `specflow` About-SDD roster entry/route/README row** — name it as a Task 6 finding for 13.2 (or a small prerequisite chore) to close; do not add it from this story.
- **Designing the adapter registry** — name the shared gap (Task 6), coordinate with 11.1/12.1, don't design or implement it here.
- **Extending `ArtifactBundle`/`EpicsModel`/etc. with new fields** — the spike records *candidate* projection extensions (AC #2); it does not land them.
- **Proposing a third registry ADR** — if an ADR is warranted, it is ONE shared registry ADR coordinated with 11.1/12.1, not per-framework.
- **A new authoring schema** for SpecFlow content — SpecScribe reads SpecFlow's existing conventions as-is.
- **Conflating classic .NET SpecFlow (BDD testing) with this spike's target** — see the collision-trap section above.

## Tasks / Subtasks

- [ ] **Task 1 — Disambiguate "SpecFlow" and confirm the contract shapes against live code (AC: #1)**
  - [ ] State explicitly, with evidence, that this spike targets the modern `@ceatoleii/specflow` AI spec-driven-development CLI, not the classic `.NET`/Gherkin BDD testing framework — cite the FR17 grouping (alongside Squad/Superpowers as "additional spec-driven frameworks") as the reasoning, and flag immediately if any evidence points the other way.
  - [ ] Read `IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`, `EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs` in full (paths above) — do not rely solely on this story's summary tables.
  - [ ] Confirm (or correct) this story's claim that no adapter-selection registry exists (`SiteGenerator.cs:51`), and check whether Story 11.1 or 12.1 has already landed/recommended a registry shape (read their Completion Notes if `done`).

- [ ] **Task 2 — Obtain and inspect a representative current-version SpecFlow repository (AC: #1, #2)**
  - [ ] Fetch/inspect `github.com/ceatoleii/specflow` and `ceatoleii.github.io/specflow` (including `/project-layout` and, if reachable, `/cli-reference`) directly, or run `npx @ceatoleii/specflow init` against a scratch repo, to confirm the exact file/folder layout — the table above is a hypothesis from thin public docs, not a repo inspection.
  - [ ] Confirm directly (e.g. by reading a real `.gitignore`) whether `.agents-state/` is actually gitignored as documented, and whether `history/` truly nests under it or (per one secondary source) lives at the repo root instead — the two sources disagreed and this spike must resolve which is correct.
  - [ ] Confirm whether SpecScribe's markdown source walk [`SiteGenerator.EnumerateSourceFiles`, src/SpecScribe/SiteGenerator.cs:3692-3698 + `PathUtil.IsIgnoredSourceFile`, src/SpecScribe/PathUtil.cs:29-36] would in fact discover `.agents-state/current/task.md` and `.agents-state/history/**/*.md` when physically present on disk (the filenames don't start with `.`, so the existing filter shouldn't exclude them) — verify this empirically against a scratch repo with an active/completed flow, don't reason about it from code alone.
  - [ ] Confirm whether SpecFlow supports more than one task/feature tracked at a time, or is genuinely single-active-task as documented.

- [ ] **Task 3 — Resolve the missing hierarchy tier (AC: #1)**
  - [ ] State explicitly whether a `history/YYYY-MM-DD-slug/` entry maps to a flat `StoryInfo` under one synthesized catch-all `EpicInfo`, or whether `EpicsModel` doesn't fit at all and SpecFlow content should route through a lesser/generic-docs surface instead — the central modeling question, with no epic-tier precedent from 11.1/12.1 to lean on this time.
  - [ ] Classify `.agents-docs/architecture.md` explicitly: ADR side-channel, new `ArtifactBundle` field/generic planning doc, or out of scope — see the "ADRs are a side-channel" note above; don't leave it unclassified, and don't force it into the ADR side-channel just because 11.1/12.1 found ADR-shaped candidates for their frameworks.
  - [ ] State explicitly, confirmed against a real repo: is `Sprint` null, is `Retros` empty, is `Requirements` null or a lesser per-task substitute (`task.md`'s AC1/AC2 prose).

- [ ] **Task 4 — Framework-extra data and deliberately-unsupported conventions (AC: #2)**
  - [ ] For any SpecFlow convention richer than the shared model (e.g. the four-phase/four-agent workflow itself, `.agents-docs/conventions.md`/`verification.md`/`design-system.md`, the Linear-sync integration, the Cursor IDE adapter), record it as either a candidate projection extension (name what it would add) or an explicit non-goal (with rationale).
  - [ ] For anything SpecScribe will deliberately not support, name the exact `AdapterDiagnosticCategory` (`Unsupported`/`Malformed`/`Skipped`/`Error`/`Informational`) its non-fatal notice would use and draft the notice's wording, mirroring `BmadArtifactAdapter`'s existing diagnostic messages for tone/specificity. In particular, draft the notice for "`.agents-state/` content discovered but is gitignored runtime state that may not exist on other machines/clones for the same commit."

- [ ] **Task 5 — Name the adapter-registry gap and the missing About-SDD placeholder as explicit findings (AC: #1, #2)**
  - [ ] Confirm the registry-gap claim against `SiteGenerator.cs`. State plainly that Story 13.2 cannot wire in a third adapter without SOME selection mechanism, that this gap is **shared with 11.1 and 12.1** (not SpecFlow-specific), and that whichever coverage story lands first closes it once for all frameworks. Do NOT propose a third registry ADR.
  - [ ] Confirm the missing `AboutSddTemplater.Frameworks` roster entry / `SiteNav` route / README row (verify against current `src/SpecScribe/AboutSddTemplater.cs`, `SiteNav.cs`, `README.md`) and record that 13.2 (or a small prerequisite chore) must add them, mirroring the pattern already used for the other five frameworks.

- [ ] **Task 6 — Record findings; no production code (AC: #1, #2)**
  - [ ] Write the coverage map (artifact-type × classification × target projection table + missing-hierarchy-tier decision + `.agents-docs/architecture.md` ADR-side-channel classification + non-goals + shared registry-gap finding + missing-placeholder finding + 13.2 recommendation) into this story's **Completion Notes**, mirroring Stories 11.1's and 12.1's convention.
  - [ ] Do **not** land production `src/**`/`tests/**` changes from this story. No new ADR unless Task 5 concludes a genuine fork exists AND neither 11.1 nor 12.1 has already covered it — coordinate to keep it a single shared registry ADR.

### Review Findings

_(populated during code-review)_

## Dev Notes

### Spike constraints (load-bearing)

- **Tracing + live repo inspection, not code.** Evidence comes from reading `src/SpecScribe/*.cs` and reading/fetching the real SpecFlow CLI's repo/docs — not from writing a prototype adapter. If you catch yourself scaffolding `SpecFlowArtifactAdapter.cs`, stop; that's 13.2.
- **No new authoring schema.** SpecScribe never asks SpecFlow users to add SpecScribe-specific files — coverage must derive from SpecFlow's own existing `.agents-*`/`AGENTS.md` conventions.
- **Verify, don't assume — and verify the gitignore claim specifically.** This story's shape table was built from a thin, young tool's docs site (two secondary write-ups disagreed on where `history/` lives) — confirm the `.agents-state/` gitignore behavior and the exact `history/` location against a real repo or `.gitignore` file before finalizing the coverage map.
- **NFR8.** Genuinely-absent artifact families (`Sprint`, `Retros`, likely `Requirements`, likely `Epics` in its literal shape) are honest absence, not gaps to fill. Don't recommend inventing an epic/sprint/retro convention for SpecFlow users — that would violate the framework-neutral principle.
- **Do not conflate classic .NET SpecFlow with this spike's target** — see the collision-trap section; this is the single easiest way to produce a worthless coverage map for this particular story.

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md:34-40] — one shared projection/rendering core; any future SpecFlow adapter only translates into `ArtifactBundle`, never reinterprets shared rendering.
- **AD-2** [ARCHITECTURE-SPINE.md:42-48] — the adapter boundary is source → normalized records; this spike maps SpecFlow source shapes onto that exact contract, nothing downstream.
- **AD-4** [ARCHITECTURE-SPINE.md:58-64] — any future SpecFlow-specific insight enrichment must stay additive/non-blocking, same as BMad's git-pulse/ADR-coverage providers.
- **NFR8** [epics.md:99]: *"Insight surfaces and guidance affordances... are framework-agnostic in shared rendering: framework-specific content flows through the adapter contract, and surfaces degrade gracefully — absent, not broken or misleadingly empty — when a methodology lacks the corresponding artifact."* This is the literal rule behind "Sprint=null/Retros=empty/Requirements=null-or-lesser is correct, not a gap" for a framework this coarse-grained.
- **Seed, Not Invariant** [ARCHITECTURE-SPINE.md:98-102]: exact adapter-loading mechanics and package/namespace split are explicitly open — do not let this spike commit to a `src/SpecScribe.Adapters.SpecFlow` package (the project is still single-namespace, single-project — [[epic-4-adapter-contract-scope]] memory: "no package split").

### Anti-patterns to prevent

- **Conflating classic .NET SpecFlow (Gherkin/BDD testing) with the modern `@ceatoleii/specflow` AI-SDD CLI** — the single biggest risk specific to this story.
- Assuming a `history/` date-slug entry is 1:1 a `StoryInfo` (or that `EpicsModel` fits at all) without stating the decision and its consequences — there is no epic-tier precedent from 11.1/12.1 to lean on.
- Treating `.agents-state/` content as reliably present the way BMad's `_bmad-output/` or Spec Kit's `specs/` are — it is documented as gitignored, machine-local runtime state.
- Forcing `.agents-docs/architecture.md` into the ADR side-channel just because 11.1/12.1 each found an ADR-shaped candidate for their framework — it may not be one; classify on the evidence.
- Building the missing `specflow` About-SDD placeholder (roster/route/README) instead of naming it as a Task 5 finding for 13.2 to close.
- Proposing a third, SpecFlow-specific adapter-registry ADR instead of coordinating one shared registry decision with 11.1/12.1.
- Silently committing to a package/namespace split (`SpecScribe.Adapters.SpecFlow`) — aspirational sketch, not current architecture.
- Treating this story's thin-docs hypothesis table as verified fact instead of confirming it against a real, current SpecFlow repo.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/13-1-specflow-integration-spike.md`
- Sprint key: `13-1-specflow-integration-spike`
- Downstream story key (not created by this spike): `13-2-specflow-baseline-adapter-coverage`
- No `src/`/`tests/` touches expected.
- No ADR file expected unless Task 5 concludes a genuine architecture fork (registry design) not already covered by 11.1/12.1 — if so, it is ONE shared registry ADR (`docs/adrs/`, next number, indexed in `docs/adrs/README.md`), coordinated with the two prior spikes, escalated rather than decided silently.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:2429-2453`] — Epic 13 intro, FR17 line, Story 13.1 (verbatim ACs quoted above) + Story 13.2 (downstream coverage story, its ACs quoted for scope-boundary context).
- [Source: `_bmad-output/planning-artifacts/epics.md:58-59`] — FR17 exact statement ("Add adapter coverage for additional spec-driven frameworks (for example SpecFlow, Squad, and Superpowers) through the shared adapter contract") and its Epic 13–15 coverage line.
- [Source: `_bmad-output/planning-artifacts/epics.md:99`] — NFR8 exact wording.
- [Source: `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`] — why Epics 11-15 exist, the fixed framework→epic mapping (13 = SpecFlow / FR17), the "X.1 spike, X.2 coverage" pattern.
- [Source: `_bmad-output/implementation-artifacts/11-1-spec-kit-integration-spike.md`, `12-1-gsd-and-gsd-pi-integration-spike.md`] — the two immediately-preceding sibling spikes; mirror their structure, Completion-Notes-as-deliverable convention, and shared registry-gap finding.
- [Source: `src/SpecScribe/IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`] — the contract + its one reference implementation (line anchors verified against current main at create-story).
- [Source: `src/SpecScribe/EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs`] — the "host-neutral" model shapes, BMad-specific vocabulary baked in.
- [Source: `src/SpecScribe/SiteGenerator.cs:51`] — the hardcoded single-adapter field; the shared registry gap.
- [Source: `src/SpecScribe/SiteGenerator.cs:3692-3698`, `src/SpecScribe/PathUtil.cs:29-36`] — the markdown source-file walk and its basename-only ignore filter (does NOT consult `.gitignore`), verified during create-story — the basis for the "`.agents-state/` may be discovered when locally present" finding.
- [Source: `src/SpecScribe/SiteGenerator.cs:3704-3718`] — the unrelated, `.gitignore`-aware `git ls-files` walk used only for the source-code treemap; do not conflate with the markdown source walk above.
- [Source: `src/SpecScribe/AboutSddTemplater.cs:10-18`] — the framework roster; confirmed no `specflow` (or `squad`) entry exists, unlike Spec Kit/GSD/GSD-Pi/Superpowers.
- [Source: `README.md:19-24,177-178`] — the "Supported frameworks" table and roadmap note; confirmed SpecFlow and Squad are absent from both.
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1/AD-2/AD-4 (lines 34-40, 42-48, 58-64), Seed-not-invariant (98-102)] — the invariants this spike must respect and the open seeds it must not over-commit.
- [Source: `_bmad-output/implementation-artifacts/19-1-work-graph-model-and-coverage-spike.md`] — the older pure-tracing spike; Completion-Notes-as-deliverable convention.
- [Web: `ceatoleii.github.io/specflow` + `ceatoleii.github.io/specflow/project-layout`, fetched live 2026-07-20] — the committed vs. gitignored file layout (`AGENTS.md`, `.specflow-version`, `.specflow-config.json`, `.agents/`, `.agents-docs/` committed; `.agents-state/` — including `current/` and `history/` — gitignored).
- [Web: Medium "SpecFlow: Spec-Driven Development (SDD) with a 4-Agent AI Workflow in Cursor" (May 2026) and DEV Community "SpecFlow: Multi-Agent SDD in Cursor," fetched live 2026-07-20] — the 4-phase/4-agent workflow (Refiner/SDD/Implementer/Reviewer), `task.md`/`plan.md`/`tasks.md`/`review.md` shapes, and the `history/YYYY-MM-DD-slug/` archive convention; one source placed `history/` at the repo root rather than nested under `.agents-state/` — Task 2 must resolve the discrepancy against a real repo.
- **Memory:** [[epic-4-adapter-contract-scope]] (Epic 4 foundation-only, no package split, spike-led per-framework pattern), [[adr-creation-trigger-gap-epic-10-retro]] (propose an ADR for architecture-shaped decisions — but ONE shared registry ADR, coordinated with 11.1/12.1, not per-spike), [[story-12-1-gsd-gsd-pi-spike-seeded]] (the immediately-preceding sibling spike's own distinguishing findings).

### Git intelligence summary

No SpecFlow code, adapter, or prior exploration exists anywhere in this repo beyond the FR17 mention and the sprint-status/epics scaffolding (confirmed via grep across `src/`, `docs/`, `epics.md`, `README.md`) — and, notably, unlike Spec Kit/GSD/GSD-Pi/Superpowers, there isn't even a placeholder About-SDD page or roster entry for SpecFlow yet. This spike starts from a genuinely clean slate on both sides: no SpecFlow-specific code in this repo, and (per the tool's own thin public docs) a real but young and lightly-documented target tool. Stories 11.1 (Spec Kit) and 12.1 (GSD/GSD-Pi) are the two immediately-preceding sibling spikes (both `ready-for-dev`) and the closest structural templates. Recent commits (Epic 10 cleanup, retro work, 11.1/12.1 create-story) are unrelated to this story's implementation scope.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-20 — Story 13.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map + missing-hierarchy-tier decision + `.agents-docs/architecture.md` ADR-side-channel classification + missing-About-SDD-placeholder finding + 13.2 scope recommendation; no production code. Third of the five per-framework spike-led epics (11-15); flags the sharpest name-collision risk (classic .NET SpecFlow vs the modern AI-SDD CLI) and the coarsest artifact granularity (single active task, gitignored runtime state, no epic tier) of the three spikes run so far.
