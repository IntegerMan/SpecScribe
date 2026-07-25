---
baseline_commit: 92fa58149253105a08dc458cfed05a95a989229b
epic: 25
nfr: NFR11
depends_on: [] # schedulable immediately — no gates
blocks: [25-2] # the quality gate + triage pass has nothing to gate without this workflow
amends_story: 16-2 # 16.2 HARDENS this workflow into a required gate; it must NOT create a second one
ships_product_code: false # dev-time only. The golden fingerprint MUST NOT move.
touches: [".github/workflows/**", ".gitignore", "README.md"] # NOT src/** and NOT tests/** except as AC #4 permits
---

# Story 25.1: SonarCloud Onboarding and Automated Analysis on Every Push to `main`

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the SpecScribe maintainer,
I want every push to `main` and every pull request to build, test, and be analyzed by SonarCloud on a clean checkout,
So that code-quality regressions surface automatically instead of being discovered during a hardening epic months later.

## ⛔ Read first — the four things that make this story unusual

1. **This is the repository's FIRST build+test CI workflow.** `.github/workflows/` contains exactly one file today
   — `publish-docs-live-pages.yml`, a Pages publish job. Nothing builds. Nothing tests. **The ~2,350-test suite
   has never run anywhere but the owner's Windows machine.** Treat "the suite is green in CI" as an unproven
   claim you are the first to test, not a formality.
2. **Story 16.2 is amended to extend this workflow, not create one.** Name the workflow and structure it so 16.2
   can add branch protection / required-check config on top. Two workflows that both build and test is the exact
   drift class this project has repeatedly paid for.
3. **This story ships NO product code.** Do not touch `src/SpecScribe/**`. The `GoldenContentFingerprint`
   constant must not move. If you find yourself editing a `.cs` file under `src/`, stop — you have left scope.
4. **A concurrent session is editing `src/` right now.** At authoring time the working tree had uncommitted
   Story 20.5 work (`src/SpecScribe/assets/plotly-hierarchy.min.js`, `tools/plotly-vendor/`,
   `src/SpecScribe/SpecScribe.csproj`). Per CLAUDE.md: **never `git reset --hard`, `git checkout --`, or
   `git clean`.** Do not "tidy" the tree. If a local `dotnet test` fails in `src/` code you did not write, that
   is the other session, not you — say so and move on.

## Acceptance Criteria

### AC #1 — Build, test, and analyze on a clean checkout

**Given** a push to `main` or a pull request
**When** CI runs
**Then** a workflow restores, builds, and executes `tests/SpecScribe.Tests` on a clean checkout, runs the
SonarScanner for .NET wrapping that build (begin → build → test → end), and uploads results to a SonarCloud
project bound to `IntegerMan/SpecScribe`
**And** the job fails on any build or test failure, and the workflow is independent of and does not disturb
`publish-docs-live-pages.yml`.

### AC #2 — Token handling and fork-PR safety

**Given** analysis requires a token
**When** the workflow authenticates
**Then** `SONAR_TOKEN` is read from a repository secret, no secret value is committed, and the workflow is safe
on pull requests from forks (analysis is skipped or runs without the token rather than leaking it)
**And** the SonarCloud project's visibility and the free-OSS-tier terms are recorded in the story record.

### AC #3 — The coverage decision is RECORDED, never left unstated

**Given** test coverage improves finding quality
**When** the analysis runs
**Then** the story records an explicit decision on coverage collection — collector, report format, upload path,
and the **measured** effect on suite runtime for a ~2,350-test suite — either implementing it or recording why it
is deferred, never leaving it unstated.

### AC #4 — Runner-OS honesty (this story's real risk, made explicit)

**Given** the suite has never executed outside the owner's Windows machine
**When** the workflow first runs green
**Then** the story record states which runner OS was chosen, **the evidence for it** (a full-suite pass, with the
pass/fail/skip counts), and — if a non-Windows runner was attempted — every test that behaved differently there
**And** any test changed to make CI pass is listed individually with its root cause, so a portability bug is
never disguised as a CI tweak. Changing a `.cs` file under `tests/` is permitted **only** under this AC and only
with that per-test justification; changing anything under `src/` is not.

## Tasks / Subtasks

