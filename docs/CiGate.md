# The CI Gate — what is required on `main`, and why

Story 16.2. This document describes the **required status check** on `main`: which check it is, what its
context string is exactly, why one job is deliberately *not* required, why the repository admin can bypass it,
and how to re-apply the configuration if it is ever lost.

Related: [`SonarCloudSetup.md`](SonarCloudSetup.md) describes the *analysis* half of the same workflow.
[ADR 0040 §9](adrs/0040-release-channels-and-versioning-policy.md) describes how a release tag inherits this gate.

---

## The short answer

| | |
|---|---|
| **Workflow file** | [`.github/workflows/build-test-analyze.yml`](../.github/workflows/build-test-analyze.yml) |
| **Workflow `name:`** | `Build, Test & Analyze` |
| **Required check context** | **`build-test-analyze`** |
| **Deliberately NOT required** | `portability-probe (ubuntu, non-gating)` |
| **Enforced by** | a repository **ruleset** on the default branch, not classic branch protection |
| **Committed record** | [`.github/rulesets/main-required-checks.json`](../.github/rulesets/main-required-checks.json) |
| **Bypass** | the repository **admin** role, `bypass_mode: always` |

## The context string is the JOB name, not the workflow name

A required status check is matched against the **check run** name, which GitHub takes from `jobs.<id>.name`.
For this workflow that is:

```yaml
jobs:
  build-test-analyze:
    name: build-test-analyze     # <- THIS is the required context
```

So the required context is `build-test-analyze` — **not** `Build, Test & Analyze`, which is the workflow's
`name:` and matches nothing. Getting this wrong produces a rule that is permanently pending (a check that never
reports) and blocks every pull request forever, which looks exactly like a broken gate.

### ⚠️ Matching is by NAME ONLY — an accepted risk, not an oversight

**[code review 2026-08-08, owner decision]** The rule carries no `integration_id`, so GitHub matches the context
purely on the string `build-test-analyze`. **Any** GitHub App or user holding `checks: write` on this repository
could `POST /check-runs` a green `build-test-analyze` and satisfy the gate without the workflow ever running.

Pinning `integration_id` to the GitHub Actions app would close that. **The owner has reviewed this and accepted
the risk**: this is a single-maintainer public repository whose only installed app with `checks: write` is
SonarCloud's, and the bypass actor already means the owner is trusted on `main` by design — so an attacker able
to forge a check run would already need write access that defeats the gate anyway.

**Revisit this if any of those change** — a second maintainer, a new GitHub App with `checks: write`, or an
outside contributor with elevated permissions. The fix is one field on the rule:

```jsonc
{ "context": "build-test-analyze", "integration_id": <the GitHub Actions app id> }
```

### If the check is never reported at all

A permanently-pending required context blocks every PR and has **four** reachable causes, not just a mistyped
string. All four look identical from the PR page, so diagnose before "fixing" the rule:

1. the context string does not match `jobs.<id>.name` (above);
2. the workflow file was renamed or deleted on the PR's branch, so the job never ran;
3. the run was **cancelled** — `concurrency.cancel-in-progress` is on for pull requests, so a superseded push
   leaves a cancelled run whose check is not a success;
4. a fork PR skipped the job (secrets are unavailable to forks).

Diagnose with `gh api repos/IntegerMan/SpecScribe/commits/<sha>/check-runs --jq '.check_runs[].name'` — if
`build-test-analyze` is absent from that list, the check was never reported and no amount of rule editing will
help. **The resolution is the admin bypass, or pushing a commit that triggers a real run — never weakening or
deleting the rule.**

## Why `portability-probe` must never be required

`portability-probe (ubuntu, non-gating)` carries `continue-on-error: true` **at the job level**:

```yaml
  portability-probe:
    name: portability-probe (ubuntu, non-gating)
    runs-on: ubuntu-latest
    continue-on-error: true
```

That flag makes the job's **conclusion `success` even when a step inside it failed**. This is not theoretical —
it is observed twice in this repository's own history:

| run | what actually failed inside the job | job conclusion |
|---|---|---|
| #63 | step `Test` | `success` |
| #75 | step `Check parity on Linux (ADR 0033 §4 cross-OS proof)` | `success` |

Requiring it would therefore be **worse than useless**: a required check that is structurally incapable of
reporting failure adds ceremony and zero safety. The probe is informational by design — it measures Linux
divergence so the project learns about it, and `build-test-analyze` is the thing that actually gates.

## Why the admin can bypass, and why that is not a hole

