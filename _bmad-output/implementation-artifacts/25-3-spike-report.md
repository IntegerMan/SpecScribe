# Story 25.3 — Spike Report: A Framework-Neutral Findings Contract for AI Agents

**Status:** complete · **Timebox:** ~2 days, used ~1 · **Date:** 2026-07-28
**Story baseline:** `40c7ee9` · **Session HEAD:** `06b300c` · **Sonar analysis revision:** `d1722f17`
**Durable deliverable:** [ADR 0023](../../docs/adrs/0023-agent-facing-analysis-observation-contract.md) (must land **Accepted**)
**Disposable evidence:** `spike/findings/` — two raw SARIF logs + two mapping scripts, quarantined per [`spike/README.md`](../../spike/README.md)

> **Ships no production code.** No `src/`, `tests/`, `web/`, or `extension/` edits. `GoldenContentFingerprint` unmoved — see § 12.

---

## 0. Executive summary

The contract is decidable and it is decided. The model is a **profile of SARIF 2.1.0**, named
**`AnalysisObservation`** (never "finding" — § 3), kept **parallel to** rather than merged into SpecScribe's
shipped `DiagnosticNotice` (§ 2), with a **4-level normalized severity that is SARIF's `level` enum verbatim**
(§ 5) and the provider's own severity values carried alongside, un-normalized.

Six things this spike measured that change what a downstream story would otherwise build:

| # | Measured | Consequence |
|---|---|---|
| **F1** | **54.6 %** of live issues (800 / 1,466) normalize to a **different** level depending on which Sonar severity axis you read | The axis is not a detail. Pin it in the ADR or two surfaces disagree by design. § 5 |
| **F2** | **14** issues carry **two** `impacts[]` entries today — and the `impactSeverities` facet **counts issues, not impact pairs**, so it can never reveal this | A scalar severity field is lossy on **live** data now, not hypothetically. The story's own R5 inferred the opposite from the facet. § 5 |
| **F3** | Observation-weighted attachment fan-out is **7.33 epics** / **10.02 stories** per attached observation; 1,765 attached observations generate **15,758** story edges | Story 26.5's "use the existing miner as the join" ships a 10× amplification unless bounded. **The single most consequential handoff in this report.** § 7 |
| **F4** | Multi-location is **source-class dependent**: 15.5 % of Sonar issues carry secondary locations (max **52**); raw Roslyn carries them on **0.1 %** | A model designed against either source alone gets this wrong. § 6 |
| **F5** | The shipped agent-facing channel is **2-level** (`"error"`\|`"warning"`) — `Info` silently becomes `"warning"` | R1's "reuse the existing serialization" is cheaper on paper than in fact. § 2 |
| **F6** | Latest analysis timestamp reads **today**; its revision is **2 commits behind** HEAD | Staleness by timestamp is not merely imprecise, it is **actively wrong**. § 9 |
| **F7** | `generate --deep-git` run 8× returned **739 pages on some runs and 436 on others**, `errors=0` every time | The silent deep-git loss is **reproducible on demand**, not folklore. It makes `attachment.basis` load-bearing, not defensive. § 13.4 |

**Channel recommendation is split, because the constraints are** (§ 10):

- **Story 25.4 → a sharded, gitignored digest artifact.** Index 8.9 KB + per-file shards at a median 3.7 KB.
  Fingerprint-safe, offline-honest, subset-consumable.
- **Epic 26 → the Epic 22 IR field.** It moves the fingerprint, which 25.4 forbids and 26.4 explicitly expects.
- **Sonar's official MCP server → adopt as a complement, not as the contract.** Zero code, but it delivers
  *Sonar's* model and forfeits the source-agnosticism this spike exists to establish.
- **A SpecScribe-emitted MCP surface → defer.** Needs a new runtime and its own ADR.

---

## 1. What was actually run

| Evidence | How | Result |
|---|---|---|
| Live Sonar issue set | `api/issues/search`, `resolved=false`, 3 pages × 500 | **1,466** unresolved |
| Live facets | `severities,impactSeverities,types,cleanCodeAttributeCategories,rules` | § 5 |
| Analysis revisions | `api/project_analyses/search` | `d1722f17`, 2026-07-28T01:56Z |
| Raw Roslyn SARIF | `dotnet build -t:Rebuild -p:ErrorLog=…%2cversion=2.1`, **one project at a time** | **834** results |
| Two-way mapping | [`spike/findings/map_to_model.py`](../../spike/findings/map_to_model.py) | § 4–§ 6 |
| Channel sizing | [`spike/findings/measure_channels.py`](../../spike/findings/measure_channels.py) | § 10 |
| Attachment | `specscribe generate --deep-git`, parsed from `impact-map.html` + 162 story pages | § 7 |
| SARIF spec | OASIS doc, fetched | § 4 |
| Sonar MCP server | GitHub README, fetched | § 10 |

**The numbers move fast.** Unresolved issue count: **1,360** (25.2, 07-26) → **1,420** (25.3 authoring, 07-27) →
**1,466** (this spike, 07-28). Roughly +50/day. Every figure here is 2026-07-28.

### 1.1 Three build traps, not two

The story named two (`%2c` escaping; `roslyn#24319` multi-project). A **third** bit first and is worth recording:

> **An up-to-date build writes no SARIF at all.** The first `dotnet build` with `ErrorLog` set reported
> `0 Warning(s)` and produced **no file** — the compiler never ran. `-t:Rebuild` is required.

And the story's suggested sanity check worked exactly as designed. Building only `src/SpecScribe` gave **261**
results against Sonar's ~819 `external_roslyn:*` — the "wildly smaller number" that means the trap bit. Adding
`tests/SpecScribe.Tests` separately brought the total to **834**, which reconciles:

| Rule | Raw SARIF | Sonar `external_roslyn:*` |
|---|---|---|
| CA1861 | 339 | 338 |
| SYSLIB1045 | 277 | 269 |
| CA1859 | 69 | 69 |
| CA1816 | 46 | 45 |
| CA1822 | 32 | 32 |
| **Total** | **834** | **819** |

