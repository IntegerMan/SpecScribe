# Story 4.9 — Spike Report: Multi-Framework Coexistence Strategy

**Status:** complete · **Timebox:** ~2 days, used well under one · **Date:** 2026-08-07
**Story baseline:** `7ff3b13` · **Session HEAD:** `07bdb79` · **Worktree:** `worktree-story-4-9-dev`
**Reference repository:** `C:/dev/CORA` at `f312528`, inspected live 2026-08-07
**Durable deliverable:** [ADR 0041](../../docs/adrs/0041-multi-framework-coexistence-policy.md) — **not** ADR 0039; see § 0.1
**Analysis digest:** `.specscribe/analysis/` is **absent** at this HEAD. Absent means UNKNOWN, never clean. No
file's quality state is cited anywhere in this report, so nothing here depends on it.

> **Ships no production code.** Zero changes under `src/`, `tests/`, `web/`, `extension/`. Proof in § 9.

---

## 0. Executive summary

The strategy is decided, and the measurements changed what it should be.

Story 4.9 was framed as "precedence, merge, or refusal per family, and should the root become multi-rooted".
Driving the shipped registry over the real reference repository and four constructed shapes produced a different
question. **The merge rules are not wrong; they are unreachable.** Across eight scenarios — including two real
multi-adapter runs on CORA and two constructed repositories where *both* frameworks hold a complete artifact set
— **not one `Skipped` diagnostic fired**. Every adapter is handed the same `sourceFiles` list from the single
`SourceRoot`, so a non-primary framework has nothing to lose and nothing is reported as lost.

Seven measured findings, each of which changes what a follow-up story would otherwise build:

| # | Measured | Consequence |
|---|---|---|
| **M1** | **0** `Skipped` diagnostics across **8** scenarios, incl. 2 real multi-adapter CORA runs and 2 constructed full-contention repos | The first-non-null merge rules and their drop-reporting have never executed. Policy must be written against root ownership, not roster order. § 3 |
| **M2** | On CORA, the BMad adapter contributes **exactly one** field — `Module` — and only because `ModuleContext.Detect` is anchored to `RepoRoot`, not `SourceRoot` | ADR 0038 §5's "artifact families still merge into the bundle" is **false**. § 3.1 |
| **M3** | A vestigial `.planning/` (abandoned 2020) **wins both layers** — source root *and* epics family — and the live BMad `epics.md` is absent with **no** diagnostic naming the loss | Marker probes test presence, never life. The highest-value fix in this spike. § 4.3 |
| **M4** | Following the registry's **own printed advice** (`--source _bmad-output`) on CORA emits **2** `Source`-anchored diagnostics whose paths **escape** the root: `../.planning/STATE.md`, `../.planning/config.json` | A live defect on a contract shared with the VS Code extension (ADR 0037). Handoff H3. § 7 |
| **M5** | `--source` at a repository root re-derives `RepoRoot` as its **parent**: on CORA, **9,510** files walked, site branded `dev`, **no** adapter matched, **0** diagnostics | Option C is not merely costly, it is not currently expressible. Kills it on measurement. § 5.3 |
| **M6** | A monorepo with per-package frameworks **throws** `DirectoryNotFoundException` from the monorepo root | Shape (d) is unsupported, not degraded. § 4.4 |
| **M7** | B11 (watch-scoped re-ingest agrees with the full build on the epics owner) held in **8 of 8** scenarios | ADR 0038 §4's structural guarantee re-confirmed by measurement, not assumed. § 6 |

**The decision.** Role, not rivalry, is the resolution axis — expressed through the mechanism already shipped
(the framework owning the resolved root owns delivery), not through a new role vocabulary on the adapter contract.
Source discovery **stays single-rooted** (Option A); Option B (auxiliary document roots on the `AdrSourceRoot`
pattern) is named as the option to take when the trigger fires; Option C is rejected on measurement; Option D is
deferred. The three defects worth fixing are **independent of root count** and cost a fraction of any of B/C/D.

### 0.1 ⚠️ The ADR number moved: **0041**, not 0039

The story file, its Change Log, and the `sprint-status.yaml` create-story note all specify **ADR 0039**. That was
correct at the story's baseline `7ff3b13`. Between that baseline and this session's HEAD `07bdb79`, **both 0039
and 0040 were claimed** by other stories:

- `0039-runtime-attached-body-level-classes.md` — the owner's sunburst verify round, dated 2026-08-06.
- `0040-release-channels-and-versioning-policy.md` — Story 16.1's packaging spike, dated 2026-08-07.

This spike therefore takes **0041**. `0019` remains claimed-but-unwritten (Stories 18.3 and 22.3) and was not
taken. See handoff **H4** for a factual error this collision left in the index.

---

## 1. What was actually run

Ground truth was re-established by reading, then **measured** with a probe harness. The harness lives **outside
the repository** (`$CLAUDE_JOB_DIR/tmp/probe/`, a throwaway console project referencing `SpecScribe.csproj`); it
ships nothing and appears in no File List.

