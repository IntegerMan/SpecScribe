# ADR 0015: BMad Module Identity Is Open-World and Multi-Valued

**Status:** **Accepted** — ratified 2026-07-26; **amended 2026-07-27** (Amendment 1, from the Story 18.2 code
review: Decisions **1c**, **4d** and **4e** are modified — read them together with
[Amendment 1](#amendment-1--2026-07-27-from-the-story-182-code-review)).
**Date:** 2026-07-25 (drafted) · 2026-07-26 (ratified) · 2026-07-27 (amended)
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0001](0001-spec-driven-development-framework.md) (BMAD is the framework SpecScribe is built on and renders); [ADR 0002](0002-shared-rendering-core-and-host-neutral-view-models.md) (module identity is part of the host-neutral projection this ADR keeps honest); Epic 18 (Stories 18.1, 18.2, 18.5); Epics 11–15 (**deliberately not** reopened — see Non-goals); **NFR8** (honest absence); FR36, FR-19

> **Citation policy.** Code is cited by **symbol**, not line number. An earlier revision of this ADR pinned
> line numbers that were correct at Story 18.1's baseline `611097d` and had drifted within days — inside
> `ModuleContext.cs` itself, and in `SiteGenerator.cs` under concurrent editing. Line numbers appear only
> where a symbol cannot identify the site, and are then marked *(as of 2026-07-26)*. Requirement citations
> name the requirement id and quote it rather than pointing at a line; **NFR8** is defined in
> `_bmad-output/planning-artifacts/epics.md` (search `NFR8:`) and is subject to the known unresolved
> PRD-vs-`epics.md` NFR numbering collision — this ADR means the *epics.md* NFR8, quoted in full in §2.

## Context

SpecScribe detects which BMad methodology module produced a source repo, and uses that identity to publish a
module's well-known planning docs, its portal glossary, and its workflow slash-commands. Today that identity is
a closed, single-valued enum — `BmadModule { Unknown, BmadMethod, GameDevStudio }` [`ModuleContext.BmadModule`] —
and `ArtifactBundle.Module` carries exactly one `ModuleContext`.

The Story 18.1 spike surveyed BMad's own module ecosystem beyond BMM and GDS and found that this model is not
merely incomplete — **it actively misreports**, today, on repos that exist now.

### 1. Identity is inferred from the wrong key

`ModuleContext.BuildContext` derives the module from the *leading token of a skill id*:

```csharp
var module = prefix.StartsWith("gds", StringComparison.OrdinalIgnoreCase)
    ? BmadModule.GameDevStudio
    : BmadModule.BmadMethod;
```

Verified against the real `module-help.csv` of every first-party module repo (see
[Upstream evidence](#upstream-evidence-provenance) for exact sources and retrieval dates):

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

Verified empirically (a probe over eight synthetic repos built from the modules' real `module-help.csv` bytes —
see [Upstream evidence](#upstream-evidence-provenance)), a repo whose only installed module is CIS, TEA, or BMB
reports `Module = BmadMethod`, `Docs = prd.md, ARCHITECTURE-SPINE.md, brief.md, DESIGN.md, EXPERIENCE.md`, and
BMM's full ten-term glossary.

The blast radius is **bounded, and worth stating precisely** rather than overstated:

| Surface | Affected | Why | Closed by |
|---|---|---|---|
| Module docs in nav / quick links | **No** | `SiteNav`'s `moduleDocs` loop skips any `ModuleDoc` with no filename match on disk. A CIS repo has no `prd.md`, so no phantom link. Self-limiting. | n/a |
| "Next Steps" command **suggestions** | **No** | Every `CommandCatalog.Command()` lookup misses → `null` → the ~43 call sites omit. Correct NFR8 degradation. | n/a |
| Command **legend** on `how-to-read.html` | **Yes** | `HowToReadTemplater.AppendCommandLegend` gates on `commands.IsEmpty`, **not** on a `Command()` miss. A CIS/TEA/BMB repo has a *non-empty* catalog, so the always-rendered `how-to-read.html` asserts "Slash commands like the ones captioned on story and epic pages come from your detected methodology, **{ModuleLabel}**" on a site that has no story or epic pages. In the dual-install case it names the **losing** module. | Decisions 1, 2, 4 + the §Decision 2 legend gate |
| Glossary on `how-to-read.html` | **Yes** | `HowToReadTemplater.AppendGlossary` gates only on `glossary.Count == 0`. | Decisions 1, 2 |
| Every rendered page | **Yes** | `SiteGenerator` runs `AbbreviationExpander.Expand(html, _module.Glossary)` site-wide. | Decisions 1, 2 |
| About-SDD "Detected" badge | **Partly** | `IsMethodPresent`/`IsGdsPresent` are independent of `ChoosePrimary` and individually correct — a CIS-only repo shows neither as Detected. But that independence is exactly what makes §3 self-contradictory: the badge says "BMad — Detected" while the primary is CIS. | Decision 3 |
| Dashboard **artifact-coverage** panel | **Yes — and NOT closed by Decisions 1/2** | `ArtifactCoverage.Specs` hardcodes the eight BMM families (PRD / Product Brief / Architecture / UX / Spec Kernel / Epics / Stories / Requirements), keyed off `ModuleContext.WellKnownDocs`, and is built from `sourceRelatives` alone with **no reference to `ModuleContext.Module` at all**. A TEA- or CIS-only repo gets a panel asserting eight missing BMM families — the same "asserts a vocabulary the project does not use" defect — and because it never consults module identity, fixing identity does not reach it. | **Decision 5** (explicitly, as its own slice) |

So on the surfaces that are *not* file-gated, SpecScribe asserts a vocabulary — FR, NFR, AC, ADR, PRD,
"spec kernel", "sprint" — that the project provably does not use. **NFR8** requires that

> *"surfaces degrade gracefully — absent, not broken or misleadingly empty — when a methodology lacks the
> corresponding artifact."*

**This is the fourth case, and the one NFR8 does not name: confidently wrong.**

### 3. Single-winner selection produces a live regression on repos that already work

`ChoosePrimary` must return exactly one winner. Among non-GDS candidates it returns
`candidates.FirstOrDefault(c => !DirName(c).StartsWith("gds", …))` — i.e. **installed-manifest order**.
Probe-verified:

```
bmm+cis (bmm first) → ModuleLabel "BMad Method"                 /create-story = /bmad-create-story   IsMethodPresent=True
cis+bmm (cis first) → ModuleLabel "Creative Intelligence Suite"  /create-story = (null)               IsMethodPresent=True
bmm+tea (tea first) → ModuleLabel "Test Architecture Enterprise" /create-story = (null)               IsMethodPresent=True
```

A repo that **genuinely has BMM installed** loses **every** BMM command suggestion portal-wide because a
sibling module won a manifest-order tie — while `IsMethodPresent` still returns `True`, so the About-SDD page
simultaneously reports "BMad — Supported, Detected", and `how-to-read.html`'s command legend names the winner.
That is an internally contradictory portal, it is install-order dependent (therefore intermittent), and it fires
the first time an owner adds TEA or CIS to an existing BMM project.

This is a **regression to already-shipped BMM support**, not a gap in new-module coverage. It is the
highest-severity item in Epic 18.

### 4. The module set cannot be enumerated

BMad Builder's entire purpose is generating custom modules with **arbitrary, user-chosen codes**, each shipping
a `module.yaml` (`code:`) and a `module-help.csv`. Because `Detect` treats *any* non-`core`
`_bmad/*/module-help.csv` as a candidate, a BMB-generated custom module is already a live input today — and hits
§1 exactly as CIS and TEA do. The first-party set is also larger and still growing (`bmad-loop`,
`bmad-automator`, `bmad-manticore`, `bmad-method-ui`, `bmad-method-wds-expansion`, a plugins marketplace).

**No closed enumeration of module codes can be correct.** Adding three enum cases would fix three repos and
leave the shape of the bug intact.

### 5. What is already generic (and should not be rebuilt)

The spike found the generic/hardcoded seam sits *inside* `BuildContext`, not between it and the doc tables:

- **Fully generic:** install discovery via `_bmad/_config/manifest.yaml` with an `_bmad/*/module-help.csv`
  disk fallback (`core` correctly excluded); CSV → `byStep` + `ModuleLabel` (the label comes through
  **correctly** for CIS/TEA/BMB).
- **Generic in body, but not reachable:** `IsModulePresent(repoRoot, code)` already takes an arbitrary code —
  but it is **`private`**. Only the two hardcoded wrappers `IsMethodPresent`/`IsGdsPresent` are public.
  Decision 3's `IsPresent(code)` is therefore **new public surface**, not the removal of a wrapper. The cost is
  small but it is not zero, and an earlier revision of this ADR overstated it.
- **Hardcoded:** the §1 identity line, and the `DocsFor` / `GlossaryFor` switches.

Two on-disk facts constrain any fix: `module.yaml` (which carries a clean `code:`/`name:`) is an installer
**source** file and is **not installed**; and `_bmad/{code}/config.yaml` carries no module identity — indeed
SpecScribe reads no module `config.yaml` at all today. **The `module` column of
`_bmad/{code}/module-help.csv`, plus the containing directory name, are the only on-disk identity signals.**

## Decision

### 1. Module identity derives from the module *code* — the `_bmad/{code}/` directory name — never from a skill prefix

The directory name is the module code (`bmm`, `gds`, `cis`, `tea`, `bmb`, and any BMB-minted code) and is
already the key `ChoosePrimary` and `IsModulePresent` use. The `prefix.StartsWith("gds")` inference in
`BuildContext` is retired.

Because "the directory name **is** the module code" is an open-world rule, it needs four guards it did not have:

**1a. Reserved names.** A `_bmad/` child is a module candidate only if it carries a `module-help.csv` **and**
its name is not reserved. Reserved: `core` (already excluded), plus `custom`, `scripts`, and **any name
beginning with `_`** (this repo's own `_bmad/` contains `_config/`, `custom/` and `scripts/`). Without this,
Decision 2 would *guarantee acceptance* of `_bmad/scripts/` as a module the instant anything dropped a
`module-help.csv` there. A reserved name carrying a `module-help.csv` is skipped silently — it is not an error.

**1b. Casing is normalized, once.** Module codes are compared **case-insensitively** and stored
**lower-invariant**. Today's code is inconsistent: manifest matching is `OrdinalIgnoreCase` but
`IsModulePresent`'s `File.Exists(Path.Combine(bmadRoot, moduleName, "module-help.csv"))` is case-sensitive on
Linux — so `_bmad/BMM/` yields `IsMethodPresent == false` while `Detect`'s disk fallback still finds it.
Presence checks must resolve the directory by **case-insensitive enumeration**, not by constructing a path.

**1c. A minted code may collide with a modeled one.** Nothing stops a BMB-generated module installing at
`_bmad/gds/` and silently inheriting GDS's docs and glossary. When a candidate's code is one SpecScribe models,
its CSV `module` label is cross-checked against that module's expected label; a mismatch demotes it to
`Unmodeled` (Decision 2) and emits an `Unsupported` diagnostic naming both labels. The label is already parsed;
this costs one comparison.

> **AMENDED 2026-07-27 — see [A1.1](#amendment-1--2026-07-27-from-the-story-182-code-review).** The match is
> now **tolerant** (whitespace-normalized, prefix/containment), not exact, and an **absent** label never
> demotes. Exact matching made a real install's survival depend on an upstream display string that §7 of this
> very document shows to drift.

**1d. The installed set is the union of manifest and disk.** Today the disk scan fires **only** when the
manifest yields zero candidates, so a manifest listing `bmm` alongside an installed `_bmad/tea/` never sees TEA
— while `IsModulePresent` (OR semantics) reports TEA present. Those two must not disagree. The set is the
**union** of manifest entries and on-disk `module-help.csv` directories, matching `IsModulePresent`'s existing
OR semantics. The disk scan stops being a fallback.

### 2. An unrecognized module code is a first-class, well-behaved outcome — never a fallback to `BmadMethod`

Such a module resolves to **`BmadModule.Unmodeled`** — a new case — **with** its real `ModuleLabel` from the
CSV, an empty doc set, an empty glossary, and its parsed `CommandCatalog` intact.

**2a. `Unknown` is not reused; `Unmodeled` is a new case.** This closes ratification question 3, and the answer
is **forced, not discretionary**: `DiagnosticsTemplater` already renders
`module.Module == BmadModule.Unknown ? "Unknown (not detected)" : module.Commands.ModuleLabel` as the
"Detected framework" row on the diagnostics page. That row is the **one surface that is already correct today**
for a CIS-only repo — it prints "Creative Intelligence Suite" from `ModuleLabel`. Routing unmodeled modules
through `Unknown` would flip it to "Unknown (not detected)", which is *strictly worse than today*. Shipped code
has therefore already bound `Unknown` to "detection failed". The three states are then distinct and must stay
so:

| State | Meaning | `ModuleLabel` | Diagnostics-page row |
|---|---|---|---|
| `ModuleContext.None` | No `_bmad/`, or nothing parsed — genuine non-detection | none | "Unknown (not detected)" |
| `Unknown` | Reserved for detection failure | none | "Unknown (not detected)" |
| `Unmodeled` | Recognized module, not one SpecScribe models | **real, from the CSV** | the real label |

**2b. `CommandCatalog.Empty` must stop carrying the label `"BMad"`.** It does today, and `ModuleContext.None`
**is** that instance — so `None` and `Unmodeled` are indistinguishable at exactly the surface Story 18-2
changes. Story 18-2's owner-elicited acknowledgement (2c) keyed on the module state alone would render
*"This project uses the BMad module"* on a repo with no `_bmad/` at all: a worse false claim than today's
silent omission. `CommandCatalog.Empty.ModuleLabel` becomes empty, and every label consumer must treat an empty
label as "no label", not as a name.

**2c. The unmodeled state renders a named acknowledgement, not a silent omission.** *(Owner design call,
2026-07-25 — folded in here from a `sprint-status.yaml` comment, where a user-visible-surface decision did not
belong.)* Where the glossary would be, `how-to-read.html` renders:

> This project uses the **{label}** module. SpecScribe doesn't publish a glossary for it yet.

This is gated on **`Unmodeled` *and* a non-empty label** — never on `None`, never on `Unknown`, and never on a
*modeled* module that legitimately publishes an empty glossary (a third state that collapses into the same
branch today, because `AppendGlossary` gates only on `glossary.Count == 0`, and Decision 5 makes glossaries
opt-in). The same gate applies to `AppendCommandLegend`, whose `commands.IsEmpty` test is what falsifies the
"command panels degrade correctly" claim in §2: the legend renders only for a **modeled** primary.

**2d. The `Informational` diagnostic, and the seam it needs.** One diagnostic per unmodeled module:

> `Detected BMad module '{code}' ({label}); SpecScribe has no module-specific docs or glossary for it, so those sections are omitted.`

`AdapterDiagnosticCategory.Informational` is the right category — it was written for exactly this "FYI, nothing
to do" case, and no sixth category is invented. But **the emission seam does not exist and must be built**;
this was unstated in the draft and is a genuine prerequisite, not a detail:

- **`Detect` takes no diagnostics sink.** It swallows everything in `catch { return None; }`, unlike sibling
  ingest paths (`IngestSprint(options, diagnostics)`). It gains an optional
  `List<AdapterDiagnostic>?` parameter, matching that existing convention.
- **Detection currently runs twice, on two different paths, and the wrong one wins.** `_module` is set from
  `bundle.Module` (the adapter path, which *has* diagnostics) and is then **overwritten** by an adapter-free
  `ModuleContext.Detect` inside `SiteGenerator.BuildNav` — and 4 of `BuildNav`'s 5 call sites *(as of
  2026-07-26)* pass no diagnostics list at all. So the detection that actually feeds nav, the glossary and
  `AbbreviationExpander` is not the one the bundle diagnosed. **Resolution: detect once per run.** The primary
  module is resolved from the full source list and cached; `BuildNav` consumes the cached `ModuleContext`
  rather than re-deriving it. This also removes the incremental/watch instability in 4e.
- **Cardinality:** at most one diagnostic **per unmodeled module per generate run**. On a watch rebuild it is
  re-emitted only if the installed module set changed — the diagnostics page must not accumulate a row per
  keystroke.
- **Anchor root.** `AdapterDiagnostic.RelativePath` is contractually **source**-root-relative, and
  `DiagnosticsTemplater` maps every adapter diagnostic to `DiagnosticAnchorRoot.Source`. The subject here —
  `_bmad/{code}/module-help.csv` — is **repo**-root-relative, so the webview Problems channel would resolve it
  to a nonexistent `{sourceRoot}/_bmad/{code}/module-help.csv`. Resolution: add
  **`DiagnosticAnchorRoot.Repo`**, combined with `ForgeOptions.RepoRoot` in `Commands.cs`'s anchor switch. One
  enum case and one switch arm; `_bmad/` is genuinely repo-anchored and should be openable.

### 3. `ModuleContext` carries the *set* of installed modules with a designated primary

Real BMad repos are increasingly multi-module (BMM + TEA + CIS). `ArtifactBundle.Module` stays a single
required, never-null `ModuleContext` — this closes ratification question 2 — but that context gains the full
installed set.

**3a. Set semantics are primary-only for `Commands`/`Docs`/`Glossary`.** The set exists so *presence* and
*identity* are honest; it does **not** merge catalogs. `Commands`, `Docs` and `Glossary` continue to come from
the primary alone. This is deliberate: BMad modules ship colliding skill ids — CIS ships Core's
`bmad-brainstorming`, and BMM and GDS both define `create-story` — so any future merge must first decide a
cross-module collision rule. `BuildContext`'s "first row wins for a given step" is only safe *within* one
module's CSV. **Merging, and the collision rule it requires, are out of scope for this ADR.**

**3b. `IsPresent(code)` must preserve OR semantics.** `IsMethodPresent`/`IsGdsPresent` — today's independent
dual-presence workaround, and the only reason the About-SDD support matrix is correct — generalize to
`IsPresent(code)` over the set, and the two wrappers remain as thin conveniences. But the wrapper returns true
on a **manifest entry alone**, whereas the candidate set requires a `module-help.csv` on disk. Defining
`IsPresent` over the *candidate* set would silently flip a manifest-only BMM install from Detected to not-
Detected. `IsPresent(code)` is therefore defined over the **union** set of 1d, not over the parsed-candidate
set.

**3c. `AboutSddTemplater.Frameworks.Id` is not the module code.** Its BMad row's `Id` is `"bmad"`; the module
code is `bmm`. That unreconciled second key feeds both `detected` switches, and `RenderFrameworkPage`'s
`Frameworks.First(f => f.Id == frameworkId)` **throws** on an unknown id rather than degrading. The roster
tuple gains an explicit `ModuleCode` field, the `detected` switches key on it, and the `First` becomes a
`FirstOrDefault` with an honest not-found path. The two-bool `detected` signature widens under this decision.

### 4. Primary selection must never demote BMM or GDS, and must be deterministic

Until Decision 3 lands in full, `ChoosePrimary` ranks `bmm`/`gds` above auxiliary modules instead of relying on
manifest order. This alone closes the §3 regression and is independently shippable. Four under-specified cases
are resolved with it:

**4a. Explicit rank, not "first wins".** Candidates are ordered by: (1) `gds` when the game-shape hint is
present, (2) `bmm`, (3) `gds`, (4) all other codes ordered **ordinal by code**. Never by discovery order.

**4b. Discovery order is never a tiebreak.** On the disk path candidate order comes from
`Directory.EnumerateDirectories` — i.e. filesystem order, which is platform-dependent. "Manifest order" was
only ever half the story. 4a's ordinal-by-code rule makes the outcome reproducible on every platform.

**4c. BMM-vs-GDS is defined.** The only existing tiebreak is the `looksLikeGame` source-path hint, which is
unconditionally false whenever `sourceRelativePaths` is empty — true on 4 of `BuildNav`'s 5 call sites. A
dual-install *game* repo therefore silently fell to BMM on every incremental and watch rebuild. Decision 2d's
detect-once-per-run rule fixes the mechanism; 4a fixes the ordering. When the hint is genuinely absent for a
BMM+GDS repo, **BMM wins**, deterministically.

**4d. A parse failure never promotes a lower-ranked module.** `Detect` currently advances to the next candidate
whenever `BuildContext` returns null, emitting no diagnostic — a path by which BMM is demoted without any tie
existing. A candidate that fails to parse emits `Malformed` and is skipped; the **rank** of the remaining
candidates is unchanged, so a lower-ranked module never inherits the primary slot merely because a higher-ranked
one was unreadable.

> **AMENDED 2026-07-27 — see [A1.2](#amendment-1--2026-07-27-from-the-story-182-code-review).** This guarantee
> now covers a **1c demotion** as well as a parse failure. As written it covered only the latter, and the
> demotion path did the opposite: a squatter at a modeled code took the primary slot over a genuine modeled
> module ranked below it. See also [A1.4](#amendment-1--2026-07-27-from-the-story-182-code-review) on the
> accepted `Malformed` → `errors=N` consequence of the category this decision mandates.

**4e. Multiple installed modules, one chosen.** When >1 non-primary module is installed, one `Skipped`
diagnostic records the others (see §7 of Story 18.1 for the drafted wording).

> **AMENDED 2026-07-27 — see [A1.3](#amendment-1--2026-07-27-from-the-story-182-code-review).** Both the
> threshold and the category changed: the notice fires at **≥1** non-primary module and is emitted as
> **`Informational`**. ">1" would have silenced the ordinary BMM+TEA install — Story 18.5's own primary
> scenario — and `Skipped` renders at Warning severity, so a correctly configured repo showed a warning.

### 5. Adding a module's docs/glossary — and its coverage vocabulary — stays an explicit, per-module act

Decision 2 makes unknown modules *safe*, not *covered*. A module gains a `ModuleDoc[]`, a `GlossaryTerm[]`, an
`AboutSddTemplater.Frameworks` row, a `SiteNav` output path, and a `README.md` support-table row only when a
story deliberately covers it.

**5a. `ArtifactCoverage` is in scope for this decision and is not closed by Decisions 1–4.** Its `Specs` family
set is hardcoded BMM vocabulary built from `sourceRelatives` alone, with **no reference to
`ModuleContext.Module`**. Identity work does not reach it. The dashboard artifact-coverage panel must become
module-aware: it renders the primary module's declared family set, and for an `Unmodeled` primary it renders
**nothing** rather than eight missing BMM families. This is a distinct slice from the identity fix and is
sequenced after it; the class comment already anticipates the seam ("a future framework adapter swaps this
family set, not the panel or the builder").

### 6. Epic 18 extends the existing adapter and does *not* require the adapter registry

`BmadArtifactAdapter.AppliesTo` markers `_bmad/` wholesale, so every BMad module — including BMB-generated ones
— already self-selects into it; a second `IArtifactAdapter` would carry an identical `AppliesTo`, making
registry selection ambiguous rather than useful. Epic 18 is therefore the one framework epic that can proceed
while the `SiteGenerator` single-adapter-field registry gap stays open.

### 7. Test fixtures for module detection are pinned to real module CSV content

The repo's current fixtures use synthetic `gds-*` rows (`ModuleContextTests`), which is precisely why §1 went
unnoticed: BMad's docs advertise GDS commands as `/bmgd-*`, and had that been the on-disk reality the suite
would still have passed. (It is not — GDS's real CSV uses `gds-*` and its `module.yaml` says `code: gds`;
**BMGD is branding**. Current GDS support is correct.)

For this decision to be actionable, the fixture content must be re-fetchable. See
[Upstream evidence](#upstream-evidence-provenance) for the repositories, paths and retrieval date; Story 18-2
re-fetches from those and records the commit SHA it pins against alongside the fixtures.

## Non-goals

- **Reopening the cross-framework adapter registry.** That decision belongs to Epics 11–15; per Decision 6
  Epic 18 does not need it. This ADR must not become a sixth competing registry proposal.
- **Merging `Commands`/`Docs`/`Glossary` across the installed set**, and the cross-module skill-id collision
  rule that would require (Decision 3a).
- **Covering CIS or BMB artifacts.** Story 18.1 recommends TEA as the priority coverage module; CIS's output
  already renders via the generic markdown pass, and BMB is a meta-tool whose outputs are other modules'
  scaffolding.
- **Reading module `config.yaml` for output paths.** TEA writes to a `test_artifacts` key SpecScribe does not
  read; that is a real Story 18.5 prerequisite but a separate, non-architectural decision.
- **Generalizing the next-step command vocabulary.** Assessed and rejected as unnecessary — see Consequences.

## Consequences

**Positive**
- Removes a live correctness defect: SpecScribe stops asserting BMM's vocabulary over projects that do not use it.
- Closes an install-order-dependent regression that would otherwise strike the first owner to add TEA or CIS to a BMM repo.
- Makes the module ecosystem's open-endedness a supported property rather than an unbounded source of future bugs — BMB-minted custom modules included.
- Identity moves onto the key two of three existing call sites already use, so the codebase becomes more internally consistent, not less.
- Multi-module repos become representable, retiring the ad-hoc `IsMethodPresent`/`IsGdsPresent` workaround.
- Detection becomes deterministic and platform-independent (4a/4b), and stops being recomputed inconsistently between the adapter path and `BuildNav` (2d).

**Negative / trade-offs**
- Touches a cross-cutting contract (`ModuleContext`, and what `ArtifactBundle.Module` means), so it is not a local fix.
- Decision 3 is the larger piece and lands after Decisions 1, 2 and 4, which are small and independently valuable (see Ratification).
- Every consumer of `ModuleContext.Module` must tolerate `Unmodeled` — a real label with a populated command catalog and no docs/glossary — a state that cannot occur today.
- Decision 2d requires **new plumbing**, not just a call: a diagnostics parameter on `Detect`, a single-detection-per-run rule, and a new `DiagnosticAnchorRoot.Repo` case.
- Decision 3b adds **new public surface** (`IsPresent`); `IsModulePresent` is private today, so this is not a wrapper removal.
- `AboutSddTemplater`'s two-bool `detected` signature, its `Frameworks` roster (which needs a `ModuleCode` field), and its throwing `First` lookup all need work.
- Decision 5a re-opens `ArtifactCoverage`, which the draft of this ADR did not name at all.
- Detection tests must be re-pinned to real module CSVs (Decision 7), which is unglamorous work.

**Explicitly unchanged**
- The `AdapterDiagnostic` five-value **category** vocabulary. Decision 2 uses `Informational` as designed; no
  sixth category. (`DiagnosticAnchorRoot` is a different enum and does gain a case — see 2d.)
- The next-step command **mechanism**, which is already module-neutral: `BuildContext` strips the prefix and
  keys on the step remainder, so `/bmad-create-story` and `/gds-create-story` both resolve `create-story` with
  no module-specific code. The residual BMM∩GDS *step vocabulary* hardcoded at ~43 call sites needs no
  generalization, because the surfaces that carry the **suggestions** (sprint board, epics, story pages) only
  exist when epics and stories exist — which only BMM and GDS produce. Note this does **not** extend to the
  always-rendered command **legend** on `how-to-read.html`, which Decision 2c gates explicitly.
  **The `epics.md` Additional-Requirements note that the mapping is "strongly GDS-oriented and requires
  generalization" is stale and should be retired**; it predates the CSV-driven `CommandCatalog`.

## Options considered

| Option | Verdict |
|---|---|
| **Leave as-is** | Rejected. §2 is a live NFR8 violation and §3 a live regression, both on module combinations that ship today. |
| **Add `Cis`/`Tea`/`Bmb` enum cases, keep prefix inference** | Rejected. Prefix inference cannot distinguish them — all three yield `bmad` — so the cases would be unreachable. It also cannot survive BMB-minted codes. Fixes three repos, leaves the bug's shape intact. |
| **Key identity on the module code, keep a closed enum** | Rejected as insufficient alone. Correct for known modules, but still misidentifies every unknown code unless paired with Decision 2's first-class `Unmodeled`. |
| **Reuse `Unknown` for the unmodeled state** | Rejected — see 2a. `DiagnosticsTemplater` has already bound `Unknown` to "detection failed"; reuse would regress the one surface that is correct today. |
| **Key on module code + first-class unmodeled + multi-valued set** | **Chosen.** Correct for known modules, safe and honest for unknown ones, and representative of real multi-module repos. |
| **New `IArtifactAdapter` per BMad module, via the registry** | Rejected. All BMad modules share `AppliesTo` (`_bmad/`), so the registry cannot discriminate between them; it would import Epics 11–15's unbuilt dependency for no benefit. |

## Ratification

Ratified **2026-07-26** by Matthew-Hope Eland. All three open questions are closed; none remain open.

1. **Scope split — resolved: yes.** Decisions **1, 2 and 4** land in Story `18-2-bmad-module-identity-foundation`
   as the prerequisite slice; they are small and close the live defects. **Decision 3** (the multi-valued
   contract change) defers to its own story. **Decision 5a** (`ArtifactCoverage`) is sequenced after the
   identity slice. Story 18-2 remains `ready-for-dev`.
2. **Does `ArtifactBundle.Module` stay singular — resolved: yes**, as proposed. The set lives inside
   `ModuleContext`. A `Modules` collection on the bundle is a wider blast radius for the same outcome.
3. **`Unknown` naming — resolved: rename.** A new `Unmodeled` case is introduced and `Unknown` is retained for
   genuine detection failure. This is **forced** by shipped code rather than chosen on taste — see 2a.

## Amendment 1 — 2026-07-27, from the Story 18.2 code review

**Status: Accepted.** Decided by Matthew-Hope Eland during the code review of Story
`18-2-bmad-module-identity-foundation`, the story that first implemented Decisions 1, 2 and 4. Three ratified
sub-decisions are amended and one previously-unrecorded tension is accepted. The implementation and this
document were changed **together**, so no window exists in which the code and the ratified text disagree.

**A1.1 — Decision 1c's label cross-check becomes TOLERANT, not exact.** As ratified, 1c demoted a modeled code
to `Unmodeled` whenever its CSV `module` label was not an exact match for the expected one. The review found
this made the shipped happy path depend on a third-party display string — and **this ADR's own §7 evidence table
documents that BMad's labels drift**: GDS's `module.yaml` says *"BMGD: BMad Game Dev Studio"* where its CSV says
*"Game Dev Studio"*, and TEA's say *"Test Architect"* vs *"Test Architecture Enterprise"*. A cosmetic upstream
rename such as `BMad Method v6` would therefore have stripped every real BMM install of its planning docs, its
whole glossary, site-wide abbreviation expansion and its command legend — signalled only by one warning row.
Amended: interior whitespace is normalized and a **prefix or containment** match passes; a genuine squatter such
as *"Totally Not GDS"* still demotes, which is the case 1c exists for. **An absent label never demotes** — an
empty `module` column is no evidence of squatting, and demoting on it would cost a genuine install everything
because of a missing CSV column. Symbols: `ModuleContext.LabelMatchesModeled`, `ModuleContext.BuildCandidate`.

**A1.2 — Decision 4d's guarantee extends from a parse failure to a 1c demotion.** As ratified, 4d said only that
a candidate which *fails to parse* is skipped without reordering the rest. The review found the demotion path
did the opposite: ranking is computed from **codes, before any label is parsed**, and `Detect` accepted the
first non-null `BuildContext` — and a demoted context is non-null. So a minted module squatting `_bmad/gds/`
beside a genuine `_bmad/bmm/`, in a repo with any game-shaped source path, took the primary slot as `Unmodeled`
and demoted **BMM, a modeled module, below an auxiliary one** — Defect B's exact symptom through a different
door, and a violation of Story 18.2's AC #2. Amended: a demotion is skipped exactly like a parse failure, with
its `Unsupported` notice still emitted, and the demoted context is retained only as a **last-resort fallback**
so a repo whose sole install is a squatter still gets its real label and catalog rather than `None`. Symbols:
`ModuleContext.Detect`'s descend-the-rank loop, `ModuleContext.CandidateContext`.

**A1.3 — Decision 4e fires at ≥1 non-primary module and emits `Informational`, not `Skipped`.** As ratified, 4e
read *"When **>1** non-primary module is installed, one `Skipped` diagnostic records the others."* Two problems.
The threshold as written would have silenced the notice for the ordinary BMM+TEA install — which is Story 18.5's
own primary scenario, and precisely the reader who needs to know why TEA's docs and commands are absent. And
`Skipped` renders at **Warning** severity (`DiagnosticsTemplater.FromEvents` maps only `Informational` to
`DiagnosticSeverity.Info`), so a correctly configured repo showed a warning for being correctly configured.
Amended: the notice fires when **one or more** non-primary modules are installed, and its category is
`Informational`. Two further corrections landed with it — the reported set is now candidates ranked **below**
the winner only (it previously swept in higher-ranked candidates that had just failed to parse, contradicting
their own `Malformed` notice), and its provenance clause no longer claims planning docs and a glossary "come
from" a primary that is itself `Unmodeled`. Symbol: `ModuleContext.ReportSecondaryModules`.

**A1.4 — Accepted tension: Decision 4d's `Malformed` category vs the Error mapping.** Recorded, not changed. 4d
mandates `Malformed` for an unparseable candidate, and `SiteGenerator.MapDiagnostics` maps `Malformed` to
`GenerationOutcome.Error`. A **non-primary** candidate's broken catalog therefore produces `errors=1` on
`GenerationSummary`'s machine summary line — the record CI greps — and makes every watch rebuild report as
failed, on a run whose site generated completely correctly. The owner accepted this: a broken module catalog is
worth failing the run's status line. Documented here so a future reader does not "fix" it as a bug.

**A1.5 — Decision 1d's "those two must not disagree" is an overstatement.** Recorded, not changed. The union
closes one direction only: the candidate set is `(manifest ∪ disk) ∩ has-csv` while `IsModulePresent` is
`manifest OR disk-csv`, so a manifest entry whose `module-help.csv` is absent still reports present while
contributing no candidate. Consequently **AC #2's "About-SDD Detected and the selected primary never contradict
each other" is only partially satisfied** — a 1c-demoted squatter and a partial install both still report
"Detected". The presence checks stay independent because Story 18.5's artifact gating depends on that contract;
`ModuleContext.DiscoverCandidates`' doc-comment was narrowed to match.

**A1.6 — Decision 7's evidence table: BMad Method's path is `src/bmm-skills/module-help.csv`.** The table above
says `src/module-help.csv` for `bmad-code-org/BMAD-METHOD`; that path does not exist. BMM's catalog was fetched
2026-07-27 from `src/bmm-skills/module-help.csv` and pinned at `bb45db4aa4496c69239f9c0629c290fd1b072fc9`. This
mattered in practice: BMM was the one module Story 18.2 left on a synthetic fixture, leaving its AC #3 —
"verified against **real** module `module-help.csv` content" — half met for the module the AC most needs held
still.

## References

Symbol-anchored; see the Citation policy at the top of this document.

- **The spike that surfaced this:** Story 18.1 — `_bmad-output/implementation-artifacts/18-1-bmad-module-landscape-and-coverage-spike.md` (Completion Notes §3, §4, §8).
- **The identity line:** `ModuleContext.BuildContext`, the `prefix.StartsWith("gds", …) ? GameDevStudio : BmadMethod` assignment. The enum: `ModuleContext.BmadModule`.
- **The single-winner selector:** `ModuleContext.ChoosePrimary`. Presence checks: `ModuleContext.IsMethodPresent` / `IsGdsPresent`. The private generic core: `ModuleContext.IsModulePresent`. Install discovery: `ModuleContext.Detect` / `ReadInstalledModules`.
- **The hardcoded tables:** `ModuleContext.DocsFor` / `GlossaryFor` and their `BmadMethodDocs` / `GameDevStudioDocs` / `*Glossary` arrays.
- **The sentinel that collides:** `ModuleContext.CommandCatalog.Empty` (label `"BMad"`) and `ModuleContext.None`.
- **The unconditional surfaces:** `HowToReadTemplater.AppendGlossary` and `HowToReadTemplater.AppendCommandLegend`; `SiteGenerator`'s `AbbreviationExpander.Expand(html, _module.Glossary)` call. The file-gated one: `SiteNav`'s `moduleDocs` loop.
- **The double detection:** `SiteGenerator.BuildNav`'s `ModuleContext.Detect` call and its five call sites.
- **The un-gated coverage panel:** `ArtifactCoverage.Specs`.
- **The `Unknown` consumer:** `DiagnosticsTemplater`'s `ModuleDisplay` assignment ("Detected framework" row).
- **The adapter marker:** `BmadArtifactAdapter.AppliesTo`; the registry gap, `SiteGenerator`'s `_adapter` field.
- **The diagnostic category and anchoring:** `AdapterDiagnosticCategory.Informational`; `AdapterDiagnostic.RelativePath` (source-root-relative); `DiagnosticAnchorRoot`; `Commands.cs`'s anchor-root switch.
- **The support roster:** `AboutSddTemplater.Frameworks` and its two `detected` switches; `RenderFrameworkPage`'s `Frameworks.First`; `README.md`'s support table.
- **The stale note:** `_bmad-output/planning-artifacts/epics.md`, Additional Requirements — search the phrase *"strongly GDS-oriented"*.
- **The requirement:** **NFR8** in `_bmad-output/planning-artifacts/epics.md` (search `NFR8:`) — quoted in §2.

### Upstream evidence provenance

Decision 7 requires fixtures pinned to real module CSVs, so the sources must be re-fetchable. All were fetched
during Story 18.1 on **2026-07-25** from the `bmad-code-org` GitHub organization. Story 18.1's probe was a
throwaway written to a session scratchpad and its fixtures lived in OS temp — **neither survives**, and no
fetched bytes were retained in the repo. Story 18-2 therefore re-fetches from these paths, records the commit
SHA it pins against, and commits the fixture content:

| Module | Repository | Files relied on |
|---|---|---|
| BMad Method | `bmad-code-org/BMAD-METHOD` | `src/module-help.csv`, `src/module.yaml` |
| Game Dev Studio | `bmad-code-org/bmad-module-game-dev-studio` | `src/module-help.csv` (`gds-*` skill ids), `src/module.yaml` (`code: gds`, `name: "BMGD: BMad Game Dev Studio"`) |
| Creative Intelligence Suite | `bmad-code-org/bmad-module-creative-intelligence-suite` | `src/module-help.csv` (five skills incl. Core's `bmad-brainstorming`; `output-location: output_folder`) |
| Test Architect (Enterprise) | `bmad-code-org/bmad-method-test-architecture-enterprise` | `src/module-help.csv` (`bmad-testarch-*`; `output-location: test_artifacts`) |
| BMad Builder | `bmad-code-org/bmad-builder` | `src/module-help.csv`, `skills/**/assets/`, `samples/` |
| (closest downstream candidate — **no** `_bmad/` install found) | `bmad-code-org/bmad-method-sample-data` | inspected and rejected as an artifact-shape source |

**Every external claim in this ADR rests on the table above and is unverified against a downstream project that
actually installed and used CIS/TEA/BMB.** No such project was found. Treat the artifact *filenames* for TEA and
CIS as the least-settled part of this decision; the *identity* findings (§1, §3) are independent of them and
were probe-verified against real CSV bytes.
