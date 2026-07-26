# ADR 0015: BMad Module Identity Is Open-World and Multi-Valued

**Status:** Proposed (awaiting owner ratification — surfaced by the Story 18.1 spike, 2026-07-25)
**Date:** 2026-07-25
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0001](0001-spec-driven-development-framework.md) (BMAD is the framework SpecScribe is built on and renders); [ADR 0002](0002-shared-rendering-core-and-host-neutral-view-models.md) (module identity is part of the host-neutral projection this ADR keeps honest); Epic 18 (Stories 18.1, 18.2); Epics 11–15 (**deliberately not** reopened — see Non-goals); PRD **NFR8** (honest absence); FR36, FR-19

## Context

SpecScribe detects which BMad methodology module produced a source repo, and uses that identity to publish a
module's well-known planning docs, its portal glossary, and its workflow slash-commands. Today that identity is
a closed, single-valued enum — `BmadModule { Unknown, BmadMethod, GameDevStudio }` [`ModuleContext.cs:8`] — and
`ArtifactBundle.Module` carries exactly one `ModuleContext` [`ArtifactBundle.cs:15`].

The Story 18.1 spike surveyed BMad's own module ecosystem beyond BMM and GDS and found that this model is not
merely incomplete — **it actively misreports**, today, on repos that exist now.

### 1. Identity is inferred from the wrong key

`BuildContext` derives the module from the *leading token of a skill id*
[`ModuleContext.cs:346-348`]:

```csharp
var module = prefix.StartsWith("gds", StringComparison.OrdinalIgnoreCase)
    ? BmadModule.GameDevStudio
    : BmadModule.BmadMethod;
```

Verified against the real `module-help.csv` of every first-party module repo:

| Module | Module code (install dir) | Skill ids | Inferred `prefix` | Identified as |
|---|---|---|---|---|
| BMad Method | `bmm` | `bmad-create-story` | `bmad` | BmadMethod ✅ |
| Game Dev Studio | `gds` | `gds-gdd`, `gds-create-story` | `gds` | GameDevStudio ✅ |
| Creative Intelligence Suite | `cis` | `bmad-cis-innovation-strategy` | `bmad` | **BmadMethod ❌** |
| Test Architect (Enterprise) | `tea` | `bmad-testarch-trace` | `bmad` | **BmadMethod ❌** |
| BMad Builder | `bmb` | `bmad-bmb-setup` | `bmad` | **BmadMethod ❌** |

**GDS is correct only by coincidence** — it is the single module whose skill prefix happens to equal its module
code. Every other first-party BMad module prefixes its skills `bmad-`. The prefix is a naming habit, not a
contract, and SpecScribe has been keying identity off it.

Note also that `BmadModule.Unknown` exists but `BuildContext` **never returns it**. A well-formed *foreign*
module CSV does not degrade to unknown; it falls through to `BmadMethod`. `ModuleContext.None` is reachable only
when no CSV parses at all.

### 2. The consequence is false presence, not honest absence

Verified empirically (a throwaway probe over eight synthetic repos built from the modules' real
`module-help.csv` bytes), a repo whose only installed module is CIS, TEA, or BMB reports
`Module = BmadMethod`, `Docs = prd.md, ARCHITECTURE-SPINE.md, brief.md, DESIGN.md, EXPERIENCE.md`, and BMM's
full ten-term glossary.

The blast radius is **bounded, and worth stating precisely** rather than overstated:

| Surface | Affected | Why |
|---|---|---|
| Module docs in nav / quick links | **No** | `SiteNav.cs:206-215` skips any `ModuleDoc` with no filename match on disk. A CIS repo has no `prd.md`, so no phantom link. Self-limiting. |
| "Next Steps" command panels | **No** | Every `Command()` lookup misses → `null` → ~40 call sites omit. Correct NFR8 degradation. |
| Glossary on `how-to-read.html` | **Yes** | `HowToReadTemplater.cs:176` gates only on `glossary.Count == 0`. |
| Every rendered page | **Yes** | `SiteGenerator.cs:4270` runs `AbbreviationExpander.Expand(html, _module.Glossary)` site-wide. |

So on the two surfaces that are *not* file-gated, SpecScribe asserts a vocabulary — FR, NFR, AC, ADR, PRD,
"spec kernel", "sprint" — that the project provably does not use. **NFR8 requires surfaces to be "absent, not
broken or misleadingly empty." This is the fourth case: confidently wrong.**

