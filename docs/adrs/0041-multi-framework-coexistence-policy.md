# ADR 0041: Multi-Framework Coexistence — Per-Family Policy, Source Discovery, and the Liveness Precondition

- **Status:** Proposed
- **Date:** 2026-08-07
- **Deciders:** Owner (Matt Eland)
- **Context story:** [Story 4.9](../../_bmad-output/implementation-artifacts/4-9-multi-framework-coexistence-strategy-spike.md)
  — a decision spike. Evidence in
  [4-9-spike-report.md](../../_bmad-output/implementation-artifacts/4-9-spike-report.md).
- **Amends:** [ADR 0038](0038-framework-adapter-selection-and-neutral-source-root-discovery.md) — the registry
  decision. This record settles what ADR 0038 §5 deliberately left open. **It is not a second registry ADR**:
  0038 remains the one selection decision inherited by Epics 11 / 12.3 / 13 / 14 / 15, and this amends that
  record rather than competing with it.
- **Relates to:** [ADR 0017](0017-projection-routes-mirror-ir-paths.md) (routes are IR paths; hrefs are never
  rewritten); [ADR 0027](0027-watch-rebuild-scope-is-one-classifier-and-topology-escalates.md) ("safe" = proven
  byte-identical); [ADR 0033](0033-content-drift-gates-are-targeted-and-regenerable.md) (gate discipline);
  [ADR 0014](0014-specscribe-settings-folder-format.md) / [ADR 0003](0003-directory-scoped-settings-and-read-only-helpers.md)
  (settings shape); [ADR 0037](0037-extension-authors-settings-through-the-core.md) (the diagnostic-anchor
  contract shared with the extension); [ADR 0015](0015-bmad-module-identity-open-world-and-multi-valued.md)
  (module identity is open-world and multi-valued *within* BMad — it is **ADR 0038 §5**, not 0015, that records
  the separate fact that `ModuleContext` is BMad-**typed**, so only a BMad adapter can ever supply it).

## Context

ADR 0038 shipped a working registry: every adapter whose `AppliesTo` returns true runs, single-valued families
take the first non-null contribution in roster order, collections union, and every drop is reported. Its §5
deferred the strategic question — *is that the right policy, and should the single `SourceRoot` stay single?* —
to this record.

Story 4.9 answered it by **measuring the shipped code**, not by reading its intent. A probe harness outside the
repository drove `AdapterRegistry.Select` / `Ingest` / `IngestEpics` over the real reference repository
(`C:/dev/CORA`, HEAD `f312528`) and over four constructed multi-framework shapes. Three measurements reframe
the question:

**1. The merge rules have never fired, and structurally almost cannot.** Across eight scenarios — including two
real multi-adapter runs on CORA and two constructed repositories where *both* frameworks hold a complete
artifact set — **not one `Skipped` diagnostic was emitted**. The reason is not that the rules are wrong; it is
that they are unreachable. Every adapter is handed the same `sourceFiles` list, enumerated from the single
`SourceRoot`, and every family except one derives from that list or from `options.SourceRoot` directly. A
non-primary framework therefore has nothing to lose, so nothing is reported as lost. On CORA the BMad adapter
contributes **exactly one** field — `Module` — and only because `ModuleContext.Detect` happens to be anchored to
`RepoRoot` rather than `SourceRoot`.

> **What `Module` crossing does and does not prove.** Two independent facts let it cross, and only one of them
> is about roots. ADR 0038 §5 records the other: `ModuleContext` "is BMad-typed to a closed `BmadModule` enum
> keyed on `_bmad/{code}/`, so a non-BMad adapter can only return `ModuleContext.None`." `Module` could
> therefore never have come from GSD under *any* root layout or *any* merge policy. Its crossing is evidence of
> BMad-typing plus a `RepoRoot` anchor — **not**, on its own, evidence that the frameworks occupy distinct
> planning/delivery roles. The role reading below is argued from the *shape of the repositories* (CORA plans in
> one framework and delivers in another) and from the coherence objection in §2, not from this single measured
> crossing. Read the other way round, the measurement's honest minimum is narrower and worth stating plainly:
> **exactly one family crosses the root boundary, and it is the one family a non-primary adapter is
> definitionally incapable of contending for.** That is why §1 declines to add a role vocabulary to the adapter
> contract: the evidence supports *describing* what root ownership already decides, not *encoding* roles.

