---
baseline_commit: 7ff3b13921b0c00c885f3b37719f18a9478ea9c3
---

# Story 4.9: Multi-Framework Coexistence Strategy Spike

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer whose repository uses more than one spec-driven framework at once,
I want a decided strategy for how SpecScribe behaves when several adapters recognize the same tree,
so that a mixed repository gets a coherent portal instead of an arbitrary winner or a silently dropped half.

## ⛔ Read first — this is a decision spike, and half the ground is already shipped

**This story ships no production code.** Its deliverables are one ADR and one spike report. Follow Story 25.3's
discipline: prove at close that `src/`, `tests/`, `web/` and `extension/` are untouched (§ Task 8 there, § Task 7
here).

**Epic 4's retrospective is `done` and stays `done`.** Story 4.9 is a *post-retrospective amendment* to Epic 4,
the same pattern as Stories 7.9 and 8.9. It does not reopen 4.1/4.2/4.8 and it does not invalidate that
retrospective's findings.

**The question you are answering is NOT "which adapter wins".** That is answered, shipped and merged
(`be65cf1`). Story 12.2 landed a working `AdapterRegistry`, framework-neutral source-root discovery, and
[ADR 0038](../../docs/adrs/0038-framework-adapter-selection-and-neutral-source-root-discovery.md) — and a real
GSD Core repository (`C:/dev/CORA`) now generates **160 pages, 0 errors**. What is *not* answered is whether
"one owner per single-valued family, everyone else diagnosed" is the **right** policy, and whether the single
`SourceRoot` should stay single. That is this story.

**You are amending a live decision, not filling a blank page.** AC #3 requires the ADR to name *which of Story
12.2's minimal behaviors it supersedes*. Those behaviors are enumerated in R1 below with file and symbol
anchors. An ADR that supersedes nothing is a legitimate outcome — "the minimal answer is the right answer,
here is why, and here is what would change our mind" — but it must say so explicitly rather than by omission.

---

## 🔴 Reconciliations against shipped code — verified 2026-08-06 at `7ff3b13`, honor these

Line anchors were confirmed at this baseline; a concurrent session may move them. **Confirm by symbol, never by
line** (CLAUDE.md § Concurrent work).

### R1 — The eleven live behaviors AC #3 must rule on, one by one

These are the shipped surface of "Story 12.2's minimal behaviors". Your ADR must state, for **each**, whether it
stands, is refined, or is superseded. Source: `src/SpecScribe/AdapterRegistry.cs`, ADR 0038 §§1–4.

| # | Shipped behavior | Anchor |
|---|---|---|
| B1 | `Select` runs `AppliesTo` on the whole ordered roster and returns **every** match, not the first | `AdapterRegistry.Select` |
| B2 | No match at all → the `BmadArtifactAdapter` fallback alone, preserving pre-registry behavior | `AdapterRegistry.Select` final line |
| B3 | Roster order is framework-specific markers first, `BmadArtifactAdapter` **last** | `AdapterRegistry.Default` |
| B4 | A single matching adapter returns its bundle **verbatim, same instance** — identity merge, and **no cross-adapter diagnostic fires** | `Ingest`, the `bundles.Count == 1` early return |
| B5 | The **epics family** (`Epics` + `Requirements` + `EpicsSourceFullPath`) is claimed **together** by the first adapter that FOUND an epics source — not field-by-field | `Ingest`, the `epicsOwner` block |
| B6 | `Sprint` — first non-null wins; the loser gets a `Skipped` diagnostic | `Ingest` / `Dropped` |
| B7 | `Module` — first with a **real detected identity** wins (`BmadModule.Unknown` is the "no identity" signal); ties resolve to the first | `HasModuleIdentity` |
| B8 | `Retros` and `Diagnostics` concatenate in adapter order; `ConsumedSourceRelatives` unions | `Ingest` |
| B9 | `StoryArtifactsById` unions; a duplicate id keeps the earlier adapter's artifact and emits a `Skipped` diagnostic, never a silent overwrite | `Ingest`, the `TryAdd` block |
| B10 | A multi-adapter run emits **one** `Informational` notice naming who matched and who supplied each family, plus **a second** naming any framework marker present at the repo root that did not become the source root | `DescribeMatchSet`, `AppendNonPrimaryMarkerNotice` |
| B11 | The watch-mode scoped re-ingest merges by the **same** epics-ownership rule, so a watch pass and a full build can never disagree about the epics owner | `AdapterRegistry.IngestEpics`; `IArtifactAdapter.IngestEpics` |

**⚠️ B5 makes AC #1's field list slightly out of date, and you must say so.** The AC names
`Epics`, `Sprint`, `Requirements`, `Module`, `EpicsSourceFullPath` as five independently-resolved single-valued
fields. In shipped code three of them are **one unit**: requirements roll up from the same file as the epics, so
carrying adapter A's source path beside adapter B's parsed model is incoherent, not merely odd (ADR 0038 §2).
Answer AC #1 against the **three** real resolution units — epics family, sprint, module — and record the
correction rather than answering a question the code no longer asks.