### 3. Single-winner selection produces a live regression on repos that already work

`ChoosePrimary` [`ModuleContext.cs:259-286`] must return exactly one winner. Among non-GDS candidates it
returns `candidates.FirstOrDefault(...)` — i.e. **installed-manifest order**. Probe-verified:

```
bmm+cis (bmm first) → ModuleLabel "BMad Method"                 /create-story = /bmad-create-story   IsMethodPresent=True
cis+bmm (cis first) → ModuleLabel "Creative Intelligence Suite"  /create-story = (null)               IsMethodPresent=True
bmm+tea (tea first) → ModuleLabel "Test Architecture Enterprise" /create-story = (null)               IsMethodPresent=True
```

A repo that **genuinely has BMM installed** loses **every** BMM command suggestion portal-wide because a
sibling module won a manifest-order tie — while `IsMethodPresent` still returns `True`
[`ModuleContext.cs:165`], so the About-SDD page simultaneously reports "BMad — Supported, Detected". That is an
internally contradictory portal, it is install-order dependent (therefore intermittent), and it fires the first
time an owner adds TEA or CIS to an existing BMM project.

This is a **regression to already-shipped BMM support**, not a gap in new-module coverage. It is the highest-severity
item in Epic 18.

### 4. The module set cannot be enumerated

BMad Builder's entire purpose is generating custom modules with **arbitrary, user-chosen codes**, each shipping
a `module.yaml` (`code:`) and a `module-help.csv`. Because `Detect` treats *any* non-`core`
`_bmad/*/module-help.csv` as a candidate [`ModuleContext.cs:204-218`], a BMB-generated custom module is already
a live input today — and hits §1 exactly as CIS and TEA do. The first-party set is also larger and still growing
(`bmad-loop`, `bmad-automator`, `bmad-manticore`, `bmad-method-ui`, `bmad-method-wds-expansion`, a plugins
marketplace).

**No closed enumeration of module codes can be correct.** Adding three enum cases would fix three repos and
leave the shape of the bug intact.

### 5. What is already generic (and should not be rebuilt)

The spike found the generic/hardcoded seam sits *inside* `BuildContext`, not between it and the doc tables:

- **Fully generic:** install discovery via `_bmad/_config/manifest.yaml` with an `_bmad/*/module-help.csv`
  disk fallback (`core` correctly excluded); `IsModulePresent(repoRoot, code)` [`ModuleContext.cs:174-188`]
  already takes an arbitrary code — only its two public wrappers are hardcoded; CSV → `byStep` +
  `ModuleLabel` (the label comes through **correctly** for CIS/TEA/BMB).
- **Hardcoded:** the §1 identity line, and the `DocsFor` / `GlossaryFor` switches
  [`ModuleContext.cs:118-123, 151-156`].

Two on-disk facts constrain any fix: `module.yaml` (which carries a clean `code:`/`name:`) is an installer
**source** file and is **not installed**; and `_bmad/{code}/config.yaml` carries no module identity — indeed
SpecScribe reads no module `config.yaml` at all today. **The `module` column of
`_bmad/{code}/module-help.csv`, plus the containing directory name, are the only on-disk identity signals.**

## Decision

**1. Module identity derives from the module *code* — the `_bmad/{code}/` directory name — never from a skill
prefix.** The directory name is the module code (`bmm`, `gds`, `cis`, `tea`, `bmb`, and any BMB-minted code) and
is already the key `ChoosePrimary` and `IsModulePresent` use. The `prefix.StartsWith("gds")` inference in
`BuildContext` is retired.

**2. An unrecognized module code is a first-class, well-behaved outcome — never a fallback to `BmadMethod`.**
Such a module resolves to `BmadModule.Unknown` **with** its real `ModuleLabel` from the CSV, an empty doc set,
an empty glossary, and its parsed `CommandCatalog` intact. It emits one
`AdapterDiagnosticCategory.Informational` diagnostic — the category [`AdapterDiagnostic.cs:26-31`] was written
for exactly this "FYI, nothing to do" case:

> `Detected BMad module '{code}' ({label}); SpecScribe has no module-specific docs or glossary for it, so those sections are omitted.`

This converts §2's silent misattribution into honest, reported absence, satisfying NFR8.