**2. The resolution axis in shipped code is neither adapter order nor artifact role. It is the source root.**
Roster order (`B3`) decided nothing in any measured scenario. Whichever framework owns the resolved root supplies
every family it can see; the other supplies nothing. That is a real policy — it is simply not the policy the
merge table describes.

**3. Marker detection tests for *presence*, never for *life*.** `ForgeOptions.FindSourceMarker` is
`Directory.Exists`; `GsdCoreArtifactAdapter.AppliesTo` and `BmadArtifactAdapter.AppliesTo` are each a single
`Directory.Exists`. A framework installed and abandoned therefore wins **twice** — it takes the source root *and*
it claims the epics family — and the live framework's artifacts vanish with no diagnostic saying so. Measured on
a constructed repository: a `.planning/` left behind from 2020 supplied the portal's only epic while the live
BMad `epics.md` was absent, under a site correctly branded with the live project's name.

Two further measurements bound the options rather than the policy, and are recorded in full in the spike report:
following the registry's own printed advice (`--source _bmad-output`) on CORA produces **two diagnostics whose
paths escape the source root** (`../.planning/STATE.md`, `../.planning/config.json`, both anchored `Source`); and
`--source` pointed at a repository root re-derives `RepoRoot` as that directory's *parent*, so on CORA it walks
**9,510** markdown files, brands the site `dev`, matches **no** adapter, and emits **zero** diagnostics.

## Decision

### 1. The resolution axis is ROLE, and the primary root is how role is declared today

A multi-framework repository is usually **one project at two stages**, not two projects competing. CORA plans in
BMad and delivers in GSD Core; the frameworks occupy different roles, and a policy built on "first non-null wins"
models a rivalry that is not happening.

SpecScribe adopts **role** as the resolution axis. It does **not** adopt a declared role vocabulary
(`planning-owner`, `delivery-owner`) on the adapter contract: that is unjustifiable on a sample of one repository
and would be a contract change inherited by five unbuilt adapters. Instead, role is expressed by the mechanism
already in place — **the framework that owns the resolved source root owns delivery**, and any other matching
framework contributes only what it can supply from outside that root.

This is what the code already does. The decision is to **say so**, so that a rule which appears to arbitrate
(roster order) stops implying a choice it never makes.

### 2. Per-family policy

Answered against the **three** real resolution units in shipped code, not the five fields Story 4.9 AC #1 names.
`Epics`, `Requirements` and `EpicsSourceFullPath` are one unit (ADR 0038 §2); see §5 below.

