# Release Setup and Workflow — the owner's guide

What you have to configure on **nuget.org**, **npm** and **GitHub** before SpecScribe can publish, and what
your day-to-day workflow looks like once it can.

**Authority:** [ADR 0040](adrs/0040-release-channels-and-versioning-policy.md) (**Accepted** 2026-08-08)
decides the channels, the packaging shape, the credential posture, the versioning scheme, the CI gate and the
atomicity policy. This document records *how to set it up and how to drive it*. Where the two disagree, the
ADR wins and this file is the bug.

**Companions:** [`CiGate.md`](CiGate.md) — the required status check and its ruleset ·
[`Packaging.md`](Packaging.md) — producing and verifying a package locally.

---

## 1. State of play — read this first

Epic 16 is **partly built**, and one piece that looks built is aimed at a design the owner has since replaced.

| | status |
|---|---|
| ADR 0040 (the policy) | ✅ **Accepted** 2026-08-08 |
| Versioning (MinVer, `v` tag prefix, `0.1` floor, `preview.0` default) | ✅ merged — `src/SpecScribe/SpecScribe.csproj` |
| Renderer packed inside the nupkg + `AssertRendererPacked` guard | ✅ merged (Story 16.3) |
| CI gate + `main` ruleset (`build-test-analyze` required) | ✅ live — ruleset id `20567252` |
| **Stage A** — auto-tag + prerelease GitHub Release on every merge to `main` | ❌ **not built** |
| **Stage B** — manual promote to nuget.org / npm | ⚠️ **built against the superseded design** — see below |
| `CHANGELOG.md` + `changelog.d/` assembler | ❌ not built (Story 16.6) |
| npm / npx channel | ❌ not built (Story 16.8) |
| VSIX / Marketplace | ❌ out of the first preview (§ Decision 4) |
| Git tags in the repository | **zero** — nothing has ever been released |

### ⚠️ The Story 16.4 worktree implements the design that was replaced

`.claude/worktrees/story-16-4-dev` (branch `worktree-story-16-4-dev`, commit `e0ea4b2`) carries a complete,
well-built release pipeline — `.github/workflows/release.yml`, seven guard scripts, a `selftest.mjs`, and a
`docs/Releasing.md`. It is **tag-triggered**: `on: push: tags: ["v*"]`, with a check-runs API lookup to prove
the tagged commit was green.

That branch was cut from `e8a689d`. Main has since moved to `96d00c3`, which **rewrote § Decision 9 to
merge-triggered releasing** and retired the API lookup entirely. So as it stands:

- its trigger, its staging and its `require-green-gate.sh` implement a superseded rule;
- its `docs/Releasing.md` still says *"ADR 0040 is `Proposed`, not `Accepted`"* and documents a manual
  `git tag && git push` cut that is no longer how a tag comes into existence;
- **most of it is still good.** The RID matrix, `assert-archive-renderer.sh`, `assert-source-date-epoch.sh`,
  `nuget-version-consumed.mjs`, `release-body.mjs`, the space-in-path extraction test and the selftest harness
  are all design-independent and should be carried over rather than rewritten.

**Do not merge that branch as-is.** Story 16.4 needs a rework pass to split it into Stage A (a job inside
`build-test-analyze.yml`) and Stage B (a `workflow_dispatch` promote workflow), after which its `Releasing.md`
supersedes the operational half of this document.

---

## 2. Decide this before you touch any registry

Two decisions gate the configuration, and both are cheap now and expensive later.

### 2a. Freeze the Stage B workflow **filename**

nuget.org and npm both bind a Trusted Publishing policy to **repo owner + repo + workflow *filename* + optional
environment**. Neither registry can be told "the workflow moved". Renaming the file silently invalidates the
policy, and the failure surfaces as a rejected push at the **last** step of a release — after the version has
already been consumed on any channel that went first (§ Decision 10).

Under merge-triggered releasing the file that runs `NuGet/login@v1` is **Stage B**, not Stage A. Story 16.4's
worktree calls it `release.yml`, but under the new design that name now describes Stage B only. Pick the name
you will live with — `promote.yml` is the honest one — **and create the nuget.org policy against that name**.

### 2b. "Reserving" a package ID is not a button — it is a publish

ADR 0040 § Decision 12 calls name reservation **owner action #1**. Be aware of what that actually means:

- **nuget.org** has no per-ID reservation. *ID prefix reservation* exists but applies to a namespace you own
  (`SpecScribe.*`) and requires review. For the bare ID `SpecScribe`, **first publish wins.**
- **npm** is the same — you claim a name by publishing to it.

The ADR's own asymmetry argument tells you what to do about that:

| | if squatted | recommendation |
|---|---|---|
| `SpecScribe` (nuget) | cheap — `SpecScribe.Cli` still installs a tool invoked as `specscribe` | **accept the race.** Claim it with the first real `preview.1`. |
| `specscribe` (npm) | **not recoverable by rename** — `npx specscribe` would run someone else's package | claim early |
| `specscribe-renderer` (npm) | 🔴 **arbitrary code execution on every consumer's machine** — § Decision 5 pins it exactly and `NuxtPrerender` spawns `node <it>/server/index.mjs` on every `generate` | **claim first, before anything else** |
| `specscribe-win32-x64`, `specscribe-linux-x64`, `specscribe-darwin-arm64` (npm) | broken install — loud, recoverable by rename | claim when convenient |

All five npm names are needed, not just `specscribe`. The spike's verification table covered only three names
(`SpecScribe`, `specscribe`, `specscribe-win32-x64`, all unclaimed on 2026-08-07); **the unchecked remainder
includes `specscribe-renderer`** — the highest-stakes name in the set. Re-check all of them before relying on
any of it:

```sh
# nuget.org — 404 means unclaimed
curl -s -o /dev/null -w '%{http_code}\n' https://api.nuget.org/v3/registration5-semver1/specscribe/index.json

# npm — E404 means unclaimed
for n in specscribe specscribe-renderer specscribe-win32-x64 specscribe-linux-x64 specscribe-darwin-arm64; do
  printf '%-28s %s\n' "$n" "$(npm view "$n" version 2>&1 | head -1)"
done
```

To claim an npm name without spending a releasable version, publish a minimal placeholder at a version the
scheme will never use (e.g. `0.0.0-reserved.0`) and `npm deprecate` it. § Decision 10's "a version is
consumed" rule bites on version *numbers*, and `0.0.0-reserved.0` is outside `0.MINOR.PATCH-preview.N`.

**If any primary ID is already taken: stop and decide, do not substitute.** § Decision 12 makes silent
fallback the failure the rule exists to prevent, and the choice lands as an ADR amendment in the same change
that updates every document naming the old string.

---

## 3. nuget.org configuration

### 3.1 Trusted Publishing (preferred — stores no secret)

1. Sign in to nuget.org → your account → **Trusted Publishing** → **Add**.
2. Fill it in exactly:

   | field | value |
   |---|---|
   | Repository owner | `IntegerMan` |
   | Repository | `SpecScribe` |
   | **Workflow file** | the Stage B filename from § 2a — **frozen** |
   | Environment | **leave empty** — unless you set one, in which case the job *must* declare a matching `environment:` or the exchange fails even though owner, repo and filename all match |

3. Add the repository **variable** `NUGET_USER` = your nuget.org **profile name** (not your email — it is an
   identity, so a variable, not a secret):

   ```sh
   gh variable set NUGET_USER --repo IntegerMan/SpecScribe --body '<your-nuget-profile-name>'
   ```

The pipeline then exchanges the Actions OIDC token for a **one-hour, single-use** push key via
`NuGet/login@v1`, **immediately before the push step and never at job start** — the release job builds three
~76 MiB RIDs ahead of it and the key would expire. A push that fails **consumes the key**; the supported retry
is re-running the job, which re-runs the exchange.

### 3.2 If Trusted Publishing is unavailable on your account

nuget.org's rollout is still gradual, and § Decision 3 leaves this **explicitly open pending your
confirmation** — it is the one condition ADR 0040 flags as unresolved in its own header. Check § 3.1 first;
if the Trusted Publishing page is not offered:

```sh
gh secret set NUGET_API_KEY --repo IntegerMan/SpecScribe --env release
```

- Scope the key on nuget.org to **push only**, to the `SpecScribe` ID (glob it if the package does not exist
  yet), with the **shortest offered expiry**.
- Store it against a repository **environment named `release`**, so it is unreachable from PR workflows.
- Under this path § Decision 3's "stores nothing" headline **weakens for the NuGet channel only**, and that
  caveat should be recorded in the ADR rather than left implicit.

**Confirm which of § 3.1 / § 3.2 applies before Story 16.4's rework begins** — it changes the job's
`permissions` block and whether it declares an `environment:`.

---

## 4. npm configuration (Story 16.8 — not yet needed)

Recorded now so the ordering constraints are not discovered late.

- **Trusted Publishing is configured per-package, and the package must already exist.** So the *first* publish
  of each of the five names cannot use it — it needs a granular access token or a manual `npm publish` from
  your machine. Configure trusted publishing **after** the placeholder publish of § 2b, then the pipeline
  never holds a token.
- The runner needs **npm CLI ≥ 11.5.1 and Node ≥ 22.14.0**, `id-token: write`, and **`NODE_AUTH_TOKEN` must
  not be set** — its presence disables the OIDC path. Provenance attestations are on by default.
