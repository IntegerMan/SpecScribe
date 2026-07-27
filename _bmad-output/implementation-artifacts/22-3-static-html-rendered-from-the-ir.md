---
baseline_commit: 32fd282
implements_decision: docs/adrs/0008-json-ir-canonical-and-incremental-generation.md # §Decision 2 (co-equal projections) — this story is what makes "co-equal" literally true
amends_decision: docs/adrs/0009-frontend-framework-for-projection-layer.md # Axis 1 Option B row + §Consequences say "retires C# HtmlRenderAdapter for content"; this story keeps it as a co-equal projection (AC #9)
gated_by: 22-2-canonical-ir-schema-and-versioning # the IR this story projects from
gates: [22-4, 22-5, 22-6] # 22.4 inherits the dead-symbol list; 22.5 inherits one region shape to invalidate
owner_decisions: 2026-07-27 # (1) FULL inversion — 25 templaters onto PageView, (2) both 23.3-handed defects in scope, (3) research subagents authorized
---

# Story 22.3: Static HTML Rendered from the IR

Status: retired

> **⛔ RETIRED 2026-07-27 — superseded by [Story 23.4](23-4-migrate-remaining-surfaces-retire-c-sharp-html-adapter.md) (owner decision D4, taken at create-story 23.4 in a concurrent session).**
>
> **DO NOT IMPLEMENT THIS STORY.** It was seeded and then retired the same day. 22.3 and 23.4 are competing
> answers to one question — *who renders static HTML from the IR*. 22.3 answers "a C# IR-projection path,
> byte-identical to golden"; 23.4 answers "the Vue/Nuxt projection layer writes every `.html` and the C# page
> render is retired." Both cannot hold, and **Nuxt-over-IR is the ratified direction**
> ([ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md)). Recorded in
> `epics.md` § Story 22.3 and `sprint-status.yaml` in the same change.
>
> **Why this file is kept.** The retirement note hands Story 22.4 a live obligation: *"23.4 AC #3 deliberately
> KEEPS one C# region-composition path (nav + wayfinding + `<main>`) feeding the IR and the webview/SPA — so
> 22.4's 'retire the duplicate, non-IR data paths' must be restated against that surviving path."* **That
> surviving path is exactly what this file characterizes.** The analysis below is retained as input to Story
> 22.4 and to 23.4's AC #3, not as a work order:
>
> - the 25-templater migration inventory and the axes on which they differ (Dev Notes § Migration inventory);
> - **the `NavLocalContext` blocker** — no `path → NavLocalContext` resolver exists, and any surviving C#
>   region-composition path either threads it or regresses the page-local nav band (Dev Notes § The one thing
>   that blocks everything else);
> - **eight traps**, one of which (the outside-`<main>` header on 10 templaters) is resolved with its
>   byte-safety argument;
> - the ADR constraint table and the ranked test-gate map, both of which bind 23.4 unchanged.
>
> **The two defects Story 23.3 handed back to Epic 22 are now owned by [Story 22.4](#) (owner decision
> 2026-07-27, recorded in `epics.md` § Story 22.4 and `sprint-status.yaml` in the same change).** They were in
> this story's scope by owner decision earlier the same day and do not disappear with its retirement:
> the **46-delta pipeline-ordering defect** — root-caused in code here at
> [`SiteGenerator.cs:326`](../../src/SpecScribe/SiteGenerator.cs) vs
> [`:339`](../../src/SpecScribe/SiteGenerator.cs) vs [`:3052`](../../src/SpecScribe/SiteGenerator.cs), where
> **the static page is the stale side** — and the **two-region-shapes defect**. AC #4 and AC #5 below are the
> specification 22.4 inherits for them; read them as requirements, not as this story's work.

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer relying on the JS-optional static HTML baseline,
I want every page's **chrome** lifted into the shared `PageView` contract, so static HTML and the canonical IR become two projections of **one view model** instead of the IR being sliced back out of already-rendered HTML,
So that ADR 0008 §Decision 2's "co-equal projections" is literally true, the last string-slicing seam in the pipeline retires, and NFR6's accessibility baseline is preserved by construction rather than by parity testing after the fact.

## Why this story looks different from epics.md — READ FIRST

epics.md's three ACs were written in 2026-07-21, before Stories 22.1, 22.2, 23.1, 23.2 and 23.3 ran. **This story's 9 ACs supersede them**; Task 12 records that drift in `epics.md` and `sprint-status.yaml` in the same change, per CLAUDE.md § Decision records.

### The AC as written is circular for 853 of 1,042 pages

epics.md AC #1 says: generate "via the IR-projection path" and compare to the direct `HtmlRenderAdapter` path. **That comparison cannot be run today**, because the IR is *made from* static HTML. Two different ways, measured in code:

| pages | how the IR gets its content today | consequence |
|---|---|---|
| **189** — `index.html`, `epics.html`, `epics/epic-{N}.html`, `epics/story-{id}.html` | **Re-rendered** from a `PageView` via `JsonSpaRenderAdapter.RenderContent` ([`SiteGenerator.cs:3060-3096`](../../src/SpecScribe/SiteGenerator.cs)) | Already co-equal. This half of ADR 0008 works. |
| **~853** — everything else | **Captured then sliced.** `SomeTemplater.RenderPage(…)` → full HTML string → `WriteOutput` ([`:3017`](../../src/SpecScribe/SiteGenerator.cs)) → `_spaCapture` → `SpaDelivery.ExtractContentRegion` cuts `<main>` back out ([`:3103-3116`](../../src/SpecScribe/SiteGenerator.cs)) | **Circular.** Static HTML cannot be rendered *from* an IR that was produced *by slicing static HTML.* |

There are exactly **5** `new PageView` sites in the codebase — [`HtmlTemplater.cs:185`](../../src/SpecScribe/HtmlTemplater.cs) and four in [`EpicsTemplater.cs`](../../src/SpecScribe/EpicsTemplater.cs). The other **25 page-producing templaters** call `PathUtil.RenderHeadOpen` directly and compose their own full document, hand-writing `</body></html>` at the end (there is no `RenderPageClose`).

### The thesis: this is finishing AD-2, not inventing an architecture

[ARCHITECTURE-SPINE.md § AD-2](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) binds *"page models, navigation graph, asset manifest, and render metadata"* to the host-neutral view model, and AD-1 says *"adapters only translate that core output into host delivery concerns."* **25 templaters composing their own `<head>`, nav, wayfinding and footer is a standing AD-1/AD-2 violation.** The IR's shortcomings are a *symptom* of it — ADR 0016 §Decision 1 requires the IR to carry "a head/meta projection" and "a declaration of embedded script islands", and those pages have no view model to carry them in.

