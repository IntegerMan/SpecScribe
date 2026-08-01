---
title: 'Configurable source-root resolution and legacy BMad (v4/v5) dataset support'
type: 'feature'
created: '2026-08-01'
status: 'draft'
review_loop_iteration: 0
context:
  - '{project-root}/docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** SpecScribe hardcodes `_bmad-output` as the only source root (`ForgeOptions.SourceDirName`) and hardcodes BMad v6.10's file conventions as the only artifact shape. Three real datasets therefore fail: a v6 repo whose installer wrote a different `output_folder` (SpecScribe never reads that key, even though it already parses the same config file for `project_name`); an early-v6 repo written before the `planning-artifacts`/`implementation-artifacts` split (`output_folder: docs`, stories under `docs/stories`); and a BMad v4/v5 repo (`.bmad-core/` + `docs/prd.md` + `docs/stories/1.2.story.md`), which today resolves to nothing at all — and even when pointed at `docs/` by hand renders a site with zero epics, stories, or requirements.

**Approach:** Two coupled halves. **(A) Resolution** — replace the single-name walk-up with an ordered chain: explicit `--source` → `.specscribe` → the installed BMad config's declared output folder → a new-then-legacy probe; report which leg won as first-class provenance. **(B) Ingestion** — add a second `IArtifactAdapter` for the v4/v5 layout behind a real adapter registry, which first requires promoting the artifact-shape predicates that ~20 call sites currently reach for as `BmadArtifactAdapter` statics.

## Boundaries & Constraints

**Always:**
- **Precedence is absolute and one-directional:** explicit `--source` > `.specscribe` `Source` > config-declared `output_folder` > probe. A later leg never overrides an earlier one.
- **`RepoRoot` is the anchor directory that held the framework marker**, never `Path.GetDirectoryName(sourceRoot)`. A nested declared folder (`output_folder: "{project-root}/docs/specs"`) currently yields `RepoRoot = <repo>/docs`, silently corrupting git metrics, README discovery, `_bmad/` module detection, code-map roots, and the ADR probe. An explicit `--source` keeps today's parent-derivation only when no marker is found up-tree.
- **Every repo-relative git key built from the source root must use the resolved root**, not the `_bmad-output` literal. `ProgressCalculator.cs:92` and `DeliveryCadence.cs:110` both compose `"{SourceDirName}/{sourceRel}"`; under any other root they silently return null — story "last updated" degrades to the change-log date and done stories drop out of cycle-time with no error.
- **Detection states itself.** The chosen root, the leg that chose it, and the detected framework family+version land on the Diagnostics page and in `--show-config`. Never guess silently.
- **Adapters never throw.** Unsupported shapes degrade to absent surfaces with a categorized `AdapterDiagnostic`, per the existing NFR8 discipline.
- **v4 and v5 parse identically.** Their `core-config.yaml` differs only by v4's extra `qa:` block; treat v5 as v4 and record the distinction only in the reported version string.
- Tests are xUnit, real temp fixtures via `Directory.CreateTempSubdirectory`, matching `BmadArtifactAdapterTests` / `ForgeOptionsTests` conventions.

**Ask First:**
- **Any change to `--show-config`'s existing field keys or origin tokens.** `SettingsResolver.cs:75-87` documents them as a stable CI contract. *Adding* origin values is fine; renaming or removing one is not.
- **If the golden content fingerprint moves.** Per CLAUDE.md, establish causality before regenerating — audit the normalizer first and confirm stability across two runs.
- **A third source-root probe candidate** beyond `_bmad-output` and `docs`.
- **Promoting `IngestEpics` onto `IArtifactAdapter`** if it forces a breaking change to the incremental/watch contract rather than an additive one.

