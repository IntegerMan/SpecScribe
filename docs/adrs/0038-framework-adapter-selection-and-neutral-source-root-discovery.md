# ADR 0038: Framework adapter selection, minimal multi-adapter merge, and framework-neutral source-root discovery

- **Status:** Proposed
- **Date:** 2026-08-06
- **Deciders:** Owner (Matt Eland)
- **Context story:** [Story 12.2](../../_bmad-output/implementation-artifacts/12-2-gsd-core-baseline-adapter-coverage.md)
  (owner decisions D4 and D5), the first story to make a non-BMad repository generate at all.
- **Inherited by:** Epics 11 (Spec Kit), 12.3 (GSD Pi), 13, 14 and 15. **This is the ONE registry decision** —
  those stories implement adapters against it and must not propose a second.

## Context

SpecScribe's ingestion seam has been named since Story 4.1: `IArtifactAdapter` turns one framework's source tree
into a normalized `ArtifactBundle`, and everything downstream consumes that one model. The seam was real. What
never arrived was anything that *used* it for more than one framework:

- **`SiteGenerator` held a single hardcoded field** — `private readonly BmadArtifactAdapter _adapter = new();` —
  whose own comment said the registry "arrives with Stories 4.3+". Those stories were relocated into Epics 11–15
  during planning, so the promise lost its owner and the field stayed.
- **`ForgeOptions.SourceDirName` was the literal `"_bmad-output"`**, and `Resolve` walked up from the current
  directory looking for a directory containing it, throwing `DirectoryNotFoundException` when there was none.

The second is the sharper one. **A pure GSD Core repository failed before `AppliesTo` was ever called.** No
adapter, however complete, could have helped: the run aborted while resolving paths. A related coupling brands the
site — `ReadProjectName` reads `_bmad/config.toml` and otherwise falls back to `DefaultSiteTitle = "BMad Live
Docs"`, so even a successful non-BMad run would have published a portal named after a framework the project does
not use.

Story 12.1's spike found both gaps and could not close them; Story 12.2's owner declined to split them into a
separate story, so it owns both, plus this record.

A third constraint shaped the answer. **`ForgeOptions.SourceRoot` is single-valued**, and it anchors *two* things:
the `*.md` enumeration that discovers every renderable document, and `ToSourceRelative`, which computes every
source-relative path as `Path.GetRelativePath(SourceRoot, fullPath)`. A path that resolves outside that root
relativizes to `..\…`, which `PathUtil.EscapesRepoRoot` exists to reject. So merging two frameworks at the
**bundle** level is cheap, and merging them at the **file-discovery** level is not.

## Decision

### 1. Adapter selection is an ordered registry, and EVERY matching adapter runs

`AdapterRegistry` holds an ordered `IReadOnlyList<IArtifactAdapter>`, runs `AppliesTo` on each, and ingests
through **all** that match — not the first. `AppliesTo` is a boolean per adapter and a real repository can
legitimately answer yes twice: the reference GSD Core project plans in BMad and delivers in GSD, carrying
`_bmad/`, `_bmad-output/` **and** `.planning/`. Picking one winner would discard real artifacts.

Order is **framework-specific markers first, `BmadArtifactAdapter` last**. BMad is also the **no-match fallback**:
a repository matching nothing — a bare `_bmad-output` tree with no install — still ingests through BMad, which is
exactly what the generator did before the registry existed.

### 2. The merge is minimal, first-non-null-wins, and never silent

| Family | Rule |
|---|---|
| `Epics`, `Requirements`, `EpicsSourceFullPath` | claimed **together** by the first adapter that FOUND an epics source |
| `Sprint` | first non-null wins |
| `Module` | first with a real detected identity wins; ties resolve to the first |
| `Retros`, `Diagnostics` | concatenated in adapter order |
| `StoryArtifactsById` | union; a duplicate key is a `Skipped` diagnostic |
| `ConsumedSourceRelatives` | union |

Every dropped contribution emits a `Skipped` diagnostic naming the adapter and the family. A multi-adapter run
additionally emits **one** `Informational` notice naming which adapters matched and which supplied each family.

The epics family is claimed **as a unit** rather than field-by-field, which is the one refinement on decision D5's
table. Requirements roll up from the same file as the epics, so a bundle carrying adapter A's source path beside
adapter B's parsed model is not merely odd — it is incoherent.

**A single-adapter run returns that adapter's bundle verbatim**, the same instance. No cross-adapter diagnostic
fires. This is deliberate: a notice that appeared on every existing BMad project would itself be the regression.

### 3. Source-root discovery probes an ordered marker set — install markers before BMad's output folder

`ForgeOptions.SourceDirNames` replaces the single literal:

```
.planning  →  .gsd  →  .specify  →  _bmad-output
```

The walk-up is unchanged in shape: **nearest directory wins**, then marker order within it.