It replicates `SiteGenerator.EnumerateSourceFiles` **exactly** — that method is private, so the harness reproduces
its body (`Directory.EnumerateFiles(SourceRoot, "*.md", AllDirectories)`, filtered by
`PathUtil.IsIgnoredSourceFile`, ordered `OrdinalIgnoreCase`) so that what the adapters see under the harness is
what they see in a real run. It then calls the public `AdapterRegistry.Select`, `Ingest` and `IngestEpics`, plus
each matched adapter's `Ingest` **in isolation**, and flags any emitted diagnostic path through
`PathUtil.EscapesRepoRoot`.

**Everything labelled OBSERVED is the real `C:/dev/CORA`. Everything labelled CONSTRUCTED is a fixture built for
this spike.** CORA is the only real multi-framework repository available; no constructed case is presented as
observed evidence.

The full suite was **not** run. This spike changes no code, so it has no obligation to — and this report does not
imply it did. R9's known `FileWatcherServiceTests` load flake (Story 12.2 §F7) was therefore neither hit nor
disproved.

### 1.1 B1–B11 confirmed by symbol at HEAD

All eleven behaviors in the story's R1 still describe `src/SpecScribe/AdapterRegistry.cs` at `07bdb79`, confirmed
**by symbol** rather than by line: `Select`, `Ingest` (the `bundles.Count == 1` early return, the `epicsOwner`
block, the `TryAdd` block), `IngestEpics`, `Dropped`, `DescribeMatchSet`, `AppendNonPrimaryMarkerNotice`,
`HasModuleIdentity`, `Default`. None had moved. R10's stale comment is also still present (handoff **H1**).

---

## 2. Counted call sites — AC #2's evidence

AC #2 requires costs "stated, not assumed", and its create-story refinement adds that *"a cost expressed as
'moderate' fails this AC."* These are counts at `07bdb79`, produced by `grep -c` per file, not estimates.

### 2.1 The single-root anchors, and their ADR-root twins

| Symbol | `src/` count | Per-file breakdown |
|---|---:|---|
| `.SourceRoot` | **46** | `SiteGenerator` 22, `Commands` 6, `GsdCoreArtifactAdapter` 4, `AdapterRegistry` 3, `FileWatcherService` 3, `BmadArtifactAdapter` 2, `DashboardViewBuilder` 2, `DiagnosticsTemplater` 2, `ConsoleUi` 1, `SettingsResolver` 1 |
| `.AdrSourceRoot` | **29** | `SiteGenerator` 12, `Commands` 7, `ConsoleUi` 4, `FileWatcherService` 3, `DashboardViewBuilder` 1, `DiagnosticsTemplater` 1, `SettingsResolver` 1 |
| `.SourceRoot` in `tests/` | **20** | `AdapterRegistryTests` 7, `ForgeOptionsTests` 5, `SettingsResolverTests` 4, `SiteGeneratorWebviewTests` 3, `DiagnosticsTemplaterTests` 1 |

Of the 46, **20** are the `_options.SourceRoot` field form inside `SiteGenerator`; **13** are the
`options.SourceRoot` parameter form used by adapters and command code.

### 2.2 `ToSourceRelative` — **four** definitions, not three

The story's R2 says three private copies. There are **four**, because `GsdCoreArtifactAdapter` carries two
overloads:

| Definition | Body |
|---|---|
| `SiteGenerator.ToSourceRelative(string)` | `Path.GetRelativePath(_options.SourceRoot, fullPath)` |
| `BmadArtifactAdapter.ToSourceRelative(ForgeOptions, string)` | `Path.GetRelativePath(options.SourceRoot, fullPath)` |
| `GsdCoreArtifactAdapter.ToSourceRelative(ForgeOptions, string)` | same |
| `GsdCoreArtifactAdapter.ToSourceRelative(ForgeOptions, string planningRoot, string fileName)` | same, over a composed path |

Call sites: `SiteGenerator` **21**, `BmadArtifactAdapter` **12**, `GsdCoreArtifactAdapter` **8** — **41 total**.

`EnumerateSourceFiles` has **1** definition (`SiteGenerator.cs:6456`) and **4** call sites;
`EnumerateAdrFiles` has **1** definition (`SiteGenerator.cs:6604`) and **4** call sites.
`PathUtil.EscapesRepoRoot` has **1** definition and **3** production call sites
(`GsdCoreArtifactAdapter`, `IdeaDiscovery`, `SiteGenerator`).

### 2.3 The two-root machinery that already exists (R3, confirmed)

Every mechanism a multi-rooted design needs is already in production for `AdrSourceRoot`:

