---
baseline_commit: fa1d79c10da5cec1b54b0399bf05efe0d906f33a
---

# Story 6.4: Read-Only VS Code Webview Runtime for Dashboard and Epics

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

> ## ✅ UNFROZEN 2026-07-10 — cleared to dev
> This story was briefly frozen while the delivery architecture was reconsidered. **[ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md)
> (Accepted) re-affirmed ADR 0005** — C# renders the webview HTML via a `WebviewRenderAdapter`, shipped as a
> self-contained binary, exactly the premise this story was written against. The pure-TS/SPA pivot was **deferred**,
> so **this story proceeds as written**, governed by ADR 0005 (as amended by ADR 0006). ADR 0006 also adds two
> *additive* delivery options — an npx wrapper channel and an optional JSON+SPA output adapter — that do **not**
> change this story's scope; they are seated as their own stories. The original gate below still applies: dev-start
> requires Story 6.2 `done`.

## Story

As a VS Code user,
I want an in-editor status surface for dashboard and epics that stays live as the project changes,
so that I can inspect project state without context-switching to a browser.

## ⛔ READ FIRST — this story is GATED on Story 6.3 (spike + ADR 0005) and Story 6.2 (do not start until both are `done`)

**This story was context-captured ahead of its gate at the owner's request** (create-story run 2026-07-10, story explicitly selected). The plan of record ([sprint-status.yaml](sprint-status.yaml) Epic 6 comments, [6.3's AC #4](6-3-vs-code-integration-spike.md)) says this story is *seated by the 6.3 spike's ADR 0005* — and **as of create-story, the spike has NOT run: there is no `docs/adrs/0005-*.md`, no spike branch, and no extension code anywhere in the repo.** The `ready-for-dev` status means "context complete," not "prerequisites met" (same convention as [Story 6.5's gate](6-5-host-aware-theming-and-explicit-helper-actions.md)).

**Hard prerequisites (all must hold before `dev-story` begins):**

