---
title: 'Webview Doc-Page Surfaces — Header Nav Links Work In-Editor'
type: 'feature'
created: '2026-07-12'
status: 'done'
review_loop_iteration: 0
baseline_commit: '279d27e230ff391ce9f73bb1e438ca24775ceaa4'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In the VS Code panel, header nav links (Readme, GDD, Narrative, Game Architecture, ADRs, Requirements, Structure…) dead-end in an "isn't part of the in-editor status view" toast — the webview bundle only ships the dashboard + epics families. Owner re-confirmed after F5 (ADRs does nothing useful).

**Approach:** Reuse Story 6.7's sanctioned non-scraping seam: enable the `WriteOutput` page capture during `specscribe webview` runs, then append every captured non-family page as a `WebviewSurface` — content region sliced by `SpaDelivery.ExtractContentRegion` (fresh per-page nav markup + captured breadcrumb/`<main>`), title via `ExtractTitle`. Exclude `code/**` (Story 7.1's tree scales with the target repo; 7.2 citations already use `revealSource` in the webview). The shim already navigates to any bundled key — only its rejection-toast copy changes.

## Boundaries & Constraints

**Always:**
- Non-scraping capture only (AD-1/AD-2): consume the render pipeline's own output at the write seam; never read generated `.html` back from disk, never re-parse `.md`.
- Generated static site stays byte-identical — capture is memory-only; `WriteOutput` bytes unchanged (golden gate).
- Dashboard/epics families keep their view-model render path (strongest parity); captured pages must never shadow a family surface key.
- Exclude `code/**` from the bundle; the shim's informative toast remains the fallback for excluded/unknown targets.
- `sourcePath` (reveal-source) for captured surfaces: repo-relative source `.md` where cheaply derivable (`_docs`, `_adrs` caches); null otherwise (button hidden) — same convention, no path literals host-side.
- Additive payload: no key/shape changes; an older shim just gains navigable keys.

**Ask First:** capping or chunking the payload if measured size on this repo exceeds ~15 MB; including per-commit/day drill pages if any turn out to be separate captured files.

