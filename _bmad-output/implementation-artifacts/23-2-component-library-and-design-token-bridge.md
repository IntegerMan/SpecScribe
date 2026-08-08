---
baseline_commit: cd7f30255bb07112332c0876f4335e6b77ca9f4d
---

# Story 23.2: Component Library + Design-Token Bridge

Status: review

<!-- Moved in-progress -> review by the 2026-08-07 dev-story round. Both blockers the code review left are
     closed: `check:ir-content` is GREEN on a full `--deep-git` corpus (1476 rules / 3 shared, and a fresh
     extraction reproduced the committed artifacts byte-for-byte — the RED was a missing flag, not a broken
     environment; see the ✅ block in § Review follow-up — 2026-08-07), and the last open [Review][Decision]
     was answered by the owner and implemented (`--if-ir`). Every checkbox in this file is now ticked.

     ⚠️ ONE THING REMAINS FOR THE OWNER, AND IT IS NOT A CHECKBOX: the live-browser pass CLAUDE.md requires
     for visual work has still NOT happened, across four consecutive sessions. This session had no browser
     tool at all. The token BINDINGS were verified statically on both surfaces and the skip-link is now
     provably a single unscoped rule, but nobody has LOOKED at the pending/deferred border change or a
     focused skip link on a scrolled IR page. That is the owner's verify round. -->


<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer preserving the antiquarian design system during the Vue/Nuxt migration,
I want the shared presentation tokens ported into scoped Vue components and a design-system reference page shipped now,
so that visual consistency — status/motion tokens, AD-7 — survives the framework change and is documented for future component authors and end users alike.

## Acceptance Criteria

_The first two ACs are the epic's stated ACs (epics.md §Story 23.2). ACs 3–6 are the concrete
scope this story was seeded with — the 23.1 spike gate's two re-scope items (AC 4, AC 5) and the
owner's design-system-page decision (AC 6)._

1. **Given** the existing `--status-*` and `--motion-*` token families (defined once in the `:root` block of `src/SpecScribe/assets/specscribe.css`)
   **When** they are made available to the Vue app
   **Then** the Vue components consume the **same token values with no duplicated or hand-re-typed color/motion definitions** — the token values reach Vue through a generated `web/assets/tokens.css` extracted from the C# stylesheet, and a build-time drift check fails if the extracted set diverges from the source `:root`.

2. **Given** AD-7's presentation-token architecture (SpecScribe owns content-semantic tokens; host chrome is host-owned)
   **When** the component library is established
   **Then** the scoped-SFC / CSS-module conventions are **documented for future component authors**, and the documentation explicitly covers the load-bearing rule the 23.1 spike surfaced: `<style scoped>` does **not** reach `v-html`-injected IR markup (see Dev Notes → Spike constraints).

3. **Given** the Vue app has no home yet
   **When** the component library lands
   **Then** it lives in a new repo-root **`web/`** directory (a production-intent Nuxt 3 app), it is **not** added to `SpecScribe.slnx`, **not** wired into `specscribe generate`, and the primitives 23.3 will consume — at minimum **StatusBadge**, the **framed chart-panel** (`chart-panel` + `Charts.Framed`/`ChartMeta` frame), **ListRow**, and the **page shell** — are implemented as scoped-CSS Vue components, each rendered in the design-system page so it is validated by a real consumer rather than authored blind.

4. **Given** the 23.1 spike finding that the async-data data path doubles site weight (2.26×, entirely hydration payload)
   **When** the component approach is chosen
   **Then** this story **measures the `<NuxtIsland>` / server-component shape against hydration-payload duplication first**, on at least one representative component, and records the result (a paragraph in the conventions doc) so 23.3 inherits a measured recommendation rather than the spike's payload-maximising shape.

5. **Given** the spike's `:deep()` finding
   **When** the conventions are written
   **Then** they establish and demonstrate the `:deep()`/global-sheet convention for styling `v-html`'d IR content, so 23.3 does not rediscover it.

6. **Given** the owner's decision that the design system must be documented in the shipped portal now (before the Nuxt app renders any user's project)
   **When** generation runs
   **Then** a **C#-generated `design-system.html`** page is written on every full run, is **referenced explicitly in the Help nav group** (and the dashboard Help quick-links), documents the `--status-*` / `--motion-*` token families and the shared visual primitives, and satisfies NFR6/NFR-5 (fully readable with JavaScript off; status shown by **name**, never color alone — UX-DR17). A parallel Nuxt `/design-system` route renders the same primitives from the Vue components and becomes the portal's design-system surface when the Nuxt app is wired in (23.3/23.4).

## Tasks / Subtasks

