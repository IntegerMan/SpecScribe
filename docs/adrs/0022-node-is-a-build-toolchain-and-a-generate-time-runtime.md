# ADR 0022 — Node Is a Build-Time Toolchain and a Generate-Time Runtime, Never a Shipped Toolchain

- **Status:** Proposed — 2026-07-27
- **Authored by:** Story 23.5 (packaging reconciliation)
- **Amends:** ADR 0006 §Decision (the "self-contained packaging … stand[s]" clause)
- **Answers:** ADR 0009 §:66 and §:74, which named this reconciliation as a spike-owned unknown
- **Governs:** Stories 23.4, 16.1, 16.3, 16.5, 16.8

## Context

ADR 0009 chose Vue + Nuxt (universal/SSR) as the projection layer over the ADR 0008/0016 IR, and left one
question explicitly open: how a Node build step reconciles with the self-contained-binary distribution
model that ADR 0005 established and ADR 0006 re-affirmed.

Story 23.1's spike measured the cost but did not resolve it, and stated the problem as a binary:

> Either the shipped artefact becomes a client-rendered SPA over the IR (which forfeits NFR6 — the thing
> Axis 1 just proved), or `specscribe generate` invokes Node at run time (which forfeits self-containment).
> **This spike did not solve it, and it is the single biggest open question in Epic 23.**

**That binary is false**, and Story 23.5 exists because it is. It conflates two separable things: the
**toolchain** that builds the projection layer, and the **runtime** that renders with it. The spike never
tested whether they could be separated, because the load-bearing assumption underneath — that **one
prebuilt artefact can render many different projects** — had never been tried.

Story 23.5 tried it. Everything below is measured, and the harness is committed
(`web/scripts/experiment-two-ir.mjs`, results in `web/measurements/two-ir.json`) because Story 23.1's
review found its "every number is reproducible" claim to be false at the time.

### The measured asymmetry that decides this ADR

| | build toolchain | shipped runtime artefact |
|---|---|---|
| size | **201.9 MB** (`node_modules`, 16,791 files) | **3.78 MB** (185 files) |
| native `.node` bindings | **14** — Rolldown, Oxc parser, Rollup, LightningCSS | **0** |
| needs `process.dlopen` | **yes** | no — pure JavaScript |
| needed at generate time | **no** | yes |

The toolchain is 53× the size of the runtime and is the only part that requires native code. That is the
whole decision in one table: ship the small pure-JS half, keep the large native half in CI.

### The two-IR experiment (AC #4)

One artefact was built with `SPECSCRIBE_PACKAGE_BUILD=1` **while no IR existed on disk at all**, copied to
an isolated directory containing nothing but `.output/`, and then pointed at two different projects' IRs in
turn via `SPECSCRIBE_IR_DIR`. Routes were driven externally, one request per route, from each IR's own
manifest — deliberately not `nuxt generate`.

| IR | project | routes | passed | boot | median | p95 | full pass |
|---|---|---|---|---|---|---|---|
| A | SpecScribe (this repo) | 1,056 | **1,056 / 1,056** | 713 ms | 3.8 ms | 14.6 ms | **6.3 s** |
| B | CORA (a different repo) | 33 | 32 / 33 | 518 ms | 4.5 ms | 17.0 ms | 1.0 s |

Pass required all four of: HTTP 200; a non-trivial `<main>` region; **no** Nuxt hydration payload; and the
emitted page containing that IR's own `<main>` inner HTML **verbatim** — the same oracle `measure:parity`
uses.

**The hypothesis holds.** The route table that `nuxt.config.ts` bakes into `nitro.prerender.routes` at
config-load time does not bind the artefact, because the caller drives the routes: SpecScribe emitted the
manifest, so it already knows every route.

Two findings from that run matter more than the headline:

1. **A prebuilt artefact must not carry prerendered pages.** The first run "passed" 5/6 routes on both
   projects while returning **project A's dashboard for project B** — HTTP 200, wrong project. Nitro serves
   `public/` static files *ahead* of the SSR route, and `nuxt build` had baked 1,060 of project A's pages
   (68.0 MB) into `.output/public`. A wrong answer with a success status is the failure mode worth
   engineering against, so `SPECSCRIBE_PACKAGE_BUILD=1` now collapses the route table to empty, and the
   experiment harness refuses to run against an artefact containing prerendered HTML.
