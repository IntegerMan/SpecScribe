# ADR 0023: An Analysis Observation Is a SARIF Profile, Parallel to SpecScribe's Own Diagnostics

**Status:** **Accepted** 2026-07-28 (authored by Story 25.3; **ratified by the owner** in the Story 25.3 dev pass, as AC #4 requires — six downstream stories bind to this record, and a Proposed ADR is not a contract they can bind to)
**Amended:** 2026-08-07 by the Story 25.3 code review, with the owner's approval. **The structural decisions are unchanged** — this amendment fills gaps and withdraws one over-claimed measurement, so downstream stories may keep binding without a re-ratification round. Changes: **Decision 3's byte argument is WITHDRAWN** (it compared indented against minified JSON; like-for-like the ratio is ~1.0×, not 2.6×); **Decision 4** gains a per-provider `severity.provider` shape, a named `relatedLocationsTruncated`, and an explicit `tags` ruling; **Decision 5** gains a redesigned `basis` enum that is actually implementable, plus `directory`; **Decision 6** gains dirty-tree fields; **new Decision 9** settles observation identity and deduplication; **new Decision 10** settles framework-neutrality for non-BMad repositories; **new Decision 11** settles suppressions; and § *Schema* is added because the original left too many fields undefined for six independent implementers to agree.
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

`## Review Findings` is a **parsed** story section: `EpicsParser` carves it, `EpicsView` carries it as `ReviewFindingsHtml`, and `HtmlRenderAdapter.Epics` renders `<h3>Review Findings</h3>` on **any story page that has one** — the render is guarded on `ReviewFindingsHtml` being non-empty, and because review is an epic-end activity most story pages do not carry the section. Story 26.5 places analysis results on those same pages. *(Amended 2026-08-07: the original read "on **every** story page". The collision is real but conditional, and the naming decision does not need the stronger claim — one page carrying both a human "Review Findings" section and a machine-ingested one is already the ambiguity being avoided.)* Two sections both called "Findings" — one authored by a human reviewer, one ingested from a machine — is the collision `ArtifactCoverage` already has with FR42's "coverage", entered knowingly.

`Insight` is taken (Git Insights, the Insights tab, `FileInsight`). `Coverage` is taken. `Diagnostic` is taken, and Decision 2 explains why the subject differs anyway.

**Reader disambiguation is structural, not typographic:** *"Review Findings"* (human, authored, part of the story record) versus *"Analysis Observations"* (machine, ingested, provider-attributed). "Observation" is also the honest word — it is a third party's claim about the code, not a verdict the project has accepted.

### 2. An observation is **parallel to** `DiagnosticNotice`, not merged into it

The split axis is **subject**. A `DiagnosticNotice` describes *SpecScribe's own run* ("I could not parse your `sprint-status.yaml`"). An `AnalysisObservation` describes *the user's code* ("this regex has no timeout"). They differ further in **lifetime** — a run notice dies with the run, an observation persists and can be stale — and in **provenance**: self versus third party. Merging them would put a generator failure and a code smell in one list.

**The argument for reuse is recorded because it is genuinely strong:** the severity scale, the anchor-root problem, the never-color-alone rendering, and an agent-facing serialization all already exist and are proven in production.

**What decided it was a measured fact the reuse case had not accounted for.** The existing agent-facing serialization is **two-level, not three**. `WebviewCommand.SerializeDiagnostics` emits `severity = notice.Severity == DiagnosticSeverity.Error ? "error" : "warning"`, so `DiagnosticSeverity.Info` — the level Story 4.8 added precisely so `Informational` could mean "nothing to do" — **collapses to `"warning"` on the wire**. Reusing that channel means either pushing a four-level scale through a two-level serializer, silently mislabelling every `note`, or widening a shipped contract the VS Code extension already consumes. Neither is the cheap reuse it appears to be.

**Three things are reused deliberately**, and are named so no downstream story rebuilds them: the **anchor-root normalization** (Decision 4 — observations need no enum, but inherit the answer), the **never-color-alone rule** (Decision 3 puts the text label in the payload), and the **non-fatal posture** (Decision 8).

### 3. The contract is a **named profile of SARIF 2.1.0**, and the normalized severity scale is SARIF's `level` verbatim

Not "is SARIF", and not a divergence:

- **Not plain SARIF**, because SARIF has no epic, story, or requirement — attachment would live in `properties` and therefore be a profile anyway, just an undocumented one. And a SARIF `result` is **not self-describing** — it carries `ruleIndex`, an integer into an out-of-line `tool.driver.rules[]` catalogue, so a single result handed to an agent has no rule name and no help URI. That matters directly for the 25.4 use case, which is handing an agent the observations for one file.

  > ⚠ **Withdrawn 2026-08-07.** This decision originally carried a third reason: that raw SARIF costs **1,793 bytes per result** against **678** for an observation, **2.6×**. The code review found that comparison measures *indented* SARIF against *minified* observations, and sizes an observation record that omits the `attachment` and `provenance` blocks this ADR makes mandatory. Re-measured like-for-like by [`spike/findings/remeasure_dedup.py`](../../spike/findings/remeasure_dedup.py): the SARIF minifies to **1,006 B/result** (indentation alone is **42.4 %** of the on-disk file) against **1,012 B** for a complete observation — a ratio of **~1.0×**. The stated 1,793 B/result is also not reproducible from the committed files (1,455,266 ÷ 834 = **1,745 B**). **The profile is not smaller than SARIF, and no byte argument supports it.** The two reasons above are sufficient on their own and are unaffected; the decision stands, its arithmetic does not.
- **Not a divergence**, because that forfeits free interoperability with Roslyn, GitHub, and SonarQube's own SARIF import for nothing.

**The normalized severity scale is `none` / `note` / `warning` / `error` — SARIF's `result.level` enum, unchanged.** This is the single most load-bearing choice in the record: it makes the raw-SARIF direction **lossless on severity**, and it means the scale is externally specified rather than a SpecScribe invention.

**Every level carries a mandatory text label** — `None` / `Note` / `Warning` / `Error` — **in the payload itself**. UX-DR17 ("severity never by color alone") is therefore satisfied by the contract, not by a rendering convention a surface could forget.

`result.kind` — SARIF's separate classification axis (`notApplicable` / `pass` / `open` / `review` / `informational` / `fail`) — is **pinned to `fail`** and stated, rather than left undefined the way Sonar left its two axes.

### 4. Provider values are carried verbatim; the normalized level is derived, never destructive

`severity.provider` is an **array**, carrying every `{softwareQuality, severity}` pair **plus** the legacy `severity`/`type`. `severity.normalized` is derived as the **maximum** over the impacts, so a multi-impact observation can never normalize below its worst quality.

**Amended 2026-08-07 — `severity.provider` is provider-shaped, and the shape is now specified.** As originally written this field was defined *only* in Sonar's vocabulary (`softwareQuality`, `type`, "legacy axis"), while this ADR simultaneously routes the entire collapse cost through it and instructs Story 26.6 to read it. Six stories cannot parse a field documented for one provider. Every entry is therefore an object with a **mandatory `axis`** discriminator naming the vocabulary, and consumers **must** switch on `axis` and **must** ignore an `axis` they do not recognize rather than failing:

| `axis` | Shape | Emitted by |
|---|---|---|
| `mqr` | `{axis, softwareQuality, severity}` | SonarQube MQR impacts |
| `legacy` | `{axis, severity, type}` | SonarQube frozen legacy fields |
| `sarif` | `{axis, level, defaultLevel}` | any SARIF producer; `defaultLevel` from `rule.defaultConfiguration.level` |

A provider with no analogue emits an empty array, and `severity.normalized` is then whatever the provider's own level maps to. **A surface wanting "show me the blocker" reads entries where `axis == "mqr"`** — stated here so it is not rediscovered per surface.

**`severity.normalized` when the provider supplies no severity at all** is `warning`, not `note`. An unrated finding is not evidence of low severity, and normalizing an unknown *downward* fails open — the same defect this record rejects for `isStale`. An unrecognized provider value maps to `warning` for the same reason, never silently to the quietest level.

**The normalizer reads `impacts[]` (MQR), not the legacy axis.** Sonar has frozen the legacy fields and MQR is the forward model; and per Context fact 1, choosing differently reorders 54.6 % of the backlog.

**The collapse cost is stated rather than hidden.** Sonar's five levels into four means `BLOCKER` (1 on this repo) and `HIGH` (120) both become `error` — **the single BLOCKER is invisible at normalized granularity**. It survives only in `severity.provider`, and any surface wanting "show me the blocker" must read that array.

**`location.path` is normatively repo-relative and forward-slashed.** Both providers need normalization and in opposite directions: Sonar's `component` is `PROJECT:path` and must be split; SARIF's `artifactLocation.uri` is an **absolute `file://` URI carrying the build machine's path**. Emitting the latter unnormalized would leak a local filesystem path — or a CI runner's workspace path — into a committed artifact.

**`relatedLocations[]` is carried, flattened, and capped.** Sonar's `flows[]` is flows-*of*-locations, two levels of nesting; the profile flattens to SARIF's own flat `relatedLocations` shape. Multi-location is **source-class dependent** — 15.5 % of Sonar issues carry secondary locations (max **52** on one issue) against 0.1 % of raw Roslyn results — so a model designed against either source alone gets it wrong. When a consumer caps the list it **must** set an explicit truncation count; silent truncation is forbidden.

**Amended 2026-08-07 — the truncation field is named and defined.** It is **`relatedLocationsTruncated`**, an integer equal to **the number of entries DROPPED** (not the original total, which is recoverable as `len(relatedLocations) + relatedLocationsTruncated`). It is `0`, never absent, when nothing was dropped. The original mandated the count without naming it or defining it, which guaranteed two implementers would pick opposite meanings and differ by exactly the cap. **The cap *value* remains deliberately unset** and per-surface (Story 26.1's, with the owner); what is now fixed is that the count means the same thing everywhere.

**Each entry in `relatedLocations[]` carries the same `path` contract as `location.path`** — repo-relative and forward-slashed, normalized identically. The original stated the rule for `location.path` only and did not extend it, and the spike's own reference mapper duly emitted raw absolute `file://` build-machine URIs in exactly this field. The rule is one rule.

**`tags` is NOT a field of this profile.** Provider tags (Sonar's `tags[]`, a SARIF rule's `properties.tags`) are **dropped**, for the same reason as `cleanCodeAttribute`: they are provider taxonomy with no cross-provider meaning, and modelling them would make the record shaped like whichever provider was implemented first. *(Amended 2026-08-07: the spike report described tags as "folded into the optional `tags` field where present" while its own mapper appended them to the loss list and no such field was ever defined. Dropped is the ruling; a surface that wants them reads `severity.provider`'s sibling raw payload, which this contract does not carry.)*

**Deliberately dropped, and why:** `assignee` (no people scoreboard); the Sonar issue `key` (server-assigned and **not stable across re-analysis of a moved line**, so carrying it would imply an identity it does not have); `effort`/`debt` and `cleanCodeAttribute` (Sonar-specific taxonomy with no analogue in the other proven provider — making it structural would make the model Sonar-shaped).

### 5. Attachment is always **labelled**, never asserted — and `requirement` is not a key

A file-scoped observation reaches planning entities through the shipped `PlanningCodeImpact` commit/branch miner. **No second, divergent story↔file mapping may be introduced.** But the miner alone is not sufficient, and this is the decision downstream surfaces most need:

Every observation carries an `attachment` block with a **mandatory, non-nullable `basis`**, a `confidence` that is **never `exact`** for epic or story, and an **`entityCount`** exposing the fan-out.

**Amended 2026-08-07 — the original `basis` enum could not produce the distinction it exists for.** Its three values were `deep-git-commit-mining` / `unavailable` / `none`, which mixed one *method* value with two *outcome* values, and — fatally — the failure mode this record cites as its own justification is **indistinguishable from the gate being off**: a deep-git timeout returns `errors=0` **and zero commits**, which is byte-identical at `progress?.DeepGit?.Commits is { Count: > 0 }` to `--deep-git` never having been passed. An implementer therefore could not emit the three-way distinction using the mechanism named here. `basis` is now **two orthogonal fields**:

| Field | Values | Meaning |
|---|---|---|
| `basis.method` | `deep-git-commit-mining` \| `none` | **How** attachment was attempted. `none` = not attempted. |
| `basis.outcome` | `computed` \| `not-attempted` \| `degraded` \| `failed` \| `no-planning-model` | **What came back.** |

- `computed` — the join ran and its answer is complete for the window it saw. Empty `epics`/`stories` then means *genuinely unattached*, which is a real and useful fact.
- `not-attempted` — the gate was off. **This is the default run.**
- `degraded` — the join ran against a **bounded or partial** window: the 300-commit horizon was hit, or mining stopped early. Attachment is real but **incomplete**, and a consumer must not read an absent entity as absence. The original had no value for this state at all.
- `failed` — attempted and did not produce a usable result.
- `no-planning-model` — see Decision 10.

**Emitters that cannot distinguish `computed` from `failed`** — which is the situation today, given the gate above — **must emit `degraded`**, never `computed`. Fail closed: an emitter unsure whether the join succeeded says so, exactly as `isStale` does. Closing the gap in the shipped gate so `computed` becomes emittable is a **defect to fix in the miner**, not a contract concession, and is named here so it is not mistaken for acceptable behaviour.

**`attachment.attachedAtRevision`** records the revision the join was computed against. Attachment is mined from local git at emit time while the observations describe `provenance.analysisRevision`; when those differ — the normal case, since `isStale: true` is expected — a consumer can now see it. The original had no field for this.

**`entityCount` is per-granularity**, not a scalar: `entityCount: { epics: N, stories: M }`. The measured fan-out differs by granularity (7.33 epics against 10.02 stories), so one integer cannot carry it, and the worked example in the spike report showed the *epic* count beside a *story* list. It is `{ epics: 0, stories: 0 }` — present, never absent, never null — whenever `outcome` is anything other than `computed` or `degraded`. Story 26.5's bounding rule reads this field, so its granularity has to be unambiguous.

**`confidence` is an enumerated scale**, not a free constraint: `approximate` | `weak`. `exact` **is not a member** — it was previously excluded by prose ("never `exact` for epic or story") that implied some other key could be `exact` when no such key exists. `weak` is for `degraded`; `approximate` is the best this join offers.

Three reasons, each measured:

- **The join is authorship history, not ownership.** `PlanningCodeImpact`'s own XML documentation calls it a two-tier best-effort heuristic whose Tier 2 is "a linear-window approximation … deliberately NOT a parent-hash DAG walk". It answers *which story's commits touched this file*. It was built to render a treemap of churn, where 10× fan-out is harmless; per-line assertions are not that.
- **The gate is real and it is the default.** Both call sites are gated on `--deep-git`. **In a default run there is no join at all**: 100 % of observations are unattached.
- **The loss can be silent.** Deep git has already dropped whole surfaces at `errors=0` in this project. Without `basis`, an empty attachment array is the same byte sequence for *"this file genuinely has no planning attachment"* (23–32 % even with deep-git on), *"attachment was never computed"* (the default), and *"attachment was attempted and failed"*. Those are three different facts and consumers must be able to tell them apart.

**`directory` is NOT a first-class attachment key either** *(added 2026-08-07)*. AC #1 named file, directory, epic, story and requirement; the original record answered four of the five and passed over `directory` in silence, which is the "decision made by accident" the story warned against. It is **derived, not carried**: any directory rollup is a prefix aggregation over `location.path`, which every observation already has, so a stored key would be a second source of truth for something computable and exact. Unlike `requirement`, nothing is lost — the derivation is not lossy or approximate, it is string prefixing.

**`requirement` is NOT a first-class attachment key.** `TraceabilityTemplater` is a requirement-to-**epic** matrix, so `observation → file → epic → requirement` is two hops with the second at epic granularity only — composed on top of a join already amplifying tenfold. A consumer that wants it may derive it; the schema will not imply an edge that does not exist.

**The Epic 19 work graph cannot be the join.** `WorkNodeKind` is `{Epic, Story, Deferred, Action, Spec, Retro}` and `WorkEdgeKind` is `{Contains, StemmedFrom, Resolves, RaisedIn}`. It has **no file nodes and no requirement nodes**. It is a planning↔planning graph. Evaluated, rejected, recorded.

**Unattached observations are a routed population, never a residue.** Their destination is Story 26.6's analysis hub — the only findings surface with no entity precondition, which is why it must work with `--deep-git` off.

### 6. Provenance is **revision-first**; staleness fails closed

A consumer must be able to tell when the analysis predates the working tree. **A timestamp cannot answer this, and on live data it actively misleads:** at authoring time the latest analysis timestamp read "an hour ago" while its revision was **two commits behind** `HEAD`.

The provenance block therefore carries `provider`, `analysisRevision`, `analysisDate`, `workingTreeRevision`, `isStale`, and `commitsBehind`. **`isStale` defaults to `true` when it cannot be computed** — a staleness field that fails open defeats its own purpose. A build-time provider (raw SARIF) sets `analysisRevision = workingTreeRevision`.

**Amended 2026-08-07 — three fields the original omitted and the first implementation had to invent.**

- **`workingTreeDirty`** (boolean). Line numbers are anchored to `analysisRevision`; uncommitted edits move them. A clean-tree comparison that reports `isStale: false` while the tree is dirty is telling a consumer the line numbers are trustworthy when they are not. This is itself a staleness condition.
- **`staleReasons`** (array). `isStale` alone cannot distinguish *behind by two commits* from *ancestry could not be computed* from *tree is dirty*, and those imply different consumer behaviour. Values: `analysis-behind-working-tree`, `working-tree-dirty`, `commits-behind-not-computable`, `analysis-revision-unknown`.
- **`commitsBehind` is `null`, never a number, when `analysisRevision` is not an ancestor of `workingTreeRevision`.** After a force-push, a rebase, or an analysis run on another branch, `git rev-list --count rev..HEAD` still returns an integer — but it counts commits reachable from HEAD and not from the revision, which for a diverged history is not "how far behind" and must not be presented as though it were. Emitters **must** test ancestry (`git merge-base --is-ancestor`) before reporting a number, and set `staleReasons: ["commits-behind-not-computable"]` when it fails.

These are not new requirements invented by the review: the digest that shipped downstream already carries `workingTreeDirty` and `staleReasons`, because the implementation could not do without them. The contract is being corrected to match what implementing it actually required — the drift is the finding.

### 7. The delivery channel differs by consumer, because the constraints differ

One recommendation for two different constraint sets would be wrong for at least one of them.

- **Story 25.4 → a sharded, gitignored digest artifact** under `.specscribe/analysis/` (the ADR 0014 folder, which exists for exactly this per-repository state). An **8.9 KB index** plus per-file shards at a **median 3.7 KB**, against a 1.49 MB whole digest — because 25.4's use case is *"the files I am about to touch"*, not the whole project. It touches no generated output, so the **golden fingerprint cannot move**, which Epic 25's charter requires.
- **Epic 26 → the Epic 22 IR field.** The IR is generated output, so adding to it **moves the fingerprint** — which is why it is structurally unavailable to 25.4 and entirely appropriate for 26.4, whose AC already expects the move and requires a two-run stability re-baseline.
- **Sonar's official MCP server → adopted as a complement, never as the contract.** It exists, supports SonarQube Cloud, documents Claude Code explicitly, and costs zero code. It also delivers **Sonar's** model, cannot see raw compiler output, cannot attach to planning entities, dies offline, and requires a token. It is strictly better than anything SpecScribe would build for interactive "what does Sonar think of this file?" — and it forfeits the source-agnosticism this record exists to establish. Both, with the roles named.
- **A SpecScribe-emitted MCP surface → deferred.** SpecScribe has no MCP dependency today; adding a server runtime and lifecycle is a new axis that **needs its own ADR**, named here rather than slipped in.

### 8. Enrichment-only, inheriting AD-4

An analysis provider **may enrich output but never owns baseline success**. Absent, stale, unreachable, or unconfigured analysis yields **nothing** — never a failed run, never a broken surface, and never a misleadingly empty one. This is ADR 0020 § 5's enrichment-only constraint applied to a second source class, and it is why the digest channel must have a designed "no analysis configured" state rather than an error path.

### 9. An observation has an identity, and duplicates are merged rather than shipped twice *(added 2026-08-07)*

The original record defined **no identity at all**. It deliberately dropped Sonar's `key` as unstable across a
re-analysis of a moved line — correct — and put nothing in its place. That is untenable given what this same record
recommends: Decision 7 adopts Sonar's MCP server **alongside** the digest, and Story 26.7 is told to build pluggable
normalizers over one shared model. Two providers reporting the same defect is the **normal case**, not an edge case:
re-measured on this repository, **810 of 834** raw Roslyn results are defects SonarQube had already imported as
`external_roslyn:*`, so a naive union inflates the corpus by **~45 %**.

**Identity is the tuple `(rule.id-without-provider-prefix, location.path, location.startLine)`.** It is content-derived,
so it is stable across a re-analysis that does not move the code, and computable by every provider without a server
round-trip.

**Two observations with equal identity are ONE observation.** The merge is: keep the **maximum** `severity.normalized`;
**concatenate** `severity.provider` so both providers' vocabularies survive (this is why that field is an array with
an `axis` discriminator); keep the longest `message`; union `relatedLocations`; and record every contributing provider
in **`providers[]`**, which replaces the scalar `provider` on a merged record.

**Stated honestly:** identity is *not* stable across a refactor that moves a line, so it answers "is this the same
finding right now" and **not** "is this the same finding as last week". Cross-run new-vs-resolved comparison is
therefore **still not supported** by this contract, and any surface that wants it must define its own durable key —
which is a real limitation and is named rather than left to be discovered.

### 10. In a repository with no planning model, attachment is absent by declaration *(added 2026-08-07)*

NFR8 is the property this record exists to establish, and the original discharged it with one table cell reading
"BMad-neutral ✅ attachment optional" — while making `basis` mandatory and non-nullable and naming its keys `epics`
and `stories`. Those are BMad vocabulary. Epics 11–15 exist because Spec Kit, GSD and frameworkless repositories are
real cases, and an emitter in one of them had no legal value to write.

**`basis.outcome` gains `no-planning-model`**, which means *this repository exposes no planning entities to attach to*
— distinct from `not-attempted` (the gate was off) and from `computed` with empty arrays (a planning model exists and
this file matched nothing in it). `entityCount` is `{ epics: 0, stories: 0 }`.

**`epics` and `stories` are the BMad projection of a general shape, not the shape itself.** The general contract is:
attachment names **planning entities the portal already projects**, and a framework adapter maps its own vocabulary
onto these two levels — container and unit of work — or declares `no-planning-model` and attaches nothing. Everything
else in the record (rule, severity, location, message, provenance) is **framework-independent by construction** and is
what a frameworkless repository still gets in full. That is the honest scope of NFR8 here: the *observation* is
framework-neutral; *attachment* is framework-shaped and says so.

### 11. A suppressed finding is not an observation *(added 2026-08-07)*

The SonarQube side of this contract is filtered `resolved=false`. The SARIF side had no equivalent and never read
`result.suppressions[]` — the SARIF-standard carrier for `#pragma warning disable`, `[SuppressMessage]`, and baseline
suppressions — so a diagnostic the developer had explicitly rejected would enter the model indistinguishable from an
open one, and reach story pages via Story 26.5.

**A SARIF result with a non-empty `suppressions[]` is NOT emitted as an observation.** A developer's suppression is
this project's answer to a third party's claim, and this record's own stated posture is that an observation is "a
claim, not a verdict the project has accepted" — ingesting the rejection of a claim as though it were the claim
inverts that.

**Correspondingly, `result.kind` other than `fail` is not emitted either.** The original pinned `kind` to `fail` on
output without saying what to do with a `pass` / `notApplicable` / `informational` / `review` input. Pinning a
constant is not deciding; a non-`fail` result is not a finding and is skipped.

## Schema

*Added 2026-08-07. The original specified fields in prose across eight decisions, which left enough undefined that six
independent implementers could not have agreed. This is the normative shape.*

```jsonc
{
  "providers": ["sonarcloud"],              // non-empty; >1 only on a merged record (Decision 9)
  "rule":  { "id": "csharpsquid:S1192",     // "{provider-prefix}:{id}"; the prefix is the provider's own
             "name": "…|null",              // null when the provider needs a second call to supply it
             "helpUri": "…|null" },
  "severity": {
    "normalized": "error|warning|note|none",   // SARIF result.level, verbatim
    "label": "Error|Warning|Note|None",        // MANDATORY; UX-DR17 lives in the payload
    "provider": [ { "axis": "mqr|legacy|sarif", "…": "per Decision 4" } ]
  },
  "location": { "path": "src/Foo.cs|null",  // repo-relative, forward-slashed; null = project-level
                "startLine": 1, "startColumn": 1, "endLine": 1, "endColumn": 20 },
  "relatedLocations": [ { "path": "…", "startLine": 1, "message": "…|null" } ],
  "relatedLocationsTruncated": 0,           // entries DROPPED; 0, never absent
  "message": "…",
  "attachment": {
    "basis": { "method": "deep-git-commit-mining|none",
               "outcome": "computed|not-attempted|degraded|failed|no-planning-model" },
    "attachedAtRevision": "…|null",
    "confidence": "approximate|weak",
    "epics": [], "stories": [],
    "entityCount": { "epics": 0, "stories": 0 }
  },
  "provenance": {
    "provider": "sonarcloud", "analysisRevision": "…", "analysisDate": "…",
    "workingTreeRevision": "…", "workingTreeDirty": false,
    "isStale": true,                        // defaults TRUE when uncomputable
    "commitsBehind": null,                  // null when analysisRevision is not an ancestor
    "staleReasons": []
  }
}
```

**`location.path` is nullable.** Project-level SonarQube issues (a `component` with no path) and location-less
compiler results both legitimately have none. A null-path observation is **not silently dropped** and **not written to
any per-file shard**; it routes to the index's `projectLevel[]` collection. The original declared the field
normatively repo-relative with no null case while its own reference mapper produced nulls.

**A consumer receiving a `location.path` that is not repo-relative — absolute, or escaping the root — MUST reject the
observation and count the rejection**, rather than emitting it or silently repairing it. A normative rule with no
enforcement clause is how the absolute-build-machine-path leak this record forbids ships anyway.

**Shard keys.** The sharded digest of Decision 7 keys on `location.path`, and because that string comes from a
provider it is **untrusted input to a filesystem write**. An emitter **must**: reject any path that is absolute or
contains a `..` segment; percent-encode every character outside `[A-Za-z0-9._-]` and the `/` separator, which covers
Windows-illegal `:<>|*?`; cap the encoded name and disambiguate an over-long path with a hash suffix; and key
case-sensitively while detecting case-collisions on case-insensitive filesystems and merging them into one shard
rather than letting one overwrite the other. The literal shard name `(none)` is **not** used — project-level
observations live in the index, per above.

**`level: none` is unreachable from SonarQube** — both its axes bottom out at `note` — and reachable only from a SARIF
producer that emits `level: "none"` on a `fail` result. The `None` label is defined so the enum is total, not because
this repository's providers can produce it. Story 26.1 should design four levels and expect three.

## Options considered

| Option | Verdict |
|---|---|
| **Reuse `DiagnosticNotice` / `DiagnosticSeverity` for analysis results** | **Rejected.** Different subject, lifetime, and provenance; and the existing agent-facing serialization is two-level, so reuse means either mislabelling every `note` or widening a contract VS Code already consumes. |
| **The contract *is* SARIF 2.1.0** | **Rejected.** No planning vocabulary (attachment becomes an undocumented profile anyway); results are not self-describing without the out-of-line rule catalogue. *(The "2.6× the bytes" reason was withdrawn 2026-08-07 — like-for-like the ratio is ~1.0×. The remaining reasons carry the rejection.)* |
| **A bespoke shape that ignores SARIF** | **Rejected.** Forfeits Roslyn/GitHub/SonarQube interoperability for nothing. |
| **A named profile of SARIF 2.1.0** | **Chosen.** Keeps the standard's severity enum and location shape; adds attachment and provenance as declared extensions; inlines rule metadata for agent ergonomics. |
| **Normalize from Sonar's legacy `severity`** | **Rejected.** Frozen by Sonar, and it reorders 54.6 % of the backlog relative to MQR. |
| **A scalar `severity` field** | **Rejected.** Lossy on live data today — 14 issues carry two impacts. |
| **Carry `requirement` as an attachment key** | **Rejected.** Two hops, epic-granular, on top of a 10× join. Derivable; not asserted. |
| **The Epic 19 work graph as the code→planning join** | **Rejected.** No file nodes and no requirement nodes; planning↔planning only. |
| **One channel for both 25.4 and Epic 26** | **Rejected.** 25.4 forbids a fingerprint move; 26.4 expects one. |
| **Adopt Sonar's MCP server as *the* contract** | **Rejected as the contract, adopted as a complement.** Free, and forfeits source-agnosticism, offline behavior, and planning attachment. |
| **Ship duplicate observations from overlapping providers** *(2026-08-07)* | **Rejected.** Measured at 810 of 834 raw Roslyn results already imported by Sonar — a ~45 % inflation. Decision 9 defines identity and a merge. |
| **A durable cross-run observation identity** *(2026-08-07)* | **Rejected for now.** Content-derived identity cannot survive a line move, and a durable key needs a store this contract does not have. New-vs-resolved is explicitly out of scope rather than implied. |
| **A single `basis` enum mixing method and outcome** *(2026-08-07)* | **Rejected.** Could not distinguish a deep-git timeout from the gate being off — the two states return an identical zero-commit result — so the record's own justification was unimplementable. Split into `method` + `outcome`. |
| **Emit suppressed SARIF results as observations** *(2026-08-07)* | **Rejected.** A suppression is this project's answer to a third party's claim; ingesting it as the claim inverts the record's stated posture. |
| **Carry `tags` as a profile field** *(2026-08-07)* | **Rejected.** Provider taxonomy with no cross-provider meaning, exactly as `cleanCodeAttribute`. |
| **Carry `directory` as an attachment key** *(2026-08-07)* | **Rejected.** A prefix aggregation over `location.path` is exact and lossless — a stored key would be a second source of truth. Unlike `requirement`, nothing is given up. |

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

**Costs added 2026-08-07:**

- **The record roughly doubled in size.** A complete observation is **~1,012 B** against the **~511 B** originally
  sized, because `attachment` and `provenance` are mandatory and were never counted. Deduplication returns ~15 % of
  that. Story 25.4's digest is correspondingly larger than § 10.1 of the spike report states, and **the profile is
  no smaller than raw SARIF** — it is justified by shape and self-description, not by bytes.
- **Merging is now a required emitter behaviour**, not an optimization. An emitter that skips Decision 9 ships a
  ~45 % inflated corpus on this repository.
- **`computed` is not currently emittable** by the shipped miner, so real digests will carry `degraded` until the
  gate is fixed. That is honest, and it is worse-looking than the original contract implied.

**Amendment surface:** this record constrains ADR 0016's IR only in that Epic 26's IR field must carry the shape defined here and bump `SchemaVersion` per that ADR's rule. It does not otherwise reopen 0016. It inherits ADR 0020 § 5's enrichment-only boundary rather than restating it.

> ⚠ **Noted 2026-08-07 — this Accepted record binds a load-bearing decision to a Proposed one.** Decision 7 routes
> Epic 26 to the Epic 22 IR field, and the clause above binds that to **[ADR 0016](0016-ir-carries-rendered-prose-html.md),
> which is still `Proposed`**. AC #4 required *this* record's ratification on the grounds that "a Proposed ADR is not
> a contract downstream stories can bind to" — the same argument applies here and was not applied. **The 25.4 half of
> Decision 7 (the sharded digest) is unaffected** and depends only on Accepted records (0014). The Epic 26 half should
> be treated as provisional until 0016 is ratified, and 26.4 should not be planned as though the IR shape is settled.