**Never:**
- Never change what a default BMad v6.10 repo resolves to. This repository's own resolution, and its generated output, must be unchanged.
- Never write into a `.bmad-core/` or legacy `docs/` tree. Legacy sources are strictly read-only.
- Never remove, rename, or repurpose `--source`.
- Never `git reset --hard`, `git checkout --`, or `git clean` (shared `main`; concurrent sessions).
- Out of scope: ingesting v4 QA gates (`docs/qa/gates/*.yml`) or assessments; migrating a v4 repo to v6; any framework that is not BMad.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Default v6 (this repo) | `_bmad/` + `_bmad-output/` present, no `--source` | Resolves `_bmad-output`; origin reported as config-declared; output byte-identical to today | N/A |
| Relocated v6 | `_bmad/config.toml` `[core] output_folder = "{project-root}/artifacts"` | Resolves `<repo>/artifacts`; `RepoRoot` stays the repo root | N/A |
| Nested declared folder | `output_folder = "{project-root}/docs/specs"` | Source root `docs/specs`, `RepoRoot` = repo root; git keys use `docs/specs/...` | N/A |
| Declared folder missing | `output_folder` names a directory that does not exist | Fall through to probe; `Informational` diagnostic naming the declared-but-absent path | Never fatal |
| Early v6 (flat) | `_bmad/core/config.yaml` `output_folder: docs`, stories in `docs/stories` | Resolves `docs`; epics parse (filename-anywhere); story artifacts resolve via the early-v6 story location | Missing `implementation-artifacts/` is not an error |
| BMad v4/v5 | `.bmad-core/core-config.yaml`, `docs/prd.md`, `docs/stories/1.2.story.md` | Legacy adapter selected; epics/stories/requirements render; `Sprint = null` so sprint surfaces omit; framework reported as v4/v5 with its manifest version | Unparseable story → `Malformed` diagnostic, siblings still land |
| v4 story status | `## Status` heading, value on the next line (`Approved`, `Ready for Review`) | Mapped to the canonical lifecycle; unmapped values render as unrecognized | `Unsupported` diagnostic per unmapped value |
| Both markers present | `_bmad/` and `.bmad-core/` both exist | v6 wins; one `Informational` diagnostic naming the ignored legacy install | N/A |
| No marker anywhere | Neither marker up-tree, `requireSource: true` | Actionable `DirectoryNotFoundException` naming every candidate probed | Existing tolerant path (`requireSource: false`) still degrades, not throws |
| Explicit `--source` at a legacy tree | `--source ./docs` in a `.bmad-core` repo | Explicit path wins; legacy adapter still selected by marker sniff | N/A |

</frozen-after-approval>

## Code Map

**Resolution (half A)**
- `src/SpecScribe/ForgeOptions.cs:87,130-206` -- `SourceDirName` const and the single-name walk-up; the `repoRoot = Path.GetDirectoryName(sourceRoot)` corruption is at `:151`. `ResolveAdrSourceRoot` (`:238`) is the existing, working precedent for an ordered probe.
- `src/SpecScribe/SettingsResolver.cs:8-18,136-159,183-198,212-217` -- `ConfigSource` closed enum, `BuildProvenance`, `FormatConfigLines`. Field keys are a documented CI contract at `:75-87`.
- `src/SpecScribe/SiteSettings.cs:10-12,121,129` -- `--source` option and both resolve wrappers.
- `src/SpecScribe/ProgressCalculator.cs:92`, `src/SpecScribe/DeliveryCadence.cs:110` -- git keys hardcoded to `{SourceDirName}/`. **Silent-degradation defect under any other root.**
- `src/SpecScribe/CodeMap.cs:187`, `src/SpecScribe/AdrLinkRewriter.cs:33,37`, `src/SpecScribe/SourceLinkifier.cs:13` -- bare `"_bmad-output"` literals (exclusion filter, ADR link rewrite, `[Source: …]` citation regex).
- `src/SpecScribe/Commands.cs:463,721-851` -- `SourceDirDefault` for the VS Code shim; interactive "Configure paths" (note `:725` persists the auto-discovered default as if explicit).
- `src/SpecScribe/FileWatcherService.cs:60-66,79-95` -- creates and watches `SourceRoot`; the `_bmad/` watcher is built from `RepoRoot`.
- `extension/src/extension.ts:789,801` -- bootstrap glob `_bmad-output/**` and the `payload.sourceRoot ?? '_bmad-output'` fallback.

