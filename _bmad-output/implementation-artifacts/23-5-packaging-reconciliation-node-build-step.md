---
baseline_commit: 86b35c267241c15b05c64e3aaa3e13cce58198b2
---

# Story 23.5: Packaging Reconciliation — Where and When the Node/Nuxt Build Runs

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer responsible for SpecScribe's distribution,
I want the Node/Nuxt build step reconciled with the self-contained-binary distribution model — decided on
measured evidence, recorded in an ADR, and wired into a real pipeline,
so that Epic 16's packaging/release story survives the presentation-layer migration and Story 23.4 can
irreversibly retire the C# `HtmlRenderAdapter` without stranding the product.

## Acceptance Criteria

_ACs 1–2 are the epic's stated ACs (epics.md §Story 23.5, lines 4039–4055). ACs 3–9 are the concrete scope
this story is seeded with, derived from the 23.1 spike gate's re-scope directive (epics.md:3942–3950), the
corrected premises recorded on the `23-5-…` sprint-status key, and the code-verified findings in Dev Notes._

1. **Given** ADR 0005/0006's self-contained-binary distribution and Epic 16's release pipeline
   **When** the Node/Nuxt build step is introduced
   **Then** a **documented packaging strategy** resolves how and when the Node toolchain runs — build-time
   only vs. runtime dependency — naming one chosen strategy, the alternatives rejected, and the measured
   basis for each.

2. **Given** the npx channel (Story 16.8) and VS Code Marketplace packaging (Story 16.5/FR33)
   **When** packaging is reconciled
   **Then** both channels continue to function **without new runtime dependencies for end users**.
   ⚠️ **Read AC #3 before acting on this one** — as written it names two channels and there are three, and
   neither named channel exists yet.

