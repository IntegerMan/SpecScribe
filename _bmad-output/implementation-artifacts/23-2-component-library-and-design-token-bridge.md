# Story 23.2: Component Library + Design-Token Bridge

Status: ready-for-dev

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

- [ ] **Task 1 — Scaffold the `web/` Nuxt app** (AC: #3)
  - [ ] Create repo-root `web/` with `package.json` (devDependencies: `nuxt ^3.14`, `vue ^3.5`, `vue-router ^4.4` — mirror `spike/nuxt-ir/package.json`), `nuxt.config.ts` (`ssr: true`, `telemetry: false`, `devtools.enabled: false`, full prerender per ADR 0009 Option B), and `app.vue`.
  - [ ] Confirm `node_modules/` is already covered by `.gitignore` (it is — global `node_modules/` rule) and that `web/` is **not** added to `SpecScribe.slnx`.
  - [ ] Do **not** wire `web/` into `specscribe generate` — packaging is Story 23.5, sequenced ahead of 23.4.
- [ ] **Task 2 — Token bridge: extract `tokens.css` from the C# stylesheet** (AC: #1)
  - [ ] Write `web/scripts/extract-tokens.mjs` that reads the `:root { … }` block from `src/SpecScribe/assets/specscribe.css` and emits `web/assets/tokens.css` containing exactly those custom properties (status, motion, and brand palette). Keep the extraction a pure copy — no re-typed literals.
  - [ ] Import `tokens.css` (and only `tokens.css`) as the token source in the Nuxt app; author every component's own rules with `<style scoped>`.
  - [ ] Add a **drift check** (`web/scripts/check-tokens.mjs`, wired as `npm run check:tokens`) that re-extracts and diffs against the committed `web/assets/tokens.css`, exiting non-zero on divergence. Document that `npm run extract:tokens` must be re-run after any token change in the C# stylesheet.
- [ ] **Task 3 — Build the proof primitives as scoped-CSS Vue components** (AC: #3)
  - [ ] `StatusBadge.vue` — the six-stage `--status-*` badge, status conveyed by **label text**, not color alone (UX-DR17). Model semantics on `.status-badge` in `specscribe.css`.
  - [ ] `ChartPanel.vue` (or `FramedPanel.vue`) — the `chart-panel` + `Charts.Framed`/`ChartMeta` frame (title, analysis window, framing sentence, legend slot) per Story 10.2.
  - [ ] `ListRow.vue` — the unified `ListRow` primitive (`--list-row-accent`; Story 10.8 grammar).
  - [ ] `PageShell.vue` — the page shell (header/main/footer chrome), reused by the design-system route.
  - [ ] Every color/timing value comes from a `var(--…)` token; no primitive re-types a token value.
- [ ] **Task 4 — Measure the `<NuxtIsland>` / server-component shape** (AC: #4)
  - [ ] Render at least one representative primitive both ways (async-data path vs `<NuxtIsland>`/server-component) under `nuxt generate`; record the output-weight delta.
  - [ ] Write the measured recommendation into the conventions doc so 23.3 uses the payload-avoiding shape by default.
- [ ] **Task 5 — Document scoped-SFC / CSS-module conventions** (AC: #2, #5)
  - [ ] `web/CONVENTIONS.md` (or a section in `web/README.md`) covering: tokens.css is the only token import and is generated (never hand-edit); `<style scoped>` for template-authored markup; **`:deep()`/global sheet is required to style `v-html`'d IR content** (the spike's load-bearing finding — 23.3 depends on it); the measured `<NuxtIsland>` payload recommendation from Task 4; AD-7 boundary (SpecScribe owns semantic tokens, host owns chrome).
- [ ] **Task 6 — Nuxt `/design-system` route** (AC: #3, #6)
  - [ ] `web/pages/design-system.vue` renders every primitive in every relevant status/motion state, using `PageShell.vue`. This is the worked example for the conventions doc and the future portal design-system surface.
  - [ ] Verify live in a browser (`npm run dev` and/or `nuxt generate` + serve): tokens resolve (`var(--status-done)` → the moss value), reduced-motion honored, status readable by name with JS off.
- [ ] **Task 7 — C#-generated `design-system.html`, wired into Help nav** (AC: #6)
  - [ ] Add `DesignSystemTemplater.cs` (model on `HowToReadTemplater.cs` / `AboutSddTemplater.cs`): renders the token families and primitive gallery as static, JS-optional HTML using the existing `specscribe.css` classes. Status by name, non-color (UX-DR17). Reduced-motion respected (the motion tokens are already neutralized by the sheet's reduce block).
  - [ ] Add `SiteNav.DesignSystemOutputPath = "design-system.html"` and register it in the **Help** group and Help quick-links in `SiteNav.Build` (see the `help.Add(...)` / `quickLinks.Add(...)` block).
  - [ ] Add `WriteDesignSystem(nav)` to `SiteGenerator`, called from the always-written page block beside `WriteHowToRead(nav)` (~line 460), going through `WriteOutput` (so SPA/webview `CapturePages` picks it up). Write it **directly**, without `ApplyReferenceLinks`, mirroring How-to-read/About (a token-vocabulary page must not self-expand its own terms).
  - [ ] Regenerate the golden fingerprint constant (it **will** move — new page + new nav entry on every page). Confirm the hash is stable across two repeated runs before locking it (concurrent-main hazard — see Dev Notes).
- [ ] **Task 8 — Tests** (AC: #1, #6)
  - [ ] C#: a `DesignSystemTemplaterTests` / `SiteGeneratorDesignSystemTests` (model on `SiteGeneratorHowToReadTests.cs`) asserting the page renders, carries the token/primitive content, and is JS-optional; a nav coherence assertion that the Help entry resolves; RenderParity coverage (`RenderParityTests.cs` — HTML ≡ webview capture) since the page rides `WriteOutput`.
  - [ ] web/: `npm run check:tokens` passes; the design-system route prerenders without error.
  - [ ] Full suite green; golden fingerprint regenerated and confirmed stable.

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

### Debug Log References

### Completion Notes List

### File List
