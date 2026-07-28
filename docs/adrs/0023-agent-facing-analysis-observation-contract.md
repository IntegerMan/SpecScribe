# ADR 0023: An Analysis Observation Is a SARIF Profile, Parallel to SpecScribe's Own Diagnostics

**Status:** **Accepted** 2026-07-28 (authored by Story 25.3; **ratified by the owner** in the Story 25.3 dev pass, as AC #4 requires — six downstream stories bind to this record, and a Proposed ADR is not a contract they can bind to)
**Date:** 2026-07-28
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0002 — Shared Rendering Core and Host-Neutral View Models](0002-shared-rendering-core-and-host-neutral-view-models.md) (AD-2: what travels is a normalized record, never raw provider bytes); [ADR 0014 — `.specscribe` Is a Settings Folder](0014-specscribe-settings-folder-format.md) (the per-repository state folder the digest lands in); [ADR 0016 — The Canonical IR Carries Rendered Prose HTML](0016-ir-carries-rendered-prose-html.md) (the IR channel this contract may later ride); [ADR 0020 — A Module May Declare Non-Markdown Sources](0020-module-declared-non-markdown-sources.md) (the enrichment-only posture this inherits); [ADR 0011 — Directed-Graph Edge Direction](0011-directed-graph-edge-direction-carrier-to-target.md) (the "state the direction rule explicitly" discipline attachment follows); Epic 25 (Story 25.3, consumed by 25.4) and Epic 26 (Stories 26.2–26.6)

**Numbering note.** `docs/adrs/` ends at **0022** on disk. **`0019` remains claimed-but-unwritten by BOTH Story 18.3** (*"LLM-Generated Artifacts Are Enrichment-Only Inputs"*) **and Story 22.3** (retired) — verified by grep at authoring time, not assumed. `0020`, `0021`, and `0022` all landed between Story 25.3's authoring (2026-07-27, which predicted `0021`) and this pass. **`0023` is the first uncontested slot.** If `0019` lands differently, renumber this one — the content, not the digit, is the decision.

**Evidence.** Every number below was measured by Story 25.3 on live data at analysis revision `d1722f17`, working tree `06b300c`, 2026-07-28. Full method and worked examples: [`25-3-spike-report.md`](../../_bmad-output/implementation-artifacts/25-3-spike-report.md).

## Context

Epic 25 gives SpecScribe's own agents access to static-analysis results; Epic 26 makes those results a product surface. Six stories across the two epics consume the same data. Without one record, 25.4 would emit a Sonar-shaped payload for an agent and 26.4 would build a second, differently-shaped one for a code page — and the first framework that is not SonarCloud would break both.

The problem is **not** a blank page. SpecScribe already ships a diagnostics model: `DiagnosticSeverity {Error, Warning, Info}`, `DiagnosticNotice`, `AdapterDiagnostic` with a five-value `AdapterDiagnosticCategory`, `DiagnosticAnchorRoot {None, Source, Adr, Repo}` (the "which root is this path relative to" problem, already solved and extended twice), and a JSON-lines stderr channel feeding VS Code's `DiagnosticCollection`. So the first question is whether analysis results are simply more of those.

Three facts, measured rather than assumed, shaped every decision here:

1. **Sonar carries two severity axes that disagree on the majority of issues.** Normalizing the same 1,466 live issues through the legacy `severity` field and through `impacts[]` puts **800 of them (54.6 %) on different levels**. This is not a rounding difference between neighbouring buckets; it is a different ordering of the backlog.
2. **`impacts` is an array, and it is populated today.** 14 issues carry two `{softwareQuality, severity}` pairs. Crucially, the `impactSeverities` **facet counts issues, not impact pairs**, so it sums to exactly the issue count and is structurally incapable of revealing the array. Only reading the payload shows it.
3. **The only available code→planning join amplifies roughly tenfold.** Measured against real generated output, 1,572 attached observations produce **15,758** story-level attachment edges — a mean fan-out of **10.02 stories per observation**, with `specscribe.css` reaching **64 stories** and `SiteGenerator.cs` reaching **18 of 19 epics**.