| Concern | Shipped mechanism |
|---|---|
| Separate discovery walk | `SiteGenerator.EnumerateAdrFiles`, rooted at `_options.AdrSourceRoot`, bounded to root + one level |
| Collision-free output | `ForgeOptions.AdrOutputSubdir = "adrs"` |
| Relativization against the right root | `Path.GetRelativePath(_options.AdrSourceRoot, file)`, kept separate |
| Diagnostics that know their root | `DiagnosticAnchorRoot { None, Source, Adr, Repo }` |
| Notice → real file | `Commands.cs` combines `Adr` with `AdrSourceRoot` after `StripAdrOutputPrefix` |
| Watch | `FileWatcherService` creates a filtered file watcher **and** a `NotifyFilters.DirectoryName` watcher for **each** of `SourceRoot` and `AdrSourceRoot` — 4 of its watchers, plus the `_bmad` config watcher |
| CLI + persistence | `--adrs`, `SavedSettings.Adrs`, its own `--show-config` provenance row (`SettingsResolver.Fields.Adrs`) |

R3 is correct and load-bearing: **multi-rooting is generalizing a working two-root case.** Pricing it as
unprecedented would be wrong on the facts. It is still not cheap — the pairs above become loops — but the shape
is proven.

---

## 3. The finding that reframes the story: the merge rules are unreachable

### 3.1 Measured — what each adapter can actually contribute

Every family except `Module` derives either from the `sourceFiles` list (enumerated from `SourceRoot`) or from
`options.SourceRoot` directly:

- `BmadArtifactAdapter.IngestSprint` → `FindSprintStatusCandidates(options.SourceRoot)`
- `BmadArtifactAdapter.IngestEpics` / `IngestRetros` / `BuildArtifactMap` → the `sourceFiles` list
- `ModuleContext.Detect(options.RepoRoot, sourceRelatives, …)` → **`RepoRoot`**, which is the exception

Measured, CORA at default resolution (`SourceRoot = .planning`, 164 source files, **OBSERVED**):

| Adapter (in isolation) | Epics | Sprint | Requirements | Module | Retros | StoryArtifacts |
|---|---|---|---|---|---|---|
| `GsdCoreArtifactAdapter` | **14 epics** | present | null | Unknown | 0 | **58** |
| `BmadArtifactAdapter` | null | null | null | **BmadMethod** | 0 | 0 |
| **Merged** | 14 epics | present | null | BmadMethod | 0 | 58 |

And in the other direction (`--source _bmad-output`, 5 source files, **OBSERVED**):

| Adapter (in isolation) | Epics | Sprint | Requirements | Module | Retros | StoryArtifacts |
|---|---|---|---|---|---|---|
| `GsdCoreArtifactAdapter` | null | null | null | Unknown | 0 | 0 |
| `BmadArtifactAdapter` | null | null | null | **BmadMethod** | 0 | 0 |

So **`Module` is the only family that ever crosses the root boundary**, and it does so because of a parameter
choice in one method signature, not because bundle-level merging works. ADR 0038 §5's claim that the non-primary
framework's "artifact families still merge into the bundle" does not survive measurement — and neither does the
same sentence as it appears in the reader-facing notice (§ 7, handoff **H2**).

### 3.2 Zero `Skipped` diagnostics, across everything

`Dropped()` — the `Skipped` diagnostic that names an adapter, the family it lost, and the winner — is the
mechanism ADR 0038 §2 offers as the guarantee that "every merge loss is visible". It did not fire once:

| Scenario | Adapters matched | `Skipped` emitted |
|---|---:|---:|
| CORA default (OBSERVED) | 2 | **0** |
| CORA `--source _bmad-output` (OBSERVED) | 2 | **0** |
| CORA `--source` repo root (OBSERVED) | 1 (fallback) | **0** |
| Shape (b) migration, both full (CONSTRUCTED) | 2 | **0** |
| Shape (b) `--source _bmad-output` (CONSTRUCTED) | 2 | **0** |
| Shape (c) vestigial marker (CONSTRUCTED) | 2 | **0** |
| Shape (d) monorepo, from `packages/alpha` (CONSTRUCTED) | 1 | **0** |
| Option C on shape (b) (CONSTRUCTED) | 1 (fallback) | **0** |

The 44 `Skipped` diagnostics GSD emits on CORA are *intra*-framework (`-SUMMARY.md` companions to `-PLAN.md`
story artifacts), not merge losses.

**This is not an argument that the rules should be deleted.** They are the correct behaviour *if* two frameworks
ever contend, and B9's duplicate-id rule in particular is right. It is an argument that policy must be written
against what decides today — root ownership — and that a rule which appears to arbitrate should not be left
implying a choice it never makes. ADR 0041 §1 and §2 do exactly that.

---

## 4. The survey — four coexistence shapes, traced and measured

### 4.1 Shape (a): plan in one, deliver in another — **OBSERVED**, CORA

`_bmad/` (with `bmm`, `tea`, …) + `_bmad-output/planning-artifacts/` (6 files) + `.planning/` (168 files, 164
`.md`). Both `AppliesTo` return true.