- [ ] **Task 1 — Establish the SonarCloud project (out-of-band, owner action) (AC: #1, #2)**
  - [ ] Confirm/create the SonarCloud (SonarQube Cloud) organization bound to the `IntegerMan` GitHub account and
        a project for `IntegerMan/SpecScribe`. Record the exact **organization key** and **project key** — they
        are the `/o:` and `/k:` values and are frequently *not* the display names.
  - [ ] Set **Analysis Method → CI-based analysis** (turn Automatic Analysis **off**). Automatic Analysis does not
        cover C#; leaving it on can silently shadow the CI results.
  - [ ] Generate a token and add it as repository secret `SONAR_TOKEN`. **Never** paste the value into a file,
        a commit message, this story, or a log.
  - [ ] Record in Dev Notes: project visibility (public), the free-OSS-tier terms actually shown at signup, and
        the region (EU/global vs US — this changes the scanner invocation, see Dev Notes § Region).
  - [ ] If the owner has not yet done this, **stop and say so** rather than inventing keys. The workflow file can
        be written first; it cannot be verified green without the project.

- [ ] **Task 2 — Write `.github/workflows/build-test-analyze.yml` (AC: #1)**
  - [ ] Triggers: `push: branches: [main]` and `pull_request: branches: [main]`, plus `workflow_dispatch`.
        **Do not add a `paths:` filter** — a build+test gate that skips on some paths is not a gate. (Contrast
        `publish-docs-live-pages.yml`, which correctly *does* filter, because it only republishes docs.)
  - [ ] `actions/checkout@v4` with `fetch-depth: 0` (Sonar: "shallow clones should be disabled for a better
        relevancy of analysis"; also matches what the Pages workflow already does, for the same class of reason).
  - [ ] `actions/setup-dotnet@v4` with **`dotnet-version: "10.0.x"`** — see Dev Notes § The 9.0.x trap. Do not
        copy `9.0.x` from the neighbouring workflow.
  - [ ] `actions/setup-java@v4` with `distribution: temurin`, **`java-version: "21"`** — see Dev Notes § Java 21.
  - [ ] Cache the scanner + NuGet (`actions/cache@v4` on `~/.sonar/scanner` and `~/.nuget/packages`), or use
        `setup-dotnet`'s `cache: true` with `cache-dependency-path` pointing at **both** csproj files.
  - [ ] `dotnet tool install --global dotnet-sonarscanner` (or `--tool-path ./.sonar/scanner`).
  - [ ] Sequence, in this order and no other: `begin` → `dotnet build` → `dotnet test` → `end`. The scanner
        injects MSBuild targets during `begin`; a build that ran before `begin` produces an empty analysis.
  - [ ] Build command: `dotnet build SpecScribe.slnx --no-incremental --disable-build-servers` (Sonar's own
        documented .NET invocation — see Dev Notes § .slnx).
  - [ ] Test command: `dotnet test SpecScribe.slnx --no-build` plus whatever Task 4 decides about coverage.
  - [ ] Verify the job fails on a build failure **and** on a test failure. Prove it, don't assume it — an `end`
        step that runs unconditionally can mask a red `dotnet test`. Do not add `continue-on-error` or
        `if: always()` to the build or test steps.
  - [ ] Confirm no interaction with the Pages workflow: different `name:`, different job ids, and **no shared
        `concurrency.group`** (the Pages workflow uses `group: pages` — do not reuse it, or one will cancel the
        other).

- [ ] **Task 3 — Token handling and fork-PR safety (AC: #2)**
  - [ ] Pass the token only as `env: SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}` and reference it as
        `$SONAR_TOKEN` / `$env:SONAR_TOKEN` in the scanner steps. Never interpolate `${{ secrets.SONAR_TOKEN }}`
        directly inside a `run:` script body — that inlines the value into the rendered command.
  - [ ] Fork guard: secrets are **not** provided to `pull_request` runs from forks. Gate the three scanner steps
        (install / begin / end) on a token-present condition — e.g. a job-level `env` plus
        `if: env.SONAR_TOKEN != ''`, or `if: github.event.pull_request.head.repo.full_name == github.repository`.
        **Build and test must still run** on fork PRs; only analysis is skipped.
  - [ ] Do **not** switch the trigger to `pull_request_target` to get the secret. It runs untrusted PR code with
        write-scoped credentials — an unacceptable trade for a dashboard, and Sonar's own community threads record
        that it breaks PR decoration anyway.
  - [ ] Set `permissions:` explicitly at the top of the workflow, least-privilege (`contents: read`, plus
        `pull-requests: write` **only** if PR decoration proves to need it).
  - [ ] Verify: run the workflow once and confirm the raw logs contain no token value and no `***`-adjacent leak
        in a scanner echo. Record the check in Dev Notes.

- [ ] **Task 4 — The coverage decision (AC: #3)**
  - [ ] **Measure the baseline first**: run `dotnet test` locally with no coverage and record wall-clock time and
        the test count. This is the denominator for the whole decision; without it AC #3 is unmet.
  - [ ] Then measure **with** coverage. `coverlet.collector` **6.0.4 is already a `PackageReference`** in
        `tests/SpecScribe.Tests/SpecScribe.Tests.csproj` — do not add a coverage package, and do not reach for
        `coverlet.msbuild`.
  - [ ] Emit **OpenCover**, not the coverlet default (Cobertura). SonarScanner for .NET does **not** document
        Cobertura support for C#; coverlet's OpenCover output is what `sonar.cs.opencover.reportsPaths` expects:
        `dotnet test --collect:"XPlat Code Coverage;Format=opencover"`.
  - [ ] Pass the path on the **`begin`** step (not `end`):
        `/d:sonar.cs.opencover.reportsPaths="**/TestResults/**/coverage.opencover.xml"`.
  - [ ] Coverage must be produced **after `build` and before `end`** — i.e. by the `dotnet test` step already in
        the sequence. A report written after `end` is silently ignored.
  - [ ] Record the decision in Dev Notes with all four required fields: **collector** (coverlet.collector 6.0.4),
        **format** (OpenCover), **upload path** (the `sonar.cs.opencover.reportsPaths` glob), and **measured
        runtime delta** (baseline → with-coverage, in seconds and %). If the delta is judged unacceptable, that is
        a legitimate outcome — but then say the number and say "deferred because X", never leave it unstated.

- [ ] **Task 5 — Analysis scope: exclude what must not be analyzed (AC: #1)**
  - [ ] Vendored third-party assets **must** be excluded or the findings list is worthless on day one:
        `src/SpecScribe/assets/prism.js`, `src/SpecScribe/assets/prism.css`, and
        `src/SpecScribe/assets/plotly-hierarchy.min.js` (**1.2 MB minified**, landed by the concurrent Story 20.5
        session; see the Read-first note).
  - [ ] Also decide and record scope for: `spike/**` (throwaway spike code, by definition not maintained),
        `extension/node_modules/**`, `extension/dist/**`, `extension/bin/**`, `SpecScribeOutput/**` (generated),
        `artifacts/**`, `tools/**`, and `_bmad-output/**`.
  - [ ] Decide explicitly whether `extension/src/**` (the real, maintained TypeScript shim) is **in** scope. It
        is genuine first-party source; the default answer is yes. State the answer either way.
  - [ ] Mark `tests/SpecScribe.Tests/**` as test sources via `/d:sonar.test.exclusions` / the test-project
        convention so test code is not scored as production code.
  - [ ] Prefer a committed `sonar-project.properties`-equivalent set of `/d:` args in the workflow **or** the
        SonarCloud UI — pick one and say which, so the next maintainer knows where the truth lives. (Note: for
        the .NET scanner, `sonar-project.properties` is **not** read; exclusions go on the `begin` command or in
        the server-side project settings.)

- [ ] **Task 6 — Run it, and confront the runner OS honestly (AC: #4)**
  - [ ] Choose the runner OS. **Recommended default: `windows-latest`.** Rationale in Dev Notes § Runner OS —
        this is the first time the suite runs anywhere else, and `GoldenContentFingerprint` is a byte-exact
        assertion. Public repos get unlimited Actions minutes, so cost is not the tiebreaker.
  - [ ] Run the full suite on the chosen runner. Record **passed / failed / skipped** counts. The
        `[SkippableFact]` tests in `SiteGeneratorCommitDetailsTests.cs` / `PathUtilTests.cs` gate on git
        availability — git is present on GitHub runners, so expect them to **execute**, not skip. If they skip,
        something is wrong with the checkout.
  - [ ] **If you attempt `ubuntu-latest`** (a legitimate, cheaper-in-time choice): run the full suite there too
        and record every divergence. Known suspects, in order: `GoldenContentFingerprint` (case-sensitive FS,
        file ordering in `FingerprintTree`, and LF-vs-CRLF — the test normalizes CRLF, but there is **no
        `.gitattributes`** in this repo, so the checkout's line endings differ by platform), culture-sensitive
        date/number formatting, and path-case collisions. Encouraging counter-evidence: the tests use
        `Path.DirectorySeparatorChar` rather than literal `\`, no test hardcodes a `C:\` path, and
        `publish-docs-live-pages.yml` already runs the full generation path on `ubuntu-latest` successfully.
  - [ ] For each test that needed a change: name the test, the root cause, and why the change is a portability
        fix and not a weakened assertion. **Never** regenerate `GoldenContentFingerprint` to make CI green — that
        constant is a rendering-regression tripwire, and a platform-dependent fingerprint is a *finding*, not a
        maintenance chore. If the fingerprint differs by platform, report it and pick the runner that matches the
        constant.

- [ ] **Task 7 — Repository hygiene (AC: #1, #2)**
  - [ ] Add to `.gitignore` (verified absent at authoring time): `.sonarqube/`, `.sonar/`, and `TestResults/`.
        The existing `coverage*.xml` entry does **not** cover `TestResults/<guid>/coverage.opencover.xml`.
  - [ ] Add the SonarCloud quality-gate / analysis badge to `README.md` **only if** it renders green; a
        permanently-red badge on the front page is worse than none. (The gate itself is Story 25.2's — a badge
        here is optional polish, not an AC.)
  - [ ] Confirm `git status` shows no unintended files and that you have not staged the concurrent session's
        Story 20.5 work.

- [ ] **Task 8 — Record the handoff to Story 16.2 and Story 25.2 (AC: #1, #3)**
  - [ ] In Dev Notes, state: the workflow file path, the job name (16.2 needs it verbatim for the required-check
        setting), and what 16.2 still has to do (branch protection, required-status-check config, release-branch
        coverage, any release matrix).
  - [ ] State what 25.2 inherits: whether a quality gate is already attached, whether `sonar.qualitygate.wait` is
        set (default: **not** set here — failing the build on the gate is 25.2's decision, not this story's), and
        where the first analysis results live.

## Dev Notes

### Verified current state (checked 2026-07-25 at `92fa581`)

| Fact | Evidence |
|---|---|
| Only one workflow exists | `.github/workflows/publish-docs-live-pages.yml` — Pages publish only. No build, no test. |
| Solution is the **new `.slnx` format** | `SpecScribe.slnx` — two projects: `src/SpecScribe/SpecScribe.csproj`, `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`. |
| Both projects target **`net10.0`** | `SpecScribe.csproj:5`, `SpecScribe.Tests.csproj:3`. |
| Coverage collector **already referenced** | `coverlet.collector` 6.0.4 in `SpecScribe.Tests.csproj`. Do not add another. |
| Test framework | xunit 2.9.3 + `xunit.runner.visualstudio` 3.1.4 + `Microsoft.NET.Test.Sdk` 17.14.1 + `Xunit.SkippableFact` 1.4.13. Standard `dotnet test` (VSTest) works. |
| Suite size | 2,015 `[Fact]`/`[Theory]`/`[SkippableFact]` attributes across 116 files → ~2,350 test cases with `[Theory]` data. |
| Repo is public | `git@github.com:IntegerMan/SpecScribe.git` → free OSS tier + GitHub App path apply; no private-source-exposure question for **this** repo. |
| `.gitattributes` | **Does not exist.** Line endings are whatever the platform's `core.autocrlf` produces. Relevant to the Linux question. |
| Vendored JS/CSS in `src/` | `prism.js` (100 KB), `prism.css`, `plotly-hierarchy.min.js` (1.2 MB). All third-party. |

### § The 9.0.x trap — do NOT copy the neighbouring workflow's SDK pin

`publish-docs-live-pages.yml:40` pins `dotnet-version: "9.0.x"`, but every project in this repo targets
**`net10.0`** and has since the DocsForge→SpecScribe rename (`af1b543`). SDK 9 cannot build a `net10.0` project.
That workflow only survives because the hosted runner image ships a .NET 10 SDK alongside whatever `setup-dotnet`
adds. **Do not inherit that pin.** Set `10.0.x` explicitly in the new workflow. The owner's machine has SDK
10.0.301 (also 10.0.100 and 9.0.203).

*Out of scope for this story, worth noting in the record:* the Pages workflow's stale pin is a latent breakage —
it will fail the day the runner image drops the SDK it is accidentally relying on.

### § Java 21 — this became mandatory five days ago

SonarQube Cloud stopped supporting scanner runtimes below Java 21 on **2026-07-20**. Analyses on Java 17 now
**fail**, not warn. Two mitigations, use both:

- Add `actions/setup-java@v4` with `java-version: "21"`, `distribution: "temurin"` — explicit and cheap.
- JRE auto-provisioning (on by default in recent scanners) would also handle it, but relying on it silently
  makes the workflow's correctness depend on an undeclared default. Don't.

If you hit a Java error at the `begin` step, this is the first thing to check — the message is not always obvious.

### § Region — EU is the default and needs no host URL

For a SonarQube Cloud organization in the **EU/global** region, pass **no** `sonar.host.url` and no region flag.
For the **US** region (`sonarqube.us`), add `/d:sonar.region=us` to the `begin` step. Confirm which region the
organization was created in during Task 1 and record it — guessing here produces an authentication failure that
reads like a bad token.

### § The scanner invocation (verified against current SonarQube Cloud docs)

```
dotnet sonarscanner begin \
  /o:"<organization-key>" \
  /k:"<project-key>" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.cs.opencover.reportsPaths="**/TestResults/**/coverage.opencover.xml" \
  /d:sonar.exclusions="<see Task 5>"

dotnet build SpecScribe.slnx --no-incremental --disable-build-servers
dotnet test  SpecScribe.slnx --no-build --collect:"XPlat Code Coverage;Format=opencover"

dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"
```

- `--no-incremental` is Sonar's documented .NET invocation — an incremental build can skip compilation entirely
  and hand the scanner nothing.
- `--disable-build-servers` prevents MSBuild node processes from holding scanner DLLs locked (matters most on
  Windows runners).
- Latest `dotnet-sonarscanner` at authoring time: **11.2.1** (2026-04-02), compatible through net10.0.
- If the tool fails to launch on a runner that has only the .NET 10 runtime, set `DOTNET_ROLL_FORWARD: Major` as
  a job-level env before reaching for a second SDK install.

### § .slnx — low risk, but verify rather than assume

`dotnet` has supported `.slnx` since SDK 9.0.200, so `dotnet build SpecScribe.slnx` is fine on SDK 10.
SonarScanner for .NET works through **injected MSBuild targets**, not by parsing the solution, so the format
should be transparent to it — but Sonar's docs enumerate `.sln/.proj/.csproj/.vbproj` and do **not** name
`.slnx`, and community reports of `.slnx` friction exist. **Verify on the first green run** that both projects
appear in the analysis. If the scanner produces an empty or single-project analysis, the documented fallback is to
build the two `.csproj` files explicitly rather than converting the solution format — the `.slnx` file is
referenced by `publish-docs-live-pages.yml:10` and changing it would ripple.

### § Runner OS — the recommendation and the honest reason for it

**Recommend `windows-latest` for the first green run.** Not because Linux is wrong, but because:

- The suite has **never** run on Linux. `GoldenContentFingerprint` is a SHA-256 over every generated file. It
  normalizes the wall-clock footer, the `?v=<ModuleVersionId>` cache-bust, CRLF, and the product version — but
  not file-enumeration order or filesystem case sensitivity.
- There is no `.gitattributes`, so the working-tree line endings genuinely differ between a Windows and a Linux
  checkout of the same commit.
- The fingerprint constant was generated on Windows. If it differs on Linux, that is a **real portability
  finding** worth its own story — not something to paper over on day one of the repo's first CI.
- Sonar's own .NET sample workflow uses a Windows runner.
- Public repos get unlimited Actions minutes, so the usual Linux cost argument does not apply.

**Attempting `ubuntu-latest` is welcome** — it is faster and it would prove portability. Do it as a *second*
experiment, and report the divergences (AC #4) rather than quietly switching. Encouraging prior evidence: no test
hardcodes a Windows path, tests use `Path.DirectorySeparatorChar`, and `publish-docs-live-pages.yml` already runs
the full generation path on `ubuntu-latest` today.

### § What this story must NOT do

- **Must not move `GoldenContentFingerprint`.** Dev-time tooling only; the generated portal is untouched.
- **Must not edit `src/SpecScribe/**`.** If a Sonar finding tempts you to fix code — that's Story 25.2's triage
  pass, and structural/security/perf items route to Stories 17.1–17.3.
- **Must not define the quality gate.** AC #1 of Story 25.2 owns the gate conditions and what a failing gate
  blocks. Leave `sonar.qualitygate.wait` unset here.
- **Must not create branch protection / required checks.** That is the amended Story 16.2.
- **Must not add a second build+test workflow**, now or as a "temporary" one.

### Project Structure Notes

- New file: `.github/workflows/build-test-analyze.yml` (name it something 16.2 can plausibly keep — it will be
  referenced by name in a branch-protection rule).
- Modified: `.gitignore` (add `.sonarqube/`, `.sonar/`, `TestResults/`).
- Optionally modified: `README.md` (badge), `tests/SpecScribe.Tests/*.cs` (only under AC #4's justification rule).
- Untouched: everything under `src/`, `SpecScribe.slnx`, `publish-docs-live-pages.yml`.

### Testing

There is no unit test for a GitHub Actions workflow. The verification for this story is **the workflow itself
running green**, and the evidence is the run log. Specifically, the Completion Notes must carry:

1. A link or run id for a green `main` run **and** a green pull-request run.
2. The passed/failed/skipped counts from `dotnet test` in CI.
3. A deliberately-broken-build run **or** a deliberately-failing-test run showing the job goes red. AC #1's
   "fails on any build or test failure" is a claim that must be demonstrated, not asserted.
4. A fork-PR (or simulated token-absent) run showing build+test still run and analysis is skipped cleanly.
5. Confirmation that `publish-docs-live-pages.yml` still succeeded on the same push.

### References

- Epic + ACs: [epics.md § Epic 25 / Story 25.1](../planning-artifacts/epics.md) (lines 3959–3999)
- Amendment this story creates the dependency for: [epics.md § Story 16.2](../planning-artifacts/epics.md) (line 2684, `AMENDED 2026-07-25` comment block)
- Requirement: NFR11 — [epics.md:130](../planning-artifacts/epics.md)
- Origin + owner decisions D1–D4, evidence table: [sprint-change-proposal-2026-07-25.md § 1.4, § 2.a](../planning-artifacts/sprint-change-proposal-2026-07-25.md)
- Downstream: [epics.md § Story 25.2](../planning-artifacts/epics.md) (quality gate + triage), § Story 17.2 (supply-chain audit scope now includes the SonarScanner and CI actions — epics.md:2913)
- Working conventions (shared `main`, no destructive git, verify-after-edit): [CLAUDE.md](../../CLAUDE.md)
- Existing workflow to not disturb: [publish-docs-live-pages.yml](../../.github/workflows/publish-docs-live-pages.yml)
- Sonar docs consulted 2026-07-25: [GitHub Actions for SonarQube Cloud](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/ci-based-analysis/github-actions-for-sonarcloud) · [SonarScanner for .NET — using](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/scanners/sonarscanner-for-dotnet/using) · [.NET test coverage](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/test-coverage/dotnet-test-coverage) · [Scanner general requirements (Java 21)](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/scanners/scanner-environment/general-requirements) · [Reminder: upgrade to Java 21 by July 20, 2026](https://community.sonarsource.com/t/reminder-upgrade-to-java-21-for-sonarqube-cloud-analysis-by-july-20-2026/185820)

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

<!-- Required by the ACs — do not mark this story done without all six:
     1. SonarCloud org key, project key, region, visibility, and the free-OSS-tier terms as shown at signup (AC #2)
     2. Coverage decision: collector / format / upload path / MEASURED runtime delta baseline→with-coverage (AC #3)
     3. Runner OS chosen + full-suite passed/failed/skipped counts + evidence (AC #4)
     4. Every tests/**.cs change, individually justified with root cause (AC #4)
     5. Analysis scope: the final exclusion list and where it lives (workflow args vs SonarCloud UI) (Task 5)
     6. Handoff: workflow path + job name for Story 16.2; gate posture for Story 25.2 (Task 8)
-->

### File List