2. **One project-independence defect exists and is not this ADR's to fix.** `DashboardSurface.vue` throws
   unconditionally when a dashboard carries no `[data-hierarchy]` mount point. That holds for this
   1,056-page repo and is false for a 33-page one, which is why CORA's `index.html` is the single failing
   route. Raised against Story 23.3 rather than patched here.

### Rendering cost, in context

A full 1,056-route pass driven over the prebuilt server is **6.3 s**, against `nuxt generate`'s 25–30 s on
the same machine and the 23.1 spike's 37.1 s warm / ~130 s cold. The cold path stops existing: there is no
`npm ci`, no Vite build, and no `node_modules` at generate time.

### What the artefact actually adds

Of the 3.78 MB artefact, **1.46 MB is assets SpecScribe already ships today** —
`plotly-hierarchy.min.js` (1.22 MB), `specscribe.js`, `prism.js`, `prism.css`. **Net new: ~2.40 MB.**

## Decision

**Node is a build-time toolchain and a generate-time runtime. It is never a shipped toolchain.**

1. **The toolchain runs in CI and at package time only.** `npm ci` + `nuxt build` never run on a user's
   machine. The 201.9 MB `node_modules` and its 14 native bindings are build infrastructure and are not
   distributed.

2. **What ships is the prebuilt, project-independent `.output/` artefact** — 3.78 MB, pure JavaScript, no
   native bindings — produced by `npm run build:package` (`SPECSCRIBE_PACKAGE_BUILD=1`). That flag stubs the
   IR manifest empty, so the artefact **can be built with no IR present** and carries no project's routes
   or pages. A build without it is the development/parity path and stays bound to one project by design.

3. **SpecScribe drives the prerender.** At generate time the CLI boots the artefact, sets
   `SPECSCRIBE_IR_DIR`, and issues one request per route from the manifest it just emitted. It does not
   invoke `nuxt generate`, and the artefact does not crawl.

4. **The IR resolves at server runtime, not at build time.** This is the adjudication AC #5 required, and
   the two couplings resolve in opposite directions:
   - the **render path** reads `IR_DIR` from `process.env.SPECSCRIBE_IR_DIR` at module scope. Verified by
     reading the built bundle, not inferred: `var IR_DIR = resolve(process.env.SPECSCRIBE_IR_DIR ?? …)`
     survives verbatim into `.output/server`, with all three `readFileSync` call sites intact. Rollup
     cannot inline a computed path.
   - the **route table** was the genuine build-time coupling, and Decision 2 removes it.

   Story 23.2's payload measurement (variant C = 1.00× vs `useAsyncData` 1.36× vs `<NuxtIsland>` 1.99×) is
   **preserved, not traded away**: "build time, module scope" means *SSR render time inside the Nitro
   server*, which in a prebuilt artefact **is** runtime. The two forces were never actually in conflict —
   only the route table was, and it yielded.

5. **All three channels, stated per channel.** ADR 0012's packaging formula names three:
   - **npx (Story 16.8)** — npm invokes npx, so Node is present by construction. No new dependency.
   - **VSIX / Marketplace (Story 16.5, FR33)** — the extension host *is* Node (ADR 0005:30). No new
     dependency.
   - **Standalone self-contained binary (Story 16.3)** — the only channel a Node dependency genuinely
     breaks. **Decision: it requires Node, documented as a prerequisite** (owner decision, 2026-07-27). The
     binary detects Node at startup and, when it is absent or out of range, fails with an actionable error
     naming the supported range (`^22.19.0 || ^24.11.0 || >=26.0.0`). It does not bundle a JS runtime and
     does not silently degrade.

   ⚠️ **AC #2's "continue to function" is a design constraint on unbuilt channels, not a non-regression
   check.** **No Epic 16 channel exists yet**: every `16-*` key in `sprint-status.yaml` is `backlog` and
   there are no `16-*` story files. Release today is manual (`dotnet pack`; a VSIX from a local task doing a
   framework-dependent Debug publish). `extension/README.md:174-178` says it outright — "no CI wiring …
   nothing here publishes."

