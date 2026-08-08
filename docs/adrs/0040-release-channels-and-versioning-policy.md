# ADR 0040 — Release Channels, Packaging Shape, Credential Posture and Versioning Policy

- **Status:** Proposed
  - ⏫ **Ratification to `Accepted` requested of the owner — this is now the record's only open item.**
    Story 16.1 AC #4 requires this ADR to land *ratified*, not `Proposed`. Stories 16.2–16.9 and 17.4 all
    build on it. The ratification is the owner's act; this line is the request, not the act.
  - ✅ **The eight technical decisions left open by the Story 16.1 code review (2026-08-07) are resolved in
    this revision** — MinVer bootstrap (§ Decision 5), version-component semantics and the `0.x` exit
    criterion (§ Decision 5), extension versioning (§ Decision 5), changelog contention (§ Decision 6), the
    CI-gate lookup rule and hotfix scope (§ Decision 9), release atomicity and withdrawal (§ Decision 10),
    the `EpicsIndexSurface` gate's ownership (§ Decision 11), and the package-ID escalation rule
    (§ Decision 12). Nothing in the record is marked OPEN any longer.
  - ⚠️ **This record is `Proposed` and it amends another `Proposed` record.** [ADR 0022](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md)
    has stood at `Proposed` since 2026-07-27 with its own ratification outstanding, and Stories 16.2 **and
    16.3 have both already shipped** against it — 16.3 implementing § Decision 1's pack item and § Decision
    5's MinVer derivation directly. Downstream stories are therefore building on an unratified chain; that
    is disclosed here rather than left for a reader to discover, and it is why ratification is the
    highest-urgency owner action rather than a formality.
- **Date:** 2026-08-07
- **Deciders:** Matthew-Hope Eland (owner) — ratification pending
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

**The pack item's exact form is normative, because a wrong one succeeds silently.** Story 16.3 implements
this item verbatim — do not paraphrase it, and do not reintroduce `%(RecursiveDir)`, which double-applies:

```xml
<None Include="..\..\web\.output\**\*" Pack="true"
      PackagePath="tools\$(TargetFramework)\any\renderer" CopyToOutputDirectory="Never" />
```

`PackagePath` **must derive from `$(TargetFramework)`, never a hard-coded `net10.0`.** The project sets
`<RollForward>Major</RollForward>`, so a TFM bump is anticipated; with a literal, a bump relocates the
assembly to `tools/net11.0/any/` while the payload stays at `tools/net10.0/any/renderer/`, `AppContext.BaseDirectory`
loses its sibling, and **every packaged consumer breaks with a green pipeline**.

**A packaging-time completeness assertion is REQUIRED, not optional** (Story 16.4). The spike measured this
exact false pass: a wrong `PackagePath` produced **187 entries, the right file count, the right total bytes
and exit 0 — with `renderer/server/index.mjs` absent** (spike report § 2.7 finding 1). A size-and-count check
therefore certifies nothing. The release job must assert the **entry point exists at its packed path** inside
the produced package, not merely that the file count matches. Related: SpecScribe currently reports
*"the renderer answered HTTP 500"* and **discards the renderer's own error text**, so an incomplete payload
surfaces to the consumer as an unexplained failure — Story 16.3 owns propagating that text.

**No `SPECSCRIBE_RENDERER_DIR` is required by any packaged consumer.** The variable remains the explicit
override and keeps its hard-fail-on-miss semantics (`NuxtPrerender.cs:80-98`).

⚠️ **Consequence for Story 16.3 — the artefact path is now consumer-chosen, and the spawn is not quoted.**
`NuxtPrerender` launches the renderer through the single-string `ProcessStartInfo(fileName, arguments)`
overload rather than `ArgumentList` (`src/SpecScribe/NuxtPrerender.cs:251`). Until this decision, `_artefactDir`
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

**Supported-platform matrix, and what an unsupported platform gets.** The three RIDs above are the *binary*
matrix only. The `dotnet` global tool is **platform-neutral** and remains available everywhere .NET 10 runs,
including `linux-arm64` and `osx-x64` — so a deferred RID is a deferred *convenience*, not an unsupported
platform, and Story 16.6 must say so. Story 16.8's `optionalDependencies` wrapper **must emit an explicit,
actionable message when no platform package matches**, naming the `dotnet tool` channel as the fallback;
npm's default behaviour is an opaque missing-binary error at run time.

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