Resolution: `SourceRoot = .planning` (GSD's install marker precedes BMad's output folder, ADR 0038 §3), site
branded **CORA** from `.planning/PROJECT.md`'s H1, 14 epics / 58 story artifacts / sprint from GSD, module
identity from BMad. Two `Informational` notices fire (B10).

**Verdict: coherent, and it is the case the design was built for.** The role split reads correctly — BMad's
module identity beside GSD's delivery data is the *planning-stage* fact sitting beside the *delivery-stage* data.

**R2 re-derived.** `_bmad-output/planning-artifacts/` holds exactly six files at `f312528`:
`architecture.md`, `prd.md`, `product-brief-CORA-knowledge-graph.md`,
`product-brief-CORA-knowledge-graph-distillate.md`, `ux-design-specification.md`, and
`ux-design-directions.html`. The five `.md` do not render as pages; the `.html` would not be caught by the `*.md`
scan under any root (ADR 0021). **The number is unchanged from create-story.** Two repo-root documents
(`README.md`, `AGENTS.md`) are also outside the root — but that is not a multi-framework cost: a BMad-only
repository's root `AGENTS.md` is equally invisible, and `README.md` is separately special-cased via
`IncludeReadme`.

### 4.2 Shape (b): migration in progress, both frameworks hold a full set — **CONSTRUCTED**

`_bmad/` + `_bmad-output/` (epics.md + sprint-status.yaml) + `.planning/` (ROADMAP.md + STATE.md + a plan).

Measured: `SourceRoot = .planning`. GSD supplies 1 epic, 1 story artifact and the sprint. **BMad supplies only
`Module`** — its complete, live artifact set is invisible, and **no diagnostic names it**. The generic
non-primary-marker notice fires, but it talks about *loose documents*, not about a displaced epics index and
sprint ledger.

**Verdict: degraded, and dishonestly so.** Not "wrong" — the portal shows real GSD data — but the reader is told
the wrong thing about what was lost.

### 4.3 Shape (c): vestigial marker — **CONSTRUCTED**, and the one that is actively wrong

A live BMad project (`_bmad/config.toml` → `project_name = "Live BMad Project"`, a current `epics.md`) that
migrated **off** GSD and left `.planning/` behind holding a 2020 roadmap.

Measured:

```
SourceRoot = …/shape-c-vestigial/.planning
SiteTitle  = Live BMad Project
MATCHED    = [GsdCoreArtifactAdapter, BmadArtifactAdapter]
  GsdCore : Epics=1 epics, Sprint=present, StoryArtifacts=1, EpicsSource=ROADMAP.md
  BMad    : Epics=null,    Sprint=null,    StoryArtifacts=0, Module=BmadMethod
MERGED    : Epics=1 epics (the STALE one), Sprint=present, Module=BmadMethod
Skipped diagnostics: 0
```

The portal is **correctly branded with the live project's name** and shows the **abandoned framework's 2020
roadmap** as its only epic. The live epic does not appear anywhere and nothing says it was displaced.

**Cause, in two layers, both of which test presence rather than life:**

- `ForgeOptions.FindSourceMarker` → `Directory.Exists(Path.Combine(dir, marker))`
- `GsdCoreArtifactAdapter.AppliesTo` → `Directory.Exists(Path.Combine(options.RepoRoot, ".planning"))`
- `BmadArtifactAdapter.AppliesTo` → `Directory.Exists(Path.Combine(options.RepoRoot, "_bmad"))`

**Verdict: wrong.** This is the shape that makes "every matching adapter runs" actively incorrect, exactly as the
story predicted — and it is cheap to fix, because **this repository already contains the right pattern**:
`ForgeOptions.AdrFallbackProbeSubdirs` is probed by `HasMarkdownWithinOneLevel`, a deliberately bounded content
test ("at least one markdown file within one directory level", never a whole-tree walk). Source-marker probing is
the same question answered inconsistently. See ADR 0041 §4a.

### 4.4 Shape (d): monorepo with per-package frameworks — **CONSTRUCTED**

`packages/alpha/_bmad{,-output}/` + `packages/beta/.planning/`, no marker at the monorepo root.

Measured from the monorepo root:

```
ForgeOptions.Resolve THREW DirectoryNotFoundException:
  Could not locate a repo root (a directory containing one of '.planning', '.gsd', '.specify',
  '_bmad-output') at or above the current directory. …
```

The walk-up only ascends; it never descends. From `packages/alpha` it resolves normally, matches BMad alone, and
produces a portal covering alpha only — beta is not merely unrendered, it is unrepresented.

**Verdict: unsupported, and honestly so** — the throw is actionable and names the marker set. Worth stating
plainly rather than leaving a reader to infer that a monorepo "sort of works". No option in § 5 addresses shape
(d); it needs per-package *runs*, not more roots, and that is a separate question.

---

## 5. Source discovery — four options, priced