The residual (~15) is drift: Sonar analyzed `d1722f17`, the build ran at `06b300c`.

---

## 2. R1 settled — **parallel**, not merged

**Decision: `AnalysisObservation` is a new type, parallel to `DiagnosticNotice`. The losing argument is recorded.**

The split axis is **subject**, exactly as the story framed it: `DiagnosticNotice` describes *SpecScribe's own run*
("I could not parse your `sprint-status.yaml`"); an `AnalysisObservation` describes *the user's code* ("this regex
has no timeout"). They also differ in **lifetime** (a run notice dies with the run; an observation persists across
runs and can be stale — § 9) and **provenance** (self vs. third party).

**The argument for reuse, recorded because it is genuinely strong:** the severity scale, the anchor-root problem
(`DiagnosticAnchorRoot`, already solved and extended twice — Story 6.12 added `Adr`, Story 18.2 added `Repo`), the
never-color-alone rendering, and an agent-facing JSON-lines serialization all already exist and are proven in
production.

**What tipped it — F5, which the story's R1 table does not record.** The existing agent-facing serialization is
not 3-level. `Commands.SerializeDiagnostics` emits:

```csharp
severity = notice.Severity == DiagnosticSeverity.Error ? "error" : "warning",
```

`DiagnosticSeverity.Info` — the level Story 4.8 added specifically so `Informational` could mean "FYI, nothing to
do" — **collapses to `"warning"` on the wire**. So reusing the channel means either shipping a 4-level scale
through a 2-level serializer (silently mislabelling every `note`), or widening a shipped contract that VS Code's
`DiagnosticCollection` already consumes (`extension/src/extension.ts`). Neither is the cheap reuse R1 implied.

**What IS reused — deliberately, and named so nobody rebuilds it:**

1. **The anchor-root concept.** `AnalysisObservation.location.path` is always **repo-relative, forward-slashed** —
   the same normalization `SerializeDiagnostics` performs for `DiagnosticAnchorRoot.Source`. Observations need no
   enum because an analyzer always speaks about repo files; the *problem* is the same and the *answer* is inherited.
2. **The never-color-alone rule.** Severity carries a mandatory text `label` in the payload itself (§ 5), not as a
   rendering convention a surface could forget.
3. **The non-fatal posture.** AD-4: an absent or broken provider yields **nothing**, never a failed run.

---

## 3. R2 settled — the model is called **Observation**

"Finding" is taken, and not loosely. `## Review Findings` is a **parsed** story section:

| Site | What |
|---|---|
| `EpicsParser.cs:253` | carves `"## Review Findings"` out of the story body |
| `EpicsView.cs:303` | carries it as `ReviewFindingsHtml` |
| `HtmlRenderAdapter.Epics.cs:609` | renders `<h3>Review Findings</h3>` on **every** story page |

Story 26.5 puts analysis findings **on those same story pages**. Two sections both called "Findings", one from a
human code review and one from a static analyzer, is the `ArtifactCoverage`-vs-FR42 collision repeating knowingly.

**Rejected alternatives, and why:** `Insight` — taken (Git Insights, the Insights tab, `FileInsight`).
`Coverage` — taken (`ArtifactCoverage`, and Epic 27 needs it). `Diagnostic` — taken, and § 2 just established the
subject differs. `Signal` — 54 source files already use the word in prose.

**`Observation`** appears in only 2 source files, both in comment prose, and it says the right thing: a third party
*observed* something about your code. It is a claim, not a verdict — which is honest about a 1,466-issue backlog
where the project has decided many are acceptable.

Reader disambiguation on a story page is therefore structural, not typographic: **"Review Findings"** (human,
authored, part of the story record) vs **"Analysis Observations"** (machine, ingested, provider-attributed).

---

## 4. R4 settled — a **named profile of SARIF 2.1.0**, option (b)

**Confirmed at spike time:** SARIF 2.1.0 is an OASIS Standard incorporating Approved Errata 01, **28 August 2023**.
`result.level` is a **4-valued** enum — `none` / `note` / `warning` / `error` — defaulting to `warning` when absent.

> **Unrecorded axis found:** SARIF also carries `result.kind` (`notApplicable` / `pass` / `open` / `review` /
> `informational` / `fail`), a **classification** axis orthogonal to `level`. The profile pins `kind` to `fail` and
> says so, rather than leaving a second axis undefined the way Sonar left its two (§ 5).

**Why not (a) "the contract IS SARIF":**

- **No planning vocabulary.** SARIF has no epic, story, or requirement. Attachment (§ 7) would live in
  `properties`, i.e. it would be a profile anyway — just an undocumented one.
- **Verbosity, measured.** Raw SARIF costs **1,793 B/result** (1.43 MB for 834 results), against **678 B** for the
  same information as an observation — **2.6× smaller**. Most of the difference is the out-of-line rule catalogue:
  331 and 420 rule descriptors for 261 and 573 results respectively.
- **Results are not self-describing.** A SARIF `result` carries `ruleIndex`, an integer into
  `tool.driver.rules[]`. Hand an agent one result and it has no rule name, no help URI, no category. The profile
  **inlines** `rule.name` and `rule.helpUri` per observation — the single biggest agent-ergonomics change.

**Why not (c) "deliberate divergence":** unjustifiable. Roslyn emits SARIF natively (proven here), GitHub code
scanning consumes it, SonarQube imports it via `sonar.sarifReportPaths`. Inventing an unrelated shape would forfeit
free interoperability for nothing.

**What "profile" buys, concretely:** the normalized severity scale is **SARIF's `level` enum verbatim** (§ 5),
which makes the raw-SARIF direction **lossless on severity** and gives a defensible, externally-specified
4-level scale instead of a SpecScribe invention.

---

## 5. R5 settled — read `impacts[]`, carry everything, normalize to SARIF `level`

Live facets, `resolved=false`, 2026-07-28 (**1,466** unresolved):