| Prereq | What it delivers that THIS story needs | Status at create-story (2026-07-10) |
|---|---|---|
| **Story 6.3** — VS Code integration spike | **ADR 0005** (`docs/adrs/0005-*.md`): the ratified extension↔core data path (JSON export vs. C#-rendered webview HTML), live-push transport, CSP constraints, and packaging direction. Also the spike's empirical findings (what survives the webview CSP, spawn latency, tooling versions). | `ready-for-dev` — **spike not run, ADR 0005 does not exist** |
| **Story 6.2** — section view models | `DashboardView` / `EpicsIndexView` / `EpicPageView` / `StoryPageView` / `StoryPlaceholderView` + builders — the host-neutral data this story delivers into the webview. **Implemented** (667 tests green, byte-parity proven) but not yet through review. | `review` |
| **Story 6.1** — delivery contract | `PageView` / `IRenderAdapter` / `HtmlRenderAdapter` / `RenderParity` / `HostRenderException` — the seam a `WebviewRenderAdapter` (if the ADR chooses it) plugs into. | `done` |

**Dev-start protocol (Task 1 makes this executable):**

1. **Read ADR 0005 FIRST.** It is the governing document for this story's delivery form. Where this story file and the ADR disagree, **the ADR wins** — update this story file's affected sections (and note it in the Change Log) *before* writing code.
2. If ADR 0005 does not exist, **STOP and surface it** — run Story 6.3 first. Do not guess the seam decision; guessing it is exactly what the spike exists to prevent (an unplanned "rewrite the renderer in TypeScript" discovery mid-story).
3. If Story 6.2 is not `done`, STOP and surface it (its code review could still reshape the view-model records this story serializes/renders).

## The ADR-0005 fork this story straddles (understand this before reading the ACs)

The epic's AC #1 as literally written says the core exposes a **JSON view-model export** that the extension's webview renders. The 6.3 spike exists to **ratify or reject a reinterpretation** (owner-preferred, captured in [6.3's scope decision](6-3-vs-code-integration-spike.md)): keep TypeScript to an irreducible thin host shim (~150 lines: register command → open `WebviewPanel` → obtain content → relay live-push) and have **C# render the webview HTML** via a `WebviewRenderAdapter` — a second `IRenderAdapter` over the same 6.2 section view models. Same view models either way; what moves is the render step's side of the process boundary.

**Both forms satisfy the epic AC's intent** (webview consumes the section view models as data, not scraped HTML, with no dependence on the HTML surface's enhancement scripts). This story is written to be implementable under either ruling:

- **If ADR 0005 ratifies C#-rendered webview HTML (the spike's working hypothesis):** build `WebviewRenderAdapter : IRenderAdapter` (`Id = "webview"`) in the rendering core, rendering the 6.2 views to webview-safe HTML (CSP-compliant, no `specscribe.js` dependence); the TS shim just requests pages and sets `webview.html` / relays patches. AC #1's "JSON view-model export" is then satisfied in its ratified form — record that reinterpretation in this story's Change Log at dev time.
- **If ADR 0005 upholds the literal JSON export:** add the export entry point (e.g. a `specscribe` sub-command emitting the serialized section view models) and the TS-side rendering it implies — and budget for materially more TypeScript (the owner accepted this only if the spike proves the C# path unworkable).

Do **not** build both paths. Build the ratified one; note the rejected one only in the ADR (already done by 6.3).

## Acceptance Criteria

**Verbatim from [epics.md:884–918](../planning-artifacts/epics.md) (Story 6.4 AC #1–#3):**

1. **Given** Story 6.2's section view models describe the dashboard and epics surfaces as host-neutral data
   **When** the webview needs that data
   **Then** the rendering core exposes a JSON view-model export of those section view models
   **And** the export carries the section data itself (not scraped HTML) with no dependence on the HTML surface's enhancement scripts.
   *(Delivery form per ADR 0005 — see "The ADR-0005 fork" above. The invariant that survives either ruling: the webview's content is produced from the 6.2 section view models by the C# core; the extension never scrapes generated `.html` files and never re-parses `.md`.)*

2. **Given** the extension opens the status webview
   **When** project data is loaded
   **Then** dashboard and epics views display with the same core interaction-state semantics as HTML
   **And** in-editor navigation is responsive and readable.

3. **Given** source artifacts change while the webview is open
   **When** host updates are pushed
   **Then** visible status refreshes in place without full panel reset
   **And** drill/breadcrumb context remains coherent.

**Additional acceptance criteria for THIS story (derived from the architecture + prior-story contracts — the dev must also satisfy these):**

4. **Parity is proven, not asserted.** The webview surface runs against the [RenderParity](../../src/SpecScribe/RenderParity.cs) harness — 6.1's chrome facts (`FindDivergences`) and 6.2's section facts (`FindSectionDivergences`) — under its own surface id (`webview`). Any legitimate host-specific divergence is registered in [HostRenderExceptions.Registry](../../src/SpecScribe/HostRenderException.cs) with a reason; an unregistered divergence is a bug. (The registry is empty today; keep every entry justified.)

5. **The HTML surface does not change a single byte.** The generated static site is byte-for-byte identical to the pre-change build (pinned `GoldenContentFingerprint` + a self-site diff with only the known-benign normalizations — memory: [golden-diff-normalization-gotchas], currently **5** normalizations). Adding the webview delivery path, CLI surface, or extension must not alter one generated `.html`/asset byte.

6. **Read-only is honored end-to-end (AD-6).** Nothing in the extension or the new delivery path writes to `_bmad-output/**`, `docs/**`, or any source planning artifact. No helper buttons in this story (they are Story 6.5's AC #2); the webview is a pure viewer.

## IN scope

- **The C# delivery of the dashboard + epics section view models to the webview**, in ADR 0005's ratified form (`WebviewRenderAdapter` as the second concrete `IRenderAdapter`, or a JSON export entry point). All rendering/serialization logic lives in C# in the rendering core (`namespace SpecScribe;`, single project). Covers **all five 6.2 surfaces**: dashboard, epics index, epic page, story page, story placeholder — those are the webview's navigable set.
- **The production VS Code extension (greenfield, minimal by design):** `package.json` manifest, one contributed command (e.g. `specscribe.openStatus`), a thin TS entry that opens a `WebviewPanel` and obtains content from the `specscribe` tool (invocation form per ADR 0005). This is **product code** (unlike 6.3's throwaway) — it lives in a self-contained folder (suggest `extension/` at repo root; final location per ADR 0005's packaging section), outside the .NET solution and the site-generation pipeline.
- **In-webview navigation across the dashboard/epics surfaces** (AC #2): link clicks inside the panel navigate between the five surfaces without leaving the webview, preserving 6.1's `InteractionState` semantics (drill parent/children, status stage) and breadcrumb coherence. Requires an explicit link-interception strategy — see Dev Notes.
- **Live host-push (AC #3):** source `.md` edits refresh the visible panel content **in place** (targeted content update via `postMessage`, not panel re-creation), preserving the user's current surface + scroll/drill context. Transport per ADR 0005 (bridge the C# [FileWatcherService](../../src/SpecScribe/FileWatcherService.cs) events vs. an extension-host `FileSystemWatcher` + one-shot re-render).
- **CSP-compliant webview content:** strict `Content-Security-Policy` meta, nonces/`asWebviewUri` for any style/script resources, inline SVG charts intact. The webview reaches all information **without** [assets/specscribe.js](../../src/SpecScribe/assets/specscribe.js) (the enhancement-script-independence rule, [rendering-architecture.md:84–92](../specs/spec-specscribe/rendering-architecture.md)).
- **Parity + regression tests** (AC #4, #5): webview-surface parity runs in the existing xUnit suite; golden byte-identity gate stays green.

## OUT of scope (do NOT start it here)

- **Host-aware theming** — Story 6.5. The webview may render with SpecScribe's own warm-light styling in this story; mapping VS Code `--vscode-*` variables and per-theme contrast tuning of the `--status-*` accents is 6.5's whole job. Do not add `.vscode-dark` handling here.
- **Helper actions / buttons** (generate-a-prompt, copy-a-command) — Story 6.5 AC #2.
- **VSIX packaging, Marketplace publish, CI wiring** — Epic 16 (Story 16.5 extension publish, 16.2 CI). Local dev-host runs (F5 / `vsce package` for a local smoke test) are fine for verification; nothing publishes, and no CI job is added here.
- **Decomposing any other page body** into view models, or extending the webview to non-dashboard/epics pages. Five surfaces only.
- **Re-parsing `.md` in the extension** — forbidden (AD-1/AD-2). All data comes from the C# core.
- **The Story 5.x CLI/watch feature work** (5.1 smart defaults, 5.2 settings, 5.3 watch safety are `ready-for-dev`, unimplemented). If this story adds a CLI sub-command, keep it additive and isolated so 5.x lands cleanly later; do not implement their ACs.
- **C# package/namespace split** — still seed-level, still forbidden ([ARCHITECTURE-SPINE.md:98–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)). All new C# is `namespace SpecScribe;` files in the single [SpecScribe.csproj](../../src/SpecScribe/SpecScribe.csproj). (The TS extension folder is a separate stack, not a C# split.)
- **A second status vocabulary** — status stages come from `StatusStyles.ForEpic`/`ForStory` via 6.1's `InteractionState`/6.2's views; never re-model (memory: [specscribe-status-token-system]; the 8.1 boundary).

## Tasks / Subtasks

- [x] **Task 1 — Gate check + ADR reconciliation** (AC: #1)
  - [x] Confirm `docs/adrs/0005-*.md` exists with an Accepted/Proposed seam decision, and Story 6.2 is `done` in [sprint-status.yaml](sprint-status.yaml). **If either fails, STOP and surface — do not proceed.**
  - [x] Read ADR 0005 end-to-end. Reconcile this story file against it: delivery form (adapter vs. JSON export), invocation/packaging (spawn CLI vs. bundled publish), live-push transport, extension folder location, and any CSP findings. Update this file's affected sections + Change Log where the ADR overrides; then re-capture `baseline_commit` to the HEAD you branch from.
  - [x] Skim the 6.3 spike's Completion Notes + any `spike/` artifacts for reusable findings (measured spawn latency, CSP breakage list, tooling versions). Reuse knowledge; do not merge throwaway spike code as-is — this story's code is product and must meet the repo's comment/test bar.

- [x] **Task 2 — Build the C# delivery path (ADR-ratified form)** (AC: #1, #5)
  - [x] **If C#-rendered webview HTML:** add `WebviewRenderAdapter : IRenderAdapter` (`Id = "webview"`) mirroring [HtmlRenderAdapter](../../src/SpecScribe/HtmlRenderAdapter.cs)'s structure (partial files per surface family if it helps), rendering `DashboardView` / `EpicsIndexView` / `EpicPageView` / `StoryPageView` / `StoryPlaceholderView` to webview-safe HTML: CSP-compatible (no inline event handlers; styles via a nonce'd `<style>`/`asWebviewUri` stylesheet), inline SVG charts intact (reuse `Charts.*` — memory: [charting-is-pure-svg-no-js]), **zero dependence on `specscribe.js`**, links emitted in a form the shim can intercept (see Task 4).
  - [x] ~~If JSON export~~ — not built (ADR 0005 rejected this form; recorded in Change Log).
  - [x] Expose it to the extension via the CLI (per ADR 0005 — e.g. a new Spectre command/branch in [Program.cs](../../src/SpecScribe/Program.cs)/[Commands.cs](../../src/SpecScribe/Commands.cs), or a mode of `watch`). Keep it additive: `generate`/`watch`/interactive behavior unchanged, and Story 5.x's pending CLI scope untouched.
  - [x] Golden gate after wiring: pinned fingerprint unchanged; `dotnet test` green.

- [x] **Task 3 — Stand up the production extension shell** (AC: #2)
  - [x] Create the extension folder (per ADR 0005; suggest `extension/`): `package.json` (`name`, `publisher` placeholder, `engines.vscode`, one command `specscribe.openStatus`, activation on command), `tsconfig.json`, bundler config (esbuild per the spike's findings), and a **thin** `src/extension.ts` — register command → create `WebviewPanel` (`enableScripts` only as needed, `retainContextWhenHidden: true`, `localResourceRoots` scoped) → obtain content from the C# tool → set/patch `webview.html`. Keep the shim dumb; every piece of logic that *can* live in C# does.
  - [x] Keep the folder self-contained: not in the .NET solution, not in the site build, no CI wiring (Epic 16). Add a short `extension/README.md` (how to F5-run against this repo; note that packaging/publish is Story 16.5).
  - [x] Work on a branch, not `main` directly — there is a background auto-committer on `main` (memory: [worktree-edits-must-target-worktree-path]); land the story as a coherent change. (Branch: `worktree-bmad-dev-story-6-4`.)

- [x] **Task 4 — In-webview navigation across the five surfaces** (AC: #2)
  - [x] Implement link interception: clicks on inter-page links inside the panel must navigate within the webview (webviews do not follow document navigation like a browser). Standard pattern: a tiny nonce'd script posts the target to the shim via `acquireVsCodeApi().postMessage`, the shim asks the C# side for that surface's content and updates the panel. Keep this script minimal and information-free (progressive-enhancement policy: it navigates, it never adds content).
  - [x] Preserve interaction-state semantics: breadcrumb trail, drill parent/children, and status stages must match the HTML surface's (6.1 `InteractionState` / `BreadcrumbTrail`); external links (e.g. source files) open outside the webview via the shim.
  - [x] Verify readability/responsiveness at typical editor-panel widths (AC #2 "responsive and readable") — the static site's CSS assumes a browser viewport; narrow-panel behavior must degrade sanely (this is layout sanity, not theming — theming is 6.5). (Verified at 520px: no horizontal overflow; the bridge's delegated Menu toggle works with correct `aria-expanded`.)

- [x] **Task 5 — Live host-push** (AC: #3)
  - [x] Implement ADR 0005's ratified transport: extension-host `FileSystemWatcher`s over `_bmad-output/**/*.md` + `docs/adrs/**/*.md`, debounced (400 ms), one-shot re-render spawn, `postMessage` patch of the current surface. (The C#-watcher bridge was the road not taken — ADR 0005 chose the extension-host watcher; the C# side stays a stateless one-shot renderer.)
  - [x] **In place, not reset** (AC #3): patch the panel content (`postMessage` + DOM swap of the content region, or at minimum `webview.html` reassignment with `retainContextWhenHidden`) so the user's current surface and drill/breadcrumb context survive the refresh. A full panel re-create fails the AC. (`webview.html` is assigned exactly once; every navigation/refresh is an innerHTML swap of `#specscribe-surface`; refresh preserves scroll — verified in the harness.)
  - [x] Honor NFR5: reads stay shared; the watch path must not hold write locks on observed files (the existing service already complies — don't regress it). (The renderer reads via `MarkdownConverter.ReadAllTextShared`; the extension watcher holds no handles; the render pass writes only to a per-project temp scratch dir.)

- [x] **Task 6 — Parity + tests** (AC: #4, #5)
  - [x] Run the webview surface through [RenderParity](../../src/SpecScribe/RenderParity.cs): chrome facts via `FindDivergences(pageView, output, "webview", exceptions)` and section facts via `FindSectionDivergences` with the `From*View` (declared) vs. `Extract*Section` (evidenced) pair, for dashboard, epics index, and epic page (the surfaces the section API covers). Register any legitimate divergence in `HostRenderExceptions.Registry` with a reason (e.g. a webview-specific asset fact); an empty registry is the ideal outcome. (Three entries registered — `asset.css`/`asset.js`/`mermaid`, all webview-scoped chrome/asset facts with reasons, exactly ADR 0005's measured set; all SECTION facts hold full parity with zero exceptions.)
  - [x] xUnit tests in [tests/SpecScribe.Tests/](../../tests/SpecScribe.Tests) following file-per-unit naming (e.g. `WebviewRenderAdapterTests.cs` / export tests): representative views render/serialize, parity green, injected divergence caught (mirror `RenderSectionParityTests`). (`WebviewRenderAdapterTests` + `SiteGeneratorWebviewTests`; JSON-path tests n/a — form rejected by ADR 0005.)
  - [x] Full suite green (`dotnet test`), Debug + Release. **If any existing rendering assertion must change, STOP** — you altered the HTML surface (fails AC #5). (701/701 both configs. The only touched existing tests are the two REGISTRY-state assertions in RenderParityTests/RenderSectionParityTests, whose own comments anticipated this story's entries — no rendering assertion changed.)
  - [x] Golden byte-identity: pinned fingerprint unchanged + self-site diff vs. `baseline_commit` build with only the 5 benign normalizations (memory: [golden-diff-normalization-gotchas]). (Fingerprint `d68a2fc9…` unchanged in Debug + Release; self-site diff of 202 files between the `fa1d79c` build and this branch's build over identical sources: zero non-benign diffs — the only residual was the diff harness's own differing `--output` path disclosed on the diagnostics page.)
  - [x] Extension-side verification is a documented manual smoke (F5 dev host against this repo: open panel → both surfaces render → edit an `.md` → in-place refresh → drill and back). Record the checklist + result in Completion Notes; do not build a TS test harness for a ~150-line shim (deliberate scope call — revisit only if the shim grows logic, which itself is a smell). (Went beyond: a browser harness drove the REAL payload + bridge through the full protocol — see Completion Notes; the one remaining true-VS-Code gap — Electron pixel paint + real watcher loop — is exactly ADR 0005's documented manual gap and the F5 checklist covers it.)

- [x] **Task 7 — Record + hand off** (AC: all)
  - [x] Completion Notes: which ADR-0005 form was built, the exact CLI/extension touchpoints, the manual smoke checklist result, any `HostRenderException` entries added (with reasons), and what Story 6.5 (theming + helpers) and Story 16.5 (packaging) inherit.
  - [x] Update [sprint-status.yaml](sprint-status.yaml) (6-4 → review at story end) and this file's Dev Agent Record / Change Log.

## Dev Notes

### What already exists — reuse it all, reinvent nothing
- **6.1 delivery seam (`done`):** [PageView](../../src/SpecScribe/PageView.cs) (+`PageKind`), `NavigationView`, `BreadcrumbTrail`, `AssetManifest`, `InteractionState` (drill parent/children + status stage, consuming `StatusStyles`), [IRenderAdapter](../../src/SpecScribe/IRenderAdapter.cs) (+`RenderedArtifact`), [HtmlRenderAdapter](../../src/SpecScribe/HtmlRenderAdapter.cs) (`.Shared`, `Id="html"`). The XML docs on `IRenderAdapter` literally name this story's adapter as the intended second implementation.
- **6.2 section view models (`review` — verify `done` at dev start):** `DashboardView` + `DashboardViewBuilder`, `EpicsView` records (`EpicChip`, `StoryCardView`, `DevAgentEntry`, `EpicsIndexView`, `EpicPageView`, `StoryPageView`, `StoryPlaceholderView`) + `EpicsViewBuilder`, rendered byte-identically by `HtmlRenderAdapter.Dashboard.cs` / `.Epics.cs` partials. The five templater entry points are already thin builder→adapter calls — the webview path taps in at exactly the same point: build domain models → build view → *your delivery form*.
- **Parity harness:** `RenderParity.SemanticFacts` + `FindDivergences` (chrome), `SectionFacts` + `FromDashboardView`/`FromEpicsIndexView`/`FromEpicPageView` + `ExtractDashboardSection`/`ExtractEpicsIndexSection`/`ExtractEpicPageSection` + `FindSectionDivergences` (sections; fact ids `section.statTiles`/`section.epicChips`/`section.storyRows`). [HostRenderExceptions.Registry](../../src/SpecScribe/HostRenderException.cs) is empty. This harness was built *for this story* — it is the AC #4 gate, not optional.
- **Watch machinery:** [FileWatcherService](../../src/SpecScribe/FileWatcherService.cs) (debounced `*.md` watching over `_bmad-output` + `docs/adrs`, routes to `SiteGenerator.RegenerateAdrs`/`RegenerateEpics`/`GenerateOne`/`RemoveFor`, emits `GenerationEvent`s) and the Spectre CLI ([Program.cs](../../src/SpecScribe/Program.cs): `generate`, `watch`, interactive default in [Commands.cs](../../src/SpecScribe/Commands.cs)).

### The 6.2 opaque-fragment reality (what the webview payload actually contains)
6.2's views are **data + named opaque HTML fragments + domain-model carriers**, deliberately: pure data records (`StatTile`, `ProgressBarView`, `NowNextCard`, `IndexCardView`/`IndexBand`, `EpicChip`, `StoryCardView`, …); chart panels carrying already-projected domain inputs (`EpicsModel`, `ProgressModel`, `CommandCatalog`, `WorkInventory`, `SprintStatus`, …) that the adapter renders via `Charts.*`; and named opaque fragments (`OverviewHtml`, story `RemainderHtml`, per-card `UserStoryHtml`/`AcBlocksHtml`/`NoteHtml`, guidance panels). Consequences:
- **C#-rendered-HTML path:** unproblematic — the adapter renders charts and emits fragments exactly as `HtmlRenderAdapter` does. This asymmetry is a large part of why the spike favors this path.
- **JSON path:** the domain-model carriers do **not** all round-trip `System.Text.Json` (`CommandCatalog` has no parameterless ctor — pinned in 6.2's `SectionViewModelSerializationTests` scope note). Serialize section data records as-is; for chart panels, ship C#-pre-rendered inline-SVG strings (charts are pure SVG + links, webview-safe) rather than re-modeling chart inputs for TS.
- The opaque fragments may contain links and (rarely) enhancement-dependent affordances — in the webview they must remain readable and navigable without `specscribe.js`; the tooltip/copy enhancements are convenience-only by policy and simply don't run there.

### Webview platform traps (the spike measures these; the ADR records them — highlights the dev must expect)
- **Links don't navigate.** A webview is not a browser tab: anchor clicks to other documents go nowhere by default. In-panel navigation needs interception (`acquireVsCodeApi().postMessage` → shim → new content). This is the single biggest "renders fine but AC #2 fails" trap.
- **CSP is strict by default:** inline scripts/styles need nonces; local resources need `asWebviewUri` under `localResourceRoots`; remote loads are blocked. Inline SVG is fine. The `?v=` asset cache-bust token scheme is meaningless inside a webview — assets load via webview URIs instead.
- **Panel lifecycle:** without `retainContextWhenHidden: true`, a hidden panel's DOM is discarded — breaking AC #3's "context remains coherent." Use it; still design updates as content patches, not re-creates.
- **Spawn model (if the shim spawns the CLI):** cold `dotnet` spawn latency is real; the spike measures it. Prefer one long-lived process (e.g. the watch/serve mode streaming updates) over per-navigation spawns if latency is poor — per ADR 0005.

### Architecture invariants that BOUND this story
- **AD-1/AD-2** ([ARCHITECTURE-SPINE.md:34–48](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): one core; adapters translate view models without reinterpreting sources. The extension re-parses **nothing**.
- **AD-6** (:74–80) + [ADR 0003](../../docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md): read-only; no write path (AC #6).
- **AD-7** (:82–88): theming boundary — SpecScribe tokens for content semantics now; host-variable mapping is Story 6.5.
- **AD-8** (:90–96) + [ADR 0004](../../docs/adrs/0004-cross-surface-interaction-and-theme-contract.md): interaction-state *shape* shared; update *transport* adapter-specific — "webview uses extension host push" is AD-8 verbatim; this story implements that clause.
- **Feature-parity rule** ([rendering-architecture.md:78–92](../specs/spec-specscribe/rendering-architecture.md)): adapters only map existing core view models; the webview reaches the same information without the HTML surface's enhancement scripts.
- **FR13** (read-only webview reusing shared parsing/projection), **NFR5** (shared reads, no write locks), **NFR6** (accessibility semantics are contract — keyboard drill, labels, non-color status text carry into the webview), **UX-DR14** (webview adaptation reuses core semantics; the "honoring host theme" half is 6.5) — [epics.md:48,85–86,126](../planning-artifacts/epics.md).

### Latest-tech notes (the 6.3 spike empirically verifies these — trust its ADR/notes over this list)
- Packaging CLI is `@vscode/vsce` (old `vsce` renamed); `esbuild` is the standard extension bundler; pin `engines.vscode` recent-but-not-bleeding-edge with matching `@types/vscode`.
- Webview API: `window.createWebviewPanel(...)`, `panel.webview.html`, `webview.postMessage` / `acquireVsCodeApi().postMessage` + `onDidReceiveMessage`, `asWebviewUri`, CSP meta with nonces.
- .NET delivery: `dotnet publish -c Release -r <rid> -p:PublishSingleFile=true --self-contained` if bundling — but the ship decision is ADR 0005's and the *packaging execution* is Story 16.5's.

### Risk centers (where review will focus)
1. **Skipping the gate** — building without ADR 0005 (or against unreviewed 6.2 records) re-creates the exact risk the spike was commissioned to retire.
2. **HTML-surface byte drift** — a new adapter/CLI branch that nudges any generated byte fails AC #5; the golden gate is non-negotiable (diagnostics page discloses config — make sure the new surface doesn't perturb it).
3. **Navigation that silently doesn't** — panel renders, links dead → AC #2 fails. Test drill down *and* breadcrumb up, across all five surfaces.
4. **Reset-style "refresh"** — re-creating the panel on file change technically updates content but fails AC #3's "without full panel reset."
5. **Fat shim** — TypeScript accreting rendering/logic that belongs in C#. The shim's job list is: command, panel, obtain content, relay messages, open-external. Anything else needs a stated reason.
6. **Parity theater** — declaring parity without running `FindSectionDivergences` for the `webview` surface, or dumping divergences into the exception registry without reasons.
7. **Scope leak into 6.5/16.5** — theme bridging, helper buttons, VSIX publish, CI. Named out; resist.

### Project Structure Notes
- **C#:** all new core code in [src/SpecScribe/](../../src/SpecScribe) `namespace SpecScribe;` (single csproj, `net10.0`, `Nullable enable`). Heavy XML-doc style with the *why*, tagged `[Story 6.4]` (match 6.1/6.2 density). Tests in [tests/SpecScribe.Tests/](../../tests/SpecScribe.Tests), file-per-unit.
- **TypeScript:** self-contained extension folder (suggest `extension/`; per ADR 0005), own `package.json`/`tsconfig.json`/build — excluded from the .NET solution and the generated-site pipeline. First product TS in the repo: keep it small enough that its entire surface is reviewable in one sitting.
- **Output dir** `SpecScribeOutput`; never `--output docs/live` (memory: [generate-output-dir-is-specscribeoutput]).
- **Branch discipline:** background auto-committer on `main` (memory: [worktree-edits-must-target-worktree-path]) — develop on a branch; re-capture `baseline_commit` at dev start.

### References
- **Epic + ACs:** [epics.md:884–918](../planning-artifacts/epics.md) (Story 6.4 + its split-provenance comment); Epic 6 goal + FR13 at [epics.md:815–819,48](../planning-artifacts/epics.md); FR32/FR33 (Epic 16 linkage) at [epics.md:73–74](../planning-artifacts/epics.md); NFR5/NFR6 at [epics.md:85–86](../planning-artifacts/epics.md); UX-DR14 at [epics.md:126](../planning-artifacts/epics.md).
- **Gate + seam decision:** [6-3-vs-code-integration-spike.md](6-3-vs-code-integration-spike.md) (the spike this story is seated by; its AC #3 defines ADR 0005's required contents); `docs/adrs/0005-*.md` (**must exist at dev start** — the governing document); [docs/adrs/README.md](../../docs/adrs/README.md).
- **Prior stories:** [6-1-shared-view-model-contract-for-html-and-webview-adapters.md](6-1-shared-view-model-contract-for-html-and-webview-adapters.md) (delivery contract + parity harness + scope discipline to copy); [6-2-read-only-vs-code-dashboard-and-epics-experience.md](6-2-read-only-vs-code-dashboard-and-epics-experience.md) (the view models + builders + serialization gotcha + File List of every seam file); [6-5-host-aware-theming-and-explicit-helper-actions.md](6-5-host-aware-theming-and-explicit-helper-actions.md) (what this story must NOT touch: theming + helpers; its gate table mirrors this one).
- **Architecture:** [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) AD-1/AD-2/AD-6/AD-7/AD-8 + Seed/no-split + Runtime Flow; [rendering-architecture.md](../specs/spec-specscribe/rendering-architecture.md) Delivery Adapter Layer, `IRenderAdapter`, Feature Parity Rules, Client-Side Enhancement Policy, Read-Only IDE Helper Pattern, Evolution Sequence step 4; [ADR 0002](../../docs/adrs/0002-shared-rendering-core-and-host-neutral-view-models.md), [ADR 0003](../../docs/adrs/0003-directory-scoped-settings-and-read-only-helpers.md), [ADR 0004](../../docs/adrs/0004-cross-surface-interaction-and-theme-contract.md).
- **Source seams:** [IRenderAdapter.cs](../../src/SpecScribe/IRenderAdapter.cs), [PageView.cs](../../src/SpecScribe/PageView.cs), [HtmlRenderAdapter.cs](../../src/SpecScribe/HtmlRenderAdapter.cs) (+ `.Dashboard.cs`/`.Epics.cs`), [DashboardView.cs](../../src/SpecScribe/DashboardView.cs)/[DashboardViewBuilder.cs](../../src/SpecScribe/DashboardViewBuilder.cs), [EpicsView.cs](../../src/SpecScribe/EpicsView.cs)/[EpicsViewBuilder.cs](../../src/SpecScribe/EpicsViewBuilder.cs), [RenderParity.cs](../../src/SpecScribe/RenderParity.cs), [HostRenderException.cs](../../src/SpecScribe/HostRenderException.cs), [FileWatcherService.cs](../../src/SpecScribe/FileWatcherService.cs), [Program.cs](../../src/SpecScribe/Program.cs)/[Commands.cs](../../src/SpecScribe/Commands.cs), [InteractionState.cs](../../src/SpecScribe/InteractionState.cs), [StatusStyles.cs](../../src/SpecScribe/StatusStyles.cs), [assets/specscribe.js](../../src/SpecScribe/assets/specscribe.js).
- **Test gates:** [SiteGeneratorAdapterTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) (golden fingerprint), [RenderSectionParityTests.cs](../../tests/SpecScribe.Tests/RenderSectionParityTests.cs) + [RenderParityTests.cs](../../tests/SpecScribe.Tests/RenderParityTests.cs) (patterns to mirror for the webview surface), [SectionViewModelSerializationTests.cs](../../tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs) (the serialization boundary).
- **Memory:** [[story-6-2-section-view-models-live]] (view-model inventory + JSON gotcha), [[story-6-1-delivery-seam-live]] (parity mechanics), [[epic-6-vscode-spike-and-renumber]] + [[epic-6-sequencing-and-6-5-theming]] (why the gate exists), [[charting-is-pure-svg-no-js]], [[specscribe-status-token-system]], [[golden-diff-normalization-gotchas]] (5 normalizations), [[generate-output-dir-is-specscribeoutput]], [[worktree-edits-must-target-worktree-path]], [[epic-4-adapter-contract-scope]] (spike-led greenfield pattern).

## Dev Agent Record

### Agent Model Used

Claude Fable 5 (claude-fable-5) via Claude Code — dev-story run 2026-07-11, branch `worktree-bmad-dev-story-6-4` off `fa1d79c`.

### Debug Log References

- `dotnet test`: 701/701 green, Debug AND Release (700 pre-existing + the story's new tests; one new-test fix along the way: an over-broad "no `specscribe.js` mention" assertion tripped on a CSS *comment* — tightened to pin the `<script src=` tag, which is the actual contract).
- Golden gates: pinned `GoldenContentFingerprint` (`d68a2fc9…`) unchanged in both configs; self-site byte diff (baseline `fa1d79c` binary vs. this branch's binary, both generating from the identical baseline source snapshot): 202 files, **zero non-benign diffs** under the 5 known normalizations (the single residual was the diff harness's own differing `--output` path, which the diagnostics page discloses by design).
- CLI smoke against this repo: `specscribe webview` → exit 0, 6.2 MB JSON payload, 93 surfaces (dashboard + epics index + 16 epic pages + 75 story pages/placeholders), 127 inline `<svg>` in the entry document, placeholders present, all content regions script-free, **no** `SpecScribeOutput` created (scratch site went to `%TEMP%/specscribe-webview/<hash>`).
- Extension: `npm run typecheck` clean; esbuild bundle 7.4 KB dev / **4.2 KB minified** (the spike's was 3.4 KB — still a probe, not a product).

### Completion Notes List

- **ADR-0005 form built: C#-rendered webview HTML** (the ratified reinterpretation of the epic's AC #1 — recorded in the Change Log). No JSON export exists; the webview's delivery is `WebviewRenderAdapter : IRenderAdapter` (`Id = "webview"`), plugging into 6.1's seam exactly as `IRenderAdapter`'s XML docs promised.
- **C# touchpoints:** `WebviewRenderAdapter` (new; full-document `Render`, swappable-region `RenderContent`, `WrapDocument`, the CSP shell + the ONE nonce'd bridge script), `WebviewBundle`/`WebviewSurface` (new records), `SiteGenerator.RenderWebviewSurfaces()` (new; renders all five surface families from the same cached models/builders/fragment pipeline the HTML site used — reference-linkified via the same `ApplyReferenceLinks`), `WebviewCommand` = **`specscribe webview`** (new Spectre command; pure-JSON stdout, stderr diagnostics, output redirected to a per-project temp scratch dir so the panel never writes into the project — AC #6). Enabling refactors, all mechanically byte-identical (golden-gated): `HtmlRenderAdapter.RenderNav` split into `RenderNavMarkup` + toggle-script const; templaters gained `Build*Page` (PageView) with `Render*` delegating; the per-story fragment block re-homed to `SiteGenerator.BuildStoryPageFragments` shared by both delivery paths.
- **Extension touchpoints (`extension/`, greenfield, self-contained):** one command `specscribe.openStatus`, one setting `specscribe.toolPath` (exe, or `.dll` run via `dotnet`; falls back to a bundled `bin/` binary — 16.5's slot — then PATH), `src/extension.ts` (~230 lines incl. comments): spawn → parse payload → substitute `__CSP_SOURCE__`/`__NONCE__` → set `webview.html` ONCE → everything after is in-place `postMessage`. Navigation: bridge resolves relative hrefs against the current surface path and posts `navigate`; shim answers from the cached bundle (instant), shows an info message for non-webview pages, and `openExternal`s only `https?`/`mailto`. Live-push: two debounced watchers (`_bmad-output/**/*.md`, `docs/adrs/**/*.md`) → one re-render spawn → patch the CURRENT surface with reason `refresh` (scroll preserved).
- **`HostRenderException` entries added (3, all `webview`, all chrome/asset, zero section facts):** `asset.css` (stylesheet inlined into `<style>` — CSP forbids plain local `<link>` loads; same CSS bytes, different carrier), `asset.js` (enhancement script deliberately absent — progressive-enhancement policy; the webview's only script is its own bridge), `mermaid` (no script can load under the CSP → roadmap degrades to readable preformatted text — ADR 0005's accepted fallback; this story hereby takes that option, bundling-with-nonce stays a 6.5+ possibility).
- **Verification beyond unit tests:** a browser harness loaded the REAL 93-surface payload with the REAL bridge script and an emulated shim, and drove the full protocol: dashboard paints (127 SVGs, 5 stat tiles); nav-click → epics index swaps in place (active-nav + breadcrumb correct, 16 chips, mermaid `<pre>` fallback present); epic drill → story drill (bare relative href resolved against `epics/` base) → breadcrumb up-drill all correct; non-surface link (`requirements.html`) correctly refused without disturbing the panel; refresh-reason push at scrollY=1200 preserved scroll exactly; 520 px panel width → no horizontal overflow and the bridge's delegated Menu toggle works with correct `aria-expanded`.
- **Remaining manual smoke (the single gap ADR 0005 names — Electron pixel paint + real watcher loop; not exercisable headlessly):** F5 checklist in `extension/README.md`: build tool → `npm install && npm run build` → F5 → set `specscribe.toolPath` to the built `SpecScribe.dll` → *SpecScribe: Open Status* → dashboard paints → drill epics→epic→story and breadcrumb back → edit an `_bmad-output` `.md` → visible surface refreshes in place with context intact.
- **Story 6.5 inherits:** the webview document shell in `WebviewRenderAdapter.DocumentTemplate` (one reviewable place to add `--vscode-*` mapping + theme bridging), the bridge script (add helper-action plumbing there), the `specscribe.toolPath` setting pattern, and the Mermaid bundling-with-nonce open option. **Story 16.5 inherits:** the `extension/bin/` bundled-binary slot the shim already prefers, `npm run package` (vsce) wiring, and the per-RID publish matrix. **Possible future optimization (noted, not owed):** scoped re-render / warm renderer to cut the ~2 s refresh (ADR 0005 flags ingest+git as the cost center); the debounce + in-place patch keep UX acceptable meanwhile.

### File List

**New — C# core:** `src/SpecScribe/WebviewRenderAdapter.cs`, `src/SpecScribe/WebviewBundle.cs`
**New — tests:** `tests/SpecScribe.Tests/WebviewRenderAdapterTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs`
**New — extension:** `extension/package.json`, `extension/tsconfig.json`, `extension/esbuild.js`, `extension/src/extension.ts`, `extension/README.md`, `extension/.gitignore`, `extension/.vscodeignore`, `extension/.vscode/launch.json`, `extension/.vscode/tasks.json`
**Modified — C# core:** `src/SpecScribe/Commands.cs` (WebviewCommand), `src/SpecScribe/Program.cs` (command registration), `src/SpecScribe/HtmlRenderAdapter.cs` (RenderNav → RenderNavMarkup + script const, bytes unchanged), `src/SpecScribe/HtmlTemplater.cs` (BuildIndexPage split), `src/SpecScribe/EpicsTemplater.cs` (Build*Page splits ×4), `src/SpecScribe/HostRenderException.cs` (3 webview registry entries), `src/SpecScribe/SiteGenerator.cs` (BuildStoryPageFragments re-homing, artifact/reference-map caches, RenderWebviewSurfaces)
**Modified — tests:** `tests/SpecScribe.Tests/RenderParityTests.cs`, `tests/SpecScribe.Tests/RenderSectionParityTests.cs` (the two registry-state tests, updated exactly as their comments anticipated)
**Modified — tracking:** `_bmad-output/implementation-artifacts/6-4-read-only-vs-code-webview-runtime-for-dashboard-and-epics.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml`

## Change Log

- 2026-07-11 — **Story implemented end-to-end; Status → review.** Built ADR 0005's ratified form: `WebviewRenderAdapter` (surface id `webview`, second `IRenderAdapter`) + `SiteGenerator.RenderWebviewSurfaces()` + the additive `specscribe webview` CLI command (JSON bundle of ALL five surface families on stdout; scratch output to temp — the project is never written) + the production `extension/` thin shim (in-webview navigation via nonce'd bridge + `postMessage`, live host-push via debounced extension-host watchers, in-place swaps only — `webview.html` set once). AC #1 satisfied in its ADR-ratified reinterpretation; AC #2/#3 verified by a browser harness driving the real payload + bridge (drill/breadcrumb/active-nav/scroll semantics, narrow-panel sanity at 520 px); AC #4 via chrome + section parity under `webview` with exactly 3 justified chrome/asset `HostRenderException` entries (`asset.css`, `asset.js`, `mermaid` — Mermaid text-fallback option formally taken); AC #5 via unchanged golden fingerprint (Debug+Release) + a 202-file self-site byte diff vs. the `fa1d79c` baseline build (zero non-benign diffs); AC #6 by construction (temp scratch output; a no-writes regression test pins it). 701/701 tests green. The two registry-state test assertions (6.1/6.2) were updated to their anticipated 6.4 form — no rendering assertion changed. Remaining manual gap (per ADR 0005): one F5 smoke in real VS Code — checklist in `extension/README.md`.
- 2026-07-11 — **Task 1 gate check passed + ADR 0005 reconciliation recorded (dev-story).** [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) is **Accepted** (2026-07-10, amended/re-affirmed by ADR 0006) and Story 6.2 is `done` (code-review 2026-07-11), so the gate holds. (Story 6.3 itself sits in `review`, but its deliverable — ADR 0005 + the spike findings — has landed, which is what this story's dev-start protocol requires.) The ADR **ratifies the C#-rendered-webview-HTML form** of AC #1: this story builds `WebviewRenderAdapter : IRenderAdapter` (`Id = "webview"`); the "JSON view-model export the TS webview renders" reading is REJECTED, so AC #1 is satisfied in its ratified reinterpretation (recorded here as the story predicted). Further ADR rulings adopted verbatim: extension obtains content by **spawning the .NET tool and reading stdout** (~1.8–2.0 s warm / ~3.5 s cold measured); live-push = **extension-host `FileSystemWatcher` → debounced re-render → in-place `postMessage`** (NOT a C# `FileWatcherService` bridge); the shim injects exactly two host-runtime placeholders (`__CSP_SOURCE__`, `__NONCE__`); CSP posture = `script-src 'nonce-…'` strict + `style-src 'unsafe-inline'` (126 inline style attrs measured); inline SVG charts survive unchanged; **Mermaid degrades to readable preformatted text** (this story accepts the ADR's text-fallback option; bundling-with-nonce deferred to 6.5+ if ever); ADR 0005 names no production extension folder, so this story's suggested `extension/` at repo root stands. No story-file section required structural rewriting — the story was authored to be implementable under exactly this ruling. `baseline_commit` re-captured to `fa1d79c` (the HEAD this story's branch `worktree-bmad-dev-story-6-4` was cut from; the frontmatter value previously pointed at the create-story-time HEAD).
- 2026-07-10 — Story 6.4 drafted (create-story, owner-selected ahead of its gate). Context captured for the read-only VS Code webview runtime: the production extension (thin TS shim) + the C# delivery of Story 6.2's dashboard/epics section view models + live host-push. **Hard gate recorded: dev-story must not begin until Story 6.3's spike lands ADR 0005 (which governs AC #1's delivery form — JSON export vs. C#-rendered webview HTML) and Story 6.2 clears review.** At create-story the spike has not run and ADR 0005 does not exist; this story is deliberately written to be implementable under either ADR ruling, with a dev-start reconciliation task (Task 1) making the ADR authoritative. Derived ACs added: webview parity via RenderParity under surface id `webview` (AC #4), HTML-surface byte-identity (AC #5), end-to-end read-only (AC #6). Host theming + helper actions (Story 6.5), VSIX/Marketplace packaging + CI (Epic 16), other page bodies, extension-side `.md` parsing, and the pending Story 5.x CLI scope are explicitly out.
