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

**No shipping channel needs a stored secret** — *on the Trusted Publishing happy path.* nuget.org and npm
both exchange a GitHub OIDC token, and GitHub Releases uses the workflow's own per-run `GITHUB_TOKEN`, which
makes AC #2's "no secret value is committed" structural rather than a matter of discipline. ⚠️ **The caveat
belongs in the headline, not below it** (code review 2026-08-07): nuget.org's Trusted Publishing is still a
gradual rollout, and if it has not reached the owner's account the NuGet channel falls back to a stored
`NUGET_API_KEY` (§ 5.1). Confirm before Story 16.4. *This sentence previously read "Two of three channels
need no stored secret at all" — it omitted the GitHub Releases channel entirely and carried no caveat.*

**NFR9 "reproducible" is claimed in its weaker reading only** — built from a clean checkout by CI — and it was
**not true when this report was written**: `npm ci` failed at `838d591`. ✅ Repaired since by `0b1f561`, so
the weak reading now holds. It was a live finding, not a theoretical gap.

---

> ⚠️ **Reviewed 2026-08-07 (`bmad-code-review`).** Three adversarial layers over commit `9837e67`; 44
> findings after dedup. Corrections are marked inline throughout this report with the date. Two claims in
> this report were found **false** and are corrected in place: *"`gh` is not installed on this machine"*
> (§ 6.1) and *"Story 4.9 claimed 0039"* (§ 11). The measured packaging results, the credential findings and
> the arithmetic throughout all **stand** — they were re-verified independently.
>
> ✅ **Its nine open items were closed on 2026-08-07** by the dev-story pass that followed. **Eight are
> resolved as decisions in ADR 0040** and tracked in § 10 (items 11–14, 17–19) — release atomicity, the CI
> gate's lookup rule and hotfix scope, the MinVer bootstrap, version-component semantics and the `0.x` exit
> criterion, extension versioning, changelog contention, package-ID escalation, and the `EpicsIndexSurface`
> gate's ownership. One structural correction came with them: this spike **did** create a cross-epic blocking
> edge (23.3 → 16.7), so it now lands in `epics.md` and `sprint-status.yaml` as CLAUDE.md requires (§ 9).
>
> **The ninth is not a decision but an act — ADR 0040's ratification (AC #4) — and it remains open**, because
> an agent cannot ratify on the owner's behalf. Note also that the MinVer item was closed largely by
> *implementation*: **Story 16.3 has shipped since this report was written**, taking § Decision 1's pack item
> and § Decision 5's MinVer derivation into the tree. Where this report and the live tree disagree, the tree
> is newer — §§ 6.1–6.4 are read as the evidence behind the decision, not as the current state.

---

## 1. Method and provenance

Every figure below is marked. Nothing in this report is an estimate presented as a measurement.

| figures | provenance |
|---|---|
| artefact file count / bytes / gzipped tarball / native-binding count | **Session-measured**, Windows 11 Pro 10.0.26200, Node v24.18.1. Commands inline in **§ 2.1** *(corrected 2026-08-07 — this cell previously pointed at § 3, which carries no commands)*. |
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

**Two provenance limits added by the code review of 2026-08-07, because the table above did not carry them.**

1. **Every session-measured figure is from a single Windows 11 / `win-x64` machine.** `PublishSingleFile` was
   exercised on `win-x64` only, and the pack used a backslash `PackagePath`. § 3.1's RID matrix is **three**
   (`win-x64`, `linux-x64`, `osx-arm64`) and ADR 0040 § Decision 1 generalizes the `AppContext.BaseDirectory`
   conclusion to all of them — so **two of the three shipped RIDs rest on extrapolation, not measurement**,
   as do both non-Windows packing hosts and any case-sensitive filesystem. Deferred to Stories 16.3/16.4 to
   verify on Linux/macOS runners; `build-test-analyze.yml` already has an `ubuntu-latest` job to hang it on.
2. **The probe repository's composition is documented below only for the *second* probe.** See § 2.1.

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

⚠️ **Recorded by the code review of 2026-08-07: there were TWO probe repositories, and only one is described
above.** § 2.7 (3) and § 4.1 report a **first** probe repository that *"had no parseable epics"* and produced
**21 route(s)**; the run reported in § 2.2 produced **373**. The ~18× larger corpus that generated the
headline number is therefore not characterised anywhere — not its path, not its content, and not
independently whether it too was `web/`-free (the assertion above is written as covering "each run", but
names only one repository). Since AC #5's conclusion is *"measured from a repository that is not this one"*,
that composition is load-bearing evidence.

**What this does and does not undermine.** It does **not** undermine the foreignness of the probe: § 2.3's
negative case proves resolution candidate 3 (`web/.output` at a repo root) did not rescue the run, which is
the property AC #5 actually turns on. It **does** mean the 373-route figure and its 4.9 ms/route derivative
cannot be reproduced from this report alone. Story 16.3 should re-derive both against a named corpus rather
than quoting them.

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
plausible, and **one of the four — (1) — produced a genuinely green-looking outcome**.

*Corrected 2026-08-07: this sentence previously claimed "three of the four". It does not hold. (2) threw a
stack trace, (3) reported `errors=1` and a failed route, and (4) turned the gate **red**. The danger in (4)
was that the gate's own **suggested remedy** would have gone green while deleting 185 rules — which is a
sharper point than the original claim, and is lost by overstating the count.*

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

**Two consequences of this finding were left un-acted-on and are now seated (code review 2026-08-07):**

