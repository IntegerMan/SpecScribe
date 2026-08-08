# ADR 0040 — Release Channels, Packaging Shape, Credential Posture and Versioning Policy

- **Status:** **Accepted** — ratified by the owner 2026-08-08, at the Story 16.1 code review.
  - ✅ **Story 16.1 AC #4 is satisfied by this line.** The record was authored 2026-08-07, extended the same
    day with the eight decisions its first code review left open, amended 2026-08-08 (§ Decision 1), and
    ratified. Stories 16.2–16.9 and 17.4 build on it; two of them (16.2, 16.3) had already shipped against it
    while it stood `Proposed`, which is what made ratification the highest-urgency owner action.
  - ✅ **The eight technical decisions left open by the Story 16.1 code review (2026-08-07) are resolved in
    this revision** — MinVer bootstrap (§ Decision 5), version-component semantics and the `0.x` exit
    criterion (§ Decision 5), extension versioning (§ Decision 5), changelog contention (§ Decision 6), the
    CI-gate lookup rule and hotfix scope (§ Decision 9), release atomicity and withdrawal (§ Decision 10),
    the `EpicsIndexSurface` gate's ownership (§ Decision 11), and the package-ID escalation rule
    (§ Decision 12).
  - ⚠️ **This record is `Accepted` but it amends a record that is still `Proposed`.**
    [ADR 0022](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) has stood at `Proposed` since
    2026-07-27 with its own ratification outstanding. Ratifying this ADR does **not** ratify that one, and the
    dependency is disclosed here rather than left for a reader to discover: **ADR 0022's ratification is still
    open.** Story 16.3's own record proposed it; it is the next release-chain ratification to make.
  - ⚠️ **One condition remains open inside this record**, and it is a credential question rather than a
    decision: § Decision 3 provides that if Trusted Publishing is unavailable on the owner's nuget.org
    account, the NuGet channel falls back to a classic API key — which weakens § Decision 3's headline.
    **Confirm which path applies before Story 16.4 begins.**
- **Date:** 2026-08-07 (ratified 2026-08-08)
- **Deciders:** Matthew-Hope Eland (owner) — **ratified 2026-08-08**
- **Authored by:** [Story 16.1](../../_bmad-output/implementation-artifacts/16-1-release-and-distribution-packaging-spike.md) (release & distribution packaging spike)
- **Evidence:** [16-1-spike-report.md](../../_bmad-output/implementation-artifacts/16-1-spike-report.md)
- **Amends:** [ADR 0006](0006-delivery-architecture-and-distribution.md) §Decision (channel list — adds the packaging shape and an ordered preview cut) and [ADR 0022](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) §Decision 5 (Node-check *placement*; and closes **one** of its two open owner questions — see § Relationship to ADR 0022)
- **Governs:** Stories 16.2, 16.3, 16.4, 16.5, 16.6, 16.7, 16.8, 16.9, 17.4

## Context

ADR 0006 chose the distribution channels. ADR 0022 decided that the prebuilt Nuxt `.output/` artefact is what
ships and that Node is a generate-time runtime. Neither said **how the artefact physically gets inside a
package**, and nothing populated it: `NuxtPrerender.ResolveArtefactDirectory` probes `renderer/` beside the
executing assembly and its own doc comment calls that *"the Epic 16 packaging shape"*
(`src/SpecScribe/NuxtPrerender.cs:68`), while `SpecScribe.csproj` packs no such payload. Story 16.9's epics.md
entry states the consequence precisely: until the renderer ships inside the package, an external consumer must
vendor this entire repository.

Two further things had drifted since the epic was planned. The credential posture for two of three channels no
longer involves a stored secret at all (Trusted Publishing). And the third channel's credential path — an
Azure DevOps global PAT — **stopped being issuable on 2026-03-15**, which is not a future deadline but a past
one.

Story 16.1 was run as a decision-first, timeboxed spike. Its load-bearing question was answered
**empirically**, because the failure mode of getting it wrong is a published tool that resolves no renderer
and tells its user to build a Nuxt artefact.

## Options considered — how the renderer rides inside a package

