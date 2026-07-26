---
baseline_commit: cd7f30255bb07112332c0876f4335e6b77ca9f4d
implements_decision: docs/adrs/0008-json-ir-canonical-and-incremental-generation.md # ADR 0008 Decision #1 (IR is canonical) — this story PROPOSES an amendment to §Decision 1 (AC #7)
amends_decision: docs/adrs/0013-text-twin-is-the-no-js-contract.md # ADR 0013 §5 already amended the SVG half of ADR 0008 §Decision 1; the prose-HTML half is amended here
gated_by: 22-1-spike-incremental-recompute-and-ir-delta-transport # verdict "Proceed, RE-SCOPED"
gates: [22-3, 22-4, 22-5, 22-6, 23-2, 23-3] # 22.6 explicitly "proceed only after 22.2 delivers page-level delta addressing"
owner_decisions: 2026-07-25 # (1) promote spa/ in place, (2) per-page hash + oversized-page cap, (3) 22.2 proposes the ADR amendment
---

# Story 22.2: Canonical IR Schema + Versioning

Status: ready-for-dev

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

- [ ] **Task 1 — Branch, baseline, and read-before-edit** (AC: all)
  - [ ] Work on a branch or worktree, not directly on `main` (`main` has a background auto-committer and concurrent sessions — memory: [[shared-main-concurrent-edit-loss-verify-after-edit]], [[worktree-edits-must-target-worktree-path]]). If in a worktree, resolve every relative path against the **worktree** root.
  - [ ] **Grep-verify every line reference in this file before trusting it.** They were verified at `cd7f302` but `SiteGenerator.cs` is 4,000+ lines and moves. Verify by **symbol**, not line number.
  - [ ] Read completely before editing: [`SpaDelivery.cs`](../../src/SpecScribe/SpaDelivery.cs), [`SpaBundle.cs`](../../src/SpecScribe/SpaBundle.cs), [`JsonSpaRenderAdapter.cs`](../../src/SpecScribe/JsonSpaRenderAdapter.cs), `SiteGenerator.BuildSpaBundle` / `AddSpaSurface` / `EmitSpaSite`, and [`WebviewRenderAdapter.RenderContent`](../../src/SpecScribe/WebviewRenderAdapter.cs).
  - [ ] Record the **pre-change** `GoldenContentFingerprint` value and a full `dotnet test SpecScribe.slnx -c Release` baseline (pass/fail/skip counts) before touching anything — you need it to prove AC #4's "static bytes unchanged".