### 3. Credential posture — two channels store nothing

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
     build now emits `0.1.0-preview.0.<height>` — inside this scheme, and still carrying a pre-release label
     so the About page's Preview badge survives. MinVer's undirected `0.0.0-alpha.0.<height>` can no longer
     be produced. `README.md`'s external-CI recipe no longer pins a literal either: Story 16.3 changed it to
     read the version off the `.nupkg` the pack produced.

     What remains is not a defect but a one-time act: **the first real tag, `v0.1.0-preview.1`, must exist
     before the first release publishes.** It is an owner action seated against **Story 16.4** (§ 8 of the
     spike report), not a precondition for 16.3 — which has already shipped safely without it.

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
  hold, and Story 17.4's sign-off tests them: (a) the IR schema is frozen under ADR 0008's versioning;
  (b) every channel in the preview cut (§ Decision 2) has published at least one release; (c) § Decision 11's
  *does not promise* list no longer contains output-, API- or IR-stability.
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
- **CLI and renderer are pinned as one released unit.** For **NuGet** this is genuinely structural — there is
  one artefact and the payload is inside it. For the **self-contained binary** it is *not* structural: the
  channel is defined as *"a sibling `renderer/` directory beside the executable"* (§ Decision 1) — **two
  filesystem objects**, which a user can desynchronize by unzipping release N over release N−1 or by
  replacing only the `.exe`. `ResolveArtefactDirectory` tests only that `renderer/server/index.mjs` exists;
  nothing stamps the artefact with a version. **Story 16.4 must therefore ship each RID as a single archive
  containing both halves** (never the exe and the renderer as separate release assets), and Story 16.3 must
  stamp the artefact with the CLI version and **fail loudly on a mismatch** rather than rendering from a
  stale renderer. Without that, this channel reproduces exactly the failure Story 16.9 AC #2 exists to
  prevent — one that *"fails as wrong output rather than as an error"*.
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
release job then writes a release body of *"No user-visible changes in this release."* and continues — it
**must not** hard-fail at the last step, because by then the packages are already published and the version
is burned.

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

- The release job (Story 16.4) **concatenates the fragments by section**, writes them into `CHANGELOG.md`
  under the released version's header, copies that section into the GitHub Release body, and **deletes the
  consumed fragments** in the release commit.
- `CHANGELOG.md` remains the published artefact in Keep a Changelog 1.1.0 format, and remains hand-authored
  in substance — the assembly is mechanical, not generative, so the § "generated notes are rejected"
  rationale is untouched. **Story 16.6 owns the format and the assembler; Story 16.4 owns invoking it.**

**Why a directory fixes it:** each story creates a *distinct new file*, so two concurrent stories cannot
conflict and neither can silently overwrite the other. The failure mode becomes a missing file — visible in
`git status` and in review — rather than a vanished line inside a shared one.

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

### 9. The CI gate applies to a tag by requiring the tagged commit to be green on `main`

The release pipeline is tag-triggered; `build-test-analyze.yml` is push/PR-triggered. NFR9's *"publishing …
gated on a passing build + test run"* is satisfied by **requiring that the tagged commit already passed on
`main`**, not by re-running build+test inside the release job. Re-running invites a different result from the
same source and doubles the wall-clock of every release.

The required-check string is the **job name verbatim: `build-test-analyze`**.
`portability-probe (ubuntu, non-gating)` carries `continue-on-error` at the job level and **must not** be
made required. Per epics.md § Story 16.2 (AMENDED 2026-07-25), **do not create a second build+test workflow**.

**The lookup rule is normative, because "already passed" is not self-implementing.** The Story 16.1 code
review (2026-08-07) found this decision naming no mechanism and no failure branch. Story 16.4's release job
begins with a **preflight** step, before any build and before any credential exchange:

- **Query:** the check-runs for the **tagged commit SHA** — `gh api repos/{owner}/{repo}/commits/{sha}/check-runs`
  — filtered to the check named **`build-test-analyze`** (§ above: the job name verbatim).
- **Pass** only on `status == "completed"` **and** `conclusion == "success"`.
- **In progress** (`queued`/`in_progress`): poll at 30 s intervals up to **15 minutes**, then fail. A tag
  pushed immediately after a merge is the normal case, not an exception, so the wait is deliberate rather
  than a courtesy.
- **Failed, cancelled, timed out, or turned red by a later re-run:** fail. The *most recent* completed run
  for that SHA is authoritative — a re-run that went red supersedes an earlier green, never the reverse.
- **No run found:** fail with the actionable message *"tag a commit that has been merged to `main`; only
  `main` is built by `build-test-analyze`."* This is the branch the review correctly noted had no defined
  action.

**Tags are created only on `main` for the whole preview**, which is what makes the rule above total. The
review's hotfix observation is real and is answered by scope rather than by mechanism: **the preview is
forward-fix only.** A defect in a published preview is fixed on `main` and released as the next
`0.MINOR.PATCH-preview.N` (§ Decision 5); no release branch exists, so no commit outside `main` ever needs a
run to point at. This is added to § Decision 2's non-goals rather than left implicit.

