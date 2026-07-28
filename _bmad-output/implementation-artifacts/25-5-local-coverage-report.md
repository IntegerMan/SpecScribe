---
baseline_commit: 755bd7a8d1679594dc48bb04fe5ac11473484618 # `755bd7a` — HEAD at authoring time (2026-07-28)
epic: 25
nfr: [NFR11]
frs: []
depends_on: [25-1] # the coverlet.collector -> OpenCover path and the CI figure this reconciles against
blocks: [25-6] # the coverage badge must show the SAME figure CI reports; this story establishes which figure that is
ships_product_code: false # dev-time only. The golden fingerprint MUST NOT move. No `src/` edits.
adrs: [] # no architectural decision — this is tooling, not a contract
touches:
  - ".config/dotnet-tools.json" # NEW — pins ReportGenerator
  - "tools/coverage/**" # NEW — the one documented command
  - "README.md" # § Development — the command, alongside the existing `dotnet test` guidance
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
# NOT src/**, NOT tests/**, NOT extension/src/**, NOT web/**, NOT .github/workflows/**
---

# Story 25.5: A Local, Browsable Coverage Report in One Command

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the SpecScribe maintainer,
I want to produce a browsable coverage report locally with a single documented command,
So that I can find untested code while I am working, without pushing a commit and opening SonarCloud to see it.

## ⛔ Read first — six live facts, two of which invert what the upstream stories wrote down

### 1. ⚠ The "89.8%" in AC #2 is a **SonarCloud** figure, not a coverlet figure

The epic AC says *"CI already measures coverage at 89.8%"*, and Story 25.2's handoff describes it as
*"coverlet/OpenCover reports ~89.8% (its own formula over C# assemblies)"*. **That attribution is wrong.**
89.8% came from Story 25.1's SonarCloud measures table (`25-1-…md`, § *After widening*, row `Coverage`) — it is
SonarCloud's `coverage` metric, read from `api/measures/component`. **No coverlet-computed percentage has ever
been recorded in this project.** Do not spend a round hunting for one, and do not treat 89.8% as a target the
local report should hit.

### 2. ⚠ Sonar's `coverage` metric is **not line coverage** — this is the whole reconciliation

SonarCloud's headline `coverage` blends line *and* branch:

```
coverage = (covered_lines + covered_conditions) / (lines_to_cover + conditions_to_cover)
```

Verified against the live API for `src/SpecScribe` on 2026-07-28:

```
(24653 − 1498) + (12102 − 1754)     23155 + 10348     33503
───────────────────────────────  =  ─────────────  =  ─────  =  91.15%   → Sonar reports 91.2
     24653 + 12102                      36755          36755
```

ReportGenerator reports **line coverage and branch coverage as two separate figures** and does not compute
Sonar's blend. So the apples-to-apples comparison is:

| Local (ReportGenerator) | Compare against | Live value 2026-07-28 |
|---|---|---|
| Line coverage | Sonar `line_coverage` for `src/SpecScribe` | **93.9%** |
| Branch coverage | Sonar `branch_coverage` for `src/SpecScribe` | **85.5%** |
| *(not produced)* | Sonar `coverage` for `src/SpecScribe` | 91.2% |
| *(not produced)* | Sonar `coverage` project-wide | 89.7% |

**A reconciliation that compares a ReportGenerator line-coverage % against Sonar's `coverage` % is comparing
two different formulas and will "discover" a discrepancy that is arithmetic, not disagreement.**

### 3. ⚠ The numbers moved again — re-measure before citing anything

| Figure | 25.1 recorded | 25.2 recorded | **Live 2026-07-28** |
|---|---|---|---|
| Project `coverage` | 89.8% | 87.6% | **89.7%** |
| `src/SpecScribe` `coverage` | — | 91.4% | **91.2%** |
| `alert_status` | — | ERROR | **ERROR** |