### R2 — `SourceRoot` is the blocker, and today's cost is measurable, not theoretical

`ForgeOptions.SourceRoot` is a single `string` and it anchors **two** things:

- **Discovery** — `SiteGenerator.EnumerateSourceFiles` is one `Directory.EnumerateFiles(_options.SourceRoot, "*.md", AllDirectories)` walk.
- **Every source-relative path** — `SiteGenerator.ToSourceRelative` is `Path.GetRelativePath(_options.SourceRoot, fullPath)`, and both adapters carry their own identical private copy (`BmadArtifactAdapter.ToSourceRelative`, `GsdCoreArtifactAdapter.ToSourceRelative`).

A path outside that root relativizes to `..\…`, which `PathUtil.EscapesRepoRoot` exists to reject.

**Measured on `C:/dev/CORA` at create-story (2026-08-06):** with `SourceRoot = .planning`, these five markdown
documents plus one HTML file live in `_bmad-output/planning-artifacts/` and **do not render as pages**:

```
architecture.md
prd.md
product-brief-CORA-knowledge-graph.md
product-brief-CORA-knowledge-graph-distillate.md
ux-design-specification.md
ux-design-directions.html          ← would not be caught by the *.md scan in any case (see ADR 0021)
```

They are not lost from the *bundle* — BMad's adapter still supplies the module identity — they are lost as
**documents**. That is the whole of the accepted cost today, it is six files on the reference repository, and
`AppendNonPrimaryMarkerNotice` is what tells the reader about it. Quote the real number in the report; do not
describe this loss in the abstract.

### R3 — SpecScribe ALREADY runs a two-root system. Price your options against it, not against a blank page

This is the most useful thing in this reconciliation list and it is easy to miss: **`AdrSourceRoot` is a second
source root and has been since Epic 1.** Every mechanism a multi-rooted design would need already exists and is
in production:

| Concern | How the ADR root already solves it |
|---|---|
| Separate discovery walk | `SiteGenerator.EnumerateAdrFiles`, rooted at `_options.AdrSourceRoot` |
| Collision-free output paths | output is prefixed — `ForgeOptions.AdrOutputSubdir = "adrs"`, joined as `adrs/{relativeToRoot}` |
| Relativization against the right root | `Path.GetRelativePath(_options.AdrSourceRoot, file)`, kept separate from `ToSourceRelative` |
| Diagnostics that know which root they anchor to | `DiagnosticAnchorRoot { None, Source, Adr, Repo }` (`DiagnosticsTemplater.cs:25`) |
| Resolving a notice back to a real file | `Commands.cs` — `DiagnosticAnchorRoot.Adr` combines with `AdrSourceRoot`, and `StripAdrOutputPrefix` removes the output prefix first |
| Watch coverage | `FileWatcherService.cs:60–74` creates a file watcher **and** a directory watcher for **each** of `SourceRoot` and `AdrSourceRoot` |
| CLI + persistence | `--adrs`, `SavedSettings.Adrs`, its own `--show-config` provenance row |

So "multi-rooted source discovery" is **not** an unprecedented architecture in this codebase — it is
generalizing a two-root special case into an n-root one. That does not make it cheap (the ADR root is
hardcoded everywhere the source root is, which is the point: n-rooting means those pairs become loops). But an
option paper that prices multi-root as a leap into the unknown is **wrong on the facts**, and a reviewer will
say so.

### R4 — `DiagnosticAnchorRoot.Source` becomes ambiguous the moment there are two source roots

`Commands.cs` resolves a notice to a real on-disk file for the VS Code Problems panel by joining
`resolved.SourceRoot` with `notice.SourcePath`. With two source roots that join is **ambiguous**: it silently
resolves against the wrong root, or to nothing. Any proposal that adds a root must price a change to the anchor
enum and to that resolution — it is a contract shared with the extension (Story 6.12,
[ADR 0037](../../docs/adrs/0037-extension-authors-settings-through-the-core.md)), not an internal detail. The
`Adr` arm is the worked precedent for what the fix looks like.

### R5 — ADR 0017 makes any output-path scheme a PUBLIC change

[ADR 0017](../../docs/adrs/0017-projection-routes-mirror-ir-paths.md) decided that a projected page's route **is**
the IR page's `outputRelativePath`, verbatim. Three consequences bind you:

- **A source document's output path is its source-relative path with the extension swapped** —
  `PathUtil.ToOutputRelative` is literally `Path.ChangeExtension(sourceRelativePath, ".html")`. So any per-root
  prefixing scheme moves URLs, and ADR 0017 §Consequences already states that a path rename is a **public**
  change, not an internal one.