6. **Emitted asset paths are page-relative, never root-absolute.** Nuxt's default `/_nuxt/…` breaks
   `file://` and any subdirectory deployment, which ADR 0012 §Decision 1 forbids. A Nitro `render:html`
   plugin rewrites them to a depth-aware relative prefix, matching the convention
   `PathUtil.RenderHeadOpen` has always used. `app.baseURL: './'` was **rejected**: `baseURL` is one global
   string, but the correct prefix depends on each page's depth, so `./_nuxt/` would resolve to
   `epics/_nuxt/` on a nested page.

7. **`web/` enters CI.** `actions/setup-node` (pinned by `web/.nvmrc`), `npm ci`, the three generated-artifact
   drift gates, and Vitest with lcov coverage now run in `build-test-analyze.yml`, ordered after the C#
   build/test and after an IR generate (which `check:ir-content` requires).

8. **SpecScribe runs on Nuxt 4.** Absorbed here rather than deferred (owner decision, 2026-07-27) because
   Nuxt 3 reached end of life on 2026-07-31 and this is the packaging decision. Nuxt 3.21.9 → 4.5.1 held
   Story 23.3's parity contract exactly: **190/190 `<main>` byte-identical, 190/190 verbatim, 0 link
   regressions**, unchanged from the Nuxt 3 baseline measured immediately before the upgrade.

## Relationship to ADR 0006 — this AMENDS it

ADR 0006 §Decision states: *"Therefore ADR 0005 is re-affirmed (not superseded). Its data path (C# renders
webview/HTML), its **self-contained packaging**, and its spawn-the-tool invocation all stand."*

**The self-contained-packaging clause no longer stands unqualified, and this ADR says so rather than letting
it drift.** The standalone binary remains a self-contained *.NET* artifact — no .NET runtime install, ADR
0005 §Decision 2's ~73 MB/RID model is untouched — but it acquires an **external Node runtime prerequisite
at generate time**. "Self-contained" now means "self-contained with respect to .NET", not "no external
runtime of any kind".

Two clarifications about what is *not* being amended:

- ADR 0006's *"data path (C# renders webview/HTML)"* clause was already relaxed for the presentation layer
  by **ADR 0009**, not by this ADR.
- ADR 0006's **NFR6 ruling** — *"A SPA that renders the information from JSON in the client violates that
  policy on its face"* — is **fully upheld**. Nothing here client-renders. Story 23.3's
  `routeRules: { '/**': { noScripts: true } }` means IR-backed routes ship no Nuxt runtime and no hydration
  payload at all, and the two-IR experiment asserts that per route rather than assuming it. What ADR 0006
  called a "free" pre-rendered fallback is now the *only* thing shipped.

  (Cite this as the PRD's **NFR-5**, referred to as NFR6 throughout Epic 23 per the recorded and still
  unresolved collision at `epics.md:123-134` — `epics.md`'s own NFR6 is cross-surface accessibility
  semantics, a different requirement.)

## Alternatives rejected

**B — bundle a JS runtime into the standalone binary.** Newly *viable*, because the shipped artefact is pure
JavaScript with zero native bindings, but rejected on cost: **+50–100 MB per RID** (a ~73 MB binary becomes
~130–180 MB), plus a second runtime's CVE surface, AV/notarization exposure, and — on Windows — a
single-file host that extracts and launches an embedded JS runtime resembling the textbook dropper
heuristic. Kept on record as the escape hatch if the Node prerequisite proves unacceptable.
(`@yao-pkg/pkg` — `vercel/pkg` was archived 2024-01-13 — or Bun `--compile`; Node's own SEA is still
`Stability: 1.1 — experimental`, and its ESM support means Node 26, not 24 LTS. `nexe`'s last stable
release was 2018.)

**C — ship `node_modules` and run `nuxt generate` on the user's machine.** Rejected: +201.9 MB and ~130 s
cold on first run. This is the option that would forfeit self-containment in the way ADR 0006 feared, and it
is the only one that actually does.

**D — client-rendered SPA over the IR.** Rejected, and **not an implementation call to make**: ADR 0009
ratified Axis 1 = Option B (universal/SSR) and explicitly rejected client-only. Choosing D means reopening a
ratified ADR. ADR 0006 is blunter still (quoted above). It also breaks `file://`.