`_bmad-output` probes **last**, and this is the ADR's most consequential detail. It is an *output* folder — a BMad
project writes one whether or not another framework is present — whereas `.planning`, `.gsd` and `.specify` are
framework *install* markers. Probing the output folder first makes it a universal winner. In the reference
repository, `_bmad-output` holds six planning documents while `.planning` holds 168 files, 11 phases and 58 plans;
resolving to the former would put every GSD artifact outside the source root, where its paths cannot be expressed
at all.

Ordering by specificity **costs nothing** in the case that matters for compatibility: a BMad-only repository — this
one included — has none of the other markers, so it resolves to `_bmad-output` exactly as it always did.

Two couplings move with it: the "could not locate a repo root" message now names the whole marker set and says
"spec-driven project" rather than "BMad project", and the site title falls back to the source root's own
`PROJECT.md` H1 and then to the repo directory name for a non-BMad root. `_bmad/config.toml` remains the first
probe and the BMad fallback is untouched. The tolerant `requireSource: false` path (the webview/extension
contract) is unchanged.

### 4. The scoped epics re-ingest moves ONTO the adapter contract

`IArtifactAdapter` gains `IngestEpics`, returning a now top-level `EpicsIngest` record.

This is the watch constraint, resolved explicitly. `SiteGenerator` held the **concrete** `BmadArtifactAdapter`
rather than the interface *solely* because `RegenerateEpics` needs the scoped epics/story/requirements re-parse
without touching the sprint/retro/module state it never refreshes (AD-5). A registry handing back a bare
`IArtifactAdapter` would have broken watch-mode incremental regeneration, or forced it to degrade to a full
re-ingest.

Of the two routes Story 12.2 considered — lift it onto the interface, or degrade for adapters that lack it —
**lifting is chosen**, because it makes the guarantee structural instead of measured: BMad's implementation is the
same method body it always had, invoked at the same call site, so watch output for a BMad repository is
byte-identical **by construction**. ADR 0027 defines "safe" as proven byte-identical to a full rebuild; this
satisfies it without a measurement that could drift.

### 5. What this decision deliberately does NOT settle

**Multi-rooted source discovery is out of scope, and is [Story 4.9](../../_bmad-output/planning-artifacts/epics.md)'s
AC #2.** In a repository carrying two frameworks, the non-primary framework's artifact families still merge into
the bundle, but its **loose documents do not render as their own pages** — they are outside the single source
root. That is the accepted cost of bundle-level merging, and the registry states it as an `Informational` notice
naming the marker that did not become primary, so it reads as a stated boundary rather than a silent gap (NFR8).

The **strategic** multi-framework policy — how SpecScribe should present a project that genuinely uses two
methodologies, and whether source discovery should become multi-rooted — is Story 4.9's, deliberately deferred.

Also unsettled, and named rather than quietly done: **`ModuleContext` is not widened.** It is BMad-typed to a
closed `BmadModule` enum keyed on `_bmad/{code}/`, so a non-BMad adapter can only return `ModuleContext.None`, and
the About-SDD matrix's "Planning docs" and "Commands" columns are therefore unfillable for any non-BMad framework.
This is a **ceiling stated on each framework page**, not unfinished work. The same applies to **ADR 0020**'s gate
for reading a non-markdown source, whose presence condition is `ModuleContext.IsModulePresent` — also BMad-keyed,
which is why GSD's `config.json` is reported as uninterpreted rather than read. Widening either is a real question
for a later story.

## Consequences

**Good.**

- A non-BMad repository generates. That was structurally impossible before, at two independent layers.
- Adapter authors in Epics 11/12.3/13/14/15 implement one interface against one selection rule and inherit this
  record; none needs its own registry decision.
- Every merge loss is visible. Nothing is averaged and nothing is silently overwritten.
- The BMad path is unchanged at each of the three seams by construction rather than by comparison: identity merge
  for one adapter, unchanged marker resolution for a repo with only `_bmad-output`, and the same watch method body.

**Costs, accepted.**

- A two-framework repository renders one framework's loose documents, not both. Diagnosed, not hidden.
- `IArtifactAdapter` is a wider contract: an adapter must implement the scoped slice even where its full ingest
  would have sufficed. For an epics-centric framework this is a few lines; it buys the AD-5 guarantee.
- Marker order is now a load-bearing decision. A future framework whose marker is a common directory name would
  need care about where it sits in `SourceDirNames`.

**Supersedes.** The `SiteGenerator` field comment promising a registry "with Stories 4.3+", and
`ForgeOptions.SourceDirName` as the single source-discovery literal. `SourceDirName` itself remains as the BMad
constant, referenced by `SourceDirNames` and by `ProgressCalculator`'s git-path join.

**No new gate.** Per ADR 0033, a new content-drift gate must localize failure to a named artifact, be scoped so a
sibling story cannot turn it red, and be proven deterministic before pinning. None was needed here: the behaviour
this ADR governs is covered by unit tests over the registry, the adapters and the epics-index renderer.