Each option is priced against the six couplings AC #2 and its refinement name.

### 5.1 Option A — status quo (one root by marker probe, bundle-level merge)

- **Implementation cost:** zero.
- **Measured cost today:** six documents on CORA (§ 4.1). Plus, now measured: shape (c) is wrong (§ 4.3), the
  reader notice is inaccurate (§ 7), and two diagnostics escape their anchor (§ 7).
- Watch / ADR 0017 / gates / `DiagnosticAnchorRoot` / settings / `EscapesRepoRoot`: **no change** to any.

### 5.2 Option B — auxiliary document roots, output-prefixed (the `AdrSourceRoot` pattern)

Additional read-only roots whose documents render under a per-root prefix, exactly as `AdrSourceRoot` → `adrs/`.

| Coupling | Cost |
|---|---|
| **Watch (R6, AD-5, ADR 0027)** | One filtered file watcher **+** one `DirectoryName` watcher per root — the shape `FileWatcherService` already builds twice. Story 5.3's coalescing sentinel is the single literal `TopologySentinelKey = "<topology>"`; it needs a per-root key **or** a written argument that one shared sentinel stays correct (a topology escalation rebuilds the whole site, so one shared key is defensible — but it must be *argued*, since ADR 0027 defines safe as proven byte-identical). `IsUnderOutputRoot` must be applied per root. |
| **ADR 0017 routes** | **New** roots get **new** prefixes, so **existing URLs do not move** — B's decisive advantage over C. New pages are new public routes. No `..` ever appears in a route, so §Decision 5's Nitro guard is satisfied by construction. |
| **Hrefs** | Not rewritten, by design (§Decision 2). A carried document's existing relative links resolve against its new depth. For a root rendered at a *fixed* prefix depth this is predictable, but it is a real behavioural difference and must be tested, not assumed. |
| **Gates (R8, ADR 0033)** | Neither existing gate can see it: `check:parity` renders a frozen corpus IR; `check:ir-content` derives from this BMad repository's own IR. Prefer **no** new gate. |
| **`DiagnosticAnchorRoot` (R4)** | The enum stops being sufficient: `Source` must become a root *identifier*, and `Commands.cs`'s join must select the right root. This is the extension contract (Story 6.12, ADR 0037), not an internal detail. `Adr` is the worked precedent. |
| **Settings (R7)** | `--source`/`SavedSettings.Source`/`source_root` are single scalars. A second root is either a list-shaped setting or a second scalar — an on-disk shape change to `.specscribe/config.json` governed by ADR 0014 (extending ADR 0003), inheriting Story 5.5's whole-document-deserialization blast radius. `--show-config`'s one-line-per-field contract needs a rule for a list. |
| **`EscapesRepoRoot`** | Unchanged *if* each root relativizes against itself. This is precisely what § 7's defect gets wrong today, which is the warning: the guard only works when the anchor is chosen correctly. |

**Collision, stated honestly.** CORA does **not** collide today: its two roots share no markdown basename, and
the paths differ regardless (`planning-artifacts/prd.md` vs top-level `PROJECT.md`). The risk is **structural,
not observed** — two roots may each hold a `README.md`, which is additionally special-cased into `index.html`.
Per-root output prefixing makes it collision-free by construction, which is the point of the option.

### 5.3 Option C — raise the root to `RepoRoot`, markers become filters. **REJECTED**

Priced precisely enough to kill it, because it is the obvious proposal.

**It is not currently expressible.** `ForgeOptions.Resolve` sets `repoRoot = Path.GetDirectoryName(sourceRoot)`
for an explicit `--source`, so pointing at a repository root silently relocates every marker probe one level up.
Measured on CORA (**OBSERVED**):

```
--source C:\dev\CORA
  RepoRoot   = C:\dev            ← the PARENT
  SourceRoot = C:\dev\CORA
  SiteTitle  = dev               ← the parent folder's name
  sourceFiles = 9,510
  MATCHED    = [BmadArtifactAdapter]   ← the NO-MATCH FALLBACK, not a real match
  MERGED     : Epics=null, Sprint=null, Module=Unknown
  DIAGNOSTICS: 0
```

A silently wrong portal: nothing detected, nothing reported, 9,510 files walked. Reproduced identically on
constructed shape (b).

Even with that coupling repaired, **every existing page's source-relative path — and therefore its URL — moves**,
which ADR 0017 §Consequences classifies as a **public** change, with no href rewriter to compensate (§Decision 2).
Add: the `*.md` walk becomes whole-repository (the 9,510 figure is what that costs on one machine), and the
`IsUnderOutputRoot` guard becomes load-bearing rather than defensive, since the output root is now inside the
source root by default.

### 5.4 Option D — a root-qualified path type `SourceRef(root, relative)`

