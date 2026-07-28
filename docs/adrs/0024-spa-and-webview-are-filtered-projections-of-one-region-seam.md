# ADR 0024: The SPA and the VS Code Webview Are Filtered Projections of ONE Region Seam

**Status:** Proposed (authored 2026-07-28 by Story 22.4; ratification is the owner's)
**Date:** 2026-07-28
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0008 — JSON IR Canonical and Incremental Generation](0008-json-ir-canonical-and-incremental-generation.md) (§Decision 2 called static HTML, SPA and webview *co-equal projections*; this record makes that true rather than aspirational); [ADR 0016 — The Canonical IR Carries Rendered Prose HTML](0016-ir-carries-rendered-prose-html.md) (§Decision 4 assigned "retiring a now-duplicate data path" to Story 22.4 — this is that call, exercised; §Decision 5's `schemaVersion` trigger fires here); [ADR 0005 — VS Code Webview Runtime and Packaging](0005-vs-code-webview-runtime-and-packaging.md) (the webview seam this reshapes without changing its contract); [ADR 0017 — Projection Routes Mirror IR Paths](0017-projection-routes-mirror-ir-paths.md) (§Decision 2's no-href-rewriting rule, still intact); [ADR 0013 — The Text Twin Is the No-JS Contract](0013-text-twin-is-the-no-js-contract.md) (§3's live-browser gate, exercised); Epic 22 (Story 22.4), Epic 23 (Story 23.4)

## Context

ADR 0008 §Decision 2 described static HTML, the SPA and the VS Code webview as **co-equal projections** of one canonical IR. That was the intent. It was not what the code did.

`BuildSpaBundle` and `RenderWebviewSurfaces` were two ~200-line builders in `SiteGenerator.cs` that each, independently:

- built the same prelude — `_docs` → `WorkInventory.Build` → `ProjectCounts.Build` → `BuildFollowUpGeometry` → `UnplannedWorkGeometry.From` → `HtmlTemplater.BuildIndexPage`, same arguments;
- iterated the epics family with the same models, the same retro map, the same pagers, the same placeholder rule and the same fragment pipeline;
- sliced captured pages with the same `CapturedNavMarkup` + `SpaDelivery.ExtractContentRegion` pair.

The webview was therefore not a projection of the IR. It was a **rival builder** that happened to agree, and had to be kept agreeing by hand. The cost was already being paid: Story 22.2's fix for the 5-anchor `codeItemHref` drift had to be applied **twice**, in both files, and both call sites carried near-identical apology comments explaining why. Two of the three defects this story inherited existed only because two paths could disagree.

Three further facts made "leave it as two" untenable:

1. **The two loops were not actually identical.** The webview wrapped its `BuildStoryPageFragments` call in a `catch (IOException or UnauthorizedAccessException)` that degrades one story to a placeholder; the SPA had no catch. Nobody decided that — it accreted.
2. **A behaviour existed on one path and not the other.** `WebviewRenderAdapter.RenderContent` strips inert `<script type="application/json">` islands, and the registered `data-island` host exception says the webview carries none — but the *captured* path never called `RenderContent`, so the strip was family-only. `impact-map.html` (reachable only under `--deep-git`) shipped a `HierarchyExplorer` island the contract claimed it did not carry, invisibly, because the no-script test ran on a generator with no capture.
3. **The IR carried two different region shapes.** `ExtractContentRegion` sliced from the inner `<div class="breadcrumb">` even when a page's pager had put that breadcrumb inside a `<div class="page-wayfinding">` wrapper, so those regions carried the wrapper's closing tag without its opener — **594 of this repo's 1,400 IR pages**, element-unbalanced. The consumer compensated with a repair-and-throw in `web/ir/adapter.ts`, whose own comment already said the emitter should slice from the wrapper.

Story 23.4 deliberately **keeps** one C# region-composition path (nav + wayfinding + `<main>`) feeding the IR and both consumers, because that path is what the IR is built from. So the question this record answers is not "delete or keep" — it is **how many** of that path there are, and what a consumer is permitted to be.

## Decision

**1. There is exactly ONE region seam, and both the SPA and the webview are filtered projections of it.** Three members in `SiteGenerator` constitute it:

- `BuildSurfacePrelude` — the shared `docs → work → counts → followUps → unplanned → dashboardPage` state, built once per bundle;
- `BuildFamilySurfaces` — the dashboard + epics-family `PageView` sequence, in emission order, carrying the reference-linkify skip keys and the identity the webview's outline tree needs;
- `CapturedRegions` — one `(path, title, region, breadcrumb, metaDescription, degraded)` record per captured page.

Neither consumer may rebuild any of these. A surface that needs different data asks the seam for it.

**2. A consumer may FILTER and POST-PROCESS; it may not re-derive.** The webview's entire remaining surface-specific behaviour is, exhaustively: its exclusion set (code pages, commit-day pages, the `commit/` prefix), its degrade skip, its JSON-island strip, its `SourcePath` join, and its `WrapDocument` entry document. The SPA consumes the same sequence unfiltered. Anything a consumer needs beyond that is a signal the seam is wrong, not a licence to fork it.

**3. The SPA/webview asymmetry on degraded pages is deliberate and preserved.** A page whose region degraded to nav-only is **kept** by the SPA and **dropped** by the webview: a browser tab is escapable, a status panel claiming "links work" is not. The degrade is signalled by a `Degraded` flag computed **at the point of slicing**, inside the seam — not re-derived by consumers. It was previously detected by a `ReferenceEquals` comparison against the nav-markup instance, which any consumer that copied or re-concatenated the region would have silently broken, with no test failing and a content-empty surface shipping.

**4. The webview is NOT a Nuxt consumer.** It consumes the C# region seam directly and continues to (ADR 0005's runtime contract is unchanged, and Story 23.4 AC #3 keeps this path). Nothing in Epic 23's projection layer sits between the seam and the webview.

**5. Divergences between projections are decided, recorded, and tested — never accreted.** Two were settled here rather than left to fall out of the refactor:

- **The JSON-island strip applies to both** family and captured surfaces. The webview ships no `specscribe.js`, so an inline island is data it can never read; the registered `data-island` exception already described the webview as carrying none. This is now asserted over the **whole** surface set, not per named page.
- **The story-fragment `try/catch` applies to both.** The SPA gains the webview's resilience, because the alternative is worse: without it, an artifact deleted or ACL-denied mid-render aborts the entire SPA emit, where the HTML path already degrades to a placeholder. It can only change behaviour in a case that previously threw.

**6. The IR has ONE region shape, fixed at the emitter.** `ExtractContentRegion` slices from the wayfinding band's **outermost** marker — `<div class="page-wayfinding"` when present, else `<div class="breadcrumb"`, taking the earliest candidate that precedes `<main>`. Every emitted region is element-balanced and carries exactly one `<main id="main-content">`. The consumer-side repair and its "cannot balance" throw are **deleted**: a repair that can no longer fire is a second, drifting truth about a boundary the emitter owns. Only the slice's **start** moved; the end is still `</main>`, which `HtmlTemplater`'s section-nav placement depends on.

**7. That is a `schemaVersion` bump, per ADR 0016 §Decision 5.** `SpaDelivery.SchemaVersion` goes **1 → 2**, and both consumer constants (`web/ir/adapter.ts`, `web/ir/adapter.client.ts`) move in the same change — the adapter only warns on mismatch, so a missed one is silent.

## Consequences

**Good.**

- A change to how a surface is composed is made **once**. The class of defect that required Story 22.2's `codeItemHref` fix to be applied twice cannot recur, because there is no second place to forget.
- The webview's bundle became verifiable against the SPA's inputs by construction rather than by test. Measured: **828 of 828 surfaces**, identical set and emission order, `entryDocument` byte-identical, **0** title and `SourcePath` differences.
- One region shape means downstream consumers stop carrying compensating logic. `web/ir/adapter.ts` lost a repair branch and a throw; `web/test/region-split.test.ts`'s two fixtures collapsed to one shape.
- The invariant is now enforced where it is produced, over the whole IR rather than a sample (`EveryIrRegion_HasOneBalancedWayfindingBand_AndExactlyOneMainLandmark`), plus `npm run check:a11y`'s `one-main` / `wayfinding-single` / `wayfinding-closed` over the emitted HTML.

**Bad, and accepted.**

- `SiteGenerator.cs` gains three members and two small record types whose only purpose is to be shared. A reader looking for "where the webview is built" now finds a filter over a seam rather than a self-contained method, which is a real loss of local readability in exchange for a global guarantee.
- The seam computes a little more than either consumer strictly needs (the webview does not use `Breadcrumb` or `MetaDescription`). Paying that is the price of one producer.
- The `schemaVersion` bump makes 594 pages' `contentHash` move in one step. Anything that had cached IR content hashes across this boundary sees a full invalidation — harmless today (Stories 22.5/22.6 are not shipped), and exactly what a version bump is for.
- **Story 23.4 inherits one region producer, and must preserve it.** The slicers are not dead code: `_spaCapture`, `CapturedNavMarkup` and the `SpaDelivery.Extract*` family remain the IR's producer for ~1,200 of 1,400 pages until 23.4 replaces them. Reading ADR 0016 §Decision 4 as a mandate to delete them breaks the IR for most of the site.

**Neutral.**

- Nothing here changes the IR's path scheme, so ADR 0017 is untouched: no href inside IR content is rewritten, and the 592 inherited dangling links are still reproduced faithfully rather than patched.
- `--spa` remains opt-in; a default generation gains no cost. The webview turns capture on independently.

## Alternatives considered

**Leave two builders and add a test that they agree.** Rejected: a test can only compare what it thinks to compare. The island divergence had a registered host exception *asserting* the correct behaviour and a no-script test — and still shipped, because the test ran without capture and the exception described intent rather than code. Agreement enforced by construction is not the same claim as agreement enforced by assertion.

**Make the webview a consumer of the Nuxt projection layer.** Rejected, and explicitly so in Decision 4: it would put a Node runtime and Epic 23's build output on the VS Code extension's critical path, contradicting ADR 0005's packaging contract for no benefit the seam does not already give.

**Fix the two region shapes in the consumer instead of the emitter.** That is the status quo this replaces. The repair worked, but it meant two components had to agree about a boundary neither owned, and the failure mode was invisible: prepending a second opener to an already-balanced region nested `<main>` and `<footer>` inside the wayfinding band on 187 pages while `<main>` stayed byte-identical, so parity, link resolution and every a11y assertion passed green. It was caught only by reading real DOM geometry in a browser.

**Delete the slicers outright**, reading ADR 0016 §Decision 4's grant maximally. Rejected: they produce the IR for ~1,200 pages. Retiring the *duplicate* is this story's grant; retiring the *slice* is Story 23.4's, and only once a replacement exists.