The project is **public and every endpoint below answers anonymously** — no token, no `gh auth`. Re-run them
in Task 1 and cite today's values, not this table's.

### 4. ⚠ Stale `TestResults/<guid>/` dirs are a silent wrong-number trap

Every `dotnet test --collect:"XPlat Code Coverage"` run writes a **new GUID-named directory**. A ReportGenerator
glob over `**/TestResults/**/coverage.opencover.xml` therefore **merges every historical run** — including runs
from a different commit, a partially-failing run, or a concurrent session's. The merged number is wrong and
looks plausible. **The command must clean its raw directory before every run** (Task 3). This is not
defensiveness; it is the difference between a report and a lie.

### 5. ⚠ The local suite is flaky, and 25.1's local measurement was **invalidated** by exactly this

`GitMetrics.cs:259` sets `private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3)` on every git
subprocess. A cold deep-git read has been measured at **6,496 ms against that 3,000 ms budget**, and Story 25.1
recorded **9 / 3 / 1 / 18 failures across four consecutive identical local runs** — a flake that does **not**
reproduce in CI (CI: 2,394 passed / 0 failed / 0 skipped). Consequences for AC #2's "measured cost":

- **Take the timing across repeated runs and report the spread**, never a single sample. 25.1's single-sample
  local read produced a fictional "+47.5%" that had to be retracted.
- **A red suite still emits a coverage report.** coverlet writes the OpenCover file regardless of test outcome,
  so a failing run yields a *lower, plausible-looking* percentage. Record the pass/fail counts alongside every
  percentage you cite, and do not reconcile a number taken from a failing run.

### 6. ⚠ A concurrent session is editing `src/` right now