**Embedding a JS engine in .NET to run the *build*.** Closed, not narrow. The Nuxt 4 toolchain ships
platform-native `.node` bindings (Rolldown, Oxc, Rollup, LightningCSS — verified, 14 of them) requiring
`process.dlopen`, so **no pure-JS engine (Jint, ClearScript/V8) can host a Nuxt build at all**.
`Microsoft.JavaScript.NodeApi`/LibNode is the only architectural fit and is a preview package hosting an
experimental subsystem pinned to Node 20.18, **EOL since 2026-04-30**.

**Citing `tools/prism-vendor/` as precedent.** Declined. It looks like a precedent for build-time-only Node
and is not one: `build.js` is run by hand, out of band, its outputs are committed and reach the C# build as
`<EmbeddedResource>`, and it produces a **project-independent** artefact. A Nuxt prerender is
project-**dependent**, which is exactly why the precedent does not transfer. The closest real industry
analogue is **ReportGenerator (.NET)** — C# writes the real markup, a prebuilt Angular bundle is committed
and embedded with no npm step in the `.csproj`, and the report works substantially with JS off. The
cautionary pole is **Allure**: prebuilt bundle, 100% client-rendered, no `<noscript>`, blank from `file://`
— which is why `allure open` has to exist. **Nobody ships a JS build toolchain to run at user-run time.**

## Consequences

**Accepted:**

- The standalone binary gains a documented Node prerequisite. A user without Node cannot generate once
  Story 23.4 retires the C# renderer. This is the cost of the decision and it is deliberate.
- ~2.40 MB net new shipped bytes.
- The release pipeline gains a Node build stage. It must run `npm run build:package`, and it must publish
  the artefact **without** `.output/public`'s prerendered HTML — Decision 2 makes that structural rather
  than a packaging instruction someone has to remember.
- `web/` coverage is measured at ~51% statements / ~53% lines, below `Sonar way`'s 80% new-code condition,
  so the first scan including it is expected to show `new_coverage` red. Not blocking
  (`sonar.qualitygate.wait` stays unset per Story 25.2). The fix is component tests, **not** widening the
  coverage exclusion back to `web/**`.
- Reproducibility (NFR9) is widened, not fixed. `SpecScribe.csproj:26-42` honours `SOURCE_DATE_EPOCH` but no
  workflow sets it, there is no `<Deterministic>`/`ContinuousIntegrationBuild`/SourceLink, and `<Version>`
  is a hand-edited literal. Adding a Nuxt build stage adds a second non-reproducible input. Named here so it
  is inherited knowingly; it is not this ADR's to close.

**Newly required of other stories:**

- **Story 23.4** is unblocked. Its precondition — "packaging is settled" — is settled by this ADR.
- **Story 16.3** must implement Node detection with an actionable error, and document the prerequisite.
- **Story 16.1/16.4** must add the `npm run build:package` stage and publish the pruned artefact.
- **Story 23.3** owns the `DashboardSurface.vue` project-independence defect.

**Constraints inherited unchanged:**

- `web/` cannot be relocated away from `src/`: `web/scripts/tokens-lib.mjs:15-17` resolves
  `../../src/SpecScribe/assets/specscribe.css` by relative path, so shipping `web/` as a separate npm
  package, submodule, or build container breaks both the token extraction and its drift gate.
- The Nuxt build is **step 2** of a two-step pipeline. `nuxt.config.ts` reads the IR manifest at config
  time and `sync-runtime-assets.mjs` copies ~1.4 MB of C#-owned assets into `web/public/`, so a
  project-bound build is ordered after a C# generate. `build:package` is the exception that needs no IR.
- `crawlLinks: false` is load-bearing, not a preference (23.1 finding 8).
- Routes carry `.html` verbatim (ADR 0017). The packaged output must preserve that path space.

## Ratified decisions

*(none yet — ratification is the owner's)*

Left to the owner:

1. Whether the standalone binary's Node prerequisite should be **checked at install time** as well as at
   generate time, for the npx channel specifically.
2. Whether `web/`'s ~51% coverage warrants a named component-test story now or after Story 23.4 collapses
   the surface area.
