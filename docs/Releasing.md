# Releasing SpecScribe

How to cut a release, how to rehearse one without spending a version number, and what to do when a publish
fails partway through.

**Authority:** [ADR 0040](adrs/0040-release-channels-and-versioning-policy.md) decides the channels, the cut
order, the credential posture, the versioning scheme, the CI gate and the atomicity policy. This document is
the operational companion — it records *how*, not *whether*. Written by Story 16.4.

**Companion:** [docs/Packaging.md](Packaging.md) covers producing and verifying a package **locally**, and
explicitly defers publishing to here. [docs/CiGate.md](CiGate.md) covers the build+test gate this pipeline
depends on.

---

## The one thing to understand before you cut anything

**A version number is consumed the moment it reaches any registry, and it can never be reused.**

nuget.org rejects a duplicate version and permits only *unlisting*, never deletion. npm rejects publishing over
an existing version. So a multi-channel release is **not transactional**, and "re-run the failed release" is not
a recovery — it is a request the registry will refuse.

Everything below follows from that. Recovery is **forward**: bump `-preview.N`, cut a new tag
([ADR 0040 §Decision 10](adrs/0040-release-channels-and-versioning-policy.md)). `-preview` counters will have
gaps, and a reader who sees `preview.1` followed by `preview.3` is looking at a consumed number, not a mistake.

---

## Cutting a release

### 0. Preconditions (owner actions, once)

| | what | why |
|---|---|---|
| 1 | **Reserve `SpecScribe` on nuget.org** | The only release prerequisite a third party can take away. Verified unclaimed 2026-08-07. An implementer may **not** silently substitute the `SpecScribe.Cli` fallback — that is an escalation and an ADR amendment (§Decision 12). |
| 2 | **Configure Trusted Publishing on nuget.org** | See § Configuration the owner mirrors, below. |
| 3 | **Set the `NUGET_USER` repository variable** | Your nuget.org **profile name**, not your email. The pipeline refuses to publish without it, and it checks *before* building. |

### 1. Rehearse first — always

```sh
gh workflow run release.yml --ref main -f dry_run=true
```

`dry_run` **defaults to `true`**, so an accidental manual invocation cannot publish. The dry run executes every
step except the two publish actions and attaches all four artifacts — the nupkg and the three archives — plus
the composed release body to the run, where they can be inspected.

To rehearse an actual tag (recommended before the real cut):

```sh
gh workflow run release.yml -f dry_run=true -f ref=v0.1.0-preview.1
```

Rehearsing a tag also exercises the CI gate check and the nuget.org version preflight, which a branch dry run
skips by construction.

### 2. Cut the tag

Tags are **`v`-prefixed**; the package version is not. `v0.1.0-preview.1` produces
`SpecScribe.0.1.0-preview.1.nupkg` and `specscribe-0.1.0-preview.1-win-x64.zip`.

```sh
git switch main && git pull
git tag v0.1.0-preview.1
git push origin v0.1.0-preview.1
```

**Tags are cut from `main` only.** The preview is forward-fix only (ADR 0040 §Decision 2, §Decision 9): there
are no release branches and no hotfixes to a published preview. A defect is fixed on `main` and ships as the
next `-preview.N`. This is not an oversight to work around — `build-test-analyze.yml` builds `main` only, so a
tag anywhere else has **no run to point at** and the gate check will refuse it with exactly that message.

Pushing the tag starts the pipeline. Nothing else is required.

---

## What the pipeline actually does, in order

Each step exists because of a specific way this release could ship something wrong while reporting success.