1. **A completeness assertion is required at packaging time**, not a size-and-count check — this finding is
   the proof that counts certify nothing. ADR 0040 § Decision 1 now makes asserting the **entry point exists
   at its packed path** a release-job obligation for Story 16.4. It compounds with § 4.1's second
   observation: SpecScribe reports *"the renderer answered HTTP 500"* and **discards the renderer's actual
   error text**, so an incomplete payload reaches the consumer as an unexplained failure.
2. **`net10.0` must not be a literal.** The exact string proven here is now recorded normatively in ADR 0040
   § Decision 1 with `$(TargetFramework)` substituted, because the project sets `<RollForward>Major</RollForward>`
   and a TFM bump would otherwise relocate the assembly to `tools/net11.0/any/` while the payload stayed
   behind — the same silent, exit-0 failure class as this finding.

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

### 4.3 🔴 `main`'s CI is red on a REQUIRED check — a stale 12-byte number in a committed manifest *(found 2026-08-07, second pass)*

**Not environmental, not this story's, and it blocks the release preflight this very ADR defines.**

`check:ir-content` fails on `main`. Causality was established **before anything was touched**, per CLAUDE.md
§ Concurrent work, and **no baseline was regenerated**:

| step | finding |
|---|---|
| Is it mine? | **No.** `git status --porcelain src/ tests/ web/ extension/` in the decisions worktree is **empty** — this pass changed no code. |
| Which artefact moved? | **Only the manifest.** `ir-content.css`, `shared-primitives.css` and `runtime-body.css` all diff to **zero bytes** against their committed versions. |
| Which field? | Exactly one: `generatedBytes` **186492 → 186504**. |
| Proof needing no environment | The **committed** `web/assets/ir-content.css` is **186,504 bytes** on disk; the **committed** manifest beside it claims **186,492**. The pair contradicts itself in the repository, with no IR, no build and no worktree involved. The regenerated value is the correct one. |
| Is it the usual worktree/pruning cause? | **No — disproven.** It reproduces **in CI**: run `31234945903` at `15336f4` fails with the identical sub-line, *"ir-content.manifest.json: out of sync with the sheet it documents."* |
| Since when? | Red at `c73ebcb` and at `15336f4`; **last green at `07bdb790`**. |
| Attributed to | **`3b085e7`** — Story 24.2's code review. Its own `sprint-status.yaml` note records the mechanism in advance: *"extraction reverted in favour of a surgical edit — **RE-VERIFY ON MAIN**."* The surgical edit moved `ir-content.css` by 12 bytes and left the field describing it untouched. The re-verify never happened. |

**Why this is worse than a red badge.** Story 16.2 made `build-test-analyze` a **required** check, so a red
`main` blocks every PR merge. And **ADR 0040 § Decision 9 — decided in this same pass — makes "the tagged
commit already passed on `main`" the release preflight.** While `main` is red, that preflight can never pass,
so **no release can be cut**. A twelve-byte staleness is currently a release blocker.

**Raised, not patched** — AC #6 forbids this story putting a `web/` file in its File List, and § 4.1/§ 4.2
already set this story's precedent of routing defects rather than patching them. **The fix is one command and
is safe by construction** (every CSS sheet is byte-identical; only the manifest stops misdescribing them):

```sh
cd web && npm run extract:ir-content   # then commit — see § 8 action 8
```

---

## 5. Credential and prerequisite inventory (AC #2)

All three mechanisms **re-verified live 2026-08-07**, as Task 4 requires. Two of the three had moved since
the story was seeded, and one had moved in a way that changes the decision.

### 5.1 The inventory is a list of one-time owner configurations, not a list of secret names

| channel | 2026 mechanism | what is stored in this repository | who rotates |
|---|---|---|---|
| **nuget.org** | **Trusted Publishing.** `NuGet/login@v1` exchanges a GitHub OIDC token for a **1-hour, single-use** API key. Needs `permissions: id-token: write` + a policy on nuget.org. | **nothing** | n/a — no credential exists to rotate |
| **npm** (16.8) | **Trusted Publishing**, GA 2025-07-31. Needs **npm CLI ≥ 11.5.1 _and_ Node ≥ 22.14.0**, `id-token: write`. Publishes provenance attestations **by default**. Must **not** set `NODE_AUTH_TOKEN`. | **nothing** | n/a |
| **VS Marketplace** (16.5) | See § 5.3 — **the PAT path is already closed for a new publisher**. When 16.5 runs: Entra workload identity federation. | **nothing secret** — client ID + tenant ID as plain Actions *variables*; the trust is a federated credential on the Entra app registration | owner, on app-registration change |
| **GitHub Releases** (binaries, 16.4) | the workflow's own `GITHUB_TOKEN`, `permissions: contents: write` scoped to the release job | **nothing** — minted per run by Actions | n/a — never stored |

⚠️ **The GitHub Releases row was added by the code review of 2026-08-07.** The original table had three rows
and omitted it, while § 3.1 puts self-contained binaries **in the shipping cut** — so a channel that
publishes had no credential entry at all, which is precisely what AC #2 asks for. It stores nothing, but
"stores nothing" is an answer that must be given rather than left blank.

**Fallback storage, also added 2026-08-07.** § 5.2 item 1 notes the NuGet Trusted Publishing rollout is
gradual and that the fallback *"reintroduces a stored secret"* — without saying **where**. AC #2 asks that
question directly. Under the fallback: repository secret **`NUGET_API_KEY`**, scoped to the **`release`
environment** so PR workflows cannot reach it, rotated by the owner, set to nuget.org's shortest offered
expiry. Recorded now so 16.4 does not have to design it under time pressure at exactly the moment this
report says to stop and check.