3. **Given** ADR 0012's three-channel packaging formula ("self-contained binary, npx Story 16.8, and the
   VSIX Story 16.5" — epics.md:3355, ADR 0012 §Spike-validation item 5) and the corrected 23.1 premise that
   **npx and the extension host already run on Node by construction** (npm invokes npx; ADR 0005:30 — "the
   extension host runs in VS Code's Node.js")
   **When** the strategy is evaluated
   **Then** it is evaluated against **all three** channels, and the report states explicitly that the
   **standalone self-contained binary** (ADR 0005 §2, Story 16.3) is the only channel a Node runtime
   dependency genuinely breaks — and says what that channel does when Node is absent.
   **And** the report states plainly that **no Epic 16 channel is built yet** (every `16-*` key is `backlog`
   with no story file), so AC #2's "continue to function" is a **design constraint on unbuilt channels**, not
   a non-regression check against a live system.

4. **Given** that the single load-bearing assumption behind "build Nuxt once, ship the output, prerender
   per-project" is that **one prebuilt artefact can render many different projects** — and that this has
   never been tested
   **When** this story runs the experiment
   **Then** a **two-IR test** is executed and recorded: build `web/` **once**, then render **two different
   projects' IRs** from that same unmodified build by varying `SPECSCRIBE_IR_DIR`
   (`web/ir/adapter.ts:142-143`), and confirm both produce correct, fully-rendered, no-JS HTML.
   A **refutation is an equally valid outcome** and must be recorded as such — it eliminates the strategy
   rather than failing the story.

5. **Given** the two forces now in direct conflict in shipped and in-flight code (see Dev Notes → **The
   crux**) — the payload measurement that drove IR resolution to **build time, module scope**
   (23.2 AC #4: variant C = 1.00× vs. `useAsyncData` 1.36× vs. `<NuxtIsland>` 1.99×) and the packaging need
   for the IR to be read at **server runtime** so one build serves many projects
   **When** the strategy is chosen
   **Then** this conflict is **explicitly adjudicated and recorded** — not resolved implicitly inside an
   implementation choice — covering both couplings by name:
   (a) `nuxt.config.ts` executes `import { site } from './ir/adapter'` at **config-load time** to compute
   `nitro.prerender.routes`, baking one project's route table into the build; and
   (b) the render path calls `readFileSync` against `IR_DIR` at **module scope** in the server bundle
   (`web/ir/adapter.ts:188, 222, 339`), which is runtime-resolvable and is the property (a) does not have.

6. **Given** this decision changes a shared architectural contract, reconciles two ratified ADRs (0005's
   self-contained packaging, 0006's re-affirmation of it), and governs Stories 23.4, 16.1, 16.3, 16.5 and
   16.8
   **When** the strategy is decided
   **Then** an **ADR is authored and proposed** (next free number — 0017 at the time of seeding; verify
   before claiming it) recording the decision, the measured alternatives, the channel-by-channel
   consequences, and whether ADR 0006 §Decision (0006:214–219 — "its self-contained packaging … stand[s]")
   is re-affirmed, amended, or contradicted.
   Per CLAUDE.md this is **proposed without being asked**, and it must **not** be buried as a note in this
   story file or in `sprint-status.yaml` prose. Ratification is the owner's.
   ⚠️ This is a **different** ADR from the ADR 0005 **CSP** amendment, which Story 23.4 owns and which
   ADR 0012 §Decision 5 requires be "landed once, not twice." Do not land the CSP amendment here.

7. **Given** the emitted output currently references its assets by **absolute root path** — verified:
   `href="/_nuxt/PageShell.BFy9n7kb.css"`, `src="/_nuxt/2rxZ9LSr.js"` in `web/.output/public/index.html` —
   because `app.baseURL` is unset, while the SpecScribe portal is a **relative-path file tree routinely
   opened from `file://`** (ADR 0012 §Decision 1: "the generated portal must keep working offline and from
   `file://`"; EXPERIENCE.md:270 copies the output folder "to a USB drive for offline demo")
   **When** the packaging strategy is decided
   **Then** the emitted asset-path form is resolved so the packaged output **loads correctly from `file://`
   and from an arbitrary subdirectory**, demonstrated in a live browser on a real emitted page — or the
   limitation is recorded as an accepted, documented degradation with the owner's decision attached.
   Note the mitigating fact: Story 23.3 sets `'/**': { noScripts: true }`, so IR-backed routes ship **no**
   Nuxt runtime — the ESM-over-`file://` problem largely dissolves, but the **linked stylesheet** does not
   (`features.inlineStyles: false` forces a `<link>`, deliberately).

8. **Given** `web/` today is built by nothing — no CI step, no MSBuild target, no npm lifecycle hook (see
   Dev Notes → **Verified state**) — and the owner already decided on 2026-07-26 (recorded on the Story 23.2
   review, `23-2-…md:86`) to "analyze it now — add a **Node CI step** + `sonar.javascript.lcov.reportPaths`"
   **When** the chosen strategy is implemented
   **Then** the pipeline it names is **actually wired**, not only described: the drift gates that exist and
   are enforced by nothing (`npm run check:tokens`, and 23.3's `check:ir-content` / `check:links` /
   `measure:parity`) run somewhere automatic, and `web/**` is given a deliberate posture in
   `.github/workflows/build-test-analyze.yml` (its Sonar exclusion list at :136 covers `spike/**`,
   `tools/**`, `extension/node_modules/**` — **`web/**` is absent**, so the next scan pulls ~1,800 lines of
   untested first-party `.vue`/`.mjs`/`.ts` in at 0% coverage and reds the gate Story 25.1 just turned
   green).
   ⚠️ **Confirm the scope split with the owner before starting** — see Dev Notes → **Questions for the
   owner**, Q3. If the CI work is carved out, say so explicitly rather than silently dropping it.

9. **Given** this story decides packaging and **Story 23.4 owns retiring the C# renderer**
   **When** this story completes
   **Then** the C# `HtmlRenderAdapter` is **fully intact**, no surface is migrated, and
   `GoldenContentFingerprint` **has not moved by this story's changes** — a moved fingerprint means this
   story leaked into the renderer and must be reverted, not re-blessed.
   ⚠️ Read the concurrency caveat in Dev Notes before interpreting a moved hash: it moves under concurrent
   sessions routinely, and Story 23.2 recorded a hash that provably sat on another session's uncommitted
   work.

## Tasks / Subtasks

- [x] **Task 1 — Re-establish the factual baseline** (AC: #1, #3)
  - [x] Re-read `_bmad-output/implementation-artifacts/23-1-spike-report.md` — **it exists**; a research
        pass during story creation wrongly reported it missing. Axis 4 and Findings 7/9 are the measured
        inputs. Treat the **cold** figures as the user-facing ones (Node cold path ~130 s: 112.5 s first-ever
        `nuxt generate` + 18.4 s `npm ci`), not the headlined warm +57 % / 37.1 s.
  - [x] Confirm the current state of `web/`: `nuxt 3.21.9`, `nitropack 2.13.4`, `vite 8.1.5`, `vue 3.5.40`,
        735 lockfile packages, **all three declared deps are `devDependencies` under `private: true`** — so
        `npm ci --omit=dev` installs nothing and a "production-only" install is not currently possible.
  - [x] ⚠️ **Nuxt 3 reaches end of life on 2026-07-31 — five days after this story was seeded.** Decide and
        record whether 23.5 absorbs the Nuxt 3 → 4 upgrade or explicitly accepts running on an unsupported
        line. Do not leave this undecided. (Current stable at seeding: Nuxt 4.5.0, released 2026-07-18;
        `engines.node` `^22.19.0 || ^24.11.0 || >=26.0.0`.)
  - [x] Record the Node version actually used. The only Node version on record anywhere in the repo is
        **24.11.1** in `web/CONVENTIONS.md:94`, and it is pinned nowhere — there is no `.nvmrc`, no
        `engines` field, no `global.json`.

- [x] **Task 2 — Run the two-IR experiment** (AC: #4) — _the load-bearing one; do this before writing any
      strategy prose_
  - [x] Produce two distinct IRs: this repo's own
        (`dotnet run --project src/SpecScribe -- generate --spa` into `SpecScribeOutput/` — the default;
        **never** `--output docs/live`), and a second from a different source tree.
  - [x] Build `web/` **once** (`npm run build`, not `generate`). Measure and record `.output/` size and file
        count. The story-creation research measured **2.83 MB / 201 files** on the pre-23.3 build
        (`.output/server` 2.04 MB incl. a self-contained `server/node_modules` of 1.56 MB) against a
        **174.8 MB / 14,407-file** `node_modules`. Re-measure on the post-23.3 build — 23.3 adds
        `ir-content.css` and ~1.4 MB of synced runtime assets (`specscribe.js` 154 KB +
        `plotly-hierarchy.min.js` 1.22 MB).
  - [x] Copy **only** `.output/` to an isolated directory — no source, no `node_modules`. Boot
        `node .output/server/index.mjs` with `SPECSCRIBE_IR_DIR` pointed at IR **A**, render routes, then
        restart pointed at IR **B** and render again. Confirm both emit correct, fully-rendered HTML.
  - [x] ⚠️ **The route table is the known gap, and it is expected to fail — that is the point.**
        `nuxt.config.ts` computes `nitro.prerender.routes` from `import { site } from './ir/adapter'` at
        **config-load time**, so a prebuilt artefact carries project A's route list. The hypothesis under
        test is that this **does not matter** when SpecScribe drives the prerender itself — it already knows
        every route (it emitted the manifest) and can issue one request per route. Test *that* shape (drive
        the routes externally), not `nuxt generate`.
  - [x] Record per-route render latency and total wall-clock for a full-site pass, against the 23.1 spike's
        `nuxt generate` baselines (warm 37.1 s / cold ~130 s for ~918 routes). Report honestly whichever way
        it lands.
  - [x] Confirm no tracked file is modified by the experiment — `.output/`, `.nuxt/`, `dist/` are all
        gitignored (`web/.gitignore:3-7`).

- [x] **Task 3 — Adjudicate the build-time ↔ runtime conflict** (AC: #5)
  - [x] Read `web/ir/adapter.ts` and `web/nuxt.config.ts` as they stand **at the time you run** — Story 23.3
        is being implemented concurrently and these files are moving.
  - [x] Establish, by reading the built bundle (not by assumption), whether `readFileSync(IR_DIR, …)`
        survives into `.output/server` as a runtime read or is inlined at build time. `IR_DIR` resolves from
        `process.env.SPECSCRIBE_IR_DIR` at module scope (`adapter.ts:142-143`), and the reads at `:188`,
        `:222`, `:339` take computed paths — so inlining should be impossible, but **verify it, don't infer
        it**.
  - [x] Write the adjudication: does the IR resolve at build time (payload-optimal, one build per project)
        or at server runtime (one build, many projects)? State which constraint yields and why.
  - [x] If the answer changes 23.3's shape, **raise it rather than editing 23.3's implementation** — 23.3 is
        in flight and mid-implementation.

- [x] **Task 4 — Evaluate the strategies against all three channels** (AC: #1, #2, #3)
  - [x] Build the comparison table over the candidates in Dev Notes → **Candidate strategies**, with real
        measurements from Task 2 replacing the seeded estimates wherever they differ.
  - [x] For each of the three channels — **standalone self-contained binary** (16.3), **npx** (16.8),
        **VSIX/Marketplace** (16.5/FR33) — state what the chosen strategy costs and what it forfeits.
  - [x] Answer the standalone-binary question explicitly: when Node is absent, does it (a) require Node,
        (b) bundle a JS runtime, or (c) degrade to something else? Name the choice.
  - [x] Record the negative result on embedding a JS engine in .NET rather than re-deriving it: Vite 8 /
        Rolldown and Oxc ship as platform-native `.node` bindings requiring `process.dlopen`, so **no pure-JS
        engine (Jint, ClearScript/V8) can run a Nuxt *build*** — the door is closed, not narrow.
        `Microsoft.JavaScript.NodeApi`/LibNode is the only architectural fit and is a preview package hosting
        an experimental subsystem pinned to Node 20.18, **EOL since 2026-04-30**.

- [x] **Task 5 — Resolve the asset-path / `file://` form** (AC: #7)
  - [x] Reproduce the defect on a real emitted page before fixing it — confirm the absolute `/_nuxt/…` refs
        and confirm what actually breaks when the page is opened from `file://` and from a subdirectory.
  - [x] Evaluate `app.baseURL: './'` (and `app.cdnURL` if needed) against the parity contract: 23.3 AC #1
        compares the `<main>` region and AC #5 pins the head projection field-by-field against
        `PathUtil.RenderHeadOpen`. A baseURL change moves head/asset markup — check it does not break either.
  - [x] Verify in a live browser, per CLAUDE.md: the suite structurally cannot see this class of defect.

- [x] **Task 6 — Wire the pipeline** (AC: #8) — _confirm scope with the owner first (Q3)_
  - [x] Add the Node step to `.github/workflows/build-test-analyze.yml` (`actions/setup-node`, `npm ci` in
        `web/`), running the drift gates. Today CI has **no Node step at all** and the workflow deliberately
        carries **no `paths:` filter**, so it already runs on `web/`-only pushes and does nothing with them.
  - [x] Give `web/**` a deliberate Sonar posture — exclusion or `sonar.javascript.lcov.reportPaths` — and
        state which and why. Do **not** let the next scan decide by default.
  - [x] Add the npm lifecycle hook the gates lack: there is no `prebuild`/`pregenerate`, so
        `npm run check:tokens` does not gate a build even for a developer.
  - [x] Do **not** create a second build/test workflow. Story 16.2 as amended (epics.md:2720–2729) is
        explicit: "two workflows that both build and test is the exact drift class this project has
        repeatedly paid for."

- [x] **Task 7 — Author the ADR** (AC: #6)
  - [x] Confirm the next free ADR number by listing `docs/adrs/` (0016 is the highest at seeding; 0015 and
        0016 are both **Proposed**).
  - [x] Author it in the house form — Status/Context/Decision/Consequences/Ratified-decisions, with the
        measured basis inline, following ADR 0012/0013's shape.
  - [x] Update `docs/adrs/README.md` in the same change.
  - [x] State the relationship to ADR 0006 §Decision (0006:214–219) explicitly. If the decision contradicts
        "its self-contained packaging … stand[s]", say so in the ADR rather than letting it drift.
  - [x] Leave it **Proposed**. Ratification is the owner's, not the dev agent's (ADR 0016 §Ratified
        decisions is the precedent).

- [x] **Task 8 — Record the strategy and unblock 23.4** (AC: #1, #9)
  - [x] Write the durable packaging-strategy report. Mirror the 22.1 / 23.1 spike-report structure — measured
        evidence, findings, verdict — since this story's primary deliverable is a decision, not code.
  - [x] Update `epics.md` **and** `sprint-status.yaml` in the **same change** (CLAUDE.md: a structural change
        recorded in only one artifact is a drift bug). Specifically: 23.4's "Blocked until 23.5 lands" note
        resolves, and its `backlog` key becomes actionable.
  - [x] Confirm the scope guard held: `git` confirms `src/SpecScribe/**` and `tests/**` untouched by this
        story's own work, `SpecScribe.slnx` still holds two projects, `HtmlRenderAdapter` intact.

## Dev Notes

### The decision this story exists to make

Epic 23's own gate (23.1 spike report §Gate; epics.md:3942–3950) states the tension as a binary:

> Either the shipped artefact becomes a client-rendered SPA over the IR (which forfeits NFR6 — the thing
> Axis 1 just proved), or `specscribe generate` invokes Node at run time (which forfeits self-containment).
> **This spike did not solve it, and it is the single biggest open question in Epic 23.**

**That binary is false, and this story's main job is to test the third option.** The toolchain and the
runtime are separable: `nuxt generate` needs 174.8 MB of `node_modules` plus a Vite build, but a **prebuilt
`.output/`** is self-contained (Nitro v2 traces dependencies into `.output/server/node_modules` with
`vercel/nft`) and measured at **2.83 MB**. If a prebuilt server can render arbitrary IRs, the cost collapses
from "ship a build toolchain" to "require a Node runtime" — a different and much smaller problem, and one
that **two of the three channels already pay** (npx runs on npm; the extension host *is* Node).

Whether that works is AC #4's experiment. It has never been run.

### Verified state of the world (code-cited — do not re-derive)

**Nothing builds `web/`. Anywhere.**
- `.github/workflows/build-test-analyze.yml` — Checkout → setup-dotnet 10.0.x → setup-java 21 → Sonar begin
  → `dotnet build SpecScribe.slnx` → `dotnet test` → Sonar end, plus a non-gating ubuntu `portability-probe`.
  **No `actions/setup-node`, no `npm ci`, no `npm run`.**
- Neither `.csproj` contains any `<Target>` or `<Exec>`; a repo-wide search for MSBuild targets invoking
  npm/node returns nothing. `SpecScribe.slnx` holds exactly two projects.
- The only npm lifecycle hook in `web/package.json` is `postinstall: nuxt prepare`. No `prebuild`, no
  `pregenerate`.
- `.claude/launch.json`'s `web` entries run `python -m http.server` against an **already-built** output.

**There is no build configuration to hook into.** No `Directory.Build.props`, no `global.json`, no `.nvmrc`,
no root `package.json`, no `.editorconfig`, and **no `.gitattributes`** anywhere in the repo.

**No `PublishSingleFile`/`SelfContained`/`RuntimeIdentifier` exists in either `.csproj`.** The
self-contained single-file binary is **design intent** (ADR 0005:80–86, ADR 0006:60–65), proven once in the
Story 6.6 spike, and implemented nowhere. `src/SpecScribe/SpecScribe.csproj` sets `PackAsTool`,
`ToolCommandName specscribe`, `PackageId SpecScribe`, `Version 0.1.0-preview` (hand-edited literal).

**The precedent for build-time-only Node is `tools/prism-vendor/` — and it is weaker than it looks.**
`build.js` is run **by hand, out of band**; its outputs (`prism.js`, `prism.css`) are **committed** and reach
the C# build only as `<EmbeddedResource>` entries. `tools/plotly-vendor/` is the same shape. Both produce a
**project-independent** artefact. A Nuxt prerender is **project-dependent**, which is exactly why the
precedent does not transfer — say so rather than citing it as a solved pattern.

**Epic 16 is entirely unbuilt.** Every `16-*` key in `sprint-status.yaml` is `backlog`; there are no `16-*.md`
story files. The whole release story today is manual: `dotnet pack` per `README.md:33`, and a VSIX from
`npm run package` in `extension/` with `bin/` populated by a local VS Code task doing a **framework-dependent
Debug publish**. `extension/README.md:174-178` states it outright: "no CI wiring … nothing here publishes."

### The crux — build-time vs. runtime IR resolution

Two forces are in direct conflict in shipped and in-flight code. **Both are correct in their own frame.**

| | wants | why | evidence |
|---|---|---|---|
| **Payload** | IR resolved at **build time, module scope** | measured 1.00× vs. `useAsyncData` 1.36× and `<NuxtIsland>` 1.99× | 23.2 AC #4; `CONVENTIONS.md:96-100` |
| **Packaging** | IR read at **server runtime** | one prebuilt artefact must serve many projects | this story, AC #4 |

The good news, verified by reading the in-flight 23.3 code: **the render path is already runtime-resolvable.**

```
web/ir/adapter.ts:142-143
  export const IR_DIR = resolve(
    process.env.SPECSCRIBE_IR_DIR ?? fileURLToPath(new URL('../../SpecScribeOutput', import.meta.url)),
  )
```

with `readFileSync` at `:188` (manifest), `:222` (lazy per-chunk), `:339` (entry page). Paths are computed,
so Rollup cannot inline them. "Build time" here means *SSR render time inside the Nitro server* — which in a
prebuilt `.output/` **is** runtime.

The genuine build-time coupling is elsewhere, and narrower:

```
web/nuxt.config.ts
  import { site } from './ir/adapter'      // executes at CONFIG LOAD
  const irRoutes = site.paths.map((p) => `/${p}`)
  …  nitro: { prerender: { crawlLinks: false, routes: ['/', ...irRoutes, …] } }
```

One project's route table is baked into the build. **The hypothesis AC #4 tests is that this is irrelevant**
when SpecScribe drives the prerender itself — it emitted the manifest, so it already knows every route and
can request them one at a time. Test the externally-driven shape; do not test `nuxt generate`.

**One more property that helps, and that you should not accidentally undo.** Story 23.3 sets
`routeRules: { '/**': { noScripts: true } }` — IR-backed routes ship **no Nuxt runtime and no hydration
payload at all**, and `#ir` is aliased to a throwing stub for the client build. The delivered page is the
prerendered HTML plus the portal's own vanilla `specscribe.js`. That makes the no-JS baseline structural
rather than measured, and it removes the ESM-over-`file://` hazard for IR routes.

### Candidate strategies (seeded estimates — replace with Task 2's measurements)

| | strategy | added bytes | cold cost | end-user runtime dep | forfeits |
|---|---|---|---|---|---|
| **A** | Prebuilt `.output/` shipped; SpecScribe drives a per-project prerender over it | **~2.8 MB** | boot ~1–5 s, then **~4 ms/route** | **Node runtime** (22/24/26) | nothing architectural; users without Node cannot generate |
| **B** | A + a bundled JS runtime for the standalone binary only | **+50–100 MB/RID** (→ ~130–180 MB total) | + a few hundred ms | none | binary size; AV/notarization exposure; a second runtime's CVE surface |
| **C** | Ship `node_modules`, run `nuxt generate` on the user's machine | **+175 MB** | **~130 s cold** | Node + full toolchain | self-containment, and the size premise of the product |
| **D** | Client-rendered SPA over the IR | +~1 MB | ~0 | none | **NFR-5 / ADR 0013's text twin; requires reopening ratified ADR 0009**; breaks `file://` |

**A is the one to test first.** **D is already ruled out** — ADR 0009 ratified Axis 1 = Option B
(universal/SSR) and explicitly rejected Option A (client-only); choosing it now means reopening a ratified
ADR, not making an implementation call. ADR 0006:223–225 is blunter still: "A SPA that renders the
information from JSON in the client violates that policy on its face."

**For B, if it is needed:** `@yao-pkg/pkg` (active fork; `vercel/pkg` archived 2024-01-13) or Bun
`--compile` (proven Nitro path, best cross-compilation). Node's own SEA is **still `Stability: 1.1 —
experimental`**, and ESM support landed in Node 25 — which is already EOL — so ESM SEA means Node 26
(Current), not 24 LTS. `nexe`'s last stable release was 2018. macOS: all require an explicit post-package
re-sign and **none document notarization**. Windows: a .NET single-file host that extracts and launches an
embedded JS runtime resembles the textbook dropper heuristic — budget for an EV certificate.

**Industry precedent is uniform and worth stating in the report:** nobody ships a JS build toolchain to run
at user-run time. The closest structural analogue is **ReportGenerator (.NET)** — C# writes the real
`<table>` markup, a prebuilt Angular bundle (committed to git, embedded as a resource, **no npm step in the
`.csproj`**) wraps it, and the report works substantially with JS off. The cautionary pole is **Allure**:
prebuilt bundle, but 100% client-rendered, no `<noscript>`, blank from `file://` — which is why `allure open`
has to exist.

### Traps

1. **Do not treat the warm build number as the user-facing one.** The 23.1 headline (+57 %, 37.1 s) is warm.
   Cold is 112.5 s plus 18.4 s `npm ci` ≈ **130 s**. If Nuxt ever runs on a user's machine, cold is what they
   experience.
2. **`web/` cannot be relocated away from `src/` without breaking the token bridge.**
   `web/scripts/tokens-lib.mjs:15-17` resolves `../../src/SpecScribe/assets/specscribe.css` by relative path.
   Any packaging that ships `web/` as a separate npm package, submodule, or build container breaks both
   extraction and the drift gate.
3. **All three of `web/`'s dependencies are `devDependencies` under `private: true`.** There is no
   production-only install; `npm ci --omit=dev` installs nothing. If the strategy needs a slim install, that
   is a change to make deliberately.
4. **The 1.99× island figure is directionally right but the harness is fragile.**
   `measure-payload.mjs:39` charges the **entire** shared `__nuxt_island/` directory to variant B, so adding
   any `.server.vue` anywhere moves the published ratio; and every size lookup ends `?? 0`, so a missing route
   prints `0.00x`, which reads as "this shape is free" — inverting the conclusion. Do not re-cite the number
   without re-checking the harness.
5. **After 23.3, `nuxt generate` is step 2 of a two-step pipeline.** `nuxt.config.ts` reads
   `SpecScribeOutput/spa/manifest.json` at config time, and `sync-runtime-assets.mjs` copies ~1.4 MB of
   C#-owned assets into `web/public/assets/`. The Nuxt build is **ordered after** a C# generate run. Any
   packaging strategy inherits that ordering.
6. **`crawlLinks: false` is load-bearing, not a preference.** Nitro's crawler walks every `<a href>` in
   rendered HTML — including links inside `v-html`'d IR content — and aborts the build on the first 404
   (23.1 finding 8). Do not "fix" it.
7. **Routes carry `.html` extensions verbatim** (`/index.html`, `/epics/epic-3.html`) so the IR's own
   relative hrefs resolve unchanged and no href is ever rewritten. Everything funnels through one
   `pages/[...path].vue` catch-all because Nuxt file-based routing cannot express `.html` routes. The
   packaged output must preserve this path space.
8. **Reproducibility (NFR9) is only half-built.** `SpecScribe.csproj:26-42` honours `SOURCE_DATE_EPOCH`, but
   **no workflow ever sets it**, there is no `<Deterministic>`/`ContinuousIntegrationBuild`/SourceLink, and
   `<Version>` is a hand-edited literal. Adding a Nuxt build to the release path widens the reproducibility
   surface — say what the strategy does about it rather than inheriting the gap silently.
9. **`web/` is already inside SpecScribe's own dogfood Code Map.** `SiteGenerator.EnumerateCodeFiles`
   (`SiteGenerator.cs:4496`) prefers `git ls-files` with no extension filter, so all tracked `web/**` files —
   including the 11,132-line `package-lock.json` — generate code pages today.
10. **NFR citation hazard.** Epic 23 cites "NFR6" throughout meaning the **PRD's NFR-5** (progressive
    enhancement). `epics.md`'s own NFR6 is "cross-surface accessibility semantics" — a different requirement.
    The collision is recorded and **unresolved** (`epics.md:123-134`). If you cite it, cite it as
    "the PRD's NFR-5, cited as NFR6 throughout Epic 23 per the recorded collision."

### Concurrency — this is a live tree

Per CLAUDE.md, assume another agent is editing these files right now. At seeding, `git status` showed:

- **Story 23.3 mid-implementation**: `web/ir/adapter.ts` and `web/ir/adapter.client.ts` untracked;
  `web/nuxt.config.ts` and `web/components/PageShell.vue` modified. **Every `web/` fact in these notes may
  have moved.** Re-read before relying on it.
- **Epic 18 in flight**: `src/SpecScribe/{AdapterDiagnostic,BmadArtifactAdapter,Commands,DiagnosticsTemplater,HowToReadTemplater,ModuleContext,SiteGenerator}.cs`,
  `docs/adrs/0015-*`, `docs/adrs/README.md` all modified. **If you author an ADR, expect `README.md` to be
  contended.**
- Story 23.2 is `review` with **19 open findings**, three carrying owner decisions — one of which
  (tokenizing four hard-coded stage backgrounds in `specscribe.css:3042-3046`) **will move the golden
  fingerprint** independently of anything you do.

Consequences: **verify after every edit** (grep for the symbol you added; a `Charts.cs` edit has silently
vanished this way before, and a zero-grep can also be a transient mid-write read — confirm with
`git diff HEAD` before re-applying). **Never `git reset --hard`, `git checkout --`, or `git clean`.** Expect
the golden fingerprint to move under you; confirm any hash across two repeated runs and say whose changes it
sat on top of. Expect the suite to show **one rotating contention flake per full run** (file-write
contention; all pass in isolation) — 23.2 recorded 2404 passed / 3 skipped on that basis.

### Questions for the owner

Saved from analysis, per the workflow. These change the shape of the work; the story is written so Tasks 1–5
and 7 proceed regardless.

- **Q1 — Nuxt 3 EOL is 2026-07-31, five days out.** Absorb the Nuxt 3 → 4 upgrade into 23.5 (it is a
  dependency/packaging decision and 23.5 is the packaging story), defer it to its own story, or explicitly
  accept an unsupported line? Deferring is defensible; leaving it unstated is not.
- **Q2 — the standalone-binary fallback.** If the two-IR test succeeds and strategy A is chosen, what does
  the **standalone self-contained binary** do when Node is absent — require Node, bundle a JS runtime
  (+50–100 MB/RID), or degrade to the C# renderer? The third only exists until 23.4 retires it, which makes
  it a *sequencing* answer with a shelf life.
- **Q3 — CI scope (AC #8).** The 2026-07-26 owner decision on the 23.2 review ("add a Node CI step +
  `sonar.javascript.lcov.reportPaths`", plus authoring a `web/` test suite) has no owning story. Does 23.5
  take the Node CI step (it decides when Node runs, so it is the natural home), with the `web/` test suite
  carved out separately? There are currently **zero tests under `web/`**.
- **Q4 — ADR count.** Confirming one ADR here (packaging), separate from 23.4's ADR 0005 **CSP** amendment
  which ADR 0012 §Decision 5 requires be landed once. If the packaging decision also touches the CSP posture,
  flag it rather than merging the two.

### References

- [Story 23.1 spike report](23-1-spike-report.md) — Axis 4 (Node in the pipeline), findings 7 and 9, and the
  §Gate table that re-scoped and resequenced this story. **It exists**, despite one story-creation research
  pass reporting otherwise.
- [Story 23.2 — component library + token bridge](23-2-component-library-and-design-token-bridge.md) — the
  payload measurement table (AC #4) and the 19 open review findings, incl. `:86` (the CI owner decision),
  `:90` (the drift gate nothing runs) and `:102` (the fragile payload harness).
- [Story 23.3 — baseline surfaces](23-3-migrate-baseline-surfaces-dashboard-epics.md) — **in flight**; its
  six owner decisions, its `SpecScribeOutput/spa/` build-time input, and its scope guard deferring packaging
  here.
- [epics.md §Epic 23](../planning-artifacts/epics.md) — Story 23.5 at :4039–4055; the execution-order note at
  :3942–3950; Epic 16 at :2687–2895; the NFR collision at :123–134.
- [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) — §Platform constraint (:28–32,
  the extension host is Node); §Decision 2 (:71–86, the self-contained binary and its ~73 MB/RID cost).
- [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) — §Decision (:186–219, the
  re-affirmation this story must reconcile with); axis D / npx (:60–65, :202–205); the NFR6 ruling
  (:221–234).
- [ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) — :66 ("the self-contained-binary
  story (ADR 0005/0006) must be reconciled with a Node build step") and :74 (this reconciliation named as a
  spike-owned unknown). ⚠️ Its §Charts clause (:45–47) is **stale** — amended by ADR 0013 §5 with no marker
  in 0009's body.
- [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) —
  §Decision 1 (offline / `file://` requirement); §Decision 5 (the ADR 0005 CSP amendment **23.4** owns);
  §Spike-validation 5 (the three-channel packaging formula).
- [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) — the amended NFR-5 wording and the
  server-rendered text-twin contract any packaged output must still satisfy.
- [CLAUDE.md](../../CLAUDE.md) — concurrent-work rules, the ADR-proposal trigger, and the requirement to
  verify visual/layout work in a live browser.

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Claude Code, `bmad-dev-story`) — 2026-07-27.

### Debug Log References

- Reproducible experiment: `web/scripts/experiment-two-ir.mjs` → `web/measurements/two-ir.{txt,json}`
- Parity / links: `web/measurements/parity.{txt,json}`, `web/measurements/links.{txt,json}`
- Durable report: [`23-5-packaging-strategy-report.md`](23-5-packaging-strategy-report.md)
- Decision: [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) — **Proposed**

### Completion Notes List

**The verdict (AC #1, #4).** The 23.1 gate's binary — client-rendered SPA *or* Node at run time — is
**false**. It conflates the toolchain that *builds* the projection layer with the runtime that *renders*
with it. Measured: build toolchain **201.9 MB / 16,791 files / 14 native `.node` bindings** requiring
`process.dlopen`; shipped artefact **3.78 MB / 185 files / zero native bindings**. Net new shipped bytes
**~2.40 MB** (1.46 MB of the artefact is plotly/prism/`specscribe.js` the C# portal already ships).

**The two-IR experiment CONFIRMED the hypothesis.** One artefact, built with **no IR present at all**,
isolated to a directory containing only `.output/`, rendered **1,056/1,056** routes of this repo and
**32/33** of a genuinely different project (CORA) via `SPECSCRIBE_IR_DIR`, at ~4 ms/route — a full pass in
**6.3 s** against `nuxt generate`'s 25–30 s and the spike's ~130 s cold. The harness is **committed**,
because 23.1's "every number is reproducible" claim was found false at review.

**Three false results are recorded in the report**, two of them the harness's own fault (substring matching
for hydration markers on a portal that renders its own source; comparing against the golden static page
rather than the IR). The third was real and structural: an artefact carrying prerendered pages returned
**project A's dashboard for project B with HTTP 200**, because Nitro serves `public/` *ahead of* the SSR
route. Fixed structurally, not by instruction.

**AC #5 adjudication: the two forces were never in conflict.** "Build time, module scope" means SSR render
time *inside Nitro*, which in a prebuilt `.output/` **is** runtime — so 23.2's 1.00× payload measurement is
preserved, not traded. Verified by **reading the built bundle**, not inferred: `process.env.SPECSCRIBE_IR_DIR`
survives verbatim with all three `readFileSync` sites intact. Only the **route table** was a genuine
build-time coupling, and `SPECSCRIBE_PACKAGE_BUILD=1` removes it.

**AC #2/#3 correction.** AC #2 names two channels; ADR 0012's formula has three. npx and the extension host
both run on Node **by construction**, so the standalone binary is the only channel a Node dependency breaks
— and it takes a **documented prerequisite** (owner decision). No Epic 16 channel is built yet, so
"continue to function" is a design constraint on unbuilt channels, not a non-regression check.

**Owner decisions taken during the story (2026-07-27):** (a) absorb the Nuxt 3 → 4 upgrade here rather than
defer it; (b) full CI scope including authoring a `web/` test suite; (c) the standalone binary requires Node
as a documented prerequisite rather than bundling a runtime.

**Nuxt 3.21.9 → 4.5.1 held 23.3's contract exactly**: 189/189 `<main>` byte-identical, 189/189 verbatim,
0 link regressions — identical to the Nuxt 3 baseline captured immediately before the upgrade. Head
projection survived unhead v2 intact. `npm audit` reports 11 highs, all one `brace-expansion` root cause,
all build-toolchain `devDependencies` that this ADR's decision never distributes.

**AC #7.** Defect reproduced first (`href="/_nuxt/…"`, root-absolute). `app.baseURL: './'` was **evaluated
and rejected** — `baseURL` is one global string but the correct prefix is per-page-depth. Fixed with a
Nitro `render:html` depth-aware rewrite and **verified in a live browser from `file://`** at depth 0 and
depth 3: all stylesheets load, `body` computes to the portal parchment, zero absolute `/_nuxt/` remain.

**AC #8 — two of the three drift gates were RED at `HEAD`** when first run (`check:ir-content`,
`check:assets`), drifted by a concurrent story in `40c7ee9` and caught by nothing. That observation *is* the
argument for AC #8. Also **fixed a gate that could never stay green**: `ir-content.manifest.json` carried
whole-corpus statistics that changed on any docs commit; removed from the committed artifact and verified
green across two different IR generates.

⚠️ **The story's Sonar premise was stale.** It records `web/**` as absent from the exclusion list; Story
25.2 (decision 1b) had since added `sonar.coverage.exclusions="web/**"`. Narrowed rather than removed, and
`web/node_modules/**` — absent, and about to matter now that CI runs `npm ci` there — added to
`sonar.exclusions`. `web/` coverage is **51.19% / 53.40%** and is **expected to show `new_coverage` red**
(non-blocking); the fix is component tests, not re-widening the exclusion.

**Raised, not patched.** `DashboardSurface.vue` hard-throws on any project whose dashboard carries no
Hierarchy Explorer (CORA: zero occurrences). A real project-independence defect, and a deliberate ADR 0012
§Decision 2 contract assertion — raised to Story 23.3 per this story's own scope instruction.

**AC #9 scope guard held.** Zero C# touched; `HtmlRenderAdapter` intact; `SpecScribe.slnx` still 2 projects;
full suite **2,538 passed / 0 failed / 3 skipped**, `GoldenContentFingerprint` **green**. The `src/` and
`tests/` modifications visible in `git status` are **concurrent Epic 18 work**, not this story's — scope was
verified by File List, never by a diff range.

**Concurrency.** `SpecScribeOutput/spa/` was wiped **twice** mid-experiment by a concurrent session running
`generate` without `--spa`; the experiment was moved onto scratch IRs so a concurrent regeneration could not
invalidate a measurement in flight. `web/ir/adapter.ts` and `web/nuxt.config.ts` had both moved since
seeding — `IR_DIR` now resolves from `process.cwd()`, not `import.meta.url`.

### File List

**Modified**

- `.github/workflows/build-test-analyze.yml` — setup-node, npm ci, IR generate, drift gates, Vitest; narrowed `sonar.coverage.exclusions`; added `sonar.javascript.lcov.reportPaths`; added `web/node_modules/**` et al. to `sonar.exclusions`
- `web/package.json` — Nuxt 4, Vitest, `engines`, `build:package`, `test`, `test:coverage`, `experiment:two-ir`, `prebuild`/`pregenerate`
- `web/package-lock.json`
- `web/nuxt.config.ts` — `PACKAGE_BUILD` empties the prerender route table
- `web/ir/adapter.ts` — `PACKAGE_BUILD` + `EMPTY_MANIFEST`; package-build hint in the not-found error
- `web/scripts/ir-content-build.mjs` — whole-corpus statistics removed from the committed manifest
- `web/assets/ir-content.css`, `web/assets/ir-content.manifest.json` — regenerated (resolves pre-existing drift)
- `web/.gitignore` — ignore `coverage/`
- `web/measurements/parity.{txt,json}`, `web/measurements/links.{txt,json}` — regenerated
- `_bmad-output/planning-artifacts/epics.md` — 23.4 unblocked; 23.5 outcome recorded
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — 23.5 → review, 23.4 → ready-for-dev
- `docs/adrs/README.md` — ADR 0022 index entry

**Added**

- `docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md`
- `_bmad-output/implementation-artifacts/23-5-packaging-strategy-report.md`
- `web/scripts/experiment-two-ir.mjs` — the AC #4 harness
- `web/scripts/build-package.mjs` — `npm run build:package`
- `web/server/plugins/relative-asset-urls.ts` — page-relative `/_nuxt/` rewrite
- `web/server/utils/relative-prefix.ts` — the depth rule, extracted to be testable
- `web/vitest.config.ts`
- `web/test/relative-prefix.test.ts`, `web/test/region-split.test.ts`, `web/test/harness-lib.test.mjs`, `web/test/ir-content-lib.test.mjs`, `web/test/tokens-lib.test.mjs`
- `web/.nvmrc` — 24.11.1
- `web/measurements/two-ir.{txt,json}`

**Not touched** (AC #9): every file under `src/` and `tests/`.

### Change Log

| date | change |
|---|---|
| 2026-07-27 | Story implemented. ADR 0022 proposed — Node is a build-time toolchain and a generate-time runtime. Two-IR experiment CONFIRMED (1,056/1,056 + 32/33 from one artefact built with no IR). Nuxt 3 → 4 absorbed, 23.3 parity held. `file://` asset paths fixed and browser-verified. `web/` wired into CI with 80 Vitest tests. Status → review; Story 23.4 unblocked. |
