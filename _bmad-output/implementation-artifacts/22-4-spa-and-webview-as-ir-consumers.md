---
baseline_commit: 6017c2c
implements_decision: docs/adrs/0016-ir-carries-rendered-prose-html.md # §Decision 4 — "retiring any now-duplicate data path is Story 22.4's call"; this story exercises that grant
amends_decision: docs/adrs/0008-json-ir-canonical-and-incremental-generation.md # §Decision 2 "co-equal projections" — the webview stops being a PARALLEL builder and becomes a filtered projection of the same bundle
gated_by: 22-2-canonical-ir-schema-and-versioning # the IR (`spa/`) this story unifies onto
gates: [23-4, 22-5] # 23.4 AC #3 inherits ONE region producer to preserve; 22.5 inherits one region shape to invalidate
inherits_from: [22-3-static-html-rendered-from-the-ir, 23-3-migrate-baseline-surfaces-dashboard-epics] # the two defects 23.3 handed back, re-homed here when 22.3 was retired
owner_decisions: 2026-07-27 # D1 one region seam + both defects; D2 22.4 runs BEFORE 23.4; D3 the STATIC page moves to converge the 46-delta
---

# Story 22.4: SPA + Webview as IR Consumers

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer of the SPA and VS Code webview surfaces,
I want both surfaces produced by **one** region seam over the canonical IR instead of two near-identical builders,
So that Story 6.7's SPA path and the webview stay consistent with each other by construction, the two defects Story 23.3 handed back to Epic 22 are closed at the emitter, and Story 23.4 inherits **one** region producer to preserve rather than two to reconcile.

## Why this story looks different from epics.md — READ FIRST

epics.md's three ACs were written 2026-07-21, before Stories 22.1, 22.2, 23.1, 23.2, 23.3 and 23.5 ran, and before Story 22.3 was retired. **This story's 9 ACs supersede them**; Task 10 records that drift in `epics.md` and `sprint-status.yaml` in the same change, per CLAUDE.md § Decision records.

### epics.md AC #1 is already satisfied and near-vacuous

> *"Given the Story 6.7 SPA adapter's current whole-site consolidation, when it is migrated to consume the IR…"*

**`spa/manifest.json` + `spa/pages-*.json` ARE the IR.** ADR 0008 seated that file set as the canonical intermediate representation and Story 22.2 promoted it **in place** — no `ir/` directory, no rename ([`SpaDelivery.SchemaVersion`](../../src/SpecScribe/SpaDelivery.cs) doc comment: *"there is no second directory and no second capture path"*). The SPA client already consumes the IR; there is nothing to migrate. AC #1 below restates the real obligation: the SPA's **producer** stops being one of two.

### The real duplication, measured in code

| | SPA | webview |
|---|---|---|
| builder | `BuildSpaBundle` ([SiteGenerator.cs:3101](../../src/SpecScribe/SiteGenerator.cs)) | `RenderWebviewSurfaces` ([:2810](../../src/SpecScribe/SiteGenerator.cs)) |
| shared prelude | `_docs.Values` → `WorkInventory.Build` → `ProjectCounts.Build` → `BuildFollowUpGeometry` → `UnplannedWorkGeometry.From` → `HtmlTemplater.BuildIndexPage(… CodeItemHref …)` | **the same five calls, same arguments** ([:2829-2843](../../src/SpecScribe/SiteGenerator.cs)) |
| epics family | `EpicsTemplater.BuildIndexPage` / `BuildEpicPage` / `BuildStoryPage` / `BuildStoryPlaceholderPage` | **the same four**, same retro map, same placeholder rule, same `BuildStoryPageFragments` |
| captured pages | `CapturedNavMarkup` + `SpaDelivery.ExtractContentRegion` ([:3166](../../src/SpecScribe/SiteGenerator.cs)) | **the same two calls** ([:2961](../../src/SpecScribe/SiteGenerator.cs)) |
| region composer | `JsonSpaRenderAdapter.RenderContent` | `WebviewRenderAdapter.RenderContent` — *identical* except a JSON-island strip |

Two ~200-line builders that must be edited in lockstep forever. **That is the "duplicate, non-IR data path" AC #3 names.** Story 22.2's fix for the 5-anchor `codeItemHref` drift had to be applied **twice**, in both files, and both carry near-identical apology comments saying so ([:2835-2839](../../src/SpecScribe/SiteGenerator.cs), [:3115-3117](../../src/SpecScribe/SiteGenerator.cs)) — that is the duplication charging rent already.

### The owner locked three decisions on 2026-07-27 (create-story elicitation)

| # | Decision | Consequence |
|---|---|---|
| **D1** | **One region seam + both inherited defects.** Collapse the two builders onto one shared prelude and one shared captured-region loop; the webview becomes a **filtered projection** of the same bundle the IR is built from. | The slicers **survive** — they are still the producer for ~853 pages — but exist in exactly **one** place. This story retires the *duplicate*, not the *slice*. |
| **D2** | **22.4 runs BEFORE 23.4.** | 23.4 AC #3's "a region-composition path survives" becomes "the one 22.4 unified", and its *"delete the page render first and the IR goes dark for 82 % of the site"* circularity is answered before it starts. This story must therefore **not** depend on any unshipped 23.4 work. |
| **D3** | **The STATIC page moves** to converge the 46-delta, not the IR. | Honours 23.3's measurement that the IR is the more complete render. `GoldenContentFingerprint` **may move** — enumerated page-by-page, never re-blessed. |

### The premise the 22.3 retirement restated — read before deleting anything

Story 23.4 AC #3 **deliberately keeps one C# region-composition path** (nav + wayfinding + `<main>`) feeding the IR *and* the webview/SPA, because that path is what the IR is built from. AC #3 below is scoped **against** that surviving path: this story unifies it to one implementation and deletes what is genuinely duplicate. **It does not delete the region path, the slicers, `_spaCapture`, or `SpaDelivery.Extract*`.** A dev agent that reads ADR 0016 §Decision 4 (*"retiring a now-duplicate data path is 22.4's call"*) as a mandate to delete the extraction helpers will break the IR for 853 pages and block 23.4.

The retired [Story 22.3 file](22-3-static-html-rendered-from-the-ir.md) is kept as a reference and characterises that surviving path — the 25-templater inventory, the `NavLocalContext` blocker, eight traps and the ADR constraint table. **Its line numbers are stale** (baseline `32fd282`); this story's are re-measured at `6017c2c`.

## Acceptance Criteria

1. **One shared prelude; the webview is a projection of the bundle, not a parallel builder.**
   **Given** `BuildSpaBundle` and `RenderWebviewSurfaces` today each build `docs` → `work` → `counts` → `followUps` → `unplanned` → `dashboardPage` and each iterate the epics family independently,
   **When** the seam is unified,
   **Then** exactly **one** code path produces that prelude and the family `PageView` sequence, consumed by both surfaces,
   **And** exactly **one** code path produces a captured page's region (`CapturedNavMarkup` + `ExtractContentRegion`), consumed by both surfaces,
   **And** the story's Completion Notes state, per surface, what remains surface-specific and why (expected: the webview's exclusion set, its degrade skip, its `SourcePath` map, its island strip, and its `WrapDocument` entry document — nothing else).

2. **The webview's observable behaviour is unchanged, exclusion for exclusion.**
   **Given** the webview deliberately excludes code pages (`_codePages` values, matched as an exact set), commit-day pages (`_commitDays`), and the `commit/` prefix, and deliberately **skips** a page whose region degraded to nav-only ([:2963](../../src/SpecScribe/SiteGenerator.cs) `ReferenceEquals`),
   **When** it is re-expressed as a filter over the shared bundle,
   **Then** the emitted `WebviewBundle` — surface set, order, `ContentHtml`, `Title`, `SourcePath`, `EntryDocument`, `ProjectOutline` — is **byte-identical** to its pre-story output on the same input,
   **And** the `ReferenceEquals` degrade signal still works (see Trap 3),
   **And** ⚠️ the **captured-surface island divergence** (see Trap 1) is resolved deliberately in one direction with the choice and its reasoning recorded — not left to fall out of the refactor.