- **No href inside IR content is ever rewritten** (ADR 0017 §Decision 2). Relative links inside carried prose are
  written against each page's own depth. A scheme that changes a page's depth changes what its existing relative
  hrefs resolve to, and there is no rewriter to compensate — by design.
- **Nitro will not write a route whose path contains the substring `..`** (§Decision 5). Any scheme that would
  express a second root as an up-level segment is disqualified outright.

**Collision, stated honestly:** CORA does **not** collide today — its two roots share no markdown basename, and
the paths differ anyway (`planning-artifacts/prd.md` vs top-level `PROJECT.md`). The risk is **structural, not
observed**: two roots may each hold a top-level `README.md`, and `README.md` is additionally special-cased into
`index.html`. Say "structural risk, not present in the reference repo" — do not manufacture a collision that
isn't there, and do not conclude from its absence that the scheme is safe.

### R6 — Watch mode (AD-5 / ADR 0027) costs one watcher pair per root, and the topology sentinel is single-root

`FileWatcherService` creates a filtered file watcher **and** an unfiltered `NotifyFilters.DirectoryName` watcher
per root. Story 5.3 added the directory watcher with a **single** coalescing sentinel key `"<topology>"` and an
`IsUnderOutputRoot` guard (a nested `--output` would otherwise re-arm the topology timer forever). Both assume
one source root. AD-5 says watch behaviour must not regress and
[ADR 0027](../../docs/adrs/0027-watch-rebuild-scope-is-one-classifier-and-topology-escalates.md) defines "safe"
as *proven byte-identical to a full rebuild*. Price: N watcher pairs, a per-root sentinel (or a documented
argument that one shared sentinel is still correct), and the `IsUnderOutputRoot` guard applied per root.

### R7 — Settings persistence is single-valued, so multi-root amends ADR 0003/0014

`--source` is one string; `SavedSettings.Source` is one string; `SettingsResolver` reports one `source_root`
field with provenance to `--show-config` (Story 5.2's one-line-per-field contract). A second root means either a
list-shaped setting or a second scalar — either way it is an on-disk shape change to `.specscribe/config.json`,
which [ADR 0014](../../docs/adrs/0014-specscribe-settings-folder-format.md) governs (extending ADR 0003). Story
5.5's review found that a *generic* enum converter fails **whole-document** deserialization on one unrecognized
token, silently discarding every other saved field; a shape change here inherits that blast radius. Name the
amendment; do not let the ADR imply the setting is free.

### R8 — The gates cannot see what you would be changing, and one of them actively lies here

- **`npm run check:parity` cannot see a C#-side change.** Its corpus IR is frozen, so a change to region
  composition renders from the *pinned* input and the gate stays green (verified 2026-08-01 — a change that
  removed an element from the shared nav on every page left all 24 routes byte-identical).
- **`check:ir-content` cannot see markup only a non-BMad repo produces.** This is Story 12.2 §F1, and it was
  **measured, not theorised**: with the stylesheet edit in place and the documented regeneration order followed
  exactly, all five `.milestone-band*` rules were pruned and the gate stayed **GREEN**. The extraction corpus is
  this repository's own IR, and this repository is a BMad project. The seam is `CONDITIONAL_CLASSES` in
  `web/scripts/ir-content-lib.mjs`, pinned by `web/test/ir-content-harvest.test.mjs`.

You ship no rendering, so neither gate should move — but **any surface your strategy proposes for a future story
inherits both blind spots**, and the ADR should say so where it recommends one. Per
[ADR 0033](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md), a *new* gate must localize
failure to a named artifact, be scoped so a sibling story cannot turn it red, and be proven deterministic before
pinning. Prefer proposing no new gate.

### R9 — Two known issues you will trip over. Neither is yours; do not chase either

- **`FileWatcherServiceTests` flakes under load** (Story 12.2 §F7: measured across six full-suite runs, three
  green at 2963/2963 and three failing with 1–2 failures, always that one class and always a *different* test;
  3/3 in isolation). Pre-existing. If you run the suite, expect it.
- **The portal renders unstyled from `file://` in Chromium** — Nuxt emits `<link … crossorigin>` and Chromium
  refuses `crossorigin` stylesheets over `file://` (Story 12.2 §F2). Pre-existing, NFR-3-relevant, reported not
  patched. If you inspect a generated site, serve it over HTTP.

### R10 — One stale comment to record as a handoff, not to silently fix

`ForgeOptions.Resolve`'s walk-up comment says a repo carrying several markers at the same level "resolves by
`SourceDirNames` order **(BMad first)**". The array is `.planning → .gsd → .specify → _bmad-output`, so BMad
probes **last** — the parenthetical contradicts both the array immediately above it and ADR 0038 §3. It is
Story 12.2's hunk and Story 12.2 is `review`. Per CLAUDE.md § hunk attribution, **record the handoff in your
spike report** so it cannot fall between the two reviews; do not adopt the hunk.

### R11 — 0039 is the next free ADR number. 0019 is claimed-but-unwritten. Do not take it

`docs/adrs/` runs 0001–0038 with **0019 absent and reserved** — claimed by both Story 18.3 and Story 22.3, and
ADR 0021 records the collision. Take **0039**. Index it in `docs/adrs/README.md` following the existing entry
shape (link — **Status** — date — a substantive parenthetical, not a one-liner). Note while you are there:
**ADR 0037 is missing from that index** (Story 12.2 §F5, left alone deliberately for the same attribution
reason) — report it again if it is still missing; adding it is not yours.

---

## Acceptance Criteria

_Verbatim from [epics.md](../planning-artifacts/epics.md) § Story 4.9 (lines 912–936). The bracketed notes are
create-story refinements against shipped code, not changes to the criteria._

1. **Given** representative repositories that carry more than one framework's markers (the motivating case: BMad
   for planning artifacts plus GSD Core for delivery, as in `C:/dev/CORA`)
   **When** the coexistence question is surveyed against the shared adapter contract
   **Then** a written strategy states, per `ArtifactBundle` family, how competing contributions resolve —
   precedence, merge, or explicit refusal — and what the reader is told about the choice
   **And** the single-valued-field conflict (`Epics`, `Sprint`, `Requirements`, `Module`, `EpicsSourceFullPath`)
   is answered directly rather than deferred to per-framework judgment.

   > _[Answer against the **three** real resolution units in shipped code — epics family (`Epics` +
   > `Requirements` + `EpicsSourceFullPath` as one unit), `Sprint`, `Module` — and record why the AC's
   > five-field list is superseded. See R1.]_
   > _["What the reader is told" has an existing home: the Story 4.8 diagnostics page, fed by the
   > `AdapterDiagnostic` channel. State whether the notice belongs there, on the dashboard, or on the About-SDD
   > framework page — do not leave the surface unnamed.]_