**Ingestion (half B)**
- `src/SpecScribe/IArtifactAdapter.cs:11,27,37` -- the two-member contract. `AppliesTo` has **zero production callers** today.
- `src/SpecScribe/SiteGenerator.cs:61-63,398,1225` -- `private readonly BmadArtifactAdapter _adapter = new();` typed concrete *because* `RegenerateEpics` needs `IngestEpics`. **There is no registry.**
- `src/SpecScribe/BmadArtifactAdapter.cs:17,23,30,34,40,47` -- the statics (`EpicsFileName`, `ImplementationArtifactsDirName`, `SprintStatusFileName`, `IsEpicsFile`, `IsUnderImplementationArtifacts`, `IsSprintStatusFile`). **These predicates are the real port.** Reached by `SiteNav.cs:293`, `ArtifactCoverage.cs:110,263`, `DashboardViewBuilder.cs:27`, `ChangeSurfaceFileResolver.cs:40`, `FollowUpRefs.cs:87`, `WorkInventory.cs:45`, and `SiteGenerator.cs:947,974-975,1271-1278,2425-2427,4620,4682,5121,5262,5496,5767,5815`.
- `src/SpecScribe/EpicsParser.cs:95-99` -- `ExtractStatus` matches line-start `Status:`; v4's `## Status` heading yields null, un-statusing every v4 story.
- `src/SpecScribe/ModuleContext.cs:335-349` -- returns `None` unless `{repoRoot}/_bmad` exists; no extension point.
- `src/SpecScribe/AdapterDiagnostic.cs:12-48` -- categories plus `DiagnosticAnchorRoot` (already supports anchoring at repo root, which is what `.bmad-core/` needs).
- `tests/SpecScribe.Tests/{ForgeOptionsTests,SettingsResolverTests,SettingsStoreTests,BmadArtifactAdapterTests,SiteGeneratorAdapterTests}.cs` -- the fixtures to extend.

## Tasks & Acceptance

**Execution:**

*Half A — resolution*
- [ ] `src/SpecScribe/BmadInstall.cs` (new) -- Detect and describe the installed framework: v6 via `_bmad/_config/manifest.yaml` (`installation.version`) reading `output_folder` from `_bmad/config.toml` `[core]` then `_bmad/core/config.yaml`; v4/v5 via `.bmad-core/core-config.yaml` (camelCase `prd.prdFile`, `prd.prdShardedLocation`, `prd.epicFilePattern`, `architecture.architectureFile`, `devStoryLocation`) with the version from `.bmad-core/install-manifest.yaml`. Resolve `{project-root}` placeholders. Never throws. -- One place that knows how each BMad generation declares itself, so neither resolution nor the adapters re-sniff.
- [ ] `src/SpecScribe/ForgeOptions.cs` -- Replace the walk-up with the ordered chain; anchor `RepoRoot` on the marker directory; surface the outcome (family, version, chosen root, winning leg, candidates probed) as a new required-with-default property. Keep `SourceDirName` as the first probe candidate. -- The resolution spine; every other task reads its output.
- [ ] `src/SpecScribe/SettingsResolver.cs` -- Add `ConfigSource` members for the config-declared and probed legs; carry the outcome through `BuildProvenance`; extend `FormatConfigLines` **additively** with the detected family/version. -- Provenance must name the new legs or `--show-config` lies.
- [ ] `src/SpecScribe/ProgressCalculator.cs` + `src/SpecScribe/DeliveryCadence.cs` -- Build the git key from the resolved source root relative to `RepoRoot`. -- Fixes the silent story-date and cycle-time loss under any non-default root.
- [ ] `src/SpecScribe/CodeMap.cs` + `src/SpecScribe/AdrLinkRewriter.cs` + `src/SpecScribe/SourceLinkifier.cs` -- Drive the exclusion filter, ADR link rewrite, and citation regex from the resolved root, keeping `_bmad-output` as the default. -- Otherwise citations and code-map exclusions break on a relocated root.
- [ ] `extension/src/extension.ts` -- Derive the bootstrap watcher glob and fallback from the payload's `sourceRoot`. -- The extension must not re-hardcode what the host now resolves.

