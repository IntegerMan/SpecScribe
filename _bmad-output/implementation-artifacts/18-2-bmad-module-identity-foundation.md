---
baseline_commit: 86b35c267241c15b05c64e3aaa3e13cce58198b2
---

# Story 18.2: BMad Module Identity Foundation

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a team using any BMad module other than BMM,
I want SpecScribe to identify my module correctly — or admit honestly that it does not model it —
so that the portal never asserts a vocabulary my project does not use, and installing a second module never silently degrades the module I already had.

## Why this story exists (read first)

**This story fixes two defects that exist in `main` right now.** It is not new-module coverage, and it is not
speculative hardening. Story 18.1's spike set out to write an artifact coverage map and found that the first
unit of work in Epic 18 is a module-**identity** bug that is already misreporting on module combinations that
ship today.

`ModuleContext.BuildContext` derives the module from the leading token of a skill id
[ModuleContext.cs:346-348]:

```csharp
var module = prefix.StartsWith("gds", StringComparison.OrdinalIgnoreCase)
    ? BmadModule.GameDevStudio
    : BmadModule.BmadMethod;
```

Every first-party BMad module **except GDS** prefixes its skills `bmad-`. **GDS is correct only by
coincidence** — it is the single module whose skill prefix happens to equal its module code. Verified against
the real `module-help.csv` of each module repo (not doc-site prose):

| Module | code (install dir) | Real skill ids | `prefix` | Identified as |
|---|---|---|---|---|
| BMad Method | `bmm` | `bmad-create-story` | `bmad` | BmadMethod ✅ |
| Game Dev Studio | `gds` | `gds-gdd`, `gds-create-story` | `gds` | GameDevStudio ✅ |
| Creative Intelligence Suite | `cis` | `bmad-cis-innovation-strategy` | `bmad` | **BmadMethod ❌** |
| Test Architect (Enterprise) | `tea` | `bmad-testarch-trace` | `bmad` | **BmadMethod ❌** |
| BMad Builder | `bmb` | `bmad-bmb-setup` | `bmad` | **BmadMethod ❌** |

**Defect A — false presence.** A CIS/TEA/BMB-only repo reports `Module = BmadMethod` and is served BMM's full
ten-term glossary. NFR8 requires surfaces to be *"absent, not broken or misleadingly empty"*; this is the
fourth case — confidently wrong.

**Defect B — a live regression to shipped BMM support.** `ChoosePrimary` must return one winner and, among
non-GDS candidates, returns the first in **installed-manifest order**. With `cis` or `tea` ahead of `bmm`, BMM
is demoted and **every** BMM command suggestion disappears portal-wide — while `IsMethodPresent` still returns
`True`, so About-SDD simultaneously reports "BMad — Supported, Detected." Install-order dependent, therefore
intermittent, and it fires the first time an owner adds TEA or CIS to an existing BMM project.

**Scope note (owner-approved 2026-07-25).** This story was formerly *"Priority BMad Module Baseline Coverage."*
Its artifact-coverage ACs moved verbatim to the new **Story 18.5**, which this story gates. `epics.md` and
`sprint-status.yaml` were both updated in that change; the sprint key changed from
`18-2-priority-bmad-module-baseline-coverage` to `18-2-bmad-module-identity-foundation`.

## Acceptance Criteria

1.
**Given** a repository whose installed BMad module is not one SpecScribe models (for example `cis`, `tea`, `bmb`, or a BMad Builder-generated custom module)
**When** generation runs
**Then** the module is identified from its module **code** — the `_bmad/{code}/` directory name — rather than from a skill-id prefix, and resolves to an unmodeled identity that carries its real module label and its parsed command catalog while publishing **no** planning docs and **no** glossary
**And** neither the how-to-read glossary nor the site-wide abbreviation expansion presents BMad Method's vocabulary for it
**And** the situation is reported once as a non-fatal `Informational` diagnostic naming the code and label.

2.
**Given** a repository with more than one BMad module installed (for example BMM alongside Test Architect or the Creative Intelligence Suite)
**When** primary-module selection runs
**Then** BMad Method and Game Dev Studio are never demoted below an auxiliary module by installed-manifest ordering, so an existing BMM repository keeps its planning docs, glossary and next-step commands intact after a second module is installed
**And** the About-SDD "Detected" reporting and the selected primary module never contradict each other.

3.
**Given** the modules SpecScribe already supports
**When** the identity change lands
**Then** BMad Method and Game Dev Studio detection, docs, glossary and commands are unchanged, verified against **real** module `module-help.csv` content rather than synthetic fixtures
**And** the existing test suite and the golden byte-parity gate stay green (or any intentional change is re-baselined).

[Source: `_bmad-output/planning-artifacts/epics.md` § Story 18.2 (rewritten 2026-07-25)]

## Owner design decision (elicited at create-story — do not re-litigate)

**The unmodeled state is a *named acknowledgement*, not a silent omission.** Where the glossary would render,
`how-to-read.html` states which module was detected and that SpecScribe does not publish vocabulary for it yet:

> This project uses the **Test Architecture Enterprise** module. SpecScribe doesn't publish a glossary for it yet.

Rationale the owner chose this over silent omission: an invisible section leaves a TEA user unable to tell
whether the portal is honest or broken. Use the module's real `CommandCatalog.ModuleLabel` (which already comes
through correctly — see Finding 2 below), never the raw code.

Constraints on the treatment:
- It replaces the glossary **section body**, so the `<h2 id="glossary">` heading and its anchor should still
  render — otherwise in-page links to `#glossary` break.
- One short sentence. Do not stack an explanation, a link to About-SDD, and a call to action; this renders on a
  page a first-time visitor reads.
- It must not read as an error. This is a supported state, not a failure — tone matches
  `AdapterDiagnosticCategory.Informational`'s "FYI, nothing to do" framing.
- **Not** shown when no module is detected at all (`ModuleContext.None`). That case keeps today's behavior:
  the whole section is omitted, because there is no module to name.

## Context & Scope

### What is already generic (do not rebuild it)