**3. `ModuleContext` carries the *set* of installed modules with a designated primary, rather than a single
winner.** Real BMad repos are increasingly multi-module (BMM + TEA + CIS). `ArtifactBundle.Module`
[`ArtifactBundle.cs:15`] stays a single required, never-null `ModuleContext` for compatibility, but that context
gains the full installed set. `IsMethodPresent` / `IsGdsPresent` [`ModuleContext.cs:165,170`] — today's
independent dual-presence workaround, and the only reason the About-SDD support matrix is correct — are
generalized to `IsPresent(code)` over that set; the two existing wrappers may remain as thin conveniences.

**4. Primary selection must never demote BMM or GDS.** Until Decision 3 lands in full, `ChoosePrimary` ranks
`bmm`/`gds` above auxiliary modules instead of relying on manifest order. This is a small, independently
shippable change and it alone closes the §3 regression.

**5. Adding a module's docs/glossary stays an explicit, per-module act.** Decision 2 makes unknown modules
*safe*, not *covered*. A module gains a `ModuleDoc[]`, a `GlossaryTerm[]`, an `AboutSddTemplater.Frameworks`
row [`AboutSddTemplater.cs:10-18`], a `SiteNav` output path, and a `README.md` support-table row only when a
story deliberately covers it. `AboutSddTemplater`'s `detected` switches [`:38-43`, `:66-70`] currently take two
bools and must widen under Decision 3.

**6. Epic 18 extends the existing adapter and does *not* require the adapter registry.**
`BmadArtifactAdapter.AppliesTo` markers `_bmad/` wholesale [`BmadArtifactAdapter.cs:76-77`], so every BMad
module — including BMB-generated ones — already self-selects into it; a second `IArtifactAdapter` would carry an
identical `AppliesTo`, making registry selection ambiguous rather than useful. Epic 18 is therefore the one
framework epic that can proceed while the `SiteGenerator.cs:51` registry gap stays open.

**7. Test fixtures for module detection are pinned to real module CSV content.** The repo's current fixtures use
synthetic `gds-*` rows (`ModuleContextTests.cs:57-58`), which is precisely why §1 went unnoticed: BMad's docs
advertise GDS commands as `/bmgd-*`, and had that been the on-disk reality the suite would still have passed.
(It is not — GDS's real CSV uses `gds-*` and its `module.yaml` says `code: gds`; **BMGD is branding**. Current
GDS support is correct.)

## Non-goals

- **Reopening the cross-framework adapter registry.** That decision belongs to Epics 11–15; per Decision 6
  Epic 18 does not need it. This ADR must not become a sixth competing registry proposal.
- **Covering CIS or BMB artifacts.** Story 18.1 recommends TEA as the priority coverage module; CIS's output
  already renders via the generic markdown pass, and BMB is a meta-tool whose outputs are other modules'
  scaffolding.
- **Reading module `config.yaml` for output paths.** TEA writes to a `test_artifacts` key SpecScribe does not
  read; that is a real Story 18.2 prerequisite but a separate, non-architectural decision.
- **Generalizing the next-step command vocabulary.** Assessed and rejected as unnecessary — see Consequences.

## Consequences

**Positive**
- Removes a live correctness defect: SpecScribe stops asserting BMM's vocabulary over projects that do not use it.
- Closes an install-order-dependent regression that would otherwise strike the first owner to add TEA or CIS to a BMM repo.
- Makes the module ecosystem's open-endedness a supported property rather than an unbounded source of future bugs — BMB-minted custom modules included.
- Identity moves onto the key two of three existing call sites already use, so the codebase becomes more internally consistent, not less.
- Multi-module repos become representable, retiring the ad-hoc `IsMethodPresent`/`IsGdsPresent` workaround.

**Negative / trade-offs**
- Touches a cross-cutting contract (`ModuleContext`, and what `ArtifactBundle.Module` means), so it is not a local fix.
- Decision 3 is the larger piece and may reasonably land after Decisions 1, 2 and 4, which are small and independently valuable.
- Every consumer of `ModuleContext.Module` must tolerate `Unknown` **with** a real label and a populated command catalog — a state that cannot occur today.
- `AboutSddTemplater`'s two-bool `detected` signature and the `Frameworks` roster need widening.
- Detection tests must be re-pinned to real module CSVs (Decision 7), which is unglamorous work.

