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

Status: review

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

- [x] **Task 1 — Establish the baseline before changing anything (AC: #1, #6)**
  - [x] `git rev-parse --short HEAD` and `git status --porcelain`; record both. Shared `main` — see CLAUDE.md.
        → `c73ebcb`, tree CLEAN. (Story frontmatter `baseline_commit: 07bdb79` PRESERVED per the workflow; main
        advanced TWO merges — `6120d2a` 23.2 and `c73ebcb`/`a2eee2a` 16.2 — between create-story and dev-story.)
  - [x] `cd web && SPECSCRIBE_PACKAGE_BUILD=1 npm ci && npm run sync:assets && npm run build:package` (R1).
        Confirm `web/.output/server/index.mjs` now exists. → all three OK; entry point present, 2.18 MB artefact.
        R1 CONFIRMED: the flag is not optional (npm 11.16.0 / Node 24.18.1). No `allowScripts` warning appeared.
  - [x] `dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental` (R8 — embedded assets). → 0 warnings.
  - [x] `dotnet test` and `cd web && npm run check`. **Record the pass/fail counts now.**
        → `dotnet test` **2978 passed / 0 failed / 3 skipped**. NO FileWatcher flake (16.2's `FileShare.Delete`
        fix holds). Gates: `check:tokens` OK · `check:ir-content` **-2 rules** (environmental, see notes) ·
        `check:assets` OK · `check:parity` OK 24/24 routes, 14/14 families.
  - [x] Record `--version`, `-v` and `--help` exit codes on the CURRENT binary. → **1 / 1 / 0**. R2 confirmed
        verbatim: `Fatal error: Unknown option 'version'.` / `'v'.`

- [x] **Task 2 — Version from the tag via MinVer (AC: #1)**
  - [x] Add `<PackageReference Include="MinVer" Version="7.0.0" PrivateAssets="All" />`.
  - [x] **Delete** `<Version>0.1.0-preview</Version>` and the now-stale half of the comment above it. No second
        version literal anywhere (verified by grep: `<Version>` survives only inside the replacement comment).
  - [x] Add `MinVerTagPrefix`, `MinVerMinimumMajorMinor`, `MinVerDefaultPreReleaseIdentifiers` (R4).
  - [x] Build, then **read `SpecScribe.AssemblyInfo.cs`**. → `0.1.0-preview.0.410+c73ebcb8f2e33f9ef452afdc5c8cb5f0c18d06d8`.
        All three assertions hold: R4's predicted shape, the `+<40-hex>` sha **still present**, and the attribute
        is `AssemblyInformationalVersionAttribute`. ⚠️ ONE UNPREDICTED SIDE EFFECT FOUND: MinVer also sets
        `AssemblyVersion` to `{Major}.0.0.0`, i.e. `0.1.0.0` → `0.0.0.0`. Traced every reader — only
        `AboutTemplater.cs:70`'s `GetName().Version` fallback, which is unreachable while an informational
        version exists (MinVer always writes one); the cache-busting token is a module hash, not a version.
        Documented in the csproj rather than fought.
  - [x] Prove version-from-tag: `git tag v0.1.0-preview.1` → rebuild → **exactly `0.1.0-preview.1`, NO height**,
        sha suffix intact. **`git tag -d` executed immediately; `git tag -l` re-verified EMPTY.** Nothing pushed.
        (Tagged HEAD rather than a throwaway commit — tags are SHARED repo state, not per-worktree, so the
        window was kept to seconds.)
  - [x] Confirm the About page still shows the `Preview` badge. → Read from the rendered `about.html` produced by
        the PACKAGED tool in the foreign probe repo: `<span class="preview-badge">Preview</span>`, Version row
        `0.1.0-preview.0.410`, **Build row `2026-08-07 · c73ebcb`** — the sha survived end to end into the page.

- [x] **Task 3 — Ship the renderer inside the package (AC: #3)**
  - [x] Add the pack item. ⚠️ **SHIPPED AS ONE `Content` ITEM, NOT R3's `None`+`PackagePath` — see Finding F1 in
        the completion notes.** The measured outcome is identical and the `None` item was proven INERT.
  - [x] `dotnet pack src/SpecScribe/SpecScribe.csproj -c Release -o artifacts`.
  - [x] **List the nupkg's entries** → `tools/net10.0/any/renderer/server/index.mjs` present at exactly that
        path, exactly once. TFM substitution resolved (`net10.0`, not `$(TargetFramework)`); no doubled tree; no
        duplicate paths; **zero `contentFiles/` leakage** (the `Pack="false"` that prevents it is load-bearing).
  - [x] Record the size delta against a no-renderer pack — **DERIVED, not quoted**: 2,520,789 B / 25 files →
        3,739,917 B / 203 files = **+1,219,128 B (+48.4%)** for a 178-file payload. (16.1: +1,241,709 / +49.4%
        / 187 files. It has moved a third time, as § 2.6 predicted.)
  - [x] Wire the self-contained publish to place a sibling `renderer/`. → `dotnet publish -r win-x64
        --self-contained`: sibling `renderer/` with **178 files**, entry point present, and the published binary
        ran `generate` **from the foreign repo at `errors=0`** with `SPECSCRIBE_RENDERER_DIR` unset.

- [x] **Task 4 — Make a missing payload impossible to ship (AC: #4)**
  - [x] Add the post-pack assertion on the **packaged path** (R7). `AssertRendererPacked`, MSBuild's built-in
        `Unzip`, no new dependency. Plus `AssertRendererAvailableForPublish` for the publish half.
  - [x] Prove it fires: moved `web/.output` aside → `dotnet pack` **FAILED**, naming the artefact path and
        `cd web && npm run build:package`. Restored and re-verified. ⚠️ Note which guard fires: `PackAsTool`
        packs VIA publish, so the PUBLISH guard trips first and **no broken nupkg is produced at all** — better
        than the designed behaviour, but worth knowing when reading the error.
  - [x] Prove it does **not** fire on `dotnet build` or `dotnet test`. → With `web/.output` still renamed away:
        `dotnet build --no-incremental` **succeeded**, `dotnet test` **38/38 passed**.
  - [x] ⚠️ Prove it would have caught R3's wrong form. → Done, and it exposed Finding F1 en route. With BOTH
        items present the wrong `%(RecursiveDir)` form stayed **GREEN** (the `Content` copy masked it); with the
        `Content` item disabled it went **RED**. Re-proven against the mechanism actually shipped by doubling the
        `Content` item's `Link`: the bad package had **203 files and 10,675,619 bytes — identical count AND
        identical total size to the good one — and no entry point.** A size-or-count check passes that; the
        packaged-path assertion fails it. Every probe edit reverted and re-verified by grep.

- [x] **Task 5 — `--version` (AC: #2)**
  - [x] Add `config.SetApplicationVersion(...)` in `Program.cs`, sourced from `ProductMetadata.FromAssembly()`.
  - [x] Run `--version`, `-v`, `--help`. → **0 / 0 / 0**, all printing `0.1.0-preview.0.410`. **`SetApplicationVersion`
        alone WAS sufficient** under `CommandApp<TDefaultCommand>` + `UseStrictParsing` — the R2 fallback (an
        explicit settings flag) was NOT needed and was NOT shipped. Re-verified from the INSTALLED tool too.
  - [x] Confirm no short-option collision. → `--help` diff before/after is exactly **one added line**
        (`-v, --version  Prints version information`); nothing else moved.

- [x] **Task 6 — The two inherited defects (AC: #5)**
  - [x] `FindRepoRoot`: accepts `.git` as a file **or** a directory; widened to `internal`; tested against a temp
        tree with an outer `.git` DIRECTORY and a nested worktree whose `.git` is a FILE. **Red-green proven**:
        with the fix reverted that one test fails and the other two still pass, so it discriminates the defect
        rather than the walk. Also verified operationally — see the completion notes.
  - [x] Non-200 diagnostics: `DescribeRouteFailure(HttpStatusCode, string?)` + `ExtractRendererMessage`, bounded
        at 500 chars with newline collapsing; wired in at the non-200 branch; server-log tail attached **once per
        run** via `serverLogAttached`. Unit-tested against a Nitro-shaped JSON body, a non-JSON HTML body, empty/
        whitespace/null bodies, the 20,000-char flood, and newline collapsing.
  - [x] Keep both tests Node-free and artefact-free. → No Node, no artefact, no network; temp dirs only.
        The file's stale "Story 16.3 has not been built" header note was corrected without weakening the rule.

- [x] **Task 7 — Prove it end-to-end from a FOREIGN repository (AC: #2, #3)**
  - [x] `dotnet tool install SpecScribe --version 0.1.0-preview.0.410 --tool-path … --add-source ./artifacts`.
        Version **read off the produced nupkg filename**, never typed.
  - [x] Foreign repo in `$CLAUDE_JOB_DIR/tmp/foreign-repo`. **Preconditions ASSERTED, not assumed**: `web/`
        absent, `SPECSCRIBE_RENDERER_DIR` empty, real `.git`, outside the SpecScribe checkout. → `generate`
        **errors=0, exit 0**. On-disk proof of the packaged shape:
        `probe-tools\.store\specscribe\0.1.0-preview.0.410\specscribe\0.1.0-preview.0.410\tools\net10.0\any\renderer`.
  - [x] **The negative case:** renamed the packaged `renderer/` away → re-run **FAILED, errors=1, exit 1**, with
        the three-location message. Candidate 3 did **not** rescue it, so the probe repo was genuinely foreign
        and the positive result above is not a false pass. Restored → `errors=0` again.
  - [x] Run `specscribe --version` and `--help` from the installed tool. → **0 / 0**, `0.1.0-preview.0.410`,
        88-line help.

- [x] **Task 8 — Documentation, only where this story falsified it (AC: #2, #6)**
  - [x] Fix the three README sites in R9's table. Removed the `SPECSCRIBE_RENDERER_DIR` step and env block from
        the published CI recipe, and the "bump the `<Version>`" instruction. **Plus one site R9 did not list and
        this story's own change newly falsified**: that recipe's `actions/checkout` of `.specscribe-src` was
        shallow, and MinVer on a tagless shallow clone yields a WRONG VERSION rather than an error — added
        `fetch-depth: 0` with the reason. (README's embedded consumer recipe, NOT `.github/**` — see AC #6.)
        The hard-coded `--version 0.1.0-preview` became a derivation from the nupkg filename.
  - [x] Write `docs/Packaging.md` — build order, pack shape, versioning, the two traps, the four-step
        verification recipe including the mandatory negative case, and an explicit § What this does not cover.
  - [x] Do **not** create `CHANGELOG.md`, write listing copy, or surface the Node prerequisite on listings.
        → None of the three done. The nuget.org relative-link limitation is RECORDED in `docs/Packaging.md`
        as a known limitation with the rewrite left to 16.6, rather than half-done here.

- [x] **Task 9 — Regression floor and scope guard (AC: #6)**
  - [x] `dotnet build --no-incremental`, `dotnet test`, `npm run check` (all four gates).
        → build OK; **2989 passed / 0 failed / 3 skipped** (baseline 2978 → **+11, exactly the 11 tests added**);
        `check:tokens` OK · `check:ir-content` **-2, IDENTICAL to baseline** · `check:assets` OK · `check:parity`
        OK 24/24, 14/14.
  - [x] If any gate moved: establish causality first. → **NO gate moved and NO baseline was regenerated.**
        `check:ir-content`'s `-2` was present BEFORE any edit (proven: `git status` showed zero source changes)
        and its cause was traced, not assumed — see the completion notes.
  - [x] `git status --porcelain .github web extension` → **EMPTY**, re-run after all probe reverts. `git tag -l`
        → **empty**. `artifacts/` confirmed gitignored (`!!`); all probe dirs live in `$CLAUDE_JOB_DIR/tmp`.
  - [x] Grep for each symbol added before relying on it. → All 14 verified present at named line numbers.
  - [x] Record in the completion notes: no structural scope change; whose commits this sat on top of.

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

Claude Opus 5 (1M context) — `claude-opus-5[1m]`, via `bmad-dev-story`, 2026-08-07.

### Debug Log References

Executed in worktree `worktree-story-16-3-dev` at `c73ebcb` (story frontmatter `baseline_commit: 07bdb79`
PRESERVED per the workflow). Probe artefacts — foreign repo, tool-path install, self-contained publish — all in
`$CLAUDE_JOB_DIR/tmp`, never inside the repository.

| # | probe | result |
|---|---|---|
| 1 | `--version` / `-v` / `--help` BEFORE | `1 / 1 / 0` — R2 reproduced verbatim |
| 2 | `--version` / `-v` / `--help` AFTER | `0 / 0 / 0`; `--help` diff = exactly one added line |
| 3 | `AssemblyInfo.cs` after MinVer | `0.1.0-preview.0.410+c73ebcb8…` — sha survived |
| 4 | local tag `v0.1.0-preview.1` | exactly `0.1.0-preview.1`, no height; tag deleted, `git tag -l` empty |
| 5 | pack, both items | 203 files, entry point present |
| 6 | pack, `Content` only | 203 files, **byte-identical** |
| 7 | pack, `None` only | 203 files, entry point present |
| 8 | pack, `None` WRONG-FORM + `Content` | 203 files, entry point present, **no doubled tree** → F1 |
| 9 | pack, `None` WRONG-FORM, `Content` off | **RED** — guard fires |
| 10 | pack, `Content` `Link` doubled | **RED** — 203 files / 10,675,619 B, identical to good, no entry point |
| 11 | pack, `web/.output` renamed away | **RED** (publish guard, before any nupkg exists) |
| 12 | `build` + `test`, artefact absent | build OK, 38/38 tests pass — guards correctly scoped |
| 13 | foreign repo, packaged tool | `errors=0`, exit 0 |
| 14 | foreign repo, renderer renamed away | `errors=1`, exit 1 — probe proven genuinely foreign |
| 15 | foreign repo, self-contained `win-x64` | `errors=0`, exit 0; sibling `renderer/` = 178 files |
| 16 | worktree generate, env var UNSET | `errors=0`, 1553 routes — R5(a) fixed operationally |

### Completion Notes List

**All 9 tasks complete. All 6 ACs satisfied.** SpecScribe now packages as a `dotnet` global tool and as a
self-contained binary that each **carry their own renderer** and render from a foreign repository with no
configuration; the version comes from the git tag; `--version` works for the first time; and both defects 16.1
routed here are fixed and tested.

#### 🚨 F1 — the prescribed two-item packaging shape was WRONG, and the wrong half was the one that hides defects

The story's R3/R11 prescribed **two** MSBuild items — a `None` with `Pack="true"` + `PackagePath` for the nupkg,
and a publish-time copy for the binary — reasoning that pack and publish are separate pipelines. Half of that is
right: a publish copy is invisible to a plain `dotnet pack` of a *library*. But **`PackAsTool` builds
`tools/<tfm>/any/` FROM THE PUBLISH OUTPUT**, so here the publish copy populates the nupkg as well.

Measured four ways (probes 5–8), each a full pack plus `unzip -l`. The finding is probe 8: with the `Content`
item present, the `None` item contributes **nothing — not even its own damage**. A deliberately broken
`PackagePath` on it left the package *byte-identical to correct* and the guard **green**. So shipping both items
would have shipped one item that looks load-bearing, is not, and silently absorbs its own defects.

**Shipped one `Content` item instead**, proven to produce a byte-identical package (probe 6) and to serve the
publish channel (probe 15). This is a deviation from the story's prescribed *mechanism*, not from ADR 0040:
§ Decision 1 specifies the packaged PATH and the sibling directory, both of which are unchanged and asserted on
every pack. Per R10 this is recorded here rather than buried, and it is **not** an ADR amendment — no ADR
statement is contradicted. It IS a correction to 16.1 § 2.7(1)'s premise, which 16.4 should inherit;
`docs/Packaging.md` § "One item, not two" carries the measurement table so the next story does not re-derive it.

#### AC #4's guard is validated against the mechanism actually shipped

Probe 10 is the one that matters, and it is the story's own trap reproduced in the real wiring: a doubled `Link`
produced a package with **203 files and 10,675,619 bytes — identical count and identical total size to the good
package — and no entry point.** A size-or-count check calls that a pass. The packaged-path assertion calls it a
failure. Both guards are scoped so `dotnet build` and `dotnet test` are untouched (probe 12).

One behaviour worth knowing: because `PackAsTool` packs via publish, the **publish** guard fires first on a
missing artefact, so `dotnet pack` fails *before* producing a broken nupkg rather than after. Better than
designed; documented in `docs/Packaging.md` so a reader is not surprised by which error they get.

#### R4's silent regression checked — and one side effect the story did not predict

The `+<sha>` suffix survived MinVer (probe 3) and reaches the rendered page: the About page's Build row shows
`2026-08-07 · c73ebcb`, read from the site the PACKAGED tool generated. The `Preview` badge is present and the
version is `0.1.0-preview.0.410`. Since no test guarded any of this, `AboutTemplaterTests` now pins commit-hash
presence and pre-release-ness **by shape, never by literal** (both move every commit).

**Unpredicted:** MinVer also sets `AssemblyVersion` to `{Major}.0.0.0`, so `0.1.0.0` → `0.0.0.0`. Every reader was
traced before accepting it: the only one is `AboutTemplater.cs:70`'s `GetName().Version` fallback, unreachable
while an informational version exists — and MinVer always writes one. `AssemblyFileVersion` still tracks the real
version. Accepted and documented in the csproj rather than fought with a target.

#### R5(a) is fixed, and CLAUDE.md's worktree workaround is now obsolete

`.git` in this worktree is a **56-byte file** (exactly 16.1's measurement). Before the fix, an unset
`SPECSCRIBE_RENDERER_DIR` here walked past the worktree root to `C:\Dev\SpecScribe`, whose `web/.output` does
**not** exist — so the developer path failed outright, and in a checkout that *did* have one it would have
rendered from another checkout's artefact. After the fix, `generate` from this worktree with the env var unset
completes at **`errors=0` across 1553 routes** (probe 16). Project memory recording "set `SPECSCRIBE_RENDERER_DIR`
or the prerender silently skips" is now stale and has been updated.

#### R5(b) proved itself in the field within an hour of being written

The first foreign-repo probe returned `errors=1`, and the new `DescribeRouteFailure` printed the cause in-band:
*"The epics index IR entry declares no child pages…"* — **16.1 Finding #1 verbatim**, which 16.1 could only obtain
by booting the artefact by hand. That is a renderer defect already routed to **Story 23.3** and recorded as
**gating 16.7**; it is not a packaging fault, and it is not this story's to fix. Re-run with a corpus whose epics
index has children: `errors=0`. This is precisely the support burden Epic 16 would otherwise have shipped.

#### Gate discipline — NO baseline was regenerated

`check:ir-content` is red by `-2` rules (`.mini-donut`) and was **red before any edit**, proven by `git status`
showing zero source changes at the time. Causality established rather than assumed: `.mini-donut` is emitted only
for a story with *partially* complete tasks (`HtmlRenderAdapter.Epics.cs:427`), and this checkout's story data has
none — every story is 0-done or all-done. Data-dependent, no stylesheet involved, **identical before and after**.
Two earlier red readings were also environmental and were resolved by fixing the input, not the baseline: a fresh
worktree has no IR at all, and a generate **without `--deep-git`** produced the documented `+4/-187` deep-analytics
signature (CI documents `+4/-182`; 16.1 saw `+4/-185`). Following the gate's own suggested fix would have deleted
187 live rules behind a green gate.

#### NFR9 — exactly one gap closed, and it is this story's

ADR 0040 § 7's ledger: `npm ci` **already closed** by `0b1f561` (credited to 16.2, not here); **version-from-tag
CLOSED by this story**; `SOURCE_DATE_EPOCH` remains 16.4's (the csproj already honours it, no workflow sets it);
`Deterministic`/`ContinuousIntegrationBuild`/SourceLink remain deferred to 17.4's burndown; byte-identical Nuxt
rebuilds explicitly out of scope. **No broader reproducibility claim is made.**

#### Scope

**Nothing was published and no tag was pushed** (R11). The tag proof used a local tag, deleted seconds later;
`git tag -l` is empty. `.github/**`, `web/**` and `extension/**` are untouched — verified by
`git status --porcelain` on those three paths after every probe revert, not by recollection (16.1 § 7.1). Story
16.2 owns `.github/**` this sprint and there is no hunk-level overlap to attribute.

**No structural scope change**: no story added, removed or renumbered, so neither `epics.md` nor
`sprint-status.yaml` needed a structural edit — recorded explicitly so the absent `epics.md` diff reads as a
decision rather than an oversight. **No new ADR**: ADR 0040 decides every shape here, and F1 changes a mechanism
the ADR does not specify.

⚠️ **One drift corrected on entry:** `sprint-status.yaml` had `16-3-cli-packaging-and-publication: backlog` while
the story file read `ready-for-dev`. Create-story 16.3 (merged at `792b308`) landed the story file but its
sprint-status edit did not survive the merge. The story file was treated as authoritative and the key corrected.

**This work sat on top of:** `a2eee2a`/`c73ebcb` (Story 16.2 — CI gate, the `FileShare.Delete` flake fix whose
2978-test floor this inherited), `6120d2a` (23.2), `792b308` (create-story 16.3), and `0b1f561` (the lockfile
repair that made R1's `npm ci` precondition work).

#### 👤 Owner actions still outstanding (unchanged by this story, re-flagged because a package now exists)

1. **Ratify ADR 0040** — still `Proposed`. R10's reasoning for proceeding stands; the residual exposure is
   § Decision 5's *mechanism*, which is one `PackageReference` and three properties to swap.
2. **Decide whether the first real tag gets pushed.** Recommendation unchanged: not until 16.4 lands.
3. **Reserve `SpecScribe` on nuget.org and the five npm names** — the only item a third party can take.
4. **Confirm Trusted Publishing is visible on the nuget.org account** — before 16.4 starts, not during.

### Change Log

| date | change |
|---|---|
| 2026-08-07 | Story 16.3 implemented. MinVer replaces the `<Version>` literal; the renderer artefact now ships inside both the nupkg and the self-contained publish; two build-time guards make a renderer-less package impossible to produce; `--version`/`-v` work for the first time; `FindRepoRoot` recognises a git worktree; a non-200 from the renderer now carries the renderer's own error text. README corrected at four sites, `docs/Packaging.md` added. Status: ready-for-dev → in-progress → review. |

### File List

| file | change |
|---|---|
| `src/SpecScribe/SpecScribe.csproj` | MODIFIED — `<Version>` deleted; MinVer 7.0.0 + three properties; renderer `Content` item for both channels; `AssertRendererPacked` and `AssertRendererAvailableForPublish` targets. Preserved: every `EmbeddedResource`, `PackageReadmeFile`, `PackageLicenseExpression`, the `SOURCE_DATE_EPOCH` block, `InternalsVisibleTo`. |
| `src/SpecScribe/Program.cs` | MODIFIED — one `SetApplicationVersion` call. `UseStrictParsing`, `PropagateExceptions`, the example rules and the `CommandParseException` → menu fallback all preserved. |
| `src/SpecScribe/NuxtPrerender.cs` | MODIFIED — `FindRepoRoot` widened to `internal` and made worktree-aware via `IsRepoRoot`; `DescribeRouteFailure` + `ExtractRendererMessage` + `MaxRouteFailureDetail` + `WhitespaceRun` added and wired into the non-200 branch; server-log tail attached once per run; two stale Story-16.3 doc-comment claims corrected. Preserved: the override's hard-fail-on-miss, Node-before-artefact ordering, the full-`MainLandmark` anchor, polled readiness, `TryKill` in `finally`. |
| `tests/SpecScribe.Tests/NuxtPrerenderTests.cs` | MODIFIED — 10 tests added (3 × `FindRepoRoot`, 7 × `DescribeRouteFailure`) plus a `Canonical` helper; stale header note corrected without weakening the Node-free/artefact-free rule. |
| `tests/SpecScribe.Tests/AboutTemplaterTests.cs` | MODIFIED — 1 test added pinning the `+<sha>` suffix and the pre-release label by shape (R4's two silent regressions). |
| `README.md` | MODIFIED — four sites: the `<Version>`-bump instruction, the "does not yet carry its renderer" blockquote, the published CI recipe's hard-coded `--version` and `SPECSCRIBE_RENDERER_DIR` block, and that recipe's shallow `actions/checkout` (needs `fetch-depth: 0` under MinVer). Embedded consumer recipe only — **no `.github/**` file was touched.** |
| `docs/Packaging.md` | **NEW** — how a package is produced and verified, the two traps, and what is out of scope. The artifact 16.4 builds its pipeline from. |
| `_bmad-output/implementation-artifacts/16-3-cli-packaging-and-publication.md` | MODIFIED — this record. |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | MODIFIED — `16-3` key: `backlog` (drift) → `in-progress` → `review`. |