The cleanest model and the largest refactor: the **41** `ToSourceRelative` call sites behind **4** definitions,
**46** `.SourceRoot` + **29** `.AdrSourceRoot` references in `src/` (**20** more in `tests/`), plus the IR schema
(ADR 0008), settings, watch and diagnostic anchoring. **Deferred, not rejected** — it is what B becomes if a
third or fourth root ever appears.

### 5.5 Recommendation and trigger

**Take A. Adopt B when the trigger fires. The three fixes worth doing now are not multi-rooting at all** —
liveness probes (§ 4.3), containment-correct diagnostics (§ 7), and an accurate non-primary notice (§ 7).

**Trigger for B:** a second *real* repository that carries a full artifact set in **both** frameworks **and** a
reader who needs the non-primary framework's loose documents as pages. CORA is not that repository: its
`_bmad-output` holds planning prose with no epics source and no sprint file, which is why pointing `--source` at
it yields a five-page portal with no epics at all.

**AD-1 compliance:** every option above keeps one shared projection core and host-neutral view models. No option
that gives a framework its own rendering path was considered admissible; that would violate AD-1 and is rejected
on that ground explicitly.

---

## 6. B1–B11 — the ruling AC #3 requires

Every behavior gets a verdict. "Supersedes nothing" is an allowed outcome; silence is not.

| # | Behavior | Verdict | One sentence |
|---|---|---|---|
| **B1** | `Select` returns **every** `AppliesTo` match, not the first | **refined** | The rule stands; *matching* must mean **live**, not **present** — shape (c) measured a 2020 husk beating a live project. |
| **B2** | No match → `BmadArtifactAdapter` fallback alone | **stands** | The compatibility floor, and it behaved correctly in both fallback scenarios; note it is also what an Option-C-shaped root silently reaches (§ 5.3). |
| **B3** | Roster order: framework markers first, BMad last | **stands** | Unchanged — but demoted in significance: measured, roster order decided **no** family in **any** scenario. |
| **B4** | Single adapter → bundle returned verbatim, same instance, **no** cross-adapter diagnostic | **stands** | Deliberately re-examined per Task 3: existing BMad-only projects gain nothing and lose nothing, which is correct — a notice firing on every existing project would be the regression. No regression accepted. |
| **B5** | Epics **family** claimed together by the first adapter that FOUND an epics source | **stands** (unit) / **refined** (tiebreak) | The unit is right and must not be split; the tiebreak changes from *roster order* to *root ownership*, which is what the code already does de facto. |
| **B6** | `Sprint` — first non-null wins; loser gets `Skipped` | **superseded** | Sprint binds to the **epics owner**; a ledger of framework A's phases beside framework B's epics index is incoherent for the same reason requirements are. Today they coincide by accident. |
| **B7** | `Module` — first with a real detected identity; ties to the first | **refined** | Mechanically unchanged, but reinterpreted as a **role** contribution (planning-side) rather than a won contest, and it must be attributed on the About-SDD page or the portal implies an ownership it does not have. |
| **B8** | `Retros`/`Diagnostics` concatenate; `ConsumedSourceRelatives` unions | **stands** | Additive, no contention possible. |
| **B9** | `StoryArtifactsById` unions; duplicate id keeps the earlier, emits `Skipped` | **stands** | Correct as written — two frameworks numbering independently really can collide — though measured unreachable today (§ 3.2). |
| **B10** | One `Informational` naming who matched and supplied what, **plus** one naming a non-primary marker | **refined** | The first is the right content on the wrong surface (promote to About-SDD); the second contains a **measurably false clause** and must be corrected (§ 7). |
| **B11** | Watch-scoped re-ingest merges by the same epics-ownership rule | **stands** | Re-measured: full build and scoped re-ingest agreed on owner, epic count and artifact count in **8 of 8** scenarios. |

### 6.1 The AC #1 five-field → three-unit correction

Recorded explicitly, as Task 5 requires. AC #1 names five independently-resolved single-valued fields
(`Epics`, `Sprint`, `Requirements`, `Module`, `EpicsSourceFullPath`). Shipped code resolves **three** units,
because `Epics` + `Requirements` + `EpicsSourceFullPath` are claimed together (ADR 0038 §2; `Ingest`'s
`epicsOwner` block). The AC's list is **superseded**; ADR 0041 §2 answers the three real units and §5 records the
correction.

### 6.2 Bounded follow-through for every non-"stands" verdict

AC #3's purpose is that the follow-up be *known*, not rediscovered.