ADR 0016 §Consequences already states this story's goal in its own words:

> "One rendering implementation, **one capture path**. Static HTML, the SPA, the webview, and Nuxt project from the same bytes rather than from three drifting captures."

### ⚠️ THE TRAP: "do not re-model into view models" does NOT apply to this story

[Story 22.2's Dev Notes § *Do not re-model into view models*](22-2-canonical-ir-schema-and-versioning.md) warns that building to ADR 0008 §Decision 1's literal wording would "discard the rendered prose, reviving the Markdig renderer-fidelity risk 23.1 measured away, and pull the ~4,691 LOC templater reimplementation into Epic 23's scope."

**That warning is about page *bodies*. This story touches only *chrome*.**

- `PageView.BodyHtml` stays an **opaque, already-rendered HTML string, carried verbatim**. No body is decomposed. No Markdig output is re-modelled. The custom-renderer fidelity risk is not reopened.
- What moves into the contract is what AD-2 already says belongs there: title, meta description, nav (**with page-local context** — see below), breadcrumb, pager, asset manifest, interaction state.
- [`PageView.cs`](../../src/SpecScribe/PageView.cs) calls `BodyHtml` *"the DEFERRED-decomposition seam"* and justifies the deferral as *"a byte-risky rewrite with no consumer."* **Both conditions have now expired** — ADR 0016 §Decision 1 defines the consumer, and Story 23.3's `measure:parity` harness makes the byte risk measurable rather than assumed. The deferral expiring is not the same as the deferral being cancelled: the *body* stays deferred.

A dev agent that reads 22.2 without this distinction will either refuse this story or over-build it. Confirmed intent, from 22.2's own scope guard: *"Not static-HTML-from-the-IR. That is 22.3."*

### The owner locked three decisions on 2026-07-27 (create-story elicitation)

| # | Decision | Consequence |
|---|---|---|
| **D1** | **FULL inversion.** Migrate every remaining page-producing templater onto `PageView`. Static HTML becomes `HtmlRenderAdapter.Render(pv)`; the IR region becomes `JsonSpaRenderAdapter.RenderContent(pv)`. | The largest of the four options considered. Chosen because **`PageView` is not retired by Story 23.4** — 23.4 retires the HTML *projection*, and a richer `PageView` is exactly what Epic 23's Nuxt layer is currently missing (see 23.3's *"Named gaps handed to Epic 22"*). This work is load-bearing for Epic 23, not thrown away by it. |
| **D2** | **Both defects 23.3 handed back are in scope.** (a) the 46-delta pipeline-ordering defect, (b) the two-region-shapes defect. | Both are literally "the IR and static HTML disagree", which is this story's subject. Fixing the inversion without them would leave the disagreement in place under a new mechanism. |
| **D3** | **Research subagents authorized** for this create-story run. | Used for the templater enumeration, the ADR constraint extraction, and the test-gate map recorded below. |

### Scope boundary this story must NOT cross

**Deleting the scrape helpers is Story 22.4's, per ADR 0016 §Decision 4** — *"retiring any now-duplicate data path, is Story 22.4's call."* This story makes them **unreachable for the IR** and **enumerates the symbols that become dead**; it does not delete them. See AC #3 and Task 4.

## Acceptance Criteria

1. **Every page-producing templater builds a `PageView`; both surfaces project from it.**
   **Given** a page-producing templater that today composes its own document via `PathUtil.RenderHeadOpen`,
   **When** it is migrated,
   **Then** it returns a `PageView` whose `BodyHtml` is the same opaque body string it renders today, and its static HTML is produced by `HtmlRenderAdapter.Render(pageView)`,
   **And** the page's IR content region is produced by `JsonSpaRenderAdapter.RenderContent(pageView)` — the same view model, never a slice of the rendered page,
   **And** the story's File List enumerates **every** migrated method,
   **And** any remaining direct `PathUtil.RenderHeadOpen` caller is **named with a justification** in the Completion Notes (the SPA entry shell and the webview shell are expected to remain; a *templater* remaining is a finding, not a default).

2. **`NavLocalContext` is carried by the view model, not recovered by slicing rendered HTML.**
   **Given** a page whose static render computes a page-local nav context band (`site-nav-local-context`),
   **When** its nav is rendered from `PageView.Nav`,
   **Then** the emitted band is **byte-identical** to the band the static page shows today, including its `aria-label`,
   **And** no code path recovers that band by slicing a rendered page.
   *(This is the load-bearing prerequisite — see Dev Notes § The one thing that blocks everything else.)*

3. **The IR no longer consumes the capture; the newly-dead symbols are enumerated and handed to 22.4.**
   **Given** the migration is complete,
   **When** the IR is emitted,
   **Then** no IR page's content region originates from `_spaCapture` or from any `SpaDelivery.Extract*` helper,
   **And** the Completion Notes enumerate every symbol that is now dead (expected: `ExtractContentRegion`, `ExtractTitle`, `ExtractBreadcrumb`, `ExtractNavMarkup`, `ExtractMetaDescription`, `CapturedNavMarkup`, and `_spaCapture` itself if it retains no other consumer), **handed to Story 22.4 for deletion per ADR 0016 §Decision 4** — this story does not delete them.

4. **One region shape, not two.**
   **Given** Story 23.3's finding that the IR carries two different region shapes — 187 re-rendered family pages carry the page-wayfinding wrapper, ~853 captured pages slice from *inside* it and are unbalanced by one element,
   **When** every region is produced by `JsonSpaRenderAdapter.RenderContent`,
   **Then** all pages carry **one** region shape,
   **And** a test asserts that shape holds across the whole emitted IR — element-balanced, exactly one `<main id="main-content">`, and the wayfinding band opening and closing on the same side of `<main>` on every page.

5. **The pipeline-ordering defect is fixed on the static side.**
   **Given** `RenderEpicsPages` runs at [`SiteGenerator.cs:326`](../../src/SpecScribe/SiteGenerator.cs) *before* the pages loop at [`:339`](../../src/SpecScribe/SiteGenerator.cs) fills `_docs`, while `BuildSpaBundle` reads `_docs.Values` at [`:3052`](../../src/SpecScribe/SiteGenerator.cs) *after* — so the epic/story follow-up inventories differ and **the static page is the stale side**,
   **When** a full generation runs,
   **Then** the static epic/story pages and the IR see the **same** work inventory,
   **And** the per-story work-graph node/edge counts agree between the two surfaces,
   **And** the fix is stated as an ordering fix (the static page was stale), not as a capture fix.

