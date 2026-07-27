# Story 23.5 — Packaging Strategy Report

**Date:** 2026-07-27 · **Baseline commit:** `86b35c2` · **HEAD at start of work:** `40c7ee9`
**Decision record:** [ADR 0022](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) — *Proposed*
**Reproduce:** `web/scripts/experiment-two-ir.mjs` · raw results in `web/measurements/two-ir.json`

---

## Verdict

**The Node toolchain and the Node runtime are separable, and separating them resolves Epic 23's biggest
open question without forfeiting either NFR-5 or the self-contained model in the way Story 23.1 feared.**

Epic 23's gate stated the problem as a binary — client-rendered SPA *or* Node at run time. It is false. The
thing that makes it false was never tested before this story: **one prebuilt artefact can render many
different projects.** It can.

| | build toolchain | shipped runtime artefact |
|---|---|---|
| size | **201.9 MB** (`node_modules`, 16,791 files) | **3.78 MB** (185 files) |
| native `.node` bindings | **14** | **0** |
| requires `process.dlopen` | yes | no — pure JavaScript |
| required at generate time | **no** | yes |

53× smaller, and the only half that needs to exist on a user's machine is the half with no native code.

Net new shipped bytes: **~2.40 MB** (1.46 MB of the artefact is `plotly-hierarchy.min.js`, `specscribe.js`,
`prism.js` and `prism.css`, which the C# portal already ships today).

---

## 1. Method and provenance

| figures | provenance |
|---|---|
| the two-IR table, per-route latency, boot times | **Harness-derived** — `web/scripts/experiment-two-ir.mjs` recomputes them and writes `measurements/two-ir.{txt,json}` |
| parity (190/190) and link (0 regressions) figures | **Harness-derived** — `npm run measure:parity`, `npm run check:links` |
| artefact / `node_modules` sizes and file counts, native-binding inventory | **Session-measured** on this machine (Windows 11, Node 24.11.1); commands given inline below |
| build and generate wall-clock | **Session-measured**; not reproduced by a committed script |
| Nuxt 4 version/engines facts | Read from the registry (`npm view nuxt`) on 2026-07-27 |

Story 23.1's code review found its "every number is reproducible with `npm run measure`" claim to be false
at the time. The load-bearing experiment here is therefore a **committed harness**, not a session
transcript, and it reports whether a run was sampled so a truncated run cannot be published as a full one.

---

## 2. The load-bearing experiment (AC #4)

### Setup

1. `npm run build:package` once — with **no IR on disk at all** (`SpecScribeOutput/spa/` did not exist).
2. Copy **only** `.output/` into an empty directory. The harness refuses to run if `node_modules`,
   `nuxt.config.ts`, `package.json`, `ir/`, `pages/` or `components/` sits beside it — otherwise a pass
   would be measuring the development tree.
3. Boot `node .output/server/index.mjs` with `SPECSCRIBE_IR_DIR` → IR **A**, drive every route from A's own
   manifest, one request each. Kill, restart against IR **B**, repeat.

Routes are driven **externally**, deliberately not by `nuxt generate`: SpecScribe emitted the manifest, so
it already knows every route.

### What counts as a pass

Per route, all four: **HTTP 200**; a non-trivial `<main>` region (a 200-with-empty-shell fails);
**no Nuxt hydration payload**; and the emitted page **containing that IR's own `<main>` inner HTML
verbatim** — the same oracle `measure:parity` uses.

### Result

| IR | project | routes | passed | boot | median | p95 | full pass |
|---|---|---|---|---|---|---|---|
| A | SpecScribe (1,056 pages) | 1,056 | **1,056 / 1,056** | 713 ms | 3.8 ms | 14.6 ms | **6.3 s** |
| B | CORA (a different repo) | 33 | 32 / 33 | 518 ms | 4.5 ms | 17.0 ms | 1.0 s |

**Confirmed.** The route table baked into `nitro.prerender.routes` at config-load time does not bind the
artefact when the caller drives the routes.

Against the 23.1 baselines: a full pass is **6.3 s**, versus `nuxt generate` at 25–30 s on this machine and
the spike's 37.1 s warm / ~130 s cold. **The cold path stops existing** — no `npm ci`, no Vite build, no
`node_modules` at generate time.

### Three things the experiment got wrong first, and what they taught

Recorded because each was a *false* result that looked plausible, and two were the harness's fault.

1. **A prebuilt artefact carrying prerendered pages returns the wrong project with a 200.** The first run
   reported 5/6 on both projects — and both returned the *same* 858 KB for `/index.html`. Nitro serves
   `public/` static files **ahead of** the SSR route, and `nuxt build` had baked 1,060 of project A's pages
   (68.0 MB) into `.output/public`. A wrong answer with a success status is the failure mode worth
   engineering against. Fixed structurally: `SPECSCRIBE_PACKAGE_BUILD=1` empties the route table, and the
   harness now refuses any artefact containing prerendered HTML.
2. **Substring matching for hydration markers failed 6 routes, then 1.** This portal renders its own source
   and its own design docs, so `_payload.json`, `window.__NUXT__` and `__NUXT_DATA__` all appear as **prose**
   — inside `<code>`, and inside JSON data islands, which are themselves `<script type="application/json">`.
   "Is it in a script tag" is not a discriminator. Every pattern must match a **mechanism**, never a
   mention. (Story 23.3 hit the identical trap with `data-hierarchy`.)
3. **Comparing against each IR's own golden STATIC page reported 46 false failures.** For the
   dashboard/epics families the IR is deliberately a *more complete* render than the static page —
   `measure:parity` scores that golden≠IR delta at 143/190 and attributes it to Epic 22. Comparing against
   the golden page measures an inherited capture delta, not whether this artefact rendered this project.

### The one real defect — raised, not patched

`web/components/surfaces/DashboardSurface.vue` throws unconditionally when a dashboard region carries no
`[data-hierarchy]` mount point. True for this 1,056-page repo; **false for a 33-page one** — CORA's
dashboard has zero occurrences, so `index.html` returns HTTP 500. Every other CORA route renders correctly.

This is a genuine project-independence defect and it is **Story 23.3's**, not this story's: the guard is a
deliberate ADR 0012 §Decision 2 contract assertion, and relaxing it trades away a real regression check for
SpecScribe's own capture. Raised as a follow-up with reproduction steps. Story 23.5 did not edit it, per its
own scope instruction.

---

## 3. The adjudication (AC #5)

Two forces were recorded as being in direct conflict:

| | wants | why | evidence |
|---|---|---|---|
| **Payload** | IR resolved at **build time, module scope** | 1.00× vs `useAsyncData` 1.36× and `<NuxtIsland>` 1.99× | 23.2 AC #4 |
| **Packaging** | IR read at **server runtime** | one artefact must serve many projects | this story |

**They were never actually in conflict.** "Build time, module scope" means *SSR render time inside the Nitro
server*, and in a prebuilt `.output/` that **is** runtime. Only one of the two couplings was genuine:

- **(b) the render path — runtime-resolvable, and verified rather than inferred.** Reading the built
  bundle: `var IR_DIR = resolve(process.env.SPECSCRIBE_IR_DIR ?? resolve(process.cwd(), "..", "SpecScribeOutput"));`
  survives verbatim into `.output/server/chunks/build/`, with all three `readFileSync` call sites intact.
  Rollup cannot inline a computed path.
- **(a) the route table — the real build-time coupling, and it yielded.** `nuxt.config.ts` executes
  `import { site } from './ir/adapter'` at config-load time to compute `nitro.prerender.routes`. Two costs,
  both measured: the artefact could not be **built at all** without an IR present, and `nuxt build`
  prerendered project A's pages into the shipped output. `SPECSCRIBE_PACKAGE_BUILD=1` stubs the manifest
  empty and collapses the route table, removing both.

**No constraint was traded away.** 23.2's payload measurement stands unchanged.

⚠️ One correction to the story's seeded notes: `IR_DIR` now resolves from **`process.cwd()`**, not from
`import.meta.url` as recorded at seeding — `web/ir/adapter.ts` documents why (an `import.meta.url` default
silently became `web/SpecScribeOutput` inside the Nitro bundle and every route failed).

---

## 4. Strategy comparison, with measurements replacing the seeded estimates

| | strategy | added bytes | cold cost | end-user runtime dep | verdict |
|---|---|---|---|---|---|
| **A** | Prebuilt `.output/`; SpecScribe drives a per-project prerender | **~2.40 MB net new** (3.78 MB artefact) | boot **~0.6 s**, then **~4 ms/route** | Node 22.19+/24.11+/26+ | **CHOSEN** |
| **B** | A + a bundled JS runtime for the standalone binary only | +50–100 MB/RID (→ ~130–180 MB) | + a few hundred ms | none | rejected on cost; kept as escape hatch |
| **C** | Ship `node_modules`, run `nuxt generate` on the user's machine | **+201.9 MB** | **~130 s cold** | Node + full toolchain | rejected — this is the option that actually forfeits self-containment |
| **D** | Client-rendered SPA over the IR | ~+1 MB | ~0 | none | **already ruled out by ADR 0009**; breaks `file://` |

Seeded estimates that the measurements moved: A's added bytes (~2.8 MB → 3.78 MB gross / ~2.40 MB net), A's
per-route cost (confirmed at ~4 ms), C's `node_modules` (175 MB → **201.9 MB** on Nuxt 4), and the boot cost
(1–5 s → **0.5–0.7 s**).

### Per channel (AC #3)

ADR 0012's formula names three channels. **All three are unbuilt** — every `16-*` key is `backlog`, no
`16-*` story file exists, and `extension/README.md:174-178` says "no CI wiring … nothing here publishes".
So AC #2's "continue to function" is a **design constraint on unbuilt channels**, not a non-regression check
against a live system.

| channel | Node already present? | what the strategy costs it |
|---|---|---|
| **npx** (16.8) | **yes** — npm invokes npx | nothing. No new dependency. |
| **VSIX / Marketplace** (16.5, FR33) | **yes** — the extension host *is* Node (ADR 0005:30) | nothing. No new dependency. |
| **Standalone binary** (16.3) | **no** | the only channel a Node dependency genuinely breaks |

**The standalone binary requires Node, as a documented prerequisite** (owner decision, 2026-07-27). It
detects Node at startup and fails with an actionable error naming the supported range when it is absent or
out of range. It does not bundle a runtime and does not silently degrade. Cost stated plainly: **a user
without Node cannot generate at all** once Story 23.4 retires the C# renderer.

### The negative result on embedding a JS engine in .NET

**The door is closed, not narrow — and this was verified, not assumed.** The Nuxt 4 toolchain ships
platform-native `.node` bindings requiring `process.dlopen`: `@rolldown/binding-win32-x64-msvc`,
`@oxc-parser/binding-win32-x64-msvc`, `@rollup/rollup-win32-x64-{msvc,gnu}`, `lightningcss-win32-x64-msvc`
— 14 `.node` files in total. **No pure-JS engine (Jint, ClearScript/V8) can run a Nuxt *build*.**
`Microsoft.JavaScript.NodeApi`/LibNode is the only architectural fit and is a preview package hosting an
experimental subsystem pinned to Node 20.18, **EOL since 2026-04-30**.

The mirror-image finding is what makes strategy A work: the **shipped artefact contains zero native
bindings**. Build needs native code; runtime does not.

### Industry precedent

Uniform, and worth stating: **nobody ships a JS build toolchain to run at user-run time.** The closest
structural analogue is **ReportGenerator (.NET)** — C# writes the real markup, a prebuilt Angular bundle is
committed and embedded as a resource with **no npm step in the `.csproj`**, and the report works
substantially with JS off. The cautionary pole is **Allure**: prebuilt bundle, but 100% client-rendered, no
`<noscript>`, blank from `file://` — which is why `allure open` has to exist.

`tools/prism-vendor/` is **not** the precedent it appears to be: run by hand out of band, outputs committed
and embedded, and producing a **project-independent** artefact. A Nuxt prerender is project-**dependent**.
The precedent does not transfer, and citing it as a solved pattern would be wrong.

---

## 5. Nuxt 3 → 4 (owner decision, absorbed here)

Nuxt 3 reached end of life **2026-07-31**. Absorbed into this story rather than deferred, because it is a
dependency/packaging decision and this is the packaging story.

**3.21.9 → 4.5.1.** The legacy directory layout is auto-detected by Nuxt 4, so no `app/` move was made —
deliberately, to keep risk off Story 23.3's parity contract.

| gate | Nuxt 3.21.9 (baseline) | Nuxt 4.5.1 |
|---|---|---|
| `<main>` IR ≡ Nuxt | 189/189 | **189/189** |
| verbatim containment | 189/189 | **189/189** |
| golden ≡ Nuxt | 143/189 (46 inherited, Epic 22's) | 143/189 |
| link regressions | 0 | **0** |
| full generate | 30 s (1,068 routes) | 25 s (1,068 routes) |

*(The later 190/190 figures elsewhere in this report are from a fresh IR generated after the upgrade, which
carries one additional migrated page. The 189-page comparison above is the like-for-like one.)*

Head projection survived unhead v2 intact — title, description, `og:*`, favicon data-URI, `lang="en"`, and
the relative `specscribe.js` reference all unchanged.

**Recorded honestly:** `npm audit` reports **11 high-severity advisories**, all one root cause — a
`brace-expansion` DoS reached transitively through `minimatch → glob → archiver → nitropack`. Every one is a
`devDependency` in the **build toolchain**, which by this ADR's decision is never distributed. Node version
used and now pinned: **24.11.1** (`web/.nvmrc`, plus an `engines` field —
`^22.19.0 || ^24.11.0 || >=26.0.0`). Before this story the repo pinned Node **nowhere**: no `.nvmrc`, no
`engines`, no `global.json`, only a version named in prose in `web/CONVENTIONS.md:94`.

---

## 6. Asset paths and `file://` (AC #7)

**Defect reproduced first**, on a real emitted page: `href="/_nuxt/entry.TlFkk8f6.css"` — root-absolute,
because `app.baseURL` is unset. From `file://` that resolves to the filesystem root and 404s, so **every
page loses its stylesheet**. ADR 0012 §Decision 1 requires the portal to work offline and from `file://`;
`EXPERIENCE.md:270` has the owner copying the output to a USB drive.

**`app.baseURL: './'` was evaluated and rejected.** `baseURL` is one global string, but the correct prefix
depends on each page's depth: `./_nuxt/` resolves against the page's own directory, so on
`epics/epic-3.html` it asks for `epics/_nuxt/` and on `code/src/SpecScribe/Charts.cs.html` for
`code/src/SpecScribe/_nuxt/`. Both 404.

**Fix:** a Nitro `render:html` plugin rewrites `/_nuxt/` to a depth-aware relative prefix, matching the
convention `PathUtil.RenderHeadOpen` has always used. The depth rule handles the trap that extension-less
routes are written to `<route>/index.html`, one level deeper than the route string suggests.

**Verified in a live browser from `file://`** (CLAUDE.md requires this; the suite structurally cannot see
this defect class):

| page | depth | emitted href | stylesheets loaded |
|---|---|---|---|
| `epics.html` | 0 | `_nuxt/entry.…css` | 871 + 11 + 9 + 64 rules |
| `code/src/SpecScribe/Charts.cs.html` | 3 | `../../../_nuxt/entry.…css` | 871 + 11 + 9 + 17 rules |

`body` computes to the portal parchment `rgb(245, 240, 232)` — not the unstyled default — and
`"/_nuxt/` appears **zero** times in the emitted document. Parity and links re-run after the change:
**190/190 byte-identical, 190/190 verbatim, 0 link regressions** (the rewrite is outside `<main>`).

---

## 7. The pipeline (AC #8)

**Before this story, nothing built or checked `web/` anywhere** — no CI step, no MSBuild target, no npm
lifecycle hook. That was not a theoretical gap:

> **Two of the three drift gates were RED at `HEAD` when this story first ran them.**
> `check:ir-content` (48 rules dropped, 3 added) and `check:assets` (`specscribe.js` stale) had both drifted
> from a concurrent story's changes in commit `40c7ee9`, and nothing had noticed. That is the argument for
> AC #8 in one observation.

Both are now green, along with `check:tokens`.

### What was wired

Into `build-test-analyze.yml` (**not** a second workflow — Story 16.2 as amended is explicit that two
workflows which both build and test is the exact drift class this project has paid for), ordered after the
C# build/test and before `SonarScanner end`, since a report written after `end` is silently ignored:

- `actions/setup-node@v4` pinned by `web/.nvmrc`, npm cache keyed on `web/package-lock.json`
- `npm ci` (not `npm install` — the lockfile is the pin)
- `dotnet run … generate --spa` — `check:ir-content` derives from the C# stylesheet **and** the emitted IR,
  so a portal must exist first. That ordering is inherent to Story 23.3's design.
- `npm run check` — the three drift gates, with no `continue-on-error`
- `npm run test:coverage` — Vitest → `web/coverage/lcov.info`

Plus `prebuild`/`pregenerate` npm lifecycle hooks running `check:tokens`, which the gates previously lacked
entirely.

### A defect this wiring surfaced, and fixed

`ir-content.manifest.json` carried `migratedPages`, `totalPages` and `passThroughUncoveredClasses` —
statistics over the **entire 1,056-page corpus**. Committed, they changed whenever anyone added a document,
so the drift gate would have been **red on ordinary docs commits that cannot touch the stylesheet**. A gate
that cannot stay green teaches people to re-run the extractor on reflex, which is exactly how real drift
gets committed unnoticed.

They are no longer committed; they are still computed and still printed by the extractor's console summary.
**Stated honestly:** this does not make the gate corpus-independent. `rules` and the emitted CSS still
depend on which classes the **four migrated families** use, so a dashboard/epics markup change can still
legitimately move the file. That is the gate working as designed — narrow (4 families) where the removed
fields were broad (every page). Verified stable by running the gate green against **two different IR
generates**.

### Sonar posture (AC #8: "state which and why")

⚠️ **The story's seeded premise is stale.** It records `web/**` as *absent* from the exclusion list. Story
25.2 (decision 1b) has since added `sonar.coverage.exclusions="web/**"` — and was right to, because the
only uploaded report was C#-only OpenCover, which structurally cannot reach a Node subtree; `web/**`
contributed 918 new lines-to-cover, all uncovered, dragging `new_coverage` to 59.4% against an 80%
threshold. That exclusion was a workaround for a **missing report**.

The report is no longer missing. This story stood up Vitest (**80 tests**) and wires
`sonar.javascript.lcov.reportPaths`, so the blanket exclusion is **narrowed** to what genuinely cannot be
unit-tested, mirroring `vitest.config.ts` so the two cannot drift:

- `web/scripts/**` — integration harnesses that spawn servers and walk `.output/`. The three pure libraries
  they share (`harness-lib`, `ir-content-lib`, `tokens-lib`) are **not** excluded and are covered.
- `web/server/plugins/**` — `defineNitroPlugin` is a Nitro auto-import that does not exist outside the Nitro
  build, so these cannot even be *imported* by a test. The one piece of real logic was moved out into
  `web/server/utils/relative-prefix.ts` precisely so it could be tested; it is at 100%.
- `web/**/*.vue` — no component tests yet. The honest gap, named below.

Also added to `sonar.exclusions`: `web/node_modules/**`, `web/.output/**`, `web/.nuxt/**`, `web/public/**`,
`web/coverage/**`, `web/measurements/**`, and the two generated CSS artifacts. **`web/node_modules/**` was
absent and CI now runs `npm ci` there** — without this the next scan would have ingested 16,791 files.

**Consequence, stated rather than hidden:** measured `web/` coverage is **51.19% statements / 53.40% lines**,
below `Sonar way`'s 80% new-code condition, so the first scan including it is **expected to show
`new_coverage` red**. It is not blocking (`sonar.qualitygate.wait` stays unset per 25.2). The fix is
component tests — **not** widening the exclusion back to `web/**`.

### The test suite

80 tests across 5 files, chosen for risk rather than for the coverage number:

| file | what it pins |
|---|---|
| `region-split.test.ts` | the **two region shapes** — the hazard that nested `<main>` inside the wayfinding band on 187 pages while every harness passed |
| `relative-prefix.test.ts` | the depth rule behind `file://`, incl. the extension-less-route trap, and agreement with the adapter's `relativePrefix` so three implementations of one rule cannot drift |
| `tokens-lib.test.mjs` | comment-aware `:root` slicing — a brace inside a comment silently truncating the token copy |
| `ir-content-lib.test.mjs` | selector scoping, incl. keeping a `:root`/`html` head *outside* the scope |
| `harness-lib.test.mjs` | `normalizeVolatile` in **both** directions — scrubbing too much silently passes a real regression |

---

## 8. Scope guard (AC #9)

| check | result |
|---|---|
| `HtmlRenderAdapter` intact | ✅ present, unmodified |
| `SpecScribe.slnx` project count | ✅ 2 |
| C# touched by this story | ✅ **none** — no file under `src/` or `tests/` is in this story's File List |
| `GoldenContentFingerprint` | ✅ **green** — full suite 2,538 passed / 0 failed / 3 skipped |

The golden fingerprint test runs against a **fixture** that is not a git repo and cites no real repo files,
so this story's new tracked `web/**` files cannot reach it through the dogfood Code Map.

⚠️ **`git status` shows modifications under `src/` and `tests/` that are NOT this story's.** Concurrent
sessions are working Epic 18 (`IdeaDiscovery.cs`, `IdeasModel.cs`, `IdeasTemplater.cs`, `Memlog.cs`,
`IdeasTests.cs`, ADR 0021, stories 18-5 and 25-3) and touched `SiteGenerator.cs`, `RelatedWork*.cs`,
`StatusStyles.cs`, `specscribe.css` and `specscribe.js`. Scope was verified by **File List**, not by a diff
range — per CLAUDE.md, and because a diff range here would attribute several other stories' work to this one.

---

## 9. Concurrency notes (this was a live tree)

Recorded because two events changed how the work had to be run:

- **`SpecScribeOutput/spa/` was wiped mid-experiment, twice**, by a concurrent session running `generate`
  *without* `--spa`. The experiment was moved onto IRs generated into a scratch directory so a concurrent
  regeneration could not invalidate a measurement in flight.
- `web/ir/adapter.ts` and `web/nuxt.config.ts` had both moved since the story was seeded; line numbers in
  the story's Dev Notes no longer resolve, and `IR_DIR`'s default had changed from `import.meta.url` to
  `process.cwd()`. Re-read before use, per the story's own instruction.

---

## 10. Open items

| # | item | owner |
|---|---|---|
| 1 | `DashboardSurface.vue` hard-throws on any project with no Hierarchy Explorer | **Story 23.3** |
| 2 | `web/` component tests to lift coverage past the 80% new-code threshold | unowned — needs a story |
| 3 | Node detection + actionable error in the standalone binary | **Story 16.3** |
| 4 | `npm run build:package` stage in the release pipeline | **Stories 16.1 / 16.4** |
| 5 | Whether the npx channel should check the Node prerequisite at install time | owner (ADR 0022) |
| 6 | NFR9 reproducibility widened, not closed — no workflow sets `SOURCE_DATE_EPOCH`, no `<Deterministic>`, `<Version>` is a hand-edited literal | unowned; named so it is inherited knowingly |

**Story 23.4 is unblocked.** Its stated precondition — that packaging is settled — is settled by ADR 0022.
