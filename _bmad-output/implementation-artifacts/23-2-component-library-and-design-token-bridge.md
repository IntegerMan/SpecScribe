---
baseline_commit: cd7f30255bb07112332c0876f4335e6b77ca9f4d
---

# Story 23.2: Component Library + Design-Token Bridge

Status: review

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

### Review Findings

_Code review 2026-07-26. Scoped to this story's File List and declared symbols — sibling stories 20.5,
20.7 and 22.2 share commit `261b300` and are excluded. Baseline `cd7f302`._

- [ ] [Review][Patch] **`web/` joined the repo without deciding which analysis surfaces it joins** — _Owner decision 2026-07-26: **analyze it now** — add a Node CI step + `sonar.javascript.lcov.reportPaths`, and author the `web/` test suite that makes the coverage figure real (scoped to `tokens-lib.mjs`/`check-tokens.mjs` plus component smoke tests). Code Map ingestion of `web/package-lock.json` is handled by the same change._ Two symptoms, one call. (a) `.github/workflows/build-test-analyze.yml:130` excludes `spike/**`, `tools/**`, `extension/node_modules/**` but **not** `web/**`, and sets no `sonar.javascript.lcov.reportPaths` — so the next scan pulls ~1,800 lines of untested first-party `.vue`/`.mjs`/`.ts` into Clean-as-You-Code at 0% coverage and reds the gate Story 25.1 just turned green. (b) `SiteGenerator.EnumerateCodeFiles` (`SiteGenerator.cs:4477`) feeds the Code Map from plain `git ls-files` with **no extension filter** — so all 24 new `web/**` files enter it, including the 11,132-line `web/package-lock.json`, which will dominate the Config bucket of SpecScribe's own dogfood portal. The Completion Notes' stated reason (`.vue`/`.mjs`/`.ts` "are outside the code-page extension set") is factually wrong: there is no such set. Options: exclude `web/**` from both until 23.5 packages it / analyze it now and add a Node CI step + lcov / exclude from Sonar only.
- [ ] [Review][Patch] **`StatusBadge.vue` drops the icon — half of the UX-DR17 channel the component claims to enforce by shape** — _Owner decision 2026-07-26: **defer the glyph to 23.3 with the IR**, where the stage→icon mapping already has a data source. The patch here is to stop the component claiming a guarantee it does not provide: drop the "enforced BY THE COMPONENT'S SHAPE" assertion from its header and record the 23.3 dependency in CONVENTIONS.md._ `StatusStyles.Badge` (`StatusStyles.cs:356`) emits `{Icon(iconClass)}{label}` and documents the rule as "color + icon + word, never icon-only"; `web/components/StatusBadge.vue:41` renders text only, with no icon prop or slot, while its header asserts "UX-DR17 is enforced BY THE COMPONENT'S SHAPE". `ready` and `drafted` share a border colour and are distinguished by glyph in the portal. Needs a call on where the Vue glyph comes from (inline SVG sprite mirroring `Icons`, an IR-supplied glyph in 23.3, or a slot the caller fills).
- [ ] [Review][Patch] **`StatusBadge.vue` re-authors four stage backgrounds, so Vue and portal badges render different tints** — _Owner decision 2026-07-26: **tokenize the four literals in `specscribe.css`**, re-run `extract:tokens`, and bind the Vue component to the new tokens so the bridge actually carries the values. Moves the golden fingerprint._ `specscribe.css:3042-3046` carries literal hexes with no token: `.done #e8f0e4`, `.active #e0ecea`, `.review #d9e6ea`, `.ready/.drafted #f5ecd4`. `StatusBadge.vue:66-89` replaces all four with `var(--parchment)` (`#f4ead5`), and flips `.is-pending`/`.is-deferred` borders from `var(--border)` to `var(--status-*)`. Token discipline is honoured — the bridge structurally *cannot* carry an untokenized literal — but the outcome is the drift Epic 23 exists to prevent, and it is recorded nowhere. Options: tokenize the four literals in `specscribe.css` and re-extract (changes the shipped portal) / accept and record the divergence / drop the stage backgrounds from the Vue primitive until 23.3.
- [ ] [Review][Patch] Token extractor and drift gate are blind to every `:root` block but the first [web/scripts/tokens-lib.mjs:71] — `specscribe.css` has **three** top-level `:root` rules: line 6 (extracted), line 5403 (`--impact-lvl-1`…`-5`), line 5839 (`--nav-offset`, inside a max-width media query). `check:tokens` prints `OK — 36 tokens in sync` while two families never cross. Make the extractor detect additional top-level `:root` rules and fail loudly; whether the impact-map ramp should cross is then 23.3's explicit call rather than a silent omission.
- [ ] [Review][Patch] AC #1's "build-time drift check" is not run by any build [web/package.json:12] — `check:tokens` exists, works, and provably catches drift, but `web/package.json` has no `prebuild`/`pregenerate` hook and `.github/workflows/build-test-analyze.yml` runs no Node step at all. Add the npm lifecycle hook so `nuxt build`/`generate` cannot proceed on drifted tokens.
- [ ] [Review][Patch] Vue status vocabulary drops `unmapped` and teaches the wrong word for it [web/pages/design-system.vue:27] — `StatusStyles.LegendStages` has **ten** stages; the Vue `stages` array and `StatusStage` union (`StatusBadge.vue:16`) have nine, and the aside that stands in for the missing one states the word as *"Unmapped"* where `StatusStyles.LegendWord("unmapped")` → `RequirementLabel(Unmapped)` → **"Not yet mapped"** (`StatusStyles.cs:180`). The `ranking="Nine canonical stages"` caption (`design-system.vue:61`) is downstream of the same omission.
- [ ] [Review][Patch] The Vue page names `--status-retired`, a token that does not exist [web/pages/design-system.vue:67] — the swatch caption interpolates `--status-{{ s.stage }}` unconditionally; `tokens.css` declares no `--status-retired`, and the page's own CSS concedes it (`.swatch-retired { background: var(--parchment-dark) }`). The C# sibling has exactly this guard (`DesignSystemTemplater.cs:139-144`); mirror it.
- [ ] [Review][Patch] `ListRow.vue` chips drop the `pill` class and re-type its look [web/components/ListRow.vue:42] — `ListRow.Chip` (`ListRow.cs:73`) emits `class="list-row-chip pill"`, and every visual property comes from `.pill` (`specscribe.css:1099`): Courier, `letter-spacing: 0.03em`, `padding: 0.2rem 0.7rem`, `--warm-white`, `--ink-faded`. The Vue chip re-declares itself serif, no letter-spacing, `0.1rem 0.55rem`, `--parchment`, `--ink-light` — a second hand-typed definition inside a file whose header calls itself "the Vue counterpart of `ListRow.Render`".
- [ ] [Review][Patch] The Vue reduced-motion block does not neutralize `animation-delay` [web/assets/base.css:54] — it sets `animation-duration`, `animation-iteration-count`, `transition-duration`, `scroll-behavior` and nothing else. `specscribe.css:6449` handles precisely this case with `animation: none`, and its comment explains why: `fill-mode: both` holds an element invisible through its delay. A 200-row list staggered by `--motion-stagger` would appear over ~8s of blank page for a reduce-motion reader. CONVENTIONS.md §6 forbids a per-SFC fix, so there is no second place to catch it.
- [ ] [Review][Patch] The C# page renders Retired with the `deferred` badge class, not the real one [src/SpecScribe/DesignSystemTemplater.cs:175] — `BadgeBody`/`StatusBody` remap `retired => deferred` and pass the remapped class into `StatusStyles.Badge`, emitting `class="status-badge deferred"`. No real caller does that (`StatusStyles.LegendKey:391` remaps only `unmapped`), and `.status-badge.retired` is its own rule (`specscribe.css:3052`). Byte-identical to `.deferred` today, so nothing looks wrong — but the class doc's load-bearing claim is "built from the ACTUAL primitives, never look-alike markup". Keep the swatch remap; pass `stage` as the badge class.
- [ ] [Review][Patch] The `Window` slot is filled with prose and component filenames on the page that teaches the frame [src/SpecScribe/DesignSystemTemplater.cs:104] — `Charts.cs:139` documents the slot as "the ONE place a **numeric analysis window** is rendered"; the C# page passes `"the panel you are reading"` and `design-system.vue:99,109,137,150` pass `"StatusBadge.vue"`, `"ListRow.vue"`, `"ChartPanel.vue"`. A 23.3 author copies the pattern from the page whose stated job is to teach it.
- [ ] [Review][Patch] `ListRow.vue` defines an `accent-review` modifier the portal has no counterpart for [web/components/ListRow.vue:16] — `specscribe.css:6970-6972` defines exactly three accents (`done`, `pending`, `deferred`); the comment at 6965 explains the neutral default is deliberate. A fourth accent is a design-system change, not a port.
- [ ] [Review][Patch] Two new tests are satisfied by construction and cannot fail [tests/SpecScribe.Tests/SiteGeneratorDesignSystemTests.cs:79] — (a) `Assert.Contains($"--status-{stage}", html)` asserts a string the templater derives from the same variable, so adding an eleventh stage to `LegendStages` with no matching token in `:root` ships a blank swatch documenting a nonexistent token, green. Assert against the tokens declared in the generated stylesheet instead. (b) `DesignSystem_BypassesApplyReferenceLinks` (line 189) asserts absence of `<abbr`/`ref-chip`, but `AbbreviationExpander` only wraps `FR/NFR/AC/ADR/PRD` and `ReferenceChipRenderer` needs `[[wiki]]`/`file:line` — none appear on this page, so the guard passes with or without the bypass. Assert the write path directly instead.
- [ ] [Review][Patch] Token-bridge guard and diagnostics have three gaps [web/scripts/tokens-lib.mjs:30] — (a) `REQUIRED_TOKENS` omits five tokens the shipped components actually bind to: `--parchment-dark`, `--ink-faded`, `--gold`, `--moss`, `--rust-light`, so a rename of any of them passes the anti-latch guard and silently blanks badge and panel styling. (b) `check-tokens.mjs:37` calls `tokenMap(actual)` outside any try/catch, so a committed `tokens.css` whose `:root {` header was removed throws an uncaught stack trace naming the *source* file as the culprit. (c) `tokenMap`'s regex requires a trailing `;`, which CSS permits the final declaration to omit — such a drift falls into the `=== 0` branch and is misreported as "token values identical — the difference is comments".
- [ ] [Review][Patch] `ChartPanel.vue` grows frame anatomy `Charts.Framed` does not have [web/components/ChartPanel.vue:29] — `<section class="chart-panel">` where `Charts.Framed` (`Charts.cs:167`) emits `<div>`, plus a `.chart-panel-body` wrapper with no rule in the SFC and no counterpart in `specscribe.css`, and a `.chart-panel-legend` slot the C# frame has no concept of. The header claims "a panel authored in Vue cannot grow a different anatomy from one authored in C#"; any `.chart-panel > …` rule stops matching once a surface migrates.
- [ ] [Review][Patch] The two design-system pages already disagree on the motion vocabulary [src/SpecScribe/DesignSystemTemplater.cs:35] — all five role sentences are hand-typed twice and all five differ (`"Hover and opacity changes — the shortest deliberate movement on the page."` vs `design-system.vue:41` `"Hover and opacity feel — the shortest deliberate change."`). The duplication is owner-accepted until 23.4; divergence on day one is not. Make the Vue copy verbatim.
- [ ] [Review][Patch] `measure:payload` charges the whole shared island directory to variant B and reports a missing route as free [web/scripts/measure-payload.mjs:39] — `island = v.route.endsWith('island') ? islandBytes : 0` sums every file under `__nuxt_island/`, which the script's own comment notes is keyed by component+props hash and shared. Add one `.server.vue` anywhere in `web/` and the published 1.99× ratio moves without variant B changing. Separately, every size lookup ends `?? 0` and the only guard is `rows.every(r => r.total === 0)`, so a single absent route prints `0.00x` — reading as "this shape is free", inverting AC #4's conclusion.
- [ ] [Review][Patch] `ListRow.vue` keys chips by their own text [web/components/ListRow.vue:42] — `:key="chip"` over a `string[]` with no uniqueness constraint; `:chips="['3 tasks', '3 tasks']"` produces duplicate keys and node reuse on reorder.
- [ ] [Review][Patch] `.claude/launch.json` now carries eight preview servers, four pointing at the same directory [.claude/launch.json:65] — `specscribe-output` (8099), `related-work-20-3` (8094), `specscribe-output-review` (8097) and the new `design-system-23-2` (8102) all serve `SpecScribeOutput`. Per-story naming guarantees the list only grows, in a file every session shares.
- [x] [Review][Defer] `WriteTextWithRetry` is undeclared sibling work with two defects of its own [src/SpecScribe/SiteGenerator.cs:4439] — deferred, pre-existing. The File List declares only `WriteDesignSystem` + call site, but the diff also adds `WriteTextWithRetry` and converts `index.html`'s write, annotated `[Story 20.5 owner round]`. Its defects belong to 20.5, not here: (a) its doc claims parity with `CopyEmbeddedAsset`, but it omits the tmp-file + atomic `File.Move` half that a Story 5.3 review-fix added precisely because truncate-then-fail leaves a corrupt file; (b) the catch filter makes no transient/permanent distinction, so a read-only target burns four attempts and still destroys the previous good page; (c) only one of the generator's write sites was converted — `WriteOutput` (`SiteGenerator.cs:3017`), which every other page including the new `design-system.html` rides, still calls bare `File.WriteAllText`.

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