Story 18.1 mapped the generic/hardcoded seam precisely. It sits *inside* `BuildContext`, not between it and the
doc tables — this matters, because it means the fix is small and surgical:

| Layer | Generic? |
|---|---|
| Install discovery — `_bmad/_config/manifest.yaml` via `ReadInstalledModules`, with an `_bmad/*/module-help.csv` disk fallback [ModuleContext.cs:204-218] | ✅ Fully generic; `core` correctly excluded |
| `IsModulePresent(repoRoot, code)` [ModuleContext.cs:174-188] | ✅ Already takes an **arbitrary** code — only the two public wrappers are hardcoded |
| CSV → `byStep` + `ModuleLabel` [ModuleContext.cs:291-345] | ✅ Generic — `ModuleLabel` resolves **correctly** for CIS/TEA/BMB today |
| **Module identification** [ModuleContext.cs:346-348] | ❌ **The bug.** A closed binary with no `Unknown` branch |
| `DocsFor` / `GlossaryFor` switches [ModuleContext.cs:118-123, 151-156] | ❌ Hardcoded per enum case |
| `ChoosePrimary` [ModuleContext.cs:259-286] | ⚠️ Directory-name based, then **manifest order** — Defect B |

**Load-bearing detail:** `BmadModule.Unknown` exists but `BuildContext` **never returns it**. Detection either
succeeds or falls through to `BmadMethod`. `ModuleContext.None` is reachable only when *no* CSV parses at all —
never when a well-formed *foreign* module CSV parses.

### Blast radius — what is and is not affected

Verified by tracing consumers. Do not widen the fix beyond this:

| Surface | Affected | Why |
|---|---|---|
| Module docs in nav / quick links | **No** | `SiteNav.cs:206-215` skips any `ModuleDoc` with no filename match on disk. A CIS repo has no `prd.md`, so no phantom link. Self-limiting. |
| "Next Steps" command panels | **No — already correct** | Every `Command()` lookup misses → `null` → ~40 call sites omit. Honest NFR8 degradation. |
| Glossary on `how-to-read.html` | **YES** | `HowToReadTemplater.cs:176` gates only on `glossary.Count == 0` |
| Every rendered page | **YES** | `SiteGenerator.cs:4270` runs `AbbreviationExpander.Expand(html, _module.Glossary)` site-wide |
| About-SDD "Detected" badge | Consistent today | `IsMethodPresent`/`IsGdsPresent` are independent and correct; a CIS-only repo shows neither as Detected |

### On-disk facts that constrain the fix

- **`module.yaml` is an installer-*source* file and is NOT installed.** It carries a clean `code:`/`name:` and
  is tempting, but it does not exist in a consuming repo. Confirmed: this repo's `_bmad/bmm/` contains **only**
  `config.yaml` and `module-help.csv`.
