---
baseline_commit: 15336f4 # local `main` == `origin/main` at authoring time (2026-08-07), working tree CLEAN.
                         # EIGHT commits ahead of Story 16.3's authoring point (07bdb79) and ten ahead of
                         # 16.1's (838d591). Verify every cited line number before trusting it: shared main.
epic: 16
frs: [FR32] # release engineering. 16.2 is the CI half, 16.3 the PACKAGING half, this story is the PIPELINE.
nfrs: [NFR9] # "Release builds are reproducible and produced by CI from a clean checkout; publishing to any
             # distribution channel is gated on a passing build + test run." (epics.md:138)
             # ⚠️ This story is the ONLY place the second clause is discharged. 16.2 built the gate; 16.3
             # built the package; nothing yet connects "gated on" to "publishing".
depends_on: [16-1, 16-2, 16-3]
  # 16-1 → ADR 0040 decides every shape here. TWO of its decisions are still ⚠️ OPEN and BLOCK this story (R1).
  # 16-2 → the gate this pipeline points at, plus docs/CiGate.md § How a release tag inherits this gate.
  # 16-3 → the package this pipeline publishes, plus docs/Packaging.md. TWO of its ADR-assigned mitigations
  #        are UNDISCHARGED and one of them breaks the channel this story ships (R3).
blocks: [16-5, 16-6, 16-7, 16-8]
  # 16.5 extends this pipeline (or adds a parallel job) to publish the VSIX — epics.md § 16.5 AC #2.
  # 16.6 documents the install path this pipeline creates, and owns CHANGELOG.md, which this job READS (R6).
  # 16.7 re-verifies install "end-to-end from the PUBLISHED artifact"; nothing is published until this lands.
  # 16.8 adds the npm/npx channel as jobs in this workflow, under ADR 0040 § 5's normative publish ORDER.
informs: [16-9, 17-4]
amends: epics.md # ⚠️ CONDITIONAL and OWNER-GATED. AC #2's "safe to re-run" clause is UNACHIEVABLE against
                 # immutable registries (ADR 0040 § Decision 10). Whatever the owner decides, the AC text
                 # must change — and per CLAUDE.md that lands in epics.md AND sprint-status.yaml in the same
                 # change. See R1-C. Do NOT quietly implement something other than what AC #2 says.
ships_product_code: false # Edits .github/workflows/** and docs/**. Does NOT edit src/**, tests/**, web/**,
                          # or extension/**. If you find yourself in src/, you have picked up 16.3's work —
                          # see R3 and hand it back rather than absorbing it.
decides: null # No new ADR expected. ADR 0040 already decides the shapes; the two OPEN items in R1 are
              # AMENDMENTS TO ADR 0040, not new records — write them into 0040 § Decision 9 and § Decision 10
              # where the ⚠️ OPEN markers currently sit, and delete the markers. Per CLAUDE.md, do not bury
              # the answer in this story file or in sprint-status.yaml prose.
deliverables:
  - ".github/workflows/release.yml (NEW — the tag-triggered pipeline; the ONLY new workflow this story adds)"
  - "docs/Releasing.md (NEW — how to cut a release, how to dry-run it, and what to do when a publish fails)"
  - "docs/adrs/0040-release-channels-and-versioning-policy.md (resolve the two ⚠️ OPEN markers, § 9 and § 10)"
  - "docs/CiGate.md (§ How a release tag inherits this gate — point it at the implementation this story ships)"
  - "docs/Packaging.md (§ What this does not cover — retire the three lines this story closes)"
  - "_bmad-output/planning-artifacts/epics.md + sprint-status.yaml (AC #2 amendment, IF the owner's § 10 answer requires it)"
---

# Story 16.4: Tag-Triggered Release Pipeline

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer cutting a release,
I want pushing a release tag to build, verify, package, and publish automatically,
So that releases are one action and never depend on a local machine's state.

| | |
|---|---|
| **Epic** | 16 — Release Engineering & Community Preview Launch |
| **Authority for every shape here** | [ADR 0040](../../docs/adrs/0040-release-channels-and-versioning-policy.md) (`Proposed`) — § 1, 2, 3, 5, 6, 7, 9, 10, 13 all route work here |
| **Operational companions already written** | [docs/Packaging.md](../../docs/Packaging.md) (16.3 — *"the artifact 16.4 builds its pipeline from"*) · [docs/CiGate.md](../../docs/CiGate.md) § How a release tag inherits this gate (16.2 — the query shape) |
| **Regression floor** | `dotnet test` + `cd web && npm run check` (4 gates) must stay green. This story ships no product code, so the floor is a *non-regression* check, not the proof of the work. |
| **The real proof** | A **dry run** that produces all four artifacts and publishes nothing, plus a **live cut** of `v0.1.0-preview.1`. See Task 10 — you cannot prove a publish pipeline with unit tests. |

---

## ⛔ Read first

This story is not "write a GitHub Actions workflow." Three of its inputs are **blocked on owner decisions**,
two of its declared prerequisites **were not actually delivered** by the story that owed them, and the
repository has **never had a git tag** — so the trigger this pipeline is built on has never fired.

Read R1 through R4 before writing a line of YAML.

---

### R1 — 🚨 THREE OWNER GATES. Two of them ADR 0040 explicitly forbids you to work around.

ADR 0040 carries two ⚠️ **OPEN** markers, and both name this story. The ADR's own words on the first:
*"Until it is decided, Story 16.4 must not infer a mechanism."* Treat these as hard gates, not as ambiguity
to resolve with a sensible default.

#### R1-A — Trusted Publishing vs. a stored API key (§ Decision 3) — *changes one step*

Story 16.1 owner action § 8 item 2, restated by 16.3 § 8 item 4: **"Check this before 16.4 starts, not
during."** nuget.org's Trusted Publishing is still a gradual rollout and its visibility on the owner's
account is **unknown** — it cannot be checked without the account.

| answer | what the push step looks like |
|---|---|
| **Trusted Publishing available** (preferred) | `NuGet/login@v1` with `id-token: write`; **nothing is stored** in the repository |
| **Not available** (fallback, § Decision 3) | repository secret **`NUGET_API_KEY`**, scoped to a `release` **environment** so PR workflows cannot reach it, shortest offered expiry, owner-rotated |