| Family | Policy | Reason |
|---|---|---|
| **Epics family** (`Epics` + `Requirements` + `EpicsSourceFullPath`) | **Precedence** — owner is the framework owning the resolved source root | Kept as a unit: requirements roll up from the epics source, so a split pair is incoherent. The tiebreak changes from *roster order* to *root ownership*, which is what the code does de facto. |
| **`Sprint`** | **Precedence, bound to the epics owner** | A sprint ledger enumerating framework A's phases beside framework B's epics index is incoherent in exactly the way ADR 0038 §2 identified for requirements. Today they coincide by accident; this makes it structural. |
| **`Module`** | **Precedence** (first real identity wins, ties to the first) — **retained across the root boundary, and attributed** | `Module` is single-valued and cannot merge; the shipped mechanism is `HasModuleIdentity`'s first-non-`Unknown` wins, and that stands (B7). What this record decides is not the tiebreak but that the contribution is **kept** when it comes from outside the resolved root, rather than being refused for consistency with the other families. It is *correct* to keep: module identity is a planning-side fact, and on CORA BMad really does own planning. Note the contest is nominal — per ADR 0038 §5 only a BMad adapter can ever supply a real identity — so "precedence" here almost always has one candidate. It must be **labelled** (§3), never presented as the delivery framework's identity. |
| **`Retros`, `Diagnostics`** | **Merge** (concatenate in adapter order) | Additive; no contention possible. |
| **`StoryArtifactsById`** | **Merge** (union; duplicate id keeps the earlier adapter's artifact and emits `Skipped`) | Correct as shipped. Two frameworks numbering independently can collide, and reporting beats overwriting. |
| **`ConsumedSourceRelatives`** | **Merge** (union) | Additive. |

**No family is resolved by explicit refusal.** Refusal was priced (it is the cheapest option and the reader
surface already exists) and rejected: the measured behaviour is already precedence-with-diagnosis, refusal would
*remove* the module identity that CORA legitimately gets from BMad, and it would buy nothing — there is no family
today where two frameworks genuinely contend.

### 3. What the reader is told, and where

The attribution notice `DescribeMatchSet` already emits is the right content — it names who matched and who
supplied each family. It is on the **wrong surface for its importance**: the Story 4.8 diagnostics page is
reachable only via footer → About → Diagnostics, deliberately (no nav entry, no dashboard callout).

**Decision:** the per-family attribution is promoted to the **About-SDD framework page**, which already exists to
state what SpecScribe can and cannot do with each detected framework and already carries ADR 0038's framework
ceilings. The diagnostics page keeps its rows — they are the evidence. **The dashboard is explicitly not used:** a
multi-framework repository is rare, the dashboard is the highest-traffic surface, and a permanent banner there
would cost every single-framework project to serve a minority case.

**No new reader surface is created.** This is a row on a page that exists.

### 4. Source discovery stays single-rooted — and the real defect is not the root count

**Decision: Option A (status quo, one root) stands.** Multi-rooted source discovery is **not** adopted now.

Four options were priced with counted call sites (spike report § 5):

- **A — Status quo.** Zero implementation cost. Measured cost on CORA: six documents in
  `_bmad-output/planning-artifacts/` (5 `.md` + 1 `.html`) do not render as pages.
- **B — Auxiliary document roots**, read-only, rendered under a per-root output prefix exactly as
  `AdrSourceRoot` → `adrs/` already does. **This is the option to take when the trigger fires.** It is a
  generalization of a two-root system that has been in production since Epic 1, not a leap: separate discovery
  walk, output prefixing, per-root diagnostic anchoring, per-root watchers and its own settings field all already
  exist for the ADR root. Crucially, new roots get *new* prefixes, so **existing URLs do not move** — which is
  what distinguishes it from C under ADR 0017.
- **C — Raise the root to `RepoRoot`, markers become filters. REJECTED, and rejected on measurement.** It is not
  merely expensive, it is not currently expressible: `ForgeOptions.Resolve` derives `RepoRoot` as the *parent* of
  an explicit `--source`, so pointing at a repository root silently relocates every marker probe one level up.
  Measured on CORA: 9,510 files walked, site branded `dev`, no adapter matched, zero diagnostics. Even with that
  coupling fixed, every existing page's source-relative path — and therefore its URL — moves, which ADR 0017
  §Consequences classifies as a **public** change with no href rewriter to compensate.
- **D — A root-qualified path type** (`SourceRef(root, relative)`). The cleanest model and the largest refactor:
  46 `.SourceRoot` and 29 `.AdrSourceRoot` references across `src/`, four `ToSourceRelative` definitions and 41
  call sites, plus the IR schema, settings, watch and diagnostic anchoring. Deferred, not rejected — it is what B
  becomes if a third or fourth root ever appears.

**The trigger that makes B correct:** a second real repository that carries a full artifact set in **both**
frameworks *and* a reader who needs the non-primary framework's loose documents as pages. CORA is not that
repository — its `_bmad-output` holds planning prose, no epics source, and no sprint file.

**The far more valuable finding is that the root count is not what is broken.** Three defects measured in this
spike are independent of multi-rooting and cost a fraction of any of B/C/D:

#### 4a. Marker probes become LIVENESS probes — and the predicate is PER-MARKER

`FindSourceMarker` and both `AppliesTo` implementations must stop resolving on a bare `Directory.Exists`. That
much is settled by shape (c) (spike report § 4.3), where a `.planning/` abandoned in 2020 takes both the source
root and the epics family while a live BMad `epics.md` vanishes undiagnosed.

**What the predicate is, is not one rule.** A single shared content probe was the obvious answer and it is the
wrong one, in two separate ways — both measured against this spike's own fixtures during the Story 4.9 code
review:

- **A content probe does not detect abandonment.** `HasMarkdownWithinOneLevel` asks for "at least one markdown
  file within one directory level". Shape (c)'s husk holds `ROADMAP.md`, `PROJECT.md` and `STATE.md` at its top
  level — the stale `ROADMAP.md` is precisely the artifact that wins the epics family. The husk **passes** a
  content probe, so shape (c) would resolve exactly as before. A vestigial directory is by definition one that
  still holds its artifacts; content presence is a different flavour of presence, not life.
- **`_bmad/` is not an artifact directory.** `BmadArtifactAdapter.AppliesTo` probes `_bmad/`, which holds
  tooling and configuration — `config.toml`, `bmm/`, `core/`, `scripts/` — and **no markdown at all** on
  SpecScribe, on CORA, and in this spike's own fixtures. A markdown probe there returns false for every BMad
  repository in existence. On CORA that costs the `Module` identity §2 just decided to keep: BMad stops
  matching, one adapter remains, `Ingest`'s `bundles.Count == 1` early return (B4) hands back GSD's bundle
  verbatim, and `Module` is `Unknown`. On a BMad-only repository detection silently reroutes through B2's
  no-match fallback and becomes indistinguishable from a framework-less repo in `DescribeMatchSet`.

**Decision — three predicates, keyed to what each marker actually is:**

| Marker | Probed by | Predicate |
|---|---|---|
| `.planning/` (and the other framework artifact markers in `SourceDirNames`) | `FindSourceMarker`, `GsdCoreArtifactAdapter.AppliesTo` | bounded content probe **plus a recency signal** — content alone cannot separate live from abandoned |
| `_bmad-output/` | `FindSourceMarker` | same: bounded content probe **plus a recency signal** |
| `_bmad/` | `BmadArtifactAdapter.AppliesTo` | **`config.toml` present** — a config marker, tested as a config marker. Explicitly **not** a markdown probe |

`ForgeOptions.AdrFallbackProbeSubdirs` / `HasMarkdownWithinOneLevel` remains the precedent for the *bounded*
half — root plus one level, never a whole-tree walk — but it is the floor of the artifact-marker predicate, not
the whole of it, and it is not the predicate for `_bmad/` at all.

**The recency signal is deliberately left unspecified here** and belongs to FT-1, which must choose it and pin
it: newest artifact mtime against a threshold, a git-log probe, an explicit opt-out marker, or a refusal to
guess coupled with 4b-style diagnosis. What this record fixes is that the question exists and that a content
probe does not answer it.

This is still the highest value-per-line item the spike found, and it is now bounded correctly rather than
optimistically.

#### 4b. Diagnostics must not emit paths that escape their anchor

`GsdCoreArtifactAdapter.ResolvePlanningRoot` implements a correct containment check and refuses cleanly when
`.planning/` is outside the source root. But `Ingest` then calls `IngestSprint` and `ReportUnsupportedArtifacts`
unconditionally, and **both re-derive the planning root from `RepoRoot` without that check**, producing
`Source`-anchored relative paths of the form `../.planning/…`. `PathUtil.EscapesRepoRoot` exists precisely to
reject these, and `Commands.cs` joins `Source`-anchored paths with `SourceRoot` to place a marker in the VS Code
Problems panel — so an escaping path resolves outside the repository. This is a contract shared with the
extension (ADR 0037), not an internal detail.

#### 4c. The non-primary marker notice makes a claim that is false

`AppendNonPrimaryMarkerNotice` tells the reader: *"Artifact families from those frameworks are still merged into
the portal, but their loose documents are outside the single source root and do not render as their own pages."*
Measured on CORA in **both** directions, the first clause is false. With `SourceRoot = .planning`, BMad
contributes only `Module`; with `SourceRoot = _bmad-output`, GSD contributes **nothing at all**. The notice
promises family merging that does not happen, which is worse than the gap it was written to explain — NFR8 asks
that a displaced framework read as a *stated boundary*, and a boundary stated inaccurately is not one.

Its second sentence — "Re-run with `--source` pointing at one of them" — is the advice that walks the user
directly into 4b.

### 5. AC #1's five-field list is superseded

Story 4.9 AC #1 names `Epics`, `Sprint`, `Requirements`, `Module` and `EpicsSourceFullPath` as five independently
resolved single-valued fields. Shipped code resolves **three** units: the epics family is claimed together by the
first adapter that *found* an epics source (ADR 0038 §2). The AC's list is superseded by that refinement; this
ADR answers the three real units and records the correction rather than answering a question the code no longer
asks.

### 6. No new gate

Per ADR 0033, none is proposed. Nothing here changes rendering, and the behaviours this record governs are
unit-testable over the registry and the adapters. Any follow-through story must note that **neither existing gate
can see this work**: `check:parity` renders from a frozen corpus IR and cannot observe a C#-side change, and
`check:ir-content` derives from *this* repository's IR — and this repository is a BMad project, so cross-framework
markup is pruned with the gate green (Story 12.2 §F1, measured). Cover follow-through with unit tests over the
region and with live-browser inspection, per CLAUDE.md § Verification.

## Consequences

**Good.**

- The stated policy and the shipped behaviour agree. A rule that looked like it arbitrated (roster order) is
  demoted to what it is, and the rule that actually decides (root ownership) is named.
- The abandoned-framework failure is localized to the marker predicate and needs no architectural change —
  though it is **not** as cheap as one shared content probe, which §4a shows would not have worked.
- Multi-rooting is neither adopted prematurely nor mis-priced as unprecedented; the option that would be taken
  (B) is identified, along with the trigger and the reason it beats C on URL stability.
- Two live correctness defects and one false reader-facing claim are named with reproductions.

**Costs, accepted.**

- A two-framework repository still renders one framework's loose documents. On CORA that remains six files.
- `Module` crossing the root boundary is retained as a role contribution, which means the About-SDD page must
  carry attribution or the portal implies a framework ownership it does not have. That is work, not a freebie.
- **The liveness predicate is three rules, not one, and one of them is unspecified.** §4a fixes the shape of the
  answer; FT-1 must still choose and pin the recency signal. Until it does, shape (c) is diagnosed but not
  fixed.
- **The liveness probe is a behaviour change with a wider blast radius than "empty directory".** It changes
  resolution for a genuinely empty framework directory (intended), but a *bounded* content probe also demotes a
  live framework whose markdown sits **two or more levels deep**, and `HasMarkdownWithinOneLevel` additionally
  **excludes `README.md`**, so a directory whose only shallow markdown is a README fails it too. Each needs its
  own pinned test, and FT-1 must decide whether either is acceptable or whether the bound must widen.
- **A recency signal can be wrong in the safe direction and the unsafe one.** A freshly cloned repository has
  uniform mtimes; a long-dormant but still-current framework looks abandoned. Whatever FT-1 picks must fail
  toward *keeping* a framework and diagnosing the ambiguity, never toward silently dropping one — that is the
  failure mode shape (c) already demonstrates and NFR8 forbids.

**Supersedes** (in ADR 0038):

Exactly one behavior is **superseded**; the rest of this list records **refinements** and one **factual
correction**, kept here so the whole amendment reads in one place. The B1–B11 verdicts in spike report § 6 use
these same three words and are authoritative — where a behavior appears below, the label matches that table.

- **Superseded — B6.** §2's `Sprint` rule "first non-null wins" → sprint binds to the epics owner.
- **Refined — B5.** §2's implicit *roster-order* tiebreak for the epics family → root ownership. The family
  remains one unit, which is why B5 counts as a **stand** in report § 6's tally: the rule itself is untouched and
  needs no code change, because root ownership is already what the code does de facto. Only the *stated* basis
  for the tiebreak moves, so nothing is superseded and no follow-through item is owed. (Recorded here because
  the wording in ADR 0038 §2 implies roster order arbitrates, and it does not.)
- **Factual correction — not a decision.** §5's statement that in a two-framework repository "the non-primary
  framework's artifact families still merge into the bundle" is **measured false** in both directions on the
  reference repository; only `Module` crosses, and per §5's own next paragraph only a BMad adapter ever could.
- **Refined — B1.** §1's "every matching adapter runs" is refined, not superseded: matching must mean *live*,
  not *present* (§4a).

**Stands, explicitly:** ADR 0038 §1's ordered registry and BMad-last fallback, §3's marker order and the reason
`_bmad-output` probes last, and §4's lift of the scoped epics re-ingest onto `IArtifactAdapter`. The watch/full
agreement §4 buys was re-measured in this spike and held in **8 of 8** scenarios.

## Alternatives considered

- **A declared role vocabulary on `IArtifactAdapter`** (each adapter announces `planning` / `delivery`
  capabilities per family). The most principled model, and the one the asymmetry argues toward. Rejected **now**:
  it is a contract change inherited by five unbuilt adapters, justified by one repository. Revisit when a second
  real multi-framework repository exists — B and this both remain reachable from the current shape.
- **Explicit refusal per family** — generate for the primary framework, refuse the second, say so loudly.
  Genuinely on the table: cheapest, most honest, and the reader surface already exists. Rejected because it
  removes the module identity CORA legitimately gains from BMad, and because no family actually contends today,
  so refusal would be ceremony over an empty conflict.
- **Picking one winning adapter instead of merging** — rejected in ADR 0038 §1 and unchanged here. Measured, the
  merge is already almost an identity; picking a winner would discard `Module` for no gain.
- **A new content-drift gate over multi-framework output** — rejected per ADR 0033 and per the measured fact that
  the extraction corpus is a BMad repository and structurally cannot see the markup such a gate would guard.