| Legacy `severities` | Count | | MQR `impactSeverities` | Count |
|---|---|---|---|---|
| INFO | **798** | | BLOCKER | 1 |
| MINOR | 385 | | HIGH | 120 |
| MAJOR | 164 | | MEDIUM | **960** |
| CRITICAL | 118 | | LOW | 385 |
| BLOCKER | 1 | | INFO | **0** |

The story's R5 holds and is now quantified. Normalizing the same 1,466 issues through each axis:

| Normalized | via MQR `impacts[]` | via legacy `severity` |
|---|---|---|
| `error` | 121 | 119 |
| `warning` | **960** | **164** |
| `note` | 385 | **1,183** |

> ### F1 — **800 of 1,466 issues (54.6 %) land on a different normalized level depending on the axis.**

That is not a rounding difference; it is a different product. Pin the axis or two surfaces built a month apart
will disagree and both will be "correct".

**Decision: read `impacts[]`.** Legacy `severity`/`type` are frozen by Sonar (no longer editable on issues or
rules) and MQR is the forward model. The legacy values are still **carried verbatim** so a consumer that wants
them has them.

### 5.1 F2 — the array is real *today*, and the facet cannot show you

The story inferred from the facet totals summing to the issue count that "every issue happens to carry exactly
one". That inference does not hold:

```
impacts[] length distribution: {1: 1452, 2: 14}
impact PAIRS total: 1480   (vs 1466 issues)
```

**14 issues carry two impacts** — every one a `javascript:S7781` reporting
`[(RELIABILITY, LOW), (MAINTAINABILITY, LOW)]`. The `impactSeverities` facet sums to exactly 1,466 because **it
counts issues, not impact pairs**. The facet is structurally incapable of revealing the array; only a payload read
can. A scalar `severity` field is lossy on live data **now**.

**Decision:** `severity.provider` is an **array** carrying every `{softwareQuality, severity}` pair plus the legacy
axis. `severity.normalized` is the **max** over the impacts, so a multi-impact observation can never normalize
below its worst quality.

### 5.2 The collapse cost, stated

- **5 → 4:** `BLOCKER` (1) and `HIGH` (120) both become `error`. The **single BLOCKER becomes invisible** at
  normalized granularity. Mitigated only because `severity.provider` retains it — any surface wanting "show me the
  blocker" must read the provider array, and § 11's note to 26.1 says so.
- **Raw Roslyn:** 810 `note`, 24 `warning` — **zero loss**, the scale is SARIF's own.
- **Against SpecScribe's 3-level `DiagnosticSeverity`:** would additionally merge `note` and `none`, and has no
  room for SARIF's `none`. This is the second reason § 2 went parallel rather than reusing the enum.

---

## 6. F4 — multi-location is carried, bounded, and source-class dependent

| | Carries secondary locations | Max on one | Total |
|---|---|---|---|
| Sonar (1,466) | **227 (15.5 %)** | **52** | 2,084 |
| Raw Roslyn SARIF (834) | **1 (0.1 %)** | 1 | 1 |

Sonar's `flows[]` is **flows-of-locations** — two levels of nesting, not the flat secondary-location list the
story's R5 described. Each location carries its own `component`, `textRange`, and an optional `msg` (a real
`csharpsquid:S3776` sample carried `"+1"`, `"+2 (incl 1 for nesting)"` — the cognitive-complexity arithmetic, which
is the *entire explanation* of the issue).

**Decision: carried, flattened, and capped.** `relatedLocations[]` is a flat list (SARIF's own
`relatedLocations` shape, so the profile stays SARIF-compatible). Flow grouping is discarded — no consumed surface
needs it and it doubles the nesting. **A cap is required**: one issue with 52 secondary locations would dominate
any per-file surface. When truncated, the payload sets an explicit `relatedLocationsTruncated` count. Silence
here would be the "decision made by accident" the AC warns about.

> **Not measured:** what cap value is right. That is a surface question, and it belongs to Story 26.1's ideation
> round with real data in front of the owner, not to this spike.

---

## 7. AC #2 — attachment, and the amplification nobody has priced

### 7.1 The gate is still there, and it moved

`PlanningCodeImpact.Build` is called at **`SiteGenerator.cs:388`** and **`:774`** — *not* `:357`/`:739` as the
story recorded. A concurrent session moved them; the story told me to grep rather than assume, and it was right.
Both remain gated on `progress?.DeepGit?.Commits is { Count: > 0 }`, falling back to `PlanningCodeImpactData.Empty`.

**In a default run there is no join at all.** Measured on this repo: with `--deep-git` off, **100 %** of 2,300
observations attach to no planning entity.

### 7.2 Measured attachment (`--deep-git` **on**)

Generated with `specscribe generate --deep-git` (737 pages, `errors=0`), then read back out of the real generated
surfaces — `impact-map.html`'s embedded hierarchy data (1,166 nodes) and all 162 story pages.

| | Epic granularity | Story granularity |
|---|---|---|
| Observations attaching to ≥1 entity | **1,765 (76.7 %)** | **1,572 (68.3 %)** |
| Unattached | 535 (23.3 %) | **728 (31.7 %)** |
| Distinct files reachable | 234 | 151 |
| Surfaces carrying the widget | 19 / 27 epic pages | 97 / 162 story pages (59.9 %) |

The run's own honesty line: **"290 of 300 analyzed commits correlated to a story or epic"** — and the deep-git
window is **bounded at 300 commits**, which is itself a silent horizon.

### 7.3 F3 — the fan-out, which is the real finding

The story predicted a finding in `SiteGenerator.cs` "would attach to nearly every story in the project." It does.
But the aggregate is worse than the anecdote:

| Fan-out | Epics | Stories |
|---|---|---|
| Mean **per file** | 4.34 | — |
| Mean **per observation** | **7.33** | **10.02** |
| Median per observation | 6 | 6 |
| Max | **18 of 19 epics** | **64** |
| Attachment **edges** generated | 12,931 | **15,758** |
| Attached observations landing on ≥5 entities | **67.0 %** | — |