This repository's owner ships by **merging locally and pushing straight to `main`**. A required status check
binds pushes to the target branch as well as merges into it, so without an exemption the rule would block the
owner's own normal workflow on every commit.

The ruleset therefore names the repository **admin role** as a `bypass_actors` entry with
`bypass_mode: "always"`. The important properties:

- It is a **named, deliberate exemption for one principal**, not a disabled rule. Pull requests from anyone
  else — and any future contributor — are gated normally.
- **Coverage still includes pushes.** "Covering pull requests and pushes" is satisfied *with* the bypass, not
  despite it. Read the bypass as a scoped exception, not as the rule failing to apply.
- If a direct push to `main` is ever **rejected**, that is the bypass actor being misconfigured — a bug in this
  configuration. Fix the actor; do **not** disable the rule.

> ⚠️ **If `bypass_actors` is ever lost, you cannot push the repair. [code review 2026-08-08]** With no bypass,
> the required check binds pushes too, and a brand-new commit can never already carry a passing
> `build-test-analyze` — so the fix cannot travel through `git push`. **The repair is API- or UI-only:** use the
> `PUT` in § Re-applying it (or the ruleset's Settings page) to restore the `bypass_actors` entry. This is worth
> knowing in advance, because from the terminal it presents as being locked out of your own default branch, and
> the tempting move — deleting the rule — throws the gate away to fix a one-field mistake.

## ⚠️ Renaming the default branch splits the ruleset from the workflow

**[code review 2026-08-08]** These two do not move together, and nothing warns you:

- the **ruleset** targets `~DEFAULT_BRANCH`, so it re-points at the new branch automatically;
- the **workflow** is pinned to `branches: ["main"]` (`push` and `pull_request` alike), so it stops running there.

The result is the worst combination: the rule still applies, but the check it requires is never reported, so
**every pull request blocks permanently** with the "never reported" signature above. Every verification command
in this document also hardcodes `main` and will report `protected: false` on what is now an ordinary branch.

No change is needed today — the default branch is `main` and there are no plans to rename it. But **a rename
requires editing `.github/workflows/build-test-analyze.yml`'s triggers in the same change**, and this note exists
so that is discovered before the rename rather than after every PR is stuck.

## `strict_required_status_checks_policy` is `false`, on purpose

`true` ("require branches to be up to date before merging") forces every pull request to be rebased onto the
tip before it can merge. On a repository where `main` **also** moves by direct push, that means a PR is
invalidated every time the owner pushes, producing near-constant re-runs for no safety gain — the check still
has to pass on the merge result either way. It is set to `false` deliberately.

## `.github/rulesets/` is a record, not configuration

**GitHub does not read `.github/rulesets/`.** Rulesets live in repository settings and there is no in-repo
mechanism that makes a committed file take effect. Editing
[`main-required-checks.json`](../.github/rulesets/main-required-checks.json) changes **nothing** on its own.

The file exists for two reasons:

1. **Reviewability.** The applied configuration is otherwise invisible in the repository — you would have to
   open repository settings to see what the gate actually is. Committing the exported JSON makes it
   diff-reviewable like everything else.
2. **Re-application.** It is the source to restore from if the ruleset is deleted or damaged.

It was produced by **exporting the live API object**, not by hand-authoring. That direction matters: the admin
role's numeric `actor_id` is not documented in the REST reference, and values circulating publicly disagree
with one another. Committing what the platform actually returned means no literal in this repository was ever
guessed.

### Re-applying it

The live ruleset is **id `20567252`**, created 2026-08-07.

> ⚠️ **Two prerequisites, both of which have bitten. [code review 2026-08-08]**
>
> 1. **`jq` is NOT installed on the owner's machine** — measured absent from both PowerShell and Git Bash. The
>    commands below therefore use `node`, which this repository already requires, instead of `jq`.
> 2. **Run this block in Git Bash, not PowerShell.** PowerShell resolves `/tmp/…` to `C:\tmp\…`, which does not
>    exist; it does not treat a trailing `\` as a line continuation; and it strips the embedded double quotes out
>    of a `--jq` expression, so `jq` sees `select(.type==required_status_checks)` and fails with
>    `function not defined`. That last one is the same trap this story's own Dev Agent Record recorded as F3.
>    PowerShell equivalents are given underneath each block.

```sh
# --- Git Bash ---
# Inspect what is live today
gh api repos/IntegerMan/SpecScribe/rulesets
gh api "repos/IntegerMan/SpecScribe/rulesets/$(gh api repos/IntegerMan/SpecScribe/rulesets \
  --jq '.[] | select(.name=="main: require build-test-analyze") | .id')"

# Recreate from the committed record (strip the server-assigned fields first)
node -e 'const o=require("./.github/rulesets/main-required-checks.json"); \
  for (const k of ["id","node_id","created_at","updated_at","_links","source","source_type","current_user_can_bypass"]) delete o[k]; \
  process.stdout.write(JSON.stringify(o,null,2))' > "$TEMP/ruleset.json"
gh api --method POST repos/IntegerMan/SpecScribe/rulesets --input "$TEMP/ruleset.json"

# Update in place instead of recreating (resolve the id by NAME — see the warning below)
gh api --method PUT "repos/IntegerMan/SpecScribe/rulesets/$RULESET_ID" --input "$TEMP/ruleset.json"
```

```powershell
# --- PowerShell equivalent ---
$id = gh api repos/IntegerMan/SpecScribe/rulesets --jq '.[] | select(.name==\"main: require build-test-analyze\") | .id'
gh api "repos/IntegerMan/SpecScribe/rulesets/$id"
gh api --method PUT "repos/IntegerMan/SpecScribe/rulesets/$id" --input "$env:TEMP\ruleset.json"
```

⚠️ **Resolve the id by NAME, not by the literal below. [code review 2026-08-08]** The id `20567252` is written
into this document in several places and into the committed JSON's `"id"` field. **Deleting and recreating the
ruleset mints a NEW id**, at which point every one of those literals silently points at a ruleset that no longer
exists — `gh api …/rulesets/20567252` simply 404s and exits non-zero, which reads like a broken command rather
than a stale id. After any recreate, the committed JSON's `id`, `node_id`, `created_at` and `updated_at` are all
stale and should be re-exported. The name is the stable key; the id is not.

`gh ruleset` is **read-only** (`list` / `view` / `check`), so `gh api` is the write path. Recreating with the
same `name` returns **`422 name must be unique`** — that error means the rule already exists, not that the
payload is malformed.

**`actor_id: 5` is the built-in `admin` repository role.** That id is not in the REST reference and public
sources disagree about it, so it was **confirmed against the platform** rather than trusted: the applied
ruleset reports `"current_user_can_bypass": "always"` for the repository admin. The committed JSON is the
exported live object, so the id in it is observed, not asserted.

### Verifying it

```sh
# --- Git Bash ---
# THE authoritative bypass check — does the CALLING user bypass this rule?
gh api repos/IntegerMan/SpecScribe/rulesets/20567252 --jq '.current_user_can_bypass'   # -> "always"

# Is the branch gated at all, and by exactly which contexts?
gh api repos/IntegerMan/SpecScribe/branches/main --jq '.protected'                     # -> true
gh api repos/IntegerMan/SpecScribe/rules/branches/main \
  --jq '.[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context'
```

```powershell
# --- PowerShell: the embedded quotes MUST be escaped as \" or PowerShell strips them and jq reports
# `function not defined: required_status_checks/0`. [code review 2026-08-08] ---
gh api repos/IntegerMan/SpecScribe/rulesets/20567252 --jq '.current_user_can_bypass'
gh api repos/IntegerMan/SpecScribe/branches/main --jq '.protected'
gh api repos/IntegerMan/SpecScribe/rules/branches/main --jq '.[] | select(.type==\"required_status_checks\") | .parameters.required_status_checks[].context'
```

⚠️ **Do not read `rules/branches/main` as a bypass check.** It lists the rules that apply to the **branch**,
and it returns them **whether or not the calling user can bypass them** — an admin who bypasses still sees
the rule listed. Only `current_user_can_bypass` on the ruleset answers "does this bind *me*". Misreading that
endpoint looks exactly like a broken bypass and will send you chasing a non-existent misconfiguration.

### What "gated" actually looks like, measured

Both directions were verified empirically on 2026-08-07 against PR **#7**, a throwaway branch carrying a
deliberately failing test so `build-test-analyze` went red:

| | before the ruleset | after |
|---|---|---|
| PR with a red gate | `mergeable: MERGEABLE`, `mergeStateStatus: **UNSTABLE**` — merge allowed | `mergeStateStatus: **BLOCKED**` |
| `branches/main.protected` | `false` | `true` |
| required contexts | *(none)* | `build-test-analyze` only |
| admin direct push | unaffected | unaffected (`current_user_can_bypass: always`) |

## It does not disturb the Pages workflow

[`publish-docs-live-pages.yml`](../.github/workflows/publish-docs-live-pages.yml) is independent of the gate by
construction, and a branch ruleset touches neither workflow:

- **Different concurrency group.** Pages owns `pages`; the gate uses `build-test-analyze-${{ github.ref }}`
  and explicitly must not share, or the two would cancel one another.
- **Different trigger shape.** Pages filters on `paths:`; the gate deliberately has **no** `paths:` filter,
  because a build+test gate that skips on some paths is not a gate.
- **Different permissions.** Pages declares permissions per job; the gate declares `contents: read` at
  workflow level.

The one interaction worth stating plainly: **Pages deploys, it does not write to `main`.** A branch ruleset
constrains writes to a ref, so it cannot block a deployment.

## How a release tag inherits this gate

Per [ADR 0040 §9](adrs/0040-release-channels-and-versioning-policy.md), the release pipeline does **not**
re-run build+test inside the release job. It requires that **the tagged commit is already green on `main`**.
This document's job is to make "green on `main`" mean something.

**Implemented by Story 16.4** in [`.github/workflows/release.yml`](../.github/workflows/release.yml)'s
`preflight` job, which runs before anything is built and before any credential exchange. The decision logic is
[`.github/scripts/release/gate-verdict.mjs`](../.github/scripts/release/gate-verdict.mjs) and the polling
wrapper is `require-green-gate.sh`; both are driven red and green from fixtures by `selftest.mjs`, which runs
as the first job of every release. See [docs/Releasing.md](Releasing.md) § How publishing is gated on build +
test for the failure branches (no run found, in progress, red re-run) and what the operator sees for each.

> ⚠️ **The shipped implementation uses the CHECK-RUNS API, not the workflow-runs query below.** Both answer the
> question; the check-runs form answers it in one call and cannot express the job-vs-run trap at all, because
> check runs are already per-job. The query below is kept because it is the shape to reach for at a terminal,
> and because it documents the trap that motivated the choice.

The query shape is:

> 🚨 **This query has TWO ways of reporting a FALSE GREEN. Both are fixed below; do not copy an older form of
> it. [code review 2026-08-08]**
>
> 1. **An empty `head_sha` returns EVERY run of the workflow.** `SHA=$(…)` is a POSIX assignment. Run this block
>    in PowerShell and it is a parse error; run only the second line and `$SHA` interpolates to the empty string,
>    which GitHub *ignores* rather than rejects — so the query happily lists every run the workflow has ever had
>    and an operator can read a green conclusion off a completely unrelated commit. **Assert the SHA is non-empty
>    before using it.**
> 2. **`.object.sha` on an ANNOTATED tag is the tag object's SHA, not the commit's.** Annotated tags
>    (`git tag -a`) point at a tag object which in turn points at the commit, so `head_sha=<tag-object-sha>`
>    matches no run at all and the empty result reads as "no green run for this tag" — a false RED that will look
>    like the gate failing. Dereference when `.object.type == "tag"`.

```sh
# Latest build-test-analyze conclusion for the exact commit a tag points at.
# Git Bash. Handles both lightweight and annotated tags, and refuses to query on an empty SHA.
REF=$(gh api "repos/IntegerMan/SpecScribe/git/refs/tags/<tag>" --jq '.object.sha + " " + .object.type')
SHA=${REF% *}; TYPE=${REF#* }
# An annotated tag points at a tag OBJECT — dereference it to reach the commit.
if [ "$TYPE" = "tag" ]; then
  SHA=$(gh api "repos/IntegerMan/SpecScribe/git/tags/$SHA" --jq '.object.sha')
fi
[ -n "$SHA" ] || { echo "refusing to query on an empty SHA — the tag did not resolve" >&2; exit 1; }

gh api "repos/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml/runs?head_sha=$SHA" \
  --jq '.workflow_runs[] | [.run_number, .conclusion] | @tsv'
```

Note that a **run-level** conclusion is not sufficient on its own: `portability-probe`'s job-level
`continue-on-error` means the run can report `success` while that job is red. Query the **`build-test-analyze`
job's** conclusion, not the run's, exactly as this document's required-context rule does:

```sh
gh api repos/IntegerMan/SpecScribe/actions/runs/<run_id>/jobs \
  --jq '.jobs[] | select(.name == "build-test-analyze") | .conclusion'
```

## Deliberately absent: a README CI badge

Story 25.1 left the badge decision to follow the SonarCloud quality-gate posture (Story 25.2 / ADR 0035), not
this story. `SonarCloudSetup.md` § Badges holds that decision. No badge is added here.