| Item | Change | Proof |
|---|---|---|
| **FT-1 — liveness probes** (B1) | `FindSourceMarker` and both `AppliesTo` require artifacts, mirroring `HasMarkdownWithinOneLevel`'s bounded content test | Unit tests over `ForgeOptions.Resolve` and each `AppliesTo` for the shape (c) fixture; a pinned test that a genuinely empty framework dir no longer wins |
| **FT-2 — sprint binds to epics owner** (B6) | `AdapterRegistry.Ingest` resolves `Sprint` from the epics owner rather than first-non-null | `AdapterRegistryTests` case: two adapters both supplying a sprint, non-epics-owner's dropped |
| **FT-3 — attribution on About-SDD** (B7, B10) | Per-family attribution row on the framework page; diagnostics-page rows retained | Region unit test + live-browser check (neither gate can see it — § 8) |
| **FT-4 — correct the non-primary notice** (B10) | Remove the false family-merging clause; say what actually happens | `AdapterRegistryTests` assertion on the notice text |
| **FT-5 — containment-correct diagnostics** (§ 7) | `IngestSprint` / `ReportUnsupportedArtifacts` route through `ResolvePlanningRoot` or anchor `Repo` | Assertion that **no** emitted diagnostic path satisfies `PathUtil.EscapesRepoRoot` for the `--source _bmad-output` fixture |

**Seating is the owner's call.** FT-1…FT-5 are naturally one story in Epic 4 (the adapter-contract epic) — FT-5
is arguably Story 12.2's to absorb while it is still `review`. Per CLAUDE.md § Decision records, a structural
scope change must land in `epics.md` **and** `sprint-status.yaml` in the same change, so this spike names the
work and does not seat it.

---

## 7. Two live defects and one false claim, found while measuring

All three are **recorded, not fixed** — this spike ships no production code, and all three sit in Story 12.2's
hunks while Story 12.2 is still `review` (CLAUDE.md § hunk attribution).

### 7.1 Diagnostics that escape their anchor — **measured**

`GsdCoreArtifactAdapter.ResolvePlanningRoot` implements a correct containment check and refuses cleanly when
`.planning/` lies outside the source root. But `Ingest` then calls **`IngestSprint`** and
**`ReportUnsupportedArtifacts`** unconditionally, and both **re-derive** `planningRoot` from
`Path.Combine(options.RepoRoot, MarkerDirName)` **without** that check, then relativize with
`ToSourceRelative(options, …)` against `SourceRoot`.

Measured on CORA, `--source _bmad-output` (**OBSERVED**):

```
[Unsupported/Source]    path='../.planning/STATE.md'      <<< EscapesRepoRoot == true
[Informational/Source]  path='../.planning/config.json'   <<< EscapesRepoRoot == true
```

Reproduced on constructed shape (b) (`STATE.md` only — that fixture has no `config.json`).

**Why it matters beyond tidiness:** `Commands.cs` resolves a `DiagnosticAnchorRoot.Source` notice to a real file
by joining `resolved.SourceRoot` with `notice.SourcePath` for the VS Code Problems panel. An escaping path
resolves outside the repository. This is the contract shared with the extension (Story 6.12, ADR 0037), and
`PathUtil.EscapesRepoRoot` exists precisely to reject these values. It is also the path the registry's **own
printed advice** steers users onto.

### 7.2 The non-primary marker notice states something false — **measured, both directions**

`AppendNonPrimaryMarkerNotice` tells the reader:

> "Artifact families from those frameworks are still merged into the portal, but their loose documents are
> outside the single source root and do not render as their own pages."

Measured on CORA: with `SourceRoot = .planning`, BMad contributes **only `Module`**; with
`SourceRoot = _bmad-output`, GSD contributes **nothing at all** (and says so itself, via `ResolvePlanningRoot`'s
notice). The first clause is false in both directions.

NFR8 requires a displaced framework to read as a **stated boundary** rather than an unexplained gap. A boundary
stated inaccurately is not one — it is worse than the gap, because a reader who believes it will not go looking.

### 7.3 The same false sentence is in ADR 0038 §5

Not a code defect, but the same claim in the ratifying record. ADR 0041 §Supersedes corrects it.

---

## 8. Gate posture

This spike changes no rendering, so **no gate should move, and none was run** — there is nothing for
`check:parity`, `check:ir-content`, `check:tokens` or `check:assets` to observe in two markdown files.

Recorded for whoever implements FT-1…FT-5, because both blind spots bite there:

- **`check:parity` cannot see a C#-side change.** Its corpus IR is frozen; a change to region composition renders
  from the pinned input and the gate stays green (verified 2026-08-01 — a change removing an element from the
  shared nav left all 24 routes byte-identical).
- **`check:ir-content` cannot see markup only a non-BMad repo produces.** Story 12.2 §F1 measured this: all five
  `.milestone-band*` rules were pruned with the gate **green**, because the extraction corpus is this
  repository's IR and this repository is a BMad project. The seam is `CONDITIONAL_CLASSES` in
  `web/scripts/ir-content-lib.mjs`, pinned by `web/test/ir-content-harvest.test.mjs`.

**No new gate is proposed**, per ADR 0033's preference. Should a future story want one, its three preconditions
stand: localize failure to a named artifact, be scoped so a sibling story cannot turn it red, and be proven
deterministic across machines and CI operating systems before pinning.

---

## 9. Task 7 — proof the spike shipped nothing

**No new NuGet or npm dependency.** No new content-drift gate. No production code.

