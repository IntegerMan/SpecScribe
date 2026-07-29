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

Status: review

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

- [x] **Task 1 — Re-measure the SonarCloud side first, before writing anything** (AC: #2)
  - [x] Run the three anonymous endpoints in Dev Notes § *Re-measure first* and record **today's** values for
        `coverage`, `line_coverage`, `branch_coverage`, `lines_to_cover`, `uncovered_lines`,
        `conditions_to_cover`, `uncovered_conditions` at **both** `IntegerMan_SpecScribe` and
        `IntegerMan_SpecScribe:src/SpecScribe`.
  - [x] Record the `analysisDate` of the analysis those numbers come from, and local `HEAD`. If the analysis
        predates `HEAD`, **say so** — the comparison is then between a local report and a stale remote one, and
        that is a legitimate part of the explanation, not a defect to hide.
  - [x] Re-verify the blend arithmetic in § Read-first 2 against today's numbers. If it no longer reconciles to
        the reported `coverage`, **stop and investigate** — the formula assumption is the backbone of AC #2.

- [x] **Task 2 — Pin the renderer** (AC: #1)
  - [x] `dotnet new tool-manifest` → creates `.config/dotnet-tools.json`. Confirm the path is **not** gitignored
        (verified at authoring time: `git check-ignore .config/dotnet-tools.json` → no match). It must be
        **committed**; an uncommitted manifest makes `dotnet tool restore` fail for the next reader.
  - [x] `dotnet tool install dotnet-reportgenerator-globaltool --version 5.5.11` (local, no `-g`).
  - [x] Record the resolved command name (`reportgenerator`) and the runtime the tool actually launches on.
        This machine carries .NET 8.0.28 / 9.0.4 / 9.0.17 / 10.0.0 / 10.0.9 runtimes, so launch is not at risk
        here — but if it ever fails to start on a leaner machine, the fix is `DOTNET_ROLL_FORWARD=Major`, the
        same mitigation `build-test-analyze.yml:56` already applies to the SonarScanner. Note it in the script's
        header comment rather than making a reader rediscover it.
  - [x] **Do not add any coverage package.** `coverlet.collector` 6.0.4 is already a `PackageReference` in
        `tests/SpecScribe.Tests/SpecScribe.Tests.csproj:11`. Adding a second collector is the exact drift AC #1
        forbids.

- [x] **Task 3 — Build the one documented command at `tools/coverage/`** (AC: #1)
  - [x] `tools/coverage/Get-Coverage.ps1` plus a short `README.md`, matching the `tools/plotly-vendor` /
        `tools/prism-vendor` shape (a script + a README explaining why it exists).
  - [x] The script, in order:
        1. **Delete `artifacts/coverage/` first.** Non-negotiable — § Read-first 4. Stale GUID dirs merge
           silently into a wrong number.
        2. `dotnet tool restore`
        3. `dotnet test SpecScribe.slnx --collect:"XPlat Code Coverage;Format=opencover" --results-directory artifacts/coverage/raw`
           — same collector, same format, same solution as `build-test-analyze.yml:208`. **No `--no-build`**:
           the command must work from a clean tree.
        4. `reportgenerator -reports:artifacts/coverage/raw/**/coverage.opencover.xml -targetdir:artifacts/coverage/html -reporttypes:"Html;TextSummary"`
        5. Print the `TextSummary` content and the absolute path of `artifacts/coverage/html/index.html`.
  - [x] ⚠ **`-reporttypes:"Html;TextSummary"` MUST be quoted in PowerShell** — an unquoted `;` is a statement
        separator and the command will silently render only `Html`, leaving Task 5 with no summary to read.
  - [x] `TextSummary` is not decoration: it is the machine-readable source of the line/branch figures Task 5
        reconciles. Do not drop it to "simplify".
  - [x] Inspect the generated report for a `SpecScribe.Tests` assembly entry. coverlet excludes the test
        assembly by default, but **verify rather than assume** — if it appears, add
        `-assemblyfilters:+SpecScribe;-SpecScribe.Tests` (quoted, same reason) and say in the script comment
        that it was measured, not precautionary.
  - [x] Add `-Open` as an opt-in switch (D4). Default off.
  - [x] Do **not** let the script `git clean`, `git reset`, or `git checkout --` anything (§ Read-first 6).
  - [x] **Prove "browsable" by opening it, not by asserting the exit code.** AC #1's operative word is
        *browsable*: load `artifacts/coverage/html/index.html` in a real browser and confirm (a) the summary
        renders with a non-zero assembly list, (b) drilling into a class reaches the line-by-line source view
        with covered/uncovered marks, and (c) the run produced no console errors. A non-zero exit code and a
        directory full of files are not evidence that the report renders — CLAUDE.md § *Verification* exists
        because exactly that assumption has shipped broken surfaces here before.

- [x] **Task 4 — Prove the output directory is ignored, do not assert it** (AC: #1)
  - [x] Run and paste the literal output of `git check-ignore -v artifacts/coverage/html/index.html` and
        `git check-ignore -v artifacts/coverage/raw/x/coverage.opencover.xml` into the Dev Agent Record.
  - [x] Confirm `git status --short` shows **no** new untracked entry under `artifacts/` after a full run.
  - [x] This is the AC's own wording — *"verified with `git check-ignore`, not assumed"* — and it exists because
        Story 25.1's own gitignore premise was wrong (`TestResults/` was already covered by `.gitignore:47`).

- [x] **Task 5 — Reconcile the numbers, and name each formula** (AC: #2)
  - [x] Build the reconciliation table: local **line** coverage vs Sonar `line_coverage` for `src/SpecScribe`;
        local **branch** coverage vs Sonar `branch_coverage` for `src/SpecScribe`.
  - [x] Then explain the **two** remaining gaps explicitly:
        - **Formula gap** — Sonar's headline `coverage` (91.2% for `src/SpecScribe`) is the line+branch blend of
          § Read-first 2, which ReportGenerator does not compute. Show the arithmetic.
        - **Denominator gap** — Sonar's project-wide 89.7% additionally carries `extension/src` (508 lines to
          cover, **0%**) and `web/` (45.9%). A C#-only local report cannot and should not show these.
  - [x] State plainly which single number 25.6's coverage badge should display, and why. 25.6's AC #1 requires
        the badge to show *"the same figure the CI analysis reports"* — after this story, "the CI figure" is
        four different numbers unless this story names one.
  - [x] If any local-vs-Sonar residual survives both explanations, **do not round it away** — investigate
        `lines_to_cover` counts (Sonar counts executable lines; coverlet counts sequence points) and record the
        finding.

- [x] **Task 6 — Document the command in `README.md`** (AC: #1)
  - [x] Add it to the existing fenced block under **§ Development** (`README.md:185-189`), which today holds
        `dotnet build` / `dotnet test` / `dotnet run`. The AC says *"alongside the existing `dotnet test`
        guidance"* — that block, not a new section.
  - [x] One line, with the same inline-comment style the block already uses. Follow with a short sentence naming
        the output path and stating that the report is C#-only and why it differs from the SonarCloud badge —
        so a reader who sees two numbers is not left guessing.
  - [x] Do **not** duplicate the reconciliation table into `README.md`; link the story or
        `docs/SonarCloudSetup.md` instead. The README is 207 lines and is a front door, not a record.

- [x] **Task 7 — Measure the cost honestly** (AC: #2)
  - [x] Time the full command **at least three times** and report min / max / median plus each run's
        passed/failed/skipped counts. § Read-first 5 — a single sample here has already produced fiction once.
  - [x] Report the *incremental* cost separately: the wall-clock added over a plain
        `dotnet test SpecScribe.slnx` on the same machine in the same session. CI measured the coverage
        collection alone at **+13.0 s / +30.6%** of the test step; the ReportGenerator render is additional and
        has never been measured here.
  - [x] Record the report's on-disk size and file count. It lands in a gitignored dir, but a reader deciding
        whether to run this on every change deserves the number.
  - [x] If any run is red, say so and say why, and **do not** use its percentage anywhere.

- [x] **Task 8 — Prove the scope claim** (AC: #3)
  - [x] `git status --short` before and after. The **only** paths this story may add or modify:
        `.config/dotnet-tools.json`, `tools/coverage/**`, `README.md`, `sprint-status.yaml`.
  - [x] State the fingerprint claim **by scope**: no file under `src/` or `tests/` is written, therefore
        `GoldenContentFingerprint` cannot move because of this story. Do not quote a hash — a concurrent session
        is actively editing `src/` (§ Read-first 6) and any hash you record will be theirs, not yours.
  - [x] Confirm `tools/**` remains inside `sonar.exclusions` (`build-test-analyze.yml:191`), so the new script
        does not appear in the findings list Epic 25 exists to triage.
  - [x] Confirm **no** change to `.github/workflows/build-test-analyze.yml`. CI already collects coverage; this
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

claude-opus-5 (Amelia / `bmad-dev-story`), 2026-07-28.

### Debug Log References

Run logs in the session scratchpad: `run2.log`–`run7.log`, `base1.log`, `base2.log`.

---

#### Task 1 — SonarCloud re-measured (all endpoints anonymous, no token)

**The analysis is NOT stale.** `api/project_analyses/search` returns `revision`
`8a2fb8352f882debb2e81c7369f52366f6a24c53` for the most recent analysis, `analysisDate`
**2026-07-28T23:55:45+0000**. Local `HEAD` is `8a2fb83` — **the same commit**. So the remote side is not
behind, and the honest caveat runs the *other* way: the local working tree is dirty (§ Working-tree gap below).

| Component | `coverage` | `line_coverage` | `branch_coverage` | `lines_to_cover` | `uncovered_lines` | `conditions_to_cover` | `uncovered_conditions` |
|---|---|---|---|---|---|---|---|
| `IntegerMan_SpecScribe` | **89.8** | 91.9 | 85.5 | 25,604 | 2,070 | 12,258 | 1,774 |
| `…:src/SpecScribe` | **91.3** | **94.1** | **85.7** | 24,951 | 1,477 | 12,188 | 1,738 |
| `…:extension/src` | 0.0 | 0.0 | — | 508 | 508 | — | — |
| `…:web` | 46.3 | 45.1 | — | 133 | 73 | — | — |

`alert_status` = **ERROR** (unchanged; the three non-coverage conditions from Story 25.2).

**Every figure in the story file has moved again** — the create-story table is superseded:

| Figure | 25.1 | 25.2 | story file (authoring) | **this run** |
|---|---|---|---|---|
| Project `coverage` | 89.8% | 87.6% | 89.7% | **89.8%** |
| `src/SpecScribe` `coverage` | — | 91.4% | 91.2% | **91.3%** |
| `src/SpecScribe` `line_coverage` | — | — | 93.9% | **94.1%** |
| `src/SpecScribe` `branch_coverage` | — | — | 85.5% | **85.7%** |

The project figure landing back on 89.8% is a **coincidence**, not a vindication of the epic's number —
§ Read-first 1 stands: 89.8% was always a *SonarCloud* measure and never a coverlet one.

**Blend arithmetic re-verified — it still reconciles exactly, at both scopes:**

```
src/SpecScribe:  (24951 − 1477) + (12188 − 1738)     23474 + 10450     33924
                 ───────────────────────────────  =  ─────────────  =  ─────  = 91.343%  → Sonar 91.3  ✓
                        24951 + 12188                    37139          37139

project-wide:    (25604 − 2070) + (12258 − 1774)     23534 + 10484     34018
                 ───────────────────────────────  =  ─────────────  =  ─────  = 89.847%  → Sonar 89.8  ✓
                        25604 + 12258                    37862          37862
```

Line and branch check out independently too: `23474/24951` = 94.080 → 94.1 ✓; `10450/12188` = 85.740 →
85.7 ✓; `23534/25604` = 91.915 → 91.9 ✓; `10484/12258` = 85.528 → 85.5 ✓. **The formula assumption behind
AC #2 holds.**

---

#### Task 2 — Renderer pinned

- `dotnet new tool-manifest` **did not** create `.config/dotnet-tools.json` on this SDK
  (10.0.400-preview.0.26322.102) — it wrote `dotnet-tools.json` to the **repo root**. Relocated to
  `.config/dotnet-tools.json` per D1. Worth knowing: the documented behaviour is not what this preview SDK does.
- `dotnet tool install dotnet-reportgenerator-globaltool --version 5.5.11` (local, no `-g`) → resolved command
  name **`reportgenerator`**, manifest entry carries `"rollForward": false`.
- Tool package ships **net8.0 / net9.0 / net10.0** assets; this machine has 8.0.28 / 9.0.4 / 9.0.17 / 10.0.0 /
  10.0.9, so launch is not at risk. Verified live: `dotnet tool run reportgenerator` starts and reports
  "No report files specified." The `DOTNET_ROLL_FORWARD=Major` fallback is recorded in the script header.
- `dotnet tool restore` → "Restore was successful."
- **No coverage package added.** `coverlet.collector` 6.0.4 remains the only collector.

---

#### Task 3 — The command, and the browsability proof

`tools/coverage/Get-Coverage.ps1` + `tools/coverage/README.md`, matching the `tools/plotly-vendor` shape.

**Test assembly — measured, not assumed.** `Summary.txt` reports `Assemblies: 1` / `specscribe`, and
`grep -c "SpecScribe.Tests" Summary.txt` → **0**. coverlet's default test-assembly exclusion holds here, so
**no `-assemblyfilters` is applied**, and the script comment says it was measured.

**A real hazard closed that the story did not anticipate:** PowerShell 7.4+ defaults
`$PSNativeCommandUseErrorActionPreference` to `$true`. Combined with `$ErrorActionPreference = 'Stop'`, a
non-zero `dotnet test` would have thrown and aborted the run — destroying the "a red suite still emits a
report" behaviour § Read-first 5 depends on. The script sets it to `$false` and checks exit codes explicitly.

**Browsability proved in a live browser** (CLAUDE.md § *Verification*), not by exit code:

- **(a)** `index.html` renders: title "Summary - Coverage Report", `Assemblies: 1`, `Classes: 268`,
  `Files: 143`, plus the Risk Hotspots table. Non-zero assembly list ✓
- **(b)** Drilled into `specscribe_StatusStyles.html`: **505** source lines rendered, **462** covered marks
  (`.lightgreen`), **8** uncovered marks (`.lightred`), real C# source text — sampled covered line
  `public static string ForStory(StoryInfo story) => ForStatus(story.Status);`, sampled uncovered line
  `return "active";`. Line-by-line source view with covered/uncovered marks ✓
- **(c)** `read_console_messages` → **"No console logs."** No console errors ✓

---

#### Task 4 — The output directory is ignored (literal output)

```
$ git check-ignore -v artifacts/coverage/html/index.html
.gitignore:66:artifacts/	artifacts/coverage/html/index.html

$ git check-ignore -v artifacts/coverage/raw/x/coverage.opencover.xml
.gitignore:66:artifacts/	artifacts/coverage/raw/x/coverage.opencover.xml
```

After a full run that produced 299 files under `artifacts/coverage/`:

```
$ git status --short -- artifacts/
(no output)
```

D3 confirmed exactly as specified, and **no `.gitignore` change was made** (`git status --short .gitignore`
→ no output).

---

#### Task 5 — The reconciliation

**Basis: the one fully green run (run 2), `2740 passed / 0 failed / 3 skipped`.** No figure below is taken
from a red run.

| Measure | Local (ReportGenerator) | Sonar (`src/SpecScribe`) | Δ |
|---|---|---|---|
| **Line coverage** | **93.78%** (23,974 / 25,563) | **94.08%** (23,474 / 24,951) | **−0.30 pp** |
| **Branch coverage** | **85.34%** (10,580 / 12,398) | **85.74%** (10,450 / 12,188) | **−0.40 pp** |
| Blended `coverage` | *not produced* | 91.34% | — |

**Gap 1 — formula.** Sonar's headline `coverage` (91.3% for `src/SpecScribe`, 89.8% project-wide) is the
line+branch blend shown in Task 1. ReportGenerator emits line and branch as two separate figures and never
computes that blend. Comparing local 93.78% against Sonar's 91.3% would "discover" a 2.5 pp discrepancy that
is **pure arithmetic**. The correct comparisons are the two rows above.

**Gap 2 — denominator.** Sonar's project-wide 89.8% additionally carries `extension/src` (508 lines to cover,
**0.0%**) and `web` (133 lines, 45.1% line coverage). A C#-only local report cannot and should not show them.
This is why `src/SpecScribe` is the only correct Sonar scope to compare against — 24,951 of the project's
25,604 lines to cover.

**Gap 3 — the residual, NOT rounded away.** A −0.30 pp / −0.40 pp residual survives both explanations, and it
is fully accounted for by **counting-unit and tree differences**:

| | Sonar | Local | Δ |
|---|---|---|---|
| Coverable lines | 24,951 | 25,563 | **+612** (+2.45%) |
| Covered lines | 23,474 | 23,974 | +500 |
| Branches / conditions | 12,188 | 12,398 | **+210** (+1.72%) |
| Covered branches | 10,450 | 10,580 | +130 |

coverlet counts **612 more coverable lines** than Sonar — Sonar counts executable lines, coverlet counts
IL **sequence points**, which split multi-line expressions and generated members differently. Those extra 612
lines are only **81.7% covered** (500/612), well below the 94% average, and that alone drags the local line
figure down by ≈0.30 pp. The same holds for branches: the extra 210 are **61.9% covered** (130/210) against an
85.7% average, giving ≈0.40 pp. **The residual is explained, not rounded.**

**Working-tree gap (a third source, and the reason the residual is not exactly reproducible).** Sonar analysed
commit `8a2fb83`; the local runs execute against a dirty working tree with a concurrent session's uncommitted
`src/` edits. This was directly observed: coverable lines moved **25,563 → 25,568** between run 2 and run 7
within this session. Local-vs-Sonar comparison is therefore trustworthy at the ~0.1 pp level, not tighter.

**Which number 25.6's badge should display: SonarCloud's project-wide `coverage` (89.8% today).** Reasons:

1. It is the figure the CI **quality gate** acts on — 25.6 AC #1's "the same figure the CI analysis reports"
   has exactly one honest referent, and this is it.
2. It covers the whole shipped product (C# + `extension/` + `web/`), which is what a README badge implies.
3. It can be sourced from SonarCloud's own badge endpoint, so it **cannot drift** — any of the other three
   figures (91.3 directory-scoped, 94.1 line, 85.7 branch) would need recomputation or hand-maintenance.
4. The local 93.78% is explicitly **not** the badge number, and `README.md` now says so in one sentence rather
   than leaving a reader to reconcile two numbers.

---

#### Task 7 — Cost, measured honestly

**Seven script runs and two baselines. The headline finding is that on this machine, under a concurrently
active session, run-to-run variance swamps the coverage overhead — so a single-sample "incremental cost"
figure would be fiction, exactly as § Read-first 5 warns.**

| Run | Wall clock | Test outcome | Report? |
|---|---|---|---|
| 1 | 216.9 s | red (counts not captured) | rendered — 93.7 / 85.3 |
| 2 | **150.1 s** | **GREEN — 2740 / 0 / 3** | rendered — **93.7 / 85.3** ← reconciliation basis |
| 3 | 314.6 s | red 5 + `CoverletDataCollectorException` | **none** — aborted |
| 4 | 241.9 s | red 6 | rendered — 93.8 / 85.4 |
| 5 | 64.0 s | **build failed** (`MSB3027`) | **none** — aborted |
| 6 | 75.3 s | **build failed** (`MSB3027`) | **none** — aborted |
| 7 | 157.3 s | red 6 | rendered — 93.8 / 85.4 |

**Runs that completed and rendered (n=4):** min **150.1 s**, max **241.9 s**, median **187.1 s**.

**Baseline `dotnet test SpecScribe.slnx`, no coverage, same machine and session:** 146.0 s (GREEN 2740/0/3),
186.8 s (red, 4 failed).

**Incremental cost.** The only apples-to-apples pair — green coverage run vs green baseline — is
**150.1 s vs 146.0 s = +4.1 s / +2.8%**. That number should be treated as indicative only: it sits inside a
146–315 s spread, so the contention noise (±100 s) is an order of magnitude larger than the signal. CI's
**+13.0 s / +30.6%** for collection alone remains the more trustworthy figure.

**The render step, however, IS isolable, and it is cheap.** Per-phase timing was added to the script for
exactly this reason. Run 7: **156.6 s total = 150.9 s build+test + 5.1 s render.** ReportGenerator costs
about **5 seconds** on this repo — the expensive part of the command is the test suite, which you were going
to run anyway.

**On-disk cost:** `artifacts/coverage/html` = **53 MB across 299 files**; `raw` = 7.3 MB; **60 MB total**.
Cleaned on every run rather than accumulated.

**Red runs, and why — no percentage is cited from any of them:**

- Runs 1, 4: the known § Read-first 5 git flake. Run 3's failures were
  `GitMetricsTryComputeTests`, `SiteGeneratorGitInsightsTests`, `SiteGeneratorCodeInsightsTests`,
  `ForgeOptionsTests` (deep-git flag) and the golden-fingerprint test — all git-subprocess-timeout dependent.
- Run 7's 6 failures were **entirely** `SpecScribe.Tests.IncrementalOracleParityTests.*` — a test file a
  **concurrent session created during this story's execution** (Story 22.5's in-flight work; it appears as
  untracked in `git status`). Not this story's, and not the git flake.

**Two findings that correct the story's own premises:**

1. **§ Read-first 5's "a red suite still emits a coverage report" is true for *test* failures but FALSE for
   collector-level failures.** Run 3 hit
   `CoverletDataCollectorException: Failed to instrument modules` (a `FileSystem.Copy` lock while another
   process held the module) and produced **no `coverage.opencover.xml` at all**. Runs 5 and 6 failed earlier
   still, at build: `MSB3027 … file is locked by "testhost"`. The script's explicit
   "No coverage.opencover.xml was produced … Nothing to render" guard turned all three into loud failures
   instead of a silently stale or absent report — the clean-first design working as intended.
2. **The 3-of-7 abort rate is itself the honest cost of running this on a shared machine.** It is recorded in
   `tools/coverage/README.md` § *When the command refuses to render* so the next reader recognises the two
   error signatures rather than rediscovering them.

---

#### Task 8 — Scope proof

`git status --short` at completion. **This story's paths only:**

| Path | Status |
|---|---|
| `.config/dotnet-tools.json` | new (untracked `.config/`) |
| `tools/coverage/Get-Coverage.ps1`, `tools/coverage/README.md` | new (untracked `tools/coverage/`) |
| `README.md` | modified — § Development block only |
| `_bmad-output/implementation-artifacts/25-5-local-coverage-report.md` | this record |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | status transitions |

**Everything else in `git status` belongs to concurrent sessions** and is explicitly NOT this story's:
`src/SpecScribe/AboutSddTemplater.cs`, `src/SpecScribe/TestArtifactsModel.cs`,
`src/SpecScribe/assets/specscribe.css`, `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs`,
`tests/SpecScribe.Tests/GoldenNormalization.cs` (new), `tests/SpecScribe.Tests/IncrementalOracleParityTests.cs`
(new), `web/**` (6 files), and four other `_bmad-output` story files. Corroborated mechanically:
`git diff -- src/ tests/ | grep -ci "reportgenerator\|Get-Coverage\|artifacts/coverage"` → **0**. None of
those edits references anything this story created.

**`GoldenContentFingerprint` — the claim is by SCOPE, and no hash is quoted** (§ Read-first 6 / D-scope):
this story writes **no file under `src/` or `tests/`**, therefore it cannot move the fingerprint. Supporting
evidence rather than proof: the fingerprint test **passed** in run 2 (fully green) and again in run 7, with
all of this story's files on disk. It failed only in run 3, where the failure message was
`Rendered output content changed` — driven by the concurrent session's `src/` edits, whose new expected hash
was `9544578b…`. That hash is **theirs, not this story's**, and is recorded here only to name the cause.

- `tools/**` confirmed still inside `sonar.exclusions` (`build-test-analyze.yml:191`) — the new script will
  not appear in the findings list Epic 25 exists to triage.
- `.github/workflows/build-test-analyze.yml` — **untouched** (`git status --short` on it → no output).
- `.gitignore` — **untouched** (D3).

---

#### Final regression run (story completion gate)

`dotnet test SpecScribe.slnx` → **2,743 passed / 6 failed / 3 skipped**, 1 m 55 s.

**All 6 failures are `SpecScribe.Tests.IncrementalOracleParityTests.*`**, and that file is **untracked** —
`git status --short tests/SpecScribe.Tests/IncrementalOracleParityTests.cs` → `?? …`;
`git ls-files --error-unmatch` → *"Did you forget to 'git add'?"*. It is a concurrent session's in-flight
Story 22.5 work created **during** this story's execution, not a regression from it. This story writes no
file under `src/` or `tests/`, so it cannot affect them.

`GoldenContentFingerprint` does **not** appear in the failure list (`grep -c` → 0): the golden fingerprint
test **passed** in this run, as it did in runs 2 and 7, with all of this story's files on disk.

Per CLAUDE.md § *Concurrent work on shared `main`*, this is the expected condition on this repo, not a
defect to engineer away — recorded here rather than hidden, and no percentage anywhere in this record is
taken from a red run.

### Completion Notes List

- **AC #1 satisfied.** One documented command (`pwsh tools/coverage/Get-Coverage.ps1`) renders a browsable
  HTML report from the *same* `coverlet.collector` 6.0.4 / OpenCover artifact CI already produces. No second
  coverage mechanism: ReportGenerator 5.5.11 is a renderer, pinned in the repo's first
  `.config/dotnet-tools.json`. Recorded in `README.md` § Development beside `dotnet test`. Output directory
  proven ignored with literal `git check-ignore -v` output, not assumed.
- **AC #2 satisfied.** Local line/branch reconciled against Sonar's `line_coverage`/`branch_coverage` for
  `src/SpecScribe`; the −0.30 pp / −0.40 pp residual is explained quantitatively (612 extra coverable lines at
  81.7% marginal coverage; 210 extra branches at 61.9%) rather than left as two numbers that disagree. Cost
  measured across 7 runs + 2 baselines with the full spread and every run's pass/fail counts reported.
- **AC #3 satisfied by scope.** No file under `src/` or `tests/` written; no `.gitignore`, no
  `.github/workflows/**`, no `web/**`, no `extension/**`, no `tools/analysis-digest/**` change.
- **Two of the story's own premises corrected:** (1) `dotnet new tool-manifest` writes to the repo root, not
  `.config/`, on SDK 10.0.400-preview; (2) "a red suite still emits a coverage report" holds for test failures
  but not for collector or build failures, which emit nothing — the script now fails loudly in that case.
- **One hazard closed that the story did not list:** PS 7.4+ `$PSNativeCommandUseErrorActionPreference`
  would have aborted the run on a red suite, defeating the designed behaviour.
- **Named for 25.6:** the badge should show SonarCloud's project-wide `coverage` (89.8% today), for four
  stated reasons.
- **Not done, and out of scope by D2:** the `web/` lcov merge. Priced and recorded in
  `tools/coverage/README.md`, not silently dropped.
- **No test project added for `tools/`** — consistent with `tools/plotly-vendor` / `tools/prism-vendor`, and
  `tools/**` is Sonar-excluded. Verification here is empirical and recorded, per the story's own
  § Testing standards.

### File List

- `.config/dotnet-tools.json` — **new.** The repo's first dotnet local tool manifest; pins
  `dotnet-reportgenerator-globaltool` 5.5.11. Committed by design.
- `tools/coverage/Get-Coverage.ps1` — **new.** The one documented command.
- `tools/coverage/README.md` — **new.** Why it exists, what it does not cover, and the two failure signatures.
- `README.md` — **modified.** § Development fenced block + one explanatory paragraph.
- `_bmad-output/implementation-artifacts/25-5-local-coverage-report.md` — **modified.** This record.
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — **modified.** `ready-for-dev` → `in-progress`
  → `review`.

## Change Log

| Date | Change |
|---|---|
| 2026-07-28 | Story implemented (`dev-story`, story baseline `755bd7a`, executed at HEAD `8a2fb83`). Local coverage report shipped as a pinned ReportGenerator 5.5.11 renderer over the existing coverlet/OpenCover artifact; reconciliation against SonarCloud completed with the residual explained quantitatively; cost measured across 7 runs + 2 baselines. Status → review. |
