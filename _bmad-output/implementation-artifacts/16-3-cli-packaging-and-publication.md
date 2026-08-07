---
baseline_commit: 07bdb79 # local `main` == `origin/main` at authoring time (2026-08-07), working tree CLEAN.
                         # ⚠️ This is FOUR commits ahead of the tree Story 16.1 measured (838d591) and TWO
                         # ahead of Story 16.2's authoring point (35437b9). One of those commits (0b1f561)
                         # CLOSES 16.1's `npm ci` blocker — see R1. Verify any cited line number: shared main.
epic: 16
frs: [FR32] # release engineering; this story is the PACKAGING half (16.2 is the CI half, 16.4 the pipeline)
nfrs: [NFR9] # "Release builds are reproducible and produced by CI from a clean checkout; publishing to any
             # distribution channel is gated on a passing build + test run." (epics.md:138)
depends_on: [16-1] # ADR 0040 decides EVERY shape this story implements: the pack path, MinVer, the RID matrix.
                   # ⚠️ ADR 0040 is still `Proposed`. See R10 — this does NOT block, and why.
blocks: [16-4, 16-8, 16-9] # 16.4 packages "per Story 16.3"; 16.8 wraps the binaries this story produces;
                           # 16.9's composite Action collapses to install-and-run only once this ships.
informs: [16-6, 16-7]
amends: null # NOTHING structural. epics.md and sprint-status.yaml need no scope edit — see § Scope guard.
ships_product_code: true # ⚠️ UNLIKE 16.1. Edits src/SpecScribe/** (csproj, NuxtPrerender.cs, Program.cs),
                         # tests/**, and the two README statements this story's own change falsifies.
                         # Does NOT edit .github/** (see R6), web/** source, or extension/**.
decides: null # No new ADR. ADR 0040 already decides all of it; the MinVer property choices in R4 sit INSIDE
              # its §Decision 5 rather than beside it. If you find yourself deviating, see R10.
deliverables:
  - "src/SpecScribe/SpecScribe.csproj (MinVer + the renderer pack item + the pack-time payload assertion)"
  - "src/SpecScribe/Program.cs (SetApplicationVersion — `--version` is broken TODAY, see R3)"
  - "src/SpecScribe/NuxtPrerender.cs (two inherited defects: worktree repo root, swallowed renderer error)"
  - "tests/SpecScribe.Tests/NuxtPrerenderTests.cs (both defect fixes, Node-free and artefact-free)"
  - "README.md (ONLY the two statements MinVer and the pack item make false — the rest is 16.6's)"
  - "docs/Packaging.md (how to produce and verify a package, and the four traps)"
---

# Story 16.3: CLI Packaging and Publication

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a prospective user,
I want SpecScribe published to its chosen distribution channel,
So that I can install and run it with a documented one-line command.

| | |
|---|---|
| **Epic** | 16 — Release Engineering & Community Preview Launch |
| **Authority for every shape here** | [ADR 0040](../../docs/adrs/0040-release-channels-and-versioning-policy.md) (`Proposed`) |
| **Evidence it is achievable** | [16-1-spike-report.md](16-1-spike-report.md) § 2 — measured, `errors=0`, on two channels |
| **Regression floor** | `dotnet test` + `cd web && npm run check` (4 gates). `GoldenContentFingerprint` is RETIRED (ADR 0034) |

---

## ⛔ Read first — eleven reconciliations against the live repository

Story 16.1 answered *whether* this is possible. It is: measured, twice, `errors=0`. **What follows is what has
moved since it measured, what is already done, what is broken right now, and the four ways this specific
change produces a green build that ships a package that cannot render.**

### R1 — 16.1's `npm ci` blocker is CLOSED. Your precondition is an env var, not a workaround.

16.1 § 6.1 and ADR 0040 § 7 both record `npm ci` failing with `Missing: @emnapi/runtime@1.11.3 from lock file`,
and 16.1 § 2.1 worked around it with `npm install --no-save --no-package-lock`. **Do not copy that workaround.**

Commit **`0b1f561`** ("CI fix: repair the lockfile and regenerate the two stale drift gates") landed the repair.
Verified at `07bdb79`: `@emnapi/runtime@1.11.3` is now a top-level entry (`web/package-lock.json:596-598`), and
`npm ci` **succeeds** on this machine with npm 11.16.0 / Node 24.18.1 — the exact toolchain that failed for 16.1.

**But `npm ci` still fails without one env var, and it is not the lockfile.** `postinstall: nuxt prepare` loads
`web/nuxt.config.ts`, which calls `loadManifest()` and hard-fails when no IR exists:

```
ERROR  IR not found at C:\Dev\SpecScribe\SpecScribeOutput\spa\manifest.json.
```

Measured this session, twice: **without** the flag → exit 1; **with** it → `added 37 packages … in 12s`.
`SPECSCRIBE_PACKAGE_BUILD=1` stubs the manifest empty, which is exactly what it is for. This is not a defect —
`build-test-analyze.yml:220-246` documents the cycle at length and sets the same flag. **The invocation is:**

```sh
cd web && SPECSCRIBE_PACKAGE_BUILD=1 npm ci   # ← the flag is NOT optional on a fresh checkout
npm run sync:assets
npm run build:package                          # NEVER `npm run build` (ADR 0022 §Decision 2)
```

⚠️ **`web/.output` does not exist at `07bdb79`** (verified: no `web/.output/server/index.mjs`). It is gitignored.
You must build it before any pack can contain it — which is precisely the failure R7 makes fail loudly.

⚠️ One machine-local wrinkle: npm 11.16.0 warns `1 package has install scripts not yet covered by allowScripts:
esbuild@0.28.1`. It warned rather than failed here, and CI pins npm 11.6.2 which has no such gate. If
`build:package` misbehaves on this machine, that is the first thing to check — not a packaging bug.

### R2 — 🚨 `specscribe --version` DOES NOT WORK TODAY. AC #2 names it explicitly.

This is the story's most easily-missed obligation, because it reads like an assertion about existing behavior.
Measured this session against `src/SpecScribe/bin/Debug/net10.0/specscribe.exe`:

| invocation | exit code | output |
|---|---|---|
| `--help` | **0** | full usage block — fine, leave it alone |
| `--version` | **1** | `Fatal error: Unknown option 'version'.` |
| `-v` | **1** | `Fatal error: Unknown option 'v'.` |

Cause: `Program.cs:18-19` calls `SetApplicationName` and `UseStrictParsing` but **never**
`SetApplicationVersion`. Spectre's own documentation states the `-v`/`--version` global option *"requires
`ApplicationVersion` to be configured in the application."* With strict parsing on, an unconfigured `--version`
is an unknown option, and `Program.cs:56-66` then drops an interactive user into the menu — so on a terminal
this misfires as a *menu*, not as an error. Worse cosmetics, same defect.

**`-v` is free.** Verified by enumerating every short option in `src/SpecScribe/`: `-a`, `-o`, `-p`, `-s` (plus
Spectre's own `-h`). Nothing collides.

⚠️ **Verify, do not assume, that `SetApplicationVersion` is sufficient here.** Spectre's docs are silent on
whether the global version flag is intercepted when the app is a `CommandApp<TDefaultCommand>` — which this one
is (`Program.cs:15`, `CommandApp<InteractiveCommand>`) — and default-command argument binding is exactly where
such a flag can get parsed against the default command's settings instead. It is a two-minute check: add the
call, build, run `--version` and `-v`. If either still fails, the fallback is an explicit flag on the default
command's settings with an early return, and **say in the completion notes which of the two you shipped.**

### R3 — The pack item has ONE correct form, and the wrong one produces a green pack with no entry point.

16.1 § 2.7(1) burned a diagnosis cycle on this and it is the single highest-value paragraph in the spike:

```xml
<!-- ✅ CORRECT — NuGet already appends the recursive directory to a PackagePath naming a folder -->
<None Include="..\..\web\.output\**\*" Pack="true"
      PackagePath="tools\$(TargetFramework)\any\renderer" CopyToOutputDirectory="Never" />
```

```xml
<!-- ❌ WRONG — %(RecursiveDir) applies the structure TWICE -->
PackagePath="tools\net10.0\any\renderer\%(RecursiveDir)"
```

The wrong form produced `tools/…/renderer/server/node_modules/hookable/dist/server/node_modules/hookable/dist/index.mjs`
— **187 entries, the right count, the right total byte size, exit 0, and no `renderer/server/index.mjs`.** A
size-and-count check calls that a pass. This is why AC #4 asserts the *packaged path*, not the payload size.

Two deliberate deltas from the spike's throwaway probe item:

- **`$(TargetFramework)`, not the literal `net10.0`.** `PackAsTool` places the tool at `tools/<tfm>/any/`; a
  hard-coded TFM silently detaches the payload from the assembly the day the TFM moves, and the symptom is a
  packaged tool that cannot find its renderer. Verify the substitution actually landed by listing the nupkg.
- **`CopyToOutputDirectory="Never"` stays.** A local `dotnet build` must NOT get a `renderer/` beside the
  assembly: copying 187 files on every build is slow, and candidate 3 (`web/.output` in the repo) already
  serves the developer path. Keep the local dev experience exactly as it is.

### R4 — MinVer: the property set matters, and the default one regresses a user-visible badge.

ADR 0040 § 5 chose **MinVer** and requires `<Version>` **deleted**, not replaced. Latest stable is **7.0.0**
(published 2026-01-05, requires SDK ≥ 8.0 — we are on `net10.0`), referenced `PrivateAssets="All"`.

**This repository has ZERO git tags.** Verified: `git tag -l` is empty, `git describe --tags` → *"No names
found."* MinVer's documented behavior with no version tag is to use **`0.0.0-alpha.0`** plus height. Take that
default and every untagged build reports `0.0.0-alpha.0.<height>` — which contradicts ADR 0040 § 5's `0.x`
scheme on its face and reads, on the About page, as a downgrade from today's `0.1.0-preview`.

**Recommended property set** (all three inside ADR 0040 § 5's scheme, none of them a new decision):

```xml
<MinVerTagPrefix>v</MinVerTagPrefix>                                    <!-- tags are v0.1.0-preview.1 -->
<MinVerMinimumMajorMinor>0.1</MinVerMinimumMajorMinor>                  <!-- floor, not 0.0 -->
<MinVerDefaultPreReleaseIdentifiers>preview.0</MinVerDefaultPreReleaseIdentifiers>
```

→ untagged builds become **`0.1.0-preview.0.<height>`**: still `0.x`, still carrying a pre-release label, same
`0.MINOR.PATCH-preview.N` family the ADR names. A tagged commit uses its tag verbatim with zero height.

**Why the pre-release label is load-bearing and not cosmetic.** `AboutTemplater.cs:133-135` renders the About
page's `Preview` badge from `meta.IsPrerelease`, and `IsPrerelease` requires a non-empty trailing label
(`AboutTemplaterTests.cs:66-75`). A version without one silently removes a shipped, user-visible badge. ADR 0040
§ 5 states the rule: *"the first release without the label is by definition no longer a preview."*

🚨 **VERIFY THE `+<sha>` SUFFIX SURVIVES — this regression is SILENT.** MinVer sets
`InformationalVersion = {MinVerVersion}`, and its documentation says build metadata *"appears only in the
assembly informational version"*. Today the suffix comes from the in-box SDK SourceLink path
(`SourceRevisionId`), which is why `obj/Debug/net10.0/SpecScribe.AssemblyInfo.cs` currently reads
`0.1.0-preview+35437b957fbdb…`. The About page's Build row parses that suffix
(`AboutTemplater.ParseInformationalVersion`, `:90`) and `IsShaLike` **drops an implausible suffix rather than
showing a bogus hash** — so if MinVer's assignment lands after the SDK's append, the hash does not error, it
just *vanishes from the page*. **Read the generated `SpecScribe.AssemblyInfo.cs` after the change and confirm
the `+<40-hex>` is still there.** No test asserts it: `AboutTemplaterTests.cs:13-23` only asserts the version
contains no `+` after trimming, so the whole suite stays green with the hash gone.

Two consequences to plan for, not discover:

- **The version now changes on every commit** (height). Nothing should break — `normalizeVolatile` folds
  `SpecScribe v[^<]+` → `SpecScribe v<VERSION>` (`web/scripts/harness-lib.mjs:60`), which is why the parity gate
  tolerates it. Confirm that empirically anyway; see R8.
- **A hard-coded `--version 0.1.0-preview` in any install recipe becomes a lie.** Two live sites, R9.

### R5 — Two inherited defects, both routed here by name, both with a designed fix

16.1 § 10 routes items **2** and **4** to this story. Neither is optional; both are in AC #5.

**(a) `FindRepoRoot` does not recognise a git worktree** — `NuxtPrerender.cs:129-137`:

```csharp
while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
```

In a worktree `.git` is a **file** (measured: 56 bytes), so `Directory.Exists` is false, the walk continues past
the worktree root, and candidate 3 resolves to **another checkout's artefact** — observed by 16.1 resolving
`C:\Dev\SpecScribe\web\.output` from inside `.claude/worktrees/story-16-1-dev`. Developer-path only (candidate 2
wins first on the packaged path), and *"silently rendering from another checkout's artefact"* is exactly the
wrong-answer-with-a-success-status class this codebase engineers against.

Fix: treat `.git` as a repo root when it is **either** a directory **or** a file. `FindRepoRoot` is `private`;
make it `internal` (`InternalsVisibleTo SpecScribe.Tests` already exists, `SpecScribe.csproj:52`) so the test can
build a temp dir containing a `.git` **file** and assert the walk stops there.

⚠️ **You are working in a worktree right now, so this defect is live under you.** Project memory records the
same thing operationally: *"specscribe generate picks the wrong renderer path in worktrees — set
`SPECSCRIBE_RENDERER_DIR` or the prerender silently skips."* After your fix, that workaround should stop being
necessary from a worktree — which is itself a clean verification.

**(b) SpecScribe discards the renderer's actual error text behind "HTTP 500"** — `NuxtPrerender.cs:286-291`:

```csharp
body = res.Content.ReadAsStringAsync()…;          // ← read
if (res.StatusCode != HttpStatusCode.OK)
    failure = $"the renderer answered HTTP {(int)res.StatusCode} for a route the manifest names.";
```

`body` is read and then thrown away. 16.1 could only obtain the real message — *"The epics index IR entry
declares no child pages…"* — by booting the artefact by hand, and § 4.1 makes the point that matters for
Epic 16: **a packaged consumer with no `web/` checkout has no such diagnostic path at all.** Shipping a package
whose only failure signal is a bare status code is a support burden this story creates if it ignores this.

Design the fix, don't just concatenate — the naive version floods the console:

- Nitro's error body is JSON (`{"statusCode":…,"message":…,"stack":…}`). Extract `message`; fall back to the raw
  body, truncated, when it will not parse as JSON (it may be an HTML error page).
- **Bound it.** One route's message, newlines collapsed, capped (~500 chars). 373 routes × a stack trace is not
  a diagnostic, it is a denial of service against the console.
- Append the **server-log tail once**, for the run's first failure only. `Tail(serverLog)` already exists
  (`:374-377`) and already caps at 40 lines; `[render error]` lines arrive on that channel.
- Put the extraction in an `internal static` helper — e.g. `DescribeRouteFailure(HttpStatusCode, string body)` —
  so it is **unit-testable without Node and without an artefact.** `NuxtPrerenderTests.cs:14-16` states that
  constraint as a standing rule inherited from Story 23.6's Dev Notes: honor it.

### R6 — `fetch-depth: 0` is ALREADY SET. Do not edit a workflow. That file belongs to 16.2 this sprint.

ADR 0040 § Consequences says *"Story 16.3 must set `fetch-depth: 0`"* (MinVer resolves the version from tag
reachability, and a shallow clone yields a **wrong version rather than an error**). **It is already satisfied.**
Verified at `07bdb79`: `.github/workflows/build-test-analyze.yml:66-70` and `:361-363` both carry
`fetch-depth: 0`, added for SonarCloud's blame data. `actions/checkout` with `fetch-depth: 0` fetches tags.

So this story's obligation collapses to **verify and record** — no `.github/**` edit. That matters beyond
tidiness: **Story 16.2 is `ready-for-dev` and owns `.github/**` right now** (its deliverables name
`.github/rulesets/main-required-checks.json` and it will be editing the same workflow). Per CLAUDE.md
§ Scoping a code review, two stories writing one file forces hunk-level attribution and creates a review gap
where a symbol belongs to neither. Staying out of that file avoids the whole class. If you conclude you
genuinely must touch it, say so in the File List with the reason, and name 16.2 as the co-owner.

### R7 — The failure this story must make IMPOSSIBLE: a package that installs and then cannot render

A glob that matches nothing is **not** an MSBuild error. `web/.output` is gitignored and absent on any fresh
checkout (R1). So the default outcome of `dotnet pack` on a clean tree is a **2.5 MB nupkg that installs
cleanly, puts `specscribe` on PATH, and fails at generate time** with "the renderer artefact could not be
found." That is the single worst thing this story could ship, and it ships silently.

**AC #4 therefore asserts the PACKAGED PATH, after the pack, and fails the build.** Assert on
`tools/<tfm>/any/renderer/server/index.mjs` existing *inside the produced nupkg* — not on the source directory,
and not on entry count or byte size, both of which R3's wrong form satisfied while shipping no entry point.

MSBuild has a built-in `Unzip` task, so this needs no new dependency:

```xml
<Target Name="AssertRendererPacked" AfterTargets="Pack" Condition="'$(PackAsTool)' == 'true'">
  <!-- Unzip the produced nupkg to a temp dir, then Error when the entry point is absent. -->
</Target>
```

Gate it so **`dotnet build` and `dotnet test` are unaffected** — only `pack` (and the publish path) may fail.
Breaking the inner loop to protect the release loop is not the trade this story wants.

### R8 — Rebuild non-incrementally, and if a gate moves, do NOT regenerate its baseline

CLAUDE.md, twice over, and both apply directly here:

- **`specscribe.css`/`.js` are embedded resources.** An incremental build reuses the cached assembly and never
  re-embeds a changed asset. You are editing the csproj's `ItemGroup` that holds those `EmbeddedResource`
  entries — build `--no-incremental` before you trust anything you measure.
- **Never regenerate a drift gate's baseline reflexively.** Establish causality first. 16.1 watched
  `check:ir-content` go red twice and regenerated nothing; following the gate's own suggested fix would have
  **deleted 185 deep-analytics rules** and turned it green over a real regression. The `+4/-185` signature is
  documented in advance at `build-test-analyze.yml:281-290` as what a generate without `--deep-git` looks like.
- **`check:parity` cannot see a C#-side change** (CLAUDE.md, verified 2026-08-01). Its corpus IR is frozen. A
  green parity run means "the renderer still behaves the same on a fixed fixture" — it says *nothing* about your
  csproj, `Program.cs` or `NuxtPrerender.cs` changes. Cover those with unit tests and a real run.

Expected: none of the four web gates should move at all. This story touches no stylesheet and no Vue component.
If one moves, that is a finding to investigate, not a baseline to re-pin.

### R9 — Two README statements this story's own change makes FALSE. Fix those two; leave the rest to 16.6.

16.1 excluded README deliberately and § 6.4 assigns `README.md:260`'s version literal to **16.6**. That
allocation is right for *release-facing documentation*. It is wrong for statements this story **falsifies**,
and leaving those is how a README starts lying:

| location | what it says today | why this story falsifies it |
|---|---|---|
| `README.md:129-131` | *"bump the `<Version>` in `src/SpecScribe/SpecScribe.csproj`, re-pack…"* | `<Version>` is **deleted** (R4). The instruction names a property that no longer exists. |
| `README.md:132-141` | *"The packaged tool does not yet carry its renderer (populating `renderer/` … is Story 16.3, backlog)"* + an `export SPECSCRIBE_RENDERER_DIR` recipe | This story **is** that work. ADR 0040 § Consequences: *"`README.md`'s external CI recipe loses its `SPECSCRIBE_RENDERER_DIR` step."* |
| `README.md:250-275` | the published-CI recipe: `--version 0.1.0-preview` and a `SPECSCRIBE_RENDERER_DIR:` env block, both commented *"not populated until Story 16.3 ships"* | Same. The literal version is additionally unpredictable under MinVer. |

**Scope line, stated so code review can check it:** fix what became false. Do **not** write release-facing
listing copy, do **not** create `CHANGELOG.md`, and do **not** surface the Node prerequisite on package
listings — all three are 16.6's by ADR 0040 and 16.1 § 9. Record the split in the completion notes.

⚠️ `README.md` is the packaged README (`PackageReadmeFile`, `SpecScribe.csproj:23,56`), so on this channel the
README **is** the NuGet listing. That is why AC #2's last clause is partly this story's and not entirely 16.6's.

### R10 — ADR 0040 is `Proposed`, not `Accepted`. Proceed anyway — here is the reasoning, on the record.

Story 16.1 AC #4 required a *ratified* ADR and could not close it; ratification is owner action § 8 item 6, and
it is still outstanding. Every shape this story implements comes from that ADR, so a rejected decision at
ratification would change this work.

**Recommendation: proceed.** The load-bearing decision (§ Decision 1, the packaging shape) was answered
**empirically** on two channels at `errors=0`, and options B–E were rejected on measured grounds, not taste —
B is impossible (an ESM module graph cannot be an `EmbeddedResource`), C adds a network dependency to a tool
that makes zero outbound calls. The residual exposure is § Decision 5's *mechanism* (MinVer vs an alternative),
which is one `PackageReference` and three properties to swap. That is a cheap thing to be wrong about, and
waiting blocks 16.4, 16.8 and 16.9 behind an owner action of unknown latency.

**If you find yourself deviating from ADR 0040 on anything, that is an ADR amendment, not a story note.**
CLAUDE.md § Decision records: propose it, do not bury it in this file or in `sprint-status.yaml` prose.

### R11 — The title says "and Publication". **Nothing is published here.** Do not push to any registry.

This story is named *CLI Packaging **and Publication***, and its AC #2 quotes `dotnet tool install -g SpecScribe`
— which reads like an instruction to put a package on nuget.org. It is not, in either direction:

- **AC #2 is proven against a LOCAL feed:** `dotnet pack -o artifacts` then
  `dotnet tool install SpecScribe --tool-path … --add-source ./artifacts`. That is exactly how 16.1 proved the
  channel (§ 2.1) and it exercises the real tool-store layout, the real PATH shim and the real
  `AppContext.BaseDirectory` — everything AC #2 asserts.
- **Actually pushing to a registry is Story 16.4's**, whose AC #1 publishes on a tag and attaches artifacts to a
  GitHub Release. **Story 16.7** then re-verifies the install *"end-to-end from the published artifact on a
  clean environment"* (`epics.md` § Story 16.7 AC #1). Two stories downstream already own the published path.
- **It is also not currently possible.** ADR 0040 § 3 puts both shipping channels on Trusted Publishing, and
  16.1 owner action § 8 item 2 says its visibility on the owner's nuget.org account is **unknown** and must be
  confirmed *"before 16.4 starts, not during."* The package IDs are also still unreserved (§ 8 item 1).

**And the binaries are consumed downstream, so produce them properly.** `epics.md` § Story 16.8 AC #1 opens
*"Given the self-contained native binary produced by Story 16.3"* — 16.8's npm wrapper spawns exactly what
Task 3 publishes. A binary without its sibling `renderer/` is a channel that cannot render.

⚠️ **Two mechanisms, not one.** The pack item carries `CopyToOutputDirectory="Never"` (R3), so it contributes
**nothing** to `dotnet publish`. The nupkg payload and the binary's sibling `renderer/` are separate wirings —
a `Target` on the publish path, or `<Content>` with `CopyToPublishDirectory`. Conflating them yields a nupkg
that works and a binary that does not, and only the nupkg is what you would think to test.

### 🔎 Analysis observations — UNKNOWN, not clean

`.specscribe/analysis/` **does not exist** at `07bdb79` (verified: no `index.json`, no shard for
`NuxtPrerender.cs`, `Program.cs` or `SpecScribe.csproj`). Per CLAUDE.md, **absent means UNKNOWN, never clean** —
the emitter deliberately writes nothing rather than an empty digest, because an empty digest reads as "this code
is clean." If you want the observations for the files you are about to touch, regenerate first:

```sh
node tools/analysis-digest/index.mjs
```

Then read only the shards for your files. Do not read `index.json` (~31 KB) or the whole digest (1.34 MB).

### NFR9 — what this story may and may not claim

ADR 0040 § 7 claims the **weaker** reading of "reproducible": *built from a clean checkout by CI*, **not**
byte-identical rebuilds. This story closes **exactly one** of its named gaps: **version-from-tag**.

- `npm ci` — **already closed** by `0b1f561` (R1), credited to 16.2, not to you.
- `SOURCE_DATE_EPOCH` — **16.4's.** The csproj already honours it (`:36-38`); no workflow sets it.
- `<Deterministic>` / `ContinuousIntegrationBuild` / SourceLink — **deferred** past preview, to 17.4's burndown.
- Byte-identical Nuxt rebuilds — **explicitly out of scope.**

**Do not let the completion notes claim more than version-from-tag.** 16.1 § 6.1's table is the ledger.

---

## Acceptance Criteria

**AC #1 and AC #2 are `epics.md` § Story 16.3 verbatim.** #3–#6 make ADR 0040's decisions and 16.1's routed
defects checkable; they add no scope the ADR and the spike did not already assign here.

1.
**Given** Story 16.1's channel decision
**When** packaging runs
**Then** the CLI is produced as the chosen artifact(s) — a NuGet global-tool package and/or self-contained
per-OS executables — reproducibly from the repository, with the version derived from the release tag rather than
a hard-coded csproj value.
   - `<Version>` is **deleted** from `SpecScribe.csproj`, not replaced by a second literal (ADR 0040 § 5).
   - MinVer 7.0.0 supplies it, `PrivateAssets="All"`, with the R4 property set.
   - A commit carrying tag `v0.1.0-preview.1` produces exactly `0.1.0-preview.1`. **Proven, by tagging.**
   - An **untagged** build still produces a `0.x` version carrying a pre-release label (R4). Stated and shown.
   - The `+<sha>` suffix is still present in `AssemblyInformationalVersion` — **read the generated file** (R4).

2.
**Given** a produced package
**When** a user follows the documented install path (for example `dotnet tool install -g SpecScribe`)
**Then** the `specscribe` command runs and `--version`/`--help` report correctly
**And** the packaged README/license render on the package listing.
   - Proven against a **local feed** (`--add-source ./artifacts`), installed to a `--tool-path`. **Nothing is
     pushed to nuget.org or npm** — that is 16.4's, and it is blocked on owner actions besides (R11).
   - `--version` and `-v` exit **0** and print the version. **They exit 1 today** (R2).
   - `--help` still exits 0 and its output is unchanged apart from the new version option.
   - The nupkg contains `README.md` at its root and declares `PackageLicenseExpression=MIT`; the README's
     badge hosts (`github.com/…/badge.svg`, `sonarcloud.io`) are both on nuget.org's allow-list, and there are
     **no relative-path images** (verified — relative images silently do not render). Its **8 relative links**
     (`LICENSE`, `docs/adrs/…`, …) will not resolve on nuget.org; record that as a known listing limitation and
     leave the rewrite decision to 16.6 rather than half-doing it here.

3.
**Given** ADR 0040 § Decision 1
**When** the package is produced
**Then** the renderer artefact ships **inside** it: packed at `tools/$(TargetFramework)/any/renderer/**` for the
`dotnet` global tool, and copied as a sibling `renderer/` directory beside the executable for a self-contained
publish
**And** a `generate` run **from a different repository**, with `SPECSCRIBE_RENDERER_DIR` **unset**, completes at
`errors=0`
**And** the negative case is proven too: with the payload renamed away, the run **fails** rather than falling
through to another checkout's artefact.
   - Running the probe *inside this repository* lets candidate 3 (`web/.output`) succeed and **reports a false
     pass** — 16.1 § 2.3 makes the negative case the proof the probe was genuinely foreign. Do both.
   - `SPECSCRIBE_RENDERER_DIR` remains the explicit override with its hard-fail-on-miss semantics, unchanged.
   - Cross-RID scope: the sibling-copy mechanism is RID-agnostic and is proven **on the host RID**. Producing
     and *executing* `linux-x64` / `osx-arm64` binaries on their own OSes is **16.4's CI matrix**. Say so; do
     not claim a platform you did not run on.

4.
**Given** that `web/.output` is gitignored and absent on a fresh checkout, and that an MSBuild glob matching
nothing is not an error
**When** `dotnet pack` (or the self-contained publish) runs without a built renderer artefact
**Then** it **fails loudly**, naming the artefact and the command that builds it
**And** the assertion is made against the **packaged path** inside the produced nupkg
(`tools/<tfm>/any/renderer/server/index.mjs`), not against payload size or entry count
**And** `dotnet build` and `dotnet test` are unaffected by the assertion.
   - R3's wrong `PackagePath` form produced 187 entries, the right byte total, exit 0, and **no entry point**.
     A size-or-count check passes that. This AC exists to catch exactly it.

5.
**Given** Story 16.1 § 10 items 2 and 4, both reproduced and both routed to this story
**When** this story completes
**Then** `FindRepoRoot` recognises a git worktree (`.git` as a **file**, not only a directory), so a worktree
never resolves candidate 3 to another checkout's artefact
**And** a non-200 from the renderer reports the renderer's **own error text**, not only `HTTP <code>`
**And** both are covered by tests that require **neither Node nor a built artefact**, per the standing
constraint recorded at `NuxtPrerenderTests.cs:14-16`.
   - The failure-text extraction is bounded and the server-log tail is emitted once per run, not per route (R5).

6.
**Given** CLAUDE.md's scope and concurrency rules
**When** this story completes
**Then** its File List is limited to `src/SpecScribe/**`, `tests/SpecScribe.Tests/**`, `README.md` (only the
statements R9 names), and `docs/Packaging.md`
**And** it edits **no** file under `.github/**` (R6), `web/**`, or `extension/**`
**And** it pushes **nothing** to nuget.org, npm or the VS Marketplace, and pushes **no git tag** (R11)
**And** no structural scope change is made to `epics.md` or `sprint-status.yaml`, recorded explicitly so the
absent diff reads as a decision rather than an oversight
**And** the regression floor is green: `dotnet test` plus all four `web/` gates, with any gate movement
**investigated, not re-pinned** (R8).

---

## Tasks / Subtasks

- [ ] **Task 1 — Establish the baseline before changing anything (AC: #1, #6)**
  - [ ] `git rev-parse --short HEAD` and `git status --porcelain`; record both. Shared `main` — see CLAUDE.md.
  - [ ] `cd web && SPECSCRIBE_PACKAGE_BUILD=1 npm ci && npm run sync:assets && npm run build:package` (R1).
        Confirm `web/.output/server/index.mjs` now exists.
  - [ ] `dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental` (R8 — embedded assets).
  - [ ] `dotnet test` and `cd web && npm run check`. **Record the pass/fail counts now**, so a later move is
        attributable. Expect a `FileWatcherServiceTests` timing flake; 16.1 saw 2962/1/3 and 11/11 on re-run.
  - [ ] Record `--version`, `-v` and `--help` exit codes on the CURRENT binary, so R2's fix is demonstrable.

- [ ] **Task 2 — Version from the tag via MinVer (AC: #1)**
  - [ ] Add `<PackageReference Include="MinVer" Version="7.0.0" PrivateAssets="All" />`.
  - [ ] **Delete** `<Version>0.1.0-preview</Version>` (`SpecScribe.csproj:19`) and the now-stale half of the
        comment above it at `:17-18`. Do not leave a second version literal anywhere.
  - [ ] Add `MinVerTagPrefix`, `MinVerMinimumMajorMinor`, `MinVerDefaultPreReleaseIdentifiers` (R4).
  - [ ] Build, then **read `src/SpecScribe/obj/*/net10.0/SpecScribe.AssemblyInfo.cs`**. Assert three things:
        the version is `0.1.0-preview.0.<height>`; the `+<40-hex>` sha suffix is **still present**; and
        `AssemblyInformationalVersionAttribute` is what you expect. 🚨 The sha loss is silent (R4).
  - [ ] Prove version-from-tag: `git tag v0.1.0-preview.1` on a throwaway local commit → rebuild → confirm the
        version is exactly `0.1.0-preview.1` with **no** height. **Then `git tag -d` it.** Do **not** push a
        tag (see § Owner actions — the first real tag is the owner's call and 16.4's trigger).
  - [ ] Confirm the About page still shows the `Preview` badge: generate, then read the rendered `about.html`.

- [ ] **Task 3 — Ship the renderer inside the package (AC: #3)**
  - [ ] Add the `None Include` pack item in **exactly** R3's form. No `%(RecursiveDir)`. `$(TargetFramework)`.
  - [ ] `dotnet pack src/SpecScribe/SpecScribe.csproj -c Release -o artifacts`.
  - [ ] **List the nupkg's entries** and confirm `tools/net10.0/any/renderer/server/index.mjs` is present at
        that exact path — the TFM substitution resolved, and the tree is not doubled (R3).
  - [ ] Record the size delta against a no-renderer pack. 16.1 measured +1,241,709 B (+49.4%) for 3.96 MB / 187
        files, and noted the figure **has moved twice** — § 2.6 says **derive it, do not quote it**.
  - [ ] Wire the self-contained publish to place a sibling `renderer/`. 16.1 § 2.4 measured the publish dir
        holding exactly 2 files (`specscribe.exe`, `.pdb`) before the payload, so `renderer/` is the only
        sibling packaging must place, and `PublishSingleFile` does **not** move `AppContext.BaseDirectory`.

- [ ] **Task 4 — Make a missing payload impossible to ship (AC: #4)**
  - [ ] Add the post-pack assertion on the **packaged path** (R7). MSBuild's built-in `Unzip` needs no new dep.
  - [ ] Prove it fires: move `web/.output` aside, `dotnet pack`, confirm a **failure** naming the artefact and
        `cd web && npm run build:package`. Restore.
  - [ ] Prove it does **not** fire on `dotnet build` or `dotnet test`.
  - [ ] ⚠️ Also prove it would have caught R3's wrong form — temporarily reintroduce `%(RecursiveDir)`, confirm
        the assertion goes red where a size/count check would have gone green, then revert. This is the one
        check that validates the guard itself rather than the payload.

- [ ] **Task 5 — `--version` (AC: #2)**
  - [ ] Add `config.SetApplicationVersion(...)` in `Program.cs`, sourced from the assembly (reuse
        `ProductMetadata.FromAssembly().Version` so the CLI and the About page cannot drift).
  - [ ] Run `--version`, `-v`, `--help`. All three must exit **0**. If the global flag is still not intercepted
        under `CommandApp<TDefaultCommand>` + `UseStrictParsing`, fall back to an explicit settings flag and
        **say which you shipped** (R2).
  - [ ] Confirm no short-option collision (`-a`, `-o`, `-p`, `-s`, `-h` are taken; `-v` is free — verified).

- [ ] **Task 6 — The two inherited defects (AC: #5)**
  - [ ] `FindRepoRoot`: accept `.git` as a file or a directory; widen to `internal`; test with a temp dir whose
        `.git` is a **file**.
  - [ ] Non-200 diagnostics: add the bounded `internal static` extraction helper; use it at
        `NuxtPrerender.cs:288-291`; emit the server-log tail once per run. Unit-test the helper against a
        Nitro-shaped JSON body **and** a non-JSON body.
  - [ ] Keep both tests Node-free and artefact-free (`NuxtPrerenderTests.cs:14-16`).

- [ ] **Task 7 — Prove it end-to-end from a FOREIGN repository (AC: #2, #3)**
  - [ ] `dotnet tool install SpecScribe --version <the MinVer version> --tool-path ./probe-tools --add-source ./artifacts`.
        ⚠️ **Read the version from the produced nupkg filename** — under MinVer it is no longer a literal you
        can type from memory.
  - [ ] In a **different git repository** with **no `web/` directory** and `SPECSCRIBE_RENDERER_DIR` **unset**:
        assert both preconditions first (16.1 asserted them rather than assuming), then `generate`, and require
        `errors=0`. Use `$CLAUDE_JOB_DIR/tmp`, not `/tmp`.
  - [ ] **The negative case:** rename the packaged `renderer/` away, re-run, and confirm it **fails** — proving
        candidate 3 did not silently rescue it, i.e. that the probe repo was genuinely foreign (16.1 § 2.3).
  - [ ] Run `specscribe --version` and `--help` from the installed tool, not just from `bin/Debug`.

- [ ] **Task 8 — Documentation, only where this story falsified it (AC: #2, #6)**
  - [ ] Fix the three README sites in R9's table. Remove the `SPECSCRIBE_RENDERER_DIR` step from the published
        CI recipe (ADR 0040 § Consequences) and the "bump the `<Version>`" instruction.
  - [ ] Write `docs/Packaging.md`: the build order from R1, the pack shape, how to verify a package, and R3/R7's
        traps. This is the artifact 16.4 builds its pipeline from.
  - [ ] Do **not** create `CHANGELOG.md`, write listing copy, or surface the Node prerequisite on listings —
        16.6's, all three (R9).

- [ ] **Task 9 — Regression floor and scope guard (AC: #6)**
  - [ ] `dotnet build --no-incremental`, `dotnet test`, `cd web && npm run check` (all four gates).
  - [ ] If any gate moved: **establish causality before touching a baseline** (R8). Bisect into a throwaway tree
        via `git archive HEAD` into the scratchpad — never by resetting the shared tree.
  - [ ] `git status --porcelain .github web extension` → must be **empty**. Verify the revert of every temporary
        probe edit **with that command**, not by remembering you made it (16.1 § 7.1).
  - [ ] Grep for each symbol you added before relying on it — CLAUDE.md § Concurrent work: a write that returned
        success has silently vanished in this repository before.
  - [ ] Record in the completion notes: no structural scope change; whose commits your work sat on top of.

---

## Dev Notes

### 👤 Owner actions — this story cannot fully close without them

1. **Ratify ADR 0040** (carried from 16.1 AC #4; owner action § 8 item 6). It is authored and complete at
   `Proposed`. R10 explains why this story proceeds without it and what the residual exposure is.
2. **Decide whether the first real tag gets pushed.** This story proves version-from-tag with a *local,
   deleted* tag. Pushing `v0.1.0-preview.1` sets the version floor for every subsequent build and is 16.4's
   release trigger — which does not exist yet. Recommendation: **do not push a tag until 16.4 lands.**
3. **Reserve `SpecScribe` on nuget.org and the five npm names** (16.1 § 5.4, owner action § 8 item 1). All were
   404 on 2026-08-07. This is the only item a third party can take from you.
4. **Confirm Trusted Publishing is visible on your nuget.org account** — 16.1 § 8 item 2 says *"before 16.4
   starts, not during."* Not this story's blocker; re-flagged because this story is the one that makes a
   publishable package exist.

### Files being modified — current state, what changes, what must be preserved

**`src/SpecScribe/SpecScribe.csproj` (UPDATE).** 76 lines. Today: `PackAsTool`/`ToolCommandName`/`PackageId`
with the `<Version>` literal at `:19`; a `SOURCE_DATE_EPOCH`-honouring build-date stamp at `:35-42` (16.4 will
set the variable — **leave this alone**); four `PackageReference`s; `InternalsVisibleTo SpecScribe.Tests` at
`:52`; and seven `EmbeddedResource` assets plus the packed README at `:55-74`.
*Changes:* `<Version>` deleted, MinVer added with three properties, one `None Include` pack item, one post-pack
assertion target.
*Preserve:* every `EmbeddedResource` (they are load-bearing and CLAUDE.md's non-incremental rule exists for
them), `PackageReadmeFile`, `PackageLicenseExpression`, the `SOURCE_DATE_EPOCH` block, `InternalsVisibleTo`.

**`src/SpecScribe/NuxtPrerender.cs` (UPDATE).** Resolution order at `:73-127` — override, then `renderer/`
beside the assembly, then `web/.output` — and its doc comment at `:68` calls candidate 2 *"the Epic 16
packaging shape"*, i.e. **this story is what finally populates it.**
*Changes:* `FindRepoRoot` at `:129-137`; the non-200 branch at `:288-291`; one new `internal static` helper.
*Preserve:* the override's hard-fail-on-miss (`:80-98`) and the reasoning comment above it; the
Node-before-artefact ordering (`:228-230`, mirrored in `SiteGenerator.PrerenderPreflight`); the
`MainLandmark` empty-shell check and **why it anchors on the full landmark** (`:34-35, 292-297`) — this portal
renders its own source, so a loose `<main` probe matches prose; readiness **polled, never slept** (`:344-372`);
`TryKill` in `finally`.

**`src/SpecScribe/Program.cs` (UPDATE).** 76 lines, top-level statements. `CommandApp<InteractiveCommand>` at
`:15`; `SetApplicationName` + `UseStrictParsing` at `:18-19`; four examples; four commands; three exception
arms at `:56-76`.
*Changes:* one `SetApplicationVersion` line.
*Preserve:* `UseStrictParsing` (`:19` — "typo'd options should fail loudly"), the single-token-only rule for
examples (`:36-37`), `PropagateExceptions`, and the `CommandParseException` → interactive-menu fallback.

**`tests/SpecScribe.Tests/NuxtPrerenderTests.cs` (UPDATE).** Its header states the standing constraint: these
tests are *"deliberately Node-free and artefact-free"* because Story 23.6's Dev Notes forbid making the C# unit
suite depend on either. It also carries a note that Node detection *"was assigned to Story 16.3, which has not
been built"* (`:7-12`) — **that note is now stale in one direction and still true in another**: detection
shipped in 23.6 and this story does **not** touch it (16.1 § 9 is explicit), but "16.3 has not been built" stops
being true here. Update the wording; do not weaken the constraint.

**`README.md` (UPDATE, narrowly).** See R9's table. Three sites, all falsified by this story's own change.

**`docs/Packaging.md` (NEW).** The build order, the pack shape, how to verify, and the traps.

### Testing standards

- xUnit, `tests/SpecScribe.Tests`. `InternalsVisibleTo` already permits testing `internal` members — that is
  the lever for both defect fixes, and it is why widening `FindRepoRoot` to `internal` is cheap.
- **Test the pure helper, not the process.** `NuxtPrerender.Render` needs a live Node server; a
  `DescribeRouteFailure(status, body)` helper does not. This is the same split
  `ValidateNodeVersion`/`VerifyNodeAvailable` already uses, and its doc comment at `:175-181` explains the trap:
  a shim-based test *"silently exercises the ABSENT path instead and passes for the wrong reason."*
- Assert on **behavior a user sees**: the message names the artefact and the build command; the version prints;
  the walk stops at the worktree root. Not on internal call shapes.
- ⚠️ **No test currently guards the `+<sha>` suffix.** `AboutTemplaterTests.cs:13-23` asserts only that the
  trimmed version contains no `+`. If you want R4's silent regression to stay caught, add one.

### Project structure notes

- Generate to `SpecScribeOutput/` (the default). **Never** `--output docs/live` — vestigial and gitignored.
- Probe repositories, scratch packs and tool-paths go in `$CLAUDE_JOB_DIR/tmp`, never `/tmp` (parallel jobs
  clobber each other there) and never inside the repository.
- `artifacts/`, `probe-*/`: confirm they are gitignored or removed before the final `git status` check.

### Scope guard — why nothing structural changes

No story is added, removed or renumbered. This story implements ACs that already exist in `epics.md` § Story
16.3 plus decisions already recorded in ADR 0040, so per CLAUDE.md **neither `epics.md` nor `sprint-status.yaml`
needs a structural edit.** Recorded explicitly so the absence of an `epics.md` diff reads as a decision.

No new ADR: ADR 0040 decides the channel, the packaging shape, the versioning mechanism, the RID matrix and the
credential posture. R4's three MinVer properties sit **inside** § Decision 5's scheme. If you deviate, R10.

### Concurrent-work discipline (CLAUDE.md)

- **Assume another agent is editing these files right now.** Grep for every symbol you add before relying on it.
- **Never `git reset --hard`, `git checkout --`, or `git clean`.** This has destroyed real work here before.
- **Rebuild `--no-incremental`** before trusting anything involving an embedded asset — you are editing the very
  `ItemGroup` that holds them.
- **`.github/**` belongs to Story 16.2 this sprint** (R6). Staying out of it is a deliberate scope choice, not
  an oversight; if you must enter it, attribute by **hunk** and name 16.2 as co-owner in the File List.
- Worktrees **are** available on this machine — five were live during 16.1, and the last several commits on
  `main` are worktree merges. CLAUDE.md's "the primary machine cannot run parallel git worktrees" is **stale**
  (16.1 § 10 item 9, routed to the owner / next retro). Do not act on that sentence.

### References

- [ADR 0040 — Release Channels, Packaging Shape, Credential Posture and Versioning Policy](../../docs/adrs/0040-release-channels-and-versioning-policy.md)
  — § Decision 1 (pack path, sibling copy, shared npm renderer), § 2 (preview cut + RID matrix + non-goals),
  § 5 (MinVer, `<Version>` deleted, the pre-release label as a product surface), § 7 (NFR9's weak reading),
  § 9 (the tag/gate relationship), § Consequences (`fetch-depth: 0`, the README's lost env step)
- [16-1-spike-report.md](16-1-spike-report.md) — § 2.1 (build order), § 2.2–2.4 (the two measured channels),
  § 2.3 (**the negative case as proof**), § 2.6 (size delta — derive it), **§ 2.7 (the four wrong answers)**,
  § 4.1–4.2 (the two defects routed here), § 6.2/6.4 (MinVer, and the four version literals),
  § 8 (owner actions), § 9 (what changes for 16.3), § 10 (open items 2 and 4)
- [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) — `build:package`
  never `build`; Node is a generate-time runtime; **§ Decision 5's "at startup" is AMENDED** by ADR 0040 § 8, so
  Node detection is **not** this story's (16.1 § 9)
- [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) — the channel list ADR 0040 amends
- [ADR 0034](../../docs/adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md) — why the renderer
  is mandatory, and where `GoldenContentFingerprint` was retired
- `src/SpecScribe/SpecScribe.csproj:14-24,35-42,52,55-74` · `NuxtPrerender.cs:65-137,226-340,344-377` ·
  `Program.cs:15-19,56-76` · `AboutTemplater.cs:90,125-140` · `SiteGenerator.cs:4646-4666`
- `.github/workflows/build-test-analyze.yml:66-70,220-246,281-290,359-363,406-420` — `fetch-depth: 0` already
  set; the load-bearing install/build order; the `+4/-182` gate signature
- `web/scripts/harness-lib.mjs:54-61` — `normalizeVolatile` folds the version token, which is why a
  per-commit version does not move the parity gate
- [MinVer](https://github.com/adamralph/minver) (7.0.0, 2026-01-05, SDK ≥ 8.0) ·
  [Package readme on nuget.org](https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org)
  (allowed image domains; relative-path images do not render) ·
  [Spectre.Console — built-in command behaviors](https://spectreconsole.net/cli/reference/built-in-command-behaviors/)
  (`-v`/`--version` requires `ApplicationVersion`)
- `CLAUDE.md` § Concurrent work · § Which gate is which · § Verification · § Decision records

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
