---
baseline_commit: b5f2514ab13d65e0c0811c0eddc5198e48a8dee4
---

# Story 6.7: JSON + Client-Renderer (SPA) Delivery Adapter

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer generating a portal for a large repository,
I want an optional delivery form that emits a JSON data layer plus a small client-side renderer instead of thousands of static HTML files,
so that file-count-heavy projects (Epic-7 scale) stay manageable while rendering remains in the C# core and the accessible static-HTML fallback is preserved.

## Scope Decision — READ FIRST (confirm or veto before dev)

This story is **seated by [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) (Accepted)** as an **additive** delivery form — Architecture **B** in that ADR ("JSON + SPA delivery adapter — C# still renders"). It is the **third concrete `IRenderAdapter`** after `HtmlRenderAdapter` (`html`) and `WebviewRenderAdapter` (`webview`). The house rule from the Epic 3 retro applies (memory: [[create-story-elicit-visual-intent]]): **the defining decision is made here, not deferred to the dev and corrected at review. If you disagree with the scope below, raise it before writing code.**

### The one defining decision: this delivers a *whole-site* file-count win, not a dashboard/epics-only proof

The story's value is entirely in the **"So that"** — cutting **thousands** of static HTML files (Epic-7 scale reaches ~1,060 files on *this* small repo, thousands-to-tens-of-thousands on large repos — [ADR 0006 evidence base](../../docs/adrs/0006-delivery-architecture-and-distribution.md)) down to a **bounded handful**. A dashboard-plus-epics-only adapter (5 files → 5 files) would deliver the *form* but **zero file-count win** — a hollow story. **So 6.7 consolidates the ENTIRE generated site**, every page type, into few files.

