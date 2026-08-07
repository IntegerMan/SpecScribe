# Story 16.1 — Release & Distribution Packaging Spike Report

**Date:** 2026-08-07
**Executed at:** `838d591` (story frontmatter records `baseline_commit: 7ff3b13`; HEAD had advanced by one merge
before this run started — see § 9)
**Machine:** Windows 11 Pro 10.0.26200 · .NET SDK 10.0.302 · Node v24.18.1 · npm 11.16.0
**Worktree:** `.claude/worktrees/story-16-1-dev` on branch `worktree-story-16-1-dev`

---

## Verdict

**The renderer artefact ships inside the package, packed as content under the tool's own directory, and it
works — measured, on two channels, from a repository that is not this one.**

A `renderer/**` payload packed at `tools/<tfm>/any/renderer/` lands beside the executing assembly in the
`dotnet tool` store, `AppContext.BaseDirectory` resolves to that directory, and
`NuxtPrerender.ResolveArtefactDirectory`'s second candidate — the one whose doc comment already calls it
"the Epic 16 packaging shape" — finds it. A self-contained `PublishSingleFile` binary resolves a sibling
`renderer/` the same way; single-file packaging does **not** move `AppContext.BaseDirectory` into an
extraction directory. Both ran `generate` to `errors=0` from a foreign repository with
`SPECSCRIBE_RENDERER_DIR` unset.

**The cost is small and the compression is favourable.** The artefact is 3.96 MB / 187 files on disk but
adds only **+1,241,709 bytes (+49.4%)** to the nupkg, because it is pure JavaScript text.

**The preview cut is: NuGet `dotnet` global tool → npx → self-contained binaries. The VSIX is OUT of the
first preview**, and not for the reason the story anticipated. The credential situation is worse than R3
recorded: Azure DevOps **blocked creation and regeneration of global PATs on 2026-03-15**, five months ago,
and `vsce` requires exactly that token shape ("All accessible organizations" + Marketplace (Manage)). The
PAT path is not "dated" — for a publisher who does not already hold a valid global PAT, it is **already
closed**.

**Two of three channels need no stored secret at all.** Trusted Publishing on nuget.org and npm makes AC #2's
"no secret value is committed" structural rather than a matter of discipline.

**NFR9 "reproducible" is claimed in its weaker reading only** — built from a clean checkout by CI — and even
that is **not true today**: `npm ci` fails at `838d591` on this machine. That is a live finding, not a
theoretical gap.

---

## 1. Method and provenance

Every figure below is marked. Nothing in this report is an estimate presented as a measurement.

| figures | provenance |
|---|---|
| artefact file count / bytes / gzipped tarball / native-binding count | **Session-measured**, Windows 11 Pro 10.0.26200, Node v24.18.1. Commands inline in § 3. |
| nupkg sizes and entry counts, baseline vs. payload | **Session-measured**, `dotnet pack -c Release`, .NET SDK 10.0.302 |
| observed `AppContext.BaseDirectory` | **Session-measured by negative case** — the artefact was renamed away and the tool's own error message printed the path it probed (§ 2.3). Not inferred from documentation. |
| route counts, ms/route, `errors=` | **Session-measured**, `specscribe generate` stdout, foreign repository |
| single-file exe size | **Session-measured**, `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` |
| package-ID availability | **Read from the registries** 2026-08-07 (HTTP status codes inline in § 5.4) |
| Trusted Publishing mechanics (NuGet, npm) | **Read from vendor documentation** 2026-08-07; NuGet doc `updated_at: 2026-08-03` |
| Azure DevOps PAT retirement dates | **Read from Microsoft DevBlogs + VS Code publishing docs** 2026-08-07 |
| `microsoft/vscode-vsce#1023` state | **Read from the GitHub API** 2026-08-07 |
| ADR 0006 / ADR 0022 figures (73 MiB, 3.78 MB, 201.9 MB, 1,558-byte wrapper) | **Inherited, ratified/authored upstream.** Cited, not re-derived (R1). Where this session re-measured one, both numbers are shown. |

**One provenance caveat, stated rather than buried.** `web/node_modules` for this session was installed with
`npm install --no-save --no-package-lock`, **not** `npm ci`, because `npm ci` fails at this commit (§ 6.1).
Dependency versions may therefore differ from the lockfile pin. This affects the *build* of the artefact,
not the packaging conclusions — the resolution behaviour under test is C#'s, and the drift gates
(§ 7) passed against the resulting artefact.

---

## 2. The load-bearing experiment (AC #5)

### 2.1 Setup

```sh
cd web && npm install --no-save --no-package-lock   # npm ci is broken at HEAD — see § 6.1
npm run sync:assets
npm run build:package                                # NEVER `npm run build` (ADR 0022 §Decision 2)
```

Temporary `SpecScribe.csproj` edit, reverted before the story closed:

```xml
<None Include="..\..\web\.output\**\*" Pack="true" PackagePath="tools\net10.0\any\renderer" CopyToOutputDirectory="Never" />
```

```sh
dotnet pack src/SpecScribe/SpecScribe.csproj -c Release -o artifacts
dotnet tool install SpecScribe --version 0.1.0-preview --tool-path ./probe-tools --add-source ./artifacts
```

The probe repository is `C:\Users\MattE\.claude\jobs\eac9eab5\tmp\probe-project` — its own git repository,
**no `web/` directory**, `SPECSCRIBE_RENDERER_DIR` unset. Both conditions were asserted before each run, not
assumed: `Test-Path web` → `False`, `git rev-parse --show-toplevel` → the probe path.

### 2.2 Result — the `dotnet tool` channel

```
[prerender] 373 route(s) in 1842 ms (4.9 ms/route)
SpecScribe: generated=18 updated=0 skipped=3 errors=0 elapsed_ms=3557
```

