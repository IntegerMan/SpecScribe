---
baseline_commit: 35437b9 # local `main` HEAD at authoring time (2026-08-07). ⚠️ `origin/main` is TWO COMMITS
                         # BEHIND at 838d591 — 913a05a "Worktrees" + 35437b9 are unpushed, and they carry
                         # 16-1-spike-report.md and ADR 0040. EVERY CI observation in this file was read from
                         # `origin/main`, i.e. from 838d591. Verify before citing a line number — shared main.
epic: 16
frs: [FR32] # release engineering; this story is the CI half of it
nfrs: [NFR9] # "Release builds are reproducible and produced by CI from a clean checkout; publishing to any
             # distribution channel is gated on a passing build + test run." (epics.md:138)
depends_on: [16-1] # ADR 0040 §9 decides HOW a release tag inherits this gate. Its Proposed-vs-Accepted state
                   # does not block here: §9 restates Story 25.1's handoff rather than making a new claim.
blocks: [16-4] # the release pipeline gates on "the tagged commit is already green on main" — which means
               # nothing until this gate is both green and required.
informs: [16-3, 16-8, 16-9, 17-4]
amends: null # NOTHING structural. epics.md and sprint-status.yaml need no scope edit — see § Scope guard.
ships_product_code: true # ⚠️ UNLIKE 16.1. Edits `tests/**` (flake root cause) and `web/package-lock.json`.
                         # Does NOT edit `src/**`, `web/` source, or `extension/**`.
decides: null # No new ADR. Every decision this story needs is already ratified or owner-answered — see R5.
owner_decisions_locked: 2026-08-07 # three, taken at create-story. See § R5. Do not re-litigate them.
deliverables:
  - ".github/rulesets/main-required-checks.json (the APPLIED ruleset, exported from the live API)"
  - "tests/SpecScribe.Tests/FileWatcherServiceTests.cs (flake root cause)"
  - "web/package-lock.json (the `npm ci` repair routed here from 16.1)"
  - "docs/CiGate.md (what is required, why, and how to re-apply it)"
---

# Story 16.2: Continuous Integration Build & Test Gate

Status: review

> ✅ **All 8 tasks complete; all 6 ACs satisfied.** `build-test-analyze` is a **required** status check on
> `main` via ruleset **`20567252`** (`active`), with the repository admin as an `always` bypass actor.
> `main` is `protected: true`; a red gate now blocks a PR merge (measured `UNSTABLE` → `BLOCKED`).
> The ruleset `POST` itself was performed by the **owner** — this session's permission layer refused writes
> to live repository settings, and that was not routed around.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want every pull request and push to build and run the test suite in CI,
So that release builds start from a known-green baseline and regressions are caught before merge.

| | |
|---|---|
| **This story does** | Make `build-test-analyze` **trustworthy, and only then required**. Three pieces of work in a load-bearing order: (1) close the `npm ci` blocker 16.1 routed here; (2) **earn a green baseline** — the gate has failed 27 of its 74 runs and `origin/main` is red right now; (3) apply a repository ruleset requiring the `build-test-analyze` check, with an admin bypass so the owner's direct pushes to `main` keep working. |
| **This story does NOT** | Create a second build+test workflow (epics.md AMENDED 2026-07-25 — the entire reason Story 25.1 exists). Touch `src/**`, `web/` source, or `extension/**`. Build the release pipeline (16.4). Make `portability-probe` required. Change the SonarCloud quality-gate posture (25.2 / ADR 0035). Add a retry-until-green step or quarantine the flaky tests — **the owner rejected both explicitly** (R5). |

**The order is not a suggestion.** Applying the ruleset before the baseline is green converts a flaky
workflow into a merge blocker on a repository whose owner ships by pushing to `main`. Flakes first, prove
green, then apply the rule.

---

## ⛔ Read first — ten reconciliations against the live repository and the live GitHub API

The seeded ACs describe a gate that needs a checkbox flipped. **It needs considerably more than that.**
Everything below was measured on **2026-08-07** against `origin/main` (`838d591`) through the public GitHub
REST API, and against the working tree at `35437b9`. None of it is inferred.

### R1 — There is no branch protection and no ruleset on this repository. At all.

Measured, unauthenticated:

```
GET /repos/IntegerMan/SpecScribe/branches/main
  → "protected": false
  → protection: { "enabled": false,
                  "required_status_checks": { "enforcement_level": "off", "contexts": [], "checks": [] } }

GET /repos/IntegerMan/SpecScribe/rulesets
  → []
```

The repository is **public** and `default_branch` is `main`, so both classic branch protection and
repository rulesets are available at no cost and **nothing has to be migrated or merged** — this is a
greenfield apply. Story 25.1's handoff ("16.2 still has to configure branch protection on `main`") is
still accurate; nothing has happened since.

### R2 — 🚨 THE GATE IS NOT GREEN AND NEVER RELIABLY HAS BEEN. This is the story's centre of gravity.

`build-test-analyze` has run **74 times**. **27 failed** (~36%). **17 of those were pushes to `main`.** The
most recent run — **#74 at `838d591`, the current tip of `origin/main`** — is **RED**.