Worst offenders: `SiteGenerator.cs` → **18 of 19 epics** / 43 stories; `assets/specscribe.css` → **64 stories**;
`Charts.cs` → 16 epics / 33 stories.

> **1,572 attached observations become 15,758 story-page rows.** A story page whose commits touched
> `specscribe.css` inherits every observation in a 95.7 KB file it changed one line of.

This is the join behaving **exactly as documented** — `PlanningCodeImpact`'s own XML docs call it a "two-tier
best-effort heuristic" whose Tier 2 is "a linear-window approximation … deliberately NOT a parent-hash DAG walk."
It answers *"which story's commits touched this file"* — **authorship history, not ownership**. It was built to
render a treemap of churn, where 10× fan-out is harmless. Findings are per-line assertions, where it is not.

**Story 26.5's AC says to use this miner "as the join — never a second, divergent story↔file mapping." That is
right, and it is not sufficient.** The contract therefore requires **every attachment to be labelled, never
asserted**:

```
attachment: {
  basis: "deep-git-commit-mining" | "unavailable" | "none",
  confidence: "approximate",          // never "exact" for epic/story
  epics: [...], stories: [...],
  entityCount: 18                     // the fan-out, exposed
}
```

`entityCount` exists so a surface can **degrade rather than dump**: an observation attached to 18 epics is
evidence about a hot file, not about any one epic. Bounding rule is a **surface** decision (Story 26.1 / 26.5), but
the *data to make it* must be in the contract or every surface re-derives it differently.

### 7.4 The silent-loss mode, and how the contract answers it

Deep git has already dropped whole surfaces at `errors=0` in this project (memory: *GitMetrics 3s timeout*). Without
a provenance flag, a consumer cannot distinguish:

- **"this file has no planning attachment"** (a real, common answer — 23–32 % even with deep-git on), from
- **"attachment was never computed"** (deep-git off — 100 %), from
- **"attachment was attempted and silently failed"** (the timeout).

These are three different facts and an empty array is the same byte sequence for all three. **`attachment.basis` is
mandatory and non-nullable**, precisely so the degenerate case is *stated*. This is the concrete, unflattering
answer AC #2 asked for.

### 7.5 R7 — the requirement key is **not** carried, and the work graph is **not** the join

`TraceabilityTemplater` is, in its own words, a *"Requirement-to-epic traceability matrix"* — requirement × **epic**.
`PlanningCodeImpact` yields epic and story. So `finding → file → epic → requirement` is **two hops, the second at
epic granularity only** — and it composes on top of a join already amplifying 7.33×.

**Decision: `requirement` is NOT a first-class attachment key.** It is derivable by a consumer that wants it
(epic → requirement is a real edge in `TraceabilityTemplater`), and the ADR says so. Putting it in the schema
would imply an edge that does not exist. AC #1 named it; the honest answer is that it is absent, and this is that
answer.

**The work graph cannot be the join, and by more than the story said.** `WorkNodeKind` is
`{Epic, Story, Deferred, Action, Spec, Retro}` and `WorkEdgeKind` is
`{Contains, StemmedFrom, Resolves, RaisedIn}`. There are **no file nodes** — and, contrary to the story's
description, **no requirement nodes either**. It is a planning↔planning graph. It is not a code→planning join and
cannot become one without a new node kind. Listed, evaluated, rejected.

### 7.6 The unattached route

Unattached is **not** an edge case: **728 observations (31.7 %)** at story granularity with `--deep-git` **on**, and
**2,300 (100 %)** with it off — which is the default. By top-level directory the unattached set is `src` 264,
`tests` 234, `web` 37.

**Destination: Story 26.6's analysis hub.** It is the only surface with no entity precondition. The contract's
obligation is that unattached observations are a **routed population with a named home**, not a residue — and that
`attachment.basis` distinguishes *unattached* from *unattachable*.

---

## 8. AC #1 — the second source class, and what each direction loses

Both mappings were executed on real data by
[`spike/findings/map_to_model.py`](../../spike/findings/map_to_model.py): **1,466** live Sonar issues and **834**
raw Roslyn SARIF results, into one `AnalysisObservation` shape.

### 8.1 Is the second source genuinely independent?

Honest answer: **the acquisition path is fully independent; the rule content overlaps heavily.** The raw SARIF is
the same analyzer family Sonar imports (CA1861 raw 339 vs Sonar 338). What differs — and what makes it a valid
proof — is that **nothing about it passed through Sonar's normalizer**: no `impacts[]`, no `cleanCodeAttribute`,
no `effort`/`debt`, no server-assigned key, a different severity enum, a different location encoding, and rule
metadata stored out-of-line.

And the two sources are **not** subsets of one another in either direction:

- **2 rules appear only in raw SARIF** — `CA1806`, `xUnit2013` — invisible in Sonar.
- **37 `csharpsquid:*` rules appear only in Sonar** — no raw analogue at all.

### 8.2 Losses, Sonar → Observation

| Dropped | On how many | Why |
|---|---|---|
| `effort` / `debt` | 1,466 | No SpecScribe analogue. Deliberate. |
| `assignee` | 1,466 | **Deliberate** — no people scoreboard. |
| `hash` | 1,466 | Sonar's line-content hash; not portable. |
| `key` | 1,466 | Server-assigned and **not stable across re-analysis of a moved line** — carrying it would imply an identity it does not have. |
| `cleanCodeAttribute` / Category | 1,466 | MQR taxonomy with no SARIF or Roslyn analogue; carrying it would make the model Sonar-shaped. |
| `tags[]` | 600 | Folded into the optional `tags` field where present. |
| **`rule.name` / `helpUri`** | **1,466** | **Not in the payload at all** — needs a second call, `api/rules/show?organization=…&key=…`. § 10 prices this. |

### 8.3 Losses, raw Roslyn SARIF → Observation

