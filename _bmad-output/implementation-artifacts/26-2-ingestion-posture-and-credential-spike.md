---
baseline_commit: e864133 # HEAD at authoring time (2026-07-30). 26.1's baseline was 8a2fb83; its dev pass ran at
                         # 630ae25. Several commits have landed since — re-verify every symbol, never a line number.
epic: 26
frs: [FR41]
nfrs: [NFR12, NFR8, NFR3] # epics.md numbering. The AC's "NFR-3" is the PRD's — a DIFFERENT list. See ⛔ R7.
ux_drs: [] # no visual surface. This story ships no rendered pixel; the visual round was 26.1's and it is DONE.
depends_on: [25-3] # ADR 0023 (Accepted) — consumed, not redefined. 26.1 is complete and explicitly unblocks you.
informs: [26-1] # 26.1 § 8.1 confirms nothing in its record changes on either posture. Do not re-run that round.
blocks: [26-3, 26-4, 26-5, 26-6] # every Epic 26 implementation story. 26.7 is independent.
adrs: [0023, 0014, 0022, 0016, 0003] # the contract · the settings FOLDER · Node at generate time · the IR · settings
decides: docs/adrs/00NN-<slug>.md # NEW ADR — this spike DECIDES and must RATIFY. `docs/adrs/` ends at 0033 on disk
                                  # and 0019 is claimed-but-unwritten, so 0034 is the first uncontested slot.
                                  # ⚠️ VERIFY BY DIRECTORY LISTING at authoring time — the numbers move daily.
ships_product_code: false # THROWAWAY spike. No `src/`, `tests/`, `web/`, `extension/`.
                          # `GoldenContentFingerprint` MUST NOT move — and must not be MEASURED. See Task 8.
timebox: ~2 days
deliverables:
  - "_bmad-output/implementation-artifacts/26-2-spike-report.md"
  - "docs/adrs/00NN-<slug>.md (RATIFIED by the owner, not merely drafted — AC #4)"
  - "_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md (ONLY if AC #2 concludes an amendment is required)"
  - "spike/analysis-ingestion/** (OPTIONAL disposable evidence; quarantined per spike/README.md)"
touches:
  - "_bmad-output/implementation-artifacts/26-2-ingestion-posture-and-credential-spike.md"
  - "_bmad-output/implementation-artifacts/26-2-spike-report.md" # NEW — the deliverable
  - "docs/adrs/00NN-<slug>.md" # NEW — the ratified ADR
  - "docs/adrs/README.md" # the ADR index carries an entry per record
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/planning-artifacts/epics.md" # ONLY if the posture changes 26.3–26.6 scope (see Task 7)
# NOT src/**, NOT tests/**, NOT extension/src/**, NOT web/**, NOT tools/**, NOT .github/workflows/**
---

# Story 26.2: SPIKE — Ingestion Posture, Credential Design, and the NFR-3 Local-First Question

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer about to give SpecScribe its first outbound network capability,
I want the ingestion posture and credential design decided on evidence with an ADR behind it,
So that the local-first question is answered once, in the open, rather than implied by whichever implementation story happens to land first.

|  |  |
|---|---|
| **This spike does** | Decide **how bytes arrive**. Price the three postures at true cost. Answer the PRD crossing plainly. Design credential handling. Author a **ratified** ADR. Hand off to 26.3–26.6, 26.7, 17.2, and the Epic 22 IR. |
| **This spike does NOT** | Ship production code. Define a findings model (ADR 0023 is **Accepted** — consume it). Design a portal surface (26.1 is **done**; 26.4–26.6 implement). Add a CLI flag or a settings field (26.3). Touch `src/**`, `tests/**`, `web/**`, `extension/**`, `tools/**`, or `.github/workflows/**`. |

**Discipline:** decision-first, timeboxed, throwaway — same as Stories 6.3, 6.6, 20.1, 20.4, 22.1, 23.1, 24.6, 25.3.
Suggested timebox **2 days**. **If one axis eats the box, finish that axis and report the rest as *unmeasured* rather
than half-measuring all of them.** 25.3 § 14 is the model: it named seven things it did not measure, and item 7 —
*"a private-repository posture … 26.2's"* — is now yours precisely because it was named honestly instead of guessed.

**No visual-intent elicitation is required for this story, and that is a decision, not an omission.** CLAUDE.md's
create-story convention demands 2–3 named design directions for any *visual surface*. This story renders nothing:
its deliverables are a report and an ADR. The visual round for all of Epic 26 was Story 26.1 and it is complete —
and 26.1 § 8.1 states outright that **no selection in that record changes on either posture**, so you are not
blocked on it and must not re-open it.

---

## ⛔ Read first — fourteen reconciliations against shipped code and live evidence

Each one changes what you would otherwise measure, design, or conclude. **Every symbol below was verified at
`e864133` on 2026-07-30 by name.** Line numbers drift daily on shared main (CLAUDE.md § Concurrent work) — confirm
by symbol before quoting one.

### R1 — SpecScribe's product code makes **zero** network calls today. The "first outbound capability" framing is literally true.

Verified by grep across `src/`: there is **no `HttpClient`, no `System.Net.Http`, no `WebClient`, no `fetch`** in any
shipped path. The only `System.Net` uses are `WebUtility.HtmlDecode`/`HtmlEncode` (`src/SpecScribe/PathUtil.cs`,
`src/SpecScribe/SpaDelivery.cs`, `src/SpecScribe/SiteGenerator.cs`). Every `https://` string under `src/` is a URL
being *composed for rendering* (`CodeSourceUrlResolver`, `AboutSddTemplater`, `Mermaid`), never fetched.

**Consequence for the ADR:** it is not authorizing "one more call". It is authorizing the **category**, and the ADR
must say so — including what the boundary is, so a future story cannot cite it as precedent for an unrelated call.
Epics.md Story 17.2 AC #2 already binds: *"the NFR3 re-confirmation accounts for the outbound network path Story
26.2's ADR authorized."* Something downstream will read your ADR as the authorization. Write it that way.