*Half B — ingestion*
- [ ] `src/SpecScribe/ArtifactConventions.cs` (new) -- Promote `IsEpicsFile`/`IsUnderImplementationArtifacts`/`IsSprintStatusFile` and their names into a per-adapter convention object exposed on `IArtifactAdapter`; keep the `BmadArtifactAdapter` statics as thin delegating shims so no call site breaks in this task. -- The ~20 static call sites are the actual blocker; nothing else in half B is safe until they can be re-pointed.
- [ ] `src/SpecScribe/IArtifactAdapter.cs` -- Add the conventions property and promote a scoped epics re-ingest (today's concrete `IngestEpics`) onto the interface. -- `SiteGenerator.RegenerateEpics` is the reason the field is typed concrete.
- [ ] `src/SpecScribe/ArtifactAdapterRegistry.cs` (new) -- Ordered `AppliesTo` selection, v6 before legacy, exactly one winner, an `Informational` diagnostic when a losing candidate also applied, and a documented no-match fallback. -- Gives `AppliesTo` its first production caller.
- [ ] `src/SpecScribe/LegacyBmadArtifactAdapter.cs` (new) -- v4/v5 ingest: epics and requirements from `docs/prd.md` or its sharded `docs/prd/epic-{n}*.md` form; stories from `devStoryLocation` accepting both `{epic}.{story}.story.md` and `{epic}.{story}.{slug}.md`; `Sprint = null`; retros absent. -- The v4/v5 dataset itself.
- [ ] `src/SpecScribe/LegacyBmadParsers.cs` (new) -- v4 PRD epic/story sections (`## Epic {n}`, `### Story {e}.{s}`), `- FR1:`/`- NFR1:` bullets under `## Requirements`, and a `## Status`-heading status reader. Map v4 statuses (`Draft`, `Approved`, `InProgress`, `Review`, `Ready for Review`, `Done`) to the canonical lifecycle, emitting `Unsupported` for anything unmapped. -- `EpicsParser`/`RequirementsParser` are hard-bound to v6 shapes; `TaskListParser` is already neutral and must be reused, not duplicated.
- [ ] `src/SpecScribe/ModuleContext.cs` -- Add a `.bmad-core` identity path returning a named legacy context instead of `None`. -- Otherwise a v4 site reports "Unknown (not detected)" on every module surface.
- [ ] `src/SpecScribe/SiteGenerator.cs` -- Select the adapter through the registry; route the ~20 predicate call sites through the selected adapter's conventions. -- The integration point; do it last so each prior task lands green.
- [ ] `tests/SpecScribe.Tests/` -- Cover every I/O Matrix row: `ForgeOptionsTests` (precedence, nested folder `RepoRoot`, missing declared folder, both markers, no marker), `SettingsResolverTests` (new origins, existing keys unchanged), new `LegacyBmadAdapterTests` + a v4 fixture, and a `SiteGeneratorAdapterTests` case pinning that the v6 output inventory is unchanged. -- The regression guarantee for "default v6 resolves identically".
- [ ] `docs/adrs/` -- Propose an ADR covering the resolution precedence chain, the `RepoRoot` anchoring rule, and the adapter-registry/conventions seam. -- CLAUDE.md requires an ADR for cross-cutting contract changes rather than burying them in a story note.
- [ ] `README.md` -- Add BMad v4/v5 to the supported-frameworks table with its honest coverage boundary (no sprint board, no QA gates). -- The table is the stated support contract.

**Acceptance Criteria:**
- Given this repository unchanged, when `specscribe generate` runs, then the generated output is byte-identical to the pre-change run and the golden content fingerprint is unmoved.
- Given a v6 repo whose `output_folder` is nested two levels deep, when generation runs, then git-derived story dates and cycle-time values are populated — proving `RepoRoot` and the git key prefix are both correct.
- Given a BMad v4 repo with no `--source` and no `.specscribe`, when generation runs, then the source root resolves from `.bmad-core/core-config.yaml` and the site renders epics, stories, and requirements.
- Given any resolution, when `--show-config` runs, then it names the winning leg and the detected framework family and version, and every pre-existing field key and origin token is unchanged.
- Given a repo with both `_bmad/` and `.bmad-core/`, when generation runs, then v6 is selected and one `Informational` diagnostic names the ignored legacy install.
- Given a v4 story whose status is unmapped, when generation runs, then the story renders as unrecognized with an `Unsupported` diagnostic and no sibling story is lost.

## Design Notes

**Why `RepoRoot` anchoring is load-bearing, not tidiness.** `ForgeOptions.cs:151` derives `RepoRoot` from the source root's parent. That is correct only because `_bmad-output` is a direct child of the repo root. The moment `output_folder` is nested, `RepoRoot` becomes a subdirectory and roughly twenty consumers silently misbehave — git runs in the wrong directory, `README.md` is not found, `_bmad/` module detection fails, the code map roots at the wrong level, and `ResolveAdrSourceRoot` probes `docs/docs/adrs`. Anchoring on the marker directory fixes all of them at once.

**Why the statics come before the second adapter.** `IArtifactAdapter` is only two members, so it looks like a clean seam. It is not the real one. `SiteNav` decides whether to show the Epics nav entry by calling `BmadArtifactAdapter.IsEpicsFile`; `ArtifactCoverage`, `WorkInventory`, `FollowUpRefs`, `DashboardViewBuilder`, and a dozen `SiteGenerator` sites do the same. A legacy adapter that produces a perfect `ArtifactBundle` would still be invisible to every one of them. Promoting the predicates first, with the statics left as delegating shims, keeps that task green on its own and makes the registry task a re-pointing exercise rather than a rewrite.

**Legacy status vocabulary.** The v4 story template enumerates `Draft, Approved, InProgress, Review, Done`, but the v4 dev agent writes `Ready for Review`, which is not in that enum. Parse leniently and map into the existing canonical lifecycle; follow the Story 8.2 precedent of reporting unmapped values as `Unsupported` rather than silently normalizing them.

## Verification

**Commands:**
- `dotnet build SpecScribe.slnx` -- expected: zero warnings, zero errors. Do not pipe the output; a pipe returns *tail's* exit status and masks failure.
- `dotnet test` -- expected: full suite green, including the unchanged-v6-inventory pin. Stop any running preview server first — a live server starves git spawns and produces a rotating one-test "flake".
- `dotnet run --project src/SpecScribe -- generate --show-config` -- expected: names the winning resolution leg and the detected family/version; every pre-existing field key present and unchanged.
- `dotnet run --project src/SpecScribe -- generate` -- expected: writes to `SpecScribeOutput/`; diff against a pre-change run is empty.

**Manual checks:**
- Build a throwaway v4 fixture in the scratchpad (`.bmad-core/core-config.yaml`, `docs/prd.md` with two epics, three `docs/stories/*.story.md`), generate against it, and open the site in a browser. Confirm epics, stories, and requirements render; the sprint board is *absent* rather than empty-and-broken; and the Diagnostics page names the detected framework and version.
- Confirm the golden content fingerprint is stable across two consecutive full (non-incremental) runs before trusting it. Assets are embedded resources — an incremental build reuses the cached assembly and reports a stale hash.