6. **Byte parity: the golden fingerprint does not move.**
   **Given** NFR4 (additive) and the fact that this story changes *how* HTML is composed, not *what* it says,
   **When** the full suite runs,
   **Then** `GoldenContentFingerprint` is **unchanged** at its pre-story value,
   **And** if it moves, that is treated as a **defect to be diagnosed, not a constant to re-bless** — with the one sanctioned exception of the AC #5 ordering fix, whose byte delta must be enumerated page-by-page and justified before any regeneration,
   **And** any regeneration follows CLAUDE.md § Verification: stable across **two repeated runs**, with the concurrent session's changes it sat on top of named in the story record.

7. **NFR6 holds, verified in a live browser with JavaScript disabled.**
   **Given** ADR 0013 §Decision 1 ("information and navigation are non-negotiable") and §Decision 3's requirement that this be *"verified in a live browser with JavaScript disabled — not by test assertion alone"*,
   **When** the regenerated static site is opened with scripts blocked,
   **Then** content renders and the site is fully navigable, identical in this respect to the pre-story baseline,
   **And** the verification names the pages checked and the mechanism used to block scripts.

8. **The `schemaVersion` question is answered explicitly, either way.**
   **Given** ADR 0016 §Decision 5 lists *"a change to how a content region is delimited"* as a `schemaVersion` bump trigger,
   **When** regions are produced by view-model composition instead of marker-slicing,
   **Then** the story records an explicit finding: **identical emitted bytes and identical region boundaries → no bump** (with the measurement that proves it), **any observable difference → bump to 2** (with the difference enumerated),
   **And** the finding is recorded in `SpaDelivery.SchemaVersion`'s doc comment, not only in the story file.

9. **The ADR is proposed.**
   **Given** ADR 0009's Axis-1 Option-B row and §Consequences both state *"retires C# `HtmlRenderAdapter` for content"*, while this story keeps it alive as a co-equal projection per ADR 0008 §Decision 2,
   **When** the story completes,
   **Then** a new ADR (**next free number: 0019**) is proposed that reconciles the two — narrowing 0009's retirement claim, and recording that `PageView` (not rendered HTML) is the single upstream both projections derive from,
   **And** it is cross-referenced from `docs/adrs/README.md`, ADR 0008, ADR 0009 and `epics.md`,
   **And** it notes that ADR 0006 §Accessibility-posture's *"the static HTML surface remains… the source of truth"* was already moved by ADR 0008 §Decision 2 and is finished by this story.

## Tasks / Subtasks

**Sequence matters.** Task 1 is a hard prerequisite. Task 2 settles the three structural design questions on three representative pages *before* the bulk migration, because getting them wrong costs a re-do of every batch after it.