This one is **not blocking** — the fallback is fully specified, so you can build the pipeline either way and
swap one step. But **ADR 0040 § Decision 3's "stores nothing" headline weakens to two-of-three channels
under the fallback**, and if the fallback is taken you must say so in the ADR rather than leave the headline
overstating the posture.

🚨 **The workflow's FILENAME is load-bearing under Trusted Publishing, and nothing in the repository will
tell you when you break it.** A nuget.org trusted-publishing policy is bound to *repo owner + repo + workflow
**filename only** + optional environment* (16.1 § 8 item 3). So:

- **Pick `release.yml` and never rename it.** A rename silently invalidates the policy; the failure surfaces
  as a push rejection at the very last step of a release, after the version has already been consumed by any
  channel that went first.
- **If the owner sets an *environment* in the policy, the release job must declare the matching
  `environment:`** — otherwise the exchange fails even though the repo, owner and filename all match.
- Record the chosen filename and environment in `docs/Releasing.md` as configuration the owner mirrors on
  nuget.org, not as an incidental implementation detail.

#### R1-B — § Decision 9's gate mechanism — ⚠️ **BLOCKING**

NFR9 requires publishing to be gated on a passing build+test run. ADR 0040 § 9 satisfies that by **requiring
the tagged commit to already be green on `main`** — deliberately *not* by re-running build+test in the
release job (*"re-running invites a different result from the same source and doubles the wall-clock"*).

**The query shape already exists** — Story 16.2 wrote it into `docs/CiGate.md:174-195`, and it is correct.
Do not invent another one:

```sh
# The tag's commit
SHA=$(gh api repos/IntegerMan/SpecScribe/git/refs/tags/<tag> --jq '.object.sha')
# Runs of the gating workflow for exactly that commit
gh api "repos/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml/runs?head_sha=$SHA" \
  --jq '.workflow_runs[] | [.run_number, .conclusion] | @tsv'
# ⚠️ The RUN conclusion is NOT sufficient. portability-probe carries job-level continue-on-error, so a run
#    can report `success` while that job is red. Query the JOB:
gh api repos/IntegerMan/SpecScribe/actions/runs/<run_id>/jobs \
  --jq '.jobs[] | select(.name == "build-test-analyze") | .conclusion'
```

**What is NOT decided, and what you must not guess (ADR 0040 § 9's own list):**

1. **No commit outside `main` is ever built by the gating workflow.** `build-test-analyze.yml:20-23` triggers
   on `push`/`pull_request` to `main` only — verified at this baseline. A tag on a release branch, or any
   hotfix to an older release, has **no run to point at**, and the naive lookup returns "no run found" with
   no defined action. As written, **a hotfix is structurally impossible without first merging to `main`**,
   which the ADR neither permits nor forbids.
2. **Which API, which check name, what timeout, what to do when the run is still in progress, and what to do
   when a re-run has since turned red.** None is specified.

**Recommendation to put to the owner** (a starting point, not a decision you may take alone):

> Preview-only posture: the release job resolves the tag's SHA, requires a **completed `build-test-analyze`
> job with conclusion `success`** for that exact SHA, **polls up to 20 minutes** if a run is `in_progress`
> (a tag pushed with its commit races the gate), and **hard-fails** on `no run found` / `failure` /
> `cancelled` / timeout. Hotfix branches are **out of scope for the preview** — during `0.x`, tags are cut
> from `main` only, and that becomes a stated release procedure in `docs/Releasing.md` rather than an
> unwritten assumption. Revisit at 1.0.

#### R1-C — § Decision 10, release atomicity — ⚠️ **BLOCKING, and it invalidates AC #2 as written**

This is the sharpest problem in the story. **AC #2 (epics.md verbatim) requires *"a failed publish leaves no
partially-released state (the pipeline is safe to re-run)"*, and that is not achievable.** The constraint is
external and non-negotiable:

- **nuget.org rejects a duplicate version** and permits only *unlisting*, never deletion.
- **npm rejects publishing over an existing version**; its unpublish window is time-limited.
- A multi-channel release is therefore **not transactional**. nuget.org succeeding and a later channel
  failing leaves a version half-released and **permanently consumed**.

ADR 0040 § 10 lists what must be decided:

1. Does a failed release **bump to a new patch/prerelease number and re-tag** (the usual answer, and the only
   one that composes with immutable registries), **or** does the pipeline attempt **per-channel resume**?
2. **How is a bad preview withdrawn** once published — unlist on nuget.org, `npm deprecate`, delete the
   GitHub Release, or leave it and supersede?

**Recommendation to put to the owner:**

> **Version burn.** A failed release is not retried at the same version: the operator bumps `preview.N` and
> pushes a new tag. Rationale: it is the only policy that composes with immutable registries, it needs no
> resume state, and during `0.x` a burned prerelease number costs nothing. Withdrawal = **unlist on
> nuget.org** (never delete) + **delete the GitHub Release** + a superseding entry in `CHANGELOG.md`.
> The pipeline is then made **fail-fast and idempotent-by-refusal**: it verifies the version does not already
> exist on nuget.org **before** it builds anything, so a re-run of a burned tag fails in seconds instead of
> at the push step.

⚠️ **Whatever the owner decides, AC #2's text must change** — and per CLAUDE.md § Decision records that is a
structural scope change landing in **`epics.md` and `sprint-status.yaml` in the same change**, plus the
resolution written into ADR 0040 § Decision 10 (replacing the ⚠️ OPEN marker). **Do not implement a
different behaviour and leave AC #2 saying something the pipeline does not do.** That is the exact
"bury the decision in a story artifact" pattern CLAUDE.md names.

---

### R2 — What ALREADY EXISTS. Reusing it is the whole job; rebuilding it is the failure mode.

Epic 16 has three landed stories ahead of this one. **Everything in this table is done — do not re-derive it.**

| you might be tempted to build | it already exists | where |
|---|---|---|
| a build+test step inside the release job | **Forbidden.** ADR 0040 § 9 + epics.md § 16.2 (AMENDED 2026-07-25): *"do not create a second build+test workflow"* | `.github/workflows/build-test-analyze.yml` |
| a way to ask "is this commit green?" | the exact `gh api` query, including the job-vs-run trap | `docs/CiGate.md:174-195` |
| version derivation from the tag | **MinVer**, already wired with three properties | `SpecScribe.csproj:63-67`, `docs/Packaging.md § Versioning` |
| a check that the nupkg carries the renderer | `AssertRendererPacked`, `AfterTargets="Pack"` — unzips the produced nupkg and asserts the entry point at `tools/$(TargetFramework)/any/renderer/server/index.mjs` | `SpecScribe.csproj:143-161` |
| a check that a publish has a renderer to copy | `AssertRendererAvailableForPublish`, `BeforeTargets="PrepareForPublish"` | `SpecScribe.csproj:165-168` |
| the renderer build recipe and its load-bearing order | `SPECSCRIBE_PACKAGE_BUILD=1 npm ci` → `sync:assets` → `build:package` | `docs/Packaging.md § Build order` |
| how to verify a package you were handed | the four-step probe, **including the negative case** | `docs/Packaging.md § Verifying a package` |
| branch protection / required check | ruleset `20567252`, context `build-test-analyze`, verified against PR #7 | `.github/rulesets/main-required-checks.json`, `docs/CiGate.md` |

🚨 **ADR 0040 § Decision 1 says "A packaging-time completeness assertion is REQUIRED, not optional (Story
16.4)". That obligation is ALREADY DISCHARGED** — Story 16.3 shipped `AssertRendererPacked`, and because it
is `AfterTargets="Pack"` your release job gets it **for free** on every `dotnet pack`. **Do not add a second
nupkg assertion.** What is *not* covered is the **self-contained binary archives**:
`AssertRendererAvailableForPublish` checks the **source** directory, not the produced archive. Asserting the
archive is genuinely yours — see R5 and AC #4.