And SARIF 2.1.0 — an OASIS Standard incorporating Approved Errata 01, 28 August 2023 — already exists for exactly this interchange problem. Roslyn emits it natively, GitHub code scanning consumes it, SonarQube imports it. Designing a bespoke shape without confronting it would be reinventing a standardized wheel.

## Decision

### 1. The record is an **`AnalysisObservation`**, and it is deliberately not called a "finding"

`## Review Findings` is a **parsed** story section: `EpicsParser` carves it, `EpicsView` carries it as `ReviewFindingsHtml`, and `HtmlRenderAdapter.Epics` renders `<h3>Review Findings</h3>` on **every story page**. Story 26.5 places analysis results on those same pages. Two sections both called "Findings" — one authored by a human reviewer, one ingested from a machine — is the collision `ArtifactCoverage` already has with FR42's "coverage", entered knowingly.

`Insight` is taken (Git Insights, the Insights tab, `FileInsight`). `Coverage` is taken. `Diagnostic` is taken, and Decision 2 explains why the subject differs anyway.

**Reader disambiguation is structural, not typographic:** *"Review Findings"* (human, authored, part of the story record) versus *"Analysis Observations"* (machine, ingested, provider-attributed). "Observation" is also the honest word — it is a third party's claim about the code, not a verdict the project has accepted.

### 2. An observation is **parallel to** `DiagnosticNotice`, not merged into it

The split axis is **subject**. A `DiagnosticNotice` describes *SpecScribe's own run* ("I could not parse your `sprint-status.yaml`"). An `AnalysisObservation` describes *the user's code* ("this regex has no timeout"). They differ further in **lifetime** — a run notice dies with the run, an observation persists and can be stale — and in **provenance**: self versus third party. Merging them would put a generator failure and a code smell in one list.

**The argument for reuse is recorded because it is genuinely strong:** the severity scale, the anchor-root problem, the never-color-alone rendering, and an agent-facing serialization all already exist and are proven in production.

**What decided it was a measured fact the reuse case had not accounted for.** The existing agent-facing serialization is **two-level, not three**. `Commands.SerializeDiagnostics` emits `severity = notice.Severity == DiagnosticSeverity.Error ? "error" : "warning"`, so `DiagnosticSeverity.Info` — the level Story 4.8 added precisely so `Informational` could mean "nothing to do" — **collapses to `"warning"` on the wire**. Reusing that channel means either pushing a four-level scale through a two-level serializer, silently mislabelling every `note`, or widening a shipped contract the VS Code extension already consumes. Neither is the cheap reuse it appears to be.

**Three things are reused deliberately**, and are named so no downstream story rebuilds them: the **anchor-root normalization** (Decision 4 — observations need no enum, but inherit the answer), the **never-color-alone rule** (Decision 3 puts the text label in the payload), and the **non-fatal posture** (Decision 8).

### 3. The contract is a **named profile of SARIF 2.1.0**, and the normalized severity scale is SARIF's `level` verbatim

Not "is SARIF", and not a divergence:

- **Not plain SARIF**, because SARIF has no epic, story, or requirement — attachment would live in `properties` and therefore be a profile anyway, just an undocumented one. Two further measured reasons: raw SARIF costs **1,793 bytes per result** against **678** for the same information as an observation (**2.6×**), and a SARIF `result` is **not self-describing** — it carries `ruleIndex`, an integer into an out-of-line `tool.driver.rules[]` catalogue, so a single result handed to an agent has no rule name and no help URI.
- **Not a divergence**, because that forfeits free interoperability with Roslyn, GitHub, and SonarQube's own SARIF import for nothing.

**The normalized severity scale is `none` / `note` / `warning` / `error` — SARIF's `result.level` enum, unchanged.** This is the single most load-bearing choice in the record: it makes the raw-SARIF direction **lossless on severity**, and it means the scale is externally specified rather than a SpecScribe invention.