- [ ] **Task 2 — Fix the lossy capture: `codeItemHref` on the SPA and webview dashboard** (AC: #4)
  - [ ] `SiteGenerator.BuildSpaBundle` calls `HtmlTemplater.BuildIndexPage(...)` with named arguments starting at `counts:`, **skipping the positional `codeItemHref`** (≈`SiteGenerator.cs:3051`). The webview path does the identical thing (≈`:2784`). The static path passes `CodeItemHref` (≈`:3226`, via `HtmlTemplater.RenderIndex`). **Pass `CodeItemHref` at both omitting sites.**
  - [ ] Confirm the mechanism end-to-end before and after: `HtmlTemplater.BuildIndexPage` → `HtmlRenderAdapter.RenderDashboardBody(view, codeItemHref, …)` → `Charts.GitPulsePanel(pulse, codeItemHref, …)` → `Charts.CodeItemLink(path, fileHref)`. With a null `fileHref`, `CodeItemLink` degrades the bar label to plain text.
  - [ ] Assert the anchor count in `<main>`: **548 → 553** on the dashboard IR region, matching the static page.
  - [ ] ⚠️ **Webview link-target hazard — check this, do not assume.** `CodeItemHref` can return (a) a rendered artifact page, (b) a `code/…html` page, or (c) an **external** `BuildExternalSourceUrl` link for an on-disk file with no code page. The webview bridge resolves relative links against `data-path` and is read-only. Verify a restored link resolves in the webview (the whole site is captured, so `code/*.html` should exist) and that an **external** href behaves acceptably there. If it does not, gate the webview's resolver to in-portal hrefs only and **register the divergence in `HostRenderExceptions`** — an unregistered divergence is a bug by house rule.
  - [ ] Prove the static site is byte-unchanged: `GoldenContentFingerprint` must equal the Task 1 baseline.

- [ ] **Task 3 — Preserve page-local nav context on captured pages** (AC: #5)
  - [ ] Root cause (verified): family pages go through `AddSpaSurface` → `JsonSpaRenderAdapter.RenderContent(page)` and use `page.Nav`, which **does** carry `NavLocalContext` (e.g. `EpicsTemplater` builds "Stories in this epic"). **Captured** pages go through the long-tail loop, which re-renders nav as `RenderNavMarkup(nav.ToNavigationView(normalized))` — **no local context argument** — so an ADR page loses its `aria-label="ADRs"` band and gets the generic key-views nav instead. That is exactly 23.1's enumerated difference #2.
  - [ ] The local context for captured pages is built **inline at render time** and thrown away (`new NavLocalContext(…)` at ≈`SiteGenerator.cs:1123` for ADRs, `:1313` commit days, `:1566` commits, `:2095`, `:4072`; plus `SiteNav.BuildInsightsLocalContext` / `BuildDeliveryLocalContext` / `BuildSddLocalContext`, `EpicsTemplater:259`, `RequirementsTemplater:658`). **There is no path → local-context resolver**, so do not try to re-derive it from the path.
  - [ ] **Recommended fix — slice the page's own nav out of the capture.** `_spaCapture` already holds the full page string the pipeline rendered (never a disk read-back — preserve that AD-1/AD-2 boundary). The nav is a contiguous `<nav class="site-nav"` … first following `</nav>` block, and the inline `NavToggleScript` follows it immediately and must be excluded (the client owns the toggle via delegation). This is byte-faithful and needs no plumbing — the same discipline `ExtractContentRegion` and `ExtractBreadcrumb` already use.
  - [ ] Alternative if slicing proves fragile: thread the `NavigationView` (or `NavLocalContext`) into `_spaCapture` at each render site. **More faithful but ~8 call sites of plumbing** — take it only if the slice can't be made robust, and say so in Completion Notes.
  - [ ] Whichever route: a captured ADR page's IR region must contain `site-nav-local-context` with the correct `aria-label`, and a page that genuinely has no local context must be **unchanged**.

- [ ] **Task 4 — Head/meta projection** (AC: #5)
  - [ ] Add a structured head projection per page to the manifest. **Minimum viable and non-redundant: `title` + `description`.** Document the derivation rule the static site already uses in `PathUtil.RenderHeadOpen`: `description` falls back to `title` when absent; `og:title` mirrors `title`; `og:description` mirrors `description`; `og:type` is the constant `"website"`; the favicon is a constant data-URI. A consumer then reproduces the full head without the IR shipping four near-duplicate strings per page.
  - [ ] Source it correctly per page class: family pages already carry `PageView.MetaDescription` (nullable, falls back to `Title`). Captured pages need the same extraction discipline as `SpaDelivery.ExtractTitle` — pull `<meta name="description" content="…">` from the captured string and HTML-decode it.
  - [ ] Do **not** carry the `?v={AssetVersion}` cache-bust in the IR head projection: it is a build token, it is already exposed via `PathUtil.CurrentAssetVersion` / the shell's `data-asset-version`, and putting it in per-page data would make every page's bytes churn on every build.

- [ ] **Task 5 — Declare the embedded script islands** (AC: #5, #1)
  - [ ] Emit, per page, a declaration of the embedded scripts a consumer must strip or nonce. **Two distinct kinds exist today — the current webview handling covers only the first:**
    - **Inert JSON data islands** — `<script type="application/json" id="sunburst-explorer-data">` ([`SunburstExplorer.cs:62,269`](../../src/SpecScribe/SunburstExplorer.cs)) and `<script type="application/json" class="ss-hierarchy-data" id="{domId}-data">` ([`HierarchyExplorer.cs:425`](../../src/SpecScribe/HierarchyExplorer.cs)). The dashboard's island alone is **20,915 B** (23.1 Axis 3). `WebviewRenderAdapter` strips these by regex today.
    - **An executable inline script** — the Story 20.5 JS-present handshake at [`HierarchyExplorer.cs:349`](../../src/SpecScribe/HierarchyExplorer.cs), which is a bare `<script>`, **not** `type="application/json"`. The webview's `JsonDataIsland` regex does **not** match it, so it survives into the webview region and is blocked by the CSP with no nonce. **Verify this against live output** and record what you find — if it is reaching the webview unhandled, that is a second live fidelity/CSP finding and it belongs in the declaration and in Completion Notes.
  - [ ] The declaration is the ADR 0013 §5 "chart data + component configuration" hook: `HierarchyExplorer.IslandHtml` already carries the component **config** alongside the nodes. Declaring the islands makes that first-class IR metadata rather than something a consumer must regex out of an HTML string.
  - [ ] Shape suggestion (dev's call, but keep it flat and JSON-trivial): per page, a list of `{ id, kind }` where `kind` distinguishes inert data from executable — that is precisely the strip-vs-nonce decision a consumer has to make.

- [ ] **Task 6 — Schema version + compatibility rule** (AC: #1)
  - [ ] Add a `schemaVersion` field to the manifest, backed by a named constant in `SpaDelivery` (alongside `MaxChunkBytes` / `MaxPagesPerChunk`), with a doc comment stating the rule. **Recommended: a monotonically increasing integer**, bumped on any breaking change to manifest or chunk shape; additive fields do not bump. Rationale: consumers do a single integer compare; there is no independent release cadence to justify semver.
  - [ ] Set the initial value to **1** and state in the doc comment that the pre-22.2 unversioned form is version 0 by implication (there is no shipped consumer outside this repo to migrate).

- [ ] **Task 7 — Byte-bounded chunking with no escape hatch** (AC: #2)
  - [ ] Read `SpaDelivery.BuildDataFiles`'s batching loop and `GroupBatchState`. Today an oversized page is *isolated* into its own dedicated batch — which bounds its blast radius on neighbours but **does not bound the file**, which is why 22.1 measured a 3.08 MB chunk against a 2 MB guard.
  - [ ] Close it. A page's content region is **atomic** (the existing doc comment is explicit and correct — do not split a region mid-HTML). So the ceiling must be honoured by *placement*, and an unavoidably-oversized single page must be **declared** rather than silently over-cap: record its real size in the manifest (Task 8 gives you the field) and, if the design still emits an over-cap file, state the ceiling as a *target with a declared exception* in the constant's doc comment and in Completion Notes. **Do not leave a silent over-cap** — memory: no silent caps.
  - [ ] Keep the existing `MaxChunkBytes` **approximation** caveat honest: it budgets raw UTF-8 `ContentHtml` bytes, not JSON-escaped output, where `<`/`>`/`&` each balloon to 6 bytes. If you tighten the ceiling claim, tighten the measurement to match — or restate the caveat rather than quietly implying precision you don't have.
  - [ ] Extend `SpaDeliveryTests` at the boundary: the existing `BuildDataFiles_IsolatesAnOversizedPage_*` trio (mid-group, first-in-group, last-in-group) is the pattern; add the over-cap case they don't currently pin.

- [ ] **Task 8 — Per-page content hash + byte size** (AC: #6)
  - [ ] Add `contentHash` and `bytes` to each manifest page entry. Use a deterministic, reproducible hash of the UTF-8 content region (SHA-256, hex; truncation is fine if documented). **NFR9 (reproducible CI) applies — no `Random`, no time, no machine-dependent input.**
  - [ ] ⚠️ **Volatility trap — this is the one most likely to bite.** The hash inherits whatever volatility lives inside the captured region. The golden gate folds footer clock, `?v=` cache-bust, and version/build rows via `NormalizeVolatile`; those live *outside* `<main>`, but **prove it** rather than assume. If any volatile token is inside a region, the hash reports a false change on every run and is worthless to 22.5/22.6. **Confirm stability by generating twice from unchanged input and diffing the manifests** — repeated runs, not a single assertion (memory: [[golden-diff-normalization-gotchas]]).
  - [ ] Record in Completion Notes which regions, if any, needed normalization and why — 22.5 will build directly on this answer.
  - [ ] **Stop at addressing.** No `specscribe-spa.js` change, no delta emission, no watch-route change. 22.1's gate explicitly holds 22.6 until this lands; it does not ask this story to build it.

- [ ] **Task 9 — Golden boundary test + suite** (AC: #3, and guards #1–#6)
  - [ ] Generalize the `SectionViewModelSerializationTests` pattern (`AssertRoundTripsLossless`: serialize → deserialize → re-serialize → compare JSON strings, because record value-equality reference-compares collection members) up to the **whole IR document** — manifest and chunks.
  - [ ] Update the existing suites that will legitimately move: `SpaDeliveryTests`, `SiteGeneratorSpaTests` (`Manifest_CarriesTheNavGraphAndPerPageBreadcrumbDrillData`, `Manifest_AndChunks_RoundTrip_EveryPageResolvesToItsRegion`, `LongTailRegion_IsTheSameCSharpRenderedContent_AsTheStaticPageMainBlock`), `SiteGeneratorWebviewTests`, `RenderSpaParityTests`.
  - [ ] ⚠️ **`LongTailRegion_IsTheSameCSharpRenderedContent_AsTheStaticPageMainBlock` is the test most likely to *start passing harder* after Task 3.** If it currently passes while the nav differs, check whether it compares only the `<main>` block — and if so, say so rather than claiming it proved something it didn't (memory: a test's prose is not its coverage — grep every test docstring you touch against its actual `Assert`s).
  - [ ] Full suite green: `dotnet test SpecScribe.slnx -c Release`. Last recorded green on `main` was **2394 / 0 failed / 0 skipped** (Story 25.1 CI gate) — treat that as indicative and use **your own Task 1 capture** as the authoritative baseline.

- [ ] **Task 10 — ADR 0016 + artifact updates** (AC: #7)
  - [ ] Author `docs/adrs/0016-<slug>.md` (0001–0015 are taken — verified). It amends **ADR 0008 §Decision 1**: the IR carries **Markdig-rendered prose HTML strings** + **chart data and component configuration** + the **server-rendered text twin** — not re-modelled view models and not pre-rendered SVG. State the consequence plainly: 23.1's finding that the ~889 LOC of custom Markdig renderers are not a fidelity risk **holds only under this amendment**; without it, that risk returns in full along with a ~4,691 LOC templater reimplementation Epic 23 assumes it can avoid.
  - [ ] Follow the house ADR shape (Context / Decision / Consequences / Options considered / Ratified decisions / References) and mark it **Proposed** — ratification is the owner's, not the dev agent's.
  - [ ] Cross-reference: add it to `docs/adrs/README.md`, to ADR 0008's References, and note it in the Epic 22 body.
  - [ ] **`epics.md` + `sprint-status.yaml` in the same change** (CLAUDE.md rule): record that Story 22.2's ACs were re-scoped by the 22.1 gate and the 23.1 fold-in, that epics.md AC #2's byte-blind-chunker premise is stale, and update the story status. A scope change recorded in only one artifact is a drift bug.

- [ ] **Task 11 — Verify in a live browser, not only in tests** (AC: #4, #5)
  - [ ] Generate to `SpecScribeOutput/` (never `--output docs/live` — vestigial and gitignored) with `--spa`, and open the SPA entry shell. **CLAUDE.md § Verification:** the suite structurally cannot see what a rendered page actually does.
  - [ ] Confirm on the dashboard: the git-pulse bar labels are **links** in the SPA, and they navigate.
  - [ ] Confirm on an ADR page in the SPA: the page-local context band renders with the right label, not the generic key-views nav.
  - [ ] Confirm no console errors after a client-side navigation (the region swap fires `specscribe:content-swapped`; new markup must not break the explorer re-init seam).

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

### Debug Log References

### Completion Notes List

### File List
