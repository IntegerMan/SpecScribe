# analysis-digest — the agent-facing findings channel

Fetches this repository's current SonarCloud findings and writes them to `.specscribe/analysis/` as
[ADR 0023](../../docs/adrs/0023-agent-facing-analysis-observation-contract.md) `AnalysisObservation`
records, sharded one file per source file. Story **25.4**.

```sh
node tools/analysis-digest/index.mjs
```

The consumption rules live in [`CLAUDE.md`](../../CLAUDE.md) § Analysis observations, which is auto-loaded
into every agent session. The operator-facing entry is
[`docs/SonarCloudSetup.md`](../../docs/SonarCloudSetup.md) § The agent-facing digest.

## What it is not

- **Not product code.** Nothing under `src/SpecScribe` imports it, nothing runs it during
  `specscribe generate`, and it writes nothing into `SpecScribeOutput/`. The golden fingerprint cannot move
  because of this tool. A networked analysis path *inside* the product is Epic 26's subject.
- **Not credentialed.** `IntegerMan_SpecScribe` is a public free-tier project and every endpoint used here
  answers anonymously — the same credential-free method `docs/SonarCloudSetup.md` § Triaging findings has
  driven since Story 25.2. There is no token knob to set, no environment variable read, and nothing written.
- **Not automatic.** Opt-in by invocation only: no hook, no watcher, no `postinstall`, no MSBuild target.
- **Not a second source.** SonarCloud only. 819 of the issues already *are* Roslyn results, imported as
  `external_roslyn:*`; adding raw SARIF would duplicate them to gain a handful. The mapper keeps a `provider`
  seam so a second source is additive later.

Zero runtime dependencies — `fetch` and `node:child_process` on Node **24.11.1** (`web/.nvmrc`; no second
Node pin is introduced). `tools/**` is in the workflow's `sonar.exclusions` list, so this emitter does not
appear in the findings it produces.

## Layout

```
.specscribe/analysis/
  index.json                        schema + provenance + totals + path -> {count, byLevel, shard}
  files/<repo-relative-path>.json   one shard per source file, path DERIVABLE without the index
  unlocated.json                    project-level observations with no file (a routed population)
  .rules-cache.json                 rule name/helpUri, keyed by rule key; versioned
```

Every shard carries the **full** provenance block. That is deliberate: the layout exists so an agent can
construct a shard path from a file it is about to touch and read it *without* the index, and a shard that
could not report its own staleness would lie by omission.

`byLevel` maps below the top level **omit zero counts** — an absent level means zero. `totals.byLevel` keeps
all four keys, because there a zero is itself informative.

Writes are **atomic**: the digest is built in `.specscribe/analysis.tmp-<pid>/` and swapped into place, so an
interrupted run never leaves a half-written digest an agent would read as authoritative.

## Measurements (2026-07-28, analysis revision `755bd7a`)

| | |
|---|---|
| Observations | **1,488** unresolved · 120 error / 979 warning / 389 note / 0 none |
| Distinct rules | **86** — counted from the fetched issues, **not** the `rules` facet (the facet caps at 100 and will silently under-report) |
| Files / shards | **201** · 0 unlocated |
| `index.json` | **31,399 B** |
| Shard bytes | p25 2,455 · **median 4,294** · p75 8,835 · p90 17,485 · max 101,668 (`SiteGenerator.cs`, 88 obs) |
| One monolithic digest, for comparison | **1,407,925 B (1.34 MB)** |
| A median three-file pass | **12,935 B** — 0.9 % of the monolith |
| A three-hotspot pass (`StatusStyles`+`Charts`+`EpicsParser`) | **146,784 B** — 10.4 % of the monolith |

The Story 25.3 spike estimated an ~8.9 KB index and a 3,691 B median shard. Both are **larger** here, and the
reasons are structural rather than sloppy: the index carries an explicit `shard` field per entry (~9 KB of
deliberate redundancy so a path needing escaping never has to be guessed), and every observation inlines its
rule name and help URI and carries the mandatory `attachment` block. Re-measure rather than quoting these —
the issue count moves by roughly +50/day.

## Contract notes worth not re-litigating

- **`severity.normalized` is derived from `impacts[]` (MQR), taking the max — never the legacy axis.** The two
  axes disagree on 54.6 % of this repo's issues. `severity.provider` is an **array** carrying every MQR pair
  plus the legacy `{severity, type}` verbatim; 14 live issues carry two impacts, so a scalar field is lossy
  today, not hypothetically.
- **`severity.label` ships in the payload.** UX-DR17 is satisfied by the contract, not by a renderer.
- **The BLOCKER is invisible at normalized granularity.** Sonar's five levels collapse into SARIF's four, so
  `BLOCKER` and `HIGH` both become `error`. That is the price of an externally-specified scale, paid
  knowingly; the distinction survives in `severity.provider`.
- **`attachment.basis` is `"unavailable"` on every record and the join is not computed.** ADR 0023 Decision 5
  mandates the block, not the join. Computing it needs `generate --deep-git` and its ~10× fan-out bounding
  rule is Story 26.5's design decision.
- **⚠ Sonar returns `impacts[]` in non-deterministic order, and the emitter sorts it.** Measured 2026-07-28:
  the same issue came back as `[MAINTAINABILITY, RELIABILITY]` on one fetch and `[RELIABILITY,
  MAINTAINABILITY]` on the next, flipping 7 shards between two states on otherwise identical input. Sorting is
  lossless (the order of a set of impact pairs carries no meaning) and `severity.normalized` was already
  order-independent because it is a max. **This is a live warning for Story 26.4**, which puts this shape into
  the Epic 22 IR — and the IR *is* covered by the golden fingerprint, so an unsorted array would make the
  fingerprint flap at random with no source change. Verified: six consecutive runs now produce a
  byte-identical digest. `relatedLocations` is deliberately **not** sorted — a flow is an ordered sequence.
- **Sonar's paging is stable and lossless** at this volume — 3 pages, 1,488 distinct keys, 0 duplicates,
  identical order across repeated fetches, with and without an explicit `s=FILE_LINE` sort. Checked rather
  than assumed, because unstable paging silently drops records across page boundaries.
- **`relatedLocations` is uncapped here.** Capping is a surface concern (Story 26.4), and any cap must emit an
  explicit truncation count — silent truncation is forbidden.
- **Sonar's `p × ps ≤ 10000` ceiling is asserted, not assumed**, so a future volume increase fails loudly
  instead of silently truncating the digest.
- **`api/rules/show` has no `helpUri` field** (verified: 24 keys, none a URL) and **requires `organization`**.
  `helpUri` is the rule's permalink in this organization — verified 200, and it resolves for every rule repo
  present (`csharpsquid`, `external_roslyn`, `javascript`, `typescript`, `css`, `Web`, `jssecurity`). A
  `rules.sonarsource.com/<lang>/RSPEC-<n>` guess was rejected: unverifiable from here, and it does not cover
  the `CS*` / `SYSLIB*` / `xUnit*` keys at all.
- **Dropped deliberately:** `assignee` (no people scoreboard), the Sonar issue `key` (server-assigned, not
  stable across re-analysis of a moved line), `hash`, `effort`/`debt`, `cleanCodeAttribute` (Sonar-only
  taxonomy — carrying it would make the model Sonar-shaped).