**Never:** re-render doc/ADR/requirements pages through new PageView builders (that's a different, bigger refactor); change nav markup or `RenderNavMarkup` (Story 10.1 owns nav re-architecture); touch the SPA delivery outputs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Nav link click | Readme/GDD/ADRs/Requirements in header | Surface swaps in place; active nav + breadcrumb correct | N/A |
| Drill from index | ADR row on adrs/index.html | ADR detail surface renders (relative href resolves) | N/A |
| Code-page link | href under code/** | Informative toast (updated copy), no dead swap | toast |
| Page missing `<main>` landmark | malformed/legacy page | Degrades to nav-only region (6.7 fallback), still navigable | N/A |
| Doc removed mid-watch | stale surface key clicked after refresh | Existing `push` fallback → entry surface | N/A |
| Older shim + new payload | pre-B extension | Extra surface keys unused, no break | N/A |
| SPA + webview same process | EmitSpa true | Capture shared, both outputs correct, no double-write | N/A |
| Deep-git off (webview default) | no git-insights page | Nothing captured for it; no dangling nav target beyond today's | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/SiteGenerator.cs:1279` -- `WriteOutput` + `_spaCapture` (gated on `EmitSpa`): generalize gate so webview runs capture too (public `CapturePages` toggle set before `GenerateAll`)
- `src/SpecScribe/SiteGenerator.cs:1306` -- `BuildSpaBundle` step 2 = the exact loop to mirror in `RenderWebviewSurfaces` (familyPaths skip, per-page `RenderNavMarkup(nav.ToNavigationView(path))`, `ExtractContentRegion`/`ExtractTitle`)
- `src/SpecScribe/SiteGenerator.cs:~850-927` -- `RenderWebviewSurfaces`: append captured surfaces after the family loop; surface keys = normalized output-relative paths
- `src/SpecScribe/SpaDelivery.cs:57` -- `ExtractContentRegion` (nav+breadcrumb+main slice, landmark fallback) — reuse as-is
- `src/SpecScribe/Commands.cs:53` -- `WebviewCommand.Execute`: enable capture on the generator before `GenerateAll`
- `extension/src/extension.ts:~370` -- the `navigate` rejection message ("dashboard + epics only") — reword for the broadened set
- `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` -- webview bundle suite to extend

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/SiteGenerator.cs` -- add `CapturePages` toggle; `WriteOutput` captures when `EmitSpa || CapturePages` (one shared dictionary) -- capture without SPA emission
- [x] `src/SpecScribe/SiteGenerator.cs` -- in `RenderWebviewSurfaces`, append captured non-family pages as `WebviewSurface`s (skip family keys + `code/` prefix; region via `ExtractContentRegion` with fresh per-page nav; title via `ExtractTitle`; `sourcePath` from `_docs`/`_adrs` where derivable, repo-relative, else null) -- the feature
- [x] `src/SpecScribe/Commands.cs` -- `WebviewCommand` sets `CapturePages = true` before `GenerateAll` -- turns it on for the panel only
- [x] `extension/src/extension.ts` -- reword the rejection toast (no longer "dashboard + epics"; now "not available in the editor view — e.g. code pages; run specscribe generate to browse the full site") -- honest fallback copy
- [x] `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` -- new tests: bundle contains `readme.html` (or fixture equivalent), `adrs/index.html`, `requirements.html`, and a planning doc page; every nav item href resolves to a bundled surface key; `code/**` absent; captured doc surface region starts with nav markup and contains the page's `<main>` content; a doc-backed surface carries its repo-relative `sourcePath` -- pins the contract
- [x] `tests/SpecScribe.Tests` -- assert golden fingerprint unchanged (run before/after; the known pre-existing failure hash must be identical) -- byte-identity proof

**Acceptance Criteria:**
- Given the panel is open, when a header nav link (Readme, ADRs, Requirements, GDD-class doc) is clicked, then the surface swaps in place with correct active-nav/breadcrumb — no toast.
- Given adrs/index.html is shown, when an ADR row is clicked, then that ADR's page renders in the panel.
- Given a link into `code/**`, when clicked in the panel, then the updated informative toast appears (no dead swap, no bundle bloat).
- Given `dotnet test` before/after, then no NEW failures and the golden failure hash is identical (site bytes untouched).
- Given the payload for this repo, then its size is measured and reported in the completion summary (Ask First threshold ~15 MB).

## Spec Change Log

- **2026-07-12 (review checkpoint — Ask-First fired + owner decisions).** The frozen Ask-First clause ("per-commit/day drill pages if any turn out to be separate captured files") triggered: `commits/*.html` day pages ARE captured files and Story 7.5 (concurrent) adds `commit/*.html`. **Owner decided: exclude both** (alongside `code/**`, now matched as the exact `_codePages`/`_commitDays` sets rather than a path prefix, so a doc folder literally named `code/` still surfaces). **Owner also renegotiated the "byte-identical" Always for one case:** a repo whose ADR root lacks a README now gets a synthesized `adrs/index.html` landing (the nav linked a page that was never generated — a pre-existing 404 this spec surfaced); repos WITH a README stay byte-identical, so the golden gate is unaffected. Review patches applied without loopback: landmark-less captured pages are skipped (honest toast instead of a silent blank surface), `CapturePages`-after-`GenerateAll` throws, `BuildCapturedSourceMap` refuses root-escaping paths and maps `readme.html`, rejection-toast copy de-overpromised, payload serializer uses `UnsafeRelaxedJsonEscaping` (10.65 → 7.80 MB with the exclusions), divergence registry's mermaid entry widened to captured surfaces, tests tightened to the shim's case-sensitive key semantics + subdir href resolution + landing synthesis + readme sourcePath. KEEP: the 6.7 capture-seam approach, the family-surfaces-first ordering, and the exact-set exclusion pattern.

## Verification

**Commands:**
- `dotnet test` (repo root) -- expected: green except the known pre-existing `GoldenContentFingerprint` failure with an UNCHANGED expected-hash message
- `npm run typecheck` && `npm run build` (in `extension/`) -- expected: clean
- `dotnet run --project src/SpecScribe -- webview > NUL`-style size probe (or serialize in a test) -- expected: payload size reported, well under the Ask-First threshold

**Manual checks (if no CLI):**
- F5 dev host on a real project: click every header nav item — each swaps in place; ADR drill works; a code citation still opens the real file via reveal; excluded targets toast with the new copy.

## Suggested Review Order

**The capture switch — pages into memory without SPA emission**

- Entry point: the opt-in toggle riding the 6.7 write-seam capture; memory-only, written bytes unchanged.
  [`SiteGenerator.cs:94`](../../src/SpecScribe/SiteGenerator.cs#L94)

- The webview command turns it on — the only caller.
  [`Commands.cs:72`](../../src/SpecScribe/Commands.cs#L72)

**The surface append — the feature itself**

- Captured-page loop: family skip, owner-decided exclusions (exact code/commit-day sets + commit/ prefix), landmark-less skip (toast beats a blank surface).
  [`SiteGenerator.cs:1428`](../../src/SpecScribe/SiteGenerator.cs#L1428)

- Reveal-source mapping for docs/ADRs/readme; refuses root-escaping paths (button hides instead).
  [`SiteGenerator.cs:1490`](../../src/SpecScribe/SiteGenerator.cs#L1490)

**The ADR landing synthesis (owner-directed scope addition)**

- README-less ADR roots get a generated landing so the nav link can never 404 — byte-identical when a README exists.
  [`SiteGenerator.cs:664`](../../src/SpecScribe/SiteGenerator.cs#L664)

**Payload & host chrome**

- Relaxed JSON escaping — the payload is HTML-dominated; 10.65 → 7.80 MB combined with the exclusions.
  [`Commands.cs:58`](../../src/SpecScribe/Commands.cs#L58)

- Honest rejection-toast copy (no longer claims code links open files).
  [`extension.ts:395`](../../extension/src/extension.ts#L395)

- Divergence registry: mermaid/script degrade now covers all captured surfaces.
  [`HostRenderException.cs:35`](../../src/SpecScribe/HostRenderException.cs#L35)

**Peripherals — tests**

- Long-tail surfaces present + every entry-nav href resolves (case-sensitive, like the shim's lookup).
  [`SiteGeneratorWebviewTests.cs:362`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L362)

- Subdirectory surface hrefs resolve through the bridge's dot-segment collapse.
  [`SiteGeneratorWebviewTests.cs:400`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L400)

- Landing synthesis on a README-less ADR root.
  [`SiteGeneratorWebviewTests.cs:437`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L437)

- Code pages generated into the site but excluded from the bundle.
  [`SiteGeneratorWebviewTests.cs:470`](../../tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs#L470)