**Every level carries a mandatory text label** — `None` / `Note` / `Warning` / `Error` — **in the payload itself**. UX-DR17 ("severity never by color alone") is therefore satisfied by the contract, not by a rendering convention a surface could forget.

`result.kind` — SARIF's separate classification axis (`notApplicable` / `pass` / `open` / `review` / `informational` / `fail`) — is **pinned to `fail`** and stated, rather than left undefined the way Sonar left its two axes.

### 4. Provider values are carried verbatim; the normalized level is derived, never destructive

`severity.provider` is an **array**, carrying every `{softwareQuality, severity}` pair **plus** the legacy `severity`/`type`. `severity.normalized` is derived as the **maximum** over the impacts, so a multi-impact observation can never normalize below its worst quality.

**The normalizer reads `impacts[]` (MQR), not the legacy axis.** Sonar has frozen the legacy fields and MQR is the forward model; and per Context fact 1, choosing differently reorders 54.6 % of the backlog.

**The collapse cost is stated rather than hidden.** Sonar's five levels into four means `BLOCKER` (1 on this repo) and `HIGH` (120) both become `error` — **the single BLOCKER is invisible at normalized granularity**. It survives only in `severity.provider`, and any surface wanting "show me the blocker" must read that array.

**`location.path` is normatively repo-relative and forward-slashed.** Both providers need normalization and in opposite directions: Sonar's `component` is `PROJECT:path` and must be split; SARIF's `artifactLocation.uri` is an **absolute `file://` URI carrying the build machine's path**. Emitting the latter unnormalized would leak a local filesystem path — or a CI runner's workspace path — into a committed artifact.

**`relatedLocations[]` is carried, flattened, and capped.** Sonar's `flows[]` is flows-*of*-locations, two levels of nesting; the profile flattens to SARIF's own flat `relatedLocations` shape. Multi-location is **source-class dependent** — 15.5 % of Sonar issues carry secondary locations (max **52** on one issue) against 0.1 % of raw Roslyn results — so a model designed against either source alone gets it wrong. When a consumer caps the list it **must** set an explicit truncation count; silent truncation is forbidden.

**Deliberately dropped, and why:** `assignee` (no people scoreboard); the Sonar issue `key` (server-assigned and **not stable across re-analysis of a moved line**, so carrying it would imply an identity it does not have); `effort`/`debt` and `cleanCodeAttribute` (Sonar-specific taxonomy with no analogue in the other proven provider — making it structural would make the model Sonar-shaped).

### 5. Attachment is always **labelled**, never asserted — and `requirement` is not a key

A file-scoped observation reaches planning entities through the shipped `PlanningCodeImpact` commit/branch miner. **No second, divergent story↔file mapping may be introduced.** But the miner alone is not sufficient, and this is the decision downstream surfaces most need:

Every observation carries an `attachment` block with a **mandatory, non-nullable `basis`** — `deep-git-commit-mining` / `unavailable` / `none` — a `confidence` that is **never `exact`** for epic or story, and an **`entityCount`** exposing the fan-out.

Three reasons, each measured:

- **The join is authorship history, not ownership.** `PlanningCodeImpact`'s own XML documentation calls it a two-tier best-effort heuristic whose Tier 2 is "a linear-window approximation … deliberately NOT a parent-hash DAG walk". It answers *which story's commits touched this file*. It was built to render a treemap of churn, where 10× fan-out is harmless; per-line assertions are not that.
- **The gate is real and it is the default.** Both call sites are gated on `--deep-git`. **In a default run there is no join at all**: 100 % of observations are unattached.
- **The loss can be silent.** Deep git has already dropped whole surfaces at `errors=0` in this project. Without `basis`, an empty attachment array is the same byte sequence for *"this file genuinely has no planning attachment"* (23–32 % even with deep-git on), *"attachment was never computed"* (the default), and *"attachment was attempted and failed"*. Those are three different facts and consumers must be able to tell them apart.