**Explicitly unchanged**
- The `AdapterDiagnostic` five-value vocabulary. Decision 2 uses `Informational` as designed; no sixth category.
- The next-step command **mechanism**, which is already module-neutral: `BuildContext` strips the prefix and
  keys on the step remainder, so `/bmad-create-story` and `/gds-create-story` both resolve `create-story` with
  no module-specific code. The residual BMM∩GDS *step vocabulary* hardcoded at ~40 call sites needs no
  generalization, because those surfaces (sprint board, epics, story pages) only exist when epics and stories
  exist — which only BMM and GDS produce. For a TEA or CIS repo the panels correctly vanish.
  **`epics.md:157`'s note that the mapping is "strongly GDS-oriented and requires generalization" is stale and
  should be retired**; it predates the CSV-driven `CommandCatalog`.

## Options considered

| Option | Verdict |
|---|---|
| **Leave as-is** | Rejected. §2 is a live NFR8 violation and §3 a live regression, both on module combinations that ship today. |
| **Add `Cis`/`Tea`/`Bmb` enum cases, keep prefix inference** | Rejected. Prefix inference cannot distinguish them — all three yield `bmad` — so the cases would be unreachable. It also cannot survive BMB-minted codes. Fixes three repos, leaves the bug's shape intact. |
| **Key identity on the module code, keep a closed enum** | Rejected as insufficient alone. Correct for known modules, but still misidentifies every unknown code unless paired with Decision 2's first-class `Unknown`. |
| **Key on module code + first-class unknown + multi-valued set** | **Chosen.** Correct for known modules, safe and honest for unknown ones, and representative of real multi-module repos. |
| **New `IArtifactAdapter` per BMad module, via the registry** | Rejected. All BMad modules share `AppliesTo` (`_bmad/`), so the registry cannot discriminate between them; it would import Epics 11–15's unbuilt dependency for no benefit. |

## Open questions for ratification

1. **Scope split** — land Decisions 1, 2 and 4 in Story 18.2 as a prerequisite slice, deferring Decision 3
   (multi-valued set) to its own story? The spike recommends yes: 1/2/4 are small and close the live defects,
   while 3 is the contract change.
2. **Does `ArtifactBundle.Module` stay singular?** This ADR proposes yes (the set lives inside `ModuleContext`).
   The alternative — a `Modules` collection on the bundle — is a wider blast radius for the same outcome.
3. **`Unknown` naming.** With Decision 2, `Unknown` no longer means "detection failed" but "recognized module,
   not one SpecScribe models." A clearer name (e.g. `Unmodeled`) may be worth the rename; `ModuleContext.None`
   remains the genuine no-detection state.

## References
- **The spike that surfaced this:** Story 18.1 — `_bmad-output/implementation-artifacts/18-1-bmad-module-landscape-and-coverage-spike.md` (Completion Notes §3, §4, §8).
- **The identity line:** `src/SpecScribe/ModuleContext.cs:346-348`; the enum, `:8`.
- **The single-winner selector:** `src/SpecScribe/ModuleContext.cs:259-286`; presence checks, `:165,170`; the generic core, `:174-188`, `:204-218`.
- **The hardcoded tables:** `src/SpecScribe/ModuleContext.cs:118-123, 151-156`.
- **The unconditional surfaces:** `src/SpecScribe/HowToReadTemplater.cs:176`; `src/SpecScribe/SiteGenerator.cs:4270`. The file-gated one: `src/SpecScribe/SiteNav.cs:206-215`.
- **The adapter marker:** `src/SpecScribe/BmadArtifactAdapter.cs:76-77`; the registry gap, `src/SpecScribe/SiteGenerator.cs:51`.
- **The diagnostic category:** `src/SpecScribe/AdapterDiagnostic.cs:26-31`.
- **The support roster:** `src/SpecScribe/AboutSddTemplater.cs:10-18, 38-43, 66-70`; `README.md:19-24`.
- **The stale note:** `_bmad-output/planning-artifacts/epics.md:157`.
- **The requirement:** PRD **NFR8** (`epics.md:99`) — framework-specific content flows through the adapter contract; surfaces degrade *absent*, not misleading.
- **Upstream evidence:** the real `module-help.csv` / `module.yaml` of `bmad-code-org/bmad-module-game-dev-studio`, `bmad-module-creative-intelligence-suite`, `bmad-method-test-architecture-enterprise`, and `bmad-builder`.
