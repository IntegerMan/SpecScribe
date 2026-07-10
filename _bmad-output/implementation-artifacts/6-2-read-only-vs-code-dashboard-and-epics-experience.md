# Story 6.2: Dashboard & Epics Section View Models (Rendering-Core Decomposition)

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,
I want the dashboard and epics page **bodies** decomposed into shared, host-neutral **section view models** in the rendering core (with the HTML adapter re-rendering them byte-for-byte identically),
so that a future VS Code webview can render those two surfaces from the same typed data — not by scraping the HTML surface — while feature semantics stay provably equivalent.

## Scope Decision — READ FIRST (this story was split; confirm or veto before dev)

**Story 6.2 in [epics.md:825–858](../planning-artifacts/epics.md) bundled two very different bodies of work.** At create-story time the owner (Matthew-Hope) split it:

- **AC #1 — rendering-core body decomposition (THIS STORY).** Decompose the dashboard + epics page bodies into shared host-neutral section view models; the HTML adapter re-renders them byte-for-byte identically; no other page body is decomposed. Pure C#. This is the natural next step Story 6.1 explicitly teed up: 6.1 shipped the delivery contract + shared **chrome** (nav/breadcrumb/shell) + parity harness and left every page **body** opaque, naming *this* story as the home for the dashboard + epics body decomposition ([6.1 Scope Decision, "OUT of scope" bullet 3](6-1-shared-view-model-contract-for-html-and-webview-adapters.md)).
- **AC #2 + #3 — the VS Code webview runtime (SPLIT OUT to a follow-up story, NOT in scope here).** The actual extension: a webview UI for the dashboard + epics and live host-push updates. **There is no VS Code extension in the repo at all** (no `package.json`, no TypeScript, no extension host) — it is a greenfield surface in a new tech stack with its own data-transport architecture decision. Building it here would fuse two unrelated stacks into one un-reviewable story, exactly the "split structural growth, don't absorb it" failure the Epic 2 retro flagged (memory: [epic-2-retro-scope-and-debt]). The epics.md AC #1 authoring comment ([epics.md:831–837](../planning-artifacts/epics.md)) already half-acknowledged this seam.

**Decisions captured at create-story (owner-confirmed):**