| # | step | if it were missing |
|---|---|---|
| 0 | **`selftest`** — every release assertion is driven red and green from fixtures | A guard that has rotted into a no-op is indistinguishable from a guard that passes |
| 1 | **Resolve the ref, refuse to publish from a non-tag** | A dispatch against a branch would push a height-suffixed version and consume it forever |
| 2 | **Assert `SOURCE_DATE_EPOCH`** | `SpecScribe.csproj:36-38` silently stamps *today* on a bad value; the pipeline stays green and the artefact is irreproducible |
| 3 | **Resolve the version from MinVer and cross-check it against the tag** | A shallow clone produces a **wrong version rather than an error**; this is what turns that into a failure |
| 4 | **Require the tagged commit to be green on `main`** | NFR9's gating clause. See below |
| 5 | **Refuse a version already on nuget.org** | Otherwise the conflict surfaces as a 409 at the push step, after three ~76 MiB RIDs and a drafted Release |
| 6 | **Require `NUGET_USER`** | A missing publisher identity would otherwise fail at the credential exchange, with everything already built |
| 7 | **Pack + three self-contained RIDs**, each built **and executed** on its own OS | Story 16.3 proved the mechanism on the host RID only; a platform you did not run on is a platform you are guessing about |
| 8 | **Assert the renderer inside each archive, by path** | A wrong-path payload gives the same file count and the same byte total with no entry point (`docs/Packaging.md § Trap 2`) |
| 9 | **Extract to a path with a space and run `generate`** | A consumer unzips to `C:\Program Files\…`; this is the only exercise of that path in the epic |
| 10 | **Draft the GitHub Release** | The reversible step, deliberately first |
| 11 | **Exchange OIDC → nuget.org key, push** | Irreversible, and bracketed by the draft |
| 12 | **Flip the draft to published** | Last, so a failure leaves a deletable draft rather than an announced release pointing at nothing |

### How publishing is gated on build + test (NFR9)

The release job does **not** re-run the suite. ADR 0040 §Decision 9 satisfies NFR9 by requiring the tagged
commit to **already be green on `main`** — re-running invites a different result from the same source and
doubles the wall-clock of every release, and epics.md § Story 16.2 (AMENDED 2026-07-25) forbids creating a
second build+test workflow.

The preflight queries the **check runs for the tagged commit SHA**, filtered to the check named
`build-test-analyze` — the job name, verbatim.

> ⚠️ **The check-runs API is used rather than the workflow-runs API on purpose.**
> `portability-probe` carries job-level `continue-on-error`, so a workflow **run** can conclude `success`
> while a job inside it is red. `docs/CiGate.md` documents that trap and works around it with a second call.
> Check runs are already per-job, so filtering by name cannot express the trap at all.

| state | what happens |
|---|---|
| latest completed run is `success` | pass |
| `queued` / `in_progress` | poll every 30 s, up to 15 min, then fail — a tag pushed right after a merge races the gate, so waiting is the normal path |
| `failure` / `cancelled` / `timed_out` | fail |
| a later re-run went red | fail — the most recent completed run is authoritative, and red supersedes green, never the reverse |
| no run found | fail: *"tag a commit that has been merged to `main`"* |

---

## Configuration the owner mirrors on nuget.org

This is configuration, not an implementation detail. Trusted Publishing binds a policy to
**repo owner + repo + workflow filename + optional environment**, and the repository cannot detect a mismatch.

| setting | value | consequence of changing it |
|---|---|---|
| repository | `IntegerMan/SpecScribe` | — |
| **workflow filename** | **`release.yml`** | 🚨 **Renaming the workflow file silently invalidates the policy.** The failure surfaces as a rejected push at the very last step, after the version has already been consumed by any channel that went first. |
| environment | **none** | If you set one on the nuget.org policy, the release job **must** declare a matching `environment:` or the exchange fails even though repo, owner and filename all match. |
| `NUGET_USER` (Actions variable) | your nuget.org **profile name** | Not your email. Not a secret — it is an identity, so it is a variable. |

**No secret is stored for any shipping channel.** nuget.org uses a one-hour, single-use key obtained by
exchanging the Actions OIDC token; GitHub Releases uses the run's own `GITHUB_TOKEN`. ADR 0040 §Decision 3's
*"stores nothing"* claim is structural here rather than a matter of discipline.

The credential exchange runs **immediately before the push**, never at job start — the key lives one hour and
is **single-use**, and the job builds three ~76 MiB RIDs plus a Nuxt artefact ahead of it. A push that fails
**consumes the key**, so the supported retry is re-running the job, which re-runs the exchange.

---

## When a release fails

**Do not re-run the tag.** Read the failure first: *where* it failed determines what, if anything, is spent.

