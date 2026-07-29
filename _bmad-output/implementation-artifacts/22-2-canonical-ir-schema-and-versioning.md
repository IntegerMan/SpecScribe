---
baseline_commit: cd7f30255bb07112332c0876f4335e6b77ca9f4d
implements_decision: docs/adrs/0008-json-ir-canonical-and-incremental-generation.md # ADR 0008 Decision #1 (IR is canonical) — this story PROPOSES an amendment to §Decision 1 (AC #7)
amends_decision: docs/adrs/0013-text-twin-is-the-no-js-contract.md # ADR 0013 §5 already amended the SVG half of ADR 0008 §Decision 1; the prose-HTML half is amended here
gated_by: 22-1-spike-incremental-recompute-and-ir-delta-transport # verdict "Proceed, RE-SCOPED"
gates: [22-3, 22-4, 22-5, 22-6, 23-2, 23-3] # 22.6 explicitly "proceed only after 22.2 delivers page-level delta addressing"
owner_decisions: 2026-07-25 # (1) promote spa/ in place, (2) per-page hash + oversized-page cap, (3) 22.2 proposes the ADR amendment
---

# Story 22.2: Canonical IR Schema + Versioning

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer building surfaces on top of a stable data contract,
I want the shipped SPA delivery form **promoted in place** into a **versioned canonical IR** — carrying rendered prose HTML, chart data + component configuration, a head/meta projection, page-local nav context, and per-page delta addressing,
So that static HTML, the SPA, the webview, and Epic 23's Nuxt front end can all project from **one durable, lossless, chunked, addressable representation** instead of three drifting capture paths.

## Why this story looks different from epics.md — READ FIRST

epics.md Story 22.2 was written **before** two spikes ran. Both re-scoped it, and the owner folded extra work in. **This story's ACs supersede the epics.md ACs**; Task 9 records that in `epics.md` so the drift is not silent.

Three things changed:

1. **The "byte-blind chunker" known constraint in epics.md AC #2 is STALE — do not implement it.** Story 22.1 measured the shipped code: `SpaDelivery.MaxChunkBytes = 2_000_000` already ships alongside `MaxPagesPerChunk = 75` ([`SpaDelivery.cs:37,56,193-199`](../../src/SpecScribe/SpaDelivery.cs)). The 112.9 MB `pages-root.json` from Story 6.6 **cannot recur**. 22.1's gate: *"The 'byte-blind chunker' known-constraint is **already fixed** — drop it. Re-aim 22.2's chunking work at **page-level (sub-chunk) delta addressing** + **capping single oversized pages**."* One real gap remains and is measured: **a single page larger than the cap gets a dedicated batch that still exceeds it (3.08 MB observed against a 2 MB guard).**

2. **Story 23.1 handed this story a HARD REQUIREMENT and a live defect.** The Nuxt spike consumed `SpaDelivery` as a **proxy IR** and proved byte-identical `<main>` parity on 3 of 4 surfaces — but *only because* that proxy carries **whole rendered HTML**. Its central finding (the ~889 LOC of custom Markdig renderers are not a fidelity risk) **does not transfer** if this story builds the IR to ADR 0008's literal wording ("view models plus pre-rendered SVG chart fragments"). It also found the capture is **lossy today**.

3. **ADR 0013 §5 (2026-07-24) already amended ADR 0008**: the IR carries chart **data + component configuration**, not pre-rendered SVG. That is a scope *reduction* — but the prose half of ADR 0008 §Decision 1 is still unamended, which is why AC #7 exists.

**The owner locked three decisions on 2026-07-25 (create-story elicitation):**

| # | Decision | Consequence |
|---|---|---|
| **D1** | **Promote `spa/` in place.** `spa/manifest.json` + `spa/pages-*.json` **become** the canonical IR — extended, not replaced. | One data path. The 5-anchor fidelity defect is fixed where it lives. **No new `ir/` directory, no rename** — renaming is 22.4's call, not this story's. |
| **D2** | **Per-page hash + oversized-page cap.** Manifest carries a stable content hash + byte size per page; a page over the cap can no longer produce an over-cap chunk. | **Addressing only.** No delta transport, no incremental engine, no client change to consume deltas. That is 22.5/22.6. |
| **D3** | **22.2 proposes the ADR amendment** for the prose-HTML requirement. | A new ADR (next free number: **0016**) amending ADR 0008 §Decision 1. Per CLAUDE.md's ADR-trigger rule, a cross-cutting contract change must not stay buried in a spike report. |

## Acceptance Criteria

**AC #1–#3 restate epics.md's three ACs under the re-scope. AC #4–#7 are the folded-in work (owner-directed 2026-07-23) and the ADR trigger.**

1. **The IR is versioned and carries rendered prose HTML, chart data + component config, and the text twin.**
   **Given** the shipped `SpaDelivery` manifest + content chunks,
   **When** they are emitted as the canonical IR,
   **Then** the manifest carries an explicit **schema version** with a documented compatibility rule,
   **And** page content travels as **Markdig-rendered prose HTML strings** (the 23.1 hard requirement — *not* re-modelled view models),
   **And** chart **data + component configuration** and the **server-rendered text twin** ride inside that content rather than being regenerated per-surface, per [ADR 0013 §5](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) — which retired the pre-rendered-SVG wording this AC previously carried.

2. **Chunking is byte-bounded with no over-cap escape hatch.**
   **Given** 22.1's measurement that a single page above `MaxChunkBytes` still produced a **3.08 MB chunk against a 2 MB guard**,
   **When** the IR is chunked,
   **Then** **no emitted content chunk exceeds the defined ceiling** — the oversized-single-page path is closed, not merely isolated,
   **And** a regression test pins the previously-escaping case at the boundary.

3. **The IR has a golden round-trip boundary.**
   **Given** the `SectionViewModelSerializationTests` round-trip pattern,
   **When** the IR manifest + chunks are round-tripped through serialize → deserialize → re-serialize,
   **Then** the result is **byte-identical**, or every difference is enumerated and justified in the test's own documentation,
   **And** this becomes the IR's golden boundary test — generalized from section view models to the whole IR document.

4. **The capture is lossless: the 5 dropped anchors are restored on BOTH affected surfaces.**
   **Given** 23.1 finding 3 — the SPA and webview captures of the dashboard git-pulse panel emit bar labels as **plain text** where the static page hyperlinks them to `code/*.html`, because both omit `codeItemHref`,
   **When** the IR is emitted,
   **Then** the dashboard IR region carries **553** `<a>` elements in `<main>`, matching the static golden (today: **548**), closing the entire **277-byte** dashboard parity delta,
   **And** the **webview** surface is fixed in the same change (23.1: *"The webview surface is affected identically — not just the SPA"*),
   **And** the static site's bytes are **unchanged** (the static path already passes `CodeItemHref`).