1. **Scope:** this story = **AC #1 only** (the decomposition). The webview runtime is deferred to a new follow-up story that must be formally seated (see "Follow-up: seat the webview-runtime story" below). This mirrors how 6.1 shipped the contract with zero webview UI.
2. **Webview data path (directional, for the deferred story — shapes THIS story's design):** the eventual webview will consume a **new JSON view-model export** of these section view models (chosen over "run the tool and load the generated HTML" and "a second HTML-ish render adapter"). **Consequence you must honor now:** every section view-model record this story adds is **pure, JSON-serializable data** — no HTML strings except explicitly-named opaque rich-content fragments, no delegates/`Func`/behavior — so the deferred export is a trivial `JsonSerializer.Serialize`. That is the design north-star for AC #1's "host-neutral section view models."

**The one-line test for "is this in scope?":** if the change is *decomposing the dashboard or epics body into typed section data that the HTML adapter renders byte-identically*, it's in. If it's *building the extension/webview*, *a JSON export*, *host-push wiring*, *host theming*, or *decomposing any other page body*, it's out.

**If you disagree with this split, raise it before writing code, not at review** (Epic 3 retro action item: don't defer a defining decision to the dev and correct it later).

## Acceptance Criteria

**AC #1 — verbatim from [epics.md:841–846](../planning-artifacts/epics.md) (Story 6.2 AC #1):**

1. **Given** Story 6.1's view-model contract carries page bodies as opaque payloads
   **When** the dashboard and epics surfaces are prepared for the webview
   **Then** the dashboard and epics page bodies are decomposed into shared, host-neutral section view models in the rendering core
   **And** the HTML adapter re-renders them byte-for-byte identically (parity harness green)
   **And** no other page body is decomposed (only the surfaces a webview consumer renders).

**Additional acceptance criteria for THIS story (derived from the captured decisions — the dev must also satisfy these):**

2. **Given** the JSON view-model export is the chosen (deferred) webview data path
   **When** the new section view-model records are defined
   **Then** every one is a plain, JSON-serializable data record (zero HTML except explicitly-named opaque rich-content fragments; no delegates, no rendering behavior)
   **And** a round-trip test proves the dashboard + epics section view models serialize to JSON and back with no loss (proving the export the follow-up story needs is trivial, without building the export path).

3. **Given** a future webview renders these sections from the section view models
   **When** the parity harness runs
   **Then** it is extended to cover the new dashboard + epics **section** facts (stat tiles, cards, panel data, epic/story rows, drill targets, statuses) in addition to 6.1's chrome facts
   **And** an injected divergence in a section fact is caught (the harness genuinely detects section regressions, per AC #1's "parity harness green").

## IN scope

- **Section view-model records (pure, JSON-serializable data)** for exactly the two surfaces a webview consumer renders:
  - **Dashboard (the home page body — [HtmlTemplater.RenderIndex:113–228](../../src/SpecScribe/HtmlTemplater.cs))**: an ordered dashboard view (e.g. `DashboardView` / `IReadOnlyList<DashboardSection>`) whose **data sections become typed records**: the stat-tile row (`StatTile { Number, Label, Sub?, Tooltip? }` — the `Charts.StatCard` inputs), Now & Next / sprint-board cards (`{ CssClass, Kicker, Title, Href }`), Overall-Progress bars (`{ Label, Value, Max, RightLabel? }`), the Requirements panel counts, the dashboard quick-links, the Work-Types section, and the index-grid bands (planning / implementation / unrecognized-folder / ADR / retro cards: `{ Title, Href, Status?, Meta?, SourcePath }`).
  - **Epics (three page families in [EpicsTemplater.cs](../../src/SpecScribe/EpicsTemplater.cs))**:
    - **Epics index** ([RenderIndex:9–94](../../src/SpecScribe/EpicsTemplater.cs)): header counts, the progress panel data, the chip sections (`EpicChip { Number, Title, Status, Href }`), the epic cards, and the drill list.
    - **Epic page** ([RenderEpic:96–213](../../src/SpecScribe/EpicsTemplater.cs)): the epic header + status stage, the story cards (`StoryCardView { Id, Title, StatusStage, TaskBadge, Href }`), the next-actions / up-next panel, the retro affordance.
    - **Story page** ([RenderStory:214–378](../../src/SpecScribe/EpicsTemplater.cs)) + **story placeholder** ([RenderStoryPlaceholder:380–465](../../src/SpecScribe/EpicsTemplater.cs)): the story metadata + status + drill; the rendered story prose is an **opaque rich fragment** (see below).
- **The HTML adapter renders the section view models to today's exact bytes.** Add body-rendering to the delivery seam: e.g. `HtmlRenderAdapter.RenderDashboardBody(DashboardView)` / `RenderEpicsIndexBody(...)` / `RenderEpicBody(...)` / `RenderStoryBody(...)` that return the `<main>…</main>` body string. The templaters become thin: build the domain models → build the section view model (a new `DashboardViewBuilder` / `EpicsViewBuilder` in the rendering core) → `adapter.RenderXBody(view)` → set the result as `PageView.BodyHtml` → flow through `HtmlRenderAdapter.Render(page)` exactly as 6.1 wired it. **6.1's `PageView.BodyHtml` opaque-string seam stays** — it now simply carries an adapter-rendered-from-data body instead of a templater-hand-built one.
- **Chart + rich-prose content stays opaque, but its DATA-shaped inputs are modeled where they already are data.** Inline-SVG charts (`Charts.Sunburst`, `Charts.Donut`/`EpicStatusPanel`, `Charts.RefinementFunnel`, `Charts.GitPulsePanel`, `Charts.CoverageMeter`/`ArtifactCoveragePanel`, `Charts.EpicMosaic`), the `Mermaid.RoadmapDiagram` block, and Markdig-rendered prose (`model.OverviewHtml`, `model.RequirementsInventoryHtml`, the story body HTML) are **carried through the section view model as either (a) the already-projected domain input the chart/prose is built from** (`ProgressModel`, `EpicsModel`, `GitPulse`, `ArtifactCoverage`, `RequirementsModel` — these are ALREADY parsed/projected data, so the adapter calling `Charts.Sunburst(model)` re-parses nothing) **or (b) an explicitly-named opaque pre-rendered fragment** (`string ChartSvg` / `string RichHtml`). **Prefer (a)** so the JSON export carries real data; use (b) only where modeling the input is disproportionate — and name every opaque field + document it as the deferred chart-data/prose-decomposition seam.
- **Parity-harness extension** (AC #3 above): extend [RenderParity.cs](../../src/SpecScribe/RenderParity.cs) so `SemanticFacts` and `FromPageView`/`Extract`/`FindDivergences` also cover the new dashboard + epics **section** facts (stat-tile values, card targets, epic/story-row ids + status stages + drill hrefs), scoped to the body region. Keep it **semantic, not a byte differ** (the golden test covers bytes) — a webview emitting different markup but equal section meaning must still pass; a dropped/renamed section fact must fail. The `HostRenderException` registry stays the only sanctioned divergence home (still empty).
- **Byte-identical golden regression** — the single most important gate. The generated site stays byte-for-byte unchanged vs. the pre-change build.

## OUT of scope (do NOT start it here)

- **The VS Code extension / webview UI and live host-push (epics.md AC #2 + #3).** Split to a follow-up story (see below). Zero TypeScript, zero `package.json`, zero extension host, zero webview markup in this story.
- **The JSON view-model export path itself.** The chosen webview data path, but **deferred** — this story only *designs for it* (AC #2: serializable records + a serialize/deserialize round-trip test). Do NOT add an export command, a `--json` flag, or a serialization entry point; that lands with the webview-runtime story that consumes it.
- **Any OTHER page body.** Only the dashboard (home) and epics (index/epic/story/placeholder) bodies decompose — the exact surfaces a webview consumer renders (AC #1: "no other page body is decomposed"). Docs, sprint, requirements, ADR, git-insights, deep-analytics, retro, structure, diagnostics, about, commit-day bodies stay hand-built in their templaters and keep flowing through 6.1's opaque `PageView.BodyHtml`. Their bytes must not change.
- **Modeling chart geometry into data** (SVG arc paths, donut math, heatmap cell layout). That is `Charts.cs`'s job and is byte-risky. Charts stay pure SVG + links (memory: [charting-is-pure-svg-no-js]); the section view model carries the chart's *input data*, never its geometry.
- **Any rendered-byte change.** The golden regression is the guardrail, identical to 4.1/6.1 AC #1: if any generated byte changes, you have overstepped.
- **Status vocabulary / canonical status model.** `StatusStyles` is the status→stage seam (Story 8.1 hardens it). Section view models **consume** the stage from `StatusStyles.ForEpic`/`ForStory` (as 6.1's `InteractionState` already does); they do not relabel, remap, or move status logic (memory: [specscribe-status-token-system]).
- **Package/namespace split** — still seed-level, still forbidden (same ruling as 4.1 and 6.1). All new types are new `.cs` files in `namespace SpecScribe;` in the single `src/SpecScribe/SpecScribe.csproj`.
- **Host theming / VS Code chrome variables** — Story 6.3 / AD-7.

## Tasks / Subtasks

- [ ] **Task 1 — Define the dashboard section view models** (AC: #1, #2)
  - [ ] Add a `DashboardView` (ordered `IReadOnlyList<DashboardSection>`, or an explicit typed record with each section as a field) plus the section records as new `.cs` files in `namespace SpecScribe;`. Data sections are pure records: `StatTile { string Number, string Label, string? Sub, string? Tooltip }` (mirrors `Charts.StatCard` args), the Now&Next card list, Overall-Progress bars, requirements-panel counts, quick-links, work-types, and index-grid card records (`{ Title, Href, Status?, Meta?, SourcePath }`).
  - [ ] Chart/rich sections carry the already-projected domain input (`ProgressModel`, `EpicsModel`, `GitPulse`, `ArtifactCoverage`, `RequirementsModel`, `CommandCatalog`) OR a named opaque fragment — see the "Chart + rich-prose" IN-scope bullet. Every opaque field is XML-doc'd as the deferred seam.
  - [ ] Preserve every **conditional** in [AppendDashboard:277–414](../../src/SpecScribe/HtmlTemplater.cs): the fifth stat card only when `!work.IsEmpty`; Now&Next / sunburst / mosaic only when `epicsModel is not null`; the tasks-vs-"none tracked" stat fork; the deep-git header links fork; the coverage panel only when `coverage is { IsEmpty: false }`; the git-pulse empty-state fallback. These are byte-load-bearing — model them as nullable/optional sections so an absent input renders exactly nothing.
  - [ ] Every record is `[Story 6.2]`-tagged, heavily XML-doc'd with the *why*, matching the 6.1 / `IArtifactAdapter.cs` comment density. No delegates, no HTML in data fields. No new project, no namespace split.

- [ ] **Task 2 — Define the epics section view models** (AC: #1, #2)
  - [ ] `EpicsIndexView`, `EpicPageView`, `StoryPageView`, `StoryPlaceholderView` as new records. Epics index: header counts + progress + `EpicChip { Number, Title, Status, Href }` chip sections + epic cards + `OverviewHtml`/`RequirementsInventoryHtml`/roadmap as named opaque fragments. Epic page: header + status stage + `StoryCardView { Id, Title, StatusStage, TaskBadge, Href }` list + next-actions + retro affordance. Story page: metadata + status + drill; **the Markdig-rendered story prose stays a named opaque `RichHtml` fragment** (it is inherently HTML — do not model it into data).
  - [ ] Preserve the epics conditionals: the empty-epics guidance ([RenderIndex:39–42](../../src/SpecScribe/EpicsTemplater.cs)), the overview banner / requirements-inventory `<details>` only when non-empty, the section dividers only when epics exist, the retro affordance / epic-retro-link presence.
  - [ ] Status stages come from `StatusStyles.ForEpic`/`ForStory` — reference, never re-model.

- [ ] **Task 3 — Render section view models to byte-identical HTML in the adapter** (AC: #1)
  - [ ] Add body-rendering to the delivery seam: `HtmlRenderAdapter.RenderDashboardBody(DashboardView)` and `RenderEpicsIndexBody`/`RenderEpicBody`/`RenderStoryBody`/`RenderStoryPlaceholderBody`, each returning the `<main>…</main>` body string. **Re-home, don't rewrite:** move the existing `Append*` / `Charts.*` / `Mermaid.*` string-building into the adapter driven by the section view models; for opaque fragments, emit the carried string verbatim. The cleanest byte-safe path is to keep calling the existing `Charts.*` and markdown helpers so the produced strings are unchanged.
  - [ ] Rewire the five templater entry points ([HtmlTemplater.RenderIndex](../../src/SpecScribe/HtmlTemplater.cs), [EpicsTemplater.RenderIndex/RenderEpic/RenderStory/RenderStoryPlaceholder](../../src/SpecScribe/EpicsTemplater.cs)) to: build domain models → build the section view model (new `DashboardViewBuilder`/`EpicsViewBuilder`) → `adapter.RenderXBody(view)` → assign to `PageView.BodyHtml` → `HtmlRenderAdapter.Shared.Render(page)`. Keep the `PageView`/`AssetManifest`/`InteractionState` construction exactly as 6.1 left it (including `MermaidNeeded = Mermaid.ContainsBlock(body)`).
  - [ ] **Byte-identical constraint:** the emitted body strings must be exactly today's. The golden test (Task 5) is the gate; run it after each surface.

- [ ] **Task 4 — Extend the parity harness to section facts** (AC: #1, #3)
  - [ ] Extend [RenderParity.cs](../../src/SpecScribe/RenderParity.cs): add the new dashboard + epics **section** facts to `SemanticFacts` (stat-tile values + labels, card/link targets, epic-chip + story-card ids/statuses/hrefs, panel presence), with a `FromPageView`/section-view-model source of truth and an `Extract` that recovers them from the rendered body region (scoped so chrome facts and body facts don't collide). Add the divergence checks to `FindDivergences`.
  - [ ] Keep it semantic, not a byte differ. Confirm the `HostRenderException` registry is still empty and still filters a section-fact divergence when registered.

- [ ] **Task 5 — Byte-identical golden regression + suite green** (AC: #1)
  - [ ] Golden-output regression: the full generated site is byte-for-byte unchanged vs. the pre-change build for the [SiteGeneratorAdapterTests](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) fixture (the pinned golden inventory + content — the same gate 4.1/6.1 used). Normalize ONLY the three known-benign diffs (memory: [golden-diff-normalization-gotchas]): the footer wall-clock, the `?v=<ModuleVersionId>` cache-bust token, git-worktree CRLF.
  - [ ] Run `dotnet test` — whole suite green. **If any existing rendering assertion must change, STOP** — that means you altered rendered output, which fails AC #1 (same rule as 4.1/6.1).
  - [ ] Generate this repo's own site to `SpecScribeOutput` and diff against a pre-change build: **zero diffs** (memory: [generate-output-dir-is-specscribeoutput]; NOT `--output docs/live`). Generate from an **isolated** source copy to avoid the `.claude/worktrees/*`-sorts-first stale-`epics.md` gotcha (memory: [epic-4-adapter-contract-scope]).

- [ ] **Task 6 — Tests for the section contract + serialization + parity** (AC: #1, #2, #3)
  - [ ] Unit tests: a representative `DashboardView` and each epics view round-trips through the adapter and the extended parity extractor with equal section facts. Follow the file-per-unit naming already in the suite (e.g. extend/add `RenderViewModelTests.cs`, `HtmlRenderAdapterTests.cs`, `RenderParityTests.cs`; add `DashboardViewTests.cs` / `EpicsViewTests.cs` if a new unit warrants one).
  - [ ] **Serialization round-trip test (AC #2):** the dashboard + epics section view models serialize to JSON (`System.Text.Json`) and deserialize back with no loss — proving the deferred export is trivial. This test does NOT add an export path; it only exercises `JsonSerializer` on the records. (If a field cannot serialize cleanly, that field is the wrong shape — fix the record, don't add a converter, unless it's a documented opaque-fragment string.)
  - [ ] **Injected-divergence test (AC #3):** an injected fake divergence in a section fact is caught by the extended parity harness (mirrors 6.1's chrome-fact injection test).

- [ ] **Task 7 — Watch-mode + reporting parity** (AC: #1)
  - [ ] Confirm the decomposition does not regress watch-mode: `RegenerateEpics`/`GenerateOne` must still produce identical pages (they compose the same re-homed epics/dashboard bodies). Existing watch-path tests ([SiteGeneratorStoryEpicPagesTests](../../tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs), [SiteGeneratorCoverageTests](../../tests/SpecScribe.Tests/SiteGeneratorCoverageTests.cs)) must pass unchanged; note in Completion Notes how parity was kept (mirror 6.1).
  - [ ] No change to `IGenerationReporter`/`GenerationEvent`, and no touch to the 4.1 ingestion seam (`IArtifactAdapter`/`ArtifactBundle`/`AdapterDiagnostic`). This story is renderer-internal (AD-2).

## Dev Notes

### What "section view models" means here — and why the JSON-export decision is the boundary that keeps it honest
6.1 established the delivery contract ([IRenderAdapter](../../src/SpecScribe/IRenderAdapter.cs)) and modeled the shared **chrome** + page **identity** + **interaction** ([PageView](../../src/SpecScribe/PageView.cs), [NavigationView](../../src/SpecScribe/NavigationView.cs), [BreadcrumbTrail](../../src/SpecScribe/BreadcrumbTrail.cs), [InteractionState](../../src/SpecScribe/InteractionState.cs), [AssetManifest](../../src/SpecScribe/AssetManifest.cs)), but deliberately carried each page **body** as an opaque `PageView.BodyHtml` string. This story fills exactly two of those opaque bodies — the dashboard and the epics family — with typed section data, because those are the only bodies a webview consumer renders (AC #1). **The webview will consume a JSON export of these records** (owner decision), which is the discipline that keeps the decomposition real: if a "section view model" is just an opaque HTML blob, a JSON export of it is useless and you have not actually decomposed anything. So data-shaped sections MUST be data. Inline SVG and Markdig prose are the two exceptions that are inherently markup — carry them as named opaque fragments (SVG renders fine inline in a webview anyway) or, better, as the already-projected domain input the adapter renders. Everything else becomes fields.

### The re-home-don't-rewrite pattern (identical discipline to 6.1)
6.1 moved the nav/breadcrumb **string-building** out of `SiteNav` into `HtmlRenderAdapter` and made `SiteNav.RenderNavBar`/`RenderBreadcrumb` **delegate** — bytes unchanged, proven by the golden fingerprint. Do the same one level down: the `Append*` body-building in [HtmlTemplater.cs](../../src/SpecScribe/HtmlTemplater.cs) / [EpicsTemplater.cs](../../src/SpecScribe/EpicsTemplater.cs) moves into `HtmlRenderAdapter.RenderXBody`, driven by section view models, still calling the same `Charts.*` / `Mermaid.*` / markdown helpers so the produced strings are identical. The templater's job shrinks to: gather domain models → build the section view model → ask the adapter to render the body. **Prefer delegating to existing helpers over re-typing their strings** — every hand-copied string is a byte-drift risk the golden test will catch, but cheaper to avoid.

### Byte-load-bearing conditionals (the biggest byte-drift trap)
The dashboard body is a lattice of `if`s that each add or omit a whole panel (fifth stat card, Now&Next, sunburst, coverage panel, deep-git header links, git-pulse empty state; epics: empty-epics guidance, overview banner, requirements inventory, section dividers). Model these as **optional/nullable sections** so an absent input renders byte-for-byte nothing — a section list that always emits a placeholder would change bytes on projects that today emit nothing. Re-read [AppendDashboard:277–414](../../src/SpecScribe/HtmlTemplater.cs) and [EpicsTemplater.RenderIndex:9–94](../../src/SpecScribe/EpicsTemplater.cs) line-by-line and map every branch to a section-view-model shape before writing the adapter.

### Charts stay pure SVG + links; status routes through StatusStyles
Do not model chart geometry into data (memory: [charting-is-pure-svg-no-js]) — the section view model carries the chart's *input* (`EpicsModel`, `ProgressModel`, `GitPulse`, …), and the adapter calls the existing `Charts.*` method. Status stages come from `StatusStyles.ForEpic`/`ForStory` (memory: [specscribe-status-token-system]); reference them exactly as [InteractionState](../../src/SpecScribe/InteractionState.cs) already does — no new status vocabulary (the 8.1 boundary, same as 6.1).

### There is no client-side drill state (unchanged from 6.1)
"Drill" is hyperlink navigation between generated pages; the client JS ([assets/specscribe.js](../../src/SpecScribe/assets/specscribe.js)) is progressive enhancement only (memory: [tooltip-clipping-use-ss-tooltip-node]). Section view models model the *content and drill targets*, never hover/JS behavior. The webview parity rule ([rendering-architecture.md:84–92](../specs/spec-specscribe/rendering-architecture.md)) — "the webview adapter must reach the same information without depending on the HTML surface's enhancement scripts" — is exactly what the extended parity harness (Task 4) enforces at the section level.

### Non-negotiable invariants (from the architecture spine)
- **AD-1 / AD-2** ([ARCHITECTURE-SPINE.md:34–48](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): one shared core emits typed view models; adapters consume them **without reinterpreting source artifacts**. The section view models are built in the rendering core; the `HtmlRenderAdapter` renders them and re-parses nothing.
- **AD-8** ([ARCHITECTURE-SPINE.md:90–96](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)): interaction-state *shape* is shared, update *transport* is adapter-specific — this story adds section content to the shared shape; it implements no transport.
- **Feature-parity rule** ([rendering-architecture.md:78–82](../specs/spec-specscribe/rendering-architecture.md)): new user-visible rendering features land in the rendering core first; adapters only map existing core view models. The dashboard + epics section models ARE that core landing.
- **NFR4** ([epics.md:78](../planning-artifacts/epics.md)): extensible so new adapters need no core rewrite — the section view models are what a future `WebviewRenderAdapter` binds to.

### Risk centers (where reviews will focus)
1. **Output drift** — any changed rendered byte fails AC #1. The golden test (Task 5) is the guardrail; run it after each surface, not just at the end. Prefer delegating to existing `Charts.*`/`Append*` string-building over re-implementing it.
2. **Conditional omissions** — a byte-load-bearing `if` modeled as an always-emitted section changes bytes on the projects where it used to emit nothing. Map every branch first.
3. **Fake decomposition** — a "section view model" that is just an opaque HTML string proves nothing and fails the JSON round-trip (AC #2). Data sections must be data.
4. **Section-fact parity that only re-checks chrome** — the harness extension must actually assert the NEW section facts (stat tiles, cards, rows), and an injected section divergence must fail (AC #3), or it is theater.
5. **Scope leak into the webview or the JSON export** — building any extension code, a `--json` flag, or an export entry point is the follow-up story's job, not this one.

### Project Structure Notes
- Single project: [src/SpecScribe/SpecScribe.csproj](../../src/SpecScribe/SpecScribe.csproj) (`net10.0`, `Nullable enable`, `ImplicitUsings enable`). All new files go here in `namespace SpecScribe;`. No new project, no namespace split (seed-level, deferred — [ARCHITECTURE-SPINE.md:98–101](../specs/spec-specscribe/ARCHITECTURE-SPINE.md)).
- Tests: [tests/SpecScribe.Tests/](../../tests/SpecScribe.Tests) (xUnit, `net10.0`). Follow existing file-per-unit naming; the 6.1 test files ([RenderViewModelTests.cs](../../tests/SpecScribe.Tests/RenderViewModelTests.cs), [HtmlRenderAdapterTests.cs](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs), [RenderParityTests.cs](../../tests/SpecScribe.Tests/RenderParityTests.cs)) are the pattern to extend.
- **Output dir is `SpecScribeOutput`** (memory: [generate-output-dir-is-specscribeoutput]). Never `--output docs/live`.
- This session/story runs on `main` (not a worktree). Edits target `C:\Dev\SpecScribe` directly. There is a background auto-committer on `main` (memory: [worktree-edits-must-target-worktree-path]) — keep commits coherent.
- Match the heavy XML-doc-comment style of the surrounding files (every public type/member carries a `<summary>` explaining the *why*, tagged `[Story N.M]`); tag new members `[Story 6.2]`.
- **`baseline_commit` for the byte-parity diff:** capture the current `HEAD` at dev start into this file's frontmatter (as 6.1 did) and diff against it. 6.1's own baseline note warns intervening non-story commits (e.g. the background auto-committer) can sit between the recorded baseline and HEAD, so isolate *this story's* diff against the HEAD you branched from.

### Follow-up: the webview-runtime work is already seated as Story 6.4 (not this story's job)
The former epics.md Story 6.2 AC #2 + #3 (the VS Code webview runtime + live host-push) were split out at create-story and are **already seated** as **Story 6.4: Read-Only VS Code Webview Runtime for Dashboard and Epics** ([epics.md](../planning-artifacts/epics.md) Epic 6, `backlog` in [sprint-status.yaml](sprint-status.yaml) — append-only/no-renumber per the project convention, memory: [epic-4-adapter-contract-scope]). 6.4 depends on THIS story (the section view models) and introduces the JSON view-model export it consumes (6.4 AC #1). **Sequencing:** 6.4 runs after this story and before Story 6.3 (host theming depends on the webview existing), even though 6.4's number sorts after 6.3. Nothing to do here — just do NOT build any of 6.4's webview/export code in this story; run `create-story 6.4` to detail it when scheduled.

### References
- [epics.md:799–858](../planning-artifacts/epics.md) — Epic 6 goal + Story 6.2 ACs (AC #1 is this story's source of truth; the AC #1 authoring comment at :831–837 documents why the decomposition was surfaced as its own AC); [epics.md:48,151](../planning-artifacts/epics.md) — FR13 (read-only webview reusing shared parsing/projection); [epics.md:78](../planning-artifacts/epics.md) — NFR4.
- [6-1-shared-view-model-contract-for-html-and-webview-adapters.md](6-1-shared-view-model-contract-for-html-and-webview-adapters.md) — the contract this story builds on; its Scope Decision explicitly hands the dashboard + epics body decomposition to this story, and its `PageView.BodyHtml` is the opaque seam being filled. Copy its scope discipline, byte-identical guardrail, and comment style.
- [ARCHITECTURE-SPINE.md:34–48 (AD-1/AD-2), 90–96 (AD-8), 98–101 (Seed — no package split)](../specs/spec-specscribe/ARCHITECTURE-SPINE.md).
- [rendering-architecture.md:22–30 (Rendering Core Layer + IRenderAdapter), 78–92 (feature-parity + client-side enhancement policy — the webview-must-not-depend-on-HTML-scripts rule the parity harness enforces), 111–116 (Evolution Sequence)](../specs/spec-specscribe/rendering-architecture.md).
- Source to decompose: [HtmlTemplater.RenderIndex + AppendDashboard + AppendWorkTypesSection + AppendPlanningSection + AppendAdrSection + AppendRetrosSection + index cards](../../src/SpecScribe/HtmlTemplater.cs); [EpicsTemplater RenderIndex/RenderEpic/RenderStory/RenderStoryPlaceholder + AppendStoryCard/AppendEpicCard/AppendChipSection/AppendProgressPanel/AppendNextActionsPanel/AppendRetroAffordance](../../src/SpecScribe/EpicsTemplater.cs). Chart inputs: [Charts.cs](../../src/SpecScribe/Charts.cs) (`StatCard`, `Sunburst`, `Donut`, `ProgressBar`, `RefinementFunnel`, `GitPulsePanel`, `CoverageMeter`/`ArtifactCoveragePanel`, `EpicMosaic`). Status seam: [StatusStyles.cs](../../src/SpecScribe/StatusStyles.cs).
- Delivery seam to extend: [IRenderAdapter.cs](../../src/SpecScribe/IRenderAdapter.cs), [HtmlRenderAdapter.cs](../../src/SpecScribe/HtmlRenderAdapter.cs), [PageView.cs](../../src/SpecScribe/PageView.cs), [RenderParity.cs](../../src/SpecScribe/RenderParity.cs), [HostRenderException.cs](../../src/SpecScribe/HostRenderException.cs).
- Golden gate: [SiteGeneratorAdapterTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) (pinned golden inventory + content). Watch parity: [SiteGeneratorStoryEpicPagesTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs), [SiteGeneratorCoverageTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorCoverageTests.cs).
- Memory: [golden-diff-normalization-gotchas] (the 3 benign diffs to normalize), [generate-output-dir-is-specscribeoutput], [specscribe-status-token-system], [charting-is-pure-svg-no-js], [now-and-next-is-the-sprint-board] (the Now&Next panel's dual sprint-board behavior — a byte-load-bearing conditional to preserve), [epic-2-retro-scope-and-debt] (split-don't-absorb), [worktree-edits-must-target-worktree-path].

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

- 2026-07-10 — Story 6.2 drafted (create-story). **Scope split at create-story (owner-confirmed):** this story = epics.md Story 6.2 **AC #1 only** — the rendering-core decomposition of the dashboard + epics page bodies into shared, JSON-serializable section view models rendered byte-identically by the HTML adapter, with the parity harness extended to section facts. The VS Code webview runtime + live host-push (epics.md AC #2 + #3) were split into a follow-up story to be seated via correct-course; the eventual webview will consume a new JSON view-model export (owner-chosen data path), which sets this story's "section view models must be serializable data" design constraint (AC #2). Webview/extension code, the JSON export path itself, other page bodies, host theming, and any package split are explicitly out of scope.