- **Publish order is normative: `specscribe-renderer` first, then the wrapper.** npm has no multi-package
  transaction. Wrapper-first makes `specscribe@X.Y.Z` installable while its exact `=X.Y.Z` renderer dependency
  does not exist yet — `npx specscribe` then fails at install for every user, with the version already burned.
  Renderer-first fails harmlessly: an orphaned renderer version.
- The wrapper must distinguish **"this platform has no package"** (`linux-arm64`, `osx-x64` → point at the
  platform-neutral `dotnet tool` channel) from **"optional dependencies were skipped"** (`npm ci
  --omit=optional` → say *reinstall without it*). Telling a supported platform it is unsupported is the
  likelier of the two mistakes, because `--omit=optional` is a common CI-hardening default.

---

## 5. GitHub configuration

### 5.1 Already done — verify, don't recreate

The `main` ruleset (id `20567252`, created 2026-08-07) requires the check **`build-test-analyze`** — the *job*
name, not the workflow's `name:`. Full detail and the re-apply procedure are in [`CiGate.md`](CiGate.md).

```sh
gh api repos/IntegerMan/SpecScribe/rulesets/20567252 \
  --jq '[.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context]'
```

### 5.2 What the two stages need

| | Stage A (job in `build-test-analyze.yml`) | Stage B (promote workflow) |
|---|---|---|
| trigger | `push` to `main`, `needs: build-test-analyze` | `workflow_dispatch` with a tag input |
| `permissions` | `contents: write` **at job level** — the workflow declares `contents: read` and must stay least-privilege | `contents: write` + `id-token: write` |
| checkout | `fetch-depth: 0` | `fetch-depth: 0` |
| creates | the `v0.1.0-preview.N` tag + a **prerelease** GitHub Release with the three RID archives and their SHA-256 digests | registry publications; appends the changelog section to the Release body |

`fetch-depth: 0` is not optional in either place. MinVer resolves from the nearest reachable tag, and a shallow
clone produces a **wrong version rather than an error** — combined with `<Version>` having been deleted from
the csproj, that is a silent-wrong-version path.

### 5.3 Settings you must change

1. **Allow Actions to open pull requests.** Settings → Actions → General → Workflow permissions →
   ✅ *"Allow GitHub Actions to create and approve pull requests"*.

   Stage B does **not** push the assembled `CHANGELOG.md` to `main` — it opens a PR. This is structural, not
   stylistic: `GITHUB_TOKEN` acts as `github-actions[bot]`, an **Integration**, which is not among the
   ruleset's `bypass_actors` (only the admin role is), so the push is rejected outright — and even with a
   bypass, a `GITHUB_TOKEN` push **triggers no workflow**, so the commit would land unbuilt and poison the
   next release.

2. **Do not add a tag ruleset that blocks `github-actions[bot]` from creating `v*` tags.** Stage A is the only
   tagger. Creating a *tag* ref is not a branch push, which is precisely why the `main` ruleset does not apply
   to it and why this model is implementable at all with the existing protection. A well-meant tag protection
   rule would break releasing entirely.

3. **Create a `release` environment only if** you took the § 3.2 API-key fallback, or you set an environment on
   the nuget.org Trusted Publishing policy. If the registry policy names an environment and the job does not
   declare a matching one, the exchange fails with everything already built.

4. **Leave `portability-probe` non-required.** It carries job-level `continue-on-error`, so a workflow *run*
   can conclude `success` with a red job inside it. Stage A must depend on **`build-test-analyze` only**, by
   job name verbatim.

---

## 6. Your new workflow

### 6.1 Every merge to `main` — automatic, nothing to do

```
merge to main
   └─ build-test-analyze runs (the required check)
        └─ Stage A  [needs: build-test-analyze — so it cannot start unless the suite passed]
             1. read the highest v0.1.*-preview.N tag, compute N+1
             2. create and push the tag at the merge commit
             3. build the nupkg + three RID archives (MinVer now resolves to that tag, height 0)
             4. publish a PRERELEASE GitHub Release with the archives and their SHA-256 digests
```

That `needs:` **is** the NFR9 gate. There is no check-run query, no polling, no pagination default to get
wrong, and no way to release a commit whose tests did not pass.

**What this costs, and it is fine:** a tag and a GitHub Release per merge. Version numbers climb faster than
releases ship. `-preview.N` is a counter, not a promise, and nothing is consumed until you promote — an
unpromoted tag and its Release are simply deletable.

### 6.2 When you actually want to ship — manual, deliberate

```sh
gh workflow run <stage-b>.yml --ref main -f tag=v0.1.0-preview.7
```

Stage B's entire preflight is: **the tag exists**, and **a Stage A Release exists for it**. That is sufficient
*because Stage A only creates a Release when its tests passed* — the green-ness is inherited from an artefact
this pipeline produced, not re-derived from an API whose defaults lie.

Then, in order: credential exchange → registry preflight (is this version already anywhere?) → renderer
package → wrapper → assemble the changelog section into the Release body.

