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

```sh
# Inspect what is live today
gh api repos/IntegerMan/SpecScribe/rulesets
gh api repos/IntegerMan/SpecScribe/rulesets/<id>

# Recreate from the committed record (strip the server-assigned fields first)
jq 'del(.id, .node_id, .created_at, .updated_at, ._links, .source, .source_type, .current_user_can_bypass)' \
  .github/rulesets/main-required-checks.json > /tmp/ruleset.json
gh api --method POST repos/IntegerMan/SpecScribe/rulesets --input /tmp/ruleset.json

# Update in place instead of recreating
gh api --method PUT repos/IntegerMan/SpecScribe/rulesets/<id> --input /tmp/ruleset.json
```

`gh ruleset` is **read-only** (`list` / `view` / `check`), so `gh api` is the write path.

### Verifying it

```sh
# Does the rule apply to ME? Empty output means the caller bypasses it.
gh api repos/IntegerMan/SpecScribe/rules/branches/main

# Classic view of the same state
gh api repos/IntegerMan/SpecScribe/branches/main --jq '.protected'
```

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
That is Story 16.4's to implement; this document's job is to make "green on `main`" mean something. The query
shape is:

```sh
# Latest build-test-analyze conclusion for the exact commit a tag points at
SHA=$(gh api repos/IntegerMan/SpecScribe/git/refs/tags/<tag> --jq '.object.sha')
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