Failing step per failed run, taken from `/actions/runs/{id}/jobs` (step-level `conclusion`, so this is
GitHub's own attribution, not a reading of logs):

| failing step on the **gating** job | runs | count |
|---|---|---|
| **`Test`** (`dotnet test`) | 1, 11, 12, 13, 24, 33, 47, 48, 49, 59, 60, 61, **74** | **13** |
| **`Check web drift gates`** (`npm run check`) | 30, 35, 36, 40, 51, 58, 62, 63, 67, 68 | **10** |
| `Generate the IR and prerender the site` | 54 | 1 |
| `SonarScanner end` | 18 | 1 |
| `Build` | 6 | 1 |

**Do not read all 27 as flakes.** Several were genuine regressions since fixed — runs 11–13 sit in the
line-ending era `.gitattributes` was written to close (its own comment names CI run 30320616634), and run
18's `SonarScanner end` failure predates the `continue-on-error` that Story 25.1's code review added.
**Your job is to classify, not to assume.** What governs AC #1 is the *current* rate and the *current* red
tip.

**Why this reframes the story:** a required check that fails ~1 push in 3 is not a gate, it is a tax that
teaches people to route around it. AC #4 exists so "green baseline" is a deliverable rather than a
precondition you are allowed to assume.

### R3 — The `Test` flake is ALREADY ROOT-CAUSED. Do not go hunting; go and fix this.

Story 16.1 observed it locally and reported the symptom:

> `FileWatcherServiceTests.EditingAStoryFile_RegeneratesThroughTheOrdinaryMarkdownRoute` failed with
> `JsonReaderException: The input does not contain any JSON tokens` … Re-run in isolation: **11/11 passed**.
> — [16-1-spike-report.md:612-617]

**The mechanism is a three-link causal chain in the test helpers. It is a real defect, not bad luck:**

1. `SiteRegion.ReadShared` opens the IR with `FileShare.ReadWrite` (`tests/SpecScribe.Tests/SiteRegion.cs:279`).
   Its own doc comment says why: sharing the handle stopped the test *locking the chunk the generator was
   trying to write*, which had been failing `BurstOfSaves`. **That fix was correct — and it is what created
   this one.** A reader that no longer contends for the handle now succeeds against a file that is
   mid-write, returning partial or empty content instead of throwing.
2. `SiteRegion.Read` / `Exists` / `Routes` feed that content straight into `JsonDocument.Parse`
   (`SiteRegion.cs:44,56,66,76`). On a torn read that throws **`JsonException`**.
3. `FileWatcherServiceTests.Evaluate` — the guard that exists *precisely* so a mid-rebuild poll returns
   "not yet" — catches **`IOException`** and **`UnauthorizedAccessException`** only
   (`FileWatcherServiceTests.cs:149-154`). `JsonException` derives from neither. **It escapes the predicate,
   escapes `WaitFor`, and fails the test** instead of costing one more 25 ms poll.

`FileWatcherServiceTests.cs` is the **only** file under `tests/` using `Thread.Sleep` or `Task.Delay`
(grepped). `SettleTimeout` is already **20 s** and every wait already polls for the outcome rather than
sleeping a fixed multiple of the debounce — **so widening the timeout is not the fix and must not be
attempted.** The fix belongs in the exception filter, where the class's own stated design intent already is.

**Do not stop at this one test.** Runs 11/24/33/47/48/49/59/60 failed on **Windows *and* Ubuntu
simultaneously**, which a single-machine timing flake does not usually do. Read the logs (AC #4) — a second,
distinct cause may be hiding behind the loud one.

### R4 — The `npm ci` blocker: **CI is not affected.** 16.1 could not check; it is checked now, and the answer flips the framing.

16.1 routed this here as *"unverified-on-CI"* because `gh` was unavailable. Verified now from the public API:

| where | Node | npm | `npm ci` |
|---|---|---|---|
| CI, pinned by `web/.nvmrc` | **24.11.1** | **11.6.2** | ✅ **green** — run #73 (`7ff3b13`), step "Install web dependencies" succeeded on **both** `build-test-analyze` (Windows) **and** `portability-probe` (Ubuntu) |
| this machine | **24.18.1** | **11.16.0** | ❌ `Missing: @emnapi/runtime@1.11.3 from lock file` |

(Node→npm mapping read live from `https://nodejs.org/dist/index.json`.)

**So the lockfile is not what makes CI red — `Test` is.** Do not conflate them in the completion notes.

It remains a genuine defect worth fixing: a contributor on a Node version *this project's own `engines`
field explicitly permits* (`web/package.json:6-8` → `^22.19.0 || ^24.11.0 || >=26.0.0`) cannot run
`npm ci` at all, and ADR 0040 §7 names it as one of three gaps the preview's weak NFR9 reading must close.

**Root cause, and proof the fix is small.** `@napi-rs/wasm-runtime@1.1.6` declares two peers —
`@emnapi/core ^1.7.1` and `@emnapi/runtime ^1.7.1` (`web/package-lock.json:1254-1257`). The lockfile carries
a top-level `node_modules/@emnapi/core@1.11.3` (line 570) but **no top-level `node_modules/@emnapi/runtime`
entry at all** — an asymmetry left by whichever older npm last wrote the file. npm 11.16 resolves the
missing peer to `1.11.3`, finds it absent, and `npm ci` correctly refuses.

`npm install --package-lock-only --ignore-scripts` was run against a **copy** of `package.json` +
`package-lock.json` in a scratch directory (no `node_modules`, nothing in the repository touched). Result: a
**69-line diff, entirely benign**:

- adds the missing `node_modules/@emnapi/runtime@1.11.3` entry — the actual fix;
- adds an `engines` block to the lockfile's root `packages[""]` entry, mirroring `web/package.json` (the
  lockfile predates Story 23.5 adding that field and was never regenerated since);
- shuffles `"peer": true` markers as npm recomputes them.

**Zero version bumps. Zero `resolved`-URL churn. No package added other than `@emnapi/runtime`.** If your
regeneration produces materially more than that, **stop** — something else moved under you (CLAUDE.md
§ Concurrent work) and that must be reported, not committed.

### R5 — Three owner decisions were taken at create-story (2026-08-07). They are locked.

| # | question | **decision** |
|---|---|---|
| 1 | The owner ships by **local merge + direct push to `main`** (the last five commits are merge commits pushed straight to `main`). A required status check blocks exactly that. | **Repository ruleset requiring `build-test-analyze`, with the repository admin as a bypass actor.** PRs are gated; the owner's direct push keeps working. |
| 2 | How much of the 13-failure `Test` class does 16.2 own? | **Diagnose and fix the root causes.** Quarantine only as a documented last resort, with a named follow-up story. |
| 3 | `gh` is not installed and the logs API is 403 unauthenticated. | **Install `gh` (`winget install GitHub.cli`); the owner runs `gh auth login` once, interactively.** |

**Decision 2 explicitly rejected the two cheaper options** a dev agent would otherwise reach for: a
retry/rerun-failed step, and quarantining the flaky tests now to fix later. **Implement neither.**
Green-by-suppression is the exact failure mode this repository's conventions exist to prevent — CLAUDE.md's
"never regenerate a gate's baseline reflexively; establish causality first" is the same rule in different
clothes.

### R6 — `gh` is not installed, and you cannot read a CI log without it.

`command -v gh` → not found. The job-logs endpoint returns **HTTP 403 unauthenticated**, so R2's
failing-step attribution (which comes from the *jobs* endpoint, which is public) is as far as anonymous
access reaches. **You need the logs to satisfy AC #4's classification requirement.**

What *is* readable without auth, and worth keeping for cheap verification:

```sh
curl -s "https://api.github.com/repos/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml/runs?per_page=100"
curl -s "https://api.github.com/repos/IntegerMan/SpecScribe/actions/runs/{run_id}/jobs"   # step-level conclusions
curl -s "https://api.github.com/repos/IntegerMan/SpecScribe/branches/main"                # protection state
curl -s "https://api.github.com/repos/IntegerMan/SpecScribe/rulesets"                     # ruleset state
```

The unauthenticated rate limit is **60 requests/hour**, and R2's 27-run sweep costs ~28 of them. Budget it,
or authenticate first (Task 1) and stop worrying about it.

### R7 — The second failure class (`Check web drift gates`, 10 runs) is a KNOWN shape. Read CLAUDE.md before touching it.

These are `npm run check` — `check:tokens` / `check:ir-content` / `check:assets` / `check:parity`. CLAUDE.md
devotes a section to this class, and Story 16.1 hit it twice in one session and was right **both** times not
to regenerate:

- **Never regenerate a baseline reflexively.** Establish causality first. A gate can move because of a
  concurrent session's change, not yours.
- The `+4 / -182`-shaped `check:ir-content` diff is **documented in advance** at
  `build-test-analyze.yml:280-292` as the signature of a `generate` run **without `--deep-git`**. Following
  the gate's own suggested fix there would delete ~182 deep-analytics rules and turn it green over a real
  regression.
- `check:parity` **cannot see a C#-side change** — its corpus is frozen. A green `check:parity` means "the
  renderer still behaves the same on the frozen fixture", never "my change is safe".

**In scope here:** classify these 10 runs — per run, was the cause (a) real drift from a concurrent story,
(b) the `--deep-git` corpus signature, or (c) something structural in the gate? **Out of scope:**
re-engineering the drift gates. If the classification surfaces a structural defect, raise it as a finding and
route it; do not absorb it.

### R8 — The required-check string, verbatim, and the workflow you must not create.

Three independent sources agree, and all three are load-bearing:

- `.github/workflows/build-test-analyze.yml:1-13` — the file's own header comment, addressed to this story
- `25-1-sonarcloud-onboarding-and-ci-analysis.md:736-747` — the Task 8 handoff table
- `docs/adrs/0040-release-channels-and-versioning-policy.md:150-159` — ADR 0040 §9

| item | value |
|---|---|
| Workflow file | `.github/workflows/build-test-analyze.yml` |
| Workflow `name:` | `Build, Test & Analyze` |
| **Required-check context, verbatim** | **`build-test-analyze`** — the `jobs.<id>.name`, **not** the workflow name |
| **Must NOT be required** | `portability-probe (ubuntu, non-gating)`. It carries `continue-on-error: true` at the **job** level, so its `conclusion` is `success` even when a step inside it failed — **observed**: run #63, step `Test` failed, job reported `success`. Requiring it would be worse than useless: a check that cannot fail. |
| **Must NOT happen** | A second workflow that builds or tests. epics.md AMENDED 2026-07-25 is explicit: *"two workflows that both build and test is the exact drift class this project has repeatedly paid for."* |

### R9 — AC #2's "does not disturb the Pages workflow" is already true. Verify it; do not re-engineer it.

`publish-docs-live-pages.yml` is independent by construction, and every part of the separation is deliberate
and commented:

- **Different concurrency group.** Pages owns `pages`; `build-test-analyze.yml:32-38` explicitly must *not*
  use that group, or the two would cancel one another.
- **Different trigger shape.** Pages filters on `paths:`; the gate deliberately has **no** `paths:` filter
  (`build-test-analyze.yml:16-19` — *"a build+test gate that skips on some paths is not a gate"*).
- **Different permissions.** Pages declares permissions **per job** (Story 25.2, `githubactions:S8233`);
  the gate declares `contents: read` at workflow level.

**A ruleset touches neither workflow.** The one real interaction to confirm: Pages **deploys**, it does not
write to `main`, so a branch ruleset cannot block it. Confirm and record; do not refactor.

### R10 — Two phrases in AC #1 that will otherwise be read wrong at code-review time

**"release-relevant branches" means `main`, and only `main`.** There are no `release/*` branches and none are
planned: `git branch -r` shows `origin/main` plus worktree and story branches, and releases are
**tag-triggered** (16.4, ADR 0040 §9), with a tag inheriting the gate by pointing at a commit already green
on `main`. **Do not invent a release-branch pattern to satisfy this phrase.** Targeting
`~DEFAULT_BRANCH` satisfies it completely today; say so in the completion notes so a reviewer does not read
the singular target as a shortfall.

**"covering pull requests and pushes" is satisfied *with* the admin bypass, not despite it.** A ruleset's
`required_status_checks` rule binds pushes to the target branch as well as merges into it — the bypass
actor is a **named, deliberate exemption for one principal** (R5 decision 1), not a hole in the coverage.
Record it that way, with the empirical proof from AC #5, so the acceptance auditor at epic-end review does
not read a working direct push as AC #1 failing.

### NFR9 — what this story may and may not claim

ADR 0040 §7 claims the **weaker** reading of "reproducible": *built from a clean checkout by CI*, not
byte-identical rebuilds. This story closes exactly one of the three named gaps — **a working `npm ci`**.
`SOURCE_DATE_EPOCH` is 16.4's, version-from-tag is 16.3's, `<Deterministic>` / SourceLink are deferred
post-preview. **Do not claim more than that in the completion notes.**

ADR 0040 §9 already answers how a *tag* inherits this gate: **require the tagged commit to be already green
on `main`**, rather than re-running build+test inside the release job. Implementing that is 16.4's. This
story's job is to make "green on `main`" a statement that means something.

---

## Acceptance Criteria

**AC #1 and AC #2 are epics.md verbatim. AC #3–#6 are added by this story** — #3 and #4 because 16.1 routed
real blockers here that #1 silently assumes away, #5 to make the applied configuration reviewable, #6 as the
scope guard.

1.
**Given** the build+test+analyze workflow established by Story 25.1
**When** this story runs
**Then** it is extended and configured as a **required** status check for release-relevant branches — covering pull requests and pushes — restoring, building, and executing the `tests/SpecScribe.Tests` suite on a clean checkout and failing on any build or test failure
**And** it does **not** introduce a second workflow that duplicates the build or test steps.

2.
**Given** the gate is green
**When** a maintainer reviews the pull request
**Then** the build/test status is visible as a required signal
**And** the workflow is independent of, and does not disturb, the existing GitHub Pages publish workflow.

3. **(NEW — the `npm ci` blocker routed here from Story 16.1)**
**Given** `npm ci` fails on a clean checkout under npm 11.16.0 with `Missing: @emnapi/runtime@1.11.3 from lock file`
**When** the lockfile is repaired
**Then** `npm ci` succeeds on a clean checkout under **both** the CI-pinned toolchain (Node 24.11.1 / npm 11.6.2) **and** the current one (Node 24.18.1 / npm 11.16.0)
**And** the committed `web/package-lock.json` diff introduces **no dependency version change** beyond adding the missing `@emnapi/runtime` entry — anything larger is reported, not committed.

4. **(NEW — the green baseline AC #1 assumes and does not have)**
**Given** `build-test-analyze` has failed 27 of 74 runs and `origin/main` is red at `838d591`
**When** the gate is made required
**Then** every `Test`-step and `Check web drift gates` failure in the run history is **classified** — genuine-regression-since-fixed, environmental, or live-flake — with the classification recorded per run
**And** each live-flake root cause is **fixed** (not retried, not quarantined, not timeout-widened) or, as a documented last resort, quarantined with a named follow-up story
**And** the gate is demonstrated green on `main` across **at least two consecutive runs** before enforcement is switched on.

5. **(NEW — the applied configuration must be reviewable)**
**Given** a repository ruleset is applied through the GitHub API
**When** the story completes
**Then** the exact applied ruleset is **exported from the live API** and committed as `.github/rulesets/main-required-checks.json`, so the configuration is diff-reviewable rather than existing only inside repository settings
**And** the admin bypass is verified **empirically** — a direct push to `main` still succeeds, and a pull request with a failing `build-test-analyze` is blocked from merging
**And** `portability-probe (ubuntu, non-gating)` is absent from the required contexts.

6. **(NEW — scope guard)**
**Given** this epic's stories share files and land bundled
**When** this story runs
**Then** it touches only `tests/SpecScribe.Tests/**`, `web/package-lock.json`, `.github/**`, and `docs/CiGate.md`
**And** it makes **no** change under `src/**`, `web/` source, or `extension/**`
**And** `npm run check:parity` and the C# suite's pass count do not move except by the flake fixes this story makes.

---

## Tasks / Subtasks

- [x] **Task 1 — Get authenticated access (AC: #4, #5). Do this first; everything depends on it.**
  - [x] ~~`winget install GitHub.cli`~~ **Not needed — `gh` was already installed**, at
        `C:\Program Files\GitHub CLI\gh.exe`. It is **not on `PATH`**; invoke by full path.
  - [x] **No owner handoff was needed.** `gh` was already authenticated as `IntegerMan` (keyring), scopes
        `gist, read:org, repo, workflow`. R5 decision 3's interactive step was already done.
  - [x] Verified: `gh auth status` OK; `permissions` → `{"admin":true,"maintain":true,"push":true,…}`, so
        ruleset writes are permitted by the token. **Endpoint paths must omit the leading `/` in a Git-Bash
        shell** (MSYS rewrites `/repos/…` to a filesystem path).

- [x] **Task 2 — Fix `npm ci` (AC: #3)** — **already repaired on `main` by `0b1f561`; verified, not redone.**
  - [x] **It does NOT reproduce**, and the story says to stop and say so if that happens. Cause established:
        `0b1f561` ("CI fix: repair the lockfile…") landed the repair between create-story and dev-story.
        `SPECSCRIBE_PACKAGE_BUILD=1 npm ci --dry-run --ignore-scripts` now **succeeds — `added 639 packages`**
        on this machine's Node 24.18.1 / npm 11.16.0, the exact toolchain R4 measured failing.
  - [x] Repair not re-run — it would have been a no-op regenerating an already-correct lockfile.
  - [x] **Committed diff read and checked against R4's prediction.** `0b1f561` touches
        `web/package-lock.json` by **+18 / −24 lines**: exactly one added package key
        (`node_modules/@emnapi/runtime`, `version 1.11.3`), and **zero other `version:` or `resolved:`
        changes** — verified by grepping the diff for both fields. Smaller than R4's predicted 69 lines
        because the root `engines` block was already present; materially it is the predicted change, and
        nothing larger. AC #3's "no dependency version change" clause holds.
  - [x] **CI-pinned toolchain proven empirically, not simulated.** Run **#78** on `main` (`07bdb79`, which
        contains `0b1f561`) shows step 11 **`Install web dependencies` → `success`** under `web/.nvmrc`'s
        pinned Node **24.11.1 / npm 11.6.2**, on the gating Windows job *and* the Ubuntu probe. Both halves
        of AC #3 are therefore measured, not inferred.
  - [x] All three consumers green in run #78: gating `Install web dependencies` (step 11), the probe's own
        install, and `publish-docs-live-pages.yml` is unaffected (its `npm ci` reads the same lockfile).

- [x] **Task 3 — Classify the whole failure history (AC: #4)** — full table in § Dev Agent Record.
  - [x] Enumerated: **78 runs, 27 failures + 1 cancelled**. R2's table reproduced and **extended** — run
        **#75** (`3312256`) post-dates the story's sweep and is classified too.
  - [x] Logs pulled for every failed gating job. **Correction:** `gh api …/jobs/{id}/logs` **fails** here
        ("the response contains terminal escape sequences"); `gh run view <run> --job <job> --log` works.
  - [x] Table produced: run · sha · failing step · failing test/gate · classification · evidence.
        **20 genuine-regression-since-fixed · 5 environmental · 1 cancelled · 2 live-flake.**
  - [x] The 10 `Check web drift gates` runs classified against R7's three shapes: **1 `check:assets`** (30),
        **7 real drift** (35, 36, 40, 51, 63, 68 + 30), **2 the `--deep-git` corpus signature** (58 `+4/−180`,
        62 `+4/−182`), **1 its inverse** (67 `+181/−4`). **NO baseline was regenerated.** The structural one
        was in the *workflow*, not the gate — CI generated without `--deep-git` — and is **already fixed** by
        `f7e812f`, so it is recorded rather than routed.
  - [x] **Run #74 is a LIVE-FLAKE** (`JsonReaderException` at `SiteRegion.Exists`, 8 KB flush boundary), and
        so is #75. They are the only two live-flakes in 78 runs, and both are the same defect — so the
        historical ~36% failure rate is emphatically **not** a ~36% flake rate.

- [x] **Task 4 — Fix the `Test` flake at its root cause (AC: #4, #6)** — the prescribed fix was already on
      `main` and was **not sufficient**; the real root cause was found by measurement and fixed.
  - [x] `JsonException` in `Evaluate`: **already landed by `48c050c`.** Verified present
        (`FileWatcherServiceTests.cs:169`) — then verified **inadequate**: the flake still reproduced, now as
        a mute 20 s timeout, because a swallowed exception is indistinguishable from "not yet".
  - [x] Doc comment extended — and **corrected**. `48c050c` calls this *"the Linux-only transient state"*;
        **run #74 is a Windows run** with the identical exception, because `FileShare.ReadWrite` removes the
        very locking that argument depends on (finding **F2**).
  - [x] `SettleTimeout` **not** widened (still 20 s); **no** sleeps added. Vindicated by the root cause: the
        poll was **stuck, not slow**, so no timeout would ever have been long enough.
  - [x] **Layer decided and recorded: `SiteRegion.ReadShared` owns it.** Every one of
        `Read`/`Exists`/`Routes`/`HasRoutesUnder` funnels through it, so it is fixed **once**, not in two
        places. `Evaluate` keeps its transient-exception guard — complementary, not duplicate.
        **Root cause:** `FileShare.ReadWrite` admits a concurrent reader and writer **but not a concurrent
        *deleter***, and `GenerateAll` wipes the output root before repopulating. The test's own poll handle
        made that wipe fail on `pages-root.json`; the pass aborted **mid-wipe**, the route vanished from the
        IR, and nothing retried. **Fix: `FileShare.ReadWrite | FileShare.Delete`.**
  - [x] **Proven: 50 consecutive loaded iterations of `FileWatcherService` across two harnesses — 0
        failures** (bar was 20). Before the fix the same harnesses failed 1-in-16 loaded, 1-in-40 focused,
        **and 1-in-1 in a plain unloaded full-suite run**.
        **Two honesty corrections to how that load was measured**, both caught rather than glossed:
        1. The **first** harness let its load generators finish early — iterations 17-20 ran *unloaded*. Its
           honest denominator is 16, not 20.
        2. In the **second**, `npm run build:package` was failing instantly with `'nuxt' is not recognized`
           because **`web/node_modules` had never been installed in this fresh worktree**, with the harness
           suppressing output. So that run's "Node build" half was **a silent no-op — the load was
           `dotnet build` only.** Discovered when `check:parity` reported no Nitro server.
        After `npm ci`, the harness was **re-run with the Node build genuinely running** (verified live: 7
        `node` processes plus `node-spawn-server` mid-run). **That third run is also 25/25, 0 failures.**
        Totals: **25 iterations under real `dotnet build` + Nuxt build load, plus 25 under `dotnet build`
        load, plus two clean full-suite runs — 0 failures in all of them.**
  - [x] Additional root cause surfaced and fixed as above. **Nothing was retried, quarantined, or
        timeout-widened** (R5 #2 honoured). The product-side "a failed pass is never retried" gap is
        `src/**` and therefore routed as finding **F1**, not absorbed.
  - [x] A **failure-path-only** `Diagnose` helper was added so a future mute timeout reports its own cause
        (`if (!WaitFor(…)) Assert.Fail(…)`, deliberately not `Assert.True(cond, msg)` — that form evaluates
        its message eagerly on every passing call).

- [x] **Task 5 — Earn the green baseline (AC: #4)**
  - [x] **`main` is green across two consecutive runs — AC #4's enforcement precondition is met.**
        Run **#78** (`push`, `07bdb79`) and run **#79** (`workflow_dispatch`, `07bdb79`), **every step green**
        on both, and `portability-probe` green on both too. `origin/main` was **not** two commits behind as
        the story assumed — it had already been pushed and was at `07bdb79`.
        ⚠️ **Be precise about what those two runs prove:** they establish that **`main`'s tree** is green.
        They do **not** contain this story's `FileShare.Delete` fix, which is on
        `worktree-story-16-2-dev` — CI for that branch is run **`31208253189`**, dispatched deliberately
        (a push to a non-`main` branch triggers nothing; the workflow is `push`/`pull_request` on `main`
        plus `workflow_dispatch`).
  - [x] **This story's own fix is proven green in CI, on both operating systems.** Run **`31208253189`** on
        `worktree-story-16-2-dev`: **`build-test-analyze` `success` with every step green** — `Build`,
        **`Test`**, `Install web dependencies`, `Generate the IR and prerender the site`,
        **`Check web drift gates`**, `Test web` — and **`portability-probe (ubuntu, non-gating)` `success`
        too**, which matters because this flake class was first observed on Linux (Story 25.1's probe).
        That green `Check web drift gates` is also the independent confirmation that the local
        `check:ir-content` red was environmental and nothing was actually drifted.
  - [x] Non-incremental rebuild done before measuring: `dotnet build SpecScribe.slnx --no-incremental`
        (0 errors), then again for the test project after the fix.
  - [x] **Regression floor recorded:**

        | measurement | 16.1's floor | this tree, BEFORE the fix | this tree, AFTER |
        |---|---|---|---|
        | `dotnet test SpecScribe.slnx` | 2962 P / **1 F** / 3 S | 2977 P / **1 F** / 3 S | **2978 P / 0 F / 3 S** ×2 consecutive |
        | `check:tokens` | OK | OK | **OK** (45 tokens, 2 `:root` blocks) |
        | `check:assets` | OK | OK | **OK** (4 runtime assets) |
        | `check:parity` | OK 24/14 | OK | **OK — 24/24 routes, 14/14 families byte-identical** |
        | `check:ir-content` | OK | — | **NOT RUNNABLE HERE — environmental, see below** |

        The single failure in both "before" columns is the flake this story fixes; it is now a pass, and the
        pass count moved by exactly that one test. **AC #6's "pass count does not move except by the flake
        fixes this story makes" holds exactly.**
  - [x] **`check:ir-content` is red locally and it is NOT this story's change — causality established before
        anything was touched, and NO baseline was regenerated** (CLAUDE.md § "Never regenerate a gate's
        baseline reflexively"). It reports `+1 / −1368`. Proof it is environmental:
        1. **This worktree has no `SpecScribeOutput/` at all** — `generate` has never run here, and
           `extract:ir-content` **prunes any selector it cannot find in the IR**, so with no IR it prunes
           essentially the whole sheet. A −1368 prune is that precondition, not a stylesheet drift.
        2. **Neither gate input is modified** — `git status` is clean for both `web/` and
           `src/SpecScribe/assets/`. This story changed only `tests/**` and `docs/**`.
        3. **CI runs it correctly and it is green**: `Check web drift gates` → `success` in runs #78 and #79,
           because CI generates the IR with `--deep-git` first.

        This is the identical precondition Story 16.1 recorded ("a fresh worktree has no IR"). Re-deriving a
        baseline from an empty IR would have deleted ~1368 live rules and turned the gate green over nothing.

- [x] **Task 6 — Apply the ruleset (AC: #1, #2, #5)** — applied by the **owner** (the `POST` was refused by
      this session's permission layer as a write to live repository settings, twice; not routed around).
      **Ruleset `20567252`, `enforcement: active`, created 2026-08-07.**
  - [x] Task 5's precondition satisfied first — runs **#78 and #79** both green on `main` at `07bdb79`, so
        the ordering constraint ("flakes first, prove green, then apply the rule") was honoured.
  - [x] Created from the prepared payload. **`422 name must be unique`** on a second invocation is what
        revealed the first had already landed — that error means "already exists", not "malformed".
  - [x] Shape confirmed **from the live object**: `target: branch`, `enforcement: active`,
        `conditions.ref_name.include: ["~DEFAULT_BRANCH"]`, one `required_status_checks` rule whose only
        context is **`build-test-analyze`**, and `bypass_actors: [{RepositoryRole, 5, always}]`.
  - [x] `strict_required_status_checks_policy`: **`false`**, reasoning recorded in `docs/CiGate.md`.
  - [x] **Exported from the live API and committed** to `.github/rulesets/main-required-checks.json` — the
        platform's own object, so every literal in it is observed rather than asserted. **The `<ADMIN_ROLE_ID>`
        trap resolved:** `actor_id: 5` **is** the built-in admin role, confirmed by the ruleset reporting
        **`"current_user_can_bypass": "always"`**.
  - [x] **Verified empirically, both directions:**

        | check | before | after |
        |---|---|---|
        | PR **#7** with a red `build-test-analyze` | `mergeable: MERGEABLE`, `mergeStateStatus: **UNSTABLE**` — merge allowed | `mergeStateStatus: **BLOCKED**` |
        | `branches/main.protected` | `false` | **`true`** |
        | required contexts | *(none — `rulesets: []`)* | **`build-test-analyze`**, and **not** `portability-probe (ubuntu, non-gating)` |
        | admin bypass | — | **`current_user_can_bypass: "always"`** |

    - [x] A red PR **cannot** be merged — proven on throwaway branch `ci/ruleset-block-proof` (commit
          `1409002`, one deliberately failing test; run #80 red on step `Test`). **PR closed and branch
          deleted** after the measurement.
    - [x] The admin bypass holds. **Not** proven by a direct push to `main` — this session is forbidden from
          pushing there — but by the platform's own authoritative field, `current_user_can_bypass: "always"`,
          read off the applied ruleset. § Owner actions item 2 still stands as the lived confirmation.
    - [x] `portability-probe (ubuntu, non-gating)` is **absent** from the required contexts, as R8 requires.

  **⚠️ Correction worth carrying — `rules/branches/main` is NOT a bypass check.** It lists the rules applying
  to the **branch** and returns them **whether or not the caller can bypass**, so an admin who *does* bypass
  still sees the rule listed. Reading it as "empty ⇒ I bypass" produced a false alarm here that looked exactly
  like a misconfigured bypass actor. Only **`current_user_can_bypass`** on the ruleset answers "does this bind
  *me*". `docs/CiGate.md` now says so explicitly, because the wrong reading sends you chasing a
  non-existent misconfiguration — and the tempting "fix" is to weaken the rule.

- [x] **Task 7 — Document it (AC: #2, #5)**
  - [x] `docs/CiGate.md` written: the required context string and **why it is the job name not the workflow
        name**; why `portability-probe` must never be required — with the **measured** evidence (runs #63 and
        #75 both had a failing step inside a job that reported `success`); the admin bypass and what it means
        for the owner's direct-push workflow; why `strict_required_status_checks_policy` is `false`; that
        **GitHub does not read `.github/rulesets/`**; re-apply and verify commands; the Pages-independence
        confirmation for AC #2; and the ADR 0040 §9 tag-inheritance query shape.
  - [x] Cross-referenced from `docs/SonarCloudSetup.md` — a note at the top scoping that document to the
        *analysis* half and pointing at `CiGate.md` for the *gating* half, so the two cannot drift.
  - [x] **No README CI badge added**, and `CiGate.md` records that as a deliberate deferral to 25.2 / ADR 0035
        rather than an oversight.
  - [ ] ⚠️ **One placeholder remains:** the doc references the ruleset id as `<id>` because the ruleset could
        not be created (Task 6). Substitute the real id when it is applied.

- [x] **Task 8 — Record the handoff (AC: #4)**
  - [x] 16.4's query shape recorded in `docs/CiGate.md` § "How a release tag inherits this gate" — including
        the trap that a **run-level** conclusion is not sufficient (`portability-probe`'s job-level
        `continue-on-error` lets a run report `success` while that job is red), so 16.4 must query the
        **`build-test-analyze` job's** conclusion. Repeated in § Handoff below.
  - [x] NFR9 scope stated plainly below: **`npm ci` only**, and it was closed by `0b1f561`, not by this story.
  - [x] Findings raised-but-not-fixed recorded with routes: **F1** (no retry after a failed generation pass →
        Epic 6) and **F2/F3** (documentation corrections) in § Dev Agent Record and § Handoff.

### Review Findings

_Code review 2026-08-08, worktree `code-review-16-2`, cut from `85d4c5c`._

**Outcome: 1 decision resolved (owner chose to ACCEPT the `integration_id` risk, recorded in `docs/CiGate.md`),
13 patches applied, 2 deferred, 4 dismissed. All 14 applied and verified.** Suite after: **3064 passed / 0
failed / 3 skipped** (`dotnet build SpecScribe.slnx --no-incremental` → 0 errors first). The higher count than
the story's 2978 floor is sibling stories merged since `a2eee2a`, not this review. `FileWatcherServiceTests` —
the class three of the patches touch — was run **five times clean** (11/11 each, once inside the full suite),
because this story's own standard of proof for that class is repetition, not a single green run.

**⚠️ STATUS DELIBERATELY LEFT AT `review`, NOT `done`.** The workflow's rule would set `done` once every finding
is resolved, and every finding is. It is being held back anyway because **two of the three review layers never
ran** (see below) — marking a story `done` at epic-end review asserts it was reviewed, and one third of this one
was. Flipping to `done` is a one-line change once the Blind Hunter and Acceptance Auditor layers are re-run and
come back clean. Nothing else is outstanding.

**Scope.** Reviewed by **File List and hunk**, not commit range (CLAUDE.md § Scoping a code review). The subject
is `07bdb79..a2eee2a` — four commits, **all exclusively Story 16.2**; no sibling story is bundled in that range.
**Excluded:** Story 16.4's later +17 lines to `docs/CiGate.md` (commit `e0ea4b2`) are 16.4's to review, not this
story's. **Attribution handoff:** finding **R9(b)** below is evidenced by tagging code that exists only in the
16.4 session's *uncommitted* working tree — `build-test-analyze.yml` was 424 lines at `a2eee2a` and is 698 lines
now. It is recorded here because 16.2 authored the query being broken, and routed to 16.4 so it cannot fall
between the two reviews.

**⚠️ Two of three review layers failed and this review is therefore INCOMPLETE.** The Blind Hunter (general
adversarial) and Acceptance Auditor subagents both terminated on an API session limit before returning findings.
Only the Edge Case Hunter completed. The reviewer independently performed much of the acceptance-audit checklist
inline — see § "Independently verified" below — so the acceptance dimension is partly covered, but **the general
adversarial dimension is entirely unexamined**. Re-run those two layers before treating this story as reviewed.
The Blind Hunter's last recorded action was an intent to *empirically* settle the Windows delete-sharing question
that finding **R4** raises analytically; that measurement was never taken.

**Independently verified by the reviewer (holds — recorded so a re-run need not redo it):** AC #6's scope guard
(`git diff 07bdb79..a2eee2a -- src web extension` is **empty**); AC #3's lockfile bound (`0b1f561` adds exactly
one `version` and one `resolved` line, both `@emnapi/runtime@1.11.3`, zero removals); the exported ruleset JSON
matches every literal the record claims (`target`, `enforcement`, `~DEFAULT_BRANCH`, sole context
`build-test-analyze`, `portability-probe` absent, `RepositoryRole 5 always`); the required context equals
`jobs.<id>.name` (`build-test-analyze.yml:42`) and not the workflow name; `continue-on-error` placement matches
the doc (`:218`, `:399`, job-level `:457`, absent on Build/Test); cited SHAs `0b1f561`, `48c050c`, `f7e812f`,
`07bdb79` are real and do what the record says; the re-apply recipe correctly strips server-assigned fields.
**Task 7's still-unchecked "`<id>` placeholder remains" subitem is stale bookkeeping, not a defect** — the only
`<id>` in `docs/CiGate.md` is `jobs.<id>.name` YAML notation at `:26`; Completion Note 9 is correct.

- [x] [Review][Decision] **The required check is satisfiable by a check run the workflow never produced** — `required_status_checks[0]` in `.github/rulesets/main-required-checks.json:29-31` carries no `integration_id`, so GitHub matches the context by **name only**. Any GitHub App or user holding `checks: write` can `POST /check-runs` a green `build-test-analyze` and satisfy the gate without the workflow running. Pinning `integration_id` to the GitHub Actions app closes it, but that is a change to live repository settings only the owner can apply, and on a single-maintainer public repo the owner may reasonably accept the risk. **Owner's call: pin it, or record the acceptance in `docs/CiGate.md`.**

- [x] [Review][Patch] `Evaluate`'s catch set misses two exceptions the poll path really throws [tests/SpecScribe.Tests/FileWatcherServiceTests.cs:164-170] — `SiteRegion.Read` throws `InvalidOperationException` when the manifest exists but lacks the route (`SiteRegion.cs:49-52`) and `KeyNotFoundException` when the chunk lacks it (`SiteRegion.cs:57`); neither is caught. `FileWatcherServiceTests.cs:273` — **a line this story rewrote** — polls `Read(Site, "sprint.html")` with **no `Exists` guard**, and this story's own captured diagnosis proves `route in IR : False` occurs mid-rebuild. This re-opens the exact loud-failure class the story closed for `JsonException`, on the exact test it fixed. (The manifest-absent path is already safe: it throws `FileNotFoundException`, which is an `IOException`.)

- [x] [Review][Patch] `Diagnose`'s early return discards the evidence that actually solved this bug [tests/SpecScribe.Tests/FileWatcherServiceTests.cs:186-187] — if `SiteRegion.Exists` throws at diagnosis time (mid-wipe / delete-pending / torn — the *most likely* state when the poll just timed out for that reason), `Diagnose` returns immediately with only the exception type, dropping the already-computed `source` **and** the events list. The events list is precisely what identified the root cause (`Error … pages-root.json … used by another process`). Fall through to the full report with a tri-state instead of returning. Same site: because `Diagnose` runs after the bound with the watcher still converging, it can also report `route in IR : True` / `marker in page : MARKER-V2` attached to an `Assert.Fail` — re-evaluate the predicate once and label a late convergence explicitly.

- [x] [Review][Patch] The share-mode widening's safety argument rests on coverage that does not exist [tests/SpecScribe.Tests/SiteRegion.cs:290-293] — the new doc comment argues the change cannot mask a generator defect because "every watch-mode test asserts `DoesNotContain(Observed(), … Error)`". That is **false for 4 of the 11 tests** in the class (`FileWatcherServiceTests.cs:422, 478, 542, 615`), and in three of them the events are structurally *unobservable* because the watcher is constructed with a discarding sink `_ => { }` (`:486`, `:566`, `:630`). Those three are the concurrency-race guards — `ConcurrentDebouncedPasses_LeaveTheDeltaSidecarCoherent`, `ATopologyPass_RacingConcurrentOrdinaryPasses_NeitherStealsNorLeaksTheFullDeltaFlag`, `RunTopologyPass_SetsTheSharedTriggerLabel_EvenWhenNoFileLevelPassRanFirst` — i.e. exactly where a masked generator error would matter most. Correct the claim, or add the assertion to those four.

- [x] [Review][Patch] On Windows, `FileShare.Delete` narrows the wipe race but does not close it, and the comment states the property unconditionally [tests/SpecScribe.Tests/SiteRegion.cs:278-289] — `FileShare.Delete` lets `DeleteFile` succeed, but the deletion is only *pending* until the last handle closes and the directory entry persists, so `Directory.Delete(OutputRoot, recursive: true)`'s `RemoveDirectory` on the parent can still fail with `ERROR_DIR_NOT_EMPTY` → `IOException`, aborting the pass exactly as before. The 50-iteration proof bounds the residual rate but cannot show it is zero. Soften the comment to the property Windows actually provides. The product-side "a failed pass is never retried" half is already correctly routed as **F1** → Epic 6; this is only the doc claim. **⚠️ This finding is analytic, not measured** — the layer that intended to measure it died before doing so.

- [x] [Review][Patch] The fix is a no-op on Linux, yet is presented as closing a class first observed on Linux [tests/SpecScribe.Tests/SiteRegion.cs:278-289] — .NET on Unix has no mandatory locking; `FileShare` is advisory and `FileShare.Delete` carries no meaning, since `unlink` always succeeded and an open fd keeps reading the unlinked inode. Linux coverage of the torn-read class therefore remains **solely** the `catch (JsonException)` at `FileWatcherServiceTests.cs:169`. State the platform scope, or a future maintainer reads the flake as fixed and removes that catch — the same trap finding **F2** already corrected once in this file.

- [x] [Review][Patch] `DeletingEpicsFile_…`'s four-way wait can pass vacuously mid-wipe [tests/SpecScribe.Tests/FileWatcherServiceTests.cs:246-251] — all four conditions short-circuit to `false` when the manifest is absent (`SiteRegion.cs:65`, `:100`), and `GenerateAll`'s wipe deletes the manifest. So the "settled state" the comment at `:240-245` says it is waiting for is indistinguishable from "sampled mid-wipe", and the wait can return true without the removal having converged. Require the manifest to be present (or N consecutive stable readings, as `OutputRootInsideTheSourceRoot_…` already does at `:451-461`). Pre-existing, but this story's change makes the wipe complete cleanly rather than abort, so the window is now routinely reachable.

- [x] [Review][Patch] The only documented recovery path in `docs/CiGate.md` cannot run on this machine [docs/CiGate.md:113] — it opens with `jq`, and `jq` is **measured absent** from both PowerShell and Git Bash here. No prerequisite is stated. Use `gh api --jq` or a `node -e` filter, both already available, or declare the dependency.

- [x] [Review][Patch] `docs/CiGate.md`'s commands are POSIX sh, but this project's primary shell is PowerShell [docs/CiGate.md:113-118, 138-139, 206-207] — three measured breakages: (a) `/tmp/ruleset.json` resolves to `C:\tmp\…`, which does not exist, so the redirect fails and `--input` then reads a missing file; (b) the trailing `\` is not a PowerShell line continuation, so the `jq` line and the redirect parse as separate statements; (c) PowerShell strips the embedded double quotes in `--jq 'select(.type=="required_status_checks")'`, so `jq` sees an undefined function — **this is the identical trap this story's own Dev Agent Record records as F3**, and the doc ships the unescaped form with no note. Mark the block Git-Bash-only or escape for PowerShell.

- [x] [Review][Patch] The 16.4 handoff query can report a **false green**, two independent ways [docs/CiGate.md:196-198] — (a) `SHA=$(…)` is a POSIX assignment; under PowerShell it is a parse error, and if only the second line runs, `$SHA` interpolates to empty, GitHub ignores an empty `head_sha`, and the query returns **every** run of the workflow — an operator can read a green conclusion off an unrelated commit and gate a release on it. (b) `.object.sha` on an **annotated** tag returns the tag object's SHA, not the commit's, so `head_sha=` matches nothing and the empty result reads as "no green run". Dereference via `.object.type == "tag"` → `git/tags/<sha>`, and assert non-empty before use. **Attribution:** the annotated-tag evidence (`git tag -a -f`) lives in the 16.4 session's uncommitted work, not in 16.2's baseline — **routed to Story 16.4**, raised here because 16.2 authored the query.

- [x] [Review][Patch] The ruleset id is hardcoded in five places with no staleness path [docs/CiGate.md:105, 110, 118, 134 and .github/rulesets/main-required-checks.json:8] — if the ruleset is ever deleted, the documented `POST` mints a **new** id and every one of those literals silently points at a ruleset that no longer exists. Resolve by name instead (`gh api …/rulesets --jq '.[] | select(.name=="main: require build-test-analyze") | .id'`) and note that the committed `id`/`node_id`/timestamps go stale after any recreate.

- [x] [Review][Patch] Two lockout/stall scenarios are undocumented, and both look like a misconfigured context string [docs/CiGate.md:35-37, 75-76] — (a) if the check is never reported (workflow renamed or deleted on a PR branch, a `cancelled` conclusion from `concurrency.cancel-in-progress`, or a fork PR skipping the job) the PR blocks indefinitely and only the bypass clears it; (b) if `bypass_actors` is ever lost, a required check binds pushes too and a new commit can never carry a passing check, so **the repair cannot itself be pushed** — the doc says "fix the actor" without saying the repair is API/UI-only and pointing at the `PUT` at `:118`. Document the diagnosis (`gh api …/commits/<sha>/check-runs`) and that the resolution is the bypass, never weakening the rule.

- [x] [Review][Patch] A default-branch rename silently splits the ruleset from the workflow [.github/rulesets/main-required-checks.json:14-20 vs .github/workflows/build-test-analyze.yml:20-23] — the ruleset follows `~DEFAULT_BRANCH` automatically; the workflow is pinned to `branches: ["main"]`. After a rename the gate never runs on the new default branch, the required context stays permanently pending, **every** PR blocks, and every verification command in `CiGate.md` hardcodes `main` and reports `protected: false`. A one-line note is enough; no config change is required today.

- [x] [Review][Patch] The workflow header still claims this story delivers "release-branch coverage" [.github/workflows/build-test-analyze.yml:3-5] — §R10 deliberately decided `main` is the only target and that no release-branch pattern should be invented. The comment is now stale and invites exactly the misreading R10 was written to prevent. Update it to say `~DEFAULT_BRANCH` is the whole of the coverage, by decision.

- [x] [Review][Defer] `Diagnose` is wired to only 3 of the 12 `WaitFor` sites in the class [tests/SpecScribe.Tests/FileWatcherServiceTests.cs:222, 246, 298, 300, 318, 352, 389, 412, 417] — deferred, pre-existing. Its own doc comment argues an exhausted bound is "now the ONLY way this class reports a non-convergence", but the other nine waits still fail mute with a bare `Assert.True(WaitFor(…), "message")`, including `:412`/`:417` which exercise the same delete-during-regeneration race. Generalizing is not a mechanical change — `Diagnose` hardcodes the `MARKER-V1`/`MARKER-V2` heuristic at `:193-195` and would need parameterizing — so it is a follow-up, not a patch.

- [x] [Review][Defer] `BurstOfSaves`'s post-wait coherence sweep reads the IR entirely outside `Evaluate` [tests/SpecScribe.Tests/FileWatcherServiceTests.cs:362-368] — deferred, pre-existing. After the wait settles, `Routes(Site)` and a per-route `Read` loop run with the watcher still live and no retry, so a late debounce fire can surface `FileNotFoundException` or the `SiteRegion.cs:49-52` throw with nothing to poll through it. Snapshot the IR before asserting over it, or wrap the sweep in the same retry.

**Dismissed as noise (4):** the ruleset "missing release-branch coverage" as a *gap* (§R10 locked `main`-only
deliberately and pre-warned reviewers; only the stale workflow comment survives, above);
`ArgumentNullException`/`NullReferenceException` from the `!`-suppressed nulls at `SiteRegion.cs:54,57` (requires
the generator to emit JSON `null` for a chunk path, which no code path produces); `InvalidOperationException`
from `GetProperty("pages")` on a non-object manifest root (a truncated write cannot parse as a valid scalar and
still reach that call — `JsonException` covers the realistic torn read); and `Diagnose`'s unguarded
`string.Join(… Observed() …)` as a concurrency hazard (`Observed()` returns `_events.ToArray()` under
`_eventsLock` at `:99-101`, so it is safe).

---

## Dev Notes

### 👤 Owner actions — this story cannot complete without them

1. **`gh auth login`** — interactive; the dev agent must not attempt it. Needs `repo`, `read:org`,
   `workflow`, and repository admin. (Task 1.)
2. **Confirm the ruleset once applied** — you will feel it on your own pushes. If a direct push to `main` is
   ever rejected, that is the bypass misconfigured, and it is a bug in this story's work, not a new policy.

Everything else is the dev agent's.

### The ruleset — shape, and the one trap

Verified against the live GitHub REST documentation on 2026-08-07 (`POST /repos/{owner}/{repo}/rulesets`):

```jsonc
{
  "name": "main: require build-test-analyze",
  "target": "branch",                 // "branch" | "tag" | "push"
  "enforcement": "active",            // "disabled" | "active" | "evaluate"
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "bypass_actors": [
    { "actor_type": "RepositoryRole", "actor_id": <ADMIN_ROLE_ID>, "bypass_mode": "always" }
  ],
  "rules": [
    { "type": "required_status_checks",
      "parameters": {
        "required_status_checks": [ { "context": "build-test-analyze" } ],
        "strict_required_status_checks_policy": false
      } }
  ]
}
```

**⚠️ The trap: `<ADMIN_ROLE_ID>` is NOT in the REST reference.** `actor_type` accepts
`Integration | OrganizationAdmin | RepositoryRole | Team | DeployKey | User`, and `actor_id` is *required*
for `RepositoryRole` — but the numeric id of the admin role is never stated, and **`OrganizationAdmin` does
not apply to a user-owned repository**, which this is. Values circulating publicly disagree with each other.

**Do not guess it.** Create the ruleset once in the web UI with the bypass configured, then `GET` it back and
commit whatever the platform actually returned. That inverts the problem: the committed JSON becomes the
source of truth for re-applying, and no literal is asserted that was not observed. `gh ruleset` is read-only
(`list` / `view` / `check`), so `gh api` is the write path.

**`evaluate` mode is a legitimate intermediate step** if you want the rule reporting-but-not-blocking while
Task 5's two green runs accumulate. The story does **not** complete in `evaluate` — AC #1 says *required*.

### Files being modified — current state, what changes, what must be preserved

| file | current state | this story changes | must be preserved |
|---|---|---|---|
| `tests/SpecScribe.Tests/FileWatcherServiceTests.cs` | 593 lines; the **only** test file using `Thread.Sleep`/`Task.Delay`. `SettleTimeout` 20 s (:28); `WaitFor` polls at 25 ms (:134-143); `Evaluate` swallows `IOException` + `UnauthorizedAccessException` (:149-154); `WaitForQuiet` requires a stable count across more than one debounce (:107-129, added by code review 2026-07-29) | `Evaluate`'s catch set, and its doc comment | The outcome-polling design. Every existing wait bound. `WaitForQuiet`'s stability requirement — a code review put it there deliberately. |
| `tests/SpecScribe.Tests/SiteRegion.cs` | `ReadShared` uses `FileShare.ReadWrite` (:279) with a doc comment explaining it stopped the test locking the generator's chunk. `Read`/`Exists`/`Routes`/`RoutesUnder` parse that content unguarded (:44,56,66,76) | possibly — **if** you decide this layer owns the torn-read guard (Task 4). Pick one layer. | `FileShare.ReadWrite` itself. Removing it re-opens `BurstOfSaves`'s lock failure, which is the worse bug. |
| `web/package-lock.json` | `lockfileVersion: 3`. Top-level `@emnapi/core@1.11.3` present (:570); top-level `@emnapi/runtime` **absent**. Root `packages[""]` carries no `engines` mirror. | regenerated via `--package-lock-only` | Every existing dependency version. |
| `.github/workflows/build-test-analyze.yml` | 425 lines, densely commented; the comments encode measured failure modes | **comments only, if anything.** The step order is explicitly load-bearing (:220-237) and must not be reordered | Everything. Especially: `--deep-git` on the generate (:280-293), `SPECSCRIBE_PACKAGE_BUILD=1` before `npm ci` (:243-246), `build:package` and never `build` (:259-270), `continue-on-error` on the two SonarScanner steps and **not** on Build/Test (:196-200). |
| `.github/rulesets/main-required-checks.json` | does not exist | **NEW** | — |
| `docs/CiGate.md` | does not exist | **NEW** | — |

### Testing standards

- xunit **2.9.3** + `Microsoft.NET.Test.Sdk` **17.14.1** + `xunit.runner.visualstudio` **3.1.4** +
  `Xunit.SkippableFact` **1.4.13** (`tests/SpecScribe.Tests/SpecScribe.Tests.csproj`). There is **no**
  `xunit.runner.json` and no `.runsettings`, so default collection parallelism is in force — part of why a
  timing-sensitive class contends with the rest of the suite.
- CI coverage is **OpenCover**, not coverlet's Cobertura default: SonarScanner for .NET reads
  `sonar.cs.opencover.reportsPaths` and does not document Cobertura for C#
  (`build-test-analyze.yml:203-208`). Do not change the format.
- The flake proof is **repetition under load**, not a single green run (Task 4).

### Project structure notes

- `.github/rulesets/` is a new directory. **GitHub does not read it.** Rulesets live in repository settings
  and there is no in-repo config mechanism for them. It is a **reviewable record and a re-apply source**, and
  `docs/CiGate.md` must say so plainly or a future reader will assume editing the file changes the rule.
- No `CODEOWNERS`, no PR template, no other `.github/` configuration exists today. Adding them is not this
  story's work.
- Local `main` (`35437b9`) is **two commits ahead of `origin/main`** (`838d591`), and those two unpushed
  commits carry `16-1-spike-report.md` and ADR 0040. All CI-side observations here were measured at
  `838d591`.

### Scope guard — why nothing structural changes

No renumber, no story added or removed, no spike inserted. Per CLAUDE.md, structural scope changes must land
in `epics.md` **and** `sprint-status.yaml` in the same change — **there are none here**, recorded explicitly
so the absent `epics.md` diff reads as a decision rather than an omission. AC #3–#6 elaborate the existing
story; they do not change the epic's shape.

Likewise **no new ADR**. ADR 0040 §9 already decides the tag/gate relationship, ADR 0035 owns the quality
gate, ADR 0033 governs new drift gates — and this story adds none. CLAUDE.md's "propose an ADR without being
asked" trigger is for decisions that change shared architecture or a cross-cutting contract; a ruleset that
*enforces* an already-ratified decision is not one. **If Task 3's classification turns up a structural defect
in the drift gates, that is different — raise it, and propose the ADR then.**

### Concurrent-work discipline (CLAUDE.md)

- **Verify after every edit.** Grep for what you just wrote before relying on it — a `Charts.cs` edit has
  silently vanished in this repository before.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.** Another session's uncommitted work may be
  in the tree. Five worktrees are live right now.
- **Expect a gate to move under you** because of a sibling story. Establish causality before touching any
  baseline — and note CLAUDE.md's "worktrees are unavailable on this machine" line is **stale**: 16.1 found
  five live worktrees, and the last four commits on `main` are worktree merges.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 16.2] — ACs #1–#2 verbatim; the `AMENDED 2026-07-25` block (lines 2922-2931) forbidding a second workflow
- [Source: _bmad-output/planning-artifacts/epics.md:138] — NFR9 text
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#7] — the weak NFR9 reading; `npm ci` named as 16.2's to close
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#9] — the required-check string; a tag inherits the gate by being green on `main`
- [Source: _bmad-output/implementation-artifacts/16-1-spike-report.md:453-497] — NFR9 gap table, the `npm ci` finding, the routing to this story
- [Source: _bmad-output/implementation-artifacts/16-1-spike-report.md:611-618] — the flake symptom and its known-class history
- [Source: _bmad-output/implementation-artifacts/16-1-spike-report.md:678] — the 16.2 handoff row
- [Source: _bmad-output/implementation-artifacts/25-1-sonarcloud-onboarding-and-ci-analysis.md:734-747] — Task 8 handoff table (workflow path, job name, must-not-be-required)
- [Source: .github/workflows/build-test-analyze.yml:1-13] — the file's own note addressed to this story
- [Source: .github/workflows/build-test-analyze.yml:335-348] — why `portability-probe` must not be required
- [Source: tests/SpecScribe.Tests/FileWatcherServiceTests.cs:145-154] — `Evaluate`, the exception filter that misses `JsonException`
- [Source: tests/SpecScribe.Tests/SiteRegion.cs:270-282] — `ReadShared`'s `FileShare.ReadWrite` and why it exists
- [Source: CLAUDE.md#Concurrent work on shared main] — never regenerate a baseline reflexively; non-incremental rebuilds
- [Source: https://docs.github.com/en/rest/repos/rules] — `POST /repos/{owner}/{repo}/rulesets` body schema (read live 2026-08-07)
- [Source: https://nodejs.org/dist/index.json] — Node 24.11.1 → npm 11.6.2; Node 24.18.1 → npm 11.16.0 (read live 2026-08-07)

---

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (1M context) — `claude-opus-5[1m]`, via `bmad-dev-story`. Executed 2026-08-07 in worktree
`.claude/worktrees/story-16-2-dev` on branch `worktree-story-16-2-dev`, cut from `origin/main` at `07bdb79`.

### ⚠️ The baseline moved before this story started — three of its premises were already stale

`baseline_commit: 35437b9` is **preserved** in the frontmatter as authored, but it is **not** where this ran.
`main` advanced to `07bdb79` between create-story and dev-story, and **two of those commits did this story's
work for it**. Established by reading the commits, not assumed:

| story premise (as authored) | what was actually true at `07bdb79` |
|---|---|
| R2: *"`origin/main` is RED at `838d591`, run #74"* | **Stale.** Run #78 at `07bdb79` is **green**, all steps. |
| R4 / AC #3: *"`npm ci` fails — repair the lockfile"* | **Already fixed** by `0b1f561` ("CI fix: repair the lockfile…"). Verified rather than redone. |
| R3 / Task 4: *"add `JsonException` to `Evaluate`'s catch set"* | **Already landed** by `48c050c`. Verified — **and proven insufficient**, see § Task 4. |

Nothing was regenerated or re-fixed on the strength of the story text. Each premise was re-measured first
(CLAUDE.md § "Never regenerate a gate's baseline reflexively — establish causality first").

### Debug Log References

- Run enumeration + step-level attribution: `/actions/workflows/build-test-analyze.yml/runs?per_page=100`
  then `/actions/runs/{id}/jobs`, filtering `steps[].conclusion == "failure"`. **78 runs, 27 failures + 1
  cancelled.** Run **#75** (`3312256`) post-dates the story's own sweep and is classified below.
- Job logs read with `gh run view <run> --job <job> --log`. **`gh api …/jobs/{id}/logs` does not work here** —
  it returns terminal escape sequences and exits non-zero unless `--allow-escape-sequences` is passed. Task 1's
  suggested command needs that correction for whoever repeats this.
- `gh` was **already installed and authenticated** (`IntegerMan`, keyring) with `permissions.admin == true`.
  **Task 1's owner handoff was not needed.** It is not on `PATH` — invoke by full path
  `C:\Program Files\GitHub CLI\gh.exe`.
- In PowerShell, `gh --jq` expressions containing double-quoted string literals must escape them (`\"`), or
  PowerShell strips the quotes and `jq` parses `build-test-analyze` as arithmetic (`function not defined:
  analyze/0`).
- Flake harnesses and raw logs: `$CLAUDE_JOB_DIR/tmp/` (`flakeproof.*`, `sprintrepro.*`, `proof2.*`, `g*.log`).

### Task 3 — classification of every failed run (AC #4)

Classification of the **gating** job (`build-test-analyze`) unless stated. Evidence is the platform's own
step-level `conclusion` plus the job log.

| run | sha | failing step | failing test / gate | classification | evidence |
|---|---|---|---|---|---|
| 1 | `252087f` | Test | `CommitDetailTemplaterTests.RenderPage_BinaryRowShowsMarkerNotZeroChurn` | genuine-regression-since-fixed | `Assert.DoesNotContain` found `"+0"` at pos 14250 inside a rendered date `"…21:18 UTC+00:00"` — the assertion was matching the timezone offset, not churn. |
| 6 | `485cd18` | Build | `__CiRedProof.cs` CS1519/CS1002 | **environmental — deliberate** | `workflow_dispatch` on `ci/measure-no-coverage`; an intentionally broken file committed to prove CI goes red. Not a defect. Precedent for this story's own AC #5 proof. |
| 11, 12, 13 | `2c1128d`, `ddbc754`, `98bbebe` | Test | `GenerateAll_GoldenContentFingerprint_…` (13 also `TEMP_PerFileFingerprintDump`) | genuine-regression-since-fixed | Line-ending era `.gitattributes` was written to close. **Subject retired entirely** by ADR 0034 / Story 23.6 — the test no longer exists. |
| 18 | `f1fcdb0` | SonarScanner end | — (tests **passed** 2394/2394) | environmental | `Post-processing failed. Exit code: 1`. Predates the `continue-on-error` Story 25.1's code review added to both scanner steps. Structurally cannot recur. |
| 24 | `aed74c0` | Test | `GoldenContentFingerprint` | genuine-regression-since-fixed | Same retired subject as 11–13. |
| 30 | `c1a6ee5` | Check web drift gates | **`check:assets`** | genuine (real drift) | `missing: specscribe.js, plotly-hierarchy.min.js, prism.css, prism.js` — `web/public/` unsynced. |
| 33 | `98d40d8` | Test | `TryParseTraceSummary_NonGateEligibleRun_OmitsGateStatus_WithoutFailing` | genuine-regression-since-fixed | `Assert.Null` failure, actual `"CONCERNS"`. Deterministic logic bug, not timing. |
| 35, 36 | `811ba17`, `06b300c` | Check web drift gates | `check:ir-content` | genuine (real drift) | `ir-content.manifest.json: out of sync with the sheet it documents` — extract not re-run after a stylesheet edit. |
| 40 | `82880ba` | Check web drift gates | `check:ir-content` | genuine (real drift) | `+2 / -2` — the `sprint-lane-empty` → `sprint-filter-empty` rename. |
| 47, 48, 49 | `a8c97f3`, `d9b50f1`, `7510a70` | Test | `GenerateAll_GoldenIrFingerprint_…` | genuine-regression-since-fixed | Fingerprint family, retired with the C# writer (ADR 0034). |
| 51 | `6df8e0d` | Check web drift gates | `check:ir-content` | genuine (real drift) | `+17 / -24`, the `ss-relgraph` family landing. |
| 54 | `3eb3429` | Generate the IR | — (tests **passed** 2888) | genuine-regression-since-fixed | `generated=481 updated=0 skipped=16 errors=1`. |
| **58, 62** | `b397084`, `9f4cb5d` | Check web drift gates | `check:ir-content` | **environmental — GATE-HARNESS DEFECT, since fixed** | `+4 / -180` and `+4 / -182` — **exactly** the signature `build-test-analyze.yml:288-292` documents in advance. CI was generating **without `--deep-git`**, so the gate validated a narrower corpus than ships. Fixed by `f7e812f`, which added the flag. |
| 59, 60 | `921f708`, `e48070f` | Test | `SiteGeneratorDesignSystemTests`, `…ReadmeTests`, `WebviewRenderAdapterTests` (multi-class) | genuine-regression-since-fixed | `FileNotFoundException … /site/design-system` and `No IR manifest at …/spa/manifest.json` — `GenerateAll` did not complete. A real generator regression, fixed same day. |
| 61 | `49a3e83` | Test | `IdeasTests` ×4 | genuine-regression-since-fixed | `Assert.True` false on forge-workspace routes. |
| 63 | `f7e812f` | Check web drift gates | `check:ir-content` | genuine (real drift, residual) | `+1 / -0` `.ownership-legend-swatch.owner-author-2`; closed by `0ef46e9`. |
| 66 | `507ac37` | *(cancelled)* | — | environmental | Superseded PR push; `cancel-in-progress` by design. |
| 67 | `613ff0b` | Check web drift gates | `check:ir-content` | environmental (mirror of 58/62) | `+181 / -4` — the **inverse** signature: the branch's committed layer had been extracted *without* `--deep-git`. Regenerated in `b4e3b88`. |
| 68 | `b4e3b88` | Check web drift gates | `check:ir-content` | genuine (real drift, residual) | `+1 / -0`, same `owner-author-2` rule as 63. |
| **74** | `838d591` | Test | `FileWatcherServiceTests.SprintStatusYaml_AddedThenEditedThenRemoved` | **LIVE-FLAKE** | `JsonReaderException` at `SiteRegion.Exists`, `BytePositionInLine: 8192` — an 8 KB flush boundary. |
| **75** | `3312256` | Test | `FileWatcherServiceTests.BurstOfSaves_CoalescesAndLeavesCoherentOutput` | **LIVE-FLAKE** | `"The input does not contain any JSON tokens"` at `SiteRegion.Exists`. Same class. |

**Totals: 20 genuine-regression-since-fixed · 5 environmental · 1 cancelled · 2 live-flake.**

**The red tip named in the story (run #74) is a LIVE-FLAKE**, and so is #75. Every other failure in 78 runs is
either a real regression that was fixed at the time or an environmental/harness condition that has since been
closed. **The ~36% historical failure rate is not a ~36% flake rate** — the live-flake population is 2 runs,
both of the same defect, and both are addressed (see Task 4).

**Two `check:ir-content` corrections to the story text.** R7 asked whether any of the 10 drift-gate failures
were structural. **Two were** (58, 62) — and the structural defect was in the *workflow*, not the gate:
`--deep-git` was missing from CI's generate. That is already fixed by `f7e812f`, so it is recorded, not
routed. **No baseline was regenerated at any point in this story.** Runs 67/68 also confirm the trap CLAUDE.md
warns about actually happened on a sibling branch: `+181/-4` is a committed layer that had been pruned by a
shallow generate.

### Task 4 — the `Test` flake: the story's stated fix was already landed, and was NOT sufficient

`48c050c` had already added `catch (JsonException)` to `FileWatcherServiceTests.Evaluate`, which is exactly
what Task 4 prescribes. **Verified present, and then verified inadequate.**

**Correction to that commit's rationale.** It describes the torn read as *"the Linux-only transient state"*,
reasoning that Windows share modes make a mid-write read fail before returning anything. **Run #74 is a
Windows run** (`build-test-analyze` is `windows-latest`) and hit the identical `JsonReaderException` at an
8 KB boundary. `SiteRegion.ReadShared` asks for `FileShare.ReadWrite`, which removes precisely the locking
that argument depends on. The fix is right; its stated scope was too narrow.

**The residual defect, found by measurement rather than reasoning.** With `JsonException` swallowed,
`SprintStatusYaml_AddedThenEditedThenRemoved` still failed — now as a **mute 20 s timeout**, because a
swallowed exception is indistinguishable from "not yet". Rates measured on this machine:

| harness | iterations | failures |
|---|---|---|
| `FileWatcherService` class, concurrent `dotnet build` + Node build | 16 loaded (of 20) | **1** |
| `SprintStatusYaml…` alone, sustained load | 40 | **1** |
| **full suite, no artificial load at all** | 1 | **1** |

To find out *why* a mute timeout happened, a `Diagnose` helper was added (`FileWatcherServiceTests.cs`) that
renders the state a timed-out poll left behind. It is **failure-path only** (`if (!WaitFor(…)) Assert.Fail(…)`,
not `Assert.True(cond, msg)`, whose message argument evaluates eagerly on every pass). It caught the cause on
the next reproduction:

```
editing sprint-status.yaml should refresh the board
  source on disk : last_updated: MARKER-V2\n…      <- the edit WAS delivered
  route in IR    : False                            <- sprint.html gone from the IR entirely
  marker in page : <neither marker present>
  events         : [Updated …/sprint-status.yaml "data source",
                    Error …/sprint-status.yaml "The process cannot access the file
                    'pages-root.json' because it is being used by another process."]
```

**Root cause: the test's own poll handle was blocking the generator's output wipe.**
`SiteRegion.ReadShared` opened with `FileShare.ReadWrite`, which admits a concurrent reader and writer **but
not a concurrent *deleter***. `GenerateAll`'s whole-tree routes `Directory.Delete(OutputRoot, recursive: true)`
before repopulating. With a poll holding a handle, that wipe failed on `pages-root.json`, the pass **aborted
part-way through the wipe**, and the IR was left with the route gone and nothing queued to restore it. A
failed pass is never retried, so the poll had nothing left to converge to and burned its full 20 s bound.
**Stuck, not slow** — which is why widening the timeout would never have helped, exactly as the story insisted.

**Fix — one flag, in the layer that already owns this concern:** `FileShare.ReadWrite | FileShare.Delete` in
`SiteRegion.ReadShared`. This answers Task 4's "decide which layer owns the guard" explicitly:

- **`SiteRegion.ReadShared` owns it** — it is the only long-lived handle any test holds on a generated file,
  and it is where the previous half of this same fix (Story 23.6's `FileShare.ReadWrite`) already lives.
- **`Evaluate` keeps the transient-exception guard** it already has; the two are complementary, not duplicate.
- **`SiteRegion.Read`/`Exists`/`Routes` were NOT separately guarded** — they all funnel through `ReadShared`,
  so fixing it once covers every caller. No guard was added in two places.
- `SettleTimeout` was **not** widened, no sleeps were added, nothing was retried, nothing was quarantined.

### Finding raised, NOT fixed — routed (product-side, out of scope per AC #6)

**F1 — a generation pass that fails transiently is never retried, and can leave the site incoherent.**
The test-side contention is fixed above, but the product-side behaviour it exposed is real and is *not* this
story's to change (AC #6 forbids `src/**`). When `GenerateAll` throws mid-wipe, `FileWatcherService` reports a
`GenerationOutcome.Error` event and **stops** — no retry, no fallback rebuild — leaving the output root
part-wiped. In `specscribe watch`, any handle held on a generated file (an editor, a browser dev-server, a file
indexer, antivirus) at the instant a rebuild starts can therefore leave the generated site **permanently stale
or broken until an unrelated edit happens to trigger another pass**.

This is asymmetric with the *directory* watcher, which already treats exactly this hazard as unacceptable:
`CreateDirectoryWatcher`'s `Error` handler calls `ForceTopologyRebuild()` because *"an overflow means the OS
dropped events outright, so logging alone could leave output silently stale"*
(`FileWatcherService.cs:207-215`). The file/generation path has the same hazard and only logs
(`FileWatcherService.cs:242-243`). It is also the NFR2 "degrade, don't lose the event" property.

**Route:** Epic 6 (watch-mode reliability) — needs a named follow-up story. Not absorbed here, and **not**
quarantined: the test is a legitimate guard and it now passes.

### Handoff (Task 8)

*Recorded here rather than in Dev Notes: `dev-story` may only modify frontmatter `baseline_commit`,
Tasks/Subtasks checkboxes, Dev Agent Record, File List, Change Log and Status.*

**→ Story 16.4 (tag-triggered release pipeline).** ADR 0040 §9's "the tagged commit is already green on
`main`" is now a queryable condition. The shape, and its one trap:

```sh
SHA=$(gh api repos/IntegerMan/SpecScribe/git/refs/tags/<tag> --jq '.object.sha')
RUN=$(gh api "repos/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml/runs?head_sha=$SHA" \
        --jq '.workflow_runs[0].id')
gh api "repos/IntegerMan/SpecScribe/actions/runs/$RUN/jobs" \
  --jq '.jobs[] | select(.name == "build-test-analyze") | .conclusion'
```

⚠️ **Do not gate on the run-level `conclusion`.** `portability-probe (ubuntu, non-gating)` carries
`continue-on-error` at the **job** level, so a run reports `success` even when that job is red — observed at
runs **#63** and **#75**. Query the `build-test-analyze` **job's** conclusion, as above.

**→ NFR9 (ADR 0040 §7).** This story's scope was **one** of the three named gaps — a working `npm ci` — and
even that was closed by `0b1f561` before dev-story began; this story **verified** it under both toolchains
rather than fixing it. Still open, unchanged: `SOURCE_DATE_EPOCH` (**16.4**), version-from-tag (**16.3**),
`<Deterministic>` / SourceLink (**deferred post-preview**). **No broader reproducibility claim is made.**

**→ Findings raised, not fixed.**

| # | finding | route |
|---|---|---|
| **F1** | A generation pass that fails transiently is **never retried**, and can abort mid-wipe leaving the output root incoherent. Asymmetric with the directory watcher, which already forces a fallback rebuild for exactly this hazard (`FileWatcherService.cs:207-215` vs `:242-243`). Real `specscribe watch` impact. | **Epic 6** — needs a named follow-up story. `src/**`, out of scope per AC #6. |
| **F2** | `48c050c`'s message calls the torn read *"the Linux-only transient state"*. **Run #74 is a Windows run** with the identical exception — `FileShare.ReadWrite` removes the locking that argument relies on. Fix correct, rationale too narrow. | Corrected in `SiteRegion.ReadShared`'s doc comment by this story. No code change needed. |
| **F3** | Task 1's `gh api …/actions/jobs/{id}/logs` **does not work** (terminal escape sequences → non-zero exit). Use `gh run view <run> --job <job> --log`. Also: `gh` is installed but **not on `PATH`**, and `--jq` string literals need `\"` escaping under PowerShell. | Recorded here for 16.3 / 16.4, which also need `gh`. |

### Completion Notes List

**Status: all 8 tasks complete, all 6 ACs satisfied.**

1. **The gate is now trustworthy AND required — in that order, which was the story's central constraint.**
   Ruleset **`20567252`** is `active` on `~DEFAULT_BRANCH` requiring exactly `build-test-analyze`, with the
   repository admin bypassing `always`. It was applied only after `main` was green across two consecutive
   runs (#78, #79). **The `POST` was executed by the owner** — this session's permission layer refused writes
   to live repository settings twice, and I did not route around it; the token was sufficient
   (`permissions.admin: true`), so the block was environmental. Everything downstream of the write
   (verification, export, commit, doc, cleanup) was done here.

2. **AC #3 — CLOSED, and by verification rather than by redoing work.** The lockfile was already repaired on
   `main` by `0b1f561`. Proven on both toolchains as AC #3 demands: `npm ci` **actually installed 639
   packages in 27 s** here on Node 24.18.1 / npm 11.16.0, and step `Install web dependencies` is `success`
   in run #78 under `web/.nvmrc`'s pinned Node 24.11.1 / npm 11.6.2. The committed diff is **+18 / −24 with
   exactly one added package and zero `version:`/`resolved:` churn** — within R4's "anything larger must be
   reported" bound.

3. **AC #4 — the classification half is CLOSED and it reframes the story's own headline.** All 27 failures
   + 1 cancelled run are classified: **20 genuine-regression-since-fixed, 5 environmental, 1 cancelled,
   2 live-flake**. The story's "fails ~1 push in 3" is a historical *failure* rate, not a flake rate — the
   live-flake population is **two runs of one defect**. Both are now fixed.

4. **AC #4 — the flake half is CLOSED, but not by the fix the story prescribed.** That fix (`JsonException`
   in `Evaluate`) was already on `main` and **provably insufficient**; it converted a loud exception into a
   mute 20 s timeout. The real cause was `SiteRegion.ReadShared`'s share mode blocking the generator's own
   output wipe. **Fixed in one layer, proven by repetition, nothing retried or quarantined or
   timeout-widened.** Full mechanism in § Task 4.

5. **AC #6 — HOLDS, with one disclosure.** Changes are confined to `tests/SpecScribe.Tests/**` and `docs/**`.
   **No `src/**`, no `web/` source, no `extension/**`.** `check:parity` is byte-identical (24/24, 14/14) and
   the C# pass count moved by exactly the one flaky test. **Disclosure:** AC #6's allow-list names
   `docs/CiGate.md` specifically, and I also edited **`docs/SonarCloudSetup.md`** — because Task 7 explicitly
   instructs cross-referencing from it. Task-sanctioned, but outside the literal AC #6 list, so it is called
   out rather than left for a reviewer to notice.

6. **NFR9 — exactly one of ADR 0040 §7's three gaps is closed (`npm ci`), and not even by this story.**
   `SOURCE_DATE_EPOCH` (16.4), version-from-tag (16.3) and `<Deterministic>`/SourceLink (deferred) all
   remain. **No broader reproducibility claim is made**, per the story's explicit instruction.

7. **No baseline was regenerated, anywhere.** `check:ir-content` went red locally at `+1 / −1368` and was
   proven environmental (fresh worktree, no IR) before anything was touched. Regenerating would have deleted
   ~1368 live rules behind a green gate — the exact failure mode CLAUDE.md and Story 16.1 both warn about.

8. **No structural scope change and no new ADR** — as the story predicted. `epics.md` and
   `sprint-status.yaml` need no scope edit; recorded explicitly so the absent `epics.md` diff reads as a
   decision. The one structural defect Task 3 surfaced (CI generating without `--deep-git`) was **already
   fixed** by `f7e812f`, so it is recorded, not routed, and needs no ADR.

9. **Cleanup done, nothing left live.** PR **#7** is closed and `ci/ruleset-block-proof` deleted; the `<id>`
   placeholders in `docs/CiGate.md` are replaced with the real ruleset id.

10. **One correction is carried forward deliberately**, because getting it wrong is actively dangerous:
    `rules/branches/main` is **not** a bypass check — it lists rules applying to the *branch* regardless of
    whether the caller bypasses them. It briefly looked here like the admin bypass had failed. The
    authoritative field is **`current_user_can_bypass`** on the ruleset. The danger is that the wrong reading
    makes a *working* configuration look broken, and the obvious "fix" is to weaken the rule.

#### 👤 What is left for the owner

1. **Merge `worktree-story-16-2-dev`** — it carries the flake fix, `docs/CiGate.md`, the exported ruleset
   JSON and this record. Nothing else is outstanding.
2. **Confirm on your next direct push to `main`** that it still lands. The bypass is verified by the
   platform's own `current_user_can_bypass: "always"`, but a lived push is the last word. **If one is ever
   rejected, that is the bypass actor being wrong — fix the actor, do not disable the rule.**
3. **Story F1 needs seating in Epic 6** — a generation pass that fails transiently is never retried. Raised,
   routed, not fixed (it is `src/**`, out of scope per AC #6).

### File List

| file | change |
|---|---|
| `tests/SpecScribe.Tests/SiteRegion.cs` | **modified** — `ReadShared` now opens `FileShare.ReadWrite \| FileShare.Delete`; doc comment extended with the causal chain and the Windows correction |
| `tests/SpecScribe.Tests/FileWatcherServiceTests.cs` | **modified** — added the failure-path-only `Diagnose` helper; the three `SprintStatusYaml…` waits now report state on timeout via `if (!WaitFor(…)) Assert.Fail(…)` |
| `docs/CiGate.md` | **NEW** — the required check, the context string, why `portability-probe` is not required, the admin bypass, re-apply/verify commands, Pages independence, ADR 0040 §9 tag inheritance |
| `docs/SonarCloudSetup.md` | **modified** — cross-reference scoping it to the *analysis* half and pointing at `CiGate.md` for the *gating* half |
| `_bmad-output/implementation-artifacts/16-2-continuous-integration-build-and-test-gate.md` | **modified** — this record |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | **modified** — status → `in-progress` |
| `.github/rulesets/main-required-checks.json` | **NEW** — the live ruleset `20567252` **exported from the API**, not hand-authored, so every literal in it (including `actor_id: 5`) is observed |
| ~~`web/package-lock.json`~~ | **NOT modified** — already repaired on `main` by `0b1f561`; verified, not redone. |

**Created outside the working tree and since cleaned up:** branch `ci/ruleset-block-proof` (commit `1409002`,
added only `tests/SpecScribe.Tests/__CiRedProof.cs`) and PR **#7** — both used for the AC #5 merge-blocking
measurement, then closed and deleted.

**Live repository state changed (not a file):** ruleset **`20567252`** on `IntegerMan/SpecScribe`.

### Change Log

| date | change |
|---|---|
| 2026-08-07 | Task 1 — `gh` found already installed and authenticated with `admin: true`; no owner handoff needed. |
| 2026-08-07 | Task 2 — AC #3 verified closed by `0b1f561`; `npm ci` proven on npm 11.16.0 locally and on the CI-pinned 11.6.2 in run #78. |
| 2026-08-07 | Task 3 — all 27 failures + 1 cancelled classified from job logs; run #75 added to the story's sweep. |
| 2026-08-07 | Task 4 — root-caused and fixed the residual `Test` flake (`FileShare.Delete` in `SiteRegion.ReadShared`); added the `Diagnose` helper; raised finding **F1** to Epic 6. |
| 2026-08-07 | Task 5 — `main` green across runs #78/#79; regression floor recorded at 2978 P / 0 F / 3 S with `check:parity` 24/24. |
| 2026-08-07 | Task 7 — `docs/CiGate.md` written; `docs/SonarCloudSetup.md` cross-referenced. |
| 2026-08-07 | Task 8 — 16.4 handoff, NFR9 scope and findings F1–F3 recorded. |
| 2026-08-07 | Task 6 initially **blocked** — ruleset `POST` refused by the session permission layer; story held at `in-progress` rather than marked `review`. |
| 2026-08-07 | Task 6 **completed** — owner applied ruleset **`20567252`**. Bypass confirmed via `current_user_can_bypass: "always"` (`actor_id: 5` **is** the admin role); AC #5 proven both directions (PR #7 `UNSTABLE` → `BLOCKED`, `protected` `false` → `true`, required context `build-test-analyze` only). Live object exported to `.github/rulesets/main-required-checks.json`; `docs/CiGate.md` id placeholders replaced and a correction added that `rules/branches/main` is **not** a bypass check. PR #7 closed, `ci/ruleset-block-proof` deleted. **Status → `review`.** |
