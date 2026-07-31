---
baseline_commit: 261b3008545a066ae1b08174b77df5b4abd4fb73 # `261b300` — HEAD at authoring time (2026-07-26)
epic: 25
nfr: NFR11
frs: [FR30] # the triage output uses the EXISTING follow-up provenance conventions
depends_on: [25-1] # the workflow, the exclusion list, and the first real analysis
blocks: [25-6] # the quality-gate badge must not advertise a gate that asserts nothing
ships_product_code: false # dev-time only. The golden fingerprint MUST NOT move. No `src/` edits.
touches:
  - ".github/workflows/build-test-analyze.yml"
  - "docs/SonarCloudSetup.md"
  - "_bmad-output/implementation-artifacts/deferred-work.md"
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
  - "_bmad-output/planning-artifacts/epics.md" # only if a triage item amends an Epic 17 story's scope
# NOT src/**, NOT tests/**, NOT extension/src/**, NOT web/**
---

# Story 25.2: Quality Gate and Findings Triage into the Project Backlog

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the SpecScribe maintainer,
I want the analysis results scanned and routed into this project's own backlog,
So that Sonar produces work items I actually act on rather than a dashboard I stop visiting.

## ⛔ Read first — five live facts that invert this story's stated premises

Every number below was read from SonarCloud's public API on **2026-07-26**, against analysis
`2026-07-26T19:00:08Z` on revision **`261b300`** (current `main`). Re-read them before you start — they move
with every push (see § Re-measure first).

1. **A quality gate already exists and is already RED.** The epic reads as though 25.2 creates a gate from
   nothing. It does not. SonarCloud applies its **default `Sonar way` gate (id 9)** to every project, and this
   project's `alert_status` is **`ERROR`** right now. Your AC #1 job is to *decide whether that is the gate we
   want*, not to invent one.