5. **The IR carries what the front end asked for: head projection, page-local nav context, and script-island declarations.**
   **Given** 23.1's stated input to this story,
   **When** a consumer reads a page from the IR,
   **Then** it gets a structured **head/meta projection** (title + description) without re-deriving it,
   **And** the page's **page-local nav context band** (`site-nav-local-context`) is preserved rather than replaced by the generic key-views nav — the difference 23.1 enumerated on the ADR surface,
   **And** the page **declares every embedded `<script>` a consumer must strip or nonce** — covering both the `application/json` data islands *and* the executable inline handshake script (see Dev Notes: the webview's existing regex only strips the former).

6. **Page-level delta addressing, without a delta channel.**
   **Given** 22.1 measured a one-line edit re-shipping **39.9 % of a 48 MB IR** at chunk granularity,
   **When** the IR is emitted,
   **Then** the manifest carries, per page, a **stable content hash** and **byte size**, so 22.5/22.6 can diff at page granularity without re-shipping chunks,
   **And** the hash is **deterministic across two consecutive runs of unchanged input** (proved by repeated runs, not asserted once),
   **And** **no delta transport, no incremental route change, and no client consumption of the hash ships in this story.**

7. **The ADR trigger is discharged.**
   **Given** ADR 0008 §Decision 1 defines the IR as "AD-2's host-neutral view models plus pre-rendered SVG chart fragments",
   **When** this story ships an IR carrying **rendered prose HTML strings** (and, per ADR 0013 §5, chart data rather than SVG),
   **Then** a new ADR (**0016**) is authored proposing the amendment to ADR 0008 §Decision 1, stating what the IR carries and why the 23.1 renderer-fidelity finding depends on it,
   **And** `docs/adrs/README.md`, ADR 0008's reference list, and `epics.md` cross-reference it.

## Tasks / Subtasks

- [x] **Task 1 — Branch, baseline, and read-before-edit** (AC: all)
  - [x] Work on a branch or worktree, not directly on `main` (`main` has a background auto-committer and concurrent sessions — memory: [[shared-main-concurrent-edit-loss-verify-after-edit]], [[worktree-edits-must-target-worktree-path]]). If in a worktree, resolve every relative path against the **worktree** root.
  - [x] **Grep-verify every line reference in this file before trusting it.** They were verified at `cd7f302` but `SiteGenerator.cs` is 4,000+ lines and moves. Verify by **symbol**, not line number.
  - [x] Read completely before editing: [`SpaDelivery.cs`](../../src/SpecScribe/SpaDelivery.cs), [`SpaBundle.cs`](../../src/SpecScribe/SpaBundle.cs), [`JsonSpaRenderAdapter.cs`](../../src/SpecScribe/JsonSpaRenderAdapter.cs), `SiteGenerator.BuildSpaBundle` / `AddSpaSurface` / `EmitSpaSite`, and [`WebviewRenderAdapter.RenderContent`](../../src/SpecScribe/WebviewRenderAdapter.cs).
  - [x] Record the **pre-change** `GoldenContentFingerprint` value and a full `dotnet test SpecScribe.slnx -c Release` baseline (pass/fail/skip counts) before touching anything — you need it to prove AC #4's "static bytes unchanged".

- [x] **Task 2 — Fix the lossy capture: `codeItemHref` on the SPA and webview dashboard** (AC: #4)
  - [x] `SiteGenerator.BuildSpaBundle` calls `HtmlTemplater.BuildIndexPage(...)` with named arguments starting at `counts:`, **skipping the positional `codeItemHref`** (≈`SiteGenerator.cs:3051`). The webview path does the identical thing (≈`:2784`). The static path passes `CodeItemHref` (≈`:3226`, via `HtmlTemplater.RenderIndex`). **Pass `CodeItemHref` at both omitting sites.**
  - [x] Confirm the mechanism end-to-end before and after: `HtmlTemplater.BuildIndexPage` → `HtmlRenderAdapter.RenderDashboardBody(view, codeItemHref, …)` → `Charts.GitPulsePanel(pulse, codeItemHref, …)` → `Charts.CodeItemLink(path, fileHref)`. With a null `fileHref`, `CodeItemLink` degrades the bar label to plain text.
  - [x] Assert the anchor count in `<main>`: **548 → 553** on the dashboard IR region, matching the static page.
  - [x] ⚠️ **Webview link-target hazard — check this, do not assume.** `CodeItemHref` can return (a) a rendered artifact page, (b) a `code/…html` page, or (c) an **external** `BuildExternalSourceUrl` link for an on-disk file with no code page. The webview bridge resolves relative links against `data-path` and is read-only. Verify a restored link resolves in the webview (the whole site is captured, so `code/*.html` should exist) and that an **external** href behaves acceptably there. If it does not, gate the webview's resolver to in-portal hrefs only and **register the divergence in `HostRenderExceptions`** — an unregistered divergence is a bug by house rule.
  - [x] Prove the static site is byte-unchanged: `GoldenContentFingerprint` must equal the Task 1 baseline.

- [x] **Task 3 — Preserve page-local nav context on captured pages** (AC: #5)
  - [x] Root cause (verified): family pages go through `AddSpaSurface` → `JsonSpaRenderAdapter.RenderContent(page)` and use `page.Nav`, which **does** carry `NavLocalContext` (e.g. `EpicsTemplater` builds "Stories in this epic"). **Captured** pages go through the long-tail loop, which re-renders nav as `RenderNavMarkup(nav.ToNavigationView(normalized))` — **no local context argument** — so an ADR page loses its `aria-label="ADRs"` band and gets the generic key-views nav instead. That is exactly 23.1's enumerated difference #2.
  - [x] The local context for captured pages is built **inline at render time** and thrown away (`new NavLocalContext(…)` at ≈`SiteGenerator.cs:1123` for ADRs, `:1313` commit days, `:1566` commits, `:2095`, `:4072`; plus `SiteNav.BuildInsightsLocalContext` / `BuildDeliveryLocalContext` / `BuildSddLocalContext`, `EpicsTemplater:259`, `RequirementsTemplater:658`). **There is no path → local-context resolver**, so do not try to re-derive it from the path.
  - [x] **Recommended fix — slice the page's own nav out of the capture.** `_spaCapture` already holds the full page string the pipeline rendered (never a disk read-back — preserve that AD-1/AD-2 boundary). The nav is a contiguous `<nav class="site-nav"` … first following `</nav>` block, and the inline `NavToggleScript` follows it immediately and must be excluded (the client owns the toggle via delegation). This is byte-faithful and needs no plumbing — the same discipline `ExtractContentRegion` and `ExtractBreadcrumb` already use.
  - [x] Alternative if slicing proves fragile: thread the `NavigationView` (or `NavLocalContext`) into `_spaCapture` at each render site. **More faithful but ~8 call sites of plumbing** — take it only if the slice can't be made robust, and say so in Completion Notes.
  - [x] Whichever route: a captured ADR page's IR region must contain `site-nav-local-context` with the correct `aria-label`, and a page that genuinely has no local context must be **unchanged**.

- [x] **Task 4 — Head/meta projection** (AC: #5)
  - [x] Add a structured head projection per page to the manifest. **Minimum viable and non-redundant: `title` + `description`.** Document the derivation rule the static site already uses in `PathUtil.RenderHeadOpen`: `description` falls back to `title` when absent; `og:title` mirrors `title`; `og:description` mirrors `description`; `og:type` is the constant `"website"`; the favicon is a constant data-URI. A consumer then reproduces the full head without the IR shipping four near-duplicate strings per page.
  - [x] Source it correctly per page class: family pages already carry `PageView.MetaDescription` (nullable, falls back to `Title`). Captured pages need the same extraction discipline as `SpaDelivery.ExtractTitle` — pull `<meta name="description" content="…">` from the captured string and HTML-decode it.
  - [x] Do **not** carry the `?v={AssetVersion}` cache-bust in the IR head projection: it is a build token, it is already exposed via `PathUtil.CurrentAssetVersion` / the shell's `data-asset-version`, and putting it in per-page data would make every page's bytes churn on every build.

- [x] **Task 5 — Declare the embedded script islands** (AC: #5, #1)
  - [x] Emit, per page, a declaration of the embedded scripts a consumer must strip or nonce. **Two distinct kinds exist today — the current webview handling covers only the first:**
    - **Inert JSON data islands** — `<script type="application/json" id="sunburst-explorer-data">` ([`SunburstExplorer.cs:62,269`](../../src/SpecScribe/SunburstExplorer.cs)) and `<script type="application/json" class="ss-hierarchy-data" id="{domId}-data">` ([`HierarchyExplorer.cs:425`](../../src/SpecScribe/HierarchyExplorer.cs)). The dashboard's island alone is **20,915 B** (23.1 Axis 3). `WebviewRenderAdapter` strips these by regex today.
    - **An executable inline script** — the Story 20.5 JS-present handshake at [`HierarchyExplorer.cs:349`](../../src/SpecScribe/HierarchyExplorer.cs), which is a bare `<script>`, **not** `type="application/json"`. The webview's `JsonDataIsland` regex does **not** match it, so it survives into the webview region and is blocked by the CSP with no nonce. **Verify this against live output** and record what you find — if it is reaching the webview unhandled, that is a second live fidelity/CSP finding and it belongs in the declaration and in Completion Notes.
  - [x] The declaration is the ADR 0013 §5 "chart data + component configuration" hook: `HierarchyExplorer.IslandHtml` already carries the component **config** alongside the nodes. Declaring the islands makes that first-class IR metadata rather than something a consumer must regex out of an HTML string.
  - [x] Shape suggestion (dev's call, but keep it flat and JSON-trivial): per page, a list of `{ id, kind }` where `kind` distinguishes inert data from executable — that is precisely the strip-vs-nonce decision a consumer has to make.

- [x] **Task 6 — Schema version + compatibility rule** (AC: #1)
  - [x] Add a `schemaVersion` field to the manifest, backed by a named constant in `SpaDelivery` (alongside `MaxChunkBytes` / `MaxPagesPerChunk`), with a doc comment stating the rule. **Recommended: a monotonically increasing integer**, bumped on any breaking change to manifest or chunk shape; additive fields do not bump. Rationale: consumers do a single integer compare; there is no independent release cadence to justify semver.
  - [x] Set the initial value to **1** and state in the doc comment that the pre-22.2 unversioned form is version 0 by implication (there is no shipped consumer outside this repo to migrate).

- [x] **Task 7 — Byte-bounded chunking with no escape hatch** (AC: #2)
  - [x] Read `SpaDelivery.BuildDataFiles`'s batching loop and `GroupBatchState`. Today an oversized page is *isolated* into its own dedicated batch — which bounds its blast radius on neighbours but **does not bound the file**, which is why 22.1 measured a 3.08 MB chunk against a 2 MB guard.
  - [x] Close it. A page's content region is **atomic** (the existing doc comment is explicit and correct — do not split a region mid-HTML). So the ceiling must be honoured by *placement*, and an unavoidably-oversized single page must be **declared** rather than silently over-cap: record its real size in the manifest (Task 8 gives you the field) and, if the design still emits an over-cap file, state the ceiling as a *target with a declared exception* in the constant's doc comment and in Completion Notes. **Do not leave a silent over-cap** — memory: no silent caps.
  - [x] Keep the existing `MaxChunkBytes` **approximation** caveat honest: it budgets raw UTF-8 `ContentHtml` bytes, not JSON-escaped output, where `<`/`>`/`&` each balloon to 6 bytes. If you tighten the ceiling claim, tighten the measurement to match — or restate the caveat rather than quietly implying precision you don't have.
  - [x] Extend `SpaDeliveryTests` at the boundary: the existing `BuildDataFiles_IsolatesAnOversizedPage_*` trio (mid-group, first-in-group, last-in-group) is the pattern; add the over-cap case they don't currently pin.

- [x] **Task 8 — Per-page content hash + byte size** (AC: #6)
  - [x] Add `contentHash` and `bytes` to each manifest page entry. Use a deterministic, reproducible hash of the UTF-8 content region (SHA-256, hex; truncation is fine if documented). **NFR9 (reproducible CI) applies — no `Random`, no time, no machine-dependent input.**
  - [x] ⚠️ **Volatility trap — this is the one most likely to bite.** The hash inherits whatever volatility lives inside the captured region. The golden gate folds footer clock, `?v=` cache-bust, and version/build rows via `NormalizeVolatile`; those live *outside* `<main>`, but **prove it** rather than assume. If any volatile token is inside a region, the hash reports a false change on every run and is worthless to 22.5/22.6. **Confirm stability by generating twice from unchanged input and diffing the manifests** — repeated runs, not a single assertion (memory: [[golden-diff-normalization-gotchas]]).
  - [x] Record in Completion Notes which regions, if any, needed normalization and why — 22.5 will build directly on this answer.
  - [x] **Stop at addressing.** No `specscribe-spa.js` change, no delta emission, no watch-route change. 22.1's gate explicitly holds 22.6 until this lands; it does not ask this story to build it.

- [x] **Task 9 — Golden boundary test + suite** (AC: #3, and guards #1–#6)
  - [x] Generalize the `SectionViewModelSerializationTests` pattern (`AssertRoundTripsLossless`: serialize → deserialize → re-serialize → compare JSON strings, because record value-equality reference-compares collection members) up to the **whole IR document** — manifest and chunks.
  - [x] Update the existing suites that will legitimately move: `SpaDeliveryTests`, `SiteGeneratorSpaTests` (`Manifest_CarriesTheNavGraphAndPerPageBreadcrumbDrillData`, `Manifest_AndChunks_RoundTrip_EveryPageResolvesToItsRegion`, `LongTailRegion_IsTheSameCSharpRenderedContent_AsTheStaticPageMainBlock`), `SiteGeneratorWebviewTests`, `RenderSpaParityTests`.
  - [x] ⚠️ **`LongTailRegion_IsTheSameCSharpRenderedContent_AsTheStaticPageMainBlock` is the test most likely to *start passing harder* after Task 3.** If it currently passes while the nav differs, check whether it compares only the `<main>` block — and if so, say so rather than claiming it proved something it didn't (memory: a test's prose is not its coverage — grep every test docstring you touch against its actual `Assert`s).
  - [x] Full suite green: `dotnet test SpecScribe.slnx -c Release`. Last recorded green on `main` was **2394 / 0 failed / 0 skipped** (Story 25.1 CI gate) — treat that as indicative and use **your own Task 1 capture** as the authoritative baseline.

- [x] **Task 10 — ADR 0016 + artifact updates** (AC: #7)
  - [x] Author `docs/adrs/0016-<slug>.md` (0001–0015 are taken — verified). It amends **ADR 0008 §Decision 1**: the IR carries **Markdig-rendered prose HTML strings** + **chart data and component configuration** + the **server-rendered text twin** — not re-modelled view models and not pre-rendered SVG. State the consequence plainly: 23.1's finding that the ~889 LOC of custom Markdig renderers are not a fidelity risk **holds only under this amendment**; without it, that risk returns in full along with a ~4,691 LOC templater reimplementation Epic 23 assumes it can avoid.
  - [x] Follow the house ADR shape (Context / Decision / Consequences / Options considered / Ratified decisions / References) and mark it **Proposed** — ratification is the owner's, not the dev agent's.
  - [x] Cross-reference: add it to `docs/adrs/README.md`, to ADR 0008's References, and note it in the Epic 22 body.
  - [x] **`epics.md` + `sprint-status.yaml` in the same change** (CLAUDE.md rule): record that Story 22.2's ACs were re-scoped by the 22.1 gate and the 23.1 fold-in, that epics.md AC #2's byte-blind-chunker premise is stale, and update the story status. A scope change recorded in only one artifact is a drift bug.

- [x] **Task 11 — Verify in a live browser, not only in tests** (AC: #4, #5)
  - [x] Generate to `SpecScribeOutput/` (never `--output docs/live` — vestigial and gitignored) with `--spa`, and open the SPA entry shell. **CLAUDE.md § Verification:** the suite structurally cannot see what a rendered page actually does.
  - [x] Confirm on the dashboard: the git-pulse bar labels are **links** in the SPA, and they navigate.
  - [x] Confirm on an ADR page in the SPA: the page-local context band renders with the right label, not the generic key-views nav.
  - [x] Confirm no console errors after a client-side navigation (the region swap fires `specscribe:content-swapped`; new markup must not break the explorer re-init seam).

### Review Findings

**Scope note.** Reviewed via Blind Hunter + Edge Case Hunter + Acceptance Auditor, scoped to this story's own File List/symbols per CLAUDE.md. `src/SpecScribe/SiteGenerator.cs`'s raw diff (1,402 lines) is almost entirely sibling-story rework (18.4/18.5/18.6, 20.7/20.8/20.9, and especially 22.4's `BuildSpaBundle`/`RenderWebviewSurfaces` unification) — reviewed by current-state symbol read instead. The initial package also mis-labeled `SpaDelivery.cs`, `SiteGeneratorSpaTests.cs`, and `SiteGeneratorWebviewTests.cs` diffs as "clean" when they too carry sibling-story content (Story 22.4's `SchemaVersion` 1→2 bump and two-marker `ExtractContentRegion` rewrite; Story 20.7/20.9/22.4/23.2/23.3-tagged tests) — excluded from the findings below once caught by both review layers and confirmed directly against the source's own `[Story ...]` attribution comments.

- [x] [Review][Decision] Manifest per-page `bytes` field used raw UTF-8 content bytes, the same approximation Task 7 closed for the chunk ceiling — owner chose to switch it to exact JSON-encoded bytes for consistency. **Fixed**: `BuildDataFiles` now reuses the pre-encoded `EncodedPage.ValueJson` token; `ManifestOversizedPage.ChunkBytes`'s doc comment updated to state both fields are exact-encoded now, differing only in scope (whole chunk vs one page's content value); both dependent test assertions (`CanonicalIrSerializationTests.ManifestAndChunks_AgreeOnEveryPage_...`, `SiteGeneratorSpaTests.Manifest_CarriesSchemaVersion_..._AndPerPageHashAndBytes`) updated to assert against `JsonSerializer.Serialize(region)` byte count. 79/79 affected tests green. [src/SpecScribe/SpaDelivery.cs:676]

- [x] [Review][Patch] `ManifestHead` description fallback treats a whitespace-only `MetaDescription` as present (`{ Length: > 0 }` is true for `" "`), shipping blank instead of falling back to title. **Fixed**: added `!string.IsNullOrWhiteSpace(d)` to the fallback condition. [src/SpecScribe/SpaDelivery.cs:684]
- [x] [Review][Patch] `ExtractNavMarkup`'s `NavBlockRegex` matches the first literal `<nav class="site-nav">...</nav>` anywhere in the captured page with no positional anchor, unlike `ExtractContentRegion`'s deliberate anchor-before-`<main>` precedent. **Fixed**: hoisted the shared `<main id="main-content"` landmark into a new `MainLandmarkMarker` constant used by both extractors; `ExtractNavMarkup` now rejects a match that doesn't precede it. [src/SpecScribe/SpaDelivery.cs:258-292]
- [x] [Review][Patch] `CanonicalIrSerializationTests`'s "whole document, no enumerated exceptions" round-trip never exercises a populated `OversizedPages` entry — the fixture is too small to produce one, so a shape regression in that record would pass silently. **Fixed**: added `Manifest_RoundTrips_WhenAPageIsDeclaredOversized`, forcing one ADR past `MaxChunkBytes` and asserting `OversizedPages` is non-empty before the byte-identical round-trip check. [tests/SpecScribe.Tests/CanonicalIrSerializationTests.cs]
- [x] [Review][Patch] `SpaDeliveryTests` calls obsolete `string.Copy` (SYSLIB0050) to force a non-interned reference for the hash-determinism test. **Fixed**: swapped for `string.Concat(region)`. [tests/SpecScribe.Tests/SpaDeliveryTests.cs:641]
- [x] [Review][Patch] `ManifestOversizedPage.ChunkBytes` is `long` while the structurally parallel `ManifestEntry.Bytes` is `int` — same "byte count of an HTML region" concept, inconsistent width with no stated reason. **Fixed**: `ChunkBytes` changed to `int` (the value already originates from `Encoding.GetByteCount`, which returns `int` — pure widening cleanup, no functional change); the mirrored `IrOversizedPage` test model updated to match. [src/SpecScribe/SpaDelivery.cs:819]

All 5 patches + the decision-item fix verified together: 111/112 relevant tests green (1 pre-existing symlink-privilege skip), and the full suite re-run clean at 2,812 passed / 0 failed / 3 skipped (all 3 pre-existing symlink-privilege gated skips) with `GoldenContentFingerprint` unaffected.

- [x] [Review][Defer] Manifest metadata growth (`head`/`scriptIslands`/`contentHash`/`bytes` on every page, no size ceiling on `manifest.json` itself unlike the content chunks) reintroduces a smaller-scale byte-blind-payload risk; the SPA client fetches all of it while reading only `title`+`chunk` — AC #6 explicitly authorizes this addressing metadata and the scope guard stops at "addressing, no transport," so not a violation, flagged for 22.5/22.6 awareness [src/SpecScribe/SpaDelivery.cs] — deferred, pre-existing scope trade-off
- [x] [Review][Defer] Three independent hand-rolled "SHA-256 → lowercase hex → truncate" idioms now exist (`Commands.cs`, `FollowUpSlug.cs`, this story's `ContentHash`) with no shared helper [src/SpecScribe/SpaDelivery.cs:351-354] — deferred, pre-existing pattern this story added a third instance of
- [x] [Review][Defer] `ExtractMetaDescription`'s regex hardcodes meta-tag attribute order (`name="description" content="..."`), matching this file's own established `ExtractTitle`/`ExtractBreadcrumb` idiom [src/SpecScribe/SpaDelivery.cs:222-223] — deferred, shared brittleness class, not a new risk
- [x] [Review][Defer] `ExtractScriptIslands`'s attribute regexes assume double-quoted, case-matched attributes and no embedded `>` in an attribute value; verified the failure direction is always safe (defaults to the conservative "executable" classification) and the only content flowing through this path is SpecScribe's own self-generated `<script>` tags, never arbitrary markup [src/SpecScribe/SpaDelivery.cs:278-285] — deferred, narrow/negligible exposure today
- [x] [Review][Defer] Oversized-chunk declaration would report the same `ChunkBytes` once per member if the one-page-per-over-cap-chunk invariant is ever loosened — not a live bug today, a note for whoever touches that invariant next [src/SpecScribe/SpaDelivery.cs:624-638] — deferred, hypothetical on future code change

## Dev Notes

### The two gates that scope this story (read the reports, not just this summary)

- **[22-1-spike-report.md](22-1-spike-report.md)** — verdict for 22.2: **"Proceed, RE-SCOPED."** Drop the byte-blind chunker (fixed); re-aim at page-level delta addressing + capping oversized pages. Also: **22.6 is gated on this story** ("proceed only after 22.2 delivers page-level delta addressing").
- **[23-1-spike-report.md](23-1-spike-report.md)** § *Follow-ups outside this story* — the hard requirement, the live defect, and the three "what the front end wished the IR carried" items.

### What "the IR" is after this story (the mental model)

The IR is **not** a new artifact. It is `spa/manifest.json` + `spa/pages-*.json`, promoted:

```
spa/manifest.json     ← schemaVersion, siteTitle, entry, nav graph,
                        and per page: title, chunk, breadcrumb, parent, children
                        + NEW: head {title, description}, scriptIslands[], contentHash, bytes
spa/pages-<group>[-N].json  ← path → rendered content region (nav + breadcrumb + <main>)
                              byte-bounded, deterministic membership
```

The **content region is rendered HTML** and stays that way. That is the whole point of D1 and of AC #1 — 23.1 proved `v-html` round-trips it verbatim through Nuxt with **zero** re-serialization, attribute reordering, or self-closing-tag rewriting.

### Do not re-model into view models

ADR 0008 §Decision 1's literal wording ("AD-2's host-neutral view models plus pre-rendered SVG chart fragments") is the trap. Building to that letter would:
- discard the rendered prose, reviving the Markdig renderer-fidelity risk 23.1 measured away, and
- pull the ~4,691 LOC templater reimplementation into Epic 23's scope.

ADR 0013 §5 already amended the SVG half. AC #7 amends the prose half. **Build to the amendment, not the letter.**

### Architecture invariants that bound this work

| Invariant | What it forbids here |
|---|---|
| **AD-1 / AD-2** (spine) | The IR is produced from the pipeline's **own in-memory output**, never by re-reading or scraping generated `.html` from disk. `SpaDelivery.ExtractContentRegion`'s doc comment states this boundary explicitly — Task 3 must preserve it. |
| **AD-2** | Adapters translate; they do not reinterpret source artifacts. Fixing the webview means passing the resolver it should always have had, not giving the webview its own link logic. |
| **AD-8** | Interaction-state shape is canonical; **transport** is adapter-specific. Per-page hashes are *shape*; a push channel is *transport* — and it is 22.6's. |
| **NFR4** (additive) | The static site's bytes must not change. AC #4 makes that a measured claim, not an aspiration. |
| **NFR9** (reproducible CI) | The hash and chunk membership must be deterministic run-to-run and machine-to-machine. |
| **ADR 0013 §2** | The text twin is **contract**: server-rendered, complete, navigable, non-color. The IR must carry it — never strip it as "redundant with the chart data". |

### Existing machinery — extend it, do not reinvent

| Need | Already exists | Where |
|---|---|---|
| Chunk splitting with two independent triggers | `BuildDataFiles` + `GroupBatchState` | `SpaDelivery.cs:163-266` |
| Byte cap | `MaxChunkBytes = 2_000_000` | `SpaDelivery.cs:56` |
| Title extraction from a captured page | `ExtractTitle` (regex + `HtmlDecode`) | `SpaDelivery.cs:103` |
| Structured breadcrumb recovery from a capture | `ExtractBreadcrumb` | `SpaDelivery.cs:125` |
| Landmark slice | `ExtractContentRegion` | `SpaDelivery.cs:76` |
| Parent/child drill graph | `BreadcrumbTrail.ParentTarget` reuse in `BuildDataFiles` | `SpaDelivery.cs:222-229` |
| Lossless-round-trip assertion helper | `AssertRoundTripsLossless` | `tests/SpecScribe.Tests/SectionViewModelSerializationTests.cs:26` |
| Sanctioned per-surface divergence registry | `HostRenderExceptions.Registry` | used by `JsonSpaRenderAdapter` (mermaid) and `WebviewRenderAdapter` (data-island) |
| Chart data + component config, already serialized | `HierarchyExplorer.IslandHtml` | `HierarchyExplorer.cs:387` |

`ExtractTitle` / `ExtractBreadcrumb` are the **precedent for Task 4's description extraction** — same file, same idiom, same HTML-decode discipline. Don't invent a third parsing style.

### The 5-anchor defect, mechanically

```
static  : SiteGenerator ≈:3226  HtmlTemplater.RenderIndex(…, CodeItemHref, …)        → links render ✅
SPA     : SiteGenerator ≈:3051  HtmlTemplater.BuildIndexPage(…, counts: …)           → codeItemHref = null ❌
webview : SiteGenerator ≈:2784  HtmlTemplater.BuildIndexPage(…, counts: …)           → codeItemHref = null ❌
                                   ↓
                    HtmlRenderAdapter.RenderDashboardBody(view, codeItemHref, …)   (Dashboard.cs:23,152)
                                   ↓
                    Charts.GitPulsePanel(pulse, fileHref, today)                    (Charts.cs:1853)
                                   ↓
                    Charts.CodeItemLink(path, fileHref)                             (Charts.cs:2183)
                            fileHref == null  →  plain text, not <a>
```

Both omitting sites use **named arguments beginning at `counts:`**, which is exactly why the positional `codeItemHref` was silently skipped. That is a one-argument fix on each, but AC #4 also demands you *prove* the anchor count moved and the static fingerprint did not.

### Scope guard — five things this story is NOT

1. **Not the incremental engine.** `RegenerateEpics`'s 56-page no-op work-graph divergence is real, live, and 22.5's — do not fix it here.
2. **Not a delta channel.** Addressing only (D2).
3. **Not a rename.** `spa/` stays `spa/` (D1); renaming and retiring duplicate data paths is 22.4.
4. **Not static-HTML-from-the-IR.** That is 22.3.
5. **Not chart-SVG retirement.** ADR 0013's per-surface gate is owned by the Epic 20 stories; this story carries chart data *and* whatever SVG is still rendered today, unchanged.

### Previous-story intelligence (22.1, done 2026-07-24)

- Its correctness matrix ran **deep-git OFF**, so its stranded-surface list is a **lower bound**. Don't cite it as complete.
- Its per-epic work-graph item/link numbers were **derived by hand**, not emitted by the probe. Its own report says so. Don't treat them as harness output.
- Its delta figures (39.9 % / 25.3 %) were measured **only through `RegenerateEpics`**, whose no-op over-count inflates them. The byte-perfect `GenerateOne` route was never delta-measured. AC #6's per-page hashing is what lets 22.6 finally measure that cleanly — that is the actual reason this task exists.
- Suite discipline the epic has already used twice: a `GoldenContentFingerprint` mismatch is **not automatically your fault**. 23.1 hit stale-constant drift and proved it pre-existing by re-running on a clean detached worktree. Do the same before regenerating a constant, and record whose changes a regeneration sat on top of.

### Git intelligence (recent commits, `cd7f302` and back)

- The repo now has CI: Story 25.1 stood up `build-test-analyze` with SonarCloud, and the suite currently runs with **zero skips** — a skip is now a signal, not noise.
- `98a90c6` made `GoldenContentFingerprint` **portable** (fixed checkout *and* date/TZ dependence). The pinned constant is `91c3aeb4346cd2f9915254ab4ed35ddf0a651251d5a3dbbf392c743a269a950c` (`SiteGeneratorAdapterTests.cs:1040`), verified stable on Windows, `windows-latest`, and `ubuntu-latest` — the first constant in this project confirmed on more than one machine. If it moves under you, that is either your change or a concurrent session's — establish which before locking in a new value.
- `bcca682` bundled five stories in one commit. Expect that; scope any later review by **File List and symbols**, never a commit range.

### Project Structure Notes

- Production code lives in `src/SpecScribe/` (single project); tests in `tests/SpecScribe.Tests/`. `SpecScribe.slnx` has exactly two projects — nothing new joins it.
- ADRs live in `docs/adrs/` with a `README.md` index. Next free number is **0016**.
- Generate to `SpecScribeOutput/`. The SPA is opt-in via `--spa` → `ForgeOptions.EmitSpa`; leave it opt-in. Nothing in this story should add cost to a default generation.
- New manifest fields are **additive**, so a page-entry record gains properties rather than being replaced — keep `System.Text.Json` camelCase policy and the existing HTML-safe escaping (the client `JSON.parse`s; it never inlines into a `<script>`).

### References

- [epics.md § Story 22.2](../planning-artifacts/epics.md) — the three original ACs (superseded here; see Task 10).
- [Story 22.1 spike report](22-1-spike-report.md) — § *Gate for Stories 22.2–22.6*, Axis 3 (IR-delta transport, chunk sizes), the 3.08 MB over-cap measurement.
- [Story 23.1 spike report](23-1-spike-report.md) — Axis 2 (parity, the 277 B delta), finding 3 (root cause of the dropped anchors), § *Follow-ups outside this story* (the hard requirement).
- [ADR 0008](../../docs/adrs/0008-json-ir-canonical-and-incremental-generation.md) — §Decision 1 (the clause AC #7 amends), §Consequences (generalizing `SectionViewModelSerializationTests` was always the plan).
- [ADR 0013](../../docs/adrs/0013-text-twin-is-the-no-js-contract.md) — §5 (IR carries chart data + component config), §2 (text twin is contract).
- [ADR 0009](../../docs/adrs/0009-frontend-framework-for-projection-layer.md) — the Epic 23 consumer this IR must serve.
- [ARCHITECTURE-SPINE.md](../specs/spec-specscribe/ARCHITECTURE-SPINE.md) — AD-1, AD-2, AD-5, AD-8.
- [CLAUDE.md](../../CLAUDE.md) — § Concurrent work on shared `main`, § Decision records, § Verification.

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (`claude-opus-5`), `bmad-dev-story`, 2026-07-26.

### Debug Log References

| Check | Command / method | Result |
|---|---|---|
| Baseline suite (Task 1) | `dotnet test SpecScribe.slnx -c Release` at HEAD `261b300` | **2405 passed / 0 failed / 3 skipped**; `GoldenContentFingerprint` green at the pinned `2050b586…` |
| Final suite | same | **2427 passed / 0 failed / 3 skipped** (+22 tests); fingerprint **UNCHANGED** → AC #4's "static bytes unchanged" is measured, not asserted |
| The 3 skips | `PathUtilTests` ×2 + `SiteGeneratorWebviewTests` ×1, all symlink-privilege gated | Pre-existing and identical in the baseline. Note memory `story-25-1-sonarcloud-ci-seeded` records "zero skips" for the CI gate — that is **not** what a local Windows run without developer mode produces, and never was during this story. |
| Anchor parity (AC #4) | `generate --spa --deep-git`, then compared the IR/webview/static Git Pulse bars | **5 of 5** bar labels are links on all three surfaces, **3 of them `code/*.html`** — exactly 23.1's finding. Whole-`<main>` blocks byte-identical static ↔ IR. |
| Hash determinism (AC #6) | two generations from a **frozen source snapshot** into the **same** output dir | manifest **and every chunk byte-identical**. First attempt looked volatile and was not — see Completion Note 6. |
| Live browser (Task 11) | `ir-verify-22-2` preview on :8103, `app.html` | bars are links and navigate; ADR page shows the `ADRs` local-context band after a client-side swap; **0 console errors**; 0 scripts in swapped regions |

**Suite provenance, stated rather than glossed.** One intermediate full run showed **2 failures** — `FileWatcherServiceTests.BurstOfSaves_CoalescesAndLeavesCoherentOutput` and `SiteGeneratorGitInsightsTests.GenerateAll_TwoRunsProduceIdenticalHubMarkup`. Both **pass in isolation** and **did not recur** on an immediate re-run of the full suite (2427 / 0 / 3). Both are named members of the rotating file-write-contention flake Story 23.2 recorded ("one rotating contention flake per full run"), and a concurrent session was writing `Charts.cs`, `Commands.cs`, `SettingsStore.cs` and several story files throughout — see Completion Note 6. They are **not** attributed to this story's changes, and neither touches any symbol in this story's File List.

**Golden fingerprint provenance.** `GoldenContentFingerprint` was **not regenerated** — it is unchanged at the pinned `2050b586…` in both the baseline and every subsequent run, which is what makes AC #4's "the static site's bytes are unchanged" a measurement. That constant sits on top of a concurrent session's uncommitted work (it did at Story 23.2 too); nothing in this story moved it.

### Completion Notes List

**1. Both omitted `codeItemHref` sites fixed; the delta is proved, not asserted (AC #4).**
`SiteGenerator.cs:2792` (webview) and `:3062` (SPA) both called `HtmlTemplater.BuildIndexPage(…)` with named arguments starting at `counts:`, silently skipping the positional `codeItemHref`. Both now pass `CodeItemHref`. Measured live on this repo: the Git Pulse "Top changed files" list has 5 rows, and all 5 labels are now `<a>` on the static page, the IR region **and** the webview surface, 3 of them `code/*.html` — the exact shape 23.1 described.

**Honest correction to AC #4's numbers:** the AC says "553 `<a>` in `<main>`, today 548". Those were 23.1's counts at its own snapshot; this repo now renders **1679** anchors in the dashboard `<main>`, equal on both surfaces. The *5-anchor delta* is the real invariant and it is closed. Rather than pin a count that drifts with the data, the regression tests assert the **whole `<main>` block is byte-identical** between the static page and each consumer — strictly stronger, and it fails on any future argument divergence at that call site, not just this one.

**2. The webview link-target hazard is discharged with evidence — no `HostRenderExceptions` entry needed.**
The story asked to check this rather than assume. `CodeItemHref` can return an in-portal `code/*.html`, another rendered artifact page, or an external `BuildExternalSourceUrl`. Findings:
- **Absolute-scheme hrefs are already handled**: the webview bridge's click handler routes any `^[a-z][a-z0-9+.-]*:` href to `openExternal` (`WebviewRenderAdapter.cs` `DocumentTemplate`). This repo emits none from this resolver today (measured: 0 across all captured surfaces).
- **`code/*.html` links are already ubiquitous in the webview**: code pages are a deliberate exclusion from the bundle (owner decision 2026-07-12) so those clicks toast honestly — and the captured surfaces already ship **2322** such links on `code-map.html`, **464** on `git-insights.html`, **331** on `risk-quadrant.html`. The 5 restored dashboard links introduce **no new class of behaviour**, so there is no divergence to register.

**3. Nav context: the slice, not the plumbing (AC #5).**
Taken the recommended route — `SpaDelivery.ExtractNavMarkup` slices the page's own `<nav class="site-nav">…</nav>` out of `_spaCapture`, excluding the trailing `NavToggleScript`. Both capture loops (SPA and webview) go through one new helper, `SiteGenerator.CapturedNavMarkup`, which falls back to the old re-render when a page carries no nav — and returns **one instance per page**, because the webview loop's `ReferenceEquals(region, navMarkup)` degrade check depends on that. The alternative (~8 call sites of `NavLocalContext` plumbing) was not needed. Verified live: an ADR page's IR region now carries `aria-label="ADRs"` with real ADR pills; `about.html`, which genuinely has no local context, is unchanged.

**4. ⚠ The story's premise about the executable handshake script is STALE — and the real finding is a latent one.**
Task 5 said `HierarchyExplorer.cs:349` emits a bare `<script>` into the body and asked me to verify it reaches the webview. **It does not, and cannot today.** That handshake moved to `HierarchyExplorer.BootScript` and is emitted on the **chrome seam** by `HtmlRenderAdapter.Render`, precisely so the webview and SPA — which consume `PageView.BodyHtml` — never receive it. The only page hosting a hierarchy chart today is the dashboard, a **family** page rendered from `BodyHtml`. Measured across the whole real site: **0 executable scripts** in the IR and **0** in the webview bundle.

**But it is latent, not absent.** `BootScript` is emitted *between the breadcrumb and `<main>`*, and `ExtractContentRegion` slices *from the breadcrumb*. Any **captured** page (all of which have breadcrumbs) that gains `HierarchyEngineNeeded` will ship that executable script into both consumers — where the webview's `JsonDataIsland` regex cannot match it (no `type="application/json"`). **This lands the moment Story 20.7/20.9 mounts a hierarchy chart on Impact Map, Work Graph, or Code Map.** The IR's new `scriptIslands` declaration is exactly the mitigation: it classifies that case `executable`, and `SpaDeliveryTests.ExtractScriptIslands_SeparatesInertDataFromExecutableScript` pins it using the real `BootScript` shape.

**5. ⚠ SECOND live finding: the webview's `data-island` exception does not describe what the webview actually does.**
`HostRenderExceptions` states that JSON data islands "are stripped from the webview content region". That is true only of the **family** path, which goes through `WebviewRenderAdapter.RenderContent`. **Captured** surfaces are sliced with `SpaDelivery.ExtractContentRegion` and never touch that regex. Measured with `webview --deep-git`: `impact-map.html` reaches the webview carrying `<script type="application/json" id="impact-map-data">` **un-stripped**. Left unfixed **deliberately** — making the webview consume the IR's island declaration is Story 22.4's job ("SPA + webview as IR consumers"), and this story's scope guard says it is not a webview-behaviour change. The declaration this story adds is what 22.4 will strip *from*.

**6. Hash volatility: which regions needed normalization, and the two false alarms (AC #6, Task 8).**
**No region needed normalization.** Nothing was normalized, deliberately: a hash that describes anything other than the bytes that shipped would be a lie to 22.5/22.6. Two apparent failures were run down:
- **False alarm A — a concurrent session.** Two real-repo runs disagreed on 12 pages. `git status` before/after showed another session had edited `5-5-…md`, `deferred-work.md`, `Commands.cs`, `SettingsStore.cs` and added three story files *between my runs*. Real input change, not volatility. (CLAUDE.md § Concurrent work, exactly as advertised.) The clean proof used a frozen source snapshot.
- **False alarm B — my own test harness.** Generating to two *different* output dirs moved one hash: `diagnostics.html` echoes the configured output root **inside its own region**. Real, and worth knowing: **`diagnostics.html` is the one page whose content hash is output-path dependent**, so it will differ machine-to-machine even on identical input. The golden gate already folds that token; the IR deliberately does not, because the bytes really do differ. 22.5/22.6 should expect exactly one page to behave this way.
- **A genuine pre-existing trap found in passing:** on a **non-git** fixture the code map falls back to `FallbackCodeWalk`, which skips dot-dirs/`bin`/`obj`/`node_modules` but **not the output directory**. With output nested inside the repo root, run 1's generated `.html` feeds run 2's `code-map.html`. Not IR volatility and not introduced here (the real repo is a git checkout whose output dir is gitignored), but it is why the determinism test writes outside the walked tree — documented in the test.

**7. The byte ceiling is now real, with exactly one declared exception (AC #2).**
The old budget counted **raw UTF-8** content bytes while the file is written with HTML-safe JSON escaping (`<`/`>`/`&` → 6 bytes each), so the doc comment honestly called itself "an approximation, not an exact ceiling". `BuildDataFiles` now pre-encodes each page's key/value tokens **once** and budgets the exact bytes — the same tokens it then assembles the file from, so the number enforced and the bytes written cannot disagree (pinned by a test asserting the assembled chunk is byte-identical to serializing the equivalent `Dictionary<string,string>`). Consequence: a multi-page chunk can no longer overshoot. `BuildDataFiles_NoChunkExceedsTheCeiling_WhenJsonEscapingInflatesTheContent` is the regression test; it uses content that fits the ceiling raw and busts it escaped, so it fails against the old behaviour.

**The declared exception:** a page's region is atomic, so a single page whose own encoded size exceeds the cap must still be written whole. That is now **declared, never silent** — `manifest.oversizedPages` names each one with the **real byte size of the file it produced**, measured on the assembled output. On this repo it names exactly two: `code-map.html` (9,139,221 B) and `git-insights.html` (**3,107,287 B** — which is 22.1's "3.08 MB against a 2 MB guard", reproduced to the byte). The `oversizedPages` array is always emitted, empty included, so "nothing is over cap" is an assertion the IR makes rather than a silence.

**My first cut of this had an off-by-one** — it predicted the chunk size as `cost + 2`, over-stating by 1 because the sole member carries no trailing comma. Caught by my own test; fixed by measuring the assembled file instead of predicting it.

**8. Schema version + compatibility rule (AC #1, Task 6).** `SpaDelivery.SchemaVersion = 1`, a monotonically increasing integer beside `MaxChunkBytes`/`MaxPagesPerChunk`. Bumps on a breaking shape change (field removed/renamed/retyped, meaning changed, or the content-region delimitation changed); an **additive** field does not bump it. Pre-22.2 is version 0 by implication. All of 22.2's own fields are additive, which is why the client needed no change — its manifest-shape comment is updated to document the full contract and to say plainly that it reads only `title` + `chunk`.

**9. Head projection (AC #5, Task 4).** Per page, `head: { title, description }`, with the description resolved **at emit** using the same fallback-to-title rule `PathUtil.RenderHeadOpen` applies, so a consumer never reproduces the fallback. Family pages source it from `PageView.MetaDescription`; captured pages from a new `SpaDelivery.ExtractMetaDescription` that mirrors `ExtractTitle`'s idiom exactly (same regex-plus-`HtmlDecode` discipline, over the captured string, never a disk read-back). The `?v=` cache-bust is deliberately **not** carried — it is a build token already exposed via `PathUtil.CurrentAssetVersion` and `data-asset-version`, and per-page it would churn every page's bytes on every build. `head.title` duplicates the entry's own `title` by design: AC #5 asks for a structured title+description projection, and one object beats a consumer joining two fields.

**10. Round-trip boundary (AC #3, Task 9).** New `CanonicalIrSerializationTests` generalizes `AssertRoundTripsLossless` from one view-model record to the **whole IR document**. The manifest round-trips through an **independently declared typed model** (mirroring `SpaDelivery`'s private records field-for-field, so a field added on one side and not the other fails loudly), and every chunk round-trips as `Dictionary<string,string>`. Both compare **byte-for-byte**; there are **no enumerated exceptions**, and the doc comment says where one would have to be written if there ever is. A third test cross-checks that the manifest's `contentHash`/`bytes`/`scriptIslands` describe the region that actually shipped, and that no chunk carries an unindexed page.

**11. Test-suite honesty note (Task 9's warning, confirmed).** `LongTailRegion_IsTheSameCSharpRenderedContent_AsTheStaticPageMainBlock` did **not** "start passing harder" after Task 3, because it never covered the nav: it asserts `Assert.Contains(staticMain, region)` plus a bare "a `<nav class="site-nav">` exists". It was structurally blind to the local-context divergence 23.1 found. Its comment now says so, and points at the test that does cover it. `RenderSpaParityTests` needed no change — it builds `PageView`s directly and never exercises the capture path. `SpaDeliveryTests`, `SiteGeneratorSpaTests` and `SiteGeneratorWebviewTests` were extended; nothing in any existing suite had to be relaxed or rewritten.

**12. Live-browser verification (Task 11) — and what it could not do.** Served `SpecScribeOutput/` on :8103 and drove `app.html`: all 5 Git Pulse labels are links, a `code/*.html` one navigates client-side (URL, `data-path`, `document.title` and `<h1>` all update), an ADR page swapped in shows the `ADRs` local-context band with real pills and **no** generic key-views band, swapped regions carry **0** `<script>` nodes, and there are **0 console errors** across several client-side navigations — the `specscribe:content-swapped` re-init seam still fires (`data-explorer-ready` present after the swap).
**Screenshots were unavailable** — the Browser pane is not displayed in this session, so verification was by live computed DOM and navigation state rather than by looking. The owner's verify round should still *look* at the SPA dashboard and an ADR page.
**One observation, pre-existing and out of scope:** the Plotly hierarchy chart does not mount in the SPA, because `HtmlRenderAdapter.Render` is the only emitter of the `plotly-hierarchy.min.js` tag and `SpaDelivery.BuildEntryShell` has never emitted it. The ADR 0013 §2 text twin **is** present in the region, which is the contract. That gap belongs to Story 20.7 / 22.4, not here.

**13. Deviation from Task 1's first subtask, recorded deliberately.** The task said to work on a branch or worktree. I worked directly on `main`, per **CLAUDE.md § Concurrent work on shared `main`** — which states that the primary machine cannot run parallel worktrees, that isolation "is not available and is not the fix", and that concurrent editing is an accepted working condition. Branching in a shared working tree would also have captured another session's in-flight commits (the background auto-committer commits to whatever branch is checked out), which is worse than the status quo. That other session was demonstrably active throughout — see Completion Note 6. No `git reset --hard`, `git checkout --`, or `git clean` was run at any point, and every edit was grep-verified after writing.

**14. Scope guard held.** No delta transport, no incremental-route change, no client consumption of the hash; no rename of `spa/`; no static-HTML-from-the-IR; no chart-SVG retirement; `RegenerateEpics`'s work-graph over-count left alone for 22.5.

### File List

**Production**
- `src/SpecScribe/SpaDelivery.cs` — modified: `SchemaVersion`, `ChunkEnvelopeBytes`, `ContentHashHexLength`, `ExtractMetaDescription`, `ExtractNavMarkup`, `ScriptIsland` + `DataIslandKind`/`ExecutableScriptKind` + `ExtractScriptIslands`, `ContentHash`, `EncodedPage`, `ManifestHead`, `ManifestOversizedPage`; `BuildDataFiles` re-worked for exact-encoded-byte budgeting, assembled chunks, and the over-cap declaration; `MaxChunkBytes` doc comment rewritten
- `src/SpecScribe/SpaBundle.cs` — modified: `SpaPage.MetaDescription` (optional 5th positional)
- `src/SpecScribe/SiteGenerator.cs` — modified: `CodeItemHref` passed at both `BuildIndexPage` call sites (`RenderWebviewSurfaces`, `BuildSpaBundle`); new `CapturedNavMarkup` helper used by both capture loops; captured pages carry `ExtractMetaDescription`; `AddSpaSurface` carries `page.MetaDescription`
- `src/SpecScribe/assets/specscribe-spa.js` — modified: manifest-shape comment only (no behaviour change; the client still reads only `title` + `chunk`)

**Tests**
- `tests/SpecScribe.Tests/CanonicalIrSerializationTests.cs` — **new** (AC #3: the IR's golden round-trip boundary)
- `tests/SpecScribe.Tests/SpaDeliveryTests.cs` — modified: 10 new tests (chunk-assembly equivalence, escaping ceiling, over-cap declaration, schema version, nav/description/island extractors, hash determinism)
- `tests/SpecScribe.Tests/SiteGeneratorSpaTests.cs` — modified: 4 new tests (dashboard `<main>` parity, local-context band, manifest fields, two-run determinism); scope comment corrected on `LongTailRegion_…`
- `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs` — modified: 2 new tests (dashboard body parity minus stripped islands, captured-surface local-context band)

**Decision records & planning artifacts**
- `docs/adrs/0016-ir-carries-rendered-prose-html.md` — **new** (Proposed; AC #7)
- `docs/adrs/README.md` — modified: ADR 0016 index entry
- `docs/adrs/0008-json-ir-canonical-and-incremental-generation.md` — modified: amendment callouts on §Decision 1 and §Decision 4, plus References
- `_bmad-output/planning-artifacts/epics.md` — modified: Story 22.2 re-scope callout + the stale byte-blind-chunker premise struck through in the epic body
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — modified: 22.2 status + `last_updated`
- `_bmad-output/implementation-artifacts/22-2-canonical-ir-schema-and-versioning.md` — modified: this record

**Tooling**
- `.claude/launch.json` — modified: `ir-verify-22-2` preview entry (port 8103) for Task 11

## Change Log

| Date | Change |
|---|---|
| 2026-07-26 | Story 22.2 implemented. `spa/manifest.json` + `spa/pages-*.json` promoted in place into the versioned canonical IR: `schemaVersion` 1 with a documented compatibility rule; per page a head projection, a script-island declaration, a content hash and a byte size. Chunk budgeting moved from raw UTF-8 to exact JSON-encoded bytes so `MaxChunkBytes` bounds the file, with the one unsplittable single-page case declared in `oversizedPages` instead of shipping silently over cap. The lossy dashboard capture fixed on **both** the SPA and webview surfaces (`codeItemHref` was being skipped by named arguments at both call sites). Captured pages now keep their own page-local nav-context band, sliced from the pipeline's own output. New `CanonicalIrSerializationTests` is the IR's golden round-trip boundary. ADR 0016 authored (Proposed) amending ADR 0008 §Decision 1's prose half; `docs/adrs/README.md`, ADR 0008 and `epics.md` cross-reference it. Suite 2427 passed / 0 failed / 3 skipped; `GoldenContentFingerprint` unchanged. |
