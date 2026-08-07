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

Status: in-progress

> ⚠️ **Deliberately NOT `review`.** 7 of 8 tasks are complete; **Task 6 is blocked on the owner** —
> `POST /repos/…/rulesets` was refused by the session's permission layer, so AC #1, #2 and #5 remain open and
> `main` is still `protected: false` / `rulesets: []`. See § Dev Agent Record → Completion Notes → *What the
> owner needs to do*. Marking this `review` would claim a gate that is not yet required.

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

- [ ] 🚫 **Task 6 — Apply the ruleset (AC: #1, #2, #5) — BLOCKED. Needs the owner. Nothing else remains.**
  - [x] Task 5's precondition **is** satisfied — runs **#78 and #79** are both green on `main` at `07bdb79`,
        so the ordering constraint ("flakes first, prove green, then apply the rule") is honoured and the
        apply is unblocked *on the story's own terms*.
  - [ ] **⛔ `POST /repos/IntegerMan/SpecScribe/rulesets` was refused by the session's permission layer**
        ("Blocked by classifier"), twice. This is a **write to live repository settings** on a public repo,
        and I did not attempt to route around it. The token itself is sufficient (`permissions.admin: true`);
        the block is environmental, not a credential problem.
  - [x] Payload prepared and reviewed — **but deliberately NOT committed to `.github/rulesets/`**, see below.
  - [x] `strict_required_status_checks_policy`: **`false`**, with the reasoning recorded in `docs/CiGate.md`
        (on a repo where `main` also moves by direct push, `true` invalidates every PR on each push for no
        added safety, since the check must pass on the merge result regardless).
  - [ ] Export and commit `.github/rulesets/main-required-checks.json` — **cannot be done**: AC #5 requires
        the JSON to be *exported from the live API*, and there is no live ruleset to export.
  - [ ] **Empirical verification, both directions:**
    - [x] **"Before" half captured, and it is the proof the gate is currently absent.** PR **#7** carries a
          **red** `build-test-analyze` (run #80, step `Test`) and GitHub reports
          `mergeable: MERGEABLE`, `mergeStateStatus: UNSTABLE` — i.e. **a red required check does not block a
          merge today**. Re-query the same PR after applying the ruleset; it must become `BLOCKED`.
    - [ ] Direct `git push origin main` still succeeds — **not attempted.** This session is forbidden from
          pushing to `main`, and the bypass cannot be exercised without a live ruleset anyway. It is already
          § Owner actions item 2.
    - [ ] `branches/main` → `"protected": true` with the right contexts — pending the apply. **Measured now:
          `protected: false`, `rulesets: []`** (unchanged from R1).

  **The payload, and why it is not in `.github/rulesets/` yet.** The story's § "The ruleset — shape, and the
  one trap" is explicit that the committed JSON must be *what the platform returned*, so that no literal is
  asserted that was never observed. `<ADMIN_ROLE_ID>` is exactly such a literal — it is **not** in the REST
  reference. Committing this hand-authored candidate to the AC #5 path would assert an **unverified** id and
  read to a reviewer as the applied configuration. So it is recorded here instead, and
  `.github/rulesets/` is deliberately **not created**:

  ```jsonc
  {
    "name": "main: require build-test-analyze",
    "target": "branch",
    "enforcement": "active",
    "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
    "bypass_actors": [
      // ⚠️ actor_id 5 is the WIDELY-CIRCULATED value for the built-in admin role and is UNVERIFIED.
      // Do not trust it. Apply, then GET the ruleset back and commit whatever the platform returned.
      { "actor_type": "RepositoryRole", "actor_id": 5, "bypass_mode": "always" }
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

  **Verify the bypass immediately after applying** — this is the cheap check that avoids blocking the owner's
  own pushes, and it needs no push to `main`:

  ```sh
  gh api repos/IntegerMan/SpecScribe/rules/branches/main   # EMPTY for an admin ⇒ the bypass works
  gh pr view 7 --repo IntegerMan/SpecScribe --json mergeable,mergeStateStatus  # must become BLOCKED
  ```

  **Cleanup owed once the proof is recorded:** close PR **#7** and delete branch `ci/ruleset-block-proof`
  (commit `1409002`, adds only `tests/SpecScribe.Tests/__CiRedProof.cs`). It is left open **on purpose** —
  it is the AC #5 "after" measurement and recreating it costs another full CI run.

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

**Status: 7 of 8 tasks complete. The story is NOT finished — Task 6 is blocked and needs the owner.**

1. **The gate is now trustworthy, but it is not yet REQUIRED.** That is the whole of what is outstanding.
   AC #1, #2 and #5 all depend on a ruleset that this session was **not permitted to create**: the
   `POST /repos/IntegerMan/SpecScribe/rulesets` call was refused by the session's permission layer, twice,
   as a write to live repository settings. The token is sufficient (`permissions.admin: true`); the block is
   environmental. **I did not attempt to route around it.**

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

9. **Two artefacts are deliberately left live and owe cleanup** (both listed under § Owner actions):
   PR **#7** / branch `ci/ruleset-block-proof` (the AC #5 "after" measurement), and the `<id>` placeholder in
   `docs/CiGate.md`.

#### 👤 What the owner needs to do — the story cannot close without it

1. **Apply the ruleset.** Payload and the unverified-`actor_id` caveat are in Task 6. Either run the
   `POST` yourself, or create it once in the GitHub web UI (the story's recommended path, which sidesteps the
   `actor_id` guess entirely).
2. **Immediately verify the bypass** — `gh api repos/IntegerMan/SpecScribe/rules/branches/main` must be
   **empty** for you. If it is not, the bypass actor is wrong: **fix the actor, do not disable the rule.**
3. **Re-query PR #7** — `mergeStateStatus` must flip `UNSTABLE` → `BLOCKED`. That completes AC #5's
   empirical half, whose "before" measurement is already recorded.
4. **`GET` the ruleset back and commit it** to `.github/rulesets/main-required-checks.json`, then replace the
   `<id>` placeholders in `docs/CiGate.md`.
5. **Close PR #7 and delete `ci/ruleset-block-proof`.**
6. **Merge `worktree-story-16-2-dev`** — it carries the flake fix and the docs.

### File List

| file | change |
|---|---|
| `tests/SpecScribe.Tests/SiteRegion.cs` | **modified** — `ReadShared` now opens `FileShare.ReadWrite \| FileShare.Delete`; doc comment extended with the causal chain and the Windows correction |
| `tests/SpecScribe.Tests/FileWatcherServiceTests.cs` | **modified** — added the failure-path-only `Diagnose` helper; the three `SprintStatusYaml…` waits now report state on timeout via `if (!WaitFor(…)) Assert.Fail(…)` |
| `docs/CiGate.md` | **NEW** — the required check, the context string, why `portability-probe` is not required, the admin bypass, re-apply/verify commands, Pages independence, ADR 0040 §9 tag inheritance |
| `docs/SonarCloudSetup.md` | **modified** — cross-reference scoping it to the *analysis* half and pointing at `CiGate.md` for the *gating* half |
| `_bmad-output/implementation-artifacts/16-2-continuous-integration-build-and-test-gate.md` | **modified** — this record |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | **modified** — status → `in-progress` |
| ~~`.github/rulesets/main-required-checks.json`~~ | **NOT created** — AC #5 requires the *exported live* object and there is no live ruleset to export. Deliberate; see Task 6. |
| ~~`web/package-lock.json`~~ | **NOT modified** — already repaired on `main` by `0b1f561`; verified, not redone. |

**Also created outside the working tree** (owed cleanup): branch `ci/ruleset-block-proof` (commit `1409002`,
adds only `tests/SpecScribe.Tests/__CiRedProof.cs`) and PR **#7**.

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
| 2026-08-07 | **Task 6 BLOCKED** — ruleset `POST` refused by the session permission layer; story held at `in-progress` rather than marked `review`. |
