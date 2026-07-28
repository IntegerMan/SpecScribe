---
baseline_commit: 86b35c267241c15b05c64e3aaa3e13cce58198b2
---

# Story 18.2: BMad Module Identity Foundation

Status: done

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

**Code review 2026-07-27** (bmad-code-review, 3 parallel layers: Blind Hunter / Edge Case Hunter / Acceptance
Auditor). Scoped by this story's **File List and declared symbols**, not by a commit range: `86b35c26..HEAD`
spans 9 commits and bundles **sibling stories 18.4, 18.5, 20.8, 23.5, 25.2, 25.3**, all excluded. In
`SiteGenerator.cs` only the four declared symbols were reviewed (`GenerationEvent`, `MapDiagnostics`,
`WriteHowToRead`, `BuildNav`). All 17 claimed `ModuleContext.cs` symbols were grep-verified present before the
File List was trusted. Severities are the reviewer's own, assigned after reading the un-diffed call sites — not
the subagents'. 13 actionable, 3 deferred, 9 dismissed.

**Decisions resolved (owner call, 2026-07-27)** — all five closed at review. The resulting work is folded into
the Patch list below; the original findings are retained (checked off) for the reasoning that led to each call.

| # | Finding | Owner's call | Becomes |
|---|---|---|---|
| D1 | Decision-1c label cross-check vs upstream label drift | **Tolerant comparison** — accept a label that starts with / contains the expected one, so `BMad Method v6` passes and `Totally Not GDS` still demotes | Patch P9 |
| D2 | A demoted candidate keeps the primary slot | **Continue descending** — treat a demotion like a parse failure; keep the `Unsupported` notice, skip the candidate, try the next rank | Patch P10 |
| D3 | About-SDD "Detected" vs the selected primary | **Correct the record only** — presence checks stay independent (Story 18.5 depends on that contract); fix Completion Note §5 and the AC #2 assessment | Patch P11 |
| D4 | `Malformed` on a non-primary candidate ⇒ `errors=1` | **Accept `errors=1`** — a broken module catalog is worth failing the run's status line even when the site generated correctly; no code change, document the behaviour | Patch P12 (record only) |
| D5 | `ReportSecondaryModules` fires below Decision 4e's ">1" threshold | **Fire at 1, as `Informational`** — keep reporting a single secondary module, but at info severity so a healthy repo shows no warning | Patch P13 |

⚠️ **ADR 0015 needs amending, and that is a separate proposal, not a silent edit.** D1 and D5 both change
ratified text — Decision **1c**'s exact-match rule becomes a tolerant match, and Decision **4e**'s
*"When >1 non-primary module is installed, one `Skipped` diagnostic"* becomes "≥1, emitted as `Informational`".
D2 additionally widens Decision **4d**'s guarantee from "a parse failure never promotes a lower-ranked module" to
cover a 1c demotion, and D4 records an accepted tension between Decision 4d's mandated category and
`MapDiagnostics`' Error mapping. Per CLAUDE.md ("propose an ADR without being asked … for any decision that
amends a prior ADR"), these should land as an ADR 0015 amendment — either a revision block on 0015 or a
superseding ADR — in the **same change** as the code patches, so the code and the ratified text never disagree.

**Decision needed (all resolved above)**

- [x] [Review][Decision] **The Decision-1c label cross-check demotes a genuine modeled module on any upstream
  label drift** — MEDIUM, AC #3 risk. `ModuleContext.ModeledModuleLabels` hardcodes `"BMad Method"` /
  `"Game Dev Studio"` and `BuildContext` demotes to `Unmodeled` on an exact (`OrdinalIgnoreCase`) mismatch, with
  no normalization beyond a per-cell `.Trim()`. ADR 0015 itself documents that upstream labels drift — `gds`'s
  `module.yaml` says *"BMGD: BMad Game Dev Studio"* and `tea`'s says *"Test Architect"* vs the CSV's
  *"Test Architecture Enterprise"*. A cosmetic upstream rename (`BMad Method v6`, a double space) therefore
  strips a real BMM/GDS install of its `Docs`, its entire glossary, site-wide `AbbreviationExpander` expansion
  and the command legend, signalled only by one `Unsupported` row rendered at **warning** severity. The guard
  defends a hypothetical squatter at the cost of making the shipped happy path depend on a third-party display
  string. Compounded by `moduleLabel` being **last-row-wins**, so one appended shared-skill row carrying a
  different `module` value is enough to trigger it. Options: (a) accept as ratified; (b) match on a normalized /
  prefix-tolerant comparison; (c) keep the `Unsupported` notice but do **not** demote — report and continue as
  the modeled module. *(blind+auditor)*
- [x] [Review][Decision] **A candidate demoted to `Unmodeled` keeps the primary slot, so a genuine BMM/GDS
  below it is never built** — MEDIUM, violates AC #2's first clause. `ModuleContext.Detect`'s descend-the-rank
  loop `break`s on the first **non-null** `BuildContext`, and a 1c-demoted context is non-null. Ranking is
  computed from codes *before* any label is parsed, so with a BMB-minted squatter at `_bmad/gds/` (wrong label)
  beside a genuine `_bmad/bmm/` and any game-shaped source path (`gdd.md` ⇒ `Rank("gds") == 0`), the squatter
  wins as `Unmodeled` and BMM — a modeled module — is demoted below it and never even built. That is Defect B's
  exact symptom, reintroduced through a different door. Note the `Malformed` path `continue`s but the demotion
  path `break`s; ADR 0015 Decision 4d's guarantee is written only for `BuildContext` returning null. Options:
  (a) treat a demotion like a parse failure and continue descending; (b) accept — a demoted module is still an
  installed module and dropping it would be worse; (c) re-rank after identity resolves. *(edge+auditor)*