At authoring time `git status` carried **7 modified files under `src/`** (`DeepAnalyticsTemplater.cs`,
`EpicsParser.cs`, `GitInsightsTemplater.cs`, `HierarchyExplorer.cs`, `RetroTemplater.cs`, `StatusStyles.cs`,
`assets/specscribe.css`) plus an untracked `tools/analysis-digest/` (Story 25.4's in-flight emitter). Therefore:

- **AC #3's "`GoldenContentFingerprint` is unmoved" means unmoved *by this story*** — not unmoved in absolute
  terms. Prove it by scope (this story writes no file under `src/` or `tests/`), not by asserting a hash you
  cannot own. See CLAUDE.md § *Concurrent work on shared `main`*.
- **Never `git reset --hard`, `git checkout --`, or `git clean`** to tidy the tree, including to get a "clean"
  coverage baseline. Another session's uncommitted work is live in it.
- Story 25.4 is `ready-for-dev` and will create `tools/analysis-digest/`. **This story creates `tools/coverage/`
  — a sibling, not a conflict.** Do not touch `tools/analysis-digest/`.

## Acceptance Criteria

Verbatim from `epics.md` § Story 25.5. **This story does not extend them** — every owner decision below lands
*inside* these three ACs, so no `epics.md` amendment is required by this story.

1.
**Given** `coverlet.collector` 6.0.4 is already referenced and Story 25.1 already emits OpenCover from
`dotnet test`
**When** the documented command is run
**Then** a browsable HTML coverage report is produced locally from that same collector and format — **no second
coverage mechanism is introduced** — and the command is recorded in `README.md` alongside the existing
`dotnet test` guidance
**And** the report output directory is gitignored, verified with `git check-ignore`, not assumed.

2.
**Given** CI already measures coverage at 89.8%
**When** the local report is generated
**Then** the local percentage is reconciled against the CI/SonarCloud figure and any discrepancy is explained
rather than left as two numbers that disagree
**And** the story records the measured cost of generating the report, so the command's expense is known before
it is recommended.

3.
**Given** this is dev-time tooling
**When** it ships
**Then** `GoldenContentFingerprint` is unmoved and nothing under `src/` changes.

## Owner decisions locked at create-story (2026-07-28)

| # | Decision | Rationale |
|---|---|---|
| **D1** | **Renderer = ReportGenerator, pinned at `5.5.11` in a NEW `.config/dotnet-tools.json` local tool manifest**, invoked by a wrapper script at `tools/coverage/`. Not a global tool install, not an MSBuild `PackageReference`. | ReportGenerator *reads* the OpenCover file coverlet already emits — it is a renderer, **not a second coverage mechanism** (AC #1). A local manifest is committed, version-pinned and restorable, so the report is reproducible; a `-g` install is none of those. An MSBuild `PackageReference` would put a dev-only package on every restore for everyone and bury the pipeline in MSBuild. `5.5.11` is the latest on nuget.org as of 2026-07-28. |
| **D2** | **Report scope = C# only.** The discrepancy against Sonar is closed by **arithmetic**, not by widening the report. | Matches AC #1's wording exactly and keeps Node out of a .NET dev loop. § Read-first 2 shows the gap is fully derivable from published metrics, so nothing is left unexplained. The C#+`web/` unified option is real (ReportGenerator ingests lcov) and is described in Dev Notes § *The unified-report option* so it is **on the record rather than undiscovered** — 25.2 asked for exactly that. |
| **D3** | **Output directory = `artifacts/coverage/`.** No `.gitignore` change. | **Measured, not assumed:** `git check-ignore -v artifacts/coverage/index.html` → `.gitignore:66:artifacts/`. A root `coverage/` is **NOT** ignored — the existing `coverage*.xml` / `*.json` / `*.info` rules are file globs and do not cover a directory. `artifacts/` already holds build output (`SpecScribe.0.1.0.nupkg`), is inside `sonar.exclusions`, and is invisible to SpecScribe's own code map because that walk uses `git ls-files` (`SiteGenerator.cs:5019`). |
| **D4** | **The script does not open a browser by default.** An `-Open` switch is opt-in. | An auto-launching browser is hostile inside an agent loop and in CI. The report path is printed instead. |

## Tasks / Subtasks

- [ ] **Task 1 — Re-measure the SonarCloud side first, before writing anything** (AC: #2)
  - [ ] Run the three anonymous endpoints in Dev Notes § *Re-measure first* and record **today's** values for
        `coverage`, `line_coverage`, `branch_coverage`, `lines_to_cover`, `uncovered_lines`,
        `conditions_to_cover`, `uncovered_conditions` at **both** `IntegerMan_SpecScribe` and
        `IntegerMan_SpecScribe:src/SpecScribe`.
  - [ ] Record the `analysisDate` of the analysis those numbers come from, and local `HEAD`. If the analysis
        predates `HEAD`, **say so** — the comparison is then between a local report and a stale remote one, and
        that is a legitimate part of the explanation, not a defect to hide.
  - [ ] Re-verify the blend arithmetic in § Read-first 2 against today's numbers. If it no longer reconciles to
        the reported `coverage`, **stop and investigate** — the formula assumption is the backbone of AC #2.

- [ ] **Task 2 — Pin the renderer** (AC: #1)
  - [ ] `dotnet new tool-manifest` → creates `.config/dotnet-tools.json`. Confirm the path is **not** gitignored
        (verified at authoring time: `git check-ignore .config/dotnet-tools.json` → no match). It must be
        **committed**; an uncommitted manifest makes `dotnet tool restore` fail for the next reader.
  - [ ] `dotnet tool install dotnet-reportgenerator-globaltool --version 5.5.11` (local, no `-g`).
  - [ ] Record the resolved command name (`reportgenerator`) and the runtime the tool actually launches on.
        This machine carries .NET 8.0.28 / 9.0.4 / 9.0.17 / 10.0.0 / 10.0.9 runtimes, so launch is not at risk
        here — but if it ever fails to start on a leaner machine, the fix is `DOTNET_ROLL_FORWARD=Major`, the
        same mitigation `build-test-analyze.yml:56` already applies to the SonarScanner. Note it in the script's
        header comment rather than making a reader rediscover it.
  - [ ] **Do not add any coverage package.** `coverlet.collector` 6.0.4 is already a `PackageReference` in
        `tests/SpecScribe.Tests/SpecScribe.Tests.csproj:11`. Adding a second collector is the exact drift AC #1
        forbids.

- [ ] **Task 3 — Build the one documented command at `tools/coverage/`** (AC: #1)
  - [ ] `tools/coverage/Get-Coverage.ps1` plus a short `README.md`, matching the `tools/plotly-vendor` /
        `tools/prism-vendor` shape (a script + a README explaining why it exists).
  - [ ] The script, in order:
        1. **Delete `artifacts/coverage/` first.** Non-negotiable — § Read-first 4. Stale GUID dirs merge
           silently into a wrong number.
        2. `dotnet tool restore`
        3. `dotnet test SpecScribe.slnx --collect:"XPlat Code Coverage;Format=opencover" --results-directory artifacts/coverage/raw`
           — same collector, same format, same solution as `build-test-analyze.yml:208`. **No `--no-build`**:
           the command must work from a clean tree.
        4. `reportgenerator -reports:artifacts/coverage/raw/**/coverage.opencover.xml -targetdir:artifacts/coverage/html -reporttypes:"Html;TextSummary"`
        5. Print the `TextSummary` content and the absolute path of `artifacts/coverage/html/index.html`.
  - [ ] ⚠ **`-reporttypes:"Html;TextSummary"` MUST be quoted in PowerShell** — an unquoted `;` is a statement
        separator and the command will silently render only `Html`, leaving Task 5 with no summary to read.
  - [ ] `TextSummary` is not decoration: it is the machine-readable source of the line/branch figures Task 5
        reconciles. Do not drop it to "simplify".
  - [ ] Inspect the generated report for a `SpecScribe.Tests` assembly entry. coverlet excludes the test
        assembly by default, but **verify rather than assume** — if it appears, add
        `-assemblyfilters:+SpecScribe;-SpecScribe.Tests` (quoted, same reason) and say in the script comment
        that it was measured, not precautionary.
  - [ ] Add `-Open` as an opt-in switch (D4). Default off.
  - [ ] Do **not** let the script `git clean`, `git reset`, or `git checkout --` anything (§ Read-first 6).
  - [ ] **Prove "browsable" by opening it, not by asserting the exit code.** AC #1's operative word is
        *browsable*: load `artifacts/coverage/html/index.html` in a real browser and confirm (a) the summary
        renders with a non-zero assembly list, (b) drilling into a class reaches the line-by-line source view
        with covered/uncovered marks, and (c) the run produced no console errors. A non-zero exit code and a
        directory full of files are not evidence that the report renders — CLAUDE.md § *Verification* exists
        because exactly that assumption has shipped broken surfaces here before.

- [ ] **Task 4 — Prove the output directory is ignored, do not assert it** (AC: #1)
  - [ ] Run and paste the literal output of `git check-ignore -v artifacts/coverage/html/index.html` and
        `git check-ignore -v artifacts/coverage/raw/x/coverage.opencover.xml` into the Dev Agent Record.
  - [ ] Confirm `git status --short` shows **no** new untracked entry under `artifacts/` after a full run.
  - [ ] This is the AC's own wording — *"verified with `git check-ignore`, not assumed"* — and it exists because
        Story 25.1's own gitignore premise was wrong (`TestResults/` was already covered by `.gitignore:47`).

- [ ] **Task 5 — Reconcile the numbers, and name each formula** (AC: #2)
  - [ ] Build the reconciliation table: local **line** coverage vs Sonar `line_coverage` for `src/SpecScribe`;
        local **branch** coverage vs Sonar `branch_coverage` for `src/SpecScribe`.
  - [ ] Then explain the **two** remaining gaps explicitly:
        - **Formula gap** — Sonar's headline `coverage` (91.2% for `src/SpecScribe`) is the line+branch blend of
          § Read-first 2, which ReportGenerator does not compute. Show the arithmetic.
        - **Denominator gap** — Sonar's project-wide 89.7% additionally carries `extension/src` (508 lines to
          cover, **0%**) and `web/` (45.9%). A C#-only local report cannot and should not show these.
  - [ ] State plainly which single number 25.6's coverage badge should display, and why. 25.6's AC #1 requires
        the badge to show *"the same figure the CI analysis reports"* — after this story, "the CI figure" is
        four different numbers unless this story names one.
  - [ ] If any local-vs-Sonar residual survives both explanations, **do not round it away** — investigate
        `lines_to_cover` counts (Sonar counts executable lines; coverlet counts sequence points) and record the
        finding.

- [ ] **Task 6 — Document the command in `README.md`** (AC: #1)
  - [ ] Add it to the existing fenced block under **§ Development** (`README.md:185-189`), which today holds
        `dotnet build` / `dotnet test` / `dotnet run`. The AC says *"alongside the existing `dotnet test`
        guidance"* — that block, not a new section.
  - [ ] One line, with the same inline-comment style the block already uses. Follow with a short sentence naming
        the output path and stating that the report is C#-only and why it differs from the SonarCloud badge —
        so a reader who sees two numbers is not left guessing.
  - [ ] Do **not** duplicate the reconciliation table into `README.md`; link the story or
        `docs/SonarCloudSetup.md` instead. The README is 207 lines and is a front door, not a record.

- [ ] **Task 7 — Measure the cost honestly** (AC: #2)
  - [ ] Time the full command **at least three times** and report min / max / median plus each run's
        passed/failed/skipped counts. § Read-first 5 — a single sample here has already produced fiction once.
  - [ ] Report the *incremental* cost separately: the wall-clock added over a plain
        `dotnet test SpecScribe.slnx` on the same machine in the same session. CI measured the coverage
        collection alone at **+13.0 s / +30.6%** of the test step; the ReportGenerator render is additional and
        has never been measured here.
  - [ ] Record the report's on-disk size and file count. It lands in a gitignored dir, but a reader deciding
        whether to run this on every change deserves the number.
  - [ ] If any run is red, say so and say why, and **do not** use its percentage anywhere.

- [ ] **Task 8 — Prove the scope claim** (AC: #3)
  - [ ] `git status --short` before and after. The **only** paths this story may add or modify:
        `.config/dotnet-tools.json`, `tools/coverage/**`, `README.md`, `sprint-status.yaml`.
  - [ ] State the fingerprint claim **by scope**: no file under `src/` or `tests/` is written, therefore
        `GoldenContentFingerprint` cannot move because of this story. Do not quote a hash — a concurrent session
        is actively editing `src/` (§ Read-first 6) and any hash you record will be theirs, not yours.
  - [ ] Confirm `tools/**` remains inside `sonar.exclusions` (`build-test-analyze.yml:191`), so the new script
        does not appear in the findings list Epic 25 exists to triage.
  - [ ] Confirm **no** change to `.github/workflows/build-test-analyze.yml`. CI already collects coverage; this
        story adds a *local* renderer and has no CI half.

## Dev Notes

### Absolute scope boundaries

| Allowed | Forbidden |
|---|---|
| `.config/dotnet-tools.json` (new, committed) | anything under `src/` — Epic 25 ships **no product code** |
| `tools/coverage/**` (new) | anything under `tests/` — including the `.csproj`; the collector is already there |
| `README.md` § Development | `.github/workflows/**` — CI's coverage path already works |
| `sprint-status.yaml` | `tools/analysis-digest/**` — Story 25.4's in-flight work |
| | `web/**`, `extension/**` — D2 scoped this to C# |
| | a second coverage collector, in any form |

### Re-measure first

The project is public; **none of these need a token**. Run them before trusting any number in this file.

```bash
# Project-wide, and the two formulas side by side
curl -s "https://sonarcloud.io/api/measures/component?component=IntegerMan_SpecScribe&metricKeys=coverage,line_coverage,branch_coverage,lines_to_cover,uncovered_lines,conditions_to_cover,uncovered_conditions,alert_status"

# The directory the local report actually covers
curl -s "https://sonarcloud.io/api/measures/component?component=IntegerMan_SpecScribe:src/SpecScribe&metricKeys=coverage,line_coverage,branch_coverage,lines_to_cover,uncovered_lines,conditions_to_cover,uncovered_conditions"

# The two directories that make the project figure lower than the C# one
curl -s "https://sonarcloud.io/api/measures/component_tree?component=IntegerMan_SpecScribe&metricKeys=coverage,line_coverage,lines_to_cover,uncovered_lines,ncloc&qualifiers=DIR&ps=25"
```

Values captured 2026-07-28 at authoring time, for drift detection only — **cite your own, not these**:

| Component | `coverage` | `line_coverage` | `branch_coverage` | `lines_to_cover` | `uncovered_lines` |
|---|---|---|---|---|---|
| `IntegerMan_SpecScribe` | 89.7 | 91.7 | 85.3 | 25,303 | 2,090 |
| `…:src/SpecScribe` | 91.2 | **93.9** | **85.5** | 24,653 | 1,498 |
| `…:extension/src` | 0.0 | — | — | 508 | 508 |
| `…:web` | 45.9 | — | — | 130 | 72 |

`alert_status` = `ERROR` (three non-coverage conditions; Story 25.2 § *What remains*). `new_coverage` is
**passing** at 89.3% since Story 23.5 supplied real Vitest coverage.

### Why ReportGenerator is not "a second coverage mechanism"

AC #1's constraint is about **collection**, not rendering. The pipeline is unchanged end to end:

```
coverlet.collector 6.0.4  →  coverage.opencover.xml  →  ┬→  SonarScanner   (CI, unchanged)
   (already referenced)        (already emitted)        └→  ReportGenerator (NEW, local only)
```

ReportGenerator reads the artifact; it never instruments, never runs tests, never produces a coverage number of
its own. Nothing about CI's path changes.

### The unified-report option — recorded, not chosen

Story 25.2 handed this story an explicit instruction: *"25.5's binding constraint of no second coverage
mechanism now needs restating — Story 23.5 added a genuine second mechanism (Vitest/lcov)… 25.5 must say so
rather than discover it."* Stating it:

**`web/` has its own collector** (`@vitest/coverage-v8`, `web/vitest.config.ts`, emitting `text` + `lcov` to the
gitignored `web/coverage/`). It does **not** violate AC #1's constraint, because it covers a language the
OpenCover path structurally cannot reach — the constraint forbids two mechanisms measuring *the same code*, not
one mechanism per stack.

ReportGenerator ingests lcov, so `-reports:"artifacts/coverage/raw/**/coverage.opencover.xml;web/coverage/lcov.info"`
would produce a single report spanning both stacks and land much closer to Sonar's blended project figure. **D2
declined it** for this story: it would require the command to also run `npm run test:coverage`, pulling Node into
a .NET dev loop and roughly doubling its runtime, for a number AC #2 can already explain arithmetically. Left
here as a known, priced option rather than an undiscovered one.

### Files to read before writing code

| File | Why |
|---|---|
| `.github/workflows/build-test-analyze.yml:203-208` | The exact `dotnet test` invocation to mirror. Deviating creates the drift AC #1 forbids. |
| `.github/workflows/build-test-analyze.yml:191` | Confirms `tools/**` is already Sonar-excluded. |
| `tests/SpecScribe.Tests/SpecScribe.Tests.csproj:11` | `coverlet.collector` 6.0.4 is **already** referenced. |
| `.gitignore:47`, `.gitignore:66` | `[Tt]est[Rr]esult*/` and `artifacts/` — the two rules D3 depends on. |
| `README.md:183-197` | The § Development fence and the § Continuous integration text that follows it. |
| `tools/plotly-vendor/README.md` | The `tools/` convention this story matches. |
| `docs/SonarCloudSetup.md` § *Coverage exclusions* | Why `web/**` is partly excluded and `extension/src/**` is not — the denominator gap in Task 5. |
| `src/SpecScribe/GitMetrics.cs:259` | The 3 s subprocess timeout behind the local flake (§ Read-first 5). |

### Anti-patterns this story is specifically at risk of

1. **Globbing `**/TestResults/**` without cleaning.** Produces a merged, wrong, plausible number. § Read-first 4.
2. **Comparing a line-coverage % against Sonar's blended `coverage` %** and reporting the difference as a
   discrepancy. It is arithmetic. § Read-first 2.
3. **Citing 89.8% as "what coverlet reports".** It never was. § Read-first 1.
4. **A single timing sample.** 25.1 already published a retracted "+47.5%" this way. § Read-first 5 / Task 7.
5. **Adding a coverage package** because the pipeline "needs one". It does not — 6.0.4 is already there.
6. **Fixing an unrelated `src/` nit** noticed while reading a coverage report. That is an `src/` edit and AC #3
   forbids it. Route it to the backlog the way Story 25.2 routed its findings.
7. **Asserting a fingerprint hash.** Prove by scope; a concurrent session owns the tree. § Read-first 6.

### Testing standards

There is **no test project for `tools/`**, and this story does not create one — `tools/plotly-vendor` and
`tools/prism-vendor` ship untested by the same reasoning, and `tools/**` is Sonar-excluded. Verification here is
**empirical and recorded** (Tasks 4, 7, 8): real runs, literal `git check-ignore` output, timings with spread,
and a `git status --short` scope proof. The existing ~2,394-test suite is *the subject* of this story's
measurement, not its verification.

### Project Structure Notes

- **New:** `.config/dotnet-tools.json` — the repo's **first** dotnet tool manifest. Committed by design.
- **New:** `tools/coverage/{Get-Coverage.ps1,README.md}` — sibling to `tools/plotly-vendor`,
  `tools/prism-vendor`, and (in flight) `tools/analysis-digest`.
- **Modified:** `README.md` § Development only.
- **No `.gitignore` change** — `artifacts/` is already covered at `.gitignore:66`, verified (D3).
- **No `epics.md` amendment** — all three ACs are carried verbatim and every decision lands inside them.
- `.ps1` under `.gitattributes`' `* text=auto eol=lf` rule is correct; pwsh reads LF fine on Windows, and the
  repo deliberately carries no `.bat`/`.cmd` CRLF carve-out.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Story 25.5] — the three ACs, verbatim.
- [Source: `_bmad-output/implementation-artifacts/25-1-sonarcloud-onboarding-and-ci-analysis.md` § *After
  widening*, § *4b. The coverage decision*] — the origin of 89.8% (a Sonar measure), the collector/format/upload
  decision, and the +13.0 s / +30.6% CI coverage cost.
- [Source: `_bmad-output/implementation-artifacts/25-2-quality-gate-and-findings-triage.md` § *Handoffs*] — the
  three-number problem, the `extension/src` and `web/` denominator effect, and the instruction to restate the
  "no second mechanism" constraint against Vitest.
- [Source: `docs/SonarCloudSetup.md` § *Coverage exclusions*] — why `web/**` is narrowly excluded and
  `extension/src/**` is not.
- [Source: `CLAUDE.md` § *Concurrent work on shared `main`*, § *Verification*] — verify after every edit; never
  `git reset --hard` / `checkout --` / `clean`; generate to `SpecScribeOutput/`.
- ReportGenerator `5.5.11` — latest on nuget.org, confirmed 2026-07-28 via
  `https://api.nuget.org/v3-flatcontainer/dotnet-reportgenerator-globaltool/index.json`.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