**`requirement` is NOT a first-class attachment key.** `TraceabilityTemplater` is a requirement-to-**epic** matrix, so `observation → file → epic → requirement` is two hops with the second at epic granularity only — composed on top of a join already amplifying tenfold. A consumer that wants it may derive it; the schema will not imply an edge that does not exist.

**The Epic 19 work graph cannot be the join.** `WorkNodeKind` is `{Epic, Story, Deferred, Action, Spec, Retro}` and `WorkEdgeKind` is `{Contains, StemmedFrom, Resolves, RaisedIn}`. It has **no file nodes and no requirement nodes**. It is a planning↔planning graph. Evaluated, rejected, recorded.

**Unattached observations are a routed population, never a residue.** Their destination is Story 26.6's analysis hub — the only findings surface with no entity precondition, which is why it must work with `--deep-git` off.

### 6. Provenance is **revision-first**; staleness fails closed

A consumer must be able to tell when the analysis predates the working tree. **A timestamp cannot answer this, and on live data it actively misleads:** at authoring time the latest analysis timestamp read "an hour ago" while its revision was **two commits behind** `HEAD`.

The provenance block therefore carries `provider`, `analysisRevision`, `analysisDate`, `workingTreeRevision`, `isStale`, and `commitsBehind`. **`isStale` defaults to `true` when it cannot be computed** — a staleness field that fails open defeats its own purpose. A build-time provider (raw SARIF) sets `analysisRevision = workingTreeRevision`.

### 7. The delivery channel differs by consumer, because the constraints differ

One recommendation for two different constraint sets would be wrong for at least one of them.

- **Story 25.4 → a sharded, gitignored digest artifact** under `.specscribe/analysis/` (the ADR 0014 folder, which exists for exactly this per-repository state). An **8.9 KB index** plus per-file shards at a **median 3.7 KB**, against a 1.49 MB whole digest — because 25.4's use case is *"the files I am about to touch"*, not the whole project. It touches no generated output, so the **golden fingerprint cannot move**, which Epic 25's charter requires.
- **Epic 26 → the Epic 22 IR field.** The IR is generated output, so adding to it **moves the fingerprint** — which is why it is structurally unavailable to 25.4 and entirely appropriate for 26.4, whose AC already expects the move and requires a two-run stability re-baseline.
- **Sonar's official MCP server → adopted as a complement, never as the contract.** It exists, supports SonarQube Cloud, documents Claude Code explicitly, and costs zero code. It also delivers **Sonar's** model, cannot see raw compiler output, cannot attach to planning entities, dies offline, and requires a token. It is strictly better than anything SpecScribe would build for interactive "what does Sonar think of this file?" — and it forfeits the source-agnosticism this record exists to establish. Both, with the roles named.
- **A SpecScribe-emitted MCP surface → deferred.** SpecScribe has no MCP dependency today; adding a server runtime and lifecycle is a new axis that **needs its own ADR**, named here rather than slipped in.

### 8. Enrichment-only, inheriting AD-4

An analysis provider **may enrich output but never owns baseline success**. Absent, stale, unreachable, or unconfigured analysis yields **nothing** — never a failed run, never a broken surface, and never a misleadingly empty one. This is ADR 0020 § 5's enrichment-only constraint applied to a second source class, and it is why the digest channel must have a designed "no analysis configured" state rather than an error path.

## Options considered