- [ ] **Task 1 — Thread `NavLocalContext` into the view model (AC #2). DO THIS FIRST; nothing else is safe until it lands.**
  - [ ] Give `PageView.Nav` (or a sibling field) the page-local context so `HtmlRenderAdapter.RenderNavMarkup` → `AppendLocalContextBand` ([`HtmlRenderAdapter.cs:270`](../../src/SpecScribe/HtmlRenderAdapter.cs)) can emit the band from the view model.
  - [ ] Update the producers that build a `NavLocalContext` inline and discard it. **16 of the 25 templaters pass one** (17 call sites — Requirements passes a different context on its index and its detail pages): `BuildInsightsLocalContext` (CodeMap, DeepAnalytics, GitInsights, RiskQuadrant, WorkGraph), `BuildDeliveryLocalContext` (Cadence, ImpactMap, Requirements-index, Sprint, Traceability), `BuildSddLocalContext` (AboutSdd), `BuildRequirementLocalContext` (Requirements-detail), plus HtmlTemplater, CodeFile, CommitDay, CommitDetail, FollowUpDetail. **The other 9 pass none and must keep passing none**: About, ActionItems, DeferredWork, DesignSystem, Diagnostics, FollowUpGroup, HowToRead, Retro (both methods), Timeline.
  - [ ] Prove byte-identity of the emitted band against today's static output **before migrating a single templater**.
- [ ] **Task 2 — Settle the three structural questions on three representative pages (AC #1, AC #6).**
  - [ ] **`extraHead`.** `HtmlRenderAdapter.Render` ([`:30`](../../src/SpecScribe/HtmlRenderAdapter.cs)) calls `RenderHeadOpen` with **four** arguments and drops the fifth. Exactly one templater uses it — `CodeFileTemplater.HighlightHead` ([`CodeFileTemplater.cs:769`](../../src/SpecScribe/CodeFileTemplater.cs)), the Prism stylesheet + deferred highlighter, on `RenderPage` only (`RenderPlaceholder` passes `highlight:false`). Give `AssetManifest` a carrier — a `CodeHighlightNeeded` flag mirroring the existing `MermaidNeeded` / `HierarchyEngineNeeded` booleans is the shape that matches the file, and it keeps ADR 0012 §Addendum-5's conditional-emission rule expressible. **Prove it on `code/**` before migrating ~hundreds of pages.**
  - [ ] **Content after `</main>`.** `Render` emits body → TOC script → footer with no slot between. `DeepAnalyticsTemplater.cs:110-123` is the **only** templater emitting real content there: the pure-CSS `:target` lightbox `<div id="coupling-zoom" class="coupling-lightbox" role="dialog">` carrying a second copy of `Charts.CouplingGraph`, emitted only when `hasCoupling`. Decide its carrier and prove it on `deep-analytics.html`.
  - [ ] **The outside-`<main>` header — RESOLVED, but verify before scaling.** See Dev Notes § Trap 1: carry it inside `BodyHtml`. Prove byte-identity on `about.html` (static **and** IR region) before applying the pattern to the other nine.
- [ ] **Task 3 — Fix the pipeline-ordering defect (AC #5).**
  - [ ] Share one work inventory between `RenderEpicsPages` and `BuildSpaBundle` — either build it before [`SiteGenerator.cs:326`](../../src/SpecScribe/SiteGenerator.cs) or move `RenderEpicsPages` after `_docs` fills. **Diagnostics event ordering is load-bearing for the golden fingerprint** ([`:415-418`](../../src/SpecScribe/SiteGenerator.cs)) — preserve it whichever route you take.
  - [ ] Enumerate the resulting static-page byte delta page-by-page and justify it under AC #6's sanctioned exception.
- [ ] **Task 4 — Migrate the singletons (AC #1).** ~19 pages, one `PageView` each, lowest risk. Verify parity after the batch, not after each file.
  - [ ] Header **inside** `<main>` (straightforward): Timeline, GitInsights, DeepAnalytics, CodeMap, RiskQuadrant, WorkGraph, ImpactMap, Traceability, Cadence, Sprint.
  - [ ] Header **outside** `<main>` (apply Task 2's resolved pattern): About, ActionItems, DeferredWork, DesignSystem, Diagnostics, HowToRead, Requirements-index, Retro-index, AboutSdd hub.
  - [ ] ⚠️ `WorkGraphTemplater` ([`SiteGenerator.cs:3628`](../../src/SpecScribe/SiteGenerator.cs)), `ActionItemsTemplater` ([`:3674`](../../src/SpecScribe/SiteGenerator.cs)) and both `FollowUpDetailTemplater` sites are **deliberately not run through `ApplyReferenceLinks`**. Keep it that way.
- [ ] **Task 5 — Migrate the per-entity families (AC #1).**
  - [ ] `RequirementsTemplater.RenderRequirement` (N) — ⚠️ passes **no description** (3-arg `RenderHeadOpen`, [`:198`](../../src/SpecScribe/RequirementsTemplater.cs)), as does the index at [`:19`](../../src/SpecScribe/RequirementsTemplater.cs). `PageView.MetaDescription = null` reproduces the title-fallback exactly — do not "improve" it.
  - [ ] `RetroTemplater.RenderPage` (N) — carries a pager; its tail (TOC script → footer → mermaid) is the closest hand-built analogue of `Render`'s, so it is the best cross-check that the adapter's tail is faithful.
  - [ ] `FollowUpGroupTemplater` (N), `FollowUpDetailTemplater.RenderActionPage` (N) + `RenderDeferredPage` (M).
  - [ ] `CommitDayTemplater` (N, pager) — ⚠️ the only templater with a **computed** meta description (`BuildMetaDescription`, [`CommitDayTemplater.cs:36`](../../src/SpecScribe/CommitDayTemplater.cs)).
  - [ ] `CommitDetailTemplater` (N, pager).
  - [ ] `AboutSddTemplater.RenderFrameworkPage` (N) — ⚠️ dynamic breadcrumb depth (2 crumbs on the hub, 3 on a framework page), and the `doc-subtitle` reuses the same string passed to `RenderHeadOpen`.
- [ ] **Task 6 — Migrate the two high-volume templaters last (AC #1).** By now the pattern is proven; these are where a mistake is most expensive.
  - [ ] `HtmlTemplater.RenderPage` — 6 call sites, all `WriteOutput`: generic docs [`:1060`](../../src/SpecScribe/SiteGenerator.cs), ADR records [`:1130`](../../src/SpecScribe/SiteGenerator.cs) (the pager site), ADR landing [`:1206`](../../src/SpecScribe/SiteGenerator.cs), structure docs [`:3230`](../../src/SpecScribe/SiteGenerator.cs), readme [`:3897`](../../src/SpecScribe/SiteGenerator.cs), quick-dev docs [`:3963`](../../src/SpecScribe/SiteGenerator.cs).
  - [ ] `CodeFileTemplater.RenderPage` + `RenderPlaceholder` (~hundreds) via the shared `BeginShell`/`EndShell` — the `extraHead` carrier from Task 2 lands here.
- [ ] **Task 7 — Route the IR off the capture and enumerate the dead symbols (AC #3).**
  - [ ] `BuildSpaBundle`'s second loop ([`SiteGenerator.cs:3103-3116`](../../src/SpecScribe/SiteGenerator.cs)) consumes `PageView`s, not `_spaCapture`.
  - [ ] Enumerate every newly-dead symbol in the Completion Notes and hand it to 22.4. **Do not delete them** (ADR 0016 §Decision 4).
- [ ] **Task 8 — Collapse the two region shapes and pin the invariant (AC #4).**
- [ ] **Task 9 — Answer the `schemaVersion` question; record it in `SpaDelivery.SchemaVersion`'s doc comment (AC #8).**
- [ ] **Task 10 — Replace the seven scrape-helper tests** (Dev Notes § Test gates, row 4). Replace, do not delete.
- [ ] **Task 11 — Verify (AC #6).**
  - [ ] `dotnet test SpecScribe.slnx` — golden fingerprint unchanged; registry ceiling holds (no new `HostRenderException`, never a `section.*` one).
  - [ ] `npm run check:links` + `npm run measure:parity` under `web/` — ADR 0017 §Decision 2 makes the emitter the only legal place a link may change, so a 25-templater reshape is exactly the change class these exist to catch.
  - [ ] `npm run check:ir-content` — ADR 0018's extraction is class/id-bound.
  - [ ] Verify the git-derived surfaces separately: `git-insights.html`, `impact-map.html`, `timeline.html`, `commits/` are **absent from the golden fixture**, so a green fingerprint says nothing about them.
- [ ] **Task 12 — Live-browser JS-off verification (AC #7).** ADR 0013 §Decision 3 requires a real browser, not a test assertion. Measure real DOM geometry — that is the only thing that caught 23.3's nested-`<main>` defect.
- [ ] **Task 13 — Propose ADR 0019 and cross-reference it from `docs/adrs/README.md`, ADR 0008, ADR 0009 and `epics.md` (AC #9).**
- [ ] **Task 14 — Record the AC drift in `epics.md` AND `sprint-status.yaml` in the same change** (CLAUDE.md § Decision records).

## Dev Notes

### The one thing that blocks everything else: there is no `path → NavLocalContext` resolver

This is the single hardest technical problem in the story, and it is why Story 22.2 declined to solve it.

Today, a captured page keeps its page-local nav band because [`SpaDelivery.ExtractNavMarkup`](../../src/SpecScribe/SpaDelivery.cs) **slices the band out of the rendered page**. Its own doc comment explains why:

> "There is no path → `NavLocalContext` resolver to re-derive it from: every producer (ADRs, commit days, commits, insights, delivery, SDD, epics, requirements) builds one inline at render time and **discards it**, so threading it here would mean ~8 call sites of plumbing for a value the captured string already holds verbatim."

22.2 was right to decline — for 22.2. **This story cannot decline it.** The moment the region comes from `RenderNavMarkup(page.Nav)` instead of from a slice, `nav.ToNavigationView(path)` takes no local-context argument and every migrated page silently loses its band — regressing Story 23.1's enumerated difference #2, which 22.2 just fixed. Two tests fail immediately and byte-for-byte:

- `SiteGeneratorSpaTests.cs:387` — `CapturedPage_KeepsItsOwnLocalContextNavBand_AndLeavesOthersUnchanged`
- `SiteGeneratorWebviewTests.cs:547` — `CapturedSurface_KeepsThePagesOwnLocalContextNavBand`

**Do Task 1 before touching a single templater.** ~8 call sites of plumbing is the actual cost of this story's correctness, and it is cheaper to pay it up front than to discover it 20 templaters in.

### Architecture invariants that bound this work

| Invariant | What it forbids here |
|---|---|
| **AD-1 / AD-2** | Adapters translate; they do not reinterpret. Marker-slicing rendered HTML is the last reinterpretation step in the pipeline — this story removes it, moving the boundary *further* from the prohibited side, never toward it. |
| **ADR 0008 §Decision 1** | The C# core is the IR's **single producer**. A `PageView`-as-single-upstream design keeps that intact. |
| **ADR 0008 §Decision 2** | Static HTML, SPA and webview are **co-equal projections**. Today static HTML is produced first and the IR sliced out of it; this story is what makes "co-equal" literally true. |
| **ADR 0005 §Decision 4** | `?v=` cache-bust tokens appear **in the head only, never in the rendered body**. The head projection must preserve that. (The golden gate normalizes `?v=` away, so it will not catch a violation — a test must.) |
| **ADR 0012 §Addendum, correction 5** | Asset emission stays **conditional**: `prism.js` copies only when in-portal code pages exist, "so a site with no code pages stays byte-identical". `AssetManifest` is where that condition lives — do not make it unconditional while lifting it into the view model. |
| **ADR 0013 §Decision 2** | The text twin is **server-rendered, never injected by script**. It must be in the `BodyHtml` the view model carries. |
| **ADR 0016 §Decision 2** | AD-2's view models are **not retired** — they are "the thing every adapter renders *from*". This story leans on that, it does not contradict it. |
| **ADR 0016 §Decision 4** | No second capture path; retiring a now-duplicate data path is **22.4's call**. Enumerate the dead symbols; do not delete them. |
| **ADR 0017 §Decision 2** | **No href inside IR content is ever rewritten.** If a link does not resolve, the fix belongs in the emitter — which this story is now touching. 499 links dangle on the golden site today; **faithfully reproduce them, do not patch them here.** |
| **ADR 0018** | The `ir-content.css` extraction is **class/id-bound**. A changed class or id in an emitted region turns `check:ir-content` red until re-extracted. |
| **NFR4 (additive)** | The static site's bytes must not change, except for the AC #5 ordering fix. |
| **NFR6 / ADR 0013 §1** | Information and navigation must survive with JS off. Non-negotiable. |

### Existing machinery — extend it, do not reinvent

Almost everything this story needs already exists. The work is **routing**, not building.

| Need | Already exists | Where |
|---|---|---|
| Compose head + nav + wayfinding + body + footer + scripts from a view model | `HtmlRenderAdapter.Render` | [`HtmlRenderAdapter.cs:27`](../../src/SpecScribe/HtmlRenderAdapter.cs) — the 10-step assembly order is the contract to match |
| Compose the IR content region from the same view model | `JsonSpaRenderAdapter.RenderContent` | [`JsonSpaRenderAdapter.cs`](../../src/SpecScribe/JsonSpaRenderAdapter.cs) — nav + wayfinding + body, no scripts |
| Breadcrumb **or** breadcrumb+pager, byte-identically | `HtmlRenderAdapter.RenderWayfinding` | [`:424`](../../src/SpecScribe/HtmlRenderAdapter.cs) — **byte-identical to `RenderBreadcrumb` when the pager renders empty**, so both templater idioms collapse onto one call |
| The page-local nav context band | `AppendLocalContextBand` | [`HtmlRenderAdapter.cs:270`](../../src/SpecScribe/HtmlRenderAdapter.cs) — already written; it just has no view-model input yet (Task 1) |
| Conditional per-page asset opt-in | `AssetManifest.MermaidNeeded` / `HierarchyEngineNeeded` | The shape `CodeHighlightNeeded` should copy (Task 2) |
| Head projection fields | `PageView.Title` / `MetaDescription`; `ManifestHead` | `MetaDescription = null` reproduces `RenderHeadOpen`'s title fallback exactly |
| Script-island classification | `SpaDelivery.ExtractScriptIslands` | Operates on the region regardless of what produced it — **survives the migration unchanged** |
| Chunking, byte budget, oversized-page declaration, content hash | `SpaDelivery.BuildDataFiles` | All survive; they consume regions, not captures |
| Sanctioned per-surface divergence | `HostRenderExceptions.Registry` | **Do not add to it** — four hygiene tests cap it (see Test gates row 5) |

### Scope guard — seven things this story is NOT

1. **Not the incremental engine.** `RegenerateEpics`' watch-mode divergence is 22.5's. Fixing the *full-generation* ordering defect (AC #5) is in scope; changing watch-route invalidation is not.
2. **Not a delta channel.** 22.6's.
3. **Not the deletion of the scrape helpers.** ADR 0016 §Decision 4 assigns that to 22.4. Enumerate them dead; leave them compiling.
4. **Not body decomposition.** `BodyHtml` stays opaque. No section view models, no Markdig re-modelling. See § THE TRAP above.
5. **Not retiring `HtmlRenderAdapter`.** That is 23.4, and it is gated behind 23.5. This story makes the adapter *more* central, and AC #9 owes the ADR that reconciles the two.
6. **Not a link-graph cleanup.** 499 links (216 distinct targets) dangle on the golden site today — nested anchors from a link rewriter running twice, and links to source files the portal never rewrites. ADR 0017 §Decision 2: **reproduce them faithfully.** Fixing them here would make this story's parity measurement unreadable.
7. **Not a `--spa` behaviour change.** The SPA stays opt-in; a default generation must gain no cost.

### Test gates, ranked by how likely this refactor trips them

1. **Page-local nav band, byte-for-byte** — `SiteGeneratorSpaTests.cs:387`, `SiteGeneratorWebviewTests.cs:547`. See above. This is #1 for a reason.
2. **`<main>` byte-identity between static and IR/webview** — `SiteGeneratorSpaTests.cs:374` (`DashboardIrRegion_CarriesTheSameMainBlock_AsTheStaticPage`, written to catch exactly the 23.1 named-arg defect), `SiteGeneratorSpaTests.cs:528` (long tail: `about.html`, `requirements/fr1.html`, `diagnostics.html`), `SiteGeneratorWebviewTests.cs:516`.
3. **Golden fingerprint** — `SiteGeneratorAdapterTests.cs:237`, constant at `SiteGeneratorAdapterTests.cs:1107`. ⚠️ **Current value is `7adbdb016cf9bb7d6be3193ee27cad8f7888066d2d0f407eee98ea95f74b0c42`** — *not* the `2050b586…` recorded in Story 23.2, and *not* the `91c3aeb4…` recorded in Story 22.2. It has moved twice since; read it from the file, never from a story record. `NormalizeVolatile` (`:1245`) strips CRLF, today's date, the fixture root, the footer clock, `?v=`, the subtitle version, and the Version/Build rows — everything else is load-bearing. The ~850-line comment block above the constant is the regeneration audit trail; follow its ritual.
4. **The seven scrape-helper tests that must be REPLACED, not deleted** — all in `SpaDeliveryTests.cs`:

   | line | test | replacement obligation |
   |---|---|---|
   | 17 | `ExtractContentRegion_IgnoresAnEarlierLiteralClosingTag_…` | Hazard disappears with the slicer; retire with a note |
   | 36 | `ExtractContentRegion_DegradesToNavOnly_WhenNoLandmarkIsPresent` | **Needs an equivalent degrade path** for a `PageView` with no body — the `ReferenceEquals` degrade signal at `SiteGenerator.cs:2913` depends on the current one-instance contract |
   | 43, 60 | `ExtractBreadcrumb_*` | `PageView.Breadcrumb` supplies this structurally — assert that instead |
   | 340, 364 | `ExtractNavMarkup_*` | **The hard one.** Assert the band comes from the view model and still carries `site-nav-local-context` + `aria-label="ADRs"` and no `<script>` |
   | 370 | `ExtractMetaDescription_*` | `PageView.MetaDescription` already exists — assert the projection uses it |

   `ExtractTitle` has **no direct unit test** — an inherited coverage gap. Do not let it vanish silently.
5. **The `HostRenderExceptions.Registry` ceiling.** Four hygiene tests cap it: `WebviewRenderAdapterTests.cs:403` (exactly 4 webview entries), `RenderSpaParityTests.cs:197` (exactly 1 spa entry), `RenderParityTests.cs:207` (**zero** `html` entries), `RenderSectionParityTests.cs:303` (**never** a `section.*` entry). **This refactor may not add a single new exception.** If it needs one, that is a design signal, not a paperwork step.
6. **Determinism and manifest/region agreement** — `SiteGeneratorSpaTests.cs:471` (manifest + chunks byte-identical across two consecutive runs; names the page whose `contentHash` moved), `CanonicalIrSerializationTests.cs:154`. **Every page's `contentHash` moves if its region bytes shift by one character** — which is the measurement AC #8 needs.
7. **`CanonicalIrSerializationTests.cs:115`** re-declares the manifest shape independently. Any new IR field fails it loudly until the mirror record is updated — that is deliberate. Its doc comment currently reads *"Enumerated and justified differences: none"*; if you need one, it goes there.

**Flake discipline.** A red `SiteGenerator*` generate-to-disk test should be re-run **in isolation** before being called a regression — it is the documented rotating file-write-contention family (named members: `FileWatcherServiceTests.BurstOfSaves`, `SiteGeneratorTimelineTests` ×3, `SiteGeneratorCodeMapTests` determinism, `SiteGeneratorGitInsightsTests` hub, `SiteGeneratorReadmeTests`, `SiteGeneratorImpactMapTests`, `SiteGeneratorGroupedNavTests`). A red `RenderParity*` / `SpaDelivery*` / `CanonicalIrSerialization*` / `GoldenContentFingerprint` is **not** in that family and must be treated as real.

### Trap 1 — the outside-`<main>` header. RESOLVED at create-story; do not re-derive it under time pressure.

**10 templaters emit `<header class="doc-header">` OUTSIDE `<main>`**: About, AboutSdd, ActionItems, DeferredWork, DesignSystem, Diagnostics, FollowUpGroup, HowToRead, Requirements-index, Retro-index. All 5 existing `PageView` sites put the header *inside*, so `BodyHtml` starting at `<main>` looks like a convention the migration must honour. **It is not a rule, and honouring it would move bytes on 10 pages.**

The resolution — and the reason it is byte-safe on **both** surfaces:

- Emit order on those pages is `head → nav → breadcrumb → header → main`.
- `ExtractContentRegion` slices **from the breadcrumb** to `</main>`, so the header is **already inside today's IR region**.
- `JsonSpaRenderAdapter.RenderContent` = `nav + wayfinding + BodyHtml`. Putting the header at the front of `BodyHtml` yields `nav + breadcrumb + header + main` — **identical to both the static page and the current IR region.**

So: **`BodyHtml` may begin before `<main>`.** No new `PageView` slot is needed. Two consequences to carry: AC #4's one-region-shape assertion must not assume `BodyHtml` opens with `<main>`, and `PageView.cs`'s doc comment (which says the body is "the `<main>…</main>` body from today's templaters") needs updating to match reality.

### Trap 2 — `DeferredWorkTemplater` will double-prefix its Home crumb

[`DeferredWorkTemplater.cs:27-31`](../../src/SpecScribe/DeferredWorkTemplater.cs) passes its Home crumb href as **`prefix + "index.html"`**. Every other templater passes the bare output-relative `"index.html"` and lets `HtmlRenderAdapter.RenderBreadcrumb` apply the prefix internally. Migrating it verbatim produces a **double-prefixed, broken link**. Strip the prefix at the call site; ADR 0017 §Decision 2 makes the emitter the only legal place to fix a link, and this is the emitter.

### Trap 3 — two templaters call `RenderFooter()` with no prefix while computing one

[`FollowUpGroupTemplater.cs:71`](../../src/SpecScribe/FollowUpGroupTemplater.cs) and [`AboutSddTemplater.cs:270`](../../src/SpecScribe/AboutSddTemplater.cs) both compute a `prefix` and then call `PathUtil.RenderFooter()` with **no argument**. `HtmlRenderAdapter.Render` ([`:51`](../../src/SpecScribe/HtmlRenderAdapter.cs)) *always* derives the prefix from `page.OutputRelativePath` — so migrating these **fixes a latent bug and moves bytes**, unless both pages genuinely sit at the output root. Determine which, and if bytes move, enumerate it under AC #6's sanctioned exception rather than re-blessing the fingerprint silently.

### Trap 4 — the adapter detects the TOC by string-sniffing; the templaters branch on a count

`HtmlRenderAdapter.Render` ([`:47`](../../src/SpecScribe/HtmlRenderAdapter.cs)) emits `Toc.ActiveSectionScript` when the body *contains the literal string* `<nav class="toc-sidebar" aria-label="On this page">`. `HtmlTemplater` ([`:84`](../../src/SpecScribe/HtmlTemplater.cs)) and `RetroTemplater` ([`:133`](../../src/SpecScribe/RetroTemplater.cs)) branch on `tocEntries.Count > 0` / `toc.Count > 0`. **These two predicates can disagree** — a page with entries whose sidebar markup differs, or markup present with an empty list. Confirm they agree on every migrated page rather than assuming the sniff is equivalent.

### Trap 5 — `HtmlTemplater.cs:82` exploits the slicer's truncation point

The section-nav script is appended *after* `</main>` **precisely because `ExtractContentRegion` truncates there**. Removing the slicer removes that constraint — and any code written to exploit it becomes load-bearing on a rule that no longer exists. Check it deliberately rather than inheriting it.

### Trap 6 — `ExtractContentRegion`'s degrade path is reference-equality

Its no-landmark path returns the nav-markup **instance**, and [`SiteGenerator.cs:2913`](../../src/SpecScribe/SiteGenerator.cs) detects the degrade with `ReferenceEquals`. Any replacement must preserve a one-instance-per-page contract or the webview's degrade detection silently stops working — and silently is the operative word: no test would fail.

### Trap 7 — the epics family does not go through `WriteOutput`

The four epics-family writes use raw `File.WriteAllText` ([`SiteGenerator.cs:2571`](../../src/SpecScribe/SiteGenerator.cs) epics.html, [`:2586`](../../src/SpecScribe/SiteGenerator.cs) per-epic, [`:2598`](../../src/SpecScribe/SiteGenerator.cs) placeholder, [`:2605`](../../src/SpecScribe/SiteGenerator.cs) story). Only the dashboard rides the normal path ([`:3258`](../../src/SpecScribe/SiteGenerator.cs)). Any reasoning of the form "every page flows through `WriteOutput`" is false.

### Trap 8 — two moving targets under a concurrent session

- `SiteGeneratorSpaTests.cs:209` asserts the hierarchy engine bundle ships **only** on `index.html` (`Assert.Equal(new[]{"index.html"}, pagesWithTag)`). Stories 20.7/20.9 are mid-flight mounting hierarchy charts on more surfaces — expect this to move under you, and confirm whose change moved it before touching it.
- **The golden fixture does not cover git-derived surfaces.** `git-insights.html`, `impact-map.html`, `timeline.html` and `commits/` are absent (see the honest-scope-limit comment at `HierarchyExplorerTests.cs:615-633`). A green fingerprint is **not** evidence those pages survived.

### Migration inventory — what varies across the 25 templaters

Everything below must be reproduced byte-for-byte. These are the axes on which the templaters actually differ:

| axis | the variation |
|---|---|
| **`extraHead`** | Used **exactly once**: `CodeFileTemplater.HighlightHead` (Prism). `Render` currently drops the parameter. |
| **Asset href prefixing** | Root-only pages pass bare `ForgeOptions.StylesheetName`/`ScriptName` (About, AboutSdd, ActionItems, DesignSystem, Diagnostics, HowToRead, Retro-index, HtmlTemplater's index); everything else passes `prefix + …`. `PageView.Assets` already models already-prefixed hrefs. |
| **Meta description** | Most interpolate a per-page sentence. **Requirements (both methods) pass none** → title fallback. **CommitDay computes one** via `BuildMetaDescription`. **DesignSystem's is a plain literal**, the only non-interpolated one. |
| **`<main>` class** | 12 distinct values that must survive: *(none)*, `dashboard`, `info-page`, `deep-page`, `deep-page git-insights`, `commit-detail`, `followup-detail`, `req-index`, `req-detail`, `sprint-page`, plus ActionItems/FollowUpGroup which wrap an inner `<section>` inside the landmark. |
| **Pager** | Rendered by **6** un-migrated methods, all via `SiteNav.RenderWayfinding`: `HtmlTemplater.RenderPage`, `CodeFileTemplater.RenderPage`/`RenderPlaceholder`, `CommitDayTemplater`, `CommitDetailTemplater`, `RetroTemplater.RenderPage`. **All others call `SiteNav.RenderBreadcrumb`, which is byte-identical to `RenderWayfinding` with a null/empty pager** — so those migrate cleanly through the adapter's single wayfinding path. |
| **Inline `<script>` in the body** | Exactly one among the un-migrated: the JSON island `<script type="application/json" id="impact-map-data">` at [`ImpactMapTemplater.cs:151`](../../src/SpecScribe/ImpactMapTemplater.cs), **inside** `<main>`. Inert, and `SpaDelivery.ExtractScriptIslands` already classifies it `data`. |
| **Content after `</main>`** | Exactly one: DeepAnalytics' `#coupling-zoom` lightbox. See Task 2. |
| **Reference-linkification** | `WorkGraphTemplater`, `ActionItemsTemplater` and both `FollowUpDetailTemplater` sites are deliberately **not** run through `ApplyReferenceLinks`. |

**Volume ranking, for batching:** `CodeFileTemplater` (~hundreds) ≫ `HtmlTemplater.RenderPage` (docs + ADRs + structure + readme + quick-dev, N) > commits (N) > commit days (N) > requirements / follow-up details / retros / follow-up groups / SDD frameworks (N each) > ~19 singletons. **Migrate in reverse order of volume** — Tasks 4 → 5 → 6.

### Previous-story intelligence

**From 22.2 (review, 2026-07-26):**
- 22.2 fixed both defects 23.3 had predicted were unavoidable (the 5-anchor `codeItemHref` drift and the page-local nav band), which is *why* 23.3 reached dashboard byte parity. Do not re-fix them.
- 22.2's `BootScript` finding is still **latent and still relevant**: `HierarchyExplorer.BootScript` is emitted *between* the breadcrumb and `<main>`, and `ExtractContentRegion` slices from the breadcrumb — so any captured page gaining `HierarchyEngineNeeded` ships an executable script into both consumers. **A `PageView` route changes where that boundary is.** Re-derive the answer; do not assume 22.2's still holds.
- 22.2 deliberately normalized nothing about hash volatility. `diagnostics.html` echoes the configured output root inside its own region, so it is **the one page whose `contentHash` is output-path dependent** and differs machine-to-machine on identical input. Expect it; do not "fix" it.

**From 23.3 (review, 2026-07-27) — the story that handed this one its work:**
- 189/189 migrated surfaces reached byte-identical `<main>`; 0 link regressions across 89,280 internal links; 0 a11y failures across 1,051 pages. That is the bar this refactor must not lower.
- **The defect worth internalizing:** a double-opened wayfinding wrapper nested `<main>` and `<footer>` inside the breadcrumb band on all 187 migrated pages — and **passed parity, link resolution, and every a11y assertion**, because the wrapper sits *outside* `<main>`. It was found only by measuring real DOM geometry in a live browser. This story reshapes chrome on ~1,042 pages; the same class of defect is the most likely thing to ship undetected.

### Git intelligence

- Baseline `32fd282` ("Overnight work"). ⚠️ At create-story time a **concurrent session has `Charts.cs` and `HierarchyExplorer.cs` modified in the working tree** — Epic 20 work (20.7 deletes the legacy arc renderers; 20.9 is backlog). Those files feed the dashboard and code-map bodies this story's templaters carry. Expect them to move; grep-verify after every edit per CLAUDE.md § Concurrent work.
- Commits routinely bundle several stories (`261b300` carried 20.5, 20.7, 22.2, 23.2). **Scope any later review by this story's File List and symbols, never by a commit range.**
- CI is live (`build-test-analyze`, Story 25.1) on Windows and Ubuntu. `web/**` is absent from the Sonar exclusion list — an open item Story 23.5 owns; do not let this story be the one that trips it.

### Project Structure Notes

- Production code: `src/SpecScribe/` (single project, .NET 10). Tests: `tests/SpecScribe.Tests/` (flat, xUnit, ~2,112 test methods). `SpecScribe.slnx` has exactly two projects — nothing new joins it.
- No new NuGet dependencies. This is an internal composition refactor; the package set (`Markdig`, `Spectre.Console`, `Spectre.Console.Cli`, `YamlDotNet`) is unchanged, so no external version research applies.
- ADRs live in `docs/adrs/` with a `README.md` index. **Next free number is 0019** (0016 is 22.2's, 0017 and 0018 are 23.3's — all three are Proposed).
- Generate to `SpecScribeOutput/` (the default). Never `--output docs/live`.
- The SPA stays opt-in via `--spa` → `ForgeOptions.EmitSpa`. Nothing in this story should add cost to a default generation.

### References

- [epics.md § Story 22.3](../planning-artifacts/epics.md) — the three original ACs, superseded here; see Task 11.
- [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1, AD-2, AD-5, AD-8.
- [ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) — §Decision 1 (single producer), §Decision 2 (co-equal projections — this story's mandate).
- [ADR 0016](../../docs/adrs/0016-ir-carries-rendered-prose-html.md) — §Decision 2 (view models not retired), §Decision 4 (no second capture path; retirement is 22.4's), §Decision 5 (`schemaVersion` bump triggers), §Consequences ("one capture path").
- [ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) — Axis 1 Option B row + §Consequences, the clause AC #9 reconciles.
- [ADR 0017](../../docs/adrs/0017-projection-routes-mirror-ir-paths.md) — §Decision 2 (no href is ever rewritten; the emitter is the only legal place to fix a link).
- [ADR 0018](../../docs/adrs/0018-transitional-ir-content-style-layer.md) — the class/id-bound extraction gate.
- [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) — §Decision 4 (`?v=` in the head only; body carries no scripts, as amended by ADR 0012 §5).
- [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) — §1 (NFR-5 amended), §2 (twin is server-rendered contract), §3 (live JS-off browser gate), §6 (fingerprint replacement).
- [Story 22.2](22-2-canonical-ir-schema-and-versioning.md) — § *Do not re-model into view models* (the trap), § *Scope guard* ("Not static-HTML-from-the-IR. That is 22.3.").
- [Story 23.3](23-3-migrate-baseline-surfaces-dashboard-epics.md) — AC #5 § *Named gaps handed to Epic 22*, and the 46-delta root cause with the "IR is the more complete side" finding.
- [22-1-spike-report.md](22-1-spike-report.md) — the latency tolerance AC referenced by epics.md AC #2.
- [CLAUDE.md](../../CLAUDE.md) — § Concurrent work on shared `main`, § Decision records, § Verification.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Change Log

| Date | Note |
|---|---|
| 2026-07-27 | **RETIRED before reaching `ready-for-dev`** — superseded by Story 23.4 on owner decision D4, taken at create-story 23.4 in a **concurrent session** while this story was being written. The conflict is the one this story's AC #9 had identified and proposed to reconcile with a new ADR: ADR 0009's *"retires C# `HtmlRenderAdapter` for content"* versus ADR 0008 §Decision 2's *"co-equal projections."* The owner resolved it in 23.4's favour instead — Nuxt-over-IR is the ratified direction, so a C# IR-projection path is not built. `sprint-status.yaml` and `epics.md` § Story 22.3 both carry the retirement. **This file was kept, not deleted**, because 23.4 AC #3 preserves one C# region-composition path and Story 22.4 must be restated against it — see the banner at the top for what remains load-bearing. **Two defects are left unowned by this retirement** (46-delta pipeline ordering; two region shapes) and need re-homing. |
| 2026-07-27 | Story created (baseline `32fd282`). Its **9 ACs supersede epics.md's 3**; Task 11 records that drift. **Owner locked a FULL inversion** — migrate every page-producing templater onto `PageView` so static HTML and the IR become co-equal projections of one view model, rather than the IR being sliced out of rendered HTML. Root cause measured in code: only **5** `new PageView` sites exist, so **189** pages are already co-equal while **~853** are captured-then-sliced, making epics.md AC #1 circular for 82% of the site. Both defects Story 23.3 handed back are in scope (46-delta pipeline ordering, two region shapes). **The load-bearing prerequisite is that no `path → NavLocalContext` resolver exists** — 22.2 declined that plumbing because slicing already had the band verbatim; this story cannot. Scope boundary held per ADR 0016 §Decision 4: the scrape helpers become unreachable and are **enumerated**, but deleting them stays 22.4's. ADR 0019 owed to reconcile ADR 0009's "retires `HtmlRenderAdapter` for content" with ADR 0008 §Decision 2's "co-equal projections". |