2. **Given** SpecScribe resolves exactly one `SourceRoot`, which anchors both the `*.md` source enumeration and
   every source-relative output path
   **When** two frameworks keep their artifacts in disjoint directories (`_bmad-output/` and `.planning/`)
   **Then** the strategy decides whether source discovery becomes multi-rooted, and if so how output paths stay
   collision-free and stable
   **And** the cost of each option to watch mode (AD-5), the canonical IR's route shape (ADR 0017), and the
   content-drift gates (ADR 0033) is stated, not assumed.

   > _[Cost must also cover the four couplings the AC does not name, each of which is real and each of which
   > R3–R7 anchors: `DiagnosticAnchorRoot` resolution shared with the extension; settings shape (ADR 0003/0014);
   > `PathUtil.EscapesRepoRoot`; and the existing `AdrSourceRoot` precedent that already solves several of these.]_
   > _["Stated, not assumed" means named symbols and counted call sites. A cost expressed as "moderate" fails
   > this AC.]_

3. **Given** the strategy changes a cross-cutting contract
   **When** the spike concludes
   **Then** it lands as one ADR amending the adapter-selection decision rather than as prose in a story file
   **And** it names which of Story 12.2's minimal behaviors it supersedes, so the follow-through is a known,
   bounded change rather than a rediscovery.

   > _[The behaviors are B1–B11 in R1. Rule on each. "Supersedes nothing" is an allowed verdict; silence is not.]_
   > _[One ADR — 0039 — amending ADR 0038. Not a second registry ADR: ADR 0038 §Inherited-by binds Epics
   > 11/12.3/13/14/15 to one registry decision, and this amends that record rather than competing with it.]_

## Tasks / Subtasks