### R2 — A networked, credential-free ingestion path **already exists, is measured, and runs today** — just outside the product.

`tools/analysis-digest/index.mjs` (Story 25.4, ~709 lines, zero runtime dependencies) already implements the entire
SonarCloud web-API posture. **This is the single most valuable piece of evidence available to you and it is
already written.** It has solved, in production, every problem AC #1 asks you to evaluate:

| Problem AC #1 names | What the shipped emitter already does |
|---|---|
| Credential requirement | **None.** `IntegerMan_SpecScribe` is public free-tier; every endpoint answers anonymously. "There is deliberately no token knob to override." |
| Rate limits / politeness | `RULE_FETCH_CONCURRENCY = 4`, `FETCH_TIMEOUT_MS = 30000` |
| Paging | Asserts Sonar's `p × ps ≤ 10000` ceiling and **fails loudly** rather than truncating; verified stable and lossless — 3 pages, 1,488 distinct keys, 0 duplicates, identical order across repeated fetches |
| Data available | `api/issues/search?resolved=false` — `resolved=false` is **mandatory** (unfiltered returned 1,598 vs 1,420, triaging ~180 issues that no longer exist) |
| Freshness | `api/project_analyses/search` supplies `analysisRevision` — the only honest staleness anchor (ADR 0023 Decision 6) |
| The rule round trip | `api/rules/show` per **distinct** rule (86 of them), `organization` **required**, no `helpUri` field (synthesized as the org permalink), cached under a **versioned** cache key |
| Determinism | Sorts `impacts[]` (Sonar returns it in non-deterministic order); six consecutive runs produce a byte-identical digest |
| Failure atomicity | Builds in `.specscribe/analysis.tmp-<pid>/` and swaps, so an interrupted run never leaves a half-written digest |

**Do not re-derive any of this.** Read `tools/analysis-digest/index.mjs` and `tools/analysis-digest/README.md`,
cite them, and spend the timebox on what they *cannot* tell you (R3, R4, R5).

### R3 — **The one thing 25.4's evidence cannot establish is the one thing AC #3 asks for.**

25.3 § 14 item 7, verbatim: *"A private-repository posture — this project is public; no token was needed for any
call here. 26.2's."* Every measurement this project has is **anonymous**. You therefore cannot validate a
credentialed path against this repository, and pretending otherwise would be exactly the half-measurement the
timebox discipline forbids.

Honest options, in preference order:

1. **Read SonarCloud's documented authentication and state it as documentation-grade, not measured** — the same
   standard 25.3 § 10.3 applied to the Sonar MCP server (*"its documentation was read; the server was not run"*).
2. Test against a private project **if one is available to you** — say which, and whether it was free-tier.
3. Name it unmeasured, with what would have to be true to measure it.

**What you must not do is design a token flow and imply it was proven.** Say which tier of evidence each claim sits on.

### R4 — The on-disk posture has **no producer today**. That is its true cost, and AC #1 demands the true cost.

- `.github/workflows/build-test-analyze.yml` runs `dotnet-sonarscanner begin` / `end` and uploads **no** SARIF, no
  issues export, no analysis artifact. Verified by reading the workflow.