| failed at | what is spent | what to do |
|---|---|---|
| selftest, preflight, pack, a RID, an archive assertion | **nothing** | Fix it on `main`. The same tag can be re-pushed only if it was never pushed to a registry — in practice, delete the tag, fix, re-tag. |
| creating the draft Release | nothing | Delete the draft, re-run. |
| **the nuget.org push** | possibly the version | Check nuget.org. If the version is listed, it is **consumed**. |
| flipping the draft to published | **the version** | The packages are live. Finish by hand: `gh release edit <tag> --draft=false`. |

### Recovery is forward

```sh
git tag v0.1.0-preview.2      # NOT a retry of preview.1
git push origin v0.1.0-preview.2
```

Per-channel resume is deliberately **not** implemented (ADR 0040 §Decision 10 point 1): it would require the
pipeline to distinguish *"this version is on this channel because I put it there"* from *"…because someone else
did"*, across registries with different conflict semantics, and would still leave the artefacts unequal across
channels.

### Withdrawing a bad preview that is already published

The pipeline cannot do this. A human does, and **deletion is the wrong instinct** on both registries.

1. **nuget.org: unlist. Never delete.** Deletion breaks package restore for anyone who already resolved it.
2. **npm** (once Story 16.8 adds the channel): `npm deprecate` naming the superseding version. Never
   `npm unpublish` — same reason, and the window is time-limited anyway.
3. **GitHub Release: delete it**, and its assets.
4. **`CHANGELOG.md`: keep the entry**, marked `[X.Y.Z] — WITHDRAWN`, naming what superseded it. The version is
   gone from the registries but its number is permanently spent, and a reader who finds a stale reference to it
   deserves an explanation.

---

## Known gaps at the time of writing

Stated here rather than discovered later.

- **The artefact carries no version stamp.** ADR 0040 §Decision 5 mitigated the self-contained channel's
  two-filesystem-objects problem with *two* controls: a version stamp on the renderer artefact (assigned to
  **Story 16.3**, not delivered) and one-archive-per-RID (this pipeline). Only the second exists. So the
  single-archive rule is currently the **sole** control preventing a desynchronized CLI/renderer pair — never
  attach the executable and the renderer as separate release assets, even briefly, even for testing.
- **`CHANGELOG.md` does not exist yet.** The release body falls back to *"No user-visible changes in this
  release."* and continues; it never hard-fails at that step, because by then the packages are published and
  the version is burned. **Story 16.6** owns authoring the file and the `changelog.d/` assembler
  (ADR 0040 §Decision 6); this pipeline only reads the assembled result.
- **npm / npx and the VSIX are not in this pipeline.** Stories 16.8 and 16.5. When 16.8 extends this workflow
  it must honour §Decision 5's normative publish order: `specscribe-renderer` **first**, then the wrapper.
- **`linux-arm64` and `osx-x64` are not built.** Named and deferred (§Decision 2). They are not unsupported
  platforms — the `dotnet tool` channel is platform-neutral and covers them.
- **Nothing is code-signed.** Declined for the preview (§Decision 13). SmartScreen warns on Windows and
  Gatekeeper blocks on macOS until cleared. The published SHA-256 digests are the compensating control, which
  is why they are not optional politeness.
- **ADR 0040 is `Proposed`, not `Accepted`.** Ratification is an outstanding owner action.

## Verifying a release as a consumer

```sh
# 1. The digests in the release body are reproducible against the download
sha256sum specscribe-0.1.0-preview.1-linux-x64.tar.gz     # Linux
shasum -a 256 specscribe-0.1.0-preview.1-osx-arm64.tar.gz # macOS
Get-FileHash specscribe-0.1.0-preview.1-win-x64.zip       # Windows PowerShell

# 2. The archive carries its renderer
tar -tzf specscribe-0.1.0-preview.1-linux-x64.tar.gz | grep 'renderer/server/index.mjs'

# 3. The global tool channel
dotnet tool install --global SpecScribe --version 0.1.0-preview.1
specscribe --version
```

Extract the **whole** archive and keep the executable and its `renderer/` directory together. Mixing halves
from two releases fails as **wrong output rather than as an error**.
