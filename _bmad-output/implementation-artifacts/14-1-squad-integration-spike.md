# Story 14.1: Squad Integration Spike

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer preparing to support Squad,
I want the Squad artifact set mapped against the shared adapter contract before coverage work begins,
so that baseline coverage starts with a defined scope, known gaps, and no surprise conventions.

## Why this story exists (read first)

Epic 4 (`done`) built the framework-agnostic **foundation** — the `IArtifactAdapter` contract, `ArtifactBundle` projection carrier, and `BmadArtifactAdapter` as the one concrete implementation — but deliberately deferred all per-framework coverage. Its original Stories 4.3–4.7 (one per framework) were extracted 2026-07-10 into five appended, spike-led epics (11–15; see `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`) so each framework gets its own upfront mapping exercise instead of guessing. **This is the third of those five spikes** — Story 11.1 (Spec Kit) and Story 12.1 (GSD/GSD-Pi) both ran first (both `ready-for-dev`); Epic 14 is currently `backlog` (this story creation moves it to `in-progress`). Epic 13 (SpecFlow) is untouched; Epic 15 (Superpowers) is untouched.

**The one-line test for "is this in scope?":** if the change *surveys* Squad repositories, *classifies* their artifacts against the existing `ArtifactBundle`/model shapes, or *writes* a coverage map + non-goals list → in. If it *builds* a `SquadArtifactAdapter`, parses a single Squad file into a real model, adds the missing `AboutSddTemplater`/`SiteNav` roster entry, or lands any `src/`/`tests/` change → out; that is Story 14.2 (Squad Baseline Adapter Coverage, not yet created).

**Precedent for this shape — read both:** Story 11.1 (`11-1-spec-kit-integration-spike.md`, `ready-for-dev`) is the original template for this AC skeleton. Story 12.1 (`12-1-gsd-and-gsd-pi-integration-spike.md`, `ready-for-dev`) is the second sibling and shows how a spike can diverge hard from 11.1's answers when the framework's shape genuinely differs (two frameworks, mandatory coverage tiers, DB-vs-markdown authority). **This spike diverges even harder — read "What makes this spike different" below before touching the Squad docs.** Neither built production code, and neither should this. If you find yourself wanting to write a branch and a scaffold, stop — you have drifted into 14.2.

## What makes this spike different from 11.1 and 12.1 (do not just copy their answers)

Squad is **not another spec/epic/story planning tool with a different file format** — it is structurally a different kind of thing, and that is the primary finding this spike must nail down:

1. **Squad's artifact set centers on a persistent AI-agent TEAM, not feature planning docs.** Live-verified 2026-07-20 (github.com/bradygaster/squad): the `.squad/` directory holds `team.md` (roster), per-agent `agents/{name}/charter.md` (identity/expertise/voice) + `agents/{name}/history.md` (that agent's accumulated project knowledge), `routing.md` (who handles what), `decisions.md` (shared decision log), `ceremonies.md` (sprint ceremony *configuration*, format unconfirmed), plus `casting/` (agent-naming policy/registry/history), `skills/` (compressed cross-session learnings), `identity/now.md` + `identity/wisdom.md` (team-level focus/patterns), and `log/` (searchable session archive). **None of these are "a spec.md" or "an epics.md with FR/NFR numbers."** Unlike Spec Kit's flat per-feature folders or GSD's Milestone→Slice→Task hierarchy, Squad's own docs do not describe an equivalent feature/work-item hierarchy at all — confirm this against the real repo/docs before concluding `Epics`/`Requirements` are null; do not assume 11.1's "stays null" table transfers, but also do not force-fit team-roster concepts onto `EpicsModel` just to have something mappable.
2. **The nearest sprint/requirements analogs are weak and unverified, unlike GSD's clean STATE.md/REQUIREMENTS.md hits.** `.squad/ceremonies.md` is the only candidate for `SprintStatus`, and its exact shape (is it YAML-like `development_status`, or free prose config?) was **not resolved from docs alone** — Task 2 below requires fetching the real file (a scaffolded `squad init` output, or the repo's own dogfooded `.squad/` if it has one) to confirm or refute. There is no candidate at all for `RequirementsModel` (no FR/NFR numbering, no requirement-contract file) — expect `Requirements = null`, but confirm rather than default to it out of pattern-matching against Spec Kit.
3. **No existing `AboutSddTemplater`/`SiteNav`/`README.md` placeholder exists for Squad — unlike every other framework spiked so far.** Spec Kit, GSD, GSD-Pi, and Superpowers all already have a `Supported: false` roster row, a routed output page, and a README "Planned" row (see `AboutSddTemplater.cs:10-18`). **Squad and SpecFlow are the only two of the five with zero pre-existing surface.** This spike does not add that placeholder (still out of scope — that is either 14.2's job or a small separate seam-extension), but it must name this as a finding: 14.2 will need to extend the `Frameworks` roster tuple, add a `SiteNav.AboutSddSquadOutputPath`, and add a README row, none of which exist today.
4. **Squad's `decisions.md` is the closest analog to Spec Kit's `constitution.md` / GSD's `DECISIONS.md`** — a project-level decision log outside any BMad shape. Classify it the same way those spikes did: ADR side-channel, new `ArtifactBundle` field, or out of scope. Do not leave it unclassified.
5. **`squad build` / `squad.config.ts` is a second, code-first authoring path** (a TypeScript SDK compiles down to the same `.squad/*.md` files `squad init` scaffolds directly). SpecScribe reads the resulting markdown either way — note this as a detail, not a modeling fork; the ingested artifact shape is the same regardless of which authoring path produced it.

## Acceptance Criteria

1. **Given** representative Squad repositories, **when** the Squad artifact set is surveyed against the shared adapter contract's `ArtifactBundle` and projection model, **then** a written coverage map classifies each Squad artifact type as mappable, partially-mappable, or unsupported, **and** the target shared-model projection is named for each mappable type.

2. **Given** Squad conventions that exceed the shared projection model or that SpecScribe will deliberately not support, **when** the spike documents its findings, **then** framework-extra data is recorded as candidate projection extensions or explicit non-goals, **and** deliberately-unsupported conventions are listed with rationale and the non-fatal notice they will emit, giving the coverage story an agreed scope boundary.

[Source: `_bmad-output/planning-artifacts/epics.md:2481-2499`]

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
- **`AdapterDiagnostic(Category, RelativePath, Message)`** with `enum AdapterDiagnosticCategory` [src/SpecScribe/AdapterDiagnostic.cs:7-43] — `Category` is one of `Unsupported` (recognized but wrong shape), `Malformed` (should have parsed, didn't), `Skipped` (deliberately not ingested), `Error` (non-artifact-specific I/O), `Informational` (FYI, no action needed). **These five categories are the entire non-fatal vocabulary this spike's "unsupported conventions" and "non-fatal notice they will emit" (AC #2) must map onto** — do not invent a sixth.
- **The one existing adapter to mirror, not copy verbatim:** `BmadArtifactAdapter` [src/SpecScribe/BmadArtifactAdapter.cs:11-344]. Read it end to end — it is the working example of "self-selection sniff → discover files by well-known name/location → parse each family independently → never let one family's failure kill the others → diagnose non-fatally." Its `AppliesTo` sniffs `_bmad/` at the repo root [BmadArtifactAdapter.cs:76-77]; the equivalent Squad signal is almost certainly a `.squad/` directory (verify against real repos, see below).

### The load-bearing gap this spike must surface, not solve — and it is shared with 11.1 and 12.1

**No adapter registry exists yet.** `SiteGenerator` holds a single hardcoded field — `private readonly BmadArtifactAdapter _adapter = new();` [src/SpecScribe/SiteGenerator.cs:51] — with a comment stating plainly that *"the adapter registry that selects among `IArtifactAdapter` implementations arrives with Stories 4.3+."* Those stories are exactly the ones relocated into Epics 11–15, so **the registry has no owner today.** `ARCHITECTURE-SPINE.md` explicitly leaves this open: *"Exact adapter loading mechanics... are implementation seeds"* [`_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md:101`].

**Both Story 11.1 and Story 12.1 already raise this exact same gap** (11.1's Task 5, 12.1's Task 6). Do not independently re-derive a competing registry design or propose a *third* ADR for it. Instead: confirm the gap still exists, and record that it is a **shared prerequisite across all five spikes** — whichever baseline-coverage story lands first (11.2, 12.2, or 14.2) must close it ONCE for all frameworks, not per-framework. If either sibling spike is `done` by the time this one is reviewed, read its Completion Notes and defer to its registry conclusion rather than writing a fresh one. Per this project's ADR-creation-trigger discipline ([[adr-creation-trigger-gap-epic-10-retro]] memory), if a genuine architecture fork is found, an ADR is warranted — but it should be **one** registry ADR shared across Epics 11–15, not one-per-spike.

### The "host-neutral" models are less framework-neutral than they sound

Read these before assuming any Squad artifact maps cleanly:

- **`EpicsModel`/`EpicInfo`/`StoryInfo`** [src/SpecScribe/EpicsModel.cs] bake in BMad vocabulary directly into what the contract calls "host-neutral": `EpicStatus { Drafted, Pending }`, `EpicSection { VerticalSlice, FurtherDevelopment }` (a BMad epics.md convention), and `StoryInfo.Id` is hard-typed as BMad's `"N.M"` two-level numbering with a resolved `ArtifactOutputPath`/`ArtifactSourcePath` pointing at a discrete per-story markdown file. Squad's own docs describe no epic/story/feature hierarchy at all — its structure is agent-team-centric (roster, charters, routing), not work-item-centric. Confirm this null result against the real repo rather than assuming it by pattern-matching Spec Kit's flat-feature-folder shape (which at least HAD a feature hierarchy, just not a two-level one).
- **`RequirementsModel`/`RequirementInfo`** [src/SpecScribe/RequirementsModel.cs:1-73] is entirely BMad's `FR`/`NFR`/`UX-DR` numbered-requirements-with-a-Coverage-Map convention, parsed from epics.md's "Requirements Inventory" section. Squad has **no requirement-contract file found** in the docs surveyed at create-story (unlike GSD's `REQUIREMENTS.md`) — expect `Requirements = null` always, but confirm against a real repo rather than the docs alone.
- **`SprintStatus`** [src/SpecScribe/SprintStatus.cs] is literally `sprint-status.yaml`-shaped (a `development_status` map + `action_items`). Squad ships `.squad/ceremonies.md` — described only as "sprint ceremonies config" with **no confirmed format** at create-story. This is the one genuinely open question: is it closer to a `development_status`-style map (→ partially-mappable), or free-prose ceremony configuration with no per-item status (→ null or a lesser substitute)? Do not guess; fetch the real file.
- **`RetroModel`** [src/SpecScribe/RetroModel.cs] is BMad's `epic-N-retro-*.md` convention. Squad has no dedicated retrospective file — the closest loose candidates are `.squad/log/` (session archive) and per-agent `history.md` (accumulated project knowledge), neither of which is a retrospective in the BMad sense (a structured post-epic review). Expect `Retros = []`, but state explicitly why the loose candidates don't qualify rather than silently omitting them from the coverage map.

**Net implication to verify, not assume:** Squad coverage will likely land almost entirely on framework-extra/non-goal territory (team roster, agent charters, decisions log, skills/identity/log) rather than the `Epics`/`Requirements`/`Sprint`/`Retros` families that BMad, Spec Kit, and GSD all substantially fill. This is a genuinely different outcome shape from both prior spikes — do not force a `Sprint`/`Epics` fit just to mirror 11.1/12.1's table structure.

### Squad's real shape — freshly gathered 2026-07-20 via web research, treat as a starting hypothesis to confirm against real repos

Live-checked 2026-07-20 via `github.com/bradygaster/squad` (README + GitHub Blog / InfoWorld coverage) — **web-fetched summaries, not a cloned repo or `squad init` output; the AC's "representative Squad repositories" language exists precisely because this must be reconfirmed against the real thing:**

| Squad concept | Path / shape (as described in Squad's own docs) | Closest `ArtifactBundle` candidate | Notes / tier hypothesis |
|---|---|---|---|
| Install / state marker | `.squad/` at repo root, created by `squad init` (idempotent) or `squad build` (from `squad.config.ts`) | `AppliesTo` self-selection signal (mirrors `_bmad/`, `.specify/`, `.gsd/`) | Confirm exact marker + idempotency behavior |
| Team roster | `.squad/team.md` — lists active agents | No BMad analog | framework-extra / non-goal candidate |
| Routing rules | `.squad/routing.md` — "who handles what" | No BMad analog | framework-extra / non-goal candidate |
| Decision log | `.squad/decisions.md` — every agent decision, team-visible | ADR side-channel candidate (parallels Spec Kit's `constitution.md`, GSD's `DECISIONS.md`) | classify explicitly, see below |
| Ceremony config | `.squad/ceremonies.md` — "sprint ceremonies config," **format unconfirmed** | `SprintStatus` candidate — **the one open question, verify format** | partially-mappable at best, confirm shape first |
| Agent charter | `.squad/agents/{name}/charter.md` — identity/expertise/voice | No BMad analog | framework-extra / non-goal candidate |
| Agent history | `.squad/agents/{name}/history.md` — accumulated project knowledge per agent | Loose `RetroModel`/knowledge analog? Verify — likely not a retro | probably non-goal, confirm |
| Casting | `.squad/casting/{policy,registry,history}.json` — agent-naming setup | No BMad analog; also JSON not markdown | out of scope (non-markdown), confirm |
| Skills | `.squad/skills/` — "compressed work learnings" | No BMad analog | framework-extra / non-goal candidate |
| Identity | `.squad/identity/{now,wisdom}.md` — team focus / reusable patterns | No BMad analog | framework-extra / non-goal candidate |
| Session log | `.squad/log/` — searchable session archive | No BMad analog | framework-extra / non-goal candidate |
| Numbered FR/NFR requirements | **None found** | `Requirements` stays null — not a gap, confirm | — |
| Epic/story/feature hierarchy | **None found** — no equivalent of BMad epics, Spec Kit feature folders, or GSD milestones/slices | `Epics`/`StoryArtifactsById` likely stay null/empty — the central, surprising finding of this spike | confirm this null result deliberately, don't assume it silently |

**Do not treat this table as ground truth.** Fetch/clone the real `bradygaster/squad` repo (or a `squad init`-scaffolded sample project) and confirm every row — especially `ceremonies.md`'s actual format, since it is the only row with a live chance of feeding `SprintStatus`, and every other row's "no BMad analog" conclusion, since getting the epics/requirements null result wrong would be this spike's most consequential mistake.

### No existing Squad surface anywhere in the portal — unlike every other spike so far

Unlike Spec Kit, GSD, GSD-Pi, and Superpowers (all of which already have a `Supported: false` roster entry, a routed output page, and a "🧭 Planned" README row before their spike even started), **Squad has zero pre-existing placeholder**:

- **`AboutSddTemplater.cs:10-18`**'s `Frameworks` roster currently lists only `bmad`, `gds`, `speckit`, `gsd`, `gsd-pi`, `superpowers` — no `squad` entry.
- **`SiteNav.cs`** has no `AboutSddSquadOutputPath` constant — no page route exists.
- **`README.md`**'s "Supported frameworks" table has no Squad row at all (verified via repo-wide grep at create-story).

This spike does **not** add any of these — that remains out of scope (see non-goals below) — but Task 5 must record it as an explicit finding: **Story 14.2 (or a small preceding seam-extension) must add the roster tuple, the `SiteNav` output path, and the README row before Squad can render anything**, unlike 11.2/12.2 which only need to fill in already-routed placeholder pages.

**ADRs are a side-channel, not part of `ArtifactBundle` at all.** There is no dedicated ADR parser class and no `ArtifactBundle` field for them — `docs/adrs/*.md` are hand-authored and read via a separate, always-optional `ForgeOptions.AdrSourceRoot` path, entirely outside the `IArtifactAdapter` contract. **This matters directly for Squad's `.squad/decisions.md`** (a team decision log with no obvious BMad analog): classify it explicitly as (a) belonging in the ADR side-channel, (b) a new `ArtifactBundle` field/model, or (c) out of scope for now — don't leave it unclassified, mirroring how 11.1 classified `constitution.md` and 12.1 classified `DECISIONS.md`.

**Coverage-map precedent.** Story 11.1's and Story 12.1's Completion Notes are the first two coverage maps actually written; this spike's should be the third and adopt a compatible shape so Epic 13 (SpecFlow) and Epic 15 (Superpowers) can reuse it. **If 11.1 or 12.1 is `done` before this spike is reviewed, read their Completion Notes and match structure** (especially the registry-gap wording and the decisions/constitution-file classification pattern).

### Deliberate non-goals (seed list — spike may extend with rationale)

- **Building `SquadArtifactAdapter`** or any parser — that's 14.2.
- **Designing the adapter registry** — name the shared gap (Task 6), don't design or implement it here; defer to 11.1/12.1's conclusion if already reached.
- **Extending `ArtifactBundle`/`EpicsModel`/etc. with new fields** — the spike records *candidate* projection extensions (AC #2); it does not land them.
- **Adding the missing `AboutSddTemplater` roster entry, `SiteNav` output path, or README row** — name it as a Task 5 finding for 14.2, don't add it here.
- **Proposing a second/third registry ADR** — if an ADR is warranted, it is ONE shared registry ADR coordinated across Epics 11–15, not per-framework.
- **A new authoring schema** for Squad content — SpecScribe reads Squad's existing `.squad/` conventions as-is, whether produced by `squad init` or `squad build`.
- **Parsing `.squad/casting/*.json`** — these are JSON, not markdown; out of scope for a markdown-reading generator regardless of classification tier.

## Tasks / Subtasks

- [ ] **Task 1 — Confirm the contract shapes against live code (AC: #1)**
  - [ ] Read `IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`, `EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs` in full (paths above) — do not rely solely on this story's summary tables; they are a starting point, not a substitute for reading the code.
  - [ ] Confirm (or correct) this story's claim that no adapter-selection registry exists (`SiteGenerator.cs:51`), and check whether Story 11.1 and/or 12.1 have already landed/recommended a registry shape (read their Completion Notes if `done`).
  - [ ] Confirm the current `AboutSddTemplater.Frameworks` roster, `SiteNav` constants, and `README.md` "Supported frameworks" table genuinely have no Squad entry (re-grep; this story's claim was made at create-story and could go stale).

- [ ] **Task 2 — Obtain and inspect a representative current-version Squad repository (AC: #1, #2)**
  - [ ] Fetch/clone `github.com/bradygaster/squad` (or run `squad init` into a scratch directory) to confirm the exact `.squad/` layout — the table above was built from web-fetched documentation summaries, not a repo inspection.
  - [ ] **Resolve `.squad/ceremonies.md`'s actual format** — this is the single most consequential open question in this spike; it decides whether `Sprint` is partially-mappable or null.
  - [ ] Confirm whether `squad build` (from `squad.config.ts`) produces byte-identical `.squad/*.md` output to `squad init`, or a divergent shape SpecScribe would need to handle differently.
  - [ ] Confirm the `.squad/agents/{name}/` per-agent structure (charter.md + history.md) and whether agent names are stable identifiers or user-chosen (affects any future per-agent page modeling, out of scope here but useful context for 14.2).

- [ ] **Task 3 — Classify every discovered artifact type (AC: #1)**
  - [ ] For each Squad artifact type (`.squad/` marker, `team.md`, `routing.md`, `decisions.md`, `ceremonies.md`, `agents/{name}/charter.md`, `agents/{name}/history.md`, `casting/*.json`, `skills/`, `identity/{now,wisdom}.md`, `log/`), classify as **mappable** (name the exact target: `ArtifactBundle` field + model type/record), **partially-mappable** (name what maps and what doesn't), or **unsupported** (name why).
  - [ ] Resolve explicitly, confirmed against the real repo: is `Epics` null (no epic/story/feature hierarchy found), is `Requirements` null (no numbered-requirements file found), is `Sprint` null or partially-mappable via `ceremonies.md`, is `Retros` empty (state explicitly why `history.md`/`log/` don't qualify as retrospectives) — do not silently copy 11.1's or 12.1's null/fill pattern without Squad-specific evidence.
  - [ ] Classify `.squad/decisions.md` explicitly: ADR side-channel, new `ArtifactBundle` field, or out of scope — mirror 11.1's `constitution.md` and 12.1's `DECISIONS.md` classification precedent.

- [ ] **Task 4 — Framework-extra data and deliberately-unsupported conventions (AC: #2)**
  - [ ] For any Squad convention richer than the shared model (team roster, agent charters/history, routing rules, casting policy, skills, identity, session log), record it as either a candidate projection extension (name what it would add) or an explicit non-goal (with rationale) — expect most of these to land as non-goals given Squad's team-coordination-not-planning-doc nature.
  - [ ] For anything SpecScribe will deliberately not support (e.g. `.squad/casting/*.json` being non-markdown), name the exact `AdapterDiagnosticCategory` (`Unsupported`/`Malformed`/`Skipped`/`Error`/`Informational`) its non-fatal notice would use and draft the notice's wording, mirroring `BmadArtifactAdapter`'s existing diagnostic messages [BmadArtifactAdapter.cs:170-188, 219-224, 262-276] for tone/specificity.

- [ ] **Task 5 — Record the missing-placeholder finding (AC: #1, #2)**
  - [ ] Confirm (Task 1) that no `AboutSddTemplater` roster entry, `SiteNav` output path, or README row exists for Squad, and state plainly in Completion Notes that Story 14.2 (or a small preceding seam-extension) must add all three before any Squad content can render — unlike 11.2/12.2, which only fill in already-routed placeholder pages.

- [ ] **Task 6 — Name the adapter-registry gap as a shared finding, coordinated with 11.1 and 12.1 (AC: #1, #2)**
  - [ ] Confirm the registry-gap claim against `SiteGenerator.cs`. State plainly that Story 14.2 cannot wire in a second adapter without SOME selection mechanism, that this gap is **shared across all five spikes** (not Squad-specific), and that whichever coverage story lands first closes it once for all frameworks.
  - [ ] Recommend (not build) a minimal registry shape, and defer to 11.1's/12.1's conclusion/ADR if either already exists. Do NOT propose a third, Squad-specific registry ADR.

- [ ] **Task 7 — Record findings; no production code (AC: #1, #2)**
  - [ ] Write the coverage map (artifact-type × classification × target projection table + the epics/requirements-null finding + decisions.md classification + non-goals + missing-placeholder finding + shared registry-gap finding + 14.2 recommendation) into this story's **Completion Notes**, mirroring Story 11.1's / 12.1's convention.
  - [ ] Do **not** land production `src/**`/`tests/**` changes from this story. No new ADR unless Task 6 concludes a genuine fork exists AND neither 11.1 nor 12.1 has already covered it — coordinate to keep it a single shared registry ADR.

### Review Findings

_(populated during code-review)_

## Dev Notes

### Spike constraints (load-bearing)

- **Tracing + live repo inspection, not code.** Evidence comes from reading `src/SpecScribe/*.cs` and reading/fetching the real Squad repo — not from writing a prototype adapter. If you catch yourself scaffolding `SquadArtifactAdapter.cs`, stop; that's 14.2.
- **No new authoring schema.** SpecScribe never asks Squad users to add SpecScribe-specific files — coverage must derive from Squad's own existing `.squad/` conventions, whether produced by `squad init` or `squad build`.
- **Verify, don't assume — especially the null results.** This story's "Squad's real shape" table was built from web-fetched documentation summaries during create-story, not a cloned repo. The AC's "representative Squad repositories" language exists precisely because the epics/requirements-null conclusion and `ceremonies.md`'s format are both unconfirmed hypotheses, not settled fact.
- **NFR8.** Genuinely-absent artifact families (likely `Epics`, `Requirements`, `Retros`) are honest absence, not gaps to fill. Don't recommend inventing an epic/story or FR-numbering convention for Squad — that would violate the framework-neutral principle. But do confirm each absence with evidence rather than pattern-matching Spec Kit's or GSD's tables.
- **Don't add the missing roster/nav/README placeholder here** — name it as a Task 5 finding for 14.2 to close, the same way 11.1/12.1 didn't build their registry recommendations, only named them.

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md:34-40] — one shared projection/rendering core; any future Squad adapter only translates into `ArtifactBundle`, never reinterprets shared rendering.
- **AD-2** [ARCHITECTURE-SPINE.md:42-48] — the adapter boundary is source → normalized records; this spike maps Squad source shapes onto that exact contract, nothing downstream.
- **AD-4** [ARCHITECTURE-SPINE.md:58-64] — any future Squad-specific insight enrichment must stay additive/non-blocking, same as BMad's git-pulse/ADR-coverage providers.
- **NFR8** [epics.md:99]: *"Insight surfaces and guidance affordances... are framework-agnostic in shared rendering: framework-specific content flows through the adapter contract, and surfaces degrade gracefully — absent, not broken or misleadingly empty — when a methodology lacks the corresponding artifact."* This is the literal rule behind "Epics=null/Requirements=null is correct, not a gap," if that is what Task 3 confirms.
- **Seed, Not Invariant** [ARCHITECTURE-SPINE.md:98-102]: exact adapter-loading mechanics and package/namespace split are explicitly open — do not let this spike commit to `src/SpecScribe.Adapters.Squad` as a real package (the project is still single-namespace, single-project — [[epic-4-adapter-contract-scope]] memory: "no package split").

### Anti-patterns to prevent

- Assuming Squad has an epic/story/feature hierarchy just because Spec Kit and GSD did — Squad's own docs describe a team-coordination model instead; confirm the null result with evidence.
- Guessing `.squad/ceremonies.md`'s format instead of fetching the real file — it is the only candidate feeding `Sprint`, and getting its tier wrong (assuming a clean map vs. free prose) misleads 14.2.
- Silently omitting the missing-placeholder finding (Task 5) — unlike 11.2/12.2, Story 14.2 starts with zero existing routed page, roster entry, or README row.
- Proposing a third, Squad-specific adapter-registry ADR instead of coordinating one shared registry decision with 11.1/12.1.
- Silently committing to a package/namespace split (`SpecScribe.Adapters.Squad`) — aspirational sketch, not current architecture.
- Treating this story's own hypothesis table (built from web-fetched summaries) as verified fact instead of confirming it against the real `bradygaster/squad` repo.
- Attempting to parse `.squad/casting/*.json` — non-markdown, out of scope regardless of classification tier.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/14-1-squad-integration-spike.md`
- Sprint key: `14-1-squad-integration-spike`
- Downstream story key (not created by this spike): `14-2-squad-baseline-adapter-coverage`
- No `src/`/`tests/` touches expected.
- No ADR file expected unless Task 6 concludes a genuine architecture fork (registry design) not already covered by 11.1/12.1 — if so, it is ONE shared registry ADR (`docs/adrs/`, next number, indexed in `docs/adrs/README.md`), coordinated across all three spikes, escalated rather than decided silently.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:2475-2519`] — Epic 14 intro, FR17 line, Story 14.1 (verbatim ACs quoted above) + Story 14.2 (downstream coverage story, its ACs quoted for scope-boundary context).
- [Source: `_bmad-output/planning-artifacts/epics.md:58,179`] — FR17 statement (SpecFlow/Squad/Superpowers baseline) and Epic 14 FR-coverage line.
- [Source: `_bmad-output/planning-artifacts/epics.md:99`] — NFR8 exact wording.
- [Source: `_bmad-output/implementation-artifacts/spec-epic-4-split-per-framework-epics.md`] — why Epics 11-15 exist, the fixed framework→epic mapping (14 = Squad / FR17), the "X.1 spike, X.2 coverage" pattern, the spike AC template.
- [Source: `_bmad-output/implementation-artifacts/11-1-spec-kit-integration-spike.md`] — the original sibling spike; mirror its structure and Completion-Notes-as-deliverable convention.
- [Source: `_bmad-output/implementation-artifacts/12-1-gsd-and-gsd-pi-integration-spike.md`] — the second sibling spike; shows how far a spike's findings can diverge from 11.1's when the framework's shape genuinely differs (this spike diverges further still).
- [Source: `src/SpecScribe/IArtifactAdapter.cs`, `ArtifactBundle.cs`, `AdapterDiagnostic.cs`, `BmadArtifactAdapter.cs`] — the contract + its one reference implementation (line anchors verified against current main at create-story).
- [Source: `src/SpecScribe/EpicsModel.cs`, `RequirementsModel.cs`, `RetroModel.cs`, `SprintStatus.cs`] — the "host-neutral" model shapes, BMad-specific vocabulary baked in.
- [Source: `src/SpecScribe/SiteGenerator.cs:51`] — the hardcoded single-adapter field; the shared registry gap.
- [Source: `src/SpecScribe/AboutSddTemplater.cs:10-18`] — the framework roster (`bmad`/`gds`/`speckit`/`gsd`/`gsd-pi`/`superpowers` only — **no `squad` entry**, confirmed by grep at create-story), the six-noun support matrix, and the "Coming soon" placeholder body pattern any future Squad page would reuse.
- [Source: `README.md`] — "Supported frameworks" table has no Squad row at all (grep-confirmed at create-story), unlike Spec Kit/GSD/GSD-Pi which are listed "🧭 Planned."
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` AD-1/AD-2/AD-4, Seed-not-invariant] — the invariants this spike must respect and the open seeds it must not over-commit.
- **Memory:** [[epic-4-adapter-contract-scope]] (Epic 4 foundation-only, no package split, spike-led per-framework pattern), [[adr-creation-trigger-gap-epic-10-retro]] (propose an ADR for architecture-shaped decisions — but ONE shared registry ADR, coordinated across 11.1/12.1/14.1, not per-spike).
- [Web: `github.com/bradygaster/squad` README + GitHub Blog ("How Squad runs coordinated AI agents inside your repository") + InfoWorld coverage, fetched live 2026-07-20] — the `.squad/` directory layout (team.md, routing.md, decisions.md, ceremonies.md, casting/, agents/{name}/{charter,history}.md, skills/, identity/, log/), `squad init`/`squad build`/`squad upgrade` commands. **Treat as a hypothesis built from documentation summaries, not a repo inspection — the dev must fetch/clone the real repo to confirm every row, especially `ceremonies.md`'s format.**

### Git intelligence summary

No Squad code, docs, or prior exploration exists anywhere in this repo beyond the epics.md/sprint-status.yaml scaffolding (confirmed via grep across `src/`, `docs/`, `README.md`) — this spike starts from a genuinely clean slate on the Squad side, and unlike Spec Kit/GSD/GSD-Pi/Superpowers, Squad also has no pre-existing placeholder page, roster entry, or README row on the SpecScribe side either. Stories 11.1 (Spec Kit) and 12.1 (GSD/GSD-Pi) are the immediately-preceding sibling spikes (`ready-for-dev`) and the closest structural templates. Recent commits (Epic 10 cleanup, retro work, 11.1/12.1 create-story) are unrelated to this story's implementation scope.

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-20 — Story 14.1 drafted (create-story). Ultimate context engine analysis completed — comprehensive developer guide created. Spike-only: coverage map + Squad's genuinely-different (team-coordination-not-planning-doc) shape finding + missing-placeholder finding + 14.2 scope recommendation; no production code. Third story of the five per-framework spike-led epics (11–15).