| Option | Verdict |
|---|---|
| **Reuse `DiagnosticNotice` / `DiagnosticSeverity` for analysis results** | **Rejected.** Different subject, lifetime, and provenance; and the existing agent-facing serialization is two-level, so reuse means either mislabelling every `note` or widening a contract VS Code already consumes. |
| **The contract *is* SARIF 2.1.0** | **Rejected.** No planning vocabulary (attachment becomes an undocumented profile anyway); 2.6× the bytes; results are not self-describing without the out-of-line rule catalogue. |
| **A bespoke shape that ignores SARIF** | **Rejected.** Forfeits Roslyn/GitHub/SonarQube interoperability for nothing. |
| **A named profile of SARIF 2.1.0** | **Chosen.** Keeps the standard's severity enum and location shape; adds attachment and provenance as declared extensions; inlines rule metadata for agent ergonomics. |
| **Normalize from Sonar's legacy `severity`** | **Rejected.** Frozen by Sonar, and it reorders 54.6 % of the backlog relative to MQR. |
| **A scalar `severity` field** | **Rejected.** Lossy on live data today — 14 issues carry two impacts. |
| **Carry `requirement` as an attachment key** | **Rejected.** Two hops, epic-granular, on top of a 10× join. Derivable; not asserted. |
| **The Epic 19 work graph as the code→planning join** | **Rejected.** No file nodes and no requirement nodes; planning↔planning only. |
| **One channel for both 25.4 and Epic 26** | **Rejected.** 25.4 forbids a fingerprint move; 26.4 expects one. |
| **Adopt Sonar's MCP server as *the* contract** | **Rejected as the contract, adopted as a complement.** Free, and forfeits source-agnosticism, offline behavior, and planning attachment. |

## Consequences

**Accepted willingly:**

- **Six stories bind to one record.** 25.4 and 26.2–26.6 consume this model rather than each inventing one. **Story 26.2 in particular consumes it and must not define a second** — if 26.2's ingestion posture cannot supply an analysis revision (Decision 6), it must **amend this ADR**, not work around it.
- **The severity scale is externally specified**, so the raw-SARIF direction is lossless and the choice is defensible beyond taste.
- **UX-DR17 is satisfied by the contract**, not by convention: the text label ships in the payload.
- **Three empty states are distinguishable** — no findings / unattached / attachment unavailable — which is the difference between an honest surface and one that silently reports "clean".

**Costs, stated plainly:**

- **A fifth vocabulary enters a repo that already has four.** `DiagnosticSeverity`, `GenerationOutcome`, `AdapterDiagnosticCategory`, and the `--status-*` stage words now gain a normalized observation level. Decision 2 argues the subject genuinely differs; a reader still has to learn which is which, and the labels (`Error`, `Warning`) deliberately overlap `DiagnosticSeverity`'s.
- **Provider taxonomy is dropped, not modelled.** `cleanCodeAttribute`, `effort`, and `debt` are real signal a Sonar-only design would have surfaced. Portability was chosen over richness.
- **The single BLOCKER is invisible at normalized granularity.** Any surface that cares must read `severity.provider`.
- **Attachment is honest rather than useful by default.** `confidence` is never `exact` for epic or story, and with `--deep-git` off — the default — attachment is universally absent. Surfaces must be designed for that being the normal case, not the exception.
- **The 10× fan-out is exposed, not solved.** `entityCount` gives every surface the data to bound; **the bounding rule itself is Story 26.5's design decision and the owner's to approve.** This record deliberately stops short of imposing one, because it is a presentation question and Story 26.1 owns visual direction.
- **Two providers is not "general".** The profile is proven on SonarCloud and raw Roslyn — disjoint serializations, disjoint severity scales, partially disjoint rule sets. Other SARIF-emitting analyzers *should* fit; that is an expectation, not a measurement.

**Explicit non-goals:** this record does not decide ingestion posture or credentials (Story 26.2, including the NFR-3 local-first question); does not design any portal surface (Story 26.1's visual direction, 26.4–26.6's implementations); does not authorize an outbound network call or a new SpecScribe runtime (each needs its own decision); and does not bring **coverage** into scope — a per-file metric has no rule identity, message, severity, or location, and Epic 27 (FR42) remains deliberately separate, with an *uncovered-lines range* view named as the one genuine edge case for Epic 27 to decide rather than inherit.

**Amendment surface:** this record constrains ADR 0016's IR only in that Epic 26's IR field must carry the shape defined here and bump `SchemaVersion` per that ADR's rule. It does not otherwise reopen 0016. It inherits ADR 0020 § 5's enrichment-only boundary rather than restating it.