**`errors=0`.** The tool store layout is:

```
probe-tools\
  specscribe.exe                                            ← the PATH shim
  .store\specscribe\0.1.0-preview\specscribe\0.1.0-preview\tools\net10.0\any\
      specscribe.dll                                        ← the real assembly
      renderer\server\index.mjs                             ← the payload, beside it
```

### 2.3 The observed `AppContext.BaseDirectory` — proven, not asserted

The artefact directory was renamed away and the tool re-run. Its own failure message printed the probed path:

```
The SpecScribe renderer artefact could not be found, so no HTML …
Looked for 'server/index.mjs' under, in order:
  · renderer/ beside the executable
      …\probe-tools\.store\specscribe\0.1.0-preview\specscribe\0.1.0-preview\tools\net10.0\any\renderer
  · web/.output/ in the repo (developer path)
```

So `AppContext.BaseDirectory` **is** the tool-store `tools/net10.0/any/` directory. Two things follow, and
the second is the one that matters:

1. The hypothesis in R2 is **confirmed**.
2. **The negative case fails hard rather than falsely passing.** With the payload removed, the run errored;
   candidate 3 (`web/.output/` in the repo) did **not** rescue it, which is the proof that the probe
   repository was genuinely foreign. Had the probe run inside this repository, that candidate would have
   succeeded and reported a false pass — the exact wrong-answer-with-a-success-status class ADR 0022
   §Decision 2 and Story 23.5 were both written against.

### 2.4 Result — the self-contained single-file channel

```sh
dotnet publish src/SpecScribe/SpecScribe.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -o probe-singlefile
cp -r web/.output probe-singlefile/renderer
```

```
[prerender] 373 route(s) in 1862 ms (5.0 ms/route)
SpecScribe: generated=18 updated=0 skipped=3 errors=0 elapsed_ms=3703
```

**`errors=0`. `PublishSingleFile` does not move `AppContext.BaseDirectory` away from a sibling `renderer/`.**
This was the open question R2 flagged as possibly-unmeasurable-in-the-box; it is measured. On .NET 10 the
single-file host reports the *executable's* directory, not a self-extraction directory, so the sibling
resolves. The publish directory holds exactly **2 files** (`specscribe.exe`, `specscribe.pdb`) before the
renderer is added — so `renderer/` is the only sibling the packaging must place.

`specscribe.exe` measured **79,742,177 bytes = 76.0 MiB**. ADR 0006 recorded 73.0 MiB; the +3 MiB is .NET 10
plus the code added since 2026-07-10. Cited as a refreshed figure, not as a correction to the ADR.

### 2.5 Result — the npm platform-package channel (16.8)

**Decided from a measured property rather than a run**, and labelled accordingly.

The shipped artefact contains **0 `.node` native bindings and 0 `.exe`/`.dll`/`.so`/`.dylib` files** — it is
platform-independent, which is ADR 0022's central asymmetry restated at packaging time. Therefore the
renderer **must not** be duplicated into each per-RID platform package. The correct shape is:

- `specscribe` — the ~1.5 KB wrapper (ADR 0006's proven pattern), with
- `optionalDependencies` on `specscribe-<rid>` per RID, each carrying **only** the native binary, and
- a **plain `dependencies` entry on one shared, platform-neutral `specscribe-renderer` package** carrying the
  3.96 MB artefact once.

Cost, in gzipped-tarball terms (npm ships tarballs — measured at **1,157,973 bytes = 1.10 MB**):

| shape | renderer cost across a 5-RID matrix |
|---|---|
| duplicated per platform package | 5 × 1.10 MB = **5.50 MB** |
| **shared package (chosen)** | 1 × 1.10 MB = **1.10 MB** |

This run did **not** publish or install an npm wrapper. The shape is a decision derived from a measured
property; the end-to-end npx install is **Story 16.8's to prove**, exactly as ADR 0006's own wrapper proof was.

### 2.6 Package size delta (AC #5)

| | bytes | entries |
|---|---:|---:|
| nupkg, baseline (no renderer) | 2,515,650 | 25 |
| nupkg, with `renderer/**` | 3,757,359 | 212 |
| **delta** | **+1,241,709 (+49.4%)** | **+187** |
| artefact on disk, uncompressed | 4,154,964 (3.96 MB) | 187 files |
| artefact as a gzipped tarball | 1,157,973 (1.10 MB) | — |

**A 3.96 MB payload costs 1.18 MB in the package.** The artefact is pure JavaScript text and compresses
~3.4×. Story 23.5 measured the artefact at 3.78 MB / 185 files on 2026-07-27; this session measures
**3.96 MB / 187 files**. Both are session-measured on the same machine class; the artefact has grown ~4.8%
in eleven days. **Refreshed, not corrected** — and worth noting that this figure has now moved twice, so
Story 16.3 should derive it rather than quote it.

### 2.7 Four things this experiment got wrong first

This section exists because Story 23.5 §2 proved it is the most useful one. Each wrong result looked
plausible and three of the four produced a *green-looking* or *self-consistent* outcome.

**(1) `%(RecursiveDir)` double-applied and silently produced a package with no entry point.**
The seeded hypothesis in R2 suggested `PackagePath="tools\net10.0\any\renderer\"`. Reaching for the usual
MSBuild idiom to preserve directory structure produced:

```
tools/net10.0/any/renderer/server/node_modules/hookable/dist/server/node_modules/hookable/dist/index.mjs
```

NuGet **already appends** the recursive directory to a `PackagePath` that names a folder, so adding
`%(RecursiveDir)` applies it twice. The pack **succeeded** — 187 entries, right count, right total bytes,
exit 0 — and `renderer/server/index.mjs` did not exist. A size-and-count check would have called this a pass.
The correct form omits the metadata entirely: `PackagePath="tools\net10.0\any\renderer"`.

**(2) `SPECSCRIBE_IR_DIR` takes the output ROOT, not the `spa/` directory.**
Booting the artefact by hand to diagnose a 500, `SPECSCRIBE_IR_DIR` was pointed at `probe-out2/spa`. The
renderer then looked for `probe-out2/spa/spa/manifest.json` and threw. The first minute of that diagnosis was
spent reading a stack trace caused by the diagnosis itself. Incidentally it confirms ADR 0022 §Decision 4:
the path in the error was the one set at *runtime*, so the artefact does read `process.env.SPECSCRIBE_IR_DIR`
at server start, not at build time.

**(3) The first probe repository had no parseable epics — and that surfaced a real defect.** See § 4.1.

**(4) `check:ir-content` went red, and regenerating the baseline would have destroyed 185 rules.**
See § 7.2. The gate's failure signature was `+4 / -185` and the fix was **not** to regenerate.

---

## 3. What is settled, what this spike decided (AC #1)

Per R1, the settled column is **inherited fact with citations**, not fresh analysis.

| question | status | authority |
|---|---|---|
| CLI ships as a `dotnet` global tool | **Settled — yes** | `SpecScribe.csproj:14-16` (verified at `838d591`) |
| CLI ships as self-contained per-OS binaries | **Settled — yes** | ADR 0006 §Decision, §Comparison |
| npx is a channel | **Settled — yes, first-class** | ADR 0006 §Decision 2; Story 16.8 |
| VSIX is a channel | **Settled — yes** (FR33), blocked on Epic 6 | epics.md §Story 16.5 |
| Node ships inside any package | **Settled — no** | ADR 0022 §Decision 1-2 |
| Standalone binary requires Node | **Settled — yes, documented prerequisite** | ADR 0022 §Decision 5 |
| **How the renderer rides inside a package** | **DECIDED HERE** — packed as content at `tools/<tfm>/any/renderer/`; sibling `renderer/` for the binary; one shared npm package | § 2 |
| **Which channels are in the preview cut** | **DECIDED HERE** | § 3.1 |
| **Non-goals** | **DECIDED HERE** | § 3.2 |

### 3.1 The preview cut, in order

1. **NuGet `dotnet` global tool** — already wired, proven end-to-end here, no stored secret (Trusted
   Publishing). Lowest-risk first cut.
2. **npx / npm wrapper** — ADR 0006 calls this the *primary* CLI channel for the JS/SDD audience and
   `dotnet tool` the secondary. It ships second only because it depends on the RID matrix and on 16.3's
   binaries existing, not because it matters less.
3. **Self-contained per-OS binaries** — the payload for (2), and a direct download channel in their own right.
4. **VSIX / VS Marketplace — OUT of the first preview.** See § 5.3. Blocked on Epic 6 regardless; the
   credential finding independently confirms the sequencing.

Story 17.4's release-readiness sign-off gates the cut (epics.md §Epic 17), and Story 16.9's composite Action
becomes collapsible to install-and-run the moment (1) ships with the payload — its epics.md entry names that
dependency precisely, and § 2 is the evidence it is now satisfiable.

### 3.2 Explicit non-goals for the preview (AC #1 requires these by name)

**Out, by name:** a stable/1.0 release · Homebrew · winget · Chocolatey · Scoop · a container image
(recorded but deliberately unseated in epics.md §Story 16.9) · Open VSX · code-signing (§ 5.5) · byte-identical
reproducible builds (§ 6) · publishing from any CI system other than GitHub Actions.

**RID matrix for the preview — three, not five:** `win-x64`, `linux-x64`, `osx-arm64`. At ~76 MiB each
(§ 2.4, ~34 MB gzipped per ADR 0006), that is the cost decision, and 16.8's `optionalDependencies` shape
follows from it directly. `linux-arm64` and `osx-x64` are **named and deferred** to the first release that
has a user asking for them — deferring is cheap because the renderer is shared (§ 2.5), so adding a RID later
costs one binary, not one binary plus 1.10 MB.

---

## 4. Defects found, raised not patched

This spike ships no product code. Both findings below were reproduced, and both are routed.

### 4.1 `EpicsIndexSurface.vue` hard-throws on a project with no epics — **the same class as 23.5's `DashboardSurface.vue`**

A project SpecScribe cannot extract epics from still gets an `epics.html` route in the manifest, with
`children: []`. The renderer then throws:

```
[render error] /epics.html
Error: The epics index IR entry declares no child pages. The epics tree is this story's primary
navigable surface; an index with no children means the manifest's parent/child graph broke upstream.
    at EpicsIndexSurface_vue_… .setup
```

Result: `[prerender] 21 route(s) …, 1 failed` and `errors=1`. Reproduced twice.

This is the **project-independence** failure mode ADR 0022's two-IR experiment was built to catch, one
surface further along: 23.5 found `DashboardSurface.vue` throwing when a project has no
`[data-hierarchy]` mount point and raised it against Story 23.3; this is the same shape on the epics index.
Its practical weight for Epic 16 is high — **the first thing an external adopter with a thin or non-BMad
planning tree sees is `errors=1` and a missing page.**

Note the dashboard already handles its own empty case *gracefully and says so* in the same run:

> `[specscribe] Dashboard "index.html" carries no [data-hierarchy] mount point, so no Hierarchy Explorer will
> render. That is EXPECTED for a project with no roadmap data to draw.`

So the correct behaviour is already modelled one component over. **Routed to Story 23.3** (which owns the
project-independence defect class per ADR 0022 §Context) with **Story 16.7 (preview cut) as the gate** — a
preview that errors on thin repositories is not shippable.

A second, smaller observation from the same run: SpecScribe reports `the renderer answered HTTP 500` and
discards the renderer's actual error text. The message above was only obtainable by booting the artefact by
hand. For a packaged consumer with no `web/` checkout that diagnostic path does not exist. **Routed to
Story 16.3.**

### 4.2 `NuxtPrerender.FindRepoRoot` does not recognise a git worktree

```csharp
while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
```
— `src/SpecScribe/NuxtPrerender.cs:132`

In a git worktree `.git` is a **file**, not a directory (measured: 56 bytes in this worktree). `Directory.Exists`
returns false, so the walk continues past the worktree root and lands on the **main checkout**. Observed
directly: a `generate` run from `.claude/worktrees/story-16-1-dev` resolved candidate 3 to
`C:\Dev\SpecScribe\web\.output` — a different checkout's artefact.

Impact is developer-path only (candidate 3), never the packaged path (candidate 2 wins first). But it is
newly reachable: CLAUDE.md still records that this machine cannot run parallel worktrees, while
`git worktree list` shows **five** in active use and the last four commits on `main` are worktree merges.
Silently rendering from another checkout's artefact is precisely the wrong-answer-with-a-success-status class
this codebase engineers against. **Routed to Story 16.3** (it owns `NuxtPrerender` resolution work), and
CLAUDE.md's "worktrees are not available" statement is now stale and should be corrected.

---

## 5. Credential and prerequisite inventory (AC #2)

All three mechanisms **re-verified live 2026-08-07**, as Task 4 requires. Two of the three had moved since
the story was seeded, and one had moved in a way that changes the decision.

### 5.1 The inventory is a list of one-time owner configurations, not a list of secret names

| channel | 2026 mechanism | what is stored in this repository | who rotates |
|---|---|---|---|
| **nuget.org** | **Trusted Publishing.** `NuGet/login@v1` exchanges a GitHub OIDC token for a **1-hour, single-use** API key. Needs `permissions: id-token: write` + a policy on nuget.org. | **nothing** | n/a — no credential exists to rotate |
| **npm** (16.8) | **Trusted Publishing**, GA 2025-07-31. Needs **npm CLI ≥ 11.5.1 _and_ Node ≥ 22.14.0**, `id-token: write`. Publishes provenance attestations **by default**. Must **not** set `NODE_AUTH_TOKEN`. | **nothing** | n/a |
| **VS Marketplace** (16.5) | See § 5.3 — **the PAT path is already closed for a new publisher** | n/a for the preview (channel is out) | n/a |

**AC #2's "no secret value is committed" is satisfied, and for the two shipping channels it is _structural_**
— there is no secret to commit, not merely a discipline of not committing one. That is a stronger property
than the AC asked for and is worth stating in those terms.

### 5.2 Three NuGet Trusted Publishing details the seeded research did not carry

Read from `learn.microsoft.com/nuget/nuget-org/trusted-publishing` (doc `updated_at: 2026-08-03`):

1. **Rollout is still gradual.** *"If you don't see the Trusted Publishing option in your nuget.org account,
   it might not be available to you yet."* This is a **prerequisite the owner must confirm before 16.4 wires
   anything** — the fallback is a classic API key, which reintroduces a stored secret and changes AC #2's
   answer for the NuGet channel.
2. **Policies on private repos start "temporarily active" for 7 days** and go inactive if no publish occurs
   in that window (NuGet needs the repo/owner IDs from a real publish to lock the policy against resurrection
   attacks). This repository is public, so it does not bite — recorded because it would silently break a
   later private fork.
3. **Policy ownership is user-or-organization and is load-bearing.** An org-owned policy goes inactive if its
   creator leaves the org. The `NuGet/login@v1` action also takes a `user:` input which is the nuget.org
   **profile name, not the email address**.

### 5.3 VS Marketplace — the finding that changes the decision

R3 framed this as *"PAT today; the migration is a live, dated risk"* with 2026-12-01 as the deadline. **The
deadline that matters already passed.** Verified 2026-08-07:

| fact | source |
|---|---|
| Creation of new global PATs **and regeneration of existing ones** was **blocked on 2026-03-15** | Azure DevOps Blog, *Retirement of Global Personal Access Tokens* |
| All existing global PATs **fully decommissioned 2026-12-01** | same |
| `vsce` requires a PAT scoped to **"All accessible organizations"** with **Marketplace (Manage)** | VS Code, *Publishing Extension* |
| The VS Code docs themselves now say to migrate: *"On December 1, 2026, global Personal Access Tokens (PATs) in Azure DevOps are retired. To keep publishing extensions, use secure automated publishing with Microsoft Entra ID instead of PATs."* | same |
| `microsoft/vscode-vsce#1023` — `--azure-credential` with a federated service principal fails with *"You need to be logged in with your corporate credentials"* — **Closed, `not_planned`** (opened 2024-07-17, last updated 2024-11-05) | GitHub API |

**"All accessible organizations" _is_ the global PAT shape.** So the two facts compose: the token `vsce`
documents is the token whose creation has been blocked for five months. Unless the owner is already holding a
valid global PAT issued before 2026-03-15, **there is no PAT path available at all** — and even that token
dies on 2026-12-01.

**Decision — option (c) with a piece of (b): the VSIX drops out of the preview cut, and when Story 16.5 runs
it targets an ORGANIZATION-owned publisher with Microsoft Entra workload identity federation.** Rationale:

- Option (a), "PAT now with a dated migration item", **is not available** on the evidence above.
- Option (b) alone — org publisher from the start — is right but insufficient on its own, because 16.5 is
  blocked on Epic 6 anyway and the preview cannot wait for it.
- Choosing the organization publisher **now, as a decision** rather than at 16.5 time is the load-bearing
  half: #1023 reports federated service principals failing specifically on a **personally-owned** publisher,
  and a publisher's ownership cannot be casually changed after extensions are published under it.

**Seated against Story 16.5**, with the Story 6.8 Workspace-Trust posture (R9) as its stated prerequisite.

### 5.4 Package-ID reservations — still unclaimed, re-verified today

| ID | endpoint | status 2026-08-07 |
|---|---|---|
| `SpecScribe` | `api.nuget.org/v3/registration5-gz-semver2/specscribe/index.json` | **404** |
| `SpecScribe` | `nuget.org/packages/SpecScribe` | **404** |
| `specscribe` | `registry.npmjs.org/specscribe` | **404** |
| `specscribe-win32-x64` | `registry.npmjs.org/specscribe-win32-x64` | **404** |

Unchanged since ADR 0022 recorded the first of these on 2026-07-27 — now eleven days of a public roadmap
naming both strings, still unreserved. **Owner action, prioritized first** (§ 8).

The full npm name set the wrapper needs, per § 2.5:
`specscribe`, `specscribe-renderer`, and `specscribe-win32-x64` / `specscribe-linux-x64` / `specscribe-darwin-arm64`.
(npm platform packages conventionally use Node's `process.platform`/`process.arch` names — `win32`, `darwin` —
not .NET RIDs; 16.8 owns the exact mapping.)

**Fallback IDs if either base name is taken before reservation:** `specscribe-cli` on npm (and the matching
`specscribe-cli-<platform>-<arch>` set), `SpecScribe.Cli` on nuget.org. Recorded now so the decision is not
made under time pressure by whoever discovers the squat.

### 5.5 Code-signing decision (AC #2 requires one)

**Neither Authenticode nor macOS notarization for the preview. Stated, not defaulted.**

What preview users will actually see, said plainly:

- **Windows:** an unsigned single-file `.exe` downloaded from GitHub Releases carries Mark-of-the-Web and
  triggers **SmartScreen "Windows protected your PC"**, requiring *More info → Run anyway*. Some AV engines
  additionally flag single-file .NET hosts on extraction heuristics — ADR 0022 §Alternatives already flagged
  the dropper heuristic for the related bundle-a-JS-runtime shape.
- **macOS:** an unsigned, un-notarized binary is blocked by Gatekeeper; the user must clear the quarantine
  attribute by hand.
- **The two channels that dodge this entirely are the two leading the cut.** `dotnet tool` and `npx` install
  through package managers, so neither triggers SmartScreen or Gatekeeper. This is an additional, unplanned
  argument for the § 3.1 ordering.

**Consequence accepted:** the direct-download binary is the roughest preview experience, and the docs must
say so rather than let a user discover it (Story 16.6). Revisit at 1.0, when a signing certificate is
justifiable — an Authenticode OV certificate is a recurring annual cost against a preview with no users yet.

### 5.6 The npx install-time Node check (ADR 0022 open question 1, owner-assigned)

**Decision: no install-time check. Keep the generate-time check only.**

- npm invokes npx, so Node is present **by construction** for this channel (ADR 0022 §Decision 5). The check
  would be asking whether Node exists in a process Node is running.
- The real risk is not absence but **version**: a user on Node 20 running `npx specscribe`. That is already
  covered — `NuxtPrerender.SupportedNodeRange` asserts `^22.19.0 || ^24.11.0 || >=26.0.0` with an actionable
  message (`NuxtPrerender.cs:41,141-216`).
- The cheap, non-executing half **is** worth taking: declare `engines.node` on the npm wrapper so npm warns
  at install time without a postinstall script. **A postinstall script is explicitly rejected** — it runs
  arbitrary code on install, is disabled outright by `--ignore-scripts` and many corporate configs, and would
  make the failure appear at install time for a tool that may never be run.

**Routed to Story 16.8** as an `engines` field, not a script.

---

## 6. Versioning, changelog and preview promises (AC #3)

### 6.1 NFR9 reproducibility — scoped, and one gap is worse than recorded

R6 named three gaps. All three still stand at `838d591`, verified:

1. `<Version>0.1.0-preview` is a hand-edited literal — `SpecScribe.csproj:19`
2. No workflow sets `SOURCE_DATE_EPOCH`, though the csproj honours it — `SpecScribe.csproj:28,36-37`
3. No `<Deterministic>` / `ContinuousIntegrationBuild` property anywhere — confirmed by grep, no match

**Which reading of "reproducible" the preview claims: the weaker one — _built from a clean checkout by CI_.**
NFR9's own wording (*"produced by CI from a clean checkout; publishing … gated on a passing build + test
run"*) supports it, and byte-identical rebuilds would require all three gaps closed plus a deterministic Nuxt
build, which is a separate and much larger problem. **Said explicitly so no reader assumes the stronger one.**

**But a fourth gap was found this session, and it breaks even the weak reading:**

> **`npm ci` fails at `838d591`.**
> ```
> `npm ci` can only install packages when your package.json and package-lock.json … are in sync.
> Missing: @emnapi/runtime@1.11.3 from lock file
> ```
> Reproduced on a **fresh worktree checkout** with npm 11.16.0 / Node 24.18.1.

The lockfile records `@napi-rs/wasm-runtime@1.1.6` with only `@tybys/wasm-util` as a dependency, while the
registry's current manifest for that version also declares `@emnapi/runtime@^1.7.1`. Three CI steps run
`npm ci` (`build-test-analyze.yml:246,416`, `publish-docs-live-pages.yml:89`), and its own comment says
*"the lockfile is the pin, and a lockfile-drifting install in CI would make the build unreproducible."*

**Attribution, honestly:** CI pins Node **24.11.1** via `web/.nvmrc`; this machine ran **24.18.1** with a
newer bundled npm. The failure may therefore be npm-version-specific and CI may still be green — **this
session could not check, because `gh` is not installed on this machine.** Recorded as
**unverified-on-CI**, not as "CI is broken". Either way it is a real developer-onboarding failure today:
a contributor on a Node version *this project's own `engines` field permits* cannot run `npm ci`.

**Routed to Story 16.2**, which owns the CI gate, as a required pre-check before 16.4 depends on `npm ci`
in a release pipeline.

**What the preview closes vs. defers:**

| gap | preview | owner |
|---|---|---|
| `<Version>` from the tag | **CLOSED** — § 6.2 | Story 16.3 |
| `npm ci` reproducibility | **CLOSED** (must be, it blocks the pipeline) | Story 16.2 |
| `SOURCE_DATE_EPOCH` set by the release workflow | **CLOSED** — cheap, the csproj already honours it | Story 16.4 |
| `<Deterministic>` / `ContinuousIntegrationBuild` / SourceLink | **DEFERRED** to post-preview | unowned → 17.4's burndown |
| byte-identical Nuxt rebuilds | **DEFERRED**, explicitly out of scope | — |

### 6.2 The scheme, and how `<Version>` derives from the tag

**Scheme: `0.MINOR.PATCH-preview.N`, SemVer 2.0, staying in `0.x` for the whole preview.**

**Mechanism: MinVer.** Chosen over Nerdbank.GitVersioning and plain `-p:Version=`:

- **MinVer** derives the version from the nearest reachable git tag with no config file, no build task, and
  no committed state; a tag `v0.1.0-preview.1` produces exactly that version. One `PackageReference`, and
  `<Version>` is deleted rather than replaced by a second literal.
- **Nerdbank.GitVersioning** is more capable and needs a committed `version.json` — a second place a version
  lives, which is the thing R7 warns about.
- **Plain `-p:Version=` from the tag** works but only in CI; a local `dotnet pack` then produces `1.0.0` and
  the About page's Preview badge silently disappears (§ 6.4). Rejected for that asymmetry.

**Routed to Story 16.3** (its AC #1 already requires version-from-tag); the *choice* is this spike's, per R6.

### 6.3 How the CLI and its renderer are pinned as one unit (Story 16.9 AC #2 depends on this)

**They are pinned by construction, not by policy — the renderer is _inside_ the package.** § 2 makes this
structural for the `dotnet tool` and binary channels: there is no way to combine a CLI and a renderer from
different revisions, because there is only one artefact.

The npm channel is the **one place a mismatch is expressible**, because § 2.5 makes the renderer a separate
package. Therefore: **`specscribe` depends on `specscribe-renderer` with an exact-version pin (`=X.Y.Z`, not
`^`), and both are published from the same tag in the same pipeline run.** The per-RID binary packages take
the same exact pin. This is the rule Story 16.9 AC #2 inherits, and it is the rule 16.8 must implement.

Story 16.9 AC #2's reasoning is why the pin is exact rather than caret: a portal rendered from a mismatched
pair *"fails as wrong output rather than as an error"* — and a caret range is precisely a licence to drift.

### 6.4 The four existing version numbers, and what happens to each (R7)

| where | today | after this policy |
|---|---|---|
| `src/SpecScribe/SpecScribe.csproj:19` | `0.1.0-preview` | **Deleted.** MinVer supplies it from the tag. |
| `extension/package.json:5` | `0.1.0` | **Unchanged for the preview** — the VSIX is out of the cut. When 16.5 runs: the Marketplace has no SemVer pre-release concept, so the version stays plain `0.1.0` and pre-release status is carried by the Marketplace's own **Preview flag** + `vsce publish --pre-release`. Recorded so nobody "fixes" it into `0.1.0-preview` and breaks the Marketplace parse. |
| `README.md:260` | hard-coded `0.1.0-preview` inside the published CI recipe | **Story 16.6's**, not fixed here (Dev Notes excludes README deliberately). Story 16.9 replaces this recipe with the composite Action, which should take the version as an input defaulting to the Action's own tag — removing the literal rather than updating it. |
| npm wrapper + platform + renderer packages | do not exist | Created at the same version from the same tag, exact-pinned (§ 6.3) |

**The pre-release label is a rendered product surface, not cosmetics.** `AboutTemplater.cs:133-135` renders a
`Preview` badge whenever the version is a pre-release (`meta.IsPrerelease`), parsed by
`ParseInformationalVersion` at `AboutTemplater.cs:90`. **Dropping `-preview` silently removes a user-visible
badge.** The policy therefore requires: *every preview release carries a SemVer pre-release label, and the
first release that does not is by definition no longer a preview.*

### 6.5 Changelog

**Keep a Changelog 1.1.0 format, `CHANGELOG.md` at the repository root, updated by hand in the story that
makes the change — not generated from commits.**

`CHANGELOG.md` **does not exist yet** (verified). Rationale for hand-authored: this repository's commits
routinely bundle several stories (CLAUDE.md §Concurrent work), so generated release notes would be
structurally misleading — the commit is not the unit of change here, the story is. Sections:
`Added / Changed / Deprecated / Removed / Fixed / Security`, newest first, with an `[Unreleased]` heading.

The release pipeline (16.4) copies the released version's section into the GitHub Release body; it does not
author it. **Story 16.6 AC #1's "a `CHANGELOG.md` following the Story 16.1 format" is satisfied by this
section.**

### 6.6 What "preview" promises, and what it does not

**Promises:**
- It generates a portal from a supported SDD repository, and the published channels install and run.
- Output is read-only with respect to your repository (AD-6).
- Breaking changes are **recorded in `CHANGELOG.md`** and carry a minor-version bump.

**Does NOT promise:**
- **API/CLI stability.** Inside `0.x`, a **minor** bump may break: SemVer assigns no compatibility guarantee
  below `1.0.0` and this policy uses that latitude deliberately rather than pretending otherwise.
- **Output-format stability.** The IR is versioned (ADR 0008 / ADR 0034) and `SpaDelivery.SchemaVersion` has
  already moved once; a consumer building on the IR should pin.
- **Support or an SLA.** Issues are best-effort.
- **That it works without Node.** The Node prerequisite (`^22.19.0 || ^24.11.0 || >=26.0.0`) is a
  **consumer-facing condition of use**, not an implementation detail — ADR 0022 §Decision 5, and it is stated
  in `README.md:92-98` but **nowhere a packaged consumer sees it** (R5). Story 16.6 owns the NuGet listing,
  npm README and Marketplace listing carrying it.
- **Signed binaries.** § 5.5.

### 6.7 Node prerequisite check — placement (R5's open half)

ADR 0022 §Decision 5 words it as *"The binary detects Node **at startup**"*. The shipped check runs at
**prerender time**, inside `NuxtPrerender` (`NuxtPrerender.cs:141-216`).

**Decision: the shipped placement stands; ADR 0022's wording is amended to match it, not the reverse.**
"At startup" would move a subprocess spawn into every invocation, including `--help` and `--version`, to
warn about a dependency that only the prerender path needs. The user-visible difference is that the message
arrives after ingest rather than immediately — on the order of a second on this repository, and ingest is
not destructive. The cost of the alternative is paid on every run; the cost of the status quo is paid once,
by a user who is about to hit the error anyway.

**This is an amendment to a ratified-pending ADR and is recorded as such in ADR 0040 §Amends.**

---

## 7. Scope guard and regression floor (AC #6)

### 7.1 No product code

`git status --porcelain src/ tests/ web/ extension/` → **empty**. The temporary `SpecScribe.csproj` probe
edit (§ 2.1) was reverted and the revert verified by that command, not assumed from the edit having been made.

No file under `src/`, `tests/`, `web/` or `extension/` appears in this story's File List.

### 7.2 Gates

| gate | result |
|---|---|
| `dotnet test SpecScribe.slnx` | **2,962 passed / 1 failed / 3 skipped** — see below |
| `npm run check:tokens` | **OK** — 45 tokens across 2 `:root` blocks |
| `npm run check:ir-content` | **OK** — 1,457 rules + 4 keyframes scoped, 1 shared primitive unscoped |
| `npm run check:assets` | **OK** — 4 runtime assets in sync |
| `npm run check:parity` | **OK** — 24 pinned routes across 14 families byte-identical |

**The one test failure is a known-class flake, and causality was established rather than assumed.**
`FileWatcherServiceTests.EditingAStoryFile_RegeneratesThroughTheOrdinaryMarkdownRoute` failed with
`JsonReaderException: The input does not contain any JSON tokens` — the test read a region JSON file
mid-write. Re-run in isolation: **11/11 passed**. This story changed no product code (§ 7.1), and this
class has a recorded flake history in this repository (`FileWatcherServiceTests.BurstOfSaves`, and the
git-SPAWN-starvation preview-server pattern). The machine was concurrently running dotnet and Node builds.

**`check:ir-content` went red twice before it went green, and _regenerating the baseline would have been the
wrong move both times_.** This is § 2.7's fourth wrong-first and it deserves its own note because CLAUDE.md
names exactly this trap:

1. **First red: "could not re-derive the layer".** A fresh worktree has never run a generate, so
   `SpecScribeOutput/spa/manifest.json` did not exist. An *environmental precondition*, not drift.
2. **Second red: `+4 rules / -185`** after a plain `generate`. The pruned rules were the deep-analytics
   surfaces — `.insight-panel`, `.code-insight-history`, `.ss-relgraph-*` — and the four *added* were
   empty-state rules. **`build-test-analyze.yml:281-290` documents this exact signature in advance**:
   *"reproduced locally as `+4 / -182` without the flag and `+0 / -0` with it."* The gate requires
   `--deep-git`, because `extract:ir-content` prunes any selector it cannot find in the IR and a shallow run
   emits a narrower corpus than the published site.
3. **Green** after `dotnet run --project src/SpecScribe -- generate --deep-git`.

Had `npm run extract:ir-content` been run at step 2 — which is what the gate's own failure message
suggests — **185 rules would have been deleted from the shipped stylesheet layer and the gate would have gone
green**. The measured `-185` here versus the workflow's documented `-182` is corpus growth, not a new
defect.

### 7.3 Concurrency

This ran in a dedicated worktree (`worktree-story-16-1-dev`, branched from `838d591`), so the shared tree was
never written to. No `git reset --hard`, `git checkout --` or `git clean` was used at any point. No
concurrent session's changes were reverted or absorbed; nothing needed to be attributed to one, because the
only tracked file this story modified outside its own deliverables is `sprint-status.yaml`.

---

## 8. Owner actions

Ordered by urgency. The dev agent inventoried these and performed none of them.

1. **Reserve `SpecScribe` on nuget.org and the five npm names in § 5.4** — both verified unclaimed again
   today; this is the only item on this list that a third party can take away from you.
2. **Confirm Trusted Publishing is visible in your nuget.org account** (§ 5.2 — still a gradual rollout).
   If it is not, AC #2's "no stored secret" answer changes for the NuGet channel and 16.4 needs an API key
   path instead. **Check this before 16.4 starts, not during.**
3. **Configure the nuget.org trusted-publishing policy** (repo owner + repo + workflow *filename only* +
   optional environment), and note the `user:` input is your nuget.org **profile name**, not your email.
4. **Configure the npm trusted-publishing policy**, explicitly selecting allowed actions (required for
   policies created after 2026-05-20).
5. **Decide organization-vs-personal for the VS Marketplace publisher — before Story 16.5 wires anything**
   (§ 5.3). The recommendation is organization. This is effectively irreversible once extensions publish
   under it.
6. **Ratify ADR 0040** (AC #4). It is authored and complete at `Proposed`; ratification is yours.
7. No signing certificate is needed for the preview (§ 5.5) — listed so its absence is a decision on the
   record rather than an omission.

---

## 9. Epic sequencing — what this spike unblocks or changes (Task 8)

**No structural scope change.** No story is added, removed or renumbered, so per CLAUDE.md neither
`epics.md` nor `sprint-status.yaml` needs a structural edit — this spike refines ACs *within* existing
stories only. Recorded explicitly, as Task 8 requires, so the absence of an `epics.md` diff is a decision
rather than an oversight.

| story | what changes |
|---|---|
| **16.2** | Required-check string is the **job name verbatim: `build-test-analyze`**. `portability-probe (ubuntu, non-gating)` must **NOT** be required. Do **not** create a second build+test workflow. **NFR9's gate-on-a-tag question is answered: require the tagged commit to already be green on `main`** rather than re-running build+test in the release job — the tag points at a commit `main` already validated, and re-running invites a different result from the same source. **Plus a new blocker: `npm ci` fails locally at `838d591` (§ 6.1); 16.2 must verify CI's status and fix the lockfile.** |
| **16.3** | Packaging shape is decided and proven (§ 2) — implement the `renderer/**` pack item and the sibling copy for the binary. Version-from-tag via **MinVer** (§ 6.2). **NOT Node detection** — it shipped in 23.6 (R5); only the *placement* question was open and § 6.7 closes it. Also inherits: the swallowed HTTP-500 renderer diagnostic (§ 4.1) and the worktree `FindRepoRoot` defect (§ 4.2). |
| **16.4** | Add the `npm run build:package` stage (ADR 0022 §Consequences and 23.5 open item 4 both assign it here). Set `SOURCE_DATE_EPOCH`. Publish via Trusted Publishing with `permissions: id-token: write`. Copy the `CHANGELOG.md` section into the Release body. |
| **16.5** | Organization-owned publisher + Entra workload identity federation; **the PAT path is closed** (§ 5.3). Prerequisite: Story 6.8's Workspace-Trust posture. Confirm whether `"private": true` blocks `vsce package` — **not confirmed by this spike**, it is 16.5's to check on a manifest it owns. |
| **16.6** | Owns `CHANGELOG.md` in the § 6.5 format. Owns surfacing the **Node prerequisite where a packaged consumer sees it** — NuGet listing, npm README, Marketplace listing (R5's second open half). Owns the `README.md:260` version literal. |
| **16.7** | The preview cut is § 3.1, gated by Story 17.4. **Add a gate: § 4.1's thin-repository `errors=1` must be fixed first.** |
| **16.8** | RID matrix = `win-x64` / `linux-x64` / `osx-arm64` (§ 3.2). Renderer is **one shared package**, not per-RID (§ 2.5), exact-pinned (§ 6.3). Node check = an `engines` field, **not** a postinstall script (§ 5.6). |
| **16.9** | Its stated dependency — the renderer being *in* the published package — is now proven satisfiable (§ 2). The Action collapses to install-and-run once 16.3 ships. Inherits the exact-pin rule (§ 6.3). |
| **23.3** | § 4.1's `EpicsIndexSurface.vue` throw, same class as the `DashboardSurface.vue` defect it already owns. |
| **17.4** | Inherits: deferred `<Deterministic>`/SourceLink (§ 6.1), and the preview promises in § 6.6 as the sign-off checklist. |

---

## 10. Open items

| # | item | state | owner |
|---|---|---|---|
| 1 | `EpicsIndexSurface.vue` hard-throws on a project with no epics | reproduced twice | **Story 23.3**, gating **16.7** |
| 2 | `FindRepoRoot` does not detect git worktrees (`.git` as a file) | reproduced | **Story 16.3** |
| 3 | `npm ci` fails at `838d591` locally; **CI status unverified** (`gh` not installed on this machine) | **unverified-on-CI** | **Story 16.2** |
| 4 | SpecScribe discards the renderer's error text behind "HTTP 500" | observed | **Story 16.3** |
| 5 | npx end-to-end install proof (§ 2.5 decided the shape from a measured property; no wrapper was published) | **unmeasured** | **Story 16.8** |
| 6 | `linux-arm64` / `osx-x64` RIDs | deferred by decision | 16.8, on demand |
| 7 | `extension/package.json`'s `"private": true` vs. `vsce package` | **unconfirmed** — deliberately not tested; R9 forbids editing the manifest here | **Story 16.5** |
| 8 | Trusted Publishing visibility on the owner's nuget.org account | **unknown** — cannot be checked without the account | **owner** (§ 8 item 2) |
| 9 | CLAUDE.md states worktrees are unavailable on this machine; five are in active use | stale documentation | owner / next retro |
| 10 | `<Deterministic>` / `ContinuousIntegrationBuild` / SourceLink | deferred past preview | 17.4 burndown |

---

## 11. Deliverables

- **This report** — `_bmad-output/implementation-artifacts/16-1-spike-report.md`
- **ADR 0040** — `docs/adrs/0040-release-channels-and-versioning-policy.md` (**Proposed**; ratification is
  owner action § 8 item 6). **0039 was NOT free** — Story 4.9 claimed it on 2026-08-06, which the story file
  told us to verify rather than assume. 0019 remains claimed-but-unwritten by Story 18.3.
- **One index line** in `docs/adrs/README.md`
- **No `spike/release/**`** — the probe needed no committed throwaway code; it was six shell commands and a
  reverted csproj item, all reproduced verbatim in § 2.1.