3. **The duplicate, non-IR data paths are retired — and the surviving region path is named.**
   **Given** ADR 0016 §Decision 4 assigns this call to this story, and Story 23.4 AC #3 keeps one C# region-composition path,
   **When** the unification lands,
   **Then** the Completion Notes enumerate **every symbol deleted** and **every symbol deliberately kept**, each with a one-line reason,
   **And** the kept list explicitly includes `_spaCapture`, `SpaDelivery.ExtractContentRegion`, `ExtractNavMarkup`, `ExtractTitle`, `ExtractBreadcrumb`, `ExtractMetaDescription` and `CapturedNavMarkup` — **still the IR's producer for ~853 pages until 23.4 replaces them**,
   **And** no new `HostRenderException` is registered (see Test gates row 5).

4. **One region shape across the whole IR — fixed at the emitter.**
   **Given** Story 23.3's measurement that the IR carries two region shapes — 187 family pages carry the `<div class="page-wayfinding">` wrapper, ~853 captured pages slice from the inner `<div class="breadcrumb"` and are unbalanced by one element,
   **When** `ExtractContentRegion` prefers `<div class="page-wayfinding"` as its slice start and falls back to `<div class="breadcrumb"`,
   **Then** every emitted IR region is element-balanced, carries exactly one `<main id="main-content">`, and opens and closes its wayfinding band on the same side of `<main>`,
   **And** a C#-side test asserts that invariant across the **whole** emitted IR, not a sample,
   **And** `web/ir/adapter.ts`'s `wayfindingRepaired` repair and its `stillUnbalanced` throw ([`splitContentRegion`](../../web/ir/adapter.ts)) are **deleted**, with `web/test/region-split.test.ts`'s `captured` fixture updated to the single shape — the comment there already says *"the emitter should slice from the wrapper"*,
   **And** `npm run check:a11y` (which asserts `one-main`, `wayfinding-single` and `wayfinding-closed` over the emitted HTML) stays green.

5. **The 46-delta pipeline-ordering defect is fixed on the static side.**
   **Given** `RenderEpicsPages` runs at [SiteGenerator.cs:365](../../src/SpecScribe/SiteGenerator.cs) and builds its follow-up geometry from `ResolveFollowUpWork(files)` because `_docs` is empty at that point ([:2617](../../src/SpecScribe/SiteGenerator.cs)), while `BuildSpaBundle` builds it from `WorkInventory.Build(_docs.Values)` ([:3109](../../src/SpecScribe/SiteGenerator.cs)) — and 23.3 measured **the static page as the stale side** across 46 surfaces,
   **When** a full generation runs,
   **Then** the static epic/story pages and the IR see the **same** work inventory,
   **And** the per-story work-graph node/edge counts agree between the two surfaces, asserted by a test that would have caught the 46-delta,
   **And** diagnostics event ordering is preserved ([:454-461](../../src/SpecScribe/SiteGenerator.cs) — load-bearing for the golden fingerprint),
   **And** the fix is stated as an **ordering fix** (the static page was stale), never as a capture fix.