**This is affordable NOW without the deferred "route every page body through the 6.1 contract" rewrite, because of one fact this story turns into a seam:** every generated page — dashboard, epics, docs, sprint, requirements, ADRs, git, commits, retros, about, diagnostics — carries **exactly one `<main id="main-content"> … </main>` landmark** (Story 1.4 AC #1, the universal skip-link target; verified across all 12 templaters). That landmark, plus the shared nav/breadcrumb chrome the client renders once, is the **content region** the SPA swaps. The adapter obtains each page's content region from the **render pipeline's own output** (the string `SiteGenerator` already holds before `File.WriteAllText`), **not** by reading back generated `.html` files and **not** by re-parsing `.md` — so AD-1/AD-2 hold (see the "Is landmark-slicing scraping?" note in Dev Notes). Future Epic-7 pages flow through the same write seam and are covered for free.

**The one-line test for "is this in scope?":** if it is about *emitting the JSON data layer + the client renderer + the noscript fallback as an opt-in output form that leaves the static site byte-identical*, it's in. If it is about *shrinking bytes* (that needs the deferred TS chart port — ADR 0006 option D), *re-modeling any page body into new section view models*, *host theming*, or *the VS Code webview*, it's out.

**IN scope**

- **A third concrete `IRenderAdapter`** (suggested `Id = "spa"`; final name the dev's call) in the rendering core (`namespace SpecScribe;`, single project) that produces the JSON+client-renderer output form. Rendering **stays in C#**; the client renderer never re-renders content or re-parses anything — it fetches, injects, and navigates (AC #1).
- **Whole-site consolidation into a bounded, small set of files**: a **manifest** (site title + the nav graph + the ordered page index: path, title, breadcrumb/drill relationships) + the page **content regions** grouped into a **few** JSON chunks (NOT one-file-per-page — that defeats the win; NOT one multi-MB monolith — that bloats first load at Epic-7 scale). Charts ship as **pre-rendered inline SVG** inside the content regions exactly as today (AC #1; ADR 0006 axis-A crux: this cuts *file count*, not *bytes*).
- **A small browser client renderer** (an entry HTML shell that renders the shared chrome once + a small vanilla-JS bundle — no framework) that: renders the initial surface (inlined for instant first paint), intercepts in-portal link clicks to swap the content region in-view with History API URL updates, and lazy-fetches non-initial chunks. Preserves 6.1 `InteractionState` semantics (drill parent/children, breadcrumb, status stage) — the same information the HTML surface carries (AC #1, AC #4).
- **The static/`noscript` fallback (AC #2, NFR6):** the C# core already emits every page as pre-rendered static HTML — **that IS the fallback**. The opt-in form emits the SPA files **alongside** the untouched static site, and the client-renderer entry degrades to the static site under `<noscript>` (link/redirect to the static `index.html`, or serve the pre-rendered surface inside `<noscript>`). Core content + navigation work with JavaScript disabled because the static site is right there, unchanged.
- **Opt-in via an additive CLI surface** (suggested `--spa` flag on `generate`/`watch`, mirroring `--deep-git`; default OFF → **zero** SPA files emitted → the default generation and the golden byte-parity gate are literally untouched — AC #3). A `specscribe spa` subcommand is an acceptable alternative; pick one, keep it additive, do not touch Story 5.x's pending CLI scope.
- **Parity + regression gates** (mirroring Story 6.4's derived ACs): the SPA content regions run through [RenderParity](../../src/SpecScribe/RenderParity.cs) under surface id `spa`; the static site stays byte-for-byte identical (pinned `GoldenContentFingerprint` + self-site diff); read-only is honored end-to-end (AD-6).

**OUT of scope (do NOT start it here)**

- **Any byte reduction / TS chart port.** ADR 0006 is explicit and the epic note repeats it: this form ships pre-rendered SVG, so it does **not** shrink bytes — only *file count*. The ~13.5 KB structured-data floor requires porting chart generation to TypeScript (ADR 0006 **option D, deferred**). Do **not** re-model charts, and do **not** consume the spike's `data.json` structured layer as the render source — ship the **pre-rendered content regions** (the spike's `bodies.json` equivalent). The spike's structured `data.json` was a *measurement*, not the product form.
- **New section view models.** The dashboard/epics `DashboardView`/`EpicsView` decomposition (Story 6.2) already exists and is reused where convenient, but this story adds **no** new per-page section models for the long-tail pages — it consolidates their rendered content region via the landmark seam.
- **The VS Code webview** (`webview` surface) — that is Stories 6.4 (done) / 6.5. Reuse its *patterns* (bundle shape, link-interception script, in-place swap, parity-under-a-surface-id) but do not touch its adapter or the extension.
- **Host theming** (Story 6.5) and **VSIX/npx packaging** (Epic 16, incl. Story 16.8 npx). This is the JS-render output form only; how it's distributed is Epic 16.
- **C# package/namespace split** — still seed-level, still forbidden ([ARCHITECTURE-SPINE.md:98–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)). All new C# is `namespace SpecScribe;` files in the single [SpecScribe.csproj](../../src/SpecScribe/SpecScribe.csproj).
- **A second status vocabulary** — status stages come from `StatusStyles` via 6.1's `InteractionState`; never re-model (memory: [[specscribe-status-token-system]]; the 8.1 boundary).

## Acceptance Criteria

**Verbatim from [epics.md:1009–1024](../planning-artifacts/epics.md) (Story 6.7 AC #1–#3):**

1. **Given** the shared section view models (Story 6.2) and the `IRenderAdapter` seam (Story 6.1)
   **When** the JSON+SPA delivery adapter runs
   **Then** it emits a JSON data layer (with charts as pre-rendered inline SVG) plus a small client renderer that renders the surfaces from it, as a second concrete `IRenderAdapter` — with rendering staying in C# and no core port.

2. **Given** NFR6 and the progressive-enhancement policy (JS never the sole carrier of information)
   **When** the JSON+SPA form is produced
   **Then** a static/`noscript` fallback is shipped (the C# core already emits the pre-rendered HTML), so core content and navigation work with JavaScript disabled.

3. **Given** this is an additive output form
   **When** it is selected
   **Then** the existing static-HTML surface and the golden byte-parity gate are unaffected (opt-in; no change to default generation).

**Additional acceptance criteria for THIS story (derived from the architecture + prior-story contracts — the dev must also satisfy these; they mirror Story 6.4's derived ACs):**

4. **Parity is proven, not asserted.** The SPA content regions run against the [RenderParity](../../src/SpecScribe/RenderParity.cs) harness under surface id `spa` — 6.1's chrome facts (`FindDivergences`) for the surfaces routed through `PageView`, and 6.2's section facts (`FindSectionDivergences`) for dashboard + epics. Any legitimate host-specific divergence is registered in [HostRenderExceptions.Registry](../../src/SpecScribe/HostRenderException.cs) with a reason; an unregistered divergence is a bug. Because the SPA ships the **same C#-rendered content** (not a re-render), section parity should hold with **zero** exceptions; the only candidate divergences are chrome/asset carriers (see Dev Notes).

5. **The static-HTML surface does not change a single byte.** With the SPA form OFF (default), and with it ON, every generated static `.html`/asset is byte-for-byte identical to the pre-change build (pinned `GoldenContentFingerprint` + a self-site diff with only the known-benign normalizations — memory: [[golden-diff-normalization-gotchas]], currently **5**). The SPA files are strictly additive; they must not perturb one existing page byte (watch the diagnostics page — it discloses config).

6. **Read-only is honored end-to-end (AD-6).** Nothing in the SPA path writes to `_bmad-output/**`, `docs/**`, or any source artifact. SPA output goes only under the configured `OutputRoot`, exactly like the static site.

7. **Whole-site coverage, few files.** With the form ON against this repo, the SPA output is a **bounded, small** file set (a manifest + a handful of content chunks + the client entry + a small JS bundle), covering **every** page the static site emits — not one JSON per page, not a single unbounded blob. Demonstrate the file-count reduction in Completion Notes (e.g. "198 static files → static site + N SPA files, where N ≪ page count").

## Tasks / Subtasks

- [ ] **Task 1 — Branch + baseline + reuse audit** (AC: #1, #5)
  - [ ] Work on a branch, not `main` directly — there is a background auto-committer on `main` (memory: [[worktree-edits-must-target-worktree-path]]). Re-capture `baseline_commit` in the frontmatter to the HEAD you branch from.
  - [ ] Read [WebviewRenderAdapter.cs](../../src/SpecScribe/WebviewRenderAdapter.cs), [WebviewBundle.cs](../../src/SpecScribe/WebviewBundle.cs), [SiteGenerator.RenderWebviewSurfaces](../../src/SpecScribe/SiteGenerator.cs) (~:750), and the `WebviewCommand` in [Commands.cs](../../src/SpecScribe/Commands.cs) (~:45). **The webview is your template**: a second render form over the same views, a bundle record, a generator method that gathers surfaces, and a thin CLI. You are building the third form with the same shape.
  - [ ] Skim the throwaway spike under [`spike/delivery/`](../../spike/delivery) — `exporter/Program.cs` (the JSON-layer shape + the `bodies` vs `data` split — you ship the **bodies** form), `spa/app.js` + `spa/index.html` (a ~90-line client renderer with in-view nav — the shape to productionize). **Do not merge spike code as-is**; it is a probe, your code is product (comment/test bar).

- [ ] **Task 2 — The content-region seam for the whole site** (AC: #1, #5, #7)
  - [ ] Define the unit the SPA ships: `{ outputRelativePath, title, contentRegionHtml }` per page, where `contentRegionHtml` = the shared nav markup + breadcrumb + the page body (the same region `WebviewRenderAdapter.RenderContent` produces). For the **five 6.2 dashboard/epics surfaces**, reuse the exact `Build*Page → PageView` path the webview taps (`HtmlTemplater.BuildIndexPage`, `EpicsTemplater.Build*Page`, `SiteGenerator.BuildStoryPageFragments`) so those regions are true view-model renders (strongest parity).
  - [ ] For **every OTHER page type** (docs, sprint, requirements, ADRs, git insights, deep analytics, commits, retros, action items, about, diagnostics), obtain the content region from the render pipeline's own output via the universal `<main id="main-content"> … </main>` landmark **plus** the shared nav/breadcrumb chrome (rendered from the same `NavigationView`/`BreadcrumbTrail` the page already built). Capture at the write seam in `SiteGenerator` where the rendered string is already in hand — **not** by re-reading `OutputRoot/*.html` (that is scraping; forbidden — AD-1/AD-2). See Dev Notes "Is landmark-slicing scraping?" for the exact legitimate boundary.
  - [ ] Add the adapter (`JsonSpaRenderAdapter : IRenderAdapter`, `Id = "spa"` suggested) + a bundle record (mirror `WebviewBundle`/`WebviewSurface`). The adapter renders one page's content region (mirror `WebviewRenderAdapter.RenderContent`); a `SiteGenerator` method (mirror `RenderWebviewSurfaces`) gathers all surfaces into the bundle.

- [ ] **Task 3 — Emit the JSON data layer + client renderer + fallback** (AC: #1, #2, #7)
  - [ ] Emit a **manifest** JSON (site title, nav graph, ordered page index with path/title/breadcrumb + drill parent/children) and the page content regions grouped into a **bounded, small** number of **content chunks** (group by category or a size cap — the invariant is *few files, not one-per-page*; see Dev Notes "Chunking & bytes"). Charts stay pre-rendered inline SVG inside the regions (memory: [[charting-is-pure-svg-no-js]]).
  - [ ] Ship the **client renderer**: an entry HTML shell (shared nav/footer chrome rendered once, initial surface inlined for instant first paint + `<noscript>`) + a **small vanilla-JS** bundle (fetch manifest/chunks, intercept in-portal `<a>` clicks → swap `#…` content region in-view, update the URL via History API, lazy-load chunks). Emit these as new **embedded assets** (mirror how `specscribe.css`/`specscribe.js` are embedded + copied — [SiteGenerator.CopyEmbeddedAsset](../../src/SpecScribe/SiteGenerator.cs) ~:1211, [SpecScribe.csproj](../../src/SpecScribe/SpecScribe.csproj) `<EmbeddedResource>`).
  - [ ] **NFR6 fallback (AC #2):** the untouched static site is the fallback. The SPA entry must degrade under `<noscript>` to working content + navigation (the pre-rendered surface and/or a link to the static `index.html`). Verify with JS disabled: the initial surface is readable and the static site is reachable.
  - [ ] All output lands under `OutputRoot` only (AC #6). Nothing outside it, nothing in sources.

- [ ] **Task 4 — Opt-in CLI wiring (additive)** (AC: #3)
  - [ ] Add the opt-in surface: suggested `--spa` flag on [SiteSettings](../../src/SpecScribe/SiteSettings.cs) (mirror `--deep-git` exactly: `[CommandOption("--spa")]`, default `false`, threaded through `ForgeOptions.Resolve`), honored by `generate`/`watch`. Default OFF ⇒ **no SPA files, no behavior change** (AC #3). (Alternative: a `specscribe spa` subcommand like `WebviewCommand` — pick one; a flag is simpler and keeps the static site + SPA in one output.)
  - [ ] Keep it additive: `generate`/`watch`/`webview`/interactive behavior unchanged; Story 5.x's pending CLI scope (5.1/5.2/5.3) untouched. Register in [Program.cs](../../src/SpecScribe/Program.cs) only if you add a subcommand.
  - [ ] Golden gate after wiring: pinned fingerprint unchanged (flag OFF **and** ON — the static bytes are identical either way); `dotnet test` green.

- [ ] **Task 5 — Parity + tests** (AC: #4, #5, #6)
  - [ ] Run the SPA content regions through [RenderParity](../../src/SpecScribe/RenderParity.cs) under surface id `spa`: chrome facts via `FindDivergences(pageView, region, "spa", exceptions)` and section facts via `FindSectionDivergences` (`From*View` declared vs. `Extract*Section` evidenced) for dashboard, epics index, epic page. Register any legitimate divergence in `HostRenderExceptions.Registry` with a reason — the ideal is **zero** (unlike the webview's 3: the SPA is a browser, so it can keep `specscribe.css`/`specscribe.js` and Mermaid, so its chrome/asset facts should match `html`). If you register any entry, justify it (e.g. an added client-nav script fact).
  - [ ] xUnit tests in [tests/SpecScribe.Tests/](../../tests/SpecScribe.Tests), file-per-unit (e.g. `JsonSpaRenderAdapterTests.cs`, `SiteGeneratorSpaTests.cs`): the bundle covers every page the static run emits (AC #7); manifest + chunks serialize and round-trip; a representative page's content region equals the `html` surface's `<main>` region (proving "same C#-rendered content, no re-render"); injected divergence caught; **read-only** — running the SPA form leaves the project tree + configured output untouched apart from the intended SPA files (AC #6, mirror `SiteGeneratorWebviewTests`).
  - [ ] **Golden byte-identity (AC #5):** pinned `GoldenContentFingerprint` unchanged (Debug + Release), and a self-site diff (baseline build vs. this branch, both with the flag OFF, then the branch with the flag ON) shows the static `.html`/assets are byte-identical under only the 5 benign normalizations (memory: [[golden-diff-normalization-gotchas]]). **If any existing rendering assertion must change, STOP** — you perturbed the static surface.
  - [ ] Full suite green, Debug + Release.

- [ ] **Task 6 — Verify the client renderer end-to-end** (AC: #1, #2)
  - [ ] The C# unit tests can't paint a browser. Verify the client renderer the way 6.4 verified its bridge: drive the **real** emitted bundle + the real client JS in a browser (the [Browser pane / preview tools], or a headless harness) — initial surface paints (charts as inline SVG), an in-portal link swaps the content region in-view with the URL updated, breadcrumb up-drill works, an external link opens normally, and **with JS disabled** the `<noscript>` fallback shows content + reaches the static site. Record the checklist + result in Completion Notes.

- [ ] **Task 7 — Record + hand off** (AC: all)
  - [ ] Completion Notes: the adapter/bundle/CLI touchpoints, the chunking scheme chosen + measured file-count reduction (AC #7) and payload sizes (ADR 0006 axis A), any `HostRenderException` entries (with reasons), the client-renderer verification result, and what Epic 16 (distribution) inherits.
  - [ ] Update [sprint-status.yaml](sprint-status.yaml) (`6-7-json-and-spa-delivery-adapter` → `review` at story end) and this file's Dev Agent Record / Change Log.

## Dev Notes

### What already exists — reuse it all, reinvent nothing
- **6.1 delivery seam (`done`):** [IRenderAdapter](../../src/SpecScribe/IRenderAdapter.cs) (+`RenderedArtifact`) — its XML docs literally invite additional adapters; [PageView](../../src/SpecScribe/PageView.cs) (+`PageKind`), [NavigationView](../../src/SpecScribe/NavigationView.cs), [BreadcrumbTrail](../../src/SpecScribe/BreadcrumbTrail.cs), [AssetManifest](../../src/SpecScribe/AssetManifest.cs), [InteractionState](../../src/SpecScribe/InteractionState.cs) (drill parent/children + status stage). [HtmlRenderAdapter](../../src/SpecScribe/HtmlRenderAdapter.cs) (`.Shared`, `Id="html"`, `RenderNavMarkup`, `RenderBreadcrumb`, `RenderDashboardBody`, `RenderEpicsIndexBody`) is the chrome+body renderer you reuse verbatim.
- **6.2 section view models (`done`):** `DashboardView`+`DashboardViewBuilder`, the `EpicsView` records + `EpicsViewBuilder`, rendered by `HtmlRenderAdapter.Dashboard.cs`/`.Epics.cs`. The section DATA round-trips `System.Text.Json` cleanly (proven by [SectionViewModelSerializationTests](../../tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs)) — but **you ship pre-rendered content regions, not the structured data** (see the Serialization gotcha below).
- **6.4 webview form (`done`) — YOUR TEMPLATE:** [WebviewRenderAdapter](../../src/SpecScribe/WebviewRenderAdapter.cs) (`RenderContent` = nav + breadcrumb + body; `Render`; a document shell), [WebviewBundle](../../src/SpecScribe/WebviewBundle.cs)/`WebviewSurface`, [SiteGenerator.RenderWebviewSurfaces](../../src/SpecScribe/SiteGenerator.cs), [WebviewCommand](../../src/SpecScribe/Commands.cs). Copy the *shape*; your differences are: (1) browser not webview → no CSP straitjacket, keep `specscribe.css`/`.js` and Mermaid; (2) **whole site**, not 5 surfaces; (3) write **files** to `OutputRoot`, not JSON to stdout; (4) a **noscript fallback** requirement (the webview had none — it's always in an editor with JS).
- **Parity harness (built for exactly this):** `RenderParity.SemanticFacts` + `FindDivergences` (chrome), `SectionFacts` + `From*View`/`Extract*Section` + `FindSectionDivergences` (sections), [HostRenderExceptions.Registry](../../src/SpecScribe/HostRenderException.cs) (currently 3 `webview`-scoped entries; add `spa`-scoped only if justified). Run it under `spa` — AC #4.
- **Asset embedding:** [SiteGenerator.CopyEmbeddedAsset](../../src/SpecScribe/SiteGenerator.cs) (~:1211) copies `specscribe.css`/`.js` from embedded resources; [SpecScribe.csproj:42-43](../../src/SpecScribe/SpecScribe.csproj) declares them. Add your client-renderer assets the same way.
- **The spike prototype:** [`spike/delivery/exporter/Program.cs`](../../spike/delivery/exporter/Program.cs) (JSON shape; note it emits both `data.json` structured + `bodies.json` pre-rendered — **you ship the bodies form**), [`spike/delivery/spa/app.js`](../../spike/delivery/spa/app.js) + [`index.html`](../../spike/delivery/spa/index.html) (the ~90-line client-nav renderer to productionize). Throwaway — reuse the *idea*, meet the product bar.

### The `<main id="main-content">` landmark IS the whole-site seam (the key enabler)
Every page emitted by every templater wraps its body in exactly one `<main id="main-content"> … </main>` (Story 1.4 AC #1, the skip-link target — verified in all 12 templaters). This is what makes whole-site coverage affordable **without** finishing 6.1's deferred "route every page body through `PageView`" wiring. The SPA's content region = shared nav markup + breadcrumb + that `<main>…</main>` block. For dashboard/epics you already have the body as a first-class render (`RenderDashboardBody`/`RenderEpicsIndexBody`); for the long tail you take the `<main>` block out of the page's own render output. Consider recording this seam in memory (`main-content-landmark-is-content-region-seam`) — it will matter for Epic-7 pages and any future SPA-adjacent work.

### Is landmark-slicing "scraping"? (the reviewer-sensitive boundary — get it right)
AD-1/AD-2 forbid two things: **re-parsing source `.md`** and **scraping the generated `.html` site**. Slicing the `<main>` region out of the string `SiteGenerator` **already holds in memory before `File.WriteAllText`** is **neither** — it is consuming the render pipeline's own output at the moment it is produced, one step before it becomes a file. The forbidden version is reading `OutputRoot/**/*.html` back off disk after generation. **Do the former, never the latter.** Frame it in the code comments as "capture the render output at the write seam," and keep the capture in `SiteGenerator` (which owns the render→write step), not in a post-hoc filesystem pass. The dashboard/epics path is even cleaner (true view-model render via `Build*Page`), so parity there is airtight; the long-tail landmark path is legitimate output-consumption, and its parity is covered by the golden byte-identity of the static site it was sliced from.

### The 6.2 opaque-fragment / serialization reality (don't ship the wrong JSON)
6.2's serialization test proves the section DATA records round-trip — but that is the path to the **~13.5 KB structured floor**, which is **explicitly NOT this story** (it needs the TS chart port, ADR 0006 option D). The chart/rich panels carry already-projected domain inputs (`EpicsModel`, `ProgressModel`, `CommandCatalog` — which has no parameterless ctor and does **not** round-trip; memory: [[story-6-2-section-view-models-live]]). **So do not serialize the view models as the render source.** Ship the **pre-rendered content region string** (charts already inline SVG). Your JSON data layer is `{ path → { title, contentRegionHtml } }` + a manifest — the `bodies.json` form the spike measured, generalized to the whole site. This keeps rendering in C# (AC #1) and the accessible fallback free (AC #2).

### Chunking & bytes (ADR 0006 axis A — measure and decide)
ADR 0006 measured that a JSON layer shipping pre-rendered SVG is **~the same bytes** as the static site (inline SVG is 69.3% of the dashboard body); the win is **file count, not bytes**. Consequences for your chunking:
- **Not one JSON per page** (that keeps thousands of files — no win) and **not one monolith** (the webview bundle was 6.2 MB for 93 surfaces; at Epic-7 scale a single file is many MB and stalls first paint). **Group into a bounded, small number of chunks** (by top-level category, or a size cap that keeps chunk count small and each chunk load-able), lazy-loaded on navigation. Inline the entry surface for instant first paint.
- Report the actual numbers (file count before/after, chunk sizes, first-paint payload) in Completion Notes — this is the axis-A evidence the ADR asked follow-on work to demonstrate.

### Architecture invariants that BOUND this story
- **AD-1/AD-2** ([ARCHITECTURE-SPINE.md:34–48](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): one core; adapters translate view models / consume render output without reinterpreting sources or scraping the site. The client renderer re-parses **nothing** and re-renders **nothing** — it fetches, injects, navigates.
- **AD-6** (:74–80) + [ADR 0003](../../docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md): read-only; SPA output only under `OutputRoot` (AC #6).
- **NFR4** (:extensible adapters, no core rewrite): the whole point — a third `IRenderAdapter`, additive, no namespace split.
- **NFR6 + Client-Side Enhancement Policy** ([rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md); [ADR 0006 §Accessibility posture](../../docs/adrs/0006-delivery-architecture-and-distribution.md)): **JS adds convenience, never information.** The JSON+SPA form is layered *over* the static-HTML baseline, never its replacement — the static site is the JS-optional source of truth; the SPA MUST ship a static/`noscript` fallback (AC #2). This is the exact ruling ADR 0006 required and it is non-negotiable.
- **Charts stay pure SVG + links** (memory: [[charting-is-pure-svg-no-js]]); **status routes through `StatusStyles` / the six `--status-*` tokens** (memory: [[specscribe-status-token-system]]) — reference, never fork.

### Risk centers (where review will focus)
1. **Byte drift on the static surface** — a new adapter/flag/embedded-asset that nudges any generated static byte fails AC #5. Golden gate OFF *and* ON. The diagnostics page discloses config — make sure the flag doesn't leak into it in a way that changes default output.
2. **Shipping the wrong JSON** — serializing the structured section data (the ~13.5 KB floor) instead of the pre-rendered regions. That is a different (deferred) story and will fail the "charts as pre-rendered inline SVG" clause of AC #1.
3. **File-count that isn't** — one JSON per page (no win) or a single monolith (Epic-7 first-paint stall). Bounded chunks, measured (AC #7).
4. **Scraping the site** — reading `OutputRoot/*.html` back instead of capturing the render output at the write seam. AD-1/AD-2 violation; see the boundary note above.
5. **Broken `<noscript>`** — an SPA entry that shows a blank page with JS off fails AC #2/NFR6. The static site is the fallback; wire it.
6. **Parity theater** — declaring parity without running `FindDivergences`/`FindSectionDivergences` under `spa`, or dumping divergences into the registry without reasons. The SPA should reach **zero** exceptions (it's a browser); a non-empty registry needs justification.
7. **Scope leak** — theming (6.5), packaging (Epic 16), a TS chart port (option D), or new section models. Named out; resist.

### Project Structure Notes
- **C#:** all new core code in [src/SpecScribe/](../../src/SpecScribe) `namespace SpecScribe;` (single csproj, `net10.0`, `Nullable enable`, `ImplicitUsings enable`). Heavy XML-doc style with the *why*, tagged `[Story 6.7]` (match 6.1/6.2/6.4 density). New client-renderer assets under `src/SpecScribe/assets/` as `<EmbeddedResource>`. Tests in [tests/SpecScribe.Tests/](../../tests/SpecScribe.Tests), file-per-unit.
- **Output dir** is `SpecScribeOutput`; never `--output docs/live` (memory: [[generate-output-dir-is-specscribeoutput]]).
- **Branch discipline:** background auto-committer on `main` (memory: [[worktree-edits-must-target-worktree-path]]) — develop on a branch; re-capture `baseline_commit` at dev start.
- **Stale-source gotcha** (memory: [[epic-4-adapter-contract-scope]]): `generate -s .` from repo root can render a STALE `epics.md` because `.claude/worktrees/*` copies sort first — verify golden diffs against a clean/isolated source.

### References
- **Epic + ACs:** [epics.md:995–1024](../planning-artifacts/epics.md) (Story 6.7 + its ADR-0006 provenance comment); Epic 6 goal + FR13 at [epics.md:815–819,48](../planning-artifacts/epics.md); NFR4/NFR6 at [epics.md:78,86](../planning-artifacts/epics.md).
- **The decision that seats this:** [ADR 0006 — Delivery Architecture & Distribution](../../docs/adrs/0006-delivery-architecture-and-distribution.md) — **Architecture B (adopted, additive)**, the axis-A evidence (file count, chart-mass %, chunking question), and the **NFR6 accessibility ruling**. [docs/adrs/README.md](../../docs/adrs/README.md). Supporting: [ADR 0002](../../docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md), [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md).
- **Prior stories:** [6-1-shared-view-model-contract-for-html-and-webview-adapters.md](6-1-shared-view-model-contract-for-html-and-webview-adapters.md) (the seam + parity harness + scope discipline); [6-2-read-only-vs-code-dashboard-and-epics-experience.md](6-2-read-only-vs-code-dashboard-and-epics-experience.md) (view models + the serialization boundary); [6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md](6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md) (**the template**: second render form, bundle, generator method, thin CLI, parity-under-a-surface-id, in-view nav script); [6-6-delivery-architecture-and-distribution-spike.md](6-6-delivery-architecture-and-distribution-spike.md) (the spike + measured numbers).
- **Architecture:** [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) AD-1/AD-2/AD-6 + Seed/no-split + NFR4/NFR6; [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md) Delivery Adapter Layer, `IRenderAdapter`, Feature-Parity + Client-Side Enhancement Policy.
- **Source seams:** [IRenderAdapter.cs](../../src/SpecScribe/IRenderAdapter.cs), [PageView.cs](../../src/SpecScribe/PageView.cs), [HtmlRenderAdapter.cs](../../src/SpecScribe/HtmlRenderAdapter.cs) (+`.Dashboard.cs`/`.Epics.cs`), [WebviewRenderAdapter.cs](../../src/SpecScribe/WebviewRenderAdapter.cs), [WebviewBundle.cs](../../src/SpecScribe/WebviewBundle.cs), [SiteGenerator.cs](../../src/SpecScribe/SiteGenerator.cs) (`RenderWebviewSurfaces` ~:750, `CopyEmbeddedAsset` ~:1211, the page write seam), [Commands.cs](../../src/SpecScribe/Commands.cs) (`WebviewCommand`), [SiteSettings.cs](../../src/SpecScribe/SiteSettings.cs) (`--deep-git` opt-in pattern), [Program.cs](../../src/SpecScribe/Program.cs), [RenderParity.cs](../../src/SpecScribe/RenderParity.cs), [HostRenderException.cs](../../src/SpecScribe/HostRenderException.cs), [assets/specscribe.js](../../src/SpecScribe/assets/specscribe.js).
- **Test gates:** [SiteGeneratorAdapterTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) (golden fingerprint), [RenderSectionParityTests.cs](../../tests/SpecScribe.Tests/RenderSectionParityTests.cs) + [RenderParityTests.cs](../../tests/SpecScribe.Tests/RenderParityTests.cs) (patterns to mirror for `spa`), [SiteGeneratorWebviewTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs) (whole-run + read-only pattern), [SectionViewModelSerializationTests.cs](../../tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs) (the serialization boundary — the floor you are NOT shipping).
- **Spike:** [`spike/delivery/`](../../spike/delivery) — `exporter/Program.cs`, `spa/app.js`, `spa/index.html`, `README.md` (measured numbers).
- **Memory:** [[story-6-4-webview-runtime-live]], [[story-6-2-section-view-models-live]], [[story-6-1-delivery-seam-live]], [[epic-6-sequencing-and-6-5-theming]], [[adr-0005-reopened-delivery-arch-spike]] (ADR 0006 ratified, this story additive), [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[golden-diff-normalization-gotchas]] (5 normalizations), [[generate-output-dir-is-specscribeoutput]], [[worktree-edits-must-target-worktree-path]], [[create-story-elicit-visual-intent]].

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-11 — Story 6.7 drafted (create-story). Scoped from [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) Architecture B (additive JSON+SPA delivery adapter) as the **third** concrete `IRenderAdapter` after `html`/`webview`. Defining scope decision recorded (READ FIRST / veto-before-dev, per the Epic 3 retro house rule): **whole-site consolidation** (the real Epic-7-scale file-count win), made affordable by capturing each page's content region at the render write seam via the universal `<main id="main-content">` landmark — no deferred all-pages `PageView` rewrite required — with the 5 dashboard/epics surfaces reusing the webview's true view-model render path. Ships **pre-rendered content regions** (charts as inline SVG), **not** the structured ~13.5 KB data floor (that needs the deferred TS chart port — ADR 0006 option D, explicitly out). Opt-in via a `--spa` flag (mirrors `--deep-git`); static site stays byte-identical (golden gate OFF and ON) and is the `<noscript>` fallback. Derived ACs added (mirroring 6.4): parity under surface id `spa` (#4), static byte-identity (#5), end-to-end read-only (#6), whole-site-few-files (#7). Byte reduction, host theming (6.5), the webview, packaging (Epic 16), and any C#→TS port explicitly out of scope. Status → ready-for-dev.