### Completion Notes List

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
resolve to real token colours (`--status-done` → `rgb(107, 143, 98)`), each with icon **and** word; `<main>`
carries 0 scripts, 0 `display:none`, 0 `hidden`, and 22 native `title=` fallbacks behind the `js-tip`
enhancement — so the page is fully readable with JS off. The Nuxt route was verified the same way at
`:8101/design-system/`, plus a mobile pass that found the token grid overflowing its panel; the reference grids
now stack under 34rem (page body never scrolled sideways either way, but "scroll the table to find the badge"
is a worse answer than stacking). **Screenshots were not available** — the Browser pane is not displayed in
this session, so `computer{action:"screenshot"}` times out; verification was by live computed styles and DOM
geometry instead, which is what catches containment leaks and sub-pixel collapse, but the owner's verify round
should still look at both pages.

**Not done, deliberately:** `web/` is not in `SpecScribe.slnx` and not wired into `specscribe generate` (23.5
owns packaging); `spike/nuxt-ir/` is left untouched as the throwaway 23.1 probe. The `web/**` sources produce no
`code/web/**` pages in the generated portal — `.vue`/`.mjs`/`.ts` are outside the code-page extension set, which
is existing behaviour and out of scope here.

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

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-25 | Story 23.2 implemented. `web/` Nuxt app established with a generated token bridge + drift gate, four scoped-CSS primitives, and a `/design-system` route. AC #4's payload experiment measured and **contradicted the spike's hypothesis** — server components cost 1.99× vs async-data's 1.36×; build-time data (1.00×) is the recorded recommendation for 23.3. C#-generated `design-system.html` shipped and wired into the Help nav. Golden fingerprint regenerated to `2050b586…` on top of a concurrent session's uncommitted Story 20.x work. |