- [x] [Review][Decision] **About-SDD "Detected" and the selected primary can still contradict each other, and
  Completion Note §5 overstates the fix** — MEDIUM, violates AC #2's trailing clause. `AboutSddTemplater`'s
  `RenderHub`/`RenderFrameworkPage` `detected` switch reads `IsMethodPresent`/`IsGdsPresent`, which are pure
  presence checks (manifest entry **or** on-disk CSV) and know nothing about demotion or ranking. Two newly
  reachable contradictions: (1) the squatter case this story's own test
  `Detect_MintedModuleSquattingAModeledCode_IsDemotedToUnmodeled_AndReported` constructs — `_bmad/gds/` labelled
  *"Totally Not GDS"* renders "BMad GDS — Supported, **Detected**" and the `sdd-detected-banner` while
  how-to-read says the module is unmodeled and no GDS docs/glossary/commands exist; (2) a manifest listing `bmm`
  with no `_bmad/bmm/module-help.csv` on disk (pinned by the pre-existing
  `IsMethodPresent_TrueWhenManifestListsBmmWithoutCsv`) beside an installed `_bmad/tea/` — "BMad — Detected"
  while the primary is Unmodeled TEA. Pre-fix pair (2) agreed *by accident*, because TEA was misidentified as
  `BmadMethod`. Completion Note §5's *"`AboutSddTemplater` needed **no** change: … fixing the ranking made
  'Detected' and the primary agree on their own"* is true only for the manifest-order case the story targeted.
  Also narrows Decision 1d's "those two must not disagree" claim: `DiscoverCandidates` is
  `(manifest ∪ disk) ∩ has-csv` while `IsModulePresent` is `manifest OR disk-csv`, so the union closes one
  direction only. Options: (a) gate `detected` on a successfully-built modeled context (changes
  `IsModulePresent`'s contract, which Story 18.5 now depends on); (b) leave presence independent and correct the
  Completion Note + AC #2 assessment; (c) narrow the ADR/AC wording to the manifest-order case. *(auditor+blind)*
- [x] [Review][Decision] **A non-primary candidate's unparseable CSV turns a fully successful run red on the
  CI-grepped machine summary line** — MEDIUM. ADR 0015 Decision 4d *mandates* the `Malformed` category, and
  `SiteGenerator.MapDiagnostics`'s pre-existing rule maps `Malformed` → `GenerationOutcome.Error` → `counts.Errors`
  → `errors=N` on `GenerationSummary`'s machine line. So a repo with a truncated `_bmad/bmm/module-help.csv`
  beside a healthy `_bmad/tea/` generates a completely correct site and still reports `errors=1` on every
  `generate`, and `RegenerateTopology`/`RegenerateFromDataSource` (both collapse to
  `events.FirstOrDefault(e => e.Outcome == Error)`) report every watch-mode rebuild as failed. Nothing is
  actually missing from the output, and pre-story this path was silent. Two ratified contracts collide; only the
  owner can pick. Options: (a) emit `Skipped` for a candidate that was not needed and reserve `Malformed` for the
  primary; (b) exempt module-identity notices from the Error mapping; (c) accept `errors=1`. *(blind+edge)*
- [x] [Review][Decision] **`ReportSecondaryModules` fires below Decision 4e's stated threshold, so a healthy
  BMM+TEA repo emits a notice on every run** — MEDIUM. ADR 0015 Decision 4e reads *"When **>1** non-primary
  module is installed, one `Skipped` diagnostic records the others"*; the guard is
  `if (diagnostics is null || ranked.Count < 2) return;` — i.e. **≥1** non-primary. Story 18.5's own primary
  scenario (BMM + TEA, one non-primary, nothing wrong) therefore emits a `Skipped` row on the diagnostics page of
  a healthy repo, at **Warning** severity, on every full run — only `Informational` maps to
  `DiagnosticSeverity.Info` in `DiagnosticsTemplater`. Pinned as intended by
  `Detect_ManifestAndDiskDisagree_TheSetIsTheirUnion`, which has exactly one non-primary and asserts
  `Assert.Single(… Skipped)`. Either the ADR wording is a slip or the code is. Options: (a) the ADR is loosely
  worded — keep the code, amend Decision 4e to "≥1"; (b) honour ">1" literally and change the guard to
  `others.Count < 2`; (c) keep firing at 1 but emit it as `Informational` so a healthy repo shows an info row,
  not a warning. *(auditor)*

**Patch**

_P9–P13 are the decision-derived patches; P1–P8 came directly from the review layers. P9, P10 and P13 change
behaviour ADR 0015 ratified — land the ADR 0015 amendment in the same change (see the ⚠️ note above)._

- [x] [Review][Patch] **P9 — make the Decision-1c label cross-check tolerant instead of exact** (from D1).
  [src/SpecScribe/ModuleContext.cs — `BuildContext`'s `ModeledModuleLabels` cross-check]. Replace the
  `!string.Equals(moduleLabel, expectedLabel, OrdinalIgnoreCase)` demotion trigger with a tolerant match — a
  label that starts with or contains the expected label passes, anything else demotes. `BMad Method v6` and
  `BMad  Method` (double space, so normalize interior whitespace too) must **not** demote; `Totally Not GDS`
  must. Keep the `Unsupported` notice wording, which already names both labels. Interacts with P1: once
  `moduleLabel` defaults to empty, decide whether an **absent** label should demote at all — recommend it should
  not, since an absent label is no evidence of squatting. Add tests for all four cases (exact, drifted-prefix,
  whitespace-variant, genuine squatter) and for the absent-label case.
- [x] [Review][Patch] **P10 — a 1c demotion should continue descending the rank, not claim the primary slot**
  (from D2). [src/SpecScribe/ModuleContext.cs — `Detect`'s descend-the-rank loop, `BuildContext`'s demotion
  branch]. `BuildContext` must let `Detect` distinguish "parsed and modeled" from "parsed but demoted" — return
  the demoted context alongside a flag, or have the loop re-check `IsUnmodeled` against a modeled code. On a
  demotion: keep the `Unsupported` notice, skip the candidate, continue to the next rank. Only fall back to the
  demoted context if **no** lower-ranked candidate builds, so a single-module repo still gets a context rather
  than `None`. Add the AC #2 regression test the hole needs: squatter at `_bmad/gds/` (wrong label) + genuine
  `_bmad/bmm/` + a `gdd.md` source path ⇒ BMad Method is primary and keeps `/bmad-create-story`, its glossary and
  `prd.md`.
- [x] [Review][Patch] **P11 — correct the review record for the About-SDD contradiction rather than the code**
  (from D3). Presence checks stay independent — that is their documented contract and Story 18.5's test-artifact
  gating depends on it. Three record edits: (a) rewrite Completion Note §5's *"`AboutSddTemplater` needed **no**
  change: … fixing the ranking made 'Detected' and the primary agree on their own"* to say the ranking fix closed
  the **manifest-order** contradiction only; (b) downgrade AC #2's trailing clause from satisfied to **partially
  satisfied**, naming the two residual cases (a 1c-demoted squatter still reports "Detected"; a manifest entry
  with no on-disk CSV beside another installed module still reports "Detected" for a module that is not primary —
  the latter pinned by the pre-existing `IsMethodPresent_TrueWhenManifestListsBmmWithoutCsv`); (c) narrow
  `DiscoverCandidates`' doc-comment claim that `IsModulePresent` and `Detect` "must not disagree" to the direction
  the union actually closed — `(manifest ∪ disk) ∩ has-csv` vs `manifest OR disk-csv` still diverge when a
  manifest entry has no CSV. No `src/` change.
- [x] [Review][Patch] **P12 — document the accepted `errors=1` behaviour** (from D4, record only). The owner
  accepted that a non-primary candidate's unparseable `module-help.csv` fails the run's status line even when the
  site generated correctly. Record it where it will be found: a Completion Note entry stating the accepted
  tension between ADR 0015 Decision 4d's mandated `Malformed` category and `SiteGenerator.MapDiagnostics`'
  `Malformed ⇒ GenerationOutcome.Error` rule, and its two observable consequences — `errors=1` on
  `GenerationSummary`'s CI-grepped machine line, and `RegenerateTopology`/`RegenerateFromDataSource` reporting a
  watch rebuild as failed. Note it in the ADR 0015 amendment too, so a future reader does not "fix" it as a bug.
  No code change.
- [x] [Review][Patch] **P13 — emit the secondary-module notice as `Informational`, keep the ≥1 threshold**
  (from D5). [src/SpecScribe/ModuleContext.cs — `ReportSecondaryModules`]. Change the category from
  `AdapterDiagnosticCategory.Skipped` to `Informational` so a healthy BMM+TEA repo shows an info row
  (`diag-info` word badge, `DiagnosticSeverity.Info`) rather than a warning; leave the `ranked.Count < 2` guard
  firing at one non-primary module, since that explanation is exactly what a BMM+TEA user needs. Update
  `Detect_ManifestAndDiskDisagree_TheSetIsTheirUnion`, which asserts `Assert.Single(… Skipped)`, and amend ADR
  0015 Decision 4e's threshold **and** category. Land with P3, which fixes the same method's `others` set and its
  "docs come from" clause. Note this puts **two** `Informational` notices on an unmodeled multi-module repo (this
  one plus `ReportUnmodeledPrimary`'s) — confirm the wording reads coherently side by side.
- [x] [Review][Patch] **P1 — `BuildContext` still fabricates the label `"BMad"`, so ADR 0015 Decision 2b is not
  enforced at the parse site and all three new `HasLabel` guards are dead code** — HIGH.
  [src/SpecScribe/ModuleContext.cs — `BuildContext` (`var moduleLabel = "BMad";`)]. Only
  `CommandCatalog.Empty`'s label was emptied. `BuildContext` requires only a `skill` column (`if (skillIdx < 0)
  return null;`), so `moduleIdx` may be `-1`, or every data row's `module` cell may be blank, and the literal
  `"BMad"` survives — and it is only ever overwritten with non-empty trimmed values. Consequences: (1) `HasLabel`
  is `true` for **every** context `BuildContext` returns, so `HowToReadTemplater.AppendGlossary`'s
  `&& module.Commands.HasLabel`, `AppendCommandLegend`'s `|| !module.Commands.HasLabel` and
  `DiagnosticsTemplater`'s `HasLabel` guard can never change an outcome — the only `HasLabel == false` instance
  is `ModuleContext.None`, whose `Module` is already `Unknown`; (2) the false claim still ships — a module at
  `_bmad/acme/` in that shape renders **"This project uses the BMad module. SpecScribe doesn't publish a glossary
  for it yet."** and `Detected framework: BMad`, verbatim the sentence Decision 2b exists to prevent; (3) worse,
  a *genuine* `_bmad/bmm/` in that shape hits the 1c cross-check (`"BMad" != "BMad Method"`) and is **demoted to
  `Unmodeled`**, losing every BMM doc, the whole glossary and every command. Fix: `var moduleLabel =
  string.Empty;` — which also makes the three guards live, as written. Consider additionally skipping the 1c
  cross-check when there is no label at all, rather than demoting on an absent one. **No test covers a CSV
  without a usable `module` column** — add one. *(blind+edge+auditor — all three layers independently)*
- [x] [Review][Patch] **P2 — the always-rendering unmodeled acknowledgement feeds the "is there content" honesty
  gate, so the page promises a reading order and a glossary it does not render** — MEDIUM.
  [src/SpecScribe/HowToReadTemplater.cs — `RenderPage`'s `hasModuleContent`, with `AppendGlossary`'s new branch].
  `hasModuleContent = readingOrder.Length > 0 || reference.Length > 0`, and the unmodeled branch **always**
  writes into `reference`. So every unmodeled-module repo now takes the `true` branch and emits the subtitle
  *"New here? Start with the reading order and glossary below, then generate the site yourself."* plus the intro
  *"…the sections below walk you through what to read first, how to rebuild this site yourself, and **what the
  recurring terms mean**"* — on a page whose glossary section states there is no glossary. Reachable on **every**
  unmodeled repo for the glossary half; for the reading-order half whenever `readingOrder` is empty (a CIS/BMB
  repo produces no `epics.md`, and with no README/ADRs/sprint the promise points at nothing). This is exactly
  [[story-5-6-how-to-use-cli-guidance-done]]'s recorded rule — *an ALWAYS-RENDERS section must not feed an "is
  there content" honesty gate* — re-broken by the new branch. `RenderPage`'s own comment states the invariant
  ("never promise content that doesn't exist"). Fix: exclude the acknowledgement from the gate (compute it
  separately from `reference`, or gate on `!module.IsUnmodeled`). The story's own test
  `HowToRead_UnmodeledModule_OmitsCommandLegend_AndSkipsModuleDocReadingOrder` asserts only absences and never
  the header copy. *(blind+edge+auditor — all three layers)*
- [x] [Review][Patch] **P3 — `ReportSecondaryModules` reports unparseable candidates as "not the primary",
  contradicting its own `Malformed` notice, and claims docs/glossary "come from" an `Unmodeled` primary** —
  MEDIUM. [src/SpecScribe/ModuleContext.cs — `ReportSecondaryModules`]. Two defects in one method.
  (1) `others = ranked.Where((_, i) => i != chosenIndex)` includes every index **below** `chosenIndex` — i.e.
  exactly the candidates that already failed `BuildContext` and were reported `Malformed`. The state
  `Detect_UnparseableHigherRankedModule_IsReported_AndNeverPromotesALowerRankedOne` sets up therefore emits both
  *"module help catalog could not be parsed; 'bmm' is skipped"* and *"1 other installed BMad module(s) (bmm) are
  not the primary"* — the second is false (bmm did not lose a ranking, it is unreadable) and tells the reader the
  ranking worked as designed. The test cannot catch it: it asserts `Assert.Single` only *within* the `Malformed`
  category. Fix: `i > chosenIndex`. (2) The message reads *"planning docs, glossary and workflow commands come
  from '{primary.Code}'"* with nothing gating that clause on `primary.IsModeled`, so a TEA+CIS repo emits it
  immediately before `ReportUnmodeledPrimary`'s notice saying the opposite. Fix: vary the clause on
  `IsModeled`. Separately, both notices anchor at `RepoRelativeCsv(primary.Code)` — the diagnostics Source column
  and the file-anchored Problems entry point the reader at the **primary's** blameless CSV for a notice about a
  different module; no notice ever names a skipped module's own file. *(blind+edge)*
- [x] [Review][Patch] **P4 — AC #3's "verified against **real** module `module-help.csv` content" is only half met —
  BMad Method's fixture is still synthetic in both test files** — MEDIUM.
  [tests/SpecScribe.Tests/ModuleContextTests.cs — `BmmCsv`; tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs
  — `BmmCsv`]. Both constants sit **outside** the new upstream-provenance block and are untouched by the diff.
  `ModuleContextTests.BmmCsv` is plainly invented (`"Prepare the next story, with commas, quoted"`,
  a `bmad-code-review` row); `SiteGeneratorHowToReadTests`' "Verbatim upstream rows, pinned exactly as in
  ModuleContextTests" comment sits *below* `BmmCsv` and covers `GdsCsv` onward only. The provenance block records
  commit SHAs for **four** modules — GDS, TEA, CIS, BMB — and the Debug Log likewise says "four
  `module-help.csv` files fetched". AC #3 names BMad Method **and** Game Dev Studio; GDS was correctly re-pinned,
  BMM was not, so every BMM-side AC #3 assertion (glossary, `prd.md`, `/bmad-create-story`) still rests on
  invented rows. Given Task 6's own premise — synthetic fixtures are *why this shipped undetected* — leaving the
  hole open for the one module that matters most defeats the task. Fix: fetch and pin
  `bmad-code-org/BMAD-METHOD` `src/module-help.csv` with its commit SHA, per ADR 0015 Decision 7's evidence
  table (which lists it). Requires a network fetch. *(auditor)*
- [x] [Review][Patch] **P5 — a manifest read failure, or a `BuildContext` that throws rather than returning null,
  discards the entire candidate set and returns `None` with no diagnostic** — LOW.
  [src/SpecScribe/ModuleContext.cs — `DiscoverCandidates` (`foreach (var name in ReadInstalledModules(bmadRoot))`),
  `Detect`'s descend-the-rank loop]. `ReadInstalledModules` calls `MarkdownConverter.ReadAllTextShared` and is
  **not** wrapped the way `SafeEnumerateDirectories` is, and it is enumerated eagerly *before* the disk loop — so
  a `_bmad/_config/manifest.yaml` that is exclusively locked, permission-denied or mid-write (i.e. during a BMad
  install) throws straight past the whole union to `Detect`'s outer `catch` ⇒ `None`, even though
  `_bmad/bmm/module-help.csv` is sitting on disk. Same shape one level down: `BuildContext`/`ParseCsv` have no
  per-candidate `try`, so a CSV deleted or locked between `FindModuleCsv`'s `File.Exists` and the read (a live
  watch-session race) aborts the loop and discards every lower-ranked, perfectly parseable module — Decision 4d's
  guarantee holds only for `BuildContext` returning `null`, not for it throwing. In both cases the operator gets
  `Detected framework: Unknown (not detected)`, indistinguishable from a repo with no BMad install, with nothing
  to act on. Note also that `Detect` mutates the **caller's** diagnostics list in place, so the outer catch can
  return `None` while the caller's list already carries `Malformed`/`Unsupported` notices naming a real code —
  a self-contradictory diagnostics page. Fix: wrap `ReadInstalledModules` inside `DiscoverCandidates`, add a
  per-candidate `try` in the loop, and either buffer diagnostics locally until success or emit a notice on the
  catch-all path. *(edge; note the Edge layer's claim that `IsModulePresent` returns `true` here is wrong — its
  own `try/catch` returns `false`, so the two agree)* 
- [x] [Review][Patch] **P6 — `RepoRelativeCsv` builds a lower-cased path, so a `_bmad/BMM/` diagnostic anchor points
  at a nonexistent file on case-sensitive filesystems** — LOW.
  [src/SpecScribe/ModuleContext.cs — `CodeOf` (`.ToLowerInvariant()`) → `RepoRelativeCsv`; consumed by
  `Commands.SerializeDiagnostics`'s new `DiagnosticAnchorRoot.Repo` arm]. `FindModuleCsv` matches the directory
  **case-insensitively**, so the real directory need not be lowercase, but every notice's `RelativePath` is
  `_bmad/{lowercased-code}/module-help.csv` and the `Repo` arm passes it through unchanged with
  `fileAnchored: true`. On Linux, `_bmad/BMM/` therefore yields a Problems entry the VS Code shim resolves to
  nothing — precisely the wrong-root failure the `Repo` anchor was introduced to prevent, reintroduced via
  casing. `ModuleContext.Code`'s doc-comment also claims it is "the `_bmad/{code}/` install-directory name",
  which it is not, so any future path construction from `Code` inherits the bug.
  `Detect_ModuleDirectoryCasing_DoesNotChangeIdentity` writes `_bmad/BMM/` but never asserts `ctx.Code`, so the
  lower-casing is unpinned. Fix: carry the real directory name for path construction (keep the lower-invariant
  form for comparison), and pin `Code` in that test. *(blind)*
- [x] [Review][Patch] **P7 — test-coverage gaps on the exact surfaces the ADR's arguments rest on** — LOW.
  Three items. (1) **No test pins `DiagnosticsTemplater.ModuleDisplay` for `Unmodeled`** — that row is the
  *entire* justification for Decision 2a (`Unmodeled` as a new case rather than a reuse of `Unknown`), and
  `DiagnosticsTemplaterTests` carries only the `"Unknown (not detected)"` case; Completion Note §4 rests on live
  inspection alone. (2) **No fixture exercises a `module-help.csv` without a usable `module` column** — the one
  input that reaches the `"BMad"` fabrication above; the `CommandCatalogEmpty_CarriesNoLabel_…` suite tests only
  the static `Empty` instance, so it passes while the live default survives. (3) Two assertions add nothing:
  `Detect_KnownModules_CarryTheirCode`'s `Assert.False(…IsUnmodeled)` is implied by its own
  `Code == "bmm"` assertion, and `Detect_ReservedBmadChild_…`'s `Assert.Empty(diagnostics)` passes partly for the
  wrong reason (with only `bmm` surviving, `ReportSecondaryModules` short-circuits on `ranked.Count < 2`
  regardless of the reserved-name logic under test). *(blind+auditor)*
- [x] [Review][Patch] **P8 — Completion Note §8 cites a code site by line number, the story's own anti-pattern #8,
  and it has already drifted** — LOW. §8 reads "Note Story 18.1's AC also references this phrase
  (**epics.md:3045**)"; that clause now lives at `_bmad-output/planning-artifacts/epics.md:3082`. The
  Additional-Requirements site (`:173`) is still accurate and *was* also anchored by phrase. The anti-pattern's
  primary requirement — do not delete the note silently — **was** honoured: both sites still carry "strongly
  GDS-oriented … requires generalization". Fix: re-anchor the §8 citation on the quoted phrase. *(auditor)*

**Deferred**

- [x] [Review][Defer] **`Informational` collapses to `"warning"` on the webview Problems wire**
  [src/SpecScribe/Commands.cs — `SerializeDiagnostics`: `severity = notice.Severity == DiagnosticSeverity.Error ?
  "error" : "warning"`] — deferred, **pre-existing**. The switch has no `Info`/Hint arm, so this story's
  "FYI, nothing to do" unmodeled notice reaches the VS Code Problems panel as a **Warning**, file-anchored, on
  every run — against the owner constraint "it must not read as an error". But `SiteGenerator` already emitted
  `Informational` before this story (the unrecognized-top-level-folder notice, Story 4.2), so 18.2 is the second
  emitter through a pre-existing collapse, not its cause. `DiagnosticsTemplater` maps it correctly to
  `DiagnosticSeverity.Info` / the `diag-info` word badge, so the HTML surface this story verified live is right.
  *(edge)*
- [x] [Review][Defer] **`IsModulePresent` made `public` and a new `public ForCode` are Story 18.5's work living
  in 18.2's primary file** [src/SpecScribe/ModuleContext.cs — `IsModulePresent`, `ForCode`] — deferred, **sibling
  story owns it**. Both XML doc blocks self-attribute to 18.5 ("PUBLIC since Story 18.5…", "[Story 18.5; ADR 0015
  Decision 1]") and both are consumed from `src/SpecScribe/TestArtifactDiscovery.cs`. Neither appears in this
  story's File List, and ADR 0015 **Decision 3b** — declared out of scope here — is explicitly the decision that
  "adds **new public surface** (`IsPresent`); `IsModulePresent` is private today". Attribution/scope drift rather
  than a defect; flagged so 18.5's review does not skip these two symbols on the assumption 18.2 covered them.
  Confirmed **Decision 5a did not leak in**: `ArtifactCoverage.SpecsFor(BmadModule)` exists in the working tree
  but is Story 18.6's work and outside both the File List and the diff. *(auditor)*
- [x] [Review][Defer] **`ForCode` has no diagnostics sink, so the Decision-1c demotion is silent on the Test
  Artifacts path** [src/SpecScribe/ModuleContext.cs — `ForCode` calls `BuildContext(csv)` with `diagnostics`
  omitted] — deferred, **Story 18.5 owns `ForCode`**. `TestArtifactDiscovery.Discover` → `ForCode(repoRoot,
  "tea")` against a CSV that hits the label default would name the module "BMad" with no notice anywhere, where
  `Detect` would have reported the same condition. Fix belongs with 18.5's own review; it depends on the
  `moduleLabel` patch above. *(edge)*

**Dismissed as noise (9)** — recorded so a later review does not re-raise them:
`DocsFor` returning empty for `Unmodeled` drops the PRD/Architecture **nav** entries for an unmodeled repo
(AC #1 literally mandates "no planning docs"; the pages themselves still render) · `AppendCommandLegend`'s
`!IsModeled` gate suppressing the legend for an unmodeled module that does parse real commands (ADR 0015
Decision 2c ratifies gating on a modeled primary; consequence is one missing explanatory sentence) ·
`_bmad/custom/` being reserved even when the manifest declares it (ADR 0015 Decision 1a lists the reserved set
and specifies silent skipping) · `AppendGlossary`'s comment claiming the `#glossary` anchor has in-page referrers
when a repo-wide search finds none (the *behaviour* is an explicit owner constraint and is correct; only the
comment's rationale is unverifiable) · two `_bmad/` directories differing only in case on a case-sensitive
filesystem collapsing non-deterministically (not a realistic checkout) · `Detect(repoRoot, null)` NRE-ing into
the outer catch with ≥2 candidates (no production caller; the parameter is non-nullable and it degrades to
`None`) · `BuildNav` no longer re-detecting freezing module identity for a whole watch session (deliberate,
ADR 0015 Decision 2d; `_bmad/**` never reached the watch dispatch anyway, so only an incidental `.md` edit ever
re-detected, and 4 of 5 call sites passed an empty source list making the tie-break wrong every time) ·
`RegenerateEpics`/`GenerateOne` rendering with `_module = ModuleContext.None` if invoked before any full run
(unreachable — watch and webview startup both run `GenerateAll` first) · `moduleLabel` being last-row-wins
(pre-existing; folded into the label-drift decision above, where it is the amplifier).

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

`AboutSddTemplater` needed no change **for the manifest-order contradiction this story targeted**:
`IsMethodPresent`/`IsGdsPresent` are independent presence checks, so fixing the ranking made "Detected" and the
primary agree in that case on their own. (Decision 3c's `ModuleCode` field on the `Frameworks` roster sits under
Decision 3 and is out of scope.)

> ⚠️ **Corrected at code review, 2026-07-27 [Review][Patch P11].** The original wording of this note —
> *"`AboutSddTemplater` needed **no** change: … fixing the ranking made 'Detected' and the primary agree on
> their own"* — was too broad. Two contradictions remain reachable, both through paths this story introduced or
> left open, and **AC #2's trailing clause is therefore _partially_ satisfied, not satisfied**:
>
> 1. **A 1c-demoted squatter still reports "Detected."** `IsGdsPresent` is a pure presence check and knows
>    nothing about the label cross-check, so the very fixture this story's own
>    `Detect_MintedModuleSquattingAModeledCode_IsDemotedToUnmodeled_AndReported` builds — `_bmad/gds/` labelled
>    *"Totally Not GDS"* — renders "BMad GDS — Supported, **Detected**" and the `sdd-detected-banner`, while
>    how-to-read says the module is unmodeled and no GDS docs, glossary or commands exist.
> 2. **A partial install still reports "Detected."** A manifest listing `bmm` with no `_bmad/bmm/module-help.csv`
>    on disk (pinned by the pre-existing `IsMethodPresent_TrueWhenManifestListsBmmWithoutCsv`) beside an
>    installed `_bmad/tea/` gives "BMad — Detected" while the primary is Unmodeled TEA. Note this pair agreed
>    **by accident** before the fix, because TEA was misidentified as `BmadMethod`.
>
> Owner's call (D3, 2026-07-27): **correct the record, not the code.** The presence checks stay independent —
> that is their documented contract, and Story 18.5's test-artifact gating now depends on it. `DiscoverCandidates`'
> doc-comment was also narrowed in the same patch: its claim that `IsModulePresent` and `Detect` "must not
> disagree" overstated the union's reach, since `(manifest ∪ disk) ∩ has-csv` and `manifest OR disk-csv` still
> diverge for a manifest entry with no CSV.

**5b. Accepted behaviour, recorded so it is not later "fixed" as a bug [Review][Patch P12].** A **non-primary**
candidate whose `module-help.csv` will not parse emits `Malformed` (ADR 0015 Decision 4d mandates that category),
and `SiteGenerator.MapDiagnostics`' pre-existing rule maps `Malformed` → `GenerationOutcome.Error`. Two
observable consequences follow, on a run whose site generated **completely correctly**: `errors=1` on
`GenerationSummary`'s machine summary line — the record CI greps — and `RegenerateTopology` /
`RegenerateFromDataSource` reporting every watch rebuild as failed, since both collapse to
`events.FirstOrDefault(e => e.Outcome == GenerationOutcome.Error)`. Pre-story this path was silent. The owner
**accepted this** at code review (D4, 2026-07-27): a broken module catalog is worth failing the run's status
line even when the output is fine. Two ratified contracts genuinely collide here; the resolution is deliberate,
not an oversight.

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
line and the call is the owner's. Note Story 18.1's AC also references this phrase — locate that second site by
the same quoted string, in the Story 18.1 section of `epics.md`, so retiring it updates both.

> ⚠️ **Corrected at code review, 2026-07-27 [Review][Patch P8].** This sentence originally cited the second site
> as `epics.md:3045` — a code/artifact site pinned by **line number**, which is this story's own anti-pattern #8,
> and it had already drifted to `:3082` by review time. Re-anchored on the quoted phrase. (The anti-pattern's
> primary requirement was honoured: the note was proposed for retirement, not silently deleted, and both sites
> still carry it.)

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

### Code-review verification (2026-07-27, all 13 patches applied)

**Suite.** The classes this story owns are **111/111 green** — `ModuleContextTests`, `SiteGeneratorHowToReadTests`,
`DiagnosticsTemplaterTests`, `CommandCatalogTests`, `BmadArtifactAdapterTests` and the golden gate. Full suite:
2586 passed / 13 failed / 3 skipped. **None of the 13 is in a class this story or this review touches** — they
are `StylesheetTests`, `SiteGeneratorCodeMapTests`, `SiteGeneratorWebviewTests` and `SiteGeneratorGitInsightsTests`,
all asserting an *ownership sunburst / treemap / component-selector* feature this story has no contact with, and
all of whose source files (`specscribe.css`, `specscribe.js`, `CodeMapTemplater.cs`, `GitInsightsTemplater.cs`,
`Charts.cs`, `HierarchyExplorer*.cs`) were uncommitted and being actively edited by a concurrent session
throughout. Distinguishing evidence, and note it differs from the deep-git contention flake this story's §7
recorded: **the failing set shifted between two consecutive runs (13 → 9) and a new name appeared in the second**
(`Stylesheet_HasNoSecondShapeToggle_TheComponentSelectorReplacedIt`) — that is a session mid-implementation, not
load. **This is not a claim the suite is green.** It is a claim, with that evidence, that none of the failures is
attributable to this review.

**Golden fingerprint: re-baselined `2bd1c18e…` → `f4a7cbac…`, and the move IS this review's** — patch P2, the only
one that changes rendered bytes. Causality proven, not assumed: a `git worktree` at HEAD `d1722f1` ran the golden
test **green on the old constant**, so the move came from the working tree, and the only other working-tree
changes are a concurrent comment-only edit, a new `.gitattributes` and a `.mjs` — none reachable by the C#
generator. Confirmed byte-identical across two consecutive runs before locking in, after an explicit rebuild (the
stale-build trap). Why a fixture with no `_bmad/` moved at all: *because* it has none — `_module` is
`ModuleContext.None`, so it takes P2's new middle branch. The no-module path had been making the same false
glossary promise the unmodeled path was, and P2 corrects both. Full reasoning is in the stacked comment above the
constant in `SiteGeneratorAdapterTests`.

**Live browser (CLAUDE.md § Verification), both paths.** Generated against a scratch fixture carrying the **real**
upstream `_bmad/tea/module-help.csv`, served over HTTP and inspected through the DOM:

- **Unmodeled path** — subtitle reads *"New here? Start with the reading order below…"* (the glossary promise is
  gone) and the intro no longer says *"what the recurring terms mean"*, while `<h2 id="glossary">` still renders
  and its anchor resolves to a real **105 × 33** box, so in-page links still land. Acknowledgement text verbatim;
  no `<dl>`; no `#commands` section; **0 `<abbr>`** anywhere. Diagnostics shows the notice badged as the WORD
  `Informational` (`status-badge diag-info`) — not colour alone — anchored at `_bmad/tea/module-help.csv`, and
  **`Detected framework = Test Architecture Enterprise`**, the Decision-2a surface, now also pinned by a test
  rather than resting on inspection. `errors=0`.
- **Modeled path unchanged (AC #3)** — this repo's own BMM portal, **432 pages, `errors=0`**: subtitle and intro
  keep both promises, the glossary `<dl>` carries all **10** terms, the legend still reads *"…detected
  methodology, BMad Method"*, all **5** `<abbr>` expansions survive on `epics.html`,
  `Detected framework = BMad Method`, and **no** unmodeled notice leaked.

⚠️ **Final re-run blocked by a concurrent session; last clean run stands.** After all 13 patches were applied and
verified green (111/111 on this story's classes, golden gate included), a concurrent session's in-flight edit
broke the **test project's compilation**: `SiteGeneratorSpaTests.cs` gained +31 lines calling a `SourceRoot()`
helper that does not exist yet (`error CS0103`). That file is not in this story's File List and was never touched
by this review — `git diff` confirms none of those lines are mine. Per CLAUDE.md § Concurrent work their
uncommitted work was left strictly alone rather than "fixed", so **no post-completion re-run was possible**. The
green result above is from the run immediately before their breakage; all patch edits were re-grep-verified
present on disk afterwards. **A re-run once their edit compiles is the confirmation this record does not have.**

⚠️ **Not verified: a pixel screenshot.** The Browser pane was not displayed, so the page never composited —
`documentElement.clientWidth` read `0` and `computer{screenshot}` timed out. Every finding above is DOM- and
geometry-based (real bounding boxes), which is what the P2 claims need, but **the horizontal-overflow check could
not be performed** and is not claimed. Generation used scratch output dirs; `--output docs/live` was never used.

## Change Log

- 2026-07-27 — **Code review complete, status → done.** Three parallel adversarial layers (Blind Hunter, Edge
  Case Hunter, Acceptance Auditor), scoped by File List and declared symbols per CLAUDE.md — sibling stories
  18.4/18.5/20.8/23.5/25.2/25.3 in the same commit range excluded. 13 actionable findings, 3 deferred, 9
  dismissed. All five `decision-needed` items were resolved by the owner and all 13 patches applied. The
  headline defect, found independently by all three layers: `BuildContext` still fabricated the label `"BMad"`,
  so ADR 0015 Decision 2b was unenforced at the parse site, **all three new `HasLabel` guards were dead code**,
  and a `module`-column-less CSV at `_bmad/bmm/` demoted a genuine BMM install. Also fixed: the unmodeled
  acknowledgement fed the how-to-read honesty gate (Story 5.6's rule, re-broken); `ReportSecondaryModules`
  double-reported unparseable candidates with a contradictory explanation; a 1c-demoted squatter could take the
  primary slot from a genuine modeled module (AC #2); the label cross-check was brittle to documented upstream
  label drift; and BMad Method's test fixture was the last synthetic one, leaving AC #3 half-met — now pinned to
  real upstream bytes at `bb45db4a`. **ADR 0015 gained Amendment 1** in the same change (Decisions 1c, 4d and 4e
  amended; two tensions accepted and recorded), so the code and the ratified text never disagree. Golden
  fingerprint re-baselined `2bd1c18e…` → `f4a7cbac…`, causality proven by a worktree run at HEAD. AC #2's
  trailing clause downgraded to **partially satisfied** and Completion Note §5 corrected — the residual About-SDD
  contradictions are recorded, not silently closed.
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