- `.sonarqube/` is gitignored (`.gitignore:502`). The scanner's intermediate output is local and transient.
- SonarCloud is a hosted service: unlike SonarQube Server it has no on-disk report a user simply *has*. The
  "on-disk export" candidate therefore means one of: **(a)** a CI change to publish an artifact (out of this
  story's scope but a real cost to name), **(b)** the user runs a scanner locally, or **(c)** a *different*
  producer entirely — raw Roslyn SARIF from `dotnet build` with `ErrorLog`.
- Path **(c)** has live evidence: `spike/findings/roslyn-specscribe.sarif` and `roslyn-tests.sarif` exist from
  Story 25.3. Its measured cost: needs `-t:Rebuild`, one project at a time (25.3 § 1.1), is **2.6× the bytes per
  result**, and results are **not self-describing** (`ruleIndex` into an out-of-line catalogue) — ADR 0023
  Decision 3.

### R5 — AC #1's *"can a user without a SonarCloud account get any value at all?"* has a measured answer waiting. Cite it; do not re-derive it.

Story 26.1 § 1.2(c) measured the engine split across 1,534 observations:

| Engine | Observations | Note |
|---|---:|---|
| `external_roslyn` | **859 (56 %)** | Roslyn analyzer output, imported by SonarCloud as an external engine |
| `csharpsquid` | 609 | SonarSource's own C# rules |
| `javascript` / `typescript` / `css` / `jssecurity` / `Web` | 66 | |

**56 % of this repository's payload is analyzer output a local build can produce with no account at all.** That is
the strongest available argument for a "both" posture, and it is also the fact that reframed Story 26.7
(26.1 § 8.6). Two counterweights you must state rather than skip:

- 25.4's README: adding raw SARIF alongside Sonar *"would duplicate them to gain a handful"* — the two sources
  overlap almost entirely on this repo.
- The degradation figure: a **non-.NET** project loses those 859 and sees roughly **44 %** of this repo's density
  (26.1 § 8.6). That is the concrete NFR8 cost of a Roslyn-only local path.

### R6 — The contract constrains you in exactly **one** way, and it is a hard gate.

ADR 0023 § Consequences, verbatim: *"**Story 26.2 in particular consumes it and must not define a second** — if
26.2's ingestion posture cannot supply an analysis revision (Decision 6), it must **amend this ADR**, not work
around it."*

- **Revision-first provenance is non-negotiable.** `provider` · `analysisRevision` · `analysisDate` ·
  `workingTreeRevision` · `isStale` · `commitsBehind`. `isStale` **fails closed** — defaults `true` when it cannot
  be computed.
- A **build-time** provider (raw SARIF) sets `analysisRevision = workingTreeRevision`, `isStale = false`.
  ⚠️ **25.3 § 9 flags this as untested against a dirty working tree** — which, per CLAUDE.md § Concurrent work,
  is the *normal* state in this repository. If your posture includes a build-time source, this is an axis to
  measure or to name unmeasured. It is not a detail.
- A timestamp cannot substitute. Measured at 25.3 authoring time: the latest analysis timestamp read "an hour ago"
  while its revision was **two commits behind** `HEAD`.

### R7 — The NFR-3 question carries **two** numbering hazards and **three** crossing surfaces, and AC #2 names only one of each.

**Hazard 1 — two independently numbered NFR lists that already disagree.**

| List | Entry | Wording |
|---|---|---|
| **PRD § 8** (`_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md`) | **NFR-3** (Local-first privacy) | *"Repository and artifact analysis runs locally by default; no remote telemetry is required for core operation."* |
| **epics.md § NonFunctional Requirements** | **NFR3** | *"Operation is local-first and privacy-preserving, requiring no remote telemetry for core behavior."* |

The AC quotes the **PRD's**. epics.md records the collision in its own comment block above NFR7 and states it is
*"deliberately NOT bundled"* into any prior change. **If you conclude an amendment is required, you must decide and
state which list it lands in.** The shipped precedent is ADR 0013, which amended the **PRD's NFR-5 only**, with an
inline `<!-- AMENDED … Previous wording: … -->` comment preserving the prior text and its cause — and epics.md
explicitly records that nothing in *its* list changed. Follow that shape, and say in the report whether leaving
epics.md's NFR3 untouched is correct or is drift you are choosing to leave for the collision's own pass.
(memory `nfr-numbering-collision-prd-vs-epics`)

**Hazard 2 — the AC names one non-goal; there are three surfaces.**

1. **PRD § 5 Non-Goals:** *"Building a hosted SaaS with account management and remote data processing in v1."*
   (named by the AC)
2. **PRD § 6.2 Out of Scope for MVP:** *"Cloud sync, authentication, or collaborative editing."* — **not named by
   the AC.** A credentialed integration touches "authentication". Assess it; if it does not cross, say why in a
   sentence rather than silently omitting it.
3. **epics.md NFR12** — the requirement that *governs* this integration rather than constraining it, added
   2026-07-25 for exactly this epic. Read it as the enabling clause the crossing analysis is measured against.

**AC #2's real question, stated plainly:** is *"analysis runs locally **by default**; no remote telemetry is
**required for core operation**"* already satisfied by an opt-in, disabled-by-default, never-required integration —
or is that a reinterpretation dressed as a reading? **Do not treat a real product concession as a
clarification.** Note also the words *"telemetry"* and *"remote data processing"*: a **read-only pull** of results
that a third party already holds is arguably neither. Say whether that distinction is load-bearing or a lawyer's
comfort — the AC's *"does not treat a real product concession as a reinterpretation"* clause exists to stop that
argument being made lazily.

### R8 — NFR12's **letter** has a known gap, and the project already resolved a case by **intent**.

`docs/SonarCloudSetup.md` § *No token, ever, for this project*, verbatim: *"NFR12's literal scope is generated
output and committed directory-scoped settings files, so a README token would sit just outside it. It plainly
crosses NFR12's intent…"*

That was Story 25.6 deciding a badge-URL token was forbidden even though the letter did not reach it. Your credential
design faces the same class of question at larger scale — a token in a shell history, a CI log, an error message, a
crash dump, a `--verbose` trace, a watch-mode diagnostic. **State whether NFR12's letter is sufficient for your
design or whether the ADR should write the intent down**, so the next integration does not re-litigate it.

### R9 — `.specscribe` is a **folder** (ADR 0014), it is gitignored **in this repo only**, and `SavedSettings` is the committed document.

- ADR 0014: `.specscribe` became a folder containing `config.json` (the `SavedSettings` document), explicitly to make
  room for *"per-repository, gitignored, local-first storage"*. That sibling-file affordance is real and is a
  candidate home for a credential — **decide it, do not assume it.**
- `.gitignore:488` ignores `.specscribe` **in SpecScribe's own checkout**. That is this project's choice, not a
  guarantee about a user's repo. ADR 0003 makes directory-scoped settings a user-facing, shareable thing.
  **NFR12 forbids a credential in a settings file that is committed — you cannot control whether a user commits it.**
- The concrete persisted shape is `SavedSettings` (`src/SpecScribe/SettingsStore.cs`): `Source`, `Adrs`, `Output`,
  `ProjectName`, `DeepGit`, `CodeUrl`, `IncludeReadme`, `TodayPolicy`. **The design instruction 26.3 needs from you
  is whether this record may gain a token-bearing field. The expected answer is no — say it explicitly.**

### R10 — `--show-config` is a **machine-parsed** surface with stable keys, and that is what AC #3's proof targets.

`SettingsResolver.Fields` holds the stable machine keys (`project`, `source`, `adrs`, `output`, `readme`,
`deep_git`, `code_url`, `today_policy`) — *"Constants because they appear in the `--show-config` output a CI script
parses; display wording may change, these may not."* `SettingsResolver.LinePrefix = "SpecScribe config:"` selects
those lines. The three-way `ConfigSource { CommandLine, SavedSettings, Default }` provenance is the Story 5.2
pattern 26.3 will extend.

**You own the rule; 26.3 owns the field.** Specify what a config surface may print for a credential-bearing setting
— *present/absent*, *source only*, *a redaction token*, or *nothing at all* — and say which of the four it is, so
26.3's regression test has something exact to pin. AC #3's four named surfaces are `--show-config`, the diagnostics
page (`DiagnosticsTemplater`), generated output, and any file SpecScribe writes into the repository.

### R11 — **ADR numbering: `docs/adrs/` ends at `0033` on disk. `0034` is the first uncontested slot — verify it yourself.**

`0019` remains **claimed-but-unwritten** by Story 18.3, and the owner decided on 2026-07-28 to *leave the proposal
under 0019 as historical record* with a corrective numbering note (18.3's file § 413). Every ADR since 0020 carries a
numbering note saying the same thing; follow that convention and add one.

⚠️ **Confirm by directory listing at authoring time, not by trusting this line.** ADR 0023's own note predicted
`0021` and was wrong within a day. Cite ADRs **by symbol/section, never by line number** (memory
`cite-adrs-by-symbol-not-line-number` — ADR 0015's refs drifted within one day).

### R12 — Node is already a **generate-time runtime**, so "reuse the emitter's shape" is a real posture, not a hypothetical.

ADR 0022 (Proposed, Story 23.5) Decision: *"Node is a build-time toolchain and a **generate-time runtime**. It is
never a shipped toolchain."* SpecScribe boots the prebuilt Nitro artefact at generate time; the standalone binary
channel **requires Node as a documented prerequisite** (`^22.19.0 || ^24.11.0 || >=26.0.0`), failing with an
actionable error when absent.

**Consequence:** an ingestion step implemented in Node — the shape `tools/analysis-digest/index.mjs` already proves
— is architecturally available. Price it against a C# implementation honestly: the Node path inherits ~709 lines of
*already-debugged* fetch/paging/caching/atomicity, but ADR 0022 is **Proposed, not Accepted**, and leaning on it
makes your ADR depend on one that has not been ratified. **Name that dependency if you take it.**

### R13 — Story 26.1 is **done** and it hands you an exact input contract. This is the shortest path to AC #4's handoff.

From 26.1 § 8.1 — what your posture must be able to feed:

- **per-file observation lists** (S1, code pages)
- **a per-file count and per-level breakdown, aggregable to directories** (S2, code map)
- **a repo-wide list carrying rule identity** (S5, the rule leaderboard + triage inbox)
- **a repo-wide four-level tally** (S6, the dashboard Quality Strip)
- **`relatedLocations` in flow order, unsorted** (26.1 § 5.1 — a flow is an ordered sequence; the cap is **5** with
  a mandatory explicit *"+ N more locations"* count)
- **`helpUri` on every record** — present on all 1,534 today, synthesized as an organization permalink rather than
  an API field. Surfaces may render "learn more" links, so **your posture must keep supplying it.**

Also inherited: **`impacts[]` must be sorted** (Sonar returns it non-deterministically; seven shards flipped on
identical input), because 26.4 puts this shape into the Epic 22 IR and the IR **is** covered by the golden
fingerprint.

### R14 — The Epic 22 IR handoff (AC #4) points at a **moving** target. Name which side you target.

25.3 § 10.2 raised this and it is now more live, not less:

- `SpaDelivery.SchemaVersion` is **`2`** today, not `1` as 25.3's report recorded — Story 22.4 bumped it. Re-read
  the constant; do not quote 25.3's figure.
- **Story 23.6 (`23-6-retire-the-c-sharp-html-writer`) is `in-progress` as of 2026-07-30** — a concurrent session,
  today. It is retiring the C# `.html` writer that still produces a large share of the IR. 25.3's caveat —
  *"Epic 26 designing against the IR shape must say which side of that migration it targets"* — is a live
  instruction, not a historical note.
- `GoldenIrFingerprint` was **removed** (commit `70b72ab`, ADR 0033: content-drift gates are targeted, never
  whole-tree). Do not propose a whole-tree hash as a verification affordance. (memory
  `adr-0033-content-drift-gates-are-targeted`)

---

## Acceptance Criteria

1.
**Given** the owner deferred the posture to this spike
**When** candidate sources are evaluated
**Then** it reports, per candidate — **SonarCloud web API**, **on-disk scanner report/export**, and **both** — the data available, freshness, offline behavior, credential requirement, rate limits, and the failure mode when the source is missing or stale
**And** it evaluates the on-disk path at its true cost, including whether a user without a SonarCloud account can get any value at all.

2.
**Given** PRD **NFR-3** ("analysis runs locally by default; no remote telemetry is required for core operation") and the § 5 Non-Goal on remote data processing
**When** the spike assesses the crossing
**Then** it states plainly whether the recommended posture **requires a PRD amendment** or is already accommodated by NFR-3's "by default" / "required for core operation" wording — and if an amendment is required, it drafts the exact replacement text with the prior wording and rationale preserved inline, following the ADR 0013 / NFR-5 precedent
**And** it does **not** treat a real product concession as a reinterpretation.

3.
**Given** any network posture needs a credential
**When** the spike designs credential handling
**Then** it specifies where the token lives (environment variable, directory-scoped `.specscribe` via `SettingsResolver`, or external), proves no token value can reach generated output, `--show-config`, the diagnostics page, or a committed settings file, and states the private-repository posture
**And** it names the supply-chain surface any new dependency adds, handing it to Story 17.2 (NFR10).

4.
**Given** Story 25.3's contract and CLAUDE.md § Decision records
**When** the spike concludes
**Then** it lands a **ratified ADR** covering ingestion posture, credential design, and the AD-4 provider boundary, **consuming Story 25.3's findings model rather than defining a second one** — and stating explicitly if it must amend it
**And** the report states what it hands to Stories 26.3–26.6 and to Epic 22's IR schema (Story 22.2).

---

## Tasks / Subtasks

- [ ] **Task 1 — Re-establish ground truth before evaluating anything** (AC: #1, #4)
  - [ ] Read `tools/analysis-digest/index.mjs` and `tools/analysis-digest/README.md` **in full**. They are ~815 lines and they are the evidence base for the entire web-API posture (⛔ R2). Everything you would otherwise measure is already measured there.
  - [ ] Re-verify by **symbol, not line number**: `SettingsResolver.Fields` / `.LinePrefix` / `ConfigSource`, `SavedSettings`, `SpaDelivery.SchemaVersion`, and the absence of `HttpClient` under `src/` (⛔ R1, R9, R10, R14).
  - [ ] Confirm the ADR slot by **directory listing** `docs/adrs/` (⛔ R11). Record the number you took and why.
  - [ ] Optionally refresh the digest (`node tools/analysis-digest/index.mjs`) if you need live density; apply CLAUDE.md's read-time staleness rule (`git rev-parse HEAD` vs `provenance.evaluatedAtRevision`) and record the revision. **Every figure moves** — 25.3 saw 121/960/385, 25.4 saw 120/979/389, 26.1 saw 125/1,013/396.
  - [ ] Confirm 23.6's status in `sprint-status.yaml` before writing the IR handoff (⛔ R14) — it was `in-progress` at authoring time.

- [ ] **Task 2 — Evaluate the three postures against AC #1's seven axes** (AC: #1)
  - [ ] Build one table: rows = **SonarCloud web API** / **on-disk scanner report or export** / **both**; columns = **data available · freshness · offline behavior · credential requirement · rate limits · failure mode when missing · failure mode when stale**. Every cell cites its evidence tier (measured here / measured by 25.3–25.4 / documentation-grade / unmeasured).
  - [ ] **The on-disk path at true cost** (⛔ R4): it has no producer today. Name what would have to exist — a CI artifact upload, a local scanner run, or a *different* producer (raw Roslyn SARIF via `ErrorLog`) — and price each. Do not evaluate a candidate that nothing can currently produce as though it were available.
  - [ ] **Answer "can a user with no SonarCloud account get value?"** using 26.1's measured 56 % `external_roslyn` split, with both counterweights stated: 25.4's duplication finding and the 44 %-density figure for a non-.NET project (⛔ R5).
  - [ ] **Rate limits are named in the AC and are not measured anywhere.** Measure, cite SonarSource's documentation, or declare unmeasured. The emitter's `RULE_FETCH_CONCURRENCY = 4` is a politeness choice, not a discovered limit.
  - [ ] Test the provenance gate against each candidate (⛔ R6): can it supply an `analysisRevision`? For any build-time source, address the untested dirty-tree case (25.3 § 9) explicitly.
  - [ ] Land a **single named recommendation**, not a menu. The AC allows "both"; if you choose it, say which is primary and what the second one is *for*.

- [ ] **Task 3 — Answer the PRD crossing, plainly** (AC: #2)
  - [ ] Quote the three surfaces verbatim before analysing them: PRD **NFR-3**, PRD **§ 5** Non-Goal, and PRD **§ 6.2** *"Cloud sync, authentication, or collaborative editing"* (⛔ R7 hazard 2 — the AC names only the first two).
  - [ ] State the verdict in **one sentence** at the top of the section: *amendment required* or *already accommodated*. Then argue it. A reader must not have to infer which you concluded.
  - [ ] Address the **"telemetry" / "remote data processing" vs read-only pull** distinction head-on. If you rely on it, say so and defend it; if it is a comfort argument, say that instead.
  - [ ] **If an amendment is required:** draft the **exact replacement text** for the PRD, with an inline `<!-- AMENDED YYYY-MM-DD (Story 26.2, ADR 00NN). Previous wording: "…" -->` comment preserving prior wording **and** rationale — the ADR 0013 / NFR-5 shape, which you can read directly in `prd.md` § 8 NFR-5.
  - [ ] **State which list the amendment lands in** and whether epics.md's `NFR3` is left untouched (the ADR 0013 precedent) or must move with it (⛔ R7 hazard 1). A change recorded in only one artifact is the drift bug CLAUDE.md § Decision records names.
  - [ ] **If no amendment is required:** say what specifically makes the existing wording sufficient, and record what change to the posture would flip the answer — so 26.3 cannot widen scope past the reading you licensed.

- [ ] **Task 4 — Design credential handling** (AC: #3)
  - [ ] Choose the home: **environment variable**, a **gitignored sibling inside the `.specscribe` folder** (the ADR 0014 affordance), or **external** (e.g. the MCP-client-config precedent 25.3 § 10.5 path 3 names). Give the reason, not just the pick.
  - [ ] **State explicitly that `SavedSettings` must not gain a token-bearing field**, or argue why it may (⛔ R9). 26.3 needs this as an instruction, not an inference.
  - [ ] Specify the rule for each of AC #3's four surfaces — `--show-config`, the diagnostics page, generated output, and any file written into the repository — and say exactly what a config surface may print for a credential-bearing setting: *present/absent*, *source only*, *a redaction token*, or *nothing*. **Pick one so 26.3's regression test has something exact to pin** (⛔ R10).
  - [ ] Extend the analysis past NFR12's letter to the surfaces R8 names — shell history, CI logs, error messages, `--verbose` traces, crash dumps, watch-mode diagnostics — and state whether the ADR should write NFR12's *intent* down.
  - [ ] **State the private-repository posture** and label its evidence tier honestly (⛔ R3). "Documentation-grade, server not exercised" is an acceptable answer; an unlabelled claim is not.
  - [ ] Specify the **misconfigured / expired credential** behavior 26.3 AC #3 will implement: actionable message, never a stack trace, never a silent empty surface.

- [ ] **Task 5 — Name the supply-chain surface and hand it to Story 17.2** (AC: #3)
  - [ ] Inventory what the recommended posture adds: new package references, a Node dependency (⛔ R12), a transitively-trusted service, or nothing.
  - [ ] Write the handoff so 17.2 AC #2 can consume it directly — it already names *"if Epic 26 shipped, its external-service integration"* and requires verifying no credential reaches generated output or a committed settings file, that the integration is off by default, and that the NFR3 re-confirmation accounts for **the outbound network path this ADR authorized**.
  - [ ] Do **not** edit Story 17.2 or epics.md for this — the AC is already written. Put the material in your report's handoff section.

- [ ] **Task 6 — Author the ADR and get it RATIFIED** (AC: #4)
  - [ ] Cover all three subjects the AC names: **ingestion posture**, **credential design**, and the **AD-4 provider boundary** (`ARCHITECTURE-SPINE.md`: optional insight providers may enrich output but never own baseline success).
  - [ ] **Consume ADR 0023; do not define a second model.** If the posture cannot supply an analysis revision, **amend ADR 0023** rather than working around it — its own § Consequences requires this (⛔ R6). State explicitly either way, including "no amendment needed".
  - [ ] Add the numbering note (⛔ R11) and read `docs/adrs/` before claiming any project rule is crossed (memory `adr-consultation-gap-three-arc-renderers` — Story 21.3 declared it was crossing a rule that a two-day-old ADR already permitted).
  - [ ] **Ratified, not merely drafted.** AC #4 says *ratified*; ADR 0023's own status line says why — *"six downstream stories bind to this record, and a Proposed ADR is not a contract they can bind to."* Four stories bind to yours. Take it to the owner in the dev pass.
  - [ ] Add the entry to `docs/adrs/README.md` — every record has one.

- [ ] **Task 7 — Write `26-2-spike-report.md` and close the handoffs** (AC: #1, #2, #3, #4)
  - [ ] Deliverable at `_bmad-output/implementation-artifacts/26-2-spike-report.md`, following the `25-3-spike-report.md` / `26-1-ideation-record.md` shape: executive summary → what was measured and at which revision → per-candidate evaluation → the crossing → credential design → **a handoff section per downstream story** → what was NOT measured.
  - [ ] **A handoff section each for 26.3, 26.4, 26.5, 26.6, 26.7, Story 17.2, and Epic 22's IR schema (Story 22.2).** That per-story structure is the mechanism that made 25.3's and 26.1's reports usable — it is not decoration.
  - [ ] The **Epic 22 IR handoff must name which side of the 23.4/23.6 migration it targets** and quote the *current* `SpaDelivery.SchemaVersion` (⛔ R14), plus whether ingestion implies a version bump per ADR 0016's rule.
  - [ ] A **"what was NOT measured"** section, in 25.3 § 14's style. It is the section that made this story possible; write the one that makes 26.3's possible.
  - [ ] **If the recommendation changes 26.3–26.6's scope, amend `epics.md` AND `sprint-status.yaml` in the same change** (CLAUDE.md § Decision records). If it changes neither, say so.

- [ ] **Task 8 — Verify the no-code contract** (AC: all)
  - [ ] `git status --porcelain -- src/ tests/ web/ extension/ tools/ .github/` shows nothing **of yours**. Expect a concurrent session's files there (23.6 was `in-progress` at authoring time) — verify by **attribution**, as 26.1 § 10.3 did, never by a clean status. **Never `git reset --hard`, `git checkout --`, or `git clean`.**
  - [ ] **Do not run a generation and do not measure the golden fingerprint.** This story ships no code, so the fingerprint cannot move; measuring it under a concurrent session reads somebody else's in-flight change, and an incremental build would not re-embed a changed asset anyway. Say in the record that it was deliberately not measured — 26.1 § 10.3 is the precedent to copy.
  - [ ] Update `sprint-status.yaml` for `26-2-…` and add a `## Change Log` entry to this file.

---

## Dev Notes

### The decision the timebox is really for

Three postures, one recommendation, in two days. The expensive-looking work — proving the web API, its paging, its
rate limits, its determinism — **is already done and shipped** (⛔ R2). Spending the box re-deriving it is the
failure mode. The genuinely open questions are:

1. **What does the on-disk path actually cost**, given nothing produces one today (⛔ R4)?
2. **Does a credentialed integration cross the PRD**, and is the answer a clarification or a concession (⛔ R7)?
3. **What does the credential design look like when it cannot be tested** on a public-only project (⛔ R3)?

Those three deserve the box. Everything else is citation.

### Working conditions (CLAUDE.md, non-negotiable)

- **Another agent may be editing the same files right now.** At authoring time `sprint-status.yaml` and
  `23-6-retire-the-c-sharp-html-writer.md` were both dirty from a concurrent session. Verify after every edit;
  grep for a symbol before relying on it — a zero-grep can be a transient mid-write (memory
  `shared-main-concurrent-edit-loss-verify-after-edit`).
- **Never `git reset --hard`, `git checkout --`, or `git clean`.** This has destroyed real work in this repository.
- **Anchor your `sprint-status.yaml` edit by key text, never by line number.** Story 25.6 hit exactly this when a
  concurrent session moved README anchors mid-story.
- Expect `workingTreeDirty: true` in any digest provenance you read. Treat cited lines as approximate; confirm by
  symbol.

### Citation discipline

**Cite ADRs by symbol/section, never by line number** (memory `cite-adrs-by-symbol-not-line-number`). Story files
survive via `baseline_commit`; ADRs do not.

⚠️ **Two requirement-numbering hazards, both live.** (1) FR41 / FR27 / FR21 / FR31 and NFR1 / NFR3 / NFR7 / NFR8 /
NFR10 / NFR12 as cited across Epic 26 live in **`epics.md`**, not `prd.md` — the PRD numbers its `FR-n`/`NFR-n` list
independently and already disagrees. (2) The AC's *"PRD NFR-3"* is genuinely the PRD's entry, which is why ⛔ R7
exists. Read both lists before writing a single amendment. (memory `nfr-numbering-collision-prd-vs-epics`)

### Reading the analysis digest correctly

`.specscribe/analysis/` is gitignored, dev-time only, refreshed by hand. Go straight to a shard for a file you care
about — the path is derivable. `index.json` is the repo-wide view only (~32 KB); reading everything costs ~1.69 MB.
**No shard = no open observations on that file. A missing digest means UNKNOWN, never clean.**

### Project Structure Notes

- Deliverables land in `_bmad-output/implementation-artifacts/` and `docs/adrs/`, alongside `25-3-spike-report.md`
  and `26-1-ideation-record.md`.
- Any disposable evidence goes under `spike/`, which is quarantined by `spike/README.md`: *"no `.sln` references it,
  it is not part of `src/SpecScribe`'s build or `dotnet pack`… The generated site is byte-identical with or without
  this folder."* Prefer citing 25.4's shipped emitter over writing new probe code — that is the whole point of R2.
- This story writes **no** file under `src/`, `tests/`, `extension/`, `web/`, `tools/`, or `.github/`.

### Testing standards

No tests. `ships_product_code: false`. Verification is Task 8: attribution over the product directories, and a
fingerprint that was deliberately never measured. The regression test AC #3 anticipates ("no token in
`--show-config`, pinned by a regression test") is **Story 26.3's** — you specify what it pins; you do not write it.

---

## Previous Story Intelligence

**From Story 26.1 (ideation, `review` — the immediately preceding story):**

- **§ 8.1 is addressed to you by name** and is quoted in ⛔ R13. Its headline: *"Nothing here is blocked on your
  answer, and nothing here constrains it… Do not re-run this round."* Every direction was chosen to be
  posture-independent, and that was confirmed as create-story Open Question 5.
- Its **method** is the one to copy: refresh the numbers first (Task 1), verify every attach point **by symbol**
  (five cited lines had drifted since its own baseline), then decide. It also proves the value of a per-downstream-
  story handoff section.
- Its Task 8 note is the precedent for Task 8 here: the literal clean-status check **did not pass**, because ~20
  files from a concurrent session were in the tree. It was resolved by **attribution**, documented in § 10.3, and
  no destructive git command was used. Do the same.
- It deliberately performed **no generation run** and never measured the fingerprint, for the reason restated in
  Task 8.

**From Story 25.3 (spike, DONE — ADR 0023 Accepted):**

- § 11's *"Story 26.2 — consumes this contract, does not redefine it"* is the shortest statement of your boundary:
  *"The model, severity axis, and attachment vocabulary are settled; 26.2 supplies **how bytes arrive**."*
- § 10.5 named a **credential-ordering risk** and resolved it for 25.4 by taking the credential-free path, adding:
  *"If 25.4 finds path 1 impractical, that is a **constraint on 26.2**, to be raised."* 25.4 shipped
  credential-free, so no constraint was raised — but note it took a *fourth* path 25.3 did not list: anonymous
  access to a public project's API. **That is itself a posture worth naming in your table**, because it is the one
  in production today and it satisfies NFR12 trivially for public projects.
- § 10.3 prices **Sonar's official MCP server as a posture, not just a channel** — the credential lives in MCP
  client config, outside SpecScribe entirely. 25.3 § 11 tells you to price it that way. It was read, not run
  (§ 14 item 6): documentation-grade.
- § 14 is the model for your own "not measured" section, and its item 7 is your AC #3.

**From Story 25.4 (digest emitter, `review`):**

- The emitter is ⛔ R2's evidence base. Read it before designing anything.
- Owner decision **D5**: attachment is emitted as `basis: "unavailable"` and **not computed**, because the fan-out
  bounding rule is 26.5's. That is why every current record reads "unavailable" rather than "none" — not because
  nothing attaches.
- ⚠️ **`impacts[]` is non-deterministically ordered and the emitter sorts it.** Carry this into the 26.4 handoff;
  the IR is fingerprint-covered.

**From Story 25.6 (badges, `review`):** the `docs/SonarCloudSetup.md` § *No token, ever* analysis in ⛔ R8 is its
work, and it is the closest existing precedent for reasoning about NFR12's letter versus its intent.

---

## Git Intelligence

`HEAD` = `e864133` ("review work"), 2026-07-30. Recent commits (`5a78ee7` IR content-layer regeneration for 24.2,
`6df8e0d` "Today's pulse", `bc7a379` Epic 22 retrospective, `70b72ab` removal of the non-deterministic IR
fingerprint gate) are batch commits bundling several stories each — the expected pattern, because code review runs
at epic end (CLAUDE.md § Story lifecycle).

**Scope any later review of this story by its own File List and declared symbols, never by a commit range**, and
where a file appears in more than one in-flight story's File List, attribute **by hunk** (CLAUDE.md § Scoping a
code review). At authoring time the working tree carried a concurrent session's edits to `sprint-status.yaml` and
`23-6-retire-the-c-sharp-html-writer.md`.

`70b72ab` is directly relevant to your verification design: the whole-tree IR fingerprint gate was **removed** as
non-deterministic, and ADR 0033 replaced it with targeted, regenerable drift gates. Do not propose a whole-tree hash.

---

## Latest Technical Information

Verified against shipped code and the project's own records at `e864133`, 2026-07-30 — not from general knowledge:

- **SonarCloud endpoints in production use here:** `api/issues/search` (paged, `p × ps ≤ 10000` hard ceiling,
  `resolved=false` mandatory), `api/project_analyses/search` (supplies `revision` — the staleness anchor),
  `api/rules/show` (per distinct rule; **requires `organization`**; has **no** `helpUri` field, which the emitter
  synthesizes as an organization permalink), `api/project_badges/measure` (public, anonymous), and
  `api/project_badges/token` — which mints a badge token for **private** projects and which
  `docs/SonarCloudSetup.md` says **must never be called for this project**.
- **Anonymous access works because the project is `visibility: public` on the free tier.** That is a property of
  *this* project, not of SonarCloud, and it is exactly why AC #3's private posture is unmeasured here.
- **Node runtime:** `24.11.1` per `web/.nvmrc`; ADR 0022's supported range for the standalone channel is
  `^22.19.0 || ^24.11.0 || >=26.0.0`. The digest emitter has **zero runtime dependencies** — `fetch` and
  `node:child_process` only — so the Node posture adds no npm supply-chain surface of its own (relevant to Task 5).
- **Sonar's MCP server** ships as `sonarsource/sonarqube-mcp` (Docker) or a JAR, supports Server and Cloud,
  documents Claude Code explicitly, and **requires a token**. Documentation-grade only: 25.3 read the docs and did
  not run the server.
- **SARIF 2.1.0** is an OASIS Standard (Approved Errata 01, 28 August 2023); ADR 0023's profile keeps
  `result.level` verbatim and pins `result.kind = "fail"`.

---

## Project Context Reference

`_bmad-output/project-context.md` carries no populated rules (its Technology Stack and Critical Implementation
Rules sections are placeholders). **`CLAUDE.md` is the operative project-context document** and is auto-loaded into
every session — §§ Concurrent work, Story lifecycle, Decision records, Analysis observations, and Verification all
bind here. The architecture spine is `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` (AD-3 configurability
parity, AD-4 the optional-provider boundary this ADR must restate for a networked provider).

---

## References

- Story ACs and epic framing — `_bmad-output/planning-artifacts/epics.md` § *Epic 26* / § *Story 26.2*, including the dated `<!-- 2026-07-25 (SCP 2026-07-25): THE NFR-3 CROSSING IS STORY 26.2's TO DECIDE -->` comment
- The findings contract (Accepted; consume, do not redefine) — `docs/adrs/0023-agent-facing-analysis-observation-contract.md` §§ Decision 3, 4, 5, 6, 7, 8, Consequences, Explicit non-goals
- The contract spike, its handoff to this story, and its unmeasured list — `_bmad-output/implementation-artifacts/25-3-spike-report.md` §§ 9, 10.1–10.6, 11 (*Story 26.2*), 14
- The ideation record and its § 8.1 handoff to this story — `_bmad-output/implementation-artifacts/26-1-ideation-record.md` §§ 1.1, 1.2, 5.1, 7.2, 8.1, 8.6
- The shipped, credential-free, networked emitter — `tools/analysis-digest/index.mjs`; `tools/analysis-digest/README.md`
- Operator-facing Sonar posture, token handling, and the NFR12 letter-vs-intent precedent — `docs/SonarCloudSetup.md` §§ *Step 2 — Generate a token*, *Step 3*, *The agent-facing digest*, *The Sonar MCP server is a complement*, *No token, ever, for this project*, *Security notes*
- PRD NFR-3, § 5 Non-Goals, § 6.2 Out of Scope, and the ADR 0013 / NFR-5 amendment shape to copy — `_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/prd.md` §§ 5, 6.2, 8
- Requirements as cited by Epic 26 — `_bmad-output/planning-artifacts/epics.md` §§ Functional Requirements (FR21, FR27, FR31, FR41), NonFunctional Requirements (NFR1, NFR3, NFR7, NFR8, NFR10, NFR11, NFR12) and the numbering-collision comment above NFR7
- The supply-chain consumer — `_bmad-output/planning-artifacts/epics.md` § *Story 17.2*, AC #2 third clause
- Settings folder, settings resolution, and the persisted document — `docs/adrs/0014-specscribe-settings-folder-format.md`; `docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md`; `src/SpecScribe/SettingsResolver.cs` (`Fields`, `LinePrefix`, `ConfigSource`, `CliOverrides`, `SettingsLoad`); `src/SpecScribe/SettingsStore.cs` (`SavedSettings`)
- Node at generate time — `docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md` § Decision (**Proposed**, not Accepted)
- The IR and its versioning rule — `docs/adrs/0016-ir-carries-rendered-prose-html.md`; `src/SpecScribe/SpaDelivery.cs` (`SchemaVersion`, `DeltaSchemaVersion`)
- Targeted drift gates, and why no whole-tree hash — `docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md`; commit `70b72ab`
- Spike quarantine rules — `spike/README.md`
- CI as it stands (no SARIF or issues artifact is published) — `.github/workflows/build-test-analyze.yml`
- Project working conventions — `CLAUDE.md` §§ Concurrent work, Story lifecycle, Scoping a code review, Decision records, Analysis observations, Verification

---

## Open Questions Raised at Create-Story (non-blocking — settle inside the spike)

These surfaced during analysis and have no answer in any existing artifact. None gates starting; each changes a
downstream story if answered differently.

1. **Is "anonymous access to a public project" a fourth posture, or a mode of the web-API posture?** It is what
   ships today (⛔ R2) and it satisfies NFR12 trivially — no credential exists to leak. If the recommendation is
   "web API, credential only when the project is private", then AC #3's credential design describes a path most
   users will never take, and that should be said out loud rather than left as an implicit majority case.
   *Default if unaddressed:* treat it as a mode, and state the public-project case first because it is the common one.

2. **Does the ADR authorize the network call, or does it authorize a *provider seam* that happens to make one?**
   Story 26.7 asks whether your design generalizes to a pluggable external-signal provider seam, and 25.3 § 11's
   note to 26.7 already recommends *"pluggable normalizers, one shared `AnalysisObservation`."* Deciding the seam
   here would pre-empt 26.7; deciding *nothing* about it leaves 26.7 with no boundary to extend.
   *Default if unaddressed:* authorize the call, name the seam as 26.7's, and state which parts of your design are
   deliberately provider-shaped so 26.7 knows what it would have to generalize.

3. **C# or Node for the eventual implementation?** ADR 0022 makes Node available at generate time and 25.4 already
   proved ~709 lines of it, but ADR 0022 is **Proposed**. Leaning on it makes your ADR depend on an unratified one.
   *Default if unaddressed:* state the trade-off, recommend, and name the ADR 0022 dependency explicitly rather
   than inheriting it silently.

4. **Where does the ingested payload land on disk for the product path?** 25.4 chose `.specscribe/analysis/` for
   the *agent* channel and ADR 0023 Decision 7 sends **Epic 26** to the IR instead. Does the product re-fetch, or
   read the digest 25.4 already writes? A shared artifact would be cheap and would couple a product surface to a
   dev-time tool; two fetches would duplicate the round trip.
   *Default if unaddressed:* say which, because 26.3's "digest location" setting (25.3 § 11 → 26.3) assumes an answer.

5. **Does a private repository change what may be rendered, not just what may be fetched?** AC #3 says "state the
   private-repository posture", which reads as an access question — but a private project's rule messages and file
   paths reaching a generated portal is a *disclosure* question, and Story 17.2 AC #2 covers privacy on private
   codebases.
   *Default if unaddressed:* note it and hand it to 17.2 rather than deciding it here.

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-30: Story created (baseline `e864133`). Context assembled from Epic 26 § Story 26.2, ADR 0023 (Accepted), the Story 25.3 spike report (§§ 9, 10, 11, 14), the completed Story 26.1 ideation record (§ 8.1's explicit handoff), the shipped Story 25.4 emitter and its README, `docs/SonarCloudSetup.md`, the PRD's NFR-3 / § 5 / § 6.2, and a verified inventory of the settings, IR, and CI surfaces the spike must reason about. Fourteen reconciliations recorded — chief among them that SpecScribe's product code makes zero network calls today, that a credential-free networked ingestion path already exists and is measured outside the product, that the on-disk candidate has no producer today, and that AC #3's private-repository posture is structurally unmeasurable on this public project. Status → ready-for-dev.
