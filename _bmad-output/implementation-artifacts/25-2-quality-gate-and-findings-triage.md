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

Status: ready-for-dev

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

- [ ] **Task 1 — Re-measure the baseline before deciding anything (AC: #2)**
  - [ ] Run the four commands in § Re-measure first and record today's numbers. **Every figure in this story is
        from 2026-07-26 and `main` moves fast** — a concurrent session landed 20.5/20.7/22.2/23.2 in `261b300`
        and moved ncloc 32,788 → 34,180 between 25.1's record and this one.
  - [ ] Confirm the delta against this story's § The baseline table and note anything that moved materially.
        If a number here is wrong, say so in the record — do not quietly re-baseline.
  - [ ] Confirm the `resolved=false` gap is still real (unresolved total vs. unfiltered total). If it has
        closed, say so; if it has widened, that is a finding.

- [ ] **Task 2 — Decide the gate (AC: #1)**
  - [ ] Read the live gate state: `api/qualitygates/project_status` and `api/qualitygates/get_by_project`.
        Record the applied gate's name/id and every condition with its current actual value.
  - [ ] Settle 1a–1e from AC #1's table. Each needs one paragraph: what you chose and what you rejected.
  - [ ] For **1b** specifically: quantify the option you pick against today's numbers (new C# 348 LTC / 24
        uncovered; new `web/` 153 LTC / 153 uncovered). A `sonar.coverage.exclusions` answer goes on the
        `begin` step in the workflow — in a diff, matching 25.1's precedent — and must state plainly that
        `web/` and `extension/src` are then **unmeasured, not covered**.
  - [ ] For **1d**: if you set `sonar.qualitygate.wait`, prove the job goes red on a failing gate the same way
        25.1 proved red-on-test-failure — a real run, not an assertion. If you do not set it, say what makes
        the signal actionable without it.
  - [ ] Update `docs/SonarCloudSetup.md` § *Quality gate* — it currently says *"No quality gate is enforced by
        this workflow"*, which is true about `qualitygate.wait` and **misleading about the gate**, since
        `Sonar way` is evaluating and failing today. Correct it either way.

- [ ] **Task 3 — Prove PR decoration, or find out why it is absent (AC: #1)**
  - [ ] Open a real pull request against `main` from a same-repo branch (so the token is present) and record
        whether the SonarQube Cloud check and/or comment appears.
  - [ ] If it does not appear: check the SonarQube Cloud GitHub App installation on `IntegerMan/SpecScribe`,
        then the workflow `permissions:` block (`contents: read` only, today). Record what fixed it.
  - [ ] Record the check name(s) SonarCloud contributes, so **Story 16.2** knows whether it has one required
        check (`build-test-analyze`) or two.

- [ ] **Task 4 — Perform the baseline triage, rule-first (AC: #2, #3)**
  - [ ] Pull the rule facet with `resolved=false` and work top-down by count. § The baseline has the current
        ranking.
  - [ ] For each rule reaching your materiality bar, record: rule id, name, count, severity, whether it is a
        SonarSource rule (`csharpsquid:`/`css:`/`typescript:`…) or an **external Roslyn** import
        (`external_roslyn:`), and the decision — fixed / scheduled to story N / accepted with rationale.
  - [ ] Give the **11 bugs** individual decisions. They are listed in § The baseline with file and line.
  - [ ] Give `csharpsquid:S6444` (151 of 155 vulnerabilities) a single explicit decision. Note that it is
        **MINOR** severity and drives the project's **security rating C** — those two facts pull in opposite
        directions and the record should say which won.
  - [ ] State the disposition of the **755 external Roslyn / INFO** issues as one decision with its reasoning,
        not 755 items.

- [ ] **Task 5 — Write the triage output in the format SpecScribe itself parses (AC: #2, #3)**
  - [ ] Follow § The output format is a parsed contract exactly. Verify by running a generation into
        `SpecScribeOutput/` and looking at the rendered follow-up surface — **not** by re-reading the markdown.
  - [ ] Keep to the budget in that section. If you exceed it, say why in the record.
  - [ ] Add `sprint-status.yaml` action items only for items that need a *person* to act, following the
        existing `- epic: / action: / owner: / status:` shape at `action_items:`. Findings scheduled into
        Epic 17 stories belong in `deferred-work.md`, not duplicated here (AC #3).

- [ ] **Task 6 — Record the rule-level decision home (AC: #3)**
  - [ ] Choose from § Where a rule-level decision lives and record why.
  - [ ] If the choice is a new `.editorconfig` / `Directory.Build.props`: **propose an ADR** and do not bury
        the decision in this story file (CLAUDE.md § Decision records). Neither file exists today — verified.
  - [ ] Whatever the choice, add it to `docs/SonarCloudSetup.md` so the next maintainer finds it without
        reading this story.

- [ ] **Task 7 — Fix only what is in scope, and route everything else (AC: #2)**
  - [ ] In scope to actually fix: the **3 `githubactions:*` findings on
        `.github/workflows/publish-docs-live-pages.yml`** (workflow-level permissions that should be
        job-level). Note that **`build-test-analyze.yml` has zero findings** — 25.1's workflow already passes
        these rules, which is worth recording as evidence the gate is working on our own CI.
  - [ ] **Out of scope to fix — route, do not touch:** anything under `src/`, `tests/`, `extension/src/`, or
        `web/`. Epic 25 ships no product code and `GoldenContentFingerprint` must not move. A tempting
        one-line CSS fix is still an `src/` edit.
  - [ ] Confirm the fingerprint is unmoved and `git status` carries no concurrent session's work.

- [ ] **Task 8 — Hand off (AC: #1, #2)**
  - [ ] To **Story 25.6**: whether the quality-gate badge can now ship, and what it will read (green/red today).
        25.6's AC #1 requires badges that render **green at the moment they land** — if the gate is red, say so
        plainly so 25.6 is not surprised.
  - [ ] To **Story 25.5**: the coverage-number reconciliation is now explainable — Sonar reports **89.4 %**
        project-wide but **91.3 % for `src/SpecScribe`**, because `extension/src` (508 lines to cover, 0 %
        covered) and `web/` sit in the same denominator. A local C#-only report will show the higher number.
        Record this so 25.5 does not rediscover it.
  - [ ] To **Story 16.2**: the required-check list, updated for anything Task 3 discovered.
  - [ ] To **Story 17.5**: the file-concentration ranking from § The baseline (SiteGenerator.cs 82 issues,
        Charts.cs 76) is independent corroboration of the large-file investigation's premise.
  - [ ] Re-check whether the **SonarJS blind spot** is still live (`specscribe.js` at `lines=2954` with **no
        `ncloc`** — verified still true today). It is not this story's to fix, but "no JavaScript findings"
        must not enter the triage record as "clean".

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

### Debug Log References

### Completion Notes List

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

### Change Log

| Date | Change |
|---|---|