| Dropped | On how many | Why |
|---|---|---|
| `properties.warningLevel` | 834 | Compiler-internal. |
| rule `executionTime*` telemetry | 834 | Analyzer performance, not a code fact. |
| rule `category` | 834 | Retained as a tag candidate; not a first-class field. |
| `properties.customProperties` | 344 | Rule-specific (e.g. `{"paramName": "anyOf"}`). Genuinely useful and genuinely unmodellable across providers. |
| No analogue at all | 834 | `cleanCodeAttribute`, `impacts[]`, `effort`/`debt`, issue key — the Sonar direction's richest fields simply do not exist here. |

**The asymmetry is the finding.** Sonar → Observation loses *taxonomy*; SARIF → Observation loses *nothing about
severity or location* but arrives with far less metadata. A model designed against Sonar alone would have made
`cleanCodeAttribute` structural and had nothing to put there for half its inputs.

### 8.4 Path normalization — both need it, in opposite directions

```
Sonar raw : IntegerMan_SpecScribe:src/SpecScribe/HtmlTemplater.cs   -> split on ':'
SARIF raw : file:///C:/Dev/SpecScribe/src/SpecScribe/RelatedWork.cs -> re-root + un-percent-encode
```

> **Unrecorded finding:** SARIF's `artifactLocation.uri` is an **absolute `file://` URI carrying the build
> machine's path**. Emitting it into a committed artifact would leak `C:/Dev/SpecScribe` (and on CI, the runner's
> workspace path) into the repository. **`location.path` is normatively repo-relative and forward-slashed**, and
> the ADR states it, because "just carry what the provider gave you" is a real footgun here.

Re-rooting all 834 results produced **0** paths escaping the repo root.

---

## 9. F6 — staleness is a **revision** question, and the timestamp actively lies

```
latest analysis date     : 2026-07-28T01:56:53+0000   <- reads "today"
latest analysis revision : d1722f17…
local working-tree HEAD  : 06b300c…
>>> analysis is 2 commit(s) BEHIND the working tree
```

Story 25.4 AC #2 requires consumers to tell *"when the analysis predates the working tree."* A timestamp cannot
answer it — the timestamp here says "an hour ago" while the analysis describes code that is two commits stale. In
a repo where a sibling session commits during your dev pass (routinely, per CLAUDE.md), this is the normal state.

**Decision: the provenance block is revision-first.**

```
provenance: {
  provider: "sonarcloud",
  analysisRevision: "d1722f17…",   // from api/project_analyses/search
  analysisDate:     "2026-07-28T01:56:53Z",
  workingTreeRevision: "06b300c…", // stamped at emit time
  isStale: true,                   // revision != workingTree, or ancestry unknown
  commitsBehind: 2                 // null when not computable
}
```

`isStale` defaults to **true** when it cannot be computed. A staleness field that fails open would defeat its own
purpose.

> **Not measured:** the raw-SARIF direction has no analysis revision — it *is* the working tree at build time. The
> profile stamps `analysisRevision = workingTreeRevision` and `isStale = false` for build-time providers, which is
> correct but untested against a dirty tree. Flagged for 25.4.

---

## 10. AC #3 — channels, five rows

| | **Digest artifact** (sharded) | **Epic 22 IR field** | **Sonar official MCP** | **SpecScribe MCP** | *(baseline)* stderr JSON-lines |
|---|---|---|---|---|---|
| **Framework-neutral (NFR8)** | ✅ file on disk | ✅ but BMad-shaped keys | ❌ **Sonar's model only** | ✅ | ✅ |
| **BMad-neutral** | ✅ attachment optional | ✅ optional | n/a | ✅ | ✅ |
| **Offline: no network** | ✅ last artifact, `isStale` | ✅ same | ❌ **dead** | ✅ | ✅ |
| **Offline: never analyzed** | ✅ absent → nothing | ✅ field absent | ❌ dead | ✅ | ✅ |
| **New runtime?** | ❌ none | ❌ none | ⚠️ **Docker or JRE** | ⚠️ **server lifecycle** | ❌ none |
| **SpecScribe code** | small emitter | IR schema bump | **zero** | new subsystem + ADR | exists |
| **Fingerprint impact** | ✅ **none** | ❌ **moves it** | ✅ none | ✅ none | ✅ none |
| **Subset-consumable** | ✅ **index + shard** | ⚠️ per-page chunks | ✅ query API | ✅ | ❌ whole-run only |
| **Staleness honest** | ✅ § 9 block | ✅ § 9 block | ⚠️ server-side, live | ✅ | n/a |
| **Credential needed** | ❌ *(if fed from CI artifact)* | ❌ same | ✅ **token required** | depends | ❌ |

### 10.1 The digest, concretely (not a category)

Measured by [`measure_channels.py`](../../spike/findings/measure_channels.py) on all 2,300 observations:

| Shape | Size |
|---|---|
| **Whole digest**, compact JSON | 1,559,177 B (**1.49 MB**) |
| **Index** (`path → count`) | **9,138 B (8.9 KB)** |
| **Per-file shards** | **201 shards**; median **3,691 B**, mean 7,758 B, max 95.7 KB |

**This is what makes the digest win for 25.4.** The use case is *"the files I am about to touch"*, not *"the whole
project"*. An agent reads an **8.9 KB index**, picks the 1–5 paths it cares about, and reads shards at a **median
3.7 KB** — never the 1.49 MB whole. A monolithic digest would be a poor agent payload; a sharded one is a good one.
Concretely: **~20 KB** for a typical dev-story pass touching three files, against 1.49 MB.

- **Where it lands:** `.specscribe/analysis/` — the ADR 0014 settings **folder**, which already exists for exactly
  "future per-repository state".