2. **The gate is red for exactly one reason, and it is not C# quality.** Five of six conditions pass. The one
   that fails is `new_coverage` — **69.8 % against an 80 % threshold**. Broken down: new C# code is **348 lines
   to cover, 24 uncovered → 93.1 %**. New `web/` code (Story 23.2's Nuxt component library) is **153 lines to
   cover, 153 uncovered → 0 %**. **The gate is failing on untested JavaScript/Vue, and the coverage report the
   workflow uploads is C#-only OpenCover — it structurally cannot cover `web/`.** This is a design decision you
   must make, not a bug you can fix by writing C# tests.

3. **The "155 vulnerabilities" question 25.1 handed forward is answered: it is one rule.** **151 of the 155**
   are `csharpsquid:S6444` — *"Regular expressions should be executed with a timeout"*, severity **MINOR**.
   That is **one** decision (accept, or schedule a sweep), not 155 problems. The remaining 4 are individually
   listed in § The baseline.

4. **`api/issues/search` lies to you by default.** Without `resolved=false` it returns **1,508** issues; the
   real unresolved count is **1,360**. The 148-issue difference is CLOSED/FIXED issues on `.claude/`,
   `.agents/`, `spike/`, and `tools/` files that Story 25.1's exclusion widening removed from analysis.
   Triaging the default response would produce ~148 backlog items pointing at files Sonar no longer looks at.
   **Always pass `resolved=false`.**

5. **`deferred-work.md` is parsed and rendered by SpecScribe itself.** `DeferredWorkParser`
   (`src/SpecScribe/DeferredWorkParser.cs`) turns it into the portal's follow-up surfaces (FR30 / Story 9.6).
   Whatever you write there becomes rendered product output on this project's own site. A triage pass that
   emits 1,360 line items floods it. See § The output format is a parsed contract for the shape and the budget.

> **A concurrent session is editing `src/` right now.** At authoring time the working tree carried uncommitted
> Story 22.2 work — `src/SpecScribe/SiteGenerator.cs`, `SpaBundle.cs`, `SpaDelivery.cs`, and
> `22-2-canonical-ir-schema-and-versioning.md`. Per CLAUDE.md: **never `git reset --hard`, `git checkout --`,
> or `git clean`.** If a local `dotnet test` fails in `src/` code you did not write, that is the other session
> — say so and move on. Expect the analysis numbers below to have moved by the time 22.2 lands.

## Acceptance Criteria

### AC #1 — A defined gate, decided rather than inherited, visible on the pull request

**Given** an analysis run completes
**When** the quality gate evaluates
**Then** a defined gate (new-code conditions at minimum) reports pass/fail as a **visible signal on the pull
request**
**And** the story records which conditions are **enforcing vs advisory**, and **what a failing gate blocks**.

This AC is met only when all five sub-decisions are recorded, each with its reason:

| # | Decision | Why it is not obvious |
|---|---|---|
| 1a | **`Sonar way` as-is, or a custom gate?** | `Sonar way` is applied today and is failing. Keeping it is a legitimate answer — but it must be a *choice*, stated. |
| 1b | **The `new_coverage` condition.** | 80 % is unreachable while `web/` and `extension/src` contribute uncovered new lines to a C#-only coverage report. Options in § The coverage trap. Pick one, price it. |
| 1c | **The new-code period.** | Currently `days: 30` (a sliding window), effectively starting at the first analysis `2026-07-25T20:54:41Z`, so "new code" today is **3,198 lines** and will keep shifting under you. `previous_version` or a reference branch are the alternatives. |
| 1d | **`sonar.qualitygate.wait`.** | Deliberately unset by 25.1, so a red gate does **not** fail CI today. Setting it is what turns "reports pass/fail" into "blocks". This is the literal answer to "what a failing gate blocks". |
| 1e | **Where the gate definition lives.** | Gate conditions are server-side in the SonarCloud UI — they cannot go in the workflow file. That breaks 25.1's "the truth lives in a diff" precedent. Say so, and say what mitigates it (e.g. the conditions transcribed into `docs/SonarCloudSetup.md`). |

**And** the "visible signal on the pull request" half is **demonstrated on a real tokened pull request, not
asserted.** Story 25.1 recorded that PR decoration is performed by the SonarQube Cloud GitHub App using its own
installation token (hence no `pull-requests: write`) — but the only PR run so far (PR
[#2](https://github.com/IntegerMan/SpecScribe/pull/2), run `30176207551`) predates the token and took the
token-absent path. **Decoration has never been observed on this repository.** If it does not appear, the
`permissions:` block in the workflow is the first suspect and changing it is in scope for this story.

### AC #2 — A repeatable triage pass, and the baseline actually performed

**Given** findings accumulate
**When** they are triaged
**Then** a **documented, repeatable** triage pass routes each material finding to a decision — **fixed**,
**scheduled into a named story**, or **explicitly accepted with rationale** — and lands in `deferred-work.md` /
`sprint-status.yaml` action items using the existing FR30 provenance conventions
**And** the **initial baseline triage of the existing codebase is performed and its result recorded**, so
Epic 17's hardening pass inherits a known state rather than an unread dashboard.

Binding clarifications:

- **"Repeatable" means a written procedure a future session can execute** — the API calls, the `resolved=false`
  filter, the rule-first (not issue-first) grouping, and the output format. Prose describing what you did once
  is not a repeatable pass. Put it in `docs/SonarCloudSetup.md` (which is already the durable home for this
  project's Sonar knowledge and is already linked from `README.md`).
- **Triage by RULE, not by issue.** 1,360 issues collapse to **~40 rules**, and the top 3 rules are **730
  issues (53.7 %)**. A per-issue pass is not a triage, it is a transcription.
- **"Material" is your call and must be defined in writing.** The 754 INFO-severity issues (all of them
  external Roslyn analyzer suggestions) are a defensible bulk-disposition; the 11 bugs are not.
- **The 11 bugs are individually enumerated in § The baseline.** Each one gets a named decision. There is no
  volume excuse for this set.

### AC #3 — Epic 17 tagging, and rule-level decisions that survive the next run

**Given** findings overlap Epic 17's scope
**When** triage runs
**Then** items matching Stories 17.1–17.3 (structural, security/privacy, performance) are **tagged to those
stories rather than duplicated**
**And** anything Sonar reports that the project **deliberately does not follow** is recorded as a **rule-level
decision, not silently re-triaged every run**.

Binding clarifications:

- **"Tagged, not duplicated"** means the item is recorded once, against the story that will fix it, with enough
  detail that 17.1/17.2/17.3 can act without re-querying Sonar. A pre-drafted routing table is in
  § Epic 17 routing — verify it against live data, do not copy it blind.
- **A rule-level decision must have exactly one home, and you must name it.** The candidates are enumerated in
  § Where a rule-level decision lives, with the trade-off each carries. This repo has **no `.editorconfig` and
  no `Directory.Build.props`** — verified — so the in-repo option means *creating* the file, which is a new
  cross-cutting convention. **If you choose that, propose an ADR** (CLAUDE.md § Decision records): it changes
  where every future analyzer decision in this repo is recorded, for `src/`, `tests/`, and `extension/` alike.
- **A decision that lives only in the SonarCloud UI is a decision that will be silently re-triaged.** That is
  the failure mode this AC exists to prevent — say explicitly how yours survives.

## Tasks / Subtasks

- [x] **Task 1 — Re-measure the baseline before deciding anything (AC: #2)**
  - [x] Run the four commands in § Re-measure first and record today's numbers. **Every figure in this story is
        from 2026-07-26 and `main` moves fast** — a concurrent session landed 20.5/20.7/22.2/23.2 in `261b300`
        and moved ncloc 32,788 → 34,180 between 25.1's record and this one.
  - [x] Confirm the delta against this story's § The baseline table and note anything that moved materially.
        If a number here is wrong, say so in the record — do not quietly re-baseline.
  - [x] Confirm the `resolved=false` gap is still real (unresolved total vs. unfiltered total). If it has
        closed, say so; if it has widened, that is a finding.

- [x] **Task 2 — Decide the gate (AC: #1)**
  - [x] Read the live gate state: `api/qualitygates/project_status` and `api/qualitygates/get_by_project`.
        Record the applied gate's name/id and every condition with its current actual value.
  - [x] Settle 1a–1e from AC #1's table. Each needs one paragraph: what you chose and what you rejected.
  - [x] For **1b** specifically: quantify the option you pick against today's numbers (new C# 348 LTC / 24
        uncovered; new `web/` 153 LTC / 153 uncovered). A `sonar.coverage.exclusions` answer goes on the
        `begin` step in the workflow — in a diff, matching 25.1's precedent — and must state plainly that
        `web/` and `extension/src` are then **unmeasured, not covered**.
  - [x] For **1d**: if you set `sonar.qualitygate.wait`, prove the job goes red on a failing gate the same way
        25.1 proved red-on-test-failure — a real run, not an assertion. If you do not set it, say what makes
        the signal actionable without it.
  - [x] Update `docs/SonarCloudSetup.md` § *Quality gate* — it currently says *"No quality gate is enforced by
        this workflow"*, which is true about `qualitygate.wait` and **misleading about the gate**, since
        `Sonar way` is evaluating and failing today. Correct it either way.

- [x] **Task 3 — Prove PR decoration, or find out why it is absent (AC: #1)**
  - [x] Open a real pull request against `main` from a same-repo branch (so the token is present) and record
        whether the SonarQube Cloud check and/or comment appears.
  - [x] If it does not appear: check the SonarQube Cloud GitHub App installation on `IntegerMan/SpecScribe`,
        then the workflow `permissions:` block (`contents: read` only, today). Record what fixed it.
  - [x] Record the check name(s) SonarCloud contributes, so **Story 16.2** knows whether it has one required
        check (`build-test-analyze`) or two.

- [x] **Task 4 — Perform the baseline triage, rule-first (AC: #2, #3)**
  - [x] Pull the rule facet with `resolved=false` and work top-down by count. § The baseline has the current
        ranking.
  - [x] For each rule reaching your materiality bar, record: rule id, name, count, severity, whether it is a
        SonarSource rule (`csharpsquid:`/`css:`/`typescript:`…) or an **external Roslyn** import
        (`external_roslyn:`), and the decision — fixed / scheduled to story N / accepted with rationale.
  - [x] Give the **11 bugs** individual decisions. They are listed in § The baseline with file and line.
  - [x] Give `csharpsquid:S6444` (151 of 155 vulnerabilities) a single explicit decision. Note that it is
        **MINOR** severity and drives the project's **security rating C** — those two facts pull in opposite
        directions and the record should say which won.
  - [x] State the disposition of the **755 external Roslyn / INFO** issues as one decision with its reasoning,
        not 755 items.

- [x] **Task 5 — Write the triage output in the format SpecScribe itself parses (AC: #2, #3)**
  - [x] Follow § The output format is a parsed contract exactly. Verify by running a generation into
        `SpecScribeOutput/` and looking at the rendered follow-up surface — **not** by re-reading the markdown.
  - [x] Keep to the budget in that section. If you exceed it, say why in the record.
  - [x] Add `sprint-status.yaml` action items only for items that need a *person* to act, following the
        existing `- epic: / action: / owner: / status:` shape at `action_items:`. Findings scheduled into
        Epic 17 stories belong in `deferred-work.md`, not duplicated here (AC #3).

- [x] **Task 6 — Record the rule-level decision home (AC: #3)**
  - [x] Choose from § Where a rule-level decision lives and record why.
  - [x] If the choice is a new `.editorconfig` / `Directory.Build.props`: **propose an ADR** and do not bury
        the decision in this story file (CLAUDE.md § Decision records). Neither file exists today — verified.
  - [x] Whatever the choice, add it to `docs/SonarCloudSetup.md` so the next maintainer finds it without
        reading this story.

- [x] **Task 7 — Fix only what is in scope, and route everything else (AC: #2)**
  - [x] In scope to actually fix: the **3 `githubactions:*` findings on
        `.github/workflows/publish-docs-live-pages.yml`** (workflow-level permissions that should be
        job-level). Note that **`build-test-analyze.yml` has zero findings** — 25.1's workflow already passes
        these rules, which is worth recording as evidence the gate is working on our own CI.
  - [x] **Out of scope to fix — route, do not touch:** anything under `src/`, `tests/`, `extension/src/`, or
        `web/`. Epic 25 ships no product code and `GoldenContentFingerprint` must not move. A tempting
        one-line CSS fix is still an `src/` edit.
  - [x] Confirm the fingerprint is unmoved and `git status` carries no concurrent session's work.

- [x] **Task 8 — Hand off (AC: #1, #2)**
  - [x] To **Story 25.6**: whether the quality-gate badge can now ship, and what it will read (green/red today).
        25.6's AC #1 requires badges that render **green at the moment they land** — if the gate is red, say so
        plainly so 25.6 is not surprised.
  - [x] To **Story 25.5**: the coverage-number reconciliation is now explainable — Sonar reports **89.4 %**
        project-wide but **91.3 % for `src/SpecScribe`**, because `extension/src` (508 lines to cover, 0 %
        covered) and `web/` sit in the same denominator. A local C#-only report will show the higher number.
        Record this so 25.5 does not rediscover it.
  - [x] To **Story 16.2**: the required-check list, updated for anything Task 3 discovered.
  - [x] To **Story 17.5**: the file-concentration ranking from § The baseline (SiteGenerator.cs 82 issues,
        Charts.cs 76) is independent corroboration of the large-file investigation's premise.
  - [x] Re-check whether the **SonarJS blind spot** is still live (`specscribe.js` at `lines=2954` with **no
        `ncloc`** — verified still true today). It is not this story's to fix, but "no JavaScript findings"
        must not enter the triage record as "clean".

### Review Findings

Code review run 2026-07-30 against this story's own 3 commits (`a9676b2`, `55c6c1e`, `6017c2c`), scoped to its
own File List. Sibling-story hunks bundled into the same commits (18.3/18.4/18.5/20.8/23.5/25.3 in
`sprint-status.yaml`) were excluded from scope. Blind Hunter, Edge Case Hunter, and an Acceptance Auditor ran in
parallel; 7 findings survived triage (14 dismissed as noise, already-handled, or verified as non-issues).

- [x] [Review][Decision] Propose an ADR for the quality-gate/coverage/rule-decision policy choices? — RESOLVED:
      owner chose to propose one. See
      [ADR 0035 — The SonarCloud Quality Gate Is Inherited Deliberately, and Rule-Level Exceptions Have One
      Home](../../docs/adrs/0035-sonarcloud-quality-gate-and-rule-decision-policy.md) (Proposed, 2026-07-31),
      which formalizes 1a–1e and AC #3's rule-decision-home choice as a standing, owner-ratifiable record.
- [x] [Review][Patch] Stale `S6444`/`SpaDelivery.cs` security-rating driver claim in `deferred-work.md` never
      reconciled — FIXED: `deferred-work.md:1172`'s evidence line now states the corrected, ADR-0035-linked
      picture (S6444/S4036 confirmed Story 17.2's, drives project + at-times new-code security rating, but exact
      count/location is expected to move under the sliding new-code window) instead of the stale five-instance
      claim. [_bmad-output/implementation-artifacts/deferred-work.md:1172]
- [x] [Review][Patch] "The 771 INFO / 776 external-Roslyn issues are ONE [decision]" — unreconciled count
      discrepancy. FIXED: corrected `776` → `771` in `sprint-status.yaml`, consistent with every other citation
      of this figure. [_bmad-output/implementation-artifacts/sprint-status.yaml:470]
- [x] [Review][Patch] Task 7's "note that `build-test-analyze.yml` has zero findings" subtask is checked off as
      done, but that observation was never actually written into `docs/SonarCloudSetup.md` or `deferred-work.md`.
      FIXED: added to the § *Rule-level decisions* table row for `githubactions:S8233`/`S8264`. [docs/SonarCloudSetup.md]
- [x] [Review][Patch] The `permissions:` comment "`actions/deploy-pages@v5` requires all three: `pages: write`
      ... `id-token: write` ... and `contents: read`" overstates what the action itself enforces. FIXED: reworded
      to state only the two permissions the action actually requires, with `contents: read` reframed as the
      job's explicit minimal default rather than an action requirement.
      [.github/workflows/publish-docs-live-pages.yml:75]
- [x] [Review][Defer] `GoldenContentFingerprint` move attributed to a concurrent Story 20.8 session via `git
      status` plus the specific file/session named (`SiteGeneratorAdapterTests.cs`), rather than the full
      bisect-into-a-throwaway-tree procedure CLAUDE.md prescribes for proving fingerprint-move causality —
      deferred, pre-existing verification-rigor gap; low risk here because only one concurrent session's files
      intersected the fingerprint's inputs, but the lighter method should not become the default going forward.
      [_bmad-output/implementation-artifacts/25-2-quality-gate-and-findings-triage.md]
- [x] [Review][Defer] The flaky-test triage entry for `FileWatcherServiceTests.BurstOfSaves_CoalescesAndLeavesCoherentOutput`
      attributes the failure to generic machine contention without cross-referencing this project's own
      previously-diagnosed "git SPAWN starvation" preview-server flake pattern, a strong candidate explanation
      for the same load-sensitive shape — deferred, pre-existing documentation gap for whoever next investigates
      this flake. [_bmad-output/implementation-artifacts/deferred-work.md]

## Dev Notes

### Re-measure first

The project is public; none of these need a token. Run them before trusting any number in this file.

```bash
curl -s "https://sonarcloud.io/api/qualitygates/project_status?projectKey=IntegerMan_SpecScribe"
```

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=IntegerMan_SpecScribe&resolved=false&ps=1&facets=rules,types,severities"
```

```bash
curl -s "https://sonarcloud.io/api/measures/component?component=IntegerMan_SpecScribe&metricKeys=ncloc,files,coverage,duplicated_lines_density,security_rating,reliability_rating,sqale_rating,alert_status"
```

```bash
curl -s "https://sonarcloud.io/api/measures/component_tree?component=IntegerMan_SpecScribe&metricKeys=violations,ncloc,coverage&qualifiers=FIL&s=metric&metricSort=violations&asc=false&ps=20"
```

Rule names resolve with `api/rules/show?organization=integerman-github&key=<rule>` — the `organization`
parameter is **required**; omitting it returns an error, not a rule.

### § The baseline (analysis `2026-07-26T19:00:08Z`, revision `261b300`)

**Project measures**

| Metric | Value | Note |
|---|---|---|
| ncloc / files | **34,180** / **170** | was 32,788 / 149 in 25.1's record; the delta is `web/` (1,163) + `.vscode` (169) landing in `261b300` |
| Coverage | **89.4 %** | `src/SpecScribe` alone is **91.3 %**; `extension/src` is 0 % over 508 lines |
| Duplication | **0.8 %** | |
| Security hotspots | **0** | nothing to review |
| Ratings | security **C**, reliability **C**, maintainability **A** | security C is driven entirely by S6444 |
| Remediation effort | **3,630 min** (~60.5 h) | `effortTotal` on the unresolved set |
| **Gate** | **`Sonar way` (id 9) — `ERROR`** | |

**Unresolved issues: 1,360** — 1,194 code smells · **155 vulnerabilities** · **11 bugs**
Severity: INFO **754** · MINOR **351** · MAJOR **152** · CRITICAL **103** · BLOCKER **0**

**Top rules (`resolved=false`)** — the top 3 are 730 issues, 53.7 % of everything:

| Count | Rule | Name | Type / severity |
|---|---|---|---|
| 325 | `external_roslyn:CA1861` | Avoid constant arrays as arguments | smell / INFO |
| 254 | `external_roslyn:SYSLIB1045` | Convert to `GeneratedRegexAttribute` | smell / INFO |
| **151** | `csharpsquid:S6444` | **Regular expressions should be executed with a timeout** | **vulnerability / MINOR** |
| 96 | `csharpsquid:S1192` | String literals should not be duplicated | smell / MINOR |
| 87 | `csharpsquid:S3776` | Cognitive Complexity of methods too high | smell / CRITICAL |
| 58 | `external_roslyn:CA1859` | Use concrete types where possible (perf) | smell / INFO |
| 44 | `external_roslyn:CA1816` | Dispose methods should call `SuppressFinalize` | smell / INFO |
| 42 | `csharpsquid:S3358` | Ternary operators should not be nested | smell / MAJOR |
| 33 | `csharpsquid:S107` | Methods should not have too many parameters | smell / MAJOR |
| 32 | `external_roslyn:CA1822` | Mark members as static | smell / INFO |
| 26 | `csharpsquid:S3267` | Loops should be simplified with LINQ | smell / MINOR |
| 23 | `csharpsquid:S2325` | Methods not accessing instance data should be static | smell / MINOR |
| 18 | `external_roslyn:xUnit2031` | Do not use `Where` with `Assert.Single` | smell / MAJOR |

> **The INFO band is one decision, not 754.** Every INFO issue is an `external_roslyn:` import — .NET SDK
> analyzer output the scanner picks up from the build, not a SonarSource rule. 755 external-Roslyn issues,
> 754 INFO. They are the single largest bulk-disposition candidate in the set, and they are also the *only*
> band that a `.editorconfig` can suppress at source (see § Where a rule-level decision lives).

**The 155 vulnerabilities are 151 × S6444 + these four:**

| Rule | Where | What |
|---|---|---|
| `csharpsquid:S4036` × 1 | C# | Searching OS commands in PATH is security-sensitive |
| `githubactions:S8233` × 2 | `publish-docs-live-pages.yml:17,18` | Move write permission from workflow to job level |
| `githubactions:S8264` × 1 | `publish-docs-live-pages.yml:16` | Move read permission from workflow to job level |

`.github/workflows/build-test-analyze.yml` — Story 25.1's workflow — has **zero** findings.

**All 11 bugs, in full:**

| Rule | Location | Message |
|---|---|---|
| `csharpsquid:S2583` | `src/SpecScribe/SiteGenerator.cs:1392` | Condition always evaluates to `True`; some paths unreachable |
| `csharpsquid:S2583` | `src/SpecScribe/SiteGenerator.cs:2441` | Condition always evaluates to `False` |
| `csharpsquid:S2583` | `src/SpecScribe/SiteGenerator.cs:2448` | Condition always evaluates to `False` |
| `csharpsquid:S2583` | `src/SpecScribe/WorkGraph.cs:403` | Condition always evaluates to `True` |
| `csharpsquid:S2583` | `src/SpecScribe/CapabilityStyler.cs:57` | Condition always evaluates to `True` |
| `csharpsquid:S4158` | `src/SpecScribe/SiteGenerator.cs:1939` | Collection known to be empty here |
| `csharpsquid:S4158` | `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs:237` | Collection known to be empty here |
| `css:S4656` | `src/SpecScribe/assets/specscribe.css:1488` | Duplicate property `border` |
| `css:S4656` | `src/SpecScribe/assets/specscribe.css:1490` | Duplicate property `padding` |
| `css:S4656` | `src/SpecScribe/assets/specscribe.css:1856` | Duplicate property `border` |
| `typescript:S5850` | `extension/src/extension.ts:1268` | Group regex parts to make operator precedence explicit |

> **Two of these deserve a second look before being filed as noise.** `css:S4666` (not a bug, but adjacent)
> reports **`:root` duplicated at `specscribe.css:5403`, first used at line 6** — this project has already
> been bitten once by a stylesheet defect that was invisible in review and only surfaced in the live
> `cssRules` count (memory: *CSS comment `*/` silent truncation*). And `css:S4666` also flags
> **`.coverage-card` duplicated at 5810 / 4083**, which touches the vocabulary collision Epic 27 is already
> tracking. Neither is fixable here (both are `src/`), but both are worth a specific note in the routing.

**Issue concentration by file** — corroborates Story 17.5's large-file premise:

| Issues | ncloc | Coverage | File |
|---|---|---|---|
| 82 | 3,132 | 87.4 % | `src/SpecScribe/SiteGenerator.cs` |
| 76 | 3,240 | 95.6 % | `src/SpecScribe/Charts.cs` |
| 52 | 679 | 95.8 % | `src/SpecScribe/EpicsParser.cs` |
| 48 | 309 | 95.6 % | `src/SpecScribe/RenderParity.cs` — 48 issues on 309 lines is the densest file in the repo |
| 24 | 575 | 95.1 % | `src/SpecScribe/HtmlRenderAdapter.Epics.cs` |
| 12 | 923 | **0.0 %** | `extension/src/extension.ts` |

### § The coverage trap — why the gate is red, precisely

The `Sonar way` gate's six conditions and their live values:

| Condition | Threshold | Actual | Status |
|---|---|---|---|
| `new_reliability_rating` | ≤ A | A | OK |
| `new_security_rating` | ≤ A | A | OK |
| `new_maintainability_rating` | ≤ A | A | OK |
| **`new_coverage`** | **≥ 80 %** | **69.8 %** | **ERROR** |
| `new_duplicated_lines_density` | ≤ 3 % | 0.0 % | OK |
| `new_security_hotspots_reviewed` | = 100 % | 100 % | OK |

New-code period: **`days: 30`**, effective start `2026-07-25T20:54:41Z` (the first analysis). New code today is
**3,198 lines**. Of the new lines Sonar can measure coverage on:

| Scope | New lines to cover | New uncovered | Effective |
|---|---|---|---|
| `src/SpecScribe` (C#) | 348 | 24 | **93.1 %** |
| `web/**` (Nuxt, Story 23.2) | 153 | 153 | **0.0 %** |

**The C# side passes comfortably. The gate fails on JavaScript/Vue that the C#-only OpenCover report cannot
reach.** `extension/src` has the same shape (508 lines to cover, 0 % covered) but is not *new*, so it only
drags the project figure, not the gate — until the next `extension.ts` change makes it new.

Four ways out, all legitimate, none free:

1. **Add JS/TS coverage collection.** Honest, and the largest scope increase. Needs a Node test runner this
   repo does not have. Also collides head-on with Story 25.5's binding constraint — *"no second coverage
   mechanism"* — which was written about C# but reads as a general principle.
2. **`sonar.coverage.exclusions` for `web/**` and `extension/src/**`.** Keeps them in *analysis* scope
   (findings still reported) while removing them from the *coverage* denominator. Goes on the `begin` step, in
   a diff, matching 25.1's precedent for where the truth lives. Must be recorded as **"unmeasured", never
   "covered"** — 25.1's exclusion lesson was precisely that a list which looks right can be 26 % wrong.
3. **Relax or drop the `new_coverage` condition** (custom gate). Cheapest; also the one most likely to read as
   moving the goalposts. If chosen, say what replaces the assurance.
4. **Accept a red gate for now.** Defensible only if `qualitygate.wait` stays unset and Story 25.6's
   quality-gate badge is explicitly deferred — a red badge on the README is worse than none (25.6 AC #1).

Whatever you choose, **Story 25.6 inherits the consequence**, so Task 8 must state it.

### § The output format is a parsed contract

`deferred-work.md` is not a scratch file. `DeferredWorkParser.Parse` reads it and the portal renders it
(FR30 / Story 9.6). The parser's real behaviour — read from the source, not assumed:

- **Groups** come from `^## Deferred from: <label>$` headings (case-insensitive). No heading ⇒ the whole file
  falls back to unstructured plain-body rendering — i.e. one malformed heading silently degrades the surface.
- **Items** are **column-0** list markers (`-`, `*`, `+`, `1.`, `1)`). Indented lines are continuations of the
  current item. A `## ` heading inside a section flushes the item and keeps scanning.
- A bullet whose entire content is `source_spec: <token>` with no non-blank continuation is dropped as
  provenance metadata, **not** rendered as an item. Existing entries use the multi-line
  `- source_spec: … / summary: … / evidence: …` shape — match it.
- **Resolution** is detected from `~~strikethrough~~` (`<del>` in the rendered HTML) or a bracketed
  `[RESOLVED` / `**[RESOLVED`. A bare word "RESOLVED" in prose does **not** flip state — that is deliberate.
- The group's provenance key is extracted from the heading label by
  `\b(\d+-\d+-[a-z0-9-]*[a-z][a-z0-9-]*(?:\.md)?)\b` (or `story-N-M`, or `spec-*`). A bare date will **not**
  match — it requires a letter in the slug. **Put `25-2-quality-gate-and-findings-triage` in the heading** if
  you want the group to link back to this story.

**Budget: ≤ 15 items for the whole baseline triage.** 1,360 issues collapse to ~40 rules; ~40 rules collapse
to a small number of decisions. If you find yourself writing the twentieth bullet, you are transcribing rather
than triaging. State the count you landed on and why.

**Verify by rendering, not by reading.** Generate to `SpecScribeOutput/` (the default — never
`--output docs/live`) and look at the follow-up surface. CLAUDE.md § Verification: the suite structurally
cannot see a rendering defect here.

### § Where a rule-level decision lives

AC #3's "recorded as a rule-level decision, not silently re-triaged every run" has four candidate homes. Pick
one, name it, say why:

| Option | Covers | Trade-off |
|---|---|---|
| **SonarCloud quality profile** (deactivate a rule) | All rule families | Server-side. Invisible in a diff, drifts silently, and no reviewer ever sees it. This is the exact failure mode 25.1 rejected for exclusions. |
| **`.editorconfig`** (`dotnet_diagnostic.CA1861.severity = none`) | Only the **755 external Roslyn** issues — *not* `csharpsquid:` rules, which are not Roslyn diagnostics in our build | In-repo, reviewable, and also silences the build-time noise. **Does not exist today** ⇒ creating it is a new cross-cutting convention ⇒ **propose an ADR.** |
| **`/d:sonar.issue.ignore.multicriteria…` on the `begin` step** | All rule families | In the workflow file, in a diff — direct continuation of 25.1's precedent. Verbose, and it hides the issue rather than recording the reasoning; pair it with a comment. |
| **Issue-level "Won't Fix" in the UI** | Individual issues | Does not scale to 151 or 325 instances and is per-issue, not per-rule. Rejected on volume alone. |

A hybrid is reasonable (e.g. `.editorconfig` for the Roslyn band, the workflow for `csharpsquid:` rules) — but
then **both** homes must be documented in `docs/SonarCloudSetup.md`, or the next maintainer finds one and
assumes it is the only one.

### § Epic 17 routing — draft, to be verified against live data

Do not copy this blind; confirm each count with `resolved=false` before filing.

| Findings | Route to | Why |
|---|---|---|
| `S3776` cognitive complexity (87), `S107` too many params (33), `S1192` duplicated literals (96), `S3358` nested ternaries (42), `S3267` loops→LINQ (26) | **Story 17.1** — Structural and Consistency Remediation Sweep | 17.1's AC #1 already names "structural weaknesses, inconsistencies, duplication… dead or unreachable code" across the C# core, extension shim, **and stylesheet**. The 5 `S2583` unreachable-condition bugs land here too. |
| `S6444` regex timeout (151), `S4036` PATH command search (1), `githubactions:S8233`/`S8264` (3) | **Story 17.2** — Security and Privacy Hardening | These four rules are the **entire** unresolved vulnerability set — 155, verified. 17.2's AC #2 was already amended (SCP 2026-07-25) to include "the CI supply chain introduced by Epic 25"; Sonar flagging our own workflows is that clause proving itself. |
| `CA1859` concrete types (58), `CA1861` constant arrays (325), `CA1822` (32) / `S2325` (23) static members | **Story 17.3** — Performance and Efficiency Pass | All are allocation/dispatch-cost rules. `S2325` and `CA1822` are the same finding from two analyzers — route them **together, once**, or you have duplicated the thing AC #3 forbids. Note honestly that CA1861 at 325 instances is a *volume* item, not a measured hotspot — 17.3's AC #1 requires measurement before and after. |
| File concentration (SiteGenerator.cs 82, Charts.cs 76, specscribe.css duplicate `:root`/`.coverage-card`/`.now-next-card.active` selectors) | **Story 17.5** — Large-File Investigation | 17.5's AC #1 asks for "measured size… ownership hotspots… coupling risks". This is exactly that data, arriving from an independent source. |
| `SYSLIB1045` GeneratedRegex (254), `CA1816` SuppressFinalize (44), `xUnit2031` (18), the rest of the INFO band | **Bulk decision** (accept / suppress) | Not a fit for any 17.x AC. This is the band a rule-level decision exists for. |

**Tagged, not duplicated** means one `deferred-work.md` entry per routed group naming the target story — not a
copy of the same finding in that story's file *and* here.

### § What this story must NOT do

- **Must not edit `src/`, `tests/`, `extension/src/`, or `web/`.** Epic 25 ships no product code and
  `GoldenContentFingerprint` must not move. Every code fix routes to a named story. The one sanctioned
  code-adjacent fix is the 3 `githubactions:*` findings on `publish-docs-live-pages.yml` (Task 7).
- **Must not create branch protection or required checks.** That is the amended Story 16.2.
- **Must not add a README badge.** That is Story 25.6, and its AC #1 requires green-at-landing.
- **Must not add a second coverage mechanism** without confronting Story 25.5's constraint head-on.
- **Must not `git reset --hard`, `git checkout --`, or `git clean`.** A concurrent session's uncommitted work
  is routinely in this tree (CLAUDE.md). `261b300` alone bundled four sibling stories.
- **Must not treat "no JavaScript findings" as clean.** `specscribe.js` reports `lines=2954` with no `ncloc` —
  verified still true today. It is not analyzed.

### Project Structure Notes

- Modified: `.github/workflows/build-test-analyze.yml` (only if 1b/1d/rule-suppression decisions land there),
  `.github/workflows/publish-docs-live-pages.yml` (the 3 `githubactions:*` fixes),
  `docs/SonarCloudSetup.md` (the repeatable procedure + the gate correction + the rule-decision home),
  `_bmad-output/implementation-artifacts/deferred-work.md` (the triage output),
  `_bmad-output/implementation-artifacts/sprint-status.yaml` (status + any action items).
- Possibly added: `.editorconfig` (**only** with a proposed ADR), `docs/adrs/00NN-*.md`.
- Untouched: everything under `src/`, `tests/`, `extension/`, `web/`, `SpecScribe.slnx`.
- **No new visual surface** — this story ships no product code, so the create-story visual-direction
  elicitation does not apply. The one rendered artifact it touches is the existing follow-up surface fed by
  `deferred-work.md`, whose design is already set by Story 9.6.

### Testing

There is no unit test for a quality-gate decision. The evidence for this story is:

1. The live gate state before and after, from `api/qualitygates/project_status` (paste both).
2. A pull-request run showing the SonarCloud signal **visible on the PR** (AC #1's unproven half).
3. If `sonar.qualitygate.wait` is set: a run demonstrating the job goes red on a failing gate.
4. A generation into `SpecScribeOutput/` confirming the triage entries render correctly on the follow-up
   surface — not merely that the markdown looks right.
5. Full suite green, and `GoldenContentFingerprint` unmoved. If it moved, you edited `src/`.

### References

- Epic + ACs: [epics.md § Story 25.2](../planning-artifacts/epics.md) (lines 4260–4284)
- Predecessor, and the source of every inherited fact: [25-1-sonarcloud-onboarding-and-ci-analysis.md](25-1-sonarcloud-onboarding-and-ci-analysis.md) — especially § 6 *Handoff*, which names the single-rule question this story answers, and Open items item 5 (the JS blind spot)
- Requirement: NFR11 — [epics.md:256](../planning-artifacts/epics.md); FR30 provenance — [epics.md:75](../planning-artifacts/epics.md)
- Downstream: [epics.md § Story 25.6](../planning-artifacts/epics.md) (badges, gated on this story), § Story 25.5 (local coverage report — the 89.4 % vs 91.3 % reconciliation), § Story 16.2 (required checks)
- Epic 17 targets: [epics.md § Stories 17.1–17.3, 17.5](../planning-artifacts/epics.md)
- The workflow this story may modify: [build-test-analyze.yml](../../.github/workflows/build-test-analyze.yml)
- The durable Sonar documentation home: [docs/SonarCloudSetup.md](../../docs/SonarCloudSetup.md)
- The parser your output must satisfy: `src/SpecScribe/DeferredWorkParser.cs` (read it; do not infer the format)
- Origin + owner decisions: [sprint-change-proposal-2026-07-25.md](../planning-artifacts/sprint-change-proposal-2026-07-25.md)
- Working conventions (shared `main`, no destructive git, verify-after-edit, ADR triggers): [CLAUDE.md](../../CLAUDE.md)
- Live dashboard: <https://sonarcloud.io/project/overview?id=IntegerMan_SpecScribe>

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Claude Code, `dev-story` workflow), 2026-07-27.

### Debug Log References

All figures below were read from SonarCloud's public API against analysis **`2026-07-27T17:49:06Z`**. The
story's own table was measured on 2026-07-26 against `261b300`; `main` has since moved to `40c7ee9`
(*Overnight work*, *Lunch output*).

### Completion Notes List

#### 1. The baseline moved enough to invert three of this story's stated premises (Task 1)

The story asked to be re-measured before being trusted. It needed it. **The premises did not just drift — three
of them inverted.**

| Metric | Story (2026-07-26, `261b300`) | Measured (2026-07-27) | |
|---|---|---|---|
| ncloc / files | 34,180 / 170 | **46,770 / 200** | +12,590 |
| Coverage | 89.4% | **87.6%** | −1.8 |
| Duplication | 0.8% | 0.6% | |
| Ratings | security C, reliability **C**, maint. A | security C, reliability **D**, maint. A | **worse** |
| Remediation effort | 3,630 min | 2,999 min | −631 |
| Unresolved issues | 1,360 | **1,420** | +60 |
| smells / vulns / bugs | 1,194 / 155 / 11 | 1,246 / **160** / **14** | |
| Severity | INFO 754 · MIN 351 · MAJ 152 · CRIT 103 · **BLOCKER 0** | INFO 771 · MIN 370 · MAJ 167 · CRIT 111 · **BLOCKER 1** | |
| `new_lines` | 3,198 | **22,640** | **+19,442** |
| Gate | ERROR on **1** condition | ERROR on **3** conditions | **worse** |

**Inversion 1 — "The gate is red for exactly one reason, and it is not C# quality" is now false.**
Three of six conditions fail, not one:

| Condition | Threshold | Story's value | Measured | |
|---|---|---|---|---|
| `new_reliability_rating` | ≤ A | A (OK) | **D** | **now ERROR** |
| `new_security_rating` | ≤ A | A (OK) | **B** | **now ERROR** |
| `new_maintainability_rating` | ≤ A | A | A | OK |
| `new_coverage` | ≥ 80% | 69.8% (ERROR) | **59.4%** | ERROR |
| `new_duplicated_lines_density` | ≤ 3% | 0.0% | 0.0% | OK |
| `new_security_hotspots_reviewed` | = 100% | 100% | 100% | OK |

`new_reliability_rating` is **D** because of two CRITICAL `javascript:S2871` bugs — a `.sort()` with no compare
function, which sorts numbers lexicographically — at `web/scripts/check-links.mjs:204` and
`web/scripts/ir-content-build.mjs:224`. `new_security_rating` is **B** because of five new `csharpsquid:S6444`
in `src/SpecScribe/SpaDelivery.cs:190,205,246,249,252`. **Both bands are in code this story is forbidden to
touch**, which is the finding that decides 1d and the 25.6 handoff.

**Inversion 2 — the SonarJS blind spot is real but far narrower than recorded.** Story 25.1 handed forward
"SonarJS silently does NOT analyze JavaScript". Measured: SonarJS analyzes `web/**` fine and produces 45
findings there, and `extension/src` TypeScript has always analyzed. The blind spot is **one file** —
`src/SpecScribe/assets/specscribe.js`, `lines=2464`, **no `ncloc`**, 0 violations. So "no findings in `web/`"
means clean; "no findings in `specscribe.js`" means not analyzed. Corrected in `docs/SonarCloudSetup.md`.

**Inversion 3 — the `resolved=false` gap widened rather than closed.** 1,598 unfiltered vs **1,420**
unresolved = **178** phantom issues, up from 148. Triaging the default response would now seed ~178 items
pointing at files Sonar no longer analyzes.

Two further corrections to the story's own tables, recorded rather than quietly re-baselined:

- **The 11 bugs are 14**, and their line numbers moved. `HtmlRenderAdapter.Dashboard.cs:237 → :235`; the three
  `css:S4656` at `1488/1490/1856 → 1596/1598/1964`. The three new ones are all in `web/`.
- **`Sonar way` is not the only gate in the org.** A second, non-default gate named **`Customized` (id 4194)**
  exists (`new_coverage ≥ 30`, `new_duplicated_lines_density ≤ 8`), is **not applied** to this project, and is
  documented nowhere. The story's 1a framing assumed a choice between `Sonar way` and something we would
  create; there was already a stray third option nobody had recorded.

#### 2. The gate decision — 1a–1e (AC #1)

**1a — `Sonar way` (id 9), kept deliberately.** Rejected a project-specific gate, and rejected adopting the
stray `Customized` gate. The reason is 1e: gate conditions are server-side objects that no diff shows and no
reviewer sees. `Customized` **is that failure mode already realised in this org** — a gate someone made, nobody
applied, and nobody wrote down. Minting a second one would be repeating it. Where the gate's inputs *can* live
in a diff (exclusions, coverage exclusions, rule suppressions, `qualitygate.wait`) they do, in the workflow.

**1b — `new_coverage`: fix the input, not the threshold.** Chose `sonar.coverage.exclusions` over relaxing the
condition. Quantified at decision time: removing `web/**` (918 new lines to cover, **918 uncovered**) left
1,124 C# lines to cover with 44 uncovered — **94.9%**, clearing 80% with 15 points of headroom. Recorded as
**unmeasured, never covered**. `extension/src/**` was deliberately **not** excluded (508 lines to cover, 0%)
because it is shipped first-party product code whose 0% this project wants visible, with the accepted
consequence that its next change turns the gate red — correctly.

> **This decision was superseded within hours by a concurrent session, and the supersession is better.** See
> §5. The reasoning survives; the setting did not.

**1c — new-code period: keep `days: 30`, with the defect named.** The evidence for changing it is strong —
`new_lines` went **3,198 → 22,640 in one day** as the sliding window swallowed whole epics, so the new-code
conditions are currently behaving as whole-project conditions. Both alternatives cost more than they are worth
*today*: `previous_version` needs `sonar.projectVersion` wired to the build's informational version and means
"new since the last release" for a project that has never released; a reference branch is degenerate when the
analyzed branch *is* `main`. **Trigger recorded: adopt `previous_version` at the first release tag (Epic 16)**,
filed as an owner action item rather than left in prose.

**1d — `sonar.qualitygate.wait` stays unset.** This is the literal answer to "what a failing gate blocks":
**nothing**. Setting it today would turn every push to `main` red on the `web/` bugs and `src/` vulnerabilities
above — code Epic 25 must not touch — and would break CI for concurrent sessions mid-epic. Three preconditions
are written into `docs/SonarCloudSetup.md` and into a sprint action item with an owner. What makes the signal
actionable meanwhile is PR decoration (§3), not a red CI job. **No `qualitygate.wait` demonstration run is
claimed, because it was not set.**

**1e — where the gate definition lives.** Server-side, and this breaks 25.1's "the truth lives in a diff"
precedent. Stated plainly rather than glossed. Mitigations actually applied: the six conditions are
**transcribed verbatim** into `docs/SonarCloudSetup.md` with the two `curl` commands that re-verify them, and
the stray `Customized` gate is documented there so a future reader does not assume it is live.

#### 3. PR decoration — OBSERVED, on both channels (AC #1, Task 3)

**Decoration works, and has now been seen on this repository for the first time.** Opened
[PR #3](https://github.com/IntegerMan/SpecScribe/pull/3) from a same-repo branch so the token is present
(run [`30298742218`](https://github.com/IntegerMan/SpecScribe/actions/runs/30298742218), `build-test-analyze`
pass in 3m46s). SonarCloud contributed **both** forms:

1. **A check run** — name **`SonarCloud Code Analysis`**, app slug `sonarqubecloud`, conclusion `success`,
   linking to `https://sonarcloud.io/dashboard?id=IntegerMan_SpecScribe&pullRequest=3`.
2. **A PR comment** from `sonarqubecloud[bot]` — "Quality Gate passed" with the issue/coverage summary.

**No `permissions:` change was needed.** The workflow's `contents: read` was sufficient, which confirms Story
25.1's stated reasoning rather than merely repeating it: decoration is performed by the SonarQube Cloud GitHub
App under its own installation token, not by `GITHUB_TOKEN`. The `permissions:` block was the first suspect
and is exonerated.

**A finding that matters more than the decoration itself: the PR gate and the branch gate are different
objects, and the PR gate is the more forgiving one.** The branch gate on `main` is `ERROR` on three conditions.
The gate on PR #3 returned **`OK`** — and it evaluated only **five** conditions, because **`new_coverage` was
absent entirely**. SonarCloud drops that condition when a pull request contributes no new lines to cover, which
this documentation-and-YAML PR does not.

The consequences are not cosmetic:

- **A green PR check does not mean the project gate is green.** Anyone reading PR #3's tick as "Sonar is happy"
  would be wrong about `main`.
- **Story 25.6's quality-gate badge reads the branch status, not a PR status**, so it would render **red**
  today regardless of how green PRs look. Reinforces the §7 handoff.
- **Story 16.2 now has a second candidate required check** — see the corrected handoff in §7.

#### 4. The baseline triage — rule-first (AC #2, #3)

**1,420 unresolved issues → ~40 rules → 12 decisions.** (The `deferred-work.md` group carries 13 items: these 12 plus one non-Sonar flaky-test finding from the verification run — see Verification #5.) The top three rules alone are 746 issues (52.5%).

Materiality bar, written down so a future pass can match or deliberately change it: every bug decided
individually; every vulnerability rule decided; every rule with ≥ 20 unresolved issues decided as a rule; the
INFO band decided once.

**The 14 bugs, each with a named decision** — no volume excuse, as the story required:

| Rule | Location | Decision |
|---|---|---|
| `csharpsquid:S2583` ×5 | `SiteGenerator.cs:1392,2441,2448`, `WorkGraph.cs:403`, `CapabilityStyler.cs:57` | → **Story 17.1**. Always-constant conditions with unreachable paths; this is the shape of defect this project has shipped before and caught only in a browser. |
| `csharpsquid:S4158` ×2 | `SiteGenerator.cs:1939`, `HtmlRenderAdapter.Dashboard.cs:235` | → **Story 17.1**. Collection known empty at use. |
| `css:S4656` ×3 | `specscribe.css:1596,1598,1964` | → **Story 17.1**. Duplicate `border` / `padding`. |
| `css:S4656` ×1 | `web/assets/ir-content.css:1191` | → **Epic 23**. |
| `javascript:S2871` ×2 | `web/scripts/check-links.mjs:204`, `ir-content-build.mjs:224` | → **Epic 23**. **CRITICAL, and the sole reason reliability is D.** Precondition 2 for 1d. |
| `typescript:S5850` ×1 | `extension/src/extension.ts:1268` | → **Story 17.1**. Regex precedence — silently matches the wrong thing rather than failing loudly. |

**`csharpsquid:S6444` (156 of 160 vulnerabilities) — one explicit decision: scheduled to Story 17.2, not
accepted, not suppressed.** The story flagged that MINOR severity and "drives security rating C" pull in
opposite directions and asked which won. **Severity lost.** SpecScribe parses markdown, epics, and sprint files
from arbitrary third-party repositories, so catastrophic backtracking is an input-driven surface, not a
theoretical one — the MINOR label reflects the rule's generic prior, not this codebase's exposure. Suppressing
it would also have removed the only standing signal that the surface exists.

**The 771 INFO / 776 external-Roslyn issues — one decision, not 771: accepted for now, not suppressed.**
Deliberately *not* suppressed, because suppression would destroy the before-measurement Story 17.3's AC #1
requires. Noted: `SYSLIB1045` (264) and `S6444` (156) describe the same construction sites from opposite
angles, so 17.2's regex sweep will likely close much of `SYSLIB1045` for free — another reason not to suppress
first.

**Epic 17 routing, verified against live data rather than copied.** The story's draft table was close but its
counts were all stale; every number below was re-pulled with `resolved=false`. Two corrections to the draft:

- `S2325` (23) and `CA1822` (32) are the **same finding from two analyzers** and are routed **together, once**
  — the draft flagged this and it held up.
- `RenderParity.cs` (48 issues on 309 ncloc) is the **densest file in the repository**, roughly one finding
  every six lines. It is not a large file and was not on Story 17.5's radar. Genuinely new information.

**The one BLOCKER is worth pulling forward.** `csharpsquid:S2699` at `tests/SpecScribe.Tests/ChartsTests.cs:340`
— a test with no assertion. It appeared between 2026-07-26 (0 BLOCKERs) and 2026-07-27 (1), so it arrived with
concurrent work. A test that cannot fail is worse than an absent one: it inflates both the 2,537-test pass count
and the coverage figure while asserting nothing.

#### 5. A decision of this story was superseded mid-session, and the supersession is better (§1b, Task 5/7)

While this story ran, a concurrent session implementing **Story 23.5** rewrote `build-test-analyze.yml` on top
of the 1b change. It **narrowed** `sonar.coverage.exclusions` from the blanket `web/**` to
`web/scripts/**,web/server/plugins/**,web/**/*.vue`, and supplied the report whose absence was the entire
justification for the blanket form: Vitest under `web/` plus `sonar.javascript.lcov.reportPaths`.

**That is the right answer and it was not reverted.** 1b's reasoning was that a coverage exclusion is a
workaround for a *missing report* — the correct resolution to which is to supply the report, which is exactly
what 23.5 did. Three artifacts were reconciled to the new reality rather than left asserting superseded
figures: `docs/SonarCloudSetup.md` § *Coverage exclusions*, the coverage entry in `deferred-work.md`, and
precondition 1 of the `qualitygate.wait` action item.

**Post-merge measurement (added after `main` reached `b86fc27`, the first analysis with the full C# + JS
coverage path actually running): the supersession did not just preserve honesty, it fixed the condition.**
`new_coverage` went **59.4% → 89.3%**, comfortably clearing 80%. Excluding `web/scripts/**` removed 743 of the
918 uncovered lines and left a denominator of genuinely testable code. **Precondition 1 for
`sonar.qualitygate.wait` is now met**, and the two that remain are both in `web/scripts/**` and both Epic 23's
— see §7.

**Two consequences that must not be lost:**

- **The `87.6% → 91.4%` projection this story would have produced never happens**, and the real outcome is
  better than either number implied: `web/` is *measured* rather than hidden, and the gate condition passes on
  the strength of real tests instead of a removed denominator.
- **`build-test-analyze.yml` is deliberately left uncommitted**, for Story 23.5 to land with its own untracked
  files (`web/.nvmrc`, `web/vitest.config.ts`, `web/test/`). Committing the workflow alone would have shipped a
  `node-version-file: web/.nvmrc` pointing at a file not in the commit — a guaranteed CI break. 25.2's
  surviving contribution to that file is the header-comment block recording the gate decision.

#### 6. The rule-level decision home (AC #3, Task 6)

**Chosen: `docs/SonarCloudSetup.md` § *Rule-level decisions* as the record, plus
`/d:sonar.issue.ignore.multicriteria` on the `begin` step as the enforcement mechanism.** One home, in-repo,
in-diff, reviewable, covering every rule family, with zero effect on the build.

**`.editorconfig` was rejected, so no ADR is required.** The story asked for an ADR *if* a new in-repo
convention was created. Three reasons it was not: it **cannot reach `csharpsquid:` / `css:` / `javascript:`
rules at all** — only the `external_roslyn:` band — so it could never be the *single* home and would guarantee
two places to look, which is the failure AC #3 exists to prevent; it changes local and CI **build** warning
behaviour for `src/` and `tests/`, which Epic 25 must not touch; and its only advantage over the workflow file
is build-time noise reduction nobody asked for. (Confirmed: neither `.editorconfig` nor `Directory.Build.props`
exists in this repo.)

**The mechanism is deliberately applied to zero rules today**, and that is recorded as a decision with its
reason, not left as an omission: every rule in the current set is either routed to a named Epic 17 story —
where suppressing it hides scheduled work from the dashboard meant to prove it done — or is INFO-band external
Roslyn, whose disposition depends on the very measurement suppression would destroy.

#### 7. Handoffs (Task 8)

**→ Story 25.6 (badges).** **The quality-gate badge still cannot ship, but it is now two fixes away rather
than three.** Re-measured on `main` at `b86fc27`: the gate is still `ERROR`, but only **two** conditions fail,
and `new_coverage` has flipped to **passing at 89.3%**. What remains:

| Condition | State | Driver | Owner |
|---|---|---|---|
| `new_coverage` | ✅ **89.3%** vs 80% | fixed by Story 23.5's real JS coverage | — |
| `new_reliability_rating` | ❌ **D** | 1 CRITICAL `javascript:S2871`, `web/scripts/check-links.mjs:204` | Epic 23 |
| `new_security_rating` | ❌ **C** | 2 MAJOR `jssecurity:S8707`/`S8705`, `web/scripts/experiment-two-ir.mjs:95` | Epic 23 |

25.6's AC #1 requires badges green at the moment they land, so the quality-gate badge is **blocked** until
Epic 23 clears three findings in two files. The build and coverage badges are unaffected and may ship alone.
**Note the ownership shift:** the blockers are no longer Story 17.2's `csharpsquid:S6444` band — that still
drives the *project-level* security rating, but no longer the *new-code* one.

**→ Story 25.5 (local coverage report).** The two-numbers-that-disagree problem is now a *three*-number problem,
and all three are explainable: coverlet/OpenCover reports ~89.8% (its own formula over C# assemblies); Sonar
reports **91.4% for `src/SpecScribe`**; Sonar reports **87.6% project-wide**, because `extension/src` (508
lines to cover, 0%) and `web/` sit in the same denominator. A local C#-only report will show the highest of the
three. Also: 25.5's binding constraint of *no second coverage mechanism* now needs restating — Story 23.5 added
a genuine second mechanism (Vitest/lcov). It does not violate the constraint's intent, because it covers a
*different language* that the first mechanism structurally cannot reach, but 25.5 must say so rather than
discover it.

**→ Story 16.2 (required checks). CORRECTED — there are now two candidate checks, not one.** Task 3 measured
this rather than assuming it: SonarCloud contributes a check run named **`SonarCloud Code Analysis`**
(app `sonarqubecloud`), alongside `build-test-analyze`. 16.2 must decide deliberately between them:

- **`build-test-analyze`** — required. Unchanged from 25.1.
- **`SonarCloud Code Analysis`** — **recommend NOT requiring it yet.** It is contributed by a third-party App
  under its own token, so it will simply never appear on pull requests from forks, and a required check that
  cannot appear blocks the PR forever. It is also evaluated against a **reduced condition set** (§3:
  `new_coverage` is dropped when a PR adds no coverable lines), so requiring it would assert less than it
  appears to.
- **`portability-probe (ubuntu, non-gating)`** — must **not** be required. It is failing today, by design.

**→ Story 17.5 (large-file investigation).** Independently corroborated, with one addition: `SiteGenerator.cs`
82 issues / 3,143 ncloc, `Charts.cs` 58 / 2,812, `EpicsParser.cs` 52 / 679, and **`RenderParity.cs` 48 / 309**
— the last being the highest issue density in the repo and not previously on 17.5's radar.

**→ Epic 23.** Owns 45 `web/` findings including the two CRITICAL bugs holding reliability at D, and owns the
`.vue` component-test gap that now keeps `new_coverage` red.

**SonarJS blind spot: still live, and re-scoped.** `specscribe.js` at `lines=2464` with no `ncloc` — about 2,464
lines of hand-authored first-party behaviour with zero static analysis over it. Unowned by any story. It must
never enter a triage record as "clean". See Inversion 2 above for the correction to its stated scope.

<!-- Required by the ACs — do not mark this story done without all seven:
     1. The gate decision: 1a-1e each answered with its reason (AC #1)
     2. PR decoration OBSERVED on a real tokened PR, or the reason it is absent and what was changed (AC #1)
     3. The repeatable triage procedure, written where a future session will find it (AC #2)
     4. The baseline triage result, with today's re-measured numbers and any delta from this story's table (AC #2)
     5. Individual decisions for the 11 bugs and for S6444 (AC #2)
     6. The Epic 17 routing, verified against live data, tagged not duplicated (AC #3)
     7. The rule-level decision home, named — plus a proposed ADR if it is a new in-repo convention (AC #3)
-->

### File List

Committed on branch `story/25-2-quality-gate` as `a9676b2` → [PR #3](https://github.com/IntegerMan/SpecScribe/pull/3).

**Modified**

- `.github/workflows/publish-docs-live-pages.yml` — the only sanctioned code-adjacent fix: workflow-level
  `permissions:` moved to job level, closing all 3 `githubactions:S8233` / `S8264` findings.
- `docs/SonarCloudSetup.md` — § *Coverage exclusions* (new), § *Known gap* (corrected and re-scoped),
  § *Quality gate* (new; replaces the misleading "no quality gate is enforced"), § *Triaging findings* (new —
  the repeatable procedure required by AC #2), § *Rule-level decisions* (new — the home required by AC #3).
- `_bmad-output/implementation-artifacts/deferred-work.md` — the 13-item baseline triage group (12 Sonar
  decisions + 1 flaky-test finding from the verification run).
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story status, plus 3 Epic 25 action items.
  **Bundles concurrent sibling work**: create-story records for 18.3 / 18.4 / 23.5 were already in this file.
- `_bmad-output/implementation-artifacts/25-2-quality-gate-and-findings-triage.md` — this record.

**Deliberately NOT committed**

- `.github/workflows/build-test-analyze.yml` — carries this story's gate-decision header comment, but its
  working-tree content is Story 23.5's in-flight work (Setup Node, npm steps, the narrowed
  `sonar.coverage.exclusions`, `sonar.javascript.lcov.reportPaths`). Committing it alone would ship a
  `node-version-file: web/.nvmrc` pointing at a file not in the commit. Left for 23.5 to land. See
  Completion Note §5.

**Untouched, as required** — `src/`, `tests/`, `extension/`, `web/`, `SpecScribe.slnx`, `epics.md`. No triage
item amended an Epic 17 story's scope, so `epics.md` did not need to move.

### Verification

1. **Live gate state, before** — 3 of 6 conditions ERROR (`new_reliability_rating` D, `new_security_rating` B,
   `new_coverage` 59.4%), `alert_status` `ERROR`, analysis `2026-07-27T17:49:06Z`. Full table in Completion
   Note §1. **After: unchanged, and expected to be** — the two non-coverage failures are driven by `web/` and
   `src/` code this story is forbidden to touch, so no gate-state change is claimed.
2. **PR decoration — OBSERVED, not asserted.** [PR #3](https://github.com/IntegerMan/SpecScribe/pull/3), run
   `30298742218`. Check run `SonarCloud Code Analysis` (app `sonarqubecloud`) → `success`, plus a
   `sonarqubecloud[bot]` comment. No `permissions:` change was needed. Full detail, including the PR-gate vs
   branch-gate divergence, in Completion Note §3.
3. **`sonar.qualitygate.wait` demonstration** — **not applicable, and not claimed.** It was not set (1d).
4. **Rendered-output check, not a markdown re-read** — generated 422 pages into `SpecScribeOutput/`
   (`errors=0`), then opened the follow-up surface in a browser. All 13 deferred items parsed as **items**
   (none dropped as bare `source_spec:` metadata), each got its own detail page, the group provenance key
   resolved to `[25-2-quality-gate-and-findings-triage]`, and all 13 + the 3 action items aggregate correctly
   onto `follow-ups/group-epic-25.html` as OPEN.
5. **Full suite green** — **2,538 passed, 0 failed, 3 skipped** (2,541 total) on a clean run.
   **One intermediate run failed and was root-caused, not waved through**:
   `FileWatcherServiceTests.BurstOfSaves_CoalescesAndLeavesCoherentOutput` failed on a run that had a portal
   generation, a browser, and a concurrent agent build alongside it. It then passed **3 of 3 in isolation**,
   and the next clean full run was green. It is a load-sensitive timing test — a 400 ms
   `ForgeOptions.DebounceInterval` polled against a 20 s `SettleTimeout` — not a regression, and this story
   touched no `src/` or `tests/` file. Filed as a deferred item rather than dismissed, because **Story 16.2 is
   about to make this suite a required status check**, and it is the second flake of this shape in Epic 25.
   **`GoldenContentFingerprint` moved, and not by this story**: a concurrent Story 20.8 session regenerated it
   `126eed3a…` → `3171cf5c…` in `SiteGeneratorAdapterTests.cs`. `git status` confirms this story touched no
   file under `src/`, `tests/`, `extension/`, or `web/`. This is the drift CLAUDE.md § *Concurrent work*
   anticipates, recorded rather than reset.

### Change Log

| Date | Change |
|---|---|
| 2026-07-27 | Re-measured the baseline. **Three of the story's premises inverted** — the gate now fails 3 of 6 conditions (not 1), the 11 bugs are 14 with moved line numbers, and the SonarJS blind spot is one file rather than the whole language. Recorded rather than quietly re-baselined. |
| 2026-07-27 | Gate decided (1a–1e): keep built-in `Sonar way` (id 9); `sonar.qualitygate.wait` stays unset with 3 written preconditions; conditions transcribed into `docs/SonarCloudSetup.md`; the org's stray unapplied `Customized` gate (id 4194) documented and filed as an owner action. |
| 2026-07-27 | Baseline triage performed rule-first: 1,420 unresolved issues → ~40 rules → **12 decisions** (budget was ≤ 15; the group carries 13 items, the extra being a non-Sonar flaky-test finding), each routed once to 17.1 / 17.2 / 17.3 / 17.5 / Epic 23. All 14 bugs decided individually; `S6444` (156) and the 771-issue INFO band each decided once. |
| 2026-07-27 | Rule-level decision home named: `docs/SonarCloudSetup.md` + `sonar.issue.ignore.multicriteria`, applied to zero rules today with the reason recorded. `.editorconfig` rejected — **no ADR required**. |
| 2026-07-27 | Fixed the 3 `githubactions:*` findings on `publish-docs-live-pages.yml` (permissions → job level). |
| 2026-07-27 | Reconciled §1b after Story 23.5 superseded it mid-session with a better answer (real Vitest coverage for `web/` instead of a blanket exclusion). Three artifacts corrected rather than left asserting stale figures. |
| 2026-07-27 | **PR decoration observed for the first time on this repository** (PR #3): check run `SonarCloud Code Analysis` + a `sonarqubecloud[bot]` comment, with **no `permissions:` change needed**. Discovered that the PR gate evaluates a *reduced* condition set — it passed with `new_coverage` dropped while `main` was red on three conditions. |
| 2026-07-27 | Handoffs recorded for 25.6 (**quality-gate badge blocked, not deferred**), 25.5 (three coverage numbers, all explained), 16.2 (required-check list unchanged), 17.5 (`RenderParity.cs` is the densest file in the repo — new information). |