The probe harness (§ 1) lives at `$CLAUDE_JOB_DIR/tmp/probe/` — **outside the repository**, in the job scratch
directory. It is not tracked, not packaged, and appears in no File List.

`git status --short` and `git diff --stat` at close, from `worktree-story-4-9-dev` off `07bdb79`:

```
 M _bmad-output/implementation-artifacts/4-9-multi-framework-coexistence-strategy-spike.md
 M _bmad-output/implementation-artifacts/sprint-status.yaml
 M docs/adrs/README.md
?? _bmad-output/implementation-artifacts/4-9-spike-report.md
?? docs/adrs/0041-multi-framework-coexistence-policy.md
```

Scoped to the four production trees:

```
$ git status --short -- src/ tests/ web/ extension/
(no output)

$ git diff --stat HEAD -- src/ tests/ web/ extension/
(no output)
```

**Zero changes under `src/`, `tests/`, `web/`, `extension/`.**

The full test suite was **not** run, and this report does not imply it was. A spike that changes no code has no
obligation to run it; R9's known `FileWatcherServiceTests` load flake (Story 12.2 §F7) was therefore neither
encountered nor disproved.

### 9.1 The `sprint-status.yaml` edit was validated — and one thing a reviewer should not chase

The `sprint-status.yaml` edit was parsed with **YamlDotNet**, the same library the product uses. Before and after
this story's change the file yields the same **225** `development_status` keys, with only
`4-9-multi-framework-coexistence-strategy-spike` moving `ready-for-dev` → `review`; `epic-4` remains
`in-progress` and `epic-4-retrospective` remains `done`, deliberately.

Loading the file **whole** raises `SemanticErrorException` at line 82 — `story_location: {project-root}/…`, where
`{` opens a YAML flow mapping. **This is pre-existing and not a defect.** It reproduces byte-identically on the
unmodified file at `HEAD` (same line, same column), and `SprintStatusParser` never sees it: that class
deliberately **slices out the `development_status` and `action_items` blocks and deserializes each in isolation**,
precisely so a hand-authored file's unrelated top-level lines cannot break sprint parsing. Recorded here so a
reviewer who runs a whole-file YAML lint does not mistake it for damage from this story.

---

## 10. Handoffs — recorded, not adopted

Per CLAUDE.md § hunk attribution. Story 12.2 is still `review`, so its hunks are live for its own code review.

| # | Handoff | Owner |
|---|---|---|
| **H1** | `ForgeOptions.Resolve`'s walk-up comment says a repo with several markers at one level "resolves by `SourceDirNames` order **(BMad first)**". The array is `.planning → .gsd → .specify → _bmad-output`, so BMad probes **last**. The parenthetical contradicts both the array immediately above it and ADR 0038 §3. **Still present at `07bdb79`.** | Story 12.2 (`review`) |
| **H2** | `AppendNonPrimaryMarkerNotice`'s family-merging clause is measurably false (§ 7.2), as is the same sentence in ADR 0038 §5 | Story 12.2 (`review`) — ADR 0041 corrects the record half |
| **H3** | `GsdCoreArtifactAdapter.IngestSprint` and `ReportUnsupportedArtifacts` emit `Source`-anchored paths that escape the source root (§ 7.1) | Story 12.2 (`review`) |
| **H4** | **ADR 0040's index entry in `docs/adrs/README.md` says "Note 0039 was taken by Story 4.9 before this landed."** That is factually wrong: 0039 is `0039-runtime-attached-body-level-classes.md` (the sunburst verify round), and Story 4.9 had not been developed. Left uncorrected here — it is Story 16.1's hunk | Story 16.1 |
| **H5** | **ADR 0037 is still missing from `docs/adrs/README.md`'s index** — re-confirmed at `07bdb79` (`grep -c 0037` → 0). Story 12.2 §F5 reported it and deliberately left it; still open | the story that wrote ADR 0037 |

Two pre-existing issues were **not** chased, per the story's R9: the `FileWatcherServiceTests` load flake
(Story 12.2 §F7) and the `crossorigin`/`file://` unstyled-portal defect (Story 12.2 §F2, NFR-3-relevant).

---

## 11. What a reviewer should check first

1. **§ 3.2's zero-`Skipped` table.** If it is wrong, the policy in ADR 0041 §1–§2 is built on sand. It is
   reproducible from the harness described in § 1.
2. **§ 4.3's shape (c) result.** It is the only "actively wrong" verdict, and it drives FT-1, the
   highest-value follow-through.
3. **§ 5.3's Option C measurement.** Rejecting an obvious option on measurement is stronger than rejecting it on
   argument — but only if the measurement is right.
4. **§ 6's B1–B11 table** against `AdapterRegistry.cs` at `07bdb79`. Every verdict should be checkable by symbol.
5. **§ 0.1's number change.** ADR **0041**, not 0039. If a reviewer's notes still say 0039, they predate the
   collision.