- [x] **Task 1 — Scaffold the `web/` Nuxt app** (AC: #3)
  - [x] Create repo-root `web/` with `package.json` (devDependencies: `nuxt ^3.14`, `vue ^3.5`, `vue-router ^4.4` — mirror `spike/nuxt-ir/package.json`), `nuxt.config.ts` (`ssr: true`, `telemetry: false`, `devtools.enabled: false`, full prerender per ADR 0009 Option B), and `app.vue`.
  - [x] Confirm `node_modules/` is already covered by `.gitignore` (it is — global `node_modules/` rule) and that `web/` is **not** added to `SpecScribe.slnx`.
  - [x] Do **not** wire `web/` into `specscribe generate` — packaging is Story 23.5, sequenced ahead of 23.4.
- [x] **Task 2 — Token bridge: extract `tokens.css` from the C# stylesheet** (AC: #1)
  - [x] Write `web/scripts/extract-tokens.mjs` that reads the `:root { … }` block from `src/SpecScribe/assets/specscribe.css` and emits `web/assets/tokens.css` containing exactly those custom properties (status, motion, and brand palette). Keep the extraction a pure copy — no re-typed literals.
  - [x] Import `tokens.css` (and only `tokens.css`) as the token source in the Nuxt app; author every component's own rules with `<style scoped>`.
  - [x] Add a **drift check** (`web/scripts/check-tokens.mjs`, wired as `npm run check:tokens`) that re-extracts and diffs against the committed `web/assets/tokens.css`, exiting non-zero on divergence. Document that `npm run extract:tokens` must be re-run after any token change in the C# stylesheet.
- [x] **Task 3 — Build the proof primitives as scoped-CSS Vue components** (AC: #3)
  - [x] `StatusBadge.vue` — the six-stage `--status-*` badge, status conveyed by **label text**, not color alone (UX-DR17). Model semantics on `.status-badge` in `specscribe.css`.
  - [x] `ChartPanel.vue` (or `FramedPanel.vue`) — the `chart-panel` + `Charts.Framed`/`ChartMeta` frame (title, analysis window, framing sentence, legend slot) per Story 10.2.
  - [x] `ListRow.vue` — the unified `ListRow` primitive (`--list-row-accent`; Story 10.8 grammar).
  - [x] `PageShell.vue` — the page shell (header/main/footer chrome), reused by the design-system route.
  - [x] Every color/timing value comes from a `var(--…)` token; no primitive re-types a token value.
- [x] **Task 4 — Measure the `<NuxtIsland>` / server-component shape** (AC: #4)
  - [x] Render at least one representative primitive both ways (async-data path vs `<NuxtIsland>`/server-component) under `nuxt generate`; record the output-weight delta. **Three** variants measured (a static control was added — it turned out to be the winner).
  - [x] Write the measured recommendation into the conventions doc so 23.3 uses the payload-avoiding shape by default.
- [x] **Task 5 — Document scoped-SFC / CSS-module conventions** (AC: #2, #5)
  - [x] `web/CONVENTIONS.md` (or a section in `web/README.md`) covering: tokens.css is the only token import and is generated (never hand-edit); `<style scoped>` for template-authored markup; **`:deep()`/global sheet is required to style `v-html`'d IR content** (the spike's load-bearing finding — 23.3 depends on it); the measured `<NuxtIsland>` payload recommendation from Task 4; AD-7 boundary (SpecScribe owns semantic tokens, host owns chrome).
- [x] **Task 6 — Nuxt `/design-system` route** (AC: #3, #6)
  - [x] `web/pages/design-system.vue` renders every primitive in every relevant status/motion state, using `PageShell.vue`. This is the worked example for the conventions doc and the future portal design-system surface.
  - [x] Verify live in a browser (`npm run dev` and/or `nuxt generate` + serve): tokens resolve (`var(--status-done)` → the moss value), reduced-motion honored, status readable by name with JS off.
- [x] **Task 7 — C#-generated `design-system.html`, wired into Help nav** (AC: #6)
  - [x] Add `DesignSystemTemplater.cs` (model on `HowToReadTemplater.cs` / `AboutSddTemplater.cs`): renders the token families and primitive gallery as static, JS-optional HTML using the existing `specscribe.css` classes. Status by name, non-color (UX-DR17). Reduced-motion respected (the motion tokens are already neutralized by the sheet's reduce block).
  - [x] Add `SiteNav.DesignSystemOutputPath = "design-system.html"` and register it in the **Help** group and Help quick-links in `SiteNav.Build` (see the `help.Add(...)` / `quickLinks.Add(...)` block).
  - [x] Add `WriteDesignSystem(nav)` to `SiteGenerator`, called from the always-written page block beside `WriteHowToRead(nav)` (~line 460), going through `WriteOutput` (so SPA/webview `CapturePages` picks it up). Write it **directly**, without `ApplyReferenceLinks`, mirroring How-to-read/About (a token-vocabulary page must not self-expand its own terms).
  - [x] Regenerate the golden fingerprint constant (it **will** move — new page + new nav entry on every page). Confirm the hash is stable across two repeated runs before locking it (concurrent-main hazard — see Dev Notes).
- [x] **Task 8 — Tests** (AC: #1, #6)
  - [x] C#: a `DesignSystemTemplaterTests` / `SiteGeneratorDesignSystemTests` (model on `SiteGeneratorHowToReadTests.cs`) asserting the page renders, carries the token/primitive content, and is JS-optional; a nav coherence assertion that the Help entry resolves; RenderParity coverage (`RenderParityTests.cs` — HTML ≡ webview capture) since the page rides `WriteOutput`.
  - [x] web/: `npm run check:tokens` passes; the design-system route prerenders without error.
  - [x] Full suite green; golden fingerprint regenerated and confirmed stable.

### Review Findings — 2026-07-26 (first pass)

_Code review 2026-07-26. Scoped to this story's File List and declared symbols — sibling stories 20.5,
20.7 and 22.2 share commit `261b300` and are excluded. Baseline `cd7f302`._

> **Reconciled 2026-07-28** against HEAD. Of the **19** open findings: **2 resolved** (both by sibling
> Story 23.5, not by a patch here) — item 1's Sonar/CI half, and the `prebuild`/`pregenerate` hook.
> **17 verified still live by symbol**, 2 of them with a changed shape (the `accent` set and `launch.json`
> — see the re-review section). The three owner decisions dated 2026-07-26 were never applied.
> Superseded by **§ Review Findings — 2026-07-28 (re-review)**.
>
> **CLOSED 2026-07-29.** Every box in this section is now ticked, and none was ticked here on its own merits —
> 16 were resolved by the re-review below, and the last (item 3, the four stage backgrounds) by the
> 2026-07-29 follow-up that released its deliberate hold. They are ticked rather than left open because an
> unchecked box reads as unfinished work to the next reader, and the supersession note above is easy to miss.
> **The re-review section below is the authoritative record for all of them.**

- [x] [Review][Patch] **`web/` joined the repo without deciding which analysis surfaces it joins** — _RESOLVED IN PART 2026-07-28: the Sonar/CI half landed with **Story 23.5** — `.github/workflows/build-test-analyze.yml` now runs a Node job off `web/.nvmrc`, sets `sonar.javascript.lcov.reportPaths="web/coverage/lcov.info"`, and carries a `web/**` exclusion set; measured `web/` coverage is ~51% statements. **The Code Map half did NOT land** and is re-raised as decision D4 below — `SpecScribeOutput/code/web/package-lock.json.html` exists on disk today._ — _Owner decision 2026-07-26: **analyze it now** — add a Node CI step + `sonar.javascript.lcov.reportPaths`, and author the `web/` test suite that makes the coverage figure real (scoped to `tokens-lib.mjs`/`check-tokens.mjs` plus component smoke tests). Code Map ingestion of `web/package-lock.json` is handled by the same change._ Two symptoms, one call. (a) `.github/workflows/build-test-analyze.yml:130` excludes `spike/**`, `tools/**`, `extension/node_modules/**` but **not** `web/**`, and sets no `sonar.javascript.lcov.reportPaths` — so the next scan pulls ~1,800 lines of untested first-party `.vue`/`.mjs`/`.ts` into Clean-as-You-Code at 0% coverage and reds the gate Story 25.1 just turned green. (b) `SiteGenerator.EnumerateCodeFiles` (`SiteGenerator.cs:4477`) feeds the Code Map from plain `git ls-files` with **no extension filter** — so all 24 new `web/**` files enter it, including the 11,132-line `web/package-lock.json`, which will dominate the Config bucket of SpecScribe's own dogfood portal. The Completion Notes' stated reason (`.vue`/`.mjs`/`.ts` "are outside the code-page extension set") is factually wrong: there is no such set. Options: exclude `web/**` from both until 23.5 packages it / analyze it now and add a Node CI step + lcov / exclude from Sonar only.
- [x] [Review][Patch] **`StatusBadge.vue` drops the icon — half of the UX-DR17 channel the component claims to enforce by shape** — _Owner decision 2026-07-26: **defer the glyph to 23.3 with the IR**, where the stage→icon mapping already has a data source. The patch here is to stop the component claiming a guarantee it does not provide: drop the "enforced BY THE COMPONENT'S SHAPE" assertion from its header and record the 23.3 dependency in CONVENTIONS.md._ `StatusStyles.Badge` (`StatusStyles.cs:356`) emits `{Icon(iconClass)}{label}` and documents the rule as "color + icon + word, never icon-only"; `web/components/StatusBadge.vue:41` renders text only, with no icon prop or slot, while its header asserts "UX-DR17 is enforced BY THE COMPONENT'S SHAPE". `ready` and `drafted` share a border colour and are distinguished by glyph in the portal. Needs a call on where the Vue glyph comes from (inline SVG sprite mirroring `Icons`, an IR-supplied glyph in 23.3, or a slot the caller fills).
- [x] [Review][Patch] **`StatusBadge.vue` re-authors four stage backgrounds, so Vue and portal badges render different tints** — _Owner decision 2026-07-26: **tokenize the four literals in `specscribe.css`**, re-run `extract:tokens`, and bind the Vue component to the new tokens so the bridge actually carries the values. Moves the golden fingerprint._ `specscribe.css:3042-3046` carries literal hexes with no token: `.done #e8f0e4`, `.active #e0ecea`, `.review #d9e6ea`, `.ready/.drafted #f5ecd4`. `StatusBadge.vue:66-89` replaces all four with `var(--parchment)` (`#f4ead5`), and flips `.is-pending`/`.is-deferred` borders from `var(--border)` to `var(--status-*)`. Token discipline is honoured — the bridge structurally *cannot* carry an untokenized literal — but the outcome is the drift Epic 23 exists to prevent, and it is recorded nowhere. Options: tokenize the four literals in `specscribe.css` and re-extract (changes the shipped portal) / accept and record the divergence / drop the stage backgrounds from the Vue primitive until 23.3.
- [x] [Review][Patch] Token extractor and drift gate are blind to every `:root` block but the first [web/scripts/tokens-lib.mjs:71] — `specscribe.css` has **three** top-level `:root` rules: line 6 (extracted), line 5403 (`--impact-lvl-1`…`-5`), line 5839 (`--nav-offset`, inside a max-width media query). `check:tokens` prints `OK — 36 tokens in sync` while two families never cross. Make the extractor detect additional top-level `:root` rules and fail loudly; whether the impact-map ramp should cross is then 23.3's explicit call rather than a silent omission.
- [x] [Review][Patch] AC #1's "build-time drift check" is not run by any build [web/package.json:12] — _RESOLVED 2026-07-28 by **Story 23.5**: `web/package.json:28-29` now carries `"prebuild"` and `"pregenerate": "node scripts/check-tokens.mjs"`, and CI runs `npm run check`. Confirmed at HEAD._ `check:tokens` exists, works, and provably catches drift, but `web/package.json` has no `prebuild`/`pregenerate` hook and `.github/workflows/build-test-analyze.yml` runs no Node step at all. Add the npm lifecycle hook so `nuxt build`/`generate` cannot proceed on drifted tokens.
- [x] [Review][Patch] Vue status vocabulary drops `unmapped` and teaches the wrong word for it [web/pages/design-system.vue:27] — `StatusStyles.LegendStages` has **ten** stages; the Vue `stages` array and `StatusStage` union (`StatusBadge.vue:16`) have nine, and the aside that stands in for the missing one states the word as *"Unmapped"* where `StatusStyles.LegendWord("unmapped")` → `RequirementLabel(Unmapped)` → **"Not yet mapped"** (`StatusStyles.cs:180`). The `ranking="Nine canonical stages"` caption (`design-system.vue:61`) is downstream of the same omission.
- [x] [Review][Patch] The Vue page names `--status-retired`, a token that does not exist [web/pages/design-system.vue:67] — the swatch caption interpolates `--status-{{ s.stage }}` unconditionally; `tokens.css` declares no `--status-retired`, and the page's own CSS concedes it (`.swatch-retired { background: var(--parchment-dark) }`). The C# sibling has exactly this guard (`DesignSystemTemplater.cs:139-144`); mirror it.
- [x] [Review][Patch] `ListRow.vue` chips drop the `pill` class and re-type its look [web/components/ListRow.vue:42] — `ListRow.Chip` (`ListRow.cs:73`) emits `class="list-row-chip pill"`, and every visual property comes from `.pill` (`specscribe.css:1099`): Courier, `letter-spacing: 0.03em`, `padding: 0.2rem 0.7rem`, `--warm-white`, `--ink-faded`. The Vue chip re-declares itself serif, no letter-spacing, `0.1rem 0.55rem`, `--parchment`, `--ink-light` — a second hand-typed definition inside a file whose header calls itself "the Vue counterpart of `ListRow.Render`".
- [x] [Review][Patch] The Vue reduced-motion block does not neutralize `animation-delay` [web/assets/base.css:54] — it sets `animation-duration`, `animation-iteration-count`, `transition-duration`, `scroll-behavior` and nothing else. `specscribe.css:6449` handles precisely this case with `animation: none`, and its comment explains why: `fill-mode: both` holds an element invisible through its delay. A 200-row list staggered by `--motion-stagger` would appear over ~8s of blank page for a reduce-motion reader. CONVENTIONS.md §6 forbids a per-SFC fix, so there is no second place to catch it.
- [x] [Review][Patch] The C# page renders Retired with the `deferred` badge class, not the real one [src/SpecScribe/DesignSystemTemplater.cs:175] — `BadgeBody`/`StatusBody` remap `retired => deferred` and pass the remapped class into `StatusStyles.Badge`, emitting `class="status-badge deferred"`. No real caller does that (`StatusStyles.LegendKey:391` remaps only `unmapped`), and `.status-badge.retired` is its own rule (`specscribe.css:3052`). Byte-identical to `.deferred` today, so nothing looks wrong — but the class doc's load-bearing claim is "built from the ACTUAL primitives, never look-alike markup". Keep the swatch remap; pass `stage` as the badge class.
- [x] [Review][Patch] The `Window` slot is filled with prose and component filenames on the page that teaches the frame [src/SpecScribe/DesignSystemTemplater.cs:104] — `Charts.cs:139` documents the slot as "the ONE place a **numeric analysis window** is rendered"; the C# page passes `"the panel you are reading"` and `design-system.vue:99,109,137,150` pass `"StatusBadge.vue"`, `"ListRow.vue"`, `"ChartPanel.vue"`. A 23.3 author copies the pattern from the page whose stated job is to teach it.
- [x] [Review][Patch] `ListRow.vue` defines an `accent-review` modifier the portal has no counterpart for [web/components/ListRow.vue:16] — `specscribe.css:6970-6972` defines exactly three accents (`done`, `pending`, `deferred`); the comment at 6965 explains the neutral default is deliberate. A fourth accent is a design-system change, not a port.
- [x] [Review][Patch] Two new tests are satisfied by construction and cannot fail [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:79] — (a) `Assert.Contains($"--status-{stage}", html)` asserts a string the templater derives from the same variable, so adding an eleventh stage to `LegendStages` with no matching token in `:root` ships a blank swatch documenting a nonexistent token, green. Assert against the tokens declared in the generated stylesheet instead. (b) `DesignSystem_BypassesApplyReferenceLinks` (line 189) asserts absence of `<abbr`/`ref-chip`, but `AbbreviationExpander` only wraps `FR/NFR/AC/ADR/PRD` and `ReferenceChipRenderer` needs `[[wiki]]`/`file:line` — none appear on this page, so the guard passes with or without the bypass. Assert the write path directly instead.
- [x] [Review][Patch] Token-bridge guard and diagnostics have three gaps [web/scripts/tokens-lib.mjs:30] — (a) `REQUIRED_TOKENS` omits five tokens the shipped components actually bind to: `--parchment-dark`, `--ink-faded`, `--gold`, `--moss`, `--rust-light`, so a rename of any of them passes the anti-latch guard and silently blanks badge and panel styling. (b) `check-tokens.mjs:37` calls `tokenMap(actual)` outside any try/catch, so a committed `tokens.css` whose `:root {` header was removed throws an uncaught stack trace naming the *source* file as the culprit. (c) `tokenMap`'s regex requires a trailing `;`, which CSS permits the final declaration to omit — such a drift falls into the `=== 0` branch and is misreported as "token values identical — the difference is comments".
- [x] [Review][Patch] `ChartPanel.vue` grows frame anatomy `Charts.Framed` does not have [web/components/ChartPanel.vue:29] — `<section class="chart-panel">` where `Charts.Framed` (`Charts.cs:167`) emits `<div>`, plus a `.chart-panel-body` wrapper with no rule in the SFC and no counterpart in `specscribe.css`, and a `.chart-panel-legend` slot the C# frame has no concept of. The header claims "a panel authored in Vue cannot grow a different anatomy from one authored in C#"; any `.chart-panel > …` rule stops matching once a surface migrates.
- [x] [Review][Patch] The two design-system pages already disagree on the motion vocabulary [src/SpecScribe/DesignSystemTemplater.cs:35] — all five role sentences are hand-typed twice and all five differ (`"Hover and opacity changes — the shortest deliberate movement on the page."` vs `design-system.vue:41` `"Hover and opacity feel — the shortest deliberate change."`). The duplication is owner-accepted until 23.4; divergence on day one is not. Make the Vue copy verbatim.
- [x] [Review][Patch] `measure:payload` charges the whole shared island directory to variant B and reports a missing route as free [web/scripts/measure-payload.mjs:39] — `island = v.route.endsWith('island') ? islandBytes : 0` sums every file under `__nuxt_island/`, which the script's own comment notes is keyed by component+props hash and shared. Add one `.server.vue` anywhere in `web/` and the published 1.99× ratio moves without variant B changing. Separately, every size lookup ends `?? 0` and the only guard is `rows.every(r => r.total === 0)`, so a single absent route prints `0.00x` — reading as "this shape is free", inverting AC #4's conclusion.
- [x] [Review][Patch] `ListRow.vue` keys chips by their own text [web/components/ListRow.vue:42] — `:key="chip"` over a `string[]` with no uniqueness constraint; `:chips="['3 tasks', '3 tasks']"` produces duplicate keys and node reuse on reorder.
- [x] [Review][Patch] `.claude/launch.json` now carries eight preview servers, four pointing at the same directory [.claude/launch.json:65] — `specscribe-output` (8099), `related-work-20-3` (8094), `specscribe-output-review` (8097) and the new `design-system-23-2` (8102) all serve `SpecScribeOutput`. Per-story naming guarantees the list only grows, in a file every session shares.
- [x] [Review][Defer] `WriteTextWithRetry` is undeclared sibling work with two defects of its own [src/SpecScribe/SiteGenerator.cs:4439] — deferred, pre-existing. The File List declares only `WriteDesignSystem` + call site, but the diff also adds `WriteTextWithRetry` and converts `index.html`'s write, annotated `[Story 20.5 owner round]`. Its defects belong to 20.5, not here: (a) its doc claims parity with `CopyEmbeddedAsset`, but it omits the tmp-file + atomic `File.Move` half that a Story 5.3 review-fix added precisely because truncate-then-fail leaves a corrupt file; (b) the catch filter makes no transient/permanent distinction, so a read-only target burns four attempts and still destroys the previous good page; (c) only one of the generator's write sites was converted — `WriteOutput` (`SiteGenerator.cs:3017`), which every other page including the new `design-system.html` rides, still calls bare `File.WriteAllText`.

### Review Findings — 2026-07-28 (re-review)

_Three-layer adversarial pass (Blind Hunter, Edge Case Hunter, Acceptance Auditor) plus a reconciliation of
every 2026-07-26 finding against HEAD. Scoped to this story's File List and declared symbols per CLAUDE.md —
**sibling work excluded**: 20.5/20.7/22.2 (shared commit `261b300`), and 23.3 (`ir/`, `surfaces/`,
`[...path].vue`, `ir-content*`, `IrHtml.ts`/`IrMain.ts`), 23.5 (packaging, `.nvmrc`, `sync-runtime-assets`,
`build-package`, CI/Node/vitest wiring, the Nuxt 4 bump), 18.4/18.5 (`IdeasOutputPath`,
`TestArtifactsOutputPath`, Module Coverage) and 8.9 (`RetirementStatusWords`, `StoryStages`) all landed in
`web/` and the shared C# files afterwards. Baseline `cd7f302`; every finding re-verified by symbol against the
working tree, not by patch line number._

**Decisions needed**

- [x] [Review][Decision] **RESOLVED 2026-07-28 — re-measured under Nuxt 4.5.1 and the write-up corrected.** `measure-payload.mjs` now prints and COMMITS its caveats (`measurements/payload.json|txt`), stating that `_nuxt/` bytes are uncounted and that variant C measures "no data had to cross the boundary" rather than "build-time data is free". CONVENTIONS §4 gained a *What variant C does and does not prove* subsection and restates the 23.3 recommendation as **build-time data + `noScripts: true`** — structural rather than measured-and-hoped — while keeping the durable A-vs-B ordering. Fresh numbers: **1.37× / 2.00× / 1.00×** (was 1.36/1.99/1.00 on Nuxt 3.21.9), so the conclusion survived the major version. Original finding: **AC #4's control is not a data path, so the recommendation handed to 23.3 was never actually measured** — this is the most consequential finding of the pass, and it undercuts the story's own headline. `pages/measure/static.vue:10` resolves `buildRows(200)` at module scope, and at the measurement commit `261b300` `nuxt.config.ts` had **no `routeRules` at all** — so variant C hydrated exactly like A and B and **re-executed `buildRows` in the browser** from `utils/measure-rows.ts`, a 14-line deterministic generator bundled into `_nuxt/`. Its 0.1 KB payload therefore measures *"this data is a pure function, so nothing had to cross the boundary"*, not *"build-time resolution is free"*. Compounding it, `measure-payload.mjs:48` computes `total = html + payload + island` and never counts `_nuxt/` chunks, so the bytes variant C moved into the client bundle are structurally invisible to the table. Real IR content is not a generator: the shape 23.3 actually needed was a `#ir` Vite-environment resolver + a throwing browser stub + `routeRules: { '/**': { noScripts: true } }` — machinery this measurement neither used nor implied. The A-vs-B half stands and its finding (islands amplify) is real and well-evidenced; it is the **control** and the CONVENTIONS.md:109 recommendation built on it that do not hold. Options: re-run AC #4 with a control that carries non-generable data and a metric that counts `_nuxt/` / re-word CONVENTIONS §4 to state what was actually measured and credit 23.3's shipped shape as the real answer / accept and annotate, since 23.3 landed on a working shape regardless.
- [x] [Review][Decision] **RESOLVED 2026-07-28** — re-run, re-recorded and version-stamped; `measure-payload.mjs` now writes `measurements/payload.json` + `.txt` like every other harness, and CONVENTIONS links them. Original finding: **AC #4's published table is no longer reproducible, and it is the one harness whose output was never committed** — `web/CONVENTIONS.md:94` pins the numbers to "Nuxt 3.21.9 / Vue 3.5.40 / Node 24.11.1"; `web/package.json` now pins `^4.5.1` and `web/node_modules/nuxt` is at **4.5.1** — a major version across which `<NuxtIsland>` payload emission is precisely the thing that changed — and `/measure/**` gained a `routeRules` entry it did not have when measured. Separately `web/README.md:49` states harness output "is committed under `measurements/`" and `web/.gitignore` explains why ("Story 23.1 claimed reproducible numbers and wasn't"), yet `web/measurements/` holds `a11y`, `links`, `parity` and `two-ir` records and **no payload record** — `measure-payload.mjs` writes to stdout only and has no file-output path at all. The Nuxt bump is 23.5's; the stale, uncheckable claim is this story's. Options: re-run and re-record under Nuxt 4 (may move 1.99×/1.36×) / annotate the table with its real provenance and add the `measurements/payload.*` writer / drop the version pin claim.
- [x] [Review][Decision] ✅ **RESOLVED 2026-07-29 — the hold is released and the owner's 2026-07-26 decision is applied, widened to the mirrors.** The blocker had cleared: the concurrent Epic 18 pass landed (HEAD `630ae25`, working tree clean of `src/`), so the stylesheet was no longer under another session's regeneration. Four new `:root` tokens — `--status-done-bg`, `--status-active-bg`, `--status-review-bg`, `--status-ready-bg` (`ready` and `drafted` deliberately share one, as the stylesheet pairs them) — now carry the fills, and **the owner chose to include the documented mirrors** rather than only the four badge rules: three `.epic-status.*`, `.status-badge.evidence-pill.tests-pass`, and `.sprint-flag` (which already bound `--status-review` for its border and held only the fill as a literal). The comment at `.epic-status.review` had *said* "mirrors `.status-badge.review`" while being enforced by nothing but two matching hexes; it is now enforced by one token with two consumers. `StatusBadge.vue` binds all four, so the flat `var(--parchment)` substitution is gone. **Verified live**, portal vs Nuxt, all eight stages identical: done `rgb(232,240,228)`, active `rgb(224,236,234)`, review `rgb(217,230,234)`, ready/drafted `rgb(245,236,212)`, pending/deferred/retired `rgb(232,213,176)` — previously five of those were one flat parchment on the Vue side. `.epic-status` mirrors on `/epics.html` match the badges exactly, with no visual change from before (same values, one declaration). New STRUCTURAL guard `StylesheetTests.Stylesheet_StageFillsOnBadgeFamilies_AreTokensNotInlineLiterals` fails on ANY hex background under `.status-badge.*`/`.epic-status.*` rather than checking a remembered list of four — **proven red** by reintroducing one literal, which reported `.status-badge.active => background: #e0ecea`. `REQUIRED_TOKENS` extended (45 tokens across 2 `:root` blocks, was 41). Fingerprint `501ee958…` → `22c921de…`, stable across two runs each preceded by `--no-incremental`; provenance CLEAN (see below). _Original finding:_ ⛔ **HELD 2026-07-28 — deliberately NOT applied, and this is the one item the re-review declined to touch.** The fix edits `specscribe.css`, which is embedded in the golden fingerprint, **while a concurrent session is mid-regeneration on that exact file** (its uncommitted Epic 18 pass, +12 lines, had already been locked to `9544578b…` with a provenance comment claiming "this regeneration reflects THIS change alone"). Stacking a second stylesheet change on top would invalidate their verified hash and their record. Re-raise once that work lands. Original finding: **`StatusBadge.vue` re-authors four stage backgrounds — owner-decided 2026-07-26, never applied, and applying it now moves the golden fingerprint** — `specscribe.css:3177-3181` still carries untokenized literals (`.done #e8f0e4`, `.active #e0ecea`, `.review #d9e6ea`, `.ready/.drafted #f5ecd4`); `StatusBadge.vue:66-89` still substitutes `var(--parchment)` for all four. The recorded decision was *"tokenize the four literals in `specscribe.css`, re-run `extract:tokens`, bind the Vue component to the new tokens"* — which changes the shipped portal's stylesheet and therefore `GoldenContentFingerprint`, under the concurrent-main hazard CLAUDE.md warns about (confirm stable across two repeated runs, record whose uncommitted work the regeneration sat on). Re-confirming the timing is the decision: apply now / apply with 23.4's stylesheet work / accept and record the divergence in CONVENTIONS.md.
- [x] [Review][Decision] **RESOLVED 2026-07-28 in its narrowest form** — `SiteGenerator.IsGeneratedLockFile` now excludes dependency lockfiles (`*-lock.json`, `*.lock`, `pnpm-lock.yaml`, `packages.lock.json`, `composer.lock`) from `EnumerateCodeFiles`. Chosen over the `web/**` and extension-allowlist options because a lockfile is machine-written inventory rather than source **for every documented repo**, not just this one. Verified: `code-map.html` now contains **zero** `package-lock` occurrences (was dominating the Config bucket). ⚠️ **Narrower than the finding implied**: `code/web/package-lock.json.html` still exists, because code PAGES come from `_codeReferenced` (the git-analytics churn sets), not `_codeFiles` — a genuinely different path, and arguably legitimate since the lockfile is a real churn hotspot. Original finding: **The Code Map half of the 2026-07-26 owner decision never landed** — `SiteGenerator.EnumerateCodeFiles` (`SiteGenerator.cs:5116`) still feeds the Code Map from `GitMetrics.TryListFiles` (plain `git ls-files`) with **no extension filter**, only a binary/unreadable skip. Demonstrated rather than argued: `SpecScribeOutput/code/web/package-lock.json.html` exists on disk, so the **11,291-line lockfile has its own code page** and dominates the Config bucket of SpecScribe's own dogfood portal, alongside 11 other `web/**` entries. Note this is a product-behaviour change, not a `web/`-local one — any filter added here affects every consumer's portal. Options: exclude lockfiles generally (`*-lock.json`, `*.lock`) / exclude `web/**` until 23.4 / add a code-page extension allowlist (the thing the Completion Notes wrongly assumed already existed) / accept.
- [x] [Review][Decision] **RESOLVED 2026-07-28** — `<section>` → `<div>` (matching `Charts.Framed`) and the unguarded `.chart-panel-body` wrapper deleted outright, which also fixes the empty-padded-div case. The `.chart-panel-legend` slot is KEPT: it is additive, always `v-if`-guarded, and cannot change the anatomy of a panel that does not use it — with a comment requiring `Charts.Framed` to gain the slot first if a legend ever needs shared-sheet styling. Verified live: panel tag `DIV`, `.chart-panel-body` count **0**. Original finding: **`ChartPanel.vue` grows frame anatomy `Charts.Framed` does not have** [web/components/ChartPanel.vue:29] — `<section class="chart-panel">` where `Charts.Framed` (`Charts.cs:168`) emits `<div>`; plus a `.chart-panel-body` wrapper and a `.chart-panel-legend` slot, **neither of which appears anywhere in `specscribe.css`** and neither of which the C# frame has any concept of. The SFC header asserts "a panel authored in Vue cannot grow a different anatomy from one authored in C#", which is already untrue. Latent today (no `.chart-panel > …` child-combinator rule exists), structural once 23.4 migrates surfaces — and `ir-content.css` is extracted from that same monolith, so it is the wrapper hazard CONVENTIONS §9 already documents for `IrHtml.ts`. The `<section>`→`<div>` and body-wrapper fixes are unambiguous; **the legend slot is the real question** — drop it, or add a legend slot to `Charts.Framed` so both frames agree.

**Patches**

- [x] [Review][Patch] Token bridge is blind to every `:root` block but the first — five live tokens silently dropped while the gate reports "in sync" [web/scripts/tokens-lib.mjs:71] — `css.search(/(^|\n):root\s*\{/)` returns the first match only. `specscribe.css` has a **second** top-level `:root` at line 5533 (`--impact-lvl-1`…`-5`, the Impact Map ramp) plus a media-query one at 5976 (`--nav-offset`). Because `check-tokens.mjs` runs the *same* one-block extractor on both sides, the two can never disagree about a token neither looks at — `check:tokens` prints "OK — 36 tokens in sync" forever and `REQUIRED_TOKENS` cannot catch it either, since it only asserts names from block one. AC #1's mechanism fails **open**. Detect additional top-level `:root` rules and fail loudly; whether the impact ramp should cross is then an explicit call rather than a silent omission. _(Raised independently by all three layers; prior finding 4.)_
- [x] [Review][Patch] `REQUIRED_TOKENS` omits five tokens the shipped primitives actually bind to [web/scripts/tokens-lib.mjs:30] — missing `--parchment-dark`, `--ink-faded`, `--moss`, `--gold`, `--rust-light`, all consumed by `StatusBadge.vue:60,66-107` and `ChartPanel.vue:87,99`. Rename any one of them in `specscribe.css` and `renderTokensCss` throws nothing, `extract:tokens` regenerates cleanly, `check:tokens` goes green — and four of nine badge stages lose their background to invalid-at-computed-value-time, rendering as transparent chips on a cream page. The guard covers the extraction target but not the consumption surface. _(Prior finding 14a.)_
- [x] [Review][Patch] `check:tokens` crashes with a stack trace blaming the C# stylesheet when the committed `tokens.css` is corrupt [web/scripts/check-tokens.mjs:36] — an empty or truncated `tokens.css` returns `''` not `null`, so the friendly `:22-26` branch is skipped; `tokenMap(actual)` then throws `no top-level ':root {' rule found in src/SpecScribe/assets/specscribe.css` **uncaught**, naming the source file for a fault in the generated one. The operator is sent to debug the wrong file, in exactly the scenario the gate exists to catch. _(Prior finding 14b.)_
- [x] [Review][Patch] `tokenMap`'s regex requires a trailing `;`, and duplicate names resolve last-wins [web/scripts/check-tokens.mjs:64] — `/(^|[\s;{])(--[A-Za-z0-9-]+)\s*:([^;]*);/g`. CSS permits the final declaration to omit its semicolon (today `--motion-stagger: 0.04s;` at `tokens.css:87`); drop it and change the value and `added`/`removed`/`changed` all come back empty, so a real status/motion value drift prints the `:51-56` fallback — *"token values identical — the difference is comments"*. The same misreport occurs when a property is declared twice and only the first copy drifts. _(Prior finding 14c.)_
- [x] [Review][Patch] The block *finder* is comment-blind while the block *scanner* is comment-aware [web/scripts/tokens-lib.mjs:71] — the depth scan tracks comment state precisely because "a future comment containing a brace would silently truncate the copy", but `css.search()` does not, so a comment whose text begins a line with `:root {` sets `braceAt` inside the comment and slices from the wrong offset. Given this repo's `*/`-in-a-custom-property truncation history, apply the same structural reasoning to locating the block.
- [x] [Review][Patch] **`StatusBadge.vue` asserts a UX-DR17 guarantee it does not provide, in two ways** [web/components/StatusBadge.vue:6] — owner-decided 2026-07-26 (*"drop the assertion from its header and record the 23.3 dependency in CONVENTIONS.md"*); **neither half landed**. (a) The header still reads "UX-DR17 is enforced BY THE COMPONENT'S SHAPE" while the template renders `{{ label }}` with no icon prop or slot, where `StatusStyles.Badge` emits `{Icon(iconClass)}{label}` and documents the rule as "color + icon + word". `.is-retired` and `.is-deferred` are byte-identical rule sets, so in Vue those two badges are pixel-identical apart from the word. (b) "`label` is a required prop, so a badge cannot be rendered as colour alone" — required-ness guards `undefined`, not `''`; `<StatusBadge stage="done" label="" />` is an empty coloured pill. CONVENTIONS.md §5 records no 23.3 glyph dependency either. _(Prior finding 2.)_
- [x] [Review][Patch] The Vue status vocabulary is nine stages where the portal has ten, and teaches the wrong word for the missing one [web/pages/design-system.vue:27] — `StatusStyles.LegendStages` (`StatusStyles.cs:412`) is ten: `pending, drafted, ready, active, review, done, deferred, unmapped, retired, unrecognized`. The `stages` array and the `StatusStage` union (`StatusBadge.vue:16`) carry nine, omitting `unmapped`; the aside standing in for it (`:75`) states the word as *"Unmapped"* where `StatusStyles.LegendWord("unmapped")` → `RequirementLabel(Unmapped)` → **"Not yet mapped"** (`StatusStyles.cs:243`). The `ranking="Nine canonical stages"` caption (`:61`) is downstream of the same omission. Consequence for 23.3: `<StatusBadge stage="unmapped">` is a type error, and the only legal substitute is `stage="pending"` — exactly the collapse `StatusStyles.Badge`'s three-arg overload exists to prevent. _(Prior finding 6.)_
- [x] [Review][Patch] The Vue page publishes `--status-retired`, a token declared nowhere [web/pages/design-system.vue:67] — the swatch caption interpolates `--status-{{ s.stage }}` unconditionally; neither `tokens.css` nor `specscribe.css` declares it, and the page's own CSS concedes the point at `:210`. A component author who follows the instruction gets an unstyled element, from the page whose entire job is to be authoritative about the vocabulary. The C# twin has exactly this guard (`DesignSystemTemplater.cs:153-158` → "shares `--status-deferred`"); mirror it, and add the same arm for `unmapped` when it is added above. _(Prior finding 7.)_
- [x] [Review][Patch] The two design-system pages teach two different colours for Retired, and the Vue one is wrong [web/pages/design-system.vue:210] — `.swatch-retired { background: var(--parchment-dark) }` = `#e8d5b0`; the portal's own rule is `specscribe.css:5726` `.status-legend-key-swatch.retired { background: var(--status-deferred) }` = `#7a6250`, which is what `design-system.html` renders. Two consequences: a reader who learned Retired = grey-brown on the portal sees pale tan on the Nuxt page, and `#e8d5b0` differs from `--status-drafted` `#e8d9a8` by 4/255 in one channel — on the page whose subject *is* the colour vocabulary, Retired and Drafted are visually the same swatch.
- [x] [Review][Patch] An out-of-vocabulary `stage` or `accent` renders silently as the pending look [web/components/StatusBadge.vue:41] — TypeScript unions erase at runtime, so a stage outside the union (`unmapped` today, any IR-passthrough status tomorrow) matches no `.is-*` rule and falls to the `.status-badge` base, which the component's own comment at `:59` documents as "the pending/deferred reading". `ListRow.vue:37`'s `accent-${accent}` behaves the same way. No unknown arm, no dev-time warning.
- [x] [Review][Patch] `ListRow.vue` chips drop the `pill` class and re-type its look [web/components/ListRow.vue:110] — `ListRow.Chip` (`ListRow.cs:73`) emits `class="list-row-chip pill"` and every visual property comes from `.pill` (`specscribe.css:1234`): Courier, `letter-spacing: 0.03em`, `padding: 0.2rem 0.7rem`, `--warm-white`, `--ink-faded`. The Vue chip re-declares itself serif, no letter-spacing, `0.1rem 0.55rem`, `--parchment`, `--ink-light` — a second hand-typed definition inside a file whose header calls itself "the Vue counterpart of `ListRow.Render`". _(Prior finding 8.)_
- [x] [Review][Patch] `ListRow.vue`'s accent set diverges from the portal in **both** directions [web/components/ListRow.vue:75] — the Vue modifiers are `done | pending | deferred | review`; `specscribe.css:7076-7082` defines `done`, `pending`, `deferred`, **`ready`**. `accent-review` is a design-system addition with no counterpart; `accent-ready` is a port that was missed. _(Prior finding 12, which has since widened — `.list-row-accent-ready` was added to the portal after the first review.)_
- [x] [Review][Patch] `ListRow.vue` has three input-robustness gaps [web/components/ListRow.vue:40] — (a) `withDefaults` substitutes only for `undefined`, so an explicit `chips: null` (the shape a JSON IR emits for an absent array) throws `Cannot read properties of null` at `chips.length` and fails the **whole route's** SSR, not just the row; (b) `:key="chip"` over a `string[]` with no uniqueness constraint gives duplicate keys and node reuse on reorder for `:chips="['3 tasks', '3 tasks']"`; (c) `primaryLabel ?? 'Open'` passes `''` through, so `primary-label=""` renders an anchor whose accessible name is the bare `→`. Not reachable from today's call sites — all seven are internal and well-formed — but this is a primitive published for 23.3 to feed from the IR.
- [x] [Review][Patch] `ChartPanel.vue` renders an empty body wrapper when the default slot is unfilled [web/components/ChartPanel.vue:38] — every other optional region is guarded (`v-if="window"`, `v-if="ranking"`, `v-if="note"`, `v-if="$slots.legend"`, `v-if="why"`); the body alone is not, so `<ChartPanel title="Coverage" />` ships `<div class="chart-panel-body"></div>` with the panel's padding. Both design-system pages state the opposite as a contract (`design-system.vue:143` "Slots that are not filled render nothing at all"; `DesignSystemTemplater.cs:241` the same).
- [x] [Review][Patch] The Vue reduced-motion block neutralizes duration but not delay [web/assets/base.css:62] — it sets `animation-duration`, `animation-iteration-count`, `transition-duration`, `scroll-behavior` and nothing else. `specscribe.css:6559` handles precisely this with `animation: none`, and its comment explains why: `fill-mode: both` holds an element at its `from` keyframe — `opacity: 0` — through the full delay. Both design-system pages publish `--motion-stagger` as the supported per-item delay unit and `PageShell.vue:132` establishes `both` as the house pattern, so a reduce-motion reader would get staggered **blank** content rather than no motion. CONVENTIONS.md §6 forbids a per-SFC fix, so there is no second place to catch it. _(Prior finding 9.)_
- [x] [Review][Patch] `measure:payload` charges the whole shared island directory to variant B [web/scripts/measure-payload.mjs:47] — `island = v.route.endsWith('island') ? islandBytes : 0` sums every file under `__nuxt_island/`, which the script's own comment at `:36` identifies as keyed by component+props hash and **shared**, then guards with a route-name string match rather than per-component attribution. Add one `.server.vue` anywhere in `web/` and the published 1.99× moves without variant B changing. _(Prior finding 17.)_
- [x] [Review][Patch] `measure:payload` reports a missing route as `0.00x` — i.e. as the best variant — and exits 0 [web/scripts/measure-payload.mjs:44] — every lookup ends `?? 0` and the only guard is `rows.every(r => r.total === 0)`, which does not fire when the *other* rows are non-zero. A prerender failure on one route prints "this shape ships nothing", inverting AC #4's conclusion, with a zero exit code. Separately, a missing control alone makes every ratio print `—` and still exits 0.
- [x] [Review][Patch] The C# page renders Retired with the `deferred` badge class, which no real caller does [src/SpecScribe/DesignSystemTemplater.cs:189] — `BadgeBody` and `StatusBody` remap `retired => deferred` and pass the **remapped** class into `StatusStyles.Badge`, emitting `class="status-badge deferred"` (confirmed in the generated HTML). `StatusStyles.LegendKey` remaps only `unmapped`, and `.status-badge.retired` is its own rule (`specscribe.css:3187`). Byte-identical to `.deferred` today, so nothing looks wrong — but the class doc's load-bearing claim is "built from the ACTUAL primitives, never look-alike markup". Keep the swatch remap; pass `stage` as the badge class. _(Prior finding 10, narrowed: the icon/tooltip half is already correct — the three-arg overload passes `stage` as `iconClass`.)_
- [x] [Review][Patch] The templater's own comment claims a distinction the page does not render [src/SpecScribe/DesignSystemTemplater.cs:151] — "Both stay distinct by word and icon" is true for `unmapped` (which has its own glyph) and **false for `retired`**: `Icons.ForStatus("retired")` (`Icons.cs:30`) and `"deferred"` (`Icons.cs:24`) emit byte-identical SVG. Retired differs from Deferred by word alone. Still UX-DR17-compliant; the claim is not.
- [x] [Review][Patch] The `Window` slot is filled with prose and component filenames on the page that teaches the frame [src/SpecScribe/DesignSystemTemplater.cs:99] — `Charts.cs:139` documents the slot as "the ONE place a **numeric analysis window** is rendered"; the C# page passes `"the panel you are reading"` and `design-system.vue:99,109,137,150` pass `"StatusBadge.vue"`, `"ListRow.vue"`, `"ChartPanel.vue"`, `"the :deep() convention"`. A 23.3 author copies the pattern from the page whose stated job is to teach it. _(Prior finding 11.)_
- [x] [Review][Patch] The two design-system pages disagree on the motion vocabulary on day one [src/SpecScribe/DesignSystemTemplater.cs:35] — all five role sentences are hand-typed twice and all five differ (`"Hover and opacity changes — the shortest deliberate movement on the page."` vs `design-system.vue:41` `"Hover and opacity feel — the shortest deliberate change."`). The duplication is owner-accepted until 23.4; divergence is not. Make the Vue copy verbatim. _(Prior finding 16.)_
- [x] [Review][Patch] The motion family is hand-authored in three places and verified against none [src/SpecScribe/DesignSystemTemplater.cs:33] — the **status** half of the page derives from `StatusStyles.LegendStages` and so cannot fall behind; `MotionTokens` is a literal five-element array, duplicated in `design-system.vue:40` and again in `REQUIRED_TOKENS`. Add `--motion-exit` to `specscribe.css` and `check:tokens` stays green, both design-system pages silently omit it, and `DesignSystem_DocumentsTheMotionTokenFamily` passes because it asserts a hand-typed list against a hand-typed list. Derive the test from `MotionTokens`, and assert each name is actually declared in the generated stylesheet.
- [x] [Review][Patch] Four of the new tests are satisfied by construction, and the one assertion that matters is absent [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:79] — (a) `Assert.Contains($"--status-{stage}", html)` asserts a string the templater derives from the same loop variable, so an eleventh `LegendStages` entry with no matching `:root` token ships a blank swatch documenting a nonexistent token, green. (b) `DesignSystem_BypassesApplyReferenceLinks` (`:189`) asserts absence of `<abbr`/`ref-chip`, but `AbbreviationExpander` only wraps FR/NFR/AC/ADR/PRD and `ReferenceChipRenderer` needs `[[wiki]]`/`file:line` — none appear on this page, so it passes with or without the bypass; assert the write path directly. (c) `DesignSystem_AndTheLegendKey_ShareOneStageWordSeam` (`:138`) asserts `LegendWord(stage)` appears in `LegendKey()`, which computes its word by calling `LegendWord(stage)` — a tautology after the 23.2 extraction left only one seam. (d) `DesignSystem_NeverStatesATokenValueAsALiteral` (`:154`) guards six palette hexes but not `#d4a017`/`#1e4a5a`/`#e8ecf0`, and couples the test to those values so an unrelated palette change breaks it and the fix is to hand-retype the hex — growing the second copy of the palette the test exists to forbid. **Nothing anywhere asserts that a `--status-*` name the page prints actually exists in the stylesheet** — the one direction a component author depends on. _(Prior finding 13, extended.)_
- [x] [Review][Patch] `PageShell.vue`'s skip link positions against the document, not the viewport [web/components/PageShell.vue:81] — `.skip-link { position: absolute; top: 0 }` with `.shell` setting no `position`, so the offsets resolve against the initial containing block. A keyboard user who has scrolled down and shift-tabs back out of the content focuses a link rendered at the **top of the document** — invisible, with `:focus-visible` drawing an outline nobody can see. `position: fixed` is the standard fix.
- [x] [Review][Patch] `.claude/launch.json` has a port collision and 20 entries [.claude/launch.json] — `jsoff-20-7` and `tea-coverage-18-5` both claim **8109**; four entries (`specscribe-output`, `related-work-20-3`, `specscribe-output-review`, `design-system-23-2`) serve the same `SpecScribeOutput` directory. The collision is the patch; the per-story naming convention that guarantees the list only grows, in a file every session shares, is the standing problem. _(Prior finding 18, now worse — the 18.6 review fixed a different 8114 collision on 2026-07-28.)_
- [x] [Review][Patch] Two Completion Notes claims are inaccurate — (a) **false**: "`.vue`/`.mjs`/`.ts` are outside the code-page extension set, which is existing behaviour" (story line 322). There is no extension set; `EnumerateCodeFiles` has no extension filter, and `SpecScribeOutput/code/web/` contains `app.vue.html`, `nuxt.config.ts.html`, `package-lock.json.html` and nine more. The stated "not done, deliberately" was a wrong belief, not a decision. (b) **overstated**: "all ten stages resolve to real token colours" (line 310) — two of the ten have no token of their own; `unmapped` borrows `--status-pending` and `retired` borrows `--status-deferred`. The page says so; the verification note does not.
- [x] [Review][Patch] AC #2's "CSS-module conventions" wording is never addressed [web/CONVENTIONS.md] — §2 covers scoped SFCs only. Defensible (the app uses no CSS modules) but unaddressed rather than deliberately declined; one line declining them and saying why closes the AC.

**New decisions raised while applying the patches (2)** — both are cases where the finding was real but the
fix is not available inside this story's scope, so they are recorded rather than papered over:

- [x] [Review][Decision] ✅ **RESOLVED 2026-07-29 — the channel was built, and the copy is DELETED rather than corrected again.** Owner chose *"extend the `ir-content` extraction to emit shared primitive classes unscoped as a second layer"* over accepting the copy until 23.4 and over inverting the source of truth. **Proposed [ADR 0029](../../docs/adrs/0029-unscoped-shared-primitive-layer.md)** without being asked, per CLAUDE.md — this amends **ADR 0018's property 3 (Scoped)**, a ratified cross-cutting contract, so burying it in a story note was not an option. Shape: a new generated `web/assets/shared-primitives.css`, emitted **UNSCOPED**, bounded by an explicit `SHARED_PRIMITIVES` allowlist (one entry, `pill`) rather than by usage — a **tighter** bound than the scoped layer's, and all-or-nothing per selector, so `.pill.status-draft` and `.pill.pill-link` correctly stay scoped. A shared rule is **removed** from `ir-content.css`, never duplicated, so the app went from two definitions of `.pill` (one generated, one hand-typed) to **one**; the manifest records the handoff from *both* sides (`carried: false` + reason in `rules`, `unscoped: true` in the new `sharedPrimitives` block) so the rule cannot drop off the list 23.4 retires. `ListRow.vue`'s re-typed block is gone, leaving only `flex-shrink` — layout, which is the component's business, not `.pill`'s. **Verified live on both surfaces.** Template-authored: the `/design-system` chip carries a `data-v-*` attribute (so the scoped layer could never have reached it) and computes Courier New / `0.3456px` / `3.2px 11.2px` / `999px` / `rgb(250,247,242)` / `rgb(90,69,53)` — `.pill`'s own values, served from `shared-primitives.css`. Injected: `/epics/story-1-2.html` carries 3 pills inside `.ir-content` with **no** `data-v-*` and **zero** unstyled, and the sample is `pill pill-link`, which exercises both layers at once — unscoped base plus the scoped variant's `rgb(46,107,122)` correctly overriding `--ink-faded`. Computed styles are **identical to the golden portal** for that element, property for property. 351 real `.pill` elements across 108 injected pages were the regression surface. Gate extended to cover both sheets in one run and to name which drifted; **proven red twice** (hand-edited shared rule → `shared-primitives.css: ~1 changed ~ .pill`; deleted sheet → named as missing). ⚠️ **A vacuity bug in my own new tests was caught and fixed**: emptying the allowlist left the "one definition" assertions passing without executing a loop body, so a `SHARED_PRIMITIVES.length > 0` guard now precedes them — the same by-construction vacuity this whole re-review exists to find. web vitest 95 → **106**. _Original finding:_ **There is no channel for shared non-IR primitive classes, so `ListRow`'s chip is knowingly a second copy of `.pill`** — the patch made the Vue chip's values match `.pill` exactly (verified live: Courier New, 0.3456px tracking, 3.2/11.2px padding, `--warm-white`, `--ink-faded`) and restored the `pill` class to the element, so the visible drift is gone. It is **still a second definition**: `.pill` lives in the C# monolith, and the Vue app imports only `tokens.css`, `base.css` and the generated `ir-content.css` — which scopes it as `.ir-content .pill`, reaching injected markup and never a template-authored component. Dropping the properties in favour of the class alone ships an unstyled chip (confirmed before reverting). Options: extend the `ir-content` extraction to emit shared primitive classes unscoped as a second layer / accept the copy until 23.4 retires the C# renderer / hand the primitives' CSS to `web/` as the source of truth and generate the portal's from it.
- [x] [Review][Decision] ✅ **RESOLVED 2026-07-29 — non-vacuous now, and none of the three recorded options was needed.** The finding's own framing was too narrow: it treated `ApplyReferenceLinks` as the abbreviation expander, when it is **five** linkifiers. `StoryEpicLinkifier` needs neither a glossary nor a requirement — only an "Epic N"/"Story N.M" mention in a **text node** — and this page renders exactly one, in the `ListRow` demo chip (`<span class="list-row-chip pill">Epic 1</span>`), while the fixture already defines Epic 1. So the positive control needs **no new fixture and no new production seam**: the test now parses the fixture's `epics.md`, asserts the shipped `<main>` contains `ListRow.Chip("Epic 1")` verbatim and **no** `epics/epic-1.html` href, then asserts `StoryEpicLinkifier.Linkify` on that same markup **does** change it and **does** produce the href. **Proven red** by making `WriteDesignSystem` call `ApplyReferenceLinks` — the test failed, and the bypass was restored. ⚠️ **The abbreviation-expander route was tried first and does NOT work**, which is worth recording so nobody re-attempts it: the page's only FR/NFR/PRD occurrences sit inside an `<a>` in the nav band, and `ProtectedSplit` protects whole anchors — an expander-based control would have been a second vacuous assertion wearing a positive control's clothes. Failure message names the cause directly rather than reporting a missing substring. _Original finding:_ **`DesignSystem_BypassesApplyReferenceLinks` cannot be made non-vacuous without a new fixture or a new seam** — a contrast control was written ("assert some OTHER page in this site carries `<abbr>`/`ref-chip`") and it **failed**, which is the finding rather than a bug in the control: the minimal fixture (one epic, one story, no glossary, no requirements) produces no expander output anywhere, so the guard passes identically with and without the bypass. The test now says so in a comment instead of implying a guarantee. Options: extend the fixture with a glossary term + requirement IDs so the linkifiers actually fire / add a seam asserting the write path directly (that `WriteDesignSystem` reaches `WriteOutput` without `ApplyReferenceLinks`) / delete the test as not worth its weight.

**Verification of the applied patches (2026-07-28).** Live in a browser at `http://localhost:3033/design-system/`
(Nuxt dev; the C# page is unchanged apart from the two fingerprint-moving edits, which the suite pins):

- **Ten** stages render, `unmapped` among them, carrying the portal's actual word **"Not yet mapped"**.
- **Zero** phantom tokens on the page: `--status-retired` / `--status-unmapped` appear nowhere. Retired's
  swatch computes `rgb(122, 98, 80)` = `--status-deferred`, matching the portal and clearly separated from
  Drafted's `rgb(232, 217, 168)` (they were 4/255 apart before).
- The chip renders `class="list-row-chip pill"` with Courier New / `0.3456px` tracking / `3.2px 11.2px` /
  `rgb(250,247,242)` / `rgb(90,69,53)` — i.e. `.pill`'s own values, exactly.
- Panel root is `DIV`; `.chart-panel-body` count is **0**.
- The reduce block computes `animation-delay: 0s` and `transition-delay: 0s` alongside the durations.
- Skip link computes `position: fixed`. (Its `:focus` offsets could not be exercised — `document.hasFocus()`
  is false in this headless pane — but the containing-block fix is what the finding was about.)
- **`--impact-lvl-3` resolves to `#dcae4d` inside the Vue app** — the family that had never crossed the
  bridge. That is the token-extractor fix confirmed at runtime rather than argued.
- Zero console errors.

⚠️ **Screenshots were again unavailable** (the Browser pane is not displayed in this session, so
`computer{action:"screenshot"}` times out). Verification was by live computed styles and DOM geometry, which
is what catches containment leaks and sub-pixel collapse — but the owner's verify round should still *look*.

### Review follow-up — 2026-07-29 (the three open decisions closed)

_All three remaining `[Review][Decision]` items resolved; the story's checkbox surface is now fully closed.
Owner answered the two that needed answering (D1's scope, D2's fork) at the start of this pass; D3 needed no
answer because a fourth option existed. Details are inline on each item above; what follows is what a reader
needs that is not on any single item._

**One ADR proposed, not asked for.** [ADR 0029](../../docs/adrs/0029-unscoped-shared-primitive-layer.md) —
**amends ADR 0018's property 3 (Scoped)**. Story 23.3 ratified that *every* rule in the generated layer is
nested under `.ir-content` "so it cannot reach a template-authored component even by accident", and that is
precisely the property the owner's chosen fix breaks. CLAUDE.md's decision-record rule makes that an ADR, not
a story note. It carves the exception as narrowly as it can (an allowlist of one, published in the manifest,
with a two-part admission test) and **states the cost plainly rather than minimising it: containment is no
longer absolute.** It also corrects a stale clause in ADR 0018 itself — property 4 still describes per-rule
line spans in the manifest, which Story 23.5 removed for reddening CI on unrelated edits.

**Both new gates were proven RED before being trusted**, per this project's standing rule:

| gate | red proof | reported as |
| --- | --- | --- |
| `Stylesheet_StageFillsOnBadgeFamilies_AreTokensNotInlineLiterals` | reintroduced one literal fill | `.status-badge.active => background: #e0ecea` |
| `DesignSystem_BypassesApplyReferenceLinks` (positive control) | made `WriteDesignSystem` call `ApplyReferenceLinks` | test failed, bypass restored |
| `check:ir-content` (shared layer, drift) | hand-edited `.pill`'s font-family | `shared-primitives.css: ~1 changed  ~ .pill` |
| `check:ir-content` (shared layer, absent) | deleted the sheet | `missing: web/assets/shared-primitives.css` |
| the whole partition mechanism | emptied `SHARED_PRIMITIVES` and re-extracted | `.pill` returned to the scoped layer (875 → 876 rules) |

⚠️ **That last proof found a defect in my own tests, and it is the most transferable thing in this pass.**
Emptying the allowlist made the *mechanism* reverse correctly but left the new "exactly one definition"
assertions **passing** — every one of them iterates `SHARED_PRIMITIVES` or the manifest's rules, so an empty
allowlist means no loop body executes. Only the two `isSharedPrimitive` unit tests went red. That is the exact
by-construction vacuity the 2026-07-28 re-review was called in to find, reproduced by the fix for it. A
`SHARED_PRIMITIVES.length > 0` guard now precedes the block, and it is proven red too. **A test that loops
over a collection is vacuous whenever that collection can be empty — assert non-emptiness first.**

**A pre-existing red gate at HEAD, fixed and disclosed.** `check:assets` was **already failing** before this
pass, on a stale `web/public/specscribe.js` — HEAD `630ae25` changed the source asset and nobody re-synced.
`web/public/` is gitignored (`web/.gitignore:13`), so this was purely a local artifact and `npm run sync:assets`
resolved it; **no committed file was involved and the drift was not caused by this change.** Recorded because
Story 23.5 reported the identical failure mode, which suggests the sync is easy to forget after an asset edit.

**Live verification, portal vs Nuxt.** All eight badge stages now compute **identical** backgrounds on both
surfaces (five of them were one flat `--parchment` on the Vue side before). The `.epic-status` mirrors on
`/epics.html` match the badges exactly, with **no visual change** from before — same values, one declaration
instead of two, which is the whole point. The `pill pill-link` case on `/epics/story-1-2.html` exercises the
unscoped base *and* the scoped variant together and matches the golden portal property for property.

⚠️ **Screenshots unavailable for the third consecutive session** on this story (Browser pane not compositing).
Everything above is a measured computed style or DOM fact. Servers left running for the owner's verify round:
**`:8102/design-system.html`** (portal) and **`:3033/design-system`** (Nuxt dev) — plus `:3033/epics/story-1-2.html`
for the injected-`.pill` case. No new `.claude/launch.json` entries were added; the existing
`design-system-23-2` and `web-dev-23-3` were reused, since "the list only grows" is a standing finding here.

**Suite.** C# **2814 passed / 0 failed / 3 skipped** (symlink-privilege gated). `web/` vitest **106 passed** (95 before).

> #### ⚠️ The "one rotating contention flake per full run" has a cause: a running preview server
>
> This story has recorded that flake as unexplained since 2026-07-25 ("a *different* single test each time…
> all pass in isolation"). This pass produced a clean signal by accident, running the suite four times either
> side of the live-browser verification:
>
> | run | preview servers | result |
> | --- | --- | --- |
> | 1 | none | **2814 / 0 failed** |
> | 2 | nuxt dev `:3033` + `http.server :8102` | 2813 / **1 failed** (`SiteGeneratorImpactMapTests…WebviewCoherence`) |
> | 3 | same two servers | **several failed**, and the messages named the cause: `git CLI unavailable on this host`, `DirectoryNotFoundException` on a fixture subdirectory |
> | — | stopped | the run-2/3 failures **pass 15/15 in isolation**; `git --version` works fine |
> | 4 | none | **2814 / 0 failed** |
>
> The failures are **git subprocess spawn** failing under load, not assertion failures — which fits the
> whole observed family (git-derived surfaces: impact map, commit details, timeline, git-insights, code-map
> determinism) and fits "a different test each time", since which spawn loses is arbitrary. It also explains
> why isolation always passes.
>
> **Practical rule: do not run the full suite while a preview server is up.** That is not a code defect and
> not a test defect, so it is recorded here rather than filed — but every previous full run on this story was
> taken during live verification, which is very likely why the flake looked constant. Worth a moment in the
> Epic 23 retrospective; a `dotnet test` that spawns `git` is inherently sensitive to host load, and the
> honest reading of past runs is "contended", not "flaky".
`npm run check` fully green: `check:tokens` (45 tokens / 2 `:root` blocks), `check:ir-content` (875 scoped
rules + 4 keyframes + 1 unscoped shared rule), `check:assets` (4 assets).

**Not re-run, and why.** `measure:parity` / `check:links` / `check:a11y` need a full `nuxt generate` over
~1,150 routes and assert on `<main>` **bytes**, link targets and chip words. This pass changed CSS
declarations, one Vue `<style>` block and one text run on a non-migrated page — it moved no IR markup, no
href and no status word, so those three cannot be affected. Stated rather than silently skipped.

**Suite state.** C#: **2752 passed / 3 skipped / 1 failed**, the failure being
`FileWatcherServiceTests.WatchedSourceFileStaysWritableAndDeletableDuringRegeneration` — re-run alone **twice,
passed both times**. That is the documented one-rotating-contention-flake-per-full-run, not a regression.
`web/` vitest: **95 passed** (88 before, +7 pinning the multi-`:root` extraction). `npm run check:tokens`
green and proven RED in five distinct failure modes. `npm run check:ir-content` fails on a **precondition**,
not this change — it needs `SpecScribeOutput/spa/manifest.json`, which a concurrent session's plain `generate`
had wiped; regenerating with `--spa` restores it.

**Dismissed as noise (1):** `LegendWord("retired")` depending on Story 8.9's `StoryLabel` arm — the `_ => "Pending"` trap is real, but `StoryLabel:123-125` carries an explicit arm *and* a comment warning about exactly this scenario, and `LegendWord:455-458` explains the routing. Two guards on a hypothetical deletion is adequate.

### Review Findings — 2026-08-07 (third pass)

_Three-layer adversarial pass (Blind Hunter, Edge Case Hunter, Acceptance Auditor). Baseline `cd7f302`;
review started at HEAD `35437b9` and **HEAD moved to `6de2890` mid-review** (a concurrent session landed
16.1/16.2 and a sunburst fix) — the story file itself was byte-identical across that move, and every finding
was re-verified by symbol against the working tree afterwards. Scoped to this story's File List and declared
symbols per CLAUDE.md. **Sibling work excluded**: 23.3 (`ir/`, `surfaces/`, `[...path].vue`,
`IrHtml.ts`/`IrMain.ts`, the bulk of `ir-content*`), 23.5 (packaging, `.nvmrc`, `build-package`, CI/vitest
wiring, the Nuxt 4 bump), 23.6 / ADR 0034 (retirement of the C# `.html` writer), 18.x, 20.x, 22.x, 8.9.
Every subagent claim was independently confirmed by the orchestrator against the real files; nothing below is
reported on a subagent's word alone._

**Gate state at HEAD.** `check:tokens` green (45 tokens / 2 `:root` blocks). `check:ir-content` and
`check:assets` fail on documented **local preconditions only** (no generated IR; `web/public/` is gitignored
and unsynced) — neither is a code defect. web vitest **165 passed / 1 skipped**. C# `DesignSystem` +
`SiteNav` + `Stylesheet` filter: **116 passed / 0 failed**. ⚠️ No `.specscribe/analysis/` digest exists in
this checkout, so SonarCloud state is **UNKNOWN, not clean** (per CLAUDE.md, absent ≠ clean).

**AC verdicts at HEAD:** #1 HOLDS (one bypass, below) · #2 PARTIAL · #3 HOLDS · #4 PARTIAL · #5 HOLDS
(soft caveat) · #6 **PARTIAL / superseded by ADR 0034**.

**Decisions needed**

- [x] [Review][Decision] ✅ **RESOLVED 2026-08-07 — owner chose "adopt the saturated borders in `specscribe.css`", the same direction taken for the fills on 2026-07-29.** Applied, and the audit widened it: only `pending` and `deferred` differed in VALUE; `done`/`active`/`review`/`ready` differed only in which token NAME they bound, because `--status-done` *is* `var(--moss-light)`, `--status-active` *is* `var(--teal)`, `--status-review` *is* `var(--teal-deep)` and `--status-ready` *is* `var(--gold-light)`. All six now bind the semantic `--status-*` token on both surfaces, so four of them are a zero-visual-change rebinding whose point is that a rule naming the alias can drift from one naming the token with no gate noticing. `retired` already agreed (`--border` on both) and is unchanged. The `.epic-status.*` mirrors followed, per the precedent set for the fills and the comment at `specscribe.css:2969` that says these mirror the badges. **Real visual change, portal side:** pending's border `#d4c4a8` → `#b8b2a8`, deferred's `#d4c4a8` → `#7a6250` (pale tan → grey-brown, the visible one). Full C# suite **2964 passed / 0 failed / 3 skipped**. ⚠️ **Not yet verified in a live browser** — see the completion note. _Original finding:_ **`StatusBadge.vue` still re-colours three stages' BORDERS away from the portal — the unfixed half of a finding ticked closed on 2026-07-29** — raised independently by two layers, which is why it leads. `specscribe.css` binds `border-color: var(--border)` (`#d4c4a8`) for `.status-badge.pending` (`:3215`), `.retired` (`:3217`) and `.deferred` (`:5547`). `StatusBadge.vue` matches for `.is-retired` (`:175`) but binds `var(--status-pending)` = `#b8b2a8` at `:163` (also `.is-unmapped`) and `var(--status-deferred)` = `#7a6250` at `:169`. Deferred is the visible one: a dark grey-brown hairline where the portal draws pale tan. The 2026-07-26 finding named this half explicitly (*"flips `.is-pending`/`.is-deferred` borders from `var(--border)` to `var(--status-*)`"*), the owner's decision addressed only the four **fills**, and the 2026-07-29 live verification enumerated **backgrounds only** — after which the Change Log generalised it to *"all eight stages now compute identically on both surfaces"*, which is not true. Nothing gates it: `check:tokens` compares token values, not bindings, and `check:parity`'s frozen corpus contains no `StatusBadge`. Options: revert the Vue to `var(--border)` / adopt the saturated borders in `specscribe.css` and re-extract, as the owner chose for the fills (moves the shipped portal) / accept and record the divergence.
- [x] [Review][Decision] ✅ **RESOLVED 2026-08-07 — owner chose "promote `.skip-link` to a shared primitive" (ADR 0029).** `SHARED_PRIMITIVES` is now `['pill', 'skip-link']`, `PageShell.vue`'s competing scoped rule is **deleted** (replaced by a comment saying why re-adding one reinstates the race), and the base `.ir-content .skip-link` will leave the scoped sheet on the next extraction — so there is no longer a pair to tie. **Promoting the rule forced a decision the finding did not anticipate:** the two rules were different designs (teal-deep pill at `z-index: 200` vs warm-white bordered at `z-index: 10`), and the portal's carried `position: absolute` — the same containing-block bug the 2026-07-28 pass had fixed *in PageShell only*. Adopting the portal's rule unchanged would have regressed that fix, so `specscribe.css`'s `.skip-link` was corrected to `position: fixed` in the same change, which now protects **both** surfaces from one definition instead of one surface from a copy. ADR 0029 amended: allowlist "of one" → "of two", a new § Admissions table, and the observation that its § Cascade order reasoning did not cover a scope class applied *to* a component's root. ⚠️ **Which rule won before the fix was never determined empirically** — the fix removes the race rather than resolving it, so that question is now moot, but the *new* single rule still wants a live-browser check. _Original finding:_ **`PageShell`'s `.skip-link` collides with the extracted `.ir-content .skip-link` rule on every IR-backed route, and both outcomes break UX-DR16** [web/components/PageShell.vue:84 vs web/assets/ir-content.css:25] — `IrSurface.vue:155` puts `class="ir-content"` **on PageShell's own root element**, so `.ir-content .skip-link` (0,2,0) and PageShell's scoped `.skip-link[data-v-*]` (0,2,0) tie, and the winner is decided by chunk order rather than by anything in the code. If PageShell wins, its `z-index: 10` (`:92`) sits **below** `.ir-content .site-nav`'s `z-index: 100` (`ir-content.css:294`, sticky at `top: 0`, opaque) — the focused skip link renders behind the nav bar, invisible. If `ir-content.css` wins, `position: absolute` returns — the exact regression `PageShell.vue:79-83` records the 2026-07-28 re-review as having fixed. The skip-link *target* is fine (`IrMain.ts:37` supplies `<main id="main-content">`), so this is purely the CSS collision. It also falsifies `IrSurface.vue:147-149`'s claim that "the extracted monolith rules cannot reach template-authored components" — applying the scope class *to* a template-authored root is precisely how they can. ⚠️ **Confirm in a live browser which rule actually wins before patching.** Options: raise PageShell's `z-index` above the nav and win the tie deliberately / exclude `.skip-link` from the `ir-content` extraction so PageShell owns it outright / promote `.skip-link` to a shared primitive under ADR 0029.
- [x] [Review][Decision] ✅ **RESOLVED 2026-08-07 — owner chose "retire the Vue showcase page now".** `web/pages/design-system.vue` is **deleted**, its `routeRules` and prerender entries removed, and the `/component-library` link re-pointed at `design-system.html` (the page users actually get, rendered from the C#-composed region through `PortalMetaSurface`). **AC #6's second half is recorded as WITHDRAWN, not pending** — ADR 0034 made the C#-composed region the thing Node renders, so there was no convergence left to wait for. ⚠️ **One thing the decision did not account for, handled explicitly:** the retired page carried AC #5's live `:deep()` worked example (the failing control and the fix, side by side). Deleting it would have removed the demonstration AC #5 requires, so the example was **moved into `CONVENTIONS.md` §3**, which is where AC #5 says the convention must be demonstrated. §3 also gained the forward pointer it was missing (patch below). _Original finding:_ **AC #6 is superseded by ADR 0034, and its second half is now unreachable rather than pending** — `SiteGenerator.WritePage` (`SiteGenerator.cs:4373`) states *"[Story 23.6 AC #1] It no longer writes anything"*, and ADR 0034 §Decision 3 is *"No C# code path emits a content `.html`."* `WriteDesignSystem` (`:5783`, still called unconditionally from `:915`) now only composes the region into `_spaPageViews`; **Node** writes `design-system.html`, and without Node no HTML is produced at all. Meanwhile `nuxt.config.ts` sets `prerender.routes` to `[]` under `PACKAGE_BUILD` and lists `/design-system` only in the non-package branch — so a user's portal renders `design-system.html` from the **C#-composed region** via `PortalMetaSurface` (`web/ir/families.ts:77`) and **never** `pages/design-system.vue`. The Dev Notes tradeoff assumed "the page is then re-authored as the Nuxt route"; as shipped, the two design-system pages are a **permanent** duplication. Options: restate AC #6 to match ADR 0034 / retire `pages/design-system.vue` / schedule the convergence as a 23.4 follow-up.
- [x] [Review][Decision] ✅ **RESOLVED 2026-08-07 — recorded honestly, and the finding itself was CORRECTED in the process.** ⚠️ **The Acceptance Auditor's claim was too strong and I confirmed it was wrong before acting on it.** It grepped only `web/components/surfaces/` and `pages/[...path].vue`. A full sweep shows `ChartPanel`, `ListRow` and `PageShell` **are** consumed by real app pages — `pages/component-library.vue` (the developer landing page) and `error.vue` — and `PageShell` additionally by every IR route through `IrSurface`. Only **`StatusBadge`** is genuinely without a product consumer, and after the `/design-system` retirement its sole remaining callers are the `/measure/*` payload fixtures. So: CONVENTIONS §5's glyph deferral to Story 23.3 is **withdrawn** (23.3 injects C#-rendered markup that already carries the glyph, so it could never have discharged it), `StatusBadge` is documented as **fixture-grade rather than a shipped primitive**, and the other three are explicitly recorded as NOT being in that position. The same correction is in `StatusBadge.vue`'s header. _Original finding (as raised, and partly wrong):_ **The primitives this story exists to ship are consumed by no live surface, so CONVENTIONS §5's glyph deferral can never be discharged by the story it names** — `grep '<StatusBadge|<ListRow|<ChartPanel'` across `web/components/surfaces/` and `pages/[...path].vue` returns **comments only**; the real routes inject C#-rendered markup (which already carries the glyph). Only `PageShell` is on the live path (`IrSurface.vue:154`). `CONVENTIONS.md:168-178` defers the badge glyph to Story 23.3 "where the stage→icon mapping gains a data source"; 23.3 is `review` and 23.4 has shipped, and `StatusBadge.vue:98` still renders `{{ label }}` with no icon prop, slot or sprite. Options: park the three unused primitives and say so plainly / re-point the deferral at a story that will actually consume them / accept them as showcase-only and rewrite §5's dependency note.
- [x] [Review][Decision] ✅ **RESOLVED 2026-08-07 (dev-story round) — owner chose "gate when an IR is present, warn otherwise".** `check-ir-content.mjs` gained an opt-in `--if-ir` flag, wired into **both** `prebuild` and `pregenerate`; `npm run check` stays deliberately **unflagged**. The flag runs the real gate when an IR is present and otherwise skips at exit 0 with a three-line warning naming the path it looked for and the command that closes the gap. That resolves the cycle rather than working around it: `pregenerate` runs before the build that would produce an IR, so an unconditional gate hard-fails every cold build, while the mistake actually worth catching — editing `specscribe.css` then re-running `generate` against an output root that **already exists**, the case CLAUDE.md's regeneration-order rule warns about — always has one. Keeping `check` unflagged means CI (which always generates first) still treats a missing IR as a hard failure, so the skip path can never hide a broken pipeline. **All four arms proven live, not argued:** no IR + flag → skip, exit 0; no IR, no flag → exit 1 (unchanged); IR present + flag → the real check runs and passes; IR present + flag + a hand-edited `shared-primitives.css` → **exit 1**, reporting `shared-primitives.css: +0 rule(s), -0, ~1 changed / ~ .pill`. The decision logic was extracted into `wantsIfIrSkip` / `irManifestPath` in `ir-content-lib.mjs` so it is unit-testable (the script does its work at module scope under top-level await, so a test importing it would execute the whole gate), and **11 tests** were added — including a **wiring** block asserting both lifecycle hooks actually invoke it, which is the load-bearing half: this story has twice found a gate whose content was verified while its delivery path was not (ADR 0029's `.pill` reached the page through one `nuxt.config.ts` import line no test read). That wiring test was **proven red** by reverting `pregenerate` to its pre-fix shape. Documented in CONVENTIONS §10. _Original finding:_ Two of its three symptoms were closed by patches below without needing the decision: `build:package` now runs `check-tokens` (it bypassed `prebuild` because npm's lifecycle prefix matches the script *name*), and `check:ir-content` now asserts that `nuxt.config.ts` actually imports all three generated sheets in the load-bearing order. What remains is the original question — whether `npm run generate` should refuse to build on a drifted `shared-primitives.css`/`ir-content.css` — and it is genuinely ambiguous because `check:ir-content` needs a generated IR, which is the cycle `.github/workflows/build-test-analyze.yml:222-233` documents. _Original finding:_ **The token bridge got a build-time hook; the CSS layer this story added did not, and the obvious fix hits a documented cycle** [web/package.json:29-30] — `prebuild`/`pregenerate` run `check-tokens.mjs` only, so `npm run generate` builds and prerenders happily on a stale or hand-edited `shared-primitives.css`/`ir-content.css`; only the explicit `npm run check` catches it. Given CLAUDE.md's regeneration-order rule (a stylesheet edit must be extracted from an IR that already contains the new markup, or rules are silently pruned), `generate` is exactly where that mistake is made and the one path with no gate. Adding `check:ir-content` to `pregenerate` is **not** unambiguous: it needs a generated IR, which is the cycle `.github/workflows/build-test-analyze.yml:222-233` documents. Options: gate only when an IR is already present (skip-with-warning otherwise) / leave it to CI and document the hole in CONVENTIONS / restructure so the check runs post-generate.

**Patches**

- [x] [Review][Patch] `.pill` reaches the unscoped shared layer only if the **consumer's IR** happens to use it, which is not ADR 0029's stated admission test [web/scripts/ir-content-build.mjs:142] — `const keep = selectors.filter(s => selectorIsUsed(s, used))` runs at `:142`, **before** the shared/scoped partition at `:149`. `used` is harvested from IR page markup only (`:64-90`), never from Vue templates. On a project whose IR renders no `.pill`, the rule is pruned as unused, `shared-primitives.css` regenerates empty, and `ListRow.vue`'s chips — which deliberately declare **no** visual properties, delegating all ten to the shared layer (`ListRow.vue:149-151`) — render completely unstyled. ADR 0029's admission test is "a C# primitive emits it AND a template-authored Vue component consumes it"; what actually gates carriage is IR usage, an unrelated condition. Exempt `SHARED_PRIMITIVES` from the usage prune.
- [x] [Review][Patch] `shared-primitives.css` has a gated *content* but a completely ungated *delivery path* [web/nuxt.config.ts:65] — ADR 0029 **removed** the base `.pill` rule from the scoped layer (verified: `ir-content.css` carries only `.pill.status-*` / `.pill.pill-link` variants), so its sole route into a page is one import line. Delete or mis-order it and every `.pill` on the site — injected chips *and* `ListRow`'s — renders unstyled, while `check:tokens`, `check:ir-content`, `check:parity` and `web/test/ir-content-lib.test.mjs:254-322` all stay **green**: the tests read the sheet file, none reads the config. Before ADR 0029 the base rule rode an import already load-bearing for hundreds of rules; the exception moved it onto a line nothing watches. Add a test asserting the import and its order.
- [x] [Review][Patch] `measure:payload` is the one harness with no truncated-run guard, contradicting the README's blanket promise [web/scripts/measure-payload.mjs] — `web/README.md:47-48` ("they refuse to publish a number from a truncated prerender") and `:51-56` ("Every harness hard-fails when it is set"). `assertFullRun` (`harness-lib.mjs:101`) is called by `check-a11y.mjs`, `check-links.mjs` and `measure-parity.mjs`, and by **nothing** in `measure-payload.mjs`. Because `nuxt.config.ts:166-173` appends `/measure/*` *after* the truncated `prerenderIrRoutes`, the three measure routes survive `SPECSCRIBE_IR_ROUTE_LIMIT=5` — so a truncated run succeeds and **commits** `measurements/payload.txt` from a five-route site with no warning.
- [x] [Review][Patch] `measure:payload` reports a missing `_payload.json` as `1.00x` and exits 0 [web/scripts/measure-payload.mjs:80] — every payload lookup ends `?? 0`. If Nuxt changes the payload filename, or `/measure/**` picks up `noScripts: true` from the `'/**'` route rule (one line away in `nuxt.config.ts`), variant A prints `payload: 0` and a `1.00x` ratio — reading as "the async-data path costs nothing", the precise inversion of AC #4's conclusion — and commits it. The comment at `:83-86` explains why `missing` is tracked separately from a zero size **for html**; the same reasoning is not applied to the payload column, which is the column the experiment is about.
- [x] [Review][Patch] A variant that declares `islandComponents` but matches zero island files exits 0 [web/scripts/measure-payload.mjs:61] — `startsWith(\`${c}_\`)` matches nothing if Nuxt emits islands under a different path shape; variant B's island column reads `—`, its total and ratio are understated, and the script still commits the table. Assert that a variant declaring island components resolved bytes for them, as the `missing` guard at `:92-98` already does for html.
- [x] [Review][Patch] Any nested `ENOENT` is reported as "`.output/public` not found" [web/scripts/measure-payload.mjs:45] — the recursive `walk` runs entirely inside one `try` whose only discriminator is `err.code === 'ENOENT'`, so a broken symlink or a file removed mid-walk sends the operator to run `npm run generate` on a directory that exists and is complete.
- [x] [Review][Patch] CONVENTIONS §4's payload table no longer matches the committed measurement it cites as proof [web/CONVENTIONS.md:104] — `:101` says "the run is committed at `measurements/payload.json` so the numbers are checkable rather than quoted", then quotes A 119.3/**163.9 KB**, B 119.0/119.0/**238.4 KB**, C 119.3/**119.4 KB**. `web/measurements/payload.txt` at HEAD records 121.2/**165.8**, 120.9/121.0/**242.2**, 121.2/**121.3**. Sibling Story 23.4 re-ran the harness in commit `a8c97f3` and left the table at the old run. **Ratios are unchanged (1.37×/2.00×/1.00×), so the conclusion is unaffected** — the "checkable rather than quoted" property is not. Same line: the link renders as `[measurements/payload.json](measurements/payload.txt)` — label and href name different files.
- [x] [Review][Patch] CONVENTIONS closes by stating something false at HEAD [web/CONVENTIONS.md:508] — *"`web/` is production-intent but **not shipped yet**: it is not in `SpecScribe.slnx` and not wired into `specscribe generate`."* The `.slnx` half is still true; the second is not — `src/SpecScribe/NuxtPrerender.cs` boots `web/.output/` as part of `generate`, and ADR 0034 makes Node a hard prerequisite. A reader reaching the bottom of the conventions doc is told the app does not ship, on the page that is now the shipping path's authority.
- [x] [Review][Patch] CONVENTIONS §3 prefers `:deep()`; what shipped is the global sheet it advises against, and §3 never says so [web/CONVENTIONS.md:90] — *"Prefer `:deep()` scoped to the injecting component — it keeps the blast radius at one component instead of the whole app."* 23.3/23.4 did the other thing: `IrHtml.ts` splices via `createStaticVNode` and styling comes from generated **global** sheets (`ir-content.css`, and per ADR 0029 the deliberately unscoped `shared-primitives.css`). §10 and §10a document that correctly, but §3 — the section AC #5 names — carries no forward pointer, so a reader following it in isolation is pointed away from the shipped architecture. (AC #5's load-bearing half — scoped styles do not reach `v-html` — is stated correctly and demonstrated live at `design-system.vue:200/209`.)
- [x] [Review][Patch] Two doc claims are false as a direct consequence of the border drift [web/components/StatusBadge.vue:12, web/CONVENTIONS.md:173] — both state that `deferred`/`retired` "are byte-identical rule sets here". They are not: `.is-deferred` uses `var(--status-deferred)`, `.is-retired` uses `var(--border)`. Both sentences are the stated justification for deferring the badge glyph to 23.3, so 23.3 inherits a premise that does not hold.
- [x] [Review][Patch] The token bridge's block *finder* rejects a grouped `:root` prelude, and fails open [web/scripts/tokens-lib.mjs:174] — `selector.trim() === ':root'` is exact-string equality, so `:root, :host { … }` or `:root,\n[data-theme="print"] { … }` is not recognised as a token block at all. The whole family it declares silently never crosses the bridge, and because `check-tokens.mjs` runs the same extractor on both sides, the two files **cannot disagree about tokens neither looks at**. `REQUIRED_TOKENS` catches it only if a listed name happens to sit in that block. This is the identical fail-open shape as the first-block-only bug this module was rewritten to fix.
- [x] [Review][Patch] "`:root` inside an at-rule = viewport override" is asserted of every at-rule, not just `@media` [web/scripts/tokens-lib.mjs:141] — `@layer tokens { :root { … } }` or `@supports (…) { :root { … } }` is silently dropped from the bridge on the grounds that it is a breakpoint override. The sibling extractor `ir-content-build.mjs:233` already recognises `@(media|supports|layer|container)` as distinct conditional forms; only the token bridge collapses them.
- [x] [Review][Patch] `tokenMap` handles only a *final* missing semicolon; one mid-block swallows the next declaration [web/scripts/check-tokens.mjs:104] — with `--a: 1` (no `;`) followed by `--b: 2;`, the map comes back `--a → "1\n  --b: 2"` and **`--b` is absent entirely**. The drift report — whose whole purpose is that "the first line of a failing CI log should already say which one happened" — then names `--a` as changed with a multi-line value and lists `--b` as missing, pointing the operator at a token nobody touched. The docblock at `:87-94` claims the `(?:;|$)` fix covers "the final declaration in a rule"; it does, and only that one. No test exercises the drift-report path.
- [x] [Review][Patch] The "no literal token values" test scans only the **first** `:root` block, and only 6-digit hex [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:249] — the regex `^:root\s*\{(.*?)^\}` is non-greedy, so it stops at block one. `specscribe.css`'s **second** top-level `:root` declares `--impact-lvl-1`…`-5` (`#f3e8c6`, `#e9cd82`, `#dcae4d`, `#c8912b`, `#a86f1e`) — the exact block whose omission from the bridge was this story's headline regression — so the page could state one of those verbatim and the test stays green. `#[0-9a-fA-F]{6}\b` also misses 3-digit, 8-digit, `rgb()`, `hsl()` and `oklch()` forms. The test's own comment says it was rewritten to stop being coupled to a hand-listed subset of the palette; it still is, along a different axis.
- [x] [Review][Patch] An unguarded loop over `LegendStages` is vacuous if the collection is ever empty [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:77] — `DesignSystem_DocumentsEveryCanonicalStatusStage_ByNameNotColourAlone` passes without executing a loop body. Its three siblings in the same file each open with exactly this guard (`Assert.NotEmpty` at `:184`, `:212`, `:257`), and `web/test/ir-content-lib.test.mjs:272` adds a dedicated non-vacuity test for the same reason. This one was missed — the same defect class the 2026-07-29 pass caught in its own tests.
- [x] [Review][Patch] `StatusBadge` crashes SSR in dev and ships a colour-only badge in prod when `label` is `null` [web/components/StatusBadge.vue:88] — `label: string` erases at runtime and a JSON IR emits `null` for an absent field. Under `import.meta.dev`, `props.label.trim()` throws and fails the **whole route's** SSR; in production the dev block is compiled out, `{{ label }}` renders `''`, and the component ships exactly the colour-only badge its own header says it must never render. The existing guard covers `''` but not `null`/`undefined`.
- [x] [Review][Patch] `ListRow`'s `accent` has no unknown-value guard, asymmetric with `StatusBadge` [web/components/ListRow.vue:59] — `` `accent-${accent}` `` emits a class no rule matches for any out-of-union string (e.g. `accent="review"`, a value this file's own doc records as having been in the set until 2026-07-28), so `--list-row-accent` stays unset and a "deferred" or "done" row reads as an ordinary row. `StatusBadge` guards union erasure with `KNOWN_STAGES` + a dev warning; `ListRow` has neither.
- [x] [Review][Patch] `ListRow` accepts a non-array `chips` and iterates it by character [web/components/ListRow.vue:49] — `props.chips ?? []` substitutes only for `null`/`undefined`, so `:chips="'3 tasks'"` passes through and `v-for` renders seven single-character `.pill` chips. Related, same lines: `:chips="['']"` makes `chipList.length` truthy, so the `.list-row-meta` cluster renders to hold one empty pill — the "never an empty-but-present wrapper (NFR8)" contract the header claims.
- [x] [Review][Patch] `ChartPanel`'s required `title` is the one field with no empty guard [web/components/ChartPanel.vue:39] — `title=""` or `:title="null"` yields an empty `<h3>`: an unlabelled heading inside a landmark-bearing panel. Every other region is `v-if`-guarded, and **both** design-system pages state the opposite as a contract (`design-system.vue:143`, `DesignSystemTemplater.cs:241`: a slot with nothing to say renders nothing at all, rather than an empty heading).
- [x] [Review][Patch] `PageShell` emits the skip link unconditionally while its target is gated [web/components/PageShell.vue:46 vs :56] — `<a href="#main-content">` always renders; `<main id="main-content">` renders only under `chrome === 'full'`. **Not reachable on the live IR path** (`IrSurface.vue` passes `chrome="nav-only"` *and* supplies `IrMain.ts:37`'s `<main id="main-content">`), so this is a latent primitive-contract gap rather than a live break — but `chrome` is also union-erased and `chrome === 'full'` is the only test, so any out-of-vocabulary value silently selects the `nav-only` branch.
- [x] [Review][Patch] ⊘ **MOOT — the file was deleted by decision 3, not patched.** The drift is gone because the second vocabulary is gone; `StatusStyles` is once again the only place the word is defined. Recorded rather than silently ticked, and noted in CONVENTIONS §5 as one of the reasons the page was retired. _Original finding:_ The Vue design-system page teaches a different definition of `unmapped` than the portal [web/pages/design-system.vue:42] — *"Listed as a requirement but mapped to no epic"* vs `StatusStyles.cs:411` *"Listed, but not yet mapped to any epic or story"*, the seam `DesignSystemTemplater.StatusBody` renders from and `SiteGeneratorDesignSystemTests:93` pins. The other nine stages match verbatim, so this is one drifted string. The page's own header (`:26-33`) claims the vocabulary is kept in exact correspondence, and nothing in `web/test/` compares the two.
- [x] [Review][Patch] "Hover or focus any badge for its meaning" is false for keyboard users [src/SpecScribe/DesignSystemTemplater.cs:227] — `StatusStyles.cs:451` emits the badge as a bare `<span class="status-badge … js-tip" data-tip title>`, and there is no `tabindex` on `.status-badge` anywhere in `specscribe.css` or `specscribe.js`, so it cannot receive focus and `title`/`data-tip` are pointer-only. On the page that teaches the portal's accessibility discipline, a keyboard-only reader following the instruction gets nothing. (No information is lost — the meaning is recoverable from the visible `status-legend-key-meaning` text — so it is the **instruction** that is wrong.)
- [x] [Review][Patch] The `/measure/*` fixtures pass prose into `ChartPanel`'s `window` slot — the exact misuse the C# twin's re-review removed [web/pages/measure/async.vue:19, island.vue:11, static.vue:17] — `ChartPanel.vue:17` documents the prop as "Numeric analysis window", and `DesignSystemTemplater.cs:123-127` records deliberately dropping `Window` because "this page passed prose into it and the Vue twin passed component filenames". These three still emit `window="variant A|B|C"`, and they are linked as the AC #4 demo — so an author copying the story's own fixtures reproduces the misuse the same story documented as a defect.
- [x] [Review][Patch] A new `LegendStages` entry gets a token caption but no swatch rule [src/SpecScribe/DesignSystemTemplater.cs:183] — an eleventh stage with a matching `--status-<stage>` token but no `.status-legend-key-swatch.<stage>` rule falls to the `_ =>` arm and emits a class matching nothing: a blank swatch beside a caption naming a token that **does** exist. `DesignSystem_NamesNoTokenTheStylesheetDoesNotDeclare` verifies the token half; nothing verifies the swatch class resolves.
- [x] [Review][Patch] The fill-sharing caption hardcodes its pair instead of deriving it from `StageFillTokens` [src/SpecScribe/DesignSystemTemplater.cs:195] — `stage is "ready" or "drafted" ? ", shared with ready/drafted" : ""`. A fifth entry pointing at `--status-ready-bg` prints no note (a reader sees two identical token names with no explanation — the confusion the note exists to prevent), or the note names the wrong two stages. The test at `SiteGeneratorDesignSystemTests.cs:232` asserts only that the literal string appears somewhere.
- [x] [Review][Patch] ⊘ **MOOT — the file was deleted by decision 3, not patched.** The C# twin's caption is now the only one, and it is no longer hardcoded: the "shared with …" note is derived from `StageFillTokens`, and its test asserts the behaviour for every shared fill rather than pinning the one literal. _Original finding:_ The Vue page drops a caption the C# twin is explicitly tested for [web/pages/design-system.vue:110] — `SiteGeneratorDesignSystemTests.cs:232` pins `"shared with ready/drafted"` on the portal page because "the page says so rather than leaving a reader to notice two identical token names and wonder if it is a mistake". The Vue twin renders only `{{ tokenFor }} on {{ fillFor }}`, showing `--status-ready on --status-ready-bg` twice with no note that the pairing is deliberate.
- [x] [Review][Patch] `REQUIRED_TOKENS` is the bridge's only defence against a rename, and it is hand-maintained with no derivation from its consumers [web/scripts/tokens-lib.mjs:30] — its own comment (`:32-36`) records that renaming `--parchment-dark` once passed every gate green while four badge stages lost their background. **Verified: all currently-consumed tokens are covered, so there is no live hole** — but the guard is a list a human must remember to extend, protecting against a failure whose whole nature is that nobody notices. Derive it: scan the SFCs and `assets/*.css` for `var(--…)` and require every referenced name to be declared.
- [x] [Review][Patch] `build:package` bypasses the token gate that `prebuild`/`pregenerate` establish [web/scripts/build-package.mjs:43] — the artefact users get is built by `npm run build:package`, which calls `spawnSync('nuxt', ['build'])` directly; npm's lifecycle prefix matches the script name `build`, not `build:package`, so `prebuild` never fires. CI still covers it (`build-test-analyze.yml:319` runs `npm run check`), so this is a local/packaging gap rather than an unguarded pipeline. **Introduced by sibling 23.5's `build:package` after this story's closure was written — handed off to 23.5 rather than claimed here.**
- [x] [Review][Patch] README drift around the new sheet and the runtime [web/README.md:3,65] — the layout map lists `tokens.css`, `ir-content.css` and `base.css` but omits `assets/shared-primitives.css`, the third generated layer ADR 0029 and CONVENTIONS §10a establish, so the README is now an incomplete answer to "what is generated here". Separately `README.md:3` and `nuxt.config.ts:1` both call this "the production-intent **Nuxt 3** app" while `package.json` pins `nuxt: ^4.5.1`.
- [x] [Review][Patch] A half-finished interpolation in the one error a maintainer reads when the bridge cannot latch [web/scripts/tokens-lib.mjs:258] — `${'are'}` is a literal, so the message reads "the extracted ':root' block(s) (…) are missing 1 required token(s)". Cosmetic, but it looks like an unfinished edit in the bridge's most diagnostic message.

**Deferred**

- [x] [Review][Defer] `npm ci` fails in `web/` — `package.json` and `package-lock.json` are out of sync [web/package-lock.json] — deferred, **sibling-owned**. `npm ci` reports `Missing: @emnapi/runtime@1.11.3 from lock file` (the lock carries `@emnapi/core` and `@emnapi/wasi-threads` but not `runtime`). CI runs `npm ci` deliberately — `build-test-analyze.yml:241` states *"the lockfile is the pin, and a lockfile-drifting install in CI would…"* — but pins Node **24.11.1** via `web/.nvmrc` while this reproduction used 24.18.1 / npm 11.16.0, so CI is probably still green and this is a lockfile-**portability** defect rather than a broken pipeline. `git log` puts the file's last write in commit `c1a6ee5` (Story 23.5's Nuxt 4 bump), **not** in 23.2's `261b300` — handed off to **23.5**.
- [x] [Review][Defer] The token scanner is comment-aware but not string-aware [web/scripts/tokens-lib.mjs:94] — a quoted CSS string containing `}` or `/*` (e.g. `content: "}"`) would decrement `depth` and misjudge every subsequent top-level `:root`. **Verified unreached: no such string exists in `specscribe.css` today.** Deferred as latent — noted because the structurally identical comment case is guarded twice with a docblock explaining why, and this repo has a `*/`-truncation incident in its history.
- [x] [Review][Defer] No committed gate covers `design-system.html`'s rendered output any more — deferred, **ADR 0034 residue**. The story record stamps three `GoldenContentFingerprint` values (`2050b586…` → `501ee958…` → `22c921de…`) as its regeneration provenance; ADR 0034 retired that gate and `SiteGeneratorAdapterTests.cs:256` carries only its tombstone. Its successor `check:parity` runs over a **frozen 24-route corpus** that does not include `design-system.html` (the `PortalMetaSurface` family is represented by `about-sdd.html`). Defensible — the family is covered by proxy and the region is well covered by the 13 C# tests — but the three stamped hashes are now unverifiable and the page's rendered chrome has no direct gate.

**Dismissed as noise (0).** Every claim raised by the three layers was verified against the real files and survived; the only downgrade was `PageShell`'s unconditional skip link, kept as a low-severity latent patch after confirming `IrMain` supplies the target on the live path.

### Review follow-up — 2026-08-07 (4 decisions applied, 28 of 30 patches applied)

Owner answered four decisions in one round; all four are applied and recorded inline above. **28 patches
applied**; the two not applied are `unmapped`'s wording drift and the missing "shared with" caption, both of
which lived in `web/pages/design-system.vue` and are **moot because that file was deleted** by decision 3.

> ## ✅ CLOSED 2026-08-07 (dev-story round) — `check:ir-content` IS GREEN, AND THE DIAGNOSIS BELOW WAS WRONG
>
> **`check:ir-content` passes at HEAD with `carriedRules: 1476` and `sharedRules: 3` — the exact numbers this
> block demanded — and it did so with NO regeneration at all.** A fresh `npm run extract:ir-content` from a
> full corpus reproduced the four committed artifacts **byte-for-byte** (`git status --porcelain web/assets/`
> → empty). The committed layer was already correct; sibling commit `0b1f561` ("regenerate the two stale
> drift gates") had done it properly.
>
> ⚠️ **The root cause named below — "this environment cannot produce a full corpus" — is NOT what happened,
> and the correction matters more than the fix.** The missing ingredient was a **command-line flag**:
> `--deep-git`. `.github/workflows/build-test-analyze.yml` documents it explicitly ("⚠️ `--deep-git` IS
> REQUIRED, and the reason is `check:ir-content` — not analytics"), because a shallow run never emits the
> code-insights history/relationships tabs, the relationship-graph swatches or the deep-analytics panels, so
> `selectorIsUsed` prunes every rule only those surfaces exercise. CI measured the delta as **`-182` rules**
> without the flag and **`-0` with it**. The third-pass run was **181 rules short** (1295 vs 1476) — the same
> number. It was a plain shallow generate, not a broken machine.
>
> Two environment factors were real but secondary, and both are now recorded in CONVENTIONS §10 so the next
> reader does not rediscover them: `npm ci` must run with `SPECSCRIBE_PACKAGE_BUILD=1` (otherwise
> `postinstall: nuxt prepare` loads `nuxt.config.ts`, which reads an IR manifest that does not exist before
> the first generate — the cycle CI documents), and from a git worktree `SPECSCRIBE_RENDERER_DIR` must point
> at *that* checkout's `web/.output` (the repo-root search looks for a `.git` **directory**; a worktree's is a
> **file**, so `generate` otherwise looks in the main checkout and silently skips the prerender).
>
> **Executed here, in full, and this is what a correct run looks like:** the `--deep-git` generate reported
> `[prerender] 1546 route(s) … errors=0`, `generated=801`, and a **populated `SpecScribeOutput/code/` with 284
> code pages** (the corpus whose absence caused the shortfall). `npm run check` is green on all four gates —
> `check:tokens` (45 tokens / 2 `:root` blocks), `check:ir-content` (1476 + 5 keyframes scoped, 3 shared, 15
> runtime-body), `check:assets`, and `check:parity` (24 pinned routes across 14 of 14 families,
> byte-identical). The gate was additionally **proven red** by hand-editing `shared-primitives.css`, which
> reported `~1 changed / ~ .pill`, then restored clean.
>
> _The original block, kept because its refusal was the right call:_ **the third-pass session ran the
> extraction, got `carriedRules: 1295`, and THREW THE RESULT AWAY rather than commit it.** Committing would
> have stripped 755 lines from the shipped stylesheet with the gate green — the exact "regenerate a baseline
> on a corpus that moved under you" failure CLAUDE.md warns about. Declining to regenerate on a corpus it
> could not trust was correct; only the explanation was wrong.
>
> <details><summary>Original ⛔ text (superseded)</summary>
>
> `specscribe.css` changed (six badge borders, five `.epic-status` mirrors, `.skip-link` → `position: fixed`)
> and `.skip-link` joined `SHARED_PRIMITIVES`. Both require `npm run extract:ir-content` to be re-run, and
> **I could not do it correctly in this environment.**
>
> I ran it, and then **threw the result away rather than commit it.** The regeneration produced
> `carriedRules: 1295` against the committed `1476` — **181 rules short** — because
> `dotnet run -- generate` here emitted only **496 pages with an empty `SpecScribeOutput/code/`**, so the
> whole code-page corpus was absent and `selectorIsUsed` pruned every rule only those pages exercise.
> Committing it would have stripped 755 lines from the shipped stylesheet **with the gate green** — the exact
> "regenerate a baseline on a corpus that moved under you" failure CLAUDE.md warns about, and far worse than
> a red gate. The `sharedRules: 1 → 3` half was correct (the two `.skip-link` rules); only the corpus was wrong.
>
> **Required next step, on a machine that can produce the full corpus:**
>
> ```sh
> dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental   # re-embed the changed asset
> dotnet run --project src/SpecScribe -- generate                  # MUST yield a populated SpecScribeOutput/code/
> cd web && npm run extract:ir-content && npm run check:ir-content
> cd web && npm run build:package && cd .. && dotnet run --project src/SpecScribe -- generate
> ```
>
> Confirm `carriedRules` lands at ~1476 (not ~1295) and `sharedRules` at **3** before committing.
>
> </details>

**What was verified, and how.** Full C# suite **2964 passed / 0 failed / 3 skipped** — no preview servers
were running, and the run was clean, which is consistent with this story's own finding that the
"one rotating flake" is host contention rather than a defect. `check:tokens` green. Every gate I touched was
**proven RED before being trusted**, per the standing rule:

| gate | red proof | reported as |
| --- | --- | --- |
| `findRootBlocks` grouped prelude | `:root, :host { --a: 1 }` | `has the grouped prelude \`:root, :host\`` |
| `findRootBlocks` non-`@media` at-rule | `@layer tokens { :root { … } }` | `declares a \`:root\` block inside \`@layer tokens\`` |
| `findRootBlocks` regression check | `@media`, plain `:root`, `html:root` | unchanged: 0 / 1 / 0 blocks, and 2 blocks on the real sheet |
| `tokenMap` mid-block missing `;` | `--a: 1\n --b: 2;` | now `{--a: "1", --b: "2"}`; previously `--b` vanished entirely |
| `check:tokens` consumer scan | a temp SFC using `var(--status-doesnt-exist)` | named the file and the token; green again after deletion |
| `measure:payload` truncated run | `SPECSCRIBE_IR_ROUTE_LIMIT=5` | refused, exit 1 — it was the only harness missing this |

⚠️ **Two things I could not prove and am not claiming.** (1) **No live-browser verification** — CLAUDE.md
requires it for visual work, and this is visual work: the pending/deferred border change and the new single
`.skip-link` rule both need eyes on a rendered page. The Vue side additionally cannot be run here at all
(`web/node_modules` is absent in the worktree, and `npm ci` fails at HEAD — see the deferred item). (2) The
new `check:ir-content` **import-order assertion** is on the gate's success path, which is unreachable until
the regeneration above, so its logic was verified by replicating it against the real `nuxt.config.ts`
(all three sheets present, `shared-primitives.css` before `ir-content.css`) rather than by a green gate run.

**A false positive I introduced and corrected, worth recording.** The first version of the derived
consumer-token check flagged nine tokens in `ir-content.css` as dangling. Seven (`--emerald-deep`,
`--gold-dark`, `--teal-light`, the `--sb-*` family …) turned out to be `var(--x, #fallback)` references, which
degrade by design; the other two (`--col`, `--lane-count`) are set **inline by the renderer**
(`Charts.cs:995`, `SprintTemplater.cs:234`). The check now matches only fallback-less `var()` and skips the
generated sheets, which are `check:ir-content`'s business. Recorded because a gate that cries wolf on
generated output is how a real drift gets waved through.

**Also changed, and it moves a committed measurement.** The three `/measure/*` fixtures stopped passing prose
into `ChartPanel`'s `window` slot (the variant moved into the title). That shifts each route's HTML by ~25
bytes against a 121.2 KB baseline — below the table's 0.1 KB precision, so the numbers synced above still
stand, but `measurements/payload.*` should be re-run at the next full build.

### Review Findings — 2026-08-07 (fourth pass)

_Three-layer adversarial pass (Blind Hunter, Edge Case Hunter, Acceptance Auditor) at HEAD `c73ebcb`,
baseline `cd7f302`. **Chunked** — this is **Group A only**: the five Vue primitives, `DesignSystemTemplater.cs`,
the `DesignSystemOutputPath`/Help-nav hunks in `SiteNav.cs`, `LegendWord` in `StatusStyles.cs`, the glyph in
`Icons.cs`, `SiteGeneratorDesignSystemTests.cs`, the `Stylesheet_StageFillsOnBadgeFamilies…` guard in
`StylesheetTests.cs`, and ADR 0029 (~2,050 of the story's 6,277 in-scope lines). **Groups B, C and D were NOT
reviewed** and are listed at the end of this section. Scoped by File List **and by hunk** per CLAUDE.md —
sibling work excluded: 18.x (`IdeasOutputPath`, `TestArtifactsOutputPath`), 20.x, 22.x, 8.9
(`RetirementStatusWords`, `StoryStages`, `StoryLabel`), 23.3 (`ir/`, `surfaces/`, `[...path].vue`,
`IrHtml.ts`/`IrMain.ts`), 23.5 (packaging, Nuxt 4 bump), 23.6 / ADR 0034. Every subagent claim was
independently re-verified by the orchestrator against the working tree; **two were corrected and four
dismissed** — nothing below rests on a subagent's word._

**Gate state at HEAD, measured (not taken from the record).** C# `DesignSystem`+`SiteNav`+`Stylesheet` filter
**146 passed / 0 failed**. web vitest **182 passed / 1 skipped**. `check:tokens` **green** — 45 tokens across
2 `:root` blocks, plus the derived "every `var(--…)` in `web/` resolves" check the third pass asked for.
`check:ir-content` and `check:parity` fail on **documented local preconditions only** (no generated IR at
`SpecScribeOutput/spa/manifest.json`; no built Nitro server) — environmental, not code defects. SonarCloud
digest regenerated: only two Group A files carry observations (`StatusBadge.vue:117`, `ChartPanel.vue:41`),
both `AvoidCommentedOutCode`, both **verified false positives** (prose doc comments).

**AC verdicts at HEAD:** #1 HOLDS · #2 HOLDS · #3 PARTIAL (`StatusBadge` has no product consumer — disclosed,
not hidden) · #4 HOLDS · #5 HOLDS (the `:deep()` worked example genuinely moved into CONVENTIONS §3) ·
#6 C# half HOLDS clause-by-clause / Vue half WITHDRAWN under ADR 0034.

**Decisions needed**

- [ ] [Review][Decision] **`PageShell`'s missing-landmark guard cannot fire on any route that can trigger the bug — a fix added by the 2026-08-07 pass is inert** [web/components/PageShell.vue:61-71] — raised **independently by two layers**, which is why it leads. The `onMounted` check registers only when `chrome !== 'full'`; the sole `nav-only` consumer is `IrSurface.vue:154`, and `nuxt.config.ts:143` sets `'/**': { noScripts: true }` for exactly those routes — they never hydrate, so `onMounted` never runs. The two routes that *do* ship scripts (`:147-148`, `/component-library` and `/measure/**`) use the default `chrome: 'full'`, where the branch is skipped. `import.meta.dev` compiles the whole block out of `nuxt generate` besides. The intersection of "guard registered" and "scripts run" is **empty**. Contrast its three siblings added in the same pass — `ChartPanel.vue:27`, `ListRow.vue:95`, `StatusBadge.vue:89` all warn from `setup()`, which does execute during SSR and does fire. The fix is **not** unambiguous: the check needs the DOM, so it cannot simply move to `setup()`. Options: inspect the default slot's vnodes for `<main id="main-content">` at render time (works in SSR, but couples the shell to its slot's shape) / emit the skip link only under `chrome === 'full'` and require `nav-only` callers to own both link and landmark (behaviour change; `IrMain` already supplies the landmark) / replace the runtime guard with a build-time test over the surface components / accept the hole and delete the dead guard rather than leave a fix that reads as applied.
- [ ] [Review][Decision] **All four primitives ship their forbidden empty output in production; every guard added on 2026-08-07 is dev-only** — `StatusBadge.vue:102-108` (inside `if (import.meta.dev)` at `:89`; template `:113` is a bare `{{ label }}`) renders `<span class="status-badge is-done" title></span>` for a `null`/`''` label — a **colour-only badge**, the one output its own header says it must never render (UX-DR17). Its comment at `:96-101` explicitly concedes this and does not fix it. Same shape: `ChartPanel.vue:51` (empty `<h3>`, guard `:27-37`), `PageShell.vue:81` (empty `<h1>` — page-level, and with **no** guard at all), `ListRow.vue:107` (`summary`, the one required prop in the family with neither guard nor warning). The correct fix is genuinely ambiguous, which is why this is a decision and not a patch. Options: render nothing (`v-if`) so an absent value is absent rather than an empty landmark / render a visible fallback word so the badge keeps its UX-DR17 channel / keep dev-only on the grounds that `StatusBadge` is now fixture-grade and the other three have real consumers — in which case say so in each header instead of asserting a guarantee that holds only in dev.
- [ ] [Review][Decision] **ADR 0029 is still `Proposed` while the mechanism it decides is shipped, load-bearing, has grown its allowlist once, and is itself amended by a second ADR** [docs/adrs/0029-unscoped-shared-primitive-layer.md:3] — the unscoped layer is in the shipped import chain (`nuxt.config.ts:67-73`), gated (`check-ir-content.mjs:119-134`), and has broken ADR 0018's ratified property 3 **in production**. ADR 0039 (Proposed 2026-08-06) already **amends** it. CLAUDE.md makes "a ratified ADR is the authority" load-bearing, which an ADR stuck at Proposed cannot discharge. The ADR itself says ratification is the owner's — so this is not a story defect, it is an outstanding owner action on an architectural change that has already taken effect. Options: ratify 0029 now (and decide 0039 with it) / leave Proposed and record why an in-production mechanism is unratified.

**Patches**

- [ ] [Review][Patch] The stage-vocabulary assertions are satisfied by construction from the functions they should be checking [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:97,99] — `Assert.Contains(PathUtil.Html(StatusStyles.LegendWord(stage)), html)` and the `StageMeaning` line assert against the **same two calls** `DesignSystemTemplater.cs:224-225` makes in the same loop. They detect the page abandoning the seam; they cannot detect the seam being wrong. This file already diagnosed and fixed exactly this vacuity for `--status-{stage}` (its comment at `:87-91`) and left the word/meaning pair untouched. Only `Ready for dev` and `In review` are genuinely pinned, at `:344-347`.
- [ ] [Review][Patch] `Assert.Contains($"--status-{stage}", html)` is satisfied by the `-bg` fill token for four of the ten stages [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:94] — for `done`, `active`, `review` and `ready` the accent name is a strict prefix of the fill token the same list item also prints (`--status-ready` ⊂ `--status-ready-bg`). Drop the accent from `StatusBody` for one of those four and the assertion still passes. Precisely the by-construction vacuity the comment block immediately above it was written to eliminate. Match the whole token (`--status-{stage}</code>`) or use a boundary.
- [ ] [Review][Patch] `Stylesheet_StageFillsOnBadgeFamilies_AreTokensNotInlineLiterals` has three holes, and its docblock claims to be structural [tests/SpecScribe.Tests/StylesheetTests.cs:146,155,158] — (a) **it passes when it inspects nothing**: the loop fills `offenders` and asserts `Assert.Empty(offenders)`, with nothing asserting a single rule was examined; rename `.status-badge.`/`.epic-status.` or break the innermost-rule regex (whose own comment at `:143-144` concedes it assumes exactly one level of at-rule nesting) and the guard reports success. Its four siblings in the neighbouring file each open with `Assert.NotEmpty`. (b) It matches `background(?:-color)?` **only** — a literal `color:` or `border-color:` on `.status-badge.<stage>` is the identical bridge-blindness failure and passes. (c) `:158` detects only `#` literals, while its sibling `DesignSystem_NeverStatesATokenValueAsALiteral:316` was widened to `rgba?|hsla?|oklch|oklab|lab|lch` **on the same 2026-08-07 pass**. The guard is structural on the stage axis and hand-listed on the property and notation axes.
- [ ] [Review][Patch] The `retired` swatch emits a class no production caller emits [src/SpecScribe/DesignSystemTemplater.cs:186] — `"retired" => ("deferred", …)`, but `.status-legend-key-swatch.retired` is its **own real rule** (`specscribe.css:5932`) and `StatusStyles.LegendKey:488` — the production legend this page claims to mirror — remaps **only** `unmapped`. Byte-identical output today (both resolve to `var(--status-deferred)`), which is why nothing looks wrong. This is the same defect the 2026-07-28 re-review fixed for `BadgeBody`, whose comment at `:260-265` states the rule verbatim: *"which no production caller emits, on the page whose load-bearing claim is 'built from the ACTUAL primitives, never look-alike markup'"*. The swatch half was left behind. (The `tokenNote` — "shares `--status-deferred`" — is accurate and should stay; it is the **class** that should be `stage`.)
- [ ] [Review][Patch] Four surviving copies of "`web/` is NOT wired into `specscribe generate`", false at HEAD — one of them **rendered on the running page** [web/pages/component-library.vue:13, web/package.json:5, web/nuxt.config.ts:6, web/README.md:8] — `src/SpecScribe/NuxtPrerender.cs` boots `web/.output/` as part of `generate` and ADR 0034 makes Node a hard prerequisite. The 2026-08-07 pass fixed this sentence in `CONVENTIONS.md:574` and in **no** other copy. `component-library.vue:13` puts it in a `subtitle` a reader of the app sees. Same line: `package.json:5` still says "**Nuxt 3**" while the pin is `^4.5.1` — the drift the same pass corrected in `nuxt.config.ts:1` and `README.md`.
- [ ] [Review][Patch] `DesignSystemTemplater`'s class doc points at a file this story deleted and states a plan it withdrew [src/SpecScribe/DesignSystemTemplater.cs:24-27] — *"this page is re-authored as the Nuxt `/design-system` route (see `web/pages/design-system.vue`, which mirrors it). The owner accepted that duplication…"*. `web/pages/` holds only `[...path].vue`, `component-library.vue` and `measure/`. The retirement was recorded in three places (`nuxt.config.ts:175-181`, `component-library.vue:18-20`, `CONVENTIONS.md:588-598`) and missed here — leaving the **primary surviving statement of the withdrawn plan** on the very class AC #6 is about.
- [ ] [Review][Patch] The shared-primitive allowlist is published as "one entry" in the two places a reader goes to count it [web/CONVENTIONS.md:430, docs/adrs/README.md:39] — it is **two**: `SHARED_PRIMITIVES = ['pill', 'skip-link']` (`web/scripts/ir-content-lib.mjs:89`). ADR 0029's body was updated with an § Admissions table; its **index entry** still reads *"an allowlist of **one**, published and gated"*, and CONVENTIONS §10a still reads *"one entry today, `pill`"*. Both are drift from 23.2's own `skip-link` admission, and ADR 0029's § Consequences names exactly this failure — the containment property *"erodes anyway if nobody counts"*. Failure scenario: an author reads §10a, concludes `.skip-link` is not in the layer, and re-adds a scoped rule to an SFC — reinstating the chunk-order specificity tie the admission was made to remove.
- [ ] [Review][Patch] `DesignSystem_DocumentsTheMotionTokenFamily` is the hand-typed copy its replacement's comment says was removed [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:108-112] — it still asserts a literal five-element array. `MotionTokens` was made `internal` (`DesignSystemTemplater.cs:33-36`) expressly so *"a test asserts against THIS list, never a re-typed copy"*, and the derived replacement sits immediately below saying the previous test *"asserted a literal five-element array against a literal five-element array"*. It was duplicated, not replaced — so renaming `--motion-stagger` reddens a test for the wrong reason and invites re-typing the new name into it.
- [ ] [Review][Patch] The motion panel's caption hardcodes its own count [src/SpecScribe/DesignSystemTemplater.cs:101] — `Ranking: "Five named timings; no surface invents its own."` while the body renders every entry of the derived `MotionTokens`, and `SiteGeneratorDesignSystemTests.cs:226-229` asserts in the reverse direction that every `--motion-*` in `specscribe.css` appears on the page. Adding `--motion-exit` therefore *forces* a sixth entry and the page renders six definitions under a caption reading "Five", whole suite green. The count belongs in `MotionTokens.Length`.
- [ ] [Review][Patch] The "shared with" assertion is over-specified against the join format and goes **red on correct output** [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:283] — the templater joins sharers with `/` (`DesignSystemTemplater.cs:215`), so three stages on one fill emit `shared with drafted/ready`; the test asserts `Contains($"shared with {other}")` for *each* member, and `shared with ready` does not occur. With two sharers the joined string happens to equal the single name, which is why it passes today. The guard written on 2026-08-07 specifically to stop pinning *"the one pair that happened to be spelled out"* fails on the first case that is not that pair.
- [ ] [Review][Patch] `StatusBody`'s sharer ordering allocates per comparison and ranks an unknown stage **first** [src/SpecScribe/DesignSystemTemplater.cs:213] — `.OrderBy(s => StatusStyles.LegendStages.ToList().IndexOf(s))` materialises a fresh `List<string>` on every key-selector invocation, inside a per-stage loop; and `IndexOf` returns `-1` for a stage absent from `LegendStages`, sorting it *ahead* of every real stage instead of flagging it. `StatusStyles.CanonicalRank` (`StatusStyles.cs:427`) already exists and ranks an unknown token *after* every known stage — it is the seam this should use.
- [ ] [Review][Patch] The story's File List names two files that do not exist and omits several rounds' work [this file:802-803, 862] — listed but absent: `web/pages/design-system.vue` (deleted by this story's own decision 3) and `web/pages/index.vue` (renamed to `component-library.vue` by 23.3). Changed by 23.2 rounds but listed nowhere: `web/test/tokens-lib.test.mjs`, `web/measurements/payload.{json,txt}`, the `/measure/*` `window`-prop hunks, and `component-library.vue`'s 23.2 hunk. Structurally there are addenda for the 2026-07-29 follow-up and the 2026-08-07 **dev-story** round but **none** for the 2026-07-28 re-review or the 2026-08-07 **review** follow-up (4 decisions + 28 patches) — which is where most of the above landed. CLAUDE.md makes the File List the scoping instrument for the next reviewer, so a stale one is what makes hunk attribution unreliable.
- [ ] [Review][Patch] Task 6 is ticked though its sole deliverable was deleted [this file:81-83] — both subtasks name `web/pages/design-system.vue`, including *"Verify live in a browser… tokens resolve, reduced-motion honored, status readable by name with JS off"*. The 2026-07-29 precedent for an item overtaken by a later decision is `⊘ MOOT`, applied to two patches (`:355`, `:360`) but not to the task they belonged to.
- [ ] [Review][Patch] An out-of-vocabulary `chrome` loses the layout as well as the landmark, and the warning names only the landmark [web/components/PageShell.vue:57-58,76,87] — the template tests `chrome === 'full'` twice and `chrome === 'nav-only'` once, three independent tests over a two-value union. `:chrome="null"` selects a **third** state: no `<main id="main-content">` *and* no `.shell-bare`, so a full-bleed IR page is constrained to the 64rem reading column. The dev warning states only the missing-landmark half.
- [ ] [Review][Patch] `$slots.x` is truthy when the slot was *passed*, not when it *rendered* [web/components/ChartPanel.vue:63, web/components/ListRow.vue:108] — `<template #badge><StatusBadge v-if="hasStatus" …/></template>` with a falsy condition renders an empty `.list-row-meta` (`margin-left: auto`) and an empty `.chart-panel-legend` (`margin-top: 0.75rem`) — the empty-but-present wrapper both headers claim never to emit (`ListRow.vue:13-14`, NFR8). The test is `$slots.badge?.()?.length`, not slot presence.
- [ ] [Review][Patch] Nothing enumerates `.status-badge.<stage>` against `LegendStages`, though the swatch half has exactly that test [tests/SpecScribe.Tests/StylesheetTests.cs:66,70] — only `unrecognized` and `retired` are pinned. `DesignSystem_EverySwatchClassResolvesToARuleInTheStylesheet` closes the class half for swatches; the badge takes the identical `_ =>` path and emits `class="status-badge <stage>"` twice per stage with no equivalent. A new stage's badge falls through to the base rule and renders visually identical to `pending`, on the page that teaches the colour vocabulary.
- [ ] [Review][Patch] "Design System" is a hand-typed literal in five places with a silent empty-glyph fallback [src/SpecScribe/SiteNav.cs:231,236 · Icons.cs:67 · DesignSystemTemplater.cs:145 · HtmlRenderAdapter.Dashboard.cs:598] — `Icons.ForConcept`'s unknown-key arm is `_ => string.Empty` (`Icons.cs:38`, *"Unknown/uncurated label → empty string (graceful)"*) and `QuickLinkFamily`'s is `family-planning`, so a one-character drift silently drops the glyph and mis-families the quick link, with no test covering either. Sibling Story 18.5 introduced `SiteNav.TestArtifactsLabel` (`SiteNav.cs:186`) as a `const` for exactly this hazard, **in the same file**; the 23.2 label did not get one.
- [ ] [Review][Patch] `ChartPanel`'s header asserts anatomy parity the file itself concedes it does not have, and anchors it to a line that has moved [web/components/ChartPanel.vue:8,41] — `:8` says *"a panel authored in Vue **cannot** grow a different anatomy from one authored in C#"* while `:60-65` renders `.chart-panel-legend` under a comment reading *"The one region the C# frame has no concept of."* The legend slot is an owner-accepted keep (decision resolved 2026-07-28); the blanket sentence was never narrowed to match the exception. Separately `:41` cites `Charts.cs:168` — `Framed` is now declared at `:174` and emits the `<div>` at `:177`; a hardcoded line number in a cross-language correspondence comment is the one place CLAUDE.md's "confirm by symbol" guidance is not followed, and it has already drifted.
- [ ] [Review][Patch] `LegendWord`'s doc comment names three delegate seams; the body reaches two [src/SpecScribe/StatusStyles.cs:455] — *"delegating to `StoryLabel` / `RequirementLabel` / `SprintLabel`"*, but the `SprintLabel` arm was removed in the same edit (`:464-466` explains why) and `SprintLabel` is now unreachable from this method. The summary was not updated with the body, on the one method that exists to be the single label seam.
- [ ] [Review][Patch] `CONVENTIONS.md` links a non-existent ADR 0034 filename, and it is the citation for AC #6's withdrawal [web/CONVENTIONS.md:579] — `../docs/adrs/0034-node-renders-every-content-page.md`; the real file is `0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md`. Confirmed the target does not exist and this is the only occurrence of that filename in the repository.
- [ ] [Review][Patch] Two reverse-direction loops have no non-emptiness guard [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:226-229, :255-258] — each sits directly below an `Assert.NotEmpty`-guarded forward loop but is itself unguarded, so a rename of the token prefix in `specscribe.css` empties the match set and the "nothing declared is undocumented" direction becomes vacuous.
- [ ] [Review][Patch] `ListRow` silently drops non-string chips [web/components/ListRow.vue:78] — `raw.filter((c) => typeof c === 'string' && …)`, while the prop's own doc at `:44` names *"a count"* as a legitimate chip. `:chips="[3, 'Epic 1']"` loses the `3`, and the dev warning at `:70-75` fires only on the non-array branch, so the per-element drop is invisible even in dev.
- [ ] [Review][Patch] `MeasureRows` renders an empty `<ul>` for a null or negative count [web/components/MeasureRows.server.vue:11-12] — `:count="null"` is not substituted by `withDefaults`, and `Array.from({ length: count })` coerces to 0, so the island emits an empty list landmark rather than no list. No lower or upper bound on `count`.

**Deferred**

- [x] [Review][Defer] `<see cref="WriteOutput"/>` is dangling in `WriteDesignSystem`'s doc — but it is **one of five** across the file, so this is 23.6's residue, not 23.2's [src/SpecScribe/SiteGenerator.cs:5788; also :1108, :4769, :5261, :5273; plus tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs:502] — deferred, **sibling-owned (23.6)**. `WriteOutput` was deleted by Story 23.6 AC #1 (tombstone at `:4332`); `WriteDesignSystem` now calls `WritePage`, which per its own doc *"no longer writes anything"*. The summary's opening words ("Writes `design-system.html`") are likewise no longer literally true — Node writes it. ⚠️ The Acceptance Auditor reported this as a 23.2 defect; the orchestrator corrected it to a repo-wide 23.6 cleanup, and it is handed off explicitly per CLAUDE.md rather than left to fall between the two reviews.
- [x] [Review][Defer] `LegendWord`'s fallback mislabels any future canonical stage as "Pending" / "Status stage" [src/SpecScribe/StatusStyles.cs:467 → StoryLabel:132; StageMeaning:414] — deferred, **pre-existing (Story 8.9)**. An eleventh `LegendStages` entry renders the wrong word on the page whose subject is the status vocabulary, *and* — via `LegendKey` — in the portal-wide legend popover on every page. 23.2's extraction only routes to the fallback; the fallback and its "Trap 1" comment are 8.9's.
- [x] [Review][Defer] `DesignSystem_NeverStatesATokenValueAsALiteral` cannot see a `:root` nested in an at-rule [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:308] — deferred, latent. `^:root` under `RegexOptions.Multiline` requires column 0. `specscribe.css:6182` is exactly that shape today but declares no colour, so nothing escapes; a `@media (prefers-color-scheme: dark) { :root { --status-done: #… } }` block would put a whole palette outside the guard. The comment's claim of "EVERY **top-level** `:root`" is accurate as written — this is the boundary, not a false claim.
- [x] [Review][Defer] `list-style: none` strips list semantics in Safari/VoiceOver [web/components/ListRow.vue:123, web/components/MeasureRows.server.vue:27] — deferred, **UNCONFIRMED** against a live screen reader. WebKit drops the `list`/`listitem` roles when `list-style-type: none` is applied and neither element carries an explicit `role`, so the row count would not be announced. Template-authored path only — injected IR markup inherits the portal's own sheet. Belongs to the outstanding live-verification round.

**Dismissed as noise (4).**
(1) SonarCloud `AvoidCommentedOutCode` on `StatusBadge.vue:117` and `ChartPanel.vue:41` — both read and confirmed to be **prose documentation comments**, not commented-out code. (2) Blind Hunter framed the `retired` swatch remap as *"contradicting its own comment three lines below"* — it does not; that comment governs `badgeClass`, and the swatch borrow is deliberately documented at `:177-182`. The substantive defect survives as a patch above; the stated contradiction was dropped. (3) Blind Hunter called `CONVENTIONS.md:412`'s *"Ratified shape: ADR 0029 (Proposed)"* self-contradictory — it is a consistent house style used identically for ADRs 0017 and 0018 at `:260` and `:314`. (4) The Acceptance Auditor scored AC #3's *"not wired into `specscribe generate`"* clause as violated — that clause was superseded by 23.5/23.6 and ADR 0034, which is later-story supersession rather than a 23.2 defect; only the **stale doc copies** survive, as a patch above.

**⚠️ Not reviewed — Groups B, C and D.** This pass covered ~2,050 of the story's 6,277 in-scope lines. Still
unreviewed at fourth-pass depth: **B** — the token bridge and payload harness (`tokens-lib.mjs`,
`extract-tokens.mjs`, `check-tokens.mjs`, `tokens-lib.test.mjs`, `measure-payload.mjs`, `pages/measure/*`,
`utils/measure-rows.ts`; ~1,200 lines). **C** — the shared-primitive CSS layer and the `--if-ir` gate
(`ir-content-lib.mjs`, `ir-content-build.mjs`, `check-ir-content.mjs`, `extract-ir-content.mjs`,
`ir-content-lib.test.mjs`; ~1,940 lines, mostly 23.3-owned and needing hunk attribution). **D** — docs and
config (`CONVENTIONS.md`, `README.md`, `package.json`, `nuxt.config.ts`, `app.vue`, `base.css`, `.gitignore`;
~1,070 lines). Do not read this section as a clean bill for those groups.

**⚠️ Still outstanding, and it is not a checkbox — now a FIFTH consecutive session.** The live-browser pass
CLAUDE.md requires for visual work has still not happened. Two changes with visual consequence remain
**un-seen**: `deferred`'s badge border (pale tan → grey-brown, on every badge and `.epic-status` mirror), and
the promoted single `.skip-link` rule (`shared-primitives.css`, `position: fixed`, `z-index: 200`) whose
predecessor's failure mode was *invisible-when-focused*. This session had no browser tool either.

## Dev Notes

### What this story is (and is not)

This is the **first Epic 23 story that ships durable code** (23.1 was a throwaway spike). Execution order
for the epic is **23.2 → 23.3 → 23.5 → 23.4** (owner-confirmed at the 23.1 spike gate). This story establishes
the component + token foundation; **23.3** migrates the dashboard/epics surfaces onto it; **23.5** decides how
the Node/Nuxt build ships; **23.4** retires the C# `HtmlRenderAdapter` for content (blocked until 23.5).

**23.2 does not consume the Epic 22 canonical IR.** The primitives are template-authored showcase components,
not IR-injected content, so this story is **not blocked** by Story 22.2 being backlog. (23.3 is where IR
consumption and the `v-html` injection path first get exercised for real — which is why the `:deep()`
convention documented here matters there, not here.)

### Owner decisions locked at create-story (do not re-litigate)

1. **Code location: new repo-root `web/`.** Production-intent, but not in `SpecScribe.slnx`, not wired into
   `generate`, node_modules already gitignored. 23.3 migrates into it; 23.5 decides how it ships.
2. **Token bridge: extract a `tokens.css` layer** from `specscribe.css`'s `:root` via a build script; Nuxt
   imports only tokens; a drift check fails the build on divergence. The C# stylesheet stays the single source
   of truth (AD-7); **no production C# change is needed for the bridge itself** (the extraction reads the file).
   This deliberately does **not** import `specscribe.css` wholesale the way the spike did — dragging the
   7,041-line monolith into every page would keep alive the exact fragility class (the `*/`-comment
   silent-truncation incident) that Epic 23 exists to end.
3. **Library scope: the proof primitives 23.3 needs** — StatusBadge, framed chart-panel, ListRow, page shell.
   Not a full inventory (most would be authored blind); not thin-tokens-only (23.3 would then reinvent
   primitives ad hoc — the drift this epic exists to prevent).
4. **Design-system page: both a C#-generated `design-system.html` referenced explicitly in the Help menu
   (ships today) and a parallel Nuxt `/design-system` route (ships when the app is wired in).**

> **Flagged tradeoff the owner accepted (AC 6):** the C# `design-system.html` adds a new templater + nav
> entry + golden-fingerprint move to the very renderer Story 23.4 is scheduled to retire, and the page is then
> re-authored as the Nuxt route. The owner chose this to get the design system documented in the portal now
> rather than waiting for the Nuxt app to be wired in. Build both; keep the C# page's markup simple so 23.4's
> removal is cheap.

### Spike constraints that bind this story (from `23-1-spike-report.md`)

- **`<style scoped>` does not reach `v-html`-injected markup** (spike finding 4). Vue stamps the `[data-v-*]`
  attribute only on template-authored elements; injected IR HTML is not stamped. Styling injected content needs
  `:deep()` or a global sheet. 23.2's own primitives are template-authored (scoped works for them), **but the
  conventions doc must document this for 23.3**, which injects IR content. See `spike/nuxt-ir/components/SurfaceShell.vue`
  for the `:deep()` demonstrator and `StatusLegend.vue` for the scoped-token probe.
- **The async-data data path doubles output weight** (spike finding 7 / gate item b): 2.26×, entirely hydration
  payload, on `code-map`-scale sites. Anything reaching a component through an async data source is serialized
  into `_payload.json` by construction. The `<NuxtIsland>` / server-component shape renders server-side with no
  hydration payload and is the right shape for content that is static by construction — but the spike did
  **not** measure it. AC #4 makes measuring it this story's first experiment.
- **The token binding is drift-free by construction only if values are never re-typed.** The spike proved
  `var(--status-done)` resolves live to `rgb(107, 143, 98)` when the tokens come from the shipped stylesheet.
  The `tokens.css` extraction preserves that property; the drift check enforces it.

### The token source (AD-7)

The single source of truth is the `:root` block in `src/SpecScribe/assets/specscribe.css` (opens ~line 65,
closes ~line 82; status/motion tokens live there alongside the brand palette). Families to carry:

- **Status (six-stage lifecycle + non-lifecycle):** `--status-pending`, `--status-drafted`, `--status-ready`,
  `--status-active`, `--status-review`, `--status-done`, `--status-deferred`, plus `--status-unrecognized` /
  `--status-unrecognized-hatch`. `StatusStyles` is the status→stage source; the tokens are the stage→color
  source. Never reintroduce per-component literal colors.
- **Motion:** `--motion-fast`, `--motion-entrance`, `--motion-entrance-long`, `--motion-ease`, `--motion-stagger`.
  Every entrance/hover routes through these, plus the `prefers-reduced-motion` reduce block. (See
  `motion-token-system`.)
- **Brand palette** the status/motion tokens resolve against (`--moss-light`, `--teal`, `--gold-light`, `--ink`,
  `--parchment`, `--border`, …) — extract the whole `:root` so the tokens resolve standalone.

Do **not** pull in the webview theme remaps (`specscribe-webview-theme.css`); those are host-owned chrome (AD-7).

### C# page wiring (Task 7) — exact seams

- **Model:** `HowToReadTemplater.cs` (static `RenderPage(SiteNav, …)` returning a full HTML string via
  `PathUtil.RenderHeadOpen` → `nav.RenderNavBar` → `RenderBreadcrumb` → `<main id="main-content">` →
  `PathUtil.RenderFooter`). `AboutSddTemplater.cs` is a second model.
- **Nav:** in `SiteNav.Build`, the Help group is assembled at the `help.Add(("How to use SpecScribe", …))`
  block (~line 184) with matching `quickLinks.Add(…, "Help")` entries. Add a `DesignSystemOutputPath` const
  near `HowToReadOutputPath` (~line 100) and register the new entry there. Written on **every full run** so the
  Help link never dangles (same guarantee as How-to-read/About/Diagnostics).
- **Generation:** add `WriteDesignSystem(nav)` beside `WriteHowToRead(nav)` in the always-written block
  (`SiteGenerator.cs` ~line 460). It must ride `WriteOutput` so `CapturePages` (webview) and `--spa` capture it.
- **No `ApplyReferenceLinks`** on this page (mirrors How-to-read/About) — a page that names token/vocabulary
  terms must not self-expand them into reference chips.

### Verification (CLAUDE.md § Verification — this is visual work)

- Verify the C# `design-system.html` in a **live browser with JavaScript off**: every status swatch is labelled
  by name (not color alone), the page is fully readable, reduced-motion is honored. The test suite structurally
  cannot see color-only signalling or a containment leak — look at the rendered page.
- Verify the Nuxt `/design-system` route renders with tokens resolving to the correct computed values and
  scoped styles containing correctly.
- **Golden fingerprint hazard (concurrent shared main):** the new page + nav entry moves
  `GoldenContentFingerprint`. The hash may also shift under a concurrent session's edits. Confirm the
  regenerated hash is **stable across two repeated runs** before locking the constant, and note in the story
  record whose changes the regeneration sat on top of. Never `git reset --hard` / `checkout --` / `clean` to
  tidy — another session's uncommitted work may be in the tree.
- Generate to `SpecScribeOutput/` (the default). Never `--output docs/live`.

### Project Structure Notes

- **New:** `web/` (Nuxt app: `package.json`, `nuxt.config.ts`, `app.vue`, `assets/tokens.css` [generated],
  `components/StatusBadge.vue|ChartPanel.vue|ListRow.vue|PageShell.vue`, `pages/design-system.vue`,
  `scripts/extract-tokens.mjs|check-tokens.mjs`, `CONVENTIONS.md`/`README.md`).
- **New:** `src/SpecScribe/DesignSystemTemplater.cs`; `tests/SpecScribe.Tests/DesignSystemTemplaterTests.cs`.
- **Update:** `src/SpecScribe/SiteNav.cs` (const + Help registration), `src/SpecScribe/SiteGenerator.cs`
  (`WriteDesignSystem`, call site). Golden-fingerprint constant in its test.
- **Reference, don't move:** `spike/nuxt-ir/` stays as the throwaway 23.1 probe. `web/` is its production
  successor; you may copy patterns (`nuxt.config.ts`, the scoped-token component shape) but the token **binding**
  changes from wholesale-`specscribe.css`-import to the extracted `tokens.css` layer.

### References

- [Epic 23 + Story 23.2 ACs](../planning-artifacts/epics.md) — §Epic 23, §Story 23.2 (execution order note 23.2→23.3→23.5→23.4).
- [Story 23.1 spike report](23-1-spike-report.md) — findings 4 (`:deep()`), 7 (payload duplication), gate row 23.2; Axis 2 (parity), Axis 4 (Node cost).
- [ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) — Vue + Nuxt 3, universal/SSR (Option B), north star relaxed for the presentation layer.
- [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) & [ADR 0012](../../docs/adrs/0012-plotly-hierarchy-chart-engine-and-standardized-explorer-component.md) — charts are client-rendered (Plotly), text twin is the no-JS contract; NFR-5 amended (JS-off may lose the visualization, never information or navigation).
- [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-7 (presentation tokens shared; host chrome host-owned).
- Tokens: `src/SpecScribe/assets/specscribe.css` `:root` (~lines 65–82); C# page models: `src/SpecScribe/HowToReadTemplater.cs`, `AboutSddTemplater.cs`; nav: `src/SpecScribe/SiteNav.cs`; generation seam: `src/SpecScribe/SiteGenerator.cs` (`WriteHowToRead`, `WriteOutput`); test models: `tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs`, `RenderParityTests.cs`.
- Memory: `specscribe-status-token-system`, `motion-token-system`, `css-comment-star-slash-silent-truncation`, `shared-main-concurrent-edit-loss-verify-after-edit`, `golden-diff-normalization-gotchas`, `generate-output-dir-is-specscribeoutput`.

## Dev Agent Record

### Agent Model Used

claude-opus-5 (dev-story, 2026-07-25)

### Debug Log References

- `npm run check:tokens` — RED before extraction (exit 1, "does not exist"), GREEN after; and proven to
  **catch** drift by hand-editing `--status-done` to `#00ff00` (reported `~ --status-done: #00ff00 ->
  var(--moss-light)`, exit 1) before restoring. A gate that has only ever been observed passing is not a gate.
- `npm run generate` — 13 routes prerendered, no errors.
- `npm run measure:payload` — reproduced identically across two builds (table below).
- Golden fingerprint captured twice, identically: `2050b586…`.
- Full suite: 2404 passed / 3 skipped. See "Test-suite flakes" below.

**2026-08-07 dev-story round.** Executed from worktree `.claude/worktrees/story-23-2-close-ir-content`
(branch `worktree-story-23-2-close-ir-content`), baselined on `main` at `07bdb79`.

- `dotnet build … --no-incremental` → succeeded (mandatory: `specscribe.css` is an embedded resource).
- `npm ci` → **failed** on `nuxt prepare`; re-run as `SPECSCRIBE_PACKAGE_BUILD=1 npm ci` → succeeded. The
  `@emnapi/runtime` lockfile mismatch recorded under **Deferred** did **not** recur.
- `npm run sync:assets && npm run build:package` → renderer artefact built.
- `SPECSCRIBE_RENDERER_DIR=<worktree>/web/.output dotnet run … -- generate --deep-git` →
  `[prerender] 1546 route(s) … errors=0`, `generated=801 skipped=17 errors=0`, `SpecScribeOutput/code/`
  populated with **284** pages.
- `npm run check:ir-content` → **OK, 1476 rules + 5 keyframes scoped, 3 shared, 15 runtime-body.**
- `npm run extract:ir-content` → re-derived; `git status --porcelain web/assets/` **empty** (byte-identical).
- Gate proven RED: hand-edited `shared-primitives.css` → `shared-primitives.css: +0, -0, ~1 changed / ~ .pill`,
  exit 1; restored clean.
- `--if-ir` proven in four states: no IR + flag → skip, exit 0 · no IR, no flag → exit 1 · IR + flag → real
  check, exit 0 · IR + flag + drift → exit 1.
- Wiring test proven RED by reverting `pregenerate` to its pre-fix shape → `× pregenerate runs
  check-ir-content with --if-ir`; restored.
- `npm run check` → all four gates green. `npm test` → 183 passed. `dotnet test SpecScribe.slnx` →
  **2978 passed / 0 failed / 3 skipped**, no preview servers running.
- ⚠️ **No browser tool in this session** — no live-browser verification performed. Stated, not skipped
  silently.

### Completion Notes List

**⟡ 2026-08-07 dev-story round — the two remaining blockers closed. Read this first; the notes below it are
from earlier rounds and are unchanged.**

**1. `check:ir-content` was never actually drifted, and the recorded diagnosis was wrong.** The ⛔ block said
this environment could not produce a full corpus. It can. What the third-pass run was missing is the
**`--deep-git` flag**, which `build-test-analyze.yml` documents as required *for this gate specifically* —
a shallow generate never emits the code-insights history/relationships tabs, the relationship-graph swatches
or the deep-analytics panels, so `selectorIsUsed` prunes every rule only those surfaces exercise. CI measured
that as `-182` rules; the third-pass run was `-181`. Run correctly here — `1546` routes prerendered,
`errors=0`, `284` code pages — the gate is **green at HEAD with `carriedRules: 1476` and `sharedRules: 3`**,
and a fresh `extract:ir-content` reproduced all four committed artifacts **byte-for-byte** (`git status
--porcelain web/assets/` → empty). Nothing needed regenerating; sibling commit `0b1f561` had already done it.
The third-pass session's refusal to commit a 1295-rule extraction was the **right call on wrong reasoning**.

Two secondary environment facts, now in CONVENTIONS §10 rather than left to be rediscovered: `npm ci` needs
`SPECSCRIBE_PACKAGE_BUILD=1` (else `postinstall: nuxt prepare` loads `nuxt.config.ts`, which reads an IR
manifest that cannot exist before the first generate), and a worktree needs `SPECSCRIBE_RENDERER_DIR` (the
repo-root search wants a `.git` *directory*; a worktree's is a *file*). The 23.5-owned `npm ci` lockfile
defect recorded under **Deferred** also **no longer reproduces** — commit `0b1f561` repaired it.

**2. The last open `[Review][Decision]` — owner chose "gate when an IR is present, warn otherwise".**
`check-ir-content.mjs` gained an opt-in `--if-ir`, wired into `prebuild` and `pregenerate`; `npm run check`
stays unflagged so CI still hard-fails on a missing IR. All four arms proven live (skip / unchanged failure /
real check / **red** on a hand-edited `shared-primitives.css`, reporting `~ .pill`). The decision logic was
extracted to `wantsIfIrSkip` + `irManifestPath` so it is unit-testable at all — the script does its work at
module scope under top-level await, so importing it in a test would run the whole gate. **11 tests added**,
of which the **wiring** block is the load-bearing part and was itself proven red: this story has twice shipped
a gate whose content was verified while its delivery path was not, and a helper that works but is never
invoked is that same defect wearing a passing test.

**⚠️ Still not done, and it is not mine to tick: the live-browser pass.** CLAUDE.md requires it for visual
work; this is visual work; and it has now been skipped for **four consecutive sessions** — this one had no
browser tool available at all. What *was* established statically is worth stating precisely, because it is
narrower than "verified":

- **Badge borders** — every stage binds the **same token name on both surfaces**: `done`/`active`/`review`/
  `ready`+`drafted` → their `--status-*`, `pending` **and** `unmapped` → `--status-pending`, `deferred` →
  `--status-deferred`, `retired` → `--border`. Since `tokens.css` is generated from that same `:root`, the
  values are identical by construction. This proves the **binding drift** the finding was about is gone.
  It does **not** show anyone the new `deferred` hairline (`#d4c4a8` → `#7a6250`, the visible change).
- **Skip link** — `.skip-link` now resolves to **exactly one rule**, in the unscoped `shared-primitives.css`,
  and is absent from `ir-content.css`; `PageShell.vue` carries no scoped copy, only a comment forbidding one.
  The (0,2,0) specificity tie is therefore **structurally** gone rather than won. The promoted rule is
  `position: fixed; z-index: 200`, which clears the sticky nav's `100`. Nobody has focused it on a scrolled
  IR page.

**Gates and suites, all green, no preview servers running:** C# **2978 passed / 0 failed / 3 skipped** —
clean, again consistent with this story's finding that the "rotating flake" is preview-server contention.
web vitest **183 passed** (was 165; +11 mine, the rest siblings'). `npm run check` green on all four:
`check:tokens` (45 tokens / 2 blocks), `check:ir-content` (1476 + 5 keyframes scoped, 3 shared, 15
runtime-body), `check:assets`, `check:parity` (24 pinned routes, 14 of 14 families, byte-identical).

---

**AC #4's result contradicts the hypothesis it was written to test — this is the story's most important finding.**

The 23.1 spike traced its 2.26× site weight entirely to hydration payload and hypothesised that
`<NuxtIsland>`/server components would avoid it. Measured on three routes rendering **identical markup from
identical data** (200 story-shaped rows through `ListRow` + `StatusBadge`), differing only in the data path:

| variant | HTML | payload | island JSON | total | vs control |
| --- | --- | --- | --- | --- | --- |
| A — `useAsyncData` | 125.5 KB | 44.5 KB | — | **170.0 KB** | 1.36× |
| B — `.server.vue` island | 125.1 KB | 3.1 KB | 121.8 KB | **250.0 KB** | 1.99× |
| C — build-time (control) | 125.4 KB | 0.1 KB | — | **125.4 KB** | 1.00× |

The island shape **loses, and loses to the thing it was supposed to beat.** It does drain the route payload
(44.5 KB → 3.1 KB), but then emits the island's entire rendered HTML *and its scoped CSS* a second time into
`__nuxt_island/<Component>_<hash>.json` for the client to re-fetch — verified by reading that file. For content
that is static once prerendered it is a payload *amplifier*.

A third variant was added that the story did not ask for, and it is the one 23.3 should use: **resolve IR data
at build time, at module scope, with no data composable at all** — 0.1 KB of payload on a 125 KB page. The IR
is available at build time by construction, so there is no reason for it to arrive through a composable that
exists to serve runtime fetching. Recorded with both caveats (island JSON dedupes across routes sharing a props
hash, which does not help per-page IR content; variant B remains right for genuinely per-request content, of
which Epic 23 has none).

**AC #5 is demonstrated, not just described.** `/design-system` carries the failing control and the working fix
side by side. Confirmed live in the browser: the `v-html`-injected node has **no** `data-v-*` attribute and
computes to the default ink colour under a plain scoped rule, while the `:deep()` variant computes to
`rgb(30, 74, 90)` with its 3px accent. That is the spike's finding reproduced as executable documentation.

**The C# page is built from the real primitives.** `DesignSystemTemplater` calls `StatusStyles.Badge`,
`ListRow.Render`/`Chip`/`PrimaryLink` and `Charts.Framed` rather than look-alike markup, and a test asserts the
**exact** primitive output appears — a gallery that mocked up its own badges could drift from the real ones,
and a design-system page that misrepresents the design system is worse than none. For the same reason nothing
on either page states a token's value: swatches show their colour by *using* `var(--status-*)`, pinned by
`DesignSystem_NeverStatesATokenValueAsALiteral`.

**One production seam extracted, one latent gap closed** (neither in the story's task list, both drift bugs):
- `StatusStyles.LegendWord` — the stage→word switch was private inside `LegendKey`. The design-system page is
  its second consumer, and a page whose subject *is* the status vocabulary is the last place a second copy of
  it belongs. Extracted; a test pins that both surfaces read the same seam.
- `HtmlRenderAdapter.QuickLinkFamily` had an explicit `family-help` label list that "Design System" was not in,
  so the classifier answered "planning" for a Help entry. Latent today (it is only consulted when a group has
  exactly one member, and Help always has five), but it is a second membership map beside `QuickLinks.Group`
  and would have been wrong the moment it mattered.

`Icons.ForConcept` needed a "Design System" glyph — caught by `IconsTests.ForConcept_EveryEmittedLabelHasAGlyph`,
which is a genuinely good guardrail: adding a nav label without an icon fails the build.

**Golden fingerprint** `91c3aeb4…` → `2050b5862e2c9fa8fa94f832739900487f3c84b18f16bb10d7697250d492063a`, stable
across two repeated runs. **Provenance (shared main):** the tree also carried a concurrent session's uncommitted
Story 20.x work — `specscribe.css` (+24 lines of `.ss-hierarchy-booting` anti-flash rules), `specscribe.js`,
`HierarchyExplorer`, `SunburstExplorer`, `DashboardViewBuilder`, `HtmlRenderAdapter`, and `SiteGenerator`'s new
`WriteTextWithRetry`. `specscribe.css` is copied to the output verbatim, so **that session's edits are inside
this hash as well as mine**; they could not be separated without destroying uncommitted work, which CLAUDE.md
forbids and I did not attempt. `SiteGenerator.cs` reported as modified-on-disk mid-edit; every edit was
grep-verified afterwards.

**Test-suite flakes (pre-existing, not from this story).** Each full run failed exactly one test, a *different*
one each time — `FileWatcherServiceTests.BurstOfSaves`, `SiteGeneratorTimelineTests` (×3),
`SiteGeneratorCommitDetailsTests`, `SiteGeneratorCodeMapTests.GenerateAll_DeterministicAcrossTwoRuns`,
`SiteGeneratorGitInsightsTests.GenerateAll_DeepGitLaterDisabled_…`. All pass in isolation, and all sit in the
file-write-contention family the concurrent session is *currently* adding `WriteTextWithRetry` for. Reported
rather than papered over: the honest reading is 2404 passed / 3 skipped with one rotating contention flake per
full-suite run, not a clean 2408.

**Verification.** The C# page was inspected live at `http://localhost:8102/design-system.html`: all ten stages
resolve to real token colours (`--status-done` → `rgb(107, 143, 98)`), each with icon **and** word — _corrected
2026-07-28: eight of the ten resolve to a token OF THEIR OWN; `unmapped` borrows `--status-pending` and
`retired` borrows `--status-deferred`, and there is no `--status-unmapped` or `--status-retired`. The page
states the sharing; this note did not._; `<main>`
carries 0 scripts, 0 `display:none`, 0 `hidden`, and 22 native `title=` fallbacks behind the `js-tip`
enhancement — so the page is fully readable with JS off. The Nuxt route was verified the same way at
`:8101/design-system/`, plus a mobile pass that found the token grid overflowing its panel; the reference grids
now stack under 34rem (page body never scrolled sideways either way, but "scroll the table to find the badge"
is a worse answer than stacking). **Screenshots were not available** — the Browser pane is not displayed in
this session, so `computer{action:"screenshot"}` times out; verification was by live computed styles and DOM
geometry instead, which is what catches containment leaks and sub-pixel collapse, but the owner's verify round
should still look at both pages.

**Not done, deliberately:** `web/` is not in `SpecScribe.slnx` and not wired into `specscribe generate` (23.5
owns packaging); `spike/nuxt-ir/` is left untouched as the throwaway 23.1 probe.

> **CORRECTED 2026-07-28 (code re-review).** This paragraph originally continued: *"The `web/**` sources produce
> no `code/web/**` pages in the generated portal — `.vue`/`.mjs`/`.ts` are outside the code-page extension set,
> which is existing behaviour and out of scope here."* **That is false.** There is no code-page extension set:
> `SiteGenerator.EnumerateCodeFiles` feeds the Code Map from `GitMetrics.TryListFiles` (plain `git ls-files`)
> with no extension filter, skipping only binary/unreadable files. `SpecScribeOutput/code/web/` exists on disk
> and holds `app.vue.html`, `nuxt.config.ts.html`, `package-lock.json.html` and nine more — so the 11,291-line
> lockfile has its own code page. What was recorded as a deliberate non-goal was a wrong belief; what to do
> about it is an open decision on this story (it is a product-behaviour change, not a `web/`-local one).

### File List

**New — `web/` (Nuxt app):**
- `web/.gitignore`
- `web/package.json`
- `web/package-lock.json`
- `web/nuxt.config.ts`
- `web/app.vue`
- `web/README.md`
- `web/CONVENTIONS.md`
- `web/assets/tokens.css` *(generated by `extract:tokens`; committed so the drift gate has a baseline)*
- `web/assets/base.css`
- `web/components/StatusBadge.vue`
- `web/components/ChartPanel.vue`
- `web/components/ListRow.vue`
- `web/components/PageShell.vue`
- `web/components/MeasureRows.server.vue`
- `web/pages/index.vue`
- `web/pages/design-system.vue`
- `web/pages/measure/async.vue`
- `web/pages/measure/island.vue`
- `web/pages/measure/static.vue`
- `web/utils/measure-rows.ts`
- `web/scripts/tokens-lib.mjs`
- `web/scripts/extract-tokens.mjs`
- `web/scripts/check-tokens.mjs`
- `web/scripts/measure-payload.mjs`

**New — C#:**
- `src/SpecScribe/DesignSystemTemplater.cs`
- `tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs`

**Modified — C#:**
- `src/SpecScribe/SiteNav.cs` — `DesignSystemOutputPath` const + Help group/quick-link registration
- `src/SpecScribe/SiteGenerator.cs` — `WriteDesignSystem(nav)` + always-written call site
- `src/SpecScribe/StatusStyles.cs` — extracted `LegendWord` from `LegendKey`
- `src/SpecScribe/Icons.cs` — "Design System" palette glyph
- `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` — "Design System" added to the `family-help` classifier

**Modified — tests:**
- `tests/SpecScribe.Tests/SiteNavTests.cs` — nav-order arrays, Help group membership + child count
- `tests/SpecScribe.Tests/RenderParityTests.cs` — nav-target arrays
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — golden inventory + regenerated fingerprint
- `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` — `CapturePages` includes `design-system.html`

**Modified — tooling:**
- `.claude/launch.json` — preview servers for the Nuxt prerender output and the generated portal

#### Added by the 2026-07-29 review follow-up (the three open decisions)

**New:**
- `docs/adrs/0029-unscoped-shared-primitive-layer.md` — **Proposed**; amends ADR 0018 property 3
- `web/assets/shared-primitives.css` — *generated by `extract:ir-content`; committed so the gate has a baseline*

**Modified — C#:**
- `src/SpecScribe/assets/specscribe.css` — four `--status-*-bg` fill tokens; the four `.status-badge.<stage>`
  rules, three `.epic-status.*`, `.status-badge.evidence-pill.tests-pass` and `.sprint-flag` bound to them
- `src/SpecScribe/DesignSystemTemplater.cs` — `StageFillTokens` map (`internal`, so tests derive from it);
  each stage's token note now names its paired fill

**Modified — tests:**
- `tests/SpecScribe.Tests/StylesheetTests.cs` — new structural guard
  `Stylesheet_StageFillsOnBadgeFamilies_AreTokensNotInlineLiterals`
- `tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs` — `DesignSystem_BypassesApplyReferenceLinks`
  given a real positive control; new `DesignSystem_DocumentsEveryStageFillToken_AndNamesNoneThatIsInvented`
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — fingerprint `501ee958…` → `22c921de…` + provenance

**Modified — `web/`:**
- `web/scripts/ir-content-lib.mjs` — `SHARED_PRIMITIVES` allowlist, `isSharedPrimitive`, `OUT_SHARED_CSS`
- `web/scripts/ir-content-build.mjs` — per-selector partition into scoped/unscoped, `sharedCss`, manifest
  `sharedPrimitives` block + the handoff entry in `rules`
- `web/scripts/extract-ir-content.mjs` — writes the second sheet; reports the allowlist
- `web/scripts/check-ir-content.mjs` — gates both sheets in one run and names which drifted
- `web/scripts/tokens-lib.mjs` — `REQUIRED_TOKENS` + the four fill tokens
- `web/assets/tokens.css`, `web/assets/ir-content.css`, `web/assets/ir-content.manifest.json` — *regenerated*
- `web/components/StatusBadge.vue` — the four stage fills bound to the new tokens
- `web/components/ListRow.vue` — the hand-retyped `.pill` block **deleted**; `flex-shrink` only
- `web/pages/design-system.vue` — `fillFor()`; swatch captions name the accent *and* the fill
- `web/nuxt.config.ts` — imports `shared-primitives.css` before `ir-content.css` (order documented)
- `web/CONVENTIONS.md` — new **§10a** (the shared-primitive channel + admission test) and a pointer from §10
- `web/test/ir-content-lib.test.mjs` — `isSharedPrimitive` cases; the shared-sheet block (incl. the
  non-vacuity guard); the two existing manifest tests updated for the new field and the two `carried: false`
  causes

**Modified — docs:**
- `docs/adrs/README.md` — ADR 0029 index entry

#### Added by the 2026-08-07 dev-story round (the CSS-gate decision)

**Modified — web:**
- `web/scripts/check-ir-content.mjs` — the `--if-ir` precondition branch (skip loudly when no IR exists)
- `web/scripts/ir-content-lib.mjs` — new exports `wantsIfIrSkip`, `irManifestPath`; `node:path` import
- `web/package.json` — `prebuild` and `pregenerate` now also run `check-ir-content.mjs --if-ir`
- `web/CONVENTIONS.md` — §10 gained the `--deep-git` regeneration warning, the full corpus recipe
  (`SPECSCRIBE_PACKAGE_BUILD` / `SPECSCRIBE_RENDERER_DIR`), and the build-lifecycle hook's contract
- `web/test/ir-content-lib.test.mjs` — 11 tests: `wantsIfIrSkip` (3), `irManifestPath` (3, incl. an
  adapter-correspondence assertion), and the lifecycle **wiring** block (5)

**Not modified, and deliberately so:** no generated asset changed. `web/assets/{ir-content,shared-primitives,
runtime-body}.css` and `ir-content.manifest.json` were re-derived from a full `--deep-git` corpus and came
back **byte-identical**, which is the evidence closing the ⛔ item — so there is nothing to commit for it.

## Change Log

| Date | Change |
| --- | --- |
| 2026-08-07 | **Dev-story round — both blockers the third code-review pass left are closed; status `in-progress` → `review`.** (1) **`check:ir-content` was never drifted.** It is green at HEAD with `carriedRules: 1476` / `sharedRules: 3`, and a fresh extraction from a full corpus reproduced all four committed artifacts **byte-for-byte** — nothing needed regenerating (sibling `0b1f561` had already done it). ⚠️ **The recorded diagnosis was wrong and is corrected in place:** the third-pass shortfall was a missing **`--deep-git`** flag, not an environment that "cannot produce a full corpus". CI documents the flag as required *for this gate* and measured its cost as `-182` rules; the third-pass run was `-181`. Its refusal to commit a 1295-rule extraction was the right call on wrong reasoning. Two secondary environment facts (`SPECSCRIBE_PACKAGE_BUILD=1` for `npm ci`, `SPECSCRIBE_RENDERER_DIR` in a worktree) are now in CONVENTIONS §10; the 23.5-owned `npm ci` lockfile defect no longer reproduces. (2) **The last open `[Review][Decision]` answered and implemented** — owner chose "gate when an IR is present, warn otherwise": `check-ir-content.mjs --if-ir`, wired into `prebuild`/`pregenerate`, with `npm run check` left unflagged so CI still hard-fails. All four arms proven live including **red** on a hand-edited `shared-primitives.css`; the logic was extracted to `wantsIfIrSkip`/`irManifestPath` to be testable at all, and 11 tests added — the **wiring** block being the load-bearing half, itself proven red, because this story has twice shipped a gate whose delivery path nothing read. Suites: C# **2978/0/3**, web vitest **183**, `npm run check` green on all four gates (incl. `check:parity`, 24 routes / 14 families byte-identical). ⚠️ **The live-browser pass is STILL outstanding — a fourth consecutive session without it** (no browser tool here). Token bindings were verified statically on both surfaces and the skip link is provably a single unscoped rule, but the `deferred` border change and a focused skip link on a scrolled page have not been *seen*. That is the owner's verify round. |
| 2026-07-29 | **Review follow-up — the three open `[Review][Decision]` items closed; the story's checkbox surface is now fully clear.** (1) The held stylesheet decision applied now that its blocker landed: four `--status-*-bg` fill tokens, bound by the four `.status-badge.<stage>` rules **and** by the mirrors that already claimed to mirror them (owner widened the scope), so the Vue badge's flat `--parchment` substitution is gone — all eight stages now compute identically on both surfaces. (2) The `.pill` second definition **deleted**, not corrected again, via a new UNSCOPED `shared-primitives.css` layer bounded by a one-entry allowlist — **[ADR 0029](../../docs/adrs/0029-unscoped-shared-primitive-layer.md) proposed unasked** because it amends ADR 0018's ratified scoping property, and it states the cost (containment is no longer absolute) rather than minimising it. (3) `DesignSystem_BypassesApplyReferenceLinks` made non-vacuous with **none** of the three recorded options — `ApplyReferenceLinks` is five linkifiers, and the demo chip's "Epic 1" gives `StoryEpicLinkifier` a positive control needing no fixture and no new seam. All four new/changed gates proven RED. ⚠️ Proving the partition mechanism exposed a **vacuity bug in this pass's own tests** (an empty allowlist skipped every loop body), now guarded — the same defect class the re-review existed to find. Fingerprint `501ee958…` → `22c921de…`, stable across two `--no-incremental` runs, **provenance clean** (self-contained temp fixture; the only modified `src/` files were this change's). Suite 2814/0/3 with no flake; web vitest 106. A **pre-existing** `check:assets` failure at HEAD (stale gitignored `web/public/specscribe.js`) was fixed and disclosed as not caused here. |
| 2026-07-25 | Story 23.2 implemented. `web/` Nuxt app established with a generated token bridge + drift gate, four scoped-CSS primitives, and a `/design-system` route. AC #4's payload experiment measured and **contradicted the spike's hypothesis** — server components cost 1.99× vs async-data's 1.36×; build-time data (1.00×) is the recorded recommendation for 23.3. C#-generated `design-system.html` shipped and wired into the Help nav. Golden fingerprint regenerated to `2050b586…` on top of a concurrent session's uncommitted Story 20.x work. |