- **`_bmad/{code}/config.yaml` carries no module identity** (just `user_name`, `output_folder`, path settings).
  SpecScribe reads no module `config.yaml` at all today — and this story does not add that (it is Story 18.5's
  prerequisite for TEA's `test_artifacts` key).
- **Therefore the only on-disk identity signals are the `_bmad/{code}/` directory name and the `module` column
  of `module-help.csv`.** The directory name is the module code and is already what `ChoosePrimary` and
  `IsModulePresent` key on.

### Real module codes and labels (verified against each module's own repo)

Note BMad's own label drift — trust `module-help.csv`'s `module` column, since that is what ships on disk:

| code | `module-help.csv` label (on disk) | `module.yaml` `name:` (not installed) |
|---|---|---|
| `bmm` | BMad Method | — |
| `gds` | Game Dev Studio | `BMGD: BMad Game Dev Studio` |
| `cis` | Creative Intelligence Suite | `CIS: Creative **Innovation** Suite` |
| `tea` | Test Architecture Enterprise | `Test Architect` |
| `bmb` | BMad Builder | `BMad Builder` |

> **Do not "fix" GDS.** BMad's docs advertise its commands as `/bmgd-*`, which would break a `gds` check — but
> the real `module-help.csv` uses `gds-*` throughout and `module.yaml` says `code: gds`. **BMGD is branding.**
> Current GDS support is correct; AC #3 exists to keep it that way.

## Tasks / Subtasks

- [x] **Task 1 — Reproduce both defects before changing anything (AC: #1, #2)**
  - [x] Read `ModuleContext.cs` in full (425 lines), plus `AdapterDiagnostic.cs`, `HowToReadTemplater.cs`, `SiteNav.cs:200-225`, and `SiteGenerator.cs` around `:3762` and `:4270`.
  - [x] Write **failing** tests first (red phase) that pin today's wrong behavior as the thing being fixed: a `cis`-only fixture asserting the module is NOT `BmadMethod` and the glossary is empty; a `tea`-before-`bmm` fixture asserting BMM keeps `/bmad-create-story`.
  - [x] Use **real** `module-help.csv` content in these fixtures (see the verified table above), not invented `cis-*`/`tea-*` skill ids — inventing prefixed ids would make the tests pass for the wrong reason and hide the bug.

- [x] **Task 2 — Identify modules by code, not skill prefix (AC: #1, #3)**
  - [x] Thread the module code (the containing directory name, `Path.GetFileName(Path.GetDirectoryName(csvPath))`) into `BuildContext` and replace the `prefix.StartsWith("gds")` inference at `ModuleContext.cs:346-348`.
  - [x] Map `bmm` → `BmadModule.BmadMethod`, `gds` → `BmadModule.GameDevStudio`, and **every other code** → the unmodeled identity (Task 3). Keep the parsed `CommandCatalog` and `ModuleLabel` intact in all cases.
  - [x] Keep the existing prefix-stripping step-key logic [ModuleContext.cs:329-338] untouched — it is what makes `/bmad-create-story` and `/gds-create-story` both resolve `create-story`, and it is **not** the bug.
  - [x] Confirm `_bmad/{code}` casing is handled the way the rest of the file handles it (`OrdinalIgnoreCase`).

- [x] **Task 3 — Make an unrecognized code a first-class outcome (AC: #1)**
  - [x] An unmodeled module resolves to a **new `BmadModule.Unmodeled` case** — **not** `Unknown` — **with** its real `ModuleLabel`, its parsed `CommandCatalog`, an empty `Docs`, and an empty `Glossary`. It must NOT fall through to `BmadMethod`.
    - **Why a new case and not `Unknown` (ADR 0015 Decision 2a, ratified 2026-07-26).** `DiagnosticsTemplater`'s `ModuleDisplay` — `module.Module == BmadModule.Unknown ? "Unknown (not detected)" : module.Commands.ModuleLabel` — is the **only live consumer of `Unknown`**, and it is the one surface already **correct today** for a CIS-only repo (it prints the real label). Routing unmodeled modules through `Unknown` would flip that row to "Unknown (not detected)", i.e. strictly worse than today. `Unknown` stays bound to genuine detection failure.
  - [x] Verify `DocsFor`/`GlossaryFor`'s existing `_ => Array.Empty<...>()` default already yields the right result for `Unmodeled` — it does; do not add a switch arm.
  - [x] **`CommandCatalog.Empty.ModuleLabel` must stop being `"BMad"`.** `ModuleContext.None` **is** that instance, so `None` and an unmodeled module are indistinguishable at exactly the surface Task 5 changes — the acknowledgement would read *"This project uses the BMad module"* on a repo with no `_bmad/` at all. Make the label empty and treat an empty label as "no label" everywhere (ADR 0015 Decision 2b).
  - [x] Emit exactly one `AdapterDiagnosticCategory.Informational` diagnostic. Drafted wording (match the tone of `BmadArtifactAdapter.cs:170-188`): `Detected BMad module '{code}' ({label}); SpecScribe has no module-specific docs or glossary for it, so those sections are omitted.`
  - [x] **Do not invent a sixth `AdapterDiagnosticCategory`.** `Informational` [AdapterDiagnostic.cs:26-31] was written for exactly this "FYI, nothing to do" case.
  - [x] Resolve the plumbing question explicitly: `ModuleContext.Detect` is static and returns no diagnostics today, while `BmadArtifactAdapter.Ingest` owns the `diagnostics` list and calls `Detect` at `BmadArtifactAdapter.cs:88`. Pick one seam (surfacing the fact on `ModuleContext` for the adapter to translate is the lighter change than making `Detect` diagnostic-aware) and say which in Completion Notes.
  - [x] **Detection currently runs twice, and the diagnosed one loses.** `_module` is set from `bundle.Module` (the adapter path, which has diagnostics) and is then **overwritten** by an adapter-free `ModuleContext.Detect` inside `SiteGenerator.BuildNav` — and 4 of `BuildNav`'s 5 call sites pass no diagnostics list at all. Whichever seam is chosen, the detection that feeds nav, glossary and `AbbreviationExpander` must be the one that was diagnosed: **detect once per run and have `BuildNav` consume the cached `ModuleContext`** (ADR 0015 Decision 2d).
  - [x] **State the emission cardinality**: at most one diagnostic per unmodeled module per generate run; on a watch rebuild, re-emit only if the installed module set changed. The diagnostics page must not accumulate a row per keystroke.
  - [x] **Fix the anchor root.** `AdapterDiagnostic.RelativePath` is contractually **source**-root-relative and `DiagnosticsTemplater` maps every adapter diagnostic to `DiagnosticAnchorRoot.Source`, but `_bmad/{code}/module-help.csv` is **repo**-root-relative — the webview Problems channel would resolve it to a nonexistent `{sourceRoot}/_bmad/...`. Add `DiagnosticAnchorRoot.Repo` and a matching arm in `Commands.cs`'s anchor switch (ADR 0015 Decision 2d).

- [x] **Task 4 — Never demote BMM or GDS on a manifest-order tie (AC: #2)**
  - [x] Change `ChoosePrimary` [ModuleContext.cs:259-286] so `bmm`/`gds` rank above auxiliary modules instead of relying on manifest order. Preserve the existing `looksLikeGame` source-shape tie-break **between** BMM and GDS — that is separate and correct.
  - [x] Verify the `bmm` + `gds` dual-install path is unchanged (`DualInstall_BothPresent` and the `looksLikeGame` behavior must still hold).
  - [x] Confirm the About-SDD contradiction is gone: with `tea` ahead of `bmm`, `IsMethodPresent` is `True` **and** the primary module is BMad Method.

- [x] **Task 5 — Named acknowledgement on how-to-read (AC: #1)**
  - [x] Implement the owner's elicited treatment (see "Owner design decision"): when a module is detected but unmodeled, render the `<h2 id="glossary">` heading with the one-sentence acknowledgement in place of the `<dl>`.
  - [x] Keep today's behavior when `ModuleContext.None` (no module at all): omit the whole section, per `HowToReadTemplater.cs:173-180`'s existing NFR8 note about never rendering an empty-but-present section.
  - [x] `HowToReadTemplater.RenderPage`'s signature currently takes `(nav, moduleDocs, glossary, commands)` [HowToReadTemplater.cs:13] — it needs the module label and the modeled/unmodeled distinction. Prefer passing the `ModuleContext` over adding two more positional parameters, and update the `SiteGenerator.cs:3762` call site.
  - [x] Escape the label through the templater's existing escaping convention — the label is third-party data from a CSV.

- [x] **Task 6 — Re-pin detection fixtures to real module CSV content (AC: #3)**
  - [x] `ModuleContextTests.cs` (54 facts) uses synthetic `gds-create-story`/`gds-dev-story` rows [`:57-58`], and `SiteGeneratorHowToReadTests.cs:57` does the same. These are **why this shipped undetected**: had GDS actually used `bmgd-*`, the suite would still have passed.
  - [x] Replace the GDS fixture rows with real ones from the module's own `module-help.csv`, and add `cis`/`tea`/`bmb` fixtures using their real `bmad-*` skill ids.
  - [x] Add a regression test that would fail if identity ever keys off the skill prefix again — e.g. assert a `tea` module whose skills are all `bmad-*` still resolves to the unmodeled identity, not `BmadMethod`.
  - [x] Add the dual-install ordering test from Defect B (`tea` before `bmm` in the manifest → BMM still primary).

- [x] **Task 7 — Regression, golden gate, and live verification (AC: #3)**
  - [x] Run the full suite. Expect the golden fingerprint to be **unchanged** for this repo (SpecScribe's own `_bmad/` has only `core` + `bmm`, so its detected identity does not move). If it moves, stop and explain why before re-baselining — and confirm stability across two repeated runs per `golden-diff-normalization-gotchas`.
  - [x] Per CLAUDE.md § Verification, verify the new how-to-read treatment **in a live browser**, not by assertion alone — generate against a scratch fixture repo containing `_bmad/tea/module-help.csv` and confirm the acknowledgement renders, the `#glossary` anchor still resolves, and no `<abbr>` expansion of FR/NFR/ADR appears anywhere in the output.
  - [x] Generate to `SpecScribeOutput/` (the default). Never `--output docs/live`.

- [x] **Task 8 — Record findings and close the stale note (AC: #1, #2, #3)**
  - [x] In Completion Notes: state which diagnostic seam Task 3 chose and why; confirm both defects are closed with the reproducing evidence; note any consumer of `ModuleContext.Module` that needed adjusting for a populated-but-`Unmodeled` context.
  - [x] Propose retiring the `epics.md` Additional-Requirements note *"current next-step command mapping is strongly GDS-oriented and requires generalization"* — **locate it by that quoted phrase, not by line number** (it was `:157` at 18.1's baseline `611097d` and is `:173` as of 2026-07-26). 18.1 assessed it as **stale** (the mechanism generalized when `CommandCatalog` became CSV-driven). Flag it for the owner; do not silently delete a requirements-adjacent line.
  - [x] ADR 0015 was **ratified `Accepted` on 2026-07-26** with all three open questions closed. This story implements its **Decisions 1, 2 and 4** only; Decision 3 (multi-valued set) and Decision 5a (`ArtifactCoverage`'s family set) are separate stories. Confirm the ADR's status is still `Accepted` at implementation time and note it in Completion Notes.

### Review Findings

_(populated during code-review)_

## Dev Notes

### Architecture compliance

- **ADR 0015 (Accepted, ratified 2026-07-26)** [docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md]
  — this story implements **Decisions 1, 2 and 4 only**, which is exactly what ratification question 1 settled.
  Decision 3 (multi-valued `ModuleContext` carrying the installed **set**) is deliberately **out of scope** and
  deferred to its own story; `ArtifactBundle.Module` stays a single required, never-null `ModuleContext`
  (ratification question 2). **Decision 5a** (`ArtifactCoverage.Specs`' hardcoded BMM family set, which the
  identity fix does *not* reach because it never reads `ModuleContext.Module`) is also out of scope here and
  sequenced after this slice. Decision 1 additionally carries four guards the draft lacked — reserved `_bmad/`
  child names (`core`, `custom`, `scripts`, any `_`-prefixed dir), case-insensitive code matching, minted-code
  collision with a modeled code, and manifest∪disk as the installed set — read them before implementing Task 2.
- **AD-1 / AD-2** [ARCHITECTURE-SPINE.md:34-48] — one shared projection core; the adapter boundary is
  source → normalized records. This story changes only how a module is *identified* inside that boundary.
  Nothing downstream of `ArtifactBundle` reinterprets anything.
- **NFR8** [epics.md:99] — *"surfaces degrade gracefully — absent, not broken or misleadingly empty."* This
  story's entire purpose is moving one surface from **confidently wrong** into that guarantee.
- **Seed, Not Invariant** [ARCHITECTURE-SPINE.md:98-102] — do not commit to a package split
  ([[epic-4-adapter-contract-scope]]: "no package split").

### Anti-patterns to prevent

- **Adding `Cis`/`Tea`/`Bmb` enum cases.** ADR 0015 rejects this explicitly. BMad Builder mints modules with
  arbitrary user-chosen codes, so **no closed enumeration can be correct**; three new cases would fix three
  repos and leave the bug's shape intact. The fix is open-world.
- **Building or designing the adapter registry.** `BmadArtifactAdapter.AppliesTo` markers `_bmad/` wholesale
  [BmadArtifactAdapter.cs:76-77], so every BMad module already self-selects into the existing adapter; a second
  `IArtifactAdapter` would have an identical `AppliesTo`. Epic 18 **extends**. The registry belongs to
  Epics 11-15 (`SiteGenerator.cs:51`) and all five of those spikes are still `ready-for-dev`.
- **Generalizing the next-step command vocabulary.** Assessed by 18.1 and rejected as unnecessary — those
  ~40 call sites live on surfaces (sprint board, epics, story pages) that only exist when epics and stories do,
  which only BMM and GDS produce. For a TEA/CIS repo the panels correctly vanish. Task 8 retires the stale note
  instead.
- **Reading `_bmad/{code}/config.yaml`.** Tempting while nearby, but it is Story 18.5's prerequisite (TEA's
  `test_artifacts` key), not this story's.
- **Widening the fix to module docs.** They are already file-gated [SiteNav.cs:206-215] and are not part of
  either defect.
- **Trusting doc-site prose over the module's real `module-help.csv`.** The `/bmgd-*` near-miss is the worked
  example.
- **Deleting the `epics.md` "strongly GDS-oriented" note silently.** Propose; let the owner decide.
- **Citing that note — or any code site — by line number.** It has already moved (`:157` → `:173` between
  18.1's baseline and 2026-07-26), and line drift inside `ModuleContext.cs` / `SiteGenerator.cs` invalidated a
  whole set of ADR 0015 references within days. Anchor on the quoted phrase or the symbol name.

### Testing standards

- xUnit, `tests/SpecScribe.Tests/`. `ModuleContextTests.cs` is the primary home (54 facts today) and follows a
  `WriteModule(code, csv, ...manifestModules)` fixture helper [`:43`] that writes a real
  `_bmad/{code}/module-help.csv` under a temp repo — reuse it rather than inventing a second fixture shape.
- Red-green-refactor per the workflow: Task 1's failing tests must exist and fail **before** Task 2's fix.
- `SiteGeneratorHowToReadTests.cs` covers the how-to-read page and also carries a synthetic `gds-*` fixture
  [`:57`] that Task 6 must re-pin.
- The golden fingerprint test [`SiteGeneratorAdapterTests.cs:235`] is byte-exact over a synthetic fixture that
  cites no real repo files; a correct implementation of this story should leave it untouched.

### Previous story intelligence (Story 18.1)

- Its Completion Notes are the full evidence base — read §1 (premise confirmations with line refs), §3 (the
  defect, with the probe output), §4 (extension points) and §8 (the ADR proposal) before starting.
- **Its verification method is worth repeating.** 18.1 proved detection behavior with a throwaway console probe
  in the session scratchpad referencing `SpecScribe.csproj`, running eight synthetic repo fixtures built from
  real module CSVs. That is a fast way to check a `ModuleContext` claim without touching `tests/`. It is a
  scratchpad tool, not a deliverable — 18.1 deleted it and `git status` confirmed a clean tree.
- 18.1 landed **no** `src/`/`tests/` changes. This story is the first Epic 18 code.

### Git intelligence summary

Baseline `611097d` (5.2, 20.5, 20.6, 25.1). **`ModuleContext.cs` has not been touched by any recent commit** —
recent work is Epic 20 (Plotly hierarchy explorer), Epic 5 (CLI/watch), and Epic 25 (SonarCloud CI), none of
which overlap this story's files. The one live hazard is CLAUDE.md's shared-`main` condition: at 18.1's close a
concurrent session had uncommitted edits to `HierarchyExplorer.cs`, `DashboardViewBuilder.cs`,
`RelatedWorkCards.cs`, `FileWatcherService.cs`, `SiteGenerator.cs`, `specscribe.css/js` and three test files,
which produced 3 unrelated suite failures (`TextTwin_IsComplete`, `GoldenContentFingerprint`,
`FileWatcherServiceTests.BurstOfSaves`). **Check `git status` before assuming any failure is yours**, and never
`git reset --hard`/`checkout --`/`clean` to tidy up. Note `SiteGenerator.cs` is in that concurrent set and this
story touches it at `:3762` — grep-verify your edit landed [[shared-main-concurrent-edit-loss-verify-after-edit]].

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/18-2-bmad-module-identity-foundation.md`
- Sprint key: `18-2-bmad-module-identity-foundation` (**renamed** from
  `18-2-priority-bmad-module-baseline-coverage` on 2026-07-25; `epics.md` updated in the same change)
- Gates: `18-5-priority-bmad-module-baseline-coverage` (new, `backlog`) — TEA artifact coverage
- Expected touches: `src/SpecScribe/ModuleContext.cs` (primary), `src/SpecScribe/HowToReadTemplater.cs`,
  `src/SpecScribe/SiteGenerator.cs` (call site + diagnostic plumbing), possibly
  `src/SpecScribe/BmadArtifactAdapter.cs` (diagnostic emission), `tests/SpecScribe.Tests/ModuleContextTests.cs`,
  `tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs`
- No new ADR expected — ADR 0015 already covers this decision.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Story 18.2] — the ACs quoted above (rewritten 2026-07-25).
- [Source: `_bmad-output/planning-artifacts/epics.md:99`] — NFR8 exact wording; `:157` — the stale command-generalization note Task 8 retires.
- [Source: `docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md`] — Decisions 1/2/4 are this story; Decision 3 is not.
- [Source: `_bmad-output/implementation-artifacts/18-1-bmad-module-landscape-and-coverage-spike.md`] — the spike; Completion Notes §1, §3, §4, §8.
- [Source: `src/SpecScribe/ModuleContext.cs:8, 118-123, 151-156, 174-188, 204-218, 259-286, 291-345, 346-348`] — the enum, the hardcoded tables, the generic core, the primary selector, and the defect.
- [Source: `src/SpecScribe/AdapterDiagnostic.cs:7-32`] — the five-value vocabulary; `:26-31` is `Informational`.
- [Source: `src/SpecScribe/HowToReadTemplater.cs:13, 173-180`] — `RenderPage` signature and the existing never-render-an-empty-section note.
- [Source: `src/SpecScribe/SiteGenerator.cs:3762, 3770-3771, 4270`] — how-to-read call site, presence checks, site-wide abbreviation expansion.
- [Source: `src/SpecScribe/SiteNav.cs:206-215`] — module docs are file-gated (why they are *not* affected).
- [Source: `src/SpecScribe/BmadArtifactAdapter.cs:76-77, 88, 170-188`] — `AppliesTo`'s `_bmad/` marker, the `Detect` call site, diagnostic tone to mirror.
- [Source: `src/SpecScribe/AboutSddTemplater.cs:10-18, 38-43, 66-70`] — the roster and its two-bool `detected` switches (widened only if AC #2's consistency check requires it; the roster itself is Story 18.5's concern).
- [Source: `tests/SpecScribe.Tests/ModuleContextTests.cs:43, 57-58, 87-96, 182-195`] — the fixture helper, the synthetic GDS rows to re-pin, the existing `Unknown` test, and the dual-install tests.
- [Upstream, verified 2026-07-25] — real `module-help.csv` / `module.yaml` of `bmad-code-org/bmad-module-game-dev-studio`, `bmad-module-creative-intelligence-suite`, `bmad-method-test-architecture-enterprise`, `bmad-builder`.
- **Memory:** [[story-18-1-bmad-module-landscape-done]], [[epic-4-adapter-contract-scope]], [[golden-diff-normalization-gotchas]], [[shared-main-concurrent-edit-loss-verify-after-edit]], [[generate-output-dir-is-specscribeoutput]].

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (`claude-opus-5`) — dev-story, 2026-07-26.

### Debug Log References

- **Golden-fingerprint causality experiment** (Task 7). `git worktree add <scratch> 86b35c26` → ran the golden
  test at baseline (passed, `dde7d077…`) → copied this story's seven `src/SpecScribe/*.cs` files into that
  worktree → ran again (**still passed, same constant**). Proof the constant does not move because of this
  story. Worktree removed with `git worktree remove --force`; `git status` on `src`/`tests` confirmed the main
  tree untouched. See Completion Note §6 for what actually moves it.
- **Live verification server**: `.claude/launch.json` gained a `tea-identity-18-2` entry (port 8108) following
  the file's existing convention. The five `preview_start` dev-server slots were all held by other chats, so the
  static server for that directory was run directly and the Browser pane attached by URL.
- **Upstream fixture re-fetch** (ADR 0015 Decision 7): four `module-help.csv` files fetched 2026-07-26 with the
  commit SHA each is pinned against recorded in the provenance block at the head of `ModuleContextTests`.

### Completion Notes List

**1. ADR 0015 status at implementation time: `Accepted`, ratified 2026-07-26** — and it was ratified *by a
concurrent session while this story was mid-implementation*. The version read at story start was `Proposed`
and materially thinner; the ratified document added Decisions 1a–1d, 2a–2d and 4a–4e. Per CLAUDE.md ("a
ratified ADR is the authority"; "project memory can be stale") the implementation follows the **ratified**
text, not the story's original task list. The story file was itself updated to match while the work was in
flight, so tasks and code now agree. This story implements **Decisions 1, 2 and 4 only**. Decision 3
(multi-valued `ModuleContext`) and Decision 5a (`ArtifactCoverage.Specs`' hardcoded BMM family set) are
explicitly out of scope and remain for their own stories.

**2. Both defects are closed, with reproducing evidence.** Every assertion below failed before the fix and
passes after:

- **Defect A — false presence.** `Detect_ModuleWithBmadSkillPrefix_IsUnmodeled_NotBmadMethod` over `cis`/`tea`/
  `bmb` fixtures built from those modules' **real** CSV bytes. Red-phase output: 9 failures across
  `ModuleContextTests`, plus 4 generation-level failures in `SiteGeneratorHowToReadTests`.
- **Defect B — the live BMM regression.** `Detect_AuxiliaryModuleAheadOfBmmInManifest_KeepsBmadMethodPrimary`
  (theory over `cis`/`tea`/`bmb`) asserts BMM keeps `/bmad-create-story`, its glossary and `prd.md`, *and* that
  `IsMethodPresent` agrees with the selected primary — the AC #2 consistency clause.

**3. Diagnostic seam chosen: `Detect` gained the sink (NOT the lighter "surface a fact on `ModuleContext`"
option the story originally suggested).** The ratified ADR (Decision 2d) settles this, and the reason is
decisive: three distinct conditions need reporting, and two of them are invisible to the returned context —
a candidate whose CSV would not parse, and the modules that lost the primary slot. A flag on the winner can
only describe the winner. `Detect` now takes `List<AdapterDiagnostic>? diagnostics = null`, matching the
existing `IngestSprint(options, diagnostics)` convention, and `BmadArtifactAdapter.Ingest` passes its own list.
Four diagnostics can now be emitted, all non-fatal:

| Condition | Category | ADR |
|---|---|---|
| Unmodeled primary | `Informational` | 2d |
| Non-primary installed modules | `Skipped` | 4e |
| Candidate CSV won't parse | `Malformed` | 4d |
| Modeled code declaring the wrong label | `Unsupported` | 1c |

**4. `Unmodeled` is a NEW enum case, and `Unknown` was left alone.** The one live consumer of `Unknown` is
`DiagnosticsTemplater.ModuleDisplay`, whose "Detected framework" row was **already correct** for a TEA repo —
it prints the parsed label. Reusing `Unknown` would have flipped it to "Unknown (not detected)", trading one
correct surface for another. Verified live: the TEA fixture's diagnostics page reads
`Detected framework = Test Architecture Enterprise`.

**5. Consumers that needed adjusting for a populated-but-`Unmodeled` context** (the state that could not occur
before):

- `HowToReadTemplater.AppendGlossary` — gains the named-acknowledgement branch, gated on `IsUnmodeled` **and**
  a non-empty label.
- `HowToReadTemplater.AppendCommandLegend` — now gated on `IsModeled`, not merely a non-empty catalog. An
  unmodeled module parses a perfectly real catalog, but the legend's sentence points at captions on story and
  epic pages, which only a modeled module produces.
- `HowToReadTemplater.RenderPage` — takes the whole `ModuleContext` instead of three projections of it.
- `DiagnosticsTemplater.ModuleDisplay` — also gates on `HasLabel`, because…
- `CommandCatalog.Empty.ModuleLabel` changed `"BMad"` → `""` (Decision 2b). `ModuleContext.None` **is** that
  instance, so without this the acknowledgement would have read *"This project uses the BMad module"* on a repo
  with no `_bmad/` at all — a worse false claim than the silent omission it replaced. New `HasLabel` guard.
- `SiteGenerator.BuildNav` — **stopped re-detecting**. Detection ran twice per run and the *undiagnosed* one won
  (`_module` was set from `bundle.Module`, then overwritten). 4 of `BuildNav`'s 5 call sites pass an empty
  source list, so the BMM-vs-GDS `looksLikeGame` tie-break was unconditionally false on every incremental and
  watch rebuild — a dual-install game repo silently fell back to BMM mid-session. Now detect-once-per-run.
- `AdapterDiagnostic` / `GenerationEvent` / `DiagnosticNotice` / `Commands.SerializeDiagnostics` — new
  `DiagnosticAnchorRoot.Repo`, because `_bmad/{code}/module-help.csv` is repo-relative while every other adapter
  diagnostic is source-relative; the webview Problems channel would otherwise resolve it to a nonexistent path.

`AboutSddTemplater` needed **no** change: `IsMethodPresent`/`IsGdsPresent` are independent presence checks, so
fixing the ranking made "Detected" and the primary agree on their own. (Decision 3c's `ModuleCode` field on the
`Frameworks` roster sits under Decision 3 and is out of scope.)

**6. The golden fingerprint moved mid-story — and it is NOT this story. Resolved; this story re-baselined
nothing.** This story is byte-neutral for a repo with no `_bmad/` install, which is exactly what the golden
fixture is. Proven, not assumed (see Debug Log): with this story's `src` changes copied into a clean worktree
at baseline `86b35c26`, the golden test **passed with the then-current constant `dde7d077…`**. The mover was a
concurrent session's 24-line addition to `src/SpecScribe/assets/specscribe.css` (Story 20.6's
`.ss-hierarchy-twin.sr-only:focus-within` rules) — that file is copied verbatim into every generated site, so
it sits inside the hash. Mid-story that session re-baselined the constant itself to **`7adbdb01…`**, which is
**bit-for-bit the value this story's runs had been computing all along** — independent corroboration that the
delta was entirely theirs. The gate is green again with the constant untouched by me.

⚠️ **New gotcha worth adding to `[[golden-diff-normalization-gotchas]]`: `git status` lied.** At the moment
`specscribe.css` was already 1280 bytes larger on disk, `git status` did **not** list it as modified; it only
appeared minutes later. The discrepancy was caught by comparing byte sizes against a fresh `git worktree`
checkout of the baseline commit, not by trusting porcelain. **Byte-compare against a clean checkout before
concluding a golden move is yours** — and note the worktree technique itself is safe under CLAUDE.md's
shared-`main` rule, since it never touches the working tree.

**7. Full-suite state — read this before treating the suite as red.** Final run: **2471 tests, 2448 passed,
19 failed, 4 skipped** (total grew from 2465 during the story — a concurrent session added tests). **Zero
failures in any class this story touches**: `ModuleContextTests`, `SiteGeneratorHowToReadTests`,
`BmadArtifactAdapterTests`, `DiagnosticsTemplaterTests` and the golden gate are all green.

Every failure is in the deep-git family (`SiteGeneratorTimelineTests`, `SiteGeneratorImpactMapTests`,
`SiteGeneratorGitInsightsTests`, `SiteGeneratorCodeInsightsTests`, `SiteGeneratorCommitDetailsTests`,
`SiteGeneratorChangeLogDateLinkTests`, `GitMetricsFirstCommitDateTests`, one `SiteGeneratorSpaTests`
sunburst-island case). Evidence they are **load-dependent contention, not regressions**:

- The failing subset **rotates every run** — 25, then 17, then 19, overlapping but never identical.
- Every one **passes in isolation** (verified 37/37 as a five-class group, 3/3, and 1/1 for a single case that
  had just failed in a group run).
- `GitMetricsFirstCommitDateTests.TryGetFirstCommitDate_ReturnsNull_ForNonexistentPath` — a test that asserts a
  nonexistent path returns null — failed in one run and passed the next. Nothing in this story can reach it.
- They all spawn `git` subprocesses against temp repos, and at least one other session was running its own
  suite concurrently (it re-baselined the golden constant mid-run). `[[epics-19-21-joint-retro-2026-07-23]]`
  and Story 23.2 already record "one rotating contention flake per full run" as a known property; this is that,
  amplified by concurrent load.

**This is not a claim that the suite is green — 19 tests did fail.** It is a claim, with the evidence above,
that none of them are attributable to this story. A quiet-machine re-run is the way to confirm.

**8. Proposal for the owner — retire a stale requirements line (do NOT let this be lost).** In
`_bmad-output/planning-artifacts/epics.md`, Additional Requirements, the clause *"current next-step command
mapping is strongly GDS-oriented and requires generalization"* (currently line 173; located by phrase, not
number) is **stale** and should be retired. The mechanism generalized when `CommandCatalog` became CSV-driven:
`BuildContext` strips the module prefix and keys on the step remainder, so `/bmad-create-story` and
`/gds-create-story` both resolve `create-story` with zero module-specific code. The residual BMM∩GDS *step
vocabulary* at ~40 call sites needs no generalization either, because those surfaces (sprint board, epics,
story pages) only exist when epics and stories exist — which only BMM and GDS produce; for a TEA or CIS repo
the panels correctly vanish, which this story verified live. **Not deleted here** — it is a requirements-adjacent
line and the call is the owner's. Note Story 18.1's AC also references this phrase (epics.md:3045), so retiring
it should update both sites.

**9. Live verification (CLAUDE.md § Verification).** Two real browser sessions, not assertions alone:

- **Unmodeled path** — a scratch fixture repo with only `_bmad/tea/module-help.csv` (the **real** upstream file,
  all 11 rows). Confirmed in-page via the DOM: `<h2 id="glossary">` present and the `#glossary` anchor resolves
  to a real box (non-zero width/height, so in-page links still land); the body is a `<p>` reading *"This project
  uses the **Test Architecture Enterprise** module. SpecScribe doesn't publish a glossary for it yet."*; no
  `.howtoread-glossary` `<dl>`; no `#commands` section; **zero `<abbr>` elements** on how-to-read, on
  `epics.html` (whose prose deliberately uses bare FR/NFR/AC/ADR/PRD), or on diagnostics; no horizontal body
  scroll; no console errors. The diagnostics page shows exactly one notice, badged **`Informational`** as a WORD
  (`status-badge diag-info`) — so the state is not signalled by colour alone.
- **Modeled path unchanged (AC #3)** — regenerated this repo's own BMM portal (413 pages, 0 errors) to a scratch
  output dir: glossary `<dl>` intact with all ten terms, command legend intact ("…your detected methodology,
  BMad Method"), all five `<abbr title>` expansions intact on `epics.html`,
  `Detected framework = BMad Method`, and **no** unmodeled notice leaked.

Generation used the default output dir / an explicit scratch dir. `--output docs/live` was never used.

**10. Anti-patterns avoided, as instructed.** No `Cis`/`Tea`/`Bmb` enum cases (the fix is open-world). No
adapter registry. No module `config.yaml` reading. No widening to module docs (already file-gated). No sixth
`AdapterDiagnosticCategory`. The prefix-stripping *step-key* logic is untouched — it was never the bug.

### File List

- `src/SpecScribe/ModuleContext.cs` — **primary.** `BmadModule.Unmodeled`; `CommandCatalog.HasLabel` +
  `Empty`'s label emptied; `ModuleContext.Code`/`IsUnmodeled`/`IsModeled`; `BmmCode`/`GdsCode`;
  `ModuleHelpFileName`; `ReservedModuleNames`/`IsReservedModuleName`; `ModeledModuleLabels`; `FindModuleCsv`;
  `SafeEnumerateDirectories`; `DiscoverCandidates`; `RankCandidates` (replaces `ChoosePrimary`); `CodeOf`;
  `ModuleForCode`; `RepoRelativeCsv`; `ReportSecondaryModules`; `ReportUnmodeledPrimary`; `Detect` and
  `BuildContext` gain the diagnostics sink.
- `src/SpecScribe/HowToReadTemplater.cs` — `RenderPage(nav, ModuleContext)`; `AppendGlossary` acknowledgement
  branch; `AppendCommandLegend` gated on a modeled primary.
- `src/SpecScribe/AdapterDiagnostic.cs` — `Anchor` parameter (defaults to `Source`).
- `src/SpecScribe/DiagnosticsTemplater.cs` — `DiagnosticAnchorRoot.Repo`; anchor derivation prefers an explicit
  event anchor; `ModuleDisplay` gains the `HasLabel` guard.
- `src/SpecScribe/Commands.cs` — `DiagnosticAnchorRoot.Repo` arm in the Problems-channel anchor switch.
- `src/SpecScribe/BmadArtifactAdapter.cs` — passes `diagnostics` into `ModuleContext.Detect`.
- `src/SpecScribe/SiteGenerator.cs` — `GenerationEvent.DiagnosticAnchor`; `MapDiagnostics` carries it;
  `WriteHowToRead` passes `_module`; `BuildNav` no longer re-detects.
- `tests/SpecScribe.Tests/ModuleContextTests.cs` — upstream-pinned fixtures (GDS/TEA/CIS/BMB) with provenance
  and commit SHAs; `WriteManifest`/`WriteModuleDir` helpers; 17 new facts/theories.
- `tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs` — upstream-pinned GDS + new TEA fixture;
  `InstallOnly` helper; 5 new generation-level facts.
- `.claude/launch.json` — `tea-identity-18-2` preview entry (port 8108) for the live verification.

## Change Log

- 2026-07-26 — **Implemented (dev-story), status → review.** Baseline `86b35c2`. Both defects closed by
  red-green-refactor: 13 failing tests written first (9 in `ModuleContextTests`, 4 generation-level), then the
  fix. Identity now derives from the module **code** (`_bmad/{code}/`), an unrecognized code resolves to a new
  first-class `BmadModule.Unmodeled` (real label + parsed catalog, no docs, no glossary) plus one
  `Informational` diagnostic, and `ChoosePrimary` became a deterministic `RankCandidates` that can never demote
  BMM or GDS. ADR 0015 was ratified `Accepted` **by a concurrent session mid-implementation**, expanding its
  Decisions 1/2/4 with guards the story draft predated (reserved `_bmad/` child names, case-insensitive codes
  stored lower-invariant, a minted-code-squatting-a-modeled-code label cross-check, manifest∪disk as the
  installed set, `Unmodeled` as a NEW case rather than a reuse of `Unknown`, an emptied
  `CommandCatalog.Empty.ModuleLabel`, a diagnostics sink on `Detect`, detect-once-per-run, and a new
  `DiagnosticAnchorRoot.Repo`); the ratified text was implemented in full. Test fixtures re-pinned to **real**
  upstream `module-help.csv` content for GDS/TEA/CIS/BMB with per-file commit SHAs recorded. Verified in a live
  browser on both paths — the unmodeled acknowledgement (anchor resolves, zero `<abbr>` site-wide, one
  `Informational` word-badge) and the unchanged BMM portal (413 pages, glossary + legend + all five `<abbr>`
  expansions intact). The golden fingerprint moved mid-story and **this story re-baselined nothing**: a worktree
  experiment at the baseline commit proved this story leaves it at `dde7d077…`, and the concurrent Story 20.6
  session then re-baselined it to `7adbdb01…` — bit-for-bit the value these runs had been computing — for its
  own `specscribe.css` change. Gate green. See Completion Notes §6, including a new golden-diff gotcha
  (`git status` under-reported a concurrent mid-write for several minutes). Open item for the owner: retire the
  stale *"strongly GDS-oriented"* note in `epics.md` (§8) — proposed, not deleted.
- 2026-07-25 — Story 18.2 **redefined and drafted** (create-story). Formerly "Priority BMad Module Baseline
  Coverage"; rescoped after Story 18.1's spike to the module-identity foundation (ADR 0015 Decisions 1/2/4), with
  its former artifact-coverage ACs moved verbatim to the new Story 18.5 (TEA), which this story now gates.
  Owner-approved scope split; `epics.md` and `sprint-status.yaml` both updated in the same change, and the sprint
  key renamed `18-2-priority-bmad-module-baseline-coverage` → `18-2-bmad-module-identity-foundation`. This is the
  first Epic 18 story to land code. It fixes two defects proven empirically in 18.1 — false module identity for
  every `bmad-`-prefixed module (CIS/TEA/BMB) and a manifest-order-dependent regression that strips all BMM
  commands from a genuine BMM repo — whose shared root cause is one line pair at `ModuleContext.cs:346-348`.
  Owner elicited the visual treatment for the unmodeled state up front: a **named acknowledgement** on
  how-to-read naming the detected module, not a silent omission. Explicit non-goals recorded: ADR 0015
  Decision 3 (multi-valued `ModuleContext`), the adapter registry, module `config.yaml` reading, and command
  vocabulary generalization.