- **Gitignored**, verified with `git check-ignore` (not assumed — Story 25.5's AC makes the same demand).
- **Not** in the output directory, so it cannot touch the fingerprint.

### 10.2 The IR row, and why it is Epic 26's answer and not 25.4's

The IR is `spa/`, promoted in place (ADR 0016), `SchemaVersion = 1`, with a per-page `ContentHash` and chunked
pages. **The IR is generated output**: anything added changes generated bytes. Story 25.4 AC #2 and Epic 25's
charter both require the fingerprint **unmoved**, so **the IR field is structurally unavailable to 25.4**.

For **Epic 26** it is the natural home, and 26.4's AC already says the fingerprint move is expected and must be
"re-baselined with a stability check across two runs." A findings payload attached per page would ride
`SpaDelivery`'s existing chunking rather than sitting in one blob.

**Caveat the ADR must carry:** Story 23.4 is blocked with **857/1046** pages' IR still produced by the code it
retires. Epic 26 designing against the IR shape must say which side of that migration it targets.

### 10.3 R8 — the MCP row is two rows, and the free one is genuinely tempting

**Confirmed at spike time** (fetched, not trusted): SonarSource ships an official MCP server that supports
**both SonarQube Server and Cloud**, is distributed as `sonarsource/sonarqube-mcp` (Docker) or a JAR, exposes
issues / hotspots / quality gates / measures / duplications / SCM, documents **Claude Code** explicitly
(`claude mcp add sonarqube …`), and **requires a token**.

| | SpecScribe code | What the agent receives |
|---|---|---|
| **Adopt Sonar's server** | **zero** | **Sonar's** model — Sonar-shaped, Sonar-only, service must be reachable |
| **SpecScribe emits MCP** | new runtime + lifecycle (**no MCP dependency exists in this repo today**) | this contract's source-agnostic model |

**Said out loud, as the AC demands:** adopting Sonar's server is nearly free and **forfeits the exact property this
spike exists to establish**. It cannot see the 834 raw Roslyn results (2 rules Sonar never imports), it cannot
attach to epics or stories, it dies offline, and it needs a credential.

**Recommendation: adopt it anyway, as a complement, and do not confuse it with the contract.** For a maintainer
interactively asking "what does Sonar think of this file?", it is strictly better than anything 25.4 would build,
at zero cost. It is not a substitute for a source-agnostic artifact that works offline and attaches to planning
entities. Both, with the roles named — that is the "adopt the free thing now, keep the contract for the surfaces"
answer the story anticipated, and it survives the evidence.

**A SpecScribe-emitted MCP surface is deferred.** It needs a new runtime, and per the story's own constraint that
needs **its own ADR** — named here, not slipped in.

### 10.4 What Story 25.4 defers

Ingestion posture and credential design (**26.2**), any portal surface (**26.1**, **26.4–26.6**), the IR field
(**Epic 26**), a SpecScribe-emitted MCP surface (**own ADR**), and provider pluggability (**26.7**).

### 10.5 The credential-ordering risk, named

25.4 runs **before** 26.2, which owns credential design. Three ways to fetch findings:

1. **Read a CI-produced artifact already on disk** — no credential. **Recommended for 25.4.**
2. Call the Sonar API from the dev machine — needs a token → needs a slice of 26.2 early.
3. Adopt Sonar's MCP server — needs a token, but held in **MCP client config**, outside SpecScribe entirely, so
   SpecScribe still writes no token value anywhere and 25.4 AC #1 is satisfied.

**Recommendation: 25.4 takes path 1** and treats path 3 as the interactive complement. This keeps 25.4 entirely
inside its own AC ("writes no token value anywhere") and leaves 26.2's design genuinely open. If 25.4 finds path 1
impractical, that is a **constraint on 26.2**, to be raised — not a licence to decide credentials inside an
implementation story.

### 10.6 The rule-metadata round trip — a real 25.4 cost

`rule.name` and `helpUri` are absent from `api/issues/search`. Populating them needs
`api/rules/show?organization=…&key=…` per **distinct rule** — **76** distinct rules across 1,466 issues, so 76
calls, cacheable indefinitely (rule metadata is near-static). Small, but real, and it must not be discovered
mid-implementation. Raw SARIF has this **for free** — the rule catalogue ships in the log.

---

## 11. Handoff

### Story 25.4 — *Agent-consumable findings channel*
- **Channel: sharded digest** in `.specscribe/analysis/` — `index.json` (8.9 KB) + per-file shards (median 3.7 KB).
  Gitignored, verified with `git check-ignore`.
- **Source: a CI-produced artifact on disk** (§ 10.5 path 1). No token, so AC #1 is satisfied by construction.
- **Fingerprint-safe by construction** — nothing under the output directory. § 10.2 explains why the IR is unavailable.
- **Staleness: § 9's revision-first block.** `isStale` fails **closed** (defaults true).
- **Budget the 76-call rule-metadata fetch** (§ 10.6). Cache it.
- **Complement, don't duplicate:** recommend Sonar's official MCP server in `docs/SonarCloudSetup.md` for
  interactive use, naming what it cannot do (§ 10.3).
- **Reproduce the evidence:** `spike/findings/*.py` run end-to-end today; the SARIF build needs `-t:Rebuild` and
  one project at a time (§ 1.1).

### Story 26.2 — *Ingestion posture and credentials* — **consumes this contract, does not redefine it**
- The model, severity axis, and attachment vocabulary are **settled**; 26.2 supplies *how bytes arrive*.
- **Amendment surface:** this contract constrains 26.2 in exactly one way — the § 9 provenance block requires an
  **analysis revision**, not just a timestamp. Any posture that cannot supply one must say so and mark `isStale`
  true. **If 26.2 needs to amend the contract, ADR 0023 must be amended, not worked around.**
- Path 3 (Sonar MCP) is worth pricing as a **posture**, not just a channel — the credential lives in MCP client
  config, outside SpecScribe.
- AD-4 boundary is upheld here: an absent provider yields **nothing**, never a failed run.

### Story 26.3 — *Configuration parity*
- One resolved setting: the digest location. Everything else is provider config (26.2's).
- Follow the Story 5.2 three-way provenance pattern via `SettingsResolver`.

### Story 26.4 — *Code pages and code map*
- `location.path` is **repo-relative, forward-slashed** — joins the `CodeFileTemplater` / `FileInsight` seam directly.
- `location.startLine` feeds the existing `#L{n}` anchor.
- **Cap `relatedLocations` per surface** (§ 6) — one issue carries **52**.
- Directory aggregation: sum over the file scope; no directory attachment exists in the model.
- Fingerprint **will** move here. Expected — re-baseline with the two-run stability check (CLAUDE.md).

### Story 26.5 — *Planning entities* — ⚠ **read § 7.3 before designing**
- **The join amplifies 10.02×.** 1,572 attached observations → **15,758** story-page rows. `specscribe.css` attaches
  to **64 stories**; `SiteGenerator.cs` to **18 of 19 epics**.
- Using `PlanningCodeImpact` as the join is **correct and insufficient**. `attachment.entityCount` is in the
  contract so this surface can bound; **the bounding rule itself is 26.5's to design and the owner's to approve.**
- **`requirement` is not an attachment key** (§ 7.5). Requirement pages must derive via epic and say so, or omit.
- **Three empty states, not one** — `attachment.basis` distinguishes them (§ 7.4): *no findings* / *unattached* /
  *attachment unavailable* (deep-git off — **the default**, 100 %).

### Story 26.6 — *Hub and dashboard signal*
- The hub is the **named destination** for unattached observations: **728 (31.7 %)** with deep-git on, **2,300
  (100 %)** with it off.
- The hub is the **only** findings surface with no entity precondition, so it must work with `--deep-git` off.
- Dashboard signal reads `severity.normalized` for the count and `severity.provider` for "1 BLOCKER" (§ 5.2).

### Note to **Story 26.1** (ideation) — vocabulary only, no layout
- **Four normalized levels, with these mandatory text labels:** `Error` / `Warning` / `Note` / `None`. The label
  ships **in the payload**, so UX-DR17 is satisfied by the contract, not by a rendering convention.
- Do **not** invent a second severity vocabulary. If four levels are too many for a surface, collapse in the
  surface and say so.
- **Counts to design against:** normalized `error` 121 / `warning` 960 / `note` 385, and **1 BLOCKER** that is
  invisible unless the surface reads `severity.provider`. Consider surfacing it.
- `relatedLocations` needs a density decision (max **52**) — real data, owner's call.
- The word is **"Observations"**, never "Findings", on story pages that already render `<h3>Review Findings</h3>`.

### Note to **Story 26.7** (provider survey)
The contract **does generalize** — proven, not asserted: two providers with disjoint serializations, disjoint
severity scales, and partially disjoint rule sets both mapped into it (§ 8). The seam is a **normalizer per
provider**: split a path, pick a severity axis, flatten related locations, stamp provenance. What does *not*
generalize is provider-specific taxonomy (`cleanCodeAttribute`, `effort`) — deliberately dropped rather than made
structural. **Recommendation: pluggable normalizers, one shared `AnalysisObservation`.**

### Note to **Epic 27** (FR42, coverage) — **outside this contract, and the reason holds**
Coverage is a **per-file metric**, not an observation: it is a *ratio over every line*, has no rule identity, no
message, no severity, and no location — the five fields that make an observation an observation. Forcing it in
would mean one synthetic observation per file carrying a number in `message`, which is a metric wearing a costume.

**Challenged on evidence, and the separation survives** — with one exception worth naming: an *uncovered-lines*
view (a **range**, with a location and no rule) could ride `relatedLocations`. That is a genuine edge, and Epic 27
should decide it deliberately rather than inherit it. `ArtifactCoverage` already owns the word "coverage"; Epic 27
faces the same naming collision this spike faced in § 3, and should resolve it the same way — deliberately.

---

## 12. AC #4 + Task 8 — the ADR, and proof this shipped nothing

**ADR: [0023 — An Agent-Facing Analysis Observation Contract](../../docs/adrs/0023-agent-facing-analysis-observation-contract.md).**

> **Numbering corrected against the story.** The story predicted **0021**. Wrong by two: `0020`, `0021`, and `0022`
> all landed on disk between authoring (07-27) and this pass (07-28). **`0019` remains claimed-but-unwritten by
> BOTH Story 18.3 and Story 22.3** — verified by grep, not assumed. **0023 is the first uncontested slot.**
> Listed in `docs/adrs/README.md` in the established one-line-with-consequences style.

**Ratification: ✅ the owner ratified ADR 0023 during this dev pass (2026-07-28).** It reads **Accepted**, as does
its `docs/adrs/README.md` index entry, satisfying AC #4's requirement that six downstream stories not be asked to
bind to a Proposed record. **0023 is the first Accepted ADR since 0015** — 0016–0018 and 0020–0022 all remain
Proposed, which is worth raising at the Epic 25 retrospective.

### Zero-product-code proof

| Check | Result |
|---|---|
| `src/`, `tests/`, `web/`, `extension/` edits **by this story** | **none** — a concurrent session's Story 22.4 work is in the tree and was left untouched (§ 13.2) |
| `GoldenContentFingerprint` | **unmoved**; its test passes standalone and in the suite (§ 13.1) |
| Full suite | **2,658 passed / 0 failed / 3 skipped** (§ 13.1) |
| `spike/findings/` referenced by any project file | **no** — `SpecScribe.slnx` has 2 projects, neither a spike |
| Generated site byte-identical with/without `spike/findings/` | **yes — tested, 0 differences** (§ 13.3) |

---

## 13. Verification

### 13.1 Full suite and fingerprint

| Check | Result |
|---|---|
| Full suite, mid-pass | **2,658 passed / 0 failed / 3 skipped** (2,661 total, 1 m 55 s) |
| Full suite, re-run at end of pass | **2,674 passed / 0 failed / 3 skipped** (2,677 total) |
| `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` | **passed**, run standalone as well as in the suite |
| `GoldenContentFingerprint` constant | **unmoved** — not edited, and its test is green |

The suite was run **twice** because the tree moved substantially between them (§ 13.2). The +16 tests are the
concurrent session's, not this story's — this story adds no tests, because a contract decision has none to add.

**F5 was re-verified after the concurrent session touched both of its source files.** `DiagnosticsTemplater.cs`
and `Commands.cs` both entered the sibling's diff late in the pass. Re-checked: `DiagnosticSeverity` is still
`{ Error, Warning, Info }` and `SerializeDiagnostics` still emits
`notice.Severity == DiagnosticSeverity.Error ? "error" : "warning"`. The sibling's changes do not touch severity,
so **F5 stands**. (Its line moved to `:280`; this report and ADR 0023 cite it **by symbol**, per
CLAUDE.md § Decision records.)

### 13.2 No product code — and a concurrent session's work, left alone

`git status` shows `src/`, `tests/`, and `web/` modifications. **None are attributable to this story.** They are a
concurrent session's in-flight Story 22.4 work — `SpaDelivery.cs`, `SiteGeneratorSpaTests.cs`, `web/ir/adapter.ts`,
`web/ir/types.ts`, `web/test/region-split.test.ts`, and `Commands.cs`. Per CLAUDE.md they were **left untouched**;
no `git reset --hard`, `git checkout --`, or `git clean` was run.

> **The tree moved substantially mid-pass.** At the start the sibling's diff was 7 files; by the end it was **~38**,
> having grown to include `Charts.cs`, `ConsoleUi.cs`, `DiagnosticsTemplater.cs`, `IdeasTemplater.cs`,
> `SiteGenerator.cs`, most of `web/`, and a new `tests/SpecScribe.Tests/ConsoleUiTests.cs`. `Commands.cs` was
> **not** in `git status` when I first checked and **was** in `git diff --stat` seconds later. That is the
> shared-`main` condition behaving exactly as CLAUDE.md describes, observed live — and it is why F5 was
> re-verified against its own source files at the end of the pass rather than trusted from the beginning (§ 13.1).

This story's own footprint is exactly four paths: `25-3-spike-report.md` (new),
`docs/adrs/0023-…md` (new), `docs/adrs/README.md` (one appended line), `sprint-status.yaml` (status), plus the
disposable `spike/findings/`.

### 13.3 `spike/findings/` is provably inert

`SpecScribe.slnx` contains **2** projects (`src/SpecScribe`, `tests/SpecScribe.Tests`); no spike project is
referenced. But that alone is not the guarantee — `spike/` **is** rendered as code pages, and `.py` files **do**
render (`code/_bmad/scripts/memlog.py.html` exists), so this needed testing rather than assuming.

**Method.** The naive whole-site hash is useless here: two identical consecutive runs differ, because every page
carries a per-run footer stamp (`Generated using SpecScribe on Jul 28, 2026 at 10:48 UTC-04:00`) — confirmed by
diffing two runs, and the reason the golden test is named `…IsStableAfterNormalizingVolatileTokens`. Normalizing
that one token gives a stable comparison:

| Run | Pages | Files | Normalized result |
|---|---|---|---|
| Baseline run 1 (no `--deep-git`) | 436 | 1,102 | — |
| Baseline run 2, identical | 436 | 1,102 | **0 differences** — baseline is stable |
| `spike/findings/` moved away | 436 | 1,102 | **0 differences** |

> **`spike/findings/` produces zero pages, zero diagnostics, and a byte-identical site.** The
> `spike/README.md` quarantine guarantee holds, tested rather than asserted.

The only generated-output change from this story is its **two durable deliverables** rendering as portal pages
(`adrs/0023-….html`, `implementation-artifacts/25-3-spike-report.html`) — expected, and listed in the story's own
Project Structure Notes. The spike report also raises one `Skipped` diagnostic for the well-known story-artifact
prefix collision, identical to the six sibling spike reports (`20-4`, `22-1`, `23-1`, `23-5`, `10-1`, `20-6`)
that already do so.

### 13.4 F7 — the deep-git silent loss, reproduced live

While establishing the baseline, `generate --deep-git` was run **eight** times. It produced **739 pages** on some
runs and **436** on others — the same 436 as a run with **no `--deep-git` at all** — with **`errors=0` every
time**. All ~304 `commit/*.html` pages and the whole deep-git surface simply vanished.

This is the documented `GitMetrics` timeout (memory: *GitMetrics 3s timeout — silent deep-git loss*), reproducing
on demand on this machine, today. It was cited in § 7.4 from project memory; it is now **first-hand evidence**.

**It strengthens Decision 5 of the ADR rather than merely illustrating it.** The failure is not rare, not
theoretical, and not announced. On a machine where it fires, a consumer reading attachment without
`attachment.basis` would see "this observation attaches to no story" — indistinguishable from the truth — on
**100 %** of observations, on a run that reported success. Any surface that treats an empty attachment array as
"no planning relationship" is wrong roughly half the time on this repo, today.

> **Corollary for Story 25.4 and 26.5:** attachment must be recomputed-or-declared per run, never cached across
> runs without its `basis`. A digest written by a lucky run and read after an unlucky one would be silently
> inconsistent with the portal beside it.

---

## 14. What was NOT measured

Named as unmeasured rather than half-measured, per the story's timebox discipline:

1. **The `relatedLocations` cap value** — a surface question with real data; Story 26.1's, with the owner.
2. **The attachment bounding rule** — § 7.3 supplies the data and the problem; the rule is 26.5's design decision.
3. **Raw-SARIF staleness against a dirty working tree** (§ 9) — the build-time provider's `isStale = false` is
   correct in principle and untested in practice. Flagged for 25.4.
4. **Digest emission cost** — sizes are measured, wall-clock to produce one is not. 25.4 owns it.
5. **Non-.NET providers** — ESLint, Bandit, and the like emit SARIF, so the profile *should* hold, but only two
   providers were proven. The § 11 note to 26.7 says "proven on two", not "proven general".
6. **Sonar MCP server behavior** — its documentation was read; **the server was not run**. § 10.3's tool-surface
   claims are documentation-grade, not measured.
7. **A private-repository posture** — this project is public; no token was needed for any call here. 26.2's.
