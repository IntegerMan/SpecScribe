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

Status: ready-for-dev

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

- [ ] **Task 1 — Get authenticated access (AC: #4, #5). Do this first; everything depends on it.**
  - [ ] `winget install GitHub.cli` (R5 decision 3). Restart the shell so `gh` lands on `PATH`.
  - [ ] **⛔ STOP and hand off:** `gh auth login` is interactive and the dev agent must not attempt it. Ask
        the owner to run `! gh auth login` in-session. Scopes: `repo`, `read:org`, `workflow`, plus
        **repository admin** (ruleset writes need `administration: write`).
  - [ ] Verify: `gh auth status`, and `gh api /repos/IntegerMan/SpecScribe --jq .permissions.admin` → `true`.

- [ ] **Task 2 — Fix `npm ci` (AC: #3)**
  - [ ] Reproduce first, in `web/`: `SPECSCRIBE_PACKAGE_BUILD=1 npm ci --dry-run --ignore-scripts`.
        Expect `Missing: @emnapi/runtime@1.11.3 from lock file`. **If it does not reproduce, stop and say
        so** — the registry may have moved and R4's analysis needs re-deriving.
  - [ ] Repair: `npm install --package-lock-only --ignore-scripts --no-audit --no-fund` in `web/`. This
        writes **only** the lockfile — no `node_modules`, no postinstall, no `nuxt prepare`, so it cannot
        trip the manifest-loading cycle documented at `build-test-analyze.yml:220-237`.
  - [ ] **Read the diff before committing.** R4 measured 69 diff lines: one added package
        (`@emnapi/runtime@1.11.3`), an added root `engines` block, and `"peer": true` recomputation. **Any
        version bump or `resolved`-URL change is out of scope** — report it, do not commit it.
  - [ ] Verify under the **CI-pinned** toolchain, not only this one: `npm ci` must pass on Node 24.11.1 /
        npm 11.6.2 as well as 24.18.1 / 11.16.0. Use `nvm`/`fnm`, or prove it with a CI run on a branch.
  - [ ] Confirm the three consumers still work: `build-test-analyze.yml:246`, `:416`, and
        `publish-docs-live-pages.yml:89`.

- [ ] **Task 3 — Classify the whole failure history (AC: #4)**
  - [ ] Enumerate every failed run and its failing step. R2's table is that sweep's output — reproduce it
        rather than re-deriving the method: `/actions/workflows/build-test-analyze.yml/runs?per_page=100`,
        then per failure `/actions/runs/{id}/jobs`, filtering steps where `conclusion == "failure"`.
  - [ ] With `gh` authenticated, pull each failed **gating** job's log:
        `gh api /repos/IntegerMan/SpecScribe/actions/jobs/{job_id}/logs`.
  - [ ] Produce a table: run · sha · date · failing step · **failing test or gate** · classification
        (**genuine-regression-since-fixed** / **environmental** / **live-flake**) · evidence.
  - [ ] Classify the `Check web drift gates` runs against R7's three shapes. **Regenerate no baseline.** If
        one looks structural, raise it as a finding and route it.
  - [ ] State which classification the current red tip (**run #74, `838d591`, step `Test`**) falls into.

- [ ] **Task 4 — Fix the `Test` flake at its root cause (AC: #4, #6)**
  - [ ] Add `JsonException` to the swallowed set in `FileWatcherServiceTests.Evaluate`
        (`FileWatcherServiceTests.cs:149-154`). A torn read of a file the generator is mid-write is a
        **normal transient state** for this poll — exactly like the `IOException` and
        `UnauthorizedAccessException` already there — and must mean "not yet", not "fail".
  - [ ] Extend that doc comment with **why** the case exists: `SiteRegion.ReadShared`'s `FileShare.ReadWrite`
        (`SiteRegion.cs:279`) deliberately stopped the reader contending for the handle, and the price is
        that a mid-write read now *succeeds* with partial content rather than throwing `IOException`. Future
        readers need the causal chain, not just the extra catch.
  - [ ] **Do NOT widen `SettleTimeout`** (already 20 s) and **do NOT add sleeps**. The class's header comment
        says a fixed sleep is what makes this class flaky; honour it.
  - [ ] Decide **which layer owns the guard** — `SiteRegion.Read`/`Exists`/`Routes`/`RoutesUnder` all parse
        shared-handle content, so every polling caller inherits the same race. Fix it in one layer and
        record which; do not fix it in both.
  - [ ] Prove it: run `FileWatcherServiceTests` **under load** (concurrent `dotnet build` + Node build — the
        condition 16.1 recorded) for **at least 20 consecutive iterations** with zero failures. One green run
        proves nothing about a race.
  - [ ] Fix any additional root cause Task 3 surfaced. **Retry steps and quarantine are rejected** (R5 #2).

- [ ] **Task 5 — Earn the green baseline (AC: #4)**
  - [ ] Push the fixes and confirm `build-test-analyze` is green on `main` for **two consecutive runs**.
        `origin/main` is two commits behind local HEAD, so pushing will itself trigger a run.
  - [ ] ⚠️ **Rebuild non-incrementally before trusting anything asset-related** (CLAUDE.md): measure the
        local floor after `dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental`.
  - [ ] Record the regression floor: `dotnet test SpecScribe.slnx` pass/fail/skip counts, and all four `web/`
        gates including `check:parity`. **Compare against 16.1's floor: 2962 passed / 1 failed / 3 skipped**,
        where that single failure is the flake this story fixes.

- [ ] **Task 6 — Apply the ruleset (AC: #1, #2, #5)**
  - [ ] **Only after Task 5 is green.** Earlier, and you have blocked the owner's own workflow on a flaky
        check.
  - [ ] Create it. **Recommended path: create once in the GitHub web UI with the bypass set, then export the
        live JSON via `gh api`** — see § The ruleset for the trap that makes hand-authoring unsafe.
  - [ ] Shape: `target: "branch"`, `enforcement: "active"`,
        `conditions.ref_name.include: ["~DEFAULT_BRANCH"]`, one `required_status_checks` rule whose only
        context is **`build-test-analyze`**, and a `bypass_actors` entry for the repository **admin** role
        with `bypass_mode: "always"`.
  - [ ] `strict_required_status_checks_policy`: **`false`**. `true` forces every PR to be rebased onto the
        tip before merge, which on a repo where `main` also moves by direct push means near-constant
        re-runs. Record the choice either way.
  - [ ] Export and commit:
        `gh api /repos/IntegerMan/SpecScribe/rulesets/{id} > .github/rulesets/main-required-checks.json`.
  - [ ] **Verify empirically, in both directions** — this is AC #5 and it is not optional:
    - [ ] A direct `git push origin main` (e.g. Task 7's docs commit) **still succeeds**. If it is rejected,
          the bypass actor is wrong — fix the actor, do not disable the rule.
    - [ ] A PR whose `build-test-analyze` is red **cannot be merged**. Prove it on a throwaway branch with a
          deliberately failing test, then delete the branch.
    - [ ] `curl -s …/branches/main` → `"protected": true`, and the required contexts contain
          `build-test-analyze` and **not** `portability-probe (ubuntu, non-gating)`.

- [ ] **Task 7 — Document it (AC: #2, #5)**
  - [ ] Write `docs/CiGate.md`: which check is required and its exact context string; why `portability-probe`
        is deliberately not required (R8); why the admin bypass exists and what it means for the owner's
        direct-push workflow; how to re-apply the ruleset from the committed JSON; and a pointer to ADR 0040
        §9 for how a release tag inherits this gate.
  - [ ] Cross-reference from `docs/SonarCloudSetup.md` where it already describes this workflow, so the two
        cannot drift.
  - [ ] **Do not** add a README CI badge — Story 25.1 open item 4 deliberately left that to follow the
        quality-gate decision, which is 25.2 / ADR 0035's, not this story's.

- [ ] **Task 8 — Record the handoff (AC: #4)**
  - [ ] In Dev Notes, for **16.4**: "the tagged commit is already green on `main`" (ADR 0040 §9) is now a
        condition it can actually query — give it the exact API shape to query it with.
  - [ ] State plainly which of ADR 0040 §7's NFR9 gaps this story closed (**`npm ci` only**) and which
        remain, with whom.
  - [ ] Record any finding raised-but-not-fixed with its route, in the style 16.1 used.

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

### Debug Log References

### Completion Notes List

### File List