If a hotfix branch is ever genuinely required — a `1.0` concern, not a preview one — the prerequisite is
explicit and seated: **`build-test-analyze.yml`'s `push` trigger must cover that branch pattern first**
(Story 16.2 owns that file). The preflight rule then works unchanged. What must not happen is a tag on an
unbuilt branch with the preflight quietly relaxed to allow it.

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

1. **Re-cut, don't re-publish.** A failed release bumps `-preview.N` and re-tags (`v0.1.0-preview.1` →
   `v0.1.0-preview.2`). Per-channel resume is **rejected**: it would require the pipeline to distinguish
   "this version is already on this channel because I put it there" from "…because someone else did", across
   three registries with three different conflict semantics, and would still leave the artefacts unequal
   across channels. § Decision 2 already states channel parity is not promised, which is what makes the
   forward-only rule affordable.
2. **The pipeline is safe to re-run — on a new tag.** This is the precise reading Story 16.4 AC #2 must be
   implemented against, and the AC is achievable under it. Re-running the *same* tag is refused, not
   attempted: a **preflight queries each target registry for the version and fails fast** if any already has
   it, so the operator gets a clear "this version is consumed, cut `preview.N+1`" instead of a partial
   pass and a 409 halfway through.
3. **Order the publishes so the only reversible step brackets the irreversible ones.** The GitHub Release is
   created as a **draft** first, the registry publishes run inside it (renderer before wrapper, § Decision 5),
   and the draft is **flipped to published last**. A failure before that flip leaves a draft nobody can
   install — deletable — rather than an announced release pointing at packages that do not exist.
4. **Withdrawal of a bad preview, once published:** **unlist** on nuget.org (never delete — deletion breaks
   restore for anyone who already resolved it), **`npm deprecate`** with a message naming the superseding
   version (never `npm unpublish`, for the same reason and because the window is time-limited), and **delete
   the GitHub Release** and its assets. The withdrawn version keeps a `CHANGELOG.md` entry marked
   **`[X.Y.Z] — WITHDRAWN`** naming what superseded it: the version is gone from the registries but its
   number is permanently spent, and a reader who finds a stale reference to it deserves an explanation.

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

**The gate has an owner: Story 23.3 implements the fix, and it blocks Story 16.7's launch readiness.** The
Story 16.1 code review (2026-08-07) was right that this needed resolving rather than asserting, on two
counts, and both are now answered:

- **Why 23.3 keeps it even though it is at `review`.** `review` is not `done` in this project — CLAUDE.md's
  story lifecycle puts owner verification and iteration *at* that stage, so a story at `review` is still
  open to work. 23.3 already owns this surface and already fixed the identical defect class one component
  over (`DashboardSurface.vue` handles its own empty case gracefully **in the same run**). Opening a new
  story to fix the sibling of a defect an in-flight story already owns would fragment the work, and moving a
  Vue surface fix into Story 16.7 — a launch-readiness and cut story — would put it somewhere no one would
  look for it.
- **Why this is a structural scope change after all.** A new cross-epic blocking dependency is exactly what
  CLAUDE.md § Decision records requires to land in `epics.md` **and** `sprint-status.yaml`, not as prose in a
  spike report. Story 16.1 Task 8's original certification of *"no structural scope change"* was wrong on
  this one point — no story was added or renumbered, but a **new blocking edge between epics** was created,
  and an edge is structure. Both files now carry it.

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
  `npx specscribe-cli` is a different command, printed today in ADR 0006 § Decision, `epics.md` § Story 16.8
  and `README.md`.

**Escalation rule — an implementer may not take a fallback.** If a primary ID is unavailable at reservation
time, the implementer **stops and escalates to the owner**; substituting silently is the failure this rule
exists to prevent. The owner then chooses, and the choice lands as an **amendment to this ADR in the same
change** that updates every document naming the old string:

| lost ID | owner's choice |
|---|---|
| `SpecScribe` (nuget.org) | take `SpecScribe.Cli`; update the install line in `README.md` and the NuGet references in `epics.md` |
| `specscribe` (npm) | **either** adopt `npx specscribe-cli` — amending ADR 0006 § Decision, `epics.md` § Story 16.8 and `README.md` together — **or** drop the npx channel from the preview cut (§ Decision 2), which is a real option since `dotnet tool` leads the cut |

This is precisely why reservation is **owner action #1** and the most urgent item on the list: it is the only
release prerequisite a third party can take away, and on npm there is no cheap recovery.

**Platform-package naming (Story 16.8):** `specscribe-<os>-<arch>` on the npm-conventional axis
(`specscribe-win32-x64`, `specscribe-linux-x64`, `specscribe-darwin-arm64`), resolved through
`optionalDependencies` — **not** .NET RID strings, which npm's `os`/`cpu` fields cannot express. The shared
renderer is `specscribe-renderer` (§ Decision 1), which is platform-neutral and appears once.

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