| | option | verdict |
|---|---|---|
| **A** | **Packed as content at `tools/<tfm>/any/renderer/`; a sibling `renderer/` beside the binary; one shared npm package** | **CHOSEN** — measured working on two channels (spike report § 2) |
| **B** | Embed the artefact as `<EmbeddedResource>`, as the seven existing assets are | **Impossible.** The artefact is 187 files Node must `import` from disk. Embedding solves a different problem (single files the C# code writes out); it cannot serve an ESM module graph. |
| **C** | Download the artefact on first run from GitHub Releases | **Rejected.** Introduces a network dependency into a tool whose codebase makes zero outbound calls, breaks air-gapped and offline use, and creates a supply-chain surface for a 1.18 MB saving. |
| **D** | Keep `SPECSCRIBE_RENDERER_DIR` as the only path; document it | **Rejected.** This is the status quo, and it is the defect: `README.md:132-141` currently tells external users to point at *SpecScribe's own clone*. |
| **E** | Publish the renderer as a separate package on every channel and resolve it at runtime | **Rejected for NuGet/binary** (re-creates the version-mismatch class Story 16.9 AC #2 exists to prevent), **adopted for npm only**, where the packaging model already separates platform payloads and the artefact is platform-neutral. |

## Decision

### 1. The renderer artefact ships **inside** the package, as content

- **NuGet `dotnet` global tool:** packed at `tools/<tfm>/any/renderer/**`. **Measured:** it lands beside the
  real assembly in the tool store, `AppContext.BaseDirectory` resolves to that directory, and a `generate`
  run from a foreign repository with `SPECSCRIBE_RENDERER_DIR` unset completes at **`errors=0`**, 373 routes
  at 4.9 ms/route. Cost: **+1,241,709 bytes (+49.4%)** on the nupkg for a 3.96 MB / 187-file payload.
- **Self-contained binary:** a sibling `renderer/` directory beside the executable. **Measured:**
  `PublishSingleFile` does **not** move `AppContext.BaseDirectory` into an extraction directory, so the
  sibling resolves. `errors=0`, 373 routes at 5.0 ms/route.
- **npm / npx:** the artefact is **one shared, platform-neutral `specscribe-renderer` package** — never
  duplicated per RID. It contains **zero native bindings**, so per-RID duplication would multiply 1.10 MB
  (gzipped) across the matrix for no benefit.

**The pack item's exact form is normative, because a wrong one succeeds silently.**

> ⚠️ **AMENDED 2026-08-08 (code review of Story 16.1, owner-decided).** This decision originally prescribed a
> `<None … Pack="true" PackagePath="tools\$(TargetFramework)\any\renderer" />` item and instructed Story 16.3
> to implement it *verbatim*. **Story 16.3 measured that prescription and found it wrong**, shipping a single
> `Content` item instead. The measurement is recorded below and in `SpecScribe.csproj`. The amendment brings
> this record into line with what ships; the original form is preserved here only as the rejected alternative,
> because a reader who meets it in the commit history must be able to see why it is not what to implement.

**ONE item serves BOTH channels.** This is a measured result, not a simplification. `PackAsTool` assembles
`tools/<tfm>/any/` **from the publish output**, so the publish-time copy populates the nupkg as well — the
separate `None`/`PackagePath` item the original decision demanded is redundant on this project. Verified four
ways at `0.1.0-preview.0.410`, each a full pack plus `unzip -l`:

| configuration | result |
|---|---|
| both items | 203 files, entry point present |
| `Content` only | 203 files, **byte-identical**, entry point present |
| `None` only | 203 files, entry point present |
| `None` in a **wrong** form + `Content` | 203 files, entry point present, **no doubled tree** |

That last row is the finding, and it is why the `None` item is not merely redundant but harmful: **with the
`Content` item present, a broken `PackagePath` on the `None` item is invisible.** Shipping both means shipping
one item that looks load-bearing, is not, and silently absorbs its own defects. Dead configuration a reader
would trust is worse than no configuration.

The normative item, which Story 16.3 ships:

```xml
<Content Include="..\..\web\.output\**\*" Pack="false"
         Link="renderer\%(RecursiveDir)%(Filename)%(Extension)"
         CopyToOutputDirectory="Never" CopyToPublishDirectory="PreserveNewest" />
```

Each attribute is load-bearing:

- **`Pack="false"` is REQUIRED, and does not mean "do not pack this".** `Content` defaults to packable, and a
  packable `Content` item lands in `contentFiles/` — a second ~190-file copy nothing ever reads, doubling the
  package while the `tools/` copy carries on working. The `tools/` copy arrives via **publish**, not via this flag.
- **`Link` controls the destination shape**, and `%(RecursiveDir)` **belongs here**. (The original decision's
  warning against `%(RecursiveDir)` was correct about `PackagePath`, where it double-applies; it does not
  transfer to `Link`, where it is what preserves the source tree.) Get `Link` wrong and the payload publishes
  to a flat or mis-rooted directory that resolution cannot find.
- **`CopyToOutputDirectory="Never"`** keeps the inner loop fast — a local `dotnet build` must not copy ~190
  files beside the assembly, and the developer path (candidate 3, `web/.output` in the repo) already serves it.
- **`CopyToPublishDirectory="PreserveNewest"`** is the half that actually delivers, for both channels.

**The TFM must be derived, never hard-coded.** The project sets `<RollForward>Major</RollForward>`, so a TFM
bump is anticipated. Under the shipped shape the derivation moved from a `PackagePath` to the assertion's
expected path — `tools/$(TargetFramework)/any/renderer/server/index.mjs`. With a literal `net10.0` anywhere in
that chain, a bump relocates the assembly to `tools/net11.0/any/` while the check still looks at
`tools/net10.0/`, and **the guard stops guarding while every packaged consumer breaks with a green pipeline**.

**A packaging-time completeness assertion is REQUIRED, not optional — and Story 16.3 has shipped it.** The
spike measured the exact false pass it exists to catch: a wrong packed path produced **187 entries, the right
file count, the right total bytes and exit 0 — with `renderer/server/index.mjs` absent** (spike report § 2.7
finding 1). A size-and-count check therefore certifies nothing. The assertion must test that the **entry point
exists at its packed path inside the produced package**.

This now lives in `SpecScribe.csproj` as the `AssertRendererPacked` target — `AfterTargets="Pack"`, gated
`Condition="'$(PackAsTool)' == 'true'"`, unzipping the produced nupkg with MSBuild's built-in `Unzip` task and
erroring unless the entry point is present. It sits at the **build** layer rather than in the release job,
which is strictly better: a broken package cannot be produced at all, rather than being caught after the fact
by one pipeline. **Story 16.4 inherits this guarantee and must not duplicate it.**

🔴 **The binary channel has NO equivalent guarantee, and that is Story 16.4's to close** *(gap found by the
Story 16.1 second code review, 2026-08-08)*. Neither shipped target covers it:

- `AssertRendererPacked` is gated `Condition="'$(PackAsTool)' == 'true'"` — **nupkg only**.
- `AssertRendererAvailableForPublish` tests `$(SpecScribeRendererEntryPoint)`, which is the **source** path
  `web/.output/server/index.mjs`. That proves an artefact existed to copy; it proves nothing about the
  publish output, and nothing at all about the **archive**.

So the one channel this ADR itself calls *"two filesystem objects, not one artefact"* (§ Decision 5) is the
one with no output-side check — and its consumer symptom is the worst of the three: a SmartScreen-warned
manual download that installs and then fails every `generate`. **Story 16.4 must assert, on the produced
archive for each RID, that `renderer/server/index.mjs` is present** — against the archive, not the publish
directory and not the source, for the same reason `AssertRendererPacked` inspects the nupkg rather than the
file count. See § Decision 5 for the second control this channel needs, the version stamp.

Related: SpecScribe reported *"the renderer answered HTTP 500"* and discarded the renderer's own error text,
so an incomplete payload surfaced as an unexplained failure. **Story 16.3 has since shipped this too** —
`NuxtPrerender.DescribeRouteFailure` now carries the renderer's own message.

**No `SPECSCRIBE_RENDERER_DIR` is required by any packaged consumer.** The variable remains the explicit
override and keeps its hard-fail-on-miss semantics (`NuxtPrerender.cs:80-98`).

⚠️ **Consequence for Story 16.3 — the artefact path is now consumer-chosen, and the spawn is not quoted.**
`NuxtPrerender` launches the renderer through the single-string `ProcessStartInfo(fileName, arguments)`
overload rather than `ArgumentList` (`NuxtPrerender.RenderRoutesAsync`, the
`new ProcessStartInfo(NodeExecutable(), Path.Combine(_artefactDir, "server", "index.mjs"))` call — cited as
`:251` when this record was written, which at HEAD is the unrelated `node --version` probe; **cite it by
symbol, and note it is still unfixed as of 2026-08-08**). Until this decision, `_artefactDir`
was a developer's repo path or an explicit env var; it is now `AppContext.BaseDirectory + "renderer"` — a
**path the consumer chooses at install time**. Any space or non-ASCII character in it (`C:\Users\Matt Eland\…`,
`C:\Program Files\SpecScribe\`) truncates the script argument and breaks the first run of the channel leading
the preview cut. **Story 16.3 must move this call to `ArgumentList`.** The spike's probe path contained no
spaces, so this was not exercised.

### 2. The preview cut, in order

1. NuGet `dotnet` global tool
2. npx / npm wrapper
3. Self-contained per-OS binaries — RID matrix **`win-x64`, `linux-x64`, `osx-arm64`**
4. **VSIX / VS Marketplace is OUT of the first preview** (§ Decision 4)

**Explicit non-goals:** stable/1.0 · Homebrew · winget · Chocolatey · Scoop · a container image · Open VSX ·
code signing · byte-identical reproducible builds · publishing from any CI other than GitHub Actions ·
`linux-arm64` and `osx-x64` (named and deferred, cheap to add later because the renderer is shared) ·
**release branches and hotfixes to an already-published preview** — the preview is **forward-fix only**, all
tags are cut from `main`, and a defect ships as the next `-preview.N` (§ Decision 5, § Decision 9).

⚠️ **This non-goal changes a merged story's acceptance criterion, and that is recorded rather than absorbed**
*(Story 16.1 second code review, 2026-08-08; owner-decided)*. `epics.md` § Story 16.2 AC #1 required the gate
be a required check for *"release-relevant branches"*, and `build-test-analyze.yml`'s header repeats
*"release-branch coverage"* as 16.2's job — but Story 16.2 shipped `main`-only triggers and has already
merged. Under § Decision 9 (amended 2026-08-08) **Stage A is the only tagger and runs only on `main`**, so a
release branch has no path to a release at all; release-branch coverage would describe a capability the
pipeline cannot use. The owner's decision is that **the non-goal stands and the AC is deferred, not deleted**:
release-branch coverage moves past the preview and is seated on **Story 16.10**, so the capability is
recoverable rather than lost. Landed in `epics.md` (§ Story 16.2's AC, § Story 16.10) **and**
`sprint-status.yaml` in the same change, per CLAUDE.md § Decision records.

**Supported-platform matrix, and what an unsupported platform gets.** The three RIDs above are the *binary*
matrix only. The `dotnet` global tool is **platform-neutral** and remains available everywhere .NET 10 runs,
including `linux-arm64` and `osx-x64` — so a deferred RID is a deferred *convenience*, not an unsupported
platform, and Story 16.6 must say so. Story 16.8's `optionalDependencies` wrapper **must emit an explicit,
actionable message when no platform package matches**, naming the `dotnet tool` channel as the fallback;
npm's default behaviour is an opaque missing-binary error at run time.

⚠️ **"No platform package matched" has two causes and they need different messages** *(added 2026-08-08,
Story 16.1 second code review)*. The wrapper must distinguish them, because they are distinguishable at
runtime and the advice diverges:

| cause | how to tell | what to say |
|---|---|---|
| the platform genuinely has no package (`linux-arm64`, `osx-x64`) | `process.platform`/`arch` is outside the supported matrix | the deferred-RID message: use the platform-neutral `dotnet tool` channel |
| **optional dependencies were skipped** (`npm ci --omit=optional`, `npm install --no-optional`) | the platform **is** in the matrix, but no package resolved | say so, and say the fix: reinstall without `--omit=optional`. **Do not** tell a supported platform it is unsupported |

The second is not exotic — `--omit=optional` is a common CI-hardening default, so the likeliest reader of
this message is on a fully supported platform being told to abandon the channel.

**Channel parity is NOT promised.** The cut is ordered across separate stories (16.3, then 16.8), so a
version resolvable on nuget.org may not exist on npm. **nuget.org is the authoritative channel for "a
released version"** — Story 16.9's Action resolves and echoes against it. A version is not required to exist
on every channel, and no story may assume it does.

**Release assets and integrity (Story 16.4).** Binaries attach to the GitHub Release as
`specscribe-<version>-<rid>.zip` (Windows) / `.tar.gz` (Linux, macOS), each accompanied by a **SHA-256
digest** published in the release body. This is not optional politeness: § Decision 4 declines code signing,
and the direct-download channel is the *only* one without integrity by construction — npm publishes
provenance attestations by default and NuGet packages carry the registry's own guarantees. A consumer
clicking through SmartScreen must have something to verify against.

### 3. Credential posture — all three shipping channels store nothing

| channel | mechanism | stored in the repository |
|---|---|---|
| nuget.org | Trusted Publishing — `NuGet/login@v1`, `id-token: write`, 1-hour single-use key | **nothing** |
| npm | Trusted Publishing — npm CLI ≥ 11.5.1 **and** Node ≥ 22.14.0, `id-token: write`, provenance by default, `NODE_AUTH_TOKEN` must **not** be set | **nothing** |
| **GitHub Releases** (self-contained binaries) | the workflow's own `GITHUB_TOKEN`, with `permissions: contents: write` scoped to the release job | **nothing** — the token is minted per run by Actions, never stored |

The GitHub Releases row exists because that channel **is in the shipping cut** (§ Decision 2) and was
missing from the spike's original three-row inventory. It stores nothing, but "stores nothing" is an answer
that has to be given rather than assumed.

Story 16.1 AC #2's *"no secret value is committed"* is therefore **structural** for all three shipping
channels, not a matter of discipline.

**Where the exchange runs, and what a retry does.** `NuGet/login@v1` must run **immediately before the push
step, not at job start** — the key is valid for one hour and is single-use, while the release job builds
three ~76 MiB self-contained RIDs plus a Nuxt artefact plus the test suite ahead of it. A push that fails
**consumes the key**; the retry must re-run the exchange, and a re-run of the whole job is the supported
recovery path. If the exchange itself fails, the job must **fail before publishing to any channel**, so a
partial release is not created — see § Decision 10.

**Fallback path, with its storage answered.** nuget.org's Trusted Publishing is still a gradual rollout. If
it is unavailable on the owner's account, the NuGet channel falls back to a classic API key stored as the
**repository secret `NUGET_API_KEY`**, scoped to the `release` environment so it is unreachable from PR
workflows, rotated by the owner, and set to nuget.org's shortest offered expiry. Under the fallback the
"stores nothing" claim weakens for that channel only, and this ADR's § Decision 3 headline must be read with
that caveat.

✅ **CONFIRMED 2026-08-08 (owner, at the start of Story 16.4): Trusted Publishing applies. The fallback is NOT
taken, and the "stores nothing" headline above stands unqualified for all three shipping channels.**
`.github/workflows/release.yml` implements the Trusted Publishing path only — there is no `NUGET_API_KEY`
reference anywhere in the repository, and no code path that could consume one. The fallback stays recorded
above because it is the answer if the policy is ever revoked, but taking it would be a change to this decision
rather than a configuration switch.

**Two things this binds that are NOT visible from inside the repository**, recorded here and mirrored in
`docs/Releasing.md` § Configuration the owner mirrors:

1. **The workflow FILENAME is part of the policy.** A trusted-publishing policy binds to
   *repo owner + repo + workflow filename + optional environment*. The filename is fixed as **`release.yml`**
   and renaming it silently invalidates the policy — surfacing as a rejected push at the LAST step of a
   release, after the version has already been consumed by any channel that went first (§ Decision 10).
2. **No `environment:` is declared on the release job**, matching a policy that names none. If the owner ever
   sets an environment on the nuget.org side, the job **must** declare the matching one or the exchange fails
   even though repo, owner and filename all match.

The publisher identity — the owner's nuget.org **profile name**, not their email — is the Actions *variable*
`NUGET_USER`. It is configuration rather than a credential, which is why it is a variable and not a secret,
and the release preflight refuses to proceed without it **before anything is built** rather than discovering
it at the exchange step.

For VS Marketplace (§ Decision 4, out of the preview): Entra workload identity federation stores **no
secret** either — the repository holds the client and tenant IDs as plain Actions *variables*, and the trust
lives in a federated credential on the Entra app registration.

### 4. VS Marketplace: organization-owned publisher + Entra federation, and out of the preview

The PAT path is **closed, not merely dated**. Azure DevOps blocked creation *and regeneration* of global PATs
on **2026-03-15**; `vsce` requires a PAT scoped to *"All accessible organizations"* with Marketplace (Manage),
which **is** the global shape; all remaining global PATs die **2026-12-01**. The VS Code documentation now
directs publishers to Microsoft Entra ID instead.

**Decision:** when Story 16.5 runs, it targets an **organization-owned** publisher using Microsoft Entra
workload identity federation. Personal ownership is rejected up front because `microsoft/vscode-vsce#1023`
reports federated service principals failing publish on a **personally-owned** publisher (closed
`not_planned`), and publisher ownership is effectively irreversible once extensions are published under it.

### 5. Versioning

- **Scheme:** SemVer 2.0, `0.MINOR.PATCH-preview.N`, remaining in `0.x` for the whole preview.
- **Derivation: MinVer**, from the nearest reachable git tag. `<Version>` is **deleted** from
  `SpecScribe.csproj` rather than replaced by a second literal. Chosen over Nerdbank.GitVersioning (needs a
  committed `version.json` — a second home for the version) and over `-p:Version=` from the tag (works only
  in CI; a local `dotnet pack` would then produce `1.0.0` and silently drop the Preview badge).

  ✅ **The two prerequisites the Story 16.1 code review raised (2026-08-07) are now CLOSED, and one of them
  was closed by implementation rather than by decision.** Both would have failed *silently* — `dotnet pack`
  exiting 0 with a wrong version — so recording how each is closed matters more than recording that it is.
  1. **`MinVerTagPrefix` — closed by Story 16.3.** `SpecScribe.csproj` now sets
     `<MinVerTagPrefix>v</MinVerTagPrefix>` explicitly, so this ADR's worked example `v0.1.0-preview.1`
     matches. MinVer's default prefix is empty and would not have.
  2. **Zero git tags — the failure mode is closed; the tag itself is a release-time owner action.** Story
     16.3 also set `<MinVerMinimumMajorMinor>0.1</MinVerMinimumMajorMinor>` and
     `<MinVerDefaultPreReleaseIdentifiers>preview.0</MinVerDefaultPreReleaseIdentifiers>`, so an **untagged**
     build now emits `0.1.0-preview.0.<height>`, still carrying a pre-release label so the About page's
     Preview badge survives. MinVer's undirected `0.0.0-alpha.0.<height>` can no longer be produced.
     ⚠️ **Corrected 2026-08-08:** this previously read *"inside this scheme"*, which the version-component
     table below does not support — that table defines exactly three shapes (`0.N.0`, `0.N.P`,
     `-preview.N`) and `-preview.N.<height>` is a **fourth**. It is a **build identifier, not a releasable
     version**, and item 3 below states the rule the original wording left unanswered: such a build must
     never be promoted, and under § Decision 9 it cannot be. `README.md`'s external-CI recipe no longer pins a literal either: Story 16.3 changed it to
     read the version off the `.nupkg` the pack produced.

     What remains is not a defect but a one-time act: **the first real tag, `v0.1.0-preview.1`, must exist
     before the first release publishes.** Under § Decision 9 (amended 2026-08-08) **Stage A creates it** on
     the first merge to `main` after the release job ships — it is no longer a manual owner action, and
     Story 16.4 owns making Stage A handle the zero-tag case (there is no previous `N` to increment from, so
     the first run must seed `preview.1`).

  3. **Tag height is a non-issue under merge-triggered releasing — and that is a real gain, not a
     coincidence.** *(Recorded 2026-08-08, from a Story 16.1 second-code-review finding.)* MinVer appends
     height to the **nearest tag's own** pre-release identifiers, so under the old human-tagged design, once
     `v0.1.0-preview.1` existed every subsequent untagged build of `main` would have emitted
     `0.1.0-preview.1.<height>` — the *published* version plus a trailing segment this scheme does not define,
     reported by `--version` and by the About page's Build row, and with no rule stating whether such a build
     could publish. `MinVerDefaultPreReleaseIdentifiers=preview.0` would never have applied again, because it
     fires only when the nearest tag is a *release* tag.

     **§ Decision 9 Stage A tags every merge to `main`, so `main`'s head is always at height 0** and every
     buildable release commit carries a clean `0.MINOR.PATCH-preview.N`. A height-suffixed version now occurs
     only on a feature branch or a dirty tree — states that are never tagged and therefore never promotable
     — which is exactly where a "this is not a release" marker belongs. **A height-suffixed version must
     never be promoted**, and Stage B cannot promote one, since it promotes tags and a tag is by definition
     at height 0.

- **What each version component means.** Previously only "minor = breaking inside `0.x`" was defined, which
  left every tag choice after the first to judgement. The full mapping:

  | component | bump it for | example |
  |---|---|---|
  | **MINOR** (`0.N.0`) | any **breaking change** *or* any **new user-visible feature** | a new portal surface; an IR-schema change; a removed CLI flag |
  | **PATCH** (`0.N.P`) | bug fixes, performance, docs, internal refactors — **no** new feature and **no** break | a rendering fix; a corrected chart label |
  | **`-preview.N`** | a **re-cut of the same target version** after a failed or withdrawn release (§ Decision 10) | `v0.1.0-preview.2` after `preview.1` half-published |

  **MINOR deliberately carries two meanings, so MINOR alone does not signal breakage.** That is SemVer's own
  `0.x` convention (§4: anything may change below 1.0) and this policy does not pretend otherwise — which is
  exactly why the `**BREAKING:**` changelog prefix (§ Decision 6) is the load-bearing signal rather than the
  version number. A consumer reads the changelog, not the digits.

  **Exit criterion for `0.x` → `1.0.0`**, so "preview forever" is not the default outcome. All three must
  hold, and Story 17.4's sign-off tests them:

  | # | criterion | how 17.4 tests it |
  |---|---|---|
  | **(a)** | the IR schema is **frozen** — `schemaVersion` is stated, a compatibility rule for changing it is ratified, and the current IR conforms | the ratified record exists and names the frozen version; a generated IR is checked against it |
  | **(b)** | every channel in the preview cut (§ Decision 2) has **published at least one release** | resolve the version on nuget.org and npm, and confirm a GitHub Release with all three RID archives |
  | **(c)** | § Decision 11's *does not promise* list no longer contains **output-, API- or IR-stability** | read the list; the three entries are absent |

  ⚠️ **Criterion (a) was corrected 2026-08-08** *(Story 16.1 second code review)*. It previously read *"the IR
  schema is frozen under **ADR 0008's** versioning"* — but ADR 0008
  (`0008-json-ir-canonical-and-incremental-generation.md`) **defines no versioning or freeze policy at all**;
  its only `schemaVersion` mention is a pointer to Story 22.2 / `SpaDelivery.cs`. The IR-versioning record is
  **ADR 0016**, which is still `Proposed`. So the criterion pointed at a policy that does not exist, and 17.4
  had nothing to test. **Ratifying ADR 0016 (or whatever record ends up owning the freeze) is therefore a
  prerequisite of leaving `0.x`** — that is now the substance of (a), rather than a citation.

  ⚠️ **Criterion (c) is deliberately the weakest of the three, and is not self-satisfying.** As worded it can
  look circular — the test for leaving `0.x` is that a list has already been edited. It is not: **(a) and (b)
  are what earn the edit**, and (c) records that the edit was made honestly rather than as a formality. (c)
  may not be satisfied by editing the list while (a) or (b) is still open.
- **Every preview release carries a SemVer pre-release label.** This is not cosmetic:
  `AboutTemplater.cs:133-135` renders the About page's `Preview` badge from `meta.IsPrerelease`. The first
  release without the label is by definition no longer a preview.
- **The VS Marketplace is the documented exception, and it needs its own counter.** It has no SemVer
  pre-release concept, so pre-release status is carried by the Marketplace's own Preview flag plus
  `vsce publish --pre-release`, and `extension/package.json` holds a plain `0.MINOR.PATCH`.

  ⚠️ **A frozen `0.1.0` would permit exactly one VSIX publish, ever** — the Marketplace requires each publish
  to be **strictly greater** than the last, and the CLI's distinguishing part (`-preview.N`) is precisely
  what the extension cannot carry. Raised by the Story 16.1 code review (2026-08-07); the rule that resolves
  it:

  > **The extension's MINOR mirrors the CLI's MINOR. The extension's PATCH is its own monotonic counter,
  > incremented on every VSIX publish.**

  So CLI `v0.2.0-preview.3` publishes as extension `0.2.0`; a second VSIX cut against the same CLI MINOR
  publishes `0.2.1`. The counter is monotonic by construction, so a re-publish is always possible, and the
  correspondence is legible in both directions — a consumer on extension `0.2.x` knows it targets CLI `0.2.y`.
  The extension's PATCH deliberately does **not** track the CLI's PATCH: the two ship on different cadences
  (§ Decision 2 puts the VSIX out of the first preview entirely), and forcing them to match would reintroduce
  the same frozen-version problem one component down. Story 16.5 implements this.

  **The counter's storage, its behaviour across a MINOR bump, and its withdrawal path** *(added 2026-08-08,
  Story 16.1 second code review — the rule solved the frozen-`0.1.0` problem and left three states
  undefined)*:

  - **Storage: `extension/package.json`'s `version` field is the counter.** There is no separate state file.
    The published Marketplace version is the check, and Story 16.5's publish step must **read the current
    Marketplace version and fail if the manifest is not strictly greater** — because a forgotten increment
    otherwise succeeds through `vsce package` and fails only at the Marketplace, after the build.
  - **Across a MINOR bump the PATCH resets to 0.** CLI MINOR `0.2` → `0.3` publishes extension `0.3.0`, not
    `0.3.8`. *"Monotonic"* admitted both readings; reset is chosen because the correspondence
    (*"extension `0.3.x` targets CLI `0.3.y`"*) is the property the rule exists to give, and a carried-over
    counter makes the PATCH's magnitude meaningless. Strictly-greater is still satisfied, since MINOR rose.
  - **Withdrawal:** § Decision 10 rule 4 names nuget.org, npm and the GitHub Release and **no Marketplace
    action**, because the VSIX is out of the preview cut (§ Decision 4). **Story 16.5 must add one when it
    brings the VSIX in** — and note the counter's monotonicity means a withdrawn version's *number* can never
    be reused, exactly as § Decision 10 requires for the other channels.
- **CLI and renderer are pinned as one released unit.** For **NuGet** this is genuinely structural — there is
  one artefact and the payload is inside it. For the **self-contained binary** it is *not* structural: the
  channel is defined as *"a sibling `renderer/` directory beside the executable"* (§ Decision 1) — **two
  filesystem objects**, which a user can desynchronize by unzipping release N over release N−1 or by
  replacing only the `.exe`. `ResolveArtefactDirectory` tests only that `renderer/server/index.mjs` exists;
  nothing stamps the artefact with a version. **Story 16.4 must therefore ship each RID as a single archive
  containing both halves** (never the exe and the renderer as separate release assets). Without that, this
  channel reproduces exactly the failure Story 16.9 AC #2 exists to prevent — one that *"fails as wrong
  output rather than as an error"*.

  **The version stamp, specified** *(2026-08-08 — the original wording said only "stamp the artefact with the
  CLI version and fail loudly on a mismatch", which named no version source, no comparison granularity and no
  developer-path exemption, and would have hard-failed every local build)*:

  - **Source.** The stamp is written **into the archive at release time by Story 16.4**, not by
    `npm run build:package` — the artefact is built in `web/`, which has no MinVer version and no way to know
    one. Story 16.4 writes `renderer/.specscribe-version` containing the exact promoted version.
  - **Granularity: exact string equality**, and only against a **stamped** artefact.
  - **The developer path is exempt by construction.** An artefact with **no** stamp file is a developer build
    (candidate 3, `web/.output` in the repo) and is accepted with no comparison. Only a *present and
    different* stamp is a mismatch. This is what keeps a local `generate` working on the commit after any
    artefact build — the case exact equality would otherwise break on every commit, since the CLI's version
    moves with every merge under § Decision 9.
  - **Where it is enforced:** `NuxtPrerender.ResolveArtefactDirectory`, beside the existing
    `server/index.mjs` existence test. **Story 16.3 owns the check; Story 16.4 owns writing the stamp** — and
    the check must ship *before or with* the first stamped archive, or it never fires.

  ⚠️ **This channel now has two controls and both must exist**: the archive-completeness assertion (§ Decision
  1) and this stamp. Neither is covered by `AssertRendererPacked`, which is `PackAsTool`-gated and nupkg-only.
- For **npm**, where § Decision 1 makes the renderer a separate package, the wrapper depends on
  `specscribe-renderer` with an **exact** version pin (`=X.Y.Z`, never `^`), published from the same tag in
  the same pipeline run. **Publish order is normative: `specscribe-renderer` FIRST, then the wrapper.** npm
  has no multi-package transaction, so publishing the wrapper first makes `specscribe@X.Y.Z` installable
  while its exact dependency does not yet exist — `npx specscribe` then fails at install for every user,
  with the version already burned (§ Decision 10). If the renderer publish succeeds and the wrapper publish
  fails, the renderer is simply an orphaned version: harmless, and the correct direction for the window to
  fall.

### 6. Changelog

**Keep a Changelog 1.1.0**, `CHANGELOG.md` at the repository root, **hand-authored in the story that makes
the change**. Generated release notes are rejected because this repository's commits routinely bundle several
stories (CLAUDE.md § Concurrent work) — the commit is not the unit of change here, the story is. The release
pipeline copies the released version's section into the GitHub Release body; it does not author it.

**Breaking changes need a marked home, because Keep a Changelog has no `Breaking` section.** The format's
sections are `Added / Changed / Deprecated / Removed / Fixed / Security`, and § Decision 11 promises that
breaking changes *are recorded*. A breaking entry therefore stays in its natural section but is **prefixed
`**BREAKING:**`**, so it is greppable and cannot be mistaken for a routine `Changed` line. Without this the
one guarantee the preview does offer is indistinguishable from the noise around it.

**An empty release — no fragments in `changelog.d/` — is not an error.** A re-cut after a failed publish
(§ Decision 10), a CI-only fix or a dependency bump may legitimately carry no user-visible change. The
promote job then writes *"No user-visible changes in this release."* as the changelog section and continues —
it **must not** hard-fail at the last step, because by then the packages are already published and the
version is burned.

⚠️ **The Release body has two authors, and they compose rather than overwrite** *(specified 2026-08-08 —
the Story 16.1 second code review found two rules writing the same field with no ordering, on the release
most likely to be a re-cut)*. § Decision 2 requires each RID archive's **SHA-256 digest** in the release
body; this decision requires the **changelog section** there. They are written at different times by
different stages, so the rule is:

1. **Stage A** creates the Release body with the digest block. Digests can only be computed once the archives
   exist, and Stage A is what builds them.
2. **Stage B** *appends* the assembled changelog section — or the empty-release sentence — **above** the
   digest block, and **must not replace the body**.

The empty-release sentence is a **changelog section**, never a whole body. Reading it as the latter would
delete the digest block, and § Decision 13 names that digest *"the compensating control"* for the only
channel shipping without a signature — so the mistake would strip the sole integrity guarantee from the
direct-download channel, precisely when a release carried no changelog to distract from it.

✅ **AMENDED by Story 16.4 (2026-08-08): a MISSING `CHANGELOG.md` is the same non-fatal path as an empty
section.** The decision above covered an empty section but not an absent file, and they are different states —
the absent one is the state the repository is *actually* in, because Story 16.6 owns authoring the file and is
scheduled behind 16.4. Leaving it unstated meant the release job's last step could crash on a missing file
*after* the packages were pushed, burning a version over a `readFileSync`.

> **File absent, section absent and section empty are ONE path:** write *"No user-visible changes in this
> release."*, emit a workflow **warning**, and continue. `release-body.mjs` guards every read and cannot throw.

**Story 16.4 deliberately does NOT seed a `CHANGELOG.md` skeleton or a `changelog.d/` directory** (owner
decision, 2026-08-08). Authoring either would take this decision's format choices with it, and those are
16.6's. The invocation seam is named in `release-body.mjs`: when 16.6 lands the assembler, the release job
runs it **before** composing the body and this script keeps reading the assembled `CHANGELOG.md` unchanged.
So 16.4 discharges "owns invoking it" as far as there is anything to invoke, and no further.

**Stories write fragments, not the file. `CHANGELOG.md` is assembled, never hand-merged.** The Story 16.1
code review (2026-08-07) identified the hazard the original decision left open: a single hand-edited file at
the repository root becomes the **highest-contention file in the repository**, in a repository whose CLAUDE.md
records that *"a `Charts.cs` edit has silently vanished this way before"* — and a lost changelog entry is
invisible until it is already missing from a published release body. Rejecting generated notes was right; it
just left the alternative's own failure mode unaddressed.

- A story that makes a user-visible change adds **one new file**, `changelog.d/<story-key>.md` — e.g.
  `changelog.d/16-3-cli-packaging-and-publication.md`.
- A fragment holds Keep a Changelog **section headings and bullets only**, no version header:

  ```markdown
  ### Added
  - The renderer artefact now ships inside the published package.

  ### Changed
  - **BREAKING:** `SPECSCRIBE_RENDERER_DIR` is no longer required by packaged consumers.
  ```

- The **promote** job (Story 16.4, § Decision 9 Stage B) **concatenates the fragments by section**, writes
  them into `CHANGELOG.md` under the promoted version's header, and copies that section into the GitHub
  Release body. **Fragments are consumed at promotion, never at merge** — Stage A tags every merge, and
  consuming fragments there would spend a story's changelog entry on a tag that may never be promoted.
- **The consumption lands as a pull request, not as a push to `main`** *(amended 2026-08-08)*. The promote job
  opens a PR carrying the assembled `CHANGELOG.md` and the fragment deletions; it does **not** push. This is
  not a stylistic preference: the Story 16.1 second code review established that a `GITHUB_TOKEN` push to
  `main` is **rejected outright** by the repository's ruleset — `github-actions[bot]` is an *Integration* and
  the only bypass actor is the admin role — and that even with a bypass such a push **triggers no workflow**,
  so the commit would land unbuilt and fail the next release. A PR goes through the same required check as
  any other change, which is the outcome the branch protection exists to produce.
- **Fragment order within a section is by story key, ascending.** Directory enumeration order differs by
  filesystem and OS, and this repository's entire gate architecture exists to pin byte-level determinism —
  ADR 0033 requires a new generated artifact be *"proven deterministic across machines and CI operating
  systems"*, and `CHANGELOG.md` is now a generated artifact. An explicit sort key is what makes the same tag
  assemble identically on any runner.
- `CHANGELOG.md` remains the published artefact in Keep a Changelog 1.1.0 format, and remains hand-authored
  in substance — the assembly is mechanical, not generative, so the § "generated notes are rejected"
  rationale is untouched. **Story 16.6 owns the format and the assembler; Story 16.4 owns invoking it.**

**Why a directory fixes it:** each story creates a *distinct new file*, so two concurrent stories cannot
conflict and neither can silently overwrite the other. The failure mode becomes a missing file — visible in
`git status` and in review — rather than a vanished line inside a shared one.

**Effective date and backfill (owner-decided 2026-08-08, at the Story 16.1 code review).** The scheme was
adopted with neither, which left a defect the second code review caught: `changelog.d/` and `CHANGELOG.md` do
not exist, Stories 16.2 and 16.3 had already shipped user-visible changes with no fragments, and the
empty-release rule above makes an empty `changelog.d/` **legal and unfailable** — so the **first preview
release of the entire product** would have published *"No user-visible changes in this release."* That is a
worse outcome than the hard-fail the rule exists to avoid, and it would have happened silently.

- **Effective immediately.** Every story that lands a user-visible change from 2026-08-08 onward adds its
  fragment as part of its own work, and the fragment belongs in that story's File List like any other file.
- **The preview's history is backfilled, not lost.** Story 16.6, which owns the format and the assembler,
  **authors fragments retroactively for every user-visible change already shipped toward the first preview**
  — the packaging and CLI work of 16.2 and 16.3 at minimum. Backfilled fragments keep the same
  `changelog.d/<story-key>.md` naming, so they assemble by the identical mechanism and need no special case
  in the assembler. This is the step that makes the first release's notes real.
- **The first release's notes are a release-readiness item, not a release-job outcome.** Story 16.7's cut
  checklist verifies `changelog.d/` is non-empty **before** the tag is pushed, and Story 17.4's sign-off
  reads the assembled section. Checking *before* the tag is the point: the empty-release rule above is
  deliberately unfailable at release time because the version is already burned by then, so an empty first
  release must be prevented upstream rather than caught downstream.

⚠️ **One gap in this scheme is knowingly left open: a fragment that is never authored is invisible.** The
"missing file is visible in `git status`" argument holds for a fragment created and then deleted; it does
**not** hold for one never created, which appears in no `git status`, no File List and no diff, and which the
release job cannot distinguish from a legitimately empty release. No gate is specified here on purpose —
ADR 0033 governs any new gate and requires it to localize failure to a named artifact and be proven
deterministic before pinning, which is design work this record should not pre-empt. **Story 16.6 owns
deciding whether such a gate is warranted**, and until then the control is the pre-tag check above.

### 7. NFR9 reproducibility — the weaker reading is claimed, explicitly

**"Reproducible" means _built from a clean checkout by CI_, not byte-identical rebuilds.** NFR9's own wording
supports this and it is stated so no reader assumes the stronger guarantee.

The preview closes: version-from-tag (16.3), `SOURCE_DATE_EPOCH` set by the release workflow (16.4 — the
csproj already honours it at `SpecScribe.csproj:28,36-37`), and a working `npm ci` (16.2).
It defers: `<Deterministic>` / `ContinuousIntegrationBuild` / SourceLink, and deterministic Nuxt builds.

**`SOURCE_DATE_EPOCH` must be the tagged commit's committer timestamp** — `git log -1 --format=%ct` on the
release ref — **never the workflow run's start time**, which would differ on every re-run and defeat the
property. Specifying the value is load-bearing rather than pedantic: the csproj gates validity on
`^[0-9]{1,10}$` and its fallback is *today's date*, so an unset, misspelled or malformed variable **silently
stamps the build date** and the pipeline stays green (`SpecScribe.csproj:36-38`). Story 16.4 must therefore
**assert the variable is set and well-formed before building**, so a typo fails the release rather than
quietly producing an irreproducible artefact.

**The weak reading was not satisfied when this ADR was written.** `npm ci` failed at `838d591` on a clean
checkout with npm 11.16.0 (`Missing: @emnapi/runtime@1.11.3 from lock file`), and three CI steps depend on
it. ✅ **Resolved 2026-08-07 by commit `0b1f561`** ("CI fix: repair the lockfile and regenerate the two stale
drift gates"), which added the missing top-level `node_modules/@emnapi/runtime` tree entry. Story 16.2 owned
the closure and it is closed; Story 16.4 may build a release pipeline on top of it.

### 8. Node prerequisite — placement, and the npx install-time check

- **The Node check stays where it shipped: at prerender time, in `NuxtPrerender`** (`NuxtPrerender.cs:141-216`).
  **This amends ADR 0022 §Decision 5's "detects Node at startup" wording to match the implementation.**
  Rationale: "at startup" moves a subprocess spawn into every invocation — including `--help` and `--version`
  — to warn about a dependency only the prerender path needs. The status quo pays that cost once, for a user
  about to hit the error anyway.

  **The shipped code disagrees with this, and that disagreement is resolved here rather than ignored.** The
  check's own doc comment describes itself as an interim stand-in: *"ADR 0022 §Decision 5 assigned Node
  DETECTION to Story 16.3, which has not been built … Until it is, this is the check"*
  (`NuxtPrerender.cs:143-145`). This ADR **promotes the stand-in to the permanent answer** — deliberately,
  on the rationale above, not by overlooking the comment. **Story 16.3 must update that doc comment** to
  cite this decision instead of describing itself as temporary; leaving it would strand a comment that tells
  the next reader the opposite of the governing record.
- **No install-time Node check for npx** (closing ADR 0022's open owner question 1). npm invokes npx, so Node
  is present by construction; the real risk is *version*, already covered by `SupportedNodeRange`. The npm
  wrapper declares `engines.node` so npm warns without executing anything. **A postinstall script is
  rejected** — it runs arbitrary code on install, is skipped by `--ignore-scripts`, and would surface a
  failure at install time for a tool that may never be run.
- **Both prerequisites are consumer-facing conditions of use** and must appear where a packaged consumer
  sees them — the NuGet listing, the npm README and the Marketplace listing (Story 16.6). Today they are
  stated only in `README.md:92-98`.
  1. **Node**, within `SupportedNodeRange` (`^22.19.0 || ^24.11.0 || >=26.0.0`).
  2. **.NET 10**, for the `dotnet` global tool channel. This was missing from the spike's promises and is
     added here: it is the **likeliest install blocker for the channel leading the preview cut**, and a
     listing that names only Node under-states what the tool needs. It does not apply to the self-contained
     binaries or to npx, which carry their own runtime.

### 9. Releasing is continuous on `main`, in two stages: automatic tag + GitHub Release, manual promote to the registries

> ⚠️ **AMENDED 2026-08-08 (owner decision, Story 16.1 second code review). This decision previously described
> a HUMAN-TAGGED release with an API lookup to prove the tagged commit was green.** That design was sound but
> its lookup rule carried five branches, two of them unanswerable, and the second code review found the
> mechanism specified two different ways across this record and `docs/CiGate.md`. **The owner chose
> merge-triggered releasing instead, and the lookup problem largely dissolves with it** — the release runs in
> the same pipeline as the tests, so it does not need to ask whether they passed. The superseded design is
> summarised at the end of this section, because a reader arriving from a 2026-08-07 citation needs to know
> it was replaced rather than lost.

NFR9 requires *"publishing … gated on a passing build + test run"*. **This model satisfies it structurally
rather than by query.**

#### Stage A — continuous, automatic, on every push to `main`

`build-test-analyze` runs as it does today. A release job in the **same workflow run** declares
`needs: build-test-analyze`, so it cannot start unless that job concluded `success`. That dependency **is**
the NFR9 gate — there is no check-run query, no polling, no pagination or `filter` default to get wrong, and
no way to release a commit whose tests did not pass. The job then:

1. reads the highest existing `v0.1.*-preview.N` tag and computes **N+1**;
2. **creates and pushes the tag** at the merge commit;
3. builds the nupkg, the npm tarballs and the three RID archives — MinVer now resolves to exactly that tag at
   height 0 (§ Decision 5), so the artefacts carry a clean `0.MINOR.PATCH-preview.N`;
4. publishes a **GitHub Release marked `prerelease`**, carrying the RID archives and their SHA-256 digests
   (§ Decision 2).

**Nothing in Stage A pushes to `main`.** Creating a tag ref is not a branch push, so the
`main` ruleset's `required_status_checks` rule and its admin-only `bypass_actors` do not apply — which is
what makes this model implementable at all with the repository's existing protection. The second code review
found that the previously-designed release commit **could not have been pushed**: `GITHUB_TOKEN` acts as
`github-actions[bot]`, an *Integration*, which is not among the bypass actors, and even with a bypass a
`GITHUB_TOKEN` push triggers no workflow, so the commit would land unbuilt and poison the next release. That
failure mode is now absent by construction rather than credentialed around.

#### Stage B — promote, manual, `workflow_dispatch` with a tag input

Publishing to **nuget.org and npm is irreversible** (§ Decision 10), so it stays a deliberate act. The
promote job's entire preflight is:

- **the tag exists**, and
- **a GitHub Release created by Stage A exists for it.**

That is the whole gate, and it is sufficient *because Stage A only creates a Release when its tests passed*.
The green-ness is inherited from an artefact this pipeline produced, not re-derived from an API whose
defaults lie. Every branch the old rule struggled with is answered by construction: a tag on an unmerged
branch has no Stage A Release and cannot be promoted; a commit that was never a push head has no tag at all;
a re-run that later goes red cannot retroactively create a Release; there is nothing to poll and nothing to
paginate.

Then, in order: credential exchange (§ Decision 3) → registry preflight (§ Decision 10) → renderer package →
wrapper (§ Decision 5) → assemble the changelog section into the Release body (§ Decision 6).

#### What this model costs, stated rather than glossed

- **A tag and a GitHub Release per merge to `main`.** Tags are cheap and Releases are deletable, so the churn
  is real but recoverable — which is exactly why the irreversible half is not in Stage A.
- **A version number is allocated per merge, not per publish.** Numbers climb faster than releases ship. That
  is harmless: `-preview.N` is a counter, not a promise, and § Decision 10's "consumed" rule bites only on
  **promotion**, so an unpromoted tag costs nothing on any registry.
- **`portability-probe` must stay non-required.** It carries `continue-on-error` at the job level; the
  release job depends on **`build-test-analyze` only**, by job name verbatim. Per epics.md § Story 16.2
  (AMENDED 2026-07-25), **do not create a second build+test workflow** — Stage A is a job in the existing one.

#### Superseded design, for readers arriving from a 2026-08-07 citation

The original rule was: a human pushes a tag; the release job queries
`gh api repos/{owner}/{repo}/commits/{sha}/check-runs` filtered to `build-test-analyze`, passes on
`completed`+`success`, polls 30 s up to 15 minutes while in progress, treats the most recent completed run as
authoritative, and fails with an actionable message when no run exists. The second code review found four
defects in it, all now moot: the 15-minute budget was shorter than the workflow's own `timeout-minutes: 30`;
`pull_request` runs produce an identically-named check run, so a green *unmerged* branch satisfied it;
a commit in the middle of a multi-commit push has no check run and got a message telling it to merge to
`main`, which it had; and the endpoint's undeclared `filter=latest` default discards the very run history the
authority rule inspects. `docs/CiGate.md` (Story 16.2) additionally prescribed a *different* query shape for
the same preflight. **Story 16.2 owns reconciling `docs/CiGate.md` to this section.**

**Tags are created only on `main`, now by construction rather than by policy** — Stage A is the only tagger,
and it runs only on `main`. The preview is **forward-fix only**: a defect in a published preview is fixed on
`main` and promoted as the next `0.MINOR.PATCH-preview.N` (§ Decision 5). If a hotfix branch is ever genuinely
required — a `1.0` concern, not a preview one — the prerequisite is explicit and seated:
`build-test-analyze.yml`'s `push` trigger must cover that branch pattern first, and Story 16.2 owns that file.

### 10. Releases are not atomic, and the pipeline is not freely re-runnable

The Story 16.1 code review (2026-08-07) found no policy anywhere for republishing, rollback or version burn —
`retag`, `yank`, `unlist`, `rollback`, `idempotent` and `409` appeared in neither document — while Story 16.4
AC #2 requires *"a failed publish leaves no partially-released state (the pipeline is safe to re-run)"*. The
constraint is external and non-negotiable:

- **nuget.org rejects a duplicate version** and permits only *unlisting*, never deletion.
- **npm rejects publishing over an existing version**, and its unpublish window is time-limited.
- A multi-channel release therefore **cannot be transactional**. Publishing to nuget.org and then failing on
  npm leaves a version that is half-released and permanently consumed.

**Decision: a version number is consumed on first publish to any channel and is never reused. Recovery is
forward — a new pre-release number and a new tag — never a retry of the same version.**

> ⚠️ **AMENDED 2026-08-08** alongside § Decision 9's move to merge-triggered releasing. The rule is unchanged;
> **where it bites has moved.** Under the old tag-triggered design, tagging and publishing were one act, so a
> tag burned a version. Under § Decision 9 a tag is created on **every** merge (Stage A) while registry
> publication is a separate deliberate act (Stage B) — so **"consumed" attaches to promotion, not to
> tagging**. An unpromoted tag and its GitHub Release cost nothing on any registry and can simply be deleted.
> This makes the forward-only rule considerably cheaper than it was when written.

1. **Re-cut, don't re-publish.** A failed promotion bumps `-preview.N` and promotes the next tag. Per-channel
   resume is **rejected**: it would require the pipeline to distinguish "this version is already on this
   channel because I put it there" from "…because someone else did", across three registries with three
   different conflict semantics, and would still leave the artefacts unequal across channels. § Decision 2
   already states channel parity is not promised, which is what makes the forward-only rule affordable.
2. **The pipeline is safe to re-run — on a new tag.** This is the precise reading Story 16.4 AC #2 must be
   implemented against, and the AC is achievable under it. Re-promoting the *same* tag is refused, not
   attempted: a **preflight queries each target registry for the version and fails fast** if any already has
   it, so the operator gets a clear "this version is consumed, promote `preview.N+1`" instead of a partial
   pass and a 409 halfway through. Under § Decision 9 the next tag already exists on the next merge, so
   recovery does not wait on anyone to cut one.
3. **The reversible step brackets the irreversible ones.** Stage A has already published the GitHub Release
   as a **`prerelease`** carrying the binaries, so a promotion that fails partway leaves a Release that is
   *deletable* and registry state that is not. Order within Stage B is therefore: credential exchange first
   (it fails before anything is published, § Decision 3) → registry preflight → **renderer package before
   wrapper** (§ Decision 5) → Release body updated with the assembled changelog section **last**, so the
   announcement never precedes the artefacts it announces.
4. **Withdrawal of a bad preview, once promoted:** **unlist** on nuget.org (never delete — deletion breaks
   restore for anyone who already resolved it), **`npm deprecate`** with a message naming the superseding
   version (never `npm unpublish`, for the same reason and because the window is time-limited), and **delete
   the GitHub Release** and its assets. The withdrawn version keeps a `CHANGELOG.md` entry marked
   **`[X.Y.Z] — WITHDRAWN`** naming what superseded it: the version is gone from the registries but its
   number is permanently spent, and a reader who finds a stale reference to it deserves an explanation.
5. **A partial promotion must be withdrawn, not merely superseded** *(gap found by the Story 16.1 second code
   review, 2026-08-08)*. Rule 1 says what to do about the *next* version and said nothing about the artefact
   already sitting in a registry, so a nuget-succeeded/npm-failed promotion left a **listed, permanently
   installable half-release** on the channel § Decision 2 designates *authoritative* and which Story 16.9's
   Action resolves against. **Rule 4's procedure applies to a partial promotion too**, and applies to
   whichever channels did publish: unlist and deprecate what landed, before promoting `preview.N+1`.
   ⚠️ **The VS Marketplace has no withdrawal step in rule 4** because the VSIX is out of the preview cut
   (§ Decision 4); when Story 16.5 brings it in, it must add one.

Two things reduce the blast radius regardless: the credential exchange fails the job **before** any channel
is published (§ Decision 3), and the renderer package publishes **before** the wrapper (§ Decision 5).

### 11. What "preview" promises, and what it does not

Promoted into this record from spike report § 6.6. It lived only in the report, while Story 17.4's
release-readiness sign-off was pointed at it as a checklist — a governing obligation cannot sit in a story
artifact (CLAUDE.md § Decision records).

**Promises.** The published channels install and run. SpecScribe generates a portal from a supported SDD
repository. Breaking changes are recorded in `CHANGELOG.md` (prefixed `**BREAKING:**`, § Decision 6) and
carry a minor-version bump inside `0.x`.

**Does NOT promise.** Any support commitment or SLA. API, IR-schema or output stability across preview
versions — inside `0.x`, a **minor** bump may break. Signed binaries (§ Decision 13). That it works without
**Node**, or — on the `dotnet tool` channel — without **.NET 10** (§ Decision 8). Availability on every
platform: the binary RID matrix is three, and `linux-arm64` / `osx-x64` users are directed to the
platform-neutral `dotnet tool` channel (§ Decision 2).

⚠️ **"A supported SDD repository" is not yet defined, and that gap has a known symptom.** The spike found
`EpicsIndexSurface.vue` **hard-throws** when the epics index has no child pages, so a thin or non-BMad
external adopter — the highest-weight first-run case for this epic — sees `errors=1` and a missing page.

**The gate has an owner: Story 23.7 implements the fix, and it blocks Story 16.7's launch readiness.**

> ⚠️ **RE-SEATED 2026-08-08 (owner decision, Story 16.1 second code review). This gate previously named
> Story 23.3, and that assignment failed in a way worth recording rather than quietly overwriting.**
>
> The 2026-08-07 assignment reasoned that 23.3 owned the surface and was at `review`, which in this project's
> lifecycle is an *iterating* state, so the story was still open to work. **That reasoning was sound and it
> still expired.** Story 23.3 closed `done` at its own code review on 2026-08-08 without shipping the fix,
> and that review's `sprint-status.yaml` note **overwrote the reciprocal seat** this ADR's change had placed
> on the `23-3` key. The edge therefore survived only on the `16-7` side, pointing at a closed story, with
> the defect unfixed — and nothing was watching for the state change.
>
> **The general lesson, which outlives this instance: routing work to a story on the strength of its current
> status buys a guarantee that expires when the status changes, and no artifact here observes that
> expiry.** A dedicated story does not have the failure mode, because closing it *is* shipping the fix.

- **Why a new story rather than reopening 23.3.** 23.3 has a completed code-review record and a `done`
  status earned on the work it did finish; reopening it would invalidate that closure to carry one unrelated
  Vue fix. **Story 23.7** takes the work, and takes it with a wider scope than the original routing had:
  besides the `EpicsIndexSurface.vue` fix, it **audits every other migrated surface for the same
  hard-throw-on-empty-collection pattern and records the surfaces found safe**, because this defect class has
  now surfaced twice on two surfaces (Story 23.5 → dashboard, Story 16.1 → epics index) and patching it a
  third time individually would be the wrong response.
- **Why not Story 16.7.** Moving a Vue surface fix into a launch-readiness and cut story would put it
  somewhere no one would look for it. That objection was right in the original assignment and is unchanged.
- **Why this is a structural scope change.** A new cross-epic blocking dependency — and now a new story —
  is exactly what CLAUDE.md § Decision records requires to land in `epics.md` **and** `sprint-status.yaml`,
  not as prose in a spike report. Story 16.1 Task 8's original certification of *"no structural scope
  change"* was wrong on this point: an edge is structure, and a story is more so. Both files carry the
  re-seated edge, on **both** ends, plus the superseded marker on § Story 23.3.

Recorded here, in the governing record, so a reader of the decision sees the precondition without reading
the spike report.

### 12. Package identity, fallback IDs, and platform-package naming

Promoted from spike report § 5.4 because Story 16.8 implements from this record.

**Primary IDs:** `SpecScribe` on nuget.org, `specscribe` on npm. Both verified **unclaimed (404)** on
2026-08-07. Reserving them is an **owner action** and the only item on the release checklist a third party
can take away.

**Fallbacks, and their real cost:** `SpecScribe.Cli` on nuget.org, `specscribe-cli` on npm. These are **not
drop-in replacements**, and the asymmetry between the two registries is the whole point:

- **Losing the NuGet ID is cheap.** `dotnet tool install SpecScribe.Cli` still installs a tool whose
  `ToolCommandName` is `specscribe`, so the *invocation* is unchanged. Only the install line moves.
- **Losing the npm ID is not recoverable by a rename.** `npx <name>` resolves the *package* name, so
  `npx specscribe` would run **someone else's package**. No fallback ID restores the documented command;
  `npx specscribe-cli` is a different command, printed today in ADR 0006 § Decision and `epics.md` § Story 16.8.

  ⚠️ **Corrected 2026-08-08:** this list previously named **`README.md`** as a third place printing
  `npx specscribe`. It does not — `grep -c npx README.md` returns **0**, and returned 0 at `9837e67`,
  `838d591`, `15336f4` and `d21d7b5` as well, so the claim was never true. The real gap is the opposite of
  what the assertion implied: **README.md documents no npx invocation at all**, while npx is channel #2 of
  the preview cut (§ Decision 2). That is a documentation hole for **Story 16.6**, and it was invisible for
  as long as this record asserted the text was already there.

**Escalation rule — an implementer may not take a fallback.** If a primary ID is unavailable at reservation
time, the implementer **stops and escalates to the owner**; substituting silently is the failure this rule
exists to prevent. The owner then chooses, and the choice lands as an **amendment to this ADR in the same
change** that updates every document naming the old string:

| lost ID | owner's choice |
|---|---|
| `SpecScribe` (nuget.org) | take `SpecScribe.Cli`; update the install line in `README.md` and the NuGet references in `epics.md` |
| `specscribe` (npm) | **either** adopt `npx specscribe-cli` — amending ADR 0006 § Decision and `epics.md` § Story 16.8 together — **or** drop the npx channel from the preview cut (§ Decision 2), which is a real option since `dotnet tool` leads the cut |
| **`specscribe-renderer` (npm)** | **stop and escalate — there is no fallback to take.** See the security note below. |
| a platform package (`specscribe-<os>-<arch>`) | rename that package and update the wrapper's `optionalDependencies`; cheap, because the name is never typed by a consumer |

This is precisely why reservation is **owner action #1** and the most urgent item on the list: it is the only
release prerequisite a third party can take away, and on npm there is no cheap recovery.

**Platform-package naming (Story 16.8):** `specscribe-<os>-<arch>` on the npm-conventional axis
(`specscribe-win32-x64`, `specscribe-linux-x64`, `specscribe-darwin-arm64`), resolved through
`optionalDependencies` — **not** .NET RID strings, which npm's `os`/`cpu` fields cannot express. The shared
renderer is `specscribe-renderer` (§ Decision 1), which is platform-neutral and appears once.

🔴 **`specscribe-renderer` is the highest-stakes name in the set, and it was the one never checked**
*(Story 16.1 second code review, 2026-08-08).* Spike report § 5.4's verification table queried four endpoints
covering three names — `SpecScribe`, `specscribe`, `specscribe-win32-x64`. The full set the wrapper needs is
**five**, and the two unchecked ones include the renderer.

The asymmetry this decision is built on runs the wrong way here, and harder than for the primary IDs:

- A squatted **platform** package is a broken install — visible, loud, recoverable by rename.
- A squatted **renderer** package is **arbitrary code execution on every consumer's machine.** § Decision 5
  pins it at an exact `=X.Y.Z`, and `NuxtPrerender` spawns `node <that package>/server/index.mjs` on every
  `generate`. A stranger's package in that slot runs as the user, on every run, silently.

**Therefore:** all five names are reserved together as **owner action #1** — reserving `specscribe` alone
does not secure the channel. `specscribe-renderer` has **no fallback**: if it is taken, the implementer stops
and the owner chooses a new renderer name *and* re-pins § Decision 5, or drops the npx channel. **Story 16.8
must not publish a wrapper whose renderer dependency resolves to a package this project did not publish** —
verify ownership at wiring time, not just availability at reservation time.

### 13. Code signing: neither Authenticode nor notarization for the preview

Promoted from spike report § 5.5 — AC #2 asked for a code-signing decision explicitly, so it belongs in the
decision record rather than in a non-goals bullet.

**Decision: neither, for the preview.** The consequences are accepted and must be documented rather than
hidden (Story 16.6): **SmartScreen** warns on the unsigned Windows binary until reputation accrues, and
**Gatekeeper** blocks the unsigned macOS binary until the user clears it explicitly. This is materially
cheaper than it first appears because the two channels leading the cut install through package managers and
trigger neither. The compensating control for the one channel that *is* exposed is the published SHA-256
digest (§ Decision 2), not a signature.

## Consequences

**Positive**
- The renderer/CLI two-halves problem disappears for every packaged consumer. Story 16.9's composite Action
  collapses to install-and-run, and `README.md`'s external CI recipe loses its `SPECSCRIBE_RENDERER_DIR` step.
- The version-mismatch failure class Story 16.9 AC #2 fears is **structurally impossible** on two of three
  channels and exact-pinned on the third.
- No long-lived publishing credential exists to leak or rotate for either shipping channel.
- The 1.18 MB package cost is small enough that no channel is priced out by it.
- The two channels leading the preview cut install through package managers, so neither triggers SmartScreen
  nor Gatekeeper — which makes the no-code-signing decision materially cheaper than it first appears.

**Negative / trade-offs**
- **The VSIX is not in the first preview.** FR33 is deferred, not dropped, and the Marketplace path now
  carries an identity-federation setup cost that a PAT would not have.
- The unsigned direct-download binary is the roughest preview experience: SmartScreen on Windows, Gatekeeper
  on macOS. Accepted for the preview and documented rather than hidden (Story 16.6).
- The npm channel is the one place a CLI/renderer mismatch remains *expressible*; it is prevented by an exact
  pin, i.e. by policy plus a pipeline invariant, not by construction.
- Adding a RID later remains a real cost (~76 MiB / ~34 MB gzipped each), even though the renderer no longer
  multiplies with it.
- MinVer makes every build depend on git tag reachability; a shallow clone without tags produces a wrong
  version rather than an error. **`fetch-depth: 0` is checkout configuration, so it belongs to the stories
  that own the workflows — Story 16.2 (`build-test-analyze.yml`) and Story 16.4 (the release pipeline) —
  not to Story 16.3**, which owns CLI packaging and does not touch either file. Combined with the deletion
  of `<Version>`, this is a silent-wrong-version path, so its mitigation must be seated where the fix
  actually lands.
- "Reproducible" is claimed in its weaker sense only. It was **not** true in that sense when this ADR was
  written; `npm ci` has since been repaired by `0b1f561` (§ Decision 7), so the weak reading now holds and
  the remaining gaps are the deferred ones.
- **The self-contained binary channel is two filesystem objects, not one artefact** (§ Decision 5), so its
  "one released unit" property is a packaging obligation on Story 16.4, not a structural guarantee.
- A **failed multi-channel publish cannot be undone** (§ Decision 10). Every release consumes its version
  number irreversibly on any channel it reached. The policy makes this survivable rather than absent —
  forward-only re-cuts, a registry preflight, and a draft GitHub Release bracketing the irreversible steps —
  but the cost is real: **`-preview.N` counters will have gaps**, and a reader who sees `preview.1` followed
  by `preview.3` is looking at a consumed number, not a mistake.
- **The preview is forward-fix only** (§ Decision 2, § Decision 9). A defect in a published preview cannot be
  patched in place; the next preview supersedes it. Acceptable at `0.x` with no support commitment
  (§ Decision 11), and explicitly a `1.0` problem rather than a preview one.
- **`CHANGELOG.md` gains an assembly step** (§ Decision 6). The fragment directory removes a real
  concurrent-edit hazard, but it adds a build-time transform between what a story author writes and what a
  consumer reads — so a broken assembler is a silent-empty-release-notes failure, and Story 16.4's release
  body must be checked, not assumed.

## Relationship to ADR 0006 — this AMENDS it

**What is genuinely amended: the channel ordering.** ADR 0006 §Decision calls npx *"the primary CLI
channel"* with `dotnet tool` secondary (`0006-…md:202`). This ADR ships **`dotnet tool` first**
(§ Decision 2) — because it is already wired, needs no RID matrix, and is the channel this spike proved
end-to-end. That is a departure from a ratified record and is flagged as the amendment, rather than being
softened into "the ordering is preserved as a statement of audience". A reader of ADR 0006 who expects npx
to lead the preview would otherwise be wrong with no marker telling them so. ADR 0006's *audience* claim
still stands: npx remains the lowest-friction channel for a consumer without .NET.

**What is an extension, not an amendment:** the **packaging shape** ADR 0006 never specified (§ Decision 1)
and the **ordered preview cut** it never had. ADR 0006's channel *list* is unchanged.

ADR 0006 §Consequences already anticipated the cost this ADR now bounds: *"Distribution now maintains two
channels … and a per-RID native-package matrix."* § Decision 2 fixes that matrix at three RIDs for the preview.

## Relationship to ADR 0022 — this AMENDS it

- **§Decision 5's "The binary detects Node at startup"** is amended to *at prerender time*, matching the
  shipped implementation (§ Decision 8). The ADR led its implementation on this point; the implementation is
  the better answer and the ADR moves to it.
- **ONE of its two "left to the owner" questions is closed here — not both.** Question 1 (install-time Node
  check for npx): **closed, no** — § Decision 8. Question 2 (`web/` coverage warranting a component-test
  story): **NOT closed** — it is a testing-scope question, unrelated to packaging, and **remains open**
  against ADR 0022.
- **§Consequences' "Story 16.1/16.4 must add the `npm run build:package` stage"** is discharged as a
  *decision* here and assigned to Story 16.4 as *implementation*.
- Everything else in ADR 0022 stands unchanged: Node is never a shipped toolchain, the artefact carries no
  prerendered pages, the IR resolves at server runtime, and asset paths stay page-relative.

## References

- **This spike:** [Story 16.1](../../_bmad-output/implementation-artifacts/16-1-release-and-distribution-packaging-spike.md) ·
  [16-1-spike-report.md](../../_bmad-output/implementation-artifacts/16-1-spike-report.md) (measurements, provenance, false starts)
- **Predecessor:** [23-5-packaging-strategy-report.md](../../_bmad-output/implementation-artifacts/23-5-packaging-strategy-report.md) — open items 3–6 are Epic 16's; this ADR takes 4, 5 and 6.
- [ADR 0006 — Delivery Architecture & Distribution](0006-delivery-architecture-and-distribution.md)
- [ADR 0022 — Node Is a Build-Time Toolchain and a Generate-Time Runtime](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md)
- [ADR 0034 — The IR Is the Product and the Site Is Rendered From It](0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md) — why the renderer is mandatory, not optional
- [ADR 0008 — JSON IR as Canonical Representation](0008-json-ir-canonical-and-incremental-generation.md) — the versioned output format the preview declines to freeze
- `src/SpecScribe/NuxtPrerender.cs:41,66-127,141-216` — artefact resolution, Node range, assertions
- `src/SpecScribe/SpecScribe.csproj:14-16,19,28,36-37` — tool packaging, version literal, `SOURCE_DATE_EPOCH` stamp
- `src/SpecScribe/AboutTemplater.cs:90,133-135` — informational-version parsing and the Preview badge
- `.github/workflows/build-test-analyze.yml:1-13,246,281-290,416` — the gate, `npm ci`, the `--deep-git` requirement
- [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) ·
  [npm trusted publishers](https://docs.npmjs.com/trusted-publishers/) ·
  [Retirement of Global PATs in Azure DevOps](https://devblogs.microsoft.com/devops/retirement-of-global-personal-access-tokens-in-azure-devops/) ·
  [VS Code — Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension) ·
  [microsoft/vscode-vsce#1023](https://github.com/microsoft/vscode-vsce/issues/1023) ·
  [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) ·
  [MinVer](https://github.com/adamralph/minver)