- [ ] **Task 1 — Re-establish ground truth before designing anything (AC: #1, #2)**
  - [ ] Read in full: `src/SpecScribe/AdapterRegistry.cs`, `IArtifactAdapter.cs`, `ArtifactBundle.cs`,
    `AdapterDiagnostic.cs`, `ForgeOptions.cs` (§`SourceDirNames`, §`Resolve`), and
    [ADR 0038](../../docs/adrs/0038-framework-adapter-selection-and-neutral-source-root-discovery.md) end to end.
    Confirm B1–B11 (R1) still describe the code **by symbol**; record any that have moved.
  - [ ] Count the real call sites, do not estimate: `ToSourceRelative` (three separate private copies —
    `SiteGenerator`, `BmadArtifactAdapter`, `GsdCoreArtifactAdapter`), `EnumerateSourceFiles`,
    `_options.SourceRoot` across `src/`, and the `AdrSourceRoot` twin of each. These counts are AC #2's evidence.
  - [ ] Re-inspect `C:/dev/CORA` and re-derive R2's six-file loss. If the repo has changed, say so in the report
    before relying on the number.
  - [ ] Read Story 12.2's Completion Notes §D1–§D5 and §F1–§F7. Several are load-bearing for your cost model and
    §F1 is the reason a proposed surface is more expensive than it looks.

- [ ] **Task 2 — Survey the coexistence shapes that actually occur (AC: #1)**
  - [ ] Enumerate the *kinds* of multi-framework repository, not just CORA's: (a) plan in one, deliver in another
    (CORA); (b) migration in progress, both frameworks holding a full artifact set; (c) vestigial marker — a
    framework installed and abandoned, its directory still present; (d) monorepo with per-package frameworks.
    Each stresses a different family. (c) is the one that makes "every matching adapter runs" actively wrong, and
    it is cheap to construct.
  - [ ] For each shape, state what shipped code does **today** — trace it, do not predict it — and whether the
    result is coherent, degraded-but-honest, or wrong.
  - [ ] Note explicitly that CORA is the **only** real multi-framework repository available; anything else is
    constructed. Do not present a constructed case as observed evidence.

- [ ] **Task 3 — Decide the per-family policy (AC: #1)**
  - [ ] For each of the three resolution units (epics family, `Sprint`, `Module`) and each collection family
    (`Retros`, `StoryArtifactsById`, `ConsumedSourceRelatives`, `Diagnostics`), choose **precedence**, **merge**,
    or **explicit refusal**, and give the reason in one paragraph.
  - [ ] Price **merge** honestly where you propose it. A merged epics index needs per-item framework attribution
    to be readable at all — which is a field on `EpicInfo`/`StoryInfo`, i.e. a shared-model change **and** an IR
    schema change (ADR 0008 territory), not a rendering tweak.
  - [ ] Price **explicit refusal** too. It is the cheapest and most honest option and it must be on the table:
    generate for the primary framework, refuse the second, say so loudly. Its cost is coverage, and the reader
    surface is already built.
  - [ ] Decide what the reader is told and **on which surface** — diagnostics page (Story 4.8), dashboard, or the
    About-SDD framework page — and whether B10's two `Informational` notices are sufficient, insufficient, or
    excessive.
  - [ ] Re-examine **B4** deliberately. A single-adapter run emits no cross-adapter notice *by design*, so that
    existing BMad projects gain nothing. Confirm that still holds under your policy, or state the regression you
    are accepting.

- [ ] **Task 4 — Decide the source-discovery question, with costs as numbers (AC: #2)**
  - [ ] Price at minimum these four options; add others if the survey suggests them:
    - **A — Status quo.** One root by marker probe, bundle-level merge, non-primary documents diagnosed. Cost
      today = R2's six files on CORA. Zero implementation cost.
    - **B — Auxiliary document roots.** Additional read-only roots whose documents render under a per-root output
      prefix, exactly as `AdrSourceRoot`/`adrs/` already does (R3). Collision-free by construction; URL-visible
      under ADR 0017.
    - **C — Raise the root to `RepoRoot`, markers become filters.** Superficially obvious; likely disqualifying,
      because every existing page's source-relative path — and therefore its URL — moves. Price it precisely
      enough to kill it, because someone will propose it.
    - **D — A root-qualified path type** (`SourceRef(root, relative)`) replacing bare relative strings. The
      cleanest model and the largest refactor: every `ToSourceRelative` call site, diagnostic anchoring, watch,
      settings and the IR.
  - [ ] For each option state the cost to: watch mode (R6), ADR 0017's route shape and href non-rewriting (R5),
    the content-drift gates (R8), `DiagnosticAnchorRoot` resolution (R4), settings shape (R7), and
    `PathUtil.EscapesRepoRoot`.
  - [ ] Recommend one, and name the **trigger** that would make a different option correct later (e.g. "when a
    second real repository carries a full artifact set in both frameworks").

- [ ] **Task 5 — Rule on B1–B11 explicitly (AC: #3)**
  - [ ] Produce a table: behavior → **stands** / **refined** / **superseded**, with one sentence each.
  - [ ] For every "superseded", state the bounded follow-through — which story owns it, roughly what changes, and
    what proves it. AC #3's purpose is that the follow-up is *known*, not rediscovered.
  - [ ] Record the AC #1 five-field/three-unit correction (R1) as an explicit item.

- [ ] **Task 6 — Write the ADR and the spike report (AC: #1, #2, #3)**
  - [ ] **ADR `docs/adrs/0039-*.md`** — the durable deliverable. Subject: the multi-framework coexistence policy,
    amending ADR 0038. Standard shape: Status / Date / Deciders / Context / Decision / Consequences / Alternatives
    considered. It must carry the per-family policy, the source-discovery decision, the B1–B11 ruling, and an
    explicit **Supersedes** section. Index it in `docs/adrs/README.md`.
  - [ ] **Report `_bmad-output/implementation-artifacts/4-9-spike-report.md`** — the evidence: the survey, the
    counted call sites, the option pricing, the measured CORA numbers, and the handoffs (R10, and R11's ADR-0037
    index gap if still open). Follow `25-3-spike-report.md`'s shape — status/timebox/baseline header, executive
    summary, numbered sections, and a closing section proving no production code shipped.
  - [ ] The ADR carries the **decision**; the report carries the **evidence**. Do not duplicate the decision into
    the story file — AC #3 forbids exactly that.

- [ ] **Task 7 — Prove the spike shipped nothing (AC: all)**
  - [ ] `git status` / `git diff --stat` showing zero changes under `src/`, `tests/`, `web/`, `extension/`.
    Paste the evidence into the report.
  - [ ] No new NuGet or npm dependency. No new content-drift gate (ADR 0033 — prefer none; if you propose one for
    a future story, state its three preconditions).
  - [ ] If you ran the full suite, report the result honestly including R9's known flake; if you did not, say you
    did not. A spike that changes no code has no obligation to run it — but it has an obligation not to imply it
    did.

## Dev Notes

### What "decided" means here, in one paragraph

A spike that ends in "it depends on the framework" has failed AC #1's second clause, which exists precisely to
forbid that. Every family gets a verdict. Where the honest verdict is "precedence, and the loss is diagnosed" —
i.e. the status quo — that is a *decision*, and it is a good outcome if it is argued rather than defaulted to.
What is not acceptable is leaving the choice to whichever adapter author gets there next: that is how two
frameworks end up with two incompatible answers and a third story to reconcile them.

### The one asymmetry worth thinking hardest about

Bundle-level merging treats the two frameworks as **equal contributors to one project**. But CORA is not two
projects — it is one project that *plans* in BMad and *delivers* in GSD. The frameworks occupy different
**stages**, not different halves. A policy built on "first non-null wins" models rivalry; a policy built on
role ("this framework owns planning artifacts, that one owns delivery") models what is actually happening. The
second is more expensive and may be premature on a sample of one repository — but the spike should at least ask
whether the resolution axis is *adapter order* or *artifact role*, and say why it chose the one it chose.

### Architecture compliance

- **AD-1 / AD-2** [ARCHITECTURE-SPINE.md §Architecture Decisions] — one shared projection core; host-neutral view
  models are the contract. Any option that gives a framework its own rendering path violates AD-1 and should be
  rejected on that ground explicitly, not silently.
- **AD-5** + **ADR 0027** — watch behaviour must not regress; "safe" = proven byte-identical to a full rebuild.
  This is the sharpest constraint on any multi-root option (R6).
- **AD-3** — settings resolve once, before generation, with provenance preserved. A second root must resolve
  through `SettingsResolver`, not be discovered ad hoc mid-run (R7).
- **ADR 0017** — routes are IR paths verbatim; hrefs are never rewritten (R5).
- **ADR 0033** — gate discipline; prefer no new gate (R8).
- **ADR 0038** — the record you are amending. It explicitly defers this question in its §5.
- **§Seed, Not Invariant** [ARCHITECTURE-SPINE.md] — "exact adapter loading mechanics" are an implementation
  seed, which is what makes changing the registry legitimate. The single-project/single-namespace layout is
  **not** open.
- **NFR8** [epics.md:137] — absent, not broken or misleadingly empty. This governs the "what the reader is told"
  half of AC #1: a framework whose artifacts were displaced must read as a *stated boundary*, never as an
  unexplained gap.
- **FR1** [epics.md:43] — *"Implement a framework adapter contract that maps each supported framework into one
  shared projection model without rewriting the core HTML templating pipeline."* Epic 4's covered requirement, and
  the reason this question is seated here rather than in a per-framework epic.

### Anti-patterns to prevent

- **Re-deriving the registry from scratch.** It exists, it is merged, it is tested (`AdapterRegistryTests.cs`,
  14 tests). Read it first; your ADR amends it.
- **Proposing a second registry ADR.** ADR 0038 is *the* registry decision, inherited by Epics 11/12.3/13/14/15.
  Amend it with 0039; do not compete with it.
- **Pricing multi-root as unprecedented.** `AdrSourceRoot` is a working second root with output prefixing,
  per-root watchers, and per-root diagnostic anchoring (R3).
- **Writing the decision into the story file.** AC #3 forbids it. The ADR is the artifact.
- **Answering AC #1 against the five-field list without noticing B5.** The code resolves three units, not five.
- **Treating a green `check:parity` as evidence about C#-side behavior.** It cannot see it (R8).
- **Silently fixing R10's stale comment**, or adopting the ADR-0037 index gap. Both belong to other stories;
  record the handoff.
- **Shipping production code.** Even a "tiny obvious fix". This is a spike (Task 7).
- **Presenting a constructed fixture as observed evidence.** CORA is the only real multi-framework repository
  available; label everything else as constructed.
- **Deciding by adapter convenience.** "GSD wins because its adapter is newer" is not a strategy.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/4-9-multi-framework-coexistence-strategy-spike.md`
- Sprint key: `4-9-multi-framework-coexistence-strategy-spike` (Epic 4, `in-progress` — reopened for this
  post-retrospective amendment; `epic-4-retrospective` stays `done` deliberately)
- **Expected new files — exactly two:**
  - `docs/adrs/0039-<slug>.md`
  - `_bmad-output/implementation-artifacts/4-9-spike-report.md`
- **Expected modified — three:** `docs/adrs/README.md` (index entry), this story file, `sprint-status.yaml`.
- **Expected modified under `src/`, `tests/`, `web/`, `extension/`: none.** That is Task 7's assertion.
- **Timebox: ~2 days.** This is a decision spike over a codebase you can read, not a research project. If it is
  running longer, the scope has drifted — most likely into implementing an option rather than pricing it.
- **`.specscribe/analysis/` is gitignored and may be absent.** Absent means UNKNOWN, never clean. You touch no
  source files, so the digest is informational here; if you cite a file's quality state, refresh it first
  (`node tools/analysis-digest/index.mjs`) and check `provenance.evaluatedAtRevision` against `git rev-parse HEAD`.
- **No dependency research was required for this story.** The technology surface is entirely internal — C#
  file-system APIs and this repository's own contracts. No library version, API, or upstream breaking change
  bears on the decision; if the spike finds one that does, that is itself a finding worth recording.

### Testing

No new tests. The spike changes no behavior, so there is nothing to pin. If your recommendation *implies* a test
that a follow-up story should write, name it in the ADR's follow-through rather than writing it here.

Two existing test files are the relevant prior art to read (not to modify): `tests/SpecScribe.Tests/AdapterRegistryTests.cs`
(14 tests — what the merge rule already guarantees) and `tests/SpecScribe.Tests/GsdCoreArtifactAdapterTests.cs`
(20 tests — the fixture style, `Directory.CreateTempSubdirectory` + `const string` bodies, never reading `C:/dev/CORA`).

### Previous story intelligence

**Story 4.8** (`done`, the previous story in this epic) built the reader-facing surface AC #1's "what the reader
is told" lands on: `diagnostics.html`, reachable via footer → About → Diagnostics (an owner decision — there is
deliberately **no** nav entry and no dashboard callout). It renders the unified `GenerationEvent` channel with
category · source path · message, and degrades to a clean all-clear state. Two constraints carry forward: it is
a full-`GenerateAll` artifact and is **not** regenerated by watch's incremental paths (accepted, documented);
and 4.8 deliberately did **not** add new diagnostic categories or emission sites. If your strategy needs a new
reader surface, say so and price it — do not assume the diagnostics page absorbs it for free.

**Story 12.2** (`review`) is the immediate predecessor in substance, though not in this epic. Its Completion
Notes are the single most valuable document for this story: five decisions that differ from its own task text
(§D1–§D5) and seven findings (§F1–§F7). Most relevant here: §D1 (why `_bmad-output` probes last), §D2 (the watch
constraint lifted onto the interface rather than degraded), §F1 (the ir-content gate cannot see cross-framework
markup — measured), §F3 (the merge case is live on a real repository, and the escalation trigger was reached and
resolved *within* the bounded answer, leaving this story's question untouched).

**Story 12.1** (`done`) is the cautionary tale: a documentation-derived spike whose coverage map lost six of
eight claims on contact with a real repository. Its own Debug Log said so and required re-confirmation. The
lesson for 4.9: trace shipped code and inspect the real repo; do not reason from ADR prose alone — including
from ADR 0038, whose §5 describes an intent that the code may express slightly differently.

### Git intelligence summary

Baseline `7ff3b13` (tree clean apart from an unrelated worktree pointer). The recent history is directly
relevant, not incidental: `bafa488` "Story 12.2: GSD Core baseline adapter coverage" → `be65cf1` merging the
`worktree-story-12-2-gsd-core-adapter` branch, then `38507ce` (external-project CI recipe, seats Story 16.9) and
`7ff3b13`. So the code this story surveys landed **days**, not weeks, before it — read the merged state at HEAD,
not the story file's description of intent. Story 12.2 is still `review`, which means its hunks are live for its
own code review; R10's stale comment and any other 12.2-attributed defect you find should be **recorded as a
handoff**, not adopted (CLAUDE.md § hunk attribution).

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:904–936`] — Story 4.9's seating comment and ACs #1–#3 verbatim.
- [Source: `_bmad-output/planning-artifacts/epics.md:832–838`] — Epic 4's objective and FR1 coverage.
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:113–130`] — Epic 4's keys, the
  `epic-4-retrospective: done` rationale, and Story 4.9's seating note.
- [Source: `docs/adrs/0038-framework-adapter-selection-and-neutral-source-root-discovery.md`] — the record this
  story amends; §2 the merge table, §3 marker order, §4 the watch lift, **§5 what it deliberately leaves open**.
- [Source: `docs/adrs/0017-projection-routes-mirror-ir-paths.md`] — routes ARE IR paths; hrefs never rewritten;
  the Nitro `..` guard.
- [Source: `docs/adrs/0027-watch-rebuild-scope-is-one-classifier-and-topology-escalates.md`] — "safe" = proven
  byte-identical.
- [Source: `docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md`] — gate preconditions.
- [Source: `docs/adrs/0014-specscribe-settings-folder-format.md`,
  `docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md`] — settings shape.
- [Source: `docs/adrs/0021-carrying-foreign-artifacts-verbatim-into-the-portal.md`] — why a `.html` planning
  artifact is not simply a page, and the `0019` numbering collision.
- [Source: `src/SpecScribe/AdapterRegistry.cs`] — B1–B11 in full: `Select`, `Ingest`, `IngestEpics`, `Dropped`,
  `DescribeMatchSet`, `AppendNonPrimaryMarkerNotice`, `HasModuleIdentity`.
- [Source: `src/SpecScribe/ForgeOptions.cs` — `SourceDirName`, `SourceDirNames`, `Resolve`, `AdrOutputSubdir`,
  `FindSourceMarker`] — marker order, the walk-up, R10's stale comment, and the ADR output prefix.
- [Source: `src/SpecScribe/SiteGenerator.cs` — `EnumerateSourceFiles`, `ToSourceRelative`, `EnumerateAdrFiles`]
  — the single-root anchors and their ADR-root twins.
- [Source: `src/SpecScribe/PathUtil.cs` — `ToOutputRelative`, `EscapesRepoRoot`] — output path derivation and the
  escape guard.
- [Source: `src/SpecScribe/DiagnosticsTemplater.cs:25` — `DiagnosticAnchorRoot`] and [`src/SpecScribe/Commands.cs`
  — the anchor→real-file resolution and `StripAdrOutputPrefix`] — R4.
- [Source: `src/SpecScribe/FileWatcherService.cs:56–75`] — per-root file and directory watchers.
- [Source: `_bmad-output/implementation-artifacts/12-2-gsd-core-baseline-adapter-coverage.md`] — §D1–§D5,
  §F1–§F7, and the coverage map. **Read the Completion Notes in full.**
- [Source: `_bmad-output/implementation-artifacts/4-8-generation-diagnostics-and-configuration-log-page.md`] —
  the diagnostics page's scope, its reachability model, and what it deliberately does not do.
- [Source: `_bmad-output/implementation-artifacts/25-3-spike-report.md`] — the spike-report shape to mirror,
  including its "shipped no production code" proof.
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`] — AD-1/AD-2/AD-3/AD-5, § Seed Not Invariant.
- [Repo: `C:/dev/CORA`, inspected live 2026-08-06] — `_bmad/` + `_bmad-output/planning-artifacts/` (5 `.md`,
  1 `.html`, **no** `epics.md`, `implementation-artifacts/` or `sprint-status.yaml`) + `.planning/` (168 files).
  **A reference, never a test dependency** — CI has no such path.
- [Source: `CLAUDE.md` § Concurrent work, § Which gate is which, § Decision records, § Verification]

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-08-06 — Story 4.9 drafted (create-story, baseline `7ff3b13`). Ultimate context engine analysis completed
  — comprehensive developer guide created. **Drafted against merged code, not against intent**: Story 12.2's
  registry, neutral source-root discovery and ADR 0038 landed on `main` (`be65cf1`) days before this story, so
  the eleven behaviors AC #3 must rule on (B1–B11) are enumerated from `AdapterRegistry.cs` with symbol anchors
  rather than from the ADR's prose. Three reconciliations materially change the shape of the work: **(1)** AC #1's
  five single-valued fields are **three** resolution units in shipped code — the epics family is claimed as a
  unit (ADR 0038 §2) — so the AC's list is superseded and the spike must say so; **(2)** `AdrSourceRoot` is
  **already a second source root** with output prefixing (`adrs/`), per-root watchers and per-root diagnostic
  anchoring, so multi-rooting is a generalization of a working two-root case rather than a leap, and any option
  paper pricing it as unprecedented is wrong on the facts; **(3)** the cost of today's single root is
  **measured, not abstract** — six documents in `C:/dev/CORA`'s `_bmad-output/` do not render. Four costs the AC
  does not name are anchored for pricing: `DiagnosticAnchorRoot.Source` becomes ambiguous under two roots and is
  a contract shared with the extension; the watch topology sentinel and `IsUnderOutputRoot` guard are single-root;
  settings persistence is single-valued and a shape change amends ADR 0003/0014; and ADR 0017 makes any
  output-path scheme a **public** URL change with no href rewriter to compensate. Deliverables fixed at two —
  ADR `0039` (0019 remains claimed-but-unwritten) plus `4-9-spike-report.md` — with a Task 7 proof that no
  production code shipped. Two handoffs recorded rather than adopted, per hunk attribution: a stale
  "(BMad first)" comment in `ForgeOptions.Resolve` that contradicts both the array above it and ADR 0038 §3, and
  ADR 0037's missing index entry.