6. **`schemaVersion` is answered explicitly, and both consumers move in the same change.**
   **Given** `SpaDelivery.SchemaVersion`'s own compatibility rule lists *"a change to how a page's content region is delimited"* as a **bump** trigger, and AC #4 changes exactly that,
   **When** the region delimiter moves,
   **Then** the story records the explicit finding — **expected: bump to `2`**, with the measurement (how many pages' `contentHash` moved, and the byte delta shape) — or, if the emitted bytes and region boundaries turn out identical on every page, **no bump**, with the measurement that proves it,
   **And** the finding is recorded in `SpaDelivery.SchemaVersion`'s doc comment, not only in the story file,
   **And** `EXPECTED_SCHEMA_VERSION` is updated in **both** [`web/ir/adapter.ts:59`](../../web/ir/adapter.ts) **and** [`web/ir/adapter.client.ts:35`](../../web/ir/adapter.client.ts) in the same change — the adapter only `console.warn`s on a mismatch, so a missed one is silent.

7. **Byte parity: only AC #5 may move the golden fingerprint.**
   **Given** NFR4 (additive), and that the golden fixture generates **without `--spa`** ([`Options()`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs) passes no `emitSpa`, so `spa/` is never emitted into it),
   **When** the full suite runs,
   **Then** AC #4's region change **cannot** move `GoldenContentFingerprint` — if it does, that is a defect to diagnose, because it means static HTML bytes changed,
   **And** AC #5's ordering fix is the **one** sanctioned mover; its delta is enumerated **page-by-page and justified before any regeneration** (it may legitimately be zero on this fixture — measure, do not assume),
   **And** any regeneration follows CLAUDE.md § Verification: stable across **two repeated runs**, naming the concurrent session's changes it sat on top of.
   ⚠️ **Current value is `3171cf5c7b389640606ccdd4fa763cdf0af38237d7d7a1ddd3077bc0a415e8c4`** ([SiteGeneratorAdapterTests.cs:1162](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs)) — **not** the `7adbdb01…` Story 22.3 recorded (it moved again under Story 20.8). **Read it from the file, never from a story record.**

8. **NFR6 holds, verified in a live browser with JavaScript disabled.**
   **Given** ADR 0013 §Decision 3 requires this be *"verified in a live browser with JavaScript disabled — not by test assertion alone"*, and Story 23.3's nested-`<main>` defect passed parity, links and every a11y assertion while the DOM was corrupt,
   **When** the regenerated static site and the SPA entry shell are opened with scripts blocked,
   **Then** content renders and both are fully navigable, identical in this respect to the pre-story baseline,
   **And** the verification names the pages checked, the mechanism used to block scripts, and reports **real DOM geometry** (the only thing that caught 23.3's defect) for at least one paged captured page and one family page.

9. **The ADR is proposed.**
   **Given** ADR 0008 §Decision 2 says static HTML, SPA and webview are *co-equal projections* while the webview is in fact a **second independent builder**,
   **When** the seam is unified,
   **Then** an ADR is proposed recording that the SPA and the webview are **filtered projections of one region seam over the canonical IR**, that the webview is **not** a Nuxt consumer (23.4 AC #3), and what the seam's surviving surface-specific filters are,
   **And** it is cross-referenced from `docs/adrs/README.md`, ADR 0005, ADR 0008 and ADR 0016.
   ⚠️ **Numbering:** `0019` is claimed-but-unwritten by Story 18.3 and `0020` is pre-claimed by Story 18.5; `0021`/`0022` are written. **`0023` is the first uncontested slot** — confirm by listing `docs/adrs/` at implementation time and expect contention on `README.md`.

## Tasks / Subtasks

**Sequence matters.** Task 1 is measurement — without a captured "before" bundle, AC #2's byte-identity claim is unfalsifiable. Tasks 2 and 3 are the two defect fixes and are *independent of each other*; do them before the unification so the refactor lands on a fixed baseline rather than carrying a defect into a new shape.

- [x] **Task 0 — Re-verify every line number in this file.** They were measured at `6017c2c`. `SiteGenerator.cs` is 5,359 lines and moves under concurrent sessions; 22.3's numbers were already ~40 lines stale within one day. Grep for the symbol, never trust the number.

- [x] **Task 1 — Capture the "before" oracle (AC: #1, #2).**
  - [x] `dotnet run --project src/SpecScribe -- generate --spa --deep-git` into `SpecScribeOutput/` (the default — **never** `--output docs/live`).
  - [x] ⚠️ **Verify `git-insights.html`, `deep-analytics.html`, `impact-map.html` and `commit/*.html` are actually present** before trusting the page count. Memory `gitmetrics-3s-timeout-silent-deep-git-loss`: `GitMetrics`' hard-coded 3,000 ms budget silently drops all of them at `errors=0` (6,496 ms measured cold), costing 3 surfaces and ~300 pages. A default generate emits **1,046** IR pages and is missing them entirely.
  - [x] Serialize the pre-story `WebviewBundle` (all surfaces, `EntryDocument`, `ProjectOutline`) and the pre-story `spa/` file set to a scratch location. **This is the AC #2 oracle** — a byte-identity claim with no captured baseline is a claim, not a measurement.

- [x] **Task 2 — Fix the two-region-shapes defect at the emitter (AC: #4, #6).**
  - [x] In [`SpaDelivery.ExtractContentRegion`](../../src/SpecScribe/SpaDelivery.cs), prefer `<div class="page-wayfinding"` as `bodyStart`, falling back to `<div class="breadcrumb"`, then to `mainOpen`. Take the **earliest candidate that precedes `mainOpen`** — the same rule `web/ir/adapter.ts` uses today — so a breadcrumb appearing *after* `<main>` never splits the region (`region-split.test.ts` already pins that case).
  - [x] The wrapper appears only where a pager renders. Affected captured templaters — the 5 `SiteNav.RenderWayfinding` callers: [`CodeFileTemplater.cs:740`](../../src/SpecScribe/CodeFileTemplater.cs) (`RenderPage` + `RenderPlaceholder`, ~hundreds), [`HtmlTemplater.cs:31`](../../src/SpecScribe/HtmlTemplater.cs) (ADR records), [`CommitDayTemplater.cs:44`](../../src/SpecScribe/CommitDayTemplater.cs), [`CommitDetailTemplater.cs:49`](../../src/SpecScribe/CommitDetailTemplater.cs), [`RetroTemplater.cs:72`](../../src/SpecScribe/RetroTemplater.cs). Every other templater calls `SiteNav.RenderBreadcrumb`, which is byte-identical to `RenderWayfinding` with an empty pager and emits **no wrapper** — those pages are unaffected.
  - [x] Add the whole-IR invariant test (AC #4). Assert over every emitted region, not a sample.
  - [x] Delete `WAYFINDING_OPEN` / `wayfindingRepaired` / `stillUnbalanced` from [`web/ir/adapter.ts`](../../web/ir/adapter.ts) and update `IrRegion` + its `types.ts` declaration; update `web/test/region-split.test.ts`'s `captured` fixture to the single shape.
  - [x] Measure the `contentHash` delta (AC #6) and decide the `schemaVersion` bump. **`SiteGeneratorSpaTests.cs:517`** (`ManifestAndChunks_AreByteIdentical_AcrossTwoConsecutiveRunsOfUnchangedInput`) names the page whose hash moved — use it as the measuring instrument, not just as a gate.

- [x] **Task 3 — Fix the 46-delta pipeline-ordering defect (AC: #5, #7).**
  - [x] Make `RenderEpicsPages` see the same fully-populated work inventory the IR sees. The blocker is structural: `_docs` is filled inside `GenerateOneInternal` ([:3289](../../src/SpecScribe/SiteGenerator.cs)) **after** it writes the page, and `RenderEpicsPages` runs before that loop — which is exactly why [`:2617`](../../src/SpecScribe/SiteGenerator.cs) reaches for `ResolveFollowUpWork(files)` instead.
  - [x] ⚠️ **Trap 2 (the `alreadyExisted` flip) applies to any pre-population approach — read it before choosing one.**
  - [x] Share **one** `WorkInventory` / `FollowUpGeometry` instance between `RenderEpicsPages`, `BuildSpaBundle` and `RenderWebviewSurfaces`. One instance, not three equal ones — an equal-but-separate build is the same defect waiting to drift again.
  - [x] Add the agreement test (AC #5): per-story work-graph node/edge counts equal across static and IR.
  - [x] Enumerate the static byte delta page-by-page. **It may be zero on the golden fixture** (whether `StorySubgraph` differs there depends on the fixture carrying deferred work) — measure the *real* `SpecScribeOutput/` too, since the fixture proves nothing about pages it does not contain.
  - [x] ⚠️ `RenderEpicsPages` is **also** called from the watch route ([:741](../../src/SpecScribe/SiteGenerator.cs)). Confirm the change does not alter watch-mode behaviour; `RegenerateEpics`' known non-oracle-faithfulness is **22.5's**, not this story's (Scope guard #1).

- [x] **Task 4 — Extract the shared prelude (AC: #1).**
  - [x] Lift `docs` → `work` → `counts` → `followUps` → `unplanned` → `dashboardPage` into one private builder returning the shared state, consumed by `BuildSpaBundle`, `RenderWebviewSurfaces` **and** (for AC #5) `RenderEpicsPages`' geometry.
  - [x] ⚠️ Keep `CodeItemHref` **explicit** at the `HtmlTemplater.BuildIndexPage` call. Both current call sites carry a comment about it because the named arguments start at `counts:` and a positional omission silently nulls the resolver, degrading Git Pulse bar labels from links to text and dropping **5 anchors** — the entire 277-byte parity delta Story 23.1 measured. Unifying the call is the moment to lose it.

- [x] **Task 5 — Extract the shared captured-region loop and express the webview as a filter (AC: #1, #2, #3).**
  - [x] One loop produces `(path, title, region, breadcrumb, metaDescription, degraded)` per captured page.
  - [x] The webview consumes it with its own filter: exclusions, `degraded → skip`, `SourcePath` join via `BuildCapturedSourceMap`, island strip. The SPA consumes it unfiltered.
  - [x] Resolve Trap 1 (the island divergence) here, in the direction chosen under AC #2.
  - [x] Diff the emitted `WebviewBundle` and `spa/` file set against Task 1's oracle. **Byte-identical except the AC #4 region change and the AC #5 inventory change**, each attributed.

- [x] **Task 6 — Enumerate deleted vs. deliberately-kept symbols (AC: #3).** Kept list must state *why* — "still the IR's producer for ~853 pages until 23.4" — so the next agent does not read it as dead code.

- [x] **Task 7 — Verify (AC: #2, #4, #5, #7).**
  - [x] `dotnet test SpecScribe.slnx` — golden fingerprint per AC #7; registry ceiling holds (see Test gates row 5).
  - [x] Under `web/`: `npm run test` (region-split), `npm run check:a11y`, `npm run check:links`, `npm run measure:parity`, `npm run check:ir-content`.
  - [x] ⚠️ Verify the git-derived surfaces **separately**: `git-insights.html`, `impact-map.html`, `timeline.html` and `commits/` are **absent from the golden fixture** (honest-scope-limit comment at [`HierarchyExplorerTests.cs:671-679`](../../tests/SpecScribe.Tests/HierarchyExplorerTests.cs) — note `code-map.html` and `risk-quadrant.html` **do** render there; only the deep-git surfaces are absent). A green fingerprint is **not** evidence they survived — and `impact-map.html` is the one captured surface carrying a JSON island (Trap 1).

- [x] **Task 8 — Live-browser JS-off verification (AC: #8).** Real DOM geometry, not a test assertion.

- [x] **Task 9 — Propose the ADR and cross-reference it (AC: #9).**

- [x] **Task 10 — Record the AC drift in `epics.md` AND `sprint-status.yaml` in the same change** (CLAUDE.md § Decision records). Include that `22-3`'s "kept as reference" status is unchanged and that 23.4's Task 8 restatement obligation is **discharged by this story running first** (D2) — 23.4 should be updated to point at the seam this story built.

## Dev Notes

### Current-state map (measured at `6017c2c`)

```
GenerateAll
 :365   RenderEpicsPages(...)            ← builds followUps from ResolveFollowUpWork(files)   ⟵ 46-delta, stale side
 :378   foreach pageFiles → GenerateOneInternal → WriteOutput(...) ; _docs[relative] = doc (:3289)
 :457   workInventory = WorkInventory.Build(_docs.Values)          ← the complete inventory
 ...
 :3101  BuildSpaBundle(nav)              ← rebuilds work/counts/followUps from _docs.Values   ⟵ IR, complete side
 :3160    foreach _spaCapture → CapturedNavMarkup + ExtractContentRegion
 :2810  RenderWebviewSurfaces()          ← rebuilds the SAME prelude a third time
 :2944    foreach _spaCapture → CapturedNavMarkup + ExtractContentRegion (+ exclusions, degrade skip)
```

`WriteOutput` ([:3074](../../src/SpecScribe/SiteGenerator.cs)) is the capture seam: it captures the finished page string in memory one step before it becomes a file. It never reads a generated `.html` back off disk — that is what keeps AD-1/AD-2 intact, and it stays true after this story.

### Existing machinery — extend it, do not reinvent

| Need | Already exists | Where |
|---|---|---|
| Compose the IR content region from a view model | `JsonSpaRenderAdapter.RenderContent` | [JsonSpaRenderAdapter.cs:42](../../src/SpecScribe/JsonSpaRenderAdapter.cs) — nav + wayfinding + body, no scripts |
| The same region minus JSON islands | `WebviewRenderAdapter.RenderContent` | [WebviewRenderAdapter.cs:63](../../src/SpecScribe/WebviewRenderAdapter.cs) — **identical** to the above except one regex |
| Breadcrumb **or** breadcrumb+pager, byte-identically | `HtmlRenderAdapter.RenderWayfinding` | [:424](../../src/SpecScribe/HtmlRenderAdapter.cs) — returns plain `RenderBreadcrumb` when the pager renders empty; **that branch is the whole two-shapes defect** |
| The page's own local-context nav band | `CapturedNavMarkup` | [SiteGenerator.cs:3193](../../src/SpecScribe/SiteGenerator.cs) — slices the band; **one instance per page** (Trap 3) |
| Script-island classification | `SpaDelivery.ExtractScriptIslands` | Operates on the region regardless of producer — survives unchanged |
| Chunking, byte budget, oversized declaration, content hash | `SpaDelivery.BuildDataFiles` | Consumes regions, not captures — survives unchanged |
| Region split on the consumer side | `web/ir/adapter.ts splitContentRegion` | The repair half is what AC #4 deletes |
| Sanctioned per-surface divergence | `HostRenderExceptions.Registry` | **Do not add to it** — four hygiene tests cap it |

### Trap 1 — captured webview surfaces carry a JSON island that family surfaces strip

`WebviewRenderAdapter.RenderContent` strips `<script type="application/json">` islands ([WebviewRenderAdapter.cs:81](../../src/SpecScribe/WebviewRenderAdapter.cs)), registered as the `data-island` webview `HostRenderException`. **But the captured loop never calls it** — it puts `ExtractContentRegion`'s output straight into a `WebviewSurface` ([:2971-2976](../../src/SpecScribe/SiteGenerator.cs)). So the strip is **family-only**.

Reachable today: [`ImpactMapTemplater.cs:59`](../../src/SpecScribe/ImpactMapTemplater.cs) calls `HierarchyExplorer.Render`, which emits `<script type="application/json" class="ss-hierarchy-data" …>` ([HierarchyExplorer.cs:560](../../src/SpecScribe/HierarchyExplorer.cs)); `impact-map.html` rides `WriteOutput` ([:3632](../../src/SpecScribe/SiteGenerator.cs)) and is in **none** of the webview's exclusion sets. It exists only under `--deep-git`, which is why no test sees it: `EverySurface_CarriesTheChromeAndNoScript` ([SiteGeneratorWebviewTests.cs:163](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs)) asserts `DoesNotContain("<script")` but runs on `GeneratedSite()` — **no capture**. The `GeneratedSiteWithCapture()` tests assert chrome and source paths, never scripts.

Unifying the seam forces the question. **Recommended: strip on both**, which is what the registered `data-island` exception already claims and what the adapter's own comment argues (the webview ships no `specscribe.js`, so nothing can ever read the island). It is inert data, so this is dead-weight removal, not a behaviour change a reader can see — but it **is** a byte change to one webview surface, so AC #2's byte-identity claim must name it as the one attributed delta. If you preserve the current behaviour instead, say so and extend the no-script assertion to the captured set so it stops being invisible either way.

### Trap 2 — pre-populating `_docs` flips `alreadyExisted` and rewrites the diagnostics stream

`GenerateOneInternal` computes `var alreadyExisted = _docs.ContainsKey(relative)` ([:3282](../../src/SpecScribe/SiteGenerator.cs)) and returns `Updated` vs `Generated` from it. **Any approach that fills `_docs` before the pages loop turns every `Generated` event into `Updated`** — which changes the diagnostics page, and diagnostics event ordering/content is load-bearing for the golden fingerprint ([:454-461](../../src/SpecScribe/SiteGenerator.cs)).

This is the single most likely way to "fix" AC #5 and move the fingerprint for a reason that has nothing to do with the work inventory — which would make AC #7's page-by-page enumeration unreadable. Options, in preference order:

1. Build the inventory from a **separate** read that does not touch `_docs` (mirroring what `ResolveFollowUpWork` does today, but over the same doc set `WorkInventory.Build(_docs.Values)` would see).
2. Pre-populate `_docs` but track first-write separately so the `Generated`/`Updated` split is preserved exactly.
3. Move `RenderEpicsPages` after the pages loop — **verify the diagnostics/phase ordering first** (`reporter?.BeginPhase(GenerationPhase.Epics)` sits around it, and `RenderRetroPages` runs between).

Whichever route: prove the diagnostics stream is unchanged **before** measuring the AC #5 byte delta, or the two changes will be indistinguishable.

### Trap 3 — `ExtractContentRegion`'s degrade path is reference-equality

Its no-landmark path returns the `navMarkup` **instance**, and the webview loop detects the degrade with `ReferenceEquals(region, navMarkup)` ([:2963](../../src/SpecScribe/SiteGenerator.cs)) to skip the surface. A shared loop that recomputes, copies or re-concatenates the nav markup **silently breaks that detection** — no test fails, and a content-empty surface ships into the webview. If the shared loop returns a `degraded` flag instead, keep the `ReferenceEquals` contract intact underneath it or replace it with an explicit signal in the **same** change.

Note the asymmetry this protects: the SPA **keeps** degraded pages (a browser tab is escapable), the webview **drops** them (a status panel claiming "links work" is not). That asymmetry is deliberate — preserve it.

### Trap 4 — the shared prelude is not actually identical; diff it before lifting

`RenderWebviewSurfaces` wraps its `BuildStoryPageFragments` call in a `try/catch (IOException or UnauthorizedAccessException)` that degrades **one** story to a placeholder (catch at [:2883](../../src/SpecScribe/SiteGenerator.cs)); `BuildSpaBundle`'s equivalent call has **no such catch** ([:3144](../../src/SpecScribe/SiteGenerator.cs)). Lifting them into one builder silently gives the SPA resilience it did not have, or takes it from the webview. Decide which, and say so — it changes behaviour under a file that vanishes mid-render.

### Trap 5 — `HtmlTemplater.cs:82` exploits the slicer's truncation point

The section-nav script is appended *after* `</main>` **because `ExtractContentRegion` truncates there** — the comment says so explicitly. AC #4 changes only the slice's **start**, not its end, so this stays valid. Do not "tidy" it; and if any later step moves the end marker, this becomes load-bearing on a rule that no longer exists.

### Trap 6 — two moving targets under a concurrent session

- `SiteGeneratorSpaTests.cs:225` (`HierarchyEngineBundle_ShipsOnlyWhereAHierarchyChartWasRendered`) constrains which pages ship the hierarchy engine. Stories 20.7/20.9 have been mounting explorers on more surfaces — expect it to move under you, and confirm whose change moved it before touching it. It also determines **how many** captured pages carry an island (Trap 1).
- `main` carries a background auto-committer and concurrent sessions. **Grep-verify every symbol you add before relying on it**, and confirm with `git diff HEAD` — a zero-grep can be a transient mid-write read (memory `shared-main-concurrent-edit-loss-verify-after-edit`).

### Test gates, ranked by how likely this refactor trips them

1. **Webview bundle shape** — `SiteGeneratorWebviewTests.cs`: `:140` five families, `:163` chrome + no-script (**family-only**, see Trap 1), `:483`/`:752` captured surfaces + `SourcePath`, `:547` `CapturedSurface_KeepsThePagesOwnLocalContextNavBand`, `:605`/`:623` every entry nav link resolves to a bundled surface, `:733` code pages excluded, `:777`/`:814` degrade-to-valid-bundle for non-BMad and empty workspaces.
2. **`<main>` byte-identity between static and IR/webview** — `SiteGeneratorSpaTests.cs:420` (`DashboardIrRegion_CarriesTheSameMainBlock_AsTheStaticPage`), `:574` (`LongTailRegion_IsTheSameCSharpRenderedContent_AsTheStaticPageMainBlock`), `SiteGeneratorWebviewTests.cs:516`.
3. **Page-local nav band, byte-for-byte** — `SiteGeneratorSpaTests.cs:433`, `SiteGeneratorWebviewTests.cs:547`. Story 22.2 just fixed this; do not regress it while unifying `CapturedNavMarkup`.
4. **Determinism + manifest/region agreement** — `SiteGeneratorSpaTests.cs:517` (byte-identical across two consecutive runs; **names the page whose `contentHash` moved** — the AC #6 measuring instrument), `:335` round-trip, `:465` schema/head/islands/hash/bytes, `CanonicalIrSerializationTests.cs` (re-declares the manifest shape independently — any new IR field fails it loudly, deliberately; its doc comment currently reads *"Enumerated and justified differences: none"*).
5. **The `HostRenderExceptions.Registry` ceiling** — `WebviewRenderAdapterTests.cs:420` asserts **exactly 5** webview entries (`asset.css`, `asset.js`, `mermaid`, `data-island`, `hierarchy-chart` — **not 4**; Story 22.3's note is stale), `RenderSpaParityTests.cs:197` exactly 1 spa entry, `RenderParityTests.cs:207` **zero** `html` entries, `RenderSectionParityTests.cs:303` **never** a `section.*` entry. **This refactor may not add one.** If it needs one, that is a design signal, not paperwork.
6. **Golden fingerprint** — `SiteGeneratorAdapterTests.cs:237`, constant at `:1162`. `NormalizeVolatile` strips CRLF, today's date, the fixture root, the footer clock, `?v=`, the subtitle version and the Version/Build rows — everything else is load-bearing. The comment block above the constant is the regeneration audit trail; follow its ritual.
7. **`web/` harnesses** — `npm run test` (`region-split.test.ts`), `check:a11y` (`one-main`, `wayfinding-single`, `wayfinding-closed`), `check:links`, `measure:parity`, `check:ir-content` (class/id-bound — a changed class or id in an emitted region turns it red until re-extracted).

**Flake discipline.** A red `SiteGenerator*` generate-to-disk test should be re-run **in isolation** before being called a regression — the documented rotating file-write-contention family (`FileWatcherServiceTests.BurstOfSaves`, `SiteGeneratorTimelineTests` ×3, `SiteGeneratorCodeMapTests` determinism, `SiteGeneratorGitInsightsTests` hub, `SiteGeneratorReadmeTests`, `SiteGeneratorImpactMapTests`, `SiteGeneratorGroupedNavTests`). A red `RenderParity*` / `SpaDelivery*` / `CanonicalIrSerialization*` / `GoldenContentFingerprint` is **not** in that family and must be treated as real.

### Architecture invariants that bound this work

| Invariant | What it forbids here |
|---|---|
| **AD-1 / AD-2** | Adapters translate; they do not reinterpret. The capture consumes the pipeline's own output, never a disk read-back — keep it that way. |
| **ADR 0008 §Decision 1** | The C# core is the IR's **single producer**. One seam strengthens this. |
| **ADR 0008 §Decision 2** | Static HTML, SPA and webview are **co-equal projections** — AC #9's ADR records how this story makes the webview a projection rather than a rival builder. |
| **ADR 0016 §Decision 4** | No second capture path; retiring a now-duplicate data path is **22.4's call** — this story's grant. It is *not* a grant to delete the region path 23.4 AC #3 keeps. |
| **ADR 0016 §Decision 5** | `schemaVersion` bumps on *"a change to how a content region is delimited"* — AC #6. |
| **ADR 0017 §Decision 2** | **No href inside IR content is ever rewritten.** 499 links (216 distinct targets) dangle on the golden site today — **reproduce them faithfully; do not patch them here** (Scope guard #6). |
| **ADR 0018** | The `ir-content.css` extraction is class/id-bound. Changing a class or id in an emitted region turns `check:ir-content` red until re-extracted. |
| **ADR 0012 §Addendum-5** | Asset emission stays **conditional** — a site with no code pages stays byte-identical. |
| **ADR 0013 §1 / §2** | Information and navigation survive with JS off; the text twin is **server-rendered**, never injected — it lives in `BodyHtml` and must survive the region path. |
| **NFR4 (additive)** | Static bytes must not change, except AC #5's ordering fix. |

### Scope guard — seven things this story is NOT

1. **Not the full inversion.** Migrating the 25 remaining templaters onto `PageView` (retired Story 22.3's D1) is **not** in scope. The `NavLocalContext` blocker stays unsolved and stays fine — captured pages keep their band by slicing it, exactly as Story 22.2 designed.
2. **Not the deletion of the slicers.** `_spaCapture` and `SpaDelivery.Extract*` are still the IR's producer for ~853 pages. 23.4 AC #3 replaces them.
3. **Not retiring `HtmlRenderAdapter`.** That is 23.4, and this story runs first (D2).
4. **Not the incremental engine.** `RegenerateEpics`' watch-mode divergence (22.1's measured 56-page work-graph over-count at no-op) is **22.5's**. Fixing the *full-generation* ordering defect (AC #5) is in scope; changing watch-route invalidation is not.
5. **Not a delta channel.** 22.6's.
6. **Not a link-graph cleanup.** 499 dangling links are inherited and reproduced faithfully (ADR 0017 §Decision 2). Fixing them here makes the parity measurement unreadable.
7. **Not a `--spa` behaviour change.** The SPA stays opt-in via `ForgeOptions.EmitSpa`; a default generation must gain no cost. Note `_spaCapture` is allocated when `EmitSpa || CapturePages` ([:209](../../src/SpecScribe/SiteGenerator.cs)) — the webview turns capture on independently.

### Previous-story intelligence

**From 22.2 (review, 2026-07-26):**
- 22.2 fixed both defects 23.3 predicted were unavoidable — the 5-anchor `codeItemHref` drift and the page-local nav band. **Do not re-fix them, and do not lose them in the prelude lift** (Task 4).
- `HierarchyExplorer.BootScript` is emitted *between* the breadcrumb and `<main>`, and the slice starts at the breadcrumb — so a captured page gaining `HierarchyEngineNeeded` ships an **executable** script into both consumers. **AC #4 moves the slice start earlier**, so re-derive this rather than assuming 22.2's answer still holds: the wrapper sits before the boot script on paged pages.
- `diagnostics.html` echoes the configured output root inside its own region, so it is **the one page whose `contentHash` is output-path dependent** and differs machine-to-machine on identical input. Expect it; do not "fix" it.

**From 23.3 (review, 2026-07-27) — the story that handed this one its work:**
- 189/189 migrated surfaces reached byte-identical `<main>`; 0 link regressions across 89,280 internal links; 0 a11y failures across 1,051 pages. **That is the bar this story must not lower.**
- **The defect worth internalizing:** a double-opened wayfinding wrapper nested `<main>` and `<footer>` inside the breadcrumb band on all 187 migrated pages — and **passed parity, link resolution and every a11y assertion**, because the wrapper sits *outside* `<main>`. Found only by measuring real DOM geometry in a live browser. This story changes the wayfinding band's boundary on hundreds of pages; **that is the same defect class, at the same seam.**
- 23.3's named gaps handed to Epic 22 (`?v=` token, favicon URI, boot script, `extraHead`, footer — all read off the generated entry page instead) are **not** this story's; they are chrome the IR does not project, and 23.4 owns them.

**From 23.5 (review, 2026-07-27):** ADR 0022 (Proposed) settled packaging — Node is a build-time toolchain and a generate-time runtime. Relevant here only because it unblocked 23.4; nothing in this story ships Node.

### Git intelligence

- Baseline `6017c2c` ("Story 25.2: re-measure the gate after the full coverage path landed"). Working tree at create-story: only `sprint-status.yaml` modified.
- Recent commits bundle several stories at once (`c1a6ee5` carried 18.4, 18.5, 20.8, 23.5, 25.3 + ADRs 0021/0022). **Scope any later code review by this story's File List and symbols, never by a commit range** (CLAUDE.md § Scoping a code review).
- `GoldenContentFingerprint` moved under Story 20.8 to `3171cf5c…`. Story 22.3's recorded `7adbdb01…` and Story 22.2's `91c3aeb4…` are both stale. **Read it from the file.**
- CI is live (`build-test-analyze`, Story 25.1) on Windows and Ubuntu, with a SonarCloud quality gate (Story 25.2) and `web/**` drift gates that need `web/public/` populated first (`b86fc27`).

### Project Structure Notes

- Production code: `src/SpecScribe/` (single project, .NET 10). Tests: `tests/SpecScribe.Tests/` (flat, xUnit). `SpecScribe.slnx` has exactly two projects — nothing new joins it.
- Front-end: `web/` (Nuxt 3). This story touches exactly two files there — `ir/adapter.ts` (+ `adapter.client.ts` for the version constant) and `test/region-split.test.ts` — plus whatever `types.ts` declares for `IrRegion`.
- **No new NuGet or npm dependencies.** This is an internal composition refactor; the package set (`Markdig`, `Spectre.Console`, `Spectre.Console.Cli`, `YamlDotNet`) is unchanged, so **no external version research applies** and none was done. The one external-clock fact in the neighbourhood — Nuxt 3 reaching EOL 2026-07-31 — belongs to Stories 23.4/23.5 and is not triggered by the two-file `web/` change here.
- ADRs live in `docs/adrs/` with a `README.md` index. See AC #9 for numbering.
- Generate to `SpecScribeOutput/` (the default). Never `--output docs/live` — vestigial and gitignored.

### References

- [epics.md § Story 22.4](../planning-artifacts/epics.md) — the three original ACs, superseded here; the SCOPE ADDITION banner; see Task 10.
- [Story 22.3 (retired)](22-3-static-html-rendered-from-the-ir.md) — kept as the characterisation of the surviving C# region path: the 25-templater inventory, the `NavLocalContext` blocker, eight traps, the ADR constraint table. ⚠️ **Its line numbers and its fingerprint value are stale.**
- [Story 23.3](23-3-migrate-baseline-surfaces-dashboard-epics.md) — the 46-delta root cause with the "IR is the more complete side" finding, the two-region-shapes measurement, and § *Named gaps handed to Epic 22*.
- [Story 23.4](23-4-migrate-remaining-surfaces-retire-c-sharp-html-adapter.md) — AC #3 (the surviving region path), AC #7 (the restatement obligation this story discharges by running first), Dev Notes § *The circularity*.
- [Story 22.2](22-2-canonical-ir-schema-and-versioning.md) — the head projection, script-island declaration, per-page hash/bytes, and the `CapturedNavMarkup` decision this story preserves.
- [ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) — §Decision 1, §Decision 2.
- [ADR 0016](../../docs/adrs/0016-ir-carries-rendered-prose-html.md) — §Decision 4 (this story's grant), §Decision 5 (`schemaVersion` triggers), §Consequences ("one capture path").
- [ADR 0017](../../docs/adrs/0017-projection-routes-mirror-ir-paths.md) — §Decision 2.
- [ADR 0018](../../docs/adrs/0018-transitional-ir-content-style-layer.md) — the class/id-bound extraction gate.
- [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) — §1, §2, §3 (the live JS-off browser gate).
- [ADR 0005](../../docs/adrs/0005-vs-code-webview-runtime-and-packaging.md) — the webview runtime contract AC #9's ADR cross-references.
- [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1, AD-2, AD-5.
- [CLAUDE.md](../../CLAUDE.md) — § Concurrent work on shared `main`, § Decision records, § Verification.

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Claude Code, dev-story workflow), 2026-07-28. Baseline `6017c2c`; run against working tree at `06b300c`.

### Debug Log References

**The measurement harness is the load-bearing part of this story — record it before the results.**

- **A baseline BINARY, not a baseline snapshot.** The first before/after pair (oracle captured 10:19, after-state 10:46) straddled a concurrent session's work and was unusable: 6 surfaces appeared, the outline summary shifted a story from review→done, and `epics/story-18-4.html` moved by +25 KB with *no code change at all* (proven by a control run: two identical-code generations 10 minutes apart differed on exactly that one page). The usable method was to build a **pre-story binary from the live tree**: copy `src/` to scratch, restore only this story's three files (`SiteGenerator.cs`, `SpaDelivery.cs`, `FollowUpRefs.cs`) from `HEAD`, compile, and run both binaries against the *same* `_bmad-output` + `docs/adrs`. Every delta is then attributable to this story alone. The concurrent session touched 7 different files, so the separation was clean.
- ⚠️ **A diff that reports "identical" can be reporting nothing.** The first webview diff claimed a perfect `822/822 contentHtml` match. The webview payload's surface field is **`content`**, not `contentHtml` — it was comparing `undefined` to `undefined`. Caught only because `contentWrapperOnly: 0` contradicted the independently-measured 594-page region change. **Assert the field exists before trusting a bundle diff.**
- Region shapes measured by walking `spa/manifest.json` + chunks directly (`measure-regions.js`); static-vs-IR `<main>` parity by comparing each page's on-disk `.html` against its IR chunk entry (`measure-46delta.js`); static byte delta with golden-style volatile normalization (footer clock, `?v=`, output root, build stamp).
- Full suite run 4×. A **rotating** 1–5 tests fail per run — a different set each time (`SiteGeneratorCodeInsightsTests`, `SiteGeneratorChangeLogDateLinkTests` ×2, `SiteGeneratorGitInsightsTests`), all green in isolation. This is the documented file-write-contention family, amplified by a concurrent session rebuilding the tree mid-run. Every gate the story marks as *not* in that family was run in isolation and is green.

### Completion Notes List

**All 9 ACs met.** Highlights, then the required enumerations.

**AC #1 — one prelude, one family sequence, one region loop.** The seam is three members in `SiteGenerator.cs`: `BuildSurfacePrelude`, `BuildFamilySurfaces`, `CapturedRegions` (plus `BuildOutline`, extracted so the webview's surface loop could stop being interleaved with tree-building). Both `BuildSpaBundle` and `RenderWebviewSurfaces` are now filters over them.

*What remains surface-specific, per surface, and why:*

| Surface | Surface-specific behaviour | Why it cannot be shared |
|---|---|---|
| webview | **Exclusion set** — `_codePages` values (exact set), `_commitDays`, `commit/` prefix | Owner decision 2026-07-12: these page classes scale with the *target repo*, not its planning artifacts. The SPA is a browser and wants them. |
| webview | **Degrade skip** | Deliberate asymmetry (AC #2): a browser tab is escapable, a status panel claiming "links work" is not. |
| webview | **JSON-island strip** | The webview ships no `specscribe.js`; the SPA's client reads islands. |
| webview | **`SourcePath` join** via `BuildCapturedSourceMap` | Reveal-source is a VS Code host affordance with no browser analogue. |
| webview | **`WrapDocument` entry document** | The host bridge shell — the one place a `<script>` is expected. |
| webview | **`ProjectOutline`** via `BuildOutline` | The sidebar tree is host-only. Projected from the shared family sequence, not rebuilt. |
| SPA | **Keeps degraded pages**; consumes the sequence **unfiltered** | Same asymmetry, other side. |
| SPA | **`Breadcrumb` + `MetaDescription`** from the shared record | The manifest ships structured drill data; the webview does not use these. Computed once in the seam, used by one consumer — the accepted cost of one producer. |

Nothing else. Both former builders' preludes, family iterations and capture loops are gone.

**AC #2 — webview observable behaviour unchanged.** Against the baseline binary on identical inputs: **828/828 surfaces**, identical set *and* emission order; `entryDocument` **byte-identical**; **0** `Title` diffs; **0** `SourcePath` diffs; `ProjectOutline` epics and summary identical. 783 surfaces byte-identical in `content`; the 45 movers are fully attributed — 33 × exactly +30 B (AC #4's wrapper: 11 retro + 22 ADR pages, i.e. precisely the pager-bearing captured set), 10 work-graph (9 epic pages + `work-graph.html`, AC #5), and 2 harness artifacts (`diagnostics.html`: same length, differs only `wv-base`→`wv-head`, the documented output-root-dependent page; `about.html`: exactly +15 B = `" · 06b300c"`, because the scratch baseline copy is not a git checkout).

The `ReferenceEquals` degrade contract is **structurally preserved and hardened**: `CapturedRegion.Degraded` is computed at the slice, inside the seam, so the island strip can no longer defeat it by ordering — which is exactly how it used to be fragile (Trap 3).

⚠️ **Trap 1 (the island divergence) was already resolved before this story began**, in commit `811ba17` — *after* the story's baseline `6017c2c` — in the direction the story recommended (**strip on both**). Verified identical on the baseline binary. This story preserves it and closes the story's own "stop being invisible either way" note by adding `EveryWebviewSurface_IncludingCapturedOnes_CarriesNoScript`, a **whole-set** assertion (the old test ran on a generator with no capture, which is why the divergence hid).

**AC #4 — one region shape.** Measured on the real repo, `--spa --deep-git`, 1,400 IR pages: unbalanced regions **594 → 0**; wrapper-bearing **189 → 783**; landmark-less **1 → 1** (unchanged, the documented degrade); multi-`<main>` **0 → 0**. epics.md's "~853 captured pages" over-counted: pages with no pager were already balanced.

**AC #5 — the 46-delta, root-caused precisely.** The story framed this as pipeline ordering; the *whole* divergence was one argument. `ResolveDeferredModel` passed `_docs.Values` to `FollowUpRefs.BuildHrefMap`, and `_docs` is empty on the `RenderEpicsPages` route — so every spec resolver's `ResolvingHref` came back null and `WorkGraph.BuildStory`'s `else if (slot.Item.ResolvingHref is { Length: > 0 })` branch dropped the resolver node **and** its `Resolves` edge. Fixed with a source-derived `BuildHrefMap` overload over `(sourceRelative, outputRelative)` pairs — legitimate because `PathUtil.ToOutputRelative` is a pure function of the source path, and because `Add` is `TryAdd` with the epics half added first, so a story artifact's key always resolves to its story page in both routes.

**Trap 2 avoided entirely:** `_docs` is never pre-populated, so `alreadyExisted` cannot flip. `updated=0` on every run, before and after. Route 3 (moving `RenderEpicsPages` after the pages loop) was considered and **rejected** — it would reorder the diagnostics stream, and the phase reporter brackets it.

Result: static-vs-IR `<main>` divergence **47 → 0** across 1,406 pages. ⚠️ **The fix reaches further than 23.3 reported**: 9 **epic** pages and `work-graph.html` carried the same defect at epic scope (Epic 1: 13 items/12 links → 16/20). 23.3 only named the 46 story surfaces.

**AC #6 — `schemaVersion` bumped 1 → 2**, recorded in `SpaDelivery.SchemaVersion`'s own doc comment with the measurement (594 pages moved, every one by exactly +30 B). Both `EXPECTED_SCHEMA_VERSION` constants moved in the same change; verified the mismatch warning fires when the new adapter reads an old IR.

**AC #7 — byte parity.** ⚠️ **`GoldenContentFingerprint` did NOT move.** AC #4 cannot move it (the fixture generates without `--spa`), and AC #5 is legitimately **zero-delta on that fixture** — its deferred work carries no spec-doc resolver, so no resolver node was being dropped there. The story anticipated this ("measure, do not assume"). The live-repo static delta was enumerated instead: **1,350 of 1,407 pages byte-identical** after volatile normalization; the 57 movers are 46 story pages + 9 epic pages + `work-graph.html` + `app.html` (asset-version token only — a baseline-build artifact, since the scratch copy has no git SHA). **Zero** changes under `adrs/`, `code/`, `commit/`, `commits/`, `follow-ups/`, `implementation-artifacts/`, `planning-artifacts/`, `requirements/`, `specs/` — i.e. AC #4 moved no static byte, exactly as required.

⚠️ The fingerprint constant is now **`06788c0f…`**, moved by a concurrent code review. The story file's `3171cf5c…` was already stale — a third consecutive story to record a stale value. **Read it from the file.**

**AC #8 — live browser, JS off.** Served under `Content-Security-Policy: script-src 'none'` (ports 8117 static / 8118 Nuxt `.output/public`; `launch.json` entries `static-jsoff-22-4`, `nuxt-jsoff-22-4`). Non-execution proved by page state, not console (CSP violations do not reach console captures): `window.Plotly` undefined and no `data-ss-hierarchy-boot`, on every page checked. Pages: `index.html`, `adrs/0013-…html` (**paged captured page** — the shape this story changed), `epics/story-1-4.html` (**family page**, one of the 46), and `app.html` (SPA entry shell).

Real DOM geometry, viewport 1280×900:

| Surface | `<main>` | `.page-wayfinding` | band bottom / main top | `<main>` in band | `<footer>` in band | h-overflow |
|---|---|---|---|---|---|---|
| static `adrs/0013` | 1 | 1 | 213 / 213 | false | false | false |
| **Nuxt `adrs/0013`** | 1 | 1 | **214 / 214** | **false** | **false** | false |
| static `story-1-4` | 1 | 1 | 135 / 135 | false | — | false |
| **Nuxt `story-1-4`** | 1 | 1 | **136 / 136** | **false** | **false** | false |

The decisive reading: on the Nuxt output `<main>`'s ancestor chain is `div.shell.shell-bare.ir-content › div › body` — a direct child of the shell, **not** nested inside the band. That is the exact geometry Story 23.3's defect corrupted while every assertion passed, and with the consumer-side repair now deleted an unbalanced band would produce it immediately. `mainTextLen` is identical static-vs-Nuxt (10,381 on the ADR; 30,424 on the story page), and both surfaces now report the **same** work-graph label live — *"6 work items and 5 provenance links"* — where the static side previously said *"4 … and 3"*. `app.html` with scripts blocked renders the server-rendered dashboard (`#spa-content` 1265×34,338, `mainTextLen` 58,978 — identical to static `index.html`) plus its `noscript` fallback; it is not a blank shell. Screenshot unavailable (the Browser pane was not compositing); DOM geometry is reported instead, which is what ADR 0013 §3 actually asks for.

**AC #9 — ADR proposed as [0024](../../docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md)**, ⚠️ **not the 0023 this story predicted**: a concurrent session authored *and had ratified* ADR 0023 (Story 25.3) the same day. `0019` remains claimed-but-unwritten. Cross-referenced from `docs/adrs/README.md` and from ADR 0005, 0008 (as **Refined by**) and 0016 (as a **Relates to**, since 0024 exercises its §Decision 4 grant and triggers its §Decision 5 bump).

#### AC #3 — every symbol deleted, and every symbol deliberately KEPT

**Deleted** (all in `SiteGenerator.cs` unless noted):

| Symbol | Why |
|---|---|
| `RenderWebviewSurfaces`' inline prelude (docs/work/counts/followUps/unplanned/dashboardPage) | Duplicate of `BuildSpaBundle`'s. Now `BuildSurfacePrelude`. |
| `BuildSpaBundle`'s inline prelude | Same, other half. |
| `RenderWebviewSurfaces`' inline epics-family iteration | Duplicate. Now `BuildFamilySurfaces`. |
| `BuildSpaBundle`'s inline epics-family iteration | Same, other half. |
| `RenderWebviewSurfaces`' inline captured loop | Duplicate. Now `CapturedRegions` + a filter. |
| `BuildSpaBundle`'s inline captured loop | Same, other half. |
| `RenderWebviewSurfaces`' inline outline construction | Interleaved with the surface loop, which is what blocked sharing. Now `BuildOutline`. |
| `WAYFINDING_OPEN`, `wayfindingRepaired`, `stillUnbalanced` (`web/ir/adapter.ts`) | The consumer-side repair + throw for a shape the emitter no longer emits. A repair that cannot fire is a second, drifting truth. |
| `IrRegion.wayfindingRepaired` (`web/ir/types.ts`) | Field of the above. |
| `region-split.test.ts`'s unbalanced `captured` fixture + its two repair tests | Pinned behaviour that no longer exists; replaced by same-shape and both-markers-after-`<main>` cases. |

**Kept deliberately** — every one **grep-verified present** after the refactor:

| Symbol | Why kept |
|---|---|
| `_spaCapture` | **Still the IR's producer for ~1,200 of 1,400 pages** until Story 23.4 replaces it. |
| `SpaDelivery.ExtractContentRegion` | Same — and it is where AC #4's one-shape fix lives. |
| `SpaDelivery.ExtractNavMarkup` | Feeds `CapturedNavMarkup`; the page-local nav band Story 22.2 fixed depends on it. |
| `SpaDelivery.ExtractTitle` | Both consumers' surface title for captured pages. |
| `SpaDelivery.ExtractBreadcrumb` | The manifest's drill parent/child data for non-family pages. |
| `SpaDelivery.ExtractMetaDescription` | The IR's head projection for captured pages. |
| `CapturedNavMarkup` | Slices the page's OWN nav band; **no `path → NavLocalContext` resolver exists**, so re-rendering would lose it on 16 of 25 templaters. |
| `SpaDelivery.ExtractScriptIslands` | Operates on the region regardless of producer. |
| `SpaDelivery.BuildDataFiles` | Consumes regions, not captures. |
| `AddSpaSurface`, `WebviewSurfaceFor` | Per-surface composition + linkify skip rules; now called from the shared loops. |
| `BuildCapturedSourceMap`, `BuildOutlineSummary` | Webview-specific, unchanged. |
| `HtmlRenderAdapter.RenderWayfinding` | The wrapper emitter. Its empty-pager branch is byte-identical to `RenderBreadcrumb` and stays. |

**No new `HostRenderException` was registered.** `WebviewRenderAdapterTests` still asserts exactly 5 webview entries, `RenderSpaParityTests` exactly 1, `RenderParityTests` zero `html`, `RenderSectionParityTests` never a `section.*` — all green.

#### Known issue, pre-existing and NOT fixed here

`npm run check:ir-content` **fails** with `+5 rule(s)`: `.ir-content .impact-file-list`, `… li`, `.code-areas-panel .impact-file-list`, `.impact-more`, `.impact-map-link`. Verified **identical against the baseline binary**, so it predates this story — the committed layer was extracted before the `--deep-git` impact widgets existed. Deliberately left unfixed: running `extract:ir-content` here would commit unrelated drift under this story's name. Worth its own follow-up.

### File List

- `src/SpecScribe/SiteGenerator.cs` — the seam (`BuildSurfacePrelude`, `BuildFamilySurfaces`, `CapturedRegions`, `BuildOutline`, records `SurfacePrelude`/`FamilySurface`/`CapturedRegion`); `BuildSpaBundle` + `RenderWebviewSurfaces` reduced to projections; `ResolveFollowUpDocPairs`; `ResolveDeferredModel` href-map fix
- `src/SpecScribe/SpaDelivery.cs` — `ExtractContentRegion` two-marker slice; `SchemaVersion` 1 → 2 + compatibility note
- `src/SpecScribe/FollowUpRefs.cs` — `BuildHrefMap` pair-based overload
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — `EveryIrRegion_HasOneBalancedWayfindingBand_AndExactlyOneMainLandmark` + `CountOccurrences`
- `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` — `EveryWebviewSurface_IncludingCapturedOnes_CarriesNoScript`
- `web/ir/adapter.ts` — repair + throw deleted; `EXPECTED_SCHEMA_VERSION` 2; header/comment updates
- `web/ir/adapter.client.ts` — `EXPECTED_SCHEMA_VERSION` 2
- `web/ir/types.ts` — `IrRegion.wayfindingRepaired` removed
- `web/test/region-split.test.ts` — fixtures collapsed to one shape; both-markers-after-`<main>` case added
- `docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md` — **new**
- `docs/adrs/README.md` — ADR 0024 index entry
- `docs/adrs/0005-vs-code-webview-runtime-and-packaging.md` — **Refined by** cross-reference
- `docs/adrs/0008-json-ir-canonical-and-incremental-generation.md` — **Refined by** cross-reference
- `docs/adrs/0016-ir-carries-rendered-prose-html.md` — **Relates to** cross-reference
- `_bmad-output/planning-artifacts/epics.md` — delivery record under § Story 22.4 (Task 10)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — status + findings (Task 10)
- `.claude/launch.json` — `static-jsoff-22-4`, `nuxt-jsoff-22-4` (AC #8)

## Change Log

| Date | Note |
|---|---|
| 2026-07-28 | **Implemented (dev-story); status -> review.** All 9 ACs met. **One seam = 3 members** (`BuildSurfacePrelude` + `BuildFamilySurfaces` + `CapturedRegions`, plus `BuildOutline`); both surfaces are filters over it, and the webview keeps exactly five surface-specific behaviours (exclusions, degrade skip, island strip, SourcePath join, WrapDocument) plus its outline. **Measurement method is load-bearing:** parity was measured against a purpose-built BASELINE BINARY (HEAD versions of this story’s 3 files restored into a copy of the live tree), because the naive before/after straddled a concurrent session — a control run proved the volatility floor was "whatever another session touched". ⚠️ The first diff reported a **vacuous** "822/822 identical": the webview payload’s surface field is `content`, not `contentHtml`, so it compared `undefined` to `undefined`. Caught only because it contradicted an independent measurement. **AC #2:** 828/828 surfaces, identical set + emission order, byte-identical `entryDocument`, 0 title/sourcePath diffs; all 45 content movers attributed (33 wrapper-only +30 B, 10 work-graph, 2 harness artifacts). **AC #4:** unbalanced IR regions **594 -> 0** (epics.md’s "~853" over-counted — pagerless pages were already balanced); wrapper regions 189 -> 783; every mover exactly +30 B. Consumer-side repair + throw deleted. **AC #5 root cause was narrower than the story framed it:** not pipeline ordering generally, but `ResolveDeferredModel` handing an EMPTY `_docs` to `FollowUpRefs.BuildHrefMap`, nulling every spec-resolver href so `WorkGraph.BuildStory` dropped the resolver node AND its edge. Fixed with a source-derived href-map overload — **no `_docs` pre-population**, so Trap 2’s `alreadyExisted` flip never fires (`updated=0` every run); moving `RenderEpicsPages` was considered and rejected (it reorders diagnostics). Static-vs-IR `<main>` divergence **47 -> 0** over 1,406 pages; `measure:parity` 191/191 on all four axes. ⚠️ **The fix reaches further than 23.3 reported** — 9 EPIC pages + `work-graph.html` carried the same defect at epic scope (Epic 1: 13/12 -> 16/20). **AC #6:** schemaVersion **1 -> 2**, both consumer constants moved. **AC #7:** ⚠️ **golden fingerprint did NOT move** — AC #4 cannot touch it (fixture runs without `--spa`) and AC #5 is legitimately zero-delta there (no spec-doc resolver in that fixture’s deferred work). Live delta enumerated instead: 1,350/1,407 byte-identical; 57 movers = 46 story + 9 epic + `work-graph.html` + `app.html` (asset-version token). ⚠️ Constant is now **06788c0f…**; the story’s `3171cf5c…` was already stale — third story running to record a stale value. **AC #8:** JS-off verified live under CSP `script-src none` on static AND Nuxt output; `<main>` is a direct child of the shell (not nested in the band), band bottom == main top exactly, and both surfaces now report the same work-graph label live. **AC #9:** ADR **0024** (not the predicted 0023 — a concurrent session took and ratified 0023 the same day), cross-referenced from README + ADR 0005/0008/0016. ⚠️ **Trap 1 was already fixed** in `811ba17`, after this story’s baseline, in the recommended strip-on-both direction; now asserted over the WHOLE surface set. **Trap 4** resolved by giving the SPA the webview’s try/catch. ⚠️ **Pre-existing, not fixed here:** `npm run check:ir-content` fails with +5 `impact-*` rules — identical on the baseline binary, so the committed layer predates the `--deep-git` impact widgets. |
| 2026-07-27 | Story created (baseline `6017c2c`). Its **9 ACs supersede epics.md's 3**; Task 10 records that drift. **epics.md AC #1 found already satisfied and near-vacuous** — `spa/` IS the IR (ADR 0016 / Story 22.2 promoted it in place), so the SPA is already an IR consumer; the real duplication is that `BuildSpaBundle` and `RenderWebviewSurfaces` are two ~200-line builders sharing an identical prelude, an identical epics-family iteration and an identical captured-region loop. **Owner locked three decisions:** D1 one region seam + both inherited defects (slicers survive — they remain the IR's producer for ~853 pages until 23.4); D2 **22.4 runs before 23.4**, so 23.4 inherits one region producer and its circularity is answered in advance; D3 the **static** page moves to converge the 46-delta, honouring 23.3's measurement that the IR is the more complete render. Both inherited defects re-root-caused at `6017c2c` (22.3's line numbers were ~40 lines stale within a day): the 46-delta is `RenderEpicsPages` at `:365` building geometry from `ResolveFollowUpWork(files)` because `_docs` is empty until `:3289`, vs `BuildSpaBundle` at `:3109` building it from `_docs.Values`; the two-region-shapes defect is `RenderWayfinding` emitting a `page-wayfinding` wrapper only when a pager renders, while `ExtractContentRegion` slices from the inner breadcrumb — a **one-marker emitter fix**, not a full inversion. New findings recorded as traps: the **captured-webview JSON-island divergence** (`impact-map.html` ships an island the registered `data-island` exception says it should not; invisible because the no-script test runs without capture), the **`alreadyExisted` flip** that turns every `Generated` diagnostic into `Updated` on any `_docs` pre-population and would move the fingerprint for the wrong reason, the **`ReferenceEquals` degrade contract** a shared loop silently breaks, and the **`try/catch` asymmetry** between the two builders' story loops. Golden fingerprint re-read from source: `3171cf5c…` (moved under Story 20.8), and the golden fixture generates **without `--spa`**, so the AC #4 region change cannot move it — only AC #5 can. |