### 6.3 What changes for story work

**Effective 2026-08-08: every story that lands a user-visible change adds one file.**

`changelog.d/<story-key>.md` — e.g. `changelog.d/16-3-cli-packaging-and-publication.md`. Section headings and
bullets only, **no version header**:

```markdown
### Added
- The renderer artefact now ships inside the published package.

### Changed
- **BREAKING:** `SPECSCRIBE_RENDERER_DIR` is no longer required by packaged consumers.
```

- The fragment belongs in the story's **File List** like any other file.
- **`**BREAKING:**` is the load-bearing signal, not the version number.** Inside `0.x`, MINOR carries two
  meanings (breaking *or* new feature), so a consumer reads the changelog, not the digits.
- One new file per story means two concurrent stories cannot conflict — which is the entire reason
  `CHANGELOG.md` is assembled rather than hand-edited. Given this repo's concurrency posture, a hand-edited
  root changelog would be its highest-contention file.
- Fragments are consumed **at promotion, not at merge** — Stage A tags every merge, and consuming there would
  spend a story's entry on a tag that may never ship.
- **A fragment never written is invisible.** No gate catches it, deliberately (ADR 0033 governs new gates and
  that design work is Story 16.6's). The control is Story 16.7's pre-cut checklist.

### 6.4 Version numbers you will see

| shape | what it is |
|---|---|
| `0.1.0-preview.3` | a tag at height 0 — a real, promotable version |
| `0.1.0-preview.3.7` | **a build identifier, not a version.** Feature branch or dirty tree. Never promotable, and Stage B cannot promote one since it promotes tags and a tag is by definition at height 0 |
| `0.1.0-preview.0.<height>` | an untagged build — what a local `dotnet pack` produces today, since the repo has zero tags |

MINOR (`0.N.0`) for any breaking change **or** new user-visible feature. PATCH (`0.N.P`) for fixes, perf, docs,
refactors. `-preview.N` re-cuts the same target version after a failed or withdrawn release.

---

## 7. When a release fails

**Do not retry the version.** Recovery is forward — always.

| failed at | what is spent | what to do |
|---|---|---|
| Stage A (anything) | nothing | fix on `main`; the next merge tags again |
| Stage B preflight / credential exchange | nothing | fix and re-dispatch the same tag |
| **the nuget.org push** | possibly the version | check nuget.org — if the version is listed, it is **consumed** |
| npm, after nuget succeeded | **the version, on nuget** | withdraw the half that landed (§ below), then promote `preview.N+1` |

A partial promotion must be **withdrawn, not merely superseded** — a nuget-succeeded/npm-failed run leaves a
listed, permanently installable half-release on the channel ADR 0040 designates *authoritative*.

**Withdrawing, and deletion is the wrong instinct on both registries:**

1. **nuget.org: unlist. Never delete** — deletion breaks restore for anyone who already resolved it.
2. **npm: `npm deprecate`** naming the superseding version. Never `npm unpublish`.
3. **GitHub Release: delete it**, and its assets.
4. **`CHANGELOG.md`: keep the entry**, marked `[X.Y.Z] — WITHDRAWN`, naming what superseded it. The number is
   permanently spent, and a reader who finds a stale reference to it deserves an explanation.

Gaps in the `-preview.N` sequence are normal. `preview.1` followed by `preview.3` is a consumed number, not a
mistake.

---

## 8. Open items on your desk

| | item | blocks |
|---|---|---|
| 1 | **Confirm nuget.org Trusted Publishing vs the `NUGET_API_KEY` fallback** (§ 3) | Story 16.4's rework — it changes the job's permissions and environment |
| 2 | **Claim the npm names**, `specscribe-renderer` first (§ 2b) | Story 16.8; and it is the only prerequisite a third party can take away |
| 3 | **Freeze the Stage B workflow filename** (§ 2a) | the nuget.org policy, which cannot be told the file moved |
| 4 | **Rework Story 16.4** from tag-triggered to Stage A + Stage B (§ 1) | every release |
| 5 | **Ratify [ADR 0022](adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md)** — `Proposed` since 2026-07-27, and ADR 0040 amends it | nothing mechanically; it is the next release-chain ratification to make |
| 6 | Enable *"Allow GitHub Actions to create and approve pull requests"* (§ 5.3) | Stage B's changelog PR |

Not blocking, but worth knowing they are known: the renderer artefact carries **no version stamp** yet
(§ Decision 5 assigned the check to 16.3 and the stamp to 16.4; neither shipped, so the single-archive-per-RID
rule is currently the *sole* control against a desynchronized CLI/renderer pair), `CHANGELOG.md` and
`changelog.d/` do not exist yet, and nothing is code-signed — SmartScreen warns on Windows and Gatekeeper
blocks on macOS until cleared, with the published SHA-256 digests as the compensating control.
