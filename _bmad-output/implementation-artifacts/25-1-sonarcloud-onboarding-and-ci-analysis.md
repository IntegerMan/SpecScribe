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

Status: done <!-- 2026-07-26: both owner actions landed; analysis is live and green end to end.
See Dev Agent Record -> Completion Notes section 0. One known blind spot is in Open items item 5.
2026-07-26: code review complete — 2 decision-needed resolved by owner, 5 patch applied, 1 deferred to 17.2,
12 dismissed. See § Review Findings. -->

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

- [x] **Task 1 — Establish the SonarCloud project (out-of-band, owner action) (AC: #1, #2)**
  - [x] Confirm/create the SonarCloud (SonarQube Cloud) organization bound to the `IntegerMan` GitHub account and
        a project for `IntegerMan/SpecScribe`. Record the exact **organization key** and **project key** — they
        are the `/o:` and `/k:` values and are frequently *not* the display names.
  - [x] Set **Analysis Method → CI-based analysis** (turn Automatic Analysis **off**). Automatic Analysis does not
        cover C#; leaving it on can silently shadow the CI results.
        → ✅ **DONE by the owner 2026-07-26.** The story's stated reason is out of date — Automatic Analysis
        DID cover C# (26,195 lines at revision `611097d`) — but the instruction was right, for a harder reason:
        SonarQube Cloud **rejects** CI analysis while it is enabled. Proven, not assumed: run `30184074554`
        failed at `SonarScanner end` with `ERROR: You are running CI analysis while Automatic Analysis is
        enabled.`, and the next run after the toggle went green.
  - [x] Generate a token and add it as repository secret `SONAR_TOKEN`. **Never** paste the value into a file,
        a commit message, this story, or a log.
        → ✅ **DONE by the owner 2026-07-26** (secret created 02:07:35Z). No token value was ever read,
        requested, or logged by this session. Leak check on the first tokened run: **zero** raw-token matches in
        the full log; the only form present is the literal `sonar.token="$env:SONAR_TOKEN"`.
  - [x] Record in Dev Notes: project visibility (public), the free-OSS-tier terms actually shown at signup, and
        the region (EU/global vs US — this changes the scanner invocation, see Dev Notes § Region).
        → **Partially done.** Visibility (`public`) and region (**EU/global**) recorded and verified via the
        public API; org key resolved to **`integerman-github`**, not the guessed `integerman`. The
        free-OSS-tier signup wording was **not captured** — signup happened out-of-band and inventing it
        would be worse than recording the gap. The org/project/region values are now **proven** rather than
        merely read: run `30184074554`'s `begin` step authenticated successfully, which a wrong `/o:` cannot.
  - [x] If the owner has not yet done this, **stop and say so** rather than inventing keys. The workflow file can
        be written first; it cannot be verified green without the project.

- [x] **Task 2 — Write `.github/workflows/build-test-analyze.yml` (AC: #1)**
  - [x] Triggers: `push: branches: [main]` and `pull_request: branches: [main]`, plus `workflow_dispatch`.
        **Do not add a `paths:` filter** — a build+test gate that skips on some paths is not a gate. (Contrast
        `publish-docs-live-pages.yml`, which correctly *does* filter, because it only republishes docs.)
  - [x] `actions/checkout@v4` with `fetch-depth: 0` (Sonar: "shallow clones should be disabled for a better
        relevancy of analysis"; also matches what the Pages workflow already does, for the same class of reason).
  - [x] `actions/setup-dotnet@v4` with **`dotnet-version: "10.0.x"`** — see Dev Notes § The 9.0.x trap. Do not
        copy `9.0.x` from the neighbouring workflow.
  - [x] `actions/setup-java@v4` with `distribution: temurin`, **`java-version: "21"`** — see Dev Notes § Java 21.
  - [x] Cache the scanner + NuGet (`actions/cache@v4` on `~/.sonar/scanner` and `~/.nuget/packages`), or use
        `setup-dotnet`'s `cache: true` with `cache-dependency-path` pointing at **both** csproj files.
  - [x] `dotnet tool install --global dotnet-sonarscanner` (or `--tool-path ./.sonar/scanner`).
  - [x] Sequence, in this order and no other: `begin` → `dotnet build` → `dotnet test` → `end`. The scanner
        injects MSBuild targets during `begin`; a build that ran before `begin` produces an empty analysis.
  - [x] Build command: `dotnet build SpecScribe.slnx --no-incremental --disable-build-servers` (Sonar's own
        documented .NET invocation — see Dev Notes § .slnx).
  - [x] Test command: `dotnet test SpecScribe.slnx --no-build` plus whatever Task 4 decides about coverage.
  - [x] Verify the job fails on a build failure **and** on a test failure. Prove it, don't assume it — an `end`
        step that runs unconditionally can mask a red `dotnet test`. Do not add `continue-on-error` or
        `if: always()` to the build or test steps.
  - [x] Confirm no interaction with the Pages workflow: different `name:`, different job ids, and **no shared
        `concurrency.group`** (the Pages workflow uses `group: pages` — do not reuse it, or one will cancel the
        other).

- [x] **Task 3 — Token handling and fork-PR safety (AC: #2)**
  - [x] Pass the token only as `env: SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}` and reference it as
        `$SONAR_TOKEN` / `$env:SONAR_TOKEN` in the scanner steps. Never interpolate `${{ secrets.SONAR_TOKEN }}`
        directly inside a `run:` script body — that inlines the value into the rendered command.
  - [x] Fork guard: secrets are **not** provided to `pull_request` runs from forks. Gate the three scanner steps
        (install / begin / end) on a token-present condition — e.g. a job-level `env` plus
        `if: env.SONAR_TOKEN != ''`, or `if: github.event.pull_request.head.repo.full_name == github.repository`.
        **Build and test must still run** on fork PRs; only analysis is skipped.
  - [x] Do **not** switch the trigger to `pull_request_target` to get the secret. It runs untrusted PR code with
        write-scoped credentials — an unacceptable trade for a dashboard, and Sonar's own community threads record
        that it breaks PR decoration anyway.
  - [x] Set `permissions:` explicitly at the top of the workflow, least-privilege (`contents: read`, plus
        `pull-requests: write` **only** if PR decoration proves to need it).
  - [x] Verify: run the workflow once and confirm the raw logs contain no token value and no `***`-adjacent leak
        in a scanner echo. Record the check in Dev Notes.
        → **Partially done.** Verified in the token-absent shape across five runs: no secret appears in any
        rendered `run:` body. Cannot be completed **with a token present** until owner action A lands.

- [x] **Task 4 — The coverage decision (AC: #3)**
  - [x] **Measure the baseline first**: run `dotnet test` locally with no coverage and record wall-clock time and
        the test count. This is the denominator for the whole decision; without it AC #3 is unmet.
  - [x] Then measure **with** coverage. `coverlet.collector` **6.0.4 is already a `PackageReference`** in
        `tests/SpecScribe.Tests/SpecScribe.Tests.csproj` — do not add a coverage package, and do not reach for
        `coverlet.msbuild`.
  - [x] Emit **OpenCover**, not the coverlet default (Cobertura). SonarScanner for .NET does **not** document
        Cobertura support for C#; coverlet's OpenCover output is what `sonar.cs.opencover.reportsPaths` expects:
        `dotnet test --collect:"XPlat Code Coverage;Format=opencover"`.
  - [x] Pass the path on the **`begin`** step (not `end`):
        `/d:sonar.cs.opencover.reportsPaths="**/TestResults/**/coverage.opencover.xml"`.
  - [x] Coverage must be produced **after `build` and before `end`** — i.e. by the `dotnet test` step already in
        the sequence. A report written after `end` is silently ignored.
  - [x] Record the decision in Dev Notes with all four required fields: **collector** (coverlet.collector 6.0.4),
        **format** (OpenCover), **upload path** (the `sonar.cs.opencover.reportsPaths` glob), and **measured
        runtime delta** (baseline → with-coverage, in seconds and %). If the delta is judged unacceptable, that is
        a legitimate outcome — but then say the number and say "deferred because X", never leave it unstated.

- [x] **Task 5 — Analysis scope: exclude what must not be analyzed (AC: #1)**
  - [x] Vendored third-party assets **must** be excluded or the findings list is worthless on day one:
        `src/SpecScribe/assets/prism.js`, `src/SpecScribe/assets/prism.css`, and
        `src/SpecScribe/assets/plotly-hierarchy.min.js` (**1.2 MB minified**, landed by the concurrent Story 20.5
        session; see the Read-first note).
  - [x] Also decide and record scope for: `spike/**` (throwaway spike code, by definition not maintained),
        `extension/node_modules/**`, `extension/dist/**`, `extension/bin/**`, `SpecScribeOutput/**` (generated),
        `artifacts/**`, `tools/**`, and `_bmad-output/**`.
  - [x] Decide explicitly whether `extension/src/**` (the real, maintained TypeScript shim) is **in** scope. It
        is genuine first-party source; the default answer is yes. State the answer either way.
  - [x] Mark `tests/SpecScribe.Tests/**` as test sources via `/d:sonar.test.exclusions` / the test-project
        convention so test code is not scored as production code.
  - [x] Prefer a committed `sonar-project.properties`-equivalent set of `/d:` args in the workflow **or** the
        SonarCloud UI — pick one and say which, so the next maintainer knows where the truth lives. (Note: for
        the .NET scanner, `sonar-project.properties` is **not** read; exclusions go on the `begin` command or in
        the server-side project settings.)

- [x] **Task 6 — Run it, and confront the runner OS honestly (AC: #4)**
  - [x] Choose the runner OS. **Recommended default: `windows-latest`.** Rationale in Dev Notes § Runner OS —
        this is the first time the suite runs anywhere else, and `GoldenContentFingerprint` is a byte-exact
        assertion. Public repos get unlimited Actions minutes, so cost is not the tiebreaker.
  - [x] Run the full suite on the chosen runner. Record **passed / failed / skipped** counts. The
        `[SkippableFact]` tests in `SiteGeneratorCommitDetailsTests.cs` / `PathUtilTests.cs` gate on git
        availability — git is present on GitHub runners, so expect them to **execute**, not skip. If they skip,
        something is wrong with the checkout.
  - [x] **If you attempt `ubuntu-latest`** (a legitimate, cheaper-in-time choice): run the full suite there too
        and record every divergence. Known suspects, in order: `GoldenContentFingerprint` (case-sensitive FS,
        file ordering in `FingerprintTree`, and LF-vs-CRLF — the test normalizes CRLF, but there is **no
        `.gitattributes`** in this repo, so the checkout's line endings differ by platform), culture-sensitive
        date/number formatting, and path-case collisions. Encouraging counter-evidence: the tests use
        `Path.DirectorySeparatorChar` rather than literal `\`, no test hardcodes a `C:\` path, and
        `publish-docs-live-pages.yml` already runs the full generation path on `ubuntu-latest` successfully.
  - [x] For each test that needed a change: name the test, the root cause, and why the change is a portability
        fix and not a weakened assertion. **Never** regenerate `GoldenContentFingerprint` to make CI green — that
        constant is a rendering-regression tripwire, and a platform-dependent fingerprint is a *finding*, not a
        maintenance chore. If the fingerprint differs by platform, report it and pick the runner that matches the
        constant.

- [x] **Task 7 — Repository hygiene (AC: #1, #2)**
  - [x] Add to `.gitignore` (verified absent at authoring time): `.sonarqube/`, `.sonar/`, and `TestResults/`.
        The existing `coverage*.xml` entry does **not** cover `TestResults/<guid>/coverage.opencover.xml`.
        → **Story premise corrected.** `.sonarqube/` and `.sonar/` were indeed absent and were added. But
        `TestResults/` was **already covered** — `git check-ignore -v` resolves
        `tests/SpecScribe.Tests/TestResults/<guid>/coverage.opencover.xml` to **`.gitignore:47`,
        `[Tt]est[Rr]esult*/`**. No redundant rule was added; the reasoning is left as a comment in
        `.gitignore` so the next maintainer does not "re-fix" it.
  - [x] Add the SonarCloud quality-gate / analysis badge to `README.md` **only if** it renders green; a
        permanently-red badge on the front page is worse than none. (The gate itself is Story 25.2's — a badge
        here is optional polish, not an AC.)
        → **Deliberately NOT added**, and the reason has changed: analysis is now green so a badge *would*
        render, but a quality-gate badge should follow Story 25.2's gate decision rather than precede it.
        Instead `README.md` gained a **Continuous integration** section linking to the new
        [`docs/SonarCloudSetup.md`](../../docs/SonarCloudSetup.md), which is the durable thing a reader needs.
  - [x] Confirm `git status` shows no unintended files and that you have not staged the concurrent session's
        Story 20.5 work.

- [x] **Task 8 — Record the handoff to Story 16.2 and Story 25.2 (AC: #1, #3)**
  - [x] In Dev Notes, state: the workflow file path, the job name (16.2 needs it verbatim for the required-check
        setting), and what 16.2 still has to do (branch protection, required-status-check config, release-branch
        coverage, any release matrix).
  - [x] State what 25.2 inherits: whether a quality gate is already attached, whether `sonar.qualitygate.wait` is
        set (default: **not** set here — failing the build on the gate is 25.2's decision, not this story's), and
        where the first analysis results live.

### Review Findings

Scoped to this story's own commits (`611097d` through `cd7f302`), excluding sibling-story commits
(`bcca682`, `1116e13`, `261b300`) that landed interleaved on shared `main` per CLAUDE.md's review-scoping
convention. Blind Hunter, Edge Case Hunter, and Acceptance Auditor ran in parallel; 20 raw findings triaged
to 2 decision-needed, 5 patch, 1 defer, 12 dismissed as noise or already-accepted trade-offs.

- [x] [Review][Decision] Standing `portability-probe` (ubuntu-latest) job runs on every future push/PR indefinitely, beyond Task 6's one-time "attempt ubuntu-latest" framing. **Owner decision (2026-07-26): keep it permanent** — non-gating, cheap on a public repo (unlimited Actions minutes), ongoing portability signal for free. [.github/workflows/build-test-analyze.yml:172-204]
- [x] [Review][Decision] `SonarScanner begin`/`end` steps have no failure isolation from Build/Test — a SonarCloud outage/auth hiccup would fail the whole required `build-test-analyze` job. **Owner decision (2026-07-26): isolate scanner failures from Build/Test** — see the applied patch below.
- [x] [Review][Patch] Applied — documented that `portability-probe` is a deliberate permanent, non-gating CI job (owner-confirmed 2026-07-26), not the one-time Task 6 experiment it started as. [25-1-sonarcloud-onboarding-and-ci-analysis.md § Runner OS]
- [x] [Review][Patch] Applied — added `continue-on-error: true` to both `SonarScanner begin` and `SonarScanner end`, so a SonarCloud outage or auth error cannot fail the `build-test-analyze` required check; Build and Test are untouched and still gate normally. [.github/workflows/build-test-analyze.yml:114-150]
- [x] [Review][Patch] Applied — added the 4 missing files (`README.md`, `docs/SonarCloudSetup.md`, `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorEpicsRemovalTests.cs`) to the File List, which had undersold this story's own scope. [25-1-sonarcloud-onboarding-and-ci-analysis.md § File List]
- [x] [Review][Patch] Applied — Dev Notes §5's exclusion table was missing 4 entries (`_bmad/**`, `.claude/**`, `.agents/**`, `chat.json`) that §0b documents adding via commit `ab7a25a`; the table now matches the shipped `/d:sonar.exclusions` string verbatim. [25-1-sonarcloud-onboarding-and-ci-analysis.md § Dev Notes § 5]
- [x] [Review][Patch] Applied — `IsCopiedAsset`/`IsVendoredAsset` were two independently hand-maintained allowlists (verified currently complete — 6 `CopyEmbeddedAsset` call sites in `SiteGenerator.cs` mapped 1:1 to the two lists, but nothing tied them to each other). Merged into a single `KnownStaticAssets` map so the two predicates read from one source of truth and cannot silently disagree. [tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:1104-1142]
- [x] [Review][Patch] Applied — `VendoredAssetToken` hashed via `File.ReadAllText(...).Replace(...)` + `Encoding.UTF8.GetBytes(...)`, a text round-trip that silently strips a BOM and replaces invalid byte sequences, undermining the token's exact-content-identity claim. Replaced with a raw-byte CRLF fold (`FoldCrLfBytes`) with no text-encoding round-trip. [tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:1147-1172]
- [x] [Review][Patch] Applied — added a sentence next to the frontmatter's "`GoldenContentFingerprint` MUST NOT move" line explicitly citing the AC #4 carve-out that licenses this story's own regeneration, so a future reviewer reading only that line doesn't misread it as a flat violation. [25-1-sonarcloud-onboarding-and-ci-analysis.md § What this story must NOT do]
- [x] [Review][Defer] Sonar-scanner `actions/cache@v4` key has no version component, so `dotnet tool update dotnet-sonarscanner` never re-runs once cached — deferred, pre-existing open question. This is the same "scanner version currently unpinned" call the story already routes to Story 17.2 ("pinning it is a legitimate 17.2 call... flagged here rather than decided unilaterally"); the cache-key gap is the flip side of that same open decision, not a new defect. [.github/workflows/build-test-analyze.yml:100-113]

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

**⚠️ Scope decision 2026-07-26 (code review).** What shipped is a *standing* `portability-probe` job that runs
on every future push and PR, not the one-time experiment this section describes. Flagged in review as a
"decision needed" because it's a permanent doubling of CI compute per push beyond what this task asked for.
**Owner decision: keep it permanent** — it is non-gating, the repo has unlimited Actions minutes as a public
project, and the ongoing portability signal is worth the cost. No workflow change made; recorded here so a
future reader does not mistake the standing job for scope creep against this section's original framing.

### § What this story must NOT do

- **Must not move `GoldenContentFingerprint`.** Dev-time tooling only; the generated portal is untouched.
  **⚠️ Clarified 2026-07-26 (code review):** this story's own work regenerates the constant anyway (§ 4) —
  that is not a violation of this line. AC #4's carve-out is what licenses it: "changing a `.cs` file under
  `tests/` is permitted only under this AC and only with that per-test justification," and § 4 documents the
  per-change root cause for each regeneration. This line prohibits an *unjustified* regeneration to paper over
  a real rendering change, not the portability-normalization fix AC #4 explicitly allows.
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

`claude-opus-5` (Claude Code, `/bmad-dev-story 25.1`), 2026-07-25.

### Debug Log References

Local measurement scripts and raw logs live in the session scratchpad (not committed):
`baseline.ps1`, `paired.ps1`, `baseline.log`, `paired.log`, `p{1,2}-{nocov,cov}.txt`. Their
findings are summarized under **§ Coverage decision** below. **They are not the evidence for
AC #3** — see that section for why the local machine could not produce a valid measurement.

Environment facts established while implementing:

| Probe | Result |
|---|---|
| `python3` | Not on PATH (Windows Store alias stub). `python` works — the resolver script ran under `python`. |
| `gh` CLI | **Was not installed.** Installed via `winget install GitHub.cli` → v2.96.0. Owner then authenticated (`gh auth login`, scopes `gist, read:org, repo, workflow`). |
| SSH to GitHub | **No keys in `~/.ssh`** — `git@github.com` returns `Permission denied (publickey)`. Pushes go over HTTPS via Git Credential Manager (`credential.helper=manager`), verified with `git push --dry-run` before any real push. |
| Local SDK | `dotnet --version` → **10.0.301**. |
| Logical cores (owner machine) | 16. GitHub hosted runners have 4 — relevant to § The 15-second git-subprocess timeout. |

### Completion Notes List

<!-- Required by the ACs — do not mark this story done without all six:
     1. SonarCloud org key, project key, region, visibility, and the free-OSS-tier terms as shown at signup (AC #2)
     2. Coverage decision: collector / format / upload path / MEASURED runtime delta baseline→with-coverage (AC #3)
     3. Runner OS chosen + full-suite passed/failed/skipped counts + evidence (AC #4)
     4. Every tests/**.cs change, individually justified with root cause (AC #4)
     5. Analysis scope: the final exclusion list and where it lives (workflow args vs SonarCloud UI) (Task 5)
     6. Handoff: workflow path + job name for Story 16.2; gate posture for Story 25.2 (Task 8)
-->

#### 0. ✅ RESOLVED 2026-07-26 — both owner actions are done and the analysis is live

The owner added the `SONAR_TOKEN` secret (02:07:35Z) and disabled Automatic Analysis. **AC #1 is now
satisfied end to end**: run [`30184223152`](https://github.com/IntegerMan/SpecScribe/actions/runs/30184223152)
executed `begin → build → test → end` green and uploaded to SonarCloud (analysis recorded `2026-07-26T02:17:11Z`
on revision `f1fcdb0`).

The intermediate run [`30184074554`](https://github.com/IntegerMan/SpecScribe/actions/runs/30184074554) is worth
keeping in the record because it is the exact failure this story predicted, quoted verbatim from the log:

```
ERROR: You are running CI analysis while Automatic Analysis is enabled. Please consider disabling one or the other.
```

`Install` and `begin` both succeeded on that run, which independently **proves the org key
`integerman-github`, the project key `IntegerMan_SpecScribe`, and the EU/global region are all correct** — a
wrong org key fails at `begin` with an authentication error instead.

The original blocked note is preserved below for the record.

<details>
<summary>Original blocked status (2026-07-25)</summary>

#### ⛔ Status: NOT complete — two owner actions block the analysis half of AC #1

Everything that can be built, run, and proven from this session **is done and evidenced**: the workflow
exists, the gate is green on `main`, red-on-build-failure and red-on-test-failure are both demonstrated,
the fork/token-absent path is demonstrated, the runner-OS question is answered with counts, the coverage
decision is made with measured CI numbers, the exclusion list is settled, and the 16.2 / 25.2 handoff is
written.

**What is NOT proven: a single line of analysis has ever reached SonarCloud from CI.** Two actions are
required, and both are outside what this session may perform:

| # | Action | Why this session cannot do it | Consequence until done |
|---|---|---|---|
| **A** | Create a SonarCloud token and add it as repository secret **`SONAR_TOKEN`** | Handling a credential value. Verified absent: `gh api repos/IntegerMan/SpecScribe/actions/secrets` → `{"total_count":0,"secrets":[]}` | Every run takes the token-absent path. Build + test gate works; **no analysis is uploaded**. |
| **B** | SonarCloud → **Administration → Analysis Method → turn Automatic Analysis OFF** (select CI-based analysis) | Requires SonarCloud project-admin rights | **Hard blocker.** SonarQube Cloud rejects CI-based analysis while Automatic Analysis is enabled. With A done but not B, the first tokened run **fails at `SonarScanner end`**. |

Do **B before or with A**, not after. Once both are done, one push to `main` completes AC #1, and the three
follow-ups in § Open items can be closed.

</details>

#### 0b. First analysis results, and the exclusion list this forced open

The first real analysis exposed that the exclusion list — written by **enumerating directories rather than by
measuring**, because no analysis had ever run to check it — left roughly a quarter of the codebase in scope
that this project does not author. Widened in commit `ab7a25a`; both columns are measured, not estimated:

| Measure | First analysis | After widening | |
|---|---|---|---|
| ncloc | 44,545 | **32,788** | −11,757 |
| files | 191 | **149** | −42 |
| Language mix | `cs=25591; css=5581; js=28; json=6121; py=4393; ts=923; web=1658; yaml=250` | `cs=25591; css=5581; js=28; json=499; ts=923; yaml=166` | Python gone entirely |
| Duplication | 12.1% | **0.9%** | the 12.1% was almost entirely `.claude/` and `.agents/` holding **the same skill packs twice** |
| Vulnerabilities | 167 | 155 | |
| Coverage | 82.6% | **89.8%** | |

What was still in scope and should not have been: **`chat.json`** at the repo root (4,861 lines), the installed
BMad skill packs under **`.claude/`** (3,274) and **`.agents/`** (3,197, the same packs duplicated), and
**`_bmad/scripts`** (425). None are authored here. `extension/src` remains deliberately **in** scope.

> **Lesson for Story 25.2 and for any future exclusion work:** an exclusion list cannot be validated by reading
> it. This one looked complete and was ~26% wrong. Validate against `api/measures/component_tree` after the
> first analysis, sorted by ncloc — the offenders are obvious there and invisible in review.

**Coverage is confirmed working end to end at 89.8%**, which closes the last piece of AC #3: the
`sonar.cs.opencover.reportsPaths` glob, the OpenCover format, and the `begin`-step placement are all correct in
practice, not just on paper.

#### 1. SonarCloud project facts (AC #2) — resolved from the public API, not from guesswork

The story's Task 1 warned that the organization key is "frequently *not* the display name". It was not.
Rather than accept a guess, the values were read from SonarQube Cloud's unauthenticated public API for
this public project:

`GET https://sonarcloud.io/api/components/show?component=IntegerMan_SpecScribe`

| Field | Value | How established |
|---|---|---|
| **Organization key (`/o:`)** | **`integerman-github`** | API `component.organization`. A first draft of the workflow used `integerman` — the conventional lowercase-login guess — and it was **wrong**. Corrected before the first push. |
| **Project key (`/k:`)** | `IntegerMan_SpecScribe` | API `component.key`; matches what the owner reported. |
| **Region** | **EU / global** | `sonarcloud.io` answers; `sonarqube.us/api/components/show?...` returns **404** for the same component. Therefore **no `sonar.host.url` and no `/d:sonar.region`** are passed. |
| **Visibility** | **`public`** | API `component.visibility`. |
| **Free-OSS-tier terms** | Not recorded — see the honesty note below. | — |

**Honesty note on the free-OSS-tier terms.** AC #2 asks for "the free-OSS-tier terms actually shown at
signup". The owner completed signup out-of-band and those screens were not shown to this session, so
**this sub-clause is recorded as not captured rather than invented.** What *is* verified from the API is
the part that carries the actual risk: the project is `public`, which is the precondition the free OSS
tier attaches to. If the exact tier wording is wanted in the record, it has to come from the owner.

**Token secrecy (AC #2).** No token value was ever read, requested, typed, or logged by this session. The
workflow references the secret only as a job-level `env` binding and dereferences it as
`$env:SONAR_TOKEN`; it is never interpolated into a `run:` body.

#### 2. Two owner actions were still outstanding at implementation time — both verified, not assumed

**(a) `SONAR_TOKEN` repository secret does not exist.**
`gh api repos/IntegerMan/SpecScribe/actions/secrets` → `{"total_count":0,"secrets":[]}`.
The repo has **zero** Actions secrets. Creating it is a credential-handling action this session must not
perform. Until the owner adds it, every run takes the token-absent path: build + test execute, the three
scanner steps skip. That path is itself AC #2 evidence (see § CI evidence), but no analysis is uploaded.

**(b) SonarCloud Automatic Analysis is ON and has already analyzed this repo.**
This was *not* visible from the repo side and would have silently broken the first tokened run.
`GET /api/project_analyses/search?project=IntegerMan_SpecScribe` returns one analysis dated
`2026-07-25T20:54:41+0000` at revision **`611097d`** — this story's parent commit. Nobody ran CI; that is
Automatic Analysis.

Its measures show exactly why Task 5 exists, and why leaving Automatic Analysis on is not acceptable:

```
ncloc                       46,108
ncloc_language_distribution cs=26195; css=5562; js=788; json=6222; py=4393; ts=1095; web=1699; yaml=154
vulnerabilities             173
bugs                        14
code_smells                 515
```

`py=4393` is `_bmad/scripts/**` tooling; `json=6222` is largely generated/vendored; `js=788` and much of
`css=5562` are the vendored Prism/Plotly assets. The **173 "vulnerabilities" are an artifact of analyzing
content this project does not author** — a findings list that is worthless on day one, precisely as the
story predicted. The CI-based analysis configured here excludes all of it.

> **Correction to the story's stated rationale.** Task 1 says to disable Automatic Analysis because
> "Automatic Analysis does not cover C#". That is now **out of date** — the run above analyzed 26,195
> lines of C#. The *instruction* nevertheless stands, and more urgently than the story implies: SonarQube
> Cloud **rejects CI-based analysis while Automatic Analysis is enabled**, so this is a hard blocker for
> AC #1, not a tidiness item. Disabling it requires SonarCloud project-admin rights and cannot be done
> from this session.

#### 3. CI evidence — the five things the Testing section requires

All five run against `windows-latest` for the gate job and `ubuntu-latest` for the non-gating probe.

| # | Requirement | Run | Result |
|---|---|---|---|
| 1a | Green run on `main` | [`30175713872`](https://github.com/IntegerMan/SpecScribe/actions/runs/30175713872) | ✅ **success** — `build-test-analyze` green, 2390/2390 |
| 1b | Green run on a pull request | [`30176207551`](https://github.com/IntegerMan/SpecScribe/actions/runs/30176207551) (PR [#2](https://github.com/IntegerMan/SpecScribe/pull/2)) | ✅ **success** — `pull_request` trigger fires and the gate is green. Same-repo PR, so the token-absent path was still taken (no secret exists yet). |
| 2 | Passed/failed/skipped from `dotnet test` in CI | `30175713872` | Windows **2390 passed / 0 failed / 0 skipped**; Ubuntu **2388 / 2 / 0** |
| 3a | A deliberately-failing **test** turns the job red | [`30175320065`](https://github.com/IntegerMan/SpecScribe/actions/runs/30175320065) | ✅ **failure** — `Test` red ⇒ job red, `SonarScanner end` **skipped**. (Not staged: this was the genuine first run, red on a real pre-existing bug. Stronger evidence than a planted one.) |
| 3b | A deliberately-broken **build** turns the job red | `30175978438` | ✅ **failure** — a planted compile error made `Build` red; `Test` **skipped**, `SonarScanner end` **skipped**. Run performed on a throwaway branch, since deleted. |
| 4 | Token-absent run still builds + tests, skips analysis cleanly | `30175713872` (and every run so far) | ✅ `Install scanner`, `SonarScanner begin`, `SonarScanner end` all **skipped**; `Build` and `Test` both **ran and passed**. The job log shows `SONAR_TOKEN:` resolving to empty. This is the exact code path a fork PR takes. |
| 5 | `publish-docs-live-pages.yml` undisturbed | `30176316010` + `30176315939` | ✅ **Both workflows ran on the same push (the PR #2 merge) and both succeeded.** Neither cancelled the other; the Pages job took 1 m 5 s and the gate 2 m 5 s, concurrently. On the two earlier pushes (`30175320065`, `30175713872`) Pages correctly **did not** trigger, because neither touched a path in its `paths:` filter. No shared `concurrency.group` — this workflow uses `build-test-analyze-${{ github.ref }}`, not `pages`. |

**On item 3a — AC #1 is demonstrated, not asserted, in both directions.** A `Build` failure and a `Test` failure were each shown to turn the job red, and in both cases `SonarScanner end` was skipped rather than running unconditionally. That was the specific hazard the story called out.

**Token-leak check (Task 3) — partially verified, and the gap is stated.** The rendered `run:` bodies in the logs contain no secret: the token is bound only as a job-level `env` and dereferenced as `$env:SONAR_TOKEN`. What **cannot** be verified yet is the same check *with a real token present*, because no token exists (see § 2). That check must be repeated on the first tokened run.

#### 4. Runner OS and the portability findings (AC #4)

**Chosen runner: `windows-latest`.** Evidence, from run `30175713872`:

| Runner | Passed | Failed | Skipped | Total | Suite duration |
|---|---|---|---|---|---|
| **`windows-latest`** (gate) | **2390** | **0** | **0** | 2390 | 49 s (with coverage) |
| `ubuntu-latest` (non-gating probe) | 2388 | **2** | 0 | 2390 | 14 s |

**Skips: zero on both runners.** The story predicted the `[SkippableFact]` git-gated tests would *execute* rather than skip, and they did. Beyond that prediction: the three symlink tests that skip on the owner's machine for want of the privilege to create symlinks (`PathUtilTests` ×2, `SiteGeneratorWebviewTests` ×1) **also executed and passed on both runners**. CI exercises strictly more of this suite than the owner's machine does.

**`ubuntu-latest` divergences — 2, both real, neither papered over:**

1. ~~**`GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`** — Linux produces a
   different hash; "the story's central prediction, confirmed: the fingerprint is platform-dependent."~~
   **⚠️ RETIRED 2026-07-26 — this conclusion was WRONG, and the correction matters more than the original
   finding.** The fingerprint was never platform-dependent. Linux now produces the **identical** hash
   (`91c3aeb4…`) as Windows local and Windows CI. Two independent defects in the *test's own normalization*
   made the constant non-portable, and both are fixed (commit `98a90c6`, test-only):

   - **Checkout dependence.** `VendoredAssetToken` SHA-256'd the vendored plotly bundle's **raw bytes**,
     bypassing `NormalizeVolatile`. With no `.gitattributes`, that asset is text-classified and materializes
     as CRLF where `core.autocrlf=true` and LF everywhere else — `git ls-files --eol` reports
     `i/lf w/crlf attr/`, a 48-byte difference.
   - **Date and time-zone dependence — the serious one.** `FoldToday` rewrites *today's* date to
     `<date-iso>`. Both are exactly ten characters, so the substitution is **length-neutral and invisible to
     any size check**. `specscribe.css` carries a dated *source comment* (`[owner verify round 2026-07-25]`),
     so on the single calendar day that date was "today" it got folded and on every other day it did not.
     **The constant depended on the wall clock**: captured on a box whose local date was 2026-07-25, it
     failed on runners already at 2026-07-26 UTC — and would have failed on the owner's own machine the next
     morning with no code change behind it, reading as a rendering regression and inviting exactly the
     needless regeneration the story warns against.

   The constant was regenerated `8af72043` → `91c3aeb4` as a deliberate **normalization** change with no
   rendering behind it, and is the **first golden constant in this project confirmed on more than one
   machine**: stable across two repeated local runs and byte-identical on the owner's Windows box,
   `windows-latest`, and `ubuntu-latest`. Every previous constant was only ever confirmed on one box.

2. **`PathUtilTests.EscapesRepoRoot_ChecksTheLeadingSegment_NotABareSubstring(relativePath: "C:/Dev/SpecScribe/file.md", expected: True)`.**
   The theory case asserts a `C:/`-rooted path escapes the repo root. That is true on Windows and false on
   Linux, where `C:/Dev/...` is an ordinary relative directory name — so `EscapesRepoRoot` correctly
   returns `False` there. **The test encodes a Windows-only assumption, not a product bug.** Left unchanged:
   fixing it is not needed for the gate, and changing a test on the strength of a non-gating job would be
   exactly the "portability bug disguised as a CI tweak" AC #4 forbids.

**Tests changed under AC #4 — exactly one, with its root cause:**

| Test | Root cause | Why this is a portability fix, not a weakened assertion |
|---|---|---|
| `CommitDetailTemplaterTests.RenderPage_BinaryRowShowsMarkerNotZeroChurn` (commit `43e9528`) | `Assert.DoesNotContain("+0", html)` searched the **whole rendered page**. The page footer renders the host's local UTC offset; on a host at UTC that string is `… UTC+00:00 …`, which contains `+0`. The test passed only by accident of the author's machine sitting at a **negative** offset. It failed on **both** runners, so this is a **time-zone** bug, not an OS bug. | Both churn assertions were narrowed to the cell markup they are actually about (`commit-added">+0<`, `commit-deleted">&minus;0<`), and **two positive assertions were added** that the binary row's numeric cells render `&mdash;`. The test now checks the intended behaviour more tightly than before, not less. No `src/` change; `GoldenContentFingerprint` untouched. |

Two further `tests/**.cs` changes landed later in the same session, after `bcca682` put the concurrent
session's Stories 18.1/18.2/20.5/20.8/5.3 on `main` and turned the gate red. Both are recorded here under the
same AC #4 rule:

| Test | Root cause | Why this is a fix, not a weakened assertion |
|---|---|---|
| `SiteGeneratorAdapterTests` — `VendoredAssetToken` + new `IsCopiedAsset`/`FoldLineEndings`; constant `8af72043` → `91c3aeb4` (commit `98a90c6`) | The fingerprint's own normalization was **checkout-dependent** (raw-byte hash of a CRLF-vs-LF asset) and **date/time-zone-dependent** (`FoldToday` folding a dated *source comment* on exactly one calendar day, length-neutrally). Full detail in § 4 divergence 1. | Nothing rendered differently; only the normalization changed. The assets remain fully content-pinned — any real edit still flips the hash. The constant went from valid-on-one-box-on-one-day to verified identical on three environments. |
| `SiteGeneratorEpicsRemovalTests.ConcurrentRegenerations_SerializeOnTheWriterLock_AndConvergeToCoherentOutput` (commit `2c1128d`) | Drove `GenerateOne` with a **story artifact**, a call the watch dispatch cannot produce: `FileWatcherService` routes `IsDataSource → IsAdr → IsEpicsRelated → GenerateOne`, and `IsEpicsRelated` claims everything under `implementation-artifacts/`. The direct call wrote a standalone page no full rebuild produces; ~23% of runs (3 of 13) failed depending on whether a racing `RegenerateEpics` pruned the orphan. | The generator was not at fault and was not touched. The leg now drives a plain `notes/` doc — what the dispatch would actually hand it, and the same reasoning the directory-rename test in that class already documents. Contention is unchanged: four routes still race on `_gate`. 0 failures in 25 consecutive runs. |

**No file under `src/` was modified at any point in this story.**

#### 4b. The coverage decision (AC #3) — all four required fields

| Field | Value |
|---|---|
| **Collector** | `coverlet.collector` **6.0.4** — already a `PackageReference` in `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`. **No coverage package was added.** |
| **Format** | **OpenCover**, via `dotnet test --collect:"XPlat Code Coverage;Format=opencover"` — not coverlet's Cobertura default, which SonarScanner for .NET does not document for C#. |
| **Upload path** | `/d:sonar.cs.opencover.reportsPaths="**/TestResults/**/coverage.opencover.xml"`, passed on the **`begin`** step. Report confirmed produced in CI: `…\TestResults\38c0deee-…\coverage.opencover.xml`. |
| **Measured runtime delta** | **See below. Decision: IMPLEMENTED — the cost is real but small.** |

**Measured in CI, on `windows-latest`, two samples each** (coverage-off samples ran from a throwaway branch
carrying only the `--collect` removal, since deleted):

| Metric | No coverage | With coverage | Delta |
|---|---|---|---|
| xUnit suite duration | 38 s, 41 s (mean **39.5 s**) | 49 s, 45 s (mean **47.0 s**) | **+7.5 s / +19.0 %** |
| `Test` step wall-clock | 41 s, 44 s (mean **42.5 s**) | 57 s, 54 s (mean **55.5 s**) | **+13.0 s / +30.6 %** |

Ranges do not overlap on either metric, so the separation is not noise. Step wall-clock is the larger and
more honest figure — it includes writing the ~6.6 MB OpenCover report, which the suite duration excludes.
**+13 s on a job that completes in about 2½ minutes is an easy trade for coverage-aware findings, so
coverage is enabled.** Runs: `30175713872`, `30175856553` (coverage on); `30175760623`, `30175857411` (off).

> **Why the local measurement was discarded — this is the AC #3 number that nearly went wrong.**
> The first measurements were taken on the owner's machine and were **invalid**, for three compounding
> reasons, and are recorded here so the number above is not mistaken for a confirmation of them:
> 1. **A concurrent session was editing `tests/` throughout.** The suite total moved **2390 → 2393 → 2394**
>    *between runs*, so "baseline" and "with coverage" were not measuring the same suite.
> 2. **The failure count was wild and unrelated to coverage**: four consecutive full runs on identical
>    binaries produced **9, 3, 1, and 18** failures. Every one was a `git` subprocess hitting the 15-second
>    `WaitForExit` timeout used in 12 test fixture helpers.
> 3. **The timings were dominated by that noise**: no-coverage 228 s and 183 s; with-coverage 184 s and
>    215 s — i.e. the *slowest* run was a no-coverage one. A delta computed from this would have been fiction.
>
> An early reading of the very first pair (131 s → 194 s, 8 failures) looked like "coverage costs +47.5 %
> and destabilizes 8 tests". **That conclusion was wrong**, and re-measuring is what caught it.
>
> **The 15-second git-subprocess timeout did NOT reproduce in CI** — zero such failures across five runs on
> a 4-vCPU runner, versus routine failures on the owner's 16-core machine. The fragility is therefore
> **local to the owner's machine under concurrent-session load**, not a CI risk, and per the owner's call no
> test timeout was touched. It is recorded here because it will keep costing local dev time.

#### 5. Analysis scope — the final exclusion list, and where the truth lives (Task 5)

**Decision: the truth lives in the workflow file**, as `/d:sonar.exclusions` on the `begin` step —
reviewable in a diff, versioned with the code, and impossible to drift silently the way a UI setting can.
Note that for the .NET scanner a `sonar-project.properties` file is **not read at all**, so the only two
real options were the `begin` command line and the SonarCloud UI.

Excluded (all verified to exist in the tree before being listed):

| Path | Why |
|---|---|
| `src/SpecScribe/assets/prism.js`, `prism.css` | Vendored third-party syntax highlighter. |
| `src/SpecScribe/assets/plotly-hierarchy.min.js` | Vendored, **1.2 MB minified** (Story 20.5). |
| `spike/**` | Throwaway spike code — not maintained, by definition. |
| `tools/**` | Vendor/build tooling (`tools/plotly-vendor/`). |
| `extension/node_modules/**`, `extension/dist/**`, `extension/bin/**` | Dependencies and build output. |
| `SpecScribeOutput/**`, `docs/live/**`, `artifacts/**` | Generated portal output. |
| `_bmad-output/**` | Planning/implementation artifacts — prose, not source. |
| `_bmad/**`, `.claude/**`, `.agents/**` | Installed BMad tooling and skill packs, not authored here — `.claude` and `.agents` hold the *same* packs, which is where the spurious 12.1% duplication figure came from. |
| `chat.json` | A 4,861-line transcript at the repo root. |

**⚠️ Table corrected 2026-07-26 (code review).** This table originally listed only the first seven rows — the
pre-widening state. The last two rows (`_bmad/**`/`.claude/**`/`.agents/**` and `chat.json`) were added by
commit `ab7a25a` and are already live in the shipped `/d:sonar.exclusions` string (see § 0b above), but this
table itself was never updated to match — exactly the kind of drift this story's own § 0b lesson warns about
("an exclusion list cannot be validated by reading it"). It now matches the workflow file verbatim.

**`extension/src/**` is IN scope** (the explicit Task 5 question). It is a single genuine first-party
file, `extension/src/extension.ts` — the maintained VS Code TypeScript shim. Excluding real source to
flatter the numbers would be the wrong trade.

**Test sources.** No `sonar.test.exclusions` flag is passed. SonarScanner for .NET auto-detects
`tests/SpecScribe.Tests` as a test project from its `Microsoft.NET.Test.Sdk` / xunit references and scores
it as test code — that is the documented convention, and adding a redundant, differently-shaped flag on
top of a working auto-detection is a drift risk, not a safety net. **This is the one scope item that is
asserted from the scanner's documented behaviour rather than observed**, because no tokened analysis has
run yet; it must be confirmed on the first green analysis.

`sonar.scanner.scanAll` is deliberately left at its default so the TypeScript/CSS in `extension/src` is
still analyzed; the exclusion list above is what keeps that from dragging in vendored content.

#### 6. Handoff (Task 8)

**To Story 16.2 — branch protection and required checks:**

| Item | Value |
|---|---|
| Workflow file | `.github/workflows/build-test-analyze.yml` |
| Workflow `name:` | `Build, Test & Analyze` |
| **Required-check string** (job name, verbatim) | **`build-test-analyze`** |
| Must NOT be required | `portability-probe (ubuntu, non-gating)` — informational, `continue-on-error: true` |

16.2 still has to: configure branch protection on `main`, mark `build-test-analyze` a required status
check, decide release-branch coverage, and decide any release matrix. It must **extend this workflow, not
create a second one** — that is the amendment 25.1 exists to enable.

**To Story 25.2 — quality gate and findings triage:**

- **No quality gate is attached by this story.** `sonar.qualitygate.wait` is deliberately **not set**, so
  a failing gate does not fail the build. Making the gate blocking is 25.2's AC #1 decision.
- Results are live at <https://sonarcloud.io/project/overview?id=IntegerMan_SpecScribe>.
- **The triage baseline 25.2 inherits** (measured after the exclusion widening, commit `ab7a25a`):
  **32,788 ncloc · 149 files · 155 vulnerabilities · 11 bugs · 1,184 code smells · 89.8% coverage ·
  0.9% duplication.** Do **not** triage against the pre-CI Automatic-Analysis numbers (173 / 14 / 515 over
  46,108 ncloc) — those were inflated by analyzing vendored and generated content.
- **Two caveats 25.2 must not inherit silently:** (a) JavaScript is a **known blind spot** — see Open items
  item 5; "no JS findings" currently means "not analyzed", not "clean". (b) 155 vulnerabilities against
  25.6k lines of first-party C# is high enough that 25.2 should check whether it is dominated by a single
  rule before treating it as 155 distinct problems.

**To Story 17.2 — supply-chain audit scope.** Newly introduced third-party CI surface:
`actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/setup-java@v4`, `actions/cache@v4`, and the
`dotnet-sonarscanner` global tool. The scanner tool is installed via `dotnet tool update
dotnet-sonarscanner` **unversioned**, matching Sonar's own documented sample; pinning it is a legitimate
17.2 call and is flagged here rather than decided unilaterally.

### Open items (what a follow-up session must pick up)

1. ~~Owner actions A and B~~ — **closed 2026-07-26.** See § 0.
2. ~~Re-run the token-leak check with a real token present~~ — **closed, PASS.** Grepped the full raw log of the
   first tokened run for both a raw hex token and the `squ_…` token shape: **zero matches**. The only form the
   token takes anywhere in the log is the literal string `sonar.token="$env:SONAR_TOKEN"` — the variable name,
   never its value.
3. ~~Confirm both projects appear and that tests are classified as test code~~ — **closed, both confirmed.**
   `.slnx` was a non-issue: the scanner picked up both projects, and `SpecScribe.Tests` was auto-detected as a
   test project (log: `Test paths: obj/project.assets.json, obj/SpecScribe.Tests.csproj.nuget.dgspec.json…`,
   plus `Skipped 117 file(s) in the secrets analysis due to automatic test file detection`). No fallback to
   building the two `.csproj` files was needed. Production `ncloc` counts C# from `src/` only, as intended.
4. **README badge (Task 7)** — still **not added**, but the reason has changed: analysis is now green, so a
   badge would render. It was left out because the quality gate is Story 25.2's decision and a badge should
   follow the gate, not precede it. Trivial to add once 25.2 settles the gate.
5. **⚠️ NEW — SonarJS is silently not analyzing our own JavaScript.** `src/SpecScribe/assets/specscribe.js`
   (2,943 lines) is registered by the scanner but has **`lines=2943` and no `ncloc` at all**, and the whole
   project reports just `js=28`. The analysis log says only:
   `INFO: Some of the project files were automatically excluded because they looked like generated code.
   Enable debug log to see which files are excluded.` — and names none of them. It is **not** the usual
   minified-bundle heuristic: max line length is 191 chars with zero lines over 1,000, and Node.js was
   available (the scanner used its embedded runtime). `extension/src/extension.ts` analyzes fine, so the
   TypeScript/JavaScript sensor itself is working. **Left unfixed deliberately** rather than guessing a
   `sonar.javascript.*` flag into the gate. Next step is one run with debug logging enabled to make the
   scanner name the excluded files. Until then, treat JS findings as a **known blind spot** — this matters to
   Story 25.2, which would otherwise read "no JavaScript issues" as a clean bill of health.
5. ~~Green pull-request run~~ — **closed.** PR [#2](https://github.com/IntegerMan/SpecScribe/pull/2), run
   `30176207551`, green. The merge push then ran the gate and the Pages workflow concurrently, both green.
6. **The `.gitattributes` / golden-fingerprint portability finding** deserves its own story — see § 4.

### Proposed follow-up story (rewritten 2026-07-26 — its original premise was disproven)

**"Move the gate to `ubuntu-latest`."** The fingerprint half of this is **already done** (see § 4 divergence
1): Linux and Windows now agree byte-for-byte. What remains is **one** test, and it is small:

`PathUtilTests.EscapesRepoRoot_ChecksTheLeadingSegment_NotABareSubstring(relativePath: "C:/Dev/SpecScribe/file.md", expected: True)`
— a `C:/…` path is absolute on Windows and an ordinary relative name on Linux, so `EscapesRepoRoot`
correctly returns `False` there. The theory case encodes a Windows-only assumption; it needs to assert the
platform-appropriate expectation rather than a fixed `True`.

**Payoff:** `ubuntu-latest` runs the suite in **13 s vs 47 s** on `windows-latest` — roughly 3.5× on every
push and PR — and the ubuntu job could then become the gate instead of a non-gating probe.

**Still worth doing separately:** add a `.gitattributes`. The repo has never had one, and its absence is
what made the vendored asset checkout-dependent in the first place. It is no longer *blocking* anything,
which is precisely why it should be filed rather than forgotten. Note that adding one will move the golden
constant again (working-tree line endings change), so it is a deliberate, reviewed change of its own.

### File List

**⚠️ Corrected 2026-07-26 (code review) — this list originally omitted 4 real files that the Dev Agent Record's
own narrative (§ 4, § 4b, § 5, Task 7) already discussed at length.** CLAUDE.md scopes reviews by a story's own
File List, so an incomplete one is a real gap, not a formality — it now matches the diff.

- `.github/workflows/build-test-analyze.yml` — **added**
- `tests/SpecScribe.Tests/CommitDetailTemplaterTests.cs` — **modified** (one test, AC #4-justified — see § 4)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — **modified** (golden-fingerprint portability fix, AC #4-justified — see § 4)
- `tests/SpecScribe.Tests/SiteGeneratorEpicsRemovalTests.cs` — **modified** (flaky concurrency test fix, AC #4-justified — see § 4)
- `.gitignore` — **modified** (added `.sonarqube/`, `.sonar/`)
- `README.md` — **modified** (added a Continuous integration section)
- `docs/SonarCloudSetup.md` — **added**
- `_bmad-output/implementation-artifacts/25-1-sonarcloud-onboarding-and-ci-analysis.md` — **modified** (this record)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — **modified** (status transitions)

### Change Log

| Date | Change |
|---|---|
| 2026-07-25 | Added `.github/workflows/build-test-analyze.yml`, the repository's first build+test workflow, with SonarScanner for .NET analysis and a non-gating ubuntu portability probe. Added `.sonarqube/` and `.sonar/` to `.gitignore`. Commit `252087f`. |
| 2026-07-25 | Fixed a time-zone-dependent assertion in `CommitDetailTemplaterTests.RenderPage_BinaryRowShowsMarkerNotZeroChurn` — it failed on both CI runners because a whole-page `DoesNotContain("+0")` matched the footer's `UTC+00:00` offset. Scoped to the churn cells and strengthened with two positive assertions. Commit `43e9528`. AC #4-justified. |
| 2026-07-25 | Gate green on `main` (run `30175713872`): 2390 passed / 0 failed / 0 skipped on `windows-latest`. Coverage enabled on measured evidence (+13 s / +30.6 % test-step wall-clock). Story remains **in-progress**: two owner actions block the analysis half of AC #1 — see § 0. |
| 2026-07-26 | Code review (scoped to this story's own commits, excluding sibling stories 18.x/20.x/22.x/23.x/25.5/25.6/27 on the same shared `main`): 2 decision-needed (owner kept `portability-probe` permanent; owner chose to isolate SonarScanner failures from Build/Test), 5 patch, 1 defer, 12 dismissed. All 7 patches applied: `continue-on-error` added to both SonarScanner steps; `IsCopiedAsset`/`IsVendoredAsset` merged into one `KnownStaticAssets` map; `VendoredAssetToken` switched from a `ReadAllText` round-trip to a raw-byte CRLF fold; File List corrected (4 missing files added); Dev Notes §5 exclusion table corrected to match the shipped workflow; a reconciling note added next to the frontmatter's fingerprint-immutability line; the ubuntu job's permanence documented. See § Review Findings. |