---

### R3 — 🚨 TWO ADR 0040 mitigations assigned to Story 16.3 are UNDISCHARGED, and one breaks the channel THIS story ships.

Verified at this baseline (`15336f4`), by reading the code, not by trusting the story record. **Story 16.3 is
at `review`, not `done`, so these are still its work — record the handoff, do not absorb it** (CLAUDE.md
§ Scoping a code review: *"a symbol whose doc comment attributes it to another story is that story's to
review — record the handoff explicitly"*).

#### R3-1 — The renderer is spawned through the UNQUOTED single-string overload. ⚠️ **This blocks the binary channel.**

`src/SpecScribe/NuxtPrerender.cs:345`:

```csharp
var psi = new ProcessStartInfo(NodeExecutable(), Path.Combine(_artefactDir, "server", "index.mjs"))
```

That is the single-string `arguments` overload, **not** `ArgumentList`. ADR 0040 § Decision 1 flagged it
explicitly and assigned the fix to 16.3 (*"Story 16.3 must move this call to `ArgumentList`"*, spike open
item 16). **It was not done.**

**Why this is 16.4's problem and not a distant nicety.** Until ADR 0040, `_artefactDir` was a developer's repo
path or an explicit env var. This story is what puts the binary into a **consumer-chosen path** — a user
unzips a GitHub Release asset wherever they like. `C:\Users\Matt Eland\Downloads\…`,
`C:\Program Files\SpecScribe\`, `~/My Tools/` all truncate the script argument, and the **first run of a
downloaded binary fails**. The 16.1 probe path had no spaces, so this has never been exercised.

**Action:** confirm with the owner whether 16.3 takes it before this story's live cut, or whether it is pulled
forward into this story. It is a one-line change plus a test; it is *not* optional before the binary channel
is published. **Prove it either way**: unzip a produced archive into a path containing a space and run
`generate` from it.

#### R3-2 — Nothing stamps the artefact with the CLI version.

ADR 0040 § Decision 5: *"Story 16.3 must stamp the artefact with the CLI version and **fail loudly on a
mismatch** rather than rendering from a stale renderer."* Verified: `ResolveArtefactDirectory`
(`NuxtPrerender.cs:81-133`) tests **only** that `server/index.mjs` exists. No stamp, no comparison, nowhere.

**Consequence for this story, stated plainly.** ADR 0040 § Decision 5 mitigated the self-contained channel's
two-filesystem-objects problem with **two** controls: the version stamp (16.3) and *"Story 16.4 must ship each
RID as a single archive containing both halves"* (this story). **Only one of the two will exist.** That makes
your single-archive obligation the *sole* mitigation, so it is more load-bearing than the ADR assumed:
shipping the `.exe` and the renderer as separate release assets — even briefly, even "just for testing" — is
the failure mode, and it *"fails as wrong output rather than as an error"* (Story 16.9 AC #2's phrasing).

---

### R4 — 🚨 The repository has ZERO git tags. Your trigger has never fired.

Verified at this baseline: `git tag -l` returns nothing. Consequences you must plan for, not discover:

- **A tag-triggered workflow cannot be tested by pushing the real tag.** Pushing `v0.1.0-preview.1` sets
  MinVer's floor for every subsequent build *and* consumes that version on nuget.org the moment the pipeline
  works. 16.3's recommendation, unchanged: **do not push a real tag until this story lands and dry-runs
  clean.** Task 10 is how you get around this.
- **The bootstrap tag is an owner action** (16.3 § 8 item 2). This story builds the mechanism; the owner
  pushes the tag.
- **Untagged builds are already well-formed** thanks to 16.3's MinVer properties (`MinVerTagPrefix=v`,
  `MinVerMinimumMajorMinor=0.1`, `MinVerDefaultPreReleaseIdentifiers=preview.0`) — an untagged build is
  `0.1.0-preview.0.<height>`, not `0.0.0-alpha.0.N`. So a dry run produces a sane version to reason about.
- **Tags are `v`-prefixed** (`MinVerTagPrefix=v`) but the **package version is not**. `v0.1.0-preview.1` → the
  nupkg is `SpecScribe.0.1.0-preview.1.nupkg` and the release assets are
  `specscribe-0.1.0-preview.1-win-x64.zip`. Strip the `v` for artifact names; keep it for the tag and the
  Release name. Getting this backwards produces asset names that do not match anything documented.

---

### R5 — The seven things ADR 0040 routes here, each with its silent-failure mode

Every one of these is a *green pipeline that ships something wrong*. That is why each has a named assertion.

| # | obligation | ADR | if you get it wrong |
|---|---|---|---|
| 1 | **`SOURCE_DATE_EPOCH` = the tagged commit's committer timestamp** (`git log -1 --format=%ct`), **never** the run's start time | § 7 | The csproj gates validity on `^[0-9]{1,10}$` and **falls back to today's date** (`SpecScribe.csproj:36-38`). An unset, misspelled or malformed variable **silently stamps the build date** and the pipeline stays green. **Assert it is set and well-formed BEFORE building.** |
| 2 | **`fetch-depth: 0` on checkout** | § Consequences | MinVer resolves from tag reachability. A shallow clone produces a **wrong version rather than an error**. Explicitly assigned here: *"belongs to the stories that own the workflows — 16.2 and 16.4 — not to 16.3."* |
| 3 | **`npm run build:package`, never `npm run build`** | § Rel. to 0022 | A plain `nuxt build` bakes SpecScribe's own pages into `.output/public`, and Nitro serves `public/` **ahead of** the SSR route — the packaged artefact then returns SpecScribe's pages for the consumer's project **at HTTP 200**. Wrong answer, success status. |
| 4 | **One archive per RID containing BOTH halves** — `specscribe-<version>-<rid>.zip` (Windows) / `.tar.gz` (Linux, macOS) | § 2, § 5 | Separate assets let a user unzip release N over N−1 and desynchronize the pair. With R3-2 undone, nothing detects it. |
| 5 | **SHA-256 digest for every asset, published in the release body** | § 2, § 13 | This is the **only channel without integrity by construction** — § 13 declines code signing, npm publishes provenance by default, NuGet carries the registry's guarantees. A consumer clicking through SmartScreen must have something to verify against. |
| 6 | **`NuGet/login@v1` immediately before the push step, not at job start** | § 3 | The key is valid **one hour and is single-use**, while the job builds three ~76 MiB RIDs plus a Nuxt artefact ahead of it. **A push that fails consumes the key** — the retry must re-run the exchange. If the exchange itself fails, **fail before publishing to any channel**. |
| 7 | **Copy the released version's `CHANGELOG.md` section into the Release body**; an empty section is **not an error** | § 6 | It must write *"No user-visible changes in this release."* and continue. **It must not hard-fail at the last step** — by then the packages are published and the version is burned. See R6: the file does not exist yet either. |

**The credential exchange output**, verified current (2026-08-07): `NuGet/login@v1` exposes the short-lived
key as `steps.<id>.outputs.NUGET_API_KEY`, and the job needs `permissions: id-token: write`. The `user:`
input is the owner's nuget.org **profile name, not their email** (16.1 § 8 item 3).

---

### R6 — `CHANGELOG.md` DOES NOT EXIST, and the story that owns it is scheduled AFTER this one.

Verified at this baseline: no `CHANGELOG.md` at the repository root. ADR 0040 § Decision 6 requires the
release job to copy the released version's section into the Release body — and assigns authorship of the file
to **Story 16.6**, which is `backlog`, behind this story.

**ADR 0040 § 6 handles the empty-`[Unreleased]` case but not the missing-file case.** They are different, and
the difference matters: the job's last step runs *after* the packages are published, so a crash there burns a
version for a formatting problem.

**Recommendation (confirm, then record it in ADR 0040 § Decision 6):** the release job treats *file absent*,
*section absent* and *section empty* as the **same non-fatal path** — write
`"No user-visible changes in this release."` and continue — and **warns** in the job log rather than failing.
Optionally this story seeds a minimal Keep-a-Changelog 1.1.0 skeleton so 16.6 has a file to fill; the
**format and policy remain 16.6's**, so do not write policy prose into it here.

⚠️ Do **not** substitute GitHub's generated release notes. ADR 0040 § 6 rejects them explicitly, and the
reason is this repository specifically: *"commits routinely bundle several stories (CLAUDE.md § Concurrent
work) — the commit is not the unit of change here, the story is."*

---

### R7 — Scope guard: what this story publishes, and what it must NOT touch

**In scope — two channels:**

1. **nuget.org**, the `dotnet` global tool (ADR 0040 § 2, channel 1 of the preview cut)
2. **GitHub Releases**, the three self-contained binaries — `win-x64`, `linux-x64`, `osx-arm64`

**Out of scope, with owners:**

| not this story | why | owner |
|---|---|---|
| **npm / npx** | ADR 0040 § 2 channel 2, but the wrapper and the platform packages are a separate story. **Shape the workflow so it can be extended**, and honour § 5's normative order when it is: `specscribe-renderer` **FIRST**, then the wrapper. | **16.8** |
| **VSIX / VS Marketplace** | ADR 0040 § 4: explicitly **OUT of the first preview**. | **16.5** |
| `linux-arm64`, `osx-x64` | named and deferred (§ 2 non-goals) | on demand |
| Code signing / notarization | declined for the preview (§ 13) | revisit at 1.0 |
| `<Deterministic>` / `ContinuousIntegrationBuild` / SourceLink | deferred past preview (§ 7) | **17.4** |
| Byte-identical rebuilds | explicit non-goal (§ 7 claims the **weaker** reading of NFR9) | — |
| Documenting install/upgrade for consumers, changelog **content**, versioning policy prose | | **16.6** |
| Re-verifying the install from the **published** artifact on a clean environment | | **16.7** |
| Reserving package IDs, configuring trusted-publishing policies, ratifying ADR 0040, pushing the first tag | **owner actions** (16.1 § 8) | **owner** |

**NFR9 — what this story may claim.** ADR 0040 § 7 claims the **weaker** reading: *built from a clean checkout
by CI*, **not** byte-identical rebuilds. Of its three named preview gaps, `npm ci` is closed (16.2, `0b1f561`)
and version-from-tag is closed (16.3). **This story closes exactly one: `SOURCE_DATE_EPOCH`.** It is also the
story that discharges NFR9's *second* clause — *"publishing … is gated on a passing build + test run"* —
which no story has discharged yet. **Do not let the completion notes claim more than those two.**

---

### R8 — ADR 0040 is `Proposed`, not `Accepted`

Story 16.1 AC #4 required a ratified ADR; ratification is owner action § 8 item 6 and is still outstanding.
16.2 and 16.3 both shipped against it anyway, with their reasoning on the record (16.3 § R10).

**The calculus is different here, and worse.** 16.3's exposure was *"one `PackageReference` and three
properties to swap"* if § Decision 5's mechanism were rejected. This story's exposure is § Decision 9 and
§ Decision 10 — which are not merely unratified, they are **unanswered** (R1). **Proceed on everything else;
gate on those two.** If ratification changes anything else, that is an ADR amendment, not a story note.

---

### 🔎 Analysis observations — UNKNOWN, not clean

`.specscribe/analysis/` **does not exist** at `15336f4` (verified). Per CLAUDE.md, **absent means UNKNOWN,
never clean**. This story touches `.github/**` and `docs/**`, which the digest does not cover in any case
(`sonar.exclusions` includes neither, but no C# or Vue file is edited here). If you end up in `src/**` — you
should not; see R3 — regenerate first with `node tools/analysis-digest/index.mjs` and read only your shards.

### Concurrency — the CLAUDE.md conditions apply

Another agent may be editing shared `main` right now. **Verify after every edit** (grep for what you just
wrote). **Never `git reset --hard`, `git checkout --`, or `git clean`.** Expect commits to bundle sibling
stories. `.github/workflows/build-test-analyze.yml` is Story 16.2's file and is at `review` — if you must
touch it (you should not need to), attribute by hunk and say so.

---

## Acceptance Criteria

**AC #1 and AC #2 are `epics.md` § Story 16.4 verbatim.** #3–#9 make ADR 0040's routed decisions checkable;
they add no scope the ADR did not already assign here.

1.
**Given** a release or pre-release tag is pushed
**When** the release pipeline runs
**Then** it builds and tests on a clean checkout, packages per Story 16.3, publishes to the chosen channel(s),
and attaches the release artifacts to the corresponding GitHub Release
**And** publishing is gated on the build+test step passing (NFR9).
   - **"builds and tests on a clean checkout" is satisfied by ADR 0040 § 9's mechanism** — requiring the
     tagged commit to already be green on `main` — **not** by re-running build+test in the release job.
     epics.md § 16.2 (AMENDED) forbids a second build+test workflow. Implement R1-B's resolved answer.
   - Checkout uses **`fetch-depth: 0`**. A shallow clone yields a wrong version, not an error.
   - The renderer artefact is built with **`npm run build:package`**, never `npm run build`, after
     `SPECSCRIBE_PACKAGE_BUILD=1 npm ci` and `npm run sync:assets` — that order is load-bearing
     (`docs/Packaging.md § Build order`).
   - The nupkg is produced by `dotnet pack`, which fires `AssertRendererPacked` automatically. **Do not add a
     second nupkg assertion** (R2).
   - Node comes from `web/.nvmrc` via `node-version-file`, never a hand-typed version.

2.
**Given** a `-preview` / pre-release tag
**When** the pipeline publishes
**Then** the release is marked as a pre-release / preview channel per Story 16.1's policy
**And** a failed publish leaves no partially-released state (the pipeline is safe to re-run).
   - The GitHub Release is created with **`prerelease: true`** whenever the version carries a SemVer
     pre-release label — which, per ADR 0040 § 5, is **every** preview release. Derive it from the version,
     not from a hand-set input.
   - ⚠️ **The second clause is UNACHIEVABLE as written** against immutable registries (ADR 0040 § Decision 10,
     16.1 open item 11). **This AC must be reworded to the owner's R1-C answer before it can be met**, and
     the rewording lands in `epics.md` + `sprint-status.yaml` + ADR 0040 § Decision 10 together. **Do not
     mark this AC done against text the pipeline does not honour.**

3.
**Given** ADR 0040 § Decision 7
**When** the release job builds anything
**Then** `SOURCE_DATE_EPOCH` is set from the **tagged commit's committer timestamp** (`git log -1
--format=%ct` on the release ref), never from the workflow run's start time
**And** the job **asserts the value is set and matches `^[0-9]{1,10}$` before the first build step**, failing
the release rather than quietly producing an irreproducible artefact.
   - The csproj's fallback is **today's date** (`SpecScribe.csproj:36-38`), so a typo is invisible without
     this assertion. That is precisely why the ADR calls specifying the value *"load-bearing rather than
     pedantic."*
   - **Prove it:** two runs of the same tag stamp the **same** `BuildDate`, and it equals the tagged commit's
     date — not the run date.

4.
**Given** ADR 0040 § Decision 2 and § Decision 5
**When** the self-contained binaries are produced
**Then** each RID ships as **one archive containing both the executable and its sibling `renderer/`** —
`specscribe-<version>-<rid>.zip` for `win-x64`, `.tar.gz` for `linux-x64` and `osx-arm64` — never as separate
release assets
**And** the job **asserts the renderer entry point is present inside each produced archive** before it is
attached.
   - `AssertRendererAvailableForPublish` checks the **source** directory; it says nothing about what landed in
     the archive. This assertion is this story's (R2).
   - Assert the **path**, not the file count or byte total. 16.3 measured a wrong-path package at **203 files
     and an identical byte total with the entry point absent** (`docs/Packaging.md § Trap 2`).
   - With R3-2 undischarged, this is the **only** control preventing a desynchronized CLI/renderer pair.

5.
**Given** § Decision 2's integrity requirement and § Decision 13's refusal of code signing
**When** the GitHub Release is published
**Then** every attached asset has a **SHA-256 digest published in the release body**
**And** the digests are reproducible by a consumer against the downloaded file.

6.
**Given** ADR 0040 § Decision 3
**When** the pipeline publishes to nuget.org
**Then** the credential exchange runs **immediately before the push step**, the job declares
`permissions: id-token: write` (and `contents: write` scoped to the release job for the GitHub Release)
**And** if the exchange fails the job **fails before publishing to any channel**, so no partial release is
created
**And** no secret value is committed to the repository.
   - Under R1-A's fallback the secret is `NUGET_API_KEY`, scoped to a `release` **environment** so PR
     workflows cannot reach it — and ADR 0040 § Decision 3's "stores nothing" headline is amended to say so.

7.
**Given** ADR 0040 § Decision 6, and that `CHANGELOG.md` does not exist yet (R6)
**When** the release body is composed
**Then** the released version's changelog section is copied into it if present
**And** an absent file, an absent section, or an empty section all produce
`"No user-visible changes in this release."` and the job **continues** — it never hard-fails at this step,
because the packages are already published and the version is burned.

8.
**Given** that a tag-triggered publish cannot be rehearsed against a real registry
**When** the pipeline is verified
**Then** a **dry-run path** exists that executes every step except the two publish actions, is reachable via
`workflow_dispatch`, and **defaults to dry-run** so an accidental manual invocation cannot publish
**And** the dry run is shown producing all four artifacts (nupkg + three archives) with all assertions green.

9.
**Given** ADR 0040 § Decision 2's three-RID matrix, proven by 16.3 on the **host RID only** (16.1 open item 15)
**When** the pipeline runs
**Then** each RID's binary is **produced on, or verified on, its own operating system** rather than
extrapolated
**And** the completion notes state exactly which RIDs were *executed* and which were only *built*.
   - 16.3 said it plainly: *"Producing and executing `linux-x64` / `osx-arm64` binaries on their own operating
     systems is 16.4's CI matrix."* Do not claim a platform you did not run on.

---

## Tasks / Subtasks

- [ ] **Task 0 — Close the owner gates BEFORE writing YAML (AC: #2, #6; R1)**
  - [ ] Put R1-A (Trusted Publishing vs. `NUGET_API_KEY`), R1-B (§ 9 gate mechanism) and R1-C (§ 10 atomicity)
        to the owner with the recommendations already drafted in R1.
  - [ ] Write each answer into **ADR 0040**, replacing the ⚠️ OPEN markers in § Decision 9 and § Decision 10.
        These are amendments to an existing record, not a new ADR (`decides: null`).
  - [ ] If the § 10 answer changes AC #2's text: amend **`epics.md` § Story 16.4 AC #2 and
        `sprint-status.yaml` in the same change** (CLAUDE.md § Decision records).
  - [ ] Confirm with the owner whether **R3-1** (the `ArgumentList` fix) is taken by 16.3 before the live cut
        or pulled into this story. Record the handoff either way.

- [ ] **Task 1 — `.github/workflows/release.yml`: trigger, permissions, concurrency (AC: #1, #6)**
  - [ ] Trigger on `push: tags: ['v*']` plus `workflow_dispatch` with a `dry_run` input **defaulting to
        `true`** (AC #8).
  - [ ] `permissions: contents: read` at workflow level; **`contents: write` and `id-token: write` on the
        release job only**.
  - [ ] Concurrency group **must not** be `pages` (owned by `publish-docs-live-pages.yml`) and must not
        collide with `build-test-analyze-${{ github.ref }}`. Use `release-${{ github.ref }}`,
        `cancel-in-progress: false` — a cancelled release mid-publish is the worst state available.
  - [ ] `actions/checkout@v4` with **`fetch-depth: 0`**; `actions/setup-dotnet@v4` at `10.0.x`;
        `actions/setup-node@v4` with `node-version-file: web/.nvmrc`. Match the versions already used in
        `build-test-analyze.yml` — consistency beats novelty here.
  - [ ] Set a job `timeout-minutes`. Three ~76 MiB RID publishes plus a Nuxt build is not a two-minute job.

- [ ] **Task 2 — Gate the release on the tagged commit being green (AC: #1; R1-B)**
  - [ ] Implement the owner's R1-B answer using `docs/CiGate.md:174-195`'s query verbatim.
  - [ ] Query the **`build-test-analyze` JOB's** conclusion, not the run's — `portability-probe`'s job-level
        `continue-on-error` makes a run report `success` while that job is red.
  - [ ] Handle every branch the owner's answer defines: no run found, `in_progress`, `failure`, `cancelled`,
        timeout.
  - [ ] This step runs **first**, before anything is built. A gate that runs after the build is not a gate.

- [ ] **Task 3 — `SOURCE_DATE_EPOCH`, asserted before the first build (AC: #3)**
  - [ ] `SOURCE_DATE_EPOCH=$(git log -1 --format=%ct "$GITHUB_SHA")` on the release ref. **Never**
        `date +%s`, never the run start time.
  - [ ] Assert non-empty **and** `^[0-9]{1,10}$`, failing the job with a message that names the variable and
        the csproj's silent fallback. The csproj gate is at `SpecScribe.csproj:36`.
  - [ ] Export it to every subsequent build step (`$GITHUB_ENV`).

- [ ] **Task 4 — Build the renderer artefact (AC: #1)**
  - [ ] `cd web && SPECSCRIBE_PACKAGE_BUILD=1 npm ci` — the flag is **not optional** on a fresh checkout;
        `postinstall: nuxt prepare` hard-fails without an IR.
  - [ ] `npm run sync:assets` — `web/public/` is gitignored and absent on a fresh checkout, and
        `build:package` bakes it into `.output/public`.
  - [ ] `npm run build:package` — **NEVER** `npm run build` (R5 #3).
  - [ ] The order is load-bearing. `docs/Packaging.md § Build order` explains each of the three traps.

- [ ] **Task 5 — Pack the nupkg (AC: #1)**
  - [ ] `dotnet pack src/SpecScribe/SpecScribe.csproj -c Release -o artifacts`.
  - [ ] `AssertRendererPacked` fires automatically (`AfterTargets="Pack"`). **Add nothing.** Confirm its
        `AssertRendererPacked OK` message appears in the log — that is your evidence.
  - [ ] Read the version off the produced `.nupkg` filename. **Never hard-code a version anywhere**
        (`docs/Packaging.md § Versioning`).

- [ ] **Task 6 — The three-RID matrix and the single archives (AC: #4, #9)**
  - [ ] Matrix over `win-x64` (windows), `linux-x64` (ubuntu), `osx-arm64` (macos) — produce each on, or
        verify each on, its own OS (AC #9). 16.3 proved the mechanism on the host RID only.
  - [ ] `dotnet publish` self-contained per RID. `AssertRendererAvailableForPublish` guards the source side;
        `CopyToPublishDirectory="PreserveNewest"` on the `Content` item is what copies the payload.
  - [ ] Archive **the whole publish directory** as one file: `specscribe-<version>-<rid>.zip` (win) /
        `.tar.gz` (linux, macOS). The version is the **unprefixed** one (R4).
  - [ ] **Assert the entry point inside each produced archive** at its archived path — not count, not bytes
        (`docs/Packaging.md § Trap 2` measured 203 files and an identical byte total with the entry point
        absent).
  - [ ] Prove one archive actually runs: extract it into a path **containing a space** and run `generate`
        with `SPECSCRIBE_RENDERER_DIR` unset. This is R3-1's exercise; if it fails, R3-1 is your blocker.

- [ ] **Task 7 — Publish to nuget.org (AC: #1, #6)**
  - [ ] Place the credential exchange **immediately before** `dotnet nuget push` — never at job start. The
        key is single-use with a one-hour life, and everything above it takes real time.
  - [ ] Trusted Publishing path: `NuGet/login@v1` with an `id`, `user:` = the owner's nuget.org **profile
        name**, then `--api-key "${{ steps.<id>.outputs.NUGET_API_KEY }}"`.
  - [ ] The policy binds to the workflow **filename** (and optionally an environment). Fix the filename as
        `release.yml`, declare `environment:` if the owner's policy names one, and record both in
        `docs/Releasing.md` (R1-A).
  - [ ] Fallback path (R1-A): `NUGET_API_KEY` from a `release` **environment**.
  - [ ] If the exchange fails, **fail before any channel publishes**.
  - [ ] Skip this step entirely when `dry_run` is true.

- [ ] **Task 8 — Create the GitHub Release (AC: #1, #2, #5, #7)**
  - [ ] Create the Release for the tag with `contents: write` from the job's own `GITHUB_TOKEN` — nothing
        stored (ADR 0040 § 3).
  - [ ] `prerelease: true` derived from the version carrying a SemVer pre-release label, not from an input.
  - [ ] Attach the three archives. **One asset per RID** (AC #4).
  - [ ] Compute SHA-256 for every asset and publish the digests in the release body (AC #5).
  - [ ] Compose the body from `CHANGELOG.md`'s section for this version; absent file / absent section / empty
        section all fall back to `"No user-visible changes in this release."` and **continue** (AC #7, R6).
  - [ ] Skip publication when `dry_run` is true; upload the artifacts to the run instead so they are
        inspectable.

- [ ] **Task 9 — Failure and re-run behaviour (AC: #2; R1-C)**
  - [ ] Implement exactly the owner's § 10 answer. If it is **version burn** (the recommendation): verify the
        version does not already exist on nuget.org **before building**, so a re-run of a burned tag fails in
        seconds rather than at the push step.
  - [ ] Document the withdrawal procedure in `docs/Releasing.md` — the pipeline cannot do it, a human does.

- [ ] **Task 10 — Prove it without burning a version (AC: #8)**
  - [ ] Dry run via `workflow_dispatch` (default `dry_run: true`): every step except the two publishes, all
        assertions green, four artifacts uploaded to the run.
  - [ ] **Negative proofs, each run deliberately:** (a) a malformed `SOURCE_DATE_EPOCH` fails the job before
        building; (b) an archive missing its renderer fails the AC #4 assertion; (c) a SHA with no green
        `build-test-analyze` job fails at Task 2.
  - [ ] Only then, and only with the owner's go-ahead, cut the real `v0.1.0-preview.1`.

- [ ] **Task 11 — Documentation (AC: all)**
  - [ ] **NEW `docs/Releasing.md`**: how to cut a release, the dry-run path, what the pipeline gates on, the
        R1-C failure/withdrawal policy, and the fact that `0.x` tags come from `main` only (if R1-B lands
        that way). It is the operational companion to `docs/Packaging.md`, which explicitly defers publishing
        to this story.
  - [ ] `docs/CiGate.md § How a release tag inherits this gate` — replace *"That is Story 16.4's to
        implement"* with a pointer to the implementation.
  - [ ] `docs/Packaging.md § What this does not cover` — retire the three lines this story closes
        (publishing, cross-RID execution, `SOURCE_DATE_EPOCH`). Leave the rest.
  - [ ] Do **not** write install/upgrade docs, changelog content, or versioning-policy prose — 16.6's (R7).

- [ ] **Task 12 — Scope guard and regression floor**
  - [ ] `dotnet test` green and `cd web && npm run check` green (4 gates). This story ships no product code,
        so a moved gate is somebody else's change — see CLAUDE.md § Concurrent work before touching a
        baseline, and establish causality first.
  - [ ] Confirm the File List contains **no** `src/**`, `tests/**`, `web/**` or `extension/**` edits. If it
        does, you have absorbed 16.3's work (R3) — hand it back.
  - [ ] State in the completion notes: NFR9's `SOURCE_DATE_EPOCH` gap closed, NFR9's *gated publishing*
        clause discharged, **nothing more** (R7).

---

## Dev Notes

### The one-paragraph version

Write `.github/workflows/release.yml`. It fires on `v*`, proves the tagged commit was already green on `main`
(never re-running the suite), stamps `SOURCE_DATE_EPOCH` from the tagged commit, builds the Nuxt artefact with
`build:package`, packs one nupkg and three self-contained archives that each carry their own renderer,
verifies each archive by **path**, publishes to nuget.org through a credential exchange that happens at the
last possible moment, and cuts a pre-release GitHub Release with SHA-256 digests and the changelog section in
its body. Nearly every shape is already decided in ADR 0040 and already proven by 16.3 — **your risk is not
"how do I write this workflow", it is the three owner gates in R1 and the two undischarged 16.3 mitigations in
R3.**

### Existing patterns to follow, not reinvent

- **Workflow house style.** `build-test-analyze.yml` is heavily commented, and the comments carry *reasons*,
  not restatements — the ordering note at `:220-237` and the `--deep-git` note at `:280-292` are the model.
  Match that density. This repository has repeatedly paid for undocumented load-bearing ordering.
- **`defaults: run: shell: pwsh`** is used in `build-test-analyze.yml` because it is Windows-only. Your matrix
  spans three OSes — prefer `bash` (available on all three GitHub runners) so one script works everywhere, and
  say so in a comment rather than leaving the divergence unexplained.
- **Never share the `pages` concurrency group.** `publish-docs-live-pages.yml` owns it, and sharing makes the
  two workflows cancel one another (`build-test-analyze.yml:32-38` documents exactly this).
- **Reading the version:** `docs/Packaging.md § Verifying a package` derives it off the `.nupkg` filename.
  Reuse that, do not compute it a second way.

### Project Structure Notes

| path | disposition |
|---|---|
| `.github/workflows/release.yml` | **NEW.** The only new workflow. Do not create a second build+test workflow. |
| `.github/workflows/build-test-analyze.yml` | **Do not edit.** Story 16.2's, at `review`. You point *at* it; you do not change it. |
| `.github/workflows/publish-docs-live-pages.yml` | **Do not edit.** Independent by construction (`docs/CiGate.md § It does not disturb the Pages workflow`). |
| `.github/rulesets/main-required-checks.json` | A **record, not configuration** (`docs/CiGate.md:85`). Do not edit as a way of changing behaviour. |
| `docs/Releasing.md` | **NEW.** |
| `docs/CiGate.md`, `docs/Packaging.md` | Targeted edits only, named in Task 11. |
| `docs/adrs/0040-…md` | Resolve the two ⚠️ OPEN markers. |
| `src/**`, `tests/**`, `web/**`, `extension/**` | **Out of scope** (`ships_product_code: false`). |
| `CHANGELOG.md` | Optional minimal skeleton only; **content and format are 16.6's** (R6). |

### Testing standards

There is no unit-test surface for a GitHub Actions workflow, and pretending otherwise is how this story would
lie about completion. **The evidence is execution:**

- The **dry run** (AC #8) is the primary artifact. Link the run.
- The **three negative proofs** in Task 10 are what distinguish "the assertions exist" from "the assertions
  work". Every one of R5's failure modes is a *green pipeline shipping something wrong*; an assertion nobody
  has seen go red is an assertion you have not tested.
- The **space-in-path extraction test** (Task 6) is the only exercise of R3-1 anywhere in the epic.
- `dotnet test` + `npm run check` remain the non-regression floor, not the proof.

### Latest technical information (verified 2026-08-07)

- **`NuGet/login@v1`** exchanges the Actions OIDC token for a short-lived nuget.org API key, exposed as
  `steps.<id>.outputs.NUGET_API_KEY`; the job needs `permissions: id-token: write`. The `user:` input is the
  nuget.org **profile name**. Key life: **one hour, single-use** — which is why ADR 0040 § 3 places the
  exchange immediately before the push and warns that a failed push **consumes** it.
- **Action versions:** stay on `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/setup-node@v4` —
  the versions `build-test-analyze.yml` already pins. Consistency across the two workflows is worth more here
  than a major-version bump this story has no reason to take.
- **npm Trusted Publishing** (for 16.8, not this story) needs npm ≥ 11.5.1 **and** Node ≥ 22.14.0, publishes
  provenance by default, and requires that `NODE_AUTH_TOKEN` is **not** set. `web/.nvmrc` pins 24.11.1, which
  satisfies it.
- ADR 0040 was authored **today** (2026-08-07) and carries the current citations for NuGet Trusted Publishing,
  npm trusted publishers, and the Azure DevOps global-PAT retirement. Its § References is the research; this
  story does not repeat it.

### Previous story intelligence — what 16.1, 16.2 and 16.3 actually leave you

| from | what it means here |
|---|---|
| **16.1** (spike, `in-progress`, **9 open owner decisions**) | ADR 0040 + the measured proof that the packaging shape works. Its open items **11, 13, 15, 16** are all yours or block you (R1, R3, AC #9). |
| **16.2** (`review`) | The gate exists, is required on `main` (ruleset `20567252`), and was **verified empirically against PR #7** in both directions. `npm ci` is repaired by `0b1f561`. The gate-lookup query shape is written down — use it. |
| **16.3** (`review`) | MinVer, the pack item, both MSBuild guards, `docs/Packaging.md`, and `--version`/`-v` now working. **Two ADR-assigned mitigations missing (R3).** Its own § 8 says: do not push a tag until 16.4 lands; confirm Trusted Publishing before 16.4 starts. |

**The lesson 16.3 paid for, which applies directly to your archive assertion:** a wrong `PackagePath` produced
**203 files, the right byte total, exit 0, and no entry point.** A size-or-count check certifies nothing.
Assert the **path**.

### Git intelligence

Baseline `15336f4`; recent history is merges of `worktree-code-review-*` branches, so **commits routinely
bundle several stories** — which is exactly why ADR 0040 § 6 rejects generated release notes in favour of a
hand-authored changelog. Do not reintroduce generated notes as a convenience.

The repository has **zero tags** and its remote is `https://github.com/IntegerMan/SpecScribe.git`. `gh` is
installed but **not on `PATH`** — invoke it as `C:\Program Files\GitHub CLI\gh.exe` locally; inside Actions it
is on `PATH` normally.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 16.4: Tag-Triggered Release Pipeline] — AC #1, #2 verbatim
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#Decision 1] — packaging shape; the completeness assertion (discharged by 16.3)
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#Decision 2] — preview cut order, RID matrix, asset naming, SHA-256 digests
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#Decision 3] — credential posture, exchange placement, `NUGET_API_KEY` fallback
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#Decision 5] — versioning, MinVer, the single-archive obligation
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#Decision 6] — changelog into the release body; empty section is not an error
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#Decision 7] — NFR9's weaker reading; `SOURCE_DATE_EPOCH` from the tagged commit
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#Decision 9] — ⚠️ OPEN: the gate mechanism
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#Decision 10] — ⚠️ OPEN: release atomicity; AC #2 unachievable
- [Source: docs/adrs/0040-release-channels-and-versioning-policy.md#Decision 13] — no code signing; digests are the compensating control
- [Source: docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md] — `build:package` vs `build`; the artefact is what ships
- [Source: docs/Packaging.md#Build order] · [#The packaging shape] · [#Versioning] · [#Two traps that produce a green build and a broken package] · [#Verifying a package] · [#What this does not cover]
- [Source: docs/CiGate.md#How a release tag inherits this gate] — the gate-lookup query, and the job-vs-run trap
- [Source: _bmad-output/implementation-artifacts/16-1-spike-report.md#10. Open items] — items 11, 13, 15, 16
- [Source: _bmad-output/implementation-artifacts/16-1-spike-report.md#8. Owner actions] — items 1–6
- [Source: _bmad-output/implementation-artifacts/16-3-cli-packaging-and-publication.md#R11] — nothing is published by 16.3; that is this story
- [Source: src/SpecScribe/SpecScribe.csproj:36-38] — the `SOURCE_DATE_EPOCH` regex gate and its today's-date fallback
- [Source: src/SpecScribe/SpecScribe.csproj:63-67] — the three MinVer properties
- [Source: src/SpecScribe/SpecScribe.csproj:143-168] — `AssertRendererPacked`, `AssertRendererAvailableForPublish`
- [Source: src/SpecScribe/NuxtPrerender.cs:81-133] — `ResolveArtefactDirectory`; no version stamp (R3-2)
- [Source: src/SpecScribe/NuxtPrerender.cs:345] — the single-string `ProcessStartInfo` overload (R3-1)
- [Source: .github/workflows/build-test-analyze.yml:20-38] — trigger scope and the concurrency-group rule
- [Source: .github/rulesets/main-required-checks.json] — the required context, `build-test-analyze`
- [Source: CLAUDE.md#Concurrent work on shared main] · [#Decision records] · [#Verification]

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