**AC #2's "no secret value is committed" is satisfied, and for all three shipping channels it is
_structural_** — there is no secret to commit, not merely a discipline of not committing one. That is a
stronger property than the AC asked for and is worth stating in those terms. ⚠️ **Caveat that belongs
alongside the claim, not below it:** this holds **only on the Trusted Publishing happy path**. If the
nuget.org rollout has not reached the owner's account, the NuGet channel stores `NUGET_API_KEY` and the
headline weakens to *two* of three. Confirm before 16.4 (§ 5.2 item 1, open item 8).

**Where the exchange runs.** `NuGet/login@v1` must run **immediately before the push step, not at job
start**: the key lives one hour and is single-use, while the release job builds three ~76 MiB RIDs plus a
Nuxt artefact plus the suite ahead of it. A failed push **consumes** the key, so the retry must re-run the
exchange — and if the exchange itself fails, the job must abort **before** publishing to any channel, so no
partial release is created (ADR 0040 § Decision 10).

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

⚠️ **These are NOT drop-in replacements, and the original wording let them read as if they were** (code
review 2026-08-07). npx resolves by **package name**, so taking `specscribe-cli` silently changes the
product's primary documented invocation from `npx specscribe` to `npx specscribe-cli` — a string printed in
**ADR 0006 § Decision**, **`epics.md` § Story 16.8** (*"`npx specscribe` generated all 196 files with NO .NET
SDK present"*) and **`README.md`**. It also undercuts the npx channel's whole rationale, which is
low-friction invocation. **If a fallback is taken, those three documents change in the same act, and the
implementer escalates rather than substituting.** Now recorded normatively in ADR 0040 § Decision 12, since
Story 16.8 implements from the ADR and this section was not carried into it.

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

⚠️ **The unsigned channel needs a compensating integrity control, and this section shipped without one**
(code review 2026-08-07). The other two channels get integrity by construction — npm publishes **provenance
attestations by default** (§ 5.1) and NuGet carries the registry's own guarantees — so the *only* channel
with no signature also had no published digest. A consumer clicking through SmartScreen had nothing to
verify against. **Decided now, in ADR 0040 § Decision 2:** each release asset is published as
`specscribe-<version>-<rid>.zip` (Windows) / `.tar.gz` (Linux, macOS) with a **SHA-256 digest in the release
body**. Story 16.4 AC #1 requires attaching release artifacts but specified no archive format, per-RID naming
or digest scheme; that gap is now closed.

**This decision is also promoted into ADR 0040 § Decision 13.** AC #2 asked for a code-signing decision
explicitly, and it previously existed in the ADR only as a non-goals bullet plus a consequences line — which
is the "bury the decision in the story artifact" pattern CLAUDE.md § Decision records names.

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

Three CI steps run `npm ci` (`build-test-analyze.yml:246,416`, `publish-docs-live-pages.yml:89`), and its own
comment says *"the lockfile is the pin, and a lockfile-drifting install in CI would make the build
unreproducible."*

⚠️ **The root cause stated here was wrong in its specifics** (code review 2026-08-07). The original text
read: *"The lockfile records `@napi-rs/wasm-runtime@1.1.6` with only `@tybys/wasm-util` as a dependency,
while the registry's current manifest for that version also declares `@emnapi/runtime@^1.7.1`."* At
`838d591` the lockfile **did** declare it — as a `peerDependencies` entry
(`"peerDependencies": { "@emnapi/core": "^1.7.1", "@emnapi/runtime": "^1.7.1" }`). The actually-missing
artifact was the **top-level `node_modules/@emnapi/runtime` tree entry at 1.11.3**, which is exactly what the
fix added. The symptom was real and the routing was right; the diagnosis pointed Story 16.2 at the wrong half
of the file.

**Attribution, honestly:** CI pins Node **24.11.1** via `web/.nvmrc`; this machine ran **24.18.1** with a
newer bundled npm. The failure may therefore be npm-version-specific and CI may still have been green.
Recorded as **unverified-on-CI**, not as "CI is broken". Either way it is a real developer-onboarding failure:
a contributor on a Node version *this project's own `engines` field permits* cannot run `npm ci`.

⚠️ **The stated reason for not verifying was false** (code review 2026-08-07). The original text read
*"this session could not check, because `gh` is not installed on this machine."* **`gh` is installed**, at
`C:\Program Files\GitHub CLI\gh.exe` — it is simply not on `PATH`, and this project's own agent memory
records that fact together with the instruction to invoke it by full path. A checkable fact was declared
uncheckable, and it was the single item this report calls worse than the three gaps it was scoping — the one
that *"breaks even the weak reading"* of NFR9. The Node-version caveat above was legitimate; the
inability-to-check was not.

✅ **Resolved since.** Commit **`0b1f561`** ("CI fix: repair the lockfile and regenerate the two stale drift
gates") added the missing tree entry. **Routed to Story 16.2**, which owned the CI gate — and closed it. The
weak reading of NFR9 now holds.

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
  no committed state. One `PackageReference`, and `<Version>` is deleted rather than replaced by a second
  literal.

  ⚠️ **The worked example above was wrong, and two prerequisites were missing** (code review 2026-08-07).
  This bullet originally claimed *"a tag `v0.1.0-preview.1` produces exactly that version."* It does not —
  **MinVer's default `MinVerTagPrefix` is empty**, so a `v`-prefixed tag is not matched at all and the build
  falls through to `0.0.0-alpha.0.<height>`. Compounding it, **this repository currently has zero git tags**
  (verified 2026-08-07), so deleting `<Version>` before a bootstrap tag exists produces that same default on
  every build — and `README.md:260`'s published recipe installs `--version 0.1.0-preview`, which would then
  resolve nothing. Every failure mode here is **silent**: `dotnet pack` exits 0. Both prerequisites are now
  recorded as ⚠️ OPEN in ADR 0040 § Decision 5, and **Story 16.3 must not delete `<Version>` until they are
  settled.** Also unaddressed: MinVer appends commit height on untagged commits, producing
  `0.1.0-preview.1.7` — a **fourth** version shape outside the documented `0.MINOR.PATCH-preview.N` scheme,
  with no rule stating whether such a build may publish.
- **Nerdbank.GitVersioning** is more capable and needs a committed `version.json` — a second place a version
  lives, which is the thing R7 warns about.
- **Plain `-p:Version=` from the tag** works but only in CI; a local `dotnet pack` then produces `1.0.0` and
  the About page's Preview badge silently disappears (§ 6.4). Rejected for that asymmetry.

**Routed to Story 16.3** (its AC #1 already requires version-from-tag); the *choice* is this spike's, per R6.

### 6.3 How the CLI and its renderer are pinned as one unit (Story 16.9 AC #2 depends on this)

**They are pinned by construction for the `dotnet tool` channel — the renderer is _inside_ the package.**
There is no way to combine a CLI and a renderer from different revisions, because there is one artefact.

⚠️ **This does NOT hold for the self-contained binary, and the original text claimed it did** (code review
2026-08-07). § 2.4 defines that channel as a **sibling `renderer/` directory beside the executable** — two
filesystem objects, not one artefact. A user who unzips release N over release N−1, replaces only the `.exe`,
or downloads the exe and the renderer as separate release assets desynchronizes them; `ResolveArtefactDirectory`
tests only that `renderer/server/index.mjs` exists, and nothing stamps the artefact with a version. So the
direct-download channel can produce exactly the mismatched pair Story 16.9 AC #2 exists to prevent — the one
that *"fails as wrong output rather than as an error"* — and this report recorded that risk for npm only.
**Now closed in ADR 0040 § Decision 5:** each RID ships as a **single archive containing both halves**, and
Story 16.3 stamps the artefact with the CLI version and fails loudly on a mismatch.

The npm channel is the **one place a mismatch is expressible by version**, because § 2.5 makes the renderer a
separate package. Therefore: **`specscribe` depends on `specscribe-renderer` with an exact-version pin
(`=X.Y.Z`, not `^`), and both are published from the same tag in the same pipeline run.** The per-RID binary
packages take the same exact pin. This is the rule Story 16.9 AC #2 inherits, and it is the rule 16.8 must
implement.

**Publish order is normative and was unspecified** (code review 2026-08-07): **`specscribe-renderer` FIRST,
then the wrapper.** npm has no multi-package transaction, so the reverse order makes `specscribe@X.Y.Z`
installable while its exact dependency does not exist — `npx specscribe` then fails at install for every
user, with no remedy (the version is burned and npm's unpublish window is limited). The chosen order fails
safe: a renderer published without its wrapper is merely an orphaned version.

**Channel parity is not promised, and 16.9 needs an answer.** The cut is ordered across separate stories, so
a version resolvable on nuget.org may not exist on npm. ADR 0040 § Decision 2 now names **nuget.org as the
authoritative channel** for "a released version", which is what Story 16.9's Action resolves and echoes
against.

Story 16.9 AC #2's reasoning is why the pin is exact rather than caret: a portal rendered from a mismatched
pair *"fails as wrong output rather than as an error"* — and a caret range is precisely a licence to drift.

### 6.4 The four existing version numbers, and what happens to each (R7)

| where | today | after this policy |
|---|---|---|
| `src/SpecScribe/SpecScribe.csproj:19` | `0.1.0-preview` | **Deleted.** MinVer supplies it from the tag. |
| `extension/package.json:5` | `0.1.0` | **Unchanged for the preview** — the VSIX is out of the cut. When 16.5 runs: the Marketplace has no SemVer pre-release concept, so the version stays plain `0.1.0` and pre-release status is carried by the Marketplace's own **Preview flag** + `vsce publish --pre-release`. Recorded so nobody "fixes" it into `0.1.0-preview` and breaks the Marketplace parse. ⚠️ **OPEN (code review 2026-08-07):** as written this permits **exactly one VSIX publish ever** — the Marketplace requires each publish to carry a strictly greater version, and "stays plain `0.1.0`" gives 16.5 no way to ship a second. It also leaves the extension version fully decoupled from the MinVer-derived CLI version with **no stated correspondence rule**. Owner decision needed before 16.5. |
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

**Three gaps closed by the code review of 2026-08-07** (all now normative in ADR 0040 § Decision 6):

1. **Breaking changes had no marked home.** Keep a Changelog's six sections contain no `Breaking`, yet § 6.6
   promises breaking changes *are recorded* — so they would land under `Changed`/`Removed`, indistinguishable
   from routine entries, defeating the one guarantee the preview offers. **A breaking entry stays in its
   natural section prefixed `**BREAKING:**`**, making it greppable and unmistakable.
2. **An empty `[Unreleased]` section had undefined pipeline behaviour** — a re-cut, a CI-only fix or a
   dependency bump may carry no user-visible change. The job writes *"No user-visible changes in this
   release."* and continues; it **must not** hard-fail at the last step, by which point the packages are
   published and the version is burned.
3. ⚠️ **The contention hazard is named but not yet mitigated** (owner decision open). A single hand-edited
   root file is the highest-contention shape available here, in a repository whose CLAUDE.md records that
   *"a `Charts.cs` edit has silently vanished this way before."* Rejecting generated notes is right; the
   alternative's own failure mode — a concurrent story's entry silently lost, invisible until missing from a
   published release body — was not addressed. A per-story fragment directory assembled at release time
   would remove it.

### 6.6 What "preview" promises, and what it does not

⚠️ **This section is now ALSO recorded in ADR 0040 § Decision 11.** It previously lived only here, while
Story 17.4's release-readiness sign-off was pointed at it as its checklist — a governing obligation cannot
sit in a story artifact (CLAUDE.md § Decision records). Treat the ADR as authoritative.

**Promises:**
- It generates a portal from a supported SDD repository, and the published channels install and run.
- Output is read-only with respect to your repository (AD-6).
- Breaking changes are **recorded in `CHANGELOG.md`** (prefixed `**BREAKING:**`, § 6.5) and carry a
  minor-version bump.

⚠️ **"A supported SDD repository" is undefined, and the gap has a known symptom** (code review 2026-08-07).
§ 4.1 found `EpicsIndexSurface.vue` **hard-throws** when the epics index has no child pages, so a thin or
non-BMad external adopter — which § 4.1 itself calls the highest-weight first-run case for this epic — gets
`errors=1` and a missing page. That is routed to Story 23.3 and **gates Story 16.7**, but the gate existed
only in this report; ADR 0040 § Decision 11 now carries it. Story 16.7 also has no definition of "supported"
to test a clean-environment install against.

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
- **That the `dotnet tool` channel works without .NET 10.** ⚠️ **Added by the code review of 2026-08-07 —
  this was missing entirely.** The promises named Node and not .NET, while `README.md:92-93` lists the .NET
  10 SDK as a prerequisite and § 3.1 puts the `dotnet tool` channel **first** in the cut. It is therefore the
  likeliest install blocker for the leading channel, and Story 16.6 would have implemented ADR 0040
  § Decision 8 as written and shipped a NuGet listing that omits it. Does not apply to the self-contained
  binaries or to npx, which carry their own runtime.
- **Availability on every platform.** ⚠️ **Also added 2026-08-07.** The promise *"the published channels
  install and run"* was unqualified by platform while § 3.2 defers `linux-arm64` and `osx-x64`. Users on
  those platforms are **not** unsupported — the `dotnet tool` channel is platform-neutral and remains
  available — but nothing said so, and nothing specified what Story 16.8's `optionalDependencies` wrapper
  does when no platform package matches (npm's default is an opaque missing-binary error at run time). ADR
  0040 § Decision 2 now requires an explicit message naming the `dotnet tool` fallback.
- **Signed binaries.** § 5.5. The compensating control is a published **SHA-256 digest** per release asset
  (ADR 0040 § Decision 2), added by the code review because the unsigned channel was the only one with
  neither a signature nor a digest.

### 6.7 Node prerequisite check — placement (R5's open half)

ADR 0022 §Decision 5 words it as *"The binary detects Node **at startup**"*. The shipped check runs at
**prerender time**, inside `NuxtPrerender` (`NuxtPrerender.cs:141-216`).

**Decision: the shipped placement stands; ADR 0022's wording is amended to match it, not the reverse.**
"At startup" would move a subprocess spawn into every invocation, including `--help` and `--version`, to
warn about a dependency that only the prerender path needs. The user-visible difference is that the message
arrives after ingest rather than immediately — on the order of a second on this repository, and ingest is
not destructive. The cost of the alternative is paid on every run; the cost of the status quo is paid once,
by a user who is about to hit the error anyway.

⚠️ **The shipped code describes itself as temporary, and this decision must overrule that explicitly** (code
review 2026-08-07). The check's own doc comment reads: *"ADR 0022 §Decision 5 assigned Node DETECTION to
Story 16.3, which has not been built … **Until it is, this is the check**"* (`NuxtPrerender.cs:143-145`).
This section promotes a self-declared interim stand-in to the permanent answer — which is defensible on the
rationale above, but was never stated, so the record and the code disagree in writing. **Story 16.3 must
update that doc comment to cite ADR 0040 § Decision 8 instead of describing itself as provisional.**
Otherwise the next reader finds a comment telling them the opposite of the governing decision, and § 9's
instruction to 16.3 (*"NOT Node detection — it shipped in 23.6"*) reads as contradicting the source.

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
6. **Ratify ADR 0040** (AC #4). It is authored and complete at `Proposed`; ratification is yours. **This is
   now the only thing standing between Story 16.1 and done** — the eight technical decisions the code review
   left open were resolved on 2026-08-07 and are in the record (§ 10, items 11–14 and 17–19). Urgency is not
   ceremonial: Stories **16.2 and 16.3 have both already shipped against this ADR**, 16.3 implementing
   § Decision 1's pack item and § Decision 5's MinVer derivation directly.
7. **Create the bootstrap tag `v0.1.0-preview.1`** before the first release publishes (§ 10 item 12). Not a
   blocker for anything shipped so far: Story 16.3's MinVer properties make an untagged build emit
   `0.1.0-preview.0.<height>`, which is inside the scheme and keeps the About page's Preview badge. Do it at
   **16.4** time, on a commit that is green on `main`.
8. 🔴 **URGENT — regenerate the `ir-content` manifest; `main`'s CI is red on a required check** (§ 4.3).
   `cd web && npm run extract:ir-content`, then commit. One field, 12 bytes; every CSS sheet is
   byte-identical, so nothing shipped changes. **This is listed above the signing item deliberately: while
   `main` is red, § Decision 9's release preflight can never pass and no release can be cut at all.**
9. No signing certificate is needed for the preview (§ 5.5) — listed so its absence is a decision on the
   record rather than an omission.

---

## 9. Epic sequencing — what this spike unblocks or changes (Task 8)

⚠️ **CORRECTED 2026-08-07 — this section originally certified "no structural scope change", and that was
wrong on one point.** No story is added, removed or renumbered, which is what the original certification was
reasoning about. But this spike **created a new cross-epic blocking edge** — Story 23.3 now gates Story 16.7
(§ 4.1, ADR 0040 § Decision 11) — and *an edge is structure*. Per CLAUDE.md § Decision records that belongs
in `epics.md` **and** `sprint-status.yaml` in the same change, not as prose in a spike report. It is now in
both, plus a reciprocal seat on § Story 23.3 so the dependency is visible from either end.

Everything else in this section remains AC refinement *within* existing stories, and the absence of a wider
`epics.md` diff is still a decision rather than an oversight.

| story | what changes |
|---|---|
| **16.2** | Required-check string is the **job name verbatim: `build-test-analyze`**. `portability-probe (ubuntu, non-gating)` must **NOT** be required. Do **not** create a second build+test workflow. **NFR9's gate-on-a-tag question is answered: require the tagged commit to already be green on `main`** rather than re-running build+test in the release job — the tag points at a commit `main` already validated, and re-running invites a different result from the same source. **Plus a new blocker: `npm ci` fails locally at `838d591` (§ 6.1); 16.2 must verify CI's status and fix the lockfile.** |
| **16.3** | Packaging shape is decided and proven (§ 2) — implement the `renderer/**` pack item and the sibling copy for the binary. Version-from-tag via **MinVer** (§ 6.2). **NOT Node detection** — it shipped in 23.6 (R5); only the *placement* question was open and § 6.7 closes it. Also inherits: the swallowed HTTP-500 renderer diagnostic (§ 4.1) and the worktree `FindRepoRoot` defect (§ 4.2). |
| **16.4** | Add the `npm run build:package` stage (ADR 0022 §Consequences and 23.5 open item 4 both assign it here). Set `SOURCE_DATE_EPOCH`. Publish via Trusted Publishing with `permissions: id-token: write`. Assemble `changelog.d/` fragments into `CHANGELOG.md` and copy the released section into the Release body. **Plus four decisions taken 2026-08-07:** the **gate preflight** on check-runs for the tagged SHA (ADR 0040 § Decision 9); the **registry preflight** + forward-only re-cut + **draft-Release bracketing** (§ Decision 10) — under which **AC #2 is achievable**, read as *"safe to re-run on a new tag"*; the **bootstrap tag `v0.1.0-preview.1`** as an owner action at release time; and `fetch-depth: 0` on the release checkout, since MinVer needs tag reachability. |
| **16.5** | Organization-owned publisher + Entra workload identity federation; **the PAT path is closed** (§ 5.3). Prerequisite: Story 6.8's Workspace-Trust posture. Confirm whether `"private": true` blocks `vsce package` — **not confirmed by this spike**, it is 16.5's to check on a manifest it owns. **Plus the versioning rule decided 2026-08-07** (ADR 0040 § Decision 5): the extension's **MINOR mirrors the CLI's MINOR** and its **PATCH is its own monotonic counter** — a frozen `0.1.0` would have permitted exactly one Marketplace publish ever. |
| **16.6** | Owns `CHANGELOG.md` in the § 6.5 format **and the `changelog.d/` fragment format + assembler** (ADR 0040 § Decision 6, decided 2026-08-07). Owns surfacing the **Node *and* .NET 10 prerequisites where a packaged consumer sees them** — NuGet listing, npm README, Marketplace listing (R5's second open half). ~~Owns the `README.md:260` version literal~~ — **already closed by Story 16.3**, which made the recipe read the version off the produced `.nupkg`. |
| **16.7** | The preview cut is § 3.1, gated by Story 17.4. **BLOCKED ON STORY 23.3** — § 4.1's thin-repository `errors=1` must be fixed before readiness can pass. Now seated in `epics.md` § Story 16.7 and `sprint-status.yaml`, not only here. |
| **16.8** | RID matrix = `win-x64` / `linux-x64` / `osx-arm64` (§ 3.2). Renderer is **one shared package**, not per-RID (§ 2.5), exact-pinned (§ 6.3). Node check = an `engines` field, **not** a postinstall script (§ 5.6). **Plus the ID escalation rule decided 2026-08-07** (ADR 0040 § Decision 12): if `specscribe` is taken on npm, **do not substitute `specscribe-cli`** — `npx` resolves the *package* name, so no rename preserves the documented command. Escalate; the owner chooses between amending all three documents together or dropping npx from the cut. **Publish order is normative:** `specscribe-renderer` first, wrapper second. |
| **16.9** | Its stated dependency — the renderer being *in* the published package — is now proven satisfiable (§ 2). The Action collapses to install-and-run once 16.3 ships. Inherits the exact-pin rule (§ 6.3). |
| **23.3** | § 4.1's `EpicsIndexSurface.vue` throw, same class as the `DashboardSurface.vue` defect it already owns. **This story now GATES 16.7** — seated in `epics.md` § Story 23.3 + § Story 16.7 and in `sprint-status.yaml` (2026-08-07). It keeps the work despite standing at `review`, because `review` is an iterating state in this project's lifecycle and the correct behaviour is already modelled one component over, in the same run. |
| **17.4** | Inherits: deferred `<Deterministic>`/SourceLink (§ 6.1), and the preview promises as the sign-off checklist — **now read from ADR 0040 § Decision 11**, not from § 6.6. *(Corrected 2026-08-07: pointing a release-gating story at a story artifact rather than the decision record is the pattern CLAUDE.md § Decision records names. § 6.6 remains as the evidence behind the decision.)* |

---

## 10. Open items

| # | item | state | owner |
|---|---|---|---|
| 1 | `EpicsIndexSurface.vue` hard-throws on a project with no epics | reproduced twice. ✅ **The gate now has an owner and is landed structurally** — `epics.md` § Story 16.7 **and** § Story 23.3 **and** `sprint-status.yaml`, per CLAUDE.md. 23.3 keeps it: `review` is an iterating state here, and it already fixed the identical class on `DashboardSurface.vue` in the same run | **Story 23.3**, gating **16.7** — *resolved 2026-08-07* |
| 2 | `FindRepoRoot` does not detect git worktrees (`.git` as a file) | ✅ **FIXED** since, by Story 16.3 | ~~Story 16.3~~ closed |
| 3 | `npm ci` fails at `838d591` locally | ✅ **FIXED** by `0b1f561`. *The original "CI status unverified (`gh` not installed)" was a **false** limit — `gh` is installed at `C:\Program Files\GitHub CLI\gh.exe`, just not on `PATH` (§ 6.1)* | ~~Story 16.2~~ closed |
| 4 | SpecScribe discards the renderer's error text behind "HTTP 500" | observed. **Raised in priority** — it is what a consumer sees when an incomplete renderer payload ships (§ 2.7 (1)) | **Story 16.3** |
| 5 | npx end-to-end install proof (§ 2.5 decided the shape from a measured property; no wrapper was published) | **unmeasured** | **Story 16.8** |
| 6 | `linux-arm64` / `osx-x64` RIDs | deferred by decision | 16.8, on demand |
| 7 | `extension/package.json`'s `"private": true` vs. `vsce package` | **unconfirmed** — deliberately not tested; R9 forbids editing the manifest here | **Story 16.5** |
| 8 | Trusted Publishing visibility on the owner's nuget.org account | **unknown** — cannot be checked without the account. Fallback storage now specified (§ 5.1) so 16.4 is not blocked either way | **owner** (§ 8 item 2) |
| 9 | CLAUDE.md states worktrees are unavailable on this machine; five are in active use | stale documentation | owner / next retro |
| 10 | `<Deterministic>` / `ContinuousIntegrationBuild` / SourceLink | deferred past preview | 17.4 burndown |
| **11** | **Release atomicity** — no re-publish, rollback, yank or version-burn policy existed; Story 16.4 AC #2 was unachievable as written | ✅ **DECIDED 2026-08-07** (ADR 0040 § Decision 10). A version is consumed on first publish and never reused; recovery is forward (bump `-preview.N`, re-tag); per-channel resume rejected; a **registry preflight** fails fast on a consumed version; the GitHub Release is a **draft** bracketing the irreversible registry publishes. Withdrawal = unlist + `npm deprecate` + delete the Release. **16.4 AC #2 is achievable** under the reading *"safe to re-run **on a new tag**"* | **16.4** implements |
| **12** | **MinVer bootstrap** — 0 git tags, `MinVerTagPrefix` unspecified; the failure was silent (`0.0.0-alpha.0.N` at exit 0) | ✅ **CLOSED — and mostly by implementation, not decision.** Story 16.3 has since landed `MinVerTagPrefix=v`, `MinVerMinimumMajorMinor=0.1`, `MinVerDefaultPreReleaseIdentifiers=preview.0`, so an untagged build emits `0.1.0-preview.0.<height>` and the alpha default is unreachable; `README.md`'s literal is gone too (it reads the version off the produced `.nupkg`). **Verified in the tree 2026-08-07** | first tag `v0.1.0-preview.1` → **owner**, at 16.4 release time — *not* a 16.3 precondition |
| **13** | **§ Decision 9's gate mechanism** — `build-test-analyze.yml` builds only `main`, so no release-branch or hotfix commit has a run to point at | ✅ **DECIDED 2026-08-07** (ADR 0040 § Decision 9). Preflight on check-runs for the tagged SHA: name `build-test-analyze`, `conclusion == success`, poll 30 s / 15 min while in progress, most-recent-completed-run authoritative, actionable failure when no run exists. The hotfix branch is answered **by scope, not mechanism** — the preview is **forward-fix only**, all tags cut from `main`, now an explicit non-goal | **16.4** implements |
| **14** | **Extension versioning** — the Marketplace rule as written permitted exactly one VSIX publish ever (§ 6.4) | ✅ **DECIDED 2026-08-07** (ADR 0040 § Decision 5). Extension **MINOR mirrors the CLI's MINOR**; extension **PATCH is its own monotonic counter**, so strictly-greater is always satisfiable and the correspondence stays legible both ways | **16.5** implements |
| **15** | **Packaging shape measured on Windows / `win-x64` only**, generalized to three RIDs and both packing hosts | **extrapolated, not measured** | **16.3 / 16.4** on Linux + macOS runners |
| **16** | **`NuxtPrerender` spawns Node via the single-string `ProcessStartInfo` overload** (`:251`); the artefact path is now consumer-chosen, so any space in it breaks the leading channel's first run | **unexercised** — the probe path had no spaces | **Story 16.3** (move to `ArgumentList`) |
| **17** | **`CHANGELOG.md` contention** — a single hand-edited root file becomes the highest-contention file in a repository whose CLAUDE.md records a silently-vanished edit | ✅ **DECIDED 2026-08-07** (ADR 0040 § Decision 6). Per-story fragments in **`changelog.d/<story-key>.md`**, assembled at release time and deleted in the release commit. Each story creates a **distinct new file**, so the vanishing-edit failure mode becomes a *missing file* — visible in `git status` | **16.6** format + assembler, **16.4** invokes |
| **18** | **Fallback package IDs silently changed the documented command** — `specscribe-cli` was offered as a drop-in while `npx specscribe` is printed in three documents | ✅ **DECIDED 2026-08-07** (ADR 0040 § Decision 12). An implementer **may not substitute** — escalate to the owner. The asymmetry is now explicit: losing the **NuGet** ID is cheap (`ToolCommandName` keeps the invocation `specscribe`); losing the **npm** ID is **not recoverable by rename**, because `npx` resolves the *package* name | **owner** decides if it happens; reservation stays action #1 |
| **19** | **Version-component semantics** — only "minor = breaking inside `0.x`" was defined, leaving every tag after the first to judgement, with no `0.x` exit criterion | ✅ **DECIDED 2026-08-07** (ADR 0040 § Decision 5). MINOR = breaking **or** new feature; PATCH = fixes/perf/docs/internal; `-preview.N` = a re-cut of the same target version. MINOR deliberately carries both meanings (SemVer's own `0.x` rule), which is **why the `**BREAKING:**` changelog prefix is the load-bearing signal**, not the digits. Plus a three-part `0.x` → `1.0.0` exit criterion | **17.4** tests the exit criterion |

| **20** | 🔴 **`main`'s CI is RED on the required `build-test-analyze` check** — `check:ir-content` fails because the committed `ir-content.manifest.json` claims `generatedBytes: 186492` while the committed `ir-content.css` beside it is **186,504 bytes** (§ 4.3) | **OPEN — blocking.** Not environmental (reproduces in CI, run `31234945903`), not this story's (no product-code change). Attributed to **`3b085e7`**. **Blocks ADR 0040 § Decision 9's release preflight**, so no release can be cut while it stands | **owner**, one command — § 8 action 8 |

*Items 11–16 added by the code review of 2026-08-07; 17–19 are that review's remaining decision items, given
numbers here so every one of its nine has a tracked home. **Items 11–14 and 17–19 were resolved on 2026-08-07**
by the dev-story pass that followed the review. Items 2 and 3 closed by work that landed since.*

**Nothing on this list is an open owner *decision* any more.** Two items remain, and both are **acts** rather
than decisions — neither is something an agent can perform on the owner's behalf, and neither needs further
deliberation:

1. **ADR 0040's ratification** (AC #4, § 8 action 6) — the story's only remaining acceptance gap.
2. 🔴 **Regenerating the `ir-content` manifest** (item 20, § 8 action 8) — one command, and **more urgent
   than the ratification in wall-clock terms**, because `main` is red on a required check *right now* and
   § Decision 9's release preflight cannot pass until it is green.

---

## 11. Deliverables

- **This report** — `_bmad-output/implementation-artifacts/16-1-spike-report.md`
- **ADR 0040** — `docs/adrs/0040-release-channels-and-versioning-policy.md` (**Proposed**; ratification is
  owner action § 8 item 6). **0039 was NOT free**, which the story file told us to verify rather than assume.
  0019 remains claimed-but-unwritten by Story 18.3.

  ⚠️ **Corrected 2026-08-07 — the attribution here was false.** This line originally read *"Story 4.9 claimed
  it on 2026-08-06."* It did not. **0039 is `0039-runtime-attached-body-level-classes.md`** — "A Second
  Bounded Unscoped Layer, for Runtime-Attached Body-Level Classes" — authored from **the owner's verify round
  on the sunburst surfaces**, Deciders: Matthew-Hope Eland, landed in `76e5e42`/`6a7bc71`. Story 4.9 had
  merely *reserved* 0039 in its own story file and ultimately landed as **ADR 0041**, whose header states:
  *"Took 0041 because 0039 **and** 0040 were both claimed after the story's baseline."* Story 4.9's own code
  review had already caught this and assigned the correction to this story. It was repeated in five places —
  here, the story file's Task 6 and Completion Note 4, the `docs/adrs/README.md` index entry, and the commit
  message of `9837e67`. All except the immutable commit message are now corrected. The conclusion — that 0040
  was the right number — is unaffected; only the reason given for it was wrong.
- **One index entry** in `docs/adrs/README.md` (the file's house style is a multi-line bullet, so "one line"
  meant one entry).
- **No `spike/release/**`** — the probe needed no committed throwaway code; it was six shell commands and a
  reverted csproj item, all reproduced in § 2.1. *(Corrected 2026-08-07: the word "verbatim" did not hold —
  § 2.1 carried `CopyToOutputDirectory="Never"` and the story file's Debug Log did not. The two records now
  agree, and ADR 0040 § Decision 1 carries the normative form.)*
